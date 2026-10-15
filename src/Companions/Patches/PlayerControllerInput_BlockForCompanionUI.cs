using HarmonyLib;

namespace Companions;

[HarmonyPatch(typeof(PlayerController), "TakeInput")]
internal static class PlayerControllerInput_BlockForCompanionUI
{
	private static void Postfix(ref bool __result)
	{
		if (!__result)
		{
			return;
		}
		StarterCompanionPanel instance = StarterCompanionPanel.Instance;
		if (instance != null && instance.IsVisible)
		{
			__result = false;
			return;
		}
		ConfigPanel instance2 = ConfigPanel.Instance;
		if (instance2 != null && instance2.IsVisible)
		{
			__result = false;
			return;
		}
		CompanionInteractPanel instance3 = CompanionInteractPanel.Instance;
		if (instance3 != null && instance3.IsNameInputFocused)
		{
			__result = false;
			return;
		}
		CompanionRadialMenu instance4 = CompanionRadialMenu.Instance;
		if (instance4 != null && instance4.IsVisible)
		{
			__result = false;
			return;
		}
		HomeZonePanel instance5 = HomeZonePanel.Instance;
		if (instance5 != null && instance5.IsVisible)
		{
			__result = false;
			return;
		}
		CompanionAiPanel instance6 = CompanionAiPanel.Instance;
		if (instance6 != null && instance6.IsVisible)
		{
			__result = false;
		}
	}
}
