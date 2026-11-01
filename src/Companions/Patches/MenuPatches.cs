using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Companions;

internal static class MenuPatches
{
	private const string ButtonName = "HC_ModOptions";

	// The game's own "down from Settings" target, captured once per menu instance: after the first pass
	// Settings.selectOnDown is the Mod Options button itself, so re-reading it on the next Show would point
	// Mod Options at itself and the controller could not move down (audit 1.9.4 #14).
	private static Button _settingsButton;

	private static Selectable _settingsDown;

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Menu), "Show")]
	private static void MenuShow_Postfix(Menu __instance)
	{
		Transform menuDialog = __instance.m_menuDialog;
		if (menuDialog == null)
		{
			return;
		}
		if (menuDialog.Find("MenuEntries/HC_ModOptions") == null)
		{
			InjectButton(__instance, menuDialog);
		}
		// C4 (audit 1.9.4): Menu.Show() calls UpdateNavigation() BEFORE this postfix adds the button, so on the
		// first open the button kept the navigation it was cloned with (down went to Settings' own target and
		// Settings still pointed past it) - the wiring only came back on the next open, or when the Save button
		// flipped. Wire it here, in the same Show. The _settingsButton/_settingsDown cache still keeps the button
		// from pointing at itself (#14).
		WireNavigation(__instance);
	}

	[HarmonyPatch(typeof(Menu), "UpdateNavigation")]
	[HarmonyPostfix]
	private static void UpdateNavigation_Postfix(Menu __instance)
	{
		WireNavigation(__instance);
	}

	/// <summary>
	/// Clones the game's "Settings" entry as "Mod Options" (right below it) and points it at the config panel.
	/// Runs once per menu: the button survives the Show/Hide of the dialog.
	/// </summary>
	private static void InjectButton(Menu menu, Transform menuDialog)
	{
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
				menu.Hide();
				if (ConfigPanel.Instance != null)
				{
					ConfigPanel.Instance.Show();
				}
			});
		}
		if (CompanionsPlugin.LogDetailed) CompanionsPlugin.Log.LogDebug("[MenuPatches] Injected 'Mod Options' button into pause menu");
	}

	/// <summary>
	/// Chains the controller navigation of the pause menu: Continue/Save -> ... -> Settings -> Mod Options -> the
	/// game's own "down from Settings" target (Logout), and back up the same way. Safe to call any number of
	/// times - it does nothing while the button is not in the menu.
	/// </summary>
	private static void WireNavigation(Menu menu)
	{
		Transform menuDialog = menu.m_menuDialog;
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
				if (button != _settingsButton || _settingsDown == null)
				{
					_settingsButton = button;
					_settingsDown = navigation.selectOnDown;
				}
				Navigation navigation2 = component.navigation;
				Navigation navigation3 = button2.navigation;
				navigation2.mode = Navigation.Mode.Explicit;
				navigation2.selectOnUp = button;
				navigation2.selectOnDown = _settingsDown;
				component.navigation = navigation2;
				navigation.selectOnDown = component;
				button.navigation = navigation;
				navigation3.selectOnUp = component;
				button2.navigation = navigation3;
			}
		}
	}
}
