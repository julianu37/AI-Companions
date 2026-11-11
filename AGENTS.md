# AGENTS.md — AI Companions

Mod de **companheiro NPC com IA** para **Valheim 1.0**, em **PT-BR** na UI/falas e **5 idiomas** no total.
É um projeto **independente** derivado de [Offline Companions Reborn](https://github.com/MaddhattrRocks/Offline-Companions-Reborn)
(**MIT** — atribuição obrigatória, ver `NOTICE.md` / `THIRD_PARTY_NOTICES.md`). O upstream já traz companheiro como
membro da equipe (jobs, comida, stamina, skills, casa, dormir, portais, menu radial); o nosso trabalho é a **camada de
IA** (LLM/voz/fala) + **localização** por cima.

> **Este arquivo é uma referência operacional, não um diário.** O histórico detalhado mora em `docs/`. Se você precisa
> do "porquê" de uma decisão da construção/IA, **não** invente: leia o doc apontado no fim deste arquivo.

## Estado agora (medido em 11/11/2026)

| item | valor |
|---|---|
| versão | **1.9.6** (`PluginVersion` em `src/Companions/Core/CompanionsPlugin.cs`); `package/CHANGELOG.md` já tem a seção `1.9.7 - in progress` |
| build | `dotnet build Companions.sln -c Release` → **0 erros, 0 avisos** |
| harness (redes por palavras + tabelas puras) | **891 PASS / 0 FAIL** em **32 seções** (`--list` imprime todas; `--only=(p)` roda uma) |
| MCP mock (sem jogo) | **122 PASS / 0 FAIL** |
| config documentada | **368 opções / 25 seções** (`gen_config_doc.py --check`) |
| gate de release | **9/9 PASS** |
| branch de trabalho | `fix/harness-integridade` (a linha de release é a `main`) |

Se algum número acima não bater, **o código ganha** — rode o comando e corrija a tabela.

## Comandos (os que um agente erra por adivinhar)

```powershell
# build (compila E FAZ DEPLOY para a pasta do jogo — ver a armadilha abaixo)
dotnet build Companions.sln -c Release

# build SEM tocar na pasta do jogo (worktree / agente)
dotnet build Companions.sln -c Release -p:SkipDeploy=true

# harness das redes por palavras + tabelas puras — precisa do build acima (lê a Companions.dll)
dotnet run --project tools/ai-intents-check -c Release
#   -- --list (as 32 seções na ordem do run) | --only=(p),(aa) (só elas) | --quiet (só FAIL e o resumo)
#   -- --strict (a DLL mais velha que o fonte vira FAIL em vez de nota)
#   O filtro NUNCA esconde FAIL/SKIP; chave de seção inexistente sai com exit 2.

# MCP DevBridge em modo mock (não precisa do jogo)
python tools/mcp-devbridge/test_mcp_devbridge.py

# gate de release: roda build, versão x nome do zip, 3 traduções, locale-fallback, config-doc, MCP e harness
powershell -NoProfile -ExecutionPolicy Bypass -File tools\release-check.ps1 -Version 1.9.6
#   -SkipGame (pula o locale-fallback, que lê a DLL publicada no Valheim) | -SkipIntents (pula o harness, o item mais lento)

# traduções / config (todos com exit code)
python tools/gen_config_doc.py --check src/Companions/Core/ModConfig.cs package/CONFIGURATION.md
python tools/audit_translations.py --check-config
python tools/audit_translations.py --lang Spanish        # também French, Italian
python tools/gen_translations.py --check                 # dívida conhecida: hoje falha com 109 (ver Pendências)

# relatório de um run de obra a partir do BepInEx\LogOutput.log
python tools/build_run_report.py [log] [--last|--all] [--json]

# wiki: valida a fonte (docs/wiki/) e publica no repo separado da GitHub Wiki
python tools/publish_wiki.py --check
python tools/publish_wiki.py

# staging do pacote (NUNCA montar o zip à mão)
python tools/stage_release.py --version 1.9.6 --from "C:\Program Files (x86)\Steam\steamapps\common\Valheim\BepInEx\plugins\AI-Companions" --to pkg\1.9.6\stage [--no-models]
```

**Não existe CI** (não há `.github/`): a verificação é toda local, por esses scripts.

**Release (ordem):** versão em `PluginVersion` + seção no `package/CHANGELOG.md` → gate `release-check.ps1` **9/9** →
staging em `pkg\<versão>\stage` (o `stage_release.py` faz allow-list e asserts) → tag `v<versão>` → **GitHub** (release +
asset + SHA-256) é do **agente**; **Nexus** é do **jogador**, com o guia em `pkg\<versão>\nexus\publicar-<versão>.md`.
O staging fica **na pasta do projeto** (`pkg\`, gitignored), **nunca** em `%TEMP%`: os modelos sobrevivem a reinstalar o
Valheim e não há de onde copiá-los depois.

## Armadilhas que custam uma sessão

- **O build faz deploy.** `dotnet build Companions.sln -c Release` copia a DLL para `DeployDir`
  (`BepInEx\plugins\AI-Companions`). Em worktree/agente, use `-p:SkipDeploy=true`. O deploy normal continua sendo do
  arquiteto.
- **`Environment.props` é gitignored** (copie de `Environment.props.example`). Sem ele o csproj cai no caminho padrão
  da Steam e não acha as DLLs do jogo. As referências do jogo vêm de `$(ValheimDir)\valheim_Data\Managed` e
  `$(ValheimDir)\BepInEx\core` — **não** há pacote NuGet do Valheim.
- **O remote `upstream` foi removido** (regra do jogador, 11/11/2026): o único remote é **`origin` =
  `julianu37/AI-Companions`**, e o `gh` está com `set-default` nele (`gh-resolved = base` no `.git/config`, local —
  em outra máquina, rode `gh repo set-default julianu37/AI-Companions` uma vez). O **histórico do autor original
  continua no `git log`** (o fork compartilha a história); o que saiu foi só o ponteiro de busca. Sem o remote não
  há como `git fetch` do MaddhattrRocks — para voltar a sincronizar, `git remote add upstream
  https://github.com/MaddhattrRocks/Offline-Companions-Reborn.git`. Ainda assim, **passe `-R julianu37/AI-Companions`
  em `gh release`/`gh api`**: é barato e não depende de config local.
- **O harness lê os `.cs` reais por `<Compile Include>`** no `tools/ai-intents-check/ai-intents-check.csproj`. Regra
  pura nova exige (a) criar o arquivo em `src/Companions/AI/`, (b) **linkar** no csproj do harness, (c) adicionar as
  asserções. Só o harness e o build **0/0** provam a mudança.
- **`package/CONFIGURATION.md` é gerado** — nunca editar à mão; o `--check` acima é o gate.
- **Jotunn NÃO é usado** (0 usos no código, sem `[BepInDependency]`, sem referência no csproj). A skill `jotunn` fica
  no repo só como referência de framework; não listar Jotunn como requisito em release/README.
- **Preservados de propósito** (jogo, saves e rede dependem): o **GUID** `Maddhattr.OfflineCompanionsReborn`, o
  `Companions.dll`, o `Companions.csproj` e as chaves `HC_*`. O nome do mod é **AI Companions**.
- **O `.cfg` é `BepInEx\config\AiCompanions.cfg`** (desde 1.8.3 — o GUID não dá mais nome a ele).
- **Caminhos resolvidos por `Assembly.Location`**, não por config: `tts\`, `whisper\`, `blueprints\`, `Translations\` e
  `Speech\` têm de ficar **dentro** da pasta do plugin. Isso já quebrou a voz em instalação nova nos zips 1.9.2/1.9.3
  (ver `docs/re/packaging-layout.md`).
- **Duas cópias instaladas = o mesmo GUID carregado duas vezes.** Alguns gerenciadores (Vortex) usam o **nome do mod**
  como pasta (`plugins\Companions\`). Se a pasta dele existir, aponte o `DeployDir` para ela **ou** remova a outra.
- **Como saber que carregou:** o `BepInEx\LogOutput.log` (sempre ligado) precisa ter
  `Loading [AI Companions <versão>]` e `loaded successfully! (N methods patched)`. A janela do console vem
  **desligada** no BepInEx novo; para abrir, `[Logging.Console] Enabled = true` em `BepInEx\config\BepInEx.cfg` (vale no
  próximo boot).
- **Artefato do build:** `src/Companions/bin/Release/net472/Companions.dll` (dali o `DeployToPlugins` copia).
- **As datas dos docs estão adiantadas** em relação ao `git log` (os runs aparecem como 11/11/2026 e os commits são de
  04/11/2026). É uma divergência **conhecida e registrada** (`docs/backlog-construcao.md`, item 9) — não "corrija"
  números de run por causa disso.

## Arquitetura (`src/Companions/`)

| pasta | conteúdo |
|---|---|
| `Core/` | entrypoint (`CompanionsPlugin`), config (`ModConfig`), **localização** (`ModLocalization`), speech config, `CompanionLocale`, helpers (reflexão, texturas, input) |
| `Companion/` | setup, spawn/prefabs, aparência, tiers, skills, stamina, comida, descanso, **talk** (TTS), morte/respawn |
| `AI/` | movimento e combate (`CompanionAI`, `CompanionCombatAI`, `MHR_Mover`, tocha, água, portas) **+ a camada de IA**: `LlmClient`, `LlmIntent`, `CompanionOrders`, `CompanionChatAI`; as tabelas **puras** (`RadialIntent`, `PickupIntent`, `GatherIntent`, `InteractionIntent`, `OrderItems`, `BuildFailure`, `BuildSupportBelow`, `BuildBody`, `BuildSpread`, …) |
| `Jobs/` | harvest, farm, fish, hunt, cook, smelt, repair, upkeep, loot, tool keeper, **`MHR_Build`** (construção) e **`MHR_BuildGroup`** (grupo de obra), `MHR_Level` |
| `UI/` | painéis, menu radial, HUD, config, zone placers, fontes/inventário, **`ModUiLayers`** (fonte única das camadas `1701+`) |
| `Patches/` | patches Harmony nas classes vanilla |
| `DevBridge/` | módulo de dev (TCP loopback `127.0.0.1:47110`), desligado por padrão |

**Fluxo de uma ordem:** chat (`Chat.SendText`) ou voz (push-to-talk → Whisper local) → `CompanionChatAI` (prompt por
idioma) → `LlmClient` (POST compatível com OpenAI, `Task` + fila) → `LlmIntent` (parser) → `CompanionOrders` → **a
mesma ação** do radial/tecla. As **37** ações de `CompanionChatAI.ValidActions` são a fonte do `enum` do JSON schema.
As redes por palavra são a segurança fora do modelo: texto livre do jogador **nunca** é traduzido.

**LLM:** Groq (`openai/gpt-oss-20b`, padrão) → LM Studio/Ollama (`127.0.0.1:1234`) → OpenRouter, tudo por
`BaseUrl`/`Model`/`ApiKey` (a chave é `ConfigEntry`, **nunca** hardcoded). Só STT (Whisper.net) e TTS (Kokoro via
sherpa-onnx) rodam **locais, no processo**.

## Localização (5 idiomas)

- **PT-BR, EN, ES, FR, IT**. O **inglês é nativo no código** (`ModLocalization` defaults): **não existe**
  `Translations/English.json`. Os **4** JSONs são `Portuguese_Brazilian`, `Spanish`, `French`, `Italian`, em
  `Translations/` (UI) e `Speech/` (falas).
- O carregamento é por **sobrescrita**: o default inglês é escrito primeiro, o JSON sobrescreve só o que tem. Uma
  chave ausente do JSON mostra o **inglês** — **não** a chave crua.
- **Rótulos de config (`hc_cfg_<secao>_<chave>`) não têm default em inglês** (o F8 mostra o nome curto da opção). Eles
  vivem **só** nos 4 JSONs e são checados por `audit_translations.py --check-config` — uma opção nova no `ModConfig`
  exige a chave nos **4** arquivos no mesmo ciclo.
- `Speech/` fica **fora** de `Translations/` por segurança: o carregador de localização do Jotunn (se o jogador o
  tiver) trataria `Translations\<pasta>\*.json` como um idioma.
- **Idioma da documentação:** **público em EN** (`README.md`, `package/*`, `docs/wiki/**` com espelhos `-pt`/`-es`/`-fr`/`-it`,
  release notes), **interno em PT-BR** (`AGENTS.md`, `docs/**`, exceto wiki/release notes). **Comentários de código em
  inglês**, **logs em PT-BR**. `package/LEIA-ME.txt` fica em PT.

## Fluxo de trabalho (regras do jogador)

1. **Nada entra sem estar documentado no mesmo ciclo.** Documentação é entrega, não sobra.
2. **Código em branch**, nunca direto na `main`: `feat/<tema>` ou `fix/<tema>` → **o jogador testa em jogo** → merge na
   `main`. **Um commit por hipótese.** Docs e release podem ir direto na `main`. **`git push` não precisa de pedido**
   (regra do jogador, 11/11/2026): se o ciclo fechou verde e o jogador disse para subir, empurre para `origin`
   (`julianu37/AI-Companions`) com `-u` na primeira vez. Nunca `--force`; nada de `upstream`.
3. **Nenhum arquivo fica editado sem commit.** A árvore termina o ciclo limpa (`git status` vazio); trabalho pela
   metade é `wip(...)` na branch, não árvore suja.
4. Mensagem de commit: `tipo(escopo): resumo`. O arquiteto usa `git add -A`; **os demais agentes usam caminhos
   explícitos** e não commitam.
5. **Sem segredos** hardcoded: chave de LLM sempre por `ConfigEntry`/variável de ambiente.
6. **Sync do upstream é do `main`** — **NUNCA** da branch `claude/relaxed-archimedes-ecoh6h` (é o WIP do autor, a
   numeração dela não é release; ler os números dela como releases já causou erro de avaliação). Base atual: `1.5.4`
   (`267b8fe`); `main` do upstream: `1.5.8` (`8aa6f53`), 9 commits à frente.
7. **Não quebre o comportamento do upstream.** Nossas mudanças são tradução + camada de IA; lógica só com motivo
   documentado.
8. **Regra de regressão da construção:** um run que fecha **985/985, 0 puladas, 0 mortes, 0 `UNSUPPORTED`, watchdog 0**
   é o baseline — qualquer um deles aparecendo é **regressão**, reverta. O que foi revertido fica documentado **com o
   número que o matou**.

## Equipe de agentes e skills

18 agentes em `.opencode/agents/` (orquestrador: **`ai-companions-architect`**; especialistas: `valheim-specialist`,
`valheim-reverse-engineer`, `csharp-engineer`, `ai-npc-developer`, `ai-integration`, `llm-backend-dev`,
`harmony-patcher`, `valheim-netcode-dev`, `valheim-qa-engineer`, `valheim-mod-reviewer`, `multilanguage-verifier`,
`repo-wiki-publisher`, `valheim-docs-writer`, `valheim-mod-architect`, `valheim-asset-dev`, `valheim-mod-recon`,
`jotunn-developer` *(não usado)*). `experimental.subagent_depth = 2` em `.opencode/opencode.jsonc`.

9 skills em `.opencode/skills/`: `bepinex`, `jotunn`, `kokoro`, `valheim-ai-backend`, `valheim-ai-orders`,
`valheim-assets`, `valheim-devbridge`, `valheim-mod-research`, `valheim-reverse-engineering`.
**Carregue a skill da área antes de codar ou revisar.**

Permissões são por caminho (primeira barreira); a segunda é o `git status --porcelain` + `git diff --stat` do arquiteto
**antes de commitar** — nunca auto-relato, a delegação aninhada edita via subagente. O diff do harness **só adiciona**
asserção. Nenhum agente pode `--force`. O arquiteto **serializa** as execuções do harness (duas ao mesmo tempo
disputam `obj/bin`).

## Construção autônoma (a frente mais ativa)

`MHR_Build` constrói **peça única** (`prefab=`), **template** (`plan=hut|canopy|tent|house|house4|house6` — o `house`
é o 8x8 medido) e **`.blueprint`** (`<plugin>\blueprints`, migrada uma vez de
`BepInEx\config\AI-Companions\blueprints`, cópia sem sobrescrever). Ordem: **andar → piso → escada → parede → telhado →
porta** (porta sempre por último). Apoio, estações, coleta e retry são governados pelo **veredito do próprio jogo**
(`WearNTear`), não por simulação nossa.

Não redesenhe isso de cabeça: **`docs/re/building-notes.md`** é a fonte (§17 = runs medidos, um por §), e
**`docs/backlog-construcao.md`** é a fila de trabalho com o critério de aceite de cada item.

## Pendências abertas (não são bugs "a descobrir")

1. Navegação no andar em obra / a rota até a **cumeeira** (`no progress` em `wood_roof_45` a 48,8 m).
2. A **voz não passa pelo mixer do jogo** — só obedece `[AI] TtsVolume`.
3. **`gen_translations.py --check` falha** com **109 chaves** por idioma (o mapa indexa por texto PT e não cobre o bloco
   `hc_cfg_building_*` nem as falas de construção). É dívida de ferramenta, **não** lacuna de tradução — as auditorias
   reais (`audit_translations.py`) fecham em 0.
4. `DeckLift` deixando ~1 m entre piso e chão **prende corpos** (decisão de produto do jogador).
5. **Multiplayer é o ÚLTIMO item** e só entra se o jogador decidir investir (posse de ZDO no spawn, troca de posse no
   meio da obra, pagamento por baú sem `SetInUse`). O alvo do produto hoje é **single-player**.
6. Corpus de blueprints: **279 de 354** arquivos perdem linha por `recipe not unlocked` e o mod **pula em silêncio** —
   falta a tela e a fala dizendo qual receita falta (o dado já existe em `BuildBlueprintReport.SkippedReasons`).

## DevBridge (testar no jogo)

Módulo de dev na própria DLL (`src/Companions/DevBridge/`) + servidor MCP em `tools/mcp-devbridge/`. **Desligado por
padrão** (`[Advanced] DevBridge`), loopback only, porta **47110**; liga-se no `.cfg` e exige reiniciar o jogo (o log diz
`[DevBridge] listening on 127.0.0.1:47110`). `world_enter` já roda o setup: `devcommands` (uma vez), `tod 0.5`,
`god on` + `heal` e stamina infinita — **`god` no Valheim 1.0 trava o HP em 1 em vez de cancelar o dano**, por isso o
`heal` é obrigatório. `companions.order action=stop` cancela obra e nivelamento (a mesma parada do chat).
`docs/mcp-devbridge.md` tem a arquitetura e as receitas.

## Onde está o resto

- `docs/roadmap.md` — fases e status; `docs/plan-fases.md` — plano com mapa do upstream.
- `docs/re/camada-ia-e-construcao.md` — histórico detalhado da camada de IA e da construção (inclui um **apêndice**
  com o texto integral que antes vivia neste arquivo).
- `docs/re/building-notes.md` — a construção **medida**, run a run. `docs/plan-construcao.md` — o plano da Trilha E.
- `docs/plan-multi-build.md` — grupo de obra. `docs/backlog-construcao.md` — fila com critério de aceite.
  `docs/backlog-1.9.5.md` — o que ficou sem correção da revisão de 10 especialistas.
- `docs/re/packaging-layout.md` — layout do zip (e por que os modelos ficam dentro da pasta do plugin).
- `docs/re/blueprint-corpus.md`, `docs/re/navmesh-tiles.md`, `docs/re/support-rule-wearntear.md`,
  `docs/re/stt-*.md`, `docs/re/tts-kokoro-sherpa-onnx.md` — engenharia reversa e medições pontuais.
- `docs/wiki/` — **fonte da GitHub Wiki** (repo separado `AI-Companions.wiki`); publicar com
  `python tools/publish_wiki.py` (`--check` valida a fonte e avisa o passo manual). `docs/SESSION_HANDOFF.md` é
  **histórico do upstream**, não handoff nosso.

## Definição de pronto

Compila em Release sem erro nem aviso · carrega no Valheim 1.0 sem exceção no `LogOutput.log` · textos novos nos **4**
JSONs (e nas falas, quando falado) · harness e MCP verdes · sem regressão perceptível de performance · documentado no
mesmo ciclo.
