# Auditoria da 1.9.2 — merge do upstream 1.5.4 → 1.5.8 + as 5 ordens de interação

Branch auditada: **`feat/ordens-interacao`** (base `main` = `f338d50`, a **1.9.1** publicada).
O que a branch contém: o merge incremental do upstream (`f7c4761` 1.5.5, `bc6016f` 1.5.6, `c0b81f2` 1.5.7,
`6a1205d` 1.5.8 + correções `3433292`, `baba127`) e as **5 ordens novas** (`hive`, `door`, `board`, `sit`,
`deposit`).

Pergunta da auditoria: **o merge sobrescreveu alguma correção/adaptação nossa, e as 5 ordens novas estão
seguras antes do teste em jogo?**

## 1. Método

Sete frentes independentes, **read-only** (nenhuma alterou arquivo, rodou o jogo ou compilou), cada uma
provando item por item contra `AGENTS.md`, `docs/roadmap.md`, `package/CHANGELOG.md` e `docs/re/*`:

| # | Frente | Como provou |
|---|---|---|
| 1 | **Global** (`valheim-mod-reviewer`) | cada decisão documentada do nosso mod × a árvore, `arquivo:linha` |
| 2 | **IA/voz/ordens** (`ai-npc-developer`) | leitura + **harness de reflexão** sobre a DLL real (172 casos) |
| 3 | **Config/UI/localização/docs** (`valheim-mod-architect`) | `ModConfig`/`ModLocalization`/UI × `main` e × upstream; doc gerada comparada byte a byte |
| 4 | **Rede/ZDO** (`valheim-netcode-dev`) | posse, `MHR_WriteOrQueue`, roster, túmulo, 5 ordens em dedicado/2 jogadores |
| 5 | **Harmony** (`harmony-patcher`) | 102 classes / 93 alvos contra o **decompilado do Valheim 1.0** e o **Harmony do BepInEx 5.4.23** |
| 6 | **O que do upstream ficou de fora** (`valheim-reverse-engineer`) | `git diff 8aa6f53 HEAD` por arquivo, trecho a trecho |
| 7 | **Contrato/schema** (`llm-backend-dev`) | enum/JSON schema + **184 checks** de parser e das redes |

## 2. Números de controle

- O upstream mudou **121 arquivos** (1.5.4 → 1.5.8: +14.907 / −2.600).
- **63 deles estão byte a byte idênticos** na nossa branch (o merge não os tocou).
- **58** são o conjunto de conflito resolvido à mão, **todos classificados**: `CarryLimit` → `CompanionCarry`
  (nossa 3.22), `SpeechConfig` nosso (M1), `Chatter` bool (3.3c), faixa de UI **1701+**, localização, docs.
- Das **276 linhas do upstream ausentes** da nossa árvore, **5 blocos eram funcionais** (os achados A1, A7,
  B6, o "por que está parado" da coleta e o `m_carryWeightRate`) — o resto é intencional, cosmético ou doc.
- **Nenhum patch Harmony novo do upstream colide** com os nossos (os que dividem método são
  ordem-independentes).
- **Nenhuma escrita nova de ZDO sem posse** foi introduzida pelas 5 ordens (elas não escrevem ZDO).

## 3. Achados

Legenda de status: **corrigido** (nesta rodada, antes do teste), **adiado** (decidido não mexer antes do
teste, com o motivo), **pendente** (aberto no backlog). Os commits das correções: `c546801` (A1, A7),
`e0b8b3d` (P1), `d3fb7a8` (chave morta), `0270af6` (B1, B2, B4, aliases), `42971bf` (A3, A4, A5, B3, B7, B8,
claim storm), `0004aba` (log duplicado), `6447f9c` (A2, A6, 3.6), `b0eca32` (H1, H2, H4, H5, nit) e os
commits das traduções e da wiki (B6, P2, 75 + 2 chaves, 5 pools de fala).

### 3.1 Alto

| ID | Achado | Evidência | Status |
|---|---|---|---|
| **A1** | **Zonas de plantio somem quando o companheiro morre.** O **leitor** de `HC_Respawn_FarmZones` sobreviveu ao merge (`CompanionTombstoneResurrect.cs:53,434`), o **escritor** não. O companheiro voltava do túmulo sem nenhuma zona (o caminho do Santuário mantinha, porque usa o roster) | upstream escreve em `CompanionSetup.CreateCompanionTombstone` (`8aa6f53:…:1653`); nosso bloco equivalente ia de `TombstoneId` a `RespawnGen` sem essa linha | **corrigido** `c546801` |
| **P1** | **O F8 gravava o primeiro valor de `LogLevel`/`PortalItems`/`RespawnStance`.** O merge mandou todo `string` com `AcceptableValueList` para o nosso `BuildChoiceRow` (dropdown da voz, M2), que não tem caso genérico e devolvia a lista de idiomas do Whisper; o `Clamp` do BepInEx transformava qualquer escolha em `AcceptableValues[0]` ⇒ **`LogLevel = Off`** (o mod fica mudo), `PortalItems = AllowAll` e `RespawnStance = Passive` inalcançáveis | `ConfigPanel.cs:1276-1279` (dispatch) × `1765-1797` (`BuildChoice` sem caso genérico), `1931-1940` (`ApplyChoice`), `AcceptableValueList.Clamp` | **corrigido** `e0b8b3d` |
| **B1** | **"fecha a porta" fechava o inventário.** Com `StructuredOutput` (padrão) o schema `strict` só aceita o enum das 35 ações, então o modelo devolve `inventory_close` (o "fecha" é alias disso desde a 3.8) e `IsInventoryWord` é testado **antes** de `IsDoorWord`; não havia rede por palavras para a porta (o `IsInventoryRequest` exige palavra de mochila) | `LlmIntent.cs:267-276,531`; `CompanionOrders.cs:246-249,713-751`; `RadialIntent.PreciseActions` protege `inventory_close` de qualquer correção | **corrigido** (novo `IsDoorRequest`) |
| **V1** | **A voz não seguia a troca de idioma com o mundo aberto.** O Kokoro é um modelo só, mas o **fonemizador (espeak) é escolhido no load** e o `OfflineTts` era carregado uma vez e mantido: trocar o jogo para PT-BR deixava `pf_dora` lendo português com fonemas ingleses, e o **cache de áudio** (`voz+velocidade+frase`, sem o idioma) seguia servindo as amostras antigas — o painel e o log diziam `pt-br` e mentiam. Provado no log do jogador: modelo carregado às 23:17 com `idioma "en-us"`, `[Locale] Portugues (Brasil)` às 23:21, e a fala seguinte sintetizada com `voz 42` (54.615 amostras) — no harness o mesmo texto sai com 80.062 amostras em `pt-br` e 76.495 em `en-us` | `CompanionTts.cs` (`TryGetTts` sem `_loadedLanguage`; `CacheKey` sem o idioma). O arquivo é **idêntico à `main`**: não é regressão do merge, é a promessa da M1 ("idioma sem reiniciar") que o código nunca cumpriu | **corrigido** (reload na thread de síntese quando `Language` != `_loadedLanguage`, cache esvaziado, `NoticeLanguageChange` no postfix do `SetupLanguage` e no `SettingChanged` do `[AI] TtsLanguage`; a lista do F8 passa a ter só os 5 idiomas — medido: `en-gb` e `fr-fr` **quebram** no `Generate`) |

### 3.2 Médio

| ID | Achado | Evidência | Status |
|---|---|---|---|
| **B6** | **A reforma da fala da 1.5.5 estava muda.** O merge trouxe 5 pools (`Wood/Stone/Ore/Upkeep/Chat`), mas o nosso `SpeechConfig.Merge` lista 15 categorias à mão e os 4 `Speech/*.json` têm 15 ⇒ `Wood/Stone/Ore` caem no `Gather` e `Upkeep`/`Chat` retornam `null` (sem crash: `Pick`/`Or` são null-safe) | `SpeechConfig.cs:56,61` × `:243-257`; `CompanionTalk.cs:610`; `IdleController.cs:534` | **corrigido** (`Merge` por reflexão + 5 pools nos 4 idiomas) |
| **A3** | **`guarda isso no baú` ignorava cerca/privacidade.** `IsChestForOrders` só exigia "tem inventário" ⇒ aceitava baú **privado de outro jogador** (e o `MHR_Chests.Open` **toma posse** do ZDO dele), túmulo, carrinho e navio. Os jobs já tinham o filtro certo | `CompanionInteract.cs:1942-1945,1739-1797` × `MHR_ToolKeeper.cs:1009-1029` | **corrigido** |
| **A4** | **O depósito escrevia no baú sem conferir a posse** — a mesma classe de bug que a 3.19 resolveu para o ZDO do companheiro. Em cliente dedicado o item pode sair da mochila e ser sobrescrito | `MHR_ToolKeeper.cs:1049-1060` (Claim + `SetInUse` sem recheck); escrita em `CompanionAI.cs:1485-1491` | **no-op** — a 1.9.4 provou que o recheck pós-claim **nunca dispara**: `ZNetView.ClaimOwnership` → `ZDO.SetOwner` deixa `IsOwner()` true na hora, localmente. A proteção real teria que ser RPC ou recheck após 1 quadro (ver `docs/audit-bughunt-1.9.4.md`, achado 27) |
| **B2** | **As 5 ordens novas não tinham rede por palavras nenhuma.** Com o modelo devolvendo `none` (7/7 casos inequívocos no harness): "guarda a comida no baú", "esvazia a colmeia", "fecha a porta", "embarca no navio", "senta perto do fogo" ⇒ "Não entendi" | `CompanionChatAI.cs:958-985` (só `GatherIntent`/`RadialIntent`) | **corrigido** (novo `InteractionIntent`) |
| **B3** | **`senta` com o companheiro já sentado por ordem: ele levanta e a resposta diz que ninguém atendeu.** `DirectSit` desliga sempre que `_isDirected && (_isSitting \|\| _navigating)`, ignorando **qual** fogo; `OrderSit` então devolve `Companion = null` ⇒ `hc_ai_no_companion` | `CompanionRest.cs:297-302`; `CompanionInteract.cs:2170-2178`; `CompanionChatAI.cs:1050-1054` | **corrigido** |
| **B8** | **O alvo "apontado" das ordens novas não é o da tecla.** `Pointed<T>` olha só o **1º hit** do raio; a tecla varre todos até achar o componente ⇒ alvo semi-ocluo cai silenciosamente no "mais próximo" (baú errado a 5 m, porta errada a 20 m) | `CompanionInteract.cs:1847-1877` × `:775-883` | **corrigido** |
| **A2** | **`MaxCompanions` não libera vaga no Despawn com `RecoverUnsummoned = true`** (padrão): o entry vira `Unsummoned`, `CountMine` conta todos e a limpeza só acontece ao recarregar o mundo — o texto da config promete que despawnar libera. Bônus: `CountMine` conta duas vezes o companheiro **sem id** (spawn recém-carregado em dedicado ⇒ "você já tem 11") | `MHR_Roster.cs:628-654,378-425,153-164`; `ModConfig.cs:733` | **corrigido** (texto honesto + dupla contagem; a semântica **não** muda antes do teste) |
| **P2** | **75 rótulos do F8 + 2 abas sem tradução** nos 4 idiomas (265 de 340 traduzidos) — exatamente as configs que a 1.9.2 restaurou/introduziu | `ConfigPanel.cs:1245,1054` × `Translations/*.json` | **corrigido** |

### 3.3 Baixo

| ID | Achado | Evidência | Status |
|---|---|---|---|
| **A5** | 4 escritas diretas de `HC_ActionMode` fora da fila da 3.19 (`DirectSmelt`, `DirectGatherMode`, `CancelAll`, `ExitGatherMode`): em dedicado o claim em voo pode reverter o modo | `CompanionInteract.cs:1589,2322,2425,2615` | **corrigido** |
| **A6** | O **perfil de IA** (voz/personalidade) não sobrevive à recuperação pelo **roster** (o túmulo guarda; o Santuário volta ao global) | `MHR_Roster.cs:40-73,959-1045` | **corrigido** |
| **A7** | **`MHR_Recover.Cancel` era código morto:** escolher postura não encerrava a recuperação (a própria fala prometia) e o auto-pickup ficava desligado | `MHR_Recover.cs:81-92` (0 chamadores) | **corrigido** `c546801` |
| **B4** | A **postura rouba** as 5 ordens novas ("fecha a porta na postura passiva" ⇒ só troca postura) | `RadialIntent.cs:286-290` (`PreciseActions` sem as 5) | **corrigido** |
| **B7** | `deposit` conta **pilhas** e a frase diz "item(ns)" (1 pilha de 50 madeiras ⇒ "1 item(ns)") | `CompanionInteract.cs:2220-2231` | **corrigido** |
| **3.6** | Escrita sem claim no ramo "recuperado depois" do respawn (o outro ramo já tinha) | `CompanionTombstoneResurrect.cs:331-332` × `:558` | **corrigido** |
| aliases | `guarda_no_bau` e o FR `assieds_toi` caem em `none`; 32/48 formas "de frase" das 5 ações só funcionam quando a ação nomeia o objeto (com `StructuredOutput=false`) | `LlmIntent.cs:531-539` | **corrigido** |
| log | Linha de injeção da localização **duplicada** (`:881` incondicional + `:884` com gate do `LogLevel`) — escreve até com `Off` | `ModLocalization.cs:881,884` | **corrigido** |
| config | `[Advanced] GatheringSwingInterval` era **chave morta** (o upstream a removeu na 1.5.6) | `ModConfig.cs:432,1043` | **corrigido** `d3fb7a8` |
| **H1** | `MHR_PieceDamageLog` monta `Vector3.Distance` contra **todos** os companheiros + strings **antes** do gate do log; `WearNTear.ApplyDamage` roda peça por peça no desgaste de base grande | `Patches/MHR_PieceDamageLog.cs` | **corrigido** |
| **H2** | `CompanionBlockLog` faz `MethodInfo.Invoke` + `GetValue` boxado **antes** do gate em todo bloqueio de jogador e companheiro | `Patches/CompanionBlockLog.cs` | **corrigido** |
| **H4** | `MHR_Encumbrance.s_isCompanion` indexa por `GetInstanceID()` e o Unity **reaproveita** IDs (personagem novo herda o veredito de um companheiro morto) | `AI/MHR_Encumbrance.cs` | **corrigido** |
| **H5** | Falha de instalação dos patches manuais (`EquipDiag`, `ContainerFix`) é engolida com log só em `Detailed` | `EquipDiag.Install`/`PatchOne` | **corrigido** |
| nit | `MHR_Pad.HidePatch` usa `StartsWith("Joy")` culture-sensitive em `ZInput.TryGetButtonState` (toda consulta de botão) | `AI/MHR_Pad.cs` | **corrigido** |

### 3.4 Adiados de propósito (documentados)

| ID | Achado | Por que fica para depois |
|---|---|---|
| **B5** | ~~`[Advanced] LogLevel = Off` **não cala a camada de IA**~~ | **falso positivo** (1.9.4): a camada de IA está gated — TTS 57/0, `CompanionOrders` 36/0, `CompanionChatAI` 29/0, `WhisperStt` 14/0, `VoiceInput` 13/0 chamadas sem gate. O que fura o `Off` são ~325 chamadas de UI/config (6 de migração do `ModConfig`, o `[UiLayers]` e o `[AppearanceDiag]`) — ver `docs/audit-bughunt-1.9.4.md` §4 |
| **H3** | `Character.IsEncumbered`/`CanMove` ⇒ ~4–5 varreduras de peso por frame por companheiro | com 10 companheiros são ~2 000 somas/frame (microssegundos). Um cache de peso mexe no caminho mais quente do jogo às vésperas do teste |
| **A2** | Liberar a vaga de verdade no Despawn | exige mexer em `CountMine` + `Recover` juntos: um erro leva a 11 vivos. Só o **texto** fica honesto agora |
| **B9** | Bolha no companheiro que executou, voz no "speaker" da mensagem — podem ser diferentes quando o alvo está longe do jogador (pré-existente, não é regressão do merge) | cosmético e pré-existente |
| **#7** | A ordem escolhe o alvo mais próximo e **depois** valida (colmeia vazia, baú cheio, porta trancada) e recusa, em vez de tentar o segundo | comportamento defensável; mexer agora troca um erro por outro |
| **coleta** | O "por que está parado" da coleta (`AtLimit`/`CompanionStatus.WhyRotate`, 4 chaves `hc_why_gather_*` órfãs) não voltou no merge | informação, não função. Coleta continua funcionando |
| **peso** | `Game.m_carryWeightRate` não escala o teto do companheiro | decisão de produto: o nosso teto é `[General] MaxCarryWeight` + cinto. Anotar na wiki |
| **3B** | Medição das 5 ações novas com o `qwen2.5-3b-instruct` (como a M5) — `hive`/`board`/`sit` não têm exemplo no contrato, só a lista | **medido em 31/10/2026**: `qwen2.5-3b-instruct` (LM Studio, temperatura 0,1, 2 rodadas, o **contrato e o schema reais** da DLL): **50/50 (100%)** nas 5 ações × 5 idiomas — e as redes também 50/50 (as frases canônicas o modelo acerta sozinho; as redes ficam para as variantes). Deixa de ser pendência |

## 4. O que foi provado intacto

Registrado porque é o que dá segurança para o teste — cada item foi conferido com `arquivo:linha` por pelo
menos um agente:

- **Identidade**: GUID `Maddhattr.OfflineCompanionsReborn`, `Companions.dll`, `.cfg` em
  `BepInEx\config\AiCompanions.cfg` (com o migrador do `com.profmags.companions.cfg`), chaves `HC_*`, seção
  `[AI]`, **Jotunn não é dependência**.
- **Camada de IA**: merge **puramente aditivo** nos arquivos da IA (só as 5 ordens); `ValidActions` **35** =
  fonte única do `enum` do schema (mesma ordem), `strict` + `additionalProperties:false`; as 3 redes antigas
  (`OrderItems`/`PickupIntent`/`GatherIntent`/`RadialIntent`) **não roubam** as novas (78 casos); `amount` = teto
  contado **na mochila** (`OrderCap`/`OrderMet`/`CapReject`/`OrderDoneNow`); `pickup` liga o flag **e** o
  `MHR_Collect` (que segue rodando **antes** do `MHR_Loot`); `drop`/`equip`/`attack` com alvo por nome;
  endereçamento por nome (nome capitalizado, forma de tratamento não conta); contrato por idioma com todas as
  regras históricas (+7 linhas, **+157…+166 tokens**, ~1.905 no PT — cabe no 3B).
- **Voz**: cache por bytes (32 MB), fila + semáforo + thread dedicada, `Shutdown` com `Join`, `Handle !=
  IntPtr.Zero` antes de `NumSamples`, nativos por caminho absoluto, `reverbZoneMix` escrito só quando muda,
  prewarm com nova tentativa a cada 30 s; STT 16 kHz mono + `NativeGate`; "não falar duas vezes"
  (`withVoice:false`, `PickSpokenText`, `SayAsPlayer` nunca fala); `AutoPickupKeyConflict` **fail-open**;
  `TextInputKeyGuard` (deixa passar `Joy*`/mouse, não toca o `EventSystem`).
- **Companheiro**: `CompanionCarry` (teto + cinto lido **do asset**, dedupe, cache) nos **17 pontos** de peso,
  "estou cheio" = `Max - 2`, `OverweightThreshold` como base; `MaxCompanions` 10 nos 3 caminhos de criação
  (morte **não** abre vaga); dverger **só combate** (roda, chat/voz e tecla); `ChangeTier` preserva
  id/geração/inventário/equip/perfil/ledger/ferramentas/stamina; `SuppressAutoEquip` em `try/finally`.
- **Rede/ZDO**: `MHR_WriteOrQueue`/`MHR_TickPendingWrites` (escrita só com posse, retry no `Update`, aviso em
  5 s) intactos; as 5 ordens chamam `CancelExistingActions` → `TakeControl` **antes** de agir e **não escrevem
  ZDO**; `IsOwned` recusa companheiro de outro jogador; roster por `HC_CompanionId`, `RemoveStaleCopies` com
  portão de 1 s e drop do inventário da cópia velha; `WriteAiProfile`/`ReadAiProfile` no túmulo; nenhuma RPC
  nova fora do `ZRoutedRpc`/`ZNetView`.
- **Harmony**: 102 classes / 93 alvos, **nenhum** alvo inexistente, overload ambíguo ou prefix "que some";
  `PatchAll(type)` **não** recursa em tipos aninhados (nada aplica duas vezes); as colisões
  (`ZInput.GetButtonDown`, os 5 `*.Damage`, `StartAttack`/`RPC_Damage`, `Container.Awake`) são
  **ordem-independentes**; o caso delicado do merge (nosso prefix que suprime o dano + o `HarvestHitRegister`
  novo) roda como queremos — o postfix roda mesmo com o corpo original pulado, e é isso que registra o golpe;
  `UiDiag`/`EcoDiag` **off** por padrão; transpiler do `CompanionWorldModifierPatch` degrada com aviso.
- **UI**: faixa **1701+** com fonte única em `ModUiLayers` (nada de `sortingOrder` literal), dropdown
  compartilhado, `ModUiAudit` opt-in com allow-list, F8 e painel `I` reconstruindo na troca de idioma,
  `_entryActivate`/`_entryClear`/`_choiceAdjust` limpos nos dois pontos de reconstrução, os dois painéis
  recusando abrir juntos, `raycastTarget` só nos controles.
- **Localização**: inglês é o nativo e **não existe** `Translations/English.json` (repo, `.csproj` ou pacote);
  injeção idempotente com `LanguageVersion` (bump só em troca real de idioma); 914 chaves nos 4 idiomas;
  migrações (3.3b, 3.3c, multilinguagem) intactas e idempotentes; `s_moved` idêntica à do upstream (70
  entradas).
- **Empacotamento**: `DeployTranslations`/`DeployWhisperNet`/`DeploySherpaOnnx`/`DeployLicenses` intactos;
  `speech.json` de runtime e `jfk.wav` não são empacotados nem requisito; `CONFIGURATION.md` é gerado do
  `ModConfig` (agora **339 opções / 24 seções**).

## 5. Roteiro de teste em jogo (o que esta auditoria quer ver)

1. **Segurança de dados** (A1): dar 2 zonas de plantio ao Fazendeiro, matar o companheiro, ressuscitar pelo
   túmulo ⇒ as zonas voltam (antes: nenhuma).
2. **Porta** (B1): "fecha a porta" com o companheiro perto de uma porta **e** com o inventário do jogador
   aberto ⇒ fecha a **porta**, nunca a janela; "fecha o inventário" continua fechando a janela.
3. **As 5 ordens** (B2/B3/B4/B7/B8): `esvazia a colmeia`, `embarca no navio`, `senta` (duas vezes, no mesmo
   fogo e em outro), `guarda isso no baú` (com 50 de madeira ⇒ "50 itens"), cada uma apontando o objeto e
   também sem apontar; e uma com "na postura passiva" (deve fazer a ordem, não só trocar a postura).
4. **Recuperação** (A7): derrubar o companheiro, usar a postura pelo radial durante a recuperação ⇒ ele
   **para** de ir ao túmulo e volta a recolher item.
5. **F8** (P1): escolher `LogLevel = Detailed`, `PortalItems = AllowAll` e `RespawnStance = Keep` e conferir
   no `.cfg` que gravou o que foi escolhido (antes: `Off`/`AllowAll`/`Passive`).
6. **Fala** (B6): mandar cortar madeira, minerar pedra e minério, e usar "Repair and Restock" ⇒ falas
   próprias, não o genérico; dois companheiros perto um do outro ⇒ o papo (`Chat`).
7. **Multiplayer** (A3/A4), se houver servidor: `guarda isso no baú` apontando o baú privado de outro jogador
   ⇒ recusa com cerca; depósito em cliente dedicado ⇒ nada de item preso.

## 6. Artefatos

Harnesses e diffs desta auditoria ficaram **fora do repositório**, em `%TEMP%\opencode\`:
`audit-ordens\` (172 casos, IA/ordens), `audit-contrato\` (184 checks, contrato/schema/parser),
`re_full\` + `valheim-decomp\` (decompilado do Valheim 1.0), `HarmonyDecomp\` (Harmony do BepInEx),
`upstream_speech.cs` (Defaults da fala da 1.5.8) e os `lines.json`/`methods.json` do diff do upstream.
