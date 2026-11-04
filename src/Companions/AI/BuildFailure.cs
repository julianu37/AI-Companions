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
/// </summary>
internal static class BuildFailure
{
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

	private static bool Same(string why, string label)
	{
		return string.Equals(why, label, StringComparison.OrdinalIgnoreCase);
	}
}
