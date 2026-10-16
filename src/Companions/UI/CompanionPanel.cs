using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Companions;

public class CompanionPanel
{
	private const float UiScale = 1f;

	private static readonly Vector3 PreviewPos = new Vector3(10030f, 5000f, 10000f);

	private RenderTexture _rt;

	private Camera _cam;

	private GameObject _camGO;

	private GameObject _clone;

	private GameObject _lightRig;

	private RawImage _previewImg;

	private float _rotation;

	private bool _dragging;

	private float _lastMouseX;

	private const float AutoRotSpeed = 15f;

	private const float DragSensitivity = 0.4f;

	private Color _savedAmbient;

	private float _savedAmbientIntensity;

	private AmbientMode _savedAmbientMode;

	private CompanionAppearance _current;

	private CompanionTierDef _selectedTier;

	private List<string> _hairs = new List<string>();

	private List<string> _beards = new List<string>();

	private int _hairIndex;

	private int _beardIndex;

	private bool _listsLoaded;

	private TextMeshProUGUI _hairNameText;

	private TextMeshProUGUI _beardNameText;

	private GameObject _beardGroup;

	private TextMeshProUGUI _coinText;

	private Button _buyButton;

	private TMP_Text _buyButtonLabel;

	private Button _maleBtn;

	private Button _femaleBtn;

	private Button _companionTypeBtn;

	private Button _dvergerTypeBtn;

	private GameObject _appearanceGroup;

	private Slider _skinSlider;

	private Slider _hairHueSlider;

	private Slider _hairBrightSlider;

	private GameObject _dvergerGroup;

	private CompanionTierDef _selectedDvergerVariant;

	private readonly List<Button> _variantButtons = new List<Button>();

	internal bool StarterMode;

	internal Action<CompanionAppearance> OnSpawnConfirmed;

	private GameObject _bankDisplayGO;

	private static readonly MethodInfo _updateVisuals = AccessTools.Method(typeof(VisEquipment), "UpdateVisuals");

	private static readonly FieldInfo _currentModelIndex = AccessTools.Field(typeof(VisEquipment), "m_currentModelIndex");

	private static readonly Color SkinLight = new Color(0.97f, 0.85f, 0.7f);

	private static readonly Color SkinDark = new Color(0.52f, 0.32f, 0.18f);

	private static readonly Color HairBlonde = new Color(0.93f, 0.8f, 0.48f);

	private static readonly Color HairDark = new Color(0.1f, 0.06f, 0.03f);

	private const float HairBrightMin = 0.1f;

	private const float HairBrightMax = 1f;

	private static readonly Color ColBg = new Color(0f, 0f, 0f, 0.75f);

	private static readonly Color GoldColor = new Color(0.83f, 0.64f, 0.31f, 1f);

	private static readonly Color GoldTextColor = new Color(0.83f, 0.52f, 0.18f, 1f);

	private static readonly Color LabelText = new Color(1f, 0.9f, 0.5f, 1f);

	private static readonly Color BtnTint = new Color(0f, 0f, 0f, 0.75f);

	private static readonly Color TabInactive = new Color(0.45f, 0.45f, 0.45f, 1f);

	private const string BankDataKey = "TraderSharedBank_Balance";

	private static Sprite _catBtnSprite;

	private static Sprite _sliderBgSprite;

	private static Sprite _genderBtnSprite;

	public GameObject Root { get; private set; }

	public void Teardown()
	{
		ClearPreviewClone();
		if (_camGO != null)
		{
			UnityEngine.Object.Destroy(_camGO);
			_camGO = null;
			_cam = null;
		}
		if (_rt != null)
		{
			_rt.Release();
			UnityEngine.Object.Destroy(_rt);
			_rt = null;
		}
		if (Root != null)
		{
			UnityEngine.Object.Destroy(Root);
			Root = null;
		}
		_listsLoaded = false;
		DestroySprite(ref _catBtnSprite);
		DestroySprite(ref _sliderBgSprite);
		DestroySprite(ref _genderBtnSprite);
	}

	public void Build(Transform parent, float colTopInset, float bottomPad, TMP_FontAsset font, GameObject buttonTemplate, float buttonHeight, float leftXL, float leftXR, float midXL, float midXR, float rightXL, float rightXR, float panelHeight)
	{
		_current = CompanionAppearance.Default();
		_selectedTier = CompanionTierData.Companion;
		if (buttonHeight <= 0f)
		{
			buttonHeight = 30f;
		}
		Root = new GameObject("CompanionContent", typeof(RectTransform));
		Root.transform.SetParent(parent, worldPositionStays: false);
		RectTransform component = Root.GetComponent<RectTransform>();
		component.anchorMin = Vector2.zero;
		component.anchorMax = Vector2.one;
		component.offsetMin = Vector2.zero;
		component.offsetMax = Vector2.zero;
		component.localScale = Vector3.one * 1f;
		RectTransform col = CreateColumn("HC_LeftColumn", Root.transform, leftXL, leftXR, bottomPad, colTopInset);
		RectTransform col2 = CreateColumn("HC_MiddleColumn", Root.transform, midXL, midXR, bottomPad, colTopInset);
		RectTransform col3 = CreateColumn("HC_RightColumn", Root.transform, rightXL, rightXR, bottomPad, colTopInset);
		float colW = rightXR - rightXL;
		float colH = panelHeight - colTopInset - bottomPad;
		BuildCustomColumn(col, font, buttonTemplate);
		BuildMiddleColumn(col2, font, buttonTemplate, buttonHeight);
		BuildPreviewColumn(col3, colW, colH);
		Root.SetActive(value: false);
	}

	public void Refresh()
	{
		if (!_listsLoaded)
		{
			LoadHairBeardLists();
		}
		SetupPreviewClone();
		RefreshBankDisplay();
	}

	public void UpdatePerFrame()
	{
		UpdatePreviewRotation();
		UpdatePreviewCamera();
		RefreshBankDisplay();
		if (_cam == null)
		{
			return;
		}
		_savedAmbient = RenderSettings.ambientLight;
		_savedAmbientIntensity = RenderSettings.ambientIntensity;
		_savedAmbientMode = RenderSettings.ambientMode;
		try
		{
			RenderSettings.ambientMode = AmbientMode.Flat;
			RenderSettings.ambientLight = new Color(0.45f, 0.4f, 0.35f);
			RenderSettings.ambientIntensity = 1.2f;
			_cam.Render();
		}
		finally
		{
			RenderSettings.ambientMode = _savedAmbientMode;
			RenderSettings.ambientLight = _savedAmbient;
			RenderSettings.ambientIntensity = _savedAmbientIntensity;
		}
	}

	private static Sprite GetCatBtnSprite()
	{
		if (_catBtnSprite != null)
		{
			return _catBtnSprite;
		}
		Texture2D texture2D = TextureLoader.LoadUITexture("CategoryBackground");
		if (texture2D == null)
		{
			return null;
		}
		_catBtnSprite = Sprite.Create(texture2D, new Rect(0f, 0f, texture2D.width, texture2D.height), new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect, new Vector4(12f, 12f, 12f, 12f));
		_catBtnSprite.name = "CategoryBackground";
		return _catBtnSprite;
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
		_sliderBgSprite.name = "SliderBackground";
		return _sliderBgSprite;
	}

	private static Sprite GetGenderBtnSprite()
	{
		if (_genderBtnSprite != null)
		{
			return _genderBtnSprite;
		}
		Texture2D texture2D = TextureLoader.LoadUITexture("ButtonBackground");
		if (texture2D == null)
		{
			return null;
		}
		_genderBtnSprite = Sprite.Create(texture2D, new Rect(0f, 0f, texture2D.width, texture2D.height), new Vector2(0.5f, 0.5f), 100f);
		_genderBtnSprite.name = "ButtonBackground";
		return _genderBtnSprite;
	}

	private static void DestroySprite(ref Sprite sprite)
	{
		if (sprite != null)
		{
			if (sprite.texture != null)
			{
				UnityEngine.Object.Destroy(sprite.texture);
			}
			UnityEngine.Object.Destroy(sprite);
			sprite = null;
		}
	}

	private static RectTransform CreateColumn(string name, Transform parent, float xLeft, float xRight, float bottomPad, float colTopInset)
	{
		GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(Image));
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		RectTransform component = gameObject.GetComponent<RectTransform>();
		component.anchorMin = new Vector2(0f, 0f);
		component.anchorMax = new Vector2(0f, 1f);
		component.pivot = new Vector2(0f, 0.5f);
		component.offsetMin = new Vector2(xLeft, bottomPad);
		component.offsetMax = new Vector2(xRight, 0f - colTopInset);
		Image component2 = gameObject.GetComponent<Image>();
		component2.sprite = null;
		component2.color = ColBg;
		return component;
	}

	private void BuildCustomColumn(RectTransform col, TMP_FontAsset font, GameObject buttonTemplate)
	{
		GameObject gameObject = new GameObject("CustomScroll", typeof(RectTransform), typeof(Image), typeof(Mask));
		gameObject.transform.SetParent(col, worldPositionStays: false);
		RectTransform component = gameObject.GetComponent<RectTransform>();
		component.anchorMin = Vector2.zero;
		component.anchorMax = Vector2.one;
		component.offsetMin = Vector2.zero;
		component.offsetMax = Vector2.zero;
		gameObject.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.003f);
		gameObject.GetComponent<Mask>().showMaskGraphic = false;
		GameObject gameObject2 = new GameObject("Content", typeof(RectTransform));
		gameObject2.transform.SetParent(gameObject.transform, worldPositionStays: false);
		RectTransform component2 = gameObject2.GetComponent<RectTransform>();
		component2.anchorMin = new Vector2(0f, 1f);
		component2.anchorMax = new Vector2(1f, 1f);
		component2.pivot = new Vector2(0.5f, 1f);
		component2.anchoredPosition = Vector2.zero;
		Scrollbar verticalScrollbar = CreateHiddenScrollbar(col);
		ScrollRect scrollRect = gameObject.AddComponent<ScrollRect>();
		scrollRect.content = component2;
		scrollRect.viewport = component;
		scrollRect.vertical = true;
		scrollRect.horizontal = false;
		scrollRect.movementType = ScrollRect.MovementType.Clamped;
		scrollRect.scrollSensitivity = 40f;
		scrollRect.verticalScrollbar = verticalScrollbar;
		scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
		Transform transform = gameObject2.transform;
		float num = -6f;
		float num2 = 4f;
		float num3 = 10f;
		float h = 22f;
		float num4 = 42f;
		float num5 = 36f;
		float num6 = 24f;
		float num7 = num;
		num = PlacePlainLabel(transform, font, ModLocalization.Loc("hc_ui_label_type"), num, h, "Type");
		num -= num2;
		Transform transform2 = MakeAnchoredRow(transform, num, num4);
		_companionTypeBtn = AddToggleButton(transform2, "Companion", buttonTemplate, new Vector2(0.01f, 0f), new Vector2(0.49f, 1f), delegate
		{
			SetType(CompanionTierData.Companion);
		}, ModLocalization.Loc("hc_ui_btn_companion"));
		_dvergerTypeBtn = AddToggleButton(transform2, "Dverger", buttonTemplate, new Vector2(0.51f, 0f), new Vector2(0.99f, 1f), delegate
		{
			SetType(CompanionTierData.Dverger);
		}, ModLocalization.Loc("hc_ui_btn_dverger"));
		transform2.gameObject.SetActive(value: false);
		Transform transform3 = transform.Find("TypeLabel");
		if (transform3 != null)
		{
			transform3.gameObject.SetActive(value: false);
		}
		num = num7;
		_appearanceGroup = new GameObject("AppearanceGroup", typeof(RectTransform));
		_appearanceGroup.transform.SetParent(transform, worldPositionStays: false);
		RectTransform component3 = _appearanceGroup.GetComponent<RectTransform>();
		component3.anchorMin = new Vector2(0f, 1f);
		component3.anchorMax = new Vector2(1f, 1f);
		component3.pivot = new Vector2(0f, 1f);
		component3.anchoredPosition = new Vector2(0f, num);
		float y = 0f;
		y = PlacePlainLabel(_appearanceGroup.transform, font, ModLocalization.Loc("hc_ui_label_gender"), y, h, "Gender");
		y -= num2;
		Transform parent = MakeAnchoredRow(_appearanceGroup.transform, y, num4);
		y -= num4 + num3;
		_maleBtn = AddToggleButton(parent, "Male", buttonTemplate, new Vector2(0.01f, 0f), new Vector2(0.49f, 1f), delegate
		{
			SetGender(0);
		}, ModLocalization.Loc("hc_ui_btn_male"));
		_femaleBtn = AddToggleButton(parent, "Female", buttonTemplate, new Vector2(0.51f, 0f), new Vector2(0.99f, 1f), delegate
		{
			SetGender(1);
		}, ModLocalization.Loc("hc_ui_btn_female"));
		y = PlacePlainLabel(_appearanceGroup.transform, font, ModLocalization.Loc("hc_ui_label_hair"), y, h, "HairStyle");
		y -= num2;
		BuildPickerRow(_appearanceGroup.transform, out _hairNameText, "Hair1", y, num5, font, buttonTemplate, delegate
		{
			CycleHair(-1);
		}, delegate
		{
			CycleHair(1);
		});
		y -= num5 + num3;
		_beardGroup = new GameObject("BeardGroup", typeof(RectTransform));
		_beardGroup.transform.SetParent(_appearanceGroup.transform, worldPositionStays: false);
		RectTransform component4 = _beardGroup.GetComponent<RectTransform>();
		component4.anchorMin = new Vector2(0f, 1f);
		component4.anchorMax = new Vector2(1f, 1f);
		component4.pivot = new Vector2(0f, 1f);
		component4.anchoredPosition = new Vector2(0f, y);
		float y2 = 0f;
		y2 = PlacePlainLabel(_beardGroup.transform, font, ModLocalization.Loc("hc_ui_label_beard"), y2, h, "BeardStyle");
		y2 -= num2;
		BuildPickerRow(_beardGroup.transform, out _beardNameText, ModLocalization.Loc("hc_ui_beard_none"), y2, num5, font, buttonTemplate, delegate
		{
			CycleBeard(-1);
		}, delegate
		{
			CycleBeard(1);
		});
		y2 -= num5 + num3;
		float num8 = 0f - y2;
		component4.sizeDelta = new Vector2(0f, num8);
		y -= num8;
		y = PlacePlainLabel(_appearanceGroup.transform, font, ModLocalization.Loc("hc_ui_label_skin"), y, h, "SkinTone");
		y -= num2;
		_skinSlider = MakeSlider(_appearanceGroup.transform, "SkinSlider", y, num6, 0.05f, delegate(float t2)
		{
			_current.SkinColor = Utils.ColorToVec3(Color.Lerp(SkinLight, SkinDark, t2));
			RefreshPreviewAppearance();
		});
		y -= num6 + num3;
		y = PlacePlainLabel(_appearanceGroup.transform, font, ModLocalization.Loc("hc_ui_label_hairtone"), y, h, "HairTone");
		y -= num2;
		_hairHueSlider = MakeSlider(_appearanceGroup.transform, "HairHue", y, num6, 0.45f, delegate
		{
			UpdateHairColor();
		});
		y -= num6 + num3;
		y = PlacePlainLabel(_appearanceGroup.transform, font, ModLocalization.Loc("hc_ui_label_hairshade"), y, h, "HairShade");
		y -= num2;
		_hairBrightSlider = MakeSlider(_appearanceGroup.transform, "HairBright", y, num6, 0.85f, delegate
		{
			UpdateHairColor();
		});
		y -= num6 + num3;
		float num9 = 0f - y;
		component3.sizeDelta = new Vector2(0f, num9);
		_dvergerGroup = new GameObject("DvergerGroup", typeof(RectTransform));
		_dvergerGroup.transform.SetParent(transform, worldPositionStays: false);
		RectTransform component5 = _dvergerGroup.GetComponent<RectTransform>();
		component5.anchorMin = new Vector2(0f, 1f);
		component5.anchorMax = new Vector2(1f, 1f);
		component5.pivot = new Vector2(0f, 1f);
		component5.anchoredPosition = new Vector2(0f, num);
		float y3 = 0f;
		y3 = PlacePlainLabel(_dvergerGroup.transform, font, ModLocalization.Loc("hc_ui_label_subtype"), y3, h, "SubType");
		y3 -= num2;
		_variantButtons.Clear();
		_selectedDvergerVariant = CompanionTierData.DvergerVariants[0];
		CompanionTierDef[] dvergerVariants = CompanionTierData.DvergerVariants;
		foreach (CompanionTierDef v in dvergerVariants)
		{
			Transform parent2 = MakeAnchoredRow(_dvergerGroup.transform, y3, num4);
			y3 -= num4 + num2;
			Button item = AddToggleButton(parent2, v.DisplayName, buttonTemplate, new Vector2(0.01f, 0f), new Vector2(0.99f, 1f), delegate
			{
				SetDvergerVariant(v);
			}, CompanionTierData.LocalName(v));
			_variantButtons.Add(item);
		}
		float num11 = 0f - y3;
		component5.sizeDelta = new Vector2(0f, num11);
		_dvergerGroup.SetActive(value: false);
		float num12 = Mathf.Max(num9, num11);
		num -= num12;
		component2.sizeDelta = new Vector2(0f, 0f - num + 8f);
		float t = ((_skinSlider != null) ? _skinSlider.value : 0.05f);
		_current.SkinColor = Utils.ColorToVec3(Color.Lerp(SkinLight, SkinDark, t));
		UpdateHairColor();
		RefreshTypeButtons();
		RefreshGenderButtons();
	}

	private void BuildMiddleColumn(RectTransform col, TMP_FontAsset font, GameObject buttonTemplate, float btnH)
	{
		GameObject gameObject = new GameObject("Title", typeof(RectTransform));
		gameObject.transform.SetParent(col, worldPositionStays: false);
		TextMeshProUGUI textMeshProUGUI = gameObject.AddComponent<TextMeshProUGUI>();
		if (font != null)
		{
			textMeshProUGUI.font = font;
		}
		textMeshProUGUI.fontSize = 24f;
		textMeshProUGUI.color = Color.white;
		textMeshProUGUI.alignment = TextAlignmentOptions.Center;
		textMeshProUGUI.text = (StarterMode ? ModLocalization.Loc("hc_ui_title_choose") : ModLocalization.Loc("hc_ui_title_companions"));
		textMeshProUGUI.raycastTarget = false;
		RectTransform component = gameObject.GetComponent<RectTransform>();
		component.anchorMin = new Vector2(0f, 1f);
		component.anchorMax = new Vector2(1f, 1f);
		component.pivot = new Vector2(0.5f, 1f);
		component.sizeDelta = new Vector2(-20f, 32f);
		component.anchoredPosition = new Vector2(0f, -4f);
		float y = btnH + 44f;
		_bankDisplayGO = new GameObject("BankDisplay", typeof(RectTransform));
		_bankDisplayGO.transform.SetParent(col, worldPositionStays: false);
		_coinText = _bankDisplayGO.AddComponent<TextMeshProUGUI>();
		if (font != null)
		{
			_coinText.font = font;
		}
		_coinText.fontSize = 24f;
		_coinText.color = GoldTextColor;
		_coinText.alignment = TextAlignmentOptions.Center;
		_coinText.text = ModLocalization.LocFmt("hc_ui_bank", 0.ToString("N0"));
		_coinText.raycastTarget = false;
		RectTransform component2 = _bankDisplayGO.GetComponent<RectTransform>();
		component2.anchorMin = new Vector2(0f, 0f);
		component2.anchorMax = new Vector2(1f, 0f);
		component2.pivot = new Vector2(0.5f, 0f);
		component2.sizeDelta = new Vector2(-24f, 24f);
		component2.anchoredPosition = new Vector2(0f, btnH + 14f);
		if (StarterMode)
		{
			_bankDisplayGO.SetActive(value: false);
		}
		if (buttonTemplate != null)
		{
			string label = (StarterMode ? ModLocalization.Loc("hc_ui_btn_spawn") : ModLocalization.LocFmt("hc_ui_btn_buy", CompanionTierData.LocalName(CompanionTierData.Companion), CompanionTierData.Price.ToString("N0")));
			GameObject gameObject2 = CreateActionButton(buttonTemplate, col, "BuyButton", label);
			_buyButton = gameObject2.GetComponent<Button>();
			if (_buyButton != null)
			{
				_buyButton.onClick.AddListener(OnBuyClicked);
				_buyButton.interactable = true;
			}
			_buyButtonLabel = gameObject2.GetComponentInChildren<TMP_Text>(includeInactive: true);
			RectTransform component3 = gameObject2.GetComponent<RectTransform>();
			component3.anchorMin = new Vector2(0f, 0f);
			component3.anchorMax = new Vector2(1f, 0f);
			component3.pivot = new Vector2(0.5f, 0f);
			component3.sizeDelta = new Vector2(-24f, btnH);
			component3.anchoredPosition = new Vector2(0f, 8f);
		}
		GameObject gameObject3 = new GameObject("DescScrollArea", typeof(RectTransform), typeof(Image), typeof(Mask));
		gameObject3.transform.SetParent(col, worldPositionStays: false);
		RectTransform component4 = gameObject3.GetComponent<RectTransform>();
		component4.anchorMin = Vector2.zero;
		component4.anchorMax = Vector2.one;
		component4.offsetMin = new Vector2(8f, y);
		component4.offsetMax = new Vector2(-14f, -38f);
		gameObject3.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.003f);
		gameObject3.GetComponent<Mask>().showMaskGraphic = false;
		GameObject gameObject4 = new GameObject("DescText", typeof(RectTransform));
		gameObject4.transform.SetParent(gameObject3.transform, worldPositionStays: false);
		TextMeshProUGUI textMeshProUGUI2 = gameObject4.AddComponent<TextMeshProUGUI>();
		if (font != null)
		{
			textMeshProUGUI2.font = font;
		}
		textMeshProUGUI2.fontSize = 18f;
		textMeshProUGUI2.color = Color.white;
		textMeshProUGUI2.alignment = TextAlignmentOptions.TopLeft;
		textMeshProUGUI2.textWrappingMode = TextWrappingModes.Normal;
		textMeshProUGUI2.overflowMode = TextOverflowModes.Overflow;
		textMeshProUGUI2.richText = true;
		textMeshProUGUI2.raycastTarget = false;
		textMeshProUGUI2.text = (StarterMode ? ModLocalization.Loc("hc_ui_desc_starter") : ModLocalization.Loc("hc_ui_desc_trader"));
		RectTransform component5 = gameObject4.GetComponent<RectTransform>();
		component5.anchorMin = new Vector2(0f, 1f);
		component5.anchorMax = new Vector2(1f, 1f);
		component5.pivot = new Vector2(0.5f, 1f);
		component5.anchoredPosition = Vector2.zero;
		component5.sizeDelta = Vector2.zero;
		gameObject4.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
		Scrollbar verticalScrollbar = CreateHiddenScrollbar(col);
		ScrollRect scrollRect = gameObject3.AddComponent<ScrollRect>();
		scrollRect.content = component5;
		scrollRect.viewport = component4;
		scrollRect.vertical = true;
		scrollRect.horizontal = false;
		scrollRect.movementType = ScrollRect.MovementType.Clamped;
		scrollRect.scrollSensitivity = 40f;
		scrollRect.verticalScrollbar = verticalScrollbar;
		scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
	}

	private void BuildPreviewColumn(RectTransform col, float colW, float colH)
	{
		GameObject gameObject = new GameObject("PreviewImg", typeof(RectTransform));
		gameObject.transform.SetParent(col, worldPositionStays: false);
		RectTransform component = gameObject.GetComponent<RectTransform>();
		component.anchorMin = Vector2.zero;
		component.anchorMax = Vector2.one;
		component.offsetMin = Vector2.zero;
		component.offsetMax = Vector2.zero;
		_previewImg = gameObject.AddComponent<RawImage>();
		_previewImg.color = Color.white;
		_previewImg.raycastTarget = true;
		int num = 4;
		int width = Mathf.Max(64, Mathf.RoundToInt(colW) * num);
		int height = Mathf.Max(64, Mathf.RoundToInt(colH) * num);
		_rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
		_rt.antiAliasing = 4;
		_rt.filterMode = FilterMode.Trilinear;
		_previewImg.texture = _rt;
		_camGO = new GameObject("HC_CompanionPreviewCam");
		UnityEngine.Object.DontDestroyOnLoad(_camGO);
		_cam = _camGO.AddComponent<Camera>();
		_cam.targetTexture = _rt;
		_cam.clearFlags = CameraClearFlags.Color;
		_cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
		_cam.fieldOfView = 30f;
		_cam.nearClipPlane = 0.1f;
		_cam.farClipPlane = 10f;
		_cam.depth = -2f;
		_cam.enabled = false;
		int num2 = LayerMask.NameToLayer("character");
		if (num2 < 0)
		{
			num2 = 9;
		}
		int num3 = LayerMask.NameToLayer("character_net");
		int num4 = 1 << num2;
		if (num3 >= 0)
		{
			num4 |= 1 << num3;
		}
		_cam.cullingMask = num4;
	}

	private static GameObject CreateActionButton(GameObject template, Transform parent, string name, string label)
	{
		GameObject gameObject = CreateButton(template, parent, name, label);
		GameObject gameObject2 = new GameObject("Tint", typeof(RectTransform), typeof(Image));
		gameObject2.transform.SetParent(gameObject.transform, worldPositionStays: false);
		gameObject2.transform.SetAsFirstSibling();
		RectTransform component = gameObject2.GetComponent<RectTransform>();
		component.anchorMin = Vector2.zero;
		component.anchorMax = Vector2.one;
		component.offsetMin = Vector2.zero;
		component.offsetMax = Vector2.zero;
		gameObject2.GetComponent<Image>().color = BtnTint;
		gameObject2.GetComponent<Image>().raycastTarget = false;
		return gameObject;
	}

	private void OnBuyClicked()
	{
		if (StarterMode)
		{
			CompanionsPlugin.Log.LogInfo($"[Panel] Starter spawn confirmed — gender={_current.ModelIndex}, " + "hair=" + _current.HairItem + ", beard=" + _current.BeardItem);
			OnSpawnConfirmed?.Invoke(_current);
		}
		else
		{
			CompanionManager.Purchase(_current, _selectedTier);
			RefreshBankDisplay();
		}
	}

	private static Scrollbar CreateHiddenScrollbar(Transform parent)
	{
		float num = 10f;
		GameObject gameObject = new GameObject("Scrollbar", typeof(RectTransform));
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		RectTransform component = gameObject.GetComponent<RectTransform>();
		component.anchorMin = new Vector2(1f, 0f);
		component.anchorMax = new Vector2(1f, 1f);
		component.pivot = new Vector2(1f, 0.5f);
		component.sizeDelta = new Vector2(num, 0f);
		component.offsetMin = new Vector2(0f - num, 4f);
		component.offsetMax = new Vector2(-2f, -4f);
		gameObject.AddComponent<Image>().color = Color.clear;
		GameObject gameObject2 = new GameObject("Sliding Area", typeof(RectTransform));
		gameObject2.transform.SetParent(gameObject.transform, worldPositionStays: false);
		RectTransform component2 = gameObject2.GetComponent<RectTransform>();
		component2.anchorMin = Vector2.zero;
		component2.anchorMax = Vector2.one;
		component2.offsetMin = Vector2.zero;
		component2.offsetMax = Vector2.zero;
		GameObject gameObject3 = new GameObject("Handle", typeof(RectTransform), typeof(Image));
		gameObject3.transform.SetParent(gameObject2.transform, worldPositionStays: false);
		RectTransform component3 = gameObject3.GetComponent<RectTransform>();
		component3.anchorMin = Vector2.zero;
		component3.anchorMax = Vector2.one;
		component3.offsetMin = Vector2.zero;
		component3.offsetMax = Vector2.zero;
		gameObject3.GetComponent<Image>().color = Color.clear;
		Scrollbar scrollbar = gameObject.AddComponent<Scrollbar>();
		scrollbar.handleRect = component3;
		scrollbar.direction = Scrollbar.Direction.BottomToTop;
		scrollbar.targetGraphic = gameObject3.GetComponent<Image>();
		return scrollbar;
	}

	private static float PlacePlainLabel(Transform parent, TMP_FontAsset font, string text, float y, float h, string goName = null)
	{
		string name = (goName ?? text) + "Label";
		TextMeshProUGUI textMeshProUGUI = MakeText(parent, name, text, font, 14f, LabelText, TextAlignmentOptions.MidlineLeft);
		textMeshProUGUI.fontStyle = FontStyles.Bold;
		RectTransform component = textMeshProUGUI.GetComponent<RectTransform>();
		component.anchorMin = new Vector2(0f, 1f);
		component.anchorMax = new Vector2(1f, 1f);
		component.pivot = new Vector2(0f, 1f);
		component.sizeDelta = new Vector2(0f, h);
		component.anchoredPosition = new Vector2(4f, y);
		return y - h;
	}

	private static Transform MakeAnchoredRow(Transform parent, float y, float h)
	{
		GameObject gameObject = new GameObject("Row", typeof(RectTransform));
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		RectTransform component = gameObject.GetComponent<RectTransform>();
		component.anchorMin = new Vector2(0f, 1f);
		component.anchorMax = new Vector2(1f, 1f);
		component.pivot = new Vector2(0f, 1f);
		component.sizeDelta = new Vector2(0f, h);
		component.anchoredPosition = new Vector2(0f, y);
		return gameObject.transform;
	}

	private static Button AddToggleButton(Transform parent, string label, GameObject buttonTemplate, Vector2 anchorMin, Vector2 anchorMax, Action onClick, string displayLabel = null)
	{
		GameObject gameObject = CreateButton(buttonTemplate, parent, label, displayLabel ?? label);
		RectTransform component = gameObject.GetComponent<RectTransform>();
		component.anchorMin = anchorMin;
		component.anchorMax = anchorMax;
		component.offsetMin = Vector2.zero;
		component.offsetMax = Vector2.zero;
		Sprite genderBtnSprite = GetGenderBtnSprite();
		Image component2 = gameObject.GetComponent<Image>();
		if (component2 != null && genderBtnSprite != null)
		{
			component2.sprite = genderBtnSprite;
			component2.type = Image.Type.Simple;
			component2.preserveAspect = false;
			component2.color = Color.white;
		}
		GameObject gameObject2 = new GameObject("Tint", typeof(RectTransform), typeof(Image));
		gameObject2.transform.SetParent(gameObject.transform, worldPositionStays: false);
		gameObject2.transform.SetAsFirstSibling();
		RectTransform component3 = gameObject2.GetComponent<RectTransform>();
		component3.anchorMin = Vector2.zero;
		component3.anchorMax = Vector2.one;
		component3.offsetMin = Vector2.zero;
		component3.offsetMax = Vector2.zero;
		gameObject2.GetComponent<Image>().color = BtnTint;
		gameObject2.GetComponent<Image>().raycastTarget = false;
		TMP_Text componentInChildren = gameObject.GetComponentInChildren<TMP_Text>(includeInactive: true);
		if (componentInChildren != null)
		{
			componentInChildren.enableAutoSizing = false;
			componentInChildren.fontSize = 16f;
		}
		Button component4 = gameObject.GetComponent<Button>();
		if (component4 != null)
		{
			component4.transition = Selectable.Transition.None;
			component4.onClick.AddListener(delegate
			{
				onClick();
			});
		}
		return component4;
	}

	private static void BuildPickerRow(Transform parent, out TextMeshProUGUI nameLabel, string initialName, float y, float rowH, TMP_FontAsset font, GameObject buttonTemplate, Action onPrev, Action onNext)
	{
		GameObject gameObject = new GameObject("PickerRow", typeof(RectTransform), typeof(Image));
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		RectTransform component = gameObject.GetComponent<RectTransform>();
		component.anchorMin = new Vector2(0f, 1f);
		component.anchorMax = new Vector2(1f, 1f);
		component.pivot = new Vector2(0.5f, 1f);
		component.sizeDelta = new Vector2(-4f, rowH);
		component.anchoredPosition = new Vector2(0f, y);
		gameObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
		HorizontalLayoutGroup horizontalLayoutGroup = gameObject.AddComponent<HorizontalLayoutGroup>();
		horizontalLayoutGroup.spacing = 2f;
		horizontalLayoutGroup.padding = new RectOffset(2, 2, 2, 2);
		horizontalLayoutGroup.childAlignment = TextAnchor.MiddleCenter;
		horizontalLayoutGroup.childForceExpandWidth = false;
		horizontalLayoutGroup.childForceExpandHeight = true;
		horizontalLayoutGroup.childControlWidth = true;
		horizontalLayoutGroup.childControlHeight = true;
		Button button = CreateCategoryArrowButton(buttonTemplate, gameObject.transform, "Prev", "<", font);
		if (button != null)
		{
			LayoutElement layoutElement = button.gameObject.AddComponent<LayoutElement>();
			layoutElement.minWidth = rowH - 4f;
			layoutElement.preferredWidth = rowH - 4f;
			layoutElement.flexibleWidth = 0f;
			button.onClick.AddListener(delegate
			{
				onPrev();
			});
		}
		GameObject gameObject2 = new GameObject("Name", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
		gameObject2.transform.SetParent(gameObject.transform, worldPositionStays: false);
		gameObject2.GetComponent<LayoutElement>().flexibleWidth = 1f;
		nameLabel = gameObject2.GetComponent<TextMeshProUGUI>();
		nameLabel.text = initialName;
		nameLabel.fontSize = 16f;
		nameLabel.color = GoldTextColor;
		nameLabel.alignment = TextAlignmentOptions.Center;
		nameLabel.textWrappingMode = TextWrappingModes.NoWrap;
		nameLabel.overflowMode = TextOverflowModes.Ellipsis;
		nameLabel.raycastTarget = false;
		if (font != null)
		{
			nameLabel.font = font;
		}
		Button button2 = CreateCategoryArrowButton(buttonTemplate, gameObject.transform, "Next", ">", font);
		if (button2 != null)
		{
			LayoutElement layoutElement2 = button2.gameObject.AddComponent<LayoutElement>();
			layoutElement2.minWidth = rowH - 4f;
			layoutElement2.preferredWidth = rowH - 4f;
			layoutElement2.flexibleWidth = 0f;
			button2.onClick.AddListener(delegate
			{
				onNext();
			});
		}
	}

	private static Slider MakeSlider(Transform parent, string name, float y, float sliderH, float initialValue, Action<float> onChange)
	{
		Slider slider = (((object)InventoryGui.instance != null) ? null : null);
		GameObject gameObject;
		Slider slider2;
		if (slider != null)
		{
			gameObject = UnityEngine.Object.Instantiate(slider.gameObject, parent);
			gameObject.name = name;
			gameObject.SetActive(value: true);
			LayoutElement component = gameObject.GetComponent<LayoutElement>();
			if (component != null)
			{
				UnityEngine.Object.Destroy(component);
			}
			slider2 = gameObject.GetComponent<Slider>();
			if (slider2 == null)
			{
				CompanionsPlugin.Log.LogWarning("[CompanionPanel] Cloned split slider missing Slider component; using fallback slider.");
				UnityEngine.Object.Destroy(gameObject);
				slider2 = CreateFallbackSlider(parent, name);
				gameObject = ((slider2 != null) ? slider2.gameObject : null);
			}
		}
		else
		{
			CompanionsPlugin.Log.LogWarning("[CompanionPanel] m_splitSlider not available, using fallback slider.");
			slider2 = CreateFallbackSlider(parent, name);
			gameObject = ((slider2 != null) ? slider2.gameObject : null);
		}
		if (gameObject == null || slider2 == null)
		{
			return null;
		}
		RectTransform component2 = gameObject.GetComponent<RectTransform>();
		component2.anchorMin = new Vector2(0f, 1f);
		component2.anchorMax = new Vector2(1f, 1f);
		component2.pivot = new Vector2(0.5f, 1f);
		component2.sizeDelta = new Vector2(-4f, sliderH);
		component2.anchoredPosition = new Vector2(0f, y);
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
		slider2.onValueChanged.RemoveAllListeners();
		slider2.transition = Selectable.Transition.None;
		slider2.wholeNumbers = false;
		slider2.minValue = 0f;
		slider2.maxValue = 1f;
		Image[] componentsInChildren2 = gameObject.GetComponentsInChildren<Image>(includeInactive: true);
		foreach (Image image3 in componentsInChildren2)
		{
			if (!(image3 == null))
			{
				image3.color = new Color(1f, 1f, 1f, image3.color.a);
			}
		}
		if (slider2.fillRect != null)
		{
			Image component3 = slider2.fillRect.GetComponent<Image>();
			if (component3 != null)
			{
				component3.color = GoldColor;
			}
		}
		RectTransform rectTransform = ((slider2.fillRect != null) ? (slider2.fillRect.parent as RectTransform) : null);
		if (rectTransform == null)
		{
			rectTransform = gameObject.transform.Find("Background") as RectTransform;
		}
		if (rectTransform == null)
		{
			rectTransform = gameObject.GetComponent<RectTransform>();
		}
		GameObject gameObject2 = new GameObject("Tint", typeof(RectTransform), typeof(Image));
		gameObject2.transform.SetParent(rectTransform, worldPositionStays: false);
		gameObject2.transform.SetAsFirstSibling();
		RectTransform component4 = gameObject2.GetComponent<RectTransform>();
		component4.anchorMin = Vector2.zero;
		component4.anchorMax = Vector2.one;
		component4.offsetMin = Vector2.zero;
		component4.offsetMax = Vector2.zero;
		gameObject2.GetComponent<Image>().color = BtnTint;
		gameObject2.GetComponent<Image>().raycastTarget = false;
		slider2.value = initialValue;
		slider2.onValueChanged.AddListener(delegate(float v)
		{
			onChange(v);
		});
		return slider2;
	}

	private void UpdateHairColor()
	{
		float t = ((_hairHueSlider != null) ? _hairHueSlider.value : 0.3f);
		float t2 = ((_hairBrightSlider != null) ? _hairBrightSlider.value : 0.8f);
		Color c = Color.Lerp(HairBlonde, HairDark, t) * Mathf.Lerp(0.1f, 1f, t2);
		_current.HairColor = Utils.ColorToVec3(c);
		RefreshPreviewAppearance();
	}

	private void RefreshTypeButtons()
	{
		bool canWearArmor = _selectedTier.CanWearArmor;
		SetToggleActive(_companionTypeBtn, canWearArmor);
		SetToggleActive(_dvergerTypeBtn, !canWearArmor);
	}

	private void RefreshGenderButtons()
	{
		SetToggleActive(_maleBtn, _current.ModelIndex == 0);
		SetToggleActive(_femaleBtn, _current.ModelIndex == 1);
	}

	private void LoadHairBeardLists()
	{
		if (ObjectDB.instance == null)
		{
			return;
		}
		List<ItemDrop> allItems = ObjectDB.instance.GetAllItems(ItemDrop.ItemData.ItemType.Customization, "Hair");
		allItems.RemoveAll((ItemDrop x) => x.name.Contains("_"));
		allItems.Sort((ItemDrop a, ItemDrop b) => string.Compare(a.name, b.name, StringComparison.Ordinal));
		_hairs.Clear();
		foreach (ItemDrop item in allItems)
		{
			_hairs.Add(item.name);
		}
		if (_hairs.Count == 0)
		{
			_hairs.Add("Hair1");
		}
		List<ItemDrop> allItems2 = ObjectDB.instance.GetAllItems(ItemDrop.ItemData.ItemType.Customization, "Beard");
		allItems2.RemoveAll((ItemDrop x) => x.name.Contains("_"));
		allItems2.Sort((ItemDrop a, ItemDrop b) => string.Compare(a.name, b.name, StringComparison.Ordinal));
		_beards.Clear();
		_beards.Add("");
		foreach (ItemDrop item2 in allItems2)
		{
			_beards.Add(item2.name);
		}
		_hairIndex = Mathf.Max(0, _hairs.IndexOf(_current.HairItem));
		_beardIndex = Mathf.Max(0, _beards.IndexOf(_current.BeardItem));
		RefreshPickerLabels();
		_listsLoaded = true;
	}

	private void SetupPreviewClone()
	{
		ClearPreviewClone();
		string text = _selectedTier.SourcePrefab ?? "Player";
		GameObject gameObject = ZNetScene.instance?.GetPrefab(text);
		if (gameObject == null)
		{
			CompanionsPlugin.Log.LogWarning("[Panel] SetupPreviewClone — prefab \"" + text + "\" not found! tier=" + _selectedTier.DisplayName);
			return;
		}
		ZNetView.m_forceDisableInit = true;
		try
		{
			_clone = UnityEngine.Object.Instantiate(gameObject, PreviewPos, Quaternion.identity);
		}
		finally
		{
			ZNetView.m_forceDisableInit = false;
		}
		Rigidbody component = _clone.GetComponent<Rigidbody>();
		if (component != null)
		{
			UnityEngine.Object.Destroy(component);
		}
		MonoBehaviour[] componentsInChildren = _clone.GetComponentsInChildren<MonoBehaviour>(includeInactive: true);
		foreach (MonoBehaviour monoBehaviour in componentsInChildren)
		{
			if (!(monoBehaviour is VisEquipment))
			{
				monoBehaviour.enabled = false;
			}
		}
		Renderer[] componentsInChildren2 = _clone.GetComponentsInChildren<Renderer>(includeInactive: true);
		foreach (Renderer renderer in componentsInChildren2)
		{
			renderer.enabled = true;
		}
		Animator[] componentsInChildren3 = _clone.GetComponentsInChildren<Animator>();
		foreach (Animator anim in componentsInChildren3)
		{
			ConfigurePreviewAnimator(anim);
		}
		int num = LayerMask.NameToLayer("character");
		if (num < 0)
		{
			num = 9;
		}
		Transform[] componentsInChildren4 = _clone.GetComponentsInChildren<Transform>(includeInactive: true);
		foreach (Transform transform in componentsInChildren4)
		{
			transform.gameObject.layer = num;
		}
		SetupLightRig();
		Vector3 vector = PreviewPos + Vector3.up * 0.9f;
		_camGO.transform.position = vector + new Vector3(0f, 0.3f, 5f);
		_camGO.transform.LookAt(vector);
		VisEquipment component2 = _clone.GetComponent<VisEquipment>();
		Humanoid component3 = _clone.GetComponent<Humanoid>();
		int num2 = _clone.GetComponentsInChildren<Renderer>(includeInactive: true).Length;
		int valueOrDefault = (component3?.m_defaultItems?.Length).GetValueOrDefault();
		int valueOrDefault2 = (component3?.m_randomWeapon?.Length).GetValueOrDefault();
		CompanionsPlugin.Log.LogInfo("[Panel] SetupPreviewClone — tier=\"" + _selectedTier.DisplayName + "\" " + $"source=\"{text}\" visEquip={component2 != null} humanoid={component3 != null} " + $"renderers={num2} defaultItems={valueOrDefault} randomWeapons={valueOrDefault2} " + $"pos={_clone.transform.position:F1}");
		ApplyCurrentToClone();
	}

	private void RefreshBankDisplay()
	{
		if (StarterMode)
		{
			if (_buyButton != null)
			{
				_buyButton.interactable = true;
			}
			return;
		}
		int bankBalance = GetBankBalance();
		int price = CompanionTierData.Price;
		if (_coinText != null)
		{
			_coinText.text = ModLocalization.LocFmt("hc_ui_bank", bankBalance.ToString("N0"));
		}
		if (_buyButton != null)
		{
			_buyButton.interactable = bankBalance >= price;
		}
		if (_buyButtonLabel != null)
		{
			string text = CompanionTierData.LocalName(_selectedTier);
			_buyButtonLabel.text = ((bankBalance >= price) ? ModLocalization.LocFmt("hc_ui_btn_buy", text, price.ToString("N0")) : ModLocalization.LocFmt("hc_ui_btn_need", price.ToString("N0")));
		}
	}

	private void UpdatePreviewRotation()
	{
		if (_previewImg == null || !_previewImg.gameObject.activeInHierarchy)
		{
			return;
		}
		if (!_dragging && Input.GetMouseButtonDown(0) && RectTransformUtility.RectangleContainsScreenPoint(_previewImg.rectTransform, Input.mousePosition, null))
		{
			_dragging = true;
			_lastMouseX = Input.mousePosition.x;
		}
		if (_dragging)
		{
			if (Input.GetMouseButton(0))
			{
				float num = Input.mousePosition.x - _lastMouseX;
				_rotation = (_rotation + num * 0.4f) % 360f;
				_lastMouseX = Input.mousePosition.x;
			}
			else
			{
				_dragging = false;
			}
		}
		else
		{
			_rotation = (_rotation + 15f * Time.deltaTime) % 360f;
		}
	}

	private void UpdatePreviewCamera()
	{
		if (!(_camGO == null))
		{
			Vector3 vector = PreviewPos + Vector3.up * 0.9f;
			float f = _rotation * (Mathf.PI / 180f);
			Vector3 vector2 = new Vector3(Mathf.Sin(f), 0.3f, Mathf.Cos(f)) * 5f;
			_camGO.transform.position = vector + vector2;
			_camGO.transform.LookAt(vector);
		}
	}

	private void ClearPreviewClone()
	{
		if (_lightRig != null)
		{
			UnityEngine.Object.Destroy(_lightRig);
			_lightRig = null;
		}
		if (_clone != null)
		{
			UnityEngine.Object.Destroy(_clone);
			_clone = null;
		}
		_rotation = 0f;
		_dragging = false;
	}

	private static void ConfigurePreviewAnimator(Animator anim)
	{
		if (!(anim == null))
		{
			anim.updateMode = AnimatorUpdateMode.Normal;
			anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
			anim.applyRootMotion = false;
			anim.enabled = true;
			anim.speed = 1f;
			anim.Rebind();
			anim.Update(0f);
			SetBoolIfExists(anim, "emote_sit", value: false);
			SetBoolIfExists(anim, "attach_bed", value: false);
			SetBoolIfExists(anim, "sleeping", value: false);
			SetBoolIfExists(anim, "sit", value: false);
			SetBoolIfExists(anim, "sitting", value: false);
			SetBoolIfExists(anim, "crouch", value: false);
			SetBoolIfExists(anim, "inBed", value: false);
			SetBoolIfExists(anim, "in_bed", value: false);
			TriggerIfExists(anim, "emote_stop");
			TriggerIfExists(anim, "wakeup");
			TriggerIfExists(anim, "wake");
			TryPlayState(anim, "Idle");
			TryPlayState(anim, "idle");
			TryPlayState(anim, "Movement");
			TryPlayState(anim, "Move");
			TryPlayState(anim, "Base Layer.Idle");
			TryPlayState(anim, "Base Layer.Movement");
			for (int i = 0; i < 20; i++)
			{
				anim.Update(0.1f);
			}
			anim.speed = 0f;
		}
	}

	private void SetupLightRig()
	{
		if (_lightRig != null)
		{
			UnityEngine.Object.Destroy(_lightRig);
		}
		_lightRig = new GameObject("HC_PreviewLightRig");
		UnityEngine.Object.DontDestroyOnLoad(_lightRig);
		_lightRig.transform.position = PreviewPos;
		int num = LayerMask.NameToLayer("character");
		if (num < 0)
		{
			num = 9;
		}
		int num2 = LayerMask.NameToLayer("character_net");
		int num3 = 1 << num;
		if (num2 >= 0)
		{
			num3 |= 1 << num2;
		}
		AddLight(_lightRig.transform, "Key", new Vector3(1.5f, 2.5f, 3.5f), 2f, new Color(1f, 0.92f, 0.82f), 15f, num3);
		AddLight(_lightRig.transform, "Fill", new Vector3(-2.5f, 1.5f, 3f), 1.2f, new Color(0.9f, 0.92f, 1f), 15f, num3);
		AddLight(_lightRig.transform, "Rim", new Vector3(0f, 3f, -2.5f), 1.2f, new Color(0.95f, 0.88f, 0.78f), 15f, num3);
		AddLight(_lightRig.transform, "Bottom", new Vector3(0f, -0.5f, 3f), 0.5f, new Color(0.85f, 0.82f, 0.78f), 10f, num3);
	}

	private void ApplyCurrentToClone()
	{
		if (_clone == null)
		{
			return;
		}
		VisEquipment component = _clone.GetComponent<VisEquipment>();
		if (component == null)
		{
			return;
		}
		if (!_selectedTier.CanWearArmor)
		{
			Humanoid component2 = _clone.GetComponent<Humanoid>();
			if (component2 != null)
			{
				GameObject[] defaultItems = component2.m_defaultItems;
				int num = ((defaultItems != null) ? defaultItems.Length : 0);
				GameObject[] randomWeapon = component2.m_randomWeapon;
				int num2 = ((randomWeapon != null) ? randomWeapon.Length : 0);
				GameObject[] randomShield = component2.m_randomShield;
				int num3 = ((randomShield != null) ? randomShield.Length : 0);
				GameObject[] randomArmor = component2.m_randomArmor;
				int num4 = ((randomArmor != null) ? randomArmor.Length : 0);
				Humanoid.ItemSet[] randomSets = component2.m_randomSets;
				int num5 = ((randomSets != null) ? randomSets.Length : 0);
				CompanionsPlugin.Log.LogDebug("[Panel] ApplyCurrentToClone — dverger gear: " + $"defaults={num} weapons={num2} shields={num3} " + $"armor={num4} sets={num5}");
				ApplyDefaultGearToVis(component, component2.m_defaultItems);
				ApplyFirstItemToVis(component, component2.m_randomWeapon);
				ApplyFirstItemToVis(component, component2.m_randomShield);
				ApplyFirstItemToVis(component, component2.m_randomArmor);
				if (component2.m_randomSets != null)
				{
					Humanoid.ItemSet[] randomSets2 = component2.m_randomSets;
					foreach (Humanoid.ItemSet itemSet in randomSets2)
					{
						if (itemSet.m_items != null && itemSet.m_items.Length != 0)
						{
							ApplyDefaultGearToVis(component, itemSet.m_items);
							break;
						}
					}
				}
			}
			else
			{
				CompanionsPlugin.Log.LogWarning("[Panel] ApplyCurrentToClone — NO Humanoid on clone!");
			}
		}
		else
		{
			_currentModelIndex?.SetValue(component, -1);
			component.SetModel(_current.ModelIndex);
			component.SetHairItem((string.IsNullOrEmpty(_current.HairItem) ? "Hair1" : _current.HairItem).GetStableHashCode());
			component.SetBeardItem(((_current.ModelIndex == 0) ? (_current.BeardItem ?? "") : "").GetStableHashCode());
			component.SetSkinColor(_current.SkinColor);
			component.SetHairColor(_current.HairColor);
		}
		_updateVisuals?.Invoke(component, null);
		Animator componentInChildren = _clone.GetComponentInChildren<Animator>(includeInactive: true);
		if (componentInChildren != null)
		{
			componentInChildren.Update(0f);
			componentInChildren.speed = 0f;
		}
		int num6 = LayerMask.NameToLayer("character");
		if (num6 < 0)
		{
			num6 = 9;
		}
		Transform[] componentsInChildren = _clone.GetComponentsInChildren<Transform>(includeInactive: true);
		foreach (Transform transform in componentsInChildren)
		{
			transform.gameObject.layer = num6;
		}
	}

	private static void AddLight(Transform parent, string name, Vector3 pos, float intensity, Color color, float range, int mask)
	{
		GameObject gameObject = new GameObject(name);
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		gameObject.transform.localPosition = pos;
		Light light = gameObject.AddComponent<Light>();
		light.type = LightType.Point;
		light.intensity = intensity;
		light.color = color;
		light.range = range;
		light.shadows = LightShadows.None;
		light.cullingMask = mask;
	}

	private static void ApplyDefaultGearToVis(VisEquipment ve, GameObject[] items)
	{
		if (items == null)
		{
			return;
		}
		foreach (GameObject gameObject in items)
		{
			if (!(gameObject == null))
			{
				ApplyItemToVis(ve, gameObject);
			}
		}
	}

	private static void ApplyFirstItemToVis(VisEquipment ve, GameObject[] items)
	{
		if (items != null && items.Length != 0 && items[0] != null)
		{
			ApplyItemToVis(ve, items[0]);
		}
	}

	private static void ApplyItemToVis(VisEquipment ve, GameObject itemGo)
	{
		ItemDrop component = itemGo.GetComponent<ItemDrop>();
		if (!(component == null))
		{
			string name = itemGo.name;
			CompanionsPlugin.Log.LogDebug($"[Panel] ApplyItemToVis — \"{name}\" type={component.m_itemData.m_shared.m_itemType}");
			switch (component.m_itemData.m_shared.m_itemType)
			{
			case ItemDrop.ItemData.ItemType.OneHandedWeapon:
			case ItemDrop.ItemData.ItemType.Bow:
			case ItemDrop.ItemData.ItemType.TwoHandedWeapon:
			case ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft:
				ve.SetRightItem(name.GetStableHashCode(), 0);
				break;
			case ItemDrop.ItemData.ItemType.Shield:
				ve.SetLeftItem(name.GetStableHashCode(), 0, 0);
				break;
			case ItemDrop.ItemData.ItemType.Chest:
				ve.SetChestItem(name.GetStableHashCode());
				break;
			case ItemDrop.ItemData.ItemType.Legs:
				ve.SetLegItem(name.GetStableHashCode());
				break;
			case ItemDrop.ItemData.ItemType.Helmet:
				ve.SetHelmetItem(name.GetStableHashCode());
				break;
			case ItemDrop.ItemData.ItemType.Shoulder:
				ve.SetShoulderItem(name.GetStableHashCode(), 0, 0);
				break;
			case ItemDrop.ItemData.ItemType.Utility:
				ve.SetUtilityItem(name.GetStableHashCode());
				break;
			}
		}
	}

	private static void SetBoolIfExists(Animator anim, string name, bool value)
	{
		if (anim == null || string.IsNullOrEmpty(name))
		{
			return;
		}
		AnimatorControllerParameter[] parameters = anim.parameters;
		foreach (AnimatorControllerParameter animatorControllerParameter in parameters)
		{
			if (animatorControllerParameter.type == AnimatorControllerParameterType.Bool && string.Equals(animatorControllerParameter.name, name, StringComparison.Ordinal))
			{
				anim.SetBool(name, value);
				break;
			}
		}
	}

	private static void TriggerIfExists(Animator anim, string name)
	{
		if (anim == null || string.IsNullOrEmpty(name))
		{
			return;
		}
		AnimatorControllerParameter[] parameters = anim.parameters;
		foreach (AnimatorControllerParameter animatorControllerParameter in parameters)
		{
			if (animatorControllerParameter.type == AnimatorControllerParameterType.Trigger && string.Equals(animatorControllerParameter.name, name, StringComparison.Ordinal))
			{
				anim.SetTrigger(name);
				break;
			}
		}
	}

	private static void TryPlayState(Animator anim, string stateName)
	{
		if (!(anim == null) && !string.IsNullOrEmpty(stateName))
		{
			int num = Animator.StringToHash(stateName);
			if (anim.HasState(0, num))
			{
				anim.Play(num, 0, 0f);
			}
		}
	}

	private void RefreshPreviewAppearance()
	{
		ApplyCurrentToClone();
	}

	private void RefreshPickerLabels()
	{
		if (_hairNameText != null)
		{
			_hairNameText.text = ((_hairs.Count > 0) ? _hairs[_hairIndex] : "—");
		}
		if (_beardNameText != null)
		{
			_beardNameText.text = ((_beardIndex == 0) ? ModLocalization.Loc("hc_ui_beard_none") : ((_beards.Count > _beardIndex) ? _beards[_beardIndex] : "—"));
		}
	}

	private void SetType(CompanionTierDef tier)
	{
		bool flag = !tier.CanWearArmor;
		if (flag)
		{
			if (_selectedDvergerVariant == null)
			{
				_selectedDvergerVariant = CompanionTierData.DvergerVariants[0];
			}
			_selectedTier = _selectedDvergerVariant;
		}
		else
		{
			_selectedTier = tier;
		}
		RefreshTypeButtons();
		RefreshBuyButtonLabel();
		if (_appearanceGroup != null)
		{
			_appearanceGroup.SetActive(!flag);
		}
		if (_dvergerGroup != null)
		{
			_dvergerGroup.SetActive(flag);
		}
		if (flag)
		{
			RefreshVariantButtons();
		}
		SetupPreviewClone();
	}

	private void RefreshBuyButtonLabel()
	{
		if (_buyButtonLabel != null)
		{
			_buyButtonLabel.text = ModLocalization.LocFmt("hc_ui_btn_buy", CompanionTierData.LocalName(_selectedTier), CompanionTierData.Price.ToString("N0"));
		}
	}

	private void RefreshVariantButtons()
	{
		CompanionTierDef[] dvergerVariants = CompanionTierData.DvergerVariants;
		for (int i = 0; i < _variantButtons.Count && i < dvergerVariants.Length; i++)
		{
			SetToggleActive(_variantButtons[i], dvergerVariants[i] == _selectedDvergerVariant);
		}
	}

	private void SetDvergerVariant(CompanionTierDef variant)
	{
		_selectedDvergerVariant = variant;
		_selectedTier = variant;
		RefreshVariantButtons();
		RefreshBuyButtonLabel();
		SetupPreviewClone();
	}

	private static void SetToggleActive(Button btn, bool on)
	{
		if (!(btn == null))
		{
			Image component = btn.GetComponent<Image>();
			if (component != null)
			{
				component.color = (on ? GoldColor : Color.white);
			}
			TMP_Text componentInChildren = btn.GetComponentInChildren<TMP_Text>(includeInactive: true);
			if (componentInChildren != null)
			{
				componentInChildren.color = GoldTextColor;
			}
		}
	}

	private void SetGender(int model)
	{
		_current.ModelIndex = model;
		if (model == 1)
		{
			_current.BeardItem = "";
			_beardIndex = 0;
		}
		if (_beardGroup != null)
		{
			_beardGroup.SetActive(model == 0);
		}
		RefreshGenderButtons();
		RefreshPickerLabels();
		RefreshPreviewAppearance();
	}

	private void CycleHair(int dir)
	{
		if (_hairs.Count != 0)
		{
			_hairIndex = (_hairIndex + dir + _hairs.Count) % _hairs.Count;
			_current.HairItem = _hairs[_hairIndex];
			RefreshPickerLabels();
			RefreshPreviewAppearance();
		}
	}

	private void CycleBeard(int dir)
	{
		if (_beards.Count != 0)
		{
			_beardIndex = (_beardIndex + dir + _beards.Count) % _beards.Count;
			_current.BeardItem = _beards[_beardIndex];
			RefreshPickerLabels();
			RefreshPreviewAppearance();
		}
	}

	private static int GetBankBalance()
	{
		Player localPlayer = Player.m_localPlayer;
		if (localPlayer == null)
		{
			return 0;
		}
		if (localPlayer.m_customData.TryGetValue("TraderSharedBank_Balance", out var value) && int.TryParse(value, out var result))
		{
			return result;
		}
		return 0;
	}

	private static GameObject CreateButton(GameObject template, Transform parent, string name, string label)
	{
		GameObject gameObject = UnityEngine.Object.Instantiate(template, parent);
		gameObject.name = name;
		gameObject.SetActive(value: true);
		Button component = gameObject.GetComponent<Button>();
		if (component != null)
		{
			component.onClick.RemoveAllListeners();
			component.navigation = new Navigation
			{
				mode = Navigation.Mode.None
			};
		}
		TMP_Text componentInChildren = gameObject.GetComponentInChildren<TMP_Text>(includeInactive: true);
		if (componentInChildren != null)
		{
			componentInChildren.gameObject.SetActive(value: true);
			componentInChildren.text = label;
		}
		StripButtonHints(gameObject, componentInChildren);
		return gameObject;
	}

	private static void StripButtonHints(GameObject btnGO, TMP_Text label)
	{
		Animator component = btnGO.GetComponent<Animator>();
		if (component != null)
		{
			UnityEngine.Object.Destroy(component);
		}
		ContentSizeFitter component2 = btnGO.GetComponent<ContentSizeFitter>();
		if (component2 != null)
		{
			UnityEngine.Object.Destroy(component2);
		}
		LayoutElement component3 = btnGO.GetComponent<LayoutElement>();
		if (component3 != null)
		{
			UnityEngine.Object.Destroy(component3);
		}
		for (int num = btnGO.transform.childCount - 1; num >= 0; num--)
		{
			Transform child = btnGO.transform.GetChild(num);
			if (!(label != null) || (!(child.gameObject == label.gameObject) && !label.transform.IsChildOf(child)))
			{
				UnityEngine.Object.Destroy(child.gameObject);
			}
		}
	}

	private static Button CreateCategoryArrowButton(GameObject buttonTemplate, Transform parent, string name, string text, TMP_FontAsset font)
	{
		if (buttonTemplate == null)
		{
			return null;
		}
		GameObject gameObject = UnityEngine.Object.Instantiate(buttonTemplate, parent);
		gameObject.name = name;
		gameObject.SetActive(value: true);
		TMP_Text componentInChildren = gameObject.GetComponentInChildren<TMP_Text>(includeInactive: true);
		for (int num = gameObject.transform.childCount - 1; num >= 0; num--)
		{
			Transform child = gameObject.transform.GetChild(num);
			if (!(componentInChildren != null) || (!(child.gameObject == componentInChildren.gameObject) && !componentInChildren.transform.IsChildOf(child)))
			{
				UnityEngine.Object.Destroy(child.gameObject);
			}
		}
		Animator component = gameObject.GetComponent<Animator>();
		if (component != null)
		{
			UnityEngine.Object.Destroy(component);
		}
		ContentSizeFitter component2 = gameObject.GetComponent<ContentSizeFitter>();
		if (component2 != null)
		{
			UnityEngine.Object.Destroy(component2);
		}
		LayoutElement component3 = gameObject.GetComponent<LayoutElement>();
		if (component3 != null)
		{
			UnityEngine.Object.Destroy(component3);
		}
		Sprite catBtnSprite = GetCatBtnSprite();
		Image image = gameObject.GetComponent<Image>();
		if (image == null)
		{
			image = gameObject.AddComponent<Image>();
		}
		if (image != null)
		{
			if (catBtnSprite != null)
			{
				image.sprite = catBtnSprite;
				image.type = Image.Type.Simple;
				image.preserveAspect = false;
			}
			image.color = Color.white;
			image.raycastTarget = true;
		}
		Button component4 = gameObject.GetComponent<Button>();
		if (component4 != null)
		{
			component4.onClick.RemoveAllListeners();
			component4.transition = Selectable.Transition.None;
			component4.navigation = new Navigation
			{
				mode = Navigation.Mode.None
			};
			if (image != null)
			{
				component4.targetGraphic = image;
			}
		}
		GameObject gameObject2 = new GameObject("Tint", typeof(RectTransform), typeof(Image));
		gameObject2.transform.SetParent(gameObject.transform, worldPositionStays: false);
		gameObject2.transform.SetAsFirstSibling();
		RectTransform component5 = gameObject2.GetComponent<RectTransform>();
		component5.anchorMin = Vector2.zero;
		component5.anchorMax = Vector2.one;
		component5.offsetMin = Vector2.zero;
		component5.offsetMax = Vector2.zero;
		gameObject2.GetComponent<Image>().color = BtnTint;
		gameObject2.GetComponent<Image>().raycastTarget = false;
		if (componentInChildren != null)
		{
			componentInChildren.gameObject.SetActive(value: true);
			componentInChildren.text = text;
			componentInChildren.richText = false;
			componentInChildren.fontSize = 22f;
			componentInChildren.color = GoldTextColor;
			componentInChildren.alignment = TextAlignmentOptions.Center;
			componentInChildren.raycastTarget = false;
			if (font != null)
			{
				componentInChildren.font = font;
			}
		}
		return component4;
	}

	private static TextMeshProUGUI MakeText(Transform parent, string name, string text, TMP_FontAsset font, float size, Color color, TextAlignmentOptions align)
	{
		GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		TextMeshProUGUI component = gameObject.GetComponent<TextMeshProUGUI>();
		component.text = text;
		component.fontSize = size;
		component.color = color;
		component.alignment = align;
		component.textWrappingMode = TextWrappingModes.NoWrap;
		component.overflowMode = TextOverflowModes.Ellipsis;
		component.raycastTarget = false;
		if (font != null)
		{
			component.font = font;
		}
		return component;
	}

	private static Slider CreateFallbackSlider(Transform parent, string name)
	{
		GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Slider));
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		gameObject.SetActive(value: true);
		Image component = gameObject.GetComponent<Image>();
		if (component != null)
		{
			component.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);
			component.raycastTarget = true;
		}
		GameObject gameObject2 = new GameObject("Fill Area", typeof(RectTransform));
		gameObject2.transform.SetParent(gameObject.transform, worldPositionStays: false);
		RectTransform component2 = gameObject2.GetComponent<RectTransform>();
		component2.anchorMin = Vector2.zero;
		component2.anchorMax = Vector2.one;
		component2.offsetMin = new Vector2(8f, 4f);
		component2.offsetMax = new Vector2(-8f, -4f);
		GameObject gameObject3 = new GameObject("Fill", typeof(RectTransform), typeof(Image));
		gameObject3.transform.SetParent(gameObject2.transform, worldPositionStays: false);
		RectTransform component3 = gameObject3.GetComponent<RectTransform>();
		component3.anchorMin = new Vector2(0f, 0f);
		component3.anchorMax = new Vector2(1f, 1f);
		component3.pivot = new Vector2(0f, 0.5f);
		component3.offsetMin = Vector2.zero;
		component3.offsetMax = Vector2.zero;
		Image component4 = gameObject3.GetComponent<Image>();
		component4.color = GoldColor;
		component4.raycastTarget = false;
		GameObject gameObject4 = new GameObject("Handle Slide Area", typeof(RectTransform));
		gameObject4.transform.SetParent(gameObject.transform, worldPositionStays: false);
		RectTransform component5 = gameObject4.GetComponent<RectTransform>();
		component5.anchorMin = Vector2.zero;
		component5.anchorMax = Vector2.one;
		component5.offsetMin = new Vector2(8f, 0f);
		component5.offsetMax = new Vector2(-8f, 0f);
		GameObject gameObject5 = new GameObject("Handle", typeof(RectTransform), typeof(Image));
		gameObject5.transform.SetParent(gameObject4.transform, worldPositionStays: false);
		RectTransform component6 = gameObject5.GetComponent<RectTransform>();
		component6.anchorMin = new Vector2(0.5f, 0.5f);
		component6.anchorMax = new Vector2(0.5f, 0.5f);
		component6.sizeDelta = new Vector2(16f, 16f);
		component6.anchoredPosition = Vector2.zero;
		Image component7 = gameObject5.GetComponent<Image>();
		component7.color = Color.white;
		component7.raycastTarget = true;
		Slider component8 = gameObject.GetComponent<Slider>();
		component8.fillRect = component3;
		component8.handleRect = component6;
		component8.targetGraphic = component7;
		component8.direction = Slider.Direction.LeftToRight;
		return component8;
	}
}
