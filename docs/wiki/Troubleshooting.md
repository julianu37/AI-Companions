> 🌍 **English** · [Português](Troubleshooting-pt) · [Español](Troubleshooting-es) · [Français](Troubleshooting-fr) · [Italiano](Troubleshooting-it)

# Troubleshooting

Symptom → probable cause → what to do.

**Where to look in the log, always:** `<Valheim>\BepInEx\LogOutput.log`. The mod's lines start with `[IA]`, `[STT]`
and `[TTS]`. (`[IA]` is the **log tag** — the `.cfg` section is **`[AI]`**, with the keys in English.)

---

## He does not answer anything

| Where to look | What to look for |
|---|---|
| `LogOutput.log` | `[IA] Chat orders ready: enabled=…, endpoint=…, model=…` |

**Probable causes, in order:**

1. **The `ApiKey` is empty or wrong.** The mod comes pointed at **Groq**, which **requires** the key (see
   "answers with an authentication error" below). If you use the local **LM Studio**, the key stays empty and the
   `BaseUrl` has to be `http://127.0.0.1:1234/v1`.
2. **The `BaseUrl` or the `Model` is wrong.** On Groq they are `https://api.groq.com/openai/v1` and
   `openai/gpt-oss-20b`. On the local LM Studio, the `Model` has to be the **API Model Identifier** it shows, not
   the folder name. See **[LLM (Groq, LM Studio or OpenRouter)](LLM)**.
3. **Using the local LM Studio: the server is not on.** Open the **Developer** tab and check the `Running` status on
   port **1234**.
4. **`[AI] Enabled` is `false`.** Turn it on in the **`F8`** panel.
5. **`[AI] Prefix` is filled in.** If you put `/c`, only the messages that start with `/c` go to the model.
6. **No companion is nearby / able to take an order.** He has to be spawned.

> Before anything, use the **Test connection** of the **`I`** panel: it says, in seconds, whether the URL answers
> and whether the model appears in the list. Remember that the **key** is only validated on a real order (the test
> confirms the URL and the model list, not the authentication).

---

## Answers with an authentication error (401 / "unauthorized")

| Where to look | What to look for |
|---|---|
| `LogOutput.log` | `[IA]` with status **401** (or an *unauthorized* / *invalid api key* message) |
| **`I`** panel → **Test connection** | It **can** answer `Connection OK` even with the wrong key — on OpenRouter the model list is public. The 401 shows up on the **order**, not on the test |

**Cause:** **Groq requires** the `[AI] ApiKey` (and OpenRouter does too), and it is **empty**, **wrong** or
**deleted** (for example, after you copy a `.cfg` from another machine, or after updating **from 1.8.2 or older** —
1.8.3 is the version that reset the settings) — or it is the key of **another** service, pasted together with a URL
that is not its own.

**What to do:**

1. Create or copy the key at **https://console.groq.com/keys** (Groq) or at **https://openrouter.ai/keys**
   (OpenRouter).
2. Paste it into **`[AI] ApiKey`** — in the **`I`** panel (**AI** tab → **LLM settings**) or straight in the `.cfg`.
3. Click **Test connection**; then **Apply** (the configuration applies on the next message).
4. If you do **not** want to use Groq, point the `BaseUrl` at the local server
   (`http://127.0.0.1:1234/v1`), use the LM Studio `Model` and **leave the `ApiKey` empty**.

> Key leaked? Delete it at **https://console.groq.com/keys** (Groq) or
> **https://openrouter.ai/settings/keys** (OpenRouter) and create another one. The `.cfg`'s `ApiKey` should not be
> shared or committed.

---

## Answers 429 (request or token limit)

| Where to look | What to look for |
|---|---|
| `LogOutput.log` | `[IA]` with status **429** (or a *rate limit* / *too many requests* message) |

**Cause:** the service's **free tier** was reached. On **Groq**, with `openai/gpt-oss-20b`, the limits are
**30 requests per minute**, **1,000 per day**, **8,000 tokens per minute** and **200,000 per day** — and, since each
order spends ~1,800 tokens, the practical ceiling is **~4 orders per minute**. On **OpenRouter**, the `:free` models
have **20 requests per minute** and a daily ceiling that depends on the account history.

**What to do:**

1. **Wait a few seconds** and send the order again — the limit is per time window, so it comes back on its own.
2. Lower the frequency: `[AI] CooldownSeconds` (default **3**) already helps; raise it if you send orders in bursts.
3. Reduce `[AI] Memory` (every remembered exchange makes the prompt bigger and, with it, the tokens of each order).
4. For heavy use, use the **local LM Studio/Ollama** — there is no request or token limit.

> **429** breaks nothing: the order is refused with a warning and the game goes on. It is **not** a key error — that
> is the **401**, above.

---

## Takes too long to answer

| Where to look | What to look for |
|---|---|
| `LogOutput.log` | `[IA] Tempo: … ms` — the mod warns when it goes past **5 s** |

**Probable causes:**

1. **The `.cfg` is still on OpenRouter.** The current default is **Groq** (`openai/gpt-oss-20b`), which answers in a
   **fraction of a second** in our measurement. If your `[AI] BaseUrl` is still `https://openrouter.ai/api/v1`, you
   are on its free router (`openrouter/free`), which **varies** from one order to another and goes through the
   provider queue. Switch to Groq (see **[LLM (Groq, LM Studio or OpenRouter)](LLM)**) or use the local LM Studio.
2. **Do not confuse slowness with the Groq limit.** The **429** answers **right away** (the order is refused with a
   warning), not slowly — if the symptom is delay, the cause is another one. See "answers 429" above for the limit.
3. **Your local `.cfg` still has `localhost` in the `BaseUrl`.** In the LM Studio case, the default is
   `http://127.0.0.1:1234/v1`; if the file is from an older version, change it. On Windows, `localhost` resolves
   first to IPv6 (`::1`) and LM Studio only listens on IPv4 — every order pays ~**2.5 s** of toll.
4. **The model is big or it is on the CPU (local).** A 3B (`qwen2.5-3b-instruct`) answers in ~0.35–0.4 s;
   `google/gemma-4-e4b` takes ~3 s. Bigger models practically require a GPU.
5. **`[AI] Memory` is high.** Every remembered exchange makes the prompt the model has to read bigger. Reduce it.
6. **The first message of the session** includes loading the model — on the local LM Studio it is naturally slower,
   and on the remote services the first one is usually the coldest too.

> Remember: the request runs in the **background**. Even slow, it does **not** freeze the game.

---

## Answers wrong or ignores the number

**Probable cause:** the model did not understand the resource or the quantity.

**What to do:**

- Check what the model understood: `[AI] ShowReplyInChat` shows the raw reply in the chat.
- Use **`qwen2.5-3b-instruct`** with `[AI] StructuredOutput = true` — it is the combination the project measured as
  reliable (the 3B went from 0/6 to **6/6** valid answers with the structured output).
- In the default (**Groq**, `openai/gpt-oss-20b`) the model is always the same, so the answer is consistent. On
  **OpenRouter** (alternative), `openrouter/free` changes the model on every request — the quality varies too. If
  the order always misses the same way, pick **a specific model** in `[AI] Model` (the free ones on OpenRouter have
  `:free` in the name) or use the local LM Studio, which is always the same model.
- Remember that **the number is a target counted in his bag**: if he already has enough, he does **not** go out to
  gather, and that is the correct behaviour.
- For the voice orders, a bad recognition reaches the model wrong. See "the microphone does not record" below.

---

## Answers in the wrong language

**Probable cause:** the game is in one language and the companion answers in another, or the chosen model is weak in
your language.

**What to do:**

- **Check the game's language.** Since 1.9.0 the contract sent to the model is in the **game's language** — Portuguese
  (Brazil), English, Spanish, French and Italian — and a language outside those five falls back to **English**. The
  voice and the speech recognition follow the same rule automatically (`[AI] VoiceLanguage` / `TtsLanguage` /
  `TtsVoice` = `auto` / `auto` / `0`).
- Use **`qwen2.5-3b-instruct`** (good in Portuguese) if you are on LM Studio; on **Groq** (default) and on
  **OpenRouter**, a bigger model usually writes better in Portuguese than the small ones.
- Adjust the personality in the **`I`** panel (preset or free text in your language) — it goes into the prompt and
  pulls the answer to that language. The presets are translated into the five languages too.
- Reduce `[AI] Temperature` (the default `0.1` is already low; high values make the model ramble).

---

## The voice echoes (sounds like a cave)

**Cause:** it is not a bug — Valheim has **one global reverb zone** of 1000 m on the `AudioListener` object itself,
with a 4 s tail. The companion's audio source was on the Unity default and picked up that whole tail.

**What to do:** confirm that `[AI] TtsReverbZoneMix` is on **`0`** (the default). If you want a subtle "space" in
the voice, use `0.19`. It applies on the **next sentence**, without restarting. See **[Voice](Voice)**.

---

## The voice does not come out, or comes out low

| Where to look | What to look for |
|---|---|
| `LogOutput.log` | `[TTS]` lines (load time, synthesis, RTF) |

**Probable causes:**

1. **`[AI] TtsEnabled` is `false`.** Turn it on in the **`I`** panel.
2. **The Kokoro model is not in place.** Check `tts\kokoro\` with `model.int8.onnx`, `voices.bin`, `tokens.txt` and
   `espeak-ng-data\`.
3. **The volume is low.** The voice **does not go through the game mixer** — it **ignores the Valheim volume
   sliders** and obeys **only** `[AI] TtsVolume`. Adjust that key (or the slider in the `I` panel).
4. **Isolated test:** in the console (**`F5`**), run `modia_tts_teste Good morning, boss!`. If it works there, the
   problem is the configuration; if it does not, it is the model.

---

## The microphone does not record

| Where to look | What to look for |
|---|---|
| `LogOutput.log` | The `[STT] Push-to-talk:` line: whether it is on, the key, and the numbered list of microphones |

**Probable causes:**

1. **No window may be open.** The key does not record with the inventory, the map, the menu, the chat, the console or
   the mod's panels open.
2. **`[AI] VoiceEnabled` is `false`.**
3. **The wrong microphone is selected.** The log lists the numbered devices — put the number in `[AI] VoiceDevice`.
4. **The silence threshold is high.** `[AI] VoiceSilenceThreshold` (default `0.01`) cuts the beginning/end of the
   recording. If your microphone is quiet, lower it; if it picks up too much noise, raise it.
5. **The speech was too short.** The log says the recording was too short — speak for longer.
6. **The language is wrong.** `[AI] VoiceLanguage` is `auto` by default (the **game's language**); a hand-written code
   of another language (e.g. `pt` with the game in English) makes the recognition much worse. Since 1.9.0 the
   automatic value follows the game and needs no restart.
7. **Isolated test:** `modia_stt_teste` in the console (**`F5`**) runs Whisper over a WAV, **without** a microphone.
   If it transcribes, the model is fine and the problem is the capture.

> **Windows x64 is required** for the voice: the natives of the **STT** (whisper.cpp) **and of the TTS**
> (sherpa-onnx/onnxruntime) are `win-x64`.

---

## The `V` key turns on the game's auto-pickup

**Cause:** in Valheim, `V` is the *Toggle Auto-Pickup* by default.

**What to do:** the mod already blocks that toggle — but **only** when `[AI] VoiceEnabled` is on **and** the game's
auto-pickup binding points at the **same** key. So:

- **Rebind the auto-pickup** in the Valheim controls screen to another key (the mod stops blocking and you have both),
  **or**
- change `[AI] VoiceKey` to another key in the **`I`** panel.

If you rebind it, the mod re-evaluates the conflict right away. See **[Voice](Voice)**.

---

## The `I` panel does not open

**Probable causes:**

1. **A window is open.** The panel does not open with the inventory, the map, the menu, the **`F8`** panel or the
   trader's search field open. Close them.
2. **The key was changed.** Check `[AI] PanelKey` (default `I`).
3. **You are typing in a text field.** In that case the key is ignored on purpose — the mod does not steal the
   chat/console key.

---

## The `F8` panel does not click / a click is swallowed

**Cause (already fixed):** the mod's panels were on the same drawing layer as the game HUD, and the HUD **is at
`sortingOrder = 400`**. On a layer tie, uGUI breaks it by the order the raycasters were registered — and the game's
one wins. There were also **decorative** elements of the panel taking clicks without being controls.

**What to do:**

- Update to the version with the fix (the mod's canvases are now in the **1701+** range, **above** the HUD).
- If the problem comes back, turn on `[Advanced] UiDiag = true`: the next time the panels open, the log writes
  `[UiDiag]` with the scene layers and who is in front of the controls. Turn it off again afterwards.

> The same layer adjustment fixed the text of the **centre of the radial menu** scrambled with the game's
> `crosshair`.

---

## I gave the companion the Megingjord and the weight did not change

| Where to look | What to look for |
|---|---|
| **`I`** panel (companion tab) and **HUD** bar | The weight is still `…/300` (or the value you put in `[General] MaxCarryWeight`) even with the belt in the bag |
| Gathering | He heads home to unload at **298** instead of around **448** |

**Cause:** in versions **before 1.8.0** the belt's bonus was not read. The companion's weight limit is **from the
mod** (`[General] MaxCarryWeight`, 300 by default), not from the game, and the Megingjord's `+150` is a *status
effect* whose only reader in vanilla is the `Player`. Since the companion is not a player, the belt only took up
space and weighed.

**What to do:** **update the mod** (the fix came in 1.8.1). After that the `/450` shows up in ~**0.5 s** (with the
default `300`), and this line confirms it in `LogOutput.log`:

```
[Carry] "Thorin": +150 de capacidade (Megingjord) - teto 450
```

> The belt **does not need to be equipped** — it counts anywhere in the bag (on a `Humanoid` that is not a player the
> utility slot does not even survive the reload). And **two** belts **do not stack**: the bonus counts once. The
> details of the two keys are in **[Configuration](Configuration)**.

---

## The game does not load the mod

| Where to look | What to look for |
|---|---|
| `LogOutput.log` | `AI Companions v1.9.0 loading...` and `AI Companions loaded successfully! (N methods patched)` — the `v1.9.0` is the version you installed |

**Probable causes:**

1. **The folder is in the wrong place.** The path has to be `<Valheim>\BepInEx\plugins\AI-Companions\` — with
   `Companions.dll` **inside** it.
2. **The dependency DLLs are missing.** `Whisper.net.dll`, `sherpa-onnx.dll` and the `System.*` ones have to be next
   to `Companions.dll`.
3. **BepInEx is not installed** (or it is on an incompatible version). Use **BepInExPack_Valheim 5.4.2351**.
4. **Incompatible game version.** The mod is for **Valheim 1.0.x** (Unity 6).

> **The PlayFab / NullReferenceException error at boot is normal** and it is **not** from the mod. Do not worry about
> it.

---

## My settings went back to the defaults (or "the mod does not read my `.cfg`")

**Cause:** **1.8.3** was the version where the configuration file changed its name and its language (that is why a
file from 1.8.2 or older has no effect). The mod reads
**`BepInEx\config\AiCompanions.cfg`**; `Maddhattr.OfflineCompanionsReborn.cfg` (from the previous versions) **is no
longer read** and stays on the disk **without effect** — that is why the options go back to the **defaults**,
including the empty `ApiKey`. The AI section also became **`[AI]`**, with the keys in **English**.

**What to do:**

1. Confirm that the file exists: `<Valheim>\BepInEx\config\AiCompanions.cfg` (it is created on the first boot).
2. Do again what you had by hand — the most important thing is the **`[AI] ApiKey`**. The **`F8`** menu (or the `I`
   panel) shows everything in game, with the names in the **game's language**.
3. Delete the old `.cfg` so you do not get confused: it does **not** start being read again.
4. Check the log for the line `[Config] Maddhattr.OfflineCompanionsReborn.cfg was replaced by AiCompanions.cfg ...`.

> **Save, world, companions, houses and inventory are not affected** — the plugin **GUID** and the `HC_*` keys did
> not change. Only the mod's configuration goes back to the default.
>
> **1.9.0 does not reset anything.** If your file is already `AiCompanions.cfg` (1.8.3 or newer), the update only
> moves the old fixed voice/language defaults (`pt`, `pt-br`, `42` and the Portuguese prompt) to `auto` — your key and
> your other choices stay.

---

## Still not solved?

Gather this information and open an issue at
https://github.com/julianu37/AI-Companions/issues :

- The excerpt of `LogOutput.log` around the problem (the `[IA]`, `[STT]` or `[TTS]` lines).
- The mod version (it appears on the boot of the log) and the Valheim version.
- What you did (chat or voice) and what happened.

<!-- D4: add Español/Français/Italiano -->
