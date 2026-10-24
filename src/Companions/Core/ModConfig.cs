using System;
using System.Collections.Generic;
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

	internal static ConfigEntry<int> ShieldMultiAttackerCount;




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

	internal static ConfigEntry<int> DeathItemRule;

	internal static ConfigEntry<float> TombstoneRecoveryRange;

	internal static ConfigEntry<bool> SleepAtNight;

	internal static ConfigEntry<bool> SleepWhenPlayerSleeps;

	internal static ConfigEntry<float> BedRange;

	internal static ConfigEntry<float> RecallRange;

	internal static ConfigEntry<float> SuperRecallHoldTime;

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

	internal static ConfigEntry<float> FollowIndoorDistance;

	internal static ConfigEntry<bool> FollowRegroup;

	internal static ConfigEntry<float> FollowRegroupTime;

	internal static ConfigEntry<bool> FollowSitIndoors;

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

	internal static ConfigEntry<float> ForageFollowRange;

	internal static ConfigEntry<float> HarvestRange;

	internal static ConfigEntry<bool> HarvestPickUpLoose;

	internal static ConfigEntry<float> HarvestScanInterval;

	internal static ConfigEntry<float> HarvestAttackInterval;

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

	internal static ConfigEntry<float> SpeechMinInterval;

	internal static ConfigEntry<float> SpeechMaxInterval;

	internal static ConfigEntry<float> SpeechCullDistance;

	internal static ConfigEntry<float> SpeechDuration;

	internal static ConfigEntry<float> SpeechSayCooldown;

	// Fase 3.3c: the periodic chatter (CompanionTalk's own timer) is off by default; orders, mode changes, job
	// milestones and everything the AI layer says still speak.
	internal static ConfigEntry<bool> SpeechChatter;

	internal static ConfigEntry<float> SkillDeathLossFactor;

	internal static ConfigEntry<KeyCode> DirectTargetKey;

	internal static ConfigEntry<KeyCode> RadialMenuKey;

	internal static ConfigEntry<KeyCode> ConfigPanelKey;

	internal static ConfigEntry<KeyCode> FarmZoneKey;

	internal static ConfigEntry<KeyCode> FarmZoneModifier;

	// [IA] - Fase 3.1: ordens por texto no chat, interpretadas por um LLM local (LM Studio/Ollama, API OpenAI-compatible)
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

	// [IA] - Fase 3.5: personalidade (preset ou texto livre), memória curta da conversa e conversa livre
	internal static ConfigEntry<string> AiPersonality;

	internal static ConfigEntry<string> AiPersonalityPreset;

	internal static ConfigEntry<int> AiMemory;

	internal static ConfigEntry<bool> AiConversation;

	// [IA] - Fase 3.1 (saída estruturada): response_format/json_schema no pedido, para o modelo sempre responder JSON
	internal static ConfigEntry<bool> AiStructuredOutput;

	// [IA] - STT local (spike): teste do Whisper.net in-process. Ver Diagnostics/WhisperSpike.cs.
	internal static ConfigEntry<bool> SttTest;

	internal static ConfigEntry<string> SttModel;

	internal static ConfigEntry<string> SttWav;

	// [IA] - Fase 3.3: ordens por voz (push-to-talk + STT local com Whisper.net). Ver AI/VoiceInput.cs.
	internal static ConfigEntry<bool> VozEnabled;

	internal static ConfigEntry<KeyCode> VozTecla;

	internal static ConfigEntry<string> VozLanguage;

	internal static ConfigEntry<float> VozMaxSeconds;

	internal static ConfigEntry<float> VozSilenceThreshold;

	internal static ConfigEntry<string> VozPromptInitial;

	internal static ConfigEntry<int> VozDevice;

	internal static ConfigEntry<bool> VozShowText;

	// [IA] - Fase 3.7: painel da IA no jogo (personalidade, voz, push-to-talk). Ver UI/CompanionAiPanel.cs.
	internal static ConfigEntry<KeyCode> AiPanelKey;

	// [IA] - Fase 3.4: TTS local (Kokoro-82M via sherpa-onnx): o companheiro FALA. Ver AI/CompanionTts.cs.
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

	// [IA] - Fase 3.4 (parte 3): o audio da voz do companheiro. O eco tinha uma causa so: o Valheim carrega UMA
	// AudioReverbZone global (no proprio objeto do AudioListener) com raio de 1000 m e 4 s de cauda, e a nossa
	// AudioSource estava no padrao do Unity (reverbZoneMix = 1) - a fala pegava a cauda inteira. O jogo foge dela
	// (a musica usa 0, o ambiente 0,19). TtsReverbZoneMix (0 = voz seca) e a opcao de verdade; EcoDiag e so o
	// diagnostico de audio no log. Ver AI/CompanionTts.cs.
	internal static ConfigEntry<float> TtsReverbZoneMix;

	internal static ConfigEntry<bool> TtsEcoDiag;

	internal static void Init(ConfigFile cfg)
	{
		// [General]
		CompanionPrice = Bind(cfg, "General", "CompanionPrice", 2000, "O preço de um companheiro extra no Trader, em moedas do banco compartilhado. 0 = de graça.", new AcceptableValueRange<int>(0, 50000));
		MaxCompanions = Bind(cfg, "General", "MaxCompanions", 10, "Fase 3.19: o número máximo de companheiros que você pode ter neste mundo (10 é o teto). A conta inclui os mortos e os perdidos: a lápide traz o MESMO companheiro de volta, então morrer não abre vaga. Ao chegar no limite, o Trader, o invocador do painel da IA e o companheiro inicial recusam criar outro (o Santuário continua podendo recuperar um companheiro morto). Para abrir vaga, dispense um pelo menu da roda (Dispensar).", new AcceptableValueRange<int>(1, 10));
		SpawnStarterCompanion = Bind(cfg, "General", "SpawnStarterCompanion", defaultValue: true, "Na primeira vez que você entra num mundo com um personagem, é oferecido um companheiro de graça.");
		ResetStarterFlag = Bind(cfg, "General", "ResetStarterFlag", defaultValue: false, "Coloque true para receber de novo a oferta do companheiro inicial grátis no mundo atual (no próximo carregamento do mundo). Ele volta sozinho para false.");
		ShowCompanionRoster = Bind(cfg, "General", "ShowCompanionRoster", defaultValue: false, "Coloque true para listar os seus companheiros neste mundo e o status de cada um: Vivo, Morto (a lápide dele vai trazê-lo de volta), Perdido (não sobrou nada dele - dá para recuperar) ou Desconhecido (num servidor dedicado, um companheiro que não está por perto não pode ser verificado). Ele volta sozinho para false.");
		RecoverLostCompanion = Bind(cfg, "General", "RecoverLostCompanion", defaultValue: false, "Coloque true para trazer de volta um companheiro Perdido, ao seu lado, com o equipamento e as configurações do último salvamento da lista. Um companheiro Desconhecido precisa que você ligue a opção uma segunda vez em até 60 s para confirmar (ele pode ainda estar vivo em algum lugar). Se o companheiro antigo aparecer depois, ele é removido. Ele volta sozinho para false.");
		ProtectCompanions = Bind(cfg, "General", "ProtectCompanions", defaultValue: true, "Impede que outros mods apaguem um companheiro de vez (por exemplo, mods que limpam criaturas de áreas protegidas). Os companheiros ainda podem morrer, deixar lápide e renascer normalmente, e o botão Dispensar continua funcionando. Quando algo é bloqueado, o log diz o que tentou. Desligue só se outro mod precisar remover companheiros.");
		ShowMinimapHudPanel = Bind(cfg, "General", "ShowMinimapPanel", defaultValue: true, "Mostra o painel de status dos companheiros embaixo do minimapa.");
		ShowOverheadBars = Bind(cfg, "General", "ShowOverheadBars", defaultValue: true, "Mostra as barras de vida e stamina em cima da cabeça de cada companheiro.");
		MaxCarryWeight = Bind(cfg, "General", "MaxCarryWeight", 300f, "O peso BASE que um companheiro consegue carregar (um jogador carrega 300). Companheiros de coleta e de caça voltam para casa para descarregar antes de chegar nesse limite. Itens que aumentam a capacidade de carga somam por cima deste valor: o Megingjord, por exemplo, dá +150 e vale com o cinto em qualquer lugar da mochila do companheiro (não precisa equipar), então com este valor em 300 o teto dele passa a 450. Dois cintos iguais não somam: o bônus conta uma vez só, como no jogo.", new AcceptableValueRange<float>(50f, 2000f));
		BaseHealth = Bind(cfg, "General", "BaseHealth", 25f, "A vida de um companheiro antes da comida (um jogador tem 25).", new AcceptableValueRange<float>(1f, 500f));
		BaseStamina = Bind(cfg, "General", "BaseStamina", 50f, "A stamina de um companheiro antes da comida (um jogador tem 50).", new AcceptableValueRange<float>(1f, 500f));
		MaxFoodSlots = Bind(cfg, "General", "MaxFoodSlots", 3, "Quantas comidas diferentes um companheiro pode ter comido ao mesmo tempo (um jogador tem 3).", new AcceptableValueRange<int>(1, 5));
		StatusEffectsOnRegen = Bind(cfg, "General", "StatusEffectsOnRegen", true, "Efeitos de status mudam a regeneração de vida e stamina de um companheiro como mudam a de um jogador: Molhado e Com Frio deixam mais lenta, hidroméis e outros buffs mudam. Desligado = os companheiros regeneram no ritmo normal, sejam quais forem os efeitos.");
		HomeTripRange = Bind(cfg, "General", "HomeTripRange", 60f, "A que distância de casa (em metros) um companheiro ainda volta para as idas ao baú: buscar ou devolver ferramentas, consertar uma ferramenta quebrada, esvaziar as bolsas. Mais longe do que isso, um companheiro sem a ferramenta do trabalho avisa e segue você até vocês dois estarem perto de casa de novo.", new AcceptableValueRange<float>(10f, 500f));
		HomeZoneRadius = Bind(cfg, "General", "HomeRadius", 50f, "Tamanho inicial (em metros) da área da casa de um companheiro em volta do ponto de casa - onde ele procura uma cama para reivindicar. O tamanho de cada companheiro pode ser mudado depois no painel da casa dele.", new AcceptableValueRange<float>(5f, 200f));
		// [Controls]
		DirectTargetKey = Bind(cfg, "Controls", "DirectTargetKey", KeyCode.Z, "A tecla de comando. Aperte olhando para alguma coisa para mandar os companheiros até ela (ir até lá, atacar, abrir a porta, sentar perto do fogo, consertar na bancada...). Segure para chamá-los até você.");
		RadialMenuKey = Bind(cfg, "Controls", "RadialMenuKey", KeyCode.E, "Segure esta tecla olhando para um companheiro para abrir a roda de comandos dele.");
		ConfigPanelKey = Bind(cfg, "Controls", "ConfigPanelKey", KeyCode.F8, "Abre o painel de configurações no jogo, onde todas estas opções podem ser mudadas.");
		FarmZoneKey = Bind(cfg, "Controls", "FarmZoneKey", KeyCode.Z, "Aperte junto com FarmZoneModifier: abre o editor de zonas de fazenda.");
		FarmZoneModifier = Bind(cfg, "Controls", "FarmZoneModifier", KeyCode.LeftAlt, "Segure esta tecla e aperte FarmZoneKey para abrir o editor de zonas de fazenda.");
		RecallRange = Bind(cfg, "Controls", "RecallRange", 40f, "A que distância (em metros) um companheiro tem que estar de você para atender um chamado (segurando a tecla de comando) ou uma ordem de apontar e comandar. Companheiros com Comando desligado na roda ignoram os dois. 0 = sem limite.", new AcceptableValueRange<float>(0f, 500f));
		SuperRecallHoldTime = Bind(cfg, "Controls", "SuperRecallHoldTime", 2f, "Continue segurando a tecla de comando por esta quantidade de segundos para chamar todos os companheiros, a qualquer distância, inclusive os que estão com Comando desligado.", new AcceptableValueRange<float>(0.5f, 10f));
		// [Follow]
		FollowMaxDistance = Bind(cfg, "Follow", "MaxDistance", 10f, "A que distância um companheiro que segue você deixa você chegar antes de se mover para acompanhar. Ele escolhe o próprio lugar dentro desse raio (ao lado, atrás, às vezes à frente) e muda de lugar de vez em quando.", new AcceptableValueRange<float>(4f, 30f));
		FollowIndoorDistance = Bind(cfg, "Follow", "IndoorDistance", 4f, "O mesmo raio enquanto você está num lugar fechado (sob um teto), para os companheiros ficarem no mesmo ambiente que você.", new AcceptableValueRange<float>(2f, 10f));
		FollowRegroup = Bind(cfg, "Follow", "Regroup", defaultValue: true, "Quando você fica parado por RegroupTime segundos, os companheiros voltam e esperam perto de você. Desligado = eles ficam onde pararam.");
		FollowRegroupTime = Bind(cfg, "Follow", "RegroupTime", 45f, "Segundos que você fica parado antes de os companheiros se reagruparem em volta de você (veja Regroup).", new AcceptableValueRange<float>(5f, 600f));
		FollowSitIndoors = Bind(cfg, "Follow", "SitIndoors", defaultValue: true, "Quando você para num lugar fechado, os companheiros sentam perto de você: numa cadeira ou banco livre, se houver, senão no chão. Perto de casa, eles esvaziam as bolsas primeiro.");
		FollowTeleportDistance = Bind(cfg, "Follow", "TeleportDistance", 50f, "Um companheiro que segue você e fica esta quantidade de metros para trás aparece do seu lado: na hora, além de 1.5x essa distância, ou quando ele não consegue mais te ver e não está chegando mais perto.", new AcceptableValueRange<float>(20f, 200f));
		FollowStuckTeleport = Bind(cfg, "Follow", "StuckTeleport", defaultValue: false, "Seguindo você, um companheiro que ficou preso por 15 segundos num lugar de onde não consegue te ver aparece do seu lado. Desligado = ele continua tentando achar o próprio caminho.");
		FormationOffset = Bind(cfg, "Follow", "FormationSpacing", 3f, "Espaço (em metros) entre companheiros que andam lado a lado em formação (quando seguem algo que não é você).", new AcceptableValueRange<float>(1f, 10f));
		FormationCatchupDist = Bind(cfg, "Follow", "FormationCatchUp", 15f, "Em formação, um companheiro a esta quantidade de metros do lugar dele corre para alcançar.", new AcceptableValueRange<float>(5f, 50f));
		// [Movement]
		WalkSpeed = Bind(cfg, "Movement", "WalkSpeed", 2f, "Velocidade de caminhada (um jogador anda a cerca de 2). Passa a valer depois que o mundo é recarregado.", new AcceptableValueRange<float>(0.5f, 5f));
		RunSpeed = Bind(cfg, "Movement", "RunSpeed", 7f, "Velocidade de corrida (um jogador corre a cerca de 7). Passa a valer depois que o mundo é recarregado.", new AcceptableValueRange<float>(2f, 15f));
		SwimSpeed = Bind(cfg, "Movement", "SwimSpeed", 2f, "Velocidade de natação (um jogador nada a cerca de 2). Passa a valer depois que o mundo é recarregado.", new AcceptableValueRange<float>(0.5f, 5f));
		TurnSpeed = Bind(cfg, "Movement", "TurnSpeed", 300f, "Com que rapidez um companheiro gira (graus por segundo). Passa a valer depois que o mundo é recarregado.", new AcceptableValueRange<float>(50f, 600f));
		ViewRange = Bind(cfg, "Movement", "ViewRange", 20f, "A que distância (em metros) um companheiro consegue ver inimigos e outras criaturas. Passa a valer depois que o mundo é recarregado.", new AcceptableValueRange<float>(5f, 100f));
		ViewAngle = Bind(cfg, "Movement", "ViewAngle", 120f, "O campo de visão de um companheiro (em graus). Passa a valer depois que o mundo é recarregado.", new AcceptableValueRange<float>(30f, 360f));
		CompanionNavMesh = Bind(cfg, "Movement", "Pathing", defaultValue: true, "Os companheiros acham o caminho com o próprio mapa do mundo, onde portas não bloqueiam a passagem (eles as abrem ao chegar) e degraus de até 0,6 m são subíveis (eles pulam). Desligado = o mesmo mapa que as criaturas usam, onde toda porta fechada é parede e a maioria das escadas não se conecta. Passa a valer depois de reiniciar o jogo.");
		WorkRunDistance = Bind(cfg, "Movement", "WorkRunDistance", 25f, "Os companheiros andam enquanto trabalham (plantar, idas ao baú, fundição, cozinha, tarefas, consertos, coleta) e só correm quando o destino está mais longe do que esta quantidade de metros. 0 = nunca corre para trabalhar. Seguir você, lutar e caçar não são afetados.", new AcceptableValueRange<float>(0f, 500f));
		DryDetour = Bind(cfg, "Movement", "DryDetour", 30f, "Os companheiros preferem ficar secos, como um jogador: quando o caminho até algum lugar cruza água, eles dão a volta por terra, desde que o caminho seco não seja mais longo do que 1.5x o caminho nadando mais esta quantidade de metros. Senão, eles nadam.", new AcceptableValueRange<float>(0f, 500f));
		JobSwim = Bind(cfg, "Movement", "JobSwim", 20f, "O máximo de natação (em metros) que um trabalho aceita para chegar a alguma coisa: o saque de um abate, uma presa, um pássaro, um ponto de pesca. Mais longe do que isso, ele deixa pra lá.", new AcceptableValueRange<float>(0f, 200f));
		MaxDrop = Bind(cfg, "Movement", "MaxDrop", 3f, "Quando uma linha reta é bem mais curta que o caminho de volta (descer a escada e voltar, contornar um muro), os companheiros a pegam como um jogador: pulam degraus, caem de beiradas de até esta quantidade de metros (jogadores não tomam dano de queda abaixo de 4 m) e pulam pequenos vãos (JumpGap).", new AcceptableValueRange<float>(1f, 4f));
		StuckDrop = Bind(cfg, "Movement", "StuckDrop", 8f, "Quando um companheiro está preso acima de onde precisa estar e não há outro jeito de descer (do alto de uma escada, de uma plataforma, de uma beirada), ele pula uma queda de até esta quantidade de metros, como um jogador que prefere tomar um pouco de dano de queda a ficar parado. Uma escada para descer vem primeiro, e ele nunca aceita uma queda que custaria mais de 30% da vida dele naquele momento (companheiros tomam dano de queda como um jogador: nenhum até 4 m).", new AcceptableValueRange<float>(3f, 20f));
		JumpGap = Bind(cfg, "Movement", "JumpGap", 1.5f, "O vão mais largo (em metros) que um companheiro salta nesse tipo de atalho. 0.5 = nenhum vão.", new AcceptableValueRange<float>(0.5f, 3f));
		JumpWaterDepth = Bind(cfg, "Movement", "JumpWaterDepth", 0.3f, "Água com pelo menos esta profundidade (em metros) no caminho de um companheiro é saltada quando o trecho é estreito o bastante (JumpWaterWidth), como um jogador pula um riacho pequeno em vez de atravessá-lo correndo. Água rasa mais larga é atravessada a pé.", new AcceptableValueRange<float>(0.05f, 2f));
		JumpWaterWidth = Bind(cfg, "Movement", "JumpWaterWidth", 3f, "O trecho de água mais largo (em metros) que um companheiro salta.", new AcceptableValueRange<float>(1f, 4f));
		WaterStaminaFleeThreshold = Bind(cfg, "Movement", "SwimStaminaShore", 0.25f, "Quando a stamina de um companheiro nadando cai abaixo desta fração (0.25 = 25%), ele vai para a margem mais próxima, seja o que for que estivesse fazendo.", new AcceptableValueRange<float>(0f, 1f));
		// [Combat]
		EngageOnNotice = Bind(cfg, "Combat", "EngageOnNotice", true, "Ligado: os companheiros atacam uma criatura assim que ela percebe você ou um companheiro. Desligado: eles esperam até ela vir de fato para cima de você (alerta). Qualquer coisa que os ataque é sempre revidada, e a Caça vai atrás da presa de qualquer jeito.");
		HealthRetreatPct = Bind(cfg, "Combat", "HealthRetreatPct", 0.3f, "Abaixo desta fração de vida (0.3 = 30%) um companheiro luta na defensiva: pega o melhor escudo de bloqueio e não abre com tocha.", new AcceptableValueRange<float>(0.05f, 0.9f));
		FleeHealthPct = Bind(cfg, "Combat", "FleeHealthPct", 0.15f, "Abaixo desta fração de vida (0.15 = 15%) um companheiro foge do inimigo.", new AcceptableValueRange<float>(0.05f, 0.6f));
		WeaponSwitchMargin = Bind(cfg, "Combat", "WeaponSwitchMargin", 0.1f, "Uma arma diferente precisa ser esta fração melhor contra o inimigo atual (0.1 = 10%) para um companheiro trocar para ela, para não ficar trocando de um lado para o outro.", new AcceptableValueRange<float>(0f, 0.5f));
		ShieldMultiAttackerCount = Bind(cfg, "Combat", "ShieldMultiAttackerCount", 3, "Quando esta quantidade de inimigos ataca um companheiro ao mesmo tempo, ele troca para o escudo com o melhor bloqueio.", new AcceptableValueRange<int>(1, 10));
		BowSwitchDistance = Bind(cfg, "Combat", "BowSwitchDistance", 15f, "Um companheiro com arco ou besta troca para ele quando o alvo está mais longe do que isso (em metros).", new AcceptableValueRange<float>(5f, 60f));
		MeleeSwitchDistance = Bind(cfg, "Combat", "MeleeSwitchDistance", 12f, "Ele volta para a arma de corpo a corpo quando o alvo chega mais perto do que isso (em metros).", new AcceptableValueRange<float>(3f, 40f));
		UseTorchOpener = Bind(cfg, "Combat", "UseTorchOpener", defaultValue: true, "Contra inimigos fracos a fogo, abre com uma tocha para incendiá-los e depois volta para a melhor arma.");
		TorchOpenerTimeout = Bind(cfg, "Combat", "TorchOpenerTimeout", 4f, "Segundos tentando incendiar um inimigo antes de desistir e voltar para a arma.", new AcceptableValueRange<float>(1f, 10f));
		TorchOpenerCooldown = Bind(cfg, "Combat", "TorchOpenerCooldown", 10f, "Segundos antes de o mesmo inimigo poder ser aberto com tocha de novo.", new AcceptableValueRange<float>(0f, 60f));
		TorchOpenerMinTargetHealth = Bind(cfg, "Combat", "TorchOpenerMinTargetHealth", 40f, "Sem abertura com tocha em inimigos com vida máxima menor que isso - não vale a pena.", new AcceptableValueRange<float>(0f, 500f));
		// [Food]
		HealthMeadThreshold = Bind(cfg, "Food", "HealthMeadThreshold", 0.5f, "Um companheiro bebe um hidromel de cura da bolsa quando a vida cai abaixo desta fração (0.5 = 50%).", new AcceptableValueRange<float>(0.1f, 0.9f));
		StaminaMeadThreshold = Bind(cfg, "Food", "StaminaMeadThreshold", 0.25f, "Ele bebe um hidromel de stamina quando a stamina cai abaixo desta fração (0.25 = 25%).", new AcceptableValueRange<float>(0.05f, 0.9f));
		MeadCooldown = Bind(cfg, "Food", "MeadCooldown", 10f, "Segundos depois de beber um hidromel antes de beber outro.", new AcceptableValueRange<float>(1f, 60f));
		// [Stamina]
		StaminaRegenRate = Bind(cfg, "Stamina", "RegenRate", 6f, "Stamina que um companheiro recupera por segundo quando não está usando nenhuma.", new AcceptableValueRange<float>(1f, 30f));
		StaminaRunDrain = Bind(cfg, "Stamina", "RunDrainRate", 10f, "Stamina gasta por segundo correndo.", new AcceptableValueRange<float>(1f, 30f));
		StaminaSneakDrain = Bind(cfg, "Stamina", "SneakDrainRate", 5f, "Stamina gasta por segundo se esgueirando.", new AcceptableValueRange<float>(1f, 30f));
		StaminaSwimDrain = Bind(cfg, "Stamina", "SwimDrainRate", 10f, "Stamina gasta por segundo nadando.", new AcceptableValueRange<float>(1f, 30f));
		StaminaRegenDelay = Bind(cfg, "Stamina", "RegenDelay", 1f, "Segundos depois de usar stamina antes de ela começar a voltar.", new AcceptableValueRange<float>(0.1f, 5f));
		// [Rest]
		SleepAtNight = Bind(cfg, "Rest", "SleepAtNight", defaultValue: true, "À noite, um companheiro que esteja dentro de BedRange da cama reivindicada para o que está fazendo e vai dormir, inclusive quando está trabalhando. De manhã ele volta ao trabalho.");
		SleepWhenPlayerSleeps = Bind(cfg, "Rest", "SleepWhenPlayerSleeps", defaultValue: true, "Quando você entra numa cama, os seus companheiros dentro de BedRange das camas reivindicadas vão dormir também, mesmo que ainda não seja noite. O pulo da noite espera até 30 segundos por eles.");
		BedRange = Bind(cfg, "Rest", "BedRange", 50f, "A que distância (em metros) um companheiro tem que estar da cama reivindicada para que as duas opções acima o mandem dormir.", new AcceptableValueRange<float>(10f, 200f));
		RestHealRate = Bind(cfg, "Rest", "HealRate", 2f, "Vida que um companheiro recupera por segundo enquanto senta perto de um fogo ou dorme.", new AcceptableValueRange<float>(0.5f, 20f));
		RestPlayerRange = Bind(cfg, "Rest", "PlayerRange", 5f, "Quando você senta perto de um fogo (a emoção de sentar), companheiros sem trabalho dentro desta quantidade de metros de você sentam ali também.", new AcceptableValueRange<float>(2f, 20f));
		RestFireRange = Bind(cfg, "Rest", "FireRange", 5f, "A que distância (em metros) um fogo tem que estar para um companheiro sentar ao lado dele.", new AcceptableValueRange<float>(2f, 20f));
		RestWarmupTime = Bind(cfg, "Rest", "WarmupTime", 20f, "Segundos sentado perto de um fogo antes de um companheiro ficar Descansado, como um jogador.", new AcceptableValueRange<float>(5f, 120f));
		RestDirectedTimeout = Bind(cfg, "Rest", "DirectedTimeout", 300f, "Quando você manda um companheiro sentar perto de um fogo, ele se levanta depois desta quantidade de segundos.", new AcceptableValueRange<float>(30f, 1800f));
		// [Death]
		AutoRespawnEnabled = Bind(cfg, "Death", "AutoRespawn", defaultValue: true, "Um companheiro morto volta sozinho depois de RespawnDelay segundos, se ninguém o trouxer de volta da lápide antes.");
		AutoRespawnDelaySeconds = Bind(cfg, "Death", "RespawnDelay", 300f, "Segundos depois da morte antes de um companheiro voltar sozinho.", new AcceptableValueRange<float>(30f, 3600f));
		AutoRespawnAtWorldSpawnWhenNoBed = Bind(cfg, "Death", "RespawnAtWorldSpawn", defaultValue: false, "Um companheiro que volta sozinho e não tem cama reivindicada aparece nas pedras do ponto inicial do mundo. Desligado = do seu lado.");
		DeathItemRule = Bind(cfg, "Death", "DeathItemRule", 0, "O que um companheiro perde quando morre. 0 = a regra de penalidade de morte do mundo, igual à de um jogador. 1 = mantém tudo. 2 = mantém o equipamento vestido, o resto vai para a lápide. 3 = tudo vai para a lápide. A opção do dono é usada mesmo que a morte seja processada pelo jogo de outro jogador.", new AcceptableValueRange<int>(0, 3));
		TombstoneRecoveryRange = Bind(cfg, "Death", "TombstoneRecoveryRange", 50f, "Um companheiro anda até a própria lápide que sobrou e a esvazia quando está dentro desta quantidade de metros dela.", new AcceptableValueRange<float>(5f, 100f));
		ShrineBuildable = Bind(cfg, "Death", "ShrineBuildable", defaultValue: true, "O Santuário de Odin pode ser construído com o martelo (aba Diversos, precisa de uma bancada). Use-o para ver os seus companheiros e trazer de volta um perdido. Passa a valer no próximo carregamento do mundo; santuários já construídos continuam funcionando.");
		ShrineRecipe = Bind(cfg, "Death", "ShrineRecipe", "Stone:10,Wood:5,GreydwarfEye:3", "Quanto custa construir o Santuário de Odin, em pares Item:Quantidade separados por vírgulas (nomes de item como no comando spawn do jogo). Passa a valer no próximo carregamento do mundo.");
		ShrineModel = Bind(cfg, "Death", "ShrineModel", "Tombstone", "A aparência do Santuário de Odin: Tombstone (a lápide de um jogador) ou Ward (a pedra de área protegida). Passa a valer no próximo carregamento do mundo, também para santuários já construídos.");
		SkillDeathLossFactor = Bind(cfg, "Death", "SkillLoss", 0.25f, "Fração das habilidades que um companheiro perde quando morre (0.25 = 25%, 0 = nenhuma).", new AcceptableValueRange<float>(0f, 1f));
		// [Looting]
		LootKills = Bind(cfg, "Looting", "PickUpKills", true, "Depois que um companheiro mata alguma coisa, ele vai até lá e recolhe o que a criatura deixou (coisas que você mesmo pegaria com o auto-pickup), como um jogador faria. Precisa de Recolher ligado na roda.");
		LootRadius = Bind(cfg, "Looting", "Radius", 6f, "A que distância em volta do abate (em metros) os drops são procurados.", new AcceptableValueRange<float>(2f, 20f));
		LootWait = Bind(cfg, "Looting", "WaitSeconds", 10f, "Tempo máximo de espera (em segundos) ao lado do corpo para os drops saírem.", new AcceptableValueRange<float>(2f, 60f));
		LootMaxDistance = Bind(cfg, "Looting", "MaxDistance", 30f, "Abates mais longe do que isso (em metros), ou dos quais você se afastou esta distância enquanto ele te segue, ficam para trás.", new AcceptableValueRange<float>(5f, 100f));
		CollectRadius = Bind(cfg, "Looting", "CollectRadius", 10f, "Com a ordem \"recolhe os itens do chão\", a que distância (em metros) do companheiro ele procura coisas soltas no chão - inclusive o que você ou ele largou de propósito. Ele anda até cada item e pega um por um, até não sobrar nada no raio, a mochila encher ou a ordem passar de 30 segundos.", new AcceptableValueRange<float>(1f, 30f));
		AutoPickupRange = Bind(cfg, "Looting", "AutoPickupRange", 2f, "Com Recolher ligado na roda, um companheiro pega itens dentro desta quantidade de metros dele enquanto anda, como o auto-pickup de um jogador.", new AcceptableValueRange<float>(0.5f, 10f));
		// [GearRepair]
		RepairDurabilityThreshold = Bind(cfg, "GearRepair", "DurabilityThreshold", 0.5f, "Um companheiro leva o equipamento até uma bancada para consertar quando algo cai abaixo desta fração de durabilidade (0.5 = 50%).", new AcceptableValueRange<float>(0.1f, 0.9f));
		RepairHomeThreshold = Bind(cfg, "GearRepair", "HomeThreshold", 0.8f, "Em casa, onde uma bancada está perto, o equipamento é consertado um pouco antes: abaixo desta fração (0.8 = 80%). 1 = a qualquer desgaste. Longe de casa vale o DurabilityThreshold.", new AcceptableValueRange<float>(0.1f, 1f));
		RepairNoStationWait = Bind(cfg, "GearRepair", "NoStationWait", 600f, "Quando um item gasto não pode ser consertado em nenhuma bancada ao alcance (digamos que precise de uma forja negra e não haja nenhuma por perto), ou o caminho até a bancada falhou, esse item fica de lado por esta quantidade de segundos antes de ele procurar de novo.", new AcceptableValueRange<float>(30f, 3600f));
		RepairScanRadius = Bind(cfg, "GearRepair", "StationRange", 20f, "A que distância (em metros) um companheiro procura uma bancada para consertar. Perto de casa, qualquer bancada perto de casa conta.", new AcceptableValueRange<float>(5f, 60f));
		// [Gathering]
		HarvestRange = Bind(cfg, "Gathering", "Range", 60f, "Madeira, Pedra, Minério e Forrageio com Seguir desligado: a que distância (em metros) de onde ele está trabalhando ele vai atrás de árvores, rochas e plantas - em volta de casa quando o trabalho começou perto dela, senão em volta de onde foi iniciado. Ele continua trabalhando ali quando você vai embora. (Fase 3.3c: 60, era 40.)", new AcceptableValueRange<float>(10f, 150f));
		HarvestFollowRange = Bind(cfg, "Gathering", "FollowRange", 25f, "Madeira, Pedra e Minério com Seguir ligado: uma árvore ou rocha tem que estar dentro desta quantidade de metros de você. Sem nada tão perto, o companheiro só segue você. (Com Seguir desligado veja Range; o Forrageio tem ForageFollowRange.) Fase 3.3c: 25, era 10 - ele trabalha a até 25 m de você em vez de ficar colado em você, e só abandona um alvo quando ele passa de 15 m além disso.", new AcceptableValueRange<float>(5f, 60f));
		ForageFollowRange = Bind(cfg, "Gathering", "ForageFollowRange", 20f, "Forrageio com Seguir ligado: uma planta tem que estar dentro desta quantidade de metros de você - as frutinhas que você passar por perto, não tudo em volta. Sem nada tão perto, o companheiro só segue você. (Com Seguir desligado veja Range.) Fase 3.3c: 20, era 10 - ainda é curto o bastante para o Forrageio continuar sendo \"o que você passou por perto\", e não uma varredura de toda a área.", new AcceptableValueRange<float>(3f, 60f));
		HarvestPickUpLoose = Bind(cfg, "Gathering", "PickUpLoose", true, "Trabalhos de Madeira e Pedra, só no início do jogo: também pegar à mão o que está no chão - galhos (trabalho de Madeira), pedras soltas e sílex (trabalho de Pedra) - desde que estejam em GatherWood / GatherStone e abaixo da quantidade deles. Início do jogo = não há machado de bronze ou melhor na mochila do companheiro, na sua mochila ou nos baús perto de casa, e não há forja em casa para fazer um. Sem ferramenta para o trabalho (sem machado para Madeira, sem picareta para Pedra), pegar as coisas do chão é tudo o que ele faz.");
		WoodLimit = Bind(cfg, "Gathering", "WoodLimit", 500, "Quantos de cada material de madeira (contando a mochila do companheiro e os baús perto da casa dele) o trabalho de Madeira coleta antes de parar. 0 = sem limite. Números por item em GatherWood têm prioridade sobre este.", new AcceptableValueRange<int>(0, 100000));
		StoneLimit = Bind(cfg, "Gathering", "StoneLimit", 500, "Igual ao WoodLimit, para o trabalho de Pedra e para GatherStone.", new AcceptableValueRange<int>(0, 100000));
		OreLimit = Bind(cfg, "Gathering", "OreLimit", 500, "Igual ao WoodLimit, para o trabalho de Minério e para GatherOre.", new AcceptableValueRange<int>(0, 100000));
		GatherWood = Bind(cfg, "Gathering", "GatherWood", "Wood, FineWood, RoundLog, ElderBark, YggdrasilWood, Blackwood, Resin", "O que o trabalho de Madeira coleta. Alvos só são trabalhados se derrubarem algo desta lista que ainda esteja abaixo da quantidade; drops fora da lista ficam no chão. Um nome sem número usa o WoodLimit. Formato da lista (igual para todas as listas): * = tudo, Nome = só esse, -Nome = nunca esse, Nome:Número = esse com a sua própria quantidade (0 = sem limite). Os nomes são nomes de prefab ou do jogo.");
		GatherStone = Bind(cfg, "Gathering", "GatherStone", "Stone, Flint, BlackMarble, Grausten", "O que o trabalho de Pedra coleta. Alvos só são trabalhados se derrubarem algo desta lista que ainda esteja abaixo da quantidade; drops fora da lista ficam no chão. Um nome sem número usa o StoneLimit. Formato da lista (igual para todas as listas): * = tudo, Nome = só esse, -Nome = nunca esse, Nome:Número = esse com a sua própria quantidade (0 = sem limite). Os nomes são nomes de prefab ou do jogo.");
		GatherOre = Bind(cfg, "Gathering", "GatherOre", "CopperOre, TinOre, IronScrap, SilverOre, BlackMetalScrap, CopperScrap, FlametalOreNew, Obsidian", "O que o trabalho de Minério coleta. Alvos só são trabalhados se derrubarem algo desta lista que ainda esteja abaixo da quantidade; drops fora da lista ficam no chão. Um nome sem número usa o OreLimit. Formato da lista (igual para todas as listas): * = tudo, Nome = só esse, -Nome = nunca esse, Nome:Número = esse com a sua própria quantidade (0 = sem limite). Os nomes são nomes de prefab ou do jogo.");
		ForageItems = Bind(cfg, "Gathering", "ForageItems", "Raspberry,Blueberries,Cloudberry,Mushroom,MushroomYellow,MushroomBlue,MushroomMagecap,MushroomJotunPuffs,MushroomSmokePuff,Thistle,Dandelion,Carrot,Turnip,Onion,Barley,Flax,RoyalJelly,Honey", "O que o trabalho de Forrageio pega. Nome:Número para de pegar aquele item quando houver essa quantidade (mochila + baús perto de casa). Formato da lista (igual para todas as listas): * = tudo, Nome = só esse, -Nome = nunca esse, Nome:Número = esse com a sua própria quantidade (0 = sem limite). Os nomes são nomes de prefab ou do jogo.");
		WoodSkipTrees = Bind(cfg, "Gathering", "WoodSkipTrees", "Oak", "Árvores de pé que o trabalho de Madeira nunca derruba, comparadas por parte do nome da árvore (Oak, Birch, Beech, Pine, Fir, SwampTree, YggaShoot...). Toras e tocos ainda são limpos.");
		BaseProtectRadius = Bind(cfg, "Gathering", "BaseProtectRadius", 10f, "O trabalho de Madeira não derruba uma árvore de pé que tenha qualquer coisa construída por jogadores dentro desta quantidade de metros do tronco. 0 = desligado.", new AcceptableValueRange<float>(0f, 50f));
		JunkCleanup = Bind(cfg, "Gathering", "JunkCleanup", defaultValue: true, "Quando a mochila de um companheiro de coleta está cheia e ele não tem baú de casa para descarregar, ele joga fora tranqueiras (materiais e troféus que não estão em nenhuma lista de coleta) para abrir espaço. Equipamento, ferramentas, munição, comida, poções, sementes e qualquer coisa em NeverDiscard são sempre mantidos.");
		NeverDiscard = Bind(cfg, "Gathering", "NeverDiscard", "Trophy", "Itens que a limpeza de tranqueiras nunca joga fora, comparados por parte do nome do item.");
		HarvestOverweightThreshold = Bind(cfg, "Gathering", "OverweightThreshold", 298f, "O peso (valor BASE) em que um companheiro de coleta - madeira, pedra, minério, forragem e a roça da horta - para de pegar coisas e volta para casa para descarregar. Itens que aumentam a capacidade de carga somam por cima deste valor, como no MaxCarryWeight: com o Megingjord na mochila (+150) e este valor em 298, ele só volta para casa perto de 448. Caça e pesca não usam este limite (elas têm as próprias opções de mochila cheia).", new AcceptableValueRange<float>(50f, 500f));
		// [Hunting]
		HuntPrey = Bind(cfg, "Hunting", "Prey", "*", "O que o trabalho de Caça persegue: criaturas que derrubam carne (e pássaros, veja Birds). Nome:Número com uma criatura ou uma carne dá àquela carne o próprio alvo em vez de MeatTarget / FeatherTarget. Animais domados, chefes e companheiros nunca são caçados. Os nomes que você pode usar estão listados em OfflineCompanionsReborn.Hunting.txt na pasta de configuração (escrito na inicialização). Formato da lista (igual para todas as listas): * = tudo, Nome = só esse, -Nome = nunca esse, Nome:Número = esse com a sua própria quantidade (0 = sem limite). Os nomes são nomes de item/criatura (prefab ou do jogo).");
		HuntMeatTarget = Bind(cfg, "Hunting", "MeatTarget", 100, "Caçar uma criatura só enquanto uma das carnes que ela derruba estiver abaixo desta quantidade (contando a mochila do companheiro e os baús perto da casa dele). Couro, peles e outros drops são pegos de brinde. 0 = sem limite.", new AcceptableValueRange<int>(0, 10000));
		HuntRange = Bind(cfg, "Hunting", "Range", 40f, "Com Seguir desligado: a que distância (em metros) de onde está caçando (casa, se estiver perto dela, senão de onde o trabalho foi iniciado) um companheiro procura presas.", new AcceptableValueRange<float>(10f, 200f));
		HuntFollowRange = Bind(cfg, "Hunting", "FollowRange", 40f, "Com Seguir ligado: a presa tem que estar dentro desta quantidade de metros de você. Sem nada tão perto, o companheiro só segue você.", new AcceptableValueRange<float>(5f, 100f));
		HuntChaseExtra = Bind(cfg, "Hunting", "ChaseExtra", 15f, "Quanto além de Range / FollowRange um companheiro persegue uma presa que foge antes de deixá-la ir.", new AcceptableValueRange<float>(0f, 100f));
		HuntBagFull = Bind(cfg, "Hunting", "BagFull", 0.9f, "Com Seguir desligado e casa definida: quando a mochila está assim cheia (por peso, ou por falta de espaços livres) o caçador vai para casa e guarda a caça, depois volta.", new AcceptableValueRange<float>(0.3f, 1f));
		HuntBirds = Bind(cfg, "Hunting", "Birds", true, "Também atirar em pássaros (corvos, gaivotas, pássaros de mods) pelas penas: quando um pousa ao alcance, o caçador chega de mansinho e atira com arco ou besta (só se tiver um com munição), depois pega os drops. Pássaros excluídos na lista Prey (-Crow, -Seagal) ficam em paz.");
		HuntFeatherTarget = Bind(cfg, "Hunting", "FeatherTarget", 100, "Atirar em pássaros só enquanto o que eles derrubam (penas) estiver abaixo desta quantidade, contando a mochila e os baús perto de casa. 0 = sem limite.", new AcceptableValueRange<int>(0, 10000));
		HuntSneak = Bind(cfg, "Hunting", "Sneak", true, "Chegar de mansinho em bichos ariscos (cervos, lebres...) que ainda não perceberam ninguém, como um jogador: agachado e sem correr, para não espantá-los. Companheiros agachados são mais silenciosos e mais difíceis de notar, como um jogador se esgueirando (luz e habilidade de Furtividade contam).");
		HuntThrowSpear = Bind(cfg, "Hunting", "ThrowSpear", true, "Um caçador com lança na mão a arremessa em bichos ariscos (cervos, lebres...) de longe, como um jogador, depois anda até lá e pega a lança de volta. Só quando ele tem outra arma na mochila para continuar a luta.");
		HuntThrowRange = Bind(cfg, "Hunting", "ThrowRange", 12f, "A que distância (em metros) um caçador arremessa a lança. Arremessos mais longos erram mais.", new AcceptableValueRange<float>(4f, 25f));
		HuntThrowStandoff = Bind(cfg, "Hunting", "ThrowStandoff", 10f, "Com uma lança para arremessar em bichos ariscos, um caçador chega só até esta distância (em metros), para e arremessa, em vez de andar até lá para esfaquear. Nunca mais que ThrowRange menos 1 m.", new AcceptableValueRange<float>(3f, 25f));
		HuntSneakFrom = Bind(cfg, "Hunting", "SneakFrom", 20f, "Mais longe do que isso (em metros) de uma presa que ainda não o notou, um caçador anda (sem correr) em vez de se esgueirar, como um jogador - se esgueirar o caminho todo gasta muita stamina. Mais perto, ele se agacha e se esgueira.", new AcceptableValueRange<float>(5f, 60f));
		HuntSwitchCloserBy = Bind(cfg, "Hunting", "SwitchCloserBy", 5f, "Antes do primeiro golpe, um caçador troca para outro animal que esteja pelo menos esta quantidade de metros mais perto que a presa atual (e veja SwitchShare). 0 = nunca troca.", new AcceptableValueRange<float>(0f, 50f));
		HuntSwitchShare = Bind(cfg, "Hunting", "SwitchShare", 0.6f, "...e que esteja no máximo nesta fração da distância até a presa (0.6 = 60%).", new AcceptableValueRange<float>(0.1f, 1f));
		HuntSneakRestStamina = Bind(cfg, "Hunting", "SneakRestStamina", 0.3f, "Esgueirar-se gasta stamina, como para um jogador (ficar parado não gasta). Quando um caçador se aproximando da presa cai abaixo desta fração de stamina, ele para onde está, continua agachado e recupera o fôlego. 0 = nunca para.", new AcceptableValueRange<float>(0f, 0.9f));
		HuntSneakResumeStamina = Bind(cfg, "Hunting", "SneakResumeStamina", 0.6f, "Um caçador descansando enquanto se esgueira continua quando a stamina volta a ficar acima desta fração.", new AcceptableValueRange<float>(0.1f, 1f));
		HuntChaseRunStamina = Bind(cfg, "Hunting", "ChaseRunStamina", 0.3f, "Perseguindo uma presa que disparou, um caçador só corre enquanto a stamina estiver acima desta fração; abaixo disso, ele trota e deixa a stamina voltar, como um jogador. 0 = corre sempre.", new AcceptableValueRange<float>(0f, 1f));
		// [Fishing]
		FishScanRadius = Bind(cfg, "Fishing", "ScanRadius", 30f, "A que distância (em metros) de onde o trabalho de Pesca foi iniciado o companheiro procura peixes para pescar. Ele fica por ali e pesca a água por perto.", new AcceptableValueRange<float>(5f, 100f));
		FishList = Bind(cfg, "Fishing", "Fish", "*", "Quais peixes pescar. Nome:Número dá àquele peixe o próprio alvo em vez de FishTarget. Os nomes dos peixes estão listados em OfflineCompanionsReborn.Fishing.txt na pasta de configuração (escrito na inicialização). Formato da lista (igual para todas as listas): * = tudo, Nome = só esse, -Nome = nunca esse, Nome:Número = esse com a sua própria quantidade (0 = sem limite).");
		FishTarget = Bind(cfg, "Fishing", "FishTarget", 100, "Pescar um tipo de peixe só enquanto houver menos que esta quantidade (mochila + baús perto de casa). 0 = sem limite.", new AcceptableValueRange<int>(0, 10000));
		FishCastWait = Bind(cfg, "Fishing", "CastWait", 60f, "Segundos de espera por uma fisgada antes de recolher e tentar de novo (um peixe que escapou, o lugar errado).", new AcceptableValueRange<float>(10f, 600f));
		FishBagFull = Bind(cfg, "Fishing", "BagFull", 0.9f, "Perto de casa: quando a mochila está assim cheia (por peso, ou por falta de espaços livres) o companheiro vai para casa e guarda a pesca, depois volta a pescar.", new AcceptableValueRange<float>(0.3f, 1f));
		// [Farming]
		FarmScanRadius = Bind(cfg, "Farming", "Range", 30f, "A que distância (em metros) em volta de si um companheiro de fazenda procura plantações para colher, zonas de fazenda para plantar e baús.", new AcceptableValueRange<float>(5f, 100f));
		FarmPlantSpacing = Bind(cfg, "Farming", "PlantSpacing", 0.75f, "O menor espaço (em metros) entre plantas. Uma planta que precise de mais espaço para crescer fica com esse espaço.", new AcceptableValueRange<float>(0.25f, 3f));
		SeedReserve = Bind(cfg, "Farming", "SeedMinimum", 20, "O mínimo de cada item plantável (sementes de cenoura, nabo, cebola, cevada, linho e por aí) que o fazendeiro mantém, contando a mochila e os baús perto da casa dele. Ele nunca planta sementes se isso levar o estoque abaixo disso. Cevada, linho e outras culturas que crescem de si mesmas ainda são plantadas, e o fazendeiro mantém essa quantidade na mochila em vez de guardar nos baús.", new AcceptableValueRange<int>(0, 1000));
		SeedTarget = Bind(cfg, "Farming", "SeedTarget", 100, "Enquanto uma semente estiver abaixo deste número, o fazendeiro cultiva plantas de semente (plantando cenoura, nabo, cebola...) junto das culturas normais para repor o estoque. Abaixo do SeedMinimum ele só cultiva plantas de semente daquela cultura. Neste número ou acima, só culturas.", new AcceptableValueRange<int>(0, 5000));
		SeedCarry = Bind(cfg, "Farming", "SeedCarry", 40, "Quantos de cada item plantável o fazendeiro carrega. Quando abre um baú para pegar sementes, ele leva até esta quantidade de cada tipo da lista PlantAny (e vegetais para plantas de semente enquanto aquela semente estiver abaixo do SeedTarget), para poder encher os campos sem voltar para cada tipo. Ele mantém até esta quantidade na mochila em vez de guardar nos baús.", new AcceptableValueRange<int>(1, 200));
		PlantGroupSize = Bind(cfg, "Farming", "PlantGroupSize", 4, "Quantos de uma mesma planta o fazendeiro coloca antes de trocar para o próximo tipo, para um campo misto ficar em blocos pequenos em vez de uma cultura só. 1 = totalmente misturado.", new AcceptableValueRange<int>(1, 100));
		FarmNextCropRange = Bind(cfg, "Farming", "NextCropRange", 3f, "Depois de colher uma planta, o fazendeiro vai direto para a próxima planta madura dentro desta quantidade de metros, como quem trabalha uma fileira, em vez de parar e escanear o campo inteiro. 0 = sempre para e escaneia.", new AcceptableValueRange<float>(0f, 10f));
		PlantAny = Bind(cfg, "Farming", "PlantAny", "CarrotSeeds, TurnipSeeds, OnionSeeds, Barley, Flax, MushroomJotunPuffs, MushroomMagecap, VineberrySeeds", "O que o fazendeiro planta nas zonas de fazenda marcadas como Qualquer Cultura. * = toda cultura que cresce em terra cultivada (mudas de árvore nunca são plantadas numa zona Qualquer, a não ser que sejam nomeadas). Nome:Número para de plantar aquela cultura quando houver essa quantidade do que ela dá (cenouras para CarrotSeeds, cevada para Barley...), contando a mochila e os baús perto de casa. Itens adicionados por outros mods também funcionam. Formato da lista (igual para todas as listas): * = tudo, Nome = só esse, -Nome = nunca esse, Nome:Número = esse com a sua própria quantidade (0 = sem limite). Os nomes são nomes de item/criatura (prefab ou do jogo).");
		// [Smelting]
		SmeltScanRadius = Bind(cfg, "Smelting", "Range", 25f, "A que distância (em metros) em volta de si um companheiro de fundição procura fornalhas, fornos de carvão, fornos e baús. Perto de casa ele também usa os baús em volta de casa ([General] HomeTripRange).", new AcceptableValueRange<float>(5f, 100f));
		SmeltMaxCarryOre = Bind(cfg, "Smelting", "MaxCarryOre", 20, "O máximo de minério (ou outro insumo) que um companheiro leva a uma fornalha numa viagem.", new AcceptableValueRange<int>(1, 100));
		SmeltMaxCarryFuel = Bind(cfg, "Smelting", "MaxCarryFuel", 40, "O máximo de combustível (carvão, madeira) que um companheiro leva a uma fornalha numa viagem.", new AcceptableValueRange<int>(1, 100));
		SmeltUsePiles = Bind(cfg, "Smelting", "UsePiles", true, "Quando os baús não têm o que uma fornalha ou um forno de carvão precisa (madeira para o forno, combustível...), o trabalho de Fundição desmonta uma pilha disso (Pilha de Madeira...) com um martelo, como um jogador, e usa. Precisa de um martelo na mochila.");
		// [Cooking]
		CookRange = Bind(cfg, "Cooking", "Range", 25f, "A que distância em volta de si (em metros) um companheiro de cozinha procura estações de cozinha, fermentadores e baús. Perto de casa ele também usa os baús em volta de casa ([General] HomeTripRange).", new AcceptableValueRange<float>(5f, 60f));
		CookIngredientReserve = Bind(cfg, "Cooking", "IngredientReserve", 20, "Um ingrediente cru só é usado enquanto os baús ao alcance (mais a bolsa do companheiro) tiverem mais que esta quantidade, para sempre sobrar algo para você cozinhar.", new AcceptableValueRange<int>(0, 1000));
		CookFoodTarget = Bind(cfg, "Cooking", "FoodTarget", 20, "Cozinhar uma comida até os baús ao alcance terem esta quantidade dela.", new AcceptableValueRange<int>(1, 1000));
		CookBatchSize = Bind(cfg, "Cooking", "BatchSize", 5, "Quantas de uma comida (ou base de hidromel) fazer de uma vez, como um jogador cozinhando uma fornada. Ainda para no FoodTarget / MeadBaseTarget e nunca entra na reserva de ingredientes. Numa estação de cozinha ou forno também é limitado pelos espaços livres.", new AcceptableValueRange<int>(1, 50));
		CookFoods = Bind(cfg, "Cooking", "Foods", "*", "O que pode ser cozinhado. Nome:Número é o alvo próprio daquela comida em vez do FoodTarget. Os nomes que você pode usar estão listados em OfflineCompanionsReborn.Cooking.txt na pasta de configuração (escrito na inicialização). Formato da lista (igual para todas as listas): * = tudo, Nome = só esse, -Nome = nunca esse, Nome:Número = esse com a sua própria quantidade (0 = sem limite). Os nomes são nomes de item/criatura (prefab ou do jogo).");
		CookStoreWithCookedFood = Bind(cfg, "Cooking", "StoreWithCookedFood", true, "Guardar comida cozida num baú que já tem aquela comida; se não houver, num que tenha outras comidas cozidas; senão, no baú mais próximo da área de cozinha. Desligado = o baú mais próximo com espaço.");
		CookMakeMead = Bind(cfg, "Cooking", "MakeMead", true, "Preparar hidromel: fazer bases de hidromel e enchê-las nos fermentadores. Desligado = só comida (hidromel que já terminou num fermentador ainda é tirado).");
		CookMeadBaseTarget = Bind(cfg, "Cooking", "MeadBaseTarget", 10, "Preparar uma base de hidromel (uma em MeadFocus) até os baús ao alcance terem esta quantidade dela.", new AcceptableValueRange<int>(0, 1000));
		CookMeadTarget = Bind(cfg, "Cooking", "MeadTarget", 20, "Manter os fermentadores abastecidos com uma base de hidromel até os baús ao alcance terem esta quantidade do hidromel pronto.", new AcceptableValueRange<int>(0, 1000));
		CookMeadFocus = Bind(cfg, "Cooking", "MeadFocus", "start: MeadBaseHealthMinor, MeadBaseStaminaMinor; eikthyr: MeadBaseHealthMinor, MeadBaseStaminaMinor, MeadBasePoisonResist; gdking: MeadBasePoisonResist, MeadBaseHealthMedium, MeadBaseStaminaMinor; bonemass: MeadBaseFrostResist, MeadBaseHealthMedium, MeadBaseStaminaMedium; dragon: MeadBaseHealthMedium, MeadBaseStaminaMedium, MeadBaseTasty, BarleyWineBase; goblinking: MeadBaseHealthMajor, MeadBaseStaminaMedium, MeadBaseEitrMinor, MeadBaseLightfoot; queen: BarleyWineBase, MeadBaseHealthMajor, MeadBaseStaminaLingering, MeadBaseHealthLingering, MeadBaseEitrMinor; fader: MeadBaseFrostResist, MeadBaseHealthMajor, MeadBaseHealthLingering, MeadBaseStaminaLingering, MeadBaseEitrMinor; kall: MeadBaseFrostResist, MeadBaseHealthMajor, MeadBaseHealthLingering, MeadBaseStaminaLingering, MeadBaseEitrMinor", "Bases de hidromel para preparar PRIMEIRO, pelo último chefe derrotado, para se preparar para o próximo (as outras bases que você conhece ainda são preparadas depois da comida, dentro de MeadBaseTarget / MeadTarget). Formato: chefe: Base, Base; chefe: ... na ordem em que você os venceu. Um chefe é o nome dele ou parte dele (eikthyr, gdking = The Elder, bonemass, dragon = Moder, goblinking = Yagluth, queen, fader, kall = Kall Fimbulbringer, ou um chefe de mod); start = nenhum vencido ainda. Os chefes que o seu jogo tem, e os nomes das bases, estão listados em OfflineCompanionsReborn.Cooking.txt na pasta de configuração.");
		CookFuelLevel = Bind(cfg, "Cooking", "FuelLevel", 0.5f, "Trabalho de Cozinha: mantém os fogos embaixo dos pontos de cozinha que usa (espeto, estação de ferro, caldeirão) e o forno nesta fração de cheio (0.5 = 50%). Quando um cai abaixo da metade disso, ele reabastece. Só o suficiente para cozinhar - o Reparar e Reabastecer enche mais os fogos, se estiver ligado.", new AcceptableValueRange<float>(0.1f, 1f));
		CookUsePiles = Bind(cfg, "Cooking", "UsePiles", true, "Trabalho de Cozinha: sem combustível nos baús, desmonta uma pilha disso (Pilha de Madeira...) com um martelo, como um jogador. Precisa de um martelo na mochila.");
		CookCauldron = Bind(cfg, "Cooking", "UseCauldron", true, "Trabalho de Cozinha: usar o caldeirão (sopas, ensopados e outras comidas fabricadas).");
		CookPrepTable = Bind(cfg, "Cooking", "UsePrepTable", true, "Trabalho de Cozinha: usar a mesa de preparo (massa, tortas cruas e coisas do tipo).");
		CookMeadKetill = Bind(cfg, "Cooking", "UseMeadKetill", true, "Trabalho de Cozinha: usar o alambique de hidromel (bases de hidromel).");
		CookCookingStation = Bind(cfg, "Cooking", "UseCookingStation", true, "Trabalho de Cozinha: usar a estação de cozinha / espeto sobre o fogo.");
		CookIronCookingStation = Bind(cfg, "Cooking", "UseIronCookingStation", true, "Trabalho de Cozinha: usar a estação de cozinha de ferro.");
		CookOven = Bind(cfg, "Cooking", "UseOven", true, "Trabalho de Cozinha: usar o forno de pedra (pão, tortas; ele também recebe madeira).");
		CookFermenter = Bind(cfg, "Cooking", "UseFermenter", true, "Trabalho de Cozinha: usar fermentadores (enchendo-os com bases de hidromel e tirando o hidromel).");
		CookBeehives = Bind(cfg, "Cooking", "UseBeehives", true, "Trabalho de Cozinha: usar colmeias (recolhendo mel).");
		CookOtherStations = Bind(cfg, "Cooking", "UseOther", true, "Trabalho de Cozinha: usar qualquer outra estação de cozinha, por exemplo uma adicionada por outro mod.");
		// [RepairAndRestock]
		UpkeepRepair = Bind(cfg, "RepairAndRestock", "Repair", true, "Reparar e Reabastecer: consertar peças de construção danificadas perto de casa com um martelo, como um jogador (um golpe por peça; a bancada da peça tem que estar ao alcance e uma área protegida tem que permitir; gasta stamina e desgasta o martelo). Um martelo é buscado num baú se ele não tiver nenhum.");
		UpkeepRepairBelow = Bind(cfg, "RepairAndRestock", "RepairBelow", 0.75f, "Consertar peças abaixo desta fração de vida (0.75 = 75% ou menos; 1 = qualquer dano). Cada conserto é um golpe que deixa a peça como nova, como o de um jogador.", new AcceptableValueRange<float>(0.1f, 1f));
		UpkeepReach = Bind(cfg, "RepairAndRestock", "Reach", 4.5f, "A que distância (em metros, dos olhos) o companheiro alcança uma peça para consertá-la. Um jogador alcança cerca de 5 m.", new AcceptableValueRange<float>(2f, 6f));
		UpkeepRestock = Bind(cfg, "RepairAndRestock", "Restock", true, "Reparar e Reabastecer: manter fogos, tochas e braseiros perto de casa abastecidos, um pedaço de combustível por vez como um jogador. O combustível vem da bolsa ou é buscado num baú; o que sobra volta para um baú. As opções Refuel... abaixo escolhem os tipos.");
		UpkeepFuelBelow = Bind(cfg, "RepairAndRestock", "FuelBelow", 0.8f, "Reabastecer um fogo quando o combustível dele estiver abaixo desta fração de cheio (0.8 = 80%). Ele é enchido até o topo.", new AcceptableValueRange<float>(0.1f, 1f));
		RefuelFires = Bind(cfg, "RepairAndRestock", "RefuelFires", true, "Reabastecer: fogueiras, fogos de chão e fogueiras grandes.");
		RefuelHearths = Bind(cfg, "RepairAndRestock", "RefuelHearths", true, "Reabastecer: lareiras.");
		RefuelBraziers = Bind(cfg, "RepairAndRestock", "RefuelBraziers", true, "Reabastecer: braseiros (de pé e pendurados).");
		RefuelStandingTorches = Bind(cfg, "RepairAndRestock", "RefuelStandingTorches", true, "Reabastecer: tochas de pé (madeira, ferro, verde, azul, névoa).");
		RefuelWallTorches = Bind(cfg, "RepairAndRestock", "RefuelWallTorches", true, "Reabastecer: arandelas e tochas de parede.");
		RefuelHotTubs = Bind(cfg, "RepairAndRestock", "RefuelHotTubs", true, "Reabastecer: banheiras quentes.");
		RefuelOther = Bind(cfg, "RepairAndRestock", "RefuelOther", true, "Reabastecer: qualquer outra coisa que queime combustível e não seja dos tipos acima (lanternas, a abóbora-lanterna, coisas adicionadas por outros mods).");
		UpkeepHoney = Bind(cfg, "RepairAndRestock", "Honey", true, "Reparar e Reabastecer: esvaziar as colmeias da sua área e guardar o mel no baú que já tem mais mel.");
		UpkeepHoneyAt = Bind(cfg, "RepairAndRestock", "HoneyAt", 0.5f, "Esvaziar uma colmeia quando ela estiver pelo menos nesta fração cheia (0.5 = metade, 1 = só quando cheia).", new AcceptableValueRange<float>(0.1f, 1f));
		UpkeepSort = Bind(cfg, "RepairAndRestock", "Sort", true, "Reparar e Reabastecer: organizar os baús da sua área (em volta de casa, ou de onde o trabalho foi iniciado), igual com igual: cada tipo de item é levado para o baú que já tem mais dele, e pilhas parciais são juntadas.");
		UpkeepSortItems = Bind(cfg, "RepairAndRestock", "SortItems", "*", "Quais itens são organizados. Separados por vírgulas: * = tudo, Nome = só esse, -Nome = nunca esse (ex.: *, -Coins, -Wood deixa moedas e madeira onde estão). Nomes de prefab ou do jogo.");
		UpkeepSortGear = Bind(cfg, "RepairAndRestock", "SortGear", false, "Organizar também as coisas que não empilham (armas, armaduras, ferramentas). Desligado: só itens empilháveis são movidos.");
		UpkeepPiles = Bind(cfg, "RepairAndRestock", "Piles", true, "Reparar e Reabastecer: empilhar madeira, pedra e coisas do tipo dos baús em pilhas (a Pilha de Madeira, Pilha de Pedra, Pilha de Madeira Fina... do jogo), construídas com um martelo como um jogador. Só ao lado de pilhas do mesmo tipo que você já construiu - ele nunca começa uma pilha nova sozinho.");
		UpkeepPileKeep = Bind(cfg, "RepairAndRestock", "PileKeep", 100, "Quantos de cada material ficam nos baús para construção: o que passa disso vai para pilhas, e quando os baús caem abaixo desse número uma pilha é desmontada (com o martelo, como um jogador) e o material volta para os baús.", new AcceptableValueRange<int>(0, 10000));
		UpkeepPileItems = Bind(cfg, "RepairAndRestock", "PileItems", "*", "Quais materiais viram pilhas. Separados por vírgulas: * = todo material do qual você tem pilhas, Nome = só esse, -Nome = nunca esse, Nome:Número = mantenha esta quantidade dele nos baús em vez do PileKeep. Nomes de prefab ou do jogo (Wood, Stone, FineWood, RoundLog...).");
		UpkeepAwayRadius = Bind(cfg, "RepairAndRestock", "AwayRadius", 30f, "Iniciado longe de casa (ou sem casa definida): a que distância (em metros) de onde o trabalho começou ele conserta, reabastece e organiza. Iniciado perto de casa, o alcance é [General] HomeTripRange em volta de casa.", new AcceptableValueRange<float>(5f, 100f));
		UpkeepWorkTime = Bind(cfg, "RepairAndRestock", "WorkTime", 1f, "Segundos por ação: um golpe de conserto, abrir um baú. Colocar combustível leva um pouco mais da metade disso por pedaço. (O jogo não conserta a mesma peça mais de uma vez por segundo.)", new AcceptableValueRange<float>(0.3f, 5f));
		UpkeepGiveUp = Bind(cfg, "RepairAndRestock", "GiveUp", 90f, "Segundos que ele continua tentando chegar ao alcance de uma peça, fogo ou baú antes de deixar isso de lado por alguns minutos.", new AcceptableValueRange<float>(20f, 600f));
		UpkeepOrderWait = Bind(cfg, "RepairAndRestock", "OrderWait", 30f, "Depois que você dá uma ordem ao companheiro (mandá-lo a algum lugar, sentar perto de um fogo...), ele espera esta quantidade de segundos quando a ordem termina antes de voltar às tarefas dele.", new AcceptableValueRange<float>(0f, 600f));
		// [Idle]
		IdleEnabled = Bind(cfg, "Idle", "Enabled", true, "Quando o trabalho dele fica um tempo sem nada para fazer (e ele não está seguindo você), o companheiro fica à toa perto do trabalho em vez de ficar parado: pequenas tarefas, sentar, dar uma volta, conversar. Ele para no instante em que o trabalho tem serviço de novo.");
		IdleStartAfter = Bind(cfg, "Idle", "StartAfter", 10f, "Segundos sem nada para fazer antes de começar a ficar à toa.", new AcceptableValueRange<float>(1f, 300f));
		IdleRadius = Bind(cfg, "Idle", "Radius", 8f, "A que distância (em metros) de onde o trabalho o deixou um companheiro anda, senta ou procura companhia enquanto está à toa.", new AcceptableValueRange<float>(2f, 40f));
		IdleChores = Bind(cfg, "Idle", "Chores", true, "Enquanto estiver à toa, fazer pequenas tarefas por perto: consertar peças danificadas, reabastecer fogos, recolher mel, guardar o que carrega (usa as configurações de Reparar e Reabastecer).");
		IdleChoreRadius = Bind(cfg, "Idle", "ChoreRadius", 15f, "A que distância (em metros) ele procura tarefas de ócio.", new AcceptableValueRange<float>(2f, 60f));
		IdleSit = Bind(cfg, "Idle", "Sit", true, "Enquanto estiver à toa, sentar um pouco - numa cadeira ou banco livre por perto, senão no chão.");
		IdleWander = Bind(cfg, "Idle", "Wander", true, "Enquanto estiver à toa, passear por perto e dar uma olhada nas estações e baús.");
		IdleSocialize = Bind(cfg, "Idle", "Socialize", true, "Enquanto estiver à toa, ir até você ou outro companheiro por perto e conversar de vez em quando.");
		IdleSpeechInterval = Bind(cfg, "Idle", "SpeechInterval", 300f, "No máximo a cada esta quantidade de segundos ele diz por que está à toa (e conversa enquanto socializa mais ou menos com metade dessa frequência). 0 = nunca.", new AcceptableValueRange<float>(0f, 3600f));
		// [Speech]
		SpeechChatter = Bind(cfg, "Speech", "Chatter", defaultValue: false, "Os companheiros falam sozinhos de vez em quando quando nada está acontecendo: uma linha sobre uma luta por perto, estar com peso demais, estar com fome, equipamento gasto, falta de cama, o trabalho em que estão, ou só seguir você. Desligado (Fase 3.3c) = nenhuma fala aleatória. Eles continuam falando quando algo de fato acontece: uma ordem (chat ou voz), uma mudança de modo (\"Deixa comigo!\"), as respostas da IA, um peixe pescado, um marco de fazenda/conserto/fundição, uma lápide encontrada, descarregar perto de casa. As falas de ócio são separadas ([Idle] SpeechInterval). Ligue para voltar ao comportamento antigo.");
		SpeechMinInterval = Bind(cfg, "Speech", "MinInterval", 15f, "Tempo mais curto (em segundos) entre as falas de um companheiro quando nada está acontecendo. Só é usado enquanto Chatter estiver ligado.", new AcceptableValueRange<float>(5f, 120f));
		SpeechMaxInterval = Bind(cfg, "Speech", "MaxInterval", 30f, "Tempo mais longo (em segundos) entre as falas de um companheiro quando nada está acontecendo. Só é usado enquanto Chatter estiver ligado.", new AcceptableValueRange<float>(10f, 300f));
		SpeechDuration = Bind(cfg, "Speech", "Duration", 5f, "Segundos que uma linha de fala fica na tela.", new AcceptableValueRange<float>(1f, 15f));
		SpeechSayCooldown = Bind(cfg, "Speech", "SayCooldown", 3f, "Tempo mínimo (em segundos) entre duas falas de qualquer companheiro, para não falarem todos ao mesmo tempo.", new AcceptableValueRange<float>(1f, 30f));
		SpeechCullDistance = Bind(cfg, "Speech", "CullDistance", 20f, "Falas de companheiros mais longe do que isso (em metros) não são mostradas.", new AcceptableValueRange<float>(5f, 60f));
		// [Advanced]
		RosterSaveInterval = Bind(cfg, "Advanced", "RosterSaveInterval", 60f, "Segundos entre os salvamentos dos seus companheiros na lista (equipamento e configurações), usados para recuperar um companheiro perdido.", new AcceptableValueRange<float>(10f, 600f));
		HarvestScanRadius = Bind(cfg, "Advanced", "GatheringSearchRadius", 50f, "Coleta: a que distância (em metros) em volta de si um companheiro procura árvores, rochas e plantas quando está seguindo você ou foi mandado a uma com a tecla de comando (com Seguir desligado vale [Gathering] Range). Fase 3.3c: 50, era 30 - seguindo, o companheiro pode estar a 25 m de você ([Gathering] FollowRange) e o alvo a até 25 m de você do outro lado, então o anel em que ele procura precisa alcançar cerca de 50 m.", new AcceptableValueRange<float>(5f, 100f));
		HarvestScanInterval = Bind(cfg, "Advanced", "GatheringScanInterval", 4f, "Coleta: segundos entre as procuras pela próxima árvore, rocha ou planta.", new AcceptableValueRange<float>(1f, 30f));
		HarvestAttackInterval = Bind(cfg, "Advanced", "GatheringSwingInterval", 2.5f, "Coleta: segundos entre golpes numa árvore ou rocha.", new AcceptableValueRange<float>(0.5f, 5f));
		HarvestDropScanRadius = Bind(cfg, "Advanced", "GatheringDropRadius", 12f, "Coleta: a que distância (em metros) em volta de uma árvore derrubada ou rocha quebrada os drops são pegos. Fase 3.3c: 12, era 8 - uma árvore pode cair (ou uma rocha se espalhar) mais longe que 8 m, e os drops ficavam para trás.", new AcceptableValueRange<float>(2f, 20f));
		HarvestBlacklistDuration = Bind(cfg, "Advanced", "GatheringSkipTime", 60f, "Coleta: segundos em que uma árvore, rocha ou planta que ele não conseguiu alcançar fica ignorada.", new AcceptableValueRange<float>(5f, 300f));
		FarmScanInterval = Bind(cfg, "Advanced", "FarmScanInterval", 4f, "Fazenda: segundos entre as procuras por plantações para colher e terra para plantar.", new AcceptableValueRange<float>(1f, 30f));
		FarmUseDistance = Bind(cfg, "Advanced", "FarmUseDistance", 2f, "Fazenda: a que distância (em metros) um companheiro tem que estar de uma plantação ou baú para usá-los.", new AcceptableValueRange<float>(1f, 5f));
		SmeltScanInterval = Bind(cfg, "Advanced", "SmeltScanInterval", 3f, "Fundição: segundos entre as olhadas nas fornalhas e baús.", new AcceptableValueRange<float>(1f, 30f));
		SmeltUseDistance = Bind(cfg, "Advanced", "SmeltUseDistance", 2.5f, "Fundição: a que distância (em metros) um companheiro tem que estar de uma fornalha para colocar ou tirar coisas.", new AcceptableValueRange<float>(1f, 5f));
		RepairScanInterval = Bind(cfg, "Advanced", "GearRepairScanInterval", 5f, "Conserto de equipamento: segundos entre as verificações de desgaste do equipamento de um companheiro.", new AcceptableValueRange<float>(1f, 30f));
		RepairTickInterval = Bind(cfg, "Advanced", "GearRepairTickInterval", 0.8f, "Conserto de equipamento: segundos por item consertado na bancada.", new AcceptableValueRange<float>(0.1f, 5f));
		RepairUseDistance = Bind(cfg, "Advanced", "GearRepairUseDistance", 2.5f, "Conserto de equipamento: a que distância (em metros) um companheiro tem que estar da bancada para consertar.", new AcceptableValueRange<float>(1f, 5f));
		UpkeepScanInterval = Bind(cfg, "Advanced", "RestockScanInterval", 5f, "Reparar e Reabastecer: segundos entre as procuras por algo para consertar, reabastecer ou organizar.", new AcceptableValueRange<float>(1f, 60f));
		AttackCooldown = Bind(cfg, "Advanced", "AttackCooldown", 0.225f, "Combate: tempo mínimo (em segundos) entre os ataques de um companheiro, somado ao tempo de ataque de cada arma.", new AcceptableValueRange<float>(0.1f, 2f));
		MeleeTooClose = Bind(cfg, "Advanced", "MeleeTooClose", 0.3f, "Combate: mais perto do que isso (em metros, da borda do alvo) um companheiro dá um passo para trás antes de golpear com uma arma de corpo a corpo - uma estocada ou golpe que começa dentro do alvo erra, como um jogador em cima de um javali.", new AcceptableValueRange<float>(0f, 1.5f));
		MeleeReachMargin = Bind(cfg, "Advanced", "MeleeReachMargin", 0.2f, "Combate: um companheiro golpeia com uma arma de corpo a corpo só quando está pelo menos esta quantidade (em metros) dentro do alcance da arma, não bem na borda dele, onde os acertos são incertos.", new AcceptableValueRange<float>(0f, 1f));
		FoodRegenInterval = Bind(cfg, "Advanced", "FoodRegenInterval", 10f, "Comida: segundos entre os tiques de regeneração de vida da comida (o de um jogador é 10).", new AcceptableValueRange<float>(1f, 60f));
		ConsumeCheckInterval = Bind(cfg, "Advanced", "FoodCheckInterval", 1f, "Comida: segundos entre as verificações se um companheiro deve comer ou beber.", new AcceptableValueRange<float>(0.5f, 10f));
		TombstoneScanInterval = Bind(cfg, "Advanced", "TombstoneScanInterval", 5f, "Morte: segundos entre as procuras pela própria lápide que sobrou por perto.", new AcceptableValueRange<float>(1f, 30f));
		TombstoneNavTimeout = Bind(cfg, "Advanced", "TombstoneNavTimeout", 120f, "Morte: segundos que um companheiro continua tentando chegar à própria lápide antes de desistir.", new AcceptableValueRange<float>(10f, 600f));
		// O diagnostico de UI: ligado, o log diz quem esta na frente dos controles (ver UI/ModUiAudit.cs). Fica na
		// [Advanced] e nao na [IA] porque ele nao e da camada de IA: ele audita a UI do mod inteira - o painel F8 e
		// o painel da IA, e a faixa de camadas que os dois compartilham (UI/ModUiLayers.cs). O [IA] EcoDiag esta la
		// porque diagnostica um componente da IA (a voz do companheiro); este e o bucket dos ajustes que ninguem
		// mexe no dia a dia, que e o caso de um despejo de diagnostico.
		UiDiag = Bind(cfg, "Advanced", "UiDiag", defaultValue: false, "Diagnóstico de UI: ligado, escreve no log o que está na frente dos controles do painel F8 e do painel da IA (uma vez por sessão, na primeira vez que cada painel é aberto): quantos alvos de clique cada painel tem (os controles e os que são de propósito), todas as camadas ativas da cena (nome e ordem) e, para cada canvas acima do painel, os alvos que cobrem a coluna de controles. É o que se liga quando um clique ou uma UI parece estranha (um botão que não responde, um texto por cima, um clique engolido): o log diz quem está na frente. Desligado (padrão) não escreve nada e não custa nada; ligado o log cresce bastante (uma linha de resumo por painel, mais uma linha por alvo suspeito). Não muda nada no jogo - só diagnostica.");

		// [IA] - Fase 3.1: the player types an order in the game chat and a local LLM (LM Studio / Ollama / any
		// OpenAI-compatible /chat/completions endpoint) turns it into an action for the companions. See CompanionChatAI.
		// [IA] - Fase 3.1: the player types an order in the game chat and an LLM (OpenRouter by default, or a local
		// LM Studio / Ollama / any OpenAI-compatible /chat/completions endpoint) turns it into an action. See CompanionChatAI.
		AiEnabled = Bind(cfg, "IA", "Ligado", defaultValue: true, "Fase 3.1: digite uma ordem no chat do jogo (\"colete 20 de madeira\", \"segue\", \"para\") e os companheiros a executam. A ordem é interpretada por um LLM (OpenRouter por padrão, ou um servidor local) através de uma API compatível com a OpenAI. Desligado = nada é enviado ao modelo.");
		AiBaseUrl = Bind(cfg, "IA", "BaseUrl", "https://openrouter.ai/api/v1", "URL base do servidor compatível com a OpenAI. Padrão: OpenRouter (https://openrouter.ai/api/v1), que pede a ApiKey - a chave gratuita sai em openrouter.ai/keys. Para rodar tudo local: LM Studio (http://127.0.0.1:1234/v1) ou Ollama (http://127.0.0.1:11434/v1); nesses dois use 127.0.0.1 em vez de localhost (no Windows o localhost resolve primeiro para IPv6 e o servidor só escuta IPv4, o que custa cerca de 2,5 s por ordem). /chat/completions é adicionado automaticamente. Esta é uma opção de texto: edite no arquivo .cfg.");
		AiModel = Bind(cfg, "IA", "Modelo", "openrouter/free", "Nome do modelo a enviar ao servidor. Padrão: openrouter/free (OpenRouter, sem custo). No LM Studio use o nome que o servidor lista (ex.: qwen2.5-3b-instruct). Opção de texto: edite no arquivo .cfg.");
		AiApiKey = Bind(cfg, "IA", "ApiKey", "", "Chave de API, se o servidor pedir uma. No OpenRouter é obrigatória (a gratuita sai em openrouter.ai/keys). Deixe vazio para LM Studio/Ollama rodando na sua máquina. Opção de texto: edite no arquivo .cfg. Nunca coloque uma chave num config compartilhado.");
		AiTemperature = Bind(cfg, "IA", "Temperatura", 0.1f, "O quanto o modelo pode improvisar. Mantenha baixo para que a mesma ordem dê sempre o mesmo JSON.", new AcceptableValueRange<float>(0f, 1f));
		AiTimeoutSeconds = Bind(cfg, "IA", "TimeoutSegundos", 30, "Segundos de espera pelo modelo antes de desistir. O pedido roda em segundo plano, então um modelo lento nunca trava o jogo.", new AcceptableValueRange<int>(1, 300));
		AiPrefix = Bind(cfg, "IA", "Prefixo", "", "Só mensagens de chat que começam com isto vão para o modelo (por exemplo /c). Vazio = toda mensagem de chat é enviada. Opção de texto: edite no arquivo .cfg.");
		AiShowResponse = Bind(cfg, "IA", "MostrarRespostaNoChat", defaultValue: true, "Mostra a resposta crua do modelo no chat, para você ver o que ele entendeu. Útil em testes; desligue para um chat mais limpo.");
		AiCooldown = Bind(cfg, "IA", "CooldownSegundos", 3f, "Tempo mínimo (em segundos) entre duas ordens, para uma rajada de mensagens de chat não inundar o modelo.", new AcceptableValueRange<float>(0f, 60f));
		AiTargetRange = Bind(cfg, "IA", "RaioDeAlvo", 40f, "Fase 3.6: a que distância de você (em metros) uma ordem que nomeia uma criatura a procura (\"ataca o greydwarf\"). A criatura que você está mirando vale sempre, por mais longe que esteja. 40 m é a mesma coleira em que o código de combate abandona um alvo.", new AcceptableValueRange<float>(5f, 200f));
		AiPersonality = Bind(cfg, "IA", "Personalidade", "", "Fase 3.5: a personalidade do seu companheiro nas suas palavras, em português - como ele fala e se comporta (\"fala como um viking leal e direto\", \"é um mago sarcástico que reclama do frio\"). Isso entra nas instruções do modelo, então o companheiro responde nesse tom. Quando preenchido, ganha do PresetPersonalidade. Até 600 caracteres. Opção de texto: edite no arquivo .cfg.");
		AiPersonalityPreset = Bind(cfg, "IA", "PresetPersonalidade", "", "Fase 3.5: uma personalidade pronta, escrita em português dentro do mod: viking (leal e destemido), mago (velho e sarcástico), sarcastico (piadas secas), guia (paciente e didático), ou custom (usa a Personalidade). Vazio ou desconhecido = o padrão (um companheiro leal e prático). Opção de texto: edite no arquivo .cfg.");
		AiMemory = Bind(cfg, "IA", "Memoria", 4, "Fase 3.5: quantas trocas recentes (uma mensagem sua + uma do companheiro) o companheiro lembra e envia junto com a próxima mensagem, para seguir o fio da conversa. 0 = sem memória. Guardada por companheiro, só na memória (nada é salvo no mundo) e esquecida quando o companheiro morre ou é dispensado. Cada troca deixa o prompt que o modelo precisa ler maior, então mantenha pequeno para as respostas mais rápidas (Fase 3.3b: 4, era 6).", new AcceptableValueRange<int>(0, 12));
		AiConversation = Bind(cfg, "IA", "Conversa", defaultValue: true, "Fase 3.5: quando a mensagem NÃO é uma ordem (\"bom dia\", \"como você está?\", \"o que achou da minha casa?\"), o companheiro responde como conversa: fala a resposta em voz alta (balão de fala) e a mostra no chat. As ordens continuam funcionando exatamente como antes. Desligado = só ordens são entendidas, como na Fase 3.1.");
		AiStructuredOutput = Bind(cfg, "IA", "SaidaEstruturada", defaultValue: true, "Pede ao servidor para forçar o formato JSON (response_format/json_schema). Deixa a resposta do modelo sempre válida e é o que permite usar modelos pequenos e rápidos (ex.: um 3B) sem ele responder texto solto. Desligue se o seu servidor não suportar - o mod cai sozinho no formato antigo e avisa no log.");

		// [IA] - STT local (spike do Whisper.net in-process, Fase 3.x): só teste, não faz parte do pipeline de voz.
		SttTest = Bind(cfg, "IA", "SttTeste", defaultValue: false, "SPIKE (teste): roda o teste de reconhecimento de voz do Whisper.net in-process sozinho, cerca de 25 segundos depois de o plugin carregar, e escreve o resultado no log. Desligado = só o comando de console \"modia_stt_teste\" o executa. É um teste simples: não ouve o microfone e não muda nenhum comportamento dos companheiros.");
		SttModel = Bind(cfg, "IA", "SttModelo", @"whisper\ggml-small-q5_1.bin", "Modelo do Whisper que o reconhecimento de voz carrega (as ordens por voz E o comando de console \"modia_stt_teste\"), relativo à pasta do plugin ou um caminho absoluto. Padrão whisper\\ggml-small-q5_1.bin (181 MB, Fase 3.3b - bem melhor em português que o antigo whisper\\ggml-base-q5_1.bin, 57 MB, que continua no disco se você quiser voltar: medido numa CPU de 16 núcleos com 8 threads, 638 ms por clipe de 2 s com o base contra 2187 ms com o small, e o tempo quase não muda com o tamanho do clipe porque o codificador sempre trabalha numa janela de 30 s). Opção de texto: edite no arquivo .cfg.");
		SttWav = Bind(cfg, "IA", "SttWavTeste", @"whisper\jfk.wav", "SPIKE (teste): o arquivo WAV a transcrever, relativo à pasta do plugin (ou um caminho absoluto). Precisa ser WAV PCM (16 kHz mono é o ideal). Opção de texto: edite no arquivo .cfg.");

		// [IA] - Fase 3.3: ordens por voz (push-to-talk + STT local com Whisper.net). O texto reconhecido entra no
		// MESMO caminho das ordens do chat. Ver AI/VoiceInput.cs e AI/WhisperStt.cs.
		VozEnabled = Bind(cfg, "IA", "VozLigada", defaultValue: true, "Fase 3.3: segure VozTecla e fale, e o companheiro aceita o que você disse - as mesmas ordens que você pode digitar no chat (\"colete 10 madeiras\", \"segue\", \"ataca o greydwarf\"). A fala é reconhecida localmente, dentro do jogo, pelo Whisper.net com o modelo de SttModelo: nada é enviado para lugar nenhum. Desligado = a tecla não faz nada (e, na Fase 3.3b, a tecla volta a fazer o que o jogo fazia com ela).");
		VozTecla = Bind(cfg, "IA", "VozTecla", KeyCode.V, "Fase 3.3: tecla de push-to-talk. Segure enquanto fala, solte para a fala ser reconhecida e executada. Só funciona com o jogo em foco e sem nenhuma janela aberta (inventário, mapa, menu, chat, console, os próprios painéis do mod). NOTA (Fase 3.3b): V também é o \"Toggle Auto-Pickup\" do jogo. Enquanto VozLigada estiver ligado, o mod bloqueia esse toggle quando o auto-pickup do jogo está vinculado a esta mesma tecla, para a tecla ficar livre para falar - reatribua o auto-pickup na tela de controles do próprio jogo (ou escolha outra tecla aqui) se quiser ele de volta.");
		VozLanguage = Bind(cfg, "IA", "VozIdioma", "pt", "Fase 3.3: idioma da fala, como um código do Whisper (pt, en, es...). Vazio ou \"auto\" deixa o modelo detectar. Opção de texto: edite no arquivo .cfg.");
		VozMaxSeconds = Bind(cfg, "IA", "VozDuracaoMaxima", 10f, "Fase 3.3: gravação mais longa (em segundos) para uma ordem por voz. Segurar a tecla mais do que isso para a gravação e transcreve o que foi capturado.", new AcceptableValueRange<float>(1f, 30f));
		VozSilenceThreshold = Bind(cfg, "IA", "VozLimiarSilencio", 0.01f, "Fase 3.3: o quão alto o áudio precisa ser (nível de pico de uma janela de 10 ms, de 0 a 1) para contar como fala quando o silêncio do começo e do fim da gravação é cortado. Fase 3.3b: 0.01 (era 0.02) - com 0.02 o começo baixinho de uma palavra era cortado com frequência, e era isso que fazia ordens curtas voltarem erradas. Aumente se o seu microfone capta muito ruído do ambiente.", new AcceptableValueRange<float>(0f, 0.5f));
		VozPromptInitial = Bind(cfg, "IA", "VozPromptInicial", "Ordens para o companheiro: colete madeira, pedra, minerio, ferro, segue, para, vem ca, ataca o greydwarf, equipa o machado de bronze, larga a pedra, descansa, bom dia.", "Fase 3.3b: prompt inicial enviado ao Whisper antes de cada transcrição. Ele enviesa o vocabulário, e é isso que faz ordens curtas (\"colete 10 madeiras\") saírem certas em vez de adivinhadas. Coloque as palavras que você realmente fala (as ordens, os nomes de criaturas e itens); vazio = sem viés. Opção de texto: edite no arquivo .cfg.");
		VozDevice = Bind(cfg, "IA", "VozDispositivo", 0, "Fase 3.3: de qual microfone gravar, como o índice da lista que o log imprime na inicialização ([STT] Push-to-talk: ... 0: nome, 1: nome). 0 = o primeiro (o padrão do Windows).", new AcceptableValueRange<int>(0, 8));
		VozShowText = Bind(cfg, "IA", "VozMostrarTexto", defaultValue: true, "Fase 3.3: mostra o que o microfone ouviu (\"Entendi: \"colete 10 madeiras\"\") no chat e no HUD. Fase 3.3d: a transcrição aparece no balão do PRÓPRIO JOGADOR (não no do companheiro), para uma transcrição errada ser fácil de notar - quem responde é o companheiro, com o balão e A VOZ dele. Desligado = o texto reconhecido só é escrito no log.");

		// [IA] - Fase 3.7: the in-game AI panel. See UI/CompanionAiPanel.cs. Fase 3.10/3.15: the panel has one tab per
		// companion (voice, personality, name and tier of that companion) plus the AI tab (summoner, volume, speed,
		// memory, conversation, push-to-talk). The global personality and voice live in this file / the F8 panel.
		AiPanelKey = Bind(cfg, "IA", "PainelTecla", KeyCode.I, "Fase 3.7: abre o painel da IA no jogo. A aba IA tem o invocador de companheiros de graça, o volume e a velocidade da fala, a memória, a conversa e a tecla do push-to-talk. Cada companheiro tem a sua aba, com o nome, o tipo (renascer), a voz (com o botão Ouvir) e a personalidade DELE - os valores de [IA] abaixo são o padrão de quem não tem escolha própria. Cada mudança é salva na hora. O padrão é I: o jogo e o mod não usam essa tecla. A tecla não é roubada de uma caixa de texto (o chat, o console e o campo de busca ficam em paz). Opção de tecla: edite no arquivo .cfg ou clique nela no painel F8.");

		// [IA] - Fase 3.4: TTS local (Kokoro-82M via sherpa-onnx, in-process). O companheiro fala as respostas em
		// voz alta, na posição dele. Ver AI/CompanionTts.cs.
		TtsEnabled = Bind(cfg, "IA", "TtsLigado", defaultValue: true, "Fase 3.4: o companheiro FALA as respostas em voz alta. A fala é sintetizada no seu computador pelo Kokoro-82M (via sherpa-onnx, dentro do jogo, sem processo filho e sem nada sair da sua máquina), na voz de TtsVoz, e sai no lugar onde o companheiro está (não no centro da tela). Desligado = ele só mostra o balão e o chat, como antes. A primeira fala de cada sessão espera o modelo carregar (~1 s, em segundo plano, quando você entra no mundo).");
		TtsVoice = Bind(cfg, "IA", "TtsVoz", 42, "Fase 3.4: qual voz do Kokoro usar. As vozes em português do Brasil deste modelo são: 42 = pf_dora (feminina, o padrão), 43 = pm_alex (masculina) e 44 = pm_santa (masculina). Os outros números são de outros idiomas (45, por exemplo, soa chinês). 0 (ou qualquer valor negativo) = a voz padrão (42).", new AcceptableValueRange<int>(0, 100));
		TtsSpeed = Bind(cfg, "IA", "TtsVelocidade", 1f, "Fase 3.4: a que velocidade o companheiro fala (1 = normal; acima de 1 fala mais rápido). Também deixa a fala mais curta.", new AcceptableValueRange<float>(0.5f, 2f));
		TtsThreads = Bind(cfg, "IA", "TtsThreads", 4, "Fase 3.4: quantos núcleos da CPU a síntese da fala pode usar. Medido nesta máquina (Ryzen 7 5700X): 4 threads geram mais rápido do que o tempo real (RTF ~0,83), então a fala sai na frente do companheiro; mais threads não aceleram e competem com o jogo.", new AcceptableValueRange<int>(1, 16));
		TtsVolume = Bind(cfg, "IA", "TtsVolume", 1f, "Fase 3.4: volume da voz do companheiro (0 = mudo, 1 = volume normal do jogo).", new AcceptableValueRange<float>(0f, 1f));
		TtsModel = Bind(cfg, "IA", "TtsModelo", @"tts\kokoro\model.int8.onnx", "Fase 3.4: o modelo do Kokoro que o companheiro fala (relativo à pasta do plugin, ou um caminho absoluto). As vozes (voices.bin), os tokens (tokens.txt) e o espeak-ng-data são procurados na MESMA pasta do modelo. Opção de texto: edite no arquivo .cfg.");
		TtsLanguage = Bind(cfg, "IA", "TtsIdioma", "pt-br", "Fase 3.4: idioma do texto falado, no formato que o Kokoro espera. Este modelo é multi-lingue e EXIGE um idioma: \"pt-br\" é português do Brasil (\"pt\" sozinho é português de Portugal). Vazio volta para \"pt-br\". Opção de texto: edite no arquivo .cfg.");
		TtsCache = Bind(cfg, "IA", "TtsCache", defaultValue: true, "Fase 3.4: guarda as falas já sintetizadas (texto + voz + velocidade) para tocar de novo na hora. As falas fixas do mod (\"Já temos o suficiente.\") saem instantâneas; a memória usada é pequena. Desligado = toda fala é sintetizada de novo.");
		TtsSpeakReplies = Bind(cfg, "IA", "TtsFalarRespostas", defaultValue: true, "Fase 3.4: fala em voz alta a resposta do modelo (o campo \"reply\"). Numa conversa é a resposta dele; numa ordem, quando TtsFalarFalasDoMod está desligado, é a confirmação no jeito dele (\"Deixa comigo.\").");
		TtsSpeakModLines = Bind(cfg, "IA", "TtsFalarFalasDoMod", defaultValue: true, "Fase 3.4: fala em voz alta a linha do próprio mod que aparece no chat/HUD (\"Ok: coletar madeira (10).\", \"Atacando o troll.\", \"Já temos o suficiente.\"). Numa ORDEM ela ganha da resposta do modelo (a voz acompanha o que está escrito na tela); desligue para ouvir a resposta do modelo no lugar dela. Nada é falado duas vezes: com as duas opções ligadas, a linha que não é falada vai só para o log.");

		// [IA] - Fase 3.4 (parte 3): os interruptores de TESTE do eco. Todos com o valor padrão = como está hoje.
		TtsReverbZoneMix = Bind(cfg, "IA", "TtsReverbZoneMix", 0f, "Quanto das zonas de reverb do mundo entra na voz do companheiro. 0 (padrão) = voz seca. O Valheim tem uma zona de reverb global (raio de 1000 m, 4 s de cauda) que deixava a fala do companheiro 'ecoando como numa caverna' - a música do jogo usa 0 e o ambiente 0,19. Suba um pouco (ex.: 0,19) se quiser um 'espaço' sutil na voz. Vale na próxima frase, sem reiniciar.", new AcceptableValueRange<float>(0f, 1f));
		TtsEcoDiag = Bind(cfg, "IA", "EcoDiag", defaultValue: false, "Diagnóstico de áudio: ligado, escreve no log (uma vez por sessão, na primeira frase falada) o AudioListener da cena, as zonas de reverb do mundo e como as fontes do próprio jogo estão configuradas (mixer, reverb, bypass). Serve para investigar qualquer problema de som do companheiro. Não muda o som - só diagnostica.");

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
				CompanionsPlugin.Log?.LogInfo($"[IA] Fase 3.3b: [IA] SttModelo estava no valor antigo ({WhisperStt.PreviousModelRelative}) e o modelo novo esta no disco - "
					+ $"passando para \"{next}\" (bem mais preciso em PT-BR; o base continua em disco se voce quiser voltar).");
			}
			else
			{
				CompanionsPlugin.Log?.LogWarning($"[IA] Fase 3.3b: [IA] SttModelo continua em \"{WhisperStt.PreviousModelRelative}\" porque o modelo novo nao esta em \"{path}\". "
					+ "Baixe o ggml-small-q5_1.bin para a pasta whisper\\ do plugin para usar o modelo mais preciso.");
			}
		}
		if (VozSilenceThreshold != null && Mathf.Approximately(VozSilenceThreshold.Value, 0.02f))
		{
			VozSilenceThreshold.Value = (float)VozSilenceThreshold.DefaultValue;
			CompanionsPlugin.Log?.LogInfo($"[IA] Fase 3.3b: [IA] VozLimiarSilencio estava no valor antigo (0.02) - agora {VozSilenceThreshold.Value:0.###}, "
				+ "porque 0.02 cortava o comeco de palavras faladas baixinho (era uma das causas das transcricoes erradas).");
		}
		if (AiMemory != null && AiMemory.Value == 6)
		{
			AiMemory.Value = (int)AiMemory.DefaultValue;
			CompanionsPlugin.Log?.LogInfo($"[IA] Fase 3.3b: [IA] Memoria estava no valor antigo (6) - agora {AiMemory.Value} trocas: cada troca entra no prompt, "
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
		new string[] { "Harvest", "AttackInterval", "Advanced", "GatheringSwingInterval" },
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
