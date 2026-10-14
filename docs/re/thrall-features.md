# Thrall (ego.ai) — Mapa de Features

> Documento de engenharia reversa para **uso pessoal/estudo**. Descreve *logicamente* o que o mod
> fazia, citando classes/métodos como referência. **Nenhum código-fonte do ego.ai é reproduzido
> aqui.** O decompilado bruto foi gerado fora do repositório
> (`%TEMP%\opencode\thrall-decompiled\`) e não deve ser versionado.

Base analisada: **ego.ai Thrall 1.0.11** — `ValheimAIModLoader.dll` e `ValheimAIModLivePatch.dll`
(ambos net472, `AssemblyFileVersion 1.0.0.0`, ano 2024).

---

## 0. Arquitetura em dois assemblies

| Assembly | Plugin (BepInEx) | Papel |
|---|---|---|
| `ValheimAIModLoader.dll` | `egoai.thrallmodloader` v1.0.0 | Bootstrap: registra prefab, console command, e contém as classes `HumanoidNPC` e `ThrallAI`. |
| `ValheimAIModLivePatch.dll` | `egoai.thrallmodlivepatch` v0.0.1 (depende do loader) | "Cérebro" do mod: ~7.3k linhas, IA de comandos, voz, UI, rede com o backend. |

Tipos por assembly (de `ilspycmd -l c`):

* **Loader (5 tipos de topo, sem os `<>c__DisplayClass`)**: `ValheimAIMod.ValheimAIModLoader`,
  `ValheimAIMod.ValheimAIModLoader.DespawnAllCommand`,
  `ValheimAIMod.ValheimAIModLoader.ZNetScene_Awake_Patch`,
  `ValheimAIModLoader.HumanoidNPC`, `ValheimAIModLoader.ThrallAI`.
* **LivePatch (10 tipos de topo + 26 display/coroutine)**: `ValheimAIModLoader.ValheimAIModLivePatch`
  (+ nested `NPCMode`, `PanelManager`, `Resource`), `ValheimAIModLoader.NPCCommandManager`,
  `ValheimAIModLoader.NPCCommand` (+ nested `CommandType`), `PatrolAction`, `HarvestAction`,
  `AttackAction`, `FollowAction`.

### Bootstrap
* `ValheimAIModLoader.Awake()`:
  * `RegisterConsoleCommands()` → `DespawnAllCommand` (`despawn_all`), via Jotunn
    `CommandManager.Instance.AddConsoleCommand`.
  * Carrega **AssetBundle embutido** `"scriptnpc"` (`AssetBundle.LoadFromStream`) e extrai o
    prefab `Assets/CustomAssets/HumanoidNPC.prefab`.
  * `PrefabManager.Instance.AddPrefab(new CustomPrefab(prefab, true))` → o NPC passa a existir em
    `ZNetScene.GetPrefab("HumanoidNPC")`.
  * `harmony.PatchAll()`.
* `DespawnPrefabInstances(prefabName)`: varre `FindObjectsOfType<ZNetView>()`, casa por nome do
  GameObject e chama `ZNetView.Destroy()`.
* `ValheimAIModLivePatch.Awake()` / `DoModInit()`:
  * `Awake()` registra `logMessageReceived` (captura de log), define os caminhos de áudio em
    `Application.persistentDataPath` (`playerdialogue.wav`, `npcdialogue.wav`,
    `npcdialogue_raw.wav`), agenda `SendLogToBrain` a cada 30 s e, se já estiver num mundo, chama
    `DoModInit()`.
  * `DoModInit()` (idempotente via `ModInitComplete`): cria a UI, força `NPCCurrentMode =
    Defensive`, procura NPC, `LoadNPCData()`, `PopulateDatabase()`.
  * Gating: `IsInAWorld()`, `IsLocalSingleplayer()` — o mod assume **single-player local**.

### Ciclo de vida principal (hooks Harmony)
| Patch | Alvo | Efeito |
|---|---|---|
| `Player_Awake_Postfix` | `Player.Awake` | Inicializa mod quando o player entra no mundo; agenda spawn do Thrall se não existir. |
| `Player_Update_Postfix` | `Player.Update` | **Loop principal**: procura NPC (`FindPlayerNPC`), limpa pins, trata teclas (spawn/dismiss, follow/patrol, harvest, combat mode, push-to-talk), sincroniza menu/inventário. |
| `MonsterAI_UpdateAI_Prefix` | `ThrallAI.UpdateAI` | Máquina de estados de comando (patrol → harvest → combat → follow → modo). |
| `HumanoidNPC_CustomFixedUpdate_Postfix` | `HumanoidNPC.CustomFixedUpdate` | Locomoção até o alvo (recurso/itens), ataque a recurso, pickup, detecção de "travado". |

---

## 1. Spawn / Dismiss / Persistência

| Feature | Implementação (Thrall) |
|---|---|
| Spawn do companheiro | `SpawnCompanion()` — `ZNetScene.GetPrefab("HumanoidNPC")` + `Object.Instantiate`, ativa, ajusta `CapsuleCollider.radius`, marca `VisEquipment.m_isPlayer = true` e copia `m_emptyBodyTexture`/`m_emptyLegsTexture` do player. |
| Configuração pós-spawn | `ThrallAI.MakeTame()`, `SetFollowTarget(player)`, `m_viewRange=80`, `m_alertRange=800`, `m_maxChaseDistance=500`, zera flags de fuga, `NPCCurrentMode = Defensive`; `LoadNPCData()`, `EquipBestClothes()`, `SetHair("Hair17")`, `SetMaxHealth(300)/SetHealth(300)`, `Tameable` destruído, `m_walkSpeed`/`m_runSpeed` copiados do player, e cria `Container` em runtime (`inventoryContainer.m_inventory = humanoid.m_inventory`). |
| Roupa padrão | Se inventário vazio: `PickupPrefab("ArmorRagsChest"/"ArmorRagsLegs")` + `EquipItem`. |
| Dismiss | Tecla `spawnKey`: `SaveNPCData()` → `AddPoisonDamage(100000f)` + `m_health = 0` (morte "limpia"), `MessageHud` avisa. |
| Morte | `OnNPCDeath()` (registrado em `Character.m_onDeath`): mensagem HUD + `SaveNPCData()`. |
| Anti-despawn | Patch `ZNetScene.RemoveObjects` reimplementado para **preservar ZDOs persistentes**; NPC tem `ZNetView.m_persistent = true` (definido em `HumanoidNPC.Awake`). |
| Console | `despawn_all [prefabName]` (default `HumanoidNPC`). |
| Pin no mapa | `HumanoidNPC.UpdatePin()` → `Minimap.AddPin(pos, PinType, m_name, false, false, 9990, "NPC")`, com limpeza dos pins órfãos por `Player.Update` (remove pins com `m_author == "NPC"`). |

### Persistência (`thrallmod.json`)
* **`SaveNPCData(GameObject)`** → JSON em `Application.persistentDataPath/thrallmod.json`:
  `name`, `personality`, `voice` (índice), `volume`, `gender`, `MicrophoneIndex`,
  `inventory[]` (`name`, `stack`, `equipped`, `durability`, `quality`), `skinColor[3]`,
  `hairColor[3]`.
* **`LoadNPCData(HumanoidNPC)`** → lê o JSON, aplica em `instance.*`, `ApplyNPCData()`, limpa o
  inventário do NPC (`RemoveAll`, `GetInventory().RemoveAll`, `m_inventory.Clear()`) e re-popula
  via `ZNetScene.GetPrefab(nome)` + `PickupPrefab(prefab, stack, true)`; reaplica
  `m_durability`/`m_quality`; `EquipItem` se `equipped`; no fim `EquipBestWeapon(player,...)`.
  Se o arquivo não existe → `OnNPCPersonalityDropdownChanged(0)` (persona default) + `ApplyNPCData`.
* **`ApplyNPCData(HumanoidNPC)`** → nome (`Character.m_name`), sincroniza widgets, `npcGender` →
  `VisEquipment.SetModel(indice)`, `SetHairColor`/`SetSkinColor` (a partir de `Color` salvo).
* Salvamentos automáticos: ao abrir/fechar inventário do NPC e ao abrir/fechar o menu.
* Config `DisableAutoSave` + patch `Game.UpdateSaving` para desligar autosave do mundo.

---

## 2. Follow

| Feature | Implementação |
|---|---|
| Iniciar/Parar | `Follow_Start(GameObject target, msg)` / `Follow_Stop(msg)` → `ThrallAI.SetFollowTarget`, `NPCCurrentCommandType = FollowPlayer/Idle`, diálogo no chat. |
| Movimento | Patch `BaseAI.Follow` (`BaseAI_Follow_Prefix`) substitui a lógica: distâncias `RunUntilDistance = 3f`, `FollowUntilDistance = 0.5f`; corre só se distância > limiar **e** o player não está agachado nem andando (`IsCrouching()`, `m_walk`); para de andar quando muito perto (folga extra de 5 m se o player agacha). |
| Reaquisição | `ProcessFollowCommand()` → se o alvo de follow não for o player (ou sumiu), volta a seguir o player. |
| Cópia de postura/velocidade | Patches `Player.SetCrouch` e `Character.SetWalk` → replicam agachar/andar no NPC quando em FollowPlayer. |
| Comando implícito | `PreProcessCommand()`: se a fila está vazia, injeta um `FollowAction`. |

---

## 3. Patrol

| Feature | Implementação |
|---|---|
| Iniciar/Parar | `Patrol_Start(msg)` / `Patrol_Stop(msg)`. `patrol_position` = posição do player; `patrol_harvest = true`. |
| Máquina de estado | `ProcessPatrolCommand(__instance, humanoidNPC)`: |
| | • se distância > `chaseUntilPatrolRadiusDistance` (20 m) e `!MovementLock` → limpa agressividade, liga `MovementLock`, volta para `patrol_position` (`BaseAI.MoveTo`). |
| | • se < `patrol_radius - 3` → desliga lock e atualiza `m_lastKnownTargetPos`. |
| | • se < `patrol_radius` → volta a ser agravável. |
| | • se o alvo de follow é um `Character`/`Humanoid` → deixa a IA normal agir. |
| Coleta durante patrulha | Se `patrol_harvest`, procura `Pickable`/`ItemDrop` num raio (`SphereSearchForGameObjectsWithComponent<Pickable>`) dentro de `chaseUntilPatrolRadiusDistance-2`; se não houver nada → "só guardando" e desliga `patrol_harvest`. |
| Ocioso | Sem alvo: `BaseAI.RandomMovementArroundPoint(dt, patrol_position, 7f, false)`. |
| Atalho | Tecla `followKey` alterna **Follow ↔ Patrol**. |

---

## 4. Harvesting (coleta/mineração/lenha)

### 4.1 Banco de recursos (offline, no início do mundo)
* `PopulateDatabase()` varre `ZNetScene.instance.m_prefabs` e classifica por componente:
  `CheckTreeBase`, `CheckTreeLog`, `CheckPickables`, `CheckItemDrop`, `CheckMineRock`,
  `CheckMineRock5`, `CheckDestructibles`, `CheckDropOnDestroyed`, `CheckCharacterDrop`.
* Estrutura: `resourceDatabase[recurso][categoria]` → lista de `Resource`; categorias:
  `TreeLog, TreeBase, MineRock, MineRock5, DropOnDestroyed, Destructible, ItemDrop, Pickable,
  CharacterDrop`. Mapas auxiliares: `resourceHealthMap`, `resourceQuantityMap`, `logToTreeMap`,
  `logToLogMap`, `destructibleToSpawnMap`.
* `AddResourceRelationships()` propaga relações (tronco→toras filhas, tronco→árvore,
  `MineRock5`→fragmentos) para permitir **cadeia de dependência** (ex.: pedra → cascalho → pedra).
* Ordem de preferência: `priorityOrder` (com arma) e `priorityOrderUnarmed` (sem arma).
* Debug: `SaveDatabaseToJson()` → `resource_database.json`; `PopulateAllWeapons()` →
  `all_weapons.json`; `PopulateCraftingRequirements`/`PopulateBuildingRequirements`/
  `PopulateMonsterPrefabs`/`PopulateAllItems` → dicionários de requisitos/recipes.

### 4.2 Pontuação de "facilidade"
* `Resource.CalculateEaseScore(distance, hasWeapon)`: média de `(Min+Max)/2 * 10` (peso 0,3/0),
  `100/Health` (0,3/0,9) e `100/(1+distance)` (0,4/0,1).
* `HumanoidNPC.HasEnoughResource(resourceName, requiredAmount)` conta stacks no inventário.

### 4.3 Escolha da ferramenta
* `GetBestHarvestingTool(tools, resourceDamageModifiers)`: ordena por dano efetivo.
* `ApplyDamageModifier` / `GetDamageModifierValue`: converte `DamageModifier` em multiplicador
  (0=normal, 1=resistente ×0,5, 2=fraco ×1,5, 3/4=imune ×0, 5=muito resistente ×0,25,
  6=longe-de-fraco ×2) — espelha a tabela de resistências do jogo.
* `CalculateWeaponEffectiveness(weapon, resourceModifiers)`: soma `dano_tipo × modificador` para
  blunt/slash/pierce/chop/pickaxe/fire/frost/lightning/poison/spirit. Skills de arco → 0.
* `CalculateEfficiency(name, category, min, max, health, effectiveness, distance)`: pontua por
  categoria (ItemDrop/Pickable ignoram arma; `DropOnDestroyed`, `Destructible`, `TreeLog/Base`,
  `MineRock/MineRock5` favorecem dano; `CharacterDrop` ×0,5; bônus 1,1 para "log"/"half"), com
  fator de distância `1/(1+d/10)`.
* `FindResourceSourcesRecursive(recurso, arma, depth, maxDepth=3)`: expande recursivamente e
  retorna tuplas `(Categoria, Nome, Eficiência, Distância, Profundidade)`; se não há relação
  explícita, recorre à categoria atual.

### 4.4 Execução
* `Harvesting_Start(ResourceName, msg)` → `CleanKey(ResourceName)`, `QueryResourceComplete(...)`,
  monta `ResourceNodesNamesOnly`/`ResourceNodesOneArray`.
* `ProcessHarvestCommand()`: ordena fontes por eficiência; para cada uma chama
  `FindClosestResource(NPC, nome, UnderwaterAllowed:false)`.
  * Se a categoria é `CharacterDrop` → mata a criatura (`SetTarget` + `m_updateTargetTimer` máximo).
  * Senão → `SetFollowTarget(recurso)` (o NPC "caça" o recurso).
  * Se nada encontrado → volta a `"Wood"` e remove o comando.
* `HumanoidNPC_CustomFixedUpdate_Postfix` (coração do harvesting):
  * Só age se `m_follow` não é o player.
  * Ajusta distância de aproximação por tipo de alvo (`ItemDrop`/`Pickable` 2,0; `TreeLog` 1,0;
    `TreeBase` 1,25; demais 0,8).
  * Aproximado: `ItemDrop` → `PickupItemDrop`; `Pickable` → `O_DoInteractAnimation` +
    `Pickable.Interact` + `Destroy`; outros → `StartAttack` (com alternância de ataque secundário
    e regra especial para "log half": ataque normal se `z` favorável, senão 75 % de chance).
  * Longe: se velocidade ≈ 0 por >3 s e `CanMove` falha → ataca mesmo assim.
  * Anti-travamento: se não se move há > `MaxChaseTimeForOneFollowTarget` (20 s) → adiciona a
    `blacklistedItems` e larga o alvo.
* `PickupItemDrop()`: `ItemDrop.CanPickup`/`RequestOwn`/`Load`, checagens de tar/quest/ícone,
  `Inventory.AddItem`, decremento de `HarvestAction.RequiredAmount`, `AddUniqueKey` para quest,
  `ZNetScene.instance.Destroy(go)`, auto-equipar arma se as mãos estiverem livres.
* `patrol_harvest` reaproveita isso para catar drops na área de patrulha.

---

## 5. Inventário

| Feature | Implementação |
|---|---|
| Ver inventário | Tecla `inventoryKey` (hover sobre o NPC) → `InventoryGui.instance.Show(humanoidNPC.inventoryContainer, 1)`; salvar dados ao abrir/fechar. |
| Drag & drop/equip between player↔NPC | Patch completo de `InventoryGui.OnSelectedItem` reimplementado, tratando `m_currentContainer == npc.inventoryContainer` e `m_dragInventory == npc.m_inventory`, `MoveItemToThis`, `UnequipItem`/`EquipItem`, `SetupDragItem`, `ShowSplitDialog`, `DropItem`. |
| Drop all | `Inventory_DropAll` → `DropAllItems()`: `ItemDrop.DropItem(item, stack, pos, rot)` para cada item e `m_inventory.RemoveAll()`. |
| Drop item | `Inventory_DropItem(nome)` → `DropItem()` casa por `m_dropPrefab.name` ou `m_shared.m_name`, dropa e remove. |
| Equipar item | `Inventory_EquipItem(nome)` → normaliza prefixo `$`, `EquipItem()` → `Humanoid.EquipItem(item, true)`. |
| Auto-equipar roupa | `EquipBestClothes()`: melhor armadura por `ItemType` (peito/pernas/elmo/capa via índices de `ItemType`). |
| Contagem | `CountItemsInInventory`, `CheckArrows` (`ItemType` 9 = flechas), `IsRangedWeapon` (4/15 ou `attackType` 2), `IsMeleeWeapon` (3/14/22), `PrintInventoryItems`. |
| Pickup automático | `AutoPickupItems()` (stub vazio) + pickup dirigido em `PickupItemDrop`. |
| Fix de arco | Patch `Humanoid.EquipItem` postfix: para armas de skill arco com `m_aiAttackRange < 10` → força 20. |

---

## 6. Combate

### Modos (`NPCMode`)
* `Passive` / `Defensive` / `Aggressive` (enum nested `ValheimAIModLivePatch.NPCMode`).
* `ToggleNPCCurrentCommandType()` cicla; ao entrar em Passive remove `AttackAction` da fila e
  limpa `enemyList`.
* `SetPassiveMode` / `SetDefensiveMode` / `SetAggressiveMode` e `NPCCurrentModeUpdate()` ajustam
  `m_aggravatable`, `m_alerted`, `m_aggravated`, `SetHuntPlayer`, `m_viewRange` (80) e
  `m_targetCreature`.
* `SetMonsterAIAggravated(thrall, bool)` centraliza ligar/desligar agressividade.

### Aquisição de alvo
* `enemyList` (`HashSet<Character>`) populado no patch `Humanoid.OnDamaged`: se o mod está fora do
  Passive e quem levou dano é o player local ou o `HumanoidNPC`, adiciona o atacante.
* `HandleEnemies()`: escolhe o inimigo mais próximo da lista, `SetTarget` + `m_updateTargetTimer`
  máximo, marca alertado/agravável, `SetHuntPlayer(true)`.
* `Combat_StartAttacking(EnemyName, msg)`: `FindClosestEnemy`, limpa follow, `SetTarget`,
  congela troca de alvo; força modo ≥ Defensive.
* `ProcessCombatCommand()`: se o alvo morreu/nome não casa → novo `FindClosestEnemy`; se não achar
  → remove o comando.
* `FindEnemies()` / `FindClosestEnemy(go, EnemyName)`: varre `Character` não `tamed`, ordena por
  distância e casa por prefixo de nome (`IsStringStartingWith`).
* `Character_OnDeath_Prefix`: decrementa `AttackAction.TargetQuantity` quando morre o alvo
  (`lastAttackedObjectZDOID` marcado no patch de `Humanoid.StartAttack`).

### Sneak / Defend
* `Combat_StartSneakAttacking`: limpa follow, `m_alerted=false`, `m_aggravatable=true`,
  `SetHuntPlayer(true)`, `SetCrouch(true)`; em `BaseAI.Follow` prefix o NPC **não corre** em
  sneak.
* `Combat_StartDefending`: limpa agressividade e fica no modo Defensive.
* `Combat_StopAttacking`: limpa follow/agressividade, volta a Idle.
* Manter distância: `m_circleTarget*` herdado do `ThrallAI` (comportamento tipo MonsterAI).

### Escolha de arma em combate
* Patch completo de `Humanoid.EquipBestWeapon` reimplementado (o original do jogo), considerando
  `optimalWeapons`/`outofRangeWeapons`/`allWeapons`, `m_aiTargetType` (0/1/2),
  `CanUseAttack`, checagem de flechas para armas à distância e `m_aiPrioritized`.
* `ThrallAI.SelectBestAttack` + `DoAttack` (cópia do MonsterAI) e `UpdateConsumeItem` para comida.

### Dano
* Patch `Character.RPC_Damage` reimplementado por completo (dificuldade, `SEMan.OnDamaged`,
  `AggravateAllInArea`, backstab, stagger, block, pushback, status effect, weakspot, armadura,
  dano elemental via `AddFireDamage`/`AddSpiritDamage`/`AddPoisonDamage`/`AddFrostDamage`/
  `AddLightningDamage`) — necessário para que armaduras/durabilidade do NPC funcionem.
* `Character_OnDamaged_Prefix(Humanoid)` → alimenta `enemyList`.
* `HumanoidNPC.DamageArmorDurability` / `GetBodyArmor` / `ApplyArmorDamageMods` sobrescritos.
* Patches `BaseAI.FindRandomStaticTarget` e `FindClosestStaticPriorityTarget` retornam `null`
  para o NPC → **o Thrall não ataca construções** por conta própria.
* `ThrallAI.OnDamaged` acorda, alerta e marca o atacante como alvo.

---

## 7. Voz do jogador (Push-to-Talk + STT)

| Etapa | Implementação |
|---|---|
| PTT | `Player_Update_Postfix`: `ZInput.GetKey(talkKey)` → `StartRecording()`; soltar → se > 1 s `StopRecording()` + `SendRecordingToBrain()`. |
| Seleção de microfone | `CreateMicInput()` (dropdown com `Microphone.devices`, truncado em 27 chars), `OnMicInputDropdownChanged`, `MicrophoneIndex` persistido no JSON. |
| Gravação | `StartRecording()`: valida `Microphone.devices.Length`, `Microphone.Start(device, false, recordingLength=10, sampleRate=22050)`; mostra "..." na conversa. |
| Fim | `StopRecording()`: `Microphone.End`, `TrimSilence()`, `SaveRecording()`, remove o texto mundial temporário. |
| Trim | `TrimSilence()`: lê amostras, corta zeros no fim, recria `AudioClip` em 44100. |
| Salvamento | `SaveRecording()` → `EncodeToWav` → `playerdialogue.wav`. |
| Encoder WAV | `EncodeToWav(clip)`: escreve header RIFF/WAVE/fmt/data manualmente, `bitDepth = 8`, mono/estéreo conforme clip; PCM 8 bits (unsigned) ou 16 bits. |
| Envio | `GetBase64FileData(path)` → `Convert.ToBase64String` → campo `player_instruction_audio_file_base64` no JSON (ver `backend-contract.md`). |
| Guarda | `shortRecordingWarningShown` (gravações < 1 s são descartadas). |

---

## 8. TTS e reprodução de áudio

| Etapa | Implementação |
|---|---|
| Pedir áudio | `BrainSynthesizeAudio(text, voice)` → `GET /synthesize_audio?text=..&voice=..&player_id=..` (+ `use_cartesia=true` para as vozes da lista `cartesiaVoices`). |
| Resposta | `OnBrainSynthesizeAudioResponse` lê `audio_file_id` e `text` do JSON. |
| Download | `DownloadAudioFile(audioFileId)` → `GET /get_audio_file?audio_file_id=..&player_id=..` (dados binários). |
| Salvar | `OnAudioFileDownloaded` → grava `npcdialogue_raw.wav`, reporta tempo de resposta do "cérebro". |
| Tocar | `PlayWavFile` → `WavToAudioClip` (parser RIFF manual: 8/16 bits, mono) → `MyPlayAudio`. |
| Saída | `MyPlayAudio(clip)`: cria GameObject "One shot audio" na posição do player, `AudioSource` com `spatialBlend=0`, `volume = npcVolume/100`, `bypassEffects/ListenerEffects/ReverbZones`, `PlayOneShot(clip, 5f)`, destrói após `clip.length`. |
| Extras | `LoadAudioClip` (leitura alternativa), `CompareAudioFormats` (debug), botão **Preview** (usa frase fixa de demonstração). |

---

## 9. Personalidade, voz, corpo e aparência

* `npcPersonalities` (11): Hermione Granger, Raiden Shogun, Childe, Draco Malfoy, Gawr Gura,
  Elon Musk, Shadow the Hedgehog, Tsunade, Yor Forger, Tsundere Maid, **Custom**.
* `npcPersonalitiesMap`: mapas `nome → prompt longo` de personalidade (passados crus ao backend no
  campo `personality`). O time de design deve criar **prompts próprios**.
* `npcVoices` (19): Asteria, Luna, Stella, Athena, Hera, Orion, Arcas, Perseus, Orpheus, Angus,
  Helios, Zeus, 1920s Radioman, ASMR Lady, Hinglish Speaking Lady, Madame Mischief,
  Pilot Over Intercom, Princess, Wizardman.
* `cartesiaVoices`: subconjunto de 7 vozes que o backend atende pela integração Cartesia
  (as demais são do motor próprio do ego.ai).
* UI: `CreatePersonalitySection` (dropdown + `InputField` multilinha), `OnPersonalityTextChanged`
  (texto livre → seleciona "Custom"), `CreateVoiceAndVolumeControls` (dropdown + slider de volume
  + botão Preview), `CreateBodyTypeToggle` (masculino/feminino → `npcGender`),
  `CreateAppearanceSection` + `CreateSkinColorPicker`/`CreateHairColorPicker`
  (`ColorPicker` do Jotunn), `CreateNameSection`.
* Envio ao backend: `personality`, `voice`, `use_cartesia`, `gender`, `agent_name`.

---

## 10. Comandos (voz e chat) e fila

### Entrada
* **Voz**: PTT → `SendRecordingToBrain()` → `BrainSendInstruction(npc, Voice:true)`.
* **Chat**: patch `Chat.InputText` (`Chat_InputText_Prefix`) → se single-player local e o texto não
  começa com `/`, chama `BrainSendInstruction(npc, Voice:false)` (usa
  `player_instruction_text`) e **suprime** o envio normal do chat.
* **Periódico**: `StartBrainPeriodicUpdateTimer()` → a cada 5–12 s (com 50 % de chance) envia
  `BrainSendInstruction(npc, Voice:false)` com o estado do jogo (sem áudio).

### Saída do backend → ações
`OnBrainSendInstructionResponse` / `OnBrainSendPeriodicUpdateResponse` leem:
`agent_text_response`, `agent_text_response_audio_file_id`, `player_instruction_transcription`,
`agent_commands[]` (cada um com `category`, `action`, `parameters[]`).

* Fala: `AddChatTalk(player,...)` + `AddChatTalk(npc,...)` e `DownloadAudioFile(file_id)`.
* `ProcessNPCCommand(category, action, parameter, agent_text_response)`:
  `category == "Inventory"` → `DropAll` / `DropItem` / `EquipItem` (`PickupItem` é no-op).
* `category == "Harvesting"` → `HarvestAction{ResourceName, RequiredAmount, OriginalRequiredAmount}`.
* `category == "Patrol"` → `PatrolAction{patrol_position = player.pos, patrol_radius = 15}`.
* `category == "Combat"` → `AttackAction{TargetName, TargetQuantity, OriginalTargetQuantity}`.
* `category == "Follow"` → `FollowAction`.
* Cada comando é adicionado à UI de fila (`AddItemToScrollBox`) e a `RefreshTaskList()`.

### Fila (`NPCCommandManager`)
* `AddCommand` insere na **posição 0**; `GetNextCommand()` devolve o primeiro não concluído e
  **remove/anuncia** os concluídos ("Done …" via chat + `BrainSynthesizeAudio`);
  `RemoveCommand(index)`, `RemoveCommandsOfType<T>()`, `GetAllCommands`, `RemoveAllCommands`.
* `NPCCommand` (abstrato): `Type` (`CommandType`), `priority`, `expiresAt`, `DestroyOnComplete`,
  `humanoidNPC`, `NPC`, `IsTaskComplete()`, `Execute()`.
* `CommandType`: `Idle, FollowPlayer, CombatAttack, CombatSneakAttack, CombatDefend,
  HarvestResource, PatrolArea, MoveToLocation`.
* Conclusão: `HarvestAction.IsTaskComplete()` (`RequiredAmount <= 0`),
  `AttackAction.IsTaskComplete()` (`TargetQuantity == 0`), Patrol/Follow sempre `false`.
* `PreProcessCommand()`: traduz o comando atual da fila em *effects* (`Harvesting_Start`,
  `Patrol_Start`, `Combat_StartAttacking`, `Follow_Start`).

---

## 11. UI / Menu e keybinds

* `PanelManager`: `CreatePanel` (via Jotunn `GUIManager.Instance.CreateWoodpanel`), `TogglePanel`
  (liga/desliga e chama `GUIManager.BlockInput`), `DestroyAllPanels`, `CreateSubPanel`, título com
  fonte `NorseBold` em `ValheimOrange`.
* `CreateModMenuUI()` cria:
  * **Settings** (480×760): Task Queue, Keybinds, Mic Input, banner ego.ai (link Discord).
  * **Thrall Customization** (450×880): Name, Personality, Voice, Body Type, Appearance, Save.
* `CreateScrollBox`/`AddItemToScrollBox`/`RefreshTaskList`/`DeleteAllTasks`: fila de tarefas com
  `ScrollRect` + `VerticalLayoutGroup` + `ContentSizeFitter` + `Scrollbar`.
* `CreateKeyBindings`/`RefreshKeyBindings`/`OnEditKeybind`/`ListenForNewKeybind`: editor de
  teclas — o coroutine varre todo o enum `KeyCode` com `ZInput.GetKeyDown` e grava no
  `ConfigEntry<KeyCode>`, evitando duplicatas.
* `Character_GetHoverText_Prefix`: substitui o hover do NPC por um guia de teclas
  (inventoryKey, talkKey, thrallMenuKey, combatModeKey + `NPCCurrentMode` atual).
* Helpers de UI em geral: `CreateSlider`, `CreateToggle`, `CreateToggleVisuals`,
  `CreateCircleSprite`/`CreateRectangleSprite`/`CreateCheckmarkSprite`, `TruncateText`,
  `CreateMultilineInputField`, `CreateNameInputField`.

---

## 12. Configurações (ConfigEntry)

| Seção | Chave | Tipo | Default | Uso |
|---|---|---|---|---|
| Bool | `LogToBrain` | bool | `true` | Envia logs ao backend. |
| Bool | `DisableAutoSave` | bool | `false` | Patch em `Game.UpdateSaving`. |
| Keybinds | `Spawn` | KeyCode | `G` (103) | Spawn/dismiss. |
| Keybinds | `Harvest` | KeyCode | `H` (104) | Inicia/para coleta ("Wood", 20). |
| Keybinds | `Follow` | KeyCode | `F` (102) | Alterna Follow/Patrol. |
| Keybinds | `Talk` | KeyCode | `T` (116) | Push-to-talk. |
| Keybinds | `Inventory` | KeyCode | `E` (101) | Abre inventário do NPC. |
| Keybinds | `Menu` | KeyCode | `Y` (121) | Abre/fecha menu. |
| Keybinds | `CombatMode` | KeyCode | `J` (106) | Cicla Passive/Defensive/Aggressive. |
| (runtime) | `BrainAPIAddress` | string | — | Declarado mas **não lido** na lógica: a URL vem de `GetBrainAPIAddress()` (decrypt). |

---

## 13. Log/telemetria

* `CaptureLog` via `Application.logMessageReceived` acumula `logEntries` (limite 10 000).
* `SendLogToBrain()` a cada 30 s (`RestartSendLogTimer`) → POST `/log_valheim`
  (`player_id`, `timestamp`, `log_string`).
* `OnDestroy` envia o log final (a menos que Alt esteja pressionado).

---

## 14. Pontos frágeis observados (lições para a reimplementação)

1. **Estado global estático** para tudo (`PlayerNPC`, `NPCCurrentMode`, `ResourceNodes`, `Key`/`IV`)
   — dificulta multi-NPC e testes.
2. **Um único NPC**: `FindPlayerNPC` destrói instâncias extras se houver >1.
3. **Só single-player local** para chat/voz (`IsLocalSingleplayer`).
4. **Dependência de nomes de GameObject** (`Contains("HumanoidNPC")`, `StartsWith("$)`) em vez de
   componentes/IDs.
5. **Loop de 1–3 s + varreduras de cena** (`FindObjectsOfType` a cada 3 s em 300 m) — caro.
6. **WebClient síncrono/async antigo**, JSON via `SimpleJson` (do Jotunn) e polling em vez de
   streaming; timeouts de 10 s.
7. **Sem suporte a multi-idioma** além do que o backend devolve.
8. **Lógica duplicada do jogo** (`MonsterAI.UpdateAI`, `Character.RPC_Damage`,
   `Humanoid.EquipBestWeapon`, `InventoryGui.OnSelectedItem`) — copiada para dentro do mod para
   poder alterar comportamento; é a maior fonte de quebra de versão (ver `api-diff.md`).
