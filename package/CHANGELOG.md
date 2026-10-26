# Changelog

All the changes to this project, derived from ProfMags' Offline Companions. The versions before 1.4.0 are the original mod (see the original mod's history, from ProfMags, for those versions) plus the first batch of compatibility fixes for the Valheim 1.0 API that made the mod load and run. **The entries below 1.5.4 are the upstream history and stay as they are (in English) — our sections (from 1.6.0 on) are ours.**

## 1.8.3 — 25/10/2026

**English as the native language — and the mod now speaks the game's language.** This is the version that **breaks the `.cfg`**: the
file becomes **`AiCompanions.cfg`** (the `Maddhattr.OfflineCompanionsReborn.cfg` is no longer read) and the section
`[IA]` became **`[AI]`**, with the keys in English. **Save, world, companions, houses and inventory are not affected** —
the plugin GUID and the `HC_*` keys stay the same.

### Why break the `.cfg`

The mod was born with the settings translated **inside the code** (`Ligado`, `Modelo`, `TtsVoz`…) and the F8 panel
drew those raw names. The result: with the game in English the `I` panel was in English and the F8 in Portuguese. Now
**English is the native language** (keys and descriptions) and **Portuguese is a translation** — which is the path for Spanish,
French and any other language to come in without touching the code.

### What changes for whoever installs

- **New `.cfg`: `BepInEx\config\AiCompanions.cfg`.** The old file stays on disk, **ignored** (you can delete it) and
  **the settings go back to the defaults** — including the `ApiKey`, which has to be pasted again.
- **`[AI]` section** (it was `[IA]`) with the 39 keys in English: `Enabled`, `BaseUrl`, `Model`, `ApiKey`, `Temperature`,
  `TimeoutSeconds`, `Prefix`, `ShowReplyInChat`, `CooldownSeconds`, `TargetRange`, `Personality`,
  `PersonalityPreset`, `Memory`, `Conversation`, `StructuredOutput`, `SttTest`, `SttModel`, `SttWavTest`,
  `VoiceEnabled`, `VoiceKey`, `VoiceLanguage`, `VoiceMaxSeconds`, `VoiceSilenceThreshold`, `VoiceInitialPrompt`,
  `VoiceDevice`, `VoiceShowText`, `PanelKey`, `TtsEnabled`, `TtsVoice`, `TtsSpeed`, `TtsThreads`, `TtsVolume`,
  `TtsModel`, `TtsLanguage`, `TtsCache`, `TtsSpeakReplies`, `TtsSpeakModLines`, `TtsReverbZoneMix`, `EcoDiag`.
- **Descriptions in English** in the `.cfg` (it is the native language; the Portuguese explanation stays in the wiki).
- **Bilingual F8**: the names of the options and of the tabs follow the **game's language** — in Portuguese they show `Ligado`,
  `Modelo`, `VozTecla`…; in English, `Enabled`, `Model`, `VoiceKey`… (the `I` panel already worked like that).
- The **upstream migrator** (`com.profmags.companions.cfg`) still works: whoever comes from the original mod keeps the
  options, because that file was already in English.

### Default changes

- **`[Death] RespawnDelay`:** the default went from **300 s** to **30 s** — the minimum accepted (the range stays **30–3600**).
  The dead companion comes back on his own in half a minute, like a player. A `.cfg` that already
  exists with another value is not touched; the F8 → *Death* tab and the **Default** button show the new value.

### For whoever translates

A new language is **one file**: `Translations/<Language>.json` (interface, config names and tabs) + `Speech/<Language>.json`
(speech lines). **English needs no file** — the keys **are** the text, so the game in English shows the right names
with no translation at all.

### Known limitations

- **The dverger types (Mage, Fire Mage, Ice Mage and Support) are combat types.** They fight normally and the ring
  shows only the combat/support actions (follow, repair and restock, dismiss, command, pick up, wander, set home and
  the stances). **Work** orders — through the chat, the voice or the command key — are **refused** with the line
  *"I'm a fighter, I don't do that work."* (the mode and the target of the current order do not change). If you **change the type** of a
  companion who was working to one of those, he goes back to following. The panel hides the food slots per
  type. **For work, use the `Companion` type, which is complete.**

## 1.8.2 — 24/10/2026

**Choosing the LLM provider became one click.** The *LLM settings* section of the `I` panel got a **provider
picker** above the server URL: choosing **Groq**, **LM Studio** or **OpenRouter** fills in the **URL** and the
**model** on its own (the key stays with you) — and, next to the **API key** field, a button
**"Create your Free API Key HERE"** opens the key page of the chosen provider in the browser.

### AI panel (key `I`)

- **"Select your LLM provider" picker** — the same dropdown as the summoner's "Type", already open on the provider your
  `.cfg` uses:
  **Groq** → `https://api.groq.com/openai/v1` + `openai/gpt-oss-20b`;
  **LM Studio** → `http://127.0.0.1:1234/v1` + `qwen2.5-3b-instruct`;
  **OpenRouter** → `https://openrouter.ai/api/v1` + `openrouter/free`.
  Choosing **fills in the fields**; nothing is saved until **Apply**, and the **key is never touched** (neither deleted nor
  filled in).
- **"Custom (manual URL)"** — when the URL in the `.cfg` is not one of the three (Ollama, own server…), the
  picker shows that option and **does not touch anything**: it never lies about what is configured. The choice is always
  inferred from the URL itself, so there is no second place that can diverge from the `.cfg`.
- **"Create your Free API Key HERE" button**, on the key line: golden like the *On* buttons, with the **soft
  highlight** on hover and **without** turning into "Off" after the click (it is an action, not a picker). It opens
  `console.groq.com/keys` on Groq and `openrouter.ai/keys` on OpenRouter; it **disappears** on LM Studio and on custom, which
  have no key to create. The URL is fixed in the code, never built from what the player typed.
- **The "Speech speed" left the `I` panel** and stayed **only in the F8 panel** — the option is still the same
  (`[IA] TtsVelocidade`) and the **Listen** button uses the value from there. With that the panel became **42 px shorter** and without
  empty space in the middle.

### For whoever already has the mod

Nothing changes on its own: the `.cfg` is not rewritten, the provider stays the one you configured (the picker only **shows**
which one it is) and the speech speed stays at the value you left.

## 1.8.1 — 24/10/2026

**New default LLM: Groq — free and fast.** The default becomes `[IA] BaseUrl` = `https://api.groq.com/openai/v1` and
`[IA] Modelo` = `openai/gpt-oss-20b`, with the **free key** at `console.groq.com/keys`. The reason is simple: the free tier of
**OpenRouter** (`openrouter/free`) is **slow** and unpredictable for a game order, while
Groq's `openai/gpt-oss-20b` answers in a fraction of a second.

- **Order of preference:** **Groq** (default) → local **LM Studio/Ollama** (optional — nothing leaves your machine) →
  **OpenRouter** (still working and documented, but the free one is slower). Switching is just editing
  `BaseUrl`/`Modelo`/`ApiKey` in the `.cfg` or in the **LLM settings** section of the `I` panel.
- **The structured output is still guaranteed:** `openai/gpt-oss-20b` accepts `json_schema` in **strict** mode (what the mod
  uses), so the reply is still valid JSON and no order is lost.
- **Limits of the Groq free tier** (`gpt-oss-20b`): **30 requests/min, 1,000/day, 8,000 tokens/min and
  200,000/day**. Since each order spends ~1,800 prompt tokens, the practical cap is **~4 orders per minute**; above
  that Groq answers **429** and the order is refused with a warning (nothing breaks, nothing gets stuck).
- **Your `.cfg` does not change on its own:** BepInEx only applies the default on a new key, so whoever already configured OpenRouter or
  LM Studio stays exactly as they are — changing is optional.
- **Docs aligned:** the wiki (LLM page, requirements, installation, configuration, common problems), the README and the
  Nexus page now present **Groq first**, **LM Studio** as the local option and **OpenRouter** last.

## 1.8.0 — 24/10/2026

**What the companion carries is now what he has in his pack: the Megingjord really adds up.** A fix for the report
sent on the Nexus (*"when I put the megingjord in the companion's inventory it doesn't raise the weight by 150"*) — and a
weight cap that now respects the equipment. **This release includes everything from 1.7.0** (several companions with their own
tab, appearance editor with a 3D preview, OpenRouter as the AI default); whoever comes from an earlier version finds the
complete set here.

### Weight and carrying capacity

- **Items with a carrying bonus now count** — the Megingjord (+150) and any item built the same way in the game.
  The companion's effective cap is `[General] MaxCarryWeight` **+ bonus**, and the value is read from the **asset**
  (`SE_Stats.m_addMaxCarryWeight`), **never** from a fixed `150`: a belt modded to +250 works the same.
- **It works with the belt anywhere in the pack** — no need to equip it in the utility slot (on a `Humanoid` that is not a
  player it does not even survive a reload). And it still counts **only once**: two identical belts do not add up, exactly as
  the game treats equipment effects.
- **Where the new cap applies:** the **`Encumbered`** state (slow walking), the `x/450` of the **companion panel** and the
  **HUD** bar, the refusal of **loot** and of **picking up from the ground**, the **smith**, the **upkeep**, the "bag full" of
  **fishing/hunting** and the "I'm full" warning.
- **Gathering also adds up:** `[Gathering] OverweightThreshold` became the **base** value (298 by default) and the bonus
  goes on top — with the Megingjord in the pack the gathering companion only goes back home near **448** and keeps
  gathering above 300. Without the belt, everything as before (298).
- **One log line per change** (never per frame): `[Carry] "Thorin": +150 de capacidade (Megingjord) - teto 450`.
- **No per-frame cost:** the bonus is cached per inventory and is only recalculated when the pack changes (the path without a
  belt allocates nothing).

### Why it did not work before

The companion's weight limit is **the mod's** (`[General] MaxCarryWeight`, 300 by default), not the game's — vanilla
**does not** impose a limit on a `Humanoid` that is not a player (`Inventory.CanAddItem` only looks at grid space and there is no
`m_maxWeight` in 1.0). And the belt's `+150` is a **status effect** whose **only reader** is `Player.GetMaxCarryWeight()`;
the companion never went through there, so the belt only took up space and weighed.

## 1.7.0 — 23/10/2026

**Several companions, each one with its own identity — and the companion comes into the game ready to use.** The mod is now
called **AI Companions** (it was ModAI-BR-Companions): the plugin folder is `BepInEx\plugins\AI-Companions\` and the packages
are `AI_Companions-<version>.zip`. **The GUID, the `.cfg` file, `Companions.dll` and the `HC_*` keys were preserved** —
updating loses no settings, companion, home, inventory or save.

### Companions

- **Summoner**: `Summon new companion` in the panel (key `I`), **for free** — pick the type (Warrior, Archer,
  Mage, Cook, Gatherer, Farmer) and he is born ready, without going through the Trader. A new companion is born with the name
  of the type plus a number ("Warrior 2") so the tabs are not confused.
- **One tab per companion** in the `I` panel: name, type, appearance, voice, personality and Call/recall — everything for that
  companion, without touching the others.
- **Order and conversation by name**: `Gruncha, collect 10 stones` applies **only** to him (the prompt, the personality, the memory
  and the order are his). Without the name, the order still applies to everyone. A beginning that looks like a name and does not exist
  (`Fulano, collect…`) gets a warning instead of ordering the wrong one — but `mano, collect 10 stones` still orders normally.
- **Name and type editable**: rename (the name applies in the HUD, in the hover, in the ring and in the commands) and **change the type** with
  a respawn preserving name, inventory, equipment, appearance, home/bed, skills, AI profile, stamina, borrowed
  tools and the cook's ledger.
- **Voice and personality per companion** (in his ZDO, they travel in the tombstone): what is in `[IA]` is the default for whoever has no
  choice of his own.
- **Cap of 10 companions** (`[General] MaxCompanions`): the count is of the roster (alive + dead + lost), so
  dying does not open a slot; the Trader, the summoner and the starting companion respect the limit and the Shrine keeps
  recovering the dead.

### Appearance

- **Appearance editor with a 3D preview**: gender, haircut, beard, skin colour, hair tone and shade — the
  model changes **live** while you choose, and **Apply** saves it to the companion **without a respawn**.

### AI

- **OpenRouter by default, without installing anything**: `[IA] BaseUrl` is born `https://openrouter.ai/api/v1` and
  `[IA] Modelo` is born `openrouter/free` — just paste the **free key** from `openrouter.ai/keys` into the `ApiKey`.
  **LM Studio (and Ollama) keep working** as a local/offline option: change the URL and the model in the `.cfg`.
- **"LLM settings" section in the `I` panel**: turns the AI and the conversation on/off, URL, model, key (**masked**),
  prefix, memory and temperature sliders, **Apply** and **Test connection** — without editing a file. The advanced
  options stay in the **F8** panel.
- **The `stop` answers what he was doing**: `stop` while gathering wood → *"Ok: stop gathering wood"*
  (before it always said "stop following"). Mining, cooking, repairing… each job with its own line.
- **The mod's own lines are now spoken**: "I need a pickaxe", the event lines and the mode change
  come out in the **companion's voice** through the local TTS, with the same deduplication as the AI replies.

### Fixes

- **Buttons of the mod panels no longer "died"**: the buttons cloned from the game (starter, appearance editor, ring)
  inherited `interactable = false` from the craft button template and stopped answering after opening the inventory —
  now they are born enabled. That was what killed the **hair and beard arrows**.
- **Hair and beard really applied**: the game caches the item hash **before** attaching; when the attach failed
  once, every change of that cut became a silent no-op. The cache is cleared on every apply and the head joint is
  looked up when it is null.
- **Colour sliders went back to the start**: the stored colours were overwritten by the defaults while the panel was built;
  now the panel opens at the position that reproduces the saved colours.
- **Orders**: `inventory_close` entered the contract (30 actions) and "close the inventory" no longer opens the window; the name in the
  command became conservative; the ZDO write waits for ownership (with a queue and a warning) instead of getting lost in multiplayer; the
  LLM got a **request id** (a late reply is discarded, without applying an old order); the tombstone and the respawn
  compare the companion id before the name (two companions with the same name); the stock caches became
  **per companion**.
- **Layout**: the middle column of the starter panel with **Listen** visible and no overlap; the name field already comes
  filled in with the type default; the footer of the `I` panel got its own strip and two summary lines.

### Requirements

- **Valheim 1.0.x** + **BepInExPack_Valheim 5.4.2351**. An **LLM**: OpenRouter (default — only the free key and
  internet) **or** a local LM Studio/Ollama (optional). **Windows x64** for both voice parts; a microphone only for the
  voice orders.
- **When updating from an earlier version, delete the old folder** (`BepInEx\plugins\ModAI-BR-Companions\`) before
  extracting: two copies of the same plugin make BepInEx load the same mod twice. The `.cfg` stays in
  `BepInEx\config\Maddhattr.OfflineCompanionsReborn.cfg` and is **not** affected.

## 1.6.4 - 19/10/2026

**Correct `[IA]` defaults and the presentation as an independent project.** For whoever already configured it, nothing changes in the game:

- **`[IA] BaseUrl` is now born `http://127.0.0.1:1234/v1`** (it was `http://localhost:1234/v1`). On Windows `localhost` resolves to IPv6 first and the LM Studio server only listens on IPv4 - every order paid about 2.5 s of waiting because of that. Whoever already has the `.cfg` is **not** affected: the configured value still applies.
- **`[IA] Modelo` is now born `qwen2.5-3b-instruct`** (it was `google/gemma-4-e4b`) - the model measured at ~0.35-0.4 s per order with the structured output.
- The help text of `BaseUrl` now explains `127.0.0.1` vs `localhost`.
- The documentation (README, LEIA-ME, wiki and release notes) stops presenting itself as a "fork" and becomes an "independent project based on Offline Companions Reborn". The MIT attribution stays mandatory and is kept.

## 1.6.3 — 17/10/2026

**Repackaged.** Nothing changes in the game:

- Same content as 1.6.2, with the mod folder in `BepInEx/plugins/AI-Companions/` and
  `README.md`/`CHANGELOG.md`/`icon.png` in the **root** of the package.
- The `.zip` is now called `AI_Companions-1.6.3.zip` (exact name `Name-Version`, no suffix).

## 1.6.2 — 17/10/2026

**New icon and a lean package.** Nothing changes in the game:

- **New icon** — the mod's art (the three companions + "AI COMPANIONS PT-BR"), in **256×256**.
  The full banner is in the repository (`docs/images/banner.png`) and at the top of the README.
- The package now goes out **without the models** (~20 MB, instead of ~368 MB). The **models (STT and TTS)
  stay in the GitHub release**, and the LEIA-ME/README say exactly where to put them (the `whisper\` folder and
  `tts\kokoro\`).

## 1.6.1 — 17/10/2026

**Lean package.** Nothing changes in the game:

- The package **no longer carries the test WAV** (`whisper\jfk.wav`): only what the mod needs to
  work goes out. The console command `modia_stt_teste` keeps working and takes a WAV path as an argument.
- The published `.zip` is now called `AI_Companions-1.6.1.zip` (exact name `Name-Version`, no suffix).

## 1.6.0 — 16/10/2026

**The AI layer: orders by chat and by voice, the companion speaks, personality/memory and an in-game panel — all local, in PT-BR.** This is the first release of the project with the AI (Phases 3.1 to 3.8); the technical detail of each one is in the sections below.

**Highlights:**
- **Text orders in the chat:** `collect 10 wood`, `follow`, `stop`, `mine iron` — a **local LLM** (OpenAI-compatible API) returns the intent and the companion performs the same action as the radial menu.
- **Voice orders:** hold **V**, speak and release — **local STT** (Whisper.net, inside the game, no internet).
- **The companion speaks:** **local TTS** (Kokoro-82M via sherpa-onnx), in PT-BR, with the voice coming out of his position.
- **Personality, memory and free conversation:** `good morning`, questions — he answers as a character and remembers the conversation.
- **Orders with a target:** `attack the greydwarf`, `equip the bronze axe`, `drop the stone` (and `throw the wood away`).
- **Picking up items from the ground:** `pick up the items` / `get the axe from the ground` — including what he dropped himself.
- **AI panel in game** (key **I**): personality, voice (with the **Listen** button), push-to-talk, volume/speed, memory and conversation.
- **Audited PT-BR translation:** 386 UI keys and 186 speech lines, with no English text visible to the player.
- **Fixes:** the **voice echo** (it was the game's global reverb zone — the voice now comes out dry), the **stone order** that did not stop at the target, and the **F8 panel** that swallowed clicks (the mod UI layers are now above the game HUD).

#### Phases included in this release

PT-BR translation: coverage audit (what the player reads, in English, was a bug) + Phase 3.1: text orders in the chat (local AI) + Phase 3.2/3.2c: the order number is a TOTAL TARGET counted in the companion's inventory + Phase 3.6: orders with a target (attack/equip/drop) + Phase 3.6b: the model's order without a target (drop/equip/attack fall back to the player's words) + Phase 3.5: personality, memory and free conversation + Phase 3.3: voice orders (push-to-talk + local STT) + Phase 3.3b: fixes from the voice test (V key, STT quality, latency) + Phase 3.3c: larger gathering radius and random chatter off + Phase 3.3d: the stone order stops at the target and the voice text goes to the player's bubble + Phase 3.8: the companion opens his own inventory by voice/chat + Phase 3.4: the companion SPEAKS (local TTS, Kokoro-82M via sherpa-onnx) + Phase 3.7: AI panel in game (personality, voice with Listen, push-to-talk) + Phase 3.4 (part 3): the TTS echo SOLVED — it was the game's global reverb zone (the voice comes out dry) + Phase 3.1: structured output (JSON schema) — unlocks small models + PT-BR translation (AI Companions project) + fix: the F8 panel no longer swallows clicks (layer 31→33 + raycast of the decorative parts) + fix (the real one): the mod panels and ring go to the **1701+** range — **the layer was the cause** of the swallowed click in the F8 (the game HUD is at 400; the 31→33 was not enough) + fix: the raycast diagnostic becomes opt-in (`[Advanced] UiDiag`, off by default — it was ~7 KB per session) and stops warning about the intentional click targets of the AI panel
- **The raycast diagnostic becomes opt-in and stops warning about intentional targets (branch `fix/uidiag`).** The audit
  `[UiDiag]` (the one that found the game `HUD` at 400 swallowing the F8 clicks) was **not** removed — it is the answer
  to "who is in front of the controls?" — but one session of it came out with **6 lines / 7,132 characters** and **three**
  of those lines were **false positives** of the AI panel (`HC_AiPanel/AiRoot`, `HC_AiPanel/AiRoot/KeyBox`,
  `HC_AiPanel/AiRoot/Overlay/PersonalityList`).
  - **New config: `[Advanced] UiDiag`** (bool, **off**). It stayed in `[Advanced]` and not in `[IA]` because **it is not part of the
    AI layer**: it audits the whole mod UI (the F8 panel **and** the AI one, and the layer range of
    `UI/ModUiLayers.cs`) — the `[IA] EcoDiag` is in `[IA]` because it diagnoses a component of the AI (the voice). The description
    says what it logs, when to turn it on (investigating a strange click/UI) and that it changes nothing in the game.
  - **Off (default): zero lines and zero cost.** The two callers (`ConfigPanel` and `CompanionAiPanel`) exit
    **before** entering `ModUiAudit`: no list building, no scene traversal. The "once per session" flag
    is only set when the audit actually runs, so turning the option on in the middle of a session applies to the next opening.
  - **On, compact:** two opening lines, once per session (the one saying the audit is on and the allow-list one — what it
    **never** reports) and **one summary line per panel** with **counts** (controls / on purpose /
    unexpected) — the **names** of the decorative targets, which were the bulk of the log, are gone. The **list of scene layers
    stays in one line** (it was what named the `HUD` at 400) and the **warnings** stay **line by line** (path +
    type + size + the point probed).
  - **The false positives:** `AiRoot` (panel background) and `KeyBox` (the key box) became `raycastTarget = false` —
    they are **decoration**, they can never be in front of a control (the background is the parent of everything) and the panel's
    **`Backdrop`** (full screen, created before the root, a click target **on purpose**) is already the modal catcher; it was the same thing the F8
    `ConfigRoot` already did. The **background bars of the dropdown lists** (`Overlay/PersonalityList` and
    `Overlay/VoiceList`) **stay intentional targets** (the items are 2 px smaller than the list and the click on that border
    has to die on the list, otherwise it reaches the `ListCatcher` and **closes** the list) and entered a **documented allow-list**
    of the audit (`ModUiAudit.Intentional`, matched by the end of the path + the reason).
  - **No logic/layout/text changed** (not the LLM, the TTS, the STT, the orders, the jobs, the translation or the layers). Build: **0 errors,
    0 warnings** + DLL deployed. **The in-game test is the player's.**
- **PT-BR translation — coverage audit (branch `fix/traducao`).** An audit of **every text the player can
  see**: the UI keys (`ModLocalization` × `Translations/Portuguese_Brazilian.json`), the speech lines
  (`Speech/Portuguese_Brazilian.json`), the panels, the hovers, the `MessageHud` messages and the docs. **374 UI
  keys: 0 missing, 0 in English** (the 7 that matched the English were legitimate) → **386 keys: 0 missing, 0 in
  English** after the 12 new ones; **15 speech categories / 186 lines: 0 missing** (only "Skål!" is the same as the English, it is
  a toast). **The English texts that showed up for the player went into the localization** (a new key in PT-BR **and**
  the English pair in `ModLocalization`):
  - **the companion's name** — `CompanionPrefabs` wrote `m_name = def.DisplayName` ("Companion", "Warrior",
    "Mage"...) into the prefab, and that was what the **HUD**, the **hover** and the **ring** showed for a companion with no
    name of his own. Now `CompanionTierData.LocalName(def)` resolves the new keys `hc_tier_companion` /
    `_warrior` / `_mage` / `_fire_mage` / `_ice_mage` / `_support` (**Companion**, **Warrior**, **Mage**, **Fire
    Mage**, **Ice Mage**, **Support**), with the English name as a fallback — the same pattern `MHR_Shrine`
    already used. It also applies to the **Sub-Type** buttons, the **buy** button ("Buy Ice Mage (500)"), the
    companion's **chest** name and the message "Your new mage has arrived!";
  - **"X has been resurrected!"** from the tombstone → `hc_msg_resurrected` (*"{0} has been resurrected!"*);
  - **"Hold"** of the companion hover (`[Hold E] Commands`) → `hc_hover_hold` (*"Hold"*);
  - **the three recall greetings** ("I've been looking for you!" / "There you are!" / "Finally found you!"),
    which were in English **in the code** of `ReturnLogic` and the companion **spoke** → `hc_cmd_return_1..3`
    (the PT-BR text lives in the JSON);
  - **"ON"/"OFF"** of the **F8** panel toggles → the radial keys (`hc_radial_on`/`hc_radial_off`, *ON* /
    *OFF*);
  - **"Push-to-talk"** (label, footer and the warning about the conflict with the game auto-pickup) → **"Press to talk"**;
  - the **"Companion"** fallbacks that were still left in visible paths (`CompanionHudPanel`, the panel name field, the
    tombstone name, the companion name in the shrine list, the *Rested* effect name, the sleep wait message)
    → `hc_msg_name_default` (*"Companion"*);
  - **`hc_radial_despawn_confirm`** was an **orphan** key: `CompanionRadialMenu` still references it (in a method
    dead since the confirmation became `YesNoPopup`) and it did not exist on either side — it came back with
    *"Confirm?"*, so the raw token never shows up if that path comes back to life.
  - **Out of scope (not ours):** the names of **item/creature/hair and beard style** come from the game assets
    (localized by Valheim itself), the game **`$...` tokens**, the **config keys** (internal
    names of the `.cfg`) and the ~1500 lines of **log/diagnostic** (`[IA]`, `[TTS]`, `[STT]`, `[UiDiag]`, `[Collect]`, `[ECO]`,
    `[Move]`, `[Direct]`, ...) that the player only sees if they open `LogOutput.log`. The **`CHANGELOG`** keeps the upstream
    history (below 1.5.4) in English on purpose — it is his record.
- **The mod panels and the ring are now ABOVE the game HUD — the swallowed click in the F8 panel and the text in the center of the
  ring, solved by the layer (branch `fix/camadas-ui`).** The player reopened the bug after `fix/clique-f8` (*"it's the
  same shit"*), and he was right: that fix got the **decorative** parts (a real problem, still fixed) but
  **not** the main cause. The `[UiDiag]` diagnostic it left in the panel itself found the culprit:
  ```
  [UiDiag] F8 raycast audit: canvas "HUD" (order=400, overrideSorting) has a raycast target over the controls of the panel:
           .../IngameGui/HUD/hudroot/crosshair/HoverName (TextMeshProUGUI 350x150) at the panel point 196,-46
  [UiDiag] F8 raycast audit: panel order=33, ... EnemyHud=200, HudMessage=1000(override), HUD=400(override),
           Menu=1700(override), UnifiedPopup=1900(override), ...
  ```
  - **The cause:** the game HUD is a canvas at **`sortingOrder = 400`** (with `overrideSorting`) and **every** canvas
    of the mod was at **28..34** — behind it. The target that swallowed the click is `crosshair/HoverName` (350x150 in the
    **center** of the screen, where the panel is), and it **receives clicks even when invisible** (the game fades it with
    `canvasRenderer.SetAlpha(0f)` and does **not** turn the `raycastTarget` off) — hence the **strip** and hence scrolling the panel solving it.
    **Why 31 → 33 was not enough:** 33 is still **below 400** (and on a `sortingOrder` tie uGUI breaks the tie by the
    registration order of the raycasters, which the game canvas wins).
  - **The fix:** every canvas of the mod went to the **1701..1708** range, with the numbers in a **single source**
    (`src/Companions/UI/ModUiLayers.cs`): `FarmZonePlacer` 28→**1701**, `HomeZonePanel` 29→**1702**,
    `CompanionRadialMenu` 30→**1703**, `StarterCompanionPanel` 30→**1704**, `FarmZoneCropPicker` 31→**1705**,
    `ConfigPanel` 33→**1706**, `CompanionAiPanel` 33→**1707** and the dropdown overlay 34→**1708**. The relative order
    among ours is the one from before.
  - **Why 1701 and not 2000:** 1701 is already above the HUD (400), the `HudMessage` (1000) and the `Menu` (1700), and
    **below** the `UnifiedPopup` (1900) — the **game's** modal popup (a vanilla class from `assembly_valheim`, not ours and
    it cannot be moved): a modal has to be in front of a window. And 2000 would be a **tie** with the game's
    `LongPressRadial`, a tie the game wins. It stays far below the console (5000) and the `SwitchCursor`
    (12000). The mod panels keep blocking the inventory/shop/menu (`Hud.InRadial`), so those never stay behind
    us.
  - **The second symptom, the same remedy:** the radial draws `_centerName`/`_centerAction`/`_centerState` in the **center**
    of the screen, where `crosshair/HoverName` lives; with the radial at 30 and the HUD at 400 the game drew over it and the texts
    got mixed. With the radial at **1703**, no more.
  - **Audit kept and generalized:** the logic left `ConfigPanel` for `src/Companions/UI/ModUiAudit.cs`; now
    `[UiDiag] F8 raycast audit` **and** `[UiDiag] AI panel raycast audit` run **once per session** (never per frame) and
    report the non-control click targets inside the panel, **every** active layer of the scene and who, above the
    panel, covers the controls column. The mod's own canvases are listed but not probed.
  - **No logic, layout, text, LLM, TTS, orders, jobs or STT changed** — only the `sortingOrder` of the eight canvases
    and the audit. Build: **0 errors, 0 warnings** + DLL deployed. **The in-game test is the player's.**
- **The F8 panel no longer swallows clicks (branch `fix/clique-f8`).** The player's report ("there is something invisible in front
  of the controls in a **strip** — clicking the ON/OFF of a line did nothing; after scrolling the panel the same button
  worked") had **two** things added up, and both applied to the same symptom:
  - **The layer.** The `ConfigPanel` canvas was at **31** — the **same** layer as the game UI. On a `sortingOrder`
    tie uGUI breaks the tie by the **registration order of the raycasters** (`RaycasterManager`), and the game canvas
    (registered much earlier) wins: its `GraphicRaycaster` delivers the click to one of **its** invisible targets (a
    `MessageHud`/HUD text faded with `canvasRenderer.SetAlpha(0f)`, which **keeps** receiving clicks) in a **fixed strip in the
    center of the screen**. The panel is centered, so scrolling the panel takes the control out of the strip — exactly what the
    player saw. The **AI panel** (key `I`) never had the problem because it was always at **33**. `ConfigPanel`
    went to **33** (the same layer; the two panels refuse to open together).
  - **The decorative parts.** Every decorative `Graphic` of the panel was `raycastTarget = true` (the uGUI default): the panel
    background, the row bars, the `ScrollRect` viewport and — the worst ones, for being the **last children of the viewport**,
    that is the graphics **on top of the whole panel** — the **invisible** scroll bar and its **handle** (10 px on the
    right border). Now **only** the controls (tabs, ON/OFF, Default, slider background+handle, *key bind*, X) and the two
    **scroll areas** (the content viewport and the tabs column — the GameObjects where the `ScrollRect`s live, which
    need the target for the mouse wheel and the drag) keep `raycastTarget = true`; the invisible bar and the
    handle became `false` **and** became the **first** children of the viewport (the `ScrollRect` keeps them by
    reference, so nothing changes for the wheel/drag). What was **discarded with proof** is in `docs/roadmap.md`
    (*Known pending items*): in uGUI an **ancestor** graphic never sits in front of its own children (the background of the
    tabs column is a click target and the tab buttons always answered).
  - **Diagnostic (once per session, on the first opening).** `[UiDiag] F8 raycast audit: panel order=33, N control
    raycast target(s), M on purpose (...), 0 unexpected; K other active canvas(es): <name=sortingOrder, ...>; 0 raycast
    target(s) of another canvas over the controls.` — and, if there is one, a **warning per target of another layer** that covers the
    panel's controls column (11 points sampled). If the strip comes back, the log says **who** it is.
  - Build: **0 errors, 0 warnings** + DLL deployed. **The in-game test is the player's.**
- **The "pick up the items from the ground" order finally picks up (Phase 3.1 — `MHR_Collect`: what was missing).** The player dropped the
  axe (`throw your axe on the ground` → `Order "drop": Companion dropped 1 x "Machado Nordico"`) and then asked to pick
  it back up (`get the nordic axe from the ground`, `gather the axe`, `pick up the items`): the model got the order right
  (`Order "pickup" -> 1 companion(s)`, `Ok: recolher itens do chao`) and **nothing happened** — the axe stayed on the ground.
  - **The finding (three things added up):** (1) the `pickup` action **only set a flag** in the companion's ZDO and **no**
    action of the mod walked to an item on the ground to pick it up (the only "picks up item" was the 2 m magnet of `CompanionAI.AutoPickup`);
    (2) what the companion drops is marked `m_autoPickup = false` on purpose (it is what the game itself does in
    `ItemDrop.OnPlayerDrop()`), and the magnet **and** `MHR_Loot` skip those items on purpose — exactly what the player
    asked to pick up; (3) the flag was only read by `MHR_Loot` and by the ring label, so the **Pick up** button did not
    turn the magnet off.
  - **New `src/Companions/Jobs/MHR_Collect.cs`** (same pattern as `MHR_Loot`: state per companion, `Start` from the order and
    `Drive` per frame from `CompanionAI.UpdateAI`, before `MHR_Loot.Drive`): it finds the nearest loose item inside
    `[Looting] CollectRadius` (**new, default 10**, range 1..30), **walks to it** with `MHR_Mover` and picks it up with
    `Humanoid.Pickup` — which **does not look at `m_autoPickup`** (checked in the decompiled game), so **including what was dropped
    on purpose** comes back. It repeats with the next one until nothing is left in the radius, the pack fills up or the order goes past **30 s**.
  - **Refusals:** quest item (the `drop` rule), piece (`IsPiece()`), a position in the `CompanionAI` *blacklist*, deep water
    (never swim for loot) and what does not fit **whole** in the pack. **It stops** with another order (chat **and** ring), a change of
    job, `Pick up` off, a full pack, death, lost ownership or the leash (`[Looting] MaxDistance`); and the
    **jobs wait** while he walks (`MHR_JobRules.OtherOrder`).
  - **The order does both things:** `pickup` turns the flag on (as before) **and** starts the pickup; `nopickup` only
    turns the flag off. The magnet and `MHR_Loot` **did not change** (the "drop it to give it" protection stays intact).
  - **The Pick up button of the ring now applies:** the magnet is now conditioned on the flag (read **once** per frame), with the
    default `true` — a companion who never touched the button stays exactly as today. The jobs that pick things up
    (`MHR_PileTaker`, `UpkeepPiles`, `UpkeepHoney`, `MHR_SpearThrow`, `HarvestController`) call `Humanoid.Pickup` directly
    and do not depend on the magnet.
  - **New line** (when there is nothing on the ground and when the pack is full): `hc_ai_collect_nothing` /
    `hc_ai_collect_full`, in PT-BR and in the English dictionary. Logs: `[Collect] <Name>: Going to pick up "X" (N m away)`,
    `Done: N item(s) picked up`, `nothing on the ground within N m`.
  - Build: **0 errors, 0 warnings**. The in-game test is the player's.
- **"Throw away" becomes `drop` even when the model says `nopickup` (Phase 3.1 — safety net).** The same in-game test
  showed that the contract alone was not enough: when the model **insists** on `nopickup` for a drop (or
  pick up) order, the **player's words** decide — the same reasoning as "the resource rules".
  - **`PickupIntent.NeighborAction`** (new, `AI/PickupIntent.cs`, **pure**) is called **once per message** by
    `CompanionOrders.Run`, in the same place as the `resource` fix: with the action **exactly `nopickup`** and a clear
    drop verb (`throw away`, `throw the wood away`, `drop`, `drop it`, `discard`, `let go`, `leave it on the ground`...) the order
    becomes **`drop`**; with a clear pickup verb (`gather`, `gather it`, `grab`, `collect from the ground`, `take from the ground`...) it becomes
    **`pickup`**. The log shows the swap in one line.
  - **Guard rails:** the **legitimate** `nopickup` is preserved (`stop`/`stop it`/`stop now` or `don't take`/`don't pick up` turn
    the correction off), only the `nopickup` action is looked at and, without a trigger, the behaviour is identical to today's.
  - **The `drop` target still comes from the Phase 3.6b path** (the player's words read by `OrderTargets.Phrases`
    and validated against the pack) — no new name extraction, and the quest item still never drops.
  - Verified outside the game (a harness with the real files): **58 cases, ALL CHECKS PASSED**.
- **The contract separates dropping, picking up and stopping picking up (Phase 3.1 — in-game test with the 3B).** The
  `qwen2.5-3b-instruct` confuses **neighbouring actions**: `throw your wood away` (and the variants `throw the wood away` and
  `threw the wood away`, with a typo) came back as **`nopickup`** — the companion **turned the automatic pickup
  off** instead of dropping the wood — and `gather the wood from the ground` was **50/50** between `pickup` and `nopickup`.
  - **A short rule in the `Contract`** separates the three: `drop` = drop/throw away what the companion has, `pickup` =
    pick up items from the ground, and `nopickup` **only** when the player asks to STOP picking up ("stop picking up", "don't pick
    up anything").
  - **3 new examples**, right after the equivalents: `"throw the wood away"` → `drop`, `"gather the wood from the ground"` →
    `pickup` (neither of the two had any example) and **`"equip the pickaxe"`** → `equip` (the contract only had `"take the
    pickaxe"`, and it was the form with `equip` that failed).
  - **Only the `Contract` changed** (`ValidActions`/schema, `LlmIntent`, `LlmClient`, personality/memory, TTS, panel, STT and
    orders intact) and the prompt grew **6 lines** (~+120 tokens). Whether the 3B stops getting it wrong with the new prompt is the player's in-game
    test.
- **The prompt now teaches that `action` and `resource` have to match (Phase 3.1 — the other half of the test finding).**
  With `qwen2.5-3b-instruct`, `collect 5 stones` came back as `{"action":"wood","resource":"wood"}`: the model copied the
  `action:"wood"` from the contract's `"collect 20 wood"` example (the verb **`collect`** was the trigger) and ignored the
  stone, even with the `resource` rule already in place — *few-shot overfitting*, the typical way a 3B gets it wrong.
  - **The `Contract` got a short rule** (`stone` → `action:"stone"` + `resource:"stone"`; `wood` → `wood`/`wood`;
    `ore` → `ore`/`ore`; "NEVER use action \"wood\" for stone") **and 2 examples with stone and the gathering verb**,
    right **after** the wood example that caused the copy.
  - **The rest of the contract stayed intact** (the 29 actions, the `target` mandatory in `attack`/`equip`/`drop`, the other
    examples) and the prompt grew **3 lines** (~+60 tokens over the ~1766 measured), because it is calibrated for
    `gemma-4-e4b` and for the 3B.
  - **Nothing else changed** (schema/`ValidActions`, `LlmIntent`, `LlmClient`, personality/memory, TTS, panel, STT,
    orders): the fix above is still the safety net for when the model insists on swapping the fields.
    Whether the 3B **stops** getting it wrong with the new prompt is the player's in-game test.
- **The resource rules: `collect 10 stones` no longer becomes wood gathering (Phase 3.1 — fix from the in-game test).** In
  the test with **`qwen2.5-3b-instruct`** the companion went **to gather wood** when the player asked `collect 10 stones`.
  The cause had two parts: **ours** — with `action` and `resource` inconsistent (`{"action":"wood","resource":"stone"}`)
  the mod obeyed the **generic** `action` and **ignored the `resource`**, which is the specific field (it was the one showing
  in the cap log, "stops at 5 **Stone**", while the job applied was the wood one); and **the small model's**, which
  in `collect N <thing>` copies the `action:"wood"` from the contract example. This part fixes ours:
  - **The `resource` wins when the two disagree about which job it is** (`OrderItems.GatherAction`, new, called
    once per message by `CompanionOrders.Run`): the log shows the correction in **one line**
    (`[IA] O modelo devolveu action="wood" com resource="stone" - usei "stone" (o recurso manda).`).
  - **Only in the gathering orders** (`wood`/`stone`/`ore`/`forage`), and only when the resource is an item of **another** job:
    `attack`, `equip`, `drop`, `inventory` and the others are not touched, and a null/unknown `resource` keeps the
    usual behaviour.
  - **No new table:** the resource goes through the same alias map `OrderItems.Resolve` already uses and the item is
    looked for in the **named entries of the four lists the jobs read** (`GatherWood`/`GatherStone`/`GatherOre`
    /`ForageItems`), by prefab or by the game name. A pair that is already coherent (the resource belongs to the job's own list) is not
    touched; a `*` list decides nothing.
  - **The order cap, the count in the pack, the `Resolve`, the radial target and the player's words fallback stay
    as they were.** Verified outside the game (a harness with the real files): **60 cases, ALL CHECKS PASSED**.
- **The model's reply is now guaranteed to be a JSON (Phase 3.1 — structured output).** The request to the model now
  carries `response_format` with a **JSON schema** (`companion_intent`) that fixes the five keys of the contract and the **closed
  list of the 29 actions** — that is what prevents the loose-text reply, which the prompt alone could not avoid. The
  practical gain is being able to use a **small and fast model**: measured with the current prompt (~1766 tokens), the
  `qwen2.5-3b-instruct` got **0 out of 6** replies in JSON (it repeated phrases from the prompt) and went to **6 out of 6**,
  including the target in `attack`/`equip`/`drop`; `google/gemma-4-e4b` (the default) stays the same.
  - **`[IA] SaidaEstruturada`** (new, **on** by default) is the option: it appears on its own in the F8 panel, AI tab, and
    turning it off goes back to exactly the request from before.
  - **No server is left out:** if the server refuses the schema (**HTTP 400**, or an error mentioning
    `response_format`/`json_schema`), the mod tries **once** with `{"type":"json_object"}` and, if that fails again,
    **once without `response_format`** (the old behaviour), warning **once in the log**. A network error or timeout
    **are not** retried: the same request is never sent twice and the mod never breaks because of it.
  - **Nothing else changed:** the prompt, the parser, the personality/memory, the TTS, the panel, the STT and the orders stayed
    the same.
- **The companion's voice echo is FIXED (Phase 3.4, part 3).** The diagnostic (`EcoDiag = true`) showed the
  cause on the first try: Valheim has **one global reverb zone**, on the **AudioListener object itself**, with a
  **1000 m** radius, **4 s** of tail and a `room` of **−10 dB** — that is, **everything** within 1 km got that 4 s
  tail, and the companion's voice was the only thing that made it obvious (the game music and sounds are short or
  repetitive). The game's own sources **avoid** it (the music uses `reverbZoneMix = 0`, the ambience 0.19), but
  our source was at the **Unity default (`1`)** and got the whole echo. **Fix:** the voice now comes out **dry**
  (`reverbZoneMix = 0`) — heard in game and approved ("it cleaned up").
  - **`[IA] TtsReverbZoneMix`** (new, range 0–1, **default 0**) is the real option: `0` = dry voice; raise it a little
    (e.g. `0,19`, the value the game ambience uses) if you want a subtle "space" in the voice. It applies **on the next sentence**,
    without restarting.
  - **The 4 echo test switches are gone** (`TtsBypassReverbZones`, `TtsBypassListenerEffects`,
    `TtsBypassEffects` and `TtsDoppler`): they did their job. They disappear from your `.cfg` on their own — your other
    options are not affected.
  - **`[IA] EcoDiag`** still exists (off by default), now as an **audio diagnostic**: once per session,
    on the first spoken sentence, it writes to the log the scene listener, the world's reverb zones and how the game's
    own sources are configured. **It does not change the sound** — it is there to investigate any sound problem in the future.
  - **Known pending item** (for later): the voice **does not** go through a game mixer group (the music and the ambience
    do). It is not needed for the echo, which is already solved.
- **The path to the fix (Phase 3.4, part 3) — test switches and diagnostic.**
  The companion's voice came out with an **echo** ("like speaking in a big place"), and what was already established ruled out the easy
  hypotheses: the synthesis is **clean** (the WAV from outside the game is identical), **only the companion's voice** echoes
  (game music and SFX are normal), the echo is **identical anywhere, close or far and with the 3 voices
  (42/43/44)** and it happens with **a single sentence** (it is not queue overlapping). It is a **fixed engine process**
  on our `AudioSource`. Who says **which** one is the player's ear, so this part started as **instrumentation**
  (switches, one at a time, and the diagnostic above). Result: **the diagnostic answered on the first try** and
  the fix became one line (`reverbZoneMix = 0`). The record of what was done in the exploration:
  - **6 new keys in `[IA]`** at that moment, all with the default value = how it was before: `EcoDiag` (false —
    diagnostic in the log only), `TtsReverbZoneMix` (1.0, range 0–1), `TtsBypassReverbZones` (false),
    `TtsBypassListenerEffects` (false, the main suspect), `TtsBypassEffects` (false) and `TtsDoppler` (true).
    They appear **in the F8 panel, AI tab** (the `Bind` already feeds `ModConfig.Categories`, no change in the panel was
    needed) and apply **on the next sentence**, without restarting. **In the end 2 were left**: `TtsReverbZoneMix` (now 0) and
    `EcoDiag` — the other 4 were removed.
  - **Application** (`StartChunk` → `ApplyReverbZoneMix`): the `AudioSource` is created once and stays for the session, so
    `reverbZoneMix` is written **only when the value changes** — changing it in the F8 panel applies on the next sentence, without restarting,
    and the first sentence already applies the default. A line goes to the log when it writes
    (`[TTS] ReverbZoneMix da voz: reverbZoneMix=0`).
  - **Diagnostic** (`EcoDiag = true`), **once per session** on the first sentence, prefix `[ECO]`: the `AudioListener`
    (name + `AudioReverbFilter`/`AudioLowPassFilter` and their values), **every `AudioReverbZone`** of the scene
    (`reverbPreset`/`minDistance`/`maxDistance`/`decayTime`/`room` + whether the player and the companion are **inside** the
    radius) and **the game's recipe**: a sample of up to 20 `AudioSource`s of the scene (ours excluded) with `spatialBlend`,
    `reverbZoneMix`, the `bypass*`, `dopplerLevel`, `spatialize` and the **mixer group name**. It is expensive on purpose:
    it runs once per session, never per frame, and never with the flag off.
  - **Reflection:** the mandatory core (the 11 members) stays the same; `reverbZoneMix` is the only **optional** member
    the voice writes; `bypassReverbZones`, `bypassEffects`, `bypassListenerEffects`, `dopplerLevel`, `spread`,
    `spatialize` and `outputAudioMixerGroup` are **optional and read-only** — the one that uses them is the **diagnostic**, to
    snapshot the game's sources. A member that does not exist does not bring the voice down (with a warning in the log). Names checked in the game's
    `UnityEngine.AudioModule.dll`.
  - **Did not change:** `spatialBlend`, the speech end gate, `StopSource`, cache, queue, synthesis, prompt, panel, STT,
    orders and jobs. Branch: **`fix/eco-voz`**.
- **Fixes from the in-game test of the AI panel (Phase 3.7) — the game keys typing in the field, the text box and the
  footer.** Three reports from the in-game test, three root causes:
  - **typing `m` in the text box opened the map** (and the same went for `Tab`, `F5`, `Enter`, `C`, `R`, `G`, `X`, `B`,
    `Z`, `Q`, `F`, `E`, `V` and the hotbar digits). The input guards the mod already had (`Player.TakeInput`,
    `PlayerController.TakeInput`, `Hud.InRadial`, `VoiceInput.BlockedByUi`) only block what **goes through them** — and the
    map is toggled in **`Minimap.Update`**, which reads `ZInput.GetButtonDown("Map")` and **never** consults
    `Player.TakeInput()` (the same goes for the inventory, the console and the chat). On top of that, a `TMP_InputField` only eats
    the keys it handles itself. **Fix:** new `Patches/TextInputKeyGuard.cs` — `ModTextInputFocus` (a registry of the mod's
    text fields + the "is any in focus?" answer **cached per frame**) and **Harmony prefixes** on
    `ZInput.GetButtonDown/GetButton/GetButtonUp/GetKeyDown/GetKey/GetKeyUp` that return `false` while a mod field is
    in focus (the same spirit as `AutoPickupKeyConflict`: with no field in focus the path is **one `int` comparison** and the
    game runs exactly the same). The **gamepad** buttons/keys (`"Joy*"`, `JoystickButton0` and up) and the **mouse** methods
    stay out — the panel reads `ZInput.GetButtonDown("JoyBack")`/`"JoyButtonB"` to close and the
    UI click goes through the `EventSystem`. Typing still types the character (the `TMP_InputField` reads its keys
    through the legacy `Input`/`Event`, never through `ZInput`) and push-to-talk stays blocked with the panel open through
    `VoiceInput.BlockedByUi` (the `V` key only writes `v` in the field).
  - **the text box "half broken" (no cursor and no selection).** `TMP_InputField.OnEnable` is where TMP creates the
    `"Caret"` object whose `CanvasRenderer` (`m_CachedInputRenderer`) draws **the cursor and the selection** — and it only does that if the
    `textComponent` **is already assigned**. The panel built the field with `AddComponent<TMP_InputField>()` on an **active**
    GameObject and assigned the `textComponent` **afterwards**: the cursor was never created and `UpdateGeometry` started
    returning early forever. **Fix:** the GameObject is born **inactive** and is only activated **after** everything is assigned (the
    `"Caret"` is moved to the last child, so it does not draw behind the text); the text now uses `Overflow` +
    `PreserveWhitespaceNoWrap` (the viewport's `RectMask2D` clips and TMP scrolls the text horizontally — before,
    `Ellipsis` truncated it with "..." and the text seemed to jump), a light cursor of 2 px, `selectionColor`, `richText = false`,
    the click positioning the cursor and **Enter applying** like the *Apply* button. The **companion name** field
    (`CompanionInteractPanel.BuildNameInput`) had the same defect and got the same fix.
  - **the panel footer was unreadable.** It was two 11 px lines in dark grey **right over the wood**, with the
    second one practically on the bottom border of the panel. **Fix:** its own dark strip (the same trick as the config
    rows), the summary in **two** localized lines (`hc_ai_panel_status_personality` + `hc_ai_panel_status_voice`,
    12.5 px almost white) and the hint at 12 px, and the panel grew **470 -> 516** so nothing gets cut. The strip is created
    once and the *refresh* stays at 2x/s.
  Nothing of the TTS/STT/orders/jobs/companion behaviour changed. Files: `Patches/TextInputKeyGuard.cs` (new),
  `UI/CompanionAiPanel.cs`, `UI/CompanionInteractPanel.cs`, `Core/ModLocalization.cs`,
  `Translations/Portuguese_Brazilian.json`.
- **Fix — the AI panel dropdown list appeared BEHIND the controls (Phase 3.7).** When opening the
  **Personality** dropdown, the list was drawn **behind** the **Voice** dropdown field, the *In your own words* field
  and the **Apply** button (a report with a screenshot). In uGUI the drawing order is the **order of the children** in the hierarchy and
  the dropdown **fields** were created **after** the list *overlay*, so they became the last children
  of the panel and drew over it. Now:
  - the *overlay* goes back to being the **last child** of the panel after the fields exist (`_overlay.SetAsLastSibling()`
    at the end of `Build()`, with the why commented);
  - the *overlay* got its **own `Canvas`** (`overrideSorting = true`, `sortingOrder` = **34**, one above the panel,
    which is 33) with a **`GraphicRaycaster`** — it is what **Unity's own `Dropdown` template** does
    (`popupCanvas.overrideSorting = true; popupCanvas.sortingOrder = kHighSortingLayer`): the list now draws
    (and receives the click) above the panel without depending on where the *overlay* is in the hierarchy;
  - **clicking outside the list closes the list:** an invisible *catcher* (`Image` with alpha 0 + `Button` with
    `transition = None` — the same *blocker* as Unity's `Dropdown`) covers the whole panel as the **first child of the
    overlay**: it stays behind the lists, so a click on an item goes to the item, and it is only **activated while a list is
    open** (outside that it would steal the panel's click).
  Nothing of the TTS/STT/orders/jobs/patches changed; no per-frame allocation, no `Find*`/LINQ. Single file:
  `src/Companions/UI/CompanionAiPanel.cs`.
- **Audit fixes (Phases 3.4 and 3.7) — the TTS and the AI panel edge-proof.** Nothing that already
  worked changed behaviour; what changed was what crashed, leaked memory or ignored the config:
  - **A1 — race in the TTS `Shutdown` (native crash):** `Shutdown` now **waits for the synthesis thread**
    (`Join(5000)`) before releasing the model, `Process`/`TryGetTts` refuse to synthesize/load after
    `Shutdown` and `Dispose` moved inside the native gate. Before, the thread could be **between two sentences**
    (the "busy" counter at 0) and the model was released with it alive — the next `Generate` on the dead handle
    brought the game down (AccessViolation, which no `catch` catches). If the `Join` times out, the model **stays loaded**
    (as before: better to hold it than to unload it in use).
  - **A2 — native audio with a null handle:** the binding returns a **non-null** object even when the native synthesis
    fails (handle `IntPtr.Zero`), and `NumSamples`/`Samples` do `PtrToStructure` on the handle — that is, reading
    `NumSamples` with a null handle was a crash. Now the handle is checked **first**, the failure goes to the log and the sentence
    is skipped.
  - **A3 — `Speak` after `Shutdown`:** it no longer queues (the line would stay in the queue forever).
  - **A4 — model pre-load with a retry:** `Prewarm` tried **once**; now it tries again every
    **30 s** while you are in the world, until it loads. Along with that, "model not found" stopped being
    final: copying the model folder with the game open now works **without restarting** (the error is logged
    once only).
  - **C2 — TTS cache with a byte cap:** the cap was per **entry** (256) and each entry is a raw `float[]`
    (a 180-character sentence ≈ 1.1 MB) — it could go past hundreds of MB, and each unique reply from the model
    **pushed the fixed lines out**. Now the cap is **32 MB of audio** (dropping the oldest sentences) and **only the
    mod lines enter the cache** — the model replies are new every time.
  - **B1 — native pre-load checks *which* DLL came in:** if `onnxruntime.dll`/`sherpa-onnx-c-api.dll` were already
    loaded from another path (another mod or a copy in the game folder), the log now **warns** — `DllImport`
    resolves by **base name** and it could end up in a version with a different ABI.
  - **D2 — the AI panel does not open on top of anything:** the key is refused with the inventory, the map, the menu, the chat
    in focus, the console, the text input, the vendor search or the **F8 panel** open — and the F8 panel, in
    turn, does not open while the AI panel is visible (never again one panel over the other).
  - **E1 — text missing in English:** `hc_ai_voice_key_conflict` (the warning about the conflict with the game
    auto-pickup) only existed in the PT-BR JSON; now it has the English pair in the defaults (another language no longer shows the raw
    token).
  - **E2 — speed slider aligned with the config:** the panel went from 0.5 to 1.5 while `[IA] TtsVelocidade`
    accepts up to 2.0 — with 2.0 in the `.cfg` the value showed as "2.00" and the first drag lowered it to 1.5 without
    warning. Now both use the **same** limits (`CompanionTts.MinSpeed/MaxSpeed`).
  - **D3 — cursor when closing the panel:** **nothing to do** (documented): it is the game itself that gives the cursor back
    (`GameCamera.UpdateMouseCapture`, every frame, when no window is open) — and forcing it would go against whoever
    turns the mouse capture off on purpose (`Ctrl+F1`).
- **Phase 3.7 — AI panel in game (key `I`): personality, voice with *Listen*, push-to-talk and toggles.** Everything
  the AI reads from the `.cfg` became adjustable **in game**, without editing any file and without restarting. New file:
  `src/Companions/UI/CompanionAiPanel.cs` (a wooden panel in the style of the other mod panels). The new key is
  **`[IA] PainelTecla`**, default **`I`** (free in the game and in the mod: `Z`/`E`/`F8`/`LeftAlt+Z`/`F7`/`V` are ours,
  W/A/S/D, Tab, Esc, F, Q, C, R, X, G, H, T, M, B, Ctrl, Shift, Space and V are the game's). As it is a **letter**, it
  is not stolen from a text box: the chat, the console, the text input and the vendor search keep the
  letter for themselves. It opens/closes with the key, with **Esc** and with the **X**; it blocks the player input while it is
  open (the same Harmony prefixes as the other panels + `Hud.InRadial` + `VoiceInput.BlockedByUi`) and closes itself if
  the player dies or leaves the world. **No companion nearby is needed** (it is global config).
  - **Personality:** dropdown (Default/Viking/Mage/Sarcastic/Guide/Free text) + a **free text** field +
    **Apply** → they save `[IA] PresetPersonalidade` and `[IA] Personalidade` (the free text wins, as always) and the
    log/chat say the effective result (`CompanionPersona.Describe()`). It applies on the **next message**.
  - **Voice (TTS):** dropdown of the **three pt-BR voices** (42 `pf_dora` female, 43 `pm_alex` male, 44 `pm_santa`
    male) + a **Listen** button (speaks a sample sentence through the **same** pipeline — worker, cache and 3D playback —
    and is disabled if the TTS is off or the model is missing) + sliders for **volume** (`TtsVolume`) and
    **speed** (`TtsVelocidade`, 0.5–2.0 in the panel, the same range as the `.cfg`). The voice applies on the **next line**; the volume, on the next audio.
  - **Push-to-talk:** shows the current key (`[IA] VozTecla`) and the **Change** button captures a new one with the **same**
    `KeyBindListener` as the F8 panel (no new logic) + the `VozLigada` toggle. When the key changes,
    `AutoPickupKeyConflict.Reset()` re-evaluates the conflict with the game's *Toggle Auto-Pickup* (Phase 3.3b).
  - **Toggles/values:** `TtsLigado`, `TtsFalarRespostas`, `TtsFalarFalasDoMod`, `Conversa` and the `Memoria` slider
    (0..12).
  - **Saves right away:** each control writes to its `ConfigEntry` and BepInEx saves the `.cfg` at that very moment
    (`ConfigFile.SaveOnConfigSet`). No main-thread blocking, no per-frame allocation (the panel is built **once**,
    on the first `Show`; per frame only the key, the cursor and a status 2x/s) and no secrets in the code.
  - **Texts:** 33 new PT-BR keys in `ModLocalization` + `Translations/Portuguese_Brazilian.json`
    (`hc_ai_panel_*`, `hc_ai_voice_42/43/44/other`).
- **Phase 3.4 — the companion SPEAKS: local synthesized voice (Kokoro-82M via sherpa-onnx).** The companion now
  **says out loud** what he answers, **at his position** in the world. The voice is synthesized **inside the game** by
  **Kokoro-82M** through **`sherpa-onnx`** (NuGet `org.k2fsa.sherpa.onnx` 1.13.8, **Apache-2.0**, in-process) —
  no HTTP, no child process, nothing leaves your machine. It is the mirror of `WhisperStt`: the model loaded **once**,
  synthesis **off the main thread**, a thread-safe queue drained in `Update`. Nothing else changed (chat/voice orders,
  STT, personality/memory/conversation, jobs, UI, radial, bubble and chatter stay the same):
  - **Voices in Brazilian Portuguese:** `[IA] TtsVoz` **42** = `pf_dora` (female, default), **43** = `pm_alex`
    (male), **44** = `pm_santa` (male). The other numbers in the model are from other languages.
  - **Per SENTENCE, with cache:** the text is cut into sentences (`.` `!` `?` `…`, line breaks; a dot between
    digits is a decimal; a sequence of terminators is **one** cut) and the **first sentence already plays while the
    next is generated** — the reply starts sounding quickly. Repeated lines (text + voice + speed) come from the
    **cache**, instantly — and **only the mod lines** enter it (the model replies are new every time), with a
    cap of **32 MB of audio** (see the audit fixes at the top).
  - **3D playback:** **one** `AudioSource` at the companion's position (`spatialBlend = 1`), volume from
    `[IA] TtsVolume`, **one line at a time** (queue, no overlapping), and the `AudioClip` is destroyed when it ends.
  - **What is spoken** (rule: *the voice follows the line that appears in the chat*): the **model's reply**
    (`[IA] TtsFalarRespostas`) and/or the **mod's own line** (`[IA] TtsFalarFalasDoMod`, e.g. *"Ok: gather
    wood (10)."*, *"We already have enough."*). In an **order** the mod's line wins (turn
    `TtsFalarFalasDoMod` off to hear the model's reply instead); in a **conversation** it is the model's
    reply. Nothing is spoken twice (the text that was not spoken goes to the log). An LLM error is not spoken.
  - **Prompt:** the contract now requires **ONE short sentence (up to ~15 words)** in `reply` and says that it is
    **spoken out loud** — short sentence = faster speech. The contract's JSON did not change.
  - **`[IA]` config (PT-BR):** `TtsLigado` (true), `TtsVoz` (42), `TtsVelocidade` (1.0), `TtsThreads` (**4** — RTF
    measured ~0.83, faster than real time), `TtsVolume` (1.0), `TtsModelo` (`tts\kokoro\model.int8.onnx`;
    `voices.bin`/`tokens.txt`/`espeak-ng-data` come from the same folder), `TtsIdioma` (**`pt-br`** — this Kokoro is
    multilingual and the native lib **refuses** to load without a language; `pt` alone is European Portuguese),
    `TtsCache` (true), `TtsFalarRespostas` (true), `TtsFalarFalasDoMod` (true).
  - **Loading the native libs (Unity/Mono):** the managed binding is a plain `[DllImport("sherpa-onnx-c-api")]`
    (it has no loader of its own like Whisper.net), and in Unity Windows would look in the **game** folder, not in the
    plugin one. `CompanionTts.EnsureNativeLibraries` runs before the first use: `SetDllDirectory(<plugin>\runtimes\
    win-x64\native)`, **pre-loads** `onnxruntime.dll` and `sherpa-onnx-c-api.dll` by absolute path with
    `LoadLibraryEx(..., LOAD_WITH_ALTERED_SEARCH_PATH)` and **restores** the search order (`SetDllDirectory(NULL)`)
    — the modules stay in the process and `DllImport` resolves by name. The log says which DLL came from where.
  - **`AudioClip`/`AudioSource` by reflection:** `UnityEngine.AudioModule` cannot be referenced in this project
    (netstandard 2.0 pinned by the voice packages → CS1705; the game's 2.1 facade breaks `Span<T>` → CS7069), the
    same reason as `VoiceInput.Mic`. The resolution is done **once** (11 members) and the calls happen once
    per sentence — the only per-frame work is the `AudioSource` `transform.position` (CoreModule, direct).
  - **Diagnostic:** `[TTS] Modelo CARREGADO em N ms`, `[TTS] Frase i/n: N ms, RTF 0,xx para N,NN s de audio`,
    `[TTS] Fala #n pronta: ...` and the console command **`modia_tts_teste [texto]`** (`F5`) to speak a text
    without needing the LLM or the microphone.
  - **PT-BR:** the "voice not ready" line (`hc_ai_tts_unavailable`, *"the companion's voice is not ready (is the Kokoro model missing?)"*).
  - **Verification:** the **real** `SplitSentences` (17 cases) and the **real** `PickSpokenText` (14 cases, the table of
    what is spoken) compiled into a console harness (net472) — **ALL CHECKS PASSED**; the sherpa-onnx API and the
    loading of the native libs validated in a harness that synthesized real audio (load ~1 s, 54 voices, 24 kHz,
    **RTF 0.73–0.91** with 4 threads). Details: `docs/re/tts-kokoro-sherpa-onnx.md`.

- **Phase 3.8 — "open your inventory": the companion opens his own inventory for the player.** Player request:
  *"would it be possible to make the companion open the inventory when asked?"*. Saying/typing `open your inventory`,
  `show the inventory`, `show me what you carry` (or `close the inventory`, as a bonus) and the **companion's inventory
  window** opens — the **same** one from the interact key, with name, food and equipment. The order comes in
  through the **same** box as the others (`CompanionChatAI.Submit` — chat **and** voice) and ends in the **same** call the
  interact key uses (`CompanionInteract.OpenCompanionInventory` → `InventoryGui.Show(container)` +
  `CompanionInteractPanel.Show(setup)`). No order, job, radial, UI, STT, player bubble or radius/chatter
  was touched:
  - **LLM contract:** new action `inventory` (`inventory_close` to close) in the action list, in the rule and in **2
    examples** of the system prompt (`"open your inventory"`, `"show me what you carry"`). The parser (`LlmIntent`)
    accepts the order in many shapes: exact aliases (`inventario`, `abrir_inventario`, `abre_o_teu_inventario`,
    `mostra_o_inventario`, `mochila`, `bolsa`, `bag`, `abre`, `abrir`, `open`, `mostra`, `mostrar`; `fecha*`/`close*`
    to close) **and** any action that carries the pack name (`invent*`, `mochila`, `bolsa`, `bag`) —
    `open_bag` and `mostrar_o_que_voce_carrega` land on the right action. A phrase that only says "carrega" also has to ask
    to **see**: `carregar_madeira` **does not** become inventory (it stays a gathering/no order).
  - **One companion only** (one window shows one pack): the companion the message was addressed to
    (`CompanionChatAI.PendingSpeaker`, the `PickSpeaker()` that already chooses who the player is talking to) and, if he no
    longer exists when the reply arrives, the nearest one that can receive an order. The choice **does not** take ownership of him — the one that
    takes it is the companion panel in `Show`, as in the interact key.
  - **Container ownership (multiplayer):** when the container is not ours, the interact key path only asked for ownership and
    told the player to interact again. `OpenCompanionInventory` now **returns** the result (`Opened`/
    `Requesting`/`NoGui`/`NoContainer` — the interact key callers ignore it, so nothing changed for them) and the order
    **asks once and opens by itself** when ownership arrives: `CompanionInteract.RequestCompanionInventory` stores the
    container and `TickPendingInventoryOpen` (in `Update`, **once per frame**, `ClaimOwnership` **a single time**)
    opens the window as soon as `IsOwner` answers; it gives up in **5 s** with a warning, and drops the request silently if the
    companion disappeared or if the player opened another window. **Zero** cost when there is no pending request.
  - **Safety net (like the 3.6b one):** if the model answers `none` to `open your inventory`,
    `CompanionOrders.IsInventoryRequest` reads the **player's words** and requires a pack word **and** a verb
    to open/close — `what's in your inventory?`, `how much does your pack weigh?` and `open the door` stay
    conversation/no order.
  - **PT-BR:** `hc_ai_inventory_open` ("Opening the inventory.", also spoken in the companion's bubble),
    `hc_ai_inventory_requesting`, `hc_ai_inventory_timeout`, `hc_ai_inventory_no_gui`, `hc_ai_inventory_closed`,
    `hc_ai_inventory_none_open`, `hc_ai_no_container`, `hc_ai_order_inventory(_close)`.
  - **Verification:** the **real** `LlmIntent.cs` and the **real** `IsInventoryRequest` (cut verbatim) compiled into a
    console harness (net472) and exercised with **45 cases** (12 shapes of model reply, 8 replies that already
    worked, `carregar_madeira`/`carregar_pedra` which **cannot** become inventory and 19 player messages for
    the fallback): **ALL CHECKS PASSED**. In-game recipe: `open your inventory` (chat), `show me what you carry`
    (voice) and `close the inventory`.
- **Phase 3.6b — the order without a target: the model answers `"target": null`.** Player report: "the drop command did not
  work". In the log (voice), the model got the action and the amount right but returned `"target": null` on both requests
  (`"Throw those stones you picked up on the ground."` and `"Throw 10 stones on the ground."`) and Phase 3.6 only knew how to drop what the
  **model field** named — result: `[IA] "drop" -> 0 companion(s)` and "I didn't understand which item.". The same went
  for `equip` and for `attack` (`attack the greydwarf` did not engage anything when the field came empty). Two fronts, without
  touching anything that already worked (gathering/target in the inventory, jobs, radial, UI, STT, radius/chatter, personality/
  memory/conversation and the player bubble stay identical):
  - **Fallback through the player's words (the main one).** `CompanionOrders.Run` now receives the player's text
    (`result.UserText` — it applies to the chat **and** to the voice, which come in through the same `Submit`). When the model does not
    name the target, the new `OrderTargets.Phrases(message)` extracts the name phrases from what the player said — only the
    words that **can** be a name enter: the order verbs (`throw`, `drop`, `equip`, `take`, `attack`), pronouns
    and demonstratives (`essas`, `aquele`, `que tu pegou`), fillers (`coisa`, `lixo`, `isso`), spelled-out numbers and
    **digits** (`10` is the `amount`) and places/directions (`ground`, `outside`, `hand`, `near`, `to`/`for`/`at`) are
    discarded; the rest becomes names of up to 3 words, **from the longest to the shortest** (so `bronze axe`
    wins over a loose `axe`, which would match any axe). Each phrase is then **validated** against the
    packs (`OrderTargets.FindItem`, the same matcher as 3.6) or against the creatures in the radius
    (`OrderTargets.FindCreatureByWords` → `ClosestByName`), with a **higher** bar than the rest
    (`MinPhraseScore = 900`, against `MinScore = 450`): without that, `"drop the stone and the wood"` would become `pedramadeira`
    and match by **substring** with `Madeira`, dropping the wrong item in silence. The model's `target` still
    wins whenever it matches something the companion carries; if not even the message solves it, the old warning stays
    (`hc_ai_need_item`/`hc_ai_no_item`) and the log explains. In `attack` the message is read **before** the aim, and only when
    the model did not name anything: `attack that one` (which names nothing) still goes to the aim, as in 3.6.
  - **Reinforced prompt.** `[IA]` system prompt (`CompanionChatAI.Contract`): `target` is **MANDATORY** in
    `attack`/`equip`/`drop`, with the player's own words and **never** `null`/empty, plus 2 explicit
    `drop` examples (`"throw those stones you picked up"` → `"stone"`, `"throw 10 stones on the ground"` → `"stone"` + `amount: 10`)
    and the note that the item goes **always** in `target`, never in `resource`. The prompt stays lean (3 more lines)
    and the fixed part stays at the beginning, cacheable by the server.
  - **Confirmed:** the 3.6 parser already accepted the item in `resource` when `target` came null
    (`LlmIntent.Parse`, `NeedsTarget` block) — nothing was changed there.
  - **Verification:** the new pieces were exercised against the **`OrderTargets.cs` itself** compiled into a harness
    outside the game (Unity stubs), with 22 messages and a realistic pack — the recipes matched the right item/creature
    and the cases that **must** be refused were refused (`throw the junk away`, `throw those things on the ground`,
    `attack that one`). In-game test recipe: `throw 10 stones on the ground` (drops 10), `throw those stones` (drops the
    stack), `drop the wood`, `equip the bronze axe`, `attack the greydwarf`.
- **Phase 3.3d — the stone order stops at the target and the voice text goes to the player's bubble.** Two problems from the in-game
  test, with no new order/job/UI/STT logic (nothing of the radial, the AI or the 3.3c radius was touched).
  - **`collect 10 stones` never stopped.** Cause confirmed in the game (bundle `c4210710`, `Assets/world/Props/Rock_3.prefab`)
    and in the log: `Rock_3` is a **`Destructible` with `health = 1` and NO drop table** — the stone comes from the
    `Rock_3_frac` that he **spawns when destroyed** (`m_spawnWhenDestroyed`), and that frac is a **`MineRock5`** with
    3-6 `Stone` per hit area (5 areas ≈ 15-30 stones per rock). Since `MHR_GatherRules.DropsOf` only looked at the
    components of the target itself, `Rock_3` looked like "drops nothing" → `AllowTarget` fell into
    `drops.Count == 0 → "unknown drops: don't block it"` → **the order cap (and the `StoneLimit` itself) did not apply
    to it**, so the companion mined forever (in the log: 10+ stones with the order of 2, Pickaxes 6→7→8 and
    no "target reached" line for stone; the **wood** order stopped normally).
    - **At the root:** `DropsOf` now **follows what the target spawns** when it breaks (`Destructible.m_spawnWhenDestroyed` and
      `m_spawnWhenDamaged`, with a depth limit of 3 and the per-target cache that already existed) — `Rock_3` is now
      judged by the stones of its frac, as was already the case with `Rock_4` (which has `DropOnDestroyed` with 3-6 stones).
    - **Safety net (the player's request):** new `MHR_GatherRules.OrderMet` — when there is an **active order for the
      mode** and the **requested item is known** and the **companion's inventory already has the number** (counted as in
      3.2c, `OrderSeen(Bag)`, the cap of what he already had), **ALL** the targets of that mode are refused, not only the ones that
      produce the item. It applies to `AllowTarget` **and** `AllowLoose`; each refusal counts as `CapReject`, so the
      `OrderDone`/`CapRejects` path that already existed ends the order **once** ("Order target reached … - order
      done, back to following") and the companion goes back to following. Without an order, the behaviour is the usual one; a target
      pointed at in the **radial** (`IsDirectedHarvest`) **ignores** the cap, as before.
    - **He stops right away:** `HarvestController.Update` calls the new `MHR_GatherRules.OrderDoneNow` while he is
      **going to or working** a target (never while collecting what that target dropped, so as not to leave stone on the
      ground) — without that, the number was only checked on the next *scan*, and a stone rock can yield a whole rock
      before that. Cost: one inventory count per frame (the same one `IsOverweight()` already does), and only
      with an active order.
  - **The voice text appears under the PLAYER's head.** The push-to-talk transcription (`HeardByVoice`) was repeated
    by the nearest companion (`Speak(PickSpeaker(), text)`), which put the **player's line** in the
    companion's bubble. Now it goes to the **player's** bubble (`CompanionTalk.SayAsPlayer` →
    `Chat.instance.SetNpcText(Player.m_localPlayer.gameObject, Vector3.up * 2f, SpeechCullDistance, SpeechDuration,
    "", text, false)` — same offset/distance/duration as the companion's bubble, no cooldown) and the feedback
    "Understood: …" stays in the **chat/HUD**. `Player.m_localPlayer == null` does nothing. The companion's replies
    (AI conversation, "Leave it to me!", "we have plenty") **stay** in his bubble.
- **Phase 3.3c — larger gathering radius and random chatter off.** Two player requests, with no new logic (no
  order, job, radial, UI, AI or STT was touched).
  - **The companion searches and works further away.** The five gathering radii went up from their defaults:
    `[Advanced] GatheringSearchRadius` **30 → 50 m** (the ring in which he looks for the next tree/rock/plant while following
    you or sent to a target), `[Gathering] FollowRange` **10 → 25 m** (wood/stone/ore following you: the target
    can be up to 25 m from **you**), `[Gathering] ForageFollowRange` **10 → 20 m** (foraging),
    `[Gathering] Range` **40 → 60 m** (with Follow **off**, the work area around home) and
    `[Advanced] GatheringDropRadius` **8 → 12 m** (what he picks up from a felled tree/broken rock).
    **Why 50 on `GatheringSearchRadius`:** following you, the real limit is `FollowRange` (each candidate goes through
    `InWorkArea` → distance from **you**), but the companion can be 25 m from you and the target 25 m on the other side —
    the worst case is ~50 m from the companion, so the ring has to reach there not to become the bottleneck. **Leash checked:**
    with a target in progress `HarvestController` **abandons** the target if it gets more than `FollowRange + 15 m` from you
    (25 + 15 = **40 m** of slack, against 25 m before) and `MHR_Follow` (`[Follow] MaxDistance` 10 m) only takes over again
    when the job becomes **idle** — so the new radius is really usable (the side effect is a longer back and forth:
    he goes up to ~25 m, gathers and comes back when the ring empties). The `AcceptableValueRange`s already accommodated every new
    value (50 ≤ 100, 25/20 ≤ 60, 60 ≤ 150, 12 ≤ 20) — no range changed.
  - **No random comments:** new config **`[Speech] Chatter`** (bool, default **false**). The periodic chatter came
    **only** from the `CompanionTalk.Update()` timer (`_talkTimer` → `ResetTimer()` → `SayContextual()`, every
    `MinInterval`–`MaxInterval` s, picking the line by the context and falling back to 30% `IdleLines` / 70% `FollowLines`);
    with `Chatter = false` the timer **is not even decremented**. **They keep talking** (no silence where it matters):
    the **forced** lines of the AI layer (order confirmation, "we already have enough", conversation replies,
    "Understood: ..." from the voice), the **mode change** line (`SayRandom(ActionLines)`, the "Leave it to me!" — it runs **before**
    the guard), the **event** lines (`hc_speech_*`: fish hooked, planting, repair, unload, tombstone, smelt milestone) and the
    **idle** lines (`[Idle] SpeechInterval`, 300 s, with its own setting). `Chatter = true` brings the whole old behaviour
    back. Cost: an `if` and a `ConfigEntry<bool>` read per frame instead of the decrement — nothing per frame,
    nothing allocated.
  - **Migration:** since the defaults changed, an existing `.cfg` keeps the old values (BepInEx keeps what is in
    the file). Same rule as 3.3b: a value that is **exactly** the old default (`30`/`10`/`10`/`40`/`8`) is
    updated with a log line explaining it; any value chosen by the player **is not** touched. The new key
    `[Speech] Chatter` is created by BepInEx itself with the default **false** (it shows up in the `F8` and in the `.cfg`). The migration
    runs after `MigrateAndClean` (1.4.149), so a `.cfg` still with the old names (`[Harvest] FollowRange`)
    is covered too.
- **Phase 3.3b — fixes from the voice test (V key, STT quality and LLM latency).** Three problems from the
  first in-game test, with evidence in the log (`"Coleta deis mandem."` for 1.1 s of speech; `"O leta deles."` for
  0.7 s; a conversation at `10237 ms` against ~3 s for the orders).
  - **The V key no longer toggles the game auto-pickup.** `V` is the *Toggle Auto-Pickup* of Valheim 1.0.17
    (`ZInput.Reset()` → `AddButton("AutoPickup", KeyToPath(Key.V))`; read in **one** place: `Player.Update`). New
    `Patches/AutoPickupKeyConflict.cs`: a Harmony prefix on **`ZInput.GetButtonDown(string)`** answers `false` only
    for `"AutoPickup"`, and only when `[IA] VozLigada = true` **and** the game binding (read from the game itself,
    `GetButtonDef("AutoPickup").GetActionPath()`) points to `[IA] VozTecla` — rebinding auto-pickup in the game
    turns the block off automatically, and any doubt (unknown key, exception) leaves the game as it is. The
    player is warned **once** (log + one line in the chat/HUD) that they can rebind auto-pickup in the
    settings. Push-to-talk stays the same (hold/release **V**).
  - **Better transcription in PT-BR:** the default model goes from `whisper\ggml-base-q5_1.bin` (57 MB) to
    **`whisper\ggml-small-q5_1.bin`** (181 MB, downloaded from `ggerganov/whisper.cpp`; the `base` **stays on disk** for
    whoever wants to go back through `[IA] SttModelo` — measured on this machine, 8 threads: **638 ms (base) x 2187 ms (small)** for
    a 2 s clip, always in a background `Task`, without freezing the game); a new **initial prompt**
    (`[IA] VozPromptInicial`, `WhisperProcessorBuilder.WithPrompt`, +3-5% of time) with the vocabulary of the
    orders, so the model does not "guess" short clips (and a discard of the prompt when the model repeats it back);
    and the **trim** became more conservative (`VozLimiarSilencio` default `0.01` instead of `0.02`, a `0.2 s` slack instead
    of `0.125 s`), because the old threshold cut the beginning of words spoken quietly.
  - **LLM latency measured:** the log now shows the real prompt size
    (`[IA] Prompt: instrucoes N car. (~N tokens) + mensagem N car. ...`) and the time with the reply
    (`[IA] Tempo: N ms no total, resposta com N car. (~N tokens, ~N tok/s ...)`), with a warning from 5 s pointing
    the local model as the bottleneck; `[IA] Memoria` default dropped from **6 to 4** exchanges (a smaller prompt). The mod **does not**
    change the player's model.
  - **Migration:** an old `.cfg` that is still exactly on the old defaults (`ggml-base-q5_1.bin`, `0.02`,
    `6`) is updated with a log line (the model only changes if the new file is on disk); values chosen
    by the player are not touched. Nothing else changes: chat orders, AI, jobs, UI, radial and the rest of the STT stay the same.
- **Phase 3.3 — voice orders (push-to-talk + local STT with Whisper.net).** Hold **V** and speak (`collect 10 wood`,
  `follow`, `attack the greydwarf`, `good morning`); when you release, the audio is transcribed **inside the game** (Whisper.net, no HTTP,
  nothing leaves the machine) and the **text enters exactly the same path as the chat orders** — order, conversation,
  personality and memory the same, because it is the **same function** (`CompanionChatAI.Submit`).
  - **Capture:** `Microphone.Start(device, loop: false, duration, 16000)` — **16 kHz mono**, the format Whisper
    expects (no 8-bit/22050 Hz like in the old Thrall). The clip is only read up to `Microphone.GetPosition`,
    the silence at the edges is trimmed (10 ms windows above `[IA] VozLimiarSilencio`, with a 0.125 s slack) and the
    audio becomes **a WAV PCM 16-bit mono assembled in memory** (no temporary file). The `AudioClip` is destroyed at the end.
  - **Reusable STT (`AI/WhisperStt.cs`):** the spike became a service — the model is loaded **once** and **kept
    loaded** (pre-loaded in the background when you enter the world), the transcription runs in a background `Task` and comes back
    through a thread-safe queue drained in `Update`, with a watchdog and `NativeGate` (one transcription at a time). **The main
    thread never waits** and nothing is allocated per frame. The spike (`modia_stt_teste`) now uses the **same** model,
    so it is **not** loaded twice.
  - **Feedback:** `Listening...` when it starts; when it ends, `Understood: "collect 10 wood"` in the chat/HUD and the companion
    **repeats it in the bubble**; then `Thinking...` and the usual line (`Ok: gather wood (10)`). Nothing recognised,
    recording too short, no microphone, no model, window open, game without focus, previous request in progress —
    all of that **warns** in PT-BR instead of recording.
  - **New configs in `[IA]`:** `VozLigada` (true), `VozTecla` (**V**), `VozIdioma` (`pt`), `VozDuracaoMaxima`
    (10 s), `VozLimiarSilencio` (0.02), `VozDispositivo` (0 = the first one) and `VozMostrarTexto` (true).
  - **`UnityEngine.AudioModule` by reflection (8 members, once only):** the mod compiles for net472 and references the
    Whisper.net package (netstandard2.0 assets), which pins the **netstandard 2.0** facade; every Unity 6 module is
    built against **netstandard 2.1** and a module outside the game closure (`AudioModule` is outside) gives **CS1705**
    against it. Pointing netstandard at the game's 2.1 facade solves the CS1705 but breaks `AudioClip.GetData`
    (`Span<T>` in mscorlib, CS7069). The reflection bridge is what keeps the build clean without bringing a second BCL
    into Mono. No new reference in the `.csproj`.
  - **Nothing broken:** a pure addition (no change in chat orders, AI, jobs, UI or radial); the only new point
    in the existing code is the shared `Submit` (the `Chat.SendText` patch now calls it, with the same
    behaviour as before) and two new calls in the plugin's `Awake`/`Update`/`Shutdown`.
- **SPIKE — local STT: Whisper.net in-process (a test, not a voice pipeline).** A proof of concept to decide
  between Whisper.net inside the game and a `whisper-server.exe` as a child process. It runs the real model on a
  **background thread** (the main thread never waits), transcribes a test WAV and writes everything to the log:
  `[STT]` detected version/architecture (`whisper_print_system_info`: AVX/AVX2/FMA), **model load** time,
  **transcription** time, number of segments, language and the **transcribed text**; any failure comes out with the full
  message + stack (including `Failed to load native whisper library`).
  - **Triggers:** the console command **`modia_stt_teste`** (F5) and the config **`[IA] SttTeste`** (bool, default
    `false`), which fires on its own ~25 s after the plugin loads (the world already up). Paths through
    **`[IA] SttModelo`** (default `whisper\ggml-base-q5_1.bin`) and **`[IA] SttWavTeste`** (default
    `whisper\jfk.wav`), relative to the plugin folder or absolute.
  - **How the native loader was pointed at our folder:** on net472 the package resolves the
    `netstandard2.0` target, whose `NativeLibraryLoader` does `Path.GetDirectoryName(RuntimeOptions.LibraryPath)` and looks for
    `<folder\runtimes\win-x64\whisper.dll`. `WhisperFactory.FromPath` **has no** `libraryPath` in 1.9.1, so
    we use `RuntimeOptions.LibraryPath = <plugin folder>\Whisper.net.dll` (set before any use of the
    factory) — the plugin folder becomes the first one probed, instead of `AppContext.BaseDirectory` (which in
    Unity is the game folder). The runtime order is locked to **CPU** (only `runtimes\win-x64` exists).
  - **Build/deploy:** a `PackageReference` to `Whisper.net` 1.9.1 + `Whisper.net.Runtime` 1.9.1 and the new target
    **`DeployWhisperNet`**, which copies to the plugin folder **7 managed DLLs** (Whisper.net,
    Microsoft.Bcl.AsyncInterfaces, System.Threading.Tasks.Extensions, System.Memory, System.Buffers,
    System.Numerics.Vectors, System.Runtime.CompilerServices.Unsafe) and the **4 native ones** in `runtimes\win-x64`
    (`whisper.dll` + `ggml-*.dll`). `Microsoft.Extensions.AI.Abstractions`/`System.Text.Json` stay **out**
    (`ExcludeAssets="all"`): they only serve `WhisperSpeechToTextClient`, which is not used, and would drag
    `System.Text.Json` into Unity's Mono.
  - **No blocking and nothing broken:** the spike is a pure addition (no change in chat orders, AI or jobs);
    the only new call in the game is a postfix on `Console.Awake` (to register the console command) and two
    `try/catch` of `Tick()` in `Update`. Details and what to look for in the log: **`docs/re/stt-whisper-net-spike.md`**.
- **Phase 3.5 — personality, memory and free conversation.** The companion now **has a way of speaking**, **remembers
  the latest exchanges** and, when the message **is not an order** (`good morning`, `how are you?`, `what did you think of my
  house?`), **answers as conversation**: he speaks in the world bubble (`CompanionTalk.Say`) and shows the reply in the chat.
  - **Personality:** new `[IA] Personalidade` (free text from the player, up to 600 characters) and
    `[IA] PresetPersonalidade` with presets built into the PT-BR — **`viking`** (loyal and fearless), **`mago`** (old and
    sarcastic), **`sarcastico`** (dry jokes), **`guia`** (patient and didactic) — plus **`custom`** (uses the free
    text). Free text **wins** over the preset; empty = default companion (loyal and practical); an unknown preset warns
    in the log and falls back to the default. The text goes at the beginning of the prompt, along with the companion's **name**
    (`You are Thorin, the player's NPC companion, and he is your owner.` / `Your way of speaking and acting: ...`).
  - **Memory:** new `[IA] Memoria` (6; 0 = off, cap 12). It keeps the latest exchanges (player ↔ companion)
    **per companion**, with a cap of `2 × Memoria` messages (240 characters each, no line breaks) and at most
    8 conversations at the same time. It goes into the prompt as **text** (`Player: ...` / `Thorin: ...`), and not as
    extra `user`/`assistant` messages — the JSON contract stays the last thing the model reads and it never
    receives its own old JSON back. It is **in memory only** (nothing in the ZDO/save) and it is **forgotten** on death
    (`OnCompanionDeath`), on dismiss/despawn (`OnDestroy`) and when the world changes.
  - **Free conversation × order:** the JSON got **`reply`** (it accepts `resposta`/`fala`/`mensagem`/`message`/`texto`/
    `text`/`say`; with no JSON at all, a sentence from the model is now read as `reply`). In an **order** nothing changed: the
    **mod line wins** and the `reply` is only logged (the companion does not say two things about the same order, and the
    localized line is the one that knows how to **name** a creature/item: "Attacking the troll."). In a conversation, the `reply` is spoken (forced bubble)
    and shown in the chat. New `[IA] Conversa` (true): `false` goes back to the exact behaviour of Phase 3.1.
  - **Who speaks:** the **nearest** companion that can receive an order (yours, `Command` on, inside the recall).
    The conversation **does not** take ownership of the companion (that stays only in the orders). With no companion nearby, the reply
    still appears in the chat, with no bubble.
  - **New files:** `src/Companions/AI/CompanionPersona.cs` (presets + resolution) and
    `src/Companions/AI/CompanionMemory.cs` (memory per companion + the prompt block). `CompanionSetup` got
    `MHR_ResolveCompanionId`/`MHR_CachedCompanionId` (the permanent ID, read once) so the memory is filed
    per companion (and not by name).
  - **PT-BR texts:** `hc_ai_default_name`, `hc_ai_preset_default/custom/viking/mago/sarcastico/guia`.
  - **No blocking:** everything stays in a single background call (no `.Result`/`.Wait()`), the prompt is built
    once per message (never per frame) and the memory is bounded at both ends.
- **Phase 3.6 — orders with a target: the companion attacks the creature you named, equips the item he has and
  drops what you tell him to.** `attack the greydwarf`, `kill the boar`, `equip the bronze axe`, `take the
  pickaxe`, `drop the stone`, `throw away 5 wood`.
  - **New model contract:** the JSON got the **`target`** field (it accepts `alvo`, `target_name`, `creature`,
    `item_name`) and the **`attack`**, **`equip`** and **`drop`** actions; `resource`/`amount` stay the same and the
    system prompt (PT-BR) explains the three, with examples, asking for the target **in the player's words**.
  - **How the creature is found:** first the one **in your aim** (camera raycast, the same path as the
    key command) when it matches the name said — point and say "attack that one"; otherwise the **nearest
    one that matches** inside the new **`[IA] RaioDeAlvo`** (40 m by default, the same leash combat uses).
    Players, companions and the dead never enter. Nothing found = **"I don't see any X nearby."**.
  - **How the item is found:** only in the **companion's inventory** (never chests), matching the name said against the
    prefab (`AxeBronze`), the localized name (`Machado de bronze`) and the words of each, with a
    PT-BR/EN dictionary (machado↔axe, picareta↔pickaxe, sucata↔scrap...), singular/plural and empty words ignored.
  - **Passive:** an explicit attack order applies **to that creature even in the Passive stance** — and the
    stance **does not change** (he goes back to "never starts a fight" as soon as the target dies/disappears). Without that the order would be
    swallowed in silence, because Passive is literally "never starts a fight".
  - **`drop` without a number drops the whole stack** ("drop the stone" = get rid of it — the same as the mod does with the
    junk it discards to make room); with a number, at most that much. The dropped stack leaves auto-pickup
    (`m_autoPickup = false`) so he does not pick his own order back up, an equipped item is unequipped first and
    **a quest item never drops**.
  - **`equip`** uses the game's `Humanoid.EquipItem` (an item that is not equippable warns). The mod's auto-equip may
    later swap the weapon back — it is the one that picks the best weapon for the job/fight; the order does not touch
    any setting.
  - **`take the pickaxe` = equip, `take wood` = gather:** the verb "take" is read by the object (equipment
    → equip; resource → gathering job), so **nothing that already worked changed**.
  - **Its own feedback** in the PT-BR (`hc_ai_attacking`, `hc_ai_no_target`, `hc_ai_no_aim`, `hc_ai_equipped`,
    `hc_ai_no_item`, `hc_ai_not_equipable`, `hc_ai_dropped`, `hc_ai_need_item`) in the HUD/chat, and the companion
    **speaks** the confirmation ("Attacking the troll.", "Equipped Machado de bronze.", "Dropped Stone (5).").
  - **New file:** `src/Companions/AI/OrderTargets.cs` (name → creature/item). Combat got
    `EngageByOrder`/`EngagePreyByOrder`; the radial and the old orders stayed **the same**.
- **Phase 3.1 — the player types the order in the game chat and the companion performs it.** `collect 20 wood`,
  `follow`, `stop`, `come here`, `mine iron`, `stay on the defensive`... The phrase goes to a **local LLM** through an
  OpenAI-compatible API (`POST {BaseUrl}/chat/completions`; LM Studio, Ollama, llama.cpp, vLLM) and the
  intent returned in JSON becomes the **same action** as the radial menu/command key. New files:
  `src/Companions/AI/LlmClient.cs` (HTTP client), `LlmIntent.cs` (DTO + tolerant parser),
  `CompanionOrders.cs` (intent → mod action) and `CompanionChatAI.cs` (chat patch + orchestration).
- **It never freezes the game:** the POST runs in a background `Task` and the reply comes back through a thread-safe queue
  drained in the plugin's `Update` (no `.Result`/`.Wait()`), with a configurable timeout, one request at a time and
  a watchdog. A network error becomes one line in the HUD/chat, never an exception in the game.
- **Chat capture:** a Harmony postfix on `Chat.SendText(Talker.Type, string)` — the path of the **player's own
  message** (received messages come in through `Chat.OnNewChatMessage` and are not captured).
  `Ping` is ignored.
- **New `[IA]` in the `.cfg`** (no secret in the code): `Ligado` (true), `BaseUrl`
  (`http://localhost:1234/v1`), `Modelo` (`google/gemma-4-e4b`), `ApiKey` (""), `Temperatura` (0.1),
  `TimeoutSegundos` (30), `Prefixo` ("" = every message; e.g. `/c` only the messages with that prefix),
  `MostrarRespostaNoChat` (true, shows the raw model reply in the chat) and `CooldownSegundos` (3).
- **Actions understood:** follow, wood, stone, ore, gather (plants), smelt, hunt, plant, fish,
  repair/restock, cook; stop (goes back to following + drops the combat target), come to you (recall),
  set home, wander/stop wandering, pick up/stop picking up, accept/ignore commands and the stances
  (balanced/defensive/ranged/melee). `despawn` is **not** exposed to the model.
- **Minimal refactor:** the body of `CompanionRadialMenu.SetActionMode` became
  `CompanionSetup.ApplyActionMode(companion, mode, toggle)` — the radial keeps toggling (`toggle: true`),
  the chat orders **set** the mode (`toggle: false`).
- **Feedback in the PT-BR** through the localization (`hc_ai_*`): `Thinking...`, `Ok: gather wood (20)`,
  `I didn't understand the order.`, `None of your companions is close enough...`, `The AI did not answer: ...`, and the
  new **AI** tab in the F8 panel.
- **Phase 3.2c — the order number is a TOTAL TARGET counted in the companion's INVENTORY (final fix).**
  User rule: *"I ask for 10 wood and he already has 15 in the inventory, so he does not carry it out and says we already have
  enough"*; *"if he already has 7, he gathers until he has 10 or more"*. That is: the number is **a total target**
  (not a delta — the "10 more" reading of 3.3/3.2b was **reverted and removed**), counted **only in the
  companion's pack** (it does not count chests or piles) and **only for the requested resource**.
  - **Order item:** `src/Companions/AI/OrderItems.cs` (new) translates the resource the model returned
    (`wood`/`madeira`, `stone`/`pedra`, `ferro`/`iron`...) into the prefab (`Wood`, `Stone`, `IronScrap`), with a
    PT-BR/EN map, a check in `ObjectDB.GetItemPrefab` and, as a fallback, the localized name of the items of the
    mode's own list. **Did not recognise it?** The target applies to the whole mode (as in 3.2).
  - **Counting:** `MHR_GatherRules.Bag()` sums the item in the `Humanoid`'s `Inventory` (all the stacks). The
    `Stock` (pack + chests + piles) stays what the config uses (`WoodLimit`/`StoneLimit`/`OreLimit`).
  - **Already has enough:** the order **does not even start** and the work mode stays as it was; the companion says
    "We already have enough." (new key `hc_ai_already_have`, PT-BR) and the HUD/chat shows the same.
  - **Only the requested resource:** with the target naming `Wood`, the tree is no longer felled just because the **resin**
    is still below the `WoodLimit` (it was the "keeps chopping to reach 10 resin").
  - **When the target is reached:** the order is closed and the companion goes back to following (he says "we have ... plenty").
    The last tree may go past the number — it falls whole.
  - **Deposit (mitigated limitation):** if the pack fills up and he takes the load to a chest, the count does not
    start over from zero — `CompanionSetup.OrderSeen` keeps the **highest** value seen in the pack during the order.
    That is, the target comes to mean "having **had** N in the inventory" when there is a deposit.
  - **With no order, the radial and `SetDirectedTarget` stay exactly as before** (pure config).
- **Phase 3.2 — the order number now applies: `collect 5 wood` stops at 5.** Before, the order only turned the
  job on and the companion chopped up to the config limit (`[Gathering] WoodLimit`, 500). Now the number becomes
  a **per-companion target**, stored in his ZDO (`HC_OrderCap` / `HC_OrderCapMode` / `HC_OrderCapItem`):
  it works with several companions (each one with its own number), survives a reload and is cleared when the order
  ends, when you say `stop`/`follow`, when another work order arrives or when the work mode
  changes (radial included). It applies to **wood, stone and ore**; a `Name:Number` from the list, if it is smaller,
  still wins in the config path.
- **When the target is reached the companion stops and goes back to following** (like `stop`), with the "we already have plenty" line
  and one line in the log.
- **Phase 3.3/3.2b (REVERTED — see 3.2c above).** It came to apply in Unreleased that the number was a DELTA
  ("collect 10" = 10 MORE): the stock base at the moment of the order was added to the number. That is not what the
  user wants — the code (`CompanionSetup.OrderCapBase` and `MHR_GatherRules.OrderLimit`) was **removed**.
- A complete translation of the **localization system** to `Portuguese_Brazilian`: `Translations/Portuguese_Brazilian.json`
  with **263 keys** — the 204 from the upstream and 59 new ones for texts that were hardcoded in English in the code.
  Keys and placeholders (`{0}`, `{1}`) identical to the English; `English.json` stays the fallback and is
  regenerated on its own when keys are missing.
- A translation of the **186 companion lines** in `Speech/Portuguese_Brazilian.json` (the mod's own speech
  system, `SpeechConfig`): greetings and orders, small talk, combat, hunger, weight, repair,
  smelting, hunting, fishing, planting, cooking and "no bed".
- The build now copies `Translations\**\*.json` from the repository root to `$(DeployDir)\Translations\`
  (the `DeployTranslations` target in `Companions.csproj`) — from where the mod reads UI and speech at runtime.
- Texts that were hardcoded in English and that the player sees now use the localization: title and tabs of the settings
  panel (F8, with the 21 translated categories), the `Default`/`Press a key...` buttons, the home
  zone panel (title, radius and control hint), the HUD health/stamina bars, the tombstone hover
  (`Hold to resurrect`) and the bed one (`X's Bed`), the recall message and the whole **Odin's Shrine** /
  companion list (piece name, description, Alive/Dead/Lost/Unknown status, warnings and recovery).
- Still in English (outside the localization system): the ~250 **setting descriptions** (they only appear in
  `BepInEx\config\*.cfg`; the F8 panel shows the keys), the **technical names of the settings** in the panel tabs
  and the diagnostic **log lines** (kept in English on purpose).

## 1.5.4
- Hunting: with a spear to throw at a deer (and another weapon in its pack), a companion sneaks only to throwing distance, stops and throws, instead of walking right up and stabbing it. New setting Hunting > ThrowStandoff (10 m; it throws from anywhere within ThrowRange, 12 m).
- Hunting: a companion with a spear as its only weapon throws it too once a deer (or other timid game) has noticed it, then lets the animal go and fetches the spear before hunting on.
- Hunting: a companion waits for the stamina a throw needs instead of closing in to stab, and the log says why it isn't throwing yet.
- Hunting: sneaking costs stamina like a player's, so a companion sneaking up on prey stops crouched to get its breath back when low (Hunting > SneakRestStamina 30%, carries on at SneakResumeStamina 60%).
- Hunting: a companion walks quietly (no running) up to prey that hasn't noticed it and only crouches within 20 m of it (Hunting > SneakFrom), like a player, instead of sneaking all the way.
- Hunting: if a much closer animal turns up before a companion has struck at its prey (closer by at least 5 m and under 60% of the distance - Hunting > SwitchCloserBy and SwitchShare), it goes for that one instead.
- Sneaking costs less stamina as a companion's Sneak skill goes up, the same way it does for a player (up to 75% less).
- Fixed: a companion that took a while sneaking up on prey (resting on the way) gave up on it after a minute without attacking, ignored that animal for a minute and went after one further away. Hunting now allows 3 minutes, and the log says when and why a target is given up.
- Hunting: while sneaking a companion wades shallow water and walks round instead of jumping, which stood it up, made noise and spent stamina (it only hops a step it's really stuck on).
- Hunting: chasing prey that bolted, a companion only runs while it has stamina to spare and jogs otherwise (Hunting > ChaseRunStamina 30%), so it doesn't reach the prey with an empty stamina bar.
- Fixed: ordering a companion to attack a deer (or other harmless game) did nothing - it dropped it straight away as "not a threat". The order now starts a hunt: it sneaks up and throws a spear like on the Hunt job.
- Hunting: with Follow on, a companion looks for prey up to 40 m from you (was 25 m - Hunting > FollowRange), closer to what you can see yourself.
- Melee: companions swing once they are a little inside their weapon's reach (Advanced > MeleeReachMargin, 0.2 m), not right at its edge where hits are hit-or-miss (user test: a stab at an unaware deer from the very edge of the spear's reach missed).
- Melee: companions aim a swing or stab at the middle of the target instead of its top (it went over a low animal's back), step back before swinging when standing right on top of it (Advanced > MeleeTooClose 0.3 m), and only stab once they face the target squarely.
- Log (debug): every melee swing, shot and spear throw is logged with distance, height and facing, followed by whether it hit (and for how much) or missed - to track down missed attacks.
- Fixed: after switching the job (e.g. turning Hunt off) a companion kept walking back to a chest for a tool the new job doesn't use, even after you called it back.
- Tools: take a tool out of a companion's pack (e.g. its bow) and it works without it for this job instead of going home for another one. Picking the job again lets it fetch tools again.

## 1.5.3
- Ladders: a tower of short ladders with landings between is now planned as one climb - the companion works out the whole chain at the start and goes from each ladder's top straight to the next one's foot without stopping. Chains that worked are remembered for a while, so after a fall (or the next time) it gets straight back on.
- Fixed: after loading in, a companion with Follow off followed you anyway (the return-to-you step on load switched follow on and never switched it back properly). It now leaves companions with Follow off alone.
- Ladders: a companion walks into a ladder and lets it lift it, like a player, and only hops when that doesn't work; it finishes each ladder on the landing before turning for the next one (it used to turn early and walk off the side).
- Ladders: a companion lines up square in front of each ladder first (like a doorway), walks straight into it and is bumped up it every half second just like you; only when it's really stuck does it hop.
- Fixed: going up a tower of ladders, a companion could walk or hop off the open side of a landing (a 9 m fall). The usual edge check now applies on ladder landings, and it never hops towards an edge.
- Fixed: a companion with nothing to do walked off - back towards where it was first spawned or last brought back, or to its home point up to 60 m away - even right after you sent it somewhere. It now waits and idles where it is, and around home only when it's at home.
- Quieter log: a cornered, fleeing companion logs once every few seconds instead of every moment.
- Ladders: companions walk up ladders instead of running, and when low on stamina they get their breath back on a landing before the next ladder (never part way up).
- Fixed: a companion with a melee weapon swung at an enemy a floor above it (hitting the ladder in between) instead of climbing up to it. Melee range now counts height, and on its way up a ladder it finishes the climb before it fights.
- Fixed: at the top of a ladder a companion could carry on walking the ladder's way and out through an opening in a tower wall (a 10 m fall). It now stops at the ladder's top end or the edge of the landing and turns for the next ladder.
- Fixed: after falling off a chain of ladders a companion couldn't find the first ladder again ("no ladder found" right beside it).
- Fixed: following your path up a tower, a companion could jump up onto a wall top beside the path and fall off the other side.
- Ladders: going down a tower of ladders works the same way - planned once, then down each ladder from its top to its foot and straight on to the next, instead of stopping short of each bottom.
- Companions recognise ladders that are part of the world (a ruined tower's), not only ones you built, so they can climb to an enemy at the top.
- Quieter log: the weapon-choice lines during fights are now debug-only.
- Fixed: at the top of a ladder a companion leapt onto the floor next to it and could carry straight on over a small floor and off the far side. It now steps off onto a floor that is level and close, looks first the way the ladder faces, and only takes spots with real footing.
- Fixed: on a sloping ladder a companion walked on past its top end while making sure the climb was over, ending up off to the side of the next ladder. It now stops there, and checks for a drop only as far as the next ladder's foot.
- Ladders: at the top of one ladder a companion that still has further to climb goes straight for the next ladder instead of stepping off first, and walks round a small opening to reach it (only a drop of over 4 m stops it).
- Ladders: a companion walks all the way to the foot of a ladder before pushing into it, instead of pushing in from beside it and walking along its edge; a narrow opening on the way no longer makes it give the ladder up.
- Ladders: walking to the next ladder a companion keeps to the floor - if the straight line would cut across the open side of a narrow floor, it goes round the corner instead, and only gives the ladder up when there is no floor all the way.
- Ladders: floor and edge checks look at a foot-sized area, so the gaps between a stair-like ladder's rungs no longer read as an edge or "no floor".
- Ladders: a companion pressed against a wall beside a ladder stops trying to get closer after a moment and climbs on from where it is.
- Fixed: at the top of a ladder a companion already at your height could jump up onto a wall top and over the side (an 11 m fall). It only jumps up when you're higher, and never onto something with a harmful drop just past it.
- Ladders: smoother climbing up a chain of ladders - shorter pauses on each landing (it steps on to the next ladder sooner), and while it waits at the top it turns towards the next ladder.
- Fixed: a companion only looked for the next ladder when its target was more than 2 m up, so with an enemy exactly one floor up it stepped off instead and could fall. Now from 1.5 m.
- Fixed: after a fall a companion wouldn't use the ladders it had just climbed again ("no ladder found" right beside them).
- Follow: a companion standing right beside the path you took steps onto it there, instead of walking round to where you last were on its level (which could mean going round a hole).

## 1.5.2
- Fixed: hunting with a melee weapon (spear, axe, knife - no bow ready) - the companion stopped 20 m away and swung at the air. It now sneaks up and strikes.
- Hunters throw their spear at skittish game (deer, hares...) when they have another weapon in their pack, then go and pick it back up.
- New [Hunting] ThrowSpear (on) and ThrowRange (12 m).
- Fixed: a spear counted as a ranged weapon (its long AI range is the throw), so a companion stabbed at the air from far off. Melee weapons now close to their real reach.
- A companion's thrown spears, arrows and bolts lead a running target and aim a little high for the drop, like a player.
- Fixed: following you, a companion could jump a stream and straight back when your path changed; it now wades or goes round instead of jumping back within a few seconds.
- Fixed: a creature it can't reach in deep water (a crocodile watching from the river) made a companion stand still, starting and dropping the fight over and over. It now leaves it unless it's attacked.
- Fixed: arrows were picked as a "bow" when the companion had arrows but no bow.
- Fixed: a companion out hunting far from home gave up its bed because the bed wasn't loaded; it only looks for its bed when it's nearby.
- Companions climb wooden ladders (push into them and hop up, like a player) to follow you up a tower or reach something on a platform, and jump from the top onto a ledge (or up onto a wall top and on to the floor) when the ladder stops short.
- Ladders stacked on top of each other are climbed as one tall ladder, all the way to the top.
- Companions jump onto a ladder whose bottom is off the ground, or when walking into it doesn't lift them, like a player, and try another way after 4 jumps that don't get them on.
- Ladder choice: companions pick a ladder whose foot is on their level and that they can walk to, and whose top gets them closest to where they are going; they climb a chain of short ladders one after another and leave a ladder alone for a while if it keeps getting them nowhere.
- Ladders: a ladder foot close by is walked to straight, without stepping off the edge of a small floor; a companion that falls off on the way looks again from where it lands instead of jumping at the ladder from below.
- Companions stop at the edge of a drop that would hurt (over 3 m, not into deep water) instead of walking off it, whether following, fighting or working, and look for a ladder or a safe way down instead.
- Gap jumps over a drop that would hurt need room to spare (80% of the companion's jump), and a companion never steps off the top of a ladder over such a drop.
- Follow: when you go up or down somewhere the companion can't find its own way (a narrow tower of ladders), it follows the exact path you took, ladders and jumps included - except a drop that would cost it more than 30% of its health.
- Follow: a companion on another floor from you keeps working its way to you (ladders included) instead of counting as "close enough" and idling, and never stops in the middle of a climb.
- Companions no longer try to jump onto a ladder from under a floor: a ladder that starts from a floor above them is climbed from that floor once they get up there.
- Ladders: companions walk round to the foot of a ladder instead of into the wall beside it, and skip a ladder that didn't get them up for a few minutes.
- Companions take fall damage like a player (none up to 4 m; Feather Fall works for them too).
- Companions go back down the ladder they came up. Only with no other way down do they jump down a drop: up to 8 m and never one costing more than 30% of their health. New [Movement] StuckDrop.
- When stuck just below where it needs to be, a companion jumps up onto something it can stand on nearby, like a player; it no longer steps backwards off a high ledge when stuck.
- Companions can use the game's use-to-climb ladders to reach something up out of reach.

## 1.5.1
- Fixed: a companion that died while you were dead too (for example both drowning at sea) left no tombstone and never came back - it now leaves its tombstone and respawns as normal, wherever you respawn.
- Fixed: a companion whose respawn time came while its tombstone was far away could be respawned again and again (one every second) - it now comes back once.
- README and guide: on a dedicated server the server needs the mod too - a server without it deletes companions.
- New companion roster: your game saves each companion's gear and settings every minute and when you log out or quit, per world.
- New [General] ShowCompanionRoster: lists your companions as Alive, Dead, Lost or Unknown.
- New [General] RecoverLostCompanion: brings a Lost companion back next to you (an Unknown one asks you to confirm).
- A recovered companion's old copy is removed if it turns up later, and its old tombstone no longer respawns it.
- New [Advanced] RosterSaveInterval (60 s): how often the roster is saved.
- New Odin's Shrine (hammer, Misc, at a workbench: 10 stone, 5 wood, 3 greydwarf eyes): shows your companions and their status in the game's message window, and offers to bring back a lost one.
- Fixed: a companion looked for a free bed about every 30 minutes instead of every 30 seconds (at night it still looked every few seconds).
- Fixed: a companion kept saying it had no bed after it had found one, until the next night.
- A bed claimed by a companion that no longer exists is free again (single player and host).
- The log now says why a companion couldn't find a bed to claim.
- Fixed: a companion could lose its bed when you loaded in, if the bed hadn't appeared yet - it now waits up to a minute for it.
- A companion can use a bed of its owner's that isn't the owner's spawn point, so clicking its bed by accident no longer takes it away. Once you make it your spawn point, the companion gives it up straight away.
- New [Death] ShrineBuildable, ShrineRecipe and ShrineModel (Tombstone or Ward): turn the shrine off, change its cost or its look.

## 1.5.0
- Major release: everything from 1.4.86 to 1.4.156 below (new movement, idle behaviour, Repair and Restock, cooking, gathering like Hunt, orders first, multiplayer ownership and chest safety, cleaned-up settings), plus the new Companion Guide (GUIDE.md) and Settings Reference (CONFIGURATION.md).

## 1.4.156
- New [Cooking] MakeMead (on by default): off = the cook makes food only - no mead bases and no filling fermenters.

## 1.4.155
- New [Combat] EngageOnNotice (on by default): companions attack a creature as soon as it has noticed you or them, as before 1.4.154. Off = they wait until it's actually coming for you.

## 1.4.154
- Companions no longer open fire on a creature that has only noticed you - only once it's actually coming for you or them (anything that attacks is still fought).
- After a kill, a companion that finds no body and no drops yet waits a few seconds longer for them to come out instead of giving up straight away.

## 1.4.153
- A full gathering companion away from home still unloads into a nearby cart or ship (as before); it now also skips one someone has open on another player's game, or one behind a ward it can't open.

## 1.4.152
- Multiplayer: a companion run by another player's game still treats you as its player - it follows you, sleeps when you sleep and sits when you sit, not whoever's game is running it.
- Multiplayer: orders you give your companion always take effect (they were lost when another player's game was running it).
- Multiplayer: companions no longer lose or duplicate items when they use chests or pick things up.
- Companions don't use chests someone has open on another player's game, private chests other players built, chests behind a ward they can't open, or carts and ship holds.
- Companions respect wards for fermenters, beehives and planting, like a player.
- Crops and wood piles a companion makes are credited to its own player; portal trips with another player's game running the companion now stick.
- Smelt and Cook also use the chests around home, so they no longer take down the wood piles Repair and Restock just built.
- Companions only pick up the honey or pile drops from the hive or pile they emptied themselves.
- Wood, Stone and Ore with Follow on now default to 10 m, like Forage ([Gathering] FollowRange; an existing config keeps its own number).

## 1.4.151
- Forage with Follow on now only picks plants within 10 m of you (new [Gathering] ForageFollowRange) - the berries you happen to walk past, not everything around. Wood, Stone and Ore keep FollowRange (20 m).

## 1.4.150
- Fixed: a patch meant only for companions stopped every creature's attacks from leaving effects on the ground.
- Fixed: Smelt could put ore or fuel into a smelter that was no longer in its pack (duplicating it).
- Fixed: waking up, or getting up from the fire, made a worker whose job turned Follow off follow you out of the base.
- Fixed: a hunter walking home with its catch barely fought back when attacked on the way.
- Fixed: the open-door order couldn't be replaced by a new order and ran during fights and loot pickup.
- Fixed: a gathering companion going over its carry weight in a fight walked to a chest with its back to the enemy.
- Fixed: the walk to the stone pile and the step aside from a falling tree ignored orders and loot pickup.
- Fixed: after switching away from Fish between casts the companion kept the rod in hand and stopped equipping new gear.
- Fixed: a second companion could sit down in the chair an idling companion was already sitting in.
- Fixed: a companion's tombstone didn't float in water or get moved back up if it fell through the ground.
- Fixed: Cancel and leaving a gathering job didn't turn Follow back on; setting Follow yourself is now remembered over the job's own switch.
- Fixed: jobs and idling kept going while seated on a ship, swimming for the shore, or sitting by a fire.
- Fixed: jobs took over a chest you had open (items changing in your chest window) and could clear its "in use" mark.
- Fixed: Smelt and Farm left a chest lid open when they were interrupted.
- Fixed: tool trips could start in the middle of an order; idle chatting could follow you away from the job.
- Fixed: an idle chore cut short kept its fuel in the pack; Farm could lose seeds from the chest with a nearly full pack.
- Fixed: two farmers in one field could plant on the same spot; the bow could flicker while stalking a bird.
- Fixed: after a death and respawn, turning the job off didn't turn Follow back on.
- The "waiting for companions" sleep message only shows on the host, where companions actually hold the night skip.
- The one-time config fix-ups (ForageItems "*", old ViewRange/ViewAngle) only run when upgrading an old config file.

## 1.4.149
- Config clean-up: settings regrouped into clear sections (General, Controls, Follow, Movement, Combat, Food, Stamina, Rest, Death, Looting, GearRepair, one per job, Idle, Speech, Advanced).
- Settings that moved or were renamed keep the values you set - they're carried over automatically the first time you load in.
- Removed 22 settings that did nothing (old combat, AI and Stay Home settings).
- Old leftover lines are cleared out of the config file (removed settings, the old Homestead and Door sections).
- Every setting's description rewritten in plain words, with units.
- Low-level tuning (scan intervals, use distances, tick timings) moved to a new [Advanced] section.
- Movement ViewRange and ViewAngle now actually work (defaults 20 m and 120 degrees - what companions always used).

## 1.4.148
- Fixed: a companion sitting on a ship flooded the log with "Setting linear velocity of a kinematic body is not supported" warnings - ship seats now work like chairs and beds.

## 1.4.147
- Fixed: a running job could pull a companion away from boarding a ship, opening a door, or sitting by a fire / going to bed when you told it to - every job now waits for these orders too.
- Repair and Restock and the idle behaviour wait for all your orders, not just "go there".

## 1.4.146
- Fixed: ordering a companion to repair at a station was cancelled straight away while its job had a task - your order now goes first and the job waits for it.
- The repair log now says why a repair trip stopped (combat, loot, another order, being talked to, the job has work).

## 1.4.145
- Fixed: a "go there" order (or hitching a cart, putting things in a chest, fetching a tombstone) and a running job steered at the same time - every job now waits until the order is done.
- Fixed: a gear repair trip could run alongside picking up loot after a fight - it now waits too.
- A worn item no station nearby can repair is left alone for 10 minutes before it looks again (new [Repair] NoStationWait).
- A gear repair trip that can't find a way to the station, or takes over 2 minutes, gives up; that item then waits the same 10 minutes.

## 1.4.144
- Fixed: with Cook on, a gear repair trip and the cook's task walked at the same time and the companion stood still - the repair trip now waits while Cook, Farm, Hunt or Fish has a task.
- Fixed: an item it can't repair nearby (an arbalest needing the black forge) sent it to the forge over and over to top up nearly-new gear - only the worn items now decide the trip.

## 1.4.143
- New idle behaviour: a companion with nothing to do no longer just stands there - after 10 s it idles near its job until work turns up again.
- Idle chores: repairs damaged pieces, tops up fires, collects honey and puts away what it carries nearby.
- Idle rest: sits on a free chair or bench nearby, or on the ground, for a while.
- Idle wander: strolls about and looks over the stations and chests near its job.
- Idle company: walks over to you or another companion nearby and chats now and then.
- Now and then says why it's idle ("Nothing left to chop here.", "Waiting for the crops to grow.").
- New [Idle] settings: Enabled, StartAfter, Radius, Chores, ChoreRadius, Sit, Wander, Socialize, SpeechInterval.

## 1.4.142
- Code clean-up, no change in behaviour: the build has no warnings any more (old Unity names updated, unused leftovers removed).

## 1.4.141
- Movement clean-up, part 2: the old steering and "unstuck" code is gone - no more stray jumps while a companion stands working (it jumped after standing still for 2 seconds with any job on), and no more teleporting to you after five failed unstuck tries during a fight. The new movement handles all of it.
- Swimming for the shore when low on stamina now uses the new movement (the old way had stopped working).
- The "open this door" command now walks up and opens the door with the new movement; the old door helper and its [Door] settings are gone (doors on the way were already handled by the new movement).

## 1.4.140
- Companions line up with a doorway before going through it, like a player: when their way leads through a door they first step onto the line straight out from the middle of the doorway, then walk straight through. They no longer cut the corner and walk into the leaf of an open door.

## 1.4.139
- Shorter stop at doors: after closing a door that was open towards it, the companion tries to open it again every moment instead of waiting, so it goes through as soon as the door has finished swinging shut.

## 1.4.138
- The open-door fix sometimes didn't find the door at all: inside a house there are so many walls and beams close by that the door search ran out of room before reaching the door. It now looks much further through the list, and an open door is closed after about a second of being held up next to it instead of three.

## 1.4.137
- Still stuck at the open door: the door's leaf wasn't straight ahead, so the look-ahead missed it. When a companion gets stuck it now closes any open door right next to it (and reopens it from its side if that's the way).

## 1.4.136
- Companions got stuck for a moment on a door that was already open towards them (opened from the other side). Like a player, they now close it and, if their way is through it, open it again from their side so it swings away.

## 1.4.135
- Farm paused for a few seconds while planting: it had walked as close to the plant spot as it needed to, but Farm measured the distance differently (straight-line, slope included) and kept waiting. Farm, Smelt and Cook now accept "arrived" the same way the movement does.

## 1.4.134
- Movement clean-up, part 1: Farm, Smelt and Cook no longer have their own "stuck" timers - they give a trip up only when the movement itself gives up (or after 2 minutes). Farm's old timer counted waiting at a door or for stamina as being stuck.
- The last pieces of the retired Homestead / Stay Home code are gone (patrolling home, teleporting home, the home-zone row on the companion panel).

## 1.4.133
- Gear repair: at home it walked to the workbench every time a tool dropped to 99% (the hammer, after each building repair). It now waits until gear is below [Repair] HomeThreshold (80%).
- Gear repair only uses a station a player could use right now - under a roof if it needs one, with a fire if it needs one.
- Repair and Restock: sending the companion somewhere with the command no longer fights the job. It goes, waits there [RepairAndRestock] OrderWait (30 s), then goes back to its chores.
- Repair and Restock now empties beehives (once half full, [RepairAndRestock] HoneyAt) and puts the honey away in the chest that already holds the most honey ([RepairAndRestock] Honey).

## 1.4.132
- Repair and Restock no longer puts a pile on top of another pile - piles can't carry each other and the new one broke. Piles only go beside each other now, on the ground or floor.

## 1.4.131
- Cook job keeps its heat going: the fires under the cooking spots it uses (spit, iron cooking station, cauldron) and the oven are kept at [Cooking] FuelLevel (50%) - when one drops below half of that it's topped back up, one piece at a time. Fuel from its pack or the chests, or a wood pile taken down with a hammer ([Cooking] UsePiles).
- When cooking in the oven it now fills it to FuelLevel instead of a flat 3 pieces.
- (Repair and Restock only fuels fires, torches, braziers and hot tubs - never kilns, smelters or the oven.)

## 1.4.130
- Smelt job: when the chests have no wood for the kiln (or no fuel for a smelter), it takes down a pile of it with a hammer, like a player, picks up the material and uses that. Needs a hammer in its pack; [Smelting] UsePiles turns it off.

## 1.4.129
- Piles are a resource now: when the chests drop below PileKeep, Repair and Restock takes a pile down with the hammer like a player (top of a stack first), picks up the material and puts it back in the chests.
- Smarter pile placement: it scores every free spot beside or on top of a pile and picks the best. It won't narrow a walkway, and won't build by a door, portal, bed, chair, crafting station, cooking spot, fermenter, beehive, chest or a smelter/kiln's ports (its back is fine). It prefers spots backed by a wall or another pile.

## 1.4.128
- After a fight on Forage (or Wood, Stone, Ore, Smelt, Farm, Cook) the companion stuttered: it tried to walk to its next job target and to what the kill dropped at the same time. It now picks up the drops first, then goes back to the job.

## 1.4.127
- Wood, stone and other materials in piles near home now count toward the Wood/Stone/Ore jobs' limits (WoodLimit, StoneLimit and per-item amounts), like what's in the chests.

## 1.4.126
- Repair and Restock now stacks wood, stone and similar materials from the chests into piles (Stack of Wood, Stone Pile, Fine Wood Stack...), built with a hammer like a player. It only adds to piles of the same kind you already built, beside them or on top, and leaves [RepairAndRestock] PileKeep (100) of each in the chests.
- New settings: Piles, PileKeep, PileItems (* / Name / -Name / Name:Number to keep a different amount).

## 1.4.125
- Repair and Restock: when the companion's own gear needed repairing (its hammer, worn from the repairs) it got stuck between walking to the workbench and walking to the next piece. Gear repair now goes first and the job carries on afterwards.

## 1.4.124
- Repair and Restock: the companion took its hammer out and then just stood there - it kept checking for the hammer as a weapon, which the game doesn't count it as. It now swings.
- It also stood next to a pole set under the floor doing nothing: the floor touching the pole counted as "in the way". It now looks at several points on a piece, ignores what's touching it, and if it still can't reach a piece (or fire) from where it stands it moves on after 3 seconds and says why in the log.

## 1.4.123
- Picking up branches, stones and flint is now early-game only: it stops once there's a bronze-or-better axe in the companion's pack, your pack or the chests near home, or a forge at home (it used to depend only on the companion's own axe).

## 1.4.122
- Wood and Stone jobs: a companion with only a stone or flint axe, or no axe at all, also picks up what's lying on the ground - branches on the Wood job, loose stones and flint on the Stone job (on the job's list and under its amount). Whichever is closer, a tree/rock or something on the ground, comes first.
- With no tool for the job (no axe for Wood, no pickaxe for Stone) it only picks things up, instead of standing around.
- New [Harvest] PickUpLoose setting (on by default) to turn this off.

## 1.4.121
- Wood, Stone, Ore and Forage with Follow off now work an area like Hunt: around home when started near home, otherwise around where they were started (new [Harvest] Range, 40 m), and keep going when you walk away instead of stopping 40 m from you.
- With nothing left to gather they walk back to the middle of that area.

## 1.4.120
- Wood, Stone, Ore and Forage work like Hunt: picking one no longer turns Follow off, and picking Follow no longer turns them off.
- With Follow on they only go for trees, rocks and plants within [Harvest] FollowRange (20 m) of you, and otherwise follow; if you move on, they leave what they were working on and catch up.
- Gathering companions that follow you put their load away when you get home, like a hunter.

## 1.4.119
- Cook: each cooking appliance can be switched off on its own - Cauldron, PrepTable, MeadKetill, CookingStation, IronCookingStation, Oven, Fermenter, Beehives and OtherStations (modded ones).
- Repair and Restock now sorts chests wherever it was started, not only at home: started near home it works around home, anywhere else within AwayRadius of where it was started (and goes back there when idle, instead of walking home).

## 1.4.118
- Repair and Restock now leaves pieces above 75% health alone by default (RepairBelow 0.75, was 1 = any damage).
- Each kind of fire can be left alone: RefuelFires, RefuelHearths, RefuelBraziers, RefuelStandingTorches, RefuelWallTorches, RefuelHotTubs and RefuelOther (lanterns, modded fires and the like).

## 1.4.117
- Repair, Restock and Homestead were three wheel jobs that didn't really do the work; they're now one job, "Repair and Restock", that walks over and does each task the way a player would.
- Repair: with a hammer in hand it walks up to each damaged building piece near home and repairs it with a swing (needs the piece's workbench in range and ward access, and costs stamina and hammer wear like a player). If it has no hammer it fetches one from a chest.
- Restock: fires, torches and braziers low on fuel are topped up one piece at a time, with fuel from its pack or fetched from a chest; any fuel left over goes back in a chest.
- New chest sorting: each kind of item is carried to the chest near home that already holds the most of it, and part-stacks are merged (needs a home set; only stackable items unless SortGear is on).
- New [RepairAndRestock] settings: Repair, RepairBelow, Reach, Restock, FuelBelow, Sort, SortItems (* / Name / -Name), SortGear, AwayRadius, ScanInterval, WorkTime, GiveUp.
- The Restock and Homestead wheel slots are now "Pending" and do nothing when clicked; companions saved on Restock switch to Repair and Restock, and Homestead is turned off (Wander still lets them move around home).

## 1.4.116
- After each catch the fisher casts at the closest fish it wants from where it stands, instead of the one it first picked further out.
- The first cast of a session no longer goes twice as far as needed (it starts from a full cast reaching about 24 m and keeps learning).

## 1.4.115
- The fisher may stand on a rock again, like a player, as long as there's something to stand on.
- If it falls off its spot it gets back on; if it falls into the water with a fish on the line, the fish is lost; and if it falls off the same spot twice it fishes from somewhere else for a while.

## 1.4.114
- The fisher may stand in shallow water (up to about waist deep) to fish, but prefers dry land.
- It only fishes from firm footing: level ground at least half a meter all round, so no more hopping onto a small rock and falling off when it casts.
- It prefers fish it can reach with a shorter cast.

## 1.4.113
- Fixed the fisher finding almost nowhere to fish from (1.4.112 was too strict on beaches): any dry ground or dock with water within 5 m counts again, as long as the water is at least a meter away.

## 1.4.112
- Fixed companions getting stuck floating in the water: while swimming they could get stuck steering at a point right under them and never try to get out.
- The fisher never fishes from the water: if it slips in, it reels in and climbs back out to its spot first.
- Fishing spots are at least a meter back from the water's edge, so it doesn't slip in while fishing.

## 1.4.111
- The fisher gets its breath back before each cast: it waits until its stamina is nearly full, so it doesn't start reeling a fish on an empty bar.
- While fishing, companions eat their most stamina-rich food first, and near home they fetch a few stamina foods from the chests if they have none (and put back what's left after the job).
- The running dive into water only happens for a real swim across, not to reach something just off the bank or to get round a fence end.

## 1.4.110
- Companions prefer staying dry like a player: when the way somewhere crosses water they go round on land unless that's a long way in another direction, and otherwise swim (General > DryDetour).
- Jobs (fishing spots, prey, birds, kill drops) accept a short swim to get somewhere and only give up on a long one (General > JobSwim, 20 m). Drops floating in deep water are still left.
- Companions take a running dive into the water whenever their route really does swim, not only while following you.

## 1.4.109
- Fixed companions counting as having stamina at zero, so a fisher out of stamina still landed its fish (a player loses it). This also applies to anything else that needs stamina.
- The fisher fishes for fish up to 20 m out (its full cast reaches about 24 m), so it finds somewhere to fish much sooner.
- Companions only take a running dive into deep water while following you, not on the way to a job.
- Less log spam from the small stamina drains while reeling.

## 1.4.108
- Fixed the fisher's float never coming out: the game's float setup crashed for companions every time the cast hit the water (and kept spawning floats).
- The fisher finds places to fish from much faster and more reliably: it maps the shore and docks around it that are right next to deep water, and picks the one nearest a fish it wants.
- It casts no further than about 14 m (fish come to the float from 10 m around it), and picks another fish when the one it was after has swum off.

## 1.4.107
- Fixed the fisher never finding a spot to fish from on a dock or a high bank: it looked for ground from below the dock, so it only saw seabed. Docks, banks and walls up to 10 m above the water count now, and it fishes from where it stands when a fish is within a cast.

## 1.4.106
- Fish redone: the companion fishes for real like a player, with the game's own float and the fish actually in the water. Fish come to the float, bite, get hooked when it strikes in time, fight, and can get away or break the line.
- It stays around where you started the job, looks for fish within Fishing > ScanRadius, walks to the bank by the one it wants and uses the bait that fish takes best.
- Reeling uses stamina and raises the Fishing skill, and a higher skill reels faster and cheaper, as for a player. Bait is only used up when something takes it.
- New Fishing > Fish list and FishTarget (100 of each, pack + chests near home). Near home it takes the catch home when the pack is full, then goes back to fishing. Fish names are in OfflineCompanionsReborn.Fishing.txt.
- The old simulated fishing settings (WaitTime, ReelTime, HookChance, MissChance, ReelStaminaDrain) are gone; new ones: CastWait, BagFull.
- Every list setting now works the same way: * = everything, Name, -Name = never this one, Name:Number = this one with its own amount. That's Foods, Prey, Fish, PlantAny, ForageItems, GatherWood, GatherStone and GatherOre.
- New amounts: Name:Number in Foods sets that food's target, in Prey that creature's (or meat's) target, in ForageItems stops picking that item at that many, in PlantAny stops planting that crop at that many.

## 1.4.105
- Companions get up in the bed like a player when they wake, then step down beside it (they were playing the get-up on the floor).

## 1.4.104
- Move here, put items in a chest, Repair buildings, Restock, going back to a tombstone and healers going to someone hurt now use the companion movement: routes, doors, stairs, climbing out of pits and keeping out of deep water. A move order it can't find a way to is dropped (the log says so).
- Chasing enemies in a fight uses the companion movement too, falling back to the game's own only when it can't find a way.
- The last steps up to a tree or rock, dodging a falling tree, the door handler and following a non-player use it as well.

## 1.4.103
- Hunters chase prey the way companions follow you: proper routes, climbing out of pits, no jumping into water. Prey they'd have to swim to, or can't find a way to, is left alone.
- With a bow or crossbow, prey down in a moat or pit is shot from the edge instead of followed down.
- A companion far from you and out of sight no longer warps to you while it's still making its way there; it walks.

## 1.4.102
- Companions no longer go straight back to bed right after you load in.
- Companions get up beside the bed onto the floor, instead of on the bed (where they slid off the end).
- Bird hunting: shoots from up to 30 m and jogs into range, since birds only stay down a few seconds; never swims after a bird.

## 1.4.101
- Fixed companions getting up from a chair into the hearth (or onto a table) in front of it and getting stuck inside it: they now step down onto open floor beside the chair.
- A companion still in a fire after 3 seconds (stuck in it) is moved to the nearest safe spot.

## 1.4.100
- Hunters shoot birds for their feathers: when a crow, gull or modded bird lands within range, they sneak up and shoot it with a bow or crossbow, then pick up the drops (Hunting > Birds, FeatherTarget).
- Fixed a companion getting stuck standing in a hearth: it now looks further for a way out, jumps out like a player, and after 6 seconds steps onto the nearest safe spot.
- When a command reaches no companion, the log now says why (too far away for RecallRange, Command switched off, or not yours).

## 1.4.99
- Jumps don't have to be perfect: a companion jumps across water when it can land somewhere shallow enough to stand, then jumps again, like a player. It keeps a quarter of its stamina back for these.
- Shortcuts through water it can wade are no longer ruled out, and gaps are judged a little less strictly.

## 1.4.98
- Companions jump like a player: their own run speed (with mead, wet, cold and other speed effects) carries them across, and a higher Jump skill jumps further.
- Jumping now costs companions stamina like it does a player, and with too little stamina for a full jump they don't try a stream or gap they can't clear; they wade or go round.
- Streams and gaps are only jumped when the companion can actually make it from where it is (width, landing height, speed, skill and stamina all count).

## 1.4.97
- Companions no longer swim out for a kill's drops or follow a body floating into deep water; they wait on the bank and leave what's out of reach.
- Fixed companions staying crouched (sneaking) while swimming.
- Shortcuts are more like a player's: when the straight way is much shorter, companions drop off ledges up to 3 m and leap gaps up to 1.5 m instead of going down the stairs and round (General > MaxDrop, JumpGap).
- Cooks no longer eat the honey they collect for the kitchen; it's put away with the rest.

## 1.4.96
- Companions sneak up on deer, hares and other skittish game that hasn't noticed anyone yet: crouched and not running, so it doesn't bolt (Hunting > Sneak).
- A crouching companion is now really sneaking, like a player: quiet footsteps and harder to spot (light and Sneak skill count). Before, it only looked like it.
- Companions jump narrow shallow streams and puddles too, not only water deep enough to swim, and wade through wider shallow water (General > JumpWaterDepth, JumpWaterWidth).
- Fixed a companion missing the drops of its first kill when it fought something else straight after: fights no longer use up its looking time, it follows a body that slides or floats off, and it looks further around once it gets there.

## 1.4.95
- Fixed companions giving up on a kill's drops about a second after setting off, before reaching the body ("nothing dropped").
- Companions wade into shallow water for a kill's drops and follow drops that float off, but don't swim out for them.
- Cooks walk to each honey after harvesting a hive, so honey that rolls off behind a hive on a slope gets picked up too.
- Wet, Cold, meads and other status effects now change a companion's health and stamina regeneration like a player's (General > StatusEffectsOnRegen).

## 1.4.94
- Hunt works with Follow on: the companion goes after game near you (FollowRange, 25 m) and keeps up with you when there's nothing close.
- Hunt with Follow off hunts around where it is: home if it's near home, otherwise where the job was started (Range, 40 m), and walks back there between hunts.
- Hunt goes after anything that drops meat, modded creatures included; never tamed animals, bosses or companions. New [Hunting] Prey list (* = all, -Name to exclude); the names are in OfflineCompanionsReborn.Hunting.txt.
- Hunt stops on a creature once you have MeatTarget (100) of each meat it drops, counting the pack and the chests near home. Leather and hides are picked up as a bonus.
- A hunter that follows you keeps its catch until you're home, then puts it away. With Follow off near home it walks home to unload when its pack is full.
- Hunt uses a bow first and melee second, and fights anything that attacks it the normal way (blocking and parrying included) before going back to its prey.
- Hunt no longer stops when there's no bow or arrows away from home; it hunts with its melee weapon.
- After any kill, a companion goes over and picks up what the creature dropped, waiting for drops that come out of the body. New [Looting] settings; needs Auto Pickup on.

## 1.4.93
- Cooks collect honey from beehives around home (within HomeTripRange, 60 m by default; at a camp, within the cooking range): from a full hive, right away when a mead base is waiting on honey, and from any half-full hive when there's nothing else to cook. The honey gets put away with the rest.
- Fixed the "Setting linear velocity of a kinematic body is not supported" warning while a companion sleeps in a bed.

## 1.4.92
- Reviewed mead plan: after each boss, cooks brew the mead bases you'll want for the next one first (Eikthyr through Fader, plus Kall Fimbulbringer). The list sets the order, it doesn't limit it: other mead bases you know are still brewed after food, within the same targets.
- Bosses are read from the game itself, so the MeadFocus setting works with new and modded bosses too; OfflineCompanionsReborn.Cooking.txt lists them.
- The log now says why a cook isn't brewing a priority mead base (enough already, recipe not known, no usable station, or an ingredient at its reserve).

## 1.4.91
- Fixed cooks never actually putting mead bases into fermenters (the base disappeared from their bag and the fermenter stayed empty), and thinking a full fermenter was empty.
- Fixed companions trying to stand on top of a fermenter (or chest, station, bed) next to the one they're using. They now stand on the floor beside it.

## 1.4.90
- Fixed cooking (and other chest trips) not reaching any chest in a room with a fire. The "keep out of fires" check now only avoids the flames themselves, not the whole area around a fire.
- Companions carry their best parrying shield again instead of the tower shield. They still switch to the tower shield in a fight when they're hurt or outnumbered.
- Companions no longer put their gear back on piece by piece when you load in. They're already dressed, like you.

## 1.4.89
- Companions jump over a small fire in their way, like a player, and only walk round a big one. Fixed a cook getting stuck stepping in and out of a fire pit on the way to the cauldron.
- Fixed the cook trying the same recipe over and over when an ingredient is only in a chest it can't reach. Such chests no longer count, and a recipe that can't be made is left alone for a minute.
- Companions can use chests on ledges and shelves from the floor below: they look for a place to stand under an overhang too, and check whether they can see the chest from a little higher (like a player's camera).

## 1.4.88
- Fixed companions running back and forth under a chest up on a ledge (seen when emptying their bags after turning Cook off).
- Companions keep out of fires: they don't pick a spot in one, walk round one in their way, and step straight out if they end up in one (seen in a tight kitchen, standing in the fire pit).
- Companions reach chests like a player: anything within about 4 m of their eyes that they can see, so chests up on a ledge or a high shelf can be used from the floor below.

## 1.4.87
- Cooking rewritten. Companions now use every cooking station like a player: cauldron and food preparation table (with a fire and roof, like you need), cooking station, iron cooking station and oven (they put food on, take it off when it's done, and keep the oven fed with wood). Bread and pies go from the prep table to the oven.
- They make the best food they can from recipes you know, until there are 20 of it in the chests nearby, and never use a raw ingredient once 20 or fewer are left, so you always have some to cook with yourself. All amounts are in the new [Cooking] settings.
- Mead: they brew and ferment the meads you'll want for the next boss (the MeadFocus setting, by the last boss beaten), keeping 10 of each mead base and 20 of each finished mead, and tap fermenters when they're done.
- They take finished food off any cooking station nearby before it burns.
- Cooked food goes in a chest that already has it, else one with other cooked food, else the one nearest the cooking area. Food you gave them to eat stays with them.
- They cook in batches like a player (Cooking > BatchSize, 5 by default): several at the cauldron or prep table in one trip, several slots on a cooking station or oven at once.
- The [Cooking] Foods setting limits what they cook (* = everything, -Name to leave one out). OfflineCompanionsReborn.Cooking.txt in the config folder lists the names, mods included.

## 1.4.86
- Smelting now uses the same movement as farming and gathering: routes through doors and up stairs, the shared stuck handling, and a spot it can stand on by each chest and by the smelter's ore and fuel slots. Chests it can't get to are skipped.
- Companions walk up stairs normally again and only jump a step that stops them.
- Removed a false "hasn't moved" warning in the log while a companion stands and fights.

## 1.4.85
- Companions now find their way with their own map of the world. Closed doors don't block their way (they open each one as they reach it, however many there are), and they hop up stairs and steps the normal map doesn't connect. New setting General > CompanionPathing (on) turns it off if it causes trouble.
- Companions find a place to stand by chests in narrow spots more often (anywhere within reach, from more directions).

## 1.4.84
- Fixed the farmer never storing crops in chests up the stairs indoors. Companions now head for the bottom of stairs the game's pathing doesn't use and hop up them.
- Companions can find their way through several closed doors in a row (up to 3), not just one.
- Farmers plant in rows from the far edge of the zone back toward where they came in, instead of in a ring around themselves until they're boxed in by their own crops.
- Companions coming home with no job now put away what they picked up, like after turning a job off. This also happens after loading in at home.
- Companions hop over low things in the way (logs, low walls) instead of running around them.

## 1.4.83
- Fixed companions running a long way round (and warping) instead of hopping up steps or a low wall. When the way round is much longer, they now go straight and jump over it like a player.
- Following companions no longer warp to you while you can still see them, unless they're half as far again as FollowTeleportDistance.
- Fixed companions switching mid-fight to creatures that weren't after anyone (boars while exploring).
- Companions now carry their shield with a one-handed weapon all the time, not only once a fight starts.

## 1.4.82
- Fixed the farmer carrying crops to a chest after every few. It now picks everything ripe first and stores when nothing ripe is left or its pack is nearly full.
- Fixed the farmer planting a few seeds and then stopping.
- Fixed following companions taking a long way round (down past the beach) when you went a shorter way. They now follow your path.
- A standing companion stays put while you're within 2.5 m of it, so it's easy to walk up to.
- Fixed gatherers walking off ledges (into a moat) while stepping in to reach a tree or rock.
- A companion stuck in a pit (a dry moat) now climbs out like a player: it picks the lowest, least steep way up nearby, sprints at it and keeps jumping, resting when out of stamina. If that fails, it's moved back up to where it last had a way to its target.
- Companions now play the interact animation when opening a chest for farming, smelting and putting tools away.

## 1.4.81
- Fixed companions walking all the way home to empty their bags or put a tool back when you turn a job off out in the field. They now do it once they're back home.
- Fixed the Wood job equipping a crossbow and standing there. Bows and crossbows are never used as gathering tools.
- Fixed following companions swimming across a moat when you walked in through the gate. They now follow your path when their own way would mean a swim.
- When you set off, a companion that was standing still now waits 1.5 seconds before following, so it's easy to walk up to, with Wander on or off.
- Fixed setting a farm zone (modifier + Z) also recalling your companions and turning their job off.

## 1.4.80
- Fixed Stone and Ore companions hitting each rock once and moving on to the next. Many rocks turn into a cracked version on the first hit, and companions took that as the rock being gone. They now keep mining the cracked rock until it breaks.

## 1.4.79
- Fixed following companions running off to a gate on the other side of the base. When they can't get to their spot, they now follow your path instead.
- Fixed the farmer picking chests it can't reach (up on a ledge, behind a wall) and running around the building trying. It now only uses chests it can get to.
- Companions no longer go through a door unless it actually leads to where they're going.
- Fixed gatherers swinging at big rocks without reaching them. They now step in until the rock is in reach.
- Gatherers wait a moment for drops to land before moving on to the next rock.
- Companions only start fights with creatures that are going after you or them. Anything that attacks them is still fought.

## 1.4.78
- Fixed the farmer running back and forth through a gate on the way to a chest. Companions now walk to a spot by chests and crafting stations that they can actually reach.
- Fixed companions sidestepping and hopping around between crops while farming.
- Fixed gatherers swinging at a rock over and over without hitting it.
- Fixed companions picking fights with harmless animals (modded crabs and dolphins) and staying "in combat" with something far away, which stopped their job. They now let go of anything more than 40 m away.
- Picking a job now turns Follow off, so the companion stays at the work. Turning the job off turns Follow back on, and picking Follow turns the job off.
- Companions only repair barely-worn gear when they're actually at home (within 25 m), not anywhere within HomeTripRange.
- Smoother following when you walk close to a companion.

## 1.4.77
The third part of the new movement system starts: jobs. Farming and the Wood, Stone, Ore and Forage jobs now move the same way as following and trips.
- Farm and gathering companions find their way around walls, open doors and gates, handle water and get unstuck like they do on trips.
- Gatherers working their way around something to reach a tree or rock no longer give up on it too early. If they really can't get there, they pick another target.
- Companions wait a moment before following when you set off, and stay put while you're right next to them, so they're easy to grab.
- The Wander toggle now also controls following. On (now the default for every companion): moves around you and idles when you stop. Off: keeps one spot near you and stands still.
- Fixed thousands of log warnings while a companion sat on a chair.

## 1.4.76
The second part of the new movement system: following you.
- Companions follow like a player. Each picks its own spot beside, behind or sometimes ahead of you, and moves it now and then.
- Several companions spread out around you instead of lining up behind you.
- Following uses the new movement: doors, water, stream jumps and getting unstuck all work while following.
- In doorways, on stairs and in narrow paths, companions follow your path and spread out again when there's room.
- When you stop, companions wait where they are and idle nearby.
- After you've stood still for a while, companions come back and wait near you. New settings `Regroup` and `RegroupTime` (Follow, on, 45 s).
- Indoors, companions stay closer (`IndoorDistance`, Follow, default 4 m) and sit down on a free chair or the floor when you stop. Near home they empty their bags first. New setting `SitIndoors` (Follow, on).
- New setting `MaxDistance` (Follow, default 10 m). `FormationOffset` and `FormationCatchupDist` no longer do anything.

## 1.4.75
- Fixed companions walking back and forth while farming. With a job on, they now stand still between tasks instead of wandering back to their idle spot.
- The farmer now goes straight to the next ripe crop close by, like working down a row, instead of pausing and rescanning the field. New setting `NextCropRange` (Farm, default 3 m, 0 = off).
- Fixed companions getting stuck in doorways when no route could start from where they stood. They now step a meter or two to find one.
- Fixed doors being closed behind a companion that was still stuck next to them.
- Fixed a weapon check that never ran. Companions in Passive stance now hold their strongest weapon.
- Farm scans now write one line to the log instead of one per crop.
- Reborn no longer shows as incompatible with ProfMags' original mod. It still won't start if the original is installed, and the log says to remove it.
- The mod is now built from regular C# source code. It should play the same; if something seems different, please send your log.

## 1.4.74
The first part of a new movement system. Tool trips, emptying bags, repair trips and walking to bed now all move the same way. Following and jobs move over in later updates.
- Fixed companions on trips going the wrong way around walls, falling into moats and giving up after 20 seconds. They now follow the route corner by corner.
- Companions open closed doors and gates on their way, including from the bottom of short stairs, and close them again behind them.
- When stuck, companions step back to one side, jump toward the next turn of their route, try another way around, and only then give up.
- Companions in deep water swim to the bank nearest where they're going, and only one they can climb out on.
- Companions walk the last few steps, jog in between, and run when the destination is far away (`WorkRunDistance`).
- Fixed companions avoiding all water when low on stamina. Wading is always fine; they only avoid swimming if their stamina won't last, and otherwise wait on the bank to rest.
- Companions jump narrow streams to stay dry and take a running jump into water they're going to swim.
- Fixed companions ignoring targets in shallow water and chasing modded sea animals. They now follow targets into wading depth, and only into swimming depth if attacked and armed with a bow or crossbow.
- Fixed companions chasing animals that never attack (deer, and modded fish and dolphins). These are now left alone.
- Fixed crossbows never firing. Companions now reload them like a player.
- Fixed bows and crossbows counting as ready without the matching ammo.
- Fixed automatic repairs only finding a station within 20 m. Near home, any station near home counts.
- Fixed companions retrying a repair trip to a station they can't reach. They now look for another tool, or say it's broken and follow you.
- New setting `FollowStuckTeleport` (General, off by default): a following companion stuck out of sight for 15 seconds appears next to you.

## 1.4.73
A review of the Wood, Stone and Ore jobs turned up several places where companions didn't play by a player's rules.
- Fixed companions damaging a tree or rock they couldn't reach. They now pick a new target instead.
- Fixed every swing at a tree landing an extra hit. It's one hit per swing, and only when the swing connects.
- Fixed pickaxe swings that hit the ground counting as hits on the rock.
- Fixed companions being left with no weapon or shield after tool, fight and job switches. Their normal gear now comes back.
- One-handed tools no longer take the shield or torch off.
- A broken tool now comes off and the companion switches to its best weapon, then repairs or replaces the tool near home.
- Fixed swings wearing down a shield or torch.
- Fixed companions standing on stumps and logs and shuffling forward while swinging.
- Fixed overweight companions flickering in and out of their job. They now say they're too heavy, pause the job and check for somewhere to unload.
- Drop-off rules: chests only near home; away from home, also a nearby cart, ship or outpost chest. Never tombstones or other players' private chests.
- Stone from the Ore job goes in a pile a few meters from the deposit instead of being hauled home.
- Fixed large copper and tin deposits being abandoned partway. They're now mined until finished.
- After a tree falls, companions go to the nearest log, if your gather list and limits allow it.
- Companions collect drops from around a rock and pick items up from a player's reach (about 2 m).
- Fixed the Forage job skipping wild barley and flax.
- Companions turn toward their work instead of snapping to face it.

## 1.4.72
- Fixed companions taking off all their armor after their inventory reloaded. They now keep wearing their gear.

## 1.4.71
- Fixed companions being unable to open a door from the bottom of the stairs below it. They now open doors from anywhere in reach.
- The same in-reach rule applies to smelters, kilns, cauldrons, fermenters, fires, chairs, crafting stations, repairs and chests. Planting still needs them on the field.
- Fixed companions walking home from far away when a tool broke. Chest and repair trips only happen within `HomeTripRange` (General, default 60 m); further away they say so, pause the job and follow you.
- Near home, a broken job tool is repaired at a workbench or forge, or replaced from a chest.
- Near home, companions repair any wear on their gear, not just items below `DurabilityThreshold`.
- Gatherers stand a little further back so they no longer end up on top of stumps.
- Emptying bags now uses `HomeTripRange` too (was a fixed 40 m).

## 1.4.70
- Fixed the farmer taking vegetables out of a chest for seed plants and storing them straight back. They stay in its pack until that seed reaches `SeedTarget`.
- Fixed companions spinning in a corner trying to reach stacked chests. They now use chests and pick crops from anywhere in reach, up to about 2 m above or below.
- Companions sprint in bursts: run down to half stamina (30% in a fight), walk back up to 90%, then run again.
- When a job is changed or turned off, or Homestead turned on, a companion near home puts away what it gathered first. It keeps equipped items and the gear, ammo, food and mead you gave it. If it's away from home, it does this when it's back.

## 1.4.69
- Fixed companions waking up beside the bed instead of where they slept.
- Companions walk while working and only run to far targets. New setting `WorkRunDistance` (General, default 25 m, 0 = never run for work).
- Fixed the farmer making a separate chest trip for each seed type. It now takes up to `SeedCarry` (Farm, default 40) of each type in one trip.

## 1.4.68
- Fixed the farmer giving up on a field or chest just above or below it. It now keeps walking until it's actually there.
- Fixed the farmer planting one crop until it ran out of seeds. It now switches type every `PlantGroupSize` plants.

## 1.4.67
- Fixed farm zones never finding seeds in chests.
- Fixed companions on tool trips being blocked from a chest by nearby chests and furniture. Only walls, floors and large solid pieces block reach now.
- Companions only head for a chest they can walk all the way to, and try another chest with the tool if one can't be reached.
- The stuck jump only kicks in when really stuck, not while walking around a building.
- The default `PlantAny` list uses the real names `MushroomJotunPuffs` and `MushroomMagecap`. Short names still work, and existing configs keep their old value.

## 1.4.66
- Fixed companions missing nearby chests in large bases. Every chest in range is now checked.
- Fixed a farmer with only a few seeds and no vegetables never planting. It now plants the seeds to get started.
- Fixed companions taking tools out of chests through walls. They now open chests only from a spot they can walk to with nothing solid in between.
- Companions stuck on stairs or ledges on tool trips and farm work jump toward where they're going.
- `PlantAny` names ignore capital letters, and "Nothing to plant" log lines show how many seeds and vegetables were found.
- Debug logging for tool trips and farm work: position, destination and whether there's a path, every 2 seconds.

## 1.4.65
- Fixed a job and Stay Home both steering the companion back and forth. Picking one now turns the other off.
- "Stay Home" on the command wheel is now called "Homestead".
- Fixed companions crawling slowly away from a chest when Follow was picked mid tool trip. Trips now finish first, and a companion that's attacked fights and then carries on.
- Fixed Any farm zones planting tree seeds. They now only plant what's on the new `PlantAny` list (Farm).
- New setting `PlantGroupSize` (default 4): plant this many of one type before switching, so mixed fields end up in small blocks.
- New settings `SeedMinimum` (default 20, replaces `SeedReserve`) and `SeedTarget` (default 100) control when the farmer grows seed plants or crops.
- Barley, flax and other self-seeding crops are always planted, keeping `SeedMinimum` in the pack for replanting.

## 1.4.64
- New gather lists `GatherWood`, `GatherStone` and `GatherOre` (Harvest) set what each job collects and how much, with `WoodLimit`, `StoneLimit` and `OreLimit` (500 each) as category limits.
- Limits count the companion's pack and chests near home. Once everything is at the limit, it says so and waits.
- New setting `WoodSkipTrees` (default `Oak`): trees the Wood job never fells.
- New setting `BaseProtectRadius` (default 10 m): the Wood job won't fell a tree that close to anything built.
- Companions step clear of falling trees and splitting logs, and approach from the uphill side on slopes.
- A gatherer with a full pack takes a load to the home chest. With no home chest, `JunkCleanup` (on by default) drops junk to make room; gear, tools, ammo, food, seeds and `NeverDiscard` items are kept.
- Fixed the Ore job mining plain boulders that only drop stone.

## 1.4.63
- Fixed tool trips giving up before reaching the chest. Companions now walk to the front of the chest and only give up after 20 seconds without progress or 2 minutes.
- Repair (buildings), Restock and Hunt now pause during a tool trip like the other jobs.
- Routine farm scanning lines only show at Debug log level.

## 1.4.62
- When a job needs a tool the companion doesn't have, it takes the best one from a chest near home.
- Borrowed tools, ammo and bait go back to the chest when the job is turned off or changed. Tools it already had stay with it.
- If there's no tool in the chests, the companion says what it needs.
- The farmer keeps a reserve of carrot, turnip and onion seeds, planting vegetables as seed plants when short. New setting `SeedReserve` (Farm, default 20, 0 = off).

## 1.4.61
- Fixed item icons from a chest showing over the companion's inventory.
- Fixed companions being pushed to the side when getting out of bed.
- Fixed Farm and gathering jobs restarting every time the companion's inventory changed.
- Fixed the farmer leaving harvested crops on the ground and storing unrelated items in the crop chest.
- Forage leaves crops in your farm fields for the Farm job.
- Fixed companions standing in an open doorway for 8 seconds and giving up on their target.
- Fixed the "[Diag] ... hasn't moved" warning showing while a companion was working.

## 1.4.60
- Fixed companions vanishing when another mod deleted them. They're now protected and can still die and respawn normally. New setting `ProtectCompanions` (General, on by default).

## 1.4.59
- Fixed companions being deleted inside active wards by the "Guardstone wards remove monsters" mod.

## 1.4.58
- Fixed Farm filling the log with "Invalid IL code" errors and never farming when another mod changes the terrain lookup.
- Farm, Fish and the gathering jobs now write what they're doing to the log.
- Fixed one food message being logged every frame.

## 1.4.57
- Recall now only reaches companions within `RecallRange` (Controls, default 40 m) that have Command on. Point-and-command orders use the same range.
- Hold the command key for `SuperRecallHoldTime` (Controls, default 2 seconds) to recall every companion at any distance.

## 1.4.56
- When you get into bed, companions near their own beds go to bed too.
- At night, companions near their bed go to sleep even while working, and pick the job back up in the morning.
- Fixed the night skip waiting forever on a companion that couldn't reach a bed. It now gives up after 30 seconds.
- Recalling your companions wakes any that are asleep.
- New settings (Sleep): `SleepAtNight`, `SleepWhenPlayerSleeps` and `BedRange` (default 50 m).

## 1.4.55
- Fixed controller stick errors filling the log and the stick not working on the command wheel.

## 1.4.54
- The mod's ID is now `Maddhattr.OfflineCompanionsReborn` and it shows as "Offline Companions Reborn" in the config manager. Settings carry over automatically.
- Reborn won't load if ProfMags' original Offline Companions is also installed. Remove the original to use Reborn.

## 1.4.53
- Every companion now has a permanent ID that stays with it through death and respawn.
- Fixed companions collecting another companion's tombstone when they shared a name and owner.
- Beds remember companions by ID, so renaming a companion keeps its bed.
- The config file is now `Maddhattr.OfflineCompanionsReborn.cfg`. Settings are copied over automatically; the old `com.profmags.companions.cfg` can be deleted.
- Fixed the starter companion window never opening with Valheim Plus's "disable arrived message" setting.

## 1.4.52
- **Change:** Despawn on the command wheel now asks you to confirm with a Yes/No box that names the companion and says its items will be left in a tombstone. This replaces the old "press the wedge again within 3 seconds" confirm, and works the same with mouse or controller.

## 1.4.51
- **Issue:** Dragging an item a companion was wearing into your own inventory left it looking equipped there.
- **Fix:** The companion takes the item off first, so it arrives in your inventory as a normal item.
- **Issue:** Despawn on the command wheel gave an error and did nothing, left over from the 1.4.32 bed update.
- **Fix:** Despawn works again. The companion leaves a tombstone with everything it was carrying, and its bed is freed up.

## 1.4.50
- **Issue:** After you slept through the night, a companion got out of bed, climbed right back in, then got up again a few seconds later.
- **Fix:** When your sleep ends the night, companions get up and stay up.
- **Issue:** A companion that came back with its gear only put on one piece, and didn't put on the rest until later.
- **Fix:** If it's still putting on one piece, it now waits and then puts on the rest.

## 1.4.49
- **Issue:** When a companion kept all its items through death, its map marker vanished right away because the tombstone was empty, even though that tombstone is where you'd go to bring it back.
- **Fix:** The marker now stays while the companion is waiting to be revived. After it comes back, the marker only stays if items were left in the tombstone.
- Cleaned up a leftover debug message that showed up as a warning in the log.

## 1.4.48
- **Issue:** A companion's bed said "unclaimed" while the companion was dead, and could show "HC_Companion(Clone)" again after it came back.
- **Fix:** The bed claim is now saved on the bed itself. It keeps the companion's real name through death, travel, and reloading the game, and other players see it too.
- **Issue:** After respawning, a companion with items left in a tombstone far away would stand around, refuse to fight, and ignore Follow for a couple of minutes, then give up on its items for good.
- **Fix:** A companion only goes after its tombstone once it's actually nearby. Until then it follows, fights, and works as normal, and it never forgets the items. It fights off attackers before going back to looting, and it remembers every one of its tombstones, not just the latest.
- **Issue:** A companion's tombstone disappeared from the map as soon as it respawned, even though its items were still in the tombstone.
- **Fix:** The map marker now stays until the tombstone is emptied, whether you empty it or the companion does.

## 1.4.47
- **Issue:** Auto-respawn always brought a companion back at its tombstone, never at its bed or next to you. When the tombstone was far away, the respawn failed completely.
- **Fix:** Auto-respawn now uses the claimed bed first (a free spot beside it), then next to you, or the spawn stones if you've turned that on.
- **Issue:** After dying and coming back, a companion forgot which bed was its own.
- **Fix:** Companions keep their claimed bed through death.
- **Issue:** Companions never said they needed a bed, even though that line was added in 1.4.30.
- **Fix:** They now speak up when they don't have a bed.

## 1.4.46
- **Issue:** A dead companion's auto-respawn timer stopped, and started over from zero, whenever you went far from its tombstone or reloaded the game.
- **Fix:** The respawn time is now saved with the world. The companion comes back on time no matter where you are, even after quitting and reloading.
- A companion that respawns while you're far from its tombstone leaves any dropped items in the tombstone and goes back for them. An empty tombstone clears itself the next time its area loads.
- A companion with no claimed bed now respawns next to you by default. New setting `AutoRespawnAtWorldSpawnWhenNoBed` sends it to the spawn stones instead. This replaces `AutoRespawnNearOwner`.
- In multiplayer, only the companion's owner's game brings it back, so two players can't respawn the same companion.
- Controller: the stance area of the command wheel is bigger, so stances are easier to pick.

## 1.4.45
- **Issue:** In 1.4.44, the command wheel wouldn't highlight anything on a controller, and a companion that had slept wouldn't follow you after waking.
- **Fix:** Corrected two new pieces of code that crashed when they ran.
- **Issue:** Companion crafting (cooking and homestead work) and taking seeds for farming could crash, left over from the 1.4.29 game-patch update.
- **Fix:** Corrected the same kind of problem in both.

## 1.4.44
- **Issue:** A companion's bed could show "HC_Companion(Clone)" instead of its name.
- **Fix:** The bed's name label is now only set when the companion claims the bed, using its real name.
- **Issue:** Companions going to bed ended up lying on the floor next to it.
- **Fix:** Companions now lie in the bed the same way a player does.
- **Issue:** Every time a companion slept, it quietly moved its Home point to a spot beside the bed, even if you never used Set Home.
- **Fix:** Home now only changes when you use Set Home.
- Added logging for controller stance selection in the command wheel, to help tune how it feels.

## 1.4.43
- **New:** What a companion loses when it dies is now one setting, `DeathItemRule` (under Respawn). 0 = follow the world's death penalty setting, the same as a player would (default). 1 = keep everything. 2 = keep equipped gear, drop the rest. 3 = drop everything. This replaces the old `DropInventoryOnDeath` and `DropEquippedGearOnDeath` toggles.
- In multiplayer, a companion always uses its owner's setting, even if another player's game happens to be handling it when it dies.
- **Issue:** When a companion auto-respawned at its bed, it instantly pulled its dropped items out of the tombstone, even from across the map.
- **Fix:** After an auto-respawn, dropped items stay in the tombstone and the companion goes back for them. Resurrecting a companion by hand at its tombstone still gives the items back right away.
- **Issue:** When keeping items through death, a companion could lose some of them. Anything past 16 stacks was deleted, and about half of the items that should have been kept went into the tombstone instead.
- **Fix:** Kept items now all come back with the companion.

## 1.4.42
- **Issue:** On a controller, picking a combat stance in the command wheel was really hard. The stance only highlighted while the stick was held at a very specific partial push, so it kept flickering off.
- **Fix:** Stance selection is now "sticky." Push the stick partway toward a stance (up for Balanced, right for Passive, down for Ranged, left for Melee) and it stays highlighted after you let go. Press the use button to apply it. The stance area is also a bit larger. Pushing the stick all the way out still selects the outer actions like before. Mouse and keyboard are unchanged.

## 1.4.41
- **Issue:** For some players (reported with the game set to Russian), the game could hang on the loading screen for a couple of minutes and then close.
- **Fix:** The mod was reloading its translation files every time the game refreshed its language settings, which some setups do many times at startup. It now only reloads when the language actually changes.

## 1.4.40
- **Issue:** A companion running the smelter would get the kiln lit but then keep interrupting itself every time it tried to insert ore or fuel, never finishing the job.
- **Fix:** Removed a bug that made the companion think its job had changed every time it used an item from its own inventory. It now stays on task.

## 1.4.39
- **Issue:** Companions could silently fail (and spam the log) while inserting ore into a smelter.
- **Fix:** Corrected a missing piece of data the game expects when adding ore. Fuel insertion was never affected.

## 1.4.38
- **Issue:** Follow and Stay Home could both end up "on" at once, with Stay Home quietly winning forever.
- **Fix:** Follow and Stay Home are now mutually exclusive — turning one on turns the other off. "Set Home" also no longer turns Stay Home on by itself; it just sets the home point.

## 1.4.37
- **Issue:** A companion could crash if it picked up ore/fuel from a chest at the exact moment its gear happened to re-equip.
- **Fix:** Closed that timing gap so it's handled cleanly instead of crashing.

## 1.4.36
- **Issue:** Companions could get "stuck" wandering near a chest instead of using it, without ever giving up and trying somewhere else.
- **Fix:** The stuck-detection now checks whether the companion is actually making progress toward its target, not just whether it's moving at all — so it correctly notices, gives up on that chest, and tries another.

## 1.4.35
- **Issue:** A companion's claimed bed showed a confusing "Unclaimed (claimed by ...)" label with a garbled name.
- **Fix:** It now reads cleanly as "\<Companion's Name\>'s Bed."

## 1.4.34
- **Issue:** Companions weren't sleeping, "Set Home" seemed to silently fail, and companions moved erratically near beds.
- **Fix:** All three turned out to be the same root cause — a bed-ownership check that was failing every time. Fixed, and all three symptoms went away together.

## 1.4.33
- **Issue:** A rare crash could hit bed logic, tombstones, or a few other spots depending on the exact build, seemingly at random.
- **Fix:** Found and corrected the underlying bug everywhere it appeared in the mod, not just the one reported spot.

## 1.4.32
- Companions now claim a specific bed as their own — permanently, not just while sleeping in it — the same way a player would settle into one. That bed shows "(claimed by \<name\>)" and is their auto-respawn point.
- A companion automatically claims a bed when you Set Home nearby, after a short delay if they don't have one, or if they notice you've claimed a bed and they haven't.
- Pointing at a bed and commanding a companion is now a manual override: claims it if free, sleeps in it if already theirs, or refuses if it belongs to someone else.
- Claiming a new bed releases the old one back to the pool.
- Companions no longer grab a random nearby bed at night — if they don't have a claimed one, they'll say so.

## 1.4.31
- Added an automatic respawn timer for dead companions, alongside the existing manual tombstone resurrect. Configurable delay, default 5 minutes.
- Auto-respawn happens at a companion's claimed bed if it has one, otherwise world spawn (or optionally near the owner).
- Companions now keep 100% of their inventory by default when they die, instead of always dropping it. Two new options let you change that.
- If items were dropped, the companion will walk back and loot its own tombstone once it's close enough. The tombstone disappears automatically once nothing's left in it.
- A companion's tombstone now shows up on the map, like a player's own death marker.

## 1.4.30
- Companions no longer take over a bed a player has personally claimed.
- A bed a companion is using now shows "claimed by \<companion name>" so it's clear it's taken.
- Companions will speak up if they don't have a bed to sleep in.

## 1.4.29
- Updated for a Valheim game patch that changed how a few things work under the hood (inventory, gear visuals, piece placement). No behavior change intended — this just keeps the mod working.

## 1.4.28
- **Issue:** Companions could get stuck standing at "bow range" even after correctly switching to melee when they ran out of arrows.
- **Fix:** Positioning now follows the weapon actually in hand, not the companion's general combat stance.

## 1.4.27
- **Issue:** A direct attack command could bypass a companion's normal combat AI (weapon choice, ammo checks, fleeing) entirely.
- **Fix:** Direct attacks now go through the same AI as everything else.
- Added a separate "flee" health threshold, distinct from the shield-retreat threshold.
- Companions can now equip a shield while fleeing.
- A companion that gets cornered while fleeing now defends itself instead of just standing there.

## 1.4.26
- Shield choice is now handled entirely by the health/threat-aware logic, instead of sometimes being overridden by "pick whatever blocks the most."
- "Return to me" now walks back normally at short range, or does a disguised teleport-recall if you're too far away, instead of risking getting stuck.
- Companions now fall in with a nearby real player and keep fighting if their owner dies mid-session, instead of standing idle.

## 1.4.17 – 1.4.25
- Fixed shields defaulting to the tower shield far more often than intended.
- Fixed companions not properly falling back to melee when they ran out of arrows.
- Rebuilt "come to me" / return-to-owner as reliable distance tracking instead of an older timer approach that could get stuck.
- Fixed a few "return to me" bugs (not firing on companion death; wrongly pulling in companions not set to Follow).
- Tracked down and fixed several different "companion just stands there" reports.

## 1.4.13 – 1.4.16
- Added companion health/stamina status bars above their heads, then simplified them based on feedback.

## 1.4.4 – 1.4.12
- Added a "torch opener" behavior so companions can light their way in the dark.
- Fixed a weapon-swap loop, a "stuck on Passive after death" bug, a version-string bug, and a water-safety stuck state.

## 1.4.1 – 1.4.3
- Fixed a long-standing bug where a companion's inventory could show garbage tooltips instead of real items.

## 1.4.0
- Initial Valheim 1.0 compatibility pass — updated several game functions that changed in the 1.0 update so the mod loads and runs at all.
