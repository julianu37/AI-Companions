using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Companions;

public class CompanionRest : MonoBehaviour
{
	private static readonly List<CompanionRest> _instances = new List<CompanionRest>();

	private static readonly MethodInfo _bedGetOwnerMethod = AccessTools.Method(typeof(Bed), "GetOwner");

	private ZNetView _nview;

	private Character _character;

	private CompanionAI _ai;

	private CompanionStamina _stamina;

	private ZSyncAnimation _zanim;

	private CompanionSetup _setup;

	private Rigidbody _body;

	private bool _isSitting;

	private float _checkTimer;

	private float _healTimer;

	private GameObject _fireTarget;

	private bool _navigating;

	private Vector3 _navTarget;

	private float _navTimeout;

	private static readonly HashSet<int> _claimedBeds = new HashSet<int>();

	internal static readonly Dictionary<int, string> _bedClaimNames = new Dictionary<int, string>();

	internal static readonly int MHR_BedClaimNameHash = "HC_BedClaimName".GetStableHashCode();

	internal static readonly int MHR_BedClaimIdHash = "HC_BedClaimId".GetStableHashCode();

	private Bed _claimedBed;

	// 1.5.1: first look a few seconds after loading in - the base around it is still being built then
	private float _bedClaimScanTimer = 5f;

	// 1.5.1: when its claimed bed was first not found (its area may still be loading)
	private float _bedMissingSince = -1f;

	// 1.5.1: why the last bed search found nothing (logged when it changes)
	private string _bedFailReason;

	private bool _needsBed;

	private bool _isAutoSleep;

	private float _autoSleepScanTimer;

	private float _mhrNoAutoSleepUntil;

	private bool _mhrStayAwake;

	private const float CheckInterval = 1f;

	private const float HealInterval = 0.5f;

	private const float DirectedMaxDist = 50f;

	private const int FireScanBufferSize = 48;

	private const float AutoSleepBedRange = 20f;

	private const float AutoSleepScanInterval = 5f;

	private readonly Collider[] _fireScanBuffer = new Collider[48];

	private readonly HashSet<int> _seenFireIds = new HashSet<int>();

	private bool _isDirected;

	private bool _isSleeping;

	private Bed _bedTarget;

	private float _directedRestTimer;

	internal static IReadOnlyList<CompanionRest> Instances => _instances;

	// 1.5.1: the player just set a new spawn point (slept in a bed) - companions check their bed straight away, so one
	// whose bed that is gives it up now rather than up to 30 s later
	internal static void MHR_RecheckBedsSoon()
	{
		foreach (CompanionRest instance in _instances)
		{
			if (instance != null)
			{
				instance._bedClaimScanTimer = 0f;
			}
		}
	}

	private static float HealRate => ModConfig.RestHealRate.Value;

	private static float PlayerRange => ModConfig.RestPlayerRange.Value;

	private static float FireRange => ModConfig.RestFireRange.Value;

	private static float DirectedSitTimeout => ModConfig.RestDirectedTimeout.Value;

	private bool HasPlayerAnims
	{
		get
		{
			if (_setup != null)
			{
				return _setup.CanWearArmor();
			}
			return false;
		}
	}

	public bool IsResting
	{
		get
		{
			if (!_isSitting)
			{
				return _isSleeping;
			}
			return true;
		}
	}

	public bool IsSleeping => _isSleeping || Time.time < _wakeUntil;

	public bool IsNavigating => _navigating;

	// 1.4.147: sitting by a fire / going to bed because the player said so - an order the jobs wait for
	public bool IsDirectedOrder => _isDirected && (_navigating || _isSitting || _isSleeping);

	public Vector3 NavTarget => _navTarget;

	public bool IsDirectedResting
	{
		get
		{
			if (_isDirected)
			{
				if (!_isSitting)
				{
					return _isSleeping;
				}
				return true;
			}
			return false;
		}
	}

	public void CancelDirected()
	{
		if (_isDirected || _isSitting || _isSleeping || _navigating)
		{
			StopAll();
		}
	}

	// 1.4.152: its own player in bed (not whoever's game is running it)
	private bool OwnerInBed()
	{
		Player p = MHR_Owner.Of(this);
		return p != null && p.InBed();
	}

	internal static bool MHR_LocalPlayerInBed()
	{
		Player localPlayer = Player.m_localPlayer;
		if (localPlayer == null)
		{
			return false;
		}
		return localPlayer.InBed();
	}

	internal bool MHR_WantsBedNow()
	{
		if (!_isSleeping && !(_nview == null) && _nview.IsOwner() && ModConfig.SleepWhenPlayerSleeps.Value)
		{
			return MHR_BedInRange();
		}
		return false;
	}

	internal static void MHR_ClearStayAwakeAll()
	{
		for (int i = 0; i < _instances.Count; i++)
		{
			CompanionRest companionRest = _instances[i];
			if (!(companionRest == null))
			{
				companionRest._mhrStayAwake = false;
			}
		}
	}

	internal void MHR_SuppressAutoSleep(float seconds)
	{
		_mhrNoAutoSleepUntil = Time.time + seconds;
	}

	internal static long GetBedOwnerSafe(Bed bed)
	{
		if ((object)bed == null)
		{
			return 0L;
		}
		if ((object)_bedGetOwnerMethod == null)
		{
			return 0L;
		}
		try
		{
			return (long)_bedGetOwnerMethod.Invoke(bed, null);
		}
		catch (Exception)
		{
			return 0L;
		}
	}

	internal static string MHR_GetBedClaimName(Bed bed)
	{
		if (!(bed == null))
		{
			ZNetView component = bed.GetComponent<ZNetView>();
			if (!(component == null))
			{
				ZDO zDO = component.GetZDO();
				if (zDO != null)
				{
					string text = zDO.GetString(MHR_BedClaimNameHash);
					if (!string.IsNullOrEmpty(text))
					{
						return text;
					}
				}
			}
		}
		return null;
	}

	public void ArriveAtNavTarget()
	{
		if (_navigating)
		{
			_navigating = false;
			CompanionsPlugin.Log.LogDebug($"[Rest] Arrived at nav target — bed={_bedTarget != null}, fire={_fireTarget != null}");
			if (_bedTarget != null)
			{
				FinalizeSleep();
			}
			else if (_fireTarget != null)
			{
				StartSitting(_fireTarget);
			}
		}
	}

	internal static void MHR_StayAwakeForRecall(CompanionSetup setup)
	{
		if (!(setup == null))
		{
			CompanionRest component = setup.GetComponent<CompanionRest>();
			if (!(component == null))
			{
				component._mhrStayAwake = true;
				CompanionsPlugin.Log.LogInfo("[Rest] Recalled - staying awake until you next go to bed");
			}
		}
	}

	public void DirectSit(GameObject fire)
	{
		if (fire == null)
		{
			return;
		}
		CompanionsPlugin.Log.LogDebug($"[Rest] DirectSit called — sitting={_isSitting}, sleeping={_isSleeping}, " + $"directed={_isDirected}, navigating={_navigating}, fire=\"{fire.name}\"");
		if ((_isSitting || _navigating) && _isDirected)
		{
			CompanionsPlugin.Log.LogDebug("[Rest] DirectSit TOGGLE OFF — calling StopAll");
			StopAll();
			return;
		}
		if (_isSitting || _isSleeping || _navigating)
		{
			StopAll();
		}
		_isDirected = true;
		_fireTarget = fire;
		_directedRestTimer = DirectedSitTimeout;
		float num = Vector3.Distance(base.transform.position, fire.transform.position);
		if (num < 3f)
		{
			StartSitting(fire);
			return;
		}
		_navigating = true;
		_navTarget = fire.transform.position;
		_navTimeout = 15f;
		if (_ai != null)
		{
			_ai.SetFollowTarget(null);
		}
		CompanionsPlugin.Log.LogDebug($"[Rest] Navigating to fire — dist={num:F1}m, timeout=15s");
	}

	public bool DirectSleep(Bed bed)
	{
		if (bed == null)
		{
			return true;
		}
		if (!MHR_BedOwnerOk(bed))
		{
			return false;
		}
		if (!(_claimedBed == bed))
		{
			if (_claimedBeds.Contains(bed.GetInstanceID()))
			{
				return false;
			}
			ClaimBed(bed);
		}
		CompanionsPlugin.Log.LogDebug($"[Rest] DirectSleep called — sleeping={_isSleeping}, sitting={_isSitting}, " + $"directed={_isDirected}, navigating={_navigating}, bed=\"{bed.name}\" myPos={base.transform.position:F2}");
		if (_isSleeping || _navigating)
		{
			CompanionsPlugin.Log.LogDebug("[Rest] DirectSleep TOGGLE OFF — calling StopAll");
			StopAll();
			return true;
		}
		BeginSleep(bed, isAuto: false);
		return true;
	}

	private void Awake()
	{
		// 1.4.102: not straight back to bed while the world loads in (user: "loading into the game the NPC keeps going
		// back to sleep" - right after loading, the time of day can still read as night for a moment)
		_mhrNoAutoSleepUntil = Time.time + 20f;
		_nview = GetComponent<ZNetView>();
		_character = GetComponent<Character>();
		_ai = GetComponent<CompanionAI>();
		_stamina = GetComponent<CompanionStamina>();
		_zanim = GetComponent<ZSyncAnimation>();
		_setup = GetComponent<CompanionSetup>();
		_body = GetComponent<Rigidbody>();
	}

	private void OnEnable()
	{
		if (!_instances.Contains(this))
		{
			_instances.Add(this);
		}
	}

	private void OnDisable()
	{
		_instances.Remove(this);
	}

	private bool _bedPinned;

	private Vector3 _bedPinPos;

	private Quaternion _bedPinRot;

	private Collider[] _bedCols;

	// keep the sleeping companion on the bed (see FinalizeSleep)
	// 1.4.105: getting up happens in the bed, like a player (user: "they should get up in bed like a player" - 1.4.104
	// moved him to the floor first, so the get-up animation played lying on the floor); then he steps down beside it
	private float _wakeUntil;

	private Bed _wakeBed;

	private void PinToBed()
	{
		if (!_bedPinned || (!_isSleeping && Time.time >= _wakeUntil))
		{
			return;
		}
		base.transform.position = _bedPinPos;
		base.transform.rotation = _bedPinRot;
		if (_body != null && !_body.isKinematic)
		{
			_body.position = _bedPinPos;
			_body.linearVelocity = Vector3.zero;
			_body.useGravity = false;
		}
	}

	private void IgnoreBed(Bed bed, bool ignore)
	{
		Collider component = GetComponent<CapsuleCollider>();
		if (ignore)
		{
			if (bed == null)
			{
				return;
			}
			Piece componentInParent = bed.GetComponentInParent<Piece>();
			_bedCols = ((componentInParent != null) ? componentInParent.GetComponentsInChildren<Collider>() : bed.GetComponentsInChildren<Collider>());
		}
		if (component == null || _bedCols == null)
		{
			return;
		}
		Collider[] bedCols = _bedCols;
		foreach (Collider collider in bedCols)
		{
			if (collider != null)
			{
				Physics.IgnoreCollision(component, collider, ignore);
			}
		}
		if (!ignore)
		{
			_bedCols = null;
		}
	}

	private void LateUpdate()
	{
		PinToBed();
	}

	private void FixedUpdate()
	{
		PinToBed();
	}

	private void Update()
	{
		if (_nview == null || !_nview.IsOwner() || _character == null || _character.GetHealth() <= 0f)
		{
			return;
		}
		if (_wakeBed != null || (_bedPinned && !_isSleeping))
		{
			if (Time.time < _wakeUntil)
			{
				return;
			}
			FinishGetUp();
		}
		if (_navigating)
		{
			UpdateNavigation();
			return;
		}
		if (_isSleeping)
		{
			UpdateSleeping();
			return;
		}
		if (_isSitting)
		{
			UpdateSitting();
			return;
		}
		_checkTimer -= Time.deltaTime;
		if (!(_checkTimer > 0f))
		{
			_checkTimer = 1f;
			TryStartSitting();
			TryAutoSleep();
			// 1.5.1: this runs once a second - it took off one frame's time, so the 30 s bed search ran about every 30 minutes
			_bedClaimScanTimer -= 1f;
			if (_bedClaimScanTimer <= 0f)
			{
				_bedClaimScanTimer = 30f;
				TryClaimBedIfNeeded();
			}
		}
	}

	private void UpdateNavigation()
	{
		if (MHR_Mover.KeepAlive(this))
		{
			_navTimeout = Mathf.Max(_navTimeout, 6f);
		}
		_navTimeout -= Time.deltaTime;
		if (_navTimeout <= 0f)
		{
			CompanionsPlugin.Log.LogDebug("[Rest] Navigation timed out — cancelling");
			CancelNavigation();
		}
		else if (_bedTarget == null && _fireTarget == null)
		{
			CompanionsPlugin.Log.LogDebug("[Rest] Navigation target destroyed — cancelling");
			CancelNavigation();
		}
	}

	private void UpdateSleeping()
	{
		if (_isAutoSleep && EnvMan.instance != null && !EnvMan.IsNight() && !OwnerInBed())
		{
			CompanionsPlugin.Log.LogDebug("[Rest] Auto-sleep — day started, waking up");
			_isAutoSleep = false;
			_isDirected = false;
			StopSleeping();
			return;
		}
		if (HasEnemyNearby())
		{
			CompanionsPlugin.Log.LogDebug("[Rest] UpdateSleeping — enemy nearby, waking up!");
			_isAutoSleep = false;
			_isDirected = false;
			StopSleeping();
			return;
		}
		if (_bedTarget == null || !_bedTarget)
		{
			CompanionsPlugin.Log.LogDebug("[Rest] UpdateSleeping — bed destroyed/null, waking up!");
			_isAutoSleep = false;
			_isDirected = false;
			StopSleeping();
			return;
		}
		Transform transform = ((_bedTarget.m_spawnPoint != null) ? _bedTarget.m_spawnPoint : _bedTarget.transform);
		base.transform.position = transform.position;
		base.transform.rotation = transform.rotation;
		if (_body != null)
		{
			_body.position = transform.position;
			_body.linearVelocity = Vector3.zero;
		}
		_healTimer -= Time.deltaTime;
		if (_healTimer <= 0f)
		{
			_healTimer = 0.5f;
			float maxHealth = _character.GetMaxHealth();
			float health = _character.GetHealth();
			if (health < maxHealth)
			{
				_character.Heal(HealRate * 0.5f);
			}
		}
	}

	private void UpdateSitting()
	{
		bool flag = false;
		if (_fireTarget == null || !_fireTarget)
		{
			flag = true;
		}
		else
		{
			Fireplace component = _fireTarget.GetComponent<Fireplace>();
			if (component != null && !component.IsBurning())
			{
				flag = true;
			}
		}
		if (HasEnemyNearby())
		{
			flag = true;
		}
		if (_isDirected && !flag)
		{
			_directedRestTimer -= Time.deltaTime;
			if (_directedRestTimer <= 0f)
			{
				CompanionsPlugin.Log.LogDebug("[Rest] Directed sit timed out — stopping");
				flag = true;
			}
			else
			{
				Player localPlayer = MHR_Owner.Of(this);
				if (localPlayer != null)
				{
					float num = Vector3.Distance(base.transform.position, localPlayer.transform.position);
					if (num > 50f)
					{
						CompanionsPlugin.Log.LogDebug($"[Rest] Directed sit — player too far ({num:F1}m > {50f}m), stopping");
						flag = true;
					}
				}
			}
		}
		if (!_isDirected && !flag)
		{
			// 1.4.152: the sit emote is the local player's - only for their own companions
			Player localPlayer2 = MHR_Owner.IsMine(this) ? Player.m_localPlayer : null;
			if (localPlayer2 == null)
			{
				flag = true;
			}
			else
			{
				if ((_nview?.GetZDO()?.GetInt(CompanionSetup.ActionModeHash)).GetValueOrDefault() != 0)
				{
					flag = true;
				}
				if (Player.LastEmote == null || !Player.LastEmote.Equals("sit", StringComparison.OrdinalIgnoreCase) || (DateTime.Now - Player.LastEmoteTime).TotalSeconds > 30.0)
				{
					flag = true;
				}
				if (!flag)
				{
					float num2 = Vector3.Distance(base.transform.position, localPlayer2.transform.position);
					if (num2 > PlayerRange * 1.5f)
					{
						flag = true;
					}
				}
			}
		}
		if (flag)
		{
			StopSitting();
			return;
		}
		_healTimer -= Time.deltaTime;
		if (_healTimer <= 0f)
		{
			_healTimer = 0.5f;
			float maxHealth = _character.GetMaxHealth();
			float health = _character.GetHealth();
			if (health < maxHealth)
			{
				_character.Heal(HealRate * 0.5f);
			}
		}
	}

	private void TryStartSitting()
	{
		// 1.4.152: the sit emote is the local player's - only their own companions sit down with them
		Player localPlayer = MHR_Owner.IsMine(this) ? Player.m_localPlayer : null;
		if (localPlayer == null)
		{
			return;
		}
		ZDO zDO = _nview.GetZDO();
		if (zDO == null || zDO.GetInt(CompanionSetup.ActionModeHash) != 0 || Player.LastEmote == null || !Player.LastEmote.Equals("sit", StringComparison.OrdinalIgnoreCase) || (DateTime.Now - Player.LastEmoteTime).TotalSeconds > 30.0)
		{
			return;
		}
		float num = Vector3.Distance(base.transform.position, localPlayer.transform.position);
		if (!(num > PlayerRange))
		{
			GameObject gameObject = FindNearbyFire();
			if (!(gameObject == null) && !HasEnemyNearby())
			{
				StartSitting(gameObject);
			}
		}
	}

	private void TryAutoSleep()
	{
		if (MHR_ToolKeeper.IsBusy(this))
		{
			return;
		}
		bool flag = default(bool);
		if (ModConfig.SleepWhenPlayerSleeps.Value && OwnerInBed())
		{
			flag = true;
			_mhrStayAwake = false;
		}
		else if (Time.time < _mhrNoAutoSleepUntil || _mhrStayAwake || !ModConfig.SleepAtNight.Value || EnvMan.instance == null || !EnvMan.IsNight())
		{
			return;
		}
		ZDO zDO = _nview?.GetZDO();
		if (zDO == null || HasEnemyNearby())
		{
			return;
		}
		if (!flag)
		{
			_autoSleepScanTimer -= 1f;
			if (_autoSleepScanTimer > 0f)
			{
				return;
			}
			_autoSleepScanTimer = 5f;
		}
		if (!(_claimedBed != null))
		{
			TryClaimBedIfNeeded();
		}
		Bed claimedBed = _claimedBed;
		if (claimedBed == null)
		{
			_needsBed = true;
			return;
		}
		_needsBed = false;
		if (MHR_BedInRange())
		{
			CompanionsPlugin.Log.LogInfo(flag ? "[Rest] Going to bed: you went to bed" : "[Rest] Going to bed: it's night and the bed is in range");
			// 1.5.43 (phase 3): action line
			GetComponent<CompanionTalk>()?.Speak(ModLocalization.Loc("hc_act_bed"), CompanionTalk.Priority.Event, "act:bed");
			CompanionsPlugin.Log.LogDebug($"[Rest] Auto-sleep — night detected, bed=\"{claimedBed.name}\" at {claimedBed.transform.position:F1}");
			BeginSleep(claimedBed, isAuto: true);
		}
	}

	public void TryClaimBedIfNeeded()
	{
		if (_claimedBed != null)
		{
			if (MHR_BedOwnerOk(_claimedBed, keeping: true))
			{
				string value = MHR_MyName();
				_bedClaimNames[_claimedBed.GetInstanceID()] = value;
				MHR_SetBedClaim(_claimedBed, value, MHR_MyId());
				return;
			}
			CompanionsPlugin.Log.LogInfo("[Rest] Its bed now belongs to a player (or is its owner's spawn bed) - looking for another");
			ReleaseClaimedBed();
			if (_nview != null)
			{
				_nview.GetZDO()?.Set(CompanionSetup.HasHomeBedHash, value: false);
			}
		}
		else if (_nview != null)
		{
			ZDO zDO = _nview.GetZDO();
			if (zDO != null && zDO.GetBool(CompanionSetup.HasHomeBedHash))
			{
				Vector3 vec = zDO.GetVec3(CompanionSetup.HomeBedPosHash, Vector3.zero);
				Bed bed = FindAnyBedNear(vec, 2f);
				// 1.5.1: the game builds a loaded area a few objects a frame, so right after loading in the bed may not be
				// there yet - it used to give the bed up on the spot and lose it for good. Now it waits up to a minute.
				// 1.5.2: a bed far away (we're out hunting, its area isn't loaded) isn't gone - only look for it when near it
				// (user log: "Its bed at (-273, 40, -314) is gone" with the companion 2700 m away)
				if (bed == null && Vector3.Distance(base.transform.position, vec) > 40f)
				{
					return;
				}
				if (bed == null)
				{
					if (_bedMissingSince < 0f)
					{
						_bedMissingSince = Time.time;
					}
					if (Time.time - _bedMissingSince < 60f)
					{
						_bedClaimScanTimer = Mathf.Min(_bedClaimScanTimer, 5f);
						return;
					}
					CompanionsPlugin.Log.LogInfo($"[Rest] Its bed at {vec:F0} is gone - looking for another");
				}
				_bedMissingSince = -1f;
				if (!(bed == null) && MHR_BedOwnerOk(bed, keeping: true) && !MHR_BedClaimedByOther(bed))
				{
					_claimedBed = bed;
					_claimedBeds.Add(bed.GetInstanceID());
					string value = MHR_MyName();
					_bedClaimNames[bed.GetInstanceID()] = value;
					MHR_SetBedClaim(bed, value, MHR_MyId());
					CompanionsPlugin.Log.LogInfo($"[Rest] Re-linked to its claimed bed as \"{value}\"");
					_needsBed = false;
					_bedFailReason = null;
					return;
				}
				zDO.Set(CompanionSetup.HasHomeBedHash, value: false);
			}
		}
		if (!(_setup != null))
		{
			return;
		}
		Vector3 anchor;
		float radius;
		if (_setup.HasHomePosition())
		{
			anchor = _setup.GetHomePosition();
			radius = _setup.GetHomeRadius();
		}
		else
		{
			anchor = base.transform.position;
			radius = 20f;
			if (!HasOwnerBedNearby(anchor, radius))
			{
				MHR_BedFail("it has no home set, so it only takes a bed within 20 m of a bed of yours, and there's none");
				return;
			}
		}
		Bed bed2 = FindUnclaimedBedNear(anchor, radius);
		if (!(bed2 == null))
		{
			ClaimBed(bed2);
			return;
		}
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		HashSet<int> hashSet = new HashSet<int>();
		Collider[] array = Physics.OverlapSphere(anchor, radius);
		foreach (Collider collider in array)
		{
			Bed bed3 = ((collider != null) ? collider.GetComponentInParent<Bed>() : null);
			if (!(bed3 == null) && hashSet.Add(bed3.GetInstanceID()))
			{
				num++;
				if (!MHR_BedOwnerOk(bed3))
				{
					num2++;
				}
				else if (MHR_BedClaimedByOther(bed3) || _claimedBeds.Contains(bed3.GetInstanceID()))
				{
					num3++;
				}
			}
		}
		MHR_BedFail((num == 0) ? $"no bed within {radius:F0} m of its {(_setup.HasHomePosition() ? "home" : "position")}" : $"{num} bed(s) within {radius:F0} m, {num2} slept in by a player, {num3} taken by another companion");
	}

	private void MHR_BedFail(string reason)
	{
		if (reason != _bedFailReason)
		{
			_bedFailReason = reason;
			CompanionsPlugin.Log.LogInfo("[Rest] No bed to claim: " + reason);
		}
	}

	private void OnDestroy()
	{
		_instances.Remove(this);
		if (_stamina != null)
		{
			_stamina.IsResting = false;
		}
		if (_isSleeping)
		{
			if (_bedTarget != null)
			{
				_claimedBeds.Remove(_bedTarget.GetInstanceID());
			}
			if (_nview != null && _nview.GetZDO() != null)
			{
				_nview.GetZDO().Set(ZDOVars.s_inBed, value: false);
			}
		}
	}

	private GameObject FindNearbyFire()
	{
		Vector3 position = base.transform.position;
		float num = FireRange * FireRange;
		int num2 = Physics.OverlapSphereNonAlloc(position, FireRange, _fireScanBuffer);
		Collider[] fireScanBuffer = _fireScanBuffer;
		int num3 = num2;
		if (num3 > 48)
		{
			num3 = 48;
		}
		_seenFireIds.Clear();
		GameObject result = null;
		float num4 = float.MaxValue;
		for (int i = 0; i < num3; i++)
		{
			Collider collider = fireScanBuffer[i];
			if (collider == null)
			{
				continue;
			}
			Fireplace componentInParent = collider.GetComponentInParent<Fireplace>();
			if (componentInParent != null && componentInParent.IsBurning())
			{
				if (_seenFireIds.Add(componentInParent.GetInstanceID()))
				{
					float sqrMagnitude = (componentInParent.transform.position - position).sqrMagnitude;
					if (sqrMagnitude < num && sqrMagnitude < num4)
					{
						result = componentInParent.gameObject;
						num4 = sqrMagnitude;
					}
				}
				continue;
			}
			EffectArea componentInParent2 = collider.GetComponentInParent<EffectArea>();
			if (componentInParent2 != null && (componentInParent2.m_type & EffectArea.Type.Heat) != EffectArea.Type.None && _seenFireIds.Add(componentInParent2.GetInstanceID()))
			{
				float sqrMagnitude2 = (componentInParent2.transform.position - position).sqrMagnitude;
				if (sqrMagnitude2 < num && sqrMagnitude2 < num4)
				{
					result = componentInParent2.gameObject;
					num4 = sqrMagnitude2;
				}
			}
		}
		return result;
	}

	private bool HasEnemyNearby()
	{
		if (_character == null)
		{
			return false;
		}
		foreach (Character allCharacter in Character.GetAllCharacters())
		{
			if (!(allCharacter == null) && !(allCharacter.GetHealth() <= 0f))
			{
				float num = Vector3.Distance(base.transform.position, allCharacter.transform.position);
				if (!(num > 15f) && BaseAI.IsEnemy(_character, allCharacter))
				{
					return true;
				}
			}
		}
		return false;
	}

	private void StartSitting(GameObject fire)
	{
		_isSitting = true;
		_fireTarget = fire;
		_healTimer = 0f;
		CompanionsPlugin.Log.LogDebug("[Rest] Sitting near fire \"" + fire.name + "\" — starting heal + stamina regen " + $"pos={base.transform.position:F2}");
		if (_ai != null)
		{
			_ai.SetFollowTarget(null);
			_ai.FreezeTimer = 0.5f;
		}
		Vector3 vector = fire.transform.position - base.transform.position;
		vector.y = 0f;
		if (vector.sqrMagnitude > 0.01f)
		{
			base.transform.rotation = Quaternion.LookRotation(vector.normalized);
		}
		CompanionsPlugin.Log.LogDebug($"[Rest] StartSitting anim — HasPlayerAnims={HasPlayerAnims} " + $"canWearArmor={_setup?.CanWearArmor()} zanim={_zanim != null}");
		if (_zanim != null && HasPlayerAnims)
		{
			_zanim.SetTrigger("emote_sit");
			_zanim.SetBool("emote_sit", value: true);
		}
		if (_stamina != null)
		{
			_stamina.IsResting = true;
		}
		CompanionRestedBuff component = GetComponent<CompanionRestedBuff>();
		if (component != null)
		{
			component.StartResting();
		}
	}

	private void StopAll()
	{
		CompanionsPlugin.Log.LogDebug($"[Rest] StopAll — sitting={_isSitting}, sleeping={_isSleeping}, " + $"directed={_isDirected}, autoSleep={_isAutoSleep}, navigating={_navigating}, pos={base.transform.position:F2}");
		_isAutoSleep = false;
		bool navigating = _navigating;
		_navigating = false;
		if (_isSitting)
		{
			StopSitting();
		}
		if (_isSleeping)
		{
			StopSleeping();
		}
		_isDirected = false;
		if (!navigating || _isSitting || _isSleeping)
		{
			return;
		}
		_fireTarget = null;
		_bedTarget = null;
		if (_ai != null)
		{
			int valueOrDefault = (_nview?.GetZDO()?.GetInt(CompanionSetup.ActionModeHash)).GetValueOrDefault();
			if (valueOrDefault == 4)
			{
				_ai.SetFollowTarget(null);
				_ai.SetPatrolPoint();
			}
			else
			{
				// 1.4.150: back to following only with Follow on - a worker whose job turned Follow off stays at the job
				_ai.SetFollowTarget(_setup != null && _setup.GetFollow() && MHR_Owner.Of(this) != null ? MHR_Owner.Go(this) : null);
			}
		}
	}

	private void ClaimBed(Bed bed)
	{
		ReleaseClaimedBed();
		_claimedBed = bed;
		_claimedBeds.Add(bed.GetInstanceID());
		string value = MHR_MyName();
		_bedClaimNames[bed.GetInstanceID()] = value;
		MHR_SetBedClaim(bed, value, MHR_MyId());
		if (_nview != null)
		{
			ZDO zDO = _nview.GetZDO();
			if (zDO != null)
			{
				zDO.Set(CompanionSetup.HomeBedPosHash, bed.transform.position);
				zDO.Set(CompanionSetup.HasHomeBedHash, value: true);
			}
		}
		CompanionsPlugin.Log.LogInfo($"[Rest] Claimed bed \"{bed.name}\" as standing home bed");
		_needsBed = false;
		_bedFailReason = null;
	}

	private void BeginSleep(Bed bed, bool isAuto)
	{
		if (_isSitting || _isSleeping || _navigating)
		{
			StopAll();
		}
		_isDirected = !isAuto;
		_isAutoSleep = isAuto;
		_bedTarget = bed;
		_healTimer = 0f;
		Vector3 position = bed.transform.position;
		if (bed.m_spawnPoint != null)
		{
			position = bed.m_spawnPoint.position;
		}
		float num = Vector3.Distance(base.transform.position, position);
		if (num < 3f)
		{
			FinalizeSleep();
			return;
		}
		_navigating = true;
		_navTarget = position;
		_navTimeout = 20f;
		if (_ai != null)
		{
			_ai.SetFollowTarget(null);
		}
		CompanionsPlugin.Log.LogDebug($"[Rest] Navigating to bed — dist={num:F1}m, auto={isAuto}");
	}

	internal bool MHR_BedInRange()
	{
		if (!(_claimedBed == null))
		{
			return Vector3.Distance(base.transform.position, _claimedBed.transform.position) <= ModConfig.BedRange.Value;
		}
		return false;
	}

	internal static bool MHR_ShouldPauseWork(Component c)
	{
		// 1.4.150: also while swimming for the shore, seated on a ship, or sitting by a fire
		if (c != null && WaterSafety.Active(c))
		{
			return true;
		}
		CompanionAI shipAi = c != null ? c.GetComponent<CompanionAI>() : null;
		if (shipAi != null && shipAi.IsOnShip)
		{
			return true;
		}
		CompanionRest sitting = c != null ? c.GetComponent<CompanionRest>() : null;
		if (sitting != null && sitting._isSitting)
		{
			return true;
		}
		if (!MHR_ToolKeeper.IsBusy(c))
		{
			if (!(c == null))
			{
				CompanionRest component = c.GetComponent<CompanionRest>();
				if (!(component == null))
				{
					if (component._isSleeping)
					{
						goto IL_0041;
					}
					if (component._navigating)
					{
						return component._bedTarget != null;
					}
				}
			}
			return false;
		}
		goto IL_0041;
		IL_0041:
		return true;
	}

	// 1.5.41: not when it has a bed back home that just isn't loaded out here (user: out wandering at night it kept
	// saying "all the beds are taken" - constantly)
	public bool NeedsBed()
	{
		if (!_needsBed)
		{
			return false;
		}
		ZDO zdo = _nview?.GetZDO();
		return zdo == null || !zdo.GetBool(CompanionSetup.HasHomeBedHash);
	}

	private void FinalizeSleep()
	{
		_isSleeping = true;
		if (_bedTarget != null)
		{
			_claimedBeds.Add(_bedTarget.GetInstanceID());
			if (_nview != null)
			{
				ZDO zDO = _nview.GetZDO();
				if (zDO != null)
				{
					zDO.Set(CompanionSetup.HomeBedPosHash, _bedTarget.transform.position);
					zDO.Set(CompanionSetup.HasHomeBedHash, value: true);
				}
			}
		}
		Transform transform = ((_bedTarget.m_spawnPoint != null) ? _bedTarget.m_spawnPoint : _bedTarget.transform);
		base.transform.position = transform.position;
		base.transform.rotation = transform.rotation;
		// 1.4.93: not kinematic (the game sets every character's velocity each physics tick, and on a kinematic body
		// that's the Unity warning "Setting linear velocity of a kinematic body is not supported" - user report). Like
		// seated companions: gravity off, the bed's own colliders ignored, held on the bed every frame (PinToBed).
		_bedPinPos = transform.position;
		_bedPinRot = transform.rotation;
		IgnoreBed(_bedTarget, ignore: true);
		_bedPinned = true;
		if (_body != null)
		{
			_body.position = transform.position;
			_body.linearVelocity = Vector3.zero;
			_body.useGravity = false;
		}
		CompanionsPlugin.Log.LogDebug($"[Rest] FinalizeSleep anim — HasPlayerAnims={HasPlayerAnims} " + $"canWearArmor={_setup?.CanWearArmor()} zanim={_zanim != null}");
		if (_zanim != null && HasPlayerAnims)
		{
			_zanim.SetBool("attach_bed", value: true);
		}
		if (_nview?.GetZDO() != null)
		{
			_nview.GetZDO().Set(ZDOVars.s_inBed, value: true);
		}
		if (_stamina != null)
		{
			_stamina.IsResting = true;
		}
		if (_setup != null)
		{
		}
		MHR_SetBedCollisionIgnored(_bedTarget, ignore: true);
		CompanionsPlugin.Log.LogDebug($"[Rest] FinalizeSleep — positioned at {transform.position:F2}");
	}

	private void CancelNavigation()
	{
		_navigating = false;
		_isDirected = false;
		_bedTarget = null;
		_fireTarget = null;
		if (!(_ai == null))
		{
			int valueOrDefault = (_nview?.GetZDO()?.GetInt(CompanionSetup.ActionModeHash)).GetValueOrDefault();
			if (valueOrDefault == 4)
			{
				_ai.SetFollowTarget(null);
				_ai.SetPatrolPoint();
			}
			else
			{
				// 1.4.150: back to following only with Follow on - a worker whose job turned Follow off stays at the job
				_ai.SetFollowTarget(_setup != null && _setup.GetFollow() && MHR_Owner.Of(this) != null ? MHR_Owner.Go(this) : null);
			}
		}
	}

	private void MHR_SetBedCollisionIgnored(Bed bed, bool ignore)
	{
		if (bed == null)
		{
			return;
		}
		CapsuleCollider component = GetComponent<CapsuleCollider>();
		if (component == null)
		{
			return;
		}
		Collider[] componentsInChildren = bed.GetComponentsInChildren<Collider>();
		if (componentsInChildren == null)
		{
			return;
		}
		foreach (Collider collider in componentsInChildren)
		{
			if (!(collider == null))
			{
				Physics.IgnoreCollision(component, collider, ignore);
			}
		}
		CompanionsPlugin.Log.LogDebug(string.Format("[Rest] Bed collisions {0} ({1} bed collider(s))", ignore ? "ignored while sleeping" : "restored", componentsInChildren.Length));
	}

	private void StopSitting()
	{
		_isSitting = false;
		_fireTarget = null;
		_isDirected = false;
		CompanionsPlugin.Log.LogDebug("[Rest] Stopped sitting — resuming normal behavior");
		if (_zanim != null && HasPlayerAnims)
		{
			_zanim.SetTrigger("emote_stop");
			_zanim.SetBool("emote_sit", value: false);
		}
		if (_stamina != null)
		{
			_stamina.IsResting = false;
		}
		CompanionRestedBuff component = GetComponent<CompanionRestedBuff>();
		if (component != null)
		{
			component.StopResting();
		}
		if (!(_ai == null))
		{
			int valueOrDefault = (_nview?.GetZDO()?.GetInt(CompanionSetup.ActionModeHash)).GetValueOrDefault();
			if (valueOrDefault == 4)
			{
				_ai.SetFollowTarget(null);
				_ai.SetPatrolPoint();
			}
			else
			{
				// 1.4.150: back to following only with Follow on - a worker whose job turned Follow off stays at the job
				_ai.SetFollowTarget(_setup != null && _setup.GetFollow() && MHR_Owner.Of(this) != null ? MHR_Owner.Go(this) : null);
			}
		}
	}

	private void StopSleeping()
	{
		Bed bedTarget = _bedTarget;
		bool inBed = bedTarget != null && bedTarget && _bedPinned;
		if (!inBed)
		{
			MHR_StandUpFromBed(bedTarget);
			MHR_SetBedCollisionIgnored(bedTarget, ignore: false);
		}
		_isSleeping = false;
		_isAutoSleep = false;
		_bedTarget = null;
		CompanionsPlugin.Log.LogDebug("[Rest] Stopped sleeping — resuming normal behavior");
		if (_zanim != null && HasPlayerAnims)
		{
			_zanim.SetBool("attach_bed", value: false);
		}
		if (_nview?.GetZDO() != null)
		{
			_nview.GetZDO().Set(ZDOVars.s_inBed, value: false);
		}
		if (inBed)
		{
			// still lying on the bed while the get-up animation plays; FinishGetUp steps him off it
			_wakeBed = bedTarget;
			_wakeUntil = Time.time + 1.6f;
		}
		else
		{
			_bedPinned = false;
			IgnoreBed(null, ignore: false);
			if (_body != null)
			{
				_body.isKinematic = false;
				_body.useGravity = true;
				_body.linearVelocity = Vector3.zero;
				if (bedTarget != null)
				{
					_body.position = base.transform.position;
				}
			}
		}
		if (_stamina != null)
		{
			_stamina.IsResting = false;
		}
		CompanionRestedBuff component = GetComponent<CompanionRestedBuff>();
		if (component != null)
		{
			component.ApplyRestedBuff();
		}
		if (!(_ai == null))
		{
			int valueOrDefault = (_nview?.GetZDO()?.GetInt(CompanionSetup.ActionModeHash)).GetValueOrDefault();
			if (valueOrDefault == 4)
			{
				_ai.SetFollowTarget(null);
				_ai.SetPatrolPoint();
			}
			else
			{
				// 1.4.150: back to following only with Follow on - a worker whose job turned Follow off stays at the job
				_ai.SetFollowTarget(_setup != null && _setup.GetFollow() && MHR_Owner.Of(this) != null ? MHR_Owner.Go(this) : null);
			}
		}
	}

	private void FinishGetUp()
	{
		Bed bed = _wakeBed;
		_wakeBed = null;
		_wakeUntil = 0f;
		if (bed != null && bed)
		{
			Vector3 floor;
			Transform spawnPoint = (bed.m_spawnPoint != null) ? bed.m_spawnPoint : bed.transform;
			base.transform.position = MHR_Follow.StandSpot(bed.transform.position, bed.transform.right, bed.transform.forward, out floor) ? floor : spawnPoint.TransformPoint(new Vector3(0f, 0.5f, 0f));
		}
		_bedPinned = false;
		if (bed != null && bed)
		{
			MHR_SetBedCollisionIgnored(bed, ignore: false);
		}
		IgnoreBed(null, ignore: false);
		if (_body != null)
		{
			_body.isKinematic = false;
			_body.useGravity = true;
			_body.linearVelocity = Vector3.zero;
			_body.position = base.transform.position;
		}
		CompanionsPlugin.Log.LogDebug("[Rest] Got up and stepped off the bed");
	}

	internal void MHR_StandUpFromBed(Bed bed)
	{
		if (_isSleeping && !(bed == null))
		{
			Transform spawnPoint = bed.m_spawnPoint;
			if (spawnPoint == null)
			{
				spawnPoint = bed.transform;
			}
			// 1.4.102: step off beside the bed onto the floor (1.4.101 test: got up on the bed and slid off the end)
			Vector3 floor;
			base.transform.position = MHR_Follow.StandSpot(bed.transform.position, bed.transform.right, bed.transform.forward, out floor) ? floor : spawnPoint.TransformPoint(new Vector3(0f, 0.5f, 0f));
			Rigidbody component = GetComponent<Rigidbody>();
			if (!(component == null))
			{
				component.linearVelocity = Vector3.zero;
				component.position = base.transform.position;
			}
			CompanionsPlugin.Log.LogDebug("[Rest] Got up at the bed's sleeping spot");
		}
	}

	private Bed FindNearbyBed()
	{
		Collider[] array = Physics.OverlapSphere(base.transform.position, 20f);
		Bed result = null;
		float num = float.MaxValue;
		HashSet<int> hashSet = new HashSet<int>();
		Collider[] array2 = array;
		foreach (Collider collider in array2)
		{
			if (collider == null)
			{
				continue;
			}
			Bed componentInParent = collider.GetComponentInParent<Bed>();
			if (componentInParent == null || !MHR_BedOwnerOk(componentInParent))
			{
				continue;
			}
			int instanceID = componentInParent.GetInstanceID();
			if (hashSet.Add(instanceID) && (!_claimedBeds.Contains(instanceID) || (!(_bedTarget == null) && _bedTarget.GetInstanceID() == instanceID)))
			{
				float num2 = Vector3.Distance(base.transform.position, componentInParent.transform.position);
				if (num2 < num)
				{
					num = num2;
					result = componentInParent;
				}
			}
		}
		return result;
	}

	private Bed FindAnyBedNear(Vector3 anchor, float radius)
	{
		Collider[] array = Physics.OverlapSphere(anchor, radius);
		Bed result = null;
		float num = float.MaxValue;
		foreach (Collider collider in array)
		{
			if (collider == null)
			{
				continue;
			}
			Bed componentInParent = collider.GetComponentInParent<Bed>();
			if (!(componentInParent == null))
			{
				float num2 = Vector3.Distance(anchor, componentInParent.transform.position);
				if (num2 < num)
				{
					num = num2;
					result = componentInParent;
				}
			}
		}
		return result;
	}

	private Bed FindUnclaimedBedNear(Vector3 anchor, float radius)
	{
		Collider[] array = Physics.OverlapSphere(anchor, radius);
		Bed result = null;
		float num = float.MaxValue;
		int i = 0;
		HashSet<int> hashSet = new HashSet<int>();
		for (; i < array.Length; i++)
		{
			Collider collider = array[i];
			if (collider == null)
			{
				continue;
			}
			Bed componentInParent = collider.GetComponentInParent<Bed>();
			if (componentInParent == null || !MHR_BedOwnerOk(componentInParent))
			{
				continue;
			}
			int instanceID = componentInParent.GetInstanceID();
			if (hashSet.Add(instanceID) && !_claimedBeds.Contains(instanceID) && !MHR_BedClaimedByOther(componentInParent))
			{
				if (MHR_BedIsMine(componentInParent))
				{
					return componentInParent;
				}
				float num2 = Vector3.Distance(anchor, componentInParent.transform.position);
				if (num2 < num)
				{
					num = num2;
					result = componentInParent;
				}
			}
		}
		return result;
	}

	// 1.5.1: a bed nobody has slept in, or one its owner has slept in that isn't the owner's spawn point (clicking a bed
	// claims it for you, so it was easy to take a companion's bed by accident). Another player's bed is never used, and
	// neither is the owner's bed when the owner isn't on this computer (only the owner's game knows their spawn point).
	// keeping = it already has this bed: when another player's game is running the companion it can't check the owner's
	// spawn point, so it keeps the bed rather than giving it up
	internal bool MHR_BedOwnerOk(Bed bed, bool keeping = false)
	{
		long bedOwnerSafe = GetBedOwnerSafe(bed);
		if (bedOwnerSafe == 0)
		{
			return true;
		}
		if (_nview?.GetZDO()?.GetString(CompanionSetup.OwnerHash) != bedOwnerSafe.ToString())
		{
			return false;
		}
		Player localPlayer = Player.m_localPlayer;
		if (localPlayer == null || localPlayer.GetPlayerID() != bedOwnerSafe)
		{
			return keeping;
		}
		PlayerProfile playerProfile = Game.instance?.GetPlayerProfile();
		if (playerProfile == null)
		{
			return false;
		}
		if (playerProfile.HaveCustomSpawnPoint())
		{
			return Vector3.Distance(bed.GetSpawnPoint(), playerProfile.GetCustomSpawnPoint()) >= 1f;
		}
		return true;
	}

	internal bool MHR_BedClaimedByOther(Bed bed)
	{
		string text = MHR_GetBedClaimId(bed);
		if (string.IsNullOrEmpty(text) && MHR_GetBedClaimName(bed) == null)
		{
			return false;
		}
		if (MHR_BedIsMine(bed))
		{
			return false;
		}
		// 1.5.1: a claim left by a companion that no longer exists anywhere (despawned by something else, deleted) doesn't
		// block the bed forever
		if (!string.IsNullOrEmpty(text) && MHR_Roster.CompanionGone(text))
		{
			return false;
		}
		return true;
	}

	internal bool MHR_BedIsMine(Bed bed)
	{
		string text = MHR_GetBedClaimId(bed);
		if (!string.IsNullOrEmpty(text))
		{
			string text2 = MHR_MyId();
			if (!string.IsNullOrEmpty(text2))
			{
				return text == text2;
			}
		}
		else
		{
			text = MHR_GetBedClaimName(bed);
			if (text != null)
			{
				return text == MHR_MyName();
			}
		}
		return false;
	}

	private bool HasOwnerBedNearby(Vector3 anchor, float radius)
	{
		Collider[] array = Physics.OverlapSphere(anchor, radius);
		foreach (Collider collider in array)
		{
			if (!(collider == null))
			{
				Bed componentInParent = collider.GetComponentInParent<Bed>();
				if (!(componentInParent == null) && GetBedOwnerSafe(componentInParent) != 0)
				{
					return true;
				}
			}
		}
		return false;
	}

	internal void MHR_ReleaseBedForDespawn()
	{
		ReleaseClaimedBed();
		if (!(_nview == null))
		{
			_nview.GetZDO()?.Set(CompanionSetup.HasHomeBedHash, value: false);
		}
		CompanionsPlugin.Log.LogInfo("[Rest] Despawn: released the companion's bed");
	}

	private void ReleaseClaimedBed()
	{
		if (!(_claimedBed == null))
		{
			if (MHR_BedIsMine(_claimedBed))
			{
				MHR_SetBedClaim(_claimedBed, "", "");
			}
			_claimedBeds.Remove(_claimedBed.GetInstanceID());
			_bedClaimNames.Remove(_claimedBed.GetInstanceID());
			_claimedBed = null;
		}
	}

	internal static void MHR_SetBedClaim(Bed bed, string name, string id)
	{
		if (name == null)
		{
			name = "";
		}
		if (id == null)
		{
			id = "";
		}
		if (bed == null)
		{
			return;
		}
		ZNetView component = bed.GetComponent<ZNetView>();
		if (component == null)
		{
			return;
		}
		ZDO zDO = component.GetZDO();
		if (zDO != null && (zDO.GetString(MHR_BedClaimNameHash) != name || zDO.GetString(MHR_BedClaimIdHash) != id))
		{
			if (!component.IsOwner())
			{
				component.ClaimOwnership();
			}
			zDO.Set(MHR_BedClaimNameHash, name);
			zDO.Set(MHR_BedClaimIdHash, id);
			CompanionsPlugin.Log.LogInfo($"[Rest] Bed claim saved on the bed: \"{name}\" (ID {id})");
		}
	}

	internal string MHR_MyName()
	{
		CompanionInteract component = GetComponent<CompanionInteract>();
		if ((object)component == null)
		{
			return ModLocalization.Loc("hc_msg_name_default");
		}
		return component.GetCompanionName();
	}

	internal static string MHR_GetBedClaimId(Bed bed)
	{
		if (!(bed == null))
		{
			ZNetView component = bed.GetComponent<ZNetView>();
			if (!(component == null))
			{
				ZDO zDO = component.GetZDO();
				if (zDO != null)
				{
					return zDO.GetString(MHR_BedClaimIdHash);
				}
			}
		}
		return "";
	}

	internal string MHR_MyId()
	{
		CompanionSetup setup = _setup;
		if ((object)setup == null)
		{
			return "";
		}
		return setup.MHR_EnsureCompanionId();
	}
}
