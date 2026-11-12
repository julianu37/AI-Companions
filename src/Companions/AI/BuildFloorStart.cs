using System;
using System.Collections.Generic;

namespace Companions;

/// <summary>
/// 12/11/2026 (the player's report over three screenshots: "after placing the stair, or when he is going to lay the second
/// floor, he should start at the floor piece at the TOP of the stair and go from there, so he is already walking on top of
/// it - and the same from the very first ground-floor piece, so he does not get stuck UNDER it"):
/// <b>where the run of floor pieces of a storey starts</b>.
///
/// <para>The defect it fixes is in <c>BuildBlueprintParser.NearestWithin</c>: every run is walked by nearest neighbour
/// <b>from wherever the previous run ended</b>. That is right for walls, beams and roofs - they are placed from a surface
/// that already exists - but wrong for the slabs, because a slab is the surface itself. Measured on
/// <c>CommHomeBasicBP</c> (985 pieces): the first floor piece of a storey sat <b>12,6 m</b> from the stair on the ground
/// floor, <b>3,5 m</b> from the stepladder on the first, <b>9,5 m</b> on the second and <b>11,0 m</b> on the fourth. The
/// companion therefore starts the storey by placing a slab he is <b>not standing on</b> (it is only inside the placement
/// reach of the eye), lays the rest of the row from that same distant edge, and the floor he needs under his feet arrives
/// late - which is how a body ends up inside the pocket of its own site.</para>
///
/// <para>The rule, pure (floats only, no <c>Physics</c>, no <c>Time</c>): the run of slabs of a storey is walked outward
/// from the <b>landing of the way up that arrives at it</b> - the <c>stair</c>/<c>ladder</c>/<c>stepladder</c> whose own
/// height is under the storey by at least <see cref="MinRise"/> and at most <see cref="MaxRise"/>, and which stands within
/// <see cref="Near"/> of one of the run's slabs. With that seed the measured first piece of each storey lands
/// <b>1,5-2,1 m</b> from its stair, and the walk is 1% <i>shorter</i> than before (468 m against 485 m over the six floor
/// runs). Ground included: <c>y=0</c> starts at the stair landing too, which is the case the player named.</para>
///
/// <para>The seed replaces the previous run's cursor <b>only</b> as the starting point of the nearest-neighbour walk: the
/// runs, their members, the door-last pass and the storey order are untouched, and a storey whose slabs have no way up
/// keeps the old behaviour. A run from the file that is already shorter than the seeded walk is still kept.</para>
/// </summary>
internal static class BuildFloorStart
{
	/// <summary>How far above the way up its landing has to be, in meters. A stair <b>on</b> the slab it serves is not a
	/// way up to it (it is the way to the next storey), so the rise is measured from the piece's own height and a floor
	/// placed level with the stair base is not anchored by it.</summary>
	internal const float MinRise = 0.4f;

	/// <summary>How far above the way up a storey may be and still be served by it, in meters. Three metres covers a
	/// stepladder that skips a half level (the measured house has one from the ground straight to the 4th storey); a slab
	/// five metres up is reached by a different stair, and the highest qualifying one wins anyway.</summary>
	internal const float MaxRise = 3f;

	/// <summary>How close the top of the way up has to be to the run of slabs it serves, in metres (horizontal). The
	/// measured landings sit 1,5-2,1 m from the nearest slab; six metres leaves room for a landing one tile off the edge
	/// without letting a stair from the other wing anchor this storey.</summary>
	internal const float Near = 6f;

	/// <summary>
	/// The point the run of <paramref name="slabs"/> is walked outward from: the top of the way up that arrives at
	/// <paramref name="storey"/>, or false when the run has none (then the caller keeps its own cursor).
	/// <para>The highest way up wins, because it is the one that lands on this storey rather than passing through it; a tie
	/// in height goes to the nearest to the run, then to the coordinates, so the same plan always answers the same.</para>
	/// </summary>
	internal static bool Seat(IList<Slab> slabs, IList<Way> ways, float storey, out float x, out float z)
	{
		x = 0f;
		z = 0f;
		if (slabs == null || ways == null || slabs.Count == 0)
		{
			return false;
		}
		bool found = false;
		float bestHeight = 0f;
		float bestNear = 0f;
		float bestX = 0f;
		float bestZ = 0f;
		for (int i = 0; i < ways.Count; i++)
		{
			Way way = ways[i];
			if (way == null || !Finite(way.X) || !Finite(way.Y) || !Finite(way.Z))
			{
				continue;
			}
			float rise = storey - way.Y;
			if (!(rise >= MinRise && rise <= MaxRise))
			{
				continue;   // a NaN rise fails both comparisons and is out, like an infinite one
			}
			float near = DistanceToRun(slabs, way.X, way.Z);
			if (!(near <= Near))
			{
				continue;
			}
			bool better;
			if (!found)
			{
				better = true;
			}
			else if (way.Y > bestHeight + TieSlack)
			{
				better = true;
			}
			else if (way.Y < bestHeight - TieSlack)
			{
				better = false;
			}
			else if (near < bestNear - TieSlack)
			{
				better = true;
			}
			else if (near > bestNear + TieSlack)
			{
				better = false;
			}
			else if (Math.Abs(way.X - bestX) > TieSlack)
			{
				better = way.X < bestX;
			}
			else
			{
				better = way.Z < bestZ;
			}
			if (better)
			{
				found = true;
				bestHeight = way.Y;
				bestNear = near;
				bestX = way.X;
				bestZ = way.Z;
			}
		}
		x = bestX;
		z = bestZ;
		return found;
	}

	/// <summary>Is this prefab a run the rule applies to? Floor and foundation pieces only (<c>wood_floor</c>,
	/// <c>stone_floor_2x2</c>, <c>wood_foundation</c>): a workbench is class 0 in the sort too, and it is placed from the
	/// ground like the walls, not laid as a surface.</summary>
	internal static bool IsSlab(string prefab)
	{
		if (string.IsNullOrEmpty(prefab))
		{
			return false;
		}
		string name = prefab.ToLowerInvariant();
		return name.IndexOf("floor", StringComparison.Ordinal) >= 0
			|| name.IndexOf("foundation", StringComparison.Ordinal) >= 0;
	}

	/// <summary>Horizontal distance from a point to the nearest slab of the run.</summary>
	private static float DistanceToRun(IList<Slab> slabs, float x, float z)
	{
		float best = float.MaxValue;
		for (int i = 0; i < slabs.Count; i++)
		{
			Slab slab = slabs[i];
			if (slab == null)
			{
				continue;
			}
			float dx = slab.X - x;
			float dz = slab.Z - z;
			float d = dx * dx + dz * dz;
			if (d < best)
			{
				best = d;
			}
		}
		return (float)Math.Sqrt(best);
	}

	/// <summary>Distances closer than this are a tie and the next key decides (metres: half a millimetre).</summary>
	private const float TieSlack = 0.0005f;

	private static bool Finite(float value)
	{
		return !float.IsNaN(value) && !float.IsInfinity(value);
	}

	/// <summary>A slab of the run, as plain floats (the parser's <c>Line</c> is not this file's business).</summary>
	internal sealed class Slab
	{
		internal float X;
		internal float Z;

		internal Slab(float x, float z)
		{
			X = x;
			Z = z;
		}
	}

	/// <summary>The foot of a way up: the <c>stair</c>/<c>ladder</c>/<c>stepladder</c> piece's own pose.</summary>
	internal sealed class Way
	{
		internal float X;
		internal float Y;
		internal float Z;

		internal Way(float x, float y, float z)
		{
			X = x;
			Y = y;
			Z = z;
		}
	}
}
