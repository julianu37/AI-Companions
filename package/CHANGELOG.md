# Changelog

All the changes to this project, derived from ProfMags' Offline Companions. The versions before 1.4.0 are the original mod (see the original mod's history, from ProfMags, for those versions) plus the first batch of compatibility fixes for the Valheim 1.0 API that made the mod load and run. **The entries below 1.5.4 are the upstream history and stay as they are (in English) — our sections (from 1.6.0 on) are ours.**

## 1.9.4 - in progress (31/10/2026)

**One fix so far: a female companion kept the beard of the male preview.** 1.9.3 made "no beard" hash `0` (which is
what the game expects), but it also left the game's attach cache cleared to `0` - and `0` is exactly the value of "no
beard". `VisEquipment.SetBeardEquipped` returns early when the hash matches the cached one, so the beard of the
previous appearance was never destroyed.

- **The cache is cleared to `int.MinValue` now** (`CompanionSetup.ResetVisualItemCache`), a value that is never a real
  item hash: `SetBeardEquipped(0)` runs the destroy branch and then skips the attach, which is what "no beard" means.
  Both paths are covered - the real companion (`ApplyAppearance`) and the 3D preview of the editor.
- **A brand new companion was already fine** (it has no beard to destroy); the bug showed up when switching the
  preview from male to female and when editing the appearance of an existing companion.
- **Workaround on 1.9.3:** pick the `BeardNone` item in the beard list (a real hash for an invisible beard) instead of
  "None".
- Nothing else changes: same config, same saves, same models.

More fixes are planned for this version - the full list is in `docs/audit-bughunt-1.9.4.md`.

## 1.9.3 - 31/10/2026

**One fix: the companion's beard.** A companion with no beard was asking the game for a beard that does not exist.

- **"No beard" is hash `0` now.** The appearance code passed the hash of an **empty string** (371857150) instead of `0`;
  the game then tried to attach a beard prefab with that hash, logged `Missing attach item: 371857150` in the Unity log
  and the beard never appeared. The decompiled `VisEquipment.SetBeardEquipped` treats `0` as "remove/skip" — the mod now
  sends `0`, in the real companion and in the appearance preview.
- **The `[AppearanceDiag]` warning** only fires when the prefab exists and the object did not appear (it used to fire on
  every companion without a beard).
- Nothing else changes: same config, same saves, same `Companions.dll`, same models.

## 1.9.2 — 31/10/2026

**Upstream sync: 1.5.4 → 1.5.8.** Four upstream releases are in — combat, ships, the Shrine, torches, controller support —
with our AI layer, localization and UI band kept.

### From upstream (game behaviour)

- **Blocks and parries like a player** (one roll per swing, better with the Blocking skill), combat tactics and bows.
- **Ships**: board a ship and take the seat you point at; **beehives** (empty them and keep the honey); **torches**
  (light and refuel); **lava** and the other world hazards; **real encumbrance** (walking pace, no running or jumping,
  stamina drain) and **tilled-ground farming**.
- **Odin's Shrine**: one menu to recover a lost companion or summon a new one with an offering (and
  `[General] RecoverUnsummoned`).
- **Speech overhaul**: a priority queue that does not drop a problem line, per-topic dedupe, the **status line** under
  the name (with the reason when a job is stuck) and problems announced in the top-left corner when you are far away.
- **Controller support**: gamepad binds, F8 navigable with a controller, and `[Advanced] LogLevel`
  (`Off`/`Normal`/`Detailed`).
- **Smaller fixes**: standing up out of sight only in dungeons, the door command closing doors, gear-first revive,
  stance keys, chests/portals/fires improvements.

### Ours (unchanged behaviour)

- The **LLM layer** (text and voice orders, intent safety nets), **TTS/STT**, the five-language localization, the panels
  following the game's language and the **1701+ UI band**.
- Our **carry ceiling** (`CompanionCarry`: `[General] MaxCarryWeight` + the belt bonus read from the item asset) stays
  in **every** weight check; the **Passive stance still fights the creature an order named**.
- **160 new strings translated** into the five languages — **991 interface keys per language** in the end — and the
  upstream's speech overhaul translated with it (**20 categories**, was 15).

### Fixed during the merge

- Four settings were declared without a `Bind` and would not appear in the `.cfg`: `[General] ShowStatus` and
  `[Speech] Enabled`, `FarAwayMessages`, `AnnounceDeath`.
- The F8 panel now clears the new per-row action dictionaries when it rebuilds (no stale rows).

### New orders

Five orders that reuse what the **command key** already does, typed in the chat or **spoken** (the same path, with
personality and memory): **`hive`** (empty the beehive), **`door`** (close an open door / open a closed one),
**`board`** (board the ship, taking the seat you point at), **`sit`** (sit by a fire) and **`deposit`** (put what the
companion carries into a chest).

- The target is the object you are **looking at** (the command key's own ray and layers) and, with nothing under the
  crosshair, the nearest one around you — 20 m for a beehive or a door, 30 m for a ship, 5 m for a fire or a chest
  (never the companion's own pack).
- The order reaches **one** companion (the one you named, or the nearest that can take orders) and answers with its
  own line: "Closing the door.", "On board!", "Putting 12 item(s) in the chest.". A locked door still answers
  "It's locked.", an empty beehive "No honey in there yet." and a full chest "There's no room in that chest.".
- 17 new strings in the five languages, and the prompt contract teaches the five actions in every language (+7 lines
  each, no extra examples beyond the two that matter: "close the door" and "put this in the chest"). A small model
  answering `inventory_close` for "close the door" (the verb "close" is an alias of that action) is read back as the
  door order: the words you typed are checked for a door word and the absence of a pack word, the same idea as the
  rule that reads the pack from the action word.

### Fixed in the merge (found by the 1.9.2 audit)

Seven independent reviews of the whole branch (AI layer, config/UI/localization, netcode/ZDO, Harmony, the prompt
contract, "what of the upstream did not make it" and a global pass over this document) before the release. What they
found and is fixed here:

- **The farm zones came back empty.** The tombstone carried the **reader** of `HC_Respawn_FarmZones` but not the
  **writer**, so a companion that died returned without the zones it was planting (the Shrine path kept them). One
  line, restored from upstream.
- **The F8 could mute the mod.** The merge mixed our voice dropdown (M2) with the upstream's generic list builder: any
  pick on `[Advanced] LogLevel`, `[General] PortalItems` or `[Death] RespawnStance` was written outside the setting's
  own list and BepInEx clamped it to the **first** value (`Off`, `AllowAll`, `Passive`). The lists now come from the
  setting itself, with no "automatic" option, and `A` on the controller opens them.
- **Choosing a stance now ends the tombstone recovery** (`MHR_Recover.Cancel` had no caller): the auto-pickup comes
  back and the companion stops walking to its tombstone against your order.
- **The chest order respects the ward and the world.** `deposit` only accepts a chest a player placed (never a
  tombstone, a cart or a ship's hold) and refuses one inside another player's `PrivateArea`; it also waits for the
  world's ownership before writing, so an item cannot leave the pack and be overwritten on a dedicated server.
- **The pointed target scans every hit**, like the command key, instead of only the first one — a chest behind a fence
  post is no longer silently replaced by "the nearest chest".
- **`senta` twice**: already sitting at that fire by an order, the companion no longer stands up and answers "nobody
  came"; sitting at another fire now takes it there.
- **`putting N item(s)` counts items**, not stacks.
- **`[Advanced] GatheringSwingInterval` is gone** (the upstream removed it in 1.5.6 and nothing read it here).
- **`[General] MaxCompanions`**: the description now says when a dismissed companion's slot comes free (on the next
  load of the world, or right away with `RecoverUnsummoned` off), and a companion with no permanent id yet no longer
  counts twice on a dedicated server.
- **The AI profile survives Odin's Shrine**: the roster copy now carries the voice and the personality, like the
  tombstone already did.
- **The new patches cost nothing with the log off** (`[Piece]` wear, block log, reflection behind the log gate) and an
  instance id recycled by Unity no longer hands a new character the encumbrance verdict of a dead companion.
- **Speech pools**: the five categories of the upstream's speech overhaul are translated in the four languages
  (`Wood`, `Stone`, `Ore`, `Upkeep`, `Chat`), and `SpeechConfig.Merge` fills any pool a hand-edited file omits.
- **75 F8 labels and 2 tab names** that were still in English are translated; **991 keys per language**.

### Fixed after the first test of the 1.9.2 build (the voice)

- **The voice follows the game language, including when you change it with the world open.** The Kokoro model is one
  file for every language, but it picks the **phonemizer when it is loaded** — and it was loaded once and kept, so
  switching the game to Portuguese kept `pf_dora` reading Portuguese with English phonemes (the panel and the log both
  said `pt-br`, and the audio was English-sounding). The model is now rebuilt on the synthesis thread, with the audio
  cache dropped, whenever the effective language changes — by the game's language or by `[AI] TtsLanguage`.
- **`[AI] TtsLanguage` offers only the five languages the mod speaks** (`pt-br`, `en-us`, `es`, `fr`, `it`), each one
  synthesized out of the game with the shipped model. `en-gb` and `fr-fr` load but fail on the first synthesis (a
  `NullReferenceException` from the binding), and `ja`/`zh`/`hi` have no interface or lines here. A hand-edited code
  outside the five **keeps the working model** instead of leaving the companion mute.

## 1.9.1 — 26/10/2026

**A ~54 MB lighter package.** The `ggml-base-q5_1.bin` model (57 MB) is **no longer shipped**: the mod never used it
(the `[AI] SttModel` default is `ggml-small-q5_1.bin`, 181 MB, and it is much more accurate in every language), so it
only made the download bigger — the full zip goes from **~368 MB to ~314 MB**. The no-models package is the same
(~28.5 MB).

- **The `base` model is still supported:** download `ggml-base-q5_1.bin` into `whisper\` and point `[AI] SttModel`
  at it if you want the faster, less accurate one (638 ms per 2 s clip against 2187 ms of the small model, on a
  16-core CPU with 8 threads).
- The `[AI] SttModel` description now says the `base` is an **optional download** instead of "it stays on disk".
- **No behaviour change and no new config key:** saves, worlds, companions, houses and the `.cfg` are untouched.

## 1.9.0 — 25/10/2026

**Five languages, and the mod follows the game.** Portuguese (Brazil), English, Spanish, French and Italian: the
interface, the companion's speech, the personality presets and the LLM contract all follow the **game's language**,
and the voice (TTS) and the speech recognition (STT) pick the language and the voice by themselves.

### What the player sees

- **`[AI] VoiceLanguage`, `[AI] TtsLanguage` and `[AI] TtsVoice` are automatic by default** (`auto` / `auto` / `0`):
  the mod resolves them from the game's language (pt-BR → `pf_dora`, English → `af_heart`, Spanish → `ef_dora`,
  French → `ff_siwis`, Italian → `if_sara`). An **explicit value still wins**, and the F8 and the AI panel show the
  **effective** value (`Automatic (en-US · af_heart)`).
- **`[AI] VoiceInitialPrompt` is automatic too**: each language has its own order vocabulary (better recognition).
- **A companion whose stored voice belongs to another language** speaks with the default voice of the active
  language (the choice is kept in the ZDO, so switching the game back restores it).
- **Content in the five languages**: interface (737 keys), companion speech (186 lines), personality presets and the
  LLM contract (rules and examples translated; the action tokens are untouched).
- **The F8 lists the voices by name** (`pf_dora (feminine)`) instead of a number; the AI panel labels show the voice
  in use and warn when a companion's voice is from another language.
- **Changing the game's language no longer needs a restart**: the panels rebuild on the next open, and the HUD, the
  speech and the TTS/STT follow along.
- **Italian is new** (`if_sara` / `im_nicola`), with UI, speech, presets and contract of its own.

### Under the hood

- `CompanionLocale` (new) is the single table: game language → Whisper code, Kokoro code, voice list and default.
- The old defaults (`pt`, `pt-br`, `42`, the old PT prompt) are migrated **once** to `auto`. In Portuguese the voice
  is the same (`pf_dora`), so nothing changes for a pt-BR player.
- `RadialIntent` (new): a keyword safety net for the actions small models confuse (stances, wander/stop wandering,
  accept/ignore commands, smelt/cook/hunt). Measured with `qwen2.5-3b-instruct`: **30/30 actions in all five
  languages** (150 cases).
- `GatherIntent` (new): the same idea for gather orders that the model answers as `pickup`/`none` — the resource and
  the amount are read from the player's words.
- `SpeechConfig` retries the language file after 30 s instead of staying in English until a restart.
- **Docs and wiki are written in English from now on**; the wiki keeps Portuguese mirrors (`-pt`).

### Known limitations

- **Audio language coverage**: Kokoro has no voice for German, Russian, Polish, Turkish, Korean, Swedish and
  Ukrainian — those languages use the English voice (the interface and the speech can still be translated).
- The **voice does not go through the game's mixer** (it ignores the Valheim volume sliders; `[AI] TtsVolume` rules).
- The **dverger types** (Mage, Fire Mage, Frost Mage and Support) are combat-only by design.

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

## 1.5.8
- Torches: companions carry one at night and in dark dungeons and caves (Torch > DarkLevel), not in a lit base or next to you holding yours; held in either hand and kept lit in a fight in the off hand; it burns down like yours (Torch > BurnsDown) and is never repaired. Companions fight with their fists rather than a torch.
- Controller binds for every action, each with an optional modifier (default: hold block + Y to command, hold block + X for the wheel); any bind can be cleared, keyboard keys too; hints show your controller's buttons.
- Settings panel (F8) with a controller: sticks the right way round, one clear highlight, readable button hints; settings with a list of choices (LogLevel, PortalItems, RespawnStance, Speech Chatter) are shown there now.
- Ashlands lava: companions go round it, get off it at once, and don't chase enemies or loot onto it.
- A companion stays put a little longer while your crosshair is on it, so you can walk up and talk to it.
- Companions no longer shut a door behind you mid-fight and can follow you through it.
- A torch used up by blocking or swinging it is gone, as for you; companions don't block with only a torch (they step out of the swing).
- In a dungeon a seated companion gets up when you go out of sight, not just when you're more than 10 m away.
- The door command closes an open door too (one companion goes); a controller's Use no longer opens a companion's bag while a menu is open.
- New Advanced > LogLevel (Off / Normal / Detailed): far less logging by default, and a level that's off costs nothing.

## 1.5.7
- The beehive order goes to the nearest companion that isn't resting; companions don't go to bed in the middle of an order.
- Companions step round a tree or rock that is in the way of their swing.
- Companions get encumbered like a player when they carry too much; a strength belt raises their limit.
- Portals follow the game's item rules for companions (General > PortalItems); ore they carry is left in a nearby chest, or they wait.
- Fixed the summon window not taking clicks for some players.
- Stone and Ore no longer mine walls or other built pieces.
- Wood takes fallen logs first, then stumps, then standing trees, finishes the log it split before starting another, and chops a log where it can reach it with nothing in the way.
- Wood: chops a log from one spot instead of moving along it, and only steps aside when a tree comes down, not when a log breaks.
- Gathering: a companion whose swings aren't damaging what it's working on (a rock in the way) tries another side, then moves on.
- Gathering: companions stay where their swings land until the target breaks, wait for a fresh log to stop rolling, and keep track of every piece of a tree they cut.
- Wood: a log that keeps rolling or is stuck up in the branches is left for a moment instead of chased.
- Companions jump out of the water at a bank like a player instead of getting stuck.
- Wood: fixed standing under a log caught in a tree, and picking up and dropping the same log when you walk away.
- Companions keep clear of the hearth when walking round it, even in a tight room.
- Companions no longer store things in or take things from world-generated chests.
- Repairs: companions without a home repair worn gear in a base like at home, and an item they couldn't fix out in the wild is looked at again once they're back.

## 1.5.6
- Combat: full combos, parries and blocks by chance (better with skill), spacing and going in on an opening, flanking, finishing blows, stamina kept for blocking.
- Bows drawn like a player (draw time, partial draws); Ranged stance carries the bow; Balanced takes the bow for enemies melee can't reach.
- Fights: only enemies in sight, no doors opened mid-fight, defensive in dungeons, no swimming after enemies, back to you past 35 m (Combat LeashFromYou), unreachable enemies let go and picked up again once reachable.
- Fixed ticks: a latched tick is fought first with full combos.
- Safer movement: spike walls avoided, fight moves stay off ledges, water and fire; follows you through dungeon doors even mid-fight.
- Fixed getting on and off boats: jumps aboard, or swims and wades to the ladder; walks to a seat (or the one you point at); gets off with you. No teleporting.
- Swimming uses stamina like the player (Stamina SwimDrainAtSkill0).
- Odin's Shrine: summon more companions for coins; recover lost or unsummoned companions; menus in the game's look.
- Collects honey from a beehive via command.
- Fixed the overhead stamina bar turning grey after a portal.
- Fixed a companion warping to you when you got off a ship.
- Companions eat again with 3 minutes of food left (Food > EatAgainMinutes); follow you indoors to a chair up to 8 m away.

## 1.5.5
Speech overhaul: companions talk less, and what they say and show tells you what they're doing and why not.
- Status: look at a companion and a line under its name says what it's doing ("Wood - chopping Beech", "Repair and Restock - fuelling the Hearth", "Mead - filling a fermenter") and, when its job is stuck, why (General > ShowStatus).
- Status: a stuck job goes through its reasons in turn, with the numbers from your settings - "Wood 512/500 - at the limit.", "Copper deposit needs a better pickaxe.", "Raw meat 104/100 - enough.", "No bait for Pike (Fishing bait).", "No wood for the Charcoal kiln.", "Mead - Honey 0/30 for Frost resistance mead." (the recipe's 10 plus the 20 kept back), "No Turnip seeds to plant.", "No Resin for the fires."
- Speech: much less small talk - every 1.5-3 minutes instead of 15-30 seconds (Speech > Chatter: Off, Rare, Normal, Often; Speech > Enabled turns all speech off).
- Speech: small talk fits what it's doing and where it is - job lines only while working that job, fight lines only in a fight, home lines at home, travelling lines away from home, lines for the biome, night, rain and cold, nothing while it sleeps, sits, swims or sails.
- Speech: it says what it's off to do ("Fetching an axe.", "Putting a cultivator back.", "Off to bed.") and gives the real reason when it has nothing to do.
- Speech: new lines for things it used to give up on silently - no way to reach you, chests full, a tool it needs (any tool), no station to fix its gear, a lost spear, prey that got away, no hunting in the Passive stance.
- Speech: replies to your orders come at once; problems aren't lost to small talk and aren't repeated for 5 minutes; hungry, worn-gear, no-bed and heavy-pack remarks at most every 10 minutes, and "no bed" only at night at home.
- Speech: a problem a companion runs into while you're out of earshot shows top left with its name (Speech > FarAwayMessages).
- Messages: when one of your companions dies, the middle of the screen says so, with the biome and how far away (Speech > AnnounceDeath); when no companion takes your order (Command off, too far) a short message top left says so.
- speech.json has a new layout with tags per line ([home], [night], [rain], [blackforest]...). Your old file is kept next to it (speech.v1.json / speech.v2.json) and replaced by the new lines.
- Death (GitHub issue #17): a revived companion gets its gear before fighting again. Death > RespawnStance = RecoverFirst (default): it comes back Passive with Pickup off, goes for its tombstone when it's safe (waiting by you while enemies are after it, but finishing the trip once it's nearer its gear than you), fights back only when attacked with a weapon in hand, puts its gear on, eats, then goes back to its old stance. Passive: stays Passive. Keep: as before.
- Stance keys (issue #17): Controls > PassiveKey, BalancedKey, RangedKey, MeleeKey (unbound by default) - the companion you're looking at, or everyone in RecallRange with Command on.
- Death (issue #16): a companion that comes back by itself says where ("Saber is back - at its bed (Meadows, 850 m from you)"), and the roster and Odin's Shrine show where each living companion is. Death > RespawnAtPlayer: it always comes back next to you.
- Fixed: a companion's farm zones were lost when it died and came back, or was recovered. They now come back with it.
- Farming: ground you've tilled with a cultivator counts as an "Any" zone - it plants the PlantAny crops there, never inside a zone you set (Farming > PlantTilledGround).
- Farming: no more planting under eaves or against walls (where crops never grow), and no more trying to reach tilled ground it can't walk to (Farming > UnreachableSpotMemory).
- Farming without zones no longer keeps announcing "no farm zones"; "All done farming" at most every 10 minutes.
- Fixed: companions walked through the edge of the hearth's flames and stepped up onto it - which damages the hearth, as it does when a player stands in it. Routes, idle spots and follow spots now keep clear of fires.
- Water: in shallow water (the swamp) companions wade instead of hopping in and out; they jump only when the jump lands them on dry land, or to save a swim.
- Fixed: shooting at a bird, a companion often hit the hillside - it now aims at the bird and only shoots with a clear line to it.
- Following indoors: no more stuttering out of the door and back; a companion that sat down stays seated while you move about within 10 m (Follow > SitStayRange).
- Fishing: the log names the stamina food it takes along.
- Fixed: key settings in the F8 panel can be set again (a click starts "Press a key...", Esc cancels).

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

## Older versions

The full history, including 1.4.0 – 1.4.156 and every detail of each version, is in [CHANGELOG-full.md](https://github.com/MaddhattrRocks/Offline-Companions-Reborn/blob/main/CHANGELOG-full.md) on GitHub.
