# Fontes de referência (terceiros)

Sempre que possível, confirmamos APIs nas **fontes oficiais** e buscamos **ideias/implementações** em
projetos de terceiros com licença permissiva. Leia as **regras de uso** no fim antes de reaproveitar.

| Fonte | Licença | Para que consultar |
|---|---|---|
| **Offline Companions Reborn** (MaddhattrRocks) | **MIT** ✅ uso livre | Mod companheiro para **Valheim 1.0**: IA de movimento/combate, **jobs** (coleta de madeira/pedra/minério/forrageio, fazenda, pesca, caça, cozinha, fundição, reparo, reabastecimento, ordenar baús), **UI** (painéis, menu radial, HUD, painel de config), patches, "tool keeper" |
| **BepInEx** | ver `LICENSE` do repo | Plugin, config, logging, Harmony |
| **Jotunn** | ver `LICENSE` do repo | Managers, prefabs, GUI, localização |
| **Whisper.net** / **whisper.cpp** | **MIT** ✅ redistribuído | STT local das ordens por voz (`WhisperStt`, Fase 3.3) |
| **sherpa-onnx** (k2-fsa) | **Apache-2.0** ✅ redistribuído | TTS local da fala do companheiro (`CompanionTts`, Fase 3.4) |
| **Kokoro-82M** (hexgrad) | **Apache-2.0** ✅ redistribuído | O modelo de voz que o TTS usa (`tts\kokoro\`) |
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
