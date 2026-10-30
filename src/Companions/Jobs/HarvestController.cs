using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.AI;

namespace Companions;

public class HarvestController : MonoBehaviour
{
	private enum HarvestState
	{
		Idle,
		Moving,
		Attacking,
		CollectingDrops
	}

	private ZNetView _nview;

	private CompanionAI _ai;

	private Humanoid _humanoid;

	private Character _character;

	private CompanionSetup _setup;

	private DoorHandler _doorHandler;

	private CompanionStamina _stamina;

	private CompanionTalk _talk;

	private HarvestState _state;

	internal GameObject _target;

	private float _scanTimer;

	private float _attackTimer;

	private float _moveTimer;

	internal bool _toolEquipped;

	internal string _targetType;

	private int _swingCount;

	private int _totalAttempts;

	private int _consecutiveFailures;

	private int _rock5ActiveSections = -1;

	private Collider _rock5CurrentSegment;

	private bool _reapproach;

	private bool _shouldRun;

	private Vector3 _lockedMoveTarget;

	private bool _moveTargetLocked;

	internal Vector3 _attackNodePos;

	private Vector3 _lockedAttackFaceTarget;

	private bool _attackFaceLocked;

	private int _claimedTargetId;

	private int _lastAoeSwing = -1;

	private static readonly HashSet<int> s_claimedTargets = new HashSet<int>();

	internal static bool IsApplyingAoeDamage;

	private ItemDrop.ItemData _pendingToolReequip;

	private GameObject _directedTarget;

	private int _directedMode;

	private bool _isDirectedHarvest;

	internal readonly Dictionary<int, float> _blacklist = new Dictionary<int, float>();

	private static HashSet<string> _forageFilter;

	private static string _forageFilterRaw;

	private const HarvestState State_Idle = HarvestState.Idle;

	private const HarvestState State_Moving = HarvestState.Moving;

	private const HarvestState State_Attacking = HarvestState.Attacking;

	private const HarvestState State_CollectingDrops = HarvestState.CollectingDrops;

	private const float AttackRetry = 0.25f;

	private const float MoveTimeout = 10f;

	private const float ArrivalSlack = 0.5f;

	private const int WhiffRetryMax = 20;

	private const int TotalAttemptMax = 30;

	private const float DropPickupRange = 3f;

	private const float DropTimeout = 10f;

	private const float DropScanDelay = 0.5f;

	private static readonly int s_pickedHash = "picked".GetStableHashCode();

	private float _overweightMsgTimer;

	private float _chestScanTimer;

	private readonly Collider[] _scanBuffer = new Collider[4096];

	private readonly Collider[] _dropBuffer = new Collider[128];

	private readonly HashSet<int> _seenIds = new HashSet<int>();

	private GameObject _bestTree;

	private float _bestTreeDist;

	private Vector3 _lastDestroyPos;

	private GameObject _currentDrop;

	private float _dropTimer;

	private float _dropScanDelayTimer;

	private int _scanSeen;

	private int _scanUnwanted;

	private int _scanNoAuto;

	private int _scanFull;

	private int _dropsPickedUp;

	private int _itemLayerMask;

	private string _tag;

	private float _moveLogTimer;

	private float _heartbeatTimer;

	private float _attackDiagTimer;

	private const float MoveLogInterval = 1f;

	private const float HeartbeatInterval = 5f;

	private const float AttackDiagInterval = 2f;

	private static float BlacklistDuration => ModConfig.HarvestBlacklistDuration.Value;

	private static float ScanInterval => ModConfig.HarvestScanInterval.Value;

	private static float ScanRadius => ModConfig.HarvestScanRadius.Value;

	private bool _chopChain;

	private bool _catchingBreath;

	// HaveQueuedChain postfix: carry the weapon's combo on while working a target
	internal bool WantsChain => _chopChain && _state == HarvestState.Attacking && (_ai == null || !_ai.IsInCombat);

	private static readonly System.Reflection.FieldInfo s_curAttack = HarmonyLib.AccessTools.Field(typeof(Humanoid), "m_currentAttack");

	internal bool CurrentAttackCanChain()
	{
		try
		{
			return (s_curAttack?.GetValue(_humanoid) as Attack)?.CanStartChainAttack() ?? false;
		}
		catch
		{
			return false;
		}
	}

	// Stumps: the downward power chop. Logs: only one lying too low for the sideways swing - its top less than
	// [Gathering] PowerChopLogBelow above the companion's feet.
	private bool WantsPowerChop(ItemDrop.ItemData tool)
	{
		if (GetMode() != 1 || _target == null || tool == null || !tool.HaveSecondaryAttack())
		{
			return false;
		}
		if (_targetType == "Stump")
		{
			return true;
		}
		if (_targetType != "TreeLog")
		{
			return false;
		}
		float top = float.MinValue;
		foreach (Collider c in _target.GetComponentsInChildren<Collider>())
		{
			if (c != null && c.enabled && !c.isTrigger)
			{
				top = Mathf.Max(top, c.bounds.max.y);
			}
		}
		return top > float.MinValue && top < base.transform.position.y + (ModConfig.GatherPowerChopLogBelow?.Value ?? 0.3f);
	}

	private static float DropScanRadius => ModConfig.HarvestDropScanRadius.Value;

	// Where a gathering companion goes home to unload: [Gathering] OverweightThreshold is the BASE and the item bonus
	// adds on top of it (a Megingjord in the pack turns 298 into 448), the same rule as the carry ceiling
	// (CompanionCarry.Max). Instance, not static: it needs this companion's inventory.
	private float OverweightThreshold => CompanionCarry.UnloadThreshold(_humanoid?.GetInventory());

	public bool IsInGatherMode { get; private set; }

	public bool IsActive => _state != HarvestState.Idle || _goingBack;

	// 1.4.121: with Follow off it works an area, like Hunt (user): around home when the job starts near home, otherwise
	// around where it was started, whether or not you're nearby. With nothing left it walks back to the middle.
	private Vector3 _anchor;

	private bool _hasAnchor;

	private bool _anchorAtHome;

	private int _anchorMode;

	private bool _wasFollow;

	private bool _goingBack;

	private static float AreaRange => ModConfig.HarvestRange?.Value ?? 40f;

	private bool Following => _setup != null && _setup.GetFollow();

	// Fase 3.2: a target the player pointed at on the radial - a chat order's target must not touch it
	internal bool IsDirectedHarvest => _isDirectedHarvest;

	private void UpdateAnchor(int mode)
	{
		bool follow = Following;
		if (follow != _wasFollow || mode != _anchorMode)
		{
			_wasFollow = follow;
			_anchorMode = mode;
			_hasAnchor = false;
			if (_goingBack)
			{
				_goingBack = false;
				MHR_Mover.Release(_ai);
			}
		}
		if (follow || _isDirectedHarvest || _hasAnchor)
		{
			return;
		}
		_hasAnchor = true;
		_anchorAtHome = _setup != null && _setup.HasHomePosition() && MHR_ToolKeeper.NearHome(_setup);
		_anchor = _anchorAtHome ? _setup.GetHomePosition() : base.transform.position;
		LogInfo("Working around " + (_anchorAtHome ? "home" : "here") + " " + MHR_Mover.Fmt(_anchor) + ", up to " + AreaRange.ToString("F0") + " m out");
	}

	private bool InWorkArea(Vector3 p, string type = null)
	{
		if (Following || _isDirectedHarvest || !_hasAnchor)
		{
			float range = PlayerRange();
			// 1.5.60: logs and stumps further out with Follow on (user: "chopping down trees when they had already chopped
			// one down and there were other nearby logs" - the logs had rolled past FollowRange 10 m from the player)
			if ((type == "TreeLog" || type == "Stump") && _setup != null && _setup.GetFollow() && !_isDirectedHarvest)
			{
				range = Mathf.Max(range, ModConfig.HarvestFollowLogRange?.Value ?? 20f);
			}
			return MHR_Owner.Of(this) == null || Vector3.Distance(MHR_Owner.Of(this).transform.position, p) <= range;
		}
		return Vector3.Distance(_anchor, p) <= AreaRange;
	}

	private Vector3 ScanCenter()
	{
		return (!Following && !_isDirectedHarvest && _hasAnchor) ? _anchor : base.transform.position;
	}

	private float ScanRad()
	{
		return (!Following && !_isDirectedHarvest && _hasAnchor) ? AreaRange : ScanRadius;
	}

	private void UpdateGoingBack()
	{
		if (!_goingBack)
		{
			return;
		}
		if (Following || Vector3.Distance(base.transform.position, _anchor) <= 5f)
		{
			_goingBack = false;
			_ai?.StopMoving();
			MHR_Mover.Release(_ai);
			return;
		}
		MHR_Mover.Go(_ai, Time.deltaTime, _anchor, 4f, true, "Gather");
		if (MHR_Mover.GaveUp(_ai))
		{
			LogInfo("Can't get back to the work area - working from here");
			_anchor = base.transform.position;
			_anchorAtHome = false;
			_goingBack = false;
			MHR_Mover.Release(_ai);
		}
	}

	// 1.4.120: with Follow on, only what's within [Harvest] FollowRange of the player (targets of opportunity, like Hunt);
	// with Follow off, anything within 40 m of the player as before
	private float PlayerRange()
	{
		if (_setup == null || !_setup.GetFollow() || _isDirectedHarvest)
		{
			return 40f;
		}
		// 1.4.151: Forage has its own, shorter range (user: "more of a you stumbled upon some raspberries" - at 20 m it
		// picked almost everything around the player)
		return GetMode() == CompanionSetup.ModeForage ? (ModConfig.ForageFollowRange?.Value ?? 10f) : (ModConfig.HarvestFollowRange?.Value ?? 20f);
	}

	internal bool IsMoving => _state == HarvestState.Moving;

	internal bool IsAttacking => _state == HarvestState.Attacking;

	internal string CurrentStateName => _state.ToString();

	internal bool IsAttackingRockTarget(GameObject obj)
	{
		if (_state == HarvestState.Attacking && _target == obj)
		{
			return IsRockLikeTarget();
		}
		return false;
	}

	private Vector3 _lastTargetPos;

	// 1.5.65: the log or tree it just broke, so the pieces that come out of it are finished first (user, log e5d77d02:
	// "The NPC should finish chopping the log it was working on rather than going off to a new one")
	private Vector3 _splitPos;

	private float _splitAt = -100f;

	// 1.5.65: are its swings landing (user: "swings are hitting the rock not the log")
	// 1.5.67: told by the game itself - the target's Damage call with this companion as the attacker (HarvestHitRegister),
	// not guessed from its health over three swings (user: "Maybe we do need a hit register on the logs. Would that help
	// with the npc figuring out where to stand and not wander around so much?")
	private GameObject _hitCheckFor;

	private float _lastHitAt = -100f;

	private float _lastSwingAt = -100f;

	private int _noDamageSwings;

	private bool _triedOtherSide;

	// where it stood when a swing last landed on the current target - it stays there until the target breaks
	private bool _hasGoodSpot;

	private Vector3 _goodSpot;

	// 1.5.67: the logs that were already lying round its target when it started on it - anything new near it after the
	// break is one of its pieces, finished first wherever it rolls (user, log 9e9a7e57: "Lost track of the other log")
	private readonly HashSet<int> _preLogs = new HashSet<int>();

	private readonly HashSet<int> _ownPieces = new HashSet<int>();

	// a fresh log still falling or rolling: when it was first seen moving
	private GameObject _settleFor;

	private float _settleSince;

	private GameObject _settleLoggedFor;

	// 1.5.68: LogReachPoint found a spot on the ground it can swing at the log from
	private bool _logPointFound;

	private readonly List<Vector3> _logBadPoints = new List<Vector3>();

	// 1.4.79: many rocks are swapped for a cracked copy on the first hit (Destructible.m_spawnWhenDamaged): the object
	// we were hitting is gone, but the rock is still there. That read as "destroyed" - no drops, off to the next rock,
	// one hit each (1.4.78 test). If a rock now stands exactly where ours was, keep mining that one.
	private bool TryFollowCrackedRock()
	{
		string type = _targetType;
		if (type != "MineRock" && type != "MineRock5" && type != "Destructible")
		{
			return false;
		}
		Collider[] cols = Physics.OverlapSphere(_lastTargetPos, 2.5f, -1, QueryTriggerInteraction.Ignore);
		GameObject found = null;
		for (int i = 0; i < cols.Length; i++)
		{
			Collider c = cols[i];
			if (c == null || c.GetComponentInParent<Piece>() != null || c.GetComponentInParent<Character>() != null)
			{
				continue;
			}
			Component rock = (Component)c.GetComponentInParent<MineRock5>() ?? (Component)c.GetComponentInParent<MineRock>() ?? c.GetComponentInParent<Destructible>();
			if (rock == null || Vector3.Distance(rock.transform.position, _lastTargetPos) > 0.5f)
			{
				continue;
			}
			ZNetView nv = rock.GetComponent<ZNetView>();
			if (nv == null || !nv.IsValid())
			{
				continue;
			}
			found = rock.gameObject;
			break;
		}
		if (found == null)
		{
			return false;
		}
		UnclaimTarget();
		_target = found;
		_targetType = ClassifyTargetType(found, GetMode());
		ClaimTarget();
		_moveTargetLocked = false;
		_attackFaceLocked = false;
		_rock5CurrentSegment = null;
		_swingCount = 0;
		_totalAttempts = 0;
		_consecutiveFailures = 0;
		if (_targetType == "MineRock5")
		{
			_rock5ActiveSections = CountActiveRock5Sections();
		}
		Log("Rock cracked - carrying on with \"" + found.name + "\" (" + _targetType + ")");
		return true;
	}

	private float _closeInTimer;

	private bool _closingIn;

	// 1.4.79: where the rock's surface actually is, straight ahead from chest height toward its centre
	private bool RockSurface(out Vector3 surface, out float dist)
	{
		surface = Vector3.zero;
		dist = 0f;
		if (_target == null)
		{
			return false;
		}
		Vector3 from = base.transform.position + Vector3.up * 1.0f;
		Vector3 to = GetEffectiveTargetPosition();
		Vector3 d = to - from;
		float len = d.magnitude;
		if (len < 0.05f)
		{
			return false;
		}
		RaycastHit[] hits = Physics.RaycastAll(from, d / len, len + 0.5f, -1, QueryTriggerInteraction.Ignore);
		float best = float.MaxValue;
		for (int i = 0; i < hits.Length; i++)
		{
			Collider c = hits[i].collider;
			if (c != null && c.transform.IsChildOf(_target.transform) && hits[i].distance < best)
			{
				best = hits[i].distance;
				surface = hits[i].point;
			}
		}
		if (best == float.MaxValue)
		{
			return false;
		}
		dist = best;
		return true;
	}

	internal void TryApplyRockAoe()
	{
		if (_state == HarvestState.Attacking && IsRockLikeTarget() && _lastAoeSwing != _swingCount)
		{
			_lastAoeSwing = _swingCount;
			if (_targetType == "MineRock5")
			{
				ApplyDirectRock5Damage();
			}
			else
			{
				ApplyDirectDamage();
			}
		}
	}

	internal bool IsAttackingTreeTarget(GameObject obj)
	{
		if (_state == HarvestState.Attacking && _target == obj)
		{
			return IsTreeTarget();
		}
		return false;
	}

	internal void TryApplyTreeAoe()
	{
		if (_state == HarvestState.Attacking && IsTreeTarget() && _lastAoeSwing != _swingCount)
		{
			_lastAoeSwing = _swingCount;
			ApplyDirectDamage();
		}
	}

	public void NotifyTerrainHit()
	{
	}

	public void NotifyActionModeChanged()
	{
		int mode = GetMode();
		bool flag = (mode >= 1 && mode <= 3) || mode == 5;
		Log($"NotifyActionModeChanged — new mode={mode} isGather={flag} " + $"(wasGather={IsInGatherMode}, state={_state})");
		CancelDirectedTarget();
		if (!flag && IsInGatherMode)
		{
			ExitGatherMode();
			return;
		}
		RestoreLoadout();
		ResetToIdle();
	}

	internal static int DetermineHarvestModeStatic(GameObject target)
	{
		if (target.GetComponent<TreeBase>() != null)
		{
			return 1;
		}
		if (target.GetComponent<TreeLog>() != null)
		{
			return 1;
		}
		Destructible component = target.GetComponent<Destructible>();
		if (component != null && target.name.IndexOf("stub", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			return 1;
		}
		if (target.GetComponent<MineRock5>() != null)
		{
			return 3;
		}
		MineRock component2 = target.GetComponent<MineRock>();
		if (component2 != null)
		{
			return (HasOreDrops(component2.m_dropItems) || IsOreNamedRock(target.name)) ? 3 : 2;
		}
		if (component != null && component.m_damages.m_pickaxe != HitData.DamageModifier.Immune && (component.m_damages.m_chop == HitData.DamageModifier.Immune || component.m_damages.m_chop == HitData.DamageModifier.Ignore))
		{
			DropOnDestroyed component3 = component.GetComponent<DropOnDestroyed>();
			if (component3 != null && HasOreDrops(component3.m_dropWhenDestroyed))
			{
				return 3;
			}
			if (IsOreNamedRock(component.gameObject.name))
			{
				return 3;
			}
			return 2;
		}
		if (component != null && component.m_damages.m_chop != HitData.DamageModifier.Immune)
		{
			return 1;
		}
		return -1;
	}

	public void SetDirectedTarget(GameObject target)
	{
		if (target == null)
		{
			return;
		}
		int num = DetermineHarvestModeStatic(target);
		if (num < 0)
		{
			Log("SetDirectedTarget REJECTED — can't determine tool for \"" + target.name + "\"");
			return;
		}
		int minToolTier = GetMinToolTier(target);
		int bestToolTier = GetBestToolTier(num);
		if (minToolTier > bestToolTier)
		{
			Log($"SetDirectedTarget REJECTED — \"{target.name}\" requires tier {minToolTier}, " + $"best tool is tier {bestToolTier}");
			string text = "Companion";
			if (_nview?.GetZDO() != null)
			{
				string text2 = _nview.GetZDO().GetString(CompanionSetup.NameHash);
				if (!string.IsNullOrEmpty(text2))
				{
					text = text2;
				}
			}
			if (MHR_Owner.IsMine(this))
			{
				Player.m_localPlayer?.Message(MessageHud.MessageType.Center, ModLocalization.LocFmt("hc_msg_tools_weak", text));
			}
			return;
		}
		_directedTarget = target;
		_directedMode = num;
		_isDirectedHarvest = true;
		_target = target;
		_targetType = ClassifyTargetType(target, num);
		_state = HarvestState.Moving;
		_moveTimer = 0f;
		_moveTargetLocked = false;
		_swingCount = 0;
		_totalAttempts = 0;
		ClaimTarget();
		_scanTimer = ScanInterval;
		ItemDrop.ItemData itemData = FindBestTool(num);
		if (itemData != null)
		{
			EquipTool(itemData);
		}
		float num2 = Vector3.Distance(base.transform.position, target.transform.position);
		Log("[PointTo] SetDirectedTarget — \"" + target.name + "\" type=" + _targetType + " " + string.Format("mode={0} tool=\"{1}\" ", num, itemData?.m_shared?.m_name ?? "NONE") + $"dist={num2:F1}m targetPos={target.transform.position:F1} " + $"myPos={base.transform.position:F1}");
	}

	public void CancelDirectedTarget()
	{
		if (_isDirectedHarvest)
		{
			Log("CancelDirectedTarget — clearing directed harvest");
			_directedTarget = null;
			_isDirectedHarvest = false;
			if (!IsInGatherMode)
			{
				ResetToIdle();
				RestoreLoadout();
			}
		}
	}

	internal void MHR_NotifyIfIdle()
	{
		if (_state == HarvestState.Idle)
		{
			NotifyActionModeChanged();
		}
	}

	private void Awake()
	{
		_nview = GetComponent<ZNetView>();
		_ai = GetComponent<CompanionAI>();
		_humanoid = GetComponent<Humanoid>();
		_character = GetComponent<Character>();
		_setup = GetComponent<CompanionSetup>();
		_doorHandler = GetComponent<DoorHandler>();
		_stamina = GetComponent<CompanionStamina>();
		_talk = GetComponent<CompanionTalk>();
		int instanceID = GetInstanceID();
		_tag = $"[Harvest#{instanceID & 0xFFFF}]";
		_itemLayerMask = LayerMask.GetMask("item");
		Log($"Awake — nview={_nview != null} ai={_ai != null} " + $"humanoid={_humanoid != null} character={_character != null} " + $"setup={_setup != null} instanceId={instanceID}");
	}

	private void Log(string msg)
	{
		string text = _character?.m_name ?? "?";
		CompanionsPlugin.Log.LogDebug("[Gather|" + text + "] " + msg);
	}

	private void OnDestroy()
	{
		UnclaimTarget();
	}

	private void UnclaimTarget()
	{
		if (_claimedTargetId != 0)
		{
			s_claimedTargets.Remove(_claimedTargetId);
			_claimedTargetId = 0;
		}
	}

	private void Update()
	{
		if (CompanionRest.MHR_ShouldPauseWork(this) || _nview == null || !_nview.IsOwner())
		{
			return;
		}
		// 1.4.150: loot pickup and orders come before the stone-pile walk / tree dodge (they all steer)
		if (MHR_Loot.Active(this) || MHR_JobRules.OrderPending(this))
		{
			return;
		}
		if (MHR_GatherRules.TickDodge(this))
		{
			return;
		}
		if (_character != null && !_tag.Contains("|"))
		{
			string text = _character.m_name;
			if (!string.IsNullOrEmpty(text) && text != "HC_Companion" && text != "HC_Dverger")
			{
				_tag = $"[Harvest#{GetInstanceID() & 0xFFFF}|{text}]";
			}
		}
		int mode = GetMode();
		bool flag = (mode >= 1 && mode <= 3) || mode == 5;
		if (!flag && !_isDirectedHarvest)
		{
			if (IsInGatherMode)
			{
				ExitGatherMode();
			}
			return;
		}
		if (flag && !IsInGatherMode)
		{
			EnterGatherMode();
		}
		UpdateAnchor(mode);
		// 1.4.150: in a fight it fights - going overweight mid-fight used to send it to a chest with its back to the enemy
		if (_ai != null && _ai.IsInCombat)
		{
			RestoreLoadout();
			return;
		}
		if (IsOverweight())
		{
			if (!(_ai != null) || !(_ai.PendingDepositContainer != null))
			{
				MHR_GatherRules.HandleOverweight(this);
			}
			return;
		}
		_overweightMsgTimer -= Time.deltaTime;
		_chestScanTimer -= Time.deltaTime;
		if (_ai != null && _ai.IsInCombat)
		{
			RestoreLoadout();
			return;
		}
		// 1.4.128: after a fight, picking up what the kill dropped goes first (1.4.127 test: on Forage it kept walking to
		// the next mushroom while the loot pickup walked to the drops - the two took turns every frame and it stuttered)
		if (MHR_Loot.Active(this) || MHR_JobRules.OrderPending(this))
		{
			return;
		}
		if (IsCompanionUIOpen())
		{
			if (_state != HarvestState.Idle)
			{
				_ai?.StopMoving();
			}
			return;
		}
		// 1.4.120: following and you've moved on - leave the tree/rock/plant and catch up, like Hunt leaves its prey
		if (_state != HarvestState.Idle && !_isDirectedHarvest && _setup != null && _setup.GetFollow() && MHR_Owner.Of(this) != null &&
			Vector3.Distance(base.transform.position, MHR_Owner.Of(this).transform.position) > PlayerRange() + 15f)
		{
			Log("You moved on - leaving \"" + (_target != null ? _target.name : "the drops") + "\" and catching up");
			ResetToIdle();
			RestoreLoadout();
			// 1.5.69: catch up first (log fc93f903: it took the same log straight back, 57 times)
			_scanTimer = 3f;
			return;
		}
		_heartbeatTimer -= Time.deltaTime;
		if (_heartbeatTimer <= 0f)
		{
			_heartbeatTimer = 5f;
			LogHeartbeat();
		}
		// Fase 3.3d: the number an order asked for is already in the pack - end it right here instead of working on.
		// The scan between targets catches the same thing (MHR_GatherRules.OrderDone), but a stone rock hands its
		// stone over in what it spawns when it breaks (see AddDrops), so "colete 10 pedras" could carry on for a
		// whole rock's worth of stone before the next scan ever ran. Only while it is walking to or working a target:
		// what that target just dropped is picked up first, so nothing the companion mined is left on the ground.
		if ((_state == HarvestState.Moving || _state == HarvestState.Attacking) && MHR_GatherRules.OrderDoneNow(this, mode))
		{
			return;
		}
		switch (_state)
		{
		case HarvestState.Idle:
			UpdateGoingBack();
			UpdateIdle(mode);
			break;
		case HarvestState.Moving:
			UpdateMoving();
			break;
		case HarvestState.Attacking:
			UpdateAttacking();
			break;
		case HarvestState.CollectingDrops:
			UpdateCollectingDrops();
			break;
		}
	}

	private int GetMode()
	{
		if (_isDirectedHarvest)
		{
			return _directedMode;
		}
		return (_nview?.GetZDO())?.GetInt(CompanionSetup.ActionModeHash) ?? 0;
	}

	private void ExitGatherMode()
	{
		Log($"EXIT gather mode — was state={_state} " + "target=\"" + ((_target != null) ? _target.name : "null") + "\" " + $"toolEquipped={_toolEquipped}");
		IsInGatherMode = false;
		_hasAnchor = false;
		if (_goingBack)
		{
			_goingBack = false;
			MHR_Mover.Release(_ai);
		}
		ResetToIdle();
		RestoreLoadout();
		bool flag = _setup != null && _setup.GetFollow();
		if (_ai != null && flag && MHR_Owner.Of(this) != null)
		{
			_ai.SetFollowTarget(MHR_Owner.Go(this));
			Log("Restored follow target to player (Follow=ON)");
		}
		else
		{
			Log("Follow OFF — idle");
		}
	}

	private void EnterGatherMode()
	{
		int mode = GetMode();
		string arg = mode switch
		{
			5 => "Forage", 
			3 => "Ore", 
			2 => "Stone", 
			1 => "Wood", 
			_ => "Unknown", 
		};
		IsInGatherMode = true;
		_scanTimer = 0f;
		_heartbeatTimer = 0f;
		GameObject gameObject = _ai?.GetFollowTarget();
		if (mode == 5)
		{
			LogInfo($"Foraging started — scanning for pickables near {base.transform.position:F1}");
		}
		Log($"ENTER gather mode: {arg} (mode={mode}) " + "followTarget=\"" + (gameObject?.name ?? "null") + "\" " + $"pos={base.transform.position:F1}");
	}

	public bool IsOverweight()
	{
		if (GetCurrentWeight() >= OverweightThreshold)
		{
			return true;
		}
		return MHR_GatherRules.PackFull(this);
	}

	internal void RestoreLoadout()
	{
		if (_toolEquipped || ((object)_setup != null && _setup.SuppressAutoEquip))
		{
			_toolEquipped = false;
			Log("RestoreLoadout — SuppressAutoEquip=false, re-equipping best gear");
			if (_setup != null)
			{
				_setup.SuppressAutoEquip = false;
				_setup.SyncEquipmentToInventory();
			}
		}
	}

	private bool IsCompanionUIOpen()
	{
		if (!CompanionInteractPanel.IsOpenFor(_setup) && !CompanionRadialMenu.IsOpenFor(_setup))
		{
			return HomeZonePanel.IsOpenFor(_setup);
		}
		return true;
	}

	private void LogHeartbeat()
	{
		// Fase 3.19 (auditoria): it was empty - the "the companion has been in this state for a while" trail never
		// existed. Debug level on purpose: every 5 s would be noise in a normal log.
		if (_state == HarvestState.Idle)
		{
			LogDebug("heartbeat: idle");
			return;
		}
		Vector3 target = GetEffectiveTargetPosition();
		float dist = Vector3.Distance(base.transform.position, target);
		string targetName = _target != null ? _target.name : "none";
		LogDebug($"heartbeat: state={_state} target=\"{targetName}\" dist={dist:F1}m type={_targetType ?? "?"} "
			+ $"swings={_swingCount}/{_totalAttempts}");
	}

	private void UpdateIdle(int mode)
	{
		_scanTimer -= Time.deltaTime;
		if (_scanTimer > 0f)
		{
			return;
		}
		_scanTimer = ScanInterval;
		if (_blacklist.Count > 0)
		{
			float time = Time.time;
			List<int> list = null;
			foreach (KeyValuePair<int, float> item in _blacklist)
			{
				if (time - item.Value > BlacklistDuration)
				{
					if (list == null)
					{
						list = new List<int>();
					}
					list.Add(item.Key);
				}
			}
			if (list != null)
			{
				for (int i = 0; i < list.Count; i++)
				{
					_blacklist.Remove(list[i]);
				}
				Log($"Blacklist cleanup: removed {list.Count} expired, {_blacklist.Count} remain");
			}
		}
		LogDebug($"Scanning for targets... mode={mode} " + $"pos={base.transform.position:F1} radius={ScanRadius}");
		// 1.4.122: with only a stone or flint axe, or no axe (Stone: or no pickaxe), also pick up branches / stones / flint
		// lying around (user); with no tool for the job, that's all it does
		GameObject loose = null;
		bool noTool = false;
		bool looseOn = !_isDirectedHarvest && (mode == 1 || mode == 2) && LooseOn(mode, out noTool);
		if (looseOn)
		{
			loose = ScanForPickable(mode);
		}
		GameObject gameObject = (looseOn && noTool) ? null : MHR_GatherRules.Scan(this, mode);
		bool pickLoose = false;
		if (loose != null && (gameObject == null || Vector3.Distance(base.transform.position, loose.transform.position) <= Vector3.Distance(base.transform.position, gameObject.transform.position)))
		{
			gameObject = loose;
			pickLoose = true;
		}
		// 1.5.42: it has something to pick up or work after all (e.g. loose items while foraging): the "nothing to gather"
		// reason no longer holds (user: forage was listed as "nothing to gather" while it was gathering)
		if (gameObject != null)
		{
			CompanionStatus.Clear(this);
			// 1.5.43: and the status says what (phase 3)
			CompanionStatus.Doing(this, pickLoose ? "hc_doing_pick" : (mode == 5) ? "hc_doing_forage" : (mode == 1) ? "hc_doing_chop" : "hc_doing_mine", MHR_Items.NiceName(gameObject));
		}
		if (gameObject == null)
		{
			// Fase 3.2: nothing to work and the reason is the target a chat order put on this job ("colete 5
			// madeiras") - the order is done, so it goes back to following instead of standing there.
			if (MHR_GatherRules.OrderDone(this, mode))
			{
				return;
			}
			LogDebug("Scan found 0 valid targets - staying idle");
			if (!_goingBack && !Following && !_isDirectedHarvest && _hasAnchor && Vector3.Distance(base.transform.position, _anchor) > 8f)
			{
				LogInfo("Nothing left nearby - walking back to the middle of the work area");
				_goingBack = true;
			}
			return;
		}
		if (_goingBack)
		{
			_goingBack = false;
			MHR_Mover.Release(_ai);
		}
		if (mode == 5 || pickLoose)
		{
			float num = Vector3.Distance(base.transform.position, gameObject.transform.position);
			LogInfo($"{(pickLoose ? "Picking up" : "Forage target")}: \"{gameObject.name}\" dist={num:F1}m");
			Log($"Forage target acquired: \"{gameObject.name}\" dist={num:F1}m " + $"pos={gameObject.transform.position:F1}");
			_target = gameObject;
			_targetType = "Pickable";
			_swingCount = 0;
			_totalAttempts = 0;
			ClaimTarget();
		}
		else
		{
			ItemDrop.ItemData itemData = FindBestTool(mode);
			if (itemData == null)
			{
				LogWarn($"No tool in inventory for mode {mode} — cannot harvest! " + "(need " + ((mode == 1) ? "axe (chop dmg)" : "pickaxe (pickaxe dmg)") + ")");
				LogInventoryContents();
				return;
			}
			float num2 = Vector3.Distance(base.transform.position, gameObject.transform.position);
			LogInfo($"Target acquired: \"{gameObject.name}\" dist={num2:F1}m " + "tool=\"" + itemData.m_shared.m_name + "\" " + $"pick={itemData.GetDamage().m_pickaxe:F0}");
			_target = gameObject;
			_targetType = ClassifyTargetType(gameObject, mode);
			_swingCount = 0;
			_totalAttempts = 0;
			EquipTool(itemData);
			ClaimTarget();
		}
		if (_ai != null)
		{
			_ai.SetFollowTarget(null);
			Log("Target set: \"" + _target.name + "\" — HarvestController driving movement");
		}
		NoteLogsAround();
		_moveTimer = 0f;
		_moveLogTimer = 0f;
		_moveTargetLocked = false;
		_attackFaceLocked = false;
		_state = HarvestState.Moving;
	}

	private void UpdateMoving()
	{
		if (LogSettling() || LogOutOfReach())
		{
			return;
		}
		if (_pendingToolReequip != null && _humanoid != null && !_humanoid.InAttack())
		{
			Log("Retrying deferred equip: \"" + _pendingToolReequip.m_shared.m_name + "\"");
			EquipTool(_pendingToolReequip);
		}
		if (_doorHandler != null && _doorHandler.IsActive)
		{
			return;
		}
		if (!IsTargetValid(_target))
		{
			Log("Target gone while moving — waiting 1.5s then rescanning");
			ResetToIdle();
			_scanTimer = 1.5f;
			return;
		}
		Vector3 effectiveTargetPosition = GetEffectiveTargetPosition();
		Vector3 vector = effectiveTargetPosition - base.transform.position;
		float num = Mathf.Abs(vector.y);
		vector.y = 0f;
		float num2 = ((_targetType == "Pickable") ? 1.5f : GetAttackRange());
		bool flag = (!(_targetType == "TreeLog") && !(_targetType == "TreeBase")) || num < 3f;
		if (vector.magnitude <= num2 && flag)
		{
			if (_ai != null)
			{
				_ai.StopMoving();
			}
			if (IsRockLikeTarget())
			{
				_lockedAttackFaceTarget = GetRockFacePosition();
				_attackFaceLocked = true;
			}
			else
			{
				_attackFaceLocked = false;
			}
			FaceTarget();
			_reapproach = false;
			_attackTimer = 0.3f;
			_attackDiagTimer = 0f;
			_closeInTimer = 0f;
			_closingIn = false;
			_state = HarvestState.Attacking;
			if (IsRockLikeTarget())
			{
				_attackNodePos = GetEffectiveTargetPosition();
			}
			if (_targetType == "MineRock5")
			{
				_rock5ActiveSections = CountActiveRock5Sections();
			}
			return;
		}
		float deltaTime = Time.deltaTime;
		bool flag2 = _humanoid != null && _humanoid.InAttack();
		if (!flag2)
		{
			Vector3 lhs = _character?.GetVelocity() ?? Vector3.zero;
			Vector3 vector2 = _target.transform.position - base.transform.position;
			vector2.y = 0f;
			float num3 = ((vector2.sqrMagnitude > 0.01f) ? Vector3.Dot(lhs, vector2.normalized) : 0f);
			// 1.4.77: following a route around something (MHR_Mover still getting closer) counts like walking at the target
			float num4 = ((num3 > 1f || MHR_Mover.Progressing(_ai)) ? 0.3f : 1f);
			if (MHR_DoorBusy())
			{
				goto IL_04a6;
			}
			_moveTimer += deltaTime * num4;
		}
		// 1.4.77: the movement worked through its whole stuck ladder - give this target up now (after a moment, so a
		// give-up left over from the previous target doesn't count)
		if (_moveTimer > 0.5f && MHR_Mover.GaveUp(_ai))
		{
			_moveTimer = 10.01f;
		}
		if (_moveTimer > 10f)
		{
			float num5 = Vector3.Distance(base.transform.position, _target.transform.position);
			Vector3 vector3 = _character?.GetVelocity() ?? Vector3.zero;
			bool flag3 = _targetType == "TreeLog" || _targetType == "TreeBase";
			bool flag4 = _targetType == "Destructible" || _targetType == "MineRock";
			_ = flag3 || flag4;
			LogWarn($"STUCK timeout ({10f}s) — giving up on \"{_target.name}\" " + $"dist={num5:F1}m vel={vector3.magnitude:F1} " + $"myPos={base.transform.position:F1} targetPos={_target.transform.position:F1} " + $"targetY={_target.transform.position.y:F2} myY={base.transform.position.y:F2} " + $"heightDiff={_target.transform.position.y - base.transform.position.y:F2}");
			_blacklist[_target.GetInstanceID()] = Time.time;
			Log($"Blacklisted \"{_target.name}\" (id={_target.GetInstanceID()}) for {BlacklistDuration}s");
			ResetToIdle();
			return;
		}
		goto IL_04a6;
		IL_04a6:
		bool flag5 = _targetType == "Pickable";
		float num6 = (flag5 ? 1.5f : GetAttackRange());
		if (!_moveTargetLocked)
		{
			Vector3 effectiveTargetPosition2 = GetEffectiveTargetPosition();
			if (IsRockLikeTarget() && NavMesh.SamplePosition(effectiveTargetPosition2, out var hit, 6f, -1))
			{
				_lockedMoveTarget = hit.position;
			}
			else
			{
				_lockedMoveTarget = effectiveTargetPosition2;
			}
			_moveTargetLocked = true;
		}
		Vector3 lockedMoveTarget = _lockedMoveTarget;
		float num7 = Vector3.Distance(base.transform.position, lockedMoveTarget);
		bool flag6 = IsRockLikeTarget();
		float num8;
		bool flag7;
		if (flag5)
		{
			num8 = 1.2f;
			flag7 = false;
		}
		else if (_reapproach)
		{
			num8 = num6 * 0.5f;
			flag7 = false;
		}
		else if (IsLowTarget())
		{
			num8 = num6 * 0.8f;
			flag7 = num7 > 8f;
		}
		else if (flag6)
		{
			num8 = num6 * 0.8f;
			flag7 = num7 > 6f;
		}
		else
		{
			num8 = num6 * 0.8f;
			flag7 = num7 > 8f;
		}
		_shouldRun = flag7;
		// 1.4.77 (movement phase 3): MHR_Mover instead of MoveToPoint.
		bool flag8 = MHR_Mover.Go(_ai, deltaTime, MHR_GatherRules.ApproachPoint(this, lockedMoveTarget), num8, flag7, "Gather");
		if (!flag8 && num7 < 4f && (_character?.GetVelocity().magnitude ?? 0f) < 1f)
		{
			// 1.4.78: put back from before 1.4.77. Close in but not moving (the approach point is right up against a big
			// rock): step straight at it, so the swings connect. Without this he swung 20 times from 1.9 m and missed.
			Vector3 vector4 = lockedMoveTarget - base.transform.position;
			vector4.y = 0f;
			// 1.4.82: not over an edge (1.4.81 log: he pushed at a stump on the moat's lip and fell in)
			if (vector4.sqrMagnitude > 0.01f && !MHR_Mover.HazardAhead(base.transform.position, vector4, 0.8f) && !MHR_Mover.Climbing(_ai))
			{
				_ai.MoveTowards(vector4.normalized, run: false);
			}
		}
		else if (flag8 && num7 > num8 + 0.5f && (_character?.GetVelocity() ?? Vector3.zero).magnitude < 0.3f)
		{
			Vector3 vector5 = lockedMoveTarget - base.transform.position;
			vector5.y = 0f;
			// 1.5.72: not over a drop, into fire or spikes
			if (vector5.sqrMagnitude > 0.01f && !MHR_Mover.HazardAhead(base.transform.position, vector5, 0.8f))
			{
				_ai.MoveTowards(vector5.normalized, run: false);
			}
		}
		_moveLogTimer -= deltaTime;
		if (_moveLogTimer <= 0f)
		{
			_moveLogTimer = 1f;
			float num10 = Vector3.Distance(base.transform.position, lockedMoveTarget);
			Vector3 vector6 = _character?.GetVelocity() ?? Vector3.zero;
			GameObject gameObject = _ai?.GetFollowTarget();
			Log($"Moving → \"{_target.name}\" dist={num10:F1}m " + $"range={num6:F1} timer={_moveTimer:F1}s " + $"moveOK={flag8} vel={vector6.magnitude:F1} velY={vector6.y:F2} " + "follow=\"" + (gameObject?.name ?? "null") + "\" " + $"inAttack={flag2} " + $"pos={base.transform.position:F1} tgt={lockedMoveTarget:F1} " + $"heightDiff={lockedMoveTarget.y - base.transform.position.y:F2}");
		}
		num7 = Vector3.Distance(base.transform.position, lockedMoveTarget);
		Vector3 vector7 = lockedMoveTarget - base.transform.position;
		float magnitude = new Vector3(vector7.x, 0f, vector7.z).magnitude;
		bool flag9 = IsLowTarget();
		float num11 = (flag5 ? 1.5f : (_reapproach ? (num6 * 0.7f) : (flag9 ? num6 : ((!flag6) ? (num6 + 0.5f) : num6))));
		if (_reapproach && _moveTimer > 3f)
		{
			float num12 = ((flag9 || flag6) ? num6 : (num6 + 0.5f));
			if (num7 <= num12 || magnitude <= num12)
			{
				num11 = num12;
			}
		}
		if (num7 <= num11 || magnitude <= num11)
		{
			if (_ai != null)
			{
				_ai.StopMoving();
			}
			if (flag6)
			{
				_lockedAttackFaceTarget = GetRockFacePosition();
				_attackFaceLocked = true;
			}
			else
			{
				_attackFaceLocked = false;
			}
			FaceTarget();
			_reapproach = false;
			_attackTimer = 0.3f;
			_attackDiagTimer = 0f;
			_closeInTimer = 0f;
			_closingIn = false;
			_state = HarvestState.Attacking;
			if (flag6)
			{
				_attackNodePos = GetEffectiveTargetPosition();
			}
			if (_targetType == "MineRock5")
			{
				_rock5ActiveSections = CountActiveRock5Sections();
			}
		}
	}

	private void UpdateAttacking()
	{
		if (LogSettling() || LogOutOfReach())
		{
			return;
		}
		int mode = GetMode();
		if (mode == 5 || _targetType == "Pickable")
		{
			Pickable pickable = _target?.GetComponentInParent<Pickable>();
			if (pickable != null)
			{
				ZNetView component = pickable.GetComponent<ZNetView>();
				if (component != null && component.GetZDO() != null && component.GetZDO().GetBool(s_pickedHash))
				{
					LogInfo("Forage target already picked — skipping \"" + pickable.name + "\"");
				}
				else
				{
					FaceTarget();
					pickable.Interact(_humanoid, repeat: false, alt: false);
					LogInfo($"Foraged: \"{pickable.name}\" at {pickable.transform.position:F1}");
				}
			}
			else
			{
				LogWarn("Forage target has no Pickable — skipping \"" + (_target?.name ?? "null") + "\"");
			}
			_lastDestroyPos = _target?.transform.position ?? base.transform.position;
			_currentDrop = null;
			_dropTimer = 0f;
			_dropScanDelayTimer = 0.5f;
			_dropsPickedUp = 0;
			_state = HarvestState.CollectingDrops;
			if (_ai != null)
			{
				_ai.StopMoving();
			}
			return;
		}
		if (_target != null)
		{
			_lastTargetPos = _target.transform.position;
		}
		if (!IsTargetValid(_target) && TryFollowCrackedRock())
		{
			return;
		}
		if (!IsTargetValid(_target))
		{
			Log($"Target DESTROYED after {_swingCount} hits, {_totalAttempts} attempts " + "— entering drop collection");
			if (_targetType == "TreeLog" || _targetType == "TreeBase")
			{
				if (Time.time - _splitAt > 60f)
				{
					_ownPieces.Clear();
				}
				_splitPos = _lastTargetPos;
				_splitAt = Time.time;
			}
			MHR_GatherRules.OnTargetDestroyed(this, _targetType);
			_lastDestroyPos = MHR_GatherRules.DropCenter(this);
			if (_targetType == "TreeBase")
			{
				GameObject gameObject = TryFindNearbyLog(base.transform.position);
				if (gameObject != null && !s_claimedTargets.Contains(gameObject.GetInstanceID()))
				{
					UnclaimTarget();
					_target = gameObject;
					_targetType = ClassifyTargetType(gameObject, 1);
					_swingCount = 0;
					_totalAttempts = 0;
					ClaimTarget();
					_moveTargetLocked = false;
					_moveTimer = 0f;
					_state = HarvestState.Moving;
					Log("Tree fell — immediately targeting fallen log \"" + gameObject.name + "\"");
					if (_ai != null)
					{
						_ai.StopMoving();
					}
					return;
				}
			}
			_currentDrop = null;
			_dropTimer = 0f;
			_dropScanDelayTimer = 0.5f;
			_dropsPickedUp = 0;
			_state = HarvestState.CollectingDrops;
			if (_ai != null)
			{
				_ai.StopMoving();
			}
			return;
		}
		FaceTarget();
		bool flag = IsRockLikeTarget();
		Vector3 effectiveTargetPosition = GetEffectiveTargetPosition();
		float num = Vector3.Distance(base.transform.position, effectiveTargetPosition);
		float attackRange = GetAttackRange();
		float num2 = (flag ? (attackRange + 1.2f) : (attackRange + 1.5f));
		bool flag2 = _targetType == "TreeLog" || _targetType == "TreeBase";
		float num3 = num;
		if (flag2)
		{
			Vector3 vector = effectiveTargetPosition - base.transform.position;
			num3 = new Vector3(vector.x, 0f, vector.z).magnitude;
		}
		_attackDiagTimer -= Time.deltaTime;
		// 1.5.69: right under a log that's above its reach - leave it (log fc93f903: walked "closer" to a log 6.4 m up for
		// minutes; each re-approach reset the stuck timer)
		if (_targetType == "TreeLog" && num3 <= attackRange && num > attackRange + 0.5f && !_character.InAttack())
		{
			SetAside("it's above its reach");
			return;
		}
		// 1.5.68: a log is reached in 3D - not from under it (log 9aef82b6: a swing from 10.6 m below a log in the branches)
		if (num3 > num2 || (_targetType == "TreeLog" && num > attackRange + 0.5f && !_character.InAttack()))
		{
			_reapproach = true;
			_attackFaceLocked = false;
			_moveTimer = 0f;
			_moveLogTimer = 0f;
			_moveTargetLocked = false;
			_state = HarvestState.Moving;
			return;
		}
		// 1.4.79: the distance above is to the spot beside the rock, not the rock itself. On a big rock the surface can be
		// further than the pickaxe reaches, so every swing missed (1.4.78 log: 10 swings at 1.5 m, no damage). Check the
		// actual surface and step in until it's in reach (up to 2.5 s, then swing anyway).
		if (flag && _character != null && !_character.InAttack())
		{
			Vector3 surface;
			float surfaceDist;
			if (RockSurface(out surface, out surfaceDist) && surfaceDist > attackRange * 0.85f && _closeInTimer < 2.5f)
			{
				_closeInTimer += Time.deltaTime;
				Vector3 step = surface - base.transform.position;
				step.y = 0f;
				if (step.sqrMagnitude > 0.01f && !MHR_Mover.HazardAhead(base.transform.position, step, 0.8f))
				{
					_ai.MoveTowards(step.normalized, run: false);
				}
				if (!_closingIn)
				{
					_closingIn = true;
					Log($"Rock surface is {surfaceDist:F1}m away (reach {attackRange * 0.85f:F1}m) — stepping in");
				}
				return;
			}
			if (_closingIn)
			{
				_closingIn = false;
				_ai.StopMoving();
			}
		}
		_attackTimer -= Time.deltaTime;
		if (_attackTimer > 0f)
		{
			return;
		}
		ItemDrop.ItemData rightItem = ReflectionHelper.GetRightItem(_humanoid);
		int mode2 = GetMode();
		bool flag3 = rightItem == null;
		if (!flag3)
		{
			HitData.DamageTypes damage = rightItem.GetDamage();
			flag3 = ((mode2 != 1) ? (damage.m_pickaxe <= 0f) : (damage.m_chop <= 0f));
		}
		if (flag3)
		{
			Log(string.Format("Wrong/missing tool: \"{0}\" — re-equipping for mode={1}", rightItem?.m_shared?.m_name ?? "NONE", mode2));
			ItemDrop.ItemData itemData = FindBestTool(mode2);
			if (itemData == null)
			{
				RestoreLoadout();
				ResetToIdle();
				return;
			}
			EquipTool(itemData);
			rightItem = ReflectionHelper.GetRightItem(_humanoid);
			if (rightItem == null)
			{
				_attackTimer = 0.25f;
				return;
			}
		}
		bool flag4 = _humanoid != null && _humanoid.InAttack();
		bool flag5 = _humanoid != null && _humanoid.InDodge();
		bool flag6 = _character != null && _character.IsStaggering();
		// 1.5.61: like a player holding attack - the next swing goes as soon as the current one allows it (the weapon's
		// combo, its last swing for double damage) instead of one swing every 2.5 s (user: "It seems like it might be a
		// little slow"). Mid-swing it waits for the chain point (CompanionChainPatch) and checks again next frame.
		bool chainReady = flag4 && !flag5 && !flag6 && _chopChain && CurrentAttackCanChain();
		if ((flag4 && !chainReady) || flag5 || flag6)
		{
			if (flag5 || flag6)
			{
				LogInfo($"Attack blocked: inDodge={flag5} stagger={flag6} — retry in {0.25f}s");
			}
			_attackTimer = (flag4 ? 0f : 0.25f);
			return;
		}
		// 1.5.61: the power chop only where a player uses it (user: "The only time I use a power chop is when taking out a
		// stump as it swings down rather than across. Occasionally I will use it if a log is below me and I cannot hit the
		// log with a regular swing.") - it used to be every swing from the 4th on
		bool flag8 = WantsPowerChop(rightItem);
		float attackStamina = (flag8 ? rightItem.m_shared.m_secondaryAttack.m_attackStamina : rightItem.m_shared.m_attack.m_attackStamina);
		if (_stamina != null && attackStamina > 0f)
		{
			// 1.5.61: chops until it's nearly out of breath, then stops and gets it back before chopping on, like a player
			float max = Mathf.Max(1f, _stamina.MaxStamina);
			if (_catchingBreath)
			{
				if (_stamina.Stamina < (ModConfig.GatherResumeStamina?.Value ?? 0.6f) * max)
				{
					_chopChain = false;
					_attackTimer = 0.5f;
					return;
				}
				_catchingBreath = false;
				Log($"Got its breath back ({_stamina.Stamina:F0}/{max:F0}) — back to work");
			}
			if (_stamina.Stamina < attackStamina + (ModConfig.GatherRestStamina?.Value ?? 0.15f) * max)
			{
				_catchingBreath = true;
				_chopChain = false;
				_attackTimer = 0.5f;
				Log($"Catching its breath ({_stamina.Stamina:F0}/{max:F0} stamina) before swinging again");
				return;
			}
		}
		bool flag9 = _humanoid != null && _humanoid.StartAttack(null, flag8);
		_totalAttempts++;
		_chopChain = flag9 && !flag8;
		// a swing that just started isn't InAttack for a frame - don't start another over it
		_attackTimer = (flag9 ? 0.4f : 0.5f);
		if (flag9)
		{
			_swingCount++;
			_consecutiveFailures = 0;
			if (IsRockLikeTarget() && _target != null)
			{
				if (_targetType == "MineRock5" && _rock5ActiveSections > 0)
				{
					int num5 = CountActiveRock5Sections();
					if (num5 < _rock5ActiveSections)
					{
						if (CountExposedRock5Sections() == 0 && num5 > 0)
						{
							MHR_GatherRules.BlacklistTarget(this);
							ResetToIdle();
							return;
						}
						_rock5ActiveSections = num5;
						_rock5CurrentSegment = null;
						_totalAttempts = 0;
						_swingCount = 0;
						_consecutiveFailures = 0;
						_reapproach = false;
						_attackFaceLocked = false;
						_moveTimer = 0f;
						_moveLogTimer = 0f;
						_moveTargetLocked = false;
						_state = HarvestState.Moving;
						return;
					}
					_rock5ActiveSections = num5;
				}
				Vector3 effectiveTargetPosition2 = GetEffectiveTargetPosition();
				float num6 = Vector3.Distance(effectiveTargetPosition2, _attackNodePos);
				if (num6 > 1.5f)
				{
					float num7 = Vector3.Distance(base.transform.position, effectiveTargetPosition2);
					Log(_targetType + " section destroyed — repositioning to next node " + $"(dist={num7:F1}m shift={num6:F1}m)");
					_attackNodePos = effectiveTargetPosition2;
					_reapproach = true;
					_attackFaceLocked = false;
					_moveTimer = 0f;
					_moveLogTimer = 0f;
					_moveTargetLocked = false;
					_state = HarvestState.Moving;
					return;
				}
			}
		}
		else
		{
			_consecutiveFailures++;
			if (_consecutiveFailures >= 2)
			{
				_reapproach = true;
				_attackFaceLocked = false;
				_consecutiveFailures = 0;
				_moveTimer = 0f;
				_moveLogTimer = 0f;
				_moveTargetLocked = false;
				_state = HarvestState.Moving;
				return;
			}
		}
		if (flag9 && _targetType == "MineRock5" && _swingCount > 0 && _swingCount % 100000 == 0)
		{
			_rock5CurrentSegment = null;
			_swingCount = 0;
			_consecutiveFailures = 0;
			_reapproach = true;
			_attackFaceLocked = false;
			_moveTimer = 0f;
			_moveLogTimer = 0f;
			_moveTargetLocked = false;
			_state = HarvestState.Moving;
			return;
		}
		if (!flag9 || _swingCount < 1 || _targetType == "TreeLog" || _targetType == "TreeBase")
		{
		}
		if (flag9 && SwingsNotLanding())
		{
			if (_targetType == "TreeLog" && !_triedOtherSide)
			{
				_triedOtherSide = true;
				_logBadPoints.Add(_logPoint);
				_logPointAt = -10f;
				_noDamageSwings = 0;
				LogInfo("Its swings aren't reaching \"" + _target.name + "\" (something in the way) - trying another side");
				_reapproach = true;
				_attackFaceLocked = false;
				_moveTimer = 0f;
				_moveLogTimer = 0f;
				_moveTargetLocked = false;
				_state = HarvestState.Moving;
				return;
			}
			LogInfo("Its swings aren't doing anything to \"" + _target.name + "\" (something in the way) - leaving it for now");
			_blacklist[_target.GetInstanceID()] = Time.time;
			ResetToIdle();
			return;
		}
		if (flag9 && _swingCount > 0 && _swingCount % 2 == 0 && num > attackRange * 0.85f && !HoldingGoodSpot)
		{
			_reapproach = true;
			_attackFaceLocked = false;
			_moveTimer = 0f;
			_moveLogTimer = 0f;
			_moveTargetLocked = false;
			_state = HarvestState.Moving;
		}
		else if (_swingCount >= 20 || _totalAttempts >= 30)
		{
			LogWarn($"Bailout — {_swingCount} hits, {_totalAttempts} attempts on \"{_target.name}\" " + $"dist={num:F1}m without destruction. Abandoning target.");
			_blacklist[_target.GetInstanceID()] = Time.time;
			Log($"Blacklisted \"{_target.name}\" (id={_target.GetInstanceID()}) for {BlacklistDuration}s");
			ResetToIdle();
		}
		else if (flag9)
		{
			Log("Attack swing → \"" + _target.name + "\" " + $"success=True dist={num:F1}m " + "weapon=\"" + (rightItem?.m_shared?.m_name ?? "NONE") + "\" " + $"power={flag8} swings={_swingCount}/{20} " + $"attempts={_totalAttempts}/{30}");
		}
		else if (_totalAttempts <= 1 || _totalAttempts % 5 == 0)
		{
			Animator animator = _humanoid?.GetComponentInChildren<Animator>();
			string arg = "unknown";
			if (animator != null)
			{
				AnimatorStateInfo currentAnimatorStateInfo = animator.GetCurrentAnimatorStateInfo(0);
				arg = $"hash={currentAnimatorStateInfo.shortNameHash} norm={currentAnimatorStateInfo.normalizedTime:F2}";
			}
			float num8 = ((_stamina != null) ? _stamina.Stamina : (-1f));
			float num9 = ((_stamina != null) ? _stamina.MaxStamina : (-1f));
			LogWarn($"Attack FAILED — attempts={_totalAttempts}/{30} " + $"animState=[{arg}] power={flag8} " + "weapon=\"" + (rightItem?.m_shared?.m_name ?? "NONE") + "\" " + $"stamina={num8:F0}/{num9:F0} " + $"atkCost={rightItem.m_shared.m_attack.m_attackStamina:F0}");
		}
	}

	private void UpdateCollectingDrops()
	{
		float deltaTime = Time.deltaTime;
		_dropTimer += deltaTime;
		if (_dropTimer > 10f)
		{
			Log($"Drop collection timeout ({10f}s) — picked up {_dropsPickedUp} items, resuming harvest");
			FinishDropCollection();
			return;
		}
		if (_dropScanDelayTimer > 0f)
		{
			_dropScanDelayTimer -= deltaTime;
			return;
		}
		if (_currentDrop != null)
		{
			if (_currentDrop == null || _currentDrop.GetComponent<ZNetView>() == null || !_currentDrop.GetComponent<ZNetView>().IsValid())
			{
				Log("Current drop target gone — scanning for more");
				_currentDrop = null;
			}
			else
			{
				float num = Vector3.Distance(base.transform.position, _currentDrop.transform.position);
				if (!(num <= 2.2f))
				{
					MHR_Mover.Go(_ai, deltaTime, _currentDrop.transform.position, 1.5f, true, "Gather");
					_moveLogTimer -= deltaTime;
					if (_moveLogTimer <= 0f)
					{
						_moveLogTimer = 2f;
						string arg = _currentDrop.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_name ?? "?";
						Log($"Approaching drop \"{arg}\" dist={num:F1}m " + $"(pickup range={3f:F1}) timer={_dropTimer:F1}s");
					}
					return;
				}
				TryPickupDrop(_currentDrop);
				_currentDrop = null;
			}
		}
		GameObject gameObject = ScanForDrops();
		if (gameObject == null)
		{
			// 1.4.79: give drops a moment to land before moving on (1.4.78 log: "picked up 0 items" right after a rock broke)
			if (_dropsPickedUp == 0 && _dropTimer < 1.5f)
			{
				return;
			}
			Log($"No more drops nearby — picked up {_dropsPickedUp} items, resuming harvest " + $"(last scan: {_scanSeen} items in {DropScanRadius:F0}m, skipped {_scanUnwanted} not wanted, {_scanNoAuto} not auto-pickup, {_scanFull} no room{(IsOverweight() ? ", overweight" : "")})");
			FinishDropCollection();
			return;
		}
		_currentDrop = gameObject;
		float num2 = Vector3.Distance(base.transform.position, gameObject.transform.position);
		string arg2 = gameObject.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_name ?? "?";
		Log($"Moving to drop: \"{arg2}\" dist={num2:F1}m pos={gameObject.transform.position:F1}");
	}

	internal void ResetToIdle()
	{
		HarvestState state = _state;
		_state = HarvestState.Idle;
		_target = null;
		_targetType = null;
		_swingCount = 0;
		_totalAttempts = 0;
		_consecutiveFailures = 0;
		_reapproach = false;
		_rock5ActiveSections = -1;
		_rock5CurrentSegment = null;
		_lastAoeSwing = -1;
		_scanTimer = 0f;
		_pendingToolReequip = null;
		_moveTargetLocked = false;
		_attackFaceLocked = false;
		UnclaimTarget();
		if (_isDirectedHarvest)
		{
			_isDirectedHarvest = false;
			_directedTarget = null;
			RestoreLoadout();
			Log("ResetToIdle (directed harvest complete) — restored loadout");
		}
		bool flag = _setup != null && _setup.GetFollow();
		if (_ai != null && flag && MHR_Owner.Of(this) != null)
		{
			_ai.SetFollowTarget(MHR_Owner.Go(this));
		}
		if (state != HarvestState.Idle)
		{
			Log($"ResetToIdle (was {state}) — follow={flag}");
		}
	}

	private bool IsRockLikeTarget()
	{
		if (!(_targetType == "MineRock5") && !(_targetType == "MineRock"))
		{
			return _targetType == "Destructible";
		}
		return true;
	}

	private void ApplyDirectRock5Damage()
	{
		if (_target == null)
		{
			return;
		}
		MineRock5 component = _target.GetComponent<MineRock5>();
		if (component == null)
		{
			return;
		}
		ItemDrop.ItemData rightItem = ReflectionHelper.GetRightItem(_humanoid);
		if (rightItem == null)
		{
			return;
		}
		Collider collider = null;
		float num = float.MaxValue;
		foreach (Transform item in _target.transform)
		{
			if (!item.gameObject.activeSelf)
			{
				continue;
			}
			Collider component2 = item.GetComponent<Collider>();
			if (!(component2 == null) && IsSegmentExposed(component2))
			{
				float num2 = Vector3.Distance(base.transform.position, component2.bounds.center);
				if (num2 < num)
				{
					num = num2;
					collider = component2;
				}
			}
		}
		if (collider == null)
		{
			return;
		}
		Vector3 vector = SafeClosestPoint(collider, base.transform.position);
		HitData hitData = new HitData();
		hitData.m_point = vector;
		hitData.m_dir = (vector - base.transform.position).normalized;
		hitData.m_hitCollider = collider;
		hitData.m_toolTier = (short)rightItem.m_shared.m_toolTier;
		hitData.m_damage = rightItem.GetDamage();
		hitData.m_hitType = HitData.HitType.PlayerHit;
		hitData.SetAttacker(_character);
		float randomSkillFactor = _character.GetRandomSkillFactor(rightItem.m_shared.m_skillType);
		if (randomSkillFactor != 1f)
		{
			hitData.m_damage.Modify(randomSkillFactor);
		}
		if (_stamina != null)
		{
			float attackStamina = rightItem.m_shared.m_attack.m_attackStamina;
			if (attackStamina > 0f)
			{
				_stamina.UseStamina(attackStamina);
			}
		}
		IsApplyingAoeDamage = true;
		component.Damage(hitData);
		IsApplyingAoeDamage = false;
	}

	private void ApplyDirectDamage()
	{
		if (_target == null)
		{
			return;
		}
		IDestructible component = _target.GetComponent<IDestructible>();
		if (component == null)
		{
			return;
		}
		ItemDrop.ItemData rightItem = ReflectionHelper.GetRightItem(_humanoid);
		if (rightItem == null)
		{
			return;
		}
		Vector3 vector = _target.transform.position;
		Collider collider = null;
		MineRock component2 = _target.GetComponent<MineRock>();
		if (component2 != null)
		{
			float num = float.MaxValue;
			Collider[] componentsInChildren = _target.GetComponentsInChildren<Collider>();
			foreach (Collider collider2 in componentsInChildren)
			{
				if (!(collider2 == null) && collider2.gameObject.activeSelf)
				{
					float num2 = Vector3.Distance(base.transform.position, collider2.bounds.center);
					if (num2 < num)
					{
						num = num2;
						collider = collider2;
					}
				}
			}
			if (collider == null)
			{
				return;
			}
			vector = collider.bounds.center;
		}
		HitData hitData = new HitData();
		hitData.m_point = vector;
		hitData.m_dir = (vector - base.transform.position).normalized;
		hitData.m_hitCollider = collider;
		hitData.m_toolTier = (short)rightItem.m_shared.m_toolTier;
		hitData.m_damage = rightItem.GetDamage();
		hitData.m_hitType = HitData.HitType.PlayerHit;
		hitData.SetAttacker(_character);
		float randomSkillFactor = _character.GetRandomSkillFactor(rightItem.m_shared.m_skillType);
		if (randomSkillFactor != 1f)
		{
			hitData.m_damage.Modify(randomSkillFactor);
		}
		if (_stamina != null)
		{
			float attackStamina = rightItem.m_shared.m_attack.m_attackStamina;
			if (attackStamina > 0f)
			{
				_stamina.UseStamina(attackStamina);
			}
		}
		IsApplyingAoeDamage = true;
		component.Damage(hitData);
		IsApplyingAoeDamage = false;
	}

	private static bool IsSegmentExposed(Collider col)
	{
		if (col == null)
		{
			return false;
		}
		Vector3 center = col.bounds.center;
		if (ZoneSystem.instance == null)
		{
			return true;
		}
		float groundHeight = ZoneSystem.instance.GetGroundHeight(center);
		return center.y >= groundHeight - 1f;
	}

	private static Vector3 SafeClosestPoint(Collider col, Vector3 point)
	{
		if (col is MeshCollider)
		{
			return col.ClosestPointOnBounds(point);
		}
		return col.ClosestPoint(point);
	}

	private static int GetMinToolTier(GameObject target)
	{
		if (target == null)
		{
			return 0;
		}
		TreeBase component = target.GetComponent<TreeBase>();
		if (component != null)
		{
			return component.m_minToolTier;
		}
		TreeLog component2 = target.GetComponent<TreeLog>();
		if (component2 != null)
		{
			return component2.m_minToolTier;
		}
		MineRock5 component3 = target.GetComponent<MineRock5>();
		if (component3 != null)
		{
			return component3.m_minToolTier;
		}
		MineRock component4 = target.GetComponent<MineRock>();
		if (component4 != null)
		{
			return component4.m_minToolTier;
		}
		Destructible component5 = target.GetComponent<Destructible>();
		if (component5 != null)
		{
			return component5.m_minToolTier;
		}
		return 0;
	}

	private int GetBestToolTier(int mode)
	{
		Inventory inventory = _humanoid?.GetInventory();
		if (inventory == null)
		{
			return -1;
		}
		int num = -1;
		foreach (ItemDrop.ItemData allItem in inventory.GetAllItems())
		{
			if (allItem?.m_shared != null && (!allItem.m_shared.m_useDurability || !(allItem.m_durability <= 0f)) && allItem.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Bow && string.IsNullOrEmpty(allItem.m_shared.m_ammoType))
			{
				HitData.DamageTypes damage = allItem.GetDamage();
				bool flag = false;
				flag = ((mode != 1) ? (damage.m_pickaxe > 0f) : (damage.m_chop > 0f));
				if (flag && allItem.m_shared.m_toolTier > num)
				{
					num = allItem.m_shared.m_toolTier;
				}
			}
		}
		return num;
	}

	private static string ClassifyTargetType(GameObject target, int mode)
	{
		if (target == null)
		{
			return null;
		}
		if (mode == 1)
		{
			if (target.GetComponent<TreeBase>() != null)
			{
				return "TreeBase";
			}
			if (target.GetComponent<TreeLog>() != null)
			{
				return "TreeLog";
			}
			Destructible component = target.GetComponent<Destructible>();
			if (component != null && target.name.IndexOf("stub", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return "Stump";
			}
		}
		if (target.GetComponent<MineRock5>() != null)
		{
			return "MineRock5";
		}
		if (target.GetComponent<MineRock>() != null)
		{
			return "MineRock";
		}
		if (target.GetComponent<Destructible>() != null)
		{
			return "Destructible";
		}
		return "Unknown";
	}

	private void ClaimTarget()
	{
		UnclaimTarget();
		_claimedTargetId = _target?.GetInstanceID() ?? 0;
		if (_claimedTargetId != 0)
		{
			s_claimedTargets.Add(_claimedTargetId);
		}
	}

	private ItemDrop.ItemData FindBestTool(int mode)
	{
		Inventory inventory = _humanoid?.GetInventory();
		if (inventory == null)
		{
			LogWarn("FindBestTool — inventory is null!");
			return null;
		}
		ItemDrop.ItemData itemData = null;
		float num = 0f;
		int num2 = 0;
		foreach (ItemDrop.ItemData allItem in inventory.GetAllItems())
		{
			if (allItem?.m_shared == null || (allItem.m_shared.m_useDurability && allItem.m_durability <= 0f))
			{
				continue;
			}
			// 1.4.81: bows and crossbows can't chop or mine, whatever chop damage the item lists (1.4.80 log: the Arbalest
			// "chop=140" beat the axe, he equipped it for Wood and just stood there)
			if (allItem.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Bow || !string.IsNullOrEmpty(allItem.m_shared.m_ammoType) || allItem.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Ammo)
			{
				continue;
			}
			HitData.DamageTypes damage = allItem.GetDamage();
			bool flag = false;
			float num3 = 0f;
			if (mode == 1)
			{
				if (damage.m_chop > 0f)
				{
					flag = true;
					num3 = damage.m_chop;
				}
			}
			else if (damage.m_pickaxe > 0f)
			{
				flag = true;
				num3 = damage.m_pickaxe;
			}
			if (flag)
			{
				num2++;
				Log("  Tool candidate: \"" + allItem.m_shared.m_name + "\" " + $"type={allItem.m_shared.m_itemType} " + $"chop={damage.m_chop:F0} pick={damage.m_pickaxe:F0} " + $"relevantDmg={num3:F0} " + $"equipped={_humanoid.IsItemEquiped(allItem)}");
				if (num3 > num)
				{
					itemData = allItem;
					num = num3;
				}
			}
		}
		if (itemData != null)
		{
			Log($"Best tool: \"{itemData.m_shared.m_name}\" dmg={num:F0} " + $"(out of {num2} matching tools)");
		}
		else
		{
			LogWarn("No matching tools found! " + string.Format("Mode={0} need={1} ", mode, (mode == 1) ? "m_chop > 0" : "m_pickaxe > 0") + $"totalItems={inventory.GetAllItems().Count}");
		}
		return itemData;
	}

	private void EquipTool(ItemDrop.ItemData tool)
	{
		if (tool == null || _humanoid == null)
		{
			return;
		}
		// 1.5.60: the game won't equip mid-swing or mid-dodge (Humanoid.EquipItem) - this unequipped the sword and then
		// failed, leaving it empty-handed for a few tries; wait for the swing to end instead
		if (_humanoid.InAttack() || _humanoid.InDodge())
		{
			return;
		}
		if (_setup != null)
		{
			_setup.SuppressAutoEquip = true;
		}
		_setup?.MHR_DropQueuedHandItems();
		ItemDrop.ItemData rightItem = ReflectionHelper.GetRightItem(_humanoid);
		ItemDrop.ItemData leftItem = ReflectionHelper.GetLeftItem(_humanoid);
		bool flag = rightItem == tool;
		bool flag2 = _humanoid.IsItemEquiped(tool);
		if (flag || flag2)
		{
			Log("EquipTool: \"" + tool.m_shared.m_name + "\" already equipped " + $"(sameRef={flag} isEquipped={flag2}) — skipping");
			_toolEquipped = true;
			return;
		}
		bool flag3 = rightItem != null && rightItem.m_shared?.m_name == tool.m_shared?.m_name;
		Log("EquipTool: equipping \"" + tool.m_shared.m_name + "\" (right=\"" + (rightItem?.m_shared?.m_name ?? "none") + "\" left=\"" + (leftItem?.m_shared?.m_name ?? "none") + "\") " + $"sameNameInHand={flag3} " + $"rightHash={rightItem?.GetHashCode() ?? 0} toolHash={tool.GetHashCode()} " + "SuppressAutoEquip=true");
		if (flag3)
		{
			if (leftItem != null)
			{
				_humanoid.UnequipItem(leftItem, triggerEquipEffects: false);
			}
			_toolEquipped = true;
			Log("After equip (same-name shortcut): rightItem=\"" + (ReflectionHelper.GetRightItem(_humanoid)?.m_shared?.m_name ?? "NONE") + "\" " + $"attackRange={GetAttackRange():F1}");
			return;
		}
		if (rightItem != null)
		{
			_humanoid.UnequipItem(rightItem, triggerEquipEffects: false);
		}
		MHR_Equip.ClearLeftFor(_humanoid, leftItem, tool);
		_humanoid.EquipItem(tool, triggerEquipEffects: false);
		ItemDrop.ItemData rightItem2 = ReflectionHelper.GetRightItem(_humanoid);
		if (rightItem2 != null)
		{
			_toolEquipped = true;
			_pendingToolReequip = null;
			Log("After equip: rightItem=\"" + (rightItem2.m_shared?.m_name ?? "?") + "\" " + $"attackRange={GetAttackRange():F1}");
		}
		else
		{
			_toolEquipped = false;
			_pendingToolReequip = tool;
			LogWarn("After equip: rightItem=\"NONE\" — EQUIP FAILED (will retry). " + $"Tried to equip \"{tool.m_shared.m_name}\" type={tool.m_shared.m_itemType} " + "inAttack=" + (_humanoid.InAttack() ? "true" : "false"));
		}
	}

	private static bool HasOreDrops(DropTable table)
	{
		if (table == null || table.m_drops.Count == 0)
		{
			return false;
		}
		foreach (DropTable.DropData drop in table.m_drops)
		{
			if (!(drop.m_item == null))
			{
				string text = drop.m_item.name;
				if (text.IndexOf("Ore", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("Scrap", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("Obsidian", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("Chitin", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("Flametal", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("BlackMetal", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					return true;
				}
			}
		}
		return false;
	}

	private static bool IsOreNamedRock(string goName)
	{
		if (goName.IndexOf("copper", StringComparison.OrdinalIgnoreCase) < 0 && goName.IndexOf("tin", StringComparison.OrdinalIgnoreCase) < 0 && goName.IndexOf("silver", StringComparison.OrdinalIgnoreCase) < 0 && goName.IndexOf("iron", StringComparison.OrdinalIgnoreCase) < 0 && goName.IndexOf("meteorite", StringComparison.OrdinalIgnoreCase) < 0 && goName.IndexOf("blackmetal", StringComparison.OrdinalIgnoreCase) < 0 && goName.IndexOf("flametal", StringComparison.OrdinalIgnoreCase) < 0 && goName.IndexOf("obsidian", StringComparison.OrdinalIgnoreCase) < 0)
		{
			return goName.IndexOf("chitin", StringComparison.OrdinalIgnoreCase) >= 0;
		}
		return true;
	}

	private void LogInfo(string msg)
	{
		string text = _character?.m_name ?? "?";
		CompanionsPlugin.Log.LogInfo("[Gather|" + text + "] " + msg);
	}

	// Per-scan / per-retry chatter: handy while debugging a job, pure noise in a normal log. Shows up only with
	// BepInEx Debug logging on.
	private void LogDebug(string msg)
	{
		string text = _character?.m_name ?? "?";
		CompanionsPlugin.Log.LogDebug("[Gather|" + text + "] " + msg);
	}

	/// <summary>Fase 3.19 (auditoria): real warnings, on the same <c>[Gather|name]</c> prefix the info lines use. This
	/// method was empty, so every diagnostic built on it (STUCK timeout, Attack FAILED, Bailout, No tool) was thrown
	/// away and a companion that couldn't work just stood there silently.</summary>
	private void LogWarn(string msg)
	{
		string text = _character?.m_name ?? "?";
		CompanionsPlugin.Log.LogWarning("[Gather|" + text + "] " + msg);
	}

	/// <summary>Fase 3.19: the pack that FindBestTool was looking at, for the "no tool" warning. Debug level - it is a
	/// long list and only wanted while a job is being investigated.</summary>
	private void LogInventoryContents()
	{
		Humanoid humanoid = GetComponent<Humanoid>();
		Inventory inventory = humanoid != null ? humanoid.GetInventory() : null;
		if (inventory == null)
		{
			LogDebug("inventory: none");
			return;
		}
		List<ItemDrop.ItemData> items = inventory.GetAllItems();
		StringBuilder builder = new StringBuilder();
		for (int i = 0; i < items.Count; i++)
		{
			ItemDrop.ItemData item = items[i];
			if (item?.m_shared == null)
			{
				continue;
			}
			if (builder.Length > 0)
			{
				builder.Append(", ");
			}
			builder.Append('"').Append(item.m_shared.m_name).Append('"');
			if (!string.IsNullOrEmpty(item.m_dropPrefab?.name))
			{
				builder.Append(" (").Append(item.m_dropPrefab.name).Append(')');
			}
		}
		LogDebug("inventory: " + (builder.Length > 0 ? builder.ToString() : "empty"));
	}

	private static bool IsTargetValid(GameObject target)
	{
		return target != null;
	}

	// the target's health where the game keeps it (trees, logs, stumps, small rocks); -1 when it can't be read (MineRock
	// and MineRock5 keep it per part)
	private float TargetHealth()
	{
		ZDO z = _target != null ? _target.GetComponent<ZNetView>()?.GetZDO() : null;
		if (z == null)
		{
			return -1f;
		}
		TreeLog log = _target.GetComponent<TreeLog>();
		if (log != null)
		{
			return z.GetFloat(ZDOVars.s_health, log.m_health);
		}
		TreeBase tree = _target.GetComponent<TreeBase>();
		if (tree != null)
		{
			return z.GetFloat(ZDOVars.s_health, tree.m_health);
		}
		Destructible d = _target.GetComponent<Destructible>();
		if (d != null)
		{
			return z.GetFloat(ZDOVars.s_health, d.m_health);
		}
		return -1f;
	}

	// HarvestHitRegister: the game registered a hit by this companion on `go`
	internal void OnHitRegistered(GameObject go)
	{
		if (go == null || _target == null || (go != _target && !_target.transform.IsChildOf(go.transform) && !go.transform.IsChildOf(_target.transform)))
		{
			return;
		}
		_lastHitAt = Time.time;
		_noDamageSwings = 0;
		if (!_hasGoodSpot || Vector3.Distance(_goodSpot, base.transform.position) > 0.7f)
		{
			_hasGoodSpot = true;
			_goodSpot = base.transform.position;
		}
	}

	// at each swing: did the one before it land? 2 in a row that didn't -> true
	private bool SwingsNotLanding()
	{
		if (_hitCheckFor != _target)
		{
			_hitCheckFor = _target;
			_noDamageSwings = 0;
			_triedOtherSide = false;
			_hasGoodSpot = false;
			_logBadPoints.Clear();
			_lastSwingAt = Time.time;
			_lastHitAt = -100f;
			return false;
		}
		bool missed = _lastHitAt < _lastSwingAt;
		_lastSwingAt = Time.time;
		if (!missed)
		{
			return false;
		}
		_noDamageSwings++;
		if (_noDamageSwings >= 1)
		{
			_hasGoodSpot = false;
		}
		return _noDamageSwings >= 2;
	}

	// following and further from you than a job may take it - the "You moved on" distance
	private bool TooFarFromYou()
	{
		return !_isDirectedHarvest && _setup != null && _setup.GetFollow() && MHR_Owner.Of(this) != null && Vector3.Distance(base.transform.position, MHR_Owner.Of(this).transform.position) > PlayerRange() + 15f;
	}

	// it's working from a spot its swings land from - no walking about
	private bool HoldingGoodSpot => _hasGoodSpot && _hitCheckFor == _target && _noDamageSwings == 0 && Vector3.Distance(_goodSpot, base.transform.position) < 0.8f;

	private void NoteLogsAround()
	{
		_preLogs.Clear();
		if (_target == null || GetMode() != 1)
		{
			return;
		}
		foreach (Collider c in Physics.OverlapSphere(_target.transform.position, 15f))
		{
			TreeLog l = c != null ? c.GetComponentInParent<TreeLog>() : null;
			if (l != null && !_ownPieces.Contains(l.gameObject.GetInstanceID()))
			{
				_preLogs.Add(l.gameObject.GetInstanceID());
			}
		}
	}

	// one of the pieces of what it just broke (new since it started on it, within 30 m of the break, for 60 s)
	private bool IsOwnPiece(GameObject go, string type)
	{
		if (type != "TreeLog" || Time.time - _splitAt > 60f)
		{
			return false;
		}
		int id = go.GetInstanceID();
		if (_ownPieces.Contains(id))
		{
			return true;
		}
		if (_preLogs.Contains(id) || Vector3.Distance(go.transform.position, _splitPos) > 30f)
		{
			return false;
		}
		_ownPieces.Add(id);
		return true;
	}

	// a fresh log still falling or rolling - wait (up to 3 s) before picking where to stand
	private bool LogSettling()
	{
		if (_targetType != "TreeLog" || _target == null)
		{
			return false;
		}
		Rigidbody rb = _target.GetComponent<Rigidbody>();
		bool moving = rb != null && !rb.isKinematic && (rb.linearVelocity.magnitude > 0.3f || rb.angularVelocity.magnitude > 0.3f);
		if (!moving)
		{
			_settleFor = null;
			return false;
		}
		if (_settleFor != _target)
		{
			_settleFor = _target;
			_settleSince = Time.time;
			if (_settleLoggedFor != _target)
			{
				_settleLoggedFor = _target;
				LogInfo("Waiting for \"" + _target.name + "\" to stop moving");
			}
		}
		// 1.5.68: stands and waits up to 8 s, then leaves a log that keeps rolling or sliding for a while instead of chasing
		// it (user, log 9aef82b6: "went chasing a rolling log that was not settling")
		if (Time.time - _settleSince > 8f)
		{
			SetAside("it keeps rolling");
			return true;
		}
		_logPointAt = -10f;
		_ai?.StopMoving();
		return true;
	}

	// 1.5.68: leave the target for 15 s (it may settle or fall), then it's a candidate again
	private void SetAside(string why)
	{
		if (_target == null)
		{
			return;
		}
		LogInfo("Leaving \"" + _target.name + "\" for a moment - " + why);
		_blacklist[_target.GetInstanceID()] = Time.time - Mathf.Max(0f, BlacklistDuration - 15f);
		_settleFor = null;
		ResetToIdle();
	}

	// 1.5.68: a log with no part it can reach from the ground (hung up in the branches - log 9aef82b6: one 13 m up, swung
	// at from 10.6 m below, knocked loose and chased down the slope)
	private bool LogOutOfReach()
	{
		if (_targetType != "TreeLog" || _target == null)
		{
			return false;
		}
		LogReachPoint();
		if (_logPointFound)
		{
			return false;
		}
		SetAside("no part of it is in reach from the ground");
		return true;
	}

	private static int s_lineMask = -1;

	// nothing but the log itself between the chest of someone standing at `ground` and the point on the log
	private bool LineToLog(Vector3 ground, Vector3 p)
	{
		if (s_lineMask == -1)
		{
			s_lineMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece");
		}
		Vector3 from = ground + Vector3.up * 1f;
		Vector3 d = p - from;
		float len = d.magnitude - 0.1f;
		if (len <= 0.1f)
		{
			return true;
		}
		foreach (RaycastHit hit in Physics.RaycastAll(from, d.normalized, len, s_lineMask, QueryTriggerInteraction.Ignore))
		{
			if (hit.collider == null || hit.collider.GetComponentInParent<Character>() != null || hit.collider.transform.IsChildOf(_target.transform))
			{
				continue;
			}
			return false;
		}
		return true;
	}

	private GameObject _logPointFor;

	private float _logPointAt;

	private Vector3 _logPoint;

	// 1.5.66: where the log was when the spot was picked - the spot is kept while the log stays put (user, log 2285db5e:
	// "Keeps moving when hitting a log. Moves to an end then side or somewhere else along the log")
	private Vector3 _logPointLogPos;

	private static int s_groundMask = -1;

	// ground spots round the log (1 m grid over its footprint, 1.5 m out); for each, the nearest point of the log -
	// usable when it is within 1.2 m above the ground there and 1 m sideways (in swing reach of someone standing there);
	// the one nearest the companion. Refreshed every 2 s (logs roll); the log's position when none is.
	private Vector3 LogReachPoint()
	{
		if (_target == _logPointFor && _logPointAt > 0f && Vector3.Distance(_target.transform.position, _logPointLogPos) < 0.5f)
		{
			return _logPoint;
		}
		_logPointLogPos = _target.transform.position;
		_logPointFor = _target;
		_logPointAt = Time.time;
		_logPoint = _target.transform.position;
		_logPointFound = false;
		if (s_groundMask == -1)
		{
			s_groundMask = LayerMask.GetMask("terrain", "Default", "static_solid", "piece");
		}
		List<Collider> cols = new List<Collider>();
		Bounds box = default(Bounds);
		bool any = false;
		foreach (Collider c in _target.GetComponentsInChildren<Collider>())
		{
			if (c == null || !c.enabled || c.isTrigger || (c is MeshCollider mc && !mc.convex))
			{
				continue;
			}
			cols.Add(c);
			if (any)
			{
				box.Encapsulate(c.bounds);
			}
			else
			{
				box = c.bounds;
				any = true;
			}
		}
		if (!any)
		{
			return _logPoint;
		}
		Vector3 me = base.transform.position;
		float best = float.MaxValue;
		for (float x = box.min.x - 1.5f; x <= box.max.x + 1.5f; x += 1f)
		{
			for (float z = box.min.z - 1.5f; z <= box.max.z + 1.5f; z += 1f)
			{
				Vector3 top = new Vector3(x, box.max.y + 2f, z);
				if (!Physics.Raycast(top, Vector3.down, out RaycastHit hit, box.size.y + 8f, s_groundMask) || hit.collider.GetComponentInParent<TreeLog>() != null || hit.collider.GetComponentInParent<TreeBase>() != null)
				{
					continue;
				}
				Vector3 ground = hit.point;
				foreach (Collider c in cols)
				{
					Vector3 p;
					try
					{
						p = c.ClosestPoint(ground + Vector3.up * 0.8f);
					}
					catch
					{
						continue;
					}
					float up = p.y - ground.y;
					float side = new Vector2(p.x - ground.x, p.z - ground.z).magnitude;
					if (up > 1.2f || up < -0.5f || side > 1f)
					{
						continue;
					}
					// 1.5.65: not past a rock or anything else (log e5d77d02: 17 swings into the rock under the log), and not
					// a spot its swings already failed from
					if (!LineToLog(ground, p) || (_hitCheckFor == _target && _logBadPoints.Exists(b => Vector3.Distance(b, p) < 1.5f)))
					{
						continue;
					}
					float d = Vector3.Distance(me, ground);
					if (d < best)
					{
						best = d;
						_logPoint = p;
						_logPointFound = true;
					}
				}
			}
		}
		return _logPoint;
	}

	private Vector3 GetEffectiveTargetPosition()
	{
		if (_target == null)
		{
			return base.transform.position;
		}
		if (_targetType == "MineRock5")
		{
			Collider collider = null;
			float num = float.MaxValue;
			foreach (Transform item in _target.transform)
			{
				if (!item.gameObject.activeSelf)
				{
					continue;
				}
				Collider component = item.GetComponent<Collider>();
				if (!(component == null) && IsSegmentExposed(component))
				{
					float num2 = Vector3.Distance(base.transform.position, component.bounds.center);
					if (num2 < num)
					{
						num = num2;
						collider = component;
					}
				}
			}
			if (collider == null)
			{
				foreach (Transform item2 in _target.transform)
				{
					if (!item2.gameObject.activeSelf)
					{
						continue;
					}
					Collider component2 = item2.GetComponent<Collider>();
					if (!(component2 == null))
					{
						float num3 = Vector3.Distance(base.transform.position, component2.bounds.center);
						if (num3 < num)
						{
							num = num3;
							collider = component2;
						}
					}
				}
			}
			if (collider != null)
			{
				_rock5CurrentSegment = collider;
				Vector3 center = collider.bounds.center;
				float y = ((ZoneSystem.instance != null) ? ZoneSystem.instance.GetGroundHeight(center) : center.y);
				return new Vector3(center.x, y, center.z);
			}
		}
		if (_targetType == "Destructible")
		{
			Vector3 vector = _target.transform.position;
			Collider[] componentsInChildren = _target.GetComponentsInChildren<Collider>();
			foreach (Collider collider2 in componentsInChildren)
			{
				if (collider2.gameObject.activeSelf && !collider2.isTrigger)
				{
					vector = collider2.bounds.center;
					break;
				}
			}
			Vector3 vector2 = base.transform.position + Vector3.up * 0.8f;
			Vector3 vector3 = vector - vector2;
			if (vector3.sqrMagnitude > 0.01f)
			{
				RaycastHit[] array = Physics.RaycastAll(vector2, vector3.normalized, vector3.magnitude + 3f);
				RaycastHit[] array2 = array;
				for (int j = 0; j < array2.Length; j++)
				{
					RaycastHit raycastHit = array2[j];
					if (raycastHit.collider.GetComponentInParent<Destructible>()?.gameObject == _target)
					{
						return raycastHit.point;
					}
				}
			}
			return vector;
		}
		// 1.5.64: a log - the part of it near the ground that it can stand next to, not its middle (user, log 862662de:
		// "Swinging at a log he cannot reach. Need to check for a part touching the ground and swing near there" - a fir
		// log's middle was 3.6 m above it on a slope)
		if (_targetType == "TreeLog")
		{
			return LogReachPoint();
		}
		if (_targetType == "MineRock")
		{
			float num4 = float.MaxValue;
			Vector3 result = _target.transform.position;
			bool flag = false;
			Collider[] componentsInChildren2 = _target.GetComponentsInChildren<Collider>();
			foreach (Collider collider3 in componentsInChildren2)
			{
				if (collider3.gameObject.activeSelf && !collider3.isTrigger)
				{
					Vector3 vector4 = collider3.bounds.ClosestPoint(base.transform.position);
					float num5 = Vector3.Distance(base.transform.position, vector4);
					if (num5 < num4)
					{
						num4 = num5;
						result = vector4;
						flag = true;
					}
				}
			}
			if (flag)
			{
				return result;
			}
		}
		return _target.transform.position;
	}

	private float GetAttackRange()
	{
		ItemDrop.ItemData rightItem = ReflectionHelper.GetRightItem(_humanoid);
		if (rightItem?.m_shared?.m_attack != null)
		{
			return rightItem.m_shared.m_attack.m_attackRange;
		}
		return 2f;
	}

	private Vector3 GetRockFacePosition()
	{
		if (_target == null)
		{
			return base.transform.position;
		}
		if (_targetType == "MineRock5")
		{
			Collider rock5CurrentSegment = _rock5CurrentSegment;
			if (rock5CurrentSegment != null && rock5CurrentSegment.gameObject.activeSelf)
			{
				Vector3 result = SafeClosestPoint(rock5CurrentSegment, base.transform.position);
				result.y = Mathf.Max(result.y, base.transform.position.y + 0.1f);
				return result;
			}
			Collider collider = null;
			float num = float.MaxValue;
			foreach (Transform item in _target.transform)
			{
				if (!item.gameObject.activeSelf)
				{
					continue;
				}
				Collider component = item.GetComponent<Collider>();
				if (!(component == null) && IsSegmentExposed(component))
				{
					float num2 = Vector3.Distance(base.transform.position, component.bounds.center);
					if (num2 < num)
					{
						num = num2;
						collider = component;
					}
				}
			}
			if (collider != null)
			{
				Vector3 result2 = SafeClosestPoint(collider, base.transform.position);
				result2.y = Mathf.Max(result2.y, base.transform.position.y + 0.1f);
				return result2;
			}
		}
		if (_targetType == "Destructible")
		{
			Collider[] componentsInChildren = _target.GetComponentsInChildren<Collider>();
			foreach (Collider collider2 in componentsInChildren)
			{
				if (collider2.gameObject.activeSelf && !collider2.isTrigger)
				{
					return collider2.bounds.center;
				}
			}
			return _target.transform.position;
		}
		if (_targetType == "MineRock")
		{
			float num3 = float.MaxValue;
			Vector3 result3 = _target.transform.position;
			bool flag = false;
			Collider[] componentsInChildren2 = _target.GetComponentsInChildren<Collider>();
			foreach (Collider collider3 in componentsInChildren2)
			{
				if (collider3.gameObject.activeSelf && !collider3.isTrigger)
				{
					Vector3 b = collider3.bounds.ClosestPoint(base.transform.position);
					float num4 = Vector3.Distance(base.transform.position, b);
					if (num4 < num3)
					{
						num3 = num4;
						result3 = collider3.bounds.center;
						flag = true;
					}
				}
			}
			if (flag)
			{
				return result3;
			}
		}
		return _target.transform.position;
	}

	private void FaceTarget()
	{
		if (_target == null || _ai == null || _character == null)
		{
			return;
		}
		Vector3 vector = ((_state != HarvestState.Attacking || !IsRockLikeTarget()) ? GetEffectiveTargetPosition() : (_attackFaceLocked ? _lockedAttackFaceTarget : GetRockFacePosition()));
		Vector3 vector2 = vector - base.transform.position;
		vector2.y = 0f;
		if (vector2.sqrMagnitude < 0.1f)
		{
			vector = _target.transform.position;
		}
		_ai.LookAtPoint(vector);
		if (_state == HarvestState.Attacking)
		{
			Vector3 vector3 = vector - base.transform.position;
			vector3.y = 0f;
			if (vector3.sqrMagnitude > 0.001f)
			{
				Quaternion q = Quaternion.LookRotation(vector3.normalized);
				_character.SetLookDir(vector3.normalized);
				MHR_Nav.TurnTo(base.transform, q);
			}
		}
	}

	private int CountActiveRock5Sections()
	{
		if (_target == null)
		{
			return 0;
		}
		int num = 0;
		foreach (Transform item in _target.transform)
		{
			if (item.gameObject.activeSelf && item.GetComponent<Collider>() != null)
			{
				num++;
			}
		}
		return num;
	}

	private bool MHR_DoorBusy()
	{
		DoorHandler component = GetComponent<DoorHandler>();
		if (component == null)
		{
			return false;
		}
		return component.IsActive;
	}

	private bool IsLowTarget()
	{
		if (_targetType == "TreeLog" || _targetType == "Stump")
		{
			return true;
		}
		if (_targetType == "TreeBase" && _target != null)
		{
			float num = _target.transform.position.y - base.transform.position.y;
			if (num < -0.3f)
			{
				return true;
			}
		}
		return false;
	}

	private void LateUpdate()
	{
		if (_character == null || IsCompanionUIOpen())
		{
			return;
		}
		if (_state == HarvestState.Moving || (_state == HarvestState.CollectingDrops && _currentDrop != null))
		{
			bool flag = _targetType == "Pickable" && _state == HarvestState.Moving;
			_character.SetRun(!flag && _shouldRun);
		}
		// 1.5.60: not in a fight - this kept walking it to the stump mid-fight (user: "they got stuck on a tree when in combat")
		if (_state == HarvestState.Attacking && _target != null && _ai != null && !_ai.IsInCombat && !MHR_Loot.Active(this) && !_character.InAttack() && IsLowTarget() && !IsRockLikeTarget() && !HoldingGoodSpot)
		{
			Vector3 effectiveTargetPosition = GetEffectiveTargetPosition();
			float num = Vector3.Distance(base.transform.position, effectiveTargetPosition);
			float attackRange = GetAttackRange();
			bool flag2 = _swingCount >= 2 || _totalAttempts >= 2;
			float num2 = (flag2 ? (attackRange * 1f) : (attackRange * 1f));
			float dist = (flag2 ? (attackRange * 0.8f) : (attackRange * 0.8f));
			if (num > num2)
			{
				_ai.MHR_Go(Time.deltaTime, effectiveTargetPosition, dist, false, "Harvest");
			}
		}
	}

	private GameObject TryFindNearbyLog(Vector3 center)
	{
		return MHR_GatherRules.NearestLog(this, center);
	}

	private int CountExposedRock5Sections()
	{
		if (_target == null)
		{
			return 0;
		}
		int num = 0;
		foreach (Transform item in _target.transform)
		{
			if (item.gameObject.activeSelf)
			{
				Collider component = item.GetComponent<Collider>();
				if (!(component == null) && IsSegmentExposed(component))
				{
					num++;
				}
			}
		}
		return num;
	}

	private void FinishDropCollection()
	{
		_currentDrop = null;
		_state = HarvestState.Idle;
		_scanTimer = 0f;
		UnclaimTarget();
		bool flag = _setup != null && _setup.GetFollow();
		if (_ai != null && flag && MHR_Owner.Of(this) != null)
		{
			_ai.SetFollowTarget(MHR_Owner.Go(this));
		}
		Log($"FinishDropCollection — collected {_dropsPickedUp} items, follow={flag}");
	}

	private void TryPickupDrop(GameObject dropGO)
	{
		if (dropGO == null || _humanoid == null)
		{
			return;
		}
		if (IsOverweight())
		{
			Log($"Skipping pickup — overweight ({GetCurrentWeight():F1} >= {OverweightThreshold})");
			return;
		}
		ItemDrop component = dropGO.GetComponent<ItemDrop>();
		if (!(component == null))
		{
			string arg = component.m_itemData?.m_shared?.m_name ?? "?";
			int num = component.m_itemData?.m_stack ?? 0;
			if (_humanoid.Pickup(dropGO, autoequip: false, autoPickupDelay: false))
			{
				_dropsPickedUp++;
				Log($"Picked up: \"{arg}\" x{num} (total {_dropsPickedUp} this cycle)");
			}
			else
			{
				Log($"Failed to pick up: \"{arg}\" x{num} — inventory full or item invalid");
			}
		}
	}

	private GameObject ScanForDrops()
	{
		int num = Physics.OverlapSphereNonAlloc(_lastDestroyPos, DropScanRadius, _dropBuffer, _itemLayerMask);
		if (num > _dropBuffer.Length)
		{
			num = _dropBuffer.Length;
		}
		GameObject result = null;
		float num2 = float.MaxValue;
		int num3 = 0;
		_scanSeen = 0;
		_scanUnwanted = 0;
		_scanNoAuto = 0;
		_scanFull = 0;
		for (int i = 0; i < num; i++)
		{
			Collider collider = _dropBuffer[i];
			if (collider == null || collider.attachedRigidbody == null)
			{
				continue;
			}
			ItemDrop component = collider.attachedRigidbody.GetComponent<ItemDrop>();
			if (component == null)
			{
				continue;
			}
			_scanSeen++;
			if (!component.m_autoPickup)
			{
				_scanNoAuto++;
				continue;
			}
			ZNetView component2 = component.GetComponent<ZNetView>();
			if (component2 == null || !component2.IsValid())
			{
				continue;
			}
			if (!MHR_GatherRules.WantDrop(this, component))
			{
				_scanUnwanted++;
				continue;
			}
			if (IsOverweight())
			{
				continue;
			}
			Inventory inventory = _humanoid?.GetInventory();
			if (inventory != null)
			{
				component.Load();
				if (!inventory.CanAddItem(component.m_itemData) && !MHR_GatherRules.TryMakeRoom(this, component.m_itemData))
				{
					string text = component.m_itemData?.m_shared?.m_name ?? "?";
					Log("Drop scan: skipping \"" + text + "\" — inventory full/can't add");
					_scanFull++;
					continue;
				}
			}
			num3++;
			float num4 = Vector3.Distance(base.transform.position, component.transform.position);
			if (num4 < num2)
			{
				num2 = num4;
				result = component.gameObject;
			}
		}
		if (num3 > 0)
		{
			Log($"Drop scan: {num3} pickable items within {DropScanRadius}m of destroy pos");
		}
		return result;
	}

	private float GetCurrentWeight()
	{
		return (_humanoid?.GetInventory())?.GetTotalWeight() ?? 0f;
	}

	// 1.5.43: how many targets the last scan skipped because they need a better tool, and the first one's name (status)
	internal int LastTierSkips;

	internal string LastTierName;

	internal GameObject ScanForTarget(int mode)
	{
		if (mode == 5)
		{
			return ScanForPickable();
		}
		int num = Physics.OverlapSphereNonAlloc(ScanCenter(), ScanRad(), _scanBuffer);
		bool flag = num >= _scanBuffer.Length;
		if (num > _scanBuffer.Length)
		{
			num = _scanBuffer.Length;
		}
		LogDebug($"Physics scan: {num} colliders in {ScanRadius}m radius" + (flag ? " ** BUFFER FULL - may miss targets! **" : ""));
		int bestToolTier = GetBestToolTier(mode);
		LastTierSkips = 0;
		LastTierName = null;
		_seenIds.Clear();
		GameObject gameObject = null;
		float num2 = float.MaxValue;
		_bestTree = null;
		_bestTreeDist = float.MaxValue;
		// 1.5.61: Wood goes logs, then stumps, then standing trees (player report: "skipping wood logs most of the time")
		GameObject bestStump = null;
		float bestStumpDist = float.MaxValue;
		// 1.5.65: first the pieces of the log or tree it just broke (within 6 m of it, for 30 s)
		GameObject bestOwn = null;
		float bestOwnDist = float.MaxValue;

		int num3 = 0;
		int num4 = 0;
		int num5 = 0;
		int num6 = 0;
		int num7 = 0;
		int num8 = 0;
		int outOfRange = 0;
		for (int i = 0; i < num; i++)
		{
			Collider collider = _scanBuffer[i];
			if (collider == null)
			{
				continue;
			}
			string type;
			GameObject harvestCandidateWithType = GetHarvestCandidateWithType(collider, mode, out type);
			if (harvestCandidateWithType == null || !MHR_GatherRules.AllowTarget(this, harvestCandidateWithType, mode) || !_seenIds.Add(harvestCandidateWithType.GetInstanceID()))
			{
				continue;
			}
			// 1.5.61: Stone and Ore leave buildings alone - built pieces and walls (player report: mining stone walls for stone)
			if ((mode == 2 || mode == 3) && IsBuilt(harvestCandidateWithType))
			{
				continue;
			}
			int minToolTier = GetMinToolTier(harvestCandidateWithType);
			if (minToolTier > bestToolTier)
			{
				num8++;
				LastTierName = LastTierName ?? MHR_Items.NiceName(harvestCandidateWithType);
				if (num8 <= 3)
				{
					LogWarn($"  Tier SKIP \"{harvestCandidateWithType.name}\" — requires tier {minToolTier}, " + $"best tool tier={bestToolTier} (need bronze pickaxe tier≥1 for copper/silver)");
				}
			}
			else if (_blacklist.ContainsKey(harvestCandidateWithType.GetInstanceID()))
			{
				if (num3 == 0)
				{
					Log($"  Skipping blacklisted \"{harvestCandidateWithType.name}\" (id={harvestCandidateWithType.GetInstanceID()})");
				}
			}
			else
			{
				if (s_claimedTargets.Contains(harvestCandidateWithType.GetInstanceID()))
				{
					continue;
				}
				bool own = !TooFarFromYou() && IsOwnPiece(harvestCandidateWithType, type);
				if (!own && !InWorkArea(harvestCandidateWithType.transform.position, type))
				{
					outOfRange++;
					continue;
				}
				num3++;
				float num11 = Vector3.Distance(base.transform.position, harvestCandidateWithType.transform.position);
				bool flag2 = type == "TreeBase";
				if (flag2)
				{
					num4++;
				}
				else if (type == "TreeLog")
				{
					num5++;
				}
				else if (type == "Stump")
				{
					num6++;
				}
				else
				{
					num7++;
				}
				if (num3 <= 5 || type == "TreeLog")
				{
					Log($"  Candidate #{num3}: \"{harvestCandidateWithType.name}\" " + $"type={type} dist={num11:F1}m pos={harvestCandidateWithType.transform.position:F1}");
				}
				if (flag2)
				{
					if (num11 < _bestTreeDist)
					{
						_bestTreeDist = num11;
						_bestTree = harvestCandidateWithType;
					}
				}
				else if (own && num11 < bestOwnDist)
				{
					bestOwnDist = num11;
					bestOwn = harvestCandidateWithType;
					if (num11 < num2)
					{
						num2 = num11;
						gameObject = harvestCandidateWithType;
					}
				}
				else if (type == "Stump")
				{
					if (num11 < bestStumpDist)
					{
						bestStumpDist = num11;
						bestStump = harvestCandidateWithType;
					}
				}
				else if (num11 < num2)
				{
					num2 = num11;
					gameObject = harvestCandidateWithType;
				}
			}
		}
		if (num3 > 5)
		{
			Log($"  ...and {num3 - 5} more candidates");
		}
		if (num8 > 0)
		{
			Log($"  Skipped {num8} targets requiring higher tool tier (best tier={bestToolTier})");
		}
		LastTierSkips = num8;
		string text = mode switch
		{
			3 => "Ore", 
			1 => "Wood", 
			_ => "Stone", 
		};
		if (mode == 1)
		{
			Log($"  Breakdown [{text}]: {num4} TreeBase, {num5} TreeLog, {num6} Stump " + $"(total {num3} unique targets, {outOfRange} out of range)");
		}
		else
		{
			Log($"  Breakdown [{text}]: {num7} deposits (total {num3} unique targets)");
		}
		if (bestOwn != null)
		{
			gameObject = bestOwn;
			num2 = bestOwnDist;
			Log("Finishing the log it just broke");
		}
		if (gameObject == null && bestStump != null)
		{
			gameObject = bestStump;
			num2 = bestStumpDist;
			Log("No logs found - taking the nearest stump");
		}
		if (gameObject == null && _bestTree != null)
		{
			gameObject = _bestTree;
			num2 = _bestTreeDist;
			Log("No logs/stumps found — falling back to standing tree");
		}
		if (gameObject != null)
		{
			Log($"Best target: \"{gameObject.name}\" dist={num2:F1}m " + $"(out of {num3} total candidates)");
		}
		return gameObject;
	}

	// 1.4.122: loose pickups for the Wood and Stone jobs
	private bool LooseOn(int mode, out bool noTool)
	{
		noTool = BestToolQuiet(mode) == null;
		if (!(ModConfig.HarvestPickUpLoose?.Value ?? true))
		{
			return false;
		}
		return EarlyGame();
	}

	// 1.4.123: only early in the game (user: "make the pickup work early game where grabbing those items is useful"):
	// no bronze-or-better axe anywhere - the companion's pack, the chests near home, your pack - and no forge at home
	// to make one. Checked every 30 s.
	private float _nextEarlyCheck;

	private bool _early;

	private bool EarlyGame()
	{
		if (Time.time < _nextEarlyCheck)
		{
			return _early;
		}
		_nextEarlyCheck = Time.time + 30f;
		bool was = _early;
		string why = null;
		bool home = _setup != null && _setup.HasHomePosition();
		Vector3 center = home ? _setup.GetHomePosition() : base.transform.position;
		float radius = home ? MHR_ToolKeeper.HomeRange() : 30f;
		if (HasGoodAxe(_humanoid?.GetInventory()))
		{
			why = "it has a bronze-or-better axe";
		}
		else if (MHR_Owner.IsMine(this) && HasGoodAxe(Player.m_localPlayer.GetInventory()))
		{
			why = "you have a bronze-or-better axe";
		}
		else
		{
			foreach (Container c in MHR_Chests.Near(center, radius))
			{
				if (c != null && c.GetComponent<Character>() == null && HasGoodAxe(c.GetInventory()))
				{
					why = "there's a bronze-or-better axe in a chest";
					break;
				}
			}
			if (why == null)
			{
				foreach (CraftingStation st in MHR_Chests.Stations(center, radius))
				{
					if (st != null && st.m_name == "$piece_forge")
					{
						why = "there's a forge" + (home ? " at home" : " nearby");
						break;
					}
				}
			}
		}
		_early = why == null;
		if (_early != was || !_earlyLogged)
		{
			_earlyLogged = true;
			LogInfo(_early ? "Early game (no bronze-or-better axe and no forge) - picking up branches, stones and flint" : "Not picking up branches, stones and flint: " + why);
		}
		return _early;
	}

	private bool _earlyLogged;

	private static bool HasGoodAxe(Inventory inv)
	{
		if (inv == null)
		{
			return false;
		}
		foreach (ItemDrop.ItemData it in inv.GetAllItems())
		{
			if (it?.m_shared == null || it.m_shared.m_skillType != Skills.SkillType.Axes || it.m_shared.m_damages.m_chop <= 0f)
			{
				continue;
			}
			string n = it.m_dropPrefab != null ? it.m_dropPrefab.name : "";
			if (n != "AxeStone" && n != "AxeFlint")
			{
				return true;
			}
		}
		return false;
	}

	// the best tool for the job without the logging (FindBestTool logs every candidate)
	private ItemDrop.ItemData BestToolQuiet(int mode)
	{
		Inventory inv = _humanoid?.GetInventory();
		if (inv == null)
		{
			return null;
		}
		ItemDrop.ItemData best = null;
		float bestDmg = 0f;
		foreach (ItemDrop.ItemData it in inv.GetAllItems())
		{
			if (it?.m_shared == null || (it.m_shared.m_useDurability && it.m_durability <= 0f))
			{
				continue;
			}
			if (it.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Bow || !string.IsNullOrEmpty(it.m_shared.m_ammoType) || it.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Ammo)
			{
				continue;
			}
			HitData.DamageTypes d = it.GetDamage();
			float dmg = mode == 1 ? d.m_chop : d.m_pickaxe;
			if (dmg > bestDmg)
			{
				bestDmg = dmg;
				best = it;
			}
		}
		return best;
	}

	private GameObject ScanForPickable(int looseMode = 0)
	{
		int num = Physics.OverlapSphereNonAlloc(ScanCenter(), ScanRad(), _scanBuffer);
		if (num > _scanBuffer.Length)
		{
			num = _scanBuffer.Length;
		}
		Log($"Forage scan: {num} colliders in {ScanRadius}m radius");
		_seenIds.Clear();
		GameObject gameObject = null;
		float num2 = float.MaxValue;
		int num3 = 0;
		for (int i = 0; i < num; i++)
		{
			Collider collider = _scanBuffer[i];
			if (collider == null)
			{
				continue;
			}
			Pickable componentInParent = collider.GetComponentInParent<Pickable>();
			if (componentInParent == null || (looseMode == 0 ? (!MHR_ForageAllowed(componentInParent) || !ForageUnderAmount(componentInParent)) : !MHR_GatherRules.AllowLoose(this, componentInParent, looseMode)))
			{
				continue;
			}
			GameObject gameObject2 = componentInParent.gameObject;
			if (!_seenIds.Add(gameObject2.GetInstanceID()))
			{
				continue;
			}
			ZNetView component = gameObject2.GetComponent<ZNetView>();
			if ((component != null && component.GetZDO() != null && component.GetZDO().GetBool(s_pickedHash)) || _blacklist.ContainsKey(gameObject2.GetInstanceID()))
			{
				continue;
			}
			if (!InWorkArea(gameObject2.transform.position))
			{
				continue;
			}
			num3++;
			float num6 = Vector3.Distance(base.transform.position, gameObject2.transform.position);
			if (num3 <= 5)
			{
				Log($"  Pickable #{num3}: \"{gameObject2.name}\" dist={num6:F1}m " + $"pos={gameObject2.transform.position:F1}");
			}
			if (num6 < num2)
			{
				num2 = num6;
				gameObject = gameObject2;
			}
		}
		if (num3 > 5)
		{
			Log($"  ...and {num3 - 5} more pickables");
		}
		Log($"  Forage breakdown: {num3} pickable targets");
		if (gameObject != null)
		{
			Log($"Best pickable: \"{gameObject.name}\" dist={num2:F1}m " + $"(out of {num3} total)");
		}
		else if (num3 == 0)
		{
			LogDebug($"Forage scan: no pickables found within {ScanRadius}m");
		}
		return gameObject;
	}

	// part of a building: a placed piece (yours or a location's) or anything named as a wall
	private static bool IsBuilt(GameObject go)
	{
		return go.GetComponentInParent<Piece>() != null || go.GetComponentInParent<WearNTear>() != null || go.name.IndexOf("wall", StringComparison.OrdinalIgnoreCase) >= 0;
	}

	private static GameObject GetHarvestCandidateWithType(Collider col, int mode, out string type)
	{
		type = null;
		switch (mode)
		{
		case 1:
		{
			TreeBase componentInParent6 = col.GetComponentInParent<TreeBase>();
			if (componentInParent6 != null)
			{
				type = "TreeBase";
				return componentInParent6.gameObject;
			}
			TreeLog componentInParent7 = col.GetComponentInParent<TreeLog>();
			if (componentInParent7 != null)
			{
				type = "TreeLog";
				return componentInParent7.gameObject;
			}
			Destructible componentInParent8 = col.GetComponentInParent<Destructible>();
			if (componentInParent8 != null && componentInParent8.m_damages.m_chop != HitData.DamageModifier.Immune && componentInParent8.gameObject.name.IndexOf("stub", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				type = "Stump";
				return componentInParent8.gameObject;
			}
			break;
		}
		case 3:
		{
			MineRock5 componentInParent3 = col.GetComponentInParent<MineRock5>();
			if (componentInParent3 != null)
			{
				type = "MineRock5";
				return componentInParent3.gameObject;
			}
			MineRock componentInParent4 = col.GetComponentInParent<MineRock>();
			if (componentInParent4 != null)
			{
				bool flag = HasOreDrops(componentInParent4.m_dropItems);
				bool flag2 = IsOreNamedRock(componentInParent4.gameObject.name);
				if (flag || flag2)
				{
					type = "MineRock";
					return componentInParent4.gameObject;
				}
			}
			Destructible componentInParent5 = col.GetComponentInParent<Destructible>();
			if (componentInParent5 != null && componentInParent5.m_damages.m_pickaxe != HitData.DamageModifier.Immune && (componentInParent5.m_damages.m_chop == HitData.DamageModifier.Immune || componentInParent5.m_damages.m_chop == HitData.DamageModifier.Ignore))
			{
				DropOnDestroyed component = componentInParent5.GetComponent<DropOnDestroyed>();
				bool flag3 = component != null && HasOreDrops(component.m_dropWhenDestroyed);
				bool flag4 = IsOreNamedRock(componentInParent5.gameObject.name);
				if (flag3 || flag4)
				{
					type = "Destructible";
					return componentInParent5.gameObject;
				}
			}
			break;
		}
		default:
		{
			MineRock componentInParent = col.GetComponentInParent<MineRock>();
			if (componentInParent != null && !HasOreDrops(componentInParent.m_dropItems) && !IsOreNamedRock(componentInParent.gameObject.name))
			{
				type = "MineRock";
				return componentInParent.gameObject;
			}
			Destructible componentInParent2 = col.GetComponentInParent<Destructible>();
			if (componentInParent2 != null && componentInParent2.m_damages.m_pickaxe != HitData.DamageModifier.Immune && (componentInParent2.m_damages.m_chop == HitData.DamageModifier.Immune || componentInParent2.m_damages.m_chop == HitData.DamageModifier.Ignore))
			{
				type = "Destructible";
				return componentInParent2.gameObject;
			}
			break;
		}
		}
		return null;
	}

	internal static bool MHR_ForageAllowed(Pickable p)
	{
		if (!IsForageItemAllowed(p))
		{
			return false;
		}
		string text = p.gameObject.name;
		if (!text.StartsWith("Pickable_Turnip") && !text.StartsWith("Pickable_Carrot") && !text.StartsWith("Pickable_Onion") && (text.Contains("_Wild") || (!text.StartsWith("Pickable_Barley") && !text.StartsWith("Pickable_Flax"))) && !FarmController.MHR_SafeCultivated(p.transform.position))
		{
			return true;
		}
		return false;
	}

	// 1.4.106: the shared list format (MHR_List): *, Name, -Name, Name:Number
	private static bool IsForageItemAllowed(Pickable pickable)
	{
		if (pickable.m_itemPrefab == null)
		{
			return false;
		}
		string prefabName = Utils.GetPrefabName(pickable.m_itemPrefab);
		string local = MHR_List.Local(pickable.m_itemPrefab.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_name);
		return MHR_List.Get(ModConfig.ForageItems.Value).Allows(prefabName, local);
	}

	// Name:Number in ForageItems: stop picking that item once there are that many (pack + chests near home)
	private bool ForageUnderAmount(Pickable pickable)
	{
		if (pickable.m_itemPrefab == null)
		{
			return true;
		}
		string prefabName = Utils.GetPrefabName(pickable.m_itemPrefab);
		int amount = MHR_List.Get(ModConfig.ForageItems.Value).AmountFor(prefabName, 0);
		return amount <= 0 || MHR_GatherRules.Stock(this, prefabName) < amount;
	}

	private static void RefreshForageFilter()
	{
		string value = ModConfig.ForageItems.Value;
		if (value == _forageFilterRaw)
		{
			return;
		}
		_forageFilterRaw = value;
		if (string.IsNullOrWhiteSpace(value) || value.Trim() == "*")
		{
			_forageFilter = null;
			return;
		}
		_forageFilter = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		string[] array = value.Split(new char[1] { ',' });
		for (int i = 0; i < array.Length; i++)
		{
			string text = array[i].Trim();
			if (text.Length > 0)
			{
				_forageFilter.Add(text);
			}
		}
	}

	private static bool DropsOnlyStone(DropTable table)
	{
		if (table == null || table.m_drops.Count == 0)
		{
			return true;
		}
		foreach (DropTable.DropData drop in table.m_drops)
		{
			if (!(drop.m_item == null))
			{
				string text = drop.m_item.name;
				if (text != "Stone" && text != "Flint")
				{
					return false;
				}
			}
		}
		return true;
	}

	internal Container FindNearestChest(Vector3 searchCenter)
	{
		return MHR_GatherRules.DropOff(this, searchCenter);
	}

	private bool IsTreeTarget()
	{
		if (!(_targetType == "TreeBase"))
		{
			return _targetType == "TreeLog";
		}
		return true;
	}
}
