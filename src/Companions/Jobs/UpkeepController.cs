using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace Companions;

// 1.4.117: Repair and Restock (wheel mode 15), replacing the old Repair, Restock and Homestead modes (user: "combine the
// wheel actions for restock, repair, and add a chest sorting feature ... perform the actual actions needed to complete
// the task rather than fake it"). Everything is done the way a player does it, walking with the shared movement:
//  - Repair: hammer in hand, walks up to a damaged building piece, one swing repairs it (the game's own repair: needs the
//    piece's crafting station in range and ward access; costs stamina and hammer durability like a player's swing).
//  - Restock: fires, torches and braziers low on fuel get topped up one piece at a time; fuel comes from the pack or is
//    fetched from a chest first.
//  - Sort: like with like - each kind of item is carried to the chest in its area that already
//    holds the most of it, and part-stacks are merged. Leftovers it fetched go back in a chest.
// Started near home it works around home (HomeTripRange); anywhere else, around where it was started (AwayRadius).
// Nothing to do: walks back to the middle of that area and idles.
public partial class UpkeepController : MonoBehaviour
{
	private enum Step
	{
		Idle,
		Repair,
		FuelFetch,
		Fuel,
		SortTake,
		SortPut,
		Tidy,
		Stow,
		PileTake,
		PileBuild,
		PileDown,
		PileCollect,
		Hive,
		HiveCollect,
		Home
	}

	private const string Tag = "Upkeep";

	private ZNetView _nview;

	private CompanionAI _ai;

	private CompanionCombatAI _combatAI;

	private CompanionSetup _setup;

	private Humanoid _humanoid;

	private Character _character;

	private CompanionStamina _stamina;

	private CompanionTalk _talk;

	private RepairController _gearRepair;

	private float _orderHoldUntil;

	// ---- 1.4.143: small chores for an idle companion on any job (IdleController) ----
	private bool _idleChore;

	private float _idleRange;

	internal bool IdleChoreActive => _idleChore;

	// a repair, a fire to top up or a full beehive within range of where it's idling; true if one was started
	internal bool StartIdleChore(Vector3 center, float range)
	{
		if (_idleChore || GetMode() == CompanionSetup.ModeRepairBuildings || _step != Step.Idle)
		{
			return false;
		}
		_idleChore = true;
		_idleRange = range;
		_atHome = false;
		_anchor = center;
		_hasAnchor = true;
		if (PlanRepair() || PlanFuel() || PlanHoney())
		{
			return true;
		}
		_idleChore = false;
		_hasAnchor = false;
		return false;
	}

	internal void StopIdleChore()
	{
		if (_idleChore)
		{
			Stop();
			EndIdleChore();
			// 1.4.150: cut short with fuel (or the like) still in the pack - it goes back with the next bag-emptying at home
			if (_carried.Count > 0)
			{
				MHR_ToolKeeper.RequestUnload(_setup);
			}
		}
	}

	private void EndIdleChore()
	{
		PutHammerAway();
		_idleChore = false;
		_hasAnchor = false;
		_step = Step.Idle;
	}

	private Step _step;

	private float _scanTimer;

	private float _stepStart;

	private float _nextAct;

	private float _nextSpotPick;

	private bool _hasSpot;

	private Vector3 _spot;

	private bool _hasAnchor;

	private Vector3 _anchor;

	private bool _atHome;

	private WearNTear _piece;

	private Fireplace _fire;

	private Container _chest;

	private Container _target;

	private Container _open;

	private string _key;

	private int _amount;

	private bool _holdingHammer;

	private bool _idleSaid;

	private float _lastSpeech;

	private float _nextWaitLog;

	private float _nextNothingLog;

	// what it took out of chests and still carries (item key -> count), so it all goes back in a chest afterwards
	private readonly Dictionary<string, int> _carried = new Dictionary<string, int>();

	private readonly Dictionary<int, float> _skip = new Dictionary<int, float>();

	private static readonly List<Piece> s_pieces = new List<Piece>();

	public bool IsActive => GetMode() == CompanionSetup.ModeRepairBuildings && _step != Step.Idle;

	private int GetMode()
	{
		return (_nview?.GetZDO())?.GetInt(CompanionSetup.ActionModeHash) ?? 0;
	}

	private void Awake()
	{
		_nview = GetComponent<ZNetView>();
		_ai = GetComponent<CompanionAI>();
		_combatAI = GetComponent<CompanionCombatAI>();
		_setup = GetComponent<CompanionSetup>();
		_humanoid = GetComponent<Humanoid>();
		_character = GetComponent<Character>();
		_stamina = GetComponent<CompanionStamina>();
		_talk = GetComponent<CompanionTalk>();
		_gearRepair = GetComponent<RepairController>();
	}

	public void NotifyActionModeChanged()
	{
		Stop();
		_hasAnchor = false;
		_scanTimer = 0f;
		_idleSaid = false;
	}

	private void Update()
	{
		if (_nview == null || !_nview.IsOwner() || _ai == null || _humanoid == null || _setup == null)
		{
			return;
		}
		if (CompanionRest.MHR_ShouldPauseWork(this))
		{
			CloseChest();
			return;
		}
		if (GetMode() != CompanionSetup.ModeRepairBuildings && !_idleChore)
		{
			if (_step != Step.Idle || _holdingHammer)
			{
				Stop();
			}
			_hasAnchor = false;
			return;
		}
		if (!_hasAnchor)
		{
			_hasAnchor = true;
			// 1.4.119: works around home when started near home, otherwise around where it was started (user: sort the
			// chests around them, not just at home)
			_atHome = _setup.HasHomePosition() && MHR_ToolKeeper.NearHome(_setup);
			_anchor = _atHome ? _setup.GetHomePosition() : base.transform.position;
			Log("Repair and Restock around " + (_atHome ? "home " : "here ") + MHR_Mover.Fmt(_anchor) + ", up to " + Range().ToString("F0") + " m out");
		}
		if (CompanionInteractPanel.IsOpenFor(_setup) || CompanionRadialMenu.IsOpenFor(_setup))
		{
			_ai.StopMoving();
			return;
		}
		if (_combatAI != null && _combatAI.IsEngaged)
		{
			if (_step != Step.Idle)
			{
				LogDebug("A fight - stopping " + _step + " for now");
				Stop();
			}
			return;
		}
		if (MHR_ToolKeeper.IsBusy(this) || MHR_Loot.Active(this))
		{
			return;
		}
		// 1.4.133: a "go there" order comes first (user: using the command broke Repair and Restock - both steered at once).
		// It stays there a little while after arriving, then carries on with the chores.
		if (MHR_JobRules.OrderPending(this))
		{
			if (_step != Step.Idle || _holdingHammer)
			{
				LogDebug("Following an order - " + _step + " waits");
				Stop();
			}
			_orderHoldUntil = Time.time + (ModConfig.UpkeepOrderWait?.Value ?? 30f);
			return;
		}
		if (Time.time < _orderHoldUntil)
		{
			return;
		}
		// 1.4.125: gear repair (e.g. the hammer worn by the repairs) goes first. 1.4.124 test: it set off for the workbench
		// while this job kept steering toward a stone stair, and the companion stood stuck between the two.
		if (_gearRepair != null && _gearRepair.IsActive)
		{
			if (_step != Step.Idle || _holdingHammer)
			{
				LogDebug("Repairing its gear first - " + _step + " waits");
				Stop();
			}
			return;
		}
		// 1.4.143: one small chore for IdleController - when it's done (and anything fetched is put back), hand back
		if (_idleChore && _step == Step.Idle)
		{
			if (!PlanStow())
			{
				EndIdleChore();
			}
			return;
		}
		switch (_step)
		{
		case Step.Repair:
			UpdateRepair();
			break;
		case Step.FuelFetch:
		case Step.SortTake:
		case Step.SortPut:
		case Step.Tidy:
		case Step.Stow:
		case Step.PileTake:
			UpdateChest();
			break;
		case Step.PileBuild:
			UpdatePileBuild();
			break;
		case Step.PileDown:
			UpdatePileDown();
			break;
		case Step.PileCollect:
			UpdatePileCollect();
			break;
		case Step.Hive:
			UpdateHive();
			break;
		case Step.HiveCollect:
			UpdateHiveCollect();
			break;
		case Step.Fuel:
			UpdateFuel();
			break;
		case Step.Home:
			UpdateHome();
			break;
		default:
			UpdateIdle();
			break;
		}
	}

	// ------------------------------------------------------------------ settings

	private float Range()
	{
		if (_idleChore)
		{
			return _idleRange;
		}
		return _atHome ? MHR_ToolKeeper.HomeRange() : (ModConfig.UpkeepAwayRadius?.Value ?? 30f);
	}

	private Vector3 Center()
	{
		return _anchor;
	}

	private static float Reach => ModConfig.UpkeepReach?.Value ?? 4.5f;

	private static float WorkTime => ModConfig.UpkeepWorkTime?.Value ?? 1f;

	private static float GiveUp => ModConfig.UpkeepGiveUp?.Value ?? 90f;

	// ------------------------------------------------------------------ choosing what to do

	private void UpdateIdle()
	{
		_scanTimer -= Time.deltaTime;
		if (_scanTimer > 0f)
		{
			return;
		}
		_scanTimer = ModConfig.UpkeepScanInterval?.Value ?? 5f;
		// fuel before putting leftovers back, so fuel fetched for one fire can go in the next one first
		bool busy = PlanRepair() || PlanFuel() || PlanHoney() || PlanStow() || PlanPiles() || PlanSort();
		if (_step != Step.Repair && _step != Step.PileTake && _step != Step.PileBuild && _step != Step.PileDown)
		{
			PutHammerAway();
		}
		if (busy)
		{
			_idleSaid = false;
			return;
		}
		if (!_idleSaid)
		{
			_idleSaid = true;
			Log("Nothing to repair, restock or sort right now");
			Say("hc_speech_upkeep_nothing");
		}
		else if (Time.time >= _nextNothingLog)
		{
			_nextNothingLog = Time.time + 120f;
			LogDebug("Still nothing to do");
		}
		if (Vector3.Distance(base.transform.position, Center()) > (_atHome ? MHR_ToolKeeper.AtHomeRange() : 10f))
		{
			Enter(Step.Home);
		}
	}

	private void Enter(Step s)
	{
		_step = s;
		_stepStart = Time.time;
		_nextAct = 0f;
		_hasSpot = false;
		_nextSpotPick = 0f;
		_arrived = false;
		_arrivedAt = 0f;
		MHR_Mover.Release(_ai);
	}

	private void Stop()
	{
		_pile = null;
		_downPile = null;
		CloseChest();
		PutHammerAway();
		_step = Step.Idle;
		_piece = null;
		_fire = null;
		_chest = null;
		_target = null;
		_key = null;
		_scanTimer = 1f;
		_ai?.StopMoving();
		if (_ai != null)
		{
			MHR_Mover.Release(_ai);
		}
	}

	private void Done(string why)
	{
		if (!string.IsNullOrEmpty(why))
		{
			LogDebug(why);
		}
		CloseChest();
		_step = Step.Idle;
		_piece = null;
		_fire = null;
		_chest = null;
		_target = null;
		_key = null;
		_scanTimer = 0.3f;
		_ai.StopMoving();
		MHR_Mover.Release(_ai);
	}

	private void Skip(Component c, float seconds, string why)
	{
		if (c != null)
		{
			_skip[c.GetInstanceID()] = Time.time + seconds;
		}
		Done(why);
	}

	private bool Skipped(Component c)
	{
		float until;
		return c != null && _skip.TryGetValue(c.GetInstanceID(), out until) && Time.time < until;
	}

	// ---- repair ----

	private bool PlanRepair()
	{
		if (ModConfig.UpkeepRepair != null && !ModConfig.UpkeepRepair.Value)
		{
			return false;
		}
		if (Hammer() == null)
		{
			return false;
		}
		float below = ModConfig.UpkeepRepairBelow?.Value ?? 1f;
		bool noBench = ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoWorkbench);
		Vector3 me = base.transform.position;
		s_pieces.Clear();
		Piece.GetAllPiecesInRadius(Center(), Range(), s_pieces);
		WearNTear best = null;
		float bestD = float.MaxValue;
		int noStation = 0;
		for (int i = 0; i < s_pieces.Count; i++)
		{
			Piece p = s_pieces[i];
			if (p == null || !p.IsPlacedByPlayer())
			{
				continue;
			}
			WearNTear w = p.GetComponent<WearNTear>();
			if (w == null || Skipped(w) || w.GetHealthPercentage() >= Mathf.Min(below, 0.9999f))
			{
				continue;
			}
			Vector3 pos = p.transform.position;
			if (!noBench && p.m_craftingStation != null && CraftingStation.HaveBuildStationInRange(p.m_craftingStation.m_name, pos) == null)
			{
				noStation++;
				continue;
			}
			if (!PrivateArea.CheckAccess(pos, 0f, flash: false))
			{
				continue;
			}
			float d = Vector3.Distance(me, pos);
			if (d < bestD)
			{
				bestD = d;
				best = w;
			}
		}
		if (best == null)
		{
			if (noStation > 0 && Time.time >= _nextWaitLog)
			{
				_nextWaitLog = Time.time + 120f;
				Log(noStation + " damaged piece(s) have no crafting station in range - a player couldn't repair those either");
			}
			return false;
		}
		_piece = best;
		Log("Repairing " + PieceName(best) + " (" + (best.GetHealthPercentage() * 100f).ToString("F0") + "%, " + bestD.ToString("F0") + " m away)");
		if (Time.time - _lastSpeech > 120f)
		{
			Say("hc_speech_homestead_repair");
		}
		Enter(Step.Repair);
		return true;
	}

	private void UpdateRepair()
	{
		if (_piece == null || !_piece.GetComponent<ZNetView>().IsValid())
		{
			Done("The piece is gone");
			return;
		}
		if (_piece.GetHealthPercentage() >= 0.9999f)
		{
			Done(PieceName(_piece) + " is already repaired");
			return;
		}
		ItemDrop.ItemData hammer = Hammer();
		if (hammer == null)
		{
			PutHammerAway();
			Done("No working hammer any more - repairs wait until there is one");
			return;
		}
		Vector3 me = base.transform.position;
		Vector3 aim;
		if (!PieceInReach(me, _piece, out aim))
		{
			if (Time.time - _stepStart > GiveUp)
			{
				Skip(_piece, 300f, "Couldn't get within reach of " + PieceName(_piece) + " - leaving it for now");
				return;
			}
			if (!Approach(_piece.transform.position, _piece))
			{
				Skip(_piece, 300f, "No way to get to " + PieceName(_piece) + " - leaving it for now");
				return;
			}
			if (_arrived)
			{
				if (_arrivedAt == 0f)
				{
					_arrivedAt = Time.time;
				}
				else if (Time.time - _arrivedAt > 3f)
				{
					Skip(_piece, 300f, "Can't see or reach " + PieceName(_piece) + " from where it can stand" + (_lastBlocker != null ? " (" + _lastBlocker + " is in the way)" : " (too far)") + " - leaving it for now");
				}
			}
			else
			{
				_arrivedAt = 0f;
			}
			return;
		}
		_ai.StopMoving();
		MHR_Mover.Release(_ai);
		if (!_humanoid.IsItemEquiped(hammer))
		{
			_setup.SuppressAutoEquip = true;
			_holdingHammer = true;
			_humanoid.EquipItem(hammer);
			_nextAct = Time.time + 0.6f;
			return;
		}
		_ai.LookAtPoint(aim);
		if (Time.time < _nextAct || _character.InAttack() || !_ai.IsLookingAtPoint(aim, 15f))
		{
			return;
		}
		// the game's own checks for a player's repair: the piece's crafting station in range of where you stand, ward access
		Piece piece = _piece.GetComponent<Piece>();
		bool noBench = ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoWorkbench);
		if (!noBench && piece != null && piece.m_craftingStation != null && CraftingStation.HaveBuildStationInRange(piece.m_craftingStation.m_name, me) == null)
		{
			Skip(_piece, 300f, "No " + MHR_List.Local(piece.m_craftingStation.m_name) + " in range of where it can stand to repair " + PieceName(_piece));
			return;
		}
		if (!PrivateArea.CheckAccess(_piece.transform.position, 0f, flash: false))
		{
			Skip(_piece, 600f, "A ward blocks repairing " + PieceName(_piece));
			return;
		}
		float cost = SwingStamina(hammer);
		if (_stamina != null && _stamina.Stamina < cost)
		{
			if (Time.time >= _nextWaitLog)
			{
				_nextWaitLog = Time.time + 20f;
				LogDebug("Catching its breath before the next swing (" + _stamina.Stamina.ToString("F0") + "/" + cost.ToString("F0") + " stamina)");
			}
			return;
		}
		_nextAct = Time.time + WorkTime;
		if (!_piece.Repair())
		{
			return;   // the game's 1 s repair cooldown, or someone else just repaired it
		}
		string anim = hammer.m_shared.m_attack?.m_attackAnimation;
		GetComponent<ZSyncAnimation>()?.SetTrigger(!string.IsNullOrEmpty(anim) ? anim : "swing_hammer");
		if (piece != null)
		{
			piece.m_placeEffect.Create(piece.transform.position, piece.transform.rotation, null, 1f, -1, _character.GetZDOID());
		}
		_stamina?.UseStamina(cost);
		if (hammer.m_shared.m_useDurability)
		{
			hammer.m_durability = Mathf.Max(0f, hammer.m_durability - hammer.m_shared.m_useDurabilityDrain * Game.m_durabilityRate);
		}
		Log("Repaired " + PieceName(_piece));
		_piece = null;
		_step = Step.Idle;
		_scanTimer = 0.2f;
		if (!PlanRepair())
		{
			PutHammerAway();
			Say("hc_speech_homestead_repaired");
		}
	}

	private float SwingStamina(ItemDrop.ItemData hammer)
	{
		float cost = hammer.m_shared.m_attack != null ? hammer.m_shared.m_attack.m_attackStamina : 0f;
		PieceTable table = hammer.m_shared.m_buildPieces;
		if (table != null && table.m_skill != Skills.SkillType.None)
		{
			cost -= cost * 0.5f * _character.GetSkillFactor(table.m_skill);
		}
		return Mathf.Max(0f, cost);
	}

	private ItemDrop.ItemData Hammer()
	{
		Inventory inv = _humanoid.GetInventory();
		if (inv == null)
		{
			return null;
		}
		ItemDrop.ItemData best = null;
		foreach (ItemDrop.ItemData it in inv.GetAllItems())
		{
			ItemDrop.ItemData.SharedData s = it?.m_shared;
			if (s?.m_buildPieces == null || s.m_buildPieces.name == null || s.m_buildPieces.name.IndexOf("Hammer", StringComparison.OrdinalIgnoreCase) < 0)
			{
				continue;
			}
			if (s.m_useDurability && it.m_durability <= 0f)
			{
				continue;
			}
			if (best == null || it.m_durability > best.m_durability)
			{
				best = it;
			}
		}
		return best;
	}

	private void PutHammerAway()
	{
		if (!_holdingHammer || _humanoid == null)
		{
			return;
		}
		_holdingHammer = false;
		// 1.4.124: the game doesn't count a hammer as a weapon (GetCurrentWeapon skips tools) - find it in the pack
		Inventory inv = _humanoid.GetInventory();
		if (inv != null)
		{
			foreach (ItemDrop.ItemData it in inv.GetAllItems())
			{
				if (it?.m_shared?.m_buildPieces != null && _humanoid.IsItemEquiped(it))
				{
					_humanoid.UnequipItem(it);
				}
			}
		}
		if (_setup != null && _setup.SuppressAutoEquip)
		{
			_setup.SuppressAutoEquip = false;
			_setup.SyncEquipmentToInventory();
		}
	}

	// within reach of the piece like a player: the nearest part of it within Reach of the eyes, nothing big in between
	// 1.4.124: several points on the piece (nearest surface point and middle of each part); the nearest one that's in
	// reach with a clear line wins. 1.4.123 test: a pole set under the floor only had its nearest point checked, the
	// floor touching it blocked the line, and the companion stood next to it doing nothing.
	private bool PieceInReach(Vector3 feet, WearNTear w, out Vector3 aim)
	{
		Vector3 eye = feet + Vector3.up * MHR_Chests.EyeHeight;
		aim = w.transform.position;
		float best = float.MaxValue;
		bool found = false;
		_lastBlocker = null;
		foreach (Collider col in w.GetComponentsInChildren<Collider>())
		{
			if (col == null || !col.enabled || col.isTrigger)
			{
				continue;
			}
			Vector3 near = col.ClosestPoint(eye);
			Vector3 mid = col.bounds.center;
			for (int k = 0; k < 2; k++)
			{
				Vector3 p = k == 0 ? near : mid;
				float d = Vector3.Distance(eye, p);
				if (d > Reach || d >= best)
				{
					continue;
				}
				string blocker;
				if (d < 1.5f || Clear(eye, p, w, out blocker))
				{
					best = d;
					aim = p;
					found = true;
				}
				else if (_lastBlocker == null)
				{
					_lastBlocker = blocker;
				}
			}
		}
		return found;
	}

	private string _lastBlocker;

	// ---- restock ----

	private bool PlanFuel()
	{
		if (ModConfig.UpkeepRestock != null && !ModConfig.UpkeepRestock.Value)
		{
			return false;
		}
		float below = ModConfig.UpkeepFuelBelow?.Value ?? 0.8f;
		Inventory bag = _humanoid.GetInventory();
		Vector3 me = base.transform.position;
		s_pieces.Clear();
		Piece.GetAllPiecesInRadius(Center(), Range(), s_pieces);
		Fireplace best = null;
		float bestD = float.MaxValue;
		bool bestInBag = false;
		Container bestChest = null;
		HashSet<string> missing = new HashSet<string>();
		for (int i = 0; i < s_pieces.Count; i++)
		{
			Fireplace f = s_pieces[i] != null ? s_pieces[i].GetComponent<Fireplace>() : null;
			if (f == null || !f.m_canRefill || f.m_infiniteFuel || f.m_fuelItem == null || f.m_maxFuel <= 0f || Skipped(f) || !RefuelOn(f))
			{
				continue;
			}
			ZNetView nv = f.GetComponent<ZNetView>();
			if (nv == null || !nv.IsValid() || Fuel(f) / f.m_maxFuel >= below)
			{
				continue;
			}
			string fuel = f.m_fuelItem.m_itemData.m_shared.m_name;
			bool inBag = bag.CountItems(fuel) > 0;
			Container chest = null;
			if (!inBag)
			{
				if (missing.Contains(fuel))
				{
					continue;
				}
				chest = ChestWith(fuel);
				if (chest == null)
				{
					missing.Add(fuel);
					continue;
				}
			}
			float d = Vector3.Distance(me, f.transform.position) + (inBag ? 0f : 20f);
			if (d < bestD)
			{
				bestD = d;
				best = f;
				bestInBag = inBag;
				bestChest = chest;
			}
		}
		if (best == null)
		{
			if (missing.Count > 0 && Time.time >= _nextWaitLog)
			{
				_nextWaitLog = Time.time + 120f;
				Log("Fires need fuel but there's none in the pack or the chests: " + string.Join(", ", Locals(missing)));
			}
			return false;
		}
		_fire = best;
		string name = best.m_fuelItem.m_itemData.m_shared.m_name;
		int need = Need(best);
		if (Time.time - _lastSpeech > 120f)
		{
			Say("hc_speech_homestead_refuel");
		}
		if (bestInBag)
		{
			Log("Topping up " + PieceName(best) + " (" + Fuel(best).ToString("F1") + "/" + best.m_maxFuel.ToString("F0") + ") with " + MHR_List.Local(name) + " from the pack");
			Enter(Step.Fuel);
			return true;
		}
		_chest = bestChest;
		_key = name;
		_amount = need;
		Log("Fetching " + need + " " + MHR_List.Local(name) + " from a chest for " + PieceName(best) + " (" + Fuel(best).ToString("F1") + "/" + best.m_maxFuel.ToString("F0") + ")");
		Enter(Step.FuelFetch);
		return true;
	}

	// 1.4.118: each kind of fire can be left alone (user: "one for fires, one for brazier, one for hearth, one for
	// standing torches, etc."), told apart by the game's piece name
	private static bool RefuelOn(Fireplace f)
	{
		string n = Utils.GetPrefabName(f.gameObject).ToLowerInvariant();
		ConfigEntry<bool> e;
		if (n.Contains("hearth"))
		{
			e = ModConfig.RefuelHearths;
		}
		else if (n.Contains("brazier"))
		{
			e = ModConfig.RefuelBraziers;
		}
		else if (n.Contains("groundtorch"))
		{
			e = ModConfig.RefuelStandingTorches;
		}
		else if (n.Contains("walltorch") || n.Contains("sconce"))
		{
			e = ModConfig.RefuelWallTorches;
		}
		else if (n.Contains("bathtub") || n.Contains("hottub"))
		{
			e = ModConfig.RefuelHotTubs;
		}
		else if (n.Contains("fire_pit") || n.Contains("firepit") || n.Contains("bonfire") || n.Contains("campfire"))
		{
			e = ModConfig.RefuelFires;
		}
		else
		{
			e = ModConfig.RefuelOther;
		}
		return e == null || e.Value;
	}

	private static float Fuel(Fireplace f)
	{
		ZNetView nv = f.GetComponent<ZNetView>();
		return nv != null && nv.IsValid() ? nv.GetZDO().GetFloat(ZDOVars.s_fuel) : 0f;
	}

	private static int Need(Fireplace f)
	{
		return Mathf.Max(0, Mathf.FloorToInt(f.m_maxFuel - Fuel(f)));
	}

	private void UpdateFuel()
	{
		if (_fire == null || !_fire.GetComponent<ZNetView>().IsValid())
		{
			Done("The fire is gone");
			return;
		}
		Inventory bag = _humanoid.GetInventory();
		string fuel = _fire.m_fuelItem.m_itemData.m_shared.m_name;
		if (Mathf.CeilToInt(Fuel(_fire)) >= _fire.m_maxFuel)
		{
			Log(PieceName(_fire) + " is full");
			Say("hc_speech_homestead_stoked");
			Done(null);
			return;
		}
		if (bag.CountItems(fuel) <= 0)
		{
			Done("Out of " + MHR_List.Local(fuel) + " for " + PieceName(_fire));
			return;
		}
		Vector3 me = base.transform.position;
		Vector3 point = _fire.transform.position + Vector3.up * 0.3f;
		if (!PointInReach(me, point, _fire))
		{
			if (Time.time - _stepStart > GiveUp)
			{
				Skip(_fire, 300f, "Couldn't get within reach of " + PieceName(_fire) + " - leaving it for now");
				return;
			}
			if (!Approach(_fire.transform.position, _fire))
			{
				Skip(_fire, 300f, "No way to get to " + PieceName(_fire) + " - leaving it for now");
				return;
			}
			if (!_arrived)
			{
				_arrivedAt = 0f;
			}
			else if (_arrivedAt == 0f)
			{
				_arrivedAt = Time.time;
			}
			else if (Time.time - _arrivedAt > 3f)
			{
				Skip(_fire, 300f, "Can't reach " + PieceName(_fire) + " from where it can stand - leaving it for now");
			}
			return;
		}
		_ai.StopMoving();
		MHR_Mover.Release(_ai);
		_ai.LookAtPoint(point);
		if (Time.time < _nextAct || !_ai.IsLookingAtPoint(point, 20f))
		{
			return;
		}
		_nextAct = Time.time + WorkTime * 0.6f;
		// the same as a player adding fuel: one piece out of the pack, one fuel into the fire
		ZNetView nv = _fire.GetComponent<ZNetView>();
		if (!nv.HasOwner())
		{
			nv.ClaimOwnership();
		}
		GetComponent<ZSyncAnimation>()?.SetTrigger("interact");
		bag.RemoveItem(fuel, 1);
		Took(fuel, -1);
		nv.InvokeRPC("RPC_AddFuel");
	}

	// ---- sorting: like with like ----

	private sealed class Stock
	{
		public string Name;

		public readonly Dictionary<Container, int> Count = new Dictionary<Container, int>();
	}

	private bool PlanSort()
	{
		if (ModConfig.UpkeepSort != null && !ModConfig.UpkeepSort.Value)
		{
			return false;
		}
		MHR_List allow = MHR_List.Get(ModConfig.UpkeepSortItems?.Value ?? "*");
		bool gear = ModConfig.UpkeepSortGear?.Value ?? false;
		List<Container> chests = Chests();
		Dictionary<string, Stock> stock = new Dictionary<string, Stock>();
		Container tidy = null;
		string tidyKey = null;
		foreach (Container c in chests)
		{
			Dictionary<string, int> partial = new Dictionary<string, int>();
			foreach (ItemDrop.ItemData it in c.GetInventory().GetAllItems())
			{
				if (!Sortable(it, allow, gear))
				{
					continue;
				}
				string key = Key(it);
				Stock s;
				if (!stock.TryGetValue(key, out s))
				{
					s = new Stock { Name = it.m_shared.m_name };
					stock[key] = s;
				}
				int n;
				s.Count.TryGetValue(c, out n);
				s.Count[c] = n + it.m_stack;
				if (it.m_shared.m_maxStackSize > 1 && it.m_stack < it.m_shared.m_maxStackSize)
				{
					partial.TryGetValue(key, out n);
					partial[key] = n + 1;
					if (n + 1 >= 2 && tidy == null)
					{
						tidy = c;
						tidyKey = key;
					}
				}
			}
		}
		Vector3 me = base.transform.position;
		Vector3 home = Center();
		Container bestFrom = null;
		Container bestTo = null;
		string bestKey = null;
		float bestD = float.MaxValue;
		foreach (KeyValuePair<string, Stock> kv in stock)
		{
			if (kv.Value.Count.Count < 2)
			{
				continue;
			}
			// the chest that already holds the most of it (ties: the one nearer home, so the choice never flips)
			Container to = null;
			int most = -1;
			float toD = 0f;
			foreach (KeyValuePair<Container, int> c in kv.Value.Count)
			{
				float d = Vector3.Distance(home, c.Key.transform.position);
				if (c.Value > most || (c.Value == most && d < toD))
				{
					most = c.Value;
					to = c.Key;
					toD = d;
				}
			}
			if (!RoomIn(to, kv.Key))
			{
				continue;
			}
			foreach (KeyValuePair<Container, int> c in kv.Value.Count)
			{
				if (c.Key == to)
				{
					continue;
				}
				float d = Vector3.Distance(me, c.Key.transform.position) + Vector3.Distance(c.Key.transform.position, to.transform.position);
				if (d < bestD)
				{
					bestD = d;
					bestFrom = c.Key;
					bestTo = to;
					bestKey = kv.Key;
				}
			}
		}
		if (bestFrom != null)
		{
			_chest = bestFrom;
			_target = bestTo;
			_key = bestKey;
			string name = stock[bestKey].Name;
			Log("Sorting: " + stock[bestKey].Count[bestFrom] + " " + MHR_List.Local(name) + " from the chest at " + MHR_Mover.Fmt(bestFrom.transform.position) + " to the one with the most (" + stock[bestKey].Count[bestTo] + ") at " + MHR_Mover.Fmt(bestTo.transform.position));
			if (Time.time - _lastSpeech > 300f)
			{
				Say("hc_speech_homestead_tidy");
			}
			Enter(Step.SortTake);
			return true;
		}
		if (tidy != null)
		{
			_chest = tidy;
			_key = tidyKey;
			Log("Merging part-stacks of " + MHR_List.Local(stock[tidyKey].Name) + " in the chest at " + MHR_Mover.Fmt(tidy.transform.position));
			Enter(Step.Tidy);
			return true;
		}
		return false;
	}

	private static bool Sortable(ItemDrop.ItemData it, MHR_List allow, bool gear)
	{
		if (it?.m_shared == null || it.m_dropPrefab == null)
		{
			return false;
		}
		if (it.m_shared.m_maxStackSize <= 1 && !gear)
		{
			return false;
		}
		return allow.Allows(it.m_dropPrefab.name, it.m_shared.m_name, MHR_List.Local(it.m_shared.m_name));
	}

	// items that stack together: same item, quality and world level (and variant for things like banners)
	private static string Key(ItemDrop.ItemData it)
	{
		return it.m_shared.m_name + "|" + it.m_quality + "|" + it.m_worldLevel + "|" + it.m_variant;
	}

	private static bool RoomIn(Container c, string key)
	{
		Inventory inv = c.GetInventory();
		if (inv.GetEmptySlots() > 0)
		{
			return true;
		}
		foreach (ItemDrop.ItemData it in inv.GetAllItems())
		{
			if (Key(it) == key && it.m_stack < it.m_shared.m_maxStackSize)
			{
				return true;
			}
		}
		return false;
	}

	private List<Container> Chests()
	{
		List<Container> list = new List<Container>();
		foreach (Container c in MHR_Chests.Near(Center(), Range()))
		{
			if (!Skipped(c) && MHR_Chests.Usable(c, this))
			{
				list.Add(c);
			}
		}
		return list;
	}

	private Container ChestWith(string sharedName)
	{
		Container best = null;
		float bestD = float.MaxValue;
		Vector3 me = base.transform.position;
		foreach (Container c in Chests())
		{
			if (c.GetInventory().CountItems(sharedName) <= 0)
			{
				continue;
			}
			float d = Vector3.Distance(me, c.transform.position);
			if (d < bestD)
			{
				bestD = d;
				best = c;
			}
		}
		return best;
	}

	// ---- leftovers it fetched go back ----

	private bool PlanStow()
	{
		if (_carried.Count == 0)
		{
			return false;
		}
		Inventory bag = _humanoid.GetInventory();
		foreach (string key in new List<string>(_carried.Keys))
		{
			if (_carried[key] <= 0 || CountKey(bag, key) <= 0)
			{
				_carried.Remove(key);
				continue;
			}
			Container to = StowChest(key);
			if (to == null)
			{
				if (Time.time >= _nextWaitLog)
				{
					_nextWaitLog = Time.time + 120f;
					Log("No chest with room for the leftover " + MHR_List.Local(NameOf(key)) + " - keeping it for now");
				}
				continue;
			}
			_chest = to;
			_key = key;
			LogDebug("Putting the leftover " + Mathf.Min(_carried[key], CountKey(bag, key)) + " " + MHR_List.Local(NameOf(key)) + " back");
			Enter(Step.Stow);
			return true;
		}
		return false;
	}

	private Container StowChest(string key)
	{
		Container best = null;
		int most = -1;
		float bestD = float.MaxValue;
		Vector3 me = base.transform.position;
		foreach (Container c in Chests())
		{
			if (!RoomIn(c, key))
			{
				continue;
			}
			int n = CountKey(c.GetInventory(), key);
			float d = Vector3.Distance(me, c.transform.position);
			if (n > most || (n == most && d < bestD))
			{
				most = n;
				best = c;
				bestD = d;
			}
		}
		return best;
	}

	private static string NameOf(string key)
	{
		int bar = key.IndexOf('|');
		return bar >= 0 ? key.Substring(0, bar) : key;
	}

	private static int CountKey(Inventory inv, string key)
	{
		int n = 0;
		foreach (ItemDrop.ItemData it in inv.GetAllItems())
		{
			if (Key(it) == key || (key.IndexOf('|') < 0 && it.m_shared.m_name == key))
			{
				n += it.m_stack;
			}
		}
		return n;
	}

	private void Took(string key, int n)
	{
		// fuel is tracked by item name; anything else by its full key
		int have;
		_carried.TryGetValue(key, out have);
		have += n;
		if (have <= 0)
		{
			_carried.Remove(key);
		}
		else
		{
			_carried[key] = have;
		}
	}

	// ---- walking to a chest and using it ----

	private void UpdateChest()
	{
		Container c = _step == Step.SortPut ? _target : _chest;
		if (c == null || c.GetInventory() == null || !c.GetComponent<ZNetView>().IsValid())
		{
			Done("The chest is gone");
			return;
		}
		Vector3 me = base.transform.position;
		if (!MHR_Chests.InReach(me, c))
		{
			if (Time.time - _stepStart > GiveUp)
			{
				Skip(c, 300f, "Couldn't get within reach of the chest at " + MHR_Mover.Fmt(c.transform.position) + " - leaving it for now");
				return;
			}
			if (!_hasSpot || Time.time >= _nextSpotPick)
			{
				_nextSpotPick = Time.time + 4f;
				Vector3 spot;
				if (MHR_Chests.PickApproach(me, c, out spot))
				{
					_spot = spot;
					_hasSpot = true;
				}
				else if (!_hasSpot)
				{
					Skip(c, 300f, "No spot to stand by the chest at " + MHR_Mover.Fmt(c.transform.position));
					return;
				}
			}
			MHR_Nav.Move(_ai, Time.deltaTime, _spot, 0.6f, run: true, Tag);
			if (MHR_Mover.GaveUp(_ai))
			{
				Skip(c, 300f, "Couldn't get to the chest at " + MHR_Mover.Fmt(c.transform.position));
			}
			return;
		}
		_ai.StopMoving();
		MHR_Mover.Release(_ai);
		_ai.LookAtPoint(c.transform.position);
		// 1.4.152: MHR_Chests.Open - in use here or on another player's game, and owned before it's marked in use
		if (_nextAct == 0f && !MHR_Chests.Open(c))
		{
			Skip(c, 30f, "Someone is using the chest");
			return;
		}
		if (_nextAct == 0f)
		{
			// open it like a player: the interact move, the lid, a moment to look in
			_open = c;
			_nextAct = Time.time + Mathf.Max(0.5f, WorkTime);
			GetComponent<ZSyncAnimation>()?.SetTrigger("interact");
			return;
		}
		if (Time.time < _nextAct)
		{
			return;
		}
		Inventory bag = _humanoid.GetInventory();
		Inventory inv = c.GetInventory();
		switch (_step)
		{
		case Step.PileTake:
			PileTaken(c, inv, bag);
			return;
		case Step.FuelFetch:
		{
			int got = Move(inv, bag, _key, byName: true, Mathf.Max(1, _amount));
			Took(_key, got);
			CloseChest(c);
			if (got <= 0)
			{
				Skip(c, 120f, "No " + MHR_List.Local(_key) + " to take after all");
				return;
			}
			Log("Took " + got + " " + MHR_List.Local(_key) + " from the chest");
			Fireplace f = _fire;
			Done(null);
			if (f != null)
			{
				_fire = f;
				Enter(Step.Fuel);
			}
			return;
		}
		case Step.SortTake:
		{
			int got = Move(inv, bag, _key, byName: false, int.MaxValue);
			Took(_key, got);
			CloseChest(c);
			if (got <= 0)
			{
				Skip(c, 60f, "Nothing to take after all (no room in the pack, or it's gone)");
				return;
			}
			LogDebug("Took " + got + " " + MHR_List.Local(NameOf(_key)) + " to carry over");
			_chest = null;
			Enter(Step.SortPut);
			return;
		}
		case Step.SortPut:
		case Step.Stow:
		{
			int want = Mathf.Min(_carried.TryGetValue(_key, out int carried) ? carried : 0, CountKey(bag, _key));
			int put = Move(bag, inv, _key, byName: _key.IndexOf('|') < 0, want);
			Took(_key, -put);
			Merge(inv, _key);
			CloseChest(c);
			Log((_step == Step.Stow ? "Put back " : "Sorted ") + put + " " + MHR_List.Local(NameOf(_key)) + " into the chest at " + MHR_Mover.Fmt(c.transform.position) + (put < want ? " (" + (want - put) + " didn't fit - they go in another chest)" : ""));
			if (put < want)
			{
				_skip[c.GetInstanceID()] = Time.time + 60f;
			}
			Done(null);
			return;
		}
		case Step.Tidy:
		{
			int merged = Merge(inv, _key);
			CloseChest(c);
			LogDebug("Merged " + merged + " part-stack(s)");
			if (merged == 0)
			{
				_skip[c.GetInstanceID()] = Time.time + 300f;
			}
			if (Time.time - _lastSpeech > 300f)
			{
				Say("hc_speech_homestead_tidied");
			}
			Done(null);
			return;
		}
		}
		Done(null);
	}

	private void CloseChest(Container c = null)
	{
		if (_open != null)
		{
			try
			{
				_open.SetInUse(inUse: false);
			}
			catch
			{
			}
		}
		_open = null;
	}

	// moves up to 'max' of an item between inventories, a stack at a time, as far as there's room (and, into the pack,
	// as far as the companion can carry); returns how many moved
	private int Move(Inventory from, Inventory to, string key, bool byName, int max)
	{
		bool intoBag = to == _humanoid.GetInventory();
		int moved = 0;
		foreach (ItemDrop.ItemData it in new List<ItemDrop.ItemData>(from.GetAllItems()))
		{
			if (moved >= max)
			{
				break;
			}
			if (byName ? it.m_shared.m_name != key : Key(it) != key)
			{
				continue;
			}
			int n = Mathf.Min(it.m_stack, max - moved);
			if (intoBag)
			{
				float w = it.m_shared.m_weight;
				float room = CompanionCarry.Max(_humanoid) - to.GetTotalWeight();
				if (w > 0f)
				{
					n = Mathf.Min(n, Mathf.FloorToInt(room / w));
				}
			}
			while (n > 0 && !to.CanAddItem(it, n))
			{
				n--;
			}
			if (n <= 0)
			{
				break;
			}
			ItemDrop.ItemData copy = it.Clone();
			copy.m_stack = n;
			if (!to.AddItem(copy))
			{
				break;
			}
			from.RemoveItem(it, n);
			moved += n;
		}
		return moved;
	}

	// the smallest part-stack is taken out and put back in, which fills the other part-stacks first; returns how many
	// part-stacks were merged away
	private static int Merge(Inventory inv, string key)
	{
		int merged = 0;
		for (int guard = 0; guard < 32; guard++)
		{
			ItemDrop.ItemData small = null;
			int partials = 0;
			foreach (ItemDrop.ItemData it in inv.GetAllItems())
			{
				if (it.m_shared.m_maxStackSize <= 1 || it.m_stack >= it.m_shared.m_maxStackSize)
				{
					continue;
				}
				if (key.IndexOf('|') < 0 ? it.m_shared.m_name != key : Key(it) != key)
				{
					continue;
				}
				partials++;
				if (small == null || it.m_stack < small.m_stack)
				{
					small = it;
				}
			}
			if (partials < 2)
			{
				break;
			}
			ItemDrop.ItemData copy = small.Clone();
			inv.RemoveItem(small);
			inv.AddItem(copy);
			merged++;
		}
		return merged;
	}

	// ---- moving ----

	private bool _arrived;

	private float _arrivedAt;

	private bool Approach(Vector3 target, Component piece)
	{
		Vector3 me = base.transform.position;
		if (!_hasSpot || Time.time >= _nextSpotPick)
		{
			_nextSpotPick = Time.time + 4f;
			Vector3 spot;
			if (MHR_Chests.PickApproachPiece(me, piece, out spot))
			{
				_spot = spot;
				_hasSpot = true;
			}
			else if (!_hasSpot)
			{
				_spot = target;
				_hasSpot = true;
			}
		}
		_arrived = MHR_Nav.Move(_ai, Time.deltaTime, _spot, _spot == target ? 1.5f : 0.6f, run: true, Tag);
		return !MHR_Mover.GaveUp(_ai);
	}

	private static bool PointInReach(Vector3 feet, Vector3 point, Component target)
	{
		Vector3 eye = feet + Vector3.up * MHR_Chests.EyeHeight;
		float d = Vector3.Distance(eye, point);
		return d <= MHR_Chests.EyeReach && (d < 1.5f || Clear(eye, point, target));
	}

	private static int s_mask = -1;

	// nothing big and solid between the eyes and the spot (the target itself, people and small clutter don't count)
	private static bool Clear(Vector3 eye, Vector3 point, Component target)
	{
		string blocker;
		return Clear(eye, point, target, out blocker);
	}

	// 1.4.124: something hit right at the target point (a floor or wall touching the piece) doesn't count as in the way
	private static bool Clear(Vector3 eye, Vector3 point, Component target, out string blocker)
	{
		blocker = null;
		if (s_mask == -1)
		{
			s_mask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain", "vehicle");
		}
		Piece own = target != null ? target.GetComponentInParent<Piece>() : null;
		Vector3 d = point - eye;
		float len = d.magnitude;
		if (len < 0.05f)
		{
			return true;
		}
		foreach (RaycastHit hit in Physics.RaycastAll(eye, d / len, Mathf.Max(0f, len - 0.05f), s_mask))
		{
			Collider col = hit.collider;
			if (col == null || (own != null && col.GetComponentInParent<Piece>() == own) || col.GetComponentInParent<Character>() != null)
			{
				continue;
			}
			if (len - hit.distance < 0.5f)
			{
				continue;
			}
			Vector3 size = col.bounds.size;
			if (Mathf.Max(size.x, Mathf.Max(size.y, size.z)) < 1.2f)
			{
				continue;
			}
			Piece bp = col.GetComponentInParent<Piece>();
			blocker = bp != null ? (MHR_List.Local(bp.m_name) ?? bp.name) : col.name;
			return false;
		}
		return true;
	}

	private void UpdateHome()
	{
		if (Vector3.Distance(base.transform.position, Center()) < 6f)
		{
			Done(_atHome ? "Back home" : "Back where it was working");
			_scanTimer = ModConfig.UpkeepScanInterval?.Value ?? 5f;
			return;
		}
		MHR_Nav.Move(_ai, Time.deltaTime, Center(), 4f, run: true, Tag);
		if (MHR_Mover.GaveUp(_ai) || Time.time - _stepStart > GiveUp * 2f)
		{
			Done("Couldn't get back - staying here for now");
			_scanTimer = 30f;
		}
	}

	// ------------------------------------------------------------------ misc

	private static string PieceName(Component c)
	{
		Piece p = c != null ? c.GetComponent<Piece>() : null;
		string n = p != null ? MHR_List.Local(p.m_name) : null;
		return "the " + (string.IsNullOrEmpty(n) ? (c != null ? Utils.GetPrefabName(c.gameObject) : "piece") : n) + (c != null ? " at " + MHR_Mover.Fmt(c.transform.position) : "");
	}

	private static IEnumerable<string> Locals(IEnumerable<string> names)
	{
		foreach (string n in names)
		{
			yield return MHR_List.Local(n) ?? n;
		}
	}

	private void Say(string key)
	{
		if (Time.time - _lastSpeech < 30f)
		{
			return;
		}
		_lastSpeech = Time.time;
		_talk?.Say(ModLocalization.Loc(key));
	}

	private static void Log(string msg)
	{
		CompanionsPlugin.Log?.LogInfo("[Upkeep] " + msg);
	}

	private static void LogDebug(string msg)
	{
		CompanionsPlugin.Log?.LogDebug("[Upkeep] " + msg);
	}
}
