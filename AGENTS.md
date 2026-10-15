# AGENTS.md — ModAI-BR-Companions

Mod de **companheiro NPC com IA** para **Valheim 1.0**, em **PT-BR**.

Este projeto é um **fork** de [Offline Companions Reborn](https://github.com/MaddhattrRocks/Offline-Companions-Reborn)
(**MIT**), que já implementa a visão de "companheiro como membro da equipe" (trabalhos, comida, stamina,
skills, casa, dormir, portais, carroça, menu radial). Nosso trabalho aqui é:

1. **Traduzir completamente para PT-BR** (o mod tem sistema próprio de tradução).
2. **Adicionar a camada de IA** (LLM/voz/fala) por cima — a "Fase 3".

O trabalho é feito pela equipe de agentes em `.opencode/agents/` (comandada por `valheim-mod-lead`) e pelas
skills em `.opencode/skills/`. Leia este arquivo antes de agir.

## Upstream e licença

- Upstream (parent do fork): `MaddhattrRocks/Offline-Companions-Reborn` — **MIT**.
- **Atribuição obrigatória**: ver `THIRD_PARTY_NOTICES.md` e `NOTICE.md`. Mantenha os avisos MIT.
- Ao puxar mudanças do upstream: `git fetch upstream` (adicione o remote se não existir).

## Alvo e versões

- **Valheim 1.0.17** — engine **Unity 6000.0.75f1 (Unity 6)**.
- **BepInExPack_Valheim 5.4.2351** (BepInEx 5.4.23.5).
- **Jotunn 2.30.2**.
- Target: **.NET Framework 4.7.2 (net472)**, C#.
- Versão atual do fork: **1.5.4** (ver `src/Companions/Core/CompanionsPlugin.cs` e `package/manifest.json`).

## Caminhos desta máquina

- Repositório: `C:\Users\revenda33\Desktop\ModAI-BR-Companions`
- Jogo: `C:\Program Files (x86)\Steam\steamapps\common\Valheim`
- Plugins: `...\BepInEx\plugins\ModAI-BR-Companions`
- Traduções (runtime): `...\BepInEx\plugins\ModAI-BR-Companions\Translations\*.json` (UI) e
  `...\Speech\*.json` (falas do companheiro). As fontes ficam em `Translations/` e `Speech/` na raiz do
  repositório e o build as copia (`Companions.csproj` → alvo `DeployTranslations`).
- Idioma do jogo em PT-BR: **`Portuguese_Brazilian`** (os arquivos são `Translations/Portuguese_Brazilian.json`
  e `Speech/Portuguese_Brazilian.json`).

## Build

- `Environment.props` na raiz define `ValheimDir` e `DeployDir` (é **gitignored**; há o
  `Environment.props.example`).
- **Build:** `dotnet build Companions.sln -c Release` → gera `src/Companions/bin/Release/net472/Companions.dll`
  e copia para `DeployDir`.
- **Teste:** iniciar o jogo e conferir `BepInEx\LogOutput.log` (deve listar
  `Loading [Offline Companions Reborn <versão>]`).

## Arquitetura do fork (`src/Companions/`)

| Pasta | Conteúdo |
|---|---|
| `Core/` | Entry point, config, **localização**, speech config, helpers (reflexão, texturas, input) |
| `Companion/` | Setup, spawn/prefabs, aparência, tiers, skills, stamina, comida, descanso, **talk**, morte/respawn |
| `AI/` | Movimento e combate: `CompanionAI`, `CompanionCombatAI`, arma/escudo, tocha, água, portas, `MHR_Mover`; **camada de IA**: `LlmClient`, `LlmIntent`, `CompanionOrders`, `CompanionChatAI` |
| `Jobs/` | Trabalhos: harvest, farm, fish, hunt, cook, smelt, repair, upkeep, loot, tool keeper |
| `UI/` | Painéis, **menu radial**, HUD, config, zone placers, fixes de fonte/inventário |
| `Patches/` | Patches Harmony nas classes vanilla |
| `Diagnostics/` | Logs de diagnóstico (`[AIGate]`, `[EQUIPDIAG]`, `[UiDiag]`, `[Diag]`) |

Notas de desenvolvimento do upstream: `docs/DEV_NOTES.md`; handoff: `docs/SESSION_HANDOFF.md`.

## Camada de IA (Fase 3.1 — ordens por texto no chat; Fase 3.2/3.2c — o número da ordem é um alvo total contado no inventário do companheiro; Fase 3.5 — personalidade, memória e conversa livre; Fase 3.6 — ordens com alvo: atacar/equipar/largar; Fase 3.6b — a ordem sem alvo do modelo cai para as palavras do jogador; Fase 3.3 — ordens por voz: push-to-talk + STT local; Fase 3.3c — raio de coleta maior e chatter aleatório desligado; Fase 3.3d — o teto da ordem vale para todo alvo do modo e a voz sai no balão do jogador)

O jogador digita no chat do jogo (`colete 20 de madeira`, `segue`, `para`, `ataca o greydwarf`,
`equipa o machado de bronze`, `larga a pedra`, `bom dia`) e um **LLM local** (API compatível com a OpenAI:
`POST {BaseUrl}/chat/completions`) devolve a intenção em JSON, que vira a **mesma ação** do menu radial /
tecla de comando — ou uma **resposta de conversa**, quando não é ordem. Fluxo: `CompanionChatAI` (patch Harmony
em `Chat.SendText` + prompt PT-BR montado por mensagem) → `LlmClient` (POST em `Task` de fundo + fila
thread-safe drenada no `Update`) → `LlmIntent` (parser tolerante: `action`, `reply`, `target`, `resource`,
`amount`) → `CompanionOrders` (intenção → `CompanionSetup.ApplyActionMode`, `SetCombatStance`, `MHR_JobRules`,
`DirectEngage`, `EquipItem`, `ItemDrop.DropItem`, recall...).

- Config `[IA]` no `.cfg` (sem segredos no código): `Ligado`, `BaseUrl`, `Modelo`, `ApiKey`, `Temperatura`,
  `TimeoutSegundos`, `Prefixo`, `MostrarRespostaNoChat`, `CooldownSegundos`, `RaioDeAlvo` (até onde procurar a
  criatura citada numa ordem de ataque; a que estiver na mira vale sempre), `Personalidade` (texto livre do
  jogador, PT-BR, até 600 caracteres), `PresetPersonalidade` (`viking`, `mago`, `sarcastico`, `guia`, `custom`;
  vazio = padrão), `Memoria` (4 — era 6 até a 3.3b; 0 = desligada, teto 12) e `Conversa` (true).
- `Chat.SendText(Talker.Type, string)` é o caminho da mensagem **do próprio jogador** (as recebidas entram
  por `Chat.OnNewChatMessage`) — é o único ponto capturado.
- `despawn` não é exposto ao modelo. O `amount` é um **ALVO TOTAL** para madeira/pedra/minério (Fase 3.2,
  corrigido na 3.2c): vira um alvo no ZDO do companheiro (`HC_OrderCap`/`HC_OrderCapMode`/`HC_OrderCapItem`) e
  é contado **apenas no inventário do companheiro** (a mochila — **não** conta baús nem pilhas), em
  `MHR_GatherRules.Bag` (usado por `AllowTarget`/`AllowLoose` através de `CompanionSetup.OrderCap`/`OrderSeen`).
  Ou seja, `colete 10` = **ter 10 na mochila**: com 15 ele nem começa e diz "já temos o suficiente"; com 7 ele
  coleta até 10 ou mais (a última árvore pode passar). O alvo vale **só para o recurso pedido**
  (`OrderItems.Resolve` traduz "madeira"/"wood" → `Wood`); se o recurso não for reconhecido, vale para o modo
  inteiro (comportamento de reserva, como na 3.2). Ao bater o alvo ele encerra a ordem e volta a seguir. Para os
  outros jobs o número segue sendo só informativo.
- **Fase 3.6 — ordens com alvo:** `attack`/`equip`/`drop` usam o campo `target` do JSON e o
  `OrderTargets` (novo) para transformar **nome → criatura** (mira do jogador primeiro, depois a mais próxima
  dentro de `[IA] RaioDeAlvo`) e **nome → item do inventário do companheiro** (prefab, nome localizado, PT-BR/EN,
  singular/plural). `attack` passa por `CompanionAI.DirectEngage(..., byOrder: true)` (a criatura nomeada é
  engajada **mesmo na postura Passiva** — a postura não muda); `equip` por `Humanoid.EquipItem`; `drop` por
  `ItemDrop.DropItem` + `Inventory.RemoveItem` (sem número = stack inteiro; a pilha sai do auto-pickup e item de
  missão nunca cai). "Pegar" é lido pelo objeto: `pega a picareta` → equipar, `pega madeira` → coleta.
- **Fase 3.6b — a ordem sem alvo (o modelo responde `"target": null`):** o relato "o comando pra dropar não
  funcionou" era isto — o modelo acertava `action` e `amount` e devolvia `target` nulo, e a 3.6 só sabia largar/
  equipar o que o **campo do modelo** nomeasse (0 companheiros, "Não entendi qual item."). Agora `Run(intent,
  userText)` leva o **texto do jogador** (chat **ou** voz) junto da ordem: quando o modelo não nomeia o alvo (ou
  nomeia algo que o companheiro não carrega), `OrderTargets.Phrases(mensagem)` extrai as frases de nome do que o
  jogador disse e cada uma é **validada** contra as mochilas (`FindItem`) ou as criaturas no raio
  (`FindCreatureByWords` → `ClosestByName`) — nunca se inventa item. `Phrases` descarta os verbos da ordem (`joga`,
  `larga`, `equipa`, `pega`, `ataca`), pronomes/demonstrativos (`essas`, `aquele`, `que tu pegou`), enchimentos
  (`coisa`, `lixo`), números e dígitos (`10` = `amount`) e lugares (`chão`, `fora`, `mão`, `perto`, `pra`/`pro`/`ao`),
  e monta nomes de até 3 palavras, **do mais longo para o mais curto**; a barra do fallback é `MinPhraseScore = 900`
  (contra os 450 do resto) para uma frase composta não casar por **substring** e largar o item errado. O `target` do
  modelo continua ganhando quando casa (critério da 3.6) e, sem nada encontrado, o aviso antigo continua. Em
  `attack` a mensagem é lida **antes** da mira e só quando o modelo não nomeou nada (`ataca aquele` segue na mira).
  O prompt (`CompanionChatAI.Contract`) passou a exigir `target` em `attack`/`equip`/`drop` (nunca `null`), com 2
  exemplos de `drop`. Detalhes: `docs/roadmap.md` (Fase 3.6b).
- **Fase 3.5 — personalidade, memória e conversa:** `CompanionPersona` (presets PT-BR embutidos + resolução do
  texto livre, que **ganha** do preset) e `CompanionMemory` (últimas `Memoria` trocas **por companheiro**, teto de
  `2 × Memoria` mensagens, 240 caracteres cada, no máximo 8 conversas; limpa na morte, no dismiss e na troca de
  mundo) entram no **prompt montado por mensagem**: nome + personalidade + contrato fixo + memória. A memória vai
  como **texto no system prompt** (`Jogador: ...` / `Thorin: ...`), nunca como mensagens `user`/`assistant`
  extras — o contrato JSON continua sendo a última coisa que o modelo lê. Quando o `action` é `none` e há
  `reply`, o companheiro **fala** (balão via `CompanionTalk.Say(..., force: true)`) e o texto aparece no chat;
  numa **ordem** a linha localizada do mod **ganha** e o `reply` é só logado (nada de mensagem duplicada). Quem
  fala é o companheiro **mais próximo** que pode receber ordem, e a conversa **não** toma posse dele
  (`MHR_Owner.TakeControl` continua só nas ordens).
- **Fase 3.3 — ordens por voz (push-to-talk + STT local):** segurar `[IA] VozTecla` (**V**) e falar; ao soltar, o
  áudio é transcrito **dentro do jogo** (`WhisperStt`, Whisper.net in-process, sem HTTP) e o texto entra no
  **mesmo** `CompanionChatAI.Submit(mensagem, origem, announceRefusal)` que o chat usa — ordem, conversa,
  personalidade e memória são idênticas às do texto digitado (nada de lógica duplicada; `[IA] Prefixo` é só do chat).
  `VoiceInput` cuida da captura (`Microphone.Start` **16 kHz mono**, `loop: false`), do trim de silêncio e do
  **WAV PCM 16-bit mono em memória**; `WhisperStt` carrega o modelo **uma vez** e o mantém carregado (pre-warm em
  segundo plano), transcreve numa `Task` de fundo com fila thread-safe drenada no `Update` e serializa o uso do
  nativo (`NativeGate`, compartilhado com o spike `modia_stt_teste` — o modelo **não** é carregado duas vezes).
  Config `[IA]`: `VozLigada` (true), `VozTecla` (V), `VozIdioma` (pt), `VozDuracaoMaxima` (10 s),
  `VozLimiarSilencio` (0.01 — era 0.02 até a 3.3b), `VozPromptInicial` (vocabulário das ordens, novo na 3.3b),
  `VozDispositivo` (0) e `VozMostrarTexto` (true). O microfone é acessado por
  **reflexão** (`VoiceInput.Mic`, 8 membros, uma vez só) porque `UnityEngine.AudioModule` não pode ser referenciado
  neste projeto: Whisper.net fixa a fachada netstandard 2.0 e o módulo do Unity 6 é netstandard 2.1 (CS1705); a
  fachada 2.1 do jogo resolve o CS1705 mas quebra `AudioClip.GetData` (CS7069, `Span<T>` no mscorlib).
- **Fase 3.3b — correções do teste da voz:** (1) **a tecla V é também o *Toggle Auto-Pickup* do Valheim**
  (`ZInput.Reset` → `AddButton("AutoPickup", KeyToPath(Key.V))`; **único** leitor: `Player.Update` →
  `ZInput.GetButtonDown("AutoPickup")`), então `Patches/AutoPickupKeyConflict.cs` põe um **prefixo Harmony em
  `ZInput.GetButtonDown(string)`** que responde `false` só para `"AutoPickup"` e só quando `[IA] VozLigada` está
  ligado **e** o binding efetivo do jogo aponta para `[IA] VozTecla` (lido por
  `GetButtonDef("AutoPickup").GetActionPath()`); reatribuir o auto-pickup no jogo, `VozLigada = false`, tecla
  desconhecida ou exceção ⇒ **não** bloqueia (o jogo fica intacto) e o jogador é avisado uma vez (log + chat/HUD).
  (2) **STT:** modelo default `whisper\ggml-small-q5_1.bin` (181 MB; o `base` fica em disco), prompt inicial
  (`WithPrompt`, sobrevive ao `WithNoContext`) e trim com limiar `0.01` + folga de `0,2 s`.
  (3) **Latência do LLM medida:** `[IA] Prompt:` (car./tokens) e `[IA] Tempo:` (ms, tokens/s, aviso a partir de 5 s),
  e `[IA] Memoria` default **4**. Detalhes: `docs/roadmap.md` (Fase 3.3b) e `docs/re/stt-voz-pipeline.md`.
- **Fase 3.3c — raio de coleta maior + chatter desligado:** (1) os cinco raios da coleta subiram de default
  (`[Advanced] GatheringSearchRadius` 30→**50**, `[Gathering] FollowRange` 10→**25**, `ForageFollowRange` 10→**20**,
  `Range` 40→**60**, `[Advanced] GatheringDropRadius` 8→**12**). Seguindo você, quem limita é o `FollowRange`
  (cada candidato passa por `InWorkArea` → `PlayerRange()`, medido **de você**); o `GatheringSearchRadius` só
  precisa cobrir o pior caso (companheiro a 25 m + alvo a 25 m do outro lado ≈ 50 m). A **coleira** continua
  respeitada: com alvo em andamento o `HarvestController` abandona o alvo acima de `FollowRange + 15 m` de você
  (40 m com o valor novo) e o `MHR_Follow` (`[Follow] MaxDistance` 10 m) só volta a mandar quando o job fica
  **ocioso** (`_harvest.IsActive`). (2) **`[Speech] Chatter`** (bool, default **false**): o chatter periódico vinha
  **só** do timer do `CompanionTalk.Update()` (`_talkTimer` → `SayContextual()`); com `false` o timer não é nem
  decrementado. **Continuam** falando as falas forçadas (`Say(..., force: true)` — ordens, "já temos o suficiente",
  conversa da IA, "Entendi: ..."), a linha de **mudança de modo** (`SayRandom(ActionLines)`, roda antes da guarda),
  as de evento (`hc_speech_*`) e as de ociosidade (`[Idle] SpeechInterval`, própria). (3) **Migração:** os cinco
  raios são atualizados **só** se estiverem exatamente no default antigo (`MovedDefault`, log explicando), depois do
  `MigrateAndClean` (cobre nomes pré-1.4.149); a chave nova é criada pelo BepInEx com `false`. Detalhes:
  `docs/roadmap.md` (Fase 3.3c).
- **Fase 3.3d — a ordem de pedra para no alvo + o texto da voz no balão do jogador:** (1) **causa raiz confirmada nos
  assets do jogo:** `Rock_3` é um `Destructible` com `health = 1` e **nenhuma tabela de drop** — a pedra vem do
  `Rock_3_frac` que ele **spawna ao quebrar** (`m_spawnWhenDestroyed`), um `MineRock5` com `Stone` 3–6 por área (5
  áreas ≈ 15–30 por rocha); como o `MHR_GatherRules.DropsOf` só olhava os componentes do próprio alvo, `Rock_3`
  parecia "não dropa nada" e o `AllowTarget` caía no `drops.Count == 0 → "unknown drops: don't block it"` — **o teto
  da ordem (e o `StoneLimit`) não valia para ele** e o companheiro minerava para sempre (log: 10+ pedras com a ordem
  de 2, Pickaxes 6→8, sem linha de "alvo atingido" para pedra; a de **madeira** encerrava normal). **Correção:**
  (a) `DropsOf` **segue o que o alvo spawna** (`Destructible.m_spawnWhenDestroyed`/`m_spawnWhenDamaged`, profundidade
  ≤ 3, cache por alvo que já existia) — `AddDrops`; (b) novo `MHR_GatherRules.OrderMet`: com **ordem ativa para o
  modo** + **item pedido conhecido** + **`OrderSeen(Bag(item)) >= cap`** (contagem da 3.2c, no inventário do
  companheiro), **TODOS** os alvos daquele modo são recusados (não só os que produzem o item) em `AllowTarget` **e**
  `AllowLoose`, cada recusa contando `CapReject` → o caminho `OrderDone`/`CompleteOrderCap` que já existia encerra a
  ordem **uma vez** e volta a seguir; resposta cacheada por frame/modo (`Per.OrderFull*`); (c) novo
  `OrderDoneNow` chamado pelo `HarvestController.Update` enquanto o companheiro está `Moving`/`Attacking` (nunca em
  `CollectingDrops`, para não deixar o drop no chão) — antes o número só era conferido no *scan* entre alvos.
  **Não muda:** sem ordem tudo é como antes, alvo do **radial** (`IsDirectedHarvest`) ignora o teto, limites da
  config intactos. (2) **Balão da voz:** `HeardByVoice` não manda mais o companheiro repetir a transcrição; ela vai
  para o balão do **jogador** via `CompanionTalk.SayAsPlayer` (`Chat.instance.SetNpcText(Player.m_localPlayer.gameObject,
  Vector3.up * 2f, SpeechCullDistance, SpeechDuration, "", texto, false)`, mesmo offset/cull/duração, sem cooldown,
  null-safe) — o chat/HUD continua com "Entendi: …" e as respostas do companheiro (conversa, "Deixa comigo!") seguem no
  balão **dele**. Detalhes: `docs/roadmap.md` (Fase 3.3d) e `docs/re/harvesting-notes.md` (§2.1).
- **Fase 3.8 — o companheiro abre o próprio inventário por voz/chat:** `abre o teu inventário` / `mostra o que você
  carrega` (e `fecha o inventário`, de bônus) viram as ações `inventory`/`inventory_close` do contrato
  (`CompanionChatAI.Contract`: lista de ações + regra + 2 exemplos) e o parser tolera as muitas formas do modelo
  (`LlmIntent`: aliases exatos **e** qualquer ação que carregue o nome da mochila — `invent*`, `mochila`, `bolsa`,
  `bag`; `carregar_madeira` **não** é esta ordem). `CompanionOrders.Inventory` escolhe **um** companheiro (o
  `CompanionChatAI.PendingSpeaker`, isto é, o `PickSpeaker()` de quem o jogador falou; se ele sumiu, o mais próximo
  que pode receber ordem) e chama **`CompanionInteract.OpenCompanionInventory`** (o **mesmo** caminho do toque na
  tecla de interagir: `InventoryGui.instance.Show(container)` + `CompanionInteractPanel.Instance?.Show(setup)`) via
  `CompanionInteract.RequestCompanionInventory`. O método agora **devolve** `InventoryOpenResult`
  (`Opened`/`Requesting`/`NoGui`/`NoContainer`; os chamadores do toque ignoram o retorno) e, quando o container não é
  nosso, a ordem **pede a posse uma vez** e o novo `CompanionInteract.TickPendingInventoryOpen` (no `Update`, **uma
  vez por frame**, `ClaimOwnership` **uma única vez**, timeout de **5 s**) abre a janela sozinha quando a posse chega —
  desistindo com aviso, ou em silêncio se o companheiro sumiu / o jogador abriu outra janela. Rede de segurança igual à
  da 3.6b: se o modelo responder `none`, `CompanionOrders.IsInventoryRequest` lê as **palavras do jogador** (palavra da
  mochila **e** verbo de abrir/fechar — `o que tem no teu inventário?` continua conversa). PT-BR:
  `hc_ai_inventory_open` (*"Abrindo o inventário."*, também falado), `hc_ai_inventory_requesting`,
  `hc_ai_inventory_timeout`, `hc_ai_inventory_no_gui`, `hc_ai_inventory_closed`, `hc_ai_inventory_none_open`,
  `hc_ai_no_container`, `hc_ai_order_inventory(_close)`. Verificado num harness fora do jogo (45 casos, com o
  `LlmIntent.cs` e o `IsInventoryRequest` **reais**): **ALL CHECKS PASSED**. Detalhes: `docs/roadmap.md` (Fase 3.8).
- Backend/contrato de referência: `docs/re/backend-contract.md`; mapa da fase: `docs/roadmap.md` (Trilha D);
  pipeline de voz: `docs/re/stt-voz-pipeline.md`.

## Nossas docs

- `docs/vision.md` — a visão/escopo (companheiro como membro da equipe) e o que falta.
- `docs/roadmap.md` — fases e status.
- `docs/plan-fases.md` — plano das fases com mapa do upstream.
- `docs/audit-vs-offline-companions.md` — auditoria do nosso mod anterior vs o upstream.
- `docs/re/` — engenharia reversa do Valheim 1.0 (APIs, diff de versão).
- `docs/re/stt-whisper-net-spike.md` — o spike do STT local (Whisper.net in-process) e o loader nativo.
- `docs/re/stt-voz-pipeline.md` — o pipeline de voz da Fase 3.3 (push-to-talk → STT → ordens) e a receita de teste.
- `docs/references.md` — fontes de terceiros e licenças.

## Skills (carregue antes de escrever/revisar código)

- `bepinex` · `jotunn` · `thunderstore-packaging` · `valheim-reverse-engineering` · `valheim-ai-backend` · `valheim-assets`

## Regras de trabalho

1. **Carregue a skill** da dependência envolvida antes de codar ou revisar.
2. **Não quebre o comportamento do upstream** — nossas mudanças são: **tradução PT-BR** + **camada de IA**.
   Mudanças de lógica só com motivo claro e documentado.
3. **Tradução**: use o sistema de localização do próprio mod (`Translations/*.json`, `ModLocalization.cs`) —
   UI/hover/HUD — e `Translations/speech/<Idioma>.json` para as falas (`SpeechConfig`).
   Não hardcode PT-BR no código quando houver token de localização.
4. **Sem segredos** hardcoded (chaves de LLM/API via `ConfigEntry` ou variável de ambiente).
5. **Sempre commitar e dar push** ao terminar (`git add -A && git commit && git push`), mensagem no padrão
   `tipo(escopo): resumo`.
6. Atualize a documentação correspondente (`README.md`, `package/CHANGELOG.md`, `docs/roadmap.md`,
   `AGENTS.md`) quando algo mudar.

## MCP servers disponíveis

- **`alvin`** — MCP do **Visual Studio** (build, debug, Output, terminal, testes). Requer o VS aberto.
- **`context7`** — documentação atualizada de bibliotecas.
- **`learn`** — Microsoft Learn (.NET/C#/Azure).
- **`github`** — repositório/PRs/issues (requer auth via `/mcps`).

## Definição de pronto

- Compila em Release sem erros.
- Carrega no Valheim 1.0 sem exceção no `LogOutput.log`.
- Textos novos em **PT-BR** via localização.
- Sem regressão perceptível de performance.
