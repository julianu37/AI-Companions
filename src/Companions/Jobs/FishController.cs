using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx;
using UnityEngine;

namespace Companions;

// 1.4.106: Fish, redone (user, Fish review): fishes for real, like a player.
//  - Stays around where the job was started and looks for fish in the water nearby (Fishing.ScanRadius).
//  - Goes to the bank by the fish it wants (MHR_Mover), picks the bait that fish takes, casts the game's own float at it.
//  - Real fish: they come to the float, nibble, get hooked when it strikes (reels) in time, fight, escape, use stamina,
//    can break the line. Reeling raises the Fishing skill, and the skill makes reeling faster and cheaper (the game's own
//    rules - the float doesn't know it's a companion).
//  - Fishing.Fish list (*, -Name, Name:Number) and FishTarget (pack + chests near home).
//  - Pack full near home: walks home, puts the catch away, comes back (like Hunt). Bait isn't crafted (backlog).
public class FishController : MonoBehaviour
{
	private enum FishState
	{
		Idle,
		Moving,
		Casting,
		Waiting,
		Reeling,
		GoingHome
	}

	private ZNetView _nview;

	private CompanionAI _ai;

	private CompanionCombatAI _combatAI;

	private CompanionSetup _setup;

	private Humanoid _humanoid;

	private Character _character;

	private CompanionTalk _talk;

	private FishState _state;

	private float _stateSince;

	private float _scanTimer;

	private float _lastSpeechTime;

	private float _nextWhyLog;

	private Vector3 _anchor;

	private bool _hasAnchor;

	private Fish _fish;

	private string _fishName;

	private Vector3 _stand;

	private string _bait;

	private float _castAt;

	private int _castTries;

	private bool _settled;

	private bool _retrieving;

	private float _hookTry;

	private Fish _hooked;

	private string _hookedName;

	private int _haveBefore;

	// how far a full cast goes - learned from where the float lands (1.4.115 log: 100% landed 24 m out; starting at 12
	// made the first cast of every session go twice as far as needed)
	private float _castScale = 24f;

	private float _nextUnloadTry;

	private float _nextCount;

	private Transform _rodTop;

	private readonly Dictionary<string, int> _counts = new Dictionary<string, int>();

	private readonly Dictionary<int, float> _skip = new Dictionary<int, float>();

	private static readonly HarmonyLib.AccessTools.FieldRef<Humanoid, float> s_drawTime = HarmonyLib.AccessTools.FieldRefAccess<Humanoid, float>("m_attackDrawTime");

	// the bait it picked for its last cast (for the float, if the cast didn't carry its ammo)
	internal static string LastBait(Character c)
	{
		FishController fc = (c != null) ? c.GetComponent<FishController>() : null;
		return fc?._bait;
	}

	public bool IsActive
	{
		get
		{
			if (GetMode() == 9)
			{
				return _state != FishState.Idle;
			}
			return false;
		}
	}

	public void NotifyActionModeChanged()
	{
		ReelOff();
		_state = FishState.Idle;
		_hasAnchor = false;
		_scanTimer = 0f;
		_lastSpeechTime = 0f;
		RestoreGear();
	}

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
		_talk = GetComponent<CompanionTalk>();
	}

	private void Update()
	{
		if (_nview == null || !_nview.IsOwner() || _ai == null || _humanoid == null || CompanionRest.MHR_ShouldPauseWork(this))
		{
			return;
		}
		if (GetMode() != 9)
		{
			if (_state != FishState.Idle || _rodOut)
			{
				ReelOff();
				_state = FishState.Idle;
				RestoreGear();
			}
			_hasAnchor = false;
			return;
		}
		if (!_hasAnchor)
		{
			_hasAnchor = true;
			_anchor = base.transform.position;
			if (CompanionsPlugin.LogNormal) Log("Fishing around " + MHR_Mover.Fmt(_anchor) + ", up to " + Radius().ToString("F0") + " m out");
			_foodLogged = false;
		}
		if (CompanionInteractPanel.IsOpenFor(_setup) || CompanionRadialMenu.IsOpenFor(_setup))
		{
			_ai.StopMoving();
			return;
		}
		if (_combatAI != null && _combatAI.IsEngaged)
		{
			// a fight: the float is lost when it swings anyway; start over afterwards
			if (_state != FishState.Idle || _rodOut)
			{
				ReelOff();
				_state = FishState.Idle;
				RestoreGear();
			}
			return;
		}
		if (MHR_ToolKeeper.IsBusy(this) || MHR_Loot.Active(this) || MHR_JobRules.OrderPending(this))
		{
			return;
		}
		switch (_state)
		{
		case FishState.Idle:
			UpdateIdle();
			break;
		case FishState.Moving:
			UpdateMoving();
			break;
		case FishState.Casting:
			UpdateCasting();
			break;
		case FishState.Waiting:
			UpdateWaiting();
			break;
		case FishState.Reeling:
			UpdateReeling();
			break;
		case FishState.GoingHome:
			UpdateGoingHome();
			break;
		}
	}

	private static float Radius()
	{
		return ModConfig.FishScanRadius?.Value ?? 30f;
	}

	private void Enter(FishState s)
	{
		_state = s;
		_stateSince = Time.time;
	}

	// ------------------------------------------------------------------ choosing what to catch

	private void UpdateIdle()
	{
		_scanTimer -= Time.deltaTime;
		if (_scanTimer > 0f)
		{
			return;
		}
		_scanTimer = 3f;
		FishCatalog.Ensure();
		if (FindRod() == null)
		{
			Say("hc_speech_fish_need_rod");
			return;
		}
		if (PackFull())
		{
			if (_setup.HasHomePosition() && MHR_ToolKeeper.NearHome(_setup))
			{
				if (Time.time >= _nextUnloadTry)
				{
					if (MHR_ToolKeeper.AtHome(_setup))
					{
						_nextUnloadTry = Time.time + 60f;
						if (CompanionsPlugin.LogNormal) Log("Pack is full - putting the catch away");
						MHR_ToolKeeper.RequestUnload(_setup);
						_nextCount = 0f;
					}
					else
					{
						if (CompanionsPlugin.LogNormal) Log("Pack is full - taking the catch home");
						MHR_Mover.Release(_ai);
						Enter(FishState.GoingHome);
					}
				}
				return;
			}
			Say("hc_speech_fish_full");
			WhyLog("Pack is full and home is too far - not fishing until it's emptied");
			return;
		}
		RefreshCounts();
		if (!PickTarget(out string why))
		{
			WhyLog("Nothing to fish (" + why + ")");
			// 1.5.43 (phase 3): the concrete reasons in turn - each fish at its target, the bait a fish here takes, no bank
			// to fish from, or no fish within the range from the settings
			if (_whyItems.Count == 0)
			{
				_whyItems.Add(new CompanionStatus.Item(null, "hc_why_fish_none", Radius().ToString("F0")));
			}
			if (why.StartsWith("no bait"))
			{
				Say("hc_speech_fish_need_bait");
			}
			else if (why.StartsWith("no fish"))
			{
				Say("hc_speech_fish_no_water");
			}
			// after Say (which records its own line): the status shows the concrete reasons
			CompanionStatus.WhyRotate(this, _whyItems);
			return;
		}
		_castTries = 0;
		if (!_foodLogged)
		{
			_foodLogged = true;
			string food = MHR_ToolKeeper.StaminaFoodText(_humanoid.GetInventory());
			if (CompanionsPlugin.LogNormal) Log(food != null ? "Stamina food for reeling: " + food : "No stamina food in the pack");
		}
		CompanionStatus.Clear(this);
		CompanionStatus.Doing(this, "hc_doing_fish", FishCatalog.Local(_fish));
		MHR_Mover.Release(_ai);
		Enter(FishState.Moving);
		if (CompanionsPlugin.LogNormal) Log("Going to fish for " + _fishName + " from " + MHR_Mover.Fmt(_stand) + " with " + CookController.Name(_bait) + NeedText(_fishName));
	}

	private bool PickTarget(out string why)
	{
		why = null;
		Inventory inv = _humanoid.GetInventory();
		Vector3 me = base.transform.position;
		float radius = Radius();
		int seen = 0;
		int enough = 0;
		int noBait = 0;
		int noBank = 0;
		string noBaitFor = null;
		string noBankAt = null;
		float best = float.MaxValue;
		Fish bestFish = null;
		Vector3 bestStand = Vector3.zero;
		string bestBait = null;
		_whyItems.Clear();
		HashSet<string> told = new HashSet<string>();
		foreach (IMonoUpdater u in Fish.Instances)
		{
			Fish fish = u as Fish;
			if (fish == null || fish.IsHooked())
			{
				continue;
			}
			Vector3 p = fish.transform.position;
			if (Flat(p, _anchor) > radius)
			{
				continue;
			}
			if (_skip.TryGetValue(fish.GetInstanceID(), out var until) && Time.time < until)
			{
				continue;
			}
			string name = FishCatalog.PrefabName(fish.gameObject);
			if (!FishCatalog.Allowed(name, fish))
			{
				continue;
			}
			seen++;
			if (!Needs(name))
			{
				enough++;
				if (told.Add("enough:" + name) && _whyItems.Count < 8)
				{
					_counts.TryGetValue(name, out var have);
					_whyItems.Add(new CompanionStatus.Item(null, "hc_why_fish_at_target", FishCatalog.Local(fish), have, FishCatalog.Target(name)));
				}
				continue;
			}
			string bait = BestBait(fish, inv);
			if (bait == null)
			{
				noBait++;
				noBaitFor = noBaitFor ?? (FishCatalog.Local(fish) + " (takes " + FishCatalog.BaitsText(fish) + ")");
				if (told.Add("bait:" + name) && _whyItems.Count < 8)
				{
					_whyItems.Add(new CompanionStatus.Item(null, "hc_why_fish_bait", FishCatalog.Local(fish), FishCatalog.BaitNames(fish)));
				}
				continue;
			}
			if (!SpotFor(fish, out var stand, out var spotCost))
			{
				if (told.Add("bank:" + name) && _whyItems.Count < 8)
				{
					_whyItems.Add(new CompanionStatus.Item(null, "hc_why_fish_no_bank", FishCatalog.Local(fish)));
				}
				noBank++;
				noBankAt = noBankAt ?? (FishCatalog.Local(fish) + " at " + MHR_Mover.Fmt(p) + ", water " + MHR_Water.WaterLevel(p).ToString("F1"));
				continue;
			}
			float d = spotCost;
			if (d < best)
			{
				best = d;
				bestFish = fish;
				bestStand = stand;
				bestBait = bait;
			}
		}
		if (bestFish == null)
		{
			why = seen == 0 ? ("no fish within " + radius.ToString("F0") + " m") : (enough == seen ? "enough of every fish here" : (noBait > 0 && noBank == 0 ? ("no bait for " + noBaitFor) : ("no bank to fish from within a cast - " + seen + " fish seen, e.g. " + noBankAt)));
			return false;
		}
		_fish = bestFish;
		_fishName = FishCatalog.PrefabName(bestFish.gameObject);
		_stand = bestStand;
		_bait = bestBait;
		return true;
	}

	// the closest wanted fish within a cast of where it stands now, in sight, with bait for it
	private bool PickFromHere()
	{
		Inventory inv = _humanoid.GetInventory();
		Vector3 me = base.transform.position;
		Fish best = null;
		string bestBait = null;
		float bestD = MaxCast;
		foreach (IMonoUpdater u in Fish.Instances)
		{
			Fish fish = u as Fish;
			if (fish == null || fish.IsHooked())
			{
				continue;
			}
			Vector3 fp = fish.transform.position;
			float d = Flat(me, fp);
			if (d >= bestD || d < 2f)
			{
				continue;
			}
			if (_skip.TryGetValue(fish.GetInstanceID(), out var until) && Time.time < until)
			{
				continue;
			}
			string name = FishCatalog.PrefabName(fish.gameObject);
			if (!FishCatalog.Allowed(name, fish) || !Needs(name))
			{
				continue;
			}
			string bait = BestBait(fish, inv);
			if (bait == null)
			{
				continue;
			}
			float water = Level(fp, fp.y);
			if (Physics.Linecast(me + Vector3.up * 1.6f, new Vector3(fp.x, water + 0.3f, fp.z), LayerMask.GetMask("Default", "static_solid", "piece", "terrain")))
			{
				continue;
			}
			bestD = d;
			best = fish;
			bestBait = bait;
		}
		if (best == null)
		{
			return false;
		}
		_fish = best;
		_fishName = FishCatalog.PrefabName(best.gameObject);
		_bait = bestBait;
		_stand = me;
		return true;
	}

	// the bait in the pack this fish takes best
	private static string BestBait(Fish fish, Inventory inv)
	{
		string best = null;
		float chance = -1f;
		foreach (Fish.BaitSetting b in fish.m_baits)
		{
			if (b?.m_bait == null)
			{
				continue;
			}
			string n = b.m_bait.gameObject.name;
			if (b.m_chance > chance && CountIn(inv, n) > 0)
			{
				chance = b.m_chance;
				best = n;
			}
		}
		return best;
	}

	// 1.4.108: the places around the fishing spot where it can stand and fish: dry ground or a dock right next to water
	// at least 1 m deep, with room to stand. Mapped once a minute (1.4.107 test: searching out from each fish found no
	// bank from the dock for minutes).
	private struct Shore
	{
		public Vector3 Stand;

		public Vector3 Water;

		public bool Wet;
	}

	private readonly List<Shore> _shore = new List<Shore>();

	private float _shoreAt = -999f;

	private Vector3 _shoreFor;

	private void MapShore()
	{
		if (Time.time - _shoreAt < 300f && Flat(_shoreFor, _anchor) < 2f)
		{
			return;
		}
		_shoreAt = Time.time;
		_shoreFor = _anchor;
		_shore.Clear();
		float r = Radius() + 6f;
		float baseWater = MHR_Water.WaterLevel(_anchor);
		if (baseWater < -1000f)
		{
			baseWater = (ZoneSystem.instance != null) ? ZoneSystem.instance.m_waterLevel : 30f;
		}
		// 1.4.113: any dry spot (ground, dock) with water within 5 m - the fish themselves are further out, the line of
		// sight to them is checked when choosing (1.4.112 wanted 1 m deep water within 2.5 m and a meter of dry ground on
		// top of that: one place left on that beach). Not right on the edge: the nearest water at least 1 m away.
		for (float x = -r; x <= r; x += 2f)
		{
			for (float z = -r; z <= r; z += 2f)
			{
				if (x * x + z * z > r * r)
				{
					continue;
				}
				Vector3 p = _anchor + new Vector3(x, 0f, z);
				float wl = Level(p, baseWater);
				// 1.4.114: standing in shallow water is fine (user: "less than waist deep should be ok", prefer dry land)
				if (!Top(p, wl, out var top) || top.y < wl - WadeDepth || top.y > wl + 10f)
				{
					continue;
				}
				bool wet = top.y < wl + 0.1f;
				// footing: something to stand on 0.3 m round it, none of it deep water. A rock top is fine (user: a player
				// would balance on a rock too); a point it can't stand on isn't. Falls are handled (see Fell).
				bool firm = true;
				for (int k = 0; k < 4 && firm; k++)
				{
					Vector3 q = top + Quaternion.Euler(0f, k * 90f, 0f) * Vector3.forward * 0.3f;
					firm = Top(q, wl, out var qt) && Mathf.Abs(qt.y - top.y) < 0.4f && qt.y > wl - WadeDepth;
				}
				if (!firm)
				{
					continue;
				}
				float nearest = 99f;
				Vector3 water = Vector3.zero;
				for (int i = 0; i < 8; i++)
				{
					Vector3 dir = Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward;
					for (float d = 1f; d <= 5f; d += 1f)
					{
						Vector3 w = top + dir * d;
						float lw = Level(w, baseWater);
						if (Top(w, lw, out var under) && lw - under.y > WadeDepth)
						{
							if (d < nearest)
							{
								nearest = d;
								water = new Vector3(w.x, lw, w.z);
							}
							break;
						}
					}
				}
				if (nearest > 5f)
				{
					continue;
				}
				if (Physics.CheckCapsule(top + Vector3.up * 0.6f, top + Vector3.up * 1.6f, 0.3f, LayerMask.GetMask("Default", "static_solid", "piece")) || MHR_Mover.InFire(top))
				{
					continue;
				}
				_shore.Add(new Shore
				{
					Stand = top,
					Water = water,
					Wet = wet
				});
			}
		}
		if (CompanionsPlugin.LogDetailed) LogDebug("Mapped " + _shore.Count + " places to fish from within " + r.ToString("F0") + " m");
	}

	private static float Level(Vector3 p, float dflt)
	{
		float w = MHR_Water.WaterLevel(p);
		return (w < -1000f) ? dflt : w;
	}

	// the shore place nearest to us from which this fish is within a cast and in sight
	// 1.4.114: up to here in the water is fine to stand in (about waist deep is where it would start swimming)
	private const float WadeDepth = 0.8f;

	// 1.4.115: spots it fell off twice are left alone for a while
	private readonly Dictionary<Vector3Int, int> _falls = new Dictionary<Vector3Int, int>();

	private readonly Dictionary<Vector3Int, float> _badSpot = new Dictionary<Vector3Int, float>();

	private static Vector3Int Cell(Vector3 p)
	{
		return new Vector3Int(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), Mathf.RoundToInt(p.z));
	}

	private void Fell(string when)
	{
		ReelOff();
		Vector3Int c = Cell(_stand);
		_falls.TryGetValue(c, out var n);
		_falls[c] = ++n;
		if (n >= 2)
		{
			_badSpot[c] = Time.time + 300f;
			_falls.Remove(c);
			if (CompanionsPlugin.LogNormal) Log("Fell off its fishing spot again (" + when + ") - trying somewhere else");
			Enter(FishState.Idle);
			_scanTimer = 0.5f;
		}
		else
		{
			if (CompanionsPlugin.LogDetailed) LogDebug("Fell off its fishing spot (" + when + ") - getting back on");
			Enter(FishState.Moving);
		}
	}

	private bool SpotFor(Fish fish, out Vector3 stand)
	{
		return SpotFor(fish, out stand, out _);
	}

	// cheapest place: a short walk, a short cast (user: "prefer casting to closer fish"), dry rather than wet
	private bool SpotFor(Fish fish, out Vector3 stand, out float cost)
	{
		stand = Vector3.zero;
		cost = float.MaxValue;
		MapShore();
		Vector3 fp = fish.transform.position;
		float water = Level(fp, fp.y);
		Vector3 target = new Vector3(fp.x, water, fp.z);
		Vector3 me = base.transform.position;
		float best = float.MaxValue;
		bool found = false;
		foreach (Shore sh in _shore)
		{
			if (_badSpot.TryGetValue(Cell(sh.Stand), out var badUntil) && Time.time < badUntil)
			{
				continue;
			}
			float d = Flat(sh.Stand, target);
			if (d < 2f || d > MaxCast)
			{
				continue;
			}
			float c = Flat(me, sh.Stand) + d * 1.5f + (sh.Wet ? 10f : 0f);
			if (c >= best)
			{
				continue;
			}
			if (Physics.Linecast(sh.Stand + Vector3.up * 1.6f, target + Vector3.up * 0.3f, LayerMask.GetMask("Default", "static_solid", "piece", "terrain")))
			{
				continue;
			}
			best = c;
			stand = sh.Stand;
			found = true;
		}
		cost = best;
		return found;
	}

	// a dry spot on the bank (or a dock, a wall) within a cast of the fish: where it stands now if that works, else walk
	// out from the fish towards where we are (then all round) to the first dry ground.
	// 1.4.107: banks up to 10 m above the water count (1.4.106 test: the moat walls are more than 4 m high, so every
	// bank was turned down - "no bank to fish from within a cast"); the ground is found from above, so a dock or a
	// wall top counts too
	private bool BankSpot(Fish fish, out Vector3 stand)
	{
		stand = Vector3.zero;
		Vector3 fp = fish.transform.position;
		float water = MHR_Water.WaterLevel(fp);
		if (water < -1000f)
		{
			return false;
		}
		Vector3 target = new Vector3(fp.x, water, fp.z);
		Vector3 me = base.transform.position;
		if (GoodStand(me, target, water))
		{
			stand = me;
			return true;
		}
		Vector3 toMe = me - fp;
		toMe.y = 0f;
		float a0 = (toMe.sqrMagnitude > 0.01f) ? Mathf.Atan2(toMe.x, toMe.z) * 57.29578f : 0f;
		for (int i = 0; i < 12; i++)
		{
			float ang = a0 + ((i % 2 == 0) ? 1 : (-1)) * ((i + 1) / 2) * 30f;
			Vector3 dir = Quaternion.Euler(0f, ang, 0f) * Vector3.forward;
			for (float x = 2f; x <= 14f; x += 1f)
			{
				Vector3 p = fp + dir * x;
				if (!Top(p, water, out var top))
				{
					continue;
				}
				if (top.y < water + 0.1f)
				{
					continue;   // still water (or ground under it)
				}
				Vector3 q = p + dir * 0.6f;
				if (Top(q, water, out var qTop) && qTop.y >= water + 0.1f && GoodStand(qTop, target, water))
				{
					stand = qTop;
					return true;
				}
				break;   // the first dry ground this way doesn't work; try another direction
			}
		}
		return false;
	}

	// the walkable surface at p, found from well above the water (terrain, a dock, a wall top)
	private static bool Top(Vector3 p, float water, out Vector3 top)
	{
		top = p;
		if (!Physics.Raycast(new Vector3(p.x, water + 30f, p.z), Vector3.down, out var hit, 60f, LayerMask.GetMask("Default", "static_solid", "piece", "terrain")))
		{
			return false;
		}
		if (hit.normal.y < 0.6f)
		{
			return false;
		}
		top = hit.point;
		return true;
	}

	private bool GoodStand(Vector3 q, Vector3 target, float water)
	{
		float d = Flat(q, target);
		if (d < 2f || d > MaxCast || q.y > water + 10f || q.y < water - 0.2f)
		{
			return false;
		}
		if (MHR_Water.WouldSwim(_character, q) || MHR_Mover.InFire(q))
		{
			return false;
		}
		// room to stand, and a clear line from head height to the water over the fish
		if (Physics.CheckCapsule(q + Vector3.up * 0.6f, q + Vector3.up * 1.6f, 0.3f, LayerMask.GetMask("Default", "static_solid", "piece")))
		{
			return false;
		}
		return !Physics.Linecast(q + Vector3.up * 1.6f, target + Vector3.up * 0.3f, LayerMask.GetMask("Default", "static_solid", "piece", "terrain"));
	}

	// ------------------------------------------------------------------ getting there, casting

	private void UpdateMoving()
	{
		if (_fish == null || _fish.IsHooked())
		{
			Enter(FishState.Idle);
			_scanTimer = 0.5f;
			return;
		}
		if (Flat(base.transform.position, _stand) <= 1.2f && Mathf.Abs(base.transform.position.y - _stand.y) < 1.5f)
		{
			_ai.StopMoving();
			MHR_Mover.Release(_ai);
			Enter(FishState.Casting);
			return;
		}
		MHR_Mover.Go(_ai, Time.deltaTime, _stand, 0.8f, true, "Fish");
		if (MHR_Mover.RouteSwim(_ai) > MHR_Mover.JobSwim() || MHR_Mover.GaveUp(_ai) || Time.time - _stateSince > 60f)
		{
			if (CompanionsPlugin.LogNormal) Log("Can't get to the bank by the " + _fishName + " - trying another");
			_skip[_fish.GetInstanceID()] = Time.time + 120f;
			MHR_Mover.Release(_ai);
			Enter(FishState.Idle);
			_scanTimer = 0.5f;
		}
	}

	private float _nextRestLog;

	private void UpdateCasting()
	{
		_ai.StopMoving();
		// 1.4.112: never fish from the water (1.4.111 test: slipped in by the bank and kept fishing while swimming) -
		// back to the bank first
		if (_character.IsSwimming() || Flat(base.transform.position, _stand) > 2.5f || base.transform.position.y < _stand.y - 1.2f)
		{
			Fell("before casting");
			return;
		}
		// 1.4.111: rest before casting until stamina is nearly full (user: "they should wait to cast until their stamina
		// has filled up") - reeling a fish in with an empty bar loses it
		CompanionStamina st = GetComponent<CompanionStamina>();
		if (st != null && st.MaxStamina > 0f && st.Stamina < st.MaxStamina * 0.9f)
		{
			_ai.LookAtPoint(AimPoint());
			_stateSince = Time.time;
			if (Time.time >= _nextRestLog)
			{
				_nextRestLog = Time.time + 20f;
				if (CompanionsPlugin.LogDetailed) LogDebug("Getting my breath back before casting (" + st.Stamina.ToString("F0") + "/" + st.MaxStamina.ToString("F0") + " stamina)");
			}
			return;
		}
		ItemDrop.ItemData rod = FindRod();
		Inventory inv = _humanoid.GetInventory();
		ItemDrop.ItemData bait = FindItem(inv, _bait);
		if (rod == null || bait == null)
		{
			if (CompanionsPlugin.LogNormal) Log(rod == null ? "No fishing rod" : ("Out of " + CookController.Name(_bait)));
			Enter(FishState.Idle);
			return;
		}
		if (_setup != null)
		{
			_setup.SuppressAutoEquip = true;
			_rodOut = true;
		}
		if (_humanoid.GetCurrentWeapon() != rod)
		{
			_humanoid.EquipItem(rod);
			return;
		}
		if (_humanoid.GetAmmoItem() != bait)
		{
			_humanoid.EquipItem(bait);
		}
		if (_fish != null && Flat(base.transform.position, _fish.transform.position) > MaxCast + 6f)
		{
			if (CompanionsPlugin.LogDetailed) LogDebug("The " + _fishName + " has swum off - choosing again");
			Enter(FishState.Idle);
			_scanTimer = 0.5f;
			return;
		}
		Vector3 aim = AimPoint();
		_ai.LookAtPoint(aim);
		if (_character.InAttack() || !_ai.IsLookingAtPoint(aim, 8f) || Time.time - _stateSince < 0.5f)
		{
			if (Time.time - _stateSince > 10f)
			{
				if (CompanionsPlugin.LogNormal) Log("Couldn't get a cast off - trying again");
				Enter(FishState.Idle);
			}
			return;
		}
		// how hard to cast: learned from where the last float landed
		float want = Flat(base.transform.position, aim);
		float pct = Mathf.Clamp01(want / Mathf.Max(4f, _castScale));
		Attack attack = rod.m_shared.m_attack;
		float full = attack.m_drawDurationMin;
		float skill = _character.GetSkillFactor(rod.m_shared.m_skillType);
		float draw = Mathf.Lerp(full, full * 0.2f, skill);
		s_drawTime(_humanoid) = (draw > 0f) ? Mathf.Max(0.01f, pct * draw) : 0f;
		bool ok = _humanoid.StartAttack(null, secondaryAttack: false);
		s_drawTime(_humanoid) = 0f;
		if (!ok)
		{
			return;
		}
		_castAt = Time.time;
		_castTries++;
		_settled = false;
		_retrieving = false;
		_hooked = null;
		if (CompanionsPlugin.LogDetailed) LogDebug("Casting at the " + _fishName + " (" + want.ToString("F0") + " m, " + (pct * 100f).ToString("F0") + "% cast)");
		Enter(FishState.Waiting);
	}

	// 1.4.109: a full cast reaches about 24 m (1.4.108 log: "Float out 24 m" at 100%); 20 m leaves room for the line
	// (the game breaks it past 30 m) and the spots were mostly further than 14 m from the fish
	private const float MaxCast = 20f;

	// at the fish, but no further than a good cast - fish come to a float within 10 m
	private Vector3 AimPoint()
	{
		Vector3 me = base.transform.position;
		Vector3 p = (_fish != null) ? _fish.transform.position : (me + base.transform.forward * 8f);
		Vector3 d = p - me;
		d.y = 0f;
		if (d.magnitude > MaxCast)
		{
			p = me + d.normalized * MaxCast;
		}
		p.y = Level(p, me.y - 1f);
		return p;
	}

	// ------------------------------------------------------------------ the float is out

	private void UpdateWaiting()
	{
		_ai.StopMoving();
		if (_character.IsSwimming() || base.transform.position.y < _stand.y - 1.2f)
		{
			Fell("waiting for a bite");
			return;
		}
		FishingFloat ff = MHR_FishingFloat.Of(_character);
		if (ff == null)
		{
			if (Time.time - _castAt > 4f)
			{
				if (CompanionsPlugin.LogDetailed) LogDebug("The cast didn't come off");
				if (_castTries >= 3)
				{
					SkipFish(60f);
					Enter(FishState.Idle);
				}
				else
				{
					Enter(FishState.Casting);
				}
			}
			return;
		}
		_ai.LookAtPoint(ff.transform.position);
		if (!_settled && Time.time - _castAt > 2.5f)
		{
			_settled = true;
			float landed = Flat(base.transform.position, ff.transform.position);
			float want = Flat(base.transform.position, AimPoint());
			float pct = Mathf.Clamp01(want / Mathf.Max(4f, _castScale));
			if (pct > 0.05f && ff.IsInWater())
			{
				_castScale = Mathf.Clamp(Mathf.Lerp(_castScale, landed / pct, 0.5f), 4f, 40f);
			}
			if (!ff.IsInWater())
			{
				if (CompanionsPlugin.LogDetailed) LogDebug("The float landed on dry ground - reeling in");
				Retrieve();
				return;
			}
		}
		// a bite: strike (reel) straight away, like a player watching the float
		Fish nibbler = MHR_FishingFloat.Nibbler(ff);
		if (nibbler != null && Time.time - MHR_FishingFloat.NibbleTime(ff) < 0.4f)
		{
			_hookTry = Time.time;
			ReelOn();
			Enter(FishState.Reeling);
			return;
		}
		if (Time.time - _castAt > (ModConfig.FishCastWait?.Value ?? 60f))
		{
			if (CompanionsPlugin.LogDetailed) LogDebug("No bite - reeling in");
			Retrieve();
		}
	}

	private void Retrieve()
	{
		_retrieving = true;
		ReelOn();
		Enter(FishState.Reeling);
	}

	private void UpdateReeling()
	{
		_ai.StopMoving();
		if (_character.IsSwimming())
		{
			// can't reel while swimming - the fish is lost, like for a player
			if (_hooked != null)
			{
				if (CompanionsPlugin.LogNormal) Log("Fell in with a " + _hookedName + " on the line - lost it");
			}
			_hooked = null;
			_hookedName = null;
			Fell("reeling");
			return;
		}
		FishingFloat ff = MHR_FishingFloat.Of(_character);
		if (ff == null)
		{
			ReelOff();
			Landed();
			return;
		}
		_ai.LookAtPoint(ff.transform.position);
		Fish fish = ff.GetCatch();
		if (fish == null)
		{
			if (_hooked != null)
			{
				// it got off (out of stamina, or it fought free)
				if (CompanionsPlugin.LogNormal) Log("The " + _hookedName + " got away");
				_talk?.Say(ModLocalization.Loc("hc_speech_fish_got_away"));
				_hooked = null;
				Retrieve();
				return;
			}
			if (!_retrieving && Time.time - _hookTry > 1f)
			{
				// struck at a nibble and missed: let the float settle and wait again
				ReelOff();
				Enter(FishState.Waiting);
			}
			else
			{
				ReelOn();
			}
		}
		else
		{
			if (_hooked == null)
			{
				_hooked = fish;
				_hookedName = FishCatalog.PrefabName(fish.gameObject);
				_haveBefore = CountIn(_humanoid.GetInventory(), _hookedName);
				if (CompanionsPlugin.LogNormal) Log("Hooked a " + FishCatalog.Local(fish) + " - reeling it in");
			}
			// like a player: ease off while it fights (or the line is tight), reel while it rests
			float tension = Tension(ff);
			if (fish.IsEscaping() || tension > 2.5f)
			{
				ReelOff();
			}
			else
			{
				ReelOn();
			}
		}
		if (Time.time - _stateSince > 90f)
		{
			if (CompanionsPlugin.LogDetailed) LogDebug("Reeling is taking too long - letting go");
			ReelOff();
			Enter(FishState.Idle);
		}
	}

	private void Landed()
	{
		if (_hooked != null || _hookedName != null)
		{
			int now = CountIn(_humanoid.GetInventory(), _hookedName);
			if (_hookedName != null && now > _haveBefore)
			{
				string local = CookController.Name(_hookedName);
				if (CompanionsPlugin.LogNormal) Log("Caught a " + local + NeedText(_hookedName));
				_talk?.Say(ModLocalization.LocFmt("hc_speech_fish_caught", local), force: true);
				_nextCount = 0f;
			}
			else if (_hooked != null)
			{
				if (CompanionsPlugin.LogNormal) Log("Lost the " + _hookedName + " (the line broke)");
			}
		}
		_hooked = null;
		_hookedName = null;
		_retrieving = false;
		RefreshCounts();
		// 1.4.116: from where it stands, cast at the closest fish it wants (user: close fish, but it kept casting at the
		// one it first picked further out); else choose a spot again
		if (!PackFull() && PickFromHere())
		{
			_castTries = 0;
			Enter(FishState.Casting);
		}
		else
		{
			Enter(FishState.Idle);
			_scanTimer = 0.5f;
		}
	}

	private float Tension(FishingFloat ff)
	{
		if (_rodTop == null)
		{
			_rodTop = FindChild(base.transform, "_RodTop");
		}
		if (_rodTop == null)
		{
			return 0f;
		}
		return Vector3.Distance(_rodTop.position, ff.transform.position) - MHR_FishingFloat.LineLength(ff);
	}

	private void ReelOn()
	{
		ReflectionHelper.TrySetBlocking(_character, value: true);
	}

	private void ReelOff()
	{
		if (_character != null)
		{
			ReflectionHelper.TrySetBlocking(_character, value: false);
		}
	}

	private void SkipFish(float seconds)
	{
		if (_fish != null)
		{
			_skip[_fish.GetInstanceID()] = Time.time + seconds;
		}
		if (_skip.Count > 100)
		{
			_skip.Clear();
		}
	}

	// ------------------------------------------------------------------ taking the catch home

	private void UpdateGoingHome()
	{
		if (!_setup.HasHomePosition())
		{
			Enter(FishState.Idle);
			return;
		}
		if (MHR_ToolKeeper.AtHome(_setup))
		{
			_nextUnloadTry = Time.time + 60f;
			if (CompanionsPlugin.LogNormal) Log("Home - putting the catch away");
			MHR_ToolKeeper.RequestUnload(_setup);
			_nextCount = 0f;
			MHR_Mover.Release(_ai);
			Enter(FishState.Idle);
			return;
		}
		MHR_Mover.Go(_ai, Time.deltaTime, _setup.GetHomePosition(), 4f, true, "Fish");
		if (MHR_Mover.GaveUp(_ai))
		{
			if (CompanionsPlugin.LogNormal) Log("Can't find the way home with the catch - trying again later");
			_nextUnloadTry = Time.time + 60f;
			Enter(FishState.Idle);
		}
	}

	private bool PackFull()
	{
		Inventory inv = _humanoid.GetInventory();
		if (inv == null)
		{
			return false;
		}
		float full = ModConfig.FishBagFull?.Value ?? 0.9f;
		return inv.GetEmptySlots() == 0 || inv.GetTotalWeight() >= CompanionCarry.Max(inv) * full;
	}

	// ------------------------------------------------------------------ counts and targets

	private bool _foodLogged;

	// 1.5.43: why there's nothing to fish, filled by PickTarget
	private readonly List<CompanionStatus.Item> _whyItems = new List<CompanionStatus.Item>();

	private bool Needs(string fish)
	{
		int target = FishCatalog.Target(fish);
		return target <= 0 || !_counts.TryGetValue(fish, out var have) || have < target;
	}

	private string NeedText(string fish)
	{
		int target = FishCatalog.Target(fish);
		_counts.TryGetValue(fish, out var have);
		return " (" + have + "/" + (target > 0 ? target.ToString() : "no limit") + ")";
	}

	private void RefreshCounts()
	{
		if (Time.time < _nextCount)
		{
			return;
		}
		_nextCount = Time.time + 15f;
		_counts.Clear();
		Add(_humanoid.GetInventory());
		Vector3 center = _setup.HasHomePosition() ? _setup.GetHomePosition() : _anchor;
		float radius = _setup.HasHomePosition() ? MHR_ToolKeeper.HomeRange() : 25f;
		foreach (Container c in MHR_Chests.Near(center, radius))
		{
			Inventory ci = c?.GetInventory();
			if (ci != null)
			{
				Add(ci);
			}
		}
	}

	private void Add(Inventory inv)
	{
		if (inv == null)
		{
			return;
		}
		foreach (ItemDrop.ItemData item in inv.GetAllItems())
		{
			string n = item?.m_dropPrefab?.name;
			if (n != null && FishCatalog.IsFish(n))
			{
				_counts.TryGetValue(n, out var have);
				_counts[n] = have + item.m_stack;
			}
		}
	}

	// ------------------------------------------------------------------ helpers

	private ItemDrop.ItemData FindRod()
	{
		foreach (ItemDrop.ItemData item in _humanoid.GetInventory().GetAllItems())
		{
			if (item.m_shared.m_animationState == ItemDrop.ItemData.AnimationState.FishingRod)
			{
				return item;
			}
		}
		return null;
	}

	private static ItemDrop.ItemData FindItem(Inventory inv, string prefab)
	{
		if (inv == null || prefab == null)
		{
			return null;
		}
		foreach (ItemDrop.ItemData item in inv.GetAllItems())
		{
			if (item?.m_dropPrefab != null && item.m_dropPrefab.name == prefab)
			{
				return item;
			}
		}
		return null;
	}

	private static int CountIn(Inventory inv, string prefab)
	{
		int n = 0;
		if (inv == null || prefab == null)
		{
			return 0;
		}
		foreach (ItemDrop.ItemData item in inv.GetAllItems())
		{
			if (item?.m_dropPrefab != null && item.m_dropPrefab.name == prefab)
			{
				n += item.m_stack;
			}
		}
		return n;
	}

	// 1.4.150: the rod is in hand (stays out between casts) - put it away whenever the job stops, not only mid-cast
	private bool _rodOut;

	private void RestoreGear()
	{
		_rodOut = false;
		ReelOff();
		if (_humanoid != null)
		{
			ItemDrop.ItemData w = _humanoid.GetCurrentWeapon();
			if (w != null && w.m_shared.m_animationState == ItemDrop.ItemData.AnimationState.FishingRod)
			{
				_humanoid.UnequipItem(w);
			}
		}
		if (_setup != null && _setup.SuppressAutoEquip)
		{
			_setup.SuppressAutoEquip = false;
			_setup.SyncEquipmentToInventory();
		}
	}

	private static Transform FindChild(Transform t, string name)
	{
		if (t.name == name)
		{
			return t;
		}
		for (int i = 0; i < t.childCount; i++)
		{
			Transform r = FindChild(t.GetChild(i), name);
			if (r != null)
			{
				return r;
			}
		}
		return null;
	}

	private static float Flat(Vector3 a, Vector3 b)
	{
		return new Vector2(a.x - b.x, a.z - b.z).magnitude;
	}

	private void Say(string key)
	{
		CompanionStatus.Why(this, key);
		if (Time.time - _lastSpeechTime >= 60f)
		{
			_lastSpeechTime = Time.time;
			_talk?.SayProblem("fish:" + key, ModLocalization.Loc(key));
		}
	}

	private void WhyLog(string msg)
	{
		if (Time.time >= _nextWhyLog)
		{
			_nextWhyLog = Time.time + 60f;
			if (CompanionsPlugin.LogDetailed) LogDebug(msg);
		}
	}

	private void Log(string msg)
	{
		if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log?.LogInfo("[Fish|" + (_character?.m_name ?? "?") + "] " + msg);
	}

	private void LogDebug(string msg)
	{
		if (CompanionsPlugin.LogDetailed) CompanionsPlugin.Log?.LogDebug("[Fish|" + (_character?.m_name ?? "?") + "] " + msg);
	}
}

// 1.4.106: which fish there are and what they bite on, from the game's own data (mods included)
internal static class FishCatalog
{
	private static readonly HashSet<string> s_fish = new HashSet<string>();

	private static bool s_built;

	internal static void Ensure()
	{
		if (s_built || ZNetScene.instance == null)
		{
			return;
		}
		s_built = true;
		try
		{
			StringBuilder sb = new StringBuilder();
			sb.AppendLine(CompanionsPlugin.PluginName + " - names for the [Fishing] settings. Written at every start from the game's own data, mods included.");
			sb.AppendLine();
			sb.AppendLine("FISH (Fishing > Fish)  name = in-game name  (baits it takes, with the chance)");
			SortedDictionary<string, string> lines = new SortedDictionary<string, string>();
			foreach (GameObject prefab in ZNetScene.instance.m_prefabs)
			{
				Fish fish = (prefab != null) ? prefab.GetComponent<Fish>() : null;
				if (fish == null)
				{
					continue;
				}
				s_fish.Add(prefab.name);
				lines[prefab.name] = prefab.name + " = " + Local(fish) + "  (" + BaitsText(fish) + ")";
			}
			foreach (string line in lines.Values)
			{
				sb.AppendLine("  " + line);
			}
			File.WriteAllText(Path.Combine(Paths.ConfigPath, "OfflineCompanionsReborn.Fishing.txt"), sb.ToString());
			if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log.LogInfo($"[Fish] {s_fish.Count} kinds of fish");
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log.LogWarning("[Fish] couldn't read the fish: " + ex.Message);
		}
	}

	internal static bool IsFish(string prefab)
	{
		Ensure();
		return s_fish.Contains(prefab);
	}

	// 1.5.43: just the bait names, for the status ("Herring bait or Fishing bait")
	internal static string BaitNames(Fish fish)
	{
		List<string> parts = new List<string>();
		foreach (Fish.BaitSetting b in fish.m_baits)
		{
			if (b?.m_bait != null)
			{
				parts.Add(CookController.Name(b.m_bait.gameObject.name));
			}
		}
		return parts.Count > 0 ? string.Join(" / ", parts.ToArray()) : "?";
	}

	internal static string BaitsText(Fish fish)
	{
		List<string> parts = new List<string>();
		foreach (Fish.BaitSetting b in fish.m_baits)
		{
			if (b?.m_bait != null)
			{
				parts.Add(CookController.Name(b.m_bait.gameObject.name) + " " + (b.m_chance * 100f).ToString("F0") + "%");
			}
		}
		return parts.Count > 0 ? string.Join(", ", parts.ToArray()) : "no bait";
	}

	internal static string Local(Fish fish)
	{
		string n = fish.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_name ?? fish.m_name;
		return MHR_List.Local(n) ?? n;
	}

	internal static bool Allowed(string prefab, Fish fish)
	{
		return MHR_List.Get(ModConfig.FishList?.Value ?? "*").Allows(prefab, Local(fish));
	}

	internal static int Target(string prefab)
	{
		int dflt = ModConfig.FishTarget?.Value ?? 100;
		MHR_List l = MHR_List.Get(ModConfig.FishList?.Value ?? "*");
		if (l.HasAmount(prefab))
		{
			return l.AmountFor(prefab, dflt);
		}
		string local = CookController.Name(prefab);
		return l.AmountFor(local, dflt);
	}

	internal static string PrefabName(GameObject go)
	{
		ZNetView nv = go.GetComponent<ZNetView>();
		ZDO zdo = (nv != null && nv.IsValid()) ? nv.GetZDO() : null;
		GameObject prefab = (zdo != null && ZNetScene.instance != null) ? ZNetScene.instance.GetPrefab(zdo.GetPrefab()) : null;
		if (prefab != null)
		{
			return prefab.name;
		}
		string text = go.name;
		int num = text.IndexOf("(Clone)", StringComparison.Ordinal);
		return (num >= 0) ? text.Substring(0, num).Trim() : text;
	}
}
