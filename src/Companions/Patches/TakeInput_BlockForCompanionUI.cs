using HarmonyLib;

namespace Companions;

[HarmonyPatch(typeof(Player), "TakeInput")]
internal static class TakeInput_BlockForCompanionUI
{
	private static void Postfix(ref bool __result)
	{
		if (!__result)
		{
			return;
		}
		// 11/11/2026: one list for every UI of the mod (ModUiLayers) - the same answer the panels, the voice key and the
		// command key read. The wheel is not a window, so it is asked apart; the farm zone placer is left out on purpose
		// (it is aimed while the player still walks).
		if (ModUiLayers.WheelOpen || ModUiLayers.AnyWindowOpen)
		{
			__result = false;
		}
	}
}
