using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Companions;

internal static class ShieldLogic
{
	private static Hashtable _lastCategory = new Hashtable();

	private static int _logs;

	private static MethodInfo _getLeftItemMethod;

	internal static ItemDrop.ItemData SelectShield(Humanoid h, Character self)
	{
		try
		{
			if (h == null || self == null)
			{
				return null;
			}
			ItemDrop.ItemData currentWeapon = h.GetCurrentWeapon();
			if (currentWeapon == null || currentWeapon.m_shared == null || currentWeapon.m_shared.m_itemType != ItemDrop.ItemData.ItemType.OneHandedWeapon)
			{
				return null;
			}
			Inventory inventory = h.GetInventory();
			if (inventory == null)
			{
				return null;
			}
			ItemDrop.ItemData itemData = null;
			float num = -1f;
			ItemDrop.ItemData itemData2 = null;
			float num2 = -1f;
			List<ItemDrop.ItemData> allItems = inventory.GetAllItems();
			for (int i = 0; i < allItems.Count; i++)
			{
				ItemDrop.ItemData itemData3 = allItems[i];
				if (itemData3 != null && itemData3.m_shared != null && itemData3.IsEquipable() && Usable(itemData3) && itemData3.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shield)
				{
					if (itemData3.m_shared.m_timedBlockBonus > num)
					{
						num = itemData3.m_shared.m_timedBlockBonus;
						itemData = itemData3;
					}
					if (itemData3.m_shared.m_blockPower > num2)
					{
						num2 = itemData3.m_shared.m_blockPower;
						itemData2 = itemData3;
					}
				}
			}
			if (itemData == null && itemData2 == null)
			{
				return null;
			}
			float healthPercentage = h.GetHealthPercentage();
			int num3 = CountNearbyEnemies(self, 15f);
			ItemDrop.ItemData itemData4 = ((!(healthPercentage < RetreatPct()) && num3 < MultiAttackerCount()) ? (itemData ?? itemData2) : (itemData2 ?? itemData));
			string text = ((itemData4 != itemData2) ? "parry" : "block");
			ItemDrop.ItemData leftItemViaReflection = GetLeftItemViaReflection(h);
			bool flag = leftItemViaReflection != null && leftItemViaReflection.m_shared != null && leftItemViaReflection.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shield;
			int instanceID = self.GetInstanceID();
			if (flag)
			{
				object obj = _lastCategory[instanceID];
				if (obj != null && (string)obj == text)
				{
					return null;
				}
			}
			_lastCategory[instanceID] = text;
			if (CompanionsPlugin.LogNormal) Log($"[Shield] -> {itemData4.m_shared.m_name} ({text}) hp={healthPercentage * 100f:F0}% alertedNearby={num3} (retreat<{RetreatPct() * 100f:F0}% or nearby>={MultiAttackerCount()})");
			return itemData4;
		}
		catch (Exception)
		{
			return null;
		}
	}

	private static void Log(string s)
	{
		if (_logs < 60)
		{
			_logs++;
			if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log?.LogInfo(s);
		}
	}

	private static float RetreatPct()
	{
		try
		{
			if (ModConfig.HealthRetreatPct != null)
			{
				return ModConfig.HealthRetreatPct.Value;
			}
		}
		catch (Exception)
		{
		}
		return 0.3f;
	}

	private static int MultiAttackerCount()
	{
		try
		{
			if (ModConfig.ShieldMultiAttackerCount != null)
			{
				return ModConfig.ShieldMultiAttackerCount.Value;
			}
		}
		catch (Exception)
		{
		}
		return 3;
	}

	private static int CountNearbyEnemies(Character self, float range)
	{
		int num = 0;
		try
		{
			List<Character> allCharacters = Character.GetAllCharacters();
			for (int i = 0; i < allCharacters.Count; i++)
			{
				Character character = allCharacters[i];
				if (!(character == null) && !(character == self) && !character.IsDead() && BaseAI.IsEnemy(self, character))
				{
					BaseAI baseAI = character.GetBaseAI();
					if (!(baseAI == null) && baseAI.IsAlerted() && Vector3.Distance(self.transform.position, character.transform.position) <= range)
					{
						num++;
					}
				}
			}
		}
		catch (Exception)
		{
		}
		return num;
	}

	private static ItemDrop.ItemData GetLeftItemViaReflection(Humanoid h)
	{
		if (_getLeftItemMethod == null)
		{
			_getLeftItemMethod = typeof(Humanoid).GetMethod("GetLeftItem", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		}
		if (_getLeftItemMethod == null)
		{
			return null;
		}
		return (ItemDrop.ItemData)_getLeftItemMethod.Invoke(h, null);
	}

	private static bool Usable(ItemDrop.ItemData it)
	{
		if (it == null || it.m_shared == null)
		{
			return false;
		}
		return !it.m_shared.m_useDurability || it.m_durability > 0f;
	}
}
