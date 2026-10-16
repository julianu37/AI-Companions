using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Companions;

public static class CompanionPrefabs
{
	private static GameObject _container;

	private static readonly FieldInfo _namedPrefabsField = AccessTools.Field(typeof(ZNetScene), "m_namedPrefabs");

	public static void Init(ZNetScene zNetScene)
	{
		if (_container == null)
		{
			_container = new GameObject("HC_CompanionPrefabs");
			UnityEngine.Object.DontDestroyOnLoad(_container);
			_container.SetActive(value: false);
		}
		CreateCompanionPrefab(zNetScene);
		CompanionTierDef[] dvergerVariants = CompanionTierData.DvergerVariants;
		foreach (CompanionTierDef def in dvergerVariants)
		{
			CreateDvergerPrefab(zNetScene, def);
		}
		CompanionsPlugin.Log.LogInfo("[CompanionPrefabs] Registered companion prefabs.");
	}

	private static void CreateCompanionPrefab(ZNetScene zNetScene)
	{
		CompanionTierDef companion = CompanionTierData.Companion;
		if (zNetScene.GetPrefab(companion.PrefabName) != null)
		{
			return;
		}
		GameObject prefab = zNetScene.GetPrefab("Player");
		if (prefab == null)
		{
			CompanionsPlugin.Log.LogError("[CompanionPrefabs] Player prefab not found!");
			return;
		}
		try
		{
			CompanionSkills.InitSkillDefs(prefab);
		}
		catch (Exception)
		{
			CompanionsPlugin.Log.LogWarning("[CompanionPrefabs] InitSkillDefs failed - continuing without cached skill defs.");
		}
		Player component = prefab.GetComponent<Player>();
		if (component != null && CompanionAI.DrownEffects == null)
		{
			CompanionAI.DrownEffects = component.m_drownEffects;
			int valueOrDefault = (CompanionAI.DrownEffects?.m_effectPrefabs?.Length).GetValueOrDefault();
			CompanionsPlugin.Log.LogInfo($"[CompanionPrefabs] Cached m_drownEffects from Player ({valueOrDefault} effects)");
		}
		GameObject gameObject = UnityEngine.Object.Instantiate(prefab, _container.transform, worldPositionStays: false);
		gameObject.name = companion.PrefabName;
		DestroyComponent(gameObject, typeof(PlayerController));
		DestroyComponent(gameObject, typeof(Player));
		DestroyComponent(gameObject, typeof(Talker));
		DestroyComponent(gameObject, typeof(Skills));
		Humanoid component2 = prefab.GetComponent<Humanoid>();
		Humanoid humanoid = gameObject.AddComponent<Humanoid>();
		if (component2 != null)
		{
			humanoid.m_unarmedWeapon = component2.m_unarmedWeapon;
			humanoid.m_consumeItemEffects = component2.m_consumeItemEffects;
			humanoid.m_equipEffects = component2.m_equipEffects;
			int valueOrDefault2 = (humanoid.m_consumeItemEffects?.m_effectPrefabs?.Length).GetValueOrDefault();
			int valueOrDefault3 = (humanoid.m_equipEffects?.m_effectPrefabs?.Length).GetValueOrDefault();
			CompanionsPlugin.Log.LogInfo($"[CompanionPrefabs]   Copied m_unarmedWeapon + m_consumeItemEffects ({valueOrDefault2} effects)" + $" + m_equipEffects ({valueOrDefault3} effects)");
		}
		SetupHumanoid(humanoid, zNetScene, companion);
		CharacterAnimEvent componentInChildren = gameObject.GetComponentInChildren<CharacterAnimEvent>();
		if (componentInChildren != null)
		{
			FieldInfo fieldInfo = AccessTools.Field(typeof(CharacterAnimEvent), "m_character");
			if (fieldInfo != null)
			{
				fieldInfo.SetValue(componentInChildren, humanoid);
			}
		}
		CompanionAI ai = gameObject.AddComponent<CompanionAI>();
		SetupCompanionAI(ai);
		SetupSharedComponents(gameObject, companion, zNetScene);
		RegisterPrefab(gameObject, zNetScene);
	}

	private static void CreateDvergerPrefab(ZNetScene zNetScene, CompanionTierDef def)
	{
		if (zNetScene.GetPrefab(def.PrefabName) != null)
		{
			return;
		}
		GameObject prefab = zNetScene.GetPrefab(def.SourcePrefab);
		if (prefab == null)
		{
			CompanionsPlugin.Log.LogWarning("[CompanionPrefabs] Vanilla prefab \"" + def.SourcePrefab + "\" not found — " + def.DisplayName + " dverger variant will not be available.");
			return;
		}
		GameObject gameObject = UnityEngine.Object.Instantiate(prefab, _container.transform, worldPositionStays: false);
		gameObject.name = def.PrefabName;
		DestroyComponent(gameObject, typeof(MonsterAI));
		DestroyComponent(gameObject, typeof(CharacterDrop));
		DestroyComponent(gameObject, typeof(NpcTalk));
		Humanoid component = gameObject.GetComponent<Humanoid>();
		if (component != null)
		{
			List<GameObject> list = new List<GameObject>();
			HashSet<string> hashSet = new HashSet<string>();
			if (component.m_defaultItems != null)
			{
				GameObject[] defaultItems = component.m_defaultItems;
				foreach (GameObject gameObject2 in defaultItems)
				{
					if (gameObject2 != null && hashSet.Add(gameObject2.name))
					{
						list.Add(gameObject2);
					}
				}
			}
			MergeFirstItem(list, hashSet, component.m_randomWeapon);
			MergeFirstItem(list, hashSet, component.m_randomShield);
			MergeFirstItem(list, hashSet, component.m_randomArmor);
			if (component.m_randomSets != null)
			{
				Humanoid.ItemSet[] randomSets = component.m_randomSets;
				foreach (Humanoid.ItemSet itemSet in randomSets)
				{
					if (itemSet.m_items == null || itemSet.m_items.Length == 0)
					{
						continue;
					}
					GameObject[] items = itemSet.m_items;
					foreach (GameObject gameObject3 in items)
					{
						if (gameObject3 != null && hashSet.Add(gameObject3.name))
						{
							list.Add(gameObject3);
						}
					}
					break;
				}
			}
			component.m_defaultItems = list.ToArray();
			string text = "";
			GameObject[] defaultItems2 = component.m_defaultItems;
			foreach (GameObject gameObject4 in defaultItems2)
			{
				text = text + ((gameObject4 != null) ? gameObject4.name : "null") + ", ";
			}
			CompanionsPlugin.Log.LogInfo($"[CompanionPrefabs]   {def.PrefabName}: merged {component.m_defaultItems.Length} m_defaultItems: [{text.TrimEnd(',', ' ')}]");
			component.m_randomWeapon = Array.Empty<GameObject>();
			component.m_randomArmor = Array.Empty<GameObject>();
			component.m_randomShield = Array.Empty<GameObject>();
			component.m_randomSets = Array.Empty<Humanoid.ItemSet>();
			Humanoid humanoid = zNetScene.GetPrefab("Player")?.GetComponent<Humanoid>();
			if (humanoid != null)
			{
				component.m_consumeItemEffects = humanoid.m_consumeItemEffects;
				CompanionsPlugin.Log.LogInfo("[CompanionPrefabs]   Dverger: copied m_consumeItemEffects from Player");
			}
			SetupHumanoid(component, zNetScene, def);
			Animator componentInChildren = gameObject.GetComponentInChildren<Animator>();
			if (componentInChildren != null)
			{
				string text2 = ((componentInChildren.runtimeAnimatorController != null) ? componentInChildren.runtimeAnimatorController.name : "NULL");
				int parameterCount = componentInChildren.parameterCount;
				CompanionsPlugin.Log.LogInfo("[CompanionPrefabs]   " + def.PrefabName + ": Animator controller=\"" + text2 + "\" " + $"params={parameterCount}");
			}
			else
			{
				CompanionsPlugin.Log.LogWarning("[CompanionPrefabs]   " + def.PrefabName + ": NO Animator found on prefab!");
			}
			CompanionAI ai = gameObject.AddComponent<CompanionAI>();
			SetupCompanionAI(ai);
			SetupSharedComponents(gameObject, def, zNetScene);
			RegisterPrefab(gameObject, zNetScene);
		}
		else
		{
			CompanionsPlugin.Log.LogError("[CompanionPrefabs] " + def.SourcePrefab + " prefab missing Humanoid component!");
			UnityEngine.Object.DestroyImmediate(gameObject);
		}
	}

	private static void DestroyComponent(GameObject go, Type componentType)
	{
		Component component = go.GetComponent(componentType);
		if (component != null)
		{
			UnityEngine.Object.DestroyImmediate(component);
		}
	}

	private static void SetupHumanoid(Humanoid h, ZNetScene scene, CompanionTierDef def)
	{
		h.m_name = CompanionTierData.LocalName(def);
		h.m_group = "HC_Companion";
		h.m_faction = Character.Faction.Players;
		h.m_boss = false;
		h.m_health = def.Health;
		h.m_walkSpeed = ModConfig.WalkSpeed.Value;
		h.m_runSpeed = ModConfig.RunSpeed.Value;
		h.m_speed = 5f;
		h.m_crouchSpeed = 2f;
		h.m_turnSpeed = ModConfig.TurnSpeed.Value;
		h.m_runTurnSpeed = ModConfig.TurnSpeed.Value;
		h.m_acceleration = 1f;
		h.m_jumpForce = 8f;
		h.m_jumpForceForward = 2f;
		h.m_jumpForceTiredFactor = 0.6f;
		h.m_canSwim = true;
		h.m_swimDepth = 2f;
		h.m_swimSpeed = ModConfig.SwimSpeed.Value;
		h.m_swimTurnSpeed = 100f;
		h.m_swimAcceleration = 0.05f;
		h.m_groundTilt = Character.GroundTiltType.None;
		h.m_groundTiltSpeed = 50f;
		h.m_eye = Utils.FindChild(h.gameObject.transform, "EyePos");
		h.m_tolerateWater = true;
		h.m_staggerWhenBlocked = true;
		h.m_staggerDamageFactor = 0f;
		h.m_hitEffects.m_effectPrefabs = BuildEffects(scene, "vfx_player_hit", "sfx_hit");
		h.m_critHitEffects.m_effectPrefabs = BuildEffects(scene, "fx_crit");
		h.m_backstabHitEffects.m_effectPrefabs = BuildEffects(scene, "fx_backstab");
		h.m_deathEffects.m_effectPrefabs = BuildEffects(scene, "vfx_ghost_death", "sfx_ghost_death");
		h.m_damageModifiers = new HitData.DamageModifiers
		{
			m_chop = HitData.DamageModifier.Immune,
			m_pickaxe = HitData.DamageModifier.Immune,
			m_spirit = HitData.DamageModifier.Immune
		};
	}

	private static void SetupCompanionAI(CompanionAI ai)
	{
		CompanionsPlugin.Log.LogInfo("[CompanionPrefabs] Setting up CompanionAI...");
		ai.m_viewRange = ModConfig.ViewRange.Value;
		ai.m_viewAngle = ModConfig.ViewAngle.Value;
		ai.m_hearRange = 20f;
		ai.m_mistVision = true;
		ai.m_alertedEffects = new EffectList();
		ai.m_idleSound = new EffectList();
		ai.m_idleSoundInterval = 10f;
		ai.m_idleSoundChance = 0f;
		ai.m_pathAgentType = Pathfinding.AgentType.Humanoid;
		ai.m_moveMinAngle = 90f;
		ai.m_smoothMovement = true;
		ai.m_serpentMovement = false;
		ai.m_jumpInterval = 0f;
		ai.m_randomCircleInterval = 2f;
		ai.m_randomMoveInterval = 10f;
		ai.m_randomMoveRange = 10f;
		ai.m_avoidFire = false;
		ai.m_afraidOfFire = false;
		ai.m_avoidWater = false;
		ai.m_aggravatable = false;
		CompanionsPlugin.Log.LogInfo("[CompanionPrefabs] CompanionAI configured: pathAgent=Humanoid " + $"moveMinAngle={ai.m_moveMinAngle} smoothMove={ai.m_smoothMovement} " + $"viewRange={ai.m_viewRange} hearRange={ai.m_hearRange} " + $"randomMoveInterval={ai.m_randomMoveInterval} " + $"randomMoveRange={ai.m_randomMoveRange}");
	}

	private static void SetupSharedComponents(GameObject go, CompanionTierDef def, ZNetScene zNetScene)
	{
		ZNetView component = go.GetComponent<ZNetView>();
		component.m_persistent = true;
		component.m_distant = false;
		component.m_type = ZDO.ObjectType.Default;
		component.m_syncInitialScale = false;
		ZSyncTransform component2 = go.GetComponent<ZSyncTransform>();
		if (component2 != null)
		{
			component2.m_syncPosition = true;
			component2.m_syncRotation = true;
			component2.m_syncScale = false;
			component2.m_syncBodyVelocity = false;
			component2.m_characterParentSync = false;
		}
		ZSyncAnimation component3 = go.GetComponent<ZSyncAnimation>();
		if (component3 != null)
		{
			component3.m_smoothCharacterSpeeds = true;
		}
		Rigidbody component4 = go.GetComponent<Rigidbody>();
		if (component4 != null)
		{
			component4.mass = 50f;
		}
		go.AddComponent<CompanionSetup>();
		CompanionsPlugin.Log.LogInfo("[CompanionPrefabs]   + CompanionSetup (" + def.PrefabName + ")");
		Container container = go.AddComponent<Container>();
		container.m_name = CompanionTierData.LocalName(def);
		container.m_width = 8;
		container.m_height = 4;
		container.m_privacy = Container.PrivacySetting.Public;
		container.m_checkGuardStone = false;
		container.m_openEffects = new EffectList();
		container.m_closeEffects = new EffectList();
		go.AddComponent<CompanionInteract>();
		go.AddComponent<CompanionFood>();
		go.AddComponent<CompanionStamina>();
		go.AddComponent<CompanionSkills>();
		go.AddComponent<CompanionCombatAI>();
		go.AddComponent<HarvestController>();
		CompanionsPlugin.Log.LogInfo("[CompanionPrefabs]   + HarvestController (" + def.PrefabName + ")");
		go.AddComponent<CompanionTalk>();
		go.AddComponent<CompanionRest>();
		go.AddComponent<CompanionRestedBuff>();
		go.AddComponent<DoorHandler>();
		go.AddComponent<RepairController>();
		go.AddComponent<SmeltController>();
		go.AddComponent<FarmController>();
		go.AddComponent<FarmZoneVisual>();
		CompanionsPlugin.Log.LogInfo("[CompanionPrefabs]   + FarmController (" + def.PrefabName + ")");
		go.AddComponent<HuntController>();
		CompanionsPlugin.Log.LogInfo("[CompanionPrefabs]   + HuntController (" + def.PrefabName + ")");
		go.AddComponent<FishController>();
		go.AddComponent<UpkeepController>();
		go.AddComponent<IdleController>();
		go.AddComponent<CookController>();
	}

	internal static void RegisterPrefab(GameObject go, ZNetScene zNetScene)
	{
		int stableHashCode = go.name.GetStableHashCode();
		zNetScene.m_prefabs.Add(go);
		if (_namedPrefabsField?.GetValue(zNetScene) is Dictionary<int, GameObject> dictionary)
		{
			dictionary[stableHashCode] = go;
		}
		CompanionsPlugin.Log.LogInfo("[CompanionPrefabs] Registered " + go.name);
	}

	private static void MergeFirstItem(List<GameObject> merged, HashSet<string> seen, GameObject[] items)
	{
		if (items != null && items.Length != 0 && items[0] != null && seen.Add(items[0].name))
		{
			merged.Add(items[0]);
		}
	}

	private static EffectList.EffectData[] BuildEffects(ZNetScene scene, params string[] names)
	{
		List<EffectList.EffectData> list = new List<EffectList.EffectData>();
		foreach (string text in names)
		{
			GameObject prefab = scene.GetPrefab(text);
			if (prefab != null)
			{
				list.Add(new EffectList.EffectData
				{
					m_prefab = prefab,
					m_enabled = true,
					m_variant = -1
				});
			}
			else
			{
				CompanionsPlugin.Log.LogWarning("[CompanionPrefabs] Effect prefab not found: " + text);
			}
		}
		return list.ToArray();
	}
}
