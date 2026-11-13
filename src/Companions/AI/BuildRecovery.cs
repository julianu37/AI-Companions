using System.Collections.Generic;

namespace Companions;

/// <summary>
/// 13/11/2026 (the solo run of <c>CommHomeBasicBP</c>, measured: <b>983/985</b> and <c>hc_ai_blueprint_done</c>; the player's
/// rule over the screenshot: <i>"peças faltando na construção não será tolerado"</i>): <b>a piece the order gave up comes back
/// to the plan until it stands</b>.
///
/// <para>The defect it fixes is not this blueprint's, it is the plan's own shape: <c>MHR_Build.Advance</c> is a
/// <b>forward-only</b> scan (<c>next = state.Index + 1</c>), and the only way out of <c>SkippedIndices</c> is a piece that was
/// already <b>placed</b> (<c>CountPlaced</c>). So a piece that spent its three attempts was out of the plan <b>for good</b> —
/// even when the reason it failed stopped being true two hundred pieces later. The two floors of that run are the case: they
/// failed with <c>no progress</c> (the body was one storey below, <c>route partial</c>, the way up not built yet), and the
/// house that finished around them is exactly what would have held them — the player read it from the screenshot before the
/// log was read: <i>"aqueles dois ali que faltaram não iriam cair se ele tivesse colocado, pois estariam ancorados nos das
/// laterais"</i>.</para>
///
/// <para>The rule: when the walk reaches the <b>end of the plan</b> with pieces given up, they come back, in plan order
/// (lowest index first — for a blueprint that is bottom-up), with the order's self-healing budget for them <b>re-armed</b>:
/// the support pull, the emergency post and the pose adjustment they may have already spent are fresh again. That is what
/// makes the second attempt a better attempt instead of a copy of the first — the house around them changed, and the tools
/// that were denied are allowed again.</para>
///
/// <para><b>When it stops:</b> a pass that places <b>nothing</b>. That is the same instrument the stall watchdog uses
/// (<c>MHR_Build.StallSeconds</c>): a plan that advances converges, and one that does not would loop forever. Every pass that
/// places at least one piece earns the next, so a house with ten unreachable pieces recovers them across passes as its
/// structure closes, and a piece the world genuinely cannot take ends the order with the number in the line instead of
/// holding the companion there for ever.</para>
///
/// <para>Pure on purpose (counts and indexes in, a verdict out): the harness pins the table (section (am)) and
/// <c>MHR_Build.RecoverSkipped</c> adds only the world - the step object, the counters and the site.</para>
/// </summary>
internal static class BuildRecovery
{
	/// <summary>
	/// How many recovery passes one order may run. Two is what makes the measured failure converge: the first pass is what the
	/// run of 13/11/2026 was missing (the house finished around two floors that had already been given up), and the second
	/// catches a piece whose support only came up during the first. It is also the cap that bounds the plan's growth - each
	/// pass appends the skipped pieces to the walk.
	/// </summary>
	internal const int MaxPasses = 2;

	/// <summary>
	/// May a recovery pass run <b>now</b>? Three conditions, and each one closes a different hole:
	///
	/// <para><b>Only a blueprint</b> - a template's pieces are its own list and a single piece has no plan to come back to.</para>
	///
	/// <para><b>Something was given up</b> - with nothing skipped there is nothing to bring back.</para>
	///
	/// <para><b>The last pass placed something</b> (<paramref name="lastPassPlaced"/>, <c>-1</c> = no pass ran yet) - a pass
	/// that placed nothing found the pieces it cannot place, and repeating it would be the same walk to the same spots for
	/// ever. This is the convergence condition: every pass that places at least one piece earns the next, so a house with
	/// several unreachable pieces recovers them across passes as its structure closes.</para>
	///
	/// <para><b>At most <see cref="MaxPasses"/></b> - a pass appends its pieces to the plan (the walk never goes back on its
	/// own), so the plan grows by the skipped set each time. The cap is what keeps that bounded: with a thousand pieces the
	/// worst case is a plan three times the size, never an order that never ends.</para>
	/// </summary>
	internal static bool Due(bool blueprint, int skipped, int passes, int lastPassPlaced)
	{
		if (!blueprint || skipped <= 0 || passes < 0 || passes >= MaxPasses)
		{
			return false;
		}
		return passes == 0 || lastPassPlaced > 0;
	}

	/// <summary>
	/// The plan indexes to bring back, <b>lowest first</b> (plan order, which for a blueprint is bottom-up), without repeats
	/// and without negatives, at most <paramref name="max"/> (<c>&lt;= 0</c> = no cap). A piece that is <b>up</b> is not
	/// missing and is never brought back: the two sets are disjoint by construction (<c>CountPlaced</c> takes an index out of
	/// the skipped one), but a stale entry from a site that outlived a worker can still be there.
	///
	/// <para>The order is not cosmetic: recovering a roof floor before the storey under it repeats the very failure that gave
	/// it up, and the player's rule is the reason the whole plan is ordered that way.</para>
	/// </summary>
	internal static List<int> Select(IEnumerable<int> skipped, ISet<int> placed, int max)
	{
		List<int> chosen = new List<int>();
		if (skipped == null)
		{
			return chosen;
		}
		foreach (int index in skipped)
		{
			if (index < 0 || chosen.Contains(index) || SettledPieces.Out(index, placed, null))
			{
				continue;
			}
			chosen.Add(index);
		}
		chosen.Sort();   // plan order: for a blueprint that is bottom-up, and a support never comes after what it holds
		if (max > 0 && chosen.Count > max)
		{
			chosen.RemoveRange(max, chosen.Count - max);
		}
		return chosen;
	}
}
