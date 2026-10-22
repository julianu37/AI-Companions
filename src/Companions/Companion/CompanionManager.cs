using System.Collections.Generic;
using UnityEngine;

namespace Companions;

public static class CompanionManager
{
	public struct RespawnData
	{
		public string PrefabName;

		public string Name;

		public string AppearanceSerialized;

		public string OwnerId;

		public int ActionMode;

		public int ActionModeSchema;

		public bool Follow;

		public bool StayHome;

		public bool AutoPickup;

		public bool Wander;

		public bool IsCommandable;

		public int FormationSlot;

		public long TombstoneId;

		public string SkillsSerialized;

		public float Timer;

		public bool HasHomePos;

		public Vector3 HomePos;
	}

	private static Game s_mhrStarterGame;

	private static Game s_mhrSeenGame;

	private static float s_mhrSeenAt;

	private const string BankDataKey = "TraderSharedBank_Balance";

	private static readonly List<RespawnData> _respawnQueue = new List<RespawnData>();

	public static void MHR_StarterOnce()
	{
		Game instance = Game.instance;
		if (!(instance == null) && (object)s_mhrStarterGame != instance)
		{
			s_mhrStarterGame = instance;
			SpawnStarterCompanion();
		}
	}

	public static int GetBankBalance()
	{
		Player localPlayer = Player.m_localPlayer;
		if (localPlayer == null)
		{
			return 0;
		}
		if (localPlayer.m_customData.TryGetValue("TraderSharedBank_Balance", out var value) && int.TryParse(value, out var result))
		{
			return result;
		}
		return 0;
	}

	public static bool CanAfford()
	{
		return GetBankBalance() >= CompanionTierData.Price;
	}

	public static bool Purchase(CompanionAppearance appearance, CompanionTierDef def = null)
	{
		if (def == null)
		{
			def = CompanionTierData.Companion;
		}
		Player localPlayer = Player.m_localPlayer;
		if (localPlayer == null)
		{
			return false;
		}
		int price = CompanionTierData.Price;
		int bankBalance = GetBankBalance();
		if (bankBalance < price)
		{
			MessageHud.instance?.ShowMessage(MessageHud.MessageType.Center, ModLocalization.LocFmt("hc_msg_need_coins", price.ToString("N0")));
			return false;
		}
		bankBalance -= price;
		localPlayer.m_customData["TraderSharedBank_Balance"] = bankBalance.ToString();
		if (!SpawnCompanion(appearance, def))
		{
			bankBalance += price;
			localPlayer.m_customData["TraderSharedBank_Balance"] = bankBalance.ToString();
			return false;
		}
		MessageHud.instance?.ShowMessage(MessageHud.MessageType.Center, ModLocalization.LocFmt("hc_msg_arrived", CompanionTierData.LocalName(def).ToLower()));
		return true;
	}

	/// <summary>
	/// Fase 3.9: the AI panel's summoner ("Invocar"). It runs the <b>same</b> spawn <see cref="Purchase"/> runs - the
	/// prefab lookup, the position next to the player, the ZDO (appearance + owner), the follow target - and only
	/// skips the price: no bank read, no coin check, no charge. <see cref="Purchase"/> is left exactly as it is, so
	/// the Trader keeps selling companions the old way; this is a second door to <see cref="SpawnCompanion"/>, never
	/// a second spawn implementation.
	///
	/// <para>A null <paramref name="appearance"/> is the default look and a null <paramref name="def"/> is the plain
	/// Companion tier, so the panel can call it with just the tier the player picked. It is defensive on purpose
	/// (no player, no world, prefab not registered) and logs one <c>[Spawn]</c> line per call, success or failure:
	/// the button must never fail silently.</para>
	/// </summary>
	public static bool SpawnFree(CompanionAppearance appearance, CompanionTierDef def = null)
	{
		if (def == null)
		{
			def = CompanionTierData.Companion;
		}
		if (appearance == null)
		{
			appearance = CompanionAppearance.Default();
		}
		Player localPlayer = Player.m_localPlayer;
		if (localPlayer == null)
		{
			CompanionsPlugin.Log.LogWarning("[Spawn] no local player - nothing to summon (are you in the main menu?)");
			return false;
		}
		if (ZNetScene.instance == null)
		{
			CompanionsPlugin.Log.LogWarning("[Spawn] the world isn't loaded (ZNetScene is null) - nothing to summon");
			return false;
		}
		if (ZNetScene.instance.GetPrefab(def.PrefabName) == null)
		{
			// SpawnCompanion would log the same thing as [CompanionManager]; the [Spawn] line is the panel's trail
			CompanionsPlugin.Log.LogError("[Spawn] prefab not found: " + def.PrefabName + " - nothing to summon");
			return false;
		}
		if (!SpawnCompanion(appearance, def))
		{
			CompanionsPlugin.Log.LogWarning($"[Spawn] the spawn failed for tier \"{def.DisplayName}\" ({def.PrefabName}) - nothing was charged (there is no charge here)");
			return false;
		}
		CompanionsPlugin.Log.LogInfo($"[Spawn] summoned \"{CompanionTierData.LocalName(def)}\" ({def.PrefabName}) for free near {localPlayer.transform.position:F1} - AI panel, no coins");
		MessageHud.instance?.ShowMessage(MessageHud.MessageType.Center, ModLocalization.LocFmt("hc_msg_arrived", CompanionTierData.LocalName(def).ToLower()));
		return true;
	}

	private static bool SpawnCompanion(CompanionAppearance appearance, CompanionTierDef def, CompanionAiDraft aiDraft = null)
	{
		GameObject gameObject = ZNetScene.instance?.GetPrefab(def.PrefabName);
		if (gameObject == null)
		{
			CompanionsPlugin.Log.LogError("[CompanionManager] Prefab not found: " + def.PrefabName);
			return false;
		}
		Player localPlayer = Player.m_localPlayer;
		Vector3 position = FindSpawnPosition(localPlayer.transform.position, 4f);
		Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
		GameObject gameObject2 = Object.Instantiate(gameObject, position, rotation);
		ZNetView component = gameObject2.GetComponent<ZNetView>();
		if (component?.GetZDO() == null)
		{
			CompanionsPlugin.Log.LogError("[CompanionManager] ZDO not available after spawn — aborting.");
			Object.Destroy(gameObject2);
			return false;
		}
		ZDO zDO = component.GetZDO();
		// Fase 3.9: a fresh companion used to be spawned without a name of its own (HC_Name empty), so every one of
		// the same tier showed the very same name - two "Guerreiro" tabs in the AI panel, indistinguishable. The name
		// is written here, once, for every spawn (the Trader's purchase included): the tier's localized name, and a
		// number when it is already taken ("Guerreiro 2"). CompanionSetup.RestoreName applies it to the Character on
		// init, and the player can still rename it in the interact panel.
		zDO.Set(CompanionSetup.NameHash, NextDefaultName(def));
		zDO.Set(CompanionSetup.AppearanceHash, appearance.Serialize());
		zDO.Set(CompanionSetup.OwnerHash, localPlayer.GetPlayerID().ToString());
		// Fase 3.10: the AI profile the player chose in the starter panel (voice and personality of THIS companion).
		// Nothing is written for a choice left at "follow the [IA] default", so a fresh companion keeps following the
		// global values - and a spawn from the Trader (no draft) keeps doing exactly what it did before.
		if (aiDraft != null)
		{
			aiDraft.WriteTo(zDO);
			if (!aiDraft.IsEmpty)
			{
				CompanionsPlugin.Log.LogInfo($"[Spawn] AI profile of the new companion: voice "
					+ $"{(aiDraft.Voice > 0 ? aiDraft.Voice.ToString() : CompanionTts.Voice + " (default)")}"
					+ (aiDraft.HasPersona
						? $" | personality \"{aiDraft.Preset}\"{(string.IsNullOrEmpty(aiDraft.FreeText) ? "" : $" + {aiDraft.FreeText.Length} chars of free text")}"
						: " | personality: default ([IA])"));
			}
		}
		zDO.Persistent = true;
		CompanionSetup component2 = gameObject2.GetComponent<CompanionSetup>();
		if (component2 != null)
		{
			component2.ApplyAppearance(appearance);
		}
		gameObject2.GetComponent<CompanionAI>()?.SetFollowTarget(localPlayer.gameObject);
		CompanionsPlugin.Log.LogInfo($"[CompanionManager] Spawned companion for player {localPlayer.GetPlayerID()}");
		return true;
	}

	/// <summary>
	/// Fase 3.9: the name a newly spawned companion gets - the tier's localized name (<see cref="CompanionTierData.LocalName"/>,
	/// the same one the hover shows) and, when a companion in the scene already answers to it, the first free number
	/// after it ("Guerreiro 2", "Guerreiro 3"...). The number goes through the localization dictionary
	/// (<c>hc_msg_name_numbered</c>) because it is visible text. Runs once per spawn, so walking the companions here
	/// is free.
	/// </summary>
	private static string NextDefaultName(CompanionTierDef def)
	{
		string baseName = CompanionTierData.LocalName(def);
		HashSet<string> used = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
		foreach (CompanionSetup companion in CompanionSetup.AllCompanions)
		{
			if (companion == null)
			{
				continue;
			}
			// the same name the player reads: the ZDO's HC_Name, or - for a companion from an older save that was
			// never renamed - the Character's own name
			string name = companion.GetComponent<ZNetView>()?.GetZDO()?.GetString(CompanionSetup.NameHash, "");
			if (string.IsNullOrEmpty(name))
			{
				Character character = companion.GetComponent<Character>();
				name = character != null ? character.m_name : null;
			}
			if (!string.IsNullOrEmpty(name))
			{
				used.Add(name);
			}
		}
		if (!used.Contains(baseName))
		{
			return baseName;
		}
		for (int i = 2; i < 100; i++)
		{
			string candidate = ModLocalization.LocFmt("hc_msg_name_numbered", baseName, i);
			if (!used.Contains(candidate))
			{
				return candidate;
			}
		}
		return baseName;
	}

	public static void RestoreFollowTargets()
	{
		foreach (CompanionSetup allCompanion in CompanionSetup.AllCompanions)
		{
			allCompanion.RestoreFollowTarget();
		}
	}

	public static void QueueRespawn(RespawnData data)
	{
		for (int i = 0; i < _respawnQueue.Count; i++)
		{
			RespawnData respawnData = _respawnQueue[i];
			if (!(respawnData.OwnerId == data.OwnerId))
			{
				continue;
			}
			bool num;
			if (data.TombstoneId == 0L)
			{
				if (!(respawnData.Name == data.Name))
				{
					continue;
				}
				num = respawnData.AppearanceSerialized == data.AppearanceSerialized;
			}
			else
			{
				num = respawnData.TombstoneId == data.TombstoneId;
			}
			if (num)
			{
				CompanionsPlugin.Log.LogWarning("[CompanionManager] Duplicate respawn blocked for \"" + data.Name + "\" " + $"(owner={data.OwnerId} tombstoneId={data.TombstoneId}) — already queued");
				return;
			}
		}
		_respawnQueue.Add(data);
		CompanionsPlugin.Log.LogInfo($"[CompanionManager] Queued respawn for \"{data.Name}\" in {data.Timer}s");
	}

	public static void ProcessRespawns(float dt)
	{
		if (_respawnQueue.Count == 0)
		{
			return;
		}
		for (int num = _respawnQueue.Count - 1; num >= 0; num--)
		{
			RespawnData respawnData = _respawnQueue[num];
			respawnData.Timer -= dt;
			_respawnQueue[num] = respawnData;
			if (respawnData.Timer <= 0f)
			{
				_respawnQueue.RemoveAt(num);
				DoRespawn(respawnData);
			}
		}
	}

	private static void DoRespawn(RespawnData data)
	{
		CompanionsPlugin.Log.LogInfo("[CompanionManager] Processing respawn for \"" + data.Name + "\" — prefab=" + data.PrefabName);
		if (Player.m_localPlayer == null)
		{
			data.Timer = 1f;
			_respawnQueue.Add(data);
			CompanionsPlugin.Log.LogDebug("[CompanionManager] Player not loaded — re-queued respawn");
			return;
		}
		foreach (CompanionSetup allCompanion in CompanionSetup.AllCompanions)
		{
			ZDO zDO = allCompanion.GetComponent<ZNetView>()?.GetZDO();
			if (zDO == null)
			{
				continue;
			}
			string text = zDO.GetString(CompanionSetup.OwnerHash);
			string text2 = zDO.GetString(CompanionSetup.NameHash);
			if (text == data.OwnerId && text2 == data.Name)
			{
				Character component = allCompanion.GetComponent<Character>();
				if (component != null && component.GetHealth() > 0f)
				{
					CompanionsPlugin.Log.LogWarning("[CompanionManager] Duplicate respawn aborted — \"" + data.Name + "\" " + $"already exists in scene with health {component.GetHealth():F0}");
					return;
				}
			}
		}
		Vector3 vector;
		if (data.HasHomePos)
		{
			vector = data.HomePos;
			CompanionsPlugin.Log.LogInfo($"[CompanionManager] Respawning at home position: {vector:F1}");
		}
		else
		{
			vector = GetWorldSpawnPoint();
			CompanionsPlugin.Log.LogInfo($"[CompanionManager] Respawning at world spawn: {vector:F1}");
		}
		GameObject gameObject = ZNetScene.instance?.GetPrefab(data.PrefabName);
		if (gameObject == null)
		{
			CompanionsPlugin.Log.LogError("[CompanionManager] Respawn failed — prefab not found: " + data.PrefabName);
			return;
		}
		Vector3 vector2 = (data.HasHomePos ? vector : FindSpawnPosition(vector, 4f));
		Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
		GameObject gameObject2 = Object.Instantiate(gameObject, vector2, rotation);
		ZNetView component2 = gameObject2.GetComponent<ZNetView>();
		if (component2?.GetZDO() == null)
		{
			CompanionsPlugin.Log.LogError("[CompanionManager] Respawn failed — ZDO not available.");
			Object.Destroy(gameObject2);
			return;
		}
		ZDO zDO2 = component2.GetZDO();
		zDO2.Set(CompanionSetup.AppearanceHash, data.AppearanceSerialized);
		zDO2.Set(CompanionSetup.OwnerHash, data.OwnerId);
		zDO2.Set(CompanionSetup.NameHash, data.Name);
		zDO2.Set(CompanionSetup.TombstoneIdHash, data.TombstoneId);
		zDO2.Set(CompanionSetup.FollowHash, data.Follow);
		zDO2.Set(CompanionSetup.ActionModeHash, data.ActionMode);
		zDO2.Set(CompanionSetup.ActionModeSchemaHash, data.ActionModeSchema);
		zDO2.Set(CompanionSetup.StayHomeHash, data.StayHome);
		zDO2.Set(CompanionSetup.AutoPickupHash, data.AutoPickup);
		zDO2.Set(CompanionSetup.WanderHash, data.Wander);
		zDO2.Set(CompanionSetup.IsCommandableHash, data.IsCommandable ? 1 : 0);
		zDO2.Set(CompanionSetup.FormationSlotHash, data.FormationSlot);
		if (data.HasHomePos)
		{
			zDO2.Set(CompanionSetup.HomePosHash, data.HomePos);
			zDO2.Set(CompanionSetup.HomePosSetHash, value: true);
		}
		if (!string.IsNullOrEmpty(data.SkillsSerialized))
		{
			zDO2.Set(CompanionSkills.SkillsHash, data.SkillsSerialized);
		}
		zDO2.Persistent = true;
		CompanionAppearance a = CompanionAppearance.Deserialize(data.AppearanceSerialized);
		CompanionSetup component3 = gameObject2.GetComponent<CompanionSetup>();
		if (component3 != null)
		{
			component3.ApplyAppearance(a);
		}
		Character component4 = gameObject2.GetComponent<Character>();
		if (component4 != null && !string.IsNullOrEmpty(data.Name))
		{
			component4.m_name = data.Name;
		}
		CompanionAI component5 = gameObject2.GetComponent<CompanionAI>();
		if (data.HasHomePos && component5 != null)
		{
			component5.SetPatrolPointAt(data.HomePos);
		}
		if (data.StayHome && data.HasHomePos && component5 != null)
		{
			component5.SetFollowTarget(null);
			component5.SetPatrolPointAt(data.HomePos);
		}
		else if (data.Follow && Player.m_localPlayer != null)
		{
			component5?.SetFollowTarget(Player.m_localPlayer.gameObject);
		}
		if (component5 != null)
		{
			component5.FreezeTimer = 5f;
		}
		string arg = (data.HasHomePos ? "home" : "world spawn");
		CompanionsPlugin.Log.LogInfo($"[CompanionManager] Respawned \"{data.Name}\" at {arg} {vector2:F1} — " + $"tombstoneId={data.TombstoneId}");
		string text3 = (data.HasHomePos ? ModLocalization.Loc("hc_msg_location_home") : ModLocalization.Loc("hc_msg_location_spawn"));
		MessageHud.instance?.ShowMessage(MessageHud.MessageType.Center, ModLocalization.LocFmt("hc_msg_returned", data.Name, text3));
	}

	internal static Vector3 GetWorldSpawnPoint()
	{
		if (ZoneSystem.instance != null && ZoneSystem.instance.GetLocationIcon(Game.instance.m_StartLocation, out var pos))
		{
			return pos + Vector3.up * 2f;
		}
		return Vector3.up * 2f;
	}

	internal static Vector3 FindSpawnPosition(Vector3 origin, float radius)
	{
		for (int i = 0; i < 20; i++)
		{
			Vector2 vector = Random.insideUnitCircle * radius;
			Vector3 vector2 = origin + new Vector3(vector.x, 0f, vector.y);
			if (ZoneSystem.instance != null && ZoneSystem.instance.FindFloor(vector2, out var height))
			{
				vector2.y = height;
				return vector2;
			}
		}
		return origin + Vector3.right * 2f;
	}

	public static void SpawnStarterCompanion()
	{
		if (!ModConfig.SpawnStarterCompanion.Value)
		{
			CompanionsPlugin.Log.LogDebug("[CompanionManager] Starter companion disabled in config");
			return;
		}
		Player localPlayer = Player.m_localPlayer;
		if (localPlayer == null || ZNet.instance == null)
		{
			return;
		}
		string key = $"HC_StarterCompanion_{ZNet.instance.GetWorldUID()}";
		StarterFix.ResetIfRequested();
		if (localPlayer.m_customData.ContainsKey(key))
		{
			CompanionsPlugin.Log.LogDebug("[CompanionManager] Starter companion already spawned for this world");
			return;
		}
		string text = localPlayer.GetPlayerID().ToString();
		foreach (CompanionSetup allCompanion in CompanionSetup.AllCompanions)
		{
			ZDO zDO = allCompanion.GetComponent<ZNetView>()?.GetZDO();
			if (zDO != null && zDO.GetString(CompanionSetup.OwnerHash) == text)
			{
				CompanionsPlugin.Log.LogInfo("[CompanionManager] Found existing companion in scene — marking world key and skipping panel");
				localPlayer.m_customData[key] = "1";
				return;
			}
		}
		CompanionsPlugin.Log.LogInfo("[CompanionManager] First spawn in world — showing starter companion panel");
		StarterCompanionPanel.ShowPanel();
	}

	public static void MHR_StarterFallbackTick()
	{
		Game instance = Game.instance;
		Player localPlayer = Player.m_localPlayer;
		if (instance == null || (object)s_mhrStarterGame == instance)
		{
			return;
		}
		if ((object)s_mhrSeenGame != instance)
		{
			s_mhrSeenGame = instance;
			s_mhrSeenAt = 0f;
		}
		if (!(localPlayer == null) && !localPlayer.IsDead() && !localPlayer.InIntro())
		{
			if (s_mhrSeenAt <= 0f)
			{
				s_mhrSeenAt = Time.time;
			}
			else if (Time.time - s_mhrSeenAt >= 5f)
			{
				CompanionsPlugin.Log.LogInfo("[CompanionManager] The game's first-spawn event didn't reach us (another mod may have skipped it) - checking for the starter companion now");
				MHR_StarterOnce();
			}
		}
	}

	public static void SpawnStarterWithAppearance(CompanionAppearance appearance)
	{
		SpawnStarterWithAppearance(appearance, null);
	}

	/// <summary>
	/// The start of the game: the companion the player designed in the starter panel, with the AI profile they chose
	/// there (Fase 3.10). The draft is written to the new companion's ZDO by <see cref="SpawnCompanion"/> - the same
	/// place the name and the appearance are written - and a null draft is the plain spawn (everything on the
	/// <c>[IA]</c> defaults), which is what the old single-argument overload does.
	/// </summary>
	internal static void SpawnStarterWithAppearance(CompanionAppearance appearance, CompanionAiDraft aiDraft)
	{
		Player localPlayer = Player.m_localPlayer;
		if (!(localPlayer == null) && SpawnCompanion(appearance, CompanionTierData.Companion, aiDraft))
		{
			StarterFix.MarkSpawned();
			CompanionsPlugin.Log.LogInfo("[CompanionManager] Starter companion spawned with custom appearance!");
			MessageHud.instance?.ShowMessage(MessageHud.MessageType.Center, ModLocalization.Loc("hc_msg_starter_joined"));
		}
	}
}
