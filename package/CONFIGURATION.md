# AI Companions — Configuration reference

Every setting of the mod: what it does and the value it starts with.

**Where to change it:** in game, press `F8` to open the settings panel. You can also edit `BepInEx/config/AiCompanions.cfg` with any text editor, with the game closed.

**Tips**

- An option shown as a fraction (like `0.5`) means 50%. `0.25` is 25%, `1` is 100%.
- Distances are in meters. For scale: a player is about 2 m tall and a workbench build range is 20 m.
- **List options** all use the same format, separated by commas:
  - `*` = everything
  - `Name` = only that one
  - `-Name` = never that one
  - `Name:Number` = that one, with its own amount (`0` = no limit)
  - Names can be the item or creature name in game or the internal name (prefab). The names you can use in cooking, hunting and fishing are written to `OfflineCompanionsReborn.Cooking.txt`, `.Hunting.txt` and `.Fishing.txt` in the config folder every time the game starts.
- Updating from an old version? The options you changed are moved to the new names automatically the first time you enter a world. **Since 2.0.0** the file is `AiCompanions.cfg` and the keys and descriptions are in English (the native language of the mod; Portuguese is a translation): the old `.cfg` (`Maddhattr.OfflineCompanionsReborn.cfg`) is ignored and the settings go back to their default values.
- The section names below are the same as in the `.cfg` file (the `F8` panel shows them translated to the game language): `General` = **Geral**, `Controls` = **Controles**, `Follow` = **Seguir**, `Movement` = **Movimento**, `Combat` = **Combate**, `Food` = **Comida**, `Stamina` = **Stamina**, `Rest` = **Descanso**, `Death` = **Morte**, `Looting` = **Saque**, `GearRepair` = **Reparo de Equip.**, `Gathering` = **Coleta**, `Hunting` = **Caça**, `Fishing` = **Pesca**, `Farming` = **Plantio**, `Smelting` = **Fundição**, `Cooking` = **Cozinha**, `RepairAndRestock` = **Reparar e Reabastecer**, `Idle` = **Ócio**, `Speech` = **Fala**, `Advanced` = **Avançado**, `AI` = **IA**.

## Sections

- [General](#general)
- [Controls](#controls)
- [Controller](#controller)
- [Follow](#follow)
- [Movement](#movement)
- [Combat](#combat)
- [Torch](#torch)
- [Food](#food)
- [Stamina](#stamina)
- [Rest](#rest)
- [Death](#death)
- [Looting](#looting)
- [Gear Repair](#gear-repair)
- [Gathering](#gathering)
- [Hunting](#hunting)
- [Fishing](#fishing)
- [Farming](#farming)
- [Smelting](#smelting)
- [Cooking](#cooking)
- [Repair and Restock](#repair-and-restock)
- [Idle](#idle)
- [Speech](#speech)
- [Advanced](#advanced)
- [AI](#ai)

## General

The basics: what a companion costs, its health, stamina and carry limits, and how far "home" reaches.

**MaxCompanions** — Default: 10 · Range: 1 – 10  
Phase 3.19: the most companions you can have in this world (10 is the ceiling). The count includes the dead, the lost and the dismissed: the tombstone brings the SAME companion back and Odin's Shrine can still recover a dismissed one, so neither dying nor dismissing frees a slot on the spot. At the limit the Trader, the AI panel summoner and the starter companion refuse to create another (the Shrine can still recover a dead or a dismissed companion). The slot of a dismissed companion comes free when you next load this world - or right away with [General] RecoverUnsummoned off.

**CompanionPrice** — Default: 1000 · Range: 0 – 50000  
The offering in coins to summon another companion at Odin's Shrine (Summon companion), paid from your pack.

**PortalItems** — Default: `Game` · Options: `Game`, `AllowAll`  
What a following companion may carry through a portal with you. Game: the same rules as you - ore, metals and the like only through a portal that allows everything or with the world's portal setting on Casual, boss items never; it first puts what can't go through in a chest within 15 m of it, otherwise it stays behind and says why. AllowAll: it comes through with anything.

**RecoverUnsummoned** — Default: On  
An unsummoned (despawned) companion can be brought back at Odin's Shrine (Recover companions) - with its name, looks, skills and settings, but not its gear, which stays in the tombstone where it was unsummoned - until you next load this world. Off: unsummoning is permanent.

**ShowStatus** — Default: On  
When you look at a companion, show what it is doing under its name - and why not, when its job is stuck (e.g. "Hunt - No game around.").

**SpawnStarterCompanion** — Default: On  
The first time you enter a world with a character, you're offered one companion for free.

**ResetStarterFlag** — Default: Off  
Set to true to be offered the free starter companion again in the current world (on the next world load). It turns itself back to false.

**ShowCompanionRoster** — Default: Off  
Set to true to list your companions in this world and their status: Alive, Dead (its tombstone will bring it back), Lost (nothing left of it - it can be recovered) or Unknown (on a dedicated server, a companion that isn't nearby can't be checked). It turns itself back to false.

**RecoverLostCompanion** — Default: Off  
Set to true to bring back a Lost companion next to you, with its gear and settings from the last roster save. An Unknown companion needs it set a second time within 60 s to confirm (it may still be alive somewhere). If the old companion turns up later, it's removed. It turns itself back to false.

**ProtectCompanions** — Default: On  
Stops other mods from deleting a companion outright (for example mods that clear creatures out of warded areas). Companions can still die, leave a tombstone and respawn as normal, and the Despawn button still works. When something is blocked, the log says what tried. Turn off only if another mod needs to remove companions.

**ShowMinimapPanel** — Default: On  
Show the companion status panel under the minimap.

**ShowOverheadBars** — Default: On  
Show health and stamina bars above each companion's head.

**MaxCarryWeight** — Default: 300 · Range: 50 – 2000  
The BASE weight a companion can carry (a player carries 300). Gathering and hunting companions head home to unload before this. Items that raise carrying capacity add on top of this value: the Megingjord, for example, gives +150 and counts with the belt anywhere in the companion's bag (no need to equip it), so with this at 300 its ceiling becomes 450. Two identical belts don't add up: the bonus counts once, as in the game.

**BaseHealth** — Default: 25 · Range: 1 – 500  
A companion's health before food (a player has 25).

**BaseStamina** — Default: 50 · Range: 1 – 500  
A companion's stamina before food (a player has 50).

**MaxFoodSlots** — Default: 3 · Range: 1 – 5  
How many different foods a companion can have eaten at the same time (a player has 3).

**StatusEffectsOnRegen** — Default: On  
Status effects change a companion's health and stamina regeneration like they do for a player: Wet and Cold slow it, meads and other buffs change it. Off = companions regenerate at their normal rate whatever the effects.

**HomeTripRange** — Default: 60 · Range: 10 – 500  
How far from home (meters) a companion will still walk back for chest trips: fetching or putting back tools, repairing a broken tool, emptying its bags. Further away than this, a companion missing its job tool says so and follows you until you are both back near home.

**HomeRadius** — Default: 50 · Range: 5 – 200  
Starting size (meters) of a companion's home area around its home spot - where it looks for a bed to claim. Each companion's own size can be changed later in its home panel.

## Controls

The keys and how far your orders reach.

**DirectTargetKey** — Default: `Z`  
The command key. Tap it while looking at something to send your companions to it (move there, attack it, open the door, sit by the fire, repair at the station...). Hold it to call them to you.

**RadialMenuKey** — Default: `E`  
Hold this while looking at a companion to open its command wheel.

**ConfigPanelKey** — Default: `F8`  
Opens the in-game settings panel, where all of these settings can be changed.

**FarmZoneKey** — Default: `Z`  
Pressed together with FarmZoneModifier: opens the farm zone editor.

**FarmZoneModifier** — Default: `LeftAlt`  
Hold this and press FarmZoneKey to open the farm zone editor.

**RecallRange** — Default: 40 · Range: 0 – 500  
How close (meters) a companion has to be to you to answer a recall (holding the command key) or a point-and-command order. Companions with Command switched off on the wheel ignore both. 0 = no limit.

**SuperRecallHoldTime** — Default: 2 · Range: 0.5 – 10  
Keep holding the command key this many seconds to recall every companion at any distance, including ones with Command switched off.

**PassiveKey** — Default: `None`  
Sets the Passive stance. Looking at one of your companions: just that one; otherwise every companion within RecallRange that has Command on. None = no key.

**BalancedKey** — Default: `None`  
Sets the Balanced stance, like PassiveKey.

**RangedKey** — Default: `None`  
Sets the Ranged stance, like PassiveKey.

**MeleeKey** — Default: `None`  
Sets the Melee stance, like PassiveKey.

## Controller



**Command** — Default: `Y`  
Controller command button: tap while looking at something to send your companions to it, hold to call them to you. None = off. Use and Alt follow the game's controller layout (Alt is the game's own modifier - your block button); the rest are fixed buttons.

**CommandModifier** — Default: `Alt`  
Held with Command. Held while pressing the button. While it's held, that button's press is for the companions only (the game doesn't see it); on its own the button works as normal. None = the button alone.

**Wheel** — Default: `X`  
Controller button for a companion's command wheel, while looking at it. None = off. Use and Alt follow the game's controller layout (Alt is the game's own modifier - your block button); the rest are fixed buttons.

**WheelModifier** — Default: `Alt`  
Held with Wheel. Held while pressing the button. While it's held, that button's press is for the companions only (the game doesn't see it); on its own the button works as normal. None = the button alone.

**Settings** — Default: `None`  
Controller button for this settings panel. None = off. Use and Alt follow the game's controller layout (Alt is the game's own modifier - your block button); the rest are fixed buttons.

**SettingsModifier** — Default: `None`  
Held with Settings. Held while pressing the button. While it's held, that button's press is for the companions only (the game doesn't see it); on its own the button works as normal. None = the button alone.

**FarmZone** — Default: `None`  
Controller button for the farm zone editor. None = off. Use and Alt follow the game's controller layout (Alt is the game's own modifier - your block button); the rest are fixed buttons.

**FarmZoneModifier** — Default: `None`  
Held with FarmZone. Held while pressing the button. While it's held, that button's press is for the companions only (the game doesn't see it); on its own the button works as normal. None = the button alone.

**Passive** — Default: `None`  
Controller button for the Passive stance (like PassiveKey). None = off. Use and Alt follow the game's controller layout (Alt is the game's own modifier - your block button); the rest are fixed buttons.

**PassiveModifier** — Default: `None`  
Held with Passive. Held while pressing the button. While it's held, that button's press is for the companions only (the game doesn't see it); on its own the button works as normal. None = the button alone.

**Balanced** — Default: `None`  
Controller button for the Balanced stance. None = off. Use and Alt follow the game's controller layout (Alt is the game's own modifier - your block button); the rest are fixed buttons.

**BalancedModifier** — Default: `None`  
Held with Balanced. Held while pressing the button. While it's held, that button's press is for the companions only (the game doesn't see it); on its own the button works as normal. None = the button alone.

**Ranged** — Default: `None`  
Controller button for the Ranged stance. None = off. Use and Alt follow the game's controller layout (Alt is the game's own modifier - your block button); the rest are fixed buttons.

**RangedModifier** — Default: `None`  
Held with Ranged. Held while pressing the button. While it's held, that button's press is for the companions only (the game doesn't see it); on its own the button works as normal. None = the button alone.

**Melee** — Default: `None`  
Controller button for the Melee stance. None = off. Use and Alt follow the game's controller layout (Alt is the game's own modifier - your block button); the rest are fixed buttons.

**MeleeModifier** — Default: `None`  
Held with Melee. Held while pressing the button. While it's held, that button's press is for the companions only (the game doesn't see it); on its own the button works as normal. None = the button alone.

## Follow

How companions follow you.

**MaxDistance** — Default: 10 · Range: 4 – 30  
How far a following companion lets you get before it moves to keep up. It picks its own spot within this range (beside, behind, sometimes ahead) and changes it now and then.

**AutoBoard** — Default: On  
When you're aboard a ship close by, a following companion gets on too, the way you would: it jumps aboard from a dock or the shore, uses the ship's ladder (swimming to it if it has the stamina), or follows your own steps aboard. If it can't, it waits on the shore. A companion that took a seat gets up again when you go ashore.

**BoardRange** — Default: 20 · Range: 5 – 60  
AutoBoard: how close (meters) the ship has to be for a following companion to board it on its own. The ship also has to be nearly still.

**IndoorDistance** — Default: 4 · Range: 2 – 10  
The same range while you're indoors (under a roof), so companions stay in the room with you.

**Regroup** — Default: On  
When you stand still for RegroupTime seconds, companions come back and wait close to you. Off = they stay wherever they stopped.

**RegroupTime** — Default: 45 · Range: 5 – 600  
Seconds you stand still before companions regroup around you (see Regroup).

**SitIndoors** — Default: On  
When you stop indoors, companions sit down near you: on a free chair or bench if there is one, otherwise on the floor. Near home they empty their bags first.

**ChairRange** — Default: 8 · Range: 2 – 30  
How far (meters) from you a companion looks for a free chair or bench to sit on when you stop indoors - one in sight of you, on your level. Only when there's none does it sit on the floor.

**SitStayRange** — Default: 10 · Range: 2 – 30  
Indoors, a companion that sat down stays seated while you move about within this many meters of it, and gets up to follow once you go further (or step outside).

**TeleportDistance** — Default: 50 · Range: 20 – 200  
A following companion that falls this many meters behind appears next to you: right away beyond 1.5x this distance, otherwise once it can't see you and isn't getting any closer.

**StuckTeleport** — Default: Off  
When following you, a companion that has been stuck for 15 seconds somewhere it can't see you appears next to you. Off = it keeps trying to find its own way.

**FormationSpacing** — Default: 3 · Range: 1 – 10  
Space (meters) between companions walking side by side in formation (when they follow something other than you).

**FormationCatchUp** — Default: 15 · Range: 5 – 50  
In formation, a companion this many meters away from its place runs to catch up.

## Movement

How companions move: speeds, how far they see and how they get around obstacles (doors, steps, water, jumps).

**WalkSpeed** — Default: 2 · Range: 0.5 – 5  
Walking speed (a player walks at about 2). Takes effect after the world is reloaded.

**RunSpeed** — Default: 7 · Range: 2 – 15  
Running speed (a player runs at about 7). Takes effect after the world is reloaded.

**SwimSpeed** — Default: 2 · Range: 0.5 – 5  
Swimming speed (a player swims at about 2). Takes effect after the world is reloaded.

**TurnSpeed** — Default: 300 · Range: 50 – 600  
How fast a companion turns (degrees per second). Takes effect after the world is reloaded.

**ViewRange** — Default: 20 · Range: 5 – 100  
How far (meters) a companion can see enemies and other creatures. Takes effect after the world is reloaded.

**ViewAngle** — Default: 120 · Range: 30 – 360  
A companion's field of view (degrees). Takes effect after the world is reloaded.

**Pathing** — Default: On  
Companions find their way with their own map of the world, where doors don't block the way (they open them as they get there) and steps up to 0.6 m are climbable (they hop them). Off = the same map creatures use, where every closed door is a wall and most stairs don't connect. Takes effect after a restart.

**WorkRunDistance** — Default: 25 · Range: 0 – 500  
Companions walk while doing jobs (farming, chest trips, smelting, cooking, chores, repairs, gathering) and only run when what they are heading to is further away than this many meters. 0 = never run for work. Following you, fighting and hunting are not affected.

**DryDetour** — Default: 30 · Range: 0 – 500  
Companions prefer staying dry, like a player: when the way somewhere crosses water they go round it on land, as long as the dry way is no longer than 1.5x the swimming way plus this many meters. Otherwise they swim.

**JobSwim** — Default: 20 · Range: 0 – 200  
The most swimming (meters) a job accepts to reach something: loot from a kill, prey, a bird, a fishing spot. Further than that and it leaves it.

**MaxDrop** — Default: 3 · Range: 1 – 4  
When a straight line is much shorter than the way round (down the stairs and back, round a wall), companions take it like a player: hopping steps, dropping off ledges up to this many meters (players take no fall damage below 4 m) and jumping small gaps (JumpGap).

**StuckDrop** — Default: 8 · Range: 3 – 20  
When a companion is stuck above where it needs to be with no other way down (off the top of a ladder, a platform, a ledge), it jumps down a drop up to this many meters, like a player who takes a bit of fall damage rather than stay put. A ladder down comes first, and it never takes a drop that would cost more than 30% of its health right now (companions take fall damage like a player: none up to 4 m).

**JumpGap** — Default: 1.5 · Range: 0.5 – 3  
The widest gap (meters) a companion leaps across on such a shortcut. 0.5 = no gaps.

**JumpWaterDepth** — Default: 0.3 · Range: 0.05 – 2  
Water at least this deep (meters) in a companion's way is jumped over when it's narrow enough (JumpWaterWidth), like a player jumps a small stream instead of running through it. Wider shallow water is waded through.

**JumpWaterWidth** — Default: 3 · Range: 1 – 4  
The widest stretch of water (meters) a companion jumps over.

**SwimStaminaShore** — Default: 0.25 · Range: 0 – 1  
When a swimming companion's stamina drops below this share (0.25 = 25%), it heads for the nearest shore, whatever it was doing.

## Combat

When companions fight and how they pick weapon and shield.

**EngageOnNotice** — Default: On  
On: companions attack a creature as soon as it has noticed you or a companion. Off: they wait until it is actually coming for you (alerted). Anything that attacks is always fought back, and Hunt goes after its prey either way.

**HealthRetreatPct** — Default: 0.3 · Range: 0.05 – 0.9  
Below this share of its health (0.3 = 30%) a companion fights defensively: it takes out its best blocking shield and doesn't open with a torch.

**FleeHealthPct** — Default: 0.15 · Range: 0.05 – 0.6  
Below this share of its health (0.15 = 15%) a companion runs away from the enemy.

**WeaponSwitchMargin** — Default: 0.1 · Range: 0 – 0.5  
A different weapon has to be this much better against the current enemy (0.1 = 10%) before a companion switches to it, so it doesn't swap back and forth.

**ShieldMultiAttackerCount** — Default: 3 · Range: 1 – 10  
When this many enemies attack a companion at once, it switches to its shield with the best block.

**ParryChance** — Default: 0.3 · Range: 0 – 1  
Chance (0.3 = 30%) that a companion with no Blocking skill times a block as a parry (more damage stopped, less stamina, and many attackers are staggered). It rises with Blocking skill up to ParryChanceMax. Shields without a parry bonus never parry.

**ParryChanceMax** — Default: 0.7 · Range: 0 – 1  
Chance (0.7 = 70%) that a companion times a block as a parry at Blocking skill 100.

**FightLikePlayer** — Default: On  
With a melee weapon, companions fight like a player: short combos when there's an opening, guarding in between, stepping out of swings, getting clear when several enemies are on them, and backing off to catch their breath when low on stamina. Off: the older way (one swing at a time on the weapon's timer).

**ComboHits** — Default: 3 · Range: 1 – 5  
Most swings in one combo. Companions follow their weapon's own combo (3 swings on melee weapons, the last doing double damage) up to this many; now and then one short, and shorter when stamina is low.

**ComboPause** — Default: 0.5 · Range: 0.2 – 5  
Seconds (roughly - it varies) between combos, guarding or stepping back out of reach.

**StaminaReserve** — Default: 0.3 · Range: 0 – 0.9  
Share of its stamina (0.3 = 30%) a companion keeps for blocking. A combo is only started above it and may dip to half of it; below it, the companion backs out of reach to get its breath back.

**StaminaResume** — Default: 0.5 · Range: 0 – 1  
Share of its stamina (0.5 = 50%) it waits for before going back in after backing off.

**BackOffTime** — Default: 8 · Range: 2 – 30  
Seconds a companion keeps trying to back off from an enemy that stays on it before it fights on with what stamina it has.

**StepAwayChance** — Default: 0.3 · Range: 0 – 1  
Chance (0.3 = 30%) a companion steps back or aside out of an enemy's swing instead of blocking it, when there's room and it can get out of reach in time. Doubled when it's low on stamina.

**GroupSize** — Default: 2 · Range: 2 – 10  
With this many enemies close enough to hit a companion, from different sides, it gets clear first - moves for a couple of seconds until they're all on one side - then fights from there.

**GroupRange** — Default: 6 · Range: 2 – 20  
How close (meters) an enemy has to be to count as one of the group on a companion.

**FlankAngle** — Default: 100 · Range: 30 – 180  
Two enemies more than this many degrees apart, seen from the companion, count as on different sides.

**BowAimTurnSpeed** — Default: 600 · Range: 100 – 1440  
Bows: how fast a companion turns to keep its aim on the target while the bow is drawn (degrees per second), like you swinging the camera round. Movement > TurnSpeed is used the rest of the time.

**BowMinDraw** — Default: 0.5 · Range: 0.1 – 1  
Bows: the least a companion draws before letting go (0.5 = half drawn). A partial draw is quicker and cheaper but does less damage and spreads more, like yours. It takes a partial draw when the target is close enough that the spread still hits it, when it's low on stamina (below StaminaReserve), or when the target is rushing in; otherwise it draws fully.

**IndoorAggroOnly** — Default: On  
Indoors - in a dungeon, mine or cave, or under a roof - a companion only fights enemies that are actually coming for you or it (alerted), as if EngageOnNotice were off. Outdoors EngageOnNotice decides.

**DungeonDefensive** — Default: On  
In a dungeon, mine or cave a companion fights defensively: it stays with you and fights what comes within DefendRange of you (and anything hitting it), instead of running off after enemies further down the corridor.

**DefendRange** — Default: 8 · Range: 3 – 30  
Fighting defensively (DungeonDefensive): enemies within this many meters of you are fought; further off they're left to come to you.

**OrderHoldTime** — Default: 10 · Range: 0 – 60  
After you recall a companion or send it somewhere, it leaves its fight and starts no new one for this many seconds - it still fights back against something hitting it up close. Companions only ever fight enemies they can see (not ones behind walls or closed doors), and only open a door to get at one when you order the attack.

**LeashFromYou** — Default: 35 · Range: 10 – 60  
A companion lets an enemy go once it is further than this many meters from you, and starts no fight with one that far from you - it comes back to you instead. An attack order, or something hitting it up close, still keeps the fight going.

**GiveUpTime** — Default: 10 · Range: 3 – 60  
An enemy a companion can't get at with its melee weapon (out in deep water, no way to walk there) and that isn't right on top of it is let go after this many seconds without being hurt, and left alone for 20 s - so it doesn't stay stuck on it.

**BowEdgeMargin** — Default: 3 · Range: 0 – 10  
Bows: a draw is only started this many meters inside the bow's 30 m reach, and a draw already started is kept while the target is up to twice this far past the reach (walking closer, not running), so a target at the edge doesn't cost the draw.

**RangedMeleeRange** — Default: 4 · Range: 1 – 15  
Ranged stance: the bow is out (also when not fighting); the melee weapon and shield only come out when an enemy gets within this many meters - or twice that while it's catching its breath - and go away again once it's past twice the distance.

**BowFullDrawRange** — Default: 15 · Range: 3 – 50  
Bows: from this many meters away a companion always draws fully - a weaker arrow is slower and drops short at range.

**FinishHits** — Default: 2 · Range: 0 – 5  
An enemy this many hits or fewer from dead (judged from its own hits so far) is finished off with all the stamina it takes, as long as nothing else is close enough to hit the companion afterwards. 0 = never spend the stamina kept back for blocking.

**Spacing** — Default: On  
One on one, a companion keeps just outside the enemy's reach, goes in for a combo when the enemy can't swing again in time, then steps back out - and steps out of swings at the edge of its reach rather than blocking them.

**Flank** — Default: On  
When an enemy is busy with you (or someone else), a companion works its way round behind it and attacks from there, out of its swings. (The game's backstab bonus only counts on enemies that haven't noticed anyone.)

**SpacingMargin** — Default: 0.4 · Range: 0 – 2  
How far (meters) outside the enemy's reach a companion waits when spacing.

**SpacingStepChance** — Default: 0.6 · Range: 0 – 1  
Chance (0.6 = 60%) that a spacing companion steps back out of a swing it's only just inside the reach of, instead of blocking it (the rest are blocks, some of them parries).

**BowSwitchDistance** — Default: 15 · Range: 5 – 60  
A companion with a bow or crossbow switches to it when its target is further away than this (meters).

**MeleeSwitchDistance** — Default: 12 · Range: 3 – 40  
It switches back to its melee weapon when the target comes closer than this (meters).

**UseTorchOpener** — Default: On  
Against enemies that are weak to fire, open with a torch to set them alight, then switch back to the best weapon.

**TorchOpenerTimeout** — Default: 4 · Range: 1 – 10  
Seconds to keep trying to set an enemy alight before giving up and switching back.

**TorchOpenerCooldown** — Default: 10 · Range: 0 – 60  
Seconds before the same enemy can be opened on with a torch again.

**TorchOpenerMinTargetHealth** — Default: 40 · Range: 0 – 500  
No torch opener on enemies with less maximum health than this - not worth it.

## Torch



**BurnsDown** — Default: On  
A torch in a companion's hand burns down like yours and is gone at 0 durability. Off: companions' torches last forever (the game itself only burns down players' torches).

**DarkLevel** — Default: 0.4 · Range: 0 – 1  
In a dungeon or cave, a companion lights a torch when the light there is below this (0 = pitch black, 1 = bright day). The log shows the light level each time you go into one ([Torch] lines), to tune this. Outdoors it's simply night.

## Food

When companions drink meads.

**HealthMeadThreshold** — Default: 0.5 · Range: 0.1 – 0.9  
A companion drinks a healing mead from its pack when its health drops below this share (0.5 = 50%).

**StaminaMeadThreshold** — Default: 0.25 · Range: 0.05 – 0.9  
It drinks a stamina mead when its stamina drops below this share (0.25 = 25%).

**EatAgainMinutes** — Default: 3 · Range: 0 – 30  
A companion eats the same food again once it has this many minutes left, like a player topping up before it runs out (the game only allows that once a food is under half its time). It needs that food in its pack; otherwise it eats something else when the slot empties. 0 = wait until it runs out.

**MeadCooldown** — Default: 10 · Range: 1 – 60  
Seconds after drinking a mead before it drinks another.

## Stamina

How fast companions spend and recover stamina.

**RegenRate** — Default: 6 · Range: 1 – 30  
Stamina a companion gets back per second when it isn't using any.

**RunDrainRate** — Default: 10 · Range: 1 – 30  
Stamina used per second while running.

**SneakDrainRate** — Default: 5 · Range: 1 – 30  
Stamina used per second while sneaking.

**SwimDrainAtSkill0** — Default: 5 · Range: 1 – 30  
Stamina used per second while swimming (and moving), at Swim skill 0 - it drops to 40% of this at Swim 100, like the player (the game's rate is 5).

**RegenDelay** — Default: 1 · Range: 0.1 – 5  
Seconds after using stamina before it starts coming back.

## Rest

Sleeping in beds, sitting by the fire and the Rested effect.

**SleepAtNight** — Default: On  
At night, a companion within BedRange of its claimed bed stops what it's doing and goes to bed, including when it's working. It goes back to its job in the morning.

**SleepWhenPlayerSleeps** — Default: On  
When you get into a bed, your companions within BedRange of their claimed beds go to bed too, even if it isn't night yet. The night skip waits up to 30 seconds for them.

**BedRange** — Default: 50 · Range: 10 – 200  
How close (meters) a companion has to be to its claimed bed for the two settings above to send it to bed.

**HealRate** — Default: 2 · Range: 0.5 – 20  
Health a companion gets back per second while sitting by a fire or sleeping.

**PlayerRange** — Default: 5 · Range: 2 – 20  
When you sit down near a fire (the sit emote), companions without a job within this many meters of you sit down by it too.

**FireRange** — Default: 5 · Range: 2 – 20  
How close (meters) a fire has to be for a companion to sit down by it.

**WarmupTime** — Default: 20 · Range: 5 – 120  
Seconds of sitting by a fire before a companion becomes Rested, like a player.

**DirectedTimeout** — Default: 300 · Range: 30 – 1800  
When you tell a companion to sit by a fire, it gets up again after this many seconds.

## Death

What happens when a companion dies and how it comes back.

**AutoRespawn** — Default: On  
A dead companion comes back by itself after RespawnDelay seconds if nobody brings it back from its tombstone first.

**RespawnDelay** — Default: 30 · Range: 30 – 3600  
Seconds after death before a companion comes back by itself.

**RespawnAtWorldSpawn** — Default: Off  
A companion that comes back by itself and has no claimed bed appears at the world spawn stones. Off = next to you.

**RespawnAtPlayer** — Default: Off  
A companion that comes back by itself always appears next to you, even when it has a bed (wins over RespawnAtWorldSpawn). Off = at its bed, and without one as RespawnAtWorldSpawn says.

**DeathItemRule** — Default: 0 · Range: 0 – 3  
What a companion loses when it dies. 0 = the world's death penalty setting, same as a player. 1 = keep everything. 2 = keep equipped gear, the rest goes in the tombstone. 3 = everything goes in the tombstone. The owner's setting is used even if another player's game handles the death.

**TombstoneRecoveryRange** — Default: 50 · Range: 5 – 100  
A companion walks over and empties its own leftover tombstone when it's within this many meters of it.

**RespawnStance** — Default: `RecoverFirst` · Options: `RecoverFirst`, `Passive`, `Keep`  
The stance a companion comes back in after it died. RecoverFirst: Passive, auto-pickup off, it gets its gear back from its tombstone when it's safe (fighting back only if attacked with a weapon in hand), puts it on, eats, then goes back to the stance it died with. Passive: Passive until you change it. Keep: the stance it died with. Setting a stance yourself always wins.

**RecoverThreatRange** — Default: 15 · Range: 5 – 50  
RecoverFirst: enemies within this many meters of the companion or its tombstone count when it decides whether it's safe to go for its gear.

**RecoverMaxMobs** — Default: 3 · Range: 0 – 20  
RecoverFirst: with more enemies than this busy fighting near its tombstone it waits by you instead of going for its gear.

**ShrineBuildable** — Default: On  
Odin's Shrine can be built with the hammer (Misc tab, needs a workbench). Use it to see your companions and bring back a lost one. Takes effect on the next world load; shrines already built keep working.

**ShrineRecipe** — Default: `Stone:10,Wood:5,GreydwarfEye:3`  
What Odin's Shrine costs to build, as Item:Amount pairs separated by commas (item names as in the game's spawn command). Takes effect on the next world load.

**ShrineModel** — Default: `Tombstone`  
What Odin's Shrine looks like: Tombstone (a player's gravestone) or Ward (the ward stone). Takes effect on the next world load, for shrines already built too.

**SkillLoss** — Default: 0.25 · Range: 0 – 1  
Share of its skills a companion loses when it dies (0.25 = 25%, 0 = none).

## Looting

Picking up what a kill left on the ground.

**PickUpKills** — Default: On  
After a companion kills something it goes over and picks up what the creature dropped (things you'd auto-pickup yourself), like a player would. Needs Auto Pickup on in the wheel.

**Radius** — Default: 6 · Range: 2 – 20  
How far around the kill (meters) the drops are looked for.

**WaitSeconds** — Default: 10 · Range: 2 – 60  
Longest wait (seconds) by the body for its drops to come out.

**MaxDistance** — Default: 30 · Range: 5 – 100  
Kills further away than this (meters), or that you've moved this far away from while it follows you, are left.

**CollectRadius** — Default: 10 · Range: 1 – 30  
With the "collect the items on the ground" order, how far (meters) from the companion it looks for loose things on the ground - including what you or it dropped on purpose. It walks to each item and picks them up one by one, until nothing is left in range, the bag is full or the order goes past 30 seconds.

**AutoPickupRange** — Default: 2 · Range: 0.5 – 10  
With Auto Pickup on in the wheel, a companion picks up items within this many meters of itself as it goes, like a player's auto-pickup.

## Gear Repair

Repairing the companion's own weapons, armour and tools at a bench.

**DurabilityThreshold** — Default: 0.5 · Range: 0.1 – 0.9  
A companion takes its gear to a crafting station to repair it when something drops below this share of durability (0.5 = 50%).

**HomeThreshold** — Default: 0.8 · Range: 0.1 – 1  
At home (or, without a home set, inside a base - a workbench's area), gear is repaired a bit sooner: below this share (0.8 = 80%). 1 = at any wear. Away from home DurabilityThreshold applies.

**NoStationWait** — Default: 600 · Range: 30 – 3600  
When a worn item can't be repaired at any station within reach (say it needs a black forge and there isn't one nearby), or the way to the station failed, that item is left alone for this many seconds before it looks again.

**StationRange** — Default: 20 · Range: 5 – 60  
How far (meters) a companion looks for a crafting station to repair at. Near home, any station near home counts.

## Gathering

The Wood, Stone, Ore and Foraging jobs.

**Range** — Default: 60 · Range: 10 – 150  
Wood, Stone, Ore and Forage with Follow off: how far (meters) from where it's working it goes for trees, rocks and plants - around home when the job is started near home, otherwise around where it was started. It keeps working there when you leave. (Phase 3.3c: 60, was 40.)

**FollowRange** — Default: 25 · Range: 5 – 60  
Wood, Stone and Ore with Follow on: a tree or rock has to be within this many meters of you. With nothing that close the companion just follows you. (With Follow off see Range; Forage has ForageFollowRange.) Phase 3.3c: 25, was 10 - it works up to 25 m from you instead of sticking to you, and it only gives up a target once that is 15 m past it.

**ForageFollowRange** — Default: 20 · Range: 3 – 60  
Forage with Follow on: a plant has to be within this many meters of you - the berries you happen to walk past, not everything around. With nothing that close the companion just follows you. (With Follow off see Range.) Phase 3.3c: 20, was 10 - still short enough for Forage to stay "what you walked past", and not a sweep of the whole area.

**FollowLogRange** — Default: 20 · Range: 5 – 60  
Wood with Follow on: fallen logs and stumps count up to this many meters from you (further than FollowRange), so it finishes the trees it fells before starting a new one - a log can roll well away from where you stand.

**RestStamina** — Default: 0.15 · Range: 0 – 0.9  
Chopping and mining: swing after swing (the weapon's combo) until stamina would drop below this share of the maximum, then stop and get its breath back.

**ResumeStamina** — Default: 0.6 · Range: 0.1 – 1  
Chopping and mining: after stopping for breath, start swinging again once stamina is back to this share of the maximum.

**PowerChopLogBelow** — Default: 0.3 · Range: -1 – 2  
Wood: the power chop (the axe's downward swing) is used on stumps, and on a log only when its top is less than this many meters above the companion's feet - too low for the sideways swing. Everything else gets normal swings.

**PickUpLoose** — Default: On  
Wood and Stone jobs, early in the game only: also pick up what's lying on the ground by hand - branches (Wood job), loose stones and flint (Stone job) - as long as they're on GatherWood / GatherStone and under their amount. Early game = there's no bronze-or-better axe in the companion's pack, your pack or the chests near home, and no forge at home to make one. With no tool for the job (no axe for Wood, no pickaxe for Stone) picking things up is all it does.

**WoodLimit** — Default: 500 · Range: 0 – 100000  
How many of each wood material (counting the companion's pack and chests near its home) the Wood job gathers before stopping. 0 = no limit. Per-item numbers in GatherWood override this.

**StoneLimit** — Default: 500 · Range: 0 – 100000  
Same as WoodLimit, for the Stone job and GatherStone.

**OreLimit** — Default: 500 · Range: 0 – 100000  
Same as WoodLimit, for the Ore job and GatherOre.

**GatherWood** — Default: see below  
What the Wood job collects. Targets are only worked if they drop something on this list that is still under its amount; drops not on the list are left on the ground. A name with no number uses WoodLimit. List format (same for every list): * = everything, Name = this one, -Name = never this one, Name:Number = this one with its own amount (0 = no limit). Names are prefab or in-game names.

Default value:

```
Wood, FineWood, RoundLog, ElderBark, YggdrasilWood, Blackwood, Resin
```

**GatherStone** — Default: `Stone, Flint, BlackMarble, Grausten`  
What the Stone job collects. Targets are only worked if they drop something on this list that is still under its amount; drops not on the list are left on the ground. A name with no number uses StoneLimit. List format (same for every list): * = everything, Name = this one, -Name = never this one, Name:Number = this one with its own amount (0 = no limit). Names are prefab or in-game names.

**GatherOre** — Default: see below  
What the Ore job collects. Targets are only worked if they drop something on this list that is still under its amount; drops not on the list are left on the ground. A name with no number uses OreLimit. List format (same for every list): * = everything, Name = this one, -Name = never this one, Name:Number = this one with its own amount (0 = no limit). Names are prefab or in-game names.

Default value:

```
CopperOre, TinOre, IronScrap, SilverOre, BlackMetalScrap, CopperScrap, FlametalOreNew, Obsidian
```

**ForageItems** — Default: see below  
What the Forage job picks up. Name:Number stops picking that item once there are that many (pack + chests near home). List format (same for every list): * = everything, Name = this one, -Name = never this one, Name:Number = this one with its own amount (0 = no limit). Names are prefab or in-game names.

Default value:

```
Raspberry,Blueberries,Cloudberry,Mushroom,MushroomYellow,MushroomBlue,MushroomMagecap,MushroomJotunPuffs,MushroomSmokePuff,Thistle,Dandelion,Carrot,Turnip,Onion,Barley,Flax,RoyalJelly,Honey
```

**WoodSkipTrees** — Default: `Oak`  
Standing trees the Wood job never fells, matched on part of the tree name (Oak, Birch, Beech, Pine, Fir, SwampTree, YggaShoot...). Logs and stumps are still cleaned up.

**BaseProtectRadius** — Default: 10 · Range: 0 – 50  
The Wood job won't fell a standing tree with anything players built within this many meters of its trunk. 0 = off.

**JunkCleanup** — Default: On  
When a gathering companion's pack is full and it has no home chest to unload into, it drops junk (materials and trophies that aren't on any gather list) to make room. Gear, tools, ammo, food, potions, seeds and anything on NeverDiscard are always kept.

**NeverDiscard** — Default: `Trophy`  
Items the junk clean-up never drops, matched on part of the item name.

**OverweightThreshold** — Default: 298 · Range: 50 – 500  
The weight (BASE value) at which a gathering companion - wood, stone, ore, forage and the farm plots - stops picking things up and heads home to unload. Items that raise carrying capacity add on top of this value, as in MaxCarryWeight: with the Megingjord in its bag (+150) and this at 298, it only heads home around 448. Hunt and Fish don't use this limit (they have their own bag-full settings).

## Hunting

The Hunting job.

**Prey** — Default: `*`  
What the Hunt job goes after: creatures that drop meat (and birds, see Birds). Name:Number with a creature or a meat name gives that meat its own target instead of MeatTarget / FeatherTarget. Tamed animals, bosses and companions are never hunted. The names to use are listed in OfflineCompanionsReborn.Hunting.txt in the config folder (written at startup). List format (same for every list): * = everything, Name = this one, -Name = never this one, Name:Number = this one with its own amount (0 = no limit). Names are item/creature names (prefab or in-game name).

**MeatTarget** — Default: 100 · Range: 0 – 10000  
Hunt a creature only while one of the meats it drops is under this many (counting the companion's pack and the chests near its home). Leather, hides and other drops are picked up as a bonus. 0 = no limit.

**Range** — Default: 40 · Range: 10 – 200  
With Follow off: how far (meters) from where it is hunting (home if it's near home, otherwise where the job was started) a companion looks for prey.

**FollowRange** — Default: 40 · Range: 5 – 100  
With Follow on: prey has to be within this many meters of you. With nothing that close the companion just follows you.

**ChaseExtra** — Default: 15 · Range: 0 – 100  
How much further than Range / FollowRange a companion chases prey that runs off before letting it go.

**BagFull** — Default: 0.9 · Range: 0.3 – 1  
With Follow off and home set: when the pack is this full (by weight, or out of free slots) the hunter walks home and puts its catch away, then goes back out.

**Birds** — Default: On  
Also shoot birds (crows, gulls, modded birds) for their feathers: when one lands within range, the hunter sneaks up and shoots it with a bow or crossbow (only if it has one with ammo), then picks up the drops. Birds on the Prey list's exclusions (-Crow, -Seagal) are left alone.

**FeatherTarget** — Default: 100 · Range: 0 – 10000  
Shoot birds only while what they drop (feathers) is under this many, counting the pack and the chests near home. 0 = no limit.

**Sneak** — Default: On  
Sneak up on skittish game (deer, hares...) that hasn't noticed anyone yet, like a player: crouched and not running, so it doesn't bolt. Crouching companions are quieter and harder to spot, like a sneaking player (light and Sneak skill count).

**ThrowSpear** — Default: On  
A hunter holding a spear throws it at skittish game (deer, hares...) from a distance, like a player, then walks over and picks it back up. Only when it has another weapon in its pack to fight on with.

**ThrowRange** — Default: 12 · Range: 4 – 25  
How far (meters) a hunter throws its spear from. Further throws drop short more often.

**ThrowStandoff** — Default: 10 · Range: 3 – 25  
With a spear to throw at skittish game, a hunter sneaks only this close (meters), stops and throws, instead of walking right up to stab it. Never more than ThrowRange less 1 m.

**SneakFrom** — Default: 20 · Range: 5 – 60  
Further than this (meters) from prey that hasn't noticed it, a hunter walks (no running) instead of sneaking, like a player - sneaking all the way costs a lot of stamina. Closer, it crouches and sneaks.

**SwitchCloserBy** — Default: 5 · Range: 0 – 50  
Before its first strike, a hunter switches to another animal that is at least this many meters closer than its prey (and see SwitchShare). 0 = never switches.

**SwitchShare** — Default: 0.6 · Range: 0.1 – 1  
...and that is at most this share of the distance to its prey (0.6 = 60%).

**SneakRestStamina** — Default: 0.3 · Range: 0 – 0.9  
Sneaking costs stamina, like for a player (standing still doesn't). When a hunter sneaking up on prey drops below this share of its stamina, it stops where it is, stays crouched and gets its breath back. 0 = never stops.

**SneakResumeStamina** — Default: 0.6 · Range: 0.1 – 1  
A hunter resting while sneaking carries on once its stamina is back above this share.

**ChaseRunStamina** — Default: 0.3 · Range: 0 – 1  
Chasing prey that has bolted, a hunter only runs while its stamina is above this share; below it, it jogs and lets its stamina come back, like a player. 0 = always runs.

## Fishing

The Fishing job.

**ScanRadius** — Default: 30 · Range: 5 – 100  
How far (meters) from where the Fish job was started the companion looks for fish to catch. It stays around there and fishes the water nearby.

**Fish** — Default: `*`  
Which fish to catch. Name:Number gives that fish its own target instead of FishTarget. The fish names are listed in OfflineCompanionsReborn.Fishing.txt in the config folder (written at startup). List format (same for every list): * = everything, Name = this one, -Name = never this one, Name:Number = this one with its own amount (0 = no limit).

**FishTarget** — Default: 100 · Range: 0 – 10000  
Catch a kind of fish only while there are fewer than this many (pack + chests near home). 0 = no limit.

**CastWait** — Default: 60 · Range: 10 – 600  
Seconds to wait for a bite before reeling in and trying again (a fish that swam off, the wrong spot).

**BagFull** — Default: 0.9 · Range: 0.3 – 1  
Near home: when the pack is this full (by weight, or out of free slots) the companion walks home and puts the catch away, then goes back to fishing.

## Farming

The Farming job.

**Range** — Default: 30 · Range: 5 – 100  
How far (meters) around itself a farming companion looks for crops to pick, farm zones to plant and chests.

**PlantTilledGround** — Default: On  
Ground you've tilled with a cultivator (outside your farm zones) counts as an Any zone: a farming companion plants whatever seeds it has on it, on the tilled ground only. Off = it only plants in the farm zones you set.

**UnreachableSpotMemory** — Default: 600 · Range: 30 – 3600  
How long (seconds) a farmer leaves a planting spot alone after finding it can't walk there (e.g. tilled ground on the other side of a wall). It tries it again after this.

**PlantSpacing** — Default: 0.75 · Range: 0.25 – 3  
The least space (meters) between plants. A plant that needs more room to grow gets that instead.

**SeedMinimum** — Default: 20 · Range: 0 – 1000  
The fewest of each plantable item (carrot, turnip, onion seeds, barley, flax and so on) the farmer keeps, counting its pack and chests near its home. It never plants seeds if that would take them below this. Barley, flax and other crops that grow from themselves are still planted, and the farmer keeps this many in its pack instead of storing them.

**SeedTarget** — Default: 100 · Range: 0 – 5000  
While a seed is under this number, the farmer grows seed plants (planting carrots, turnips, onions...) alongside its regular crops to build the stock back up. Below SeedMinimum it grows only seed plants for that crop. At or above this number it grows only crops.

**SeedCarry** — Default: 40 · Range: 1 – 200  
How many of each plantable item the farmer carries. When it opens a chest for seeds it takes up to this many of every type on the PlantAny list (and vegetables for seed plants while that seed is under SeedTarget), so it can fill the fields without walking back for each type. It keeps up to this many in its pack instead of storing them.

**PlantGroupSize** — Default: 4 · Range: 1 – 100  
How many of one plant the farmer puts in before switching to the next type, so a mixed field ends up in small blocks instead of one crop. 1 = fully mixed.

**NextCropRange** — Default: 3 · Range: 0 – 10  
After picking a crop, the farmer goes straight to the next ripe crop within this many meters, like working down a row, instead of pausing and scanning the whole field. 0 = always pause and scan.

**PlantAny** — Default: see below  
What the farmer plants in farm zones set to Any crop. * = every crop that grows on cultivated ground (tree saplings are never planted in an Any zone unless named). Name:Number stops planting that crop once there are that many of what it gives (carrots for CarrotSeeds, barley for Barley...), counting pack and chests near home. Items added by other mods work too. List format (same for every list): * = everything, Name = this one, -Name = never this one, Name:Number = this one with its own amount (0 = no limit). Names are item/creature names (prefab or in-game name).

Default value:

```
CarrotSeeds, TurnipSeeds, OnionSeeds, Barley, Flax, MushroomJotunPuffs, MushroomMagecap, VineberrySeeds
```

## Smelting

The Smelting job.

**Range** — Default: 25 · Range: 5 – 100  
How far (meters) around itself a smelting companion looks for smelters, kilns, furnaces and chests. Near home it also uses the chests around home ([General] HomeTripRange).

**MaxCarryOre** — Default: 20 · Range: 1 – 100  
The most ore (or other input) a companion carries to a smelter in one trip.

**MaxCarryFuel** — Default: 40 · Range: 1 – 100  
The most fuel (coal, wood) a companion carries to a smelter in one trip.

**UsePiles** — Default: On  
When the chests have none of what a smelter or kiln needs (wood for the kiln, fuel...), the Smelt job takes down a pile of it (Stack of Wood...) with a hammer, like a player, and uses that. Needs a hammer in its pack.

## Cooking

The Cooking job.

**Range** — Default: 25 · Range: 5 – 60  
How far around itself (meters) a cooking companion looks for cooking stations, fermenters and chests. Near home it also uses the chests around home ([General] HomeTripRange).

**IngredientReserve** — Default: 20 · Range: 0 – 1000  
A raw ingredient is only used while the chests in range (plus the companion's bag) hold more than this many, so there is always some left for you to cook something yourself.

**FoodTarget** — Default: 20 · Range: 1 – 1000  
Cook a food until the chests in range hold this many of it.

**BatchSize** — Default: 5 · Range: 1 – 50  
How many of a food (or mead base) to make in one go, like a player cooking a batch. Still stops at FoodTarget / MeadBaseTarget and never goes into the ingredient reserve. On a cooking station or oven it's also limited by the free slots.

**Foods** — Default: `*`  
What may be cooked. Name:Number is that food's own target instead of FoodTarget. The names to use are listed in OfflineCompanionsReborn.Cooking.txt in the config folder (written at startup). List format (same for every list): * = everything, Name = this one, -Name = never this one, Name:Number = this one with its own amount (0 = no limit). Names are item/creature names (prefab or in-game name).

**StoreWithCookedFood** — Default: On  
Put cooked food in a chest that already holds that food; if there is none, one that holds other cooked food; otherwise the chest nearest the cooking area. Off = the nearest chest with room.

**MakeMead** — Default: On  
Brew mead: make mead bases and fill fermenters with them. Off = food only (mead that has already finished in a fermenter is still tapped).

**MeadBaseTarget** — Default: 10 · Range: 0 – 1000  
Brew a mead base (one in MeadFocus) until the chests in range hold this many of it.

**MeadTarget** — Default: 20 · Range: 0 – 1000  
Keep fermenters going with a mead base until the chests in range hold this many of the finished mead.

**MeadFocus** — Default: see below  
Mead bases to brew FIRST, by the last boss beaten, to get ready for the next one (other mead bases you know are still brewed after food, within MeadBaseTarget / MeadTarget). Format: boss: Base, Base; boss: ... in the order you beat them. A boss is its name or part of it (eikthyr, gdking = The Elder, bonemass, dragon = Moder, goblinking = Yagluth, queen, fader, kall = Kall Fimbulbringer, or a modded boss); start = none beaten yet. The bosses your game has, and the mead base names, are listed in OfflineCompanionsReborn.Cooking.txt in the config folder.

Default value:

```
start: MeadBaseHealthMinor, MeadBaseStaminaMinor;
eikthyr: MeadBaseHealthMinor, MeadBaseStaminaMinor, MeadBasePoisonResist;
gdking: MeadBasePoisonResist, MeadBaseHealthMedium, MeadBaseStaminaMinor;
bonemass: MeadBaseFrostResist, MeadBaseHealthMedium, MeadBaseStaminaMedium;
dragon: MeadBaseHealthMedium, MeadBaseStaminaMedium, MeadBaseTasty, BarleyWineBase;
goblinking: MeadBaseHealthMajor, MeadBaseStaminaMedium, MeadBaseEitrMinor, MeadBaseLightfoot;
queen: BarleyWineBase, MeadBaseHealthMajor, MeadBaseStaminaLingering, MeadBaseHealthLingering, MeadBaseEitrMinor;
fader: MeadBaseFrostResist, MeadBaseHealthMajor, MeadBaseHealthLingering, MeadBaseStaminaLingering, MeadBaseEitrMinor;
kall: MeadBaseFrostResist, MeadBaseHealthMajor, MeadBaseHealthLingering, MeadBaseStaminaLingering, MeadBaseEitrMinor
```

**FuelLevel** — Default: 0.5 · Range: 0.1 – 1  
Cook job: keeps the fires under the cooking spots it uses (spit, iron cooking station, cauldron) and the oven at this share of full (0.5 = 50%). When one drops below half of that it tops it back up. Only enough to cook with - Repair and Restock fills fires further if it's on.

**UsePiles** — Default: On  
Cook job: with no fuel in the chests, take down a pile of it (Stack of Wood...) with a hammer, like a player. Needs a hammer in its pack.

**UseCauldron** — Default: On  
Cook job: use the cauldron (soups, stews and other crafted food).

**UsePrepTable** — Default: On  
Cook job: use the food preparation table (dough, raw pies and the like).

**UseMeadKetill** — Default: On  
Cook job: use the mead ketill (mead bases).

**UseCookingStation** — Default: On  
Cook job: use the cooking station / spit over a fire.

**UseIronCookingStation** — Default: On  
Cook job: use the iron cooking station.

**UseOven** — Default: On  
Cook job: use the stone oven (bread, pies; it also gets wood).

**UseFermenter** — Default: On  
Cook job: use fermenters (filling them with mead bases and tapping the mead).

**UseBeehives** — Default: On  
Cook job: use beehives (collecting honey).

**UseOther** — Default: On  
Cook job: use any other cooking station, e.g. one added by another mod.

## Repair and Restock

The Repair and Restock job (taking care of your base).

**Repair** — Default: On  
Repair and Restock: repair damaged building pieces near home with a hammer, like a player (one swing per piece; the piece's crafting station has to be in range and a ward has to allow it; costs stamina and hammer wear). A hammer is fetched from a chest if it has none.

**RepairBelow** — Default: 0.75 · Range: 0.1 – 1  
Repair pieces below this much health (0.75 = 75% or less; 1 = any damage at all). Each repair is one swing that fixes the piece fully, like a player's.

**Reach** — Default: 4.5 · Range: 2 – 6  
How far (meters, from the eyes) the companion can reach a piece to repair it. A player reaches about 5 m.

**Restock** — Default: On  
Repair and Restock: keep fires, torches and braziers near home fuelled, one piece of fuel at a time like a player. Fuel comes from the pack or is fetched from a chest; what's left over goes back in a chest. The Refuel... settings below pick which kinds.

**FuelBelow** — Default: 0.8 · Range: 0.1 – 1  
Top up a fire when its fuel is below this share of full (0.8 = 80%). It's filled all the way up.

**RefuelFires** — Default: On  
Restock: campfires, fire pits and bonfires.

**RefuelHearths** — Default: On  
Restock: hearths.

**RefuelBraziers** — Default: On  
Restock: braziers (standing and hanging).

**RefuelStandingTorches** — Default: On  
Restock: standing torches (wood, iron, green, blue, mist).

**RefuelWallTorches** — Default: On  
Restock: sconces and wall torches.

**RefuelHotTubs** — Default: On  
Restock: hot tubs.

**RefuelOther** — Default: On  
Restock: anything else that burns fuel and isn't one of the kinds above (lanterns, the jack-o-turnip, things added by other mods).

**Honey** — Default: On  
Repair and Restock: empty the beehives in its area and put the honey away in the chest that already holds the most honey.

**HoneyAt** — Default: 0.5 · Range: 0.1 – 1  
Empty a beehive once it's at least this full (0.5 = half full, 1 = only when full).

**Sort** — Default: On  
Repair and Restock: sort the chests in its area (around home, or around where the job was started), like with like: each kind of item is carried to the chest that already holds the most of it, and part-stacks are merged.

**SortItems** — Default: `*`  
Which items get sorted. Comma-separated: * = everything, Name = this one, -Name = never this one (e.g. *, -Coins, -Wood keeps coins and wood where they are). Prefab or in-game names.

**SortGear** — Default: Off  
Also sort things that don't stack (weapons, armor, tools). Off: only stackable items are moved.

**Piles** — Default: On  
Repair and Restock: stack wood, stone and the like from the chests into piles (the game's Stack of Wood, Stone Pile, Fine Wood Stack...), built with a hammer like a player. Only next to piles of the same kind you already built - it never starts a new pile somewhere on its own.

**PileKeep** — Default: 100 · Range: 0 – 10000  
How many of each material stay in the chests for building: what's above this goes into piles, and when the chests drop below it a pile is taken down (with the hammer, like a player) and the material put back in the chests.

**PileItems** — Default: `*`  
Which materials go into piles. Comma-separated: * = every material you have piles of, Name = this one, -Name = never this one, Name:Number = keep this many of it in the chests instead of PileKeep. Prefab or in-game names (Wood, Stone, FineWood, RoundLog...).

**AwayRadius** — Default: 30 · Range: 5 – 100  
Started away from home (or with no home set): how far (meters) from where the job was started it repairs, restocks and sorts. Started near home, the range is [General] HomeTripRange around home.

**WorkTime** — Default: 1 · Range: 0.3 – 5  
Seconds per action: a repair swing, opening a chest. Adding fuel takes a bit over half of this per piece. (The game won't repair the same piece more than once a second.)

**GiveUp** — Default: 90 · Range: 20 – 600  
Seconds it keeps trying to get within reach of a piece, fire or chest before leaving it for a few minutes.

**OrderWait** — Default: 30 · Range: 0 – 600  
After you give the companion an order (send it somewhere, sit by a fire...), it waits this many seconds once the order is done before going back to its chores.

## Idle

What a companion does when its job has nothing to do.

**Enabled** — Default: On  
When its job has had nothing to do for a while (and it isn't following you), a companion idles near its job instead of standing still: small chores, sitting down, wandering about, chatting. It stops the moment the job has work again.

**StartAfter** — Default: 10 · Range: 1 – 300  
Seconds with nothing to do before it starts idling.

**Radius** — Default: 8 · Range: 2 – 40  
How far (meters) from where its job left it a companion wanders, sits or looks for company while idle.

**Chores** — Default: On  
While idle, do small chores nearby: repair damaged pieces, top up fires, collect honey, put away what it carries (uses the Repair and Restock settings).

**ChoreRadius** — Default: 15 · Range: 2 – 60  
How far (meters) it looks for idle chores.

**Sit** — Default: On  
While idle, sit down for a while - on a free chair or bench nearby, else on the ground.

**Wander** — Default: On  
While idle, stroll about nearby and look over stations and chests.

**Socialize** — Default: On  
While idle, go over to you or another companion nearby and chat now and then.

**SpeechInterval** — Default: 300 · Range: 0 – 3600  
At most every this many seconds it says why it's idle (and chats while socializing about half as often). 0 = never.

## Speech

How often companions speak and how long the lines stay on screen.

**Chatter** — Default: Off  
Companions talk to themselves now and then when nothing is happening: a line about a fight nearby, being too heavy, being hungry, worn gear, no bed, the job they're on, or just following you. Off (Phase 3.3c) = no random speech at all. They still speak when something actually happens: an order (chat or voice), a change of mode ("On it!"), the AI replies, a fish caught, a farming/repair/smelting milestone, a tombstone found, unloading near home. The idle lines are separate ([Idle] SpeechInterval). Turn it on to go back to the old behaviour.

**MinInterval** — Default: 15 · Range: 5 – 120  
Shortest time (seconds) between a companion's lines when nothing is happening. Only used while Chatter is on.

**MaxInterval** — Default: 30 · Range: 10 – 300  
Longest time (seconds) between a companion's lines when nothing is happening. Only used while Chatter is on.

**Enabled** — Default: On  
Companions say things at all (replies to your orders, what they're doing, problems, chatter). Off = silent.

**FarAwayMessages** — Default: On  
When a companion runs into a problem (needs a tool, can't get somewhere...) while you're out of earshot (further than CullDistance), it shows as a message in the top left corner with its name.

**AnnounceDeath** — Default: On  
When one of your companions dies, say so in the middle of the screen with where it happened (biome, how far from you), wherever you are.

**Duration** — Default: 5 · Range: 1 – 15  
Seconds a line of speech stays on screen.

**SayCooldown** — Default: 3 · Range: 1 – 30  
Least time (seconds) between two lines from any companion, so they don't all talk at once.

**CullDistance** — Default: 20 · Range: 5 – 60  
Speech from companions further away than this (meters) isn't shown.

## Advanced

Fine tuning of timers and distances. Most players never need to touch it.

**RosterSaveInterval** — Default: 60 · Range: 10 – 600  
Seconds between saves of your companions to the roster (their gear and settings), used to recover a lost companion.

**GatheringSearchRadius** — Default: 50 · Range: 5 – 100  
Gathering: how far (meters) around itself a companion looks for trees, rocks and plants when it's following you or sent to one with the command key (with Follow off it's [Gathering] Range). Phase 3.3c: 50, was 30 - while following, the companion can be 25 m from you ([Gathering] FollowRange) and the target up to 25 m from you on the other side, so the ring it searches has to reach about 50 m.

**GatheringScanInterval** — Default: 4 · Range: 1 – 30  
Gathering: seconds between looks for the next tree, rock or plant.

**GatheringDropRadius** — Default: 12 · Range: 2 – 20  
Gathering: how far (meters) around a felled tree or broken rock the drops are picked up. Phase 3.3c: 12, was 8 - a tree can fall (or a rock spread out) further than 8 m, and the drops were being left behind.

**GatheringSkipTime** — Default: 60 · Range: 5 – 300  
Gathering: seconds a tree, rock or plant it couldn't get to is skipped.

**FarmScanInterval** — Default: 4 · Range: 1 – 30  
Farming: seconds between looks for crops to pick and ground to plant.

**FarmUseDistance** — Default: 2 · Range: 1 – 5  
Farming: how close (meters) a companion has to be to a crop or chest to use it.

**SmeltScanInterval** — Default: 3 · Range: 1 – 30  
Smelting: seconds between looks at the smelters and chests.

**SmeltUseDistance** — Default: 2.5 · Range: 1 – 5  
Smelting: how close (meters) a companion has to be to a smelter to put things in or take them out.

**GearRepairScanInterval** — Default: 5 · Range: 1 – 30  
Gear repair: seconds between checks of a companion's gear for wear.

**GearRepairTickInterval** — Default: 0.8 · Range: 0.1 – 5  
Gear repair: seconds per item repaired at the station.

**GearRepairUseDistance** — Default: 2.5 · Range: 1 – 5  
Gear repair: how close (meters) a companion has to be to the station to repair.

**RestockScanInterval** — Default: 5 · Range: 1 – 60  
Repair and Restock: seconds between looks for something to repair, refuel or sort.

**AttackCooldown** — Default: 0.225 · Range: 0.1 – 2  
Combat: least time (seconds) between a companion's attacks, on top of each weapon's own attack timing.

**MeleeTooClose** — Default: 0.3 · Range: 0 – 1.5  
Combat: closer than this (meters, from the edge of the target) a companion steps back before swinging a melee weapon - a stab or swing that starts inside the target misses, like a player standing on top of a boar.

**MeleeReachMargin** — Default: 0.2 · Range: 0 – 1  
Combat: a companion swings a melee weapon only once it is at least this much (meters) inside the weapon's reach, not right at the edge of it, where hits are hit-or-miss.

**BlockReactionTime** — Default: 0.2 · Range: 0 – 1  
Combat: seconds between an enemy starting a swing at a companion and the companion raising its guard.

**LogPlayerCombat** — Default: On  
Testing, with LogLevel on Detailed: write your own fighting to the log (lines starting [You]) - swings, guard, blocks and parries, enemy swings at you and how far you were - to help tune how companions fight. Will be removed once combat is tuned.

**FoodRegenInterval** — Default: 10 · Range: 1 – 60  
Food: seconds between health regeneration ticks from food (a player's is 10).

**FoodCheckInterval** — Default: 1 · Range: 0.5 – 10  
Food: seconds between checks whether a companion should eat or drink.

**TombstoneScanInterval** — Default: 5 · Range: 1 – 30  
Death: seconds between looks for its own leftover tombstone nearby.

**LogLevel** — Default: `Normal` · Options: `Off`, `Normal`, `Detailed`  
How much the mod writes to the BepInEx log. Off: warnings and errors only. Normal: the main events (orders, deaths, portals, jobs starting and stopping, problems). Detailed: everything the companions think and do, plus the diagnostics - turn this on when sending a log with a bug report. Lower levels cost nothing: the lines aren't even put together.

**TombstoneNavTimeout** — Default: 120 · Range: 10 – 600  
Death: seconds a companion keeps trying to get to its tombstone before giving up.

**UiDiag** — Default: Off  
UI diagnostics: when on, writes to the log what is in front of the controls of the F8 panel and the AI panel (once per session, the first time each panel is opened): how many click targets each panel has (the controls and the ones that are there on purpose), every active layer in the scene (name and order) and, for each canvas above the panel, the targets that cover the controls column. It is what you turn on when a click or the UI looks odd (a button that doesn't answer, text on top of something, a swallowed click): the log says who is in front. Off (default) writes nothing and costs nothing; on, the log grows a lot (one summary line per panel, plus one line per suspect target). It changes nothing in the game - it only reports.

## AI

The AI layer of the project: text and voice orders, personality, memory, speech recognition and the companion's voice.

**Enabled** — Default: On  
Phase 3.1: type an order in the game chat ("collect 20 wood", "follow", "stop") and the companions carry it out. The order is interpreted by an LLM (Groq by default, or a local server, or OpenRouter) through an OpenAI-compatible API. Off = nothing is sent to the model.

**BaseUrl** — Default: `https://api.groq.com/openai/v1`  
Base URL of the OpenAI-compatible server. Default: Groq (https://api.groq.com/openai/v1), which asks for the ApiKey - the free key is at console.groq.com/keys. Alternatives: LM Studio (http://127.0.0.1:1234/v1) or Ollama (http://127.0.0.1:11434/v1) to run everything locally - with those two use 127.0.0.1 instead of localhost (on Windows localhost resolves to IPv6 first and the server only listens on IPv4, which costs about 2.5 s per order); or OpenRouter (https://openrouter.ai/api/v1), which works but the free tier is usually much slower. /chat/completions is added automatically. This is a text option: edit it in the .cfg file.

**Model** — Default: `openai/gpt-oss-20b`  
Name of the model sent to the server. Default: openai/gpt-oss-20b (Groq, free tier). Others: openrouter/free on OpenRouter; on LM Studio use the name the server lists (e.g. qwen2.5-3b-instruct). Text option: edit it in the .cfg file.

**ApiKey** — Default: ``  
API key, if the server asks for one. On Groq it is required (the free one is at console.groq.com/keys) and on OpenRouter too (openrouter.ai/keys). Leave it empty for LM Studio/Ollama running on your machine. Text option: edit it in the .cfg file. Never put a key in a shared config.

**Temperature** — Default: 0.1 · Range: 0 – 1  
How much the model may improvise. Keep it low so the same order always gives the same JSON.

**TimeoutSeconds** — Default: 30 · Range: 1 – 300  
Seconds to wait for the model before giving up. The request runs in the background, so a slow model never freezes the game.

**Prefix** — Default: ``  
Only chat messages that start with this go to the model (for example /c). Empty = every chat message is sent. Text option: edit it in the .cfg file.

**ShowReplyInChat** — Default: On  
Shows the model's raw reply in the chat, so you can see what it understood. Useful while testing; turn it off for a cleaner chat.

**CooldownSeconds** — Default: 3 · Range: 0 – 60  
Minimum time (in seconds) between two orders, so a burst of chat messages does not flood the model.

**TargetRange** — Default: 40 · Range: 5 – 200  
Phase 3.6: how far from you (in metres) an order that names a creature searches for it ("attack the greydwarf"). The creature you are aiming at always counts, however far away it is. 40 m is the same leash at which the combat code gives up on a target.

**Personality** — Default: ``  
Phase 3.5: your companion's personality in your own words - how it speaks and behaves ("speaks like a loyal, blunt viking", "a sarcastic mage who complains about the cold"). This goes into the model's instructions, so the companion answers in that tone. When filled in it wins over PersonalityPreset. Up to 600 characters. Text option: edit it in the .cfg file.

**PersonalityPreset** — Default: ``  
Phase 3.5: a ready-made personality, written in Portuguese inside the mod: viking (loyal and fearless), mago (old and sarcastic), sarcastico (dry jokes), guia (patient and didactic), or custom (uses Personality). Empty or unknown = the default (a loyal, practical companion). Text option: edit it in the .cfg file.

**Memory** — Default: 4 · Range: 0 – 12  
Phase 3.5: how many recent exchanges (one message from you + one from the companion) the companion remembers and sends along with the next message, to keep the thread of the conversation. 0 = no memory. Kept per companion, in memory only (nothing is saved in the world) and forgotten when the companion dies or is dismissed. Every exchange makes the prompt the model has to read bigger, so keep it small for faster replies (Phase 3.3b: 4, was 6).

**Conversation** — Default: On  
Phase 3.5: when the message is NOT an order ("good morning", "how are you?", "what did you think of my house?"), the companion answers as conversation: it says the reply out loud (speech bubble) and shows it in the chat. Orders keep working exactly as before. Off = only orders are understood, as in Phase 3.1.

**StructuredOutput** — Default: On  
Asks the server to force the JSON format (response_format/json_schema). It keeps the model's reply always valid and is what allows small, fast models (e.g. a 3B) to be used without them answering loose text. Turn it off if your server does not support it - the mod falls back to the old format on its own and says so in the log.

**SttTest** — Default: Off  
SPIKE (test): runs the in-process Whisper.net speech recognition test by itself, about 25 seconds after the plugin loads, and writes the result to the log. Off = only the "modia_stt_teste" console command runs it. It is a simple test: it does not listen to the microphone and changes no companion behaviour.

**SttModel** — Default: `whisper\ggml-small-q5_1.bin`  
Whisper model the speech recognition loads (the voice orders AND the "modia_stt_teste" console command), relative to the plugin folder or an absolute path. Default whisper\ggml-small-q5_1.bin (181 MB, Phase 3.3b - much better in Portuguese than the old whisper\ggml-base-q5_1.bin, 57 MB, which is no longer shipped - download it into the whisper folder and point this setting at it if you want the faster, less accurate one: measured on a 16-core CPU with 8 threads, 638 ms per 2 s clip with base against 2187 ms with small, and the time barely changes with the clip length because the encoder always works on a 30 s window). Text option: edit it in the .cfg file.

**SttWavTest** — Default: `whisper\jfk.wav`  
SPIKE (test): the WAV file to transcribe, relative to the plugin folder (or an absolute path). It must be PCM WAV (16 kHz mono is ideal). Text option: edit it in the .cfg file.

**VoiceEnabled** — Default: On  
Phase 3.3: hold VoiceKey and speak, and the companion accepts what you said - the same orders you can type in the chat ("collect 10 wood", "follow", "attack the greydwarf"). The speech is recognised locally, inside the game, by Whisper.net with the SttModel model: nothing is sent anywhere. Off = the key does nothing (and, in Phase 3.3b, the key goes back to doing what the game did with it).

**VoiceKey** — Default: `V`  
Phase 3.3: push-to-talk key. Hold it while you speak, release it for the speech to be recognised and carried out. It only works with the game focused and no window open (inventory, map, menu, chat, console, the mod's own panels). NOTE (Phase 3.3b): V is also the game's "Toggle Auto-Pickup". While VoiceEnabled is on, the mod blocks that toggle when the game's auto-pickup is bound to this same key, so the key stays free for talking - rebind auto-pickup in the game's own controls screen (or pick another key here) if you want it back.

**VoiceLanguage** — Default: `auto`  
Multilanguage: language of the speech, as a Whisper code (pt, en, es, fr) - or "auto" (the default) to follow the game language, which is what makes the companion understand orders in whatever language you are playing. An explicit code always wins. Text option: edit it in the .cfg file.

**VoiceMaxSeconds** — Default: 10 · Range: 1 – 30  
Phase 3.3: longest recording (in seconds) for a voice order. Holding the key longer than this stops the recording and transcribes what was captured.

**VoiceSilenceThreshold** — Default: 0.01 · Range: 0 – 0.5  
Phase 3.3: how loud the audio must be (peak level of a 10 ms window, from 0 to 1) to count as speech when the silence at the start and the end of the recording is trimmed. Phase 3.3b: 0.01 (was 0.02) - at 0.02 the quiet start of a word was often cut, and that is what made short orders come back wrong. Raise it if your microphone picks up a lot of room noise.

**VoiceInitialPrompt** — Default: ``  
Phase 3.3b: initial prompt sent to Whisper before every transcription. It biases the vocabulary, and that is what makes short orders ("collect 10 wood") come out right instead of guessed. Empty (the default) = the orders vocabulary of the game language, written by the mod; anything you type here wins. Write "none" for no bias at all. Text option: edit it in the .cfg file.

**VoiceDevice** — Default: 0 · Range: 0 – 8  
Phase 3.3: which microphone to record from, as the index of the list the log prints at startup ([STT] Push-to-talk: ... 0: name, 1: name). 0 = the first one (the Windows default).

**VoiceShowText** — Default: On  
Phase 3.3: shows what the microphone heard ("Entendi: "collect 10 wood"") in the chat and on the HUD. Phase 3.3d: the transcription appears in the PLAYER's own bubble (not the companion's), so a wrong transcription is easy to spot - the one who answers is the companion, with its own bubble and VOICE. Off = the recognised text is only written to the log.

**PanelKey** — Default: `I`  
Phase 3.7: opens the AI panel in game. The AI tab has the free companion summoner, the speech volume and speed, the memory, the conversation and the push-to-talk key. Each companion has its own tab, with the name, the tier (reborn), the voice (with the Listen button) and ITS personality - the [AI] values below are the default of whoever has no choice of its own. Every change is saved right away. The default is I: neither the game nor the mod uses that key. The key is not stolen from a text box (the chat, the console and the search field are left in peace). Key option: edit it in the .cfg file or click it in the F8 panel.

**TtsEnabled** — Default: On  
Phase 3.4: the companion SPEAKS the replies out loud. The speech is synthesised on your computer by Kokoro-82M (via sherpa-onnx, inside the game, no child process and nothing leaving your machine), in the TtsVoice voice, and comes out where the companion is (not in the middle of the screen). Off = it only shows the bubble and the chat, as before. The first speech of every session waits for the model to load (~1 s, in the background, when you enter the world).

**TtsVoice** — Default: 0 · Range: 0 – 100  
Multilanguage: which Kokoro voice to use. 0 (the default) = automatic: the default voice of the game's language (42 = pf_dora in Portuguese, 3 = af_heart in English, 28 = ef_dora in Spanish, 30 = ff_siwis in French). An explicit id always wins while it belongs to the game's language: the Brazilian Portuguese voices are 42 = pf_dora (feminine), 43 = pm_alex (masculine) and 44 = pm_santa (masculine). A voice of another language is not used (it would speak with the wrong accent) - each companion's own voice follows the same rule.

**TtsSpeed** — Default: 1 · Range: 0.5 – 2  
Phase 3.4: how fast the companion speaks (1 = normal; above 1 speaks faster). It also makes the speech shorter.

**TtsThreads** — Default: 4 · Range: 1 – 16  
Phase 3.4: how many CPU cores the speech synthesis may use. Measured on this machine (Ryzen 7 5700X): 4 threads generate faster than real time (RTF ~0.83), so the speech is ready before the companion needs it; more threads do not speed it up and compete with the game.

**TtsVolume** — Default: 1 · Range: 0 – 1  
Phase 3.4: volume of the companion's voice (0 = muted, 1 = the game's normal volume).

**TtsModel** — Default: `tts\kokoro\model.int8.onnx`  
Phase 3.4: the Kokoro model the companion speaks with (relative to the plugin folder, or an absolute path). The voices (voices.bin), the tokens (tokens.txt) and espeak-ng-data are looked for in the SAME folder as the model. Text option: edit it in the .cfg file.

**TtsLanguage** — Default: `auto`  
Multilanguage: language of the spoken text, in the format Kokoro expects - or "auto" (the default) to follow the game language (pt-br, en-us, es, fr). This model is multi-lingual and REQUIRES a language: "pt-br" is Brazilian Portuguese ("pt" alone is European Portuguese), "en-us" and "en-gb" are the two English ones (the voice has to match it). An explicit code always wins. Text option: edit it in the .cfg file.

**TtsCache** — Default: On  
Phase 3.4: keeps the speeches already synthesised (text + voice + speed) to play them again instantly. The mod's fixed lines ("Ok: gathering wood (10).") come out instantly; the cache holds up to 32 MB of audio (the oldest sentences are dropped first). Off = every speech is synthesised again.

**TtsSpeakReplies** — Default: On  
Phase 3.4: speaks the model's reply out loud (the "reply" field). In a conversation it is its answer; in an order, when TtsSpeakModLines is off, it is the confirmation in its own way ("Deixa comigo.").

**TtsSpeakModLines** — Default: On  
Phase 3.4: speaks out loud the mod's own line that shows in the chat/HUD ("Ok: coletar madeira (10).", "Atacando o troll.", "Já temos o suficiente."). In an ORDER it wins over the model's reply (the voice follows what is written on the screen); turn it off to hear the model's reply instead. Nothing is spoken twice: with both options on, the line that is not spoken only goes to the log.

**TtsReverbZoneMix** — Default: 0 · Range: 0 – 1  
How much of the world's reverb zones goes into the companion's voice. 0 (default) = dry voice. Valheim has a global reverb zone (1000 m radius, 4 s tail) that made the companion's speech 'echo like in a cave' - the game's music uses 0 and the ambience 0.19. Raise it a little (e.g. 0.19) if you want a subtle 'space' in the voice. It applies to the next sentence, no restart.

**EcoDiag** — Default: Off  
Audio diagnostics: when on, writes to the log (once per session, on the first spoken sentence) the scene's AudioListener, the world's reverb zones and how the game's own sources are configured (mixer, reverb, bypass). Use it to investigate any sound problem with the companion. It does not change the sound - it only reports.
