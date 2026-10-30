using System.Collections.Generic;
using UnityEngine;

namespace Companions;

public class RepairController : MonoBehaviour
{
	internal enum RepairPhase
	{
		Idle,
		MovingToStation,
		Repairing
	}

	private CompanionAI _ai;

	private Humanoid _humanoid;

	private Character _character;

	private CompanionSetup _setup;

	private ZNetView _nview;

	private ZSyncAnimation _zanim;

	private CompanionTalk _talk;

	private HarvestController _harvest;

	private DoorHandler _doorHandler;

	private SmeltController _smelt;

	private CookController _cook;

	private FarmController _farm;

	private HuntController _hunt;

	private FishController _fish;

	private static readonly HashSet<int> s_claimedStations = new HashSet<int>();

	private int _claimedStationId;

	private RepairPhase _phase;

	private CraftingStation _targetStation;

	private float _scanTimer;

	private float _repairTickTimer;

	private float _stuckTimer;

	private bool _lastScanFailed;

	private readonly List<ItemDrop.ItemData> _repairQueue = new List<ItemDrop.ItemData>();

	private readonly List<ItemDrop.ItemData> _tempWorn = new List<ItemDrop.ItemData>();

	// 1.4.145: worn items no station in range can fix, and until when they're left alone ([Repair] NoStationWait)
	private readonly Dictionary<ItemDrop.ItemData, float> _noStationUntil = new Dictionary<ItemDrop.ItemData, float>();

	private float _moveStart;

	// 1.4.146: this trip is the player's order (DirectRepairAt) - it goes ahead of the job, and the jobs wait for it
	private bool _direct;

	public bool IsDirect => _direct && _phase != RepairPhase.Idle;

	private const float ScanBackoffInterval = 60f;

	private const float MoveTimeout = 10f;

	internal RepairPhase Phase => _phase;

	public bool IsActive => _phase != RepairPhase.Idle;

	private static float DurabilityThreshold => ModConfig.RepairDurabilityThreshold.Value;

	private static float ScanInterval => ModConfig.RepairScanInterval.Value;

	private static float ScanRadius => ModConfig.RepairScanRadius.Value;

	private static float RepairTickInterval => ModConfig.RepairTickInterval.Value;

	private static float UseDistance => ModConfig.RepairUseDistance.Value;

	private CraftingStation _approachStation;

	private Vector3 _approachSpot;

	private bool _hasApproach;

	// 1.4.78: walk to a reachable spot by the station, not its centre (see MHR_Chests.PickApproachPiece)
	private Vector3 StationGoal()
	{
		if (_targetStation != _approachStation)
		{
			_approachStation = _targetStation;
			_hasApproach = MHR_Chests.PickApproachPiece(base.transform.position, _targetStation, out _approachSpot);
		}
		if (!_hasApproach)
		{
			return _targetStation.transform.position;
		}
		return _approachSpot;
	}

	public bool DirectRepairAt(CraftingStation station)
	{
		if (station == null)
		{
			return false;
		}
		if (_phase != RepairPhase.Idle)
		{
			Abort("new directed repair");
		}
		Inventory inventory = _humanoid?.GetInventory();
		if (inventory == null)
		{
			return false;
		}
		_tempWorn.Clear();
		inventory.GetWornItems(_tempWorn);
		if (_tempWorn.Count == 0)
		{
			Log("DirectRepairAt — no worn items");
			return false;
		}
		_repairQueue.Clear();
		for (int i = 0; i < _tempWorn.Count; i++)
		{
			if (CanRepairAt(station, _tempWorn[i]))
			{
				_repairQueue.Add(_tempWorn[i]);
			}
			else
			{
				Log("DirectRepairAt: \"" + _tempWorn[i].m_shared.m_name + "\" — " + GetRepairRejectReason(station, _tempWorn[i]));
			}
		}
		if (_repairQueue.Count == 0)
		{
			Log("DirectRepairAt — no items repairable at \"" + station.m_name + "\"");
			return false;
		}
		_targetStation = station;
		ClaimStation();
		_stuckTimer = 0f;
		_phase = RepairPhase.MovingToStation;
		_moveStart = Time.time;
		_direct = true;
		_lastScanFailed = false;
		Log($"DirectRepairAt — {_repairQueue.Count} items to repair at \"{station.m_name}\"");
		return true;
	}

	public void CancelDirected()
	{
		if (_phase != RepairPhase.Idle)
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
		_zanim = GetComponent<ZSyncAnimation>();
		_talk = GetComponent<CompanionTalk>();
		_harvest = GetComponent<HarvestController>();
		_doorHandler = GetComponent<DoorHandler>();
		_smelt = GetComponent<SmeltController>();
		_cook = GetComponent<CookController>();
		_farm = GetComponent<FarmController>();
		_hunt = GetComponent<HuntController>();
		_fish = GetComponent<FishController>();
	}

	private void OnDestroy()
	{
		UnclaimStation();
	}

	private void UnclaimStation()
	{
		if (_claimedStationId != 0)
		{
			s_claimedStations.Remove(_claimedStationId);
			_claimedStationId = 0;
		}
	}

	private void Update()
	{
		if (CompanionRest.MHR_ShouldPauseWork(this) || _nview == null || _nview.GetZDO() == null || !_nview.IsOwner() || _character == null || _character.GetHealth() <= 0f)
		{
			return;
		}
		float deltaTime = Time.deltaTime;
		string abortWhy = ShouldAbort();
		if (abortWhy != null)
		{
			if (_phase != RepairPhase.Idle)
			{
				Abort("interrupted (" + abortWhy + ")");
			}
			return;
		}
		switch (_phase)
		{
		case RepairPhase.Idle:
			UpdateIdle(deltaTime);
			break;
		case RepairPhase.MovingToStation:
			UpdateMoving(deltaTime);
			break;
		case RepairPhase.Repairing:
			UpdateRepairing(deltaTime);
			break;
		}
	}

	private string ShouldAbort()
	{
		if (_ai != null && _ai.IsInCombat)
		{
			return "combat";
		}
		// 1.4.145: loot pickup and other orders steer too
		if (MHR_Loot.Active(this))
		{
			return "picking up loot";
		}
		if (MHR_JobRules.OtherOrder(this))
		{
			return "another order";
		}
		if (CompanionInteractPanel.IsOpenFor(_setup) || CompanionRadialMenu.IsOpenFor(_setup))
		{
			return "being talked to";
		}
		// a repair the player ordered goes ahead of the job (the jobs wait for it: MHR_JobRules.OrderPending)
		if (IsDirect)
		{
			return null;
		}
		if ((_harvest != null && _harvest.IsActive) || (_smelt != null && _smelt.IsActive))
		{
			return "the job has work";
		}
		// 1.4.144: the other jobs too - with Cook on, both walked at once (forge vs honey/cauldron), fought over the
		// mover and it stood still. The job's task comes first; the repair trip waits until the job is between tasks.
		if ((_cook != null && _cook.IsActive) || (_farm != null && _farm.IsActive) || (_hunt != null && _hunt.IsActive) || (_fish != null && _fish.IsActive))
		{
			return "the job has work";
		}
		return null;
	}

	private void Abort(string reason)
	{
		if (_zanim != null && _setup != null && _setup.CanWearArmor())
		{
			_zanim.SetInt("crafting", 0);
		}
		Log($"Aborted — {reason} (phase was {_phase})");
		UnclaimStation();
		_targetStation = null;
		_repairQueue.Clear();
		_phase = RepairPhase.Idle;
		_direct = false;
		_scanTimer = ScanInterval;
	}

	private void UpdateIdle(float dt)
	{
		_scanTimer -= dt;
		if (_scanTimer > 0f)
		{
			return;
		}
		_scanTimer = ScanInterval;
		Inventory inventory = _humanoid?.GetInventory();
		if (inventory == null)
		{
			return;
		}
		_tempWorn.Clear();
		inventory.GetWornItems(_tempWorn);
		if (_tempWorn.Count == 0)
		{
			return;
		}
		if (!_lastScanFailed)
		{
			for (int i = 0; i < _tempWorn.Count; i++)
			{
				ItemDrop.ItemData itemData = _tempWorn[i];
				float maxDurability = itemData.GetMaxDurability();
				float num = ((maxDurability > 0f) ? (itemData.m_durability / maxDurability * 100f) : 100f);
				Recipe recipe = ObjectDB.instance?.GetRecipe(itemData);
				string arg = recipe?.m_craftingStation?.m_name ?? recipe?.m_repairStation?.m_name ?? "none";
				int num2 = recipe?.m_minStationLevel ?? 0;
				Log($"Scan: \"{itemData.m_shared.m_name}\" dur={itemData.m_durability:F0}/{maxDurability:F0} ({num:F0}%) " + $"station=\"{arg}\" minLevel={num2} canRepair={itemData.m_shared.m_canBeReparied}");
			}
		}
		// 1.4.144: only the items below the threshold pick the station - the 56% arbalest (black forge only) sent it to the
		// forge over and over to top up 96-100% bronze gear (user: "the NPC seems to repair a lot")
		List<ItemDrop.ItemData> low = new List<ItemDrop.ItemData>();
		float threshold = MHR_Nav.RepairThreshold(this);
		float now = Time.time;
		if (_noStationUntil.Count > 0)
		{
			foreach (ItemDrop.ItemData old in new List<ItemDrop.ItemData>(_noStationUntil.Keys))
			{
				if (now >= _noStationUntil[old])
				{
					_noStationUntil.Remove(old);
				}
			}
		}
		for (int j = 0; j < _tempWorn.Count; j++)
		{
			float maxDurability2 = _tempWorn[j].GetMaxDurability();
			float until;
			if (_noStationUntil.TryGetValue(_tempWorn[j], out until) && now < until)
			{
				continue;
			}
			if (maxDurability2 > 0f && _tempWorn[j].m_durability / maxDurability2 < threshold)
			{
				low.Add(_tempWorn[j]);
			}
		}
		bool flag = low.Count > 0;
		if (!flag)
		{
			Log($"Scan: {_tempWorn.Count} worn items but none below {threshold * 100f:F0}% that a station nearby can fix — skipping");
			return;
		}
		List<CraftingStation> allCraftingStations = ReflectionHelper.GetAllCraftingStations();
		if (allCraftingStations == null)
		{
			return;
		}
		CraftingStation craftingStation = null;
		float num3 = float.MaxValue;
		HashSet<ItemDrop.ItemData> fixable = new HashSet<ItemDrop.ItemData>();
		for (int k = 0; k < allCraftingStations.Count; k++)
		{
			CraftingStation craftingStation2 = allCraftingStations[k];
			if (craftingStation2 == null)
			{
				continue;
			}
			float num4 = MHR_Nav.StationScanDist(base.transform.position, craftingStation2.transform.position, this);
			if (num4 > ScanRadius)
			{
				continue;
			}
			// one it just failed to get to doesn't count (one another companion is using still does - it's free again soon)
			bool usable = _ai == null || !_ai.IsPositionBlacklisted(craftingStation2.transform.position);
			bool flag2 = false;
			for (int l = 0; l < low.Count; l++)
			{
				if (CanRepairAt(craftingStation2, low[l]))
				{
					flag2 = true;
					if (usable)
					{
						fixable.Add(low[l]);
					}
				}
			}
			Log($"Scan: station \"{craftingStation2.m_name}\" level={craftingStation2.GetLevel()} " + $"dist={num4:F1}m canRepairWorn={flag2}");
			if (flag2 && num4 < num3 && !s_claimedStations.Contains(craftingStation2.GetInstanceID()))
			{
				if (_ai != null && _ai.IsPositionBlacklisted(craftingStation2.transform.position))
				{
					Log("Scan: skipping \"" + craftingStation2.m_name + "\" — position blacklisted");
					continue;
				}
				craftingStation = craftingStation2;
				num3 = num4;
			}
		}
		// the worn items no usable station in range can fix: leave them alone for a while instead of looking every scan
		float wait = ModConfig.RepairNoStationWait?.Value ?? 600f;
		foreach (ItemDrop.ItemData item in low)
		{
			if (!fixable.Contains(item))
			{
				_noStationUntil[item] = now + wait;
				Log($"No station within {ScanRadius}m can repair \"{item.m_shared.m_name}\" ({item.m_durability / item.GetMaxDurability() * 100f:F0}%) - not trying again for {wait / 60f:F0} min");
				// 1.5.42: at home that's worth knowing (a station to build or upgrade); out in the wild it's expected
				if (_setup != null && _setup.HasHomePosition() && MHR_ToolKeeper.AtHome(_setup))
				{
					CompanionStatus.Why(this, "hc_why_repair_no_station", true, Localization.instance?.Localize(item.m_shared.m_name) ?? item.m_shared.m_name);
				}
			}
		}
		if (craftingStation == null)
		{
			if (!_lastScanFailed)
			{
				Log($"Scan: no reachable station can repair the items below {threshold * 100f:F0}% within {ScanRadius}m — backing off {60f}s");
			}
			_lastScanFailed = true;
			_scanTimer = 60f;
			return;
		}
		_lastScanFailed = false;
		_repairQueue.Clear();
		for (int m = 0; m < _tempWorn.Count; m++)
		{
			if (CanRepairAt(craftingStation, _tempWorn[m]))
			{
				_repairQueue.Add(_tempWorn[m]);
				continue;
			}
			Log("Scan: \"" + _tempWorn[m].m_shared.m_name + "\" cannot be repaired at \"" + craftingStation.m_name + "\" — " + GetRepairRejectReason(craftingStation, _tempWorn[m]));
		}
		_targetStation = craftingStation;
		ClaimStation();
		_stuckTimer = 0f;
		_phase = RepairPhase.MovingToStation;
		_moveStart = Time.time;
		Log($"Found {_repairQueue.Count} items to repair at " + $"\"{_targetStation.m_name}\" level={_targetStation.GetLevel()} ({num3:F1}m away)");
		if (_talk != null)
		{
			_talk.Say(ModLocalization.Loc("hc_speech_repair_start"));
		}
	}

	private void UpdateMoving(float dt)
	{
		if (_doorHandler != null && _doorHandler.IsActive)
		{
			return;
		}
		if (_targetStation == null)
		{
			Abort("station destroyed while moving");
			return;
		}
		float num = MHR_Nav.ReachDist(base.transform.position, _targetStation.transform.position);
		if (num < UseDistance)
		{
			_ai.StopMoving();
			_ai.LookAtPoint(_targetStation.transform.position);
			if (_zanim != null && _setup != null && _setup.CanWearArmor())
			{
				_zanim.SetInt("crafting", _targetStation.m_useAnimation);
			}
			_targetStation.PokeInUse();
			_repairTickTimer = RepairTickInterval;
			_phase = RepairPhase.Repairing;
			Log($"Arrived at \"{_targetStation.m_name}\" — starting repair of {_repairQueue.Count} items");
			return;
		}
		Vector3 goal = StationGoal();
		MHR_Mover.Go(_ai, dt, goal, _hasApproach ? 0.5f : UseDistance, num > 8f, "Repair");
		// 1.4.145: no way there, or the trip is taking too long -> give up on that station (blacklisted like a stuck trip)
		if (MHR_Mover.GaveUp(_ai) || Time.time - _moveStart > MHR_Nav.LegTimeout)
		{
			_ai.BlacklistPosition(_targetStation.transform.position);
			Abort($"couldn't get to \"{_targetStation.m_name}\" ({num:F1}m away, {Time.time - _moveStart:F0}s)");
			return;
		}
		_stuckTimer = MHR_Mover.StuckSeconds(_ai);
		if (_stuckTimer > 10f)
		{
			if (_targetStation != null && _ai != null)
			{
				_ai.BlacklistPosition(_targetStation.transform.position);
			}
			Abort($"stuck moving to \"{_targetStation.m_name}\" ({num:F1}m away, stuck {_stuckTimer:F1}s)");
		}
	}

	private void UpdateRepairing(float dt)
	{
		if (_targetStation == null)
		{
			Abort("station destroyed while repairing");
			return;
		}
		float num = Vector3.Distance(base.transform.position, _targetStation.transform.position);
		if (num > UseDistance * 2f)
		{
			Abort($"drifted too far from station ({num:F1}m)");
			return;
		}
		_ai.LookAtPoint(_targetStation.transform.position);
		_targetStation.PokeInUse();
		_repairTickTimer -= dt;
		if (_repairTickTimer > 0f)
		{
			return;
		}
		_repairTickTimer = RepairTickInterval;
		while (_repairQueue.Count > 0)
		{
			ItemDrop.ItemData itemData = _repairQueue[0];
			_repairQueue.RemoveAt(0);
			Inventory inventory = _humanoid?.GetInventory();
			if (inventory != null && inventory.ContainsItem(itemData) && !(itemData.m_durability >= itemData.GetMaxDurability()))
			{
				float durability = itemData.m_durability;
				float num2 = (itemData.m_durability = itemData.GetMaxDurability());
				_humanoid?.GetInventory()?.m_onChanged?.Invoke();
				_targetStation.m_repairItemDoneEffects.Create(_targetStation.transform.position, Quaternion.identity, null, 1f, -1, ZDOID.None);
				Log($"Repaired \"{itemData.m_shared.m_name}\" — {durability:F0} → {num2:F0}");
				return;
			}
		}
		FinishRepair();
	}

	private void Log(string msg)
	{
		string text = _character?.m_name ?? "?";
		CompanionsPlugin.Log.LogDebug("[Repair|" + text + "] " + msg);
	}

	internal static bool CanRepairAt(CraftingStation station, ItemDrop.ItemData item)
	{
		if (station == null || item == null)
		{
			return false;
		}
		if (!item.m_shared.m_canBeReparied)
		{
			return false;
		}
		// 1.4.133: only a station a player could use right now - under a roof if it needs one, with a fire if it needs one
		// (user: it repaired at workbenches the player can't use because they have no roof)
		Player user = Player.m_localPlayer;
		if (user != null && !station.CheckUsable(user, showMessage: false))
		{
			return false;
		}
		Recipe recipe = ObjectDB.instance?.GetRecipe(item);
		if (recipe == null)
		{
			return false;
		}
		if (recipe.m_craftingStation == null && recipe.m_repairStation == null)
		{
			return false;
		}
		if ((!(recipe.m_repairStation != null) || !(recipe.m_repairStation.m_name == station.m_name)) && (!(recipe.m_craftingStation != null) || !(recipe.m_craftingStation.m_name == station.m_name)) && item.m_worldLevel >= Game.m_worldLevel)
		{
			return false;
		}
		if (Mathf.Min(station.GetLevel(), 4) < recipe.m_minStationLevel)
		{
			return false;
		}
		return true;
	}

	private static string GetRepairRejectReason(CraftingStation station, ItemDrop.ItemData item)
	{
		if (!item.m_shared.m_canBeReparied)
		{
			return "canBeRepaired=false";
		}
		Recipe recipe = ObjectDB.instance?.GetRecipe(item);
		if (recipe == null)
		{
			return "no recipe found";
		}
		if (recipe.m_craftingStation == null && recipe.m_repairStation == null)
		{
			return "recipe has no station";
		}
		string arg = recipe.m_repairStation?.m_name ?? recipe.m_craftingStation?.m_name ?? "?";
		if ((!(recipe.m_repairStation != null) || !(recipe.m_repairStation.m_name == station.m_name)) && (!(recipe.m_craftingStation != null) || !(recipe.m_craftingStation.m_name == station.m_name)) && item.m_worldLevel >= Game.m_worldLevel)
		{
			return $"wrong station (needs \"{arg}\", worldLevel={item.m_worldLevel} gameLevel={Game.m_worldLevel})";
		}
		int num = Mathf.Min(station.GetLevel(), 4);
		if (num < recipe.m_minStationLevel)
		{
			return $"station level too low ({num} < {recipe.m_minStationLevel})";
		}
		return "unknown";
	}

	private void ClaimStation()
	{
		UnclaimStation();
		_claimedStationId = _targetStation?.GetInstanceID() ?? 0;
		if (_claimedStationId != 0)
		{
			s_claimedStations.Add(_claimedStationId);
		}
	}

	private void FinishRepair()
	{
		if (_zanim != null && _setup != null && _setup.CanWearArmor())
		{
			_zanim.SetInt("crafting", 0);
		}
		Log("All items repaired at \"" + (_targetStation?.m_name ?? "?") + "\"");
		if (_talk != null)
		{
			_talk.Say(ModLocalization.Loc("hc_speech_repair_done"));
		}
		UnclaimStation();
		_targetStation = null;
		_repairQueue.Clear();
		_phase = RepairPhase.Idle;
		_direct = false;
		_scanTimer = 1f;
	}
}
