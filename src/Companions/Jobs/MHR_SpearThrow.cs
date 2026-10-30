using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Companions;

// 1.5.2: hunting skittish game (deer, hares...) with a spear: throw it like a player (the spear's own secondary attack),
// but only with another weapon in the pack to fight on with, then walk over and pick the spear back up.
// (user: "make the NPC throw the spear when hunting one of the timid / flee animals ... only if they have another
// weapon in their inventory. The NPC should then go over and recover the spear.")
// The game drops a thrown spear as a normal item where it lands (Projectile.m_respawnItemOnHit); a patch on
// ItemDrop.DropItem spots our spear landing by its item, so the companion knows exactly where to go.
internal static class MHR_SpearThrow
{
	private class State
	{
		public ItemDrop.ItemData Thrown;

		public string Name;

		public float ThrownAt;

		public ItemDrop Drop;

		public float NextTry;

		public float NextWhyLog;
	}

	private const float MinThrowDistance = 3f;

	private const float LandWaitSeconds = 15f;

	private const float GiveUpSeconds = 120f;

	private static readonly Dictionary<CompanionAI, State> s_states = new Dictionary<CompanionAI, State>();

	private static readonly Dictionary<ItemDrop.ItemData, State> s_inFlight = new Dictionary<ItemDrop.ItemData, State>();

	private static State Get(CompanionAI ai)
	{
		if (!s_states.TryGetValue(ai, out var value))
		{
			value = new State();
			s_states[ai] = value;
		}
		return value;
	}

	internal static bool IsThrowable(ItemDrop.ItemData w)
	{
		if (w == null || !w.HaveSecondaryAttack())
		{
			return false;
		}
		Attack secondaryAttack = w.m_shared.m_secondaryAttack;
		if (secondaryAttack != null && secondaryAttack.m_attackType == Attack.AttackType.Projectile)
		{
			return secondaryAttack.m_consumeItem;
		}
		return false;
	}

	// game that runs away: AnimalAI (deer, hares...) or a monster that flees until it's alerted
	private static bool Skittish(Character c)
	{
		BaseAI baseAI = c?.GetBaseAI();
		if (!(baseAI is AnimalAI))
		{
			if (baseAI is MonsterAI monsterAI)
			{
				return monsterAI.m_fleeIfNotAlerted;
			}
			return false;
		}
		return true;
	}

	// 1.5.4: the prey has noticed the hunter (it's alerted or after someone) - no sneaking up on it any more
	private static bool Noticed(Character c)
	{
		BaseAI baseAI = c?.GetBaseAI();
		if (baseAI != null)
		{
			if (!baseAI.IsAlerted())
			{
				return baseAI.GetTargetCreature() != null;
			}
			return true;
		}
		return false;
	}

	// 1.5.4: a throw needs another weapon to fight on with - or prey that has noticed us and is about to bolt, when the
	// spear is the only chance (user: "Throw the spear when hunting timid animals when noticed.")
	private static bool MayThrow(Humanoid hum, ItemDrop.ItemData spear, Character prey)
	{
		if (!HasOtherWeapon(hum, spear))
		{
			return Noticed(prey);
		}
		return true;
	}

	// a spear is out (thrown, not picked back up) and nothing else to fight with is left in the pack
	internal static bool UnarmedAfterThrow(CompanionAI ai, Humanoid hum)
	{
		if (ai != null && hum != null && s_states.TryGetValue(ai, out var value) && value.Thrown != null)
		{
			return !HasOtherWeapon(hum, null);
		}
		return false;
	}

	// something else to fight with once the spear is gone (not a tool, pickaxe, fishing rod or broken item)
	private static bool HasOtherWeapon(Humanoid hum, ItemDrop.ItemData spear)
	{
		Inventory inventory = hum.GetInventory();
		if (inventory == null)
		{
			return false;
		}
		foreach (ItemDrop.ItemData allItem in inventory.GetAllItems())
		{
			if (allItem == spear || allItem?.m_shared == null)
			{
				continue;
			}
			ItemDrop.ItemData.ItemType itemType = allItem.m_shared.m_itemType;
			if ((itemType == ItemDrop.ItemData.ItemType.OneHandedWeapon || itemType == ItemDrop.ItemData.ItemType.TwoHandedWeapon || itemType == ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft || itemType == ItemDrop.ItemData.ItemType.Bow) && (!allItem.m_shared.m_useDurability || !(allItem.m_durability <= 0f)) && !(allItem.m_shared.m_damages.m_pickaxe > 0f) && allItem.m_shared.m_skillType != Skills.SkillType.Fishing)
			{
				return true;
			}
		}
		return false;
	}

	// 1.5.4: a throw is on for this prey (spear in hand, skittish game, another weapon in the pack, no spear out), whatever
	// the distance - the hunter then stops at ThrowStandoff instead of walking up to stab it
	internal static bool ReadyToThrow(CompanionAI ai, Humanoid hum, Character prey)
	{
		if (ai == null || hum == null || prey == null || !(ModConfig.HuntThrowSpear?.Value ?? true) || !Skittish(prey))
		{
			return false;
		}
		if (s_states.TryGetValue(ai, out var value) && value.Thrown != null)
		{
			return false;
		}
		ItemDrop.ItemData currentWeapon = hum.GetCurrentWeapon();
		if (IsThrowable(currentWeapon))
		{
			return MayThrow(hum, currentWeapon, prey);
		}
		return false;
	}

	// how close to stop for a throw
	internal static float Standoff()
	{
		float range = ModConfig.HuntThrowRange?.Value ?? 12f;
		return Mathf.Clamp(ModConfig.HuntThrowStandoff?.Value ?? 10f, MinThrowDistance + 0.5f, Mathf.Max(MinThrowDistance + 0.5f, range - 1f));
	}

	private static void Why(State st, string text)
	{
		if (Time.time >= st.NextWhyLog)
		{
			st.NextWhyLog = Time.time + 5f;
			CompanionsPlugin.Log.LogDebug("[Hunt] Not throwing yet: " + text);
		}
	}

	// hunting: holding a throwable spear, the prey is skittish game within throwing range, another weapon in the pack,
	// no spear already out
	internal static bool WantsThrow(CompanionAI ai, Humanoid hum, Character prey, float dist)
	{
		if (ai == null || hum == null || prey == null || !(ModConfig.HuntThrowSpear?.Value ?? true) || hum.InAttack())
		{
			return false;
		}
		State state = Get(ai);
		if (state.Thrown != null || Time.time < state.NextTry)
		{
			return false;
		}
		if (dist < MinThrowDistance || dist > (ModConfig.HuntThrowRange?.Value ?? 12f))
		{
			return false;
		}
		ItemDrop.ItemData currentWeapon = hum.GetCurrentWeapon();
		if (!IsThrowable(currentWeapon) || !Skittish(prey) || !MayThrow(hum, currentWeapon, prey))
		{
			return false;
		}
		// 1.5.4: the game refuses an attack it can't pay for - wait for the stamina rather than walking in to stab
		float cost = currentWeapon.m_shared.m_secondaryAttack.m_attackStamina;
		if (cost > 0f && !hum.HaveStamina(cost + 0.1f))
		{
			CompanionStamina stam = hum.GetComponent<CompanionStamina>();
			Why(state, $"not enough stamina for a throw ({(stam != null ? stam.Stamina : 0f):F0}, needs {cost:F0})");
			return false;
		}
		return true;
	}

	// hunting skittish game with no bow: hold a throwable spear (when there's another weapon to fall back on and no spear
	// is out already). True when one is in hand.
	internal static bool EquipForHunt(CompanionAI ai, Humanoid hum, Character prey)
	{
		if (ai == null || hum == null || prey == null || !(ModConfig.HuntThrowSpear?.Value ?? true) || !Skittish(prey))
		{
			return false;
		}
		if (s_states.TryGetValue(ai, out var value) && value.Thrown != null)
		{
			return false;
		}
		ItemDrop.ItemData currentWeapon = hum.GetCurrentWeapon();
		if (IsThrowable(currentWeapon) && MayThrow(hum, currentWeapon, prey))
		{
			return true;
		}
		Inventory inventory = hum.GetInventory();
		if (inventory == null)
		{
			return false;
		}
		ItemDrop.ItemData itemData = null;
		foreach (ItemDrop.ItemData allItem in inventory.GetAllItems())
		{
			if (IsThrowable(allItem) && (!allItem.m_shared.m_useDurability || allItem.m_durability > 0f) && MayThrow(hum, allItem, prey) && (itemData == null || allItem.GetDamage().GetTotalDamage() > itemData.GetDamage().GetTotalDamage()))
			{
				itemData = allItem;
			}
		}
		if (itemData == null || !hum.EquipItem(itemData))
		{
			return false;
		}
		CompanionsPlugin.Log.LogDebug("[Hunt] Took out its " + Loc(itemData.m_shared.m_name) + " to throw at " + Loc(prey.m_name));
		return true;
	}

	internal static void Throw(CompanionAI ai, Humanoid hum, Character prey)
	{
		State state = Get(ai);
		ItemDrop.ItemData currentWeapon = hum.GetCurrentWeapon();
		if (currentWeapon == null)
		{
			return;
		}
		state.NextTry = Time.time + 2f;
		if (hum.StartAttack(prey, secondaryAttack: true))
		{
			state.Thrown = currentWeapon;
			state.Name = currentWeapon.m_shared.m_name;
			state.ThrownAt = Time.time;
			state.Drop = null;
			s_inFlight[currentWeapon] = state;
			CompanionsPlugin.Log.LogInfo("[Hunt] Threw its " + Loc(state.Name) + " at " + Loc(prey.m_name) + $" ({Vector3.Distance(ai.transform.position, prey.transform.position):F0} m)");
			ai.GetComponent<CompanionCombatAI>()?.NoteSwing(prey, "Throw", $"{Vector3.Distance(ai.transform.position, prey.transform.position):F1} m, {prey.transform.position.y - ai.transform.position.y:+0.0;-0.0} m up, prey moving {prey.GetVelocity().magnitude:F1} m/s", 4f);
		}
	}

	// the thrown spear has landed as an item
	internal static void OnDropped(ItemDrop.ItemData item, ItemDrop drop)
	{
		if (item != null && !(drop == null) && s_inFlight.TryGetValue(item, out var value))
		{
			s_inFlight.Remove(item);
			value.Drop = drop;
			CompanionsPlugin.Log.LogDebug("[Hunt] The thrown " + Loc(value.Name) + $" landed at {drop.transform.position:F1}");
		}
	}

	private static void Clear(State st)
	{
		if (st.Thrown != null)
		{
			s_inFlight.Remove(st.Thrown);
		}
		st.Thrown = null;
		st.Drop = null;
	}

	// after the fight: walk over to the spear and pick it up. True while busy with it.
	internal static bool Drive(CompanionAI ai, float dt, bool inCombat)
	{
		if (ai == null || !s_states.TryGetValue(ai, out var value) || value.Thrown == null)
		{
			return false;
		}
		Humanoid component = ai.GetComponent<Humanoid>();
		Inventory inventory = component?.GetInventory();
		if (inventory == null)
		{
			Clear(value);
			return false;
		}
		// the attack never went off (staggered mid-throw): the spear is still in the pack
		if (value.Drop == null && inventory.ContainsItem(value.Thrown) && Time.time - value.ThrownAt > 3f)
		{
			Clear(value);
			return false;
		}
		if (inCombat)
		{
			return false;
		}
		if (Time.time - value.ThrownAt > GiveUpSeconds)
		{
			CompanionsPlugin.Log.LogInfo("[Hunt] Gave up on getting its " + Loc(value.Name) + " back (took too long)");
			ai.GetComponent<CompanionTalk>()?.SayProblem("spear:lost", ModLocalization.LocFmt("hc_why_spear_lost", Loc(value.Name)));
			Clear(value);
			return false;
		}
		if ((object)value.Drop == null)
		{
			if (Time.time - value.ThrownAt > LandWaitSeconds)
			{
				CompanionsPlugin.Log.LogInfo("[Hunt] Lost its " + Loc(value.Name) + " - it never landed anywhere it could find (water?)");
				ai.GetComponent<CompanionTalk>()?.SayProblem("spear:lost", ModLocalization.LocFmt("hc_why_spear_lost", Loc(value.Name)));
				Clear(value);
			}
			return false;
		}
		if (value.Drop == null)
		{
			// 1.5.2: usually it walked over it and its own auto-pickup took it (user log: "someone picked it up" 3 m away)
			bool flag2 = false;
			foreach (ItemDrop.ItemData allItem in inventory.GetAllItems())
			{
				if (allItem?.m_shared != null && allItem.m_shared.m_name == value.Name)
				{
					flag2 = true;
					break;
				}
			}
			CompanionsPlugin.Log.LogInfo("[Hunt] " + (flag2 ? ("Picked its " + Loc(value.Name) + " back up on the way") : ("Its " + Loc(value.Name) + " isn't on the ground any more (someone else picked it up)")));
			Clear(value);
			return false;
		}
		Vector3 position = value.Drop.transform.position;
		Vector3 position2 = ai.transform.position;
		if (new Vector2(position2.x - position.x, position2.z - position.z).magnitude <= 1.6f && Mathf.Abs(position2.y - position.y) <= 2f)
		{
			if (!value.Drop.CanPickup())
			{
				value.Drop.RequestOwn();
				ai.StopMoving();
				return true;
			}
			value.Drop.Load();
			bool flag = component.Pickup(value.Drop.gameObject, autoequip: false, autoPickupDelay: false);
			CompanionsPlugin.Log.LogInfo("[Hunt] " + (flag ? "Picked its " : "Couldn't pick up its ") + Loc(value.Name) + (flag ? " back up" : " (pack full?)"));
			MHR_Mover.Release(ai);
			Clear(value);
			return false;
		}
		MHR_Mover.Go(ai, dt, position, 0.8f, allowRun: true, "Spear");
		if (MHR_Mover.RouteSwim(ai) > MHR_Mover.JobSwim() || MHR_Mover.GaveUp(ai))
		{
			CompanionsPlugin.Log.LogInfo("[Hunt] Can't get to its " + Loc(value.Name) + " - leaving it where it landed");
			MHR_Mover.Release(ai);
			Clear(value);
			return false;
		}
		return true;
	}

	private static string Loc(string s)
	{
		if (Localization.instance == null || string.IsNullOrEmpty(s))
		{
			return s ?? "";
		}
		return Localization.instance.Localize(s);
	}
}

[HarmonyPatch(typeof(ItemDrop), "DropItem")]
internal static class SpearLandedPatch
{
	private static void Postfix(ItemDrop.ItemData item, ItemDrop __result)
	{
		MHR_SpearThrow.OnDropped(item, __result);
	}
}

// 1.5.2: a companion's thrown spear (or arrow, bolt) is aimed like a player would: ahead of a running target and a bit
// high for the drop. The game aims an AI's projectile straight at the target's middle (Attack.GetProjectileSpawnPoint),
// so it fell short and behind a deer on the run (user: "the aim with the spear throw is not great against deer").
[HarmonyPatch(typeof(Attack), "GetProjectileSpawnPoint")]
internal static class CompanionProjectileAimPatch
{
	// 1.5.42: a point to shoot at that isn't a creature (a bird on the ground) - set just before the shot
	private static readonly System.Collections.Generic.Dictionary<Humanoid, KeyValuePair<Vector3, float>> s_point = new System.Collections.Generic.Dictionary<Humanoid, KeyValuePair<Vector3, float>>();

	internal static void AimAt(Humanoid h, Vector3 point)
	{
		if (h != null)
		{
			s_point[h] = new KeyValuePair<Vector3, float>(point, Time.time);
		}
	}

	private static readonly AccessTools.FieldRef<Attack, Humanoid> s_character = AccessTools.FieldRefAccess<Attack, Humanoid>("m_character");

	private static readonly AccessTools.FieldRef<Attack, BaseAI> s_baseAI = AccessTools.FieldRefAccess<Attack, BaseAI>("m_baseAI");

	private static readonly AccessTools.FieldRef<Attack, ItemDrop.ItemData> s_ammo = AccessTools.FieldRefAccess<Attack, ItemDrop.ItemData>("m_ammoItem");

	private static readonly AccessTools.FieldRef<Attack, float> s_drawPct = AccessTools.FieldRefAccess<Attack, float>("m_attackDrawPercentage");

	private static void Postfix(Attack __instance, ref Vector3 spawnPoint, ref Vector3 aimDir)
	{
		try
		{
			Humanoid humanoid = s_character(__instance);
			if (humanoid == null || humanoid.GetComponent<CompanionSetup>() == null)
			{
				return;
			}
			Character character = s_baseAI(__instance)?.GetTargetCreature();
			bool hasPoint = s_point.TryGetValue(humanoid, out var pt) && Time.time - pt.Value < 2f;
			if (character == null && !hasPoint)
			{
				return;
			}
			float num = __instance.m_projectileVel;
			float velMin = __instance.m_projectileVelMin;
			AnimationCurve curve = __instance.m_drawVelocityCurve;
			GameObject gameObject = __instance.m_attackProjectile;
			ItemDrop.ItemData itemData = s_ammo(__instance);
			if (itemData?.m_shared?.m_attack != null)
			{
				num += itemData.m_shared.m_attack.m_projectileVel;
				velMin += itemData.m_shared.m_attack.m_projectileVelMin;
				curve = itemData.m_shared.m_attack.m_drawVelocityCurve;
				if (itemData.m_shared.m_attack.m_attackProjectile != null)
				{
					gameObject = itemData.m_shared.m_attack.m_attackProjectile;
				}
			}
			// 1.5.63: a bow arrow flies at the speed of the draw (Attack.ProjectileAttackTriggered) - a partial draw is slower
			// and drops more, so aim for that speed, not the full-draw one (user: "The arrows do have some drop as they travel
			// so that needs to be taken into account" - half-drawn arrows went under a boar 15 m off and 4 m up)
			if (__instance.m_bowDraw && curve != null)
			{
				num = Mathf.Lerp(velMin, num, curve.Evaluate(s_drawPct(__instance)));
			}
			if (num < 1f)
			{
				return;
			}
			float num2 = 0f;
			Projectile projectile = ((gameObject != null) ? gameObject.GetComponent<Projectile>() : null);
			if (projectile != null)
			{
				num2 = projectile.m_gravity;
			}
			// a bird (no creature target): aim at its point, standing still (user: "Gunter keeps trying to shoot a birds and
			// shoots directly into the hillside" - the game fired along its facing with no allowance for the drop)
			Vector3 centerPoint = (character != null) ? character.GetCenterPoint() : pt.Key;
			Vector3 velocity = (character != null) ? character.GetVelocity() : Vector3.zero;
			Vector3 vector = centerPoint;
			float num3 = 0f;
			for (int i = 0; i < 3; i++)
			{
				num3 = Vector3.Distance(spawnPoint, vector) / num;
				vector = centerPoint + velocity * num3;
			}
			vector.y += 0.5f * num2 * num3 * num3;
			Vector3 vector2 = vector - spawnPoint;
			// only a correction: if the AI's target isn't what it's shooting at (birds, a stale target), leave the game's aim
			if (vector2.sqrMagnitude > 0.01f && Vector3.Angle(aimDir, vector2) <= 25f)
			{
				aimDir = vector2.normalized;
			}
		}
		catch
		{
		}
	}
}

