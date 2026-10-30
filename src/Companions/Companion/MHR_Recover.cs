using System.Collections.Generic;
using UnityEngine;

namespace Companions;

// 1.5.44 (GitHub issue #17): after a revive, get the gear back before fighting again. Femis64: "My companion was in Melee
// mode in the swamp and got killed by a leech. Every time I revived her she went straight back on the attack and died
// again in seconds." User design (2026-10-14): with [Death] RespawnStance = RecoverFirst the companion comes back Passive
// with auto-pickup off, goes for its tombstone when it's safe (CompanionAI.UpdateTombstoneRecovery, any distance while
// recovering), puts its gear back on, eats, then goes back to the stance it died with. Attacked while armed it fights
// back (CompanionCombatAI.Engage); unarmed it keeps to the player. Setting a stance yourself ends it.
internal static class MHR_Recover
{
	internal static readonly int RecoveringHash = "HC_Recovering".GetStableHashCode();

	internal static readonly int RecoverStanceHash = "HC_RecoverStance".GetStableHashCode();

	internal static readonly int RecoverPickupHash = "HC_RecoverPickup".GetStableHashCode();

	private class State
	{
		public float NextCheck;

		public float NoTombSince = -1f;

		public float GearSince = -1f;

		public bool WaitSaid;

		// the hover status while recovering (a localization key)
		public string Status;
	}

	private static readonly Dictionary<int, State> s_state = new Dictionary<int, State>();

	// RespawnStance: "RecoverFirst", "Passive" or "Keep"
	internal static string Mode => ModConfig.RespawnStance?.Value ?? "RecoverFirst";

	// called by the revive (CompanionTombstoneResurrect) on the new companion's ZDO, after the stance was copied over
	internal static void OnRevived(ZDO zdo, int diedStance, bool autoPickup)
	{
		if (zdo == null)
		{
			return;
		}
		string mode = Mode;
		if (mode == "Keep")
		{
			return;
		}
		zdo.Set(CompanionSetup.CombatStanceHash, CompanionSetup.StancePassive);
		if (mode != "RecoverFirst")
		{
			return;
		}
		zdo.Set(RecoveringHash, value: true);
		zdo.Set(RecoverStanceHash, diedStance);
		zdo.Set(RecoverPickupHash, autoPickup);
		// so it never ends up leaving something in the tombstone (user)
		zdo.Set(CompanionSetup.AutoPickupHash, value: false);
		CompanionsPlugin.Log.LogInfo("[Recover] Revived Passive with auto-pickup off - getting its gear back before going back to stance " + diedStance);
	}

	internal static bool IsRecovering(Component c)
	{
		ZDO zdo = c?.GetComponent<ZNetView>()?.GetZDO();
		return zdo != null && zdo.GetBool(RecoveringHash);
	}

	// the hover status line while recovering, else null
	internal static string StatusText(Component c)
	{
		if (c == null || !IsRecovering(c) || !s_state.TryGetValue(c.gameObject.GetInstanceID(), out var st) || st.Status == null)
		{
			return null;
		}
		return ModLocalization.Loc(st.Status);
	}

	// you set a stance yourself (wheel or stance key): that's the stance now, recovery is over
	internal static void Cancel(CompanionSetup cs)
	{
		ZDO zdo = cs?.GetComponent<ZNetView>()?.GetZDO();
		if (zdo == null || !zdo.GetBool(RecoveringHash))
		{
			return;
		}
		zdo.Set(RecoveringHash, value: false);
		zdo.Set(CompanionSetup.AutoPickupHash, zdo.GetBool(RecoverPickupHash, defaultValue: true));
		s_state.Remove(cs.gameObject.GetInstanceID());
		CompanionsPlugin.Log.LogInfo("[Recover] You set a stance - recovery over");
	}

	// while recovering it's Passive, but it does fight back when something attacks it and it has a weapon in hand
	internal static bool MayDefend(Component c, Character target)
	{
		if (target == null || !IsRecovering(c))
		{
			return false;
		}
		Character me = c.GetComponent<Character>();
		Humanoid h = me as Humanoid;
		return target.GetBaseAI()?.GetTargetCreature() == me && h != null && h.GetCurrentWeapon() != null && h.GetCurrentWeapon() != h.m_unarmedWeapon?.m_itemData;
	}

	// it was just hit by attacker (the attacker's target may not point at it yet)
	internal static bool MayDefendHit(Component c, Character attacker)
	{
		if (attacker == null || attacker.IsPlayer() || !IsRecovering(c))
		{
			return false;
		}
		Humanoid h = c.GetComponent<Humanoid>();
		return h != null && h.GetCurrentWeapon() != null && h.GetCurrentWeapon() != h.m_unarmedWeapon?.m_itemData;
	}

	// is it safe to go for the tombstone at tomb? (MonsterAI.UpdateTarget, decompiled: every 2 s a mob takes the closest
	// enemy it can sense, so a mob stays on the player only while the player is closer to it than the companion is)
	// committed = already on its way. 1.5.44 test: "Got most of the way there then a mob was around. Would have been
	// better to finish getting the gear then working back to me. It looked like they were closer to the gear than to me."
	// On the way it only turns back for a mob that's in the way (nearer the tombstone than it is) or about to catch it
	// before it gets there - and never while the tombstone is nearer than the player.
	internal static bool SafeToGo(Character me, Vector3 tomb, out string why, GameObject owner = null, bool committed = false)
	{
		why = null;
		float range = ModConfig.RecoverThreatRange?.Value ?? 15f;
		int maxMobs = ModConfig.RecoverMaxMobs?.Value ?? 3;
		Vector3 pos = me.transform.position;
		float toTomb = Vector3.Distance(pos, tomb);
		float toOwner = (owner != null) ? Vector3.Distance(pos, owner.transform.position) : float.MaxValue;
		// only counts as on its way once it's nearer the gear than you (at the start it's by your side)
		committed = committed && toTomb < toOwner;
		int busy = 0;
		foreach (Character c in Character.GetAllCharacters())
		{
			if (c == null || c == me || c.IsDead() || c.IsPlayer() || !BaseAI.IsEnemy(me, c))
			{
				continue;
			}
			float dMe = Vector3.Distance(c.transform.position, pos);
			float dTomb = Vector3.Distance(c.transform.position, tomb);
			if (dMe > range && dTomb > range)
			{
				continue;
			}
			BaseAI ai = c.GetBaseAI();
			Character target = ai?.GetTargetCreature();
			bool alert = ai != null && (ai.IsAlerted() || target != null);
			if (!alert)
			{
				// asleep or wandering: only right on top of the tombstone
				if (dTomb < 4f)
				{
					why = c.m_name + " is right by the tombstone";
					return false;
				}
				continue;
			}
			if (target == me)
			{
				// after it: keep going if it gets there well before the mob gets to it
				if (toTomb + 3f < dMe)
				{
					continue;
				}
				// on its way and nearer the gear than you: finish getting it, unless the mob stands between it and the gear
				if (committed && dTomb > toTomb && dMe > 2f)
				{
					continue;
				}
				why = c.m_name + " is after it";
				return false;
			}
			if (committed && dTomb > 3f)
			{
				// busy with someone else and not on top of the tombstone: carry on
				continue;
			}
			busy++;
			// busy with someone else: don't come closer to it than its target is
			float theirs = (target != null) ? Vector3.Distance(c.transform.position, target.transform.position) : 0f;
			if (target != null && dTomb < theirs + 2f)
			{
				why = c.m_name + " is fighting by the tombstone";
				return false;
			}
		}
		if (busy > maxMobs)
		{
			why = busy + " enemies around";
			return false;
		}
		return true;
	}

	// per frame (from MHR_ToolKeeper.Tick): once the tombstone is empty, wait for the gear to go on, eat, then back to the
	// old stance
	internal static void Tick(CompanionSetup cs)
	{
		ZDO zdo = cs?.GetComponent<ZNetView>()?.GetZDO();
		if (zdo == null || !zdo.GetBool(RecoveringHash))
		{
			return;
		}
		int id = cs.gameObject.GetInstanceID();
		if (!s_state.TryGetValue(id, out var st))
		{
			st = new State();
			s_state[id] = st;
		}
		float now = Time.time;
		if (now < st.NextCheck)
		{
			return;
		}
		st.NextCheck = now + 1f;
		Character me = cs.GetComponent<Character>();
		if (me == null || me.IsDead())
		{
			return;
		}
		if (OwnTombWithItems(cs, zdo, out var tomb))
		{
			st.NoTombSince = -1f;
			st.GearSince = -1f;
			st.Status = "hc_status_recover";
			string why;
			if (!SafeToGo(me, tomb.transform.position, out why))
			{
				st.Status = "hc_status_recover_wait";
				if (!st.WaitSaid)
				{
					st.WaitSaid = true;
					CompanionsPlugin.Log.LogInfo("[Recover] Waiting before going for its tombstone: " + why);
					cs.GetComponent<CompanionTalk>()?.Speak(ModLocalization.Loc("hc_speech_recover_wait"), CompanionTalk.Priority.Event, "recover:wait");
				}
			}
			else
			{
				st.WaitSaid = false;
			}
			return;
		}
		Humanoid h = me as Humanoid;
		bool armed = h != null && h.GetCurrentWeapon() != null && h.GetCurrentWeapon() != h.m_unarmedWeapon?.m_itemData;
		// no tombstone with its things here: either it's emptied (gear back on its way on), or it's out of reach
		if (zdo.GetLong(CompanionSetup.TombstoneIdHash, 0L) != 0 && !armed)
		{
			if (st.NoTombSince < 0f)
			{
				st.NoTombSince = now;
			}
			if (now - st.NoTombSince < 60f)
			{
				st.Status = "hc_status_recover_far";
				return;
			}
			// out of reach for a minute: stay out of fights until you bring it its gear or set a stance
			End(cs, zdo, restoreStance: false);
			CompanionsPlugin.Log.LogInfo("[Recover] Its tombstone is out of reach - staying Passive until you set a stance");
			cs.GetComponent<CompanionTalk>()?.Speak(ModLocalization.Loc("hc_speech_recover_far"), CompanionTalk.Priority.Problem, "recover:far");
			return;
		}
		// gear on: wait for the equip queue, then eat, then back to work
		st.Status = "hc_status_recover_equip";
		if (cs.IsEquipping)
		{
			st.GearSince = -1f;
			return;
		}
		if (st.GearSince < 0f)
		{
			st.GearSince = now;
			return;
		}
		if (now - st.GearSince < 1.5f)
		{
			return;
		}
		cs.GetComponent<CompanionFood>()?.EatNow();
		End(cs, zdo, restoreStance: armed);
		if (armed)
		{
			cs.GetComponent<CompanionTalk>()?.Speak(ModLocalization.Loc("hc_speech_recover_done"), CompanionTalk.Priority.Event, "recover:done");
		}
	}

	private static void End(CompanionSetup cs, ZDO zdo, bool restoreStance)
	{
		zdo.Set(RecoveringHash, value: false);
		zdo.Set(CompanionSetup.AutoPickupHash, zdo.GetBool(RecoverPickupHash, defaultValue: true));
		if (restoreStance)
		{
			int stance = zdo.GetInt(RecoverStanceHash);
			zdo.Set(CompanionSetup.CombatStanceHash, stance);
			CompanionsPlugin.Log.LogInfo("[Recover] Gear back on and fed - back to stance " + stance);
		}
		s_state.Remove(cs.gameObject.GetInstanceID());
	}

	private static readonly Dictionary<int, KeyValuePair<float, TombStone>> s_tombCache = new Dictionary<int, KeyValuePair<float, TombStone>>();

	// its own tombstone, loaded, with something still in it (looked up every 3 s)
	internal static bool OwnTombWithItems(Component c, ZDO me, out TombStone tomb)
	{
		tomb = null;
		int id = c.gameObject.GetInstanceID();
		if (s_tombCache.TryGetValue(id, out var cached) && Time.time - cached.Key < 3f)
		{
			tomb = cached.Value;
		}
		else
		{
			Character ch = c.GetComponent<Character>();
			foreach (TombStone t in Object.FindObjectsByType<TombStone>(FindObjectsSortMode.None))
			{
				ZDO tz = t?.GetComponent<ZNetView>()?.GetZDO();
				if (tz != null && CompanionAI.MHR_IsOwnGrave(tz, me, ch))
				{
					tomb = t;
					break;
				}
			}
			s_tombCache[id] = new KeyValuePair<float, TombStone>(Time.time, tomb);
		}
		if (tomb == null)
		{
			return false;
		}
		Inventory inv = tomb.GetComponent<Container>()?.GetInventory();
		return inv != null && inv.NrOfItems() > 0;
	}
}
