namespace Companions;

/// <summary>
/// The sorting order of <b>every</b> canvas of the mod, in one place - and the reason for the band it lives in.
///
/// <para><b>The problem.</b> The mod's canvases used to sit at 28..34 (the numbers the upstream shipped). The game's
/// play HUD is a canvas at <b>400</b> (<c>HUD</c>, with <c>overrideSorting</c>), its centred messages at <b>1000</b>
/// (<c>HudMessage</c>), its inventory at 600, the pause menu at <b>1700</b>, the game's modal popup
/// (<c>UnifiedPopup</c>) at <b>1900</b>, the long-press radial at <b>2000</b> and the developer console at
/// <b>5000</b>. uGUI draws - and raycasts - in the order of the canvas sorting order, so <b>every</b> canvas of the
/// mod was behind the whole play HUD. That is exactly the reported bug, and the <c>[UiDiag]</c> audit of the F8 panel
/// measured it: <c>HUD/hudroot/crosshair/HoverName</c>, a 350x150 <c>TextMeshProUGUI</c> dead in the <b>centre</b> of
/// the screen (right where the F8 panel and the centre of the wheel are), kept the click of a whole horizontal band
/// of the F8 controls - invisible, because the game had faded the text out - and drew the game's hover text over the
/// centre labels of the wheel. Moving the F8 panel from 31 to 33 was <b>not</b> enough for the very same reason: 33
/// is still below 400.</para>
///
/// <para><b>The band.</b> <see cref="Base"/> = 1701 puts the mod above every layer of the play HUD and above every
/// game window that the mod's own panels are documented to block (inventory 600, store 700, barber 800, text input
/// 1100, pause menu 1700) - all of them gated by <c>Hud.InRadial()</c>, which the mod's Harmony prefixes return
/// <c>true</c> from while one of the panels is visible, so the two can never be on screen together. The band stops at
/// <b>1899</b> on purpose, i.e. <b>below</b> the game's modal popup (<c>UnifiedPopup</c>, 1900): it is a vanilla
/// class of <c>assembly_valheim</c> whose canvas order comes from the game's own prefab, so it cannot be moved - and
/// a modal dialog must always stay in front of a window. It also stays below the game's <c>LongPressRadial</c>
/// (2000), so it never <b>ties</b> with a game canvas: a tie is decided by the registration order of the raycasters
/// (<c>RaycasterManager</c>), which the game - registered long before the mod - always wins. And it stays far below
/// the console (5000) and the cursor switcher (12000).</para>
///
/// <para><b>The order.</b> Between the mod's own canvases the rule is the usual one: the higher the number, the more
/// it is "on top". The relative order of the upstream numbers is kept, so nothing about the mod's own stacking
/// changes - only the band it happens in. This is the single source of truth: the panels read their order from here
/// instead of carrying a literal, and <see cref="ModUiAudit"/> reports whoever ends up above them.</para>
/// </summary>
internal static class ModUiLayers
{
	/// <summary>First (lowest) layer of the mod's UI. See the class summary for why 1701, and why not 2000.</summary>
	internal const int Base = 1701;

	/// <summary>The farm zone placement hint strip of <c>FarmZonePlacer</c> (was 28).</summary>
	internal const int FarmZonePlacer = Base;

	/// <summary>The home zone panel (was 29).</summary>
	internal const int HomeZonePanel = Base + 1;

	/// <summary>The companion wheel (was 30).</summary>
	internal const int RadialMenu = Base + 2;

	/// <summary>The starter companion picker (was 30).</summary>
	internal const int StarterCompanionPanel = Base + 3;

	/// <summary>The farm crop picker (was 31).</summary>
	internal const int FarmZoneCropPicker = Base + 4;

	/// <summary>The F8 config panel (was 33).</summary>
	internal const int ConfigPanel = Base + 5;

	/// <summary>The in-game AI panel (was 33).</summary>
	internal const int AiPanel = Base + 6;

	/// <summary>The dropdown-list overlay of the AI panel: one step above its panel, never below (was 34).</summary>
	internal const int AiPanelOverlay = Base + 7;
}
