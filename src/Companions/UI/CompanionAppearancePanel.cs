using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Companions;

/// <summary>
/// The appearance editor of a companion that already exists: the window behind the "Aparência" button of a companion's
/// tab in the AI panel.
///
/// <para><b>What it is.</b> A host for <see cref="CompanionPanel"/> in its edit mode
/// (<see cref="CompanionPanel.AppearanceEditMode"/>), which is the starter's left column (gender, hair, beard, skin and
/// hair colours) and the starter's 3D preview, without the AI section, the name field or "Invocar". The panel itself
/// never touches the companion: it reads the look from the target's ZDO and hands the result back through
/// <see cref="CompanionPanel.OnAppearanceConfirmed"/> - this class is what applies it. There is only one implementation
/// of the preview on purpose; a second copy would have to keep the RenderSettings trick, the clone rig and the
/// reflection into <c>VisEquipment</c> in sync forever.</para>
///
/// <para><b>What "Aplicar" changes and what it does not.</b> It calls
/// <see cref="CompanionSetup.ApplyAppearance"/> on the <b>real</b> companion (the look changes right there, and the
/// <c>VisEquipment</c> setters it uses are the game's own networked ones) and writes <c>HC_Appearance</c> through
/// <see cref="CompanionSetup.MHR_WriteOrQueue"/> - the ownership-aware write of the 3.19 audit, which claims the ZDO
/// and keeps the write queued (with a warning) when the claim has not arrived yet. <b>Nothing else</b> is written:
/// there is no respawn (this is not <see cref="CompanionManager.ChangeTier"/>), so the tier, the name, the AI profile,
/// the inventory, the equipment, the skills and the position all stay exactly as they are, and the companion is not
/// disturbed from whatever it was doing.</para>
///
/// <para><b>Closing.</b> "Cancelar", Esc, the X and the gamepad back button all close without writing anything. The
/// window also closes itself when the player dies or leaves the world. The AI panel stays open behind it (this window
/// is one layer above, see <see cref="ModUiLayers.CompanionAppearancePanel"/>) and gets the keyboard back when it goes
/// away - its Update ignores keys while this one is up, so Esc never closes both.</para>
/// </summary>
internal sealed class CompanionAppearancePanel : MonoBehaviour
{
	// The starter's width, and only as tall as the editor needs: the middle column has no AI section, no name field and
	// no description, so the extra 160px of the starter would be empty. The three columns are the starter's geometry,
	// computed the same way (three equal columns with the same margins), which is what keeps the preview's aspect ratio
	// close to the starter's.
	private const float PanelW = 920f;

	private const float PanelH = 700f;

	private const float Margin = 6f;

	private const float ColumnGap = 6f;

	private const float ButtonHeight = 44f;

	private CompanionPanel _panel;

	private GameObject _backdrop;

	private GameObject _container;

	private Canvas _canvas;

	private CompanionSetup _target;

	private bool _refreshPending;

	private static Sprite _panelBgSprite;

	internal static CompanionAppearancePanel Instance { get; private set; }

	internal bool IsVisible => _panel?.Root != null && _panel.Root.activeSelf;

	/// <summary>Is the editor on screen? Read by the AI panel (which stays behind it), by the F8 panel and by the input
	/// guards of the mod, none of which may act while a modal window of the mod is up.</summary>
	internal static bool IsOpen => Instance != null && Instance.IsVisible;

	/// <summary>
	/// Opens the editor for <paramref name="setup"/>. A dead companion is refused with a localized message: its look is
	/// already written into its tombstone (<c>HC_Respawn_Appearance</c>), and the resurrection would put the old one
	/// back - the same reason the rename and the tier change refuse it.
	/// </summary>
	internal static void Open(CompanionSetup setup)
	{
		if (setup == null)
		{
			CompanionChatAI.Announce(ModLocalization.Loc("hc_ai_comp_gone"));
			return;
		}
		Character character = setup.GetComponent<Character>();
		if (character == null || character.GetHealth() <= 0f)
		{
			CompanionChatAI.Announce(ModLocalization.Loc("hc_ui_appearance_dead"));
			return;
		}
		if (IsOpen)
		{
			// one editor at a time: the button lives on one tab, and a second window would build a second preview clone
			// and a second camera for nothing
			CompanionsPlugin.Log?.LogDebug("[AppearancePanel] ja esta aberto - pedido ignorado");
			return;
		}
		GameObject holder = new GameObject("HC_CompanionAppearancePanel");
		Object.DontDestroyOnLoad(holder);
		Instance = holder.AddComponent<CompanionAppearancePanel>();
		Instance.Init(setup);
	}

	private void Awake()
	{
		if (Instance != null && Instance != this)
		{
			Object.Destroy(base.gameObject);
			return;
		}
		Instance = this;
	}

	/// <summary>Builds the window around a <see cref="CompanionPanel"/> in its edit mode. Called right after the
	/// component exists, so the canvas and the panel are built once, from the companion that was asked for.</summary>
	private void Init(CompanionSetup setup)
	{
		_target = setup;

		_canvas = base.gameObject.AddComponent<Canvas>();
		_canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		_canvas.sortingOrder = ModUiLayers.CompanionAppearancePanel;
		base.gameObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		base.gameObject.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920f, 1080f);
		base.gameObject.AddComponent<GraphicRaycaster>();

		_backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
		_backdrop.transform.SetParent(base.transform, worldPositionStays: false);
		RectTransform backdropRT = _backdrop.GetComponent<RectTransform>();
		backdropRT.anchorMin = Vector2.zero;
		backdropRT.anchorMax = Vector2.one;
		backdropRT.offsetMin = Vector2.zero;
		backdropRT.offsetMax = Vector2.zero;
		// the backdrop is what makes this a modal window: it swallows every click that is not on the window, so the AI
		// panel behind it (and the game) cannot be touched while the player is editing
		_backdrop.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.85f);

		_container = new GameObject("PanelContainer", typeof(RectTransform), typeof(Image));
		_container.transform.SetParent(base.transform, worldPositionStays: false);
		RectTransform containerRT = _container.GetComponent<RectTransform>();
		containerRT.anchorMin = new Vector2(0.5f, 0.5f);
		containerRT.anchorMax = new Vector2(0.5f, 0.5f);
		containerRT.pivot = new Vector2(0.5f, 0.5f);
		containerRT.sizeDelta = new Vector2(PanelW, PanelH);
		Image containerImage = _container.GetComponent<Image>();
		Sprite panelBgSprite = GetPanelBgSprite();
		if (panelBgSprite != null)
		{
			containerImage.sprite = panelBgSprite;
			containerImage.type = Image.Type.Simple;
			containerImage.color = Color.white;
		}
		else
		{
			containerImage.color = new Color(0.05f, 0.05f, 0.08f, 0.95f);
		}
		containerImage.raycastTarget = true;

		TMP_FontAsset font = ResolveFont();
		GameObject buttonTemplate = ResolveButtonTemplate();
		float columnW = (PanelW - Margin * 2f - ColumnGap * 2f) / 3f;
		float leftXL = Margin;
		float leftXR = leftXL + columnW;
		float midXL = leftXR + ColumnGap;
		float midXR = midXL + columnW;
		float rightXL = midXR + ColumnGap;
		float rightXR = rightXL + columnW;

		_panel = new CompanionPanel
		{
			AppearanceEditMode = true,
			EditTarget = setup
		};
		_panel.OnAppearanceConfirmed = OnApply;
		_panel.OnAppearanceCancelled = Cancel;
		_panel.Build(_container.transform, 6f, 6f, font, buttonTemplate, ButtonHeight,
			leftXL, leftXR, midXL, midXR, rightXL, rightXR, PanelH);
		_panel.Root.SetActive(value: true);
		BuildCloseButton(_container.transform, font);
		EnableGamepadNavigation();
		_refreshPending = true;
		CompanionsPlugin.Log?.LogInfo($"[AppearancePanel] Aberto para \"{NameOf(setup)}\" (tier={CompanionTierData.LocalName(CompanionTierOf(setup))}) "
			+ $"- preview + coluna da aparencia, sem secao de IA; painel {PanelW:0}x{PanelH:0}, botoes {ButtonHeight:0}px");
	}

	private static Sprite GetPanelBgSprite()
	{
		if (_panelBgSprite != null)
		{
			return _panelBgSprite;
		}
		Texture2D texture = TextureLoader.LoadUITexture("PanelBackground");
		if (texture == null)
		{
			return null;
		}
		_panelBgSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
		_panelBgSprite.name = "AppearancePanelBackground";
		return _panelBgSprite;
	}

	// The font and the button of the game, resolved the same way the starter panel and the other windows of the mod do
	// (each one resolves its own: they are three lines, and sharing them would couple two hosts that have nothing else
	// in common).
	private static TMP_FontAsset ResolveFont()
	{
		if (InventoryGui.instance != null)
		{
			TMP_Text text = InventoryGui.instance.GetComponentInChildren<TMP_Text>(includeInactive: true);
			if (text?.font != null)
			{
				return text.font;
			}
		}
		if (Hud.instance != null)
		{
			TMP_Text text2 = Hud.instance.GetComponentInChildren<TMP_Text>(includeInactive: true);
			if (text2?.font != null)
			{
				return text2.font;
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

	/// <summary>"Aplicar": the look the player built, on the real companion and in its ZDO - and the window goes away.
	/// No respawn, no new object: only the appearance changes.</summary>
	private void OnApply(CompanionAppearance appearance)
	{
		CompanionSetup setup = _target;
		if (setup == null || appearance == null)
		{
			Close();
			return;
		}
		string who = NameOf(setup);
		Character character = setup.GetComponent<Character>();
		if (character == null || character.GetHealth() <= 0f)
		{
			// it died while the editor was open: its look is already in its tombstone, and writing HC_Appearance now
			// would be undone by the resurrection - same rule as Open(), said again because the window was already open
			CompanionChatAI.Announce(ModLocalization.Loc("hc_ui_appearance_dead"));
			Close();
			return;
		}
		// Fase 3.21: the game's own setters (VisEquipment.SetHairItem/SetBeardItem/SetModel/SetSkinColor/SetHairColor)
		// write the look into the ZDO themselves, but only when this game owns it - so the claim comes first and the
		// visual apply travels with the durable write (if the claim hasn't arrived yet, both run on the next tick).
		string data = appearance.Serialize();
		bool written = setup.MHR_WriteOrQueue(delegate
		{
			setup.ApplyAppearance(appearance);
			setup.GetComponent<ZNetView>()?.GetZDO()?.Set(CompanionSetup.AppearanceHash, data);
		});
		CompanionsPlugin.Log?.LogInfo($"[AppearancePanel] \"{who}\": aparencia aplicada (gender={appearance.ModelIndex}, "
			+ $"hair={appearance.HairItem}, beard={appearance.BeardItem}); HC_Appearance "
			+ (written ? "gravado agora (com posse do ZDO)" : "na fila (sem posse do ZDO ainda - aviso se nao chegar)"));
		CompanionChatAI.Announce(ModLocalization.LocFmt("hc_ui_appearance_applied", who));
		Close();
	}

	/// <summary>"Cancelar", Esc, the X and the gamepad back button: the window goes away and nothing is written - the
	/// companion keeps the look it had when the editor opened (the preview clone is torn down with the panel).</summary>
	private void Cancel()
	{
		CompanionsPlugin.Log?.LogInfo("[AppearancePanel] Fechado sem gravar (Cancelar/Esc/X)");
		Close();
	}

	private void Close()
	{
		Object.Destroy(base.gameObject);
	}

	private void OnDestroy()
	{
		// the panel owns the preview clone, the camera, the RenderTexture and its own root: Teardown is what frees them
		_panel?.Teardown();
		_panel = null;
		_target = null;
		if (Instance == this)
		{
			Instance = null;
		}
		CompanionsPlugin.Log?.LogDebug("[AppearancePanel] Fechado");
	}

	private void Update()
	{
		if (_refreshPending)
		{
			_refreshPending = false;
			_panel?.Refresh();
		}
		_panel?.UpdatePerFrame();
		Player player = Player.m_localPlayer;
		if (player == null || player.IsDead())
		{
			// the same rule as the AI panel: a window about a companion (or a player) that is gone has nothing to show
			Close();
			return;
		}
		if (_target == null)
		{
			// the companion was destroyed while the editor was up (dismissed, world change): nothing left to edit
			CompanionsPlugin.Log?.LogDebug("[AppearancePanel] companheiro sumiu - fechando o editor");
			Close();
			return;
		}
		Cursor.lockState = CursorLockMode.None;
		Cursor.visible = true;
		if (ZInput.IsGamepadActive())
		{
			EventSystem current = EventSystem.current;
			if (current != null && current.currentSelectedGameObject == null)
			{
				Selectable first = _container.GetComponentInChildren<Selectable>(includeInactive: false);
				if (first != null)
				{
					current.SetSelectedGameObject(first.gameObject);
				}
			}
		}
		// there is no text box in this mode (the starter's name/personality fields are not built), so Esc is the window's
		if (Input.GetKeyDown(KeyCode.Escape) || ZInput.GetButtonDown("JoyBack") || ZInput.GetButtonDown("JoyButtonB"))
		{
			Cancel();
		}
	}

	/// <summary>The X of the window: the same as "Cancelar" (it never applies).</summary>
	private void BuildCloseButton(Transform parent, TMP_FontAsset font)
	{
		GameObject buttonGO = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
		buttonGO.transform.SetParent(parent, worldPositionStays: false);
		RectTransform rt = buttonGO.GetComponent<RectTransform>();
		rt.anchorMin = new Vector2(1f, 1f);
		rt.anchorMax = new Vector2(1f, 1f);
		rt.pivot = new Vector2(1f, 1f);
		rt.sizeDelta = new Vector2(36f, 36f);
		rt.anchoredPosition = new Vector2(-2f, -2f);
		buttonGO.GetComponent<Image>().color = new Color(0.3f, 0.1f, 0.1f, 0.9f);
		buttonGO.GetComponent<Button>().onClick.AddListener(delegate
		{
			Cancel();
		});
		GameObject labelGO = new GameObject("X", typeof(RectTransform));
		labelGO.transform.SetParent(buttonGO.transform, worldPositionStays: false);
		TextMeshProUGUI label = labelGO.AddComponent<TextMeshProUGUI>();
		if (font != null)
		{
			label.font = font;
		}
		label.text = "X";
		label.fontSize = 20f;
		label.color = Color.white;
		label.alignment = TextAlignmentOptions.Center;
		label.raycastTarget = false;
		RectTransform labelRT = labelGO.GetComponent<RectTransform>();
		labelRT.anchorMin = Vector2.zero;
		labelRT.anchorMax = Vector2.one;
		labelRT.offsetMin = Vector2.zero;
		labelRT.offsetMax = Vector2.zero;
	}

	private void EnableGamepadNavigation()
	{
		Color highlighted = new Color(1f, 0.85f, 0.5f, 1f);
		Color pressed = new Color(0.9f, 0.7f, 0.3f, 1f);
		Selectable[] selectables = _container.GetComponentsInChildren<Selectable>(includeInactive: true);
		foreach (Selectable selectable in selectables)
		{
			selectable.navigation = new Navigation
			{
				mode = Navigation.Mode.Automatic
			};
			if (selectable.transition == Selectable.Transition.None)
			{
				selectable.transition = Selectable.Transition.ColorTint;
				ColorBlock colors = selectable.colors;
				colors.highlightedColor = highlighted;
				colors.selectedColor = highlighted;
				colors.pressedColor = pressed;
				selectable.colors = colors;
			}
		}
	}

	private static string NameOf(CompanionSetup setup)
	{
		string stored = setup != null ? setup.GetComponent<ZNetView>()?.GetZDO()?.GetString(CompanionSetup.NameHash) : null;
		if (!string.IsNullOrEmpty(stored))
		{
			return stored;
		}
		Character character = setup != null ? setup.GetComponent<Character>() : null;
		if (character != null && !string.IsNullOrEmpty(character.m_name))
		{
			return character.m_name;
		}
		return ModLocalization.Loc("hc_msg_name_default");
	}

	/// <summary>The tier of a companion, from the prefab hash its ZDO was spawned with (for the log line only).</summary>
	private static CompanionTierDef CompanionTierOf(CompanionSetup setup)
	{
		ZDO zdo = setup != null ? setup.GetComponent<ZNetView>()?.GetZDO() : null;
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
}
