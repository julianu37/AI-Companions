using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Companions;

public class CompanionTombstoneResurrect : MonoBehaviour
{
	internal static readonly int RespawnPrefabHash = "HC_Respawn_Prefab".GetStableHashCode();

	internal static readonly int RespawnNameHash = "HC_Respawn_Name".GetStableHashCode();

	internal static readonly int RespawnAppearanceHash = "HC_Respawn_Appearance".GetStableHashCode();

	internal static readonly int RespawnOwnerHash = "HC_Respawn_Owner".GetStableHashCode();

	internal static readonly int RespawnActionModeHash = "HC_Respawn_ActionMode".GetStableHashCode();

	internal static readonly int RespawnActionSchemaHash = "HC_Respawn_ActionSchema".GetStableHashCode();

	internal static readonly int RespawnFollowHash = "HC_Respawn_Follow".GetStableHashCode();

	internal static readonly int RespawnFollowPausedHash = "HC_Respawn_FollowPaused".GetStableHashCode();

	internal static readonly int FollowPausedByJobHash = "HC_FollowPausedByJob".GetStableHashCode();

	internal static readonly int RespawnStayHomeHash = "HC_Respawn_StayHome".GetStableHashCode();

	internal static readonly int RespawnAutoPickupHash = "HC_Respawn_AutoPickup".GetStableHashCode();

	internal static readonly int RespawnWanderHash = "HC_Respawn_Wander".GetStableHashCode();

	internal static readonly int RespawnCommandableHash = "HC_Respawn_Commandable".GetStableHashCode();

	internal static readonly int RespawnFormationHash = "HC_Respawn_Formation".GetStableHashCode();

	internal static readonly int RespawnSkillsHash = "HC_Respawn_Skills".GetStableHashCode();

	internal static readonly int RespawnHasHomeHash = "HC_Respawn_HasHome".GetStableHashCode();

	internal static readonly int RespawnHomePosHash = "HC_Respawn_HomePos".GetStableHashCode();

	internal static readonly int RespawnCombatStanceHash = "HC_Respawn_CombatStance".GetStableHashCode();

	internal static readonly int RespawnHasBedHash = "HC_Respawn_HasBed".GetStableHashCode();

	internal static readonly int RespawnBedPosHash = "HC_Respawn_BedPos".GetStableHashCode();

	internal static readonly int RespawnInventoryHash = "HC_Respawn_Inventory".GetStableHashCode();

	private const float ResurrectDuration = 7f;

	private const float InteractRange = 3f;

	private ZNetView _nview;

	private float _holdStartTime;

	private bool _isHolding;

	private Player _interactingPlayer;

	private float _lastInteractTime;

	private bool _autoRespawnDone;

	internal bool _cleanupOnly;

	private static readonly FieldInfo _pinsField = AccessTools.Field(typeof(Minimap), "m_pins");

	internal static float LastResurrectTime { get; private set; }

	internal bool TryStartResurrect(Player player, bool hold)
	{
		if (_nview == null || _nview.GetZDO() == null)
		{
			return false;
		}
		ZDO zDO = _nview.GetZDO();
		long num = zDO.GetLong(CompanionSetup.TombstoneIdHash, 0L);
		if (num == 0L)
		{
			return false;
		}
		string text = zDO.GetString(RespawnOwnerHash);
		if (string.IsNullOrEmpty(text))
		{
			return false;
		}
		string text2 = Player.m_localPlayer?.GetPlayerID().ToString() ?? "";
		if (text != text2)
		{
			return false;
		}
		if (!hold)
		{
			_isHolding = true;
			_holdStartTime = Time.time;
			_interactingPlayer = player;
			_lastInteractTime = Time.time;
			return true;
		}
		if (!_isHolding || _interactingPlayer != player)
		{
			return false;
		}
		_lastInteractTime = Time.time;
		float num2 = Time.time - _holdStartTime;
		if (num2 >= 7f)
		{
			DoResurrect(isAutoRespawn: false);
			CancelHold();
			LastResurrectTime = Time.time;
		}
		return true;
	}

	internal string GetResurrectHoverText()
	{
		ZDO zDO = _nview?.GetZDO();
		if (zDO == null)
		{
			return null;
		}
		string text = zDO.GetString(RespawnNameHash);
		string text2 = zDO.GetString(RespawnOwnerHash);
		if (string.IsNullOrEmpty(text2))
		{
			return null;
		}
		string text3 = Player.m_localPlayer?.GetPlayerID().ToString() ?? "";
		if (text2 != text3)
		{
			return null;
		}
		string text4 = (string.IsNullOrEmpty(text) ? ModLocalization.Loc("hc_msg_name_default") : text);
		if (_isHolding)
		{
			float num = Time.time - _holdStartTime;
			float num2 = Mathf.Clamp01(num / 7f);
			int num3 = Mathf.RoundToInt(num2 * 100f);
			return text4 + "\n[<color=yellow><b>$KEY_Use</b></color>] " + ModLocalization.LocFmt("hc_hover_resurrecting", num3);
		}
		return text4 + "\n[<color=yellow><b>$KEY_Use</b></color>] " + ModLocalization.Loc("hc_hover_resurrect");
	}

	internal static void MHR_RemoveGravePinNear(Vector3 pos)
	{
		List<Minimap.PinData> list = MHR_GetPins();
		if (list == null)
		{
			return;
		}
		string suffix = MHR_GraveSuffix();
		for (int num = list.Count - 1; num >= 0; num--)
		{
			Minimap.PinData pinData = list[num];
			if (MHR_IsGravePin(pinData, suffix) && Vector3.Distance(pinData.m_pos, pos) <= 8f)
			{
				Minimap.instance.RemovePin(pinData);
				CompanionsPlugin.Log.LogInfo($"[Minimap] Removed companion tombstone pin at {pos:F0} (tombstone emptied)");
			}
		}
	}

	internal static void WriteRespawnData(ZDO tombZdo, string prefabName, string name, string appearance, string ownerId, int actionMode, int actionModeSchema, bool follow, bool stayHome, bool autoPickup, bool wander, bool commandable, int formationSlot, string skillsSerialized, bool hasHome, Vector3 homePos, int combatStance, bool hasHomeBed, Vector3 homeBedPos, string inventorySerialized)
	{
		tombZdo.Set(RespawnPrefabHash, prefabName);
		tombZdo.Set(RespawnNameHash, name);
		tombZdo.Set(RespawnAppearanceHash, appearance);
		tombZdo.Set(RespawnOwnerHash, ownerId);
		tombZdo.Set(RespawnActionModeHash, actionMode);
		tombZdo.Set(RespawnActionSchemaHash, actionModeSchema);
		tombZdo.Set(RespawnFollowHash, follow);
		tombZdo.Set(RespawnStayHomeHash, stayHome);
		tombZdo.Set(RespawnAutoPickupHash, autoPickup);
		tombZdo.Set(RespawnWanderHash, wander);
		tombZdo.Set(RespawnCommandableHash, commandable);
		tombZdo.Set(RespawnFormationHash, formationSlot);
		tombZdo.Set(RespawnSkillsHash, skillsSerialized);
		tombZdo.Set(RespawnHasHomeHash, hasHome);
		tombZdo.Set(RespawnHomePosHash, homePos);
		tombZdo.Set(RespawnCombatStanceHash, combatStance);
		tombZdo.Set(RespawnHasBedHash, hasHomeBed);
		tombZdo.Set(RespawnBedPosHash, homeBedPos);
		tombZdo.Set(RespawnInventoryHash, inventorySerialized);
		tombZdo.Set(MHR_RespawnTracker.RespawnAtHash, MHR_RespawnTracker.ComputeRespawnAt());
	}

	private void Awake()
	{
		_nview = GetComponent<ZNetView>();
	}

	private void Update()
	{
		if (_cleanupOnly)
		{
			MHR_DoCleanup();
			return;
		}
		if (!_autoRespawnDone && ModConfig.AutoRespawnEnabled.Value && MHR_IsAutoRespawnDue())
		{
			_autoRespawnDone = true;
			DoResurrect(isAutoRespawn: true);
		}
		if (_isHolding && !(_interactingPlayer == null) && Time.time - _lastInteractTime > 0.25f)
		{
			CancelHold();
		}
	}

	private void MHR_DoCleanup()
	{
		_cleanupOnly = false;
		Container component = GetComponent<Container>();
		if (!(component == null))
		{
			Inventory inventory = component.GetInventory();
			if (inventory != null && inventory.NrOfItems() <= 0 && !(_nview == null) && _nview.GetZDO() != null && !(ZNetScene.instance == null))
			{
				_nview.ClaimOwnership();
				CompanionsPlugin.Log.LogInfo("[Respawn] Removed an empty companion tombstone (its companion already respawned)");
				ZNetScene.instance.Destroy(base.gameObject);
				return;
			}
		}
		Object.Destroy(this);
	}

	private bool MHR_IsAutoRespawnDue()
	{
		if (!(_nview == null))
		{
			return MHR_RespawnTracker.IsDueForLocalOwner(_nview.GetZDO());
		}
		return false;
	}

	private void DoResurrect(bool isAutoRespawn)
	{
		if (!(_nview == null))
		{
			MHR_ResurrectCore(_nview.GetZDO(), this, isAutoRespawn);
		}
	}

	private void CancelHold()
	{
		_isHolding = false;
		_holdStartTime = 0f;
		_interactingPlayer = null;
	}

	internal static void MHR_ResurrectCore(ZDO tombZdo, CompanionTombstoneResurrect comp, bool isAutoRespawn)
	{
		if (tombZdo == null)
		{
			return;
		}
		string text = tombZdo.GetString(RespawnPrefabHash);
		string text2 = tombZdo.GetString(RespawnNameHash);
		string text3 = tombZdo.GetString(RespawnAppearanceHash);
		string text4 = tombZdo.GetString(RespawnOwnerHash);
		if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(text4))
		{
			CompanionsPlugin.Log.LogError("[Resurrect] Missing respawn data on tombstone!");
			return;
		}
		MHR_RespawnTracker.RemovePending(tombZdo.m_uid);
		// 1.5.1: this companion was recovered from the roster after this tombstone was made - the tombstone no longer
		// brings it back (that would make two). Its items stay in it for pickup.
		string mhrId = tombZdo.GetString(CompanionSetup.MHR_TombCompanionIdHash);
		int mhrGen = tombZdo.GetInt(MHR_Roster.RespawnGenHash);
		if (!string.IsNullOrEmpty(mhrId) && text4 == (Player.m_localPlayer?.GetPlayerID().ToString() ?? "") && MHR_Roster.GenOf(mhrId) > mhrGen)
		{
			CompanionsPlugin.Log.LogWarning("[Resurrect] \"" + text2 + "\" was recovered since this tombstone was made - it won't respawn from it (items stay in it)");
			tombZdo.Set(RespawnOwnerHash, "");
			tombZdo.Set(RespawnPrefabHash, "");
			if (!(comp == null))
			{
				Object.Destroy(comp);
			}
			return;
		}
		foreach (CompanionSetup allCompanion in CompanionSetup.AllCompanions)
		{
			ZDO zDO = allCompanion.GetComponent<ZNetView>()?.GetZDO();
			if (zDO == null)
			{
				continue;
			}
			string text5 = zDO.GetString(CompanionSetup.OwnerHash);
			string text6 = zDO.GetString(CompanionSetup.NameHash);
			if (text5 == text4 && text6 == text2)
			{
				Character component = allCompanion.GetComponent<Character>();
				if (component != null && component.GetHealth() > 0f)
				{
					CompanionsPlugin.Log.LogWarning("[Resurrect] Duplicate blocked — \"" + text2 + "\" already alive");
					return;
				}
			}
		}
		GameObject gameObject = ZNetScene.instance?.GetPrefab(text);
		if (gameObject == null)
		{
			CompanionsPlugin.Log.LogError("[Resurrect] Prefab not found: " + text);
			return;
		}
		Vector3 vector;
		if (isAutoRespawn)
		{
			if (!tombZdo.GetBool(RespawnHasBedHash))
			{
				goto IL_01ab;
			}
			vector = CompanionManager.FindSpawnPosition(tombZdo.GetVec3(RespawnBedPosHash, Vector3.zero), 2f);
		}
		else
		{
			if ((object)comp == null)
			{
				goto IL_01ab;
			}
			vector = comp.transform.position + Vector3.up * 0.5f;
		}
		goto IL_0211;
		IL_0211:
		Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
		GameObject gameObject2 = Object.Instantiate(gameObject, vector, rotation);
		ZNetView component2 = gameObject2.GetComponent<ZNetView>();
		if (component2?.GetZDO() == null)
		{
			CompanionsPlugin.Log.LogError("[Resurrect] Spawn failed — no ZDO");
			Object.Destroy(gameObject2);
			return;
		}
		ZDO zDO2 = component2.GetZDO();
		zDO2.Set(CompanionSetup.AppearanceHash, text3);
		zDO2.Set(CompanionSetup.OwnerHash, text4);
		zDO2.Set(CompanionSetup.NameHash, text2);
		int value = tombZdo.GetInt(RespawnActionModeHash);
		int value2 = tombZdo.GetInt(RespawnActionSchemaHash);
		bool flag = tombZdo.GetBool(RespawnFollowHash, defaultValue: true);
		bool flag2 = tombZdo.GetBool(RespawnStayHomeHash);
		bool value3 = tombZdo.GetBool(RespawnAutoPickupHash, defaultValue: true);
		bool value4 = tombZdo.GetBool(RespawnWanderHash);
		bool flag3 = tombZdo.GetBool(RespawnCommandableHash, defaultValue: true);
		int value5 = tombZdo.GetInt(RespawnFormationHash, -1);
		string value6 = tombZdo.GetString(RespawnSkillsHash);
		bool flag4 = tombZdo.GetBool(RespawnHasHomeHash);
		Vector3 vec = tombZdo.GetVec3(RespawnHomePosHash, Vector3.zero);
		int value7 = tombZdo.GetInt(RespawnCombatStanceHash);
		zDO2.Set(CompanionSetup.FollowHash, flag);
		zDO2.Set(FollowPausedByJobHash, tombZdo.GetBool(RespawnFollowPausedHash));
		zDO2.Set(CompanionSetup.ActionModeHash, value);
		zDO2.Set(CompanionSetup.ActionModeSchemaHash, value2);
		zDO2.Set(CompanionSetup.StayHomeHash, flag2);
		zDO2.Set(CompanionSetup.AutoPickupHash, value3);
		zDO2.Set(CompanionSetup.WanderHash, value4);
		zDO2.Set(CompanionSetup.IsCommandableHash, flag3 ? 1 : 0);
		zDO2.Set(CompanionSetup.FormationSlotHash, value5);
		zDO2.Set(CompanionSetup.CombatStanceHash, value7);
		if (flag4)
		{
			zDO2.Set(CompanionSetup.HomePosHash, vec);
			zDO2.Set(CompanionSetup.HomePosSetHash, value: true);
		}
		if (!string.IsNullOrEmpty(value6))
		{
			zDO2.Set(CompanionSkills.SkillsHash, value6);
		}
		zDO2.Persistent = true;
		zDO2.Set(MHR_Roster.GenHash, mhrGen);
		string text7 = tombZdo.GetString(CompanionSetup.MHR_TombCompanionIdHash);
		if (!string.IsNullOrEmpty(text7))
		{
			zDO2.Set(CompanionSetup.MHR_CompanionIdHash, text7);
			CompanionsPlugin.Log.LogInfo($"[Resurrect] Companion keeps its permanent ID {text7}");
		}
		zDO2.Set(CompanionSetup.HasHomeBedHash, tombZdo.GetBool(RespawnHasBedHash));
		zDO2.Set(CompanionSetup.HomeBedPosHash, tombZdo.GetVec3(RespawnBedPosHash, Vector3.zero));
		string text8 = tombZdo.GetString(RespawnInventoryHash);
		if (!string.IsNullOrEmpty(text8))
		{
			Humanoid component3 = gameObject2.GetComponent<Humanoid>();
			if (component3 != null)
			{
				Inventory inventory = component3.GetInventory();
				if (inventory != null)
				{
					ZPackage pkg = new ZPackage(text8);
					inventory.Load(pkg);
					CompanionsPlugin.Log.LogInfo("[Resurrect] Restored kept inventory");
				}
			}
		}
		bool flag5 = false;
		Container container = comp?.GetComponent<Container>();
		Humanoid component4 = gameObject2.GetComponent<Humanoid>();
		if (isAutoRespawn)
		{
			if (container != null)
			{
				Inventory inventory2 = container.GetInventory();
				if (inventory2 != null)
				{
					flag5 = inventory2.NrOfItems() > 0;
					if (flag5)
					{
						zDO2.Set(CompanionSetup.TombstoneIdHash, tombZdo.GetLong(CompanionSetup.TombstoneIdHash, 0L));
						CompanionsPlugin.Log.LogInfo($"[Resurrect] Auto-respawn: left {inventory2.NrOfItems()} item(s) in the tombstone for the companion to recover");
					}
				}
			}
			else
			{
				flag5 = true;
				zDO2.Set(CompanionSetup.TombstoneIdHash, tombZdo.GetLong(CompanionSetup.TombstoneIdHash, 0L));
				CompanionsPlugin.Log.LogInfo("[Resurrect] Far-away respawn: tombstone isn't loaded, any dropped items stay in it for the companion to recover");
			}
		}
		else if (container != null && component4 != null)
		{
			Inventory inventory2 = container.GetInventory();
			Inventory inventory3 = component4.GetInventory();
			if (inventory2 != null && inventory3 != null)
			{
				List<ItemDrop.ItemData> list = new List<ItemDrop.ItemData>(inventory2.GetAllItems());
				int num = 0;
				foreach (ItemDrop.ItemData item in list)
				{
					if (inventory3.AddItem(item))
					{
						inventory2.RemoveItem(item);
						num++;
					}
				}
				flag5 = inventory2.NrOfItems() > 0;
				if (flag5)
				{
					zDO2.Set(CompanionSetup.TombstoneIdHash, tombZdo.GetLong(CompanionSetup.TombstoneIdHash, 0L));
				}
				int width = inventory3.GetWidth();
				int num2 = 0;
				foreach (ItemDrop.ItemData allItem in inventory3.GetAllItems())
				{
					allItem.m_gridPos = new Vector2i(num2 % width, num2 / width);
					num2++;
				}
				if (num > 0)
				{
					inventory3.m_onChanged?.Invoke();
				}
				CompanionsPlugin.Log.LogInfo($"[Resurrect] Transferred {num} items from tombstone to companion" + (flag5 ? $", {inventory2.NrOfItems()} items left in tombstone" : ""));
			}
		}
		CompanionAppearance a = CompanionAppearance.Deserialize(text3);
		CompanionSetup component5 = gameObject2.GetComponent<CompanionSetup>();
		if (component5 != null)
		{
			component5.ApplyAppearance(a);
		}
		Character component6 = gameObject2.GetComponent<Character>();
		if (component6 != null && !string.IsNullOrEmpty(text2))
		{
			component6.m_name = text2;
		}
		CompanionAI component7 = gameObject2.GetComponent<CompanionAI>();
		if (flag4 && component7 != null)
		{
			component7.SetPatrolPointAt(vec);
		}
		if (flag2 && flag4 && component7 != null)
		{
			component7.SetFollowTarget(null);
			component7.SetPatrolPointAt(vec);
		}
		else if (flag && MHR_Owner.Of(component7) != null)
		{
			component7?.SetFollowTarget(MHR_Owner.Go(component7));
		}
		// 1.5.1: static - the auto-respawn for a far-away tombstone passes comp == null, and the instance call threw here,
		// before the tombstone's respawn data was cleared, so the next tick (1 s later) respawned the companion again
		PlayResurrectEffects(vector);
		if (!flag5 || tombZdo.GetInt(CompanionSetup.MHR_TombItemsHash, -1) == 0)
		{
			MHR_RemoveGravePinNear(tombZdo.GetPosition());
		}
		MessageHud.instance?.ShowMessage(MessageHud.MessageType.Center, ModLocalization.LocFmt("hc_msg_resurrected", text2));
		CompanionsPlugin.Log.LogInfo($"[Resurrect] \"{text2}\" resurrected at {vector:F1}");
		tombZdo.SetOwner(ZDOMan.GetSessionID());
		if (flag5)
		{
			tombZdo.Set(RespawnOwnerHash, "");
			tombZdo.Set(RespawnPrefabHash, "");
			if (!(comp == null))
			{
				Object.Destroy(comp);
			}
			else
			{
				tombZdo.Set(MHR_RespawnTracker.CleanupHash, value: true);
			}
			CompanionsPlugin.Log.LogInfo("[Resurrect] Tombstone kept — items remain for player pickup");
		}
		else if ((object)comp != null)
		{
			if (comp._nview != null && comp._nview.IsOwner())
			{
				ZNetScene.instance?.Destroy(comp.gameObject);
			}
			else
			{
				Object.Destroy(comp.gameObject);
			}
		}
		else
		{
			// 1.5.1: respawned from far away with an empty tombstone - clear its respawn data and mark it for clean-up,
			// or loading it later would bring the companion back a second time
			tombZdo.Set(RespawnOwnerHash, "");
			tombZdo.Set(RespawnPrefabHash, "");
			tombZdo.Set(MHR_RespawnTracker.CleanupHash, value: true);
		}
		return;
		IL_01ab:
		vector = ((ModConfig.AutoRespawnAtWorldSpawnWhenNoBed.Value || !(Player.m_localPlayer != null)) ? CompanionManager.GetWorldSpawnPoint() : CompanionManager.FindSpawnPosition(Player.m_localPlayer.transform.position, 4f));
		goto IL_0211;
	}

	internal static void PlayResurrectEffects(Vector3 pos)
	{
		GameObject gameObject = ZNetScene.instance?.GetPrefab("fx_eikthyr_forwardshockwave");
		if (gameObject != null)
		{
			Object.Instantiate(gameObject, pos, Quaternion.identity);
		}
		GameObject gameObject2 = ZNetScene.instance?.GetPrefab("vfx_offering");
		if (gameObject2 != null)
		{
			Object.Instantiate(gameObject2, pos + Vector3.up, Quaternion.identity);
		}
		GameObject gameObject3 = ZNetScene.instance?.GetPrefab("sfx_lore_read");
		if (gameObject3 != null)
		{
			Object.Instantiate(gameObject3, pos, Quaternion.identity);
		}
		GameObject gameObject4 = ZNetScene.instance?.GetPrefab("sfx_GP_activate");
		if (gameObject4 != null)
		{
			Object.Instantiate(gameObject4, pos, Quaternion.identity);
		}
	}

	internal static List<Minimap.PinData> MHR_GetPins()
	{
		if (!(Minimap.instance == null) && (object)_pinsField != null)
		{
			return _pinsField.GetValue(Minimap.instance) as List<Minimap.PinData>;
		}
		return null;
	}

	internal static string MHR_GraveSuffix()
	{
		return " " + ModLocalization.Loc("hc_minimap_grave");
	}

	internal static bool MHR_IsGravePin(Minimap.PinData pin, string suffix)
	{
		if (pin != null && pin.m_type == Minimap.PinType.Death && pin.m_name != null)
		{
			return pin.m_name.EndsWith(suffix);
		}
		return false;
	}

	internal static bool MHR_HasFullGraveNear(Vector3 pos)
	{
		TombStone[] array = Object.FindObjectsByType<TombStone>(FindObjectsSortMode.None);
		foreach (TombStone tombStone in array)
		{
			if (tombStone == null || !(Vector3.Distance(tombStone.transform.position, pos) <= 8f))
			{
				continue;
			}
			ZNetView component = tombStone.GetComponent<ZNetView>();
			if (component == null)
			{
				continue;
			}
			ZDO zDO = component.GetZDO();
			if (zDO == null || zDO.GetLong(CompanionSetup.TombstoneIdHash, 0L) == 0)
			{
				continue;
			}
			if (!string.IsNullOrEmpty(zDO.GetString(RespawnPrefabHash)))
			{
				return true;
			}
			Container component2 = tombStone.GetComponent<Container>();
			if (!(component2 == null))
			{
				Inventory inventory = component2.GetInventory();
				if (inventory != null && inventory.NrOfItems() > 0)
				{
					return true;
				}
			}
		}
		return false;
	}

	internal static void MHR_SweepGravePins()
	{
		if (Player.m_localPlayer == null || ZNetScene.instance == null)
		{
			return;
		}
		List<Minimap.PinData> list = MHR_GetPins();
		if (list == null)
		{
			return;
		}
		string suffix = MHR_GraveSuffix();
		Vector3 position = Player.m_localPlayer.transform.position;
		for (int num = list.Count - 1; num >= 0; num--)
		{
			Minimap.PinData pinData = list[num];
			if (MHR_IsGravePin(pinData, suffix) && Vector3.Distance(pinData.m_pos, position) <= 30f && ZNetScene.instance.IsAreaReady(pinData.m_pos) && !MHR_HasFullGraveNear(pinData.m_pos))
			{
				Minimap.instance.RemovePin(pinData);
				CompanionsPlugin.Log.LogInfo($"[Minimap] Removed companion tombstone pin \"{pinData.m_name}\" (tombstone is gone or empty)");
			}
		}
	}

	private static void RemoveDeathPin(string companionName)
	{
		if (Minimap.instance == null)
		{
			return;
		}
		string text = companionName + " " + ModLocalization.Loc("hc_minimap_grave");
		if (!(_pinsField?.GetValue(Minimap.instance) is List<Minimap.PinData> list))
		{
			return;
		}
		for (int num = list.Count - 1; num >= 0; num--)
		{
			Minimap.PinData pinData = list[num];
			if (pinData.m_type == Minimap.PinType.Death && pinData.m_name == text)
			{
				Minimap.instance.RemovePin(pinData);
				break;
			}
		}
	}

	private void OnDestroy()
	{
		if (_isHolding)
		{
			CancelHold();
		}
	}
}
