using System.Collections.Generic;

namespace Companions;

/// <summary>
/// 12/11/2026 (the solo run of <c>CommHomeBasicBP</c>, the player's report over three screenshots: "he is climbing onto the
/// floor at the end of the stair but then he keeps walking and falling instead of laying the floors ahead of him", and
/// "he still had not laid all the supports under the upper floor before climbing"): <b>which floors of a landing may be
/// brought to the front of the plan</b>.
///
/// <para><c>MHR_Build.PullFloorsAbove</c> puts the floors that land on a <c>ladder</c> right after it so the ladder leads
/// somewhere. It picked them by distance alone - the two nearest, whatever is under them. <c>BuildBlueprintParser.ClassOf</c>
/// sorts a <c>stair</c>/<c>ladder</c> (class 1) <b>before</b> the beams and poles of the same storey (class 2), so when the
/// ladder goes up the pieces that hold the upper floor can still be <b>ahead</b> in the plan: the pull moved the floor in
/// front of its own support, the placement came back <c>no_support</c>, the emergency post planted a beam the blueprint does
/// not have, and with that post the ladder finally had a landing - the companion climbed onto a floor that only existed
/// because the safety net had caught it, walked to the edge and fell. Measured in that log: 29 <c>no_support</c>, 15
/// emergency posts, and the pull firing at plan indices 579, 598, 1484 and 1565.</para>
///
/// <para>The rule: a floor is brought forward <b>only when what holds it is already standing</b>. The pull is then what it
/// was meant to be - the landing of a ladder that already has structure under it - and a floor whose support is still ahead
/// keeps the storey order, where the beams go up first and the floor is laid from the storey below (one metre above the
/// feet, inside the placement reach).</para>
///
/// <para>Pure (indexes and flags, no Unity, no state): <c>MHR_Build</c> answers "what holds it is up" for each candidate and
/// this decides. The harness asserts it (section (ak)).</para>
/// </summary>
internal static class LandingFloors
{
    /// <summary>
    /// The floors to bring forward, from <paramref name="candidates"/> (plan indexes, nearest to the ladder first), with
    /// <paramref name="supported"/> giving, for each of them, "what holds it is already standing". Keeps the order it was
    /// given - the nearest landing first - and stops at <paramref name="max"/>.
    ///
    /// <para>A candidate without a standing support is <b>skipped</b>, not counted against the budget: with two supported
    /// ones after an unsupported one, the two still come forward. A repeated or negative index is ignored (the plan holds a
    /// step once), and a <paramref name="max"/> below one pulls nothing.</para>
    /// </summary>
    internal static List<int> Select(IList<int> candidates, IList<bool> supported, int max)
    {
        List<int> chosen = new List<int>();
        if (candidates == null || supported == null || max <= 0)
        {
            return chosen;
        }
        int count = candidates.Count < supported.Count ? candidates.Count : supported.Count;
        for (int i = 0; i < count && chosen.Count < max; i++)
        {
            int index = candidates[i];
            if (!supported[i] || index < 0 || chosen.Contains(index))
            {
                continue;
            }
            chosen.Add(index);
        }
        return chosen;
    }
}
