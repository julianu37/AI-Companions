using System.Collections.Generic;
using UnityEngine;

namespace Companions;

/// <summary>
/// 11/11/2026 (Run 14, the player's report <i>"eles parecem estar disputando pra ver quem coloca a peça primeiro, em vez
/// de trabalhar em equipe"</i>): <b>how far apart the crew of a site builds</b>. The plan of a blueprint is ordered by
/// storey, class and height and each run of equal keys is then walked as a nearest-neighbour path
/// (<c>BuildBlueprintParser.Ordered</c>/<c>NearestWithin</c>), so <b>consecutive indices are neighbours in space</b>:
/// five companions on the same plan reach for the same corner at the same time - the log of the Run 14 has <b>88</b>
/// <c>corpo na peça</c> and <b>32</b> step-aside asks (see <c>docs/re/building-notes.md</c> §17.20).
///
/// <para>The rule is one question: is a piece of the plan <b>far</b> from what the others are building? A worker looks at
/// the next few pieces of the same run (<see cref="Window"/>) and takes the first one that is at least
/// <see cref="SpreadDistance"/> (XZ) away from every piece the site is on right now; the pieces it walked past keep their
/// turn in the site's queue (<c>MHR_BuildGroup.NoteSpread</c>) and come back when the neighbourhood is free
/// (<c>MHR_BuildGroup.NextRetry</c>). Nothing else changes: the plan is never reordered, the claims, the storey barrier
/// and the retry cap stay exactly as they are, and with a single worker (or the switch off) the walk is the one of
/// before.</para>
///
/// <para>Pure on purpose (plain vectors, no <c>Physics</c>, no <c>Time</c>, no group): the harness asserts the table
/// (section (z)). The Unity side - the window of the plan and the claimed poses of the site - is
/// <c>MHR_Build.Advance</c>/<c>SpreadWindow</c>.</para>
/// </summary>
internal static class BuildSpread
{
	/// <summary>
	/// How far (XZ) a piece must be from another piece the site is building to be taken "apart". It is <b>two body
	/// windows</b> (<see cref="BuildAsideSpot.Clearance"/> = 1,5 m: the capsule of a body plus the box of the piece with
	/// room for the walk): one window is what "in the way" means, two is a place where two companions work without
	/// stepping on each other.
	/// </summary>
	internal const float SpreadDistance = 3f;

	/// <summary>
	/// How many pieces of the run a worker looks at (the next one plus 5). Six finds a free spot on a row of a house and
	/// is small enough that the piece taken is still "the work in front of me": a window of the whole storey would have
	/// the crew jumping to the other side of the site on every piece.
	/// </summary>
	internal const int Window = 6;

	/// <summary>
	/// The slack of the keys of a <b>run</b> (<see cref="SameRun"/>): the same 0,01 m the parser uses to cut its runs, so
	/// the window is exactly one run of <c>BuildBlueprintParser.NearestWithin</c>.
	/// </summary>
	internal const float KeySlack = 0.01f;

	/// <summary>
	/// Is this candidate piece <b>far</b> from everything the site is building right now? The distance is <b>XZ</b> only
	/// (a floor one storey above the piece being placed is not "in the way": its body window is about where the feet
	/// are); exactly <paramref name="minDistance"/> away is free.
	///
	/// <para>A candidate that is <b>not a number</b> is <b>never</b> far (it cannot be reasoned about - the caller then
	/// keeps today's piece), while a <b>claim</b> that is not a number is <b>ignored</b>: a broken pose in the site's
	/// claims must not freeze the whole crew. No claims, or a distance of zero or less, means far.</para>
	/// </summary>
	internal static bool Far(Vector3 piece, IList<Vector3> claims, float minDistance)
	{
		if (!Finite(piece))
		{
			return false;
		}
		float limit = Finite(minDistance) && minDistance > 0f ? minDistance : 0f;
		if (claims == null || claims.Count == 0)
		{
			return true;
		}
		for (int i = 0; i < claims.Count; i++)
		{
			Vector3 claim = claims[i];
			if (!Finite(claim))
			{
				continue;   // a claim that is not a number cannot hold the site
			}
			if (Flat(piece, claim) < limit)
			{
				return false;
			}
		}
		return true;
	}

	/// <summary>
	/// The position of the <b>first</b> candidate of <paramref name="window"/> that is <see cref="Far"/> from every claim
	/// - or <b>0</b> when there is none, which is today's behaviour (the first piece of the window is taken). The window
	/// is in the order the plan already has, so "the first one far enough" is the nearest of the far ones.
	///
	/// <para><paramref name="passedCount"/> is how many candidates came <b>before</b> the chosen one (what the caller
	/// queues as "postponed": they keep their turn and come back when the place is free), and it is <b>0</b> whenever 0
	/// is answered. A candidate that is not a number is skipped (never chosen); a <paramref name="minDistance"/> that is
	/// not a number or is zero or less answers 0 with nothing passed.</para>
	/// </summary>
	internal static int Pick(IList<Vector3> window, IList<Vector3> claims, float minDistance, out int passedCount)
	{
		passedCount = 0;
		if (window == null || window.Count == 0)
		{
			return 0;
		}
		if (!Finite(minDistance) || minDistance <= 0f)
		{
			return 0;
		}
		for (int i = 0; i < window.Count; i++)
		{
			if (!Finite(window[i]))
			{
				continue;   // never a destination
			}
			if (Far(window[i], claims, minDistance))
			{
				passedCount = i;
				return i;
			}
		}
		return 0;   // nowhere far: the first one, exactly as today
	}

	/// <summary>
	/// Are these two pieces of the plan in the <b>same run</b> - the block the parser cuts by its own keys (door flag,
	/// storey key, class, height)? Here the door flag is not needed: a door is class 4. The storey key
	/// (<c>PlannedStep.Level</c>) and the height are compared with <see cref="KeySlack"/> and the class exactly; a key
	/// that is not a number is <b>never</b> the same run (fail-closed: without a run there is no spreading, which is the
	/// behaviour of before).
	/// </summary>
	internal static bool SameRun(int classA, float levelA, float yA, int classB, float levelB, float yB)
	{
		if (classA != classB)
		{
			return false;
		}
		return Near(levelA, levelB) && Near(yA, yB);
	}

	/// <summary>Two keys of a run are the same one: both real numbers and at most <see cref="KeySlack"/> apart.</summary>
	private static bool Near(float a, float b)
	{
		if (!Finite(a) || !Finite(b))
		{
			return false;
		}
		return System.Math.Abs(a - b) <= KeySlack;
	}

	/// <summary>Horizontal distance (XZ) between two points. A point that is not finite answers the largest float, so it
	/// never looks near (the caller already left those out - this is the safety net).</summary>
	private static float Flat(Vector3 a, Vector3 b)
	{
		float dx = a.x - b.x;
		float dz = a.z - b.z;
		float flat = (float)System.Math.Sqrt(dx * dx + dz * dz);
		return float.IsNaN(flat) || float.IsInfinity(flat) ? float.MaxValue : flat;
	}

	/// <summary>Is every coordinate a real number? Internal because the window of the plan asks it too (a pose that is not a
	/// number is never a candidate, so it never enters the site's queue).</summary>
	internal static bool Finite(Vector3 v)
	{
		return Finite(v.x) && Finite(v.y) && Finite(v.z);
	}

	/// <summary>Is this float a real number?</summary>
	private static bool Finite(float value)
	{
		return !float.IsNaN(value) && !float.IsInfinity(value);
	}
}
