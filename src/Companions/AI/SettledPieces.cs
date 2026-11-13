using System.Collections.Generic;

namespace Companions;

/// <summary>
/// 12/11/2026 (the solo run of <c>CommHomeBasicBP</c>, measured: 29 <c>no_support</c> and 15 emergency posts while the
/// same blueprint finished with two workers): <b>which plan indexes count as "already settled"</b> when the builder looks
/// for the piece that holds another one up.
///
/// <para>The defect it fixes is one line of <c>MHR_Build.DirectSupportIndex</c>:
/// <c>MHR_BuildGroup.IsDone(state.Group, i)</c>. <c>MHR_BuildGroup.IsDone</c> answers <c>false</c> for a null site key
/// (<c>MHR_BuildGroup.cs:654</c>), and the solo order has no site - so a piece that is <b>already standing</b> stayed a
/// candidate support, and the search could pick it and send the companion back to a spot with nothing left to build. The
/// two paths keep their indexes in different places: a site has <c>Done</c>/<c>SkippedIndex</c> keyed by plan position, a
/// single worker has its own <c>PlacedIndices</c>/<c>SkippedIndices</c>.</para>
///
/// <para>Pure on purpose (indexes and two sets in, a verdict out): the harness feeds it both paths with the same numbers
/// (section (ak)), which is how the rule is stated - <b>a given plan must give the same answer whether the companion is
/// alone or in a crew</b>.</para>
/// </summary>
internal static class SettledPieces
{
    /// <summary>
    /// Is the plan index <paramref name="index"/> out of the running as a support? It is when it is <b>up</b> (a piece that
    /// already stands cannot hold anything up anymore - the game's own rule is about what is still to be built) or
    /// <b>given up</b> (it never will be). A null set is an empty set - a worker that has placed nothing yet - and an index
    /// below zero names no plan step, so it is out too.
    ///
    /// <para>What the sets mean is the caller's business: the site's <c>Done</c>/<c>SkippedIndex</c> in a crew, the worker's
    /// <c>PlacedIndices</c>/<c>SkippedIndices</c> for a single companion.</para>
    /// </summary>
    internal static bool Out(int index, ISet<int> placed, ISet<int> skipped)
    {
        if (index < 0)
        {
            return true;
        }
        return (placed != null && placed.Contains(index)) || (skipped != null && skipped.Contains(index));
    }
}
