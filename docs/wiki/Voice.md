> 🌍 **English** · [Português](Voice-pt)

# Voice

The mod has **two** voice parts, and they are independent:

- **STT** (*speech to text*) — **you** speak and the mod understands: hold **`V`**, speak and release.
- **TTS** (*text to speech*) — the **companion** speaks: his reply comes out loud, at his position in the world.

Both run **inside the game**, on your machine, with no external service.

---

## You speak: the STT (Whisper)

### How it works

1. You **hold `V`** and speak.
2. The mod records the microphone at **16 kHz mono** and cuts the silence at the beginning and at the end.
3. On release, the audio is transcribed **inside the game** by **Whisper.net** (whisper.cpp in-process, on the CPU)
   — no HTTP, no child process, no internet.
4. The recognized text goes into the **same** path as the typed orders: order, conversation, personality and memory
   work the same. You see `Got it: "…"` in the chat.

> The **`V`** key only records with the game focused and with **no** window open (inventory, map, menu, chat,
> console or the mod's panels).

### Models

| File | Size | Note |
|---|---|---|
| `whisper\ggml-small-q5_1.bin` | 181 MB | **Default.** Much better in Portuguese |
| `whisper\ggml-base-q5_1.bin` | 57 MB | Faster and less accurate — change it in `[AI] SttModel` |

Where to download: https://huggingface.co/ggerganov/whisper.cpp/tree/main

Project measurement (16-core CPU, 8 threads, 2 s clip): **~638 ms** with `base` against **~2187 ms** with `small`.
The time barely changes with the clip size, because the encoder always works on a 30 s window.

### STT settings

| `[AI]` key | Default | What it does |
|---|---|---|
| `VoiceEnabled` | `true` | Turns the push-to-talk on. Off = the key does nothing (and goes back to doing what the game did with it) |
| `VoiceKey` | `V` | Push-to-talk key |
| `VoiceLanguage` | `pt` | Language of the speech (Whisper code). Empty or `auto` lets the model detect it |
| `VoiceMaxSeconds` | `10` | Recording ceiling, in seconds |
| `VoiceSilenceThreshold` | `0.01` | How loud the audio has to be to count as speech, when cutting the silence. Raise it if your microphone picks up too much noise |
| `VoiceInitialPrompt` | *(the orders vocabulary, in PT-BR)* | Biases the recognition towards the order words. Put the words **you** say |
| `VoiceDevice` | `0` | Which microphone to use (index of the list the log prints) |
| `VoiceShowText` | `true` | Shows in the chat/HUD what the microphone heard |
| `SttModel` | `whisper\ggml-small-q5_1.bin` | Whisper model (relative to the plugin folder or an absolute path) |

> **Microphones:** at startup the mod writes the list of available microphones to the log — starting with
> `[STT] Push-to-talk:` and then the number of devices with their index and name. Use that number in `VoiceDevice`.

### The `V` key and the game's auto-pickup

In Valheim, **`V`** is the *Toggle Auto-Pickup* key by default. To avoid stealing the game's key, the mod has a
patch that **blocks the auto-pickup** — but **only** when both conditions are true at the same time:

- `[AI] VoiceEnabled` is **on**, **and**
- the game's auto-pickup binding points at the **same** key (`VoiceKey`).

If you **rebind** the auto-pickup in the Valheim controls screen, the mod **does not** block anything — the game
stays intact and the `V` key stays free for speaking. You can also simply change `VoiceKey`.

---

## He speaks: the TTS (Kokoro)

### How it works

- The voice is synthesized **inside the game** by **Kokoro-82M** (through `sherpa-onnx`, on the CPU) — no external
  service, no child process, nothing leaves your machine.
- The speech comes out **at the companion's position** in the world (3D sound), not in the middle of the screen.
- The synthesis runs on a **background thread** and the speech is assembled **sentence by sentence**: the first one
  already plays while the next one is generated. Repeated lines come from the **cache**, instantly.
- The model is loaded **once**, in the background, when you enter the world.

### Brazilian Portuguese voices

| `TtsVoice` | Voice |
|---|---|
| **42** (default) | `pf_dora` — feminine |
| **43** | `pm_alex` — masculine |
| **44** | `pm_santa` — masculine |

> The other numbers of the model are from **other languages** (45, for instance, sounds Chinese).

### What he says

The rule is: **the voice follows what is written on the screen.**

| `[AI]` key | Default | What it does |
|---|---|---|
| `TtsEnabled` | `true` | He **speaks** the replies. Off = only the balloon and the chat |
| `TtsVoice` | `42` | Which voice to use (42/43/44) |
| `TtsVolume` | `1` | Voice volume (0 = muted, 1 = normal volume) |
| `TtsSpeed` | `1` | Speed (above 1 speaks faster and shorter) |
| `TtsThreads` | `4` | CPU cores for the synthesis (4 is already faster than real time) |
| `TtsModel` | `tts\kokoro\model.int8.onnx` | Kokoro model (relative to the plugin folder or an absolute path) |
| `TtsLanguage` | `pt-br` | Language of the spoken text. **Never leave it empty** — Kokoro requires a language (`pt` alone is European Portuguese) |
| `TtsCache` | `true` | Keeps the lines already synthesized (the repeated ones come out instantly) |
| `TtsSpeakReplies` | `true` | Speaks the **model's reply** (his own way: *"Leave it to me."*) |
| `TtsSpeakModLines` | `true` | Speaks the **mod's line** that appears in the chat (*"Ok: gather wood (10)."*) |
| `TtsReverbZoneMix` | `0` | How much of the world's reverb zones goes into the voice. `0` = dry voice |

- In an **order**, the mod's line **wins** (the voice follows what is written on the screen). Turn
  `TtsSpeakModLines` off to hear the model's reply instead.
- In a **conversation**, the model's reply **is** the speech.
- **Nothing is spoken twice:** with both options on, the line that is not spoken only goes to the log.

### Models

| Where | Files |
|---|---|
| `tts\kokoro\` | `model.int8.onnx` (109 MB), `voices.bin` (27 MB), `tokens.txt` and the `espeak-ng-data\` folder |

The voices, the tokens and the `espeak-ng-data` are looked for in the **same folder** as the model.

Where to download: the releases of https://github.com/k2-fsa/sherpa-onnx (package of the
**`kokoro-int8-multi-lang`** model).

### The voice echo (and the fix)

If you hear the companion's speech **echoing as if in a cave**, it is not a mod bug: Valheim has **one global
reverb zone** — an `AudioReverbZone` with a **1000 m** radius on the `AudioListener` object itself, with a **4 s**
tail. Everything within 1 km got that tail, and our audio source was on the Unity default.

The fix is the **`[AI] TtsReverbZoneMix`** key, which already comes as **`0`** (dry voice, like the game's music).
If you want a subtle "space" in the voice, use **`0.19`** — the same value the game's ambience uses. It applies on
the **next sentence**, without restarting.

---

## Testing from the game console

Open the console with **`F5`**:

| Command | What it does |
|---|---|
| `modia_tts_teste` | Speaks a test sentence through the TTS (full pipeline) |
| `modia_tts_teste <text>` | Speaks the text you write — e.g. `modia_tts_teste Good morning, boss!` |
| `modia_stt_teste` | Runs the Whisper recognition over a WAV, **without** using the microphone |

> `modia_stt_teste` transcribes the file set in `[AI] SttWavTest` (default `whisper\jfk.wav`) and writes the result
> to the log. It is the right test to know whether the Whisper model is loading — useful when the voice orders do
> not come out.
>
> **The package does not bring that WAV** (it left the package to save space): put a file of yours in
> `...\AI-Companions\whisper\` and point `[AI] SttWavTest` at it (a path relative to the plugin folder or an
> absolute one). The command **does not** take the path as an argument — only the key. A short speech in English or
> Portuguese, WAV PCM 16 bits, is what it expects.

The log shows the synthesis time of each sentence and the **RTF** (fraction of real time: below 1 is faster than
real time).

---

## Known limitation: game volume

The companion's voice **does not go through the game mixer**. That means it **ignores the Valheim volume sliders**
and obeys **only** `[AI] TtsVolume`. If the voice is too loud or too low compared with the game, adjust it through
that key (or through the volume slider in the **`I`** panel).

<!-- D4: add Español/Français/Italiano -->
