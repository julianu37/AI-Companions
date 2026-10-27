> 🌍 **English** · [Português](Credits-and-license-pt) · [Español](Credits-and-license-es) · [Français](Credits-and-license-fr) · [Italiano](Credits-and-license-it)

# Credits and license

**AI Companions** is an **independent project** that exists on top of other people's work. This page says who did
what.

---

## Upstream (the base of the mod)

| Project | Author | License |
|---|---|---|
| **Offline Companions Reborn** | **MaddhattrRocks** | **MIT** |
| *Offline Companions* (the original) | **ProfMags** | MIT |

https://github.com/MaddhattrRocks/Offline-Companions-Reborn

**Comes from there:** the companion itself — the jobs (gathering, farming, fishing, hunting, cooking, smelting,
repairing, loot), the food and the stamina, the skills, the home and the rest, the portals and the cart, the movement
and combat AI, the radial menu and the whole base UI. Our project's AI layer is only a **new entry point** for those
same actions.

> Offline Companions Reborn, in turn, is a fork of *Offline Companions*, by **ProfMags**.

---

## What is ours

What the **AI Companions** project adds:

1. **The localization in five languages** — Portuguese (Brazil), English, Spanish, French and Italian: 737 interface
   keys and 186 companion lines per language, audited (no English text visible to a player of those languages), with
   **English as the native language**.
2. **The AI layer:**
   - Orders in **natural language** by **text in the chat** and by **voice** (push-to-talk + local STT).
   - **Personality** (presets or free text), **memory** per companion and **free conversation**.
   - **Local TTS** (Kokoro-82M through sherpa-onnx): the companion **speaks** at his position in the world.
   - The **AI panel** (key `I`).
3. **Tweaks and fixes:**
   - Bigger gathering radius and random chatter off by default.
   - The `V` key no longer triggers the game's *Toggle Auto-Pickup* (conditional patch).
   - The voice echo (it was the game's global reverb zone).
   - The stone order that did not stop at the target.
   - The mod's panels above the game HUD (the swallowed click on `F8`).

---

## Third parties

| Component | What it is | License / link |
|---|---|---|
| **Whisper.net** | .NET binding for whisper.cpp, used in the STT | MIT — https://github.com/sandrohanea/whisper.net |
| **whisper.cpp** | Whisper implementation in C/C++ | MIT — https://github.com/ggml-org/whisper.cpp |
| **Whisper ggml models** | The `ggml-*.bin` files | MIT — https://huggingface.co/ggerganov/whisper.cpp |
| **sherpa-onnx** | TTS/STT runtime (ONNX) used in the TTS | Apache-2.0 — https://github.com/k2-fsa/sherpa-onnx |
| **Kokoro-82M** | The voice model the companion uses | Apache-2.0 — https://huggingface.co/hexgrad/Kokoro-82M |
| **onnxruntime** | The runtime that executes the ONNX models (comes with sherpa-onnx) | MIT — https://github.com/microsoft/onnxruntime |
| **espeak-ng** | Phonemization used by Kokoro (comes as **data**, in `espeak-ng-data\`) | GPL-3.0, the data only — https://github.com/espeak-ng/espeak-ng |
| **Groq** | The service that serves the free LLM by default (nothing of it is redistributed) | https://console.groq.com — use subject to the service terms |
| **OpenRouter** | The remote LLM service kept as an alternative (nothing of it is redistributed) | https://openrouter.ai — use subject to the service terms |
| **LM Studio** | The application that serves the local LLM | https://lmstudio.ai |
| **Qwen2.5** | The family of the recommended model for the local mode (`qwen2.5-3b-instruct`) | Apache-2.0 — https://huggingface.co/Qwen |
| **gpt-oss** | The open model served by Groq by default (`openai/gpt-oss-20b`) | Apache-2.0 — https://huggingface.co/openai/gpt-oss-20b |

> The full texts of the third-party licenses travel **inside the package**, in the `LICENSES\` folder.
>
> **Conceptual reference:** **Thrall** (ego.ai) was studied as a reference for ideas (*all rights reserved*).
> **No** code or binary of it is redistributed here.

---

## Project license

**MIT.** You can use, modify and redistribute it, keeping the copyright notices.

The full files are in the repository:

- [`LICENSE`](https://github.com/julianu37/AI-Companions/blob/main/LICENSE)
- [`THIRD_PARTY_NOTICES.md`](https://github.com/julianu37/AI-Companions/blob/main/THIRD_PARTY_NOTICES.md)
- [`NOTICE.md`](https://github.com/julianu37/AI-Companions/blob/main/NOTICE.md)

> The `LICENSES\` folder that comes with the installed mod brings the license texts of the third-party components
> distributed with it.
