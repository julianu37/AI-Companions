using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace Companions;

internal static class ModConfig
{
	internal static readonly Dictionary<string, List<ConfigEntryBase>> Categories = new Dictionary<string, List<ConfigEntryBase>>();

	internal static readonly List<string> CategoryOrder = new List<string>();

	internal static ConfigEntry<int> CompanionPrice;

	/// <summary>Fase 3.19: the ceiling of companions per player in a world (see CompanionManager.CanAddCompanion).</summary>
	internal static ConfigEntry<int> MaxCompanions;
	internal static ConfigEntry<bool> RecoverUnsummoned;

	internal static ConfigEntry<bool> SpawnStarterCompanion;

	internal static ConfigEntry<bool> ResetStarterFlag;

	internal static ConfigEntry<bool> ShowCompanionRoster;

	internal static ConfigEntry<bool> RecoverLostCompanion;

	internal static ConfigEntry<float> RosterSaveInterval;

	internal static ConfigEntry<bool> ShrineBuildable;

	internal static ConfigEntry<string> ShrineRecipe;

	internal static ConfigEntry<string> ShrineModel;

	internal static ConfigEntry<float> MaxCarryWeight;

	internal static ConfigEntry<float> BaseHealth;

	internal static ConfigEntry<float> BaseStamina;

	internal static ConfigEntry<int> MaxFoodSlots;


	internal static ConfigEntry<float> FollowTeleportDistance;

	internal static ConfigEntry<bool> ShowMinimapHudPanel;

	internal static ConfigEntry<float> HealthRetreatPct;

	internal static ConfigEntry<float> FleeHealthPct;

	internal static ConfigEntry<float> WeaponSwitchMargin;

	internal static ConfigEntry<bool> UseTorchOpener;

	internal static ConfigEntry<bool> EngageOnNotice;

	internal static ConfigEntry<float> TorchOpenerTimeout;

	internal static ConfigEntry<float> TorchOpenerCooldown;

	internal static ConfigEntry<float> TorchOpenerMinTargetHealth;

	internal static ConfigEntry<float> WaterStaminaFleeThreshold;

	internal static ConfigEntry<bool> ShowOverheadBars;

	internal static ConfigEntry<bool> ShowStatus;

	internal static ConfigEntry<int> ShieldMultiAttackerCount;

	internal static ConfigEntry<float> ParryChance;

	internal static ConfigEntry<float> ParryChanceMax;

	internal static ConfigEntry<float> BlockReactionTime;

	internal static ConfigEntry<bool> LogPlayerCombat;

	internal static ConfigEntry<bool> FightLikePlayer;

	internal static ConfigEntry<int> CombatComboHits;

	internal static ConfigEntry<float> CombatComboPause;

	internal static ConfigEntry<float> CombatStaminaReserve;

	internal static ConfigEntry<float> CombatStaminaResume;

	internal static ConfigEntry<float> CombatRecoverTime;

	internal static ConfigEntry<float> StepAwayChance;

	internal static ConfigEntry<int> CombatGroupSize;

	internal static ConfigEntry<float> CombatGroupRange;

	internal static ConfigEntry<float> CombatFlankAngle;

	internal static ConfigEntry<int> CombatFinishHits;

	internal static ConfigEntry<bool> CombatSpacing;

	internal static ConfigEntry<bool> CombatFlank;

	internal static ConfigEntry<float> CombatSpacingMargin;

	internal static ConfigEntry<float> CombatSpacingStepChance;




	internal static ConfigEntry<float> AttackCooldown;

	internal static ConfigEntry<float> MeleeTooClose;

	internal static ConfigEntry<float> MeleeReachMargin;




	internal static ConfigEntry<float> BowSwitchDistance;

	internal static ConfigEntry<float> MeleeSwitchDistance;











	internal static ConfigEntry<float> FormationOffset;

	internal static ConfigEntry<float> FormationCatchupDist;





	internal static ConfigEntry<float> AutoPickupRange;

	internal static ConfigEntry<float> TombstoneScanInterval;

	internal static ConfigEntry<float> TombstoneNavTimeout;

	// [Advanced] - o diagnostico de UI do mod ([UiDiag]): os alvos de clique de dentro de um painel que nao sao
	// controles, as camadas ativas da cena e quem, acima do painel, cobre a coluna de controles. Fica desligado por
	// padrao porque o log dele e grande (uma sessao de teste deu ~7 KB em 6 linhas, e 3 delas eram falsos positivos
	// do painel da IA) e so serve para investigar clique/UI estranha. Ver UI/ModUiAudit.cs.
	internal static ConfigEntry<bool> UiDiag;

	internal static ConfigEntry<bool> AutoRespawnEnabled;

	internal static ConfigEntry<float> AutoRespawnDelaySeconds;

	internal static ConfigEntry<bool> AutoRespawnAtWorldSpawnWhenNoBed;

	internal static ConfigEntry<bool> AutoRespawnAtPlayer;

	internal static ConfigEntry<int> DeathItemRule;

	internal static ConfigEntry<float> TombstoneRecoveryRange;

	internal static ConfigEntry<string> RespawnStance;

	internal static ConfigEntry<float> RecoverThreatRange;

	internal static ConfigEntry<int> RecoverMaxMobs;

	internal static ConfigEntry<bool> SleepAtNight;

	internal static ConfigEntry<bool> SleepWhenPlayerSleeps;

	internal static ConfigEntry<float> BedRange;

	internal static ConfigEntry<float> RecallRange;

	internal static ConfigEntry<float> SuperRecallHoldTime;

	internal static ConfigEntry<KeyCode> PassiveKey;

	internal static ConfigEntry<KeyCode> BalancedKey;

	internal static ConfigEntry<KeyCode> RangedKey;

	internal static ConfigEntry<KeyCode> MeleeKey;

	internal static ConfigEntry<bool> ProtectCompanions;

	internal static ConfigEntry<int> SeedReserve;

	internal static ConfigEntry<int> SeedTarget;

	internal static ConfigEntry<int> SeedCarry;

	internal static ConfigEntry<float> WorkRunDistance;

	internal static ConfigEntry<float> HomeTripRange;

	internal static ConfigEntry<bool> FollowStuckTeleport;

	internal static ConfigEntry<bool> CompanionNavMesh;

	internal static ConfigEntry<float> CookRange;

	internal static ConfigEntry<int> CookIngredientReserve;

	internal static ConfigEntry<int> CookFoodTarget;

	internal static ConfigEntry<int> CookBatchSize;

	internal static ConfigEntry<string> CookFoods;

	internal static ConfigEntry<bool> CookStoreWithCookedFood;

	internal static ConfigEntry<int> CookMeadBaseTarget;

	internal static ConfigEntry<int> CookMeadTarget;

	internal static ConfigEntry<string> CookMeadFocus;

	internal static ConfigEntry<bool> CookCauldron;

	internal static ConfigEntry<bool> CookPrepTable;

	internal static ConfigEntry<bool> CookMeadKetill;

	internal static ConfigEntry<bool> CookCookingStation;

	internal static ConfigEntry<bool> CookIronCookingStation;

	internal static ConfigEntry<bool> CookOven;

	internal static ConfigEntry<bool> CookFermenter;

	internal static ConfigEntry<bool> CookBeehives;

	internal static ConfigEntry<bool> CookOtherStations;

	internal static ConfigEntry<bool> StatusEffectsOnRegen;

	internal static ConfigEntry<float> DryDetour;

	internal static ConfigEntry<float> JobSwim;

	internal static ConfigEntry<float> MaxDrop;

	internal static ConfigEntry<float> StuckDrop;

	internal static ConfigEntry<float> JumpGap;

	internal static ConfigEntry<float> JumpWaterDepth;

	internal static ConfigEntry<float> JumpWaterWidth;

	internal static ConfigEntry<string> HuntPrey;

	internal static ConfigEntry<bool> HuntSneak;

	internal static ConfigEntry<bool> HuntThrowSpear;

	internal static ConfigEntry<float> HuntThrowRange;

	internal static ConfigEntry<float> HuntThrowStandoff;

	internal static ConfigEntry<float> HuntSneakRestStamina;

	internal static ConfigEntry<float> HuntSneakFrom;

	internal static ConfigEntry<float> HuntSwitchCloserBy;

	internal static ConfigEntry<float> HuntSwitchShare;

	internal static ConfigEntry<float> HuntSneakResumeStamina;

	internal static ConfigEntry<float> HuntChaseRunStamina;

	internal static ConfigEntry<bool> HuntBirds;

	internal static ConfigEntry<int> HuntFeatherTarget;

	internal static ConfigEntry<int> HuntMeatTarget;

	internal static ConfigEntry<float> HuntRange;

	internal static ConfigEntry<float> HuntFollowRange;

	internal static ConfigEntry<float> HuntChaseExtra;

	internal static ConfigEntry<float> HuntBagFull;

	internal static ConfigEntry<bool> LootKills;

	internal static ConfigEntry<float> LootRadius;

	internal static ConfigEntry<float> LootWait;

	internal static ConfigEntry<float> LootMaxDistance;

	internal static ConfigEntry<float> CollectRadius;

	internal static ConfigEntry<float> FollowMaxDistance;

	internal static ConfigEntry<bool> FollowAutoBoard;

	internal static ConfigEntry<float> FollowBoardRange;

	internal static ConfigEntry<float> FollowIndoorDistance;

	internal static ConfigEntry<bool> FollowRegroup;

	internal static ConfigEntry<float> FollowRegroupTime;

	internal static ConfigEntry<bool> FollowSitIndoors;

	internal static ConfigEntry<float> FollowChairRange;

	internal static ConfigEntry<float> FollowSitStayRange;

	internal static ConfigEntry<int> PlantGroupSize;

	internal static ConfigEntry<float> FarmNextCropRange;

	internal static ConfigEntry<string> PlantAny;

	internal static ConfigEntry<int> WoodLimit;

	internal static ConfigEntry<int> StoneLimit;

	internal static ConfigEntry<int> OreLimit;

	internal static ConfigEntry<string> GatherWood;

	internal static ConfigEntry<string> GatherStone;

	internal static ConfigEntry<string> GatherOre;

	internal static ConfigEntry<string> WoodSkipTrees;

	internal static ConfigEntry<float> BaseProtectRadius;

	internal static ConfigEntry<bool> JunkCleanup;

	internal static ConfigEntry<string> NeverDiscard;


	internal static ConfigEntry<float> WalkSpeed;

	internal static ConfigEntry<float> RunSpeed;

	internal static ConfigEntry<float> SwimSpeed;

	internal static ConfigEntry<float> TurnSpeed;

	internal static ConfigEntry<float> ViewRange;

	internal static ConfigEntry<float> ViewAngle;

	internal static ConfigEntry<float> FoodRegenInterval;

	internal static ConfigEntry<float> MeadCooldown;

	internal static ConfigEntry<float> HealthMeadThreshold;

	internal static ConfigEntry<float> StaminaMeadThreshold;

	internal static ConfigEntry<float> ConsumeCheckInterval;

	internal static ConfigEntry<float> StaminaRegenRate;

	internal static ConfigEntry<float> StaminaRunDrain;

	internal static ConfigEntry<float> StaminaSneakDrain;

	internal static ConfigEntry<float> StaminaSwimDrain;

	internal static ConfigEntry<float> StaminaRegenDelay;

	internal static ConfigEntry<float> HarvestScanRadius;

	internal static ConfigEntry<float> HarvestFollowRange;

	internal static ConfigEntry<float> HarvestFollowLogRange;

	internal static ConfigEntry<float> CombatBowMinDraw;

	internal static ConfigEntry<float> CombatBowAimTurnSpeed;

	internal static ConfigEntry<float> CombatRangedMeleeRange;

	internal static ConfigEntry<float> FoodEatAgainMinutes;

	internal static ConfigEntry<bool> CombatIndoorAggroOnly;

	internal static ConfigEntry<bool> CombatDungeonDefensive;

	internal static ConfigEntry<float> CombatDefendRange;

	internal static ConfigEntry<float> CombatOrderHoldTime;

	internal static ConfigEntry<float> CombatLeashFromYou;

	internal static ConfigEntry<float> CombatGiveUpTime;

	internal static ConfigEntry<float> CombatBowEdgeMargin;

	internal static ConfigEntry<float> CombatBowFullDrawRange;

	internal static ConfigEntry<float> GatherRestStamina;

	internal static ConfigEntry<float> GatherResumeStamina;

	internal static ConfigEntry<float> GatherPowerChopLogBelow;

	internal static ConfigEntry<float> ForageFollowRange;

	internal static ConfigEntry<float> HarvestAttackInterval;

	internal static ConfigEntry<float> HarvestRange;

	internal static ConfigEntry<bool> HarvestPickUpLoose;

	internal static ConfigEntry<float> HarvestScanInterval;


	internal static ConfigEntry<float> HarvestOverweightThreshold;

	internal static ConfigEntry<float> HarvestDropScanRadius;

	internal static ConfigEntry<float> HarvestBlacklistDuration;

	internal static ConfigEntry<string> ForageItems;

	internal static ConfigEntry<float> HomeZoneRadius;

	internal static ConfigEntry<float> SmeltScanRadius;

	internal static ConfigEntry<float> SmeltScanInterval;

	internal static ConfigEntry<int> SmeltMaxCarryOre;

	internal static ConfigEntry<int> SmeltMaxCarryFuel;

	internal static ConfigEntry<float> SmeltUseDistance;

	internal static ConfigEntry<float> FarmScanRadius;

	internal static ConfigEntry<bool> FarmPlantTilled;

	internal static ConfigEntry<float> FarmUnreachableForget;

	internal static ConfigEntry<float> FarmScanInterval;

	internal static ConfigEntry<float> FarmPlantSpacing;

	internal static ConfigEntry<float> FarmUseDistance;

	internal static ConfigEntry<float> FishScanRadius;

	internal static ConfigEntry<string> FishList;

	internal static ConfigEntry<int> FishTarget;

	internal static ConfigEntry<float> FishCastWait;

	internal static ConfigEntry<float> FishBagFull;

	internal static ConfigEntry<float> RepairDurabilityThreshold;

	internal static ConfigEntry<float> RepairHomeThreshold;

	internal static ConfigEntry<float> RepairNoStationWait;

	internal static ConfigEntry<float> RepairScanRadius;

	internal static ConfigEntry<float> RepairScanInterval;

	internal static ConfigEntry<float> RepairTickInterval;

	internal static ConfigEntry<float> RepairUseDistance;

	internal static ConfigEntry<bool> UpkeepRepair;

	internal static ConfigEntry<float> UpkeepRepairBelow;

	internal static ConfigEntry<float> UpkeepReach;

	internal static ConfigEntry<bool> UpkeepRestock;

	internal static ConfigEntry<bool> RefuelFires;

	internal static ConfigEntry<bool> RefuelHearths;

	internal static ConfigEntry<bool> RefuelBraziers;

	internal static ConfigEntry<bool> RefuelStandingTorches;

	internal static ConfigEntry<bool> RefuelWallTorches;

	internal static ConfigEntry<bool> RefuelHotTubs;

	internal static ConfigEntry<bool> RefuelOther;

	internal static ConfigEntry<float> UpkeepFuelBelow;

	internal static ConfigEntry<bool> UpkeepSort;

	internal static ConfigEntry<string> UpkeepSortItems;

	internal static ConfigEntry<bool> UpkeepSortGear;

	internal static ConfigEntry<float> UpkeepAwayRadius;

	internal static ConfigEntry<float> UpkeepScanInterval;

	internal static ConfigEntry<float> UpkeepWorkTime;

	internal static ConfigEntry<float> UpkeepGiveUp;

	internal static ConfigEntry<bool> UpkeepPiles;

	internal static ConfigEntry<bool> UpkeepHoney;

	internal static ConfigEntry<float> UpkeepHoneyAt;

	internal static ConfigEntry<float> UpkeepOrderWait;

	internal static ConfigEntry<bool> SmeltUsePiles;

	internal static ConfigEntry<float> CookFuelLevel;

	internal static ConfigEntry<bool> CookUsePiles;

	internal static ConfigEntry<bool> CookMakeMead;

	internal static ConfigEntry<bool> IdleEnabled;

	internal static ConfigEntry<float> IdleStartAfter;

	internal static ConfigEntry<float> IdleRadius;

	internal static ConfigEntry<bool> IdleChores;

	internal static ConfigEntry<float> IdleChoreRadius;

	internal static ConfigEntry<bool> IdleSit;

	internal static ConfigEntry<bool> IdleWander;

	internal static ConfigEntry<bool> IdleSocialize;

	internal static ConfigEntry<float> IdleSpeechInterval;

	internal static ConfigEntry<int> UpkeepPileKeep;

	internal static ConfigEntry<string> UpkeepPileItems;

	internal static ConfigEntry<float> RestHealRate;

	internal static ConfigEntry<float> RestPlayerRange;

	internal static ConfigEntry<float> RestFireRange;

	internal static ConfigEntry<float> RestWarmupTime;

	internal static ConfigEntry<float> RestDirectedTimeout;

	internal static ConfigEntry<bool> SpeechEnabled;

	internal static ConfigEntry<bool> SpeechFarMessages;

	internal static ConfigEntry<bool> SpeechAnnounceDeath;

	internal static ConfigEntry<float> SpeechCullDistance;

	internal static ConfigEntry<float> SpeechDuration;

	internal static ConfigEntry<float> SpeechSayCooldown;

	internal static ConfigEntry<float> SpeechMinInterval;

	internal static ConfigEntry<float> SpeechMaxInterval;

	// Fase 3.3c: the periodic chatter (CompanionTalk's own timer) is off by default; orders, mode changes, job
	// milestones and everything the AI layer says still speak.
	internal static ConfigEntry<bool> SpeechChatter;

	internal static ConfigEntry<float> SkillDeathLossFactor;

	internal static ConfigEntry<KeyCode> DirectTargetKey;

	internal static ConfigEntry<KeyCode> RadialMenuKey;

	internal static ConfigEntry<KeyCode> ConfigPanelKey;

	internal static ConfigEntry<KeyCode> FarmZoneKey;

	internal static ConfigEntry<KeyCode> FarmZoneModifier;

	// [AI] - Fase 3.1: ordens por texto no chat, interpretadas por um LLM local (LM Studio/Ollama, API OpenAI-compatible)
	internal static ConfigEntry<bool> AiEnabled;

	internal static ConfigEntry<string> AiBaseUrl;

	internal static ConfigEntry<string> AiModel;

	internal static ConfigEntry<string> AiApiKey;

	internal static ConfigEntry<float> AiTemperature;

	internal static ConfigEntry<int> AiTimeoutSeconds;

	internal static ConfigEntry<string> AiPrefix;

	internal static ConfigEntry<bool> AiShowResponse;

	internal static ConfigEntry<float> AiCooldown;

	internal static ConfigEntry<float> AiTargetRange;

	// [AI] - Fase 3.5: personalidade (preset ou texto livre), memória curta da conversa e conversa livre
	internal static ConfigEntry<string> AiPersonality;

	internal static ConfigEntry<string> AiPersonalityPreset;

	internal static ConfigEntry<int> AiMemory;

	internal static ConfigEntry<bool> AiConversation;

	// [AI] - Fase 3.1 (saída estruturada): response_format/json_schema no pedido, para o modelo sempre responder JSON
	internal static ConfigEntry<bool> AiStructuredOutput;

	// [AI] - local STT (spike): the in-process Whisper.net test. See Diagnostics/WhisperSpike.cs.
	internal static ConfigEntry<bool> SttTest;

	internal static ConfigEntry<string> SttModel;

	internal static ConfigEntry<string> SttWav;

	// [AI] - Fase 3.3: ordens por voz (push-to-talk + STT local com Whisper.net). Ver AI/VoiceInput.cs.
	internal static ConfigEntry<bool> VozEnabled;

	internal static ConfigEntry<KeyCode> VozTecla;

	internal static ConfigEntry<string> VozLanguage;

	internal static ConfigEntry<float> VozMaxSeconds;

	internal static ConfigEntry<float> VozSilenceThreshold;

	internal static ConfigEntry<string> VozPromptInitial;

	internal static ConfigEntry<int> VozDevice;

	internal static ConfigEntry<bool> VozShowText;

	// [AI] - Fase 3.7: painel da IA no jogo (personalidade, voz, push-to-talk). Ver UI/CompanionAiPanel.cs.
	internal static ConfigEntry<KeyCode> AiPanelKey;

	// [AI] - Fase 3.4: TTS local (Kokoro-82M via sherpa-onnx): o companheiro FALA. Ver AI/CompanionTts.cs.
	internal static ConfigEntry<bool> TtsEnabled;

	internal static ConfigEntry<int> TtsVoice;

	internal static ConfigEntry<float> TtsSpeed;

	internal static ConfigEntry<int> TtsThreads;

	internal static ConfigEntry<float> TtsVolume;

	internal static ConfigEntry<string> TtsModel;

	internal static ConfigEntry<string> TtsLanguage;

	internal static ConfigEntry<bool> TtsCache;

	internal static ConfigEntry<bool> TtsSpeakReplies;

	internal static ConfigEntry<bool> TtsSpeakModLines;

	// [AI] - Fase 3.4 (parte 3): o audio da voz do companheiro. O eco tinha uma causa so: o Valheim carrega UMA
	// AudioReverbZone global (no proprio objeto do AudioListener) com raio de 1000 m e 4 s de cauda, e a nossa
	// AudioSource estava no padrao do Unity (reverbZoneMix = 1) - a fala pegava a cauda inteira. O jogo foge dela
	// (a musica usa 0, o ambiente 0,19). TtsReverbZoneMix (0 = voz seca) e a opcao de verdade; EcoDiag e so o
	// diagnostico de audio no log. Ver AI/CompanionTts.cs.
	internal static ConfigEntry<float> TtsReverbZoneMix;

	internal static ConfigEntry<bool> TtsEcoDiag;

	internal static void Init(ConfigFile cfg)
	{
		// 1.8.3: BepInEx's SaveOnConfigSet is true and every Bind writes the whole file (ConfigFile.Bind -> TrySave),
		// so the boot rewrote the .cfg once per setting - 266 times for nothing, on the main thread. The saves are off
		// for the whole of Init and one write happens at the end, after the migrations (they change values too and
		// call Save themselves, which is fine with the flag off); the flag goes back to what it was, so the panels
		// (which write entry.Value) keep saving the file on the spot.
		bool saveOnConfigSet = cfg.SaveOnConfigSet;
		cfg.SaveOnConfigSet = false;
		try
		{
			BindEntries(cfg);
		}
		finally
		{
			SaveQuietly(cfg);
			cfg.SaveOnConfigSet = saveOnConfigSet;
		}
	}

	/// <summary>1.8.3: the binds and the migrations, all in one place so <see cref="Init"/> can hold the per-bind saves
	/// off around them. The last thing it does is the migrations - they have to see the values the file already had.
	/// </summary>
	private static void BindEntries(ConfigFile cfg)
	{
		// [General]
		MaxCompanions = Bind(cfg, "General", "MaxCompanions", 10, "Phase 3.19: the most companions you can have in this world (10 is the ceiling). The count includes the dead and the lost: the tombstone brings the SAME companion back, so dying does not free a slot. At the limit the Trader, the AI panel summoner and the starter companion refuse to create another (the Shrine can still recover a dead companion). To free a slot, dismiss one from its wheel menu (Despawn).", new AcceptableValueRange<int>(1, 10));
		CompanionPrice = Bind(cfg, "General", "CompanionPrice", 1000, "The offering in coins to summon another companion at Odin's Shrine (Summon companion), paid from your pack.", new AcceptableValueRange<int>(0, 50000));
		RecoverUnsummoned = Bind(cfg, "General", "RecoverUnsummoned", defaultValue: true, "An unsummoned (despawned) companion can be brought back at Odin's Shrine (Recover companions) - with its name, looks, skills and settings, but not its gear, which stays in the tombstone where it was unsummoned - until you next load this world. Off: unsummoning is permanent.");
		// 1.5.71: the old default (2000) was never used for anything - move it to the new default
		if (CompanionPrice.Value == 2000)
		{
			CompanionPrice.Value = 1000;
		}
		SpawnStarterCompanion = Bind(cfg, "General", "SpawnStarterCompanion", defaultValue: true, "The first time you enter a world with a character, you're offered one companion for free.");
		ResetStarterFlag = Bind(cfg, "General", "ResetStarterFlag", defaultValue: false, "Set to true to be offered the free starter companion again in the current world (on the next world load). It turns itself back to false.");
		ShowCompanionRoster = Bind(cfg, "General", "ShowCompanionRoster", defaultValue: false, "Set to true to list your companions in this world and their status: Alive, Dead (its tombstone will bring it back), Lost (nothing left of it - it can be recovered) or Unknown (on a dedicated server, a companion that isn't nearby can't be checked). It turns itself back to false.");
		RecoverLostCompanion = Bind(cfg, "General", "RecoverLostCompanion", defaultValue: false, "Set to true to bring back a Lost companion next to you, with its gear and settings from the last roster save. An Unknown companion needs it set a second time within 60 s to confirm (it may still be alive somewhere). If the old companion turns up later, it's removed. It turns itself back to false.");
		ProtectCompanions = Bind(cfg, "General", "ProtectCompanions", defaultValue: true, "Stops other mods from deleting a companion outright (for example mods that clear creatures out of warded areas). Companions can still die, leave a tombstone and respawn as normal, and the Despawn button still works. When something is blocked, the log says what tried. Turn off only if another mod needs to remove companions.");
		ShowMinimapHudPanel = Bind(cfg, "General", "ShowMinimapPanel", defaultValue: true, "Show the companion status panel under the minimap.");
		ShowOverheadBars = Bind(cfg, "General", "ShowOverheadBars", defaultValue: true, "Show health and stamina bars above each companion's head.");
		MaxCarryWeight = Bind(cfg, "General", "MaxCarryWeight", 300f, "The BASE weight a companion can carry (a player carries 300). Gathering and hunting companions head home to unload before this. Items that raise carrying capacity add on top of this value: the Megingjord, for example, gives +150 and counts with the belt anywhere in the companion's bag (no need to equip it), so with this at 300 its ceiling becomes 450. Two identical belts don't add up: the bonus counts once, as in the game.", new AcceptableValueRange<float>(50f, 2000f));
		BaseHealth = Bind(cfg, "General", "BaseHealth", 25f, "A companion's health before food (a player has 25).", new AcceptableValueRange<float>(1f, 500f));
		BaseStamina = Bind(cfg, "General", "BaseStamina", 50f, "A companion's stamina before food (a player has 50).", new AcceptableValueRange<float>(1f, 500f));
		MaxFoodSlots = Bind(cfg, "General", "MaxFoodSlots", 3, "How many different foods a companion can have eaten at the same time (a player has 3).", new AcceptableValueRange<int>(1, 5));
		StatusEffectsOnRegen = Bind(cfg, "General", "StatusEffectsOnRegen", true, "Status effects change a companion's health and stamina regeneration like they do for a player: Wet and Cold slow it, meads and other buffs change it. Off = companions regenerate at their normal rate whatever the effects.");
		HomeTripRange = Bind(cfg, "General", "HomeTripRange", 60f, "How far from home (meters) a companion will still walk back for chest trips: fetching or putting back tools, repairing a broken tool, emptying its bags. Further away than this, a companion missing its job tool says so and follows you until you are both back near home.", new AcceptableValueRange<float>(10f, 500f));
		HomeZoneRadius = Bind(cfg, "General", "HomeRadius", 50f, "Starting size (meters) of a companion's home area around its home spot - where it looks for a bed to claim. Each companion's own size can be changed later in its home panel.", new AcceptableValueRange<float>(5f, 200f));
		// [Controls]
		DirectTargetKey = Bind(cfg, "Controls", "DirectTargetKey", KeyCode.Z, "The command key. Tap it while looking at something to send your companions to it (move there, attack it, open the door, sit by the fire, repair at the station...). Hold it to call them to you.");
		RadialMenuKey = Bind(cfg, "Controls", "RadialMenuKey", KeyCode.E, "Hold this while looking at a companion to open its command wheel.");
		ConfigPanelKey = Bind(cfg, "Controls", "ConfigPanelKey", KeyCode.F8, "Opens the in-game settings panel, where all of these settings can be changed.");
		FarmZoneKey = Bind(cfg, "Controls", "FarmZoneKey", KeyCode.Z, "Pressed together with FarmZoneModifier: opens the farm zone editor.");
		FarmZoneModifier = Bind(cfg, "Controls", "FarmZoneModifier", KeyCode.LeftAlt, "Hold this and press FarmZoneKey to open the farm zone editor.");
		RecallRange = Bind(cfg, "Controls", "RecallRange", 40f, "How close (meters) a companion has to be to you to answer a recall (holding the command key) or a point-and-command order. Companions with Command switched off on the wheel ignore both. 0 = no limit.", new AcceptableValueRange<float>(0f, 500f));
		SuperRecallHoldTime = Bind(cfg, "Controls", "SuperRecallHoldTime", 2f, "Keep holding the command key this many seconds to recall every companion at any distance, including ones with Command switched off.", new AcceptableValueRange<float>(0.5f, 10f));
		PassiveKey = Bind(cfg, "Controls", "PassiveKey", KeyCode.None, "Sets the Passive stance. Looking at one of your companions: just that one; otherwise every companion within RecallRange that has Command on. None = no key.");
		BalancedKey = Bind(cfg, "Controls", "BalancedKey", KeyCode.None, "Sets the Balanced stance, like PassiveKey.");
		RangedKey = Bind(cfg, "Controls", "RangedKey", KeyCode.None, "Sets the Ranged stance, like PassiveKey.");
		MeleeKey = Bind(cfg, "Controls", "MeleeKey", KeyCode.None, "Sets the Melee stance, like PassiveKey.");
		// [Follow]
		FollowMaxDistance = Bind(cfg, "Follow", "MaxDistance", 10f, "How far a following companion lets you get before it moves to keep up. It picks its own spot within this range (beside, behind, sometimes ahead) and changes it now and then.", new AcceptableValueRange<float>(4f, 30f));
		FollowAutoBoard = Bind(cfg, "Follow", "AutoBoard", defaultValue: true, "When you're aboard a ship close by, a following companion gets on too, the way you would: it jumps aboard from a dock or the shore, uses the ship's ladder (swimming to it if it has the stamina), or follows your own steps aboard. If it can't, it waits on the shore. A companion that took a seat gets up again when you go ashore.");
		FollowBoardRange = Bind(cfg, "Follow", "BoardRange", 20f, "AutoBoard: how close (meters) the ship has to be for a following companion to board it on its own. The ship also has to be nearly still.", new AcceptableValueRange<float>(5f, 60f));
		FollowIndoorDistance = Bind(cfg, "Follow", "IndoorDistance", 4f, "The same range while you're indoors (under a roof), so companions stay in the room with you.", new AcceptableValueRange<float>(2f, 10f));
		FollowRegroup = Bind(cfg, "Follow", "Regroup", defaultValue: true, "When you stand still for RegroupTime seconds, companions come back and wait close to you. Off = they stay wherever they stopped.");
		FollowRegroupTime = Bind(cfg, "Follow", "RegroupTime", 45f, "Seconds you stand still before companions regroup around you (see Regroup).", new AcceptableValueRange<float>(5f, 600f));
		FollowSitIndoors = Bind(cfg, "Follow", "SitIndoors", defaultValue: true, "When you stop indoors, companions sit down near you: on a free chair or bench if there is one, otherwise on the floor. Near home they empty their bags first.");
		FollowChairRange = Bind(cfg, "Follow", "ChairRange", 8f, "How far (meters) from you a companion looks for a free chair or bench to sit on when you stop indoors - one in sight of you, on your level. Only when there's none does it sit on the floor.", new AcceptableValueRange<float>(2f, 30f));
		FollowSitStayRange = Bind(cfg, "Follow", "SitStayRange", 10f, "Indoors, a companion that sat down stays seated while you move about within this many meters of it, and gets up to follow once you go further (or step outside).", new AcceptableValueRange<float>(2f, 30f));
		FollowTeleportDistance = Bind(cfg, "Follow", "TeleportDistance", 50f, "A following companion that falls this many meters behind appears next to you: right away beyond 1.5x this distance, otherwise once it can't see you and isn't getting any closer.", new AcceptableValueRange<float>(20f, 200f));
		FollowStuckTeleport = Bind(cfg, "Follow", "StuckTeleport", defaultValue: false, "When following you, a companion that has been stuck for 15 seconds somewhere it can't see you appears next to you. Off = it keeps trying to find its own way.");
		FormationOffset = Bind(cfg, "Follow", "FormationSpacing", 3f, "Space (meters) between companions walking side by side in formation (when they follow something other than you).", new AcceptableValueRange<float>(1f, 10f));
		FormationCatchupDist = Bind(cfg, "Follow", "FormationCatchUp", 15f, "In formation, a companion this many meters away from its place runs to catch up.", new AcceptableValueRange<float>(5f, 50f));
		// [Movement]
		WalkSpeed = Bind(cfg, "Movement", "WalkSpeed", 2f, "Walking speed (a player walks at about 2). Takes effect after the world is reloaded.", new AcceptableValueRange<float>(0.5f, 5f));
		RunSpeed = Bind(cfg, "Movement", "RunSpeed", 7f, "Running speed (a player runs at about 7). Takes effect after the world is reloaded.", new AcceptableValueRange<float>(2f, 15f));
		SwimSpeed = Bind(cfg, "Movement", "SwimSpeed", 2f, "Swimming speed (a player swims at about 2). Takes effect after the world is reloaded.", new AcceptableValueRange<float>(0.5f, 5f));
		TurnSpeed = Bind(cfg, "Movement", "TurnSpeed", 300f, "How fast a companion turns (degrees per second). Takes effect after the world is reloaded.", new AcceptableValueRange<float>(50f, 600f));
		ViewRange = Bind(cfg, "Movement", "ViewRange", 20f, "How far (meters) a companion can see enemies and other creatures. Takes effect after the world is reloaded.", new AcceptableValueRange<float>(5f, 100f));
		ViewAngle = Bind(cfg, "Movement", "ViewAngle", 120f, "A companion's field of view (degrees). Takes effect after the world is reloaded.", new AcceptableValueRange<float>(30f, 360f));
		CompanionNavMesh = Bind(cfg, "Movement", "Pathing", defaultValue: true, "Companions find their way with their own map of the world, where doors don't block the way (they open them as they get there) and steps up to 0.6 m are climbable (they hop them). Off = the same map creatures use, where every closed door is a wall and most stairs don't connect. Takes effect after a restart.");
		WorkRunDistance = Bind(cfg, "Movement", "WorkRunDistance", 25f, "Companions walk while doing jobs (farming, chest trips, smelting, cooking, chores, repairs, gathering) and only run when what they are heading to is further away than this many meters. 0 = never run for work. Following you, fighting and hunting are not affected.", new AcceptableValueRange<float>(0f, 500f));
		DryDetour = Bind(cfg, "Movement", "DryDetour", 30f, "Companions prefer staying dry, like a player: when the way somewhere crosses water they go round it on land, as long as the dry way is no longer than 1.5x the swimming way plus this many meters. Otherwise they swim.", new AcceptableValueRange<float>(0f, 500f));
		JobSwim = Bind(cfg, "Movement", "JobSwim", 20f, "The most swimming (meters) a job accepts to reach something: loot from a kill, prey, a bird, a fishing spot. Further than that and it leaves it.", new AcceptableValueRange<float>(0f, 200f));
		MaxDrop = Bind(cfg, "Movement", "MaxDrop", 3f, "When a straight line is much shorter than the way round (down the stairs and back, round a wall), companions take it like a player: hopping steps, dropping off ledges up to this many meters (players take no fall damage below 4 m) and jumping small gaps (JumpGap).", new AcceptableValueRange<float>(1f, 4f));
		StuckDrop = Bind(cfg, "Movement", "StuckDrop", 8f, "When a companion is stuck above where it needs to be with no other way down (off the top of a ladder, a platform, a ledge), it jumps down a drop up to this many meters, like a player who takes a bit of fall damage rather than stay put. A ladder down comes first, and it never takes a drop that would cost more than 30% of its health right now (companions take fall damage like a player: none up to 4 m).", new AcceptableValueRange<float>(3f, 20f));
		JumpGap = Bind(cfg, "Movement", "JumpGap", 1.5f, "The widest gap (meters) a companion leaps across on such a shortcut. 0.5 = no gaps.", new AcceptableValueRange<float>(0.5f, 3f));
		JumpWaterDepth = Bind(cfg, "Movement", "JumpWaterDepth", 0.3f, "Water at least this deep (meters) in a companion's way is jumped over when it's narrow enough (JumpWaterWidth), like a player jumps a small stream instead of running through it. Wider shallow water is waded through.", new AcceptableValueRange<float>(0.05f, 2f));
		JumpWaterWidth = Bind(cfg, "Movement", "JumpWaterWidth", 3f, "The widest stretch of water (meters) a companion jumps over.", new AcceptableValueRange<float>(1f, 4f));
		WaterStaminaFleeThreshold = Bind(cfg, "Movement", "SwimStaminaShore", 0.25f, "When a swimming companion's stamina drops below this share (0.25 = 25%), it heads for the nearest shore, whatever it was doing.", new AcceptableValueRange<float>(0f, 1f));
		// [Combat]
		EngageOnNotice = Bind(cfg, "Combat", "EngageOnNotice", true, "On: companions attack a creature as soon as it has noticed you or a companion. Off: they wait until it is actually coming for you (alerted). Anything that attacks is always fought back, and Hunt goes after its prey either way.");
		HealthRetreatPct = Bind(cfg, "Combat", "HealthRetreatPct", 0.3f, "Below this share of its health (0.3 = 30%) a companion fights defensively: it takes out its best blocking shield and doesn't open with a torch.", new AcceptableValueRange<float>(0.05f, 0.9f));
		FleeHealthPct = Bind(cfg, "Combat", "FleeHealthPct", 0.15f, "Below this share of its health (0.15 = 15%) a companion runs away from the enemy.", new AcceptableValueRange<float>(0.05f, 0.6f));
		WeaponSwitchMargin = Bind(cfg, "Combat", "WeaponSwitchMargin", 0.1f, "A different weapon has to be this much better against the current enemy (0.1 = 10%) before a companion switches to it, so it doesn't swap back and forth.", new AcceptableValueRange<float>(0f, 0.5f));
		ShieldMultiAttackerCount = Bind(cfg, "Combat", "ShieldMultiAttackerCount", 3, "When this many enemies attack a companion at once, it switches to its shield with the best block.", new AcceptableValueRange<int>(1, 10));
		ParryChance = Bind(cfg, "Combat", "ParryChance", 0.3f, "Chance (0.3 = 30%) that a companion with no Blocking skill times a block as a parry (more damage stopped, less stamina, and many attackers are staggered). It rises with Blocking skill up to ParryChanceMax. Shields without a parry bonus never parry.", new AcceptableValueRange<float>(0f, 1f));
		ParryChanceMax = Bind(cfg, "Combat", "ParryChanceMax", 0.7f, "Chance (0.7 = 70%) that a companion times a block as a parry at Blocking skill 100.", new AcceptableValueRange<float>(0f, 1f));
		FightLikePlayer = Bind(cfg, "Combat", "FightLikePlayer", true, "With a melee weapon, companions fight like a player: short combos when there's an opening, guarding in between, stepping out of swings, getting clear when several enemies are on them, and backing off to catch their breath when low on stamina. Off: the older way (one swing at a time on the weapon's timer).");
		CombatComboHits = Bind(cfg, "Combat", "ComboHits", 3, "Most swings in one combo. Companions follow their weapon's own combo (3 swings on melee weapons, the last doing double damage) up to this many; now and then one short, and shorter when stamina is low.", new AcceptableValueRange<int>(1, 5));
		CombatComboPause = Bind(cfg, "Combat", "ComboPause", 0.5f, "Seconds (roughly - it varies) between combos, guarding or stepping back out of reach.", new AcceptableValueRange<float>(0.2f, 5f));
		CombatStaminaReserve = Bind(cfg, "Combat", "StaminaReserve", 0.3f, "Share of its stamina (0.3 = 30%) a companion keeps for blocking. A combo is only started above it and may dip to half of it; below it, the companion backs out of reach to get its breath back.", new AcceptableValueRange<float>(0f, 0.9f));
		CombatStaminaResume = Bind(cfg, "Combat", "StaminaResume", 0.5f, "Share of its stamina (0.5 = 50%) it waits for before going back in after backing off.", new AcceptableValueRange<float>(0f, 1f));
		CombatRecoverTime = Bind(cfg, "Combat", "BackOffTime", 8f, "Seconds a companion keeps trying to back off from an enemy that stays on it before it fights on with what stamina it has.", new AcceptableValueRange<float>(2f, 30f));
		StepAwayChance = Bind(cfg, "Combat", "StepAwayChance", 0.3f, "Chance (0.3 = 30%) a companion steps back or aside out of an enemy's swing instead of blocking it, when there's room and it can get out of reach in time. Doubled when it's low on stamina.", new AcceptableValueRange<float>(0f, 1f));
		CombatGroupSize = Bind(cfg, "Combat", "GroupSize", 2, "With this many enemies close enough to hit a companion, from different sides, it gets clear first - moves for a couple of seconds until they're all on one side - then fights from there.", new AcceptableValueRange<int>(2, 10));
		CombatGroupRange = Bind(cfg, "Combat", "GroupRange", 6f, "How close (meters) an enemy has to be to count as one of the group on a companion.", new AcceptableValueRange<float>(2f, 20f));
		CombatFlankAngle = Bind(cfg, "Combat", "FlankAngle", 100f, "Two enemies more than this many degrees apart, seen from the companion, count as on different sides.", new AcceptableValueRange<float>(30f, 180f));
		CombatBowAimTurnSpeed = Bind(cfg, "Combat", "BowAimTurnSpeed", 600f, "Bows: how fast a companion turns to keep its aim on the target while the bow is drawn (degrees per second), like you swinging the camera round. Movement > TurnSpeed is used the rest of the time.", new AcceptableValueRange<float>(100f, 1440f));
		CombatBowMinDraw = Bind(cfg, "Combat", "BowMinDraw", 0.5f, "Bows: the least a companion draws before letting go (0.5 = half drawn). A partial draw is quicker and cheaper but does less damage and spreads more, like yours. It takes a partial draw when the target is close enough that the spread still hits it, when it's low on stamina (below StaminaReserve), or when the target is rushing in; otherwise it draws fully.", new AcceptableValueRange<float>(0.1f, 1f));
		CombatIndoorAggroOnly = Bind(cfg, "Combat", "IndoorAggroOnly", defaultValue: true, "Indoors - in a dungeon, mine or cave, or under a roof - a companion only fights enemies that are actually coming for you or it (alerted), as if EngageOnNotice were off. Outdoors EngageOnNotice decides.");
		CombatDungeonDefensive = Bind(cfg, "Combat", "DungeonDefensive", defaultValue: true, "In a dungeon, mine or cave a companion fights defensively: it stays with you and fights what comes within DefendRange of you (and anything hitting it), instead of running off after enemies further down the corridor.");
		CombatDefendRange = Bind(cfg, "Combat", "DefendRange", 8f, "Fighting defensively (DungeonDefensive): enemies within this many meters of you are fought; further off they're left to come to you.", new AcceptableValueRange<float>(3f, 30f));
		CombatOrderHoldTime = Bind(cfg, "Combat", "OrderHoldTime", 10f, "After you recall a companion or send it somewhere, it leaves its fight and starts no new one for this many seconds - it still fights back against something hitting it up close. Companions only ever fight enemies they can see (not ones behind walls or closed doors), and only open a door to get at one when you order the attack.", new AcceptableValueRange<float>(0f, 60f));
		CombatLeashFromYou = Bind(cfg, "Combat", "LeashFromYou", 35f, "A companion lets an enemy go once it is further than this many meters from you, and starts no fight with one that far from you - it comes back to you instead. An attack order, or something hitting it up close, still keeps the fight going.", new AcceptableValueRange<float>(10f, 60f));
		CombatGiveUpTime = Bind(cfg, "Combat", "GiveUpTime", 10f, "An enemy a companion can't get at with its melee weapon (out in deep water, no way to walk there) and that isn't right on top of it is let go after this many seconds without being hurt, and left alone for 20 s - so it doesn't stay stuck on it.", new AcceptableValueRange<float>(3f, 60f));
		CombatBowEdgeMargin = Bind(cfg, "Combat", "BowEdgeMargin", 3f, "Bows: a draw is only started this many meters inside the bow's 30 m reach, and a draw already started is kept while the target is up to twice this far past the reach (walking closer, not running), so a target at the edge doesn't cost the draw.", new AcceptableValueRange<float>(0f, 10f));
		CombatRangedMeleeRange = Bind(cfg, "Combat", "RangedMeleeRange", 4f, "Ranged stance: the bow is out (also when not fighting); the melee weapon and shield only come out when an enemy gets within this many meters - or twice that while it's catching its breath - and go away again once it's past twice the distance.", new AcceptableValueRange<float>(1f, 15f));
		CombatBowFullDrawRange = Bind(cfg, "Combat", "BowFullDrawRange", 15f, "Bows: from this many meters away a companion always draws fully - a weaker arrow is slower and drops short at range.", new AcceptableValueRange<float>(3f, 50f));
		CombatFinishHits = Bind(cfg, "Combat", "FinishHits", 2, "An enemy this many hits or fewer from dead (judged from its own hits so far) is finished off with all the stamina it takes, as long as nothing else is close enough to hit the companion afterwards. 0 = never spend the stamina kept back for blocking.", new AcceptableValueRange<int>(0, 5));
		CombatSpacing = Bind(cfg, "Combat", "Spacing", true, "One on one, a companion keeps just outside the enemy's reach, goes in for a combo when the enemy can't swing again in time, then steps back out - and steps out of swings at the edge of its reach rather than blocking them.");
		CombatFlank = Bind(cfg, "Combat", "Flank", true, "When an enemy is busy with you (or someone else), a companion works its way round behind it and attacks from there, out of its swings. (The game's backstab bonus only counts on enemies that haven't noticed anyone.)");
		CombatSpacingMargin = Bind(cfg, "Combat", "SpacingMargin", 0.4f, "How far (meters) outside the enemy's reach a companion waits when spacing.", new AcceptableValueRange<float>(0f, 2f));
		CombatSpacingStepChance = Bind(cfg, "Combat", "SpacingStepChance", 0.6f, "Chance (0.6 = 60%) that a spacing companion steps back out of a swing it's only just inside the reach of, instead of blocking it (the rest are blocks, some of them parries).", new AcceptableValueRange<float>(0f, 1f));
		BowSwitchDistance = Bind(cfg, "Combat", "BowSwitchDistance", 15f, "A companion with a bow or crossbow switches to it when its target is further away than this (meters).", new AcceptableValueRange<float>(5f, 60f));
		MeleeSwitchDistance = Bind(cfg, "Combat", "MeleeSwitchDistance", 12f, "It switches back to its melee weapon when the target comes closer than this (meters).", new AcceptableValueRange<float>(3f, 40f));
		UseTorchOpener = Bind(cfg, "Combat", "UseTorchOpener", defaultValue: true, "Against enemies that are weak to fire, open with a torch to set them alight, then switch back to the best weapon.");
		TorchOpenerTimeout = Bind(cfg, "Combat", "TorchOpenerTimeout", 4f, "Seconds to keep trying to set an enemy alight before giving up and switching back.", new AcceptableValueRange<float>(1f, 10f));
		TorchOpenerCooldown = Bind(cfg, "Combat", "TorchOpenerCooldown", 10f, "Seconds before the same enemy can be opened on with a torch again.", new AcceptableValueRange<float>(0f, 60f));
		TorchOpenerMinTargetHealth = Bind(cfg, "Combat", "TorchOpenerMinTargetHealth", 40f, "No torch opener on enemies with less maximum health than this - not worth it.", new AcceptableValueRange<float>(0f, 500f));
		// [Food]
		HealthMeadThreshold = Bind(cfg, "Food", "HealthMeadThreshold", 0.5f, "A companion drinks a healing mead from its pack when its health drops below this share (0.5 = 50%).", new AcceptableValueRange<float>(0.1f, 0.9f));
		StaminaMeadThreshold = Bind(cfg, "Food", "StaminaMeadThreshold", 0.25f, "It drinks a stamina mead when its stamina drops below this share (0.25 = 25%).", new AcceptableValueRange<float>(0.05f, 0.9f));
		FoodEatAgainMinutes = Bind(cfg, "Food", "EatAgainMinutes", 3f, "A companion eats the same food again once it has this many minutes left, like a player topping up before it runs out (the game only allows that once a food is under half its time). It needs that food in its pack; otherwise it eats something else when the slot empties. 0 = wait until it runs out.", new AcceptableValueRange<float>(0f, 30f));
		MeadCooldown = Bind(cfg, "Food", "MeadCooldown", 10f, "Seconds after drinking a mead before it drinks another.", new AcceptableValueRange<float>(1f, 60f));
		// [Stamina]
		StaminaRegenRate = Bind(cfg, "Stamina", "RegenRate", 6f, "Stamina a companion gets back per second when it isn't using any.", new AcceptableValueRange<float>(1f, 30f));
		StaminaRunDrain = Bind(cfg, "Stamina", "RunDrainRate", 10f, "Stamina used per second while running.", new AcceptableValueRange<float>(1f, 30f));
		StaminaSneakDrain = Bind(cfg, "Stamina", "SneakDrainRate", 5f, "Stamina used per second while sneaking.", new AcceptableValueRange<float>(1f, 30f));
		// 1.5.88: new key (was SwimDrainRate 10 - double the player's) so existing config files pick up the player's rate
		StaminaSwimDrain = Bind(cfg, "Stamina", "SwimDrainAtSkill0", 5f, "Stamina used per second while swimming (and moving), at Swim skill 0 - it drops to 40% of this at Swim 100, like the player (the game's rate is 5).", new AcceptableValueRange<float>(1f, 30f));
		StaminaRegenDelay = Bind(cfg, "Stamina", "RegenDelay", 1f, "Seconds after using stamina before it starts coming back.", new AcceptableValueRange<float>(0.1f, 5f));
		// [Rest]
		SleepAtNight = Bind(cfg, "Rest", "SleepAtNight", defaultValue: true, "At night, a companion within BedRange of its claimed bed stops what it's doing and goes to bed, including when it's working. It goes back to its job in the morning.");
		SleepWhenPlayerSleeps = Bind(cfg, "Rest", "SleepWhenPlayerSleeps", defaultValue: true, "When you get into a bed, your companions within BedRange of their claimed beds go to bed too, even if it isn't night yet. The night skip waits up to 30 seconds for them.");
		BedRange = Bind(cfg, "Rest", "BedRange", 50f, "How close (meters) a companion has to be to its claimed bed for the two settings above to send it to bed.", new AcceptableValueRange<float>(10f, 200f));
		RestHealRate = Bind(cfg, "Rest", "HealRate", 2f, "Health a companion gets back per second while sitting by a fire or sleeping.", new AcceptableValueRange<float>(0.5f, 20f));
		RestPlayerRange = Bind(cfg, "Rest", "PlayerRange", 5f, "When you sit down near a fire (the sit emote), companions without a job within this many meters of you sit down by it too.", new AcceptableValueRange<float>(2f, 20f));
		RestFireRange = Bind(cfg, "Rest", "FireRange", 5f, "How close (meters) a fire has to be for a companion to sit down by it.", new AcceptableValueRange<float>(2f, 20f));
		RestWarmupTime = Bind(cfg, "Rest", "WarmupTime", 20f, "Seconds of sitting by a fire before a companion becomes Rested, like a player.", new AcceptableValueRange<float>(5f, 120f));
		RestDirectedTimeout = Bind(cfg, "Rest", "DirectedTimeout", 300f, "When you tell a companion to sit by a fire, it gets up again after this many seconds.", new AcceptableValueRange<float>(30f, 1800f));
		// [Death]
		AutoRespawnEnabled = Bind(cfg, "Death", "AutoRespawn", defaultValue: true, "A dead companion comes back by itself after RespawnDelay seconds if nobody brings it back from its tombstone first.");
		AutoRespawnDelaySeconds = Bind(cfg, "Death", "RespawnDelay", 30f, "Seconds after death before a companion comes back by itself.", new AcceptableValueRange<float>(30f, 3600f));
		AutoRespawnAtWorldSpawnWhenNoBed = Bind(cfg, "Death", "RespawnAtWorldSpawn", defaultValue: false, "A companion that comes back by itself and has no claimed bed appears at the world spawn stones. Off = next to you.");
		AutoRespawnAtPlayer = Bind(cfg, "Death", "RespawnAtPlayer", defaultValue: false, "A companion that comes back by itself always appears next to you, even when it has a bed (wins over RespawnAtWorldSpawn). Off = at its bed, and without one as RespawnAtWorldSpawn says.");
		DeathItemRule = Bind(cfg, "Death", "DeathItemRule", 0, "What a companion loses when it dies. 0 = the world's death penalty setting, same as a player. 1 = keep everything. 2 = keep equipped gear, the rest goes in the tombstone. 3 = everything goes in the tombstone. The owner's setting is used even if another player's game handles the death.", new AcceptableValueRange<int>(0, 3));
		TombstoneRecoveryRange = Bind(cfg, "Death", "TombstoneRecoveryRange", 50f, "A companion walks over and empties its own leftover tombstone when it's within this many meters of it.", new AcceptableValueRange<float>(5f, 100f));
		RespawnStance = Bind(cfg, "Death", "RespawnStance", "RecoverFirst", "The stance a companion comes back in after it died. RecoverFirst: Passive, auto-pickup off, it gets its gear back from its tombstone when it's safe (fighting back only if attacked with a weapon in hand), puts it on, eats, then goes back to the stance it died with. Passive: Passive until you change it. Keep: the stance it died with. Setting a stance yourself always wins.", new AcceptableValueList<string>("RecoverFirst", "Passive", "Keep"));
		RecoverThreatRange = Bind(cfg, "Death", "RecoverThreatRange", 15f, "RecoverFirst: enemies within this many meters of the companion or its tombstone count when it decides whether it's safe to go for its gear.", new AcceptableValueRange<float>(5f, 50f));
		RecoverMaxMobs = Bind(cfg, "Death", "RecoverMaxMobs", 3, "RecoverFirst: with more enemies than this busy fighting near its tombstone it waits by you instead of going for its gear.", new AcceptableValueRange<int>(0, 20));
		ShrineBuildable = Bind(cfg, "Death", "ShrineBuildable", defaultValue: true, "Odin's Shrine can be built with the hammer (Misc tab, needs a workbench). Use it to see your companions and bring back a lost one. Takes effect on the next world load; shrines already built keep working.");
		ShrineRecipe = Bind(cfg, "Death", "ShrineRecipe", "Stone:10,Wood:5,GreydwarfEye:3", "What Odin's Shrine costs to build, as Item:Amount pairs separated by commas (item names as in the game's spawn command). Takes effect on the next world load.");
		ShrineModel = Bind(cfg, "Death", "ShrineModel", "Tombstone", "What Odin's Shrine looks like: Tombstone (a player's gravestone) or Ward (the ward stone). Takes effect on the next world load, for shrines already built too.");
		SkillDeathLossFactor = Bind(cfg, "Death", "SkillLoss", 0.25f, "Share of its skills a companion loses when it dies (0.25 = 25%, 0 = none).", new AcceptableValueRange<float>(0f, 1f));
		// [Looting]
		LootKills = Bind(cfg, "Looting", "PickUpKills", true, "After a companion kills something it goes over and picks up what the creature dropped (things you'd auto-pickup yourself), like a player would. Needs Auto Pickup on in the wheel.");
		LootRadius = Bind(cfg, "Looting", "Radius", 6f, "How far around the kill (meters) the drops are looked for.", new AcceptableValueRange<float>(2f, 20f));
		LootWait = Bind(cfg, "Looting", "WaitSeconds", 10f, "Longest wait (seconds) by the body for its drops to come out.", new AcceptableValueRange<float>(2f, 60f));
		LootMaxDistance = Bind(cfg, "Looting", "MaxDistance", 30f, "Kills further away than this (meters), or that you've moved this far away from while it follows you, are left.", new AcceptableValueRange<float>(5f, 100f));
		CollectRadius = Bind(cfg, "Looting", "CollectRadius", 10f, "With the \"collect the items on the ground\" order, how far (meters) from the companion it looks for loose things on the ground - including what you or it dropped on purpose. It walks to each item and picks them up one by one, until nothing is left in range, the bag is full or the order goes past 30 seconds.", new AcceptableValueRange<float>(1f, 30f));
		AutoPickupRange = Bind(cfg, "Looting", "AutoPickupRange", 2f, "With Auto Pickup on in the wheel, a companion picks up items within this many meters of itself as it goes, like a player's auto-pickup.", new AcceptableValueRange<float>(0.5f, 10f));
		// [GearRepair]
		RepairDurabilityThreshold = Bind(cfg, "GearRepair", "DurabilityThreshold", 0.5f, "A companion takes its gear to a crafting station to repair it when something drops below this share of durability (0.5 = 50%).", new AcceptableValueRange<float>(0.1f, 0.9f));
		RepairHomeThreshold = Bind(cfg, "GearRepair", "HomeThreshold", 0.8f, "At home, where a workbench is close by, gear is repaired a bit sooner: below this share (0.8 = 80%). 1 = at any wear. Away from home DurabilityThreshold applies.", new AcceptableValueRange<float>(0.1f, 1f));
		RepairNoStationWait = Bind(cfg, "GearRepair", "NoStationWait", 600f, "When a worn item can't be repaired at any station within reach (say it needs a black forge and there isn't one nearby), or the way to the station failed, that item is left alone for this many seconds before it looks again.", new AcceptableValueRange<float>(30f, 3600f));
		RepairScanRadius = Bind(cfg, "GearRepair", "StationRange", 20f, "How far (meters) a companion looks for a crafting station to repair at. Near home, any station near home counts.", new AcceptableValueRange<float>(5f, 60f));
		// [Gathering]
		HarvestRange = Bind(cfg, "Gathering", "Range", 60f, "Wood, Stone, Ore and Forage with Follow off: how far (meters) from where it's working it goes for trees, rocks and plants - around home when the job is started near home, otherwise around where it was started. It keeps working there when you leave. (Phase 3.3c: 60, was 40.)", new AcceptableValueRange<float>(10f, 150f));
		HarvestFollowRange = Bind(cfg, "Gathering", "FollowRange", 25f, "Wood, Stone and Ore with Follow on: a tree or rock has to be within this many meters of you. With nothing that close the companion just follows you. (With Follow off see Range; Forage has ForageFollowRange.) Phase 3.3c: 25, was 10 - it works up to 25 m from you instead of sticking to you, and it only gives up a target once that is 15 m past it.", new AcceptableValueRange<float>(5f, 60f));
		ForageFollowRange = Bind(cfg, "Gathering", "ForageFollowRange", 20f, "Forage with Follow on: a plant has to be within this many meters of you - the berries you happen to walk past, not everything around. With nothing that close the companion just follows you. (With Follow off see Range.) Phase 3.3c: 20, was 10 - still short enough for Forage to stay \"what you walked past\", and not a sweep of the whole area.", new AcceptableValueRange<float>(3f, 60f));
		HarvestFollowLogRange = Bind(cfg, "Gathering", "FollowLogRange", 20f, "Wood with Follow on: fallen logs and stumps count up to this many meters from you (further than FollowRange), so it finishes the trees it fells before starting a new one - a log can roll well away from where you stand.", new AcceptableValueRange<float>(5f, 60f));
		GatherRestStamina = Bind(cfg, "Gathering", "RestStamina", 0.15f, "Chopping and mining: swing after swing (the weapon's combo) until stamina would drop below this share of the maximum, then stop and get its breath back.", new AcceptableValueRange<float>(0f, 0.9f));
		GatherResumeStamina = Bind(cfg, "Gathering", "ResumeStamina", 0.6f, "Chopping and mining: after stopping for breath, start swinging again once stamina is back to this share of the maximum.", new AcceptableValueRange<float>(0.1f, 1f));
		GatherPowerChopLogBelow = Bind(cfg, "Gathering", "PowerChopLogBelow", 0.3f, "Wood: the power chop (the axe's downward swing) is used on stumps, and on a log only when its top is less than this many meters above the companion's feet - too low for the sideways swing. Everything else gets normal swings.", new AcceptableValueRange<float>(-1f, 2f));
		HarvestPickUpLoose = Bind(cfg, "Gathering", "PickUpLoose", true, "Wood and Stone jobs, early in the game only: also pick up what's lying on the ground by hand - branches (Wood job), loose stones and flint (Stone job) - as long as they're on GatherWood / GatherStone and under their amount. Early game = there's no bronze-or-better axe in the companion's pack, your pack or the chests near home, and no forge at home to make one. With no tool for the job (no axe for Wood, no pickaxe for Stone) picking things up is all it does.");
		WoodLimit = Bind(cfg, "Gathering", "WoodLimit", 500, "How many of each wood material (counting the companion's pack and chests near its home) the Wood job gathers before stopping. 0 = no limit. Per-item numbers in GatherWood override this.", new AcceptableValueRange<int>(0, 100000));
		StoneLimit = Bind(cfg, "Gathering", "StoneLimit", 500, "Same as WoodLimit, for the Stone job and GatherStone.", new AcceptableValueRange<int>(0, 100000));
		OreLimit = Bind(cfg, "Gathering", "OreLimit", 500, "Same as WoodLimit, for the Ore job and GatherOre.", new AcceptableValueRange<int>(0, 100000));
		GatherWood = Bind(cfg, "Gathering", "GatherWood", "Wood, FineWood, RoundLog, ElderBark, YggdrasilWood, Blackwood, Resin", "What the Wood job collects. Targets are only worked if they drop something on this list that is still under its amount; drops not on the list are left on the ground. A name with no number uses WoodLimit. List format (same for every list): * = everything, Name = this one, -Name = never this one, Name:Number = this one with its own amount (0 = no limit). Names are prefab or in-game names.");
		GatherStone = Bind(cfg, "Gathering", "GatherStone", "Stone, Flint, BlackMarble, Grausten", "What the Stone job collects. Targets are only worked if they drop something on this list that is still under its amount; drops not on the list are left on the ground. A name with no number uses StoneLimit. List format (same for every list): * = everything, Name = this one, -Name = never this one, Name:Number = this one with its own amount (0 = no limit). Names are prefab or in-game names.");
		GatherOre = Bind(cfg, "Gathering", "GatherOre", "CopperOre, TinOre, IronScrap, SilverOre, BlackMetalScrap, CopperScrap, FlametalOreNew, Obsidian", "What the Ore job collects. Targets are only worked if they drop something on this list that is still under its amount; drops not on the list are left on the ground. A name with no number uses OreLimit. List format (same for every list): * = everything, Name = this one, -Name = never this one, Name:Number = this one with its own amount (0 = no limit). Names are prefab or in-game names.");
		ForageItems = Bind(cfg, "Gathering", "ForageItems", "Raspberry,Blueberries,Cloudberry,Mushroom,MushroomYellow,MushroomBlue,MushroomMagecap,MushroomJotunPuffs,MushroomSmokePuff,Thistle,Dandelion,Carrot,Turnip,Onion,Barley,Flax,RoyalJelly,Honey", "What the Forage job picks up. Name:Number stops picking that item once there are that many (pack + chests near home). List format (same for every list): * = everything, Name = this one, -Name = never this one, Name:Number = this one with its own amount (0 = no limit). Names are prefab or in-game names.");
		WoodSkipTrees = Bind(cfg, "Gathering", "WoodSkipTrees", "Oak", "Standing trees the Wood job never fells, matched on part of the tree name (Oak, Birch, Beech, Pine, Fir, SwampTree, YggaShoot...). Logs and stumps are still cleaned up.");
		BaseProtectRadius = Bind(cfg, "Gathering", "BaseProtectRadius", 10f, "The Wood job won't fell a standing tree with anything players built within this many meters of its trunk. 0 = off.", new AcceptableValueRange<float>(0f, 50f));
		JunkCleanup = Bind(cfg, "Gathering", "JunkCleanup", defaultValue: true, "When a gathering companion's pack is full and it has no home chest to unload into, it drops junk (materials and trophies that aren't on any gather list) to make room. Gear, tools, ammo, food, potions, seeds and anything on NeverDiscard are always kept.");
		NeverDiscard = Bind(cfg, "Gathering", "NeverDiscard", "Trophy", "Items the junk clean-up never drops, matched on part of the item name.");
		HarvestOverweightThreshold = Bind(cfg, "Gathering", "OverweightThreshold", 298f, "The weight (BASE value) at which a gathering companion - wood, stone, ore, forage and the farm plots - stops picking things up and heads home to unload. Items that raise carrying capacity add on top of this value, as in MaxCarryWeight: with the Megingjord in its bag (+150) and this at 298, it only heads home around 448. Hunt and Fish don't use this limit (they have their own bag-full settings).", new AcceptableValueRange<float>(50f, 500f));
		// [Hunting]
		HuntPrey = Bind(cfg, "Hunting", "Prey", "*", "What the Hunt job goes after: creatures that drop meat (and birds, see Birds). Name:Number with a creature or a meat name gives that meat its own target instead of MeatTarget / FeatherTarget. Tamed animals, bosses and companions are never hunted. The names to use are listed in OfflineCompanionsReborn.Hunting.txt in the config folder (written at startup). List format (same for every list): * = everything, Name = this one, -Name = never this one, Name:Number = this one with its own amount (0 = no limit). Names are item/creature names (prefab or in-game name).");
		HuntMeatTarget = Bind(cfg, "Hunting", "MeatTarget", 100, "Hunt a creature only while one of the meats it drops is under this many (counting the companion's pack and the chests near its home). Leather, hides and other drops are picked up as a bonus. 0 = no limit.", new AcceptableValueRange<int>(0, 10000));
		HuntRange = Bind(cfg, "Hunting", "Range", 40f, "With Follow off: how far (meters) from where it is hunting (home if it's near home, otherwise where the job was started) a companion looks for prey.", new AcceptableValueRange<float>(10f, 200f));
		HuntFollowRange = Bind(cfg, "Hunting", "FollowRange", 40f, "With Follow on: prey has to be within this many meters of you. With nothing that close the companion just follows you.", new AcceptableValueRange<float>(5f, 100f));
		HuntChaseExtra = Bind(cfg, "Hunting", "ChaseExtra", 15f, "How much further than Range / FollowRange a companion chases prey that runs off before letting it go.", new AcceptableValueRange<float>(0f, 100f));
		HuntBagFull = Bind(cfg, "Hunting", "BagFull", 0.9f, "With Follow off and home set: when the pack is this full (by weight, or out of free slots) the hunter walks home and puts its catch away, then goes back out.", new AcceptableValueRange<float>(0.3f, 1f));
		HuntBirds = Bind(cfg, "Hunting", "Birds", true, "Also shoot birds (crows, gulls, modded birds) for their feathers: when one lands within range, the hunter sneaks up and shoots it with a bow or crossbow (only if it has one with ammo), then picks up the drops. Birds on the Prey list's exclusions (-Crow, -Seagal) are left alone.");
		HuntFeatherTarget = Bind(cfg, "Hunting", "FeatherTarget", 100, "Shoot birds only while what they drop (feathers) is under this many, counting the pack and the chests near home. 0 = no limit.", new AcceptableValueRange<int>(0, 10000));
		HuntSneak = Bind(cfg, "Hunting", "Sneak", true, "Sneak up on skittish game (deer, hares...) that hasn't noticed anyone yet, like a player: crouched and not running, so it doesn't bolt. Crouching companions are quieter and harder to spot, like a sneaking player (light and Sneak skill count).");
		HuntThrowSpear = Bind(cfg, "Hunting", "ThrowSpear", true, "A hunter holding a spear throws it at skittish game (deer, hares...) from a distance, like a player, then walks over and picks it back up. Only when it has another weapon in its pack to fight on with.");
		HuntThrowRange = Bind(cfg, "Hunting", "ThrowRange", 12f, "How far (meters) a hunter throws its spear from. Further throws drop short more often.", new AcceptableValueRange<float>(4f, 25f));
		HuntThrowStandoff = Bind(cfg, "Hunting", "ThrowStandoff", 10f, "With a spear to throw at skittish game, a hunter sneaks only this close (meters), stops and throws, instead of walking right up to stab it. Never more than ThrowRange less 1 m.", new AcceptableValueRange<float>(3f, 25f));
		HuntSneakFrom = Bind(cfg, "Hunting", "SneakFrom", 20f, "Further than this (meters) from prey that hasn't noticed it, a hunter walks (no running) instead of sneaking, like a player - sneaking all the way costs a lot of stamina. Closer, it crouches and sneaks.", new AcceptableValueRange<float>(5f, 60f));
		HuntSwitchCloserBy = Bind(cfg, "Hunting", "SwitchCloserBy", 5f, "Before its first strike, a hunter switches to another animal that is at least this many meters closer than its prey (and see SwitchShare). 0 = never switches.", new AcceptableValueRange<float>(0f, 50f));
		HuntSwitchShare = Bind(cfg, "Hunting", "SwitchShare", 0.6f, "...and that is at most this share of the distance to its prey (0.6 = 60%).", new AcceptableValueRange<float>(0.1f, 1f));
		HuntSneakRestStamina = Bind(cfg, "Hunting", "SneakRestStamina", 0.3f, "Sneaking costs stamina, like for a player (standing still doesn't). When a hunter sneaking up on prey drops below this share of its stamina, it stops where it is, stays crouched and gets its breath back. 0 = never stops.", new AcceptableValueRange<float>(0f, 0.9f));
		HuntSneakResumeStamina = Bind(cfg, "Hunting", "SneakResumeStamina", 0.6f, "A hunter resting while sneaking carries on once its stamina is back above this share.", new AcceptableValueRange<float>(0.1f, 1f));
		HuntChaseRunStamina = Bind(cfg, "Hunting", "ChaseRunStamina", 0.3f, "Chasing prey that has bolted, a hunter only runs while its stamina is above this share; below it, it jogs and lets its stamina come back, like a player. 0 = always runs.", new AcceptableValueRange<float>(0f, 1f));
		// [Fishing]
		FishScanRadius = Bind(cfg, "Fishing", "ScanRadius", 30f, "How far (meters) from where the Fish job was started the companion looks for fish to catch. It stays around there and fishes the water nearby.", new AcceptableValueRange<float>(5f, 100f));
		FishList = Bind(cfg, "Fishing", "Fish", "*", "Which fish to catch. Name:Number gives that fish its own target instead of FishTarget. The fish names are listed in OfflineCompanionsReborn.Fishing.txt in the config folder (written at startup). List format (same for every list): * = everything, Name = this one, -Name = never this one, Name:Number = this one with its own amount (0 = no limit).");
		FishTarget = Bind(cfg, "Fishing", "FishTarget", 100, "Catch a kind of fish only while there are fewer than this many (pack + chests near home). 0 = no limit.", new AcceptableValueRange<int>(0, 10000));
		FishCastWait = Bind(cfg, "Fishing", "CastWait", 60f, "Seconds to wait for a bite before reeling in and trying again (a fish that swam off, the wrong spot).", new AcceptableValueRange<float>(10f, 600f));
		FishBagFull = Bind(cfg, "Fishing", "BagFull", 0.9f, "Near home: when the pack is this full (by weight, or out of free slots) the companion walks home and puts the catch away, then goes back to fishing.", new AcceptableValueRange<float>(0.3f, 1f));
		// [Farming]
		FarmScanRadius = Bind(cfg, "Farming", "Range", 30f, "How far (meters) around itself a farming companion looks for crops to pick, farm zones to plant and chests.", new AcceptableValueRange<float>(5f, 100f));
		FarmPlantTilled = Bind(cfg, "Farming", "PlantTilledGround", true, "Ground you've tilled with a cultivator (outside your farm zones) counts as an Any zone: a farming companion plants whatever seeds it has on it, on the tilled ground only. Off = it only plants in the farm zones you set.");
		FarmUnreachableForget = Bind(cfg, "Farming", "UnreachableSpotMemory", 600f, "How long (seconds) a farmer leaves a planting spot alone after finding it can't walk there (e.g. tilled ground on the other side of a wall). It tries it again after this.", new AcceptableValueRange<float>(30f, 3600f));
		FarmPlantSpacing = Bind(cfg, "Farming", "PlantSpacing", 0.75f, "The least space (meters) between plants. A plant that needs more room to grow gets that instead.", new AcceptableValueRange<float>(0.25f, 3f));
		SeedReserve = Bind(cfg, "Farming", "SeedMinimum", 20, "The fewest of each plantable item (carrot, turnip, onion seeds, barley, flax and so on) the farmer keeps, counting its pack and chests near its home. It never plants seeds if that would take them below this. Barley, flax and other crops that grow from themselves are still planted, and the farmer keeps this many in its pack instead of storing them.", new AcceptableValueRange<int>(0, 1000));
		SeedTarget = Bind(cfg, "Farming", "SeedTarget", 100, "While a seed is under this number, the farmer grows seed plants (planting carrots, turnips, onions...) alongside its regular crops to build the stock back up. Below SeedMinimum it grows only seed plants for that crop. At or above this number it grows only crops.", new AcceptableValueRange<int>(0, 5000));
		SeedCarry = Bind(cfg, "Farming", "SeedCarry", 40, "How many of each plantable item the farmer carries. When it opens a chest for seeds it takes up to this many of every type on the PlantAny list (and vegetables for seed plants while that seed is under SeedTarget), so it can fill the fields without walking back for each type. It keeps up to this many in its pack instead of storing them.", new AcceptableValueRange<int>(1, 200));
		PlantGroupSize = Bind(cfg, "Farming", "PlantGroupSize", 4, "How many of one plant the farmer puts in before switching to the next type, so a mixed field ends up in small blocks instead of one crop. 1 = fully mixed.", new AcceptableValueRange<int>(1, 100));
		FarmNextCropRange = Bind(cfg, "Farming", "NextCropRange", 3f, "After picking a crop, the farmer goes straight to the next ripe crop within this many meters, like working down a row, instead of pausing and scanning the whole field. 0 = always pause and scan.", new AcceptableValueRange<float>(0f, 10f));
		PlantAny = Bind(cfg, "Farming", "PlantAny", "CarrotSeeds, TurnipSeeds, OnionSeeds, Barley, Flax, MushroomJotunPuffs, MushroomMagecap, VineberrySeeds", "What the farmer plants in farm zones set to Any crop. * = every crop that grows on cultivated ground (tree saplings are never planted in an Any zone unless named). Name:Number stops planting that crop once there are that many of what it gives (carrots for CarrotSeeds, barley for Barley...), counting pack and chests near home. Items added by other mods work too. List format (same for every list): * = everything, Name = this one, -Name = never this one, Name:Number = this one with its own amount (0 = no limit). Names are item/creature names (prefab or in-game name).");
		// [Smelting]
		SmeltScanRadius = Bind(cfg, "Smelting", "Range", 25f, "How far (meters) around itself a smelting companion looks for smelters, kilns, furnaces and chests. Near home it also uses the chests around home ([General] HomeTripRange).", new AcceptableValueRange<float>(5f, 100f));
		SmeltMaxCarryOre = Bind(cfg, "Smelting", "MaxCarryOre", 20, "The most ore (or other input) a companion carries to a smelter in one trip.", new AcceptableValueRange<int>(1, 100));
		SmeltMaxCarryFuel = Bind(cfg, "Smelting", "MaxCarryFuel", 40, "The most fuel (coal, wood) a companion carries to a smelter in one trip.", new AcceptableValueRange<int>(1, 100));
		SmeltUsePiles = Bind(cfg, "Smelting", "UsePiles", true, "When the chests have none of what a smelter or kiln needs (wood for the kiln, fuel...), the Smelt job takes down a pile of it (Stack of Wood...) with a hammer, like a player, and uses that. Needs a hammer in its pack.");
		// [Cooking]
		CookRange = Bind(cfg, "Cooking", "Range", 25f, "How far around itself (meters) a cooking companion looks for cooking stations, fermenters and chests. Near home it also uses the chests around home ([General] HomeTripRange).", new AcceptableValueRange<float>(5f, 60f));
		CookIngredientReserve = Bind(cfg, "Cooking", "IngredientReserve", 20, "A raw ingredient is only used while the chests in range (plus the companion's bag) hold more than this many, so there is always some left for you to cook something yourself.", new AcceptableValueRange<int>(0, 1000));
		CookFoodTarget = Bind(cfg, "Cooking", "FoodTarget", 20, "Cook a food until the chests in range hold this many of it.", new AcceptableValueRange<int>(1, 1000));
		CookBatchSize = Bind(cfg, "Cooking", "BatchSize", 5, "How many of a food (or mead base) to make in one go, like a player cooking a batch. Still stops at FoodTarget / MeadBaseTarget and never goes into the ingredient reserve. On a cooking station or oven it's also limited by the free slots.", new AcceptableValueRange<int>(1, 50));
		CookFoods = Bind(cfg, "Cooking", "Foods", "*", "What may be cooked. Name:Number is that food's own target instead of FoodTarget. The names to use are listed in OfflineCompanionsReborn.Cooking.txt in the config folder (written at startup). List format (same for every list): * = everything, Name = this one, -Name = never this one, Name:Number = this one with its own amount (0 = no limit). Names are item/creature names (prefab or in-game name).");
		CookStoreWithCookedFood = Bind(cfg, "Cooking", "StoreWithCookedFood", true, "Put cooked food in a chest that already holds that food; if there is none, one that holds other cooked food; otherwise the chest nearest the cooking area. Off = the nearest chest with room.");
		CookMakeMead = Bind(cfg, "Cooking", "MakeMead", true, "Brew mead: make mead bases and fill fermenters with them. Off = food only (mead that has already finished in a fermenter is still tapped).");
		CookMeadBaseTarget = Bind(cfg, "Cooking", "MeadBaseTarget", 10, "Brew a mead base (one in MeadFocus) until the chests in range hold this many of it.", new AcceptableValueRange<int>(0, 1000));
		CookMeadTarget = Bind(cfg, "Cooking", "MeadTarget", 20, "Keep fermenters going with a mead base until the chests in range hold this many of the finished mead.", new AcceptableValueRange<int>(0, 1000));
		CookMeadFocus = Bind(cfg, "Cooking", "MeadFocus", "start: MeadBaseHealthMinor, MeadBaseStaminaMinor; eikthyr: MeadBaseHealthMinor, MeadBaseStaminaMinor, MeadBasePoisonResist; gdking: MeadBasePoisonResist, MeadBaseHealthMedium, MeadBaseStaminaMinor; bonemass: MeadBaseFrostResist, MeadBaseHealthMedium, MeadBaseStaminaMedium; dragon: MeadBaseHealthMedium, MeadBaseStaminaMedium, MeadBaseTasty, BarleyWineBase; goblinking: MeadBaseHealthMajor, MeadBaseStaminaMedium, MeadBaseEitrMinor, MeadBaseLightfoot; queen: BarleyWineBase, MeadBaseHealthMajor, MeadBaseStaminaLingering, MeadBaseHealthLingering, MeadBaseEitrMinor; fader: MeadBaseFrostResist, MeadBaseHealthMajor, MeadBaseHealthLingering, MeadBaseStaminaLingering, MeadBaseEitrMinor; kall: MeadBaseFrostResist, MeadBaseHealthMajor, MeadBaseHealthLingering, MeadBaseStaminaLingering, MeadBaseEitrMinor", "Mead bases to brew FIRST, by the last boss beaten, to get ready for the next one (other mead bases you know are still brewed after food, within MeadBaseTarget / MeadTarget). Format: boss: Base, Base; boss: ... in the order you beat them. A boss is its name or part of it (eikthyr, gdking = The Elder, bonemass, dragon = Moder, goblinking = Yagluth, queen, fader, kall = Kall Fimbulbringer, or a modded boss); start = none beaten yet. The bosses your game has, and the mead base names, are listed in OfflineCompanionsReborn.Cooking.txt in the config folder.");
		CookFuelLevel = Bind(cfg, "Cooking", "FuelLevel", 0.5f, "Cook job: keeps the fires under the cooking spots it uses (spit, iron cooking station, cauldron) and the oven at this share of full (0.5 = 50%). When one drops below half of that it tops it back up. Only enough to cook with - Repair and Restock fills fires further if it's on.", new AcceptableValueRange<float>(0.1f, 1f));
		CookUsePiles = Bind(cfg, "Cooking", "UsePiles", true, "Cook job: with no fuel in the chests, take down a pile of it (Stack of Wood...) with a hammer, like a player. Needs a hammer in its pack.");
		CookCauldron = Bind(cfg, "Cooking", "UseCauldron", true, "Cook job: use the cauldron (soups, stews and other crafted food).");
		CookPrepTable = Bind(cfg, "Cooking", "UsePrepTable", true, "Cook job: use the food preparation table (dough, raw pies and the like).");
		CookMeadKetill = Bind(cfg, "Cooking", "UseMeadKetill", true, "Cook job: use the mead ketill (mead bases).");
		CookCookingStation = Bind(cfg, "Cooking", "UseCookingStation", true, "Cook job: use the cooking station / spit over a fire.");
		CookIronCookingStation = Bind(cfg, "Cooking", "UseIronCookingStation", true, "Cook job: use the iron cooking station.");
		CookOven = Bind(cfg, "Cooking", "UseOven", true, "Cook job: use the stone oven (bread, pies; it also gets wood).");
		CookFermenter = Bind(cfg, "Cooking", "UseFermenter", true, "Cook job: use fermenters (filling them with mead bases and tapping the mead).");
		CookBeehives = Bind(cfg, "Cooking", "UseBeehives", true, "Cook job: use beehives (collecting honey).");
		CookOtherStations = Bind(cfg, "Cooking", "UseOther", true, "Cook job: use any other cooking station, e.g. one added by another mod.");
		// [RepairAndRestock]
		UpkeepRepair = Bind(cfg, "RepairAndRestock", "Repair", true, "Repair and Restock: repair damaged building pieces near home with a hammer, like a player (one swing per piece; the piece's crafting station has to be in range and a ward has to allow it; costs stamina and hammer wear). A hammer is fetched from a chest if it has none.");
		UpkeepRepairBelow = Bind(cfg, "RepairAndRestock", "RepairBelow", 0.75f, "Repair pieces below this much health (0.75 = 75% or less; 1 = any damage at all). Each repair is one swing that fixes the piece fully, like a player's.", new AcceptableValueRange<float>(0.1f, 1f));
		UpkeepReach = Bind(cfg, "RepairAndRestock", "Reach", 4.5f, "How far (meters, from the eyes) the companion can reach a piece to repair it. A player reaches about 5 m.", new AcceptableValueRange<float>(2f, 6f));
		UpkeepRestock = Bind(cfg, "RepairAndRestock", "Restock", true, "Repair and Restock: keep fires, torches and braziers near home fuelled, one piece of fuel at a time like a player. Fuel comes from the pack or is fetched from a chest; what's left over goes back in a chest. The Refuel... settings below pick which kinds.");
		UpkeepFuelBelow = Bind(cfg, "RepairAndRestock", "FuelBelow", 0.8f, "Top up a fire when its fuel is below this share of full (0.8 = 80%). It's filled all the way up.", new AcceptableValueRange<float>(0.1f, 1f));
		RefuelFires = Bind(cfg, "RepairAndRestock", "RefuelFires", true, "Restock: campfires, fire pits and bonfires.");
		RefuelHearths = Bind(cfg, "RepairAndRestock", "RefuelHearths", true, "Restock: hearths.");
		RefuelBraziers = Bind(cfg, "RepairAndRestock", "RefuelBraziers", true, "Restock: braziers (standing and hanging).");
		RefuelStandingTorches = Bind(cfg, "RepairAndRestock", "RefuelStandingTorches", true, "Restock: standing torches (wood, iron, green, blue, mist).");
		RefuelWallTorches = Bind(cfg, "RepairAndRestock", "RefuelWallTorches", true, "Restock: sconces and wall torches.");
		RefuelHotTubs = Bind(cfg, "RepairAndRestock", "RefuelHotTubs", true, "Restock: hot tubs.");
		RefuelOther = Bind(cfg, "RepairAndRestock", "RefuelOther", true, "Restock: anything else that burns fuel and isn't one of the kinds above (lanterns, the jack-o-turnip, things added by other mods).");
		UpkeepHoney = Bind(cfg, "RepairAndRestock", "Honey", true, "Repair and Restock: empty the beehives in its area and put the honey away in the chest that already holds the most honey.");
		UpkeepHoneyAt = Bind(cfg, "RepairAndRestock", "HoneyAt", 0.5f, "Empty a beehive once it's at least this full (0.5 = half full, 1 = only when full).", new AcceptableValueRange<float>(0.1f, 1f));
		UpkeepSort = Bind(cfg, "RepairAndRestock", "Sort", true, "Repair and Restock: sort the chests in its area (around home, or around where the job was started), like with like: each kind of item is carried to the chest that already holds the most of it, and part-stacks are merged.");
		UpkeepSortItems = Bind(cfg, "RepairAndRestock", "SortItems", "*", "Which items get sorted. Comma-separated: * = everything, Name = this one, -Name = never this one (e.g. *, -Coins, -Wood keeps coins and wood where they are). Prefab or in-game names.");
		UpkeepSortGear = Bind(cfg, "RepairAndRestock", "SortGear", false, "Also sort things that don't stack (weapons, armor, tools). Off: only stackable items are moved.");
		UpkeepPiles = Bind(cfg, "RepairAndRestock", "Piles", true, "Repair and Restock: stack wood, stone and the like from the chests into piles (the game's Stack of Wood, Stone Pile, Fine Wood Stack...), built with a hammer like a player. Only next to piles of the same kind you already built - it never starts a new pile somewhere on its own.");
		UpkeepPileKeep = Bind(cfg, "RepairAndRestock", "PileKeep", 100, "How many of each material stay in the chests for building: what's above this goes into piles, and when the chests drop below it a pile is taken down (with the hammer, like a player) and the material put back in the chests.", new AcceptableValueRange<int>(0, 10000));
		UpkeepPileItems = Bind(cfg, "RepairAndRestock", "PileItems", "*", "Which materials go into piles. Comma-separated: * = every material you have piles of, Name = this one, -Name = never this one, Name:Number = keep this many of it in the chests instead of PileKeep. Prefab or in-game names (Wood, Stone, FineWood, RoundLog...).");
		UpkeepAwayRadius = Bind(cfg, "RepairAndRestock", "AwayRadius", 30f, "Started away from home (or with no home set): how far (meters) from where the job was started it repairs, restocks and sorts. Started near home, the range is [General] HomeTripRange around home.", new AcceptableValueRange<float>(5f, 100f));
		UpkeepWorkTime = Bind(cfg, "RepairAndRestock", "WorkTime", 1f, "Seconds per action: a repair swing, opening a chest. Adding fuel takes a bit over half of this per piece. (The game won't repair the same piece more than once a second.)", new AcceptableValueRange<float>(0.3f, 5f));
		UpkeepGiveUp = Bind(cfg, "RepairAndRestock", "GiveUp", 90f, "Seconds it keeps trying to get within reach of a piece, fire or chest before leaving it for a few minutes.", new AcceptableValueRange<float>(20f, 600f));
		UpkeepOrderWait = Bind(cfg, "RepairAndRestock", "OrderWait", 30f, "After you give the companion an order (send it somewhere, sit by a fire...), it waits this many seconds once the order is done before going back to its chores.", new AcceptableValueRange<float>(0f, 600f));
		// [Idle]
		IdleEnabled = Bind(cfg, "Idle", "Enabled", true, "When its job has had nothing to do for a while (and it isn't following you), a companion idles near its job instead of standing still: small chores, sitting down, wandering about, chatting. It stops the moment the job has work again.");
		IdleStartAfter = Bind(cfg, "Idle", "StartAfter", 10f, "Seconds with nothing to do before it starts idling.", new AcceptableValueRange<float>(1f, 300f));
		IdleRadius = Bind(cfg, "Idle", "Radius", 8f, "How far (meters) from where its job left it a companion wanders, sits or looks for company while idle.", new AcceptableValueRange<float>(2f, 40f));
		IdleChores = Bind(cfg, "Idle", "Chores", true, "While idle, do small chores nearby: repair damaged pieces, top up fires, collect honey, put away what it carries (uses the Repair and Restock settings).");
		IdleChoreRadius = Bind(cfg, "Idle", "ChoreRadius", 15f, "How far (meters) it looks for idle chores.", new AcceptableValueRange<float>(2f, 60f));
		IdleSit = Bind(cfg, "Idle", "Sit", true, "While idle, sit down for a while - on a free chair or bench nearby, else on the ground.");
		IdleWander = Bind(cfg, "Idle", "Wander", true, "While idle, stroll about nearby and look over stations and chests.");
		IdleSocialize = Bind(cfg, "Idle", "Socialize", true, "While idle, go over to you or another companion nearby and chat now and then.");
		IdleSpeechInterval = Bind(cfg, "Idle", "SpeechInterval", 300f, "At most every this many seconds it says why it's idle (and chats while socializing about half as often). 0 = never.", new AcceptableValueRange<float>(0f, 3600f));
		// [Speech]
		SpeechChatter = Bind(cfg, "Speech", "Chatter", defaultValue: false, "Companions talk to themselves now and then when nothing is happening: a line about a fight nearby, being too heavy, being hungry, worn gear, no bed, the job they're on, or just following you. Off (Phase 3.3c) = no random speech at all. They still speak when something actually happens: an order (chat or voice), a change of mode (\"On it!\"), the AI replies, a fish caught, a farming/repair/smelting milestone, a tombstone found, unloading near home. The idle lines are separate ([Idle] SpeechInterval). Turn it on to go back to the old behaviour.");
		SpeechMinInterval = Bind(cfg, "Speech", "MinInterval", 15f, "Shortest time (seconds) between a companion's lines when nothing is happening. Only used while Chatter is on.", new AcceptableValueRange<float>(5f, 120f));
		SpeechMaxInterval = Bind(cfg, "Speech", "MaxInterval", 30f, "Longest time (seconds) between a companion's lines when nothing is happening. Only used while Chatter is on.", new AcceptableValueRange<float>(10f, 300f));
		SpeechDuration = Bind(cfg, "Speech", "Duration", 5f, "Seconds a line of speech stays on screen.", new AcceptableValueRange<float>(1f, 15f));
		SpeechSayCooldown = Bind(cfg, "Speech", "SayCooldown", 3f, "Least time (seconds) between two lines from any companion, so they don't all talk at once.", new AcceptableValueRange<float>(1f, 30f));
		SpeechCullDistance = Bind(cfg, "Speech", "CullDistance", 20f, "Speech from companions further away than this (meters) isn't shown.", new AcceptableValueRange<float>(5f, 60f));
		// [Advanced]
		RosterSaveInterval = Bind(cfg, "Advanced", "RosterSaveInterval", 60f, "Seconds between saves of your companions to the roster (their gear and settings), used to recover a lost companion.", new AcceptableValueRange<float>(10f, 600f));
		HarvestScanRadius = Bind(cfg, "Advanced", "GatheringSearchRadius", 50f, "Gathering: how far (meters) around itself a companion looks for trees, rocks and plants when it's following you or sent to one with the command key (with Follow off it's [Gathering] Range). Phase 3.3c: 50, was 30 - while following, the companion can be 25 m from you ([Gathering] FollowRange) and the target up to 25 m from you on the other side, so the ring it searches has to reach about 50 m.", new AcceptableValueRange<float>(5f, 100f));
		HarvestScanInterval = Bind(cfg, "Advanced", "GatheringScanInterval", 4f, "Gathering: seconds between looks for the next tree, rock or plant.", new AcceptableValueRange<float>(1f, 30f));
		HarvestAttackInterval = Bind(cfg, "Advanced", "GatheringSwingInterval", 2.5f, "Gathering: seconds between swings at a tree or rock.", new AcceptableValueRange<float>(0.5f, 5f));
		HarvestDropScanRadius = Bind(cfg, "Advanced", "GatheringDropRadius", 12f, "Gathering: how far (meters) around a felled tree or broken rock the drops are picked up. Phase 3.3c: 12, was 8 - a tree can fall (or a rock spread out) further than 8 m, and the drops were being left behind.", new AcceptableValueRange<float>(2f, 20f));
		HarvestBlacklistDuration = Bind(cfg, "Advanced", "GatheringSkipTime", 60f, "Gathering: seconds a tree, rock or plant it couldn't get to is skipped.", new AcceptableValueRange<float>(5f, 300f));
		FarmScanInterval = Bind(cfg, "Advanced", "FarmScanInterval", 4f, "Farming: seconds between looks for crops to pick and ground to plant.", new AcceptableValueRange<float>(1f, 30f));
		FarmUseDistance = Bind(cfg, "Advanced", "FarmUseDistance", 2f, "Farming: how close (meters) a companion has to be to a crop or chest to use it.", new AcceptableValueRange<float>(1f, 5f));
		SmeltScanInterval = Bind(cfg, "Advanced", "SmeltScanInterval", 3f, "Smelting: seconds between looks at the smelters and chests.", new AcceptableValueRange<float>(1f, 30f));
		SmeltUseDistance = Bind(cfg, "Advanced", "SmeltUseDistance", 2.5f, "Smelting: how close (meters) a companion has to be to a smelter to put things in or take them out.", new AcceptableValueRange<float>(1f, 5f));
		RepairScanInterval = Bind(cfg, "Advanced", "GearRepairScanInterval", 5f, "Gear repair: seconds between checks of a companion's gear for wear.", new AcceptableValueRange<float>(1f, 30f));
		RepairTickInterval = Bind(cfg, "Advanced", "GearRepairTickInterval", 0.8f, "Gear repair: seconds per item repaired at the station.", new AcceptableValueRange<float>(0.1f, 5f));
		RepairUseDistance = Bind(cfg, "Advanced", "GearRepairUseDistance", 2.5f, "Gear repair: how close (meters) a companion has to be to the station to repair.", new AcceptableValueRange<float>(1f, 5f));
		UpkeepScanInterval = Bind(cfg, "Advanced", "RestockScanInterval", 5f, "Repair and Restock: seconds between looks for something to repair, refuel or sort.", new AcceptableValueRange<float>(1f, 60f));
		AttackCooldown = Bind(cfg, "Advanced", "AttackCooldown", 0.225f, "Combat: least time (seconds) between a companion's attacks, on top of each weapon's own attack timing.", new AcceptableValueRange<float>(0.1f, 2f));
		MeleeTooClose = Bind(cfg, "Advanced", "MeleeTooClose", 0.3f, "Combat: closer than this (meters, from the edge of the target) a companion steps back before swinging a melee weapon - a stab or swing that starts inside the target misses, like a player standing on top of a boar.", new AcceptableValueRange<float>(0f, 1.5f));
		MeleeReachMargin = Bind(cfg, "Advanced", "MeleeReachMargin", 0.2f, "Combat: a companion swings a melee weapon only once it is at least this much (meters) inside the weapon's reach, not right at the edge of it, where hits are hit-or-miss.", new AcceptableValueRange<float>(0f, 1f));
		BlockReactionTime = Bind(cfg, "Advanced", "BlockReactionTime", 0.2f, "Combat: seconds between an enemy starting a swing at a companion and the companion raising its guard.", new AcceptableValueRange<float>(0f, 1f));
		LogPlayerCombat = Bind(cfg, "Advanced", "LogPlayerCombat", true, "Testing: write your own fighting to the log (debug level, lines starting [You]) - swings, guard, blocks and parries, enemy swings at you and how far you were - to help tune how companions fight. Will be removed once combat is tuned.");
		FoodRegenInterval = Bind(cfg, "Advanced", "FoodRegenInterval", 10f, "Food: seconds between health regeneration ticks from food (a player's is 10).", new AcceptableValueRange<float>(1f, 60f));
		ConsumeCheckInterval = Bind(cfg, "Advanced", "FoodCheckInterval", 1f, "Food: seconds between checks whether a companion should eat or drink.", new AcceptableValueRange<float>(0.5f, 10f));
		TombstoneScanInterval = Bind(cfg, "Advanced", "TombstoneScanInterval", 5f, "Death: seconds between looks for its own leftover tombstone nearby.", new AcceptableValueRange<float>(1f, 30f));
		TombstoneNavTimeout = Bind(cfg, "Advanced", "TombstoneNavTimeout", 120f, "Death: seconds a companion keeps trying to get to its tombstone before giving up.", new AcceptableValueRange<float>(10f, 600f));
		// O diagnostico de UI: ligado, o log diz quem esta na frente dos controles (ver UI/ModUiAudit.cs). Fica na
		// [Advanced] e nao na [AI] porque ele nao e da camada de IA: ele audita a UI do mod inteira - o painel F8 e
		// o painel da IA, e a faixa de camadas que os dois compartilham (UI/ModUiLayers.cs). O [AI] EcoDiag esta la
		// porque diagnostica um componente da IA (a voz do companheiro); este e o bucket dos ajustes que ninguem
		// mexe no dia a dia, que e o caso de um despejo de diagnostico.
		UiDiag = Bind(cfg, "Advanced", "UiDiag", defaultValue: false, "UI diagnostics: when on, writes to the log what is in front of the controls of the F8 panel and the AI panel (once per session, the first time each panel is opened): how many click targets each panel has (the controls and the ones that are there on purpose), every active layer in the scene (name and order) and, for each canvas above the panel, the targets that cover the controls column. It is what you turn on when a click or the UI looks odd (a button that doesn't answer, text on top of something, a swallowed click): the log says who is in front. Off (default) writes nothing and costs nothing; on, the log grows a lot (one summary line per panel, plus one line per suspect target). It changes nothing in the game - it only reports.");

		// [AI] - Phase 3.1: the player types an order in the game chat and an LLM (Groq's free tier by default, a local
		// LM Studio / Ollama, or any OpenAI-compatible /chat/completions endpoint) turns it into an action. See CompanionChatAI.
		AiEnabled = Bind(cfg, "AI", "Enabled", defaultValue: true, "Phase 3.1: type an order in the game chat (\"collect 20 wood\", \"follow\", \"stop\") and the companions carry it out. The order is interpreted by an LLM (Groq by default, or a local server, or OpenRouter) through an OpenAI-compatible API. Off = nothing is sent to the model.");
		AiBaseUrl = Bind(cfg, "AI", "BaseUrl", "https://api.groq.com/openai/v1", "Base URL of the OpenAI-compatible server. Default: Groq (https://api.groq.com/openai/v1), which asks for the ApiKey - the free key is at console.groq.com/keys. Alternatives: LM Studio (http://127.0.0.1:1234/v1) or Ollama (http://127.0.0.1:11434/v1) to run everything locally - with those two use 127.0.0.1 instead of localhost (on Windows localhost resolves to IPv6 first and the server only listens on IPv4, which costs about 2.5 s per order); or OpenRouter (https://openrouter.ai/api/v1), which works but the free tier is usually much slower. /chat/completions is added automatically. This is a text option: edit it in the .cfg file.");
		AiModel = Bind(cfg, "AI", "Model", "openai/gpt-oss-20b", "Name of the model sent to the server. Default: openai/gpt-oss-20b (Groq, free tier). Others: openrouter/free on OpenRouter; on LM Studio use the name the server lists (e.g. qwen2.5-3b-instruct). Text option: edit it in the .cfg file.");
		AiApiKey = Bind(cfg, "AI", "ApiKey", "", "API key, if the server asks for one. On Groq it is required (the free one is at console.groq.com/keys) and on OpenRouter too (openrouter.ai/keys). Leave it empty for LM Studio/Ollama running on your machine. Text option: edit it in the .cfg file. Never put a key in a shared config.");
		AiTemperature = Bind(cfg, "AI", "Temperature", 0.1f, "How much the model may improvise. Keep it low so the same order always gives the same JSON.", new AcceptableValueRange<float>(0f, 1f));
		AiTimeoutSeconds = Bind(cfg, "AI", "TimeoutSeconds", 30, "Seconds to wait for the model before giving up. The request runs in the background, so a slow model never freezes the game.", new AcceptableValueRange<int>(1, 300));
		AiPrefix = Bind(cfg, "AI", "Prefix", "", "Only chat messages that start with this go to the model (for example /c). Empty = every chat message is sent. Text option: edit it in the .cfg file.");
		AiShowResponse = Bind(cfg, "AI", "ShowReplyInChat", defaultValue: true, "Shows the model's raw reply in the chat, so you can see what it understood. Useful while testing; turn it off for a cleaner chat.");
		AiCooldown = Bind(cfg, "AI", "CooldownSeconds", 3f, "Minimum time (in seconds) between two orders, so a burst of chat messages does not flood the model.", new AcceptableValueRange<float>(0f, 60f));
		AiTargetRange = Bind(cfg, "AI", "TargetRange", 40f, "Phase 3.6: how far from you (in metres) an order that names a creature searches for it (\"attack the greydwarf\"). The creature you are aiming at always counts, however far away it is. 40 m is the same leash at which the combat code gives up on a target.", new AcceptableValueRange<float>(5f, 200f));
		AiPersonality = Bind(cfg, "AI", "Personality", "", "Phase 3.5: your companion's personality in your own words - how it speaks and behaves (\"speaks like a loyal, blunt viking\", \"a sarcastic mage who complains about the cold\"). This goes into the model's instructions, so the companion answers in that tone. When filled in it wins over PersonalityPreset. Up to 600 characters. Text option: edit it in the .cfg file.");
		AiPersonalityPreset = Bind(cfg, "AI", "PersonalityPreset", "", "Phase 3.5: a ready-made personality, written in Portuguese inside the mod: viking (loyal and fearless), mago (old and sarcastic), sarcastico (dry jokes), guia (patient and didactic), or custom (uses Personality). Empty or unknown = the default (a loyal, practical companion). Text option: edit it in the .cfg file.");
		AiMemory = Bind(cfg, "AI", "Memory", 4, "Phase 3.5: how many recent exchanges (one message from you + one from the companion) the companion remembers and sends along with the next message, to keep the thread of the conversation. 0 = no memory. Kept per companion, in memory only (nothing is saved in the world) and forgotten when the companion dies or is dismissed. Every exchange makes the prompt the model has to read bigger, so keep it small for faster replies (Phase 3.3b: 4, was 6).", new AcceptableValueRange<int>(0, 12));
		AiConversation = Bind(cfg, "AI", "Conversation", defaultValue: true, "Phase 3.5: when the message is NOT an order (\"good morning\", \"how are you?\", \"what did you think of my house?\"), the companion answers as conversation: it says the reply out loud (speech bubble) and shows it in the chat. Orders keep working exactly as before. Off = only orders are understood, as in Phase 3.1.");
		AiStructuredOutput = Bind(cfg, "AI", "StructuredOutput", defaultValue: true, "Asks the server to force the JSON format (response_format/json_schema). It keeps the model's reply always valid and is what allows small, fast models (e.g. a 3B) to be used without them answering loose text. Turn it off if your server does not support it - the mod falls back to the old format on its own and says so in the log.");

		// [AI] - local STT (the in-process Whisper.net spike, Phase 3.x): test only, not part of the voice pipeline.
		SttTest = Bind(cfg, "AI", "SttTest", defaultValue: false, "SPIKE (test): runs the in-process Whisper.net speech recognition test by itself, about 25 seconds after the plugin loads, and writes the result to the log. Off = only the \"modia_stt_teste\" console command runs it. It is a simple test: it does not listen to the microphone and changes no companion behaviour.");
		SttModel = Bind(cfg, "AI", "SttModel", @"whisper\ggml-small-q5_1.bin", "Whisper model the speech recognition loads (the voice orders AND the \"modia_stt_teste\" console command), relative to the plugin folder or an absolute path. Default whisper\\ggml-small-q5_1.bin (181 MB, Phase 3.3b - much better in Portuguese than the old whisper\\ggml-base-q5_1.bin, 57 MB, which is no longer shipped - download it into the whisper folder and point this setting at it if you want the faster, less accurate one: measured on a 16-core CPU with 8 threads, 638 ms per 2 s clip with base against 2187 ms with small, and the time barely changes with the clip length because the encoder always works on a 30 s window). Text option: edit it in the .cfg file.");
		SttWav = Bind(cfg, "AI", "SttWavTest", @"whisper\jfk.wav", "SPIKE (test): the WAV file to transcribe, relative to the plugin folder (or an absolute path). It must be PCM WAV (16 kHz mono is ideal). Text option: edit it in the .cfg file.");

		// [AI] - Phase 3.3: voice orders (push-to-talk + local STT with Whisper.net). The recognised text enters the
		// SAME path as the chat orders. See AI/VoiceInput.cs and AI/WhisperStt.cs.
		VozEnabled = Bind(cfg, "AI", "VoiceEnabled", defaultValue: true, "Phase 3.3: hold VoiceKey and speak, and the companion accepts what you said - the same orders you can type in the chat (\"collect 10 wood\", \"follow\", \"attack the greydwarf\"). The speech is recognised locally, inside the game, by Whisper.net with the SttModel model: nothing is sent anywhere. Off = the key does nothing (and, in Phase 3.3b, the key goes back to doing what the game did with it).");
		VozTecla = Bind(cfg, "AI", "VoiceKey", KeyCode.V, "Phase 3.3: push-to-talk key. Hold it while you speak, release it for the speech to be recognised and carried out. It only works with the game focused and no window open (inventory, map, menu, chat, console, the mod's own panels). NOTE (Phase 3.3b): V is also the game's \"Toggle Auto-Pickup\". While VoiceEnabled is on, the mod blocks that toggle when the game's auto-pickup is bound to this same key, so the key stays free for talking - rebind auto-pickup in the game's own controls screen (or pick another key here) if you want it back.");
		VozLanguage = Bind(cfg, "AI", "VoiceLanguage", "auto", "Multilanguage: language of the speech, as a Whisper code (pt, en, es, fr) - or \"auto\" (the default) to follow the game language, which is what makes the companion understand orders in whatever language you are playing. An explicit code always wins. Text option: edit it in the .cfg file.");
		VozMaxSeconds = Bind(cfg, "AI", "VoiceMaxSeconds", 10f, "Phase 3.3: longest recording (in seconds) for a voice order. Holding the key longer than this stops the recording and transcribes what was captured.", new AcceptableValueRange<float>(1f, 30f));
		VozSilenceThreshold = Bind(cfg, "AI", "VoiceSilenceThreshold", 0.01f, "Phase 3.3: how loud the audio must be (peak level of a 10 ms window, from 0 to 1) to count as speech when the silence at the start and the end of the recording is trimmed. Phase 3.3b: 0.01 (was 0.02) - at 0.02 the quiet start of a word was often cut, and that is what made short orders come back wrong. Raise it if your microphone picks up a lot of room noise.", new AcceptableValueRange<float>(0f, 0.5f));
		VozPromptInitial = Bind(cfg, "AI", "VoiceInitialPrompt", "", "Phase 3.3b: initial prompt sent to Whisper before every transcription. It biases the vocabulary, and that is what makes short orders (\"collect 10 wood\") come out right instead of guessed. Empty (the default) = the orders vocabulary of the game language, written by the mod; anything you type here wins. Write \"none\" for no bias at all. Text option: edit it in the .cfg file.");
		VozDevice = Bind(cfg, "AI", "VoiceDevice", 0, "Phase 3.3: which microphone to record from, as the index of the list the log prints at startup ([STT] Push-to-talk: ... 0: name, 1: name). 0 = the first one (the Windows default).", new AcceptableValueRange<int>(0, 8));
		VozShowText = Bind(cfg, "AI", "VoiceShowText", defaultValue: true, "Phase 3.3: shows what the microphone heard (\"Entendi: \"collect 10 wood\"\") in the chat and on the HUD. Phase 3.3d: the transcription appears in the PLAYER's own bubble (not the companion's), so a wrong transcription is easy to spot - the one who answers is the companion, with its own bubble and VOICE. Off = the recognised text is only written to the log.");

		// [AI] - Phase 3.7: the in-game AI panel. See UI/CompanionAiPanel.cs. Phase 3.10/3.15: the panel has one tab per
		// companion (voice, personality, name and tier of that companion) plus the AI tab (summoner, volume, speed,
		// memory, conversation, push-to-talk). The global personality and voice live in this file / the F8 panel.
		AiPanelKey = Bind(cfg, "AI", "PanelKey", KeyCode.I, "Phase 3.7: opens the AI panel in game. The AI tab has the free companion summoner, the speech volume and speed, the memory, the conversation and the push-to-talk key. Each companion has its own tab, with the name, the tier (reborn), the voice (with the Listen button) and ITS personality - the [AI] values below are the default of whoever has no choice of its own. Every change is saved right away. The default is I: neither the game nor the mod uses that key. The key is not stolen from a text box (the chat, the console and the search field are left in peace). Key option: edit it in the .cfg file or click it in the F8 panel.");

		// [AI] - Phase 3.4: local TTS (Kokoro-82M via sherpa-onnx, in-process). The companion speaks the replies out
		// loud, at its own position. See AI/CompanionTts.cs.
		TtsEnabled = Bind(cfg, "AI", "TtsEnabled", defaultValue: true, "Phase 3.4: the companion SPEAKS the replies out loud. The speech is synthesised on your computer by Kokoro-82M (via sherpa-onnx, inside the game, no child process and nothing leaving your machine), in the TtsVoice voice, and comes out where the companion is (not in the middle of the screen). Off = it only shows the bubble and the chat, as before. The first speech of every session waits for the model to load (~1 s, in the background, when you enter the world).");
		TtsVoice = Bind(cfg, "AI", "TtsVoice", 0, "Multilanguage: which Kokoro voice to use. 0 (the default) = automatic: the default voice of the game's language (42 = pf_dora in Portuguese, 3 = af_heart in English, 28 = ef_dora in Spanish, 30 = ff_siwis in French). An explicit id always wins while it belongs to the game's language: the Brazilian Portuguese voices are 42 = pf_dora (feminine), 43 = pm_alex (masculine) and 44 = pm_santa (masculine). A voice of another language is not used (it would speak with the wrong accent) - each companion's own voice follows the same rule.", new AcceptableValueRange<int>(0, 100));
		TtsSpeed = Bind(cfg, "AI", "TtsSpeed", 1f, "Phase 3.4: how fast the companion speaks (1 = normal; above 1 speaks faster). It also makes the speech shorter.", new AcceptableValueRange<float>(0.5f, 2f));
		TtsThreads = Bind(cfg, "AI", "TtsThreads", 4, "Phase 3.4: how many CPU cores the speech synthesis may use. Measured on this machine (Ryzen 7 5700X): 4 threads generate faster than real time (RTF ~0.83), so the speech is ready before the companion needs it; more threads do not speed it up and compete with the game.", new AcceptableValueRange<int>(1, 16));
		TtsVolume = Bind(cfg, "AI", "TtsVolume", 1f, "Phase 3.4: volume of the companion's voice (0 = muted, 1 = the game's normal volume).", new AcceptableValueRange<float>(0f, 1f));
		TtsModel = Bind(cfg, "AI", "TtsModel", @"tts\kokoro\model.int8.onnx", "Phase 3.4: the Kokoro model the companion speaks with (relative to the plugin folder, or an absolute path). The voices (voices.bin), the tokens (tokens.txt) and espeak-ng-data are looked for in the SAME folder as the model. Text option: edit it in the .cfg file.");
		TtsLanguage = Bind(cfg, "AI", "TtsLanguage", "auto", "Multilanguage: language of the spoken text, in the format Kokoro expects - or \"auto\" (the default) to follow the game language (pt-br, en-us, es, fr). This model is multi-lingual and REQUIRES a language: \"pt-br\" is Brazilian Portuguese (\"pt\" alone is European Portuguese), \"en-us\" and \"en-gb\" are the two English ones (the voice has to match it). An explicit code always wins. Text option: edit it in the .cfg file.");
		TtsCache = Bind(cfg, "AI", "TtsCache", defaultValue: true, "Phase 3.4: keeps the speeches already synthesised (text + voice + speed) to play them again instantly. The mod's fixed lines (\"Ok: gathering wood (10).\") come out instantly; the cache holds up to 32 MB of audio (the oldest sentences are dropped first). Off = every speech is synthesised again.");
		TtsSpeakReplies = Bind(cfg, "AI", "TtsSpeakReplies", defaultValue: true, "Phase 3.4: speaks the model's reply out loud (the \"reply\" field). In a conversation it is its answer; in an order, when TtsSpeakModLines is off, it is the confirmation in its own way (\"Deixa comigo.\").");
		TtsSpeakModLines = Bind(cfg, "AI", "TtsSpeakModLines", defaultValue: true, "Phase 3.4: speaks out loud the mod's own line that shows in the chat/HUD (\"Ok: coletar madeira (10).\", \"Atacando o troll.\", \"Já temos o suficiente.\"). In an ORDER it wins over the model's reply (the voice follows what is written on the screen); turn it off to hear the model's reply instead. Nothing is spoken twice: with both options on, the line that is not spoken only goes to the log.");

		// [AI] - Phase 3.4 (part 3): the echo test switches. Every default = the way it is today.
		TtsReverbZoneMix = Bind(cfg, "AI", "TtsReverbZoneMix", 0f, "How much of the world's reverb zones goes into the companion's voice. 0 (default) = dry voice. Valheim has a global reverb zone (1000 m radius, 4 s tail) that made the companion's speech 'echo like in a cave' - the game's music uses 0 and the ambience 0.19. Raise it a little (e.g. 0.19) if you want a subtle 'space' in the voice. It applies to the next sentence, no restart.", new AcceptableValueRange<float>(0f, 1f));
		TtsEcoDiag = Bind(cfg, "AI", "EcoDiag", defaultValue: false, "Audio diagnostics: when on, writes to the log (once per session, on the first spoken sentence) the scene's AudioListener, the world's reverb zones and how the game's own sources are configured (mixer, reverb, bypass). Use it to investigate any sound problem with the companion. It does not change the sound - it only reports.");

		// Fase 3.3b: the voice defaults changed after the first test in game. A .cfg written before this version still
		// holds the old values and BepInEx keeps whatever is in the file, so a value that is EXACTLY the old default is
		// moved to the new one (and said so in the log). Anything else was chosen by the player and is left alone.
		ApplyVoiceDefaultsPhase33b();

		// 1.4.149: settings moved/renamed in the config clean-up keep the values players set; leftovers are removed.
		// 1.4.150: the fix-ups below only run on such an upgrade (a file from before the clean-up), not on every start -
		// "*" for ForageItems or 40/90 for the view are fine choices once someone makes them on purpose.
		bool upgraded = MigrateAndClean(cfg);
		// 1.4.149: ViewRange/ViewAngle were never applied (20 m / 120 were hard-coded); a file still holding the old
		// unused defaults (40 / 90) gets the values companions have always really used, so nothing changes in game
		if (upgraded && Mathf.Approximately(ViewRange.Value, 40f) && Mathf.Approximately(ViewAngle.Value, 90f))
		{
			ViewRange.Value = 20f;
			ViewAngle.Value = 120f;
		}
		if (upgraded && ForageItems.Value.Trim() == "*")
		{
			ForageItems.Value = (string)ForageItems.DefaultValue;
		}
		// Fase 3.3c: the gathering radii grew (the companion searches and works further away). Same rule as 3.3b: a
		// value that is EXACTLY the old default is moved to the new one and said so in the log; anything else stays.
		// This runs after the config clean-up above, so a file still using the pre-1.4.149 names ([Harvest] FollowRange)
		// is covered too.
		ApplyGatheringDefaultsPhase33c();
		// Multilanguage M2 fix: the four [AI] language/voice defaults became "auto" (the game language decides). A
		// file written before that still holds the old fixed values, and BepInEx keeps whatever is in the file - so a
		// value that is EXACTLY the old default is moved to the new one (see ApplyMultilanguageDefaults).
		ApplyMultilanguageDefaults();
		// 1.4.92: a config file still holding the 1.4.87 draft gets the reviewed table
		if (CookMeadFocus.Value == "start: MeadBaseHealthMinor, MeadBaseStaminaMinor; eikthyr: MeadBaseHealthMinor, MeadBaseStaminaMinor, MeadBasePoisonResist; gdking: MeadBaseHealthMinor, MeadBaseStaminaMinor, MeadBasePoisonResist; bonemass: MeadBaseHealthMedium, MeadBaseStaminaMedium, MeadBaseFrostResist; dragon: MeadBaseHealthMedium, MeadBaseStaminaMedium, MeadBaseFrostResist, BarleyWineBase; goblinking: MeadBaseHealthMajor, MeadBaseStaminaMedium, MeadBaseEitrMinor, BarleyWineBase; queen: MeadBaseHealthMajor, MeadBaseStaminaLingering, MeadBaseEitrMinor, BarleyWineBase; fader: MeadBaseHealthMajor, MeadBaseHealthLingering, MeadBaseStaminaLingering, MeadBaseEitrMinor, BarleyWineBase")
		{
			CookMeadFocus.Value = "start: MeadBaseHealthMinor, MeadBaseStaminaMinor; eikthyr: MeadBaseHealthMinor, MeadBaseStaminaMinor, MeadBasePoisonResist; gdking: MeadBasePoisonResist, MeadBaseHealthMedium, MeadBaseStaminaMinor; bonemass: MeadBaseFrostResist, MeadBaseHealthMedium, MeadBaseStaminaMedium; dragon: MeadBaseHealthMedium, MeadBaseStaminaMedium, MeadBaseTasty, BarleyWineBase; goblinking: MeadBaseHealthMajor, MeadBaseStaminaMedium, MeadBaseEitrMinor, MeadBaseLightfoot; queen: BarleyWineBase, MeadBaseHealthMajor, MeadBaseStaminaLingering, MeadBaseHealthLingering, MeadBaseEitrMinor; fader: MeadBaseFrostResist, MeadBaseHealthMajor, MeadBaseHealthLingering, MeadBaseStaminaLingering, MeadBaseEitrMinor; kall: MeadBaseFrostResist, MeadBaseHealthMajor, MeadBaseHealthLingering, MeadBaseStaminaLingering, MeadBaseEitrMinor";
		}
	}

	/// <summary>
	/// Fase 3.3b: three voice defaults changed after the first test in game (the model, the silence threshold and the
	/// memory size). A config file written before this version still holds the old value, and BepInEx keeps whatever
	/// is in the file - so a value that is <b>exactly the old default</b> is moved to the new one (and the log says
	/// so). A value the player picked is never touched.
	/// </summary>
	private static void ApplyVoiceDefaultsPhase33b()
	{
		// the model: only switch when the new file is really on disk, otherwise the voice orders would stop working
		if (SttModel != null && string.Equals(SttModel.Value, WhisperStt.PreviousModelRelative, StringComparison.OrdinalIgnoreCase))
		{
			string next = (string)SttModel.DefaultValue;
			string path = WhisperStt.ResolvePath(WhisperStt.PluginDirectory(), next, next);
			if (System.IO.File.Exists(path))
			{
				SttModel.Value = next;
				CompanionsPlugin.Log?.LogInfo($"[IA] Fase 3.3b: [AI] SttModel estava no valor antigo ({WhisperStt.PreviousModelRelative}) e o modelo novo esta no disco - "
					+ $"passando para \"{next}\" (bem mais preciso em PT-BR; o base continua em disco se voce quiser voltar).");
			}
			else
			{
				CompanionsPlugin.Log?.LogWarning($"[IA] Fase 3.3b: [AI] SttModel continua em \"{WhisperStt.PreviousModelRelative}\" porque o modelo novo nao esta em \"{path}\". "
					+ "Baixe o ggml-small-q5_1.bin para a pasta whisper\\ do plugin para usar o modelo mais preciso.");
			}
		}
		if (VozSilenceThreshold != null && Mathf.Approximately(VozSilenceThreshold.Value, 0.02f))
		{
			VozSilenceThreshold.Value = (float)VozSilenceThreshold.DefaultValue;
			CompanionsPlugin.Log?.LogInfo($"[IA] Fase 3.3b: [AI] VoiceSilenceThreshold estava no valor antigo (0.02) - agora {VozSilenceThreshold.Value:0.###}, "
				+ "porque 0.02 cortava o comeco de palavras faladas baixinho (era uma das causas das transcricoes erradas).");
		}
		if (AiMemory != null && AiMemory.Value == 6)
		{
			AiMemory.Value = (int)AiMemory.DefaultValue;
			CompanionsPlugin.Log?.LogInfo($"[IA] Fase 3.3b: [AI] Memory estava no valor antigo (6) - agora {AiMemory.Value} trocas: cada troca entra no prompt, "
				+ "entao menos memoria = prompt menor = resposta mais rapida. Coloque 6 de volta se preferir a memoria longa.");
		}
	}

	/// <summary>
	/// Fase 3.3c: the gathering radii grew (the companion looks for trees, rocks and plants much further away and
	/// picks up the drops of a wider felling). A config file written before this version still holds the old value
	/// and BepInEx keeps whatever is in the file - so a value that is <b>exactly the old default</b> is moved to the
	/// new one (and the log says so). A value the player picked is never touched. The new <c>[Speech] Chatter</c> key
	/// needs nothing here: BepInEx writes it with its own default (off) the first time the file is saved.
	/// </summary>
	private static void ApplyGatheringDefaultsPhase33c()
	{
		MovedDefault(HarvestScanRadius, 30f, "procura arvores, rochas e plantas num raio bem maior (seguindo voce, o companheiro pode estar a 25 m de distancia)");
		MovedDefault(HarvestFollowRange, 10f, "madeira/pedra/minerio agora sao procurados ate 25 m de voce, em vez de 10 m");
		MovedDefault(ForageFollowRange, 10f, "forrageio agora procura ate 20 m de voce (continua sendo \"o que voce passou perto\")");
		MovedDefault(HarvestRange, 40f, "com Follow desligado o companheiro trabalha ate 60 m do ponto de trabalho");
		MovedDefault(HarvestDropScanRadius, 8f, "os drops de uma arvore derrubada ou rocha quebrada sao catados ate 12 m do ponto da queda");
	}

	/// <summary>Fase 3.3c: moves one setting from an old default to the new one, saying what and why in the log.</summary>
	private static void MovedDefault(ConfigEntry<float> entry, float oldDefault, string why)
	{
		if (entry == null || !Mathf.Approximately(entry.Value, oldDefault))
		{
			return;
		}
		entry.Value = (float)entry.DefaultValue;
		CompanionsPlugin.Log?.LogInfo($"[Config] Fase 3.3c: [{entry.Definition.Section}] {entry.Definition.Key} estava no default antigo ({oldDefault:0.#}) - "
			+ $"agora {entry.Value:0.#}: {why}. Coloque {oldDefault:0.#} de volta se preferir o raio curto.");
	}

	/// <summary>
	/// The <c>[AI] VoiceInitialPrompt</c> default of every version before the multilanguage config: the orders'
	/// vocabulary, hard-coded in Portuguese. It is the value this migration replaces by the vocabulary of the game
	/// language (empty in the file), so it is compared <b>exactly</b>.
	/// </summary>
	private const string OldVoicePrompt = "Ordens para o companheiro: colete madeira, pedra, minerio, ferro, segue, para, "
		+ "vem ca, ataca o greydwarf, equipa o machado de bronze, larga a pedra, descansa, bom dia.";

	/// <summary>
	/// Multilanguage M1/M2 fix: four <c>[AI]</c> defaults became <b>automatic</b> (the game language decides - see
	/// <see cref="CompanionLocale"/>): <c>VoiceLanguage</c> "pt" → "auto", <c>TtsLanguage</c> "pt-br" → "auto",
	/// <c>TtsVoice</c> 42 → 0 and <c>VoiceInitialPrompt</c> (the old Portuguese list) → "". A config file written
	/// before that still holds the old fixed values and BepInEx keeps whatever is in the file, so a value that is
	/// <b>exactly the old default</b> is moved to the new one - the same rule as the 3.3b/3.3c migrations. Any other
	/// value was chosen by the player and is never touched.
	///
	/// <para><b>Nobody loses their voice:</b> "42" in the old Portuguese default <i>is</i> the default voice of pt-BR
	/// (and "pt-br"/"pt" are the Kokoro/Whisper codes of Portuguese), so the migrated file keeps speaking with
	/// <c>pf_dora</c> while the game is in Portuguese - and now follows the language if the player changes it. The old
	/// prompt gives way to the vocabulary of the game language, which is the point of the change.</para>
	///
	/// <para>It is <b>idempotent</b> (auto / 0 / "" do not match the old defaults, so the second boot finds nothing)
	/// and it writes nothing itself: <see cref="Init"/> holds BepInEx's per-Bind saves off and writes the file once at
	/// the end. One line in the log, naming the settings that really moved.</para>
	/// </summary>
	private static void ApplyMultilanguageDefaults()
	{
		List<string> moved = new List<string>(4);
		if (WasOldDefault(VozLanguage, "pt", StringComparison.OrdinalIgnoreCase))
		{
			VozLanguage.Value = (string)VozLanguage.DefaultValue;
			moved.Add("VoiceLanguage");
		}
		if (WasOldDefault(TtsLanguage, "pt-br", StringComparison.OrdinalIgnoreCase))
		{
			TtsLanguage.Value = (string)TtsLanguage.DefaultValue;
			moved.Add("TtsLanguage");
		}
		if (TtsVoice != null && TtsVoice.Value == 42)
		{
			TtsVoice.Value = (int)TtsVoice.DefaultValue;
			moved.Add("TtsVoice");
		}
		if (WasOldDefault(VozPromptInitial, OldVoicePrompt, StringComparison.Ordinal))
		{
			VozPromptInitial.Value = (string)VozPromptInitial.DefaultValue;
			moved.Add("VoiceInitialPrompt");
		}
		if (moved.Count == 0)
		{
			return;
		}
		string which = string.Join("/", moved.ToArray());
		CompanionsPlugin.Log?.LogInfo($"[AI] Migracao da multilinguagem: {which} "
			+ (moved.Count == 1 ? "estava no padrao antigo e agora e automatico" : "estavam nos padroes antigos e agora sao automaticos")
			+ " (escolhas suas nao sao tocadas).");
	}

	/// <summary>Is the setting still on the old default? (<paramref name="comparison"/> says how exact it has to be)</summary>
	private static bool WasOldDefault(ConfigEntry<string> entry, string oldDefault, StringComparison comparison)
	{
		if (entry == null)
		{
			return false;
		}
		string value = entry.Value;
		// the codes are compared without the surrounding spaces and without case (a hand-edited .cfg may have either);
		// the prompt is an exact sentence, so it is compared as it is
		return comparison == StringComparison.OrdinalIgnoreCase
			? string.Equals((value ?? "").Trim(), oldDefault, comparison)
			: string.Equals(value, oldDefault, comparison);
	}

	// 1.4.149: old section/key -> new section/key
	private static readonly string[][] s_moved = new string[][]
	{
		new string[] { "General", "ShowMinimapHudPanel", "General", "ShowMinimapPanel" },
		new string[] { "Overhead", "ShowOverheadBars", "General", "ShowOverheadBars" },
		new string[] { "StayHome", "HomeZoneRadius", "General", "HomeRadius" },
		new string[] { "General", "FollowTeleportDistance", "Follow", "TeleportDistance" },
		new string[] { "General", "FollowStuckTeleport", "Follow", "StuckTeleport" },
		new string[] { "AI", "FormationOffset", "Follow", "FormationSpacing" },
		new string[] { "AI", "FormationCatchupDist", "Follow", "FormationCatchUp" },
		new string[] { "General", "CompanionPathing", "Movement", "Pathing" },
		new string[] { "General", "WorkRunDistance", "Movement", "WorkRunDistance" },
		new string[] { "General", "DryDetour", "Movement", "DryDetour" },
		new string[] { "General", "JobSwim", "Movement", "JobSwim" },
		new string[] { "General", "MaxDrop", "Movement", "MaxDrop" },
		new string[] { "General", "JumpGap", "Movement", "JumpGap" },
		new string[] { "General", "JumpWaterDepth", "Movement", "JumpWaterDepth" },
		new string[] { "General", "JumpWaterWidth", "Movement", "JumpWaterWidth" },
		new string[] { "Combat", "WaterStaminaFleeThreshold", "Movement", "SwimStaminaShore" },
		new string[] { "Sleep", "SleepAtNight", "Rest", "SleepAtNight" },
		new string[] { "Sleep", "SleepWhenPlayerSleeps", "Rest", "SleepWhenPlayerSleeps" },
		new string[] { "Sleep", "BedRange", "Rest", "BedRange" },
		new string[] { "Respawn", "AutoRespawnEnabled", "Death", "AutoRespawn" },
		new string[] { "Respawn", "AutoRespawnDelaySeconds", "Death", "RespawnDelay" },
		new string[] { "Respawn", "AutoRespawnAtWorldSpawnWhenNoBed", "Death", "RespawnAtWorldSpawn" },
		new string[] { "Respawn", "DeathItemRule", "Death", "DeathItemRule" },
		new string[] { "Respawn", "TombstoneRecoveryRange", "Death", "TombstoneRecoveryRange" },
		new string[] { "Skills", "DeathLossFactor", "Death", "SkillLoss" },
		new string[] { "AI", "AutoPickupRange", "Looting", "AutoPickupRange" },
		new string[] { "Repair", "DurabilityThreshold", "GearRepair", "DurabilityThreshold" },
		new string[] { "Repair", "HomeThreshold", "GearRepair", "HomeThreshold" },
		new string[] { "Repair", "NoStationWait", "GearRepair", "NoStationWait" },
		new string[] { "Repair", "ScanRadius", "GearRepair", "StationRange" },
		new string[] { "Harvest", "Range", "Gathering", "Range" },
		new string[] { "Harvest", "FollowRange", "Gathering", "FollowRange" },
		new string[] { "Harvest", "PickUpLoose", "Gathering", "PickUpLoose" },
		new string[] { "Harvest", "WoodLimit", "Gathering", "WoodLimit" },
		new string[] { "Harvest", "StoneLimit", "Gathering", "StoneLimit" },
		new string[] { "Harvest", "OreLimit", "Gathering", "OreLimit" },
		new string[] { "Harvest", "GatherWood", "Gathering", "GatherWood" },
		new string[] { "Harvest", "GatherStone", "Gathering", "GatherStone" },
		new string[] { "Harvest", "GatherOre", "Gathering", "GatherOre" },
		new string[] { "Harvest", "ForageItems", "Gathering", "ForageItems" },
		new string[] { "Harvest", "WoodSkipTrees", "Gathering", "WoodSkipTrees" },
		new string[] { "Harvest", "BaseProtectRadius", "Gathering", "BaseProtectRadius" },
		new string[] { "Harvest", "JunkCleanup", "Gathering", "JunkCleanup" },
		new string[] { "Harvest", "NeverDiscard", "Gathering", "NeverDiscard" },
		new string[] { "Harvest", "OverweightThreshold", "Gathering", "OverweightThreshold" },
		new string[] { "Farming", "ScanRadius", "Farming", "Range" },
		new string[] { "Farm", "SeedMinimum", "Farming", "SeedMinimum" },
		new string[] { "Farm", "SeedTarget", "Farming", "SeedTarget" },
		new string[] { "Farm", "SeedCarry", "Farming", "SeedCarry" },
		new string[] { "Farm", "PlantGroupSize", "Farming", "PlantGroupSize" },
		new string[] { "Farm", "NextCropRange", "Farming", "NextCropRange" },
		new string[] { "Farm", "PlantAny", "Farming", "PlantAny" },
		new string[] { "Smelting", "ScanRadius", "Smelting", "Range" },
		new string[] { "Harvest", "ScanRadius", "Advanced", "GatheringSearchRadius" },
		new string[] { "Harvest", "ScanInterval", "Advanced", "GatheringScanInterval" },
		new string[] { "Harvest", "DropScanRadius", "Advanced", "GatheringDropRadius" },
		new string[] { "Harvest", "BlacklistDuration", "Advanced", "GatheringSkipTime" },
		new string[] { "Farming", "ScanInterval", "Advanced", "FarmScanInterval" },
		new string[] { "Farming", "UseDistance", "Advanced", "FarmUseDistance" },
		new string[] { "Smelting", "ScanInterval", "Advanced", "SmeltScanInterval" },
		new string[] { "Smelting", "UseDistance", "Advanced", "SmeltUseDistance" },
		new string[] { "Repair", "ScanInterval", "Advanced", "GearRepairScanInterval" },
		new string[] { "Repair", "RepairTickInterval", "Advanced", "GearRepairTickInterval" },
		new string[] { "Repair", "UseDistance", "Advanced", "GearRepairUseDistance" },
		new string[] { "RepairAndRestock", "ScanInterval", "Advanced", "RestockScanInterval" },
		new string[] { "Combat", "AttackCooldown", "Advanced", "AttackCooldown" },
		new string[] { "Food", "FoodRegenInterval", "Advanced", "FoodRegenInterval" },
		new string[] { "Food", "ConsumeCheckInterval", "Advanced", "FoodCheckInterval" },
		new string[] { "AI", "TombstoneScanInterval", "Advanced", "TombstoneScanInterval" },
		new string[] { "AI", "TombstoneNavTimeout", "Advanced", "TombstoneNavTimeout" },
	};

	// BepInEx keeps every line of the .cfg it doesn't have a setting for ("orphaned") and writes it back on every save.
	// Values under an old name are copied to the new setting, then all leftovers (old names, removed settings, the old
	// Homestead and Door sections) are dropped and the file is saved clean.
	private static bool MigrateAndClean(ConfigFile cfg)
	{
		try
		{
			Dictionary<ConfigDefinition, string> orphans = AccessTools.Property(typeof(ConfigFile), "OrphanedEntries")?.GetValue(cfg, null) as Dictionary<ConfigDefinition, string>;
			if (orphans == null || orphans.Count == 0)
			{
				return false;
			}
			int moved = 0;
			foreach (string[] m in s_moved)
			{
				string value;
				ConfigDefinition from = new ConfigDefinition(m[0], m[1]);
				if (!orphans.TryGetValue(from, out value))
				{
					continue;
				}
				ConfigDefinition toDef = new ConfigDefinition(m[2], m[3]);
				if (cfg.ContainsKey(toDef))
				{
					cfg[toDef].SetSerializedValue(value);
					moved++;
				}
			}
			int dropped = orphans.Count;
			// 1.8.3: the leftovers were thrown away without a trace, and they can be something the player typed by
			// hand, a key from a newer version he is testing or the rest of a manual edit - keep the whole file next
			// to it first (AiCompanions.cfg.bak).
			if (dropped > 0)
			{
				string backup = cfg.ConfigFilePath + ".bak";
				try
				{
					File.Copy(cfg.ConfigFilePath, backup, overwrite: true);
					CompanionsPlugin.Log?.LogInfo($"[Config] {dropped} orphaned setting(s) removed - previous file saved as {Path.GetFileName(backup)}");
				}
				catch (Exception ex)
				{
					CompanionsPlugin.Log?.LogWarning("[Config] Couldn't back up the config file before removing the old settings: " + ex.Message);
				}
			}
			orphans.Clear();
			cfg.Save();
			CompanionsPlugin.Log?.LogInfo($"[Config] Cleaned up the config file: {moved} setting(s) moved to their new names, {dropped} old line(s) removed");
			return true;
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log?.LogWarning("[Config] Couldn't clean up old settings: " + ex.Message);
			return false;
		}
	}

	/// <summary>Writes the file the way BepInEx writes it after a Bind: a locked or lost file is a warning, never an
	/// exception out of <see cref="Init"/> (the plugin loads with the in-memory values in that case).</summary>
	private static void SaveQuietly(ConfigFile cfg)
	{
		try
		{
			cfg.Save();
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log?.LogWarning("[Config] Couldn't write " + cfg.ConfigFilePath + " (" + ex.Message + ") - continuing with the settings in memory.");
		}
	}

	private static ConfigEntry<T> Bind<T>(ConfigFile cfg, string category, string key, T defaultValue, string desc, AcceptableValueBase range = null)
	{
		ConfigEntry<T> configEntry = cfg.Bind(category, key, defaultValue, new ConfigDescription(desc, range));
		if (!Categories.TryGetValue(category, out var value))
		{
			value = new List<ConfigEntryBase>();
			Categories[category] = value;
			CategoryOrder.Add(category);
		}
		value.Add(configEntry);
		return configEntry;
	}
}
