# 1.9.1 — a ~54 MB lighter package

![AI Companions — AI NPC companions for Valheim, in five languages](https://raw.githubusercontent.com/julianu37/AI-Companions/main/docs/images/banner.png)

> **The package no longer ships the `ggml-base-q5_1.bin` model (57 MB).** The mod never used it: the `[AI] SttModel`
> default is `ggml-small-q5_1.bin` (181 MB), which is much more accurate in every language. Everything else is the
> same as **1.9.0** — five languages, automatic voice and language, the per-language LLM contract and the intent
> safety nets.

## What changed

- **Full package: ~368 MB → ~314 MB.** Only the model that was not used left; the Whisper `small` model, the Kokoro
  voice model and everything else are still inside.
- **The `base` model is still supported:** download `ggml-base-q5_1.bin` into `whisper\` and point `[AI] SttModel`
  at it if you want the faster, less accurate one (638 ms per 2 s clip against 2187 ms of the small model, on a
  16-core CPU with 8 threads).
- **The `[AI] SttModel` description** now says the `base` is an **optional download** (it used to say "it stays on
  disk if you want to go back").
- **No behaviour change and no new config key:** saves, worlds, companions, houses, inventory and the `.cfg` are
  untouched. The no-models package keeps the same contents (~28.5 MB).

## Files

| File | Size | What comes in it |
|---|---|---|
| `AI_Companions-1.9.1.zip` | ~314 MB | The mod **and** the voice models (STT + TTS) — **recommended** |
| `AI_Companions-1.9.1-sem-modelos.zip` | ~28.5 MB | Only the mod and the translations (no Whisper model, no Kokoro model — it does bring `tokens.txt` and `espeak-ng-data`) |

SHA-256: `AI_Companions-1.9.1.zip` = `368f3d081c221a32f7d4a173a1d8e1ef6fe1a60d9906a9ec737bea6682fc5396` ·
`…-sem-modelos.zip` = `c5bec6ee55af2b0a6dfcd0ab6e585ff3d1509d539afc1efa5cef3f8893613b1f`

## How to update

1. Close the game.
2. Delete the old `BepInEx\plugins\AI-Companions\` folder (only one copy of the mod) and extract this package — the
   folder is `BepInEx\plugins\AI-Companions\`.
3. **The `.cfg` is not touched**: the reset happened in **1.8.3**, not here. Coming from 1.8.3 or 1.9.0, your settings
   and your API key stay exactly as they are. Coming from **1.8.2 or older**, the 1.8.3 rules apply: the config file
   is `BepInEx\config\AiCompanions.cfg` now, the settings go back to the defaults and the API key has to be pasted
   again in the `I` panel.

## Requirements and installation

- **Valheim 1.0.x** + **BepInExPack_Valheim 5.4.2351** (BepInEx 5).
- **An LLM**: **Groq** (default — the free key and internet) **or** a local **LM Studio/Ollama** **or**
  **OpenRouter** (works, but the free tier is slower).
- **A microphone** only for the voice orders.

Full step-by-step, with pictures: https://github.com/julianu37/AI-Companions/wiki/Installation

## In action

| | | |
|---|---|---|
| ![Chat order](https://raw.githubusercontent.com/julianu37/AI-Companions/main/docs/wiki/images/en-companions-chat-orders.jpg) | ![AI panel](https://raw.githubusercontent.com/julianu37/AI-Companions/main/docs/wiki/images/en-ai-panel.jpg) | ![Several companions](https://raw.githubusercontent.com/julianu37/AI-Companions/main/docs/wiki/images/en-multiple-companions-1.jpg) |
| **Text order** in the chat: he understands and answers | **AI panel** (key `I`): voice, memory, conversation, LLM | **Up to 10 companions**, each with its own tab |

## Credits and license

**Independent project** based on [Offline Companions Reborn](https://github.com/MaddhattrRocks/Offline-Companions-Reborn)
(**MIT**), by **Maddhattr**, which in turn is a fork of *Offline Companions*, by **ProfMags** (**MIT**). The full
attribution is in `LICENSE`, `NOTICE.md` and `THIRD_PARTY_NOTICES.md`.

Voice: [Whisper.net](https://github.com/sandrohanea/whisper.net) + **openai/whisper** models (MIT) and
[Kokoro-82M](https://huggingface.co/hexgrad/Kokoro-82M) via [sherpa-onnx](https://github.com/k2-fsa/sherpa-onnx)
(Apache-2.0).
