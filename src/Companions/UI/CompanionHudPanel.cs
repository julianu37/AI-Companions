using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Companions;

public class CompanionHudPanel : MonoBehaviour
{
	private class CompanionRow
	{
		internal RectTransform RowRoot;

		private TextMeshProUGUI _nameText;

		private Image _hpFill;

		private Image _staFill;

		private TextMeshProUGUI _hpText;

		private TextMeshProUGUI _staText;

		// the localized bar templates are resolved once per row (the language can only change with a scene reload)
		private string _hpFormat;

		private string _staFormat;

		private Image _restedIcon;

		private Image[] _seIconSlots;

		private Image[] _foodIconSlots;

		// 1.8.3: the components of a row never change with the frame (a row belongs to one companion), so the five
		// GetComponent calls that ran every frame are done once per owner; the row is refreshed 5 times a second
		// instead of 60 (nobody reads faster than that) and the texts are only rewritten when the value really
		// changed - every assignment rebuilds the text mesh.
		private CompanionSetup _owner;

		private Character _character;

		private CompanionStamina _stamina;

		private CompanionRestedBuff _rested;

		private Humanoid _humanoid;

		private CompanionFood _food;

		private float _nextRefresh;

		private int _lastHealth = int.MinValue;

		private int _lastMaxHealth = int.MinValue;

		private int _lastStamina = int.MinValue;

		private int _lastMaxStamina = int.MinValue;

		internal void Refresh(CompanionSetup comp)
		{
			if (!ReferenceEquals(_owner, comp))
			{
				// a row is re-bound when the list around it changes: look the components up again for the new owner
				_owner = comp;
				_character = comp.GetComponent<Character>();
				_stamina = comp.GetComponent<CompanionStamina>();
				_rested = comp.GetComponent<CompanionRestedBuff>();
				_humanoid = comp.GetComponent<Humanoid>();
				_food = comp.GetComponent<CompanionFood>();
				_lastHealth = int.MinValue;
				_lastMaxHealth = int.MinValue;
				_lastStamina = int.MinValue;
				_lastMaxStamina = int.MinValue;
				_nextRefresh = 0f;
			}
			Character component = _character;
			CompanionStamina component2 = _stamina;
			CompanionRestedBuff component3 = _rested;
			Humanoid component4 = _humanoid;
			CompanionFood component5 = _food;
			float num = ((component != null) ? component.GetHealth() : 0f);
			float num2 = ((component != null) ? component.GetMaxHealth() : 1f);
			// the two bars stay per frame: a single fillAmount, no string and no lookup
			if (_hpFill != null)
			{
				_hpFill.fillAmount = ((num2 > 0f) ? Mathf.Clamp01(num / num2) : 0f);
			}
			float num3 = ((component2 != null) ? component2.Stamina : 0f);
			float num4 = ((component2 != null) ? component2.MaxStamina : 1f);
			if (_staFill != null)
			{
				_staFill.fillAmount = ((num4 > 0f) ? Mathf.Clamp01(num3 / num4) : 0f);
			}
			// 1.8.3: everything below formats text or looks prefabs and sprites up, so it runs 5 times a second instead
			// of every frame (nobody reads a number faster than that), and the text is only rewritten when the value
			// really changed - every assignment rebuilds the text mesh.
			float now = Time.unscaledTime;
			if (now < _nextRefresh)
			{
				return;
			}
			_nextRefresh = now + 0.2f;
			if (_nameText != null)
			{
				_nameText.text = ((component != null && !string.IsNullOrEmpty(component.m_name)) ? component.m_name : ModLocalization.Loc("hc_msg_name_default"));
			}
			// the same rounding "F0" uses (half away from zero), so the number on screen does not change
			int health = Mathf.FloorToInt(num + 0.5f);
			int maxHealth = Mathf.FloorToInt(num2 + 0.5f);
			if (_hpText != null && (health != _lastHealth || maxHealth != _lastMaxHealth))
			{
				_lastHealth = health;
				_lastMaxHealth = maxHealth;
				if (_hpFormat == null)
				{
					_hpFormat = ModLocalization.Loc("hc_hud_health_fmt");
				}
				_hpText.text = string.Format(_hpFormat, health.ToString("F0"), maxHealth.ToString("F0"));
			}
			int stamina = Mathf.FloorToInt(num3 + 0.5f);
			int maxStamina = Mathf.FloorToInt(num4 + 0.5f);
			if (_staText != null && (stamina != _lastStamina || maxStamina != _lastMaxStamina))
			{
				_lastStamina = stamina;
				_lastMaxStamina = maxStamina;
				if (_staFormat == null)
				{
					_staFormat = ModLocalization.Loc("hc_hud_stamina_fmt");
				}
				_staText.text = string.Format(_staFormat, stamina.ToString("F0"), maxStamina.ToString("F0"));
			}
			if (_foodIconSlots != null)
			{
				int num5 = 0;
				if (component5 != null)
				{
					int maxFoodSlots = CompanionFood.MaxFoodSlots;
					for (int i = 0; i < maxFoodSlots; i++)
					{
						if (num5 >= _foodIconSlots.Length)
						{
							break;
						}
						CompanionFood.FoodEffect food = component5.GetFood(i);
						if (food.IsActive)
						{
							Sprite sprite = (string.IsNullOrEmpty(food.ItemPrefabName) ? null : ObjectDB.instance?.GetItemPrefab(food.ItemPrefabName))?.GetComponent<ItemDrop>()?.m_itemData?.GetIcon();
							if (sprite != null)
							{
								_foodIconSlots[num5].sprite = sprite;
								_foodIconSlots[num5].gameObject.SetActive(value: true);
								num5++;
							}
						}
					}
				}
				for (int j = num5; j < _foodIconSlots.Length; j++)
				{
					_foodIconSlots[j].gameObject.SetActive(value: false);
				}
			}
			if (_restedIcon != null)
			{
				bool flag = component3 != null && component3.IsRested;
				_restedIcon.gameObject.SetActive(flag);
				if (flag)
				{
					Sprite sprite2 = component3.DisplaySE?.m_icon;
					_restedIcon.sprite = sprite2;
					_restedIcon.color = ((sprite2 != null) ? Color.white : new Color(0.4f, 1f, 0.4f));
				}
			}
			if (_seIconSlots == null)
			{
				return;
			}
			List<StatusEffect> list = (component4?.GetSEMan())?.GetStatusEffects();
			int num6 = 0;
			if (list != null)
			{
				for (int k = 0; k < list.Count; k++)
				{
					if (num6 >= _seIconSlots.Length)
					{
						break;
					}
					StatusEffect statusEffect = list[k];
					if (!(statusEffect == null) && !(statusEffect.m_icon == null) && statusEffect.NameHash() != SEMan.s_statusEffectEncumbered)
					{
						_seIconSlots[num6].sprite = statusEffect.m_icon;
						_seIconSlots[num6].gameObject.SetActive(value: true);
						num6++;
					}
				}
			}
			for (int l = num6; l < _seIconSlots.Length; l++)
			{
				_seIconSlots[l].gameObject.SetActive(value: false);
			}
		}

		internal CompanionRow(RectTransform parent, int index, TMP_FontAsset font, float cardWidth)
		{
			GameObject gameObject = new GameObject($"CompanionRow_{index}", typeof(RectTransform));
			gameObject.transform.SetParent(parent, worldPositionStays: false);
			RowRoot = gameObject.GetComponent<RectTransform>();
			RowRoot.anchorMin = new Vector2(0f, 1f);
			RowRoot.anchorMax = new Vector2(0f, 1f);
			RowRoot.pivot = new Vector2(0f, 1f);
			RowRoot.sizeDelta = new Vector2(cardWidth, 61f);
			Sprite sliderSprite = GetSliderSprite();
			if (_gradHp == null)
			{
				_gradHp = MakeGradientSprite(HpL, HpR);
			}
			if (_gradSta == null)
			{
				_gradSta = MakeGradientSprite(StaL, StaR);
			}
			float num = -4f;
			_foodIconSlots = new Image[5];
			for (int i = 0; i < 5; i++)
			{
				GameObject gameObject2 = new GameObject($"FoodIcon_{i}", typeof(RectTransform));
				gameObject2.transform.SetParent(RowRoot, worldPositionStays: false);
				Image image = gameObject2.AddComponent<Image>();
				image.color = Color.white;
				image.raycastTarget = false;
				RectTransform component = gameObject2.GetComponent<RectTransform>();
				component.anchorMin = new Vector2(0f, 1f);
				component.anchorMax = new Vector2(0f, 1f);
				component.pivot = new Vector2(0f, 1f);
				component.sizeDelta = new Vector2(20f, 20f);
				component.anchoredPosition = new Vector2(6f + (float)i * 23f, num);
				gameObject2.SetActive(value: false);
				_foodIconSlots[i] = image;
			}
			GameObject gameObject3 = new GameObject("RestedIcon", typeof(RectTransform));
			gameObject3.transform.SetParent(RowRoot, worldPositionStays: false);
			_restedIcon = gameObject3.AddComponent<Image>();
			_restedIcon.color = new Color(0.4f, 1f, 0.4f);
			_restedIcon.raycastTarget = false;
			RectTransform component2 = gameObject3.GetComponent<RectTransform>();
			component2.anchorMin = new Vector2(1f, 1f);
			component2.anchorMax = new Vector2(1f, 1f);
			component2.pivot = new Vector2(1f, 1f);
			component2.sizeDelta = new Vector2(20f, 20f);
			component2.anchoredPosition = new Vector2(-6f, num);
			gameObject3.SetActive(value: false);
			_seIconSlots = new Image[5];
			float num2 = 29f;
			for (int j = 0; j < 5; j++)
			{
				GameObject gameObject4 = new GameObject($"SeIcon_{j}", typeof(RectTransform));
				gameObject4.transform.SetParent(RowRoot, worldPositionStays: false);
				Image image2 = gameObject4.AddComponent<Image>();
				image2.color = Color.white;
				image2.raycastTarget = false;
				RectTransform component3 = gameObject4.GetComponent<RectTransform>();
				component3.anchorMin = new Vector2(1f, 1f);
				component3.anchorMax = new Vector2(1f, 1f);
				component3.pivot = new Vector2(1f, 1f);
				component3.sizeDelta = new Vector2(20f, 20f);
				component3.anchoredPosition = new Vector2(0f - (num2 + (float)j * 23f), num);
				gameObject4.SetActive(value: false);
				_seIconSlots[j] = image2;
			}
			_nameText = MakeText(RowRoot, "Name", ModLocalization.Loc("hc_msg_name_default"), font, 10f, ColName, TextAlignmentOptions.Center);
			RectTransform component4 = _nameText.GetComponent<RectTransform>();
			component4.anchorMin = new Vector2(0f, 1f);
			component4.anchorMax = new Vector2(1f, 1f);
			component4.pivot = new Vector2(0.5f, 1f);
			component4.sizeDelta = new Vector2(-12f, 20f);
			component4.anchoredPosition = new Vector2(0f, num);
			num -= 23f;
			(_hpFill, _hpText, num) = MakeBar(RowRoot, "HP", "Health", _gradHp, font, sliderSprite, num);
			(_staFill, _staText, num) = MakeBar(RowRoot, "STA", "Stamina", _gradSta, font, sliderSprite, num);
		}

		private static TextMeshProUGUI MakeText(Transform parent, string goName, string text, TMP_FontAsset font, float size, Color color, TextAlignmentOptions align)
		{
			GameObject gameObject = new GameObject(goName, typeof(RectTransform));
			gameObject.transform.SetParent(parent, worldPositionStays: false);
			TextMeshProUGUI textMeshProUGUI = gameObject.AddComponent<TextMeshProUGUI>();
			if (font != null)
			{
				textMeshProUGUI.font = font;
				if (textMeshProUGUI.fontSharedMaterial == null && font.material != null)
				{
					textMeshProUGUI.fontSharedMaterial = font.material;
				}
				if (textMeshProUGUI.fontSharedMaterial != null)
				{
					textMeshProUGUI.outlineWidth = 0.2f;
					textMeshProUGUI.outlineColor = new Color32(0, 0, 0, 200);
				}
			}
			textMeshProUGUI.text = text;
			textMeshProUGUI.fontSize = size;
			textMeshProUGUI.color = color;
			textMeshProUGUI.alignment = align;
			textMeshProUGUI.raycastTarget = false;
			textMeshProUGUI.overflowMode = TextOverflowModes.Overflow;
			return textMeshProUGUI;
		}

		private static (Image fill, TextMeshProUGUI barText, float nextY) MakeBar(RectTransform parent, string goName, string statLabel, Sprite gradientSprite, TMP_FontAsset font, Sprite sliderSprite, float topY)
		{
			GameObject gameObject = new GameObject(goName + "Bg", typeof(RectTransform));
			gameObject.transform.SetParent(parent, worldPositionStays: false);
			Image image = gameObject.AddComponent<Image>();
			if (sliderSprite != null)
			{
				image.sprite = sliderSprite;
				image.type = Image.Type.Sliced;
				image.color = new Color(1f, 1f, 1f, 0.9f);
			}
			else
			{
				image.color = new Color(0.08f, 0.08f, 0.08f, 0.75f);
			}
			image.raycastTarget = false;
			RectTransform component = gameObject.GetComponent<RectTransform>();
			component.anchorMin = new Vector2(0f, 1f);
			component.anchorMax = new Vector2(1f, 1f);
			component.pivot = new Vector2(0f, 1f);
			component.sizeDelta = new Vector2(0f, 16f);
			component.anchoredPosition = new Vector2(0f, topY);
			GameObject gameObject2 = new GameObject(goName + "Fill", typeof(RectTransform));
			gameObject2.transform.SetParent(gameObject.transform, worldPositionStays: false);
			Image image2 = gameObject2.AddComponent<Image>();
			image2.sprite = gradientSprite;
			image2.type = Image.Type.Filled;
			image2.fillMethod = Image.FillMethod.Horizontal;
			image2.fillOrigin = 0;
			image2.fillAmount = 1f;
			image2.color = Color.white;
			image2.raycastTarget = false;
			RectTransform component2 = gameObject2.GetComponent<RectTransform>();
			component2.anchorMin = Vector2.zero;
			component2.anchorMax = Vector2.one;
			component2.offsetMin = new Vector2(2f, 1f);
			component2.offsetMax = new Vector2(-2f, -1f);
			GameObject gameObject3 = new GameObject(goName + "Text", typeof(RectTransform));
			gameObject3.transform.SetParent(gameObject.transform, worldPositionStays: false);
			TextMeshProUGUI textMeshProUGUI = gameObject3.AddComponent<TextMeshProUGUI>();
			if (font != null)
			{
				textMeshProUGUI.font = font;
			}
			textMeshProUGUI.text = statLabel + "  0 / 0";
			textMeshProUGUI.fontSize = 8f;
			textMeshProUGUI.color = ColBarTxt;
			textMeshProUGUI.alignment = TextAlignmentOptions.Center;
			textMeshProUGUI.raycastTarget = false;
			textMeshProUGUI.overflowMode = TextOverflowModes.Truncate;
			RectTransform component3 = gameObject3.GetComponent<RectTransform>();
			component3.anchorMin = Vector2.zero;
			component3.anchorMax = Vector2.one;
			component3.offsetMin = Vector2.zero;
			component3.offsetMax = Vector2.zero;
			return (fill: image2, barText: textMeshProUGUI, nextY: topY - 16f);
		}
	}

	private const float IconSz = 20f;

	private const float IconGap = 3f;

	private const float WidthExtra = 0f;

	private const float RowHeight = 61f;

	private const float RowGap = 3f;

	private const float PadH = 6f;

	private const float PadTop = 4f;

	private const float PadBot = 2f;

	private const float BarH = 16f;

	private const float BarGap = 3f;

	private const float NameFontSz = 10f;

	private const float NameH = 12f;

	private const float NameIconGap = 2f;

	private const float BarTextSz = 8f;

	private const float RestedSz = 20f;

	private static readonly Color HpL = new Color(0.95f, 0.2f, 0.15f);

	private static readonly Color HpR = new Color(0.55f, 0.05f, 0.03f);

	private static readonly Color StaL = new Color(1f, 0.92f, 0.15f);

	private static readonly Color StaR = new Color(0.68f, 0.52f, 0.02f);

	private static readonly Color ColDarkOverlay = new Color(0f, 0f, 0f, 0.4f);

	private static readonly Color ColName = new Color(0.95f, 0.9f, 0.75f);

	private static readonly Color ColBarTxt = Color.white;

	private static Sprite _gradHp;

	private static Sprite _gradSta;

	private RectTransform _panelRoot;

	private float _cardWidth;

	private readonly List<CompanionRow> _rows = new List<CompanionRow>();

	private bool _built;

	private const string PrefKeyHudX = "HC_HudPanelOffsetX";

	private const string PrefKeyHudY = "HC_HudPanelOffsetY";

	private bool _dragMode;

	private bool _dragging;

	private Vector2 _dragMouseStart;

	private Vector2 _dragPanelStart;

	private Vector2 _defaultPosition;

	private Vector2 _userOffset;

	private static Sprite _sliderSprite;

	public static CompanionHudPanel Instance { get; private set; }

	internal static void EnsureInstance()
	{
		if (!(Instance != null))
		{
			GameObject gameObject = new GameObject("HC_CompanionHudPanelHost");
			Object.DontDestroyOnLoad(gameObject);
			gameObject.AddComponent<CompanionHudPanel>();
		}
	}

	private void Awake()
	{
		Instance = this;
	}

	private void OnDestroy()
	{
		if (Instance == this)
		{
			Instance = null;
		}
	}

	private void Update()
	{
		if (_built && _panelRoot == null)
		{
			_built = false;
			_rows.Clear();
		}
		if (!_built)
		{
			if (Minimap.instance == null || Minimap.instance.m_smallRoot == null)
			{
				return;
			}
			BuildUI();
			_built = true;
		}
		if (_panelRoot == null)
		{
			return;
		}
		IReadOnlyList<CompanionSetup> allCompanions = CompanionSetup.AllCompanions;
		bool flag = ModConfig.ShowMinimapHudPanel.Value && Minimap.instance != null && Minimap.instance.m_smallRoot != null && Minimap.instance.m_smallRoot.activeSelf && allCompanions.Count > 0;
		_panelRoot.gameObject.SetActive(flag);
		if (!flag)
		{
			if (_dragMode)
			{
				_dragMode = false;
				_dragging = false;
			}
			return;
		}
		if (Input.GetKeyDown(KeyCode.F7) && (CompanionInteractPanel.Instance == null || !CompanionInteractPanel.Instance.IsVisible))
		{
			_dragMode = !_dragMode;
			_dragging = false;
			if (_dragMode)
			{
				Player.m_localPlayer?.Message(MessageHud.MessageType.Center, ModLocalization.Loc("hc_msg_reposition_on"));
			}
			else
			{
				SaveHudOffset();
				Player.m_localPlayer?.Message(MessageHud.MessageType.Center, ModLocalization.Loc("hc_msg_reposition_off"));
			}
		}
		if (_dragMode)
		{
			HandleDragReposition();
		}
		EnsureRowCount(allCompanions.Count);
		for (int i = 0; i < _rows.Count; i++)
		{
			bool flag2 = i < allCompanions.Count && allCompanions[i] != null;
			_rows[i].RowRoot.gameObject.SetActive(flag2);
			if (flag2)
			{
				_rows[i].Refresh(allCompanions[i]);
			}
		}
		UpdatePanelSize(allCompanions.Count);
	}

	private void BuildUI()
	{
		RectTransform component = Minimap.instance.m_smallRoot.GetComponent<RectTransform>();
		Transform parent = component.parent;
		_cardWidth = component.sizeDelta.x + 0f;
		GameObject gameObject = new GameObject("HC_CompanionHudPanel", typeof(RectTransform));
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		_panelRoot = gameObject.GetComponent<RectTransform>();
		_panelRoot.anchorMin = new Vector2(1f, 1f);
		_panelRoot.anchorMax = new Vector2(1f, 1f);
		_panelRoot.pivot = new Vector2(1f, 1f);
		float num = component.anchoredPosition.y - component.sizeDelta.y;
		_defaultPosition = new Vector2(component.anchoredPosition.x, num - 4f);
		_panelRoot.anchoredPosition = _defaultPosition;
		_panelRoot.sizeDelta = new Vector2(_cardWidth, 67f);
		Image image = gameObject.AddComponent<Image>();
		image.color = new Color(0f, 0f, 0f, 0.5f);
		image.raycastTarget = false;
		ApplyHudOffset();
	}

	private void SaveHudOffset()
	{
		if (!(_panelRoot == null))
		{
			_userOffset = _panelRoot.anchoredPosition - _defaultPosition;
			PlayerPrefs.SetFloat("HC_HudPanelOffsetX", _userOffset.x);
			PlayerPrefs.SetFloat("HC_HudPanelOffsetY", _userOffset.y);
			PlayerPrefs.Save();
		}
	}

	private void HandleDragReposition()
	{
		if (_panelRoot == null)
		{
			return;
		}
		if (Input.GetMouseButtonDown(0) && RectTransformUtility.RectangleContainsScreenPoint(_panelRoot, Input.mousePosition))
		{
			_dragging = true;
			_dragMouseStart = Input.mousePosition;
			_dragPanelStart = _panelRoot.anchoredPosition;
		}
		if (_dragging && Input.GetMouseButton(0))
		{
			Vector2 vector = (Vector2)Input.mousePosition - _dragMouseStart;
			Canvas componentInParent = _panelRoot.GetComponentInParent<Canvas>();
			if (componentInParent != null)
			{
				vector /= componentInParent.scaleFactor;
			}
			_panelRoot.anchoredPosition = _dragPanelStart + vector;
		}
		if (Input.GetMouseButtonUp(0) && _dragging)
		{
			_dragging = false;
		}
	}

	private void EnsureRowCount(int needed)
	{
		while (_rows.Count < needed)
		{
			_rows.Add(new CompanionRow(_panelRoot, _rows.Count, GetFont(), _cardWidth));
		}
	}

	private void UpdatePanelSize(int activeCount)
	{
		if (!(_panelRoot == null))
		{
			float num = 6f + (float)activeCount * 61f + (float)Mathf.Max(0, activeCount - 1) * 3f;
			Vector2 sizeDelta = _panelRoot.sizeDelta;
			if (!Mathf.Approximately(sizeDelta.x, _cardWidth) || !Mathf.Approximately(sizeDelta.y, num))
			{
				_panelRoot.sizeDelta = new Vector2(_cardWidth, num);
			}
			for (int i = 0; i < _rows.Count; i++)
			{
				float y = 0f - (4f + (float)i * 64f);
				_rows[i].RowRoot.anchoredPosition = new Vector2(0f, y);
			}
		}
	}

	private void ApplyHudOffset()
	{
		_userOffset = new Vector2(PlayerPrefs.GetFloat("HC_HudPanelOffsetX", 0f), PlayerPrefs.GetFloat("HC_HudPanelOffsetY", 0f));
		if (_panelRoot != null)
		{
			_panelRoot.anchoredPosition = _defaultPosition + _userOffset;
		}
	}

	private static TMP_FontAsset GetFont()
	{
		TextMeshProUGUI[] array = Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None);
		TextMeshProUGUI[] array2 = array;
		foreach (TextMeshProUGUI textMeshProUGUI in array2)
		{
			if (textMeshProUGUI != null && textMeshProUGUI.font != null)
			{
				return textMeshProUGUI.font;
			}
		}
		return null;
	}

	private static Sprite MakeGradientSprite(Color top, Color bottom)
	{
		Texture2D texture2D = new Texture2D(1, 4, TextureFormat.RGBA32, mipChain: false);
		texture2D.filterMode = FilterMode.Bilinear;
		texture2D.wrapMode = TextureWrapMode.Clamp;
		for (int i = 0; i < 4; i++)
		{
			texture2D.SetPixel(0, i, Color.Lerp(bottom, top, (float)i / 3f));
		}
		texture2D.Apply();
		return Sprite.Create(texture2D, new Rect(0f, 0f, 1f, 4f), new Vector2(0.5f, 0.5f), 1f);
	}

	internal static Sprite GetSliderSprite()
	{
		if (_sliderSprite != null)
		{
			return _sliderSprite;
		}
		Texture2D texture2D = TextureLoader.LoadUITexture("SliderBackground");
		if (texture2D == null)
		{
			return null;
		}
		_sliderSprite = Sprite.Create(texture2D, new Rect(0f, 0f, texture2D.width, texture2D.height), new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect, new Vector4(6f, 6f, 6f, 6f));
		return _sliderSprite;
	}
}
