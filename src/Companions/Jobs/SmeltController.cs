using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Companions;

public class SmeltController : MonoBehaviour
{
	internal enum SmeltPhase
	{
		Idle,
		Scanning,
		MovingToChest,
		TakingFromChest,
		MovingToSmelter,
		InsertingItem,
		Monitoring,
		CollectingOutput,
		MovingToOutputChest,
		StoringOutput,
		TakingDownPile
	}

	private CompanionAI _ai;

	private Humanoid _humanoid;

	private Character _character;

	private CompanionSetup _setup;

	private ZNetView _nview;

	private CompanionTalk _talk;

	private DoorHandler _doorHandler;

	private HarvestController _harvest;

	private RepairController _repair;

	private SmeltPhase _phase;

	private float _scanTimer;

	private float _actionTimer;

	private float _stuckTimer;

	private float _monitorTimer;

	private bool _wasMonitoring;

	private bool _allDoneNotified;

	private float _insertTimer;

	private static readonly HashSet<int> s_claimedSmelters = new HashSet<int>();

	private int _claimedSmelterId;

	private Smelter _targetSmelter;

	private Container _targetChest;

	private Container _outputChest;

	private string _carryingItemPrefab;

	private bool _carryingIsFuel;

	private int _carryingAmount;

	private bool _chestOpened;

	private readonly List<Smelter> _nearbySmelters = new List<Smelter>();

	private readonly List<Container> _nearbyChests = new List<Container>();

	private readonly List<Piece> _tempPieces = new List<Piece>();

	private readonly List<Smelter> _tempFurnaces = new List<Smelter>();

	private readonly List<Smelter> _tempKilns = new List<Smelter>();

	private static readonly Collider[] _dropScanBuffer = new Collider[32];

	private static int _itemLayerMask = -1;

	private const float MoveTimeout = 10f;

	private const float MonitorInterval = 3f;

	private const float InsertDelay = 0.3f;

	private static readonly FieldInfo s_addedOreTime = typeof(Smelter).GetField("m_addedOreTime", BindingFlags.Instance | BindingFlags.NonPublic);

	private float _moveLogTimer;

	private bool _shouldRun;

	private bool _collectTriggered;

	internal SmeltPhase Phase => _phase;

	public bool IsActive => _phase != SmeltPhase.Idle;

	private static float ScanInterval => ModConfig.SmeltScanInterval.Value;

	private static float ScanRadius => ModConfig.SmeltScanRadius.Value;

	private static float UseDistance => ModConfig.SmeltUseDistance.Value;

	private static int MaxCarryOre => ModConfig.SmeltMaxCarryOre.Value;

	private static int MaxCarryFuel => ModConfig.SmeltMaxCarryFuel.Value;

	public void NotifyActionModeChanged()
	{
		if (_phase != SmeltPhase.Idle)
		{
			if (CompanionsPlugin.LogNormal) Log("NotifyActionModeChanged — aborting smelt");
			Abort("mode changed");
		}
	}

	public void CancelDirected()
	{
		if (_phase != SmeltPhase.Idle)
		{
			Abort("cancelled by new command");
		}
	}

	private void Awake()
	{
		_ai = GetComponent<CompanionAI>();
		_humanoid = GetComponent<Humanoid>();
		_character = GetComponent<Character>();
		_setup = GetComponent<CompanionSetup>();
		_nview = GetComponent<ZNetView>();
		_talk = GetComponent<CompanionTalk>();
		_doorHandler = GetComponent<DoorHandler>();
		_harvest = GetComponent<HarvestController>();
		_repair = GetComponent<RepairController>();
	}

	private void OnDestroy()
	{
		UnclaimSmelter();
		CloseAnyOpenChest();
	}

	private void UnclaimSmelter()
	{
		if (_claimedSmelterId != 0)
		{
			s_claimedSmelters.Remove(_claimedSmelterId);
			_claimedSmelterId = 0;
		}
	}

	private void Update()
	{
		if (CompanionRest.MHR_ShouldPauseWork(this) || _nview == null || _nview.GetZDO() == null || !_nview.IsOwner() || _character == null || _character.GetHealth() <= 0f)
		{
			// 1.4.150: a chest it had open is closed while it's away (the lid stayed open)
			CloseAnyOpenChest();
			return;
		}
		// 1.4.128: picking up what a kill dropped goes first - two jobs steering at once made the companion stutter
		if (MHR_Loot.Active(this) || MHR_JobRules.OrderPending(this))
		{
			CloseAnyOpenChest();
			return;
		}
		if (ShouldAbort())
		{
			if (_phase != SmeltPhase.Idle)
			{
				Abort("interrupted");
			}
		}
		else
		{
			if (!IsSmeltMode() && _phase == SmeltPhase.Idle)
			{
				return;
			}
			if (!IsSmeltMode() && _phase != SmeltPhase.Idle)
			{
				Abort("mode changed");
				return;
			}
			float deltaTime = Time.deltaTime;
			switch (_phase)
			{
			case SmeltPhase.Idle:
				UpdateIdle(deltaTime);
				break;
			case SmeltPhase.Scanning:
				UpdateScanning();
				break;
			case SmeltPhase.MovingToChest:
				UpdateMovingToChest(deltaTime);
				break;
			case SmeltPhase.TakingFromChest:
				UpdateTakingFromChest();
				break;
			case SmeltPhase.MovingToSmelter:
				UpdateMovingToSmelter(deltaTime);
				break;
			case SmeltPhase.InsertingItem:
				UpdateInsertingItem(deltaTime);
				break;
			case SmeltPhase.Monitoring:
				UpdateMonitoring(deltaTime);
				break;
			case SmeltPhase.CollectingOutput:
				UpdateCollectingOutput();
				break;
			case SmeltPhase.MovingToOutputChest:
				UpdateMovingToOutputChest(deltaTime);
				break;
			case SmeltPhase.StoringOutput:
				UpdateStoringOutput();
				break;
			case SmeltPhase.TakingDownPile:
				UpdateTakingDownPile();
				break;
			}
		}
	}

	private bool ShouldAbort()
	{
		if (_ai != null && _ai.IsInCombat)
		{
			return true;
		}
		if (CompanionInteractPanel.IsOpenFor(_setup) || CompanionRadialMenu.IsOpenFor(_setup))
		{
			return true;
		}
		return false;
	}

	private void Abort(string reason)
	{
		if (CompanionsPlugin.LogNormal) Log($"Aborted — {reason} (phase was {_phase})");
		if (_pileTaker != null)
		{
			_pileTaker.PutAway(_humanoid, _setup);
			_pileTaker = null;
		}
		UnclaimSmelter();
		CloseAnyOpenChest();
		_targetSmelter = null;
		_targetChest = null;
		_outputChest = null;
		_carryingItemPrefab = null;
		_carryingAmount = 0;
		_collectTriggered = false;
		_wasMonitoring = false;
		_allDoneNotified = false;
		_phase = SmeltPhase.Idle;
		_scanTimer = ScanInterval;
		RestoreFollow();
	}

	internal bool IsSmeltMode()
	{
		ZDO zDO = _nview?.GetZDO();
		if (zDO == null)
		{
			return false;
		}
		return zDO.GetInt(CompanionSetup.ActionModeHash) == 6;
	}

	private void UpdateIdle(float dt)
	{
		_scanTimer -= dt;
		if (!(_scanTimer > 0f))
		{
			_scanTimer = ScanInterval;
			_phase = SmeltPhase.Scanning;
		}
	}

	private void UpdateScanning()
	{
		ScanNearbySmelters();
		if (_nearbySmelters.Count == 0)
		{
			if (CompanionsPlugin.LogNormal) Log("Scan: no smelters found within " + ScanRadius + "m");
			_phase = SmeltPhase.Idle;
			_scanTimer = 10f;
			return;
		}
		foreach (Smelter nearbySmelter in _nearbySmelters)
		{
			if (nearbySmelter == null || s_claimedSmelters.Contains(nearbySmelter.GetInstanceID()))
			{
				continue;
			}
			ZNetView component = nearbySmelter.GetComponent<ZNetView>();
			if (!(component == null) && component.GetZDO() != null)
			{
				float num = component.GetZDO().GetFloat(ZDOVars.s_fuel);
				int num2 = component.GetZDO().GetInt(ZDOVars.s_queued);
				int num3 = component.GetZDO().GetInt(ZDOVars.s_spawnAmount);
				bool flag = nearbySmelter.m_maxFuel == 0;
				int num4 = CountGroundDropsNearSmelter(nearbySmelter);
				if (CompanionsPlugin.LogDetailed) LogDebug("  Smelter \"" + nearbySmelter.m_name + "\" type=" + (flag ? "kiln" : "furnace") + " " + $"fuel={num:F0}/{nearbySmelter.m_maxFuel} ore={num2}/{nearbySmelter.m_maxOre} " + $"output={num3} groundDrops={num4} active={nearbySmelter.IsActive()} " + $"pos={nearbySmelter.transform.position:F1} " + $"dist={Vector3.Distance(base.transform.position, nearbySmelter.transform.position):F1}m");
				if (num3 > 0)
				{
					_targetSmelter = nearbySmelter;
					ClaimSmelter();
					ClearFollowForMovement();
					ResetStuck();
					_phase = SmeltPhase.CollectingOutput;
					if (CompanionsPlugin.LogNormal) Log($"→ Collecting queued output from \"{nearbySmelter.m_name}\" ({num3} items)");
					CompanionStatus.Doing(this, "hc_doing_smelt_collect", SName(nearbySmelter));
					return;
				}
				if (num4 > 0)
				{
					_targetSmelter = nearbySmelter;
					ClaimSmelter();
					ClearFollowForMovement();
					ResetStuck();
					_collectTriggered = true;
					_actionTimer = 0f;
					_phase = SmeltPhase.CollectingOutput;
					if (CompanionsPlugin.LogNormal) Log($"→ Collecting {num4} ground drops near \"{nearbySmelter.m_name}\"");
					CompanionStatus.Doing(this, "hc_doing_smelt_collect", SName(nearbySmelter));
					return;
				}
			}
		}
		_whyItems.Clear();
		ScanNearbyChests();
		if (_nearbyChests.Count == 0)
		{
			if (CompanionsPlugin.LogNormal) Log("No chests found nearby");
			CompanionStatus.Why(this, "hc_why_smelt_no_chests", speak: true);
			_phase = SmeltPhase.Idle;
			_scanTimer = 10f;
			return;
		}
		_tempKilns.Clear();
		foreach (Smelter nearbySmelter2 in _nearbySmelters)
		{
			if (!(nearbySmelter2 == null) && nearbySmelter2.m_maxFuel <= 0 && !s_claimedSmelters.Contains(nearbySmelter2.GetInstanceID()))
			{
				_tempKilns.Add(nearbySmelter2);
			}
		}
		_tempKilns.Sort(delegate(Smelter a, Smelter b)
		{
			ZNetView component4 = a.GetComponent<ZNetView>();
			ZNetView component5 = b.GetComponent<ZNetView>();
			float num8 = ((component4?.GetZDO() != null) ? ((float)component4.GetZDO().GetInt(ZDOVars.s_queued) / (float)Mathf.Max(1, a.m_maxOre)) : 1f);
			float value = ((component5?.GetZDO() != null) ? ((float)component5.GetZDO().GetInt(ZDOVars.s_queued) / (float)Mathf.Max(1, b.m_maxOre)) : 1f);
			return num8.CompareTo(value);
		});
		foreach (Smelter tempKiln in _tempKilns)
		{
			ZNetView component2 = tempKiln.GetComponent<ZNetView>();
			if (component2 != null && component2.GetZDO() != null && tempKiln.m_maxOre > 0)
			{
				int num5 = component2.GetZDO().GetInt(ZDOVars.s_queued);
				if (num5 > tempKiln.m_maxOre / 2)
				{
					if (CompanionsPlugin.LogDetailed) LogDebug($"  Kiln \"{tempKiln.m_name}\" well-stocked ({num5}/{tempKiln.m_maxOre}) — deferring to furnaces");
					continue;
				}
			}
			if (TryPlanRefill(tempKiln))
			{
				return;
			}
		}
		_tempFurnaces.Clear();
		foreach (Smelter nearbySmelter3 in _nearbySmelters)
		{
			if (!(nearbySmelter3 == null) && nearbySmelter3.m_maxFuel != 0 && !s_claimedSmelters.Contains(nearbySmelter3.GetInstanceID()))
			{
				_tempFurnaces.Add(nearbySmelter3);
			}
		}
		_tempFurnaces.Sort(delegate(Smelter a, Smelter b)
		{
			ZNetView component4 = a.GetComponent<ZNetView>();
			ZNetView component5 = b.GetComponent<ZNetView>();
			float num8 = ((component4?.GetZDO() != null) ? ((float)component4.GetZDO().GetInt(ZDOVars.s_queued) / (float)Mathf.Max(1, a.m_maxOre)) : 1f);
			float value = ((component5?.GetZDO() != null) ? ((float)component5.GetZDO().GetInt(ZDOVars.s_queued) / (float)Mathf.Max(1, b.m_maxOre)) : 1f);
			return num8.CompareTo(value);
		});
		foreach (Smelter tempFurnace in _tempFurnaces)
		{
			ZNetView component3 = tempFurnace.GetComponent<ZNetView>();
			if (component3 == null || component3.GetZDO() == null)
			{
				continue;
			}
			float num6 = component3.GetZDO().GetFloat(ZDOVars.s_fuel);
			int num7 = component3.GetZDO().GetInt(ZDOVars.s_queued);
			bool flag2 = num6 < (float)tempFurnace.m_fuelPerProduct;
			bool flag3 = num6 > (float)(tempFurnace.m_maxFuel - 1);
			bool flag4 = num7 == 0;
			bool flag5 = num7 >= tempFurnace.m_maxOre;
			if (flag4 && !flag5)
			{
				if (TryPlanRefill(tempFurnace) || (!flag3 && TryPlanFuelRefill(tempFurnace)))
				{
					return;
				}
			}
			else if (flag2 && !flag3)
			{
				if (TryPlanFuelRefill(tempFurnace))
				{
					return;
				}
			}
			else if (!flag5)
			{
				if (TryPlanRefill(tempFurnace))
				{
					return;
				}
			}
			else if (!flag3 && num6 < (float)(tempFurnace.m_maxFuel / 2) && TryPlanFuelRefill(tempFurnace))
			{
				return;
			}
		}
		bool flag6 = false;
		foreach (Smelter nearbySmelter4 in _nearbySmelters)
		{
			if (nearbySmelter4 != null && nearbySmelter4.IsActive())
			{
				flag6 = true;
				break;
			}
		}
		// the status: what's missing, in turn; else the smelters are working / there's nothing to smelt
		if (_whyItems.Count == 0)
		{
			_whyItems.Add(new CompanionStatus.Item(null, flag6 ? "hc_speech_smelt_monitoring" : "hc_why_smelt_nothing"));
		}
		CompanionStatus.WhyRotate(this, _whyItems);
		if (flag6)
		{
			_phase = SmeltPhase.Monitoring;
			_monitorTimer = 3f;
			if (!_wasMonitoring)
			{
				_wasMonitoring = true;
				if (CompanionsPlugin.LogNormal) Log("All smelters stocked — monitoring");
				if (_talk != null)
				{
					_talk.Say(ModLocalization.Loc("hc_speech_smelt_monitoring"));
				}
			}
			return;
		}
		if (!_allDoneNotified)
		{
			_allDoneNotified = true;
			if (CompanionsPlugin.LogNormal) Log("All smelters idle and fully stocked — done");
			if (_talk != null)
			{
				_talk.Say(ModLocalization.Loc("hc_speech_smelt_done"));
			}
			RestoreFollow();
		}
		_phase = SmeltPhase.Idle;
		_scanTimer = 30f;
	}

	private void UpdateMovingToChest(float dt)
	{
		if (_doorHandler != null && _doorHandler.IsActive)
		{
			return;
		}
		if (_targetChest == null)
		{
			Abort("chest destroyed");
			return;
		}
		float num = MHR_Nav.ReachDist(base.transform.position, _targetChest.transform.position);
		Vector3 goal = ChestGoal(_targetChest);
		if (num >= UseDistance && ChestOutOfReach(_targetChest))
		{
			Abort("chest out of reach");
			return;
		}
		if (num < UseDistance || AtSpot(num))
		{
			_ai.StopMoving();
			_ai.LookAtPoint(_targetChest.transform.position);
			_chestOpened = false;
			_phase = SmeltPhase.TakingFromChest;
			if (CompanionsPlugin.LogNormal) Log($"Arrived at chest (dist={num:F1}m) — taking {_carryingAmount}x {_carryingItemPrefab}");
		}
		else
		{
			_shouldRun = num > 8f;
			bool moveOk = MHR_Nav.MoveSmelt(_ai, dt, goal, MoveDist, _shouldRun);
			LogMovement(dt, "chest", num, moveOk);
			UpdateStuckDetection(dt, "chest");
		}
	}

	private void UpdateTakingFromChest()
	{
		if (_targetChest == null)
		{
			if (_chestOpened)
			{
				_chestOpened = false;
			}
			Abort("chest destroyed while taking");
			return;
		}
		// 1.4.150/152: someone has this chest open (here or on another player's game) - don't take it over; opening
		// it makes this game its owner so the changes are saved (MHR_Chests.Open)
		if (!_chestOpened && !MHR_Chests.Open(_targetChest))
		{
			Abort("someone has the chest open");
			return;
		}
		if (!_chestOpened)
		{
			GetComponent<ZSyncAnimation>()?.SetTrigger("interact");
			_chestOpened = true;
			_actionTimer = 0.8f;
			if (CompanionsPlugin.LogNormal) Log("Opened chest — waiting for animation");
			return;
		}
		_actionTimer -= Time.deltaTime;
		if (_actionTimer > 0f)
		{
			return;
		}
		Inventory inventory = _targetChest.GetInventory();
		Inventory inventory2 = _humanoid?.GetInventory();
		if (inventory == null || inventory2 == null)
		{
			_targetChest.SetInUse(inUse: false);
			_chestOpened = false;
			Abort("inventory null");
			return;
		}
		int num = 0;
		List<ItemDrop.ItemData> allItems = inventory.GetAllItems();
		if (CompanionsPlugin.LogNormal) Log($"Taking from chest: looking for \"{_carryingItemPrefab}\" in {allItems.Count} items");
		int num2 = allItems.Count - 1;
		while (num2 >= 0 && num < _carryingAmount)
		{
			ItemDrop.ItemData itemData = allItems[num2];
			if (itemData != null)
			{
				if (itemData.m_dropPrefab == null)
				{
					if (CompanionsPlugin.LogNormal) Log("  Skip item \"" + (itemData.m_shared?.m_name ?? "?") + "\" — m_dropPrefab is null");
				}
				else if (!(itemData.m_dropPrefab.name != _carryingItemPrefab))
				{
					int num3 = Mathf.Min(itemData.m_stack, _carryingAmount - num);
					ItemDrop.ItemData itemData2 = itemData.Clone();
					itemData2.m_stack = num3;
					if (!inventory2.AddItem(itemData2))
					{
						if (CompanionsPlugin.LogNormal) Log($"  No inventory space — stopping at {num} items");
						break;
					}
					inventory.RemoveItem(itemData, num3);
					num += num3;
					if (CompanionsPlugin.LogNormal) Log($"  Took {num3}x \"{_carryingItemPrefab}\" (total taken={num})");
				}
			}
			num2--;
		}
		if ((object)_targetChest != null)
		{
			_targetChest.SetInUse(inUse: false);
			_chestOpened = false;
			if (num == 0)
			{
				if (CompanionsPlugin.LogNormal) Log("Failed to take any items from chest — retrying scan");
				_phase = SmeltPhase.Scanning;
				return;
			}
			_carryingAmount = num;
			float num4 = ((_targetSmelter != null) ? Vector3.Distance(base.transform.position, _targetSmelter.transform.position) : (-1f));
			if (CompanionsPlugin.LogNormal) Log($"Took {num}x {_carryingItemPrefab} — heading to smelter " + string.Format("\"{0}\" (dist={1:F1}m)", _targetSmelter?.m_name ?? "null", num4));
			ResetStuck();
			_phase = SmeltPhase.MovingToSmelter;
		}
	}

	private void UpdateMovingToSmelter(float dt)
	{
		if (_doorHandler != null && _doorHandler.IsActive)
		{
			return;
		}
		if (_targetSmelter == null)
		{
			Abort("smelter destroyed");
			return;
		}
		Vector3 smelterInteractPoint = GetSmelterInteractPoint(_targetSmelter, _carryingIsFuel);
		float num = MHR_Nav.ReachDist(base.transform.position, smelterInteractPoint);
		Vector3 goal = SlotGoal(_targetSmelter, SlotPosition(_targetSmelter, _carryingIsFuel), smelterInteractPoint, _carryingIsFuel ? "fuel" : "ore");
		if (!(num < UseDistance) && !AtSpot(num))
		{
			_shouldRun = num > 8f;
			bool flag = MHR_Nav.MoveSmelt(_ai, dt, goal, MoveDist, _shouldRun);
			if (!flag || !(num < UseDistance + 1f))
			{
				LogMovement(dt, "smelter", num, flag);
				UpdateStuckDetection(dt, "smelter");
				return;
			}
		}
		_ai.StopMoving();
		_ai.LookAtPoint(smelterInteractPoint);
		_insertTimer = 0.3f;
		_phase = SmeltPhase.InsertingItem;
		if (CompanionsPlugin.LogNormal) Log($"Arrived at smelter interact point (dist={num:F1}m) — inserting {_carryingAmount}x {_carryingItemPrefab}");
	}

	private void UpdateInsertingItem(float dt)
	{
		if (_targetSmelter == null)
		{
			Abort("smelter destroyed while inserting");
			return;
		}
		_insertTimer -= dt;
		if (_insertTimer > 0f)
		{
			return;
		}
		_insertTimer = 0.3f;
		if (_carryingAmount <= 0)
		{
			if (CompanionsPlugin.LogNormal) Log("All items inserted — checking for next smelter with same material");
			if (!TryNextSmelterWithSameItem())
			{
				_phase = SmeltPhase.Scanning;
			}
			return;
		}
		ZNetView component = _targetSmelter.GetComponent<ZNetView>();
		if (component == null || component.GetZDO() == null)
		{
			Abort("smelter ZDO lost");
			return;
		}
		if (_carryingIsFuel)
		{
			float num = component.GetZDO().GetFloat(ZDOVars.s_fuel);
			if (num > (float)(_targetSmelter.m_maxFuel - 1))
			{
				if (CompanionsPlugin.LogNormal) Log($"Smelter fuel is full — {_carryingAmount} leftover, trying next smelter");
				if (!TryNextSmelterWithSameItem())
				{
					_phase = SmeltPhase.Scanning;
				}
			}
			else
			{
				// 1.4.150: out of the pack first - it used to add to the smelter even when the item was no longer there
				// (e.g. a tool trip had put it back in a chest), duplicating it
				if (!ConsumeOneFromInventory(_carryingItemPrefab))
				{
					if (CompanionsPlugin.LogNormal) Log($"No {_carryingItemPrefab} left in the pack ({_carryingAmount} expected) — rescanning");
					_carryingAmount = 0;
					_phase = SmeltPhase.Scanning;
					return;
				}
				component.InvokeRPC("RPC_AddFuel");
				_targetSmelter.m_fuelAddedEffects.Create(_targetSmelter.transform.position, _targetSmelter.transform.rotation, _targetSmelter.transform, 1f, -1, ZDOID.None);
				_carryingAmount--;
				if (CompanionsPlugin.LogNormal) Log($"Inserted fuel — {_carryingAmount} remaining");
			}
			return;
		}
		int num2 = component.GetZDO().GetInt(ZDOVars.s_queued);
		if (num2 >= _targetSmelter.m_maxOre)
		{
			if (CompanionsPlugin.LogNormal) Log($"Smelter ore queue is full — {_carryingAmount} leftover, trying next smelter");
			if (!TryNextSmelterWithSameItem())
			{
				_phase = SmeltPhase.Scanning;
			}
			return;
		}
		if (!ConsumeOneFromInventory(_carryingItemPrefab))
		{
			if (CompanionsPlugin.LogNormal) Log($"No {_carryingItemPrefab} left in the pack ({_carryingAmount} expected) — rescanning");
			_carryingAmount = 0;
			_phase = SmeltPhase.Scanning;
			return;
		}
		component.InvokeRPC("RPC_AddOre", _carryingItemPrefab, false);
		_targetSmelter.m_oreAddedEffects.Create(_targetSmelter.transform.position, _targetSmelter.transform.rotation, null, 1f, -1, ZDOID.None);
		s_addedOreTime?.SetValue(_targetSmelter, Time.time);
		_carryingAmount--;
		if (CompanionsPlugin.LogNormal) Log($"Inserted ore {_carryingItemPrefab} — {_carryingAmount} remaining");
	}

	private void UpdateMonitoring(float dt)
	{
		_monitorTimer -= dt;
		if (!(_monitorTimer > 0f))
		{
			_monitorTimer = 3f;
			_phase = SmeltPhase.Scanning;
		}
	}

	private void UpdateCollectingOutput()
	{
		if (_doorHandler != null && _doorHandler.IsActive)
		{
			return;
		}
		if (_targetSmelter == null)
		{
			_collectTriggered = false;
			_phase = SmeltPhase.Scanning;
			return;
		}
		Vector3 smelterOutputPoint = GetSmelterOutputPoint(_targetSmelter);
		float num = Vector3.Distance(base.transform.position, smelterOutputPoint);
		Vector3 goal = SlotGoal(_targetSmelter, OutputSlotPosition(_targetSmelter), smelterOutputPoint, "out");
		if (num > UseDistance && !AtSpot(num))
		{
			_shouldRun = num > 8f;
			bool flag = MHR_Nav.MoveSmelt(_ai, Time.deltaTime, goal, MoveDist, _shouldRun);
			if (!flag || !(num < UseDistance + 1f))
			{
				LogMovement(Time.deltaTime, "smelter (collect)", num, flag);
				UpdateStuckDetection(Time.deltaTime, "smelter (collect)");
				return;
			}
			_ai.StopMoving();
		}
		if (!_collectTriggered)
		{
			ZNetView component = _targetSmelter.GetComponent<ZNetView>();
			if (component == null || component.GetZDO() == null)
			{
				_collectTriggered = false;
				_phase = SmeltPhase.Scanning;
				return;
			}
			int num2 = component.GetZDO().GetInt(ZDOVars.s_spawnAmount);
			if (num2 <= 0)
			{
				_collectTriggered = false;
				_phase = SmeltPhase.Scanning;
				return;
			}
			_ai.StopMoving();
			_ai.LookAtPoint(smelterOutputPoint);
			component.InvokeRPC("RPC_EmptyProcessed");
			if (CompanionsPlugin.LogNormal) Log($"Triggered output spawn ({num2} items) from \"{_targetSmelter.m_name}\"");
			_collectTriggered = true;
			_actionTimer = 1.5f;
			return;
		}
		_actionTimer -= Time.deltaTime;
		if (!(_actionTimer > 0f))
		{
			PickUpNearbyDrops();
			_collectTriggered = false;
			ScanNearbyChests();
			_outputChest = FindChestWithSpace();
			if (_outputChest != null)
			{
				ResetStuck();
				_phase = SmeltPhase.MovingToOutputChest;
				if (CompanionsPlugin.LogNormal) Log($"Will store output in chest at {_outputChest.transform.position:F1}");
			}
			else
			{
				if (CompanionsPlugin.LogNormal) Log("No chest with space for output — items left in inventory");
				_phase = SmeltPhase.Scanning;
			}
		}
	}

	private void UpdateMovingToOutputChest(float dt)
	{
		if (_doorHandler != null && _doorHandler.IsActive)
		{
			return;
		}
		if (_outputChest == null)
		{
			_phase = SmeltPhase.Scanning;
			return;
		}
		float num = MHR_Nav.ReachDist(base.transform.position, _outputChest.transform.position);
		Vector3 goal = ChestGoal(_outputChest);
		if (num >= UseDistance && ChestOutOfReach(_outputChest))
		{
			Abort("output chest out of reach");
			return;
		}
		if (num < UseDistance || AtSpot(num))
		{
			_ai.StopMoving();
			_ai.LookAtPoint(_outputChest.transform.position);
			_chestOpened = false;
			_phase = SmeltPhase.StoringOutput;
			if (CompanionsPlugin.LogNormal) Log($"Arrived at output chest (dist={num:F1}m) — storing output");
		}
		else
		{
			_shouldRun = num > 8f;
			bool moveOk = MHR_Nav.MoveSmelt(_ai, dt, goal, MoveDist, _shouldRun);
			LogMovement(dt, "output chest", num, moveOk);
			UpdateStuckDetection(dt, "output chest");
		}
	}

	private void UpdateStoringOutput()
	{
		if (_outputChest == null)
		{
			if (_chestOpened)
			{
				_chestOpened = false;
			}
			if (CompanionsPlugin.LogNormal) Log("Output chest gone — back to scanning");
			_phase = SmeltPhase.Scanning;
			return;
		}
		// 1.4.150/152: someone has this chest open (here or on another player's game) - don't take it over; opening
		// it makes this game its owner so the changes are saved (MHR_Chests.Open)
		if (!_chestOpened && !MHR_Chests.Open(_outputChest))
		{
			if (CompanionsPlugin.LogNormal) Log("Someone has the output chest open — back to scanning");
			_phase = SmeltPhase.Scanning;
			return;
		}
		if (!_chestOpened)
		{
			GetComponent<ZSyncAnimation>()?.SetTrigger("interact");
			_chestOpened = true;
			_actionTimer = 0.8f;
			if (CompanionsPlugin.LogNormal) Log("Opened output chest — waiting for animation");
			return;
		}
		_actionTimer -= Time.deltaTime;
		if (_actionTimer > 0f)
		{
			return;
		}
		Inventory inventory = _humanoid?.GetInventory();
		Inventory inventory2 = _outputChest.GetInventory();
		if (inventory == null || inventory2 == null)
		{
			_outputChest.SetInUse(inUse: false);
			_chestOpened = false;
			_phase = SmeltPhase.Scanning;
			return;
		}
		int num = 0;
		List<ItemDrop.ItemData> allItems = inventory.GetAllItems();
		for (int num2 = allItems.Count - 1; num2 >= 0; num2--)
		{
			ItemDrop.ItemData itemData = allItems[num2];
			if (itemData != null && IsSmeltOutput(itemData) && inventory2.CanAddItem(itemData, itemData.m_stack))
			{
				int stack = itemData.m_stack;
				if (CompanionsPlugin.LogNormal) Log(string.Format("  Storing \"{0}\" x{1}", itemData.m_shared?.m_name ?? "?", stack));
				ItemDrop.ItemData itemData2 = itemData.Clone();
				itemData2.m_stack = stack;
				if (inventory2.AddItem(itemData2))
				{
					inventory.RemoveItem(itemData);
					num++;
				}
				else
				{
					if (CompanionsPlugin.LogNormal) Log("  AddItem failed for \"" + (itemData.m_shared?.m_name ?? "?") + "\" — skipping to prevent item loss");
				}
			}
		}
		_outputChest.SetInUse(inUse: false);
		_chestOpened = false;
		if (CompanionsPlugin.LogNormal) Log($"Stored {num} item stacks in chest — back to scanning");
		_phase = SmeltPhase.Scanning;
	}

	private void LateUpdate()
	{
		if (!(_character == null) && (_phase == SmeltPhase.MovingToChest || _phase == SmeltPhase.MovingToSmelter || _phase == SmeltPhase.MovingToOutputChest || (_phase == SmeltPhase.CollectingOutput && !_collectTriggered)))
		{
			_character.SetRun(_shouldRun);
		}
	}

	private void ScanNearbySmelters()
	{
		_nearbySmelters.Clear();
		_tempPieces.Clear();
		Piece.GetAllPiecesInRadius(base.transform.position, ScanRadius, _tempPieces);
		foreach (Piece tempPiece in _tempPieces)
		{
			if (tempPiece == null)
			{
				continue;
			}
			Smelter component = tempPiece.GetComponent<Smelter>();
			if (!(component == null))
			{
				ZNetView component2 = component.GetComponent<ZNetView>();
				if (!(component2 == null) && component2.GetZDO() != null)
				{
					_nearbySmelters.Add(component);
				}
			}
		}
		if (CompanionsPlugin.LogDetailed) LogDebug($"Scan: found {_nearbySmelters.Count} smelters");
	}

	private void Log(string msg)
	{
		string text = _character?.m_name ?? "?";
		if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log.LogInfo("[Smelt|" + text + "] " + msg);
	}

	private static int CountGroundDropsNearSmelter(Smelter smelter)
	{
		Vector3 position = ((smelter.m_outputPoint != null) ? smelter.m_outputPoint.position : smelter.transform.position);
		if (_itemLayerMask < 0)
		{
			_itemLayerMask = LayerMask.GetMask("item");
		}
		int num = Physics.OverlapSphereNonAlloc(position, 3f, _dropScanBuffer, _itemLayerMask);
		int num2 = 0;
		for (int i = 0; i < num; i++)
		{
			Collider collider = _dropScanBuffer[i];
			if (!(collider == null))
			{
				ItemDrop componentInParent = collider.GetComponentInParent<ItemDrop>();
				if (!(componentInParent == null) && componentInParent.m_itemData != null && componentInParent.m_itemData.m_shared != null && componentInParent.m_itemData.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Material)
				{
					num2++;
				}
			}
		}
		return num2;
	}

	private void LogDebug(string msg)
	{
		string text = _character?.m_name ?? "?";
		if (CompanionsPlugin.LogDetailed) CompanionsPlugin.Log.LogDebug("[Smelt|" + text + "] " + msg);
	}

	private void ClaimSmelter()
	{
		UnclaimSmelter();
		_claimedSmelterId = _targetSmelter?.GetInstanceID() ?? 0;
		if (_claimedSmelterId != 0)
		{
			s_claimedSmelters.Add(_claimedSmelterId);
		}
	}

	private void ClearFollowForMovement()
	{
		_wasMonitoring = false;
		_allDoneNotified = false;
		if (_ai != null)
		{
			_ai.SetFollowTarget(null);
			if (CompanionsPlugin.LogNormal) Log("Cleared follow target — SmeltController driving movement");
		}
	}

	private void ResetStuck()
	{
		_stuckTimer = 0f;
	}

	// 1.4.86 (movement phase 3 part 2): Smelt moves through MHR_Mover like Farm, and walks to a spot it can actually
	// stand on by the chest or by the smelter's ore/fuel slot, instead of at the chest or slot itself.
	private string _approachKey;

	private Vector3 _approachSpot;

	private bool _hasApproach;

	private readonly HashSet<int> _unreachableChests = new HashSet<int>();

	private float _unreachableClearAt;

	private Vector3 ChestGoal(Container chest)
	{
		string text = "c" + chest.GetInstanceID();
		if (_approachKey != text)
		{
			_approachKey = text;
			_hasApproach = MHR_Chests.PickApproach(base.transform.position, chest, out _approachSpot);
		}
		return _hasApproach ? _approachSpot : chest.transform.position;
	}

	private Vector3 SlotGoal(Smelter smelter, Vector3 slot, Vector3 fallback, string which)
	{
		string text = "s" + smelter.GetInstanceID() + which;
		if (_approachKey != text)
		{
			_approachKey = text;
			_hasApproach = MHR_Chests.PickApproachPoint(base.transform.position, slot, smelter, out _approachSpot);
		}
		return _hasApproach ? _approachSpot : fallback;
	}

	// standing on the picked spot counts as in reach (it can be a little past UseDistance from the measuring point)
	private bool AtSpot(float dist)
	{
		if (!_hasApproach || dist > UseDistance + 1.5f)
		{
			return false;
		}
		Vector3 position = base.transform.position;
		return new Vector2(position.x - _approachSpot.x, position.z - _approachSpot.z).magnitude <= 0.7f && Mathf.Abs(position.y - _approachSpot.y) <= 1.5f;   // 1.4.135: same height rule as the movement's "arrived"
	}

	private float MoveDist => _hasApproach ? 0.5f : UseDistance;

	// a chest with no spot in reach that a route gets to: skip it for a minute and pick another
	private bool ChestOutOfReach(Container chest)
	{
		if (_hasApproach || MHR_Chests.CanReach(base.transform.position, chest))
		{
			return false;
		}
		_unreachableChests.Add(chest.GetInstanceID());
		if (CompanionsPlugin.LogNormal) Log($"Skipping chest at {chest.transform.position:F1} - can't get within reach of it");
		return true;
	}

	private bool SkipChest(Container chest)
	{
		return chest == null || _unreachableChests.Contains(chest.GetInstanceID()) || (_ai != null && _ai.IsPositionBlacklisted(chest.transform.position));
	}

	private static Vector3 SlotPosition(Smelter smelter, bool isFuel)
	{
		Transform transform = ((!isFuel) ? smelter.m_addOreSwitch?.transform : smelter.m_addWoodSwitch?.transform);
		return (transform != null) ? transform.position : smelter.transform.position;
	}

	private static Vector3 OutputSlotPosition(Smelter smelter)
	{
		if (smelter.m_emptyOreSwitch != null)
		{
			return smelter.m_emptyOreSwitch.transform.position;
		}
		if (smelter.m_outputPoint != null)
		{
			return smelter.m_outputPoint.position;
		}
		return smelter.transform.position;
	}

	private void ScanNearbyChests()
	{
		if (Time.time >= _unreachableClearAt)
		{
			_unreachableChests.Clear();
			_unreachableClearAt = Time.time + 60f;
		}
		_nearbyChests.Clear();
		_tempPieces.Clear();
		Piece.GetAllPiecesInRadius(base.transform.position, ScanRadius, _tempPieces);
		foreach (Piece tempPiece in _tempPieces)
		{
			if (!(tempPiece == null))
			{
				Container componentInChildren = tempPiece.GetComponentInChildren<Container>();
				// 1.4.152: MHR_Chests.Usable - no carts or ships, ward access, private chests only its own player's, not in use
				if (!(componentInChildren == null) && !(componentInChildren.gameObject == base.gameObject) && MHR_Chests.Usable(componentInChildren, this))
				{
					_nearbyChests.Add(componentInChildren);
				}
			}
		}
		MHR_Chests.AddHomeChests(_nearbyChests, _setup);
		if (CompanionsPlugin.LogDetailed) LogDebug($"Scan: found {_nearbyChests.Count} chests within {ScanRadius}m (and around home)");
		foreach (Container nearbyChest in _nearbyChests)
		{
			Inventory inventory = nearbyChest.GetInventory();
			float num = Vector3.Distance(base.transform.position, nearbyChest.transform.position);
			if (CompanionsPlugin.LogDetailed) LogDebug($"  Chest \"{nearbyChest.m_name}\" items={inventory?.GetAllItems().Count ?? 0} " + $"empty={inventory?.GetEmptySlots() ?? 0} dist={num:F1}m " + $"pos={nearbyChest.transform.position:F1}");
		}
	}

	// 1.5.43: why it isn't loading or fuelling, gathered during a scan
	private readonly List<CompanionStatus.Item> _whyItems = new List<CompanionStatus.Item>();

	private void AddWhy(string key, params object[] args)
	{
		if (_whyItems.Count >= 8)
		{
			return;
		}
		string sig = key + "|" + string.Join("|", args);
		foreach (CompanionStatus.Item it in _whyItems)
		{
			if (it.Key + "|" + string.Join("|", it.Args) == sig)
			{
				return;
			}
		}
		_whyItems.Add(new CompanionStatus.Item(null, key, args));
	}

	private static string SName(Smelter s)
	{
		return (s == null) ? "" : (MHR_List.Local(s.m_name) ?? s.m_name);
	}

	private bool TryPlanRefill(Smelter smelter)
	{
		ZNetView component = smelter.GetComponent<ZNetView>();
		if (component == null || component.GetZDO() == null)
		{
			return false;
		}
		int num = component.GetZDO().GetInt(ZDOVars.s_queued);
		if (num >= smelter.m_maxOre)
		{
			return false;
		}
		int num2 = smelter.m_maxOre - num;
		bool flag = smelter.m_maxFuel == 0;
		Inventory inventory = _humanoid?.GetInventory();
		if (inventory != null)
		{
			foreach (Smelter.ItemConversion item in smelter.m_conversion)
			{
				if (!(item?.m_from == null) && (!flag || !(item.m_from.gameObject.name != "Wood")))
				{
					string text = item.m_from.gameObject.name;
					int num3 = CountItemByPrefab(inventory, text);
					if (num3 > 0)
					{
						int num4 = Mathf.Min(num2, num3, MaxCarryOre);
						_targetSmelter = smelter;
						ClaimSmelter();
						_targetChest = null;
						_carryingItemPrefab = text;
						_carryingIsFuel = false;
						_carryingAmount = num4;
						ClearFollowForMovement();
						ResetStuck();
						_phase = SmeltPhase.MovingToSmelter;
						if (CompanionsPlugin.LogNormal) Log($"Plan: use {num4}x ore \"{text}\" from inventory → " + $"\"{smelter.m_name}\" (queued={num}/{smelter.m_maxOre})");
						CompanionStatus.Doing(this, "hc_doing_smelt_load", SName(smelter), CookController.Name(text));
						return true;
					}
				}
			}
		}
		Container container = null;
		float num5 = float.MaxValue;
		string bestPrefab = null;
		int num6 = 0;
		foreach (Smelter.ItemConversion item2 in smelter.m_conversion)
		{
			if (item2?.m_from == null || (flag && item2.m_from.gameObject.name != "Wood"))
			{
				continue;
			}
			string text2 = item2.m_from.gameObject.name;
			foreach (Container nearbyChest in _nearbyChests)
			{
				if (SkipChest(nearbyChest))
				{
					continue;
				}
				Inventory inventory2 = nearbyChest.GetInventory();
				if (inventory2 == null)
				{
					continue;
				}
				int num7 = CountItemByPrefab(inventory2, text2);
				if (num7 > 0)
				{
					float num8 = Vector3.Distance(base.transform.position, nearbyChest.transform.position);
					if (num8 < num5)
					{
						container = nearbyChest;
						num5 = num8;
						bestPrefab = text2;
						num6 = num7;
					}
				}
			}
		}
		if (container == null || bestPrefab == null)
		{
			// 1.4.130: nothing in the chests - a pile of it (e.g. wood for the kiln) will do
			foreach (Smelter.ItemConversion conv in smelter.m_conversion)
			{
				if (conv?.m_from != null && (!flag || conv.m_from.gameObject.name == "Wood") && TryStartPile(conv.m_from.gameObject.name, smelter))
				{
					return true;
				}
			}
			// 1.5.43 (phase 3): why it isn't loading this one (user: "Smelting should also mention if there is no wood to add
			// to the kiln")
			AddWhy(flag ? "hc_why_smelt_no_wood" : "hc_why_smelt_no_ore", SName(smelter));
			return false;
		}
		int num9 = num2;
		foreach (Smelter nearbySmelter in _nearbySmelters)
		{
			if (nearbySmelter == null || nearbySmelter == smelter)
			{
				continue;
			}
			foreach (Smelter.ItemConversion item3 in nearbySmelter.m_conversion)
			{
				if (!(item3?.m_from == null) && !(item3.m_from.gameObject.name != bestPrefab))
				{
					ZNetView component2 = nearbySmelter.GetComponent<ZNetView>();
					if (component2?.GetZDO() != null)
					{
						num9 += Mathf.Max(0, nearbySmelter.m_maxOre - component2.GetZDO().GetInt(ZDOVars.s_queued));
					}
					break;
				}
			}
		}
		float num10 = (_humanoid?.GetInventory())?.GetTotalWeight() ?? 0f;
		float num11 = Mathf.Max(0f, CompanionCarry.Max(_humanoid) - num10);
		float num12 = (container.GetInventory()?.GetAllItems().Find((ItemDrop.ItemData i) => i.m_dropPrefab != null && i.m_dropPrefab.name == bestPrefab))?.m_shared?.m_weight ?? 1f;
		int num13 = ((num12 > 0f) ? Mathf.FloorToInt(num11 / num12) : MaxCarryOre);
		int num14 = Mathf.Min(num9, num6, Mathf.Max(1, num13));
		_targetSmelter = smelter;
		ClaimSmelter();
		_targetChest = container;
		_carryingItemPrefab = bestPrefab;
		_carryingIsFuel = false;
		_carryingAmount = num14;
		ClearFollowForMovement();
		ResetStuck();
		_phase = SmeltPhase.MovingToChest;
		if (CompanionsPlugin.LogNormal) Log($"Plan: take {num14}x ore \"{bestPrefab}\" (totalNeeded={num9} weightCap={num13}) " + $"from chest (dist={num5:F1}m) → \"{smelter.m_name}\" (queued={num}/{smelter.m_maxOre})");
		CompanionStatus.Doing(this, "hc_doing_smelt_load", SName(smelter), CookController.Name(bestPrefab));
		if (_talk != null)
		{
			_talk.Say(ModLocalization.Loc("hc_speech_smelt_materials"));
		}
		return true;
	}

	private bool TryPlanFuelRefill(Smelter smelter)
	{
		if (smelter.m_fuelItem == null || smelter.m_maxFuel == 0)
		{
			return false;
		}
		ZNetView component = smelter.GetComponent<ZNetView>();
		if (component == null || component.GetZDO() == null)
		{
			return false;
		}
		float num = component.GetZDO().GetFloat(ZDOVars.s_fuel);
		if (num > (float)(smelter.m_maxFuel - 1))
		{
			return false;
		}
		int num2 = smelter.m_maxFuel - (int)num;
		string fuelPrefab = smelter.m_fuelItem.gameObject.name;
		string text = smelter.m_fuelItem.m_itemData.m_shared.m_name;
		Inventory inventory = _humanoid?.GetInventory();
		if (inventory != null)
		{
			int num3 = CountItemByPrefab(inventory, fuelPrefab);
			if (num3 > 0)
			{
				int num4 = Mathf.Min(num2, num3, MaxCarryFuel);
				_targetSmelter = smelter;
				ClaimSmelter();
				_targetChest = null;
				_carryingItemPrefab = fuelPrefab;
				_carryingIsFuel = true;
				_carryingAmount = num4;
				ClearFollowForMovement();
				ResetStuck();
				_phase = SmeltPhase.MovingToSmelter;
				if (CompanionsPlugin.LogNormal) Log($"Plan: use {num4}x fuel \"{text}\" from inventory → " + $"\"{smelter.m_name}\" (fuel={num:F0}/{smelter.m_maxFuel})");
				CompanionStatus.Doing(this, "hc_doing_smelt_fuel", SName(smelter));
				return true;
			}
		}
		Container container = null;
		float num5 = float.MaxValue;
		int num6 = 0;
		foreach (Container nearbyChest in _nearbyChests)
		{
			if (SkipChest(nearbyChest))
			{
				continue;
			}
			Inventory inventory2 = nearbyChest.GetInventory();
			if (inventory2 == null)
			{
				continue;
			}
			int num7 = CountItemByPrefab(inventory2, fuelPrefab);
			if (num7 > 0)
			{
				float num8 = Vector3.Distance(base.transform.position, nearbyChest.transform.position);
				if (num8 < num5)
				{
					container = nearbyChest;
					num5 = num8;
					num6 = num7;
				}
			}
		}
		if (container == null)
		{
			// 1.4.130: or from a pile
			if (TryStartPile(fuelPrefab, smelter))
			{
				return true;
			}
			AddWhy("hc_why_smelt_no_fuel", CookController.Name(fuelPrefab), SName(smelter));
			return false;
		}
		int num9 = num2;
		foreach (Smelter nearbySmelter in _nearbySmelters)
		{
			if (!(nearbySmelter == null) && !(nearbySmelter == smelter) && !(nearbySmelter.m_fuelItem == null) && !(nearbySmelter.m_fuelItem.gameObject.name != fuelPrefab))
			{
				ZNetView component2 = nearbySmelter.GetComponent<ZNetView>();
				if (component2?.GetZDO() != null)
				{
					float num10 = component2.GetZDO().GetFloat(ZDOVars.s_fuel);
					num9 += Mathf.Max(0, nearbySmelter.m_maxFuel - (int)num10);
				}
			}
		}
		float num11 = (_humanoid?.GetInventory())?.GetTotalWeight() ?? 0f;
		float num12 = Mathf.Max(0f, CompanionCarry.Max(_humanoid) - num11);
		float num13 = (container.GetInventory()?.GetAllItems().Find((ItemDrop.ItemData i) => i.m_dropPrefab != null && i.m_dropPrefab.name == fuelPrefab))?.m_shared?.m_weight ?? 1f;
		int num14 = ((num13 > 0f) ? Mathf.FloorToInt(num12 / num13) : MaxCarryFuel);
		int num15 = Mathf.Min(num9, num6, Mathf.Max(1, num14));
		_targetSmelter = smelter;
		ClaimSmelter();
		_targetChest = container;
		_carryingItemPrefab = fuelPrefab;
		_carryingIsFuel = true;
		_carryingAmount = num15;
		ClearFollowForMovement();
		ResetStuck();
		_phase = SmeltPhase.MovingToChest;
		if (CompanionsPlugin.LogNormal) Log($"Plan: take {num15}x fuel \"{text}\" (totalNeeded={num9} weightCap={num14}) " + $"from chest (dist={num5:F1}m) → \"{smelter.m_name}\" (fuel={num:F0}/{smelter.m_maxFuel})");
		CompanionStatus.Doing(this, "hc_doing_smelt_fuel", SName(smelter));
		if (_talk != null)
		{
			_talk.Say(ModLocalization.Loc("hc_speech_smelt_fuel"));
		}
		return true;
	}

	// ---- 1.4.130: wood (or any material) from a pile when the chests have none (user) ----
	private MHR_PileTaker _pileTaker;

	private readonly Dictionary<int, float> _pileSkip = new Dictionary<int, float>();

	private float _nextNoHammerLog;

	private bool TryStartPile(string itemPrefab, Smelter smelter)
	{
		if (ModConfig.SmeltUsePiles != null && !ModConfig.SmeltUsePiles.Value)
		{
			return false;
		}
		Piece pile = MHR_PileTaker.Find(base.transform.position, ScanRadius, itemPrefab, base.transform.position, _pileSkip);
		if (pile == null)
		{
			return false;
		}
		if (MHR_PileTaker.Hammer(_humanoid) == null)
		{
			if (Time.time >= _nextNoHammerLog)
			{
				_nextNoHammerLog = Time.time + 120f;
				if (CompanionsPlugin.LogNormal) Log("No " + itemPrefab + " in the chests, but there's a pile of it - it needs a hammer to take it down");
				AddWhy("hc_why_smelt_pile_hammer", MHR_List.Local(pile.m_name) ?? pile.m_name);
			}
			return false;
		}
		_pileTaker = new MHR_PileTaker(pile, "Smelt");
		ClearFollowForMovement();
		_phase = SmeltPhase.TakingDownPile;
		if (CompanionsPlugin.LogNormal) Log("No " + itemPrefab + " in the chests for \"" + smelter.m_name + "\" - taking down the " + MHR_List.Local(pile.m_name) + " at " + MHR_Mover.Fmt(pile.transform.position));
		CompanionStatus.Doing(this, "hc_doing_take_pile", MHR_List.Local(pile.m_name) ?? pile.m_name);
		return true;
	}

	private void UpdateTakingDownPile()
	{
		if (_pileTaker == null)
		{
			_phase = SmeltPhase.Idle;
			return;
		}
		int r = _pileTaker.Tick(_ai, _humanoid, _setup);
		if (r == 0)
		{
			return;
		}
		_pileTaker.PutAway(_humanoid, _setup);
		if (r < 0)
		{
			if (_pileTaker.Pile != null)
			{
				_pileSkip[_pileTaker.Pile.GetInstanceID()] = Time.time + 600f;
			}
			if (CompanionsPlugin.LogNormal) Log("Gave up on the pile: " + _pileTaker.Why);
		}
		else
		{
			if (CompanionsPlugin.LogNormal) Log("Took down the pile - picked up " + _pileTaker.Got + " " + MHR_List.Local(_pileTaker.Shared));
		}
		_pileTaker = null;
		_ai?.StopMoving();
		MHR_Mover.Release(_ai);
		_phase = SmeltPhase.Idle;
		_scanTimer = 0.5f;
	}

	private void RestoreFollow()
	{
		if (!(_ai == null))
		{
			bool flag = _setup != null && _setup.GetFollow();
			if (flag && MHR_Owner.Of(this) != null)
			{
				_ai.SetFollowTarget(MHR_Owner.Go(this));
				if (CompanionsPlugin.LogNormal) Log("Restored follow target to player (Follow=ON)");
			}
			else
			{
				if (CompanionsPlugin.LogNormal) Log("Follow OFF, no StayHome — idle");
			}
		}
	}

	private void LogMovement(float dt, string target, float dist, bool moveOk)
	{
		_moveLogTimer -= dt;
		if (!(_moveLogTimer > 0f))
		{
			_moveLogTimer = 1f;
			Vector3 vector = _character?.GetVelocity() ?? Vector3.zero;
			GameObject obj = _ai?.GetFollowTarget();
			if (CompanionsPlugin.LogNormal) Log($"Moving → {target} dist={dist:F1}m moveOk={moveOk} " + string.Format("vel={0:F1} follow=\"{1}\" ", vector.magnitude, obj?.name ?? "null") + $"pos={base.transform.position:F1}");
		}
	}

	// 1.4.134 (movement phase 4): only the movement decides it's stuck (see FarmController); two minutes per leg at most
	private Vector3 _stuckLeg;

	private void UpdateStuckDetection(float dt, string targetName)
	{
		Vector3 leg = GetCurrentTargetPosition();
		if (Vector3.Distance(leg, _stuckLeg) > 1.5f)
		{
			_stuckLeg = leg;
			_stuckTimer = 0f;
		}
		_stuckTimer += dt;
		if ((_ai != null && MHR_Mover.GaveUp(_ai)) || _stuckTimer > MHR_Nav.LegTimeout)
		{
			Vector3 currentTargetPosition = GetCurrentTargetPosition();
			if (currentTargetPosition != Vector3.zero && _ai != null)
			{
				_ai.BlacklistPosition(currentTargetPosition);
			}
			Abort("stuck moving to " + targetName);
		}
	}

	private static Vector3 GetSmelterInteractPoint(Smelter smelter, bool isFuel)
	{
		Transform transform = ((!isFuel) ? smelter.m_addOreSwitch?.transform : smelter.m_addWoodSwitch?.transform);
		if (transform == null)
		{
			return smelter.transform.position;
		}
		return OffsetFromCenter(smelter.transform.position, transform.position, 1.3f);
	}

	private bool TryNextSmelterWithSameItem()
	{
		if (string.IsNullOrEmpty(_carryingItemPrefab))
		{
			return false;
		}
		Inventory inventory = _humanoid?.GetInventory();
		if (inventory == null)
		{
			return false;
		}
		int num = CountItemByPrefab(inventory, _carryingItemPrefab);
		if (num <= 0)
		{
			return false;
		}
		foreach (Smelter nearbySmelter in _nearbySmelters)
		{
			if (nearbySmelter == null || nearbySmelter == _targetSmelter || (_ai != null && _ai.IsPositionBlacklisted(nearbySmelter.transform.position)))
			{
				continue;
			}
			ZNetView component = nearbySmelter.GetComponent<ZNetView>();
			if (component == null || component.GetZDO() == null)
			{
				continue;
			}
			bool flag = false;
			int num3;
			if (_carryingIsFuel)
			{
				if (nearbySmelter.m_fuelItem == null || nearbySmelter.m_fuelItem.gameObject.name != _carryingItemPrefab)
				{
					continue;
				}
				float num2 = component.GetZDO().GetFloat(ZDOVars.s_fuel);
				num3 = nearbySmelter.m_maxFuel - (int)num2;
				if (num3 > 0)
				{
					flag = true;
				}
			}
			else
			{
				int num4 = component.GetZDO().GetInt(ZDOVars.s_queued);
				num3 = nearbySmelter.m_maxOre - num4;
				if (num3 > 0)
				{
					foreach (Smelter.ItemConversion item in nearbySmelter.m_conversion)
					{
						if (!(item?.m_from == null) && !(item.m_from.gameObject.name != _carryingItemPrefab))
						{
							flag = true;
							break;
						}
					}
				}
			}
			if (flag)
			{
				_targetSmelter = nearbySmelter;
				ClaimSmelter();
				_carryingAmount = Mathf.Min(num, Mathf.Max(0, num3));
				ClearFollowForMovement();
				ResetStuck();
				_phase = SmeltPhase.MovingToSmelter;
				if (CompanionsPlugin.LogNormal) Log($"Direct: routing to \"{nearbySmelter.m_name}\" with {_carryingAmount}x {_carryingItemPrefab} (no chest trip)");
				return true;
			}
		}
		return false;
	}

	private bool ConsumeOneFromInventory(string prefabName)
	{
		Inventory inventory = _humanoid?.GetInventory();
		if (inventory == null)
		{
			return false;
		}
		foreach (ItemDrop.ItemData allItem in inventory.GetAllItems())
		{
			if (allItem?.m_dropPrefab != null && allItem.m_dropPrefab.name == prefabName)
			{
				return inventory.RemoveItem(allItem, 1);
			}
		}
		return false;
	}

	private static Vector3 GetSmelterOutputPoint(Smelter smelter)
	{
		Vector3 position = smelter.transform.position;
		if (smelter.m_emptyOreSwitch != null)
		{
			return OffsetFromCenter(position, smelter.m_emptyOreSwitch.transform.position, 1.3f);
		}
		if (smelter.m_outputPoint != null)
		{
			return OffsetFromCenter(position, smelter.m_outputPoint.position, 1.3f);
		}
		return position;
	}

	private void PickUpNearbyDrops()
	{
		if (_targetSmelter == null)
		{
			return;
		}
		Vector3 position = ((_targetSmelter.m_outputPoint != null) ? _targetSmelter.m_outputPoint.position : _targetSmelter.transform.position);
		if (_itemLayerMask < 0)
		{
			_itemLayerMask = LayerMask.GetMask("item");
		}
		int num = Physics.OverlapSphereNonAlloc(position, 3f, _dropScanBuffer, _itemLayerMask);
		Inventory inventory = _humanoid?.GetInventory();
		if (inventory == null)
		{
			return;
		}
		for (int i = 0; i < num; i++)
		{
			Collider collider = _dropScanBuffer[i];
			if (collider == null)
			{
				continue;
			}
			ItemDrop componentInParent = collider.GetComponentInParent<ItemDrop>();
			if (!(componentInParent == null) && componentInParent.m_itemData != null)
			{
				ZNetView component = componentInParent.GetComponent<ZNetView>();
				// 1.4.152: only a drop this game owns (else ask for it and come back) - two games picking up the same drop
				// both got a copy
				if (!(component == null) && component.GetZDO() != null && !componentInParent.CanPickup(autoPickupDelay: false))
				{
					componentInParent.RequestOwn();
					continue;
				}
				if (!(component == null) && component.GetZDO() != null && inventory.CanAddItem(componentInParent.m_itemData, componentInParent.m_itemData.m_stack))
				{
					inventory.AddItem(componentInParent.m_itemData);
					component.ClaimOwnership();
					component.Destroy();
					if (CompanionsPlugin.LogNormal) Log($"Picked up {componentInParent.m_itemData.m_stack}x \"{componentInParent.m_itemData.m_shared.m_name}\"");
				}
			}
		}
	}

	private Container FindChestWithSpace()
	{
		Container result = null;
		float num = float.MaxValue;
		foreach (Container nearbyChest in _nearbyChests)
		{
			if (SkipChest(nearbyChest))
			{
				continue;
			}
			Inventory inventory = nearbyChest.GetInventory();
			if (inventory != null && inventory.GetEmptySlots() > 0)
			{
				float num2 = Vector3.Distance(base.transform.position, nearbyChest.transform.position);
				if (num2 < num)
				{
					result = nearbyChest;
					num = num2;
				}
			}
		}
		return result;
	}

	private static bool IsSmeltOutput(ItemDrop.ItemData item)
	{
		if (item == null || item.m_shared == null)
		{
			return false;
		}
		ItemDrop.ItemData.ItemType itemType = item.m_shared.m_itemType;
		return itemType == ItemDrop.ItemData.ItemType.Material;
	}

	private static int CountItemByPrefab(Inventory inv, string prefabName)
	{
		int num = 0;
		List<ItemDrop.ItemData> allItems = inv.GetAllItems();
		for (int i = 0; i < allItems.Count; i++)
		{
			ItemDrop.ItemData itemData = allItems[i];
			if (itemData?.m_dropPrefab != null && itemData.m_dropPrefab.name == prefabName)
			{
				num += itemData.m_stack;
			}
		}
		return num;
	}

	private static Vector3 OffsetFromCenter(Vector3 center, Vector3 target, float offset)
	{
		Vector3 vector = target - center;
		vector.y = 0f;
		if (vector.sqrMagnitude < 0.01f)
		{
			return target;
		}
		return target + vector.normalized * offset;
	}

	private void Finish()
	{
		if (CompanionsPlugin.LogNormal) Log("Finish — restoring follow target");
		UnclaimSmelter();
		CloseAnyOpenChest();
		_targetSmelter = null;
		_targetChest = null;
		_outputChest = null;
		_carryingItemPrefab = null;
		_carryingAmount = 0;
		_collectTriggered = false;
		_wasMonitoring = false;
		_allDoneNotified = false;
		_phase = SmeltPhase.Idle;
		_scanTimer = ScanInterval;
		RestoreFollow();
	}

	private void CloseAnyOpenChest()
	{
		if (_chestOpened)
		{
			if (_phase == SmeltPhase.StoringOutput && _outputChest != null)
			{
				_outputChest.SetInUse(inUse: false);
			}
			else if (_targetChest != null)
			{
				_targetChest.SetInUse(inUse: false);
			}
			_chestOpened = false;
		}
	}

	private Vector3 GetCurrentTargetPosition()
	{
		switch (_phase)
		{
		case SmeltPhase.MovingToChest:
			if (!(_targetChest != null))
			{
				return Vector3.zero;
			}
			return _targetChest.transform.position;
		case SmeltPhase.MovingToSmelter:
		case SmeltPhase.CollectingOutput:
			if (!(_targetSmelter != null))
			{
				return Vector3.zero;
			}
			return _targetSmelter.transform.position;
		case SmeltPhase.MovingToOutputChest:
			if (!(_outputChest != null))
			{
				return Vector3.zero;
			}
			return _outputChest.transform.position;
		default:
			return Vector3.zero;
		}
	}
}
