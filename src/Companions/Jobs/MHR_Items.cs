using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Companions;

// 1.4.117: item helpers that used to live in HomesteadController (Homestead is retired; Cook still uses these)
internal static class MHR_Items
{
	// 1.5.43: a name to show for something in the world ("Beech", "Copper deposit", "Raspberry bush"): the game's own name
	// where it has one, else the object's name tidied up (trees have none: "Beech1(Clone)" -> "Beech")
	internal static string NiceName(GameObject go)
	{
		if (go == null)
		{
			return "";
		}
		string n = null;
		try
		{
			Hoverable hv = go.GetComponentInParent<Hoverable>();
			n = hv?.GetHoverName();
			if (string.IsNullOrEmpty(n))
			{
				n = go.GetComponentInParent<ItemDrop>()?.m_itemData?.m_shared?.m_name;
			}
			if (!string.IsNullOrEmpty(n) && Localization.instance != null)
			{
				n = Localization.instance.Localize(n);
			}
		}
		catch
		{
			n = null;
		}
		if (!string.IsNullOrEmpty(n) && !n.StartsWith("$"))
		{
			return n;
		}
		string raw = go.name.Replace("(Clone)", "").Trim().TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '_', ' ');
		StringBuilder sb = new StringBuilder();
		for (int i = 0; i < raw.Length; i++)
		{
			char ch = raw[i];
			if (ch == '_')
			{
				sb.Append(' ');
				continue;
			}
			if (i > 0 && char.IsUpper(ch) && char.IsLower(raw[i - 1]))
			{
				sb.Append(' ');
			}
			sb.Append(sb.Length == 0 ? char.ToUpperInvariant(ch) : char.ToLowerInvariant(ch));
		}
		return sb.ToString();
	}

	internal static int CountItemInInventory(Inventory inv, string prefabName)
	{
		int num = 0;
		List<ItemDrop.ItemData> allItems = inv.GetAllItems();
		for (int i = 0; i < allItems.Count; i++)
		{
			string text = allItems[i].m_dropPrefab?.name ?? allItems[i].m_shared?.m_name;
			if (text == prefabName)
			{
				num += allItems[i].m_stack;
			}
		}
		return num;
	}

	internal static ItemDrop.ItemData FindItemByPrefab(Inventory inv, string prefabName)
	{
		List<ItemDrop.ItemData> allItems = inv.GetAllItems();
		for (int i = 0; i < allItems.Count; i++)
		{
			if (allItems[i].m_dropPrefab != null && allItems[i].m_dropPrefab.name == prefabName)
			{
				return allItems[i];
			}
		}
		return null;
	}

	internal static bool TransferOne(Inventory source, Inventory dest, ItemDrop.ItemData item)
	{
		ItemDrop.ItemData itemData = item.Clone();
		itemData.m_stack = 1;
		if (!dest.CanAddItem(itemData, 1))
		{
			return false;
		}
		if (!dest.AddItem(itemData))
		{
			return false;
		}
		source.RemoveItem(item, 1);
		return true;
	}

	internal static bool ConsumeOneFromInventory(Inventory inv, string prefabName)
	{
		List<ItemDrop.ItemData> allItems = inv.GetAllItems();
		for (int i = 0; i < allItems.Count; i++)
		{
			string text = allItems[i].m_dropPrefab?.name ?? allItems[i].m_shared?.m_name;
			if (text == prefabName)
			{
				inv.RemoveItem(allItems[i], 1);
				return true;
			}
		}
		return false;
	}
}
