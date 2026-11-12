using System.Collections.Generic;

namespace Companions;

/// <summary>
/// 11/11/2026 (<b>the identity of a plan piece</b>): the plan of a build order is a list of steps, and every other
/// structure of the state names a step by its <b>position in that list</b> - <c>State.PlacedIndices</c>,
/// <c>SkippedIndices</c>, <c>PulledSupport</c>, <c>FallAdjusted</c>, <c>FallbackPosts</c>, <c>BarrierServed</c>,
/// <c>Index</c>, <c>PullUntil</c>, <c>ResumeIndex</c>, <c>WalkFor</c> and, on a site, <c>MHR_BuildGroup.Done</c> /
/// <c>SkippedIndex</c> / <c>Claims</c> / <c>BodyAt</c> / <c>RetryEntry.Index</c>. Four functions of
/// <see cref="MHR_Build"/> change the list's order (<c>PreferNearFloor</c> swaps two steps, <c>InsertBenchStep</c>
/// inserts one in the middle, <c>PullFloorsAbove</c> rebuilds it, <c>PullSupports</c> removes and re-inserts), and every
/// one of them used to leave those structures pointing at <b>other pieces</b>: a piece already up became a piece to
/// pay for again, a skipped index became a good one, a claim named a neighbour.
///
/// <para>This is the arithmetic that makes a reorder safe: given the <b>new</b> plan expressed as the <b>old</b> index
/// of each position (<see cref="Map"/>), it answers where every old index went. The Unity side - rebuilding the list
/// and applying the map to the state - is <c>MHR_Build.Reorder</c>.</para>
///
/// <para>Pure on purpose (no Unity, no config, no state): the harness asserts the table (section (ag)), including the
/// invariant the whole thing exists for - <b>a marked-done index still points at the same prefab</b> after a swap, an
/// insert, a removal and an append.</para>
/// </summary>
internal static class BuildPlanOrder
{
	/// <summary>
	/// Where every index of the old plan went, given the new plan as the <b>old</b> index of each of its positions:
	/// <c>order[i]</c> is the old index of the step that is now at <paramref name="order"/>'s position <c>i</c>, and
	/// <c>-1</c> is a step that <b>did not exist</b> before (the workbench <c>InsertBenchStep</c> adds).
	///
	/// <para>The answer is <c>map[old] = new</c>, and <b>-1</b> for an old index that is no longer in the plan (a step
	/// that was dropped). An index that appears <b>twice</b> in <c>order</c> maps to its <b>first</b> position: the plan
	/// holds a step once, and a repeated entry is a bug of the caller, not a second piece. An entry that is not a valid
	/// old index (negative, or past <paramref name="oldCount"/>) is ignored - it names no old step.</para>
	/// </summary>
	internal static int[] Map(IList<int> order, int oldCount)
	{
		int[] map = new int[oldCount > 0 ? oldCount : 0];
		for (int i = 0; i < map.Length; i++)
		{
			map[i] = -1;   // until an entry of the new order claims it
		}
		if (order == null)
		{
			return map;   // nothing of the old plan survived
		}
		for (int i = 0; i < order.Count; i++)
		{
			int old = order[i];
			if (old < 0 || old >= map.Length || map[old] >= 0)
			{
				continue;   // a new step, out of range, or the second appearance of an index
			}
			map[old] = i;
		}
		return map;
	}

	/// <summary>Where <paramref name="oldIndex"/> is in the new plan, or <c>-1</c> when the step is no longer there
	/// (or the index is outside the old plan - the answer to "where did it go" for something that was never in it).</summary>
	internal static int Remap(int[] map, int oldIndex)
	{
		if (map == null || oldIndex < 0 || oldIndex >= map.Length)
		{
			return -1;
		}
		return map[oldIndex];
	}

	/// <summary>
	/// The same set of indices, moved: every index of <paramref name="old"/> replaced by where it went, and the ones that
	/// left the plan dropped. A set that held <c>{2, 7}</c> of the old plan holds their new positions - never <c>-1</c>,
	/// because a set of indices that no longer means a piece must lose the entry, not keep a hole.
	/// </summary>
	internal static HashSet<int> RemapAll(int[] map, IEnumerable<int> old)
	{
		HashSet<int> moved = new HashSet<int>();
		if (old == null)
		{
			return moved;
		}
		foreach (int index in old)
		{
			int at = Remap(map, index);
			if (at >= 0)
			{
				moved.Add(at);
			}
		}
		return moved;
	}
}
