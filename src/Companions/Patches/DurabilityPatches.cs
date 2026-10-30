using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Companions;

public static class DurabilityPatches
{
	[HarmonyPatch(typeof(Humanoid), "StartAttack")]
	private static class WeaponDurability_Patch
	{
		private static void Postfix(Humanoid __instance, bool __result)
		{
			if (!__result || __instance.GetComponent<CompanionSetup>() == null)
			{
				return;
			}
			ItemDrop.ItemData itemData = MHR_Equip.DurabilityItem(__instance);
			if (itemData != null && itemData.m_shared.m_useDurability)
			{
				float durability = itemData.m_durability;
				float useDurabilityDrain = itemData.m_shared.m_useDurabilityDrain;
				itemData.m_durability -= useDurabilityDrain;
				float maxDurability = itemData.GetMaxDurability();
				float num = ((maxDurability > 0f) ? (itemData.m_durability / maxDurability * 100f) : 0f);
				if (CompanionsPlugin.LogDetailed) CompanionsPlugin.Log.LogDebug("[Durability] Weapon drain — \"" + itemData.m_shared.m_name + "\" " + $"durability {durability:F1} → {itemData.m_durability:F1} / {maxDurability:F0} " + $"({num:F0}%) drain={useDurabilityDrain:F1} " + "companion=\"" + __instance.m_name + "\"");
				if (itemData.m_durability <= 0f)
				{
					itemData.m_durability = 0f;
					__instance.UnequipItem(itemData, triggerEquipEffects: false);
					// 1.5.8: items the game destroys when they break (torches) go, as for a player (Humanoid.DrainEquipedItemDurability)
					if (itemData.m_shared.m_destroyBroken)
					{
						__instance.GetInventory()?.RemoveItem(itemData);
					}
					MHR_Equip.OnToolBroke(__instance);
					CompanionsPlugin.Log.LogWarning("[Durability] Weapon BROKEN — \"" + itemData.m_shared.m_name + "\" unequipped, companion=\"" + __instance.m_name + "\"");
				}
			}
		}
	}

	[HarmonyPatch(typeof(Character), "RPC_Damage")]
	private static class ArmorDurability_Patch
	{
		private static readonly List<ItemDrop.ItemData> _armorPieces = new List<ItemDrop.ItemData>(4);

		private static void Prefix(Character __instance, HitData hit)
		{
			// fail-open: a null hit is not ours to touch (the old code threw right here)
			if (hit == null || __instance == null)
			{
				return;
			}
			CompanionSetup component = __instance.GetComponent<CompanionSetup>();
			if (!(component == null))
			{
				float totalArmor = component.GetTotalArmor();
				if (totalArmor > 0f)
				{
					float totalDamage = hit.GetTotalDamage();
					hit.ApplyArmor(totalArmor);
					float totalDamage2 = hit.GetTotalDamage();
					if (CompanionsPlugin.LogDetailed) CompanionsPlugin.Log.LogDebug($"[Durability] Armor reduction — totalArmor={totalArmor:F1} " + $"dmg {totalDamage:F1} → {totalDamage2:F1} " + $"(blocked {totalDamage - totalDamage2:F1}) " + "companion=\"" + __instance.m_name + "\"");
				}
			}
		}

		private static void Postfix(Character __instance, HitData hit)
		{
			if (hit == null || __instance == null)
			{
				return;
			}
			CompanionSetup component = __instance.GetComponent<CompanionSetup>();
			if (component == null)
			{
				return;
			}
			Humanoid humanoid = __instance as Humanoid;
			if (humanoid == null)
			{
				return;
			}
			// 1.8.3 (patch audit): the armor is the companion's own state - only the ZDO owner drains it,
			// the same rule CompanionFallDamagePatch and PortalPatches follow. Without this every client
			// that sees the hit would take durability off its local copy of the item.
			ZNetView nview = __instance.GetComponent<ZNetView>();
			if (nview == null || !nview.IsOwner())
			{
				return;
			}
			_armorPieces.Clear();
			AddIfValid(_armorPieces, _chestItem?.GetValue(humanoid) as ItemDrop.ItemData);
			AddIfValid(_armorPieces, _legItem?.GetValue(humanoid) as ItemDrop.ItemData);
			AddIfValid(_armorPieces, _helmetItem?.GetValue(humanoid) as ItemDrop.ItemData);
			AddIfValid(_armorPieces, _shoulderItem?.GetValue(humanoid) as ItemDrop.ItemData);
			if (_armorPieces.Count == 0)
			{
				if (CompanionsPlugin.LogDetailed) CompanionsPlugin.Log.LogDebug("[Durability] No armor with durability equipped — skipping drain companion=\"" + __instance.m_name + "\"");
				return;
			}
			float num = hit.GetTotalPhysicalDamage() + hit.GetTotalElementalDamage();
			if (!(num <= 0f))
			{
				ItemDrop.ItemData itemData = _armorPieces[Random.Range(0, _armorPieces.Count)];
				float durability = itemData.m_durability;
				itemData.m_durability = Mathf.Max(0f, itemData.m_durability - num);
				float maxDurability = itemData.GetMaxDurability();
				float num2 = ((maxDurability > 0f) ? (itemData.m_durability / maxDurability * 100f) : 0f);
				if (CompanionsPlugin.LogDetailed) CompanionsPlugin.Log.LogDebug("[Durability] Armor drain — \"" + itemData.m_shared.m_name + "\" " + $"durability {durability:F1} → {itemData.m_durability:F1} / {maxDurability:F0} " + $"({num2:F0}%) dmgTaken={num:F1} " + $"armorPieces={_armorPieces.Count} companion=\"{__instance.m_name}\"");
				if (itemData.m_durability <= 0f)
				{
					humanoid.UnequipItem(itemData, triggerEquipEffects: false);
					CompanionsPlugin.Log.LogWarning("[Durability] Armor BROKEN — \"" + itemData.m_shared.m_name + "\" unequipped, companion=\"" + __instance.m_name + "\"");
				}
			}
		}
	}

	private static readonly FieldInfo _chestItem = AccessTools.Field(typeof(Humanoid), "m_chestItem");

	private static readonly FieldInfo _legItem = AccessTools.Field(typeof(Humanoid), "m_legItem");

	private static readonly FieldInfo _helmetItem = AccessTools.Field(typeof(Humanoid), "m_helmetItem");

	private static readonly FieldInfo _shoulderItem = AccessTools.Field(typeof(Humanoid), "m_shoulderItem");

	private static void AddIfValid(List<ItemDrop.ItemData> list, ItemDrop.ItemData item)
	{
		if (item != null && item.m_shared != null && item.m_shared.m_useDurability)
		{
			list.Add(item);
		}
	}
}
