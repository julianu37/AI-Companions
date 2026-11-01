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

	// 1.5.71: hiring another companion at Odin's Shrine uses this panel too
	private static bool s_hireNext;

	private bool _hire;

	public static void ShowPanel()
	{
		if (!(Instance != null))
		{
			GameObject gameObject = new GameObject("HC_StarterCompanionPanel");
			Object.DontDestroyOnLoad(gameObject);
			Instance = gameObject.AddComponent<StarterCompanionPanel>();
		}
	}

	public static void ShowHirePanel()
	{
		if (!(Instance != null))
		{
			s_hireNext = true;
			ShowPanel();
		}
	}

	private void Awake()
	{
		_canvas = base.gameObject.AddComponent<Canvas>();
		_canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		_canvas.sortingOrder = ModUiLayers.StarterCompanionPanel;
		base.gameObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		base.gameObject.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920f, 1080f);
		// 1.5.80: scaled like the game's own GUI
		GameLook.MatchGameScale(base.gameObject.GetComponent<CanvasScaler>());
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
		// Fase 3.13: 860 instead of 800 - the middle column gained the name band (label + field, 52px) and the
		// description still gets ~394px, so the starter text fits without being squeezed (see the layout log).
		component2.sizeDelta = new Vector2(920f, 860f);
		Image component3 = _container.GetComponent<Image>();
		// 1.5.78: the game's own panel background (was a packed texture)
		GameLook.ApplyPanel(component3, new Color(0.05f, 0.05f, 0.08f, 0.95f));
		component3.raycastTarget = true;
		TMP_FontAsset tMP_FontAsset = ResolveFont();
		GameObject gameObject = ResolveButtonTemplate();
		if (CompanionsPlugin.LogDetailed) CompanionsPlugin.Log.LogDebug("[StarterPanel] Font=" + (tMP_FontAsset?.name ?? "null") + ", ButtonTemplate=" + (gameObject?.name ?? "null"));
		_hire = s_hireNext;
		s_hireNext = false;
		_panel = new CompanionPanel
		{
			StarterMode = true,
			HireMode = _hire
		};
		_panel.OnSpawnConfirmed = OnConfirm;
		float num = 920f;
		float panelHeight = 860f;
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
		if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log.LogInfo("[StarterPanel] Opened customisation panel");
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
		// Fase 3.10: the AI choices of the panel (voice + personality) go to the companion that is being born; the
		// draft is written to its ZDO in the spawn, next to the name and the appearance
		CompanionAiDraft aiDraft = _panel?.AiDraft();
		// 1.5.6 (restored in 1.9.4): the Shrine's hire pays the offering. This branch was lost when the 1.5.6 merge
		// resolved OnConfirm in favour of our aiDraft version: the panel ran the starter's FREE spawn and
		// StarterFix.MarkSpawned() consumed the free starter of this world on top of it.
		if (_hire)
		{
			if (CompanionManager.HireWithCoins(appearance, aiDraft))
			{
				if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log.LogInfo("[StarterPanel] Companion summoned for the offering - closing panel");
				Close();
			}
			return;
		}
		CompanionManager.SpawnStarterWithAppearance(appearance, aiDraft);
		CompanionsPlugin.Log.LogInfo("[StarterPanel] Companion spawned — closing panel"
			+ (aiDraft != null && !aiDraft.IsEmpty
				? $" (AI profile: voice {(aiDraft.Voice > 0 ? aiDraft.Voice.ToString() : "default")}, personality {(aiDraft.HasPersona ? "\"" + aiDraft.Preset + "\"" : "default")})"
				: " (AI profile: the [IA] defaults)"));
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
			if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log.LogInfo(_hire ? "[StarterPanel] Summon cancelled" : "[StarterPanel] Skipped via close button — will re-appear next session");
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
			if (CompanionsPlugin.LogDetailed) CompanionsPlugin.Log.LogDebug("[StarterPanel] Initial Refresh() complete");
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
			// Fase 3.10: a player typing the personality loses the text box first, and Esc only closes the panel on
			// the next press (the same order the AI panel uses)
			if (_panel != null && _panel.ReleaseTextFocus())
			{
				return;
			}
			CompanionsPlugin.Log.LogInfo(_hire ? "[StarterPanel] Summon cancelled" : "[StarterPanel] Skipped — will re-appear next session");
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
		if (CompanionsPlugin.LogDetailed) CompanionsPlugin.Log.LogDebug("[StarterPanel] Destroyed");
	}
}
