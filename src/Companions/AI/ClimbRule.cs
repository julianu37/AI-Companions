namespace Companions;

/// <summary>
/// 12/11/2026 (measured: two companions frozen inside the walls of the site they were building for over 20 min):
/// <b>which step up is a way out</b> - the rule <c>MHR_Mover.FindClimb</c> walks its 16 directions by.
///
/// <para>The defect it fixes is a single line inside a raycast loop:</para>
/// <code>
/// if (rise > 1.0f &amp;&amp; hit.collider.gameObject.layer != s_terrainLayer) break;   // a built wall or a roof, not a slope
/// </code>
/// <para>That line exists so a house wall is not treated as a hill: a companion following the player must not start
/// climbing the player's own house. But the same line refuses the <b>fence, the beam and the half wall the companion
/// himself just placed</b> - and those are exactly what surrounds him when a build order boxes him in. The result is an
/// enclosure with no exit: every direction is a built piece taller than a hop, so <c>FindClimb</c> returns false and the
/// companion stays put forever.</para>
///
/// <para>The rule kept: land (terrain) is climbable at its slope, a <b>built</b> step is climbable only when it is low
/// enough to be vaulted <b>and</b> has somewhere to stand on top. A house wall (2 m and up, or with nothing behind the
/// lip) still fails both tests, so the player's house stays a wall.</para>
///
/// <para>Pure (numbers and booleans only, no Unity): the harness links this file and asserts the table (section (ag)).</para>
/// </summary>
internal static class ClimbRule
{
	/// <summary>The tallest <b>built</b> step a companion vaults, in meters. A Valheim hop clears ~1,1 m standing still
	/// (the same figure <c>HopLine</c> uses), and a step needs margin over the body's own height: above this the body
	/// hits the lip instead of clearing it. Terrain is not limited by this - a slope is walked, not vaulted.</summary>
	internal const float BuiltStepMax = 1.6f;

	/// <summary>Above this a terrain step is not a slope but a cliff: <c>FindClimb</c> already breaks at 6 m, and a
	/// companion cannot vault a cliff, so the two agree here.</summary>
	internal const float TerrainStepMax = 6f;

	/// <summary>
	/// May the companion climb this step? <paramref name="rise"/> is the height gained by this one-meter step,
	/// <paramref name="isTerrain"/> is whether the hit collider is the game's terrain layer, and
	/// <paramref name="hasStandingRoom"/> is whether the spot past the lip fits the body's capsule.
	///
	/// <para>Land: any rise up to <see cref="TerrainStepMax"/> (walked, not vaulted). Built: up to
	/// <see cref="BuiltStepMax"/> <b>and</b> with room to stand - a lip with a void or a wall behind it is a roof edge,
	/// not a way out, and climbing it would leave the companion worse off than standing still.</para>
	/// </summary>
	internal static bool CanClimb(float rise, bool isTerrain, bool hasStandingRoom)
	{
		if (rise > TerrainStepMax) return false;
		if (isTerrain) return true;
		return rise <= BuiltStepMax && hasStandingRoom;
	}

	/// <summary>
	/// The same test for the two-step lookahead of <c>FindClimb</c>: a step that dips back down means that direction is
	/// not a way up at all. Kept next to <see cref="CanClimb"/> so the two halves of the loop are proven together.
	/// </summary>
	internal static bool KeepsGoing(float rise)
	{
		return rise >= -1.5f;
	}
}
