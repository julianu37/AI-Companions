> 🌍 **English** · [Português](Commands-pt) · [Español](Commands-es) · [Français](Commands-fr) · [Italiano](Commands-it)

# Commands

Keys, chat/voice orders and console commands.

---

## Keys

| Key | Action |
|---|---|
| **`V`** | **Push-to-talk**: hold it and speak; release for him to understand |
| **`Z`** | **Command key**: look at something and tap it to command the companions (move / attack / open a door / sit / repair). **Hold** it to call them |
| **`E`** | **Hold** it looking at a companion to open the **radial menu**; **tap** it to open his panel / inventory |
| **`LeftAlt` + `Z`** | Farm zone editor |
| **`I`** | **AI panel**: one **tab per companion** (name, type, Call, Appearance, his voice and personality) and the **AI tab** (companion summoner, LLM settings with the **provider picker**, volume, memory, conversation and push-to-talk) — everything saved right away. The **speech speed** stays in **`F8`** |
| **`F8`** | Mod settings panel (every option, in game) |
| **`F5`** | Game console (for the mod's test commands) |

> The AI panel (key **`I`**, changeable in `[AI] PanelKey`) opens **without needing a companion nearby**, closes with
> **`I`**, **`Esc`** or the **X**, and every control is saved right away (in the `.cfg`, when it is a global option;
> in the companion, when it belongs to his tab). While it is open, your character does not answer the controls, and
> the key is ignored with the inventory, the map, the menu or the `F8` panel open.

> **Types:** the **dverger** (**Mage**, **Fire Mage**, **Ice Mage** and **Support**) are **combat** types — the ring
> shows only the combat/support actions and the **work** orders (**chat**, **voice** or the **command key**) are
> refused with *"I fight, I don't do that work."*. To work, use the **Companion** type
> (**[How it works](How-it-works#companion-types)**).

### Radial menu (hold `E`)

Hold **`E`** looking at a companion to open the action ring (move, attack, open a door, sit, repair…). A **tap** on
`E` opens his panel / inventory.

![The radial menu open, with the actions around the centre](images/en-radial-menu.jpg)

---

## Chat and voice orders

You type in the game chat **or** speak holding **`V`**. The order is interpreted by an LLM: you can say the same
thing in **several ways** — you do not have to memorize the exact sentence.

![The chat with the order "Sophia, collect wood", the companion's reply and the speech balloon](images/en-companions-chat-orders.jpg)

### By name

Start the message with a companion's **name** to talk **only** to him: `Gruncha, gather 5 stones` (the prompt, the
personality, the memory and the order are his). **Without** the name, the order applies to **everyone** who can
take it. A beginning that looks like a name and does not exist (`Fulano, collect 10 stones`) is warned about — and
nothing is ordered —, but a form of address keeps working (`mate, collect 10 stones`).

> The name is the one you gave in the **companion's tab** (`I` panel) or the name of the type, if he was never
> renamed.

### Follow and stop

| You say | What he does |
|---|---|
| `follow me` | Goes back to following you |
| `stop` / `stop following me` | Stops what he is doing and stands still — and the answer says **the job** he stopped ("stop (gather wood)", "stop (cook)") |
| `come here` | Comes to you |

> There is no separate "follow closely": the options are **following** you or **stopping**.

### Gather

| You say | What he does |
|---|---|
| `gather 10 wood` / `chop some wood` | Gathers **wood** |
| `gather 10 stones` / `get 10 stones` | Gathers **stone** |
| `mine iron` / `mine ore` | **Mines** ore |
| `pick the berries` / `get the mushrooms` | **Forages** (plants, fruit, mushrooms) |

> **The number is a target.** He counts it **in the companion's inventory** (his bag — it does not count chests or
> piles on the ground). If he already has 10 wood and you ask for 10, he says *"We already have enough."* and does
> not go out to gather. The target applies **only to the resource asked for**.

### Attack

| You say | What he does |
|---|---|
| `attack that greydwarf` | Attacks the creature **named** (even in the Passive stance) |
| `kill the boar` | The same |
| `attack the troll` | The same |

> The creature is looked for **first in your sights**; then, the closest one within `[AI] TargetRange` (default
> 40 m). The one you are aiming at **always** counts, however far away it is. *"Attack"* without naming a creature
> becomes **hunting**.

### Equip and drop

| You say | What he does |
|---|---|
| `equip the bronze axe` / `take the pickaxe` | Equips the item **he already carries** |
| `drop the stone` / `throw away 5 wood` | Drops the item on the ground (no number = the whole stack) |

> `equip` and `drop` only work for what he **already has in his bag**. A quest item never drops.

### Items on the ground

| You say | What he does |
|---|---|
| `pick up the items` / `pick up the wood from the ground` | Walks to the loose items and picks them up **one by one** |
| `pick up what is on the ground` | The same — he takes whatever is **closest**, not an item chosen by name |
| `stop picking up` / `don't pick anything up` | **Turns off** the automatic pickup |

> The pickup takes **even what he dropped on purpose** — which is exactly what the game's automatic magnet ignores.
> He gives up if the item does not fit whole in his bag or if it is too far away.

### Doors, hives, ships, fires and chests

| You say | What he does |
|---|---|
| `close the door` / `open the door` | Opens or closes the **door** he is looking at |
| `empty the beehive` / `get the honey` | Empties the **beehive** and keeps the honey |
| `board the ship` / `get on the boat` | Boards the **ship**, taking the seat you point at |
| `sit by the fire` | Sits down by the **fire** he is looking at |
| `put this in the chest` / `store it` | Puts what he carries into the **chest** |

> The target is the object **you are looking at**, with the same ray and layers as the command key. With nothing
> under the crosshair he takes the nearest one around you: **20 m** for a door or a beehive, **30 m** for a ship,
> **5 m** for a fire or a chest — never his own bag. The order reaches **one** companion: the one you named by name,
> or the nearest one to that object that can take orders.

> His answer says what happened: "Closing the door.", "On board!", "Sitting by the fire.", "Putting 12 item(s) in
> the chest.". A **locked** door answers "It's locked.", an **empty** beehive "No honey in there yet." and a **full**
> chest "There's no room in that chest."; with nothing around he says he cannot see one.

> The fire has to be **lit**, and the chest has to be a real storage one that is free to use: not a tombstone, a
> cart, a ship's hold, a dungeon's loot chest or one inside a ward he cannot enter.

### Inventory

| You say | What he does |
|---|---|
| `open your inventory` / `show me what you carry` | Opens **his inventory window** for you to see |
| `close the inventory` | Closes the window (the window's **X** always closes it) |

> Closing is an action of its own (`inventory_close`): if the model answers "open" for a sentence with a closing
> verb, the mod corrects it by **your** sentence. The window's **X** and `Esc` always close it.

![The companion's inventory open: the "SOPHIA" window with what she carries and the Food slots, next to your inventory and the crafting panel](images/en-companions-inventory.jpg)

### Combat stance

| You say | What he does |
|---|---|
| `stay defensive` | **Passive** (defensive) stance |
| `stay balanced` | **Balanced** stance |
| `fight at range` | **Ranged** stance |
| `fight up close` | **Melee** stance |

### Jobs

| You say | What he does |
|---|---|
| `take care of the farm` | **Farming** job |
| `go fishing` | **Fishing** job |
| `repair the buildings` | **Repair** job |
| `go cook` | **Cooking** job |
| `smelt the ore` | **Smelting** job |
| `go hunting` | **Hunting** job |
| `set the home here` | Sets the **home** |
| `wander around` / `stop wandering` | Turns **wandering** on/off |
| `take orders` / `ignore orders` | Turns **taking orders** on/off |

### Conversation

| You say | What he does |
|---|---|
| `good morning` | Answers as a character |
| `how are you?` | The same |
| `what did you think of my house?` | The same |

> When the message is **not** an order, the companion **converses**: he speaks the answer out loud and shows it in
> the chat. That is controlled by `[AI] Conversation` (default `true`). Off = only orders are understood.

---

## Console commands (`F5`)

| Command | What it does |
|---|---|
| `modia_tts_teste` | Speaks a test sentence through the TTS (full pipeline: model, synthesis and playback) |
| `modia_tts_teste <text>` | Speaks the text you write — e.g. `modia_tts_teste Good morning, boss!` |
| `modia_stt_teste` | Runs the Whisper recognition over a WAV, **without** using the microphone |

> `modia_stt_teste` transcribes the file in `[AI] SttWavTest` (default `whisper\jfk.wav`) and writes the result to
> the log. It is there to check whether the Whisper model is loading, when the voice orders do not come out.
>
> **The package does not bring that WAV** (it left the package to save space) — put a file of yours in the
> `whisper\` folder and point `[AI] SttWavTest` at it. The command **does not** take the path as an argument.

---

## Important notes

- **The language follows the game.** Since 1.9.0 the interface, the speech, the personality presets and the LLM
  contract are in **five languages** (PT-BR, EN, ES, FR, IT) and follow the **game's language** — the orders are
  understood in that language too, and the voice and the speech recognition are **automatic** (`auto` / `0`).
- **The number is a target counted in his bag.** `gather 10` = **having 10 in the companion's inventory**. With 15,
  he does not even start and says he already has enough; with 7, he gathers up to 10 (the last tree can go over).
- **The order is read by the LLM**, so you can speak your own way. `get 10 stones`, `I'll need some stone`,
  `grab some stones` — all of them should land on the same action.
- **`[AI] Prefix`** (empty by default): if you fill it in, for example with `/c`, **only** the messages that start
  with that prefix go to the model. The rest of the chat is left alone.
- **`[AI] CooldownSeconds`** (default `3`): minimum time between two orders, so a burst of messages does not flood
  the model.
- **The chat and the voice come in through the same path.** What works typed works spoken, with the same
  personality, memory and conversation.
- **`despawn` is not exposed to the model.** You cannot order the companion to disappear by chat or voice.

---

## Convenience tweaks of the mod

- **The `V` key** no longer triggers the game's *Toggle Auto-Pickup* (see **[Voice](Voice)**).
- **Bigger gathering radius:** wood/stone/ore are looked for up to **25 m** from you, and the drops are picked up
  up to **12 m**.
- **Random chatter off** by default: the loose little lines every 15–30 s do not happen anymore. The useful lines
  (orders, change of mode, AI replies, events) stay.

To see every option, go to **[Configuration](Configuration)**.
