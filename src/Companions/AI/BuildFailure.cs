using System;

namespace Companions;

/// <summary>
/// E7/P1 (04/11/2026): the <b>classification of a placement failure</b> - is the piece waiting for something the plan
/// itself is still going to put up, or did it really fail? It is the <b>support gate</b> of the build order
/// (<see cref="MHR_Build"/>): a failure by <b>dependency</b> (the floor/wall under the piece or the workbench the plan
/// brings is not there yet) spends <b>no</b> attempt and is never counted as skipped - it comes back with a growing wait.
/// Everything else is a <b>real</b> failure and spends one of the <c>MaxPasses</c> attempts.
///
/// <para>Measured reason for existing (04/11/2026, the 985-piece house): the ridge pieces failed with <c>no_support</c>
/// before the support existed, burned their 3 attempts and were left out of the house.</para>
///
/// <para>Pure on purpose (no Unity, no config): the table is the one the harness asserts (section (p)) and the only part
/// that needs the world - "is there anything under the pose?" - stays in <c>MHR_Build.GroundUnder</c>, which only feeds
/// <c>groundBelow</c>.</para>
///
/// <para>11/11/2026 (the <b>body barrier</b>, player's rule: "they may skip, but come back and try again before going
/// up"): <c>blocked_by_player</c> is its own class, <see cref="Kind.Blocked"/> - a body on the spot is nobody's fault,
/// so it never spends an attempt either. It is also the one reason a spot may be tried again on the same breath
/// (<see cref="IsTransient"/>/<see cref="ShortRetry"/>) - the player is walking, not the spot failing. While a body
/// holds a piece, the pieces of the storeys above it wait
/// (<see cref="BodySameStorey"/>/<see cref="BodyBarrierSeconds"/>, the numbers are here so the harness pins them and no
/// second copy of the rule exists in the jobs).</para>
///
/// <para>11/11/2026 (the <b>storey barrier</b> of the Run 9): the piece a body held came back only in the end-of-plan
/// retry pass, so the plan climbed a storey without the entrance steps. The barrier's own numbers
/// (<see cref="BodyRetryDelay"/>/<see cref="BodyRetryStep"/>/<see cref="BodyRetryMaxWait"/>) are here for the same
/// reason: one copy of the rule, pinned by the harness.</para>
///
/// <para>11/11/2026 (Run 17, <b>one clock</b>): the wait of the site's queue and the gate that holds the storeys above a
/// body are the <b>same</b> number (<see cref="BodyBarrierSeconds"/> = <see cref="BodyRetryMaxWait"/> = 10 s) - the
/// measured run had 20 s of gate against 25 s of wait, so the wait always outlived the gate and the workers stood
/// still. And a piece refused again and again by a parked player waits longer each time
/// (<see cref="BlockedDelay"/>).</para>
/// </summary>
internal static class BuildFailure
{
	/// <summary>11/11/2026 (the body barrier): how many SHORT retries one plan piece gets when the spot is held by a
	/// body - they are counted for the <b>site</b>, never per worker, and they do not spend any of the 3 attempts.</summary>
	internal const int MaxShortRetries = 3;

	/// <summary>11/11/2026: how long the short retry of a piece held by a body waits before the SAME pose is tried again
	/// (the player is walking over the spot - a moment, not a timeout).</summary>
	internal const float ShortRetryDelay = 2.5f;

	/// <summary>11/11/2026: how far above the blocked piece's level another piece may be and still be taken while the
	/// barrier lives. One storey of the measured house is 1 m, so 1,5 m keeps the <b>same</b> storey (the neighbours of
	/// the piece, the workbench) and holds back everything above it.</summary>
	internal const float BodySameStorey = 1.5f;

	/// <summary>11/11/2026: how long a body barrier lives, counted from the <b>first</b> refusal of that piece. It is what
	/// keeps a player who parks on the spot from blocking the site forever: after this the plan goes on and the piece
	/// stays in the retry queue (the same rule the player gave for the fall/roof pieces).
	///
	/// <para>11/11/2026 (Run 17): <b>10 s</b> = the 3 short retries (<see cref="MaxShortRetries"/> 3 x
	/// <see cref="ShortRetryDelay"/> 2,5 = 7,5 s) rounded up. Past that the piece is not coming <b>now</b> - the body is
	/// still on it - and it stays in the site's queue (it is never lost): holding the storeys above it longer than that is
	/// only a parked worker (measured: 31 "nothing above ... is taken" lines with 20 s).</para></summary>
	internal const float BodyBarrierSeconds = 10f;

	/// <summary>11/11/2026 (the <b>storey barrier</b>): how long a piece refused by a body waits in the site's queue before
	/// the plan may come back to it. The body is walking, not the spot failing: a couple of seconds is all the player needs
	/// to step off the entrance (measured in the Run 9 - the spot freed up while the companion was still on the previous
	/// piece). It is the first wait the queue gives a <see cref="Kind.Blocked"/> entry (<see cref="BlockedDelay"/>).</summary>
	internal const float BodyRetryDelay = 2f;

	/// <summary>11/11/2026 (the storey barrier): how much higher than the piece just built the next one had to be to count as
	/// <b>climbing a storey</b>. One storey of the measured house is 1 m, so a piece of the same storey (a neighbour, the
	/// workbench, the next tile of the floor) never triggered the barrier - only the row above did.
	///
	/// <para>11/11/2026 (Run 10): the barrier is no longer drawn by a <b>climb</b> - the wall of the 1st floor has the same y
	/// as the beam whose tiles were waiting 2 m below, and the climb rule never saw the pending piece. The constant stays
	/// (the harness pins it and it documents the storey of the measured house), but the drawing condition is
	/// <c>MHR_Build.PendingBelowIndex</c>, not this number.</para>
	/// </summary>
	internal const float BodyRetryStep = 1f;

	/// <summary>11/11/2026 (the storey barrier): how long the barrier may hold the plan before it gives up and climbs. It is
	/// the <b>same clock</b> as <see cref="BodyBarrierSeconds"/> on purpose (11/11/2026, Run 17: <b>one clock</b>) - the
	/// patience of <c>MHR_Build.ServeBarrier</c> is never allowed to outlive the <c>MHR_BuildGroup.MayTake</c> gate, or the
	/// longer one always wins: between 10 and 20+ s the gate would already let the storeys above be taken while the wait
	/// still held the worker. The name stays because the harness pins it; a different number here would be a <b>second</b>
	/// rule for the same thing.</summary>
	internal const float BodyRetryMaxWait = BodyBarrierSeconds;

	/// <summary>What a piece that could not be placed is waiting for.</summary>
	internal enum Kind
	{
		/// <summary>Something the plan itself is going to put up (a floor below, the bench ahead in the plan): the piece is
		/// kept and comes back later - it never costs an attempt and is never counted as skipped.</summary>
		Dependency,
		/// <summary>11/11/2026 (the storey barrier): a <b>body</b> (the player or another companion) is standing on the
		/// spot. It is nobody's fault - the pose is fine, the body is walking - so it never costs an attempt either, and it
		/// comes back after <see cref="BlockedDelay"/> instead of the growing dependency wait. Never counted as
		/// skipped.</summary>
		Blocked,
		/// <summary>The spot, the way to it or the payment failed: the piece spends one of the <c>MaxPasses</c> attempts and
		/// is given up when they run out.</summary>
		Real
	}

	/// <summary>
	/// Classifies the <c>why</c> of a piece that did not go up.
	///
	/// <para><b>Dependency:</b> <c>no_support</c>, the two-spellings <c>no support</c> (the WearNTear verdict of a piece
	/// that was placed and then lost its support), <c>needs_station</c> (the workbench the plan brings is ahead of it) and
	/// <c>no_ground</c> <b>when the placement probe found something under the pose</b> (the piece of the plan below).</para>
	///
	/// <para><b>Blocked:</b> <c>blocked_by_player</c> - a body is on the spot (the player walking over the entrance, another
	/// companion crossing the piece). The spot is not failing and the pose is not wrong, so it is its own class: no
	/// attempt spent and a short wait that grows with the refusals (<see cref="BlockedDelay"/>).</para>
	///
	/// <para><b>Real:</b> <c>no progress</c>, <c>clipping</c>, <c>verification: ...</c>, <c>too_steep</c>,
	/// <c>can't reach it</c>, <c>took too long</c>, a <c>no_ground</c> whose probe hit nothing (a hole, the sea, the far
	/// side of a cliff - the spot itself failing) and anything unknown. <c>null</c>/empty is real (a failure nobody named
	/// is not something to wait for).</para>
	///
	/// <para>The comparison ignores case: a status that comes back upper case from another game version must not turn a
	/// dependency into a real failure (that is exactly the bug the gate exists for).</para>
	/// </summary>
	/// <param name="why">The refusal the caller got (<c>BuildSite.Check</c> status, the executor's reason, the job's own
	/// message).</param>
	/// <param name="groundBelow">Did the placement probe find anything under the piece's pose? Only read for
	/// <c>no_ground</c>; ignored for every other reason.</param>
	internal static Kind Classify(string why, bool groundBelow)
	{
		if (string.IsNullOrEmpty(why))
		{
			return Kind.Real;
		}
		if (Same(why, "no_support") || Same(why, "no support") || Same(why, "needs_station"))
		{
			return Kind.Dependency;
		}
		if (Same(why, "blocked_by_player"))
		{
			return Kind.Blocked;   // a body on the spot: nobody's fault, no attempt spent
		}
		if (Same(why, "no_ground"))
		{
			return groundBelow ? Kind.Dependency : Kind.Real;
		}
		return Kind.Real;
	}

	/// <summary>Convenience for the callers that only need the yes/no (the build gate spends attempts on <c>Real</c>).</summary>
	internal static bool IsDependency(string why, bool groundBelow)
	{
		return Classify(why, groundBelow) == Kind.Dependency;
	}

	/// <summary>11/11/2026 (the storey barrier): is this refusal only a <b>body on the spot</b>? It is the class
	/// <see cref="Kind.Blocked"/> and nothing else - the same yes/no <see cref="IsTransient"/> answers from the string, so a
	/// caller that already classified does not have to compare strings again.</summary>
	internal static bool IsBlocked(string why, bool groundBelow)
	{
		return Classify(why, groundBelow) == Kind.Blocked;
	}

	/// <summary>
	/// 11/11/2026 (the body barrier): is this refusal only a <b>body on the spot</b> - the player (or a companion) walking
	/// over the piece? It is <b>only</b> <c>blocked_by_player</c>: that reason is its own class in
	/// <see cref="Classify"/> (<see cref="Kind.Blocked"/>: it never spends one of the 3 attempts), and it is also the one
	/// reason that may be tried again right away at the same pose, because the obstacle is somebody moving.
	///
	/// <para>Nothing else is transient on purpose: <c>no_support</c>/<c>needs_station</c>/<c>no_ground</c> are the
	/// dependency gate (they never pass the barrier - the plan itself is what is missing) and <c>clipping</c>,
	/// <c>too_steep</c>, <c>no progress</c>, <c>can't reach it</c>, <c>took too long</c> and <c>verification: ...</c> are
	/// the piece/spot failing: trying them again 2,5 s later would only spend the pose checks. <c>null</c>/empty is not
	/// transient (a failure nobody named is not a body).</para>
	/// </summary>
	internal static bool IsTransient(string why)
	{
		return Same(why, "blocked_by_player");
	}

	/// <summary>
	/// 11/11/2026 (the body barrier): may the same pose be tried again after <paramref name="tries"/> short retries? Only
	/// a transient refusal, and only for the first <see cref="MaxShortRetries"/> of them (0/1/2 yes, 3 no) - after that
	/// the piece takes the normal path (the site's retry queue, where the barrier below keeps the upper storeys
	/// waiting).
	/// </summary>
	internal static bool ShortRetry(string why, int tries)
	{
		return IsTransient(why) && tries < MaxShortRetries;
	}

	/// <summary>11/11/2026 (Run 17): how long a piece refused by a BODY waits in the site's queue, by how many body
	/// refusals it already had. A player walking over the spot is a couple of seconds; a player PARKED on it made the
	/// same piece be picked, walked to and refused again every 2 s (36 bounces in the Run 16 log, each one a walk across
	/// the site). The wait grows to keep the peace and stops at the same 6 s: the piece is still retried by the queue.</summary>
	internal static float BlockedDelay(int blocks)
	{
		if (blocks <= 1)
		{
			return BodyRetryDelay;   // the walking player: the piece comes back in 2 s
		}
		if (blocks == 2)
		{
			return 4f;   // refused twice: it is not just somebody crossing the spot
		}
		return 6f;   // parked: the queue keeps it, but the workers stop walking to it every other piece
	}

	/// <summary>
	/// 11/11/2026 (the <b>cut</b> of the storey barrier): is it worth <b>standing still</b> for a piece whose clock says
	/// <paramref name="dueAt"/>? True while the patience that is left (<paramref name="patience"/>) still covers it - the piece
	/// can be served before the barrier gives up. False when the piece comes later than the patience (the worker climbs
	/// instead of waiting for something it will never see) and false on a <c>NaN</c> clock (it never holds a worker). A
	/// <paramref name="dueAt"/> of 0 or less means "there is no clock to read": the caller already knows there is a piece
	/// below, so it <b>holds</b> - the behaviour of before this cut.
	///
	/// <para>Pure on purpose: the harness pins the two cases the review asked for (the lowest piece due in 20 s with one above
	/// it due in 2 s is a 2 s wait, held; everything later than the patience is not held).</para>
	/// </summary>
	internal static bool BodyWaitsFor(float dueAt, float now, float patience)
	{
		if (!(dueAt > 0f))
		{
			return !float.IsNaN(dueAt);   // no clock: hold (a NaN clock is not a reason to wait)
		}
		return dueAt - now <= patience;
	}

	private static bool Same(string why, string label)
	{
		return string.Equals(why, label, StringComparison.OrdinalIgnoreCase);
	}
}
