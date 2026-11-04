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

	/// <summary>
	/// P1 (04/11/2026, measured: the emergency post of the deck run was planted with its CENTRE 1 m under the piece, so a
	/// piece 1 m above the ground got a 2 m post buried 1 m deep and the game refused the post itself with
	/// <c>no_support</c>): which of the available posts reaches a gap? Returns the <b>index</b> of the shortest post that
	/// does, or -1 (nothing that reaches it: the caller does not plant one - stacking would be needed).
	///
	/// <para><paramref name="lengths"/> are the real lengths of the posts this mod can build, measured from each prefab's own
	/// snap points (0 or less = the prefab is not in this game and is skipped). A post <b>reaches</b> when it is at most
	/// <see cref="ShortTolerance"/> shorter than the gap (the game's support box is grown by 0,15 m, so the top may sit a
	/// little under the piece) and at most <see cref="MaxOverlap"/> longer (a post a storey too long would go through the
	/// piece it is meant to hold). The shortest one wins: the post must look like the one a player would pick.</para>
	/// </summary>
	internal static int PickPost(float[] lengths, float gap)
	{
		if (lengths == null)
		{
			return -1;
		}
		int best = -1;
		float bestLength = float.MaxValue;
		for (int i = 0; i < lengths.Length; i++)
		{
			float length = lengths[i];
			if (length <= 0.01f || length < gap - ShortTolerance || length > gap + MaxOverlap)
			{
				continue;
			}
			if (length < bestLength)
			{
				bestLength = length;
				best = i;
			}
		}
		return best;
	}

	/// <summary>How much shorter than the gap a post may be. 0,2 m is the slack of the measured ladder (1 m under a 1,2 m gap,
	/// 2 m under a 2,2 m); the game's support box is grown by 0,15 m, so a post at the very edge sits 5 cm short and the piece
	/// is simply retried - better than a post that does not fit.</summary>
	internal const float ShortTolerance = 0.2f;

	/// <summary>How much longer than the gap a post may be. A little overlap is how the pieces of the house already meet;
	/// 0,35 m is slack, a whole storey (2 m) would go through the piece above.</summary>
	internal const float MaxOverlap = 0.35f;

	/// <summary>
	/// P1 (04/11/2026, measured: the post ray of a floor at 40,8 stopped on a roof eave <b>0,16 m</b> under it - "vão de
	/// 0,16 m" 318 times - and no post fits in 16 cm, while the surface a post wants, the deck floor, was 1,02 m down): the
	/// first surface of <paramref name="depths"/> that is <b>deep enough to be a base</b>, i.e. at least
	/// <paramref name="minGap"/> under the piece. <paramref name="count"/> entries are read (the caller fills a fixed
	/// buffer in the order the ray met them). Returns the index of that surface, or -1 when none of them is usable (the
	/// caller then uses the terrain height).
	///
	/// <para>Pure on purpose (floats only): the walk itself is a Unity raycast, this is the decision the harness asserts
	/// (section (r)).</para>
	/// </summary>
	internal static int UsefulSurface(float[] depths, int count, float minGap)
	{
		if (depths == null)
		{
			return -1;
		}
		int read = count < depths.Length ? count : depths.Length;
		for (int i = 0; i < read; i++)
		{
			if (depths[i] >= minGap)
			{
				return i;
			}
		}
		return -1;
	}

	/// <summary>
	/// P1 (04/11/2026, measured on the 985-piece house): can a piece of the <paramref name="candidateClass"/> class
	/// (<c>BuildBlueprintParser.ClassOf</c>) hold up a piece of the <paramref name="pieceClass"/> class?
	///
	/// <para>A <b>door</b> (4) never holds anything up - and pulling one would break "doors last". A <b>roof</b> (3) holds
	/// only a <b>roof</b>: in a gable the row of a slope is supported by the row below it, which is also a roof (roof 45 over
	/// the wall, gable over the slope), while a floor is never held by a roof above it. Everything else that is structural
	/// (floors, walls, beams, poles, stairs) holds up anything that is not a door. The east gable of that run stayed open
	/// because the pull refused every roof as a support and never fired.</para>
	/// </summary>
	internal static bool CanSupport(int pieceClass, int candidateClass)
	{
		if (candidateClass == 4)
		{
			return false;   // a door is never what holds a piece up (and it is built last on purpose)
		}
		if (candidateClass == 3)
		{
			return pieceClass == 3;   // roof on roof: the row below the slope, never the roof over a floor
		}
		return true;
	}

	/// <summary>
	/// P1 (04/11/2026, measured: 13 roof pieces stayed out for FIVE rounds and the emergency post was refused 318 times with
	/// "vão de 0,16 m"): can a collider found inside the footprint of a piece of <paramref name="pieceClass"/> hold it?
	///
	/// <para>The terrain holds anything. A <b>roof</b> (class 3, which includes <c>wood_wall_roof_*</c>) is held by <b>any</b>
	/// <see cref="WearNTear"/> - a roof piece has <c>m_supports = false</c> in Valheim (a roof does not carry support; it
	/// holds the layer above it by the <b>edge</b>, the row below of the same slope), so a rule that demands
	/// <c>m_supports</c> refuses every roof over another roof. Every other class (floor, wall, beam, pole, stair, door) still
	/// needs a piece that really carries (<paramref name="wearSupports"/>), otherwise a wall would stand on a chest.</para>
	///
	/// <para>Sibling of <see cref="CanSupport"/> (the pull's rule): there a roof only holds a <b>roof</b>; here a roof is
	/// only <b>held</b>, by anything with a <see cref="WearNTear"/>. Both agree that a door never holds and that a roof never
	/// holds a floor.</para>
	/// </summary>
	internal static bool HoldsPlacedPiece(int pieceClass, bool isTerrain, bool hasWear, bool wearSupports)
	{
		if (isTerrain)
		{
			return true;   // the ground holds every piece, whatever it is
		}
		if (!hasWear)
		{
			return false;   // a rock, a tree, an item on the ground: not a support
		}
		return pieceClass == 3 || wearSupports;
	}
}
