# AGENTS.md — AI Companions

Mod de **companheiro NPC com IA** para **Valheim 1.0**, em **PT-BR**.

Este projeto é um **projeto independente** baseado em [Offline Companions Reborn](https://github.com/MaddhattrRocks/Offline-Companions-Reborn)
(**MIT**), que já implementa a visão de "companheiro como membro da equipe" (trabalhos, comida, stamina,
skills, casa, dormir, portais, carroça, menu radial). Nosso trabalho aqui é:

1. **Traduzir completamente para PT-BR** (o mod tem sistema próprio de tradução).
2. **Adicionar a camada de IA** (LLM/voz/fala) por cima — a "Fase 3".

O trabalho é feito pela equipe de agentes especialistas em `.opencode/agents/` (cada um com a sua frente:
IA/ordens, LLM, Harmony, rede/ZDO, assets, arquitetura, revisão, QA e docs) e pelas skills em `.opencode/skills/`.
Leia este arquivo antes de agir.

## Regra prioritária — tudo documentado

**Nada entra, muda ou é decidido no projeto sem estar documentado no mesmo ciclo.** Código, config, decisão de
produto, medição, correção, exceção, pendência, release, caminho de pasta: se não está neste `AGENTS.md` (ou no doc
que ele aponta), **não aconteceu**. Na dúvida entre "documentar depois" e "documentar agora", é **agora** — o
"depois" já custou retrabalho (a 1.9.2 teve que reconstruir o que a voz prometia e não fazia, e uma frase errada
minha sobre onde o pacote era montado virou discussão). Vale para agente e para humano: **documentação é entrega,
não sobra.**

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
- Versão: **1.9.6** (publicada em **02/11/2026**, tag `v1.9.6`, GitHub **e** Nexus) — **multilíngue (5 idiomas: PT-BR,
  EN, ES, FR, IT)** (fonte única: `PluginVersion` em `src/Companions/Core/CompanionsPlugin.cs`). A 1.9.6 é a
  **construção autônoma (Trilha E, E0-E10)**: o `build` de peça única, as **estruturas por template** (cabana com
  porta, abrigo da bancada, tenda, casas 4x4/6x6/8x8), a **coleta para a obra** (baús → resto do plano → retomada no
  ponto exato), o **nivelamento com a enxada** e o `build.delete` do DevBridge — ver `docs/plan-construcao.md`,
  `docs/re/building-notes.md` e a seção 1.9.6 do `docs/roadmap.md`. A 1.9.5 é o **lote da revisão de 10 especialistas**
  (20 itens; ver `docs/backlog-1.9.5.md` e a seção 1.9.5 do `docs/roadmap.md`). A 1.9.4
  (31/10/2026, tag `v1.9.4`, só no GitHub) é a
  **barba + teto da coleta + `tokens/s`** (merge `f36e311`)
  **+ os três altos** (Santuário, `ChangeTier`, dupla morte) **+ os 3 fixes de UI + o `FightLikePlayer = false`**
  (combate da 1.9.1, validado em jogo) **+ a lápide com o dono certo + a memória localizada + o DevBridge**, tudo
  mergeado da `fix/altos-194`. A **1.9.2** (branch
  `feat/ordens-interacao`, **mergeada na `main` e publicada em 31/10/2026**, tag `v1.9.2`)
  é a **sincronização com o upstream 1.5.4 → 1.5.8** (121 arquivos, +14.907/−2.600; **63 byte a byte idênticos**,
  58 conflitos resolvidos à mão) **+ as 5 ordens de interação** por chat/voz (`hive`/`door`/`board`/`sit`/`deposit`,
  `ValidActions` 30 → **35**). A auditoria das 7 frentes está em **`docs/audit-upstream-1.9.2.md`**: ela achou
  **2 regressões altas** do merge (as zonas de plantio sumiam no túmulo e o F8 gravava o **primeiro** valor de
  `LogLevel`/`PortalItems`/`RespawnStance`) e o que ficou adiado de propósito está listado lá. A **1.9.3**
  (31/10/2026, tag `v1.9.3`) tentou corrigir só a barba (`hash("")` → `0`) e **trocou um bug por outro** — a
  companheira feminina ficava com a barba do masculino; a **1.9.4** corrige de verdade (sentinela do cache em
  `int.MinValue`, ver `docs/release-notes-1.9.4.md`) e traz os três altos (Santuário, `ChangeTier`, dupla morte)
  corrigidos e **validados em jogo** (mundo `test3`, 31/10/2026). A 1.9.4 também **volta o combate ao da 1.9.1**:
  o `[Combat] FightLikePlayer` (o "combate como um jogador" do upstream 1.5.6, que guardava o escudo entre combos e
  recuava por stamina) passou a nascer **`false`** — um golpe por vez no timer da arma e bloqueio só de reação. Quem
  quiser o combate novo liga a chave. Detalhes em `docs/roadmap.md` (1.9.4 — combate de volta ao da 1.9.1).
  **Validado em jogo pelo jogador (31/10/2026): "o combate ficou bem melhor depois que revertemos".**
  A **caça a bugs da 1.9.4** (6 frentes, read-only, ~60 achados
  distintos) está em **`docs/audit-bughunt-1.9.4.md`** — é de lá que sai o escopo da 1.9.4/1.9.5. Histórico: as
  releases saíram em sequência em 23–26/10 (`1.7.0` → `1.8.0` → `1.8.1` → `1.8.2` → `1.8.3` → `1.9.0` → `1.9.1`;
  a **1.7.1 foi pulada de propósito**, pedido do jogador) e a **1.9.1** só tirou o `ggml-base-q5_1.bin` (57 MB) do
  pacote (~368 MB → ~314 MB no zip completo). **Branches de fase** (conferido em 31/10/2026 com
  `git branch --merged main`): **mergeadas na `main` e apagáveis** — `feat/multi-companheiro` (3.9–3.20),
  `fix/peso-cinto` (3.22), `fix/renascer-30` (`RespawnDelay` 300→30), `fix/dverger-combate` (dverger só combate),
  `feat/idiomas-auto` (M1–M5), `languages` (4.0), `feat/llm-groq` (1.8.1), `feat/provedor-llm` (3.24),
  `feat/upstream-1.5.8` (o merge 1.5.4→1.5.8 que virou a 1.9.2), `fix/teto-e-tokens` e `fix/barba-sentinela`
  (as duas da 1.9.4 já na `main`, merge `f36e311`), **`fix/altos-194`** (o resto da 1.9.4, tag `v1.9.4`) e
  **`feat/1.9.5`** (o lote da revisão, tag `v1.9.5`); a
  `feat/ordens-interacao` foi apagada (local e `origin`) depois do merge da 1.9.2. As que existem no `origin` saem com
  `git push origin --delete <nome>`. A `main` é a linha de release.

## Caminhos desta máquina

- Repositório: `C:\Users\revenda33\Desktop\ModAI-BR-Companions` (a pasta manteve o nome antigo; o repo no GitHub é
  `AI-Companions`)
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
  e copia para `DeployDir`. `-p:SkipDeploy=true` compila **sem** tocar na pasta do jogo (worktree/agente);
  o build/deploy normal continua sendo **só do arquiteto**.
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
  BepInEx. Download: `https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/BepInEx_win_x64_5.4.23.5.zip`
  (extrair na raiz do jogo; o `.ini` já vem com `enabled = true` e o `target_assembly` certo).
- **Pacote/release (1.9.2 em diante):** o staging fica **na pasta do projeto**, em `pkg\<versão>\` (gitignored —
  `pkg/`), **nunca mais em `%TEMP%`**: `pkg\<versão>\stage` (o que vai no zip) e `pkg\<versão>\models\` (os modelos,
  que vêm do release anterior: Whisper `ggml-small-q5_1.bin` 181 MB e `tts\kokoro\` ~155 MB). Motivo: os modelos
  **sobrevivem** a reinstalar o Valheim (uma instalação limpa apaga a pasta do jogo, inclusive `BepInEx\plugins`,
  e aí não há de onde copiá-los). **O staging é montado por `tools/stage_release.py`** (allow-list fechada +
  asserts): `python tools/stage_release.py --version <v> --from <pasta do plugin> --to pkg/<v>/stage [--no-models]`
  — falha se achar `speech.json`, `Translations\English.json`, marcador do Vortex ou idioma fora dos 4, e coloca
  `tts\`/`whisper\` **dentro de `BepInEx\plugins\AI-Companions\`** (é onde o `CompanionTts`/`WhisperStt` procuram;
  o **1.9.1**, o pacote do Nexus, já estava assim — os zips 1.9.2/1.9.3 regrediram e tinham os modelos na raiz).
  No "sem-modelos" saem **só** os 3 arquivos grandes (`model.int8.onnx`, `voices.bin`, `whisper\*.bin`); `tokens.txt`
  e `espeak-ng-data` **ficam** (são o fonemizador).
- **Conteúdo do zip:** completa = DLL + dependências + `runtimes\win-x64\native` + `tts\kokoro\` +
  `whisper\ggml-small-q5_1.bin` + `Translations\` + `Speech\` + `LICENSES\` + `LEIA-ME.txt`. **Nunca** levar
  `speech.json` (runtime), `Translations\English.json` (runtime; o inglês é nativo no código) nem
  `__folder_managed_by_vortex`. **Layout (regra com regressão registrada):** `tts\` e `whisper\` vão **dentro de
  `BepInEx\plugins\AI-Companions\`** (é onde o `CompanionTts`/`WhisperStt` procuram — `Assembly.Location`); o 1.9.1
  estava certo, os zips **1.9.2/1.9.3** puseram os modelos na **raiz** e quebraram a voz em instalação nova, e o
  **1.9.4** voltou ao layout do 1.9.1. Detalhes, histórico e o gate de conferência: `docs/re/packaging-layout.md`.
- **"Sem modelos" descontinuado:** a partir da **1.9.4** só o pacote **completo** é distribuído; a última release
  do "sem-modelos" é a **1.9.1** (Nexus). O `--no-models` do `tools/stage_release.py` continua para uso local e
  tira só `model.int8.onnx`, `voices.bin` e `whisper\*.bin` (o `tokens.txt`/`espeak-ng-data` ficam — são o
  fonemizador).
- **Publicação:** o **GitHub** (release + o asset completo + SHA-256) é do **agente**; o **Nexus** é do **jogador**,
  com o guia que o agente escreve em `pkg\<versão>\nexus\publicar-<versão>.md`.
- **`gh` neste repositório resolve o remote `upstream` por padrão** (o fork compartilha a história): em tudo que for
  do nosso repo use `gh -R julianu37/AI-Companions ...` (ou `GH_REPO=julianu37/AI-Companions`).

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
- **Config:** as chaves e os defaults vivem em `Core/ModConfig.cs` (**359** opções / 25 seções, medidas em 03/11/2026
  com `python tools/gen_config_doc.py`, que imprime `359 settings 25 sections`); a doc gerada é
  `package/CONFIGURATION.md` — **nunca** editar à mão: rodar o gerador e depois o `--check`. O `.cfg` é
  `BepInEx\config\AiCompanions.cfg`.
  A `[Building] PlaceReach` desta rodada (float, padrão **10**, faixa **5..20**; rótulo `hc_cfg_building_placereach`,
  "Place reach" no inglês nativo) é o `MaxPlace` do `MHR_Build` — ver o alcance de colocação na seção da construção.
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
- **Construção (Trilha E):** `MHR_Build` — peça única (`prefab`), template (`plan=hut|canopy|tent|house4|…`) e
  `.blueprint` (`BepInEx/config/AI-Companions/blueprints`). Ordem: **andar → piso → escada → parede → telhado →
  porta**; a estação é planejada; nivelar → limpar → construir; a coleta entrega à obra e retoma na mesma peça;
  retry pass (3); scaffold provisório **off** por padrão. Medições: `building-notes` §15 e `plan-construcao`.
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
- **P1 do multi-build (grupo de obra) — implementado, EM TESTE (03/11/2026, branch `fix/pisos-nivel-2`):** a ordem de
  **blueprint** sem nome agora vai para **todos** os companheiros que podem receber ordem (`MHR_Owner.IsMine` +
  `MHR_InScope`, não *combat-only*, casa a ≤ `[Building] MaxDistanceFromHome` do canteiro; morto não entra); **com
  nome**, só ele — e o caminho de **1** é o de antes (`StartBlueprintGroup` com 1 chama `StartBlueprint`).
  **Template e peça única continuam com um** (dois workers fariam a mesma peça duas vezes). O canteiro
  (`Jobs/MHR_BuildGroup.cs`, novo) tem **um plano** (o exato do líder, entregue aos seguidores por `storedPlan`),
  **claims por índice** (20 s, renovados a cada frame dirigido; peça de outro é **pulada** sem contar como feita),
  **fila de retry do canteiro** (dedup por índice, teto `MaxPasses` = 3, 2 s entre tentativas, `WaitRetry` com teto
  de 60 s) e o portão `BuildPhase`. O **líder** faz `Level`/`Clear` e abre a construção; os seguidores ficam em
  `WaitGroup` (sem relógio de timeout e sem guarda de peso) até lá; se o líder sai (coleta/morte/"para, Fulano") o
  próximo é promovido (`IsLeader` pergunta ao **grupo**). O **nivelamento devolve os N** (`MHR_Level.SetThenWorkers`
  por **nome** + `NoteLeveled`, que carimba o guard de 90 s). Com **≥2** o plano **não** é reordenado (`PlanLocked`:
  `PreferNearFloor`/`PullSupports`/`PullFloorsAbove` desligados) e o `InsertBenchStep` **anexa** a bancada ao fim (o
  retry cobre); com 1, tudo como antes. **Uma obra por jogador** (`SiteBusy`; a segunda ordem é recusada).
  **Anti-duplicata:** claim revalidado antes de pagar + `VerifyAt` (0,35 m e yaw ±25°) para "já está de pé";
  `MarkDone` conta **1× por índice**. DevBridge `companions.build` de blueprint: `workers` e `refused` (`no companion
  in range`/`the site is busy`/`missing station: …`/`missing piece: …`/`not started`), `name` vazio = todos.
  **Limitações declaradas:** **sem reserva de material por peça** (cada worker conta mochila+baús por conta própria;
  pior caso: N× o resto do plano, com até **3 coletando ao mesmo tempo** — o `Stock` é a metade aberta do P1); grupo
  só de **blueprint**; **em memória** (não persiste); "para" = todos, "para, <nome>" = um, um modo num companheiro
  tira só ele; nivelar/limpar com **um** (P5). **Nenhuma** chave de tradução nova (reusa `hc_ai_no_companion` e
  `hc_ai_build_failed`) — harness **471** inalterado, MCP **101** (as **12 asserções novas** de 03/11/2026 cobrem o
  contrato do grupo: `workers`, `refused` e `name` vazio = todos), config **359/25**. **Teste em jogo pendente**
  (2 companheiros num 6x6; receita em `docs/plan-multi-build.md` §7).
- **Pendências conhecidas (abertas):** (1) navegação no andar em obra; (2) a voz não passa pelo mixer do jogo (só
  `[AI] TtsVolume`); (3) sync com o upstream `main` (1.5.8) em dia — conferir antes de cada release; (4) o nome do
  companheiro no centro do radial (cosmético); (5) posse do ZDO no spawn em multiplayer dedicado; (6) a dívida de
  45 chaves do `gen_translations.py`; (7) a coleta do build com a mochila no teto de peso (o run dos 1045 ficou
  300 s sem entregar a madeira).

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
- `docs/plan-construcao.md` — **plano da construção autônoma (Trilha E, E0-E5 feitas)**: a ordem `build`
  (bancada → baú/fogueira/cama → estruturas por template → **planos do BuildOrders**), o validador de local
  dirigido pelas flags do `Piece`, o executor (`Player.PlacePiece` quando o `Player` do dono existe + reserva
  própria), a sonda `modia_build_probe` da Fase 0 e as armadilhas confirmadas (`Piece.SetCreator` só grava com
  posse, `m_name` é token, `m_spaceRequirement` não é "espaço livre"). As fases 0–9 com critério de aceite
  estão lá, e a §3 é a pesquisa do ecossistema (BuildOrders MIT com API + helpers `Count`/`Next`/`Place`/
  `Blockers`, Quad's Companion que já constrói planos, PlanBuild WTFPL só como fonte de arquivo).
- `docs/plan-multi-build.md` — **o plano do multi-build**: o **P1 MVP (grupo de obra) implementado e sem teste em
  jogo** (§3: plano/claims/retry/líder/`PlanLocked`/limitações), a **P2 medida em jogo**, o **P4** (nomes múltiplos)
  desenhado e o que falta (P3/P5 + a reserva de material).
- `docs/audit-vs-offline-companions.md` — auditoria do nosso mod anterior vs o upstream.
- `docs/backlog-1.9.5.md` — **o que ficou sem correção** depois da revisão de 10 especialistas (detalhado: o que
  está quebrado, o que pode quebrar, por que corrigir e o esforço), com o que já foi feito na 1.9.4 no fim.
- `docs/re/` — engenharia reversa do Valheim 1.0 (APIs, diff de versão).
- `docs/re/camada-ia-e-construcao.md` — o histórico detalhado da camada de IA e da construção (extraído do
  AGENTS em 03/11/2026; o resumo/estado atual fica aqui).
- `docs/re/packaging-layout.md` — **o layout do zip** (o que vai em `BepInEx\plugins\AI-Companions\`, por que os
  modelos ficam lá, o histórico da regressão 1.9.2/1.9.3 e o gate de conferência antes de zipar).
- `docs/re/stt-whisper-net-spike.md` — o spike do STT local (Whisper.net in-process) e o loader nativo.
- `docs/re/stt-voz-pipeline.md` — o pipeline de voz da Fase 3.3 (push-to-talk → STT → ordens) e a receita de teste.
- `docs/re/tts-kokoro-sherpa-onnx.md` — a Fase 3.4 (Kokoro-82M via sherpa-onnx: carregamento dos nativos,
  síntese por frase, cache, playback 3D) e a receita de teste.
- `docs/references.md` — fontes de terceiros e licenças.

## Skills (carregue antes de escrever/revisar código)

- `bepinex` · `jotunn` · `valheim-reverse-engineering` · `valheim-ai-backend` · `valheim-assets` · `kokoro` ·
  `valheim-devbridge`

## Regras de trabalho

1. **Carregue a skill** da dependência envolvida antes de codar ou revisar.
2. **Nenhum arquivo fica editado sem commit.** Toda alteração — código, doc, config, a **fonte** da wiki
   (`docs/wiki/`), o `.tsv` de referência — é **commitada na branch correspondente no mesmo ciclo** em que foi feita.
   A árvore de trabalho termina o ciclo **limpa** (`git status` vazio). Nunca deixar mudanças soltas “para commitar
   depois”; se o trabalho estiver pela metade, o commit é `wip(...)` na branch, não uma árvore suja.
3. **Não quebre o comportamento do upstream** — nossas mudanças são: **tradução PT-BR** + **camada de IA**.
   Mudanças de lógica só com motivo claro e documentado.
4. **Tradução**: use o sistema de localização do próprio mod (`Translations/*.json`, `ModLocalization.cs`) —
   UI/hover/HUD — e `Speech/<Idioma>.json` para as falas (`SpeechConfig`). O inglês é **nativo** (não tem arquivo);
   os **4** JSONs são PT/ES/FR/IT. Não hardcode PT-BR no código quando houver token de localização.
5. **Sem segredos** hardcoded (chaves de LLM/API via `ConfigEntry` ou variável de ambiente).
6. **Código vai em branch, não direto na `main`** (combinado em 22/10/2026): `feat/<tema>` ou `fix/<tema>` →
   **o jogador testa em jogo** → merge na `main`. **Um commit por hipótese** (não junte coisas independentes num
   commit só). Docs e release podem ir direto na `main`.
7. **Sempre commitar e dar push** ao terminar (`git add -A && git commit && git push` **pelo arquiteto**; os demais
   agentes usam **caminhos explícitos**, nunca `-A`), mensagem no padrão `tipo(escopo): resumo`.
8. Atualize a documentação correspondente (`README.md`, `package/CHANGELOG.md`, `docs/roadmap.md`,
   `AGENTS.md`) quando algo mudar.
9. **Idioma da documentação:** o **público é em inglês** (`README.md`, `package/*`, a wiki com o **padrão EN** e os
   espelhos `-pt`/`-es`/`-fr`/`-it`) e o **interno é em PT-BR** (`AGENTS.md`, `docs/roadmap.md`, `docs/plan-*.md`,
   `docs/vision.md`, `docs/audit-*.md`, `docs/references.md`, `docs/re/*`) — é a nossa ferramenta de trabalho. As
   **release notes** (`docs/release-notes-*.md`) são **EN** (viram o corpo da release no GitHub) e o
   `package/LEIA-ME.txt` fica em **PT**. **Comentários de código continuam em inglês** (convenção do repositório) e
   as **mensagens de log em PT-BR**. O mod (UI/falas) segue o idioma do jogo. Plano: `docs/plan-i18n-docs.md`.
10. **Paralelismo com revisão (regra do jogador, 31/10/2026):** tarefas **independentes** podem ser distribuídas a
   **subagentes em paralelo** para agilizar (frentes com arquivos disjuntos). O **orquestrador revisa o diff, roda
   build/testes e commita** — nada entra sem revisão; e **tudo é documentado no mesmo ciclo** (AGENTS/CHANGELOG/
   release notes/roadmap/backlog), como na regra 2.
11. **Padrão de trabalho: arquiteto-orquestrador + especialistas (regra do jogador, 03/11/2026).** Toda frente que
    mexe em comportamento, rede, assets, localização ou release **começa distribuída**: carregue a skill da área e
    acione os subagentes especialistas em **paralelo e read-only** (`ai-companions-architect` comanda;
    `valheim-specialist`, `csharp-engineer`, `ai-integration`, `valheim-reverse-engineer`, `valheim-qa-engineer`
    (papel *mcp-test-engineer*), `valheim-mod-reviewer` (papel *code-reviewer*), `valheim-netcode-dev`,
    `valheim-docs-writer`, `multilanguage-verifier`, `repo-wiki-publisher`, `ai-npc-developer`, `llm-backend-dev`,
    `harmony-patcher`, `valheim-mod-architect`, `valheim-asset-dev`), dizendo a cada um o alvo, o que responder e o
    formato. `experimental.subagent_depth = 2` (`.opencode/opencode.jsonc`): o especialista pode chamar o executor
    (ex.: `valheim-specialist` → `csharp-engineer`; o `csharp-engineer` tem `subagent: deny`, então a cadeia para em
    2 níveis). O orquestrador
    consolida os achados por severidade, **aplica as correções, roda build/gate, documenta e commita** — nenhuma
    frente relevante entra sem a leitura de um especialista, e número/medida se confirma com o engenheiro reverso ou o
    QA (nunca "de memória"). Registrar no mesmo ciclo quem foi acionado, o que achou e o que foi aplicado.

## Equipe de agentes (`.opencode/agents/`)

| agente | papel |
|---|---|
| **`ai-companions-architect`** | orquestrador (primary): entende, divide, despacha, revisa, integra, valida e reporta |
| `valheim-specialist` | domínio do jogo; decide e **delega** (`csharp-engineer`/`valheim-reverse-engineer`) |
| `csharp-engineer` | implementação C# genérica |
| `ai-integration` | transporte do LLM (`LlmClient`), STT e TTS |
| `ai-npc-developer` | companheiro NPC (setup, movimento/combate, jobs, fala, ordens) |
| `llm-backend-dev` | "cérebro": contrato/prompt, schema, parser, persona/memória, redes por palavra |
| `valheim-reverse-engineer` | APIs/asset/upstream com `arquivo:linha` |
| `valheim-netcode-dev` | ZDO, posse, roster, lápide |
| `harmony-patcher` | patches seguros |
| `valheim-qa-engineer` | papel **mcp-test-engineer**: harness fora do jogo + receita/DevBridge (quem roda o jogo é o arquiteto) |
| `valheim-mod-reviewer` | papel **code-reviewer**: revisão independente, read-only |
| `multilanguage-verifier` | 5 idiomas ponta a ponta (UI, falas, contratos, vozes, config) |
| `repo-wiki-publisher` | repo + wiki nos 5 idiomas, README/CHANGELOG/release notes |
| `valheim-docs-writer` | docs internas: AGENTS, roadmap, `docs/re`, `building-notes` e os 4 JSONs de tradução |
| `valheim-mod-architect` | csproj, pacotes congelados, versão, config |
| `valheim-asset-dev` | AssetBundles/SoftReferenceableAssets |
| `jotunn-developer` | **não usado** (o mod não usa Jotunn); só se a adoção for pedida |

`experimental.subagent_depth = 2` está no `.opencode/opencode.jsonc`: o `ai-companions-architect` despacha o
especialista, e o especialista pode despachar o executor. Quem tem `subagent: deny` (a maioria dos agentes antigos)
continua sem poder abrir subagente — é proposital.

**Permissões por caminho** (03/11/2026): a permissão é a primeira barreira; a segunda é o `git status --porcelain` +
`git diff --stat` do arquiteto **antes de commitar** (nunca auto-relato — a delegação aninhada pode editar via
subagente). `valheim-specialist` e `valheim-mod-reviewer` **não editam** e rodam só git de leitura;
`valheim-qa-engineer` edita `tools/**` e o harness em `%TEMP%\opencode`; `repo-wiki-publisher` edita `README.md`,
`package/*`, as release notes, `docs/wiki/**` e o clone da wiki (nunca `src/**`); `valheim-docs-writer` edita
`AGENTS.md`, `docs/**` (menos `wiki/` e `release-notes-*`), `Translations/**`, `Speech/**` e o `CONFIGURATION.md`
gerado; os engenheiros editam `src/**` + `tools/ai-intents-check`; o `valheim-mod-architect` edita só `src/**`,
`Companions.sln` e `Environment.props.example`; o `valheim-reverse-engineer` edita só `docs/re/**` e roda uma
allowlist de pesquisa (git de leitura/fetch, `ilspycmd`, `dotnet tool`, `python tools/*`). O **build/deploy do
`Companions.sln` é só do arquiteto** (os executores rodam o harness, que não deploya), e nenhum agente pode
`--force`. O diff do harness (`tools/ai-intents-check`) **só adiciona asserção** (quem implementa não afrouxa o
teste que o aprova), e o arquiteto **serializa** as execuções do harness (duas ao mesmo tempo disputam `obj/bin`).
`repo-wiki-publisher` usa **caminhos explícitos** no `git add` (nunca `-A`/`.`/`-u`, nem `reset`/`checkout`/
`clean`/stash/force).
**Testado no runtime em 03/11/2026** (9 subagentes reais): `valheim-specialist` e `multilanguage-verifier` ficam
**sem** ferramenta de escrita (o `edit` nem aparece no catálogo); `csharp-engineer`, `ai-integration` e
`valheim-mod-architect` editam `src/` e são negados em `docs/`; `valheim-docs-writer` edita `docs/` e é negado em
`src/`; `valheim-qa-engineer` edita `tools/` e é negado em `src/`; `repo-wiki-publisher` edita `docs/wiki/` e é
negado em `src/`; `valheim-reverse-engineer` edita `docs/re/` e é negado em `src/`. `git push`/`git commit` voltam
`permission.rejected` onde não são permitidos, e o `verifier` só roda `gen_translations.py --check` (sem `--check` é
negado). Nenhum arquivo de teste ficou na árvore.

## DevBridge (ferramenta de desenvolvimento)

O `Companions.dll` carrega um módulo de dev (`src/Companions/DevBridge/`) que expõe o jogo em `127.0.0.1:47110` para o
servidor MCP em `tools/mcp-devbridge/`. **Desligado por padrão** (`[Advanced] DevBridge`), loopback only — ele roda
comandos de console e spawna coisas, então não é feature de jogador.

- **Arquitetura:** TCP loopback + fila; a API do jogo só roda na main thread (`DevBridgeService.Pump` no `Update`).
  `Commands.cs` chama `CompanionSetup`/`CompanionManager` **direto** (mesma assembly), sem reflexão.
- **Ligar:** `[Advanced] DevBridge = true` no `AiCompanions.cfg` e reiniciar o jogo; o log diz
  `[DevBridge] listening on 127.0.0.1:47110`.
- **Regra ao entrar no mundo:** `world_enter` já roda o `world.setup` — `devcommands` (**uma vez**; o comando é um
  toggle), `tod 0.5` (meio-dia) e `god on` + `heal`, mais a **stamina infinita** (`stamina`, padrão ligado;
  `stamina: false` desliga; comando solto `player.stamina`). O `god` do Valheim 1.0 **não** cancela o dano: ele trava
  o HP em **1** quando o golpe mataria (`Character.cs`), então o `heal` é obrigatório para a barra não ficar em 1. A
  stamina é reposta **a cada 0,25 s** pelo `Pump` (não há `SetStamina` na 1.0 nem comando de console - o campo privado
  é escrito por reflexão, escolhido **pelo valor** igual ao `GetStamina()`, com uma linha de log dizendo qual).
  `setup: false` no `world_enter` desliga.
- **Testar:** `python tools/mcp-devbridge/test_mcp_devbridge.py` (mock, sem jogo) — **101 checks** (as **12** novas de
  03/11/2026 cobrem o contrato do `companions.build` do grupo: `workers`, `refused` só quando recusa e `name` vazio =
  todos).
- **Testes versionados da IA:** `dotnet run --project tools/ai-intents-check` — **471 asserções** das redes por
  palavras (`RadialIntent`, `PickupIntent`, `GatherIntent`, `InteractionIntent`, `LlmIntent`, `OrderItems`,
  `CompanionNameMatch`) com os `.cs` reais linkados, mais o cross-check das cópias do `Stubs.cs` contra a
  `Companions.dll` (seção (h) — impede drift). Build 0/0 e exit 0 são o gate.
- **Gate de release:** `powershell -ExecutionPolicy Bypass -File tools\release-check.ps1 -Version <versão>` — roda o
  build, a versão (`PluginVersion` × nome do zip), as 3 traduções, o `locale-fallback-check.ps1` (agora com asserts),
  o `gen_config_doc.py --check`, o teste do MCP (101) e as 471 asserções; **PASS/FAIL por item e exit 1**. É o que tem
  de passar antes de tag/zip (`-SkipGame`/`-SkipIntents` para rodar parte).
- **Docs:** `docs/mcp-devbridge.md` (arquitetura, protocolo, as **60** ferramentas, a receita de **jogar pelo agente**
  e a de **comandos de console**, e as evidências dos testes da 1.9.4) e `tools/mcp-devbridge/README.md`
  (parâmetros/exemplos).
- **Construção (Trilha E):** E0-E6 medidas em jogo. **E6 (estruturas por template, 01/11/2026):** `hut` (1 piso + 4
  paredes + 2 telhados) e `canopy` (4 postes + 4 telhados) em `AI/BuildTemplates.cs` (puro, seção (j) do harness),
  deslocamentos tirados dos **snaps** (`build.snaps`) - a tabela está em `docs/re/building-notes.md` §11. O
  `MHR_Build` ganhou o modo **plano** (`StartPlan`, `FindAnchor` validando o template inteiro, prazo por peça) e o
  apoio é o veredito do **próprio jogo** (`AI/BuildSupport.cs`: `WearNTear.UpdateSupport`/`HaveSupport` por
  reflexão, fail-open). **Aceite:** cabana **7/7 `supported`**, todas `placed (Direct)` - **o companheiro constrói
  sozinho** (o executor não usa mais o `Player.PlacePiece`, que dava crédito/barulho/estatística ao jogador).
  Três lições: o apoio não existe antes das peças (revalidar no `Place`), o companheiro bloqueava a própria peça
  (`ignore` no validador) e o âncora é do template (11 m de erro antes). Novas ferramentas: `build.check`,
  `build.snaps`, `build.usable` (a regra do telhado), `build.delete` (limpeza de área de teste: raio até 120 m,
  `filter` por nome, `drops` leva os itens soltos - o `forcedelete` do jogo tem teto de raio e é um comando por nome;
  medido em 02/11/2026: 138 peças + 16 itens numa passada) e `companions.build plan=`. **O abrigo fecha o "usável" da
  E5:** a bancada sob o `canopy` responde `usable=True cover=0.94 underRoof=True` e as descobertas ao lado
  `usable=False cover=0.18` (o `build.usable` pega a bancada MAIS PRÓXIMA - o registro do jogo devolvia a
  primeira e respondia pela errada). **E6b - a casa simples:** `"construa um abrigo aqui"` -> 8x8 com piso,
  paredes, teto, **porta** (`wood_door`, 2x2, toma o lugar da parede) e **janela** (`wood_wall_half` a 0,5 m), 57
  peças (com a bancada); 8x8 é o tamanho que fecha o telhado 45° (2 peças por água, cumeeira no centro). Duas falas novas: 
  `hc_ai_build_cant_reach` (o `MHR_Mover` desistiu - o local está bom, o caminho não) e `hc_ai_build_too_heavy`
  (acima do peso o companheiro não anda). **E10 - terraplanagem (01/11/2026):** a ordem própria "nivele um terreno
  10x10 aqui" (`AI/LevelIntent.cs` puro + `Jobs/MHR_Level.cs`) e o nivelamento automático antes da obra (fase `Level`
  do `MHR_Build`, que escolhe o canto mais plano). **O jogo não tem "level" duro:** o Nivelar da enxada é um
  **smooth** (`mud_road_v2`, raio 3, potência 3, **±1 m por operação**) - grid **espaçado pelo raio** (o denso
  divergia 0,55 → 2,01 m), alvo na mediana, até 10 passadas ou 0,2 m, recusa acima de 1,2 m. **O companheiro não anda
  até cada ponto** (o mover desiste e ele treme no lugar): vai ao **centro**, vira, bate a enxada (animação) e a
  operação cai 0,45 s depois. **A casa entrega o nivelamento ao job** (`MHR_Level`, o mesmo da ordem) e retoma
  sozinha (`hc_ai_build_leveling` -> `the ground is ready - back to the house`) - o nivelamento é **visível**, nunca
  em lote (pedido do jogador, 01/11/2026: medido 1,07 -> 0,59 -> 0,34 -> 0,21 -> **0,13 m em 4 passadas** e 57/57
  peças). **Materiais de um plano (desenho do jogador, 01/11/2026):** a obra **começa com o que tem** e paga **peça
  por peça** (mochila → baús); quando falta, **entrega à coleta** (`Pending.Template`) e **volta sozinha** quando o
  total do template estiver coberto (`TemplateMissing`) - o antigo "exige tudo antes" saiu (era o bug do teste:
  nivelou, faltou madeira e parou). **A retomada é no ponto exato:** o vigia espera **o que faltou** (`Pending.Amount`,
  não o custo inteiro - estourava os 300 s) e o `Pending` guarda a **peça e a âncora** (`Index`/`Anchor`/`AnchorRot`);
  `StartPlan(..., anchorKnown: true)` continua da mesma pose em vez de recomeçar do passo 0 (que criava **outra
  bancada** e uma casa nova ao lado). **A coleta pede o que falta para a CASA TODA (02/11/2026):** se ele tem que
  cortar madeira, volta com o suficiente para o resto da obra **ou até a mochila encher** (80% do limite) - o total do
  template pode não caber no saco. E o log diz **de onde** veio o material (`[Build] stock: took Nx <item> from the
  bag|the chest "..." (N m)`). **Antes de sair para coletar ele RECOLHE DOS BAÚS (02/11/2026):**
  `BuildStock.WithdrawTemplate` move para a mochila o que os baús têm, até o total da estrutura, e só então
  `TemplateMissing` decide o que coletar (o jogador viu uma pilha de 50 virar 8 e outra de 30 intocada enquanto o
  companheiro ia cortar). **O número da coleta é o RESTO do plano (02/11/2026):** `PlanMissing` recebe o índice da peça e
  conta só do que falta construir (pedia 122 madeiras com 75 faltando), e a fala `hc_ai_build_collecting` usa o
  item/quantidade que a coleta pediu - o jogador ouviu "Vou buscar 1 madeira" enquanto a ordem era de 75. **E6c - a coleta do build trabalha o CANTEIRO (02/11/2026):** o
  modo madeira **mantém o Follow ligado** no upstream (`KeepsFollow`), e isso prendia o harvest a 25 m do **jogador**
  enquanto o guarda da base (10 m de qualquer peça) recusava as árvores do canteiro **em silêncio** - o companheiro
  ficava te seguindo. Agora `HandOver` **desliga o Follow** e o `RestoreFollow` devolve em todas as saídas
  (`FollowWas`), o `NearestResource` pula árvore protegida (`MHR_GatherRules.GatherRefusesTree`), o prazo de 300 s
  conta **desde o último progresso** (`LastProgressAt`, o relógio fixo cortou 36/39), 20 s de harvest ocioso encerram
  dizendo o **resto do plano** e o companheiro volta ao **modo Follow** (antes seguia catando tronco). Aceite do
  jogador: *"agora parece que funcionou perfeitamente"*. A casa custa **123 madeiras** (246 de peso). **E6d - os templates pequenos (02/11/2026):** a casa virou
  **função do tamanho** (`HouseSteps(tiles, door, window, roofAlongZ)`): **4x4** (21 peças, 51, bancada num **anexo fora**, colocada
  primeiro), **6x6** (46 peças, 101, beiral de 1 m com **4 postes** nos cantos) e **8x8** (57, 123, intacta). A **tenda**
  (`tent`) é um toldo de 1 água de 4x2 m (2 postes + 2 telhados, 8 de madeira, **4/4 `supported`**) e o **"barraco"** é
  apelido da cabana 2x2. No 6x6 a bancada fica na **mesma posição da 8x8** (girada 90° ela ficava em cima de um piso e o piso era
  recusado) e a **porta foi para a vaga da direita**; o telhado **gira** (`roofAlongZ`: empena em cima da porta) e as
  **empenas estavam com o yaw trocado** (entravam como asas - yaw 180 no lado +x). A **trava de 90° no yaw do plano foi
  tentada e revertida** (deixava a casa torta; o plano usa o yaw da mira, como a 8x8) e o anel de nivelamento caiu de
  4 m para 1,5 m (a casa ia para outro terreno). **Aceite do jogador: "tudo certo!"**. **E7 - os planos do jogador (02/11/2026):** o companheiro constrói as builds
  da biblioteca **`.blueprint`** (PlanBuild/BiomeBlueprints/valheimians): pasta `BepInEx/config/AI-Companions/blueprints/`,
  ordem pelo **nome do arquivo** ("construa a hs_meadows_hut aqui"; o **nome mais longo ganha** do template - o 3B
  devolvia "hut" para `hs_meadows_hut`; empate fica com o template - e a pasta tem cache de 10 s para nao abrir 353
  arquivos por mensagem), leitura em `AI/BuildBlueprintParser.cs` (texto,
  `#Name` + `#Pieces`, V1/V2, vírgula antiga, prefab sem caixa) e `MHR_Build.StartBlueprint` (de baixo para cima, linha
  que não serve é **pulada e contada**, `needs_station` nomeia a estação, `BlueprintFree`/`BlueprintMaxPieces`). **Sem
  PlanBuild em runtime** (ele só cria/coloca o fantasma). **Medido: `hs_meadows_hut` 28/28 `supported`**, `TestBox_V2`
  5/5 com 1 pulada. Numa build **grande** da comunidade (Nexus 1807: **985 linhas no arquivo / 983 peças no plano**,
  porque 2 são puladas na leitura) o teste achou e corrigiu: ordem por
  **classe** de peça (piso → parede → telhado; por altura começava num topo de telhado), **footprint centrado na mira**
  (a origem do arquivo é onde o autor estava), **nivelar primeiro** com retomada (e a **mesma área** de 20 m, senão vira
  loop), **estação checada antes** da 1ª peça e **pular** a peça inalcançável/que não materializa em vez de derrubar a
  ordem. Harness **405 → 430**. **O RETRY PASSE entrou no lugar da simulação de estabilidade (03/11/2026):** peça que não fica de pé
  (sem apoio, inalcançável, lenta ou que não materializa) vai para a fila e é **tentada de novo** quando o resto do
  plano sobe (`MaxPasses = 3`), com o **veredito do próprio jogo**; o que falha na última passada é pulado e contado.
  Plano grande exige **local plano e aberto** (o mover desiste em ~8 s sem progresso; o nivelamento não corta
  morro nem derruba árvore). **Bug do mesmo teste (03/11/2026):** a fala "carregando demais para construir" era **código
  morto** - `CompanionCarry.Fraction` faz `Mathf.Clamp01` (é a barra do HUD), então `Fraction > 1f` nunca era verdade; o
  companheiro tentava andar carregado, o mover desistia em 8 s e a ordem culpava o lugar ("can't get to the spot").
  Agora compara o **peso real** com o teto (`CompanionCarry.Max`). Análise do AICompanion deles em `docs/plan-e7.md` §7 (não nivela; o módulo é a lógica).
  **Limpar o canteiro (03/11/2026):** antes da primeira peça o companheiro **corta as árvores/tocos/troncos dentro do
  footprint** (`Phase.Clear` + `[Building] ClearSite`, padrão true), pelo **alvo dirigido do harvest** (o mesmo do radial:
  machado, tier, caminhada, golpes e drops são do jogo) — o `SetDirectedTarget` agora devolve `bool` (false = sem
  ferramenta/tier baixo, o obstáculo é ignorado). Orçamentos: 45 s por obstáculo, 40 obstáculos, 240 s por ordem, 5
  recusas seguidas encerram; o que sobra fica para o retry/skip. O guarda de peso fica suspenso na fase Clear (a madeira
  da limpeza cai na mochila que paga a obra) e volta na construção. Ordem: **nivelar → limpar → construir** (a enxada
  alisa o terreno com as árvores em cima). Fala `hc_ai_build_clearing` (5 idiomas). **Medido em jogo (03/11/2026, 983 peças):** nivelar 0,37 → 0,13 m, limpar
  **7 obstáculos em 156 s** (3 árvores no quadrado de 30 m; 44 s foram o harvest levando carga ao baú — o timer por
  obstáculo **pausa** acima do `UnloadThreshold`), depois a obra. O teste também expôs o **"só anda pela obra"**: o 2º/3º
  piso era recusado com `no_support` **~60× em 40 s**, cada um com caminhada; o `Goto` agora faz o `BuildSite.Check`
  **antes de andar** (`State.Checked`, 1×/peça) e manda a peça para o retry pass, e a limpeza **reancora** o relógio da
  ordem (a 1ª peça era pulada como "took too long"). **Troncos rolando no canteiro (relato do jogador, 03/11/2026):**
  o quadrado parava em 30 m (footprint 32,1 m), o filtro olhava o **pivô** (a tora tem o pivô numa ponta), a árvore
  derrubada spawna as toras depois e elas rolam, e o obstáculo "perdido" nunca era retentado — agora o quadrado é **40 m**,
  `TreeLog` é julgado pelos **limites** (`Renderer.bounds`), há uma **varredura extra 2 s depois** e uma **segunda chance**
  para os ignorados. **Cepos** também entram (são `Destructible` com `"stub"` no nome). **Blueprint retomado como
  blueprint (03/11/2026):** o `Pending` leva `Blueprint`/`BlueprintFile` + `Retry`, e o `ResumeBlueprint` continua na mesma
  âncora/rotação/peça sem repetir nivelamento nem limpeza — antes a casa de 983 peças voltava como `build wood_beam` e
  terminava. **A bancada é planejada (03/11/2026):** a regra do jogo é **por peça** (`HaveBuildStationInRange`, `m_rangeBuild` = **20 m**,
  com o **Y achatado**), então o `PlanStations` percorre o plano e, para cada peça que precisa de bancada sem nenhuma no
  raio **dela** (nem bancada já planejada), **insere uma bancada no plano logo antes** — procurada no **Y da peça** (piso,
  não terreno), 4 rotações, pelo `BuildSite.Check`; teto de 8. Estação que o mod não fabrica continua recusa nomeada. A
  retomada usa o **plano exato** (`pending.Plan`), senão os índices andariam ao reconstruir sem as bancadas já de pé.
  **No 1.0 TODA peça tem `m_craftingStation`** (medido: `station: 983 piece(s) need a workbench`, `wood_floor refused
  (needs_station)`) — sem bancada no raio da peça nada sobe. Teste de 03/11/2026: a bancada era buscada **dentro** da casa
  (tomava o lugar de um piso) → agora vai **fora do footprint, no lado da peça, no chão**; a saída "clearing stopped" não
  reancorava o relógio (a bancada foi pulada como "took too long") → corrigido; e `needs_station` **matava** o blueprint →
  agora vai para o **retry**.
  **Os baús são o estoque do CANTEIRO (03/11/2026):** `Take`/`WithdrawTemplate`/`PlanMissing` procuravam baús ao redor
  da **peça** (30 m) e, no canto oposto de uma casa de 32 m, os baús da base ficavam a ~35 m — ele entregou a coleta com
  **mais de mil madeiras nos baús**; agora a busca é ao redor da **âncora da obra** (`state.Anchor`). Segundo teste com as
  correções: bancadas **fora** do footprint (17,8 m do centro), `done: piece_workbench ... supported`, **119 peças**, 98
  **Escada antes das paredes (03/11/2026):** `ClassOf` tinha escada/escadinha na **mesma classe das paredes** (ordenada por
  altura), então as paredes fechavam o poço antes da escadinha de cima e o companheiro ficava **preso a 2,8 m do pé** dela
  (`stuck on ladder 1/2`), sem chegar ao 2º/3º piso. Agora a ordem é **piso → escada/escadinha → paredes → telhado**
  (classes 0/1/2/3) — 787/983 com as peças de cima pendentes para o retry pass.
  **Porta/portão por último (sugestão do jogador, 03/11/2026):** classe **4** (`door`/`gate`), depois do telhado — durante a
  obra o vão fica aberto e o `MHR_Mover` não abre/fecha a porta a cada passagem (mesma regra dos templates, onde a porta é
  sempre a última peça).
  **Obra andar por andar + pulinho (03/11/2026):** o plano era ordenado por **classe no plano inteiro**, então todos os
  pisos entravam na 1ª leva; os de cima caíam em `no_support`/retry e o **telhado subia antes do piso** ("ele não colocou o
  2º piso"). Agora `StoreyOf` (o último `floor` na altura ou abaixo) ordena **por andar** e, dentro dele, piso → escada →
  parede → telhado → porta. E o **pulinho**: peça até **1 m além** dos 5 m do olho, com os pés no lugar, ele `Jump` e
  **Escadinha no vazio + queda em loop (03/11/2026):** o salão de pé-direito duplo não tem piso no nível 2, a escadinha do
  plano terminava no vazio e ele **subia e caía até morrer**. O `HasLanding` do build **foi removido** (§15.2r): a escada é
  peça do plano e quem recusa a **subida** para o vazio é o `MHR_Mover.LadderLanding` (fail-open). Ficam **duas quedas
  (>0,8 m/frame) na mesma peça** → ela é pulada (só em blueprint).
  **A obra retoma depois da morte (relatos "ele caiu e morreu, aí não continuou a build" e "ele morreu e não continuou",
  03/11/2026) - corrigido de verdade em §15.2t:** o `Drive` **não roda com o corpo morto**, então o `Pending` não era
  gravado; e o respawn pode criar um **objeto novo** (novo `GetInstanceID`). Agora o `MHR_Build.OnDeath` é chamado do
  **`CompanionSetup.OnCompanionDeath`**, grava o plano com `AfterDeath` + o **id permanente** (`HC_CompanionId`), e o
  `MHR_Build.Reattach` passa o pending do id antigo para o corpo novo (com `MHR_ResolveCompanionId`, porque no `TryInit` o id
  ainda não estava resolvido - 2º bug do mesmo teste) e o `CheckPending` chama o `Reattach` **todo frame** antes de procurar o
  pending; o `CheckPending` **espera o respawn** e
  retoma **na mesma peça** pelo `ResumeBlueprint`/`StartPlan(anchorKnown)`. Um "para" do jogador continua derrubando.
  **Ele usa a obra como andaime (relato "subiu pelo telhado pra fazer a ponta", 03/11/2026):** o `MHR_Mover` sobe pela
  **própria água do telhado** e coloca a peça de cima — 20 peças acima de 43 m no log, com `wood_roof_45` a 43,8/45,8 e
  `wood_wall_roof_top_45` a 44,8/46,8. Sem teleporte: é a razão de a trava de progresso ser conservadora (12 s **sem chegar
  mais perto**, nunca por tempo total).
  **A escadinha provisória (pedido "vamos tentar aquele seu plano de fazer escadinhas provisórias", 03/11/2026):** só em
  **blueprint**, quando a trava de **12 s sem chegar mais perto** dispara **e** a peça está acima do alcance do olho, o
  `TryScaffold` coloca uma **`wood_stair` provisória** (2 madeiras, **+1 m**, rampa: o mover sobe como geometria, sem lógica
  de escada nem pouso para cair), tenta a peça de cima e **remove a escada assim que a peça sobe** (`Verify`), **nunca com
  ele a ≤1,5 m dela**; `Finish`/`Cancel`/`CancelAll`/`DiscardState`/`OnSceneLoaded`/`Shutdown` limpam só as sobras,
  devolvendo a madeira **só quando ela foi paga e o ZDO saiu** (no `BlueprintFree` não há estorno). Teto de **4 escadas
  colocadas por ordem** (`MaxScaffolds`; tentativa recusada não gasta), 1 a cada 4 s; a rampa sobe para o **-Z local**
  (`LookRotation(-toPiece)`) e não pode ocupar o lugar de uma peça do plano **nem de uma que está na fila de retry**
  (`BlockedByPlan`, 1,5 m em XZ e 2 m em Y). Antes de remover, o `StairHoldsPiece` **desliga os colliders** da escada (de pé
  ela conta como apoio), pergunta o apoio só das peças a 4 m/3 m dela e **recalcula depois** de religar; se alguma perdeu,
  **aquela** escada fica e o log avisa. Config `[Building] TempScaffold` (padrão **false**, com migração `true → false` e
  `CurrentConfigVersion = 196`).
  **O alcance era menor que o do jogo (relato "ele ainda está apanhando tentando colocar uma peça no teto alto", 03/11/2026):**
  a regra era `flat <= Reach + 0,5` (3,0 m) **e** olho ≤ 5 m; um topo de empena a 3,15 m planos (3,3 m em 3D) era recusado
  por 15 cm. Agora o limite plano é `MaxPlace - 0,5` e vale a regra do olho (`Vector3.Distance(eye, pos) <= MaxPlace`); o
  `Reach` (2,5 m) ficou como distância de parada do `MHR_Mover` **e** como limite do "pulinho" (`flat <= Reach + 0,5`); o
  teto relaxado só abre depois de 3 s sem chegar mais perto. **`MaxPlace` é a config `[Building] PlaceReach`** (float,
  padrão **10**, faixa **5..20**, lida a cada uso: mudar no F8 vale para a ordem em andamento e o `null` cai no vanilla
  **5**): com o padrão, plano relaxado **9,5 m** e olho **10 m**; com `5` volta ao de antes (4,5 m / 5 m).
  **O piso sobe junto com o apoio (pedidos "ele tem que fazer o piso no fim da escada" e "pode colocar o piso de baixo pra
  cima também", 03/11/2026):** sempre que uma peça que **sustenta** (`WearNTear.m_supports`) é aceita, o `PullFloorsAbove`
  puxa para a frente do plano (logo depois dela) os **pisos** que caem na altura alvo - **topo da escada**
  (`Renderer.bounds.max.y`) ou **um andar acima** (`Pos.y + 2`) para parede/viga/piso/pole -, com ≤2,5 m em XZ e |ΔY| ≤ 1,2 m,
  teto de **2 pisos** por passo (um patamar precisa de um tile) e só no **lado de subida** da escada. O piso 2 m acima dos
  pés está ao alcance (o olho padrão é 10 m, `[Building] PlaceReach`), então ele constrói o andar de cima **do nível em que está**. A 1ª versão movia com
  `RemoveAt`/`Insert` e **pulava o tile mais perto da escada** (reprovada na simulação do especialista); a final reconstrói a
  lista no lugar (`head` + escolhidos por distância + resto), porque `Pending.Plan` guarda a mesma referência.
  **O loop da escada (relato "fica subindo/descendo e caindo da escada", 03/11/2026) — corrigido:** a recusa do
  `LadderLanding` não marcava a escada como ruim, então a busca de rota (2 s) escolhia a mesma de novo e o relógio de
  progresso de 12 s nunca vencia (cada subida aproximava da peça). Agora a recusa chama `MarkBadLadder(..., 60 s)` e a
  peça vai para o retry pass. Detalhes em `building-notes` §15.2x.

  **A escada é peça da obra; o pouso é do mover (pedido "a escada da build deve ser feita a partir do piso", 03/11/2026):**
  o adiamento por `HasLanding` **saiu** (a casa ficava horas sem escada) - a escada é colocada quando o plano chega nela,
  apoiada no piso de baixo. Quem não sobe é o **mover**: `MHR_Mover.LadderLanding` recusa subir para o vazio nas rotas
  de **build** (e em cada degrau da corrente `PlanChain(..., requireLanding)`), e a corrente lembrada (`UseKnownChain`) não
  é usada no build (ela não passa pelo guarda). **Regra de regressão: só o VAZIO é recusado** - o guarda pergunta se
  existe **qualquer coisa** ao alcance do topo que não seja a própria escada (piso/topo de parede até 1,4 m abaixo do alvo,
  ou sólido até 1,3 m acima para a escada que termina curta), no alvo `min(topo da peça, needY + 0,5)`. As duas versões
  anteriores erraram: a 1ª sondava o **centro** (falso negativo: 2 m de profundidade) e a 2ª exigia **1,85 m de espaço**
  (recusava todo andar com teto). Falso negativo trava o andar de cima inteiro; falso positivo já é coberto pelas 2
  quedas. Medido com `build.snaps`: `wood_stair` sobe +1 m e `wood_stepladder` **+2 m**, ambas para o **-Z local**.
  **Ponta do telhado (relato "ela tá apanhando pra fazer essa ponta", 03/11/2026):** as peças da empena no topo vão para o
  retry por `no_support` (adiadas sem andar), mas as águas da ponta gastavam os **60 s** do timeout andando sem chegar. Nova
  trava de **progresso** no `Goto`: **12 s sem chegar 0,5 m mais perto** → retry (`no progress ...`).
  **Porta/portão são o último item da OBRA (relato "ele fez as portas no térreo enquanto trabalha no térreo", 03/11/2026):**
  a classe 4 valia só dentro do andar; agora o `Ordered` ordena "é porta? 0/1" **antes** do andar — as portas de todos os
  andares entram no fim de tudo.
  **Erro nosso corrigido (relato "ele não fez a escada") - superado em 03/11/2026 (§15.2r):** a guarda `HasLanding`
  **pulava** a escadinha sem piso no topo e a casa terminava sem ela; a correção seguinte (adiar para o retry) também saiu -
  hoje a escada é **peça do plano**, colocada quando o plano chega nela, e a subida é que é barrada pelo mover.
  **Andar POR ÁREA (03/11/2026):** `StoreyOf` passou a usar o **piso mais alto abaixo da peça a até 3 m em XZ** (depois 8 m,
  e só então o prédio inteiro) — no salão de pé-direito duplo a escadinha entra com o térreo e o piso nasce a partir dela.
  **"Tremendo e parou" (03/11/2026):** `MHR_Build.Active`/`MHR_Level.Active` comparavam o `GetInstanceID()` do **componente**
  chamador com um dicionário chaveado pelo do **`CompanionAI`** — nunca casava, o `OtherOrder` respondia false durante a obra
  e o **job de ócio andava junto** (dois `MHR_Mover` no mesmo corpo). Agora `StateOf`/`Owner` resolvem o `CompanionAI` do
  GameObject (o próprio componente ou `GetComponent<CompanionAI>()`) e usam o id dele; o `Cancel` tinha o mesmo defeito e
  também passa por ali (`MHR_Build.cs:648`/`:663`, `MHR_Level.cs:129`). Com isso o `OtherOrder` responde **true** durante a
  obra (todo job para, como o desenho sempre disse), com **uma exceção**: o **harvest** continua trabalhando enquanto a
  fase é `Clear` (`MHR_Build.Clearing`, `HarvestController.cs:718`) — ele é a ferramenta da limpeza do canteiro.
  **Bancada auto-curada (03/11/2026):** o `Place`, ao receber `needs_station` numa peça que precisa de bancada, chama o
  **`InsertBenchStep`** — procura o lugar (fora do footprint, no chão, 4 rotações, `BuildSite.Check`), **insere a bancada
  no plano logo antes da peça** e a torna o passo atual (teto de 8/ordem). Era o furo: o passe de estações só lê o mundo no
  início da ordem, então bancada derrubada depois (ou longe num sítio de 32 m) deixava peças recusadas só no retry.
  **Nivelamento para quando estagna (03/11/2026):** o `NextPass` guarda a variação da passada anterior e **duas passadas
  seguidas sem ganho** encerram com `hc_ai_level_failed` — antes ia até `MaxPasses = 10` (5 passadas de 24 s no mesmo
  0,26 m, porque o "para quando não melhora" estava só na doc). **A desistência do nivelamento devolve a ordem**
  (03/11/2026, relato "não fez"): `resumeBuild` inclui `hc_ai_level_failed`, senão um sítio que para em 0,30 m contra a
  tolerância de 0,20 m terminava a obra sem colocar uma peça.
  **A ponte do BuildOrders (F11) fica para depois** (barata, API pública).
  O vigia do E6b deixou de comparar **total-da-mochila** com
  **o-que-falta** (a 4x4 entrava em loop na porta); agora recalcula o que falta e retoma quando é zero. Harness
  **430**. Armadilha: `"casa pequeña"` (ES) e `"casa pequena"` (PT) **empatam** no Fold - apelidos iguais depois
  do Fold não podem existir em templates diferentes. **Aceite: 1,06 m → 0,33 m em 3 passadas, 165 s.** É ordem de um tiro (qualquer outra
  cancela - sem isso ele ficava travado). `ValidActions` 36 → 37, 5 idiomas, config **357 opções / 25 seções**
  (medido com `python tools/gen_config_doc.py --check src/Companions/Core/ModConfig.cs package/CONFIGURATION.md`).
  **Aceite em jogo (01/11/2026): 57/57 peças, todas `supported`**, todas
  `placed (Direct)` - a casa **traz a própria bancada** (passo 0, a estação vem do plano), a **porta é a última**
  (senão ele fica trancado dentro e não alcança o telhado), o alcance é o do jogo (**5 m do olho**,
  `Player.m_maxPlaceDistance` - a fileira de cima do telhado fica a 4,9 m dos pés) e **madeira pesa 2** (160 =
  320/300, o mover desiste). Custo: 123 unidades. Detalhes em `docs/re/building-notes.md` §12. Chão plano é a **E10**.
- **Construção (Trilha E, base):** E0-E5 medidas em jogo. **E5 (a oficina mínima, 01/11/2026):** 4 ordens seguidas pelo
  chat (bancada 4 s → baú 4 s → fogueira 2 s → cama 6 s), todas em `hc_ai_build_done`, o baú e a cama usando a
  bancada recém-construída como estação. **Colocar** peça só precisa da estação no raio
  (`CraftingStation.HaveBuildStationInRange`); **craftar** na bancada passa por `CheckUsable` e exige **telhado**
  (`m_craftRequireRoof`, 70% de cobertura) - o telhado é a E6 e o primeiro template dela é o **abrigo da bancada**
  (`docs/re/building-notes.md` §9). O catálogo resolve os nomes nos 5 idiomas **com plural** (49 casos no harness,
  seção (i)). As particularidades saíram do validador: **fogueira em cima de madeira -> `not_on_wood`**
  (medido: 1 `not_on_wood` + 3 `valid`, e baú/cama `valid` no mesmo ponto), baú/cama sem estação ->
  `needs_station`, e o "espaço" da cama é o **encaixe do fantasma** (`m_spaceRequirement` só vale para
  `StationExtension`). `player.look` (MCP) vira a mira - que é o `hint` da ordem - e `down` **positivo = olhar para baixo**
  (`SetMouseLook` subtrai o y). `build.inspect` / `build.sites` são as duas ferramentas da **E0** (a sonda): leem a ficha
  real de uma peça (receita, estação, flags de `Piece`, `WearNTear`, ward) e o veredito de anéis de pontos **sem
  colocar nada**. `companions.build` (E3) dá a ordem sem o LLM (o job sozinho) e `build.pieces` (E1) lista o que **já está no mundo** (prefab, criador, `mine`, `zdo`, vida,
  estação) — é como o relog foi provado. A mesma função atende os comandos `modia_build_probe [prefab] [raio]` e
  `modia_build_test [prefab] [auto|direct]`; a medição está em `docs/re/building-notes.md`.
- **Não** empacotar o servidor Python no zip; o módulo vai na DLL, desligado.

## MCP servers disponíveis

- **`valheim-devbridge`** — MCP de dev do mod, **neste repo**: `tools/mcp-devbridge/` (servidor) + o módulo
  `src/Companions/DevBridge/` na DLL. Ver `docs/mcp-devbridge.md`.
- **`alvin`** — MCP do **Visual Studio** (build, debug, Output, terminal, testes). Requer o VS aberto.
- **`context7`** — documentação atualizada de bibliotecas.
- **`learn`** — Microsoft Learn (.NET/C#/Azure).
- **`github`** — repositório/PRs/issues (requer auth via `/mcps`).

## Definição de pronto

- Compila em Release sem erros.
- Carrega no Valheim 1.0 sem exceção no `LogOutput.log`.
- Textos novos em **PT-BR** via localização.
- Sem regressão perceptível de performance.
