> 🌍 **English** · [Português](Requirements-pt) · [Español](Requirements-es) · [Français](Requirements-fr) · [Italiano](Requirements-it)

# Requirements

What you need to have before installing **AI Companions**.

The mod works in **two independent layers**: the **text orders in the chat** (they need an **LLM**) and the
**voice orders** (they need a microphone). You can use **only the chat**, typing — the voice is optional.

## Required

| Item | Version / detail |
|---|---|
| **Valheim** | **1.0.x** (Unity 6 engine — tested on **1.0.20**) |
| **BepInEx** | **BepInExPack_Valheim 5.4.2351** (BepInEx 5.4.23.5) — the mod is a BepInEx 5 plugin |
| **An LLM** | For the text/voice orders to be understood. The default is **Groq**: it only needs **internet** and a **free key** (see below). The **local** alternative (**LM Studio** or **Ollama**) is **optional** and sends nothing out; **OpenRouter** still works, but its free tier is slower. See **[LLM (Groq, LM Studio or OpenRouter)](LLM)** |
| **Disk space** | See the table below |

> **The LLM is required; the path is not.** **Groq** is the default: the **free key**
> (https://console.groq.com/keys) and internet are enough — and the text of the orders **leaves your machine**.
> **LM Studio/Ollama** is the **local, optional alternative**: nothing leaves, but you download a model (~1.9 GB)
> and keep the server on while you play. **OpenRouter** also works (free key at https://openrouter.ai/keys) and
> stays as an **alternative**, because its free tier is slower. The three paths — in that order of preference —
> are in **[LLM (Groq, LM Studio or OpenRouter)](LLM)**.

## Optional

| Item | What for |
|---|---|
| **Microphone** | Only for the **voice orders** (push-to-talk). Without one, use the chat |
| **GPU** | Only makes a difference in the **local** option: the LLM in LM Studio runs **much** faster with a GPU. On Groq (and on OpenRouter), the model runs on their server |
| **Voice models** | They already come in the **full** package. In the **no-models** package you download them separately. The same Kokoro `voices.bin` carries the voices of the **five languages** |

## Disk space

| What | Size |
|---|---|
| **Full** package (mod + models) | ~314 MB (zip) |
| **No-models** package (mod + translations) | ~28.4 MB (zip) |
| Mod installed (plugin folder) | ~371 MB |
| Whisper model `ggml-small-q5_1.bin` (STT default) | 181 MB |
| Kokoro model (TTS) `model.int8.onnx` | 109 MB |
| Kokoro voices `voices.bin` | 27 MB (the voices of the five languages: PT-BR, EN, ES, FR and IT) |
| **Local** LLM model (`qwen2.5-3b-instruct` Q4_K_M, LM Studio) — local option only | ~1.93 GB |

> Set aside something around **1 GB** for the mod and the voice models, plus the space of the LLM model.

## Hardware

- **CPU:** the TTS (Kokoro-82M) synthesizes on the **CPU**. With **4 cores** it already generates faster than
  real time (the speech comes out ahead of the companion). The STT (Whisper) also runs on the CPU.
- **GPU:** it only matters in the **local** option — in LM Studio a GPU makes a huge difference. A 3B model runs
  fine on the CPU, but it is much faster with a GPU. On Groq (and on OpenRouter), their server runs the model.
- **RAM:** in the local option, a quantized 3B model (Q4) takes about **2 GB** of memory, on top of the game. With
  Groq (or OpenRouter) there is no such cost.

## How to install BepInEx

BepInEx is the plugin loader for Valheim. Install it **before** the mod:

1. Download **BepInExPack_Valheim 5.4.2351** from the BepInEx releases page:
   https://github.com/BepInEx/BepInEx/releases
2. Extract the contents into the game folder, so that `winhttp.dll` and the `BepInEx\` folder end up **in the
   root** of Valheim (`C:\Program Files (x86)\Steam\steamapps\common\Valheim` by default).
3. Run Valheim **once** and close it — BepInEx creates the folder structure
   (`BepInEx\plugins\`, `BepInEx\config\`, `BepInEx\LogOutput.log`).

> If BepInEx does not load, check the official documentation: https://docs.bepinex.dev/

After that, follow the mod's **[Installation](Installation)**.

## Next step

➡️ **[Installation](Installation)**

<!-- D4: add Español/Français/Italiano -->
