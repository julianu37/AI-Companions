using System.Collections.Generic;
using UnityEngine;

namespace Companions;

// 1.4.143: what a companion does when it has nothing to do (user: "It looked like the NPC was stuck but there were not.
// We should add some sort of idle"). Agreed with the user: near its job (so it's right there when work comes up again),
// all four activities - small chores, sitting, wandering about, socializing - says now and then why it's idle, global
// [Idle] settings. Starts after its job has had nothing to do for a while; stops the moment the job has work again,
// a fight starts, it's sent somewhere, it goes to bed or it follows you.
public class IdleController : MonoBehaviour
{
	private enum Act
	{
		Off,
		Pause,
		Chore,
		ToSeat,
		Seated,
		Wander,
		Social
	}

	private ZNetView _nview;

	private CompanionAI _ai;

	private CompanionSetup _setup;

	private Character _character;

	private CompanionTalk _talk;

	private CompanionRest _rest;

	private HarvestController _harvest;

	private SmeltController _smelt;

	private FarmController _farm;

	private CookController _cook;

	private HuntController _hunt;

	private FishController _fish;

	private UpkeepController _upkeep;

	private RepairController _gearRepair;

	private DoorHandler _door;

	private Act _act;

	private float _eligibleSince = -1f;

	private Vector3 _center;

	private float _until;

	private float _nextChoreTry;

	private Chair _chair;

	private Vector3 _goal;

	private Vector3 _look;

	private Character _buddy;

	private bool _hasGoal;

	private float _lastWhy = -9999f;

	private float _nextChat;

	private static readonly List<Piece> s_pieces = new List<Piece>();

	public bool IsActive => _act != Act.Off;

	private static bool On => ModConfig.IdleEnabled == null || ModConfig.IdleEnabled.Value;

	private static float Radius => ModConfig.IdleRadius?.Value ?? 8f;

	private void Awake()
	{
		_nview = GetComponent<ZNetView>();
		_ai = GetComponent<CompanionAI>();
		_setup = GetComponent<CompanionSetup>();
		_character = GetComponent<Character>();
		_talk = GetComponent<CompanionTalk>();
		_rest = GetComponent<CompanionRest>();
		_harvest = GetComponent<HarvestController>();
		_smelt = GetComponent<SmeltController>();
		_farm = GetComponent<FarmController>();
		_cook = GetComponent<CookController>();
		_hunt = GetComponent<HuntController>();
		_fish = GetComponent<FishController>();
		_upkeep = GetComponent<UpkeepController>();
		_gearRepair = GetComponent<RepairController>();
		_door = GetComponent<DoorHandler>();
	}

	private int Mode()
	{
		return (_nview?.GetZDO())?.GetInt(CompanionSetup.ActionModeHash) ?? 0;
	}

	// ------------------------------------------------------------------ when

	// nothing else wants the companion right now
	private bool Free(out string why)
	{
		why = null;
		if (!On)
		{
			why = "idle behaviour off";
			return false;
		}
		int mode = Mode();
		if (mode == CompanionSetup.ModeStay)
		{
			why = "told to stay";
			return false;
		}
		if (_ai.GetFollowTarget() != null || (_setup != null && _setup.GetFollow()))
		{
			why = "following";
			return false;
		}
		if (_ai.IsInCombat || _character.IsSwimming() || _ai.IsOnShip)
		{
			why = "busy";
			return false;
		}
		if (MHR_JobRules.OrderPending(this))
		{
			why = "an order";
			return false;
		}
		if (MHR_ToolKeeper.IsBusy(this) || MHR_Loot.Active(this))
		{
			why = "a trip";
			return false;
		}
		if (_rest != null && (_rest.IsSleeping || _rest.IsNavigating || _rest.IsResting))
		{
			why = "resting";
			return false;
		}
		if (CompanionInteractPanel.IsOpenFor(_setup) || CompanionRadialMenu.IsOpenFor(_setup))
		{
			why = "being talked to";
			return false;
		}
		bool chore = _act == Act.Chore && _upkeep != null && _upkeep.IdleChoreActive;
		if ((_harvest != null && _harvest.IsActive) || (_smelt != null && _smelt.IsActive) || (_farm != null && _farm.IsActive) || (_cook != null && _cook.IsActive) || (_hunt != null && _hunt.IsActive) || (_fish != null && _fish.IsActive) || (_gearRepair != null && _gearRepair.IsActive) || (_door != null && _door.IsActive) || (!chore && _upkeep != null && _upkeep.IsActive))
		{
			why = "the job has work";
			return false;
		}
		return true;
	}

	private void Update()
	{
		if (_nview == null || !_nview.IsValid() || !_nview.IsOwner() || _ai == null || _character == null || _character.IsDead())
		{
			return;
		}
		string why;
		if (!Free(out why))
		{
			_eligibleSince = -1f;
			if (_act != Act.Off)
			{
				Stop(why);
			}
			return;
		}
		if (_act == Act.Off)
		{
			if (_eligibleSince < 0f)
			{
				_eligibleSince = Time.time;
			}
			if (Time.time - _eligibleSince < (ModConfig.IdleStartAfter?.Value ?? 10f))
			{
				return;
			}
			Begin();
			return;
		}
		switch (_act)
		{
		case Act.Pause:
			UpdatePause();
			break;
		case Act.Chore:
			if (_upkeep == null || !_upkeep.IdleChoreActive)
			{
				Pause(3f, 6f);
			}
			break;
		case Act.ToSeat:
			UpdateToSeat();
			break;
		case Act.Seated:
			if (!MHR_Follow.IdleSeated(_ai) || Time.time >= _until)
			{
				MHR_Follow.IdleStand(_ai, "had a rest");
				Pause(2f, 5f);
			}
			break;
		case Act.Wander:
			UpdateWander();
			break;
		case Act.Social:
			UpdateSocial();
			break;
		}
	}

	private void Begin()
	{
		// near the job: where the job left it; with no job, where it is - home only when it's actually at home.
		// 1.5.3 (user: "The NPC keeps running off when there is nothing to do"): "near home" was HomeTripRange (60 m), so
		// after being sent somewhere 30 m from home it ran back there to idle. Within 15 m of home only.
		_center = base.transform.position;
		if (Mode() == 0 && _setup != null && _setup.HasHomePosition() && Vector3.Distance(_setup.GetHomePosition(), base.transform.position) <= 15f)
		{
			_center = _setup.GetHomePosition();
		}
		if (CompanionsPlugin.LogNormal) Log("Nothing to do - idling around " + MHR_Mover.Fmt(_center));
		SayWhy();
		_nextChoreTry = 0f;
		Pause(1f, 3f);
	}

	private void Stop(string why)
	{
		if (_act == Act.Chore && _upkeep != null)
		{
			_upkeep.StopIdleChore();
		}
		if (_act == Act.Seated || _act == Act.ToSeat)
		{
			MHR_Follow.IdleStand(_ai, why);
		}
		if (_act != Act.Pause && _act != Act.Chore)
		{
			_ai.StopMoving();
			MHR_Mover.Release(_ai);
		}
		if (CompanionsPlugin.LogDetailed) LogDebug("Stopped idling (" + why + ")");
		_act = Act.Off;
		_chair = null;
		_buddy = null;
		_hasGoal = false;
	}

	// ------------------------------------------------------------------ what

	private void Pause(float min, float max)
	{
		_act = Act.Pause;
		_until = Time.time + Random.Range(min, max);
		_ai.StopMoving();
		MHR_Mover.Release(_ai);
		// look around while standing
		Vector2 r = Random.insideUnitCircle.normalized * 4f;
		_look = base.transform.position + new Vector3(r.x, 1.2f, r.y);
	}

	private void UpdatePause()
	{
		_ai.StopMoving();
		_ai.LookAtPoint(_look);
		if (Time.time < _until)
		{
			return;
		}
		Next();
	}

	private void Next()
	{
		// chores first - a little useful work beats standing about
		if ((ModConfig.IdleChores == null || ModConfig.IdleChores.Value) && _upkeep != null && Time.time >= _nextChoreTry)
		{
			_nextChoreTry = Time.time + 20f;
			if (_upkeep.StartIdleChore(_center, ModConfig.IdleChoreRadius?.Value ?? 15f))
			{
				_act = Act.Chore;
				return;
			}
		}
		List<Act> pick = new List<Act>();
		if (ModConfig.IdleWander == null || ModConfig.IdleWander.Value)
		{
			pick.Add(Act.Wander);
			pick.Add(Act.Wander);
		}
		if (ModConfig.IdleSit == null || ModConfig.IdleSit.Value)
		{
			pick.Add(Act.ToSeat);
		}
		if ((ModConfig.IdleSocialize == null || ModConfig.IdleSocialize.Value) && FindBuddy() != null)
		{
			pick.Add(Act.Social);
		}
		if (pick.Count == 0)
		{
			Pause(5f, 10f);
			return;
		}
		Act a = pick[Random.Range(0, pick.Count)];
		bool started = a == Act.ToSeat ? StartSit() : a == Act.Social ? StartSocial() : StartWander();
		if (!started)
		{
			Pause(3f, 6f);
		}
	}

	// ---- sitting: a free chair or bench nearby, else the ground ----

	private bool StartSit()
	{
		_chair = MHR_Follow.IdleFindChair(_ai, _center, Radius);
		_until = Time.time + Random.Range(20f, 60f);
		if (_chair == null)
		{
			MHR_Follow.IdleSitFloor(_ai, base.transform.position + base.transform.forward);
			if (CompanionsPlugin.LogDetailed) LogDebug("Sitting down for a bit");
			_act = Act.Seated;
			return true;
		}
		if (CompanionsPlugin.LogDetailed) LogDebug("Going to sit on the " + _chair.m_name);
		_act = Act.ToSeat;
		_goal = _chair.m_attachPoint.position;
		_until = Time.time + 20f;   // to get there
		return true;
	}

	private void UpdateToSeat()
	{
		if (_chair == null || _chair.m_attachPoint == null || Time.time >= _until)
		{
			MHR_Follow.IdleStand(_ai, "couldn't get to the seat");
			Pause(2f, 4f);
			return;
		}
		MHR_Follow.IdleClaimChair(_ai, _chair);
		bool there = MHR_Mover.Go(_ai, Time.deltaTime, _chair.m_attachPoint.position, 0.8f, false, "Idle", MHR_Mover.SpeedWalk);
		if (MHR_Mover.GaveUp(_ai))
		{
			MHR_Follow.IdleStand(_ai, "couldn't get to the seat");
			Pause(2f, 4f);
			return;
		}
		if (there)
		{
			MHR_Follow.IdleSit(_ai, _chair);
			_act = Act.Seated;
			_until = Time.time + Random.Range(20f, 60f);
		}
	}

	// ---- wandering: a stroll, sometimes over to a station or chest to "check on it" ----

	private bool StartWander()
	{
		Vector3 me = base.transform.position;
		if (Random.value < 0.4f && PickStation(out _goal, out _look))
		{
			_hasGoal = true;
		}
		else
		{
			_hasGoal = false;
			for (int i = 0; i < 8 && !_hasGoal; i++)
			{
				Vector2 r = Random.insideUnitCircle * Radius;
				Vector3 p = _center + new Vector3(r.x, 0f, r.y);
				RaycastHit hit;
				if (!Physics.Raycast(p + Vector3.up * 4f, Vector3.down, out hit, 10f, LayerMask.GetMask("Default", "static_solid", "piece", "terrain")) || hit.normal.y < 0.6f)
				{
					continue;
				}
				p = hit.point;
				if (Mathf.Abs(p.y - me.y) > 3f || MHR_Water.WouldSwim(_character, p) || MHR_Mover.NearFire(p, 1.2f) || Vector3.Distance(p, me) < 2f || !MHR_Mover.CanGetTo(me, p))
				{
					continue;
				}
				_goal = p;
				Vector3 d = p - me;
				d.y = 0f;
				_look = p + d.normalized * 3f + Vector3.up * 1.2f;
				_hasGoal = true;
			}
		}
		if (!_hasGoal)
		{
			return false;
		}
		_act = Act.Wander;
		_until = Time.time + 25f;
		return true;
	}

	private bool PickStation(out Vector3 stand, out Vector3 look)
	{
		stand = look = Vector3.zero;
		s_pieces.Clear();
		Piece.GetAllPiecesInRadius(_center, Radius, s_pieces);
		List<Component> things = new List<Component>();
		foreach (Piece p in s_pieces)
		{
			if (p == null)
			{
				continue;
			}
			Component c = (Component)p.GetComponent<CraftingStation>() ?? (Component)p.GetComponent<Smelter>() ?? (Component)p.GetComponent<Fireplace>() ?? (Component)p.GetComponent<CookingStation>() ?? (Component)p.GetComponent<Fermenter>() ?? (Component)p.GetComponent<Beehive>() ?? (Component)p.GetComponentInChildren<Container>();
			if (c != null)
			{
				things.Add(c);
			}
		}
		for (int i = 0; i < 4 && things.Count > 0; i++)
		{
			Component c = things[Random.Range(0, things.Count)];
			things.Remove(c);
			Vector3 spot;
			if (MHR_Chests.PickApproachPiece(base.transform.position, c, out spot))
			{
				stand = spot;
				look = c.transform.position + Vector3.up * 0.8f;
				return true;
			}
		}
		return false;
	}

	private void UpdateWander()
	{
		bool there = MHR_Mover.Go(_ai, Time.deltaTime, _goal, 0.8f, false, "Idle", MHR_Mover.SpeedWalk);
		if (there)
		{
			Pause(4f, 9f);
			_look = _look == Vector3.zero ? base.transform.position + base.transform.forward * 3f : _look;
			return;
		}
		if (MHR_Mover.GaveUp(_ai) || Time.time >= _until)
		{
			Pause(2f, 4f);
		}
	}

	// ---- socializing: over to you or another companion nearby, a word now and then ----

	private Character FindBuddy()
	{
		Vector3 me = base.transform.position;
		float reach = Radius * 2f;
		Player p = MHR_Owner.Of(this);
		if (p != null && !p.IsDead() && Vector3.Distance(me, p.transform.position) <= reach && Mathf.Abs(p.transform.position.y - me.y) < 3f)
		{
			return p;
		}
		IReadOnlyList<CompanionSetup> all = CompanionSetup.AllCompanions;
		if (all != null)
		{
			foreach (CompanionSetup cs in all)
			{
				if (cs == null || cs == _setup)
				{
					continue;
				}
				Character c = cs.GetComponent<Character>();
				if (c != null && !c.IsDead() && Vector3.Distance(me, c.transform.position) <= reach && Mathf.Abs(c.transform.position.y - me.y) < 3f)
				{
					return c;
				}
			}
		}
		return null;
	}

	private bool StartSocial()
	{
		_buddy = FindBuddy();
		if (_buddy == null)
		{
			return false;
		}
		if (CompanionsPlugin.LogDetailed) LogDebug("Going over to " + _buddy.m_name);
		_act = Act.Social;
		_until = Time.time + Random.Range(15f, 30f);
		return true;
	}

	private void UpdateSocial()
	{
		// 1.4.150: not off after someone walking away - only company within reach of where it idles
		if (_buddy == null || _buddy.IsDead() || Time.time >= _until || Vector3.Distance(_center, _buddy.transform.position) > Radius * 2f)
		{
			Pause(2f, 5f);
			return;
		}
		Vector3 at = _buddy.transform.position;
		float d = Vector3.Distance(base.transform.position, at);
		if (d > 2.5f)
		{
			MHR_Mover.Go(_ai, Time.deltaTime, at, 2.0f, false, "Idle", MHR_Mover.SpeedWalk);
			if (MHR_Mover.GaveUp(_ai))
			{
				Pause(2f, 4f);
			}
			return;
		}
		_ai.StopMoving();
		MHR_Mover.Release(_ai);
		_ai.LookAtPoint(at + Vector3.up * 1.5f);
		float every = ModConfig.IdleSpeechInterval?.Value ?? 300f;
		if (every > 0f && Time.time >= _nextChat)
		{
			_nextChat = Time.time + Mathf.Max(60f, every * 0.5f);
			if (Random.value < 0.6f)
			{
				// 1.5.41: chat that fits (no "fine day" at night), from speech.json
				if (_talk != null)
				{
					_talk.Speak(_talk.Pick(SpeechConfig.Instance.Chat), CompanionTalk.Priority.Ambient, null);
				}
			}
		}
	}

	// ------------------------------------------------------------------ speech, log

	// why it's idle, said when it starts idling - not more often than [Idle] SpeechInterval
	private void SayWhy()
	{
		float every = ModConfig.IdleSpeechInterval?.Value ?? 300f;
		if (every <= 0f || Time.time - _lastWhy < every || _talk == null)
		{
			return;
		}
		// 1.5.42: the real reason its job has stopped, when the job recorded one (CompanionStatus); else the job's general line
		string line = CompanionStatus.SpokenReason(this);
		if (string.IsNullOrEmpty(line))
		{
			string key = "hc_idle_why_" + Mode();
			line = ModLocalization.Loc(key);
			if (string.IsNullOrEmpty(line) || line == key || line.StartsWith("["))
			{
				return;
			}
		}
		_lastWhy = Time.time;
		_talk.Say(line);
	}

	private void Log(string msg)
	{
		if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log?.LogInfo("[Idle|" + (_character != null ? _character.m_name : "?") + "] " + msg);
	}

	private void LogDebug(string msg)
	{
		if (CompanionsPlugin.LogDetailed) CompanionsPlugin.Log?.LogDebug("[Idle|" + (_character != null ? _character.m_name : "?") + "] " + msg);
	}
}
