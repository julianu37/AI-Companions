using UnityEngine;

namespace Companions;

public class CompanionCombatAI : MonoBehaviour
{
	private const float AlertRange = 9999f;

	private const float InterceptTimeMin = 0f;

	private const float InterceptTimeMax = 4f;

	private const float CircleTargetInterval = 3f;

	private const float CircleTargetDuration = 1f;

	private const float CircleTargetDistance = 4f;

	private const float UpdateWeaponInterval = 1f;

	private const float GiveUpTime = 30f;

	private const float UpdateTargetIntervalNear = 2f;

	private const float UpdateTargetIntervalFar = 6f;

	private const float UnableToAttackDuration = 15f;

	private const float RangedAttackRange = 30f;

	private const float RangedStandoff = 20f;

	private const float RangedFleeRadius = 10f;

	private CompanionAI _ai;

	private Character _character;

	private Humanoid _humanoid;

	private CompanionSetup _setup;

	private Character _targetCreature;

	private StaticTarget _targetStatic;

	private Vector3 _lastKnownTargetPos;

	private bool _beenAtLastPos;

	private float _timeSinceAttacking;

	private float _timeSinceSensedTarget;

	private float _updateTargetTimer;

	private float _updateWeaponTimer;

	private float _interceptTime;

	private float _pauseTimer;

	private float _unableToAttackTargetTimer;

	private float _weaponSwitchCooldown;

	private bool _engaged;

	private Character _recallIgnored;

	private float _recallTimer;

	private const float RecallDuration = 8f;

	private int _stance;

	private float _blockHoldTimer;

	private float _enemySwingTimer;

	private const float ParryDelay = 0.2f;

	private int _parryAttackCount;

	private float _parryGraceTimer;

	// 2.0.0 (performance): the enemy scan of UpdateBlocking walked every Character in the world on every frame. It now
	// runs 5 times a second and the result is kept in between (the enemy that was swinging at the last scan).
	private Character _swingingEnemy;

	private float _nextEnemyScan;

	private float _logTimer;

	private const float LogInterval = 3f;

	private const float WeaponSwitchCooldown = 4f;

	internal bool IsEngaged => _engaged;

	// 1.4.94: hunting only counts while the target is the prey. Something that attacks us mid-hunt is fought the normal
	// way (blocking, parrying, leash), like a player would.
	private Character _huntPrey;

	internal bool HuntMode
	{
		get
		{
			return _huntPrey != null && _targetCreature == _huntPrey;
		}
		set
		{
			_huntPrey = value ? _targetCreature : null;
		}
	}

	internal Character HuntPrey => _huntPrey;

	// 1.4.96: sneak up on game that hasn't noticed us yet (user: "try and sneak if they see a deer so as to not spook
	// it"), like a player: crouched, no running, until it's alerted or something else is the target
	private bool _stalking;

	private float _nextStalkLog;

	private void UpdateStalk()
	{
		bool stalk = HuntMode && (ModConfig.HuntSneak?.Value ?? true) && !_character.IsSwimming() && Unaware(_targetCreature);
		// 1.5.4: walk up to SneakFrom, then crouch (user log: sneaking from 30 m out took three breathers per deer)
		bool far = false;
		if (stalk && Vector3.Distance(base.transform.position, _targetCreature.transform.position) > (ModConfig.HuntSneakFrom?.Value ?? 20f) + (_stalking ? 2f : 0f))
		{
			stalk = false;
			far = true;
		}
		if (stalk == _stalking)
		{
			return;
		}
		_stalking = stalk;
		_ai.MHR_SetCrouch(stalk);
		if (stalk)
		{
			_character.SetRun(run: false);
		}
		if (Time.time >= _nextStalkLog)
		{
			_nextStalkLog = Time.time + 5f;
			CompanionsPlugin.Log.LogDebug("[Hunt] " + (stalk ? "Sneaking up on " : far ? "Too far off to sneak yet - walking towards " : "Stopped sneaking - spotted by ") + _targetCreature?.m_name);
		}
	}

	private void StopStalk()
	{
		if (_stalking)
		{
			_stalking = false;
			_ai.MHR_SetCrouch(false);
		}
	}

	// 1.4.103: hunting chases go through MHR_Mover like following does - routes, climbing out of pits, no jumping into
	// water (1.4.101 test: followed a deer into the moat with the game's own movement, couldn't climb the wall and ended
	// up warping to the player). With a bow or crossbow, prey well below (a moat, a pit) is shot from the edge.
	private float _nextHuntMoveLog;
	private float _nextFleeLog;
	private float _nextCloseLog;

	// 1.5.4: attack log - each swing or throw, then whether it hit (CompanionHitLog) or not within a moment
	private float _swingAt = -1f;
	private float _swingWindow;
	private Character _swingTarget;
	private string _swingKind;
	private string _swingName;

	// the miss check runs every frame, so a swing gets its result even when the fight ends right after it
	private void Update()
	{
		if (_swingAt >= 0f)
		{
			CheckMiss(force: false);
		}
	}

	// 1.5.4: it has swung at, shot at or thrown at its current target since engaging it (a hunter only swaps prey before that)
	private bool _attackedTarget;

	internal bool HasAttackedTarget => _attackedTarget;

	internal void NoteSwing(Character target, string kind, string detail, float window)
	{
		_attackedTarget = true;
		CheckMiss(force: true);
		_swingAt = Time.time;
		_swingWindow = window;
		_swingTarget = target;
		_swingKind = kind;
		_swingName = target != null ? target.m_name : "?";
		CompanionsPlugin.Log.LogDebug("[Attack] " + kind + " at " + (target != null ? target.m_name : "?") + ": " + detail);
	}

	internal void NoteHit(Character target, HitData hit)
	{
		if (_swingAt < 0f)
		{
			CompanionsPlugin.Log.LogDebug($"[Attack] hit {target.m_name} for {hit.GetTotalDamage():F1}");
			return;
		}
		CompanionsPlugin.Log.LogDebug($"[Attack] {_swingKind} hit {target.m_name}{(target != _swingTarget ? " (not the one aimed at)" : "")} for {hit.GetTotalDamage():F1} after {Time.time - _swingAt:F2} s");
		if (target == _swingTarget)
		{
			_swingAt = -1f;
		}
	}

	private void CheckMiss(bool force)
	{
		if (_swingAt < 0f || (!force && Time.time - _swingAt < _swingWindow))
		{
			return;
		}
		string where;
		if (_swingTarget != null)
		{
			Vector3 d = _swingTarget.transform.position - base.transform.position;
			where = $" (now {new Vector2(d.x, d.z).magnitude:F1} m away, {d.y:+0.0;-0.0} m up, {(_swingTarget.IsDead() ? "dead - something else killed it" : $"{_swingTarget.GetHealth():F0} hp")})";
		}
		else
		{
			where = " (it's gone - killed by something else or out of range)";
		}
		CompanionsPlugin.Log.LogDebug("[Attack] " + _swingKind + " missed " + _swingName + where);
		_swingAt = -1f;
	}

	// 1.4.104: every chase goes through MHR_Mover (routes, climbing out, water); if it has given up on the way there
	// for now, the game's own movement still closes in (a fight can't just stand still)
	private bool ChaseMove(float dt, Vector3 point, float dist, bool run)
	{
		bool there = MHR_Mover.Go(_ai, dt, point, Mathf.Max(dist, 1f), run, "Combat", run ? MHR_Mover.SpeedRun : MHR_Mover.SpeedJog);
		if (!there && MHR_Mover.GaveUp(_ai))
		{
			return _ai.GameMoveTo(dt, point, dist, run);
		}
		return there;
	}

	// 1.5.4: stamina while hunting, like a player (user: "the NPC should be conserving stamina when sneaking and try to
	// avoid jumping. They can also pause to recover stamina." Log: sneaking, a jump and a running chase left him at 13/104
	// when he reached the deer). Sneaking drains stamina, standing crouched doesn't.
	private CompanionStamina _stam;

	private bool _sneakRest;

	private float StaminaShare()
	{
		if (_stam == null)
		{
			_stam = GetComponent<CompanionStamina>();
		}
		return (_stam != null && _stam.MaxStamina > 0f) ? _stam.Stamina / _stam.MaxStamina : 1f;
	}

	private bool HuntMove(float dt, Vector3 point, float dist, bool run)
	{
		if (!HuntMode || _targetCreature == null)
		{
			return false;
		}
		float share = StaminaShare();
		if (_stalking)
		{
			float restBelow = ModConfig.HuntSneakRestStamina?.Value ?? 0.3f;
			float resumeAt = Mathf.Max(restBelow, ModConfig.HuntSneakResumeStamina?.Value ?? 0.6f);
			if (!_sneakRest && share < restBelow)
			{
				_sneakRest = true;
				CompanionsPlugin.Log.LogDebug($"[Hunt] Out of breath sneaking up on {_targetCreature.m_name} - resting crouched ({share:P0} stamina)");
			}
			else if (_sneakRest && share >= resumeAt)
			{
				_sneakRest = false;
				CompanionsPlugin.Log.LogDebug($"[Hunt] Got its breath back ({share:P0}) - sneaking on");
			}
			if (_sneakRest)
			{
				_ai.StopMoving();
				_ai.LookAtPoint(_targetCreature.GetCenterPoint());
				return true;
			}
		}
		else
		{
			_sneakRest = false;
		}
		// a bolting deer outruns anyone: run only while there's stamina to spare, else jog and let it come back
		bool tired = run && share < (ModConfig.HuntChaseRunStamina?.Value ?? 0.3f);
		// still out of sneaking range of prey that hasn't noticed: walk up quietly, no running
		if (!_stalking && (ModConfig.HuntSneak?.Value ?? true) && Unaware(_targetCreature))
		{
			run = false;
			tired = false;
		}
		Vector3 me = base.transform.position;
		Vector3 goal = point;
		ItemDrop.ItemData weapon = _humanoid.GetCurrentWeapon();
		bool ranged = MHR_IsProjectileWeapon(weapon);
		if (ranged && point.y < me.y - 2.5f)
		{
			if (EdgeSpot(me, _targetCreature, out var spot))
			{
				goal = spot;
				dist = 0.5f;
			}
			else
			{
				LeavePrey("it's down where I can't get a shot from up here");
				return true;
			}
		}
		MHR_Mover.Go(_ai, dt, goal, Mathf.Max(dist, 1f), run && !tired, "Hunt", tired ? MHR_Mover.SpeedJog : run ? MHR_Mover.SpeedAuto : MHR_Mover.SpeedWalk);
		if (MHR_Mover.RouteSwim(_ai) > MHR_Mover.JobSwim())
		{
			LeavePrey("I'd have to swim");
		}
		else if (MHR_Mover.GaveUp(_ai))
		{
			LeavePrey("I can't find a way to it");
		}
		return true;
	}

	private void LeavePrey(string why)
	{
		if (Time.time >= _nextHuntMoveLog)
		{
			_nextHuntMoveLog = Time.time + 5f;
			CompanionsPlugin.Log.LogDebug("[Hunt] Leaving " + (_targetCreature != null ? _targetCreature.m_name : "it") + " - " + why);
		}
		MHR_Mover.Release(_ai);
		Disengage();
	}

	// a spot on our level, towards the prey, from where it can be seen (the edge of the moat / pit)
	private static bool EdgeSpot(Vector3 me, Character prey, out Vector3 spot)
	{
		spot = me;
		Vector3 target = prey.GetTopPoint();
		Vector3 d = prey.transform.position - me;
		d.y = 0f;
		float len = d.magnitude;
		if (len < 0.5f)
		{
			return false;
		}
		d /= len;
		Vector3 best = Vector3.zero;
		bool found = false;
		for (float x = 0f; x < len; x += 0.5f)
		{
			Vector3 p = me + d * x;
			float g = MHR_Water.GroundAt(p);
			if (Mathf.Abs(g - me.y) > 1.5f || MHR_Water.DepthAt(p) > 0.4f)
			{
				break;   // the edge: dropping down or into water from here
			}
			p.y = g;
			bool inRange = Vector3.Distance(p, target) < RangedAttackRange - 3f;
			if (inRange && MHR_Mover.CanSee(p + Vector3.up * 1.6f, target))
			{
				best = p;
				found = true;
				break;
			}
			best = p;
		}
		if (!found && best != Vector3.zero && Vector3.Distance(best, target) < RangedAttackRange - 3f && MHR_Mover.CanSee(best + Vector3.up * 1.6f, target))
		{
			found = true;
		}
		spot = best;
		return found;
	}

	// skittish game (deer, hares, anything that runs when it notices you) that hasn't noticed anyone yet
	// how close a melee weapon has to be: its AI range, but never more than the swing really reaches (a spear's AI range
	// is its throwing distance)
	private static float MHR_MeleeReach(ItemDrop.ItemData item)
	{
		float num = item.m_shared.m_aiAttackRange;
		Attack attack = item.m_shared.m_attack;
		if (attack != null && attack.m_attackRange > 0f)
		{
			num = Mathf.Min(num, attack.m_attackRange + 0.5f);
		}
		return Mathf.Max(num, 1f);
	}

	// a weapon that shoots (bow, crossbow, a staff that fires bolts) - as opposed to one you hit with
	internal static bool MHR_IsProjectileWeapon(ItemDrop.ItemData item)
	{
		if (item == null || item.m_shared == null)
		{
			return false;
		}
		ItemDrop.ItemData.ItemType itemType = item.m_shared.m_itemType;
		if (itemType == ItemDrop.ItemData.ItemType.Ammo || itemType == ItemDrop.ItemData.ItemType.AmmoNonEquipable || itemType == ItemDrop.ItemData.ItemType.Consumable)
		{
			return false;
		}
		if (itemType == ItemDrop.ItemData.ItemType.Bow || !string.IsNullOrEmpty(item.m_shared.m_ammoType))
		{
			return true;
		}
		return item.m_shared.m_attack != null && item.m_shared.m_attack.m_attackType == Attack.AttackType.Projectile;
	}

	private static bool Unaware(Character c)
	{
		if (c == null)
		{
			return false;
		}
		BaseAI ai = c.GetBaseAI();
		if (ai == null || ai.IsAlerted() || ai.GetTargetCreature() != null)
		{
			return false;
		}
		MonsterAI monsterAI = ai as MonsterAI;
		return ai is AnimalAI || (monsterAI != null && monsterAI.m_fleeIfNotAlerted);
	}

	internal void EngagePrey(Character prey)
	{
		Engage(prey);
		MarkPrey(prey);
	}

	/// <summary>Fase 3.6: harmless game an ORDER named ("ataca o cervo") - engaged even on the Passive stance.</summary>
	internal void EngagePreyByOrder(Character prey)
	{
		EngageByOrder(prey);
		MarkPrey(prey);
	}

	private void MarkPrey(Character prey)
	{
		if (_engaged && _targetCreature == prey)
		{
			_huntPrey = prey;
		}
	}

	internal bool IsRecalled => _recallTimer > 0f;

	internal Character TargetCreature => _targetCreature;

	/// <summary>Fase 3.6: the one creature a chat order told it to attack. It is the only target the Passive stance
	/// does not drop, so "ataca o troll" works on a companion that never starts fights on its own - and the stance
	/// stays Passive, so as soon as this creature is gone the companion goes back to not fighting.</summary>
	private Character _orderTarget;

	/// <summary>Fase 3.6: engage the creature an order named. Same as Engage, but the Passive stance does not
	/// refuse it (see _orderTarget).</summary>
	internal void EngageByOrder(Character target)
	{
		if (target == null || target.GetHealth() <= 0f)
		{
			return;
		}
		_orderTarget = target;
		StartEngage(target);
	}

	internal void Engage(Character target)
	{
		if (target == null || target.GetHealth() <= 0f)
		{
			return;
		}
		_stance = _setup?.GetCombatStance() ?? 0;
		if (_stance == 1)
		{
			return;
		}
		StartEngage(target);
	}

	/// <summary>The engage itself, without the stance question (both Engage and EngageByOrder end up here).</summary>
	private void StartEngage(Character target)
	{
		_targetCreature = target;
		_targetStatic = null;
		_lastKnownTargetPos = target.transform.position;
		_beenAtLastPos = false;
		_timeSinceAttacking = 0f;
		_timeSinceSensedTarget = 0f;
		_updateTargetTimer = 0f;
		_updateWeaponTimer = 0f;
		_weaponSwitchCooldown = 0f;
		_pauseTimer = Random.Range(0f, 3f);
		_interceptTime = Random.Range(0f, 4f);
		_unableToAttackTargetTimer = 0f;
		_parryAttackCount = 0;
		_engaged = true;
		_attackedTarget = false;
		Character after = target.GetBaseAI()?.GetTargetCreature();
		CompanionsPlugin.Log.LogDebug($"[Combat] Engage -> target=\"{target.m_name}\" stance={_stance} dist={Vector3.Distance(base.transform.position, target.transform.position):F1}m its target=\"{(after != null ? after.m_name : "none")}\" alerted={target.GetBaseAI()?.IsAlerted() ?? false}");
		_ai.SetAlerted(alert: true);
	}

	internal bool UpdateCombat(float dt)
	{
		CheckMiss(force: false);
		if (WaterSafety.ShouldAvoidChase(_humanoid, _targetCreature))
		{
			return false;
		}
		if (!_engaged)
		{
			return false;
		}
		if (_humanoid == null)
		{
			return false;
		}
		_stance = _setup?.GetCombatStance() ?? 0;
		if (_stance == 1)
		{
			// Fase 3.6: the one creature a chat order named is fought even on the Passive stance (EngageByOrder).
			// The setting stays Passive - only this fight behaves like a normal one, so the companion doesn't
			// circle the target the way the ranged stance does. Once this creature is gone, Disengage clears the
			// order and the companion goes back to not starting fights.
			if (_targetCreature != _orderTarget)
			{
				Disengage();
				return false;
			}
			_stance = 0;
		}
		UpdateTarget(dt, out var canHearTarget, out var canSeeTarget);
		if (_targetCreature == null && _targetStatic == null)
		{
			Disengage();
			return false;
		}
		_ai.m_targetCreature = _targetCreature;
		_ai.m_targetStatic = _targetStatic;
		// 1.5.4: threw its only weapon at the prey: let it go and fetch the spear first, like a player (MHR_SpearThrow.Drive
		// walks to where it landed once out of combat)
		if (HuntMode && MHR_SpearThrow.UnarmedAfterThrow(_ai, _humanoid))
		{
			CompanionsPlugin.Log.LogDebug("[Hunt] Threw its only weapon at " + _targetCreature?.m_name + " - going to pick the spear up first");
			StopStalk();
			Disengage();
			return false;
		}
		UpdateStalk();
		UpdateBlocking(dt);
		float healthPercentage = _character.GetHealthPercentage();
		if (healthPercentage < (ModConfig.FleeHealthPct?.Value ?? 0.15f) && _targetCreature != null)
		{
			Player localPlayer = Player.m_localPlayer;
			if (localPlayer != null)
			{
				// 1.5.3: once every 5 s, not every tick (user log: ~70 lines in a few seconds while cornered)
				bool fleeLog = Time.time >= _nextFleeLog;
				if (fleeLog) { _nextFleeLog = Time.time + 5f; CompanionsPlugin.Log.LogInfo($"[Flee] triggered -> hp={healthPercentage:P0} (below flee threshold) target=\"{_targetCreature.m_name}\""); }
				TryEquipShield();
				if (_ai.FleeFrom(dt, _targetCreature.transform.position))
				{
					return true;
				}
				if (fleeLog) CompanionsPlugin.Log.LogInfo("[Flee] could not create distance (blocked/cornered) -> defending itself, will keep trying to flee");
			}
		}
		_logTimer += dt;
		if (_logTimer >= 3f)
		{
			_logTimer = 0f;
			string text = ((_targetCreature != null) ? _targetCreature.m_name : "static");
			float num = ((_targetCreature != null) ? Vector3.Distance(base.transform.position, _targetCreature.transform.position) : (-1f));
			string text2 = _humanoid.GetCurrentWeapon()?.m_shared?.m_name ?? "none";
		}
		bool blocking = ReflectionHelper.GetBlocking(_character);
		bool flag = _stance == 0 || _stance == 3;
		if (_targetCreature != null && !blocking && _parryAttackCount <= 0 && !HuntMode && !flag)
		{
			_pauseTimer += dt;
			if (_pauseTimer > 3f)
			{
				if (_pauseTimer > 4f)
				{
					_pauseTimer = Random.Range(0f, 0.3f);
				}
				_ai.RandomMovementAroundPoint(dt, _targetCreature.transform.position, 4f, _ai.IsAlerted());
				return true;
			}
		}
		ItemDrop.ItemData itemData = SelectBestAttack(dt);
		float num2 = ((itemData != null && itemData.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Bow) ? 0.5f : 0f);
		bool flag2 = itemData != null && Time.time - itemData.m_lastAttackTime > itemData.m_shared.m_aiAttackInterval + num2;
		float value = ModConfig.AttackCooldown.Value;
		bool flag3 = _character.GetTimeSinceLastAttack() >= value;
		bool flag4 = _parryAttackCount > 0;
		bool flag5 = itemData != null && !_ai.IsTakingOff() && (flag4 || (flag2 && flag3));
		if (!_ai.IsCharging() && (_targetStatic != null || _targetCreature != null) && itemData != null && flag5 && !_character.InAttack() && itemData.m_shared.m_attack != null && !itemData.m_shared.m_attack.IsDone() && !string.IsNullOrEmpty(itemData.m_shared.m_attack.m_chargeAnimationBool))
		{
			_ai.ChargeStart(itemData.m_shared.m_attack.m_chargeAnimationBool);
		}
		// 1.5.2: ranged = a weapon that shoots. It used to be "AI attack range over 10 m" too, and a spear's AI range is
		// its throwing distance - so a spear counted as ranged and stabbed at the air from 16 m (user log, bear hunt).
		bool flag6 = ((itemData == null) ? (_stance == 2) : MHR_IsProjectileWeapon(itemData));
		if (_targetCreature != null && itemData != null && !flag5 && !_character.InAttack() && !flag6 && !HuntMode && !flag)
		{
			Vector3 position = _targetCreature.transform.position;
			_ai.RandomMovementAroundPoint(dt, position, (_ai.m_randomMoveRange > 0f) ? _ai.m_randomMoveRange : 5f, _ai.IsAlerted());
			return true;
		}
		if ((_targetStatic == null && _targetCreature == null) || itemData == null)
		{
			_ai.ChargeStop();
			Disengage();
			return false;
		}
		if (itemData.m_shared.m_aiTargetType == ItemDrop.ItemData.AiTarget.Enemy)
		{
			if (_targetCreature != null)
			{
				if (canHearTarget || canSeeTarget)
				{
					_beenAtLastPos = false;
					_lastKnownTargetPos = _targetCreature.transform.position;
					float num3 = Vector3.Distance(_lastKnownTargetPos, base.transform.position) - _targetCreature.GetRadius();
					float num4 = 9999f * _targetCreature.GetStealthFactor();
					if (canSeeTarget && num3 < num4)
					{
						_ai.SetAlerted(alert: true);
					}
					// 1.5.2: the 30 m reach and 20 m standoff are for a ranged weapon only. Hunting used them for any weapon,
					// so a hunter with a spear or axe (no bow ready) stopped 20 m off and stabbed at the air (user video).
					// Now it sneaks right up and strikes like a melee fighter.
					bool flag7 = flag6;
					float num5 = (flag7 ? 30f : MHR_MeleeReach(itemData));
					// 1.5.4: not right at the edge of the reach (user: "using 1.8 and forcing the NPC to ensure it is under 2.0")
					if (!flag7)
					{
						num5 = Mathf.Max(0.8f, num5 - (ModConfig.MeleeReachMargin?.Value ?? 0.2f));
					}
					bool flag8 = num3 < num5;
					// 1.5.3: a sword only reaches someone about on our level - a skeleton a floor up (user log: 2.8 m away, 2 m up)
					// had him swinging at the ladder between them instead of climbing on. Out of reach: keep moving.
					float reachDy = _targetCreature.transform.position.y - base.transform.position.y;
					if (!flag7 && (reachDy > 1.4f || reachDy < -1.8f)) flag8 = false;
					// on the way up a chain of ladders to it: finish the climb onto its floor first, like a player (user log: stopped
					// half way up the last ladder and fought from it)
					if (!flag7 && reachDy > 0.5f && MHR_Mover.OnLadderChain(_ai)) flag8 = false;
					float dist = (flag7 ? 20f : 0f);
					// 1.5.4: a spear to throw at skittish game: sneak to throwing distance, stop and throw, rather than walking
					// right up to it and stabbing (bug report: stabbed the deer up close, threw only once it had run off)
					if (!flag7 && HuntMode && MHR_SpearThrow.ReadyToThrow(_ai, _humanoid, _targetCreature))
					{
						dist = MHR_SpearThrow.Standoff();
					}
					bool flag9 = !flag8 || !canSeeTarget || !_ai.IsAlerted() || (!flag7 && itemData.m_shared.m_aiAttackRangeMin < 0f);
					if (flag7 && !_character.InAttack() && num3 < 10f && canSeeTarget && _ai.IsAlerted())
					{
						_ai.FleeFrom(dt, _targetCreature.transform.position);
					}
					else if (flag7 && flag8 && canSeeTarget && _ai.IsAlerted() && !flag9)
					{
						_ai.RandomMovementAroundPoint(dt, _targetCreature.transform.position, 20f, run: true);
					}
					else if (flag9)
					{
						Vector3 velocity = _targetCreature.GetVelocity();
						Vector3 vector = velocity * _interceptTime;
						Vector3 lastKnownTargetPos = _lastKnownTargetPos;
						if (num3 > vector.magnitude / 4f)
						{
							lastKnownTargetPos += velocity * _interceptTime;
						}
						if (!HuntMove(dt, lastKnownTargetPos, dist, !_stalking))
						{
							ChaseMove(dt, lastKnownTargetPos, dist, !_stalking);
						}
						if (_timeSinceAttacking > 15f)
						{
							_unableToAttackTargetTimer = 15f;
						}
					}
					else
					{
						_ai.StopMoving();
					}
					// 1.5.2: hunting skittish game with a spear (and another weapon in the pack): throw it from range
					if (HuntMode && canSeeTarget && !blocking && MHR_SpearThrow.WantsThrow(_ai, _humanoid, _targetCreature, num3))
					{
						_ai.StopMoving();
						_ai.LookAtPoint(_targetCreature.GetCenterPoint());
						if (_ai.IsLookingAtPoint(_targetCreature.GetCenterPoint(), 10f))
						{
							MHR_SpearThrow.Throw(_ai, _humanoid, _targetCreature);
						}
					}
					else if (flag8 && canSeeTarget && _ai.IsAlerted() && !blocking)
					{
						// 1.5.4: melee aims like a player (user: the first stab at a boar missed - no skill gain, and it only noticed
						// him after; he was a metre below it on a slope). The game sweeps a melee attack out from the hand along the
						// body's facing, tilted only a few degrees towards where it looks, and skips anything behind the start:
						// look at the middle of the target, not its top (over a low animal's back); step back when standing on top of
						// it; and a narrow attack (a stab) waits until it faces the target squarely.
						Vector3 aimAt = _targetCreature.GetCenterPoint();
						_ai.LookAtPoint(aimAt);
						float tooClose = ModConfig.MeleeTooClose?.Value ?? 0.3f;
						if (!flag7 && tooClose > 0f && num3 < tooClose && !_character.InAttack())
						{
							Vector3 away = base.transform.position - _targetCreature.transform.position;
							away.y = 0f;
							if (away.sqrMagnitude < 0.01f) away = -base.transform.forward;
							_ai.MoveTowards(away.normalized, run: false);
							_ai.LookAtPoint(aimAt);
							if (Time.time >= _nextCloseLog)
							{
								_nextCloseLog = Time.time + 5f;
								CompanionsPlugin.Log.LogDebug($"[Combat] Too close to swing at {_targetCreature.m_name} ({num3:F1} m) - stepping back");
							}
						}
						else
						{
							float maxAngle = itemData.m_shared.m_aiAttackMaxAngle;
							Attack melee = itemData.m_shared.m_attack;
							if (!flag7 && melee != null && melee.m_attackAngle < 40f)
							{
								maxAngle = Mathf.Min(maxAngle, Mathf.Max(8f, melee.m_attackAngle * 0.5f + 5f));
							}
							if (flag5 && _ai.IsLookingAtPoint(_lastKnownTargetPos, maxAngle, itemData.m_shared.m_aiInvertAngleCheck) && DoAttack(_targetCreature))
							{
								Vector3 to = _targetCreature.transform.position - base.transform.position;
								Vector3 flat = new Vector3(to.x, 0f, to.z);
								Vector3 fwd = base.transform.forward; fwd.y = 0f;
								NoteSwing(_targetCreature, flag7 ? "Shot" : "Swing", $"{itemData.m_shared.m_name}, {num3:F1} m from its edge, {to.y:+0.0;-0.0} m up, facing {Vector3.Angle(fwd, flat):F0} deg off (limit {maxAngle:F0}), reach {num5:F1} m, " +
									(melee != null ? $"sweep {melee.m_attackAngle:F0} deg wide ({melee.m_attackType}), tilt up to {melee.m_maxYAngle:F0} deg, weapon range {melee.m_attackRange:F2} m, AI range {itemData.m_shared.m_aiAttackRange:F2} m, hit width {melee.m_attackRayWidth:F2} m (+{melee.m_attackRayWidthCharExtra:F2} vs creatures), hit height {melee.m_attackHeight:F2} m" : "no attack data") + (_stalking ? ", sneaking" : "") + (_targetCreature.GetBaseAI() != null && !_targetCreature.GetBaseAI().IsAlerted() ? ", it hasn't noticed" : ""), flag7 ? 4f : 1.2f);
								if (_parryAttackCount > 0)
								{
									_parryAttackCount--;
								}
							}
						}
					}
				}
				else
				{
					_ai.ChargeStop();
					float dist2 = (flag6 ? 20f : 0f);
					if (_beenAtLastPos)
					{
						_ai.RandomMovement(dt, _lastKnownTargetPos);
						if (_timeSinceAttacking > 15f)
						{
							_unableToAttackTargetTimer = 15f;
						}
					}
					else if (HuntMode ? (HuntMove(dt, _lastKnownTargetPos, dist2, !_stalking) && Vector3.Distance(base.transform.position, _lastKnownTargetPos) <= dist2 + 1.5f) : ChaseMove(dt, _lastKnownTargetPos, dist2, !_stalking))
					{
						_beenAtLastPos = true;
					}
				}
			}
		}
		else if (itemData.m_shared.m_aiTargetType == ItemDrop.ItemData.AiTarget.FriendHurt || itemData.m_shared.m_aiTargetType == ItemDrop.ItemData.AiTarget.Friend)
		{
			Character character = ((itemData.m_shared.m_aiTargetType == ItemDrop.ItemData.AiTarget.FriendHurt) ? _ai.HaveHurtFriendInRange(_ai.m_viewRange) : _ai.HaveFriendInRange(_ai.m_viewRange));
			if (character != null)
			{
				float num6 = Vector3.Distance(character.transform.position, base.transform.position);
				if (num6 < itemData.m_shared.m_aiAttackRange)
				{
					if (flag5)
					{
						_ai.StopMoving();
						_ai.LookAtPoint(character.transform.position);
						DoAttack(character);
					}
					else
					{
						_ai.RandomMovement(dt, character.transform.position);
					}
				}
				else
				{
					ChaseMove(dt, character.transform.position, 0f, true);
				}
			}
			else
			{
				_ai.RandomMovement(dt, base.transform.position);
			}
		}
		return true;
	}

	internal void OnDamaged(Character attacker)
	{
		if (!(attacker == null) && (!attacker.IsPlayer() || !_character.IsTamed()))
		{
			if (_recallTimer > 0f)
			{
				ClearRecall();
			}
			if (_recallIgnored != null && attacker == _recallIgnored)
			{
				_recallIgnored = null;
			}
			if (_engaged && (_targetCreature == null || _targetCreature.GetHealth() <= 0f || (HuntMode && attacker != _targetCreature)))
			{
				if (HuntMode && attacker != _targetCreature)
				{
					CompanionsPlugin.Log.LogDebug("[Combat] " + attacker.m_name + " attacked while hunting " + _targetCreature.m_name + " - fighting it first");
				}
				_targetCreature = attacker;
				_lastKnownTargetPos = attacker.transform.position;
				_beenAtLastPos = false;
				_targetStatic = null;
				_timeSinceSensedTarget = 0f;
			}
		}
	}

	private void Awake()
	{
		_ai = GetComponent<CompanionAI>();
		_character = GetComponent<Character>();
		_humanoid = GetComponent<Humanoid>();
		_setup = GetComponent<CompanionSetup>();
	}

	internal void Disengage()
	{
		if (_engaged)
		{
			string text = ((_targetCreature != null) ? _targetCreature.m_name : "none");
			_targetCreature = null;
			_targetStatic = null;
			_engaged = false;
			// Fase 3.6: the order is over once it lets the creature go (dead, gone, out of the leash...)
			_orderTarget = null;
			_ai.m_targetCreature = null;
			_ai.m_targetStatic = null;
			_ai.SetAlerted(alert: false);
			_ai.StopMoving();
			_ai.ChargeStop();
			ClearBlock();
			_parryAttackCount = 0;
			HuntMode = false;
			StopStalk();
		}
	}

	private void ClearBlock()
	{
		_enemySwingTimer = 0f;
		if (ReflectionHelper.GetBlocking(_character))
		{
			ReflectionHelper.TrySetBlocking(_character, value: false);
		}
		_blockHoldTimer = 0f;
	}

	internal void Recall()
	{
		_recallIgnored = _targetCreature;
		_recallTimer = 8f;
		if (_engaged)
		{
			Disengage();
			return;
		}
		_targetCreature = null;
		_targetStatic = null;
		_ai.m_targetCreature = null;
		_ai.m_targetStatic = null;
		_ai.SetAlerted(alert: false);
	}

	internal void ClearRecall()
	{
		_recallTimer = 0f;
		_recallIgnored = null;
	}

	private void UpdateTarget(float dt, out bool canHearTarget, out bool canSeeTarget)
	{
		_unableToAttackTargetTimer -= dt;
		_updateTargetTimer -= dt;
		if (_recallTimer > 0f)
		{
			_recallTimer -= dt;
		}
		if (_updateTargetTimer <= 0f && !_character.InAttack())
		{
			bool flag = Player.IsPlayerInRange(base.transform.position, 50f);
			_updateTargetTimer = (flag ? 2f : 6f);
			Character character = _ai.FindNearbyEnemy();
			if (character != null && IsPassiveAnimal(character))
			{
				character = null;
			}
			// 1.4.83: switching targets mid-fight follows the same rule as starting one: only something that's after us
			// (1.4.82 log: after a Neck, he moved on to boars that were minding their own business). 1.4.94: hunting too -
			// a hunter goes after its prey and whatever attacks, not every creature around
			if (character != null && character != _targetCreature && !MHR_Threat.IsAfterUs(character))
			{
				character = null;
			}
			if (_recallIgnored != null)
			{
				if (!_recallIgnored || _recallIgnored.GetHealth() <= 0f)
				{
					_recallIgnored = null;
				}
				else if (character == _recallIgnored)
				{
					character = null;
				}
				else if (character != null)
				{
					_recallIgnored = null;
				}
			}
			if (character != null)
			{
				_ = _targetCreature != character;
				_targetCreature = character;
				_targetStatic = null;
			}
		}
		if (_targetCreature != null && _character.IsTamed())
		{
			bool flag2 = false;
			Vector3 b;
			if (_ai.GetPatrolPoint(out var point))
			{
				b = point;
				flag2 = true;
			}
			else
			{
				GameObject followTarget = _ai.GetFollowTarget();
				if (followTarget != null)
				{
					b = followTarget.transform.position;
					flag2 = true;
				}
				else
				{
					b = Vector3.zero;
				}
			}
			if (flag2)
			{
				float num = Vector3.Distance(_targetCreature.transform.position, b);
				if (num > 9999f)
				{
					_targetCreature = null;
				}
			}
		}
		if (_targetCreature != null)
		{
			ZNetView component = _targetCreature.GetComponent<ZNetView>();
			if (component != null && !component.IsValid())
			{
				_targetCreature = null;
			}
			else if (_targetCreature.GetHealth() <= 0f)
			{
				_targetCreature = null;
			}
			else if (!HuntMode && !_ai.IsEnemy(_targetCreature))
			{
				_targetCreature = null;
			}
			else if (!HuntMode && Vector3.Distance(base.transform.position, _targetCreature.transform.position) > CombatLeash)
			{
				// 1.4.78: don't chase something across the map (1.4.77 log: a dolphin 141 m out kept him "in combat",
				// so his job never started)
				CompanionsPlugin.Log.LogDebug($"[Combat] {_targetCreature.m_name} is over {CombatLeash:F0} m away - letting it go");
				_targetCreature = null;
			}
			else if (!HuntMode && IsPassiveAnimal(_targetCreature) && _targetCreature.GetBaseAI()?.GetTargetCreature() == null)
			{
				// 1.4.78: it's gone back to minding its own business
				CompanionsPlugin.Log.LogDebug("[Combat] " + _targetCreature.m_name + " isn't a threat - leaving it");
				_targetCreature = null;
			}
		}
		canHearTarget = false;
		canSeeTarget = false;
		if (_targetCreature != null)
		{
			canHearTarget = _ai.CanHearTarget(_targetCreature);
			canSeeTarget = _ai.CanSeeTarget(_targetCreature);
			if (canSeeTarget | canHearTarget)
			{
				_timeSinceSensedTarget = 0f;
			}
		}
		_timeSinceSensedTarget += dt;
		if (_ai.IsAlerted() || _targetCreature != null)
		{
			_timeSinceAttacking += dt;
			// 1.5.4: a stalk with a few breathers takes longer than a minute without an attack (user log: dropped the deer it
			// was sneaking up on, "Lost Deer", skipped it for a minute and went for one further off) - hunting gets 3 minutes
			float num2 = HuntMode ? 180f : 60f;
			if (_timeSinceSensedTarget > 30f || _timeSinceAttacking > num2)
			{
				if (_targetCreature != null)
				{
					CompanionsPlugin.Log.LogDebug("[Combat] Giving up on " + _targetCreature.m_name + (_timeSinceSensedTarget > 30f ? " - lost sight and sound of it for 30 s" : $" - no attack in {num2:F0} s"));
				}
				_ai.SetAlerted(alert: false);
				_targetCreature = null;
				_targetStatic = null;
				_timeSinceAttacking = 0f;
				_updateTargetTimer = 5f;
			}
		}
	}

	/// <summary>2.0.0: the enemy within 8 m that is swinging right now (the one the companion turns to and blocks),
	/// or null. Split out of <see cref="UpdateBlocking"/> so the scan can run on a timer instead of every frame.</summary>
	private Character FindSwingingEnemy()
	{
		foreach (Character allCharacter in Character.GetAllCharacters())
		{
			if (!(allCharacter == null) && !(allCharacter.GetHealth() <= 0f) && BaseAI.IsEnemy(_character, allCharacter))
			{
				float num = Vector3.Distance(base.transform.position, allCharacter.transform.position);
				if (num < 8f && allCharacter.InAttack())
				{
					return allCharacter;
				}
			}
		}
		return null;
	}

	private void UpdateBlocking(float dt)
	{
		if (_targetCreature == null || _character.InAttack() || _character.IsStaggering() || _parryAttackCount > 0)
		{
			_swingingEnemy = null;
			ClearBlock();
			return;
		}
		// 2.0.0 (performance): the scan is timed, not per frame (Character.GetAllCharacters walks the whole world).
		// Fase 3.24 (auditoria): 12.5 times a second instead of 5. The block below waits 0.2 s from the first scan that
		// saw the swing, so a 0.2 s window meant up to ~0.4 s before the shield went up - longer than the wind-up of a
		// greydwarf or a skeleton (~0.3 s), and the parry arrived after the hit. At 0.08 s the worst case is ~0.28 s.
		float now = Time.time;
		if (now >= _nextEnemyScan)
		{
			_nextEnemyScan = now + 0.08f;
			Character previousEnemy = _swingingEnemy;
			_swingingEnemy = FindSwingingEnemy();
			if (!ReferenceEquals(previousEnemy, _swingingEnemy))
			{
				// Fase 3.24 (auditoria): the timer counts the swing of ONE enemy - without this reset the time spent
				// with a previous target was carried over to the new one and the block fired on a fresh swing.
				_enemySwingTimer = 0f;
			}
			if (_swingingEnemy != null)
			{
				_ai.LookAtPoint(_swingingEnemy.GetTopPoint());
			}
		}
		// the cached enemy may have died or been removed since the scan
		bool flag = _swingingEnemy != null && _swingingEnemy.GetHealth() > 0f;
		if (flag)
		{
			_enemySwingTimer += dt;
			if (_enemySwingTimer >= 0.2f)
			{
				ReflectionHelper.TrySetBlocking(_character, value: true);
				ReflectionHelper.TrySetBlockTimer(_humanoid, 0f);
				_blockHoldTimer = 0.2f;
			}
			return;
		}
		if (_blockHoldTimer > 0f)
		{
			_blockHoldTimer -= dt;
			ReflectionHelper.TrySetBlocking(_character, value: true);
			_enemySwingTimer = 0f;
			return;
		}
		if (_blockHoldTimer < 0f)
		{
			_parryGraceTimer = 0.15f;
		}
		if (_parryGraceTimer > 0f)
		{
			_parryGraceTimer -= dt;
			if (_targetCreature != null && _targetCreature.IsStaggering())
			{
				_parryAttackCount = 2;
				_parryGraceTimer = 0f;
			}
		}
		ClearBlock();
	}

	private bool TryEquipShield()
	{
		ItemDrop.ItemData itemData = ShieldLogic.SelectShield(_humanoid, _humanoid.GetComponent<Character>());
		if (itemData == null)
		{
			return false;
		}
		_humanoid.EquipItem(itemData);
		return true;
	}

	private ItemDrop.ItemData SelectBestAttack(float dt)
	{
		if (_targetCreature != null || _targetStatic != null)
		{
			_updateWeaponTimer -= dt;
			if (_weaponSwitchCooldown > 0f)
			{
				_weaponSwitchCooldown -= dt;
			}
			if (_updateWeaponTimer <= 0f && !_character.InAttack() && _weaponSwitchCooldown <= 0f)
			{
				_updateWeaponTimer = 1f;
				if (!TorchOpener.TrySelect(_humanoid, _targetCreature, _stance, HuntMode))
				{
					string text = _humanoid.GetCurrentWeapon()?.m_shared?.m_name;
					bool flag = false;
					ItemDrop.ItemData currentWeapon = _humanoid.GetCurrentWeapon();
					if (_stance == 2 || HuntMode)
					{
						if (currentWeapon == null || currentWeapon.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Bow)
						{
							// 1.5.2: a ready bow first; no bow: a spear to throw at skittish game (with another weapon in the pack),
							// else the best melee weapon. The spear check comes before the game's own pick, which would swap the
							// spear out every second.
							if (!TryForceRangedWeapon() && !(HuntMode && MHR_SpearThrow.EquipForHunt(_ai, _humanoid, _huntPrey)))
							{
								_ai.HaveFriendsInRange(_ai.m_viewRange, out var hurtFriend, out var friend);
								_humanoid.EquipBestWeapon(_targetCreature, _targetStatic, hurtFriend, friend);
								TryForceMeleeWeapon();
							}
							flag = true;
						}
					}
					else if (!HuntMode && _stance == 3)
					{
						if (currentWeapon == null || (currentWeapon.m_shared.m_itemType != ItemDrop.ItemData.ItemType.OneHandedWeapon && currentWeapon.m_shared.m_itemType != ItemDrop.ItemData.ItemType.TwoHandedWeapon && currentWeapon.m_shared.m_itemType != ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft) || currentWeapon.GetDamage().m_pickaxe != 0f || !(GetCombatDamage(currentWeapon) > 0f))
						{
							_ai.HaveFriendsInRange(_ai.m_viewRange, out var hurtFriend2, out var friend2);
							_humanoid.EquipBestWeapon(_targetCreature, _targetStatic, hurtFriend2, friend2);
							TryForceMeleeWeapon();
							flag = true;
						}
					}
					else
					{
						_ai.HaveFriendsInRange(_ai.m_viewRange, out var hurtFriend3, out var friend3);
						_humanoid.EquipBestWeapon(_targetCreature, _targetStatic, hurtFriend3, friend3);
					}
					TryEquipShield();
					string text2 = _humanoid.GetCurrentWeapon()?.m_shared?.m_name;
					if (text2 != text || flag)
					{
						_weaponSwitchCooldown = 4f;
					}
				}
			}
		}
		return _humanoid.GetCurrentWeapon();
	}

	private bool DoAttack(Character target)
	{
		ItemDrop.ItemData currentWeapon = _humanoid.GetCurrentWeapon();
		if (currentWeapon == null)
		{
			return false;
		}
		if (!_ai.CanUseAttack(currentWeapon))
		{
			return false;
		}
		Attack attack = currentWeapon.m_shared?.m_attack;
		bool flag = false;
		if (attack != null && attack.m_bowDraw)
		{
			flag = true;
			attack.m_bowDraw = false;
		}
		bool flag2 = _character.StartAttack(target, charge: false);
		if (flag && attack != null)
		{
			attack.m_bowDraw = true;
		}
		if (flag2)
		{
			_timeSinceAttacking = 0f;
		}
		return flag2;
	}

	private const float CombatLeash = 40f;

	private static bool IsPassiveAnimal(Character c)
	{
		return MHR_Threat.IsPassive(c);
	}

	private bool TryForceRangedWeapon()
	{
		ItemDrop.ItemData itemData = WeaponLogic.FindReadyBow(_humanoid, _targetCreature);
		if (itemData != null)
		{
			_humanoid.EquipItem(itemData);
			return true;
		}
		return false;
	}

	private bool TryForceMeleeWeapon()
	{
		Inventory inventory = _humanoid.GetInventory();
		if (inventory == null)
		{
			return false;
		}
		ItemDrop.ItemData itemData = null;
		float num = -1f;
		foreach (ItemDrop.ItemData allItem in inventory.GetAllItems())
		{
			// 1.5.2: skip weapons that shoot (a spear's long AI range is its throw, it's still a melee weapon)
			if (!allItem.IsEquipable() || MHR_IsProjectileWeapon(allItem))
			{
				continue;
			}
			ItemDrop.ItemData.ItemType itemType = allItem.m_shared.m_itemType;
			if ((itemType == ItemDrop.ItemData.ItemType.OneHandedWeapon || itemType == ItemDrop.ItemData.ItemType.TwoHandedWeapon || itemType == ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft) && !(allItem.GetDamage().m_pickaxe > 0f) && (!(allItem.GetDamage().m_chop > 0f) || allItem.GetDamage().m_slash != 0f || allItem.GetDamage().m_blunt != 0f || allItem.GetDamage().m_pierce != 0f) && itemType != ItemDrop.ItemData.ItemType.Tool && (!allItem.m_shared.m_useDurability || !(allItem.m_durability <= 0f)))
			{
				float combatDamage = GetCombatDamage(allItem);
				if (combatDamage > num)
				{
					num = combatDamage;
					itemData = allItem;
				}
			}
		}
		if (itemData != null)
		{
			_humanoid.EquipItem(itemData);
			return true;
		}
		return false;
	}

	private static float GetCombatDamage(ItemDrop.ItemData item)
	{
		return WeaponLogic.Score(item, null);
	}
}
