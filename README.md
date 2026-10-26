# AI Companions

![AI Companions — AI NPC companions for Valheim](docs/images/banner.png)

> An **AI NPC companion** for **Valheim 1.0**: you **speak or type** and he understands and acts. The UI and
> the companion's speech follow the **game's language**.

This project is an **independent project** based on [Offline Companions Reborn](https://github.com/MaddhattrRocks/Offline-Companions-Reborn)
(**MIT**) with two things of ours on top:

1. **A complete PT-BR translation** (interface, menus, panels and the companion's speech) over a mod that is now
   **multilingual**, with **English as the native language**: the panels follow the **game's language**.
2. **An AI layer**: orders in **natural language** (text in the chat or **voice** through push-to-talk), with
   **personality**, **memory** and **conversation** — using an **LLM** (the **Groq** by default, or a **local**
   model) and **local** speech recognition.

The companion itself (jobs, food, stamina, skills, home, rest, portals, cart, radial menu) comes from the upstream
and keeps working the same — the AI is only a **new entry point** for the same actions.

> **Companion types:** the **dverger** (**Mage**, **Fire Mage**, **Ice Mage** and **Support**) are **combat**
> types — the ring shows only the combat/support actions, and the **work** orders (chat, voice or the command
> key) are **refused** with *"I'm a fighter, I don't do that work."*. To work, use the **Companion**
> type (details in the **[wiki](https://github.com/julianu37/AI-Companions/wiki/How-it-works#companion-types)**).

## 🎬 In action

[![Watch: "This mod changes EVERYTHING in Valheim! AI Companions"](docs/images/video-thumb.jpg)](https://www.youtube.com/watch?v=40FnVEIbXyo)

> 🎥 **"This mod changes EVERYTHING in Valheim! AI Companions"** — a video from the **SuaTV** channel showing the
> mod in game. **[▶️ Watch on YouTube](https://www.youtube.com/watch?v=40FnVEIbXyo)** (clicking the image opens it too)

---

## What it does

### 🗣️ Text and voice orders

| How | Example |
|---|---|
| **Voice** (hold **`V`**, speak, release) | *"get 10 stones"* · *"chop some wood"* · *"attack the greydwarf"* |
| **Chat** (the game's chat key) | *"collect 10 wood"* · *"follow"* · *"stop following me"* · *"equip the bronze axe"* |

The speech is transcribed **inside the game** (no external voice service) and the text goes into the **same**
path as the typed orders: order, conversation, personality and memory work the same way.

**Examples that work today:**
`collect 10 wood` · `get 10 stones` · `mine iron` · `follow` · `stop` · `stop following me` ·
`come here` · `attack the greydwarf` · `kill the boar` · `equip the bronze axe` · `take the pickaxe` ·
`drop the stone` · `throw away 5 wood` · `open your inventory` · `stay on the defensive` · `good morning` ·
`how are you?`

> The number in the order is a **target** counted in **his inventory**: if he already has 10 wood and you ask for
> 10, he says *"We already have enough."* and does not go out to gather.

> Ask to see what he carries (*"open your inventory"*, *"show me what you carry"*) and the **companion's
> inventory window** opens for you — the same one from the interact key, with name, food and equipment.
> *"Close the inventory"* closes it.

### 🧠 Personality, memory and conversation

- **Personality** through a **preset** (`viking`, `mago`, `sarcastico`, `guia`) or **free text** — it goes into the
  LLM prompt, so he answers in the tone you chose.
- **Short memory** of the latest exchanges (per companion).
- **Free conversation**: *"good morning"*, *"what did you think of my house?"* — he answers as a character.

### 🔊 He speaks (local synthesized voice)

The companion **says out loud** what he answers, at his position in the world (not in the middle of the screen).
The voice is synthesized **inside the game** by **Kokoro-82M** (through `sherpa-onnx`, CPU) — no external service,
no child process, nothing leaves your machine. Three voices in **Brazilian Portuguese**:

| `TtsVoice` | Voice |
|---|---|
| **42** (default) | `pf_dora` — female |
| **43** | `pm_alex` — male |
| **44** | `pm_santa` — male |

> **What he speaks:** the model's reply (his own way — *"Leave it to me."*) and/or the mod's own line that
> appears in the chat (*"Ok: gather wood (10)."*, *"We already have enough."*). The two options are separate
> (`TtsSpeakReplies` / `TtsSpeakModLines`) and the rule is **the voice follows what is written on the screen**: in an
> order the mod's line wins (turn `TtsSpeakModLines` off to hear the model's reply instead). Nothing is spoken
> twice.

> **No game stalls:** the synthesis runs on a background thread and the speech is assembled **sentence by
> sentence** — the first sentence already starts playing while the next one is generated. Repeated lines come
> from the **cache**, instantly. Quick test in the game console (`F5`): `modia_tts_teste`, or
> `modia_tts_teste Good morning, boss!` to speak a text of your own.

### 🎛️ AI panel (key `I`)

Press **`I`** at any time (no companion needs to be nearby) to adjust the AI **in game**, without editing the
`.cfg`:

| What | How |
|---|---|
| **Personality** | Pick a **preset** (Default, Loyal Viking, Sarcastic Mage, Sarcastic, Patient Guide) or write it **in your own words** and click **Apply** — it takes effect on the next reply |
| **Voice** | Pick one of the 3 pt-BR voices and press **Listen** to hear a sample sentence right away |
| **Select your LLM provider** | The dropdown above the **server URL**: **Groq**, **LM Studio** or **OpenRouter** fill in the **URL and the model** (it applies after **Apply**; the key is not touched). An Ollama/own-server URL shows up as **Custom** and nothing is touched |
| **Create your Free API Key HERE** | Button next to the **API key**: opens the key page of the chosen provider in the browser (`console.groq.com/keys` on Groq, `openrouter.ai/keys` on OpenRouter). It disappears on LM Studio/custom |
| **Volume** | Slider (it applies to the next audio). The **speech speed** stays **only in `F8`** (`[AI] TtsSpeed`) |
| **Push-to-talk** | See the current key and press **Change** to capture another one |
| **On/off** | Companion speaks, speak AI replies, speak mod lines, free conversation and voice orders |
| **Memory** | How many recent exchanges he remembers (0–12) |

Everything is saved to the `.cfg` **right away**. `Esc` or the **X** close it. While the panel is open, your
character does not answer the controls (and the push-to-talk key does not record).

### ⚙️ Our tweaks on top of the upstream

- **Larger gathering radius** (wood/stone/ore searched up to 25 m from you, drops picked up up to 12 m).
- **Random chatter off** (the little idle lines every 15–30 s). The useful lines stay.
- **The `V` key belongs to push-to-talk**, without triggering the game's *Toggle Auto-Pickup* (which is still
  available if you rebind it in the Valheim settings).

---

## How it works (architecture)

```
voice (Microphone 16 kHz) ──► Whisper.net (STT, whisper.cpp in-process, CPU) ─┐
                                                                              ├─► same order box ─► CompanionOrders ─► the companion acts
game chat ────────────────────────────────────────────────────────────────────┘           │
                                                                                          ├─► LLM (Groq by default, or local LM Studio/Ollama, or OpenRouter) ─► action + reply
                                                                                          └─► Kokoro-82M (TTS, sherpa-onnx in-process, CPU) ─► he SPEAKS at his position
```

- **LLM**: **Groq** by default (an OpenAI-compatible API; only the **free key** is needed), a **local** server
  (LM Studio/Ollama) as an option or **OpenRouter** as an alternative — used to **understand** the order and to
  **talk**. In the `I` panel, the **"Select your LLM provider"** dropdown fills in the URL and the model of the
  chosen provider, and the **"Create your Free API Key HERE"** button opens the key page in the browser.
- **Local STT**: `Whisper.net` (whisper.cpp) running **inside the game**, with the model loaded once.
- **Local TTS**: `sherpa-onnx` with the **Kokoro-82M** model — the companion **speaks** the reply inside the
  game, at his position, with no external service.
- **Privacy**: the **voice** (STT and TTS) is always **local**. The **LLM** is the only piece that can be remote:
  with a **local LM Studio/Ollama** nothing leaves your machine; with **Groq** (default) — or **OpenRouter** — the
  text of the orders and of the conversation goes to a **remote service**.
- None of that blocks the game: everything runs on background threads.

---

## Requirements

- **Valheim 1.0.x** (Unity 6 engine) with **BepInEx 5.4.2351**.
- **An LLM** for the orders: **Groq** (default — only internet and the **free key** at
  https://console.groq.com/keys are needed; the text of the orders **leaves your machine**) — or a local
  **LM Studio/Ollama** (optional — nothing leaves) — or **OpenRouter** (alternative, with a slower free tier). See
  the **[wiki](https://github.com/julianu37/AI-Companions/wiki/LLM)**.
- **A microphone** (only for the voice orders).
- **Windows x64** for the STT (the whisper.cpp native binaries are `win-x64`).

---

## Installation

> The **detailed, illustrated** step by step (including the **Groq key** and the local option) is in the
> **[project Wiki](https://github.com/julianu37/AI-Companions/wiki/Installation)**. Summary:

1. Install **BepInExPack_Valheim** and copy the mod folder to `BepInEx\plugins\AI-Companions`.
2. (Optional, for voice) leave a Whisper ggml model in `...\AI-Companions\whisper\` — e.g.
   `ggml-small-q5_1.bin`.
3. (Optional, for him to **speak**) leave the Kokoro model in `...\AI-Companions\tts\kokoro\`
   (`model.int8.onnx`, `voices.bin`, `tokens.txt`, `espeak-ng-data\`) — it already ships in the package.
4. **For the LLM:** create the **free key** at https://console.groq.com/keys and paste it into `[AI] ApiKey` —
   the `BaseUrl` (`https://api.groq.com/openai/v1`) and the `Model` (`openai/gpt-oss-20b`) **are already the
   defaults**. Want to run it locally? LM Studio with `qwen2.5-3b-instruct`, `[AI] BaseUrl = http://127.0.0.1:1234/v1`
   and an empty `ApiKey`. (**OpenRouter** still works as an alternative: `https://openrouter.ai/api/v1` +
   `openrouter/free`.)
5. Enter the world. In the chat: `collect 10 wood`. Or hold **`V`** and speak. He answers **out loud**.

> **Updating to 1.8.3? Settings go back to the defaults.** The configuration file is now
> `BepInEx\config\AiCompanions.cfg` and the old one (`Maddhattr.OfflineCompanionsReborn.cfg`) **is no longer
> read** — you can delete it. The new file is born with the **default values**, including an **empty `ApiKey`**
> that has to be pasted again; the AI section became **`[AI]`** and the **keys are in English** (`Enabled`,
> `Model`, `TtsVoice`…). **Save, world, companions, houses and inventory are not affected** — the plugin GUID and
> the `HC_*` keys did not change. The full table is in
> **[Configuration](https://github.com/julianu37/AI-Companions/wiki/Configuration)**.

---

## Controls

| Key | Action |
|---|---|
| **`V`** | **Push-to-talk**: hold and speak; release for him to understand |
| **`Z`** | Command key: look at something and tap to send the companions (move/attack/open door/sit/repair). Hold to call them |
| **`E`** | Hold while looking at a companion to open the **radial menu**; tap to open his panel/inventory |
| **`LeftAlt` + `Z`** | Farm zone editor |
| **`I`** | **AI panel**: personality, voice (with **Listen**), volume, memory, conversation, the push-to-talk key and the **LLM settings** (with the provider picker) — all saved right away |
| **`F8`** | Mod settings panel (every option, in game) |

> The AI panel (key **`I`**, changeable in `[AI] PanelKey`) opens without needing a companion nearby, closes with
> **Esc** or the **X**, and every control is saved to the `.cfg` at that very moment. While it is open, your
> character does not answer the controls.

---

## AI configuration (the `[AI]` section of `BepInEx\config\AiCompanions.cfg`)

| Key | Default | What it does |
|---|---|---|
| `Enabled` | `true` | Turns the text orders on |
| `BaseUrl` | `https://api.groq.com/openai/v1` | LLM endpoint (Groq; or local LM Studio/Ollama, or OpenRouter) |
| `Model` | `openai/gpt-oss-20b` | Model in use — `openai/gpt-oss-20b` on Groq (default); `openrouter/free` on OpenRouter; the LM Studio name locally |
| `ApiKey` | *(empty)* | **Required on Groq** (the free one comes from console.groq.com/keys) and on OpenRouter; empty on LM Studio/Ollama. Never commit a key |
| `Prefix` | *(empty)* | Empty = every message goes to the LLM. E.g.: `/c` requires the prefix |
| `Personality` / `PersonalityPreset` | *(empty)* | Free text (it wins) or preset (`viking`, `mago`, `sarcastico`, `guia`) |
| `Memory` | `4` | How many recent exchanges he remembers (`0` turns it off) |
| `Conversation` | `true` | Answer free conversation when it is not an order |
| `TargetRange` | `40` | How far to search for the creature named in an attack order |
| `VoiceEnabled` | `true` | Turns push-to-talk on |
| `VoiceKey` | `V` | Push-to-talk key |
| `VoiceLanguage` | `pt` | Transcription language |
| `VoiceMaxSeconds` | `10` | Recording cap (s) |
| `VoiceInitialPrompt` | *(order vocabulary)* | Biases the recognition towards the words of the orders |
| `VoiceDevice` | `0` | Microphone index |
| `SttModel` | `whisper\ggml-small-q5_1.bin` | Whisper model |
| `TtsEnabled` | `true` | He **speaks** the replies (local TTS, Kokoro) |
| `TtsVoice` | `42` | Voice: `42` pf_dora (fem.), `43` pm_alex (male), `44` pm_santa (male) |
| `TtsVolume` / `TtsSpeed` | `1` / `1` | Volume and speed of the speech |
| `TtsThreads` | `4` | Cores for the synthesis (4 = faster than real time) |
| `TtsSpeakReplies` | `true` | Speak the model's reply |
| `TtsSpeakModLines` | `true` | Speak the mod's line that appears in the chat (it wins in an order) |
| `TtsCache` | `true` | Keep the lines already synthesized (the repeated ones come out instantly) |
| `TtsModel` / `TtsLanguage` | `tts\kokoro\model.int8.onnx` / `pt-br` | Kokoro model and language (never empty) |
| `PanelKey` | `I` | Key that opens the **AI panel** in game (personality, voice, push-to-talk) |

> The **keys and the descriptions of the `.cfg` are in English** (the mod's native language); the **`F8` panel**
> shows the names in the **game's language** — in Portuguese you read `Ligado`, `Modelo`…; in English, `Enabled`,
> `Model`… The explanation of each option, with the defaults, is in the
> **[wiki](https://github.com/julianu37/AI-Companions/wiki/Configuration)**.

---

## Repository structure

| Path | What it is |
|---|---|
| `src/Companions/` | The mod (C#, net472): `Core/`, `Companion/`, `AI/`, `Jobs/`, `UI/`, `Patches/`, `Diagnostics/` |
| `src/Companions/AI/` | **Our AI layer**: `LlmClient`, `LlmIntent`, `CompanionOrders`, `CompanionChatAI`, `WhisperStt`, `VoiceInput`, `CompanionTts`, `CompanionPersona`, `CompanionMemory` |
| `Translations/`, `Speech/` | PT-BR translations (interface and speech) — the build copies them into the plugin |
| `package/` | Package files (icon, README/CHANGELOG, guide and configuration) |
| `docs/` | Roadmap, vision, phase plan, reverse engineering and technical notes |
| `.opencode/` | The **agent team** used to develop the project |

## Documentation

- **[Wiki](https://github.com/julianu37/AI-Companions/wiki)** — the guide for whoever is going to install and use
  it: requirements, step-by-step installation (with images), **LLM configuration (Groq, LM Studio or OpenRouter)**,
  voice, commands, AI configuration and common problems. (The source of the pages is in [`docs/wiki/`](docs/wiki/).)
- [`docs/roadmap.md`](docs/roadmap.md) — phases and status (Tracks A–D).
- [`docs/vision.md`](docs/vision.md) — the goal of the project.
- [`docs/re/`](docs/re/) — reverse engineering of Valheim 1.0 and STT/voice notes.
- [`docs/references.md`](docs/references.md) — third-party sources and licenses.
- [`AGENTS.md`](AGENTS.md) — how the agent team works in this repository.

## Build

```powershell
# Environment.props (local paths) + build
dotnet build Companions.sln -c Release
# the .dll and the translations are copied to DeployDir (BepInEx\plugins\...)
```

Requirements: [.NET SDK](https://dotnet.microsoft.com/download) 8+ (or VS 2022+) and Valheim with BepInEx
installed. The build uses the DLLs of your own game installation (no game file lives in the repository).

---

## Credits and license

- **Upstream:** [Offline Companions Reborn](https://github.com/MaddhattrRocks/Offline-Companions-Reborn) —
  a fork of **ProfMags**' *Offline Companions* — **MIT**. The companion, the jobs, the movement/combat AI, the UI
  and the base of all of it come from there.
- **Us:** the PT-BR translation and the **AI layer** (LLM/voice), plus fixes and tweaks.
- **STT:** [Whisper.net](https://github.com/sandrohanea/whisper.net) + [whisper.cpp](https://github.com/ggml-org/whisper.cpp)
  (MIT) and the Whisper ggml models (MIT).
- Project license: **MIT** — see [`LICENSE`](LICENSE) and [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md).
