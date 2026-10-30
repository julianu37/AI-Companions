using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Companions;

[HarmonyPatch]
internal static class PortalPatches
{
	private static readonly List<ZDOID> _pendingCompanions = new List<ZDOID>();

	// 1.5.62: the portal you're going through (TeleportWorld.Teleport -> Player.TeleportTo): whether it lets everything
	// through. Dungeon entrances and map teleports also go through TeleportTo, but the game checks no items there.
	private static bool s_viaPortal;

	private static bool s_portalAllowAll;

	private static float s_portalAt = -10f;

	private const float ChestRange = 15f;

	[HarmonyPatch(typeof(TeleportWorld), "Teleport")]
	[HarmonyPrefix]
	private static void PortalTeleport_Prefix(TeleportWorld __instance)
	{
		s_portalAllowAll = __instance != null && __instance.m_allowAllItems;
		s_portalAt = Time.time;
	}

	[HarmonyPatch(typeof(Player), "TeleportTo")]
	[HarmonyPostfix]
	private static void TeleportTo_Postfix(Player __instance, bool __result)
	{
		if (!__result || __instance != Player.m_localPlayer)
		{
			return;
		}
		_pendingCompanions.Clear();
		s_viaPortal = Time.time - s_portalAt < 1f;
		string text = __instance.GetPlayerID().ToString();
		foreach (CompanionSetup allCompanion in CompanionSetup.AllCompanions)
		{
			ZNetView component = allCompanion.GetComponent<ZNetView>();
			if (component?.GetZDO() == null)
			{
				continue;
			}
			ZDO zDO = component.GetZDO();
			if (!(zDO.GetString(CompanionSetup.OwnerHash) != text))
			{
				int num = zDO.GetInt(CompanionSetup.ActionModeHash);
				if (num != 4 && zDO.GetBool(CompanionSetup.FollowHash, defaultValue: true) && MayGoThrough(allCompanion))
				{
					_pendingCompanions.Add(zDO.m_uid);
				}
			}
		}
		if (_pendingCompanions.Count > 0)
		{
			CompanionsPlugin.Log.LogInfo($"[Portal] Player teleporting — {_pendingCompanions.Count} companion(s) queued");
		}
	}

	// 1.5.62: through a portal a companion follows the game's item rules like you (user: "add the check for items that
	// cannot go through the portals ... match whatever the game is set at as a default and be adjustable"). What can't go
	// through goes into a chest within 15 m of it - at once, as its area unloads with you gone (user: "The Player wont see
	// it so in this case its ok to cheat a bit") - otherwise it stays behind and says why.
	private static bool MayGoThrough(CompanionSetup cs)
	{
		if (!s_viaPortal || cs == null || (ModConfig.PortalItems?.Value ?? "Game") == "AllowAll")
		{
			return true;
		}
		Humanoid h = cs.GetComponent<Humanoid>();
		Inventory inv = h != null ? h.GetInventory() : null;
		if (inv == null || inv.IsTeleportable(s_portalAllowAll))
		{
			return true;
		}
		List<ItemDrop.ItemData> stuck = new List<ItemDrop.ItemData>();
		bool allowAll = s_portalAllowAll || ZoneSystem.instance.GetGlobalKey(GlobalKeys.TeleportAll);
		foreach (ItemDrop.ItemData it in inv.GetAllItems())
		{
			if (it.m_shared.m_toolTier >= 1000 || (!allowAll && !it.m_shared.m_teleportable))
			{
				stuck.Add(it);
			}
		}
		string name = cs.GetComponent<Character>()?.m_name ?? "companion";
		string what = stuck.Count > 0 ? MHR_List.Local(stuck[0].m_shared.m_name) : "that";
		// boss items never go in a chest - it keeps them and waits
		bool bossItem = stuck.Exists(i => i.m_shared.m_toolTier >= 1000);
		if (!bossItem && stuck.Count > 0 && LeaveInChest(cs, h, inv, stuck, out string left))
		{
			CompanionsPlugin.Log.LogInfo($"[Portal] {name} left {left} in a chest by the portal and comes through");
			cs.GetComponent<CompanionTalk>()?.Say(ModLocalization.LocFmt("hc_say_portal_left", left), force: true);
			return true;
		}
		CompanionsPlugin.Log.LogInfo($"[Portal] {name} can't take {what} through the portal{(bossItem ? " (a boss item)" : " and found no chest with room within " + ChestRange + " m")} - staying here");
		cs.GetComponent<CompanionTalk>()?.Say(ModLocalization.LocFmt("hc_say_portal_stay", what), force: true);
		return false;
	}

	// all of `stuck` into usable chests within 15 m (most of that item first); false (nothing moved) if they don't all fit
	private static bool LeaveInChest(CompanionSetup cs, Humanoid h, Inventory inv, List<ItemDrop.ItemData> stuck, out string left)
	{
		left = null;
		List<Container> chests = MHR_Chests.Near(h.transform.position, ChestRange).FindAll(c => MHR_Chests.Usable(c, cs));
		if (chests.Count == 0)
		{
			return false;
		}
		// fits everywhere? plan first so nothing is half moved
		Dictionary<ItemDrop.ItemData, Container> plan = new Dictionary<ItemDrop.ItemData, Container>();
		Dictionary<Container, int> slotsUsed = new Dictionary<Container, int>();
		foreach (ItemDrop.ItemData it in stuck)
		{
			Container best = null;
			int bestHas = -1;
			foreach (Container c in chests)
			{
				Inventory ci = c.GetInventory();
				slotsUsed.TryGetValue(c, out int used);
				bool fits = ci.CanAddItem(it, it.m_stack) && ci.GetEmptySlots() - used >= 1 || ci.CanAddItem(it, it.m_stack) && ci.CountItems(it.m_shared.m_name) > 0;
				if (!fits)
				{
					continue;
				}
				int has = ci.CountItems(it.m_shared.m_name);
				if (has > bestHas)
				{
					bestHas = has;
					best = c;
				}
			}
			if (best == null)
			{
				return false;
			}
			plan[it] = best;
			if (bestHas <= 0)
			{
				slotsUsed[best] = (slotsUsed.TryGetValue(best, out int u) ? u : 0) + 1;
			}
		}
		Dictionary<string, int> moved = new Dictionary<string, int>();
		foreach (KeyValuePair<ItemDrop.ItemData, Container> kv in plan)
		{
			ItemDrop.ItemData it = kv.Key;
			Container c = kv.Value;
			MHR_Chests.Claim(c);
			int n = it.m_stack;
			if (it.m_equipped)
			{
				h.UnequipItem(it, triggerEquipEffects: false);
			}
			if (c.GetInventory().AddItem(it))
			{
				inv.RemoveItem(it);
				string nice = MHR_List.Local(it.m_shared.m_name);
				moved[nice] = (moved.TryGetValue(nice, out int m) ? m : 0) + n;
			}
		}
		List<string> parts = new List<string>();
		foreach (KeyValuePair<string, int> kv in moved)
		{
			parts.Add(kv.Value + " " + kv.Key);
		}
		left = string.Join(", ", parts);
		return !inv.IsTeleportable(s_portalAllowAll) ? false : parts.Count > 0;
	}

	[HarmonyPatch(typeof(Player), "UpdateTeleport")]
	[HarmonyPrefix]
	private static void UpdateTeleport_Prefix(Player __instance, out bool __state)
	{
		__state = __instance == Player.m_localPlayer && __instance.IsTeleporting();
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Player), "UpdateTeleport")]
	private static void UpdateTeleport_Postfix(Player __instance, bool __state)
	{
		if (!__state || __instance.IsTeleporting() || __instance != Player.m_localPlayer || _pendingCompanions.Count == 0)
		{
			return;
		}
		Vector3 position = __instance.transform.position;
		CompanionsPlugin.Log.LogInfo($"[Portal] Teleport complete at {position:F1} — warping {_pendingCompanions.Count} companion(s)");
		foreach (ZDOID pendingCompanion in _pendingCompanions)
		{
			WarpCompanion(pendingCompanion, position);
		}
		_pendingCompanions.Clear();
	}

	private static void WarpCompanion(ZDOID zdoid, Vector3 targetPos)
	{
		ZDO zDO = ZDOMan.instance.GetZDO(zdoid);
		if (zDO == null)
		{
			CompanionsPlugin.Log.LogWarning($"[Portal] Companion ZDO {zdoid} not found — skipping");
			return;
		}
		// 1.5.69: a spot you can see, not through a wall (CompanionAI.MHR_SafeSpotNear)
		Vector3 vector = Player.m_localPlayer != null ? CompanionAI.MHR_SafeSpotNear(targetPos, -Player.m_localPlayer.transform.forward) : FindNearbyPosition(targetPos, 3f);
		// 1.4.152: this game takes the companion over first - a position written by a game that doesn't own it is
		// overwritten by the owner's (the warp was undone when another player's game ran the companion)
		if (!zDO.IsOwner())
		{
			zDO.SetOwner(ZDOMan.GetSessionID());
		}
		zDO.SetPosition(vector);
		GameObject gameObject = ZNetScene.instance?.FindInstance(zdoid);
		if (gameObject != null)
		{
			CompanionRest component = gameObject.GetComponent<CompanionRest>();
			if (component != null)
			{
				component.CancelDirected();
			}
			gameObject.transform.position = vector;
			Rigidbody component2 = gameObject.GetComponent<Rigidbody>();
			if (component2 != null)
			{
				component2.position = vector;
				component2.linearVelocity = Vector3.zero;
			}
			CompanionAI component3 = gameObject.GetComponent<CompanionAI>();
			if (component3 != null)
			{
				component3.CancelMoveTarget();
				component3.CancelPendingCart();
				component3.CancelPendingShipBoard();
				component3.CancelHiveOrder();
				component3.CancelPendingDeposit();
				component3.CancelTombstoneRecovery();
				component3.SetFollowTarget(Player.m_localPlayer?.gameObject);
			}
			CompanionsPlugin.Log.LogInfo($"[Portal] Warped companion to {vector:F1} (GameObject present)");
		}
		else
		{
			CompanionsPlugin.Log.LogInfo($"[Portal] Updated companion ZDO position to {vector:F1} (not loaded)");
		}
	}

	private static Vector3 FindNearbyPosition(Vector3 origin, float radius)
	{
		for (int i = 0; i < 20; i++)
		{
			Vector2 vector = Random.insideUnitCircle * radius;
			Vector3 vector2 = origin + new Vector3(vector.x, 0f, vector.y);
			if (ZoneSystem.instance != null && ZoneSystem.instance.FindFloor(vector2, out var height))
			{
				vector2.y = height;
				return vector2;
			}
		}
		return origin + Vector3.right * 2f;
	}
}
