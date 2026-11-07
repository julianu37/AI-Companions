using HarmonyLib;

namespace Companions;

[HarmonyPatch(typeof(Hud), "InRadial")]
internal static class InRadial_BlockForStarterPanel
{
	private static void Postfix(ref bool __result)
	{
		if (__result)
		{
			return;
		}
		// 1.8.3 (patch audit): PlayerController.LateUpdate reads Hud.instance.m_radialMenu right after
		// Hud.InRadial() returns true, with no null check of its own. Faking "true" while the HUD (or its radial
		// menu) is missing would throw an NRE every frame, so the fake only happens when the invariant holds.
		Hud hud = Hud.instance;
		if (hud == null || hud.m_radialMenu == null)
		{
			return;
		}
		// 04/11/2026: one list for every window of the mod (ModUiLayers.AnyWindowOpen) - the same answer the panels, the
		// voice key and the command key read. Winning this fake keeps the game's own windows (inventory, menu) from
		// opening over ours. The farm zone placer is not here: it is aimed while walking (see ModUiLayers).
		if (ModUiLayers.AnyWindowOpen)
		{
			__result = true;
		}
	}
}
