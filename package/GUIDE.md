# AI Companions — Companion guide

How your companions work: what each job does, how to prepare and what to expect. If you were wondering why a companion did (or did not do) something, the answer is probably here.

Most of the numbers below are the defaults and can be changed in the settings (see `CONFIGURATION.md`, or press `F8` in game).

## Contents

1. [First steps](#first-steps)
2. [Talking to the companion](#talking-to-the-companion)
3. [The command ring](#the-command-ring)
4. [Point and command](#point-and-command)
5. [Home](#home)
6. [Following you](#following-you)
7. [Jobs](#jobs)
   - [Wood, stone, ore and foraging](#wood-stone-ore-and-foraging)
   - [Hunting](#hunting)
   - [Fishing](#fishing)
   - [Farming (and how to mark the farm zones)](#farming)
   - [Smelting](#smelting)
   - [Cooking](#cooking)
   - [Repairing and restocking](#repairing-and-restocking)
8. [When there is nothing to do](#when-there-is-nothing-to-do)
9. [Tools, chests and load](#tools-chests-and-load)
10. [Combat](#combat)
11. [Food, stamina and health](#food-stamina-and-health)
12. [Sleeping, resting and beds](#sleeping-resting-and-beds)
13. [Death and return](#death-and-return)
14. [Movement](#movement)
15. [Skills](#skills)
16. [Multiplayer](#multiplayer)
17. [Troubleshooting](#troubleshooting)

---

## First steps

- The first time you enter a world with a character, a companion is offered to you for free.
- Other ways of getting more companions are coming.
- A new companion already follows you. Give him equipment and food: he equips the best weapon, shield and armour he is carrying, and eats from his own inventory.
- To give him a job, open the command ring (below) and pick one. Most jobs work better after you set his **home**.

## Talking to the companion

**Tap** the interact key (`E`) while looking at a companion to open his **panel**:

- his inventory and equipment
- his food
- his name (you can rename him)

Drag items in and out like in a chest. Everything you give him is his: he keeps it and does not put it in a chest.

## The command ring

**Hold** the interact key (`E`) while looking at a companion to open his **command ring**. Take the mouse (or the analogue stick) to a slice and click.

| Slice | What it does |
|:---|:---|
| **Follow** | Turns following you on or off |
| **Wood / Stone / Ore / Gather** | Gathering jobs (the **Gather** slice is foraging) |
| **Hunt** | Hunting job |
| **Fish** | Fishing job |
| **Plant** | Farming job (right-click to open the farm zone editor) |
| **Smelt** | Smelting job |
| **Cook** | Cooking job and mead preparation |
| **Repair and Restock** | Takes care of your base |
| **Set Home** | Marks the place where the companion is as home |
| **Wander** | Following you: on = he moves and looks around when you stop; off = he stays in one place, standing still |
| **Pick up** | Picks up items on the way and after kills (like auto-pickup) |
| **Command** | Whether this companion answers the command key (point and call) |
| **Dismiss** | Removes the companion (asks first) |

Picking a job again, or picking **Follow**, turns the job off. The **inner ring** has the [combat stances](#combat).

**Jobs and Follow:**
- **Wood, Stone, Ore, Gather and Hunt** can run with Follow on or off. With Follow on, they only go after what is near you as you walk. With Follow off, they work an area.
- **The other jobs** (Fish, Plant, Smelt, Cook, Repair and Restock) turn Follow off when you pick them, and turn it back on when the job ends.

## Point and command

Look at something and press the **command key** (`Z`). The companions nearby do what makes sense for what you are pointing at:

| Point at… | They… |
|:---|:---|
| An enemy | Attack it |
| A tree, rock or ore | Gather |
| A cart | Hitch it and pull (point again to let go) |
| A ship | Board it and sit somewhere |
| A bed | Go to sleep in it (point again to wake them up) |
| A fire | Sit next to it (point again to stand up) |
| A closed door | Open it |
| A chest | Store what they gathered |
| A workbench | Repair the equipment there |
| His tombstone | Go and get his things |
| The ground | Walk there and wait |

**Ships.** When you're aboard a ship close by, following companions get on too (Follow > AutoBoard): they jump aboard from a dock or the shore (from where you jumped, or the nearest spot in jump reach), use the ship's ladder (swimming to it if they have the stamina), or follow your own steps aboard. They walk across the deck to a free seat, or stand on deck near you if none is free. If one can't get aboard it says so and waits on dry land nearby; a swim to the ladder it doesn't have the stamina for (well fed, a companion has more) is one it won't try. A seated companion that is following you gets up again when you go ashore and walks, jumps or swims off after you.

- **Hold** the command key to call your companions to you.
- **Keep holding** for about 2 seconds to call **every** companion, however far away they are, including the ones with Command off.
- Only companions within 40 m answer a normal command or a call.

**Orders come first.** While a companion carries out your order, his job waits. Afterwards he goes back to what he was doing. A Repair and Restock companion stays where you sent him for 30 seconds before going back to his tasks.

## Home

**Set Home** (on the command ring) marks the place where the companion is as home and opens the home panel, where you can change the size of the home area.

Home matters because the companions:

- **claim a bed** inside the home area (shown with their name)
- **fetch tools** from the chests around home and give back the borrowed ones (take a tool out of his pack and he works without it on that job)
- **empty their bags** into the chests around home when they come back or change jobs
- **work around home** when a job is started near it
- **repair the equipment** a little earlier at home, where a workbench is nearby

"Home range" means within 60 m of the home point (the `HomeTripRange` option).

## Following you

- A companion who follows you picks his own place near you: beside, behind, sometimes in front. He moves when you get more than 10 m away (4 m in tight places).
- **Stand still for 45 seconds** and your companions come back and wait close to you.
- **Stop in a tight place** and they sit near you: on a free chair or bench, otherwise on the ground. Near home, they empty their bags first.
- If a companion falls too far behind (50 m) and cannot see you, he reaches you by appearing by your side.
- They go through portals with you.

## Jobs

Every job pauses for a fight, for your orders, for bedtime and to pick up the loot, and then continues. When a job runs out of work, see [When there is nothing to do](#when-there-is-nothing-to-do).

> **Companions only work while you are near.** The game unloads the parts of the world where there is no player, and everything in them freezes, companions included. A companion working at home while you are far away exploring is not working; he continues when you come back. In multiplayer, the area stays loaded while any player is near it.

### Wood, stone, ore and foraging

**Where they work**
- **Follow on:** only trees, rocks or plants within 25 m of you (20 m for foraging), as you walk. Nothing that close? They just follow you. Think of it as "we went past some raspberries, let's grab them".
- **Follow off:** they work an area. Around home, if the job started near it; otherwise, around where you left them, up to 60 m away. They keep working there after you leave.

**What they gather**
- Each job has a list of what to gather and how much to keep (500 of each by default, counting the companion's pack and the chests at home). When they already have enough, they stop.
- Things that are not on the list (like stone from a copper vein) stay on the ground.
- **Wood:** fells trees and cuts logs and stumps. By default he ignores **Oak** (the `WoodSkipTrees` option) and does not fell trees less than 10 m from anything you built. He also gets out of the way of falling trees.
- **Stone:** mines rocks to get stone, flint and the like.
- **Ore:** mines ore veins. The stone left over from the vein is thrown on a pile away from the ore, so it does not bury it.
- **Gather (foraging):** picks berries, mushrooms, flowers and the like. No tool needed.

**Early game**
If there is no bronze axe (or better) anywhere and not even a forge at home to make one, the Wood and Stone jobs also pick up branches, stones and flint from the ground. A companion with no axe and no pickaxe at all only picks up things from the ground.

**Tools**
They need an axe for Wood and a pickaxe for Stone and Ore. The missing tool is fetched from the chests at home. If the tool cannot cut what is there (a stone axe on an old tree, for example), you get a message.

**When the bag fills up**
- Near home: they unload into the chests at home.
- Far from home: they unload into a chest, cart or ship up to 30 m away.
- If there is nowhere to unload, they throw away the junk they do not need (nothing of equipment, food, tools or anything on their lists).

### Hunting

- **What:** creatures that drop meat, until there is enough of each type of meat (100 by default, counting the pack and the chests at home). Hide and other drops are picked up as a bonus. Tamed animals, bosses and other companions are never hunted.
- **Sneaking up:** they walk quietly up to skittish animals like deer and hares, and then sneak crouched over the last 20 m (Hunting > SneakFrom), so the animal does not bolt. Sneaking costs stamina like a player's: when it gets low, they stop, stay crouched and catch their breath (Hunting > SneakRestStamina). While sneaking, they wade through shallow water on foot instead of jumping. Chasing a prey that bolted, they only run while they have stamina to spare (Hunting > ChaseRunStamina).
- **Weapons:** a bow or crossbow with ammunition comes first. With neither, they hunt with a melee weapon: they sneak up and strike.
- **Thrown spears:** a hunter with a spear in hand throws it at skittish animals up to 12 m away (Hunting > ThrowRange), as long as he has another weapon in the pack to continue the fight — or, with the spear as his only weapon, when the prey has already noticed him (then he picks the spear up before continuing the hunt). He sneaks only up to the throwing distance (Hunting > ThrowStandoff, 10 m), stops and throws, and waits for the stamina the throw needs instead of walking up to stab. After the hunt he walks there and picks the spear back up. Turn this off in Hunting > ThrowSpear.
- **Birds:** with a bow or crossbow and ammunition, they shoot crows and gulls for the feathers.
- **Follow on:** they hunt up to 40 m from you (Hunting > FollowRange). **Follow off:** they hunt up to 40 m around where they were put to work.
- **Chase:** they chase a prey that runs, up to 15 m further, and then let it go.
- **Bag full:** with Follow off and a home set, they bring the game home and go back to the wild.

### Fishing

**How to prepare**
- Give the companion a **fishing rod** and **bait**, and leave him near the water.
- He fishes around the place where you started the job, up to 30 m away.

**How he fishes**
- He looks for fish within reach and picks a place on the bank.
- He uses the best bait he has for those fish, casts at the right distance and waits up to 60 seconds for a bite.
- When a fish bites, he strikes and then reels carefully, giving line when it stretches.

**Limits**
- Each fish type has a goal (100 by default, counting his pack and the chests at home). When it is reached, he leaves that type alone.
- Near home, he brings the catch home when the pack is almost full.

### Farming

The farmer **plants** in the farm zones you mark and **harvests** the ripe crops nearby.

**How to mark a farm zone**

1. **Cultivate the ground first**, with a cultivator, as you would to plant yourself. The farmer only plants on cultivated ground, and he does not cultivate the ground himself yet.
2. **Open the farm zone editor**, in one of two ways:
   - Stand near your farmer and press `Left Alt` + `Z`. That opens the editor of the nearest companion.
   - Or right-click **Plant** on his command ring.
3. A green square shows where the zone would be. Controls:

   | Mouse and keyboard | Controller | Action |
   |:---|:---|:---|
   | Left button | A | Places the zone |
   | Right button on a zone | X | Removes it |
   | Mouse scroll | Bumpers | Makes it bigger or smaller (from 4 m to 30 m per side) |
   | Hold right button + scroll | D-pad | Rotates the zone |
   | Esc (or `Left Alt` + `Z` again) | Start | Closes the editor |

4. After placing a zone, pick its **crop**, or **Any Crop**.
5. Each companion can have up to **4 zones**. Then put the companion on the **Plant** job.

**Planting**
- **A zone with a crop:** the farmer plants that crop there.
- **An "Any Crop" zone:** he plants from the `PlantAny` list (carrot, turnip, onion, barley, flax, mushrooms and vine berries by default). He plants in small blocks of 4 of each, so the field ends up mixed but organized. Tree saplings are never planted in an "any" zone.
- He spaces the plants so each one has room to grow, and does not plant inside a protected area that does not let him in.

**Seeds**
- He carries up to 40 of each seed, taken from the chests.
- He always keeps at least 20 of each seed and does not plant the last ones.
- When a seed drops below 100, he plants a few seed plants (carrot, turnip, onion…) alongside the crops to restock.

**Harvesting**
He picks the ripe crops, going straight to the next ripe one nearby like someone working a row, and stores the harvest in a chest.

### Smelting

- **Feeding:** keeps charcoal kilns, smelters, ovens and blast furnaces fed. He brings the input (ore, wood for the charcoal kiln…) and the fuel (coal, wood) from the nearby chests, and from the chests at home when he is near home.
- **Output:** collects what comes out and stores it.
- **No fuel in the chests:** he can take a wood pile apart with a hammer and use that wood. He needs a hammer in the pack.
- **Trip size:** he carries up to 20 of input and 40 of fuel per trip.

### Cooking

**Stations he uses**
- cauldron
- food preparation table
- mead still
- cooking station and spit
- iron cooking station
- stone oven
- fermenting barrels
- beehives

Each type can be turned off in the settings. He only uses recipes you know.

**What he does**
- **Food:** cooks each food until the chests nearby (and the ones at home) have 20 of it. He cooks in batches of 5, like a player.
- **Reserve:** he always leaves at least 20 of each raw ingredient, so there is some left for you.
- **Mead:** prepares mead bases, fills the fermenting barrels and takes out the finished mead, until there are 10 of each base and 20 of each mead. No mead wanted? Turn `MakeMead` off and he only cooks food.
- **Your progress:** he prepares first the mead bases you are going to need for the next boss. For example, after Bonemass they are frost resistance and medium healing and stamina.
- **Storing:** the cooked food goes into a chest that already has that food (or other cooked foods).

**Fires and honey**
- He keeps the fires under the cooking stations he uses and the oven at about half capacity, and refuels when they get low.
- With no wood in the chests, he can take a wood pile apart.
- He collects honey from the beehives for his recipes.

### Repairing and restocking

The job for a companion you leave taking care of a base. Start it near home and he works around home. Start it anywhere else (at an outpost, say) and he works within 30 m of where you started it.

In priority order:

1. **Repair:** fixes building pieces below 75% health with a hammer, one hit each. He needs the piece's workbench nearby and access to the protected area, like a player.
2. **Restock:** keeps the fires fed, putting fuel in a bit at a time and topping up any that is below 80%. Each of these can be turned on or off:
   - campfires and ground fires
   - hearths
   - braziers
   - standing torches
   - wall torches and sconces
   - hot tubs
   - anything else that burns fuel

   He does not feed charcoal kilns or smelters; that is the Smelting job.
3. **Beehives:** empties the beehives that are at least half full and adds the honey to the rest of your honey.
4. **Piles:** keeps 100 of each building material (wood, stone…) in the chests. The surplus becomes piles (Wood Pile, Stone Pile…), built next to piles you already made. He never starts a new pile place on his own. When the chests drop below 100, he takes a pile apart to restock them.
5. **Organize:** arranges the chests like with like, taking each item to the chest that already has more of it and merging partial stacks. Equipment (weapons, armour, tools) is not moved, unless you turn that on.

He fetches a hammer and fuel from the chests if he needs to, and gives back what is left over.

## When there is nothing to do

If a companion goes 10 seconds with nothing to do and is not following you, he idles within 8 m of his job instead of standing still. He may:

- **do a small task nearby:** fix a piece, refuel a fire, collect honey, store what he is carrying
- **sit** on a free chair or bench (or on the ground) for a while
- **take a walk** to a station or chest to take a look
- **chat:** go to you or to another companion and start a conversation

Once in a while (at most every 5 minutes) he says why he is idling, for example "Nothing to cook right now." or "Waiting for the crops to grow."

The moment his job has something to do, he goes back to work. He also stops idling because of a fight, your orders, bedtime, or you opening his panel or his ring.

## Tools, chests and load

**Tools**
- The jobs fetch the tool they need from the chests at home: axe, pickaxe, cultivator, rod and bait, hammer, bow and arrows.
- When the job ends, the borrowed tool goes back to its chest.

**Repairing equipment**
- A companion takes worn equipment to a workbench when something drops below 50%, or below 80% at home.
- He only uses workbenches a player could use at that moment: under a roof, with a fire if needed.
- If no workbench nearby can repair an item (for example, he needs a black forge you have not built), that item is set aside for 10 minutes instead of being tried again and again.

**Emptying the bags**
- When you change a companion's job, or he goes back home, he stores what he picked up while working.
- What you gave him, he keeps.

**Which chests he uses**
- Companions use chests like a player. They do not use a chest someone left open, another player's private chest, or a chest behind a protected area they cannot open.
- Jobs that look for a chest do not use carts or ship holds. The exceptions: a gatherer with a full pack and far from home may unload into one of them, and you can always point a companion at one.

**Load**
A companion can carry 300 of weight, the same as a player.

## Combat

**Stances** (inner ring of the command ring; on a controller, push the analogue stick halfway towards one):

| Stance | Behaviour |
|:---|:---|
| **Balanced** | Chooses melee or bow/crossbow according to the distance |
| **Passive** | Never starts a fight; only follows or works |
| **Ranged** | Prefers bow or crossbow and keeps the distance |
| **Melee** | Stays on the close-range weapons |

**Stance keys:** you can bind a key to each stance (Controls > PassiveKey, BalancedKey, RangedKey, MeleeKey; none by default). Looking at one of your companions, the key sets just that one; otherwise every companion within RecallRange that has Command on. A message top left says who changed - handy to pull someone out of a fight that's going badly.

**When they fight**
- They attack a creature after it notices you or them. With the `EngageOnNotice` option off, they wait until it actually comes for you.
- They leave alone the animals that never attack, like deer. Hunting goes after game on purpose.
- Anything that attacks them is always answered.

**How they fight**
- **Blocking:** they block with the shield, parry and counter-attack.
- **Several attackers:** they switch to the best blocking shield (a tower shield, say) when 3 or more enemies attack at the same time.
- **Weapon choice:** they pick the weapon that does the most damage to *this* enemy, using its weaknesses and resistances.
- **Fire:** they open with a torch against enemies weak to fire, and then go back.
- **Bows and crossbows:** only used when they carry the right ammunition, at 15 m or more. Below 12 m they go back to melee.
- **Low health:** below 30% they fight defensively, and below 15% they run.
- **Back attacks:** companions do not take damage from back attacks.

**After the fight**
With Pick up on, they go and get what the kill left behind (up to 30 m). They do not swim to get it.

## Food, stamina and health

- **Food:** companions have 3 food slots like a player and eat from their own inventory when a slot becomes empty. The food gives health and stamina the same way it gives them to you.
- **Base health and stamina:** 25 health and 50 stamina before the food, the same as a player.
- **Meads:** they drink a healing mead below 50% health and a stamina mead below 25% stamina.
- **Status effects:** being wet, cold, rested and the like change their regeneration the way they change yours.
- **Running and swimming:** both cost stamina. A companion swimming with little stamina (25%) heads for the bank.

## Sleeping, resting and beds

**Beds**
- Each companion claims a free bed in his home area. The bed shows his name.
- He can also use one of your beds, as long as it is not your spawn point. So clicking a companion's bed by accident does not take it from him (the bed keeps showing the companion's name). Sleeping in it does take it, because that turns it into your spawn point. He never uses another player's bed.
- With no home set, he only takes a bed up to 20 m from one of your beds.
- He keeps the bed after dying and when you leave and come back to the game.
- Point the command key at a bed to send a companion to sleep in it.

**Sleeping**
- **At night:** a companion up to 50 m from his bed stops what he is doing and goes to sleep. That includes companions working. He goes back to work in the morning.
- **When you sleep:** your companions go to sleep too, even if it is not night yet. The night skip waits up to 30 seconds for them.

**Resting near a fire**
- When you sit near a fire, companions nearby with no job sit too.
- After 20 seconds near a fire they get the *Rested* effect. Sitting also heals.
- When you send a companion to sit near a fire with the command key, he stays there up to 5 minutes, or until you point at the fire again.

## Death and return

- **Tombstone:** a companion who dies leaves a tombstone, marked on your map until it is emptied.
- **What he loses:** that follows your world's death penalty option, as for a player. You can change it (keep everything, keep the worn equipment, or drop everything).
- **Return:** after 5 minutes he comes back on his own at his bed, or by your side if he has no bed. The time keeps counting while you are away or offline.
- **His things:** when he comes back and is near his own tombstone, he walks there and picks up his things. You can also point the command key at the tombstone.
- **Skills:** he loses 25% of the skill progress, more or less like a player.

### Recovering a lost companion

Your game keeps a list of your companions in every world, saving each one's equipment and settings every minute while he is near you, and every time the game saves your character (including when you leave or close the game). If a companion disappears for good (for example, deleted by a server without the mod), you can bring him back.

**Odin's Shrine.** Build one with the hammer (Misc tab, near a workbench) for 10 stone, 5 wood and 3 greydwarf eyes. It looks like a tombstone (or a ward stone, with Death > ShrineModel). Use it to see your companions in this world and the status of each one. For each one that is Lost or Unknown it asks whether it should bring him back; say Yes and he appears next to the shrine, say No to move on to the next. You can change the cost in Death > ShrineRecipe, or turn the shrine off in Death > ShrineBuildable.

**Through the settings** (no shrine needed):

- **Seeing the status:** turn on **General > ShowCompanionRoster** in the settings. You will see each companion as **Alive**, **Dead** (his tombstone will bring him back), **Lost** (nothing of him is left, so he can be recovered) or **Unknown**.
- **Bringing one back:** turn on **General > RecoverLostCompanion**. The first Lost companion appears by your side with the equipment and the settings from the last save in the list. Both options turn themselves off afterwards.

What the statuses mean:

- **Unknown:** on a dedicated server your game can only check the area around you, so a companion who is not nearby stays Unknown. He may still be alive somewhere. The shrine adds a warning when it asks; through the settings, turn RecoverLostCompanion on a second time within 60 seconds to recover him anyway. If he has died, his tombstone also keeps what was in him.
- **No duplicates:** if the old companion shows up later, he is removed, and his old tombstone will not bring him back.
- **Old tombstones:** a recovered companion still knows his own tombstones. Like one who was reborn, he walks there and picks up the things when he is near one (within Death > TombstoneRecoveryRange, 50 m). You can also empty it yourself.
- A companion only enters the list after being near you on version 1.5.1 or newer. A dismissed companion leaves the list.

## Movement

The companions find their own way:

- **Doors:** they open doors and gates when they arrive, line up with the opening and close the door behind them.
- **Steps and ledges:** they jump steps and step down from ledges of up to 3 m (no fall damage) when that is much shorter than going around.
- **Ladders:** to get up to where you are (a tower, a platform), they climb a wooden ladder the way you would: they walk to the foot of it and get on, and the ladder lifts them. If the ladder starts above the ground, or pushing against it does not lift them, they jump into it; after a few jumps that do not work, they try another way up. A ladder that starts from a floor is climbed from that floor, not jumped at from below. Ladders stacked one on top of the other are climbed all the way to the top. If the ladder stops a little short, they jump from the top to the floor or the ledge beside it, or onto a wall and from there to the floor. A tower of short ladders with landings in between is climbed as a single ladder: they plan the whole chain first, go from each top straight to the next foot, and remember the chains that worked (after a fall they go straight back up). Going down works the same way. They pick a ladder whose foot they can reach by walking on their own level and whose top leaves them as close as possible to where they are going, and they climb a chain of ladders one after the other. A ladder that did not work, or that always leaves them back where they started, is set aside for a few minutes.
- **Following your path:** when you are on another floor and they cannot find the way on their own (a tight tower of short ladders and small floors), they go to the last place where you were on their level and follow your exact steps from there, getting on every ladder the way you did and jumping where you jumped. If you jumped from something that would cost them more than 30% of their health, they look for another way down.
- **Ledges:** they do not walk off a drop of more than 3 m (Movement > MaxDrop) onto dry ground — they stop at the edge and look for a ladder, steps or a safe drop.
- **Going down:** they use the ladder or go around when there is one, or climb down the ladder they came up by. Only when there is no other way down do they jump a drop, of up to 8 m (Movement > StuckDrop) and never one that would cost more than 30% of their health at that moment. A gap over a dangerous drop is only jumped with room to spare.
- **Fall damage:** companions take fall damage like a player: none up to 4 m, and more the further they fall (the Feather Fall effect helps them too).
- **Water:** they jump narrow streams and small gaps. They go around the water when the dry way is not much longer, and otherwise they swim.
- **Walking or running:** they walk while working and run when what they are going to get is more than 25 m away.
- **Fire and tar:** they avoid fire and tar pits.

## Skills

Companions get better at what they do, like a player. Fighting raises their weapon skills, and the jobs raise the matching skills (wood cutting, pickaxes, fishing, sneaking…). When a skill goes up a level, you get a message.

## Multiplayer

- **Your companion stays yours.** Sometimes another player's game is the one running your companion (the game hands the characters to the computer of the nearby player it wants). Even so, he follows you, sleeps when you sleep and sits when you sit.
- **Your orders always work.** When you give an order or open his ring, your game takes control of him.
- **Shared chests are safe.** Items a companion moves are saved correctly, with no losses and no duplication. He does not use a chest another player left open.
- **The credit goes to the owner.** The crops he plants and the piles he builds count as built by his owner.
- **Everyone needs the mod, including the server.** On a dedicated server, install the mod on the server and in each player's game. A server (or a host) without the mod deletes any companion it loads. You can bring a deleted companion back through the list (see [Recovering a lost companion](#recovering-a-lost-companion)), but it is better not to lose him. A dedicated server always keeps the area around the world center loaded, so companions near the world spawn point are at risk even with everyone online. Players without the mod simply cannot see the companions.

## Troubleshooting

**My companion is just standing there.**
Usually his job has nothing to do, and he starts idling within a few seconds. Common reasons:
- **Cooking:** he is leaving the ingredient reserve for you.
- **Gathering:** the items reached the limits.
- **Planting:** there is no cultivated ground in his zones.
- **Smelting:** he has no input in the chests.

Check the log (below) to know the reason.

**My companion disappeared.**
Use Odin's Shrine, or turn on **General > ShowCompanionRoster**, to see his status. If he is Lost or Unknown, see [Recovering a lost companion](#recovering-a-lost-companion).

**He did nothing while I was away.**
The game unloads the parts of the world where there is no player, and the companions there freeze until someone comes back. A companion only works while you (or another player) are close enough for his area to stay loaded.

**He does not go to sleep.**
He needs a free bed in his home area (set a home first) and has to be within 50 m of the bed.

**He keeps going to a workbench to repair.**
At home, equipment is repaired when it drops below 80%. You can change `HomeThreshold` in the equipment repair settings.

**He ignores a chest.**
That chest may be open, private (built by another player), behind a protected area, or out of his reach.

**Reading the log**
The companions write what they are doing and why to `BepInEx/LogOutput.log`, for example "Nothing to cook right now" or "No station within 20m can repair…". If something looks wrong, that file is the best thing to include in a bug report.
