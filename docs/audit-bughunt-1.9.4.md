# Caça a bugs da 1.9.4 — 6 frentes read-only (31/10/2026)

Base: `main` em `cedd854` (1.9.3). **Nada foi editado durante a caça** (árvore limpa no fim); as correções saem em
branches, uma por hipótese, e são marcadas aqui.

O objetivo foi achar o que **sobrou** depois da auditoria da 1.9.2 (`docs/audit-upstream-1.9.2.md`) e do fix da barba
da 1.9.3. As frentes foram divididas por **escopo de arquivo**, para não duplicar trabalho:

| frente | escopo | achados |
|---|---|---|
| Reviewer | `Companion/**`, `Jobs/**`, `Core/**` (fora tombstone/roster/recover/config/localização) | 10 |
| Netcode | `CompanionTombstoneResurrect`, `MHR_Roster`, `MHR_Recover`, `MHR_RespawnTracker`, `MHR_Owner`, `CompanionSetup` (ZDO), `CompanionManager`, `CompanionInteract` (ZDO) | 18 |
| Architect | `UI/**` (fora `CompanionInteract`), `ModConfig`, `ModLocalization`, `Translations/*`, `Speech/*`, wiki, `package/*` | 14 |
| Harmony | `Patches/**` + patches embutidos | 12 |
| IA/ordens/voz | `AI/**` + `CompanionInteract` (ordens) | 16 |
| QA | build, tools, log real do jogador, sinais estáticos, código morto | 10 |

**80 achados brutos → ~60 distintos** depois de juntar as duplicatas (a classe do `GetInstanceID()` aparece em 4
frentes; o `LogLevel` em 2; o inglês fixo em 2).

Ferramentas de apoio (fora do repositório, em `%TEMP%\opencode\`): harness de reflexão sobre a `Companions.dll`
(`audit-ai-1949`, 146 casos), harness do QA (`qa-harness`, 34 checks), medição com `qwen2.5-3b-instruct` no LM Studio
(contrato e schema reais lidos da DLL) e o decompilado do Valheim 1.0.20 (`vh-decomp`).

---

## 1. Verificado pessoalmente (o orquestrador abriu o código)

Estes não dependem do relato de nenhum agente:

| achado | evidência |
|---|---|
| `ChangeTier` copia **2 strings + 1 float** e mais nada | `CompanionManager.cs:678-688` |
| `"no"` está na lista de **negações** | `RadialIntent.cs:428` |
| `SpawnOnHitTerrain` devolve `false` **sem `__result`** | `CombatPatches.cs:322-332` |
| o teto da coleta é calculado e **não é usado** no `Apply` | `CompanionOrders.cs:187` × `:278` |
| `ataca aquele` não cai na mira | `CompanionOrders.cs:335` (só com `target` vazio) |
| migrações **sem gate**, chamadas incondicionais | `ModConfig.cs:1127,1148,1152` |
| **Santuário invoca de graça** (`HireWithCoins` com 0 chamadores) | `StarterCompanionPanel.cs:149-160` |
| `BuildPadHint` **nunca é chamado** | `ConfigPanel.cs:1088` (definição) — 0 chamadas |
| guard de `Capturing` **sumiu** do `Update` | `ConfigPanel.cs:529-571`; `Capturing` só em `:498` |
| `s_noDark` **nunca é atribuído** | `CompanionPanel.cs:264` (é o CS0649 do build) |

---

## 2. P0 — corrigir antes de qualquer release

| # | achado | evidência | status |
|---|---|---|---|
| 1 | **O Santuário de Odin invoca companheiro de graça**: o painel mostra o preço (`CompanionPrice` = 1000), mas o confirm chama o spawn do starter (sem moeda). `HireWithCoins` **não tem chamador**. Efeito colateral: `StarterFix.MarkSpawned()` marca "starter já invocado", então quem invoca no Santuário antes do starter **nunca ganha o starter** | `ShrinePanel.cs:155-166` → `StarterCompanionPanel.cs:149-160`; `CompanionManager.cs:1066`; upstream `8aa6f53:StarterCompanionPanel.cs:147-160` tem o ramo `_hire` — **removido no merge da 1.5.8** (`cd0b2e7` = 1.5.6) | **aberto** |
| 2 | **`ChangeTier` perde `HC_FarmZones`** — o Fazendeiro perde as zonas de plantio | `CompanionManager.cs:505-508,651-688`; `FarmZoneSerializer.cs:10` | **aberto** |
| 3 | **`ChangeTier` perde `HC_Recovering`/`HC_RecoverStance`/`HC_RecoverPickup`** → corpo novo nasce com `HC_AutoPickup = false` e sem o flag de recuperação ⇒ **auto-pickup desligado para sempre** | `CompanionManager.cs:349,455,505-508` × `MHR_Recover.cs:64-68,84-89,288-299` | **aberto** |
| 4 | **Morrer 2× durante a recuperação** → `OnRevived` grava `AutoPickup = false`, o túmulo copia esse `false`, o próximo revive grava `RecoverPickup = false` ⇒ mesma sequela, silenciosa | `MHR_Recover.cs:56-60,291` → `CompanionSetup.cs:983,1034,2351` → `CompanionTombstoneResurrect.cs:410,429` | **aberto** |
| 5 | **A barba da companheira feminina** (1.9.3 trocou um bug por outro) | `CompanionSetup.cs:450-451` × `VisEquipment.cs:1133` | ✅ **corrigido** na branch `fix/barba-sentinela` (ver `docs/roadmap.md`, seção da 1.9.4) |

---

## 3. P1 — comportamento visível

| # | achado | evidência |
|---|---|---|
| 6 | **`ataca aquele` nunca engaja** — o contrato obriga `target`, o 3B manda `"aquele"`, o fallback da mira só roda com `target` vazio ⇒ "Não vejo nenhum aquele por perto" | `CompanionOrders.cs:335-339`; `OrderTargets.cs:55-68` |
| 7 | **`"no"` desliga os comandos** — "aceita os comandos **no** combate" → o modelo diz `command` e a rede inverte para `nocommand` | `RadialIntent.cs:428,163` |
| 8 | **"Primeiro, colete 10 pedras" é recusado** como nome desconhecido (e a voz piora: o Whisper capitaliza o início) | `CompanionChatAI.cs:1171-1179`; `CompanionNames.cs:365-427` |
| 9 | **O teto da coleta não é aplicado — mas só quando a rede por palavras corrige a ação.** No caminho normal (o modelo devolve `stone` com `amount` 10) **funciona** — confirmado em jogo em 31/10/2026: `Order target: mode 2 stops at 10 Stone in the pack` e `[Gather] Order target reached`. Quando o `GatherIntent` corrige `pickup` → `wood`, o número sai do texto do jogador (`outcome.Amount = 10`) mas o `Apply` recebia o amount do **modelo** (0) ⇒ sem `SetOrderCap`, com o chat anunciando `(10)` | `CompanionOrders.cs:187` × `:278` | ✅ **corrigido** na branch `fix/teto-e-tokens` (`outcome.Amount`) |
| 10 | **`HC_Respawn_Inventory` volta com `m_equipped` sujo** — é o único dos 3 caminhos de restauração que não limpa as flags; com `DeathItemRule = 1/2` a mochila enche e a coleta para | `CompanionTombstoneResurrect.cs:463-465` |
| 11 | **`MHR_ResurrectCore` sem `try/catch`** — exceção no `inventory.Load` deixa corpo novo + túmulo armado ⇒ **duplicata** na recarga da zona | `CompanionTombstoneResurrect.cs:400-597` |
| 12 | **`MHR_Roster.Recover` sem `try/catch`** e grava `Persistent = true` antes do load ⇒ corpo meio-construído persistente + geração sem bump | `MHR_Roster.cs:1027-1114` |
| 13 | **F8: Esc fecha o painel no meio do bind** e a tecla seguinte é gravada na config (o guard do upstream sumiu no merge) | `ConfigPanel.cs:529-571`; upstream `8aa6f53:ConfigPanel.cs:351-354` |
| 14 | **"Mod Options" aponta para si mesmo** na 2ª abertura do menu (down do controle trava) | `MenuPatches.cs:79-81` |
| 15 | **`SpawnOnHitTerrain` devolve `null` ao `Aoe.OnHit`**, que desreferencia sem checar ⇒ NRE por golpe em AOE com `springDelay`/`placeOnGround` | `CombatPatches.cs:322-332`; `Aoe.cs:638-657` |
| 16 | **A afirmação sobre um objeto vira trabalho** — "o forno está aceso" → `smelt`; "a cozinha está limpa" → `cook` | `RadialIntent.cs:195-228` |

---

## 4. P2 — config, UI e docs que mentem

| # | achado | evidência |
|---|---|---|
| 17 | **As migrações rodam em todo boot** e revertem escolha legítima (`Memory = 6`, `VozLimiar = 0.02`, os raios antigos, `TtsVoice = 42`) — e o log diz "escolhas suas não são tocadas" (falso) e "coloque 6 de volta" (volta a reverter) | `ModConfig.cs:1127,1148,1152,1185-1274` |
| 18 | **`[Speech] MinInterval`/`MaxInterval` são mortas** — o código usa **90–180 s fixos**; o F8 e 3 docs prometem 15–30 s | `ModConfig.cs:1028-1029` × `CompanionTalk.cs:312` |
| 19 | **Os 3 sliders do LLM são mais estreitos que o `.cfg` e gravam clampado** — `TimeoutSeconds = 200` aparece como 120 e qualquer toque grava 120 | `CompanionAiPanel.cs:1689-1691,1964-1985` × `ModConfig.cs:1075-1079` |
| 20 | **`package/LEIA-ME.txt` ainda é 1.9.1** — vai **dentro** dos zips | `package/LEIA-ME.txt:2,24,117` |
| 21 | **A wiki anuncia 1.9.1** como última release (×5 idiomas) | `docs/wiki/_Footer.md:1`; `Home*.md:111-116` |
| 22 | **`s_noDark` nunca é atribuído** → painel do starter/aparência com a camada escura que o upstream removeu (é o **único warning do build**) | `CompanionPanel.cs:264,266,268`; upstream `:169` |
| 23 | **`BuildPadHint` nunca é chamado** → a legenda do controle (gamepad) não existe | `ConfigPanel.cs:1088,1130` |
| 24 | **Inglês fixo no F8**: `"Press a key..."`, `"Press a button..."`, `"None"` e a dica do controle | `KeyBindListener.cs:49`; `PadBindListener.cs:36,50`; `ConfigPanel.cs:1138` |
| 25 | Starter/aparência/zona de casa/plantio **não seguem a troca de idioma** em sessão | `StarterCompanionPanel.cs:58`; `HomeZonePanel.cs:136`; `FarmZonePlacer.cs:218` |
| 26 | 2 chaves `hc_cfg_*` órfãs ×4 idiomas; o `gen_config_doc` lista 22 de 24 seções e em PT num doc EN | `Translations/*.json:452,609`; `tools/gen_config_doc.py:85` |

---

## 5. P3 — multiplayer / posse (precisa de servidor para validar)

| # | achado | evidência |
|---|---|---|
| 27 | **O fix A4 da 1.9.2 é no-op e o `MHR_WriteOrQueue` nunca enfileira**: `ZNetView.ClaimOwnership` → `ZDO.SetOwner` deixa `IsOwner()` **true na hora, localmente** ⇒ o recheck pós-claim nunca dispara e a fila só existe para `ZNetView` nulo. A "rede de segurança" da 3.19 é **ilusória** | `MHR_ToolKeeper.cs:1057-1067`; `CompanionSetup.cs:714-722`; `ZNetView.cs:245-251` |
| 28 | **Claim de companheiro órfão escreve `HC_Owner` sem posse e sem fila** ⇒ em dedicado reescreve o ZDO **todo frame** e nunca gruda | `CompanionSetup.cs:1177-1182` |
| 29 | **`ToggleAutoPickup` da roda escreve o ZDO direto** (o `SetAutoPickup` com fila já existe) | `CompanionRadialMenu.cs:1810-1818` |
| 30 | **Dono vazio = "de todo mundo"**: `IsMine("")` responde `true` para qualquer jogador; `RestoreFollowTarget`/`RestoreActionMode` gravam `HC_Owner` = jogador local em todo cliente que carrega o objeto | `MHR_Roster.cs:1038`; `MHR_Owner.cs:56`; `CompanionSetup.cs:282-287,2036-2040` |
| 31 | **`RemovePending` antes do teste de duplicata** ⇒ o companheiro não volta na sessão e o túmulo fica armado com os itens | `CompanionTombstoneResurrect.cs:323` × `:344-363` |
| 32 | **`MHR_Chests.Close` desmarca `inUse` sem recheck** ⇒ baú fica "em uso" para sempre e nenhum job o usa | `MHR_ToolKeeper.cs:1072-1078` |
| 33 | **Transferência de itens do túmulo antes do claim** (inferido) ⇒ item nos dois lugares se a réplica estiver atrás | `CompanionTombstoneResurrect.cs:494-528` × `:563` |
| 34 | **`RemoveStaleCopies` pode logar "cópia antiga removida" num despawn normal** (o `Destroy` é adiado ao fim do quadro) | `MHR_Roster.cs:688-712` × `:479-523` |
| 35 | **`EnsureLoaded` remove de `s_entries` sem limpar `s_byId`** ⇒ `Find(id)` devolve entry órfã (NRE possível) | `MHR_Roster.cs:168-173,334-342,785,793` |
| 36 | **`DropInventory` assume que `ItemDrop.DropItem` devolve `null`** para item sem prefab — ele **estoura** e o `catch` aborta o laço | `MHR_Roster.cs:548-557` |

---

## 6. P4 — higiene, performance e código morto

### A classe do H4: estado por `GetInstanceID()` sem `ReferenceEquals`

Só o `MHR_Encumbrance` foi corrigido na 1.9.2; sobraram **9**:

| arquivo | linha | o que vaza num ID reciclado |
|---|---|---|
| `Jobs/MHR_Collect.cs` | `46,72-126,314` | ordem `pickup` ativa "herdada" + `ApplyFollowMode(force: true)` no `Finish` |
| `Companion/CompanionStatus.cs` | `23-29,96` | "por que está parado"/"fazendo" do morto por até 180 s; `s_turn` sem limite |
| `Companion/MHR_Recover.cs` | `34,73,205-210,298` | `Status`/`NoTombSince`/`GearSince` herdados + `TombStone` de referência velha |
| `Patches/ContainerFix.cs` | `14-16,102-178` | contêiner herda `_owned`/`_bytes` ⇒ `Load` pulado |
| `Patches/EnemyHudPatches.cs` | `33-41,162-164` | barras do morto no novo (ou companheiro sem barras) |
| `Patches/MHR_PieceDamageLog.cs` | `14,35-58` | log de peça |
| `Patches/PlayerCombatLog.cs` | `16,129-143` | log de combate |
| `Patches/CompanionFallDamagePatch.cs` | `18,31-35` | veredito de queda |

### Performance

| achado | evidência |
|---|---|
| `Physics.OverlapSphere` **alocado por frame** na coleta de pilha e colmeia (o resto do mod usa `NonAlloc`) | `MHR_PileTaker.cs:269`; `UpkeepHoney.cs:145` |
| `GetComponentsInChildren<Selectable>(includeInactive: true)` **por frame** no F8 | `ConfigPanel.cs:569,576,1112` |
| `LogSceneLayers` **sem gate** + varredura da cena **a cada abertura da roda** | `ModUiLayers.cs:85` ← `CompanionRadialMenu.cs:352` |
| `HasEnemyNearby` varre **todos os personagens** por frame enquanto dorme/senta | `CompanionRest.cs:551,603,946-964` |
| `GetComponent` por golpe **antes** do gate do log | `CompanionHitLog.cs:27`; `DurabilityPatches.cs:15`; `CompanionBlockLog.cs:16` |
| `LocFmt`/`GlyphOf`/`Enum.GetValues(KeyCode)` por frame com a UI aberta | `FarmZonePlacer.cs:277,333`; `HomeZonePanel.cs:175,219`; `KeyBindListener.cs:71` |

### Correção e código morto

| achado | evidência |
|---|---|
| A fila de fala **descarta a linha de *Problem*** (`RemoveAt(0)`) — contradiz o "never lost" do cabeçalho e do CHANGELOG | `CompanionTalk.cs:251-254` |
| O log mente no `tokens/s`: diz "contando prompt e resposta juntos" mas divide só os tokens da **resposta** (o prompt de 2.219 tokens não entra) | `LlmClient.cs:304-305` | ✅ **corrigido** na `fix/teto-e-tokens` (o texto passa a dizer "de geracao - so a resposta") |
| `SmeltController` **pula um item** ao esvaziar pilha inteira (varredura para trás sobre a lista viva) | `SmeltController.cs:560-588` |
| `HC_StarterGear` é escrito em todo init e **ninguém lê** | `CompanionSetup.cs:103,2119-2125` |
| `QueueRespawn`/`ProcessRespawns` mortos nos **dois lados** (a fila nunca é drenada; o `DoRespawn` re-enfileira no erro) | `CompanionManager.cs:772,804,849-856` |
| Nossos: `CompanionPanel.GetCatBtnSprite`/`GetGenderBtnSprite`, `CompanionInteract.PointedCollider` | ver §4 do relatório do QA |
| `[Speech] MinInterval`/`MaxInterval`, `HC_StarterGear`, `HC_Respawn_*` sem leitor | ver P2 |
| `ReturnLogic.OwnerDead` é estado global de sessão (coberto pelo postfix de `Player.OnSpawned`) | `ReturnLogic.cs:33,522`; `PlayerHooks.cs:20` |

---

## 7. P5 — nits das redes por palavras

| achado | evidência |
|---|---|
| `"joga … para o chão"` não é reconhecido como lugar de largar (o `nopickup` fica e desliga o auto-pickup) | `PickupIntent.cs:296,215` |
| `"a colmeia está vazia"` vira ordem (`vazia` em `HiveVerbs`) | `InteractionIntent.cs:69,187-195` |
| `"né"` conta como negação no depósito | `InteractionIntent.cs:250` |
| `"si accomodi"` é entrada morta (tem espaço; o casamento é `_palavra_`) | `InteractionIntent.cs:220` |
| `"pula pra dentro do barco"` não está em `BoardVerbs` | `InteractionIntent.cs:206-212` |
| `IsInventoryRequest` aceita pergunta (`?`), ao contrário do `IsDoorRequest` | `CompanionOrders.cs:725-763` × `:798-803` |
| `drop` sem número quando o modelo perde o `amount` larga a **pilha inteira** | `CompanionOrders.cs:516` |
| `NumberIn` trunca milhar (`1.000` → 1) | `GatherIntent.cs:129-151` |
| `colocar/coloca/por/poe` são alias de `equip` (só sem `StructuredOutput`) | `LlmIntent.cs:644` |
| A memória entra **depois** do contrato (a doc diz que o contrato é a última coisa) | `CompanionChatAI.cs:796-803` |
| A thread de síntese lê config e pode tocar `Localization.instance` | `CompanionTts.cs:926,927,934,1098,1218` |
| `MarkDone()` do STT **sem id** (o LLM já resolveu isso com `LlmResult.Id`) | `WhisperStt.cs:386-389,398-402` |

---

## 8. O que está **sólido** (não gastar tempo aqui)

- **`LlmClient`**: CAS do `_busy`, `_busyId`/`MarkDone(id)`, watchdog com deadline congelado, `Reset` na troca de
  mundo, descarte por id — nenhuma corrida encontrada.
- **`ValidActions` = 35** = `enum` do schema; **0** ações fora; todas as chaves `hc_ai_order_*` das 34 ações existem
  nos 4 JSON; as 25 chaves de `LocFmt` têm `{0}` (e `{1}` onde é usado) nos 4 idiomas.
- **`ItemDrop.DropItem` não remove do inventário** ⇒ o par `DropItem` + `RemoveItem` não duplica nem apaga.
- **`Inventory.Load` faz `m_inventory.Clear()` antes de ler** ⇒ `Recover`/`ResurrectCore`/`ChangeTier` não duplicam.
- **`MHR_ResurrectCore` reivindica o túmulo antes de limpar** (`:563-567`) — o 3.6 da 1.9.2 está fechado nos dois ramos.
- **`RemoveStaleCopies`** usa `PrepareStaleCopyForDespawn` e dropa antes do `Destroy` — correto.
- **A roda reivindica o ZDO ao abrir** (`CompanionRadialMenu.cs:354-357`) e a tecla recusa companheiro de outro jogador
  (`CompanionInteract.cs:46-53`) — o cenário de griefing/dupe por despawn **não** existe (só com `HC_Owner` vazio).
- **`CountMine`** correto depois da A2 (spawn sem id / morto / perdido).
- **Os dois prefixes de `ZInput.GetButtonDown`** (`AutoPickupKeyConflict` + `TextInputKeyGuard`) só forçam `false` e
  devolvem `true` no fast-path ⇒ ordem-independente. Os **93 alvos** de patch existem no decompilado 1.0.20.
- **`HarvestHitRegister`** roda mesmo quando o prefix suprime o dano (como o `AGENTS.md` exige).
- **`m_equipEffect` nunca é nulo**; `p.m_resources[0]` protegido por `IsPile`; `RPC_Extract` da colmeia não exige
  posse (funciona de cliente não-dono); os 4 `s_claimed*` são liberados no `OnDestroy`; `GetAllCharacters` não aloca.
- **Build**: 0 erros, **1 warning** — e ele **é** o achado 22 (`s_noDark`).
- **Tools**: `locale-fallback-check` PASS (5 perfis + 18 idiomas sem perfil), `tts-language-check` PASS (14 vozes
  válidas em 54; `en-gb`/`fr-fr` falham **como documentado**), `audit_translations` ES/FR/IT **0 lacunas**.
- **Log da sessão do jogador**: nenhum `[Patch] failed`, nenhuma exceção, nenhum `Missing attach item`. O `401` sem
  `ApiKey` e o `Translations\English.json` criado em runtime são **esperados**.

---

## 9. Correções à auditoria da 1.9.2

| item | antes | agora |
|---|---|---|
| **B5** (`LogLevel = Off` não cala a IA) | pendência | **falso positivo**: a camada de IA está gated (TTS 57/0, Orders 36/0, ChatAI 29/0, STT 14/0, Voice 13/0 sem gate). O que fura o `Off` são ~325 chamadas de UI/config (6 de migração do `ModConfig`, o `[UiLayers]` e o `[AppearanceDiag]`) |
| **A4** (depósito escrevia sem posse) | "corrigido" | **no-op**: o recheck pós-`ClaimOwnership` nunca dispara (o claim deixa `IsOwner()` true na hora). A proteção real teria que ser RPC ou recheck após 1 quadro — ver achado 27 |

---

## 10. Estado e próximos passos

- **Corrigido até agora:** só a barba (achado 5), na branch `fix/barba-sentinela`.
- **Escopo sugerido para a 1.9.4:** P0 inteiro (1–4), P1 inteiro (6–16), os itens 17/18/19 (config), 20/21 (o que já
  está publicado errado), 22/23/24 (UI) e o **27 documentado** (decidir RPC vs recheck após 1 quadro).
- **Para a 1.9.5:** os `try/catch` de rede (11/12 — mexem em respawn, querem teste em jogo), o P3 multiplayer
  (precisa de servidor), o P4 (9 arquivos, mecânico) e o P5.
- **O teste em jogo é do jogador.** Nada aqui foi validado em servidor dedicado.
