using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Companions;

public class HomeZonePanel : MonoBehaviour
{
	private bool _visible;

	private Canvas _canvas;

	private GameObject _root;

	private TMP_FontAsset _font;

	private Slider _slider;

	private TextMeshProUGUI _valueLabel;

	private TextMeshProUGUI _titleLabel;

	private TextMeshProUGUI _hintLabel;

	private CompanionSetup _companion;

	private CompanionAI _companionAI;

	private HomeZoneVisual _homeZoneVisual;

	private float _dpadRepeatTimer;

	private const float DpadInitialDelay = 0.35f;

	private const float DpadRepeatRate = 0.08f;

	private const float StickDeadZone = 0.3f;

	private const int SliderMin = 1;

	private const int SliderMax = 40;

	private const int StepMeters = 5;

	private const float PanelW = 350f;

	private const float PanelH = 140f;

	private static readonly Color BgColor = new Color(0.08f, 0.07f, 0.06f, 0.92f);

	private static readonly Color GoldColor = new Color(1f, 0.85f, 0.2f, 0.7f);

	private static readonly Color LabelColor = new Color(1f, 0.9f, 0.5f, 1f);

	private static readonly Color HandleColor = new Color(1f, 0.95f, 0.6f, 1f);

	public static HomeZonePanel Instance { get; private set; }

	public bool IsVisible => _visible;

	public CompanionSetup CurrentCompanion => _companion;

	public static bool IsOpenFor(CompanionSetup setup)
	{
		if (Instance != null && Instance._visible && Instance._companion != null)
		{
			return Instance._companion == setup;
		}
		return false;
	}

	public static void EnsureInstance()
	{
		if (!(Instance != null))
		{
			GameObject gameObject = new GameObject("HC_HomeZonePanel");
			UnityEngine.Object.DontDestroyOnLoad(gameObject);
			gameObject.AddComponent<HomeZonePanel>();
		}
	}

	public void Show(CompanionSetup companion)
	{
		if (!(companion == null))
		{
			if (_root == null)
			{
				Build();
			}
			_companion = companion;
			_companionAI = companion.GetComponent<CompanionAI>();
			_homeZoneVisual = companion.gameObject.GetComponent<HomeZoneVisual>() ?? companion.gameObject.AddComponent<HomeZoneVisual>();
			float homeRadius = _companion.GetHomeRadius();
			int num = Mathf.Clamp(Mathf.RoundToInt(homeRadius / 5f), 1, 40);
			_slider.SetValueWithoutNotify(num);
			UpdateValueLabel(num * 5);
			if (_companion.HasHomePosition())
			{
				_homeZoneVisual.Show(_companion.GetHomePosition(), homeRadius);
			}
			string text = companion.GetComponent<Humanoid>()?.m_name;
			string text2 = companion.GetComponent<ZNetView>()?.GetZDO()?.GetString(CompanionSetup.NameHash);
			if (!string.IsNullOrEmpty(text2))
			{
				text = text2;
			}
			_titleLabel.text = (string.IsNullOrEmpty(text) ? ModLocalization.Loc("hc_ui_home_zone") : (text + ModLocalization.Loc("hc_ui_home_zone_suffix")));
			UpdateHintText();
			_root.SetActive(value: true);
			_visible = true;
			_dpadRepeatTimer = 0f;
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

	private void OnDestroy()
	{
		if (Instance == this)
		{
			Instance = null;
		}
	}

	private void Build()
	{
		_canvas = base.gameObject.AddComponent<Canvas>();
		_canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		_canvas.sortingOrder = 29;
		CanvasScaler canvasScaler = base.gameObject.AddComponent<CanvasScaler>();
		canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
		base.gameObject.AddComponent<GraphicRaycaster>();
		_font = ResolveFont();
		_root = new GameObject("HomeZoneRoot", typeof(RectTransform), typeof(Image));
		_root.transform.SetParent(base.transform, worldPositionStays: false);
		RectTransform component = _root.GetComponent<RectTransform>();
		component.anchorMin = new Vector2(0.5f, 0.5f);
		component.anchorMax = new Vector2(0.5f, 0.5f);
		component.pivot = new Vector2(0.5f, 0.5f);
		component.sizeDelta = new Vector2(350f, 140f);
		Image component2 = _root.GetComponent<Image>();
		component2.color = BgColor;
		component2.raycastTarget = true;
		BuildTitle();
		BuildCloseButton();
		BuildSliderRow();
		BuildHintText();
		_root.SetActive(value: false);
	}

	private void UpdateValueLabel(float radius)
	{
		if (_valueLabel != null)
		{
			_valueLabel.text = $"{radius:F0}m";
		}
	}

	private void UpdateHintText()
	{
		if (!(_hintLabel == null))
		{
			_hintLabel.text = (ZInput.IsGamepadActive() ? ModLocalization.Loc("hc_ui_homezone_hint_gamepad") : ModLocalization.Loc("hc_ui_homezone_hint"));
		}
	}

	public void Hide()
	{
		_visible = false;
		if (_root != null)
		{
			_root.SetActive(value: false);
		}
		_homeZoneVisual?.Hide();
		_companion = null;
		_companionAI = null;
		_homeZoneVisual = null;
	}

	private void Update()
	{
		if (!_visible)
		{
			return;
		}
		if (_companion == null || !_companion)
		{
			Hide();
			return;
		}
		if (Input.GetKeyDown(KeyCode.Escape) || ZInput.GetButtonDown("JoyButtonB"))
		{
			Hide();
			return;
		}
		if (ZInput.GetButtonDown("JoyButtonA"))
		{
			Hide();
			return;
		}
		Cursor.lockState = CursorLockMode.None;
		Cursor.visible = true;
		if (ZInput.IsGamepadActive())
		{
			UpdateControllerInput();
		}
		UpdateHintText();
	}

	private void UpdateControllerInput()
	{
		float unscaledDeltaTime = Time.unscaledDeltaTime;
		float num = MHR_Stick.LX();
		bool buttonDown = ZInput.GetButtonDown("JoyDPadLeft");
		bool buttonDown2 = ZInput.GetButtonDown("JoyDPadRight");
		bool flag = buttonDown || num < -0.3f;
		bool flag2 = buttonDown2 || num > 0.3f;
		if (flag || flag2)
		{
			if (_dpadRepeatTimer <= 0f || buttonDown || buttonDown2)
			{
				AdjustSlider(flag2 ? 1 : (-1));
				_dpadRepeatTimer = ((buttonDown || buttonDown2) ? 0.35f : 0.08f);
			}
			_dpadRepeatTimer -= unscaledDeltaTime;
		}
		else
		{
			_dpadRepeatTimer = 0f;
		}
	}

	private void AdjustSlider(int direction)
	{
		int num = Mathf.RoundToInt(_slider.value);
		int num2 = Mathf.Clamp(num + direction, 1, 40);
		_slider.value = num2;
	}

	private void OnSliderChanged(float rawValue)
	{
		if (!(_companion == null))
		{
			int num = Mathf.RoundToInt(rawValue);
			float num2 = num * 5;
			_companion.SetHomeRadius(num2);
			UpdateValueLabel(num2);
			if (_homeZoneVisual != null && _companion.HasHomePosition())
			{
				_homeZoneVisual.Show(_companion.GetHomePosition(), num2);
			}
		}
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
		if (Hud.instance != null)
		{
			TextMeshProUGUI[] componentsInChildren2 = Hud.instance.GetComponentsInChildren<TextMeshProUGUI>(includeInactive: true);
			for (int j = 0; j < componentsInChildren2.Length; j++)
			{
				if (componentsInChildren2[j] != null && componentsInChildren2[j].font != null && componentsInChildren2[j].font.name.IndexOf("LiberationSans", StringComparison.OrdinalIgnoreCase) < 0)
				{
					return componentsInChildren2[j].font;
				}
			}
		}
		return TMP_Settings.defaultFontAsset;
	}

	private void BuildTitle()
	{
		GameObject gameObject = new GameObject("Title", typeof(RectTransform));
		gameObject.transform.SetParent(_root.transform, worldPositionStays: false);
		RectTransform component = gameObject.GetComponent<RectTransform>();
		component.anchorMin = new Vector2(0f, 1f);
		component.anchorMax = new Vector2(1f, 1f);
		component.pivot = new Vector2(0.5f, 1f);
		component.sizeDelta = new Vector2(0f, 32f);
		component.anchoredPosition = new Vector2(0f, -2f);
		_titleLabel = gameObject.AddComponent<TextMeshProUGUI>();
		if (_font != null)
		{
			_titleLabel.font = _font;
		}
		_titleLabel.text = ModLocalization.Loc("hc_ui_home_zone");
		_titleLabel.fontSize = 16f;
		_titleLabel.color = LabelColor;
		_titleLabel.fontStyle = FontStyles.Bold;
		_titleLabel.alignment = TextAlignmentOptions.Center;
		_titleLabel.raycastTarget = false;
	}

	private void BuildCloseButton()
	{
		GameObject gameObject = new GameObject("CloseBtn", typeof(RectTransform), typeof(Image), typeof(Button));
		gameObject.transform.SetParent(_root.transform, worldPositionStays: false);
		RectTransform component = gameObject.GetComponent<RectTransform>();
		component.anchorMin = new Vector2(1f, 1f);
		component.anchorMax = new Vector2(1f, 1f);
		component.pivot = new Vector2(1f, 1f);
		component.sizeDelta = new Vector2(28f, 28f);
		component.anchoredPosition = new Vector2(-4f, -4f);
		gameObject.GetComponent<Image>().color = new Color(0.3f, 0.15f, 0.1f, 0.7f);
		gameObject.GetComponent<Button>().onClick.AddListener(Hide);
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

	private void BuildSliderRow()
	{
		GameObject gameObject = new GameObject("SliderRow", typeof(RectTransform));
		gameObject.transform.SetParent(_root.transform, worldPositionStays: false);
		RectTransform component = gameObject.GetComponent<RectTransform>();
		component.anchorMin = new Vector2(0f, 0.3f);
		component.anchorMax = new Vector2(1f, 0.7f);
		component.offsetMin = new Vector2(20f, 0f);
		component.offsetMax = new Vector2(-20f, 0f);
		GameObject gameObject2 = new GameObject("RadiusLabel", typeof(RectTransform));
		gameObject2.transform.SetParent(gameObject.transform, worldPositionStays: false);
		TextMeshProUGUI textMeshProUGUI = gameObject2.AddComponent<TextMeshProUGUI>();
		if (_font != null)
		{
			textMeshProUGUI.font = _font;
		}
		textMeshProUGUI.text = ModLocalization.Loc("hc_ui_home_radius");
		textMeshProUGUI.fontSize = 14f;
		textMeshProUGUI.color = new Color(0.85f, 0.8f, 0.65f, 1f);
		textMeshProUGUI.alignment = TextAlignmentOptions.Left;
		textMeshProUGUI.raycastTarget = false;
		RectTransform component2 = gameObject2.GetComponent<RectTransform>();
		component2.anchorMin = new Vector2(0f, 0f);
		component2.anchorMax = new Vector2(0.2f, 1f);
		component2.offsetMin = Vector2.zero;
		component2.offsetMax = Vector2.zero;
		GameObject gameObject3 = BuildSlider(gameObject.transform);
		RectTransform component3 = gameObject3.GetComponent<RectTransform>();
		component3.anchorMin = new Vector2(0.2f, 0.15f);
		component3.anchorMax = new Vector2(0.82f, 0.85f);
		component3.offsetMin = new Vector2(4f, 0f);
		component3.offsetMax = new Vector2(-4f, 0f);
		GameObject gameObject4 = new GameObject("ValueLabel", typeof(RectTransform));
		gameObject4.transform.SetParent(gameObject.transform, worldPositionStays: false);
		_valueLabel = gameObject4.AddComponent<TextMeshProUGUI>();
		if (_font != null)
		{
			_valueLabel.font = _font;
		}
		_valueLabel.text = "50m";
		_valueLabel.fontSize = 16f;
		_valueLabel.color = LabelColor;
		_valueLabel.fontStyle = FontStyles.Bold;
		_valueLabel.alignment = TextAlignmentOptions.Center;
		_valueLabel.raycastTarget = false;
		RectTransform component4 = gameObject4.GetComponent<RectTransform>();
		component4.anchorMin = new Vector2(0.82f, 0f);
		component4.anchorMax = new Vector2(1f, 1f);
		component4.offsetMin = Vector2.zero;
		component4.offsetMax = Vector2.zero;
	}

	private void BuildHintText()
	{
		GameObject gameObject = new GameObject("Hint", typeof(RectTransform));
		gameObject.transform.SetParent(_root.transform, worldPositionStays: false);
		RectTransform component = gameObject.GetComponent<RectTransform>();
		component.anchorMin = new Vector2(0f, 0f);
		component.anchorMax = new Vector2(1f, 0.25f);
		component.offsetMin = new Vector2(10f, 4f);
		component.offsetMax = new Vector2(-10f, 0f);
		_hintLabel = gameObject.AddComponent<TextMeshProUGUI>();
		if (_font != null)
		{
			_hintLabel.font = _font;
		}
		_hintLabel.fontSize = 11f;
		_hintLabel.color = new Color(0.6f, 0.6f, 0.6f, 0.8f);
		_hintLabel.alignment = TextAlignmentOptions.Center;
		_hintLabel.raycastTarget = false;
	}

	private GameObject BuildSlider(Transform parent)
	{
		GameObject gameObject = new GameObject("RadiusSlider", typeof(RectTransform));
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		GameObject gameObject2 = new GameObject("Background", typeof(RectTransform), typeof(Image));
		gameObject2.transform.SetParent(gameObject.transform, worldPositionStays: false);
		gameObject2.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f, 0.6f);
		RectTransform component = gameObject2.GetComponent<RectTransform>();
		component.anchorMin = Vector2.zero;
		component.anchorMax = Vector2.one;
		component.offsetMin = new Vector2(0f, 8f);
		component.offsetMax = new Vector2(0f, -8f);
		GameObject gameObject3 = new GameObject("Fill Area", typeof(RectTransform));
		gameObject3.transform.SetParent(gameObject.transform, worldPositionStays: false);
		RectTransform component2 = gameObject3.GetComponent<RectTransform>();
		component2.anchorMin = Vector2.zero;
		component2.anchorMax = Vector2.one;
		component2.offsetMin = new Vector2(5f, 8f);
		component2.offsetMax = new Vector2(-5f, -8f);
		GameObject gameObject4 = new GameObject("Fill", typeof(RectTransform), typeof(Image));
		gameObject4.transform.SetParent(gameObject3.transform, worldPositionStays: false);
		gameObject4.GetComponent<Image>().color = GoldColor;
		RectTransform component3 = gameObject4.GetComponent<RectTransform>();
		component3.anchorMin = Vector2.zero;
		component3.anchorMax = new Vector2(0f, 1f);
		component3.offsetMin = Vector2.zero;
		component3.offsetMax = Vector2.zero;
		GameObject gameObject5 = new GameObject("Handle Slide Area", typeof(RectTransform));
		gameObject5.transform.SetParent(gameObject.transform, worldPositionStays: false);
		RectTransform component4 = gameObject5.GetComponent<RectTransform>();
		component4.anchorMin = Vector2.zero;
		component4.anchorMax = Vector2.one;
		component4.offsetMin = new Vector2(5f, 6f);
		component4.offsetMax = new Vector2(-5f, -6f);
		GameObject gameObject6 = new GameObject("Handle", typeof(RectTransform), typeof(Image));
		gameObject6.transform.SetParent(gameObject5.transform, worldPositionStays: false);
		Image component5 = gameObject6.GetComponent<Image>();
		component5.color = HandleColor;
		RectTransform component6 = gameObject6.GetComponent<RectTransform>();
		component6.sizeDelta = new Vector2(10f, 0f);
		component6.anchorMin = new Vector2(0f, 0f);
		component6.anchorMax = new Vector2(0f, 1f);
		_slider = gameObject.AddComponent<Slider>();
		_slider.fillRect = component3;
		_slider.handleRect = component6;
		_slider.targetGraphic = component5;
		_slider.direction = Slider.Direction.LeftToRight;
		_slider.minValue = 1f;
		_slider.maxValue = 40f;
		_slider.wholeNumbers = true;
		_slider.value = 10f;
		_slider.onValueChanged.AddListener(OnSliderChanged);
		return gameObject;
	}
}
