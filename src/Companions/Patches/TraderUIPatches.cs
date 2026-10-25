using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Companions;

public static class TraderUIPatches
{
	[HarmonyPatch]
	private static class BuildUI_Patch
	{
		private static bool Prepare()
		{
			return FindTraderMethod("BuildUI") != null;
		}

		private static MethodBase TargetMethod()
		{
			return FindTraderMethod("BuildUI");
		}

		private static void Postfix(object __instance)
		{
			try
			{
				CacheReflection();
				InjectCompanionTab(__instance);
			}
			catch (Exception arg)
			{
				CompanionsPlugin.Log.LogError($"[TraderUIPatches] BuildUI postfix error: {arg}");
			}
		}
	}

	[HarmonyPatch]
	private static class SwitchTab_Patch
	{
		private static bool Prepare()
		{
			return FindTraderMethod("SwitchTab") != null;
		}

		private static MethodBase TargetMethod()
		{
			return FindTraderMethod("SwitchTab");
		}

		private static bool Prefix(object __instance, int newTab)
		{
			if (CompanionTabIndex < 0)
			{
				return true;
			}
			CacheReflection();
			if (_activeTabField == null)
			{
				return true;
			}
			if (newTab == CompanionTabIndex)
			{
				int num = (int)_activeTabField.GetValue(__instance);
				if (num == CompanionTabIndex)
				{
					return false;
				}
				ActivateCompanionTab(__instance);
				return false;
			}
			if (_companionPanel?.Root != null)
			{
				_companionPanel.Root.SetActive(value: false);
			}
			return true;
		}
	}

	[HarmonyPatch]
	private static class RefreshTabHighlights_Patch
	{
		private static bool Prepare()
		{
			return FindTraderMethod("RefreshTabHighlights") != null;
		}

		private static MethodBase TargetMethod()
		{
			return FindTraderMethod("RefreshTabHighlights");
		}

		private static void Postfix(object __instance)
		{
			CacheReflection();
			if (!(_tabCompanions == null) && !(_activeTabField == null))
			{
				int num = (int)_activeTabField.GetValue(__instance);
				Button component = _tabCompanions.GetComponent<Button>();
				if (component != null)
				{
					component.interactable = true;
					component.transition = Selectable.Transition.None;
				}
				Image component2 = _tabCompanions.GetComponent<Image>();
				if (component2 != null)
				{
					component2.color = ((num == CompanionTabIndex) ? GoldColor : new Color(0.45f, 0.45f, 0.45f, 1f));
				}
			}
		}
	}

	[HarmonyPatch]
	private static class RefreshTabPanels_Patch
	{
		private static bool Prepare()
		{
			return FindTraderMethod("RefreshTabPanels") != null;
		}

		private static MethodBase TargetMethod()
		{
			return FindTraderMethod("RefreshTabPanels");
		}

		private static void Postfix(object __instance)
		{
			CacheReflection();
			if (!(_companionPanel?.Root == null) && !(_activeTabField == null))
			{
				int num = (int)_activeTabField.GetValue(__instance);
				if (num == CompanionTabIndex)
				{
					ActivateCompanionTab(__instance);
				}
				else
				{
					_companionPanel.Root.SetActive(value: false);
				}
			}
		}
	}

	[HarmonyPatch]
	private static class Update_Patch
	{
		private static bool Prepare()
		{
			return FindTraderMethod("Update") != null;
		}

		private static MethodBase TargetMethod()
		{
			return FindTraderMethod("Update");
		}

		private static void Prefix(object __instance, ref int __state)
		{
			CacheReflection();
			__state = ((_activeTabField != null) ? ((int)_activeTabField.GetValue(__instance)) : (-1));
		}

		private static void Postfix(object __instance, int __state)
		{
			if (CompanionTabIndex < 0 || _isVisibleField == null || _activeTabField == null || !(bool)_isVisibleField.GetValue(__instance))
			{
				return;
			}
			int num = (int)_activeTabField.GetValue(__instance);
			TMP_InputField tMP_InputField = _searchInputField?.GetValue(__instance) as TMP_InputField;
			if (tMP_InputField != null && tMP_InputField.isFocused)
			{
				return;
			}
			if (__state == CompanionTabIndex && num != CompanionTabIndex)
			{
				if (!Input.GetKeyDown(KeyCode.Q) && !ZInput.GetButtonDown("JoyTabLeft") && !Input.GetMouseButtonUp(0) && !Input.GetMouseButtonDown(0) && !ZInput.GetButtonDown("JoyTabRight"))
				{
					ActivateCompanionTab(__instance);
					num = CompanionTabIndex;
				}
			}
			else if (__state == CompanionTabIndex - 1)
			{
				num = (int)_activeTabField.GetValue(__instance);
				if (num == CompanionTabIndex - 1 && (Input.GetKeyDown(KeyCode.E) || ZInput.GetButtonDown("JoyTabRight")))
				{
					ActivateCompanionTab(__instance);
					num = CompanionTabIndex;
				}
			}
			num = (int)_activeTabField.GetValue(__instance);
			if (num == CompanionTabIndex)
			{
				if (_companionPanel?.Root != null && !_companionPanel.Root.activeSelf)
				{
					ActivateCompanionTab(__instance);
				}
				_companionPanel?.UpdatePerFrame();
			}
		}
	}

	private static Type _traderUIType;

	// 2.0.0 (patch audit): the type alone is not a good enough test. TraderOverhaul can rename or remove one of
	// these methods between releases, and a null TargetMethod makes Harmony throw - which used to abort the whole
	// PatchAll. Prepare returning false skips only this patch, and TargetMethod never returns null either.
	private static MethodBase FindTraderMethod(string name)
	{
		try
		{
			Type type = Type.GetType("TraderOverhaul.TraderUI, TraderOverhaul");
			if (type == null)
			{
				return null;
			}
			// Public | NonPublic | Instance: the mod only ever patched Instance|NonPublic, which threw
			// AmbiguousMatchException on a public overload (Update).
			return type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		}
		catch (Exception)
		{
			return null;
		}
	}

	private static FieldInfo _mainPanelField;

	private static FieldInfo _buttonTemplateField;

	private static FieldInfo _activeTabField;

	private static FieldInfo _panelWidthField;

	private static FieldInfo _tabBtnHeightField;

	private static FieldInfo _leftColumnField;

	private static FieldInfo _middleColumnField;

	private static FieldInfo _rightColumnField;

	private static FieldInfo _bankContentPanelField;

	private static FieldInfo _searchFilterField;

	private static FieldInfo _searchInputField;

	private static FieldInfo _activeCategoryFilterField;

	private static FieldInfo _joyCategoryFocusIndexField;

	private static FieldInfo _colTopInsetField;

	private static FieldInfo _bottomPadField;

	private static FieldInfo _valheimFontField;

	private static FieldInfo _isVisibleField;

	private static FieldInfo _craftBtnHeightField;

	private static MethodInfo _refreshTabHighlightsMethod;

	private static MethodInfo _updateCategoryFilterVisualsMethod;

	private static GameObject _tabCompanions;

	private static CompanionPanel _companionPanel;

	private static bool _reflectionCached;

	private static readonly Color GoldColor = new Color(0.83f, 0.64f, 0.31f, 1f);

	internal static int CompanionTabIndex { get; private set; } = -1;

	private static void CacheReflection()
	{
		if (_reflectionCached)
		{
			return;
		}
		_traderUIType = Type.GetType("TraderOverhaul.TraderUI, TraderOverhaul");
		if (_traderUIType == null)
		{
			CompanionsPlugin.Log.LogError("[TraderUIPatches] Could not find TraderUI type!");
			return;
		}
		BindingFlags bindingAttr = BindingFlags.Instance | BindingFlags.NonPublic;
		_mainPanelField = _traderUIType.GetField("_mainPanel", bindingAttr);
		_buttonTemplateField = _traderUIType.GetField("_buttonTemplate", bindingAttr);
		_activeTabField = _traderUIType.GetField("_activeTab", bindingAttr);
		_panelWidthField = _traderUIType.GetField("_panelWidth", bindingAttr);
		_tabBtnHeightField = _traderUIType.GetField("_tabBtnHeight", bindingAttr);
		_leftColumnField = _traderUIType.GetField("_leftColumn", bindingAttr);
		_middleColumnField = _traderUIType.GetField("_middleColumn", bindingAttr);
		_rightColumnField = _traderUIType.GetField("_rightColumn", bindingAttr);
		_bankContentPanelField = _traderUIType.GetField("_bankContentPanel", bindingAttr);
		_searchFilterField = _traderUIType.GetField("_searchFilter", bindingAttr);
		_searchInputField = _traderUIType.GetField("_searchInput", bindingAttr);
		_activeCategoryFilterField = _traderUIType.GetField("_activeCategoryFilter", bindingAttr);
		_joyCategoryFocusIndexField = _traderUIType.GetField("_joyCategoryFocusIndex", bindingAttr);
		_colTopInsetField = _traderUIType.GetField("_colTopInset", bindingAttr);
		_bottomPadField = _traderUIType.GetField("_bottomPad", bindingAttr);
		_valheimFontField = _traderUIType.GetField("_valheimFont", bindingAttr);
		_isVisibleField = _traderUIType.GetField("_isVisible", bindingAttr);
		_craftBtnHeightField = _traderUIType.GetField("_craftBtnHeight", bindingAttr);
		_refreshTabHighlightsMethod = _traderUIType.GetMethod("RefreshTabHighlights", bindingAttr);
		_updateCategoryFilterVisualsMethod = _traderUIType.GetMethod("UpdateCategoryFilterVisuals", bindingAttr);
		if (_mainPanelField == null)
		{
			CompanionsPlugin.Log.LogWarning("[TraderUIPatches] _mainPanel field not found.");
		}
		if (_buttonTemplateField == null)
		{
			CompanionsPlugin.Log.LogWarning("[TraderUIPatches] _buttonTemplate field not found.");
		}
		if (_activeTabField == null)
		{
			CompanionsPlugin.Log.LogWarning("[TraderUIPatches] _activeTab field not found.");
		}
		if (_panelWidthField == null)
		{
			CompanionsPlugin.Log.LogWarning("[TraderUIPatches] _panelWidth field not found.");
		}
		_reflectionCached = true;
		CompanionsPlugin.Log.LogInfo("[TraderUIPatches] Reflection cached successfully.");
	}

	private static void InjectCompanionTab(object traderUI)
	{
		if (_mainPanelField == null || _buttonTemplateField == null || _activeTabField == null || _panelWidthField == null || _tabBtnHeightField == null || _colTopInsetField == null || _bottomPadField == null || _valheimFontField == null)
		{
			CompanionsPlugin.Log.LogError("[TraderUIPatches] Cannot inject companion tab — critical reflection fields are missing.");
			return;
		}
		_companionPanel?.Teardown();
		_companionPanel = null;
		if (_tabCompanions != null)
		{
			UnityEngine.Object.Destroy(_tabCompanions);
			_tabCompanions = null;
		}
		CompanionTabIndex = -1;
		GameObject gameObject = (GameObject)_mainPanelField.GetValue(traderUI);
		GameObject gameObject2 = (GameObject)_buttonTemplateField.GetValue(traderUI);
		float num = (float)_panelWidthField.GetValue(traderUI);
		float y = (float)_tabBtnHeightField.GetValue(traderUI);
		float colTopInset = (float)_colTopInsetField.GetValue(traderUI);
		float bottomPad = (float)_bottomPadField.GetValue(traderUI);
		TMP_FontAsset font = _valheimFontField.GetValue(traderUI) as TMP_FontAsset;
		if (gameObject == null || gameObject2 == null)
		{
			return;
		}
		List<GameObject> list = new List<GameObject>();
		for (int i = 0; i < gameObject.transform.childCount; i++)
		{
			GameObject gameObject3 = gameObject.transform.GetChild(i).gameObject;
			if (gameObject3.name.StartsWith("Tab_") && gameObject3.GetComponent<Button>() != null)
			{
				list.Add(gameObject3);
			}
		}
		int num2 = list.Count + 1;
		CompanionTabIndex = list.Count;
		float num3 = num - 12f - 4f * (float)(num2 - 1);
		float num4 = num3 / (float)num2;
		float[] array = new float[num2];
		for (int j = 0; j < num2; j++)
		{
			array[j] = 6f + num4 / 2f + (float)j * (num4 + 4f);
		}
		for (int k = 0; k < list.Count; k++)
		{
			ResizeTab(list[k], array[k], num4);
		}
		_tabCompanions = UnityEngine.Object.Instantiate(gameObject2, gameObject.transform);
		_tabCompanions.name = "Tab_Companions";
		_tabCompanions.SetActive(value: true);
		Button component = _tabCompanions.GetComponent<Button>();
		if (component != null)
		{
			component.onClick.RemoveAllListeners();
			component.onClick.AddListener(delegate
			{
				ActivateCompanionTab(traderUI);
			});
			component.navigation = new Navigation
			{
				mode = Navigation.Mode.None
			};
		}
		TMP_Text componentInChildren = _tabCompanions.GetComponentInChildren<TMP_Text>(includeInactive: true);
		if (componentInChildren != null)
		{
			componentInChildren.text = ModLocalization.Loc("hc_ui_title_companions");
			componentInChildren.gameObject.SetActive(value: true);
		}
		for (int num5 = _tabCompanions.transform.childCount - 1; num5 >= 0; num5--)
		{
			Transform child = _tabCompanions.transform.GetChild(num5);
			if (!(componentInChildren != null) || (!(child.gameObject == componentInChildren.gameObject) && !componentInChildren.transform.IsChildOf(child)))
			{
				UnityEngine.Object.Destroy(child.gameObject);
			}
		}
		RectTransform component2 = _tabCompanions.GetComponent<RectTransform>();
		component2.anchorMin = new Vector2(0f, 1f);
		component2.anchorMax = new Vector2(0f, 1f);
		component2.pivot = new Vector2(0.5f, 1f);
		component2.sizeDelta = new Vector2(num4, y);
		component2.anchoredPosition = new Vector2(array[CompanionTabIndex], -6f);
		float buttonHeight = 30f;
		if (_craftBtnHeightField != null)
		{
			buttonHeight = (float)_craftBtnHeightField.GetValue(traderUI);
		}
		RectTransform rectTransform = _leftColumnField?.GetValue(traderUI) as RectTransform;
		RectTransform rectTransform2 = _middleColumnField?.GetValue(traderUI) as RectTransform;
		RectTransform rectTransform3 = _rightColumnField?.GetValue(traderUI) as RectTransform;
		float leftXL = rectTransform?.offsetMin.x ?? 6f;
		float leftXR = rectTransform?.offsetMax.x ?? 266f;
		float midXL = rectTransform2?.offsetMin.x ?? 270f;
		float midXR = rectTransform2?.offsetMax.x ?? 610f;
		float rightXL = rectTransform3?.offsetMin.x ?? 614f;
		float rightXR = rectTransform3?.offsetMax.x ?? 874f;
		RectTransform component3 = gameObject.GetComponent<RectTransform>();
		float panelHeight = ((component3 != null) ? component3.sizeDelta.y : 432f);
		_companionPanel = new CompanionPanel();
		_companionPanel.Build(gameObject.transform, colTopInset, bottomPad, font, gameObject2, buttonHeight, leftXL, leftXR, midXL, midXR, rightXL, rightXR, panelHeight);
		CompanionsPlugin.Log.LogInfo($"[TraderUIPatches] Companion tab injected at index {CompanionTabIndex} ({num2} total tabs).");
	}

	private static void ResizeTab(GameObject tab, float centerX, float width)
	{
		if (!(tab == null))
		{
			RectTransform component = tab.GetComponent<RectTransform>();
			if (!(component == null))
			{
				component.sizeDelta = new Vector2(width, component.sizeDelta.y);
				component.anchoredPosition = new Vector2(centerX, component.anchoredPosition.y);
			}
		}
	}

	private static void ActivateCompanionTab(object traderUI)
	{
		if (_activeTabField == null || _mainPanelField == null)
		{
			return;
		}
		if (_companionPanel?.Root == null)
		{
			CompanionsPlugin.Log.LogWarning("[TraderUIPatches] Companion panel missing during activation, attempting reinject.");
			InjectCompanionTab(traderUI);
			if (_companionPanel?.Root == null)
			{
				return;
			}
		}
		int num = (int)_activeTabField.GetValue(traderUI);
		bool flag = num != CompanionTabIndex;
		if (flag)
		{
			_activeTabField.SetValue(traderUI, CompanionTabIndex);
		}
		_searchFilterField?.SetValue(traderUI, "");
		TMP_InputField tMP_InputField = _searchInputField?.GetValue(traderUI) as TMP_InputField;
		if (tMP_InputField != null)
		{
			tMP_InputField.text = "";
		}
		_activeCategoryFilterField?.SetValue(traderUI, null);
		_joyCategoryFocusIndexField?.SetValue(traderUI, -1);
		_updateCategoryFilterVisualsMethod?.Invoke(traderUI, null);
		RefreshAllTabHighlights(traderUI);
		GameObject gameObject = (GameObject)_mainPanelField.GetValue(traderUI);
		string text = ((_companionPanel?.Root != null) ? _companionPanel.Root.name : "");
		for (int i = 0; i < gameObject.transform.childCount; i++)
		{
			GameObject gameObject2 = gameObject.transform.GetChild(i).gameObject;
			if (!gameObject2.name.StartsWith("Tab_") && (!(gameObject2.name == text) || text.Length <= 0))
			{
				gameObject2.SetActive(value: false);
			}
		}
		if (_companionPanel?.Root != null)
		{
			bool activeSelf = _companionPanel.Root.activeSelf;
			_companionPanel.Root.SetActive(value: true);
			if (flag || !activeSelf)
			{
				_companionPanel.Refresh();
			}
		}
		if (EventSystem.current != null)
		{
			EventSystem.current.SetSelectedGameObject(null);
		}
	}

	private static void RefreshAllTabHighlights(object traderUI)
	{
		_refreshTabHighlightsMethod?.Invoke(traderUI, null);
	}
}
