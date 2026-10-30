# Session handoff (2026-10-08)

Paste this into a new session to pick up where we left off. Details per version are in `docs/DEV_NOTES.md` (NEXT BUILD section, newest first) and `package/CHANGELOG.md`.

## Repo and workflow
- Repo: `MaddhattrRocks/Offline-Companions-Reborn` (private, going public soon). Fork of ProfMags' Offline Companions (MIT).
- Work branch: `claude/relaxed-archimedes-ecoh6h`. PRs #1-#5 are merged into `main` (C# conversion, 1.4.75, 1.4.76, 1.4.77-1.4.85, 1.4.86-1.4.140). After a merge the branch is restarted from `main` under the same name.
- The user builds the DLL (`dotnet build -c Release` on their PC, paths in the git-ignored `Environment.props`). Sessions only compile-check, never hand over DLLs unless asked. No game files in git.
- Version lives in `src/Companions/Core/CompanionsPlugin.cs` (PluginVersion) and `package/CHANGELOG.md` (one line per item, issue and fix together).
- Check the user's suggestions against the game's real code/mechanics (decompiled source) before building an alternative; read how the game does a mechanic for the player before imitating it (ladder bump lesson, 2026-10-13).
- Each version: user builds, tests, sends LogOutput.log; next version fixes what the log shows. Show the README "Recent Highlights" before any packaging (one bullet per feature: update an existing bullet, e.g. ladders, instead of adding a second one for a later version; keep bullets short); no packaged release until the movement update (phases 2-4) is done.

## Current state: 1.5.6 released - Combat LeashFromYou default 35 m, README Future Plans updated (next: second combat review + per-enemy strategies, combat on ships, late-game features/weapons, animal husbandry). 1.5.93 (last test): boarding jumps aboard first (your own takeoff spot, else a dry spot in jump reach of the deck, near-miss log), ladder only when there is none. 1.5.92: boarding: launch spot used once, wades to the ladder from the shallows (uses it standing), straight-line swim check in the water. 1.5.91: an enemy let go as unreachable is taken up again once melee can reach it or it attacks you/the companion within 5 m; Engage logged once per target. 1.5.90: beehive command (MHR_Hive: empty it, keep the honey), overhead stamina bar fixed after portals (the copy's health_fast re-enabled, rebuilt the same frame), food already-active log once. 1.5.89: getting off a ship: nearest side facing you (MHR_Deck.SideSpot), run and jump with quick retries, no follow route mid-air, [Deck] walk/stuck log. 1.5.88: swim stamina like the player (5/s at Swim 0 -> 2/s, [Stamina] SwimDrainAtSkill0), boarding swim counted to ladder reach and from the launch spot, ordered-aboard companions get off with you while following. 1.5.87: original 15 m "portal jump" follow warp removed (PortalPatches covers every game teleport). 1.5.86: old portal-jump warp fixed (stale follow spot after sitting), jog on deck, you count as aboard by the ship's player list, ladder only while swimming/standing. 1.5.85: deck route planner (AI/MHR_Deck.cs: a grid per ship type, A*, only while aboard - groundwork for ranged weapons on boats, the user's next topic); companions walk to seats and off ships (no teleports), stand up in place; boarding heads for the dry spot nearest the ladder and dives in; WaterSafety off while aboard. 1.5.84: boarding: dive in near the ladder, tread water until it's in reach, no shore retreat with the ladder close; mast seat point skipped; pointing at a ship seat sends a companion there. 1.5.83: boarding: resting for a swim isn't stuck, gives up at once on a swim it can't afford, waits on dry land; seat taken is logged (invisible-stool report - pick seats after the test). 1.5.82: ships boarded like a player (MHR_Board: jump aboard, the ship's ladder, swim to it, your steps; auto when you're aboard; waits on shore if it can't; gets up when you go ashore). 1.5.81: shrine buttons Summon / Recover, no dark layers in the summon/starter windows. 1.5.80: Odin's Shrine menu/recover list are the game's UnifiedPopup with relabelled buttons; unsummoned companions recoverable (no gear) until the world is loaded again ([General] RecoverUnsummoned); customise window copies the game canvas scale and popup button text. 1.5.79: shrine menu with the game's text styles and Craft-button copies, flavour text, closes like a chest (Use/Escape/walk away). 1.5.78: Odin's Shrine summoning (coin check on the menu button with a popup, coins taken on Summon), new Odin texts, game-look shrine menu and customising window (UI/GameLook.cs). WORKING RULES (user): read the WHOLE log first ("[You]" = the player; the companion's hits are "[Durability] Armor reduction"); propose changes and wait for the user's OK before writing code; once approved, build + commit + push in one go. 1.5.77: Odin's Shrine menu (Recover lost companions list / Purchase a new companion with the customising window), combo on a latched tick, no fleeing with one on, "let go" logged when it dies. 1.5.76: 1.5.75 combat rolled back (counter/leaper); a latched tick: logged, targeted at once, guard down, swing without lining up; no bow swap for a latched/mid-jump tick; loot never swims (1.5.75, kept). Confirm with the user before every build; read the whole log first ("[You]" lines are the player). 1.5.74: Balanced takes the bow for enemies melee can't reach (deep water, a level up/down, no route), dungeon bow shots from where it stands, leash 25 m from you, gives up on unreachable enemies after 10 s, draws kept at the bow's edge, tick roll removed (latched tick fought first, no backing off), Balanced status log every 3 s with ammo count. 1.5.73: latched ticks fought first, no swimming after deep-water enemies (bow from the bank), wading in shallow pools in a fight, wider spike look-ahead. 1.5.72: movement safety audit: combat fallbacks safe, spike walls avoided, safe placement spots. 1.5.71: hire companions at Odin's Shrine (Shift+E, 1000 coins from your pack). 1.5.70: no water-jumps in dungeons or over drops. 1.5.69: warps land on a spot you can see (no walls), following companions come through dungeon doors after you even mid-fight. 1.5.68: draw stamina by the companion's own Bows skill, faster turn while aiming. 1.5.67: flanking only over safe ground. 1.5.66: food topped up with 3 min left, fix for the combat freeze (NRE on a dead attacker). 1.5.65: targets only in sight (no walls/doors), closest first, fights don't open doors, indoor aggro-only, dungeon defensive within 8 m, recall/move orders hold fights 10 s. 1.5.64: Ranged stance carries the bow, melee only within RangedMeleeRange 4 m (8 m when out of breath), no fleeing when out of breath. 1.5.63: bow aims for the draw's arrow speed, no draw it can't finish (waits for breath), full draw far off. 1.5.62: bow draw runs on the clock, stand-and-shoot, clear-shot check, [BowDiag] for the bow string. 1.5.61: bows drawn like a player, chopping/mining swing through the combo with breathers, power chop only on stumps and low logs. 1.5.60: mod panels moved above the game HUD (order 1050+, below the pause menu; fixes the F8 click strip), more strings translatable, armour goes on while a job holds a tool (naked with Wood job report). Next: combat - fighting different kinds of enemies. 1.5.59: flanking: an enemy busy with someone else is attacked from behind (backstab only counts on unaware enemies - explained to the user). 1.5.58: spacing one on one. 1.5.57: chairs within 8 m. See DEV_NOTES 1.5.59-1.5.53.

## Previous state: 1.5.5 released - PR #18 squash-merged to main (3b4affe), main merged back into the work branch. Release zips built from main (Thunderstore with the BepInExPack dependency, Hexium, Nexus in BepInEx/plugins/OfflineCompanionsReborn/) plus the Nexus page text; the user uploads them. From 1.5.6: one zip for Thunderstore AND Hexium (the Thunderstore one; Hexium strips the BepInExPack dependency) + the Nexus zip, with LICENSE in both (see DEV_NOTES Packaging). Next test builds are 1.5.51, 1.5.52... toward 1.5.6. Next up: the combat review (weapons and usage, enemy/boss tactics, combat movement near edges); backlog in DEV_NOTES (speech flavour pass, wet/fire status effects, chair range, controller stance binds, stamina top-up while fishing, far-away recall/pins, ...).
- 1.4.152: multiplayer ownership (MHR_Owner), one chest helper (MHR_Chests Usable/Open/Claim), home chests for Smelt/Cook, own-drops-only pickups; Gathering FollowRange 10.
- 1.4.151: Forage with Follow on: own 10 m range ([Gathering] ForageFollowRange).
- 1.4.150: full-mod conflict audit - 14 groups of fixes (steering, items, follow flags, chests, patches).
- 1.4.149: config clean-up - regrouped sections, old values carried over, 22 dead settings removed, descriptions rewritten, [Advanced] section, ViewRange/ViewAngle work.
- 1.4.148: ship seats no longer kinematic (no Unity warning flood while seated on a ship).
- 1.4.147: all jobs (and Upkeep, Idle) also wait for ship boarding, the door command and ordered sit/sleep.
- 1.4.146: a repair the player orders at a station goes ahead of the job (jobs pause for it).
- 1.4.145: all jobs wait for orders (move/cart/deposit/tombstone); gear repair waits for loot pickup and orders, gives up on unreachable stations, skips items no station can fix for [Repair] NoStationWait.
- 1.4.144: gear repair trip waits for Cook/Farm/Hunt/Fish tasks (they fought over movement); only items below the threshold pick the repair station.
- 1.4.143: idle behaviours - near its job: small chores, sit, wander, socialize; says why now and then; [Idle] settings.
- 1.4.142: build has 0 warnings (no behaviour change).
- 1.4.141: movement phase 4 part 2 - old CompanionAI steering/ground-stuck/proactive jump/teleport removed, WaterSafety and the door command on MHR_Mover, old DoorHandler and [Door] settings gone. 
- 1.4.140: lines up with doorways before going through (no clipping open door leaves).
- 1.4.139: quicker retry at doors (close -> reopen).
- 1.4.138: door search buffer 16 -> 512 colliders (missed the door indoors); closes an open door after 1 s held up.
- 1.4.137: closes any open door next to it when stuck (1.4.136's look-ahead missed the leaf).
- 1.4.135 test: good; brief stops at an open door - 1.4.136 closes a door open towards it and reopens from its side. Next: movement phase 4 part 2.
- 1.4.135: jobs accept the mover's "arrived" (Farm planting pause). The last test log was still 1.4.133 - 1.4.134/135 untested.
- 1.4.134: movement phase 4 part 1 - per-job stuck timers replaced by the mover's give-up; Homestead leftovers removed. Part 2 next (CompanionAI old steering/ground-stuck/teleport, DoorHandler, WaterSafety).
- 1.4.133: gear repair at home below 80% only and only at usable stations (roof/fire); Repair and Restock pauses for commands, collects honey. Backlog: chest organisation by category near stations.
- 1.4.131 test: mostly working; a pile placed on top of another broke - 1.4.132 only places piles beside each other.
- 1.4.131: Cook keeps cooking fires and the oven at FuelLevel (50%), wood from chests or piles.
- 1.4.130: Smelt takes down a wood pile when the chests have no wood for the kiln (needs a hammer).
- 1.4.129: piles refill the chests (taken down below PileKeep); pile spots scored (no walkways, not by doors/ports/stations, backed by walls preferred).
- 1.4.127 test: piles work; Forage stuttered after fights (loot pickup and the job both steering) - 1.4.128 makes every job wait for the loot pickup.
- 1.4.127: pile contents count toward the gather jobs' limits.
- 1.4.126: Repair and Restock builds wood/stone piles next to existing ones (PileKeep 100 stays in chests).
- 1.4.124 test: repairs work (18 done); stuck when gear repair started at the same time - 1.4.125 lets gear repair go first. Next: wood/stone piles while sorting (design pending).
- 1.4.123 test: Repair and Restock stood still (hammer check used GetCurrentWeapon; reach blocked by touching floor). 1.4.124 fixes both.
- 1.4.123: loose pickups only early game (no bronze+ axe anywhere, no forge at home).
- 1.4.122: Wood/Stone pick up branches, stones and flint with a stone/flint axe or no axe ([Harvest] PickUpLoose).
- 1.4.121: gather jobs with Follow off work an area (home or start point, [Harvest] Range 40 m) like Hunt.
- 1.4.120: Wood/Stone/Ore/Forage keep Follow and work targets of opportunity near you (FollowRange 20 m).
- 1.4.119: per-appliance Cook switches; Repair and Restock sorts wherever it was started.
- 1.4.118: RepairBelow default 0.75; per-kind refuel switches (fires, hearths, braziers, standing torches, wall torches, hot tubs, other).
- 1.4.117: Repair and Restock job (real repair, refuel, like-with-like chest sorting near home); Restock and Homestead wheel slots are "Pending"; Homestead retired.
- 1.4.115 test: Fish works (user). 1.4.116: recast at the closest fish, better first cast.
- 1.4.115: rock tops allowed again, falls handled (back on / lost fish / avoid the spot after two falls).
- 1.4.113 test: fishing works really well (user). 1.4.114: wading spots, firm footing, shorter casts preferred.
- 1.4.112 test: only 1 fishing place mapped (too strict). 1.4.113 relaxes MapShore.
- 1.4.111 test: stuck floating (swim corner bug), fished from the water. 1.4.112 fixes both, spots off the edge.
- 1.4.108 test (user hadn't built 1.4.109/110): fishing works - fast spot, Perch + Pike caught. 1.4.111: rest before casting, stamina food (eat + fetch), stricter dive.
- 1.4.110: prefer dry routes unless the detour is long (DryDetour), jobs accept short swims (JobSwim).
- 1.4.108 test: first real catch (a Pike). 1.4.109: strict HaveStamina, 20 m casts, no job dives.
- 1.4.107 test: first real cast, float Setup crashed for the companion. 1.4.108: own float setup for companions, shore map for spots, aim clamp.
- 1.4.106 test: never found a bank (moat walls too high for the 4 m rule). 1.4.107 fixes the bank search.
- 1.4.106: Fish redone for real (vanilla float/fish, MHR_FishingPatches), one list format for every list (Core/MHR_List.cs). Bait crafting parked in the backlog.
- 1.4.104 tested: user says everything else works. 1.4.105: get up in the bed, then step off.
- 1.4.104: commands, wheel actions, tombstone, heal, combat chases, harvest approach/dodge, door handler, formation follow on MHR_Mover. Still old: Homestead, Stay Home, Fish.
- 1.4.103: hunting chases on MHR_Mover (shoot from the edge, leave prey needing a swim), no follow warp while still making progress.
- 1.4.102: no auto-sleep for 20 s after load, get up beside the bed, birds shot from 30 m / no swimming. Combat chases onto MHR_Mover is in the combat review.
- 1.4.101: get up from chairs onto open floor (not into the hearth); fire rescue after 3 s.
- 1.4.100: bird hunting (feathers), hearth/fire escape fix, command logs why nobody answered. User: hunting is fine now - next job is Fish (bait crafting).
- 1.4.99: close-enough jumps into shallow water, chained jumps, looser gap/water rules for shortcuts.
- 1.4.98: jumps use the companion's run speed, Jump skill and stamina (MHR_Jump); jumps cost stamina. Skills/stats review added to the backlog.
- 1.4.97: no swimming for loot, no crouch in water, shortcuts with 3 m drops / 1.5 m gap leaps, cook keeps collected honey. Bed kinematic fix confirmed.
- 1.4.96: hunters sneak up on skittish game (real stealth for crouching companions), jump narrow shallow water, loot survives a fight in between kills.
- 1.4.95: loot timing fix (+ shallow water, no swimming), cook walks to each honey, status effects (Wet etc.) slow companion regen.
- 1.4.88-1.4.93: Cook test fixes (fermenter RPC, fire spots, chest reach, mead plan by boss, beehives). Cook is done apart from testing the honey collecting.
- 1.4.94: Hunt redone (Follow on = hunt near the player; off = around home/start spot; anything that drops meat via [Hunting] Prey; MeatTarget 100; bow first; defends itself) and every kill gets looted (MHR_Loot, [Looting]). Next jobs: Fish (bait crafting), then Homestead (rename).

## Previous: 1.4.87
- 1.4.87: Cook rewritten to the agreed design (all cooking stations like a player, best known food, reserves/targets, boss-based mead focus, ledger so his own food isn't stored, [Cooking] settings, reference file OfflineCompanionsReborn.Cooking.txt). The user still has to review the MeadFocus defaults. Expect several refinement rounds.

## Previous: 1.4.86 pushed, not yet tested
- 1.4.86 (phase 3 part 2, Smelt only): Smelt on MHR_Mover with stand spots at chests and smelter slots, unreachable chests skipped; stairs walked normally (hop only when held up); no "hasn't moved" diag warning in combat. Cook and Homestead wait for a feature review.

## Previous: 1.4.85 (tested OK, merged in PR #4)
- 1.4.85: companions' own route map (`Patches/MHR_CompanionNavMesh.cs`): closed doors don't block routes (opened on arrival, any number), steps up to 0.6 m connect and are hopped as he reaches them. Config `General > CompanionPathing` (on; restart). Wider chest stand-spot search (1.2/1.7/2.2 m, 16 directions); farm opens a chest from its picked spot. Log should say "[Move] Companion route map ready" at world load.
- User's next test: build 1.4.85, check the farm stores crops in the house chests up the stairs, doors, stairs, narrow chests ("Skipping chest at ..." lines show any still ruled out).

## What 1.4.77-1.4.85 did (movement overhaul phase 3 part 1 + test fixes)
- Farm and gathering on MHR_Mover; farm harvests everything ripe before storing; plants in rows from the far edge; stores past unreachable chests; chest animations.
- Mover: soft-reset/give-up fixes, ledge guard, climb out of pits (sprint + jump at the lowest/least steep way up), trapped rescue (teleport back to the last good spot), go straight and hop steps/logs instead of long detours, hop bridges for stairs, door chains, then the companion route map.
- Follow: 1.5 s hold after the player sets off + hold while the player is within 2.5 m, trail mode for long detours/swims, no warp while in view (unless 1.5 x FollowTeleportDistance).
- Combat: only fights things after the player/companions (also when switching targets mid-fight); Engage log shows the creature's target. Shield carried with a one-hander.
- Bags: emptied when arriving home with no job (and after loading in at home), not only on a job change.

## Backlog (talk through with the user before building)
- Summon/starter customise window: remove the X in the corner, close with Escape/Use like the shrine menu (user, 1.5.79).
- Bigger buttons on the shrine popup (user, 1.5.81: revisit after testing) - resize UnifiedPopup's buttons while ours is up and restore them on close.
- Game look for the rest of the UI (user, 1.5.78): ConfigPanel (F8), HUD bars, interact panel, command wheel icons still use the packed PNGs.
- Bow-swap cooldown (user, 1.5.76): especially for fast-moving targets, so Balanced does not flip between bow and melee mid-fight.
- Greydwarf reach in spacing (user, 1.5.91, log 32bba563): the companion kept "just out of greydwarf's reach (its reach 2.7 m)" - the game's swing reach is ~1.2 m ([You] lines), so it hung back more than needed. Check the Danger/reach estimate per enemy in the enemy strategies overhaul.
- Combat on ships (user, 1.5.93: "The combat on ships should go on the backlog"): seated companions are switched off entirely (OnShip gate before combat) - get up for a threat, fight from the deck (bow with a clear shot over the side via MHR_Deck spots, melee from the side nearest a swimming enemy), sit back down after. Open questions from 1.5.91: any threat or only an attack; and while you steer.
- Second combat review, with per-enemy strategies (user, 1.5.93): a second pass over combat including the enemy strategies overhaul below (reach estimates such as the greydwarf one, ticks, leapers).
- Ticks in the enemy strategies overhaul (user, 1.5.74).
  - Swing sweep vs a latched tick (user, log 0a0d75c2): Balanced took the spear (40 deg stab) every tick fight; a tick latched 95 deg off to the side was missed, the iron sword (90 deg sweep) hit ticks 21-33 deg off every time. Think about weapon choice / sweep for ticks.
  - Dodge roll when a latched tick can't be reached - off to the side or on the back (user). (1.5.73 had a roll; the user wants it only for this case.)
  - Help others: a tick on the player or another companion is the priority to kill, as long as none is on this companion itself (user).
- **Jobs + Follow together:** field jobs (Wood/Stone/Ore...) should keep following the player and work what's nearby; home jobs turn Follow off (current rule for all jobs except Hunt since 1.4.94). User: with Ore and no ore around he just stood there.
- **Command wheel revisit** (Rename, guard/patrol, late-game features; maybe a sub-menu) and a **rename fallback** via the vanilla TextInput dialog (SeneaL_UI blocks the panel's name box).
- Movement phase 3 part 2 (Smelt, Cook, Homestead on MHR_Mover), part 3 (Hunt, Fish), phase 4 (remove old per-job stuck code and DoorHandler).
- Ranged weapons on boats (user, 1.5.85 - next topic): firing spots on the deck with line of sight over the rail (MHR_Deck has the grid and paths).
- Combat polish, ship-seat kinematic warnings, config cleanup, release packaging; older items in DEV_NOTES BACKLOG.
- Low priority (user, 2026-10-13): cart supply runs, siege weapons, battle formations + emotes, standing crew / ship combat - see DEV_NOTES BACKLOG (with video links). Infinity Hammer presets for testing.
- More sneaking (user, 2026-10-13): widen sneaking beyond hunting (follow the player's sneak, unaware enemies, dungeons) - see DEV_NOTES BACKLOG.
- Log cleanup once the mod is more complete: repeated debug lines (food checks, repair scans) log once until something changes (user, 2026-10-13).
