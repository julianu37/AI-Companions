# Changelog (full detail)

Every change, version by version. The `CHANGELOG.md` shipped with the mod is a short version of this.

All notable changes to this fork of ProfMags' Offline Companions. Versions before 1.4.0 are the original mod (see [ProfMags' Thunderstore page](https://thunderstore.io/c/valheim/p/ProfMags/Offline_Companions/) for that history) plus the initial batch of Valheim 1.0 API-compatibility fixes that let the mod load and run at all on 1.0.

## 1.5.7
- The beehive order goes to the nearest companion that isn't resting, sleeping or heading to bed (it went to the first one on the list, even one on its way to bed).
- A companion carrying out one of your orders (beehive, boarding, move, cart, chest, tombstone) no longer goes to bed in the middle of it.
- Melee: a tree, log or rock between a companion and its target soaked up its swings; it now steps round to a spot with a clear line first (and swings anyway if there's none).
- Encumbrance: a companion carrying more than it can is encumbered like a player - walking pace, no running, jumping or dodging, the loaded-down walk, 10 stamina a second while moving, and standing still when that runs out. It says so, and its status line reads "carrying too much - walking slowly". Its limit is General > MaxCarryWeight plus what its gear adds (a strength belt +150), times the world's carry weight setting; pickups, the weight bar and the jobs' "pack full" checks use the same limit.
- Portals: a following companion follows the game's item rules (new setting General > PortalItems, Game by default): ore, metals and the like only through a portal that lets everything through or with the world's portal setting on Casual, boss items never. Carrying something that can't go through, it leaves it in a chest within 15 m (stacking with the same item where it can) and comes through; with no chest that has room it stays behind and says why. AllowAll brings companions through with anything, as before. Dungeon entrances and map teleports aren't checked, as for the player.
- Fixed the summon window's buttons not taking clicks (and showing "Missing requirement" when hovered) after you had opened your inventory somewhere you couldn't craft - they were copies of the game's Craft button, greyed out at that moment.
- Stone and Ore leave buildings alone: built pieces (yours or part of a location) and walls are no longer mined for stone.
- Wood works through fallen logs first, then stumps, and only then fells a standing tree (logs and stumps were taken nearest first, together).
- Wood: a log lying on a slope or across something is chopped at the part near the ground it can reach, instead of walking under its middle (it got stuck there, gave up on the log and came back to it). The spot has to have a clear line to the log - a rock in between doesn't count.
- Wood: after splitting a log or felling a tree, the pieces that come out of it are finished first (within 6 m, for 30 s), instead of the nearest other log.
- Wood: a companion keeps to the spot it picked to chop a log (it re-picked the nearest spot every 2 s and kept moving along the log), and only steps aside when a standing tree comes down - breaking a log no longer sends it 4 m away before it comes back for the wood.
- Gathering: a companion checks its swings land - three swings that don't lower the target's health and it tries another side of a log, then leaves it for a while (it chopped 17 times at a rock under a log).
- Gathering: the game now tells a companion when one of its swings lands on a tree, log, stump or rock (a hit register). Once a swing lands it stays on that spot until the target breaks - no re-approaching or shuffling along the log - and only moves after 2 swings in a row that didn't land (another side first, then it leaves the target).
- Wood: a log that has just fallen or split is left alone until it stops rolling (up to 3 s) before the companion picks where to stand.
- Wood: the logs a tree or log breaks into are remembered and finished first wherever they roll (within 30 m, for 60 s), even outside the Follow range; logs that were already lying there don't count.
- Wood: a companion waits up to 8 s for a log to stop rolling or sliding; one that keeps moving is left for 15 s instead of chased down the slope.
- Wood: a log hung up in the branches with no part in reach from the ground is left for 15 s (it may fall) - no more swinging at it from underneath.
- Wood: no swing at a log that's further away than the axe reaches; it steps closer first.
- Quieter log: the food check's "no food found" line is written once until the inventory or food changes.
- Water: a swimming companion stuck at a bank jumps out toward where it's going, like a player (the game lets a swimmer that touches the ground jump). Its get-unstuck steps are no longer cancelled by the deep water round it (it stood still in a pool by a stake wall for 7 minutes).
- Wood: a log caught in a standing tree no longer counts as reachable from the tree's trunk; a companion right under a log above its reach leaves it for 15 s instead of walking in place for minutes.
- Wood: when you walk off and the companion leaves its log to catch up, it doesn't pick the same log straight back up (it did, over and over, swapping axe and sword each time); it looks for work again after 3 s.
- Repairs: a companion without a home set uses the home threshold (GearRepair > HomeThreshold, 80%) inside a base (a workbench's area), not only below 50%.
- Repairs: an item set aside because no station nearby could fix it is looked at again once the companion is 20 m from where that happened (it waited 10 minutes even after you got home).
- Fires: the way round a hearth or campfire is sized by the hearth stone itself and every stretch of it is checked for flames, pushed further out until it's clear; a companion that steps out of a fire plans its route again from outside (it cut across the hearth stone into the flames 7 times while sorting chests). Applies to all movement - following, jobs, chores, combat.
- Fires: every fire check now also covers a lit fire's damage zone (the game keeps it separate from the flames the checks looked at); when the simple way round a fire doesn't fit, a companion looks for a chain of clear spots round it; it never hops up a step (the hearth stone) toward a fire; and a job that can only get somewhere through a fire skips that errand instead of walking through it (following you, it still steers round step by step).
- Chests: companions only use chests a player built - not the chests of huts, camps and dungeons the world generated (one carried its wood to a hut's chest). Pointing a companion at a chest yourself still works on any chest.
- Debug log: a companion that shoots up faster and steeper than it can climb (over 1 m in a quarter second, no jump or ladder) is logged with where it was, what it was doing and the building pieces it touched (one ended up on a roof with no way up).
- Fixed an error when Repair and Restock opened a chest just before your character had spawned.

## 1.5.6
- Boarding: a companion jumps straight onto the deck when it can - from where you jumped aboard, or the nearest dock or bank spot in jump reach - and only dives in for the ladder when there's no such spot.
- Boarding through shallow water: a companion that lands in the shallows wades straight to the ship's ladder and uses it from there (or swims once it's deep), instead of running back round the dock to dive in again.
- An enemy a companion gave up on because it couldn't reach it (out in deep water) is taken on again as soon as it can be reached, or as soon as it attacks you or the companion - it used to be ignored for 20 s even after it came ashore and went for you.
- Pointing the command key at a beehive sends a companion to empty it and keep the honey (it says so when there's none yet).
- Fixed the overhead stamina bar turning into a plain grey bar after a portal (or after the companion was out of range).
- Getting off a ship: a companion steps to the nearest point of the side facing you (not the deck spot nearest you), runs at it and jumps, keeps heading your way in the air, and jumps again straight away if it lands back on the deck.
- Swimming costs companions stamina like the player: 5 a second at Swim skill 0, less with skill (setting Stamina SwimDrainAtSkill0 replaces SwimDrainRate, which was 10). Boarding counts only the swim to within reach of the ladder, so a companion no longer waits for nearly full stamina or gives up on a swim it can make.
- A companion you ordered aboard also gets up and follows you when you go ashore, as long as it's following you.
- Fixed a following companion warping to you when you got off a ship: the original "you moved 15 m, warp after you" check is gone. Portals, dungeon doors and map teleports still bring following companions along (as before), and one that falls far behind still catches up (Follow TeleportDistance).
- Ships: companions jog across the deck (walking only the last step to a seat), know you're aboard when the game does, and use the ship's ladder only while swimming or standing - not in mid-dive.
- Ships: companions walk across the deck to their seat and sit down (also when you point them at another seat), and get up and walk, jump or swim off when you go ashore - no more teleporting on and off seats. Each ship type gets a small deck map the first time a companion is aboard one.
- Boarding: with the ship's ladder within about 15 m, a companion heads for the dock end or bank nearest the ladder and dives in from there, instead of walking the long way round.
- A companion aboard a ship no longer heads off looking for the shore because it was low on stamina when it climbed out of the water.
- Boarding like a player: from a dock or shore near the ship's ladder a companion dives in towards it and treads water beside it until it's in reach, instead of going the long way round or turning back for the shore. It never takes the mast's seat point, and pointing at a seat with the command key sends it to that seat (moving there if it's already aboard).
- Boarding: a companion resting for its stamina before a swim to the ship no longer gives up; one that can't get aboard waits on dry land, not in the water, and a swim it could never manage is given up at once with the reason in the log.
- Companions get on ships like a player: they jump aboard from a dock or the shore, use the ship's ladder (swimming to it if they have the stamina), or follow your own steps aboard - on the board order, and on their own when you're aboard a ship close by (new settings: Follow AutoBoard, BoardRange). No more teleporting into a seat; one that can't get aboard waits on the shore. A companion that boarded with you gets up again when you go ashore.
- Odin's Shrine buttons read Summon and Recover (the longer labels were shrunk to fit); the summon and starter windows lose the dark layers over their sections, buttons and sliders.
- Odin's Shrine now uses the game's own popup (the same one as the unsummon and quit confirms) for its menu and the recover list.
- An unsummoned companion can be brought back at the shrine - without its gear - until you next load the world (new setting: General RecoverUnsummoned).
- The companion customising window scales like the game's own windows, and its button labels match the game's popup buttons.
- Odin's Shrine menu: the game's own text styles and buttons, a line of flavour text, and no close button - it closes like a chest (Use again, Escape, or walking away).
- Odin's Shrine: "Summon a new companion" checks your coins first - short, a popup says how many you have and need, and the window doesn't open; the coins are taken when you press Summon Companion.
- The summon and starter windows have new text (Odin's offering), and both buttons say Summon Companion; hair and beard styles show their proper names.
- The shrine menu and the companion customising window use the game's own window and button look instead of textures packed in the mod.
- Odin's Shrine opens a menu: Recover lost companions (each companion with its status, a Recover button for a lost one) or Purchase a new companion (the customising window with a Purchase button; 1,000 coins from your pack, and it tells you how many you're short). The Shift+E hire is gone.
- A tick latched on: the companion swings its whole combo at it while it has the stamina, and doesn't run away with it still feeding.
- A tick latched on to a companion: it turns on it at once, lowers its guard (blocking only soaks the bites while it keeps feeding), doesn't step away, and swings without waiting to line up with it.
- Fix: Balanced stance no longer swaps to the bow for a tick that is latched on or mid-jump ("above, out of reach").
- Companions don't swim or dive in for loot any more - a drop on the far side of water is left.
- A tick latched on to a companion is its first target and is swung at - no more backing away while it feeds.
- Balanced stance takes the bow, at any distance, for an enemy its melee weapon can't get at (out in deep water, a level above or below, no way to walk there) when it has a clear shot.
- In a dungeon a companion with a bow shoots alerted enemies it has a clear shot at from where it stands, out to bow range, without leaving you.
- Companions let an enemy go once it is more than 35 m from you and come back (new setting: Combat LeashFromYou).
- An enemy a companion can't get at and hasn't hurt for 10 s is let go and left alone for 20 s (new setting: Combat GiveUpTime); one it can't get at also counts as further away when picking what to fight, so a closer enemy comes first.
- Bows: a draw starts a little inside the 30 m reach, and a draw already started carries on, walking closer, if the target steps just out of reach (new setting: Combat BowEdgeMargin).
- Companions don't swim out after an enemy in deep water - they shoot it from the bank, or wait there for it with a melee weapon. Fighting with Balanced stance, an enemy out in deep water always gets the bow.
- Fix: a companion standing in a shallow pool wouldn't step anywhere in a fight ("standing its ground") - it now steps through water no deeper than where it stands.
- Spike walls are spotted further ahead and turned away from harder when already at their edge.
- Companions keep off spike walls (sharpened stakes and any other built defence that hurts what touches it) - they walk along them rather than into them, never step back into them in a fight, and don't land a jump on them.
- Fix: several fight moves could take a companion off a ledge, into deep water or fire - backing off with a bow, running when badly hurt, moving about while waiting for an opening, stepping back to make room for a stab, and the last-ditch chase when no route was found. All of them now check the ground first and stand still rather than take an unsafe step.
- Recall, respawn, recovering a lost companion and getting off a ship put the companion on a safe spot next to you (in view, dry, no fire or spikes) instead of any floor nearby.
- Hire more companions: alt-use Odin's Shrine (Shift + Use) to hire another companion for 1,000 coins from your pack - you choose its looks in the same panel as your first one (General > CompanionPrice).
- Fix: in a dungeon a companion could take the gap beside a ledge for shallow water and jump it, landing on the level below; it no longer jumps "water" in dungeons or where the ground drops away.
- Fix: a companion could end up behind a wall - e.g. stuck behind the invisible barrier in a burial chamber's entrance - when it was brought to you through a dungeon entrance, a portal or for falling behind; it now lands on a spot you can see, with floor at your height.
- Fix: when you left a dungeon (or went in) without your companion, it could stay behind - especially while fighting; a following companion now comes through after you.
- Bows: the stamina a companion uses holding a draw now goes by its own Bows skill (the game used yours), and it turns faster to keep its aim while drawn, like you swinging the camera round (Combat > BowAimTurnSpeed).
- Fix: working its way round behind an enemy, a companion could walk off a ledge - it now only goes round over safe ground and otherwise attacks from where it is.
- Companions eat the same food again when it has 3 minutes left, like a player topping up, instead of waiting for it to run out (Food > EatAgainMinutes).
- Fix: after an enemy it had been fighting died or unloaded, a companion could freeze mid-fight - sliding along the ground and no longer fighting - with errors filling the log.
- Bows: companions draw the bow before each shot like a player - the draw takes as long as yours at their Bows skill and drains stamina while held - instead of firing every arrow instantly at full draw. They stand and shoot rather than running circles round the target, draw while turning to it, draw again as soon as the last arrow is away, and hold the shot (or step closer) when a hill or rock is in the way.
- Bows: like a player, companions don't always draw fully - a close target, low stamina or an enemy rushing in gets a quicker, weaker partial draw, never under half (Combat > BowMinDraw, BowFullDrawRange); out of stamina mid-draw they still shoot if the draw is far enough.
- Bows: companions aim for the speed of their draw, so a partial draw's slower, dropping arrow still reaches the target; they don't start a draw they can't finish - short of stamina they wait to get their breath back (Combat > StaminaResume) instead of drawing and dropping the arrow over and over; far targets always get a full draw.
- Ranged stance: companions carry their bow instead of a sword and shield, and only switch to melee when an enemy gets within 4 m (8 m while catching their breath), switching back once it's further off (Combat > RangedMeleeRange).
- Fix: in Ranged stance, a companion out of breath kept running from a close enemy with its bow out and never fought back.
- Combat: companions only go after enemies they can actually get at - in sight, not behind walls or closed doors (hearing one is no longer enough) - and pick the closest, starting with whatever is attacking them, then whatever is after you.
- Combat: a fight never opens a door; an enemy that goes behind a wall or door is let go. Ordering the attack still lets them open the door.
- Combat: indoors (dungeons, mines, caves and under a roof) companions only fight enemies that are actually coming for you or them (Combat > IndoorAggroOnly).
- Combat: in dungeons, mines and caves companions fight defensively - they stay with you and fight what comes within 8 m of you instead of running off down the corridor (Combat > DungeonDefensive, DefendRange).
- Fix: a recall or move order didn't stop a fight - companions went straight back to it. Now they leave the fight and start no new one for 10 s, only fighting back against something hitting them up close (Combat > OrderHoldTime).
- Chopping and mining: companions swing again as soon as the last swing allows, through the weapon's combo like a player holding attack, instead of one swing every 2.5 seconds; they stop to get their breath back when low and carry on when rested (Gathering > RestStamina, ResumeStamina; Advanced > GatheringSwingInterval removed).
- Chopping: the power chop is only used on stumps and on logs lying too low for a normal swing, not on every swing after the third (Gathering > PowerChopLogBelow).
- Fix: on the Wood job a companion could get stuck against a tree in a fight - the job kept walking it towards its stump while it fought.
- Fix: after a fight on the Wood job, a companion swapped between its sword and axe a few times before chopping again.
- Wood job with Follow on: fallen logs and stumps up to 20 m from you are finished before a new tree is felled (Gathering > FollowLogRange) - logs often roll past the 10 m FollowRange.
- Fix: a companion loading in with the Wood job (or another tool job) on could work with no armour on - it took out its axe before its gear went on, and gear was then held back until the job ended.
- Fix: a strip across the middle of the F8 settings panel ignored clicks, and the game's hover text showed through the middle of the command wheel - the mod's panels were drawn under the game's HUD. They now sit above the HUD and below the pause menu.
- Translations: tier names (Companion, Warrior, Mage, Fire Mage, Ice Mage, Support), the "found you" greetings, ON/OFF in the settings panel, "Hold" in the hover text and the tombstone's resurrect text can now be translated.
- Following you indoors, companions now look for a free chair up to 8 m away before sitting on the floor (Follow > ChairRange).
- Fix: General > ResetStarterFlag didn't bring the starter companion offer back when you already had a companion in that world.
- Fix: with the Combat world modifier raised, companions grew bigger than the player (heads clipping through helmets) and moved faster - they now keep a player's size and speed.
- Combat: companions no longer parry every block - each enemy swing is a parry or a plain block by chance, better with Blocking skill (Combat > ParryChance, ParryChanceMax).
- Combat: companions only block swings that can reach them, not every enemy swinging within 8 m.
- Combat: melee companions swing their weapon's full combo (3 swings, the last for double damage) instead of one swing every 2 seconds, starting each swing as soon as the last allows it, like a player holding attack (Combat > FightLikePlayer, ComboHits, ComboPause).
- Combat: they attack on an opening - nothing swinging at them, the target staggered, or right after a swing has landed on their shield.
- Combat: when the enemy is staggered (a parry), they go in with a combo even while catching their breath - always at least one hit if they have the stamina for it.
- Combat: one on one, companions keep just outside the enemy's reach, go in for a combo when it can't swing again in time, then step back out - and step out of swings at the edge of its reach rather than blocking (Combat > Spacing, SpacingMargin, SpacingStepChance).
- Combat: when an enemy is busy with you or someone else, companions work round behind it and attack from there, out of its swings (Combat > Flank).
- Combat: an enemy 1-2 hits from dead is finished off with all the stamina it takes, when nothing else is close enough to hit them afterwards (Combat > FinishHits).
- Combat: catching their breath, they walk up to the next enemy and wait just out of its reach instead of standing still, and don't sprint into a fight short of stamina.
- Combat: sometimes they step out of a swing instead of blocking - back, back and to the side, or to the side, away from the other enemies, onto safe ground only (Combat > StepAwayChance).
- Combat: once started, a combo is carried through instead of breaking off whenever another enemy swings, and a wide swing doesn't wait to be lined up exactly.
- Combat: they keep stamina back for blocking - a combo may dip into it, then they take a step back out of the enemy's reach and guard while they get their breath back (Combat > StaminaReserve, StaminaResume, BackOffTime).
- Combat: when their swings would hit your walls and there's a clear spot by the enemy, they move there between combos.
- Combat: with enemies close enough to hit them from different sides, they move for a couple of seconds until all of them are on one side, then fight from there (Combat > GroupSize, GroupRange, FlankAngle).
- Combat: their reaction to a swing varies a little each time, so they misjudge now and then.
- Combat: badly hurt, they run to you when the enemy isn't in the way, avoiding drops and water.
- Combat: stepping back when too close to stab now actually steps back (it used to stand still); wide swings don't step back at all, they hit from up close.
- Combat: a companion that takes out its tower shield against a group no longer puts it straight away again mid-fight.

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
