# AI Companions

![AI Companions — AI NPC companions for Valheim](https://raw.githubusercontent.com/julianu37/AI-Companions/main/docs/images/banner.png)

An **AI NPC companion** for **Valheim 1.0**: you **speak or type** and he understands and acts — in **five
languages** (Portuguese, English, Spanish, French and Italian), following the **game's language**.

This is an **independent project** based on **[Offline Companions Reborn](https://github.com/MaddhattrRocks/Offline-Companions-Reborn)**
(by **Maddhattr**), which in turn is the Valheim 1.0 update of **ProfMags**' **Offline Companions**.

> **All credit for the concept, the design and the original code goes to ProfMags** (and, for the update, to Maddhattr).
> The companion, the jobs, the movement/combat AI, the interface and the base of all of it come from there.
> **What is ours:** the **localization in five languages** (Portuguese, English, Spanish, French and Italian) and the
> **AI layer** (LLM + voice).

> **Source code:** [julianu37/AI-Companions](https://github.com/julianu37/AI-Companions)
> — guide, settings reference and bug reports.

---

## 🇧🇷 What we added

### 🗣️ Voice and text orders (AI)

**Hold `V`**, speak and release — or type in the chat. The speech is transcribed **inside the game** and the text
goes into the same path as the typed orders. Examples:

`collect 10 wood` · `get 10 stones` · `mine iron` · `follow` · `stop` · `stop following me` · `come here` ·
`attack the greydwarf` · `kill the boar` · `equip the bronze axe` · `take the pickaxe` · `drop the stone` ·
`throw away 5 wood` · `open your inventory` · `stay on the defensive` · `good morning` · `how are you?`

- The number in the order is a **target** counted in **his inventory**: if he already has 10 wood and you ask for
  10, he says *"We already have enough."* and does not go out to gather.
- **Seeing what he carries:** `open your inventory` / `show me what you carry` opens the **companion's inventory
  window** (the same one from the interact key); `close the inventory` closes it.
- **The voice is local:** the speech recognition and the **companion's voice** run **on your machine** — none of
  it is sent out. The **LLM** (which understands the order) uses **Groq** by default, a **remote service**:
  the text of the orders and of the conversation **leaves your machine**. To send nothing, use a local
  **LM Studio** (`[AI] BaseUrl = http://127.0.0.1:1234/v1`).

### 🔊 He speaks (local synthesized voice)

The companion **says out loud** what he answers, at his position in the world. The voice is synthesized **inside
the game** by **Kokoro-82M** (through `sherpa-onnx`, CPU) — no external service. **The voice follows the game's
language**: `[AI] TtsVoice = 0` (the default) is **automatic**, and each language has its own voices.

| Language | Voices (the default first) |
|---|---|
| Portuguese (Brazil) | `pf_dora` (F), `pm_alex` (M), `pm_santa` (M) |
| English | `af_heart` (F), `af_bella` (F), `af_nicole` (F), `am_fenrir` (M), `am_michael` (M), `am_puck` (M) |
| Spanish | `ef_dora` (F), `em_alex` (M) |
| French | `ff_siwis` (F) |
| Italian | `if_sara` (F), `im_nicola` (M) |

> A language the Kokoro model does not speak (**German, Russian, Polish, Turkish, Korean, Swedish and Ukrainian**)
> uses the **English** voice. An explicit voice of another language is not used (it would sound wrong).

- **What he speaks:** the model's reply (*"Leave it to me."*) and/or the mod's line that appears in the chat
  (*"Ok: gather wood (10)."*, *"We already have enough."*) — two separate options
  (`TtsSpeakReplies` / `TtsSpeakModLines`). In an order the mod's line wins (the voice follows what is
  written on the screen); turn `TtsSpeakModLines` off to hear the model's reply. Nothing is spoken twice.
- **No game stalls:** the synthesis runs on a background thread, the speech is delivered **sentence by
  sentence** (the first one already plays while the next is generated) and repeated lines come from the
  **cache**.
- **Quick test** in the game console (`F5`): `modia_tts_teste` (or `modia_tts_teste Good morning, boss!`).

### 🧠 Personality, memory and conversation

- **Personality** through a preset (`viking`, `mago`, `sarcastico`, `guia`) or **free text** — he answers in that tone.
- **Short memory** of the latest exchanges, per companion.
- **Free conversation:** ask something and he answers as a character.

### ⚙️ Our tweaks

- **Larger gathering radius** (wood/stone/ore up to 25 m from you; drops picked up up to 12 m).
- **No random idle lines** every 15–30 s (the useful lines stay).
- **The `V` key belongs to push-to-talk** and does not trigger the game's *Toggle Auto-Pickup*.
- **Interface, menus and speech in five languages** — **Portuguese, English, Spanish, French and Italian**, all
  following the **game's language**, with **English as the native language**: a new language is **one file**, and
  the voice and the speech recognition pick the language by themselves.

---

## What this mod does

It adds **persistent NPC allies** to Valheim. A companion is offered to you the first time you enter a world.

Companions **are not pets** — they are team mates with their own inventory, equipment, food, stamina, skills and
speech. Point them at the work and they do it: they fight by your side, chop wood, mine, gather, farm, fish, hunt,
cook and prepare meads, operate your smelters, take care of your base, pull your cart, sit by the fire, sleep in
beds and follow you through portals.

Everything (appearance, inventory, equipment, job, home, skills, ownership) is **saved with the world** and
survives sessions, zone changes, server restarts and deaths.

The full guide is in `GUIDE.md` and the reference for every setting is in `CONFIGURATION.md`.

<details>
<summary><b>Commanding your companions</b></summary>

- **Tap** the interact key on a companion to open his panel: inventory, equipment, food and name.
- **Hold** the interact key to open the **command ring**: pick a job, turn *follow* on/off, set his home and toggle
  *Wander*, *Pick up* and *Command*. The inner ring has the combat stances.
- **Point and send:** aim at something and press the command key (`Z` by default). He figures it out from the
  context: enemy — attack · resource — gather · cart — pull · ship — board · bed — sleep · fire — sit ·
  door — open · chest — store the things · workbench — repair the equipment · tombstone — recover · ground — go there.
- **Hold** the command key to call the nearby companions; keep holding for a few seconds to call **everyone**, at
  any distance.
- **Your orders come first:** while he carries out an order, the job waits and resumes afterwards.
- **Set home** marks where he lives: he claims a bed nearby, brings tools there, empties his bags
  and works around it.

</details>

<details>
<summary><b>Combat</b></summary>

The companions fight with a defensive AI: they raise the shield and block, parry and counter-attack, and switch to
the best blocking shield when several enemies attack at the same time. They pick the weapon by the enemy's
**weaknesses and resistances**, not by raw damage, and they open with a torch against enemies weak to fire.

Bows and crossbows are used when there is ammunition; crossbows are loaded properly, and they go back to melee
when the enemy gets close.

They do not pick a fight with creatures that are minding their own business: by default they attack after the
creature notices you or them. Anything that attacks them is answered. Below a certain health they fight
defensively; on very low health, they run.

**Stances** (inner ring of the wheel):

| Stance | Behaviour |
|:---|:---|
| **Balanced** | Chooses melee or ranged according to the situation |
| **Passive** | Never starts a fight; only follows or works |
| **Ranged** | Prefers bow/crossbow and keeps the distance |
| **Melee** | Stays on the close-range weapons |

After the fight he goes to pick up what the kill left behind (with *Pick up* on).

</details>

<details>
<summary><b>Gathering: wood, stone, ore and foraging</b></summary>

- **Following you**, he goes after trees, rocks or plants **near you** while you walk
  (up to 25 m by default) — and follows you when there is nothing nearby.
- **Not following**, he works an **area**: around home, if the job started near it; otherwise, around
  where you left him (up to 60 m).
- **What and how much:** the lists and the amounts decide what he gathers and where he stops. Items outside the list stay on the ground.
- **Early game:** with no bronze axe and no forge, he also picks up **branches, stones and flint** from the ground.
- **Safety:** he gets out of the way of falling trees and does not fell trees glued to your buildings.
- **Bag full:** he unloads into a chest (or a cart/ship nearby).

</details>

<details>
<summary><b>Hunting and fishing</b></summary>

**Hunting**
- He goes after creatures that drop meat, until he has enough of each type. He never hunts tamed animals, bosses or
  other companions.
- He approaches skittish animals (like deer) quietly.
- He shoots birds for feathers (needs a bow/crossbow).
- He picks up what fell and takes it home when the bag fills.

**Fishing**
- He casts with a rod and bait, choosing the best bait for the fish around.
- He waits for the bite, strikes and reels watching the line.
- Each fish type has a goal — he stops when you already have enough.

</details>

<details>
<summary><b>Farming</b></summary>

- Harvests the ripe crops, row by row, and stores the harvest.
- Replants in the **farm zones** you mark with the editor (`LeftAlt` + `Z` by default). A zone can be for
  a specific crop or "any".
- Keeps a **seed reserve** — plants seeds to restock and never uses your last ones.
- Respects the protected areas (wards), like a player building.

</details>

<details>
<summary><b>Smelting and cooking</b></summary>

**Smelting**
- Keeps furnaces, smelters and kilns stocked with ore and fuel from the nearby chests (and from home).
- Collects the output and stores it.

**Cooking**
- Uses **every** type of station like a player: cauldron, preparation table, mead still, spit,
  iron station, oven, fermenting barrels and beehives.
- Cooks until each food reaches the goal and always leaves a reserve of ingredients for you.
- Prepares meads in batches, fills and empties the barrels, and organizes the cooked food next to the same kind.

</details>

<details>
<summary><b>Repairing and restocking (taking care of your base)</b></summary>

A job for the companion who stays at home (or at an outpost) — as long as you (or another player) are close
enough for the area to stay loaded:

- **Repair:** fixes damaged pieces with a hammer, one hit at a time, like a player.
- **Restock:** keeps fires, braziers, torches and hot tubs burning.
- **Beehives:** empties them and stores the honey.
- **Piles:** stacks the wood and stone left over from the chests next to the piles you already made.
- **Organize:** arranges the chests "like with like", taking each item to the chest that already has more of that item.

He fetches what he needs (hammer, fuel) from the chests and gives back what is left over.

</details>

<details>
<summary><b>When there is nothing to do</b></summary>

When the work is done and he is not following you, he stays around instead of stopping in place: he does small
tasks, sits on a chair or on the ground, takes a walk looking at the stations and chests, or comes over to talk.
He goes back to work the moment there is something to do.

</details>

<details>
<summary><b>Equipment, tools and repair</b></summary>

- Every companion has a full inventory and equips the best weapon, shield and armour he carries.
- **Tools:** the jobs take the tool they need (axe, pickaxe, cultivator, rod and bait,
  hammer…) from the chests at home and give it back afterwards.
- **Repair:** worn equipment is taken to a workbench that a player could use.
- **Bags:** when changing jobs or going back home, he empties what he gathered into the chests.

</details>

<details>
<summary><b>Food, rest, beds and death</b></summary>

- **Food:** he eats in up to three slots, like a player, using the food from his own inventory. He drinks healing
  and resistance meads when he needs them.
- **Rest:** he gets the *Rested* effect near a fire — and sits with you when you sit.
- **Beds:** each companion claims a bed, which shows his name. He goes to sleep at night (and when you sleep)
  and goes back to work in the morning.
- **Death:** he leaves a tombstone on the map until it is emptied; he comes back after a while at his bed (or by
  your side) and picks up his own equipment when he gets close to the tombstone.

</details>

<details>
<summary><b>Multiplayer</b></summary>

- **Your companion stays yours:** he treats the owner as his player — he follows you, sleeps when you sleep and
  carries out your orders, no matter which computer is running him.
- **Chests are safe to share:** he does not use an open chest, another player's private chest or a chest behind a
  protected area he cannot open. Moved items are saved correctly, with no losses and no duplication.
- **Install everywhere:** on a dedicated server, the server needs the mod too.

</details>

<details>
<summary><b>Settings</b></summary>

Everything lives in `BepInEx/config/AiCompanions.cfg` and can also be changed **in game** with the
settings panel (**`F8`** by default). Every option has a clear description.

> **Updating from 1.8.2 or older?** The configuration file changed its name **on 1.8.3** (it was
> `Maddhattr.OfflineCompanionsReborn.cfg`, which **is no longer read** and can be deleted) and the **settings go
> back to the defaults** — including the **`ApiKey`**, which has to be pasted again. The AI section became
> **`[AI]`** and the **keys are in English** (`Enabled`, `Model`, `TtsVoice`…). **Already on 1.8.3? Nothing is
> lost:** **1.9.0 does not touch the `.cfg`** — it only moves the old fixed defaults (`VoiceLanguage = pt`,
> `TtsLanguage = pt-br`, `TtsVoice = 42` and the old Portuguese prompt) to `auto` **once**, and a value you chose
> yourself is never touched. **Save, world, companions, houses and inventory are not affected** (the plugin GUID
> and the `HC_*` keys did not change).

The AI layer has its **own panel** (key **`I`** by default, changeable in `[AI] PanelKey`): the
**LLM settings** (URL, model, key and the **Test connection**), personality (preset or free text), the companion's
voice (with the **Listen** button), volume, memory, free conversation, the toggles of what is
spoken and the **push-to-talk** key (*Change* button). Everything is saved to the `.cfg` right away.
It opens without needing a companion nearby and closes with `Esc`.

In the **LLM settings** section, the **"Select your LLM provider"** dropdown (above the server URL)
**fills in the URL and the model** for **Groq**, **LM Studio** or **OpenRouter** — the key is not touched and
nothing applies before **Apply**; an Ollama/own-server URL shows up as **Custom (manual URL)**. Next to the
**API key** field, the **"Create your Free API Key HERE"** button opens the key page in the browser (it
disappears on LM Studio, which has no key). The **speech speed** stayed **only in the `F8` panel**.

The sections include **General, Controls, Follow, Movement, Combat, Food, Stamina, Rest, Death, Looting, Gear Repair**,
one per job (**Gathering, Hunting, Fishing, Farming, Smelting, Cooking, Repair and Restock**), **Idle,
Speech** and **Advanced** — plus ours in **`[AI]`** (text/voice orders, personality, memory, Whisper).

**Lists** (what to gather, cook, hunt, plant…) use the same format:
- `*` = everything
- `Name` = only that one
- `-Name` = never that one
- `Name:Number` = that one, with its own amount

When updating from an old version, the options you changed are migrated automatically — **except from 1.8.2 or
older to 1.8.3**, which swapped the file (new name, keys in English) and therefore **went back to the defaults**:
you only have to configure it again once (the table of every key is in the
**[wiki, under Configuration](https://github.com/julianu37/AI-Companions/wiki/Configuration)**). **From 1.8.3 to
1.9.0 nothing is lost**: the `.cfg` is kept and only the old fixed defaults of the voice/language become `auto`.

</details>

---

## Installation

1. Install **BepInExPack_Valheim** (BepInEx 5.4.23.5 for Valheim 1.0.x).
2. Copy the mod folder to `BepInEx/plugins/AI-Companions` (or use a mod manager).
3. **For the voice orders:** leave a Whisper model (e.g. `ggml-small-q5_1.bin`) in
   `.../AI-Companions/whisper/`.
4. **For him to speak:** the Kokoro model already ships in the package, in `.../AI-Companions/tts/kokoro/`.
5. **For the AI:** create the **free key** at **[console.groq.com/keys](https://console.groq.com/keys)** and paste it into
   `[AI] ApiKey`. The `BaseUrl` (`https://api.groq.com/openai/v1`) and the `Model` (`openai/gpt-oss-20b`) **already
   come configured**. Want to run it locally? Install **[LM Studio](https://lmstudio.ai/)**, load
   `qwen2.5-3b-instruct`, start the server and use `[AI] BaseUrl = http://127.0.0.1:1234/v1` with an **empty**
   `ApiKey`. In game, **Test connection** (`I` panel) tells you whether the URL answers; the **key** is only
   validated on a real order.

**Requirements:** Valheim **1.0.x** (tested on **1.0.20**) + BepInEx (the BepInExPack already brings Harmony). For the orders, an **LLM**:
**Groq** (only the free key and internet), a local **LM Studio** (optional) or **OpenRouter** (alternative). The
voice orders require **Windows x64** and a **microphone**.

**Multiplayer:** every player needs the mod, and so does the dedicated server.

## Future plans

- Keep improving the companion's AI and the natural-language orders.
- More voices and more languages (the five current ones are Portuguese, English, Spanish, French and Italian) and
  voice choice through the radial menu (the **AI panel**, key `I`, already lets you choose the voice and the
  personality **in game**, and the voice is **automatic** by default since 1.9.0).
- More multiplayer features.
- A review of the skills and attributes on top of a player's.

## Known problems

- **Companion types:** the **dverger** (**Mage**, **Fire Mage**, **Ice Mage** and **Support**) are **combat**
  types — the ring shows only the combat/support actions and the **work** orders (through the chat, the voice or the
  command key) are **refused** with *"I'm a fighter, I don't do that work."*. The panel hides the food slots per
  type. To work, use the **Companion** type.
- **Voice volume:** the companion's speech **does not** go through the game mixer, so it **ignores the Valheim
  volume sliders** (music/SFX). What controls its volume is `[AI] TtsVolume` (or the slider in the AI panel).
- *(Fixed in 1.6.0)* **`F8` panel:** the mouse click did not answer in one strip of the controls (there was an
  invisible click target of the game HUD **in front of** the panel — the HUD uses `sortingOrder` 400 and the mod
  panels were below it). Now **every** canvas of the mod is in the **1701+** range, above the HUD. The AI panel
  (key `I`) never had the problem.

## Source code

Full code on GitHub: **[julianu37/AI-Companions](https://github.com/julianu37/AI-Companions)**.

## License

**MIT** — see `LICENSE`. It is the same license as the original Offline Companions; we keep the copyright notice of
ProfMags (and Maddhattr) along with our changes, as the license requires.

## Disclaimer

This is an **independent version** of a community mod, maintained for personal/study use, with our
AI layer on top. It is not affiliated with or endorsed by ProfMags, Maddhattr or Iron Gate.
If any of the authors wants something removed, changed or incorporated, just ask.

Part of the code was written with the help of AI tools, with specific instruction, review and testing before
every release.
