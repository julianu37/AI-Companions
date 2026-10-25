using HarmonyLib;
using UnityEngine;

namespace Companions;

public static class CombatPatches
{
	[HarmonyPatch(typeof(Character), "RPC_Damage")]
	private static class RPC_Damage_NoBackstab
	{
		private static void Prefix(Character __instance, HitData hit)
		{
			// fail-open: a null hit (or a destroyed character) is not ours to touch
			if (hit == null || __instance == null || hit.m_backstabBonus <= 1f)
			{
				return;
			}
			if (__instance.GetComponent<CompanionSetup>() == null)
			{
				return;
			}
			hit.m_backstabBonus = 1f;
		}
	}

	[HarmonyPatch(typeof(Character), "GetMaxHealth")]
	private static class GetMaxHealth_Patch
	{
		private static void Postfix(Character __instance, ref float __result)
		{
			BaseAI baseAI = __instance.GetBaseAI();
			if (baseAI == null || !(baseAI is CompanionAI))
			{
				return;
			}
			CompanionFood component = __instance.GetComponent<CompanionFood>();
			if (!(component == null))
			{
				float baseHealth = CompanionFood.BaseHealth;
				float totalHealthBonus = component.TotalHealthBonus;
				float num = baseHealth + totalHealthBonus;
				if (Time.time - _lastHealthLogTime > 5f)
				{
					_lastHealthLogTime = Time.time;
					CompanionsPlugin.Log.LogDebug($"[Combat] GetMaxHealth — base={baseHealth:F0} + food={totalHealthBonus:F1} " + $"= {num:F1} (vanilla was {__result:F1}) " + "companion=\"" + __instance.m_name + "\"");
				}
				__result = num;
			}
		}
	}

	[HarmonyPatch(typeof(BaseAI), "CanUseAttack")]
	public static class ToolCombatExclude
	{
		private static void Postfix(BaseAI __instance, ItemDrop.ItemData item, ref bool __result)
		{
			// This postfix runs for every BaseAI in the game: a missing item (a fist attack) or a shared data
			// without a weapon entry must not throw inside the vanilla call - fail open and keep the result.
			if (item == null || item.m_shared == null)
			{
				return;
			}
			if (__result && __instance is CompanionAI)
			{
				if (item.m_shared.m_animationState == ItemDrop.ItemData.AnimationState.FishingRod)
				{
					__result = false;
				}
				else if (item.GetDamage().m_pickaxe > 0f)
				{
					__result = false;
				}
				else if (item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Tool)
				{
					__result = false;
				}
			}
		}
	}

	[HarmonyPatch(typeof(Humanoid), "EquipBestWeapon")]
	private static class EquipBestWeapon_PreferHighestDamage
	{
		[HarmonyPrefix]
		private static bool Prefix(Humanoid __instance)
		{
			return WeaponLogic.BalancedPrefix(__instance);
		}

		[HarmonyPostfix]
		private static void Postfix(Humanoid __instance)
		{
			if (WeaponLogic.IsManagedStance(__instance))
			{
				return;
			}
			BaseAI baseAI = __instance.GetBaseAI();
			if (baseAI == null || !(baseAI is CompanionAI))
			{
				return;
			}
			ItemDrop.ItemData currentWeapon = __instance.GetCurrentWeapon();
			if (currentWeapon == null)
			{
				return;
			}
			Character target = ((!baseAI.HaveTarget()) ? null : baseAI.GetTargetCreature());
			float effectiveCombatDamage = GetEffectiveCombatDamage(currentWeapon, target);
			Inventory inventory = __instance.GetInventory();
			if (inventory == null)
			{
				return;
			}
			ItemDrop.ItemData itemData = currentWeapon;
			float num = effectiveCombatDamage;
			foreach (ItemDrop.ItemData allItem in inventory.GetAllItems())
			{
				if (allItem != null && allItem.m_shared != null && allItem.IsWeapon() && baseAI.CanUseAttack(allItem) && allItem.m_shared.m_aiTargetType == ItemDrop.ItemData.AiTarget.Enemy && (!allItem.m_shared.m_useDurability || !(allItem.m_durability <= 0f)))
				{
					float effectiveCombatDamage2 = GetEffectiveCombatDamage(allItem, target);
					if (effectiveCombatDamage2 > num)
					{
						itemData = allItem;
						num = effectiveCombatDamage2;
					}
				}
			}
			if (itemData != currentWeapon && num > effectiveCombatDamage * WeaponLogic.SwitchFactor())
			{
				__instance.EquipItem(itemData);
			}
		}

		private static float GetEffectiveCombatDamage(ItemDrop.ItemData item, Character target)
		{
			return WeaponLogic.Score(item, target);
		}

		private static float ModifierToMultiplier(HitData.DamageModifier mod)
		{
			return mod switch
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

		private static float GetCombatDamage(ItemDrop.ItemData item)
		{
			return WeaponLogic.Score(item, null);
		}
	}

	[HarmonyPatch(typeof(Destructible), "Damage")]
	private static class Destructible_SuppressHarvestPhysical
	{
		private static bool Prefix(Destructible __instance, HitData hit)
		{
			// fail-open on both ends: no hit means the damage is not ours to suppress, and any error inside the
			// mod's own code hands the damage back to the game (the old code threw out of the prefix)
			try
			{
				if (hit == null || HarvestController.IsApplyingAoeDamage)
				{
					return true;
				}
				Character attacker = hit.GetAttacker();
				if (attacker == null)
				{
					return true;
				}
				HarvestController component = attacker.GetComponent<HarvestController>();
				if (component != null && component.IsAttackingRockTarget(__instance.gameObject))
				{
					component.TryApplyRockAoe();
					return false;
				}
				return true;
			}
			catch
			{
				return true;
			}
		}
	}

	[HarmonyPatch(typeof(MineRock), "Damage")]
	private static class MineRock_SuppressHarvestPhysical
	{
		private static bool Prefix(MineRock __instance, HitData hit)
		{
			try
			{
				if (hit == null || HarvestController.IsApplyingAoeDamage)
				{
					return true;
				}
				Character attacker = hit.GetAttacker();
				if (attacker == null)
				{
					return true;
				}
				HarvestController component = attacker.GetComponent<HarvestController>();
				if (component != null && component.IsAttackingRockTarget(__instance.gameObject))
				{
					component.TryApplyRockAoe();
					return false;
				}
				return true;
			}
			catch
			{
				return true;
			}
		}
	}

	[HarmonyPatch(typeof(MineRock5), "Damage")]
	private static class MineRock5_SuppressHarvestPhysical
	{
		private static bool Prefix(MineRock5 __instance, HitData hit)
		{
			try
			{
				if (hit == null || HarvestController.IsApplyingAoeDamage)
				{
					return true;
				}
				Character attacker = hit.GetAttacker();
				if (attacker == null)
				{
					return true;
				}
				HarvestController component = attacker.GetComponent<HarvestController>();
				if (component != null && component.IsAttackingRockTarget(__instance.gameObject))
				{
					component.TryApplyRockAoe();
					return false;
				}
				return true;
			}
			catch
			{
				return true;
			}
		}
	}

	[HarmonyPatch(typeof(TreeBase), "Damage")]
	private static class TreeBase_SuppressHarvestPhysical
	{
		private static bool Prefix(TreeBase __instance, HitData hit)
		{
			try
			{
				if (hit == null || HarvestController.IsApplyingAoeDamage)
				{
					return true;
				}
				Character attacker = hit.GetAttacker();
				if (attacker == null)
				{
					return true;
				}
				HarvestController component = attacker.GetComponent<HarvestController>();
				if (component != null && component.IsAttackingTreeTarget(__instance.gameObject))
				{
					component.TryApplyTreeAoe();
					return false;
				}
				return true;
			}
			catch
			{
				return true;
			}
		}
	}

	[HarmonyPatch(typeof(TreeLog), "Damage")]
	private static class TreeLog_SuppressHarvestPhysical
	{
		private static bool Prefix(TreeLog __instance, HitData hit)
		{
			try
			{
				if (hit == null || HarvestController.IsApplyingAoeDamage)
				{
					return true;
				}
				Character attacker = hit.GetAttacker();
				if (attacker == null)
				{
					return true;
				}
				HarvestController component = attacker.GetComponent<HarvestController>();
				if (component != null && component.IsAttackingTreeTarget(__instance.gameObject))
				{
					component.TryApplyTreeAoe();
					return false;
				}
				return true;
			}
			catch
			{
				return true;
			}
		}
	}

	[HarmonyPatch(typeof(Attack), "SpawnOnHitTerrain")]
	private static class SpawnOnHitTerrain_NoNpcTerrain
	{
		private static bool Prefix(Character character)
		{
			try
			{
				// 1.4.150: companions only - it blocked every creature's ground effects (and Aoe.OnHit uses the result)
				if (character == null || character.IsPlayer() || character.GetComponent<CompanionSetup>() == null)
				{
					return true;
				}
				character.GetComponent<HarvestController>()?.NotifyTerrainHit();
				return false;
			}
			catch
			{
				// fail-open: an exception here would abort the game's ground-effect spawn
				return true;
			}
		}
	}

	[HarmonyPatch(typeof(SE_Poison), "AddDamage")]
	private static class SE_Poison_AddDamage_Patch
	{
		private static void Prefix(SE_Poison __instance, ref float __state)
		{
			__state = __instance.m_TTLPerDamage;
			if (__instance.m_character != null && __instance.m_character.GetComponent<CompanionSetup>() != null)
			{
				__instance.m_TTLPerDamage = __instance.m_TTLPerDamagePlayer;
			}
		}

		private static void Postfix(SE_Poison __instance, float __state)
		{
			__instance.m_TTLPerDamage = __state;
		}
	}

	private static float _lastHealthLogTime;

	private const float HealthLogInterval = 5f;
}
