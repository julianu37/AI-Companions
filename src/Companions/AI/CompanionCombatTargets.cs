using System.Collections.Generic;
using UnityEngine;

namespace Companions;

// 1.5.65: which enemy to fight, like a player (user, 2026-10-23, passing on a player's report: "that psycho runs towards
// everything they see and especially don't see (like from behind a wall)", "it ignores every recall or command when they
// have anything to fight", "it still tries to get to another seeker behind the opposite blocked door. We don't look for
// easy and near targets!"; user: "NPC should only attack mobs they have a path too, or have line of sight for ranged
// weapons, with a priority on close mobs ... Only open a door with nearby mobs when commanded. Indoor spaces they should
// swap to only going after mobs that have actual aggro"; "the 'defensive fight' should be the default in a dungeon").
//
// It used to take the game's BaseAI.FindEnemy: the nearest enemy it could see or HEAR (20 m, through walls and doors),
// straight-line distance, and the chase opened any door on the way. Now:
// - only enemies it can get at: in plain sight (nothing solid between its eyes and the enemy - walls, doors, rock), or
//   right next to it. Hearing alone doesn't start a fight.
// - the best of those: whatever is attacking it first, then whatever is after you, closest first; the current target
//   is kept unless another is clearly better.
// - indoors (a dungeon, mine or cave, or under a roof): only enemies that are alerted and after you or a companion
//   ([Combat] IndoorAggroOnly).
// - in a dungeon, mine or cave: a defensive fight ([Combat] DungeonDefensive) - only what comes within DefendRange of
//   you, so it stays with you instead of running off down the corridor.
// - a fight never opens a door (MHR_Mover refuses for the "Combat" tag); an enemy behind one is let go for a while.
//   An attack order (DirectEngage) still does.
// - a recall or move order drops the fight and starts no new one for [Combat] OrderHoldTime, except against something
//   hitting it up close.
public partial class CompanionCombatAI
{
	private Character _ordered;

	private float _holdUntil;

	private readonly Dictionary<Character, float> _ignoreUntil = new Dictionary<Character, float>();

	// 1.5.91: let go because melee couldn't get at it - picked up again as soon as it can
	private readonly HashSet<Character> _letGoUnreachable = new HashSet<Character>();

	private float _liftCheckAt;

	private float _noSightSince = -1f;

	private float _indoorCheckAt;

	private bool _indoors;

	private float _nextTargetLog;

	internal bool Holding => Time.time < _holdUntil;

	internal bool IsOrdered(Character c)
	{
		return c != null && c == _ordered;
	}

	// an attack order: this target may be chased anywhere, doors included
	internal void Order(Character target)
	{
		_ordered = target;
		_holdUntil = 0f;
		_ignoreUntil.Remove(target);
		_letGoUnreachable.Remove(target);
	}

	// a recall or move order: let go of the fight and start no new one for a while
	internal void HoldForOrder(string what)
	{
		float secs = ModConfig.CombatOrderHoldTime?.Value ?? 10f;
		if (_engaged)
		{
			CompanionsPlugin.Log.LogDebug($"[Combat] {what} - leaving {(_targetCreature != null ? _targetCreature.m_name : "the fight")}, no new fights for {secs:F0} s");
			Disengage();
		}
		_ordered = null;
		_holdUntil = Time.time + secs;
	}

	// while holding for an order, only something hitting it up close is fought
	internal bool MayFightBack(Character attacker)
	{
		if (!Holding || attacker == null)
		{
			return true;
		}
		return Vector3.Distance(base.transform.position, attacker.transform.position) - attacker.GetRadius() < 3f;
	}

	internal bool IndoorsNow()
	{
		if (Time.time >= _indoorCheckAt)
		{
			_indoorCheckAt = Time.time + 1f;
			_indoors = _character != null && (_character.InInterior() || Physics.Raycast(base.transform.position + Vector3.up * 2f, Vector3.up, 30f, LayerMask.GetMask("piece", "static_solid", "Default")));
		}
		return _indoors;
	}

	private bool Defensive()
	{
		return _character != null && _character.InInterior() && (ModConfig.CombatDungeonDefensive?.Value ?? true);
	}

	// nothing solid between it and the enemy, or the enemy right next to it
	internal bool CanGetAt(Character c)
	{
		if (c == null)
		{
			return false;
		}
		float d = Vector3.Distance(base.transform.position, c.transform.position) - c.GetRadius();
		return d < 2.5f || MHR_BowDraw.ClearShot(_character, c);
	}

	private Character _latched;

	private string _latchedName;

	private class Reach
	{
		public float At;
		public bool Ok;
		public string Why;
	}

	private readonly Dictionary<Character, Reach> _reach = new Dictionary<Character, Reach>();

	private Character _stuckOn;

	private float _stuckSince;

	private float _stuckHealth;

	private static float LeashFromYou => ModConfig.CombatLeashFromYou?.Value ?? 25f;

	// 1.5.74: can a melee weapon get at it? Not out in swimming-deep water, not a level above or below it right here, and
	// with a walking route there that isn't a long way round (checked every 2 s per enemy)
	internal bool MeleeCanReach(Character c, out string why)
	{
		why = null;
		if (c == null || _character == null)
		{
			return true;
		}
		if (_reach.TryGetValue(c, out Reach r) && Time.time < r.At)
		{
			why = r.Why;
			return r.Ok;
		}
		if (_reach.Count > 40)
		{
			_reach.Clear();
		}
		r = new Reach { At = Time.time + 2f, Ok = true };
		Vector3 me = base.transform.position;
		Vector3 it = c.transform.position;
		Vector3 flat = it - me;
		float dy = flat.y;
		flat.y = 0f;
		float d = flat.magnitude;
		if (!_character.IsSwimming() && MHR_Water.WouldSwim(_character, it))
		{
			r.Ok = false;
			r.Why = "it's out in deep water";
		}
		// 1.5.76: only something standing (or flying) above or below - not a tick latched on or one in mid-jump (log fd7b1683:
		// "bow for $enemy_tick (2.0 m): it's above, out of reach" swapped to the bow mid-fight)
		else if (d < 3f && (dy > 1.4f || dy < -1.8f) && (c.IsOnGround() || c.IsFlying()) && MHR_Latched.LatchedOn(_character) != c)
		{
			r.Ok = false;
			r.Why = dy > 0f ? "it's above, out of reach" : "it's below, out of reach";
		}
		else if (d > 2.5f)
		{
			if (!MHR_Mover.WalkRoute(me, it, out float len))
			{
				r.Ok = false;
				r.Why = "there's no way to walk there";
			}
			else if (len > d * 2f + 15f)
			{
				r.Ok = false;
				r.Why = $"the only way there is a long way round ({len:F0} m)";
			}
		}
		_reach[c] = r;
		why = r.Why;
		return r.Ok;
	}

	// 1.5.74: an enemy only to be shot at, not walked up to: melee can't get at it, or - fighting defensively - it is
	// further than DefendRange from you
	internal bool BowOnly(Character c, out string why)
	{
		if (!MeleeCanReach(c, out why))
		{
			return true;
		}
		if (Defensive())
		{
			Player owner = MHR_Owner.Of(_setup);
			float range = ModConfig.CombatDefendRange?.Value ?? 8f;
			if (owner != null && c != null && Vector3.Distance(owner.transform.position, c.transform.position) > range)
			{
				why = $"fighting defensively, it's more than {range:F0} m from you";
				return true;
			}
		}
		return false;
	}

	private bool BowReady()
	{
		return MHR_IsProjectileWeapon(_humanoid.GetCurrentWeapon()) || MHR_Crossbow.HasReadyRanged(_humanoid);
	}

	private float _latchedCheckAt;

	// 1.5.73: a tick latched on to it (checked every 0.25 s)
	internal Character Latched()
	{
		if (Time.time >= _latchedCheckAt)
		{
			_latchedCheckAt = Time.time + 0.25f;
			Character was = _latched;
			string wasName = _latchedName;
			bool hadOne = wasName != null;
			_latched = MHR_Latched.LatchedOn(_character);
			_latchedName = _latched != null ? _latched.m_name : null;
			// 1.5.76: logged both ways, so a test log shows when one was feeding
			// plain reference compare: a tick that died is already destroyed, which Unity's == treats as null
			if (!ReferenceEquals(_latched, was))
			{
				if (_latched != null)
				{
					CompanionsPlugin.Log.LogDebug($"[Combat] {_latched.m_name} latched on to {_character.m_name}");
				}
				// 1.5.77: also when it died and is already gone (Unity null) - the name was kept
				if (hadOne)
				{
					CompanionsPlugin.Log.LogDebug($"[Combat] {wasName} let go of {_character.m_name}" + (was == null || was.IsDead() ? " (dead)" : ""));
				}
			}
		}
		return _latched;
	}

	private bool LatchedTarget()
	{
		return _targetCreature != null && Latched() == _targetCreature;
	}

	// the enemy to fight now, or null
	internal Character PickTarget()
	{
		// 1.5.73: something latched on and feeding comes first, orders or not
		Character latched = Latched();
		if (latched != null && !latched.IsDead())
		{
			return latched;
		}
		if (Holding || _character == null)
		{
			return null;
		}
		bool indoors = IndoorsNow();
		bool aggroOnly = indoors && (ModConfig.CombatIndoorAggroOnly?.Value ?? true);
		bool defensive = Defensive();
		float defendRange = ModConfig.CombatDefendRange?.Value ?? 8f;
		Player owner = MHR_Owner.Of(_setup);
		Vector3 me = base.transform.position;
		float maxRange = Mathf.Max(_ai.m_viewRange, 20f);
		if (MHR_IsProjectileWeapon(_humanoid.GetCurrentWeapon()) || _setup?.GetCombatStance() == 2)
		{
			maxRange = Mathf.Max(maxRange, 30f);
		}
		Character best = null;
		float bestScore = float.MaxValue;
		foreach (Character c in Character.GetAllCharacters())
		{
			if (c == null || c == _character || c.IsDead() || c.m_aiSkipTarget || !_ai.IsEnemy(c) || IsPassiveAnimal(c) || MHR_Threat.IgnoredInWater(c))
			{
				continue;
			}
			if (_ignoreUntil.TryGetValue(c, out float until) && Time.time < until)
			{
				if (!LiftIgnore(c))
				{
					continue;
				}
			}
			float d = Vector3.Distance(me, c.transform.position);
			if (d > maxRange)
			{
				continue;
			}
			BaseAI cai = c.GetBaseAI();
			if (cai != null && cai.IsSleeping())
			{
				continue;
			}
			Character itsTarget = cai?.GetTargetCreature();
			bool onMe = itsTarget == _character;
			if (!MHR_Threat.IsAfterUs(c))
			{
				continue;
			}
			if (aggroOnly && cai != null && !cai.IsAlerted())
			{
				continue;
			}
			bool upClose = onMe && d - c.GetRadius() < 3f;
			// 1.5.74: nothing further than LeashFromYou from you
			if (owner != null && !upClose && Vector3.Distance(owner.transform.position, c.transform.position) > LeashFromYou)
			{
				continue;
			}
			// in a dungeon only what comes close to you - or, with a bow ready, whatever is after you it has a clear shot
			// at, shot from where it stands (1.5.74: user, "some bugs were below us and it would have been a good place
			// to use a bow")
			bool bowShot = false;
			if (defensive && owner != null && Vector3.Distance(owner.transform.position, c.transform.position) > defendRange && !upClose)
			{
				if (!BowReady() || d > 30f || (cai != null && !cai.IsAlerted()))
				{
					continue;
				}
				bowShot = true;
			}
			if (!CanGetAt(c) || (bowShot && !MHR_BowDraw.ClearShot(_character, c)))
			{
				continue;
			}
			float score = d - (onMe ? 6f : 0f) - (itsTarget != null && itsTarget.IsPlayer() ? 2f : 0f) - (c == _targetCreature ? 3f : 0f);
			// 1.5.74: one it can't get at with its melee weapon counts as much further off - a Greydwarf at 5 m beats a Neck
			// out in the river
			if (!upClose && !MeleeCanReach(c, out _))
			{
				score += 15f;
			}
			if (score < bestScore)
			{
				bestScore = score;
				best = c;
			}
		}
		return best;
	}

	// The current target is still worth chasing? Lost from sight for 3 s (gone round a corner, behind a door), or - in a
	// dungeon - gone further than DefendRange from you: let it go, unless it was ordered or is hitting it.
	private void CheckTargetStillValid()
	{
		if (_targetCreature == null || HuntMode || IsOrdered(_targetCreature))
		{
			_noSightSince = -1f;
			return;
		}
		Character c = _targetCreature;
		bool onMe = c.GetBaseAI()?.GetTargetCreature() == _character;
		float d = Vector3.Distance(base.transform.position, c.transform.position) - c.GetRadius();
		if (onMe && d < 3f)
		{
			_noSightSince = -1f;
			return;
		}
		Player you = MHR_Owner.Of(_setup);
		if (you != null && Vector3.Distance(you.transform.position, c.transform.position) > LeashFromYou)
		{
			TargetLog($"[Combat] {c.m_name} is more than {LeashFromYou:F0} m from you - letting it go and coming back");
			LetGo(c, 10f);
			return;
		}
		if (GiveUpOnUnreachable(c))
		{
			return;
		}
		if (Defensive())
		{
			Player owner = MHR_Owner.Of(_setup);
			float range = (ModConfig.CombatDefendRange?.Value ?? 8f) + 3f;
			// 1.5.74: with a bow it keeps shooting from where it stands (ChaseMove doesn't walk it away from you)
			if (owner != null && Vector3.Distance(owner.transform.position, c.transform.position) > range && !(BowReady() && MHR_BowDraw.ClearShot(_character, c)))
			{
				TargetLog($"[Combat] {c.m_name} is more than {range:F0} m from you - fighting defensively, letting it come to us");
				_ignoreUntil[c] = Time.time + 3f;
				_targetCreature = null;
				return;
			}
		}
		if (CanGetAt(c))
		{
			_noSightSince = -1f;
			return;
		}
		if (_noSightSince < 0f)
		{
			_noSightSince = Time.time;
		}
		else if (Time.time - _noSightSince > 3f)
		{
			TargetLog($"[Combat] Lost sight of {c.m_name} (behind a wall or door) - letting it go");
			_ignoreUntil[c] = Time.time + 8f;
			_targetCreature = null;
			_noSightSince = -1f;
		}
	}

	// 1.5.91: an enemy let go is taken up again once the reason is gone (user, log 32bba563: a greydwarf let go out in deep
	// water came ashore and swung at you for a while; it was only fought once it hit the companion): melee can reach it now
	// (let go as unreachable), or it's swinging at you or the companion within 5 m (any reason). Checked twice a second.
	private bool LiftIgnore(Character c)
	{
		if (Time.time < _liftCheckAt)
		{
			return false;
		}
		Character after = c.GetBaseAI()?.GetTargetCreature();
		Player you = MHR_Owner.Of(_setup);
		bool attacking = c.InAttack() && after != null && (after == _character || after == you) && Vector3.Distance(c.transform.position, after.transform.position) < 5f;
		bool reach = !attacking && _letGoUnreachable.Contains(c) && MeleeCanReach(c, out _);
		if (!attacking && !reach)
		{
			_liftCheckAt = Time.time + 0.5f;
			return false;
		}
		_ignoreUntil.Remove(c);
		_letGoUnreachable.Remove(c);
		CompanionsPlugin.Log.LogDebug($"[Combat] {c.m_name} {(attacking ? "is attacking " + (after == _character ? "it" : "you") : "came within reach")} - back on it");
		return true;
	}

	private void LetGo(Character c, float secs)
	{
		_ignoreUntil[c] = Time.time + secs;
		_letGoUnreachable.Remove(c);
		_targetCreature = null;
		_noSightSince = -1f;
		_stuckOn = null;
		MHR_Mover.Release(_ai);
	}

	// 1.5.74: an enemy it can't get at in melee (out in deep water, no way there) and that isn't hurt for GiveUpTime: let
	// it go for 20 s (log 93298113: shot at a Neck out in the river, missed, ran out of breath, and stayed on it while
	// you walked off)
	private bool GiveUpOnUnreachable(Character c)
	{
		if (MeleeCanReach(c, out string why))
		{
			_stuckOn = null;
			return false;
		}
		float hp = c.GetHealth();
		if (_stuckOn != c || hp < _stuckHealth - 0.01f)
		{
			_stuckOn = c;
			_stuckSince = Time.time;
			_stuckHealth = hp;
			return false;
		}
		float limit = ModConfig.CombatGiveUpTime?.Value ?? 10f;
		if (Time.time - _stuckSince < limit)
		{
			return false;
		}
		CompanionsPlugin.Log.LogDebug($"[Combat] Can't get at {c.m_name} ({why}) and not a hit on it for {limit:F0} s - letting it go");
		LetGo(c, 20f);
		_letGoUnreachable.Add(c);
		return true;
	}

	// the chase came to a closed door: a fight doesn't open doors (an order does)
	private void DoorInTheWay()
	{
		if (_targetCreature == null)
		{
			return;
		}
		TargetLog($"[Combat] {_targetCreature.m_name} is behind a closed door - not opening it in a fight");
		_ignoreUntil[_targetCreature] = Time.time + 15f;
		_targetCreature = null;
		MHR_Mover.Release(_ai);
	}

	private void TargetLog(string msg)
	{
		if (Time.time >= _nextTargetLog)
		{
			_nextTargetLog = Time.time + 1f;
			CompanionsPlugin.Log.LogDebug(msg);
		}
	}
}
