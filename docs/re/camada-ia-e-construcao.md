# Camada de IA e construção — histórico detalhado

> Extraído do `AGENTS.md` em **03/11/2026**, sem mudança de conteúdo (a seção *Camada de IA* inteira).
> O **estado atual resumido** e as **pendências abertas** ficam no `AGENTS.md`; aqui está o detalhe por fase,
> as medições, as receitas de teste e o histórico da construção autônoma (Trilha E), da navegação e da
> multilinguagem. Fonte primária do comportamento: o código + `package/CHANGELOG.md` + `docs/roadmap.md`.

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
  (`TryGetTts`, lazy) e mantido; `Prewarm()` carrega em segundo plano quando você entra no mundo. **1.9.2: até o
  idioma mudar** — o `Lang` do Kokoro escolhe o **fonemizador** no load, então trocar o idioma do jogo (ou
  `[AI] TtsLanguage`) com o mundo aberto deixava a voz `pf_dora` lendo português com fonemas ingleses (soava como
  inglês; o log dizia `idioma pt-br` e mentia, porque `_loadedLanguage` não existia). Agora o worker compara
  `Language` com `_loadedLanguage` antes de cada fala, **libera e recarrega** o modelo (só ele toca no nativo) e
  **esvazia o cache** (`ClearCache`, as amostras eram do fonemizador antigo); `NoticeLanguageChange` (postfix do
  `SetupLanguage` + `SettingChanged` do `[AI] TtsLanguage`) só levanta o flag e acorda a thread, então o postfix
  continua sem tocar a camada de plataforma. Um código **fora dos 5 do mod** não recarrega nada (o modelo que
  funciona fica, com um aviso): medido fora do jogo com o modelo empacotado, **`en-gb` e `fr-fr` carregam mas
  `Generate` estoura `NullReferenceException`** — por isso a lista do F8 tem só `pt-br`/`en-us`/`es`/`fr`/`it`.
  A síntese roda
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
  - **Correções da auditoria da 1.9.2** (rodada antes do teste; detalhes em `docs/audit-upstream-1.9.2.md`): a rede
    da porta **não bastava** — com `StructuredOutput` (padrão) o schema só aceita o enum das 35 ações, então o modelo
    devolve `inventory_close` e o `IsDoorWord`, que lê a **ação**, nunca dispara: o companheiro fechava a janela do
    jogador. O novo **`CompanionOrders.IsDoorRequest`** lê as **palavras** (porta + verbo de abrir/fechar e **sem**
    palavra de mochila — "abre o inventário e fecha a porta" continua inventário) e roda no `Run` **e** no lado `none`
    do `HandleResult`. O novo **`AI/InteractionIntent`** (puro, 1×/mensagem, palavra inteira entre separadores, ≤10
    palavras, `?` nunca é ordem, negação/"para de…" recusam) cobre `hive`/`board`/`sit`/`deposit` no lado `none` e no
    `Run` para `drop`/`inventory*` — antes 7/7 ordens inequívocas caíam em "não entendi". As 5 entraram em
    `RadialIntent.PreciseActions` (uma postura na mesma frase não engole mais a ordem); `senta` ganhou
    `fromOrder` (já sentado **no mesmo** fogo continua sentado, fogo diferente levanta e vai, a tecla continua toggle);
    `deposit` conta **itens** (não pilhas), usa as regras dos jobs (`MHR_Chests.Usable` + `PrivateArea`, nada de túmulo/
    carrinho/navio/baú privado de outro jogador) e a posse do baú; `Pointed<T>`/`PointedShip` varrem **todos** os hits
    do raio (como a tecla) e as 4 escritas diretas de `HC_ActionMode` passaram pela fila da 3.19. Nenhuma chave nova.
  - **Medição das 5 ações novas (M5, 2ª metade, 31/10/2026):** `qwen2.5-3b-instruct` no LM Studio (temperatura 0,1,
    2 rodadas, o **contrato e o schema reais** lidos da DLL por reflexão): **50/50 (100%)** nas 5 ações × 5 idiomas —
    e as redes por palavras também 50/50. Leitura: as frases **canônicas** o 3B acerta sozinho (o `enum` do schema
    estreita a saída); as redes continuam valendo para as **variantes** ("sobe no barco", "põe isso no baú", "esvazia
    a colmeia aí"), que é onde ele erra. Harness: 50 requisições (~3,2 s cada), fora do repositório.
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
  de 28..34 para **1701..1709** (`src/Companions/UI/ModUiLayers.cs`, **fonte única**: `FarmZonePlacer` 1701,
  `HomeZonePanel` 1702, `CompanionRadialMenu` 1703, `StarterCompanionPanel` 1704, `FarmZoneCropPicker` 1705,
  `ConfigPanel` 1706, `CompanionAiPanel` 1707, overlay do dropdown 1708, `CompanionAppearancePanel` 1709),
  **mantendo a ordem relativa** entre eles. A
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
  modal; é o que o `ConfigRoot` do F8 já fazia), e as **barras de fundo das listas do dropdown**
  (`Overlay/TierList`, `Overlay/CompPersonalityList`, `Overlay/CompVoiceList`, `Overlay/LlmProviderList` e
  `DropdownOverlay/ChoiceList`) **continuam alvos de propósito** (o clique na borda de 2 px da lista tem que morrer nela, senão
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
  **inglês** com uma linha no log. **Essa é a regra e ela é verificada no DLL:** `tools/locale-fallback-check.ps1`
  confere os 5 perfis (`pt-br`/42, `en-us`/3, `es`/28, `fr`/30, `it`/35) e **17 idiomas do jogo sem perfil** — todos
  têm de cair em `en-us`/`en`/voz 3, e `ResolveKokoro("auto")`/`ResolveWhisper("auto")` têm de responder o inglês
  quando não há `Localization` (é o caso do `Awake` do plugin). Rode-o antes de release que mexa em idioma. O perfil é
  **cacheado** (`Current`), resolvido **na main thread** (a língua vem do
  `Localization.SetupLanguage` via `CompanionLocale.NotifyLanguage`, porque ler `GetSelectedLanguage()` dentro do
  postfix é o que recursava no hotfix da 1.8.3) e **invalidado** no postfix (`Invalidate()`) e no `OnSceneLoaded`;
  sem `Localization` (o `Awake` do plugin) responde inglês **sem** cachear. **Config:** `[AI] VoiceLanguage` e
  `[AI] TtsLanguage` passam a nascer **`auto`**, `[AI] TtsVoice` **`0` (= automático)**, `[AI] VoiceInitialPrompt`
  **vazio (= vocabulário do idioma; `none` desliga)** — valores explícitos do jogador **sempre ganham** e **não há
  migração** (um `.cfg` com `pt`/`pt-br`/`42` continua igual). **1.9.2:** o F8 oferece **só os 5 idiomas** nos dois
  (`TtsLanguage` e `VoiceLanguage`); um código escrito à mão no `.cfg` continua valendo (o Whisper é multi-lingue e o
  Kokoro aceita alguns), mas o prompt e o contrato ficam em inglês e a voz não tem esse idioma — o log avisa uma vez. `CompanionTts.DefaultVoice` passou a ser o padrão do
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
  mais as falas (categorias/contagem/linhas iguais ao PT). O modo sem argumento continua idêntico, agora com **369
  lacunas** (03/11/2026): **362** são as `hc_cfg_*`/`hc_cat_*` que só existem no JSON (por desenho) e **7** são
  literais em inglês em linhas de UI, dos quais 3 são o `why` que só vai para o log (`MHR_Build`) e 2 são pré-existentes
  (`KeyBindListener`/`PadBindListener`). Resultado: **ES 0, FR 0 e IT 0**.
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
- **A ordem das peças dentro do andar (ideia do jogador, 03/11/2026) — implementada:** o companheiro **atravessava a obra de
  um lado para o outro** porque, dentro do grupo (andar → classe → altura), a ordem era a do arquivo `.blueprint` (a ordem em
  que o autor desenhou as peças, não um caminho). Agora o `BuildBlueprintParser.NearestWithin` reordena **cada run** que
  compartilha (flag de porta, `StoreyOf`, `ClassOf`, `Py` ±0,01) por um caminho **guloso de vizinho mais próximo**, começando
  onde o run anterior terminou; **nada muda de grupo** e porta/portão continuam por último, e o guloso **nunca fica pior que o
  arquivo** (o `PathLength` compara e mantém o menor). O `BuildBlueprints.Load` ganhou **cache por caminho +
  `LastWriteTimeUtc`** porque o `CheckPending` o chamava **por frame** enquanto a obra esperava material. O parser é puro e o
  harness cobre a ordenação (seção (m) em `tools/ai-intents-check`).
- **Dependência na ordem das peças (relatos "telhado antes da hora" e "de novo a escada sem piso", 03/11/2026) — corrigida:**
  os dois tinham a mesma causa (a ordem não modelava dependência). (1) **Chave de ordenação `LevelKey`:** o nível de uma peça é a **altura dela** — a obra sobe **andar a andar** (piso → escada
  → parede → telhado → porta dentro do nível). Exceção: **telhado** (classe 3) pertence à **laje que cobre** (piso mais
  próximo acima, 0,3-6 m, 2,5 m em XZ) ou ao próprio nível quando não há laje acima (o caso do **telhado que começa no chão**).
  Três tentativas reprovadas: mover o piso de andar (`RemoveAt/Insert`), `StoreyOf + 2` (só pé-direito de 2 m) e a chave pelo
  **piso abaixo**, que construía uma parede em y=0 antes do piso em y=-0,5 (o canteiro "feio"). (2)
  `PullFloorsAbove` voltou a valer **só para a escada** (generalizado para todo apoio, ele puxava um piso 2 m acima de cada
  viga/poste e enchia o canteiro de pisos soltos). `MHR_Build.PullSupports` puxa os **apoios** (mesmo XZ a 1,5 m, 0,5-2,6 m abaixo, até 3, **nunca porta nem telhado**) de uma
  peça recusada com `no_support` e a **retenta logo depois deles** (não no fim do plano). Log:
  `N support piece(s) under the <peça> brought forward so it can stand`. Detalhes em `building-notes` §15.2v e §15.2w.
- **Navegação em obra (pesquisa + plano: `docs/re/navegacao-companheiro.md`, 03/11/2026):** o mover só tinha primitivas
  **locais** (andar/pular/escada/descer) e repetia a mesma quando o caminho falhava. A **fase A** (`MHR_Mover.TryDetour`)
  procura uma rota alternativa em volta (8 direções × 2 distâncias × 3 alturas, exigindo chão, espaço de cabeça, linha
  livre e ganho de proximidade **ou de altura**) e anda até lá por 4 s; 3 tentativas por alvo. **B** (links de
  escada/telhado na navmesh) e **C** (navmesh própria por grade, como o Vikings for Hire) ficam como opções se a A não
  resolver. Base da pesquisa: a navmesh do jogo não serve para base de jogador (Vikings for Hire, Nexus 4057); nosso
  `MHR_CompanionNavMesh` já usa agente próprio (tipo 40, sem portas, sobe 0,6 m).
- **O harness passou a provar o que dizia provar (branch `fix/harness-integridade`, 11/11/2026):** o
  `tools/ai-intents-check` rodava **718 PASS / 0 FAIL** e mesmo assim a confiança na prova estava errada em quatro
  pontos, todos medidos e fechados nesta rodada (o README do harness é a referência atual): a seção (h) comparava a
  cópia do `CompanionOrders` com **qualquer** `Companions.dll`, inclusive uma velha; o corpus do `JobMode` era fixo em
  18 ações enquanto o `ValidActions` já tinha 37; a seção (f) provava o **texto de fábrica copiado no `Stubs.cs`**, não
  a regra; e `KnownFail`/`HarnessLog` existiam sem ninguém os ler. Agora a (h) recusa DLL mais velha que o fonte
  (**nota** por padrão, **FAIL** com `--strict`) e compara as **37** ações lidas por reflexão; a **(aa)** falha se
  qualquer ação do `ValidActions` faltar num dos 5 contratos; a **(ab)** falha se uma ação do schema não voltar dela
  mesma do `LlmIntent` (a classe do bug do `level`); a **(f)** seta as quatro listas de coleta explicitamente
  (`WithLists`) e sobrevive ao texto de fábrica virar lixo; a **(ae)** afirma que as redes **não logam** debug no
  caminho feliz (as 7 chamadas de log dos arquivos linkados estão todas em `catch`). `--only`/`--quiet`/`--list`/`--strict`
  fecham a rodada: **FAIL e SKIP nunca são escondidos**, e `--only` com chave inexistente sai com **exit 2**.
  **Medido:** 891 PASS / 0 FAIL em 32 seções; cada asserção nova foi provada **ao contrário** (mexer na regra e ver o
  run falhar) antes de voltar ao verde.
- **Pendências conhecidas (abertas):** a **voz não passa pelo
  mixer do jogo** (por isso ignora os sliders de volume do Valheim e só obedece `[AI] TtsVolume`); o **sync com o
  upstream** está **em dia com a 1.5.8** (era 1.5.4 — ver a seção *Upstream e licença*), mas o autor publica
  rápido: conferir o `main` antes de cada release; a **sobreposição do nome do companheiro no centro do radial** (o
  nome do mundo aparece por trás do centro transparente da roda — cosmético, visto nas capturas da 1.7.0); o
  **risco médio do roster com N companheiros**; a
  **posse do ZDO no spawn em multiplayer** (revisar quando houver teste em servidor dedicado); e o
  **débito do `tools/gen_translations.py`** (medido em 03/11/2026): o mapa `ES_UI`/`FR_UI`/`IT_UI` está **45 chaves
  atrás** do PT — o bloco `hc_cfg_building_*` e as falas de construção/nivelamento (`hc_ai_build_*`, `hc_ai_level_*`)
  foram escritas à mão nos 4 JSONs e nunca entraram no gerador, então **`python tools/gen_translations.py --check`
  sai 1 (45 chaves sem tradução por idioma)** e, sem `--check`, o script **não grava nada** (só escreve quando a lista
  está vazia). Cada `hc_cfg_*`/fala nova escrita só nos JSONs **aumenta** esse número (as 5 `hc_cfg_building_*` de
  03/11/2026 o levaram de 39 para 44; a `hc_cfg_movement_pathingonbuild`, da P2, de 44 para 45). O
  `tools/release-check.ps1` **não roda esse script** (roda só o
  `audit_translations.py` dos 3 idiomas), por isso o gate de release passa com o gerador quebrado. Consertar =
  completar os mapas, não relaxar o `--check`.
  O **release/empacotamento da 1.9.2 e da 1.9.3 está fechado** (31/10/2026): tags `v1.9.2` (no merge `e9953e1`, **sem** o fix
  da barba) e `v1.9.3` (no fix `fe1b8ff`), cada uma com GitHub Release e os dois zips gerados de `pkg\<versão>\stage`
  (sem `speech.json`, sem `Translations\English.json`, sem marcador do Vortex); falta só o upload manual no **Nexus**
  (guia em `pkg\<versão>\nexus\publicar-<versão>.md`). A
  branch `origin/claude/relaxed-archimedes-ecoh6h` **não** é pendência: é o **WIP do autor do upstream** e **não**
  deve ser mergeada nem usada como referência. Detalhes na subseção *Pendências conhecidas* do `docs/roadmap.md`.

---

## Apêndice — estado da construção e da camada de IA (extraído do `AGENTS.md` em 10/11/2026)

> Bloco movido do `AGENTS.md` **sem mudança de conteúdo**, na compactação do AGENTS (o arquivo tinha 1298
> linhas e ~135 KB e virou uma referência). Tudo o que estava na secao *Camada de IA e construcao (estado
> atual)* do AGENTS esta aqui **verbatim**: as fases, os runs medidos, as tabelas de run, as chaves de config e
> as pendencias. **Atencao:** os numeros deste apendice sao um retrato de 04-11/11/2026 e alguns ja estao
> defasados no codigo (ver o topo do `AGENTS.md` para os valores medidos agora). O resumo vivo e o
> `AGENTS.md` + `docs/roadmap.md` + `docs/backlog-construcao.md`.

## Camada de IA e construção (estado atual)

O detalhe por fase (3.1 → 4.0, M1–M5, Trilha E E0–E10, navegação) está em
**`docs/re/camada-ia-e-construcao.md`**; a construção medida está em `docs/re/building-notes.md` e o plano em
`docs/plan-construcao.md`.

- **Fluxo de uma ordem:** chat (`Chat.SendText`) ou voz (push-to-talk → Whisper local) → `CompanionChatAI` (prompt
  por idioma) → `LlmClient` (POST compatível com OpenAI, `Task` + fila) → `LlmIntent` (parser) → `CompanionOrders`
  → a mesma ação do radial/tecla. `action: none` + `reply` = conversa (personalidade/memória).
- **Provedores (config `[AI]`):** Groq (`openai/gpt-oss-20b`, padrão) → LM Studio/Ollama (`127.0.0.1:1234`) →
  OpenRouter; tudo por `BaseUrl`/`Model`/`ApiKey` (a chave é `ConfigEntry`, nunca hardcoded). Saída estruturada
  `json_schema` → `json_object` → sem formato, **só** em recusa de formato.
- **Config:** as chaves e os defaults vivem em `Core/ModConfig.cs` (**367** opções / 25 seções, medidas em
  11/11/2026 pelo `python tools/gen_config_doc.py --check …` → `[ok] … (367 settings, 25 sections)`; eram **361** em
  04/11/2026 e as chaves novas do ciclo são `[Building] BodyRetry`, `[Building] StepAside`,
  `[Building] BlueprintStations`, `[Controls] BuildPanelKey`, `[Building] ReachWalk` (**365 → 366**) e
  `[Building] SpreadWorkers` (**366 → 367**)). A doc
  gerada é `package/CONFIGURATION.md` — **nunca** editar à mão: rodar o gerador e depois o `--check`. O `.cfg` é
  `BepInEx\config\AiCompanions.cfg`.
  A `[Building] PlaceReach` desta rodada (float, padrão **10**, faixa **5..20**; rótulo `hc_cfg_building_placereach`,
  "Place reach" no inglês nativo) é o `MaxPlace` do `MHR_Build` — ver o alcance de colocação na seção da construção.
  A `[Building] DeckLift` (float, padrão **1**, faixa **0..3**; rótulo `hc_cfg_building_decklift`, "Deck height" no
  inglês nativo) é o **máximo** que a obra sobe acima do chão — desde 11/11/2026 ela sobe **só o que o plano precisa**
  (o quanto a peça mais funda está enterrada, medido **uma vez por ordem**): plano desenhado no chão sobe **0,00 m** e
  estacas enterradas 2 m sobem o valor da config — ver o deck na seção da construção e `docs/re/building-notes.md`
  §17.23.
  A `[Building] SupportFallback` (bool, padrão **true**; rótulo `hc_cfg_building_supportfallback`, "Emergency support"
  no inglês nativo) liga o **apoio de emergência** — ver a empenha na seção da construção.
  A `[Building] BodyRetry` (bool, padrão **true**; rótulo `hc_cfg_building_bodyretry`, "Retry blocked pieces" no inglês
  nativo) liga a **barreira de andar** — peça recusada por corpo volta e é re-tentada antes de o plano subir de andar;
  ver a barreira (Run 10) na seção da construção e `docs/re/building-notes.md` §17.16.
  A `[Building] StepAside` (bool, padrão **true**; rótulo `hc_cfg_building_stepaside`, "Move aside" no inglês nativo) faz
  o companheiro **em espera** no grupo de obra dar um passo de **2,2 m** para fora do caminho de uma peça que outro vai
  colocar — pedido por quem coloca, andando com o próprio mover, **nunca teleporte** e **nunca o jogador**; desligada, a
  peça espera o companheiro sair sozinho. **MEDIDA no Run 11** (11/11/2026): rodou **ligada** no run de **0 puladas** —
  a **contagem** das linhas `pedindo para sair da frente`/`saindo da frente` saiu nos **Runs 15/16** (**33** linhas ×
  **`step-aside: 12`**, com ressalva de métrica — ver §17.21); ver §17.17.
  **O destino passou a ser escolhido por quem coloca** (Run 14, 11/11/2026 — §17.20): `AI/BuildAsideSpot.cs` (puro,
  seção **(y)**) monta o anel de 8 direções a 2,2 m e **descarta** o destino a menos de **1,5 m em XZ** de uma peça
  **pendente** do plano (±2 m de altura); **sem candidato o pedido não é feito** e a peça vai para a fila. No grupo, a
  mesma dupla trocando de **peça** em < **20 s** responde `Repeating` — não pede de novo (a barreira de andar continua).
  A `[Building] SpreadWorkers` (bool, padrão **true**; rótulo `hc_cfg_building_spreadworkers`, "Spread the crew out" no
  inglês nativo, `Core/ModLocalization.cs:850`) é a **dispersão dos workers** (lote `a29dedc`, 11/11/2026 — §17.21): ao
  pegar a próxima peça, o worker olha uma **janela de 6** posições da **mesma run** (`AI/BuildSpread.cs`, puro, seção
  **(z)** do harness) e escolhe a **primeira que está a 3 m ou mais (XZ)** de um **claim ativo** de outro; **nenhuma
  livre → pega a primeira mesmo** (o comportamento antigo). As adiadas entram na **fila de retry que já existe** marcadas
  `Spread` (`MHR_BuildGroup.NoteSpread`, `Jobs/MHR_BuildGroup.cs:1077`), **sem gastar tentativa**, e só são puxadas
  quando a vizinhança está livre (`SpreadFree`, `:1117`); **retry real nunca ganha esse teste** (`:1043`). Sem
  blueprint, sem **grupo compartilhado** ou com **1** worker → **fast path** (custo zero, `MHR_Build.SpreadOn`, `:999`). Resumo de
  **uma** linha no `Finish` (`dispersão: N adiada(s), M retomada(s) · step-aside: K pedido(s)`, `:7551`) e o detalhe por
  salto só com `LogDetailed`.
  A `[Building] BlueprintStations` (bool, padrão **true**; rótulo `hc_cfg_building_blueprintstations`, "Blueprint: no
  stations" no inglês nativo) **dispensa a estação só no blueprint**: com ela ligada o `PlanStations` **não** roda, o
  `BuildSite.Check` não devolve `needs_station` (`AI/BuildProbe.cs:566-569`, com o `hasStation` do resultado ainda
  dizendo a verdade do mundo, `:636`) e **template/peça única continuam exigindo** (`AI/BuildStationCover.cs:35-42`).
  **MEDIDA no Run 13** (11/11/2026): **0 bancadas inseridas** (Runs 8–11 tinham 4–7) e as **2 `stone_floor_2x2`** de pé
  **sem talhador no mundo** — ver §17.18.
- **5 idiomas (PT/EN/ES/FR/IT):** `CompanionLocale` resolve voz/idioma; o inglês é **nativo** (sem arquivo) e os
  **4** JSONs são PT/ES/FR/IT; contrato/prompt/presets por idioma; o texto livre do jogador **nunca** é traduzido.
- **Voz:** STT `Whisper.net` in-process (16 kHz mono, `whisper\ggml-small-q5_1.bin`); TTS `Kokoro-82M` via
  `sherpa-onnx` (thread própria, cache por bytes, playback 3D, `reverbZoneMix = 0` para não pegar a reverb zone
  global). `TtsVoice`/`TtsLanguage` com `auto` pela tabela.
- **Painel `I`:** abas por companheiro, voz (Ouvir/push-to-talk), personalidade, nome/tipo, aparência; o **F8** é a
  config global. Camadas de UI `1701+` (`ModUiLayers`), acima do HUD (400) e abaixo do `UnifiedPopup` (1900).
- **Ordens:** as **37** ações de `CompanionChatAI.ValidActions` (fonte do `enum` do schema); as redes por palavra
  (`RadialIntent`, `PickupIntent`, `GatherIntent`, `InteractionIntent`, `IsDoorRequest`, `IsInventoryRequest`,
  `OrderItems`) são a segurança fora do modelo. `build`/`level` são ordens; `companions.order` do bridge
  **cancela** obra/nivelamento.
  **Medido em jogo em 11/11/2026 (o fixture do `level`):** Valheim aberto pela Steam, mundo `tests`, bridge em
  `127.0.0.1:47110`, LLM local `qwen2.5-3b-instruct`, **jogo em inglês** (o contrato segue `CompanionLocale`). Três
  envios de `level the ground 8x8 here` pelo chat: **3/3** com `{"action":"level",...}` **na resposta crua** e **zero**
  linhas de resgate (`O modelo devolveu "none"…`) — antes dos dois fixes de contrato/`Aliases` a mesma frase dava
  `"action":"build"` com `target="8x8"`. A ordem executou de verdade na primeira (`[Level] … pass 1..3`, variação
  **1,09 m → 0,23 m**, `hc_ai_level_done`) e nas seguintes respondeu `hc_ai_level_already` (chão plano, 0,21 m e
  0,01 m) — o comportamento correto. Smoke do mesmo build: `companions.order wood` → `applied: true` e, pelo chat,
  `gather wood` → `[IA] Order "wood" -> 1 companion(s)` + `Applied "wood" in 975 ms`; em PT,
  `nivele o terreno 6x6 aqui` → `[Level] … 6,0 m around` + `hc_ai_level_already`. Log do boot desta sessão:
  `Loading [AI Companions 1.9.6]` e `loaded successfully! (95 methods patched)`.
- **Construção (Trilha E):** `MHR_Build` — peça única (`prefab`), template (`plan=hut|canopy|tent|house4|…`) e
  `.blueprint` (`<plugin>\blueprints` — era `BepInEx/config/AI-Companions/blueprints` até 11/11/2026). Ordem: **andar →
  piso → escada → parede → telhado → porta**; a estação é planejada (**fora do blueprint**: no blueprint ela é
  dispensada com a `[Building] BlueprintStations`, padrão true); nivelar → limpar → construir; a coleta entrega à obra e
  retoma na mesma peça; retry pass (3); scaffold provisório **off** por padrão. Medições: `building-notes` §15 e
  `plan-construcao`.
  **A pasta dos `.blueprint` (11/11/2026, `AI/BuildBlueprints.cs` → `BuildBlueprints.Folder()`):** é
  **`<plugin>\blueprints`**, isto é `BepInEx\plugins\AI-Companions\blueprints\` — resolvida pelo `Assembly.Location`,
  como `tts\`, `whisper\` e `Translations\`, porque é **conteúdo e não config** (e assim o pacote pode levá-la);
  **criada no primeiro uso** e, na primeira vez em que ela ainda **não existe**, o mod **migra** a antiga
  `BepInEx\config\AI-Companions\blueprints` **uma vez** — **cópia (nunca move)** e **sem sobrescrever** homônimo, com o
  log PT-BR `[Build] blueprints: N arquivo(s) migrados de config para plugins`; se a pasta nova já existe (o pacote a
  traz), a migração **não roda**. O pacote **leva** `blueprints\*.blueprint` quando a pasta tem arquivo
  (`tools/stage_release.py`).
  **Receita de teste × comportamento (regra do jogador, 11/11/2026:** *"âncora, mochilas, auto-pickup, etc é só nos
  testes, depois dos testes ficam normal"* **e** *"ninguém vai fazer a mesma construção no mesmo lugar"***):** âncora
  fixa no script, `BlueprintFree`, `PlaceReach` alto, `LevelGround = false`, mochila zerada
  (`companions.inventory action=clear`), `autopickup_off`, ferramenta dada por comando e talhador temporário são
  **bancada de teste**, não comportamento — o produto é a ordem ancorada na **mira/posição do jogador**, o **peso e o
  auto-pickup do jogador** e os defaults do código (10 / 1 / true / blueprint sem estação). A tabela inteira (o que é
  de teste, o que é produto e o `arquivo:linha` de cada default) está no `docs/re/building-notes.md` **§17.19**.
- **Estado medido (03/11/2026):** a `CommHomeBasicBP` (985 peças) subiu ~650 peças e **travou no nível 2**:
  `no_support` e `no progress ... 12 s` (12–17 m) nos pisos de y≈42,8 — o mover não atravessa o piso incompleto.
  É a pendência aberta da navegação (a fase A `TryDetour` não resolveu; B/C em `docs/re/navegacao-companheiro.md`).
- **P2 da navegação (multi-build) — medida em jogo (03/11/2026):** `[Movement] PathingOnBuild` (bool, **true**; exige `[Movement] Pathing`) faz a rota do companheiro
  enxergar a peça assim que ela é colocada — os ganchos `WearNTear.OnPlaced`/`Destroy` (+ chamadas em
  `PlaceDirect`/`ClearScaffolds`/`build.delete`/`MHR_Level.NextPass`) adiantam o tile (`m_pokeTime` +
  `m_buildTime = now - 5,1`, coalescido **0,35 s**, **só tiles existentes**, nunca `-1000`) e o `MHR_Mover.Plan` dá
  ~6 s de carência em rota `Partial` com alvo acima (só `Build`/`BuildGather`); tools `nav.tiles`/`nav.probe`/
  `nav.stats`. Medição que o justificou: **36 `no progress`**, 17 `no_support`, 9 `no_ground`, 6 `too_steep` —
  evidência em `docs/re/navmesh-tiles.md`, desenho/implementação em `docs/re/navegacao-companheiro.md` §6.1.
  **Medido:** a bancada colocada pelo companheiro levou o `pokes` 0 → 1 e o `rebuilds` +9 (2 no `build.delete`),
  `maxCollectMs` 2,65 ms; a `CommHomeBasicBP` foi de **720/983 para 1045/1078** e parou por falta de 77 madeiras
  (coleta 300 s sem entregar, mochila em 302/300).
- **P1 do multi-build (grupo de obra) — medido em jogo (04/11/2026, branch `fix/pisos-nivel-2`):** a ordem de
  **blueprint** sem nome agora vai para **todos** os companheiros que podem receber ordem (`MHR_Owner.IsMine` +
  `MHR_InScope`, não *combat-only*, casa a ≤ `[Building] MaxDistanceFromHome` do canteiro; morto não entra); **com
  nome**, só ele — e o caminho de **1** é o de antes (`StartBlueprintGroup` com 1 chama `StartBlueprint`).
  **Template e peça única continuam com um** (dois workers fariam a mesma peça duas vezes). O canteiro
  (`Jobs/MHR_BuildGroup.cs`, novo) tem **um plano** (o exato do líder, entregue aos seguidores por `storedPlan`),
  **claims por índice** (20 s, renovados a cada frame dirigido; peça de outro é **pulada** sem contar como feita),
  **fila de retry do canteiro** (dedup por índice, teto `MaxPasses` = 3, 2 s entre tentativas; o portão de apoio
  abaixo não gasta tentativa) e o portão `BuildPhase`. O **líder** faz `Level`/`Clear` e abre a construção; os
  seguidores ficam em `WaitGroup` (sem relógio de timeout e sem guarda de peso) até lá; se o líder sai
  (coleta/morte/"para, Fulano") o próximo é promovido (`IsLeader` pergunta ao **grupo**). O **nivelamento devolve os
  N** (`MHR_Level.SetThenWorkers` por **nome** + `NoteLeveled`, que carimba o guard de 90 s). Com **≥2** o plano **não**
  é reordenado (`PlanLocked`: `PreferNearFloor`/`PullSupports`/`PullFloorsAbove` desligados) e o `InsertBenchStep`
  **anexa** a bancada ao fim (o retry cobre); com 1, tudo como antes. **Uma obra por jogador** (`SiteBusy`; a segunda
  ordem é recusada).
  **Anti-duplicata:** claim revalidado antes de pagar + `VerifyAt` (0,35 m e yaw ±25°) para "já está de pé";
  `MarkDone` conta **1× por índice**. DevBridge `companions.build` de blueprint: `workers` e `refused` (`no companion
  in range`/`the site is busy`/`missing station: …`/`missing piece: …`/`not started`), `name` vazio = todos.
  **Limitações declaradas:** **sem reserva de material por peça** (cada worker conta mochila+baús por conta própria;
  pior caso: N× o resto do plano, com até **3 coletando ao mesmo tempo** — o `Stock` é a metade aberta do P1); grupo
  só de **blueprint**; **em memória** (não persiste); "para" = todos, "para, <nome>" = um, um modo num companheiro
  tira só ele; nivelar/limpar com **um** (P5). **Nenhuma** chave de tradução nova (reusa `hc_ai_no_companion` e
  `hc_ai_build_failed`) — harness **711** (471 + **28** da **(p)** + 26 da **(q)** + **66** da **(r)** — `CanSupport`,
  `PickPost`, `UsefulSurface`, `HoldsPlacedPiece` e as constantes da barreira — + **18** da **(s)** — `AI/BuildBody.cs`,
  a janela do corpo (`Blocks`, `HalfY` e os casos medidos do Run 10) — + **27** da **(t)** — a **barreira de andar**,
  11/11/2026 — + **6** da **(u)** —
  `BuildSupportBelow.FallbackPostAllowed`, o poste quando o apoio do plano não vem — + **5** da **(v)**,
  `BuildStationCover.Required` — + **9** da **(w)**, `BuildMaterials.Add`/`Total` — + **10** da **(x)**,
  `BuildWalkSpot.Choose` (de onde a peça é alcançada, o walk spot do Run 13) — + **18** da **(y)**,
  `BuildAsideSpot.Plan`, para onde o companheiro sai da frente (o `StepAside` do Run 14) — + **22** da **(z)**,
  `BuildSpread.Far`/`Pick`/`SameRun`, a **dispersão dos workers** (lote `a29dedc`, Run 15/16) — **706** —, **+ 5** do
  ciclo da `fila`/`DeckLift` (11/11/2026, **sem seção nova**)), MCP
  **122** (as **12**
  asserções de 03/11/2026 cobrem o contrato do grupo: `workers`, `refused` e `name` vazio = todos; as **6** de
  11/11/2026 cobrem o `stop` do `companions.order`; e as do `blueprints_list`/`blueprints_inspect` a biblioteca e o
  relatório sem ordem), config **367/25** e build **0/0**.
  **Medido em jogo (`CommHomeBasicBP`, `BlueprintFree = true`) — dezesseis runs documentados entre 04 e
  11/11/2026** (1–3 com 3 companheiros e `PlaceReach` 10/15/15+guarda; 4 com o deck; 5 com 5 companheiros; **6 em
  11/11/2026** com os dois lotes → **969 + 18 = 987**, watchdog **0**, **985 de 987** no canteiro, **`apoio de
  emergência` = 0**, **telhado
  fechado** no `render_view`, **1 morte** e `blocked_by_player` **≈770**, o jogador dentro do canteiro — §17.13;
  **7 em 11/11/2026** = o run do **degrau de 0,6 m** com o `MaxOverlap` 0,35 → **travou em 950/987**; **8 em
  11/11/2026** = a validação do lote do poste → **987 de 987 índices colocados, `skip` = 0**, **39 postes** de
  emergência (**0** recusas), **1026 peças** no canteiro (**987** do plano = **983** do arquivo + **4 bancadas** +
  **39 postes**), **watchdog `0`**, **0 `UNSUPPORTED`** (o veredito do próprio jogo, `MHR_Build.Verify`,
  `MHR_Build.cs:4561`), **0 mortes**, **0 `falls`**, **telhado fechado** e **empena leste fechada** no `render_view`,
  `blocked_by_player` **120** (116 voltaram para a fila + 4 `desistindo`) e `no progress` **11**, com o jogador **fora**
  do canteiro — §17.14; **9 em 11/11/2026** = o run com o **jogador andando** no canteiro (posicionado **uma vez**
  antes da ordem e depois **livre** — a guarda de posição foi removida a pedido dele: *"pra que eu possa andar com ele
  pelo jogo depois sem ser teleportado"*) → **977 colocadas + 11 puladas = 988** (a fala final medida foi
  `977 peças, 11 puladas`), **watchdog `0`**, **0 `UNSUPPORTED`**, **1023 peças** no canteiro e **5 bancadas**
  inseridas, `blocked_by_player` **235** (recusas **terminais** — nesta árvore ainda não existia a curta), **1 morte**
  (o companheiro 4 caiu **8,1 m do telhado** na peça **962/988**,
  lápide com **26 itens**; o mod logou `it died during the build - the order resumes after respawn` e o **próprio
  companheiro 4 encerrou a ordem** com `hc_ai_blueprint_done` depois do respawn — a morte é **ignorada por decisão do
  jogador**, não é investigação aberta) e as **11 puladas** com **2 causas medidas**: **1 `wood_gate`** em (-163,3,
  40,8, -422,9) por `blocked_by_player` repetido — era onde o jogador estava parado, o portão da entrada — e **10 numa
  coluna em x≈-152,7** (3 `wood_floor` em **39,8/40,8/40,8**, 3 `wood_roof_45` em **42,8** e 4 `piece_chest_wood` em
  **40,9**), todas `no_support` até o watchdog: a vizinhança está de pé (**5** `wood_beam_1` em **39,3**, pisos em
  39,8, vigas em 39,8/40,8/41,8) e o que faltou foi **o tile exato de 39,8**, recusado por corpo e mandado à fila de
  retry; o piso de **40,8** acima dele vê peça **do plano** embaixo (`DirectSupportIndex >= 0`) e por isso **não pede
  poste** ("nunca onde o plano já tem apoio"), mas essa peça do plano **não estava no mundo** → a coluna congelou —
  §17.15; **10 em 11/11/2026** = a **barreira de andar MEDIDA** (lote `fbda4ee`, `[Building] BodyRetry = true`, o
  **padrão**; mesmos 5 companheiros, `DeckLift = 1`, `PlaceReach = 15`, `BlueprintFree` e o **jogador andando** —
  posicionado uma vez em (-166,92, 39,92, -401,59) e depois livre, entrando no canteiro) → **989 colocadas + 1 pulada =
  990** (a fala final: `989 peças, 1 puladas`), **watchdog `0`**, **0 `UNSUPPORTED`**, **1022 peças** no canteiro
  (**989 + 33 postes**), **33 postes** de emergência (**0** recusas) e **23/23 prefabs do blueprint no lugar**
  (`accept_run.py`), **1 morte** (ignorada por decisão do jogador); **o plano fecha: 983 do arquivo + 7 bancadas
  inseridas = 990** (o run nasceu com **986** e o planejador inseriu bancadas durante a obra; **6 de pé + 1 recusada por
  `clipping` = 7**). **A única pulada não é peça da casa** — `piece_workbench` em (-156,5, 38,4, -410,6) por
  **`clipping`**, a 7ª bancada do `PlanStations`/`InsertBenchStep` (as outras **6** subiram), e é ela que fecha a conta
  do plano (não há "sobra de 1"). **Frente ao Run 9: as puladas caem 11 → 1**, com **16 peças** que chegaram a
  `curta 3/3` e voltaram **antes** de o plano subir (as peças 367-379 subiram depois das curtas; as paredes deixaram de
  subir com o térreo aberto); o `blocked_by_player` **não** é comparável direto — Run 9 **235** terminais contra Run 10
  **141 terminais + 224 curtas** (critério diferente, §17.16) — §17.16; **11 em 11/11/2026** = o **primeiro run com
  `0 puladas`** (o lote da sonda por **cápsula × caixa girada** + os **dois furos da barreira**, com
  `[Building] BodyRetry = true` **e** `[Building] StepAside = true` — a chave **não existia** na árvore do Run 10; mesmos
  5 companheiros, `DeckLift = 1`, `PlaceReach = 15`, `BlueprintFree` e o **jogador andando** — posicionado uma vez em
  (-166,92, 39,92, -401,59), depois livre, entrou no canteiro várias vezes e **subiu até y 45**) → **987 colocadas + 0
  puladas = 987** (a fala final medida: `987 peças, 0 puladas`), **watchdog `0`**, **0 `UNSUPPORTED`**, **0 mortes**,
  **0 `falls`**, **1020 peças** no canteiro (**987 + 33 postes**), **4 bancadas** inseridas e **todas colocadas** (a 7ª
  bancada com `clipping` do Run 10 **não apareceu**) e **23/23 prefabs do blueprint no lugar** (`accept_run.py`); corpo:
  **98** recusas terminais + **145** curtas e a **barreira disparou 56×** (`nada acima de … m é pego por … s`). **O plano
  do Run 11 tem 987 índices** (983 do arquivo + **4** bancadas) contra **990** (983 + **7**) do Run 10: menos bancadas,
  total absoluto menor — o número que conta é **puladas 1 → 0**. Fora do plano continuam só as **2 `stone_floor_2x2`**,
  puladas na **leitura** (`recipe not unlocked` — `MHR_Build.cs:1504-1506`, a regra pura em
  `AI/BuildBlueprintSkip.cs:25-36`): peça do **talhador de pedras** (alcance
  20 m), que exige **pedra** — o inventário do jogador está **vazio** (medido) e o catálogo do mod
  (`AI/BuildCatalog.cs:40-72`, **5** prefabs) **não constrói talhador**; é o **teto do blueprint** nesta instalação,
  não falha da obra — §17.17; **13 em 11/11/2026** = o run da **`[Building] BlueprintStations`** (lote `f93f290`, a
  chave **no padrão true**): **5 companheiros**, `BlueprintFree = true`, `PlaceReach = 15`, **`LevelGround = false`**
  (contorno — item (c) de §17.18), `TempScaffold = false` e o **jogador andando** (entrou no canteiro várias vezes) →
  **984 colocadas + 1 pulada = 985**, com o plano **985 = 983 do arquivo + as 2 `stone_floor_2x2`** (a receita foi
  aprendida na sessão e o talhador temporário **apagado antes da ordem**), **0 bancadas inseridas** (antes 4–7), as
  **2 de pedra de pé SEM talhador no mundo** (`done: stone_floor_2x2 at (-159,7, 39,3, -424,6) yaw 228,9 supported`,
  depois de **2** recusas por corpo), **30 postes**, **1014 peças** no canteiro (**984 + 30**), **watchdog `0`**,
  **0 `UNSUPPORTED`**, **0 mortes**. A **única** fora é a **cumeeira**: `wood_roof_45` em **(-152,6, 48,8, -423,6)** por
  **`no progress`** (*"12 s, 22,8 m away"* → *"23,5 m away"* → desistindo, **1 pulada**) — a peça fica a **~10 m**
  acima do chão do canteiro, o companheiro **se afastou 0,7 m** e o problema é **navegação**, não apoio nem alcance (o
  `PlaceReach` do teste era **15**); a escadinha provisória **não é a resposta** — o jogador **vetou** o `TempScaffold`,
  que fica **false**. **O ciclo de teste deste run também fechou 4 coisas de ferramenta:** a **âncora** de
  `companions.build` sem `x`/`z` passou a ser a **mira/posição do jogador** (`BuildFindCommand.Hint` +
  `jogador + 4 m`, `DevBridge/Commands.cs:1313-1321`; antes a casa foi para o **barranco** e um run se perdeu); o
  **companheiro pesado** (25–32 stacks cada) ganhou a ferramenta de teste `companions.inventory action=clear`
  (`f36e323`); o **nivelamento que dá `hc_ai_build_too_steep` não devolve a obra** (a chave não está na lista de
  retomada, `MHR_Level.cs:455`) — contornado com `LevelGround = false`; e `blueprints.list`/`inspect` entraram no
  bridge + MCP (**122 checks**, **62 ferramentas**). Detalhe, tabela Run 11 × Run 13 e o que **não** foi contado:
  `docs/re/building-notes.md` §17.18, com a seção **"receita de teste × comportamento"** no §17.19; **14 em 11/11/2026** =
  o run do **fim da obra** (lote `1ee93b1`) → **984 colocadas + 1 pulada = 985**, e a **única** fora é um `wood_roof_45`
  em **(-161,9, 46,8, -426,2)** por **`blocked_by_player`** (não é a cumeeira de 48,8 do Run 13): o log mostrou o
  **ping-pong** do `StepAside` — a saída da peça **909** caía na **910** e o pedido se repetia peça após peça — e o
  **walk spot** de uma peça de telhado **7 m abaixo** dela (41,9 para uma peça a 48,8), que é `no progress` com o
  companheiro parado longe. **Os dois foram consertados no lote:** `AI/BuildAsideSpot.cs` (puro, seção **(y)**) escolhe
  o destino **fora das peças pendentes** e o grupo **para de pedir** quando a mesma dupla troca de peça em < **20 s**; e
  o `MHR_Build.WalkSpot` passou a **anéis de 1,5/3 m** (17 raios) com `WalkHits = 3`, atravessando até 3 hits que **não
  são superfície** (corpo em cima do ponto, peça acima, mobília) em vez de perder a coluna no primeiro — §17.20;
  **15 em 11/11/2026** = o **baseline do A/B** (árvore `1ee93b1`, **sem** a dispersão; a mesma casa, a mesma âncora, os
  mesmos **5** companheiros e o canteiro **vazio nos dois**) → **984 colocadas + 1 pulada = 985**, a **única** fora um
  `wood_roof_45` em **(-151,3, 48,8, -431,3)** por **`no progress`** (a **cumeeira** de novo, pose diferente da do Run
  13), com **`corpo na peça` 194**, **`pedindo para sair da frente` 33** e **`no progress` 66**; **16 em 11/11/2026** = a
  **dispersão** (árvore `a29dedc`, `[Building] SpreadWorkers` no padrão **true**, o mesmo canteiro vazio) →
  **985/985 colocadas**, **`FALTANDO` vazio** (**23/23** prefabs), **0 mortes**, **0 `UNSUPPORTED`**, **watchdog `0`**,
  **1014 peças** no canteiro, e os marcadores de disputa em queda: **`corpo na peça` 50** (eram **194**), **`pedindo`
  12** (eram **33**) e **`no progress` 4** (eram **66**). **Ressalva honesta (confirmada pelo arquiteto):** o contador
  `dispersão:` do `Finish` saiu **0 adiada(s) / 0 retomada(s)** — **nenhuma peça foi adiada neste run** —, e a queda dos
  marcadores vem **junto** com os dois consertos do Run 14 (o `WalkSpot` em anéis e o destino novo do `StepAside`), que
  já estão **nas duas** árvores; como a dispersão **não agiu**, **não se atribui a queda a ela** — o que fecha a conta é
  o **A/B limpo** (mesma árvore, `SpreadWorkers` false × true), que **ainda não foi rodado** — ver §17.21; **17 em
  andamento** (11/11/2026, **canteiro zerado de propósito**, dispersão ligada): é a confirmação da casa do zero, sem
  número ainda; **18 em 11/11/2026** = o run da **`fila`** (o mesmo canteiro vazio e o jogador andando) → **985/985**,
  **0 puladas**, **0 mortes**, **0 `UNSUPPORTED`**, watchdog **0**, **1010 peças** no canteiro (**985 + 25 postes**),
  **14 min 04 s** e a `espera` de **1292,8 s** somada nos 5 workers (dos quais **1040,2 s** de `fila`) — é a medição
  que abriu o §17.22; **19 em 11/11/2026** = o **por frame** (o `Advance` a 60 Hz dentro da espera) → **regressão
  medida e revertida**: **38 peças/min contra 78** e `corpo na peça` **130** em **347** peças (§17.24(C)); **22 em
  11/11/2026** = o **1 Hz** (`AdvanceAskSeconds`), mesma casa e mesma receita → **985/985**, **0 puladas**, **0 mortes**,
  **0 `UNSUPPORTED`**, watchdog **0**, **727 s (12 min 07 s)**, **`fila` 4,5 s** e **`espera` 414,1 s** (§17.24(D),
  fecha o A/B do §17.22). **Dos runs 1–3:** 1 e 2
  diferem só no `PlaceReach` (10 = padrão do código; 15 = config de teste) e o
  3 roda em 15 com a guarda anti-loop: com **10**,
  **970 + 15 = 985** — a fala final fecha (`970 peças, 15 puladas`), `no_ground` **~25 → 0**, `blocked_by_player`
  **17+ → 0** *(naquele build, com o ignore de companheiro ligado — **revertido**, ver o validador)*, `log ajustado`
  **6** (o nudge agindo), **1 morte** e watchdog `15 peça(s) sem apoio`; com **15**, a
  passada principal chegou a **987/987**, **972 colocadas**, **0 mortes** e watchdog `13 peça(s) sem apoio`; no
  **Run 3** (15 + guarda), **`970 peças, 16 puladas`** = **986** (**969** `done`), **0 mortes**, **382 KB** de log (sem
  loop) e **16 peças sem apoio nomeadas** pelo watchdog. **As 13 do Run 2:** **2 baús** (`piece_chest_wood`
  em (-168,2, 39,9, -434,9), `too_steep` — o nudge de mobília de 2,5 m não achou lugar), **1 bancada**
  (`piece_workbench` em (-178,3, 38,6, -431,7), `clipping` — âncora externa colidindo) e **≈10 do andar de cima**
  (`wood_floor`/`wood_roof_45` em 43,8/45,8, `no_support` até o watchdog) — é o alvo do lote seguinte (nudge 5 m com
  3 anéis × 12, `TryBenchAt` sem `clipping`, cadeia de apoio do andar de cima). **As 16 do Run 3 são colunas
  verticais** (`wood_beam_45` em y 37,8/39,8 a oeste; `wood_floor` 39,8/43,8 + `wood_roof_45` 41,8/45,8 a leste; 4 baús
  em 39,9) e a **visão** (`render_view`) mostrou o telhado quase completo com **buraco no lado leste** — diagnóstico:
  o plano ancora pela **origem do arquivo** e a fundação do blueprint fica **1 m abaixo do terreno**, o jogo recusa a
  peça enterrada e tudo acima falha em cascata. **Desenho novo (substitui o clamp por peça do `0f13878`):** o clamp
  deixava o **piso no nível do chão** — justamente o que o jogador não queria ("a obra inteira deve subir um pouco,
  apoiada nas estacas, tipo um deck"). Agora a **obra inteira sobe `[Building] DeckLift` metros** (float, **padrão 1**,
  faixa **0..3**, rótulo `hc_cfg_building_decklift`) e as **pontas de estaca abaixo do terreno assentam no chão**:
  sem a config o deslocamento automático pelo ponto mais baixo levantava **2,00 m** (as
  estacas do arquivo estão enterradas 2 m) e ficava alto demais. O bloco é um só (`MHR_Build.cs:1818-1883`): sobe
  **todas** as peças e assenta as que ainda ficariam abaixo do chão (log
  `a obra sobe X m (deck) - N peça(s) de fundação assentadas no terreno`; o fallback é
  `DeckLiftDefault = 1f`, `:238`). **Medido no run do deck (04/11/2026, `docs/re/building-notes.md` §17.10):** ~**972
  peças de pé**, **0 mortes**, piso do térreo ~**1 m** acima do chão com as **escadas das entradas encontrando**; **2 dos
  3** workers fecharam com `0 peça(s) sem apoio` e o terceiro parou com **6 telhados** nomeados (42,8/44,8/46,8) + **4**
  `falls`, e a visão (`render_view`) mostrou a **faixa da água sul** faltando e a **empenha leste aberta**. **A config
  virou TETO (11/11/2026, `docs/re/building-notes.md` §17.23):** o lift é `Mathf.Clamp(buried, 0f, wanted)`
  (`MHR_Build.cs:1845`), com `buried` = o quanto a **peça mais funda** do plano está sob o terreno
  (`GetGroundHeight(pos) - pos.y`, `:1830-1844`, **uma vez por ordem**) e banda morta de **0,05 m** (`:1848-1851`, a
  mesma do laço de assentamento) para uma peça 3 cm enterrada não capar a config em 0,03 m. **Medido no corpus, terreno
  plano em 38,78:** os cinco planos rasos (Meadow Stilt Hut, Forest Hut, Meadow House, Meadow Manor, Hobbit Repairhut)
  logavam `a obra sobe 1,00 m (deck) - 0 peça(s) de fundação assentadas no terreno` e a peça mais baixa ia para
  **39,78 m** (**+1,00 m exatos**) — obra **flutuando**, e peça no ar não tem apoio: o jogo aplica **100 % de dano** no
  tick seguinte, ela **cai**, **dropa o material** e volta para a fila (o desperdício que o jogador viu). Hoje o mesmo
  plano loga `a obra sobe 0,00 m (deck pedido 1,00 m) - 0 peça(s) …, nada enterrado` (`:1868-1882`) e a casa de
  referência continua logando **byte a byte** `a obra sobe 1,00 m (deck) - 3 peça(s) …` (estacas enterradas →
  `capped = false`). A descrição da chave diz agora "The MOST the whole blueprint may rise…" (`Core/ModConfig.cs:975`) e
  o `CONFIGURATION.md` foi **regenerado** (**367/25**, `--check` ok); o run de revalidação (a escada do corpus com 5
  companheiros + o A/B da casa) **não foi rodado**. **Lote do
  telhado (build 0/0, harness **601** naquele lote — hoje **711**; medido no run de 11/11):** (a) **telhado apoia
  telhado** — o `DirectSupportIndex`
  excluía a **classe
  3** como apoio, então numa **empena** a fileira de cima (apoiada pela de baixo, também telhado) **nunca** era puxada;
  agora o `BuildSupportBelow.CanSupport` (`AI/BuildSupportBelow.cs:145`, usado no `MHR_Build`) permite
  telhado→telhado (e telhado no piso/parede/viga/escada), **porta nunca apoia** e a **mobília** continua fora pelo
  `IsStructural`, com a janela 0,4–2,5 m abaixo × 2,5 m em XZ e o `Rank` **intactos**; (b) **a peça colocada**:
  `BuildProbe.FootprintSupported` (`:672`) só aceitava `WearNTear` com **`m_supports = true`** e ganhou o bypass de
  **telhado (classe 3)** em `BuildSupportBelow.HoldsPlacedPiece(pieceClass, isTerrain, hasWear, wearSupports)` (`:172`):
  **terreno** segura tudo, **telhado (3)** é apoiado por **qualquer `WearNTear`**, e as **outras classes** continuam
  exigindo `m_supports` (senão uma parede subiria em cima de um baú). **O motivo escrito na época está ERRADO** — a doc
  dizia "telhado no Valheim tem `m_supports = false`", e **medido na 1.0.20** (`build.inspect` pelo DevBridge,
  04/11/2026, `docs/re/support-rule-wearntear.md` §5) `wood_roof`, `wood_roof_45`, `wood_roof_top`, `wood_roof_top_45`,
  `wood_roof_icorner_45`, `wood_floor`, `wood_wall_half`, `wood_stair`, `wood_stepladder`, `wood_pole2` e
  `wood_beam_1` são **todos `m_supports = true`**; só a **mobília** (`piece_chest_wood`) é `false`. Com as duas pontas
  em `true` o bypass é **inócuo** (`:182` `return pieceClass == 3 || wearSupports`) — **o comportamento não mudou** com
  telhado sobre telhado; ele **ficou** por segurança (a intenção era só não exigir de telhado um flag de quem carrega
  apoio), e a **única** ponta em que ele **não** é inócuo é a **errada**: um telhado apoiado em **mobília**
  (`m_supports = false`) passa no nosso teste — o jogo recusaria pela mira e derrubaria de qualquer forma. As outras
  classes seguem exigindo o flag (senão uma parede subiria em cima de um baú). E a **causa real das 13 peças** de
  telhado (42,8/44,8/46,8), que passaram **5 rodadas** em `no_support`, é **geometria — o alcance de apoio**, não o flag
  (§5 da mesma nota). **A regra do jogo:** apoio = **0,15 m de sobreposição entre colliders**
  (`WearNTear.SetupColliders` cresce cada caixa em 0,3 m no total, `WearNTear.cs:484-514`)
  e a **colocação não testa apoio** (`Player.UpdatePlacementGhost` não chama `UpdateSupport`/`HaveSupport`,
  `Player.cs:3793-4114`): a peça sem conexão (`m_support` 0) é **colocada** e o `UpdateWear` aplica **100 % de dano** no
  tick seguinte (~1 s) — ela **cai** e dropa os materiais (`WearNTear.cs:568-575`/`:685-692`). **Medido no run de 11/11
  do degrau:** um `wood_floor` a **0,6 m** acima do deck não tem apoio (**~4×** os 0,15 m): com `MaxOverlap` **0,35** o
  `wood_beam_1` (o mais curto) só cobria vão de **0,65 a 1,20 m** e o poste não o alcançava → **8 pisos em degrau**
  (y 40,4/41,4) **+ 12 baús** recusados e o run **travou em 950/987** no retry (`wood_floor` **297/298/304** repetindo):
  ali o `no_support` estava **CERTO** — colocar faria o jogo derrubar (§17.12 e §4/§6 item 6 da nota). **O lote do poste
  (`ae9ebc3`, 11/11/2026) fechou esse furo** — `MaxOverlap` **0,45**, janela do `wood_beam_1` `[0,55; 1,20]`, o vão de
  0,60 m passa a caber (§17.14); o run de validação foi o **Run 8** (**987/987**, §17.14); (c) **`falls` com
  uma segunda tentativa** — a peça que sobe e cai **duas vezes** ganha **uma** tentativa em **pose ajustada até 1 m**
  (`AdjustFallenPose`/`AdjustPose`, guarda `State.FallAdjusted`, log `log ajustado … - falls`) e só desiste sem pose
  válida; o `NudgePiece` passou a **delegar** no `AdjustPose`. O grupo distribuiu o plano por índices distintos,
  **promoveu o líder** depois da morte
  (`o líder do grupo ... saiu - outro companheiro assumiu`) e **retomou** no mesmo plano (`voltou para o grupo ... na
  peça X`). O ciclo fechou o **portão de apoio**
  (`AI/BuildFailure.cs`, puro, tabela da seção **(p)**; `no_support`/`no support`/`needs_station`/`no_ground` **com
  algo embaixo** = dependência, espera **8/12/20/25 s**, não gasta tentativa, nunca conta como pulada; o resto gasta
  uma das 3), o **watchdog de 60 s** sem peça subir (`StallSeconds`, do `LastProgressAt` do canteiro) e a **cobertura
  de bancadas** (`MaxPlannedBenches` 8 → **12**, `MaxBenchSearches` 6 → **32**, âncoras **fora do footprint** e no
  **terreno**, até 4 por peça, resumo `station: N precisam, M planejadas, K sem cobertura`). **O apoio imediato
  (04/11/2026) e a sua guarda:** num grupo o plano anda por índice e ninguém reordena (`PlanLocked`), então uma peça
  recusada com `no_support` tem o **apoio direto** puxado **por fora da ordem** (`PullDirectSupport` +
  `AI/BuildSupportBelow.cs`: 0,4–2,5 m abaixo × 2,5 m em XZ, seção **(r)**) sem tocar no plano; a guarda é
  **`State.PulledSupport`** (`MHR_Build.cs:213`/`:3110`) — **uma vez por índice de peça e no máximo 24 por ordem** — e
  na segunda recusa o pull não acontece (a peça segue o portão: 8/12/20/25 s + watchdog). **Por que a guarda existe
  (incidente do build `290afc1`, 04/11/2026):** sem ela o ciclo recusa → puxa → apoio falha → recusa virou **loop
  infinito**, com **344 MB** de log em ~10 min (**1.403.973** `sem apoio ainda` + **332.967** `apoio imediato`), índice
  congelado (434/930/722) e o **jogo travado**; o watchdog não protege disso porque só é lido em `WaitRetry` e o loop
  nunca espera. Corrigido em `797003c` (build 0/0; o harness era **539** naquele build); no run seguinte o log ficou em
  **30 KB em 20 s**.
  Detalhe e lições em `docs/re/building-notes.md` §17.8. **O apoio de emergência (`[Building] SupportFallback`, bool,
  padrão true, rótulo `hc_cfg_building_supportfallback`; 04/11/2026):** quando a peça do plano é recusada com
  `no_support` e **o plano não tem nada embaixo dela** (`DirectSupportIndex < 0` — o pull do apoio imediato responde
  −1), o companheiro **finca um poste vertical sob ela**: a **base na superfície de baixo** (chão, piso do andar de
  baixo ou topo de viga) e o **comprimento escolhido pelo vão** (`gap = peça − base`). **O raio desce em laço**: cada
  superfície **mais perto que `MinPostGap` = 0,4 m** (`MHR_Build.cs:97`) é **atravessada** e a passada seguinte começa
  **5 cm abaixo do hit**, até **`MaxPostPasses` = 4** (`:101`), com os vãos **e o veredito de cada superfície** em buffers
  fixos (`s_postDepths`/`s_postHolds`, `:105`/`:111`) e a decisão **pura** em `BuildSupportBelow.UsefulSurface`
  (`AI/BuildSupportBelow.cs:139`: o primeiro índice com `depths[i] >= minGap` **e** `holds[i]`); o alcance
  (`PostReach` = 10 m, `:92`) é o **total da caminhada**, e sem superfície útil cai no `GetGroundHeight` (fail-open).
  **Motivo medido:** **318
  recusas** com `vão de 0,16 m` — uma **beira de telhado colada** sob o piso de 40,8 parava o raio, e o apoio útil era o
  **piso do deck a 1,02 m**. **Profundidade não bastou (11/11/2026, Run 7):** uma superfície funda ainda pode **não
  segurar** — o **topo de um baú** (a mobília é a única classe com `m_supports = false` na 1.0.20) foi aceito como base, o
  poste subiu sobre o nada e o jogo recusou **o próprio poste** com `no_support` **95 vezes** (20 peças nunca subiram). A
  caminhada preenche o `holds` de cada hit (`PostBaseHolds`, `MHR_Build.cs:3383`, que pergunta o `HoldsPlacedPiece` com a
  **classe 2 fixa** — o poste é viga/poste) e **atravessa** a rasa **ou** a que não segura. **Guarda de mobília:** peça do
  plano que **não é estrutural** (`if (!BuildPieceKind.IsStructural(state.Prefab)) return false;`, `:3266`) **não ganha
  poste** — o apoio dela é o piso do plano, e esses prefabs têm `m_noClipping` (o jogo recusaria o poste ao lado como
  `clipping`, pago e perdido). `BuildSupportBelow.PickPost` (`AI/BuildSupportBelow.cs:80`) pega o **mais curto que
  alcança**: até **0,20 m mais curto** (`ShortTolerance`, `:107`) e no máximo **0,45 m mais longo** (`MaxOverlap`, `:119`
  — era **0,35** até o lote de 11/11/2026), o que dá as **janelas por comprimento**: 1 m → **`[0,55; 1,20]`**, 2 m →
  **`[1,55; 2,20]`**, 2,83 m → **`[2,38; 3,03]`** — com **dois buracos em vanilla**, **1,20–1,55 m** e **2,20–2,38 m**
  (não há peça entre 1 m e 2 m, e a de 2,83 m é o degrau seguinte); candidatos `wood_beam_1` (1 m),
  `wood_beam` (2 m), `wood_pole2` (2 m) e `wood_beam_45` (2,83 m), com o comprimento **medido dos snaps**; centro em
  `baseY + comprimento/2`. (**A 1ª versão usava `wood_beam` fixo com o centro em `y − 1`: a base ficava enterrada e o
  jogo recusava o próprio poste** — no run de 5 o `apoio de emergência` ficou em **0**.) Validado pelo jogo
  (`BuildSite.Check`: chão, estação, sem clipping) e **aceito mesmo com a nossa previsão de apoio contra, quando a única
  recusa é `no_support`** (`if (!Ok(check) && !(useful >= 0 && OnlyNoSupport(check)))`, `:3340`; `OnlyNoSupport`,
  `:4961`, exige **uma** razão e ela ser `no_support` — a prévia é a nossa, conservadora, e o jogo conecta por
  **sobreposição de collider**; `clipping`/`needs_station`/`ward`/`blocked_by_player`/`too_steep` continuam barrando, e se
  o poste cair a peça segue o retry/`falls` normal, sem caso especial), **pago** como qualquer peça (`BuildStock.Take` —
  baús/mochila, ou de graça com `BlueprintFree`) e colocado pelo executor; a peça mantém índice, claim e pose e sobe em cima do poste
  no frame seguinte (a coluna fecha **de baixo para cima**). **Uma vez por peça** (`State.FallbackPosts`) e **teto de
  24 por ordem** (`MaxFallbackPosts`, `:115`), nunca onde o plano já tem apoio; log
  `apoio de emergência: <prefab> (<len> m) sob <prefab> em (x, y, z)`, e as recusas dizem o motivo
  (`- vão de X,XX m (N superfície(s) rasa(s) ou sem apoio ignorada(s))`, `- <status> (vão X,XX m)`, `sem material …`,
  `- <erro> (vão X,XX m)`). Não há peça
  vertical de ~4 m em vanilla → vão > ~3,1 m **não recebe poste**. No **Run 6** (§17.13) ele **não foi necessário**
  (`apoio de emergência` = 0): **não era apoio que faltava** — o telhado fechou com a regra do pull (a) e o flag do
  telhado não era o problema (b). O **degrau de 0,6 m** passou a ter poste com o lote de 11/11/2026 (o `wood_beam_1` de
  1 m atravessa 0,40 m da peça — a ligação é de **collider**, não de comprimento exato): §17.14.
  **No Run 8 ele fez o trabalho (medido, 11/11/2026):** **39 postes** — **38** `wood_beam_1` (**1,00 m**) + **1**
  `wood_beam_45` (**2,83 m**, sob um `wood_beam` em (-158,8, 41,3, -428,3)) —, **0 recusas** (`sem lugar para o apoio
  de emergência` = **0**) e as peças do **degrau de 0,6 m** (os `wood_floor` de **40,4**) **subiram**: (-152,3, 40,4,
  -424,9), (-150,5, 40,4, -425,9) e (-157,1, 40,4, -438,3) — as mesmas poses que no Run 7 ficavam em `no_support`
  (§17.12/§17.14), agora em cima de um poste de 1 m. **O teto `MaxFallbackPosts` = 24 é por *worker***
  (`State.FallbackPosts`, `MHR_Build.cs:259-266`, e o teste em `:3259`): os 39 do canteiro saíram de **5**
  companheiros, então a leitura honesta é "24 por companheiro, **sem** teto por canteiro".
  **O validador (lote de
  04/11/2026, medido):** `AttachedToStructure` com **4,0 m** de `OverlapSphere` (2,6 m não achava a vizinha do
  telhado, que fica a ~2,8–3,5 m na diagonal; com 4,0 m o `no_ground` foi **~25 → 0**); **`NudgePiece` universal**
  (qualquer peça com `too_steep`/`no_ground`, não só mobília de chão) com raio por tipo via `BuildPieceKind` —
  **estrutural ≤ 1 m**, **mobília até 2,5 m** naquele lote (o `290afc1` levou a mobília a **5 m** com **3 anéis × 12**,
  `NudgeFurnitureRadius` em `MHR_Build.cs:126`; baú/cama em y 39,9 eram mobília e nunca eram ajustados; `log ajustado`
  **6** no run); e **`blocked_by_player` voltou**, agora pela **janela do corpo** (`AI/BuildBody.cs`, seção **(s)**): o
  companheiro **é obstáculo** — o ignore dele foi **REVERTIDO** (a peça era colocada **dentro** do companheiro e ele
  **afundava**). A janela é a do jogo (`Player.UpdatePlacementGhost` → `Character`): a **cápsula real** do corpo
  (`Collider.bounds.min.y`/`max.y` + raio, `BuildProbe.CapsuleOf`, `AI/BuildProbe.cs:813`) contra a **caixa girada** da
  peça (`center = point + rot*(box.center − piece.transform.position)`, `halfY` pelo `BuildBody.HalfY`, `:733-793`), com
  **toque = livre** (`pieceTop <= capsuleBottom || pieceBottom >= capsuleTop`, `AI/BuildBody.cs:45`): um piso **sobre a
  cabeça** bloqueia (era o bug "dois companions travados embaixo do piso") e um vizinho **ao lado** não. Isso **matou o
  disco**: até 11/11 o teste tratava a peça como um **disco** de `max(size.x, size.z)/2` (sem pivô nem rotação) e o
  **Run 10 pagou** — um feixe de 2 m virava um disco de 1 m e "prendia" o Companheiro 6 a 1,08 m (§17.16).
  O bloqueio (**20** no run de 5, **≈770** no de 11/11 — o jogador estava **dentro do canteiro**) é **retry**: falha
  real (gasta uma das 3), a peça volta e sobe quando o corpo sai. Detalhe medido em `docs/re/building-notes.md` §17.11,
  §17.13 e §17.16.
  **Falta medir:** o **vão do deck** (abaixo), run com **material pago** e o teste pequeno de 2 companheiros num 6x6.
  A **barreira de andar** foi **MEDIDA no Run 10** (abaixo) — saiu desta lista.
  O **Run 8** do lote do poste **foi medido** em 11/11/2026 (§17.14) — saiu desta lista também.
  A **sonda de corpo** (cápsula × caixa girada) e a **`[Building] StepAside`** foram **MEDIDAS no Run 11** (abaixo, e
  §17.17) — saíram desta lista; o que **continua aberto** nelas é a **criatura** e a **contagem** das linhas do step-aside,
  que saiu nos **Runs 15/16** com ressalva de métrica (**33** linhas × **`step-aside: 12`**, item (8) das pendências).
  **Pendência aberta — o vão do deck (`DeckLift = 1`):** a folga de ~1 m entre o piso do deck e o chão **prende
  corpos**; depois do run de 11/11 o arquiteto **apagou 41 peças do piso** (**25 + 16**) para libertar o jogador e os
  companheiros, e a casa ficou de piso furado até o **Run 8** recolocar o plano inteiro (**987/987**, §17.14). Opções
  (decisão **do jogador**): **(1)**
  `DeckLift = 0` (sem deck), **(2)** **saia em volta** fechando o perímetro, **(3)** **regra de rota** para o mover não
  entrar/parar sob o deck (`docs/re/building-notes.md` §17.13).
  **Barreira de andar — MEDIDA no Run 10 (11/11/2026, lote `fbda4ee`):** o defeito que a originou foi o do **Run 9**
  (acima): as **escadas da entrada** são as peças **8, 9 e 10 de 988** do plano — tentaram **primeiro**, foram recusadas
  por corpo (`blocked_by_player`) e **só subiram na fase de retry, no fim**. A regra do jogador, com print:
  *"essa parte da escadinha da entrada e esses dois pisos já deveriam estar ali antes de continuarem construindo pra
  cima"* e *"recusado por causa de alguém em cima/na posição, eles podem pular, mas retornar e tentar novamente antes
  de continuar erguendo pra cima"*. **O que entrou:** `blocked_by_player` virou a classe `Kind.Blocked` do
  `BuildFailure` (não gasta tentativa), a peça ganha **3 curtas na mesma pose** (2,5 s, contadas para o canteiro) e, com
  a chave `[Building] BodyRetry` (bool, **padrão true**, `Core/ModConfig.cs:958`), o canteiro **não entrega nada acima
  de 1,5 m** do nível da peça bloqueada enquanto a barreira dela estiver viva (a **primeira** recusa carimba o relógio,
  `MHR_BuildGroup.NoteBody`, idempotente em `MHR_BuildGroup.cs:782-786`, então um jogador parado no lugar não segura o
  canteiro para sempre) e a subida de andar **serve antes** a peça da fila que ficou embaixo, desistindo quando o relógio
  vence — **25 s** no Run 10 e **10 s desde o relógio único do Run 17** (`BodyRetryMaxWait` **é** `BodyBarrierSeconds`,
  `AI/BuildFailure.cs:61`/`:86`: os 25 s sobreviviam ao portão e seguravam o worker **já liberado**, §17.22)
  (`BuildFailure.BodySameStorey`/`BodyBarrierSeconds`; `MHR_BuildGroup.MayTake`/`NextRetryBelow`;
  `MHR_Build.StoreyBarrier`).
  **Refinada no mesmo lote:** a barreira roda **antes do `TryClaim`** (`MHR_Build.cs:945`/`:960`) — peça adiada não fica
  reivindicada 20 s pelo worker que a adiou — e, depois de servir a peça da fila, o `Advance` **volta ao índice da
  subida** (`State.ResumeIndex`, `MHR_Build.cs:898-902`, carimbado em `:1163`), sem re-caminhar o que já subiu.
  **Dois furos reais corrigidos:** (i) ela **só era desenhada** quando a próxima peça estava **>1 m** acima
  (`BodyRetryStep`) — a parede do 1º andar tem o **mesmo y** do feixe (**41,8**) com os tiles pendentes em **39,8**, então
  **nunca desenhava**; agora **qualquer peça pendente abaixo** da próxima desenha (`PendingBelowIndex`,
  `MHR_Build.cs:1499`; o `BodyRetryStep` ficou como constante **sem uso** em `AI/BuildFailure.cs:78`); (ii) a paciência
  contava **do desenho**, mas um `dependency` espera até **25 s** (`DependencyDelay`) e ela expirava antes de a peça
  ficar **devida** — o lote fez a paciência correr **só depois do "due"** e o Run 17 a juntou num **relógio único**
  (`BodyRetryMaxWait` = `BodyBarrierSeconds` = **10 s**, `AI/BuildFailure.cs:86`), com o watchdog de **60 s** como teto.
  **Medido:** **989 + 1 = 990**, **puladas 11 → 1** (a única é a **7ª bancada**, com `clipping`, não peça da casa),
  **16 peças** chegaram a `curta 3/3` e voltaram **antes** de o plano subir. O `blocked_by_player` **não** se compara
  direto: Run 9 **235 terminais** (não havia curta) contra Run 10 **141 terminais + 224 curtas** — o número comparável é
  o de puladas.
  Detalhe (constantes, log, tabela Run 9 × Run 10): `docs/re/building-notes.md` §17.16.
  **O "bloqueador COMPANHEIRO" do Run 10 era FALSO POSITIVO da nossa sonda — corrigido no lote seguinte:** o caso medido
  (`wood_beam` em (-168,1, 41,8, -421,1) × **Companheiro 6** em (-167,1, 39,9, -421,5): **1,08 m** em XZ, **1,9 m**
  abaixo) **não** era corpo atravessando peça — era o **disco** de 1 m da sonda (um feixe de 2 m virava disco de 1 m).
  O conserto **não** foi mexer no número: a sonda passou a usar a **cápsula real** do corpo contra a **caixa girada** da
  peça (**toque = livre**), a mesma regra do jogo (`Player.cs:4195-4225`, `ComputePenetration`), e o log imprime os
  números na **1ª** recusa (`corpo na peça X - cápsula 0,00-1,80 m, r 0,30`; `MHR_Build.cs:5030-5032`, `CapsuleText` em
  `:5074`). A `[Building] StepAside` (padrão **true**) é a **segunda linha de defesa**:
  quem coloca **pede** (`RequestStepAside`, dedup de 4 s) e o companheiro **em espera** anda **2,2 m** com o próprio
  mover (8 direções, `MHR_Mover.SafeStep`; passo de até 2,5 s) — **nunca teleporte, nunca o jogador**. **O destino, desde
  o Run 14, é escolhido por quem coloca** (`AI/BuildAsideSpot.cs`, fora das peças pendentes do plano — §17.20) e não
  mais "o mais perto dos 8 pontos", que era o que fazia o passo cair na peça seguinte. **MEDIDA no Run 11
  (11/11/2026, §17.17):** rodou **ligada** no run de **0 puladas** (a chave **não existia** na árvore do Run 10); a
  **contagem** das linhas `pedindo para sair da frente`/`saindo da frente` saiu nos **Runs 15/16** (§17.21). **Em aberto:**
  a sonda **não testa criaturas** (um javali em cima da peça ainda passa) — §17.16.
  **Run 18 (11/11/2026) — a `fila` era uma barreira invisível (§17.22):** a linha da `espera` (estreia do Run 17)
  mediu, no **Run 18**, **1292,8 s** somados nos **5** workers, dos quais **1040,2 s (80 %)** caíam em `fila`. A revisão
  independente corrigiu duas leituras: a soma é **por worker** (1292,8 ÷ 5 = **258,6 s** num run de **844 s** =
  **30,6 %** do tempo-homem; **4 dos 5** saíram antes — só a espera do **último** encurta o run, então **nada se
  dimensiona pelos 1040 s**) e o
  `1012 adiada(s)` da dispersão **não é tamanho de fila** (`SpreadSkipped += passed` soma o **índice do escolhido**, no
  máximo 5 por peça, e o `NoteSpread` **deduplica** — é cota superior de entradas criadas). **O defeito real** (o "3
  parados de uma vez"): o `WaitRetry` só chamava o `Advance` com a fila do canteiro **vazia** (`:3620` na árvore do lote
  — guarda que o próprio `Advance` **já tem** no fim do plano, `:1227`), então quem entrava na espera **no meio do
  plano**, pelas **duas portas** (o portão `:1151` e a barreira `:1433`), **nunca mais re-perguntava o `MayTake`**:
  ficava `entry.At` (2–25 s) parado **com peça livre logo à frente**, e o tempo caía em `fila`, não em barreira — é por
  isso que o `BarrierHolds` deu **0** (ele só conta em `:1149`, que exige `Phase != WaitRetry`). **O patch:**
  `bool ahead = state.Index + 1 < state.Plan.Count; if (ahead) return Advance(ai, state, dt);`
  (`MHR_Build.cs:3689-3693`; no fim do plano o caminho segue **byte a byte** e o `PlanLocked` não reordena); o livelock
  existe e é limitado pelo **relógio único** de **10 s** + watchdog de **60 s**, e o custo dele é **uma pulada** — é por
  isso que `puladas` é o guarda do run. **Instrumento:** `WaitGate` (`:480`/`:1168`, o tempo do **portão**, que não
  caía em balde nenhum porque o `Advance` roda com `dt = 0`, `:1113`), `WaitQueueEnd` × `WaitAhead` (`:467`/`:474`), o
  `dt` repassado ao `StoreyBarrier` (`:1350`) e **`BlockedRecently`** (`:1550`; `MHR_BuildGroup.cs:752`) no lugar de
  `Blocked > 0` (que **nunca é zerado**, `MHR_BuildGroup.cs:1075-1080`/`:1097`, e inflava "corpo" deflacionando
  "dependência"); a linha do `Finish` imprime `fila E s (fim P s, meio Q s)` e `portão N (G s)` com o **`total`
  incluindo o portão** (`:7691-7696`), e o `tools/build_run_report.py` entende **os dois formatos** (`:184-196`).
  **Run 18 medido:** **985/985**, **0 puladas**, **0 mortes**, **0 `UNSUPPORTED`**, watchdog **0**, **1010 peças** no
  canteiro (**985 + 25 postes**), **14 min 04 s** (relógio externo — o log não tem timestamp). **Estado:** build
  **0/0**, harness **711**, config **367/25**, MCP **122**, gate **9/9**.
  **Run 19 (por frame) e Run 22 (1 Hz) — o parque da espera (§17.24, 11/11/2026):** o defeito **provado pela revisão**
  é que `RetryPending` responde `group.Retry.Count > 0` (`MHR_BuildGroup.cs:1181-1185`) — "tem entrada na fila", **não**
  "tem peça para mim" — e a entrada de **dependência** é **imortal** (`Dependency++`, `At = now + 8/12/20/25 s`, sem teto
  e sem remoção, `MHR_BuildGroup.cs:1089-1094` com `MHR_Build.DependencyDelay`, `MHR_Build.cs:876-891`): uma peça
  esperando o apoio do **colega** mantinha a fila "não vazia" **pelo resto da obra** e o worker que entrava na espera
  (portão `:1177`, barreira `:3662`) **nunca mais olhava o plano** — é o "3 parados, às vezes 5". **A regra: 1 Hz, nunca
  por frame** — `AdvanceAskSeconds = 1f` (`:221`) + `State.AdvanceAskedAt` (`:497`, zerado no `EnterStep` `:1341`), com
  `dt` repassado (`:3722-3726`). **O Run 19 é a prova do "nunca por frame":** a 60 Hz **dobrou** o tempo (**38 peças/min
  contra 78**; `corpo na peça` **130** em **347** peças) porque o `Advance` faz `ReleaseAllOf` ao entrar e os 5 disputam
  o mesmo canto — o comentário fica no código (`:3702-3707`). **Run 22 medido (mesma receita do Run 18):** duração
  **844 → 727 s** (12 min 07 s), `espera` **1292,8 → 414,1 s**, **`fila` 1040,2 → 4,5 s**, barreira **244,9 s/73** →
  **402,7 s/99**, dependência **222,1 → 357,0 s**, **985/985 e 0 puladas** nos dois, **0/0/0** de mortes/`UNSUPPORTED`/
  watchdog, `FALTANDO` **vazio (23/23)**, postes **25 → 26** (**11** `sem lugar para o apoio`), dispersão **1012/32 → 1287/6**.
  **Leitura:** *[I]* a **barreira subiu porque absorveu a fila** (+64,4 % — a espera mudou de balde) e o número que
  decide continua sendo a **duração** e as **puladas**; a queda de **68 %** na espera virou **13,9 %** na duração (soma
  de 5 workers em paralelo, §17.22(B)). **Dois lotes revertidos com o número que os matou (§17.24(F)):** escopo **XZ de
  6 m** nas duas barreiras e o **filtro de destino do `step-aside`** — `~33 min` (2,4× a base), `dependência` **206**
  retenções e `sem lugar livre` **179**; revertidos em **`8ec9de7`**. **Custo por frame (§17.25):** `NextRetry`,
  `RetryReady` e `ServeBarrier` rodavam a **60 Hz por worker** (**~4,2 ms/frame nos 5 = ~25 %** de 16,6 ms, com
  alocação), o `ClassOf` alocava **por chamada** (**~3.000 strings/frame ≈ 170–210 KB ≈ 10–12 MB/s de GC**) e o
  `RetryReady` era **100 % morto** (`NextRetry == -1 ⟹ RetryReady == false`, sempre) — **corrigido em `c0826b4`**:
  `RetryReady` **removido** e `ClassOf` **memoizado** (`Dictionary<string,int>` `Ordinal`, `AI/BuildBlueprintParser.cs:302-317`)
  → **0 strings/frame** nesse caminho; harness **711**, gate **9/9**. **Não medido:** `µs/frame` do `Drive` (não existe
  instrumento), `BodyAt` no pico e o frame time com a obra travada (§17.25(C)).
- **Pendências conhecidas (abertas):** (1) navegação no andar em obra; (2) a voz não passa pelo mixer do jogo (só
  `[AI] TtsVolume`); (3) sync com o upstream `main` (1.5.8) em dia — conferir antes de cada release; (4) o nome do
  companheiro no centro do radial (cosmético); (5) posse do ZDO no spawn em multiplayer dedicado; (6) a dívida de
  **106 → 107** chaves por idioma do `gen_translations.py --check` (**49 → 106 em 11/11/2026**: o mapa indexa por texto PT e
  não cobre o bloco `hc_cfg_building_*` nem as falas de construção, e o lote do painel de obra pôs **57** chaves novas
  nos **4** JSONs (`48e2e96`, 56 do painel + `hc_cfg_building_reachwalk`) **sem** pôr essas strings no mapa — 49 + 57 =
  **106**; **+1** com a `hc_cfg_building_spreadworkers` traduzida no ciclo do Run 18 = **107**, medido com
  `python tools/gen_translations.py --check`);
  (7) a coleta do build com a mochila no teto de peso (o run dos 1045 ficou 300 s sem entregar a madeira);
  (8) a sonda de corpo do build **não testa criaturas** (um javali em cima da peça
  ainda passa) — a `[Building] StepAside` e a sonda por **cápsula real × caixa girada** **foram medidas no Run 11**
  (§17.17: `987 peças, 0 puladas`, 98 terminais + 145 curtas, barreira disparada **56×**), e o que fica aberto nelas é a
  **criatura** e a **contagem** das linhas do step-aside — no Run 14 o **defeito do destino** apareceu medido (o passo
  caía na peça seguinte, §17.20) e foi consertado no lote `1ee93b1`. **A contagem saiu nos Runs 15/16, com ressalva de
  métrica:** o Run 15 (árvore `1ee93b1`) tem **33** linhas `pedindo para sair da frente` (a linha é **1 por peça** —
  `AsideLogged` zerado em `EnterStep`, `MHR_Build.cs:1270`) e o Run 16 (árvore `a29dedc`) imprime **`step-aside: 12
  pedido(s)`** na linha do `Finish`, que conta **todo** pedido aceito (`AsideAsked`, `:6438`); como o contador conta
  **mais** do que as linhas, a **direção** da queda (33 → 12) é confiável — no Run 16 houve **no máximo 12** pedidos —,
  mas a **atribuição** não (§17.21); (9) as **2 `stone_floor_2x2`** do `CommHomeBasicBP`
  **saíram do teto no Run 13** (11/11/2026): com a receita aprendida na sessão e a `[Building] BlueprintStations`
  (padrão **true**) as **2 subiram sem talhador no mundo** (§17.18) — o que continua fora é **aprender a receita do
  zero** (exige pedra no jogador) e o catálogo do mod (`AI/BuildCatalog.cs:40-72`) **não construir talhador**;
  (10) **a rota até a cumeeira** — no Run 13 a **única** peça fora do plano foi o `wood_roof_45` de
  (-152,6, 48,8, -423,6) por `no progress` (12 s, **22,8 → 23,5 m**, o companheiro **se afastou**): é **navegação**
  (a mesma frente do item 1), com pose exata, e a `TempScaffold` **está vetada pelo jogador** (fica false). **O Run 14
  mostrou o que a navegação estava perdendo:** o **walk spot** de uma peça de telhado saía **7 m abaixo** dela (41,9
  para uma peça a 48,8, com `no progress … 23,4 m away, walk spot (-161,1, 41,9, -422,4)`) — o lote `1ee93b1` trocou
  os raios pelos **anéis de 1,5/3 m** (17) e o `WalkHits = 3` (§17.20). **O Run 15 mediu e NÃO resolveu:** a única
  peça fora dele foi **outra** `wood_roof_45`, em **(-151,3, 48,8, -431,3)**, por **`no progress`** — mesmo sintoma, na
  mesma altura, em pose diferente da do Run 13; o **Run 16** (mesma casa, com a dispersão) fechou **985/985**, mas o
  A/B **não isola** a causa (o contador da dispersão saiu **0**, §17.21). Ou seja: **a rota até a cumeeira continua
  aberta** — é a mesma frente do item 1, e a `TempScaffold` continua vetada pelo jogador;
  (11) **um companheiro acima do peso derruba a obra** — `MHR_Build.cs:3120-3128` encerra a ordem **daquele** worker
  com `hc_ai_build_too_heavy` e o `Finish` tira ele do grupo (`:7006-7013`), então **com todos pesados o canteiro
  morre** (no ciclo de teste eram **25–32 stacks** cada, resolvido com a ferramenta de teste
  `companions.inventory action=clear`, `DevBridge/Commands.cs:1832-1847`); num jogo normal a saída é a mochila e os
  baús, e a **reserva de material** continua sendo a metade aberta do P1;
  (12) **o nivelamento que recusa o terreno mata a obra** — `hc_ai_build_too_steep` (`MHR_Level.cs:282`) **não** está
  na lista de retomada (`:455`, só `done`/`already`/`failed`), então `[Building] LevelGround` (padrão **true**) pode
  encerrar a ordem em vez de devolver ao build; contornado no Run 13 com `LevelGround = false` (§17.18, item (c));
  (13) **o A/B do Run 19 está FECHADO** (§17.24): o **Run 22** (1 Hz) fechou **985/985**, **0 puladas**, **0 mortes**,
  **0 `UNSUPPORTED`**, watchdog **0**, em **727 s** com **`fila` 4,5 s** (era **1040,2 s**) — `fila ≤ 400 s` ✅; a
  **duração** ficou **727 s** contra o corte `≤ 720 s` ⚠️ (**7 s acima**, registrado como ficou) e o `WaitAhead`/a espera
  do **último** worker **não foram transcritos**. **A pendência nova é o instrumento:** a **barreira absorveu a fila**
  (**402,7 s / 99** retenções contra **244,9 s / 73** — *[I]* mudou de balde, não de tamanho) e o **`portão` continua
  `0,0 s`** mesmo com o `dt` **repassado** (`WaitGate += dt` em `MHR_Build.cs:1185`, só com `Phase == WaitRetry`,
  `:1181-1186`): **o instrumento ainda não explica por quê**. Junto: **`step-aside` 42 `sem lugar` para 10 pedidos** — os
  dois números contam **coisas diferentes** (a linha é **1 por peça**, `AsideLogged` zerado em `EnterStep`,
  `MHR_Build.cs:1332`, impressa em `:6569-6573`; o contador é **todo pedido aceito**, `AsideAsked`, `:6595`, na linha do
  `Finish` `:7721`), então **não formam taxa** — §17.24(G). Do mesmo ciclo, também pendente: **o run de revalidação do
  `DeckLift`** (§17.23) — a escada do corpus com **5** companheiros e o **A/B da casa de referência**;
  (14) **o corpus baixado perde linhas por receita e o mod pula em silêncio** — **279 de 354** blueprints perdem pelo
  menos uma linha por `recipe not unlocked` (**387.024** linhas no total, **100 %** delas por receita; **nenhuma** por
  prefab inexistente) e o jogador vê o **número** de puladas, nunca **qual receita** falta. O dado **já existe**
  (`BuildBlueprintReport.SkippedReasons`, devolvido como `skippedReasons` pelo `blueprints.inspect`): falta a **tela e a
  fala**. **É decisão de produto, não bug** — ver `docs/re/blueprint-corpus.md` §6.1 (e o §6.4: a pasta do corpus é a
  que o staging da release **leva**).
  (15) **o custo por frame da espera não tem medidor** (§17.25) — os **~4,2 ms/frame** nos 5 workers (≈ **25 %** de
  16,6 ms) e os **~3.000 strings/frame ≈ 170–210 KB ≈ 10–12 MB/s de GC** do `ClassOf` vêm da **revisão de código**, não
  de cronômetro: falta `Stopwatch` no `Drive` + contador em `Ready` + `GC.GetTotalMemory` na linha do `Finish`, e falta
  o `BodyAt` no pico/fim e o frame time com a obra **travada**. O que **já foi corrigido** (`c0826b4`): `RetryReady`
  **removido** (era **100 % morto**: `NextRetry == -1 ⟹ RetryReady == false`, sempre) e `ClassOf` **memoizado**
  (`Dictionary<string,int>` `Ordinal`, `AI/BuildBlueprintParser.cs:302-317`) → **0 strings/frame** nesse caminho;
  harness **711**, gate **9/9**.
  (16) **multiplayer: a troca de posse do corpo no meio da obra para tudo em silêncio** (§revisão de rede, 11/11/2026) —
  o servidor redistribui posse a cada **2 s** (`ZDOMan.ReleaseNearbyZDOS`) e o `BaseAI.UpdateAI` do jogo devolve `false`
  fora do dono, então o `MHR_Build.Drive` **para sem `Finish` e sem `Pending`**; a linha `why = "another player's game runs
  it now"` (`MHR_Build.cs:3479-3482`) é **código morto** porque o `UpdateAI` já retornou antes. O worker vira **fantasma**,
  o grupo nunca fecha (`Workers.Count > 0`) e o **`SiteBusy` trava o jogador** ("já existe um grupo de obra") até um
  "para" ou recarregar o mundo. Não há **hook de desconexão** no mod (grep por `Disconnect|Logout` = 0) e o `Pending` de
  morte é **memória pura** (perde-se se o corpo voltar noutro peer). **Não medível em single-player** — precisa de
  host + 1 cliente, e o teste mínimo está na revisão. **A chave do grupo já é por jogador** (`KeyOf` inclui
  `MHR_Owner.CreatorId`, `MHR_BuildGroup.cs:204-215`), então **duas obras do mesmo canteiro não colidem** — elas
  simplesmente **não se coordenam** (produto).
  (17) **o pagamento por baú não usa o caminho seguro** — `BuildStock.Take` (`AI/BuildStock.cs:279-281`) claima e escreve
  sem `SetInUse`, enquanto o resto do mod usa `MHR_Chests.Open`, que confere a posse depois do claim e recusa
  (`Jobs/MHR_ToolKeeper.cs:1063-1072`). Com dois jogadores tirando da mesma pilha, um `Save` perde no desempate por
  `DataRevision` → **duplicar ou perder** material (janela estreita, sem teste aqui).
  **ESCOPO DO PROJETO (decisão do jogador, 11/2026):** o **multiplayer é o ÚLTIMO item de tudo** e só entra **se o
  jogador decidir investir nele** — as pendências (5), (16) e (17) e o achado de "posse do corpo redistribuída a cada
  2 s" ficam **declarados e parados**, não são trabalho em andamento. O alvo do produto hoje é **single-player**: a
  obra de blueprint terminar **985/985 sem pulada, sem morte e sem greve** na receita de produto (material pago,
  `PlaceReach` 10, `LevelGround` ligado, peso real). Nada de desenho de rede entra no caminho crítico até essa ordem
  mudar.
  **Achado de localização (11/11/2026, CORRIGIDO no mesmo dia):** `hc_cfg_building_blueprintstations`
  (`Core/ModLocalization.cs:860`) e `hc_cfg_controls_buildpanelkey` (`:863`) estavam **só no inglês** — em PT/ES/FR/IT
  o F8 mostrava o **rótulo em inglês** ("Blueprint: no stations", "Build panel key"), **não** a chave crua: as duas
  estão registradas nos defaults ingleses, que a injeção escreve **primeiro** (`Core/ModLocalization.cs:996-1002`), e o
  arquivo do idioma só **sobrescreve** o que tem (`:1003-1018`); o rótulo sai do `LocOr` (`:948-959`) sobre a chave
  montada em `UI/ConfigPanel.cs:1262-1267` (`hc_cfg_<secao>_<chave>` → fallback `entry.Definition.Key`). Os dois
  entraram nos **4** JSONs — `buildpanelkey` em
  `Translations/*.json:1416` ("Tecla do painel de obra" / "Tecla del panel de obra" / "Touche du panneau de chantier" /
  "Tasto del pannello cantiere") e `blueprintstations` em `:2872` ("Blueprint: sem estações" / "… sin estaciones" /
  "… : sans établis" / "…: senza stazioni") — e as auditorias fecharam: **PT 0 ausentes** (eram 2) e **ES/FR/IT 0
  lacunas** (`python tools/audit_translations.py [--lang …]`); a dívida do `gen_translations.py --check` foi de **47
  para 49** (item 6).

  **Achado de localização (11/11/2026, CORRIGIDO no ciclo do Run 18):** a chave **nova**
  `hc_cfg_building_spreadworkers` (`Core/ModLocalization.cs:850`, "Spread the crew out") estava **só no inglês** (o F8
  em PT/ES/FR/IT mostrava o **rótulo em inglês**, "Spread the crew out") e entrou nos **4** JSONs, depois de
  `hc_cfg_building_reachwalk` (o bloco `hc_cfg_building_*` **não é alfabético** — é *append*), na **mesma linha (2908)
  nos quatro** arquivos:
  PT "Espalhar os companheiros" · ES "Repartir a los compañeros" · FR "Répartir les compagnons" ·
  IT "Distribuisci i compagni". **Medido:** os 4 JSONs parseiam (`json.load`), `audit_translations.py` com
  **0 ausentes / 0 sobrando / 0 categorias ausentes** nos quatro idiomas, e a dívida do
  `gen_translations.py --check` foi **106 → 107** por idioma (o valor novo não existe no mapa por texto PT — item 6).

  **Achado de localização (04/11/2026, CORRIGIDO):** `hc_cfg_advanced_configversion` (`Core/ModConfig.cs:1161`,
  `Bind(cfg, "Advanced", "ConfigVersion", 0, …)`) **não tinha entrada em nenhum** dos 4 JSONs: sem default inglês, o
  `LocOr` caía no **fallback** e o F8 mostrava o **nome curto da opção** (`ConfigVersion`) nos **5** idiomas — nem
  inglês, nem chave crua. Entrou no fim do bloco `hc_cfg_advanced_*` (o bloco é *append*, não alfabético), na **mesma
  linha (2556) nos quatro** arquivos: PT "Versão da config" · ES "Versión de la config" · FR "Version de la config" ·
  IT "Versione della config". **Medido:** `audit_translations.py --check-config` → `chaves de config no codigo: 370` e
  `ausentes (config): 0`, com a linha `sem rotulo (config, …)` **sumindo** (era
  `1 chave(s) em 4 arquivo(s) -> hc_cfg_advanced_configversion`); `--lang Spanish|French|Italian` com **0 ausentes /
  0 sobrando / 0 iguais ao PT / 0 com cara de ingles**; e o `gen_translations.py --check` foi de **107 para 108** por
  idioma (o texto PT novo não está no mapa `*_UI` — item 6).
