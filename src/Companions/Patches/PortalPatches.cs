using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Companions;

[HarmonyPatch]
internal static class PortalPatches
{
	private static readonly List<ZDOID> _pendingCompanions = new List<ZDOID>();

	[HarmonyPatch(typeof(Player), "TeleportTo")]
	[HarmonyPostfix]
	private static void TeleportTo_Postfix(Player __instance, bool __result)
	{
		if (!__result || __instance != Player.m_localPlayer)
		{
			return;
		}
		_pendingCompanions.Clear();
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
				if (num != 4 && zDO.GetBool(CompanionSetup.FollowHash, defaultValue: true))
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
