using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Companions;

/// <summary>
/// 04/11/2026 - <b>the build panel</b> (<c>[Controls] BuildPanelKey</c>, default <b>B</b>): the window of the build
/// site the companions are working on. The four blocks are the player's list, in his order:
/// <list type="number">
/// <item><b>the site</b> - what is being built, how many pieces of the plan are up, how many were given up, how many
/// emergency posts went up, the phase, how long it has been running and who the leader is, with
/// <b>Pause</b>/<b>Resume</b>/<b>Cancel</b> wired straight to <see cref="MHR_Build.PauseSite"/>,
/// <see cref="MHR_Build.ResumeSite"/> and <see cref="MHR_Build.CancelSite"/>;</item>
/// <item><b>the companions</b> - who is on the site and who could join it. Joining is
/// <see cref="MHR_Build.JoinSite"/> (the very path the group start uses for a follower) and leaving is
/// <see cref="MHR_Build.Cancel"/> for that one worker, i.e. "para, Fulano" of the chat. Who may join is
/// <see cref="CompanionOrders.CanWorkSite"/>, so the panel and a nameless build order can never disagree;</item>
/// <item><b>the materials</b> - <see cref="MHR_Build.SiteMaterials"/>: what the whole plan needs, what the bags of the
/// site plus the chests around the anchor have, and what is missing, with the game's own item names;</item>
/// <item><b>the blueprints</b> - the folder of <see cref="BuildBlueprints"/> with a filter, the numbers of the selected
/// file (<see cref="BuildBlueprintReport"/> - the same reader the order and the DevBridge use), <b>Open folder</b>
/// (there is no file dialog here) and <b>Delete</b>, which asks for a second click.</item>
/// </list>
///
/// <para><b>It never steers anything by itself.</b> The panel reads a snapshot twice a second
/// (<see cref="MHR_Build.CurrentSite"/>) and only acts on a click. It also <b>never touches the plan</b>:
/// <c>SiteInfo.Steps</c> is the very list the site walks (index, claims and retry queue live in the order), so it is
/// read-only here - the only thing that reads it is the material count of <see cref="MHR_Build.SiteMaterials"/>.</para>
///
/// <para><b>Cost.</b> The controls are built once (first open) and only shown/hidden afterwards, so the per-frame work
/// is the key, the cursor and one float comparison. The site is refreshed every <see cref="UiInterval"/> (0.5 s), the
/// materials every <see cref="MaterialsInterval"/> (2 s - it walks every bag of the site and the chests around the
/// anchor) and the blueprint folder every <see cref="BlueprintScanInterval"/> (5 s; the folder listing itself is cached
/// by <see cref="BuildBlueprints.Files"/>). A real language change rebuilds the panel, exactly like the AI panel (every
/// label is written at build time).</para>
///
/// <para><b>Input.</b> Like the mod's other windows it blocks the player while it is up (the <c>Hud.InRadial</c>,
/// <c>Player.TakeInput</c> and <c>PlayerController.TakeInput</c> guards plus <see cref="VoiceInput"/>), and it refuses
/// to <b>open</b> over a window that owns the mouse and the keyboard (the inventory, the map, the menu, the text input,
/// the console, the store, the chat with focus, the mod's F8/AI/starter/home/appearance windows and the wheel) - while
/// the F8 and the AI panels refuse to open over this one. The wheel closes itself with <b>Esc</b> (and the pad's B);
/// the keyboard <b>B</b> belongs to this panel alone ("one key, one owner"), so <see cref="CompanionRadialMenu.LastKeyCloseFrame"/>
/// is only a guard for the pad path.</para>
/// </summary>
internal sealed class CompanionBuildPanel : MonoBehaviour
{
	// ------------------------------------------------------------------ layout

	private const float PanelW = 920f;

	private const float PanelH = 700f;

	/// <summary>Left column (the site above, the companions below).</summary>
	private const float LeftX = 8f;

	private const float LeftW = 452f;

	/// <summary>Right column (the materials above, the blueprints below).</summary>
	private const float RightX = 472f;

	private const float RightW = 440f;

	// Block 1 - the site (measured down from the top of the panel).
	private const float SiteTitleY = 44f;

	private const float SiteLabelY = 66f;

	private const float SiteLabelH = 40f;

	private const float SiteProgressY = 110f;

	private const float SiteStateY = 130f;

	private const float SiteLeaderY = 150f;

	private const float SiteButtonsY = 172f;

	private const float SiteButtonH = 30f;

	private const float SiteStatusY = 208f;

	private const float SiteStatusH = 40f;

	// Block 2 - the companions.
	private const float WorkersTitleY = 258f;

	private const float WorkersTop = 280f;

	private const float WorkersH = 368f;

	private const float WorkerRowH = 30f;

	/// <summary>Width of the [Join]/[Leave] button of a companion row (the rest of the row is the name and the phase).</summary>
	private const float WorkerButtonW = 92f;

	/// <summary>Where the phase/reason column of a companion row starts.</summary>
	private const float WorkerSubX = 208f;

	private const float WorkerSubW = 140f;

	// Block 3 - the materials.
	private const float MaterialsTitleY = 44f;

	private const float MaterialsTop = 66f;

	private const float MaterialsH = 250f;

	private const float MaterialRowH = 26f;

	// Block 4 - the blueprints.
	private const float BpTitleY = 326f;

	private const float BpFilterY = 348f;

	private const float BpFilterH = 24f;

	private const float BpFilterW = 300f;

	private const float BpListTop = 378f;

	private const float BpListH = 176f;

	private const float BpRowH = 28f;

	private const float BpDetailY = 558f;

	private const float BpDetailH = 40f;

	private const float BpButtonsY = 604f;

	private const float BpFolderY = 640f;

	private const float BpHintY = 664f;

	/// <summary>Sorting order of the panel's canvas, from <see cref="ModUiLayers"/> (the one place where the band of the
	/// mod's UI is documented).</summary>
	private const int PanelSortingOrder = ModUiLayers.BuildPanel;

	/// <summary>How often the site snapshot, the companion list and the two-click confirmations are refreshed.</summary>
	private const float UiInterval = 0.5f;

	/// <summary>How often the material need/stock is counted (it reads the bags and the chests - the most expensive
	/// read of the panel, which is why it is NOT on the half-second tick).</summary>
	private const float MaterialsInterval = 2f;

	/// <summary>How often the blueprint folder is listed (a file dropped in the folder shows up within this window;
	/// the listing of <see cref="BuildBlueprints.Files"/> is cached for 10 s on its own).</summary>
	private const float BlueprintScanInterval = 5f;

	/// <summary>How long a status line of the panel stays up.</summary>
	private const float StatusSeconds = 6f;

	/// <summary>How long the "click again" of the delete lasts.</summary>
	private const float ConfirmSeconds = 5f;

	/// <summary>Rows of the blueprint list drawn at once (the library may have hundreds of files - the filter is what
	/// narrows it down; each row is a GameObject + a TMP text, so they are never all built).</summary>
	private const int MaxBlueprintRows = 40;

	// ------------------------------------------------------------------ colours (the AI panel's palette)

	private static readonly Color LabelColor = new Color(1f, 0.9f, 0.5f, 1f);

	private static readonly Color GoldColor = new Color(0.83f, 0.64f, 0.31f, 1f);

	private static readonly Color DimColor = new Color(0.62f, 0.62f, 0.62f, 1f);

	private static readonly Color RowColor = new Color(0.08f, 0.08f, 0.1f, 0.55f);

	private static readonly Color ButtonColor = new Color(0.28f, 0.26f, 0.22f, 0.95f);

	private static readonly Color OffColor = new Color(0.3f, 0.3f, 0.33f, 0.95f);

	private static readonly Color SelectedColor = new Color(0.42f, 0.34f, 0.16f, 0.95f);

	private static readonly Color DangerColor = new Color(0.45f, 0.16f, 0.12f, 0.95f);

	private static readonly Color MissingColor = new Color(1f, 0.72f, 0.4f, 1f);

	private static readonly Color OkColor = new Color(0.62f, 0.83f, 0.55f, 1f);

	// ------------------------------------------------------------------ state

	private bool _visible;

	private GameObject _backdrop;

	private GameObject _root;

	private TMP_FontAsset _font;

	private TMP_Text _hint;

	// ---- block 1: the site ----

	private TMP_Text _siteLabel;

	private TMP_Text _siteProgress;

	private TMP_Text _siteState;

	private TMP_Text _siteLeader;

	private TMP_Text _siteStatus;

	private Button _pause;

	private TMP_Text _pauseLabel;

	private Button _resume;

	private TMP_Text _resumeLabel;

	private Button _cancel;

	private TMP_Text _cancelLabel;

	// ---- block 2: the companions ----

	private TMP_Text _workersTitle;

	private RectTransform _workersContent;

	private TMP_Text _workersEmpty;

	private readonly List<ListRow> _workerRows = new List<ListRow>();

	/// <summary>The ids on the site of the last refresh (a reusable set: the list is rebuilt twice a second).</summary>
	private readonly HashSet<int> _onSite = new HashSet<int>();

	// ---- block 3: the materials ----

	private RectTransform _materialsContent;

	private TMP_Text _materialsEmpty;

	private readonly List<ListRow> _materialRows = new List<ListRow>();

	/// <summary>The order the material rows are drawn in (missing first, then the biggest need).</summary>
	private readonly List<string> _materialOrder = new List<string>();

	private Comparison<string> _materialCompare;

	// ---- block 4: the blueprints ----

	private TMP_InputField _bpFilter;

	private TMP_Text _bpCount;

	private RectTransform _bpContent;

	private TMP_Text _bpNone;

	private TMP_Text _bpDetail;

	private TMP_Text _bpFolder;

	private Button _bpDelete;

	private TMP_Text _bpDeleteLabel;

	private readonly List<ListRow> _bpRows = new List<ListRow>();

	/// <summary>Every file of the folder (the listing of the last scan).</summary>
	private readonly List<string> _bpFiles = new List<string>();

	/// <summary>The files that pass the filter (what the rows show, capped by <see cref="MaxBlueprintRows"/>).</summary>
	private readonly List<string> _bpShown = new List<string>();

	private string _bpSelected;

	/// <summary>The numbers of the selected file, read on the click (never per frame: a 985-line file is a parse).</summary>
	private BuildBlueprintReport _bpReport;

	private string _bpError;

	// ---- live data ----

	/// <summary>The site of the local player, as the last half-second refresh saw it (null when there is none).</summary>
	private MHR_Build.SiteInfo _site;

	private Dictionary<string, int> _need;

	private Dictionary<string, int> _have;

	private Dictionary<string, int> _missing;

	/// <summary>The status line of the site block (a refusal, a delete, ...) and when it expires.</summary>
	private string _status;

	private float _statusUntil;

	/// <summary>The file the first click of the delete named, and when the second click stops counting.</summary>
	private string _deleteFile;

	private float _deleteUntil;

	private float _nextUi;

	private float _nextMaterials;

	private float _nextScan;

	/// <summary>The <see cref="ModLocalization.LanguageVersion"/> this panel was built in (a real language change
	/// rebuilds it, because every label is written at build time).</summary>
	private int _builtLanguageVersion;

	public static CompanionBuildPanel Instance { get; private set; }

	public bool IsVisible => _visible;

	/// <summary>Is the panel on screen right now? Read by the other panels (they refuse to open over it) and by the
	/// input patches.</summary>
	internal static bool IsOpen => Instance != null && Instance._visible;

	/// <summary>Creates the (hidden) panel once. Called from the plugin's Awake, next to the other panels.</summary>
	internal static void Create()
	{
		if (Instance != null)
		{
			return;
		}
		GameObject holder = new GameObject("HC_BuildPanel");
		UnityEngine.Object.DontDestroyOnLoad(holder);
		Instance = holder.AddComponent<CompanionBuildPanel>();
		string key = ModConfig.BuildPanelKey != null ? ModConfig.BuildPanelKey.Value.ToString() : "?";
		CompanionsPlugin.Log?.LogInfo("[Build] Painel da obra pronto: abre/fecha com " + key + " ([Controls] BuildPanelKey) ou Esc. "
			+ "Mostra a obra em andamento (pecas, puladas, postes, fase, tempo e lider, com pausar/continuar/cancelar), os "
			+ "companheiros do grupo (entrar/sair), os materiais (necessario x disponivel x faltando) e a biblioteca de "
			+ ".blueprint (filtrar, abrir a pasta e excluir).");
	}

	private void Awake()
	{
		if (Instance != null && Instance != this)
		{
			UnityEngine.Object.Destroy(base.gameObject);
			return;
		}
		Instance = this;
		_materialCompare = CompareMaterials;
	}

	private void OnDestroy()
	{
		if (Instance == this)
		{
			Instance = null;
		}
	}

	// ------------------------------------------------------------------ open / close

	internal void Show()
	{
		// a real change of the game language rebuilds the panel (every label is written at build time)
		if (_root == null)
		{
			Build();
		}
		else if (_builtLanguageVersion != ModLocalization.LanguageVersion)
		{
			DestroyBuiltControls();
			Build();
		}
		_backdrop.SetActive(true);
		_root.SetActive(true);
		_visible = true;
		_status = null;
		_statusUntil = 0f;
		_deleteFile = null;
		_deleteUntil = 0f;
		Cursor.lockState = CursorLockMode.None;
		Cursor.visible = true;
		RefreshAll();
		Log("painel da obra aberto");
	}

	internal void Hide()
	{
		if (!_visible)
		{
			return;
		}
		_visible = false;
		_status = null;
		_statusUntil = 0f;
		_deleteFile = null;
		_deleteUntil = 0f;
		if (_bpFilter != null && _bpFilter.isFocused)
		{
			_bpFilter.DeactivateInputField();
		}
		if (_root != null)
		{
			_root.SetActive(false);
		}
		if (_backdrop != null)
		{
			_backdrop.SetActive(false);
		}
		Log("painel da obra fechado");
	}

	private void Update()
	{
		if (!_visible)
		{
			if (CanOpen() && PanelKeyDown())
			{
				Show();
			}
			return;
		}
		// the player left the world or died: the panel has nothing to show and would keep the cursor free
		Player player = Player.m_localPlayer;
		if (player == null || player.IsDead())
		{
			Hide();
			return;
		}
		if (Input.GetKeyDown(KeyCode.Escape) || ZInput.GetButtonDown("JoyBack") || ZInput.GetButtonDown("JoyButtonB"))
		{
			// Esc first leaves the filter box, then closes the panel - never both at once
			if (_bpFilter != null && _bpFilter.isFocused)
			{
				_bpFilter.DeactivateInputField();
				return;
			}
			Hide();
			return;
		}
		if (PanelKeyDown())
		{
			Hide();
			return;
		}
		Cursor.lockState = CursorLockMode.None;
		Cursor.visible = true;
		float now = Time.unscaledTime;
		if (now >= _nextUi)
		{
			_nextUi = now + UiInterval;
			RefreshSite();
			ExpireDeleteConfirm();
		}
		if (now >= _nextMaterials)
		{
			_nextMaterials = now + MaterialsInterval;
			RefreshMaterials();
		}
		if (now >= _nextScan)
		{
			_nextScan = now + BlueprintScanInterval;
			ScanBlueprints();
		}
	}

	/// <summary>The key of the panel, with the one guard a text box needs: typing a "b" in the filter must not close
	/// the window.</summary>
	private bool PanelKeyDown()
	{
		ConfigEntry<KeyCode> entry = ModConfig.BuildPanelKey;
		if (entry == null || entry.Value == KeyCode.None || !Input.GetKeyDown(entry.Value))
		{
			return false;
		}
		if (_bpFilter != null && _bpFilter.isFocused)
		{
			return false;
		}
		return true;
	}

	/// <summary>
	/// May the panel open right now? The same windows the AI panel refuses, plus the wheel (it is above the panels in
	/// the band of <see cref="ModUiLayers"/> and closes itself with <b>Esc</b>/pad B) and the frame guard below: on the
	/// pad a single press could close the wheel and open this panel at once, so <see cref="CompanionRadialMenu.LastKeyCloseFrame"/>
	/// keeps them apart.
	/// </summary>
	private bool CanOpen()
	{
		Player player = Player.m_localPlayer;
		if (player == null || player.IsDead())
		{
			return false;
		}
		if (Time.frameCount == CompanionRadialMenu.LastKeyCloseFrame)
		{
			return false;
		}
		if (InventoryGui.IsVisible() || Menu.IsVisible() || Minimap.IsOpen())
		{
			return false;
		}
		if (TextInput.IsVisible() || Console.IsVisible() || StoreGui.IsVisible())
		{
			return false;
		}
		if (Chat.instance != null && Chat.instance.HasFocus())
		{
			return false;
		}
		if (ModTextInputFocus.AnyFocused)
		{
			return false;
		}
		if (CompanionRadialMenu.Instance != null && CompanionRadialMenu.Instance.IsVisible)
		{
			return false;
		}
		if (ConfigPanel.Instance != null && ConfigPanel.Instance.IsVisible)
		{
			return false;
		}
		if (CompanionAiPanel.Instance != null && CompanionAiPanel.Instance.IsVisible)
		{
			return false;
		}
		if (StarterCompanionPanel.Instance != null && StarterCompanionPanel.Instance.IsVisible)
		{
			return false;
		}
		if (HomeZonePanel.Instance != null && HomeZonePanel.Instance.IsVisible)
		{
			return false;
		}
		if (CompanionAppearancePanel.IsOpen)
		{
			return false;
		}
		return true;
	}

	// ------------------------------------------------------------------ build

	private void Build()
	{
		// Build can run more than once (a real language change rebuilds the panel) and Unity allows one Canvas per
		// GameObject, so the Canvas/Scaler/Raycaster are reused and only the children are created anew.
		Canvas canvas = base.gameObject.GetComponent<Canvas>();
		if (canvas == null)
		{
			canvas = base.gameObject.AddComponent<Canvas>();
		}
		if (base.gameObject.GetComponent<GraphicRaycaster>() == null)
		{
			base.gameObject.AddComponent<GraphicRaycaster>();
		}
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		canvas.sortingOrder = PanelSortingOrder;
		CanvasScaler scaler = base.gameObject.GetComponent<CanvasScaler>();
		if (scaler == null)
		{
			scaler = base.gameObject.AddComponent<CanvasScaler>();
		}
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(1920f, 1080f);
		_font = ResolveFont();

		_backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
		_backdrop.transform.SetParent(base.transform, worldPositionStays: false);
		RectTransform backdropRT = _backdrop.GetComponent<RectTransform>();
		backdropRT.anchorMin = Vector2.zero;
		backdropRT.anchorMax = Vector2.one;
		backdropRT.offsetMin = Vector2.zero;
		backdropRT.offsetMax = Vector2.zero;
		// the modal click catcher: created BEFORE the root, so its render (and raycast) depth is below every control
		_backdrop.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f);

		_root = new GameObject("BuildRoot", typeof(RectTransform), typeof(Image));
		_root.transform.SetParent(base.transform, worldPositionStays: false);
		RectTransform rootRT = _root.GetComponent<RectTransform>();
		rootRT.anchorMin = new Vector2(0.5f, 0.5f);
		rootRT.anchorMax = new Vector2(0.5f, 0.5f);
		rootRT.pivot = new Vector2(0.5f, 0.5f);
		rootRT.sizeDelta = new Vector2(PanelW, PanelH);
		Image rootImage = _root.GetComponent<Image>();
		// the game's own panel background, like the starter panel and the inventory window
		GameLook.ApplyPanel(rootImage, new Color(0.06f, 0.05f, 0.04f, 0.96f));
		// decorative: it is the parent of every control, so it can never be in front of one
		rootImage.raycastTarget = false;

		TMP_Text title = CreateText(_root.transform, ModLocalization.Loc("hc_ui_build_title"), 8f, 4f, PanelW - 56f, 30f, 18f, LabelColor, TextAlignmentOptions.Center);
		title.fontStyle = FontStyles.Bold;
		CreateButton(_root.transform, "Close", PanelW - 40f, 2f, 36f, 34f, "X", DangerColor, Hide, out _);

		BuildSiteBlock();
		BuildWorkersBlock();
		BuildMaterialsBlock();
		BuildBlueprintsBlock();

		_hint = CreateText(_root.transform, HintText(), 8f, BpHintY, PanelW - 16f, 22f, 12f, DimColor, TextAlignmentOptions.Center);

		_backdrop.SetActive(false);
		_root.SetActive(false);
		_builtLanguageVersion = ModLocalization.LanguageVersion;
	}

	/// <summary>The footer: how the panel closes (the key is the player's own).</summary>
	private static string HintText()
	{
		string key = ModConfig.BuildPanelKey != null ? ModConfig.BuildPanelKey.Value.ToString() : "?";
		return ModLocalization.LocFmt("hc_ui_build_hint", key);
	}

	private void BuildSiteBlock()
	{
		TMP_Text title = CreateText(_root.transform, ModLocalization.Loc("hc_ui_build_site"), LeftX, SiteTitleY, LeftW, 20f, 14f, LabelColor, TextAlignmentOptions.MidlineLeft);
		title.fontStyle = FontStyles.Bold;
		_siteLabel = CreateText(_root.transform, "", LeftX, SiteLabelY, LeftW, SiteLabelH, 13f, Color.white, TextAlignmentOptions.TopLeft);
		_siteProgress = CreateText(_root.transform, "", LeftX, SiteProgressY, LeftW, 18f, 13f, Color.white, TextAlignmentOptions.MidlineLeft);
		_siteState = CreateText(_root.transform, "", LeftX, SiteStateY, LeftW, 18f, 13f, Color.white, TextAlignmentOptions.MidlineLeft);
		_siteLeader = CreateText(_root.transform, "", LeftX, SiteLeaderY, LeftW, 18f, 13f, GoldColor, TextAlignmentOptions.MidlineLeft);
		_siteStatus = CreateText(_root.transform, "", LeftX, SiteStatusY, LeftW, SiteStatusH, 12f, MissingColor, TextAlignmentOptions.TopLeft);

		_pause = CreateButton(_root.transform, "Pause", LeftX, SiteButtonsY, 110f, SiteButtonH, ModLocalization.Loc("hc_ui_build_btn_pause"), ButtonColor, OnPauseClick, out _pauseLabel);
		_resume = CreateButton(_root.transform, "Resume", LeftX + 118f, SiteButtonsY, 110f, SiteButtonH, ModLocalization.Loc("hc_ui_build_btn_resume"), ButtonColor, OnResumeClick, out _resumeLabel);
		_cancel = CreateButton(_root.transform, "Cancel", LeftX + 236f, SiteButtonsY, 130f, SiteButtonH, ModLocalization.Loc("hc_ui_build_btn_cancel"), ButtonColor, OnCancelClick, out _cancelLabel);
	}

	private void BuildWorkersBlock()
	{
		_workersTitle = CreateText(_root.transform, ModLocalization.LocFmt("hc_ui_build_workers", 0), LeftX, WorkersTitleY, LeftW, 20f, 14f, LabelColor, TextAlignmentOptions.MidlineLeft);
		_workersTitle.fontStyle = FontStyles.Bold;
		RectTransform content;
		CreateScroll(_root.transform, "WorkersScroll", LeftX, WorkersTop, LeftW, WorkersH, out content);
		_workersContent = content;
		_workersEmpty = CreateText(_root.transform, "", LeftX + 8f, WorkersTop + 6f, LeftW - 16f, WorkersH - 12f, 12f, DimColor, TextAlignmentOptions.TopLeft);
	}

	private void BuildMaterialsBlock()
	{
		TMP_Text title = CreateText(_root.transform, ModLocalization.Loc("hc_ui_build_materials"), RightX, MaterialsTitleY, RightW, 20f, 14f, LabelColor, TextAlignmentOptions.MidlineLeft);
		title.fontStyle = FontStyles.Bold;
		RectTransform content;
		CreateScroll(_root.transform, "MaterialsScroll", RightX, MaterialsTop, RightW, MaterialsH, out content);
		_materialsContent = content;
		_materialsEmpty = CreateText(_root.transform, "", RightX + 8f, MaterialsTop + 6f, RightW - 16f, MaterialsH - 12f, 12f, DimColor, TextAlignmentOptions.TopLeft);
	}

	private void BuildBlueprintsBlock()
	{
		TMP_Text title = CreateText(_root.transform, ModLocalization.Loc("hc_ui_build_blueprints"), RightX, BpTitleY, RightW, 20f, 14f, LabelColor, TextAlignmentOptions.MidlineLeft);
		title.fontStyle = FontStyles.Bold;
		_bpFilter = CreateFilterField(RightX, BpFilterY, BpFilterW, BpFilterH);
		_bpCount = CreateText(_root.transform, "", RightX + BpFilterW + 6f, BpFilterY, RightW - BpFilterW - 6f, BpFilterH, 11f, DimColor, TextAlignmentOptions.MidlineRight);
		RectTransform content;
		CreateScroll(_root.transform, "BlueprintsScroll", RightX, BpListTop, RightW, BpListH, out content);
		_bpContent = content;
		_bpNone = CreateText(_root.transform, ModLocalization.Loc("hc_ui_build_bp_none"), RightX + 8f, BpListTop + 6f, RightW - 16f, BpListH - 12f, 12f, DimColor, TextAlignmentOptions.TopLeft);
		_bpDetail = CreateText(_root.transform, "", RightX, BpDetailY, RightW, BpDetailH, 12f, Color.white, TextAlignmentOptions.TopLeft);
		CreateButton(_root.transform, "OpenFolder", RightX, BpButtonsY, 200f, SiteButtonH, ModLocalization.Loc("hc_ui_build_bp_open_folder"), ButtonColor, OpenBlueprintFolder, out _);
		_bpDelete = CreateButton(_root.transform, "DeleteBlueprint", RightX + 208f, BpButtonsY, 130f, SiteButtonH, ModLocalization.Loc("hc_ui_build_bp_delete"), DangerColor, OnDeleteClick, out _bpDeleteLabel);
		_bpFolder = CreateText(_root.transform, "", RightX, BpFolderY, RightW, 18f, 11f, DimColor, TextAlignmentOptions.MidlineLeft);
	}

	/// <summary>
	/// Drops everything <see cref="Build"/> created so the panel can be built again in another language. The Canvas
	/// components of the GameObject are reused; the font belongs to the game and is not touched.
	/// </summary>
	private void DestroyBuiltControls()
	{
		if (_root != null)
		{
			_root.SetActive(false);
			UnityEngine.Object.Destroy(_root);
			_root = null;
		}
		if (_backdrop != null)
		{
			_backdrop.SetActive(false);
			UnityEngine.Object.Destroy(_backdrop);
			_backdrop = null;
		}
		_hint = null;
		_siteLabel = null;
		_siteProgress = null;
		_siteState = null;
		_siteLeader = null;
		_siteStatus = null;
		_pause = null;
		_pauseLabel = null;
		_resume = null;
		_resumeLabel = null;
		_cancel = null;
		_cancelLabel = null;
		_workersTitle = null;
		_workersContent = null;
		_workersEmpty = null;
		_workerRows.Clear();
		_onSite.Clear();
		_materialsContent = null;
		_materialsEmpty = null;
		_materialRows.Clear();
		_materialOrder.Clear();
		_bpFilter = null;
		_bpCount = null;
		_bpContent = null;
		_bpNone = null;
		_bpDetail = null;
		_bpFolder = null;
		_bpDelete = null;
		_bpDeleteLabel = null;
		_bpRows.Clear();
		_bpFiles.Clear();
		_bpShown.Clear();
		_bpSelected = null;
		_bpReport = null;
		_bpError = null;
		_nextUi = 0f;
		_nextMaterials = 0f;
		_nextScan = 0f;
	}

	// ------------------------------------------------------------------ refresh

	private void RefreshAll()
	{
		RefreshSite();
		RefreshMaterials();
		ScanBlueprints();
		if (_bpFolder != null)
		{
			_bpFolder.text = BuildBlueprints.Folder();
		}
		if (_hint != null)
		{
			_hint.text = HintText();
		}
		float now = Time.unscaledTime;
		_nextUi = now + UiInterval;
		_nextMaterials = now + MaterialsInterval;
		_nextScan = now + BlueprintScanInterval;
	}

	/// <summary>
	/// The half-second tick of the site: one <see cref="MHR_Build.CurrentSite"/> snapshot (a plain read - it allocates
	/// a small snapshot and never steers anything), the four lines of the block, the three buttons and the list of
	/// companions.
	/// </summary>
	private void RefreshSite()
	{
		MHR_Build.SiteInfo site;
		_site = MHR_Build.CurrentSite(LocalOwner(), out site) ? site : null;
		if (_statusUntil > 0f && Time.unscaledTime >= _statusUntil)
		{
			_status = null;
			_statusUntil = 0f;
		}
		if (_siteStatus != null)
		{
			_siteStatus.text = _status ?? string.Empty;
		}
		bool has = _site != null;
		if (_siteLabel != null)
		{
			if (has)
			{
				_siteLabel.text = ModLocalization.LocFmt("hc_ui_build_site_label", _site.Label ?? "?");
				_siteLabel.color = Color.white;
			}
			else
			{
				_siteLabel.text = ModLocalization.Loc("hc_ui_build_none");
				_siteLabel.color = DimColor;
			}
		}
		if (_siteProgress != null)
		{
			_siteProgress.gameObject.SetActive(has);
			if (has)
			{
				_siteProgress.text = ModLocalization.LocFmt("hc_ui_build_site_progress", _site.Placed, _site.Plan, _site.Skipped, _site.Posts);
			}
		}
		if (_siteState != null)
		{
			_siteState.gameObject.SetActive(has);
			if (has)
			{
				_siteState.text = ModLocalization.LocFmt("hc_ui_build_site_state", PhaseLabel(_site.Phase),
					Clock(_site.StartedAt > 0f ? Time.time - _site.StartedAt : 0f));
			}
		}
		if (_siteLeader != null)
		{
			string leader = LeaderName(_site);
			_siteLeader.gameObject.SetActive(has && leader != null);
			_siteLeader.text = leader != null ? ModLocalization.LocFmt("hc_ui_build_site_leader", leader) : string.Empty;
		}
		SetButtonEnabled(_pause, _pauseLabel, has && !_site.Paused, ButtonColor);
		SetButtonEnabled(_resume, _resumeLabel, has && _site.Paused, ButtonColor);
		SetButtonEnabled(_cancel, _cancelLabel, has, ButtonColor);
		RefreshWorkers();
	}

	/// <summary>The name of the leader of the site (null when there is none or the site has no members).</summary>
	private static string LeaderName(MHR_Build.SiteInfo site)
	{
		if (site == null || site.Members == null)
		{
			return null;
		}
		for (int i = 0; i < site.Members.Count; i++)
		{
			MHR_Build.SiteWorker member = site.Members[i];
			if (member != null && member.Leader)
			{
				return string.IsNullOrEmpty(member.Name) ? "?" : member.Name;
			}
		}
		return null;
	}

	/// <summary>
	/// The companion list: the members of the site first (with [Leave] - <see cref="MHR_Build.Cancel"/> for that one,
	/// "para, Fulano"), then every companion of the player that could join (<see cref="CompanionOrders.CanWorkSite"/>,
	/// the read-only form - reading the list never steals a companion). Joining is only offered on a <b>group</b> site:
	/// a single-worker order has no group to join, and its site becomes a group only when the order itself is placed by
	/// <see cref="MHR_Build.StartBlueprintGroup"/>.
	/// </summary>
	private void RefreshWorkers()
	{
		int shown = 0;
		_onSite.Clear();
		if (_site != null && _site.Members != null)
		{
			for (int i = 0; i < _site.Members.Count; i++)
			{
				MHR_Build.SiteWorker member = _site.Members[i];
				if (member == null)
				{
					continue;
				}
				_onSite.Add(member.Id);
				CompanionSetup cs = SetupOf(member.Id);
				ListRow row = EnsureWorkerRow(shown);
				row.Root.SetActive(true);
				row.Index = i;
				row.Setup = cs;
				row.Action = cs != null ? WorkerAction.Leave : WorkerAction.None;
				string name = string.IsNullOrEmpty(member.Name) ? "?" : member.Name;
				row.Text.text = member.Leader ? ModLocalization.LocFmt("hc_ui_build_leader_tag", name) : name;
				row.Text.color = member.Leader ? GoldColor : Color.white;
				row.Sub.text = PhaseLabel(member.Phase);
				row.Sub.color = member.Paused ? MissingColor : DimColor;
				SetRowButton(row, ModLocalization.Loc("hc_ui_build_btn_leave"), ButtonColor, row.Action != WorkerAction.None);
				shown++;
			}
		}
		if (_site != null)
		{
			// who could join: the very rule of a nameless build order, read-only (the click is what takes the companion
			// over, see Join)
			IReadOnlyList<CompanionSetup> all = CompanionSetup.AllCompanions;
			for (int i = 0; i < all.Count; i++)
			{
				CompanionSetup cs = all[i];
				if (cs == null)
				{
					continue;
				}
				CompanionAI ai = cs.GetComponent<CompanionAI>();
				if (ai == null || _onSite.Contains(ai.GetInstanceID()))
				{
					continue;
				}
				ListRow row = EnsureWorkerRow(shown);
				row.Root.SetActive(true);
				row.Index = i;
				row.Setup = cs;
				string why = null;
				string reason = null;
				bool join = false;
				if (CompanionOrders.CanWorkSite(cs, _site.Anchor, false, out why))
				{
					if (!_site.IsGroup || string.IsNullOrEmpty(_site.Key))
					{
						reason = ModLocalization.Loc("hc_ui_build_why_solo");
					}
					else if (_site.Paused)
					{
						reason = ModLocalization.Loc("hc_ui_build_why_paused");
					}
					else
					{
						join = true;
					}
				}
				else
				{
					reason = WhyLabel(why);
				}
				row.Action = join ? WorkerAction.Join : WorkerAction.None;
				row.Text.text = CompanionNames.NameOf(cs);
				row.Text.color = join ? Color.white : DimColor;
				row.Sub.text = reason ?? string.Empty;
				row.Sub.color = DimColor;
				SetRowButton(row, ModLocalization.Loc("hc_ui_build_btn_join"), ButtonColor, join);
				shown++;
			}
		}
		for (int i = shown; i < _workerRows.Count; i++)
		{
			_workerRows[i].Root.SetActive(false);
		}
		if (_workersContent != null)
		{
			_workersContent.sizeDelta = new Vector2(0f, Mathf.Max(WorkersH, (float)shown * WorkerRowH));
		}
		if (_workersTitle != null)
		{
			// the count is the workers OF THE SITE (the rows below also list who could join, which is another question)
			_workersTitle.text = ModLocalization.LocFmt("hc_ui_build_workers", _site != null ? _site.Workers : 0);
		}
		if (_workersEmpty != null)
		{
			_workersEmpty.gameObject.SetActive(shown == 0);
			_workersEmpty.text = _site == null
				? ModLocalization.Loc("hc_ui_build_workers_no_site")
				: ModLocalization.Loc("hc_ui_build_workers_none");
		}
	}

	/// <summary>
	/// The two-second tick of the materials: one <see cref="MHR_Build.SiteMaterials"/> (the plan's need, the bags of the
	/// site and the chests around the anchor) and the rows, the missing ones first.
	/// </summary>
	private void RefreshMaterials()
	{
		if (_site != null)
		{
			MHR_Build.SiteMaterials(_site, out _need, out _have, out _missing);
		}
		else
		{
			_need = null;
			_have = null;
			_missing = null;
		}
		_materialOrder.Clear();
		if (_need != null)
		{
			foreach (string item in _need.Keys)
			{
				_materialOrder.Add(item);
			}
			_materialOrder.Sort(_materialCompare);
		}
		for (int i = 0; i < _materialOrder.Count; i++)
		{
			string item = _materialOrder[i];
			int need = ValueOf(_need, item);
			int have = ValueOf(_have, item);
			int missing = ValueOf(_missing, item);
			ListRow row = EnsureMaterialRow(i);
			row.Root.SetActive(true);
			row.Text.text = missing > 0
				? ModLocalization.LocFmt("hc_ui_build_material_missing", ItemLabel(item), have, need, missing)
				: ModLocalization.LocFmt("hc_ui_build_material_ok", ItemLabel(item), have, need);
			row.Text.color = missing > 0 ? MissingColor : OkColor;
		}
		for (int j = _materialOrder.Count; j < _materialRows.Count; j++)
		{
			_materialRows[j].Root.SetActive(false);
		}
		if (_materialsContent != null)
		{
			_materialsContent.sizeDelta = new Vector2(0f, Mathf.Max(MaterialsH, (float)_materialOrder.Count * MaterialRowH));
		}
		if (_materialsEmpty != null)
		{
			_materialsEmpty.gameObject.SetActive(_materialOrder.Count == 0);
			_materialsEmpty.text = ModLocalization.Loc("hc_ui_build_materials_none");
		}
	}

	/// <summary>Missing first, then the biggest need, then the name - the order a builder reads.</summary>
	private int CompareMaterials(string a, string b)
	{
		int missingA = ValueOf(_missing, a);
		int missingB = ValueOf(_missing, b);
		if (missingA != missingB)
		{
			return missingB.CompareTo(missingA);
		}
		int needA = ValueOf(_need, a);
		int needB = ValueOf(_need, b);
		if (needA != needB)
		{
			return needB.CompareTo(needA);
		}
		return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
	}

	private static int ValueOf(Dictionary<string, int> counts, string key)
	{
		int value;
		return counts != null && key != null && counts.TryGetValue(key, out value) ? value : 0;
	}

	// ------------------------------------------------------------------ the blueprint library

	/// <summary>Lists the folder and rebuilds the rows (the file dropped in the folder appears on the next scan).</summary>
	private void ScanBlueprints()
	{
		_bpFiles.Clear();
		_bpFiles.AddRange(BuildBlueprints.Files());
		RefreshBlueprintRows();
	}

	private void RefreshBlueprintRows()
	{
		string filter = _bpFilter != null ? _bpFilter.text : null;
		_bpShown.Clear();
		for (int i = 0; i < _bpFiles.Count; i++)
		{
			string name = Path.GetFileNameWithoutExtension(_bpFiles[i]);
			if (string.IsNullOrEmpty(filter) || name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
			{
				_bpShown.Add(_bpFiles[i]);
			}
		}
		int shown = Mathf.Min(_bpShown.Count, MaxBlueprintRows);
		for (int i = 0; i < shown; i++)
		{
			string file = _bpShown[i];
			ListRow row = EnsureBlueprintRow(i);
			row.Root.SetActive(true);
			row.Index = i;
			row.Text.text = Path.GetFileNameWithoutExtension(file);
			bool selected = file == _bpSelected;
			row.Text.color = selected ? Color.white : new Color(0.9f, 0.9f, 0.9f, 1f);
			if (row.Background != null)
			{
				row.Background.color = selected ? SelectedColor : RowColor;
			}
		}
		for (int j = shown; j < _bpRows.Count; j++)
		{
			_bpRows[j].Root.SetActive(false);
		}
		if (_bpContent != null)
		{
			_bpContent.sizeDelta = new Vector2(0f, Mathf.Max(BpListH, (float)shown * BpRowH));
		}
		if (_bpCount != null)
		{
			_bpCount.text = ModLocalization.LocFmt("hc_ui_build_bp_count", shown, _bpFiles.Count);
		}
		if (_bpNone != null)
		{
			_bpNone.gameObject.SetActive(_bpFiles.Count == 0);
		}
	}

	/// <summary>
	/// Selects one file and reads it the way the order does (<see cref="BuildBlueprintReport"/>). This is the one place
	/// of the panel that parses a file, and it only happens on a click - never per frame nor per refresh.
	/// </summary>
	private void SelectBlueprint(int index)
	{
		if (index < 0 || index >= _bpShown.Count || index >= MaxBlueprintRows)
		{
			return;
		}
		string file = _bpShown[index];
		if (file != _bpSelected)
		{
			_deleteFile = null;
			_deleteUntil = 0f;
			if (_bpDeleteLabel != null)
			{
				_bpDeleteLabel.text = ModLocalization.Loc("hc_ui_build_bp_delete");
			}
		}
		_bpSelected = file;
		_bpReport = null;
		_bpError = null;
		BuildBlueprintReport report;
		string error;
		if (BuildBlueprintReport.Read(file, out report, out error))
		{
			_bpReport = report;
		}
		else
		{
			_bpError = error;
		}
		RefreshBlueprintDetail();
		RefreshBlueprintRows();
	}

	private void RefreshBlueprintDetail()
	{
		if (_bpDetail == null)
		{
			return;
		}
		if (_bpSelected == null)
		{
			_bpDetail.text = string.Empty;
			SetButtonEnabled(_bpDelete, _bpDeleteLabel, false, DangerColor);
			return;
		}
		string name = Path.GetFileName(_bpSelected);
		if (_bpReport != null)
		{
			_bpDetail.text = ModLocalization.LocFmt("hc_ui_build_bp_detail", name, _bpReport.Pieces, _bpReport.Skipped, _bpReport.Lines);
			_bpDetail.color = Color.white;
		}
		else if (!string.IsNullOrEmpty(_bpError))
		{
			_bpDetail.text = ModLocalization.LocFmt("hc_ui_build_bp_detail_error", name, _bpError);
			_bpDetail.color = MissingColor;
		}
		else
		{
			_bpDetail.text = name;
			_bpDetail.color = Color.white;
		}
		SetButtonEnabled(_bpDelete, _bpDeleteLabel, true, DangerColor);
	}

	/// <summary>
	/// The delete of one file: the first click only <b>names</b> it (the sentence goes to the wide detail line and the
	/// button becomes "Confirm"), the second one inside <see cref="ConfirmSeconds"/> deletes it. A file that a site is
	/// waiting for materials with is re-read every time the site checks, so that site dies with the file - the sentence
	/// says so.
	/// </summary>
	private void OnDeleteClick()
	{
		if (_bpSelected == null)
		{
			return;
		}
		string name = Path.GetFileName(_bpSelected);
		float now = Time.unscaledTime;
		if (_deleteFile != _bpSelected || now > _deleteUntil)
		{
			_deleteFile = _bpSelected;
			_deleteUntil = now + ConfirmSeconds;
			if (_bpDetail != null)
			{
				_bpDetail.text = ModLocalization.LocFmt("hc_ui_build_bp_delete_confirm", name);
				_bpDetail.color = MissingColor;
			}
			if (_bpDeleteLabel != null)
			{
				_bpDeleteLabel.text = ModLocalization.Loc("hc_ui_build_bp_confirm");
			}
			return;
		}
		_deleteFile = null;
		_deleteUntil = 0f;
		try
		{
			File.Delete(_bpSelected);
			BuildBlueprints.Invalidate();
			_bpSelected = null;
			_bpReport = null;
			_bpError = null;
			Status(ModLocalization.LocFmt("hc_ui_build_bp_deleted", name));
			Log("blueprint apagado pelo painel: " + name);
		}
		catch (Exception ex)
		{
			Status(ModLocalization.LocFmt("hc_ui_build_bp_delete_failed", name, ex.Message));
			Log("nao deu para apagar o blueprint " + name + ": " + ex.Message);
		}
		if (_bpDeleteLabel != null)
		{
			_bpDeleteLabel.text = ModLocalization.Loc("hc_ui_build_bp_delete");
		}
		ScanBlueprints();
		RefreshBlueprintDetail();
	}

	/// <summary>Expires the first click of the delete (the half-second tick), so a stale "Confirm" can never delete.</summary>
	private void ExpireDeleteConfirm()
	{
		if (_deleteFile == null || Time.unscaledTime <= _deleteUntil)
		{
			return;
		}
		_deleteFile = null;
		_deleteUntil = 0f;
		if (_bpDeleteLabel != null)
		{
			_bpDeleteLabel.text = ModLocalization.Loc("hc_ui_build_bp_delete");
		}
		RefreshBlueprintDetail();
	}

	/// <summary>
	/// The "add" of the panel: there is no file dialog in Unity/Valheim, so the Explorer window of the folder IS the
	/// add - the player drops the .blueprint there and the next scan lists it.
	/// </summary>
	private void OpenBlueprintFolder()
	{
		string folder = BuildBlueprints.Folder();
		if (_bpFolder != null)
		{
			_bpFolder.text = folder;
		}
		try
		{
			System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
			{
				FileName = folder,
				UseShellExecute = true
			});
			Log("abrindo a pasta dos blueprints: " + folder);
		}
		catch (Exception ex)
		{
			Status(ModLocalization.LocFmt("hc_ui_build_folder_failed", ex.Message));
			Log("nao deu para abrir a pasta dos blueprints: " + ex.Message);
		}
		_nextScan = 0f;   // a file dropped in right now shows up on the next tick
	}

	// ------------------------------------------------------------------ the site actions

	private void OnPauseClick()
	{
		if (_site == null)
		{
			return;
		}
		MHR_Build.PauseSite(_site);
		RefreshSite();
	}

	private void OnResumeClick()
	{
		if (_site == null)
		{
			return;
		}
		MHR_Build.ResumeSite(_site);
		RefreshSite();
	}

	private void OnCancelClick()
	{
		if (_site == null)
		{
			return;
		}
		MHR_Build.CancelSite(_site);
		RefreshSite();
		RefreshMaterials();
	}

	/// <summary>
	/// Puts one companion on the site: <see cref="MHR_Build.JoinSite"/>, the very block a follower of a fresh order
	/// runs. The click <b>is</b> the order, so this is where the companion is taken over
	/// (<see cref="CompanionOrders.CanWorkSite"/> with <c>takeControl: true</c>) - reading the list never is.
	/// </summary>
	private void Join(CompanionSetup cs)
	{
		MHR_Build.SiteInfo site;
		if (!MHR_Build.CurrentSite(LocalOwner(), out site) || site == null)
		{
			Status(ModLocalization.Loc("hc_ui_build_why_nosite"));
			return;
		}
		if (!site.IsGroup || string.IsNullOrEmpty(site.Key))
		{
			Status(ModLocalization.Loc("hc_ui_build_why_solo"));
			return;
		}
		string why;
		if (!CompanionOrders.CanWorkSite(cs, site.Anchor, true, out why))
		{
			Status(WhyLabel(why));
			Log("nao entrou na obra: " + CompanionNames.NameOf(cs) + " (" + why + ")");
			return;
		}
		if (!MHR_Build.JoinSite(cs, site.Key, out why))
		{
			Status(WhyLabel(why));
			Log("nao entrou na obra: " + CompanionNames.NameOf(cs) + " (" + why + ")");
			return;
		}
		Log("entrou na obra: " + CompanionNames.NameOf(cs) + " (" + (site.Label ?? "?") + ")");
		RefreshAll();
	}

	/// <summary>Takes one companion out of the site: the very path of "para, Fulano" (the worker leaves the group and
	/// frees its piece; the others go on).</summary>
	private void Leave(CompanionSetup cs)
	{
		MHR_Build.Cancel(cs);
		Log("saiu da obra: " + CompanionNames.NameOf(cs));
		RefreshAll();
	}

	private void OnWorkerRowClick(ListRow row, WorkerAction action)
	{
		if (row == null || row.Setup == null || action == WorkerAction.None)
		{
			return;
		}
		if (action == WorkerAction.Join)
		{
			Join(row.Setup);
			return;
		}
		Leave(row.Setup);
	}

	private void Status(string text)
	{
		_status = text;
		_statusUntil = Time.unscaledTime + StatusSeconds;
		if (_siteStatus != null)
		{
			_siteStatus.text = text ?? string.Empty;
		}
	}

	// ------------------------------------------------------------------ rows (pooled: built once, reused)

	private enum WorkerAction
	{
		None,
		Join,
		Leave
	}

	/// <summary>One row of a list of the panel. A row is built once and only its texts/colours/target change afterwards
	/// (nothing is created or destroyed on a refresh).</summary>
	private sealed class ListRow
	{
		internal GameObject Root;

		internal TMP_Text Text;

		/// <summary>The second column (the phase or the reason of a companion row); null on a plain row.</summary>
		internal TMP_Text Sub;

		/// <summary>The [Join]/[Leave] button of a companion row; null on a plain row.</summary>
		internal Button Button;

		internal TMP_Text ButtonLabel;

		/// <summary>The tinted background of a selectable row (the blueprint list).</summary>
		internal Image Background;

		/// <summary>Which entry of the current data this row shows.</summary>
		internal int Index;

		internal CompanionSetup Setup;

		internal WorkerAction Action;
	}

	private ListRow EnsureWorkerRow(int index)
	{
		while (_workerRows.Count <= index)
		{
			ListRow row = new ListRow();
			GameObject go = CreateRowObject(_workersContent, "Worker", index, WorkerRowH, true);
			row.Root = go;
			row.Background = go.GetComponent<Image>();
			row.Background.color = RowColor;
			row.Text = CreateText(go.transform, "", 6f, 2f, WorkerSubX - 12f, WorkerRowH - 6f, 13f, Color.white, TextAlignmentOptions.MidlineLeft);
			row.Sub = CreateText(go.transform, "", WorkerSubX, 2f, WorkerSubW, WorkerRowH - 6f, 11f, DimColor, TextAlignmentOptions.MidlineLeft);
			TMP_Text label;
			Button button = CreateButton(go.transform, "Action", LeftW - WorkerButtonW - 6f, 3f, WorkerButtonW, WorkerRowH - 6f,
				ModLocalization.Loc("hc_ui_build_btn_join"), ButtonColor, null, out label);
			row.Button = button;
			row.ButtonLabel = label;
			button.onClick.AddListener(delegate
			{
				OnWorkerRowClick(row, row.Action);
			});
			_workerRows.Add(row);
		}
		ListRow result = _workerRows[index];
		RectTransform rt = result.Root.GetComponent<RectTransform>();
		rt.anchoredPosition = new Vector2(0f, 0f - (float)index * WorkerRowH);
		return result;
	}

	private void SetRowButton(ListRow row, string text, Color color, bool on)
	{
		if (row == null || row.Button == null)
		{
			return;
		}
		row.Button.gameObject.SetActive(on);
		if (row.ButtonLabel != null)
		{
			row.ButtonLabel.text = on ? text : string.Empty;
		}
		SetButtonEnabled(row.Button, row.ButtonLabel, on, color);
	}

	private ListRow EnsureMaterialRow(int index)
	{
		while (_materialRows.Count <= index)
		{
			ListRow row = new ListRow();
			GameObject go = CreateRowObject(_materialsContent, "Material", index, MaterialRowH, false);
			row.Root = go;
			row.Text = CreateText(go.transform, "", 6f, 1f, RightW - 12f, MaterialRowH - 4f, 12f, Color.white, TextAlignmentOptions.MidlineLeft);
			_materialRows.Add(row);
		}
		ListRow result = _materialRows[index];
		RectTransform rt = result.Root.GetComponent<RectTransform>();
		rt.anchoredPosition = new Vector2(0f, 0f - (float)index * MaterialRowH);
		return result;
	}

	private ListRow EnsureBlueprintRow(int index)
	{
		while (_bpRows.Count <= index)
		{
			ListRow row = new ListRow();
			GameObject go = CreateRowObject(_bpContent, "Blueprint", index, BpRowH, true);
			row.Root = go;
			Image background = go.GetComponent<Image>();
			background.color = RowColor;
			row.Background = background;
			Button button = go.AddComponent<Button>();
			button.targetGraphic = background;
			button.onClick.AddListener(delegate
			{
				SelectBlueprint(row.Index);
			});
			row.Button = button;
			row.Text = CreateText(go.transform, "", 8f, 1f, RightW - 16f, BpRowH - 4f, 12f, new Color(0.9f, 0.9f, 0.9f, 1f), TextAlignmentOptions.MidlineLeft);
			_bpRows.Add(row);
		}
		ListRow result = _bpRows[index];
		RectTransform rt = result.Root.GetComponent<RectTransform>();
		rt.anchoredPosition = new Vector2(0f, 0f - (float)index * BpRowH);
		return result;
	}

	/// <summary>A row of a scroll list: full width of the list (stretched), placed by its index.</summary>
	private static GameObject CreateRowObject(RectTransform parent, string name, int index, float height, bool background)
	{
		GameObject go = new GameObject(name + index, typeof(RectTransform));
		if (background)
		{
			go.AddComponent<Image>();
		}
		go.transform.SetParent(parent, worldPositionStays: false);
		RectTransform rt = go.GetComponent<RectTransform>();
		rt.anchorMin = new Vector2(0f, 1f);
		rt.anchorMax = new Vector2(1f, 1f);
		rt.pivot = new Vector2(0f, 1f);
		rt.sizeDelta = new Vector2(0f, height);
		rt.anchoredPosition = new Vector2(0f, 0f - (float)index * height);
		if (background)
		{
			// a row tint is decoration: the click belongs to the row's button (when it has one) or to the scroll area
			go.GetComponent<Image>().raycastTarget = true;
		}
		return go;
	}

	// ------------------------------------------------------------------ widgets

	private ScrollRect CreateScroll(Transform parent, string name, float x, float y, float w, float h, out RectTransform content)
	{
		GameObject view = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(RectMask2D));
		view.transform.SetParent(parent, worldPositionStays: false);
		RectTransform viewRT = view.GetComponent<RectTransform>();
		Place(viewRT, x, y, w, h);
		Image image = view.GetComponent<Image>();
		image.color = new Color(0f, 0f, 0f, 0.22f);
		// the scroll area itself takes the wheel/drag (it is the parent of every row, so it can never be in front)
		image.raycastTarget = true;
		// the invisible bar, in ONE place for the whole mod (extracted from the F8 panel and the companion window)
		Scrollbar bar = GameLook.CreateHiddenScrollbar(view.transform);
		GameObject contentGo = new GameObject("Content", typeof(RectTransform));
		contentGo.transform.SetParent(view.transform, worldPositionStays: false);
		content = contentGo.GetComponent<RectTransform>();
		content.anchorMin = new Vector2(0f, 1f);
		content.anchorMax = new Vector2(1f, 1f);
		content.pivot = new Vector2(0.5f, 1f);
		content.anchoredPosition = Vector2.zero;
		content.sizeDelta = Vector2.zero;
		ScrollRect scroll = view.AddComponent<ScrollRect>();
		scroll.content = content;
		scroll.viewport = viewRT;
		scroll.vertical = true;
		scroll.horizontal = false;
		scroll.movementType = ScrollRect.MovementType.Clamped;
		scroll.scrollSensitivity = 30f;
		scroll.verticalScrollbar = bar;
		scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
		return scroll;
	}

	/// <summary>The filter box of the blueprint list: the same hand-built field the AI panel uses (a TMP field with its
	/// own text, placeholder and viewport), registered in <see cref="ModTextInputFocus"/> so the game's keys stop while
	/// the player types.</summary>
	private TMP_InputField CreateFilterField(float x, float y, float w, float h)
	{
		GameObject go = new GameObject("BlueprintFilter", typeof(RectTransform), typeof(Image));
		// inactive until the very end: the TMP_InputField added below has to see its text component already assigned
		// when its OnEnable runs, or the caret object is never created
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

		TMP_Text text = CreateText(area.transform, "", 0f, 0f, w - 16f, h - 6f, 12f, Color.white, TextAlignmentOptions.MidlineLeft);
		text.overflowMode = TextOverflowModes.Overflow;
		text.textWrappingMode = TextWrappingModes.PreserveWhitespaceNoWrap;

		TMP_Text placeholder = CreateText(area.transform, ModLocalization.Loc("hc_ui_build_bp_filter"), 0f, 0f, w - 16f, h - 6f, 12f,
			new Color(0.6f, 0.6f, 0.6f, 0.7f), TextAlignmentOptions.MidlineLeft);
		placeholder.fontStyle = FontStyles.Italic;
		placeholder.textWrappingMode = TextWrappingModes.PreserveWhitespaceNoWrap;

		TMP_InputField input = go.AddComponent<TMP_InputField>();
		input.targetGraphic = image;
		input.textComponent = text;
		input.textViewport = areaRT;
		input.placeholder = placeholder;
		input.characterLimit = 40;
		input.contentType = TMP_InputField.ContentType.Standard;
		input.lineType = TMP_InputField.LineType.SingleLine;
		input.richText = false;
		input.onFocusSelectAll = false;
		input.caretWidth = 2;
		input.onValueChanged.AddListener(delegate
		{
			RefreshBlueprintRows();
		});

		go.SetActive(true);   // OnEnable runs here: TMP creates its caret object and caches its renderer

		Transform caret = area.transform.Find("Caret");
		if (caret != null)
		{
			caret.SetAsLastSibling();
		}
		ModTextInputFocus.Register(input);
		return input;
	}

	private Button CreateButton(Transform parent, string name, float x, float y, float w, float h, string text, Color color,
		Action onClick, out TMP_Text label)
	{
		GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
		go.transform.SetParent(parent, worldPositionStays: false);
		Place(go.GetComponent<RectTransform>(), x, y, w, h);
		Image image = go.GetComponent<Image>();
		image.color = color;
		Button button = go.GetComponent<Button>();
		button.targetGraphic = image;
		if (onClick != null)
		{
			button.onClick.AddListener(delegate
			{
				onClick();
			});
		}
		label = CreateText(go.transform, text, 2f, 2f, w - 4f, h - 4f, 12f, Color.white, TextAlignmentOptions.Center);
		return button;
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

	/// <summary>Panel-local placement: the top-left corner is (0,0) and y grows downwards, like the other panels.</summary>
	private static void Place(RectTransform rt, float x, float y, float w, float h)
	{
		rt.anchorMin = new Vector2(0f, 1f);
		rt.anchorMax = new Vector2(0f, 1f);
		rt.pivot = new Vector2(0f, 1f);
		rt.sizeDelta = new Vector2(w, h);
		rt.anchoredPosition = new Vector2(x, 0f - y);
	}

	private static void SetButtonEnabled(Button button, TMP_Text label, bool on, Color color)
	{
		if (button == null)
		{
			return;
		}
		button.interactable = on;
		if (button.targetGraphic != null)
		{
			button.targetGraphic.color = on ? color : OffColor;
		}
		if (label != null)
		{
			label.color = on ? Color.white : DimColor;
		}
	}

	// ------------------------------------------------------------------ lookups

	/// <summary>The id of the local player (the owner of the site).</summary>
	private static long LocalOwner()
	{
		Player player = Player.m_localPlayer;
		return player != null ? player.GetPlayerID() : 0L;
	}

	/// <summary>The companion of one worker id (the site keys its workers by the CompanionAI's instance id).</summary>
	private static CompanionSetup SetupOf(int workerId)
	{
		IReadOnlyList<CompanionSetup> all = CompanionSetup.AllCompanions;
		for (int i = 0; i < all.Count; i++)
		{
			CompanionSetup cs = all[i];
			CompanionAI ai = cs != null ? cs.GetComponent<CompanionAI>() : null;
			if (ai != null && ai.GetInstanceID() == workerId)
			{
				return cs;
			}
		}
		return null;
	}

	/// <summary>The item name as the player reads it (the keys of <see cref="BuildMaterials"/> are the game's own tokens,
	/// <c>$item_wood</c>, which <see cref="Localization"/> resolves).</summary>
	private static string ItemLabel(string shared)
	{
		if (string.IsNullOrEmpty(shared))
		{
			return "?";
		}
		try
		{
			if (Localization.instance != null)
			{
				string localized = Localization.instance.Localize(shared);
				if (!string.IsNullOrEmpty(localized))
				{
					return localized;
				}
			}
		}
		catch
		{
			// localization not up yet: the token is still readable
		}
		return shared;
	}

	/// <summary>The phase token of the site/worker as the player reads it (<see cref="MHR_Build.SiteInfo.Phase"/>).</summary>
	private static string PhaseLabel(string token)
	{
		switch (token)
		{
			case "paused":
				return ModLocalization.Loc("hc_ui_build_phase_paused");
			case "preparing":
				return ModLocalization.Loc("hc_ui_build_phase_preparing");
			case "collecting":
				return ModLocalization.Loc("hc_ui_build_phase_collecting");
			case "building":
				return ModLocalization.Loc("hc_ui_build_phase_building");
			case "retrying":
				return ModLocalization.Loc("hc_ui_build_phase_retrying");
			case "done":
				return ModLocalization.Loc("hc_ui_build_phase_done");
			case "waiting":
				return ModLocalization.Loc("hc_ui_build_phase_waiting");
			default:
				return string.IsNullOrEmpty(token) ? "?" : token;
		}
	}

	/// <summary>
	/// The refusal tokens of <see cref="CompanionOrders.CanWorkSite"/> (<c>owner</c>, <c>dead</c>, <c>combat</c>,
	/// <c>home</c>, <c>command</c>, <c>range</c>) and of <see cref="MHR_Build.JoinSite"/> (<c>no site</c>,
	/// <c>it's already on the site</c>, <c>it's dead</c>, <c>it's the leader</c>, <c>the site is paused</c>,
	/// <c>the site has no plan</c>, <c>the blueprint is gone</c>, <c>could not start the plan</c>) as the player reads
	/// them. A token nobody mapped is shown with the raw token inside the "refused" line - never silently dropped.
	/// </summary>
	private static string WhyLabel(string token)
	{
		switch (token)
		{
			case "owner":
				return ModLocalization.Loc("hc_ui_build_why_owner");
			case "dead":
			case "it's dead":
				return ModLocalization.Loc("hc_ui_build_why_dead");
			case "combat":
				return ModLocalization.Loc("hc_ui_build_why_combat");
			case "home":
				return ModLocalization.Loc("hc_ui_build_why_home");
			case "command":
				return ModLocalization.Loc("hc_ui_build_why_command");
			case "range":
				return ModLocalization.Loc("hc_ui_build_why_range");
			case "no site":
				return ModLocalization.Loc("hc_ui_build_why_nosite");
			case "it's the leader":
				return ModLocalization.Loc("hc_ui_build_why_leader");
			case "the site has no plan":
				return ModLocalization.Loc("hc_ui_build_why_plan");
			case "the blueprint is gone":
				return ModLocalization.Loc("hc_ui_build_why_blueprint");
			case "could not start the plan":
				return ModLocalization.Loc("hc_ui_build_why_start");
			case "it's already on the site":
				return ModLocalization.Loc("hc_ui_build_why_already");
			case "the site is paused":
				return ModLocalization.Loc("hc_ui_build_why_paused");
			default:
				return ModLocalization.LocFmt("hc_ui_build_why_unknown", token ?? "?");
		}
	}

	/// <summary>The "how long has it been running" line, in the game's own digits (m:ss).</summary>
	private static string Clock(float seconds)
	{
		int total = Mathf.Max(0, Mathf.RoundToInt(seconds));
		return (total / 60).ToString() + ":" + (total % 60).ToString("00");
	}

	private static bool IsBrokenTmpFont(TMP_FontAsset font)
	{
		if (font == null)
		{
			return true;
		}
		return font.name.IndexOf("LiberationSans", StringComparison.OrdinalIgnoreCase) >= 0;
	}

	/// <summary>The game's own font (the default one of a fresh Valheim install has no glyphs of some languages).</summary>
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

	private static void Log(string message)
	{
		if (CompanionsPlugin.LogNormal && CompanionsPlugin.Log != null)
		{
			CompanionsPlugin.Log.LogInfo("[Build] " + message);
		}
	}
}
