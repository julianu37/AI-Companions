using System.Collections.Generic;
using UnityEngine;

namespace Companions;

/// <summary>
/// The <b>effective carry weight</b> of a companion: the mod's own base ceiling (<c>[General] MaxCarryWeight</c>,
/// 300 by default) plus whatever the items it is carrying add - the Megingjord belt and anything else built the same
/// way in the game.
///
/// <para><b>Why the mod has its own ceiling.</b> Vanilla only limits a <see cref="Player"/>: <c>Inventory.CanAddItem</c>
/// checks grid space alone (there is no <c>Inventory.m_maxWeight</c> in 1.0) and <c>Encumbered</c> is applied by us in
/// <see cref="CompanionSetup"/>'s update. So the belt's +150 did nothing for a companion: the game never asked
/// <c>Player.GetMaxCarryWeight</c>, and that is the only consumer of the bonus (see the chain below).</para>
///
/// <para><b>The chain in the game</b> (decompiled 1.0, <c>assembly_valheim</c>):</para>
/// <code>
/// BeltStrength (prefab, ItemType.Utility)
///   └ ItemDrop.ItemData.m_shared.m_equipStatusEffect : StatusEffect   (public field)
///       └ asset SE_Stats "BeltStrength"
///           └ SE_Stats.m_addMaxCarryWeight : float = 150             (public field, serialized in the asset)
///               └ SE_Stats.ModifyMaxCarryWeight() → SEMan.ModifyMaxCarryWeight(...)
///                   └ Player.GetMaxCarryWeight()                     (the only consumer)
/// </code>
/// <para>Two consequences this file lives by:</para>
/// <list type="bullet">
/// <item><b>the number comes from the asset</b>, never from our code - it is read from
/// <c>((SE_Stats)shared.m_equipStatusEffect).m_addMaxCarryWeight</c>, so a modded belt with +250 works too and a
/// rebalanced Megingjord keeps working;</item>
/// <item><b>it never stacks</b>: the game deduplicates equipment effects by <b>asset reference</b> (the
/// <c>HashSet&lt;StatusEffect&gt;</c> of <c>Humanoid.UpdateEquipmentStatusEffects</c> and the hash early-return in
/// <c>SEMan.AddStatusEffect</c>), so two belts give +150 once - and so do we, by the same rule.</item>
/// </list>
///
/// <para><b>Product decision (the bug report this fixes).</b> The bonus counts with the item <b>anywhere in the
/// companion's inventory</b> - backpack or equipped - because that is what the player means by "giving the belt to the
/// companion", and because <c>m_utilityItem</c> is not persisted for a <see cref="Humanoid"/> that is not a
/// <see cref="Player"/> (equipping it would be lost on reload). Nothing here equips the item, and
/// <see cref="CompanionSetup.AutoEquipBest"/> is untouched.</para>
///
/// <para><b>Cache (per inventory).</b> <c>Inventory.GetAllItems()</c> builds a brand new <c>List</c> on every call and
/// the inventory list itself is protected (<c>m_inventory</c>), so there is no allocation-free way to walk it. This
/// walk runs in <see cref="CompanionSetup"/>'s per-frame update and in the overhead HUD (<c>EnemyHudPatches</c>, which
/// can run many times per frame per companion), so the result is cached per <see cref="Inventory"/> and only rescanned
/// when <c>NrOfItems()</c> or <c>GetTotalWeight()</c> change - both are plain counter/sum loops with no allocation -
/// and never more than once every <see cref="RefreshInterval"/> seconds. The common path (no belt) therefore costs
/// two cheap getters and <b>zero</b> allocations per frame.</para>
///
/// <para>Main thread only: it reads <see cref="Inventory"/>/<see cref="Humanoid"/> and logs.</para>
/// </summary>
internal static class CompanionCarry
{
	/// <summary>The base ceiling, i.e. <c>[General] MaxCarryWeight</c>. The <b>effective</b> ceiling also includes
	/// <see cref="Bonus"/> - use <see cref="Max(Inventory)"/> and friends, not this.</summary>
	internal static float Base => CompanionTierData.MaxCarryWeight;

	/// <summary>How long a cached bonus is trusted before the items are walked again (seconds).</summary>
	private const float RefreshInterval = 0.5f;

	/// <summary>Safety net: even with the same item count and the same weight (an item swapped for another of equal
	/// weight), the inventory is rescanned this often. Cheap (one list per companion per interval) and it keeps a
	/// same-weight swap from pinning a stale bonus forever.</summary>
	private const float FullRescanInterval = 10f;

	/// <summary>How many inventories are remembered. One per companion inventory (the ceiling is 10 companions), with
	/// room to spare; the oldest slot is simply reused beyond that.</summary>
	private const int MaxEntries = 12;

	private struct Snapshot
	{
		public Inventory Inv;

		/// <summary>The last Humanoid seen with this inventory: only used to name the companion in the change log.</summary>
		public Humanoid Owner;

		public int Items;

		public float Weight;

		public float Bonus;

		/// <summary>Earliest time of the next (allocating) scan.</summary>
		public float NextAllowed;

		/// <summary>Time of the next unconditional rescan.</summary>
		public float NextFull;

		public bool Known;
	}

	private static readonly Snapshot[] _entries = new Snapshot[MaxEntries];

	private static int _nextSlot;

	/// <summary>Effects already counted in the scan being run - reused so the walk allocates nothing beyond the list
	/// <c>GetAllItems</c> returns. Compared by reference (Unity's <c>==</c> on a
	/// <see cref="UnityEngine.Object"/> IS identity, including the destroyed-object case).</summary>
	private static readonly List<StatusEffect> _seen = new List<StatusEffect>(8);

	/// <summary>The extra weight the items in <paramref name="inv"/> give (0 when there is none or no inventory).</summary>
	internal static float Bonus(Inventory inv)
	{
		return Resolve(inv, null);
	}

	/// <summary>The <b>effective</b> ceiling of this inventory: base + item bonus.</summary>
	internal static float Max(Inventory inv)
	{
		return Base + Resolve(inv, null);
	}

	/// <summary>The effective ceiling of a companion (no humanoid/inventory ⇒ the base).</summary>
	internal static float Max(Humanoid humanoid)
	{
		if (humanoid == null)
		{
			return Base;
		}
		return Base + Resolve(humanoid.GetInventory(), humanoid);
	}

	/// <summary>The effective ceiling of a companion, given its setup component (no humanoid/inventory ⇒ the base).</summary>
	internal static float Max(CompanionSetup setup)
	{
		Humanoid humanoid = ((setup != null) ? setup.GetComponent<Humanoid>() : null);
		if (humanoid == null)
		{
			return Base;
		}
		return Max(humanoid);
	}

	/// <summary>
	/// The ceiling at which a <b>gathering</b> companion stops picking things up and goes home to unload:
	/// <c>[Gathering] OverweightThreshold</c> <b>plus the item bonus</b>, exactly like <see cref="Max(Inventory)"/> -
	/// a Megingjord in the pack has to raise this one too, or the commonest job in the mod (wood/stone/ore/forage)
	/// would keep turning home at 298 kg while the pack could hold 450. No clamp: the config could always be set above
	/// <c>[General] MaxCarryWeight</c> and that has not changed.
	/// </summary>
	internal static float UnloadThreshold(Inventory inv)
	{
		return ModConfig.HarvestOverweightThreshold.Value + Bonus(inv);
	}

	/// <summary>How full the pack is, from 0 to 1, <b>against the effective ceiling</b> (the overhead bar and the
	/// weight line of the interaction panel).</summary>
	internal static float Fraction(Inventory inv)
	{
		if (inv == null)
		{
			return 0f;
		}
		float max = Max(inv);
		return (max > 0f) ? Mathf.Clamp01(inv.GetTotalWeight() / max) : 0f;
	}

	/// <summary>Same as <see cref="Fraction(Inventory)"/>, for a companion (0 without a humanoid).</summary>
	internal static float Fraction(Humanoid humanoid)
	{
		return (humanoid != null) ? Fraction(humanoid.GetInventory()) : 0f;
	}

	/// <summary>Forgets everything (world unload/tests). Not required for correctness - a destroyed inventory frees its
	/// slot on the next lookup - but it keeps a stale owner reference from outliving the session.</summary>
	internal static void Reset()
	{
		for (int i = 0; i < _entries.Length; i++)
		{
			_entries[i] = default(Snapshot);
		}
		_nextSlot = 0;
	}

	// ------------------------------------------------------------------ the cache

	/// <summary>
	/// The slot of this inventory, in two passes: (1) the slot that already holds <b>this same</b> inventory, (2) the
	/// first free one, (3) the round-robin victim. The order matters: looking for a free slot first would hand back a
	/// slot <b>before</b> an existing entry for the same inventory, which would then be re-cached as a brand new one -
	/// an extra scan and, worse, a second <c>[Carry]</c> line for a bonus that did not change.
	/// </summary>
	private static int SlotFor(Inventory inv)
	{
		for (int i = 0; i < _entries.Length; i++)
		{
			if (ReferenceEquals(_entries[i].Inv, inv))
			{
				return i;
			}
		}
		for (int i = 0; i < _entries.Length; i++)
		{
			if (_entries[i].Inv == null)
			{
				return i;
			}
		}
		int slot = _nextSlot;
		_nextSlot = (_nextSlot + 1) % _entries.Length;
		_entries[slot] = default(Snapshot);
		return slot;
	}

	/// <summary>The cached (or freshly scanned) bonus of this inventory, logging when it changes.</summary>
	private static float Resolve(Inventory inv, Humanoid owner)
	{
		if (inv == null)
		{
			return 0f;
		}
		int slot = SlotFor(inv);
		Snapshot entry = _entries[slot];
		if (owner != null)
		{
			entry.Owner = owner;
		}
		float now = Time.unscaledTime;
		if (entry.Known)
		{
			if (now < entry.NextAllowed)
			{
				_entries[slot] = entry;
				return entry.Bonus;
			}
			if (now < entry.NextFull && entry.Items == inv.NrOfItems() && entry.Weight == inv.GetTotalWeight())
			{
				_entries[slot] = entry;
				return entry.Bonus;
			}
		}
		float bonus = Scan(inv, out string items, out int duplicates);
		bool first = !entry.Known;
		float previous = entry.Bonus;
		entry.Inv = inv;
		entry.Known = true;
		entry.Items = inv.NrOfItems();
		entry.Weight = inv.GetTotalWeight();
		entry.Bonus = bonus;
		entry.NextAllowed = now + RefreshInterval;
		entry.NextFull = now + FullRescanInterval;
		_entries[slot] = entry;
		// One line per CHANGE (not per frame): a belt going in or out of the pack, or a second one that does not stack.
		// A brand new entry with no bonus stays silent - that is the common case and would be pure noise.
		if (first ? (bonus > 0f) : (bonus != previous))
		{
			LogChange(entry, bonus, items, duplicates);
		}
		return bonus;
	}

	/// <summary>A one-line summary of the change, e.g. <c>[Carry] "Thorin": +150 de capacidade (Cinto de forca) - teto 450</c>.</summary>
	private static void LogChange(Snapshot entry, float bonus, string items, int duplicates)
	{
		string who = ((entry.Owner != null) ? ("\"" + entry.Owner.m_name + "\": ") : "");
		// the duplicate note is what tells the player that a second belt gave nothing (the game's own rule, kept here)
		string extra = ((duplicates > 0) ? ("; " + duplicates + " item(ns) igual(is) nao soma(m)") : "");
		if (bonus > 0f)
		{
			CompanionsPlugin.Log.LogInfo($"[Carry] {who}+{bonus:0.##} de capacidade ({items}{extra}) - teto {Base + bonus:0.##}");
		}
		else
		{
			CompanionsPlugin.Log.LogInfo($"[Carry] {who}sem bonus de capacidade - teto {Base + bonus:0.##}");
		}
	}

	// ------------------------------------------------------------------ the walk

	/// <summary>Walks every item of the inventory (backpack and equipped alike) and sums the carry-weight bonus of its
	/// equipment effect, once per effect asset. Allocates exactly one <c>List</c> (the game's
	/// <c>GetAllItems</c>) - only called from <see cref="Resolve"/> when the cache says something changed.</summary>
	private static float Scan(Inventory inv, out string items, out int duplicates)
	{
		float bonus = 0f;
		int duplicateCount = 0;
		string names = null;
		_seen.Clear();
		List<ItemDrop.ItemData> all = inv.GetAllItems();
		for (int i = 0; i < all.Count; i++)
		{
			ItemDrop.ItemData item = all[i];
			if (item == null || item.m_shared == null)
			{
				continue;
			}
			StatusEffect effect = item.m_shared.m_equipStatusEffect;
			if (!(effect is SE_Stats stats) || !(stats.m_addMaxCarryWeight > 0f))
			{
				continue;
			}
			if (Contains(effect))
			{
				duplicateCount++;
				continue;
			}
			_seen.Add(effect);
			bonus += stats.m_addMaxCarryWeight;
			string name = ItemName(item);
			names = ((names == null) ? name : (names + ", " + name));
		}
		items = ((names != null) ? names : "sem item");
		duplicates = duplicateCount;
		return bonus;
	}

	private static bool Contains(StatusEffect effect)
	{
		for (int i = 0; i < _seen.Count; i++)
		{
			if (_seen[i] == effect)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>The item name as the player reads it (the game localizes <c>m_shared.m_name</c>, and a not yet
	/// localized token comes back as the token); falls back to the prefab name.</summary>
	private static string ItemName(ItemDrop.ItemData item)
	{
		string name = item.m_shared?.m_name;
		if (!string.IsNullOrEmpty(name))
		{
			try
			{
				string localized = ((Localization.instance != null) ? Localization.instance.Localize(name) : name);
				if (!string.IsNullOrEmpty(localized))
				{
					return localized;
				}
			}
			catch
			{
				// localization not up yet: the token is still readable
			}
			return name;
		}
		return ((item.m_dropPrefab != null) ? item.m_dropPrefab.name : "?");
	}
}
