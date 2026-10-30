using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Companions;

// 1.5.62: the game only treats the player as encumbered (Player.IsEncumbered: pack over Player.GetMaxCarryWeight) - for
// everyone else Character.IsEncumbered is false, so a companion over its limit still ran and jumped (user: "The NPC cannot
// become encumbered currently"). For a companion it's its pack over CompanionTierData.CarryLimit; the game then does the
// rest as for the player: walking pace (Character.UpdateWalking), no running or jumping, no block, the loaded-down
// animation. Out of stamina while encumbered it can't move, like Player.CanMove.
internal static class MHR_Encumbrance
{
	private static readonly Dictionary<int, bool> s_isCompanion = new Dictionary<int, bool>();

	internal static bool IsCompanion(Character c)
	{
		int id = c.GetInstanceID();
		if (!s_isCompanion.TryGetValue(id, out bool yes))
		{
			if (s_isCompanion.Count > 1000)
			{
				s_isCompanion.Clear();
			}
			yes = c.GetComponent<CompanionSetup>() != null;
			s_isCompanion[id] = yes;
		}
		return yes;
	}

	internal static bool Over(Character c)
	{
		Inventory inv = (c as Humanoid)?.GetInventory();
		// upstream 1.5.62 measured the ceiling with CompanionTierData.CarryLimit; we use CompanionCarry (Fase 3.22:
		// [General] MaxCarryWeight + the belt bonus read from the item asset)
		return inv != null && inv.GetTotalWeight() > CompanionCarry.Max(inv);
	}
}

[HarmonyPatch(typeof(Character), nameof(Character.IsEncumbered))]
internal static class Character_IsEncumbered_Companion
{
	private static void Postfix(Character __instance, ref bool __result)
	{
		if (__result || __instance == null || __instance.IsPlayer() || !MHR_Encumbrance.IsCompanion(__instance))
		{
			return;
		}
		__result = MHR_Encumbrance.Over(__instance);
	}
}

[HarmonyPatch(typeof(Character), nameof(Character.CanMove))]
internal static class Character_CanMove_EncumberedCompanion
{
	private static void Postfix(Character __instance, ref bool __result)
	{
		if (!__result || __instance == null || __instance.IsPlayer() || !MHR_Encumbrance.IsCompanion(__instance) || !MHR_Encumbrance.Over(__instance))
		{
			return;
		}
		CompanionStamina st = __instance.GetComponent<CompanionStamina>();
		if (st != null && st.Stamina <= 0f)
		{
			__result = false;
		}
	}
}
