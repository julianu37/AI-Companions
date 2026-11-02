# 1.9.6 – 02/11/2026

![AI Companions – AI NPC companions for Valheim](https://raw.githubusercontent.com/julianu37/AI-Companions/main/docs/images/banner.png)

> **The building update.** Your companion now builds what you ask – a workbench, a chest, a bed, a shelter, a tent or
> a whole house – in the five languages: it finds the spot with the game's own rules, pays from its bag and your
> chests, gathers what is missing, levels the ground with the hoe and comes back to finish the job by itself.

## What it can build

| you say | it raises | pieces | wood |
|---|---|---|---|
| "construa uma bancada aqui" | workbench (or chest, fire pit, bed, dragon bed) | 1 | the recipe's |
| "construa uma cabana aqui" / "um barraco" | 2x2 hut with a **door** | 7 | 16 |
| "construa um abrigo para a bancada" | bench shelter (what crafting under a roof needs) | 8 | 12 |
| "construa uma tenda aqui" | 4x2 m lean-to (two poles, two 45° pieces) | 4 | 8 |
| "construa uma casa 4x4 aqui" | small house, workbench in an **annex outside** | 21 | 51 |
| "construa uma casa 6x6 aqui" | medium house, 1 m eave on four corner posts | 46 | 101 |
| "construa uma casa aqui" | 8x8 house with door and window | 57 | 123 |
| "nivele um terreno 10x10 aqui" | leveled ground, point by point with the hoe | – | – |

All of them were built and measured in game (every piece `supported`, the door always goes in **last** so the
companion does not lock itself inside).

## How it behaves

- **It builds alone.** The piece no longer goes through the player's own placement: the creator is you, but the
  hands (and the materials, and the noise) are the companion's.
- **It starts with what it has.** A plan pays piece by piece from the bag, then from the chests it may use. When a
  piece is short it **empties the chests first**, gathers what is missing **for the rest of the whole structure**
  (or until the bag is nearly full) and resumes at the **same anchor and the same piece** – it used to start a second
  house next to the first.
- **The gather works the building site, not the player.** Wood/stone/ore normally work around you; for a build the
  companion works the area of the spot, skips the trees inside the base guard and its limit counts from the last wood
  that arrived (a test lost 36/39 to a fixed clock).
- **It levels the ground visibly.** A house on uneven ground hands the job to the same leveling the "level the
  ground" order uses: the companion walks to the middle, turns and swings the hoe, spot by spot, and comes back by
  itself when the ground is flat.
- **The spot is where you aim.** The build stays near the point you are looking at (the leveling looks 1.5 m around
  it), instead of wandering a handful of metres away.
- **It says what is missing** – before starting, when it gives up, and in the log with the piece, the pose and the
  ground it hit (`[Build] piece wood_floor refused now (no_support) at (...) yaw ... | ground=...`).

## Fixes that came out of the tests

- The build watcher compared the **bag total** with the **shortfall** and looped forever on a house's last door
  (4 wood): it now recomputes what is actually missing and resumes only when nothing is.
- The **workbench of the 6x6** sat on top of a floor tile (that floor was refused with `no_support`); it went back to
  the 8x8's corner and the 6x6's door moved to the right slot.
- The **6x6 roof** runs with the gable over the door, and its gable pieces were going in like wings – they are turned
  the right way now.
- The plan's rotation briefly snapped to 90° steps and made the house lean; it is back to the direction you are
  looking at, like the 8x8.
- The companion no longer stays in wood mode after a build ends (it kept chopping logs on the way home), and the
  encumbered state (over 300 weight) is reported instead of silently freezing the order.

## Under the hood

- New `[Building]` settings (enabled, search radius, distance from home, chests, hammer, quiet, gather radius,
  leveling) and the `build` action (`ValidActions` 36) with the piece or the structure in `target`.
- New DevBridge tools for testing: `build.check`, `build.snaps`, `build.usable`, `build.delete` (wipe a test area),
  `terrain.check`, `terrain.level`, `companions.build`/`companions.level`, `player.look`.
- The offline harness is at **405 assertions** and the MCP test at **70 checks**; the release gate runs all of it
  before the package is staged.

**Full changelog:** [package/CHANGELOG.md](https://github.com/julianu37/AI-Companions/blob/main/package/CHANGELOG.md)
· **How the building works:** [docs/re/building-notes.md](https://github.com/julianu37/AI-Companions/blob/main/docs/re/building-notes.md)
