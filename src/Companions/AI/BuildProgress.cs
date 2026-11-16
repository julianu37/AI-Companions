using System.Collections.Generic;

namespace Companions;

/// <summary>
/// 15/11/2026 (the solo run of the <c>CommHomeBasicBP</c>, 985 pieces, measured in the log of the day: at piece <b>854</b>
/// the companion fell 6,9 m, took 18 damage and died; the order was resumed after the respawn - <c>back from the dead -
/// resuming the build at piece 854</c> - and the very next lines were <c>a peça 1/985 já está de pé</c>, <c>2/985</c>,
/// <c>3/985</c>…): <b>what the walk had already settled has to survive an interruption</b>.
///
/// <para>The defect is the shape of the resume, not of the walk. <c>MHR_Build.StartBlueprint</c> rebuilds a <b>fresh</b>
/// state on a resume and only the plan, the index and the retry queue travelled with the pending; a fresh state starts
/// with empty <c>PlacedIndices</c>/<c>SkippedIndices</c>. The walk then begins at the resumed index, but every piece the
/// plan holds before it - and every piece the storey barrier placed <b>out of order</b> after it - is still "not placed"
/// as far as that state knows, so it is walked to (3 m to 17 m each) and asked for a ghost check that can only answer
/// "it is already standing". The player read it exactly that way: <i>"ele deveria saber quais estão colocadas corretamente
/// depois que ele colocar a porra da peça, e não ficar passando de uma em uma pra checar se está no lugar"</i>.</para>
///
/// <para>Why the <b>index</b> is not enough: the walk places out of order by design - the storey barrier serves a pending
/// piece below the next one (<c>MHR_Build.ServeBarrier</c>) and the recovery ladder moves a given-up step to the tail
/// (<c>BuildRecovery.InsertAt</c>) - so "the walk is at 854" says nothing about which of the 853 before it are up. The two
/// <b>sets</b> are the only honest answer, and they are what this rule carries across.</para>
///
/// <para>The invariant it restores is the one the counters already keep (<c>MHR_Build.CountPlaced</c>): a plan index is
/// settled <b>once</b>, and placed and given-up are <b>disjoint</b> - a piece that was given up and then went up is
/// <b>placed</b>, never both. The resumed order's <c>PlacedTotal</c> (the clock of the ladder's first rung,
/// <see cref="BuildRecovery.QueueGap"/>) restarts from it, so no given-up piece becomes due just because the body died.</para>
///
/// <para>Pure on purpose (sets and a count in, the restored sets out): the harness pins it in section (aq) next to the
/// storey gates. The Unity side is <c>MHR_Build.RestoreProgress</c>, called by both resume paths (death and gather).</para>
/// </summary>
internal static class BuildProgress
{
	/// <summary>
	/// Writes <paramref name="savedPlaced"/>/<paramref name="savedSkipped"/> into <paramref name="placed"/>/
	/// <paramref name="skipped"/>, which are <b>cleared</b> first (the resumed state may already hold work of its own - the
	/// saved sets are the whole truth). Returns the order's placement count, <c>PlacedTotal</c>, which is exactly the number
	/// of recognised placed indices.
	///
	/// <para><paramref name="planCount"/> is the plan the indices belong to. An index outside <c>[0, planCount)</c> names no
	/// step and is dropped: a resumed order may have been rebuilt from a plan that lost lines (<c>BuildBlueprintSkip</c>
	/// drops what this game cannot build), and an index out of that range would mark a <b>different</b> piece as done - the
	/// one way this carry could hide a hole. <c>planCount &lt;= 0</c> means "the plan is not built yet" (a template, whose
	/// steps are laid out in a later phase) and no range filter is applied.</para>
	///
	/// <para>A null <paramref name="savedPlaced"/> is the "nothing to carry" case (a single piece, a pending written before
	/// this rule existed): the sets are left alone and the count is the one already there - the resume behaves as it did
	/// before, byte for byte.</para>
	/// </summary>
	internal static int Restore(ISet<int> placed, ISet<int> skipped, IEnumerable<int> savedPlaced, IEnumerable<int> savedSkipped,
		int planCount)
	{
		if (placed == null)
		{
			return 0;
		}
		int carried = 0;
		if (skipped != null)
		{
			skipped.Clear();
		}
		if (savedPlaced == null)
		{
			return placed.Count;
		}
		placed.Clear();
		foreach (int index in savedPlaced)
		{
			if (!InPlan(index, planCount))
			{
				continue;
			}
			placed.Add(index);
			carried++;
		}
		if (skipped != null && savedSkipped != null)
		{
			foreach (int index in savedSkipped)
			{
				if (InPlan(index, planCount))
				{
					skipped.Add(index);
				}
			}
			// a piece that went up after being given up is PLACED, never both (the ladder's whole point) - the saved sets
			// come from a live state that already kept this, and the carry keeps it too instead of trusting the input
			skipped.ExceptWith(placed);
		}
		return carried;
	}

	/// <summary>
	/// Does <paramref name="index"/> name a step of the plan? See <see cref="Restore"/> for what <c>planCount &lt;= 0</c>
	/// means (the plan is not built yet - nothing to filter against).
	/// </summary>
	internal static bool InPlan(int index, int planCount)
	{
		if (index < 0)
		{
			return false;
		}
		return planCount <= 0 || index < planCount;
	}
}
