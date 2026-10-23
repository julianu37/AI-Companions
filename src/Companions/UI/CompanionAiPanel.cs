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
/// Fase 3.9 - <b>tabs and the summoner</b>: the panel is no longer one page. A strip of tabs sits under the title: the
/// first tab is the AI settings (<see cref="ModLocalization"/> key <c>hc_ai_tab_main</c>) and every companion of the
/// local player gets a tab of its own, ordered by name. The AI tab also carries the summoner - a tier dropdown plus
/// "Invocar", which spawns a companion <b>for free</b> (no coins, no Trader bank, no limit, through
/// <see cref="CompanionManager.SpawnFree"/>, i.e. the very spawn the Trader's purchase runs) - and the count of the
/// companions the player has. A companion's tab shows its name, its tier and whether it is alive, and its "Chamar"
/// button brings it over through the same recall the command key uses (<see cref="CompanionInteract.RecallOne"/> and
/// <see cref="ReturnLogic"/>). There is no dismiss button here on purpose: the wheel already has one, and it is
/// permanent.
///
/// The pages are built once, like everything else; only the tab buttons are destroyed and rebuilt (on open and after
/// a summon), because a companion can be born or die between two openings.
///
/// Fase 3.10 - <b>the profile of each companion</b>: the voice and the personality stopped being one global setting
/// for the whole world. A companion's tab now carries its own "voice and personality" section (the same hand-built
/// dropdowns, the same "Ouvir", the same "Aplicar"), and those controls write into <b>that companion's ZDO</b> through
/// <see cref="CompanionProfile"/> - the AI tab keeps writing the <c>[IA]</c> values, which are the <b>default</b> of
/// every companion that has nothing of its own. The first entries of both lists are explicit: "Follow the [IA]
/// default" clears what that companion stores, and the footer says "(padrão)" when the value in use is the global
/// one. "Ouvir" previews exactly the voice of the context: the companion's on its tab, the global one on the AI tab.
///
/// The panel needs no companion nearby (the AI tab is a global setting of the mod): the title just names the
/// companion that would answer right now, and the "Ouvir" preview uses that same companion as the 3D source of the
/// voice.
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

	private const float PanelH = 650f;

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

	// ------------------------------------------------------------------ tabs (Fase 3.9)

	/// <summary>Height of the strip of tabs (the AI tab and one per companion).</summary>
	private const float TabH = 30f;

	/// <summary>Top of the strip of tabs, measured down from the top of the panel (right below the title).</summary>
	private const float TabRowY = 38f;

	/// <summary>Space between two tabs.</summary>
	private const float TabGap = 4f;

	/// <summary>Narrowest a tab may get; past that the strip scrolls sideways (the player may summon any number).</summary>
	private const float TabMinW = 56f;

	/// <summary>Width of a tab when there is room for it.</summary>
	private const float TabMaxW = 150f;

	/// <summary>How far everything below the strip of tabs moved down (the tab row plus its breathing room).</summary>
	private const float TabShift = TabH + 10f;

	// Top of each row, measured down from the top of the panel (rows step by RowH + 8).
	private const float RowPersonality = 40f + TabShift;

	private const float RowFreeText = 82f + TabShift;

	private const float RowVoice = 124f + TabShift;

	private const float RowVolume = 166f + TabShift;

	private const float RowKey = 208f + TabShift;

	private const float RowToggleA = 250f + TabShift;

	private const float RowToggleB = 292f + TabShift;

	private const float RowMemory = 334f + TabShift;

	private const float RowSpeed = 376f + TabShift;

	// Fase 3.9: the summoner at the bottom of the AI tab - a section title, the tier row and the counter
	private const float RowSpawnTitle = 452f;

	private const float RowSpawnTier = 476f;

	private const float RowSpawnCount = 514f;

	/// <summary>Width of the tier dropdown of the summoner ("Invocar" takes the rest of the row).</summary>
	private const float TierFieldW = 300f;

	/// <summary>Fase 3.9: the shortest gap between two summons, so a double click can't create two companions.</summary>
	private const float SummonInterval = 0.5f;

	// Fase 3.9: the page of a companion's tab (same coordinates: the page fills the whole panel)
	private const float RowCompName = 80f;

	private const float RowCompTier = 122f;

	private const float RowCompStatus = 164f;

	private const float RowCompRecall = 206f;

	// Fase 3.10: the profile of that companion (its own voice and personality) sits under the "Chamar" button
	private const float RowCompProfileTitle = 252f;

	private const float RowCompPersonality = 278f;

	private const float RowCompFreeText = 320f;

	private const float RowCompVoice = 362f;

	private const float RowCompHint = 408f;

	/// <summary>The hint of the "Chamar" button, kept from Fase 3.9 (the profile hint above explains the defaults).</summary>
	private const float RowCompCallHint = 452f;

	// The footer is two two-line blocks over their own dark strip (see BuildFooter): the summary of what is in use
	// right now and the hint line. It used to be two 11px dark-grey single lines that were unreadable over the wooden
	// panel - and the second one was clipped by the bottom edge of the panel.
	private const float RowStatus = 546f;

	private const float RowHint = 590f;

	/// <summary>Height of one line of the footer (the blocks are two lines tall).</summary>
	private const float FooterLineH = 18f;

	/// <summary>How often the status line and the "Ouvir" button are refreshed (never per frame).</summary>
	private const float StatusInterval = 0.5f;

	/// <summary>Sorting order of the panel's own canvas, from <see cref="ModUiLayers"/> - the one place where the
	/// whole band of the mod's UI is documented (the game's play HUD is at 400, so 33 - the number this panel has
	/// always had - is behind it).</summary>
	private const int PanelSortingOrder = ModUiLayers.AiPanel;

	/// <summary>Sorting order of the overlay canvas that holds the dropdown lists: one step above the panel, so the
	/// list of an open dropdown is drawn - and clicked - above every control of the panel.</summary>
	private const int OverlaySortingOrder = ModUiLayers.AiPanelOverlay;

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

	/// <summary>Fase 3.9: the tier dropdown of the summoner at the bottom of the AI tab.</summary>
	private AiDropdown _tier;

	/// <summary>The six tiers the summoner offers, in the order the dropdown shows them.</summary>
	private static readonly CompanionTierDef[] Tiers = new CompanionTierDef[6]
	{
		CompanionTierData.Companion,
		CompanionTierData.DvergerWarrior,
		CompanionTierData.Dverger,
		CompanionTierData.DvergerFire,
		CompanionTierData.DvergerIce,
		CompanionTierData.DvergerSupport
	};

	/// <summary>The prefab hash of each tier, computed once: a companion's ZDO keeps the prefab it was spawned from,
	/// and that is how a tab names its tier (<see cref="TierName"/>).</summary>
	private static readonly int[] TierHashes = BuildTierHashes();

	private static int[] BuildTierHashes()
	{
		int[] hashes = new int[Tiers.Length];
		for (int i = 0; i < Tiers.Length; i++)
		{
			hashes[i] = Tiers[i].PrefabName.GetStableHashCode();
		}
		return hashes;
	}

	// ------------------------------------------------------------------ tabs / pages (Fase 3.9)

	/// <summary>Every control of the AI tab lives under this page, so a tab switch is one SetActive.</summary>
	private GameObject _aiPage;

	/// <summary>The page of a companion's tab (built once and filled with whoever is selected).</summary>
	private GameObject _compPage;

	/// <summary>The page the primitives parent to: the AI page while building it, the companion page while building
	/// that one, null (the panel root) for the title and the strip of tabs, which every tab shares.</summary>
	private Transform _page;

	private RectTransform _tabContent;

	private ScrollRect _tabScroll;

	/// <summary>The tabs of the strip: index 0 is the AI tab (<c>Companion == null</c>), the rest are companions.</summary>
	private readonly List<AiTab> _tabs = new List<AiTab>();

	/// <summary>The tab buttons themselves, so they can be destroyed and rebuilt (companions are born and die).</summary>
	private readonly List<GameObject> _tabObjects = new List<GameObject>();

	/// <summary>Index into <see cref="_tabs"/> of the open tab.</summary>
	private int _activeTab;

	private TMP_Text _spawnCount;

	/// <summary>Fase 3.9: the earliest moment the next summon may run (see <see cref="Summon"/>).</summary>
	private float _nextSummonAt;

	private TMP_Text _compName;

	private TMP_Text _compTier;

	private TMP_Text _compStatus;

	private AiButton _recall;

	// ------------------------------------------------------------------ Fase 3.10: the profile of the open companion

	/// <summary>The personality dropdown of a companion's tab: the "follow the [IA] default" entry, the default
	/// personality, the presets and the free text.</summary>
	private AiDropdown _compPersonality;

	/// <summary>The voice dropdown of a companion's tab: "follow the [IA] default" and the three pt-BR voices.</summary>
	private AiDropdown _compVoice;

	private TMP_InputField _compFreeText;

	private AiButton _compApply;

	private AiButton _compListen;

	private string[] _compPersonalityIds;

	private string[] _compPersonalityLabels;

	private int[] _compVoiceIds;

	private string[] _compVoiceLabels;

	/// <summary>The companion whose profile the controls of the companion page are showing, so they are only
	/// refilled when the tab really changes (a twice-a-second refill would fight the text box being typed in).</summary>
	private CompanionSetup _compFormFor;

	/// <summary>
	/// Fase 3.12 (auditoria): the two footer labels of the open companion, computed when its tab loads (and after every
	/// change made on it), never on the twice-a-second refresh - reading them live meant four ZDO keys per half
	/// second for a text that only changes when the player changes something.
	/// </summary>
	private string _compStatusPersonality;

	private string _compStatusVoice;

	/// <summary>The dropdown whose list is open right now (only one at a time), or null.</summary>
	private AiDropdown _openList;

	private readonly List<AiToggle> _toggles = new List<AiToggle>();

	private string[] _personalityIds;

	private string[] _personalityLabels;

	private int[] _voiceIds;

	private float _nextStatus;

	/// <summary>The raycast audit of this panel runs once per session (the first open), never per frame.</summary>
	private static bool _raycastAudited;

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
			+ "volume, velocidade, memoria, conversa e o push-to-talk ficam todos ali, e cada mudanca e salva na hora no .cfg. "
			+ "A aba IA tambem invoca companheiros de graca (sem moedas) e cada companheiro tem a sua aba, com o botao Chamar "
			+ "e a voz e a personalidade DELE (guardadas no ZDO dele; a aba IA e o padrao de quem nao tem escolha propria).");
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
		// Fase 3.9: the strip of tabs is rebuilt on every open (a companion can be summoned or die between two
		// openings, and the tab of one that is gone must not linger); every other control is still built once.
		RebuildTabs();
		RefreshSpawnCount();
		RefreshStatus();
		Cursor.lockState = CursorLockMode.None;
		Cursor.visible = true;
		EnsureEventSystem();
		LogRaycastAudit();
	}

	/// <summary>
	/// One shot per session, on the first open: the raycast audit of this panel - the same one the F8 panel runs
	/// (<see cref="ModUiAudit"/>), with the geometry of <b>this</b> panel. It answers "who is in front of the
	/// controls?" with the layers of the scene, and it is the cheap insurance against the swallowed click coming
	/// back: this panel used to be at 33, behind the game's play HUD at 400, and the audit is what says so out loud.
	/// Once, never per frame.
	///
	/// <para>It only runs with <c>[Advanced] UiDiag = true</c> (off by default): while it is off nothing is built and
	/// nothing is walked. The one-shot flag is only set when it really runs, so turning the switch on later in the
	/// session still gets the audit on the next open.</para>
	/// </summary>
	private void LogRaycastAudit()
	{
		if (_raycastAudited || !ModUiAudit.Enabled)
		{
			return;
		}
		_raycastAudited = true;
		ModUiAudit.Log("AI panel raycast audit", PanelSortingOrder, GetComponent<Canvas>(),
			_root != null ? _root.GetComponent<RectTransform>() : null, ProbePoints, _backdrop, this);
	}

	/// <summary>The probed points (panel-local; the panel is 680x650 with a centre pivot): the middle of the column
	/// of controls, at eleven heights, so a band anywhere over the controls is caught. The column starts at
	/// <c>FieldX</c> (166 from the left edge) and stops at <c>RightEdge</c> (672), so its centre is 419 from the left
	/// edge - i.e. 79 from the centre pivot - and the rows run from 80 to 590 from the top (the strip of tabs and the
	/// summoner of Fase 3.9 moved everything below the title down).</summary>
	private static readonly Vector2[] ProbePoints = new Vector2[11]
	{
		new Vector2(79f, 245f),
		new Vector2(79f, 194f),
		new Vector2(79f, 143f),
		new Vector2(79f, 92f),
		new Vector2(79f, 41f),
		new Vector2(79f, -10f),
		new Vector2(79f, -61f),
		new Vector2(79f, -112f),
		new Vector2(79f, -163f),
		new Vector2(79f, -214f),
		new Vector2(79f, -265f)
	};

	public void Hide()
	{
		_visible = false;
		CloseLists();
		_listeningKey = false;
		if (_freeText != null && _freeText.isFocused)
		{
			_freeText.DeactivateInputField();
		}
		// Fase 3.10: the text box of a companion's profile is a text box like any other
		if (_compFreeText != null && _compFreeText.isFocused)
		{
			_compFreeText.DeactivateInputField();
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
			if (_compFreeText != null && _compFreeText.isFocused)
			{
				_compFreeText.DeactivateInputField();
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
		if (Player.m_localPlayer == null || Player.m_localPlayer.IsDead())
		{
			// Fase 3.9: also while the player is dead - the panel would open and close itself on the next tick anyway
			// (RefreshStatus calls Hide() for a dead player), which reads as a flicker of the whole window
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
		// nor on top of the starter companion picker of the first spawn: that one is choosing the very companion this
		// panel could now summon (Fase 3.9), and stacking two of the mod's panels is never what the player meant
		if (StarterCompanionPanel.Instance != null && StarterCompanionPanel.Instance.IsVisible)
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
		if (_compFreeText != null && _compFreeText.isFocused)
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
		// Fase 3.9: the companion of the open tab is a scene object and can leave the world while the panel is up
		// (dead and destroyed, dismissed from the wheel, world change). Unity answers "null" for a destroyed object,
		// so SelectedCompanion() starts returning null, but nothing else would move the panel off the companion page -
		// it would sit there with "Nome = ?" and the AI tab hidden. Rebuilding the strip drops the tab of a companion
		// that is gone, and ApplyTab falls back to the AI tab (index 0) for that same reason.
		CompanionSetup selected = SelectedCompanion();
		if (_activeTab > 0 && selected == null)
		{
			RebuildTabs();
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
		// Fase 3.10: the "Ouvir" of a companion's tab follows the same rule (and it exists from the first build)
		if (_compListen != null)
		{
			_compListen.Button.interactable = ttsReady;
			if (_compListen.Label != null)
			{
				_compListen.Label.color = ttsReady ? Color.white : DimColor;
			}
		}
		// The title names the companion that would answer right now (the same one the "Ouvir" preview uses as the
		// source of the voice), so it is clear who is talking before the sample plays. Fase 3.9: on a companion's tab
		// it names that companion instead - the page below is about it, not about the voice.
		if (_title != null)
		{
			string name = selected != null ? NameOf(selected) : CompanionName(CompanionChatAI.PickSpeaker());
			_title.text = string.IsNullOrEmpty(name)
				? ModLocalization.Loc("hc_ai_panel_title")
				: ModLocalization.LocFmt("hc_ai_panel_title_named", name);
		}
		if (_status != null)
		{
			string key = ModConfig.VozTecla != null ? ModConfig.VozTecla.Value.ToString() : "?";
			// two localized lines instead of one long line: the personality on the first, the voice and the
			// push-to-talk key on the second (the footer block is two lines tall). Fase 3.10: the summary follows the
			// open tab - a companion's own values on its tab, the global ones on the AI tab - and says "(padrão)"
			// when what is in use is the global value, so it is never a mystery whose voice is about to be heard.
			_status.text = ModLocalization.LocFmt("hc_ai_panel_status_personality", PersonalityStatusLabel(selected))
				+ "\n" + ModLocalization.LocFmt("hc_ai_panel_status_voice", VoiceStatusLabel(selected), key);
		}
		// Fase 3.9: a companion's tab shows whether it is alive, so it follows the companion with this same tick - one
		// health read twice a second, and the texts only change when they really change (TMP compares the string)
		if (selected != null)
		{
			RefreshCompanionPage();
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
		// The backdrop is the modal click catcher, the same one the F8 panel has: it stays a raycast target (the
		// default of a uGUI Image) so a click outside the panel hits this - and does nothing - instead of the game's
		// UI behind it. It is created BEFORE the panel root, so its render depth, and therefore its raycast priority,
		// is strictly lower than every control: the controls always win, and this is what catches everything else.
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
		// The panel's own background is decorative, exactly like the F8 panel's (ConfigPanel.Build says the same
		// thing): it is the parent of every control, so it can never be in front of one, and a click on the padding
		// is better left to the backdrop - the modal click catcher created just before this root - than to die here.
		// (It used to be raycastTarget = true; the raycast audit reported it as a false positive of this panel the
		// first time it ran with [Advanced] UiDiag on.)
		rootImage.raycastTarget = false;

		BuildHeader();
		BuildTabBar();

		// Fase 3.9: one page for the AI tab, one for a companion's tab. Both fill the panel (i.e. they use the same
		// coordinates as Place), so switching tabs is showing one page and hiding the other - never a rebuild, and
		// never a control that has to be laid out again.
		_aiPage = CreatePage("AiPage");
		_page = _aiPage.transform;
		BuildRowBackgrounds();
		BuildPersonality();
		BuildVoice();
		BuildPushToTalk();
		BuildToggles();
		BuildNumbers();
		BuildSpawner();
		BuildFooter();
		_compPage = CreatePage("CompanionPage");
		_page = _compPage.transform;
		BuildCompanionPage();
		_page = null;
		_compPage.SetActive(false);

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
		// the fields of the dropdowns belong to the page they are shown on (only the lists float)
		_page = _aiPage.transform;
		BuildDropdowns();
		// Fase 3.10: and the two dropdowns of a companion's profile, on its own page (the lists go to the same
		// overlay, which is why this happens after it exists)
		_page = _compPage.transform;
		BuildCompanionDropdowns();
		_page = null;
		// The fields of the dropdowns are created by BuildDropdowns/BuildCompanionDropdowns with their page as the
		// parent, i.e. AFTER the overlay: in uGUI the render order is the order of the siblings (the last child draws
		// on top), so without this line the field of "Voz", the free-text box and "Aplicar" would be drawn OVER the
		// open list - which is exactly the bug of the screenshot (the list of "Personalidade" appearing behind the
		// rows below it). The overlay canvas above already wins over the panel canvas, but the sibling order is kept
		// correct too, so nothing depends on a single mechanism.
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

	// ------------------------------------------------------------------ tabs and pages (Fase 3.9)

	/// <summary>The page the primitives below parent their controls to: the AI page, the companion page, or the panel
	/// root (the title and the strip of tabs, which every tab shares) while <see cref="_page"/> is null.</summary>
	private Transform Page => _page != null ? _page : _root.transform;

	/// <summary>A full-panel container: its top-left corner is the panel's, so the coordinates of <see cref="Place"/>
	/// mean the same thing inside a page as they do on the panel. No Image - a page never eats a click.</summary>
	private GameObject CreatePage(string name)
	{
		GameObject page = new GameObject(name, typeof(RectTransform));
		page.transform.SetParent(_root.transform, worldPositionStays: false);
		RectTransform rt = page.GetComponent<RectTransform>();
		rt.anchorMin = Vector2.zero;
		rt.anchorMax = Vector2.one;
		rt.offsetMin = Vector2.zero;
		rt.offsetMax = Vector2.zero;
		return page;
	}

	/// <summary>
	/// The strip of tabs: the AI tab and then one per companion of the local player. It is a horizontal scroll view
	/// because the player may summon as many companions as they want - the tabs shrink down to <see cref="TabMinW"/>
	/// and past that the strip scrolls (dragging, or the mouse wheel over it: see <see cref="AiTabStripWheel"/>).
	///
	/// <para>The buttons are <b>not</b> built here: they are destroyed and rebuilt by <see cref="RebuildTabs"/> on
	/// every open, because a companion can be summoned or die between two openings; the scroll view itself is built
	/// once, like every other control of the panel.</para>
	/// </summary>
	private void BuildTabBar()
	{
		GameObject strip = new GameObject("TabStrip", typeof(RectTransform), typeof(ScrollRect));
		strip.transform.SetParent(_root.transform, worldPositionStays: false);
		Place(strip.GetComponent<RectTransform>(), LeftX, TabRowY, RightEdge - LeftX, TabH);

		GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
		viewport.transform.SetParent(strip.transform, worldPositionStays: false);
		RectTransform viewportRT = viewport.GetComponent<RectTransform>();
		viewportRT.anchorMin = Vector2.zero;
		viewportRT.anchorMax = Vector2.one;
		viewportRT.offsetMin = Vector2.zero;
		viewportRT.offsetMax = Vector2.zero;

		GameObject content = new GameObject("Content", typeof(RectTransform));
		content.transform.SetParent(viewport.transform, worldPositionStays: false);
		RectTransform contentRT = content.GetComponent<RectTransform>();
		// anchored to the left edge and as tall as the strip: the tabs sit at y = 0 inside it, so the strip scrolls
		// sideways only (and a strip that fits never moves)
		contentRT.anchorMin = new Vector2(0f, 0f);
		contentRT.anchorMax = new Vector2(0f, 1f);
		contentRT.pivot = new Vector2(0f, 1f);
		contentRT.anchoredPosition = Vector2.zero;
		contentRT.sizeDelta = new Vector2(RightEdge - LeftX, 0f);

		ScrollRect scroll = strip.GetComponent<ScrollRect>();
		scroll.viewport = viewportRT;
		scroll.content = contentRT;
		scroll.horizontal = true;
		scroll.vertical = false;
		scroll.movementType = ScrollRect.MovementType.Clamped;
		scroll.inertia = false;
		scroll.scrollSensitivity = 20f;

		// the wheel is vertical in Unity and this strip only scrolls sideways: this helper sits on the viewport, i.e.
		// under the ScrollRect in the event bubble, so it is the one that gets the wheel (see AttachHorizontalWheel)
		AttachHorizontalWheel(scroll);

		_tabContent = contentRT;
		_tabScroll = scroll;
	}

	/// <summary>
	/// Rebuilds the strip for the companions the local player has <b>right now</b>. Called from <see cref="Show"/>
	/// (a companion can be born or die between two openings) and right after a summon - never per frame. The open
	/// tab survives when the companion it pointed at still exists; when it doesn't, the panel goes back to the AI
	/// tab. The tabs are ordered by name, so they don't dance between openings.
	/// </summary>
	private void RebuildTabs()
	{
		CompanionSetup previous = SelectedCompanion();
		for (int i = 0; i < _tabObjects.Count; i++)
		{
			if (_tabObjects[i] != null)
			{
				UnityEngine.Object.Destroy(_tabObjects[i]);
			}
		}
		_tabObjects.Clear();
		_tabs.Clear();

		// the AI tab is always the first one, and it is the one with a null companion
		_tabs.Add(new AiTab
		{
			Label = ModLocalization.Loc("hc_ai_tab_main"),
			Companion = null
		});
		List<CompanionSetup> mine = OwnCompanions();
		for (int i = 0; i < mine.Count; i++)
		{
			_tabs.Add(new AiTab
			{
				Label = NameOf(mine[i]),
				Companion = mine[i]
			});
		}

		float available = RightEdge - LeftX;
		int count = _tabs.Count;
		float width = Mathf.Clamp((available - TabGap * (count - 1)) / count, TabMinW, TabMaxW);
		float total = count * width + (count - 1) * TabGap;
		// the content is at least as wide as the strip: tabs narrower than the strip simply sit on the left
		_tabContent.sizeDelta = new Vector2(Mathf.Max(available, total), 0f);
		// Fase 3.9: the strip always goes back to the first tab after a rebuild. A ScrollRect only re-clamps when it is
		// dragged or scrolled, so a strip that shrank (a companion dismissed or dead) kept the old sideways offset and
		// the first tabs stayed out of sight - the "strip looks shifted" of the audit. Writing 0 when the tabs fit
		// moves nothing (the content is already at the left edge) and it is what a brand new strip wants anyway.
		if (_tabScroll != null)
		{
			_tabScroll.horizontalNormalizedPosition = 0f;
		}

		for (int i = 0; i < count; i++)
		{
			int index = i;
			AiButton button = CreateButton(_tabContent, "Tab" + i, i * (width + TabGap), 0f, width, TabH, _tabs[i].Label, delegate
			{
				SelectTab(index);
			});
			_tabs[i].Button = button.Button.gameObject;
			_tabs[i].Image = button.Image;
			_tabs[i].Text = button.Label;
			_tabObjects.Add(button.Button.gameObject);
		}

		int active = 0;
		if (previous != null)
		{
			for (int i = 1; i < _tabs.Count; i++)
			{
				if (_tabs[i].Companion == previous)
				{
					active = i;
					break;
				}
			}
		}
		ApplyTab(active);
	}

	private void SelectTab(int index)
	{
		if (index < 0 || index >= _tabs.Count)
		{
			return;
		}
		// an open list belongs to the tab it was opened in (and its catcher would swallow the first click)
		CloseLists();
		// the key listener of "Alterar" only runs while its page is on screen: leaving the AI tab mid-capture would
		// never finish and would keep the panel key blocked
		CancelKeyListening();
		ApplyTab(index);
	}

	/// <summary>Stops a push-to-talk capture that is waiting for a key, putting the current key back on the label.</summary>
	private void CancelKeyListening()
	{
		if (!_listeningKey)
		{
			return;
		}
		_listeningKey = false;
		if (_keyLabel != null)
		{
			_keyLabel.text = ModConfig.VozTecla != null ? ModConfig.VozTecla.Value.ToString() : "?";
			_keyLabel.color = ValueColor;
		}
	}

	/// <summary>Shows the page of the tab at <paramref name="index"/> and paints the strip (the open tab is gold).</summary>
	private void ApplyTab(int index)
	{
		_activeTab = Mathf.Clamp(index, 0, Mathf.Max(0, _tabs.Count - 1));
		for (int i = 0; i < _tabs.Count; i++)
		{
			AiTab tab = _tabs[i];
			bool on = i == _activeTab;
			if (tab.Image != null)
			{
				tab.Image.color = on ? GoldColor : ButtonColor;
			}
			if (tab.Text != null)
			{
				tab.Text.color = on ? Color.white : DimColor;
			}
		}
		bool ai = SelectedCompanion() == null;
		if (_aiPage != null)
		{
			_aiPage.SetActive(ai);
		}
		if (_compPage != null)
		{
			_compPage.SetActive(!ai);
		}
		// the companion page is filled by RefreshStatus (it refreshes the selected companion on every tick); the
		// title follows the open tab for the same reason
		RefreshStatus();
	}

	/// <summary>The companion of the open tab, or null when the AI tab is the one open.</summary>
	private CompanionSetup SelectedCompanion()
	{
		return (_activeTab > 0 && _activeTab < _tabs.Count) ? _tabs[_activeTab].Companion : null;
	}

	/// <summary>
	/// The local player's companions that are in the scene right now - dead ones included (their tab says so) - in a
	/// stable order. Ownership is the same check the rest of the mod uses: the ZDO's <c>HC_Owner</c> against the local
	/// player's id, so another player's companions never get a tab here. Allocates a list: called on open and on a
	/// summon, never per frame.
	/// </summary>
	private static List<CompanionSetup> OwnCompanions()
	{
		List<CompanionSetup> list = new List<CompanionSetup>();
		Player player = Player.m_localPlayer;
		if (player == null)
		{
			return list;
		}
		string id = player.GetPlayerID().ToString();
		IReadOnlyList<CompanionSetup> all = CompanionSetup.AllCompanions;
		for (int i = 0; i < all.Count; i++)
		{
			CompanionSetup setup = all[i];
			if (setup == null || setup._mhrStale)
			{
				continue;
			}
			ZDO zdo = setup.GetComponent<ZNetView>()?.GetZDO();
			if (zdo == null || zdo.GetString(CompanionSetup.OwnerHash) != id)
			{
				continue;
			}
			list.Add(setup);
		}
		list.Sort(CompareCompanions);
		return list;
	}

	private static int CompareCompanions(CompanionSetup a, CompanionSetup b)
	{
		int byName = string.Compare(NameOf(a), NameOf(b), StringComparison.OrdinalIgnoreCase);
		// two companions may share a name: the instance id keeps the order stable even then
		return byName != 0 ? byName : a.GetInstanceID().CompareTo(b.GetInstanceID());
	}

	/// <summary>
	/// The name of a companion for its tab: the one the player gave it (the ZDO's <c>HC_Name</c>, which the interact
	/// panel writes when it is renamed), or - for a companion that was never renamed - the name it carries as a
	/// Character (the tier name the hover shows, see <c>CompanionPrefabs</c>). Never empty.
	/// </summary>
	private static string NameOf(CompanionSetup setup)
	{
		string name = CompanionName(setup);
		if (string.IsNullOrEmpty(name))
		{
			Character character = setup != null ? setup.GetComponent<Character>() : null;
			name = character != null ? character.m_name : null;
		}
		return string.IsNullOrEmpty(name) ? ModLocalization.Loc("hc_msg_name_default") : name;
	}

	/// <summary>
	/// Turns the wheel into horizontal scrolling for the strip of tabs. A <see cref="ScrollRect"/> only reads the
	/// wheel for the axis it scrolls, and this strip only scrolls sideways, so without this the wheel would do
	/// nothing over the strip (dragging would still work). It lives on the viewport - under the ScrollRect in the
	/// event bubble - so it is the one that receives the event.
	/// </summary>
	private void AttachHorizontalWheel(ScrollRect scroll)
	{
		AiTabStripWheel wheel = scroll.viewport.gameObject.AddComponent<AiTabStripWheel>();
		wheel.Init(scroll);
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
			row.transform.SetParent(Page, worldPositionStays: false);
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
		CreateButton(Page, "Apply", applyX, RowFreeText, RightEdge - applyX, RowH, ModLocalization.Loc("hc_ai_panel_apply"), ApplyPersonality);
	}

	private void BuildVoice()
	{
		CreateLabel(RowVoice, ModLocalization.Loc("hc_ai_panel_voice"));
		float listenX = FieldX + 298f;
		_listen = CreateButton(Page, "Listen", listenX, RowVoice, RightEdge - listenX, RowH, ModLocalization.Loc("hc_ai_panel_listen"), PreviewVoice);

		CreateLabel(RowVolume, ModLocalization.Loc("hc_ai_panel_volume"));
		_volume = CreateSlider("Volume", FieldX, RowVolume, 440f, RowH, 0f, 1f, 1f, false, OnVolumeChanged);
		_volumeLabel = CreateText(Page, "", RightEdge - 60f, RowVolume + 6f, 60f, 22f, 12f, ValueColor, TextAlignmentOptions.MidlineRight);
	}

	private void BuildPushToTalk()
	{
		CreateLabel(RowKey, ModLocalization.Loc("hc_ai_panel_push_to_talk"));

		GameObject keyBox = new GameObject("KeyBox", typeof(RectTransform), typeof(Image));
		keyBox.transform.SetParent(Page, worldPositionStays: false);
		Place(keyBox.GetComponent<RectTransform>(), FieldX, RowKey, 170f, RowH);
		Image keyBoxImage = keyBox.GetComponent<Image>();
		keyBoxImage.color = new Color(0.12f, 0.12f, 0.15f, 0.9f);
		// Decorative: it is the dark box the key is drawn in, there is nothing to click on it (the "Alterar" button is
		// its own GameObject right next to it), so it does not eat a click - the backdrop, right behind the panel,
		// catches whatever lands here. It was reported as a false positive of the panel by the raycast audit.
		keyBoxImage.raycastTarget = false;
		_keyLabel = CreateText(keyBox.transform, "?", 0f, 0f, 170f, RowH, 13f, ValueColor, TextAlignmentOptions.Center);

		// The same KeyBindListener the F8 panel uses: click "Alterar", press a key, and it lands in [IA] VozTecla.
		float changeX = FieldX + 178f;
		AiButton change = CreateButton(Page, "ChangeKey", changeX, RowKey, 110f, RowH, ModLocalization.Loc("hc_ai_panel_change"), null);
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
		_memoryLabel = CreateText(Page, "", RightEdge - 60f, RowMemory + 6f, 60f, 22f, 12f, ValueColor, TextAlignmentOptions.MidlineRight);

		CreateLabel(RowSpeed, ModLocalization.Loc("hc_ai_panel_speed"));
		// the bounds of [IA] TtsVelocidade (0,5–2,0) come from CompanionTts, the same constants its clamp uses: the
		// slider used to stop at 1,5, so a .cfg with 2,0 was shown as "2.00" with the handle at the end and the
		// first drag silently lowered the setting to 1,5
		_speed = CreateSlider("Speed", FieldX, RowSpeed, 440f, RowH, CompanionTts.MinSpeed, CompanionTts.MaxSpeed, 1f, false, OnSpeedChanged);
		_speedLabel = CreateText(Page, "", RightEdge - 60f, RowSpeed + 6f, 60f, 22f, 12f, ValueColor, TextAlignmentOptions.MidlineRight);
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
		strip.transform.SetParent(Page, worldPositionStays: false);
		Place(strip.GetComponent<RectTransform>(), 4f, RowStatus - 6f, RightEdge - 8f, RowHint - RowStatus + 2f * FooterLineH + 12f);
		Image stripImage = strip.GetComponent<Image>();
		stripImage.color = FooterBackColor;
		stripImage.raycastTarget = false;

		_status = CreateText(Page, "", LeftX + 4f, RowStatus, RightEdge - LeftX - 8f, 2f * FooterLineH, 12.5f,
			FooterColor, TextAlignmentOptions.TopLeft);
		// the summary is built from two localized lines (see RefreshStatus): never truncated, never one long line
		_status.overflowMode = TextOverflowModes.Overflow;

		string key = ModConfig.AiPanelKey != null ? ModConfig.AiPanelKey.Value.ToString() : "?";
		TMP_Text hint = CreateText(Page, ModLocalization.LocFmt("hc_ai_panel_hint_fmt", key), LeftX + 4f, RowHint,
			RightEdge - LeftX - 8f, 2f * FooterLineH, 12f, FooterHintColor, TextAlignmentOptions.TopLeft);
		hint.overflowMode = TextOverflowModes.Overflow;
	}

	// ------------------------------------------------------------------ the summoner (Fase 3.9)

	/// <summary>
	/// The summoner of the AI tab: a section title, a tier dropdown and the "Invocar" button, plus a counter of how
	/// many companions the player has right now. Summoning costs nothing - no coins, no Trader bank, no limit - and
	/// runs the very same spawn the Trader's purchase runs (<see cref="CompanionManager.SpawnFree"/>), so a companion
	/// summoned here is exactly the companion bought there.
	///
	/// <para>The dropdown's field is created in <see cref="BuildDropdowns"/> (it needs the overlay the lists live
	/// on); this method builds the section title, the row and the counter.</para>
	/// </summary>
	private void BuildSpawner()
	{
		TMP_Text title = CreateText(Page, ModLocalization.Loc("hc_ai_spawn_title"), LeftX, RowSpawnTitle,
			RightEdge - LeftX, 20f, 14f, LabelColor, TextAlignmentOptions.MidlineLeft);
		title.fontStyle = FontStyles.Bold;
		title.overflowMode = TextOverflowModes.Overflow;

		// the row background, like every other row (the section title and the counter have none on purpose)
		GameObject row = new GameObject("RowSpawn", typeof(RectTransform), typeof(Image));
		row.transform.SetParent(Page, worldPositionStays: false);
		Place(row.GetComponent<RectTransform>(), 4f, RowSpawnTier, RightEdge - 4f, RowH);
		Image rowImage = row.GetComponent<Image>();
		rowImage.color = RowColor;
		rowImage.raycastTarget = false;

		CreateLabel(RowSpawnTier, ModLocalization.Loc("hc_ai_spawn_tier"));

		float buttonX = FieldX + TierFieldW + 8f;
		CreateButton(Page, "Summon", buttonX, RowSpawnTier, RightEdge - buttonX, RowH, ModLocalization.Loc("hc_ai_spawn_button"), Summon);

		_spawnCount = CreateText(Page, "", FieldX, RowSpawnCount, RightEdge - FieldX, 20f, 12.5f, FooterColor, TextAlignmentOptions.MidlineLeft);
		_spawnCount.overflowMode = TextOverflowModes.Overflow;
	}

	/// <summary>The "Invocar" button: a free spawn of the tier the dropdown shows, then the strip is rebuilt so the
	/// new companion already has its tab (and the counter is right again). Nothing is charged anywhere.</summary>
	private void Summon()
	{
		// Fase 3.9: a double click (or a second click landing in the same frame on a slow one) must not create two
		// companions - the spawn itself is instant, so a short guard is enough and the player never waits for it
		if (Time.unscaledTime < _nextSummonAt)
		{
			return;
		}
		_nextSummonAt = Time.unscaledTime + SummonInterval;
		CompanionTierDef def = TierAt(_tier != null ? _tier.Index : 0);
		if (!CompanionManager.SpawnFree(null, def))
		{
			CompanionChatAI.Announce(ModLocalization.Loc("hc_ai_spawn_failed"));
			return;
		}
		RebuildTabs();
		RefreshSpawnCount();
	}

	/// <summary>The tier at an index of the dropdown (the plain Companion for anything out of range).</summary>
	private static CompanionTierDef TierAt(int index)
	{
		return (index >= 0 && index < Tiers.Length) ? Tiers[index] : CompanionTierData.Companion;
	}

	/// <summary>Rewrites the "Companheiros: N" line. Called on open and after a summon only (it walks the scene).</summary>
	private void RefreshSpawnCount()
	{
		if (_spawnCount == null)
		{
			return;
		}
		_spawnCount.text = ModLocalization.LocFmt("hc_ai_spawn_count", OwnCompanions().Count);
	}

	// ------------------------------------------------------------------ the companion tab (Fase 3.9)

	/// <summary>
	/// The page of a companion's tab: its name (read-only), its tier, whether it is alive, the "Chamar" button and -
	/// Fase 3.10 - its <b>own</b> voice and personality.
	///
	/// <para>One page serves every companion tab - it is filled with whoever is selected when the tab opens (see
	/// <see cref="RefreshCompanionPage"/>), so ten companions are still one set of controls.</para>
	///
	/// <para>The two dropdowns of the profile are <b>not</b> built here: their lists have to go to the panel's
	/// overlay, which is created after both pages, so they are built by <see cref="BuildCompanionDropdowns"/> (the
	/// page is the parent of the fields either way, since <see cref="Page"/> is set to it again).</para>
	///
	/// <para>There is <b>no</b> dismiss/despawn button here on purpose: the wheel already has one, and it is
	/// permanent.</para>
	/// </summary>
	private void BuildCompanionPage()
	{
		float[] rows = new float[6] { RowCompName, RowCompTier, RowCompStatus, RowCompPersonality, RowCompFreeText, RowCompVoice };
		for (int i = 0; i < rows.Length; i++)
		{
			GameObject row = new GameObject("CompRow" + i, typeof(RectTransform), typeof(Image));
			row.transform.SetParent(Page, worldPositionStays: false);
			Place(row.GetComponent<RectTransform>(), 4f, rows[i], RightEdge - 4f, RowH);
			Image image = row.GetComponent<Image>();
			image.color = RowColor;
			image.raycastTarget = false;
		}

		// the name of the companion is the title of its tab, so the page repeats it as the first field (it is also
		// what the wheel and the hover show, and it is the one thing the player has to be sure about)
		CreateLabel(RowCompName, ModLocalization.Loc("hc_ai_comp_name"));
		_compName = CreateText(Page, "", FieldX, RowCompName + 6f, RightEdge - FieldX - 8f, 22f, 13f,
			Color.white, TextAlignmentOptions.MidlineLeft);

		CreateLabel(RowCompTier, ModLocalization.Loc("hc_ai_comp_tier"));
		_compTier = CreateText(Page, "", FieldX, RowCompTier + 6f, RightEdge - FieldX - 8f, 22f, 13f,
			ValueColor, TextAlignmentOptions.MidlineLeft);

		CreateLabel(RowCompStatus, ModLocalization.Loc("hc_ai_comp_status"));
		_compStatus = CreateText(Page, "", FieldX, RowCompStatus + 6f, RightEdge - FieldX - 8f, 22f, 13f,
			GoldColor, TextAlignmentOptions.MidlineLeft);

		_recall = CreateButton(Page, "Recall", FieldX, RowCompRecall, 200f, RowH, ModLocalization.Loc("hc_ai_comp_recall"), RecallSelected);

		// Fase 3.10: the profile of the companion - a section title, the two dropdowns (built in
		// BuildCompanionDropdowns), the free text with the "Aplicar" button and the hint that says the global values
		// are only the default.
		TMP_Text profileTitle = CreateText(Page, ModLocalization.Loc("hc_ai_comp_profile_title"), LeftX, RowCompProfileTitle,
			RightEdge - LeftX, 20f, 14f, LabelColor, TextAlignmentOptions.MidlineLeft);
		profileTitle.fontStyle = FontStyles.Bold;
		profileTitle.overflowMode = TextOverflowModes.Overflow;

		CreateLabel(RowCompPersonality, ModLocalization.Loc("hc_ai_panel_personality"));
		CreateLabel(RowCompFreeText, ModLocalization.Loc("hc_ai_panel_free_text"));
		float inputW = 380f;
		_compFreeText = CreateInputField("CompFreeText", FieldX, RowCompFreeText, inputW, RowH,
			ModLocalization.Loc("hc_ai_panel_free_placeholder"), CompanionPersona.MaxFreeTextLength);
		// Enter applies the profile of the companion (the same as the button) instead of the global one
		_compFreeText.onSubmit.RemoveAllListeners();
		_compFreeText.onSubmit.AddListener(delegate
		{
			ApplyCompPersonality();
		});
		float applyX = FieldX + inputW + 8f;
		_compApply = CreateButton(Page, "CompApply", applyX, RowCompFreeText, RightEdge - applyX, RowH, ModLocalization.Loc("hc_ai_panel_apply"), ApplyCompPersonality);

		CreateLabel(RowCompVoice, ModLocalization.Loc("hc_ai_panel_voice"));
		float listenX = FieldX + 298f;
		_compListen = CreateButton(Page, "CompListen", listenX, RowCompVoice, RightEdge - listenX, RowH, ModLocalization.Loc("hc_ai_panel_listen"), PreviewCompVoice);

		TMP_Text hint = CreateText(Page, ModLocalization.Loc("hc_ai_profile_hint"), LeftX, RowCompHint,
			RightEdge - LeftX, 2f * FooterLineH + 6f, 12f, FooterHintColor, TextAlignmentOptions.TopLeft);
		hint.overflowMode = TextOverflowModes.Overflow;

		TMP_Text callHint = CreateText(Page, ModLocalization.Loc("hc_ai_comp_hint"), LeftX, RowCompCallHint,
			RightEdge - LeftX, 2f * FooterLineH + 6f, 12f, FooterHintColor, TextAlignmentOptions.TopLeft);
		callHint.overflowMode = TextOverflowModes.Overflow;
	}

	/// <summary>
	/// Fase 3.10: the two dropdowns of a companion's profile - personality (with "follow the [IA] default" in front,
	/// then the default personality, the presets and the free text) and voice (with the same first entry, then the
	/// three pt-BR voices). Built with <see cref="_compPage"/> as the current page, so the fields land on the
	/// companion page and only the open lists float on the overlay.
	/// </summary>
	private void BuildCompanionDropdowns()
	{
		_compPersonalityIds = CompanionProfile.PresetIds(withInherit: true);
		_compPersonalityLabels = CompanionProfile.PresetLabels(withInherit: true);
		_compPersonality = CreateDropdown("CompPersonality", FieldX, RowCompPersonality, 380f, RowH,
			_compPersonalityLabels, 0, OnCompPersonalityPicked);

		_compVoiceIds = CompanionProfile.VoiceIds(withInherit: true);
		_compVoiceLabels = CompanionProfile.VoiceLabels(withInherit: true);
		_compVoice = CreateDropdown("CompVoice", FieldX, RowCompVoice, 290f, RowH,
			_compVoiceLabels, 0, OnCompVoicePicked);
	}

	/// <summary>
	/// Fills the companion page with the selected companion. Called when a tab opens, on open and after a
	/// summon - never per frame.
	///
	/// <para>Fase 3.10: the name/tier/status block follows the companion on every tick (cheap reads), but the profile
	/// controls are only loaded when the tab really changes (<see cref="_compFormFor"/>): they are what the player is
	/// typing in, and a twice-a-second refill would rewrite the text box under their hands.</para>
	/// </summary>
	private void RefreshCompanionPage()
	{
		if (_compName == null)
		{
			return;
		}
		CompanionSetup setup = SelectedCompanion();
		if (setup == null)
		{
			_compName.text = "?";
			_compTier.text = "?";
			_compStatus.text = "";
			SetRecallEnabled(false);
			SetCompProfileEnabled(false);
			_compFormFor = null;
			CacheCompanionStatus(null);
			return;
		}
		Character character = setup.GetComponent<Character>();
		bool alive = character != null && character.GetHealth() > 0f;
		_compName.text = NameOf(setup);
		_compTier.text = TierName(setup);
		_compStatus.text = ModLocalization.Loc(alive ? "hc_ai_comp_alive" : "hc_ai_comp_dead");
		_compStatus.color = alive ? GoldColor : DimColor;
		SetRecallEnabled(alive);
		SetCompProfileEnabled(true);
		if (setup != _compFormFor)
		{
			_compFormFor = setup;
			RefreshCompanionProfile(setup);
		}
	}

	/// <summary>
	/// Puts the stored profile of <paramref name="setup"/> on the controls of the companion page. A companion with
	/// nothing of its own shows the first entry of both lists ("Follow the [IA] default") and an empty text box -
	/// which is exactly what the footer says with "(padrão)" - and the values themselves are never copied into the
	/// companion, so changing the global setting later still reaches it.
	/// </summary>
	private void RefreshCompanionProfile(CompanionSetup setup)
	{
		if (setup == null)
		{
			return;
		}
		if (CompanionProfile.HasPersona(setup))
		{
			string preset = CompanionProfile.StoredPreset(setup);
			string free = CompanionProfile.StoredFreeText(setup);
			int index = IndexOfPreset(_compPersonalityIds, preset);
			if (index < 0)
			{
				// an unknown preset (a .cfg id from a newer version?) is shown as the free-text entry, which is the
				// one that carries whatever the player wrote
				index = free.Length > 0 ? IndexOfPreset(_compPersonalityIds, CompanionPersona.CustomId) : 1;
			}
			SetDropdown(_compPersonality, Mathf.Max(0, index));
			if (_compFreeText != null && !_compFreeText.isFocused)
			{
				_compFreeText.SetTextWithoutNotify(free);
			}
		}
		else
		{
			SetDropdown(_compPersonality, 0);
			if (_compFreeText != null && !_compFreeText.isFocused)
			{
				_compFreeText.SetTextWithoutNotify("");
			}
		}

		int voice = CompanionProfile.StoredVoice(setup);
		int voiceIndex = voice > 0 ? IndexOf(_compVoiceIds, voice) : 0;
		if (voiceIndex < 0)
		{
			if (_compVoice != null)
			{
				_compVoice.Index = -1;
			}
			SetCaption(_compVoice, ModLocalization.LocFmt("hc_ai_voice_other", voice), DimColor);
			HighlightItems(_compVoice, -1);
		}
		else
		{
			SetDropdown(_compVoice, voiceIndex);
		}
		// Fase 3.12: the footer line of this companion is computed here, once per tab load (see CacheCompanionStatus)
		CacheCompanionStatus(setup);
	}

	// ------------------------------------------------------------------ Fase 3.10: the handlers of the profile

	/// <summary>A personality preset (or "follow the [IA] default", or the free text) was picked for the companion
	/// whose tab is open.</summary>
	private void OnCompPersonalityPicked(int index)
	{
		CompanionSetup setup = SelectedCompanion();
		if (setup == null)
		{
			return;
		}
		string id = IdAt(_compPersonalityIds, index);
		string text = _compFreeText != null && _compFreeText.text != null ? _compFreeText.text.Trim() : "";
		if (!CompanionProfile.SetPersona(setup, id, text))
		{
			CompanionChatAI.Announce(ModLocalization.Loc("hc_ai_profile_save_failed"));
			RefreshCompanionProfile(setup);
			CacheCompanionStatus(setup);
			return;
		}
		if (string.Equals(id, CompanionProfile.InheritId, StringComparison.Ordinal))
		{
			// picking "follow the default" is "forget what this companion had": the text box goes with it, or it
			// would keep showing a personality that is no longer stored
			if (_compFreeText != null)
			{
				_compFreeText.SetTextWithoutNotify("");
			}
			CompanionsPlugin.Log?.LogInfo($"[IA] Painel: \"{NameOf(setup)}\" volta a usar a personalidade padrao de [IA] ({CompanionPersona.Describe()})");
		}
		else
		{
			CompanionsPlugin.Log?.LogInfo($"[IA] Painel: personalidade de \"{NameOf(setup)}\" = \"{id}\" - em uso agora: {CompanionProfile.DescribePersona(setup)}");
			if (text.Length > 0)
			{
				// the free text wins over the preset (CompanionPersona.Resolve): say it, so the pick is not silently ignored
				CompanionChatAI.Announce(ModLocalization.LocFmt("hc_ai_panel_preset_shadowed", CompanionProfile.DescribePersona(setup)));
			}
		}
		CacheCompanionStatus(setup);
		RefreshStatus();
	}

	/// <summary>A voice (or "follow the [IA] default") was picked for the companion whose tab is open.</summary>
	private void OnCompVoicePicked(int index)
	{
		CompanionSetup setup = SelectedCompanion();
		if (setup == null || index < 0 || index >= _compVoiceIds.Length)
		{
			return;
		}
		int voice = _compVoiceIds[index];
		if (!CompanionProfile.SetVoice(setup, voice))
		{
			CompanionChatAI.Announce(ModLocalization.Loc("hc_ai_profile_save_failed"));
			RefreshCompanionProfile(setup);
			CacheCompanionStatus(setup);
			return;
		}
		CompanionsPlugin.Log?.LogInfo(voice > 0
			? $"[IA] Painel: voz de \"{NameOf(setup)}\" = {voice} ({_compVoice.LabelAt(index)}) - vale na proxima fala dele. O cache guarda por voz."
			: $"[IA] Painel: \"{NameOf(setup)}\" volta a usar a voz padrao de [IA] ({CompanionTts.Voice}).");
		CacheCompanionStatus(setup);
		RefreshStatus();
	}

	/// <summary>
	/// The "Aplicar" of the companion page: writes the free text (and the preset the dropdown shows) into the
	/// companion that is open. Typing a text while the dropdown says "follow the [IA] default" is not a contradiction:
	/// the text itself becomes the companion's own personality, and the preset is then "the default one".
	/// </summary>
	private void ApplyCompPersonality()
	{
		CompanionSetup setup = SelectedCompanion();
		if (setup == null || _compFreeText == null)
		{
			return;
		}
		string text = _compFreeText.text != null ? _compFreeText.text.Trim() : "";
		if (text.Length > CompanionPersona.MaxFreeTextLength)
		{
			text = text.Substring(0, CompanionPersona.MaxFreeTextLength);
			_compFreeText.SetTextWithoutNotify(text);
		}
		string id = IdAt(_compPersonalityIds, _compPersonality != null ? _compPersonality.Index : 0);
		bool inherit = string.Equals(id, CompanionProfile.InheritId, StringComparison.Ordinal);
		// Fase 3.12 (auditoria): a failed write is not a success - the same check the two dropdown handlers do. The
		// ZDO can be held by another game right then (multiplayer), and saying "applied" would leave the player with a
		// personality that was never stored.
		bool saved = (inherit && text.Length == 0)
			// nothing chosen at all: back to the [IA] values (the same as picking the first entry)
			? CompanionProfile.ClearPersona(setup)
			: CompanionProfile.SetPersona(setup, inherit ? "" : id, text);
		if (!saved)
		{
			CompanionChatAI.Announce(ModLocalization.Loc("hc_ai_profile_save_failed"));
			RefreshCompanionProfile(setup);
			CacheCompanionStatus(setup);
			return;
		}
		string effective = CompanionProfile.DescribePersona(setup);
		CompanionsPlugin.Log?.LogInfo($"[IA] Painel: personalidade de \"{NameOf(setup)}\" aplicada ({(text.Length > 0 ? text.Length + " caracteres de texto livre" : "sem texto livre")}) "
			+ $"- em uso agora: {effective}");
		CompanionChatAI.Announce(ModLocalization.LocFmt("hc_ai_panel_apply_ok", effective));
		// what is stored may not be what the controls showed (a text typed while the picker said "follow the default"
		// is stored as "the default preset + the text"), so the page reloads from the ZDO
		_compFormFor = null;
		RefreshCompanionPage();
		RefreshStatus();
	}

	/// <summary>The "Ouvir" of a companion's tab: the sample in <b>that companion's</b> voice (the global voice when it
	/// has none of its own), played from the companion when it is close enough to be heard - otherwise at the player,
	/// so the preview is never silent just because the companion is far away.</summary>
	private void PreviewCompVoice()
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
		CompanionSetup setup = SelectedCompanion();
		Player player = Player.m_localPlayer;
		if (player == null)
		{
			CompanionChatAI.Announce(ModLocalization.Loc("hc_ai_panel_no_companion"));
			return;
		}
		int voice = setup != null ? CompanionProfile.GetVoice(setup) : CompanionTts.Voice;
		string name = setup != null ? NameOf(setup) : "?";
		float cull = ModConfig.SpeechCullDistance != null ? ModConfig.SpeechCullDistance.Value : 20f;
		GameObject talker = player.gameObject;
		string where = "em voce (o companheiro esta fora do alcance da voz)";
		if (setup != null && Vector3.Distance(setup.transform.position, player.transform.position) <= cull)
		{
			talker = setup.gameObject;
			where = "no companheiro";
		}
		string text = ModLocalization.Loc("hc_ai_panel_tts_sample");
		CompanionsPlugin.Log?.LogInfo($"[IA] Painel: Ouvir ({name}) -> voz {voice}, volume {CompanionTts.Volume:0.##}, velocidade {CompanionTts.Speed:0.##}, "
			+ $"falando {where} | \"{text}\"");
		CompanionTts.Speak(talker, text, true, voice);
	}

	private void SetRecallEnabled(bool enabled)
	{
		if (_recall == null)
		{
			return;
		}
		_recall.Button.interactable = enabled;
		if (_recall.Image != null)
		{
			_recall.Image.color = enabled ? ButtonColor : OffColor;
		}
		if (_recall.Label != null)
		{
			_recall.Label.color = enabled ? Color.white : DimColor;
		}
	}

	/// <summary>Fase 3.10: the profile controls only make sense with a companion on the tab - the page is hidden
	/// without one, and this keeps the buttons from being clickable in the frame where it disappears.</summary>
	private void SetCompProfileEnabled(bool enabled)
	{
		if (_compApply != null)
		{
			_compApply.Button.interactable = enabled;
			if (_compApply.Image != null)
			{
				_compApply.Image.color = enabled ? ButtonColor : OffColor;
			}
		}
		if (_compPersonality != null && _compPersonality.ListRoot != null && !enabled && _openList == _compPersonality)
		{
			CloseLists();
		}
		if (_compVoice != null && _compVoice.ListRoot != null && !enabled && _openList == _compVoice)
		{
			CloseLists();
		}
	}

	/// <summary>
	/// The tier name of a companion that already exists: the ZDO keeps the hash of the prefab it was spawned from,
	/// and the prefabs are one per tier (<c>HC_Companion</c>, <c>HC_DvergerWarrior</c>...), so the tab can say
	/// "Guerreiro" or "Mago de Fogo" instead of a generic name.
	/// </summary>
	private static string TierName(CompanionSetup setup)
	{
		ZDO zdo = setup != null ? setup.GetComponent<ZNetView>()?.GetZDO() : null;
		if (zdo != null)
		{
			int prefab = zdo.GetPrefab();
			for (int i = 0; i < TierHashes.Length; i++)
			{
				if (TierHashes[i] == prefab)
				{
					return CompanionTierData.LocalName(Tiers[i]);
				}
			}
		}
		return ModLocalization.Loc("hc_ai_comp_tier_unknown");
	}

	/// <summary>
	/// The "Chamar" button: the recall the command key runs, for this one companion
	/// (<see cref="CompanionInteract.RecallOne"/> takes control, drops what it was doing, sets Follow and asks
	/// <see cref="ReturnLogic"/> to bring it in - walking when it is close, through the same teleport recall when
	/// it is beyond the safe chase range).
	/// </summary>
	private void RecallSelected()
	{
		CompanionSetup setup = SelectedCompanion();
		Player player = Player.m_localPlayer;
		if (setup == null || player == null)
		{
			CompanionChatAI.Announce(ModLocalization.Loc("hc_ai_comp_gone"));
			RefreshCompanionPage();
			return;
		}
		string name = NameOf(setup);
		if (!CompanionInteract.RecallOne(setup, player))
		{
			CompanionChatAI.Announce(ModLocalization.LocFmt("hc_ai_comp_recall_failed", name));
		}
		else
		{
			CompanionChatAI.Announce(ModLocalization.LocFmt("hc_ai_comp_recall_ok", name));
		}
		RefreshCompanionPage();
	}

	/// <summary>
	/// The three dropdowns: the field button goes into its page (where the row is), the list of options goes into the
	/// overlay. All of them are built after the overlay exists, and <c>Build</c> puts the overlay back as the last
	/// child of the panel right after this - the fields are the reason that line has to exist.
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
		string[] voiceLabels = CompanionProfile.VoiceLabels(withInherit: false);
		_voice = CreateDropdown("Voice", FieldX, RowVoice, 290f, RowH, voiceLabels, 0, OnVoicePicked);

		// Fase 3.9: the tier of the summoner. Its list is six items tall (180 px) and the row is near the bottom of
		// the panel, so it opens upwards - otherwise it would hang off the panel's edge.
		string[] tierLabels = new string[Tiers.Length];
		for (int i = 0; i < Tiers.Length; i++)
		{
			tierLabels[i] = CompanionTierData.LocalName(Tiers[i]);
		}
		_tier = CreateDropdown("Tier", FieldX, RowSpawnTier, TierFieldW, RowH, tierLabels, 0, null, openUp: true);
		// the field is built empty (the other two are filled from the .cfg by RefreshFromConfig): this one is not a
		// setting, so it starts on the first tier - the plain Companion
		SetDropdown(_tier, 0);
	}

	// ------------------------------------------------------------------ config <-> controls

	private void RefreshFromConfig()
	{
		SetDropdown(_personality, Mathf.Max(0, IndexOf(_personalityIds, Trimmed(ModConfig.AiPersonalityPreset))));

		// Fase 3.12: the global voice as the TTS really uses it ([IA] TtsVoz 0/negativo = a padrão), so the panel never
		// shows "voice 0 (another language)" while the companion speaks 42
		int voice = CompanionTts.Voice;
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

	/// <summary>The preset ids of the AI tab: default, the presets, then "custom" (free text). Fase 3.10: the list
	/// itself lives in <see cref="CompanionProfile"/>, which builds it with and without the "follow the default"
	/// entry - the companion's tab needs that first entry, this one must not have it.</summary>
	private static string[] PersonalityIds()
	{
		return CompanionProfile.PresetIds(withInherit: false);
	}

	private static string[] PersonalityLabels()
	{
		return CompanionProfile.PresetLabels(withInherit: false);
	}

	/// <summary>The index of a preset id in a list, where "" is a value of its own ("the default personality") and not
	/// "nothing" - the reason <see cref="IndexOf(string[], string)"/> (which answers 0 for an empty value) cannot be
	/// used on the companion's list, where 0 is the "follow the default" entry.</summary>
	private static int IndexOfPreset(string[] ids, string wanted)
	{
		if (ids == null)
		{
			return -1;
		}
		string value = wanted ?? "";
		for (int i = 0; i < ids.Length; i++)
		{
			if (string.Equals(ids[i], value, StringComparison.OrdinalIgnoreCase))
			{
				return i;
			}
		}
		return -1;
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

	private static string Trimmed(string value)
	{
		return value == null ? "" : value.Trim();
	}

	/// <summary>
	/// Fase 3.12 (auditoria): the footer line of the open companion, from the cache filled when its tab loaded (and
	/// after every change made there). Only the AI tab (no companion) reads the values live, which costs nothing.
	/// </summary>
	private string PersonalityStatusLabel(CompanionSetup setup)
	{
		if (setup == null)
		{
			return EffectivePersonalityLabel(null);
		}
		return _compStatusPersonality ?? EffectivePersonalityLabel(setup);
	}

	/// <summary>The same for the voice line of the footer (see <see cref="PersonalityStatusLabel"/>).</summary>
	private string VoiceStatusLabel(CompanionSetup setup)
	{
		if (setup == null)
		{
			return CurrentVoiceLabel(null);
		}
		return _compStatusVoice ?? CurrentVoiceLabel(setup);
	}

	/// <summary>Fills the footer cache of <paramref name="setup"/>: when its tab loads and after a change made on that
	/// tab - never per frame.</summary>
	private void CacheCompanionStatus(CompanionSetup setup)
	{
		if (setup == null)
		{
			_compStatusPersonality = null;
			_compStatusVoice = null;
			return;
		}
		_compStatusPersonality = EffectivePersonalityLabel(setup);
		_compStatusVoice = CurrentVoiceLabel(setup);
	}

	/// <summary>
	/// What the player would hear: the free text wins over the preset (see CompanionPersona.Resolve). With a companion
	/// on the tab (Fase 3.10) it is <b>its</b> personality, and "(padrão)" is appended when the value in use is the
	/// global one it inherits.
	/// </summary>
	private string EffectivePersonalityLabel(CompanionSetup setup)
	{
		if (setup == null)
		{
			return PersonalityLabel(Trimmed(ModConfig.AiPersonalityPreset), Trimmed(ModConfig.AiPersonality));
		}
		string label = PersonalityLabel(CompanionProfile.GetPreset(setup), CompanionProfile.GetFreeText(setup));
		return CompanionProfile.HasPersona(setup) ? label : ModLocalization.LocFmt("hc_ai_profile_inherited", label);
	}

	/// <summary>The label of a personality pair, taken from the lists the dropdowns show.</summary>
	private string PersonalityLabel(string preset, string freeText)
	{
		if (!string.IsNullOrEmpty(Trimmed(freeText)))
		{
			return ModLocalization.Loc("hc_ai_preset_custom");
		}
		int index = IndexOf(_personalityIds, Trimmed(preset));
		return index >= 0 && index < _personalityLabels.Length ? _personalityLabels[index] : _personalityLabels[0];
	}

	/// <summary>
	/// The voice in use, by name. A voice that is not one of the pt-BR ones is named as "other language", because the
	/// panel only offers the three pt-BR voices. With a companion on the tab it is its own voice, and "(padrão)" is
	/// appended when it inherits the global one.
	/// </summary>
	private string CurrentVoiceLabel(CompanionSetup setup)
	{
		int voice = setup != null
			? CompanionProfile.GetVoice(setup)
			: CompanionTts.Voice;
		string label = IndexOf(_voiceIds, voice) < 0
			? ModLocalization.LocFmt("hc_ai_voice_other", voice)
			: ModLocalization.Loc("hc_ai_voice_" + voice);
		if (setup != null && !CompanionProfile.HasVoice(setup))
		{
			return ModLocalization.LocFmt("hc_ai_profile_inherited", label);
		}
		return label;
	}

	/// <summary>The pt-BR voices of the AI tab (the global list, without the "follow the default" entry).</summary>
	private static int[] VoiceIds()
	{
		return CompanionProfile.VoiceIds(withInherit: false);
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

	/// <summary>
	/// Fase 3.9: one tab of the strip - the AI settings (<see cref="Companion"/> null) or one companion of the local
	/// player. The button is rebuilt on every open (see <see cref="RebuildTabs"/>), so the tab only carries what has
	/// to be repainted (the gold of the open tab) and which companion it points at.
	/// </summary>
	private sealed class AiTab
	{
		internal string Label;

		internal CompanionSetup Companion;

		internal GameObject Button;

		internal Image Image;

		internal TMP_Text Text;
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
		CreateText(Page, text, labelX, y + 6f, labelW, 22f, 12f, Color.white, TextAlignmentOptions.MidlineLeft);
		AiButton button = CreateButton(Page, "Toggle_" + text, buttonX, y, buttonW, RowH, "", null);
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

	/// <summary>The hand-built dropdown: the field button on the page, its caption and the (hidden) list of options on
	/// the overlay. <paramref name="openUp"/> puts the list <b>above</b> the field instead of under it, for a row that
	/// sits too close to the bottom edge of the panel for its list to fit below (the summoner's tier).</summary>
	private AiDropdown CreateDropdown(string name, float x, float y, float w, float h, string[] labels, int index, Action<int> onPick, bool openUp = false)
	{
		AiDropdown dropdown = new AiDropdown
		{
			Labels = labels,
			Index = index,
			OnPick = onPick
		};
		// the field belongs to the panel (its row), the list to the overlay: only the list has to float above the rows
		AiButton field = CreateButton(Page, name + "Field", x, y, w, h, "", delegate
		{
			ToggleList(dropdown);
		});
		dropdown.Caption = field.Label;
		dropdown.Caption.alignment = TextAlignmentOptions.MidlineLeft;

		GameObject listRoot = new GameObject(name + "List", typeof(RectTransform), typeof(Image));
		listRoot.transform.SetParent(_overlay, worldPositionStays: false);
		float listY = openUp ? y - labels.Length * ItemH : y + h;
		Place(listRoot.GetComponent<RectTransform>(), x, listY, w, labels.Length * ItemH);
		// The list background stays a raycast target on purpose (and the raycast audit allow-lists it by name): the
		// option buttons are 2 px smaller than the list, so a click on that border - or on the sliver under the last
		// option - would fall through to whatever is behind the open list. With this target the list itself swallows
		// it, which is what an open dropdown does (and the ListCatcher below only gets the clicks outside the list).
		// The list is only active while it is open, so it never eats a click otherwise.
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
		go.transform.SetParent(Page, worldPositionStays: false);
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
		go.transform.SetParent(Page, worldPositionStays: false);
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
		CreateText(Page, text, LeftX, rowY + 6f, LabelW, 22f, 13f, LabelColor, TextAlignmentOptions.MidlineLeft);
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

/// <summary>
/// Fase 3.9: the mouse wheel of the strip of tabs of <see cref="CompanionAiPanel"/>. The strip is a
/// <see cref="ScrollRect"/> that only scrolls sideways (a companion tab per companion, and the player may summon as
/// many as they like), and a ScrollRect only reads the wheel for the axis it scrolls - so the wheel would do nothing
/// over it. This helper turns the vertical wheel into horizontal scrolling; it lives on the viewport, i.e. under the
/// ScrollRect in the event bubble, so it is the one that gets the event.
/// </summary>
internal sealed class AiTabStripWheel : MonoBehaviour, IScrollHandler
{
	private ScrollRect _scroll;

	internal void Init(ScrollRect scroll)
	{
		_scroll = scroll;
	}

	public void OnScroll(PointerEventData eventData)
	{
		if (_scroll == null || _scroll.content == null || _scroll.viewport == null)
		{
			return;
		}
		// nothing to scroll when the tabs fit (and moving the content then would shift it off the strip)
		if (_scroll.content.rect.width <= _scroll.viewport.rect.width)
		{
			return;
		}
		_scroll.horizontalNormalizedPosition = Mathf.Clamp01(_scroll.horizontalNormalizedPosition - eventData.scrollDelta.y * 0.1f);
	}
}
