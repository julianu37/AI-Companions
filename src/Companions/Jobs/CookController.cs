using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace Companions;

// 1.4.87: the Cook job, rewritten (design agreed with the user 2026-10-07, see docs/DEV_NOTES.md).
// Works like a player at home or at a camp:
// - Uses every cooking station: cauldron and food preparation table (crafted there, needs fire/roof like for a player),
//   cooking station, iron cooking station and oven (items go on, come off when done, oven gets wood).
// - Makes the best food it can: a recipe the player knows, food below Cooking.FoodTarget in the chests in range,
//   and every raw ingredient above Cooking.IngredientReserve (the player always keeps some to cook with).
//   Prep table -> oven chains (dough, raw pies) count as the food they turn into.
// - Mead: brews the bases in Cooking.MeadFocus for the last boss beaten (up to MeadBaseTarget), keeps fermenters
//   going while the finished mead is below MeadTarget, taps finished fermenters.
// - Takes finished food off any cooking station in range before it burns.
// - Puts away exactly what it made (a ledger kept on the companion), not food it was given to eat.
public partial class CookController : MonoBehaviour
{
	internal enum CookPhase
	{
		Idle,
		Fetching,
		MovingToStation,
		Crafting,
		Cooking,
		Fermenting,
		CollectingDrops,
		Storing,
		TakingPile
	}

	private enum TaskKind
	{
		None,
		Craft,
		Grill,
		Rescue,
		Fill,
		Tap,
		Store,
		Hive,
		Stoke,
		Pile
	}

	private class Task
	{
		public TaskKind Kind;

		public Recipe Recipe;

		public CraftingStation Station;

		public CookingStation Cooker;

		public Fermenter Fermenter;

		public Beehive Hive;

		public Fireplace Fire;

		public Piece Pile;

		public string Item;

		public int Count;

		public string Final;

		public readonly List<KeyValuePair<string, int>> Fetch = new List<KeyValuePair<string, int>>();

		public string Describe()
		{
			switch (Kind)
			{
			case TaskKind.Craft:
				return "make " + Count + "x " + Name(Recipe?.m_item?.gameObject.name) + " at the " + Loc(Station?.m_name);
			case TaskKind.Grill:
				return "cook " + Count + "x " + Name(Item) + " on the " + Loc(Cooker?.m_name);
			case TaskKind.Rescue:
				return "take finished food off the " + Loc(Cooker?.m_name);
			case TaskKind.Fill:
				return "fill a fermenter with " + Name(Item);
			case TaskKind.Tap:
				return "tap a fermenter";
			case TaskKind.Store:
				return "put away " + Name(Item);
			case TaskKind.Hive:
				return "collect honey from a beehive";
			case TaskKind.Stoke:
				return "add " + Count + "x " + Name(Item) + " to the " + Loc(Fire != null ? Fire.m_name : Cooker?.m_name);
			case TaskKind.Pile:
				return "take down a pile for " + Name(Item);
			default:
				return "nothing";
			}
		}
	}

	private CompanionAI _ai;

	private Humanoid _humanoid;

	private Character _character;

	private CompanionSetup _setup;

	private ZNetView _nview;

	private ZSyncAnimation _zanim;

	private CompanionTalk _talk;

	private CookPhase _phase;

	private Task _task;

	private float _scanTimer;

	private float _timer;

	private float _phaseStart;

	private float _nextWatchLog;

	private int _fetchIndex;

	private Container _chest;

	private bool _chestOpen;

	private Vector3 _cookArea;

	private bool _hasCookArea;

	private bool _allDoneNotified;

	private readonly List<Container> _chests = new List<Container>();

	private readonly List<CraftingStation> _stations = new List<CraftingStation>();

	private readonly List<CookingStation> _cookers = new List<CookingStation>();

	private readonly List<Fermenter> _fermenters = new List<Fermenter>();

	private readonly List<Beehive> _hives = new List<Beehive>();

	private readonly List<Piece> _pieces = new List<Piece>();

	private readonly HashSet<int> _unreachable = new HashSet<int>();

	private float _unreachableClearAt;

	private readonly Dictionary<int, float> _stationProblemUntil = new Dictionary<int, float>();

	// a food that couldn't be made just now (an ingredient out of reach, a station problem): leave it a minute
	private readonly Dictionary<string, float> _skipUntil = new Dictionary<string, float>();

	private readonly Dictionary<string, int> _ledger = new Dictionary<string, int>();

	private bool _ledgerLoaded;

	private string _approachKey;

	private Vector3 _approachSpot;

	private bool _hasApproach;

	private float _stuckTimer;

	private static readonly HashSet<int> s_claimed = new HashSet<int>();

	private int _claimedId;

	private static readonly Collider[] s_drops = new Collider[48];

	private string _hiveDrop;

	private static int s_itemMask = -1;

	private static readonly int LedgerHash = "HC_CookLedger".GetStableHashCode();

	private static readonly FieldInfo s_haveFire = AccessTools.Field(typeof(CraftingStation), "m_haveFire");

	private static readonly MethodInfo s_isFireLit = AccessTools.Method(typeof(CookingStation), "IsFireLit");

	private const float ScanInterval = 4f;

	private const float UseReach = 2.5f;

	internal CookPhase Phase => _phase;

	public bool IsActive => _phase != CookPhase.Idle;

	private static float Range => ModConfig.CookRange?.Value ?? 25f;

	private static int Reserve => ModConfig.CookIngredientReserve?.Value ?? 20;

	private static int FoodTarget => ModConfig.CookFoodTarget?.Value ?? 20;

	private static int BaseTarget => ModConfig.CookMeadBaseTarget?.Value ?? 10;

	private static int Batch => ModConfig.CookBatchSize?.Value ?? 5;

	private static int MeadTarget => ModConfig.CookMeadTarget?.Value ?? 20;

	public void NotifyActionModeChanged()
	{
		if (_phase != CookPhase.Idle)
		{
			Abort("mode changed");
		}
	}

	internal bool IsCookMode()
	{
		ZDO zDO = _nview?.GetZDO();
		return zDO != null && zDO.GetInt(CompanionSetup.ActionModeHash) == 17;
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
	}

	private void OnDestroy()
	{
		Unclaim();
		CloseChest();
	}

	private void Update()
	{
		if (CompanionRest.MHR_ShouldPauseWork(this) || _nview == null || _nview.GetZDO() == null || !_nview.IsOwner() || _character == null || _character.GetHealth() <= 0f)
		{
			return;
		}
		// 1.4.128: picking up what a kill dropped goes first - two jobs steering at once made the companion stutter
		if (MHR_Loot.Active(this) || MHR_JobRules.OrderPending(this))
		{
			return;
		}
		if (!_ledgerLoaded)
		{
			LoadLedger();
		}
		if ((_ai != null && _ai.IsInCombat) || CompanionInteractPanel.IsOpenFor(_setup) || CompanionRadialMenu.IsOpenFor(_setup))
		{
			if (_phase != CookPhase.Idle)
			{
				Abort("interrupted");
			}
			return;
		}
		if (!IsCookMode())
		{
			if (_phase != CookPhase.Idle)
			{
				Abort("mode changed");
			}
			return;
		}
		float dt = Time.deltaTime;
		try
		{
			switch (_phase)
			{
			case CookPhase.Idle:
				UpdateIdle(dt);
				break;
			case CookPhase.Fetching:
				UpdateFetching(dt);
				break;
			case CookPhase.MovingToStation:
				UpdateMovingToStation(dt);
				break;
			case CookPhase.Crafting:
				UpdateCrafting(dt);
				break;
			case CookPhase.Cooking:
				UpdateCooking(dt);
				break;
			case CookPhase.Fermenting:
				UpdateFermenting(dt);
				break;
			case CookPhase.CollectingDrops:
				UpdateCollecting(dt);
				break;
			case CookPhase.Storing:
				UpdateStoring(dt);
				break;
			case CookPhase.TakingPile:
				UpdateTakingPile();
				break;
			}
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log.LogWarning("[Cook] " + ex.Message + " - starting over");
			Abort("error");
		}
	}

	// ---------------------------------------------------------------- deciding what to do

	private void UpdateIdle(float dt)
	{
		_scanTimer -= dt;
		if (_scanTimer > 0f)
		{
			return;
		}
		_scanTimer = ScanInterval;
		CookCatalog.Ensure();
		Scan();
		_whys.Clear();
		_foodWanted = false;
		_foodShortSaid = false;
		Task task = PickTask();
		if (task == null)
		{
			ShowWhy();
			if (!_allDoneNotified)
			{
				_allDoneNotified = true;
				Log("Nothing to cook right now");
				RestoreFollow();
			}
			_scanTimer = 10f;
			return;
		}
		_allDoneNotified = false;
		Begin(task);
	}

	private Task PickTask()
	{
		// 1) things that won't wait: finished food about to burn, finished fermenters
		foreach (CookingStation cooker in _cookers)
		{
			if (CountSlots(cooker, 1) + CountSlots(cooker, 2) > 0 && Usable(cooker))
			{
				return new Task { Kind = TaskKind.Rescue, Cooker = cooker };
			}
		}
		foreach (Fermenter fermenter in _fermenters)
		{
			if (!s_claimed.Contains(fermenter.GetInstanceID()) && FermenterState(fermenter) == 2 && Usable(fermenter))
			{
				return new Task { Kind = TaskKind.Tap, Fermenter = fermenter };
			}
		}
		// 1b) 1.4.93: honey from the beehives - when a hive is full, or when a priority mead base is short of honey
		// (user: "The NPC should be able to harvest bee hives to collect honey")
		Task hive = HiveTask(idle: false);
		if (hive != null)
		{
			return hive;
		}
		// 1c) 1.4.131: keep the fires under the cooking spots and the oven at [Cooking] FuelLevel (user)
		Task stoke = StokeTask();
		if (stoke != null)
		{
			return stoke;
		}
		// 2) a full bag of things it made: put them away first
		int carried = LedgerCarried();
		if (carried >= 10)
		{
			Task task = StoreTask();
			if (task != null)
			{
				return task;
			}
		}
		// 3) mead: fill an empty fermenter (priority bases first), brew a priority base for the next boss
		// 1.4.156: [Cooking] MakeMead off = no mead bases and no filling fermenters (finished mead is still tapped)
		bool mead = ModConfig.CookMakeMead == null || ModConfig.CookMakeMead.Value;
		Task task2 = mead ? (FillTask() ?? BrewTask(focus: true)) : null;
		if (task2 != null)
		{
			return task2;
		}
		// 4) the best food it can make
		Task task3 = FoodTask();
		if (task3 != null)
		{
			return task3;
		}
		// 5) 1.4.92: any other mead base the player knows (the priority list sets the order, it doesn't limit it)
		Task task4 = mead ? BrewTask(focus: false) : null;
		if (task4 != null)
		{
			return task4;
		}
		// 6) nothing to make: put away what it made, then empty any beehive with honey in it
		return ((carried > 0) ? StoreTask() : null) ?? HiveTask(idle: true);
	}

	private static int HoneyIn(Beehive hive)
	{
		return hive.GetComponent<ZNetView>()?.GetZDO()?.GetInt(ZDOVars.s_level) ?? 0;
	}

	// a beehive worth emptying: full, or (not idle) honey is what a priority mead base is waiting for; idle: any honey
	private Task HiveTask(bool idle)
	{
		bool needHoney = false;
		if (!idle && (ModConfig.CookMakeMead == null || ModConfig.CookMakeMead.Value))
		{
			foreach (string mead in CookCatalog.Focus())
			{
				Recipe recipe = CookCatalog.RecipeFor(mead);
				if (recipe == null)
				{
					continue;
				}
				foreach (Piece.Requirement req in recipe.m_resources)
				{
					if (req?.m_resItem != null && req.m_resItem.gameObject.name == "Honey" && Usable("Honey") < req.GetAmount(1))
					{
						needHoney = true;
					}
				}
			}
		}
		Beehive best = null;
		float bestDist = float.MaxValue;
		foreach (Beehive hive in _hives)
		{
			if (hive == null || !Usable(hive) || Skipped(TaskKind.Hive, hive.GetInstanceID().ToString()))
			{
				continue;
			}
			int honey = HoneyIn(hive);
			// idle: at least half full, so it isn't walking over for every drop
			if (honey <= 0 || (!idle && !needHoney && honey < hive.m_maxHoney) || (idle && honey * 2 < hive.m_maxHoney))
			{
				continue;
			}
			float num = Vector3.Distance(base.transform.position, hive.transform.position);
			if (num < bestDist)
			{
				best = hive;
				bestDist = num;
			}
		}
		return (best != null) ? new Task
		{
			Kind = TaskKind.Hive,
			Hive = best,
			Item = best.GetInstanceID().ToString()
		} : null;
	}

	private bool Skipped(TaskKind kind, string item)
	{
		return _skipUntil.TryGetValue(kind + ":" + item, out var value) && Time.time < value;
	}

	private Task FoodTask()
	{
		Task best = null;
		float bestScore = 0f;
		// crafted at the cauldron / prep table
		foreach (Recipe recipe in CookCatalog.Recipes)
		{
			string output = recipe.m_item.gameObject.name;
			string final = CookCatalog.FinalFood(output);
			if (final == null || CookCatalog.IsMeadBase(output) || !CookCatalog.Allowed(final) || !Known(recipe) || Skipped(TaskKind.Craft, output))
			{
				continue;
			}
			float score = CookCatalog.Value(final);
			if (score <= bestScore)
			{
				continue;
			}
			int stock = Count(final) + ((final != output) ? Count(output) : 0);
			int target = CookCatalog.Target(final, FoodTarget);
			if (stock >= target)
			{
				continue;
			}
			_foodWanted = true;
			// a prep-table item needs a cooking station in range that finishes it
			if (final != output && FindCooker(output, needFree: false) == null)
			{
				continue;
			}
			CraftingStation station = FindStation(recipe);
			if (station == null)
			{
				continue;
			}
			if (!IngredientsOk(recipe))
			{
				// 1.5.43: which ingredient it's short of, for the status ("Ooze 0/22 for Muckshake.")
				FoodShort(recipe, final);
				continue;
			}
			best = CraftTask(recipe, station, final, target - stock);
			bestScore = score;
		}
		// on a cooking station / oven
		foreach (CookingStation cooker in _cookers)
		{
			if (!Usable(cooker) || CountSlots(cooker, -1) == 0 || !HasHeat(cooker, orFuelInReach: true))
			{
				continue;
			}
			foreach (CookingStation.ItemConversion conv in cooker.m_conversion)
			{
				if (conv?.m_from == null || conv.m_to == null)
				{
					continue;
				}
				string from = conv.m_from.gameObject.name;
				string to = conv.m_to.gameObject.name;
				if (!CookCatalog.Allowed(to) || Skipped(TaskKind.Grill, from))
				{
					continue;
				}
				// finishing something already prepared beats starting something new of the same worth
				float score = CookCatalog.Value(to) + (CookCatalog.IsMade(from) ? 0.5f : 0f);
				if (score <= bestScore)
				{
					continue;
				}
				int room = CookCatalog.Target(to, FoodTarget) - Count(to);
				int have = Usable(from);
				if (room > 0)
				{
					_foodWanted = true;
				}
				int n = Mathf.Min(Mathf.Min(CountSlots(cooker, -1), Batch), Mathf.Min(room, have));
				if (n <= 0)
				{
					continue;
				}
				Task task = new Task
				{
					Kind = TaskKind.Grill,
					Cooker = cooker,
					Item = from,
					Final = to,
					Count = n
				};
				AddFetch(task, from, n);
				if (cooker.m_useFuel && cooker.m_fuelItem != null)
				{
					int fuelNeed = Mathf.Max(0, FuelTarget(cooker.m_maxFuel) - Mathf.FloorToInt(Fuel(cooker)));
					int fuelHave = Count(cooker.m_fuelItem.gameObject.name);
					if (fuelNeed > 0 && fuelHave > 0)
					{
						AddFetch(task, cooker.m_fuelItem.gameObject.name, Mathf.Min(fuelNeed, fuelHave));
					}
				}
				best = task;
				bestScore = score;
			}
		}
		return best;
	}

	// 1.4.92: why each focus base isn't being brewed, logged when it changes (1.4.91 log: no brewing, no reason given)
	private readonly Dictionary<string, string> _brewWhy = new Dictionary<string, string>();

	private void BrewWhy(string mead, string why)
	{
		// only the priority list is explained in the log (all twenty-odd bases would be noise)
		if (!CookCatalog.Focus().Contains(mead))
		{
			return;
		}
		if (!_brewWhy.TryGetValue(mead, out var value) || value != why)
		{
			_brewWhy[mead] = why;
			Log("Mead base " + Name(mead) + ": " + why);
		}
	}

	private Task BrewTask(bool focus)
	{
		List<string> focusList = CookCatalog.Focus();
		foreach (string mead in focus ? focusList : CookCatalog.MeadBases)
		{
			if (!focus && focusList.Contains(mead))
			{
				continue;
			}
			Recipe recipe = CookCatalog.RecipeFor(mead);
			if (recipe == null)
			{
				BrewWhy(mead, "no recipe for it");
				continue;
			}
			if (!Known(recipe))
			{
				BrewWhy(mead, "you don't know the recipe yet");
				continue;
			}
			if (Skipped(TaskKind.Craft, mead))
			{
				continue;
			}
			string finished = CookCatalog.MeadOf(mead);
			int have = Count(mead);
			if (have >= BaseTarget)
			{
				BrewWhy(mead, $"enough ({have} of {BaseTarget} bases)");
				continue;
			}
			int done = (finished != null) ? Count(finished) : 0;
			if (finished != null && done >= MeadTarget)
			{
				BrewWhy(mead, $"enough {Name(finished)} ({done} of {MeadTarget})");
				continue;
			}
			CraftingStation station = FindStation(recipe);
			if (station == null)
			{
				BrewWhy(mead, "no usable " + Loc(recipe.m_craftingStation?.m_name) + " in range (needs the right level, a fire and a roof)");
				AddWhy(true, "hc_why_cook_no_station", Loc(recipe.m_craftingStation?.m_name));
				continue;
			}
			string shortOf = ShortIngredient(recipe, mead);
			if (shortOf != null)
			{
				BrewWhy(mead, shortOf);
				continue;
			}
			BrewWhy(mead, "brewing");
			return CraftTask(recipe, station, mead, BaseTarget - have);
		}
		return null;
	}

	// 1.5.42: why it isn't cooking or brewing, for the status and the idle "why". User (log 4b94cbba): "It just reports the
	// need for honey and nothing else. I took all the ingredients so there really is nothing to cook... It should rotate
	// between missing details every so often." Every reason found in a scan is kept; one is shown per scan (10 s), in turn.
	// Mead reasons show as "Mead", food ones as "Cook".
	private class Why
	{
		public bool Mead;

		public string Key;

		public object[] Args;
	}

	private readonly List<Why> _whys = new List<Why>();

	// some food is under its target (FoodTask): nothing cooked means the ingredients are missing, not that it's stocked up
	private bool _foodWanted;

	private bool _foodShortSaid;

	private const int MaxWhys = 8;

	private void AddWhy(bool mead, string key, params object[] args)
	{
		if (_whys.Count >= MaxWhys)
		{
			return;
		}
		foreach (Why w in _whys)
		{
			if (w.Key == key && w.Mead == mead && string.Join("|", w.Args) == string.Join("|", args))
			{
				return;
			}
		}
		_whys.Add(new Why { Mead = mead, Key = key, Args = args });
	}

	private void ShowWhy()
	{
		// food last: the mead shortfalls are the specific ones
		bool mead = ModConfig.CookMakeMead == null || ModConfig.CookMakeMead.Value;
		AddFoodWhy();
		if (_whys.Count == 0)
		{
			CompanionStatus.Why(this, "hc_why_cook_nothing");
			return;
		}
		List<CompanionStatus.Item> items = new List<CompanionStatus.Item>();
		foreach (Why w in _whys)
		{
			items.Add(new CompanionStatus.Item(ModLocalization.Loc(w.Mead && mead ? "hc_status_job_mead" : "hc_status_job_cook"), w.Key, w.Args));
		}
		CompanionStatus.WhyRotate(this, items);
	}

	// 1.5.43 (phase 3): the status says what it's doing; mead work shows as "Mead"
	private void ShowDoing(Task t)
	{
		string label = null;
		string key = null;
		string what = "";
		switch (t.Kind)
		{
		case TaskKind.Craft:
		{
			string output = t.Recipe?.m_item?.gameObject.name;
			bool meadBase = output != null && CookCatalog.IsMeadBase(output);
			label = meadBase ? ModLocalization.Loc("hc_status_job_mead") : null;
			key = "hc_doing_cook_make";
			what = Name(output);
			break;
		}
		case TaskKind.Grill:
			key = "hc_doing_cook_make";
			what = Name(t.Final ?? t.Item);
			break;
		case TaskKind.Rescue:
			key = "hc_doing_cook_rescue";
			what = Loc(t.Cooker?.m_name);
			break;
		case TaskKind.Fill:
			label = ModLocalization.Loc("hc_status_job_mead");
			key = "hc_doing_mead_fill";
			what = Name(t.Item);
			break;
		case TaskKind.Tap:
			label = ModLocalization.Loc("hc_status_job_mead");
			key = "hc_doing_mead_tap";
			break;
		case TaskKind.Store:
			key = "hc_doing_cook_store";
			what = Name(t.Item);
			break;
		case TaskKind.Hive:
			key = "hc_doing_upkeep_honey";
			break;
		case TaskKind.Stoke:
			key = "hc_doing_upkeep_fuel";
			what = Loc(t.Fire != null ? t.Fire.m_name : t.Cooker?.m_name);
			break;
		case TaskKind.Pile:
			key = "hc_doing_take_pile";
			what = Loc(t.Pile?.m_name);
			break;
		}
		if (key != null)
		{
			CompanionStatus.DoingAs(this, label, key, what);
		}
	}

	private void AddFoodWhy()
	{
		// a food it wanted was short of an ingredient: that's said already (hc_why_cook_food_short)
		if (_foodShortSaid)
		{
			return;
		}
		if (_whys.Count >= MaxWhys)
		{
			_whys.RemoveAt(_whys.Count - 1);
		}
		_whys.Add(new Why { Mead = false, Key = _foodWanted ? "hc_why_cook_no_food" : "hc_why_cook_stocked", Args = new object[0] });
	}

	private void FoodShort(Recipe recipe, string final)
	{
		foreach (Piece.Requirement req in recipe.m_resources)
		{
			if (req?.m_resItem == null || req.GetAmount(1) <= 0)
			{
				continue;
			}
			string item = req.m_resItem.gameObject.name;
			if (Usable(item) < req.GetAmount(1))
			{
				bool made = CookCatalog.IsMade(item);
				AddWhy(false, "hc_why_mead_short", Name(item), Count(item), req.GetAmount(1) + (made ? 0 : Reserve), Name(final));
				_foodShortSaid = true;
				return;
			}
		}
	}

	// every ingredient that would go below the reserve: a reason each ("Honey 0/30 for Frost resistance mead" - the 30 is
	// the recipe's 10 plus the 20 kept back, [Cooking] IngredientReserve); the first as a sentence for the log
	private string ShortIngredient(Recipe recipe, string mead)
	{
		string first = null;
		string finished = CookCatalog.MeadOf(mead);
		string what = Name(finished ?? mead);
		foreach (Piece.Requirement req in recipe.m_resources)
		{
			if (req?.m_resItem == null || req.GetAmount(1) <= 0)
			{
				continue;
			}
			string item = req.m_resItem.gameObject.name;
			if (Usable(item) < req.GetAmount(1))
			{
				bool made = CookCatalog.IsMade(item);
				int need = req.GetAmount(1) + (made ? 0 : Reserve);
				AddWhy(true, "hc_why_mead_short", Name(item), Count(item), need, what);
				if (first == null)
				{
					first = made ? $"needs {req.GetAmount(1)} {Name(item)}, {Count(item)} in reach" : $"needs {req.GetAmount(1)} {Name(item)}, {Count(item)} in reach and {Reserve} are kept back";
				}
			}
		}
		return first;
	}

	private Task FillTask()
	{
		// priority bases first, then any other mead base in the bag or the chests (1.4.92)
		List<string> list = new List<string>(CookCatalog.Focus());
		foreach (string text in CookCatalog.MeadBases)
		{
			if (!list.Contains(text))
			{
				list.Add(text);
			}
		}
		foreach (string mead in list)
		{
			string finished = CookCatalog.MeadOf(mead);
			if (finished == null || Count(finished) >= MeadTarget || Count(mead) <= 0 || Skipped(TaskKind.Fill, mead))
			{
				continue;
			}
			foreach (Fermenter fermenter in _fermenters)
			{
				if (s_claimed.Contains(fermenter.GetInstanceID()) || FermenterState(fermenter) != 0 || !Usable(fermenter) || !Accepts(fermenter, mead))
				{
					continue;
				}
				Task task = new Task
				{
					Kind = TaskKind.Fill,
					Fermenter = fermenter,
					Item = mead
				};
				AddFetch(task, mead, 1);
				return task;
			}
		}
		return null;
	}

	// 1.4.87: a batch (Cooking.BatchSize), as many as the target has room for and the ingredients allow
	private Task CraftTask(Recipe recipe, CraftingStation station, string final, int room)
	{
		int per = Mathf.Max(1, recipe.m_amount);
		int times = Mathf.Min(Batch, Mathf.Max(1, (room + per - 1) / per));
		foreach (Piece.Requirement req in recipe.m_resources)
		{
			if (req?.m_resItem != null && req.GetAmount(1) > 0)
			{
				times = Mathf.Min(times, Usable(req.m_resItem.gameObject.name) / req.GetAmount(1));
			}
		}
		times = Mathf.Max(1, times);
		Task task = new Task
		{
			Kind = TaskKind.Craft,
			Recipe = recipe,
			Station = station,
			Item = recipe.m_item.gameObject.name,
			Final = final,
			Count = times
		};
		foreach (Piece.Requirement req2 in recipe.m_resources)
		{
			if (req2?.m_resItem != null && req2.GetAmount(1) > 0)
			{
				AddFetch(task, req2.m_resItem.gameObject.name, req2.GetAmount(1) * times);
			}
		}
		return task;
	}

	private void AddFetch(Task task, string item, int amount)
	{
		int inBag = MHR_Items.CountItemInInventory(Bag, item);
		if (amount > inBag)
		{
			task.Fetch.Add(new KeyValuePair<string, int>(item, amount));
		}
	}

	private Task StoreTask()
	{
		Inventory bag = Bag;
		if (bag == null)
		{
			return null;
		}
		foreach (KeyValuePair<string, int> item in _ledger)
		{
			if (item.Value <= 0 || MHR_Items.CountItemInInventory(bag, item.Key) <= 0)
			{
				continue;
			}
			Container container = StoreChest(item.Key);
			if (container != null)
			{
				return new Task
				{
					Kind = TaskKind.Store,
					Item = item.Key
				};
			}
		}
		return null;
	}

	// ---------------------------------------------------------------- running a task

	private void Begin(Task task)
	{
		_task = task;
		_fetchIndex = 0;
		_chest = null;
		_approachKey = null;
		if (_ai != null)
		{
			_ai.SetFollowTarget(null);
		}
		if (task.Fermenter != null)
		{
			Claim(task.Fermenter);
		}
		Log("Going to " + task.Describe());
		ShowDoing(task);
		Say(task.Kind);
		if (task.Kind == TaskKind.Pile)
		{
			_pileTaker = new MHR_PileTaker(task.Pile, "Cook");
			Enter(CookPhase.TakingPile);
		}
		else if (task.Kind == TaskKind.Store)
		{
			_chest = StoreChest(task.Item);
			Enter(CookPhase.Storing);
		}
		else if (task.Fetch.Count > 0)
		{
			Enter(CookPhase.Fetching);
		}
		else
		{
			Enter(CookPhase.MovingToStation);
		}
	}

	private void Enter(CookPhase phase)
	{
		_phase = phase;
		_phaseStart = Time.time;
		_timer = 0f;
		_approachKey = null;
		ResetStuck();
	}

	private void UpdateFetching(float dt)
	{
		Inventory bag = Bag;
		if (bag == null)
		{
			Abort("no inventory");
			return;
		}
		while (_fetchIndex < _task.Fetch.Count && MHR_Items.CountItemInInventory(bag, _task.Fetch[_fetchIndex].Key) >= _task.Fetch[_fetchIndex].Value)
		{
			_fetchIndex++;
			_chest = null;
		}
		if (_fetchIndex >= _task.Fetch.Count)
		{
			CloseChest();
			Enter(CookPhase.MovingToStation);
			return;
		}
		string item = _task.Fetch[_fetchIndex].Key;
		int need = _task.Fetch[_fetchIndex].Value - MHR_Items.CountItemInInventory(bag, item);
		if (_chest == null)
		{
			_chest = ChestWith(item);
			if (_chest == null)
			{
				Abort("no chest in reach has " + Name(item));
				return;
			}
		}
		if (!AtChest(_chest, dt, "chest with " + Name(item)))
		{
			return;
		}
		if (!OpenChest(_chest, dt))
		{
			return;
		}
		Inventory inv = _chest.GetInventory();
		int taken = 0;
		while (taken < need)
		{
			ItemDrop.ItemData itemData = MHR_Items.FindItemByPrefab(inv, item);
			if (itemData == null || !MHR_Items.TransferOne(inv, bag, itemData))
			{
				break;
			}
			taken++;
		}
		Log($"Took {taken}x {Name(item)}");
		CloseChest();
		if (taken == 0)
		{
			// someone emptied it on the way: try another chest
			_unreachable.Add(_chest.GetInstanceID());
		}
		_chest = null;
		_approachKey = null;
	}

	private void UpdateMovingToStation(float dt)
	{
		Component target = TaskTarget();
		if (target == null)
		{
			Abort("station gone");
			return;
		}
		Vector3 point = TaskPoint();
		if (!AtPoint(target, point, dt, Loc(TaskName())))
		{
			return;
		}
		_ai?.StopMoving();
		_ai?.LookAtPoint(target.transform.position);
		_cookArea = target.transform.position;
		_hasCookArea = true;
		switch (_task.Kind)
		{
		case TaskKind.Craft:
			if (!StationReady(_task.Station, out var why))
			{
				_stationProblemUntil[_task.Station.GetInstanceID()] = Time.time + 60f;
				Abort("the " + Loc(_task.Station.m_name) + " " + why);
				return;
			}
			_task.Station.PokeInUse();
			_zanim?.SetTrigger("interact");
			_task.Station.m_craftItemEffects?.Create(_task.Station.transform.position, Quaternion.identity);
			Enter(CookPhase.Crafting);
			break;
		case TaskKind.Grill:
			AddToCooker();
			Enter(CookPhase.Cooking);
			break;
		case TaskKind.Rescue:
			Enter(CookPhase.Cooking);
			break;
		case TaskKind.Stoke:
			Stoke();
			Finish();
			break;
		case TaskKind.Fill:
		case TaskKind.Tap:
			_zanim?.SetTrigger("interact");
			Enter(CookPhase.Fermenting);
			break;
		case TaskKind.Hive:
		{
			_zanim?.SetTrigger("interact");
			int honey = HoneyIn(_task.Hive);
			_honeyHave = CountIn(Bag, (_task.Hive.m_honeyItem != null) ? _task.Hive.m_honeyItem.gameObject.name : "Honey");
			_honeySkip.Clear();
			_honeyTarget = null;
			if (honey > 0)
			{
				// 1.4.152: honey already lying there is someone else's
				_honeySkip.UnionWith(MHR_Chests.DropsNear(_task.Hive.m_spawnPoint != null ? _task.Hive.m_spawnPoint.position : _task.Hive.transform.position, 4f));
				_task.Hive.GetComponent<ZNetView>()?.InvokeRPC("RPC_Extract");
				Log($"Collected {honey}x {Name("Honey")} from the beehive");
			}
			Enter(CookPhase.CollectingDrops);
			break;
		}
		default:
			Finish();
			break;
		}
	}

	private void UpdateCrafting(float dt)
	{
		CraftingStation station = _task.Station;
		if (station == null)
		{
			Abort("station gone");
			return;
		}
		_ai?.StopMoving();
		_ai?.LookAtPoint(station.transform.position);
		station.PokeInUse();
		_timer += dt;
		if (_timer < 2.5f)
		{
			return;
		}
		Inventory bag = Bag;
		Recipe recipe = _task.Recipe;
		foreach (Piece.Requirement req in recipe.m_resources)
		{
			if (req?.m_resItem != null && MHR_Items.CountItemInInventory(bag, req.m_resItem.gameObject.name) < req.GetAmount(1))
			{
				Abort("missing " + Name(req.m_resItem.gameObject.name));
				return;
			}
		}
		foreach (Piece.Requirement req2 in recipe.m_resources)
		{
			if (req2?.m_resItem != null)
			{
				for (int i = 0; i < req2.GetAmount(1); i++)
				{
					MHR_Items.ConsumeOneFromInventory(bag, req2.m_resItem.gameObject.name);
				}
			}
		}
		string output = recipe.m_item.gameObject.name;
		int amount = Mathf.Max(1, recipe.m_amount);
		if (bag.AddItem(output, amount, 1, 0, 0L, "", cheated: false) == null)
		{
			GameObject prefab = ObjectDB.instance?.GetItemPrefab(output);
			if (prefab != null)
			{
				ItemDrop drop = UnityEngine.Object.Instantiate(prefab, base.transform.position + Vector3.up * 0.5f, Quaternion.identity).GetComponent<ItemDrop>();
				if (drop != null)
				{
					drop.m_itemData.m_stack = amount;
				}
			}
			Log("Bag full - dropped " + Name(output));
		}
		AddLedger(output, amount);
		station.m_craftItemDoneEffects?.Create(station.transform.position, Quaternion.identity);
		Log($"Made {amount}x {Name(output)} at the {Loc(station.m_name)}");
		// the rest of the batch
		_task.Count--;
		if (_task.Count > 0 && CanCraftAgain(recipe))
		{
			_timer = 0.5f;
			_zanim?.SetTrigger("interact");
			station.m_craftItemEffects?.Create(station.transform.position, Quaternion.identity);
			return;
		}
		// a fresh mead base goes straight into an empty fermenter, if one wants it
		if (CookCatalog.IsMeadBase(output))
		{
			Task task = FillTask();
			if (task != null && task.Item == output)
			{
				Begin(task);
				return;
			}
		}
		Finish();
	}

	private bool CanCraftAgain(Recipe recipe)
	{
		foreach (Piece.Requirement req in recipe.m_resources)
		{
			if (req?.m_resItem != null && MHR_Items.CountItemInInventory(Bag, req.m_resItem.gameObject.name) < req.GetAmount(1))
			{
				return false;
			}
		}
		return true;
	}

	private void AddToCooker()
	{
		CookingStation cooker = _task.Cooker;
		Inventory bag = Bag;
		ZNetView nv = cooker.GetComponent<ZNetView>();
		if (bag == null || nv == null || !nv.IsValid())
		{
			return;
		}
		if (cooker.m_useFuel && cooker.m_fuelItem != null)
		{
			string fuel = cooker.m_fuelItem.gameObject.name;
			int added = 0;
			while (Fuel(cooker) + added < FuelTarget(cooker.m_maxFuel) && MHR_Items.ConsumeOneFromInventory(bag, fuel))
			{
				nv.InvokeRPC("RPC_AddFuel");
				added++;
			}
			if (added > 0)
			{
				Log($"Added {added}x {Name(fuel)} to the {Loc(cooker.m_name)}");
			}
		}
		int put = 0;
		int free = CountSlots(cooker, -1);
		while (put < _task.Count && put < free && MHR_Items.ConsumeOneFromInventory(bag, _task.Item))
		{
			nv.InvokeRPC("RPC_AddItem", _task.Item, false);
			put++;
		}
		_zanim?.SetTrigger("interact");
		Log($"Put {put}x {Name(_task.Item)} on the {Loc(cooker.m_name)}");
	}

	// stand by the station, take each item off when it's done (before it burns), pick it up
	private void UpdateCooking(float dt)
	{
		CookingStation cooker = _task.Cooker;
		if (cooker == null)
		{
			Abort("cooking station gone");
			return;
		}
		ZNetView nv = cooker.GetComponent<ZNetView>();
		_ai?.StopMoving();
		_ai?.LookAtPoint(cooker.transform.position);
		_timer -= dt;
		if (_timer > 0f)
		{
			return;
		}
		_timer = 0.5f;
		PickUpDrops(DropPoint(cooker), 4f, ledger: true);
		if (CountSlots(cooker, 1) + CountSlots(cooker, 2) > 0)
		{
			nv?.InvokeRPC("RPC_RemoveDoneItem", base.transform.position, 1);
			_zanim?.SetTrigger("interact");
			_timer = 0.8f;
			return;
		}
		bool waiting = _task.Kind == TaskKind.Grill && CountCooking(cooker, _task.Item) > 0;
		if (waiting && Time.time - _phaseStart < 240f)
		{
			if (Time.time >= _nextWatchLog)
			{
				_nextWatchLog = Time.time + 20f;
				Log("Waiting for the " + Loc(cooker.m_name) + " (" + CountCooking(cooker, _task.Item) + " cooking)");
			}
			return;
		}
		PickUpDrops(DropPoint(cooker), 4f, ledger: true);
		Finish();
	}

	private void UpdateFermenting(float dt)
	{
		Fermenter fermenter = _task.Fermenter;
		if (fermenter == null)
		{
			Abort("fermenter gone");
			return;
		}
		_ai?.StopMoving();
		_ai?.LookAtPoint(fermenter.transform.position);
		_timer += dt;
		if (_timer < 0.6f)
		{
			return;
		}
		ZNetView nv = fermenter.GetComponent<ZNetView>();
		if (_task.Kind == TaskKind.Fill)
		{
			if (FermenterState(fermenter) == 0 && MHR_Items.ConsumeOneFromInventory(Bag, _task.Item))
			{
				// 1.4.91: the game's fermenter takes the item's hash and a cheated flag (sending the name did nothing -
				// user: "It never succeeded. The fermenter is still empty.")
				nv?.InvokeRPC("RPC_AddItem", _task.Item.GetStableHashCode(), false);
				TakeFromLedger(_task.Item, 1);
				Log("Put " + Name(_task.Item) + " in the fermenter");
			}
			Finish();
			return;
		}
		if (FermenterState(fermenter) == 2)
		{
			nv?.InvokeRPC("RPC_Tap");
			Log("Tapped the fermenter");
		}
		Enter(CookPhase.CollectingDrops);
	}

	private void UpdateCollecting(float dt)
	{
		_timer += dt;
		if (_timer < 2f)
		{
			return;
		}
		Fermenter fermenter = _task.Fermenter;
		Vector3 center = (fermenter != null && fermenter.m_outputPoint != null) ? fermenter.m_outputPoint.position : base.transform.position;
		if (_task.Hive != null)
		{
			UpdateHoneyPickup(dt);
			return;
		}
		_hiveDrop = null;
		PickUpDrops(center, 4f, ledger: true);
		Finish();
	}

	// 1.4.95: honey rolls off a hive on a slope (user: "sometimes the honey is more behind the hive"). Walk to each
	// honey on the ground near the hive and pick it up like a player, instead of only what lands within 4 m.
	private ItemDrop _honeyTarget;

	private float _honeyTargetSince;

	private readonly HashSet<int> _honeySkip = new HashSet<int>();

	private void UpdateHoneyPickup(float dt)
	{
		Beehive hive = _task.Hive;
		Vector3 center = (hive.m_spawnPoint != null) ? hive.m_spawnPoint.position : hive.transform.position;
		_hiveDrop = (hive.m_honeyItem != null) ? hive.m_honeyItem.gameObject.name : "Honey";
		PickUpDrops(base.transform.position + Vector3.up * 0.5f, 1.8f, ledger: false);
		// 1.4.97: whatever honey ends up in the bag is work honey, however it got there (the companion's auto pickup
		// grabs it as it walks up) - so it's put away and not eaten (1.4.96 log: ate one of the honeys it collected)
		int have = CountIn(Bag, _hiveDrop);
		if (have > _honeyHave)
		{
			AddLedger(_hiveDrop, have - _honeyHave);
			Log($"Picked up {have - _honeyHave}x {Name(_hiveDrop)}");
		}
		_honeyHave = have;
		if (_timer > 30f)
		{
			Log("Leaving the rest of the honey - took too long to get to it");
			EndHoney();
			return;
		}
		if (_honeyTarget != null)
		{
			ZNetView nv = _honeyTarget.GetComponent<ZNetView>();
			if (nv == null || !nv.IsValid())
			{
				_honeyTarget = null;
			}
			else if (Time.time - _honeyTargetSince > 10f || (_ai != null && MHR_Mover.GaveUp(_ai)))
			{
				Log($"Can't get to the honey at {_honeyTarget.transform.position:F1} - leaving it");
				_honeySkip.Add(_honeyTarget.GetInstanceID());
				_honeyTarget = null;
				if (_ai != null)
				{
					MHR_Mover.Release(_ai);
				}
			}
		}
		if (_honeyTarget == null)
		{
			_honeyTarget = NearestHoney(center, 10f);
			_honeyTargetSince = Time.time;
			if (_honeyTarget == null)
			{
				EndHoney();
				return;
			}
		}
		MHR_Nav.Move(_ai, dt, _honeyTarget.transform.position, 0.6f, run: false, "Cook");
	}

	private int _honeyHave;

	private static int CountIn(Inventory inv, string prefab)
	{
		int n = 0;
		if (inv != null && prefab != null)
		{
			foreach (ItemDrop.ItemData item in inv.GetAllItems())
			{
				if (item?.m_dropPrefab != null && item.m_dropPrefab.name == prefab)
				{
					n += item.m_stack;
				}
			}
		}
		return n;
	}

	// 1.4.97: food the cook made or collected for the kitchen (its ledger) isn't eaten; only copies beyond that are
	internal bool IsWorkFood(ItemDrop.ItemData item)
	{
		string name = item?.m_dropPrefab?.name;
		if (name == null)
		{
			return false;
		}
		if (!_ledgerLoaded)
		{
			LoadLedger();
		}
		int ledger = LedgerOf(name);
		return ledger > 0 && CountIn(Bag, name) <= ledger;
	}

	private void EndHoney()
	{
		_hiveDrop = null;
		_honeyTarget = null;
		_honeySkip.Clear();
		_ai?.StopMoving();
		Finish();
	}

	private ItemDrop NearestHoney(Vector3 center, float radius)
	{
		if (s_itemMask < 0)
		{
			s_itemMask = LayerMask.GetMask("item");
		}
		Inventory bag = Bag;
		ItemDrop best = null;
		float bestDist = float.MaxValue;
		int num = Physics.OverlapSphereNonAlloc(center, radius, s_drops, s_itemMask);
		for (int i = 0; i < num; i++)
		{
			ItemDrop drop = s_drops[i]?.GetComponentInParent<ItemDrop>();
			ZNetView nv = drop?.GetComponent<ZNetView>();
			if (drop == null || drop.m_itemData == null || nv == null || !nv.IsValid() || _honeySkip.Contains(drop.GetInstanceID()))
			{
				continue;
			}
			string text = drop.m_itemData.m_dropPrefab?.name ?? Utils.GetPrefabName(drop.gameObject);
			if (text != _hiveDrop || bag == null || !bag.CanAddItem(drop.m_itemData, drop.m_itemData.m_stack))
			{
				continue;
			}
			float d = Vector3.Distance(base.transform.position, drop.transform.position);
			if (d < bestDist)
			{
				bestDist = d;
				best = drop;
			}
		}
		return best;
	}

	private void UpdateStoring(float dt)
	{
		Inventory bag = Bag;
		if (_chest == null || bag == null)
		{
			Finish();
			return;
		}
		if (!AtChest(_chest, dt, "chest to store " + Name(_task.Item)))
		{
			return;
		}
		if (!OpenChest(_chest, dt))
		{
			return;
		}
		Inventory inv = _chest.GetInventory();
		int stored = 0;
		// everything it made that fits here, starting with the item it came for
		List<string> items = new List<string> { _task.Item };
		foreach (string key in _ledger.Keys)
		{
			if (!items.Contains(key) && (HasItem(inv, key) || key == _task.Item))
			{
				items.Add(key);
			}
		}
		foreach (string item in items)
		{
			int want = Mathf.Min(LedgerOf(item), MHR_Items.CountItemInInventory(bag, item));
			int moved = 0;
			while (moved < want)
			{
				ItemDrop.ItemData itemData = MHR_Items.FindItemByPrefab(bag, item);
				if (itemData == null || !inv.CanAddItem(itemData, 1) || !MHR_Items.TransferOne(bag, inv, itemData))
				{
					break;
				}
				moved++;
			}
			if (moved > 0)
			{
				TakeFromLedger(item, moved);
				stored += moved;
				Log($"Put away {moved}x {Name(item)}");
			}
		}
		CloseChest();
		if (stored == 0)
		{
			// nothing fitted: don't come back to this chest for a while
			_unreachable.Add(_chest.GetInstanceID());
		}
		_chest = null;
		Finish();
	}

	// ---------------------------------------------------------------- moving

	private Component TaskTarget()
	{
		switch (_task.Kind)
		{
		case TaskKind.Craft:
			return _task.Station;
		case TaskKind.Grill:
		case TaskKind.Rescue:
			return _task.Cooker;
		case TaskKind.Fill:
		case TaskKind.Tap:
			return _task.Fermenter;
		case TaskKind.Hive:
			return _task.Hive;
		case TaskKind.Stoke:
			return (_task.Fire != null) ? (Component)_task.Fire : _task.Cooker;
		default:
			return null;
		}
	}

	private string TaskName()
	{
		Component component = TaskTarget();
		if (component is CraftingStation craftingStation)
		{
			return craftingStation.m_name;
		}
		if (component is CookingStation cookingStation)
		{
			return cookingStation.m_name;
		}
		if (component is Beehive beehive)
		{
			return beehive.m_name;
		}
		if (component is Fireplace fireplace)
		{
			return fireplace.m_name;
		}
		return "$piece_fermenter";
	}

	// where to stand: by the cooking station's food slot, by the fermenter's tap, by the crafting station
	private Vector3 TaskPoint()
	{
		if (_task.Cooker != null && (_task.Kind == TaskKind.Grill || _task.Kind == TaskKind.Rescue))
		{
			return (_task.Cooker.m_addFoodSwitch != null) ? _task.Cooker.m_addFoodSwitch.transform.position : _task.Cooker.transform.position;
		}
		if (_task.Kind == TaskKind.Stoke && _task.Cooker != null && _task.Cooker.m_addFuelSwitch != null)
		{
			return _task.Cooker.m_addFuelSwitch.transform.position;
		}
		if (_task.Fermenter != null && (_task.Kind == TaskKind.Fill || _task.Kind == TaskKind.Tap))
		{
			return (_task.Fermenter.m_outputPoint != null) ? _task.Fermenter.m_outputPoint.position : _task.Fermenter.transform.position;
		}
		return TaskTarget().transform.position;
	}

	private bool AtPoint(Component target, Vector3 point, float dt, string what)
	{
		string text = "p" + target.GetInstanceID();
		if (_approachKey != text)
		{
			_approachKey = text;
			_hasApproach = MHR_Chests.PickApproachPoint(base.transform.position, point, target, out _approachSpot);
		}
		Vector3 position = base.transform.position;
		float num = MHR_Nav.ReachDist(position, point);
		if (num < 2f || (_hasApproach && OnSpot(num)))
		{
			return true;
		}
		Vector3 goal = _hasApproach ? _approachSpot : point;
		MHR_Nav.Move(_ai, dt, goal, _hasApproach ? 0.5f : 2f, FarEnough(num), "Cook");
		CheckStuck(dt, num, what);
		return false;
	}

	private bool AtChest(Container chest, float dt, string what)
	{
		string text = "c" + chest.GetInstanceID();
		if (_approachKey != text)
		{
			_approachKey = text;
			_hasApproach = MHR_Chests.PickApproach(base.transform.position, chest, out _approachSpot);
			if (!_hasApproach && !MHR_Chests.CanReach(base.transform.position, chest))
			{
				_unreachable.Add(chest.GetInstanceID());
				Log($"Skipping chest at {chest.transform.position:F1} - can't get within reach of it");
				Abort("chest out of reach");
				return false;
			}
		}
		float num = MHR_Nav.ReachDist(base.transform.position, chest.transform.position);
		if (num < UseReach || (_hasApproach && OnSpot(num)))
		{
			_ai?.StopMoving();
			return true;
		}
		Vector3 goal = _hasApproach ? _approachSpot : chest.transform.position;
		MHR_Nav.Move(_ai, dt, goal, _hasApproach ? 0.5f : UseReach, FarEnough(num), "Cook");
		CheckStuck(dt, num, what);
		return false;
	}

	private bool OnSpot(float dist)
	{
		Vector3 position = base.transform.position;
		return dist <= UseReach + 1.5f && new Vector2(position.x - _approachSpot.x, position.z - _approachSpot.z).magnitude <= 0.7f && Mathf.Abs(position.y - _approachSpot.y) <= 1.5f;   // 1.4.135: as the movement's "arrived"
	}

	private static bool FarEnough(float dist)
	{
		float num = ModConfig.WorkRunDistance?.Value ?? 25f;
		return num > 0f && dist > num;
	}

	private void ResetStuck()
	{
		_stuckTimer = 0f;
	}

	// 1.4.134 (movement phase 4): only the movement decides it's stuck; two minutes per leg at most
	private string _stuckLeg;

	private void CheckStuck(float dt, float dist, string what)
	{
		if (what != _stuckLeg)
		{
			_stuckLeg = what;
			_stuckTimer = 0f;
		}
		_stuckTimer += dt;
		if ((_ai != null && MHR_Mover.GaveUp(_ai)) || _stuckTimer > MHR_Nav.LegTimeout)
		{
			Abort("couldn't get to the " + what);
		}
	}

	// ---------------------------------------------------------------- chests

	private bool OpenChest(Container chest, float dt)
	{
		// 1.4.150: someone has this chest open - leave it (the task is dropped and planned again later)
		if (!_chestOpen && !MHR_Chests.Open(chest))
		{
			Abort("someone has the chest open");
			return false;
		}
		if (!_chestOpen)
		{
			_chestOpen = true;
			_zanim?.SetTrigger("interact");
			_ai?.LookAtPoint(chest.transform.position);
			_timer = 0.8f;
			return false;
		}
		_timer -= dt;
		return _timer <= 0f;
	}

	private void CloseChest()
	{
		if (_chestOpen && _chest != null)
		{
			_chest.SetInUse(inUse: false);
		}
		_chestOpen = false;
	}

	private Container ChestWith(string item)
	{
		// nearest first, skipping any it can't get to (checked once, remembered for a minute)
		List<Container> list = new List<Container>();
		foreach (Container chest in _chests)
		{
			if (chest != null && !chest.IsInUse() && !_unreachable.Contains(chest.GetInstanceID()) && HasItem(chest.GetInventory(), item))
			{
				list.Add(chest);
			}
		}
		Vector3 position = base.transform.position;
		list.Sort((Container a, Container b) => Vector3.Distance(position, a.transform.position).CompareTo(Vector3.Distance(position, b.transform.position)));
		foreach (Container chest2 in list)
		{
			if (MHR_Chests.CanReach(position, chest2))
			{
				return chest2;
			}
			_unreachable.Add(chest2.GetInstanceID());
			Log($"Skipping chest at {chest2.transform.position:F1} - can't get within reach of it");
		}
		return null;
	}

	// like emptying bags: a chest that already has this food; else one with other cooked food; else the one nearest the
	// cooking area (Cooking.StoreWithCookedFood off: the nearest chest with room)
	private Container StoreChest(string item)
	{
		GameObject prefab = ObjectDB.instance?.GetItemPrefab(item);
		ItemDrop.ItemData sample = prefab?.GetComponent<ItemDrop>()?.m_itemData;
		if (sample == null)
		{
			return null;
		}
		bool smart = ModConfig.CookStoreWithCookedFood?.Value ?? true;
		Vector3 near = _hasCookArea ? _cookArea : base.transform.position;
		Container same = null;
		Container cooked = null;
		Container nearest = null;
		float d1 = float.MaxValue;
		float d2 = float.MaxValue;
		float d3 = float.MaxValue;
		foreach (Container chest in _chests)
		{
			if (chest == null || chest.IsInUse() || _unreachable.Contains(chest.GetInstanceID()))
			{
				continue;
			}
			Inventory inv = chest.GetInventory();
			if (inv == null || !inv.CanAddItem(sample, 1))
			{
				continue;
			}
			float num = Vector3.Distance(near, chest.transform.position);
			if (smart && HasItem(inv, item) && num < d1)
			{
				same = chest;
				d1 = num;
			}
			else if (smart && HasCookedFood(inv) && num < d2)
			{
				cooked = chest;
				d2 = num;
			}
			else if (inv.GetEmptySlots() > 0 && num < d3)
			{
				nearest = chest;
				d3 = num;
			}
		}
		return same ?? cooked ?? nearest;
	}

	private static bool HasItem(Inventory inv, string item)
	{
		return inv != null && MHR_Items.CountItemInInventory(inv, item) > 0;
	}

	private static bool HasCookedFood(Inventory inv)
	{
		foreach (ItemDrop.ItemData item in inv.GetAllItems())
		{
			string text = item?.m_dropPrefab?.name;
			if (text != null && CookCatalog.IsMade(text))
			{
				return true;
			}
		}
		return false;
	}

	// ---------------------------------------------------------------- counting

	private Inventory Bag => _humanoid?.GetInventory();

	// in the chests in range and the bag
	private int Count(string item)
	{
		int num = MHR_Items.CountItemInInventory(Bag, item);
		foreach (Container chest in _chests)
		{
			// 1.4.88: only chests it can actually get things from (1.4.88 log: Black Soup tried 13 times for Bloodbag
			// in a chest it can't reach)
			if (chest != null && !_unreachable.Contains(chest.GetInstanceID()))
			{
				num += MHR_Items.CountItemInInventory(chest.GetInventory(), item);
			}
		}
		return num;
	}

	// how many of an ingredient may be used: raw ones keep Cooking.IngredientReserve back
	private int Usable(string item)
	{
		int num = Count(item);
		return CookCatalog.IsMade(item) ? num : (num - Reserve);
	}

	private bool IngredientsOk(Recipe recipe)
	{
		foreach (Piece.Requirement req in recipe.m_resources)
		{
			if (req?.m_resItem != null && req.GetAmount(1) > 0 && Usable(req.m_resItem.gameObject.name) < req.GetAmount(1))
			{
				return false;
			}
		}
		return true;
	}

	private static bool Known(Recipe recipe)
	{
		Player localPlayer = Player.m_localPlayer;
		if (localPlayer == null)
		{
			return true;
		}
		string text = recipe.m_item?.m_itemData?.m_shared?.m_name;
		return text != null && localPlayer.IsRecipeKnown(text);
	}

	// ---------------------------------------------------------------- stations

	private void Scan()
	{
		if (Time.time >= _unreachableClearAt)
		{
			_unreachable.Clear();
			_unreachableClearAt = Time.time + 60f;
		}
		_chests.Clear();
		_stations.Clear();
		_cookers.Clear();
		_fermenters.Clear();
		_hives.Clear();
		_pieces.Clear();
		Vector3 position = base.transform.position;
		MHR_Chests.AddHomeChests(_chests, _setup);   // 1.4.152: the home chests too (see MHR_Chests.AddHomeChests)
		Piece.GetAllPiecesInRadius(position, Range, _pieces);
		foreach (Piece piece in _pieces)
		{
			if (piece == null)
			{
				continue;
			}
			Container container = piece.GetComponentInChildren<Container>();
			// 1.4.152: MHR_Chests.Usable - no carts or ships, ward access, private chests only its own player's, not in use
			if (container != null && container.gameObject != base.gameObject && !_chests.Contains(container) && MHR_Chests.Usable(container, this))
			{
				_chests.Add(container);
			}
			if (!ApplianceOn(piece))
			{
				continue;
			}
			CookingStation cookingStation = piece.GetComponent<CookingStation>();
			if (cookingStation != null && cookingStation.GetComponent<ZNetView>()?.GetZDO() != null)
			{
				_cookers.Add(cookingStation);
			}
			Fermenter fermenter = piece.GetComponent<Fermenter>();
			if (fermenter != null && fermenter.GetComponent<ZNetView>()?.GetZDO() != null)
			{
				_fermenters.Add(fermenter);
			}

			CraftingStation craftingStation = piece.GetComponent<CraftingStation>();
			if (craftingStation != null && CookCatalog.IsCookingStation(craftingStation.m_name))
			{
				_stations.Add(craftingStation);
			}
		}
		// 1.4.93: beehives are often out in the yard, away from the kitchen (user): anywhere within HomeTripRange of
		// home while he's near home; at a camp, within the cooking range around him
		Vector3 center = position;
		float radius = Range;
		if (_setup != null && _setup.HasHomePosition() && MHR_ToolKeeper.NearHome(_setup))
		{
			center = _setup.GetHomePosition();
			radius = MHR_ToolKeeper.HomeRange();
		}
		_pieces.Clear();
		Piece.GetAllPiecesInRadius(center, radius, _pieces);
		foreach (Piece piece2 in _pieces)
		{
			Beehive beehive = (piece2 != null) ? piece2.GetComponent<Beehive>() : null;
			if (beehive != null && beehive.GetComponent<ZNetView>()?.GetZDO() != null && ApplianceOn(piece2))
			{
				_hives.Add(beehive);
			}
		}
	}

	// 1.4.119: each kind of cooking appliance can be left alone (user: "a toggle for each cooking appliance"), told apart
	// by the game's piece name. Chests aren't appliances and are always on.
	private static bool ApplianceOn(Piece piece)
	{
		if (piece.GetComponent<CookingStation>() == null && piece.GetComponent<Fermenter>() == null && piece.GetComponent<Beehive>() == null && piece.GetComponent<CraftingStation>() == null)
		{
			return true;
		}
		string n = Utils.GetPrefabName(piece.gameObject).ToLowerInvariant();
		ConfigEntry<bool> e;
		if (n.Contains("meadcauldron") || n.Contains("ketill"))
		{
			e = ModConfig.CookMeadKetill;
		}
		else if (n.Contains("cauldron"))
		{
			e = ModConfig.CookCauldron;
		}
		else if (n.Contains("preptable"))
		{
			e = ModConfig.CookPrepTable;
		}
		else if (n.Contains("cookingstation_iron"))
		{
			e = ModConfig.CookIronCookingStation;
		}
		else if (n.Contains("cookingstation"))
		{
			e = ModConfig.CookCookingStation;
		}
		else if (n.Contains("oven"))
		{
			e = ModConfig.CookOven;
		}
		else if (piece.GetComponent<Fermenter>() != null)
		{
			e = ModConfig.CookFermenter;
		}
		else if (piece.GetComponent<Beehive>() != null)
		{
			e = ModConfig.CookBeehives;
		}
		else if (piece.GetComponent<CraftingStation>() != null && piece.GetComponent<CookingStation>() == null && !CookCatalog.IsCookingStation(piece.GetComponent<CraftingStation>().m_name))
		{
			return true;   // a workbench, forge... - not a cooking appliance
		}
		else
		{
			e = ModConfig.CookOtherStations;
		}
		return e == null || e.Value;
	}

	private bool Usable(Component c)
	{
		if (c == null || (_ai != null && _ai.IsPositionBlacklisted(c.transform.position)))
		{
			return false;
		}
		// 1.4.152: fermenters and beehives are ward-protected for players too
		if ((c.GetComponent<Fermenter>() != null || c.GetComponent<Beehive>() != null) && !PrivateArea.CheckAccess(c.transform.position, 0f, flash: false))
		{
			return false;
		}
		float value;
		return !_stationProblemUntil.TryGetValue(c.GetInstanceID(), out value) || Time.time >= value;
	}

	private CraftingStation FindStation(Recipe recipe)
	{
		CraftingStation best = null;
		float bestDist = float.MaxValue;
		foreach (CraftingStation station in _stations)
		{
			if (station == null || recipe.m_craftingStation == null || station.m_name != recipe.m_craftingStation.m_name || station.GetLevel() < recipe.GetRequiredStationLevel(1) || !Usable(station) || !StationReady(station, out var _))
			{
				continue;
			}
			float num = Vector3.Distance(base.transform.position, station.transform.position);
			if (num < bestDist)
			{
				best = station;
				bestDist = num;
			}
		}
		return best;
	}

	// what a player needs to use it: a roof and a fire, where the station asks for them
	private static bool StationReady(CraftingStation station, out string why)
	{
		why = "";
		if (station.m_craftRequireRoof && station.m_roofCheckPoint != null)
		{
			Cover.GetCoverForPoint(station.m_roofCheckPoint.position, out var coverPercentage, out var underRoof);
			if (!underRoof || coverPercentage < 0.7f)
			{
				why = "needs a roof";
				return false;
			}
		}
		if (station.m_craftRequireFire && s_haveFire != null && !(bool)s_haveFire.GetValue(station))
		{
			why = "needs a fire";
			return false;
		}
		return true;
	}

	private CookingStation FindCooker(string from, bool needFree)
	{
		foreach (CookingStation cooker in _cookers)
		{
			if (cooker == null || !Usable(cooker) || (needFree && CountSlots(cooker, -1) == 0))
			{
				continue;
			}
			foreach (CookingStation.ItemConversion conv in cooker.m_conversion)
			{
				if (conv?.m_from != null && conv.m_from.gameObject.name == from)
				{
					return cooker;
				}
			}
		}
		return null;
	}

	private bool HasHeat(CookingStation cooker, bool orFuelInReach)
	{
		if (cooker.m_requireFire && s_isFireLit != null && (bool)s_isFireLit.Invoke(cooker, null))
		{
			return true;
		}
		if (cooker.m_useFuel)
		{
			return Fuel(cooker) >= 1f || (orFuelInReach && cooker.m_fuelItem != null && Count(cooker.m_fuelItem.gameObject.name) > 0);
		}
		return !cooker.m_requireFire;
	}

	private static float Fuel(CookingStation cooker)
	{
		return cooker.GetComponent<ZNetView>()?.GetZDO()?.GetFloat(ZDOVars.s_fuel) ?? 0f;
	}

	// status: -1 = empty slots, 0 = cooking, 1 = done, 2 = burnt
	private static int CountSlots(CookingStation cooker, int status)
	{
		ZDO zDO = cooker.GetComponent<ZNetView>()?.GetZDO();
		if (zDO == null || cooker.m_slots == null)
		{
			return 0;
		}
		int num = 0;
		for (int i = 0; i < cooker.m_slots.Length; i++)
		{
			string text = zDO.GetString("slot" + i);
			if (status < 0)
			{
				if (text == "")
				{
					num++;
				}
			}
			else if (text != "" && zDO.GetInt("slotstatus" + i) == status)
			{
				num++;
			}
		}
		return num;
	}

	private static int CountCooking(CookingStation cooker, string from)
	{
		ZDO zDO = cooker.GetComponent<ZNetView>()?.GetZDO();
		if (zDO == null || cooker.m_slots == null)
		{
			return 0;
		}
		int num = 0;
		for (int i = 0; i < cooker.m_slots.Length; i++)
		{
			if (zDO.GetString("slot" + i) == from && zDO.GetInt("slotstatus" + i) == 0)
			{
				num++;
			}
		}
		return num;
	}

	private static Vector3 DropPoint(CookingStation cooker)
	{
		return (cooker.m_spawnPoint != null) ? cooker.m_spawnPoint.position : cooker.transform.position;
	}

	// 0 = empty, 1 = fermenting, 2 = ready
	private static int FermenterState(Fermenter fermenter)
	{
		ZDO zDO = fermenter.GetComponent<ZNetView>()?.GetZDO();
		// 1.4.91: the content is stored as the item's hash (an int), like Fermenter.GetContent reads it
		if (zDO == null || zDO.GetInt(ZDOVars.s_content) == 0)
		{
			return 0;
		}
		long @long = zDO.GetLong(ZDOVars.s_startTime, 0L);
		if (@long == 0L || ZNet.instance == null)
		{
			return 1;
		}
		return ((ZNet.instance.GetTime() - new DateTime(@long)).TotalSeconds > (double)fermenter.m_fermentationDuration) ? 2 : 1;
	}

	private static bool Accepts(Fermenter fermenter, string item)
	{
		foreach (Fermenter.ItemConversion conv in fermenter.m_conversion)
		{
			if (conv?.m_from != null && conv.m_from.gameObject.name == item)
			{
				return true;
			}
		}
		return false;
	}

	private void Claim(Fermenter f)
	{
		Unclaim();
		_claimedId = f.GetInstanceID();
		s_claimed.Add(_claimedId);
	}

	private void Unclaim()
	{
		if (_claimedId != 0)
		{
			s_claimed.Remove(_claimedId);
			_claimedId = 0;
		}
	}

	// ---------------------------------------------------------------- picking things up

	private void PickUpDrops(Vector3 center, float radius, bool ledger)
	{
		if (s_itemMask < 0)
		{
			s_itemMask = LayerMask.GetMask("item");
		}
		Inventory bag = Bag;
		if (bag == null)
		{
			return;
		}
		int num = Physics.OverlapSphereNonAlloc(center, radius, s_drops, s_itemMask);
		for (int i = 0; i < num; i++)
		{
			ItemDrop drop = s_drops[i]?.GetComponentInParent<ItemDrop>();
			ZNetView nv = drop?.GetComponent<ZNetView>();
			if (drop == null || drop.m_itemData == null || nv == null || nv.GetZDO() == null)
			{
				continue;
			}
			string text = drop.m_itemData.m_dropPrefab?.name ?? Utils.GetPrefabName(drop.gameObject);
			// only what came off the stations: cooked food, mead, coal from burnt food (and honey from a hive)
			if (!CookCatalog.IsMade(text) && !CookCatalog.IsMead(text) && text != "Coal" && text != _hiveDrop)
			{
				continue;
			}
			if (!bag.CanAddItem(drop.m_itemData, drop.m_itemData.m_stack))
			{
				continue;
			}
			// 1.4.152: only a drop this game owns (else ask for it; picked up next time) - no copies in multiplayer
			if (!drop.CanPickup(autoPickupDelay: false))
			{
				drop.RequestOwn();
				continue;
			}
			int stack = drop.m_itemData.m_stack;
			bag.AddItem(drop.m_itemData);
			nv.ClaimOwnership();
			nv.Destroy();
			if (ledger)
			{
				AddLedger(text, stack);
			}
			Log($"Picked up {stack}x {Name(text)}");
		}
	}

	// ---------------------------------------------------------------- what it made (kept on the companion)

	private void LoadLedger()
	{
		_ledgerLoaded = true;
		_ledger.Clear();
		string text = _nview?.GetZDO()?.GetString(LedgerHash, "") ?? "";
		foreach (string part in text.Split(';'))
		{
			int num = part.LastIndexOf(':');
			if (num > 0 && int.TryParse(part.Substring(num + 1), out var result) && result > 0)
			{
				_ledger[part.Substring(0, num)] = result;
			}
		}
	}

	private void SaveLedger()
	{
		StringBuilder stringBuilder = new StringBuilder();
		foreach (KeyValuePair<string, int> item in _ledger)
		{
			if (item.Value > 0)
			{
				stringBuilder.Append(item.Key).Append(':').Append(item.Value)
					.Append(';');
			}
		}
		_nview?.GetZDO()?.Set(LedgerHash, stringBuilder.ToString());
	}

	private void AddLedger(string item, int n)
	{
		_ledger[item] = LedgerOf(item) + n;
		SaveLedger();
	}

	private void TakeFromLedger(string item, int n)
	{
		int num = LedgerOf(item) - n;
		if (num > 0)
		{
			_ledger[item] = num;
		}
		else
		{
			_ledger.Remove(item);
		}
		SaveLedger();
	}

	private int LedgerOf(string item)
	{
		return _ledger.TryGetValue(item, out var value) ? value : 0;
	}

	// what it made and still carries (the ledger can't be more than what's in the bag)
	private int LedgerCarried()
	{
		Inventory bag = Bag;
		if (bag == null)
		{
			return 0;
		}
		int num = 0;
		List<string> list = null;
		foreach (KeyValuePair<string, int> item in _ledger)
		{
			int num2 = MHR_Items.CountItemInInventory(bag, item.Key);
			if (num2 < item.Value)
			{
				if (list == null)
				{
					list = new List<string>();
				}
				list.Add(item.Key);
			}
			num += Mathf.Min(num2, item.Value);
		}
		if (list != null)
		{
			// eaten, given away or dropped: forget what's no longer carried
			foreach (string item2 in list)
			{
				int num3 = MHR_Items.CountItemInInventory(bag, item2);
				if (num3 > 0)
				{
					_ledger[item2] = num3;
				}
				else
				{
					_ledger.Remove(item2);
				}
			}
			SaveLedger();
		}
		return num;
	}

	// ---------------------------------------------------------------- ending a task

	private void Finish()
	{
		DropPileTaker();
		Unclaim();
		CloseChest();
		_task = null;
		_phase = CookPhase.Idle;
		_scanTimer = 1f;
	}

	private void Abort(string reason)
	{
		Log("Stopped - " + reason + " (was " + (_task?.Describe() ?? "idle") + ")");
		if (_task != null && !string.IsNullOrEmpty(_task.Item) && reason != "interrupted" && reason != "mode changed")
		{
			_skipUntil[_task.Kind + ":" + _task.Item] = Time.time + 60f;
		}
		DropPileTaker();
		Unclaim();
		CloseChest();
		_task = null;
		_chest = null;
		_phase = CookPhase.Idle;
		_scanTimer = 5f;
	}

	private void RestoreFollow()
	{
		if (_setup != null && _ai != null && _setup.GetFollow() && MHR_Owner.Of(this) != null && _ai.GetFollowTarget() == null)
		{
			_ai.SetFollowTarget(MHR_Owner.Go(this));
		}
	}

	private void Say(TaskKind kind)
	{
		if (_talk == null)
		{
			return;
		}
		string text;
		switch (kind)
		{
		case TaskKind.Craft:
		case TaskKind.Grill:
			text = "hc_speech_cook_cooking";
			break;
		case TaskKind.Fill:
			text = "hc_speech_cook_fermenter";
			break;
		case TaskKind.Tap:
			text = "hc_speech_cook_tapping";
			break;
		case TaskKind.Store:
			text = "hc_speech_cook_storing";
			break;
		default:
			return;
		}
		_talk.Say(ModLocalization.Loc(text));
	}

	private void Log(string msg)
	{
		CompanionsPlugin.Log.LogInfo("[Cook|" + (_character?.m_name ?? "?") + "] " + msg);
	}

	internal static string Name(string prefab)
	{
		if (string.IsNullOrEmpty(prefab))
		{
			return "?";
		}
		string text = ObjectDB.instance?.GetItemPrefab(prefab)?.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_name;
		return (text != null) ? Loc(text) : prefab;
	}

	internal static string Loc(string token)
	{
		if (string.IsNullOrEmpty(token))
		{
			return "?";
		}
		return Localization.instance?.Localize(token) ?? token;
	}
}

// What can be cooked, from the game's own data (mods included): cauldron / prep table recipes, cooking station and oven
// conversions, fermenter conversions. Built once; also writes OfflineCompanionsReborn.Cooking.txt with the names to use
// in the Cooking settings.
internal static class CookCatalog
{
	internal static readonly List<Recipe> Recipes = new List<Recipe>();

	private static readonly Dictionary<string, string> s_convTo = new Dictionary<string, string>();

	private static readonly Dictionary<string, string> s_meadOf = new Dictionary<string, string>();

	private static readonly HashSet<string> s_meads = new HashSet<string>();

	private static readonly HashSet<string> s_made = new HashSet<string>();

	private static readonly HashSet<string> s_stationNames = new HashSet<string>();

	private static readonly Dictionary<string, Recipe> s_recipeFor = new Dictionary<string, Recipe>();

	private static bool s_built;

	private static readonly HashSet<string> s_allow = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	private static readonly HashSet<string> s_deny = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	private static string s_focusKey;

	private static string s_focusStage;

	private static readonly List<string> s_focus = new List<string>();

	internal static void Ensure()
	{
		if (s_built || ObjectDB.instance == null || ObjectDB.instance.m_recipes == null || ObjectDB.instance.m_recipes.Count == 0)
		{
			return;
		}
		s_built = true;
		try
		{
			foreach (Fermenter fermenter in Resources.FindObjectsOfTypeAll<Fermenter>())
			{
				if (fermenter?.m_conversion == null)
				{
					continue;
				}
				foreach (Fermenter.ItemConversion conv in fermenter.m_conversion)
				{
					if (conv?.m_from != null && conv.m_to != null)
					{
						s_meadOf[conv.m_from.gameObject.name] = conv.m_to.gameObject.name;
						s_meads.Add(conv.m_to.gameObject.name);
					}
				}
			}
			foreach (CookingStation cooker in Resources.FindObjectsOfTypeAll<CookingStation>())
			{
				if (cooker?.m_conversion == null)
				{
					continue;
				}
				foreach (CookingStation.ItemConversion conv2 in cooker.m_conversion)
				{
					if (conv2?.m_from != null && conv2.m_to != null)
					{
						s_convTo[conv2.m_from.gameObject.name] = conv2.m_to.gameObject.name;
						s_made.Add(conv2.m_to.gameObject.name);
					}
				}
			}
			foreach (Recipe recipe in ObjectDB.instance.m_recipes)
			{
				if (recipe == null || !recipe.m_enabled || recipe.m_item == null || recipe.m_craftingStation == null)
				{
					continue;
				}
				string name = recipe.m_item.gameObject.name;
				ItemDrop.ItemData.SharedData shared = recipe.m_item.m_itemData?.m_shared;
				if (shared == null)
				{
					continue;
				}
				bool flag = Value(shared) > 0f || s_meadOf.ContainsKey(name) || (s_convTo.TryGetValue(name, out var value) && Value(value) > 0f);
				if (flag && !s_meads.Contains(name))
				{
					Recipes.Add(recipe);
					s_made.Add(name);
					s_stationNames.Add(recipe.m_craftingStation.m_name);
					if (!s_recipeFor.ContainsKey(name))
					{
						s_recipeFor[name] = recipe;
					}
				}
			}
			WriteReference();
			CompanionsPlugin.Log.LogInfo($"[Cook] {Recipes.Count} recipes, {s_convTo.Count} cooking station items, {s_meadOf.Count} mead bases");
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log.LogWarning("[Cook] couldn't read the cooking recipes: " + ex.Message);
		}
	}

	internal static List<string> MeadBases => new List<string>(s_meadOf.Keys);

	internal static bool IsCookingStation(string stationName)
	{
		return s_stationNames.Contains(stationName);
	}

	internal static bool IsMade(string item)
	{
		return s_made.Contains(item);
	}

	// 1.4.94: something a cooking station (or iron grill) cooks - how Hunt tells meat from other drops
	internal static bool IsGrillInput(string item)
	{
		Ensure();
		return s_convTo.ContainsKey(item);
	}

	internal static bool IsMeadBase(string item)
	{
		return s_meadOf.ContainsKey(item);
	}

	internal static bool IsMead(string item)
	{
		return s_meads.Contains(item);
	}

	internal static string MeadOf(string meadBase)
	{
		return s_meadOf.TryGetValue(meadBase, out var value) ? value : null;
	}

	internal static Recipe RecipeFor(string item)
	{
		return s_recipeFor.TryGetValue(item, out var value) ? value : null;
	}

	// the food a crafted item ends up as: itself, or what a cooking station / oven turns it into
	internal static string FinalFood(string item)
	{
		if (Value(item) > 0f && !IsMeadBase(item))
		{
			return item;
		}
		if (s_convTo.TryGetValue(item, out var value) && Value(value) > 0f)
		{
			return value;
		}
		return null;
	}

	internal static float Value(string item)
	{
		ItemDrop.ItemData.SharedData sharedData = ObjectDB.instance?.GetItemPrefab(item)?.GetComponent<ItemDrop>()?.m_itemData?.m_shared;
		return (sharedData != null) ? Value(sharedData) : 0f;
	}

	private static float Value(ItemDrop.ItemData.SharedData s)
	{
		if (s.m_itemType != ItemDrop.ItemData.ItemType.Consumable)
		{
			return 0f;
		}
		return s.m_food + s.m_foodStamina + s.m_foodEitr;
	}

	// 1.4.106: the shared list format (MHR_List): *, Name, -Name, Name:Number (that food's own FoodTarget)
	internal static bool Allowed(string item)
	{
		return MHR_List.Get(ModConfig.CookFoods?.Value ?? "*").Allows(item, CookController.Name(item));
	}

	internal static int Target(string item, int dflt)
	{
		MHR_List l = MHR_List.Get(ModConfig.CookFoods?.Value ?? "*");
		int n = l.AmountFor(item, -1);
		if (n < 0)
		{
			n = l.AmountFor(CookController.Name(item), dflt);
		}
		return (n <= 0) ? int.MaxValue / 4 : n;
	}

	// the mead bases to work on first: the MeadFocus entry for the last boss beaten. 1.4.92: the entries are in the
	// order the player beats the bosses; a boss is named by (part of) its name or its defeated key, so new and modded
	// bosses work too (bosses are read from the game's own data, see Bosses)
	internal static List<string> Focus()
	{
		string text = ModConfig.CookMeadFocus?.Value ?? "";
		List<KeyValuePair<string, string>> list = new List<KeyValuePair<string, string>>();
		foreach (string part in text.Split(';'))
		{
			int num = part.IndexOf(':');
			if (num > 0)
			{
				list.Add(new KeyValuePair<string, string>(part.Substring(0, num).Trim(), part.Substring(num + 1)));
			}
		}
		// the last entry (in the player's order) whose boss is beaten; "start" when none is
		string stage = null;
		string value = null;
		foreach (KeyValuePair<string, string> item in list)
		{
			if (item.Key.Equals("start", StringComparison.OrdinalIgnoreCase))
			{
				if (stage == null)
				{
					stage = item.Key;
					value = item.Value;
				}
			}
			else if (Beaten(item.Key))
			{
				stage = item.Key;
				value = item.Value;
			}
		}
		if (text == s_focusKey && stage == s_focusStage)
		{
			return s_focus;
		}
		s_focusKey = text;
		s_focusStage = stage;
		s_focus.Clear();
		if (value != null)
		{
			foreach (string part2 in value.Split(','))
			{
				string text2 = part2.Trim();
				if (text2.Length > 0)
				{
					if (IsMeadBase(text2))
					{
						s_focus.Add(text2);
					}
					else
					{
						CompanionsPlugin.Log.LogWarning("[Cook] MeadFocus: \"" + text2 + "\" isn't a mead base (see OfflineCompanionsReborn.Cooking.txt)");
					}
				}
			}
		}
		CompanionsPlugin.Log.LogInfo("[Cook] Mead priority (" + (stage ?? "no entry") + "): " + ((s_focus.Count > 0) ? string.Join(", ", s_focus) : "none"));
		return s_focus;
	}

	// bosses from the game's data (mods included): the key their death sets, prefab and display name
	private class Boss
	{
		public string Key;

		public string Prefab;

		public string Name;
	}

	private static List<Boss> s_bosses;

	private static List<Boss> Bosses()
	{
		if (s_bosses != null)
		{
			return s_bosses;
		}
		List<Boss> list = new List<Boss>();
		try
		{
			foreach (Character character in Resources.FindObjectsOfTypeAll<Character>())
			{
				if (character == null || !character.m_boss || string.IsNullOrEmpty(character.m_defeatSetGlobalKey) || list.Exists((Boss b) => b.Key == character.m_defeatSetGlobalKey))
				{
					continue;
				}
				list.Add(new Boss
				{
					Key = character.m_defeatSetGlobalKey,
					Prefab = character.gameObject.name,
					Name = CookController.Loc(character.m_name)
				});
			}
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log.LogWarning("[Cook] couldn't read the bosses: " + ex.Message);
		}
		if (list.Count > 0)
		{
			s_bosses = list;
		}
		return list;
	}

	private static string Norm(string s)
	{
		StringBuilder stringBuilder = new StringBuilder();
		foreach (char c in s ?? "")
		{
			if (char.IsLetterOrDigit(c))
			{
				stringBuilder.Append(char.ToLowerInvariant(c));
			}
		}
		return stringBuilder.ToString();
	}

	// is the boss this MeadFocus entry names beaten (world key, or the player's own key on "player-based" worlds)?
	private static bool Beaten(string label)
	{
		string text = Norm(label);
		if (text.Length == 0)
		{
			return false;
		}
		List<string> keys = new List<string>();
		foreach (Boss boss in Bosses())
		{
			string text2 = Norm(boss.Key);
			if (text2 == text || text2 == "defeated" + text || text2.EndsWith(text) || Norm(boss.Name).Contains(text) || Norm(boss.Prefab).Contains(text))
			{
				keys.Add(boss.Key);
			}
		}
		if (keys.Count == 0)
		{
			keys.Add("defeated_" + label.Trim().ToLowerInvariant());
		}
		foreach (string key in keys)
		{
			if ((ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(key)) || (Player.m_localPlayer != null && Player.m_localPlayer.HaveUniqueKey(key)))
			{
				return true;
			}
		}
		return false;
	}

	private static void WriteReference()
	{
		try
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine(CompanionsPlugin.PluginName + " - names for the [Cooking] settings. Written at every start from the game's own data, mods included.");
			stringBuilder.AppendLine();
			stringBuilder.AppendLine("FOODS (Cooking > Foods)  name = in-game name  (health + stamina + eitr, where it's made)");
			SortedDictionary<string, string> sortedDictionary = new SortedDictionary<string, string>();
			foreach (Recipe recipe in Recipes)
			{
				string name = recipe.m_item.gameObject.name;
				if (!IsMeadBase(name) && Value(name) > 0f)
				{
					sortedDictionary[name] = $"{name} = {CookController.Name(name)}  ({Value(name):F0}, {CookController.Loc(recipe.m_craftingStation.m_name)})";
				}
			}
			foreach (KeyValuePair<string, string> item in s_convTo)
			{
				if (Value(item.Value) > 0f)
				{
					sortedDictionary[item.Value] = $"{item.Value} = {CookController.Name(item.Value)}  ({Value(item.Value):F0}, from {CookController.Name(item.Key)} on a cooking station / oven)";
				}
			}
			foreach (string value in sortedDictionary.Values)
			{
				stringBuilder.AppendLine("  " + value);
			}
			stringBuilder.AppendLine();
			stringBuilder.AppendLine("MEAD BASES (Cooking > MeadFocus)  name = in-game name -> mead");
			foreach (KeyValuePair<string, string> item2 in s_meadOf)
			{
				stringBuilder.AppendLine($"  {item2.Key} = {CookController.Name(item2.Key)} -> {CookController.Name(item2.Value)}");
			}
			stringBuilder.AppendLine();
			stringBuilder.AppendLine("BOSSES (Cooking > MeadFocus: name a boss by any part of its name or key; start = none beaten yet)");
			foreach (Boss boss in Bosses())
			{
				stringBuilder.AppendLine($"  {boss.Name}  (key {boss.Key}, prefab {boss.Prefab})");
			}
			File.WriteAllText(Path.Combine(Paths.ConfigPath, "OfflineCompanionsReborn.Cooking.txt"), stringBuilder.ToString());
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log.LogWarning("[Cook] couldn't write OfflineCompanionsReborn.Cooking.txt: " + ex.Message);
		}
	}
}
