using System.Collections.Generic;

namespace Companions;

/// <summary>
/// 13/11/2026 (player: *"mesmo assim, ele deveria continuar construindo depois de tirar um pouco de itens/peso da
/// bag dele"*): an <b>encumbered</b> companion used to <b>end</b> the build order (<c>hc_ai_build_too_heavy</c>) the
/// moment the bag passed the carry ceiling - measured in the solo run of 12/11/2026, where the companion walked to its
/// own tombstone, collected the items and the order died at 851/985 with 470/300. Ending is the wrong verdict: nothing
/// about the house changed, the piece is still there, and the bag is the companion's own to unload.
///
/// <para>This is the pure half of the rule that replaces that ending: <b>which stacks leave the bag</b>. The caller
/// walks the answer and drops each stack (the game's own <c>ItemDrop.DropItem</c>, with auto-pickup off so the bag
/// does not take it straight back - the same mechanics <c>CompanionOrders.DropNamed</c> uses), then the build goes on
/// at the same piece.</para>
///
/// <para><b>What is never dropped:</b> equipment and gear (weapons, tools, shields, torches, armour, ammo, utility) -
/// a pickaxe on the ground is a job the companion cannot do - food, and quest items (the game refuses those anyway,
/// <c>$msg_cantdrop</c>). <b>What is dropped first:</b> the <b>structural</b> materials the house itself pays for
/// (wood, stone, and what <c>BuildStock</c> can take back from the ground) - a stock the build can pick up again is
/// not a loss, a gem or a trophy is worth more than the shelf it sits on. Then everything else by <b>heaviest
/// first</b>, so the fewest stacks come out.</para>
///
/// <para><b>Enough is enough:</b> the walk stops as soon as the bag is under the ceiling minus a margin
/// (<see cref="Slack"/>) - unloading 470 to 299 would put the companion straight back over the line on the next
/// <c>Take</c>, and the loop would unload again every piece. The margin is what the heaviest piece of the plan
/// costs, capped by <see cref="MaxSlack"/>, so a full storey of floors still fits.</para>
/// </summary>
internal static class BuildOverweight
{
	/// <summary>Items whose weight the companion carries anyway: <b>never</b> dropped for the build. The structural
	/// materials (wood/stone) are deliberate exceptions - they are what the house consumes.</summary>
	internal static bool Keeps(string prefab, bool equipable, bool quest, bool food, bool material)
	{
		if (quest || food || equipable)
		{
			return true;   // the game refuses the first, a hungry companion cannot build, and gear is the job itself
		}
		return !material;   // anything that is not a building material stays (gems, trophies, resin, trophies...)
	}

	/// <summary>"Is this stack worth taking to make room?" - the two questions the walk order needs are the weight and
	/// whether it may go; a zero-weight or kept stack never enters the list.</summary>
	internal static bool Drops(float weight, bool keeps)
	{
		return !keeps && weight > 0f;
	}

	/// <summary>
	/// The stacks to drop, as indices into <paramref name="weights"/>, in the order they leave the bag: the
	/// <b>structural materials last</b> (they are the build's own stock) and, inside each group, the <b>heaviest
	/// first</b>. Stops as soon as <paramref name="weight"/> would fall to <paramref name="target"/>.
	///
	/// <para><b>All or nothing.</b> When even dropping every stack it is allowed to drop cannot reach
	/// <paramref name="target"/>, the answer is <b>empty</b>: the companion is still over the ceiling either way, and
	/// the only thing the partial walk would buy is its own wood on the ground (470 to 357 is not "unencumbered", it
	/// is a house that lost its material). An empty answer is the caller's cue to keep the bag and wait or say so.</para>
	/// </summary>
	internal static List<int> Plan(IList<float> weights, IList<bool> keeps, IList<bool> material, float weight, float target)
	{
		List<int> order = new List<int>();
		if (weights == null || weight <= target)
		{
			return order;   // already under the line: no stack has to go
		}
		List<int> gems = new List<int>();
		List<int> stock = new List<int>();
		for (int i = 0; i < weights.Count; i++)
		{
			bool kept = keeps == null || i >= keeps.Count || keeps[i];
			float w = weights[i];
			if (!Drops(w, kept))
			{
				continue;
			}
			bool isMaterial = material != null && i < material.Count && material[i];
			(isMaterial ? stock : gems).Add(i);
		}
		HeaviestFirst(gems, weights);
		HeaviestFirst(stock, weights);
		float left = weight;
		Append(order, gems, weights, ref left, target);
		Append(order, stock, weights, ref left, target);
		if (left > target)
		{
			order.Clear();   // still over the ceiling after everything allowed: the bag stays as it is
		}
		return order;
	}

	private static void Append(List<int> order, List<int> from, IList<float> weights, ref float left, float target)
	{
		for (int i = 0; i < from.Count && left > target; i++)
		{
			int index = from[i];
			order.Add(index);
			left -= weights[index];
		}
	}

	/// <summary>Heaviest first, stable for equal weights (the plan's order is the tie-break, so the same bag always
	/// sheds the same stacks - a test can assert the exact list).</summary>
	private static void HeaviestFirst(List<int> list, IList<float> weights)
	{
		list.Sort(delegate (int a, int b)
		{
			int byWeight = weights[b].CompareTo(weights[a]);
			return byWeight != 0 ? byWeight : a.CompareTo(b);
		});
	}

	/// <summary>How far under the ceiling the bag has to end: the heaviest piece of the plan, capped. Below that the
	/// very next <c>Take</c> puts the companion overweight again and the order unloads on every piece.</summary>
	internal static float Slack(float heaviestPiece, float cap)
	{
		if (heaviestPiece <= 0f)
		{
			return 0f;
		}
		return heaviestPiece < cap ? heaviestPiece : cap;
	}

	/// <summary>The ceiling with the slack already discounted - what <see cref="Plan"/> is asked to reach.</summary>
	internal static float Target(float ceiling, float slack)
	{
		float target = ceiling - (slack > 0f ? slack : 0f);
		return target > 0f ? target : 0f;
	}

	/// <summary>P1: never more than this many seconds waiting for the bag to come down by itself before the order
	/// says so and lets the player act (a companion holding still forever is a silent order).</summary>
	internal const float MaxWait = 30f;

	/// <summary>The cap of <see cref="Slack"/>: one full stack of the heaviest building material (wood, 50 x 0,5 =
	/// 25; stone 50 x 2 = 100) would be too much, two floors (2 x 1,5 = 3) is exactly what the piece needs.</summary>
	internal const float MaxSlack = 20f;
}
