# 2.0.0 — the companions catch up with the game (and take five new orders)

![AI Companions — AI NPC companions for Valheim](https://raw.githubusercontent.com/julianu37/AI-Companions/main/docs/images/banner.png)

> **Four upstream releases merged (1.5.5 → 1.5.8) with our AI layer, five languages and UI kept intact, plus five
> new orders you can type or speak: empty a beehive, close a door, board a ship, sit by a fire and put what they
> carry in a chest.** This is the biggest update since we forked: blocks and parries like a player, ships, beehives,
> torches, lava, real encumbrance, tilled-ground farming, Odin's Shrine and controller support — and the whole
> speech overhaul translated into the five languages.

## What changed

### From upstream (game behaviour)

- **Fighting like a player**: one roll per swing, block and parry with the Blocking skill behind it, combat tactics,
  bows, and the Passive stance still fights the creature an order names.
- **Ships and the sea**: board a ship and take the seat you are looking at; the companion stays with the ship.
- **Beehives and torches**: empty a hive and keep the honey; light and refuel torches.
- **World hazards**: lava, and the rest of what the world throws at them.
- **Real encumbrance**: in our own ceiling (`[General] MaxCarryWeight` + the belt, read from the item asset), walking
  pace, no running or jumping, stamina drain.
- **Tilled-ground farming**, plus the smaller fixes of the four releases: standing up out of sight only in dungeons,
  the door command closing doors, gear-first revive, stance keys, chests/portals/fires.
- **Odin's Shrine**: one menu to recover a lost companion or summon a new one with an offering
  (`[General] RecoverUnsummoned`).
- **Speech overhaul**: a priority queue that does not drop a problem line, per-topic dedupe, **the status line under
  the name** (saying why a job is stuck), problems announced in the top-left corner when you are far away, and new
  line pools per resource (**wood, stone, ore**), for **repair and restock** and for **idle chat** between companions.
- **Controller support**: gamepad binds, the F8 panel navigable with a controller, and `[Advanced] LogLevel`
  (`Off` / `Normal` / `Detailed`).

### Five new orders (chat or voice)

Each one reuses what the **command key** already does, so nothing is a second implementation: **`hive`** (empty the
beehive), **`door`** (close an open door, open a closed one), **`board`** (board the ship, taking the seat you point
at), **`sit`** (sit by a fire) and **`deposit`** (put what the companion carries into a chest).

- The target is the object you are **looking at** (the command key's own ray and layers) and, with nothing under the
  crosshair, the nearest one around you — 20 m for a beehive or a door, 30 m for a ship, 5 m for a fire or a chest
  (never the companion's own pack, never a tombstone, a cart or a ship's hold).
- The order reaches **one** companion (the one you named, or the nearest that can take orders) and answers with its
  own line. A locked door still answers "It's locked.", an empty beehive "No honey in there yet." and a full chest
  "There's no room in that chest.".
- **A chest inside someone else's ward is refused** — the order respects `PrivateArea`, like the rest of the mod.
- **The words you typed are read back** when the model answers something adjacent: "close the door" is a door, never
  the inventory; "put this in the chest" is a deposit, never a drop; an order said while already sitting at that fire
  does not stand the companion up.

### Our layer, kept and improved

- **The LLM layer** (text and voice orders, the intent safety nets `GatherIntent`/`PickupIntent`/`RadialIntent`, now
  plus an `InteractionIntent` for the five new actions), **TTS/STT**, the five-language localization, the panels
  following the game's language and the **1701+ UI band**.
- The prompt contract teaches the five actions in every language (+7 lines each, with the two examples that matter),
  and the action list the model may answer is the single source of the JSON schema (35 actions).
- **Everything the upstream said, in your language**: the 160 new strings of the four releases, the new speech pools
  (**20 categories**, was 15) and **991 interface keys per language**.
- Our **carry ceiling** stays in every weight check, `[Speech] Chatter` stays the on/off switch of our own speech
  settings (the upstream's four-level version does not replace it).

### Fixed in the merge (found by a seven-front audit before the release)

- **Farm zones came back empty.** The tombstone carried the reader of `HC_Respawn_FarmZones` but not the writer, so a
  companion that died returned without the zones it was planting. One line, restored from upstream.
- **The F8 panel could mute the mod.** The merge had mixed our voice dropdown builder with the upstream's generic
  list builder; any pick on `LogLevel`, `PortalItems` or `RespawnStance` was written outside the setting's own list and
  BepInEx clamped it to the **first** value (`Off`, `AllowAll`, `Passive`). The lists now come from the setting itself.
- **Choosing a stance now ends the tombstone recovery** (the auto-pickup comes back) instead of letting the companion
  walk to its tombstone against your order.
- **Depositing into a chest waits for the world's ownership** on a dedicated server, so items cannot leave the pack
  and be overwritten; the chest order also refuses chests that are not a player-placed chest.
- **The pointed target of an order scans every hit**, like the command key, instead of only the first one — a chest
  behind a fence post is no longer replaced by "the nearest chest".
- **`putting N item(s)` counts items**, not stacks.
- Smaller: a companion with no id yet no longer counts twice against `[General] MaxCompanions`; the AI profile (voice
  and personality) survives being recovered at Odin's Shrine; the log of the new patches costs nothing with the log
  off; and the `[General] MaxCompanions` description now says when a dismissed companion's slot comes free.

## How to update

1. Close the game and **unzip over the old folder** (keep `BepInEx\config\AiCompanions.cfg` — your settings, the AI
   key, the voice choices and the per-companion profiles are kept).
2. **Nothing new to download**: the models are the same ones (Whisper `ggml-small-q5_1.bin`, Kokoro `tts\kokoro\`).
3. **Playing in English?** Delete `BepInEx\plugins\AI-Companions\speech.json` once — the new speech categories
   (wood/stone/ore, repair and restock, idle chat) are generated on the next start.
4. `[Advanced] GatheringSwingInterval` is gone (the upstream removed it in 1.5.6; the swing timing now comes from the
   stamina/combo rules). The `.cfg` drops the line by itself and keeps a `.bak`.
5. Companions already in the world are untouched: same GUID, same saves, same roster.

## Credits

Built on **Offline Companions** by ProfMags and **Offline Companions Reborn** by MaddhattrRocks (MIT) — the upstream
of this fork. Voice: **Kokoro-82M** via **sherpa-onnx**; speech recognition: **Whisper.net**. See
`THIRD_PARTY_NOTICES.md`.
