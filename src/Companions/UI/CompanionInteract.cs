using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace Companions;

public class CompanionInteract : MonoBehaviour
{
	private class DistComparer : IComparer<RaycastHit>
	{
		public int Compare(RaycastHit a, RaycastHit b)
		{
			return a.distance.CompareTo(b.distance);
		}
	}

	[HarmonyPatch(typeof(Container), "Interact")]
	private static class ContainerInteract_Patch
	{
		private static bool Prefix(Container __instance, Humanoid character, bool hold, ref bool __result)
		{
			CompanionSetup component = __instance.GetComponent<CompanionSetup>();
			if (component == null)
			{
				return true;
			}
			if (Time.time - CompanionTombstoneResurrect.LastResurrectTime < 1f)
			{
				__result = false;
				return false;
			}
			ZNetView component2 = __instance.GetComponent<ZNetView>();
			if (component2?.GetZDO() == null)
			{
				return true;
			}
			Player player = character as Player;
			if (player == null)
			{
				__result = false;
				return false;
			}
			string text = component2.GetZDO().GetString(CompanionSetup.OwnerHash);
			string text2 = player.GetPlayerID().ToString();
			if (text.Length > 0 && text != text2)
			{
				player.Message(MessageHud.MessageType.Center, ModLocalization.Loc("hc_msg_not_yours"));
				__result = false;
				return false;
			}
			if (ZInput.IsGamepadActive())
			{
				__result = false;
				return false;
			}
			if (!IsRadialKeyUse)
			{
				if (hold)
				{
					__result = false;
					return false;
				}
				CompanionsPlugin.Log.LogDebug("[Interact] Prefix — separate key mode, opening inventory");
				OpenCompanionInventory(component, __instance, player);
				__result = true;
				return false;
			}
			if (hold)
			{
				CompanionsPlugin.Log.LogInfo("[Interact] Prefix — hold=true arrived, opening radial");
				_pendingTapContainer = null;
				_pendingTapPlayer = null;
				_loggedPendingStart = false;
				_gamepadReleaseDetected = false;
				CompanionRadialMenu.EnsureInstance();
				if (CompanionRadialMenu.Instance != null && !CompanionRadialMenu.Instance.IsVisible)
				{
					CompanionRadialMenu.Instance.Show(component);
				}
				__result = true;
				return false;
			}
			if (_pendingTapContainer == null)
			{
				_pendingTapContainer = __instance;
				_pendingTapPlayer = player;
				_pendingTapTime = Time.time;
				_loggedPendingStart = false;
				_pendingIsGamepad = false;
				_gamepadReleaseDetected = false;
				CompanionsPlugin.Log.LogInfo("[Interact] Prefix — tap deferred, " + $"InputKey={Input.GetKey(ModConfig.RadialMenuKey.Value)} " + $"time={Time.time:F3}");
			}
			else
			{
				CompanionsPlugin.Log.LogDebug("[Interact] Prefix — already pending, keeping existing timer " + $"(elapsed={Time.time - _pendingTapTime:F3}s)");
			}
			__result = false;
			return false;
		}
	}

	[HarmonyPatch(typeof(Character), "GetHoverText")]
	private static class CharacterGetHoverText_Patch
	{
		private static bool Prefix(Character __instance, ref string __result)
		{
			CompanionInteract component = __instance.GetComponent<CompanionInteract>();
			if (component == null)
			{
				return true;
			}
			__result = component.GetHoverText();
			return false;
		}
	}

	[HarmonyPatch(typeof(Character), "GetHoverName")]
	private static class CharacterGetHoverName_Patch
	{
		private static bool Prefix(Character __instance, ref string __result)
		{
			CompanionInteract component = __instance.GetComponent<CompanionInteract>();
			if (component == null)
			{
				return true;
			}
			__result = component.GetHoverName();
			return false;
		}
	}

	private ZNetView _nview;

	private Character _character;

	private static Container _pendingTapContainer;

	private static Player _pendingTapPlayer;

	private static float _pendingTapTime;

	private static bool _pendingIsGamepad;

	private static bool _gamepadReleaseDetected;

	private static bool _gpHoldActive;

	private static float _gpHoldStart;

	private static CompanionSetup _gpHoldTarget;

	private const float HoldThreshold = 0.2f;

	private static readonly FieldInfo _playerHoveringField = AccessTools.Field(typeof(Player), "m_hovering");

	private static int _directCommandFrame = -1;

	private static float _cmdCooldown;

	private static bool _holdActive;

	private static float _holdTimer;

	private static bool _holdFired;

	private static bool _mhrSuperFired;

	private static bool _mhrSuperRecall;

	private const float CmdHoldThreshold = 0.4f;

	private static readonly RaycastHit[] _cmdHitBuffer = new RaycastHit[32];

	private static readonly DistComparer _distCompare = new DistComparer();

	private static bool _loggedPendingStart;

	private static string[] _comeHereLines;

	private static string[] _attackLines;

	private static string[] _cartPullLines;

	private static string[] _cartReleasedLines;

	private static string[] _doorLines;

	private static string[] _sitLines;

	private static string[] _sleepLines;

	private static string[] _wakeLines;

	private static string[] _notMyBedLines;

	private static string[] _depositLines;

	private static string[] _depositEmptyLines;

	private static string[] _harvestLines;

	private static string[] _cancelLines;

	private static string[] _moveLines;

	private static string[] _repairLines;

	private static string[] _repairNothingLines;

	private static string[] _boardLines;

	private static string[] _tombstoneLines;

	private static string[] _smeltLines;

	private static bool IsRadialKeyUse => ModConfig.RadialMenuKey.Value == KeyCode.E;

	private static string[] ComeHereLines
	{
		get
		{
			object obj = _comeHereLines;
			if (obj == null)
			{
				obj = new string[3]
				{
					ModLocalization.Loc("hc_cmd_comehere_1"),
					ModLocalization.Loc("hc_cmd_comehere_2"),
					ModLocalization.Loc("hc_cmd_comehere_3")
				};
				_comeHereLines = (string[])obj;
			}
			return (string[])obj;
		}
	}

	private static string[] AttackLines
	{
		get
		{
			object obj = _attackLines;
			if (obj == null)
			{
				obj = new string[4]
				{
					ModLocalization.Loc("hc_cmd_attack_1"),
					ModLocalization.Loc("hc_cmd_attack_2"),
					ModLocalization.Loc("hc_cmd_attack_3"),
					ModLocalization.Loc("hc_cmd_attack_4")
				};
				_attackLines = (string[])obj;
			}
			return (string[])obj;
		}
	}

	private static string[] CartPullLines
	{
		get
		{
			object obj = _cartPullLines;
			if (obj == null)
			{
				obj = new string[3]
				{
					ModLocalization.Loc("hc_cmd_cart_pull_1"),
					ModLocalization.Loc("hc_cmd_cart_pull_2"),
					ModLocalization.Loc("hc_cmd_cart_pull_3")
				};
				_cartPullLines = (string[])obj;
			}
			return (string[])obj;
		}
	}

	private static string[] CartReleasedLines
	{
		get
		{
			object obj = _cartReleasedLines;
			if (obj == null)
			{
				obj = new string[3]
				{
					ModLocalization.Loc("hc_cmd_cart_release_1"),
					ModLocalization.Loc("hc_cmd_cart_release_2"),
					ModLocalization.Loc("hc_cmd_cart_release_3")
				};
				_cartReleasedLines = (string[])obj;
			}
			return (string[])obj;
		}
	}

	private static string[] DoorLines
	{
		get
		{
			object obj = _doorLines;
			if (obj == null)
			{
				obj = new string[3]
				{
					ModLocalization.Loc("hc_cmd_door_1"),
					ModLocalization.Loc("hc_cmd_door_2"),
					ModLocalization.Loc("hc_cmd_door_3")
				};
				_doorLines = (string[])obj;
			}
			return (string[])obj;
		}
	}

	private static string[] SitLines
	{
		get
		{
			object obj = _sitLines;
			if (obj == null)
			{
				obj = new string[3]
				{
					ModLocalization.Loc("hc_cmd_sit_1"),
					ModLocalization.Loc("hc_cmd_sit_2"),
					ModLocalization.Loc("hc_cmd_sit_3")
				};
				_sitLines = (string[])obj;
			}
			return (string[])obj;
		}
	}

	private static string[] SleepLines
	{
		get
		{
			object obj = _sleepLines;
			if (obj == null)
			{
				obj = new string[3]
				{
					ModLocalization.Loc("hc_cmd_sleep_1"),
					ModLocalization.Loc("hc_cmd_sleep_2"),
					ModLocalization.Loc("hc_cmd_sleep_3")
				};
				_sleepLines = (string[])obj;
			}
			return (string[])obj;
		}
	}

	private static string[] WakeLines
	{
		get
		{
			object obj = _wakeLines;
			if (obj == null)
			{
				obj = new string[3]
				{
					ModLocalization.Loc("hc_cmd_wake_1"),
					ModLocalization.Loc("hc_cmd_wake_2"),
					ModLocalization.Loc("hc_cmd_wake_3")
				};
				_wakeLines = (string[])obj;
			}
			return (string[])obj;
		}
	}

	private static string[] DepositLines
	{
		get
		{
			object obj = _depositLines;
			if (obj == null)
			{
				obj = new string[3]
				{
					ModLocalization.Loc("hc_cmd_deposit_1"),
					ModLocalization.Loc("hc_cmd_deposit_2"),
					ModLocalization.Loc("hc_cmd_deposit_3")
				};
				_depositLines = (string[])obj;
			}
			return (string[])obj;
		}
	}

	private static string[] DepositEmptyLines
	{
		get
		{
			object obj = _depositEmptyLines;
			if (obj == null)
			{
				obj = new string[2]
				{
					ModLocalization.Loc("hc_cmd_deposit_empty_1"),
					ModLocalization.Loc("hc_cmd_deposit_empty_2")
				};
				_depositEmptyLines = (string[])obj;
			}
			return (string[])obj;
		}
	}

	private static string[] HarvestLines
	{
		get
		{
			object obj = _harvestLines;
			if (obj == null)
			{
				obj = new string[3]
				{
					ModLocalization.Loc("hc_cmd_harvest_1"),
					ModLocalization.Loc("hc_cmd_harvest_2"),
					ModLocalization.Loc("hc_cmd_harvest_3")
				};
				_harvestLines = (string[])obj;
			}
			return (string[])obj;
		}
	}

	private static string[] CancelLines
	{
		get
		{
			object obj = _cancelLines;
			if (obj == null)
			{
				obj = new string[3]
				{
					ModLocalization.Loc("hc_cmd_cancel_1"),
					ModLocalization.Loc("hc_cmd_cancel_2"),
					ModLocalization.Loc("hc_cmd_cancel_3")
				};
				_cancelLines = (string[])obj;
			}
			return (string[])obj;
		}
	}

	private static string[] MoveLines
	{
		get
		{
			object obj = _moveLines;
			if (obj == null)
			{
				obj = new string[3]
				{
					ModLocalization.Loc("hc_cmd_move_1"),
					ModLocalization.Loc("hc_cmd_move_2"),
					ModLocalization.Loc("hc_cmd_move_3")
				};
				_moveLines = (string[])obj;
			}
			return (string[])obj;
		}
	}

	private static string[] RepairLines
	{
		get
		{
			object obj = _repairLines;
			if (obj == null)
			{
				obj = new string[3]
				{
					ModLocalization.Loc("hc_cmd_repair_1"),
					ModLocalization.Loc("hc_cmd_repair_2"),
					ModLocalization.Loc("hc_cmd_repair_3")
				};
				_repairLines = (string[])obj;
			}
			return (string[])obj;
		}
	}

	private static string[] RepairNothingLines
	{
		get
		{
			object obj = _repairNothingLines;
			if (obj == null)
			{
				obj = new string[3]
				{
					ModLocalization.Loc("hc_cmd_repair_nothing_1"),
					ModLocalization.Loc("hc_cmd_repair_nothing_2"),
					ModLocalization.Loc("hc_cmd_repair_nothing_3")
				};
				_repairNothingLines = (string[])obj;
			}
			return (string[])obj;
		}
	}

	private static string[] BoardLines
	{
		get
		{
			object obj = _boardLines;
			if (obj == null)
			{
				obj = new string[3]
				{
					ModLocalization.Loc("hc_cmd_board_1"),
					ModLocalization.Loc("hc_cmd_board_2"),
					ModLocalization.Loc("hc_cmd_board_3")
				};
				_boardLines = (string[])obj;
			}
			return (string[])obj;
		}
	}

	private static string[] TombstoneLines
	{
		get
		{
			object obj = _tombstoneLines;
			if (obj == null)
			{
				obj = new string[3]
				{
					ModLocalization.Loc("hc_cmd_tombstone_1"),
					ModLocalization.Loc("hc_cmd_tombstone_2"),
					ModLocalization.Loc("hc_cmd_tombstone_3")
				};
				_tombstoneLines = (string[])obj;
			}
			return (string[])obj;
		}
	}

	private static string[] SmeltLines
	{
		get
		{
			object obj = _smeltLines;
			if (obj == null)
			{
				obj = new string[3]
				{
					ModLocalization.Loc("hc_cmd_smelt_1"),
					ModLocalization.Loc("hc_cmd_smelt_2"),
					ModLocalization.Loc("hc_cmd_smelt_3")
				};
				_smeltLines = (string[])obj;
			}
			return (string[])obj;
		}
	}

	internal static bool ShouldKeep(ItemDrop.ItemData item, Humanoid humanoid)
	{
		if (item == null || item.m_shared == null)
		{
			return true;
		}
		if (humanoid.IsItemEquiped(item))
		{
			return true;
		}
		ItemDrop.ItemData.ItemType itemType = item.m_shared.m_itemType;
		if (itemType == ItemDrop.ItemData.ItemType.Consumable && (item.m_shared.m_food > 0f || item.m_shared.m_foodStamina > 0f || item.m_shared.m_foodEitr > 0f))
		{
			return true;
		}
		switch (itemType)
		{
		case ItemDrop.ItemData.ItemType.OneHandedWeapon:
		case ItemDrop.ItemData.ItemType.Bow:
		case ItemDrop.ItemData.ItemType.TwoHandedWeapon:
		case ItemDrop.ItemData.ItemType.Torch:
		case ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft:
			return true;
		case ItemDrop.ItemData.ItemType.Shield:
		case ItemDrop.ItemData.ItemType.Helmet:
		case ItemDrop.ItemData.ItemType.Chest:
		case ItemDrop.ItemData.ItemType.Legs:
		case ItemDrop.ItemData.ItemType.Hands:
		case ItemDrop.ItemData.ItemType.Shoulder:
		case ItemDrop.ItemData.ItemType.Utility:
			return true;
		default:
			return false;
		}
	}

	private void Awake()
	{
		_nview = GetComponent<ZNetView>();
		_character = GetComponent<Character>();
	}

	/// <summary>Fase 3.24: when the "in use" flag of this companion's pack was first seen stale (-1 = not stale).</summary>
	private float _containerStaleSince = -1f;

	/// <summary>Fase 3.24: when this companion's next stale-flag check is due (once a second).</summary>
	private float _nextContainerUseCheck;

	private void Update()
	{
		// Fase 3.8: opens the container an AI order is waiting for (once per frame, first instance wins)
		TickPendingInventoryOpen();
		// Fase 3.24 (auditoria): releases an "in use" flag left by a client that went away. Once a second per
		// companion, never per frame.
		float useNow = Time.time;
		if (useNow >= _nextContainerUseCheck)
		{
			_nextContainerUseCheck = useNow + 1f;
			CompanionContainerUse.CheckStale(GetComponent<Container>(), ref _containerStaleSince);
		}
		HandleDirectCommand();
		if (ZInput.IsGamepadActive())
		{
			HandleGamepadHold();
		}
		if (!IsRadialKeyUse)
		{
			HandleSeparateKeyRadial();
			if (!_gpHoldActive)
			{
				ProcessPendingTap();
			}
		}
		else if (!_gpHoldActive)
		{
			ProcessPendingTap();
		}
	}

	private static void HandleDirectCommand()
	{
		if (Time.frameCount == _directCommandFrame)
		{
			return;
		}
		_directCommandFrame = Time.frameCount;
		Player localPlayer = Player.m_localPlayer;
		if (localPlayer == null)
		{
			_holdActive = false;
			return;
		}
		if (InventoryGui.IsVisible() || Minimap.IsOpen() || Menu.IsVisible())
		{
			_holdActive = false;
			return;
		}
		if (TextInput.IsVisible() || Console.IsVisible() || StoreGui.IsVisible())
		{
			_holdActive = false;
			return;
		}
		if (Chat.instance != null && Chat.instance.HasFocus())
		{
			_holdActive = false;
			return;
		}
		if (Hud.IsPieceSelectionVisible())
		{
			_holdActive = false;
			return;
		}
		if (CompanionRadialMenu.Instance != null && CompanionRadialMenu.Instance.IsVisible)
		{
			_holdActive = false;
			return;
		}
		// 1.4.81: the farm zone key is the same key (Z by default), pressed with a modifier. Holding modifier + Z to set a
		// zone counted as holding the command key, which recalled the companions and switched their job off.
		if (!ZInput.IsGamepadActive() && ((FarmZonePlacer.Instance != null && FarmZonePlacer.Instance.IsVisible) || Input.GetKey(ModConfig.FarmZoneModifier.Value)))
		{
			_holdActive = false;
			_holdTimer = 0f;
			return;
		}
		float deltaTime = Time.deltaTime;
		_cmdCooldown -= deltaTime;
		bool flag = ZInput.IsGamepadActive();
		bool flag2 = (flag ? ZInput.GetButtonDown("JoyUse") : Input.GetKeyDown(ModConfig.DirectTargetKey.Value));
		bool flag3 = (flag ? ZInput.GetButton("JoyUse") : Input.GetKey(ModConfig.DirectTargetKey.Value));
		if (flag && (flag2 || flag3))
		{
			GameObject hoverObject = localPlayer.GetHoverObject();
			if (hoverObject != null || Hud.InRadial())
			{
				_holdActive = false;
				_holdTimer = 0f;
				return;
			}
		}
		if (flag2 && _cmdCooldown <= 0f)
		{
			_holdActive = true;
			_holdTimer = 0f;
			_holdFired = false;
			_mhrSuperFired = false;
		}
		if (_holdActive && flag3)
		{
			_holdTimer += deltaTime;
			if (_holdTimer >= 0.4f)
			{
				if (!_holdFired)
				{
					_holdFired = true;
					_cmdCooldown = 0.5f;
					FireComeToMe(localPlayer);
				}
				else if (!_mhrSuperFired && _holdTimer >= ModConfig.SuperRecallHoldTime.Value)
				{
					_mhrSuperFired = true;
					_cmdCooldown = 0.5f;
					MHR_FireSuperRecall(localPlayer);
				}
			}
		}
		else
		{
			if (!_holdActive)
			{
				return;
			}
			_holdActive = false;
			if (_holdFired || _cmdCooldown > 0f)
			{
				return;
			}
			_cmdCooldown = 0.5f;
			Camera mainCamera = Utils.GetMainCamera();
			if (mainCamera == null)
			{
				return;
			}
			Ray ray = new Ray(mainCamera.transform.position, mainCamera.transform.forward);
			int mask = LayerMask.GetMask("character", "character_net", "character_ghost", "character_noenv", "Default", "Default_small", "piece", "piece_nonsolid", "static_solid", "terrain", "vehicle", "item");
			int num = Physics.RaycastNonAlloc(ray, _cmdHitBuffer, 50f, mask);
			if (num > 1)
			{
				Array.Sort(_cmdHitBuffer, 0, num, _distCompare);
			}
			IReadOnlyList<CompanionSetup> allCompanions = CompanionSetup.AllCompanions;
			string localId = localPlayer.GetPlayerID().ToString();
			for (int i = 0; i < num; i++)
			{
				Collider collider = _cmdHitBuffer[i].collider;
				if (collider == null || collider.GetComponentInParent<Player>() != null || collider.GetComponentInParent<CompanionSetup>() != null)
				{
					continue;
				}
				Character componentInParent = collider.GetComponentInParent<Character>();
				if (componentInParent != null)
				{
					if (componentInParent.GetHealth() > 0f && BaseAI.IsEnemy(localPlayer, componentInParent))
					{
						DirectAttack(allCompanions, localId, componentInParent);
						return;
					}
					continue;
				}
				Vagon componentInParent2 = collider.GetComponentInParent<Vagon>();
				if (componentInParent2 != null)
				{
					DirectCart(allCompanions, localId, componentInParent2);
					return;
				}
				Door componentInParent3 = collider.GetComponentInParent<Door>();
				if (componentInParent3 != null)
				{
					DirectDoor(allCompanions, localId, componentInParent3);
					return;
				}
				Fireplace componentInParent4 = collider.GetComponentInParent<Fireplace>();
				if (componentInParent4 != null)
				{
					DirectSit(allCompanions, localId, componentInParent4);
					return;
				}
				Bed componentInParent5 = collider.GetComponentInParent<Bed>();
				if (componentInParent5 != null)
				{
					DirectSleep(allCompanions, localId, componentInParent5);
					return;
				}
				CraftingStation componentInParent6 = collider.GetComponentInParent<CraftingStation>();
				if (componentInParent6 != null)
				{
					DirectRepair(allCompanions, localId, componentInParent6);
					return;
				}
				Smelter componentInParent7 = collider.GetComponentInParent<Smelter>();
				if (componentInParent7 != null)
				{
					DirectSmelt(allCompanions, localId);
					return;
				}
				Ship componentInParent8 = collider.GetComponentInParent<Ship>();
				if (componentInParent8 != null)
				{
					DirectBoard(allCompanions, localId, componentInParent8);
					return;
				}
				TombStone componentInParent9 = collider.GetComponentInParent<TombStone>();
				if (componentInParent9 != null)
				{
					DirectTombstoneRecovery(allCompanions, localId, componentInParent9);
					return;
				}
				Container componentInParent10 = collider.GetComponentInParent<Container>();
				if (componentInParent10 != null && componentInParent10.GetComponent<CompanionSetup>() == null)
				{
					DirectDeposit(allCompanions, localId, componentInParent10);
					return;
				}
				GameObject harvestable = GetHarvestable(collider);
				if (harvestable != null)
				{
					if (harvestable.GetComponent<MineRock5>() == null)
					{
						for (int j = i + 1; j < num; j++)
						{
							Collider collider2 = _cmdHitBuffer[j].collider;
							if (!(collider2 == null))
							{
								MineRock5 componentInParent11 = collider2.GetComponentInParent<MineRock5>();
								if (componentInParent11 != null)
								{
									harvestable = componentInParent11.gameObject;
									break;
								}
							}
						}
					}
					DirectGatherMode(allCompanions, localId, harvestable);
					return;
				}
				int layer = collider.gameObject.layer;
				if (layer == LayerMask.NameToLayer("terrain") || layer == LayerMask.NameToLayer("Default") || layer == LayerMask.NameToLayer("static_solid") || layer == LayerMask.NameToLayer("piece") || layer == LayerMask.NameToLayer("piece_nonsolid"))
				{
					DirectGround(allCompanions, localId, _cmdHitBuffer[i].point);
					return;
				}
			}
			CancelAll(allCompanions, localId);
		}
	}

	private void HandleGamepadHold()
	{
		Player localPlayer = Player.m_localPlayer;
		if (localPlayer == null)
		{
			ResetGamepadHold();
			return;
		}
		GameObject gameObject = _playerHoveringField?.GetValue(localPlayer) as GameObject;
		CompanionSetup companionSetup = ((gameObject != null) ? gameObject.GetComponentInParent<CompanionSetup>() : null);
		CompanionSetup component = GetComponent<CompanionSetup>();
		if (companionSetup == null || companionSetup != component)
		{
			if (_gpHoldActive && _gpHoldTarget == component)
			{
				ResetGamepadHold();
			}
		}
		else if (CompanionRadialMenu.Instance != null && CompanionRadialMenu.Instance.IsVisible)
		{
			ResetGamepadHold();
		}
		else if (ZInput.GetButton("JoyUse"))
		{
			if (!_gpHoldActive)
			{
				_gpHoldActive = true;
				_gpHoldStart = Time.time;
				_gpHoldTarget = companionSetup;
				CompanionsPlugin.Log.LogDebug("[Interact] Gamepad independent hold START on \"" + companionSetup.name + "\"");
			}
			else if (_gpHoldTarget == companionSetup && Time.time - _gpHoldStart >= 0.2f)
			{
				CompanionsPlugin.Log.LogInfo("[Interact] Gamepad independent hold → RADIAL " + $"(held {Time.time - _gpHoldStart:F3}s)");
				ClearPendingTap();
				ResetGamepadHold();
				CompanionRadialMenu.EnsureInstance();
				if (CompanionRadialMenu.Instance != null && !CompanionRadialMenu.Instance.IsVisible)
				{
					CompanionRadialMenu.Instance.Show(companionSetup);
				}
			}
		}
		else
		{
			if (!_gpHoldActive || !(_gpHoldTarget == companionSetup))
			{
				return;
			}
			float num = Time.time - _gpHoldStart;
			ClearPendingTap();
			ResetGamepadHold();
			if (num >= 0.2f)
			{
				CompanionsPlugin.Log.LogInfo("[Interact] Gamepad hold → RADIAL on release " + $"(held {num:F3}s, threshold={0.2f}s)");
				CompanionRadialMenu.EnsureInstance();
				if (CompanionRadialMenu.Instance != null && !CompanionRadialMenu.Instance.IsVisible)
				{
					CompanionRadialMenu.Instance.Show(companionSetup);
				}
			}
			else
			{
				CompanionsPlugin.Log.LogInfo("[Interact] Gamepad hold → TAP " + $"(released after {num:F3}s), opening inventory");
				Container component2 = companionSetup.GetComponent<Container>();
				if (component2 != null)
				{
					OpenCompanionInventory(companionSetup, component2, localPlayer);
				}
			}
		}
	}

	private void HandleSeparateKeyRadial()
	{
		if (!Input.GetKeyDown(ModConfig.RadialMenuKey.Value))
		{
			return;
		}
		Player localPlayer = Player.m_localPlayer;
		if (localPlayer == null)
		{
			return;
		}
		GameObject gameObject = _playerHoveringField?.GetValue(localPlayer) as GameObject;
		if (gameObject == null)
		{
			return;
		}
		CompanionSetup componentInParent = gameObject.GetComponentInParent<CompanionSetup>();
		if (componentInParent == null)
		{
			return;
		}
		ZNetView component = componentInParent.GetComponent<ZNetView>();
		if (component?.GetZDO() == null)
		{
			return;
		}
		string text = component.GetZDO().GetString(CompanionSetup.OwnerHash);
		if (text.Length > 0 && text != localPlayer.GetPlayerID().ToString())
		{
			localPlayer.Message(MessageHud.MessageType.Center, ModLocalization.Loc("hc_msg_not_yours"));
			return;
		}
		CompanionsPlugin.Log.LogInfo($"[Interact] Separate key ({ModConfig.RadialMenuKey.Value}) — opening radial");
		CompanionRadialMenu.EnsureInstance();
		if (CompanionRadialMenu.Instance != null && !CompanionRadialMenu.Instance.IsVisible)
		{
			CompanionRadialMenu.Instance.Show(componentInParent);
		}
	}

	private void ProcessPendingTap()
	{
		if (_pendingTapContainer == null)
		{
			return;
		}
		float num = Time.time - _pendingTapTime;
		if (_pendingIsGamepad && !_gamepadReleaseDetected && ZInput.GetButtonUp("JoyUse"))
		{
			_gamepadReleaseDetected = true;
			CompanionsPlugin.Log.LogInfo($"[Interact] Gamepad ButtonUp detected at {num:F3}s");
		}
		bool flag = ((!_pendingIsGamepad) ? Input.GetKey(ModConfig.RadialMenuKey.Value) : (!_gamepadReleaseDetected));
		if (!_loggedPendingStart)
		{
			_loggedPendingStart = true;
			CompanionsPlugin.Log.LogInfo("[Interact] Update — processing pending tap: " + $"useHeld={flag} gamepad={_pendingIsGamepad} " + $"key={ModConfig.RadialMenuKey.Value} " + $"elapsed={num:F3}s");
		}
		if (!flag)
		{
			CompanionsPlugin.Log.LogInfo($"[Interact] Update — RELEASED after {num:F3}s " + $"(gamepad={_pendingIsGamepad}), opening inventory");
			Container pendingTapContainer = _pendingTapContainer;
			Player pendingTapPlayer = _pendingTapPlayer;
			CompanionSetup companionSetup = pendingTapContainer?.GetComponent<CompanionSetup>();
			_pendingTapContainer = null;
			_pendingTapPlayer = null;
			_loggedPendingStart = false;
			_gamepadReleaseDetected = false;
			if (pendingTapContainer != null && pendingTapPlayer != null && companionSetup != null)
			{
				OpenCompanionInventory(companionSetup, pendingTapContainer, pendingTapPlayer);
			}
		}
		else
		{
			if (!(num >= 0.2f))
			{
				return;
			}
			CompanionsPlugin.Log.LogInfo($"[Interact] Update — hold threshold reached ({num:F3}s), " + $"gamepad={_pendingIsGamepad}, opening radial");
			CompanionSetup component = _pendingTapContainer.GetComponent<CompanionSetup>();
			_pendingTapContainer = null;
			_pendingTapPlayer = null;
			_loggedPendingStart = false;
			_gamepadReleaseDetected = false;
			if (component != null)
			{
				CompanionRadialMenu.EnsureInstance();
				if (CompanionRadialMenu.Instance != null && !CompanionRadialMenu.Instance.IsVisible)
				{
					CompanionRadialMenu.Instance.Show(component);
					CompanionsPlugin.Log.LogInfo("[Interact] Radial menu Show() called");
				}
			}
		}
	}

	[SpecialName]
	private static string[] get_NotMyBedLines()
	{
		object obj = _notMyBedLines;
		if (obj == null)
		{
			obj = new string[3]
			{
				ModLocalization.Loc("hc_cmd_notmybed_1"),
				ModLocalization.Loc("hc_cmd_notmybed_2"),
				ModLocalization.Loc("hc_cmd_notmybed_3")
			};
			_notMyBedLines = (string[])obj;
		}
		return (string[])obj;
	}

	internal static void ResetCachedLines()
	{
		_comeHereLines = null;
		_attackLines = null;
		_cartPullLines = null;
		_cartReleasedLines = null;
		_doorLines = null;
		_sitLines = null;
		_sleepLines = null;
		_wakeLines = null;
		_depositLines = null;
		_depositEmptyLines = null;
		_harvestLines = null;
		_cancelLines = null;
		_moveLines = null;
		_repairLines = null;
		_repairNothingLines = null;
		_boardLines = null;
		_tombstoneLines = null;
		_smeltLines = null;
	}

	private static void FireComeToMe(Player player)
	{
		IReadOnlyList<CompanionSetup> allCompanions = CompanionSetup.AllCompanions;
		string localId = player.GetPlayerID().ToString();
		CancelAll(allCompanions, localId);
		CompanionTalk companionTalk = null;
		foreach (CompanionSetup item in allCompanions)
		{
			if (IsOwned(item, localId) && MHR_InScope(item))
			{
				MHR_Owner.TakeControl(item);
				CompanionRest.MHR_StayAwakeForRecall(item);
				// 1.4.150: you set Follow yourself - a job that had paused it no longer turns it back on/off later
				MHR_JobRules.FollowSetByPlayer(item);
				if (!item.GetFollow())
				{
					item.SetFollow(v: true);
					CompanionsPlugin.Log.LogDebug("[Direct] Come-to-me: forced Follow ON for \"" + (item.GetComponent<Character>()?.m_name ?? "?") + "\"");
				}
				CompanionAI component = item.GetComponent<CompanionAI>();
				if (component != null && Player.m_localPlayer != null)
				{
					component.SetFollowTarget(Player.m_localPlayer.gameObject);
				}
				ReturnLogic.Trigger(item, player.gameObject);
				if (companionTalk == null)
				{
					companionTalk = item.GetComponent<CompanionTalk>();
				}
			}
		}
		SayRandom(companionTalk, ComeHereLines);
		CompanionsPlugin.Log.LogDebug("[Direct] Come-to-me triggered");
	}

	internal static void MHR_FireSuperRecall(Player player)
	{
		_mhrSuperRecall = true;
		try
		{
			FireComeToMe(player);
		}
		finally
		{
			_mhrSuperRecall = false;
		}
		MessageHud.instance?.ShowMessage(MessageHud.MessageType.Center, ModLocalization.Loc("hc_msg_recall_all"));
		CompanionsPlugin.Log.LogInfo("[Direct] Super recall: all companions, any distance, Command toggle ignored");
	}

	/// <summary>
	/// Fase 3.9: the "Chamar" button of the AI panel, for <b>one</b> companion - the very steps the come-to-me key
	/// runs for everyone in scope (<see cref="FireComeToMe"/>): take control, drop what it was doing, keep it awake,
	/// turn Follow on (the player asked for it, so a paused job must not turn it back off) and let
	/// <see cref="ReturnLogic"/> bring it in. Two differences, both because it comes from a button and not from the
	/// command key: it needs no Command toggle and no recall range (a click on its own tab is a deliberate call for
	/// that one companion), and only the companion whose tab is open moves.
	/// </summary>
	internal static bool RecallOne(CompanionSetup setup, Player player)
	{
		if (setup == null || player == null)
		{
			return false;
		}
		Character character = setup.GetComponent<Character>();
		string name = (character != null && !string.IsNullOrEmpty(character.m_name)) ? character.m_name : "?";
		if (character == null || character.GetHealth() <= 0f)
		{
			CompanionsPlugin.Log.LogInfo("[Direct] AI panel: \"" + name + "\" is dead - not recalled");
			return false;
		}
		if (!IsOwned(setup, player.GetPlayerID().ToString()))
		{
			CompanionsPlugin.Log.LogInfo("[Direct] AI panel: \"" + name + "\" isn't yours - not recalled");
			return false;
		}
		MHR_Owner.TakeControl(setup);
		// drop whatever it was on (job, move target, cart, rest...) before the walk-in starts, like the come-to-me key
		CancelExistingActions(setup);
		CompanionRest.MHR_StayAwakeForRecall(setup);
		MHR_JobRules.FollowSetByPlayer(setup);
		if (!setup.GetFollow())
		{
			setup.SetFollow(v: true);
		}
		// mode 0 = follow: a job that was running must not come back and pause Follow again
		CompanionSetup.ApplyActionMode(setup, CompanionSetup.ModeFollow, toggle: false);
		setup.GetComponent<CompanionAI>()?.SetFollowTarget(player.gameObject);
		float distance = Vector3.Distance(setup.transform.position, player.transform.position);
		bool recalled = ReturnLogic.RecallToPlayer(setup, player);
		CompanionsPlugin.Log.LogInfo("[Direct] AI panel: \"" + name + "\" called from its tab (" + distance.ToString("F0") + "m away) - "
			+ (recalled ? "on its way" : "the recall did not start"));
		return recalled;
	}

	private static void DirectAttack(IReadOnlyList<CompanionSetup> setups, string localId, Character enemy)
	{
		int num = 0;
		CompanionTalk companionTalk = null;
		foreach (CompanionSetup setup in setups)
		{
			if (!IsOwned(setup, localId) || !MHR_InScope(setup))
			{
				continue;
			}
			CompanionAI component = setup.GetComponent<CompanionAI>();
			if (!(component == null))
			{
				CancelExistingActions(setup);
				component.DirectEngage(enemy);
				if (companionTalk == null)
				{
					companionTalk = setup.GetComponent<CompanionTalk>();
				}
				num++;
			}
		}
		SayRandom(companionTalk, AttackLines);
		CompanionsPlugin.Log.LogDebug($"[Direct] {num} companion(s) → attack \"{enemy.m_name}\"");
	}

	private static void DirectCart(IReadOnlyList<CompanionSetup> setups, string localId, Vagon vagon)
	{
		foreach (CompanionSetup setup in setups)
		{
			if (!IsOwned(setup, localId))
			{
				continue;
			}
			Character component = setup.GetComponent<Character>();
			if (!(component != null) || !vagon.IsAttached(component))
			{
				continue;
			}
			Humanoid component2 = setup.GetComponent<Humanoid>();
			if (component2 != null)
			{
				vagon.Interact(component2, hold: false, alt: false);
				CompanionAI component3 = setup.GetComponent<CompanionAI>();
				if (component3 != null && Player.m_localPlayer != null)
				{
					component3.SetFollowTarget(Player.m_localPlayer.gameObject);
				}
				SayRandom(setup.GetComponent<CompanionTalk>(), CartReleasedLines);
				CompanionsPlugin.Log.LogDebug("[Direct] Companion → detach from cart");
			}
			return;
		}
		CompanionSetup companionSetup = null;
		float num = float.MaxValue;
		foreach (CompanionSetup setup2 in setups)
		{
			if (IsOwned(setup2, localId) && MHR_InScope(setup2))
			{
				float num2 = Vector3.Distance(setup2.transform.position, vagon.transform.position);
				if (num2 < num)
				{
					num = num2;
					companionSetup = setup2;
				}
			}
		}
		if (companionSetup == null)
		{
			return;
		}
		Humanoid component4 = companionSetup.GetComponent<Humanoid>();
		CompanionAI component5 = companionSetup.GetComponent<CompanionAI>();
		if (component4 == null || component5 == null)
		{
			return;
		}
		CancelExistingActions(companionSetup);
		Vector3 vector = vagon.m_attachPoint.position - vagon.m_attachOffset;
		vector.y = companionSetup.transform.position.y;
		float num3 = Vector3.Distance(companionSetup.transform.position, vector);
		if (num3 < 3f)
		{
			companionSetup.transform.position = vector;
			Rigidbody component6 = companionSetup.GetComponent<Rigidbody>();
			if (component6 != null)
			{
				component6.position = vector;
				component6.linearVelocity = Vector3.zero;
			}
			Vector3 vector2 = vagon.transform.position - companionSetup.transform.position;
			vector2.y = 0f;
			if (vector2.sqrMagnitude > 0.01f)
			{
				companionSetup.transform.rotation = Quaternion.LookRotation(vector2.normalized);
			}
			component5.FreezeTimer = 1f;
			component5.SetFollowTarget(vagon.gameObject);
			vagon.Interact(component4, hold: false, alt: false);
		}
		else
		{
			component5.SetPendingCart(vagon, component4);
		}
		SayRandom(companionSetup.GetComponent<CompanionTalk>(), CartPullLines);
		CompanionsPlugin.Log.LogDebug($"[Direct] Companion → cart attach (dist={num:F1}m)");
	}

	private static void DirectDoor(IReadOnlyList<CompanionSetup> setups, string localId, Door door)
	{
		int num = 0;
		CompanionTalk companionTalk = null;
		foreach (CompanionSetup setup in setups)
		{
			if (!IsOwned(setup, localId) || !MHR_InScope(setup))
			{
				continue;
			}
			CancelExistingActions(setup);
			DoorHandler component = setup.GetComponent<DoorHandler>();
			if (!(component == null))
			{
				component.DirectOpenDoor(door);
				if (companionTalk == null)
				{
					companionTalk = setup.GetComponent<CompanionTalk>();
				}
				num++;
			}
		}
		SayRandom(companionTalk, DoorLines);
		CompanionsPlugin.Log.LogDebug($"[Direct] {num} companion(s) → open door \"{door.m_name}\"");
	}

	private static void DirectSit(IReadOnlyList<CompanionSetup> setups, string localId, Fireplace fire)
	{
		if (!fire.IsBurning())
		{
			return;
		}
		int num = 0;
		CompanionTalk companionTalk = null;
		foreach (CompanionSetup setup in setups)
		{
			if (!IsOwned(setup, localId) || !MHR_InScope(setup))
			{
				continue;
			}
			CancelExistingActions(setup, cancelRest: false);
			CompanionRest component = setup.GetComponent<CompanionRest>();
			if (!(component == null))
			{
				component.DirectSit(fire.gameObject);
				if (companionTalk == null)
				{
					companionTalk = setup.GetComponent<CompanionTalk>();
				}
				num++;
			}
		}
		SayRandom(companionTalk, SitLines);
		CompanionsPlugin.Log.LogDebug($"[Direct] {num} companion(s) → sit near fire");
	}

	private static void DirectSleep(IReadOnlyList<CompanionSetup> setups, string localId, Bed bed)
	{
		int num = 0;
		int num2 = 0;
		CompanionTalk companionTalk = null;
		foreach (CompanionSetup setup in setups)
		{
			if (!IsOwned(setup, localId) || !MHR_InScope(setup))
			{
				continue;
			}
			CancelExistingActions(setup, cancelRest: false);
			CompanionRest component = setup.GetComponent<CompanionRest>();
			if (component == null)
			{
				continue;
			}
			bool flag = component.IsResting || component.IsNavigating;
			if (!component.DirectSleep(bed))
			{
				SayRandom(setup.GetComponent<CompanionTalk>(), get_NotMyBedLines());
				continue;
			}
			bool flag2 = component.IsResting || component.IsNavigating;
			if (!flag && flag2)
			{
				num++;
			}
			else if (flag && !flag2)
			{
				num2++;
			}
			if (companionTalk == null)
			{
				companionTalk = setup.GetComponent<CompanionTalk>();
			}
		}
		if (num2 > 0)
		{
			SayRandom(companionTalk, WakeLines);
		}
		else if (num > 0)
		{
			SayRandom(companionTalk, SleepLines);
		}
		else
		{
			SayRandom(companionTalk, CancelLines);
		}
		CompanionsPlugin.Log.LogDebug($"[Direct] Sleep — started={num}, wokeUp={num2}");
	}

	private static void DirectRepair(IReadOnlyList<CompanionSetup> setups, string localId, CraftingStation station)
	{
		CompanionTalk companionTalk = null;
		int num = 0;
		foreach (CompanionSetup setup in setups)
		{
			if (!IsOwned(setup, localId) || !MHR_InScope(setup))
			{
				continue;
			}
			RepairController component = setup.GetComponent<RepairController>();
			if (!(component == null))
			{
				if (companionTalk == null)
				{
					companionTalk = setup.GetComponent<CompanionTalk>();
				}
				CancelExistingActions(setup);
				if (component.DirectRepairAt(station))
				{
					num++;
				}
			}
		}
		if (num > 0)
		{
			SayRandom(companionTalk, RepairLines);
		}
		else
		{
			SayRandom(companionTalk, RepairNothingLines);
		}
		CompanionsPlugin.Log.LogDebug($"[Direct] {num} companion(s) → repair at \"{station.m_name}\"");
	}

	private static void DirectSmelt(IReadOnlyList<CompanionSetup> setups, string localId)
	{
		CompanionTalk companionTalk = null;
		int num = 0;
		foreach (CompanionSetup setup in setups)
		{
			if (!IsOwned(setup, localId) || !MHR_InScope(setup))
			{
				continue;
			}
			CancelExistingActions(setup);
			ZNetView component = setup.GetComponent<ZNetView>();
			if (component?.GetZDO() != null)
			{
				component.GetZDO().Set(CompanionSetup.ActionModeHash, 6);
				MHR_JobRules.OnJobSet(setup, 6);
				setup.GetComponent<SmeltController>()?.NotifyActionModeChanged();
				if (companionTalk == null)
				{
					companionTalk = setup.GetComponent<CompanionTalk>();
				}
				num++;
			}
		}
		SayRandom(companionTalk, SmeltLines);
		CompanionsPlugin.Log.LogDebug($"[Direct] {num} companion(s) → smelt mode");
	}

	private static void DirectBoard(IReadOnlyList<CompanionSetup> setups, string localId, Ship ship)
	{
		Chair[] componentsInChildren = ship.GetComponentsInChildren<Chair>();
		List<Chair> list = new List<Chair>();
		List<Character> allCharacters = Character.GetAllCharacters();
		Chair[] array = componentsInChildren;
		foreach (Chair chair in array)
		{
			if (!chair.m_inShip || chair.m_attachPoint == null)
			{
				continue;
			}
			Vector3 position = chair.m_attachPoint.position;
			bool flag = false;
			foreach (Character item in allCharacters)
			{
				if (Vector3.Distance(item.transform.position, position) < 0.5f)
				{
					flag = true;
					break;
				}
			}
			if (!flag)
			{
				list.Add(chair);
			}
		}
		CompanionTalk companionTalk = null;
		int num = 0;
		HashSet<Chair> hashSet = new HashSet<Chair>();
		foreach (CompanionSetup setup in setups)
		{
			if (!IsOwned(setup, localId) || !MHR_InScope(setup))
			{
				continue;
			}
			CompanionAI component = setup.GetComponent<CompanionAI>();
			if (component == null || component.IsOnShip)
			{
				continue;
			}
			CancelExistingActions(setup);
			Chair chair2 = null;
			float num2 = float.MaxValue;
			foreach (Chair item2 in list)
			{
				if (!hashSet.Contains(item2))
				{
					float num3 = Vector3.Distance(setup.transform.position, item2.m_attachPoint.position);
					if (num3 < num2)
					{
						num2 = num3;
						chair2 = item2;
					}
				}
			}
			if (chair2 != null)
			{
				hashSet.Add(chair2);
				component.SetPendingShipBoard(ship, chair2);
			}
			else
			{
				Vector3 position2 = ship.transform.position + ship.transform.up * 1.5f;
				setup.transform.position = position2;
				Rigidbody component2 = setup.GetComponent<Rigidbody>();
				if (component2 != null)
				{
					component2.position = position2;
					component2.linearVelocity = Vector3.zero;
				}
				component.SetFollowTarget(ship.gameObject);
				component.FreezeTimer = 1f;
			}
			if (companionTalk == null)
			{
				companionTalk = setup.GetComponent<CompanionTalk>();
			}
			num++;
		}
		SayRandom(companionTalk, BoardLines);
		CompanionsPlugin.Log.LogDebug($"[Direct] {num} companion(s) → board ship");
	}

	private static void DirectTombstoneRecovery(IReadOnlyList<CompanionSetup> setups, string localId, TombStone tombstone)
	{
		CompanionSetup companionSetup = null;
		float num = float.MaxValue;
		foreach (CompanionSetup setup in setups)
		{
			if (IsOwned(setup, localId) && MHR_InScope(setup))
			{
				float num2 = Vector3.Distance(setup.transform.position, tombstone.transform.position);
				if (num2 < num)
				{
					num = num2;
					companionSetup = setup;
				}
			}
		}
		if (!(companionSetup == null))
		{
			CancelExistingActions(companionSetup);
			CompanionAI component = companionSetup.GetComponent<CompanionAI>();
			if (!(component == null))
			{
				component.SetDirectedTombstoneRecovery(tombstone);
				SayRandom(companionSetup.GetComponent<CompanionTalk>(), TombstoneLines);
				CompanionsPlugin.Log.LogDebug("[Direct] Companion → recover tombstone");
			}
		}
	}

	private static void DirectDeposit(IReadOnlyList<CompanionSetup> setups, string localId, Container chest)
	{
		Inventory inventory = chest.GetInventory();
		if (inventory == null)
		{
			return;
		}
		int num = 0;
		CompanionTalk companionTalk = null;
		foreach (CompanionSetup setup in setups)
		{
			if (!IsOwned(setup, localId) || !MHR_InScope(setup))
			{
				continue;
			}
			Humanoid component = setup.GetComponent<Humanoid>();
			if (component == null)
			{
				continue;
			}
			Inventory inventory2 = component.GetInventory();
			if (inventory2 == null)
			{
				continue;
			}
			bool flag = false;
			foreach (ItemDrop.ItemData allItem in inventory2.GetAllItems())
			{
				if (!ShouldKeep(allItem, component))
				{
					flag = true;
					break;
				}
			}
			if (companionTalk == null)
			{
				companionTalk = setup.GetComponent<CompanionTalk>();
			}
			if (flag)
			{
				CancelExistingActions(setup);
				CompanionAI component2 = setup.GetComponent<CompanionAI>();
				if (!(component2 == null))
				{
					component2.SetPendingDeposit(chest, component);
					num++;
				}
			}
		}
		if (num > 0)
		{
			SayRandom(companionTalk, DepositLines);
		}
		else
		{
			SayRandom(companionTalk, DepositEmptyLines);
		}
		CompanionsPlugin.Log.LogDebug($"[Direct] {num} companion(s) → deposit");
	}

	private static GameObject GetHarvestable(Collider col)
	{
		TreeBase componentInParent = col.GetComponentInParent<TreeBase>();
		if (componentInParent != null)
		{
			return componentInParent.gameObject;
		}
		TreeLog componentInParent2 = col.GetComponentInParent<TreeLog>();
		if (componentInParent2 != null)
		{
			return componentInParent2.gameObject;
		}
		MineRock5 componentInParent3 = col.GetComponentInParent<MineRock5>();
		if (componentInParent3 != null)
		{
			return componentInParent3.gameObject;
		}
		MineRock componentInParent4 = col.GetComponentInParent<MineRock>();
		if (componentInParent4 != null)
		{
			return componentInParent4.gameObject;
		}
		Destructible componentInParent5 = col.GetComponentInParent<Destructible>();
		if (componentInParent5 != null)
		{
			if (componentInParent5.m_damages.m_chop != HitData.DamageModifier.Immune && componentInParent5.gameObject.name.IndexOf("stub", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return componentInParent5.gameObject;
			}
			if (componentInParent5.m_damages.m_pickaxe != HitData.DamageModifier.Immune && componentInParent5.m_damages.m_chop == HitData.DamageModifier.Immune)
			{
				return componentInParent5.gameObject;
			}
		}
		return null;
	}

	private static void DirectGatherMode(IReadOnlyList<CompanionSetup> setups, string localId, GameObject target)
	{
		int num = HarvestController.DetermineHarvestModeStatic(target);
		if (num < 0)
		{
			return;
		}
		CompanionTalk companionTalk = null;
		int num2 = 0;
		foreach (CompanionSetup setup in setups)
		{
			if (!IsOwned(setup, localId) || !MHR_InScope(setup))
			{
				continue;
			}
			CancelExistingActions(setup);
			ZNetView component = setup.GetComponent<ZNetView>();
			if (component?.GetZDO() != null)
			{
				component.GetZDO().Set(CompanionSetup.ActionModeHash, num);
				MHR_JobRules.OnJobSet(setup, num);
				HarvestController component2 = setup.GetComponent<HarvestController>();
				if (component2 != null && num2 == 0)
				{
					component2.SetDirectedTarget(target);
				}
				if (companionTalk == null)
				{
					companionTalk = setup.GetComponent<CompanionTalk>();
				}
				num2++;
			}
		}
		SayRandom(companionTalk, HarvestLines);
		CompanionsPlugin.Log.LogDebug($"[Direct] {num2} companion(s) → gather mode {num}");
	}

	private static void DirectGround(IReadOnlyList<CompanionSetup> setups, string localId, Vector3 point)
	{
		foreach (CompanionSetup setup in setups)
		{
			if (IsOwned(setup, localId) && MHR_InScope(setup))
			{
				HarvestController component = setup.GetComponent<HarvestController>();
				if (component != null && component.IsInGatherMode)
				{
					ExitGatherMode(setups, localId);
					return;
				}
			}
		}
		CompanionTalk companionTalk = null;
		int num = 0;
		foreach (CompanionSetup setup2 in setups)
		{
			if (!IsOwned(setup2, localId) || !MHR_InScope(setup2))
			{
				continue;
			}
			CancelExistingActions(setup2);
			CompanionAI component2 = setup2.GetComponent<CompanionAI>();
			if (!(component2 == null))
			{
				component2.SetMoveTarget(point);
				if (companionTalk == null)
				{
					companionTalk = setup2.GetComponent<CompanionTalk>();
				}
				num++;
			}
		}
		SayRandom(companionTalk, MoveLines);
		CompanionsPlugin.Log.LogDebug($"[Direct] {num} companion(s) → move to {point:F1}");
		if (num == 0)
		{
			MHR_WhyNotCommanded(setups, localId);
		}
	}

	private static void CancelAll(IReadOnlyList<CompanionSetup> setups, string localId)
	{
		CompanionTalk companionTalk = null;
		foreach (CompanionSetup setup in setups)
		{
			if (!IsOwned(setup, localId) || !MHR_InScope(setup))
			{
				continue;
			}
			MHR_Owner.TakeControl(setup);
			if (companionTalk == null)
			{
				companionTalk = setup.GetComponent<CompanionTalk>();
			}
			CompanionAI component = setup.GetComponent<CompanionAI>();
			if (component != null)
			{
				component.CancelPendingCart();
				component.CancelMoveTarget();
				component.CancelPendingDeposit();
				component.CancelPendingShipBoard();
				component.CancelTombstoneRecovery();
				component.ClearTargets();
				component.StopMoving();
				if (component.IsOnShip)
				{
					component.DetachFromShip();
				}
				if (Player.m_localPlayer != null && setup.GetFollow())
				{
					component.SetFollowTarget(Player.m_localPlayer.gameObject);
				}
				else if (!setup.GetFollow())
				{
					component.SetFollowTarget(null);
				}
			}
			ZNetView component2 = setup.GetComponent<ZNetView>();
			if (component2?.GetZDO() != null && component2.GetZDO().GetInt(CompanionSetup.ActionModeHash) != 0)
			{
				component2.GetZDO().Set(CompanionSetup.ActionModeHash, 0);
				// 1.4.150: the job's off - Follow comes back if the job had turned it off (same rule as the wheel)
				MHR_JobRules.OnJobSet(setup, 0);
			}
			setup.GetComponent<DoorHandler>()?.Cancel();
			setup.GetComponent<HarvestController>()?.NotifyActionModeChanged();
			CompanionRest component3 = setup.GetComponent<CompanionRest>();
			if (component3 != null)
			{
				component3.CancelDirected();
			}
			RepairController component4 = setup.GetComponent<RepairController>();
			if (component4 != null && component4.IsActive)
			{
				component4.CancelDirected();
			}
			SmeltController component5 = setup.GetComponent<SmeltController>();
			if (component5 != null)
			{
				component5.NotifyActionModeChanged();
			}
			setup.GetComponent<UpkeepController>()?.NotifyActionModeChanged();
		}
		SayRandom(companionTalk, CancelLines);
		CompanionsPlugin.Log.LogDebug("[Direct] Cancelled all directed commands");
	}

	private static bool IsOwned(CompanionSetup setup, string localId)
	{
		ZNetView component = setup.GetComponent<ZNetView>();
		if (component == null || component.GetZDO() == null)
		{
			return false;
		}
		return component.GetZDO().GetString(CompanionSetup.OwnerHash) == localId;
	}

	// 1.4.100: say why nobody answered a command (1.4.99 test: "the command button was not working" - 0 companions)
	private static void MHR_WhyNotCommanded(IReadOnlyList<CompanionSetup> setups, string localId)
	{
		Player localPlayer = Player.m_localPlayer;
		foreach (CompanionSetup s in setups)
		{
			if (s == null)
			{
				continue;
			}
			string name = s.GetComponent<Character>()?.m_name ?? "?";
			string why;
			if (!IsOwned(s, localId))
			{
				why = "not yours";
			}
			else if (!s.GetIsCommandable())
			{
				why = "Command is switched off for it on the wheel";
			}
			else if (localPlayer != null && ModConfig.RecallRange != null && ModConfig.RecallRange.Value > 0f)
			{
				why = $"it's {Vector3.Distance(s.transform.position, localPlayer.transform.position):F0} m away (RecallRange is {ModConfig.RecallRange.Value:F0} m - hold the command key {ModConfig.SuperRecallHoldTime?.Value ?? 2f:F0}s to call everyone)";
			}
			else
			{
				why = "unknown";
			}
			CompanionsPlugin.Log.LogInfo("[Direct] \"" + name + "\" didn't take the command: " + why);
		}
	}

	// 1.4.152: every command checks IsOwned(...) && MHR_InScope(...) - a companion that answers is taken over by this
	// game (MHR_Owner.TakeControl), so the order is carried out here and not lost on another player's game
	internal static bool MHR_InScope(CompanionSetup s)
	{
		bool inScope = MHR_InScopeCheck(s);
		if (inScope)
		{
			MHR_Owner.TakeControl(s);
		}
		return inScope;
	}

	private static bool MHR_InScopeCheck(CompanionSetup s)
	{
		if (_mhrSuperRecall)
		{
			return true;
		}
		if (!s.GetIsCommandable())
		{
			return false;
		}
		if (ModConfig.RecallRange != null)
		{
			float value = ModConfig.RecallRange.Value;
			if (value > 0f)
			{
				Player localPlayer = Player.m_localPlayer;
				if (!(localPlayer == null))
				{
					return Vector3.Distance(s.transform.position, localPlayer.transform.position) <= value;
				}
			}
		}
		return true;
	}

	private static void SayRandom(CompanionTalk talk, string[] pool)
	{
		if (!(talk == null) && pool != null && pool.Length != 0)
		{
			talk.Say(pool[UnityEngine.Random.Range(0, pool.Length)]);
		}
	}

	// Fase 3.6: internal so a chat order that points at a creature ("ataca o greydwarf") can drop whatever the
	// companion was on its way to do, exactly like the radial menu's attack does.
	internal static void CancelExistingActions(CompanionSetup setup, bool cancelRest = true)
	{
		// 1.4.152: an order from its player - that player's game runs the companion from now on, so the order (kept in
		// memory) is carried out (on another player's game it was silently lost)
		MHR_Owner.TakeControl(setup);
		if (cancelRest)
		{
			CompanionRest component = setup.GetComponent<CompanionRest>();
			if (component != null && (component.IsResting || component.IsNavigating))
			{
				component.CancelDirected();
			}
		}
		CompanionAI component2 = setup.GetComponent<CompanionAI>();
		if (component2 != null)
		{
			component2.CancelPendingCart();
			component2.CancelMoveTarget();
			component2.CancelPendingDeposit();
			component2.CancelPendingShipBoard();
			component2.CancelTombstoneRecovery();
			if (component2.IsOnShip)
			{
				component2.DetachFromShip();
			}
		}
		// Fase 3.1: the wheel's orders drop a "recolhe os itens do chão" that is running (a one-shot order, like the
		// ones above) - the chat path does the same in CompanionOrders.Run.
		MHR_Collect.Cancel(setup);
		setup.GetComponent<HarvestController>()?.CancelDirectedTarget();
		setup.GetComponent<DoorHandler>()?.Cancel();
		RepairController component3 = setup.GetComponent<RepairController>();
		if (component3 != null && component3.IsActive)
		{
			component3.CancelDirected();
		}
		SmeltController component4 = setup.GetComponent<SmeltController>();
		if (component4 != null && component4.IsActive)
		{
			component4.CancelDirected();
		}
		setup.GetComponent<UpkeepController>()?.NotifyActionModeChanged();
	}

	private static void ExitGatherMode(IReadOnlyList<CompanionSetup> setups, string localId)
	{
		CompanionTalk companionTalk = null;
		int num = 0;
		foreach (CompanionSetup setup in setups)
		{
			if (!IsOwned(setup, localId) || !MHR_InScope(setup))
			{
				continue;
			}
			ZNetView component = setup.GetComponent<ZNetView>();
			if (component?.GetZDO() != null)
			{
				component.GetZDO().Set(CompanionSetup.ActionModeHash, 0);
				MHR_JobRules.OnJobSet(setup, 0);
				if (companionTalk == null)
				{
					companionTalk = setup.GetComponent<CompanionTalk>();
				}
				num++;
			}
		}
		SayRandom(companionTalk, CancelLines);
		CompanionsPlugin.Log.LogDebug($"[Direct] {num} companion(s) exited gather mode → follow");
	}

	private static void ResetGamepadHold()
	{
		_gpHoldActive = false;
		_gpHoldTarget = null;
	}

	internal static void ClearPendingTap()
	{
		if (_pendingTapContainer != null)
		{
			CompanionsPlugin.Log.LogDebug("[Interact] ClearPendingTap called while pending");
		}
		_pendingTapContainer = null;
		_pendingTapPlayer = null;
		_loggedPendingStart = false;
		_gamepadReleaseDetected = false;
	}

	/// <summary>Fase 3.8: what came of a request to open a companion's inventory. Read by the AI order ("abre o teu
	/// inventário"), which has to tell the player what happened; the interact key ignores it.</summary>
	internal enum InventoryOpenResult
	{
		/// <summary>The game's window is showing the companion's pack (and the mod's panel with it).</summary>
		Opened,

		/// <summary>The container wasn't ours: its ownership was asked for and the window opens as soon as it arrives
		/// (the interact key's own "interact again in a moment", which an order retries by itself).</summary>
		Requesting,

		/// <summary>No InventoryGui in this scene - there is no world to show a window in.</summary>
		NoGui,

		/// <summary>Nothing to open (no container).</summary>
		NoContainer,

		/// <summary>Someone else has the companion's pack open (the game's own "in use" flag), so opening it here
		/// would let two players edit one inventory: only the owner of the ZDO saves it and the other one's edits are
		/// lost. Nothing was opened and nothing was marked.</summary>
		InUse
	}

	internal static InventoryOpenResult OpenCompanionInventory(CompanionSetup setup, Container container, Player player)
	{
		if (InventoryGui.instance == null)
		{
			return InventoryOpenResult.NoGui;
		}
		if (container == null)
		{
			return InventoryOpenResult.NoContainer;
		}
		// the game's own guard (Container.RPC_RequestOpen does the same on the owner): a pack that is already open
		// somewhere else must not be shown again here. Checked before the claim - claiming first would hide the
		// synced flag behind the new local one.
		if (CompanionContainerUse.InUse(container))
		{
			CompanionsPlugin.Log.LogWarning("[Companion] " + container.gameObject.name + " is in use by someone else - not opening it");
			Player.m_localPlayer?.Message(MessageHud.MessageType.Center, "$msg_inuse");
			return InventoryOpenResult.InUse;
		}
		ZNetView component = container.GetComponent<ZNetView>();
		if (component != null && !component.IsOwner())
		{
			component.ClaimOwnership();
			CompanionsPlugin.Log.LogInfo("[Companion] Requesting container access - interact again in a moment");
			return InventoryOpenResult.Requesting;
		}
		ShowCompanionInventory(setup, container);
		return InventoryOpenResult.Opened;
	}

	/// <summary>Shows a companion's container in the game's window and brings the mod's own panel along with it (the
	/// one with the name, the food slots and the gear). The ownership is NOT looked at here - the caller does that.
	/// The pack is marked as in use for as long as that window is open (see
	/// <see cref="CompanionContainerUse"/>), which the game does for a chest and the mod has to do for its own
	/// window.</summary>
	private static void ShowCompanionInventory(CompanionSetup setup, Container container)
	{
		InventoryGui.instance.Show(container);
		CompanionContainerUse.Mark(container);
		CompanionInteractPanel.EnsureInstance();
		CompanionInteractPanel.Instance?.Show(setup);
	}

	// ---- Fase 3.8: the AI order that opens the companion's inventory ----

	/// <summary>The container an AI order asked for, while its ownership is on its way, and how long we wait for it.</summary>
	private static Container _aiOpenContainer;

	private static CompanionSetup _aiOpenSetup;

	private static float _aiOpenDeadline;

	private static int _aiOpenFrame = -1;

	private const float AiOpenTimeoutSeconds = 5f;

	/// <summary>
	/// Fase 3.8: opens a companion's inventory for an AI order ("abre o teu inventário"). Exactly the call the
	/// interact key makes, plus one thing the key does not need: when the container isn't ours yet the ownership is
	/// asked for AND remembered, so the window opens by itself as soon as the ownership arrives
	/// (<see cref="TickPendingInventoryOpen"/>) - the player asked once, and being told to ask again would be a poor
	/// answer to a spoken order.
	/// </summary>
	internal static InventoryOpenResult RequestCompanionInventory(CompanionSetup setup, Container container, Player player)
	{
		InventoryOpenResult result = OpenCompanionInventory(setup, container, player);
		if (result == InventoryOpenResult.Requesting)
		{
			_aiOpenContainer = container;
			_aiOpenSetup = setup;
			_aiOpenDeadline = Time.time + AiOpenTimeoutSeconds;
		}
		return result;
	}

	/// <summary>
	/// Fase 3.8: opens the container an AI order is waiting for, once its ownership has arrived. Runs once per frame
	/// (the first CompanionInteract instance of the frame does it) and does nothing at all when no order is waiting.
	/// Gives up after <see cref="AiOpenTimeoutSeconds"/> and says so, so a request that can never be granted does not
	/// leave the player waiting for a window that never comes.
	/// </summary>
	internal static void TickPendingInventoryOpen()
	{
		if (_aiOpenContainer == null)
		{
			// Fase 3.24 (auditoria): a destroyed container reads as null too, and the pending open used to stay set
			// (_aiOpenSetup and the deadline) until the next order - which could then inherit a companion that was
			// never asked for. Nothing to do here anyway, so the state is dropped.
			ClearPendingInventoryOpen();
			return;
		}
		if (Time.frameCount == _aiOpenFrame)
		{
			return;
		}
		_aiOpenFrame = Time.frameCount;
		if (Time.time > _aiOpenDeadline)
		{
			CompanionsPlugin.Log.LogWarning("[IA] Inventory: the container's ownership never arrived - giving up on opening it");
			ClearPendingInventoryOpen();
			CompanionChatAI.Announce(ModLocalization.Loc("hc_ai_inventory_timeout"));
			return;
		}
		CompanionSetup setup = _aiOpenSetup;
		ZNetView nview = setup != null ? setup.GetComponent<ZNetView>() : null;
		if (nview == null || nview.GetZDO() == null)
		{
			// the companion is gone (dismissed, dead, unloaded): nothing to show any more
			CompanionsPlugin.Log.LogInfo("[IA] Inventory: the companion is gone - the pending open was dropped");
			ClearPendingInventoryOpen();
			return;
		}
		if (InventoryGui.IsVisible())
		{
			// the player opened something else in the meantime: leave it alone, the order is dropped silently
			CompanionsPlugin.Log.LogInfo("[IA] Inventory: another window is open - the pending open was dropped");
			ClearPendingInventoryOpen();
			return;
		}
		Container container = _aiOpenContainer;
		ZNetView containerView = container != null ? container.GetComponent<ZNetView>() : null;
		if (containerView != null && !containerView.IsOwner())
		{
			// still waiting for the ownership the order asked for (never claimed again here: once is enough)
			return;
		}
		ShowCompanionInventory(setup, container);
		CompanionsPlugin.Log.LogInfo("[IA] Inventory: ownership arrived - the companion's window is open");
		ClearPendingInventoryOpen();
	}

	private static void ClearPendingInventoryOpen()
	{
		_aiOpenContainer = null;
		_aiOpenSetup = null;
		_aiOpenDeadline = 0f;
	}

	internal string GetHoverText()
	{
		string companionName = GetCompanionName();
		string text = ModLocalization.Loc("hc_hover_inventory");
		string text2 = ModLocalization.Loc("hc_hover_commands");
		string text3 = ((!ZInput.IsGamepadActive()) ? ZInput.instance.GetBoundKeyString("Use") : ZInput.instance.GetBoundKeyString("JoyUse"));
		if (IsRadialKeyUse)
		{
			return companionName + "\n[<color=yellow><b>" + text3 + "</b></color>] " + text + "\n[<color=yellow>" + ModLocalization.Loc("hc_hover_hold") + " <b>" + text3 + "</b></color>] " + text2;
		}
		string text4 = ((!ZInput.IsGamepadActive()) ? ModConfig.RadialMenuKey.Value.ToString() : text3);
		return companionName + "\n[<color=yellow><b>" + text3 + "</b></color>] " + text + "\n[<color=yellow><b>" + text4 + "</b></color>] " + text2;
	}

	internal string GetCompanionName()
	{
		if (_nview != null && _nview.GetZDO() != null)
		{
			string text = _nview.GetZDO().GetString(CompanionSetup.NameHash);
			if (!string.IsNullOrEmpty(text))
			{
				return text;
			}
		}
		if (!(_character != null))
		{
			return ModLocalization.Loc("hc_msg_name_default");
		}
		return _character.m_name;
	}

	internal string GetHoverName()
	{
		return GetCompanionName();
	}
}
