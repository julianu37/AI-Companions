using System;
using System.Collections.Generic;
using UnityEngine;

namespace Companions;

/// <summary>
/// Fase E4: the <b>inventory manager</b> of a build order - what the piece needs, what the companion already has
/// (its own bag, then the chests it may use) and what is still missing.
///
/// <para>The rules are the mod's own, not a new set: a chest is usable when <see cref="MHR_Chests.Usable"/> says so
/// (a storage chest a player built, inside a ward that lets the companion in, not someone else's private chest, not
/// open) and the search radius is the same kind of leash the other jobs use. Nothing here guesses a cost: it is
/// always <c>Piece.m_resources</c> and <c>Piece.Requirement.GetAmount(1)</c>.</para>
///
/// <para><b>Taking is all-or-nothing</b> (<see cref="Take"/>): the bag first, then the chests, and only when the
/// whole cost is there - the same rule the BuildOrders API uses ("no building resources are consumed until the
/// ghosts are built"). A chest is claimed before its contents change, because the game only saves a chest's
/// contents on its owner (the reason <see cref="MHR_Chests.Claim"/> exists).</para>
/// </summary>
internal static class BuildStock
{
	/// <summary>How far from the piece's spot the chests are looked for (the same kind of leash the other jobs use).</summary>
	internal const float ChestRadius = 30f;

	private static readonly List<Container> s_chests = new List<Container>();

	/// <summary>What the piece needs, counting the bag and the usable chests near <paramref name="center"/>.</summary>
	internal static Dictionary<string, int> Available(Inventory bag, Piece piece, Vector3 center, Component who, bool useChests)
	{
		Dictionary<string, int> have = new Dictionary<string, int>();
		if (piece == null || piece.m_resources == null)
		{
			return have;
		}
		AddFrom(have, bag);
		if (useChests)
		{
			foreach (Container chest in Chests(center, who))
			{
				AddFrom(have, chest != null ? chest.GetInventory() : null);
			}
		}
		return have;
	}

	/// <summary>The shortfall per item (empty when the bag + the chests cover the piece).</summary>
	internal static Dictionary<string, int> Missing(Inventory bag, Piece piece, Vector3 center, Component who, bool useChests)
	{
		Dictionary<string, int> missing = new Dictionary<string, int>();
		if (piece == null || piece.m_resources == null)
		{
			return missing;
		}
		Dictionary<string, int> have = Available(bag, piece, center, who, useChests);
		foreach (Piece.Requirement requirement in piece.m_resources)
		{
			if (requirement == null || requirement.m_resItem == null)
			{
				continue;
			}
			string name = requirement.m_resItem.m_itemData.m_shared.m_name;
			int need = requirement.GetAmount(1);
			if (need <= 0)
			{
				continue;
			}
			int got;
			have.TryGetValue(name, out got);
			if (got < need)
			{
				missing[name] = need - got;
			}
		}
		return missing;
	}

	/// <summary>
	/// Pays the piece: the bag first, then the usable chests, all or nothing. False (and nothing taken) when the
	/// whole cost is not there - the caller answers "we're missing X" instead of half paying.
	/// </summary>
	internal static bool Take(Inventory bag, Piece piece, Vector3 center, Component who, bool useChests, out Dictionary<string, int> missing)
	{
		missing = Missing(bag, piece, center, who, useChests);
		if (missing.Count > 0)
		{
			return false;
		}
		if (piece == null || piece.m_resources == null)
		{
			return true;
		}
		List<Container> chests = useChests ? Chests(center, who) : new List<Container>();
		foreach (Piece.Requirement requirement in piece.m_resources)
		{
			if (requirement == null || requirement.m_resItem == null)
			{
				continue;
			}
			string name = requirement.m_resItem.m_itemData.m_shared.m_name;
			int left = requirement.GetAmount(1);
			if (left <= 0)
			{
				continue;
			}
			int fromBag = bag != null ? Mathf.Min(left, bag.CountItems(name)) : 0;
			if (fromBag > 0)
			{
				bag.RemoveItem(name, fromBag);
				left -= fromBag;
			}
			foreach (Container chest in chests)
			{
				if (left <= 0)
				{
					break;
				}
				Inventory chestBag = chest != null ? chest.GetInventory() : null;
				if (chestBag == null)
				{
					continue;
				}
				int take = Mathf.Min(left, chestBag.CountItems(name));
				if (take <= 0)
				{
					continue;
				}
				MHR_Chests.Claim(chest);
				chestBag.RemoveItem(name, take);
				left -= take;
			}
			if (left > 0)
			{
				// the chests changed between the count and the take: put the bag's part back, nothing is half paid
				if (fromBag > 0)
				{
					bag.AddItem(requirement.m_resItem.gameObject, fromBag);
				}
				missing = new Dictionary<string, int> { { name, left } };
				return false;
			}
		}
		return true;
	}

	/// <summary>The usable chests around a point (the mod's rules, the same list the jobs use).</summary>
	internal static List<Container> Chests(Vector3 center, Component who)
	{
		s_chests.Clear();
		foreach (Container chest in MHR_Chests.Near(center, ChestRadius))
		{
			if (MHR_Chests.Usable(chest, who))
			{
				s_chests.Add(chest);
			}
		}
		return s_chests;
	}

	/// <summary>The gather mode that produces an item, or 0 when the item is not something a job collects.</summary>
	internal static int GatherMode(string sharedName)
	{
		if (string.IsNullOrEmpty(sharedName))
		{
			return 0;
		}
		string name = sharedName.ToLowerInvariant();
		if (name.Contains("wood") || name.Contains("finewood") || name.Contains("roundlog"))
		{
			return CompanionSetup.ModeGatherWood;
		}
		if (name.Contains("stone") || name.Contains("flint") || name.Contains("grausten") || name.Contains("blackmarble"))
		{
			return CompanionSetup.ModeGatherStone;
		}
		if (name.Contains("copper") || name.Contains("tin") || name.Contains("iron") || name.Contains("silver")
			|| name.Contains("blackmetal") || name.Contains("scrap") || name.Contains("ore"))
		{
			return CompanionSetup.ModeGatherOre;
		}
		return 0;
	}

	private static void AddFrom(Dictionary<string, int> have, Inventory inventory)
	{
		if (inventory == null || have == null)
		{
			return;
		}
		foreach (ItemDrop.ItemData item in inventory.GetAllItems())
		{
			if (item == null || item.m_shared == null)
			{
				continue;
			}
			string name = item.m_shared.m_name;
			int count;
			have.TryGetValue(name, out count);
			have[name] = count + item.m_stack;
		}
	}
}
