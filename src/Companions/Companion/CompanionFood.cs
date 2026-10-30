using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Companions;

public class CompanionFood : MonoBehaviour
{
	public struct FoodEffect
	{
		public string ItemName;

		public string ItemPrefabName;

		public float HealthBonus;

		public float StaminaBonus;

		public float EitrBonus;

		public float FoodRegen;

		public float RemainingTime;

		public float TotalTime;

		public bool IsActive
		{
			get
			{
				if (RemainingTime > 0f)
				{
					return !string.IsNullOrEmpty(ItemName);
				}
				return false;
			}
		}
	}

	private enum MeadKind
	{
		Unknown,
		Health,
		Stamina
	}

	private const int MaxFoodSlotsCapacity = 5;

	private FoodEffect[] _foods = new FoodEffect[5];

	private ZNetView _nview;

	// 1.5.90: the "already active" rejection is logged once per food until it changes (log 32bba563: ~250 lines with honey
	// as its only food)
	private string _lastActiveReject;

	// 1.5.68: "no food found" logged once per slot until the inventory or its food changes (log 9aef82b6: 816 lines)
	private int _noFoodCount = -1;

	private int _noFoodSlots;

	private Humanoid _humanoid;

	private Character _character;

	private SEMan _seman;

	private ZSyncAnimation _zanim;

	private CompanionStamina _stamina;

	private CompanionSetup _setup;

	private bool _initialized;

	private float _saveTimer;

	private float _consumeTimer;

	private float _remoteSyncTimer;

	private float _foodRegenTimer;

	private float _meadCheckTimer;

	private float _meadCooldownTimer;

	private const float SaveInterval = 5f;

	private const float RemoteSyncInterval = 0.5f;

	private const float MeadCheckInterval = 2f;

	private static readonly int[] FoodHashes;

	public static int MaxFoodSlots => ModConfig.MaxFoodSlots.Value;

	public static float BaseHealth => ModConfig.BaseHealth.Value;

	public static float BaseStamina => ModConfig.BaseStamina.Value;

	private static float ConsumeCheckInterval => ModConfig.ConsumeCheckInterval.Value;

	private static float FoodRegenInterval => ModConfig.FoodRegenInterval.Value;

	private static float MeadCooldown => ModConfig.MeadCooldown.Value;

	private static float HealthMeadThreshold => ModConfig.HealthMeadThreshold.Value;

	private static float StaminaMeadThreshold => ModConfig.StaminaMeadThreshold.Value;

	public float TotalHealthBonus
	{
		get
		{
			float num = 0f;
			for (int i = 0; i < MaxFoodSlots; i++)
			{
				if (_foods[i].IsActive)
				{
					num += GetScaledBonus(_foods[i].HealthBonus, _foods[i]);
				}
			}
			return num;
		}
	}

	public float TotalStaminaBonus
	{
		get
		{
			float num = 0f;
			for (int i = 0; i < MaxFoodSlots; i++)
			{
				if (_foods[i].IsActive)
				{
					num += GetScaledBonus(_foods[i].StaminaBonus, _foods[i]);
				}
			}
			return num;
		}
	}

	public float TotalEitrBonus
	{
		get
		{
			float num = 0f;
			for (int i = 0; i < MaxFoodSlots; i++)
			{
				if (_foods[i].IsActive)
				{
					num += GetScaledBonus(_foods[i].EitrBonus, _foods[i]);
				}
			}
			return num;
		}
	}

	static CompanionFood()
	{
		FoodHashes = new int[5];
		for (int i = 0; i < 5; i++)
		{
			FoodHashes[i] = $"HC_Food_{i}".GetStableHashCode();
		}
	}

	private static float GetScaledBonus(float baseBonus, FoodEffect food)
	{
		if (!food.IsActive)
		{
			return 0f;
		}
		float num = Mathf.Max(1f, food.TotalTime);
		float f = Mathf.Clamp01(food.RemainingTime / num);
		return baseBonus * Mathf.Pow(f, 0.3f);
	}

	public FoodEffect GetFood(int slot)
	{
		if (slot < 0 || slot >= MaxFoodSlots)
		{
			return default(FoodEffect);
		}
		return _foods[slot];
	}

	private void Awake()
	{
		_nview = GetComponent<ZNetView>();
		_humanoid = GetComponent<Humanoid>();
		_character = GetComponent<Character>();
		_seman = ((_character != null) ? _character.GetSEMan() : null);
		_zanim = GetComponent<ZSyncAnimation>();
		_stamina = GetComponent<CompanionStamina>();
		_setup = GetComponent<CompanionSetup>();
	}

	private void Start()
	{
		if (!_initialized)
		{
			TryInit();
		}
	}

	private void TryInit()
	{
		if (_initialized || _nview == null || _nview.GetZDO() == null)
		{
			return;
		}
		if (_seman == null && _character != null)
		{
			_seman = _character.GetSEMan();
		}
		LoadFromZDO();
		ClampHealth();
		_initialized = true;
		int num = 0;
		for (int i = 0; i < MaxFoodSlots; i++)
		{
			if (_foods[i].IsActive)
			{
				num++;
			}
		}
		CompanionsPlugin.Log.LogDebug($"[Food] Initialized — {num}/{MaxFoodSlots} active food slots " + $"hpBonus={TotalHealthBonus:F0} stamBonus={TotalStaminaBonus:F0} " + "companion=\"" + (_character?.m_name ?? "?") + "\"");
	}

	private void LoadFromZDO()
	{
		if (_nview == null || _nview.GetZDO() == null)
		{
			return;
		}
		ZDO zDO = _nview.GetZDO();
		for (int i = 0; i < MaxFoodSlots; i++)
		{
			string data = zDO.GetString(FoodHashes[i]);
			_foods[i] = DeserializeFood(data);
			if (_foods[i].IsActive)
			{
				CompanionsPlugin.Log.LogDebug($"[Food] ZDO load slot {i} — \"{_foods[i].ItemName}\" " + $"hp={_foods[i].HealthBonus:F0} stam={_foods[i].StaminaBonus:F0} " + $"remaining={_foods[i].RemainingTime:F0}/{_foods[i].TotalTime:F0}s");
			}
		}
	}

	private void ClampHealth()
	{
		if (_character == null)
		{
			return;
		}
		float num = BaseHealth + TotalHealthBonus;
		float health = _character.GetHealth();
		if (health > num)
		{
			if (health - num > 0.5f)
			{
				CompanionsPlugin.Log.LogDebug($"[Food] ClampHealth — {health:F1} → {num:F1} (food expired?) " + "companion=\"" + _character.m_name + "\"");
			}
			_character.SetHealth(num);
		}
	}

	private void Update()
	{
		if (!_initialized)
		{
			TryInit();
		}
		else
		{
			if (_nview == null || _nview.GetZDO() == null)
			{
				return;
			}
			if (!_nview.IsOwner())
			{
				_remoteSyncTimer -= Time.deltaTime;
				if (_remoteSyncTimer <= 0f)
				{
					_remoteSyncTimer = 0.5f;
					LoadFromZDO();
				}
				return;
			}
			float deltaTime = Time.deltaTime;
			bool flag = false;
			bool flag2 = false;
			for (int i = 0; i < MaxFoodSlots; i++)
			{
				FoodEffect foodEffect = _foods[i];
				if (foodEffect.IsActive)
				{
					flag2 = true;
					foodEffect.RemainingTime -= deltaTime;
					if (foodEffect.RemainingTime <= 0f)
					{
						CompanionsPlugin.Log.LogDebug($"[Food] Slot {i} expired — \"{foodEffect.ItemName}\" " + $"(hp={foodEffect.HealthBonus:F0} stam={foodEffect.StaminaBonus:F0} " + $"eitr={foodEffect.EitrBonus:F0} regen={foodEffect.FoodRegen:F1}) " + "companion=\"" + (_character?.m_name ?? "?") + "\"");
						_foods[i] = default(FoodEffect);
						flag = true;
					}
					else
					{
						_foods[i] = foodEffect;
					}
				}
			}
			if (flag || flag2)
			{
				ClampHealth();
			}
			_foodRegenTimer += deltaTime;
			if (_foodRegenTimer >= FoodRegenInterval)
			{
				_foodRegenTimer = 0f;
				float num = 0f;
				for (int j = 0; j < MaxFoodSlots; j++)
				{
					if (_foods[j].IsActive)
					{
						num += _foods[j].FoodRegen;
					}
				}
				if (num > 0f && _character != null)
				{
					CompanionRestedBuff component = GetComponent<CompanionRestedBuff>();
					if (component != null && component.IsRested)
					{
						num *= CompanionRestedBuff.HealthRegenMultiplier;
					}
					// 1.4.95: wet, cold, meads and other status effects change regen like they do for a player
					if (ModConfig.StatusEffectsOnRegen?.Value ?? true)
					{
						float mult = 1f;
						_character.GetSEMan()?.ModifyHealthRegen(ref mult);
						num *= mult;
					}
					float health = _character.GetHealth();
					_character.Heal(num);
					float health2 = _character.GetHealth();
					CompanionsPlugin.Log.LogDebug($"[Food] Regen tick — healed {num:F1} HP " + $"({health:F1} → {health2:F1} / {_character.GetMaxHealth():F1}) " + "companion=\"" + _character.m_name + "\"");
				}
			}
			_consumeTimer -= deltaTime;
			if (_consumeTimer <= 0f)
			{
				_consumeTimer = ConsumeCheckInterval;
				TryAutoConsume();
			}
			if (_meadCooldownTimer > 0f)
			{
				_meadCooldownTimer -= deltaTime;
			}
			_meadCheckTimer -= deltaTime;
			if (_meadCheckTimer <= 0f)
			{
				_meadCheckTimer = 2f;
				TryConsumeMead();
			}
			_saveTimer += deltaTime;
			if (_saveTimer >= 5f)
			{
				_saveTimer = 0f;
				SaveToZDO();
			}
		}
	}

	// 1.5.44: eat now (after a revive, once the gear is back on - health and stamina back up)
	internal void EatNow()
	{
		_consumeTimer = ConsumeCheckInterval;
		TryAutoConsume();
	}

	private void TryAutoConsume()
	{
		if (_humanoid == null || _character == null)
		{
			return;
		}
		Inventory inventory = _humanoid.GetInventory();
		if (inventory == null)
		{
			return;
		}
		if (_seman == null)
		{
			_seman = _character.GetSEMan();
		}
		bool flag = false;
		for (int i = 0; i < MaxFoodSlots; i++)
		{
			if (!_foods[i].IsActive)
			{
				ItemDrop.ItemData itemData = FindFoodForSlot(inventory);
				if (itemData == null)
				{
					int count = inventory.GetAllItems().Count;
					if (_noFoodCount != count)
					{
						_noFoodCount = count;
						_noFoodSlots = 0;
					}
					if ((_noFoodSlots & (1 << i)) != 0)
					{
						continue;
					}
					_noFoodSlots |= 1 << i;
					CompanionsPlugin.Log.LogDebug($"[Food] AutoConsume — slot {i} empty, no food found in inventory " + string.Format("(items={0}) companion=\"{1}\"", inventory.GetAllItems().Count, _character?.m_name ?? "?"));
				}
				else if (!CanConsumeFoodItem(itemData))
				{
					CompanionsPlugin.Log.LogDebug($"[Food] AutoConsume — slot {i} empty, found \"{itemData.m_shared?.m_name}\" " + "but CanConsume=false companion=\"" + (_character?.m_name ?? "?") + "\"");
				}
				else if (ConsumeIntoSlot(inventory, itemData, i))
				{
					flag = true;
					_noFoodCount = -1;
				}
			}
		}
		// 1.5.66: top up a food that's about to run out, like a player - the same food again once it has [Food]
		// EatAgainMinutes left (the game only allows it under half its time) - instead of waiting for the slot to empty
		// (user: "The NPC should not wait until the food has expired to eat more")
		float soon = (ModConfig.FoodEatAgainMinutes?.Value ?? 3f) * 60f;
		if (!flag && soon > 0f && !_humanoid.InAttack())
		{
			for (int j = 0; j < MaxFoodSlots && !flag; j++)
			{
				if (!_foods[j].IsActive || _foods[j].RemainingTime > soon || !CanEatAgain(j))
				{
					continue;
				}
				ItemDrop.ItemData same = null;
				foreach (ItemDrop.ItemData it in inventory.GetAllItems())
				{
					if (IsFoodItem(it) && it.m_shared.m_name == _foods[j].ItemName)
					{
						same = it;
						break;
					}
				}
				if (same != null && CanConsumeFoodItem(same, isRefresh: true))
				{
					float left = _foods[j].RemainingTime;
					if (ConsumeIntoSlot(inventory, same, j))
					{
						flag = true;
						CompanionsPlugin.Log.LogDebug($"[Food] Topped up \"{same.m_shared.m_name}\" in slot {j} with {left:F0} s left companion=\"" + (_character?.m_name ?? "?") + "\"");
					}
				}
			}
		}
		if (flag)
		{
			ClampHealth();
			SaveToZDO();
		}
	}

	private void TryConsumeMead()
	{
		if (_humanoid == null || _character == null || _meadCooldownTimer > 0f || _humanoid.InAttack() || _humanoid.InDodge() || _character.IsStaggering())
		{
			return;
		}
		Inventory inventory = _humanoid.GetInventory();
		if (inventory == null)
		{
			return;
		}
		if (_seman == null)
		{
			_seman = _character.GetSEMan();
		}
		float num = ((_character.GetMaxHealth() > 0f) ? (_character.GetHealth() / _character.GetMaxHealth()) : 1f);
		float num2 = ((_stamina != null) ? _stamina.GetStaminaPercentage() : 1f);
		if (num < HealthMeadThreshold)
		{
			ItemDrop.ItemData itemData = FindMead(inventory, MeadKind.Health);
			if (itemData != null)
			{
				if (ConsumeMead(inventory, itemData, MeadKind.Health))
				{
					return;
				}
			}
			else
			{
				CompanionsPlugin.Log.LogDebug($"[Food] Mead check — hp={num * 100f:F0}% below threshold, " + "no health mead in inventory companion=\"" + (_character?.m_name ?? "?") + "\"");
			}
		}
		if (num2 < StaminaMeadThreshold)
		{
			ItemDrop.ItemData itemData2 = FindMead(inventory, MeadKind.Stamina);
			if (itemData2 != null)
			{
				ConsumeMead(inventory, itemData2, MeadKind.Stamina);
			}
			else
			{
				CompanionsPlugin.Log.LogDebug($"[Food] Mead check — stam={num2 * 100f:F0}% below threshold, " + "no stamina mead in inventory companion=\"" + (_character?.m_name ?? "?") + "\"");
			}
		}
	}

	private void SaveToZDO()
	{
		if (!(_nview == null) && _nview.GetZDO() != null && _nview.IsOwner())
		{
			ZDO zDO = _nview.GetZDO();
			for (int i = 0; i < MaxFoodSlots; i++)
			{
				zDO.Set(FoodHashes[i], SerializeFood(i));
			}
		}
	}

	private void OnDestroy()
	{
		if (_initialized)
		{
			SaveToZDO();
		}
	}

	private ItemDrop.ItemData FindFoodForSlot(Inventory inv)
	{
		List<ItemDrop.ItemData> allItemsInGridOrder = inv.GetAllItemsInGridOrder();
		// 1.4.111: fishing - the most stamina first (user: "if fishing the NPC should prefer stamina food")
		int mode = GetComponent<ZNetView>()?.GetZDO()?.GetInt(CompanionSetup.ActionModeHash) ?? 0;
		if (mode == CompanionSetup.ModeFish)
		{
			ItemDrop.ItemData best = null;
			for (int j = 0; j < allItemsInGridOrder.Count; j++)
			{
				ItemDrop.ItemData it = allItemsInGridOrder[j];
				if ((best == null || it.m_shared.m_foodStamina > best.m_shared.m_foodStamina) && CanConsumeFoodItem(it))
				{
					best = it;
				}
			}
			if (best != null)
			{
				return best;
			}
		}
		for (int i = 0; i < allItemsInGridOrder.Count; i++)
		{
			ItemDrop.ItemData itemData = allItemsInGridOrder[i];
			if (CanConsumeFoodItem(itemData))
			{
				return itemData;
			}
		}
		return null;
	}

	private bool CanConsumeFoodItem(ItemDrop.ItemData item, bool isRefresh = false)
	{
		if (!IsFoodItem(item))
		{
			return false;
		}
		string text = item.m_shared?.m_name ?? "?";
		CookController cook = GetComponent<CookController>();
		if (cook != null && cook.IsWorkFood(item))
		{
			return false;
		}
		if (!isRefresh && IsFoodAlreadyActive(item.m_shared.m_name))
		{
			if (_lastActiveReject != text)
			{
				_lastActiveReject = text;
				CompanionsPlugin.Log.LogDebug("[Food] CanConsume REJECTED \"" + text + "\" — already active in another slot (logged once)");
			}
			return false;
		}
		_lastActiveReject = null;
		if (!CanApplyConsumeStatus(item))
		{
			CompanionsPlugin.Log.LogDebug("[Food] CanConsume REJECTED \"" + text + "\" — status effect conflict");
			return false;
		}
		return true;
	}

	private bool ConsumeIntoSlot(Inventory inv, ItemDrop.ItemData item, int slot)
	{
		if (inv == null || item == null || slot < 0 || slot >= MaxFoodSlots)
		{
			return false;
		}
		float num = Mathf.Max(1f, item.m_shared.m_foodBurnTime);
		_foods[slot] = new FoodEffect
		{
			ItemName = item.m_shared.m_name,
			ItemPrefabName = GetItemPrefabName(item),
			HealthBonus = item.m_shared.m_food,
			StaminaBonus = item.m_shared.m_foodStamina,
			EitrBonus = item.m_shared.m_foodEitr,
			FoodRegen = item.m_shared.m_foodRegen,
			TotalTime = num,
			RemainingTime = num
		};
		inv.RemoveOneItem(item);
		if (_seman != null && item.m_shared.m_consumeStatusEffect != null)
		{
			_seman.AddStatusEffect(item.m_shared.m_consumeStatusEffect, resetTime: true, 0, 0f, -1);
		}
		string text = ((_setup != null && _setup.CanWearArmor()) ? "eat" : "consume");
		if (_zanim != null)
		{
			_zanim.SetTrigger(text);
		}
		CompanionsPlugin.Log.LogDebug("[Food] Consume anim trigger=\"" + text + "\" " + $"canWearArmor={_setup?.CanWearArmor()} companion=\"{_character?.m_name}\"");
		if (_humanoid != null && _humanoid.m_consumeItemEffects != null)
		{
			_humanoid.m_consumeItemEffects.Create(base.transform.position, Quaternion.identity, null, 1f, -1, ZDOID.None);
		}
		float num2 = BaseHealth + TotalHealthBonus;
		float health = _character.GetHealth();
		if (health < num2)
		{
			_character.SetHealth(Mathf.Min(health + _foods[slot].HealthBonus, num2));
		}
		float health2 = _character.GetHealth();
		bool flag = item.m_shared.m_consumeStatusEffect != null;
		CompanionsPlugin.Log.LogDebug($"[Food] Consumed \"{item.m_shared.m_name}\" → slot {slot} " + $"hp={item.m_shared.m_food:F0} stam={item.m_shared.m_foodStamina:F0} " + $"eitr={item.m_shared.m_foodEitr:F0} regen={item.m_shared.m_foodRegen:F1} " + $"burnTime={num:F0}s statusEffect={flag} " + $"health {health:F1} → {health2:F1} / {num2:F1} " + $"totalStamBonus={TotalStaminaBonus:F1} " + "companion=\"" + (_character?.m_name ?? "?") + "\"");
		return true;
	}

	private static string GetItemPrefabName(ItemDrop.ItemData item)
	{
		if (item == null)
		{
			return "";
		}
		if (item.m_dropPrefab != null)
		{
			return item.m_dropPrefab.name;
		}
		List<GameObject> list = ObjectDB.instance?.m_items;
		if (list == null || item.m_shared == null)
		{
			return "";
		}
		for (int i = 0; i < list.Count; i++)
		{
			ItemDrop itemDrop = ((list[i] != null) ? list[i].GetComponent<ItemDrop>() : null);
			if (itemDrop?.m_itemData?.m_shared != null && itemDrop.m_itemData.m_shared == item.m_shared)
			{
				return list[i].name;
			}
		}
		return "";
	}

	public bool TryConsumeItem(ItemDrop.ItemData item)
	{
		if (!_initialized)
		{
			TryInit();
		}
		if (_nview == null || !_nview.IsOwner())
		{
			return false;
		}
		if (_humanoid == null || _character == null || item == null)
		{
			return false;
		}
		Inventory inventory = _humanoid.GetInventory();
		if (inventory == null || !inventory.ContainsItem(item))
		{
			return false;
		}
		string text = item.m_shared?.m_name ?? "?";
		int num = FindEmptyFoodSlot();
		if (num >= 0)
		{
			if (!CanConsumeFoodItem(item))
			{
				CompanionsPlugin.Log.LogDebug("[Food] TryConsumeItem REJECTED \"" + text + "\" — " + $"empty slot {num} available but CanConsume=false");
				return false;
			}
		}
		else
		{
			num = FindRefreshableSlotForFood(item.m_shared?.m_name);
			if (num < 0)
			{
				num = FindMostDepletedRefreshableSlot();
			}
			if (num < 0)
			{
				CompanionsPlugin.Log.LogDebug("[Food] TryConsumeItem REJECTED \"" + text + "\" — all 3 slots occupied, none refreshable");
				return false;
			}
			if (!CanConsumeFoodItem(item, isRefresh: true))
			{
				CompanionsPlugin.Log.LogDebug("[Food] TryConsumeItem REJECTED \"" + text + "\" — " + $"refresh slot {num} found but CanConsume(refresh)=false");
				return false;
			}
		}
		CompanionsPlugin.Log.LogDebug($"[Food] TryConsumeItem \"{text}\" → slot {num} " + "companion=\"" + (_character?.m_name ?? "?") + "\"");
		if (!ConsumeIntoSlot(inventory, item, num))
		{
			return false;
		}
		ClampHealth();
		SaveToZDO();
		return true;
	}

	private int FindEmptyFoodSlot()
	{
		for (int i = 0; i < MaxFoodSlots; i++)
		{
			if (!_foods[i].IsActive)
			{
				return i;
			}
		}
		return -1;
	}

	private int FindRefreshableSlotForFood(string itemName)
	{
		if (string.IsNullOrEmpty(itemName))
		{
			return -1;
		}
		for (int i = 0; i < MaxFoodSlots; i++)
		{
			if (_foods[i].IsActive && !(_foods[i].ItemName != itemName) && CanEatAgain(i))
			{
				return i;
			}
		}
		return -1;
	}

	private int FindMostDepletedRefreshableSlot()
	{
		int result = -1;
		float num = float.MaxValue;
		for (int i = 0; i < MaxFoodSlots; i++)
		{
			if (CanEatAgain(i))
			{
				float remainingTime = _foods[i].RemainingTime;
				if (remainingTime < num)
				{
					num = remainingTime;
					result = i;
				}
			}
		}
		return result;
	}

	private bool CanEatAgain(int slot)
	{
		if (slot < 0 || slot >= MaxFoodSlots)
		{
			return false;
		}
		FoodEffect foodEffect = _foods[slot];
		if (!foodEffect.IsActive)
		{
			return false;
		}
		float num = Mathf.Max(1f, foodEffect.TotalTime);
		return foodEffect.RemainingTime < num * 0.5f;
	}

	private static bool IsFoodItem(ItemDrop.ItemData item)
	{
		if (item == null || item.m_shared == null)
		{
			return false;
		}
		if (item.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Consumable)
		{
			return false;
		}
		if (!(item.m_shared.m_food > 0f) && !(item.m_shared.m_foodStamina > 0f))
		{
			return item.m_shared.m_foodEitr > 0f;
		}
		return true;
	}

	private bool IsFoodAlreadyActive(string itemName)
	{
		if (string.IsNullOrEmpty(itemName))
		{
			return false;
		}
		for (int i = 0; i < MaxFoodSlots; i++)
		{
			if (_foods[i].IsActive && _foods[i].ItemName == itemName)
			{
				return true;
			}
		}
		return false;
	}

	private bool CanApplyConsumeStatus(ItemDrop.ItemData item)
	{
		if (_seman == null || item == null || item.m_shared == null)
		{
			return true;
		}
		StatusEffect consumeStatusEffect = item.m_shared.m_consumeStatusEffect;
		if (consumeStatusEffect == null)
		{
			return true;
		}
		if (!_seman.HaveStatusEffect(consumeStatusEffect.NameHash()))
		{
			return !_seman.HaveStatusEffectCategory(consumeStatusEffect.m_category);
		}
		return false;
	}

	private ItemDrop.ItemData FindMead(Inventory inv, MeadKind wanted)
	{
		List<ItemDrop.ItemData> allItemsInGridOrder = inv.GetAllItemsInGridOrder();
		for (int i = 0; i < allItemsInGridOrder.Count; i++)
		{
			ItemDrop.ItemData itemData = allItemsInGridOrder[i];
			if (IsMeadItem(itemData) && ClassifyMead(itemData) == wanted)
			{
				StatusEffect consumeStatusEffect = itemData.m_shared.m_consumeStatusEffect;
				if (!(consumeStatusEffect != null) || _seman == null || (!_seman.HaveStatusEffect(consumeStatusEffect.NameHash()) && !_seman.HaveStatusEffectCategory(consumeStatusEffect.m_category)))
				{
					return itemData;
				}
			}
		}
		return null;
	}

	private bool ConsumeMead(Inventory inv, ItemDrop.ItemData item, MeadKind kind)
	{
		if (inv == null || item == null)
		{
			return false;
		}
		string arg = item.m_shared?.m_name ?? "?";
		string arg2 = ((item.m_dropPrefab != null) ? item.m_dropPrefab.name : "?");
		StatusEffect consumeStatusEffect = item.m_shared.m_consumeStatusEffect;
		if (consumeStatusEffect != null && _seman != null)
		{
			_seman.AddStatusEffect(consumeStatusEffect, resetTime: true, 0, 0f, -1);
		}
		if (kind == MeadKind.Stamina && _stamina != null && consumeStatusEffect is SE_Stats { m_staminaUpFront: >0f } sE_Stats)
		{
			_stamina.Restore(sE_Stats.m_staminaUpFront);
		}
		string text = ((_setup != null && _setup.CanWearArmor()) ? "eat" : "consume");
		if (_zanim != null)
		{
			_zanim.SetTrigger(text);
		}
		CompanionsPlugin.Log.LogDebug("[Food] Mead anim trigger=\"" + text + "\" " + $"canWearArmor={_setup?.CanWearArmor()} companion=\"{_character?.m_name}\"");
		if (_humanoid != null && _humanoid.m_consumeItemEffects != null)
		{
			_humanoid.m_consumeItemEffects.Create(base.transform.position, Quaternion.identity, null, 1f, -1, ZDOID.None);
		}
		inv.RemoveOneItem(item);
		float num = ((_character.GetMaxHealth() > 0f) ? (_character.GetHealth() / _character.GetMaxHealth() * 100f) : 0f);
		float num2 = ((_stamina != null) ? (_stamina.GetStaminaPercentage() * 100f) : 0f);
		CompanionsPlugin.Log.LogDebug($"[Food] MEAD consumed \"{arg}\" (prefab={arg2}) kind={kind} " + $"hp={num:F0}% stam={num2:F0}% " + string.Format("hasSE={0} companion=\"{1}\"", consumeStatusEffect != null, _character?.m_name ?? "?"));
		_meadCooldownTimer = MeadCooldown;
		return true;
	}

	private static bool IsMeadItem(ItemDrop.ItemData item)
	{
		return IsPotionItem(item);
	}

	private static MeadKind ClassifyMead(ItemDrop.ItemData item)
	{
		StatusEffect statusEffect = item.m_shared?.m_consumeStatusEffect;
		if (statusEffect == null)
		{
			return MeadKind.Unknown;
		}
		if (statusEffect is SE_Stats sE_Stats)
		{
			if (sE_Stats.m_healthOverTime > 0f || sE_Stats.m_healthOverTimeDuration > 0f)
			{
				return MeadKind.Health;
			}
			if (sE_Stats.m_staminaOverTime > 0f || sE_Stats.m_staminaOverTimeDuration > 0f || sE_Stats.m_staminaUpFront > 0f)
			{
				return MeadKind.Stamina;
			}
		}
		string text = ((item.m_dropPrefab != null) ? item.m_dropPrefab.name : "");
		if (text.IndexOf("Health", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			return MeadKind.Health;
		}
		if (text.IndexOf("Stamina", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			return MeadKind.Stamina;
		}
		return MeadKind.Unknown;
	}

	internal static bool IsPotionItem(ItemDrop.ItemData item)
	{
		if (item == null || item.m_shared == null)
		{
			return false;
		}
		if (item.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Consumable)
		{
			return false;
		}
		if (item.m_shared.m_consumeStatusEffect == null)
		{
			return false;
		}
		if (item.m_shared.m_food <= 0f && item.m_shared.m_foodStamina <= 0f)
		{
			return item.m_shared.m_foodEitr <= 0f;
		}
		return false;
	}

	public bool TryConsumePotion(ItemDrop.ItemData item)
	{
		if (!_initialized)
		{
			TryInit();
		}
		if (_nview == null || !_nview.IsOwner())
		{
			return false;
		}
		if (_humanoid == null || _character == null || item == null)
		{
			return false;
		}
		if (item.m_shared?.m_consumeStatusEffect == null)
		{
			return false;
		}
		Inventory inventory = _humanoid.GetInventory();
		if (inventory == null || !inventory.ContainsItem(item))
		{
			return false;
		}
		if (_seman == null)
		{
			_seman = _character.GetSEMan();
		}
		StatusEffect consumeStatusEffect = item.m_shared.m_consumeStatusEffect;
		if (_seman != null)
		{
			if (_seman.HaveStatusEffect(consumeStatusEffect.NameHash()))
			{
				CompanionsPlugin.Log.LogDebug("[Food] Potion REJECTED \"" + item.m_shared.m_name + "\" — effect already active on \"" + (_character?.m_name ?? "?") + "\"");
				return false;
			}
			if (!string.IsNullOrEmpty(consumeStatusEffect.m_category) && _seman.HaveStatusEffectCategory(consumeStatusEffect.m_category))
			{
				CompanionsPlugin.Log.LogDebug("[Food] Potion REJECTED \"" + item.m_shared.m_name + "\" — category \"" + consumeStatusEffect.m_category + "\" already active on \"" + (_character?.m_name ?? "?") + "\"");
				return false;
			}
		}
		if (_seman != null)
		{
			_seman.AddStatusEffect(consumeStatusEffect, resetTime: true, 0, 0f, -1);
		}
		if (_stamina != null && consumeStatusEffect is SE_Stats { m_staminaUpFront: >0f } sE_Stats)
		{
			_stamina.Restore(sE_Stats.m_staminaUpFront);
		}
		string trigger = ((_setup != null && _setup.CanWearArmor()) ? "eat" : "consume");
		if (_zanim != null)
		{
			_zanim.SetTrigger(trigger);
		}
		if (_humanoid.m_consumeItemEffects != null)
		{
			_humanoid.m_consumeItemEffects.Create(base.transform.position, Quaternion.identity, null, 1f, -1, ZDOID.None);
		}
		inventory.RemoveOneItem(item);
		CompanionsPlugin.Log.LogDebug("[Food] POTION consumed \"" + item.m_shared.m_name + "\" se=\"" + consumeStatusEffect.m_name + "\" companion=\"" + (_character?.m_name ?? "?") + "\"");
		_meadCooldownTimer = MeadCooldown;
		return true;
	}

	private string SerializeFood(int slot)
	{
		FoodEffect foodEffect = _foods[slot];
		if (!foodEffect.IsActive)
		{
			return "";
		}
		return string.Join("|", foodEffect.ItemName, foodEffect.HealthBonus.ToString("F1", CultureInfo.InvariantCulture), foodEffect.StaminaBonus.ToString("F1", CultureInfo.InvariantCulture), foodEffect.EitrBonus.ToString("F1", CultureInfo.InvariantCulture), foodEffect.RemainingTime.ToString("F1", CultureInfo.InvariantCulture), foodEffect.TotalTime.ToString("F1", CultureInfo.InvariantCulture), foodEffect.ItemPrefabName ?? "", foodEffect.FoodRegen.ToString("F1", CultureInfo.InvariantCulture));
	}

	private static FoodEffect DeserializeFood(string data)
	{
		if (string.IsNullOrEmpty(data))
		{
			return default(FoodEffect);
		}
		string[] array = data.Split(new char[1] { '|' });
		if (array.Length < 6)
		{
			return default(FoodEffect);
		}
		if (!float.TryParse(array[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
		{
			return default(FoodEffect);
		}
		if (!float.TryParse(array[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var result2))
		{
			return default(FoodEffect);
		}
		if (!float.TryParse(array[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var result3))
		{
			return default(FoodEffect);
		}
		if (!float.TryParse(array[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var result4))
		{
			return default(FoodEffect);
		}
		if (!float.TryParse(array[5], NumberStyles.Float, CultureInfo.InvariantCulture, out var result5))
		{
			return default(FoodEffect);
		}
		float result6 = 0f;
		if (array.Length >= 8)
		{
			float.TryParse(array[7], NumberStyles.Float, CultureInfo.InvariantCulture, out result6);
		}
		return new FoodEffect
		{
			ItemName = array[0],
			ItemPrefabName = ((array.Length >= 7) ? array[6] : ""),
			HealthBonus = result,
			StaminaBonus = result2,
			EitrBonus = result3,
			FoodRegen = result6,
			RemainingTime = result4,
			TotalTime = result5
		};
	}
}
