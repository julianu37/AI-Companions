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
///
/// <para>11/11/2026 (the P0 of the redesign, approved by two independent reviews): the same file answers the <b>opposite</b>
/// question as well - <see cref="Nearest"/> is the piece this worker can place <b>from where it stands</b> (the smallest XZ
/// distance inside the reach, no <see cref="SpreadDistance"/> at all). The order measured 4,55 s per piece with 2,0 s of
/// it inside <c>Goto</c>, walking to pieces the game lets it place from afar; with the reach the companion <b>stays and
/// places</b> what is around it and only walks when the run has nothing inside the reach.</para>
///
/// <para>11/11/2026 (the repulsion, the effect the P0 created and both reviews predicted): five companions staying in their
/// own corners bunch up, because the corners of five 10 m reaches overlap. The isolation of
/// <see cref="SpreadDistance"/> therefore moved from the <b>pieces</b> to the <b>positions</b>:
/// <see cref="NearestApart"/>/<see cref="PickNearApart"/> answer a piece that is also at least
/// <see cref="SpreadDistance"/> (XZ) away from <b>every other body</b> of the crew, and fall back to the answer of before
/// when there is no such piece - the order never starves for the comfort of the crew.</para>
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
	/// 11/11/2026 (the player's rule <i>"build everything within reach before going somewhere else"</i>): of the candidates of
	/// the window that are <see cref="Far"/> from every claim, take the <b>nearest to the body</b> that is also within
	/// <paramref name="nearRadius"/> - the smallest XZ distance to <paramref name="me"/> wins, and a tie keeps the lower index
	/// (the plan's own order), so the answer never depends on the order the list was filled in.
	///
	/// <para>Three layers, the last two being today's behaviour: <b>(1)</b> the near ones above; <b>(2)</b> <see cref="Pick"/> -
	/// the first far candidate in the plan's order; <b>(3)</b> <b>0</b> - the first piece of the window, the walk of before the
	/// spread. <paramref name="passedCount"/> has the same contract as <see cref="Pick"/>: how many candidates came before the
	/// chosen one, and 0 whenever 0 is answered. Fail-open by design: a <paramref name="me"/> that is not a number or a
	/// <paramref name="nearRadius"/> of zero or less switch layer 1 off (layers 2 and 3 answer), and a candidate that is not a
	/// number is never chosen.</para>
	/// </summary>
	internal static int PickNear(IList<Vector3> window, IList<Vector3> claims, float minDistance, Vector3 me, float nearRadius,
		out int passedCount)
	{
		passedCount = 0;
		if (window == null || window.Count == 0)
		{
			return 0;
		}
		float radius = Finite(nearRadius) && nearRadius > 0f ? nearRadius : 0f;
		if (Finite(me) && radius > 0f)
		{
			int best = NearInside(window, claims, null, 0f, me, radius, minDistance);
			if (best >= 0)
			{
				passedCount = best;
				return best;
			}
		}
		return Pick(window, claims, minDistance, out passedCount);
	}

	/// <summary>
	/// 11/11/2026 (the repulsion of the P0, the player's rule <i>"eles não podem se agrupar, devem ser repelidos um de perto
	/// do outro pra não travar tudo"</i>): <see cref="PickNear"/> with <b>one</b> extra filter - the piece the worker walks to
	/// has to be at least <paramref name="isolation"/> (XZ) from <b>every</b> body of the crew as well. The P0 made each
	/// companion stay in its own corner and place everything its reach covers; the corners of five 10 m reaches overlap, so
	/// the crew bunched up (the measured run: 48 <c>corpo na peça</c>, 40 <c>sem lugar livre</c> and four of the five workers
	/// holding 21-32 % of the samples).
	///
	/// <para>Same contract as <see cref="PickNear"/>: the answer is an index into <paramref name="window"/> and
	/// <paramref name="passedCount"/> is how many candidates came before it (the caller queues exactly those). The layers are
	/// the ones of today plus the isolation on the first one: <b>(1)</b> near, far from the claims <b>and</b> apart from the
	/// bodies; <b>(2)</b> <see cref="PickNear"/> itself - the walk must never starve for the comfort of the crew; <b>(3)</b> its
	/// own layers (the spread of the Run 14 and then 0). No <paramref name="bodies"/> (or an empty list) and an
	/// <paramref name="isolation"/> of zero or less (or one that is not a number) are <see cref="PickNear"/>, byte for
	/// byte.</para>
	/// </summary>
	internal static int PickNearApart(IList<Vector3> window, IList<Vector3> claims, IList<Vector3> bodies,
		float minDistance, Vector3 me, float nearRadius, float isolation, out int passedCount)
	{
		passedCount = 0;
		if (window == null || window.Count == 0)
		{
			return 0;
		}
		if (bodies == null || bodies.Count == 0 || !Finite(isolation) || isolation <= 0f)
		{
			return PickNear(window, claims, minDistance, me, nearRadius, out passedCount);   // nothing to keep away from
		}
		float radius = Finite(nearRadius) && nearRadius > 0f ? nearRadius : 0f;
		if (Finite(me) && radius > 0f)
		{
			int best = NearInside(window, claims, bodies, isolation, me, radius, minDistance);
			if (best >= 0)
			{
				passedCount = best;
				return best;
			}
		}
		return PickNear(window, claims, minDistance, me, nearRadius, out passedCount);
	}

	/// <summary>
	/// The first layer of <see cref="PickNear"/>/<see cref="PickNearApart"/>, shared so the two cannot drift: the candidate
	/// has to be a number, <see cref="Far"/> from every claim, <see cref="Apart"/> from every body (<paramref name="bodies"/>
	/// null or an <paramref name="isolation"/> of zero or less switch that filter off) and at most
	/// <paramref name="radius"/> away from <paramref name="me"/>. The nearest one wins, a tie keeping the lower index.
	/// </summary>
	private static int NearInside(IList<Vector3> window, IList<Vector3> claims, IList<Vector3> bodies, float isolation,
		Vector3 me, float radius, float minDistance)
	{
		int best = -1;
		float bestFlat = float.MaxValue;
		for (int i = 0; i < window.Count; i++)
		{
			Vector3 piece = window[i];
			if (!Finite(piece) || !Far(piece, claims, minDistance))
			{
				continue;   // never a destination, or not far from what the others are building
			}
			if (!Apart(piece, bodies, isolation))
			{
				continue;   // a companion's body is there: the crew is not supposed to bunch up on one spot
			}
			float flat = Flat(piece, me);
			if (flat > radius)
			{
				continue;   // far from the crew but also away from me: the next piece of the row first
			}
			if (best < 0 || flat < bestFlat)
			{
				best = i;   // a tie keeps the first one seen - the lower index, the plan's own order
				bestFlat = flat;
			}
		}
		return best;
	}

	/// <summary>
	/// 11/11/2026 (the P0 of the redesign, approved by two independent reviews): the <b>nearest</b> pending piece this
	/// worker can place <b>from where it stands</b> - the smallest XZ distance to <paramref name="me"/> among the candidates
	/// that are at most <paramref name="reach"/> away (exactly <paramref name="reach"/> is inside), skipping the candidate
	/// at <paramref name="skip"/>. It answers the index into <paramref name="poses"/> (the caller keeps the plan index of
	/// each pose next to it), or <b>-1</b> when there is none - and -1 means "walk to today's piece, byte for byte".
	///
	/// <para>It is <b>not</b> <see cref="PickNear"/>: <see cref="PickNear"/> only looks at pieces that are
	/// <see cref="Far"/> from the crew, and that <see cref="SpreadDistance"/> of 3 m is exactly what keeps the companion
	/// walking from piece to piece (4,55 s/piece measured, 2,0 s of it inside <c>Goto</c>). Here what is already inside the
	/// reach is placed from there, no matter how close to another companion it is; the placement itself does not need the
	/// body next to the piece (<c>BuildExecutor.Place</c> instantiates it), so the reach is 100 % the caller's rule.</para>
	///
	/// <para><paramref name="claims"/> are the poses the site is <b>building right now</b> (<c>MHR_BuildGroup.ClaimPoses</c>):
	/// a candidate that is the very same piece as a claim belongs to another worker and is never chosen (see
	/// <see cref="Claimed"/>). The caller's <c>TryClaim</c> is the final word. <paramref name="skip"/> is an index into
	/// <paramref name="poses"/> (-1 = nothing to skip) - it is <b>not</b> a plan index: the caller's window starts after the
	/// piece it is on, so that piece is never a candidate anyway.</para>
	///
	/// <para>Fail-open by design: an empty list, a <paramref name="reach"/> of zero or less (or one that is not a number)
	/// and a <paramref name="me"/> that is not a number answer -1, and a candidate that is not a number is never chosen. A
	/// tie keeps the first one seen (the plan's own order), so the answer never depends on the order the list was filled
	/// in.</para>
	/// </summary>
	internal static int Nearest(IList<Vector3> poses, IList<Vector3> claims, Vector3 me, float reach, int skip)
	{
		if (poses == null || poses.Count == 0)
		{
			return -1;
		}
		if (!Finite(me) || !Finite(reach) || reach <= 0f)
		{
			return -1;
		}
		int best = -1;
		float bestFlat = float.MaxValue;
		for (int i = 0; i < poses.Count; i++)
		{
			if (i == skip)
			{
				continue;
			}
			Vector3 piece = poses[i];
			if (!Finite(piece))
			{
				continue;   // never a destination
			}
			float flat = Flat(piece, me);
			if (flat > reach)
			{
				continue;   // outside the reach
			}
			if (Claimed(piece, claims))
			{
				continue;   // another companion is on this very piece
			}
			if (best < 0 || flat < bestFlat)
			{
				best = i;   // a tie keeps the first one seen - the lower index, the plan's own order
				bestFlat = flat;
			}
		}
		return best;
	}

	/// <summary>
	/// 11/11/2026 (the repulsion of the P0, the player's rule <i>"eles não podem se agrupar, devem ser repelidos um de perto
	/// do outro pra não travar tudo"</i>): the <see cref="Nearest"/> piece that is <b>also</b> away from the bodies of the
	/// crew - the isolation moved from the <b>pieces</b> to the <b>positions</b>. The P0 (stay and place) fixed the walking
	/// (65 → 156 pieces/min, 52 % of the pieces without a walk) and created the effect both reviews predicted: each companion
	/// stays in its own corner and the corners of five 10 m reaches overlap, so the bodies bunch up (the measured run: 48
	/// <c>corpo na peça</c>, 40 <c>sem lugar livre</c>, 25 <c>nada acima de</c> and four of the five workers holding 21-32 %
	/// of the samples).
	///
	/// <para>The rule is the one of <see cref="Nearest"/> (inside <paramref name="reach"/> with the edge counting, no
	/// <paramref name="skip"/>, no claim, a pose that is not a number never chosen, a tie keeping the lower index) with
	/// <b>one</b> extra filter: the piece has to be at least <paramref name="isolation"/> (XZ) from <b>every</b> entry of
	/// <paramref name="bodies"/> in the same pass - not "take the nearest and check it after", because a refused nearest
	/// would only fall back to walking. Among the isolated ones, the nearest to <paramref name="me"/> wins.</para>
	///
	/// <para><b>Never starves:</b> when <b>no</b> candidate satisfies the isolation the answer is exactly
	/// <see cref="Nearest"/> - building next to a companion is better than not building. No <paramref name="bodies"/> (or an
	/// empty list) and an <paramref name="isolation"/> of zero or less (or one that is not a number) are
	/// <see cref="Nearest"/> itself; a body that is not a number is <b>ignored</b> (a broken pose cannot repel anybody).
	/// <b>-1</b> when there is nothing within the reach at all, the same contract of <see cref="Nearest"/>.</para>
	///
	/// <para>Pure on purpose (plain vectors, no <c>Physics</c>, no group): the harness pins the table next to the one of
	/// <see cref="Nearest"/>. The Unity side - who the other bodies are - is <c>MHR_Build.Bodies</c>.</para>
	/// </summary>
	internal static int NearestApart(IList<Vector3> poses, IList<Vector3> claims, IList<Vector3> bodies,
		Vector3 me, float reach, float isolation, int skip)
	{
		if (bodies == null || bodies.Count == 0 || !Finite(isolation) || isolation <= 0f)
		{
			return Nearest(poses, claims, me, reach, skip);   // nothing to keep away from: today's answer
		}
		if (poses == null || poses.Count == 0)
		{
			return -1;
		}
		if (!Finite(me) || !Finite(reach) || reach <= 0f)
		{
			return -1;
		}
		int best = -1;
		float bestFlat = float.MaxValue;
		for (int i = 0; i < poses.Count; i++)
		{
			if (i == skip)
			{
				continue;
			}
			Vector3 piece = poses[i];
			if (!Finite(piece))
			{
				continue;   // never a destination
			}
			float flat = Flat(piece, me);
			if (flat > reach)
			{
				continue;   // outside the reach
			}
			if (Claimed(piece, claims))
			{
				continue;   // another companion is on this very piece
			}
			if (!Apart(piece, bodies, isolation))
			{
				continue;   // a companion's body is there: another piece of the run first
			}
			if (best < 0 || flat < bestFlat)
			{
				best = i;   // a tie keeps the first one seen - the lower index, the plan's own order
				bestFlat = flat;
			}
		}
		// nowhere apart from the crew: the nearest of today, byte for byte - the order never starves for the isolation
		return best >= 0 ? best : Nearest(poses, claims, me, reach, skip);
	}

	/// <summary>
	/// 11/11/2026 (the repulsion): is this piece at least <paramref name="isolation"/> (XZ) away from <b>every</b> body?
	/// Exactly the isolation is <b>apart</b> (the edge counts, like the reach of the placement). A body that is not a
	/// number is ignored - a broken pose must not repel the whole crew - and no bodies, or a distance of zero or less,
	/// is always apart (fail-open: without bodies there is no repulsion at all).
	/// </summary>
	internal static bool Apart(Vector3 piece, IList<Vector3> bodies, float isolation)
	{
		if (bodies == null || bodies.Count == 0 || !Finite(isolation) || isolation <= 0f)
		{
			return true;
		}
		for (int i = 0; i < bodies.Count; i++)
		{
			Vector3 body = bodies[i];
			if (!Finite(body))
			{
				continue;   // a body that is not a number cannot repel anybody
			}
			if (Flat(piece, body) < isolation)
			{
				return false;
			}
		}
		return true;
	}

	/// <summary>
	/// Is this pose one of the <b>claimed</b> ones? A claim carries the plan's own pose (<c>MHR_BuildGroup.ClaimPoses</c>),
	/// so "the same piece" is the same point: XZ within <see cref="KeySlack"/> <b>and</b> the same height - a floor and the
	/// roof right above it share the XZ and are two different pieces, and the roof must not be dropped because the floor is
	/// claimed. A claim that is not a number is <b>ignored</b> (a broken pose in the site's claims must not freeze the crew).
	/// </summary>
	private static bool Claimed(Vector3 piece, IList<Vector3> claims)
	{
		if (claims == null || claims.Count == 0)
		{
			return false;
		}
		for (int i = 0; i < claims.Count; i++)
		{
			Vector3 claim = claims[i];
			if (!Finite(claim))
			{
				continue;
			}
			if (Flat(piece, claim) <= KeySlack && System.Math.Abs(piece.y - claim.y) <= KeySlack)
			{
				return true;
			}
		}
		return false;
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

	/// <summary>
	/// 12/11/2026 (the <b>partition of the phase</b> - the player's report <i>"a redistribuição de peças é o gargalo: o run
	/// sobe a 125/min e decai para 107/min no fim"</i>, approved by the player): the rule that makes the contention of the
	/// crew <b>disappear by construction</b> instead of being negotiated piece by piece. The plan is a nearest-neighbour tour
	/// inside each run (<c>BuildBlueprintParser.Ordered</c>/<c>NearestWithin</c>), so a <b>contiguous cut by index</b> is a
	/// contiguous stretch of the site - no geometry needed, only arithmetic.
	///
	/// <para>The phase <c>[start, end)</c> is cut into <paramref name="slots"/> chunks of (almost) equal size and the chunk of
	/// <paramref name="slot"/> is answered: its first index and how many indices it covers. Two chunks never differ by more
	/// than 1 piece and the <b>remainder goes to the first chunks</b>, so the chunks are non-increasing in size and cover
	/// <c>[start, end)</c> exactly once: each piece of the plan belongs to exactly <b>one</b> worker.</para>
	///
	/// <para>Pure on purpose (no plan, no Unity, no group): the harness pins the table. The fail-safe answer is
	/// <c>first = start, length = 0</c> - an empty chunk, never a piece of somebody else - for an empty phase
	/// (<paramref name="end"/> &lt;= <paramref name="start"/>), a number of slots of zero or less and a slot outside
	/// <c>[0, slots)</c>: the caller keeps the picks it had instead of splitting a plan by a number that makes no sense. A
	/// chunk <b>may</b> be empty when the phase has fewer pieces than the crew (the tail slots); the caller then falls back
	/// to the tail of the largest one.</para>
	/// </summary>
	internal static void Chunk(int start, int end, int slots, int slot, out int first, out int length)
	{
		first = start;
		length = 0;
		if (slots <= 0 || slot < 0 || slot >= slots || end <= start)
		{
			return;
		}
		int total = end - start;
		int size = total / slots;
		int extra = total % slots;
		// the remainder goes to the FIRST chunks: slot < extra gets one extra piece, and every chunk before it also took
		// one, which is exactly the offset the `min` adds
		first = start + slot * size + (slot < extra ? slot : extra);
		length = size + (slot < extra ? 1 : 0);
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
