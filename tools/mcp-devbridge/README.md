# MCP tools (DevBridge)

Every tool returns `content: [{type:"text", text:"<pretty JSON>"}]`, except `screenshot`, which returns an **image**
content item plus the file path. A failed call returns `isError: true` with the reason as text.

## Generic

### `devbridge_ping`

No parameters. Confirms the in-game half is alive.

```json
{ "bridge": "DevBridge", "version": "1.0.0", "inGame": true, "world": "MeuMundo", "mod": "1.9.4" }
```

### `game_status`

No parameters. `inGame: false` when the world is not loaded (main menu).

```json
{ "inGame": true, "player": "Tester", "x": 120.5, "y": 30.2, "z": -44.1,
  "hp": 87.0, "maxHp": 100.0, "stamina": 120.0, "biome": "Meadows",
  "world": "MeuMundo", "day": 12, "dayFraction": 0.43, "time": 1234.5 }
```

### `game_console`

| param | type | default |
|---|---|---|
| `command` | string | - (required) |
| `cheats` | boolean | `true` |

```json
{"command": "spawn PickaxeBlackMetal 5", "ok": true, "cheats": true, "output": [], "prefab": "PickaxeBlackMetal", "prefabFound": true}
{"command": "tod 0.5", "ok": true, "cheats": true, "output": ["Setting time of day:0,5"]}
{"command": "naoexiste", "ok": false, "cheats": true, "output": ["'naoexiste' is not a recognized command. Type 'help' to see a list of valid commands."]}
```

Runs the command exactly like typing it in the in-game console (F5) and returns **what it printed** - the bridge reads
`Terminal.m_chatBuffer` before and after and returns only the new lines.

- **Dev commands are enabled first** when needed: `spawn`/`tod`/`god` are cheats and the game refuses them silently
  without `devcommands`. The command is a **toggle**, so the bridge only sends it when `Console.m_cheat` is false.
- **`spawn` reports whether the prefab exists** (`prefabFound`): `AxeBlackmetal` does **not** exist, the right name is
  **`AxeBlackMetal`** (case-sensitive). Use `world_prefabs` to find names.
- `spawn` drops the item at the player's feet; `player_give` puts it in the inventory. `spawn` also takes the game's
  extras: `spawn <prefab> [amount] [level] [radius] [p/e/i]` (`p` tries to pick up, `e` equips).
- `ok: false` means the console answered "not a recognized command" or "not valid in the current context".

### `world_prefabs`

| param | type | default |
|---|---|---|
| `filter` | string (case-insensitive substring) | `""` |
| `limit` | integer (1..500) | 60 |

```json
{"filter": "pickaxe", "count": 9, "names": ["dvergrprops_pickaxe", "PickaxeAntler", "PickaxeBlackMetal", "PickaxeBronze", "PickaxeIron", "PickaxeStone", "sfx_pickaxe_hit", "sfx_pickaxe_hit_vibration_only", "sfx_pickaxe_swing"]}
```

The prefab names the world knows - the exact spelling for `spawn` and `world_spawn`.

### `build_inspect`

| param | type | default |
|---|---|---|
| `prefab` | string | - |

```json
{"prefab": "piece_workbench", "found": true, "name": "Bancada", "rawName": "$piece_workbench",
 "inHammer": true, "recipeKnown": true,
 "resources": [{"item": "Wood", "name": "Madeira", "amount": 10}],
 "flags": {"noInWater": true, "notOnTiltingSurface": true, "noClipping": true, "groundOnly": false},
 "space": {"spaceRequirement": 0, "blockRadius": 0, "blockingPieces": [], "onlyInBiome": "None"},
 "wearNTear": {"supports": false, "material": "Wood", "health": 200},
 "craftingStation": {"rawName": "$piece_workbench", "rangeBuild": 20, "requireRoof": true}}
```

The real sheet of a buildable piece, read from the live prefab: recipe, station, **every** placement flag of
`Piece`, the space/connection rules, `WearNTear`, the private-area radius, whether it is in the player's hammer
table and whether the player knows the recipe. The flags are not published anywhere (they live in the asset
bundle), so this is how the requirements are known instead of guessed. Phase E0 of `docs/plan-construcao.md`;
the measured pieces are in `docs/re/building-notes.md`.

### `build_sites`

| param | type | default |
|---|---|---|
| `prefab` | string | - |
| `x`, `y`, `z` | number | the player's position |
| `radius` | number (0.5..40) | 4 |
| `rings` | integer (1..8) | 2 |
| `dirs` | integer (1..24) | 8 |

```json
{"prefab": "piece_workbench", "total": 16, "valid": 11,
 "points": [{"ring": 1, "dir": 0, "x": -278.2, "y": 81.2, "z": -271.5, "ok": true, "status": "valid", "reasons": [],
             "ground": {"name": "Terrain", "isHeightmap": true, "normalY": 0.99, "drop": 0.0}}]}
```

Tests candidate spots **without placing anything**: rings of points around the player (or around `x/y/z`), each
with a stable status name - `valid`, `no_ground`, `in_water`, `no_water`, `need_cultivated`, `need_dirt`,
`not_on_wood`, `too_steep`, `not_ceiling`, `not_on_floor`, `no_teleport_area`, `not_in_dungeon`, `deep_snow`,
`no_snow`, `wrong_biome`, `no_build_zone`, `ward`, `needs_station`, `too_close_piece`, `blocked_by_player`,
`no_support` - plus the ground facts (heightmap, normal, drop, biome, station in range, ward, no-build zone).
It is the checks of `Player.UpdatePlacementGhost` that do not need the ghost; the ghost-clipping test
(`m_noClipping`) comes in E2.

### `build_pieces`

| param | type | default |
|---|---|---|
| `x`, `y`, `z` | number | the player's position |
| `radius` | number (0.5..100) | 10 |
| `filter` | string (substring of the prefab or piece name) | `""` |

```json
{"count": 1, "pieces": [{"prefab": "piece_workbench(Clone)", "name": "$piece_workbench", "creator": 1738272368,
                          "mine": true, "distance": 1.5, "zdo": true, "health": 1.0, "station": "$piece_workbench"}]}
```

The pieces **already standing** near a point: prefab, name, creator, whether the creator is the local player,
distance, position, valid ZDO, health (0..1) and the crafting station it carries. It is how a placement - or a
relog - is proven from the outside, and how the companion's builds are checked.

### `player_give`

| param | type | default |
|---|---|---|
| `item` | string (prefab name) | â€” |
| `amount` | integer | 1 |

```json
{"item": "Coins", "amount": 2000, "added": true}
```

### `player_teleport`

| param | type |
|---|---|
| `x`, `y`, `z` | number |

```json
{"x": 0.0, "y": 30.0, "z": 0.0}
```

### `player_kill`

No parameters. Applies lethal damage on the main thread. It refuses while the player is in **god mode**, ghost mode
or debug flight (turn those off with `game_console` first).

```json
{"killed": true, "hpBefore": 87.0, "damage": 1000000.0, "hpAfter": 0.0}
```

### `player_inventory`

| param | type | default |
|---|---|---|
| `item` | string (prefab name, optional) | â€” |
| `set` | integer â‰¥ 0 (optional; requires `item`) | â€” |

Reads the pack. With `item`, returns `count`. With `set`, **removes every stack of that item and adds exactly that
many** (only that item is touched â€” useful to put the exact coin amount a test needs).

```json
{ "item": "Coins", "count": 2000, "totalStacks": 8, "weight": 120.3, "maxWeight": 300.0,
  "stacks": [ { "prefab": "Coins", "name": "$item_coins", "amount": 2000, "quality": 1, "equipped": false, "grid": "0,0" } ] }
```

### `world_spawn`

| param | type | default |
|---|---|---|
| `prefab` | string | â€” |
| `count` | integer (1â€“20) | 1 |
| `x`, `y`, `z` | number | player + 3 m forward |

```json
{"prefab": "Troll", "count": 2}
```

### `world_time`

| param | type | meaning |
|---|---|---|
| `day` | integer â‰¥ 0 | 0-based day (`EnvMan.GetDay`; day 1 of the HUD is `0` here) |
| `time` | number | seconds into the day (the real length is `dayLength`, 1800 s in this build) |
| `fraction` | number 0â€“1 | within-day fraction (`0.5` = midday) |
| `skip` | number | relative change, in game seconds (negative rewinds) |

No parameters just reads the clock. `time` and `fraction` are alternatives; `skip` wins over `day`/`time`.
Single-player host only (it writes `ZNet.SetNetTime`).

```json
{ "before": 1234.5, "time": 3600.0, "day": 3, "fraction": 0.5, "dayLength": 1200 }
```

### `world_aim`

Raycasts the crosshair and reports what is under it. Useful to check the aim before attacking.

| param | type | default |
|---|---|---|
| `max` | number (1..200) | 100 |

```json
{"hit": true, "name": "viewblock", "distance": 5.17, "x": -63.5, "y": 82.75, "z": 55.95, "tree": true, "creature": null}
```

### `world_nearest`

The nearest **tree** or **creature** around the player - the target to aim at (pair with `game_status` `camYaw` and
`input_mouse_move`).

| param | type | default |
|---|---|---|
| `kind` | `tree` / `creature` | `tree` |
| `max` | number (1..300) | 100 |

```json
{"found": true, "kind": "tree", "name": "Beech1(Clone)", "distance": 8.37, "x": -63.39, "y": 82.1, "z": 55.57}
```

### `world_tombstones`

The tombstones loaded around the player, with what decides the vanilla despawn: **item count**, **ZDO owner**
(`isOwner`), whether the **container is in use** (`inUse`), plus `tombstoneId`, `respawnPrefab` and the **contents**
(prefab/amount/grid). No parameters.

```json
{"count": 1, "tombstones": [{"name": "Companheiro 2", "x": 4.93, "y": 82.12, "z": 2.82, "distance": 2.0, "items": 1, "inUse": false, "isOwner": true, "owner": 1247168093, "tombstoneId": 639290843717192268, "respawnPrefab": "", "contents": [{"prefab": "Stone", "amount": 10, "grid": "0,3"}]}]}
```

### `world_tombstone_take`

Loots the **nearest tombstone with items** through the game's own entry point: `Container.TakeAll` first (what the game
calls when the grave fits in the inventory - and it does **not** set `m_inUse`), then `TombStone.Interact` (the E key)
if nothing moved. No parameters.

### `world_tombstone_destroy`

Destroys the companion tombstones around the player (dev cleanup for tests).

| param | type | default |
|---|---|---|
| `includePlayer` | boolean (also the player's own tombstones) | `false` |

### `world_tombstone_diag`

Diagnostics for the companion tombstone despawn: component `enabled`, `activeInHierarchy`, `IsInvoking("UpdateDespawn")`,
`nviewValid`, `isOwner`, `inUse`, `items` and, with `tick`, a forced call to the game's private `UpdateDespawn`.

| param | type | default |
|---|---|---|
| `tick` | boolean | `false` |

### `world_setup`

The dev setup for driving the player, in one call: `devcommands` (only when it is still off - the command is a
**toggle**), `tod <time>` (0.5 = midday) and `god on` + `heal`.

| param | type | default |
|---|---|---|
| `time` | number 0..0.999 (day fraction) | 0.5 |
| `god` | boolean | `true` |

```json
{"cheats": true, "god": true, "hp": 25, "time": 0.5}
```

**Valheim 1.0's `god` does not cancel damage**: it clamps a killing blow to **1 HP** (`Character.cs`). The player does
not die, but the bar keeps dropping - that is why the setup runs `heal` too. `ghost on` (a game command, not enabled
here) is what makes enemies ignore the player.

### `screenshot`

| param | type |
|---|---|
| `path` | string (optional `.png`) |

Returns the PNG as an image plus the path:

```json
[{"type":"image","mimeType":"image/png","data":"<base64>"}, {"type":"text","text":"C:\\â€¦\\BepInEx\\DevBridge\\shot-â€¦.png"}]
```

### `render_view`

An **independent off-screen camera**: it renders the world from any point/angle and never touches the player's view
(the UI is not in the picture - it is a Screen Space overlay).

| param | type | default |
|---|---|---|
| `x`, `z` | number (the point to look at) | required |
| `y` | number | the player's height |
| `yaw` | number (degrees, orbit) | 0 |
| `pitch` | number (degrees, -85..85) | 15 |
| `dist` | number (meters) | 8 |
| `size` | integer (128..2048) | 768 |

```json
{"x": -58.0, "z": 63.0, "yaw": 45, "pitch": 20, "dist": 6}
```

Returns the PNG inline (same shape as `screenshot`). The bridge command is `render.view` and answers `path`, `size`,
`bytes`, `target` and `camera` (the position it used).

### `input_focus` / `input_key` / `input_key_down` / `input_key_up` / `input_text` / `input_click` / `input_mouse_down` / `input_mouse_up` / `input_mouse_move`

Keyboard/mouse injection. Every call focuses the Valheim window first (best effort: the synthetic events go straight
into the Unity **Input System**, which the game reads, so they work even when Windows refuses the focus). The legacy
`SendInput` is sent too - that is the path the mod's own UI uses.

| tool | params |
|---|---|
| `input_focus` | — |
| `input_key` | `key` (KeyCode name: `Escape`, `F8`, `Return`, `W`, `LeftShift`, `Alpha1`) |
| `input_key_down` / `input_key_up` | `key` (holds: walk with `W`, run with `LeftShift` + `W`) |
| `input_text` | `text` (Unicode: accents work) |
| `input_click` | `x`, `y` (game screen pixels - the same coordinates as `screenshot`) |
| `input_mouse_down` / `input_mouse_up` | `x`, `y` (attack: press/release in separate frames) |
| `input_mouse_move` | `dx`, `dy` (camera turn; measured **0.05 deg/px**) |

### `log_tail`
| param | type | default |
|---|---|---|
| `lines` | integer (1â€“2000) | 80 |

Works with the game closed (it reads the file). The reply includes `cursor`, the byte offset at the end of the file.
Use it before an action and pass it to `log_watch` as `since`.

### `log_watch`

| param | type | default |
|---|---|---|
| `pattern` | string | â€” |
| `regex` | boolean | false |
| `timeout_ms` | integer (100â€“60000) | 15000 |
| `since` | integer (byte cursor) | current end |
| `case_sensitive` | boolean | false |

Waits for the **first** log line matching `pattern`, then returns it. Without `since` it only sees lines written
**after** the call; with `since` (from `log_tail` or an earlier `log_watch`) it reads everything after that byte
offset. The returned `cursor` is the offset right after the matched line, so the next call still sees the lines that
were already buffered after it. A timeout is a normal result (`matched: false`), not an `isError`.

```json
{ "matched": true, "pattern": "Recovery over", "count": 1,
  "lines": ["[Info   :AI Companions] [Recover] Recovery over - auto-pickup on"], "cursor": 91234, "elapsed_ms": 812 }
```

## Companions (AI Companions mod)

### `companions_list`

No parameters.

```json
{ "count": 1, "companions": [ { "name": "Maria", "prefab": "HC_Companion(Clone)", "hp": 120.0,
  "maxHp": 120.0, "dead": false, "distanceToPlayer": 3.2, "id": "â€¦", "mode": 1, "stance": 3,
  "autoPickup": true, "recovering": false, "owner": "Tester", "x": 120.5, "y": 30.2, "z": -44.1 } ],
  "tombstones": [ { "name": "Maria", "items": 14, "id": 639â€¦, "x": 120.5, "y": 30.2, "z": -44.1 } ] }
```

`tombstones[]` are the tombstones owned by the local player. The dead companion object is destroyed by the game;
`companions_resurrect` works from its tombstone.

### `companions_spawn`

No parameters. Calls `CompanionManager.SpawnFree` â€” the AI panel's spawn, **no coins**, default look and tier.

```json
{"spawned": true}
```

### `companions_order`

| param | type | required |
|---|---|---|
| `action` | string | yes |
| `name` | string (contains, case-insensitive) | no |

Actions: `follow`, `wood`, `stone`, `ore`, `forage`, `smelt`, `hunt`, `farm`, `fish`, `repair`, `restock`, `cook`,
`stay`, `stance_balanced`, `stance_passive`, `stance_ranged`, `stance_melee`, `autopickup_on`, `autopickup_off`.

```json
{"action": "wood", "mode": 1, "applied": true}
```

Without `name` the **first** companion is used â€” pass the name when there is more than one.

### `companions_teleport`

| param | type |
|---|---|
| `name` | string (optional) |
| `x`, `y`, `z` | number (optional; default: player + 2 m forward) |

```json
{"name": "Maria", "x": 120.0, "y": 30.0, "z": -44.0}
```

### `companions_give`

| param | type | default |
|---|---|---|
| `item` | string | â€” |
| `amount` | integer | 1 |
| `name` | string (optional) | â€” |

### `companions_zdo`

| param | type |
|---|---|
| `name` | string (optional) |

Dumps the companion's `HC_*` keys â€” the fastest way to diagnose a stuck state (e.g. `AutoPickup: false` with
`Recovering: false`).

```json
{ "name": "Maria", "keys": { "ActionMode": 1, "CombatStance": 3, "AutoPickup": true, "Recovering": false,
  "RecoverPickup": true, "OrderCap": 10, "FarmZones": "184 chars" },
  "farmZones": [ { "x": 100.0, "y": 30.0, "z": -40.0, "halfSize": 4.0, "crop": "CarrotSeeds", "rotation": 0.0 } ] }
```

### `companions_dismiss`

Dismisses a companion through the **same path as the radial menu** (`PrepareForDespawn` + release the bed +
`DespawnWithTombstone` + remove the body): the gear goes to a tombstone. Useful for tombstone tests.

| param | type |
|---|---|
| `name` | string (contains, case-insensitive) |

```json
{"name": "Companheiro 2", "dismissed": true}
```

### `companions_inventory`

Lists what a companion is carrying (prefab, name, amount, grid slot).

| param | type |
|---|---|
| `name` | string (contains, case-insensitive) |

```json
{"name": "Companheiro", "stacks": [{"prefab": "Stone", "name": "$item_stone", "amount": 10, "grid": "0,3"}], "totalStacks": 1}
```

### `companions_kill`

| param | type | default |
|---|---|---|
| `name` | string (contains, case-insensitive) | first companion |
| `damage` | number â‰¥ 1 | enough to kill |

Applies lethal damage with `Character.ApplyDamage` on the main thread (it bypasses armor on purpose). The mod's
`OnDeath` runs normally: tombstone, roster and recovery state are all written. Use `companions_list` to see the
`tombstones[]` entry.

```json
{"name": "Maria", "killed": true, "hpBefore": 120.0, "damage": 1000000.0, "hpAfter": 0.0}
```

### `companions_tier`

| param | type | required |
|---|---|---|
| `tier` | string | yes |
| `name` | string (optional) | no |

Runs `CompanionManager.ChangeTier` â€” the same swap the AI panel does, with all the carry-over (inventory, skills,
recovery keys, farm zones, name, appearance). Accepted tiers: `companion`, `warrior`, `mage`, `fire`, `ice`, `support`
or a prefab name (`HC_DvergerFire`). `changed: false` means it was already that tier.

```json
{"name": "Maria", "tier": "Warrior", "prefabBefore": "HC_Companion(Clone)", "prefabAfter": "HC_DvergerWarrior(Clone)", "changed": true}
```

### `companions_hire`

No parameters. Runs the shrine's **paid** path (`CompanionManager.HireWithCoins`): reads the price from
`CompanionTierData.Price`, requires that many coins in the pack, spawns the companion and removes the coins. It does
**not** open the shrine panel (see the workflow below for what that means).

```json
{"hired": true, "price": 1000, "coinsBefore": 2000, "coinsAfter": 1000}
```

When there are not enough coins it returns `hired: false`, `reason: "not enough coins"` and the mod logs its
`[Shrine] Odin asks for an offeringâ€¦` line, exactly like the panel's pre-check.

### `companions_resurrect`

| param | type |
|---|---|
| `name` | string (optional; matches the tombstone's respawn name) |

Finds the player's tombstone (by `HC_TombstoneId`/respawn data) and calls the mod's `MHR_ResurrectCore` â€” the same
path the 7-second hold uses. It does not need the dead object (the game destroys it). With `RecoverFirst`, the new
companion comes back Passive with auto-pickup off and starts the recovery flow.

```json
{"name": "Maria", "resurrected": true, "hp": 25.0, "x": 120.5, "y": 30.2, "z": -44.1, "tombstoneId": 639â€¦}
```

### `companions_farm_zone`

| param | type | default |
|---|---|---|
| `action` | `add`, `list`, `clear` | `list` |
| `name` | string (optional) | first companion |
| `x`, `y`, `z` | number | player position |
| `halfSize` | number â‰¥ 0.5 | 4 |
| `crop` | string (seed prefab) | `""` |

Reads/writes `HC_FarmZones` directly (the string format is documented in `docs/protocol.md`). `clear` removes every
zone. This is the setup helper for the `ChangeTier` Ã— farm-zones test.

```json
{"action": "add", "count": 1,
 "zones": [{"x": 120.5, "y": 30.2, "z": -44.1, "halfSize": 4.0, "crop": "CarrotSeeds", "rotation": 0.0}]}
```

## Suggested workflows

**Reproduce an order without typing:** `game_console` with `devcommands` + `spawn Troll 2` â†’ `companions_order`
`attack`/`stance_melee` â†’ `companions_list` to watch health â†’ `log_tail` for the order lines.

**Test a companion change:** `companions_zdo` before/after, with `screenshot` to see the appearance.

**Check a release in game:** `devbridge_ping` (mod version) â†’ `log_tail` â†’ `game_status`.

### The four 1.9.4 tests

Use `log_tail` to capture a `cursor` **before** the action, then `log_watch` with `since` so no line is missed.

**1. Shrine offering.** `player_inventory` `{item:"Coins", set:2000}` â†’ `log_tail` (cursor) â†’ `companions_hire` â†’
`log_watch` `"Summoned a companion for an offering"`; check `coinsAfter == 1000`. Then `player_inventory`
`{item:"Coins", set:999}` (price âˆ’ 1; the price is in the reply) â†’ `companions_hire` â†’ `hired: false` and
`log_watch` `"[Shrine]"`; no companion was created. Coins stack at 999, so the tool fills several stacks for 2000.
(The tool calls the paid method itself, not the panel's pre-check â€” the "refuses before opening the panel" branch
lives in `ShrinePanel.Summon`, which still has to be clicked by hand.)

**2. ChangeTier Ã— farm zones.** `companions_farm_zone` `{action:"add", crop:"CarrotSeeds"}` â†’ `companions_order`
`{action:"farm"}` â†’ **wait ~1.5 s** (`ApplyActionMode` writes the ZDO through `MHR_WriteOrQueue`) â†’ `log_tail`
(cursor) â†’ `companions_tier` `{tier:"warrior"}` â†’ `companions_tier` `{tier:"companion"}` â†’ `log_watch`
`"farmZones=yes"` â†’ `companions_zdo` and check `farmZones[0].crop == "CarrotSeeds"`. Note: every dverger tier is
combat-only (`CanWearArmor = false`), so changing to one **drops work mode 8 by design** (`work mode 8 dropped â€¦ a
combat-only type`); after returning to Companion, set `farm` again â€” the zones themselves must survive. The `[Tier]`
line also shows `recovering=` for test 3.

**3. ChangeTier Ã— recovery.** `companions_order` `{action:"stance_melee"}` â†’ `log_tail` (cursor) â†’
`companions_kill` â†’ `companions_resurrect` â†’ `log_watch` `"Revived Passive"` â†’ `companions_tier`
`{tier:"warrior"}` â†’ `log_watch` `"Found its own tombstone"` â†’ `log_watch` `"Gear back on and fed"` â†’
`log_watch` `"Recovery over - auto-pickup on"`. The `[Tier]` line must show `recovering=yes`. Observed nuance:
`MHR_ResurrectCore` already transfers the tombstone's items to the companion, so `"Found its own tombstone"` may not
appear when the tombstone is emptied by the revive; the recovery still reaches `Gear back on and fed` and
`Recovery over`.

**4. Double death during recovery.** `companions_kill` â†’ `companions_resurrect` (now recovering) â†’
`companions_kill` again during the recovery â†’ `log_watch` `"auto-pickup was True"` (must appear twice, with
`stance N`) â†’ `companions_resurrect` â†’ `log_watch` `"Recovery over - auto-pickup on"`. Counterproof:
`companions_order` `{action:"autopickup_off"}` â†’ kill/resurrect â†’ the line must say `auto-pickup was False` and the
end state must stay off.
