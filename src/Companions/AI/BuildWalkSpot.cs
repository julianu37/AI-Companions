using System.Collections.Generic;
using UnityEngine;

namespace Companions;

/// <summary>
/// 11/11/2026 (Run 13, the ridge of the <c>CommHomeBasicBP</c>): <b>where should the companion stand</b> to place a piece
/// that is not at its feet? The walk used to be sent straight at the piece's pose - a roof row at 48,8, ten metres above
/// the ground - and <c>Pathfinding.GetPath</c> snaps the <c>from</c> point to the navmesh up to 12 m, so the route started
/// far from the feet and walked the <b>wrong way</b>: measured <c>no progress</c> after 12 s at <b>22,8 m</b> and then
/// <b>23,5 m</b> from the piece (it got <b>further</b> away). The piece itself was never the problem: the plan's own
/// ladders reach the floor below it and from there the game's eye rule (<c>[Building] PlaceReach</c>) covers the last
/// 2 m.
///
/// <para>So the walk target is chosen among the <b>surfaces under the piece</b> (the caller collects them with rays down:
/// finished floors, roof rows, the ground), in this order: a surface of the <b>same storey</b> (the row of the piece,
/// up to <see cref="SameStoreyBelow"/> below it and <see cref="SameStoreyAbove"/> above) - the one <b>closest to the
/// piece's own level</b> wins, and the flat distance to the companion only breaks a tie; failing that, the <b>highest</b>
/// surface the piece can be reached from - climbing is the walk it would have done anyway, and the highest one is the
/// closest to the piece. A surface the piece cannot be reached <b>from</b> (the eye rule, the same one the placement
/// uses) is not a candidate at all: a floor 30 m below a ridge is not a place to build from.</para>
///
/// <para>13/11/2026 (the run of the <c>CommHomeBasicBP</c>, measured: <b>trapped at (-147,7, 38,8, -429,5) for 90 s with
/// no way out</b>): the same-storey pass used to pick the surface <b>closest to the companion</b>, and on this house the
/// ground under a 1 m crawlspace is closer than the deck itself - the floors of the deck at 39,8 were placed from the
/// ground at 38,8 (pieces 173-178, all 0,6 to 2,5 m from the companion's own feet) and the sixth one <b>closed the
/// ceiling over him</b>. The measured house has the ground floor at 38,8, the deck at 39,8 and a <c>wood_wall_half</c> at
/// 39,3 filling the gap: the deck <b>is</b> the storey, the crawlspace under it is not a place to build from. The
/// piece's own level is therefore the first key - the surface you stand on to build AT that level is the one at it, not
/// the one nearest your feet.</para>
///
/// <para>Pure on purpose (plain floats, no <c>Physics</c>, no <c>Time</c>): the rule is asserted by the harness
/// (section (x)) outside the game. The Unity side - the rays that collect the surfaces and the structural filter (terrain,
/// or a <c>Piece</c> with <c>WearNTear</c> that is not furniture) - is <c>MHR_Build.WalkSpot</c>.</para>
/// </summary>
internal static class BuildWalkSpot
{
	/// <summary>How far below the piece a surface still counts as the <b>same storey</b> (a floor/roof row: the blueprint's
	/// storeys are 1 m in the floors and 2 m in walls/roofs, so 2,5 m keeps the row and the top of the wall below it).</summary>
	internal const float SameStoreyBelow = 2.5f;

	/// <summary>A surface a little <b>above</b> the piece is still the same storey (the ridge line of a roof, a beam top
	/// the piece is attached to). Anything higher is not a floor to stand on for this piece.</summary>
	internal const float SameStoreyAbove = 0.5f;

	/// <summary>The companion's eye height above its feet - the game measures the placement distance from the eye, and
	/// <c>MHR_Build</c> uses the same number.</summary>
	internal const float EyeHeight = 1.5f;

	/// <summary>Distances closer than this are a tie and the geometric order decides (metres: half a millimetre).</summary>
	private const float TieSlack = 0.0005f;

	/// <summary>
	/// The best surface to walk to for <paramref name="piece"/>, or false when none of <paramref name="surfaces"/> is a
	/// place the piece can be reached from (<paramref name="spot"/> is then the piece itself, so the caller keeps its old
	/// target). See the class summary for the two passes.
	/// </summary>
	internal static bool Choose(Vector3 me, Vector3 piece, float maxPlace, IList<Vector3> surfaces, out Vector3 spot)
	{
		spot = piece;
		if (surfaces == null || surfaces.Count == 0 || !Finite(piece) || float.IsNaN(maxPlace) || maxPlace <= 0f)
		{
			return false;
		}
		// first pass: the same storey (the shortest walk to a surface the piece is placed from)
		bool found = false;
		Vector3 best = piece;
		float bestFlat = 0f;
		float bestLevel = 0f;
		for (int i = 0; i < surfaces.Count; i++)
		{
			Vector3 surface = surfaces[i];
			if (!Reachable(surface, piece, maxPlace))
			{
				continue;
			}
			float below = piece.y - surface.y;
			if (below > SameStoreyBelow || below < -SameStoreyAbove)
			{
				continue;
			}
			float flat = Flat(me, surface);
			float level = Level(piece.y, surface.y);
			if (!found || Better(flat, bestFlat, level, bestLevel, surface, best, false))
			{
				found = true;
				best = surface;
				bestFlat = flat;
				bestLevel = level;
			}
		}
		if (found)
		{
			spot = best;
			return true;
		}
		// second pass: nothing of the piece's own storey - the HIGHEST surface it can still be reached from wins
		// (climbing is the walk the route does anyway, and the highest one is the closest to the piece)
		for (int i = 0; i < surfaces.Count; i++)
		{
			Vector3 surface = surfaces[i];
			if (!Reachable(surface, piece, maxPlace))
			{
				continue;
			}
			float flat = Flat(me, surface);
			float level = Level(piece.y, surface.y);
			if (!found || Better(flat, bestFlat, level, bestLevel, surface, best, true))
			{
				found = true;
				best = surface;
				bestFlat = flat;
				bestLevel = level;
			}
		}
		if (found)
		{
			spot = best;
		}
		return found;
	}

	/// <summary>Is the piece within the placement reach from the <b>eye</b> of a companion standing on this surface? The
	/// same rule the build uses (<c>MaxPlace</c> from the eye), so a candidate here is never a walk to a place where the
	/// piece would be refused.</summary>
	private static bool Reachable(Vector3 surface, Vector3 piece, float maxPlace)
	{
		if (!Finite(surface))
		{
			return false;
		}
		float dx = surface.x - piece.x;
		float dy = surface.y + EyeHeight - piece.y;
		float dz = surface.z - piece.z;
		float distance = (float)System.Math.Sqrt(dx * dx + dy * dy + dz * dz);
		return distance <= maxPlace;   // a NaN distance compares false and is out, like an infinite one
	}

	/// <summary>
	/// Is this candidate better than the current best? With <paramref name="byHeight"/> the higher surface wins (second
	/// pass). Without it the <b>level</b> comes first - the surface whose height is the closest to the piece's own, so a
	/// deck is chosen over the ground 1 m under it (see the class summary) - and the flat distance to the companion decides
	/// only between two surfaces of the same level. Ties are broken by the surface's own coordinates, so the result does
	/// not depend on the order the caller collected them in.
	/// </summary>
	private static bool Better(float flat, float bestFlat, float level, float bestLevel, Vector3 candidate, Vector3 best,
		bool byHeight)
	{
		if (byHeight)
		{
			if (candidate.y > best.y + TieSlack)
			{
				return true;
			}
			if (candidate.y < best.y - TieSlack)
			{
				return false;
			}
		}
		else
		{
			if (level < bestLevel - TieSlack)
			{
				return true;
			}
			if (level > bestLevel + TieSlack)
			{
				return false;
			}
		}
		if (flat < bestFlat - TieSlack)
		{
			return true;
		}
		if (flat > bestFlat + TieSlack)
		{
			return false;
		}
		return Before(candidate, best);
	}

	/// <summary>How far a surface's height is from the piece's own - the first key of the same-storey pass (metres of
	/// vertical distance, unsigned).</summary>
	private static float Level(float pieceY, float surfaceY)
	{
		float level = pieceY - surfaceY;
		return level < 0f ? -level : level;
	}

	/// <summary>The final tie-break: (x, y, z) ascending. Two equal candidates always answer the same way.</summary>
	private static bool Before(Vector3 a, Vector3 b)
	{
		if (a.x < b.x)
		{
			return true;
		}
		if (a.x > b.x)
		{
			return false;
		}
		if (a.y < b.y)
		{
			return true;
		}
		if (a.y > b.y)
		{
			return false;
		}
		return a.z < b.z;
	}

	/// <summary>Horizontal distance (XZ) between two points. A point that is not finite never wins a tie (it answers the
	/// largest float, so every real candidate is closer).</summary>
	private static float Flat(Vector3 a, Vector3 b)
	{
		float dx = a.x - b.x;
		float dz = a.z - b.z;
		float flat = (float)System.Math.Sqrt(dx * dx + dz * dz);
		return float.IsNaN(flat) || float.IsInfinity(flat) ? float.MaxValue : flat;
	}

	/// <summary>Is every coordinate a real number? (a NaN or an infinity from the ray side cannot decide a walk).</summary>
	private static bool Finite(Vector3 v)
	{
		return Finite(v.x) && Finite(v.y) && Finite(v.z);
	}

	/// <summary>Is this float a real number?</summary>
	private static bool Finite(float value)
	{
		return !float.IsNaN(value) && !float.IsInfinity(value);
	}
}
