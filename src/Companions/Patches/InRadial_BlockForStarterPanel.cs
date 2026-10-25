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
		StarterCompanionPanel instance = StarterCompanionPanel.Instance;
		if (instance != null && instance.IsVisible)
		{
			__result = true;
			return;
		}
		ConfigPanel instance2 = ConfigPanel.Instance;
		if (instance2 != null && instance2.IsVisible)
		{
			__result = true;
			return;
		}
		HomeZonePanel instance3 = HomeZonePanel.Instance;
		if (instance3 != null && instance3.IsVisible)
		{
			__result = true;
			return;
		}
		CompanionAiPanel instance4 = CompanionAiPanel.Instance;
		if (instance4 != null && instance4.IsVisible)
		{
			__result = true;
			return;
		}
		// Fase 3.20: the appearance editor (a modal window of its own, above the AI panel)
		if (CompanionAppearancePanel.IsOpen)
		{
			__result = true;
		}
	}
}
