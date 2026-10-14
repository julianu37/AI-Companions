using HarmonyLib;

namespace Companions;

[HarmonyPatch(typeof(Bed), "GetHoverText")]
public static class BedClaimHoverTextPatch
{
	[HarmonyPostfix]
	private static void Postfix(Bed __instance, ref string __result)
	{
		long bedOwnerSafe = CompanionRest.GetBedOwnerSafe(__instance);
		if (bedOwnerSafe != 0)
		{
			// 1.5.1: a bed of yours a companion sleeps in - not once it's your spawn point (the companion gives it up then)
			string text = CompanionRest.MHR_GetBedClaimName(__instance);
			if (text != null && Player.m_localPlayer != null && Player.m_localPlayer.GetPlayerID() == bedOwnerSafe && !__instance.IsCurrent())
			{
				// the first line is your name - show the companion's bed instead, like an unclaimed bed it sleeps in, and keep
				// the game's own lines below it (sleep / set spawn point)
				int num = __result.IndexOf('\n');
				__result = ModLocalization.LocFmt("hc_hover_bed", text) + ((num >= 0) ? __result.Substring(num) : "");
			}
		}
		else
		{
			string value = CompanionRest.MHR_GetBedClaimName(__instance);
			if (value != null || CompanionRest._bedClaimNames.TryGetValue(__instance.GetInstanceID(), out value))
			{
				__result = ModLocalization.LocFmt("hc_hover_bed", value);
			}
		}
	}
}
