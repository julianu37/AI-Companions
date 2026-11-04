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
/// up"): <c>blocked_by_player</c> stays a <b>real</b> failure, but it is also the one reason a spot may be tried again
/// on the same breath (<see cref="IsTransient"/>/<see cref="ShortRetry"/>) - the player is walking, not the spot
/// failing. While a body holds a piece, the pieces of the storeys above it wait
/// (<see cref="BodySameStorey"/>/<see cref="BodyBarrierSeconds"/>, the numbers are here so the harness pins them and no
/// second copy of the rule exists in the jobs).</para>
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
	/// stays in the retry queue (the same rule the player gave for the fall/roof pieces).</summary>
	internal const float BodyBarrierSeconds = 20f;

	/// <summary>What a piece that could not be placed is waiting for.</summary>
	internal enum Kind
	{
		/// <summary>Something the plan itself is going to put up (a floor below, the bench ahead in the plan): the piece is
		/// kept and comes back later - it never costs an attempt and is never counted as skipped.</summary>
		Dependency,
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
	/// <para><b>Real:</b> <c>no progress</c>, <c>blocked_by_player</c>, <c>clipping</c>, <c>verification: ...</c>,
	/// <c>too_steep</c>, <c>can't reach it</c>, <c>took too long</c>, a <c>no_ground</c> whose probe hit nothing (a hole,
	/// the sea, the far side of a cliff - the spot itself failing) and anything unknown. <c>null</c>/empty is real
	/// (a failure nobody named is not something to wait for).</para>
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

	/// <summary>
	/// 11/11/2026 (the body barrier): is this refusal only a <b>body on the spot</b> - the player (or a companion) walking
	/// over the piece? It is <b>only</b> <c>blocked_by_player</c>: <c>blocked_by_player</c> is still a real failure in
	/// <see cref="Classify"/> (it spends one of the 3 attempts when the piece finally goes to the retry queue), but it is
	/// the one reason that may be tried again right away at the same pose, because the obstacle is somebody moving.
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

	private static bool Same(string why, string label)
	{
		return string.Equals(why, label, StringComparison.OrdinalIgnoreCase);
	}
}
