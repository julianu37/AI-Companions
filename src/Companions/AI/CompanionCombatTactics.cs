using System.Collections.Generic;
using UnityEngine;

namespace Companions;

// 1.5.52: combat part 2 - melee fighting like a player (user, 2026-10-15): "Most of the time I will do 2-3 quick swings
// with a fast weapon, then block again. Less swings with a slower weapon. All the time while moving a repositioning";
// "when blocking multiple enemies ... focus on blocking and getting clear of the group and attack when there is an
// opening"; "Often times its better to just take a step back or to the side"; "prioritize staying alive and reducing
// damage, then attacking. Trying to stay out of hit range to recharge stamina when possible"; "We also dont want to make
// the NPC perfect like some super robot."
//
// - Combos: the game carries a combo on only when the next swing starts within 0.2 s of the last one ending
//   (Attack.Start), and melee used to wait for the weapon's AI attack interval (2 s on player weapons) - so it never
//   comboed. Now it swings in bursts of 1-3 straight after each other, then pauses.
// - Openings: it swings when nothing is swinging at it, its target is staggered, or the swing at it has already landed
//   (blocked or hit) and the attacker is finishing it.
// - Stamina: it keeps a reserve for blocking; below it, it backs out of reach to get its breath back, and only fights
//   on if it can't get clear.
// - Groups: with several on it from different sides, it moves until they're all on one side.
// - Steps: sometimes it steps back or aside out of a swing's reach instead of blocking (UpdateBlocking).
// - All chance-based with a varied reaction time, so it gets it wrong now and then.
public partial class CompanionCombatAI
{
	private bool _tacticsOn;

	private int _burstLeft;

	private bool _burstPending;

	private float _burstPauseUntil;

	private Vector3 _stepDir;

	private float _stepUntil;

	private bool _stepRun;

	private Character _stepFrom;

	private bool _recovering;

	private float _recoverSince;

	private float _recoverStepReady;

	private bool _cornered;

	private float _corneredUntil;

	private float _repositionSince = -1f;

	private float _repositionBlockedUntil;

	private Vector3 _moveDir;

	private float _moveDirUntil;

	private float _swingStartedAt;

	private float _swingLandedAt = -1f;

	private bool _evading;

	private string _lastTacticLog;

	private float _nextTacticLog;

	private readonly List<Character> _attackers = new List<Character>();

	private static float Reserve => ModConfig.CombatStaminaReserve?.Value ?? 0.3f;

	private static float Resume => Mathf.Max(Reserve, ModConfig.CombatStaminaResume?.Value ?? 0.5f);

	internal static bool FightLikePlayer => ModConfig.FightLikePlayer?.Value ?? true;

	private bool TacticsFor(ItemDrop.ItemData weapon)
	{
		// 1.5.73: no stepping away from a tick latched on - it comes along; swing at it (MHR_Latched)
		return FightLikePlayer && weapon != null && !MHR_IsProjectileWeapon(weapon) && _targetCreature != null && !_stalking && !Unaware(_targetCreature) && !LatchedTarget();
	}

	private void TLog(string s)
	{
		if (s != _lastTacticLog || Time.time >= _nextTacticLog)
		{
			_lastTacticLog = s;
			_nextTacticLog = Time.time + 5f;
			if (CompanionsPlugin.LogDetailed) CompanionsPlugin.Log.LogDebug("[Tactics] " + s);
		}
	}

	// how far (centre to centre) a creature's swing reaches us
	// 1.5.53: the swing's real reach (MHR_MeleeReach: the AI range, but no more than the attack's own range + 0.5), not
	// the AI range alone - that's where the AI starts a swing, often well short of where it lands, so steps went too far
	internal static float SwingReach(Character c)
	{
		ItemDrop.ItemData w = (c as Humanoid)?.GetCurrentWeapon();
		return (w != null && w.m_shared != null) ? MHR_MeleeReach(w) : 2f;
	}

	private float Danger(Character c)
	{
		return SwingReach(c) + c.GetRadius() + _character.GetRadius();
	}

	// enemies on this companion: its target and anything close whose target is this companion
	private void GatherAttackers()
	{
		_attackers.Clear();
		float range = ModConfig.CombatGroupRange?.Value ?? 6f;
		Vector3 me = base.transform.position;
		foreach (Character c in Character.GetAllCharacters())
		{
			if (c == null || c == _character || c.GetHealth() <= 0f || !BaseAI.IsEnemy(_character, c))
			{
				continue;
			}
			if (Vector3.Distance(me, c.transform.position) > range)
			{
				continue;
			}
			if (c != _targetCreature && c.GetBaseAI()?.GetTargetCreature() != _character)
			{
				continue;
			}
			_attackers.Add(c);
		}
	}

	private Character NearestAttacker(out float dist)
	{
		Character best = null;
		dist = float.MaxValue;
		foreach (Character c in _attackers)
		{
			// 1.5.66: a creature killed or unloaded since the list was made (NullReferenceException x968, log b78220a8)
			if (c == null)
			{
				continue;
			}
			float d = Vector3.Distance(base.transform.position, c.transform.position);
			if (d < dist)
			{
				dist = d;
				best = c;
			}
		}
		return best;
	}

	// attackers on different sides (more than FlankAngle apart, seen from here)
	private readonly List<Character> _close = new List<Character>();

	// 1.5.55: the attackers that can actually reach it - within their reach + 1.5 m (log d973bc65: everything targeting it
	// within 6 m counted, so with 3-5 skeletons about it was "getting clear" over and over)
	private void GatherClose()
	{
		_close.Clear();
		foreach (Character c in _attackers)
		{
			if (c == null)
			{
				continue;
			}
			if (Vector3.Distance(base.transform.position, c.transform.position) < Danger(c) + 1.5f)
			{
				_close.Add(c);
			}
		}
	}

	private bool Flanked()
	{
		float limit = ModConfig.CombatFlankAngle?.Value ?? 100f;
		Vector3 me = base.transform.position;
		for (int i = 0; i < _close.Count; i++)
		{
			Vector3 a = _close[i].transform.position - me;
			a.y = 0f;
			for (int j = i + 1; j < _close.Count; j++)
			{
				Vector3 b = _close[j].transform.position - me;
				b.y = 0f;
				if (Vector3.Angle(a, b) > limit)
				{
					return true;
				}
			}
		}
		return false;
	}

	// the safe direction that gets furthest from the attackers (and a little towards the owner when it's close by)
	private Vector3 BestDir(float dist, Character from)
	{
		Vector3 me = base.transform.position;
		Vector3 toOwner = Vector3.zero;
		Player owner = MHR_Owner.Of(this);
		if (owner != null)
		{
			Vector3 o = owner.transform.position - me;
			o.y = 0f;
			if (o.magnitude > 2f && o.magnitude < 30f)
			{
				toOwner = o.normalized;
			}
		}
		Vector3 best = Vector3.zero;
		float bestScore = float.MinValue;
		for (int i = 0; i < 12; i++)
		{
			Vector3 d = Quaternion.Euler(0f, i * 30f, 0f) * Vector3.forward;
			float score = 0.5f * Vector3.Dot(d, toOwner);
			foreach (Character c in _attackers)
			{
				if (c == null)
				{
					continue;
				}
				Vector3 t = c.transform.position - me;
				t.y = 0f;
				float w = (c == from) ? 2f : 1f;
				score -= w * Vector3.Dot(d, t.normalized) / Mathf.Max(1f, t.magnitude * 0.3f);
			}
			if (from != null && !_attackers.Contains(from))
			{
				Vector3 t = from.transform.position - me;
				t.y = 0f;
				score -= 2f * Vector3.Dot(d, t.normalized);
			}
			score += Random.Range(0f, 0.15f);   // two near-equal ways: not always the same one
			if (score > bestScore && MHR_Mover.SafeStep(me, d, dist))
			{
				bestScore = score;
				best = d;
			}
		}
		return best;
	}

	// moves that way while facing the enemy (backing off, stepping aside). BaseAI.MoveTowards with smooth movement only
	// ever walks forward (moveDir = forward x how well it's lined up), so the move direction is set directly; a
	// non-player turns to where it looks (Character.AlwaysRotateCamera is true), not where it moves.
	private void Move(Vector3 dir, bool run, Character face)
	{
		dir.y = 0f;
		_character.SetMoveDir(dir.normalized);
		_character.SetRun(run);
		if (face != null)
		{
			_ai.LookAtPoint(face.GetCenterPoint());
		}
		else
		{
			_ai.LookTowards(dir);
		}
	}

	private float AttackCost(ItemDrop.ItemData w)
	{
		Attack a = w?.m_shared?.m_attack;
		if (a == null)
		{
			return 0f;
		}
		return a.m_attackStamina * (1f - 0.33f * _character.GetSkillFactor(w.m_shared.m_skillType));
	}

	private float StaminaNow()
	{
		StaminaShare();
		return _stam != null ? _stam.Stamina : 999f;
	}

	private float StaminaMax()
	{
		StaminaShare();
		return (_stam != null && _stam.MaxStamina > 0f) ? _stam.MaxStamina : 100f;
	}

	// 0 = carry on as normal (chase, swing when ReadyToSwing), 1 = it moved this tick, 2 = hold here (swing only if ready)
	private int Tactics(float dt, ItemDrop.ItemData weapon)
	{
		_spacingHold = false;
		GatherAttackers();
		TrackDamage();
		if (_burstPending && !_character.InAttack())
		{
			_burstPending = false;
			float pause = ModConfig.CombatComboPause?.Value ?? 0.5f;
			_burstPauseUntil = Time.time + pause * Random.Range(0.6f, 1.4f);
			if (!MaybeClearOfWalls(weapon))
			{
				MaybeStepOut();
			}
		}
		Character near = NearestAttacker(out var nearDist);
		// a step out of a swing or out of reach under way
		if (Time.time < _stepUntil)
		{
			if (!_character.InAttack())
			{
				// 1.5.53: stop as soon as it's out of reach (user: "moving back a lot and not re-engaging after" - the
				// step ran for a set time, at a run, and covered 4-6 m)
				if (_stepToReach && _stepFrom != null && Vector3.Distance(base.transform.position, _stepFrom.transform.position) >= Danger(_stepFrom) + 0.3f)
				{
					_stepUntil = 0f;
				}
				else if (!MHR_Mover.SafeStep(base.transform.position, _stepDir, 0.8f))
				{
					_stepUntil = 0f;
				}
				else
				{
					Move(_stepDir, _stepRun, _stepFrom ?? near);
					return 1;
				}
			}
			else
			{
				return 1;
			}
		}
		if (_character.InAttack())
		{
			return 0;
		}
		// a target nearly dead, or staggered: close in and hit it (ReadyToSwing) rather than back off or reposition
		if ((_finishing && _burstLeft > 0) || FinishOff(weapon) || Punish(weapon))
		{
			_stepUntil = 0f;
			return 0;
		}
		// stamina: keep enough to block with; get out of reach to get it back
		float share = StaminaShare();
		if (!_recovering && share < Reserve)
		{
			_recovering = true;
			_recoverSince = Time.time;
			_burstLeft = 0;
			if (CompanionsPlugin.LogDetailed) TLog($"Low on stamina ({share:P0}) - backing out of reach to get its breath back");
		}
		else if (_recovering && share >= Resume)
		{
			_recovering = false;
			_cornered = false;
			if (CompanionsPlugin.LogDetailed) TLog($"Got its breath back ({share:P0}) - back in");
		}
		if (_cornered && Time.time >= _corneredUntil)
		{
			_cornered = false;
			_recoverSince = Time.time;
		}
		if (_recovering && !_cornered)
		{
			// 1.5.56: a step back to just past the weapon's reach, then guard - not a retreat (user: "It should just be a
			// step back to get beyond the hit window for the melee weapon.")
			Character close = null;
			foreach (Character c in _attackers)
			{
				if (c == null)
				{
					continue;
				}
				if (Vector3.Distance(base.transform.position, c.transform.position) < Danger(c) + 0.3f)
				{
					close = c;
					break;
				}
			}
			if (close == null)
			{
				// nothing on it: walk (not run - stamina comes back while walking) up to just outside the target's reach,
				// ready to go back in once it has its breath back
				Character t = _targetCreature;
				if (t != null && Vector3.Distance(base.transform.position, t.transform.position) > Danger(t) + 3f)
				{
					MHR_Mover.Go(_ai, dt, t.transform.position, Danger(t) + 2f, allowRun: false, "Combat", MHR_Mover.SpeedJog);
					return 1;
				}
				_ai.StopMoving();
				if (near != null)
				{
					_ai.LookAtPoint(near.GetCenterPoint());
				}
				return 2;
			}
			if (Time.time - _recoverSince > (ModConfig.CombatRecoverTime?.Value ?? 8f))
			{
				return Cornered(close.m_name + " keeps after it");
			}
			if (Time.time >= _recoverStepReady)
			{
				float need = Mathf.Max(0.6f, Danger(close) + 0.4f - Vector3.Distance(base.transform.position, close.transform.position));
				Vector3 back = base.transform.position - close.transform.position;
				back.y = 0f;
				Vector3 dir = (back.sqrMagnitude > 0.01f && !TowardsOther(back.normalized, close) && MHR_Mover.SafeStep(base.transform.position, back.normalized, need)) ? back.normalized : EvadeDir(close, need);
				if (dir == Vector3.zero)
				{
					return Cornered("nowhere to step back to");
				}
				StartStep(dir, need, close, run: false);
				_recoverStepReady = Time.time + 1.5f;
				if (CompanionsPlugin.LogDetailed) TLog("Stepping " + StepWord(close, dir) + $" out of {close.m_name}'s reach to get its breath back ({need:F1} m)");
				return 1;
			}
			// it followed straight back in: guard (blocks still happen in UpdateBlocking) rather than keep retreating
			_ai.StopMoving();
			_ai.LookAtPoint(close.GetCenterPoint());
			return 2;
		}
		// several on it from different sides: get them all on one side first
		int groupSize = ModConfig.CombatGroupSize?.Value ?? 2;
		GatherClose();
		if (_close.Count >= groupSize && Flanked() && Time.time >= _repositionBlockedUntil)
		{
			if (_repositionSince < 0f)
			{
				_repositionSince = Time.time;
				if (CompanionsPlugin.LogDetailed) TLog($"{_close.Count} on it from different sides - getting clear");
			}
			// a couple of seconds at most, then it fights where it stands for a while
			if (Time.time - _repositionSince > 2.5f)
			{
				_repositionSince = -1f;
				_repositionBlockedUntil = Time.time + 6f;
				if (CompanionsPlugin.LogDetailed) TLog("Couldn't get them on one side - fighting where it stands");
				return 0;
			}
			if (Time.time >= _moveDirUntil)
			{
				_moveDir = BestDir(2f, null);
				_moveDirUntil = Time.time + 0.3f;
			}
			if (_moveDir != Vector3.zero)
			{
				Move(_moveDir, run: StaminaShare() > Resume, near);
				return 1;
			}
		}
		else
		{
			if (_repositionSince >= 0f)
			{
				// got them on one side: fight from here for a while before moving again
				_repositionBlockedUntil = Time.time + 4f;
			}
			_repositionSince = -1f;
		}
		return Spacing(dt, weapon);
	}

	// 1.5.58: spacing, like the user fights (DEV_NOTES 1.5.57: "stay just out of range and then dip in to attack then back
	// out. And block when needed to either parry or create an opening"). One enemy on it (the others aren't close enough
	// to hit): it keeps just outside that enemy's reach; when the enemy can't swing again before it gets there and hits
	// (MonsterAI: the weapon's m_aiAttackInterval since its last swing, and m_minAttackInterval), it dips in for a combo,
	// then steps back out (MaybeStepOut). An enemy walking in and swinging is met by UpdateBlocking: parry it, or step
	// back out of the swing (more likely when it only needs a short step), then dip in while it recovers.
	private bool _dipping;

	private float _dipUntil;

	internal static bool SpacingOn => ModConfig.CombatSpacing?.Value ?? true;

	// seconds until it can start another swing (0 = it can now)
	private static float EnemyReadyIn(Character c)
	{
		float t = 0f;
		try
		{
			ItemDrop.ItemData w = (c as Humanoid)?.GetCurrentWeapon();
			if (w != null && w.m_shared != null)
			{
				t = w.m_lastAttackTime + w.m_shared.m_aiAttackInterval - Time.time;
			}
			if (c.GetBaseAI() is MonsterAI m)
			{
				t = Mathf.Max(t, m.m_minAttackInterval - c.GetTimeSinceLastAttack());
			}
		}
		catch
		{
		}
		return Mathf.Max(0f, t);
	}

	// spacing applies to this fight (ignoring the combo just finished)
	private bool SpacingTarget()
	{
		Character t = _targetCreature;
		return t != null && _close.Count <= 1 && (_close.Count == 0 || _close[0] == t) && t.GetBaseAI()?.GetTargetCreature() == _character;
	}

	private bool SpacingFor(Character t)
	{
		return SpacingOn && t != null && _burstLeft <= 0 && _close.Count <= 1 && (_close.Count == 0 || _close[0] == t) && t.GetBaseAI()?.GetTargetCreature() == _character;
	}

	// 1.5.59: flanking (user: "If the enemies are not focused on the NPC the NPC should try to get behind them if possible").
	// The game's backstab bonus only lands on a creature that isn't alerted (Character.RPC_Damage: !IsAlerted, once per
	// 300 s), so one fighting the player gets none - but its swings go where it faces, at the player, so from behind the
	// companion hits freely without blocking. While the target is after someone else and nothing else is close enough to
	// hit it, it works round to the target's back (via the side it's on) at a jog, then attacks; gives up after 4 s and
	// attacks from where it is for 6 s.
	private float _flankSince = -1f;

	private float _flankBlockedUntil;

	private bool _flankLogged;

	private int Flank(float dt, ItemDrop.ItemData weapon)
	{
		Character t = _targetCreature;
		Character itsTarget = t?.GetBaseAI()?.GetTargetCreature();
		if (!(ModConfig.CombatFlank?.Value ?? true) || t == null || itsTarget == null || itsTarget == _character || _burstLeft > 0 || _close.Count > 1 || (_close.Count == 1 && _close[0] != t) || t.IsStaggering() || Time.time < _flankBlockedUntil)
		{
			_flankSince = -1f;
			return 0;
		}
		Vector3 me = base.transform.position;
		Vector3 from = me - t.transform.position;
		from.y = 0f;
		Vector3 fwd = t.transform.forward;
		fwd.y = 0f;
		float angle = Vector3.Angle(fwd, from);   // 0 = in front of it, 180 = right behind
		if (angle >= 110f)
		{
			if (_flankSince >= 0f)
			{
				if (CompanionsPlugin.LogDetailed) TLog("Behind " + t.m_name + " - attacking while it's busy with " + itsTarget.m_name);
			}
			_flankSince = -1f;
			return 0;
		}
		if (_flankSince < 0f)
		{
			_flankSince = Time.time;
			_flankLogged = false;
		}
		if (Time.time - _flankSince > 4f)
		{
			_flankSince = -1f;
			_flankBlockedUntil = Time.time + 6f;
			if (CompanionsPlugin.LogDetailed) TLog("Couldn't get behind " + t.m_name + " - attacking from here");
			return 0;
		}
		if (!_flankLogged)
		{
			_flankLogged = true;
			if (CompanionsPlugin.LogDetailed) TLog(t.m_name + " is busy with " + itsTarget.m_name + " - working round behind it");
		}
		// round by its side first (the side it's already on), then to its back, at about our swing reach
		float r = Mathf.Max(1f, MHR_MeleeReach(weapon) + t.GetRadius() + _character.GetRadius() - 0.3f);
		Vector3 right = Vector3.Cross(Vector3.up, fwd.normalized);
		float side = Vector3.Dot(from, right) >= 0f ? 1f : -1f;
		Vector3 goal = angle < 70f ? t.transform.position + right * side * r : t.transform.position - fwd.normalized * r;
		goal.y = me.y;
		// 1.5.67: only round it over safe ground - no drop, deep water or fire on the way (log 26d28214: working round a
		// Seeker on a ledge in the mines it walked off a 7 m drop)
		Vector3 way = goal - me;
		way.y = 0f;
		float wayLen = way.magnitude;
		if (wayLen > 0.3f && !MHR_Mover.SafeStep(me, way / wayLen, wayLen))
		{
			_flankSince = -1f;
			_flankBlockedUntil = Time.time + 6f;
			if (CompanionsPlugin.LogDetailed) TLog("No safe way round " + t.m_name + " (a drop or water) - attacking from here");
			return 0;
		}
		MHR_Mover.Go(_ai, dt, goal, 0.4f, allowRun: StaminaShare() > Resume, "Combat", MHR_Mover.SpeedJog);
		return 1;
	}

	private int Spacing(float dt, ItemDrop.ItemData weapon)
	{
		_spacingHold = false;
		Character t = _targetCreature;
		int flank = Flank(dt, weapon);
		if (flank != 0)
		{
			_dipping = false;
			return flank;
		}
		if (!SpacingFor(t))
		{
			_dipping = false;
			return 0;
		}
		Vector3 me = base.transform.position;
		float d = Vector3.Distance(me, t.transform.position);
		float hover = Danger(t) + (ModConfig.CombatSpacingMargin?.Value ?? 0.4f);
		float ourReach = MHR_MeleeReach(weapon) + t.GetRadius() + _character.GetRadius();
		// within our own reach and outside its: hit it from here
		if (d <= ourReach - 0.2f && d > Danger(t))
		{
			_dipping = false;
			return 2;
		}
		// time to get into reach (jogging ~4 m/s) and get a swing off (~0.35 s)
		float closeIn = Mathf.Max(0f, d - (ourReach - 0.3f)) / 4f + 0.35f;
		float ready = EnemyReadyIn(t);
		bool open = t.IsStaggering() || (!t.InAttack() && ready > closeIn);
		if (_dipping && (Time.time > _dipUntil || t.InAttack()))
		{
			_dipping = false;
		}
		if (!_dipping && open && Time.time >= _burstPauseUntil && StaminaNow() - AttackCost(weapon) >= Reserve * StaminaMax())
		{
			_dipping = true;
			_dipUntil = Time.time + closeIn + 0.6f;
			if (CompanionsPlugin.LogDetailed) TLog($"{t.m_name} can't swing again for {ready:F1} s - going in");
		}
		if (_dipping)
		{
			return 0;   // the chase closes in, ReadyToSwing starts the combo
		}
		// hold just outside its reach, facing it; walk up if further off
		if (d > hover + 0.5f)
		{
			MHR_Mover.Go(_ai, dt, t.transform.position, hover, allowRun: false, "Combat", MHR_Mover.SpeedJog);
			_ai.LookAtPoint(t.GetCenterPoint());
			return 1;
		}
		if (CompanionsPlugin.LogDetailed) TLog($"Keeping just out of {t.m_name}'s reach (its reach {Danger(t):F1} m) - waiting for an opening");
		_spacingHold = true;
		_ai.StopMoving();
		_ai.LookAtPoint(t.GetCenterPoint());
		return 2;
	}

	// holding off for an opening: no combo started just because it walked into reach (it's about to swing - parry or step
	// out of it instead, UpdateBlocking); a stagger or a finishing blow still goes ahead
	private bool _spacingHold;

	private int Cornered(string why)
	{
		_cornered = true;
		_corneredUntil = Time.time + 4f;
		if (CompanionsPlugin.LogDetailed) TLog("Can't get clear (" + why + ") - fighting on what stamina it has");
		return 0;
	}

	// after a combo: when tired, in a group, or now and then anyway, step back out of the target's reach
	private void MaybeStepOut()
	{
		if (_targetCreature == null)
		{
			return;
		}
		// 1.5.53: not after every combo below StaminaResume (that was most of the fight) - when close to the reserve
		// 1.5.55: not after every combo in a group (log d973bc65: too much backing off) - when tired, or now and then
		// 1.5.58: with spacing, it always backs out after a combo
		bool want = StaminaShare() < Reserve + 0.15f || Random.value < 0.15f || (SpacingOn && SpacingTarget());
		if (!want)
		{
			return;
		}
		float need = Danger(_targetCreature) + 0.7f - Vector3.Distance(base.transform.position, _targetCreature.transform.position);
		if (need <= 0f || need > 2.5f)
		{
			return;
		}
		Vector3 dir = EvadeDir(_targetCreature, Mathf.Max(need, 0.8f));
		if (dir == Vector3.zero)
		{
			return;
		}
		StartStep(dir, need, _targetCreature, run: false);
		if (CompanionsPlugin.LogDetailed) TLog("Stepping " + StepWord(_targetCreature, dir) + " out of " + _targetCreature.m_name + "'s reach after its combo");
	}

	private bool _stepToReach = true;

	private void StartStep(Vector3 dir, float dist, Character from, bool run, bool untilOutOfReach = true)
	{
		_stepDir = dir;
		_stepRun = run;
		_stepFrom = from;
		_stepToReach = untilOutOfReach;
		_stepUntil = Time.time + Mathf.Max(dist, 0.8f) / 3f + 0.3f;   // a cap - it stops once out of reach
		_burstLeft = 0;
	}

	// 1.5.56: after a combo, if its swings would catch the player's own building pieces and there's a clear spot next to the
	// target, it shifts there (user: "Hitting nearby walls is fine. Thats what a player might do. They would try to
	// maneuver away from the walls to a clear spot if there is one.")
	private static int s_pieceMask = -1;

	private static readonly Collider[] s_pieceHits = new Collider[32];

	private string _wallName;

	private bool WallInSwing(Vector3 pos, Vector3 toTarget, ItemDrop.ItemData w)
	{
		if (s_pieceMask == -1)
		{
			s_pieceMask = LayerMask.GetMask("piece", "piece_nonsolid");
		}
		toTarget.y = 0f;
		float reach = MHR_MeleeReach(w) + _character.GetRadius() + 0.3f;
		Attack a = w?.m_shared?.m_attack;
		float half = (a != null ? a.m_attackAngle * 0.5f : 45f) + 10f;
		Vector3 chest = pos + Vector3.up * 1f;
		int n = Physics.OverlapSphereNonAlloc(chest, reach, s_pieceHits, s_pieceMask);
		for (int i = 0; i < n; i++)
		{
			Collider col = s_pieceHits[i];
			Piece p = col != null ? col.GetComponentInParent<Piece>() : null;
			if (p == null || !p.IsPlacedByPlayer())
			{
				continue;
			}
			Vector3 d = col.ClosestPoint(chest) - chest;
			d.y = 0f;
			if (d.magnitude < reach && Vector3.Angle(toTarget, d) < half)
			{
				_wallName = p.m_name;
				return true;
			}
		}
		return false;
	}

	private bool MaybeClearOfWalls(ItemDrop.ItemData w)
	{
		Character t = _targetCreature;
		if (t == null || w == null)
		{
			return false;
		}
		Vector3 me = base.transform.position;
		if (!WallInSwing(me, t.transform.position - me, w))
		{
			return false;
		}
		string wall = _wallName;
		// spots around the target at about the same distance, the nearest first
		Vector3 off = me - t.transform.position;
		off.y = 0f;
		float r = Mathf.Max(0.8f, Mathf.Min(off.magnitude, MHR_MeleeReach(w) + t.GetRadius() - 0.2f));
		if (off.sqrMagnitude < 0.01f)
		{
			off = -t.transform.forward;
		}
		off.Normalize();
		foreach (float a in new[] { 30f, -30f, 60f, -60f, 90f, -90f })
		{
			Vector3 spot = t.transform.position + Quaternion.AngleAxis(a, Vector3.up) * off * r;
			spot.y = me.y;
			Vector3 move = spot - me;
			move.y = 0f;
			float dist = move.magnitude;
			if (dist < 0.4f || TowardsOther(move, t) || !MHR_Mover.SafeStep(me, move, dist) || WallInSwing(spot, t.transform.position - spot, w))
			{
				continue;
			}
			StartStep(move.normalized, dist, t, run: false, untilOutOfReach: false);
			if (CompanionsPlugin.LogDetailed) TLog("Moving to a clear spot - its swings were catching the " + wall);
			return true;
		}
		return false;
	}

	// 1.5.63: a tree, log or rock between it and the target soaks up the swing (log 4122afdf: four swings at a greydwarf
	// 0.9 m away all "missed" into a tree) - like the walls: step round to a spot with a clear line first. Checked at most
	// twice a second; with no clear spot it swings anyway.
	private static int s_obstacleMask = -1;

	private float _obstacleCheckAt;

	private string _obstacleName;

	private bool ObstacleInLine(Vector3 pos, Character t)
	{
		if (s_obstacleMask == -1)
		{
			s_obstacleMask = LayerMask.GetMask("Default", "static_solid", "Default_small");
		}
		Vector3 from = pos + Vector3.up * 1f;
		Vector3 to = t.GetCenterPoint();
		Vector3 d = to - from;
		float len = d.magnitude - t.GetRadius() * 0.5f;
		if (len <= 0.2f)
		{
			return false;
		}
		foreach (RaycastHit hit in Physics.SphereCastAll(from, 0.15f, d.normalized, len, s_obstacleMask, QueryTriggerInteraction.Ignore))
		{
			Collider c = hit.collider;
			if (c == null || c.GetComponentInParent<Character>() != null || c.GetComponentInParent<Piece>() != null)
			{
				continue;
			}
			Component o = (Component)c.GetComponentInParent<TreeBase>() ?? (Component)c.GetComponentInParent<TreeLog>() ?? (Component)c.GetComponentInParent<Destructible>() ?? (Component)c.GetComponentInParent<MineRock5>() ?? c.GetComponentInParent<MineRock>();
			_obstacleName = o != null ? MHR_Items.NiceName(o.gameObject) : c.gameObject.name;
			return true;
		}
		return false;
	}

	// true = it's stepping round this tick (don't swing now)
	internal bool MaybeClearOfObstacle(ItemDrop.ItemData w)
	{
		Character t = _targetCreature;
		if (t == null || w == null || Time.time < _obstacleCheckAt || Time.time < _stepUntil)
		{
			return false;
		}
		_obstacleCheckAt = Time.time + 0.5f;
		Vector3 me = base.transform.position;
		if (!ObstacleInLine(me, t))
		{
			return false;
		}
		string what = _obstacleName;
		Vector3 off = me - t.transform.position;
		off.y = 0f;
		float r = Mathf.Max(0.8f, Mathf.Min(off.magnitude, MHR_MeleeReach(w) + t.GetRadius() - 0.2f));
		if (off.sqrMagnitude < 0.01f)
		{
			off = -t.transform.forward;
		}
		off.Normalize();
		foreach (float a in new[] { 30f, -30f, 60f, -60f, 90f, -90f, 120f, -120f })
		{
			Vector3 spot = t.transform.position + Quaternion.AngleAxis(a, Vector3.up) * off * r;
			spot.y = me.y;
			Vector3 move = spot - me;
			move.y = 0f;
			float dist = move.magnitude;
			if (dist < 0.4f || TowardsOther(move, t) || !MHR_Mover.SafeStep(me, move, dist) || ObstacleInLine(spot, t))
			{
				continue;
			}
			StartStep(move.normalized, dist, t, run: false, untilOutOfReach: false);
			if (CompanionsPlugin.LogDetailed) TLog("Moving round the " + what + " - it was in the way of its swing at " + t.m_name);
			return true;
		}
		if (CompanionsPlugin.LogDetailed) TLog("The " + what + " is in the way of its swing at " + t.m_name + " and there's no clear spot - swinging anyway");
		_obstacleCheckAt = Time.time + 3f;
		return false;
	}

	// a swing at it it can get out of the way of, rather than block (UpdateBlocking calls this once per swing, at the
	// moment it reacts)
	private bool TryEvade(Character swinger, bool always = false)
	{
		float chance = always ? 1f : (ModConfig.StepAwayChance?.Value ?? 0.3f);
		if (StaminaShare() < Reserve)
		{
			chance = Mathf.Min(1f, chance * 2f);   // blocking costs stamina it doesn't have
		}
		// 1.5.58: spacing - at the edge of its reach a short step back makes the swing miss, and costs no stamina
		if (SpacingOn && swinger == _targetCreature && SpacingTarget())
		{
			float edge = Danger(swinger) + 0.6f - Vector3.Distance(base.transform.position, swinger.transform.position);
			if (edge <= 1.2f)
			{
				chance = Mathf.Max(chance, ModConfig.CombatSpacingStepChance?.Value ?? 0.6f);
			}
		}
		if (!always && Random.value >= chance)
		{
			return false;
		}
		float need = Danger(swinger) + 0.6f - Vector3.Distance(base.transform.position, swinger.transform.position);
		if (need > 2.5f)
		{
			return false;   // too deep in its reach to get out in time - block
		}
		need = Mathf.Max(need, 0.8f);
		Vector3 dir = EvadeDir(swinger, need);
		if (dir == Vector3.zero)
		{
			return false;
		}
		// a quick step: a run only when it has further to go than a jog covers in time
		bool run = need > 1.5f && StaminaShare() > Reserve;
		StartStep(dir, need, swinger, run);
		if (CompanionsPlugin.LogDetailed) TLog("Stepping " + StepWord(swinger, dir) + $" out of {swinger.m_name}'s swing ({need:F1} m)");
		return true;
	}

	private string StepWord(Character from, Vector3 dir)
	{
		Vector3 away = base.transform.position - from.transform.position;
		away.y = 0f;
		float ang = Vector3.Angle(away, dir);
		return ang < 30f ? "back" : ang < 70f ? "back and aside" : "aside";
	}

	// 1.5.54: which way to step from a swing - straight back, back and aside, or aside, picked by chance among the safe
	// ones (user: "The NPC only looks to step backward rather than to a side." - BestDir scores distance from the enemies
	// only, so it was nearly always straight back). Never towards another enemy close by; the side away from the others
	// (or towards the owner) is tried first.
	private Vector3 EvadeDir(Character from, float dist)
	{
		Vector3 me = base.transform.position;
		Vector3 away = me - from.transform.position;
		away.y = 0f;
		if (away.sqrMagnitude < 0.01f)
		{
			away = -base.transform.forward;
		}
		away.Normalize();
		// which side first: away from the other attackers, else towards the owner, else either
		float sideBias = 0f;
		Vector3 right = Vector3.Cross(Vector3.up, away);
		foreach (Character c in _attackers)
		{
			if (c == null)
			{
				continue;
			}
			if (c != from)
			{
				Vector3 t = c.transform.position - me;
				t.y = 0f;
				sideBias -= Vector3.Dot(right, t.normalized);
			}
		}
		Player owner = MHR_Owner.Of(this);
		if (sideBias == 0f && owner != null)
		{
			Vector3 o = owner.transform.position - me;
			o.y = 0f;
			sideBias = Vector3.Dot(right, o.normalized);
		}
		float first = (sideBias > 0f || (sideBias == 0f && Random.value < 0.5f)) ? 1f : -1f;
		// back 25%, back and aside 35%, aside 40%
		float r = Random.value;
		float[] order = r < 0.25f ? new[] { 0f, 45f, 90f } : r < 0.6f ? new[] { 45f, 90f, 0f } : new[] { 90f, 45f, 0f };
		foreach (float a in order)
		{
			foreach (float sign in a == 0f ? new[] { 1f } : new[] { first, -first })
			{
				// the angle is measured from straight away, turning towards the chosen side ("right" = +)
				Vector3 d = Quaternion.AngleAxis(-sign * a, Vector3.up) * away;
				if (TowardsOther(d, from) || !MHR_Mover.SafeStep(me, d, dist))
				{
					continue;
				}
				return d;
			}
		}
		return Vector3.zero;
	}

	private bool TowardsOther(Vector3 dir, Character except)
	{
		foreach (Character c in _attackers)
		{
			if (c == null)
			{
				continue;
			}
			if (c == except)
			{
				continue;
			}
			Vector3 t = c.transform.position - base.transform.position;
			t.y = 0f;
			if (t.magnitude < 4f && Vector3.Dot(dir, t.normalized) > 0.5f)
			{
				return true;
			}
		}
		return false;
	}

	// an opening: nothing swinging at it, the target staggered, or the swing at it already landed (it's finishing it)
	private bool Opening()
	{
		if (_targetCreature != null && _targetCreature.IsStaggering())
		{
			return true;
		}
		Character sw = SwingAtUs();
		if (sw == null)
		{
			return true;
		}
		return sw == _blockAgainst && _swingLandedAt >= _swingStartedAt;
	}

	// a hit on it or a block, from the creature whose swing it's watching: that swing has landed
	internal void NoteIncoming(Character attacker)
	{
		if (attacker != null && attacker == _blockAgainst)
		{
			_swingLandedAt = Time.time;
		}
	}

	// 1.5.53: finishing an enemy off (user: "the NPC should push their stamina low if it means killing an enemy that then
	// gives them some breathing room rather than trying to conserver it ... if the enemy is low health and it takes 1-2
	// more hits to kill the enemy the NPC should use up their stamina so they can do a larger recharge. Then they can
	// re-join the fight by walking upto another enemy.") How much a hit takes off is measured: the target's health drop
	// within a moment of one of its hits (resistances, difficulty and all), halved when the target was staggered (the
	// game doubles damage to a staggered creature, Character.RPC_Damage).
	private Character _dmgTarget;

	private float _dmgLastHp;

	private float _perHit;

	private float _lastOwnHitAt = -10f;

	private bool _finishing;

	private int _finishHits;

	internal void NoteOwnHit(Character target)
	{
		if (target == _targetCreature)
		{
			_lastOwnHitAt = Time.time;
		}
	}

	private void TrackDamage()
	{
		Character t = _targetCreature;
		if (t != _dmgTarget)
		{
			_dmgTarget = t;
			_perHit = 0f;
			_dmgLastHp = t != null ? t.GetHealth() : 0f;
			return;
		}
		if (t == null)
		{
			return;
		}
		float hp = t.GetHealth();
		if (hp < _dmgLastHp - 0.1f && Time.time - _lastOwnHitAt < 1f)
		{
			float drop = _dmgLastHp - hp;
			_perHit = t.IsStaggering() ? drop * 0.5f : drop;
		}
		_dmgLastHp = hp;
	}

	private int HitsToKill()
	{
		if (_targetCreature == null || _perHit <= 0f)
		{
			return 99;
		}
		float per = _perHit * 0.9f * (_targetCreature.IsStaggering() ? 2f : 1f);
		return Mathf.Max(1, Mathf.CeilToInt(_targetCreature.GetHealth() / per));
	}

	// nothing else close enough to hit it while it's out of stamina afterwards
	private bool BreathingRoomAfter()
	{
		foreach (Character c in _attackers)
		{
			if (c == null)
			{
				continue;
			}
			if (c != _targetCreature && Vector3.Distance(base.transform.position, c.transform.position) < Danger(c) + 2f)
			{
				return false;
			}
		}
		return true;
	}

	private bool FinishOff(ItemDrop.ItemData w)
	{
		if (_targetCreature == null || w == null || _targetCreature.GetHealth() <= 0f)
		{
			return false;
		}
		float cost = AttackCost(w);
		if (_burstLeft > 0)
		{
			return _finishing && StaminaNow() >= cost;
		}
		int need = HitsToKill();
		// started on an opening like any combo (not into a swing), then carried through
		if (need > (ModConfig.CombatFinishHits?.Value ?? 2) || StaminaNow() < need * cost || !BreathingRoomAfter() || !Opening())
		{
			return false;
		}
		_finishHits = need;
		return true;
	}

	// 1.5.53: its target is staggered (a parry, or a hit that staggered it) and there's stamina for a combo that still
	// leaves a block's worth: go in and punish it, even while catching its breath or between combos (user: "If the NPC
	// parries is should perform a combo if possible against the enemy.")
	// At least one hit whenever it has the stamina for that swing (user: "It should at least do a single hit."); the rest of
	// the combo only above half the reserve (BurstSize, and below for the swings after the first). One combo per stagger.
	private bool Punish(ItemDrop.ItemData w)
	{
		if (_targetCreature == null || w == null || !_targetCreature.IsStaggering())
		{
			_punishUsed = false;
			return false;
		}
		if (_burstLeft > 0)
		{
			return _punishing && StaminaNow() - AttackCost(w) >= Reserve * StaminaMax() * 0.5f;
		}
		return !_punishUsed && StaminaNow() >= AttackCost(w);
	}

	private bool _punishing;

	// the combo for this stagger has started
	private bool _punishUsed;

	private bool ReadyToSwing(ItemDrop.ItemData w)
	{
		if (w == null)
		{
			return false;
		}
		// 1.5.55: the swing it just started hasn't shown up as an attack yet (the animation takes a frame or two) - starting
		// it again restarted swing 1 and used up the combo (log d973bc65: three "combo swing 1/3" 0.04 s apart)
		if (_awaitingSwing)
		{
			if (_character.InAttack() || Time.time - _swingStartedTime > 0.4f)
			{
				_awaitingSwing = false;
			}
			else
			{
				return false;
			}
		}
		// 1.5.53: mid-combo, the next swing starts as soon as the current one allows it, like a player holding attack
		// (Player.HaveQueuedChain -> Attack.CanStartChainAttack: the swing animation's Chain event); a non-player had to
		// wait for the whole swing to end (Humanoid.HaveQueuedChain is false) - CompanionChainPatch
		// (the usual checks below still apply - an enemy swinging at it or too little stamina ends the combo)
		if (_character.InAttack() && !(WantsChain && CurrentAttackCanChain()))
		{
			return false;
		}
		if (FinishOff(w))
		{
			if (!_finishing)
			{
				_finishing = true;
				if (CompanionsPlugin.LogDetailed) TLog($"{_targetCreature.m_name} is nearly dead ({_targetCreature.GetHealth():F0} hp, about {_finishHits} hit(s)) - spending its stamina to finish it");
			}
			return true;
		}
		if (_finishing && _burstLeft <= 0)
		{
			_finishing = false;
		}
		if (Punish(w))
		{
			if (!_punishing)
			{
				_punishing = true;
				if (CompanionsPlugin.LogDetailed) TLog(_targetCreature.m_name + " is staggered - going in with a combo" + (_recovering ? " (catching its breath can wait)" : ""));
			}
			return true;
		}
		if (_punishing && _burstLeft <= 0)
		{
			_punishing = false;
		}
		if (_spacingHold && _burstLeft <= 0)
		{
			return false;
		}
		if ((_recovering && !_cornered) || _burstPending)
		{
			EndBurst();   // a punish combo cut short by stamina ends here
			return false;
		}
		if (_burstLeft <= 0 && Time.time < _burstPauseUntil)
		{
			return false;
		}
		// a combo is started on an opening and then carried through, like a player committing to it (log 4b8d4066: with
		// 4-5 skeletons around one of them was nearly always swinging, and that ended almost every combo after swing 1)
		if (_burstLeft <= 0 && !Opening())
		{
			return false;
		}
		// 1.5.53: a combo is started above the reserve and may dip to half of it (still enough for a block), then it backs
		// off to get its breath back - like a player at 40% stamina still doing a full combo (user: "It had just looked like
		// the NPC was taking 1 swing near the end"; log: 35/85 stamina, 25 kept back, 8 a swing -> one swing at a time)
		float floor = _cornered ? 0f : Reserve * StaminaMax() * (_burstLeft > 0 ? 0.5f : 1f);
		if (StaminaNow() - AttackCost(w) < floor)
		{
			EndBurst();
			return false;
		}
		return true;
	}

	private void EndBurst()
	{
		if (_burstLeft > 0)
		{
			_burstLeft = 0;
			_burstPending = true;
		}
	}

	private int BurstSize(ItemDrop.ItemData w)
	{
		// 1.5.53: the weapon's own combo (Attack.m_attackChainLevels - 3 on the melee weapons; the last swing does
		// m_lastChainDamageMultiplier x2), capped at ComboHits (user: "basically all melee weapons have a 3 hit combo ...
		// we probably only need the one setting for combo"). No separate two-handed limit - a heavier weapon's swings cost
		// more stamina, which shortens its combos on its own. Now and then (1 in 5) one swing short, so it isn't a robot.
		int max = ModConfig.CombatComboHits?.Value ?? 3;
		Attack a = w.m_shared.m_attack;
		int size = (a != null && a.m_attackChainLevels > 1) ? a.m_attackChainLevels : 2;
		size = Mathf.Clamp(size, 1, Mathf.Max(1, max));
		if (size > 1 && !_finishing && !_punishing && Random.value < 0.2f)
		{
			size--;
		}
		float cost = AttackCost(w);
		float floor = _cornered ? 0f : Reserve * StaminaMax() * 0.5f;
		if (_finishing)
		{
			size = Mathf.Max(size, _finishHits);
			floor = 0f;
		}
		int want = size;
		while (size > 1 && StaminaNow() - size * cost < floor)
		{
			size--;
		}
		if (size < want)
		{
			if (CompanionsPlugin.LogDetailed) TLog($"{size}-hit combo, not {want} - saving stamina to block ({StaminaNow():F0}/{StaminaMax():F0})");
		}
		return size;
	}

	// 1.5.77: a tick latched on - the whole combo, swing after swing, as long as there's the stamina for the next one, until
	// it's dead (user: "They should if they have stamina until the tick is dead. Then keep clear until stamina is restored")
	internal bool WantsChain => (_tacticsOn && _burstLeft > 0) || (LatchedTarget() && StaminaNow() >= AttackCost(_humanoid.GetCurrentWeapon()));

	private static readonly System.Reflection.FieldInfo s_curAttackT = HarmonyLib.AccessTools.Field(typeof(Humanoid), "m_currentAttack");

	internal bool CurrentAttackCanChain()
	{
		try
		{
			return (s_curAttackT?.GetValue(_humanoid) as Attack)?.CanStartChainAttack() ?? false;
		}
		catch
		{
			return false;
		}
	}

	// a swing that started (DoAttack succeeded)
	private bool _awaitingSwing;

	private float _swingStartedTime;

	private void NoteTacticSwing(ItemDrop.ItemData w)
	{
		_awaitingSwing = true;
		_swingStartedTime = Time.time;
		if (_burstLeft <= 0)
		{
			_burstPending = false;
			if (_punishing)
			{
				_punishUsed = true;
			}
			_burstLeft = BurstSize(w);
			if (_burstLeft > 1)
			{
				if (CompanionsPlugin.LogDetailed) TLog($"{_burstLeft}-hit combo at {_targetCreature?.m_name}");
			}
		}
		_burstLeft--;
		if (_burstLeft <= 0)
		{
			_burstPending = true;
		}
	}

	// below the flee threshold: run to the owner if the enemy isn't between them, through the mover (edges, water)
	private bool FleeToOwner(float dt)
	{
		Player owner = MHR_Owner.Of(this);
		if (owner == null || _targetCreature == null)
		{
			return false;
		}
		Vector3 me = base.transform.position;
		Vector3 toO = owner.transform.position - me;
		toO.y = 0f;
		if (toO.magnitude < 4f || toO.magnitude > 60f)
		{
			return false;
		}
		Vector3 toE = _targetCreature.transform.position - me;
		toE.y = 0f;
		if (Vector3.Angle(toO, toE) < 60f)
		{
			return false;
		}
		MHR_Mover.Go(_ai, dt, owner.transform.position, 3f, allowRun: true, "Flee", MHR_Mover.SpeedRun);
		if (MHR_Mover.GaveUp(_ai))
		{
			return false;
		}
		if (CompanionsPlugin.LogDetailed) TLog("Badly hurt - running to its owner, away from " + _targetCreature.m_name);
		return true;
	}

	private void ResetTactics()
	{
		_burstLeft = 0;
		_burstPending = false;
		_stepUntil = 0f;
		_recovering = false;
		_cornered = false;
		_repositionSince = -1f;
		_evading = false;
		_punishing = false;
		_punishUsed = false;
		_finishing = false;
		_dipping = false;
		_spacingHold = false;
		_flankSince = -1f;
	}
}
