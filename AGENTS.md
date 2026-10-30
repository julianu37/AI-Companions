# AGENTS.md — AI Companions

Mod de **companheiro NPC com IA** para **Valheim 1.0**, em **PT-BR**.

Este projeto é um **projeto independente** baseado em [Offline Companions Reborn](https://github.com/MaddhattrRocks/Offline-Companions-Reborn)
(**MIT**), que já implementa a visão de "companheiro como membro da equipe" (trabalhos, comida, stamina,
skills, casa, dormir, portais, carroça, menu radial). Nosso trabalho aqui é:

1. **Traduzir completamente para PT-BR** (o mod tem sistema próprio de tradução).
2. **Adicionar a camada de IA** (LLM/voz/fala) por cima — a "Fase 3".

O trabalho é feito pela equipe de agentes em `.opencode/agents/` (comandada por `valheim-mod-lead`) e pelas
skills em `.opencode/skills/`. Leia este arquivo antes de agir.

## Nome e repositório (renomeação de 23/10/2026)

O mod se chama **AI Companions** (até 23/10/2026 era **ModAI-BR-Companions**): repositório
`https://github.com/julianu37/AI-Companions` (a wiki é `AI-Companions.wiki`), pasta de deploy
`BepInEx\plugins\AI-Companions\` e pacotes `AI_Companions-<versão>.zip`. **Preservados de propósito** (o jogo, os
saves e a rede dependem deles): o **GUID** `Maddhattr.OfflineCompanionsReborn`, o `Companions.dll`, o
`Companions.csproj`, as chaves `HC_*` e os créditos/avisos do upstream (**MIT**). Desde a **1.8.3** o arquivo de
config é **`BepInEx\config\AiCompanions.cfg`** (o GUID não dá mais nome a ele; ver a fase *4.0 — multilíngue*).

## Upstream e licença

- Upstream (projeto de origem): `MaddhattrRocks/Offline-Companions-Reborn` — **MIT**.
- **Atribuição obrigatória**: ver `THIRD_PARTY_NOTICES.md` e `NOTICE.md`. Mantenha os avisos MIT.
- Ao puxar mudanças do upstream: `git fetch upstream` (adicione o remote se não existir).
- **⚠️ O alvo do sync é o `main` do upstream — NUNCA a `claude/relaxed-archimedes-ecoh6h`.** Essa branch é o
  **WIP do próprio autor** (commits tipo *"Dev notes: backlog - multi-companion management panel"*, atualizada
  várias vezes por dia) e a **numeração de versão dela não é release**: ela vai até `1.5.42` enquanto o `main`
  publicado está na **`1.5.8`** (era `1.5.5` em 17/10). Ler os números de versão dela como "releases que estamos perdendo" já causou um
  erro de avaliação uma vez (eu reportei "38 versões atrás" quando o real era **1 release**). **Não mergeie, não
  rebaseie e não use como referência de comportamento** — só o `main` conta.
- **Estado da divergência (verificado em 2026-10-30):** a nossa base é a **`1.5.4`** (`267b8fe`, o commit do
  upstream - a nossa história **compartilha** a dele, então um `git merge` é o caminho) e o `main` está na
  **`1.5.8`** (`8aa6f53`, 28/10). Ou seja: **4 releases atrás** (`1.5.5` → `1.5.6` → `1.5.7` → `1.5.8`) —
  **9 commits, 121 arquivos, +14.907 / −2.600**, **25 arquivos novos** e nenhum removido. O que vem: 1.5.5 (reforma da
  fala + status line, revive por equipamento, teclas de postura, farm em terra arada); 1.5.6 (combate como um jogador,
  arcos, navios, invocação no Santuário de Odin, colmeias + **CHANGELOG curto + `CHANGELOG-full.md`**); 1.5.7 (janela de
  invocação, Stone/Ore e construções, Wood, peso, portais, fogueiras, baús); 1.5.8 (tochas, binds de controle, F8 no
  controle, lava, nível de log, levantar fora de vista só em masmorra, comando de porta fecha portas). Os pontos
  delicados do merge são **58 arquivos que os dois lados mexeram** — em especial `CompanionTalk.cs` (o nosso TTS),
  `CompanionAI.cs`, `CompanionCombatAI.cs`, `ModConfig.cs`/`ModLocalization.cs`/`SpeechConfig.cs` (as nossas chaves),
  `CompanionsPlugin.cs`, os painéis de UI e o `package/CHANGELOG.md`; e **`UI/ModUiLayers.cs` é add/add** (nós criamos
  para a faixa 1701+, eles criaram no 1.5.8). Regra do merge: **upstream manda em comportamento/jogo; nós mandamos na
  camada de IA, localização, chaves de config e camadas de UI**.
- O upstream **não publica tags nem releases** no GitHub (a versão vive na mensagem do commit do `main`).
  Para conferir o estado real: `gh api repos/MaddhattrRocks/Offline-Companions-Reborn/commits/main` (ou a branch
  `main` do clone local em `~/.local/share/opencode/repos/github.com/MaddhattrRocks/Offline-Companions-Reborn`).

## Alvo e versões

- **Valheim 1.0.x** (testado na **1.0.20**) — engine **Unity 6000.0.75f1 (Unity 6)**.
- **BepInExPack_Valheim 5.4.2351** (BepInEx 5.4.23.5).
- **Jotunn: NÃO é usado por este mod.** O código não referencia nenhuma API do Jotunn (0 ocorrências de uso — só
  comentários), o plugin **não** declara `[BepInDependency]` dele e o `.csproj` não o referencia; o upstream também não
  o usa. A única relação é **defensiva**: a pasta `Speech/` fica fora de `Translations/` porque o carregador de
  localização do Jotunn (se o jogador o tiver instalado) trataria `Translations\<pasta>\*.json` como um idioma. Não
  listar o Jotunn como requisito em release/README. (A skill `jotunn` da equipe continua no repositório como
  referência de framework, mas **não** se aplica a este mod.)
- Target: **.NET Framework 4.7.2 (net472)**, C#.
- Versão: **1.9.1** — **multilíngue (5 idiomas: PT-BR, EN, ES, FR, IT)** em 26/10/2026 (fonte única: `PluginVersion` em
  `src/Companions/Core/CompanionsPlugin.cs`). A **1.9.1** só tira o `ggml-base-q5_1.bin` (57 MB, nunca usado) do
  pacote: o zip completo caiu de **~368 MB para ~314 MB** (o "sem modelos" segue ~28,5 MB) e o texto do
  `[AI] SttModel` passou a dizer que o `base` é **download opcional**. As releases saíram em sequência em 23–26/10
  (`1.7.0` → `1.8.0` → `1.8.1` → `1.8.2` → `1.8.3` → `1.9.0` → `1.9.1`); a **1.7.1 foi pulada de propósito**
  (pedido do jogador). As branches `feat/multi-companheiro`, `fix/peso-cinto`, `feat/llm-groq`, `feat/provedor-llm`
  e `languages` foram mergeadas (`--no-ff`) e podem ser apagadas — a `main` é a linha de release.

## Caminhos desta máquina

- Repositório: `C:\Users\revenda33\Desktop\AI-Companions`
- Jogo: `C:\Program Files (x86)\Steam\steamapps\common\Valheim`
- Plugins: `...\BepInEx\plugins\AI-Companions`
- Traduções (runtime): `...\BepInEx\plugins\AI-Companions\Translations\*.json` (UI) e
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
  `Loading [AI Companions <versão>]` + `loaded successfully! (N methods patched)`).
- **Pasta de deploy:** o build copia para `DeployDir` = `...\BepInEx\plugins\AI-Companions\`. **Não** deixe
  outra cópia do mod instalada ao mesmo tempo (ex.: o Vortex deploya em `plugins\Companions\`, usando o **nome do
  mod** como pasta): duas cópias = o mesmo GUID carregado duas vezes. Se um gerenciador cuidar da pasta dele,
  aponte o `DeployDir` para ela **ou** remova a outra.
- **Console do BepInEx:** vem **desligado** no `BepInEx.cfg` novo. Para abrir a janela do console, use
  `[Logging.Console] Enabled = true` em `...\BepInEx\config\BepInEx.cfg` (lido no próximo boot). O
  `LogOutput.log` (disco) continua sempre ligado.
- **BepInEx manual (sem gerenciador):** BepInEx **5.4.23.5** (o pack do Nexus é o `5.4.2351`), extraído na raiz do
  jogo: `winhttp.dll` + `doorstop_config.ini` (`target_assembly=BepInEx\core\BepInEx.Preloader.dll`) +
  `.doorstop_version` + `BepInEx\core\`. O `.cfg` do mod e a pasta `plugins\` **sobrevivem** a reinstalar o
  BepInEx.

## Arquitetura do projeto (`src/Companions/`)

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

## Camada de IA (Fase 3.1 — ordens por texto no chat; Fase 3.2/3.2c — o número da ordem é um alvo total contado no inventário do companheiro; Fase 3.5 — personalidade, memória e conversa livre; Fase 3.6 — ordens com alvo: atacar/equipar/largar; Fase 3.6b — a ordem sem alvo do modelo cai para as palavras do jogador; Fase 3.3 — ordens por voz: push-to-talk + STT local; Fase 3.3c — raio de coleta maior e chatter aleatório desligado; Fase 3.3d — o teto da ordem vale para todo alvo do modo e a voz sai no balão do jogador; Fase 3.4 — o companheiro FALA: TTS local (Kokoro-82M via sherpa-onnx); Fase 3.7 — painel da IA no jogo: abas por companheiro, voz com Ouvir e push-to-talk; Fases 3.9–3.17 — invocador/abas, nome no comando, perfil de IA por companheiro no ZDO, nome/tipo editáveis, a voz acompanhando o balão; Fase 3.19 — a rodada de correções da revisão completa e o teto de 10 companheiros)

O jogador digita no chat do jogo (`colete 20 de madeira`, `segue`, `para`, `ataca o greydwarf`,
`equipa o machado de bronze`, `larga a pedra`, `bom dia`) e um **LLM** (API compatível com a OpenAI:
`POST {BaseUrl}/chat/completions`) devolve a intenção em JSON, que vira a **mesma ação** do menu radial /
tecla de comando — ou uma **resposta de conversa**, quando não é ordem. Fluxo: `CompanionChatAI` (patch Harmony
em `Chat.SendText` + prompt montado por mensagem, **no idioma do jogo** desde a M3) → `LlmClient` (POST em `Task` de fundo + fila
thread-safe drenada no `Update`) → `LlmIntent` (parser tolerante: `action`, `reply`, `target`, `resource`,
`amount`) → `CompanionOrders` (intenção → `CompanionSetup.ApplyActionMode`, `SetCombatStance`, `MHR_JobRules`,
`DirectEngage`, `EquipItem`, `ItemDrop.DropItem`, recall...).

- **Endpoint (desde a 1.8.1): `[AI] BaseUrl` = `https://api.groq.com/openai/v1` e `[AI] Model` = `openai/gpt-oss-20b`**
  (**Groq é o padrão**; a `ApiKey` é **obrigatória** — a chave gratuita sai em `console.groq.com/keys`). **Ordem de
  preferência: Groq → LM Studio/Ollama (local) → OpenRouter.** O **LM Studio/Ollama continua suportado** como opção
  local/offline (`http://127.0.0.1:1234/v1` + `qwen2.5-3b-instruct`; `127.0.0.1` e **não** `localhost`, que resolve IPv6
  primeiro e custa ~2,5 s por ordem) e o **OpenRouter** (`https://openrouter.ai/api/v1` + `openrouter/free`) **continua
  funcionando e documentado**, mas foi rebaixado para último porque o tier gratuito é lento/imprevisível. Limites do
  tier gratuito do Groq para o `gpt-oss-20b` (fonte: `console.groq.com/docs/rate-limits`): **30 RPM, 1.000 RPD, 8.000 TPM,
  200.000 TPD** — com ~1.800 tokens por ordem o TPM dá **~4 ordens/min** e o excedente volta **429**. Structured outputs:
  o `gpt-oss-20b` suporta `json_schema` **strict** (o modo que usamos); streaming/tool use não, e o mod não usa nenhum.
  Privacidade: com Groq/OpenRouter o texto da ordem **sai da máquina**; com LM Studio/Ollama, não. A **voz** é local nos
  três casos. (A 1.7.0 tinha aberto o OpenRouter como padrão; a 1.8.1 trocou.)
- Config `[AI]` no `.cfg` (sem segredos no código): `Enabled`, `BaseUrl`, `Model`, `ApiKey`, `Temperature`,
  `TimeoutSeconds`, `Prefix`, `ShowReplyInChat`, `CooldownSeconds`, `TargetRange` (até onde procurar a
  criatura citada numa ordem de ataque; a que estiver na mira vale sempre), `Personality` (texto livre do
  jogador, PT-BR, até 600 caracteres), `PersonalityPreset` (`viking`, `mago`, `sarcastico`, `guia`, `custom`;
  vazio = padrão), `Memory` (4 — era 6 até a 3.3b; 0 = desligada, teto 12) e `Conversation` (true). Desde a 1.7.0 boa
  parte disso é editável **no jogo** (painel `I` → aba IA → *Configurações do LLM*, com **Testar conexão**;
  `LlmConnectionTest`): a chave é mostrada **mascarada** e o que não cabe lá continua no painel **F8**. (Nomes em
  inglês desde a 1.8.3 — antes eram `Ligado`, `Modelo`, `Temperatura`….)
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
  dentro de `[AI] TargetRange`) e **nome → item do inventário do companheiro** (prefab, nome localizado, PT-BR/EN,
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
- **Fase 3.3 — ordens por voz (push-to-talk + STT local):** segurar `[AI] VoiceKey` (**V**) e falar; ao soltar, o
  áudio é transcrito **dentro do jogo** (`WhisperStt`, Whisper.net in-process, sem HTTP) e o texto entra no
  **mesmo** `CompanionChatAI.Submit(mensagem, origem, announceRefusal)` que o chat usa — ordem, conversa,
  personalidade e memória são idênticas às do texto digitado (nada de lógica duplicada; `[AI] Prefix` é só do chat).
  `VoiceInput` cuida da captura (`Microphone.Start` **16 kHz mono**, `loop: false`), do trim de silêncio e do
  **WAV PCM 16-bit mono em memória**; `WhisperStt` carrega o modelo **uma vez** e o mantém carregado (pre-warm em
  segundo plano), transcreve numa `Task` de fundo com fila thread-safe drenada no `Update` e serializa o uso do
  nativo (`NativeGate`, compartilhado com o spike `modia_stt_teste` — o modelo **não** é carregado duas vezes).
  Config `[AI]`: `VoiceEnabled` (true), `VoiceKey` (V), `VoiceLanguage` (pt), `VoiceMaxSeconds` (10 s),
  `VozLimiarSilencio` (0.01 — era 0.02 até a 3.3b), `VozPromptInicial` (vocabulário das ordens, novo na 3.3b),
  `VozDispositivo` (0) e `VozMostrarTexto` (true). O microfone é acessado por
  **reflexão** (`VoiceInput.Mic`, 8 membros, uma vez só) porque `UnityEngine.AudioModule` não pode ser referenciado
  neste projeto: Whisper.net fixa a fachada netstandard 2.0 e o módulo do Unity 6 é netstandard 2.1 (CS1705); a
  fachada 2.1 do jogo resolve o CS1705 mas quebra `AudioClip.GetData` (CS7069, `Span<T>` no mscorlib).
- **Fase 3.3b — correções do teste da voz:** (1) **a tecla V é também o *Toggle Auto-Pickup* do Valheim**
  (`ZInput.Reset` → `AddButton("AutoPickup", KeyToPath(Key.V))`; **único** leitor: `Player.Update` →
  `ZInput.GetButtonDown("AutoPickup")`), então `Patches/AutoPickupKeyConflict.cs` põe um **prefixo Harmony em
  `ZInput.GetButtonDown(string)`** que responde `false` só para `"AutoPickup"` e só quando `[AI] VoiceEnabled` está
  ligado **e** o binding efetivo do jogo aponta para `[AI] VoiceKey` (lido por
  `GetButtonDef("AutoPickup").GetActionPath()`); reatribuir o auto-pickup no jogo, `VozLigada = false`, tecla
  desconhecida ou exceção ⇒ **não** bloqueia (o jogo fica intacto) e o jogador é avisado uma vez (log + chat/HUD).
  (2) **STT:** modelo default `whisper\ggml-small-q5_1.bin` (181 MB; o `base` fica em disco), prompt inicial
  (`WithPrompt`, sobrevive ao `WithNoContext`) e trim com limiar `0.01` + folga de `0,2 s`.
  (3) **Latência do LLM medida:** `[IA] Prompt:` (car./tokens) e `[IA] Tempo:` (ms, tokens/s, aviso a partir de 5 s),
  e `[AI] Memory` default **4**. Detalhes: `docs/roadmap.md` (Fase 3.3b) e `docs/re/stt-voz-pipeline.md`.
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
- **Fase 3.4 — o companheiro FALA (TTS local, Kokoro-82M via sherpa-onnx):** o espelho do `WhisperStt`. Novo
  `AI/CompanionTts.cs`: o `OfflineTts` (NuGet **`org.k2fsa.sherpa.onnx`** 1.13.8, **Apache-2.0**, `netstandard2.0`
  → net472; nativos `sherpa-onnx-c-api.dll` + `onnxruntime.dll` do `...runtime.win-x64`) é carregado **uma vez**
  (`TryGetTts`, lazy) e mantido; `Prewarm()` carrega em segundo plano quando você entra no mundo. A síntese roda
  numa **thread de fundo dedicada** (`ModIA-CompanionTts`) alimentada por `ConcurrentQueue` + `SemaphoreSlim`
  (teto de 3 pedidos), e os pedaços prontos voltam por outra `ConcurrentQueue` drenada no `Update` — o jogo nunca
  espera. **Por frase** (`SplitSentences`: `.` `!` `?` `…` e quebras de linha; ponto entre dígitos é decimal; uma
  sequência de terminadores é **um** corte; `:`/`,` não cortam) a 1ª frase já toca enquanto a 2ª é gerada;
  **cache** `voz+velocidade+frase → amostras` **só das linhas do mod** (as respostas do modelo, novas a cada vez,
  não entram: eram elas que expulsavam as falas fixas) com teto **por bytes** — **32 MB** de áudio, descartando as
  frases mais antigas (`MaxCacheBytes`/`MaxCacheEntries`, `StoreCached`/`TrimCache`) — faz as falas fixas saírem na
  hora. **Ciclo de vida:** o `Prewarm` **tenta de novo** a cada 30 s enquanto você está no mundo e o modelo não
  carregou (`MaybePrewarm`; "modelo não encontrado" no `TryGetTts` **não** é mais definitivo, então copiar a pasta
  com o jogo aberto funciona sem reiniciar), e o `Shutdown` **espera a thread de síntese** (`_worker.Join(5000)`)
  antes de liberar o `OfflineTts`, com `Process`/`TryGetTts`/`Speak` recusando trabalhar depois do flag
  `_stopping` (o `Dispose` fica dentro do `lock (NativeGate)`; se o `Join` estourar, o modelo **fica carregado**).
  Playback: **um**
  `AudioSource` **3D** na posição do companheiro (`spatialBlend=1`, `maxDistance = [Speech] CullDistance`), volume
  de `[AI] TtsVolume`, uma fala por vez (fila), `AudioClip` destruído ao terminar. **Nativos:** o binding é um
  `[DllImport("sherpa-onnx-c-api")]` puro (sem loader próprio, ao contrário do Whisper.net) e no Unity o Windows
  procuraria na pasta do **jogo** — `EnsureNativeLibraries` roda antes do 1º uso: `SetDllDirectory(<plugin>\
  runtimes\win-x64\native)`, pré-carrega as duas por caminho absoluto com
  `LoadLibraryEx(..., LOAD_WITH_ALTERED_SEARCH_PATH)` e **restaura** (`SetDllDirectory(NULL)`); o módulo fica no
  processo e o `DllImport` resolve por nome. **`AudioClip`/`AudioSource` por reflexão** (11 membros, uma vez): o
  `UnityEngine.AudioModule` não é referenciável (CS1705 com a fachada netstandard 2.0 fixada pelos pacotes de voz;
  a 2.1 do jogo quebra `Span<T>` → CS7069), o mesmo motivo do `VoiceInput.Mic`. **O que é falado** (regra: a voz
  acompanha a linha do chat, decidida pela função pura `CompanionChatAI.PickSpokenText`): `[AI] TtsSpeakReplies`
  = a resposta do modelo (`reply`); `[AI] TtsSpeakModLines` = a linha localizada do mod (numa **ordem** ela
  ganha; numa **conversa** a linha é a própria resposta). Só um dos dois é falado — o outro vai para o log.
  **Prompt:** o contrato passou a exigir `reply` de **UMA frase curta (até ~15 palavras)**, dito que ela é
  **falada em voz alta**. **Config `[AI]`:** `TtsEnabled` (true), `TtsVoice` (42 = pf_dora fem.; 43 = pm_alex,
  44 = pm_santa masc.), `TtsVelocidade` (1.0), `TtsThreads` (**4**; RTF medido **0,73–0,91**, mais rápido que o
  tempo real), `TtsVolume` (1.0), `TtsModelo` (`tts\kokoro\model.int8.onnx`; `voices.bin`/`tokens.txt`/
  `espeak-ng-data` da mesma pasta), `TtsIdioma` (**`pt-br`**, nunca vazio — o Kokoro é multi-lingue e a lib nativa
  recusa carregar sem idioma), `TtsCache` (true), `TtsFalarRespostas` (true), `TtsFalarFalasDoMod` (true). Logs:
  tempo de carga, tempo de síntese por frase, **RTF**, tamanho do áudio; comando de console **`modia_tts_teste
  [texto]`**. PT-BR: `hc_ai_tts_unavailable`. Verificado fora do jogo: `SplitSentences` **real** (17 casos) +
  `PickSpokenText` **real** (14 casos) → **ALL CHECKS PASSED**, e um harness que sintetizou áudio de verdade com os
  nativos carregados por esse caminho. Detalhes: `docs/re/tts-kokoro-sherpa-onnx.md`.
- **Fase 3.7 — painel da IA no jogo (abas por companheiro + push-to-talk):** novo `UI/CompanionAiPanel.cs`, aberto pela
  tecla **`[AI] PanelKey`** (novo `ConfigEntry<KeyCode>`, padrão **`I`** — livre no jogo e no mod; como é uma letra,
  a tecla é ignorada quando o chat/console/entrada de texto/busca do comerciante está ativo, e a tecla **também** é
  recusada com o inventário, o mapa, o menu ou o painel **F8** abertos — e o `ConfigPanel` recusa abrir enquanto este
  painel está visível, então os dois nunca ficam um sobre o outro). Painel de madeira
  montado **uma vez** (no primeiro `Show`) e só mostrado/escondido depois; fecha com a tecla, com **Esc** e com o
  **X**, entra na lista de UI que bloqueia o input (os dois prefixos de `TakeInput`, `Hud.InRadial` e
  `VoiceInput.BlockedByUi`) e se fecha sozinho quando o jogador morre ou sai do mundo. **Não precisa de companheiro
  perto** (config global). Controles (todos gravam no `ConfigEntry` e o BepInEx salva o `.cfg` na hora):
  a **aba IA** tem o invocador de companheiros de graça, os sliders de `TtsVolume` e `TtsVelocidade` (0,5 a 2,0, o
  mesmo intervalo do `.cfg` — `CompanionTts.MinSpeed/MaxSpeed`), os toggles `TtsLigado`/`TtsFalarRespostas`/
  `TtsFalarFalasDoMod`/`Conversa`, o slider de `Memoria` (0..12) e o **push-to-talk** (mostra `VozTecla` + botão
  **Alterar** com o **mesmo** `KeyBindListener` do painel F8 + toggle `VozLigada`; ao trocar a tecla o
  `AutoPickupKeyConflict.Reset()` reavalia o conflito com o auto-pickup do jogo). **Fase 3.10/3.15: a personalidade e a
  voz globais NÃO são editadas aqui** — elas vivem no `.cfg` e no painel **F8**; o painel `I` tem **uma aba por
  companheiro**, com a voz (com o botão **Ouvir**, que fala pelo **mesmo** `CompanionTts` — worker/cache/playback 3D)
  e a personalidade **daquele** companheiro, guardadas no ZDO dele, além do **nome** (campo + Aplicar) e do **tipo**
  (picker + "Trocar tipo (renasce)"). O dropdown é **feito à mão**
  (botão-campo + lista de botões numa camada overlay que é o último filho do painel e que tem **`Canvas` próprio** com
  `overrideSorting`/`sortingOrder` **1708** + `GraphicRaycaster`, com um *catcher* invisível que fecha a lista ao clicar
  fora) porque um `TMP_Dropdown` exige um template inteiro e o `Dropdown` legado usa o `Text` legado (sem a fonte do
  Valheim). Sem bloqueio de main thread, sem alocação por frame (por frame: tecla + cursor + status 2×/s) e sem
  segredos. PT-BR: 33 chaves
  `hc_ai_panel_*` / `hc_ai_voice_*` no `ModLocalization` **e** no `Translations/Portuguese_Brazilian.json`.
  Detalhes: `docs/roadmap.md` (Fase 3.7 — inclui a correção da lista que aparecia atrás dos controles).
- **Fases 3.9–3.17 — o companheiro como vários, com identidade própria (branch `feat/multi-companheiro`, versão 1.7.0,
  **mergeada na `main` e publicada** — testada em jogo pelo jogador):**
  - **3.9 — invocador e abas:** a aba IA invoca companheiros de graça (`CompanionManager.SpawnFree`, o **mesmo**
    `SpawnCompanion` do Trader, sem cobrar) e o painel ganhou **uma aba por companheiro** (o nome é o título), com
    Chamar/recall e os controles daquele companheiro; o nome padrão de um spawn novo passou a ser o nome do tipo com
    número ("Guerreiro 2") para duas abas não ficarem idênticas.
  - **3.11 — nome no comando:** `AI/CompanionNames.cs` — uma mensagem que **começa** com o nome de um companheiro real
    (`"Gruncha, colete 10 pedras"`) vale só para ele (prompt, persona, memória e ordem); o nome sai das **palavras do
    jogador**, não do campo do modelo, e a lista de nomes entra no prompt. Sem nome, a ordem continua indo para todos.
    Um nome inexistente **que parece nome** ("Fulano, colete…") é avisado e nada é ordenado — desde a **3.19** só conta
    como nome um começo **capitalizado** e que não seja forma de tratamento ("mano", "companheiro", "bora"...); na
    dúvida a ordem segue normal.
  - **3.10 — perfil de IA no ZDO:** `AI/CompanionProfile.cs` — voz (`HC_AiVoice`, 0 = padrão) e personalidade
    (`HC_AiPersonaSet`/`HC_AiPreset`/`HC_AiPersona`) **por companheiro**, no ZDO dele; os valores `[AI]` viraram o
    **padrão** de quem não tem escolha própria (a primeira entrada das listas é "seguir o padrão"). Lido no prompt e na
    fala; escrito só pelo dono (claim → `IsOwner()` → escrever).
  - **3.10 (lápide) — o perfil sobrevive à morte:** `WriteAiProfile`/`ReadAiProfile` em
    `CompanionTombstoneResurrect` copiam voz e personalidade para a lápide e de volta no respawn.
  - **3.13/3.15 — nome e tipo editáveis:** a aba do companheiro tem o **nome** (texto + Aplicar → `CompanionSetup.Rename`,
    que escreve `HC_Name` + `Character.m_name`; recusado para companheiro morto, porque a lápide guardaria o nome antigo)
    e o **tipo** (picker + confirmação → `CompanionManager.ChangeTier`: respawn com nome, inventário, equipamento
    (flags `m_equipped` limpos), aparência, casa/cama, skills, perfil de IA, `HC_CompanionId`/geração, alvo da ordem,
    **ledger do cozinheiro, ferramentas emprestadas e stamina** preservados).
  - **3.17 — a voz acompanha o balão:** `CompanionTalk.Say` passou a falar a linha que mostra (mesma string, voz **do**
    companheiro, cacheável) quando `TtsLigado` + `TtsFalarFalasDoMod` + modelo pronto; a deduplicação é um flag por
    chamada (`withVoice: false`) nos caminhos que já falam pela IA (`CompanionChatAI`/`CompanionOrders`), e `SayAsPlayer`
    nunca é falado. `[Speech] Chatter = false` segue valendo: o que não aparece não é falado.
  - **3.19 — teto de 10 (`[General] MaxCompanions`):** `CompanionManager.CanAddCompanion` barra o `SpawnCompanion`
    (Trader, invocador e companheiro inicial) quando o jogador já tem o limite; a conta é a do **roster** (`MHR_Roster.
    CountMine`: vivos + mortos + perdidos), então morrer não abre vaga; o Santuário continua recuperando os mortos e a
    recusa é avisada (HUD/chat) + log.
  - **3.18 — editor de aparência com preview 3D:** `UI/CompanionAppearancePanel.cs` (o botão *Aparência* na aba do
    companheiro, no painel `I`) reaproveita o `CompanionPanel` do starter num **modo edição** (`AppearanceEditMode`) —
    gênero, corte de cabelo, barba, cor de pele, tom e sombra do cabelo, com um **clone 3D** atualizando ao vivo. O
    apply vai por `CompanionSetup.ApplyAppearance` (ZDO `HC_Appearance`, **claim antes** — os setters do jogo só gravam
    sendo dono) **sem respawn**; a cauda de defaults do `BuildCustomColumn` só roda no **starter**, e
    `ApplyAppearanceToControls` restaura as cores guardadas (`_storedSkinColor`/`_storedHairColor`) antes de posicionar
    os sliders (era isso que fazia os três sliders abrirem no início).
  - **3.17b — o `stop` nomeia o trabalho:** `CompanionOrders` lê o modo do ZDO (`CompanionSetup.ActionModeHash`, a
    **mesma** chave do radial) **antes** de parar e monta a linha com o rótulo do trabalho (`hc_ai_order_<modo>` +
    `hc_ai_order_stop_job` = "parar de {0}"; `hc_ai_order_stop_other` = "parar" para casa/vagar) — antes dizia sempre
    "parar de seguir". Só a mensagem mudou; o comportamento do `stop` é o mesmo. Log: `[IA] Order "stop" (was: wood)…`.
  - **3.20 — os botões clonados do jogo e o cabelo/barba (três causas, uma sessão de teste):** (1) todo botão clonado
    do jogo herda o estado do **molde** — o do craft (`InventoryGui.m_craftButton`) vem `interactable = false` assim
    que você abre o inventário sem nada craftável, e o clone passa a **ignorar todo clique em silêncio**: `CreateButton`
    **e** `CreateCategoryArrowButton` (`CompanionPanel`) agora forçam `interactable = true` (era isso que "matava" as
    **setas de cabelo/barba** depois do primeiro inventário — o starter funcionava só porque abria antes). (2)
    `VisEquipment.SetHairEquipped`/`SetBeardEquipped` gravam o **hash antes** de anexar: se o `AttachItem` falha uma
    vez, aquele corte vira **no-op para sempre** — `CompanionSetup.PrepareVisualAttach` limpa
    `m_currentHairItemHash`/`m_currentBeardItemHash` a cada apply (e a cada troca de corte no preview) e procura o joint
    `Helmet`/`Head` quando `m_helmet` está nulo (sem ele o cabelo nasce **fora da cabeça**). (3) Diagnóstico
    `[AppearanceDiag]` (uma linha por ação: ZDO cru, cor guardada → posição dos sliders, estado do attach no preview e
    no companheiro) para não precisar adivinhar se voltar.
  - **Correção da 1.7.0 — a introdução do OpenRouter:** ver a seção *Camada de IA* (`[AI] BaseUrl`/`Modelo`) e
    `docs/wiki/LLM-no-LM-Studio.md` (a página cobre OpenRouter primeiro e o LM Studio como alternativa local).
- **Fase 3.22 — o peso efetivo do companheiro (1.8.0, branch `fix/peso-cinto`): o Megingjord soma de verdade.** O relato
  do Nexus (*"coloco a megingjord no inventário do companion e ela não aumenta o peso em 150"*) tinha três causas
  somadas: (1) o limite de peso é **nosso** (`[General] MaxCarryWeight`, 300) — o vanilla **não** limita um `Humanoid` que
  não é `Player` (`Inventory.CanAddItem` só olha grade; não existe `m_maxWeight` na 1.0); (2) o `+150` do cinto é um
  **status effect** (`item.m_shared.m_equipStatusEffect` → asset `SE_Stats` → **`m_addMaxCarryWeight` = 150, serializado
  no asset**) e o **único consumidor** é `Player.GetMaxCarryWeight()`; (3) o `AutoEquipBest` escolhe `Utility` por
  `GetArmor()` e o cinto tem armor 0, então ele nem equipava. **Correção:** novo `Companion/CompanionCarry.cs` —
  `Base` (config) + `Bonus(inv)` lido **do asset** (`SE_Stats.m_addMaxCarryWeight`, nunca 150 chumbado), com o item em
  **qualquer lugar da mochila** (decisão do jogador: `m_utilityItem` não persiste num `Humanoid` não-`Player`),
  **deduplicado por referência do `StatusEffect`** (a mesma regra do `HashSet` de `Humanoid.UpdateEquipmentStatusEffects`
  — dois cintos não somam), tudo cacheado por inventário (o `GetAllItems()` aloca; valida por `NrOfItems()` +
  `GetTotalWeight()`, 0,5 s de gate, re-scan completo a cada 10 s, 12 entradas, `Reset()` no `OnSceneLoaded`) para
  **zero alocação por frame** no caminho sem cinto. `Max(...)`/`Fraction(...)` passaram a valer em ~15 pontos
  (Encumbered no `CompanionSetup`, `CompanionAI` do ímã, loot/recolher, pesca, caça, ferreiro, upkeep, HUD de cima,
  painel do inventário), o `298f` fixo do "estou cheio" (`CompanionTalk`) virou `Max - 2`, e o
  **`[Gathering] OverweightThreshold`** virou **base** (`UnloadThreshold = config + Bonus`): a coleta agora só volta
  para casa perto de **448** com o cinto (298 + 150), continuando acima de 300. `CONFIGURATION.md` é **gerado** de
  `ModConfig.cs` (`tools/gen_config_doc.py`) — regenerar sempre que o texto de uma config mudar. Teste em jogo do
  jogador: *"ficou perfeito a correção do bug do usuário"*.
- **Fase 3.7 — correções do teste em jogo (teclas do jogo digitando no campo, caixa de texto e rodapé):** (1) novo
  `Patches/TextInputKeyGuard.cs`: `ModTextInputFocus` (registro dos campos de texto do mod + "algum em foco?"
  **cacheado por frame**) e **prefixos Harmony** em `ZInput.GetButtonDown(string)`/`GetButton`/`GetButtonUp`/
  `GetKeyDown(KeyCode)`/`GetKey`/`GetKeyUp` que devolvem `false` enquanto um campo de texto do mod está em foco — é o
  que impede as teclas do jogo de dispararem ao digitar (o `m` abria o mapa: `Minimap.Update` lê
  `ZInput.GetButtonDown("Map")` e **não** passa pelo `Player.TakeInput`, que é o que os guardas antigos barravam).
  Mesmo espírito do `AutoPickupKeyConflict` (condicional e *fail-open*); gamepad (`"Joy*"`, `JoystickButton0+`) e os
  métodos de mouse **passam** (o painel fecha com `JoyBack`/`JoyButtonB` e o clique na UI é do `EventSystem`); o
  `TMP_InputField` e os painéis do mod leem teclas pelo `Input`/`Event` legado, então digitar, Ctrl+A/C/V/X, Enter, Esc
  e o *key bind listener* seguem iguais. Os dois campos de texto do mod são registrados (`CompanionAiPanel` e o nome do
  companheiro em `CompanionInteractPanel`). (2) `CreateInputField` do painel monta o `TMP_InputField` com o GameObject
  **inativo** e só o ativa depois de `textComponent`/`textViewport`/`placeholder` estarem atribuídos (o `OnEnable` é
  onde o TMP cria o objeto `"Caret"` que desenha cursor e seleção — antes ele rodava com o componente vazio e o campo
  ficava **sem cursor e sem seleção**), o `"Caret"` vai para último filho, o texto usa `Overflow` +
  `PreserveWhitespaceNoWrap` (o `RectMask2D` do viewport recorta e o TMP rola na horizontal em vez de truncar com "…"),
  cursor claro de 2 px + `selectionColor`, `richText = false` e `onSubmit` → `ApplyPersonality()` (Enter aplica). O
  campo de nome do companheiro ganhou a mesma correção de `OnEnable`. (3) o rodapé do painel ganhou **faixa escura**
  própria, resumo em **duas linhas** localizadas (`hc_ai_panel_status_personality` + `hc_ai_panel_status_voice`, 12,5 px
  quase branco, `Overflow`), dica em 12 px e o painel cresceu **470 → 516** (nada cortado na borda). Detalhes:
  `docs/roadmap.md` (Fase 3.7 — correções do teste em jogo).
- **Correções da auditoria das Fases 3.4/3.7 (A1–A4, B1, C2, D2, E1, E2):** endurecimento do TTS e do painel, sem
  mudar comportamento. `CompanionTts`: `Shutdown` **espera a thread** (`Join(5000)`) antes de `Dispose` e
  `Process`/`TryGetTts`/`Speak` recusam trabalhar depois de `_stopping` (A1/A3); `audio.Handle != IntPtr.Zero`
  checado **antes** de `NumSamples` (o binding devolve objeto não-nulo com handle nulo → *AccessViolation*) (A2);
  `Prewarm` com **nova tentativa** a cada 30 s e "modelo não encontrado" **não** mais definitivo (A4); cache **por
  bytes** (32 MB) + **só linhas do mod** (C2); `PreloadNative` **avisa** se o módulo carregado não é o do plugin
  (o `DllImport` resolve por nome-base) (B1); `MinSpeed`/`MaxSpeed` compartilhados com o painel (E2).
  `CompanionAiPanel`: a tecla é recusada com inventário/mapa/menu/**painel F8** abertos e o `ConfigPanel` recusa
  abrir com o painel da IA visível (D2); `hc_ai_voice_key_conflict` ganhou o par em inglês (E1); **D3** (cursor no
  `Hide`) não foi mexido de propósito — quem devolve o cursor é o `GameCamera.UpdateMouseCapture` do próprio jogo.
  Detalhes: `docs/roadmap.md` (Fase 3.4/3.7 — correções da auditoria) e `docs/re/tts-kokoro-sherpa-onnx.md`.
- **Fase 3.4 (parte 3) — o eco da voz: RESOLVIDO (branch `fix/eco-voz`). O eco era a zona de reverb global do
  Valheim:** o diagnóstico (`EcoDiag = true`) achou a causa na primeira tentativa. O jogo tem **UMA `AudioReverbZone`
  global**, no **próprio objeto do `AudioListener`** (`[ECO] zona "Listner": preset=User, minDistance=1000,
  maxDistance=1000, decayTime=4, room=-1000, dentro: jogador=SIM, companheiro=SIM`) — raio de **1000 m**, **4 s** de
  cauda e `room = −10 dB`, ou seja, **tudo** dentro de 1 km levava essa cauda (o ouvinte **não** tem
  `AudioReverbFilter`/`AudioLowPassFilter`: não era filtro no ouvinte nem Doppler). As fontes do **jogo fogem** dela
  (música `reverbZoneMix = 0`, ambiente `0,1876583` no mixer `SFX`; as de SFX curtas usam 1 e num som curto/repetitivo
  não se nota), mas a **nossa** `AudioSource` estava no **padrão do Unity (`reverbZoneMix = 1`)** e sem mixer group —
  a fala pegava a cauda inteira. **Correção (ouvida e aprovada em jogo: "limpou"): `reverbZoneMix = 0`** — a voz sai
  **seca**, como a música. O que ficou:
  - **`[AI] TtsReverbZoneMix`** (float 0–1, **padrão 0** = voz seca) é a opção de verdade: `0,19` (o valor do ambiente
    do jogo) dá um "espaço" sutil, se o jogador quiser. `ApplyReverbZoneMix(source)` roda em `StartChunk` e escreve
    **só quando o valor muda** (a `AudioSource` é criada uma vez e fica na sessão) — vale **na próxima frase**, sem
    reiniciar, e a primeira frase já aplica o 0.
  - **Os 4 interruptores de exploração foram removidos** (`TtsBypassReverbZones`, `TtsBypassListenerEffects`,
    `TtsBypassEffects`, `TtsDoppler`: campos, `Bind`, propriedades e o código de aplicação) — cumpriram o papel, e o
    `.cfg` do jogo perde as chaves sozinho.
  - **`[AI] EcoDiag`** (bool, `false`) **ficou**, agora como **diagnóstico de áudio geral**: **uma vez por sessão** na
    primeira frase (prefixo `[ECO]`, `try/catch`, na main thread) escreve o `AudioListener` (nome +
    `AudioReverbFilter`/`AudioLowPassFilter` e valores), **todas as `AudioReverbZone`** da cena
    (`reverbPreset`/`minDistance`/`maxDistance`/`decayTime`/`room` + se o jogador e o companheiro estão **dentro** do
    raio) e **a receita do jogo** — amostra de até 20 `AudioSource` da cena (a nossa fora) com `spatialBlend`,
    `reverbZoneMix`, os `bypass*`, `dopplerLevel`, `spatialize` e o **nome do mixer group**
    (`Resources.FindObjectsOfTypeAll(Type)` filtrado por `gameObject.scene.IsValid()`, nunca por frame). **Não muda o
    som.**
  - **Reflexão:** o núcleo obrigatório (11 membros) continua igual; `reverbZoneMix` é o único membro **opcional** que
    a voz escreve; `bypassReverbZones`/`bypassEffects`/`bypassListenerEffects`/`dopplerLevel`/`spread`/`spatialize`/
    `outputAudioMixerGroup` continuam resolvidos e são usados **só de leitura** pelo diagnóstico (`DescribeSource`).
  - **Pendência conhecida:** a voz **não** passa por um **mixer group** do jogo (a música e o ambiente passam); roteá-la
    para o `SFX` fica para depois — não é necessário para o eco.
  - **Não mudou:** `spatialBlend`, o gate de fim de fala (`_currentEndsAt`), `StopSource`, cache, fila, síntese,
    prompt, painel, STT, ordens e jobs.
  Detalhes: `docs/roadmap.md` (Fase 3.4 — o eco, parte 3) e `docs/re/tts-kokoro-sherpa-onnx.md`.
- **Fase 3.1 — saída estruturada (JSON schema): o que permite modelos pequenos:** o pedido ao LLM passou a levar
  **`response_format`** com um **JSON schema** (`companion_intent`, `strict`, `additionalProperties: false`) que fixa as
  cinco chaves do contrato e a **lista fechada de ações** — o `enum` sai de **`CompanionChatAI.ValidActions`** (fonte
  única, ao lado do `Contract`, com comentário dizendo que **tem** que acompanhar o `Contract`: 29 ações, de `follow` a
  `none`), montado **uma vez** em `LlmClient.JsonSchemaFormat`. É o que elimina a classe de erro "o modelo respondeu
  texto solto" e o que **destrava modelos pequenos**: medido com o prompt atual (~1766 tokens), o
  `qwen2.5-3b-instruct` dava **0/6** respostas em JSON e passou a **6/6** (inclusive `target` em
  `attack`/`equip`/`drop`); o `google/gemma-4-e4b` continua igual. **Cadeia de fallback, 1 tentativa cada**
  (`LlmClient.Post`): `json_schema` → `{"type":"json_object"}` → **sem** `response_format` (como era antes); só uma
  **recusa do formato** dispara a próxima (`RejectedFormat`: **HTTP 400** ou erro citando
  `response_format`/`json_schema`) — timeout, socket morto, 500 e erro de rede **não** são repetidos, e o mesmo corpo
  nunca é enviado duas vezes. Logs: uma linha na 1ª mensagem dizendo que a saída estruturada está ligada (schema, nº de
  ações, tamanho do corpo), uma por queda e **um único aviso** (`WarnNoStructuredOnce`) ao chegar no formato antigo.
  Cada tentativa tem o **seu** `Timeout`/`ReadWriteTimeout` = `[AI] TimeoutSeconds` (não dividido: o caminho normal é
  a tentativa 1 e um modelo lento é o caso comum; um 400 volta em ms, então a cadeia quase não custa tempo) e o
  **watchdog** do slot em voo passou a cobrir as 3 (`TimeoutSegundos × 3 + 10 s` com a saída estruturada ligada, igual
  a antes quando desligada) para não liberar o slot no meio de um fallback. Config nova `[AI] StructuredOutput` (bool,
  **true**; aparece sozinha no painel F8). Os três corpos são montados **na main thread** (a `Task` só envia). **Não
  mudou:** o `Contract`, o `LlmIntent`, o fallback da 3.6b, a personalidade/memória, o `KeepAlive`, o `BaseUrl`, o TTS,
  o painel, o STT e as ordens. Detalhes: `docs/roadmap.md` (Fase 3.1 — saída estruturada).
- **Fase 3.1 — o teste em jogo com o `qwen2.5-3b-instruct` e a correção "o recurso manda":** o teste achou o companheiro
  **coletando madeira** para `colete 10 pedras`. O log mostrou o modelo devolvendo `action` e `resource`
  **inconsistentes** (`{"action":"wood","resource":"stone"}`) e o mod obedecendo ao `action` **genérico**, ignorando o
  `resource` — que é o campo específico e o que já tinha sido resolvido para o item do teto (`Order target: mode 1
  stops at 5 Stone` + `Order "wood" (x5)`). Correção: **`OrderItems.GatherAction(action, resource)`** (novo) diz **qual
  ação de coleta o `resource` indica**, e o **`CompanionOrders.Run`** a chama **uma vez por mensagem** — quando os dois
  discordam sobre qual trabalho é, **o recurso manda** e sai **uma linha** no log (`[IA] O modelo devolveu action="wood"
  com resource="stone" - usei "stone" (o recurso manda).`). **Só as ordens de coleta** (`wood`/`stone`/`ore`/`forage`):
  `attack`/`equip`/`drop`/`inventory` e as demais passam intactas, e `resource` nulo/desconhecido mantém tudo como
  antes. **Sem tabela nova:** o nome passa pelo **mesmo** mapa de apelidos do `OrderItems.Resolve` (`pedra`→`Stone`) e o
  item é procurado entre as **entradas nomeadas das quatro listas que os trabalhos leem**
  (`GatherWood`/`GatherStone`/`GatherOre`/`ForageItems`), por prefab **ou** pelo nome do jogo; par já coerente (o
  recurso é da lista do próprio trabalho) **não** é mexido e lista `*` não decide nada. Ficam como estavam: o teto da
  3.2c e a contagem na mochila, o `Resolve`, o alvo do radial e o fallback da 3.6b. Verificado fora do jogo (harness com
  os arquivos reais): **60 casos, ALL CHECKS PASSED**. Detalhes: `docs/roadmap.md` (Fase 3.1 — §6).
  A outra metade do achado é **do modelo**, e o **`Contract`** mudou por causa dela: o 3B copiava o `action:"wood"` do
  exemplo `"colete 20 de madeira"` para `colete N pedras` (o verbo `colete` era o gatilho — *few-shot overfitting*), então
  entrou **uma regra curta** dizendo que `action` e `resource` **combinam** (pedra → `stone`/`stone`, madeira →
  `wood`/`wood`, minério → `ore`/`ore`) **e 2 exemplos** de `colete N pedras` → `stone`, logo depois do exemplo de
  madeira. O resto do contrato (as 29 ações, o `target` obrigatório, os outros exemplos) ficou **intacto** e o prompt
  cresceu **3 linhas** (~+60 tokens sobre os ~1766 medidos). Detalhes: `docs/roadmap.md` (Fase 3.1 — §7).
- **Fase 3.1 — o teste em jogo com o `qwen2.5-3b-instruct` e as ações vizinhas (`drop`/`pickup`/`nopickup`):** o 3B
  confunde as três — `jogue fora sua madeira` (e as variantes `jogue sua madeira fora` e `jugue a madeira fora`, com erro
  de digitação) voltou como **`nopickup`**, isto é, o companheiro **DESLIGAVA a coleta automática** em vez de largar a
  madeira, `junte a madeira do chão` ficou **50/50** entre `pickup` e `nopickup`, e `equipe a picareta` devolveu
  `target:"machado de bronze"` (copiou o exemplo; o fallback da 3.6b equipou a picareta certa, mas o alvo veio errado do
  modelo). **(1) O contrato** ganhou **uma regra curta** separando as três ações — `drop` = largar/jogar fora o que o
  companheiro tem, `pickup` = recolher itens do chão, `nopickup` **só** para PARAR de recolher ("pare de recolher", "não
  pegue nada") — e **3 exemplos**: `"jogue fora a madeira"`→`drop`, `"junte a madeira do chão"`→`pickup` (nenhum dos dois
  tinha exemplo) e `"equipe a picareta"`→`equip` (só existia `"pega a picareta"`, e foi a forma com `equipe` que falhou);
  **6 linhas** (~+120 tokens sobre os ~1766 medidos), com `ValidActions`/schema, `LlmIntent`, `LlmClient`,
  personalidade/memória, TTS, painel, STT e ordens **intactos**. Detalhes: `docs/roadmap.md` (Fase 3.1 — §8.1).
- **Fase 3.1 — a rede de segurança das ações vizinhas (`"jogue fora"` vira `drop` mesmo se o modelo disser `nopickup`):**
  o contrato sozinho não bastava (o 3B insiste em `nopickup` em parte das tentativas), então as **palavras do jogador**
  mandam — mesmo raciocínio do "o recurso manda". **`PickupIntent.NeighborAction(action, userText)`** (novo
  `AI/PickupIntent.cs`, **puro**: sem Unity/config/mundo) é chamado **uma vez por mensagem** pelo `CompanionOrders.Run`,
  no mesmo lugar da correção do `resource`: só com a ação **exatamente `nopickup`**, um verbo claro de **largar** (`joga
  fora`, `jogue a madeira fora`, `larga`, `largar`, `descarta`, `soltar`, `deixa no chão`...) troca a ação por **`drop`**,
  e um verbo claro de **recolher** (`junta`, `junte`, `apanha`, `recolhe do chão`, `pega do chão`...) por **`pickup`** —
  guarda-corpos: o `nopickup` **legítimo** ganha (`para`/`pare`/`parar` ou `não pegue`/`não recolha` desligam a correção),
  nenhuma outra ação é tocada, o casamento é entre separadores (`_larga_` não casa `largada`) e, sem gatilho, o
  comportamento é idêntico ao de hoje. O **alvo do `drop`** continua vindo do caminho da 3.6b (`OrderTargets.Phrases` +
  validação contra a mochila) — nada de extração de nome nova; o `pickup` é o mesmo toggle do radial (`SetAutoPickup`).
  Log: `[IA] O modelo devolveu "nopickup" mas o jogador pediu para largar - usei "drop".` Verificado fora do jogo (harness
  com os arquivos reais): **58 casos, ALL CHECKS PASSED**. Detalhes: `docs/roadmap.md` (Fase 3.1 — §8.2).
- **Fase 3.1 — a ação de recolher os itens do chão (o que faltava) + o botão Recolher da roda passa a valer:** o jogador
  largava o machado (`jogue seu machado no chão`) e depois pedia para pegar de volta (`pegue o machado nordico do chão`,
  `junte o machado`, `recolhe os itens`); o modelo acertava (`Order "pickup" -> 1 companion(s)`, `Ok: recolher itens do
  chao`) e **nada acontecia**. O achado são três coisas que se somam: (1) a ação `pickup` **só setava um flag** no ZDO
  (`CompanionSetup.AutoPickupHash`) e **nenhuma** ação do mod andava até um item no chão para pegá-lo (o único "pega item"
  era o ímã de 2 m do `CompanionAI.AutoPickup`); (2) o que o companheiro larga é marcado `m_autoPickup = false` **de
  propósito** (é o que o próprio jogo faz em `ItemDrop.OnPlayerDrop()`), e o ímã **e** o `MHR_Loot` pulam esses itens de
  propósito — justamente o que o jogador pediu para pegar; (3) o flag só era lido pelo `MHR_Loot` e pelo rótulo da roda,
  então o botão **Recolher** não desligava o ímã (igual no upstream). **Novo `Jobs/MHR_Collect.cs`** (mesmo padrão do
  `MHR_Loot`: estado por companheiro, `Start(CompanionSetup)` pela ordem e `Drive(CompanionAI, dt)` por frame pelo
  `CompanionAI.UpdateAI`, **antes** do `MHR_Loot.Drive`): acha o item solto mais próximo dentro de `[Looting] CollectRadius`
  (**novo, padrão 10**, faixa 1..30), **anda até ele** com o `MHR_Mover` (tag `Collect`) e pega com `Humanoid.Pickup` — que
  **não olha `m_autoPickup`** (conferido no decompilado: só `ItemDrop.CanPickup` — o atraso de 0,5 s do spawn —, a mochila e
  o item de missão), então **inclusive o que foi largado de propósito** volta. Repete com o próximo até não sobrar nada no
  raio, a mochila encher ou a ordem passar de **30 s** (tempo não dirigido — briga, descanso — não conta). **Recusa:** item
  de missão (`m_shared.m_questItem`, a regra do `drop`), peça (`IsPiece()`), posição na *blacklist* do `CompanionAI`, água
  funda (`MHR_Water.WouldSwim`, "nunca nadar por loot") e o que não couber **inteiro** na mochila (`Inventory.CanAddItem(item)`
  sem número — sem isso o alvo seria reescolhido a cada frame). **Para** com outra ordem (o `CompanionOrders.Run` derruba o
  recolhimento para qualquer ordem que não seja `pickup` e o `CompanionInteract.CancelExistingActions` faz o mesmo para a
  roda), troca de trabalho (modo virou um job), `Recolher` desligado, mochila cheia, morte, posse perdida ou a **coleira**
  (`[Looting] MaxDistance`, a mesma do `MHR_Loot`); e os **jobs esperam** enquanto ele anda (`MHR_JobRules.OtherOrder`
  responde `true` — todos dirigem pelo mesmo `MHR_Mover`). A ordem faz **as duas coisas** (`Apply`, `case "pickup"`: liga o
  flag **e** `MHR_Collect.Start`); o `nopickup` continua só desligando o flag, e o ímã/`MHR_Loot` **não mudaram** (a proteção
  do "larga pra dar" fica intacta). **O ímã agora respeita o flag** (lido **uma vez** por frame em `UpdateAI`, padrão `true` —
  quem nunca mexeu no botão Recolher fica exatamente como hoje; `MHR_PileTaker`/`UpkeepPiles`/`UpkeepHoney`/`MHR_SpearThrow`/
  `HarvestController` chamam `Humanoid.Pickup` direto e não dependem dele). Falas novas: `hc_ai_collect_nothing` ("Não tem nada
  no chão por perto.") e `hc_ai_collect_full` ("Minha mochila está cheia.") — PT-BR + dicionário em inglês; logs
  `[Collect] <Nome>: Going to pick up "X" (N m away)`, `Done: N item(s) picked up`, `nothing on the ground within N m`.
  Detalhes: `docs/roadmap.md` (Fase 3.1 — a ação de recolher os itens do chão).
- **Fase 2.0 — as 5 ordens de interação (chat e voz): `hive`, `door`, `board`, `sit` e `deposit`.** Cada uma reusa o que a
  **tecla de comando** já faz (as `Direct*` privadas de `UI/CompanionInteract.cs`): os novos wrappers `OrderHive`/`OrderDoor`/
  `OrderBoard`/`OrderSit`/`OrderDeposit` resolvem o **alvo** (o objeto **apontado**, com o mesmo raio/mask/50 m da tecla, senão o
  mais próximo do jogador — 20 m colmeia/porta, 30 m navio, 5 m fogueira/baú) e entregam à `Direct*` uma **lista de um
  companheiro só** (o endereçado, ou o mais próximo que pode receber ordem, com o mesmo `MHR_InScope` de `attack`/`equip`). O
  `ValidActions` foi de **30 para 35** (é dele que sai o `enum` do schema) e os **5 contratos** ganharam uma cláusula curta para
  cada ação + **2 exemplos** ("fecha a porta", "guarda isso no baú" — mais exemplos pioram o 3B). As `Direct*` falam a própria
  linha aleatória: durante a ordem ela é **silenciada** (`Silent`/`_orderSpeech` no `SayRandom`) porque a ordem diz a **sua**
  linha (`hc_ai_door_closed`/`hc_ai_deposit_done` com `{0}` etc.) e ela já vai para o TTS pelo `SpeakOutLoud`. Porta trancada →
  `hc_cmd_door_locked`; colmeia vazia → `hc_cmd_hive_empty`; baú sem espaço → `hc_ai_deposit_full`; nada por perto →
  `hc_ai_<ação>_none`. 17 chaves novas nos 5 idiomas. Rede extra no `LlmIntent` (`IsDoorWord`, no mesmo molde do
  `IsInventoryWord`): uma ação que **nomeia a porta** (`fecha_a_porta`) vale mais que o verbo, que o 3B manda como
  `inventory_close` (o `"fecha"` é apelido dessa ação desde a 3.8).
- Backend/contrato de referência: `docs/re/backend-contract.md`; mapa da fase: `docs/roadmap.md` (Trilha D);
  pipeline de voz: `docs/re/stt-voz-pipeline.md`; fala do companheiro: `docs/re/tts-kokoro-sherpa-onnx.md`.
- **Status (out/2026):** as fases da camada de IA estão **validadas em jogo** pelo jogador (3.1, 3.1-saída
  estruturada, 3.1b, 3.1c, 3.1d, 3.2, 3.2c, 3.3, 3.3b, 3.3c, 3.3d, 3.4, 3.5, 3.6, 3.6b, 3.7, 3.8 — com a evidência
  citada na tabela da Trilha D do `docs/roadmap.md`). A **3.1b/3.1c** (`colete 10 pedras`) também foi confirmada em
  jogo: o `qwen2.5-3b-instruct` devolveu `stone`/`stone` e a ordem aplicou o teto de 10 de pedra em ~378 ms.
- **Correção (branch `fix/clique-f8`) — o painel F8 não engole mais cliques.** O relato ("algo invisível na frente dos
  controles numa **faixa**") tinha **duas** coisas somadas: (1) o canvas do `ConfigPanel` estava em **31**, a **mesma
  camada** do UI do jogo, e num empate de `sortingOrder` o uGUI desempata pela **ordem de registro dos raycasters**
  (`RaycasterManager`) — a do jogo vence, e um alvo de clique **invisível** dele (texto de `MessageHud`/HUD apagado com
  `canvasRenderer.SetAlpha(0f)`, que continua recebendo clique) numa **faixa fixa no centro da tela** engolia os cliques
  daquela faixa; o painel da IA (tecla `I`) **nunca** teve o problema porque sempre esteve em **33**. (2) Os elementos
  **decorativos** do painel eram `raycastTarget = true` (fundo do painel, faixas das linhas, o viewport do `ScrollRect`
  e — os piores, por serem os **últimos irmãos** do viewport, isto é os gráficos mais acima de todo o painel — a barra
  de scroll **invisível** e o **handle** dela). **Correção:** a camada foi para **33** (a mesma do painel da IA; os dois
  recusam abrir juntos) e todo gráfico decorativo virou `raycastTarget = false`, ficando `raycastTarget = true` **só**
  nos controles (abas, ON/OFF, Padrão, slider fundo+handle, *key bind*, X) e nas duas **áreas de scroll** (o viewport do
  conteúdo e a coluna de abas, os GameObjects onde vivem os `ScrollRect`s); a barra invisível virou **primeiro** irmão do
  viewport. Um **diagnóstico** novo (uma vez por sessão, na primeira abertura) loga `[UiDiag] F8 raycast audit: ...` com
  os alvos não-interativos do painel e **as outras camadas ativas da cena** (`nome=sortingOrder`) mais qualquer alvo que
  cubra a coluna de controles — se o bug voltar, o log diz quem é. Ver `docs/roadmap.md` (*Pendências conhecidas*) e
  `package/CHANGELOG.md`.
- **Correção (branch `fix/camadas-ui`) — os painéis e a roda do mod vão para a faixa de camadas 1701+; era isso o clique
  engolido no F8.** A correção anterior (`fix/clique-f8`) era **insuficiente**: subir o `ConfigPanel` de 31 para **33**
  não resolve nada porque **o HUD do jogo está em `sortingOrder = 400`** — o `[UiDiag]` que ela mesma deixou no painel
  mediu isso (`canvas "HUD" (order=400, overrideSorting) ... crosshair/HoverName (TextMeshProUGUI 350x150) at the panel
  point 196,-46`), e **qualquer valor abaixo de 400 perde o clique** para o HUD. Agora **todos** os canvases do mod saem
  de 28..34 para **1701..1708** (`src/Companions/UI/ModUiLayers.cs`, **fonte única**: `FarmZonePlacer` 1701,
  `HomeZonePanel` 1702, `CompanionRadialMenu` 1703, `StarterCompanionPanel` 1704, `FarmZoneCropPicker` 1705,
  `ConfigPanel` 1706, `CompanionAiPanel` 1707, overlay do dropdown 1708), **mantendo a ordem relativa** entre eles. A
  faixa foi escolhida assim: **acima** do HUD (400), do `HudMessage` (1000) e do `Menu` (1700); **abaixo** do
  `UnifiedPopup` (**1900** — popup modal **do jogo**, classe vanilla de `assembly_valheim`, que **não** é nossa e não
  pode ser movida: um modal tem que ficar na frente de uma janela) e do `LongPressRadial` (2000 — evitar o **empate**,
  que o jogo ganha pela ordem de registro dos raycasters); e muito abaixo do console (5000) e do `SwitchCursor` (12000).
  Os painéis do mod continuam bloqueando inventário/loja/menu (`Hud.InRadial`), então esses nunca ficam atrás de nós. O
  mesmo `sortingOrder` alto resolve o **outro** sintoma relatado — o **texto do centro da roda** embaralhado com o
  `crosshair/HoverName` do jogo, que desenhava por cima dos rótulos do centro. O audit foi **mantido e generalizado**: a
  lógica saiu para `src/Companions/UI/ModUiAudit.cs` e agora roda para o painel F8 **e** para o painel da IA, uma vez por
  sessão (nunca por frame). Ver `docs/roadmap.md` (*Correção — o clique engolido no painel F8 e o texto do centro da
  roda: a camada era a causa*) e `package/CHANGELOG.md`.
- **Correção (branch `fix/uidiag`) — o diagnóstico de raycast vira opt-in (`[Advanced] UiDiag`, padrão desligado) e para
  de avisar sobre os alvos de clique intencionais do painel da IA.** O audit `[UiDiag]` (`UI/ModUiAudit.cs`) **continua
  no mod** — foi ele que achou o `HUD` do jogo em 400 engolindo os cliques —, mas ele rodava **sempre** e uma sessão
  saía com **6 linhas / 7.132 caracteres**, das quais **três** eram **falsos positivos** do painel da IA
  (`HC_AiPanel/AiRoot`, `HC_AiPanel/AiRoot/KeyBox`, `HC_AiPanel/AiRoot/Overlay/PersonalityList`). Agora: **config nova
  `[Advanced] UiDiag`** (bool, **`false`**; na `[Advanced]` e **não** na `[AI]` porque ele audita a **UI do mod** — os
  dois painéis e a faixa de `UI/ModUiLayers.cs` —, enquanto o `[AI] EcoDiag` diagnostica um componente da IA, a voz);
  **desligado = zero linha e zero custo** (os dois chamadores, `ConfigPanel.LogRaycastAudit` e
  `CompanionAiPanel.LogRaycastAudit`, saem antes de entrar no `ModUiAudit` — e o flag de "uma vez por sessão" só é
  marcado quando o audit **roda**, então ligar a opção no meio da sessão vale na próxima abertura); **ligado = compacto**
  (uma linha de aviso de que está ligado, uma **linha de resumo por painel** com **contagens** — os nomes dos alvos
  decorativos saíram —, a lista de camadas da cena numa linha e os **avisos** linha por linha com caminho + tipo +
  tamanho + ponto). Os falsos positivos: `AiRoot` (fundo do painel) e `KeyBox` (caixa da tecla) viraram
  **`raycastTarget = false`** (decoração, e o `Backdrop` do painel — tela cheia, criado antes do root — já é o catcher
  modal; é o que o `ConfigRoot` do F8 já fazia), e as **barras de fundo das listas do dropdown** (`Overlay/PersonalityList`
  e `Overlay/VoiceList`) **continuam alvos de propósito** (o clique na borda de 2 px da lista tem que morrer nela, senão
  chega ao `ListCatcher` e fecha a lista) numa **allow-list documentada** (`ModUiAudit.Intentional`, casada pelo fim do
  caminho + motivo). Ver `docs/roadmap.md` (§3.1 da correção das camadas) e `package/CHANGELOG.md`.
- **Tradução PT-BR — auditoria de cobertura (branch `fix/traducao`):** varredura de **todo texto que o jogador pode
  ver** — chaves de UI (`ModLocalization` × `Translations/Portuguese_Brazilian.json`), falas
  (`Speech/Portuguese_Brazilian.json`), chamadas `Loc`/`LocFmt`/`LocOr` do `src/`, literais em inglês nos caminhos de
  UI/HUD/hover/fala e os docs. **374 → 386 chaves de UI (0 ausentes, 0 em inglês)** e **15 categorias de fala / 186
  linhas (0 ausentes)**. Os **textos em inglês que apareciam** viraram chaves: o **nome do companheiro e dos tipos**
  (`CompanionPrefabs` escrevia `m_name = def.DisplayName`, e era isso que o HUD/hover/roda mostravam —
  `CompanionTierData.LocalName` resolve `hc_tier_*`), **"X has been resurrected!"** (`hc_msg_resurrected`), o
  **"Hold"** do hover (`hc_hover_hold`), os **três cumprimentos do recall** que estavam hardcoded no `ReturnLogic`
  (`hc_cmd_return_1..3`), **"ON"/"OFF"** do painel F8 (chaves do radial), **"Push-to-talk"** → **"Aperte para falar"**
  e os reservas **"Companion"** que sobravam em caminhos visíveis (`hc_msg_name_default`). A chave **órfã**
  `hc_radial_despawn_confirm` voltou aos dois lados. **Não é nosso** (fica como está): nomes de item/criatura/cultura
  dos assets do jogo, tokens `$...` do jogo, chaves de configuração e as ~1500 linhas de log/diagnóstico — o
  `CHANGELOG` mantém o histórico do upstream (abaixo de 1.5.4) em inglês de propósito. Números e método:
  `docs/roadmap.md` (*Tradução PT-BR — auditoria de cobertura*) e `package/CHANGELOG.md`.
- **Fase 3.19 — a rodada de correções da revisão completa (branch `feat/multi-companheiro`, 1.7.0):** `inventory_close`
  entrou no **`ValidActions`** (agora **30** ações — é dele que sai o `enum` do schema), o `Contract` a cita com regra +
  exemplo e o fallback `IsInventoryRequest` roda também quando o modelo devolve `inventory` **e** o texto tem verbo de
  fechar (`"fecha o inventário"` não abre mais a janela); o endereço desconhecido virou **conservador** (só conta como
  nome um começo capitalizado que não seja forma de tratamento, então `"mano, colete 10 pedras"` volta a ordenar — e
  `"Fulano, colete 10 pedras"` continua avisando); o nome do painel de interação é gravado **no `onEndEdit`** por
  `CompanionSetup.Rename` (claim + dono), não a cada tecla; a **troca de tipo** recusa companheiro morto e o botão se
  repinta quando ele morre/renasce; as ordens gravam no ZDO só com posse e **repetem no Update** (`MHR_WriteOrQueue`,
  com aviso se a posse não chegar em 5 s); o LLM ganhou **id de requisição** (`LlmResult.Id`, watchdog enfileira erro em
  vez de só liberar o slot e a resposta atrasada é descartada); a **lápide e o `DoRespawn`** comparam
  `HC_CompanionId`/lápide antes do nome (dois companheiros com o mesmo nome), o roster indexa por id e o
  `RemoveStaleCopies` passou a rodar num portão de **1 s** (e **dropa** o inventário da cópia velha antes de destruí-la); os
  caches de estoque de `MHR_GatherRules`/`MHR_SeedRules` são **por companheiro** (`MHR_StockKey`); `SuppressAutoEquip`
  da lápide está em `try/finally`; a localização se reinjeta quando o `Localization` é recriado (instância + idioma); o
  `ChangeTier` copia `HC_CookLedger`/`HC_Borrowed`/`HC_Stamina` (`CopyCarriedKeys`, lista explícita); `LogWarn`/
  `LogInventoryContents`/`LogHeartbeat` do coletor logam de verdade; "para" no meio (preposição) não é mais verbo de parar
  no `PickupIntent`; o push-to-talk respeita o painel do starter e o da casa; o endpoint do LLM é logado **sem query**; a
  memória cai para 4 quando o config não está pronto; o `UiDiag` perdeu as duas entradas mortas; o rodapé do companheiro
  recalcula quando ele herda o valor global; o banco do Trader é lido 2×/s; e o teto de **`[General] MaxCompanions = 10`**
  barra os três caminhos de criação com aviso localizado. Detalhes: `docs/roadmap.md` (Fase 3.19).
- **Fase 4.0 — multilíngue com o inglês nativo (1.8.3, branch `languages`):** o mod deixou de ter as configs traduzidas
  no código. Agora **as chaves e as descrições do `.cfg` são em inglês** (as 237 do upstream foram recuperadas do repo
  de referência; as nossas — 39 da seção de IA + `CollectRadius`, `UiDiag`, `MinInterval`/`MaxInterval` etc. — foram
  escritas em inglês) e o **português é tradução**, carregada de `Translations/Portuguese_Brazilian.json`. O arquivo de
  config passou a ser **`BepInEx\config\AiCompanions.cfg`** (o `Maddhattr.OfflineCompanionsReborn.cfg` é **ignorado** e o
  jogador volta aos **padrões**; o **GUID continua** o mesmo, e o migrador do upstream `com.profmags.companions.cfg`
  segue valendo, porque o arquivo dele já era em inglês). A seção de config `[IA]` virou **`[AI]`**. O **F8 ficou
  bilíngue**: `ConfigPanel.EntryLabel` usa `LocOr("hc_cfg_<seção>_<chave>", <chave>)` e as abas `hc_cat_*` — com o jogo
  em inglês o texto é a **própria chave** (zero tradução) e com o jogo em PT vem o rótulo do JSON; as **chaves repetidas**
  entre seções (`MaxDistance`, `Range`, `Radius`, `FollowRange`, `BagFull`, `UsePiles`) resolveram-se porque a seção
  entra na chave. **Nomenclatura:** nesta doc, menções históricas a `[IA] <chave em PT>` devem ser lidas como
  `[AI] <Key>` (a tabela completa está no `CHANGELOG` 1.8.3 e na wiki, em *Configuração*); as **linhas de log** mantêm
  o prefixo **`[IA]`** de propósito (é tag de log, não caminho de config). Descrições PT de referência (v1.8.2):
  `docs/re/config-descricoes-pt-1.8.2.tsv`. Traduzir para outro idioma = **um arquivo** `Translations/<Idioma>.json`
  (UI + `hc_cfg_*` + `hc_cat_*`) + `Speech/<Idioma>.json`.
- **Tipos alternativos (dverger: Mago, Mago de Fogo, Mago de Gelo e Suporte) são DE COMBATE, por desenho:** a roda já
  esconde os trabalhos (gate `_isDverger`, igual ao upstream) e desde a 1.8.3 (`fix/dverger-combate`) o **chat/voz** e a
  **tecla de comando** também são recusados (`CompanionSetup.IsWorkMode`/`ApplyActionMode` → `false`, recusa em
  `CompanionOrders` e nas duas escritas diretas de `CompanionInteract`), com a fala **"Sou de combate, não faço esse
  trabalho."**; o `ChangeTier` para um tipo de combate volta ao Follow. **Limitação que continua:** o painel esconde os
  espaços de comida por tipo e as peças de armadura ficam na mochila em vez de equipadas. O inventário **não** foi
  confirmado com defeito em jogo (o teste do jogador mostrou que o que ele coloca persiste); a suspeita das duas
  mochilas fica registrada como **hipótese** na seção *Backlog da revisão da 1.8.3* do roadmap. **Nunca** trocar
  `Container.m_inventory` no `Awake` sem mundo de teste (tentativa de 1.8.3 revertida em `e8247ff`).
- **Fase M1 da multilinguagem (branch `feat/idiomas-auto`) — `Locale` + `auto` nas configs + o bug do
  `_loadedLanguage`:** `Core/CompanionLocale.cs` (novo) é a **tabela única** idioma-do-jogo → `{Name, WhisperCode,
  KokoroCode, Voices (padrão primeiro), DefaultVoice, SpeechPrompt}` para **PT-BR, EN, ES e FR**, mais `HasVoice`,
  `ResolveVoice`, `ResolveKokoro`, `ResolveWhisper`, `ResolvePrompt`, `VoiceName`/`VoiceGender` (mapa `sid → nome` do
  export do sherpa; o gênero é a 2ª letra do nome). Idioma sem voz no Kokoro (de, ru, pl, tr, ko, sv, uk) cai no
  **inglês** com uma linha no log. O perfil é **cacheado** (`Current`), resolvido **na main thread** (a língua vem do
  `Localization.SetupLanguage` via `CompanionLocale.NotifyLanguage`, porque ler `GetSelectedLanguage()` dentro do
  postfix é o que recursava no hotfix da 1.8.3) e **invalidado** no postfix (`Invalidate()`) e no `OnSceneLoaded`;
  sem `Localization` (o `Awake` do plugin) responde inglês **sem** cachear. **Config:** `[AI] VoiceLanguage` e
  `[AI] TtsLanguage` passam a nascer **`auto`**, `[AI] TtsVoice` **`0` (= automático)**, `[AI] VoiceInitialPrompt`
  **vazio (= vocabulário do idioma; `none` desliga)** — valores explícitos do jogador **sempre ganham** e **não há
  migração** (um `.cfg` com `pt`/`pt-br`/`42` continua igual). `CompanionTts.DefaultVoice` passou a ser o padrão do
  idioma, `PortugueseVoices`/`PortugueseVoiceCount`/`PortugueseVoiceAt` **saíram** (os chamadores leem
  `CompanionLocale.Current.Voices`), `CompanionTts.VoiceName` virou `CompanionLocale.VoiceName`, e a **voz de um
  companheiro que não pertence ao idioma ativo não é usada** (`CompanionProfile.GetVoice` cai na padrão do idioma,
  log Debug uma vez por id, **sem reescrever o ZDO** — voltar o jogo para aquele idioma restaura a escolha).
  `CompanionProfile.VoiceIds/VoiceLabels` seguem o idioma e o novo `LabelOf(voice)` monta `"{nome} ({gênero})"` com
  `hc_ai_voice_label`/`_female`/`_male` (as `hc_ai_voice_42/43/44` continuam tendo prioridade) — sem UI nova (o
  dropdown com nomes é a M2) e sem traduções novas (M3). **`SpeechConfig` (achado #2 da auditoria):** o
  `_loadedLanguage` só é gravado quando o **arquivo daquele idioma carrega**; nos fallbacks (`speech.json`/defaults)
  fica vazio e o `Instance` **tenta de novo com portão de 30 s** (`_lastAttemptUtc`, `RetrySeconds`) em vez de ficar
  preso até reiniciar — e `_usingFallback` evita reler o mesmo arquivo de 30 em 30 s. Logs:
  `[Locale] English -> en / en-us | 6 vozes, padrao 3 (af_heart)`, a queda para inglês, e (uma vez, já no mundo)
  `[TTS] idioma efetivo: pt-br (auto) | voz 42 (auto, pf_dora)` + a lista de vozes do idioma e
  `[STT] idioma efetivo: pt (auto, o do jogo) | prompt inicial: N caracteres (...)`; no carregamento do modelo,
  `idioma "pt-br" ... voz 42 (pf_dora)`. Verificado fora do jogo (harness com o `CompanionLocale.cs` real):
  **84 casos, ALL CHECKS PASSED**. Detalhes: `docs/plan-multilinguagem.md` (M1).
- **Fase M2 da multilinguagem (branch `feat/idiomas-auto`) — o dropdown com nomes, a lista rolável e o F8 com
  listas:** `UI/ModDropdown.cs` (novo) extraiu o dropdown do painel `I` para uso dos **dois** painéis:
  `ModDropdownHost` (o overlay com `Canvas` `overrideSorting` em `ModUiLayers.AiPanelOverlay` + o **`ListCatcher`**
  como primeiro filho, ativo só com uma lista aberta) e `ModDropdownList` (a lista: fundo que engole o clique da
  borda, viewport com `RectMask2D`, `ScrollRect` vertical com roda/arrasto e **geometria medida na abertura** -
  abre para baixo, vira para cima quando o lado preferido tem menos espaço e, se não couber em nenhum, **rola**; o
  item selecionado é trazido para a janela). Os itens só são recriados quando a **quantidade** muda. No painel `I`
  (M2a) o campo continua o mesmo `CreateButton` (posições/cores/tamanhos das linhas intactos) e a lista virou o
  widget: as **7 opções** de voz do inglês (6 + "Seguir o padrão") cabem no painel (antes eram 630 px contra 516
  de altura útil e as últimas ficavam fora). O status da voz (`VoiceStatusLabel`/`CurrentVoiceLabel`) passou a dizer o
  **efetivo**: `Automático (pt-BR · pf_dora)` quando nada foi escolhido (ou quando o companheiro herda, com
  `(padrão)` no fim), `pf_dora (feminina)` quando a escolha é do idioma ativo e
  `pf_dora (pt-BR) - fora do idioma, usando af_heart` quando a voz guardada no ZDO é de **outro** idioma (o ZDO não é
  tocado). `CompanionProfile.LanguageTag()`/`LanguageTagOf(voice)` (display: `pt-br` → `pt-BR`, família pela 1ª letra
  do nome) e as chaves novas `hc_cfg_auto`, `hc_cfg_auto_value`, `hc_ai_voice_out_of_language`. No **F8** (M2b),
  `BuildChoiceRow` (um tipo de linha novo, casado por `(Section, Key)` e **só** para `AI/TtsVoice` (int),
  `AI/TtsLanguage` (string) e `AI/VoiceLanguage` (string)) mostra o campo com o valor em uso - `Automático (en-us)`,
  `Automático (af_heart · en-US)`, `pf_dora (feminina)`, `en-gb` - e abre a **mesma** `ModDropdownList` (uma
  instância por painel, reposicionada a cada abertura) com as opções pedidas (`auto` + as vozes do idioma ativo /
  `en-us,en-gb,pt-br,es,fr,it,ja,zh,hi` / `pt,en,es,fr,it,de,ru,pl,tr,ko,ja,zh,hi,uk,sv`); o "Padrão" volta para
  `auto`/`0`, nada é gravado por abrir o painel e o **gamepad** (esq/dir) percorre as opções (`_choiceAdjust`, em vez
  de mexer no número por baixo). A lista fecha ao rolar o conteúdo, ao trocar de categoria, no `Esc` (antes de fechar
  o painel) e no `Hide`. `ModUiAudit`: `DropdownOverlay/ChoiceList` e `Overlay/LlmProviderList` entraram na
  allow-list, e a regra "dentro de um `ScrollRect`" deixou de aceitar **tudo** abaixo dele (agora os controles são
  classificados pelo `Selectable` primeiro e só a superfície de rolagem - o GameObject do `ScrollRect` e o viewport -
  é exceção), senão as opções da lista esconderiam um alvo decorativo novo. Detalhes: `docs/plan-multilinguagem.md` (M2).
- **Correções do teste em jogo da M2 (branch `feat/idiomas-auto`) — a migração dos padrões antigos, os painéis que
  ficavam no idioma da construção e o log honesto da voz:** (1) **migração** em `ModConfig.ApplyMultilanguageDefaults`
  (junto das 3.3b/3.3c, com a regra "só o padrão antigo EXATO"): `VoiceLanguage = pt` → `auto`, `TtsLanguage = pt-br`
  → `auto`, `TtsVoice = 42` → `0`, `VoiceInitialPrompt` = o texto antigo (167 caracteres, `OldVoicePrompt`) → `""`;
  uma **linha** de log nomeando o que mudou (`[AI] Migracao da multilinguagem: VoiceLanguage/TtsLanguage/TtsVoice/
  VoiceInitialPrompt estavam nos padroes antigos e agora sao automaticos (escolhas suas nao sao tocadas).`),
  idempotente (auto/0/vazio não casam) e **sem save próprio** (o `Init` já segura os saves por `Bind` e grava uma vez
  no fim). Ninguém perde a voz: `42`/`pt-br`/`pt` são justamente a voz e os códigos do PT-BR, então no jogo em
  português o companheiro continua com a `pf_dora`. (2) **o bug de verdade da troca de idioma:**
  `ModLocalization.LanguageVersion` (int, incrementado **só** quando `InjectTranslations` vê um idioma diferente do
  último injetado — a mesma decisão que a injeção já tomava; um `Localization` novo com o mesmo idioma não conta),
  `ConfigPanel`/`CompanionAiPanel` guardam a versão da construção (`_builtLanguageVersion`) e no **`Show()`**
  reconstroem o painel quando ela mudou (`DestroyBuiltControls()` + `Build()`, com o `Canvas`/`CanvasScaler`/
  `GraphicRaycaster` do GameObject reaproveitados porque a Unity só permite um `Canvas` por objeto e o `Destroy` é
  adiado para o fim do frame; `Hide`/`Destroy` fazem o resto, os sprites estáticos continuam no dono), preservando a
  **categoria do F8** e a **aba do companheiro** no painel `I`; o `CompanionHudPanel` re-resolve `_hpFormat`/
  `_staFormat` quando a versão muda (achado #1 da auditoria — é o único texto estático do HUD). (3) **log honesto:**
  `CompanionTts.VoiceLogText()` diz `voz 42 (pf_dora)` para escolha válida, `voz 3 (auto, af_heart)` para o automático
  e `voz 3 (a 42 (pf_dora) e de outro idioma; usando a padrao do idioma)` quando a escolha foi substituída (antes tudo
  virava "auto"); no F8, um `TtsVoice` de outro idioma aparece como `pf_dora (pt-BR) - usando af_heart` (chave nova e
  curta `hc_cfg_voice_out_of_language` = `{0} ({1}) - usando {2}`, porque a `hc_ai_voice_out_of_language` completa não
  cabe no campo de ~220 px - ela é o que o rodapé do painel `I` mostra).
  Verificado contra o `.cfg` real do jogador: as quatro linhas casam exatamente com os padrões antigos.
- **Fase M3 da multilinguagem (branch `feat/idiomas-auto`) — ES e FR de verdade, presets por idioma e a auditoria
  por idioma:** (1) **`Translations/Spanish.json` e `Translations/French.json`** — as **737** chaves do PT-BR, na
  **mesma ordem** (mesma indentação/quebras, UTF-8 **sem BOM**; o PT usa CRLF e as falas LF), com os placeholders
  `{0}`/`{1}`/`{2}` preservados. Gerados por **`tools/gen_translations.py`** (novo), que lê o PT e troca só os valores
  a partir de um mapa PT→idioma (chave = valor do PT sem acento, então os 35 valores repetidos viram uma entrada só);
  o mesmo script reescreve as falas linha a linha, então a estrutura é sempre a do PT. **`Speech/Spanish.json` e
  `Speech/French.json`** — as 15 categorias / **186** linhas (1:1 com o PT). Terminologia do jogo respeitada
  (madera/bois, piedra/pierre, mineral/minerai, plantar/planter...) e tratamento informal (tú/tu). (2) **Presets de
  personalidade por idioma** (`AI/CompanionPersona.cs`): `Preset.Text` e `CompanionPersona.DefaultText` passaram de
  texto PT fixo a `Pick(pt, en, es, fr)`, que escolhe pelo **`CompanionLocale.Current.KokoroCode`** (a mesma tabela do
  TTS/STT: `pt-br`, `es`, `fr`, e **inglês** para todo o resto - alemão, russo...). O texto é resolvido na hora do
  prompt (1× por mensagem), nunca por frame, e o **texto livre do jogador (`custom`) nunca é traduzido**. (3)
  **`tools/audit_translations.py --lang <Arquivo>`**: valida uma tradução contra o PT — chaves ausentes/sobrando/
  vazias, **placeholders divergentes**, valores **iguais ao PT** (só os legitimamente iguais passam: nome de voz,
  string só com placeholders/códigos e a lista documentada `IDENTICAL_OK` — palavra que o idioma escreve igual, token
  técnico, nome próprio) e **cara de inglês** (`EN_HINTS`, sem as palavras que o ES/FR também usam, como "panel"),
  mais as falas (categorias/contagem/linhas iguais ao PT). O modo sem argumento continua idêntico (267 lacunas
  pré-existentes do PT, as `hc_cfg_*` que só existem no JSON). Resultado: **ES 0 e FR 0**.
- **Correção do teste em francês da M3 (branch `feat/idiomas-auto`) — o contrato do LLM no idioma do jogador + a
  rede de segurança da coleta:** o jogador, com o jogo em **francês**, disse "maria ramasse 10 bois" e o modelo
  devolveu `pickup` ("Je prends les bois.") - o companheiro foi catar item no chão. **Causa:** o
  `CompanionChatAI.Contract` era **100% PT-BR** (regras e exemplos), então fora do PT o modelo não tinha exemplo de
  ordem de coleta. **Correção em duas camadas:** (1) **contrato por idioma** — `Contract` passou a ser uma propriedade
  que escolhe entre `ContractPt`/`ContractEn`/`ContractEs`/`ContractFr` (todos `const`, custo zero por mensagem) pelo
  **`CompanionLocale.Current.KokoroCode`** (mesma tabela do TTS/presets; idioma sem contrato → **inglês**), com
  **regras e exemplos traduzidos** e os **tokens intactos** (chaves JSON e as 30 ações de `ValidActions`, que é o
  `enum` do schema). Todas as correções históricas continuam nos quatro (o par `action`/`resource`, `target`
  obrigatório em attack/equip/drop, `drop`×`pickup`×`nopickup`, o exemplo com `CompanionExampleName`) e cada idioma
  ganhou o **par que confunde** (coletar com número × pegar do chão) e uma regra nova: *número + verbo de coleta +
  material = ordem de coleta*. O `Header` do prompt (quem é o companheiro, personalidade, aviso da fala) e a linha
  `CompanionNames.PromptLine()` ("Companheiros seus: …") também seguem o idioma. (2) **rede de segurança**
  (`AI/GatherIntent.cs`, novo e **puro**): com o modelo devolvendo `pickup` (no `CompanionOrders.Run`) ou `none` (no
  `CompanionChatAI.HandleResult`), se as palavras do jogador têm **recurso conhecido** (PT/EN/ES/FR) **+ número** e
  **sem** marcador de chão/itens (`chão`, `suelo`, `ground`, `par terre`, `itens`…), a ação vira a coleta daquele
  recurso e a quantidade sai do texto — uma linha de log (`[IA] O modelo devolveu "pickup" mas o jogador pediu para
  coletar madeira - usei "wood" (10).`). Guardas: `nopickup` nunca é lido, `target`/`equip`/`drop` não são tocados,
  o caso legítimo (com marcador de chão) continua `pickup`, e no lado `none` ainda se exige um verbo de coleta (para
  "você tem 10 madeiras?" não virar ordem). (3) **`OrderItems`** ganhou os apelidos **ES/FR** (madera/bois,
  piedra/pierre, hierro/fer, frambuesa/framboise, champinhon/champignon…, 180 apelidos) para o teto da ordem e o
  `GatherAction`. Verificado com um harness que carrega a **`Companions.dll` real** (reflexão): contrato por idioma
  (7734/7720/7698/8136 chars; alemão → EN), 38 casos de mensagem→ação/quantidade nos 4 idiomas, as regras da 3.1 do
  `PickupIntent` e o mapa de apelidos — **272 casos, ALL CHECKS PASSED**. Medido com o **`qwen2.5-3b-instruct`**
  (LM Studio, temperatura 0,1, mesmo schema/contrato do mod): **14/14 nos quatro idiomas** (56 casos, incluindo
  `ramasse 10 bois` -> `wood` x10 e `ramasse le bois par terre` -> `pickup`); a medição rendeu um exemplo de `drop`
  a mais no EN e no ES.
- **Italiano no mod (branch `feat/idiomas-auto`) — quinto idioma completo, o primeiro fora da leva M1–M3:**
  (1) **`CompanionLocale`**: perfil `Italiano` (`WhisperCode`/`KokoroCode` = `it`, vozes **35 `if_sara` (f)** e
  **36 `im_nicola` (m)**, padrão 35, `SpeechPrompt` em italiano) e `ByGameLanguage("Italian")`; os idiomas sem perfil
  continuam caindo no inglês. Nada mudou nas tabelas PT/EN/ES/FR. (2) **`CompanionChatAI`**: `ContractIt` (const, mesma
  estrutura do `ContractFr`, **tokens e as 30 ações intactos**), `case "it"` no `Contract`/`Header` e o italiano no
  `CompanionNames.PromptLine()` ("I tuoi compagni: …"); todas as correções históricas estão nele (o par
  `action`/`resource`, `target` obrigatório, `drop`×`pickup`×`nopickup`, o exemplo com `CompanionExampleName`, o par
  coleta-com-número × pegar-do-chão e a regra *número + verbo de coleta + material = coleta*). (3) **`CompanionPersona`**:
  `Pick(pt, en, es, fr, it)` + `case "it"` e os 5 textos (padrão + 4 presets) em italiano; alemão/russo seguem no EN e o
  texto livre do jogador **nunca** é traduzido. (4) **Traduções**: `Translations/Italian.json` (737 chaves, mesma ordem
  do PT) e `Speech/Italian.json` (15 categorias / 186 linhas), gerados pelo `tools/gen_translations.py` (mapa `IT_UI`/
  `IT_SPEECH`; o script agora escreve os **três** idiomas de uma vez, e ES/FR saem byte a byte idênticos) e validados
  pelo `tools/audit_translations.py --lang Italian` → **0 lacunas** (0 ausentes/sobrando/vazias, 0 placeholders
  divergentes, 0 iguais ao PT indevidos, 0 cara de inglês; as palavras que o italiano escreve igual ao PT entraram na
  `IDENTICAL_OK` do italiano e `Skål!` na `SPEECH_IDENTICAL_OK`; o `all` saiu do `EN_HINTS`, que casava
  "all'asciutto"). (5) **Ordens**: `OrderItems` com apelidos IT (legna/legno/alberi, pietra/pietre/sasso, rame/stagno/
  argento/ferro, lamponi/mirtilli/funghi…, 224 apelidos no total), `GatherIntent` com as tabelas IT (materiais, verbos
  `raccogli`/`raccatta`/`prendi`/`estrai`, marcadores de chão `a_terra`/`per_terra`/`in_terra`/`da_terra` e
  `oggetto`/`oggetti`) e `PickupIntent` com os verbos IT de largar/recolher e de parar (`fermati`, `smetti di
  raccogliere`), mantendo a lógica (nada novo). As listas de idiomas do F8 (`TtsLanguage`/`VoiceLanguage`) **já tinham**
  `it` desde a M2. Verificado com a `Companions.dll` real (reflexão): locale IT/alemão, `ContractIt` (7.986 chars,
  30 ações, todos os tokens), persona IT, 14 casos do `GatherIntent`, 7 do `PickupIntent`, 19 apelidos IT e o nome lido
  das palavras ("Maria, raccogli 5 pietre") — **107 casos, ALL CHECKS PASSED**.
- **Fase M5 (1ª metade) — a rede determinística das quatro famílias que o 3B erra (branch `feat/idiomas-auto`):** a
  medição das **30 ações × 5 idiomas** (150 casos, `qwen2.5-3b-instruct`, temperatura 0,1, 2 rodadas) fechou em
  **29/30 PT, 27/30 EN, 29/30 ES, 29/30 FR e 29/30 IT**, e a lição foi a **oposta** da 3.1: **acrescentar exemplos ao
  contrato piora** o modelo pequeno (o PT regrediu e o ES `vaga` passou a vir como `nowander`), então **não se
  acrescentam mais exemplos** — o caminho é a **rede por palavras**, no mesmo espírito da `PickupIntent` (3.1 §8.2) e
  da `GatherIntent` (M3). Novo **`AI/RadialIntent.cs`** (**puro**, 1× por mensagem, sem Unity/config/mundo):
  `Correct(action, message, out reason)` devolve a ação corrigida e a frase do log, com **casamento por palavra
  inteira entre separadores** (sem substring), **mensagem curta** (`MaxWords = 10`) e **nunca com `?`** (pergunta não
  é ordem), e **quatro portões de ação** (cada regra só olha as ações em que o modelo erra de verdade):
  (1) **postura** — palavra de postura (`postura|stance|posture|posizione|posicion`) + a qualidade
  (`equilibrad|balanced|equilibree|equilibrata|balancead` → `stance_balanced`; `distanc|ranged|distanza|distancia` →
  `stance_ranged`; `corpo a corpo|melee|corps à corps|cuerpo a cuerpo` → `stance_melee`;
  `passiv|passive|pasiva|defensiv|defensive|defensif` → `stance_passive`), valendo para qualquer ação **menos as
  precisas** (`attack`/`equip`/`drop`/`inventory*`/as de coleta, que carregam alvo ou recurso);
  (2) **vagar** — palavra de vagar (`vagar|vagueia|wander|errer|vagare|vagabond`…) + verbo de parar ⇒ `nowander`,
  senão ⇒ `wander`, para `stop|wander|nowander|none`; o verbo de parar tem que estar **no início** ("para" como
  preposição de "joga a pedra para o chão" **não** dispara) **ou** ser palavra que só pode ser de parada (`pare`,
  `stop`, `smetti`, `deja`, `fermati`…); (3) **comandos** — palavra de comando
  (`comando|order|ordres|ordini|orden`) + ignorar/negar ⇒ `nocommand` (a negação ganha do verbo de aceitar) ou
  aceitar/obedecer ⇒ `command`, para `none|stop|command|nocommand`; (4) **trabalhos** —
  `fundi|fundicao|smelt|fourneau|fonderia|furnace|forno|horno` ⇒ `smelt`, `cozinh|kitchen|cuisine|cucina|cocina|cook` ⇒
  `cook`, `cac|hunt|chasse|cacciare|cazar` ⇒ `hunt`, só para `repair|farm|wander|none` e com **duas guardas**: uma
  criatura nomeada (`greydwarf`, `troll`, `javali`…) mantém o `attack` e um verbo de reparo (`conserta`, `repair`,
  `ripara`…) mantém o `repair`. **Onde:** `CompanionOrders.Run`, antes de despachar (depois das redes do
  recurso/pickup/coleta e antes de `outcome.LabelKey`), e no lado `none` do `CompanionChatAI.HandleResult` (depois da
  `GatherIntent`, só com a ação ainda `none`). Uma linha de log por correção
  (`[IA] O modelo devolveu "stop" mas o jogador pediu para parar de vagar - usei "nowander".`) e os rótulos do chat
  já existiam (`hc_ai_order_stance_*`, `_nowander`, `_nocommand`, `_smelt`, `_cook`, `_hunt`). **Contrato, TTS/STT,
  painéis, dverger, cache e camadas de UI intocados.** Verificado fora do jogo com a `Companions.dll` real
  (reflexão): as 7 falhas medidas + os casos de não-regressão dos 5 idiomas + as guardas — **95 casos, ALL CHECKS
  PASSED**.
- **Pendências conhecidas (abertas):** a **voz não passa pelo
  mixer do jogo** (por isso ignora os sliders de volume do Valheim e só obedece `[AI] TtsVolume`); o **sync com o
  upstream** (estamos **1 release atrás**: base `1.5.4`, `main` na `1.5.5` — ver a seção *Upstream e licença*);
  a **sobreposição do nome do companheiro no centro do radial** (o nome do mundo aparece por trás do centro
  transparente da roda — cosmético, visto nas capturas da 1.7.0); o **risco médio do roster com N companheiros**; e a
  **posse do ZDO no spawn em multiplayer** (revisar quando houver teste em servidor dedicado). O
  **release/empacotamento da 1.8.3 está fechado** (25/10/2026): tag `v1.8.3` no `c023ca1`, GitHub Release
  `releases/tag/v1.8.3` com os dois zips — `AI_Companions-1.8.3.zip` (385.866.837 bytes, md5 `340d320d…`) e
  `…-sem-modelos.zip` (29.846.898 bytes, md5 `0372b807…`), gerados de `pkg183\stage` sem `speech.json` (runtime) e sem
  os marcadores do Vortex; falta só o upload manual no Nexus (`Temp\opencode\nexus\publicar-183.md`). A
  branch `origin/claude/relaxed-archimedes-ecoh6h` **não** é pendência: é o **WIP do autor do upstream** e **não**
  deve ser mergeada nem usada como referência. Detalhes na subseção *Pendências conhecidas* do `docs/roadmap.md`.

## Nossas docs

- **Idioma (a partir de 26/10/2026): público em inglês, interno em PT-BR.** O **público** — `README.md`,
  `package/*`, a wiki (**padrão EN** + espelhos `-pt`/`-es`/`-fr`/`-it`) e as **release notes** — é em **EN**; os
  **internos** — este arquivo, `roadmap`, `plan-*`, `vision`, `audit`, `references`, `docs/re/*` — são em **PT-BR**
  (é a ferramenta de trabalho da equipe). O `package/LEIA-ME.txt` fica em **PT**; **comentários de código** em inglês
  e **logs** em PT. Plano/fases: `docs/plan-i18n-docs.md`.

- `docs/wiki/` — **a fonte da GitHub Wiki** (o guia de instalação/uso para quem baixa o mod): `Home.md`,
  `Requisitos.md`, `Instalacao.md`, `LLM-no-LM-Studio.md`, `Voz.md`, `Comandos.md`, `Configuracao.md`,
  `Problemas-comuns.md`, `Como-funciona.md`, `Creditos-e-licenca.md`, `_Sidebar.md`, `_Footer.md` e `images/`.
  A wiki publicada vive em **repositório git separado** (`https://github.com/julianu37/AI-Companions.wiki.git`,
  branch `master`): edite aqui e publique com `git clone <url>.wiki.git` + copiar os `.md` e a pasta `images/` +
  commit/push. (O repositório da wiki só existe depois da 1ª página salva pelo site — não há API para isso.)
- `docs/vision.md` — a visão/escopo (companheiro como membro da equipe) e o que falta.
- `docs/roadmap.md` — fases e status.
- `docs/plan-fases.md` — plano das fases com mapa do upstream.
- `docs/plan-multilinguagem.md` — plano da multilinguagem (EN/PT/ES/FR): `Locale`, config `auto`, catálogo de
  vozes por idioma e as fases M1–M5.
- `docs/audit-vs-offline-companions.md` — auditoria do nosso mod anterior vs o upstream.
- `docs/re/` — engenharia reversa do Valheim 1.0 (APIs, diff de versão).
- `docs/re/stt-whisper-net-spike.md` — o spike do STT local (Whisper.net in-process) e o loader nativo.
- `docs/re/stt-voz-pipeline.md` — o pipeline de voz da Fase 3.3 (push-to-talk → STT → ordens) e a receita de teste.
- `docs/re/tts-kokoro-sherpa-onnx.md` — a Fase 3.4 (Kokoro-82M via sherpa-onnx: carregamento dos nativos,
  síntese por frase, cache, playback 3D) e a receita de teste.
- `docs/references.md` — fontes de terceiros e licenças.

## Skills (carregue antes de escrever/revisar código)

- `bepinex` · `jotunn` · `valheim-reverse-engineering` · `valheim-ai-backend` · `valheim-assets` · `kokoro`

## Regras de trabalho

1. **Carregue a skill** da dependência envolvida antes de codar ou revisar.
2. **Nenhum arquivo fica editado sem commit.** Toda alteração — código, doc, config, a **fonte** da wiki
   (`docs/wiki/`), o `.tsv` de referência — é **commitada na branch correspondente no mesmo ciclo** em que foi feita.
   A árvore de trabalho termina o ciclo **limpa** (`git status` vazio). Nunca deixar mudanças soltas “para commitar
   depois”; se o trabalho estiver pela metade, o commit é `wip(...)` na branch, não uma árvore suja.
3. **Não quebre o comportamento do upstream** — nossas mudanças são: **tradução PT-BR** + **camada de IA**.
   Mudanças de lógica só com motivo claro e documentado.
4. **Tradução**: use o sistema de localização do próprio mod (`Translations/*.json`, `ModLocalization.cs`) —
   UI/hover/HUD — e `Translations/speech/<Idioma>.json` para as falas (`SpeechConfig`).
   Não hardcode PT-BR no código quando houver token de localização.
5. **Sem segredos** hardcoded (chaves de LLM/API via `ConfigEntry` ou variável de ambiente).
6. **Código vai em branch, não direto na `main`** (combinado em 22/10/2026): `feat/<tema>` ou `fix/<tema>` →
   **o jogador testa em jogo** → merge na `main`. **Um commit por hipótese** (não junte coisas independentes num
   commit só). Docs e release podem ir direto na `main`.
7. **Sempre commitar e dar push** ao terminar (`git add -A && git commit && git push`), mensagem no padrão
   `tipo(escopo): resumo`.
8. Atualize a documentação correspondente (`README.md`, `package/CHANGELOG.md`, `docs/roadmap.md`,
   `AGENTS.md`) quando algo mudar.
9. **Idioma da documentação:** o **público é em inglês** (`README.md`, `package/*`, a wiki com o **padrão EN** e os
   espelhos `-pt`/`-es`/`-fr`/`-it`) e o **interno é em PT-BR** (`AGENTS.md`, `docs/roadmap.md`, `docs/plan-*.md`,
   `docs/vision.md`, `docs/audit-*.md`, `docs/references.md`, `docs/re/*`) — é a nossa ferramenta de trabalho. As
   **release notes** (`docs/release-notes-*.md`) são **EN** (viram o corpo da release no GitHub) e o
   `package/LEIA-ME.txt` fica em **PT**. **Comentários de código continuam em inglês** (convenção do repositório) e
   as **mensagens de log em PT-BR**. O mod (UI/falas) segue o idioma do jogo. Plano: `docs/plan-i18n-docs.md`.

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
