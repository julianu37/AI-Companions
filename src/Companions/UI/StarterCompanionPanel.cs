using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Companions;

public class StarterCompanionPanel : MonoBehaviour
{
	private CompanionPanel _panel;

	private GameObject _backdrop;

	private Canvas _canvas;

	private GameObject _container;

	private static Sprite _panelBgSprite;

	private bool _refreshPending;

	public static StarterCompanionPanel Instance { get; private set; }

	public bool IsVisible
	{
		get
		{
			if (_panel?.Root != null)
			{
				return _panel.Root.activeSelf;
			}
			return false;
		}
	}

	public static void ShowPanel()
	{
		if (!(Instance != null))
		{
			GameObject gameObject = new GameObject("HC_StarterCompanionPanel");
			Object.DontDestroyOnLoad(gameObject);
			Instance = gameObject.AddComponent<StarterCompanionPanel>();
		}
	}

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
		_panelBgSprite.name = "StarterPanelBackground";
		return _panelBgSprite;
	}

	private void Awake()
	{
		_canvas = base.gameObject.AddComponent<Canvas>();
		_canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		_canvas.sortingOrder = ModUiLayers.StarterCompanionPanel;
		base.gameObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		base.gameObject.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920f, 1080f);
		base.gameObject.AddComponent<GraphicRaycaster>();
		_backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
		_backdrop.transform.SetParent(base.transform, worldPositionStays: false);
		RectTransform component = _backdrop.GetComponent<RectTransform>();
		component.anchorMin = Vector2.zero;
		component.anchorMax = Vector2.one;
		component.offsetMin = Vector2.zero;
		component.offsetMax = Vector2.zero;
		_backdrop.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.85f);
		_container = new GameObject("PanelContainer", typeof(RectTransform), typeof(Image));
		_container.transform.SetParent(base.transform, worldPositionStays: false);
		RectTransform component2 = _container.GetComponent<RectTransform>();
		component2.anchorMin = new Vector2(0.5f, 0.5f);
		component2.anchorMax = new Vector2(0.5f, 0.5f);
		component2.pivot = new Vector2(0.5f, 0.5f);
		component2.sizeDelta = new Vector2(920f, 560f);
		Image component3 = _container.GetComponent<Image>();
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
		component3.raycastTarget = true;
		TMP_FontAsset tMP_FontAsset = ResolveFont();
		GameObject gameObject = ResolveButtonTemplate();
		CompanionsPlugin.Log.LogDebug("[StarterPanel] Font=" + (tMP_FontAsset?.name ?? "null") + ", ButtonTemplate=" + (gameObject?.name ?? "null"));
		_panel = new CompanionPanel
		{
			StarterMode = true
		};
		_panel.OnSpawnConfirmed = OnConfirm;
		float num = 920f;
		float panelHeight = 560f;
		float num2 = 6f;
		float num3 = 6f;
		float num4 = (num - num2 * 2f - num3 * 2f) / 3f;
		float num5 = num2;
		float num6 = num5 + num4;
		float num7 = num6 + num3;
		float num8 = num7 + num4;
		float num9 = num8 + num3;
		float rightXR = num9 + num4;
		_panel.Build(_container.transform, 6f, 6f, tMP_FontAsset, gameObject, 48f, num5, num6, num7, num8, num9, rightXR, panelHeight);
		_panel.Root.SetActive(value: true);
		BuildCloseButton(_container.transform, tMP_FontAsset);
		EnableGamepadNavigation();
		_refreshPending = true;
		CompanionsPlugin.Log.LogInfo("[StarterPanel] Opened customisation panel");
	}

	private static TMP_FontAsset ResolveFont()
	{
		if (InventoryGui.instance != null)
		{
			TMP_Text componentInChildren = InventoryGui.instance.GetComponentInChildren<TMP_Text>(includeInactive: true);
			if (componentInChildren?.font != null)
			{
				return componentInChildren.font;
			}
		}
		if (Hud.instance != null)
		{
			TMP_Text componentInChildren2 = Hud.instance.GetComponentInChildren<TMP_Text>(includeInactive: true);
			if (componentInChildren2?.font != null)
			{
				return componentInChildren2.font;
			}
		}
		return null;
	}

	private static GameObject ResolveButtonTemplate()
	{
		if (InventoryGui.instance != null && InventoryGui.instance.m_craftButton != null)
		{
			return InventoryGui.instance.m_craftButton.gameObject;
		}
		return null;
	}

	private void OnConfirm(CompanionAppearance appearance)
	{
		CompanionManager.SpawnStarterWithAppearance(appearance);
		CompanionsPlugin.Log.LogInfo("[StarterPanel] Companion spawned — closing panel");
		Close();
	}

	private void BuildCloseButton(Transform parent, TMP_FontAsset font)
	{
		GameObject gameObject = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		RectTransform component = gameObject.GetComponent<RectTransform>();
		component.anchorMin = new Vector2(1f, 1f);
		component.anchorMax = new Vector2(1f, 1f);
		component.pivot = new Vector2(1f, 1f);
		component.sizeDelta = new Vector2(36f, 36f);
		component.anchoredPosition = new Vector2(-2f, -2f);
		gameObject.GetComponent<Image>().color = new Color(0.3f, 0.1f, 0.1f, 0.9f);
		gameObject.GetComponent<Button>().onClick.AddListener(delegate
		{
			CompanionsPlugin.Log.LogInfo("[StarterPanel] Skipped via close button — will re-appear next session");
			Close();
		});
		GameObject gameObject2 = new GameObject("X", typeof(RectTransform));
		gameObject2.transform.SetParent(gameObject.transform, worldPositionStays: false);
		TextMeshProUGUI textMeshProUGUI = gameObject2.AddComponent<TextMeshProUGUI>();
		if (font != null)
		{
			textMeshProUGUI.font = font;
		}
		textMeshProUGUI.text = "X";
		textMeshProUGUI.fontSize = 20f;
		textMeshProUGUI.color = Color.white;
		textMeshProUGUI.alignment = TextAlignmentOptions.Center;
		textMeshProUGUI.raycastTarget = false;
		RectTransform component2 = gameObject2.GetComponent<RectTransform>();
		component2.anchorMin = Vector2.zero;
		component2.anchorMax = Vector2.one;
		component2.offsetMin = Vector2.zero;
		component2.offsetMax = Vector2.zero;
	}

	private void EnableGamepadNavigation()
	{
		Color color = new Color(1f, 0.85f, 0.5f, 1f);
		Color pressedColor = new Color(0.9f, 0.7f, 0.3f, 1f);
		Selectable[] componentsInChildren = _container.GetComponentsInChildren<Selectable>(includeInactive: true);
		foreach (Selectable selectable in componentsInChildren)
		{
			selectable.navigation = new Navigation
			{
				mode = Navigation.Mode.Automatic
			};
			if (selectable.transition == Selectable.Transition.None)
			{
				selectable.transition = Selectable.Transition.ColorTint;
				ColorBlock colors = selectable.colors;
				colors.highlightedColor = color;
				colors.selectedColor = color;
				colors.pressedColor = pressedColor;
				selectable.colors = colors;
			}
		}
	}

	private void Update()
	{
		if (_refreshPending)
		{
			_refreshPending = false;
			_panel?.Refresh();
			CompanionsPlugin.Log.LogDebug("[StarterPanel] Initial Refresh() complete");
		}
		_panel?.UpdatePerFrame();
		Cursor.lockState = CursorLockMode.None;
		Cursor.visible = true;
		if (ZInput.IsGamepadActive())
		{
			EventSystem current = EventSystem.current;
			if (current != null && current.currentSelectedGameObject == null)
			{
				Selectable componentInChildren = _container.GetComponentInChildren<Selectable>(includeInactive: false);
				if (componentInChildren != null)
				{
					current.SetSelectedGameObject(componentInChildren.gameObject);
				}
			}
		}
		if (Input.GetKeyDown(KeyCode.Escape) || ZInput.GetButtonDown("JoyBack"))
		{
			CompanionsPlugin.Log.LogInfo("[StarterPanel] Skipped — will re-appear next session");
			Close();
		}
	}

	private void Close()
	{
		Object.Destroy(base.gameObject);
	}

	private void OnDestroy()
	{
		_panel?.Teardown();
		_panel = null;
		if (Instance == this)
		{
			Instance = null;
		}
		CompanionsPlugin.Log.LogDebug("[StarterPanel] Destroyed");
	}
}
