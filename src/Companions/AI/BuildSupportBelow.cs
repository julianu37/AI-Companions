using System;

namespace Companions;

/// <summary>
/// P1 (04/11/2026, measured on the 985-piece house with 3 companions): what can be the <b>direct support</b> of a piece of
/// a plan - the piece below it that the game's support rule (<c>WearNTear</c>) reads. It is the question a piece refused
/// with <c>no_support</c> asks: <i>what is it waiting for?</i>
///
/// <para>The measured run left ~10 pieces of the <b>upper storey</b> behind (<c>wood_floor</c>/<c>wood_roof_45</c> at y 43,8
/// and 45,8). In a group of workers the plan is walked by <b>index</b> and nobody reorders it (the indices are the site's,
/// shared by the claims), so the piece that would hold them up may still be <b>ahead</b> in the plan - or it was skipped by
/// a worker whose turn it was and nobody went back to it. Either way the piece waits in the queue as a <b>dependency</b>
/// (no attempt spent, growing waits) until the stall watchdog gives up on it after 60 s without progress. The pull of
/// <c>MHR_Build.PullDirectSupport</c> drives that support <b>next</b>, by index, without touching the plan.</para>
///
/// <para><b>The window</b> (2,5 m in XZ, 0,4 to 2,5 m below) is the one a piece is held up by: the storeys of the measured
/// house are 1 m in the blueprint's floors and 2 m in walls/roofs, so a support at 2 m is a wall/beam top and one at 1 m is
/// the floor tile under the piece. A piece <b>less than 0,4 m below</b> is a neighbour of the same storey (a beam lying on
/// the same floor, another floor tile) - it never holds the refused piece up - and the <b>drop is always positive</b>, so
/// the pull only ever walks <b>down</b> the plan and cannot loop.</para>
///
/// <para>Pure on purpose (arithmetic only, no Unity): the window and the ranking are asserted by the harness (section (r)),
/// so a bad number here is caught outside the game.</para>
/// </summary>
internal static class BuildSupportBelow
{
	/// <summary>How far in XZ (metres) the support may be from the piece it holds up.</summary>
	internal const float MaxFlat = 2.5f;

	/// <summary>How far below (metres) the support may be - a wall/roof top two storeys down is not what holds a piece up.</summary>
	internal const float MaxDrop = 2.5f;

	/// <summary>How far below (metres) it has to be at least - closer than this it is a neighbour of the same storey, not a
	/// support (and it is what makes the pull strictly descend, so it cannot loop).</summary>
	internal const float MinDrop = 0.4f;

	/// <summary>
	/// Is the candidate at (<paramref name="dx"/>, <paramref name="drop"/>, <paramref name="dz"/>) - <b>relative to the
	/// piece it would hold up</b> - its direct support? <paramref name="drop"/> is positive when the candidate is
	/// <b>below</b> the piece (this floor's Y minus the candidate's Y).
	/// </summary>
	internal static bool IsDirectSupport(float dx, float dz, float drop)
	{
		if (drop < MinDrop || drop > MaxDrop)
		{
			return false;   // above it, on its own storey, or too far down to be what holds it
		}
		return dx * dx + dz * dz <= MaxFlat * MaxFlat;
	}

	/// <summary>
	/// How good a candidate is (smaller is better): the <b>drop weighs 10x the flat distance</b>, because what holds a piece
	/// up is what is under it - the closest below wins over a neighbour that is nearer but lower (a floor tile at 1 m in XZ
	/// loses to the beam top 0,5 m below it, which is the piece the game actually reads).
	/// </summary>
	internal static float Rank(float dx, float dz, float drop)
	{
		return drop * 10f + (float)Math.Sqrt(dx * dx + dz * dz);
	}
}
