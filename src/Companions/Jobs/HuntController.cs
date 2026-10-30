using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx;
using UnityEngine;

namespace Companions;

// 1.4.94: Hunt, redone (user, 1.4.93 review):
//  - Follow on: goes after game near you and catches up when there's nothing close. Follow off: hunts around where it
//    is (home if it's near home, otherwise where the job was started).
//  - Anything that drops meat ([Hunting] Prey list, mods included), never tamed animals, bosses or companions.
//  - Only while a meat it drops is under [Hunting] MeatTarget (pack + chests near home). Leather and hides are a bonus.
//  - Picks up what the kill dropped (MHR_Loot, which every kill now uses).
//  - Following: keeps the catch until you're home. Follow off near home: walks home to unload when the pack is full.
//  - Bow first, melee second (CompanionCombatAI hunt mode); anything that attacks is fought the normal way.
public class HuntController : MonoBehaviour
{
	private enum HuntState
	{
		Idle,
		Hunting,
		Returning,
		GoingHome,
		Birding
	}

	private const float ScanInterval = 2f;

	private ZNetView _nview;

	private CompanionAI _ai;

	private CompanionCombatAI _combatAI;

	private CompanionSetup _setup;

	private Character _character;

	private Humanoid _humanoid;

	private HuntState _state;

	private float _scanTimer;

	private Character _prey;

	private float _nextSwapCheck;

	private Vector3 _lastPreyPos;

	private string _preyName;

	private bool _wasChasing;

	private Vector3 _anchor;

	private bool _hasAnchor;

	private bool _wasFollow;

	private float _nextUnloadTry;

	private float _nextNothingLog;

	private float _nextFullLog;

	private float _nextCount;

	private readonly Dictionary<string, int> _meatCount = new Dictionary<string, int>();

	private readonly Dictionary<int, float> _skip = new Dictionary<int, float>();

	public bool IsActive
	{
		get
		{
			if (GetMode() == 7)
			{
				return _state != HuntState.Idle;
			}
			return false;
		}
	}

	public void NotifyActionModeChanged()
	{
		ReleaseBirdBow();
		if (_combatAI != null && _combatAI.HuntMode)
		{
			_combatAI.HuntMode = false;
		}
		_state = HuntState.Idle;
		_prey = null;
		_scanTimer = 0f;
		_hasAnchor = false;
		_nextCount = 0f;
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
		_character = GetComponent<Character>();
		_humanoid = GetComponent<Humanoid>();
	}

	private void Update()
	{
		if (_nview == null || !_nview.IsOwner() || _setup == null || _ai == null || _combatAI == null || CompanionRest.MHR_ShouldPauseWork(this))
		{
			return;
		}
		if (GetMode() != 7)
		{
			ReleaseBirdBow();
			if (_state != HuntState.Idle || _prey != null)
			{
				if (_combatAI.HuntMode)
				{
					_combatAI.HuntMode = false;
				}
				_state = HuntState.Idle;
				_prey = null;
			}
			_hasAnchor = false;
			return;
		}
		bool follow = _setup.GetFollow();
		if (follow != _wasFollow)
		{
			_wasFollow = follow;
			_hasAnchor = false;
			if (_state == HuntState.Returning || _state == HuntState.GoingHome)
			{
				Stop();
			}
		}
		if (!follow && !_hasAnchor)
		{
			_hasAnchor = true;
			bool home = _setup.HasHomePosition() && MHR_ToolKeeper.NearHome(_setup);
			_anchor = home ? _setup.GetHomePosition() : base.transform.position;
			if (CompanionsPlugin.LogNormal) Log("Hunting around " + (home ? "home" : "here") + " " + MHR_Mover.Fmt(_anchor) + ", up to " + Range(follow: false).ToString("F0") + " m out");
		}
		if (MHR_ToolKeeper.IsBusy(this) || MHR_Loot.Active(this) || MHR_JobRules.OrderPending(this) || CompanionInteractPanel.IsOpenFor(_setup) || CompanionRadialMenu.IsOpenFor(_setup))
		{
			return;
		}
		switch (_state)
		{
		case HuntState.Hunting:
			UpdateHunting(follow);
			break;
		case HuntState.Birding:
			UpdateBirding(follow);
			break;
		case HuntState.GoingHome:
			UpdateGoingHome(follow);
			break;
		default:
			UpdateIdle(follow);
			break;
		}
	}

	private float Range(bool follow)
	{
		return follow ? (ModConfig.HuntFollowRange?.Value ?? 25f) : (ModConfig.HuntRange?.Value ?? 40f);
	}

	private Vector3 Center(bool follow)
	{
		if (follow && MHR_Owner.Of(this) != null)
		{
			return MHR_Owner.Of(this).transform.position;
		}
		return _hasAnchor ? _anchor : base.transform.position;
	}

	private void Stop()
	{
		ReleaseBirdBow();
		_state = HuntState.Idle;
		_ai.StopMoving();
		MHR_Mover.Release(_ai);
		_setup.ApplyFollowMode(7, force: true);
	}

	private void UpdateIdle(bool follow)
	{
		if (_combatAI.IsEngaged)
		{
			return;
		}
		if (_state == HuntState.Returning)
		{
			if (Vector3.Distance(base.transform.position, _anchor) <= 5f || follow)
			{
				Stop();
			}
			else
			{
				MHR_Mover.Go(_ai, Time.deltaTime, _anchor, 4f, true, "Hunt");
				if (MHR_Mover.GaveUp(_ai))
				{
					if (CompanionsPlugin.LogNormal) Log("Can't get back to the hunting spot - hunting from here");
					_anchor = base.transform.position;
					Stop();
				}
			}
		}
		_scanTimer -= Time.deltaTime;
		if (_scanTimer > 0f)
		{
			return;
		}
		_scanTimer = ScanInterval;
		if (PackFull())
		{
			if (!follow && _setup.HasHomePosition() && MHR_ToolKeeper.NearHome(_setup))
			{
				if (MHR_ToolKeeper.AtHome(_setup))
				{
					if (Time.time >= _nextUnloadTry)
					{
						_nextUnloadTry = Time.time + 60f;
						if (CompanionsPlugin.LogNormal) Log("Pack is full - putting the catch away");
						MHR_ToolKeeper.RequestUnload(_setup);
						_nextCount = 0f;
					}
				}
				else if (Time.time >= _nextUnloadTry)
				{
					if (CompanionsPlugin.LogNormal) Log("Pack is full - taking the catch home");
					_state = HuntState.GoingHome;
					MHR_Mover.Release(_ai);
				}
				return;
			}
			if (Time.time >= _nextFullLog)
			{
				_nextFullLog = Time.time + 120f;
				if (CompanionsPlugin.LogNormal) Log(follow ? "Pack is full - keeping the catch until we're home" : "Pack is full and home is too far - not hunting until it's emptied");
			}
			// 1.5.42: say why it isn't hunting
			CompanionStatus.Why(this, follow ? "hc_why_pack_full" : "hc_why_pack_full_far", speak: true);
			return;
		}
		// 1.5.4: its only weapon (a spear) is lying where it was thrown - fetch it before going after anything else
		if (MHR_SpearThrow.UnarmedAfterThrow(_ai, _humanoid))
		{
			CompanionStatus.Why(this, "hc_why_hunt_spear", speak: true);
			return;
		}
		Character prey = FindPrey(follow, out string why);
		if (prey == null && StartBird(follow))
		{
			return;
		}
		if (prey == null)
		{
			if (Time.time >= _nextNothingLog)
			{
				_nextNothingLog = Time.time + 60f;
				if (CompanionsPlugin.LogDetailed) LogDebug("Nothing to hunt" + (why != null ? " (" + why + ")" : ""));
			}
			// 1.5.43 (phase 3): the concrete reasons in turn - each meat at its target with the numbers, game out of reach,
			// or no game within the range from the settings
			if (_whyItems.Count == 0)
			{
				_whyItems.Add(new CompanionStatus.Item(null, (why != null && why.StartsWith("only")) ? "hc_why_hunt_reach" : "hc_why_hunt_none_range", Range(follow).ToString("F0")));
			}
			CompanionStatus.WhyRotate(this, _whyItems);
			if (!follow && _state == HuntState.Idle && Vector3.Distance(base.transform.position, _anchor) > 10f)
			{
				if (CompanionsPlugin.LogDetailed) LogDebug("Heading back to the hunting spot");
				_state = HuntState.Returning;
				MHR_Mover.Release(_ai);
			}
			return;
		}
		_combatAI.EngagePrey(prey);
		if (!_combatAI.HuntMode)
		{
			if (Time.time >= _nextNothingLog)
			{
				_nextNothingLog = Time.time + 60f;
				if (CompanionsPlugin.LogNormal) Log("Can't hunt with the Passive stance");
			}
			CompanionStatus.Why(this, "hc_why_hunt_passive", speak: true);
			return;
		}
		_prey = prey;
		_preyName = prey.m_name;
		_lastPreyPos = prey.transform.position;
		_wasChasing = true;
		_state = HuntState.Hunting;
		CompanionStatus.Clear(this);
		CompanionStatus.Doing(this, "hc_doing_hunt", Loc(prey.m_name));
		MHR_Mover.Release(_ai);
		if (CompanionsPlugin.LogNormal) Log("Going after " + Loc(prey.m_name) + " (" + Vector3.Distance(base.transform.position, prey.transform.position).ToString("F0") + " m away)" + NeedText(PrefabName(prey)));
	}

	private void UpdateHunting(bool follow)
	{
		Character prey = _prey;
		bool gone = prey == null || prey.GetComponent<ZNetView>() == null || !prey.GetComponent<ZNetView>().IsValid();
		if (!gone && (prey.GetHealth() <= 0f || prey.IsDead()))
		{
			if (CompanionsPlugin.LogDetailed) LogDebug(Loc(_preyName) + " is down");
			MHR_Loot.OnKill(_setup, prey);
			Done();
			return;
		}
		if (gone)
		{
			if (CompanionsPlugin.LogDetailed) LogDebug(Loc(_preyName) + " is gone");
			Done();
			return;
		}
		_lastPreyPos = prey.transform.position;
		if (_combatAI.IsEngaged && _combatAI.TargetCreature != null && _combatAI.TargetCreature != prey)
		{
			// something attacked: the combat code is fighting it; carry on with the prey afterwards
			_wasChasing = false;
			return;
		}
		if (!_combatAI.IsEngaged || _combatAI.TargetCreature != prey)
		{
			if (_wasChasing)
			{
				if (CompanionsPlugin.LogDetailed) LogDebug("Lost " + Loc(_preyName) + " - leaving it for a while");
				Skip(prey, 60f);
			}
			Done();
			return;
		}
		_wasChasing = true;
		// 1.5.4: a much closer animal turns up before the first strike: go for that one instead, like a player would
		// (user: "switch to a closer deer if it hasn't attacked yet")
		float closerBy = ModConfig.HuntSwitchCloserBy?.Value ?? 5f;
		if (closerBy > 0f && Time.time >= _nextSwapCheck && !_combatAI.HasAttackedTarget)
		{
			_nextSwapCheck = Time.time + 2f;
			Character closer = FindPrey(follow, out string _);
			if (closer != null && closer != prey)
			{
				float dNow = Vector3.Distance(base.transform.position, prey.transform.position);
				float dNew = Vector3.Distance(base.transform.position, closer.transform.position);
				if (dNew < dNow * (ModConfig.HuntSwitchShare?.Value ?? 0.6f) && dNow - dNew > closerBy)
				{
					_combatAI.EngagePrey(closer);
					if (_combatAI.HuntMode && _combatAI.TargetCreature == closer)
					{
						if (CompanionsPlugin.LogNormal) Log("Going for a closer " + Loc(closer.m_name) + " (" + dNew.ToString("F0") + " m) instead of the " + Loc(_preyName) + " " + dNow.ToString("F0") + " m away");
						_prey = closer;
						_preyName = closer.m_name;
						_lastPreyPos = closer.transform.position;
						return;
					}
				}
			}
		}
		float leash = Range(follow) + (ModConfig.HuntChaseExtra?.Value ?? 15f);
		if (Vector3.Distance(prey.transform.position, Center(follow)) > leash)
		{
			if (CompanionsPlugin.LogNormal) Log(Loc(_preyName) + " got away");
			GetComponent<CompanionTalk>()?.Speak(ModLocalization.Loc("hc_why_hunt_got_away"), CompanionTalk.Priority.Event, "hunt:away");
			Skip(prey, 60f);
			_combatAI.Disengage();
			Done();
		}
	}

	private void Done()
	{
		if (_combatAI.HuntMode)
		{
			_combatAI.Disengage();
		}
		_prey = null;
		_state = HuntState.Idle;
		_scanTimer = 1f;
		_nextCount = 0f;
	}

	// ---- 1.4.100: birds (user: "add hunting birds as a toggle? They drop feathers that are useful"). Birds aren't
	// creatures: they're RandomFlyingBird objects that break when hit and drop their DropOnDestroyed table. Like a
	// player: wait for one to land, sneak within bow range, aim, shoot, then pick up the feathers.
	private RandomFlyingBird _bird;

	private string _birdName;

	private Vector3 _birdPos;

	private float _birdSince;

	private float _nextShot;

	private float _noBowUntil;

	private HashSet<int> _birdBefore;

	// 1.4.102: from further away - birds only stay down a few seconds (1.4.101 test: every bird flew off before he got
	// a shot off, walking up to 20 m)
	private const float BirdShotRange = 30f;

	private bool StartBird(bool follow)
	{
		if (!(ModConfig.HuntBirds?.Value ?? true) || Time.time < _noBowUntil || _humanoid == null)
		{
			return false;
		}
		Vector3 center = Center(follow);
		float range = Range(follow);
		Vector3 me = base.transform.position;
		RandomFlyingBird best = null;
		float bestDist = float.MaxValue;
		foreach (IMonoUpdater updater in RandomFlyingBird.Instances)
		{
			RandomFlyingBird bird = updater as RandomFlyingBird;
			if (bird == null || !Landed(bird))
			{
				continue;
			}
			Vector3 p = bird.transform.position;
			if (Vector3.Distance(p, center) > range || Vector3.Distance(p, me) > BirdShotRange + 10f)
			{
				continue;
			}
			if (_skip.TryGetValue(bird.GetInstanceID(), out var until) && Time.time < until)
			{
				continue;
			}
			string prefab = BirdPrefab(bird);
			if (!HuntCatalog.CanHuntBird(prefab) || !NeedsBirdDrop(prefab) || MHR_Water.WouldSwim(_character, p))
			{
				continue;
			}
			float d = Vector3.Distance(me, p);
			if (d < bestDist)
			{
				bestDist = d;
				best = bird;
			}
		}
		if (best == null)
		{
			return false;
		}
		if (WeaponLogic.FindReadyBow(_humanoid, null) == null)
		{
			_noBowUntil = Time.time + 60f;
			if (CompanionsPlugin.LogDetailed) LogDebug("A bird has landed nearby, but there's no bow or crossbow with ammo to shoot it");
			return false;
		}
		_bird = best;
		_birdName = BirdPrefab(best);
		_birdPos = best.transform.position;
		_birdBefore = MHR_Loot.ItemsAround(_birdPos);
		_birdSince = Time.time;
		_state = HuntState.Birding;
		MHR_Mover.Release(_ai);
		if (CompanionsPlugin.LogNormal) Log("Going after a " + _birdName + " that landed " + bestDist.ToString("F0") + " m away" + NeedText(_birdName));
		CompanionStatus.Doing(this, "hc_doing_hunt", MHR_Items.NiceName(best.gameObject));
		return true;
	}

	private void UpdateBirding(bool follow)
	{
		if (_combatAI.IsEngaged)
		{
			EndBird();
			return;
		}
		ZNetView nv = (_bird != null) ? _bird.GetComponent<ZNetView>() : null;
		if (_bird == null || nv == null || !nv.IsValid())
		{
			if (CompanionsPlugin.LogDetailed) LogDebug("Got the " + _birdName);
			MHR_Loot.OnKillAt(_setup, _birdPos, _birdName, _birdBefore);
			EndBird();
			_nextCount = 0f;
			return;
		}
		_birdPos = _bird.transform.position;
		if (!Landed(_bird) || Time.time - _birdSince > 30f)
		{
			if (CompanionsPlugin.LogDetailed) LogDebug("The " + _birdName + (Landed(_bird) ? " - taking too long" : " flew off"));
			_skip[_bird.GetInstanceID()] = Time.time + 30f;
			EndBird();
			return;
		}
		ItemDrop.ItemData weapon = _humanoid.GetCurrentWeapon();
		if (weapon == null || weapon.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Bow && weapon.m_shared.m_aiAttackRange <= 10f)
		{
			ItemDrop.ItemData bow = WeaponLogic.FindReadyBow(_humanoid, null);
			if (bow == null)
			{
				_noBowUntil = Time.time + 60f;
				EndBird();
				return;
			}
			// 1.4.150: keep auto-equip from swapping the bow back to the melee weapon while it stalks the bird
			if (_setup != null)
			{
				_setup.SuppressAutoEquip = true;
				_birdBowOut = true;
			}
			_humanoid.EquipItem(bow);
		}
		Vector3 me = base.transform.position;
		Vector3 aim = _birdPos + Vector3.up * 0.25f;
		float dist = Vector3.Distance(me, _birdPos);
		// 1.5.42: a clear line from the bow (hand height) to the bird - CanSee adds 1.6 m to both ends, so the old check
		// (me + 1.6) looked from 3.2 m up and passed over the hill the arrow then hit
		if (dist > BirdShotRange || !MHR_Mover.CanSee(me - Vector3.up * 0.2f, aim - Vector3.up * 1.6f))
		{
			// get within a shot quickly (birds only stay down a few seconds); never swim for one (1.4.101 test: dived into
			// the moat after a gull)
			MHR_Mover.Go(_ai, Time.deltaTime, _birdPos, BirdShotRange * 0.8f, true, "Hunt", MHR_Mover.SpeedJog);
			if (MHR_Mover.GaveUp(_ai) || MHR_Mover.RouteSwim(_ai) > MHR_Mover.JobSwim())
			{
				if (CompanionsPlugin.LogDetailed) LogDebug("Can't get a shot at the " + _birdName + (MHR_Mover.RouteSwim(_ai) > MHR_Mover.JobSwim() ? " without a long swim" : "") + " - leaving it");
				_skip[_bird.GetInstanceID()] = Time.time + 120f;
				EndBird();
			}
			return;
		}
		_ai.StopMoving();
		_ai.LookAtPoint(aim);
		bool drawBow = MHR_BowDraw.NeedsDraw(_humanoid);
		// 1.5.61/62: a bow is drawn while turning to the bird and loosed once lined up (MHR_BowDraw; combat's Update runs the
		// draw and lets go of one nobody keeps wanting)
		if (Time.time < _nextShot || (_character.InAttack() && !MHR_BowDraw.IsDrawing(_humanoid)) || !_ai.IsLookingAtPoint(aim, drawBow ? 45f : 3f))
		{
			return;
		}
		CompanionProjectileAimPatch.AimAt(_humanoid, aim);
		bool shot;
		if (drawBow)
		{
			shot = MHR_BowDraw.Tick(_humanoid, null, _ai.IsLookingAtPoint(aim, 3f), clear: true);
			if (!shot)
			{
				return;
			}
		}
		else
		{
			shot = _character.StartAttack(null, false);
		}
		_nextShot = Time.time + 1.5f;
		if (CompanionsPlugin.LogDetailed) LogDebug((shot ? "Shooting at the " : "Couldn't shoot at the ") + _birdName + " (" + dist.ToString("F0") + " m, " + (_humanoid.GetCurrentWeapon()?.m_shared?.m_name ?? "no weapon") + ")");
	}

	private bool _birdBowOut;

	private void ReleaseBirdBow()
	{
		if (_birdBowOut && _setup != null)
		{
			_birdBowOut = false;
			_setup.SuppressAutoEquip = false;
			_setup.SyncEquipmentToInventory();
		}
	}

	private void EndBird()
	{
		ReleaseBirdBow();
		_ai.MHR_SetCrouch(false);
		_ai.StopMoving();
		MHR_Mover.Release(_ai);
		_bird = null;
		_state = HuntState.Idle;
		_scanTimer = 1f;
	}

	private static bool Landed(RandomFlyingBird bird)
	{
		Vector3 p = bird.transform.position;
		return p.y - MHR_Water.GroundAt(p) < 0.8f;
	}

	private static string BirdPrefab(RandomFlyingBird bird)
	{
		ZNetView nv = bird.GetComponent<ZNetView>();
		ZDO zdo = (nv != null && nv.IsValid()) ? nv.GetZDO() : null;
		GameObject prefab = (zdo != null && ZNetScene.instance != null) ? ZNetScene.instance.GetPrefab(zdo.GetPrefab()) : null;
		if (prefab != null)
		{
			return prefab.name;
		}
		string text = bird.gameObject.name;
		int num = text.IndexOf("(Clone)", StringComparison.Ordinal);
		return (num >= 0) ? text.Substring(0, num).Trim() : text;
	}

	private bool NeedsBirdDrop(string prefab)
	{
		return NeedsDrop(prefab, ModConfig.HuntFeatherTarget?.Value ?? 100);
	}

	private void UpdateGoingHome(bool follow)
	{
		// 1.4.150: attacked on the way home - the fight comes first (both steered: it stuttered and barely fought back)
		if (_combatAI.IsEngaged)
		{
			return;
		}
		if (follow || !_setup.HasHomePosition())
		{
			Stop();
			return;
		}
		if (MHR_ToolKeeper.AtHome(_setup))
		{
			_nextUnloadTry = Time.time + 60f;
			if (CompanionsPlugin.LogNormal) Log("Home - putting the catch away");
			MHR_ToolKeeper.RequestUnload(_setup);
			_nextCount = 0f;
			Stop();
			return;
		}
		MHR_Mover.Go(_ai, Time.deltaTime, _setup.GetHomePosition(), 4f, true, "Hunt");
		if (MHR_Mover.GaveUp(_ai))
		{
			if (CompanionsPlugin.LogNormal) Log("Can't find the way home with the catch - trying again later");
			_nextUnloadTry = Time.time + 60f;
			Stop();
		}
	}

	private bool PackFull()
	{
		Inventory inventory = _humanoid?.GetInventory();
		if (inventory == null)
		{
			return false;
		}
		float full = ModConfig.HuntBagFull?.Value ?? 0.9f;
		return inventory.GetEmptySlots() == 0 || inventory.GetTotalWeight() >= CompanionCarry.Max(inventory) * full;
	}

	private void Skip(Character c, float seconds)
	{
		if (c == null)
		{
			return;
		}
		if (_skip.Count > 100)
		{
			_skip.Clear();
		}
		_skip[c.GetInstanceID()] = Time.time + seconds;
	}

	private Character FindPrey(bool follow, out string why)
	{
		why = null;
		Vector3 center = Center(follow);
		float range = Range(follow);
		Vector3 me = base.transform.position;
		float reach = follow ? (range + 10f) : (range + (ModConfig.HuntChaseExtra?.Value ?? 15f));
		RefreshCounts(follow);
		Character best = null;
		float bestDist = float.MaxValue;
		int seen = 0;
		int enough = 0;
		_whyItems.Clear();
		HashSet<string> told = new HashSet<string>();
		foreach (Character c in Character.GetAllCharacters())
		{
			if (c == null || c == _character || c.GetHealth() <= 0f || c.IsDead())
			{
				continue;
			}
			if (Vector3.Distance(c.transform.position, center) > range || Vector3.Distance(c.transform.position, me) > reach)
			{
				continue;
			}
			if (_skip.TryGetValue(c.GetInstanceID(), out var until) && Time.time < until)
			{
				continue;
			}
			string prefab = PrefabName(c);
			if (!HuntCatalog.CanHunt(c, prefab))
			{
				continue;
			}
			seen++;
			if (!NeedsMeat(prefab))
			{
				enough++;
				// what it has enough of, with the numbers ("Raw meat 104/100 - enough.")
				List<string> meats = HuntCatalog.MeatsOf(prefab);
				if (meats != null && _whyItems.Count < 8)
				{
					foreach (string meat in meats)
					{
						if (told.Add(meat))
						{
							_meatCount.TryGetValue(meat, out var have);
							_whyItems.Add(new CompanionStatus.Item(null, "hc_why_hunt_at_target", CookController.Name(meat), have, HuntCatalog.Target(prefab, meat, ModConfig.HuntMeatTarget?.Value ?? 100)));
						}
					}
				}
				continue;
			}
			if (MHR_Water.WouldSwim(_character, c.transform.position))
			{
				continue;
			}
			float d = Vector3.Distance(me, c.transform.position);
			if (d < bestDist)
			{
				bestDist = d;
				best = c;
			}
		}
		if (best == null)
		{
			why = seen == 0 ? "no game within " + range.ToString("F0") + " m" : (enough == seen ? "enough meat in store" : "only out of reach");
		}
		return best;
	}

	// 1.5.43: why there's nothing to hunt, filled by FindPrey
	private readonly List<CompanionStatus.Item> _whyItems = new List<CompanionStatus.Item>();

	private bool NeedsMeat(string prefab)
	{
		return NeedsDrop(prefab, ModConfig.HuntMeatTarget?.Value ?? 100);
	}

	private bool NeedsDrop(string prefab, int dflt)
	{
		List<string> meats = HuntCatalog.MeatsOf(prefab);
		if (meats == null || meats.Count == 0)
		{
			return false;
		}
		foreach (string meat in meats)
		{
			int target = HuntCatalog.Target(prefab, meat, dflt);
			if (target <= 0 || !_meatCount.TryGetValue(meat, out var have) || have < target)
			{
				return true;
			}
		}
		return false;
	}

	private string NeedText(string prefab)
	{
		int dflt = HuntCatalog.CanHuntBird(prefab) ? (ModConfig.HuntFeatherTarget?.Value ?? 100) : (ModConfig.HuntMeatTarget?.Value ?? 100);
		List<string> meats = HuntCatalog.MeatsOf(prefab);
		if (meats == null)
		{
			return "";
		}
		StringBuilder sb = new StringBuilder();
		foreach (string meat in meats)
		{
			int target = HuntCatalog.Target(prefab, meat, dflt);
			_meatCount.TryGetValue(meat, out var have);
			sb.Append(sb.Length == 0 ? " - " : ", ").Append(CookController.Name(meat)).Append(' ').Append(have).Append('/').Append(target > 0 ? target.ToString() : "no limit");
		}
		return sb.ToString();
	}

	// the meat in the pack plus the chests near home (or around the hunting spot without a home)
	private void RefreshCounts(bool follow)
	{
		if (Time.time < _nextCount)
		{
			return;
		}
		_nextCount = Time.time + 15f;
		_meatCount.Clear();
		Inventory inventory = _humanoid?.GetInventory();
		if (inventory != null)
		{
			Add(inventory);
		}
		Vector3 center;
		float radius;
		if (_setup.HasHomePosition())
		{
			center = _setup.GetHomePosition();
			radius = MHR_ToolKeeper.HomeRange();
		}
		else
		{
			center = follow ? base.transform.position : Center(follow);
			radius = 25f;
		}
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
		foreach (ItemDrop.ItemData item in inv.GetAllItems())
		{
			string name = item?.m_dropPrefab?.name;
			if (name != null && HuntCatalog.IsMeat(name))
			{
				_meatCount.TryGetValue(name, out var n);
				_meatCount[name] = n + item.m_stack;
			}
		}
	}

	internal static string PrefabName(Character c)
	{
		ZNetView nv = c.GetComponent<ZNetView>();
		ZDO zdo = (nv != null && nv.IsValid()) ? nv.GetZDO() : null;
		GameObject prefab = (zdo != null && ZNetScene.instance != null) ? ZNetScene.instance.GetPrefab(zdo.GetPrefab()) : null;
		if (prefab != null)
		{
			return prefab.name;
		}
		string text = c.gameObject.name;
		int num = text.IndexOf("(Clone)", StringComparison.Ordinal);
		return (num >= 0) ? text.Substring(0, num).Trim() : text;
	}

	private static string Loc(string s)
	{
		try
		{
			return (Localization.instance != null) ? Localization.instance.Localize(s) : s;
		}
		catch
		{
			return s;
		}
	}

	private static void Log(string msg)
	{
		if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log?.LogInfo("[Hunt] " + msg);
	}

	private static void LogDebug(string msg)
	{
		if (CompanionsPlugin.LogDetailed) CompanionsPlugin.Log?.LogDebug("[Hunt] " + msg);
	}
}

// 1.4.94: which creatures can be hunted and the meat each drops, read from the game's own data (mods included)
internal static class HuntCatalog
{
	private static readonly Dictionary<string, List<string>> s_meatsOf = new Dictionary<string, List<string>>();

	private static readonly HashSet<string> s_meats = new HashSet<string>();

	private static bool s_built;

	private static readonly HashSet<string> s_allow = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	private static readonly HashSet<string> s_deny = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	internal static void Ensure()
	{
		if (s_built || ZNetScene.instance == null || ObjectDB.instance == null || ObjectDB.instance.m_recipes == null || ObjectDB.instance.m_recipes.Count == 0)
		{
			return;
		}
		s_built = true;
		try
		{
			CookCatalog.Ensure();
			foreach (GameObject prefab in ZNetScene.instance.m_prefabs)
			{
				if (prefab == null)
				{
					continue;
				}
				Character character = prefab.GetComponent<Character>();
				CharacterDrop drop = prefab.GetComponent<CharacterDrop>();
				if (character == null || drop == null || drop.m_drops == null || character.IsPlayer() || character.m_boss || prefab.GetComponent<CompanionSetup>() != null)
				{
					continue;
				}
				List<string> meats = null;
				foreach (CharacterDrop.Drop d in drop.m_drops)
				{
					string name = d?.m_prefab?.name;
					if (name != null && LooksLikeMeat(name))
					{
						if (meats == null)
						{
							meats = new List<string>();
						}
						if (!meats.Contains(name))
						{
							meats.Add(name);
						}
						s_meats.Add(name);
					}
				}
				if (meats != null)
				{
					s_meatsOf[prefab.name] = meats;
				}
			}
			// 1.4.100: birds (crows, gulls, modded ones) - what they drop when shot (feathers)
			foreach (GameObject prefab in ZNetScene.instance.m_prefabs)
			{
				if (prefab == null || prefab.GetComponent<RandomFlyingBird>() == null)
				{
					continue;
				}
				DropOnDestroyed dod = prefab.GetComponent<DropOnDestroyed>();
				if (dod?.m_dropWhenDestroyed?.m_drops == null)
				{
					continue;
				}
				List<string> drops = new List<string>();
				foreach (DropTable.DropData d in dod.m_dropWhenDestroyed.m_drops)
				{
					if (d.m_item != null && !drops.Contains(d.m_item.name))
					{
						drops.Add(d.m_item.name);
						s_meats.Add(d.m_item.name);
					}
				}
				if (drops.Count > 0)
				{
					s_meatsOf[prefab.name] = drops;
					s_birds.Add(prefab.name);
				}
			}
			WriteReference();
			if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log.LogInfo($"[Hunt] {s_meatsOf.Count - s_birds.Count} creatures drop meat, {s_birds.Count} kinds of bird ({s_meats.Count} kinds of drops counted)");
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log.LogWarning("[Hunt] couldn't read the creatures: " + ex.Message);
		}
	}

	// meat: anything a cooking station cooks (NeckTail and the like) or named ...Meat (modded meats)
	private static bool LooksLikeMeat(string item)
	{
		return CookCatalog.IsGrillInput(item) || item.IndexOf("meat", StringComparison.OrdinalIgnoreCase) >= 0;
	}

	private static readonly HashSet<string> s_birds = new HashSet<string>();

	internal static bool CanHuntBird(string prefab)
	{
		Ensure();
		return s_birds.Contains(prefab) && Allowed(prefab, prefab);
	}

	internal static bool IsMeat(string item)
	{
		Ensure();
		return s_meats.Contains(item);
	}

	internal static List<string> MeatsOf(string prefab)
	{
		Ensure();
		return s_meatsOf.TryGetValue(prefab, out var value) ? value : null;
	}

	internal static bool CanHunt(Character c, string prefab)
	{
		Ensure();
		if (c.IsPlayer() || c.IsTamed() || c.IsBoss() || c.GetComponent<CompanionSetup>() != null || !s_meatsOf.ContainsKey(prefab) || s_birds.Contains(prefab))
		{
			return false;
		}
		return Allowed(prefab, c.m_name);
	}

	// 1.4.106: the shared list format (MHR_List): *, Name, -Name, Name:Number (the creature's meats - or that meat, or
	// that bird's drops - get their own target instead of MeatTarget / FeatherTarget)
	private static bool Allowed(string prefab, string token)
	{
		return MHR_List.Get(ModConfig.HuntPrey?.Value ?? "*").Allows(prefab, MHR_List.Local(token));
	}

	internal static int Target(string prefab, string item, int dflt)
	{
		MHR_List l = MHR_List.Get(ModConfig.HuntPrey?.Value ?? "*");
		if (l.HasAmount(item))
		{
			return l.AmountFor(item, dflt);
		}
		return l.AmountFor(prefab, dflt);
	}

	private static void WriteReference()
	{
		try
		{
			StringBuilder sb = new StringBuilder();
			sb.AppendLine(CompanionsPlugin.PluginName + " - names for the [Hunting] settings. Written at every start from the game's own data, mods included.");
			sb.AppendLine();
			sb.AppendLine("PREY (Hunting > Prey)  name = in-game name  (meat it drops)");
			sb.AppendLine("Tamed animals, bosses and companions are never hunted.");
			SortedDictionary<string, string> lines = new SortedDictionary<string, string>();
			foreach (KeyValuePair<string, List<string>> kv in s_meatsOf)
			{
				GameObject prefab = ZNetScene.instance.GetPrefab(kv.Key);
				Character c = prefab != null ? prefab.GetComponent<Character>() : null;
				bool bird = s_birds.Contains(kv.Key);
				List<string> names = new List<string>();
				foreach (string meat in kv.Value)
				{
					names.Add(meat + " = " + CookController.Name(meat));
				}
				lines[kv.Key] = kv.Key + " = " + (c != null ? CookController.Loc(c.m_name) : kv.Key) + (bird ? " (bird, needs Hunting > Birds)" : "") + "  (" + string.Join(", ", names.ToArray()) + ")";
			}
			foreach (string line in lines.Values)
			{
				sb.AppendLine("  " + line);
			}
			File.WriteAllText(Path.Combine(Paths.ConfigPath, "OfflineCompanionsReborn.Hunting.txt"), sb.ToString());
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log.LogWarning("[Hunt] couldn't write OfflineCompanionsReborn.Hunting.txt: " + ex.Message);
		}
	}
}
