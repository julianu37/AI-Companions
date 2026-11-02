# Fontes de referência (terceiros)

Sempre que possível, confirmamos APIs nas **fontes oficiais** e buscamos **ideias/implementações** em
projetos de terceiros com licença permissiva. Leia as **regras de uso** no fim antes de reaproveitar.

| Fonte | Licença | Para que consultar |
|---|---|---|
| **Offline Companions Reborn** (MaddhattrRocks) | **MIT** ✅ uso livre | Mod companheiro para **Valheim 1.0**: IA de movimento/combate, **jobs** (coleta de madeira/pedra/minério/forrageio, fazenda, pesca, caça, cozinha, fundição, reparo, reabastecimento, ordenar baús), **UI** (painéis, menu radial, HUD, painel de config), patches, "tool keeper" |
| **BepInEx** | ver `LICENSE` do repo | Plugin, config, logging, Harmony |
| **Jotunn** | ver `LICENSE` do repo | Managers, prefabs, GUI, localização — **não é usado por este mod** (0 uso no código, sem `[BepInDependency]`, sem referência no build; a única relação é defensiva: `Speech/` fica fora de `Translations/` por causa do carregador de localização dele). Fica aqui como referência de framework |
| **Whisper.net** / **whisper.cpp** | **MIT** ✅ redistribuído | STT local das ordens por voz (`WhisperStt`, Fase 3.3) |
| **sherpa-onnx** (k2-fsa) | **Apache-2.0** ✅ redistribuído | TTS local da fala do companheiro (`CompanionTts`, Fase 3.4) |
| **Kokoro-82M** (hexgrad) | **Apache-2.0** ✅ redistribuído | O modelo de voz que o TTS usa (`tts\kokoro\`) |
| **PlanBuild** (sirskunkalot) | **WTFPL** ✅ só o **formato** `.blueprint` | O formato de arquivo que a E7 lê (BiomeBlueprints/valheimians); **não** é dependência (sem Jotunn no mod) |
| **BiomeBlueprints** (OverDrive) — <https://thunderstore.io/c/valheim/p/OverDrive/BiomeBlueprints/> · download direto: <https://thunderstore.io/package/download/OverDrive/BiomeBlueprints/1.0.3/> | sem licença declarada ⚠️ uso **local** | 353 builds `.blueprint` de onde saiu o `hs_meadows_hut` do teste; **não** vai no pacote |
| **valheim-mods** (HardHeadHackerHead) | **MIT** ✅ uso livre | Coleção de mods de Valheim **com as sources**: **BuildOrders** (o alvo das fases E7/E8), `BuildFromChests`/`CraftFromChests`/`FeedFromChests` (material de baú), `Shared`/`Scripts.Engine`. É de lá que vem a referência do **apoio/estabilidade** e dos **fantasmas** |
| **Thrall** (ego.ai) | **All rights reserved** ⚠️ | Apenas referência **conceitual**; **não** copiar código nem redistribuir binários (ver `NOTICE.md`) |

---

## Offline Companions Reborn (MIT)

- Repositório: <https://github.com/MaddhattrRocks/Offline-Companions-Reborn>
- Também está registrado como **reference do OpenCode** (`offline-companions`) em
  `.opencode/opencode.jsonc` — os agentes podem lê-lo diretamente.
- Fork de compatibilidade com o **Valheim 1.0** do mod "Offline Companions" (ProfMags).

### Onde olhar (`src/Companions/`)

| Pasta | Conteúdo útil |
|---|---|
| `AI/` | `CompanionAI`, `CompanionCombatAI`, escolha de arma/escudo, tocha, segurança na água, portas, `MHR_Mover` |
| `Jobs/` | Modos de trabalho: **harvest (wood/stone/ore/forage)**, **loot (catar os drops da criatura abatida — `MHR_Loot`)**, farm, fish, hunt, cook, smelt, repair (gear), repair/restock (buildings, fires, **chest sorting**), regras de gather/seed, **tool keeper** |
| `UI/` | Painéis, **menu radial**, HUD, painel de config, zone placers, fixes de fonte e inventory-grid |
| `Patches/` | Patches Harmony em classes vanilla |
| `Diagnostics/` | Helpers de log (`[AIGate]`, `[EQUIPDIAG]`, `[GRIDDIAG]`, `[UiDiag]`, `[Diag]`) |
| `Core/` | Entry point, config, localização, speech config, helpers (reflexão, texturas, input) |
| `Companion/` | Setup, spawn/prefabs, aparência, tiers, skills, stamina, comida, descanso, talk, morte/respawn |

- Notas de desenvolvimento/backlog: `docs/DEV_NOTES.md`.
- Docs do jogador: `package/GUIDE.md`, `package/CONFIGURATION.md`.

### Como usar

- **Reaproveitar ideias e padrões** livremente.
- Se **copiar porções substanciais de código**, mantenha o aviso MIT — ver
  [`THIRD_PARTY_NOTICES.md`](../THIRD_PARTY_NOTICES.md) (atribuição obrigatória).
- Cite a fonte no commit/CHANGELOG quando a implementação for derivada.

---

---

## valheim-mods (HardHeadHackerHead, MIT)

- Repositório: <https://github.com/HardHeadHackerHead/valheim-mods> — uma pasta por mod, **com o código-fonte**.
- **MIT**, o jogador confirmou o uso livre (01/11/2026). Ao derivar algo, cite a fonte no commit/CHANGELOG (regra 3).
- O que interessa hoje:

| Mod / arquivo | Para que consultar |
|---|---|
| `BuildOrders/Stability.cs` | **A referência do "sem peça solta no ar":** ele simula o `WearNTear.UpdateSupport` do jogo (caixas de colisão da peça crescidas **0,15 m**; o chão ou qualquer sólido que não é peça dá apoio cheio; de uma peça encostada, o apoio dela menos a perda do material pela distância entre os centros de massa - horizontal ou vertical; dois apoios em lados opostos são **mediados**; abaixo do mínimo do material a peça quebra e o que estava em cima é recalculado). Lido na E6 |
| `BuildOrders/Ghosts.cs` | Como o jogo monta o fantasma de colocação (instanciar com rede desligada e tirar física/som/luz/partículas) - o mesmo caminho do `BuildSite.CreateGhost` |
| `BuildOrders/Orders.cs`, `Plugin.Api.cs` | A API dos planos (`Count`/`Next`/`Place`/`Blockers`) que a **E7** vai chamar |
| `BuildFromChests`, `CraftFromChests` | Material vindo dos baús (o nosso `BuildStock` já faz isso; serve de conferência) |

> **O que não copiamos:** o `Stability.cs` é uma **simulação** do apoio (necessária para ele, que constrói planos
> grandes de uma vez). A E6 usa o **próprio jogo** (`WearNTear.UpdateSupport`/`HaveSupport` por reflexão) depois de
> cada peça, o que é mais simples e é a mesma resposta - ver `docs/re/building-notes.md` §11.

## Regras de uso

1. **MIT (Offline Companions Reborn):** pode usar/copiar/modificar/distribuir — **desde que mantenha o
   aviso de copyright/licença** em cópias substanciais.
2. **Thrall (ego.ai):** "All rights reserved" — **somente leitura como referência**; não copiar código
   nem redistribuir binários.
3. Ao implementar algo inspirado em terceiros, **cite a fonte** no commit e no `CHANGELOG.md`.

## Binários redistribuídos (voz local)

O pacote do mod embute os binários das duas metades da voz. Todos são de licença permissiva e mantêm o aviso:

| Binário (na pasta do plugin) | Origem | Licença |
|---|---|---|
| `Whisper.net.dll`, `runtimes\win-x64\whisper.dll`, `ggml-*.dll` | [Whisper.net](https://github.com/sandrohanea/whisper.net) + [whisper.cpp](https://github.com/ggerganov/whisper.cpp) | MIT |
| `sherpa-onnx.dll`, `runtimes\win-x64\native\sherpa-onnx-c-api.dll`, `onnxruntime.dll` | [sherpa-onnx](https://github.com/k2-fsa/sherpa-onnx) (NuGet `org.k2fsa.sherpa.onnx` 1.13.8) | Apache-2.0 |
| `tts\kokoro\model.int8.onnx`, `voices.bin`, `tokens.txt`, `espeak-ng-data\` | [Kokoro-82M](https://huggingface.co/hexgrad/Kokoro-82M) (via os modelos do sherpa-onnx) | Apache-2.0 (ver `tts\kokoro\LICENSE`) |

> Os **modelos** (Whisper `.bin` e Kokoro) são baixados/copiados para a pasta do plugin e **não** ficam no
> repositório (são grandes).

### Fontes da voz (Kokoro)

| Fonte | Licença | Para que consultar |
|---|---|---|
| [VOICES.md](https://huggingface.co/hexgrad/Kokoro-82M/blob/main/VOICES.md) | doc do modelo (Apache-2.0) | As 54 vozes: gênero, nota de **qualidade/treino** e a faixa ideal de texto (**100–200 tokens**; fraco abaixo de 10–20, apressado acima de 400) |
| [hexgrad/kokoro](https://github.com/hexgrad/kokoro) | Apache-2.0 | `KPipeline` e os `lang_code` (`a`/`b`/`e`/`f`/`h`/`i`/`j`/`p`/`z`); espeak-ng como fallback de G2P |
| [k2-fsa — Kokoro](https://k2-fsa.github.io/sherpa/onnx/tts/pretrained_models/kokoro.html) | Apache-2.0 | O **export que a gente usa**: mapa **`sid → nome`** (42/43/44 = `pf_dora`/`pm_alex`/`pm_santa`), RTF e os nomes dos arquivos do pacote |
| [remsky/Kokoro-FastAPI](https://github.com/remsky/Kokoro-FastAPI) | Apache-2.0 | Alternativa **HTTP** (endpoint compatível com a OpenAI), mistura de vozes e streaming — só se o TTS sair do jogo |

> O **mapa de `sid` vale para o export do sherpa**, não para a ordem alfabética do Hugging Face (a do HF tem 54
> vozes; o mapa do sherpa v1_0 tem 53 — falta `em_santa`). A skill `.opencode/skills/kokoro/` consolida o catálogo,
> a integração e as armadilhas.

## Fixtures de teste da E7 (o leitor `.blueprint`)

As fixtures do harness e dos testes em jogo vêm do repositório do **PlanBuild** (`PlanBuildTest/resources/`:
`TestBox_V1.blueprint`, `TestBox_V2.blueprint`, `Tree_V2_SnapPoints.blueprint`) - **WTFPL** - e do **BiomeBlueprints**
(`hs_meadows_hut.blueprint`). Elas **não** vão no zip: o leitor é testado com texto embutido no harness e o jogador
baixa as builds dele.
