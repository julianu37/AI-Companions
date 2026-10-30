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

	// Fase 3.20: the cached sprites (built once, released by DestroySprite)
	private static Sprite _catBtnSprite;

	private static Sprite _sliderBgSprite;

	private static Sprite _genderBtnSprite;

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

	/// <summary>Fase 3.19: when the bank line was last rebuilt (see UpdatePerFrame - twice a second, not every frame).</summary>
	private float _bankRefreshAt;

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

	/// <summary>
	/// Fase 3.21 (bug dos sliders): the colours the companion already had when the editor opened, kept aside because
	/// <see cref="BuildCustomColumn"/> ends by writing the starter's own defaults into <see cref="_current"/> (its
	/// sliders are born on those values, which is right for the starter). In edit mode the stored colours are the truth,
	/// and <see cref="ApplyAppearanceToControls"/> puts them back before moving the sliders - without this the editor
	/// reopened showing the default colours (the sliders "went back to the start") and the stored ones were lost.
	/// </summary>
	private Vector3 _storedSkinColor;

	private Vector3 _storedHairColor;

	/// <summary>The last (hair, beard) pair logged by the preview diagnostic, so a slider drag logs once per item.</summary>
	private string _lastPreviewHairLog;

	/// <summary>The haircut/beard hashes last applied to the preview clone: the game's attach cache is only cleared when
	/// one of them really changed (see <see cref="ApplyCurrentToClone"/>).</summary>
	private int _lastPreviewHairHash = int.MinValue;

	private int _lastPreviewBeardHash = int.MinValue;

	private GameObject _dvergerGroup;

	private CompanionTierDef _selectedDvergerVariant;

	private readonly List<Button> _variantButtons = new List<Button>();

	internal bool StarterMode;

	// 1.5.71: the same one-companion panel, for hiring another at Odin's Shrine (paid in coins)
	internal bool HireMode;

	internal Action<CompanionAppearance> OnSpawnConfirmed;

	// ------------------------------------------------------------------ the appearance editor (a companion that exists)
	//
	// The same panel in a second mode: the left column (gender, hair, beard, colours) and the 3D preview are the very
	// ones of the starter - there is only one implementation of them - and the middle column is replaced by a short
	// line saying whose look is being edited and the two buttons ("Aplicar" / "Cancelar").
	//
	// Nothing here touches the companion. The appearance being edited is a COPY read from the target's ZDO
	// (ReadTargetAppearance), the preview is the visual clone (never the real object), and writing anything belongs to
	// the host (UI/CompanionAppearancePanel.cs): the panel only says "this is what the player built".

	/// <summary>True while the panel edits the look of a companion that already exists (no AI section, no "Invocar").</summary>
	internal bool AppearanceEditMode;

	/// <summary>The companion being edited (AppearanceEditMode only). Only read here - its ZDO is written by the host.</summary>
	internal CompanionSetup EditTarget;

	/// <summary>"Aplicar": the appearance built in the panel. Applying it (and writing the ZDO) belongs to the host.</summary>
	internal Action<CompanionAppearance> OnAppearanceConfirmed;

	/// <summary>"Cancelar": the player gave up. Nothing was written, and the host closes the window.</summary>
	internal Action OnAppearanceCancelled;

	private GameObject _bankDisplayGO;

	// ------------------------------------------------------------------ Fase 3.10: the AI section of the starter panel
	//
	// The voice and the personality of the companion that is about to be born. They cannot be written anywhere yet
	// (the companion does not exist while this panel is open), so they are kept here and handed to the spawn as a
	// CompanionAiDraft - see AiDraft(). The first entry of both pickers is "follow the [IA] default", which writes
	// nothing: the companion then follows the global values, including any change made later.

	private const float AiRowH = 32f;

	private const float AiGap = 4f;

	// Middle column geometry (Fase 3.12), all measured from the bottom of the column. The AI section and the
	// description are stacked from there, so the description can only be as tall as what the three bands leave - and
	// the numbers are logged at build time (LogMiddleColumnLayout) instead of being guessed from a screenshot.
	private const float MiddleBuyBottom = 8f;

	/// <summary>Room between "Invocar" and the AI section.</summary>
	private const float MiddleSectionGap = 12f;

	/// <summary>Room between the AI section and the description scroll view.</summary>
	private const float MiddleDescGap = 12f;

	/// <summary>Room kept at the top of the column for the panel title ("Escolha seu Companheiro").</summary>
	private const float MiddleDescTop = 38f;

	/// <summary>Height of the name band of the starter (its label, the 2px gap and the field itself).</summary>
	private const float MiddleNameH = 18f + 2f + AiRowH;

	private TextMeshProUGUI _aiVoiceValue;

	private TextMeshProUGUI _aiPresetValue;

	private TMP_InputField _aiFreeText;

	private int _aiVoiceIndex;

	private int _aiPresetIndex;

	private int[] _aiVoiceIds;

	private string[] _aiVoiceLabels;

	private string[] _aiPresetIds;

	private string[] _aiPresetLabels;

	// ------------------------------------------------------------------ Fase 3.13: the name of the first companion

	/// <summary>The name field of the starter panel: the player's own name for the companion that is about to be born.</summary>
	private TMP_InputField _nameField;

	/// <summary>The default the field currently carries (the localized name of the chosen tier).</summary>
	private string _nameDefault = "";

	/// <summary>
	/// Has the player typed something that is not the default? While it is false the field follows the chosen tier
	/// ("Companheiro", "Guerreiro"...); once it is true the player's text is kept, even if the tier changes. Clearing
	/// the field (empty/only spaces) puts it back to following the default.
	/// </summary>
	private bool _nameEdited;

	/// <summary>Fase 3.12: "Ouvir" of the AI section, kept so its geometry can be written to the log (and so a future
	/// change can enable/disable it without hunting for it).</summary>
	private Button _aiListenButton;

	/// <summary>Where the "Ouvir" button sits, measured from the panel's bottom (filled by BuildAiSection).</summary>
	private float _aiListenBottom;

	/// <summary>The height of the AI section, as returned to BuildMiddleColumn (filled by BuildAiSection).</summary>
	private float _aiHeight;

	// The panel geometry the middle column lays itself out with (set by Build, from the arguments the caller passed):
	// the layout is computed by hand here, so these are the numbers that have to be logged and reasoned about. The
	// values below are only placeholders until Build runs (it always runs before anything reads them).
	private float _panelHeight = 660f;

	private float _columnHeight = 648f;

	private float _middleWidth = 299f;

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

	// 1.5.81: the summon and starter windows have no dark layers over the game's wood panel - not on the three sections,
	// the buttons or the slider tracks (user: "there is like a dark highlight overlayed the 3 sections"; "Lets remove it
	// from the extras too"). The trader tab keeps them. Set at the start of each Build.
	private static bool s_noDark;

	private static Color SectionBg => s_noDark ? Color.clear : ColBg;

	private static Color Tint => s_noDark ? Color.clear : BtnTint;

	private static readonly Color TabInactive = new Color(0.45f, 0.45f, 0.45f, 1f);

	private const string BankDataKey = "TraderSharedBank_Balance";

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
	}

	public void Build(Transform parent, float colTopInset, float bottomPad, TMP_FontAsset font, GameObject buttonTemplate, float buttonHeight, float leftXL, float leftXR, float midXL, float midXR, float rightXL, float rightXR, float panelHeight)
	{
		// the editor starts from the look the companion already has (its HC_Appearance), never from Default(): the panel
		// has to show what is there before the player changes it. The tier is only used for the preview prefab and for
		// whether the appearance even applies (a dverger's look is its prefab), so it is read from the target too.
		_current = AppearanceEditMode ? ReadTargetAppearance() : CompanionAppearance.Default();
		_selectedTier = (AppearanceEditMode ? TierOfTarget() : null) ?? CompanionTierData.Companion;
		// Fase 3.21: the colours the companion already has, kept before anything touches _current (see the fields below)
		_storedSkinColor = _current.SkinColor;
		_storedHairColor = _current.HairColor;
		if (buttonHeight <= 0f)
		{
			buttonHeight = 30f;
		}
		// Fase 3.12 (bug de layout): the numbers the middle column lays itself out with, kept so the geometry can be
		// written in one line to the log at build time - see LogMiddleColumnLayout. Nothing reads the RectTransform
		// here on purpose: at build time the layout has not run yet, and the panel's own measurements are the ones
		// that matter.
		_panelHeight = panelHeight;
		_columnHeight = panelHeight - colTopInset - bottomPad;
		_middleWidth = midXR - midXL;
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
		if (AppearanceEditMode)
		{
			BuildEditColumn(col2, font, buttonTemplate, buttonHeight);
		}
		else
		{
			BuildMiddleColumn(col2, font, buttonTemplate, buttonHeight);
		}
		BuildPreviewColumn(col3, colW, colH);
		if (AppearanceEditMode)
		{
			// BuildCustomColumn leaves the controls on the starter's defaults (its sliders are born there): the stored
			// appearance goes back on them - sliders included - before the first frame of the editor
			ApplyAppearanceToControls();
		}
		Root.SetActive(value: false);
	}

	public void Refresh()
	{
		if (!_listsLoaded)
		{
			LoadHairBeardLists();
		}
		SetupPreviewClone();
		if (!AppearanceEditMode)
		{
			RefreshBankDisplay();
		}
	}

	public void UpdatePerFrame()
	{
		UpdatePreviewRotation();
		UpdatePreviewCamera();
		// Fase 3.19 (auditoria): the bank line was rebuilt every frame (a balance lookup plus two formatted strings,
		// 60x a second). Twice a second is plenty - and the tier click repaints the button itself (SetType ->
		// RefreshBuyButtonLabel). The appearance editor has no bank line at all (it edits a companion that is already
		// paid for), so the tick costs nothing there.
		float now = Time.unscaledTime;
		if (!AppearanceEditMode && now - _bankRefreshAt >= 0.5f)
		{
			_bankRefreshAt = now;
			RefreshBankDisplay();
		}
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

	/// <summary>
	/// Releases one of the panel's own sprites. The <b>texture is not ours</b>: it comes from
	/// <see cref="TextureLoader.LoadUITexture"/>, which caches it by name and hands the very same one to every panel of
	/// the mod - destroying it here would leave a destroyed texture in that cache for whoever asks next. That used to be
	/// invisible (the panel was built once and torn down at the end of the session), but the appearance editor
	/// (Fase 3.20) opens and closes this panel whenever the player wants, one right after the other - so only the sprite
	/// goes, which is what <c>CompanionAiPanel</c> already does for the same reason.
	/// </summary>
	private static void DestroySprite(ref Sprite sprite)
	{
		if (sprite != null)
		{
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
		component2.color = SectionBg;
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
			Button item = AddToggleButton(parent2, v.LocalName, buttonTemplate, new Vector2(0.01f, 0f), new Vector2(0.99f, 1f), delegate
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
		// Fase 3.21 (bug dos sliders): writing _current from the sliders that were just born is the starter's job only.
		// In the editor it threw away the stored colours (they were read from the ZDO at the top of Build), so the
		// "nearest point" of the next step was always the default one and the panel reopened on the start positions.
		if (!AppearanceEditMode)
		{
			float t = ((_skinSlider != null) ? _skinSlider.value : 0.05f);
			_current.SkinColor = Utils.ColorToVec3(Color.Lerp(SkinLight, SkinDark, t));
			UpdateHairColor();
		}
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
		textMeshProUGUI.text = (HireMode ? ModLocalization.Loc("hc_ui_title_hire") : StarterMode ? ModLocalization.Loc("hc_ui_title_choose") : ModLocalization.Loc("hc_ui_title_companions"));
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
			component3.anchoredPosition = new Vector2(0f, MiddleBuyBottom);
		}
		// Fase 3.10/3.13: the bands of the middle column, from the bottom up: "Invocar", the AI section (starter only),
		// the name of the companion (starter only) and the description scroll view, which gets whatever is left between
		// the name band and the title. The heights the builders report are what reserve their space - nothing overlaps,
		// and the numbers are written to the log below so the account can be checked without opening the game.
		float buyTop = MiddleBuyBottom + (buttonTemplate != null ? btnH : 0f);
		float aiBottom = buyTop + MiddleSectionGap;
		_aiHeight = 0f;
		if (StarterMode && buttonTemplate != null)
		{
			_aiHeight = BuildAiSection(col.transform, font, buttonTemplate, aiBottom);
		}
		float aiTop = aiBottom + _aiHeight;
		float nameBottom = aiTop + MiddleDescGap;
		float nameHeight = 0f;
		if (StarterMode && buttonTemplate != null)
		{
			nameHeight = BuildNameField(col.transform, font, nameBottom);
		}
		float descBottom = (nameHeight > 0f) ? (nameBottom + nameHeight + MiddleDescGap) : (_aiHeight > 0f ? (aiTop + MiddleDescGap) : y);
		GameObject gameObject3 = new GameObject("DescScrollArea", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
		gameObject3.transform.SetParent(col, worldPositionStays: false);
		RectTransform component4 = gameObject3.GetComponent<RectTransform>();
		component4.anchorMin = Vector2.zero;
		component4.anchorMax = Vector2.one;
		component4.offsetMin = new Vector2(8f, descBottom);
		component4.offsetMax = new Vector2(-14f, 0f - MiddleDescTop);
		gameObject3.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.003f);
		// RectMask2D, not Mask (Fase 3.12 - the reported bug): the description text was drawing its last lines OUTSIDE
		// the viewport, over the top of the AI section (the "two overlapping texts" of the screenshot). A Mask clips
		// through a stencil written by the Image's material; RectMask2D clips by rectangle, with no stencil and no
		// material involved, so text longer than the viewport can only ever be scrolled - it is the same component the
		// mod's own text boxes use for the same reason.
		gameObject3.GetComponent<RectMask2D>().padding = Vector4.zero;
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
		textMeshProUGUI2.text = (HireMode ? ModLocalization.LocFmt("hc_ui_desc_hire", CompanionTierData.Price.ToString("N0")) : StarterMode ? ModLocalization.Loc("hc_ui_desc_starter") : ModLocalization.Loc("hc_ui_desc_trader"));
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
		if (StarterMode)
		{
			LogMiddleColumnLayout(btnH, buyTop, aiBottom, aiTop, nameBottom, nameHeight, descBottom);
		}
	}

	// ------------------------------------------------------------------ the middle column of the appearance editor

	/// <summary>Room between "Aplicar" and "Cancelar" of the editor.</summary>
	private const float EditButtonGap = 8f;

	/// <summary>How far below the title the "Editando a aparência de ..." line starts, and how tall it may grow before it
	/// would run into the buttons (the log says how much room is left between the two).</summary>
	private const float EditTextTop = 44f;

	private const float EditTextH = 72f;

	/// <summary>
	/// The middle column of the appearance editor: the title, one line saying whose look is being edited and the two
	/// buttons, nothing else. It is much shorter than the starter's column on purpose - there is no AI section, no name
	/// field and no description, because nothing but the look is touched here (the tier, the name, the AI profile, the
	/// inventory, the equipment and the position all stay as they are).
	///
	/// <para>The buttons are built exactly like the starter's "Invocar" (an explicit rect anchored to both sides of the
	/// column), so their size comes from the column and not from a helper row - the same lesson as the AI section's
	/// "Ouvir". The numbers are written to the log once (see <see cref="LogEditColumnLayout"/>).</para>
	/// </summary>
	private void BuildEditColumn(RectTransform col, TMP_FontAsset font, GameObject buttonTemplate, float btnH)
	{
		GameObject titleGO = new GameObject("Title", typeof(RectTransform));
		titleGO.transform.SetParent(col, worldPositionStays: false);
		TextMeshProUGUI title = titleGO.AddComponent<TextMeshProUGUI>();
		if (font != null)
		{
			title.font = font;
		}
		title.fontSize = 24f;
		title.color = Color.white;
		title.alignment = TextAlignmentOptions.Center;
		title.text = ModLocalization.Loc("hc_ui_title_appearance");
		title.raycastTarget = false;
		RectTransform titleRT = titleGO.GetComponent<RectTransform>();
		titleRT.anchorMin = new Vector2(0f, 1f);
		titleRT.anchorMax = new Vector2(1f, 1f);
		titleRT.pivot = new Vector2(0.5f, 1f);
		titleRT.sizeDelta = new Vector2(-20f, 32f);
		titleRT.anchoredPosition = new Vector2(0f, -4f);

		// "Editando a aparência de X" - the one thing the player must be able to read with two companions around
		TextMeshProUGUI who = MakeText(col, "EditingFor", ModLocalization.LocFmt("hc_ui_editing_appearance", TargetName()), font,
			17f, LabelText, TextAlignmentOptions.Top);
		who.textWrappingMode = TextWrappingModes.Normal;
		who.overflowMode = TextOverflowModes.Overflow;
		RectTransform whoRT = who.GetComponent<RectTransform>();
		whoRT.anchorMin = new Vector2(0f, 1f);
		whoRT.anchorMax = new Vector2(1f, 1f);
		whoRT.pivot = new Vector2(0.5f, 1f);
		whoRT.sizeDelta = new Vector2(-26f, EditTextH);
		whoRT.anchoredPosition = new Vector2(0f, 0f - EditTextTop);

		float applyBottom = MiddleBuyBottom;
		float cancelBottom = applyBottom + btnH + EditButtonGap;
		if (buttonTemplate != null)
		{
			GameObject apply = CreateActionButton(buttonTemplate, col, "AppearanceApply", ModLocalization.Loc("hc_ui_btn_apply"));
			RectTransform applyRT = apply.GetComponent<RectTransform>();
			applyRT.anchorMin = new Vector2(0f, 0f);
			applyRT.anchorMax = new Vector2(1f, 0f);
			applyRT.pivot = new Vector2(0.5f, 0f);
			applyRT.sizeDelta = new Vector2(-24f, btnH);
			applyRT.anchoredPosition = new Vector2(0f, applyBottom);
			Button applyBtn = apply.GetComponent<Button>();
			if (applyBtn != null)
			{
				applyBtn.onClick.AddListener(delegate
				{
					OnApplyClicked();
				});
			}

			GameObject cancel = CreateActionButton(buttonTemplate, col, "AppearanceCancel", ModLocalization.Loc("hc_ui_btn_cancel"));
			RectTransform cancelRT = cancel.GetComponent<RectTransform>();
			cancelRT.anchorMin = new Vector2(0f, 0f);
			cancelRT.anchorMax = new Vector2(1f, 0f);
			cancelRT.pivot = new Vector2(0.5f, 0f);
			cancelRT.sizeDelta = new Vector2(-24f, btnH);
			cancelRT.anchoredPosition = new Vector2(0f, cancelBottom);
			Button cancelBtn = cancel.GetComponent<Button>();
			if (cancelBtn != null)
			{
				cancelBtn.onClick.AddListener(delegate
				{
					OnCancelClicked();
				});
			}
		}
		else
		{
			CompanionsPlugin.Log?.LogWarning("[AppearancePanel] no button template - the editor has no Aplicar/Cancelar (Esc still closes it)");
		}
		LogEditColumnLayout(btnH, applyBottom, cancelBottom);
	}

	/// <summary>
	/// One line with the geometry of the editor's middle column, written once when the panel is built - the same idea as
	/// <see cref="LogMiddleColumnLayout"/> for the starter: the panel, the column, the two buttons and the room the text
	/// band gets. Every number is measured from the bottom of the column except the widths and the panel height, and
	/// they are the very numbers the rects above were built with.
	/// </summary>
	private void LogEditColumnLayout(float btnH, float applyBottom, float cancelBottom)
	{
		// the text band is anchored to the top of the column and the buttons to its bottom: the free band between them is
		// what a longer line ("Editando a aparência de ...") would have before it would have to be shortened
		float textBottom = EditTextTop + EditTextH;
		float cancelTop = cancelBottom + btnH;
		float free = Mathf.Max(0f, _columnHeight - textBottom - cancelTop);
		CompanionsPlugin.Log?.LogInfo($"[AppearancePanel] Layout da coluna do meio: painel={_panelHeight:0} coluna={_columnHeight:0} largura={_middleWidth:0} | "
			+ $"alvo=\"{TargetName()}\" | Aplicar: base={applyBottom:0} altura={btnH:0} topo={applyBottom + btnH:0} | "
			+ $"Cancelar: base={cancelBottom:0} topo={cancelTop:0} | texto: topo={EditTextTop:0} altura={EditTextH:0} (do topo) | faixa livre={free:0}");
	}

	/// <summary>"Aplicar": hands the appearance being edited to the host (which applies it and writes the ZDO).</summary>
	private void OnApplyClicked()
	{
		CompanionsPlugin.Log?.LogInfo($"[AppearancePanel] Aplicar: gender={_current.ModelIndex}, hair={_current.HairItem}, beard={_current.BeardItem}");
		OnAppearanceConfirmed?.Invoke(_current);
	}

	/// <summary>"Cancelar": nothing is written; the host closes the window.</summary>
	private void OnCancelClicked()
	{
		CompanionsPlugin.Log?.LogInfo("[AppearancePanel] Cancelar: nada foi gravado");
		OnAppearanceCancelled?.Invoke();
	}

	/// <summary>The look the companion has now, read from its ZDO (a copy - the panel never writes it).</summary>
	private CompanionAppearance ReadTargetAppearance()
	{
		string data = EditTarget != null ? EditTarget.GetComponent<ZNetView>()?.GetZDO()?.GetString(CompanionSetup.AppearanceHash) : null;
		CompanionAppearance appearance = string.IsNullOrEmpty(data) ? CompanionAppearance.Default() : CompanionAppearance.Deserialize(data);
		// Fase 3.21 (bug dos sliders): the raw value and what came out of it, once per open - it is what tells whether
		// the panel is showing what the ZDO holds (and it is the only place the string is available)
		CompanionsPlugin.Log?.LogInfo($"[AppearanceDiag] abrindo o editor: HC_Appearance "
			+ (string.IsNullOrEmpty(data) ? "(vazia - usando o padrao)" : $"\"{data}\"")
			+ $" -> gender={appearance.ModelIndex} hair=\"{appearance.HairItem}\" beard=\"{appearance.BeardItem}\" "
			+ $"skin={Vec(appearance.SkinColor)} cabelo={Vec(appearance.HairColor)}");
		return appearance;
	}

	/// <summary>
	/// The tier of the companion being edited, from the prefab the ZDO was spawned with (the same way the AI panel's tab
	/// reads it). It only decides the preview prefab - and, through <see cref="CompanionTierDef.CanWearArmor"/>, whether
	/// the appearance even shows (a dverger's look is its prefab; the values are still kept for a future tier change).
	/// </summary>
	private CompanionTierDef TierOfTarget()
	{
		ZDO zdo = EditTarget != null ? EditTarget.GetComponent<ZNetView>()?.GetZDO() : null;
		if (zdo != null)
		{
			int prefab = zdo.GetPrefab();
			CompanionTierDef[] all = CompanionTierData.All;
			for (int i = 0; i < all.Length; i++)
			{
				if (all[i].PrefabName.GetStableHashCode() == prefab)
				{
					return all[i];
				}
			}
		}
		return CompanionTierData.Companion;
	}

	/// <summary>The name of the companion being edited, for the "Editando a aparência de ..." line.</summary>
	private string TargetName()
	{
		if (EditTarget == null)
		{
			return "";
		}
		string stored = EditTarget.GetComponent<ZNetView>()?.GetZDO()?.GetString(CompanionSetup.NameHash);
		if (!string.IsNullOrEmpty(stored))
		{
			return stored;
		}
		Character character = EditTarget.GetComponent<Character>();
		if (character != null && !string.IsNullOrEmpty(character.m_name))
		{
			return character.m_name;
		}
		return ModLocalization.Loc("hc_msg_name_default");
	}

	/// <summary>
	/// Fase 3.13: the name of the companion the starter is about to spawn - a label and the panel's own text field,
	/// pre-filled with the localized name of the chosen tier ("Companheiro"). While the player has not typed anything the
	/// field follows the tier (see <see cref="RefreshNameDefault"/>); once the player types, the text is theirs. The
	/// value is read once, at spawn, by <see cref="AiDraft"/>.
	/// </summary>
	private float BuildNameField(Transform parent, TMP_FontAsset font, float bottomY)
	{
		GameObject group = new GameObject("NameSection", typeof(RectTransform));
		group.transform.SetParent(parent, worldPositionStays: false);
		RectTransform rt = group.GetComponent<RectTransform>();
		rt.anchorMin = new Vector2(0f, 0f);
		rt.anchorMax = new Vector2(1f, 0f);
		rt.pivot = new Vector2(0.5f, 0f);
		rt.sizeDelta = new Vector2(-24f, MiddleNameH);
		rt.anchoredPosition = new Vector2(0f, bottomY);
		Transform content = group.transform;

		float y = 0f;
		y = PlacePlainLabel(content, font, ModLocalization.Loc("hc_ui_name"), y, 18f, "NameLabel");
		y -= 2f;
		_nameField = BuildTextField(content, font, y, AiRowH, "NameField",
			ModLocalization.Loc("hc_ui_placeholder_name"), CompanionAiDraft.MaxNameLength);
		_nameDefault = CompanionTierData.LocalName(_selectedTier);
		_nameEdited = false;
		if (_nameField != null)
		{
			// SetTextWithoutNotify does not raise onValueChanged, so the pre-filled default does not count as "typed"
			_nameField.SetTextWithoutNotify(_nameDefault);
			_nameField.onValueChanged.AddListener(OnNameChanged);
			// Enter cleans the field up (trim/limit) instead of leaving a name with spaces around it for the spawn
			_nameField.onSubmit.AddListener(delegate
			{
				NormalizeNameField();
			});
		}
		return MiddleNameH;
	}

	/// <summary>Keeps track of whether the name in the field is the player's or still the default of the tier.</summary>
	private void OnNameChanged(string value)
	{
		string text = value == null ? "" : value.Trim();
		_nameEdited = text.Length > 0 && !string.Equals(text, _nameDefault, StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>
	/// Makes the field follow the chosen tier, as long as the player has not typed a name of their own: the starter
	/// offers "Companheiro" and the dverger tiers, and the default has to be the name of the one that is selected now.
	/// A typed name is never overwritten (the only way to get the default back is to clear the field).
	/// </summary>
	private void RefreshNameDefault()
	{
		string next = CompanionTierData.LocalName(_selectedTier);
		bool follow = !_nameEdited;
		_nameDefault = next;
		if (follow && _nameField != null)
		{
			_nameField.SetTextWithoutNotify(next);
		}
	}

	/// <summary>Trims the name field and cuts it at the limit, so what the player sees is exactly what will be spawned.</summary>
	private void NormalizeNameField()
	{
		if (_nameField == null)
		{
			return;
		}
		string cleaned = CompanionAiDraft.CleanName(_nameField.text);
		if (!string.Equals(cleaned, _nameField.text, StringComparison.Ordinal))
		{
			_nameField.SetTextWithoutNotify(cleaned);
		}
		OnNameChanged(cleaned);
	}

	/// <summary>
	/// Fase 3.12/3.13: one line with the geometry of the middle column, written once when the panel is built, so the
	/// account can be checked from <c>LogOutput.log</c> without opening the game: the panel, the column, the four bands
	/// ("Invocar", the AI section, the name and the description) and the "Ouvir" button. Every number is measured from
	/// the bottom of the column except the widths and the panel height, and they are the very numbers the rects above
	/// were built with.
	/// </summary>
	private void LogMiddleColumnLayout(float btnH, float buyTop, float aiBottom, float aiTop, float nameBottom, float nameHeight, float descBottom)
	{
		float descTop = _columnHeight - MiddleDescTop;
		float descHeight = Mathf.Max(0f, descTop - descBottom);
		string listen = _aiListenButton == null
			? "AUSENTE"
			: $"{_aiListenBottom:0} base, {AiRowH:0} altura, {_middleWidth - 4f:0} largura";
		string name = nameHeight > 0f
			? $"base={nameBottom:0} topo={nameBottom + nameHeight:0} altura={nameHeight:0} "
				+ $"(campo {nameBottom:0}..{nameBottom + AiRowH:0}, padrao \"{_nameDefault}\", digitado={(_nameEdited ? "sim" : "nao")})"
			: "ausente (Trader)";
		CompanionsPlugin.Log?.LogInfo($"[StarterPanel] Layout da coluna do meio: painel={_panelHeight:0} coluna={_columnHeight:0} largura={_middleWidth:0} | "
			+ $"Invocar: base={MiddleBuyBottom:0} altura={btnH:0} topo={buyTop:0} | "
			+ $"secao IA: base={aiBottom:0} altura={_aiHeight:0} topo={aiTop:0} | "
			+ $"nome: {name} | "
			+ $"descricao: base={descBottom:0} topo={descTop:0} altura={descHeight:0} | "
			+ $"Ouvir: {listen}");
	}

	// ------------------------------------------------------------------ the AI section (Fase 3.10)

	/// <summary>
	/// The "AI" section of the starter panel: the voice of the companion that is about to be born (with "Ouvir") and
	/// its personality (a preset or the player's own words), plus a hint saying the <c>[AI]</c> values are what is
	/// used when nothing is chosen here.
	///
	/// <para>It lives in the <b>middle column</b>, in the fixed band right above "Invocar": the left column is a
	/// scroll with the appearance, and burying the AI choices at the bottom of it would hide the one thing that is
	/// new. The controls are the panel's own idioms (the same <c>&lt; value &gt;</c> picker of hair and beard, the
	/// same label, the same button), so nothing here depends on the AI panel's overlay dropdowns - which could not
	/// work inside a scrolling column anyway (the field would move under an open list).</para>
	///
	/// <para>The height it used is returned so <see cref="BuildMiddleColumn"/> can reserve exactly that much for it
	/// (the description above is placed from that number - Fase 3.12, the layout bug).</para>
	/// </summary>
	private float BuildAiSection(Transform parent, TMP_FontAsset font, GameObject buttonTemplate, float bottomY)
	{
		_aiVoiceIds = CompanionProfile.VoiceIds(withInherit: true);
		_aiVoiceLabels = CompanionProfile.VoiceLabels(withInherit: true);
		_aiPresetIds = CompanionProfile.PresetIds(withInherit: true);
		_aiPresetLabels = CompanionProfile.PresetLabels(withInherit: true);
		_aiVoiceIndex = 0;
		_aiPresetIndex = 0;
		_aiListenButton = null;
		_aiListenBottom = 0f;

		GameObject group = new GameObject("AiSection", typeof(RectTransform));
		group.transform.SetParent(parent, worldPositionStays: false);
		Transform content = group.transform;

		// the group is anchored to the bottom of the column, its children are laid out from its top down
		float y = 0f;
		y = PlacePlainLabel(content, font, ModLocalization.Loc("hc_ai_profile_title"), y, 20f, "AiTitle");
		y -= 2f;
		y = PlacePlainLabel(content, font, ModLocalization.Loc("hc_ai_panel_voice"), y, 18f, "AiVoice");
		y -= 2f;
		BuildPickerRow(content, out _aiVoiceValue, _aiVoiceLabels[0], y, AiRowH, font, buttonTemplate,
			delegate
			{
				CycleAiVoice(-1);
			}, delegate
			{
				CycleAiVoice(1);
			});
		y -= AiRowH + AiGap;
		// Fase 3.12 (the reported bug): "Ouvir" is built exactly like the panel's other action button ("Invocar") -
		// an explicit rect anchored to both sides of the section. The old version put it on a MakeAnchoredRow row and
		// handed it Vector2.zero/one anchors, and in game the button was never visible; this way its size comes from
		// the section itself and depends on no helper row.
		float listenY = y;
		GameObject listen = CreateActionButton(buttonTemplate, content, "AiListen", ModLocalization.Loc("hc_ai_panel_listen"));
		RectTransform listenRT = listen.GetComponent<RectTransform>();
		listenRT.anchorMin = new Vector2(0f, 1f);
		listenRT.anchorMax = new Vector2(1f, 1f);
		listenRT.pivot = new Vector2(0.5f, 1f);
		listenRT.sizeDelta = new Vector2(-4f, AiRowH);
		listenRT.anchoredPosition = new Vector2(0f, listenY);
		Image listenImage = listen.GetComponent<Image>();
		if (listenImage != null)
		{
			// an explicit tint, so the button is visible whatever the template's own colour is
			listenImage.color = Color.white;
			listenImage.raycastTarget = true;
		}
		_aiListenButton = listen.GetComponent<Button>();
		if (_aiListenButton != null)
		{
			_aiListenButton.onClick.RemoveAllListeners();
			_aiListenButton.onClick.AddListener(PreviewStarterVoice);
			_aiListenButton.navigation = new Navigation
			{
				mode = Navigation.Mode.None
			};
			_aiListenButton.interactable = true;
		}
		TMP_Text listenLabel = listen.GetComponentInChildren<TMP_Text>(includeInactive: true);
		if (listenLabel != null)
		{
			listenLabel.fontSize = 16f;
			listenLabel.color = Color.white;
			listenLabel.alignment = TextAlignmentOptions.Center;
			listenLabel.raycastTarget = false;
			listenLabel.gameObject.SetActive(value: true);
		}
		y -= AiRowH + AiGap;
		y = PlacePlainLabel(content, font, ModLocalization.Loc("hc_ai_panel_personality"), y, 18f, "AiPersonality");
		y -= 2f;
		BuildPickerRow(content, out _aiPresetValue, _aiPresetLabels[0], y, AiRowH, font, buttonTemplate,
			delegate
			{
				CycleAiPreset(-1);
			}, delegate
			{
				CycleAiPreset(1);
			});
		y -= AiRowH + AiGap;
		y = PlacePlainLabel(content, font, ModLocalization.Loc("hc_ai_panel_free_text"), y, 18f, "AiWords");
		y -= 2f;
		_aiFreeText = BuildTextField(content, font, y, AiRowH, "AiFreeText",
			ModLocalization.Loc("hc_ai_panel_free_placeholder"), CompanionPersona.MaxFreeTextLength);
		y -= AiRowH + AiGap;
		TMP_Text hint = MakeText(content, "AiHint", ModLocalization.Loc("hc_ai_profile_hint"), font, 11f, LabelText, TextAlignmentOptions.TopLeft);
		RectTransform hintRT = hint.GetComponent<RectTransform>();
		hintRT.anchorMin = new Vector2(0f, 1f);
		hintRT.anchorMax = new Vector2(1f, 1f);
		hintRT.pivot = new Vector2(0f, 1f);
		hintRT.sizeDelta = new Vector2(-8f, 46f);
		hintRT.anchoredPosition = new Vector2(4f, y);
		hint.textWrappingMode = TextWrappingModes.Normal;
		hint.overflowMode = TextOverflowModes.Overflow;
		y -= 46f;

		float height = 0f - y;
		RectTransform rt = group.GetComponent<RectTransform>();
		rt.anchorMin = new Vector2(0f, 0f);
		rt.anchorMax = new Vector2(1f, 0f);
		rt.pivot = new Vector2(0.5f, 0f);
		rt.sizeDelta = new Vector2(-24f, height);
		rt.anchoredPosition = new Vector2(0f, bottomY);
		// where the "Ouvir" button really sits, measured from the panel's bottom (for LogMiddleColumnLayout): the
		// group's bottom plus its height, minus the rows above the button (listenY is negative, measured from the
		// group's top) and minus the button itself
		_aiListenBottom = bottomY + height + listenY - AiRowH;
		return height;
	}

	/// <summary>The voice picker of the AI section: one step through the list, wrapping ("follow the default", 42,
	/// 43, 44).</summary>
	private void CycleAiVoice(int dir)
	{
		if (_aiVoiceIds == null || _aiVoiceIds.Length == 0)
		{
			return;
		}
		_aiVoiceIndex = (_aiVoiceIndex + dir + _aiVoiceIds.Length) % _aiVoiceIds.Length;
		if (_aiVoiceValue != null)
		{
			_aiVoiceValue.text = _aiVoiceLabels[_aiVoiceIndex];
		}
		CompanionsPlugin.Log.LogDebug("[StarterPanel] AI voice: " + (_aiVoiceIds[_aiVoiceIndex] > 0
			? _aiVoiceIds[_aiVoiceIndex] + " (" + _aiVoiceLabels[_aiVoiceIndex] + ")"
			: "default ([AI])"));
	}

	/// <summary>The personality picker of the AI section: one step through "follow the default", the presets and the
	/// free-text entry.</summary>
	private void CycleAiPreset(int dir)
	{
		if (_aiPresetIds == null || _aiPresetIds.Length == 0)
		{
			return;
		}
		_aiPresetIndex = (_aiPresetIndex + dir + _aiPresetIds.Length) % _aiPresetIds.Length;
		if (_aiPresetValue != null)
		{
			_aiPresetValue.text = _aiPresetLabels[_aiPresetIndex];
		}
		CompanionsPlugin.Log.LogDebug("[StarterPanel] AI personality: " + _aiPresetLabels[_aiPresetIndex]);
	}

	/// <summary>
	/// Fase 3.10: the "Ouvir" of the AI section - the real synthesis pipeline (the same worker and the same 3D
	/// playback), with the voice this picker shows and the vol/voz/speed of <c>[AI]</c>. The companion does not exist
	/// yet, so the sample plays at the player.
	/// </summary>
	private void PreviewStarterVoice()
	{
		if (!CompanionTts.Enabled)
		{
			CompanionChatAI.Announce(ModLocalization.Loc("hc_ai_panel_tts_off"));
			return;
		}
		if (!CompanionTts.Ready)
		{
			CompanionChatAI.Announce(ModLocalization.Loc("hc_ai_panel_tts_missing"));
			return;
		}
		Player player = Player.m_localPlayer;
		if (player == null)
		{
			return;
		}
		int voice = (_aiVoiceIds != null && _aiVoiceIndex >= 0 && _aiVoiceIndex < _aiVoiceIds.Length) ? _aiVoiceIds[_aiVoiceIndex] : 0;
		string text = ModLocalization.Loc("hc_ai_panel_tts_sample");
		CompanionsPlugin.Log.LogInfo($"[StarterPanel] Ouvir -> voz {(voice > 0 ? voice.ToString() : CompanionTts.Voice + " (padrao [IA])")}, "
			+ $"volume {CompanionTts.Volume:0.##}, velocidade {CompanionTts.Speed:0.##} | \"{text}\"");
		CompanionTts.Speak(player.gameObject, text, true, voice);
	}

	/// <summary>
	/// The AI choices of the panel, for the companion that is about to be born. "Follow the [IA] default" in both
	/// pickers and an empty text box is the normal case: an empty draft, i.e. a companion that follows the global
	/// values (and keeps following them when they change).
	/// </summary>
	internal CompanionAiDraft AiDraft()
	{
		CompanionAiDraft draft = new CompanionAiDraft();
		if (!StarterMode || _aiVoiceIds == null || _aiPresetIds == null)
		{
			return draft;
		}
		if (_aiVoiceIndex > 0 && _aiVoiceIndex < _aiVoiceIds.Length)
		{
			draft.Voice = _aiVoiceIds[_aiVoiceIndex];
		}
		string preset = (_aiPresetIndex >= 0 && _aiPresetIndex < _aiPresetIds.Length) ? _aiPresetIds[_aiPresetIndex] : CompanionProfile.InheritId;
		string text = _aiFreeText != null && _aiFreeText.text != null ? _aiFreeText.text.Trim() : "";
		bool inherit = string.IsNullOrEmpty(preset) ? _aiPresetIndex <= 0 : string.Equals(preset, CompanionProfile.InheritId, StringComparison.Ordinal);
		if (!inherit || text.Length > 0)
		{
			draft.HasPersona = true;
			draft.Preset = inherit ? "" : preset;
			draft.FreeText = text;
		}
		// Fase 3.13: the name typed in the panel (clean = trimmed and limited; "" when the player left it empty, and
		// then the spawn keeps its own default). Read here, once, on Invocar.
		draft.Name = CompanionAiDraft.CleanName(_nameField != null ? _nameField.text : null);
		return draft;
	}

	/// <summary>
	/// Releases a text box of the panel when it has the focus, and says whether it did. The starter panel closes on Esc,
	/// and a player typing the name or the personality must lose the focus first (the same order the AI panel uses for
	/// its own text box).
	/// </summary>
	internal bool ReleaseTextFocus()
	{
		if (_nameField != null && _nameField.isFocused)
		{
			NormalizeNameField();
			_nameField.DeactivateInputField();
			return true;
		}
		if (_aiFreeText != null && _aiFreeText.isFocused)
		{
			_aiFreeText.DeactivateInputField();
			return true;
		}
		return false;
	}

	/// <summary>
	/// The single-line text box of this panel (the name of the companion and the player's own words for its
	/// personality), with the placeholder and the character limit the caller asks for.
	///
	/// <para>The order matters: the GameObject is created <b>inactive</b> and only activated after its properties are
	/// assigned, because <c>TMP_InputField.OnEnable</c> is where TMP creates the "Caret" it draws the caret and the
	/// selection with - with the component still empty there is no caret and no selection at all (the same lesson as
	/// <c>CompanionAiPanel.CreateInputField</c>). The field is registered in <see cref="ModTextInputFocus"/>, so the
	/// game's own keys (m for the map, Tab for the inventory...) do not fire while the player types.</para>
	/// </summary>
	private TMP_InputField BuildTextField(Transform parent, TMP_FontAsset font, float y, float h, string objectName, string placeholderText, int limit)
	{
		Transform row = MakeAnchoredRow(parent, y, h);
		GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(Image));
		go.SetActive(false);
		go.transform.SetParent(row, worldPositionStays: false);
		RectTransform rt = go.GetComponent<RectTransform>();
		rt.anchorMin = Vector2.zero;
		rt.anchorMax = Vector2.one;
		rt.offsetMin = new Vector2(2f, 0f);
		rt.offsetMax = new Vector2(-2f, 0f);
		Image image = go.GetComponent<Image>();
		image.color = new Color(0.1f, 0.1f, 0.12f, 0.92f);

		GameObject area = new GameObject("TextArea", typeof(RectTransform));
		area.transform.SetParent(go.transform, worldPositionStays: false);
		RectTransform areaRT = area.GetComponent<RectTransform>();
		areaRT.anchorMin = Vector2.zero;
		areaRT.anchorMax = Vector2.one;
		areaRT.offsetMin = new Vector2(8f, 3f);
		areaRT.offsetMax = new Vector2(-8f, -3f);
		area.AddComponent<RectMask2D>();

		TextMeshProUGUI text = MakeText(area.transform, "Text", "", font, 13f, Color.white, TextAlignmentOptions.MidlineLeft);
		RectTransform textRT = text.GetComponent<RectTransform>();
		textRT.anchorMin = Vector2.zero;
		textRT.anchorMax = Vector2.one;
		textRT.offsetMin = Vector2.zero;
		textRT.offsetMax = Vector2.zero;
		// the viewport mask clips it; the text itself must overflow, which is what lets TMP scroll it to the caret
		text.overflowMode = TextOverflowModes.Overflow;
		text.textWrappingMode = TextWrappingModes.PreserveWhitespaceNoWrap;

		TextMeshProUGUI placeholder = MakeText(area.transform, "Placeholder", placeholderText, font, 13f,
			new Color(0.6f, 0.6f, 0.6f, 0.7f), TextAlignmentOptions.MidlineLeft);
		placeholder.fontStyle = FontStyles.Italic;
		placeholder.textWrappingMode = TextWrappingModes.PreserveWhitespaceNoWrap;
		RectTransform placeholderRT = placeholder.GetComponent<RectTransform>();
		placeholderRT.anchorMin = Vector2.zero;
		placeholderRT.anchorMax = Vector2.one;
		placeholderRT.offsetMin = Vector2.zero;
		placeholderRT.offsetMax = Vector2.zero;

		TMP_InputField input = go.AddComponent<TMP_InputField>();
		input.targetGraphic = image;
		input.textComponent = text;
		input.textViewport = areaRT;
		input.placeholder = placeholder;
		input.characterLimit = limit;
		input.contentType = TMP_InputField.ContentType.Standard;
		input.lineType = TMP_InputField.LineType.SingleLine;
		input.richText = false;
		input.onFocusSelectAll = false;
		input.customCaretColor = true;
		input.caretColor = new Color(1f, 0.95f, 0.85f, 1f);
		input.caretWidth = 2;
		input.selectionColor = new Color(0.55f, 0.72f, 1f, 0.55f);

		go.SetActive(true);

		// TMP creates the caret as the FIRST child of the text's parent, i.e. behind the text: as the last child it is
		// always on top of both
		Transform caret = area.transform.Find("Caret");
		if (caret != null)
		{
			caret.SetAsLastSibling();
		}

		ModTextInputFocus.Register(input);
		return input;
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
		gameObject2.GetComponent<Image>().color = Tint;
		gameObject2.GetComponent<Image>().raycastTarget = false;
		return gameObject;
	}

	private void OnBuyClicked()
	{
		if (StarterMode)
		{
			CompanionsPlugin.Log.LogInfo((HireMode ? "[Panel] Summon confirmed" : "[Panel] Starter spawn confirmed") + $" — gender={_current.ModelIndex}, " + "hair=" + _current.HairItem + ", beard=" + _current.BeardItem);
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
		// 1.5.78: the copied Craft button keeps the game's own look (no packed texture)
		GameObject gameObject2 = new GameObject("Tint", typeof(RectTransform), typeof(Image));
		gameObject2.transform.SetParent(gameObject.transform, worldPositionStays: false);
		gameObject2.transform.SetAsFirstSibling();
		RectTransform component3 = gameObject2.GetComponent<RectTransform>();
		component3.anchorMin = Vector2.zero;
		component3.anchorMax = Vector2.one;
		component3.offsetMin = Vector2.zero;
		component3.offsetMax = Vector2.zero;
		gameObject2.GetComponent<Image>().color = Tint;
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
		// 1.5.78: the track is the game's panel background (was a packed texture)
		Transform sliderBg = gameObject.transform.Find("Background");
		Image sliderBgImg = sliderBg != null ? sliderBg.GetComponent<Image>() : null;
		if (sliderBgImg == null)
		{
			foreach (Image image4 in componentsInChildren2)
			{
				if (image4 != null && image4.gameObject.name.IndexOf("background", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					sliderBgImg = image4;
					break;
				}
			}
		}
		GameLook.ApplyPanel(sliderBgImg, new Color(0.12f, 0.1f, 0.08f, 0.95f));
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
		gameObject2.GetComponent<Image>().color = Tint;
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

	/// <summary>
	/// Edit mode: puts the appearance being edited back on the controls. <see cref="BuildCustomColumn"/> writes the
	/// starter's defaults into <see cref="_current"/> (its sliders are born on those values), so the stored colours are
	/// restored here and the three sliders are moved to the nearest point of their own gradient - that way the panel
	/// opens showing the companion's colours, and the first touch of a slider does not jump the colour somewhere else.
	///
	/// <para>Runs once, when the panel is built (never per frame), and only in the editor: the starter keeps its own
	/// defaults, byte for byte.</para>
	/// </summary>
	private void ApplyAppearanceToControls()
	{
		RefreshGenderButtons();
		if (_beardGroup != null)
		{
			_beardGroup.SetActive(_current.ModelIndex == 0);
		}
		// Fase 3.21: the stored colours win over anything the starter's defaults may have written into _current (see
		// _storedSkinColor). SetValueWithoutNotify does not raise onValueChanged, so _current keeps exactly the stored
		// colours (the sliders only become the way back to them, and the moment the player moves one the gradient takes
		// over).
		if (AppearanceEditMode)
		{
			_current.SkinColor = _storedSkinColor;
			_current.HairColor = _storedHairColor;
		}
		if (_skinSlider != null)
		{
			_skinSlider.SetValueWithoutNotify(NearestSkinT(_current.SkinColor));
		}
		if (_hairHueSlider != null && _hairBrightSlider != null)
		{
			NearestHairTone(_current.HairColor, out float hue, out float bright);
			_hairHueSlider.SetValueWithoutNotify(hue);
			_hairBrightSlider.SetValueWithoutNotify(bright);
		}
		CompanionsPlugin.Log?.LogInfo($"[AppearanceDiag] painel da aparencia ({(AppearanceEditMode ? "edicao" : "starter")}): "
			+ $"cor guardada skin={Vec(_storedSkinColor)} cabelo={Vec(_storedHairColor)} -> sliders skin={_skinSlider?.value:0.00} "
			+ $"tom={_hairHueSlider?.value:0.00} brilho={_hairBrightSlider?.value:0.00}; hair=\"{_current.HairItem}\" "
			+ $"beard=\"{_current.BeardItem}\" gender={_current.ModelIndex}");
	}

	/// <summary>A colour for the log ("0.951,0.820,0.680").</summary>
	private static string Vec(Vector3 v)
	{
		return v.x.ToString("0.000") + "," + v.y.ToString("0.000") + "," + v.z.ToString("0.000");
	}

	/// <summary>The point of the skin gradient (<see cref="SkinLight"/> to <see cref="SkinDark"/>) closest to a stored
	/// colour. A grid search, because Color.Lerp per channel cannot be inverted exactly; 21 samples once per open.</summary>
	private static float NearestSkinT(Vector3 stored)
	{
		const int steps = 20;
		float best = float.MaxValue;
		float bestT = 0.05f;
		for (int i = 0; i <= steps; i++)
		{
			float t = (float)i / steps;
			float distance = (Utils.ColorToVec3(Color.Lerp(SkinLight, SkinDark, t)) - stored).sqrMagnitude;
			if (distance < best)
			{
				best = distance;
				bestT = t;
			}
		}
		return bestT;
	}

	/// <summary>The (tone, shade) pair of <see cref="UpdateHairColor"/>'s own formula closest to a stored colour: the
	/// hue slider runs the palette from blond to dark and the shade slider scales it, and the multiplication makes the
	/// pair impossible to invert channel by channel - so it is a 21x21 grid search, once per open.</summary>
	private static void NearestHairTone(Vector3 stored, out float hue, out float bright)
	{
		const int steps = 20;
		float best = float.MaxValue;
		hue = 0.45f;
		bright = 0.85f;
		for (int i = 0; i <= steps; i++)
		{
			float t = (float)i / steps;
			for (int j = 0; j <= steps; j++)
			{
				float b = (float)j / steps;
				Color c = Color.Lerp(HairBlonde, HairDark, t) * Mathf.Lerp(0.1f, 1f, b);
				float distance = (Utils.ColorToVec3(c) - stored).sqrMagnitude;
				if (distance < best)
				{
					best = distance;
					hue = t;
					bright = b;
				}
			}
		}
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
		// Fase 3.21: the indices the pickers show, once per open - "index 0" for a stored name that is not in the list
		// is what would make the picker label say Hair1 while the stored one was something else
		CompanionsPlugin.Log?.LogInfo($"[AppearanceDiag] listas: {_hairs.Count} cabelos, {_beards.Count - 1} barbas "
			+ $"(+ nenhuma); guardado hair=\"{_current.HairItem}\" -> indice {_hairIndex}, beard=\"{_current.BeardItem}\" "
			+ $"-> indice {_beardIndex}");
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
		int hairHash = 0;
		int beardHash = 0;
		if (_selectedTier.CanWearArmor)
		{
			hairHash = (string.IsNullOrEmpty(_current.HairItem) ? "Hair1" : _current.HairItem).GetStableHashCode();
			beardHash = ((_current.ModelIndex == 0) ? (_current.BeardItem ?? "") : "").GetStableHashCode();
			// Fase 3.21 (bug do cabelo/barba): the same cache the real companion needs cleared (see
			// CompanionSetup.ResetVisualItemCache) - the clone's VisEquipment remembers the hash it "already" attached
			// and never tries the object again after one failed attempt, so the preview kept the first haircut for ever.
			// Only when the haircut/beard really changed: this method also runs on every slider frame, and re-attaching
			// the hair there would throw the object away and rebuild it 60 times a second.
			if (hairHash != _lastPreviewHairHash || beardHash != _lastPreviewBeardHash)
			{
				CompanionSetup.PrepareVisualAttach(component);
				_lastPreviewHairHash = hairHash;
				_lastPreviewBeardHash = beardHash;
			}
			_currentModelIndex?.SetValue(component, -1);
			component.SetModel(_current.ModelIndex);
			component.SetHairItem(hairHash);
			component.SetBeardItem(beardHash);
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
		if (_selectedTier.CanWearArmor)
		{
			// Fase 3.21: one line per change of haircut/beard (a slider drag must not fill the log) - it proves whether
			// the clone got the objects and whether the prefab of that hash exists at all
			string pair = _current.ModelIndex + ":" + hairHash + ":" + beardHash;
			if (pair != _lastPreviewHairLog)
			{
				_lastPreviewHairLog = pair;
				CompanionsPlugin.Log?.LogInfo($"[AppearanceDiag] preview: gender={_current.ModelIndex} "
					+ $"hair=\"{_current.HairItem}\"({hairHash}) beard=\"{_current.BeardItem}\"({beardHash}) | depois: "
					+ CompanionSetup.DescribeVisualState(component, hairHash, beardHash));
			}
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

	private static string StyleName(string prefab)
	{
		try
		{
			string key = ObjectDB.instance?.GetItemPrefab(prefab)?.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_name;
			if (!string.IsNullOrEmpty(key) && Localization.instance != null)
			{
				string loc = Localization.instance.Localize(key);
				if (!string.IsNullOrEmpty(loc) && !loc.StartsWith("[") && !loc.StartsWith("$"))
				{
					return loc;
				}
			}
		}
		catch
		{
		}
		return prefab;
	}

	private void RefreshPickerLabels()
	{
		if (_hairNameText != null)
		{
			_hairNameText.text = ((_hairs.Count > 0) ? StyleName(_hairs[_hairIndex]) : "—");
		}
		if (_beardNameText != null)
		{
			_beardNameText.text = ((_beardIndex == 0) ? ModLocalization.Loc("hc_ui_beard_none") : ((_beards.Count > _beardIndex) ? StyleName(_beards[_beardIndex]) : "—"));
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
		// Fase 3.13: the default name follows the chosen type while the player hasn't typed one
		RefreshNameDefault();
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
		// Fase 3.13: same as SetType - the default name follows the type until the player types one
		RefreshNameDefault();
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

	internal static GameObject CreateButton(GameObject template, Transform parent, string name, string label)
	{
		GameObject gameObject = UnityEngine.Object.Instantiate(template, parent);
		gameObject.name = name;
		gameObject.SetActive(value: true);
		Button component = gameObject.GetComponent<Button>();
		if (component != null)
		{
			component.onClick.RemoveAllListeners();
			// the template is the inventory's craft button (InventoryGui.m_craftButton), and the game leaves that
			// button interactable=false whenever nothing is craftable - which is the normal state after the
			// inventory has been opened once. A clone would inherit it and silently ignore every click (the
			// starter panel only worked because it opens before the first inventory). Always enable the clone.
			component.interactable = true;
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
			// 1.5.80: labels like the game popup's OK button
			GameLook.ApplyButtonText(componentInChildren);
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
		// 1.5.78: the copied button keeps the game's own sprite (no packed texture)
		Image image = gameObject.GetComponent<Image>();
		if (image == null)
		{
			image = gameObject.AddComponent<Image>();
		}
		if (image != null)
		{
			image.color = Color.white;
			image.raycastTarget = true;
		}
		Button component4 = gameObject.GetComponent<Button>();
		if (component4 != null)
		{
			component4.onClick.RemoveAllListeners();
			// the same trap as CreateButton: the craft-button template can come interactable=false (the game
			// disables it whenever nothing is craftable), and a clone would silently ignore every click - the
			// hair/beard arrows stopped responding after the first inventory open because of this
			component4.interactable = true;
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
		gameObject2.GetComponent<Image>().color = Tint;
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
