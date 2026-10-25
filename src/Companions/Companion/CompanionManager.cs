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

		/// <summary>Fase 3.19 (auditoria): the companion's permanent id (HC_CompanionId), when the caller knows it.
		/// It is what tells two companions with the same name apart when a respawn checks whether it already happened.</summary>
		public string CompanionId;

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

	/// <summary>
	/// Fase 3.19: the <c>[General] MaxCompanions</c> ceiling (10 by default). Counted on the roster - the companions of
	/// this player in this world, <b>alive, dead and lost</b> - so dying does not free a place (the tombstone brings the
	/// same companion back, it does not create one). The Shrine keeps being able to recover the dead ones: refusing
	/// here only blocks creating a NEW companion.
	///
	/// <para>When the ceiling is reached the player is told (HUD + chat) and nothing is spawned; every creation path
	/// goes through <see cref="SpawnCompanion"/>, which is where this is called.</para>
	/// </summary>
	internal static bool CanAddCompanion()
	{
		int limit = ModConfig.MaxCompanions != null ? ModConfig.MaxCompanions.Value : 10;
		if (limit <= 0)
		{
			return true;
		}
		int count = MHR_Roster.CountMine();
		if (count < limit)
		{
			return true;
		}
		CompanionsPlugin.Log?.LogWarning($"[Spawn] Refused: the player already has {count} companion(s) in this world and "
			+ $"[General] MaxCompanions is {limit} - nothing was created (dismiss one, or recover a tombstone)");
		CompanionChatAI.Announce(ModLocalization.LocFmt("hc_msg_companion_limit", count, limit));
		return false;
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
		// Fase 3.19: the ceiling counts every companion of this player (alive, dead and lost) - see CanAddCompanion.
		// It is checked here because every creation path goes through this method (the Trader's purchase, the AI
		// panel's summoner and the starter companion).
		if (!CanAddCompanion())
		{
			return false;
		}
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
		// Fase 3.13: the starter panel has a name field now, and a name typed there wins (trimmed and limited by
		// CompanionAiDraft.CleanName). Nothing typed = the default above, byte for byte - the Trader's purchase and
		// the AI panel's summoner (no draft) keep the old behaviour.
		string typedName = CompanionAiDraft.CleanName(aiDraft != null ? aiDraft.Name : null);
		string companionName = typedName.Length > 0 ? typedName : NextDefaultName(def);
		zDO.Set(CompanionSetup.NameHash, companionName);
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
						: " | personality: default ([AI])"));
			}
		}
		CompanionsPlugin.Log.LogInfo($"[CompanionManager] Name of the new companion: \"{companionName}\" "
			+ (typedName.Length > 0 ? "(typed in the starter panel)" : "(default of the tier)"));
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
	/// Fase 3.14: changes the TIER of a companion that already exists. A tier is a prefab, so there is no way to keep
	/// the same object: this respawns the companion as the new prefab, on the same spot, and carries everything that
	/// makes it <b>that</b> companion across:
	///
	/// <list type="bullet">
	/// <item><b>the name</b> (<c>HC_Name</c>, typed by the player) and the <b>appearance</b> (<c>HC_Appearance</c>);</item>
	/// <item><b>the whole inventory</b> (serialized with <c>Inventory.Save</c>/<c>Load</c>, the same pair the roster and
	/// the tombstone use) and the <b>equipment</b> that was in hand/worn (re-equipped by grid position after the load);</item>
	/// <item><b>the AI profile</b> of Fase 3.10 (voice + personality keys), read/written through
	/// <see cref="CompanionProfile"/>'s hashes;</item>
	/// <item><b>the companion id</b> (<c>HC_CompanionId</c>), the roster generation (<c>HC_Gen</c>) and the tombstone id,
	/// so the roster, the Shrine and the bed claim keep pointing at the same companion;</item>
	/// <item>owner, action mode + schema, follow / follow-paused, stay home, auto-pickup, wander, commandable, combat
	/// stance, formation slot, home position + flag, home bed position + flag, the order target of Fase 3.2c and the
	/// <b>skills</b> (<see cref="CompanionSkills"/> serialized string).</item>
	/// </list>
	///
	/// <para>What does NOT survive (it is not part of the ZDO): the conversation memory of the session (it is cleared
	/// when the old object is destroyed), the job's current step (the mode is kept, so the job starts over from a new
	/// scan), a cart being pulled, a tombstone recovery in progress and the combat target. A tier that cannot wear armor
	/// (the dverger ones) keeps the armor pieces in its pack instead of wearing them - that is the tier, not this
	/// method.</para>
	///
	/// <para>Multiplayer: only the owner of the companion runs this (the ZDO is claimed first, like an order). The old
	/// object is marked stale before it is destroyed, so <see cref="MHR_DeleteGuard"/> lets it go and the roster does
	/// not treat it as an old copy of itself; the new object is born with the old <c>HC_Owner</c>. Every step is logged
	/// under <c>[Tier]</c>, and <c>null</c> means nothing was changed (the old companion is still there).</para>
	/// </summary>
	internal static CompanionSetup ChangeTier(CompanionSetup companion, CompanionTierDef def)
	{
		if (companion == null || def == null)
		{
			return null;
		}
		ZNetView nv = companion.GetComponent<ZNetView>();
		ZDO old = (nv != null && nv.IsValid()) ? nv.GetZDO() : null;
		Player player = Player.m_localPlayer;
		if (old == null || player == null)
		{
			CompanionsPlugin.Log?.LogWarning("[Tier] Change cancelled: the companion has no ZDO (or there is no local player)");
			return null;
		}
		if (!MHR_Owner.IsMine(companion))
		{
			CompanionsPlugin.Log?.LogWarning("[Tier] Change cancelled: that companion isn't yours");
			return null;
		}
		// a dead companion is under the tombstone/respawn flow: spawning a live copy of it now would leave the tombstone
		// able to bring ANOTHER copy back later, so the swap waits for the resurrection
		Character oldCharacter = companion.GetComponent<Character>();
		if (oldCharacter != null && oldCharacter.GetHealth() <= 0f)
		{
			CompanionsPlugin.Log?.LogWarning("[Tier] Change cancelled: the companion is dead - bring it back first");
			return null;
		}
		if (!nv.IsOwner())
		{
			nv.ClaimOwnership();
			if (!nv.IsOwner())
			{
				CompanionsPlugin.Log?.LogWarning("[Tier] Change cancelled: this game doesn't own the companion's data yet (try again in a moment)");
				return null;
			}
		}
		string oldName = old.GetString(CompanionSetup.NameHash, ModLocalization.Loc("hc_msg_name_default"));
		CompanionTierDef oldDef = TierOf(old.GetPrefab());
		if (old.GetPrefab() == def.PrefabName.GetStableHashCode())
		{
			CompanionsPlugin.Log?.LogInfo($"[Tier] \"{oldName}\" is already \"{CompanionTierData.LocalName(def)}\" - nothing was changed");
			return companion;
		}
		GameObject prefab = ZNetScene.instance?.GetPrefab(def.PrefabName);
		if (prefab == null)
		{
			CompanionsPlugin.Log?.LogError("[Tier] Change cancelled: prefab not found: " + def.PrefabName);
			return null;
		}

		// ---- everything the new companion has to carry over, read while the old one is still alive ----
		string appearance = old.GetString(CompanionSetup.AppearanceHash);
		string owner = old.GetString(CompanionSetup.OwnerHash);
		int mode = old.GetInt(CompanionSetup.ActionModeHash);
		int schema = old.GetInt(CompanionSetup.ActionModeSchemaHash);
		bool follow = old.GetBool(CompanionSetup.FollowHash, defaultValue: true);
		bool followPaused = old.GetBool(CompanionTombstoneResurrect.FollowPausedByJobHash);
		bool stayHome = old.GetBool(CompanionSetup.StayHomeHash);
		bool autoPickup = old.GetBool(CompanionSetup.AutoPickupHash, defaultValue: true);
		bool wander = old.GetBool(CompanionSetup.WanderHash);
		int commandable = old.GetInt(CompanionSetup.IsCommandableHash, 1);
		int formation = old.GetInt(CompanionSetup.FormationSlotHash, -1);
		Vector3 home = old.GetVec3(CompanionSetup.HomePosHash, Vector3.zero);
		bool hasHome = old.GetBool(CompanionSetup.HomePosSetHash) || home != Vector3.zero;
		int stance = old.GetInt(CompanionSetup.CombatStanceHash);
		bool hasBed = old.GetBool(CompanionSetup.HasHomeBedHash);
		Vector3 bed = old.GetVec3(CompanionSetup.HomeBedPosHash, Vector3.zero);
		string skills = companion.GetComponent<CompanionSkills>()?.SerializeSkills() ?? old.GetString(CompanionSkills.SkillsHash);
		string companionId = companion.MHR_EnsureCompanionId();
		int gen = old.GetInt(MHR_Roster.GenHash);
		long tombstoneId = old.GetLong(CompanionSetup.TombstoneIdHash, 0L);
		int cap = old.GetInt(CompanionSetup.OrderCapHash, 0);
		int capMode = old.GetInt(CompanionSetup.OrderCapModeHash, 0);
		string capItem = old.GetString(CompanionSetup.OrderCapItemHash, "");
		int aiVoice = old.GetInt(CompanionProfile.VoiceHash, 0);
		bool personaSet = old.GetBool(CompanionProfile.PersonaSetHash, false);
		string personaPreset = old.GetString(CompanionProfile.PresetHash, "");
		string personaText = old.GetString(CompanionProfile.PersonaTextHash, "");

		// the inventory and what was equipped, by grid position (Inventory.Load puts every item back where it was)
		string inventoryData = "";
		List<Vector2i> equipped = new List<Vector2i>();
		Humanoid oldHumanoid = companion.GetComponent<Humanoid>();
		Inventory oldInventory = oldHumanoid != null ? oldHumanoid.GetInventory() : null;
		int items = 0;
		if (oldInventory != null)
		{
			try
			{
				foreach (ItemDrop.ItemData item in oldInventory.GetAllItems())
				{
					if (item == null)
					{
						continue;
					}
					items++;
					if (item.m_equipped)
					{
						equipped.Add(item.m_gridPos);
					}
				}
				ZPackage package = new ZPackage();
				oldInventory.Save(package);
				inventoryData = package.GetBase64();
			}
			catch (System.Exception ex)
			{
				// without a safe copy of the pack nothing else matters: the old companion stays as it is
				CompanionsPlugin.Log?.LogError("[Tier] Change cancelled: the inventory could not be read: " + ex);
				return null;
			}
		}
		Vector3 position = companion.transform.position;
		Quaternion rotation = companion.transform.rotation;

		// ---- the new companion is born first, so a failure below can still leave the old one untouched ----
		GameObject spawned;
		try
		{
			spawned = Object.Instantiate(prefab, position, rotation);
		}
		catch (System.Exception ex)
		{
			CompanionsPlugin.Log?.LogError("[Tier] Change cancelled: the new companion could not be spawned: " + ex);
			return null;
		}
		ZNetView newNv = spawned.GetComponent<ZNetView>();
		ZDO zdo = (newNv != null && newNv.IsValid()) ? newNv.GetZDO() : null;
		if (zdo == null)
		{
			CompanionsPlugin.Log?.LogError("[Tier] Change cancelled: the new companion has no ZDO");
			Object.Destroy(spawned);
			return null;
		}
		int equippedCount = 0;
		try
		{
			zdo.Set(CompanionSetup.NameHash, oldName);
			zdo.Set(CompanionSetup.AppearanceHash, appearance);
			zdo.Set(CompanionSetup.OwnerHash, owner);
			zdo.Set(CompanionSetup.ActionModeHash, mode);
			zdo.Set(CompanionSetup.ActionModeSchemaHash, schema);
			zdo.Set(CompanionSetup.FollowHash, follow);
			zdo.Set(CompanionTombstoneResurrect.FollowPausedByJobHash, followPaused);
			zdo.Set(CompanionSetup.StayHomeHash, stayHome);
			zdo.Set(CompanionSetup.AutoPickupHash, autoPickup);
			zdo.Set(CompanionSetup.WanderHash, wander);
			zdo.Set(CompanionSetup.IsCommandableHash, commandable);
			if (formation >= 0)
			{
				zdo.Set(CompanionSetup.FormationSlotHash, formation);
			}
			if (hasHome)
			{
				zdo.Set(CompanionSetup.HomePosHash, home);
				zdo.Set(CompanionSetup.HomePosSetHash, value: true);
			}
			zdo.Set(CompanionSetup.CombatStanceHash, stance);
			zdo.Set(CompanionSetup.HasHomeBedHash, hasBed);
			if (hasBed)
			{
				zdo.Set(CompanionSetup.HomeBedPosHash, bed);
			}
			if (!string.IsNullOrEmpty(skills))
			{
				zdo.Set(CompanionSkills.SkillsHash, skills);
			}
			if (!string.IsNullOrEmpty(companionId))
			{
				// the id is what the roster/Shrine/bed claim know this companion by - it must survive the swap
				zdo.Set(CompanionSetup.MHR_CompanionIdHash, companionId);
			}
			zdo.Set(MHR_Roster.GenHash, gen);
			if (tombstoneId != 0L)
			{
				zdo.Set(CompanionSetup.TombstoneIdHash, tombstoneId);
			}
			if (cap > 0)
			{
				// Fase 3.2c: a "colete 10 pedras" that was running keeps its target
				zdo.Set(CompanionSetup.OrderCapHash, cap);
				zdo.Set(CompanionSetup.OrderCapModeHash, capMode);
				zdo.Set(CompanionSetup.OrderCapItemHash, capItem);
			}
			// Fase 3.10: the AI profile of this companion (voice + personality), or "follow the [IA] default" when unused
			if (aiVoice > 0)
			{
				zdo.Set(CompanionProfile.VoiceHash, aiVoice);
			}
			if (personaSet)
			{
				zdo.Set(CompanionProfile.PresetHash, personaPreset);
				zdo.Set(CompanionProfile.PersonaTextHash, personaText);
				zdo.Set(CompanionProfile.PersonaSetHash, true);
			}
			// Fase 3.19 (auditoria): the companion's own state that is not in the list above. It was silently lost on
			// a tier change - the cook forgot what it had already produced for the job (and ate its food), the
			// borrowed tools never came back and the stamina started over.
			CopyCarriedKeys(old, zdo);
			zdo.Persistent = true;

			// auto-equip is held off until the pack is in place: OnInventoryChanged runs from inside Inventory.Load,
			// and it would both fight our loadout and leave flags behind (see the flag handling below)
			CompanionSetup newSetup = spawned.GetComponent<CompanionSetup>();
			if (newSetup != null)
			{
				newSetup.SuppressAutoEquip = true;
			}
			Humanoid newHumanoid = spawned.GetComponent<Humanoid>();
			Inventory newInventory = newHumanoid != null ? newHumanoid.GetInventory() : null;
			if (newInventory != null && !string.IsNullOrEmpty(inventoryData))
			{
				newInventory.Load(new ZPackage(inventoryData));
				// Fase 3.16 (auditoria): the serialized items bring their own m_equipped flags, and a flag left set on
				// something that is NOT in a hand/slot makes MHR_ToolKeeper/MHR_GatherRules skip that item for good
				// (the companion would stop equipping its pickaxe/axe after the swap). Clear them all first, the same
				// thing MHR_Roster does when it restores a pack, and only the items below get the flag back.
				foreach (ItemDrop.ItemData item in newInventory.GetAllItems())
				{
					if (item != null)
					{
						item.m_equipped = false;
					}
				}
				// then put back in hand/worn exactly what was equipped (by grid position, which Load restores)
				// A tier that cannot wear armor (the dverger ones) keeps the armor pieces in the pack - that is what
				// the confirmation says, and equipping them would only put invisible gear in its slots.
				bool canWearArmor = def.CanWearArmor;
				for (int i = 0; i < equipped.Count; i++)
				{
					ItemDrop.ItemData item = newInventory.GetItemAt(equipped[i].x, equipped[i].y);
					if (item == null || item.m_shared == null)
					{
						continue;
					}
					if (!canWearArmor && IsArmorPiece(item))
					{
						continue;
					}
					if (newHumanoid.EquipItem(item, triggerEquipEffects: false))
					{
						equippedCount++;
					}
				}
				// and nothing else may stay flagged: EquipItem only flags what it took, but a slot the new prefab
				// refused would otherwise keep the flag from the loop above
				foreach (ItemDrop.ItemData item in newInventory.GetAllItems())
				{
					if (item != null && !newHumanoid.IsItemEquiped(item))
					{
						item.m_equipped = false;
					}
				}
				// no extra m_onChanged here on purpose: the inventory is already whole and the equipment is decided;
				// the mod's automatic equipment runs on the next real inventory change, as it always does
			}
			if (newSetup != null)
			{
				newSetup.SuppressAutoEquip = false;
				CompanionAppearance applied = CompanionAppearance.Deserialize(appearance);
				newSetup.ApplyAppearance(applied);
				// the pack is already in place: the roster may snapshot this companion without waiting the load settle
				newSetup._mhrAwakeTime = Time.time - 11f;
			}
			Character character = spawned.GetComponent<Character>();
			if (character != null)
			{
				character.m_name = oldName;
			}
			CompanionAI ai = spawned.GetComponent<CompanionAI>();
			if (ai != null)
			{
				if (hasHome)
				{
					ai.SetPatrolPointAt(home);
				}
				if (stayHome && hasHome)
				{
					ai.SetFollowTarget(null);
				}
				else if (follow)
				{
					ai.SetFollowTarget(player.gameObject);
				}
			}
		}
		catch (System.Exception ex)
		{
			CompanionsPlugin.Log?.LogError("[Tier] Change cancelled: the new companion could not be filled with the old data: " + ex);
			// Fase 3.16 (auditoria): the ZDO is already Persistent, so Object.Destroy would leave it behind. Tear the
			// half-built companion down the same way a successful swap removes the old one: mark it stale (that is what
			// MHR_DeleteGuard looks at) and go through ZNetView.Destroy, i.e. ZNetScene.Destroy.
			CompanionSetup broken = spawned.GetComponent<CompanionSetup>();
			if (broken != null)
			{
				broken._mhrStale = true;
			}
			if (newNv != null && newNv.IsValid())
			{
				newNv.Destroy();
			}
			else
			{
				Object.Destroy(spawned);
			}
			return null;
		}

		// ---- the old object goes, without a tombstone and without being mistaken for an old copy of the new one ----
		companion._mhrStale = true;
		Humanoid humanoid = companion.GetComponent<Humanoid>();
		if (humanoid != null)
		{
			humanoid.m_onDeath = null;
		}
		companion.HidePinFor(5f);
		nv.Destroy();

		CompanionsPlugin.Log?.LogInfo($"[Tier] \"{oldName}\" respawned as \"{CompanionTierData.LocalName(def)}\" (was \""
			+ (oldDef != null ? CompanionTierData.LocalName(oldDef) : "?") + $"\") at {position:F1} - kept: name, appearance, "
			+ $"owner, mode={mode}, follow={follow}, stayHome={stayHome}, autoPickup={autoPickup}, wander={wander}, "
			+ $"commandable={commandable}, stance={stance}, formation={formation}, home={(hasHome ? "yes" : "no")}, "
			+ $"bed={(hasBed ? "yes" : "no")}, skills={(string.IsNullOrEmpty(skills) ? "none" : "yes")}, "
			+ $"companionId={(string.IsNullOrEmpty(companionId) ? "none" : companionId)}, gen={gen}, "
			+ $"orderCap={(cap > 0 ? cap + " " + capItem : "none")}, aiVoice={(aiVoice > 0 ? aiVoice.ToString() : "default")}, "
			+ $"personality={(personaSet ? "\"" + personaPreset + "\"" : "default ([AI])")}, "
			+ $"inventory={items} item(s), equipped={equippedCount}");
		return spawned.GetComponent<CompanionSetup>();
	}

	/// <summary>
	/// Fase 3.19 (auditoria): copies the companion's own <c>HC_*</c> state that a tier change used to forget. The list
	/// is explicit on purpose: when a new key is added to a companion, it belongs here (or in the block in
	/// <see cref="ChangeTier"/>, when it needs more than a copy).
	/// <list type="bullet">
	/// <item><b>HC_CookLedger</b> (string) - what the cook already produced for its job; without it the job starts over
	/// and the companion eats the food it was supposed to cook;</item>
	/// <item><b>HC_Borrowed</b> (string) - the tools it borrowed from nearby chests; without it they are never returned;</item>
	/// <item><b>HC_Stamina</b> (float) - a tired companion came back with a full bar.</item>
	/// </list>
	/// </summary>
	private static void CopyCarriedKeys(ZDO from, ZDO to)
	{
		if (from == null || to == null)
		{
			return;
		}
		for (int i = 0; i < CarriedStringKeys.Length; i++)
		{
			int hash = CarriedStringKeys[i];
			string value = from.GetString(hash, "");
			if (!string.IsNullOrEmpty(value))
			{
				to.Set(hash, value);
			}
		}
		for (int i = 0; i < CarriedFloatKeys.Length; i++)
		{
			int hash = CarriedFloatKeys[i];
			float value = from.GetFloat(hash, float.NaN);
			if (!float.IsNaN(value))
			{
				to.Set(hash, value);
			}
		}
	}

	/// <summary>String keys of <see cref="CopyCarriedKeys"/> - written by their owners, copied here verbatim.</summary>
	private static readonly int[] CarriedStringKeys = new int[2]
	{
		"HC_CookLedger".GetStableHashCode(),   // Jobs/CookController.cs
		"HC_Borrowed".GetStableHashCode()      // Jobs/MHR_ToolKeeper.cs
	};

	/// <summary>Float keys of <see cref="CopyCarriedKeys"/>.</summary>
	private static readonly int[] CarriedFloatKeys = new int[1]
	{
		CompanionSetup.StaminaHash   // Companion/CompanionStamina.cs
	};

	/// <summary>The tier a prefab hash belongs to (the same table the UI lists), or null when it isn't one of ours.</summary>
	internal static CompanionTierDef TierOf(int prefabHash)
	{
		CompanionTierDef[] defs = CompanionTierData.All;
		for (int i = 0; i < defs.Length; i++)
		{
			if (defs[i].PrefabName.GetStableHashCode() == prefabHash)
			{
				return defs[i];
			}
		}
		return null;
	}

	/// <summary>Is this one of the armor slots (the pieces a tier without <c>CanWearArmor</c> cannot wear)?</summary>
	private static bool IsArmorPiece(ItemDrop.ItemData item)
	{
		switch (item.m_shared.m_itemType)
		{
		case ItemDrop.ItemData.ItemType.Chest:
		case ItemDrop.ItemData.ItemType.Legs:
		case ItemDrop.ItemData.ItemType.Helmet:
		case ItemDrop.ItemData.ItemType.Shoulder:
			return true;
		default:
			return false;
		}
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

	/// <summary>
	/// Fase 3.19 (auditoria): is this ZDO the companion the respawn data is about? The permanent id answers whenever
	/// both sides have one, then the tombstone it came from; the name is only the fallback of an old save that has
	/// neither. Comparing names alone blocked the second of two companions that share a name.
	/// </summary>
	/// <param name="companionId">Id of the companion being brought back (""/null when the caller doesn't know it).</param>
	/// <param name="tombstoneId">The tombstone that respawned it (0 when there is none).</param>
	internal static bool IsSameCompanion(ZDO zdo, string ownerId, string name, string companionId, long tombstoneId)
	{
		if (zdo == null || zdo.GetString(CompanionSetup.OwnerHash) != ownerId)
		{
			return false;
		}
		string otherId = zdo.GetString(CompanionSetup.MHR_CompanionIdHash, "");
		if (!string.IsNullOrEmpty(companionId) && !string.IsNullOrEmpty(otherId))
		{
			return otherId == companionId;
		}
		long otherTomb = zdo.GetLong(CompanionSetup.TombstoneIdHash, 0L);
		if (tombstoneId != 0L && otherTomb != 0L)
		{
			return otherTomb == tombstoneId;
		}
		return zdo.GetString(CompanionSetup.NameHash) == name;
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
			// Fase 3.19 (auditoria): the comparison used owner+name, so a second companion with the same name could
			// never come back (the first one "already exists"). See IsSameCompanion.
			if (IsSameCompanion(zDO, data.OwnerId, data.Name, data.CompanionId, data.TombstoneId))
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
	/// <c>[AI]</c> defaults), which is what the old single-argument overload does.
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
