namespace Companions;

/// <summary>
/// 14/11/2026 (the solo run of the <c>CommHomeBasicBP</c>, measured in game with <c>build.check</c>): <b>which support
/// rule does a pose fall under - the piece's footprint, or the one piece the vertical ray hit?</b>
///
/// <para>The probe has two <c>no_support</c> rules and it chose between them by one number: how far above the ray's hit
/// the pose stands (<c>point.y - surface.y &gt; 0.6</c>, <c>BuildProbe.CheckPoint</c>). The two ends of the measured run
/// are what that number got wrong.</para>
///
/// <para><b>What was measured</b> (two <c>wood_roof_45</c> of the run, plan items #901 and #917, the holes of the player's
/// screenshots): the ray from above the pose goes through the roof's own gap and lands on the top of a <b>chest</b>
/// (<c>piece_chest_wood</c>, <c>m_supports = false</c> - the only prefab class that answers false) <b>0,16 m</b> under the
/// pose. Under the old rule that is "a piece on the ground", so the probe asked the chest's <c>m_supports</c>, got
/// <c>false</c> and refused every attempt - 8 log lines for #901, 21 for #917, three plan rounds each, 60 s of the stall
/// watchdog, and the two pieces were dropped from the site. The probe's <b>own</b> footprint answer was never asked.</para>
///
/// <para><b>And the footprint says yes.</b> The same house has the two supports the game reads standing under both poses:
/// a <c>wood_floor</c> at <b>1,00 m</b> below, and a <c>wood_wall_roof_45</c> at <b>1,00 m</b> with <b>1,00 m</b> of flat
/// distance - both <c>m_supports = true</c>, both inside the collider box the game grows by 0,15 m
/// (<c>docs/re/support-rule-wearntear.md</c> §0-§3).</para>
///
/// <para><b>The rule</b>: furniture under the ray's hit is <b>not evidence</b> that the pose is a piece on the ground. The
/// ray hit something the game itself refuses to build on - "não dá para colocar peça em cima de mobília" (vanilla
/// <c>Player.UpdatePlacementGhost</c>, <c>Player.cs:3890-3893</c>) - and that says nothing about what holds a piece
/// 1 m above it. So when the hit does <b>not</b> support, the footprint is the test, exactly as it already is for anything
/// more than 0,6 m up. Everything else keeps the old branch: a ray hit on the terrain, on a wall, on a floor, on anything
/// structural, still answers through the piece the game reads.</para>
///
/// <para>Pure on purpose (plain booleans, no <c>UnityEngine</c>): the harness asserts the table (section (ap)). The Unity
/// side is the <c>elevated</c> local of <c>BuildProbe.CheckPoint</c>.</para>
/// </summary>
internal static class BuildSupportBranch
{
	/// <summary>
	/// How far above the ray's hit a pose has to stand to be read by its <b>footprint</b> instead of by the single piece
	/// under its centre: 0,6 m (the historical number, unchanged - a threshold, a stair's step).
	/// </summary>
	internal const float Elevated = 0.6f;

	/// <summary>
	/// Does a pose at <paramref name="drop"/> above the surface its vertical ray hit go through the <b>footprint</b> rule
	/// (<c>BuildProbe.FootprintSupported</c> - the collider box grown by 0,15 m, the game's own reach) instead of the single
	/// piece the ray hit?
	///
	/// <para><paramref name="hitSupports"/> is the <c>m_supports</c> of that piece; <c>null</c> when there is no piece
	/// there at all (terrain, rock, a tree - the ray hit nothing with a <see cref="WearNTear"/>). A hit that <b>does</b>
	/// support keeps the old answer: the piece under the centre is a floor or a wall, it is what the game reads, and the
	/// footprint would only ever be more generous.</para>
	///
	/// <para>The two ends of the measured run, which are the cases below:</para>
	/// <list type="bullet">
	/// <item><c>drop = 0,16</c> over a <b>chest</b> (<c>false</c>) - <b>elevated</b>: the two roofs of #901/#917. Under the
	/// old rule this answered <c>false</c> and the piece was refused 29 times.</item>
	/// <item><c>drop = 0,10</c> over a <b>floor</b> (<c>true</c>) - not elevated: the 81 floors of the same house stand on
	/// their neighbour and the game reads that neighbour. Asking for the footprint here changed nothing and made 10 of 10
	/// measured floors answer the same as before.</item>
	/// </list>
	/// </summary>
	internal static bool UsesFootprint(float drop, bool? hitSupports)
	{
		if (drop > Elevated)
		{
			return true;   // up in the air: the single piece under the centre is not what holds it
		}
		return hitSupports == false;   // furniture (or any piece the game refuses to build on) under a piece that is really held
	}
}
