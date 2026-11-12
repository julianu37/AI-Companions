namespace Companions;

/// <summary>
/// Lote 1 (player's request, 11/11/2026): <b>does this order still need a crafting station?</b> - the one decision behind
/// <c>[Building] BlueprintStations</c>, kept in a single place so the probe, the order and the executor can never disagree.
///
/// <para>The player asked for the workbench/stonecutter requirement to be lifted <b>only for blueprints</b>: a template
/// (<c>plan=hut|house8|...</c>) and a single-piece order (<c>build a workbench</c>) keep the vanilla rule, because the mod
/// knows what they cost and a workbench in range is part of what the order is. A blueprint is somebody else's file, often
/// downloaded, and the player wants the house to go up without the workbench dance.</para>
///
/// <para>The table is small on purpose and mirrors <c>[Building] BlueprintFree</c>: it is a <b>switch on the blueprint
/// path</b>, never a change of the vanilla rule anywhere else. When the switch is on, the order plans no workbench
/// (<c>MHR_Build.PlanStations</c> is not called) and the probe does not report <c>needs_station</c> - which is what keeps
/// the piece out of the retry loop the probe's own verdict would otherwise create.</para>
///
/// <para>Pure (two booleans, no Unity, no config read): the table is asserted by the harness (section (v)), so the
/// "config is irrelevant outside a blueprint" half can never drift unnoticed.</para>
/// </summary>
internal static class BuildStationCover
{
	/// <summary>
	/// The switch's meaning when the config entry is missing (a failed read): <b>on</b>, the same as the default of
	/// <c>[Building] BlueprintStations</c>. It lives here so the fallback of <c>MHR_Build.StationRequired</c> and the
	/// harness' "no config" case are the same number.
	/// </summary>
	internal const bool Default = true;

	/// <summary>
	/// Is the station rule still in force? <c>true</c> unless the order is a <b>blueprint</b> and the switch is
	/// <b>on</b> (the switch means "waived", the same polarity as <c>BlueprintFree</c>: on = no cost/requirement).
	/// </summary>
	/// <param name="isBlueprint">The order is a <c>.blueprint</c> (not a template, not a single piece).</param>
	/// <param name="config"><c>[Building] BlueprintStations</c> - <c>true</c> waives the stations in a blueprint.</param>
	internal static bool Required(bool isBlueprint, bool config)
	{
		if (!isBlueprint)
		{
			return true;   // a template or a single piece always needs the station: the switch does not reach them
		}
		return !config;
	}
}
