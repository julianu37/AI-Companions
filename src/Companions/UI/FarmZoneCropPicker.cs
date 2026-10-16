using System;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Companions;

public class FarmZoneCropPicker : MonoBehaviour
{
	private struct CropEntry
	{
		public string SeedPrefabName;

		public string DisplayName;

		public Sprite Icon;
	}

	[HarmonyPatch(typeof(Hud), "InRadial")]
	private static class HudInRadial_Patch
	{
		private static void Postfix(ref bool __result)
		{
			if (!__result && IsOpen)
			{
				__result = true;
			}
		}
	}

	private static FarmZoneCropPicker _instance;

	private Action<string> _onSelect;

	private Action _onCancel;

	private Canvas _canvas;

	private GameObject _root;

	private TMP_FontAsset _font;

	private static readonly Color BgColor = new Color(0.08f, 0.07f, 0.06f, 0.94f);

	private static readonly Color ButtonColor = new Color(0.15f, 0.13f, 0.1f, 0.9f);

	private static readonly Color ButtonHover = new Color(0.25f, 0.22f, 0.16f, 0.9f);

	private static readonly Color LabelColor = new Color(1f, 0.9f, 0.5f, 1f);

	private static readonly Color TextColor = new Color(0.85f, 0.8f, 0.65f, 1f);

	private const float ButtonSize = 80f;

	private const float Spacing = 6f;

	private const int Columns = 5;

	internal static bool IsOpen
	{
		get
		{
			if (_instance != null && _instance._root != null)
			{
				return _instance._root.activeSelf;
			}
			return false;
		}
	}

	internal static void Show(FarmZone zone, Action<string> onSelect, Action onCancel)
	{
		if (_instance == null)
		{
			GameObject gameObject = new GameObject("HC_FarmZoneCropPicker");
			UnityEngine.Object.DontDestroyOnLoad(gameObject);
			_instance = gameObject.AddComponent<FarmZoneCropPicker>();
		}
		_instance._onSelect = onSelect;
		_instance._onCancel = onCancel;
		_instance.Build();
	}

	private void Build()
	{
		if (_root != null)
		{
			UnityEngine.Object.Destroy(_root);
		}
		if (_canvas != null)
		{
			UnityEngine.Object.Destroy(_canvas.gameObject);
		}
		_font = ResolveFont();
		GameObject gameObject = new GameObject("CropPickerCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
		gameObject.transform.SetParent(base.transform, worldPositionStays: false);
		_canvas = gameObject.GetComponent<Canvas>();
		_canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		_canvas.sortingOrder = ModUiLayers.FarmZoneCropPicker;
		CanvasScaler component = gameObject.GetComponent<CanvasScaler>();
		component.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		component.referenceResolution = new Vector2(1920f, 1080f);
		FarmController.BuildSeedMappingPublic();
		List<CropEntry> list = new List<CropEntry>();
		if (FarmController.s_seedToPlant != null)
		{
			foreach (KeyValuePair<string, FarmController.SeedPlantInfo> item2 in FarmController.s_seedToPlant)
			{
				if (!IsSeedMultiplicationEntry(item2.Key, item2.Value))
				{
					CropEntry item = new CropEntry
					{
						SeedPrefabName = item2.Key,
						DisplayName = GetCropDisplayName(item2.Value),
						Icon = GetCropIcon(item2.Key)
					};
					list.Add(item);
				}
			}
		}
		list.Sort((CropEntry a, CropEntry b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));
		int num = list.Count + 1;
		int num2 = Mathf.CeilToInt((float)num / 5f);
		float num3 = 436f;
		float num4 = (float)num2 * 86f + 6f;
		float x = num3 + 20f;
		float y = num4 + 60f;
		_root = new GameObject("CropPickerRoot", typeof(RectTransform), typeof(Image));
		_root.transform.SetParent(gameObject.transform, worldPositionStays: false);
		RectTransform component2 = _root.GetComponent<RectTransform>();
		component2.anchorMin = new Vector2(0.5f, 0.5f);
		component2.anchorMax = new Vector2(0.5f, 0.5f);
		component2.pivot = new Vector2(0.5f, 0.5f);
		component2.sizeDelta = new Vector2(x, y);
		_root.GetComponent<Image>().color = BgColor;
		GameObject gameObject2 = new GameObject("Title", typeof(RectTransform));
		gameObject2.transform.SetParent(_root.transform, worldPositionStays: false);
		TextMeshProUGUI textMeshProUGUI = gameObject2.AddComponent<TextMeshProUGUI>();
		if (_font != null)
		{
			textMeshProUGUI.font = _font;
		}
		textMeshProUGUI.text = ModLocalization.Loc("hc_farmzone_title");
		textMeshProUGUI.fontSize = 18f;
		textMeshProUGUI.color = LabelColor;
		textMeshProUGUI.fontStyle = FontStyles.Bold;
		textMeshProUGUI.alignment = TextAlignmentOptions.Center;
		textMeshProUGUI.raycastTarget = false;
		RectTransform component3 = gameObject2.GetComponent<RectTransform>();
		component3.anchorMin = new Vector2(0f, 1f);
		component3.anchorMax = new Vector2(1f, 1f);
		component3.pivot = new Vector2(0.5f, 1f);
		component3.sizeDelta = new Vector2(0f, 36f);
		component3.anchoredPosition = new Vector2(0f, -4f);
		BuildCloseButton(_root.transform);
		GameObject gameObject3 = new GameObject("Content", typeof(RectTransform));
		gameObject3.transform.SetParent(_root.transform, worldPositionStays: false);
		RectTransform component4 = gameObject3.GetComponent<RectTransform>();
		component4.anchorMin = new Vector2(0.5f, 1f);
		component4.anchorMax = new Vector2(0.5f, 1f);
		component4.pivot = new Vector2(0.5f, 1f);
		component4.sizeDelta = new Vector2(num3, num4);
		component4.anchoredPosition = new Vector2(0f, -44f);
		BuildCropButton(gameObject3.transform, 0, "", ModLocalization.Loc("hc_farmzone_any"), null);
		for (int num5 = 0; num5 < list.Count; num5++)
		{
			CropEntry cropEntry = list[num5];
			BuildCropButton(gameObject3.transform, num5 + 1, cropEntry.SeedPrefabName, cropEntry.DisplayName, cropEntry.Icon);
		}
		Cursor.lockState = CursorLockMode.None;
		Cursor.visible = true;
	}

	private void OnDestroy()
	{
		if (_instance == this)
		{
			_instance = null;
		}
	}

	private void Update()
	{
		if (!(_root == null) && _root.activeSelf)
		{
			Cursor.lockState = CursorLockMode.None;
			Cursor.visible = true;
			if (Input.GetKeyDown(KeyCode.Escape) || ZInput.GetButtonDown("JoyButtonB"))
			{
				Cancel();
			}
		}
	}

	private void Cancel()
	{
		Action onCancel = _onCancel;
		CleanupUI();
		onCancel?.Invoke();
	}

	private static TMP_FontAsset ResolveFont()
	{
		if (InventoryGui.instance != null)
		{
			TextMeshProUGUI[] componentsInChildren = InventoryGui.instance.GetComponentsInChildren<TextMeshProUGUI>(includeInactive: true);
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				if (componentsInChildren[i] != null && componentsInChildren[i].font != null && componentsInChildren[i].font.name.IndexOf("LiberationSans", StringComparison.OrdinalIgnoreCase) < 0)
				{
					return componentsInChildren[i].font;
				}
			}
		}
		return TMP_Settings.defaultFontAsset;
	}

	private static bool IsSeedMultiplicationEntry(string seedName, FarmController.SeedPlantInfo info)
	{
		if (FarmController.s_cropOutputNames == null || !FarmController.s_cropOutputNames.Contains(seedName))
		{
			return false;
		}
		Plant plant = info.PlantPrefab?.GetComponent<Plant>();
		if (plant?.m_grownPrefabs != null)
		{
			GameObject[] grownPrefabs = plant.m_grownPrefabs;
			foreach (GameObject gameObject in grownPrefabs)
			{
				if (!(gameObject == null))
				{
					Pickable component = gameObject.GetComponent<Pickable>();
					if (component?.m_itemPrefab != null && component.m_itemPrefab.name == seedName)
					{
						return false;
					}
				}
			}
		}
		if (ZNetScene.instance != null)
		{
			GameObject prefab = ZNetScene.instance.GetPrefab(seedName);
			if (prefab != null)
			{
				ItemDrop component2 = prefab.GetComponent<ItemDrop>();
				if (component2?.m_itemData?.m_shared != null)
				{
					ItemDrop.ItemData.SharedData shared = component2.m_itemData.m_shared;
					if (shared.m_food > 0f || shared.m_foodStamina > 0f)
					{
						return true;
					}
				}
			}
		}
		return false;
	}

	private static string GetCropDisplayName(FarmController.SeedPlantInfo info)
	{
		if (info.PlantPrefab == null)
		{
			return info.SeedPrefabName;
		}
		Piece component = info.PlantPrefab.GetComponent<Piece>();
		if (component != null && !string.IsNullOrEmpty(component.m_name))
		{
			return Localization.instance.Localize(component.m_name);
		}
		return info.SeedPrefabName;
	}

	private static Sprite GetCropIcon(string seedPrefabName)
	{
		if (ZNetScene.instance == null)
		{
			return null;
		}
		GameObject prefab = ZNetScene.instance.GetPrefab(seedPrefabName);
		if (prefab == null)
		{
			return null;
		}
		ItemDrop component = prefab.GetComponent<ItemDrop>();
		if (component?.m_itemData == null)
		{
			return null;
		}
		return component.m_itemData.GetIcon();
	}

	private void BuildCloseButton(Transform parent)
	{
		GameObject gameObject = new GameObject("CloseBtn", typeof(RectTransform), typeof(Image), typeof(Button));
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		RectTransform component = gameObject.GetComponent<RectTransform>();
		component.anchorMin = new Vector2(1f, 1f);
		component.anchorMax = new Vector2(1f, 1f);
		component.pivot = new Vector2(1f, 1f);
		component.sizeDelta = new Vector2(28f, 28f);
		component.anchoredPosition = new Vector2(-4f, -4f);
		gameObject.GetComponent<Image>().color = new Color(0.3f, 0.15f, 0.1f, 0.7f);
		gameObject.GetComponent<Button>().onClick.AddListener(Cancel);
		GameObject gameObject2 = new GameObject("X", typeof(RectTransform));
		gameObject2.transform.SetParent(gameObject.transform, worldPositionStays: false);
		TextMeshProUGUI textMeshProUGUI = gameObject2.AddComponent<TextMeshProUGUI>();
		if (_font != null)
		{
			textMeshProUGUI.font = _font;
		}
		textMeshProUGUI.text = "X";
		textMeshProUGUI.fontSize = 14f;
		textMeshProUGUI.color = Color.white;
		textMeshProUGUI.alignment = TextAlignmentOptions.Center;
		textMeshProUGUI.raycastTarget = false;
		RectTransform component2 = gameObject2.GetComponent<RectTransform>();
		component2.anchorMin = Vector2.zero;
		component2.anchorMax = Vector2.one;
		component2.offsetMin = Vector2.zero;
		component2.offsetMax = Vector2.zero;
	}

	private void BuildCropButton(Transform parent, int index, string seedName, string displayName, Sprite icon)
	{
		int num = index % 5;
		int num2 = index / 5;
		float x = 6f + (float)num * 86f;
		float y = 0f - (6f + (float)num2 * 86f);
		GameObject gameObject = new GameObject("Crop_" + seedName, typeof(RectTransform), typeof(Image), typeof(Button));
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		RectTransform component = gameObject.GetComponent<RectTransform>();
		component.anchorMin = new Vector2(0f, 1f);
		component.anchorMax = new Vector2(0f, 1f);
		component.pivot = new Vector2(0f, 1f);
		component.sizeDelta = new Vector2(80f, 80f);
		component.anchoredPosition = new Vector2(x, y);
		Image component2 = gameObject.GetComponent<Image>();
		component2.color = ButtonColor;
		Button component3 = gameObject.GetComponent<Button>();
		ColorBlock colors = component3.colors;
		colors.normalColor = ButtonColor;
		colors.highlightedColor = ButtonHover;
		colors.pressedColor = ButtonHover;
		colors.selectedColor = ButtonColor;
		component3.colors = colors;
		string capturedSeed = seedName;
		component3.onClick.AddListener(delegate
		{
			SelectCrop(capturedSeed);
		});
		if (icon != null)
		{
			GameObject gameObject2 = new GameObject("Icon", typeof(RectTransform), typeof(Image));
			gameObject2.transform.SetParent(gameObject.transform, worldPositionStays: false);
			RectTransform component4 = gameObject2.GetComponent<RectTransform>();
			component4.anchorMin = new Vector2(0.5f, 0.5f);
			component4.anchorMax = new Vector2(0.5f, 0.5f);
			component4.pivot = new Vector2(0.5f, 0.5f);
			component4.sizeDelta = new Vector2(48f, 48f);
			component4.anchoredPosition = new Vector2(0f, 6f);
			Image component5 = gameObject2.GetComponent<Image>();
			component5.sprite = icon;
			component5.raycastTarget = false;
		}
		GameObject gameObject3 = new GameObject("Label", typeof(RectTransform));
		gameObject3.transform.SetParent(gameObject.transform, worldPositionStays: false);
		TextMeshProUGUI textMeshProUGUI = gameObject3.AddComponent<TextMeshProUGUI>();
		if (_font != null)
		{
			textMeshProUGUI.font = _font;
		}
		textMeshProUGUI.text = displayName;
		textMeshProUGUI.fontSize = 10f;
		textMeshProUGUI.color = TextColor;
		textMeshProUGUI.alignment = TextAlignmentOptions.Bottom;
		textMeshProUGUI.raycastTarget = false;
		textMeshProUGUI.textWrappingMode = TextWrappingModes.Normal;
		textMeshProUGUI.overflowMode = TextOverflowModes.Truncate;
		RectTransform component6 = gameObject3.GetComponent<RectTransform>();
		component6.anchorMin = new Vector2(0f, 0f);
		component6.anchorMax = new Vector2(1f, 0.35f);
		component6.offsetMin = new Vector2(2f, 2f);
		component6.offsetMax = new Vector2(-2f, 0f);
	}

	private void SelectCrop(string seedPrefabName)
	{
		Action<string> onSelect = _onSelect;
		CleanupUI();
		onSelect?.Invoke(seedPrefabName);
	}

	private void CleanupUI()
	{
		_onSelect = null;
		_onCancel = null;
		if (_root != null)
		{
			UnityEngine.Object.Destroy(_root);
			_root = null;
		}
		if (_canvas != null)
		{
			UnityEngine.Object.Destroy(_canvas.gameObject);
			_canvas = null;
		}
	}
}
