using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace Companions;

[HarmonyPatch(typeof(Container), "Awake")]
public static class ContainerAwakePatch
{
	private const int CompanionInventoryWidth = 8;

	private const int CompanionInventoryHeight = 4;

	private static readonly FieldInfo _inventoryField = AccessTools.Field(typeof(Container), "m_inventory");

	internal static readonly FieldInfo _invWidthField = AccessTools.Field(typeof(Inventory), "m_width");

	internal static readonly FieldInfo _invHeightField = AccessTools.Field(typeof(Inventory), "m_height");

	private static readonly MethodInfo _onContainerChanged = AccessTools.Method(typeof(Container), "OnContainerChanged");

	[HarmonyPostfix]
	private static void Postfix(Container __instance)
	{
		CompanionSetup component = __instance.GetComponent<CompanionSetup>();
		if (component == null)
		{
			return;
		}
		Humanoid component2 = __instance.GetComponent<Humanoid>();
		if (component2 == null)
		{
			return;
		}
		__instance.m_width = 8;
		__instance.m_height = 4;
		ZNetView component3 = __instance.GetComponent<ZNetView>();
		if (component3 != null && component3.GetZDO() != null && CompanionTierData.IsDvergerVariant(component3.GetZDO().GetPrefab()))
		{
			ForceInventoryDimensions(__instance.GetInventory(), 8, 4);
			CompanionsPlugin.Log.LogDebug("[ContainerAwake] Dverger variant, keeping separate inventories");
			return;
		}
		if ((component3 == null || component3.GetZDO() == null) && __instance.gameObject.name.Contains("Dverger"))
		{
			ForceInventoryDimensions(__instance.GetInventory(), 8, 4);
			CompanionsPlugin.Log.LogDebug("[ContainerAwake] Dverger variant (name check), keeping separate inventories");
			return;
		}
		Inventory inventory = component2.GetInventory();
		if (_inventoryField == null || inventory == null)
		{
			CompanionsPlugin.Log.LogWarning($"[ContainerAwake] Cannot share inventory, field={_inventoryField != null} humanoidInv={inventory != null}");
			return;
		}
		int num = 8;
		int num2 = 4;
		int width = inventory.GetWidth();
		int height = inventory.GetHeight();
		ForceInventoryDimensions(inventory, num, num2);
		_inventoryField.SetValue(__instance, inventory);
		if (_onContainerChanged != null)
		{
			Action action = (Action)Delegate.CreateDelegate(typeof(Action), __instance, _onContainerChanged);
			inventory.m_onChanged = (Action)Delegate.Remove(inventory.m_onChanged, action);
			inventory.m_onChanged = (Action)Delegate.Combine(inventory.m_onChanged, action);
		}
		CompanionsPlugin.Log.LogDebug($"[ContainerAwake] Shared inventory, container={__instance.m_width}x{__instance.m_height} " + $"humanoidInv dim {width}x{height} -> {num}x{num2} items={inventory.NrOfItems()}");
	}

	private static void ForceInventoryDimensions(Inventory inv, int width, int height)
	{
		if (inv != null)
		{
			int width2 = inv.GetWidth();
			NormalizeLegacyInventoryLayout(inv, width2, width);
			_invWidthField?.SetValue(inv, width);
			_invHeightField?.SetValue(inv, height);
		}
	}

	private static void NormalizeLegacyInventoryLayout(Inventory inv, int sourceWidth, int targetWidth)
	{
		if (inv == null || targetWidth <= 0)
		{
			return;
		}
		List<ItemDrop.ItemData> allItems = inv.GetAllItems();
		if (allItems == null || allItems.Count == 0)
		{
			return;
		}
		int num = ((sourceWidth > 0) ? sourceWidth : targetWidth);
		bool flag = num != targetWidth;
		if (!flag)
		{
			for (int i = 0; i < allItems.Count; i++)
			{
				ItemDrop.ItemData itemData = allItems[i];
				if (itemData != null && itemData.m_gridPos.x >= targetWidth)
				{
					flag = true;
					break;
				}
			}
		}
		if (!flag)
		{
			return;
		}
		for (int j = 0; j < allItems.Count; j++)
		{
			ItemDrop.ItemData itemData2 = allItems[j];
			if (itemData2 != null)
			{
				int num2 = Math.Max(0, itemData2.m_gridPos.y) * num + Math.Max(0, itemData2.m_gridPos.x);
				itemData2.m_gridPos = new Vector2i(num2 % targetWidth, num2 / targetWidth);
			}
		}
		CompanionsPlugin.Log.LogDebug($"[ContainerAwake] Normalized companion inventory layout to {targetWidth}-wide grid");
	}
}
