using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Companions;

public class CompanionSetup : MonoBehaviour
{
	private static readonly List<CompanionSetup> _allCompanions = new List<CompanionSetup>();

	internal static readonly int AppearanceHash = "HC_Appearance".GetStableHashCode();

	internal static readonly int OwnerHash = "HC_Owner".GetStableHashCode();

	internal static readonly int DeathItemRuleHash = "HC_DeathItemRule".GetStableHashCode();

	private float _mhrRuleStampTimer;

	internal bool _mhrDespawning;

	internal bool _mhrDying;

	// 1.5.1: an old copy of a recovered companion, being removed (MHR_Roster)
	internal bool _mhrStale;

	// 1.5.1: when this companion was loaded - its items load from the world a moment later, so the roster waits a bit
	internal float _mhrAwakeTime;

	internal static readonly int NameHash = "HC_Name".GetStableHashCode();

	// Fase 3.10: the AI profile of this companion - the voice it speaks with and the personality it answers in, kept
	// in its own ZDO so several companions of the same player no longer talk and behave alike (see
	// AI/CompanionProfile.cs, which reads and writes them). Every key means "the player chose this for THIS
	// companion": a zero headcount/absent value is "follow the [IA] defaults", which is what an old save and every
	// companion spawned without a choice have. Written by the owner only, like every other key here.
	internal static readonly int AiVoiceHash = "HC_AiVoice".GetStableHashCode();

	internal static readonly int AiPersonaSetHash = "HC_AiPersonaSet".GetStableHashCode();

	internal static readonly int AiPresetHash = "HC_AiPreset".GetStableHashCode();

	internal static readonly int AiPersonaTextHash = "HC_AiPersona".GetStableHashCode();

	internal static readonly int ActionModeHash = "HC_ActionMode".GetStableHashCode();

	internal static readonly int ActionModeSchemaHash = "HC_ActionModeSchema".GetStableHashCode();

	// Fase 3.2: the target a chat order put on a gathering job ("colete 5 madeiras" -> 5). 0 = no target (the
	// config rules, as before). In the ZDO so every companion has its own and it survives a reload.
	internal static readonly int OrderCapHash = "HC_OrderCap".GetStableHashCode();

	// Fase 3.2: which work mode that target belongs to - a target for Wood must not follow the companion into Stone.
	internal static readonly int OrderCapModeHash = "HC_OrderCapMode".GetStableHashCode();

	// Fase 3.2c: the item that target is about ("colete 10 madeiras" -> "Wood"). Empty when the order named no item
	// the game knows: the target is then the whole job, as in Fase 3.2. In the ZDO, with the target.
	internal static readonly int OrderCapItemHash = "HC_OrderCapItem".GetStableHashCode();

	// Fase 3.2c: the most of the ordered item this companion has had in its OWN pack during the current order, one
	// int per companion, session-only. The target is counted in the pack (not in the chests), so unloading a full
	// pack into a chest would start the count over - the high-water mark keeps the order ending when it has
	// collected the number asked for. Written and dropped with the target: SetOrderCap/ClearOrderCap/OrderSeen.
	private static readonly Dictionary<int, int> s_orderSeen = new Dictionary<int, int>();

	internal static readonly int StaminaHash = "HC_Stamina".GetStableHashCode();

	internal static readonly int AutoPickupHash = "HC_AutoPickup".GetStableHashCode();

	internal static readonly int WanderHash = "HC_Wander".GetStableHashCode();

	// 1.4.77: Wander also controls how a companion moves around while following. Switched on once for every companion.
	internal static readonly int WanderFollowMigratedHash = "HC_WanderFollowMigrated".GetStableHashCode();

	internal static readonly int FormationSlotHash = "HC_FormationSlot".GetStableHashCode();

	internal static readonly int IsCommandableHash = "HC_IsCommandable".GetStableHashCode();

	internal static readonly int StayHomeHash = "HC_StayHome".GetStableHashCode();

	internal static readonly int HomePosHash = "HC_HomePos".GetStableHashCode();

	internal static readonly int HomePosSetHash = "HC_HomePosSet".GetStableHashCode();

	internal static readonly int HomeRadiusHash = "HC_HomeRadius".GetStableHashCode();

	private static readonly int StarterGearHash = "HC_StarterGear".GetStableHashCode();

	internal static readonly int CombatStanceHash = "HC_CombatStance".GetStableHashCode();

	internal static readonly int FollowHash = "HC_Follow".GetStableHashCode();

	internal static readonly int TombstoneIdHash = "HC_TombstoneId".GetStableHashCode();

	internal static readonly int MHR_TombOwnerHash = "HC_TombOwner".GetStableHashCode();

	internal static readonly int MHR_TombItemsHash = "HC_TombItems".GetStableHashCode();

	internal static readonly int MHR_CompanionIdHash = "HC_CompanionId".GetStableHashCode();

	internal static readonly int MHR_TombCompanionIdHash = "HC_TombCompanionId".GetStableHashCode();

	private bool _mhrHasId;

	// Fase 3.5: the permanent ID, kept once it is known so the conversation memory can be filed under it (and
	// forgotten from OnDestroy, where the ZNetView may already be gone).
	private string _mhrId;

	internal static readonly int TombInvWidthHash = "HC_TombInvW".GetStableHashCode();

	internal static readonly int TombInvHeightHash = "HC_TombInvH".GetStableHashCode();

	internal static readonly int HomeBedPosHash = "HC_HomeBedPos".GetStableHashCode();

	internal static readonly int HasHomeBedHash = "HC_HasHomeBed".GetStableHashCode();

	private static readonly HashSet<VisEquipment> _companionVisEquips = new HashSet<VisEquipment>();

	internal const int ModeFollow = 0;

	internal const int ModeGatherWood = 1;

	internal const int ModeGatherStone = 2;

	internal const int ModeGatherOre = 3;

	internal const int ModeStay = 4;

	internal const int ModeForage = 5;

	internal const int ModeSmelt = 6;

	internal const int ModeHunt = 7;

	internal const int ModeFarm = 8;

	internal const int ModeFish = 9;

	internal const int ModeRepairBuildings = 15;

	internal const int ModeRestock = 16;

	internal const int ModeCook = 17;

	internal const int StanceBalanced = 0;

	internal const int StancePassive = 1;

	internal const int StanceRanged = 2;

	internal const int StanceMelee = 3;

	private const int ActionModeSchemaVersion = 3;

	private static readonly MethodInfo _updateVisuals = AccessTools.Method(typeof(VisEquipment), "UpdateVisuals");

	internal static readonly FieldInfo _rightItemField = AccessTools.Field(typeof(Humanoid), "m_rightItem");

	internal static readonly FieldInfo _leftItemField = AccessTools.Field(typeof(Humanoid), "m_leftItem");

	private static readonly FieldInfo _chestItemField = AccessTools.Field(typeof(Humanoid), "m_chestItem");

	private static readonly FieldInfo _legItemField = AccessTools.Field(typeof(Humanoid), "m_legItem");

	private static readonly FieldInfo _helmetItemField = AccessTools.Field(typeof(Humanoid), "m_helmetItem");

	private static readonly FieldInfo _shoulderItemField = AccessTools.Field(typeof(Humanoid), "m_shoulderItem");

	private static readonly FieldInfo _utilityItemField = AccessTools.Field(typeof(Humanoid), "m_utilityItem");

	private static readonly FieldInfo _trinketItemField = AccessTools.Field(typeof(Humanoid), "m_trinketItem");

	private ZNetView _nview;

	private VisEquipment _visEquip;

	private CompanionAI _ai;

	private Humanoid _humanoid;

	private ZSyncAnimation _zanim;

	private HarvestController _harvestCached;

	private CompanionRest _restCached;

	private SmeltController _smeltCached;

	private HuntController _huntCached;

	private CompanionCombatAI _combatAI;

	private bool _initialized;

	private bool _ownerMismatchLogged;

	private bool _uiFrozen;

	private bool _dead;

	private Minimap.PinData _minimapPin;

	private ZDOID _zdoid = ZDOID.None;

	private bool _removePinOnDestroy;

	private float _pinHiddenUntil;

	private static Minimap s_pinMinimapInstance;

	private static readonly Dictionary<ZDOID, Minimap.PinData> s_persistentPins = new Dictionary<ZDOID, Minimap.PinData>();

	private bool _equipping;

	private float _autoEquipCooldown;

	private bool _autoEquipPending;

	private const float AutoEquipMinInterval = 0.5f;

	private readonly List<ItemDrop.ItemData> _equipQueue = new List<ItemDrop.ItemData>();

	private float _equipTimer;

	private bool _equipAnimActive;

	private Action _inventoryCallback;

	private bool _mhrNoDestroyDone;

	internal static IReadOnlyList<CompanionSetup> AllCompanions => _allCompanions;


	internal bool SuppressAutoEquip { get; set; }

	internal bool IsEquipping
	{
		get
		{
			if (_equipQueue.Count <= 0)
			{
				return _equipAnimActive;
			}
			return true;
		}
	}

	internal void RestoreFollowTarget()
	{
		if (_ai == null || Player.m_localPlayer == null)
		{
			return;
		}
		ZDO zDO = _nview?.GetZDO();
		if (zDO == null)
		{
			return;
		}
		string text = zDO.GetString(OwnerHash);
		long playerID = Player.m_localPlayer.GetPlayerID();
		string text2 = playerID.ToString();
		if (playerID != 0L)
		{
			if (string.IsNullOrEmpty(text))
			{
				zDO.Set(OwnerHash, text2);
				CompanionsPlugin.Log.LogDebug("[Setup] RestoreFollow — claimed orphan, set owner to " + text2);
				text = text2;
			}
			if (text != text2)
			{
				CompanionsPlugin.Log.LogDebug("[Setup] RestoreFollow — owner mismatch: zdo=\"" + text + "\" local=\"" + text2 + "\"");
			}
			else
			{
				int mode = zDO.GetInt(ActionModeHash);
				ApplyFollowMode(mode);
				AssignFormationSlot();
			}
		}
	}

	internal void ApplyAppearance(CompanionAppearance a)
	{
		if (CanWearArmor() && _visEquip != null)
		{
			_visEquip.SetModel(a.ModelIndex);
			_visEquip.SetHairItem((string.IsNullOrEmpty(a.HairItem) ? "Hair1" : a.HairItem).GetStableHashCode());
			_visEquip.SetBeardItem(((a.ModelIndex == 0) ? (a.BeardItem ?? "") : "").GetStableHashCode());
			_visEquip.SetSkinColor(a.SkinColor);
			_visEquip.SetHairColor(a.HairColor);
		}
		if (_visEquip != null)
		{
			_updateVisuals?.Invoke(_visEquip, null);
		}
		int num = LayerMask.NameToLayer("character");
		if (num < 0)
		{
			num = 9;
		}
		Transform[] componentsInChildren = GetComponentsInChildren<Transform>(includeInactive: true);
		foreach (Transform transform in componentsInChildren)
		{
			transform.gameObject.layer = num;
		}
	}

	internal float GetTotalArmor()
	{
		float num = 0f;
		ItemDrop.ItemData equipSlot = GetEquipSlot(_chestItemField);
		ItemDrop.ItemData equipSlot2 = GetEquipSlot(_legItemField);
		ItemDrop.ItemData equipSlot3 = GetEquipSlot(_helmetItemField);
		ItemDrop.ItemData equipSlot4 = GetEquipSlot(_shoulderItemField);
		ItemDrop.ItemData equipSlot5 = GetEquipSlot(_trinketItemField);
		if (equipSlot != null)
		{
			num += equipSlot.GetArmor();
		}
		if (equipSlot2 != null)
		{
			num += equipSlot2.GetArmor();
		}
		if (equipSlot3 != null)
		{
			num += equipSlot3.GetArmor();
		}
		if (equipSlot4 != null)
		{
			num += equipSlot4.GetArmor();
		}
		if (equipSlot5 != null)
		{
			num += equipSlot5.GetArmor();
		}
		return num;
	}

	internal static bool IsCompanionVisEquip(VisEquipment ve)
	{
		return _companionVisEquips.Contains(ve);
	}

	internal bool CanWearArmor()
	{
		if (_nview == null || _nview.GetZDO() == null)
		{
			return true;
		}
		return !CompanionTierData.IsDvergerVariant(_nview.GetZDO().GetPrefab());
	}

	internal bool HasHomePosition()
	{
		ZDO zDO = _nview?.GetZDO();
		if (zDO == null)
		{
			return false;
		}
		if (zDO.GetBool(HomePosSetHash))
		{
			return true;
		}
		Vector3 vec = zDO.GetVec3(HomePosHash, Vector3.zero);
		if (vec != Vector3.zero)
		{
			zDO.Set(HomePosSetHash, value: true);
			return true;
		}
		return false;
	}

	internal bool GetFollow()
	{
		return _nview?.GetZDO()?.GetBool(FollowHash, defaultValue: true) ?? true;
	}

	internal Vector3 GetHomePosition()
	{
		return _nview?.GetZDO()?.GetVec3(HomePosHash, Vector3.zero) ?? Vector3.zero;
	}

	internal bool GetWander()
	{
		return _nview?.GetZDO()?.GetBool(WanderHash) == true;
	}

	internal float GetHomeRadius()
	{
		return _nview?.GetZDO()?.GetFloat(HomeRadiusHash, ModConfig.HomeZoneRadius.Value) ?? ModConfig.HomeZoneRadius.Value;
	}

	internal int GetCombatStance()
	{
		return (_nview?.GetZDO()?.GetInt(CombatStanceHash)).GetValueOrDefault();
	}

	/// <summary>
	/// How many places the ring around the player has (Fase 3.9: it used to be 8, so the 9th companion and everyone
	/// after it shared slot 0 - one on top of the other). <see cref="CompanionAI"/> lays the slots out by angle: 0-3
	/// beside the player, 4-7 just behind, 8-31 on a wider ring (see its <c>GetFormationOffset</c>).
	/// </summary>
	private const int FormationSlotCount = 32;

	internal int AssignFormationSlot()
	{
		ZDO zDO = _nview?.GetZDO();
		if (zDO == null)
		{
			return 0;
		}
		int num = zDO.GetInt(FormationSlotHash, -1);
		if (num >= 0)
		{
			return num;
		}
		HashSet<int> hashSet = new HashSet<int>();
		Player localPlayer = Player.m_localPlayer;
		if (localPlayer == null)
		{
			return 0;
		}
		string text = localPlayer.GetPlayerID().ToString();
		foreach (CompanionSetup allCompanion in _allCompanions)
		{
			if (allCompanion == this)
			{
				continue;
			}
			ZDO zDO2 = allCompanion._nview?.GetZDO();
			if (zDO2 != null && !(zDO2.GetString(OwnerHash) != text))
			{
				int num2 = zDO2.GetInt(FormationSlotHash, -1);
				if (num2 >= 0)
				{
					hashSet.Add(num2);
				}
			}
		}
		for (int i = 0; i < FormationSlotCount; i++)
		{
			if (!hashSet.Contains(i))
			{
				zDO.Set(FormationSlotHash, i);
				return i;
			}
		}
		// every place of the ring is taken (33+ companions): sharing the first one is the only option left, and it is
		// said out loud so it is not mistaken for the bug of the old 8-slot loop
		CompanionsPlugin.Log.LogWarning("[Setup] Formation: all " + FormationSlotCount + " places are taken - \""
			+ zDO.GetString(NameHash, "Companion") + "\" shares slot 0");
		zDO.Set(FormationSlotHash, 0);
		return 0;
	}

	internal void SyncEquipmentToInventory()
	{
		if (!(_humanoid == null) && !(_nview == null) && _nview.IsOwner())
		{
			CompanionsPlugin.Log.LogDebug($"[Setup] SyncEquipmentToInventory — SuppressAutoEquip={SuppressAutoEquip}");
			UnequipMissingEquippedItems();
			if (!SuppressAutoEquip)
			{
				AutoEquipBest();
			}
		}
	}

	internal void SetFollow(bool v)
	{
		_nview?.GetZDO()?.Set(FollowHash, v);
	}

	internal bool GetIsCommandable()
	{
		ZDO zDO = _nview?.GetZDO();
		if (zDO == null)
		{
			return true;
		}
		return zDO.GetInt(IsCommandableHash, 1) != 0;
	}

	internal string MHR_EnsureCompanionId()
	{
		if (!(_nview == null))
		{
			ZDO zDO = _nview.GetZDO();
			if (zDO != null)
			{
				string text = zDO.GetString(MHR_CompanionIdHash);
				if (string.IsNullOrEmpty(text))
				{
					if (!_nview.IsOwner())
					{
						goto IL_0095;
					}
					text = Guid.NewGuid().ToString("N");
					zDO.Set(MHR_CompanionIdHash, text);
					CompanionsPlugin.Log.LogInfo(string.Format("[Setup] Companion \"{0}\" was given its permanent ID {1}", zDO.GetString(NameHash, "Companion"), text));
				}
				return text;
			}
		}
		goto IL_0095;
		IL_0095:
		return "";
	}

	/// <summary>Fase 3.5: the companion's permanent ID, read at most once - the conversation memory (CompanionMemory)
	/// is filed under it, so it survives a rename. Returns "" when the companion has no ID yet (the caller falls back
	/// to its name).</summary>
	internal string MHR_ResolveCompanionId()
	{
		if (string.IsNullOrEmpty(_mhrId))
		{
			_mhrId = MHR_EnsureCompanionId();
		}
		return _mhrId ?? "";
	}

	/// <summary>Fase 3.5: the ID already known, without touching the ZNetView - safe to call from OnDestroy.</summary>
	internal string MHR_CachedCompanionId => _mhrId ?? "";

	internal ItemDrop.ItemData GetHelmetItem()
	{
		return GetEquipSlot(_helmetItemField);
	}

	internal ItemDrop.ItemData GetEquipSlot(FieldInfo field)
	{
		return field?.GetValue(_humanoid) as ItemDrop.ItemData;
	}

	internal ItemDrop.ItemData GetChestItem()
	{
		return GetEquipSlot(_chestItemField);
	}

	internal ItemDrop.ItemData GetLegsItem()
	{
		return GetEquipSlot(_legItemField);
	}

	internal ItemDrop.ItemData GetShoulderItem()
	{
		return GetEquipSlot(_shoulderItemField);
	}

	internal ItemDrop.ItemData GetUtilityItem()
	{
		return GetEquipSlot(_utilityItemField);
	}

	internal ItemDrop.ItemData GetTrinketItem()
	{
		return GetEquipSlot(_trinketItemField);
	}

	private void Awake()
	{
		MHR_MarkNoDestroy();
		_mhrAwakeTime = Time.time;
		_allCompanions.Add(this);
		_nview = GetComponent<ZNetView>();
		_visEquip = GetComponent<VisEquipment>();
		_ai = GetComponent<CompanionAI>();
		_humanoid = GetComponent<Humanoid>();
		_zanim = GetComponent<ZSyncAnimation>();
		_harvestCached = GetComponent<HarvestController>();
		_restCached = GetComponent<CompanionRest>();
		_smeltCached = GetComponent<SmeltController>();
		_huntCached = GetComponent<HuntController>();
		_combatAI = GetComponent<CompanionCombatAI>();
		CompanionsPlugin.Log.LogInfo($"[Setup] Awake — nview={_nview != null} visEquip={_visEquip != null} " + $"ai={_ai != null} humanoid={_humanoid != null} name=\"{base.gameObject.name}\"");
		if (_visEquip != null)
		{
			_companionVisEquips.Add(_visEquip);
		}
		if (_humanoid != null)
		{
			_humanoid.m_onDeath = (Action)Delegate.Combine(_humanoid.m_onDeath, new Action(OnCompanionDeath));
		}
		TryInit();
	}

	internal void MHR_MarkNoDestroy()
	{
		if (_mhrNoDestroyDone || _nview == null)
		{
			return;
		}
		ZDO zDO = _nview.GetZDO();
		if (zDO == null)
		{
			return;
		}
		if (!zDO.GetBool("SKK_NoDestroy"))
		{
			if (!_nview.IsOwner())
			{
				return;
			}
			zDO.Set("SKK_NoDestroy", value: true);
		}
		_mhrNoDestroyDone = true;
	}

	private void OnCompanionDeath()
	{
		_mhrDying = true;
		if (_dead)
		{
			return;
		}
		_dead = true;
		// Fase 3.5: a companion that goes down forgets the conversation (it comes back as the same companion, but a
		// fresh start is what the player expects from one that just died).
		CompanionMemory.Clear(MHR_CachedCompanionId);
		if (_nview == null || !_nview.IsOwner())
		{
			return;
		}
		ZDO zDO = _nview.GetZDO();
		if (zDO != null)
		{
			MHR_Roster.OnDied(this);
			if (CompanionInteractPanel.IsOpenFor(this))
			{
				CompanionInteractPanel.Instance?.Hide();
				InventoryGui.instance?.Hide();
			}
			string text = ((_humanoid != null) ? _humanoid.m_name : ModLocalization.Loc("hc_msg_name_default"));
			string text2 = zDO.GetString(NameHash);
			if (!string.IsNullOrEmpty(text2))
			{
				text = text2;
			}
			string appearance = zDO.GetString(AppearanceHash);
			string ownerId = zDO.GetString(OwnerHash);
			int actionMode = zDO.GetInt(ActionModeHash);
			int actionModeSchema = zDO.GetInt(ActionModeSchemaHash);
			bool follow = zDO.GetBool(FollowHash, defaultValue: true);
			bool stayHome = zDO.GetBool(StayHomeHash);
			bool autoPickup = zDO.GetBool(AutoPickupHash, defaultValue: true);
			bool wander = zDO.GetBool(WanderHash);
			int num = zDO.GetInt(IsCommandableHash, 1);
			int formationSlot = zDO.GetInt(FormationSlotHash, -1);
			Vector3 vector = ((_humanoid != null) ? _humanoid.GetCenterPoint() : base.transform.position);
			int prefab = zDO.GetPrefab();
			string prefabName = CompanionTierData.Companion.PrefabName;
			GameObject gameObject = ZNetScene.instance?.GetPrefab(prefab);
			if (gameObject != null)
			{
				prefabName = gameObject.name;
			}
			CompanionsPlugin.Log.LogInfo($"[Setup] Companion \"{text}\" died at {vector:F1} — prefab={prefabName}");
			string skillsSerialized = "";
			CompanionSkills component = GetComponent<CompanionSkills>();
			if (component != null)
			{
				component.OnDeath();
				skillsSerialized = component.SerializeSkills();
			}
			long num2 = zDO.GetLong(TombstoneIdHash, 0L);
			bool flag = _humanoid != null && _humanoid.GetInventory() != null && _humanoid.GetInventory().NrOfItems() > 0;
			long tombstoneId = (flag ? DateTime.UtcNow.Ticks : num2);
			if (!flag && num2 != 0L)
			{
				CompanionsPlugin.Log.LogInfo($"[Setup] No items — preserving existing tombstoneId={num2} " + "so respawned companion can find previous tombstone");
			}
			bool flag2 = HasHomePosition();
			Vector3 homePos = (flag2 ? GetHomePosition() : Vector3.zero);
			int combatStance = zDO.GetInt(CombatStanceHash);
			bool hasHomeBed = zDO.GetBool(HasHomeBedHash);
			Vector3 vec = zDO.GetVec3(HomeBedPosHash, Vector3.zero);
			CreateCompanionTombstone(text, vector, tombstoneId, prefabName, appearance, ownerId, actionMode, actionModeSchema, follow, stayHome, autoPickup, wander, num != 0, formationSlot, skillsSerialized, flag2, homePos, combatStance, hasHomeBed, vec);
		}
	}

	private void TryInit()
	{
		if (!_initialized && !(_nview == null) && _nview.GetZDO() != null)
		{
			ZDO zDO = _nview.GetZDO();
			string text = zDO.GetString(AppearanceHash);
			CompanionAppearance a = (string.IsNullOrEmpty(text) ? CompanionAppearance.Default() : CompanionAppearance.Deserialize(text));
			EnsureActionModeMigration();
			ApplyAppearance(a);
			RestoreFollowTarget();
			RestoreActionMode();
			RestoreName();
			RegisterInventoryCallback();
			AddStarterGear(zDO);
			int num = zDO.GetInt(ActionModeHash);
			CompanionsPlugin.Log.LogInfo(string.Format("[Setup] Initialized — mode={0} appearance={1} ", num, string.IsNullOrEmpty(text) ? "default" : "saved") + $"pos={base.transform.position:F1}");
			_instantEquipUntil = Time.time + 10f;
			_initialized = true;
		}
	}

	private void Start()
	{
		if (!_initialized)
		{
			TryInit();
		}
	}

	private void Update()
	{
		if (!_initialized)
		{
			TryInit();
			return;
		}
		MHR_StampOwnerDeathRule();
		MHR_MarkNoDestroy();
		if (!_mhrHasId)
		{
			string companionId = MHR_EnsureCompanionId();
			if (!string.IsNullOrEmpty(companionId))
			{
				_mhrId = companionId;
				_mhrHasId = true;
			}
		}
		if (CompanionInteractPanel.IsOpenFor(this) || CompanionRadialMenu.IsOpenFor(this))
		{
			_ai?.SetPatrolPoint();
			_ai?.SetFollowTarget(null);
			_uiFrozen = true;
			return;
		}
		if (_uiFrozen)
		{
			_uiFrozen = false;
			if (_ai != null && _ai.IsRecoveringTombstone)
			{
				CompanionsPlugin.Log.LogDebug("[Setup] UI closed during tombstone recovery — skipping follow restore");
			}
			else
			{
				ZDO zDO = _nview?.GetZDO();
				if (zDO != null && _ai != null)
				{
					int mode = zDO.GetInt(ActionModeHash);
					ApplyFollowMode(mode, force: true);
				}
			}
		}
		if (_autoEquipCooldown > 0f)
		{
			_autoEquipCooldown -= Time.deltaTime;
			if (_autoEquipCooldown <= 0f && _autoEquipPending)
			{
				_autoEquipPending = false;
				if (_humanoid != null && _nview != null && _nview.IsOwner() && !SuppressAutoEquip)
				{
					CompanionsPlugin.Log.LogDebug("[Setup] Deferred auto-equip firing now");
					AutoEquipBest();
				}
				else if (SuppressAutoEquip)
				{
					CompanionsPlugin.Log.LogDebug("[Setup] Deferred auto-equip skipped — SuppressAutoEquip=true");
				}
			}
		}
		UpdateEquipQueue(Time.deltaTime);
		UpdateMinimapPin();
		if (_nview != null && _nview.IsOwner() && _humanoid != null)
		{
			Inventory inventory = _humanoid.GetInventory();
			SEMan sEMan = _humanoid.GetSEMan();
			if (inventory != null && sEMan != null)
			{
				if (inventory.GetTotalWeight() > CompanionTierData.MaxCarryWeight)
				{
					sEMan.AddStatusEffect(SEMan.s_statusEffectEncumbered, resetTime: false, 0, 0f, -1);
				}
				else
				{
					sEMan.RemoveStatusEffect(SEMan.s_statusEffectEncumbered);
				}
			}
		}
		if (!(_ai != null) || !(_ai.GetFollowTarget() == null) || !(Player.m_localPlayer != null))
		{
			return;
		}
		ZDO zDO2 = _nview?.GetZDO();
		if (zDO2 == null)
		{
			return;
		}
		string text = zDO2.GetString(OwnerHash);
		long playerID = Player.m_localPlayer.GetPlayerID();
		string text2 = playerID.ToString();
		if (playerID == 0L)
		{
			return;
		}
		if (string.IsNullOrEmpty(text))
		{
			zDO2.Set(OwnerHash, text2);
			CompanionsPlugin.Log.LogDebug("[Setup] Claimed orphan companion — set owner to " + text2);
			text = text2;
		}
		// 1.4.152: another player's companion run by this game follows its own player (when they're loaded here)
		if (text != text2 && MHR_Owner.Of(this) == null)
		{
			if (!_ownerMismatchLogged)
			{
				_ownerMismatchLogged = true;
				CompanionsPlugin.Log.LogDebug("[Setup] Another player's companion, its player isn't here — follow target not restored");
			}
		}
		else
		{
			int num = zDO2.GetInt(ActionModeHash);
			if ((!(_harvestCached != null) || !_harvestCached.IsActive) && (!(_smeltCached != null) || !_smeltCached.IsActive) && (!(_huntCached != null) || !_huntCached.IsActive) && (!(_restCached != null) || (!_restCached.IsNavigating && !_restCached.IsResting)) && !(_ai.PendingCartAttach != null) && !_ai.PendingMoveTarget.HasValue && !(_ai.PendingDepositContainer != null) && !MHR_ToolKeeper.IsBusy(this) && !_ai.IsRecoveringTombstone && GetFollow() && num != 4)
			{
				_ai.SetFollowTarget(MHR_Owner.Go(this));
				CompanionsPlugin.Log.LogDebug("[Setup] Follow target was null — restored to its player " + $"(mode={num} harvestActive={_harvestCached?.IsActive ?? false})");
			}
		}
	}

	private void MHR_StampOwnerDeathRule()
	{
		_mhrRuleStampTimer -= Time.deltaTime;
		if (!(_mhrRuleStampTimer <= 0f))
		{
			return;
		}
		_mhrRuleStampTimer = 2f;
		if (_nview == null || !_nview.IsOwner())
		{
			return;
		}
		ZDO zDO = _nview.GetZDO();
		if (zDO == null || Player.m_localPlayer == null)
		{
			return;
		}
		long playerID = Player.m_localPlayer.GetPlayerID();
		if (zDO.GetString(OwnerHash) == playerID.ToString())
		{
			int value = ModConfig.DeathItemRule.Value;
			if (zDO.GetInt(DeathItemRuleHash, -1) != value)
			{
				zDO.Set(DeathItemRuleHash, value);
			}
		}
	}

	// Fase 3.1: one place that switches a companion's work mode, shared by the radial menu (toggle) and the chat
	// orders (set, no toggle). The radial's own SetActionMode did exactly this; it now calls this method.
	internal static void ApplyActionMode(CompanionSetup companion, int mode, bool toggle)
	{
		if (companion == null)
		{
			return;
		}
		ZNetView nv = companion.GetComponent<ZNetView>();
		ZDO zdo = nv != null ? nv.GetZDO() : null;
		if (zdo == null)
		{
			return;
		}
		int current = zdo.GetInt(ActionModeHash);
		int next = (toggle && current == mode) ? 0 : mode;
		zdo.Set(ActionModeHash, next);
		// Fase 3.2: the target belongs to one job. Picking another job (or turning the job off) drops it, so a stale
		// target can never stop a job the player didn't ask for. A chat order sets its own right after this call.
		if (next == 0 || zdo.GetInt(OrderCapModeHash, 0) != next)
		{
			ClearOrderCap(companion);
		}
		MHR_JobRules.OnJobSet(companion, next);
		companion.GetComponent<HarvestController>()?.NotifyActionModeChanged();
		companion.ApplyFollowMode(next);
		companion.GetComponent<SmeltController>()?.NotifyActionModeChanged();
		companion.GetComponent<FarmController>()?.NotifyActionModeChanged();
		companion.GetComponent<FishController>()?.NotifyActionModeChanged();
		companion.GetComponent<CookController>()?.NotifyActionModeChanged();
		companion.GetComponent<HuntController>()?.NotifyActionModeChanged();
		companion.GetComponent<UpkeepController>()?.NotifyActionModeChanged();
	}

	// ---- Fase 3.2 / 3.2c: the target a chat order put on a gathering job ----
	// "colete 5 madeiras" used to be a job like any other, so the companion kept chopping until the config limit
	// (WoodLimit, 500 by default). The target is kept per companion in its ZDO (so several companions each stop at
	// their own number and it survives a reload) and is only read by the gather jobs.
	// Fase 3.2c: the number is a TOTAL counted in the companion's own pack ("colete 10 madeiras" = have 10 wood on
	// you - not "10 more", which is how Fase 3.2b read it), and it belongs to the item the order named (see
	// OrderCapItemHash), not to every item on the job's list.

	/// <summary>The target an order put on this mode for this companion; 0 = no target (or it belongs to another
	/// mode). Reads the ZDO straight: a dictionary lookup, at most once per candidate per scan - never per frame.</summary>
	internal static int OrderCap(Component c, int mode)
	{
		ZDO zdo = ZdoOf(c, out _);
		if (zdo == null)
		{
			return 0;
		}
		int cap = zdo.GetInt(OrderCapHash, 0);
		return (cap > 0 && zdo.GetInt(OrderCapModeHash, 0) == mode) ? cap : 0;
	}

	/// <summary>Fase 3.2c: the item the target is about ("Wood"), or "" when there is no target, when it belongs to
	/// another mode, or when the order named no item the game knows - in that last case the target is the whole job
	/// (every item on the mode's list), exactly as Fase 3.2 did it.</summary>
	internal static string OrderCapItem(Component c, int mode)
	{
		ZDO zdo = ZdoOf(c, out _);
		if (zdo == null)
		{
			return "";
		}
		int cap = zdo.GetInt(OrderCapHash, 0);
		if (cap <= 0 || zdo.GetInt(OrderCapModeHash, 0) != mode)
		{
			return "";
		}
		return zdo.GetString(OrderCapItemHash, "") ?? "";
	}

	/// <summary>Remembers the target of an order ("colete 5 madeiras"). Owner only: it is written where the order was
	/// given (the caller has claimed the companion by then). <paramref name="item"/> is the item prefab the order
	/// named, or null/"" when it couldn't be pinned to one item.</summary>
	internal static void SetOrderCap(Component c, int mode, int amount, string item)
	{
		ZNetView nv;
		ZDO zdo = ZdoOf(c, out nv);
		if (zdo == null || !nv.IsOwner())
		{
			CompanionsPlugin.Log?.LogWarning("[IA] Order target not written: not the owner of this companion");
			return;
		}
		item = amount > 0 ? (item ?? "") : "";
		if (amount > 0 && zdo.GetInt(OrderCapHash, 0) == amount && zdo.GetInt(OrderCapModeHash, 0) == mode && (zdo.GetString(OrderCapItemHash, "") ?? "") == item)
		{
			return;   // already there - don't dirty the ZDO for nothing
		}
		ClearOrderSeen(c);   // Fase 3.2c: a new target counts the pack from scratch
		zdo.Set(OrderCapHash, amount);
		zdo.Set(OrderCapModeHash, amount > 0 ? mode : 0);
		zdo.Set(OrderCapItemHash, item);
	}

	/// <summary>Forgets the target (order finished or cancelled, or another job picked).</summary>
	internal static void ClearOrderCap(Component c)
	{
		ClearOrderSeen(c);   // Fase 3.2c: the pack count goes with the target
		ZNetView nv;
		ZDO zdo = ZdoOf(c, out nv);
		if (zdo == null || !nv.IsOwner())
		{
			return;
		}
		if (zdo.GetInt(OrderCapHash, 0) == 0 && zdo.GetInt(OrderCapModeHash, 0) == 0 && string.IsNullOrEmpty(zdo.GetString(OrderCapItemHash, "")))
		{
			return;
		}
		zdo.Set(OrderCapHash, 0);
		zdo.Set(OrderCapModeHash, 0);
		zdo.Set(OrderCapItemHash, "");
	}

	/// <summary>Fase 3.2c: how much of the ordered item the companion has had in its own pack during this order -
	/// the pack count of right now, or the highest it has reached (so unloading part of a full pack into a chest,
	/// which the mod does on its own, doesn't make the order start over). One dictionary lookup per candidate per
	/// scan, never per frame.</summary>
	internal static int OrderSeen(Component c, int inPackNow)
	{
		if (c == null || c.gameObject == null)
		{
			return inPackNow;
		}
		int id = c.gameObject.GetInstanceID();
		int seen;
		if (s_orderSeen.TryGetValue(id, out seen) && seen >= inPackNow)
		{
			return seen;
		}
		if (s_orderSeen.Count > 256)
		{
			s_orderSeen.Clear();   // same soft cap the other session caches use
		}
		s_orderSeen[id] = inPackNow;
		return inPackNow;
	}

	/// <summary>Fase 3.2c: forgets the pack count of the order (called with the target itself).</summary>
	private static void ClearOrderSeen(Component c)
	{
		if (c == null || c.gameObject == null)
		{
			return;
		}
		s_orderSeen.Remove(c.gameObject.GetInstanceID());
	}

	/// <summary>The target was met: the order is done. Forget it and go back to following, exactly like "stop".</summary>
	internal static void CompleteOrderCap(Component c)
	{
		// The caller is the gather job (HarvestController), not the CompanionSetup: resolve the companion from
		// whatever component was passed, otherwise the target is never cleared and the job never stops.
		CompanionSetup companion = c as CompanionSetup ?? (c != null ? c.GetComponent<CompanionSetup>() : null);
		if (companion == null)
		{
			CompanionsPlugin.Log?.LogWarning("[IA] Order target: no CompanionSetup on the component that reached the target");
			return;
		}
		ClearOrderCap(companion);
		MHR_JobRules.FollowSetByPlayer(companion);
		companion.SetFollow(true);
		ApplyActionMode(companion, ModeFollow, toggle: false);
	}

	private static ZDO ZdoOf(Component c, out ZNetView nv)
	{
		nv = c != null ? c.GetComponent<ZNetView>() : null;
		return (nv != null && nv.IsValid()) ? nv.GetZDO() : null;
	}

	internal void ApplyFollowMode(int mode, bool force = false)
	{
		if (_ai == null)
		{
			return;
		}
		bool follow = GetFollow();
		CompanionsPlugin.Log.LogDebug($"[Setup] ApplyFollowMode — mode={mode} follow={follow} force={force}");
		switch (mode)
		{
		case 0:
			if (follow && MHR_Owner.Of(this) != null)
			{
				_ai.SetFollowTarget(MHR_Owner.Go(this));
				CompanionsPlugin.Log.LogDebug($"[Setup]   → Follow player (mode={mode})");
			}
			else
			{
				_ai.SetFollowTarget(null);
				CompanionsPlugin.Log.LogDebug("[Setup]   → Follow OFF, no StayHome — idle");
			}
			break;
		case 1:
		case 2:
		case 3:
		case 5:
		case 6:
		case 7:
		case 8:
		case 17:
			if (!force)
			{
				HarvestController component = GetComponent<HarvestController>();
				if (component != null && component.IsActive)
				{
					CompanionsPlugin.Log.LogDebug($"[Setup]   → Gather mode={mode}, harvest active — skipping follow override");
					break;
				}
				SmeltController component2 = GetComponent<SmeltController>();
				if (component2 != null && component2.IsActive)
				{
					CompanionsPlugin.Log.LogDebug($"[Setup]   → Smelt mode={mode}, smelt active — skipping follow override");
					break;
				}
				FarmController component3 = GetComponent<FarmController>();
				if (component3 != null && component3.IsActive)
				{
					CompanionsPlugin.Log.LogDebug($"[Setup]   → Farm mode={mode}, farm active — skipping follow override");
					break;
				}
				HuntController component4 = GetComponent<HuntController>();
				if (component4 != null && component4.IsActive)
				{
					CompanionsPlugin.Log.LogDebug($"[Setup]   → Hunt mode={mode}, hunt active — skipping follow override");
					break;
				}
				CookController component5 = GetComponent<CookController>();
				if (component5 != null && component5.IsActive)
				{
					CompanionsPlugin.Log.LogDebug($"[Setup]   → Cook mode={mode}, cook active — skipping follow override");
					break;
				}
			}
			if (follow && MHR_Owner.Of(this) != null)
			{
				_ai.SetFollowTarget(MHR_Owner.Go(this));
				CompanionsPlugin.Log.LogDebug($"[Setup]   → Follow player (mode={mode})");
			}
			else
			{
				_ai.SetFollowTarget(null);
				CompanionsPlugin.Log.LogDebug($"[Setup]   → Follow OFF, gather mode={mode} — idle");
			}
			break;
		case 9:
			_ai.SetFollowTarget(null);
			_ai.SetPatrolPoint();
			CompanionsPlugin.Log.LogDebug("[Setup]   → Fish mode — stationary, patrol at current pos");
			break;
		case 4:
			_ai.SetFollowTarget(null);
			_ai.SetPatrolPoint();
			CompanionsPlugin.Log.LogDebug($"[Setup]   → Stay/patrol at {base.transform.position:F1}");
			break;
		default:
			if (follow && MHR_Owner.Of(this) != null)
			{
				_ai.SetFollowTarget(MHR_Owner.Go(this));
				CompanionsPlugin.Log.LogDebug($"[Setup]   → Follow player (default fallback, mode={mode})");
			}
			else
			{
				_ai.SetFollowTarget(null);
				CompanionsPlugin.Log.LogDebug($"[Setup]   → Follow OFF (default fallback, mode={mode})");
			}
			break;
		}
	}

	private void AutoEquipBest()
	{
		Inventory inventory = _humanoid.GetInventory();
		if (inventory == null || TorchOpener.IsActive(_humanoid))
		{
			return;
		}
		if (_equipQueue.Count > 0)
		{
			_autoEquipPending = true;
			_autoEquipCooldown = 1f;
			return;
		}
		ClearEquipQueue();
		List<ItemDrop.ItemData> allItems = inventory.GetAllItems();
		ItemDrop.ItemData itemData = null;
		float num = 0f;
		ItemDrop.ItemData itemData2 = null;
		float num2 = 0f;
		ItemDrop.ItemData itemData3 = null;
		float num3 = 0f;
		ItemDrop.ItemData itemData4 = null;
		float num4 = 0f;
		ItemDrop.ItemData itemData5 = null;
		float num5 = 0f;
		ItemDrop.ItemData itemData6 = null;
		float num6 = 0f;
		ItemDrop.ItemData itemData7 = null;
		float num7 = 0f;
		ItemDrop.ItemData bestItem = null;
		float num8 = 0f;
		ItemDrop.ItemData bestItem2 = null;
		float num9 = 0f;
		ItemDrop.ItemData itemData8 = null;
		float num10 = 0f;
		foreach (ItemDrop.ItemData item in allItems)
		{
			if (item == null || item.m_shared == null || (item.m_shared.m_useDurability && item.m_durability <= 0f) || item.m_shared.m_animationState == ItemDrop.ItemData.AnimationState.FishingRod)
			{
				continue;
			}
			ItemDrop.ItemData.ItemType itemType = item.m_shared.m_itemType;
			float num11 = WeaponLogic.Score(item, null);
			float armor = item.GetArmor();
			switch (itemType)
			{
			case ItemDrop.ItemData.ItemType.OneHandedWeapon:
			case ItemDrop.ItemData.ItemType.Torch:
				if (num11 > num)
				{
					itemData = item;
					num = num11;
				}
				break;
			case ItemDrop.ItemData.ItemType.TwoHandedWeapon:
			case ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft:
				if (item.GetDamage().m_pickaxe > 0f)
				{
					if (num11 > num3)
					{
						itemData3 = item;
						num3 = num11;
					}
				}
				else if (num11 > num2)
				{
					itemData2 = item;
					num2 = num11;
				}
				break;
			case ItemDrop.ItemData.ItemType.Tool:
				if (num11 > num3)
				{
					itemData3 = item;
					num3 = num11;
				}
				break;
			case ItemDrop.ItemData.ItemType.Chest:
				if (armor > num5)
				{
					itemData5 = item;
					num5 = armor;
				}
				break;
			case ItemDrop.ItemData.ItemType.Legs:
				if (armor > num6)
				{
					itemData6 = item;
					num6 = armor;
				}
				break;
			case ItemDrop.ItemData.ItemType.Helmet:
				if (armor > num7)
				{
					itemData7 = item;
					num7 = armor;
				}
				break;
			case ItemDrop.ItemData.ItemType.Shoulder:
				if (armor > num8)
				{
					bestItem = item;
					num8 = armor;
				}
				break;
			case ItemDrop.ItemData.ItemType.Utility:
				if (armor > num9)
				{
					bestItem2 = item;
					num9 = armor;
				}
				break;
			// 1.4.83: carry the best shield with a one-hander, like a player (it was only picked up once a fight started)
			// 1.4.90: the best one for parrying, the same pick the combat code makes when he's healthy and not surrounded
			// (ShieldLogic) - not the highest block power, which is always the tower shield (user: "favoring the tower
			// shield again"). The combat code still swaps to the tower shield when he's hurt or outnumbered.
			case ItemDrop.ItemData.ItemType.Shield:
			{
				float shieldScore = item.m_shared.m_timedBlockBonus * 1000f + item.GetBaseBlockPower();
				if (shieldScore > num4)
				{
					itemData4 = item;
					num4 = shieldScore;
				}
				break;
			}
			case ItemDrop.ItemData.ItemType.Trinket:
				if (armor > num10)
				{
					itemData8 = item;
					num10 = armor;
				}
				break;
			}
		}
		EquipDiag.OnScan(allItems, itemData);
		CompanionsPlugin.Log.LogDebug(string.Format("[Setup] AutoEquipBest — 1H=\"{0}\"({1:F0}) ", itemData?.m_shared?.m_name ?? "none", num) + string.Format("2H=\"{0}\"({1:F0}) ", itemData2?.m_shared?.m_name ?? "none", num2) + string.Format("shield=\"{0}\"(block={1:F0}) ", itemData4?.m_shared?.m_name ?? "none", num4) + "helm=\"" + (itemData7?.m_shared?.m_name ?? "none") + "\" chest=\"" + (itemData5?.m_shared?.m_name ?? "none") + "\" legs=\"" + (itemData6?.m_shared?.m_name ?? "none") + "\" trinket=\"" + (itemData8?.m_shared?.m_name ?? "none") + "\"");
		ItemDrop.ItemData itemData9 = GetEquipSlot(_rightItemField);
		ItemDrop.ItemData itemData10 = GetEquipSlot(_leftItemField);
		bool flag = itemData2 != null && (itemData == null || num2 >= num);
		ItemDrop.ItemData itemData11 = (flag ? itemData2 : itemData);
		CompanionsPlugin.Log.LogDebug($"[Setup] Weapon decision: use2H={flag} " + "desired=\"" + (itemData11?.m_shared?.m_name ?? "none") + "\" current=\"" + (itemData9?.m_shared?.m_name ?? "none") + "\" " + $"needsSwap={itemData11 != null && itemData9 != itemData11}");
		bool flag2 = _combatAI != null && _combatAI.IsEngaged;
		bool flag3 = (itemData9 != null && itemData9.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Bow) || (itemData10 != null && itemData10.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Bow);
		if (!flag2 && (!flag3 || GetCombatStance() != 2) && itemData11 != null && itemData9 != itemData11)
		{
			if (itemData9 != null)
			{
				CompanionsPlugin.Log.LogDebug("[Setup] Unequip right: \"" + (itemData9.m_shared?.m_name ?? "?") + "\"");
				_humanoid.UnequipItem(itemData9, triggerEquipEffects: false);
			}
			if (IsTwoHandedWeapon(itemData11) && itemData10 != null)
			{
				CompanionsPlugin.Log.LogDebug("[Setup] Unequip left for 2H: \"" + (itemData10.m_shared?.m_name ?? "?") + "\"");
				_humanoid.UnequipItem(itemData10, triggerEquipEffects: false);
				itemData10 = null;
			}
			QueueEquip(itemData11);
			CompanionsPlugin.Log.LogDebug("[Setup] Queued right: \"" + (itemData11.m_shared?.m_name ?? "?") + "\"");
			itemData9 = itemData11;
		}
		bool flag4 = itemData9 != null && IsTwoHandedWeapon(itemData9);
		bool flag5 = itemData10 != null && IsTwoHandedWeapon(itemData10);
		if (!flag4 && !flag5 && itemData4 != null)
		{
			if (itemData10 != itemData4)
			{
				if (itemData10 != null)
				{
					CompanionsPlugin.Log.LogDebug("[Setup] Unequip left for shield swap: \"" + (itemData10.m_shared?.m_name ?? "?") + "\"");
					_humanoid.UnequipItem(itemData10, triggerEquipEffects: false);
				}
				QueueEquip(itemData4);
				CompanionsPlugin.Log.LogDebug("[Setup] Queued shield: \"" + (itemData4.m_shared?.m_name ?? "?") + "\"");
			}
		}
		else if (flag4 && itemData10 != null)
		{
			CompanionsPlugin.Log.LogDebug("[Setup] Unequip left (2H in right): \"" + (itemData10.m_shared?.m_name ?? "?") + "\"");
			_humanoid.UnequipItem(itemData10, triggerEquipEffects: false);
		}
		if (CanWearArmor())
		{
			EquipBestArmorSlot(_chestItemField, itemData5);
			EquipBestArmorSlot(_legItemField, itemData6);
			EquipBestArmorSlot(_helmetItemField, itemData7);
			EquipBestArmorSlot(_shoulderItemField, bestItem);
		}
		EquipBestArmorSlot(_utilityItemField, bestItem2);
		EquipBestArmorSlot(_trinketItemField, itemData8);
	}

	private void UpdateEquipQueue(float dt)
	{
		TorchOpener.Tick(_humanoid);
		if (_equipQueue.Count == 0)
		{
			if (_equipAnimActive)
			{
				if (_zanim != null && CanWearArmor())
				{
					_zanim.SetBool("equipping", value: false);
				}
				_equipAnimActive = false;
			}
			return;
		}
		Inventory inventory = _humanoid?.GetInventory();
		while (_equipQueue.Count > 0 && (inventory == null || !inventory.ContainsItem(_equipQueue[0])))
		{
			CompanionsPlugin.Log.LogDebug("[Setup] Equip queue: skipping \"" + (_equipQueue[0]?.m_shared?.m_name ?? "?") + "\" — no longer in inventory");
			_equipQueue.RemoveAt(0);
		}
		if (_equipQueue.Count == 0)
		{
			return;
		}
		if (!_equipAnimActive)
		{
			_equipAnimActive = true;
			if (_zanim != null && CanWearArmor())
			{
				_zanim.SetBool("equipping", value: true);
			}
			float equipDuration = _equipQueue[0].m_shared.m_equipDuration;
			_equipTimer = ((equipDuration > 0f) ? equipDuration : 0.1f);
			CompanionsPlugin.Log.LogDebug("[Setup] Equip queue: started \"" + _equipQueue[0].m_shared.m_name + "\" " + $"duration={_equipTimer:F1}s ({_equipQueue.Count} items queued)");
			return;
		}
		_equipTimer -= dt;
		if (_equipTimer > 0f)
		{
			return;
		}
		ItemDrop.ItemData itemData = _equipQueue[0];
		_equipQueue.RemoveAt(0);
		_equipping = true;
		try
		{
			_humanoid.EquipItem(itemData);
			itemData.m_shared.m_equipEffect.Create(base.transform.position, Quaternion.identity, null, 1f, -1, ZDOID.None);
		}
		finally
		{
			_equipping = false;
		}
		CompanionsPlugin.Log.LogDebug($"[Setup] Equip queue: equipped \"{itemData.m_shared.m_name}\" ({_equipQueue.Count} remaining)");
		if (_equipQueue.Count > 0)
		{
			float equipDuration2 = _equipQueue[0].m_shared.m_equipDuration;
			_equipTimer = ((equipDuration2 > 0f) ? equipDuration2 : 0.1f);
			CompanionsPlugin.Log.LogDebug($"[Setup] Equip queue: next \"{_equipQueue[0].m_shared.m_name}\" duration={_equipTimer:F1}s");
			return;
		}
		if (_zanim != null && CanWearArmor())
		{
			_zanim.SetBool("equipping", value: false);
		}
		_equipAnimActive = false;
	}

	private void UpdateMinimapPin()
	{
		if (Minimap.instance == null || _pinHiddenUntil > Time.time)
		{
			return;
		}
		if (s_pinMinimapInstance != Minimap.instance)
		{
			s_pinMinimapInstance = Minimap.instance;
			s_persistentPins.Clear();
			_minimapPin = null;
		}
		ZDO zDO = _nview?.GetZDO();
		if (zDO == null || Player.m_localPlayer == null)
		{
			RemoveMinimapPin();
			return;
		}
		_zdoid = zDO.m_uid;
		string text = zDO.GetString(OwnerHash);
		long playerID = Player.m_localPlayer.GetPlayerID();
		if (playerID == 0L)
		{
			return;
		}
		if (text != playerID.ToString())
		{
			RemoveMinimapPin();
			return;
		}
		if (_minimapPin == null && s_persistentPins.TryGetValue(_zdoid, out var value))
		{
			_minimapPin = value;
			s_persistentPins.Remove(_zdoid);
		}
		if (_minimapPin == null)
		{
			string arg = zDO.GetString(NameHash, "Companion");
			_minimapPin = Minimap.instance.AddPin(base.transform.position, Minimap.PinType.Player, arg, save: false, isChecked: false, 0L);
			CompanionsPlugin.Log.LogDebug($"[Minimap] Created pin for \"{arg}\" at {base.transform.position:F1}");
		}
		if (_minimapPin.m_pos != base.transform.position)
		{
			_minimapPin.m_pos = base.transform.position;
		}
	}

	private void RemoveMinimapPin()
	{
		if (_minimapPin != null && Minimap.instance != null)
		{
			CompanionsPlugin.Log.LogDebug("[Minimap] Removed pin for \"" + _minimapPin.m_name + "\"");
			Minimap.instance.RemovePin(_minimapPin);
			_minimapPin = null;
		}
	}

	public void HidePinFor(float seconds)
	{
		_pinHiddenUntil = Time.time + seconds;
		RemoveMinimapPin();
	}

	private void EnsureActionModeMigration()
	{
		if (_nview == null || !_nview.IsOwner())
		{
			return;
		}
		ZDO zDO = _nview.GetZDO();
		if (zDO == null)
		{
			return;
		}
		int num = zDO.GetInt(ActionModeSchemaHash);
		if (num < 3)
		{
			int num2 = zDO.GetInt(ActionModeHash);
			if (num2 == 2)
			{
				num2 = 4;
			}
			if ((num2 < 0 || num2 > 9) && num2 != 15 && num2 != 16 && num2 != 17)
			{
				num2 = 0;
			}
			zDO.Set(ActionModeHash, num2);
			zDO.Set(ActionModeSchemaHash, 3);
		}
		if (!zDO.GetBool(WanderFollowMigratedHash))
		{
			zDO.Set(WanderHash, value: true);
			zDO.Set(WanderFollowMigratedHash, value: true);
			CompanionsPlugin.Log.LogDebug("[Setup] Wander switched on (it now also controls how the companion moves around while following)");
		}
		// 1.4.117: Restock is part of Repair and Restock now, and Homestead is retired
		if (zDO.GetInt(ActionModeHash) == ModeRestock)
		{
			zDO.Set(ActionModeHash, ModeRepairBuildings);
			CompanionsPlugin.Log.LogInfo("[Setup] Restock job moved to Repair and Restock");
		}
		if (zDO.GetBool(StayHomeHash))
		{
			zDO.Set(StayHomeHash, value: false);
			CompanionsPlugin.Log.LogInfo("[Setup] Homestead is retired - turned off (Wander still lets it move around home)");
		}
		// 1.4.78: a job and Follow can't both be on (saves from before have both)
		int mode = zDO.GetInt(ActionModeHash);
		if (MHR_JobRules.IsJobMode(mode))
		{
			MHR_JobRules.OnJobSet(this, mode);
		}
	}

	private void RestoreActionMode()
	{
		if (_ai == null || Player.m_localPlayer == null)
		{
			return;
		}
		ZDO zDO = _nview?.GetZDO();
		if (zDO == null)
		{
			return;
		}
		string text = zDO.GetString(OwnerHash);
		long playerID = Player.m_localPlayer.GetPlayerID();
		string text2 = playerID.ToString();
		if (playerID != 0L)
		{
			if (string.IsNullOrEmpty(text))
			{
				zDO.Set(OwnerHash, text2);
				text = text2;
			}
			if (!(text != text2))
			{
				int mode = zDO.GetInt(ActionModeHash);
				ApplyFollowMode(mode);
			}
		}
	}

	private void RestoreName()
	{
		ZDO zDO = _nview?.GetZDO();
		if (zDO == null)
		{
			return;
		}
		string value = zDO.GetString(NameHash);
		if (!string.IsNullOrEmpty(value))
		{
			Character component = GetComponent<Character>();
			if (component != null)
			{
				component.m_name = value;
			}
		}
	}

	/// <summary>
	/// Fase 3.14: renames the companion - the same two steps the interact panel does when the player edits the name box
	/// (<c>HC_Name</c> in the ZDO, which every tab/hover/wheel/name order reads, and the <c>Character.m_name</c> that the
	/// game shows). Written by the owner only: the claim is the same one an order makes, so a rename from a panel works
	/// in multiplayer too. <paramref name="newName"/> is expected already trimmed and limited by the caller; an empty
	/// value is accepted (the companion then shows the generic name, exactly like the interact panel's empty box).
	/// </summary>
	internal bool Rename(string newName)
	{
		ZDO zdo = _nview?.GetZDO();
		if (zdo == null)
		{
			return false;
		}
		if (!_nview.IsOwner())
		{
			_nview.ClaimOwnership();
			if (!_nview.IsOwner())
			{
				CompanionsPlugin.Log?.LogWarning("[Rename] No ownership of the companion's ZDO - the name was not written");
				return false;
			}
		}
		zdo.Set(NameHash, newName ?? "");
		Character character = GetComponent<Character>();
		if (character != null)
		{
			character.m_name = string.IsNullOrEmpty(newName) ? ModLocalization.Loc("hc_msg_name_default") : newName;
		}
		return true;
	}

	private void RegisterInventoryCallback()
	{
		if (!(_humanoid == null))
		{
			Inventory inventory = _humanoid.GetInventory();
			if (inventory != null)
			{
				_inventoryCallback = OnInventoryChanged;
				inventory.m_onChanged = (Action)Delegate.Combine(inventory.m_onChanged, _inventoryCallback);
			}
		}
	}

	private void AddStarterGear(ZDO zdo)
	{
		if (_nview.IsOwner())
		{
			zdo.Set(StarterGearHash, value: true);
		}
	}

	private void OnInventoryChanged()
	{
		if (_equipping || _humanoid == null)
		{
			return;
		}
		bool flag = _nview != null && _nview.IsOwner();
		_equipping = true;
		try
		{
			UnequipMissingEquippedItems();
			if (flag && !SuppressAutoEquip)
			{
				if (_autoEquipCooldown > 0f)
				{
					_autoEquipPending = true;
					CompanionsPlugin.Log.LogDebug($"[Setup] OnInventoryChanged — deferred (cooldown={_autoEquipCooldown:F2}s)");
				}
				else
				{
					CompanionsPlugin.Log.LogDebug("[Setup] OnInventoryChanged — auto-equip triggered");
					AutoEquipBest();
					_autoEquipCooldown = 0.5f;
				}
			}
			if (flag)
			{
				NotifyControllersInventoryChanged();
			}
		}
		finally
		{
			_equipping = false;
		}
	}

	private void UnregisterInventoryCallback()
	{
		if (!(_humanoid == null) && _inventoryCallback != null)
		{
			Inventory inventory = _humanoid.GetInventory();
			if (inventory != null)
			{
				inventory.m_onChanged = (Action)Delegate.Remove(inventory.m_onChanged, _inventoryCallback);
				_inventoryCallback = null;
			}
		}
	}

	internal void PrepareForDespawn()
	{
		_removePinOnDestroy = true;
		MHR_Roster.Forget(this);
	}

	internal void DespawnWithTombstone()
	{
		_mhrDespawning = true;
		if (_humanoid == null)
		{
			return;
		}
		Inventory inventory = _humanoid.GetInventory();
		if (inventory == null || inventory.NrOfItems() == 0)
		{
			return;
		}
		string companionName = _humanoid.m_name ?? ModLocalization.Loc("hc_msg_name_default");
		ZDO zDO = _nview?.GetZDO();
		if (zDO != null)
		{
			string text = zDO.GetString(NameHash);
			if (!string.IsNullOrEmpty(text))
			{
				companionName = text;
			}
		}
		Vector3 centerPoint = _humanoid.GetCenterPoint();
		long ticks = DateTime.UtcNow.Ticks;
		_mhrDespawning = true;
		CreateCompanionTombstone(companionName, centerPoint, ticks, "", "", "", 0, 0, follow: false, stayHome: false, autoPickup: false, wander: false, commandable: false, -1, "", hasHome: false, Vector3.zero, 0, hasHomeBed: false, Vector3.zero);
	}

	private void CreateCompanionTombstone(string companionName, Vector3 position, long tombstoneId, string prefabName, string appearance, string ownerId, int actionMode, int actionModeSchema, bool follow, bool stayHome, bool autoPickup, bool wander, bool commandable, int formationSlot, string skillsSerialized, bool hasHome, Vector3 homePos, int combatStance, bool hasHomeBed, Vector3 homeBedPos)
	{
		// 1.5.1: the tombstone prefab came from the local player only - when the player was dead too (both drowned at sea,
		// GitHub issue #10) there was no local player, no tombstone and no respawn data, and the companion was gone for
		// good. The game's player prefab always has it.
		GameObject gameObject = Player.m_localPlayer?.m_tombstone;
		if (gameObject == null)
		{
			gameObject = Game.instance?.m_playerPrefab?.GetComponent<Player>()?.m_tombstone;
		}
		if (gameObject == null)
		{
			gameObject = ZNetScene.instance?.GetPrefab("Player_tombstone");
		}
		if (gameObject == null)
		{
			CompanionsPlugin.Log.LogError("[Setup] Could not find tombstone prefab!");
			return;
		}
		SuppressAutoEquip = true;
		string inventorySerialized = "";
		Inventory inventory;
		if ((object)_humanoid != null && (inventory = _humanoid.GetInventory()) != null)
		{
			int num = MHR_GetDeathItemRule();
			int num2 = 0;
			int num3 = 0;
			List<ItemDrop.ItemData> list = new List<ItemDrop.ItemData>(inventory.GetAllItems());
			Inventory inventory2 = default(Inventory);
			for (int i = 0; i < list.Count; i++)
			{
				ItemDrop.ItemData itemData = list[i];
				int num4 = MHR_DeathItemAction(num, itemData.m_equipped);
				if (num4 == 1)
				{
					continue;
				}
				inventory.RemoveItem(itemData);
				if (num4 == 2)
				{
					num3++;
					continue;
				}
				if (inventory2 == null)
				{
					inventory2 = new Inventory("hc_tomb_keep", null, 8, 4);
				}
				if (!inventory2.AddItem(itemData))
				{
					inventory.AddItem(itemData);
				}
				else
				{
					num2++;
				}
			}
			CompanionsPlugin.Log.LogInfo($"[Setup] Death items (rule={num}): kept {num2}, deleted {num3}, the rest go to the tombstone");
			if (inventory2 != null && inventory2.NrOfItems() != 0)
			{
				ZPackage zPackage = new ZPackage();
				inventory2.Save(zPackage);
				inventorySerialized = zPackage.GetBase64();
				CompanionsPlugin.Log.LogInfo($"[Setup] Companion keeps {inventory2.NrOfItems()} item(s) through death/respawn");
			}
		}
		inventory = _humanoid?.GetInventory();
		int num5 = inventory?.NrOfItems() ?? 0;
		CompanionsPlugin.Log.LogInfo($"[Setup] Creating tombstone for \"{companionName}\" — {num5} items, " + $"tombstoneId={tombstoneId}");
		if (inventory != null && num5 > 0)
		{
			_humanoid.UnequipAllItems();
			int num6 = 0;
			foreach (ItemDrop.ItemData allItem in inventory.GetAllItems())
			{
				if (allItem.m_equipped)
				{
					num6++;
				}
				allItem.m_equipped = false;
			}
			CompanionsPlugin.Log.LogDebug($"[Setup] Unequipped all items for grave transfer (cleared {num6} stale equipped flags)");
		}
		GameObject gameObject2 = UnityEngine.Object.Instantiate(gameObject, position, base.transform.rotation);
		CompanionsPlugin.Log.LogDebug($"[Setup] Tombstone spawned at {position:F1}");
		if (inventory != null && num5 > 0)
		{
			Container component = gameObject2.GetComponent<Container>();
			if (component != null)
			{
				Inventory inventory3 = component.GetInventory();
				if (inventory3 != null)
				{
					inventory3.MoveInventoryToGrave(inventory);
					int num7 = inventory3.NrOfItems();
					CompanionsPlugin.Log.LogInfo($"[Setup] Grave transfer complete — {num7} items in tombstone");
					int width = inventory3.GetWidth();
					int num8 = 0;
					foreach (ItemDrop.ItemData allItem2 in inventory3.GetAllItems())
					{
						allItem2.m_gridPos = new Vector2i(num8 % width, num8 / width);
						num8++;
					}
				}
			}
		}
		TombStone component2 = gameObject2.GetComponent<TombStone>();
		if (component2 != null)
		{
			component2.Setup(companionName, tombstoneId);
			CompanionsPlugin.Log.LogDebug($"[Setup] Tombstone ownership set — name=\"{companionName}\" id={tombstoneId}");
		}
		ZNetView component3 = gameObject2.GetComponent<ZNetView>();
		if (component3?.GetZDO() != null)
		{
			ZDO zDO = component3.GetZDO();
			zDO.Set(TombstoneIdHash, tombstoneId);
			if (ownerId != null)
			{
				zDO.Set(MHR_TombOwnerHash, ownerId);
			}
			zDO.Set(MHR_TombCompanionIdHash, MHR_EnsureCompanionId());
			Inventory inventory4 = gameObject2.GetComponent<Container>()?.GetInventory();
			if (inventory4 != null)
			{
				zDO.Set(TombInvWidthHash, inventory4.GetWidth());
				zDO.Set(TombInvHeightHash, inventory4.GetHeight());
				zDO.Set(MHR_TombItemsHash, inventory4.NrOfItems());
			}
			CompanionTombstoneResurrect.WriteRespawnData(zDO, prefabName, companionName, appearance, ownerId, actionMode, actionModeSchema, follow, stayHome, autoPickup, wander, commandable, formationSlot, skillsSerialized, hasHome, homePos, combatStance, hasHomeBed, homeBedPos, inventorySerialized);
			// Fase 3.10: the AI profile (voice and personality) of this companion goes with the respawn data, so the
			// companion that comes back is the same one in the way it talks - see CompanionTombstoneResurrect.
			CompanionTombstoneResurrect.WriteAiProfile(zDO, _nview?.GetZDO());
			// 1.4.150: also whether its job had turned Follow off (so turning the job off after a respawn turns it back on)
			zDO.Set(CompanionTombstoneResurrect.RespawnFollowPausedHash, _nview?.GetZDO()?.GetBool(CompanionTombstoneResurrect.FollowPausedByJobHash) ?? false);
			// 1.5.1: which copy of the companion this is - a tombstone of an older copy (the companion was recovered since) won't respawn it
			zDO.Set(MHR_Roster.RespawnGenHash, _nview?.GetZDO()?.GetInt(MHR_Roster.GenHash) ?? 0);
			gameObject2.AddComponent<CompanionTombstoneResurrect>();
			MHR_RespawnTracker.OnCompanionDied(zDO, ownerId);
			CompanionsPlugin.Log.LogInfo("[Setup] Tombstone resurrection data written and component attached");
		}
		if (Minimap.instance != null)
		{
			string text = companionName + " " + ModLocalization.Loc("hc_minimap_grave");
			Minimap.instance.AddPin(position, Minimap.PinType.Death, text, save: true, isChecked: false, 0L);
			CompanionsPlugin.Log.LogInfo($"[Setup] Added tombstone minimap pin at {position:F1}");
		}
		SuppressAutoEquip = false;
	}

	internal int MHR_GetDeathItemRule()
	{
		if (_mhrDespawning)
		{
			return 3;
		}
		int num = ModConfig.DeathItemRule.Value;
		if (!(_nview == null))
		{
			ZDO zDO = _nview.GetZDO();
			if (zDO != null)
			{
				int num2 = zDO.GetInt(DeathItemRuleHash, -1);
				if (num2 >= 0)
				{
					num = num2;
				}
			}
		}
		if (num >= 0 && num <= 3)
		{
			return num;
		}
		return 0;
	}

	internal static int MHR_DeathItemAction(int rule, bool equipped)
	{
		switch (rule)
		{
		case 1:
			return 0;
		case 2:
			if (equipped)
			{
				return 0;
			}
			goto case 3;
		default:
		{
			ZoneSystem instance = ZoneSystem.instance;
			if (!(instance == null))
			{
				if (instance.GetGlobalKey(GlobalKeys.DeathKeepInventory))
				{
					return 0;
				}
				if (equipped)
				{
					if (instance.GetGlobalKey(GlobalKeys.DeathKeepEquip))
					{
						return 0;
					}
					if (instance.GetGlobalKey(GlobalKeys.DeathDeleteItems) && !instance.GetGlobalKey(GlobalKeys.DeathDeleteUnequipped))
					{
						return 2;
					}
				}
				else if (instance.GetGlobalKey(GlobalKeys.DeathDeleteItems) || instance.GetGlobalKey(GlobalKeys.DeathDeleteUnequipped))
				{
					break;
				}
			}
			goto case 3;
		}
		case 3:
			return 1;
		}
		return 2;
	}

	private void OnDestroy()
	{
		_allCompanions.Remove(this);
		// Fase 3.5: dismissal/despawn (and leaving the world) forgets this companion's conversation
		CompanionMemory.Clear(MHR_CachedCompanionId);
		if (_dead || _removePinOnDestroy)
		{
			RemoveMinimapPin();
			if (_zdoid != ZDOID.None)
			{
				s_persistentPins.Remove(_zdoid);
			}
		}
		else if (_minimapPin != null && _zdoid != ZDOID.None)
		{
			s_persistentPins[_zdoid] = _minimapPin;
			_minimapPin = null;
		}
		UnregisterInventoryCallback();
		if (_visEquip != null)
		{
			_companionVisEquips.Remove(_visEquip);
		}
	}

	// 1.4.90: right after loading in, gear goes straight on - a player loads in already dressed (user: on loading the
	// companion visibly re-equipped everything, one piece at a time)
	private float _instantEquipUntil;

	private void QueueEquip(ItemDrop.ItemData item)
	{
		if (item == null)
		{
			return;
		}
		if (Time.time < _instantEquipUntil && _humanoid != null)
		{
			_equipping = true;
			try
			{
				_humanoid.EquipItem(item, triggerEquipEffects: false);
			}
			finally
			{
				_equipping = false;
			}
			return;
		}
		_equipQueue.Add(item);
	}

	internal void ClearEquipQueue()
	{
		if (_equipQueue.Count > 0)
		{
			CompanionsPlugin.Log.LogDebug($"[Setup] Equip queue cleared ({_equipQueue.Count} items dropped)");
			_equipQueue.Clear();
		}
		if (_equipAnimActive)
		{
			if (_zanim != null && CanWearArmor())
			{
				_zanim.SetBool("equipping", value: false);
			}
			_equipAnimActive = false;
		}
		_equipTimer = 0f;
	}

	private void UnequipMissingEquippedItems()
	{
		Inventory inventory = _humanoid.GetInventory();
		if (inventory != null)
		{
			List<ItemDrop.ItemData> allItems = inventory.GetAllItems();
			UnequipIfMissing(allItems, GetEquipSlot(_rightItemField));
			UnequipIfMissing(allItems, GetEquipSlot(_leftItemField));
			UnequipIfMissing(allItems, GetEquipSlot(_chestItemField));
			UnequipIfMissing(allItems, GetEquipSlot(_legItemField));
			UnequipIfMissing(allItems, GetEquipSlot(_helmetItemField));
			UnequipIfMissing(allItems, GetEquipSlot(_shoulderItemField));
			UnequipIfMissing(allItems, GetEquipSlot(_utilityItemField));
			UnequipIfMissing(allItems, GetEquipSlot(_trinketItemField));
		}
	}

	private void NotifyControllersInventoryChanged()
	{
		ZDO zDO = _nview?.GetZDO();
		if (zDO != null)
		{
			int num = zDO.GetInt(ActionModeHash);
			FarmController component = GetComponent<FarmController>();
			if (component != null && num == 8)
			{
				component.MHR_NotifyIfIdle();
			}
			HarvestController component2 = GetComponent<HarvestController>();
			if (component2 != null && ((num >= 1 && num <= 3) || num == 5))
			{
				component2.MHR_NotifyIfIdle();
			}
		}
	}

	private static bool IsTwoHandedWeapon(ItemDrop.ItemData item)
	{
		if (item == null || item.m_shared == null)
		{
			return false;
		}
		ItemDrop.ItemData.ItemType itemType = item.m_shared.m_itemType;
		if (itemType != ItemDrop.ItemData.ItemType.TwoHandedWeapon && itemType != ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft)
		{
			return itemType == ItemDrop.ItemData.ItemType.Bow;
		}
		return true;
	}

	private void EquipBestArmorSlot(FieldInfo slotField, ItemDrop.ItemData bestItem)
	{
		if (_humanoid == null || slotField == null || bestItem == null)
		{
			return;
		}
		ItemDrop.ItemData equipSlot = GetEquipSlot(slotField);
		if (equipSlot != bestItem)
		{
			if (equipSlot != null)
			{
				CompanionsPlugin.Log.LogDebug("[Setup] Armor swap " + slotField.Name + ": \"" + (equipSlot.m_shared?.m_name ?? "?") + "\" → \"" + (bestItem.m_shared?.m_name ?? "?") + "\"");
				_humanoid.UnequipItem(equipSlot, triggerEquipEffects: false);
			}
			else
			{
				CompanionsPlugin.Log.LogDebug("[Setup] Armor queued " + slotField.Name + ": \"" + (bestItem.m_shared?.m_name ?? "?") + "\"");
			}
			QueueEquip(bestItem);
		}
	}

	internal void SetCombatStance(int stance)
	{
		_nview?.GetZDO()?.Set(CombatStanceHash, stance);
	}

	internal void SetWander(bool v)
	{
		_nview?.GetZDO()?.Set(WanderHash, v);
	}

	internal void SetIsCommandable(bool value)
	{
		(_nview?.GetZDO())?.Set(IsCommandableHash, value ? 1 : 0);
	}

	internal void SetHomePosition(Vector3 p)
	{
		ZDO zDO = _nview?.GetZDO();
		if (zDO != null)
		{
			zDO.Set(HomePosHash, p);
			zDO.Set(HomePosSetHash, value: true);
			CompanionRest component = GetComponent<CompanionRest>();
			if (component != null)
			{
				component.TryClaimBedIfNeeded();
			}
		}
	}

	internal void SetHomeRadius(float r)
	{
		_nview?.GetZDO()?.Set(HomeRadiusHash, Mathf.Clamp(r, 5f, 200f));
	}

	private void UnequipIfMissing(List<ItemDrop.ItemData> items, ItemDrop.ItemData equipped)
	{
		if (equipped != null && !items.Contains(equipped))
		{
			MHR_Equip.SwapOrUnequip(_humanoid, items, equipped);
		}
	}
}
