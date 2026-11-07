using System.Collections.Generic;
using UnityEngine;

namespace Companions;

/// <summary>
/// 04/11/2026 (Run 14, the player's report <i>"eles tão muito travados aqui no final da build"</i>): <b>where does the
/// companion of the site step to</b> when another one has to place a piece on its body (<c>[Building] StepAside</c>)?
/// The step used to be "the closest of 8 spots 2,2 m around the piece", and on the last row of roof tiles that spot was
/// <b>the next piece of the plan</b>: the companion walked out of piece 909 onto 910, the placer of 910 asked it to move
/// again (onto 911 - the next one), and the two spent the end of the build shuffling each other around the ridges:
/// <code>
/// C5: corpo na peça 909 ("C6") - pedindo para sair da frente
/// C6: saindo da frente (peça em -160,7, -424,6) para (-162,3, -426,2)   &lt;- on top of piece 910
/// C5: corpo na peça 910 ("C6") - pedindo para sair da frente
/// C6: saindo da frente (peça em -161,9, -426,2) para (-161,9, -424,0)
/// </code>
///
/// <para>So the destination is chosen by this rule, in this order:</para>
/// <list type="number">
/// <item>a spot must be <b>1,5 m in XZ away from every pending piece of the plan</b> (a piece that is not done and not
/// given up) within <see cref="Height"/> of the piece being placed - the same row/storey. Nothing free = <b>no step at
/// all</b> (the piece goes to the site's queue: better than the ping-pong);</item>
/// <item>the directions that walk <b>away from the placer's front</b> come first (the plan's next pieces are ahead of it,
/// where it is looking), then its two <b>sides</b>, and its <b>front is never</b> a candidate;</item>
/// <item>inside the same preference the <b>direction most directly behind</b> the placer wins, then the <b>shortest
/// step</b> (a shorter walk is a smaller chance of a step that does not happen); ties are broken by the coordinates, so
/// the answer never depends on the order of the ring.</item>
/// </list>
///
/// <para>Pure on purpose (plain vectors, no <c>Physics</c>, no <c>Time</c>): the harness asserts the table (section
/// (y)). The Unity side - the pending pieces of the site (<c>MHR_BuildGroup.PendingNear</c>), the ground ray and
/// <c>MHR_Mover.SafeStep</c> - is <c>MHR_Build.AsideSpot</c>.</para>
/// </summary>
internal static class BuildAsideSpot
{
	/// <summary>
	/// How far in XZ a spot must be from a <b>pending</b> piece to be free of it - the flat rule of <see cref="Plan"/>
	/// (<see cref="Blocked"/>): the body window of the game (<c>BuildBody</c>: the capsule plus the piece's box) with room for
	/// the walk.
	///
	/// <para>04/11/2026 (the honest cost of this rule, measured): an exact test was tried in its place - the candidate's capsule
	/// against the rotated box of every pending piece (<c>BuildProbe.BoxOf</c>/<c>CapsuleOf</c> + <c>BuildBody.HalfY</c>/
	/// <c>Blocks</c>) - and it is the right idea with the wrong granularity: <c>Blocks</c> has no tolerance, so a pending floor
	/// <b>above</b> the spot and the neighbouring tiles refused every candidate. The run measured <b>424</b> <c>sem lugar
	/// livre</c> and <b>429</b> <c>corpo no caminho</c> in 638 pieces (<b>67%</b> of the plan refused for a body) against
	/// <b>4,4 per 100 pieces</b> with this circle, so the circle is back. It stays a <b>debt</b>, not a solution: on a storey
	/// being roofed the border <b>is made of</b> pending pieces and this 1,5 m rejects the whole ring (measured: 424 refusals),
	/// and it has no vertical dimension (a piece of the storey above blocks a spot at the same XZ).</para>
	/// </summary>
	internal const float Clearance = 1.5f;

	/// <summary>
	/// How far above/below the spot another pending piece still shares its <b>row</b> - a piece of the storey above (a floor
	/// 2 m up) is not something the step lands on.
	///
	/// <para>04/11/2026 (measured: <b>42 of 52</b> blocked pieces never moved anybody, 81%): it was <b>2,0 m</b> and the test
	/// is <c>&gt; Height</c>, so a row <b>exactly</b> 2,0 m above (the 46,8 tiles of the measured house against the 48,8 ones)
	/// counted as blocking a spot where the companion was only <b>under</b> it - the body is 1,8 m tall
	/// (<c>BuildProbe.CapsuleOf</c>/<c>BuildBody</c>), so a piece 2,0 m up cannot be what the step lands on. <b>1,8 m</b> is the
	/// capsule: a piece lower than that still counts (the companion's head is inside it).</para>
	/// </summary>
	internal const float Height = 1.8f;

	/// <summary>The ring of directions a spot is picked from (8 = 45 degrees apart).</summary>
	internal const int Ring = 8;

	/// <summary>Distances closer than this are a tie and the coordinates decide (metres: half a millimetre).</summary>
	private const float TieSlack = 0.0005f;

	/// <summary>Two coordinates closer than this are the same one (a tenth of a millimetre). The ring is built with
	/// <c>sin</c>/<c>cos</c>, so "zero" comes back as 6e-17 and would otherwise decide the tie-break by the sign of a
	/// rounding artifact - the two sides of a piece would swap places for no reason.</summary>
	private const float CoordinateSlack = 0.0001f;

	/// <summary>
	/// Fills <paramref name="spots"/> with the places <paramref name="me"/> may step to, in the order they are tried
	/// (away from the placer's front first, then its sides; inside a rank the direction most directly behind the placer
	/// wins, then the shortest step), and returns how many there are (0 = nowhere to go - the caller must not ask for the
	/// step).
	/// <para><paramref name="from"/> is the pose of the piece being placed (the point to get away from),
	/// <paramref name="away"/> is the direction the placer has its back to (its front negated, XZ only),
	/// <paramref name="pending"/> are the poses of the plan's pending pieces (<c>MHR_BuildGroup.PendingNear</c>) and
	/// <paramref name="distance"/> is the length of the step. A degenerate <paramref name="away"/> (the placer looking
	/// straight up or down) drops the preference - every direction is a side then - but the pending pieces still
	/// filter the spots.</para>
	/// </summary>
	internal static int Plan(Vector3 me, Vector3 from, Vector3 away, IList<Vector3> pending, float distance,
		Vector3[] spots)
	{
		if (spots == null || spots.Length == 0 || !Finite(me) || !Finite(from))
		{
			return 0;
		}
		if (!Finite(distance) || distance <= 0f)
		{
			return 0;
		}
		Vector3 flat;
		bool facing = Facing(away, out flat);
		int count = 0;
		uint taken = 0;
		for (int rank = 0; rank < 2; rank++)
		{
			while (count < spots.Length)
			{
				int best = -1;
				float bestDot = float.MinValue;
				float bestFlat = float.MaxValue;
				Vector3 bestSpot = Vector3.zero;
				for (int i = 0; i < Ring; i++)
				{
					if ((taken & (1u << i)) != 0)
					{
						continue;
					}
					Vector3 dir = Direction(flat, facing, i);
					if (Rank(flat, facing, dir) != rank)
					{
						continue;
					}
					Vector3 spot = new Vector3(from.x + dir.x * distance, me.y, from.z + dir.z * distance);
					if (Blocked(spot, from.y, pending))
					{
						continue;   // on top of a piece that is still going to be built
					}
					float dot = facing ? flat.x * dir.x + flat.z * dir.z : 0f;
					float flatDistance = Flat(me, spot);
					if (best < 0 || Better(dot, bestDot, flatDistance, bestFlat, spot, bestSpot))
					{
						best = i;
						bestDot = dot;
						bestFlat = flatDistance;
						bestSpot = spot;
					}
				}
				if (best < 0)
				{
					break;   // nothing left in this rank (whatever is left is blocked)
				}
				taken |= 1u << best;
				spots[count] = bestSpot;
				count++;
			}
		}
		return count;
	}

	/// <summary>
	/// Is this candidate better than the current best: the one more directly <b>behind the placer</b> first (its dot with
	/// the away vector), then the <b>shorter</b> step, then the coordinates. The three keys are what make the answer
	/// independent of the ring's order and of the order the slots were filled in.
	/// </summary>
	private static bool Better(float dot, float bestDot, float flat, float bestFlat, Vector3 spot, Vector3 best)
	{
		if (dot > bestDot + TieSlack)
		{
			return true;
		}
		if (dot < bestDot - TieSlack)
		{
			return false;
		}
		if (flat < bestFlat - TieSlack)
		{
			return true;
		}
		if (flat > bestFlat + TieSlack)
		{
			return false;
		}
		return Before(spot, best);
	}

	/// <summary>
	/// The unit direction of the ring for index <paramref name="i"/>: measured from <paramref name="away"/> when the
	/// placer has a usable facing (0 = straight behind it, 2 and 6 = its sides, 4 = its front), otherwise from +X (the
	/// world ring, no preference).
	/// </summary>
	private static Vector3 Direction(Vector3 away, bool facing, int i)
	{
		double angle = System.Math.PI * 2.0 * i / Ring;
		float cos = (float)System.Math.Cos(angle);
		float sin = (float)System.Math.Sin(angle);
		if (!facing)
		{
			return new Vector3(cos, 0f, sin);
		}
		return new Vector3(away.x * cos + away.z * sin, 0f, away.z * cos - away.x * sin);
	}

	/// <summary>
	/// The rank of a direction: 0 = away from the piece, behind the placer (where it came from, already built); 1 = its
	/// two sides; 2 = its front - where the plan's next pieces usually are - which is <b>never</b> used. Without a facing
	/// every direction is a side (fail-open: the pending pieces still filter it).
	/// </summary>
	private static int Rank(Vector3 away, bool facing, Vector3 dir)
	{
		if (!facing)
		{
			return 1;
		}
		float dot = away.x * dir.x + away.z * dir.z;
		if (dot >= 0.5f)
		{
			return 0;
		}
		return dot > -0.5f ? 1 : 2;
	}

	/// <summary>Is the spot too close to a pending piece of the plan? The band is taken around the piece being placed
	/// (<c>from.y</c>): another piece of the same row/storey blocks the spot, a floor a storey up does not.</summary>
	private static bool Blocked(Vector3 spot, float pieceY, IList<Vector3> pending)
	{
		if (pending == null)
		{
			return false;
		}
		for (int i = 0; i < pending.Count; i++)
		{
			Vector3 piece = pending[i];
			if (!Finite(piece) || System.Math.Abs(piece.y - pieceY) > Height)
			{
				continue;
			}
			if (Flat(spot, piece) < Clearance)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>The placer's back as a unit XZ vector. False when it has no usable facing (looking straight up/down, or a
	/// value that is not a number): the ring is then the world one and no direction is "the front".</summary>
	private static bool Facing(Vector3 away, out Vector3 flat)
	{
		flat = Vector3.zero;
		if (!Finite(away))
		{
			return false;
		}
		float length = (float)System.Math.Sqrt(away.x * away.x + away.z * away.z);
		if (length < 0.1f)
		{
			return false;
		}
		flat = new Vector3(away.x / length, 0f, away.z / length);
		return true;
	}

	/// <summary>The final tie-break of two equidistant spots: x ascending, then z ascending, both with
	/// <see cref="CoordinateSlack"/> (a value that is "zero" up to the float of <c>sin</c>/<c>cos</c> is not a side of its
	/// own). Two spots that are the same point keep the first one found - the order never depends on the scan.</summary>
	private static bool Before(Vector3 a, Vector3 b)
	{
		if (a.x < b.x - CoordinateSlack)
		{
			return true;
		}
		if (a.x > b.x + CoordinateSlack)
		{
			return false;
		}
		return a.z < b.z - CoordinateSlack;
	}

	/// <summary>Horizontal distance (XZ) between two points. A point that is not finite answers the largest float, so it
	/// never wins a tie and never blocks (a NaN piece cannot be reasoned about - it is left out of the filter).</summary>
	private static float Flat(Vector3 a, Vector3 b)
	{
		float dx = a.x - b.x;
		float dz = a.z - b.z;
		float flat = (float)System.Math.Sqrt(dx * dx + dz * dz);
		return float.IsNaN(flat) || float.IsInfinity(flat) ? float.MaxValue : flat;
	}

	/// <summary>Is every coordinate a real number?</summary>
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
