using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace Companions;

internal static class WeaponLogic
{
	private static FieldInfo _stanceField;

	private static PropertyInfo _huntProp;

	private static int _balLogs;

	internal static string LastReason;

	private static int _rangedLogs;

	private static int _entryLogs;

	private static FieldInfo _targetField;

	private static int _nonCompanionLogs;

	internal static bool BalancedPrefix(Humanoid h)
	{
		try
		{
			if (!IsBalancedCompanion(h))
			{
				bool flag = false;
				try
				{
					BaseAI baseAI = h.GetBaseAI();
					flag = baseAI != null && baseAI.GetType().Name == "CompanionAI";
				}
				catch (Exception)
				{
				}
				if (flag)
				{
					EntryLog("[Balanced] skip: " + LastReason);
				}
				else
				{
					NonCompanionLog("[Balanced] skip: " + LastReason);
				}
				return true;
			}
			if (TorchOpener.IsActive(h))
			{
				EntryLog("[Balanced] skip: torch opener active.");
				return true;
			}
			BaseAI baseAI2 = h.GetBaseAI();
			Component component = h.GetComponent("CompanionCombatAI");
			if (component == null)
			{
				EntryLog("[Balanced] skip: no CompanionCombatAI component found for target lookup.");
				return true;
			}
			if (_targetField == null)
			{
				_targetField = component.GetType().GetField("_targetCreature", BindingFlags.Instance | BindingFlags.NonPublic);
			}
			Character character = ((!(_targetField == null)) ? ((Character)_targetField.GetValue(component)) : null);
			if (character == null)
			{
				EntryLog("[Balanced] skip: no target.");
				return true;
			}
			if (character == null || character.IsDead())
			{
				EntryLog("[Balanced] skip: target null/dead.");
				return true;
			}
			if (h.InAttack())
			{
				EntryLog("[Balanced] handling (mid-attack, no re-pick).");
				return false;
			}
			ItemDrop.ItemData currentWeapon = h.GetCurrentWeapon();
			Inventory inventory = h.GetInventory();
			if (currentWeapon == null || inventory == null)
			{
				EntryLog("[Balanced] skip: no current weapon or inventory.");
				return true;
			}
			EntryLog("[Balanced] handling weapon choice for this call.");
			float num = Vector3.Distance(h.transform.position, character.transform.position) - character.GetRadius();
			if (num < 0f)
			{
				num = 0f;
			}
			IList allItems = inventory.GetAllItems();
			ItemDrop.ItemData itemData = null;
			float num2 = -1f;
			ItemDrop.ItemData itemData2 = null;
			float num3 = -1f;
			string text = null;
			for (int i = 0; i < allItems.Count; i++)
			{
				ItemDrop.ItemData itemData3 = (ItemDrop.ItemData)allItems[i];
				if (itemData3 == null || itemData3.m_shared == null || !itemData3.IsWeapon() || !MHR_Crossbow.ReadyFor(itemData3, h) || !baseAI2.CanUseAttack(itemData3) || itemData3.m_shared.m_aiTargetType != ItemDrop.ItemData.AiTarget.Enemy)
				{
					continue;
				}
				float num4 = Score(itemData3, character);
				if (BowLike(itemData3))
				{
					string ammoType = itemData3.m_shared.m_ammoType;
					if (!string.IsNullOrEmpty(ammoType) && inventory.GetAmmoItem(ammoType) == null)
					{
						if (text == null)
						{
							text = itemData3.m_shared.m_name + " (needs " + ammoType + ")";
						}
					}
					else if (num4 > num2)
					{
						num2 = num4;
						itemData = itemData3;
					}
				}
				else if (num4 > num3)
				{
					num3 = num4;
					itemData2 = itemData3;
				}
			}
			bool flag2 = BowLike(currentWeapon);
			bool flag3 = itemData != null && (itemData2 == null || num > BowSwitch() || (!(num < MeleeSwitch()) && flag2));
			// 1.5.73/1.5.74: the bow, whatever the distance, for a target the melee weapon can't get at - out in deep water,
			// a level above or below, no way to walk there - or (fighting defensively) too far from you to go after; only
			// with a clear shot (user: "some bugs were below us and it would have been a good place to use a bow")
			if (itemData != null && !flag3 && component is CompanionCombatAI cc && cc.BowOnly(character, out string bowWhy) && MHR_BowDraw.ClearShot(h, character))
			{
				flag3 = true;
				BowWhyLog("[Balanced] bow for " + character.m_name + " (" + num.ToString("F1") + " m): " + bowWhy);
			}
			ItemDrop.ItemData itemData4 = null;
			if (flag3)
			{
				if (!flag2 || (currentWeapon != itemData && num2 > Score(currentWeapon, character) * SwitchFactor()))
				{
					itemData4 = itemData;
				}
			}
			else if (itemData2 != null && (flag2 || (currentWeapon != itemData2 && num3 > Score(currentWeapon, character) * SwitchFactor())))
			{
				itemData4 = itemData2;
			}
			StatusLog(string.Format("[Balanced] status dist={0:F1} cur={1} bestBow={2} bestMelee={3} wantRanged={4} pick={5} ammo={6}", num, currentWeapon.m_shared.m_name, (itemData != null) ? itemData.m_shared.m_name : ((text == null) ? "none in inventory" : ("none - " + text)), (itemData2 == null) ? "none" : itemData2.m_shared.m_name, flag3, (itemData4 == null) ? "(no change)" : itemData4.m_shared.m_name, (itemData != null && !string.IsNullOrEmpty(itemData.m_shared.m_ammoType)) ? (inventory.GetAmmoItem(itemData.m_shared.m_ammoType)?.m_stack ?? 0) : -1));
			if (itemData4 != null && itemData4 != currentWeapon)
			{
				BalLog(string.Format("[Balanced] dist={0:F1} -> {1} ({2})", num, itemData4.m_shared.m_name, (!flag3) ? "melee" : "ranged"));
				h.EquipItem(itemData4);
			}
			return false;
		}
		catch (Exception)
		{
			return true;
		}
	}

	internal static bool IsManagedStance(Humanoid h)
	{
		try
		{
			if (h == null)
			{
				return false;
			}
			Component component = h.GetComponent("CompanionCombatAI");
			if (component == null)
			{
				return false;
			}
			BaseAI baseAI = h.GetBaseAI();
			if (baseAI == null || baseAI.GetType().Name != "CompanionAI")
			{
				return false;
			}
			Type type = component.GetType();
			BindingFlags bindingAttr = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
			if (_stanceField == null)
			{
				_stanceField = type.GetField("_stance", bindingAttr);
			}
			if (_stanceField == null)
			{
				return false;
			}
			int num = (int)_stanceField.GetValue(component);
			return num == 0 || num == 2 || num == 3;
		}
		catch (Exception)
		{
			return false;
		}
	}

	internal static float SwitchFactor()
	{
		try
		{
			if (ModConfig.WeaponSwitchMargin != null)
			{
				float num = ModConfig.WeaponSwitchMargin.Value;
				if (num < 0f)
				{
					num = 0f;
				}
				return 1f + num;
			}
		}
		catch (Exception)
		{
		}
		return 1.1f;
	}

	internal static float Score(ItemDrop.ItemData item, Character target)
	{
		if (item == null || item.m_shared == null)
		{
			return 0f;
		}
		HitData.DamageTypes damage = item.GetDamage();
		float num;
		if (target == null)
		{
			num = damage.m_damage + damage.m_blunt + damage.m_slash + damage.m_pierce + damage.m_fire + damage.m_frost + damage.m_lightning + damage.m_poison + damage.m_spirit;
		}
		else
		{
			HitData.DamageModifiers damageModifiers = target.GetDamageModifiers();
			num = damage.m_damage;
			num += damage.m_blunt * Mult(damageModifiers.GetModifier(HitData.DamageType.Blunt));
			num += damage.m_slash * Mult(damageModifiers.GetModifier(HitData.DamageType.Slash));
			num += damage.m_pierce * Mult(damageModifiers.GetModifier(HitData.DamageType.Pierce));
			num += damage.m_fire * Mult(damageModifiers.GetModifier(HitData.DamageType.Fire));
			num += damage.m_frost * Mult(damageModifiers.GetModifier(HitData.DamageType.Frost));
			num += damage.m_lightning * Mult(damageModifiers.GetModifier(HitData.DamageType.Lightning));
			num += damage.m_poison * Mult(damageModifiers.GetModifier(HitData.DamageType.Poison));
			num += damage.m_spirit * Mult(damageModifiers.GetModifier(HitData.DamageType.Spirit));
		}
		return num * SpeedFactor(item);
	}

	internal static ItemDrop.ItemData FindReadyBow(Humanoid h, Character target)
	{
		try
		{
			if (h == null)
			{
				return null;
			}
			Inventory inventory = h.GetInventory();
			if (inventory == null)
			{
				return null;
			}
			IList allItems = inventory.GetAllItems();
			ItemDrop.ItemData itemData = null;
			float num = -1f;
			bool flag = false;
			string text = null;
			for (int i = 0; i < allItems.Count; i++)
			{
				ItemDrop.ItemData itemData2 = (ItemDrop.ItemData)allItems[i];
				if (itemData2 == null || itemData2.m_shared == null || !itemData2.IsEquipable() || !MHR_Crossbow.ReadyFor(itemData2, h) || !BowLike(itemData2))
				{
					continue;
				}
				flag = true;
				string ammoType = itemData2.m_shared.m_ammoType;
				if (!string.IsNullOrEmpty(ammoType) && inventory.GetAmmoItem(ammoType) == null)
				{
					if (text == null)
					{
						text = itemData2.m_shared.m_name + " (needs " + ammoType + ")";
					}
					continue;
				}
				float num2 = Score(itemData2, target);
				if (num2 > num)
				{
					num = num2;
					itemData = itemData2;
				}
			}
			if (itemData != null)
			{
				RangedLog("[Ranged] FindReadyBow -> " + itemData.m_shared.m_name + " (ready, will equip)");
			}
			else if (text != null)
			{
				RangedLog("[Ranged] FindReadyBow -> none - " + text + " -> falling back to melee");
			}
			else if (flag)
			{
				RangedLog("[Ranged] FindReadyBow -> none - bow present but not usable (durability/equipable) -> falling back to melee");
			}
			else
			{
				RangedLog("[Ranged] FindReadyBow -> none - no bow-type item in inventory -> falling back to melee");
			}
			return itemData;
		}
		catch (Exception)
		{
			return null;
		}
	}

	internal static float SpeedFactor(ItemDrop.ItemData item)
	{
		if (item == null || item.m_shared == null)
		{
			return 1f;
		}
		Skills.SkillType skillType = item.m_shared.m_skillType;
		ItemDrop.ItemData.ItemType itemType = item.m_shared.m_itemType;
		bool flag = itemType == ItemDrop.ItemData.ItemType.TwoHandedWeapon || itemType == ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft;
		if (skillType == Skills.SkillType.Knives || skillType == Skills.SkillType.Spears || skillType == Skills.SkillType.Unarmed)
		{
			return 1.25f;
		}
		if (flag && (skillType == Skills.SkillType.Axes || skillType == Skills.SkillType.Clubs))
		{
			return 0.75f;
		}
		return 1f;
	}

	private static float Mult(HitData.DamageModifier m)
	{
		return m switch
		{
			HitData.DamageModifier.Normal => 1f, 
			HitData.DamageModifier.Resistant => 0.5f, 
			HitData.DamageModifier.Weak => 1.5f, 
			HitData.DamageModifier.Immune => 0f, 
			HitData.DamageModifier.Ignore => 0f, 
			HitData.DamageModifier.VeryResistant => 0.25f, 
			HitData.DamageModifier.VeryWeak => 2f, 
			HitData.DamageModifier.SlightlyResistant => 0.75f, 
			HitData.DamageModifier.SlightlyWeak => 1.25f, 
			_ => 1f, 
		};
	}

	private static bool IsBalanced(Humanoid h)
	{
		Component component = h.GetComponent("CompanionCombatAI");
		if (component == null)
		{
			LastReason = "no CompanionCombatAI component on this GameObject";
			return false;
		}
		Type type = component.GetType();
		BindingFlags bindingAttr = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
		if (_stanceField == null)
		{
			_stanceField = type.GetField("_stance", bindingAttr);
		}
		if (_huntProp == null)
		{
			_huntProp = type.GetProperty("HuntMode", bindingAttr);
		}
		if (_stanceField == null)
		{
			LastReason = "reflection could not find field _stance on " + type.Name;
			return false;
		}
		int num = (int)_stanceField.GetValue(component);
		bool flag = _huntProp != null && (bool)_huntProp.GetValue(component, null);
		LastReason = "stance=" + num + " (0=Balanced) hunt=" + flag;
		return num == 0 && !flag;
	}

	internal static bool IsBalancedCompanion(Humanoid h)
	{
		try
		{
			if (h == null)
			{
				LastReason = "humanoid is null";
				return false;
			}
			BaseAI baseAI = h.GetBaseAI();
			if (baseAI == null)
			{
				LastReason = "GetBaseAI() returned null";
				return false;
			}
			if (baseAI.GetType().Name != "CompanionAI")
			{
				LastReason = "BaseAI type is " + baseAI.GetType().Name + ", not CompanionAI";
				return false;
			}
			return IsBalanced(h);
		}
		catch (Exception ex)
		{
			LastReason = "exception: " + ex.Message;
			return false;
		}
	}

	private static bool BowLike(ItemDrop.ItemData it)
	{
		if (it == null || it.m_shared == null)
		{
			return false;
		}
		// 1.5.2: arrows and bolts carry an ammo type too - they're ammunition, not a bow (user log: "FindReadyBow -> arrows")
		if (it.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Ammo || it.m_shared.m_itemType == ItemDrop.ItemData.ItemType.AmmoNonEquipable)
		{
			return false;
		}
		return it.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Bow || !string.IsNullOrEmpty(it.m_shared.m_ammoType);
	}

	private static bool Ready(ItemDrop.ItemData it)
	{
		if (it == null || it.m_shared == null)
		{
			return false;
		}
		return !it.m_shared.m_useDurability || it.m_durability > 0f;
	}

	private static void RangedLog(string s)
	{
		if (_rangedLogs < 40)
		{
			_rangedLogs++;
			CompanionsPlugin.Log?.LogInfo(s);
		}
	}

	private static float BowSwitch()
	{
		try
		{
			if (ModConfig.BowSwitchDistance != null)
			{
				return ModConfig.BowSwitchDistance.Value;
			}
		}
		catch (Exception)
		{
		}
		return 15f;
	}

	private static float MeleeSwitch()
	{
		try
		{
			if (ModConfig.MeleeSwitchDistance != null)
			{
				return ModConfig.MeleeSwitchDistance.Value;
			}
		}
		catch (Exception)
		{
		}
		return 12f;
	}

	private static void BalLog(string s)
	{
		if (_balLogs < 60)
		{
			_balLogs++;
			CompanionsPlugin.Log?.LogInfo(s);
		}
	}

	// 1.5.74: every 3 s instead of the first 30 of the session (log 93298113 had none after the dungeon, so the weapon
	// choices against the Necks and Greydwarfs couldn't be seen)
	private static float _nextStatusLog;

	private static void StatusLog(string s)
	{
		if (Time.time >= _nextStatusLog)
		{
			_nextStatusLog = Time.time + 3f;
			CompanionsPlugin.Log?.LogDebug(s);
		}
	}

	private static float _nextBowWhyLog;

	private static void BowWhyLog(string s)
	{
		if (Time.time >= _nextBowWhyLog)
		{
			_nextBowWhyLog = Time.time + 5f;
			CompanionsPlugin.Log?.LogDebug(s);
		}
	}

	private static void EntryLog(string s)
	{
		if (_entryLogs < 200)
		{
			_entryLogs++;
			CompanionsPlugin.Log?.LogDebug(s);
		}
	}

	private static void NonCompanionLog(string s)
	{
		if (_nonCompanionLogs < 3)
		{
			_nonCompanionLogs++;
			CompanionsPlugin.Log?.LogInfo(s + " (further non-companion skips suppressed)");
		}
	}
}
