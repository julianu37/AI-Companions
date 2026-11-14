namespace Companions;

/// <summary>
/// 13/11/2026 (the solo run of the <c>CommHomeBasicBP</c>, measured: <b>trapped at (-147,7, 38,8, -429,5) for 90 s with
/// no way out</b>): <b>is the piece being placed going to close a ceiling over the companion's own head?</b>
///
/// <para>The measured sequence, read from <c>BepInEx\LogOutput.log</c>: pieces <b>173 to 178</b> of the plan are six
/// <c>wood_floor</c> at <b>39,8</b> placed one after the other with the companion's feet at <b>38,8</b> - 0,6 to 2,5 m
/// from where it stood. The <c>inReach</c> test of <c>MHR_Build.Goto</c> is the game's own eye rule and it is right:
/// a floor 1 m above the feet, 2 m away, IS within the reach. What was missing is the consequence - the sixth of them
/// <b>closed the deck over the body</b>, and the next line of the log is the mover's
/// <c>the way is blocked - trying another one at (-146,9, 38,8, -426,8)</c>, then 35 pieces of
/// <c>no progress ... route full</c> and <c>desistindo de wood_beam (saiu da obra antes de colocar)</c>.</para>
///
/// <para>The house of the blueprint is what makes it possible: the ground floor at 38,8, the deck at 39,8 - <b>1 m of
/// crawlspace</b>, with a <c>wood_wall_half</c> at 39,3 filling the gap. The companion cannot stand up in there, so the
/// floor over its head is a lid, not a ceiling.</para>
///
/// <para>The rule is the same one the game applies to a <b>player</b> standing on the spot (<c>blocked_by_player</c>), minus
/// the one exception that made this possible: the companion placing the piece is the <c>ignore</c> of
/// <c>BuildProbe.CheckPoint</c>, so its own body never refuses its own piece. That exception exists for the piece at the
/// feet (standing ON the tile you are placing), and it must not extend to a tile that lands on the body. So: the body's
/// capsule against the piece's box, with the <b>vertical window shrunk by one tile of room</b> - a piece that clears the
/// head is not a lid and is placed exactly as before.</para>
///
/// <para>Pure on purpose (plain floats, no <c>UnityEngine</c> types, no <c>Physics</c>): the harness asserts the table
/// (section (an)) outside the game. The Unity side - the capsule the game reports and the piece's box at the pose - is
/// <c>MHR_Build.OverheadBlocked</c>, which reuses <c>BuildProbe.CapsuleOf</c>/<c>BoxOf</c>/<c>BuildBody</c>.</para>
/// </summary>
internal static class BuildOverhead
{
	/// <summary>
	/// How much clear air the body needs under a piece that lands on it: the companion's height (1,8 m vanilla) has to fit
	/// <b>between its feet and the piece</b>, and it is the same number <c>BuildProbe.FallbackBodyHeight</c> uses.
	///
	/// <para>It is a height, not a gap: a floor placed <b>at the feet</b> (the tile being stood on) has 0 m of air and is
	/// still placed - the capsule's bottom is at the feet and <c>Blocks</c> refuses only the pieces that <b>cross</b> the
	/// body. What this constant refuses is the floor that lands <b>above the feet but under the head</b> - measured: 39,8
	/// against feet at 38,8, i.e. 1,0 m of air for a 1,8 m body.</para>
	/// </summary>
	internal const float Headroom = 1.8f;

	/// <summary>How close to the headroom counts as <b>at</b> it: a millimetre of float slack, so "exactly the body's
	/// height" is decided by the rule and not by the rounding of <c>38,8 + 1,8</c>.</summary>
	internal const float Slack = 0.001f;

	/// <summary>
	/// Does the piece, at this pose, land on the body standing at these feet? <paramref name="pieceBottom"/>/
	/// <paramref name="pieceTop"/> are the piece's vertical span at the pose; the body occupies
	/// <paramref name="capsuleBottom"/>..<paramref name="capsuleTop"/> and <paramref name="capsuleRadius"/> around
	/// <paramref name="axisX"/>/<paramref name="axisZ"/>.
	///
	/// <para>Two windows, as <c>BuildBody.Blocks</c>: the body has to be <b>horizontally under the piece</b> (the axis
	/// within the box's half extents grown by the capsule's radius) and the piece has to start <b>above the feet but under
	/// the head</b>. A piece at the feet is the tile being stood on and is placed - that is the case the companion's own
	/// <c>ignore</c> exists for. A piece over the head (<c>rise &gt;= headroom</c>) is a ceiling it walks under. Only the
	/// lid in between is refused.</para>
	/// </summary>
	internal static bool LandsOnHead(float axisX, float axisZ, float halfX, float halfZ, float pieceBottom,
		float pieceTop, float capsuleBottom, float capsuleTop, float capsuleRadius, float headroom)
	{
		float dx = (axisX < 0f ? -axisX : axisX) - halfX;
		if (dx < 0f)
		{
			dx = 0f;
		}
		float dz = (axisZ < 0f ? -axisZ : axisZ) - halfZ;
		if (dz < 0f)
		{
			dz = 0f;
		}
		if (dx * dx + dz * dz >= capsuleRadius * capsuleRadius)
		{
			return false;   // the body is beside the piece, not under it
		}
		// the piece is a lid only when it starts ABOVE the feet and inside the body's own height: at the feet it is the
		// floor being stood on (the game places it), and over the head it is a ceiling the companion walks under. The
		// comparison is not strict: "exactly the body's height" is a ceiling, and 38,8 + 1,8 is 40,6000003 in floats -
		// the tie has to fall on the ceiling's side or the boundary is decided by rounding.
		float rise = pieceBottom - capsuleBottom;
		return rise > 0f && rise < headroom - Slack;
	}
}
