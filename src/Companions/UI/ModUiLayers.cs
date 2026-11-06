using UnityEngine;

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

	/// <summary>The appearance editor of a companion that already exists: a window opened from a companion's tab in the
	/// AI panel, so it has to be above that panel - and above its dropdown overlay too, which is closed when it opens
	/// (the editor is a window, and the overlay of the window behind it must not draw over it).</summary>
	internal const int CompanionAppearancePanel = Base + 8;

	/// <summary>04/11/2026: the build panel (<c>[Controls] BuildPanelKey</c>) - the site the companions are building.
	/// It is the last window of the mod's band (1710), still below the game's modal popup at 1900: a site panel is a
	/// window, not a dialog.</summary>
	internal const int BuildPanel = Base + 9;

	// ------------------------------------------------------------------ "is the player in a UI of ours?"

	/// <summary>
	/// Is the companion wheel on screen right now? It is tested apart from the windows because it is not one: it draws
	/// over the panels (see <see cref="RadialMenu"/>) and its own camera/hud behaviour is not the game's radial, so the
	/// callers that fake the game's radial mode leave it out.
	/// </summary>
	internal static bool WheelOpen
	{
		get
		{
			return CompanionRadialMenu.Instance != null && CompanionRadialMenu.Instance.IsVisible;
		}
	}

	/// <summary>
	/// Is any <b>window</b> of the mod open? This is the mod's single list of windows, and every caller that has to know
	/// "the player is inside one of our panels" reads it from here instead of repeating the list: the panels refuse to
	/// open over each other, <see cref="VoiceInput"/> refuses to record over one, and the game's input patches stop the
	/// player's own keys. A new window of the mod is added <b>here</b>, once.
	///
	/// <para>Cheap on purpose: one null/reference test per panel, no allocation and no <c>GetComponent</c> - the direct
	/// command key reads <see cref="AnyOpen"/> on every frame.</para>
	/// </summary>
	internal static bool AnyWindowOpen
	{
		get
		{
			if (CompanionInteractPanel.Instance != null && CompanionInteractPanel.Instance.IsVisible)
			{
				return true;
			}
			if (Companions.ConfigPanel.Instance != null && Companions.ConfigPanel.Instance.IsVisible)
			{
				return true;
			}
			if (CompanionAiPanel.Instance != null && CompanionAiPanel.Instance.IsVisible)
			{
				return true;
			}
			if (Companions.StarterCompanionPanel.Instance != null && Companions.StarterCompanionPanel.Instance.IsVisible)
			{
				return true;
			}
			if (Companions.HomeZonePanel.Instance != null && Companions.HomeZonePanel.Instance.IsVisible)
			{
				return true;
			}
			if (Companions.CompanionAppearancePanel.IsOpen)
			{
				return true;
			}
			if (CompanionBuildPanel.IsOpen)
			{
				return true;
			}
			// the shrine menu (recover/purchase) draws over the game's own modal popup
			if (ShrinePanel.IsOpen)
			{
				return true;
			}
			return false;
		}
	}

	/// <summary>
	/// Is the player aiming a farm zone right now (the placer or its crop picker)? It counts as "in a UI" for the keys
	/// that must not fire an order from inside one, but it is <b>not</b> a window: the cursor stays locked and the player
	/// has to keep walking and looking around, so the input patches leave it out.
	/// </summary>
	internal static bool AimOverlayOpen
	{
		get
		{
			if (Companions.FarmZonePlacer.Instance != null && Companions.FarmZonePlacer.Instance.IsVisible)
			{
				return true;
			}
			return Companions.FarmZoneCropPicker.IsOpen;
		}
	}

	/// <summary>
	/// Is any UI of the mod on screen right now - a window, the wheel or an aiming overlay? The answer to "the player is
	/// in a UI, not playing", which is what the direct command key (<c>[Controls] DirectTargetKey</c>) asks before firing
	/// an order: a <b>Z</b> typed inside one of our panels must never send the companions anywhere.
	/// </summary>
	internal static bool AnyOpen
	{
		get
		{
			return WheelOpen || AnyWindowOpen || AimOverlayOpen;
		}
	}

	/// <summary>upstream 1.5.6: one debug line with the sorting order of the scene canvases (called once per
	/// panel). Our <see cref="ModUiAudit"/> does the same audit with more detail.</summary>
	internal static void LogSceneLayers(string panel)
	{
		try
		{
			System.Text.StringBuilder sb = new System.Text.StringBuilder();
			foreach (Canvas c in Resources.FindObjectsOfTypeAll<Canvas>())
			{
				if (c == null || c.gameObject == null || !c.gameObject.scene.IsValid())
				{
					continue;
				}
				sb.Append(c.name).Append('=').Append(c.sortingOrder).Append(' ');
			}
			CompanionsPlugin.Log?.LogInfo("[UiLayers] " + panel + ": " + sb.ToString().TrimEnd());
		}
		catch (System.Exception ex)
		{
			CompanionsPlugin.Log?.LogDebug("[UiLayers] LogSceneLayers falhou: " + ex.Message);
		}
	}
}
