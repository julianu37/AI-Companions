using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Companions;

/// <summary>
/// Fase 3.7: the in-game AI panel. Everything the AI layer reads from the .cfg can be changed here without leaving the
/// game: the personality (a preset from <see cref="CompanionPersona"/> or the player's own text), the voice of the
/// local TTS (<see cref="CompanionTts"/> - which voice, volume, speed, what gets spoken), the free conversation and the
/// memory size, plus the push-to-talk key of the voice orders (captured with the mod's own
/// <see cref="KeyBindListener"/>, the same one the F8 panel uses).
///
/// Shape (deliberately the same shape as the mod's other panels):
/// <list type="bullet">
/// <item>a wooden panel (<c>PanelBackground</c>) over a dark backdrop, opened/closed by <c>[IA] PainelTecla</c>
/// (default <b>I</b>), by <b>Esc</b> and by the X button; the panel blocks the player's input while it is open (the
/// same Harmony guards the other panels use) and hides itself when the player dies or leaves the world; it also
/// refuses to <b>open</b> over a window that owns the mouse and the keyboard - the game's inventory, map, menu,
/// text input, console, store, the chat with focus - or over the mod's own F8 panel (which in turn refuses to open
/// while this one is visible), so the two panels are never on top of each other;</item>
/// <item>the controls are built <b>once</b> (first open) and only shown/hidden afterwards - nothing is allocated per
/// frame; the only per-frame work is the panel key, the cursor and a status refresh <b>twice a second</b>;</item>
/// <item>every control writes straight into its <see cref="ConfigEntry"/>, and BepInEx saves the .cfg on the spot
/// (<c>ConfigFile.SaveOnConfigSet</c> is on by default), so the change is live and permanent;</item>
/// <item>a change is applied at the next use: the voice at the next line (the synthesis worker reads the config when
/// it runs), the personality/memory at the next message (the prompt is built per message), the volume at the next
/// audio.</item>
/// </list>
///
/// The panel needs no companion nearby (it is a global setting of the mod): the title just names the companion that
/// would answer right now, and the "Ouvir" preview uses that same companion as the 3D source of the voice.
///
/// Why the dropdown is hand-built: a <c>TMP_Dropdown</c> needs a whole template (scroll view, mask, toggle items)
/// instantiated at runtime, and the game's legacy <c>Dropdown</c> renders with the legacy <c>Text</c> (whose default
/// font Valheim removed). This one is a field button plus a list of buttons - the same primitives the mod already
/// builds everywhere else, drawn on an overlay layer that is the last child of the panel and that carries a
/// <c>Canvas</c> of its own (the same trick Unity's own dropdown template uses), so an open list is never clipped
/// and never drawn behind a control that happens to be created after it.
/// </summary>
internal sealed class CompanionAiPanel : MonoBehaviour
{
	// ------------------------------------------------------------------ layout

	private const float PanelW = 680f;

	private const float PanelH = 516f;

	/// <summary>Height of a control row (the row backgrounds are the same height).</summary>
	private const float RowH = 34f;

	/// <summary>Height of one option in a dropdown list.</summary>
	private const float ItemH = 30f;

	/// <summary>Left edge of the controls and the right edge they stop at (panel-local coordinates).</summary>
	private const float LeftX = 8f;

	private const float RightEdge = 672f;

	/// <summary>Width of the "setting name" label column.</summary>
	private const float LabelW = 150f;

	/// <summary>First x of a control that sits after the label column.</summary>
	private const float FieldX = LeftX + LabelW + 8f;

	// Top of each row, measured down from the top of the panel (rows step by RowH + 8).
	private const float RowPersonality = 40f;

	private const float RowFreeText = 82f;

	private const float RowVoice = 124f;

	private const float RowVolume = 166f;

	private const float RowKey = 208f;

	private const float RowToggleA = 250f;

	private const float RowToggleB = 292f;

	private const float RowMemory = 334f;

	private const float RowSpeed = 376f;

	// The footer is two two-line blocks over their own dark strip (see BuildFooter): the summary of what is in use
	// right now and the hint line. It used to be two 11px dark-grey single lines that were unreadable over the wooden
	// panel - and the second one was clipped by the bottom edge of the panel.
	private const float RowStatus = 418f;

	private const float RowHint = 462f;

	/// <summary>Height of one line of the footer (the blocks are two lines tall).</summary>
	private const float FooterLineH = 18f;

	/// <summary>How often the status line and the "Ouvir" button are refreshed (never per frame).</summary>
	private const float StatusInterval = 0.5f;

	/// <summary>Sorting order of the panel's own canvas (it is the whole mod UI layer: 33).</summary>
	private const int PanelSortingOrder = 33;

	/// <summary>Sorting order of the overlay canvas that holds the dropdown lists: one step above the panel, so the
	/// list of an open dropdown is drawn - and clicked - above every control of the panel.</summary>
	private const int OverlaySortingOrder = PanelSortingOrder + 1;

	// ------------------------------------------------------------------ colours

	private static readonly Color LabelColor = new Color(1f, 0.9f, 0.5f, 1f);

	private static readonly Color ValueColor = new Color(0.83f, 0.52f, 0.18f, 1f);

	private static readonly Color GoldColor = new Color(0.83f, 0.64f, 0.31f, 1f);

	private static readonly Color DimColor = new Color(0.62f, 0.62f, 0.62f, 1f);

	private static readonly Color RowColor = new Color(0.08f, 0.08f, 0.1f, 0.55f);

	private static readonly Color ButtonColor = new Color(0.28f, 0.26f, 0.22f, 0.95f);

	private static readonly Color ItemColor = new Color(0.16f, 0.15f, 0.13f, 0.98f);

	private static readonly Color OffColor = new Color(0.3f, 0.3f, 0.33f, 0.95f);

	private static readonly Color HandleColor = new Color(0.95f, 0.85f, 0.65f, 1f);

	/// <summary>Footer text (the "what is in use right now" summary): warm near-white, readable over the dark strip.</summary>
	private static readonly Color FooterColor = new Color(1f, 0.94f, 0.8f, 1f);

	/// <summary>Footer hint line: a touch dimmer than the summary, still clearly readable.</summary>
	private static readonly Color FooterHintColor = new Color(0.86f, 0.86f, 0.88f, 1f);

	/// <summary>The dark strip behind the footer, so the text never sits straight on the wooden panel.</summary>
	private static readonly Color FooterBackColor = new Color(0.04f, 0.04f, 0.05f, 0.82f);

	// ------------------------------------------------------------------ state

	private bool _visible;

	private GameObject _backdrop;

	private GameObject _root;

	private Transform _overlay;

	/// <summary>The invisible click catcher of the overlay: active only while a dropdown list is open, so clicking
	/// anywhere else on the panel closes the list instead of doing nothing (see <see cref="BuildListCatcher"/>).</summary>
	private GameObject _listCatcher;

	private TMP_FontAsset _font;

	private Sprite _panelSprite;

	private TMP_Text _status;

	private TMP_Text _title;

	private TMP_InputField _freeText;

	private Slider _volume;

	private Slider _speed;

	private Slider _memory;

	private TMP_Text _volumeLabel;

	private TMP_Text _speedLabel;

	private TMP_Text _memoryLabel;

	private TMP_Text _keyLabel;

	private bool _listeningKey;

	private AiButton _listen;

	private AiDropdown _personality;

	private AiDropdown _voice;

	/// <summary>The dropdown whose list is open right now (only one at a time), or null.</summary>
	private AiDropdown _openList;

	private readonly List<AiToggle> _toggles = new List<AiToggle>();

	private string[] _personalityIds;

	private string[] _personalityLabels;

	private int[] _voiceIds;

	private float _nextStatus;

	public static CompanionAiPanel Instance { get; private set; }

	public bool IsVisible => _visible;

	/// <summary>Creates the (hidden) panel once. Called from the plugin's Awake.</summary>
	public static void Create()
	{
		if (Instance != null)
		{
			return;
		}
		GameObject holder = new GameObject("HC_AiPanel");
		UnityEngine.Object.DontDestroyOnLoad(holder);
		Instance = holder.AddComponent<CompanionAiPanel>();
		string key = ModConfig.AiPanelKey != null ? ModConfig.AiPanelKey.Value.ToString() : "?";
		CompanionsPlugin.Log?.LogInfo($"[IA] Painel da IA pronto: abre/fecha com {key} ([IA] PainelTecla) ou Esc. Personalidade, voz (com Ouvir), "
			+ "volume, velocidade, memoria, conversa e o push-to-talk ficam todos ali, e cada mudanca e salva na hora no .cfg.");
	}

	private void Awake()
	{
		if (Instance != null && Instance != this)
		{
			UnityEngine.Object.Destroy(base.gameObject);
			return;
		}
		Instance = this;
	}

	private void OnDestroy()
	{
		if (Instance == this)
		{
			Instance = null;
		}
		// The texture comes from TextureLoader's cache and is shared with the other panels: only the Sprite is ours.
		if (_panelSprite != null)
		{
			UnityEngine.Object.Destroy(_panelSprite);
			_panelSprite = null;
		}
	}

	// ------------------------------------------------------------------ open / close

	public void Show()
	{
		if (_root == null)
		{
			Build();
		}
		if (IsBrokenTmpFont(_font))
		{
			TMP_FontAsset replacement = ResolveFont();
			if (replacement != null && !IsBrokenTmpFont(replacement))
			{
				_font = replacement;
				TMP_Text[] texts = _root.GetComponentsInChildren<TMP_Text>(includeInactive: true);
				for (int i = 0; i < texts.Length; i++)
				{
					if (texts[i] != null)
					{
						texts[i].font = _font;
					}
				}
			}
		}
		RefreshFromConfig();
		// defensive: no list (and no click catcher) may survive a hide, and Show() is the one place that turns the
		// panel back on - Hide() already closes the lists, so this only matters if something ever changes that
		CloseLists();
		_backdrop.SetActive(true);
		_root.SetActive(true);
		_visible = true;
		_nextStatus = 0f;
		RefreshStatus();
		Cursor.lockState = CursorLockMode.None;
		Cursor.visible = true;
		EnsureEventSystem();
	}

	public void Hide()
	{
		_visible = false;
		CloseLists();
		_listeningKey = false;
		if (_freeText != null && _freeText.isFocused)
		{
			_freeText.DeactivateInputField();
		}
		if (_root != null)
		{
			_root.SetActive(false);
		}
		if (_backdrop != null)
		{
			_backdrop.SetActive(false);
		}
	}

	private void Update()
	{
		if (!_visible)
		{
			if (PanelKeyPressed())
			{
				Show();
			}
			return;
		}
		if (Input.GetKeyDown(KeyCode.Escape) || ZInput.GetButtonDown("JoyBack") || ZInput.GetButtonDown("JoyButtonB"))
		{
			// Esc first leaves the text box, then closes the open list, then the panel - never all at once
			if (_freeText != null && _freeText.isFocused)
			{
				_freeText.DeactivateInputField();
				return;
			}
			if (_openList != null)
			{
				CloseLists();
				return;
			}
			Hide();
			return;
		}
		if (!_listeningKey && PanelKeyPressed())
		{
			Hide();
			return;
		}
		Cursor.lockState = CursorLockMode.None;
		Cursor.visible = true;
		if (Time.unscaledTime >= _nextStatus)
		{
			_nextStatus = Time.unscaledTime + StatusInterval;
			RefreshStatus();
		}
	}

	/// <summary>
	/// Is the panel key pressed right now? The key is a letter by default (I), so it is never stolen from a text box:
	/// the guards run only on the frame the key goes down, and the player has to exist (no panel in the main menu).
	/// </summary>
	private bool PanelKeyPressed()
	{
		ConfigEntry<KeyCode> entry = ModConfig.AiPanelKey;
		if (entry == null || entry.Value == KeyCode.None || !Input.GetKeyDown(entry.Value))
		{
			return false;
		}
		if (Player.m_localPlayer == null)
		{
			return false;
		}
		// the same windows VoiceInput.BlockedByUi refuses to talk over: an open inventory, the map or the game menu
		// mean the player is busy with the mouse and the keyboard
		if (InventoryGui.IsVisible() || Menu.IsVisible() || Minimap.IsOpen())
		{
			return false;
		}
		if (TextInput.IsVisible() || Console.IsVisible() || StoreGui.IsVisible())
		{
			return false;
		}
		// never on top of the mod's own F8 panel (ConfigPanel refuses to open while this one is visible)
		if (ConfigPanel.Instance != null && ConfigPanel.Instance.IsVisible)
		{
			return false;
		}
		if (Chat.instance != null && Chat.instance.HasFocus())
		{
			return false;
		}
		if (_freeText != null && _freeText.isFocused)
		{
			return false;
		}
		return true;
	}

	private static void EnsureEventSystem()
	{
		if (EventSystem.current != null)
		{
			return;
		}
		GameObject holder = new GameObject("HC_AiPanel_EventSystem");
		holder.AddComponent<EventSystem>();
		holder.AddComponent<StandaloneInputModule>();
		UnityEngine.Object.DontDestroyOnLoad(holder);
	}

	/// <summary>The twice-a-second refresh: the voice preview availability, the status line and the automatic close
	/// when the player is gone or dead.</summary>
	private void RefreshStatus()
	{
		Player player = Player.m_localPlayer;
		if (player == null || player.IsDead())
		{
			Hide();
			return;
		}
		bool ttsReady = CompanionTts.Enabled && CompanionTts.Ready;
		if (_listen != null)
		{
			_listen.Button.interactable = ttsReady;
			if (_listen.Label != null)
			{
				_listen.Label.color = ttsReady ? Color.white : DimColor;
			}
		}
		// The title names the companion that would answer right now (the same one the "Ouvir" preview uses as the
		// source of the voice), so it is clear who is talking before the sample plays.
		if (_title != null)
		{
			string name = CompanionName(CompanionChatAI.PickSpeaker());
			_title.text = string.IsNullOrEmpty(name)
				? ModLocalization.Loc("hc_ai_panel_title")
				: ModLocalization.LocFmt("hc_ai_panel_title_named", name);
		}
		if (_status != null)
		{
			string key = ModConfig.VozTecla != null ? ModConfig.VozTecla.Value.ToString() : "?";
			// two localized lines instead of one long line: the personality on the first, the voice and the
			// push-to-talk key on the second (the footer block is two lines tall)
			_status.text = ModLocalization.LocFmt("hc_ai_panel_status_personality", EffectivePersonalityLabel())
				+ "\n" + ModLocalization.LocFmt("hc_ai_panel_status_voice", CurrentVoiceLabel(), key);
		}
	}

	// ------------------------------------------------------------------ build

	/// <summary>The companion's own name (the one shown on its hover), or "" when there is none nearby.</summary>
	private static string CompanionName(CompanionSetup companion)
	{
		ZDO zdo = companion != null ? companion.GetComponent<ZNetView>()?.GetZDO() : null;
		return zdo != null ? zdo.GetString(CompanionSetup.NameHash, "") : "";
	}

	private void Build()
	{
		Canvas canvas = base.gameObject.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		canvas.sortingOrder = PanelSortingOrder;
		CanvasScaler scaler = base.gameObject.AddComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(1920f, 1080f);
		base.gameObject.AddComponent<GraphicRaycaster>();
		_font = ResolveFont();

		_backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
		_backdrop.transform.SetParent(base.transform, worldPositionStays: false);
		RectTransform backdropRT = _backdrop.GetComponent<RectTransform>();
		backdropRT.anchorMin = Vector2.zero;
		backdropRT.anchorMax = Vector2.one;
		backdropRT.offsetMin = Vector2.zero;
		backdropRT.offsetMax = Vector2.zero;
		_backdrop.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f);

		_root = new GameObject("AiRoot", typeof(RectTransform), typeof(Image));
		_root.transform.SetParent(base.transform, worldPositionStays: false);
		RectTransform rootRT = _root.GetComponent<RectTransform>();
		rootRT.anchorMin = new Vector2(0.5f, 0.5f);
		rootRT.anchorMax = new Vector2(0.5f, 0.5f);
		rootRT.pivot = new Vector2(0.5f, 0.5f);
		rootRT.sizeDelta = new Vector2(PanelW, PanelH);
		Image rootImage = _root.GetComponent<Image>();
		_panelSprite = LoadPanelSprite();
		if (_panelSprite != null)
		{
			rootImage.sprite = _panelSprite;
			rootImage.type = Image.Type.Simple;
			rootImage.color = Color.white;
		}
		else
		{
			rootImage.color = new Color(0.06f, 0.05f, 0.04f, 0.96f);
		}
		rootImage.raycastTarget = true;

		BuildHeader();
		BuildRowBackgrounds();
		BuildPersonality();
		BuildVoice();
		BuildPushToTalk();
		BuildToggles();
		BuildNumbers();
		BuildFooter();

		// The dropdown lists live on an overlay that is the last child of the panel, so an open list is drawn above
		// every row instead of being clipped by the row it belongs to. The overlay itself has no Image (it never eats
		// a click) and its own Canvas with overrideSorting, so the list draws - and gets the click - above the whole
		// panel no matter where the overlay sits in the hierarchy: that is exactly what Unity's own dropdown template
		// does, and it is why the list can never end up behind a control again.
		GameObject overlay = new GameObject("Overlay", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
		overlay.transform.SetParent(_root.transform, worldPositionStays: false);
		RectTransform overlayRT = overlay.GetComponent<RectTransform>();
		overlayRT.anchorMin = Vector2.zero;
		overlayRT.anchorMax = Vector2.one;
		overlayRT.offsetMin = Vector2.zero;
		overlayRT.offsetMax = Vector2.zero;
		Canvas overlayCanvas = overlay.GetComponent<Canvas>();
		overlayCanvas.overrideSorting = true;
		overlayCanvas.sortingOrder = OverlaySortingOrder;
		_overlay = overlay.transform;
		// the catcher is the FIRST child of the overlay: the lists created after it draw above it, so an item click
		// reaches the item and only a click outside the list reaches the catcher
		BuildListCatcher(_overlay);
		BuildDropdowns();
		// The fields of the two dropdowns are created by BuildDropdowns with the panel itself as the parent, i.e.
		// AFTER the overlay: in uGUI the render order is the order of the siblings (the last child draws on top), so
		// without this line the field of "Voz", the free-text box and "Aplicar" would be drawn OVER the open list -
		// which is exactly the bug of the screenshot (the list of "Personalidade" appearing behind the rows below it).
		// The overlay canvas above already wins over the panel canvas, but the sibling order is kept correct too, so
		// nothing depends on a single mechanism.
		_overlay.SetAsLastSibling();

		_backdrop.SetActive(false);
		_root.SetActive(false);
	}

	/// <summary>
	/// The invisible catcher that closes an open list when the player clicks anywhere else on the panel. It is the
	/// first child of the overlay, so the lists are drawn (and clicked) above it, and it is only active while a list
	/// is open - otherwise it would eat every click on the panel. No visual state: an alpha-0 Image with
	/// <c>raycastTarget</c> still receives the click, and the Button transition is off because there is nothing to
	/// tint. It is the same primitive Unity's own Dropdown uses for its "blocker" (a clear Image plus a Button whose
	/// onClick hides the list) - here it is a sibling that sits under the lists instead of a canvas of its own one
	/// sorting order below, which is the same thing one canvas lower.
	/// </summary>
	private void BuildListCatcher(Transform overlay)
	{
		GameObject catcher = new GameObject("ListCatcher", typeof(RectTransform), typeof(Image), typeof(Button));
		catcher.transform.SetParent(overlay, worldPositionStays: false);
		RectTransform rt = catcher.GetComponent<RectTransform>();
		rt.anchorMin = Vector2.zero;
		rt.anchorMax = Vector2.one;
		rt.offsetMin = Vector2.zero;
		rt.offsetMax = Vector2.zero;
		Image image = catcher.GetComponent<Image>();
		image.color = Color.clear;
		image.raycastTarget = true;
		Button button = catcher.GetComponent<Button>();
		button.transition = Selectable.Transition.None;
		button.targetGraphic = image;
		button.onClick.AddListener(CloseLists);
		catcher.SetActive(false);
		_listCatcher = catcher;
	}

	private void BuildHeader()
	{
		_title = CreateText(_root.transform, ModLocalization.Loc("hc_ai_panel_title"), 8f, 4f, PanelW - 16f, 30f, 18f, LabelColor, TextAlignmentOptions.Center);
		_title.fontStyle = FontStyles.Bold;

		AiButton close = CreateButton(_root.transform, "Close", PanelW - 40f, 2f, 36f, 34f, "X", Hide);
		close.Image.color = new Color(0.45f, 0.16f, 0.12f, 0.95f);
	}

	private void BuildRowBackgrounds()
	{
		float[] rows = new float[9]
		{
			RowPersonality, RowFreeText, RowVoice, RowVolume, RowKey, RowToggleA, RowToggleB, RowMemory, RowSpeed
		};
		for (int i = 0; i < rows.Length; i++)
		{
			GameObject row = new GameObject("Row" + i, typeof(RectTransform), typeof(Image));
			row.transform.SetParent(_root.transform, worldPositionStays: false);
			Place(row.GetComponent<RectTransform>(), 4f, rows[i], RightEdge - 4f, RowH);
			Image image = row.GetComponent<Image>();
			image.color = RowColor;
			image.raycastTarget = false;
		}
	}

	private void BuildPersonality()
	{
		CreateLabel(RowPersonality, ModLocalization.Loc("hc_ai_panel_personality"));
		CreateLabel(RowFreeText, ModLocalization.Loc("hc_ai_panel_free_text"));

		float inputW = 380f;
		_freeText = CreateInputField("FreeText", FieldX, RowFreeText, inputW, RowH,
			ModLocalization.Loc("hc_ai_panel_free_placeholder"), CompanionPersona.MaxFreeTextLength);
		float applyX = FieldX + inputW + 8f;
		CreateButton(_root.transform, "Apply", applyX, RowFreeText, RightEdge - applyX, RowH, ModLocalization.Loc("hc_ai_panel_apply"), ApplyPersonality);
	}

	private void BuildVoice()
	{
		CreateLabel(RowVoice, ModLocalization.Loc("hc_ai_panel_voice"));
		float listenX = FieldX + 298f;
		_listen = CreateButton(_root.transform, "Listen", listenX, RowVoice, RightEdge - listenX, RowH, ModLocalization.Loc("hc_ai_panel_listen"), PreviewVoice);

		CreateLabel(RowVolume, ModLocalization.Loc("hc_ai_panel_volume"));
		_volume = CreateSlider("Volume", FieldX, RowVolume, 440f, RowH, 0f, 1f, 1f, false, OnVolumeChanged);
		_volumeLabel = CreateText(_root.transform, "", RightEdge - 60f, RowVolume + 6f, 60f, 22f, 12f, ValueColor, TextAlignmentOptions.MidlineRight);
	}

	private void BuildPushToTalk()
	{
		CreateLabel(RowKey, ModLocalization.Loc("hc_ai_panel_push_to_talk"));

		GameObject keyBox = new GameObject("KeyBox", typeof(RectTransform), typeof(Image));
		keyBox.transform.SetParent(_root.transform, worldPositionStays: false);
		Place(keyBox.GetComponent<RectTransform>(), FieldX, RowKey, 170f, RowH);
		keyBox.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.15f, 0.9f);
		_keyLabel = CreateText(keyBox.transform, "?", 0f, 0f, 170f, RowH, 13f, ValueColor, TextAlignmentOptions.Center);

		// The same KeyBindListener the F8 panel uses: click "Alterar", press a key, and it lands in [IA] VozTecla.
		float changeX = FieldX + 178f;
		AiButton change = CreateButton(_root.transform, "ChangeKey", changeX, RowKey, 110f, RowH, ModLocalization.Loc("hc_ai_panel_change"), null);
		change.Button.onClick.AddListener(delegate
		{
			_listeningKey = true;
			_keyLabel.text = ModLocalization.Loc("hc_ui_press_key");
			_keyLabel.color = LabelColor;
		});
		// The listening flag is a field, not a local: KeyBindListener's Update only runs while the panel is shown, and
		// Hide() has to be able to stop a capture that was left half-done (otherwise the next key press would be
		// captured as soon as the panel was opened again).
		KeyBindListener listener = change.Button.gameObject.AddComponent<KeyBindListener>();
		listener.Init(ModConfig.VozTecla, _keyLabel, () => _listeningKey, delegate(bool value)
		{
			_listeningKey = value;
			_keyLabel.color = ValueColor;
			if (!value)
			{
				// the push-to-talk key changed: the auto-pickup conflict check has to be redone (Fase 3.3b)
				AutoPickupKeyConflict.Reset();
				CompanionsPlugin.Log?.LogInfo($"[IA] Painel: push-to-talk = {ModConfig.VozTecla.Value} (a tecla antiga volta a fazer o que o jogo fazia com ela).");
				RefreshStatus();
			}
		});

		float voiceToggleX = changeX + 118f;
		CreateToggle(voiceToggleX, 126f, RowKey, voiceToggleX + 130f, 72f, ModLocalization.Loc("hc_ai_panel_voice_enabled"), ModConfig.VozEnabled);
	}

	private void BuildToggles()
	{
		float half = (RightEdge - LeftX) / 2f;
		float rightX = LeftX + half + 4f;
		CreateToggle(LeftX + 8f, 180f, RowToggleA, LeftX + 198f, 122f, ModLocalization.Loc("hc_ai_panel_tts_on"), ModConfig.TtsEnabled);
		CreateToggle(rightX + 8f, 180f, RowToggleA, rightX + 198f, 122f, ModLocalization.Loc("hc_ai_panel_tts_replies"), ModConfig.TtsSpeakReplies);
		CreateToggle(LeftX + 8f, 180f, RowToggleB, LeftX + 198f, 122f, ModLocalization.Loc("hc_ai_panel_tts_modlines"), ModConfig.TtsSpeakModLines);
		CreateToggle(rightX + 8f, 180f, RowToggleB, rightX + 198f, 122f, ModLocalization.Loc("hc_ai_panel_conversation"), ModConfig.AiConversation);
	}

	private void BuildNumbers()
	{
		CreateLabel(RowMemory, ModLocalization.Loc("hc_ai_panel_memory"));
		_memory = CreateSlider("Memory", FieldX, RowMemory, 440f, RowH, 0f, 12f, 4f, true, OnMemoryChanged);
		_memoryLabel = CreateText(_root.transform, "", RightEdge - 60f, RowMemory + 6f, 60f, 22f, 12f, ValueColor, TextAlignmentOptions.MidlineRight);

		CreateLabel(RowSpeed, ModLocalization.Loc("hc_ai_panel_speed"));
		// the bounds of [IA] TtsVelocidade (0,5–2,0) come from CompanionTts, the same constants its clamp uses: the
		// slider used to stop at 1,5, so a .cfg with 2,0 was shown as "2.00" with the handle at the end and the
		// first drag silently lowered the setting to 1,5
		_speed = CreateSlider("Speed", FieldX, RowSpeed, 440f, RowH, CompanionTts.MinSpeed, CompanionTts.MaxSpeed, 1f, false, OnSpeedChanged);
		_speedLabel = CreateText(_root.transform, "", RightEdge - 60f, RowSpeed + 6f, 60f, 22f, 12f, ValueColor, TextAlignmentOptions.MidlineRight);
	}

	/// <summary>
	/// The footer: the summary of what is in use right now plus the hint line, both on their own dark strip.
	///
	/// <para>
	/// What changed (the report was "the text at the bottom where it shows the current configs is very hard to see"):
	/// the two blocks used to be 11px <see cref="DimColor"/> lines drawn straight on the wooden panel - dark grey on
	/// dark wood - and the hint line ended up clipped by the bottom edge of the panel. Now they sit on a dark
	/// translucent strip (<see cref="FooterBackColor"/>, the same trick the setting rows use), the text is a warm
	/// near-white at 12.5/12px, the summary is <b>two</b> lines instead of one long line, and the panel grew
	/// (470 -> 516) so the strip and both blocks fit with room to spare above the bottom edge.
	/// </para>
	///
	/// The strip is one extra Image created <b>once</b>, at build time: the twice-a-second refresh only rewrites the
	/// text of the two blocks, exactly as before.
	/// </summary>
	private void BuildFooter()
	{
		// the dark strip behind both blocks (the rows above use the same primitive for the same reason)
		GameObject strip = new GameObject("Footer", typeof(RectTransform), typeof(Image));
		strip.transform.SetParent(_root.transform, worldPositionStays: false);
		Place(strip.GetComponent<RectTransform>(), 4f, RowStatus - 6f, RightEdge - 8f, RowHint - RowStatus + 2f * FooterLineH + 12f);
		Image stripImage = strip.GetComponent<Image>();
		stripImage.color = FooterBackColor;
		stripImage.raycastTarget = false;

		_status = CreateText(_root.transform, "", LeftX + 4f, RowStatus, RightEdge - LeftX - 8f, 2f * FooterLineH, 12.5f,
			FooterColor, TextAlignmentOptions.TopLeft);
		// the summary is built from two localized lines (see RefreshStatus): never truncated, never one long line
		_status.overflowMode = TextOverflowModes.Overflow;

		string key = ModConfig.AiPanelKey != null ? ModConfig.AiPanelKey.Value.ToString() : "?";
		TMP_Text hint = CreateText(_root.transform, ModLocalization.LocFmt("hc_ai_panel_hint_fmt", key), LeftX + 4f, RowHint,
			RightEdge - LeftX - 8f, 2f * FooterLineH, 12f, FooterHintColor, TextAlignmentOptions.TopLeft);
		hint.overflowMode = TextOverflowModes.Overflow;
	}

	/// <summary>
	/// The two dropdowns: the field button goes into the panel (where the row is), the list of options goes into the
	/// overlay. Both are built after the overlay exists, and <c>Build</c> puts the overlay back as the last child of
	/// the panel right after this - the fields are the reason that line has to exist.
	/// </summary>
	private void BuildDropdowns()
	{
		_personalityIds = PersonalityIds();
		_personalityLabels = PersonalityLabels();
		int personalityIndex = IndexOf(_personalityIds, Trimmed(ModConfig.AiPersonalityPreset));
		if (personalityIndex < 0)
		{
			// an unknown preset is treated as the default by CompanionPersona.Resolve - show it that way here too
			personalityIndex = 0;
		}
		_personality = CreateDropdown("Personality", FieldX, RowPersonality, 380f, RowH, _personalityLabels, personalityIndex, OnPersonalityPicked);

		_voiceIds = VoiceIds();
		string[] voiceLabels = new string[_voiceIds.Length];
		for (int i = 0; i < _voiceIds.Length; i++)
		{
			voiceLabels[i] = ModLocalization.Loc("hc_ai_voice_" + _voiceIds[i]);
		}
		_voice = CreateDropdown("Voice", FieldX, RowVoice, 290f, RowH, voiceLabels, 0, OnVoicePicked);
	}

	// ------------------------------------------------------------------ config <-> controls

	private void RefreshFromConfig()
	{
		SetDropdown(_personality, Mathf.Max(0, IndexOf(_personalityIds, Trimmed(ModConfig.AiPersonalityPreset))));

		int voice = ModConfig.TtsVoice != null ? ModConfig.TtsVoice.Value : CompanionTts.DefaultVoice;
		int voiceIndex = IndexOf(_voiceIds, voice);
		if (voiceIndex < 0)
		{
			if (_voice != null)
			{
				_voice.Index = -1;
			}
			SetCaption(_voice, ModLocalization.LocFmt("hc_ai_voice_other", voice), DimColor);
			HighlightItems(_voice, -1);
		}
		else
		{
			SetDropdown(_voice, voiceIndex);
		}

		if (_freeText != null)
		{
			_freeText.SetTextWithoutNotify(ModConfig.AiPersonality != null ? ModConfig.AiPersonality.Value : "");
		}
		SetSlider(_volume, ModConfig.TtsVolume != null ? ModConfig.TtsVolume.Value : 1f);
		SetSlider(_speed, ModConfig.TtsSpeed != null ? ModConfig.TtsSpeed.Value : 1f);
		SetSlider(_memory, ModConfig.AiMemory != null ? ModConfig.AiMemory.Value : 4);
		for (int i = 0; i < _toggles.Count; i++)
		{
			ApplyToggleVisual(_toggles[i]);
		}
		RefreshValueLabels();
		if (_keyLabel != null && !_listeningKey)
		{
			_keyLabel.text = ModConfig.VozTecla != null ? ModConfig.VozTecla.Value.ToString() : "?";
			_keyLabel.color = ValueColor;
		}
	}

	private void RefreshValueLabels()
	{
		if (_volumeLabel != null)
		{
			_volumeLabel.text = Mathf.RoundToInt(Mathf.Clamp01(ModConfig.TtsVolume != null ? ModConfig.TtsVolume.Value : 1f) * 100f) + "%";
		}
		if (_speedLabel != null)
		{
			_speedLabel.text = (ModConfig.TtsSpeed != null ? ModConfig.TtsSpeed.Value : 1f).ToString("0.00");
		}
		if (_memoryLabel != null)
		{
			_memoryLabel.text = ModLocalization.LocFmt("hc_ai_panel_memory_fmt", ModConfig.AiMemory != null ? ModConfig.AiMemory.Value : 4);
		}
	}

	private void OnPersonalityPicked(int index)
	{
		if (ModConfig.AiPersonalityPreset == null)
		{
			return;
		}
		string id = IdAt(_personalityIds, index);
		ModConfig.AiPersonalityPreset.Value = id;
		if (Trimmed(ModConfig.AiPersonality).Length > 0)
		{
			// the free text wins (CompanionPersona.Resolve): say it, so the choice is not silently ignored
			CompanionChatAI.Announce(ModLocalization.LocFmt("hc_ai_panel_preset_shadowed", CompanionPersona.Describe()));
		}
		CompanionsPlugin.Log?.LogInfo($"[IA] Painel: preset de personalidade = \"{id}\" - em uso agora: {CompanionPersona.Describe()}");
		RefreshStatus();
	}

	private void OnVoicePicked(int index)
	{
		if (ModConfig.TtsVoice == null || index < 0 || index >= _voiceIds.Length)
		{
			return;
		}
		ModConfig.TtsVoice.Value = _voiceIds[index];
		CompanionsPlugin.Log?.LogInfo($"[IA] Painel: voz do companheiro = {_voiceIds[index]} ({_voice.LabelAt(index)}) - vale na proxima fala.");
		RefreshStatus();
	}

	private void ApplyPersonality()
	{
		if (_freeText == null)
		{
			return;
		}
		string text = _freeText.text != null ? _freeText.text.Trim() : "";
		if (text.Length > CompanionPersona.MaxFreeTextLength)
		{
			text = text.Substring(0, CompanionPersona.MaxFreeTextLength);
			_freeText.SetTextWithoutNotify(text);
		}
		if (ModConfig.AiPersonality != null)
		{
			ModConfig.AiPersonality.Value = text;
		}
		if (ModConfig.AiPersonalityPreset != null)
		{
			ModConfig.AiPersonalityPreset.Value = IdAt(_personalityIds, _personality != null ? _personality.Index : 0);
		}
		string effective = CompanionPersona.Describe();
		CompanionsPlugin.Log?.LogInfo($"[IA] Painel: personalidade aplicada ({(text.Length > 0 ? text.Length + " caracteres de texto livre" : "sem texto livre")}) "
			+ $"- em uso agora: {effective}");
		CompanionChatAI.Announce(ModLocalization.LocFmt("hc_ai_panel_apply_ok", effective));
		RefreshStatus();
	}

	private void OnVolumeChanged(float value)
	{
		float volume = Mathf.Clamp01(value);
		if (ModConfig.TtsVolume != null)
		{
			ModConfig.TtsVolume.Value = volume;
		}
		if (_volumeLabel != null)
		{
			_volumeLabel.text = Mathf.RoundToInt(volume * 100f) + "%";
		}
	}

	private void OnSpeedChanged(float value)
	{
		float speed = Mathf.Clamp(value, CompanionTts.MinSpeed, CompanionTts.MaxSpeed);
		if (ModConfig.TtsSpeed != null)
		{
			ModConfig.TtsSpeed.Value = speed;
		}
		if (_speedLabel != null)
		{
			_speedLabel.text = speed.ToString("0.00");
		}
	}

	private void OnMemoryChanged(float value)
	{
		int exchanges = Mathf.Clamp(Mathf.RoundToInt(value), 0, 12);
		if (ModConfig.AiMemory != null)
		{
			ModConfig.AiMemory.Value = exchanges;
		}
		if (_memoryLabel != null)
		{
			_memoryLabel.text = ModLocalization.LocFmt("hc_ai_panel_memory_fmt", exchanges);
		}
	}

	/// <summary>
	/// The "Ouvir" button: speaks a short sample line through the real pipeline (the same synthesis worker and the same
	/// 3D playback the companion uses), so the player hears exactly what the chosen voice/volume/speed will sound like.
	/// The sample comes from the companion that would answer when it is close enough for its voice to be heard (the
	/// same cull distance the speech bubbles use); otherwise it plays at the player, so the preview is never silent
	/// just because the companion is far away.
	/// </summary>
	private void PreviewVoice()
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
			CompanionChatAI.Announce(ModLocalization.Loc("hc_ai_panel_no_companion"));
			return;
		}
		float cull = ModConfig.SpeechCullDistance != null ? ModConfig.SpeechCullDistance.Value : 20f;
		CompanionSetup speaker = CompanionChatAI.PickSpeaker();
		GameObject talker = null;
		string where = "no companheiro mais proximo";
		if (speaker != null && Vector3.Distance(speaker.transform.position, player.transform.position) <= cull)
		{
			talker = speaker.gameObject;
		}
		if (talker == null)
		{
			talker = player.gameObject;
			where = "em voce (nenhum companheiro dentro do alcance da voz)";
		}
		string text = ModLocalization.Loc("hc_ai_panel_tts_sample");
		CompanionsPlugin.Log?.LogInfo($"[IA] Painel: Ouvir -> voz {CompanionTts.Voice}, volume {CompanionTts.Volume:0.##}, velocidade {CompanionTts.Speed:0.##}, "
			+ $"falando {where} | \"{text}\"");
		CompanionTts.Speak(talker, text);
	}

	// ------------------------------------------------------------------ dropdown plumbing

	private void ToggleList(AiDropdown dropdown)
	{
		if (dropdown == null)
		{
			return;
		}
		if (_openList == dropdown)
		{
			CloseLists();
			return;
		}
		CloseLists();
		dropdown.ListRoot.SetActive(true);
		// the catcher only exists while a list is open: outside that it would swallow the clicks of the panel
		if (_listCatcher != null)
		{
			_listCatcher.SetActive(true);
		}
		_openList = dropdown;
	}

	private void CloseLists()
	{
		if (_openList != null && _openList.ListRoot != null)
		{
			_openList.ListRoot.SetActive(false);
		}
		_openList = null;
		if (_listCatcher != null)
		{
			_listCatcher.SetActive(false);
		}
	}

	private void Pick(AiDropdown dropdown, int index)
	{
		if (dropdown == null)
		{
			return;
		}
		SetDropdown(dropdown, index);
		CloseLists();
		dropdown.OnPick?.Invoke(index);
	}

	private void SetDropdown(AiDropdown dropdown, int index)
	{
		if (dropdown == null)
		{
			return;
		}
		dropdown.Index = index;
		if (index >= 0 && index < dropdown.Labels.Length)
		{
			SetCaption(dropdown, dropdown.Labels[index], ValueColor);
		}
		else
		{
			SetCaption(dropdown, "?", DimColor);
		}
		HighlightItems(dropdown, index);
	}

	private static void HighlightItems(AiDropdown dropdown, int index)
	{
		if (dropdown == null || dropdown.Items == null)
		{
			return;
		}
		for (int i = 0; i < dropdown.Items.Count; i++)
		{
			dropdown.Items[i].Image.color = i == index ? GoldColor : ItemColor;
		}
	}

	private static void SetCaption(AiDropdown dropdown, string text, Color color)
	{
		if (dropdown == null || dropdown.Caption == null)
		{
			return;
		}
		dropdown.Caption.text = text;
		dropdown.Caption.color = color;
	}

	/// <summary>The preset ids in the order the dropdown shows them: default, the presets, then "custom" (free text).</summary>
	private static string[] PersonalityIds()
	{
		CompanionPersona.Preset[] presets = CompanionPersona.Presets;
		string[] ids = new string[presets.Length + 2];
		ids[0] = "";
		for (int i = 0; i < presets.Length; i++)
		{
			ids[i + 1] = presets[i].Id;
		}
		ids[ids.Length - 1] = CompanionPersona.CustomId;
		return ids;
	}

	private static string[] PersonalityLabels()
	{
		CompanionPersona.Preset[] presets = CompanionPersona.Presets;
		string[] labels = new string[presets.Length + 2];
		labels[0] = ModLocalization.Loc("hc_ai_preset_default");
		for (int i = 0; i < presets.Length; i++)
		{
			labels[i + 1] = ModLocalization.Loc(presets[i].NameKey);
		}
		labels[labels.Length - 1] = ModLocalization.Loc("hc_ai_preset_custom");
		return labels;
	}

	private static string IdAt(string[] ids, int index)
	{
		if (ids == null || index < 0 || index >= ids.Length)
		{
			return "";
		}
		return ids[index];
	}

	private static int IndexOf(string[] values, string wanted)
	{
		if (values == null || string.IsNullOrEmpty(wanted))
		{
			return 0;
		}
		for (int i = 0; i < values.Length; i++)
		{
			if (string.Equals(values[i], wanted, StringComparison.OrdinalIgnoreCase))
			{
				return i;
			}
		}
		return -1;
	}

	private static int IndexOf(int[] values, int wanted)
	{
		if (values == null)
		{
			return -1;
		}
		for (int i = 0; i < values.Length; i++)
		{
			if (values[i] == wanted)
			{
				return i;
			}
		}
		return -1;
	}

	private static string Trimmed(ConfigEntry<string> entry)
	{
		string value = entry != null ? entry.Value : null;
		return value == null ? "" : value.Trim();
	}

	/// <summary>What the player would hear: the free text wins over the preset (see CompanionPersona.Resolve).</summary>
	private string EffectivePersonalityLabel()
	{
		if (Trimmed(ModConfig.AiPersonality).Length > 0)
		{
			return ModLocalization.Loc("hc_ai_preset_custom");
		}
		int index = IndexOf(_personalityIds, Trimmed(ModConfig.AiPersonalityPreset));
		return index >= 0 && index < _personalityLabels.Length ? _personalityLabels[index] : _personalityLabels[0];
	}

	/// <summary>The voice in use, by name. A voice that is not one of the pt-BR ones is named as "other language",
	/// because the panel only offers the three pt-BR voices.</summary>
	private string CurrentVoiceLabel()
	{
		int voice = ModConfig.TtsVoice != null ? ModConfig.TtsVoice.Value : CompanionTts.DefaultVoice;
		if (IndexOf(_voiceIds, voice) < 0)
		{
			return ModLocalization.LocFmt("hc_ai_voice_other", voice);
		}
		return ModLocalization.Loc("hc_ai_voice_" + voice);
	}

	private static int[] VoiceIds()
	{
		int count = CompanionTts.PortugueseVoiceCount;
		int[] ids = new int[count];
		for (int i = 0; i < count; i++)
		{
			ids[i] = CompanionTts.PortugueseVoiceAt(i);
		}
		return ids;
	}

	private static void SetSlider(Slider slider, float value)
	{
		if (slider == null)
		{
			return;
		}
		slider.SetValueWithoutNotify(Mathf.Clamp(value, slider.minValue, slider.maxValue));
	}

	private static void ApplyToggleVisual(AiToggle toggle)
	{
		if (toggle == null || toggle.Entry == null)
		{
			return;
		}
		bool on = toggle.Entry.Value;
		if (toggle.Image != null)
		{
			toggle.Image.color = on ? GoldColor : OffColor;
		}
		if (toggle.Label != null)
		{
			toggle.Label.text = ModLocalization.Loc(on ? "hc_ai_panel_on" : "hc_ai_panel_off");
		}
	}

	// ------------------------------------------------------------------ primitives

	/// <summary>A button with the label it shows (the toggles and "Ouvir" change it later).</summary>
	private sealed class AiButton
	{
		internal Button Button;

		internal TMP_Text Label;

		internal Image Image;
	}

	/// <summary>A hand-built dropdown: the field button, its caption and the (hidden) list of options.</summary>
	private sealed class AiDropdown
	{
		internal TMP_Text Caption;

		internal GameObject ListRoot;

		internal string[] Labels;

		internal readonly List<AiButton> Items = new List<AiButton>();

		internal int Index;

		internal Action<int> OnPick;

		internal string LabelAt(int index)
		{
			return Labels != null && index >= 0 && index < Labels.Length ? Labels[index] : "?";
		}
	}

	/// <summary>A boolean setting shown as a row: its name on the left, an On/Off button on the right.</summary>
	private sealed class AiToggle
	{
		internal ConfigEntry<bool> Entry;

		internal Image Image;

		internal TMP_Text Label;
	}

	private void CreateToggle(float labelX, float labelW, float y, float buttonX, float buttonW, string text, ConfigEntry<bool> entry)
	{
		CreateText(_root.transform, text, labelX, y + 6f, labelW, 22f, 12f, Color.white, TextAlignmentOptions.MidlineLeft);
		AiButton button = CreateButton(_root.transform, "Toggle_" + text, buttonX, y, buttonW, RowH, "", null);
		AiToggle toggle = new AiToggle
		{
			Entry = entry,
			Image = button.Image,
			Label = button.Label
		};
		button.Button.onClick.AddListener(delegate
		{
			if (toggle.Entry == null)
			{
				return;
			}
			toggle.Entry.Value = !toggle.Entry.Value;
			ApplyToggleVisual(toggle);
		});
		_toggles.Add(toggle);
	}

	private AiButton CreateButton(Transform parent, string name, float x, float y, float w, float h, string text, Action onClick)
	{
		GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
		go.transform.SetParent(parent, worldPositionStays: false);
		Place(go.GetComponent<RectTransform>(), x, y, w, h);
		Image image = go.GetComponent<Image>();
		image.color = ButtonColor;
		Button button = go.GetComponent<Button>();
		button.targetGraphic = image;
		if (onClick != null)
		{
			button.onClick.AddListener(delegate
			{
				onClick();
			});
		}
		TMP_Text label = CreateText(go.transform, text, 2f, 2f, w - 4f, h - 4f, 12f, Color.white, TextAlignmentOptions.Center);
		return new AiButton
		{
			Button = button,
			Label = label,
			Image = image
		};
	}

	private AiDropdown CreateDropdown(string name, float x, float y, float w, float h, string[] labels, int index, Action<int> onPick)
	{
		AiDropdown dropdown = new AiDropdown
		{
			Labels = labels,
			Index = index,
			OnPick = onPick
		};
		// the field belongs to the panel (its row), the list to the overlay: only the list has to float above the rows
		AiButton field = CreateButton(_root.transform, name + "Field", x, y, w, h, "", delegate
		{
			ToggleList(dropdown);
		});
		dropdown.Caption = field.Label;
		dropdown.Caption.alignment = TextAlignmentOptions.MidlineLeft;

		GameObject listRoot = new GameObject(name + "List", typeof(RectTransform), typeof(Image));
		listRoot.transform.SetParent(_overlay, worldPositionStays: false);
		Place(listRoot.GetComponent<RectTransform>(), x, y + h, w, labels.Length * ItemH);
		listRoot.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.06f, 0.99f);
		for (int i = 0; i < labels.Length; i++)
		{
			int captured = i;
			AiButton item = CreateButton(listRoot.transform, name + "Item" + i, 2f, i * ItemH + 1f, w - 4f, ItemH - 2f, labels[i], delegate
			{
				Pick(dropdown, captured);
			});
			item.Label.alignment = TextAlignmentOptions.MidlineLeft;
			item.Image.color = i == index ? GoldColor : ItemColor;
			dropdown.Items.Add(item);
		}
		dropdown.ListRoot = listRoot;
		listRoot.SetActive(false);
		return dropdown;
	}

	private Slider CreateSlider(string name, float x, float y, float w, float h, float min, float max, float value, bool whole, Action<float> onChanged)
	{
		GameObject go = new GameObject(name, typeof(RectTransform));
		go.transform.SetParent(_root.transform, worldPositionStays: false);
		Place(go.GetComponent<RectTransform>(), x, y, w, h);

		GameObject background = new GameObject("Background", typeof(RectTransform), typeof(Image));
		background.transform.SetParent(go.transform, worldPositionStays: false);
		background.GetComponent<Image>().color = new Color(0.14f, 0.14f, 0.16f, 0.9f);
		RectTransform backgroundRT = background.GetComponent<RectTransform>();
		backgroundRT.anchorMin = Vector2.zero;
		backgroundRT.anchorMax = Vector2.one;
		backgroundRT.offsetMin = new Vector2(0f, 9f);
		backgroundRT.offsetMax = new Vector2(0f, -9f);

		GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
		fillArea.transform.SetParent(go.transform, worldPositionStays: false);
		RectTransform fillAreaRT = fillArea.GetComponent<RectTransform>();
		fillAreaRT.anchorMin = Vector2.zero;
		fillAreaRT.anchorMax = Vector2.one;
		fillAreaRT.offsetMin = new Vector2(5f, 9f);
		fillAreaRT.offsetMax = new Vector2(-5f, -9f);
		GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
		fill.transform.SetParent(fillArea.transform, worldPositionStays: false);
		fill.GetComponent<Image>().color = GoldColor;
		RectTransform fillRT = fill.GetComponent<RectTransform>();
		fillRT.anchorMin = Vector2.zero;
		fillRT.anchorMax = new Vector2(0f, 1f);
		fillRT.offsetMin = Vector2.zero;
		fillRT.offsetMax = Vector2.zero;

		GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
		handleArea.transform.SetParent(go.transform, worldPositionStays: false);
		RectTransform handleAreaRT = handleArea.GetComponent<RectTransform>();
		handleAreaRT.anchorMin = Vector2.zero;
		handleAreaRT.anchorMax = Vector2.one;
		handleAreaRT.offsetMin = new Vector2(5f, 7f);
		handleAreaRT.offsetMax = new Vector2(-5f, -7f);
		GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
		handle.transform.SetParent(handleArea.transform, worldPositionStays: false);
		Image handleImage = handle.GetComponent<Image>();
		handleImage.color = HandleColor;
		RectTransform handleRT = handle.GetComponent<RectTransform>();
		handleRT.sizeDelta = new Vector2(12f, 0f);
		handleRT.anchorMin = new Vector2(0f, 0f);
		handleRT.anchorMax = new Vector2(0f, 1f);

		Slider slider = go.AddComponent<Slider>();
		slider.fillRect = fillRT;
		slider.handleRect = handleRT;
		slider.targetGraphic = handleImage;
		slider.direction = Slider.Direction.LeftToRight;
		slider.minValue = min;
		slider.maxValue = max;
		slider.wholeNumbers = whole;
		slider.value = Mathf.Clamp(value, min, max);
		slider.onValueChanged.AddListener(delegate(float v)
		{
			onChanged?.Invoke(v);
		});
		return slider;
	}

	/// <summary>
	/// The hand-built single-line text box (the free-text personality).
	///
	/// <para>
	/// The important part is the <b>order</b>: the GameObject is created <b>inactive</b> and is only activated after
	/// every property is assigned, because <c>TMP_InputField.OnEnable</c> is where TMP creates the "Caret" object it
	/// draws the caret and the selection with (<c>m_CachedInputRenderer</c> comes from that child's CanvasRenderer) -
	/// and it only does that when <c>textComponent</c> is already set. Adding the component with <c>AddComponent</c>
	/// on an active object ran <c>OnEnable</c> while the component was still empty, so the caret object was never
	/// created and the field had <b>no caret and no selection highlight at all</b> (the "buggy" box of the report:
	/// nothing showing where the text would go). Assigning everything before the first activation fixes both.
	/// </para>
	///
	/// <para>
	/// The rest is what a real TMP input prefab has and a hand-built one usually misses: the text <b>overflows</b> (it
	/// is the viewport mask that clips it, and TMP scrolls the text rect horizontally to follow the caret - with the
	/// ellipsis overflow mode of <see cref="CreateText"/> long text was truncated with "…" and appeared to jump), the
	/// caret is explicitly white and 2px wide (TMP would use the text colour), the selection colour is set for the
	/// dark box, rich text is off (the personality is plain text for the prompt), and <b>Enter applies</b> the text
	/// like the "Aplicar" button (<c>onSubmit</c> - Esc, which TMP treats as a cancel, does not apply anything).
	/// </para>
	///
	/// The field is also registered in <see cref="ModTextInputFocus"/>, which is what stops the game's own keys from
	/// firing while the player types (an "m" used to open the map - see <c>Patches/TextInputKeyGuard.cs</c>).
	/// </summary>
	private TMP_InputField CreateInputField(string name, float x, float y, float w, float h, string placeholder, int limit)
	{
		GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
		// inactive until the very end: the TMP_InputField added below has to see its text component already assigned
		// when its OnEnable runs, or the caret/selection object is never created (see the summary above)
		go.SetActive(false);
		go.transform.SetParent(_root.transform, worldPositionStays: false);
		Place(go.GetComponent<RectTransform>(), x, y, w, h);
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

		TMP_Text text = CreateText(area.transform, "", 0f, 0f, w - 16f, h - 6f, 13f, Color.white, TextAlignmentOptions.MidlineLeft);
		// the viewport mask is what clips the text; the text itself must overflow (no ellipsis, no wrapping), which is
		// what lets TMP scroll it horizontally to keep the caret in sight
		text.overflowMode = TextOverflowModes.Overflow;
		text.textWrappingMode = TextWrappingModes.PreserveWhitespaceNoWrap;

		TMP_Text placeholderLabel = CreateText(area.transform, placeholder, 0f, 0f, w - 16f, h - 6f, 13f,
			new Color(0.6f, 0.6f, 0.6f, 0.7f), TextAlignmentOptions.MidlineLeft);
		placeholderLabel.fontStyle = FontStyles.Italic;
		placeholderLabel.textWrappingMode = TextWrappingModes.PreserveWhitespaceNoWrap;

		TMP_InputField input = go.AddComponent<TMP_InputField>();
		input.targetGraphic = image;
		input.textComponent = text;
		input.textViewport = areaRT;
		input.placeholder = placeholderLabel;
		input.characterLimit = limit;
		input.contentType = TMP_InputField.ContentType.Standard;
		input.lineType = TMP_InputField.LineType.SingleLine;
		input.richText = false;
		input.onFocusSelectAll = false;   // clicking puts the caret where the player clicked, it does not select all
		input.customCaretColor = true;    // the caret is ours (white), not the text colour
		input.caretColor = new Color(1f, 0.95f, 0.85f, 1f);
		input.caretWidth = 2;
		input.selectionColor = new Color(0.55f, 0.72f, 1f, 0.55f);
		// Enter = the "Aplicar" button (TMP sends onSubmit on Enter and onEndEdit on Esc/focus loss, so Esc cancels)
		input.onSubmit.AddListener(delegate
		{
			ApplyPersonality();
		});

		go.SetActive(true);   // OnEnable runs here: TMP creates its caret object and caches its renderer

		// TMP creates that caret as the FIRST child of the text's parent, so the caret was drawn behind the text and
		// the placeholder; as the last child it is always on top of both
		Transform caret = area.transform.Find("Caret");
		if (caret != null)
		{
			caret.SetAsLastSibling();
		}

		ModTextInputFocus.Register(input);
		return input;
	}

	private static void Place(RectTransform rt, float x, float y, float w, float h)
	{
		rt.anchorMin = new Vector2(0f, 1f);
		rt.anchorMax = new Vector2(0f, 1f);
		rt.pivot = new Vector2(0f, 1f);
		rt.sizeDelta = new Vector2(w, h);
		rt.anchoredPosition = new Vector2(x, 0f - y);
	}

	private void CreateLabel(float rowY, string text)
	{
		CreateText(_root.transform, text, LeftX, rowY + 6f, LabelW, 22f, 13f, LabelColor, TextAlignmentOptions.MidlineLeft);
	}

	private TMP_Text CreateText(Transform parent, string text, float x, float y, float w, float h, float size, Color color, TextAlignmentOptions align)
	{
		GameObject go = new GameObject("Text", typeof(RectTransform));
		go.transform.SetParent(parent, worldPositionStays: false);
		Place(go.GetComponent<RectTransform>(), x, y, w, h);
		TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
		if (_font != null)
		{
			label.font = _font;
		}
		label.text = text;
		label.fontSize = size;
		label.color = color;
		label.alignment = align;
		label.raycastTarget = false;
		label.overflowMode = TextOverflowModes.Ellipsis;
		return label;
	}

	// ------------------------------------------------------------------ font / sprite

	private static bool IsBrokenTmpFont(TMP_FontAsset font)
	{
		if (font == null)
		{
			return true;
		}
		return font.name.IndexOf("LiberationSans", StringComparison.OrdinalIgnoreCase) >= 0;
	}

	private static TMP_FontAsset ResolveFont()
	{
		if (InventoryGui.instance != null)
		{
			TMP_Text[] texts = InventoryGui.instance.GetComponentsInChildren<TMP_Text>(includeInactive: true);
			for (int i = 0; i < texts.Length; i++)
			{
				if (texts[i] != null && !IsBrokenTmpFont(texts[i].font))
				{
					return texts[i].font;
				}
			}
		}
		if (Hud.instance != null)
		{
			TMP_Text[] texts = Hud.instance.GetComponentsInChildren<TMP_Text>(includeInactive: true);
			for (int i = 0; i < texts.Length; i++)
			{
				if (texts[i] != null && !IsBrokenTmpFont(texts[i].font))
				{
					return texts[i].font;
				}
			}
		}
		if (!IsBrokenTmpFont(TMP_Settings.defaultFontAsset))
		{
			return TMP_Settings.defaultFontAsset;
		}
		return null;
	}

	/// <summary>The wooden background. The texture belongs to TextureLoader's cache (shared with the other panels), so
	/// only the Sprite is created here - and only the Sprite is destroyed on unload.</summary>
	private static Sprite LoadPanelSprite()
	{
		Texture2D texture = TextureLoader.LoadUITexture("PanelBackground");
		if (texture == null)
		{
			return null;
		}
		return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
	}
}
