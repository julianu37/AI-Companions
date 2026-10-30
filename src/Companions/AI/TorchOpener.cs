using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace Companions;

internal static class TorchOpener
{
	private class State
	{
		public Character target;

		public float startTime;

		public float readyAt;
	}

	private static Hashtable _active = new Hashtable();

	private static Hashtable _cooldown = new Hashtable();

	private static FieldInfo _rightField;

	private static int _logs;

	internal static bool TrySelect(Humanoid h, Character target, int stance, bool huntMode)
	{
		try
		{
			if (h == null)
			{
				return false;
			}
			int instanceID = h.GetInstanceID();
			State state = (State)_active[instanceID];
			if (state != null)
			{
				if (!Validate(h, state, target, out var why))
				{
					End(h, state, why);
					return false;
				}
				return true;
			}
			if (!Enabled() || huntMode || target == null || target.IsDead())
			{
				return false;
			}
			if (stance != 0 && stance != 3)
			{
				return false;
			}
			if (h.GetHealthPercentage() < RetreatPct())
			{
				return false;
			}
			if (target.GetMaxHealth() < MinHealth())
			{
				return false;
			}
			if (!FireWeak(target))
			{
				return false;
			}
			if (Burning(target) || Wet(target))
			{
				return false;
			}
			if (OnCooldown(h, target))
			{
				return false;
			}
			ItemDrop.ItemData itemData = FindTorch(h);
			if (itemData == null)
			{
				return false;
			}
			if (IsTorch(Right(h)))
			{
				return false;
			}
			if (!HasOtherMeleeWeapon(h))
			{
				return false;
			}
			if (!h.EquipItem(itemData))
			{
				SetCooldown(h, target);
				return false;
			}
			State state2 = new State();
			state2.target = target;
			state2.startTime = Time.time;
			state2.readyAt = Time.time + 0.4f;
			_active[instanceID] = state2;
			Info("[TorchOpener] igniting fire-weak target with torch.");
			return true;
		}
		catch (Exception ex)
		{
			Info("[TorchOpener] error: " + ex.Message);
			return false;
		}
	}

	internal static void Tick(Humanoid h)
	{
		try
		{
			if (_active.Count != 0 && !(h == null))
			{
				State state = (State)_active[h.GetInstanceID()];
				if (state != null && !Validate(h, state, state.target, out var why))
				{
					End(h, state, why);
				}
			}
		}
		catch (Exception)
		{
		}
	}

	internal static bool IsActive(Humanoid h)
	{
		try
		{
			if (h == null)
			{
				return false;
			}
			State state = (State)_active[h.GetInstanceID()];
			if (state == null)
			{
				return false;
			}
			if (Time.time > state.startTime + Timeout() + 1.5f)
			{
				End(h, state, "expired");
				return false;
			}
			return true;
		}
		catch (Exception)
		{
			return false;
		}
	}

	private static void Info(string s)
	{
		if (_logs < 300)
		{
			_logs++;
			CompanionsPlugin.Log?.LogInfo(s);
		}
	}

	private static bool Enabled()
	{
		try
		{
			return ModConfig.UseTorchOpener != null && ModConfig.UseTorchOpener.Value;
		}
		catch (Exception)
		{
			return false;
		}
	}

	private static float Timeout()
	{
		try
		{
			if (ModConfig.TorchOpenerTimeout != null)
			{
				return ModConfig.TorchOpenerTimeout.Value;
			}
		}
		catch (Exception)
		{
		}
		return 4f;
	}

	private static float CooldownSecs()
	{
		try
		{
			if (ModConfig.TorchOpenerCooldown != null)
			{
				return ModConfig.TorchOpenerCooldown.Value;
			}
		}
		catch (Exception)
		{
		}
		return 10f;
	}

	private static float MinHealth()
	{
		try
		{
			if (ModConfig.TorchOpenerMinTargetHealth != null)
			{
				return ModConfig.TorchOpenerMinTargetHealth.Value;
			}
		}
		catch (Exception)
		{
		}
		return 40f;
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

	private static ItemDrop.ItemData Right(Humanoid h)
	{
		if (_rightField == null)
		{
			_rightField = typeof(Humanoid).GetField("m_rightItem", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		}
		return (!(_rightField == null)) ? ((ItemDrop.ItemData)_rightField.GetValue(h)) : null;
	}

	private static bool IsTorch(ItemDrop.ItemData it)
	{
		return it != null && it.m_shared != null && it.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Torch;
	}

	private static bool Usable(ItemDrop.ItemData it)
	{
		if (it == null || it.m_shared == null)
		{
			return false;
		}
		return !it.m_shared.m_useDurability || it.m_durability > 0f;
	}

	private static ItemDrop.ItemData FindTorch(Humanoid h)
	{
		Inventory inventory = h.GetInventory();
		if (inventory == null)
		{
			return null;
		}
		IList allItems = inventory.GetAllItems();
		for (int i = 0; i < allItems.Count; i++)
		{
			ItemDrop.ItemData itemData = (ItemDrop.ItemData)allItems[i];
			if (IsTorch(itemData) && Usable(itemData))
			{
				return itemData;
			}
		}
		return null;
	}

	private static bool HasOtherMeleeWeapon(Humanoid h)
	{
		Inventory inventory = h.GetInventory();
		if (inventory == null)
		{
			return false;
		}
		IList allItems = inventory.GetAllItems();
		for (int i = 0; i < allItems.Count; i++)
		{
			ItemDrop.ItemData itemData = (ItemDrop.ItemData)allItems[i];
			if (itemData != null && itemData.m_shared != null && Usable(itemData))
			{
				ItemDrop.ItemData.ItemType itemType = itemData.m_shared.m_itemType;
				if ((itemType == ItemDrop.ItemData.ItemType.OneHandedWeapon || itemType == ItemDrop.ItemData.ItemType.TwoHandedWeapon || itemType == ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft) && !CompanionCombatAI.MHR_IsProjectileWeapon(itemData) && !(itemData.GetDamage().m_pickaxe > 0f))
				{
					return true;
				}
			}
		}
		return false;
	}

	private static bool FireWeak(Character target)
	{
		HitData.DamageModifier modifier = target.GetDamageModifiers().GetModifier(HitData.DamageType.Fire);
		return modifier == HitData.DamageModifier.Weak || modifier == HitData.DamageModifier.VeryWeak || modifier == HitData.DamageModifier.SlightlyWeak;
	}

	private static bool Burning(Character target)
	{
		return target.GetSEMan()?.HaveStatusEffect(SEMan.s_statusEffectBurning) ?? false;
	}

	private static bool Wet(Character target)
	{
		return target.GetSEMan()?.HaveStatusEffect(SEMan.s_statusEffectWet) ?? false;
	}

	private static string CoolKey(Humanoid h, Character target)
	{
		return $"{h.GetInstanceID()}:{target.GetInstanceID()}";
	}

	private static bool OnCooldown(Humanoid h, Character target)
	{
		object obj = _cooldown[CoolKey(h, target)];
		return obj != null && Time.time < (float)obj;
	}

	private static void SetCooldown(Humanoid h, Character target)
	{
		if (!(target == null))
		{
			if (_cooldown.Count > 200)
			{
				_cooldown.Clear();
			}
			_cooldown[CoolKey(h, target)] = Time.time + CooldownSecs();
		}
	}

	private static bool Validate(Humanoid h, State s, Character target, out string why)
	{
		why = null;
		if (Time.time > s.startTime + Timeout())
		{
			why = "timeout";
			return false;
		}
		if (target == null || target.IsDead())
		{
			why = "target gone";
			return false;
		}
		if (s.target != null && target != s.target)
		{
			why = "target changed";
			return false;
		}
		ItemDrop.ItemData it = Right(h);
		if (!IsTorch(it) || !Usable(it))
		{
			why = "torch not in hand";
			return false;
		}
		if (Burning(target))
		{
			why = "target burning";
			return false;
		}
		if (h.GetHealthPercentage() < RetreatPct())
		{
			why = "low health";
			return false;
		}
		return true;
	}

	private static void End(Humanoid h, State s, string why)
	{
		try
		{
			_active.Remove(h.GetInstanceID());
			SetCooldown(h, s.target);
			Info("[TorchOpener] done (" + why + ") - back to best weapon.");
			Component component = h.GetComponent("CompanionSetup");
			if (component != null)
			{
				MethodInfo method = component.GetType().GetMethod("AutoEquipBest", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (method != null)
				{
					method.Invoke(component, new object[1] { false });
				}
			}
		}
		catch (Exception ex)
		{
			Info("[TorchOpener] end failed: " + ex.Message);
		}
	}
}
