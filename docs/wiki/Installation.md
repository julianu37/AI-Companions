> 🌍 **English** · [Português](Installation-pt) · [Español](Installation-es) · [Français](Installation-fr) · [Italiano](Installation-it)

# Installation

The complete step by step, from zero to the companion answering an order.

Before you start, make sure you have everything in **[Requirements](Requirements)**.

---

## 1. Have Valheim installed

The game has to be on version **1.0.x** (Unity 6). The default Steam install is in:

```
C:\Program Files (x86)\Steam\steamapps\common\Valheim
```

In the next steps, `<Valheim>` is that folder.

---

## 2. Install BepInEx

The mod is a BepInEx 5 plugin. Install **BepInExPack_Valheim 5.4.2351**:

1. Download it from https://github.com/BepInEx/BepInEx/releases
2. Extract it into `<Valheim>`, so that `winhttp.dll` and the `BepInEx\` folder end up in the game **root**.
3. Run Valheim **once** and close it. BepInEx creates `BepInEx\plugins\`, `BepInEx\config\` and
   `BepInEx\LogOutput.log`.

Official documentation: https://docs.bepinex.dev/

---

## 3. Download the mod release

Go to **https://github.com/julianu37/AI-Companions/releases** and download **one** of the two files:

| File | Size | When to pick it |
|---|---|---|
| `AI_Companions-1.9.1.zip` | ~314 MB | **Recommended.** It already comes with the voice models (STT + TTS). Install and play |
| `AI_Companions-1.9.1-sem-modelos.zip` | ~28.5 MB | Slow internet, or you already have the models and want to download them separately |

> The difference is **only** the voice models. The mod and the translations are the same in both.
>
> *(**1.9.0** is the **five-language** release — Portuguese, English, Spanish, French and Italian — with the voice
> and the speech recognition **automatic**, following the **game's language**. **1.8.3** is where English became
> the **native language**: the `.cfg` is now `AiCompanions.cfg` and the AI section became `[AI]`, with the keys in
> English. **1.8.2** added a **LLM provider picker** and the **API key** button to the `I` panel. **1.8.0**
> brought the weight fix — the **Megingjord** +150 now adds to the companion's ceiling — and **1.8.1** changes
> the default LLM to **Groq** (free and fast), keeping LM Studio as the local option and OpenRouter as the
> alternative. **1.7.0** was the release that renamed the mod to **AI Companions**.)*

---

## 4. Extract into the Valheim folder

The zip already comes with the folder structure ready: inside it the folder is `BepInEx\plugins\AI-Companions\`.
So you only have to extract it **into the Valheim root** (`<Valheim>`) and every file lands in the right place.
If you use a mod manager, point the zip at it — the manager installs it directly, without renaming anything.

> If you prefer to do it by hand, the final result has to be
> `<Valheim>\BepInEx\plugins\AI-Companions\`, with `Companions.dll` **inside** it. BepInEx loads plugins from
> **any** subfolder of `BepInEx\plugins\` — keeping that name only avoids confusion when updating.

> **Updating from an older version?** Delete the old folder `BepInEx\plugins\ModAI-BR-Companions\` **before**
> extracting. The mod changed its name (it was *ModAI-BR-Companions*), but the **GUID** and the `HC_*` keys are
> the same: the **save**, the companions, the house and the inventory still work. Two folders with the same
> plugin = BepInEx loads the mod twice.

At the end, the folder has to look like this:

![The mod folder in Explorer, with the files and the subfolders](images/pasta-do-mod.png)

```
<Valheim>\
└── BepInEx\
    └── plugins\
        └── AI-Companions\
            ├── LICENSES\
            ├── runtimes\
            ├── Speech\                     ← the companion's lines (5 languages)
            ├── Translations\               ← the interface texts (5 languages)
            ├── tts\                        ← the TTS model (Kokoro)
            ├── whisper\                    ← the STT models (Whisper)
            ├── Companions.dll              ← the mod
            ├── Microsoft.Bcl.AsyncInterfaces.dll
            ├── sherpa-onnx.dll
            ├── System.Buffers.dll
            ├── System.Memory.dll
            ├── System.Numerics.Vectors.dll
            ├── System.Runtime.CompilerServices.Unsafe.dll
            ├── System.Threading.Tasks.Extensions.dll
            └── Whisper.net.dll
```

> The zip also brings `README.md`, `CHANGELOG.md` and `icon.png` in the **root** — that is what the mod manager
> shows in the mod page.

> Those `System.*` DLLs and the two voice ones (`Whisper.net.dll`, `sherpa-onnx.dll`) have to stay **next to
> `Companions.dll`** — they are the dependencies the mod uses for the STT and the TTS.

---

## 5. Voice models

**If you downloaded the full package, this step is done** — the models already came in the `whisper\` and `tts\`
folders.

If you downloaded the **no-models** one, put the files like this:

| Where to put them | Files |
|---|---|
| `...\AI-Companions\whisper\` | `ggml-small-q5_1.bin` (181 MB) — the default of the speech recognition |
| `...\AI-Companions\tts\kokoro\` | `model.int8.onnx` (109 MB), `voices.bin` (27 MB), `tokens.txt` and the `espeak-ng-data\` folder |

> In the **no-models** package `tokens.txt` and the `espeak-ng-data\` folder **already come** — only
> `ggml-small-q5_1.bin` (Whisper) and `model.int8.onnx` + `voices.bin` (Kokoro) are missing.

Where to download them:

- **Whisper (ggml):** https://huggingface.co/ggerganov/whisper.cpp/tree/main — take `ggml-small-q5_1.bin`.
  `ggml-base-q5_1.bin` (57 MB) is faster and less accurate and **no longer comes in the package**: download it
  to the `whisper\` folder and point `[AI] SttModel` at it if you want the quickest; see **[Voice](Voice)**.
- **Kokoro (sherpa-onnx):** the releases of https://github.com/k2-fsa/sherpa-onnx — the package of the
  `kokoro-int8-multi-lang` model (which brings `model.int8.onnx`, `voices.bin`, `tokens.txt` and
  `espeak-ng-data`).

> Quality, speed and alternatives are detailed in **[Voice](Voice)**.

---

## 6. Configure the LLM

The orders are understood by an **LLM**. The **recommended** path is the local **LM Studio**: install it from
https://lmstudio.ai, get `qwen2.5-3b-instruct` through **Model Search**, turn the **Local Server** on (port
**1234**) and press **Load Model** — the step by step with the screens is in
**[LLM (LM Studio, Groq or OpenRouter)](LLM)**. If you would rather not install anything, the `.cfg` **already
comes pointed at Groq**: create the **free key** at https://console.groq.com/keys and paste it into `[AI] ApiKey`.
**OpenRouter** also works (free key at https://openrouter.ai/keys) and stays as the alternative.

> **Without editing the file:** above the **server URL** there is the **"Pick your LLM provider"** dropdown —
> choosing **Groq**, **LM Studio** or **OpenRouter** **fills in the URL and the model** (the key is not touched; it
> applies after **Apply**). And, on the **API key** row, the **"Create your free API key HERE"** button opens the
> key page in the browser (`console.groq.com/keys` on Groq). The speech speed, which left this panel, stays in
> **`F8`**.

![The AI panel on the AI tab: in LLM settings, the "Pick your LLM provider" dropdown already on Groq, the server URL with api.groq.com, the model openai/gpt-oss-20b, the masked API key and the "Create your free API key HERE" button to the right of the key](images/en-ai-panel.jpg)

> This capture is from **1.8.2**: the **provider picker** is on **Groq** (what the `.cfg` ships with), the URL and
> the model come from Groq and the key button appears to the right of the **API key**. If you pick **LM Studio**,
> the button disappears (there is no key).

> **With Groq, the text of the orders leaves your machine** (it is a remote service) and the free tier has a limit
> of requests and of **tokens per minute** (~4 orders per minute with `gpt-oss-20b`). The local **LM Studio** has
> none of that: it is the recommended path, and **nothing** leaves the machine.

The complete guide of the three paths (LM Studio → Groq → OpenRouter) is in
**[LLM (LM Studio, Groq or OpenRouter)](LLM)**.

---

## 7. First run and the log

1. Run Valheim.
2. Open `<Valheim>\BepInEx\LogOutput.log`.
3. Look for these two lines:

```
AI Companions v1.9.1 loading...
AI Companions loaded successfully! (N methods patched)
```

If they appear, the mod loaded.

> The `v1.9.1` is the **version you installed** (the number of the zip you downloaded) and `N` is how many game
> methods the mod adjusted — that number can change from one version to another.

> On the **first run after updating from 1.8.2 or older** the log also shows that the configuration file changed its
> name: `[Config] Maddhattr.OfflineCompanionsReborn.cfg was replaced by AiCompanions.cfg ...` — it is the normal
> 1.8.3 notice, and the old `.cfg` can be deleted.

> **It is normal** to see a **PlayFab / NullReferenceException** error at boot — it is **not** from the mod and
> blocks nothing. If you want to confirm, see **[Troubleshooting](Troubleshooting)**.

---

## 8. First in-game test

With the world open and the companion by your side:

1. **By text:** open the game chat and type `collect 10 wood`.
   He should answer and leave to gather. *(The number is a target counted in his inventory — if he already has 10
   wood, he says "We already have enough.")*
2. **By voice:** hold **`V`**, say `get 10 stones` and release. The recognized text appears in the chat.
3. **His voice:** he should **say** the answer out loud, at his position in the world.
4. **AI panel:** press **`I`** to adjust personality, voice, volume and the push-to-talk key.

To see every key and order, go to **[Commands](Commands)**.

---

## Checklist

- [ ] Valheim 1.0.x installed
- [ ] BepInEx 5.4.2351 extracted into the game root
- [ ] Release **1.9.1** downloaded (the full package or the no-models one)
- [ ] Folder `BepInEx\plugins\AI-Companions\` created with `Companions.dll` and the dependencies
- [ ] Voice models in `whisper\` and `tts\kokoro\`
- [ ] Configuration **checked** in `BepInEx\config\AiCompanions.cfg` (only going back to the defaults if you come
      from **1.8.2 or older**) and the old `.cfg` deleted
- [ ] Groq free key created and `[AI] ApiKey` filled in (or the local LM Studio configured)
- [ ] `[AI] BaseUrl` and `[AI] Model` checked in the `.cfg`
- [ ] **Test connection** (`I` panel) answering `Connection OK`
- [ ] Log with `AI Companions loaded successfully!`
- [ ] `collect 10 wood` in the chat works
- [ ] Holding `V` and speaking works

If any item fails, see **[Troubleshooting](Troubleshooting)**.
