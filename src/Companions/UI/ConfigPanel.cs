using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Companions;

public class ConfigPanel : MonoBehaviour
{
	private bool _visible;

	private Canvas _canvas;

	private GameObject _root;

	private GameObject _backdrop;

	private string _activeCategory;

	private int _activeCategoryIndex;

	private readonly Dictionary<string, Button> _tabButtons = new Dictionary<string, Button>();

	private ScrollRect _scrollRect;

	private RectTransform _contentRT;

	private ScrollRect _tabScrollRect;

	private RectTransform _tabContentRT;

	private TMP_FontAsset _font;

	private readonly List<GameObject> _entryRows = new List<GameObject>();

	private readonly List<ConfigEntryBase> _entryData = new List<ConfigEntryBase>();

	private readonly List<Slider> _entrySliders = new List<Slider>();

	private readonly List<Action> _entryResetActions = new List<Action>();

	private int _selectedRowIndex = -1;

	private float _dpadRepeatTimer;

	private float _dpadHRepeatTimer;

	private const float DpadInitialDelay = 0.35f;

	private const float DpadRepeatRate = 0.08f;

	private const float SliderStep = 0.02f;

	private static readonly Color GoldColor = new Color(0.83f, 0.64f, 0.31f, 1f);

	private static readonly Color GoldTextColor = new Color(0.83f, 0.52f, 0.18f, 1f);

	private static readonly Color LabelText = new Color(1f, 0.9f, 0.5f, 1f);

	private static readonly Color TabInactive = new Color(0.45f, 0.45f, 0.45f, 1f);

	private static readonly Color ColBg = new Color(0f, 0f, 0f, 0.75f);

	private static readonly Color RowBg = new Color(0.08f, 0.08f, 0.1f, 0.6f);

	private static readonly Color RowBgSelected = new Color(0.18f, 0.15f, 0.08f, 0.8f);

	private static readonly Color DefaultBtnColor = new Color(0.35f, 0.35f, 0.38f, 1f);

	private const float PanelW = 700f;

	private const float PanelH = 500f;

	private const float TabW = 150f;

	private const float Pad = 8f;

	private const float RowH = 30f;

	private const float RowGap = 2f;

	private const float TitleH = 32f;

	/// <summary>
	/// Sorting order of the panel's own canvas, from <see cref="ModUiLayers"/> - the one place where the whole band
	/// of the mod's UI is documented. It used to be <b>31</b> and then <b>33</b>, and the invisible band that ate
	/// the clicks of the controls (the report: clicking the ON/OFF of a row did nothing until the row was scrolled
	/// out of the band) survived both, because the culprit is the game's play HUD at <b>400</b>: any value below 400
	/// loses the raycast to it, tie or not.
	/// </summary>
	private const int PanelSortingOrder = ModUiLayers.ConfigPanel;

	private static Sprite _panelBgSprite;

	private static Sprite _catBgSprite;

	private static Sprite _sliderBgSprite;

	private static Sprite _btnBgSprite;

	/// <summary>The raycast audit runs once per session (the first open), never per frame.</summary>
	private static bool _raycastAudited;

	public static ConfigPanel Instance { get; private set; }

	public bool IsVisible => _visible;

	public static void Create()
	{
		if (!(Instance != null))
		{
			GameObject gameObject = new GameObject("HC_ConfigPanel");
			UnityEngine.Object.DontDestroyOnLoad(gameObject);
			Instance = gameObject.AddComponent<ConfigPanel>();
		}
	}

	public void Show()
	{
		if (_root == null)
		{
			Build();
		}
		if (IsBrokenTmpFont(_font))
		{
			TMP_FontAsset tMP_FontAsset = ResolveFont();
			if (tMP_FontAsset != null && !IsBrokenTmpFont(tMP_FontAsset))
			{
				_font = tMP_FontAsset;
				TextMeshProUGUI[] componentsInChildren = _root.GetComponentsInChildren<TextMeshProUGUI>(includeInactive: true);
				for (int i = 0; i < componentsInChildren.Length; i++)
				{
					if (componentsInChildren[i] != null)
					{
						componentsInChildren[i].font = _font;
					}
				}
			}
		}
		_backdrop.SetActive(value: true);
		_root.SetActive(value: true);
		_visible = true;
		_selectedRowIndex = -1;
		Cursor.lockState = CursorLockMode.None;
		Cursor.visible = true;
		if (EventSystem.current == null)
		{
			GameObject gameObject = new GameObject("HC_EventSystem");
			gameObject.AddComponent<EventSystem>();
			gameObject.AddComponent<StandaloneInputModule>();
			UnityEngine.Object.DontDestroyOnLoad(gameObject);
		}
		LogRaycastAudit();
	}

	/// <summary>
	/// One shot per session, on the first open: the raycast audit of the panel, i.e. the answer to "who is in front
	/// of the controls?" - the only question that matters when a click is swallowed again. The audit itself lives in
	/// <see cref="ModUiAudit"/> (shared with the AI panel, which needs the same answer about the same band of
	/// layers); here only the geometry of <b>this</b> panel is given to it. It runs once, never per frame, and never
	/// throws into <see cref="Show"/>.
	/// </summary>
	private void LogRaycastAudit()
	{
		if (_raycastAudited)
		{
			return;
		}
		_raycastAudited = true;
		ModUiAudit.Log("F8 raycast audit", PanelSortingOrder, _canvas, _root != null ? _root.GetComponent<RectTransform>() : null,
			ProbePoints, _backdrop, this);
	}

	/// <summary>The probed points (panel-local; the panel is 700x500 with a centre pivot): the middle of the column of
	/// toggles / buttons / sliders, at eleven heights, so a band anywhere over the controls is caught.</summary>
	private static readonly Vector2[] ProbePoints = new Vector2[11]
	{
		new Vector2(196f, 230f),
		new Vector2(196f, 184f),
		new Vector2(196f, 138f),
		new Vector2(196f, 92f),
		new Vector2(196f, 46f),
		new Vector2(196f, 0f),
		new Vector2(196f, -46f),
		new Vector2(196f, -92f),
		new Vector2(196f, -138f),
		new Vector2(196f, -184f),
		new Vector2(196f, -230f)
	};

	private static Sprite GetPanelBgSprite()
	{
		if (_panelBgSprite != null)
		{
			return _panelBgSprite;
		}
		Texture2D texture2D = TextureLoader.LoadUITexture("PanelBackground");
		if (texture2D == null)
		{
			return null;
		}
		_panelBgSprite = Sprite.Create(texture2D, new Rect(0f, 0f, texture2D.width, texture2D.height), new Vector2(0.5f, 0.5f), 100f);
		return _panelBgSprite;
	}

	private static Sprite GetCatBgSprite()
	{
		if (_catBgSprite != null)
		{
			return _catBgSprite;
		}
		Texture2D texture2D = TextureLoader.LoadUITexture("CategoryBackground");
		if (texture2D == null)
		{
			return null;
		}
		_catBgSprite = Sprite.Create(texture2D, new Rect(0f, 0f, texture2D.width, texture2D.height), new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect, new Vector4(12f, 12f, 12f, 12f));
		return _catBgSprite;
	}

	private static Sprite GetSliderBgSprite()
	{
		if (_sliderBgSprite != null)
		{
			return _sliderBgSprite;
		}
		Texture2D texture2D = TextureLoader.LoadUITexture("SliderBackground");
		if (texture2D == null)
		{
			return null;
		}
		_sliderBgSprite = Sprite.Create(texture2D, new Rect(0f, 0f, texture2D.width, texture2D.height), new Vector2(0.5f, 0.5f), 100f);
		return _sliderBgSprite;
	}

	private static Sprite GetBtnBgSprite()
	{
		if (_btnBgSprite != null)
		{
			return _btnBgSprite;
		}
		Texture2D texture2D = TextureLoader.LoadUITexture("ButtonBackground");
		if (texture2D == null)
		{
			return null;
		}
		_btnBgSprite = Sprite.Create(texture2D, new Rect(0f, 0f, texture2D.width, texture2D.height), new Vector2(0.5f, 0.5f), 100f);
		return _btnBgSprite;
	}

	private void Build()
	{
		_canvas = base.gameObject.AddComponent<Canvas>();
		_canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		_canvas.sortingOrder = PanelSortingOrder;
		CanvasScaler canvasScaler = base.gameObject.AddComponent<CanvasScaler>();
		canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
		base.gameObject.AddComponent<GraphicRaycaster>();
		_font = ResolveFont();
		_backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
		_backdrop.transform.SetParent(base.transform, worldPositionStays: false);
		RectTransform component = _backdrop.GetComponent<RectTransform>();
		component.anchorMin = Vector2.zero;
		component.anchorMax = Vector2.one;
		component.offsetMin = Vector2.zero;
		component.offsetMax = Vector2.zero;
		// The backdrop is the modal click catcher: it stays a raycast target on purpose, so a click outside the panel
		// hits this (and does nothing) instead of the game's UI behind it. It is a sibling created BEFORE the panel
		// root, so its render depth - and therefore its raycast priority - is strictly lower than every control.
		_backdrop.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.85f);
		_root = new GameObject("ConfigRoot", typeof(RectTransform), typeof(Image));
		_root.transform.SetParent(base.transform, worldPositionStays: false);
		RectTransform component2 = _root.GetComponent<RectTransform>();
		component2.anchorMin = new Vector2(0.5f, 0.5f);
		component2.anchorMax = new Vector2(0.5f, 0.5f);
		component2.pivot = new Vector2(0.5f, 0.5f);
		component2.sizeDelta = new Vector2(700f, 500f);
		Image component3 = _root.GetComponent<Image>();
		Sprite panelBgSprite = GetPanelBgSprite();
		if (panelBgSprite != null)
		{
			component3.sprite = panelBgSprite;
			component3.type = Image.Type.Simple;
			component3.color = Color.white;
		}
		else
		{
			component3.color = new Color(0.05f, 0.05f, 0.08f, 0.95f);
		}
		// The panel's own background is decorative: it is the parent of every control, so it can never be in front of
		// one, and a click on the padding is better left to the backdrop (the modal catcher) than to die here.
		component3.raycastTarget = false;
		BuildTitle(_root.transform);
		BuildCloseButton(_root.transform);
		BuildTabColumn(_root.transform);
		BuildContentArea(_root.transform);
		if (ModConfig.CategoryOrder.Count > 0)
		{
			SelectCategory(ModConfig.CategoryOrder[0]);
		}
		_backdrop.SetActive(value: false);
		_root.SetActive(value: false);
	}

	private static bool IsBrokenTmpFont(TMP_FontAsset font)
	{
		if (!(font == null))
		{
			return font.name.IndexOf("LiberationSans", StringComparison.OrdinalIgnoreCase) >= 0;
		}
		return true;
	}

	private static TMP_FontAsset ResolveFont()
	{
		if (InventoryGui.instance != null)
		{
			TextMeshProUGUI[] componentsInChildren = InventoryGui.instance.GetComponentsInChildren<TextMeshProUGUI>(includeInactive: true);
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				if (componentsInChildren[i] != null && !IsBrokenTmpFont(componentsInChildren[i].font))
				{
					return componentsInChildren[i].font;
				}
			}
		}
		if (Hud.instance != null)
		{
			TextMeshProUGUI[] componentsInChildren2 = Hud.instance.GetComponentsInChildren<TextMeshProUGUI>(includeInactive: true);
			for (int j = 0; j < componentsInChildren2.Length; j++)
			{
				if (componentsInChildren2[j] != null && !IsBrokenTmpFont(componentsInChildren2[j].font))
				{
					return componentsInChildren2[j].font;
				}
			}
		}
		if (!IsBrokenTmpFont(TMP_Settings.defaultFontAsset))
		{
			return TMP_Settings.defaultFontAsset;
		}
		TMP_FontAsset[] array = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
		TMP_FontAsset tMP_FontAsset = null;
		foreach (TMP_FontAsset tMP_FontAsset2 in array)
		{
			if (!IsBrokenTmpFont(tMP_FontAsset2))
			{
				string text = tMP_FontAsset2.name.ToLowerInvariant();
				if (text.Contains("averia") || text.Contains("norse") || text.Contains("valheim"))
				{
					return tMP_FontAsset2;
				}
				if (tMP_FontAsset == null)
				{
					tMP_FontAsset = tMP_FontAsset2;
				}
			}
		}
		return tMP_FontAsset;
	}

	public void Hide()
	{
		_visible = false;
		_selectedRowIndex = -1;
		if (_root != null)
		{
			_root.SetActive(value: false);
		}
		if (_backdrop != null)
		{
			_backdrop.SetActive(value: false);
		}
	}

	private void Awake()
	{
		if (Instance != null && Instance != this)
		{
			UnityEngine.Object.Destroy(base.gameObject);
		}
		else
		{
			Instance = this;
		}
	}

	private void Update()
	{
		if (Input.GetKeyDown(ModConfig.ConfigPanelKey.Value))
		{
			if (_visible)
			{
				Hide();
			}
			else if (CompanionAiPanel.Instance == null || !CompanionAiPanel.Instance.IsVisible)
			{
				// Fase 3.7: never on top of the AI panel (the AI panel refuses to open while this one is visible)
				Show();
			}
		}
		if (_visible)
		{
			if (Input.GetKeyDown(KeyCode.Escape) || ZInput.GetButtonDown("JoyBack") || ZInput.GetButtonDown("JoyButtonB"))
			{
				Hide();
				return;
			}
			Cursor.lockState = CursorLockMode.None;
			Cursor.visible = true;
			UpdateControllerInput();
		}
	}

	private void UpdateControllerInput()
	{
		if (!ZInput.IsGamepadActive())
		{
			return;
		}
		float unscaledDeltaTime = Time.unscaledDeltaTime;
		if (ZInput.GetButtonDown("JoyTabLeft"))
		{
			int num = _activeCategoryIndex - 1;
			if (num < 0)
			{
				num = ModConfig.CategoryOrder.Count - 1;
			}
			SelectCategory(ModConfig.CategoryOrder[num]);
		}
		if (ZInput.GetButtonDown("JoyTabRight"))
		{
			int num2 = _activeCategoryIndex + 1;
			if (num2 >= ModConfig.CategoryOrder.Count)
			{
				num2 = 0;
			}
			SelectCategory(ModConfig.CategoryOrder[num2]);
		}
		float num3 = MHR_Stick.LY();
		bool buttonDown = ZInput.GetButtonDown("JoyDPadUp");
		bool buttonDown2 = ZInput.GetButtonDown("JoyDPadDown");
		bool flag = buttonDown || num3 > 0.5f;
		bool flag2 = buttonDown2 || num3 < -0.5f;
		if (flag || flag2)
		{
			if (_dpadRepeatTimer <= 0f || buttonDown || buttonDown2)
			{
				if (_dpadRepeatTimer <= 0f)
				{
					int num4 = ((!flag) ? 1 : (-1));
					int num5 = _selectedRowIndex + num4;
					if (num5 < 0)
					{
						num5 = _entryRows.Count - 1;
					}
					if (num5 >= _entryRows.Count)
					{
						num5 = 0;
					}
					SetSelectedRow(num5);
					_dpadRepeatTimer = ((_selectedRowIndex < 0) ? 0.35f : 0.35f);
				}
				else if (buttonDown || buttonDown2)
				{
					int num6 = ((!flag) ? 1 : (-1));
					int num7 = _selectedRowIndex + num6;
					if (num7 < 0)
					{
						num7 = _entryRows.Count - 1;
					}
					if (num7 >= _entryRows.Count)
					{
						num7 = 0;
					}
					SetSelectedRow(num7);
					_dpadRepeatTimer = 0.35f;
				}
			}
			_dpadRepeatTimer -= unscaledDeltaTime;
		}
		else
		{
			_dpadRepeatTimer = 0f;
		}
		if (_selectedRowIndex >= 0 && _selectedRowIndex < _entryData.Count)
		{
			float num8 = MHR_Stick.LX();
			bool buttonDown3 = ZInput.GetButtonDown("JoyDPadLeft");
			bool buttonDown4 = ZInput.GetButtonDown("JoyDPadRight");
			bool flag3 = buttonDown3 || num8 < -0.5f;
			bool flag4 = buttonDown4 || num8 > 0.5f;
			if (flag3 || flag4)
			{
				if ((_dpadHRepeatTimer <= 0f || buttonDown3 || buttonDown4) && (_dpadHRepeatTimer <= 0f || buttonDown3 || buttonDown4))
				{
					AdjustSelectedValue(flag4 ? 1 : (-1));
					_dpadHRepeatTimer = ((buttonDown3 || buttonDown4) ? 0.35f : 0.08f);
				}
				_dpadHRepeatTimer -= unscaledDeltaTime;
			}
			else
			{
				_dpadHRepeatTimer = 0f;
			}
			if (ZInput.GetButtonDown("JoyButtonA"))
			{
				ConfigEntryBase configEntryBase = _entryData[_selectedRowIndex];
				if (configEntryBase.SettingType == typeof(bool))
				{
					AdjustSelectedValue(1);
				}
			}
			if (ZInput.GetButtonDown("JoyButtonY") && _selectedRowIndex < _entryResetActions.Count)
			{
				_entryResetActions[_selectedRowIndex]?.Invoke();
			}
		}
		float num9 = MHR_Stick.RY();
		if (Mathf.Abs(num9) > 0.1f && _scrollRect != null)
		{
			_scrollRect.verticalNormalizedPosition = Mathf.Clamp01(_scrollRect.verticalNormalizedPosition + num9 * unscaledDeltaTime * 2f);
		}
	}

	private void OnDestroy()
	{
		if (Instance == this)
		{
			Instance = null;
		}
		DestroySprite(ref _panelBgSprite);
		DestroySprite(ref _catBgSprite);
		DestroySprite(ref _sliderBgSprite);
		DestroySprite(ref _btnBgSprite);
	}

	private static void DestroySprite(ref Sprite sprite)
	{
		if (!(sprite == null))
		{
			Texture2D texture = sprite.texture;
			UnityEngine.Object.Destroy(sprite);
			if (texture != null)
			{
				UnityEngine.Object.Destroy(texture);
			}
			sprite = null;
		}
	}

	private void SelectCategory(string category)
	{
		_activeCategory = category;
		_activeCategoryIndex = ModConfig.CategoryOrder.IndexOf(category);
		_selectedRowIndex = -1;
		foreach (KeyValuePair<string, Button> tabButton in _tabButtons)
		{
			Image component = tabButton.Value.GetComponent<Image>();
			bool flag = tabButton.Key == category;
			component.color = (flag ? GoldColor : TabInactive);
			TMP_Text componentInChildren = tabButton.Value.GetComponentInChildren<TMP_Text>();
			if (componentInChildren != null)
			{
				componentInChildren.color = (flag ? Color.white : new Color(0.8f, 0.8f, 0.8f));
			}
		}
		ScrollToTab(_activeCategoryIndex);
		RefreshCategory();
	}

	private void SetSelectedRow(int index)
	{
		if (_selectedRowIndex >= 0 && _selectedRowIndex < _entryRows.Count)
		{
			Image component = _entryRows[_selectedRowIndex].GetComponent<Image>();
			if (component != null)
			{
				component.color = RowBg;
			}
		}
		_selectedRowIndex = index;
		if (_selectedRowIndex >= 0 && _selectedRowIndex < _entryRows.Count)
		{
			Image component2 = _entryRows[_selectedRowIndex].GetComponent<Image>();
			if (component2 != null)
			{
				component2.color = RowBgSelected;
			}
			ScrollToRow(_selectedRowIndex);
		}
	}

	private void AdjustSelectedValue(int direction)
	{
		if (_selectedRowIndex < 0 || _selectedRowIndex >= _entryData.Count)
		{
			return;
		}
		ConfigEntryBase configEntryBase = _entryData[_selectedRowIndex];
		if (configEntryBase.SettingType == typeof(float))
		{
			ConfigEntry<float> configEntry = (ConfigEntry<float>)configEntryBase;
			float num = 0f;
			float num2 = 100f;
			if (configEntryBase.Description?.AcceptableValues is AcceptableValueRange<float> acceptableValueRange)
			{
				num = acceptableValueRange.MinValue;
				num2 = acceptableValueRange.MaxValue;
			}
			float num3 = (num2 - num) * 0.02f;
			configEntry.Value = Mathf.Clamp(Mathf.Round((configEntry.Value + num3 * (float)direction) * 100f) / 100f, num, num2);
			if (_selectedRowIndex < _entrySliders.Count && _entrySliders[_selectedRowIndex] != null)
			{
				_entrySliders[_selectedRowIndex].value = configEntry.Value;
			}
		}
		else if (configEntryBase.SettingType == typeof(int))
		{
			ConfigEntry<int> configEntry2 = (ConfigEntry<int>)configEntryBase;
			int num4 = 0;
			int num5 = 100;
			if (configEntryBase.Description?.AcceptableValues is AcceptableValueRange<int> acceptableValueRange2)
			{
				num4 = acceptableValueRange2.MinValue;
				num5 = acceptableValueRange2.MaxValue;
			}
			int num6 = Mathf.Max(1, (num5 - num4) / 50);
			configEntry2.Value = Mathf.Clamp(configEntry2.Value + num6 * direction, num4, num5);
			if (_selectedRowIndex < _entrySliders.Count && _entrySliders[_selectedRowIndex] != null)
			{
				_entrySliders[_selectedRowIndex].value = configEntry2.Value;
			}
		}
		else if (configEntryBase.SettingType == typeof(bool))
		{
			ConfigEntry<bool> configEntry3 = (ConfigEntry<bool>)configEntryBase;
			configEntry3.Value = !configEntry3.Value;
			RefreshCategory();
		}
	}

	private void RefreshCategory()
	{
		foreach (GameObject entryRow in _entryRows)
		{
			UnityEngine.Object.Destroy(entryRow);
		}
		_entryRows.Clear();
		_entryData.Clear();
		_entrySliders.Clear();
		_entryResetActions.Clear();
		if (!ModConfig.Categories.TryGetValue(_activeCategory, out var value))
		{
			return;
		}
		float num = -4f;
		foreach (ConfigEntryBase item in value)
		{
			GameObject gameObject = BuildEntryRow(item, num);
			if (gameObject != null)
			{
				_entryRows.Add(gameObject);
				num -= 32f;
			}
		}
		_contentRT.sizeDelta = new Vector2(0f, 0f - num + 8f);
		if (_scrollRect != null)
		{
			_scrollRect.verticalNormalizedPosition = 1f;
		}
		if (_selectedRowIndex >= _entryRows.Count)
		{
			_selectedRowIndex = _entryRows.Count - 1;
		}
		if (_selectedRowIndex >= 0)
		{
			SetSelectedRow(_selectedRowIndex);
		}
	}

	private void ScrollToRow(int index)
	{
		if (_scrollRect == null || _contentRT == null || index < 0 || index >= _entryRows.Count)
		{
			return;
		}
		RectTransform component = _entryRows[index].GetComponent<RectTransform>();
		float y = _contentRT.sizeDelta.y;
		float height = _scrollRect.GetComponent<RectTransform>().rect.height;
		if (!(y <= height))
		{
			float num = 0f - component.anchoredPosition.y;
			float num2 = num + 30f;
			float num3 = y - height;
			float num4 = (1f - _scrollRect.verticalNormalizedPosition) * num3;
			if (num < num4)
			{
				_scrollRect.verticalNormalizedPosition = 1f - num / num3;
			}
			else if (num2 > num4 + height)
			{
				_scrollRect.verticalNormalizedPosition = 1f - (num2 - height) / num3;
			}
		}
	}

	private void ScrollToTab(int index)
	{
		if (_tabScrollRect == null || _tabContentRT == null || index < 0)
		{
			return;
		}
		RectTransform component = _tabScrollRect.GetComponent<RectTransform>();
		float y = _tabContentRT.sizeDelta.y;
		float height = component.rect.height;
		if (!(y <= height))
		{
			float num = 32f;
			float num2 = 2f;
			float num3 = 4f + (float)index * (num + num2);
			float num4 = num3 + num;
			float num5 = y - height;
			float num6 = (1f - _tabScrollRect.verticalNormalizedPosition) * num5;
			if (num3 < num6)
			{
				_tabScrollRect.verticalNormalizedPosition = 1f - num3 / num5;
			}
			else if (num4 > num6 + height)
			{
				_tabScrollRect.verticalNormalizedPosition = 1f - (num4 - height) / num5;
			}
		}
	}

	private void BuildTitle(Transform parent)
	{
		GameObject gameObject = new GameObject("Title", typeof(RectTransform));
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		RectTransform component = gameObject.GetComponent<RectTransform>();
		component.anchorMin = new Vector2(0f, 1f);
		component.anchorMax = new Vector2(1f, 1f);
		component.pivot = new Vector2(0.5f, 1f);
		component.sizeDelta = new Vector2(0f, 32f);
		component.anchoredPosition = Vector2.zero;
		TextMeshProUGUI textMeshProUGUI = gameObject.AddComponent<TextMeshProUGUI>();
		if (_font != null)
		{
			textMeshProUGUI.font = _font;
		}
		textMeshProUGUI.text = ModLocalization.Loc("hc_ui_settings_title");
		textMeshProUGUI.fontSize = 18f;
		textMeshProUGUI.color = LabelText;
		textMeshProUGUI.fontStyle = FontStyles.Bold;
		textMeshProUGUI.alignment = TextAlignmentOptions.Center;
		textMeshProUGUI.raycastTarget = false;
	}

	private void BuildCloseButton(Transform parent)
	{
		GameObject gameObject = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		RectTransform component = gameObject.GetComponent<RectTransform>();
		component.anchorMin = new Vector2(1f, 1f);
		component.anchorMax = new Vector2(1f, 1f);
		component.pivot = new Vector2(1f, 1f);
		component.sizeDelta = new Vector2(36f, 36f);
		component.anchoredPosition = new Vector2(-4f, -2f);
		Image component2 = gameObject.GetComponent<Image>();
		Sprite btnBgSprite = GetBtnBgSprite();
		if (btnBgSprite != null)
		{
			component2.sprite = btnBgSprite;
			component2.type = Image.Type.Simple;
			component2.color = new Color(0.6f, 0.2f, 0.2f, 1f);
		}
		else
		{
			component2.color = new Color(0.3f, 0.1f, 0.1f, 0.9f);
		}
		gameObject.GetComponent<Button>().onClick.AddListener(Hide);
		GameObject gameObject2 = new GameObject("X", typeof(RectTransform));
		gameObject2.transform.SetParent(gameObject.transform, worldPositionStays: false);
		TextMeshProUGUI textMeshProUGUI = gameObject2.AddComponent<TextMeshProUGUI>();
		if (_font != null)
		{
			textMeshProUGUI.font = _font;
		}
		textMeshProUGUI.text = "X";
		textMeshProUGUI.fontSize = 16f;
		textMeshProUGUI.color = Color.white;
		textMeshProUGUI.alignment = TextAlignmentOptions.Center;
		textMeshProUGUI.raycastTarget = false;
		RectTransform component3 = gameObject2.GetComponent<RectTransform>();
		component3.anchorMin = Vector2.zero;
		component3.anchorMax = Vector2.one;
		component3.offsetMin = Vector2.zero;
		component3.offsetMax = Vector2.zero;
	}

	private void BuildTabColumn(Transform parent)
	{
		GameObject gameObject = new GameObject("TabColumn", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		RectTransform component = gameObject.GetComponent<RectTransform>();
		component.anchorMin = new Vector2(0f, 0f);
		component.anchorMax = new Vector2(0f, 1f);
		component.pivot = new Vector2(0f, 1f);
		component.offsetMin = new Vector2(8f, 8f);
		component.offsetMax = new Vector2(158f, -36f);
		// This Image stays a raycast target on purpose: the tab column's own ScrollRect lives on this GameObject, so
		// this is the surface that receives the wheel / drag over the column (the tabs bubble their events up to it).
		// It is the parent of every tab, so it can never be in front of one.
		gameObject.GetComponent<Image>().color = ColBg;
		GameObject gameObject2 = new GameObject("TabContent", typeof(RectTransform));
		gameObject2.transform.SetParent(gameObject.transform, worldPositionStays: false);
		_tabContentRT = gameObject2.GetComponent<RectTransform>();
		_tabContentRT.anchorMin = new Vector2(0f, 1f);
		_tabContentRT.anchorMax = new Vector2(1f, 1f);
		_tabContentRT.pivot = new Vector2(0.5f, 1f);
		_tabContentRT.anchoredPosition = Vector2.zero;
		float num = -4f;
		float num2 = 32f;
		float num3 = 2f;
		foreach (string item in ModConfig.CategoryOrder)
		{
			GameObject gameObject3 = new GameObject("Tab_" + item, typeof(RectTransform), typeof(Image), typeof(Button));
			gameObject3.transform.SetParent(_tabContentRT, worldPositionStays: false);
			RectTransform component2 = gameObject3.GetComponent<RectTransform>();
			component2.anchorMin = new Vector2(0f, 1f);
			component2.anchorMax = new Vector2(1f, 1f);
			component2.pivot = new Vector2(0.5f, 1f);
			component2.anchoredPosition = new Vector2(0f, num);
			component2.sizeDelta = new Vector2(-8f, num2);
			num -= num2 + num3;
			Image component3 = gameObject3.GetComponent<Image>();
			Sprite catBgSprite = GetCatBgSprite();
			if (catBgSprite != null)
			{
				component3.sprite = catBgSprite;
				component3.type = Image.Type.Simple;
				component3.color = TabInactive;
			}
			else
			{
				component3.color = TabInactive;
			}
			GameObject gameObject4 = new GameObject("Label", typeof(RectTransform));
			gameObject4.transform.SetParent(gameObject3.transform, worldPositionStays: false);
			TextMeshProUGUI textMeshProUGUI = gameObject4.AddComponent<TextMeshProUGUI>();
			if (_font != null)
			{
				textMeshProUGUI.font = _font;
			}
			// the tab shows the translated name, the category itself stays the config section key
			textMeshProUGUI.text = ModLocalization.LocOr("hc_cat_" + item.ToLowerInvariant(), item);
			textMeshProUGUI.fontSize = 11f;
			textMeshProUGUI.color = Color.white;
			textMeshProUGUI.alignment = TextAlignmentOptions.Center;
			textMeshProUGUI.raycastTarget = false;
			RectTransform component4 = gameObject4.GetComponent<RectTransform>();
			component4.anchorMin = Vector2.zero;
			component4.anchorMax = Vector2.one;
			component4.offsetMin = Vector2.zero;
			component4.offsetMax = Vector2.zero;
			string catCapture = item;
			Button component5 = gameObject3.GetComponent<Button>();
			component5.onClick.AddListener(delegate
			{
				SelectCategory(catCapture);
			});
			_tabButtons[item] = component5;
		}
		_tabContentRT.sizeDelta = new Vector2(0f, 0f - num + 4f);
		Scrollbar verticalScrollbar = CreateHiddenScrollbar(gameObject.transform);
		_tabScrollRect = gameObject.AddComponent<ScrollRect>();
		_tabScrollRect.content = _tabContentRT;
		_tabScrollRect.viewport = component;
		_tabScrollRect.vertical = true;
		_tabScrollRect.horizontal = false;
		_tabScrollRect.movementType = ScrollRect.MovementType.Clamped;
		_tabScrollRect.scrollSensitivity = 30f;
		_tabScrollRect.verticalScrollbar = verticalScrollbar;
		_tabScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
	}

	private void BuildContentArea(Transform parent)
	{
		float x = 166f;
		GameObject gameObject = new GameObject("ContentScroll", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		Image component = gameObject.GetComponent<Image>();
		component.color = new Color(0f, 0f, 0f, 0.01f);
		// This Image stays a raycast target on purpose: the content's ScrollRect lives on this GameObject, so this is
		// the scroll area - the surface that receives the wheel / drag over the panel. It is the parent of every row,
		// so it can never be in front of one.
		component.raycastTarget = true;
		RectTransform component2 = gameObject.GetComponent<RectTransform>();
		component2.anchorMin = new Vector2(0f, 0f);
		component2.anchorMax = new Vector2(1f, 1f);
		component2.offsetMin = new Vector2(x, 8f);
		component2.offsetMax = new Vector2(-8f, -36f);
		GameObject gameObject2 = new GameObject("Content", typeof(RectTransform));
		gameObject2.transform.SetParent(gameObject.transform, worldPositionStays: false);
		_contentRT = gameObject2.GetComponent<RectTransform>();
		_contentRT.anchorMin = new Vector2(0f, 1f);
		_contentRT.anchorMax = new Vector2(1f, 1f);
		_contentRT.pivot = new Vector2(0.5f, 1f);
		_contentRT.anchoredPosition = Vector2.zero;
		_contentRT.sizeDelta = new Vector2(0f, 0f);
		Scrollbar verticalScrollbar = CreateHiddenScrollbar(gameObject.transform);
		_scrollRect = gameObject.AddComponent<ScrollRect>();
		_scrollRect.content = _contentRT;
		_scrollRect.viewport = component2;
		_scrollRect.vertical = true;
		_scrollRect.horizontal = false;
		_scrollRect.movementType = ScrollRect.MovementType.Clamped;
		_scrollRect.scrollSensitivity = 40f;
		_scrollRect.verticalScrollbar = verticalScrollbar;
		_scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
	}

	private static Scrollbar CreateHiddenScrollbar(Transform parent)
	{
		GameObject gameObject = new GameObject("Scrollbar", typeof(RectTransform));
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		RectTransform component = gameObject.GetComponent<RectTransform>();
		component.anchorMin = new Vector2(1f, 0f);
		component.anchorMax = new Vector2(1f, 1f);
		component.pivot = new Vector2(1f, 0.5f);
		component.sizeDelta = new Vector2(10f, 0f);
		// Invisible bar and handle: they are NOT raycast targets. Nobody can aim at something that is not drawn, and
		// as the last siblings of the viewport they used to be the topmost graphics of the whole panel - the first
		// suspects of "the click is swallowed". The ScrollRect holds them by reference (verticalScrollbar/handleRect),
		// so this changes nothing for the wheel or the drag: those are received by the scroll area (the viewport
		// Image) and by the rows above it.
		Image barImage = gameObject.AddComponent<Image>();
		barImage.color = Color.clear;
		barImage.raycastTarget = false;
		GameObject gameObject2 = new GameObject("Sliding Area", typeof(RectTransform));
		gameObject2.transform.SetParent(gameObject.transform, worldPositionStays: false);
		RectTransform component2 = gameObject2.GetComponent<RectTransform>();
		component2.anchorMin = Vector2.zero;
		component2.anchorMax = Vector2.one;
		component2.offsetMin = Vector2.zero;
		component2.offsetMax = Vector2.zero;
		GameObject gameObject3 = new GameObject("Handle", typeof(RectTransform), typeof(Image));
		gameObject3.transform.SetParent(gameObject2.transform, worldPositionStays: false);
		Image handleImage = gameObject3.GetComponent<Image>();
		handleImage.color = Color.clear;
		handleImage.raycastTarget = false;
		RectTransform component3 = gameObject3.GetComponent<RectTransform>();
		component3.anchorMin = Vector2.zero;
		component3.anchorMax = new Vector2(1f, 0.5f);
		component3.offsetMin = Vector2.zero;
		component3.offsetMax = Vector2.zero;
		Scrollbar scrollbar = gameObject.AddComponent<Scrollbar>();
		scrollbar.handleRect = component3;
		scrollbar.direction = Scrollbar.Direction.BottomToTop;
		// First sibling of the viewport: the invisible bar (and its handle) now sit BELOW the content instead of on
		// top of it, so the topmost graphic of the panel is a control, never a decorative element. The ScrollRect
		// does not care about the sibling order (it holds the references).
		gameObject.transform.SetAsFirstSibling();
		return scrollbar;
	}

	private GameObject BuildEntryRow(ConfigEntryBase entry, float yPos)
	{
		if (entry.SettingType == typeof(float))
		{
			return BuildFloatRow(entry, yPos);
		}
		if (entry.SettingType == typeof(int))
		{
			return BuildIntRow(entry, yPos);
		}
		if (entry.SettingType == typeof(bool))
		{
			return BuildBoolRow(entry, yPos);
		}
		if (entry.SettingType == typeof(KeyCode))
		{
			return BuildKeyCodeRow(entry, yPos);
		}
		return null;
	}

	private GameObject BuildFloatRow(ConfigEntryBase entry, float yPos)
	{
		ConfigEntry<float> typed = (ConfigEntry<float>)entry;
		float min = 0f;
		float max = 100f;
		if (entry.Description?.AcceptableValues is AcceptableValueRange<float> acceptableValueRange)
		{
			min = acceptableValueRange.MinValue;
			max = acceptableValueRange.MaxValue;
		}
		GameObject gameObject = CreateRowContainer("Row_" + entry.Definition.Key, yPos);
		GameObject gameObject2 = CreateLabel(gameObject.transform, entry.Definition.Key, 12f, Color.white);
		RectTransform component = gameObject2.GetComponent<RectTransform>();
		component.anchorMin = new Vector2(0f, 0f);
		component.anchorMax = new Vector2(0.38f, 1f);
		component.offsetMin = new Vector2(8f, 0f);
		component.offsetMax = Vector2.zero;
		gameObject2.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.MidlineLeft;
		GameObject gameObject3 = CreateLabel(gameObject.transform, FormatFloat(typed.Value), 11f, GoldTextColor);
		RectTransform component2 = gameObject3.GetComponent<RectTransform>();
		component2.anchorMin = new Vector2(0.74f, 0f);
		component2.anchorMax = new Vector2(0.84f, 1f);
		component2.offsetMin = Vector2.zero;
		component2.offsetMax = Vector2.zero;
		gameObject3.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.MidlineRight;
		TMP_Text valTMP = gameObject3.GetComponent<TMP_Text>();
		GameObject gameObject4 = CreateSlider(gameObject.transform, min, max, typed.Value, wholeNumbers: false);
		RectTransform component3 = gameObject4.GetComponent<RectTransform>();
		component3.anchorMin = new Vector2(0.4f, 0.15f);
		component3.anchorMax = new Vector2(0.73f, 0.85f);
		component3.offsetMin = Vector2.zero;
		component3.offsetMax = Vector2.zero;
		Slider slider = gameObject4.GetComponent<Slider>();
		slider.onValueChanged.AddListener(delegate(float v)
		{
			typed.Value = Mathf.Round(v * 100f) / 100f;
			valTMP.text = FormatFloat(typed.Value);
		});
		CreateDefaultButton(gameObject.transform, entry, delegate
		{
			slider.value = (float)entry.DefaultValue;
			valTMP.text = FormatFloat(typed.Value);
		});
		_entryData.Add(entry);
		_entrySliders.Add(slider);
		_entryResetActions.Add(delegate
		{
			entry.BoxedValue = entry.DefaultValue;
			slider.value = (float)entry.DefaultValue;
			valTMP.text = FormatFloat(typed.Value);
		});
		return gameObject;
	}

	private GameObject BuildIntRow(ConfigEntryBase entry, float yPos)
	{
		ConfigEntry<int> typed = (ConfigEntry<int>)entry;
		int num = 0;
		int num2 = 100;
		if (entry.Description?.AcceptableValues is AcceptableValueRange<int> acceptableValueRange)
		{
			num = acceptableValueRange.MinValue;
			num2 = acceptableValueRange.MaxValue;
		}
		GameObject gameObject = CreateRowContainer("Row_" + entry.Definition.Key, yPos);
		GameObject gameObject2 = CreateLabel(gameObject.transform, entry.Definition.Key, 12f, Color.white);
		RectTransform component = gameObject2.GetComponent<RectTransform>();
		component.anchorMin = new Vector2(0f, 0f);
		component.anchorMax = new Vector2(0.38f, 1f);
		component.offsetMin = new Vector2(8f, 0f);
		component.offsetMax = Vector2.zero;
		gameObject2.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.MidlineLeft;
		GameObject gameObject3 = CreateLabel(gameObject.transform, typed.Value.ToString(), 11f, GoldTextColor);
		RectTransform component2 = gameObject3.GetComponent<RectTransform>();
		component2.anchorMin = new Vector2(0.74f, 0f);
		component2.anchorMax = new Vector2(0.84f, 1f);
		component2.offsetMin = Vector2.zero;
		component2.offsetMax = Vector2.zero;
		gameObject3.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.MidlineRight;
		TMP_Text valTMP = gameObject3.GetComponent<TMP_Text>();
		GameObject gameObject4 = CreateSlider(gameObject.transform, num, num2, typed.Value, wholeNumbers: true);
		RectTransform component3 = gameObject4.GetComponent<RectTransform>();
		component3.anchorMin = new Vector2(0.4f, 0.15f);
		component3.anchorMax = new Vector2(0.73f, 0.85f);
		component3.offsetMin = Vector2.zero;
		component3.offsetMax = Vector2.zero;
		Slider slider = gameObject4.GetComponent<Slider>();
		slider.onValueChanged.AddListener(delegate(float v)
		{
			typed.Value = Mathf.RoundToInt(v);
			valTMP.text = typed.Value.ToString();
		});
		CreateDefaultButton(gameObject.transform, entry, delegate
		{
			slider.value = (int)entry.DefaultValue;
			valTMP.text = typed.Value.ToString();
		});
		_entryData.Add(entry);
		_entrySliders.Add(slider);
		_entryResetActions.Add(delegate
		{
			entry.BoxedValue = entry.DefaultValue;
			slider.value = (int)entry.DefaultValue;
			valTMP.text = typed.Value.ToString();
		});
		return gameObject;
	}

	private GameObject BuildBoolRow(ConfigEntryBase entry, float yPos)
	{
		ConfigEntry<bool> typed = (ConfigEntry<bool>)entry;
		GameObject gameObject = CreateRowContainer("Row_" + entry.Definition.Key, yPos);
		GameObject gameObject2 = CreateLabel(gameObject.transform, entry.Definition.Key, 12f, Color.white);
		RectTransform component = gameObject2.GetComponent<RectTransform>();
		component.anchorMin = new Vector2(0f, 0f);
		component.anchorMax = new Vector2(0.65f, 1f);
		component.offsetMin = new Vector2(8f, 0f);
		component.offsetMax = Vector2.zero;
		gameObject2.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.MidlineLeft;
		GameObject gameObject3 = new GameObject("Toggle", typeof(RectTransform), typeof(Image), typeof(Button));
		gameObject3.transform.SetParent(gameObject.transform, worldPositionStays: false);
		RectTransform component2 = gameObject3.GetComponent<RectTransform>();
		component2.anchorMin = new Vector2(0.62f, 0.1f);
		component2.anchorMax = new Vector2(0.83f, 0.9f);
		component2.offsetMin = Vector2.zero;
		component2.offsetMax = Vector2.zero;
		Image btnImg = gameObject3.GetComponent<Image>();
		Sprite btnBgSprite = GetBtnBgSprite();
		if (btnBgSprite != null)
		{
			btnImg.sprite = btnBgSprite;
			btnImg.type = Image.Type.Simple;
			btnImg.color = (typed.Value ? GoldColor : Color.white);
		}
		else
		{
			btnImg.color = (typed.Value ? GoldColor : TabInactive);
		}
		GameObject gameObject4 = CreateLabel(gameObject3.transform, OnOff(typed.Value), 11f, Color.white);
		RectTransform component3 = gameObject4.GetComponent<RectTransform>();
		component3.anchorMin = Vector2.zero;
		component3.anchorMax = Vector2.one;
		component3.offsetMin = Vector2.zero;
		component3.offsetMax = Vector2.zero;
		gameObject4.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.Center;
		TMP_Text btnTMP = gameObject4.GetComponent<TMP_Text>();
		bool hasToggleSprite = btnBgSprite != null;
		gameObject3.GetComponent<Button>().onClick.AddListener(delegate
		{
			typed.Value = !typed.Value;
			btnImg.color = (typed.Value ? GoldColor : (hasToggleSprite ? Color.white : TabInactive));
			btnTMP.text = OnOff(typed.Value);
		});
		CreateDefaultButton(gameObject.transform, entry, delegate
		{
			typed.Value = (bool)entry.DefaultValue;
			btnImg.color = (typed.Value ? GoldColor : (hasToggleSprite ? Color.white : TabInactive));
			btnTMP.text = OnOff(typed.Value);
		});
		_entryData.Add(entry);
		_entrySliders.Add(null);
		_entryResetActions.Add(delegate
		{
			typed.Value = (bool)entry.DefaultValue;
			btnImg.color = (typed.Value ? GoldColor : (hasToggleSprite ? Color.white : TabInactive));
			btnTMP.text = OnOff(typed.Value);
		});
		return gameObject;
	}

	private GameObject BuildKeyCodeRow(ConfigEntryBase entry, float yPos)
	{
		ConfigEntry<KeyCode> typed = (ConfigEntry<KeyCode>)entry;
		GameObject gameObject = CreateRowContainer("Row_" + entry.Definition.Key, yPos);
		GameObject gameObject2 = CreateLabel(gameObject.transform, entry.Definition.Key, 12f, Color.white);
		RectTransform component = gameObject2.GetComponent<RectTransform>();
		component.anchorMin = new Vector2(0f, 0f);
		component.anchorMax = new Vector2(0.55f, 1f);
		component.offsetMin = new Vector2(8f, 0f);
		component.offsetMax = Vector2.zero;
		gameObject2.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.MidlineLeft;
		GameObject gameObject3 = new GameObject("KeyBind", typeof(RectTransform), typeof(Image), typeof(Button));
		gameObject3.transform.SetParent(gameObject.transform, worldPositionStays: false);
		RectTransform component2 = gameObject3.GetComponent<RectTransform>();
		component2.anchorMin = new Vector2(0.5f, 0.1f);
		component2.anchorMax = new Vector2(0.83f, 0.9f);
		component2.offsetMin = Vector2.zero;
		component2.offsetMax = Vector2.zero;
		Image component3 = gameObject3.GetComponent<Image>();
		Sprite btnBgSprite = GetBtnBgSprite();
		if (btnBgSprite != null)
		{
			component3.sprite = btnBgSprite;
			component3.type = Image.Type.Simple;
			component3.color = Color.white;
		}
		else
		{
			component3.color = new Color(0.15f, 0.15f, 0.18f, 0.9f);
		}
		GameObject gameObject4 = CreateLabel(gameObject3.transform, typed.Value.ToString(), 11f, GoldTextColor);
		RectTransform component4 = gameObject4.GetComponent<RectTransform>();
		component4.anchorMin = Vector2.zero;
		component4.anchorMax = Vector2.one;
		component4.offsetMin = Vector2.zero;
		component4.offsetMax = Vector2.zero;
		gameObject4.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.Center;
		TMP_Text btnTMP = gameObject4.GetComponent<TMP_Text>();
		bool listening = false;
		gameObject3.GetComponent<Button>().onClick.AddListener(delegate
		{
			listening = true;
			btnTMP.text = ModLocalization.Loc("hc_ui_press_key");
			btnTMP.color = LabelText;
		});
		KeyBindListener keyBindListener = gameObject3.AddComponent<KeyBindListener>();
		keyBindListener.Init(typed, btnTMP, () => listening, delegate(bool v)
		{
			listening = v;
		});
		CreateDefaultButton(gameObject.transform, entry, delegate
		{
			typed.Value = (KeyCode)entry.DefaultValue;
			btnTMP.text = typed.Value.ToString();
			btnTMP.color = GoldTextColor;
		});
		_entryData.Add(entry);
		_entrySliders.Add(null);
		_entryResetActions.Add(delegate
		{
			typed.Value = (KeyCode)entry.DefaultValue;
			btnTMP.text = typed.Value.ToString();
			btnTMP.color = GoldTextColor;
		});
		return gameObject;
	}

	private GameObject CreateRowContainer(string name, float yPos)
	{
		GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(Image));
		gameObject.transform.SetParent(_contentRT, worldPositionStays: false);
		RectTransform component = gameObject.GetComponent<RectTransform>();
		component.anchorMin = new Vector2(0f, 1f);
		component.anchorMax = new Vector2(1f, 1f);
		component.pivot = new Vector2(0.5f, 1f);
		component.anchoredPosition = new Vector2(0f, yPos);
		component.sizeDelta = new Vector2(-8f, 30f);
		// Decorative band behind the row's controls: the controls are its children (they always win the raycast) and
		// the wheel / drag is received by the scroll area (the viewport) behind it, so it does not need the click.
		Image rowImage = gameObject.GetComponent<Image>();
		rowImage.color = RowBg;
		rowImage.raycastTarget = false;
		return gameObject;
	}

	// Traducao PT-BR - auditoria de cobertura: the toggle labels were hardcoded "ON"/"OFF" here. They reuse the
	// radial's own keys (hc_radial_on/hc_radial_off), which are already localized in every language file.
	private static string OnOff(bool on)
	{
		return ModLocalization.Loc(on ? "hc_radial_on" : "hc_radial_off");
	}

	private GameObject CreateLabel(Transform parent, string text, float size, Color color)
	{
		GameObject gameObject = new GameObject("Label", typeof(RectTransform));
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		TextMeshProUGUI textMeshProUGUI = gameObject.AddComponent<TextMeshProUGUI>();
		if (_font != null)
		{
			textMeshProUGUI.font = _font;
		}
		textMeshProUGUI.text = text;
		textMeshProUGUI.fontSize = size;
		textMeshProUGUI.color = color;
		textMeshProUGUI.raycastTarget = false;
		textMeshProUGUI.overflowMode = TextOverflowModes.Ellipsis;
		return gameObject;
	}

	private static string FormatFloat(float v)
	{
		if (v >= 100f)
		{
			return v.ToString("F0");
		}
		if (v >= 10f)
		{
			return v.ToString("F1");
		}
		return v.ToString("F2");
	}

	private static GameObject CreateSlider(Transform parent, float min, float max, float value, bool wholeNumbers)
	{
		Slider slider = (((object)InventoryGui.instance != null) ? null : null);
		if (slider != null)
		{
			GameObject gameObject = UnityEngine.Object.Instantiate(slider.gameObject, parent);
			gameObject.name = "Slider";
			gameObject.SetActive(value: true);
			LayoutElement component = gameObject.GetComponent<LayoutElement>();
			if (component != null)
			{
				UnityEngine.Object.Destroy(component);
			}
			Slider component2 = gameObject.GetComponent<Slider>();
			if (component2 == null)
			{
				UnityEngine.Object.Destroy(gameObject);
				return CreateFallbackSlider(parent, min, max, value, wholeNumbers);
			}
			Sprite sliderBgSprite = GetSliderBgSprite();
			if (sliderBgSprite != null)
			{
				Image image = null;
				Transform transform = gameObject.transform.Find("Background");
				if (transform != null)
				{
					image = transform.GetComponent<Image>();
				}
				if (image == null)
				{
					Image[] componentsInChildren = gameObject.GetComponentsInChildren<Image>(includeInactive: true);
					foreach (Image image2 in componentsInChildren)
					{
						if (image2 != null && image2.gameObject.name.IndexOf("background", StringComparison.OrdinalIgnoreCase) >= 0)
						{
							image = image2;
							break;
						}
					}
				}
				if (image == null)
				{
					image = gameObject.GetComponent<Image>();
				}
				if (image != null)
				{
					image.sprite = sliderBgSprite;
					image.type = Image.Type.Simple;
					image.preserveAspect = false;
					image.color = Color.white;
				}
			}
			component2.onValueChanged.RemoveAllListeners();
			component2.transition = Selectable.Transition.None;
			component2.interactable = true;
			component2.navigation = new Navigation
			{
				mode = Navigation.Mode.Automatic
			};
			component2.wholeNumbers = wholeNumbers;
			component2.minValue = min;
			component2.maxValue = max;
			Image[] componentsInChildren2 = gameObject.GetComponentsInChildren<Image>(includeInactive: true);
			foreach (Image image3 in componentsInChildren2)
			{
				if (!(image3 == null))
				{
					image3.color = new Color(1f, 1f, 1f, image3.color.a);
				}
			}
			if (component2.fillRect != null)
			{
				Image component3 = component2.fillRect.GetComponent<Image>();
				if (component3 != null)
				{
					component3.color = GoldColor;
				}
			}
			component2.value = value;
			return gameObject;
		}
		return CreateFallbackSlider(parent, min, max, value, wholeNumbers);
	}

	private void CreateDefaultButton(Transform parent, ConfigEntryBase entry, Action onReset)
	{
		GameObject gameObject = new GameObject("DefaultBtn", typeof(RectTransform), typeof(Image), typeof(Button));
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		RectTransform component = gameObject.GetComponent<RectTransform>();
		component.anchorMin = new Vector2(0.85f, 0.1f);
		component.anchorMax = new Vector2(1f, 0.9f);
		component.offsetMin = Vector2.zero;
		component.offsetMax = new Vector2(-2f, 0f);
		Image component2 = gameObject.GetComponent<Image>();
		Sprite catBgSprite = GetCatBgSprite();
		if (catBgSprite != null)
		{
			component2.sprite = catBgSprite;
			component2.type = Image.Type.Simple;
			component2.color = TabInactive;
		}
		else
		{
			component2.color = DefaultBtnColor;
		}
		GameObject gameObject2 = new GameObject("Label", typeof(RectTransform));
		gameObject2.transform.SetParent(gameObject.transform, worldPositionStays: false);
		TextMeshProUGUI textMeshProUGUI = gameObject2.AddComponent<TextMeshProUGUI>();
		if (_font != null)
		{
			textMeshProUGUI.font = _font;
		}
		textMeshProUGUI.text = ModLocalization.Loc("hc_ui_default");
		textMeshProUGUI.fontSize = 9f;
		textMeshProUGUI.color = Color.white;
		textMeshProUGUI.alignment = TextAlignmentOptions.Center;
		textMeshProUGUI.raycastTarget = false;
		RectTransform component3 = gameObject2.GetComponent<RectTransform>();
		component3.anchorMin = Vector2.zero;
		component3.anchorMax = Vector2.one;
		component3.offsetMin = Vector2.zero;
		component3.offsetMax = Vector2.zero;
		gameObject.GetComponent<Button>().onClick.AddListener(delegate
		{
			entry.BoxedValue = entry.DefaultValue;
			onReset?.Invoke();
		});
	}

	private static GameObject CreateFallbackSlider(Transform parent, float min, float max, float value, bool wholeNumbers)
	{
		GameObject gameObject = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		GameObject gameObject2 = new GameObject("Background", typeof(RectTransform), typeof(Image));
		gameObject2.transform.SetParent(gameObject.transform, worldPositionStays: false);
		RectTransform component = gameObject2.GetComponent<RectTransform>();
		component.anchorMin = Vector2.zero;
		component.anchorMax = Vector2.one;
		component.offsetMin = Vector2.zero;
		component.offsetMax = Vector2.zero;
		Image component2 = gameObject2.GetComponent<Image>();
		Sprite sliderBgSprite = GetSliderBgSprite();
		if (sliderBgSprite != null)
		{
			component2.sprite = sliderBgSprite;
			component2.type = Image.Type.Simple;
			component2.color = Color.white;
		}
		else
		{
			component2.color = new Color(0.1f, 0.1f, 0.12f, 0.9f);
		}
		GameObject gameObject3 = new GameObject("Fill Area", typeof(RectTransform));
		gameObject3.transform.SetParent(gameObject.transform, worldPositionStays: false);
		RectTransform component3 = gameObject3.GetComponent<RectTransform>();
		component3.anchorMin = Vector2.zero;
		component3.anchorMax = Vector2.one;
		component3.offsetMin = new Vector2(5f, 0f);
		component3.offsetMax = new Vector2(-5f, 0f);
		GameObject gameObject4 = new GameObject("Fill", typeof(RectTransform), typeof(Image));
		gameObject4.transform.SetParent(gameObject3.transform, worldPositionStays: false);
		RectTransform component4 = gameObject4.GetComponent<RectTransform>();
		component4.anchorMin = Vector2.zero;
		component4.anchorMax = new Vector2(0f, 1f);
		component4.offsetMin = Vector2.zero;
		component4.offsetMax = Vector2.zero;
		Image fillImage = gameObject4.GetComponent<Image>();
		fillImage.color = GoldColor;
		// Decorative: the drag is received by the slider's background (it spans the whole track) and by the handle.
		fillImage.raycastTarget = false;
		GameObject gameObject5 = new GameObject("Handle Slide Area", typeof(RectTransform));
		gameObject5.transform.SetParent(gameObject.transform, worldPositionStays: false);
		RectTransform component5 = gameObject5.GetComponent<RectTransform>();
		component5.anchorMin = Vector2.zero;
		component5.anchorMax = Vector2.one;
		component5.offsetMin = new Vector2(5f, 0f);
		component5.offsetMax = new Vector2(-5f, 0f);
		GameObject gameObject6 = new GameObject("Handle", typeof(RectTransform), typeof(Image));
		gameObject6.transform.SetParent(gameObject5.transform, worldPositionStays: false);
		RectTransform component6 = gameObject6.GetComponent<RectTransform>();
		component6.sizeDelta = new Vector2(12f, 0f);
		component6.anchorMin = new Vector2(0f, 0f);
		component6.anchorMax = new Vector2(0f, 1f);
		gameObject6.GetComponent<Image>().color = new Color(0.95f, 0.85f, 0.65f, 1f);
		Slider component7 = gameObject.GetComponent<Slider>();
		component7.fillRect = component4;
		component7.handleRect = component6;
		component7.direction = Slider.Direction.LeftToRight;
		component7.minValue = min;
		component7.maxValue = max;
		component7.wholeNumbers = wholeNumbers;
		component7.value = value;
		return gameObject;
	}
}
