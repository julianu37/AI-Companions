namespace Companions;

/// <summary>
/// 16/11/2026 (the false <c>hc_ai_blueprint_incomplete</c> of the run of 16:57): <b>how many pieces the house has</b> is
/// the size of the plan when the order started, never the size of the list now.
///
/// <para>The single worker's retry pass <b>appends</b> to the same plan the pieces that did not stand yet
/// (<c>MHR_Build.Advance</c>: <c>state.Plan.AddRange(again)</c>), on purpose - the step object is the piece's identity, so
/// appending the same object lets the walk come back to the exact piece without a remap. What it also does is make
/// <c>Plan.Count</c> grow: the measured run of 16/11/2026 (the one that put the doors in their place, 7 of 985 as the
/// <b>last</b> pieces) had 6 pieces fail to stand on the first pass, the retry pass appended them, and the order closed
/// with <c>Plan.Count = 991</c>.</para>
///
/// <para>The completion test compares the placed count against this number - and the placed count holds the <b>first</b>
/// index of each step (<c>MHR_Build.PieceIndex</c>: "the copy at the end is not another piece"), so it tops out at 985.
/// Comparing 985 against 991 ended the order claiming <c>6 peça(s) não subiram</c> about six pieces <b>the same log had just
/// found standing</b>, one line per piece: <c>a peça 986/991 já está de pé (outra tentativa a deixou lá) - seguindo</c>.
/// The player's rule is that a build with a piece missing never closes as done - and the mirror of that rule is that a
/// build with <b>no</b> piece missing never closes as unfinished.</para>
///
/// <para>Pure on purpose (numbers in, a verdict out): the harness pins the table (section (aq)) and
/// <c>MHR_Build.Finish</c> adds only the two counters.</para>
/// </summary>
internal static class BuildPlanTotal
{
	/// <summary>
	/// The size of the house: <paramref name="planTotal"/> (the plan's count when the order started) when it is known, and
	/// the list's current size only as the fallback for an order that has no plan of its own (a template, a single piece).
	/// Never below 1 - the callers divide by it for the report.
	/// </summary>
	internal static int Of(int planTotal, int planCount)
	{
		int size = planTotal > 0 ? planTotal : planCount;
		return size > 0 ? size : 1;
	}

	/// <summary>
	/// Did every piece of the house end up standing? It is the only condition under which a blueprint may close as
	/// <c>done</c> - and, read the other way, the reason a run must never close as <c>incomplete</c> with the count at the
	/// plan's own size.
	/// </summary>
	/// <param name="placed">Pieces standing, counted by the <b>first</b> plan index of each step.</param>
	/// <param name="planned">The size of the house - <see cref="Of"/>, never the grown list.</param>
	internal static bool Complete(int placed, int planned)
	{
		return planned > 0 && placed >= planned;
	}
}
