using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Companions;

internal static class MenuPatches
{
	private const string ButtonName = "HC_ModOptions";

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Menu), "Show")]
	private static void MenuShow_Postfix(Menu __instance)
	{
		Transform menuDialog = __instance.m_menuDialog;
		if (menuDialog == null || menuDialog.Find("MenuEntries/HC_ModOptions") != null)
		{
			return;
		}
		GameObject gameObject = menuDialog.Find("MenuEntries/Settings")?.gameObject;
		if (gameObject == null)
		{
			CompanionsPlugin.Log.LogWarning("[MenuPatches] Could not find MenuEntries/Settings — skipping Mod Options button");
			return;
		}
		GameObject gameObject2 = Object.Instantiate(gameObject, gameObject.transform.parent);
		gameObject2.name = "HC_ModOptions";
		int siblingIndex = gameObject.transform.GetSiblingIndex();
		gameObject2.transform.SetSiblingIndex(siblingIndex + 1);
		TMP_Text componentInChildren = gameObject2.GetComponentInChildren<TMP_Text>(includeInactive: true);
		if (componentInChildren != null)
		{
			string text = ModLocalization.Loc("hc_menu_mod_options");
			componentInChildren.text = ((text != null && !text.StartsWith("[")) ? text : "Mod Options");
		}
		Button component = gameObject2.GetComponent<Button>();
		if (component != null)
		{
			component.onClick.RemoveAllListeners();
			component.onClick.AddListener(delegate
			{
				__instance.Hide();
				if (ConfigPanel.Instance != null)
				{
					ConfigPanel.Instance.Show();
				}
			});
		}
		if (CompanionsPlugin.LogDetailed) CompanionsPlugin.Log.LogDebug("[MenuPatches] Injected 'Mod Options' button into pause menu");
	}

	[HarmonyPatch(typeof(Menu), "UpdateNavigation")]
	[HarmonyPostfix]
	private static void UpdateNavigation_Postfix(Menu __instance)
	{
		Transform menuDialog = __instance.m_menuDialog;
		if (menuDialog == null)
		{
			return;
		}
		Transform transform = menuDialog.Find("MenuEntries/HC_ModOptions");
		if (transform == null)
		{
			return;
		}
		Button component = transform.GetComponent<Button>();
		if (!(component == null))
		{
			Button button = menuDialog.Find("MenuEntries/Settings")?.GetComponent<Button>();
			Button button2 = menuDialog.Find("MenuEntries/Logout")?.GetComponent<Button>();
			if (!(button == null) && !(button2 == null))
			{
				Navigation navigation = button.navigation;
				Navigation navigation2 = component.navigation;
				Navigation navigation3 = button2.navigation;
				navigation2.mode = Navigation.Mode.Explicit;
				navigation2.selectOnUp = button;
				navigation2.selectOnDown = navigation.selectOnDown;
				component.navigation = navigation2;
				navigation.selectOnDown = component;
				button.navigation = navigation;
				_ = navigation.selectOnDown != null;
				navigation3.selectOnUp = component;
				button2.navigation = navigation3;
			}
		}
	}
}
