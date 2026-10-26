# 1.9.0 — five languages: the mod follows the game

![AI Companions — AI NPC companions for Valheim](https://raw.githubusercontent.com/julianu37/AI-Companions/main/docs/images/banner.png)

> **Portuguese (Brazil), English, Spanish, French and Italian.** The interface, the companion's speech, the
> personality presets and the LLM contract all follow the **game's language**, and the voice (TTS) and the speech
> recognition (STT) pick the language and the voice by themselves. Changing the game's language **no longer needs a
> restart**.

## What changed

### Automatic voice and language

- **`[AI] VoiceLanguage`, `[AI] TtsLanguage` and `[AI] TtsVoice` are automatic by default** (`auto` / `auto` / `0`):
  the mod resolves them from the game's language — pt-BR → `pf_dora`, English → `af_heart`, Spanish → `ef_dora`,
  French → `ff_siwis`, Italian → `if_sara`. **An explicit value still wins**, and the F8 and the AI panel show the
  **effective** value (`Automatic (en-US · af_heart)`).
- **`[AI] VoiceInitialPrompt` is automatic too**: each language has its own order vocabulary (better recognition).
- **A companion whose stored voice belongs to another language** speaks with the default voice of the active
  language — the choice stays in the ZDO, so switching the game back restores it.
- **Italian is new** (`if_sara` / `im_nicola`), with interface, speech, presets and contract of its own.

### In every language

- **Interface**: 737 keys per language, including the F8 labels and the tab names.
- **Companion speech**: 186 lines across 15 categories.
- **LLM contract**: the rules and examples are written in the game's language; the action tokens are the same.
- **Personality presets** in the five languages (the player's own text is never translated).

### Small models got more reliable

- **`RadialIntent`** (new): a keyword safety net for the actions small models confuse — stances, wander/stop
  wandering, accept/ignore commands, smelt/cook/hunt. Measured with the local `qwen2.5-3b-instruct`:
  **30/30 actions in all five languages** (150 cases).
- **`GatherIntent`** (new): the same idea for gather orders that the model answers as `pickup`/`none` — the resource
  and the amount are read from the player's words.
- **`SpeechConfig`** retries the language file after 30 s instead of staying in English until a restart.

### Documentation

- **The docs and the wiki are written in English from now on.** The wiki keeps Portuguese mirrors (`-pt`); Spanish,
  French and Italian pages come next.

## How to update

1. Close the game.
2. Delete the old `BepInEx\plugins\AI-Companions\` folder (only one copy of the mod) and extract this package — the
   folder is `BepInEx\plugins\AI-Companions\`.
3. **Coming from 1.8.3? Nothing is reset.** The `.cfg` reset happened in **1.8.3**; this release only migrates the
   old automatic defaults (`pt`, `pt-br`, `42`, the old prompt) to `auto` **once** — in Portuguese the voice is the
   same (`pf_dora`), so nothing changes for a pt-BR player.
4. **Coming from 1.8.2 or older?** The 1.8.3 rules apply: the config file becomes `BepInEx\config\AiCompanions.cfg`
   (`AiCompanions.cfg`), the settings go back to the defaults and the API key has to be pasted again in the `I` panel.
5. In the `I` panel → **LLM settings**, pick the provider, paste your key and press **Test connection**.

## Files

| File | Size | What comes in it |
|---|---|---|
| `AI_Companions-1.9.0.zip` | ~368 MB | The mod **and** the voice models (STT + TTS) — **recommended** |
| `AI_Companions-1.9.0-sem-modelos.zip` | ~28.5 MB | Only the mod and the translations (it already comes with `tokens.txt` and `espeak-ng-data`) |

SHA-256: `AI_Companions-1.9.0.zip` = `ddaf678739b348b8b1585fb66a2a2b7b45e6f601285836cb70a6c8d8905caebd` ·
`…-sem-modelos.zip` = `905f8006143547a26276088dd5b2608eed117acfc3a909830dc834b6c3980d95`

## Requirements and installation

- **Valheim 1.0.x** + **BepInExPack_Valheim 5.4.2351** (BepInEx 5).
- **An LLM**: **Groq** (default — just the free key and internet) **or** a local **LM Studio/Ollama** **or**
  **OpenRouter** (works, but the free tier is slower).
- **Windows x64** for both voice halves; a **microphone** only for the voice orders.

Full step-by-step, with pictures: https://github.com/julianu37/AI-Companions/wiki/Installation

## In action

| | | |
|---|---|---|
| ![AI panel](https://raw.githubusercontent.com/julianu37/AI-Companions/main/docs/wiki/images/en-ai-panel.jpg) | ![Several companions](https://raw.githubusercontent.com/julianu37/AI-Companions/main/docs/wiki/images/en-multiple-companions-1.jpg) | ![Radial menu](https://raw.githubusercontent.com/julianu37/AI-Companions/main/docs/wiki/images/en-radial-menu.jpg) |
| **AI panel** (key `I`): voice, memory, conversation | **Several companions**, each with its own tab | **Radial menu**: hold the interact key |

## Credits and license

**Independent project** based on [Offline Companions Reborn](https://github.com/MaddhattrRocks/Offline-Companions-Reborn)
(**MIT**), by **Maddhattr**, which in turn is a fork of *Offline Companions*, by **ProfMags** (**MIT**). The full
attribution is in `LICENSE`, `NOTICE.md` and `THIRD_PARTY_NOTICES.md`.

Voice: [Whisper.net](https://github.com/sandrohanea/whisper.net) + **openai/whisper** models (MIT) and
[Kokoro-82M](https://huggingface.co/hexgrad/Kokoro-82M) via [sherpa-onnx](https://github.com/k2-fsa/sherpa-onnx)
(Apache-2.0).
