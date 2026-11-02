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

	/// <summary>
	/// E6b (player's request, 02/11/2026): <b>takes from the chests into the bag before going out to gather</b> - the wood
	/// in a chest is wood the house can use, and the player must not watch it chop while a chest still holds 30. It moves
	/// up to the piece's need per item (for a plan, the whole structure's), leaves the rest where it is and logs what it
	/// took.
	/// </summary>
	internal static int Withdraw(Inventory bag, Piece piece, Vector3 center, Component who, bool useChests)
	{
		if (bag == null || piece == null || piece.m_resources == null)
		{
			return 0;
		}
		Dictionary<string, int> need = new Dictionary<string, int>();
		foreach (Piece.Requirement requirement in piece.m_resources)
		{
			if (requirement == null || requirement.m_resItem == null)
			{
				continue;
			}
			string name = requirement.m_resItem.m_itemData.m_shared.m_name;
			int amount = requirement.GetAmount(1);
			int seen;
			need.TryGetValue(name, out seen);
			need[name] = seen + amount;
		}
		return MoveFromChests(bag, need, center, who, useChests);
	}

	/// <summary>E6b: the same for a whole structure - the need is the sum of every piece of the template.</summary>
	internal static int WithdrawTemplate(Inventory bag, BuildTemplates.Template template, Vector3 center, Component who, bool useChests)
	{
		if (bag == null || template == null)
		{
			return 0;
		}
		Dictionary<string, int> need = new Dictionary<string, int>();
		foreach (BuildTemplates.Step step in template.Steps)
		{
			Piece piece = BuildProbe.PieceOf(step.Prefab);
			if (piece == null || piece.m_resources == null)
			{
				continue;
			}
			foreach (Piece.Requirement requirement in piece.m_resources)
			{
				if (requirement == null || requirement.m_resItem == null)
				{
					continue;
				}
				string name = requirement.m_resItem.m_itemData.m_shared.m_name;
				int amount = requirement.GetAmount(1);
				int seen;
				need.TryGetValue(name, out seen);
				need[name] = seen + amount;
			}
		}
		return MoveFromChests(bag, need, center, who, useChests);
	}

	/// <summary>Moves what the chests hold into the bag, up to the need (what the bag already has counts).</summary>
	private static int MoveFromChests(Inventory bag, Dictionary<string, int> need, Vector3 center, Component who, bool useChests)
	{
		if (bag == null || need == null || !useChests)
		{
			return 0;
		}
		List<Container> chests = Chests(center, who);
		if (chests.Count == 0)
		{
			return 0;
		}
		int moved = 0;
		foreach (KeyValuePair<string, int> kv in need)
		{
			string name = kv.Key;
			int left = kv.Value - bag.CountItems(name);
			if (left <= 0)
			{
				continue;
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
				GameObject prefab = PrefabOf(name);
				if (prefab == null)
				{
					continue;
				}
				MHR_Chests.Claim(chest);
				chestBag.RemoveItem(name, take);
				bag.AddItem(prefab, take);
				left -= take;
				moved += take;
				CompanionsPlugin.Log?.LogInfo("[Build] stock: moved " + take + "x " + name + " from the chest \"" + chest.gameObject.name
					+ "\" to the bag (" + Vector3.Distance(chest.transform.position, center).ToString("F0") + " m from the spot)");
			}
		}
		return moved;
	}

	/// <summary>The prefab for a shared item name (the item the chest holds, so the bag takes the same object).</summary>
	private static GameObject PrefabOf(string sharedName)
	{
		if (string.IsNullOrEmpty(sharedName) || ObjectDB.instance == null || ObjectDB.instance.m_items == null)
		{
			return null;
		}
		foreach (GameObject item in ObjectDB.instance.m_items)
		{
			ItemDrop drop = item != null ? item.GetComponent<ItemDrop>() : null;
			if (drop != null && drop.m_itemData != null && drop.m_itemData.m_shared != null && drop.m_itemData.m_shared.m_name == sharedName)
			{
				return item;
			}
		}
		return null;
	}

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
				// same reason as the chest line below: the player must be able to SEE where the material came from
				CompanionsPlugin.Log?.LogInfo("[Build] stock: took " + fromBag + "x " + name + " from the bag");
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
				// E6b (player's test, 01/11/2026): it must be visible WHERE the wood came from - "took it from the
				// chest" was a guess because this was silent
				CompanionsPlugin.Log?.LogInfo("[Build] stock: took " + take + "x " + name + " from the chest \"" + chest.gameObject.name
					+ "\" (" + Vector3.Distance(chest.transform.position, center).ToString("F0") + " m from the spot)");
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
