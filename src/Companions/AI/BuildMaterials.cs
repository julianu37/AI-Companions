using System;
using System.Collections.Generic;

namespace Companions;

/// <summary>
/// One cost line of a piece - the pair the whole build layer counts with: the <b>shared item name</b> (the key the
/// game localizes, <c>Piece.Requirement.m_resItem.m_itemData.m_shared.m_name</c>) and how many of it.
/// </summary>
internal struct Cost
{
	internal string Item;
	internal int Amount;
}

/// <summary>
/// The <b>material need</b> of a plan - the pure half of what <c>BuildStock</c> and <c>MHR_Build.TemplateMissing</c>
/// already do one piece at a time, shared so the build panel can ask "how much does this house need" without a second
/// set of rules.
///
/// <para>The rules are the ones of the order, not a new set: a cost is <c>requirement.GetAmount(1)</c> (the amount a
/// single piece takes, never the stack size) keyed by the shared item name, and an entry with no item or with
/// <c>amount &lt;= 0</c> is ignored - exactly like <c>BuildStock.Withdraw</c>/<c>MHR_Build.PlanMissing</c> do.</para>
///
/// <para>Unity-free and stateless on purpose: the harness links this file (section (w)) and the panel calls
/// <see cref="Total"/> every couple of seconds, so nothing here may keep state between calls.</para>
/// </summary>
internal static class BuildMaterials
{
	/// <summary>
	/// Adds <paramref name="count"/> copies of one piece's costs to <paramref name="total"/>. Returns how many entries
	/// were <b>ignored</b> for being invalid (no item name, or an amount that is not positive) - a piece counted zero
	/// times is not an invalid entry, it simply adds nothing.
	/// </summary>
	internal static int Add(Dictionary<string, int> total, IReadOnlyList<Cost> costs, int count)
	{
		if (total == null || costs == null || count <= 0)
		{
			return 0;
		}
		int ignored = 0;
		for (int i = 0; i < costs.Count; i++)
		{
			Cost cost = costs[i];
			if (string.IsNullOrEmpty(cost.Item) || cost.Amount <= 0)
			{
				ignored++;
				continue;
			}
			int seen;
			total.TryGetValue(cost.Item, out seen);
			total[cost.Item] = seen + cost.Amount * count;
		}
		return ignored;
	}

	/// <summary>
	/// The whole need of a plan: <paramref name="pieces"/> is prefab -&gt; how many of it the plan builds, and
	/// <paramref name="costsOf"/> answers one piece's own costs (the caller resolves the prefab; a prefab that is not a
	/// buildable piece here answers null/empty). <paramref name="noRecipe"/> counts the pieces that answered nothing -
	/// the honest number behind "this plan needs N materials it does not know".
	/// </summary>
	internal static Dictionary<string, int> Total(IEnumerable<KeyValuePair<string, int>> pieces,
		Func<string, IReadOnlyList<Cost>> costsOf, out int noRecipe)
	{
		noRecipe = 0;
		Dictionary<string, int> total = new Dictionary<string, int>();
		if (pieces == null)
		{
			return total;
		}
		foreach (KeyValuePair<string, int> piece in pieces)
		{
			if (piece.Value <= 0)
			{
				continue;   // a piece the plan does not build costs nothing
			}
			IReadOnlyList<Cost> costs = costsOf != null ? costsOf(piece.Key) : null;
			if (costs == null || costs.Count == 0)
			{
				noRecipe++;
				continue;
			}
			Add(total, costs, piece.Value);
		}
		return total;
	}
}
