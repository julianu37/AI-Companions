#!/usr/bin/env python3
"""MCP server for the Valheim DevBridge (AI Companions).

It speaks the Model Context Protocol over stdio and forwards every tool call to the in-game BepInEx module
(`src/Companions/DevBridge/`, compiled into Companions.dll) over a loopback TCP socket. The game must be running with
the bridge enabled; when it is not, every tool returns a clear error instead of hanging.

Configuration (environment variables):
  DEVRIDGE_HOST   default 127.0.0.1
  DEVRIDGE_PORT   default 47110
  DEVRIDGE_TOKEN  default empty (must match [Bridge] Token in the plugin config)
  DEVRIDGE_LOG    default <Valheim>\\BepInEx\\LogOutput.log

Only the Python standard library is used.
"""

import base64
import ctypes
import json
import os
import re
import socket
import sys
import time

HOST = os.environ.get("DEVRIDGE_HOST", "127.0.0.1")
PORT = int(os.environ.get("DEVRIDGE_PORT", "47110"))
TOKEN = os.environ.get("DEVRIDGE_TOKEN", "")
LOG_PATH = os.environ.get(
    "DEVRIDGE_LOG",
    r"C:\Program Files (x86)\Steam\steamapps\common\Valheim\BepInEx\LogOutput.log",
)

SERVER_NAME = "valheim-devbridge"
SERVER_VERSION = "1.0.0"
PROTOCOL_VERSION = "2024-11-05"


# ----------------------------------------------------------------------------------------------------------------
# Bridge client
# ----------------------------------------------------------------------------------------------------------------

def focus_game():
    """Brings the Valheim window to the foreground. The MCP server runs under the foreground application (the editor
    or the terminal), which is allowed to set the foreground window - the game process itself is blocked by the
    Windows foreground lock when it is in the background."""
    try:
        user32 = ctypes.windll.user32
        hwnd = user32.FindWindowW("UnityWndClass", None)
        if not hwnd:
            return False
        user32.ShowWindow(hwnd, 9)  # SW_RESTORE
        user32.SetForegroundWindow(hwnd)
        return bool(user32.GetForegroundWindow() == hwnd)
    except Exception:  # noqa: BLE001 - best effort, the bridge reports the real state
        return False


def call_bridge(cmd, **args):
    payload = {"cmd": cmd, "args": args}
    if TOKEN:
        payload["token"] = TOKEN
    data = (json.dumps(payload, ensure_ascii=False) + "\n").encode("utf-8")
    try:
        with socket.create_connection((HOST, PORT), timeout=10.0) as sock:
            sock.settimeout(12.0)
            sock.sendall(data)
            buf = b""
            while not buf.endswith(b"\n"):
                chunk = sock.recv(65536)
                if not chunk:
                    break
                buf += chunk
    except OSError as exc:
        raise RuntimeError(
            "cannot reach the DevBridge at %s:%s (%s). Is Valheim running with the DevBridge plugin loaded?"
            % (HOST, PORT, exc)
        ) from exc
    if not buf:
        raise RuntimeError("the DevBridge closed the connection without answering")
    return json.loads(buf.decode("utf-8"))


def require_ok(result):
    if not result.get("ok"):
        raise RuntimeError(result.get("error") or "the bridge returned an error")
    return result.get("data")


def world_enter(args):
    """Entra no mundo com a sequencia do JOGADOR, por teclado e pelo bridge (5/11/2026).

    `Espaco` em rajadas curtas (pula o cinematico) e depois `Seta para baixo` + `Enter` x3 a 0,5 s, repetindo a
    rodada INTEIRA ate entrar. Por que repetir e nao "3 tentativas": o bridge responde no BOOT (o plugin sobe com o
    jogo, ~3 s apos o launch), muito antes de existir menu - com 3 tentativas o orcamento acabava durante o
    cinematico e o login FALHAVA (medido: "the key sequence did not load the world in 120 s"). O relogio aqui e de
    parede (`wait` segundos), nao um numero de tentativas.
    """
    wait = max(10, min(300, int(args.get("wait", 120))))
    delay = max(0, min(180, int(args.get("delay", 0))))
    if delay:
        time.sleep(delay)
    steps = [("DownArrow", 0.5), ("Return", 0.5), ("Return", 0.5)]
    deadline = time.time() + wait

    def poll(seconds):
        end = time.time() + seconds
        last = None
        while time.time() < end:
            try:
                last = require_ok(call_bridge("ping"))
                if last.get("inGame"):
                    return last
            except Exception as exc:  # noqa: BLE001 - the bridge may be busy while the world loads
                last = {"error": str(exc)}
            time.sleep(1.5)
        return None

    entered = None
    attempt = 0
    while entered is None and time.time() < deadline:
        attempt += 1
        # rajada curta de Espaco: o cinematico engole as primeiras, e o menu (se ja estiver na frente) nao liga
        for _ in range(5):
            if time.time() >= deadline:
                break
            focus_game()
            require_ok(call_bridge("input.key", key="Space"))
            time.sleep(0.2)
        # a sequencia do jogador, sem enrolacao
        for key, step_delay in steps:
            focus_game()
            require_ok(call_bridge("input.key", key=key))
            time.sleep(step_delay)
        entered = poll(3)

    if entered is None:
        raise RuntimeError("the key sequence did not load the world in %d s (Space > Down > Enter x3, %d rounds)"
                           % (wait, attempt))

    data = dict(entered)
    if args.get("setup", True):
        time.sleep(1.5)
        data["setup"] = require_ok(call_bridge("world.setup", time=args.get("time", 0.5)))
    return data


def log_tail(lines):
    if not os.path.exists(LOG_PATH):
        raise RuntimeError("log not found: " + LOG_PATH)
    with open(LOG_PATH, "r", encoding="utf-8", errors="replace") as handle:
        content = handle.readlines()
    return {
        "path": LOG_PATH,
        "count": min(lines, len(content)),
        "cursor": os.path.getsize(LOG_PATH),
        "lines": [l.rstrip("\n") for l in content[-lines:]],
    }


def log_watch(pattern, regex=False, timeout_ms=15000, since=None, case_sensitive=False):
    """Waits for the first log line matching `pattern` and returns it. Without 'since' it only looks at lines
    written from now on; pass the cursor returned by log_tail/log_watch to continue from a known point. The
    returned cursor is the byte offset right after the matched line, so a later call still sees the lines that
    were already buffered after it."""
    if not os.path.exists(LOG_PATH):
        raise RuntimeError("log not found: " + LOG_PATH)
    timeout_ms = max(100, min(60000, int(timeout_ms)))
    started = time.monotonic()
    deadline = started + timeout_ms / 1000.0
    flags = 0 if case_sensitive else re.IGNORECASE
    matcher = re.compile(pattern, flags) if regex else None
    needle = pattern if case_sensitive else pattern.lower()
    size = os.path.getsize(LOG_PATH)
    offset = size if since is None else max(0, int(since))
    if offset > size:
        offset = 0
    buf = b""
    line_start = offset
    while True:
        if os.path.exists(LOG_PATH):
            size = os.path.getsize(LOG_PATH)
            if size < offset:
                offset = 0
                line_start = 0
                buf = b""
            if size > offset:
                with open(LOG_PATH, "rb") as handle:
                    handle.seek(offset)
                    chunk = handle.read()
                offset += len(chunk)
                buf += chunk
                while b"\n" in buf:
                    index = buf.index(b"\n")
                    raw = buf[:index]
                    line_end = line_start + index + 1
                    line = raw.decode("utf-8", errors="replace").rstrip("\r")
                    buf = buf[index + 1:]
                    line_start = line_end
                    hit = bool(matcher.search(line)) if matcher else (needle in (line if case_sensitive else line.lower()))
                    if hit:
                        return {
                            "matched": True,
                            "pattern": pattern,
                            "count": 1,
                            "lines": [line],
                            "cursor": line_end,
                            "elapsed_ms": int((time.monotonic() - started) * 1000),
                        }
        if time.monotonic() >= deadline:
            return {
                "matched": False,
                "pattern": pattern,
                "count": 0,
                "lines": [],
                "cursor": offset,
                "elapsed_ms": int((time.monotonic() - started) * 1000),
                "timeout_ms": timeout_ms,
            }
        time.sleep(0.2)


def _wait_for_file(path, timeout=4.0):
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        if os.path.exists(path) and os.path.getsize(path) > 0:
            return True
        time.sleep(0.1)
    return os.path.exists(path) and os.path.getsize(path) > 0


def _image_result(path):
    with open(path, "rb") as handle:
        encoded = base64.b64encode(handle.read()).decode("ascii")
    return [
        {"type": "image", "data": encoded, "mimeType": "image/png"},
        {"type": "text", "text": path},
    ]


def screenshot(args):
    data = require_ok(call_bridge("screenshot", **({"path": args["path"]} if args.get("path") else {})))
    path = data["path"]
    if not _wait_for_file(path):
        raise RuntimeError("the game did not write the screenshot yet: " + path)
    return _image_result(path)


def render_view(args):
    payload = {"x": args["x"], "z": args["z"]}
    for key in ("y", "yaw", "pitch", "dist", "size"):
        if key in args:
            payload[key] = args[key]
    data = require_ok(call_bridge("render.view", **payload))
    path = data["path"]
    if not _wait_for_file(path):
        raise RuntimeError("the game did not write the render yet: " + path)
    return _image_result(path)


# ----------------------------------------------------------------------------------------------------------------
# Tools
# ----------------------------------------------------------------------------------------------------------------

TOOLS = [
    {
        "name": "devbridge_ping",
        "description": "Checks the in-game bridge: bridge version, mod version, whether a world is loaded.",
        "inputSchema": {"type": "object", "properties": {}, "additionalProperties": False},
    },
    {
        "name": "game_status",
        "description": "Player name, position, health, stamina, biome, day and world name.",
        "inputSchema": {"type": "object", "properties": {}, "additionalProperties": False},
    },
    {
        "name": "game_console",
        "description": (
            "Runs a Valheim console command exactly as typed in the F5 console and returns what it printed. Dev "
            "commands (cheats) are enabled first when needed - 'devcommands' is a toggle, so the bridge only sends it "
            "once. Examples: 'spawn PickaxeBlackMetal 5', 'spawn AxeBlackmetal', 'tod 0.5' (midday), 'god on', "
            "'heal', 'ghost on', 'env Clear', 'find Beech1', 'help'. 'spawn' drops the item at the player's feet "
            "(use player_give to put it in the inventory); the reply also says whether the prefab exists."
        ),
        "inputSchema": {
            "type": "object",
            "properties": {
                "command": {"type": "string", "description": "e.g. 'spawn PickaxeBlackMetal 5' or 'tod 0.5'"},
                "cheats": {
                    "type": "boolean",
                    "default": True,
                    "description": "enable dev commands first (needed by spawn/tod/god)",
                },
            },
            "required": ["command"],
            "additionalProperties": False,
        },
    },
    {
        "name": "player_give",
        "description": "Adds an item to the player's inventory.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "item": {"type": "string", "description": "prefab name, e.g. 'Coins' or 'Bronze'"},
                "amount": {"type": "integer", "minimum": 1, "default": 1},
            },
            "required": ["item"],
            "additionalProperties": False,
        },
    },
    {
        "name": "player_teleport",
        "description": "Moves the player to world coordinates.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "x": {"type": "number"},
                "y": {"type": "number"},
                "z": {"type": "number"},
            },
            "required": ["x", "y", "z"],
            "additionalProperties": False,
        },
    },
    {
        "name": "player_look",
        "description": "Turns the player's view (the build order takes its hint from the aim ray). 'down' is degrees to pitch down; the answer includes where the aim lands.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "down": {"type": "number", "description": "degrees to pitch down (negative looks up)"},
                "right": {"type": "number", "description": "degrees to turn right"},
            },
            "additionalProperties": False,
        },
    },
    {
        "name": "player_kill",
        "description": "Kills the player once (no god mode). Useful to test death/respawn flows quickly.",
        "inputSchema": {"type": "object", "properties": {}, "additionalProperties": False},
    },
    {
        "name": "player_inventory",
        "description": (
            "Reads the player's inventory, or counts one item. With 'set' it removes every stack of that item "
            "and adds exactly that many (destructive for that item only)."
        ),
        "inputSchema": {
            "type": "object",
            "properties": {
                "item": {"type": "string", "description": "prefab name, e.g. 'Coins'"},
                "set": {"type": "integer", "minimum": 0, "description": "exact amount to leave (requires item)"},
            },
            "additionalProperties": False,
        },
    },
    {
        "name": "world_spawn",
        "description": "Spawns a prefab near the player (or at the given coordinates).",
        "inputSchema": {
            "type": "object",
            "properties": {
                "prefab": {"type": "string", "description": "e.g. 'Troll', 'Rock_3', 'Wood'"},
                "count": {"type": "integer", "minimum": 1, "maximum": 20, "default": 1},
                "x": {"type": "number"},
                "y": {"type": "number"},
                "z": {"type": "number"},
            },
            "required": ["prefab"],
            "additionalProperties": False,
        },
    },
    {
        "name": "world_aim",
        "description": "Raycasts the crosshair and reports what is under it (name, distance, point, whether it is a tree or which creature).",
        "inputSchema": {
            "type": "object",
            "properties": {"max": {"type": "number", "minimum": 1, "maximum": 200, "default": 100}},
            "additionalProperties": False,
        },
    },
    {
        "name": "world_nearest",
        "description": "Finds the nearest tree or creature around the player (kind 'tree' or 'creature') - the target to aim at.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "kind": {"type": "string", "enum": ["tree", "creature"], "default": "tree"},
                "max": {"type": "number", "minimum": 1, "maximum": 300, "default": 100},
            },
            "additionalProperties": False,
        },
    },
    {
        "name": "world_prefabs",
        "description": "Lists the prefab names the world knows, filtered by a substring - the exact name for 'spawn' (e.g. filter 'axe' shows AxeBlackmetal, AxeBronze...).",
        "inputSchema": {
            "type": "object",
            "properties": {
                "filter": {"type": "string", "description": "case-insensitive substring, empty = everything"},
                "limit": {"type": "integer", "minimum": 1, "maximum": 500, "default": 60},
            },
            "additionalProperties": False,
        },
    },
    {
        "name": "build_inspect",
        "description": (
            "Reads a buildable piece from the live game (E0 of the autonomous building plan): the recipe "
            "(m_resources), the crafting station it needs, every placement flag of Piece (groundOnly, groundPiece, "
            "noInWater, notOnWood, onlyInBiome, noClipping...), the space/connection rules, WearNTear (supports, "
            "material, health), the private-area radius, whether it is in the player's hammer table and whether the "
            "player knows the recipe. The flags are not published anywhere - they live in the asset bundle - so this "
            "is how the real requirements are known instead of guessed."
        ),
        "inputSchema": {
            "type": "object",
            "properties": {
                "prefab": {"type": "string", "description": "prefab name, e.g. 'piece_workbench' or 'piece_chest_wood'"},
            },
            "required": ["prefab"],
            "additionalProperties": False,
        },
    },
    {
        "name": "build_sites",
        "description": (
            "Tests candidate spots for a buildable piece without placing anything (E0): rings of points around the "
            "player (or around x/y/z) up to 'radius', each with a stable status name (valid, no_ground, in_water, "
            "need_cultivated, not_on_wood, too_steep, no_build_zone, ward, needs_station, blocked_by_player...) plus "
            "the ground facts (heightmap, normal, drop, biome, station in range, ward). Use it to learn where the "
            "companion would be able to place a piece."
        ),
        "inputSchema": {
            "type": "object",
            "properties": {
                "prefab": {"type": "string", "description": "prefab name, e.g. 'piece_workbench'"},
                "x": {"type": "number", "description": "centre (default: the player)"},
                "y": {"type": "number"},
                "z": {"type": "number"},
                "radius": {"type": "number", "minimum": 0.5, "maximum": 40, "default": 4},
                "rings": {"type": "integer", "minimum": 1, "maximum": 8, "default": 2},
                "dirs": {"type": "integer", "minimum": 1, "maximum": 24, "default": 8},
            },
            "required": ["prefab"],
            "additionalProperties": False,
        },
    },
    {
        "name": "build_usable",
        "description": "Is the workbench in range of a point USABLE? (the game's CraftingStation.CheckUsable: the roof rule - 70% cover - that placing a piece does not ask for).",
        "inputSchema": {
            "type": "object",
            "properties": {
                "x": {"type": "number"}, "y": {"type": "number"}, "z": {"type": "number"},
            },
            "additionalProperties": False,
        },
    },
    {
        "name": "terrain_check",
        "description": "How uneven is the ground over a square area? (E10: the sampling the build order does before leveling, with the median height it would level to).",
        "inputSchema": {
            "type": "object",
            "properties": {
                "x": {"type": "number"}, "y": {"type": "number"}, "z": {"type": "number"},
                "size": {"type": "number", "description": "side of the square in metres (default 8)"},
                "tolerance": {"type": "number"},
            },
            "additionalProperties": False,
        },
    },
    {
        "name": "build_hoe",
        "description": "The hoe's terrain pieces (the game's own leveling operations) and the prefab its attack spawns.",
        "inputSchema": {"type": "object", "properties": {}, "additionalProperties": False},
    },
    {
        "name": "build_check",
        "description": "Would the game accept this exact pose? (E6: the y is NOT snapped to the ground, so a wall on a floor can be checked).",
        "inputSchema": {
            "type": "object",
            "properties": {
                "prefab": {"type": "string"},
                "x": {"type": "number"}, "y": {"type": "number"}, "z": {"type": "number"},
                "yaw": {"type": "number", "description": "degrees around Y"},
            },
            "required": ["prefab", "x", "y", "z"],
            "additionalProperties": False,
        },
    },
    {
        "name": "build_snaps",
        "description": "The piece's snap points (Piece.GetSnapPoints) in the prefab space - how the game lines a roof onto a wall.",
        "inputSchema": {
            "type": "object",
            "properties": {"prefab": {"type": "string"}},
            "required": ["prefab"],
            "additionalProperties": False,
        },
    },
    {
        "name": "build_find",
        "description": (
            "Where the piece would go (E2): runs the real placement search from a hint - the player's aim when no "
            "coordinates are given - and returns the chosen pose, the distance from the hint, how many candidates "
            "were tried and how many failed for each reason (clipping, no_ground, no_build_zone, ward, "
            "needs_station...). It does NOT place anything. This is the 'aqui' of the build order."
        ),
        "inputSchema": {
            "type": "object",
            "properties": {
                "prefab": {"type": "string", "description": "prefab name, e.g. 'piece_workbench'"},
                "x": {"type": "number", "description": "hint (default: what the player is looking at, else 3 m ahead)"},
                "y": {"type": "number"},
                "z": {"type": "number"},
                "radius": {"type": "number", "minimum": 1, "maximum": 40, "default": 8},
                "rings": {"type": "integer", "minimum": 1, "maximum": 8, "default": 3},
                "dirs": {"type": "integer", "minimum": 4, "maximum": 24, "default": 8},
            },
            "required": ["prefab"],
            "additionalProperties": False,
        },
    },
    {
        "name": "companions_sethome",
        "description": "Sets the companion's home where it stands (the wheel's set-home action), so the build leash ([Building] MaxDistanceFromHome) can be tested from the outside.",
        "inputSchema": {
            "type": "object",
            "properties": {"name": {"type": "string", "description": "companion name; default: the first one"}},
            "additionalProperties": False,
        },
    },
    {
        "name": "companions_level",
        "description": "Gives a companion a leveling job without the LLM (E10): it walks to each point of the area and swings the hoe there, pass by pass, until the ground is flat enough. It needs a hoe in its bag.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "size": {"type": "number", "description": "side of the square in metres (default 8)"},
                "x": {"type": "number"}, "y": {"type": "number"}, "z": {"type": "number"},
                "name": {"type": "string", "description": "companion name; default: the first one"},
            },
            "additionalProperties": False,
        },
    },
    {
        "name": "companions_build",
        "description": (
            "Gives a companion a build order without the LLM (Trilha E, E3): it checks the recipe, finds a valid spot "
            "near the player's aim, walks there, pays the materials from its own bag, places the piece and verifies "
            "it. The piece must be in the allow-list (piece_workbench, piece_chest_wood, fire_pit, bed, piece_bed02)."
            " With 'plan' it builds a whole TEMPLATE instead (E6: hut = 2x2 floor + 4 walls + gable roof; "
            "canopy = 4 poles + 4 roofs over the bench). With 'blueprint' it builds a .blueprint from the player's "
            "library (E7: BepInEx/plugins/AI-Companions/blueprints), centered on the hint/aim."
            " A blueprint is ONE site handed to a GROUP: with no 'name' EVERY companion of the player joins it and "
            "the pieces are shared between them; with 'name' only that companion works it (a piece or a plan order "
            "is always one companion). The answer carries 'workers' (how many joined the site; 0 = none) and, ONLY "
            "when nothing started, 'refused' - one of 'no companion in range', 'the site is busy', 'missing station: "
            "<x>', 'missing piece: <x>' or 'not started' ('started' means workers > 0). A nameless blueprint with "
            "nobody to work is that normal answer (started false, workers 0, refused 'no companion in range'), "
            "while a 'name' nobody has is still an error."
        ),
        "inputSchema": {
            "type": "object",
            "properties": {
                "prefab": {"type": "string", "description": "e.g. 'piece_workbench' (omit when using plan/blueprint)"},
                "plan": {"type": "string", "description": "a template: 'hut' or 'canopy'"},
                "blueprint": {"type": "string", "description": "a .blueprint name or full path (E7); one site for the whole group"},
                "x": {"type": "number"}, "y": {"type": "number"}, "z": {"type": "number"},
                "name": {"type": "string", "description": "companion name; default: every companion of the player on one blueprint site (the group), else the first one"},
            },
            "required": [],
            "additionalProperties": False,
        },
    },
    {
        "name": "build_pieces",
        "description": (
            "Lists the build pieces already standing near a point (default: the player): prefab, name, creator, "
            "whether the creator is the local player, distance, position, valid ZDO, health and the crafting station "
            "it carries. Use it to prove a placement (or a relog) from the outside, and to check the companion's "
            "builds."
        ),
        "inputSchema": {
            "type": "object",
            "properties": {
                "x": {"type": "number", "description": "centre (default: the player)"},
                "y": {"type": "number"},
                "z": {"type": "number"},
                "radius": {"type": "number", "minimum": 0.5, "maximum": 100, "default": 10},
                "filter": {"type": "string", "description": "case-insensitive substring of the prefab or piece name"},
            },
            "additionalProperties": False,
        },
    },
    {
        "name": "blueprints_list",
        "description": (
            "Lists the .blueprint library the build order reads (BepInEx/plugins/AI-Companions/blueprints): the "
            "folder, how many files there are and, for each one, the file name, size in bytes, line count and "
            "modification time. It is how the player sees which builds are available BEFORE giving an order. "
            "Read-only: it touches neither the world nor any companion."
        ),
        "inputSchema": {"type": "object", "properties": {}, "additionalProperties": False},
    },
    {
        "name": "blueprints_inspect",
        "description": (
            "Parses ONE .blueprint of the library and answers what the build order WOULD do with it, without giving "
            "it: the pieces it would build, how many lines it would SKIP and why (skippedReasons: prefab, reason, "
            "count - e.g. stone_floor_2x2 refused with 'recipe not unlocked'), the crafting stations the pieces need "
            "(stations: token -> count, e.g. $piece_workbench), the prefab counts, the bounding box, how many storeys "
            "the plan has and how many pieces need a station in range. It is the same parse the order does "
            "(BuildBlueprintParser), so the numbers match what the companion would try to place. Read-only: nothing "
            "is placed and no companion is ordered."
        ),
        "inputSchema": {
            "type": "object",
            "properties": {
                "file": {"type": "string", "description": "a .blueprint name (e.g. 'CommHomeBasicBP.blueprint') or a full path"},
            },
            "required": ["file"],
            "additionalProperties": False,
        },
    },
    {
        "name": "world_setup",
        "description": (
            "Dev setup for driving the player: enables dev commands (once), sets the time of day (0.5 = midday), "
            "turns god mode on (or off with god=false) and keeps the player's stamina full (stamina=false turns that off)."
        ),
        "inputSchema": {
            "type": "object",
            "properties": {
                "time": {"type": "number", "minimum": 0, "maximum": 0.999, "default": 0.5},
                "god": {"type": "boolean", "default": True},
                "stamina": {"type": "boolean", "default": True},
            },
            "additionalProperties": False,
        },
    },
    {
        "name": "world_time",
        "description": (
            "Reads/sets the world clock. 'day' is the 0-based EnvMan day, 'time' is seconds into the day "
            "(the day is 1200 s), 'fraction' is 0..1 (0.5 = midday) and 'skip' advances/rewinds by game seconds."
        ),
        "inputSchema": {
            "type": "object",
            "properties": {
                "day": {"type": "integer", "minimum": 0},
                "time": {"type": "number", "minimum": 0, "description": "seconds into the day"},
                "fraction": {"type": "number", "minimum": 0, "maximum": 1},
                "skip": {"type": "number", "description": "relative change in game seconds"},
            },
            "additionalProperties": False,
        },
    },
    {
        "name": "screenshot",
        "description": "Takes a screenshot of the game and returns it as an image (plus the file path).",
        "inputSchema": {
            "type": "object",
            "properties": {"path": {"type": "string", "description": "optional absolute .png path"}},
            "additionalProperties": False,
        },
    },
    {
        "name": "render_view",
        "description": (
            "Renders the world from an independent off-screen camera (the player's view is not touched) and returns "
            "the PNG. (x, z) is the point to look at; yaw/pitch orbit the camera around it and dist is how far it sits."
        ),
        "inputSchema": {
            "type": "object",
            "properties": {
                "x": {"type": "number"},
                "z": {"type": "number"},
                "y": {"type": "number", "description": "default: the player's height"},
                "yaw": {"type": "number", "default": 0},
                "pitch": {"type": "number", "default": 15},
                "dist": {"type": "number", "default": 8},
                "size": {"type": "integer", "minimum": 128, "maximum": 2048, "default": 768},
            },
            "required": ["x", "z"],
            "additionalProperties": False,
        },
    },
    {
        "name": "input_focus",
        "description": "Brings the Valheim window to the foreground. The input tools do it automatically; use this to check the focus.",
        "inputSchema": {"type": "object", "properties": {}, "additionalProperties": False},
    },
    {
        "name": "input_key",
        "description": "Sends a key tap to the game (KeyCode name: Escape, F8, Return, W, Alpha1...). Focuses the game first.",
        "inputSchema": {
            "type": "object",
            "properties": {"key": {"type": "string"}},
            "required": ["key"],
            "additionalProperties": False,
        },
    },
    {
        "name": "input_key_down",
        "description": "Holds a key down (for holds: the command wheel, sprint...). Pair with input_key_up.",
        "inputSchema": {
            "type": "object",
            "properties": {"key": {"type": "string"}},
            "required": ["key"],
            "additionalProperties": False,
        },
    },
    {
        "name": "input_key_up",
        "description": "Releases a key held with input_key_down.",
        "inputSchema": {
            "type": "object",
            "properties": {"key": {"type": "string"}},
            "required": ["key"],
            "additionalProperties": False,
        },
    },
    {
        "name": "input_text",
        "description": "Types text into the focused game field (Unicode: accents work).",
        "inputSchema": {
            "type": "object",
            "properties": {"text": {"type": "string"}},
            "required": ["text"],
            "additionalProperties": False,
        },
    },
    {
        "name": "input_click",
        "description": "Left-clicks at game screen pixels (x, y) - the screenshot uses the same coordinates.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "x": {"type": "integer", "minimum": 0},
                "y": {"type": "integer", "minimum": 0},
            },
            "required": ["x", "y"],
            "additionalProperties": False,
        },
    },
    {
        "name": "input_mouse_down",
        "description": "Presses the left mouse button at client (x, y) and keeps it down - pair with input_mouse_up across a few frames for a click the UI cannot miss.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "x": {"type": "integer", "minimum": 0},
                "y": {"type": "integer", "minimum": 0},
            },
            "required": ["x", "y"],
            "additionalProperties": False,
        },
    },
    {
        "name": "input_mouse_up",
        "description": "Releases the left mouse button at client (x, y).",
        "inputSchema": {
            "type": "object",
            "properties": {
                "x": {"type": "integer", "minimum": 0},
                "y": {"type": "integer", "minimum": 0},
            },
            "required": ["x", "y"],
            "additionalProperties": False,
        },
    },
    {
        "name": "input_mouse_move",
        "description": "Turns the camera by a mouse delta (dx, dy in pixels): the game reads <Mouse>/delta, so this is how the player looks around. The delta lasts one frame.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "dx": {"type": "integer"},
                "dy": {"type": "integer"},
            },
            "required": ["dx", "dy"],
            "additionalProperties": False,
        },
    },
    {
        "name": "world_enter",
        "description": "Skips the intro and enters the world (Space > Down > Enter > Enter > Enter) and waits for it to load. Then runs the dev setup (devcommands, tod 0.5, god on) so the agent can drive the player without dying.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "wait": {"type": "integer", "minimum": 10, "maximum": 300, "default": 120},
                "delay": {"type": "integer", "minimum": 0, "maximum": 180, "default": 0},
                "setup": {"type": "boolean", "default": True, "description": "run devcommands + tod 0.5 + god on after loading"},
                "time": {"type": "number", "minimum": 0, "maximum": 0.999, "default": 0.5},
            },
            "additionalProperties": False,
        },
    },
    {
        "name": "log_tail",
        "description": "Returns the last lines of BepInEx\\LogOutput.log (works even with the game closed).",
        "inputSchema": {
            "type": "object",
            "properties": {"lines": {"type": "integer", "minimum": 1, "maximum": 2000, "default": 80}},
            "additionalProperties": False,
        },
    },
    {
        "name": "log_watch",
        "description": (
            "Waits for a line to appear in LogOutput.log (literal or regex) and returns it. Starts at the "
            "current end unless 'since' is the cursor from log_tail/log_watch, so an action can be triggered first."
        ),
        "inputSchema": {
            "type": "object",
            "properties": {
                "pattern": {"type": "string"},
                "regex": {"type": "boolean", "default": False},
                "timeout_ms": {"type": "integer", "minimum": 100, "maximum": 60000, "default": 15000},
                "since": {"type": "integer", "minimum": 0, "description": "byte cursor returned by log_tail"},
                "case_sensitive": {"type": "boolean", "default": False},
            },
            "required": ["pattern"],
            "additionalProperties": False,
        },
    },
    {
        "name": "companions_list",
        "description": "Lists every companion: name, position, health, mode, stance, auto-pickup, recovering, owner.",
        "inputSchema": {"type": "object", "properties": {}, "additionalProperties": False},
    },
    {
        "name": "companions_spawn",
        "description": "Summons a free companion next to the player (the AI panel's spawn, no coins).",
        "inputSchema": {"type": "object", "properties": {}, "additionalProperties": False},
    },
    {
        "name": "companions_order",
        "description": (
            "Gives a companion an order: a work mode (follow, wood, stone, ore, forage, smelt, hunt, farm, fish, "
            "repair, restock, cook, stay), a stance (stance_balanced, stance_passive, stance_ranged, stance_melee), "
            "autopickup_on / autopickup_off, or the stop order (stop / cancel / para), which cancels the companion's "
            "build and leveling; an empty name stops every companion and answers the list."
        ),
        "inputSchema": {
            "type": "object",
            "properties": {
                "action": {"type": "string"},
                "name": {"type": "string", "description": "optional companion name (contains, case-insensitive)"},
            },
            "required": ["action"],
            "additionalProperties": False,
        },
    },
    {
        "name": "companions_teleport",
        "description": "Moves a companion to the player (default) or to the given coordinates.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "name": {"type": "string"},
                "x": {"type": "number"},
                "y": {"type": "number"},
                "z": {"type": "number"},
            },
            "additionalProperties": False,
        },
    },
    {
        "name": "companions_give",
        "description": (
            "Adds an item to a companion's inventory. A NEGATIVE amount takes it away again (a test that overfed the "
            "companion leaves it encumbered and unable to move: 150 wood is 300 weight, the carry limit)."
        ),
        "inputSchema": {
            "type": "object",
            "properties": {
                "item": {"type": "string"},
                "amount": {"type": "integer", "default": 1},
                "name": {"type": "string"},
            },
            "required": ["item"],
            "additionalProperties": False,
        },
    },
    {
        "name": "companions_zdo",
        "description": "Dumps the companion's ZDO keys (HC_*) - the fastest way to see a stuck state.",
        "inputSchema": {
            "type": "object",
            "properties": {"name": {"type": "string"}},
            "additionalProperties": False,
        },
    },
    {
        "name": "companions_kill",
        "description": "Kills a companion on purpose (lethal damage), for tombstone/recovery tests.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "name": {"type": "string", "description": "optional companion name (contains, case-insensitive)"},
                "damage": {"type": "number", "minimum": 1, "description": "default: enough to kill"},
            },
            "additionalProperties": False,
        },
    },
    {
        "name": "companions_dismiss",
        "description": "Dismisses a companion exactly like the radial menu (gear goes to a tombstone, the body is removed) - for tombstone tests.",
        "inputSchema": {
            "type": "object",
            "properties": {"name": {"type": "string", "description": "companion name (contains, case-insensitive)"}},
            "required": ["name"],
            "additionalProperties": False,
        },
    },
    {
        "name": "companions_inventory",
        "description": (
            "Lists what a companion is carrying (prefab, name, amount, grid slot). "
            "`action: clear` empties the bag WITHOUT dropping (the weight test tool of the build recipe): it is what "
            "MHR_Build cooks use, and it used to be unreachable from here because the schema had no `action`."
        ),
        "inputSchema": {
            "type": "object",
            "properties": {
                "name": {"type": "string", "description": "companion name (contains, case-insensitive)"},
                "action": {"type": "string", "enum": ["clear"], "description": "clear = empty the bag without dropping"},
            },
            "required": ["name"],
            "additionalProperties": False,
        },
    },
    {
        "name": "world_tombstones",
        "description": (
            "Lists the tombstones loaded around the player with what decides the vanilla despawn: item count, ZDO "
            "owner (isOwner), whether the container is in use, plus the companion tombstone id and its respawn data."
        ),
        "inputSchema": {"type": "object", "properties": {}, "additionalProperties": False},
    },
    {
        "name": "world_tombstone_take",
        "description": "Loots the nearest tombstone with items through the game's own TombStone.Interact (what the E key calls): takes everything when it fits.",
        "inputSchema": {"type": "object", "properties": {}, "additionalProperties": False},
    },
    {
        "name": "world_tombstone_destroy",
        "description": "Destroys the companion tombstones around the player (dev cleanup for tests); includePlayer also removes the player's own tombstones.",
        "inputSchema": {
            "type": "object",
            "properties": {"includePlayer": {"type": "boolean", "default": False}},
            "additionalProperties": False,
        },
    },
    {
        "name": "world_tombstone_diag",
        "description": "Diagnostics for the companion tombstone despawn (component enabled, IsInvoking, ZNetView valid, owner, in use, items); tick=true forces the game's private UpdateDespawn.",
        "inputSchema": {
            "type": "object",
            "properties": {"tick": {"type": "boolean", "default": False}},
            "additionalProperties": False,
        },
    },
    {
        "name": "companions_tier",
        "description": (
            "Changes a companion's tier through CompanionManager.ChangeTier (the same swap the AI panel does): "
            "companion, warrior, mage, fire, ice, support, or a prefab name."
        ),
        "inputSchema": {
            "type": "object",
            "properties": {
                "tier": {"type": "string"},
                "name": {"type": "string", "description": "optional companion name (contains, case-insensitive)"},
            },
            "required": ["tier"],
            "additionalProperties": False,
        },
    },
    {
        "name": "companions_hire",
        "description": (
            "Runs the shrine's PAID summon path (CompanionManager.HireWithCoins): checks the offering in coins, "
            "spawns the companion and takes the coins. It does not open the shrine panel."
        ),
        "inputSchema": {"type": "object", "properties": {}, "additionalProperties": False},
    },
    {
        "name": "companions_resurrect",
        "description": "Resurrects a dead companion from its own tombstone (the tombstone's respawn path, no hold).",
        "inputSchema": {
            "type": "object",
            "properties": {"name": {"type": "string", "description": "optional tombstone/companion name"}},
            "additionalProperties": False,
        },
    },
    {
        "name": "companions_farm_zone",
        "description": (
            "Lists, adds or clears the companion's planting zones (HC_FarmZones). 'add' defaults to the player's "
            "position, 4 m half-size and no crop; 'crop' is a seed prefab such as 'CarrotSeeds'."
        ),
        "inputSchema": {
            "type": "object",
            "properties": {
                "action": {"type": "string", "enum": ["add", "list", "clear"], "default": "list"},
                "name": {"type": "string"},
                "x": {"type": "number"},
                "y": {"type": "number"},
                "z": {"type": "number"},
                "halfSize": {"type": "number", "minimum": 0.5, "default": 4},
                "crop": {"type": "string"},
            },
            "additionalProperties": False,
        },
    },
    {
        "name": "nav_tiles",
        "description": (
            "Navigation instrumentation (P2): the navmesh tiles near a point (default: the player) - id, centre, "
            "whether the tile already carries the poke/build flood data and whether its instance is still valid. It "
            "is how the nav grid the companion walks on is read from the outside, tile by tile."
        ),
        "inputSchema": {
            "type": "object",
            "properties": {
                "x": {"type": "number", "description": "centre (default: the player)"},
                "y": {"type": "number"},
                "z": {"type": "number"},
                "radius": {"type": "number", "minimum": 1, "maximum": 120, "default": 40},
            },
            "additionalProperties": False,
        },
    },
    {
        "name": "nav_probe",
        "description": (
            "Navigation instrumentation (P2): probes the navmesh around a point (default: the player) and answers "
            "whether a tile was found, how far it is and where it is. It separates 'there is no navmesh here' from "
            "'the mover gave up' when a companion refuses to walk somewhere."
        ),
        "inputSchema": {
            "type": "object",
            "properties": {
                "x": {"type": "number", "description": "point to probe (default: the player)"},
                "y": {"type": "number"},
                "z": {"type": "number"},
                "range": {"type": "number", "minimum": 0.5, "maximum": 50, "default": 3},
            },
            "additionalProperties": False,
        },
    },
    {
        "name": "nav_stats",
        "description": (
            "Navigation instrumentation (P2) counters: how many probes (pokes), how many grid rebuilds and the worst "
            "collection time in ms since the world loaded - the measured cost of the nav instrumentation."
        ),
        "inputSchema": {"type": "object", "properties": {}, "additionalProperties": False},
    },
]

TOOL_NAMES = {tool["name"] for tool in TOOLS}


def run_tool(name, args):
    if name == "devbridge_ping":
        return require_ok(call_bridge("ping"))
    if name == "game_status":
        return require_ok(call_bridge("status"))
    if name == "game_console":
        return require_ok(call_bridge("console", command=args["command"], cheats=args.get("cheats", True)))
    if name == "player_give":
        return require_ok(call_bridge("player.give", item=args["item"], amount=args.get("amount", 1)))
    if name == "player_teleport":
        return require_ok(call_bridge("player.teleport", x=args["x"], y=args["y"], z=args["z"]))
    if name == "player_look":
        payload = {}
        if "down" in args:
            payload["down"] = args["down"]
        if "right" in args:
            payload["right"] = args["right"]
        return require_ok(call_bridge("player.look", **payload))
    if name == "player_kill":
        return require_ok(call_bridge("player.kill"))
    if name == "player_inventory":
        payload = {}
        if args.get("item"):
            payload["item"] = args["item"]
        if "set" in args:
            payload["set"] = args["set"]
        return require_ok(call_bridge("player.inventory", **payload))
    if name == "world_spawn":
        payload = {"prefab": args["prefab"], "count": args.get("count", 1)}
        for key in ("x", "y", "z"):
            if key in args:
                payload[key] = args[key]
        return require_ok(call_bridge("world.spawn", **payload))
    if name == "world_aim":
        return require_ok(call_bridge("world.aim", max=args.get("max", 100)))
    if name == "world_nearest":
        return require_ok(call_bridge("world.nearest", kind=args.get("kind", "tree"), max=args.get("max", 100)))
    if name == "world_prefabs":
        return require_ok(call_bridge("world.prefabs", filter=args.get("filter", ""), limit=args.get("limit", 60)))
    if name == "build_inspect":
        return require_ok(call_bridge("build.inspect", prefab=args["prefab"]))
    if name == "build_sites":
        payload = {"prefab": args["prefab"], "radius": args.get("radius", 4), "rings": args.get("rings", 2), "dirs": args.get("dirs", 8)}
        for key in ("x", "y", "z"):
            if key in args:
                payload[key] = args[key]
        return require_ok(call_bridge("build.sites", **payload))
    if name == "build_usable":
        payload = {}
        for key in ("x", "y", "z"):
            if key in args:
                payload[key] = args[key]
        return require_ok(call_bridge("build.usable", **payload))
    if name == "terrain_check":
        payload = {"size": args.get("size", 8)}
        for key in ("x", "y", "z", "tolerance"):
            if key in args:
                payload[key] = args[key]
        return require_ok(call_bridge("terrain.check", **payload))
    if name == "build_hoe":
        return require_ok(call_bridge("build.hoe"))
    if name == "build_check":
        return require_ok(call_bridge("build.check", prefab=args["prefab"], x=args["x"], y=args["y"], z=args["z"],
                                      **({"yaw": args["yaw"]} if "yaw" in args else {})))
    if name == "build_snaps":
        return require_ok(call_bridge("build.snaps", prefab=args["prefab"]))
    if name == "build_find":
        payload = {"prefab": args["prefab"], "radius": args.get("radius", 8), "rings": args.get("rings", 3), "dirs": args.get("dirs", 8)}
        for key in ("x", "y", "z"):
            if key in args:
                payload[key] = args[key]
        return require_ok(call_bridge("build.find", **payload))
    if name == "companions_sethome":
        payload = {}
        if args.get("name"):
            payload["name"] = args["name"]
        return require_ok(call_bridge("companions.sethome", **payload))
    if name == "companions_level":
        payload = {"size": args.get("size", 8)}
        for key in ("x", "y", "z", "name"):
            if key in args:
                payload[key] = args[key]
        return require_ok(call_bridge("companions.level", **payload))
    if name == "companions_build":
        payload = {}
        if args.get("prefab"):
            payload["prefab"] = args["prefab"]
        if args.get("plan"):
            payload["plan"] = args["plan"]
        if args.get("blueprint"):
            payload["blueprint"] = args["blueprint"]
        if args.get("name"):
            payload["name"] = args["name"]
        for key in ("x", "y", "z"):
            if key in args:
                payload[key] = args[key]
        return require_ok(call_bridge("companions.build", **payload))
    if name == "build_pieces":
        payload = {"radius": args.get("radius", 10)}
        if args.get("filter"):
            payload["filter"] = args["filter"]
        for key in ("x", "y", "z"):
            if key in args:
                payload[key] = args[key]
        return require_ok(call_bridge("build.pieces", **payload))
    if name == "blueprints_list":
        return require_ok(call_bridge("blueprints.list"))
    if name == "blueprints_inspect":
        return require_ok(call_bridge("blueprints.inspect", file=args["file"]))
    if name == "world_setup":
        payload = {
            "time": args.get("time", 0.5),
            "god": args.get("god", True),
            "stamina": args.get("stamina", True),
        }
        return require_ok(call_bridge("world.setup", **payload))
    if name == "world_time":
        payload = {}
        for key in ("day", "time", "fraction", "skip"):
            if key in args:
                payload[key] = args[key]
        return require_ok(call_bridge("world.time", **payload))
    if name == "screenshot":
        return screenshot(args)
    if name == "render_view":
        return render_view(args)
    if name == "input_focus":
        return {"focused": focus_game()}
    if name == "input_key":
        focus_game()
        return require_ok(call_bridge("input.key", key=args["key"]))
    if name == "input_key_down":
        focus_game()
        return require_ok(call_bridge("input.key_down", key=args["key"]))
    if name == "input_key_up":
        focus_game()
        return require_ok(call_bridge("input.key_up", key=args["key"]))
    if name == "input_text":
        focus_game()
        return require_ok(call_bridge("input.text", text=args["text"]))
    if name == "input_click":
        focus_game()
        return require_ok(call_bridge("input.click", x=args["x"], y=args["y"]))
    if name == "input_mouse_down":
        focus_game()
        return require_ok(call_bridge("input.mouse_down", x=args["x"], y=args["y"]))
    if name == "input_mouse_up":
        focus_game()
        return require_ok(call_bridge("input.mouse_up", x=args["x"], y=args["y"]))
    if name == "input_mouse_move":
        focus_game()
        return require_ok(call_bridge("input.mouse_move", dx=args["dx"], dy=args["dy"]))
    if name == "world_enter":
        return world_enter(args)
    if name == "log_tail":
        return log_tail(int(args.get("lines", 80)))
    if name == "log_watch":
        return log_watch(
            args["pattern"],
            regex=bool(args.get("regex", False)),
            timeout_ms=int(args.get("timeout_ms", 15000)),
            since=args.get("since"),
            case_sensitive=bool(args.get("case_sensitive", False)),
        )
    if name == "companions_list":
        return require_ok(call_bridge("companions.list"))
    if name == "companions_spawn":
        return require_ok(call_bridge("companions.spawn"))
    if name == "companions_order":
        payload = {"action": args["action"]}
        if args.get("name"):
            payload["name"] = args["name"]
        return require_ok(call_bridge("companions.order", **payload))
    if name == "companions_teleport":
        payload = {}
        if args.get("name"):
            payload["name"] = args["name"]
        for key in ("x", "y", "z"):
            if key in args:
                payload[key] = args[key]
        return require_ok(call_bridge("companions.teleport", **payload))
    if name == "companions_give":
        payload = {"item": args["item"], "amount": args.get("amount", 1)}
        if args.get("name"):
            payload["name"] = args["name"]
        return require_ok(call_bridge("companions.give", **payload))
    if name == "companions_zdo":
        payload = {}
        if args.get("name"):
            payload["name"] = args["name"]
        return require_ok(call_bridge("companions.zdo", **payload))
    if name == "companions_kill":
        payload = {}
        if args.get("name"):
            payload["name"] = args["name"]
        if args.get("damage"):
            payload["damage"] = args["damage"]
        return require_ok(call_bridge("companions.kill", **payload))
    if name == "companions_dismiss":
        return require_ok(call_bridge("companions.dismiss", name=args["name"]))
    if name == "companions_inventory":
        payload = {"name": args["name"]}
        if args.get("action"):
            payload["action"] = args["action"]
        return require_ok(call_bridge("companions.inventory", **payload))
    if name == "world_tombstones":
        return require_ok(call_bridge("world.tombstones"))
    if name == "world_tombstone_take":
        return require_ok(call_bridge("world.tombstone_take"))
    if name == "world_tombstone_destroy":
        return require_ok(call_bridge("world.tombstone_destroy", includePlayer=args.get("includePlayer", False)))
    if name == "world_tombstone_diag":
        return require_ok(call_bridge("world.tombstone_diag", tick=args.get("tick", False)))
    if name == "companions_tier":
        payload = {"tier": args["tier"]}
        if args.get("name"):
            payload["name"] = args["name"]
        return require_ok(call_bridge("companions.tier", **payload))
    if name == "companions_hire":
        return require_ok(call_bridge("companions.hire"))
    if name == "companions_resurrect":
        payload = {}
        if args.get("name"):
            payload["name"] = args["name"]
        return require_ok(call_bridge("companions.resurrect", **payload))
    if name == "companions_farm_zone":
        payload = {"action": args.get("action", "list")}
        if args.get("name"):
            payload["name"] = args["name"]
        for key in ("x", "y", "z", "halfSize", "crop"):
            if key in args:
                payload[key] = args[key]
        return require_ok(call_bridge("companions.farm_zone", **payload))
    if name == "nav_tiles":
        payload = {"radius": args.get("radius", 40)}
        for key in ("x", "y", "z"):
            if key in args:
                payload[key] = args[key]
        return require_ok(call_bridge("nav.tiles", **payload))
    if name == "nav_probe":
        payload = {"range": args.get("range", 3)}
        for key in ("x", "y", "z"):
            if key in args:
                payload[key] = args[key]
        return require_ok(call_bridge("nav.probe", **payload))
    if name == "nav_stats":
        return require_ok(call_bridge("nav.stats"))
    raise RuntimeError("unknown tool: " + name)


def tool_result(name, args):
    try:
        data = run_tool(name, args or {})
        if isinstance(data, list):  # already a content list (screenshot)
            return {"content": data}
        return {"content": [{"type": "text", "text": json.dumps(data, ensure_ascii=False, indent=2)}]}
    except Exception as exc:  # noqa: BLE001 - the message is the tool result
        return {"content": [{"type": "text", "text": "error: " + str(exc)}], "isError": True}


# ----------------------------------------------------------------------------------------------------------------
# MCP over stdio
# ----------------------------------------------------------------------------------------------------------------

def send(message):
    sys.stdout.write(json.dumps(message, ensure_ascii=False) + "\n")
    sys.stdout.flush()


def handle(request):
    method = request.get("method")
    request_id = request.get("id")
    if request_id is None:  # notification (e.g. notifications/initialized) - no reply
        return
    if method == "initialize":
        send({
            "jsonrpc": "2.0",
            "id": request_id,
            "result": {
                "protocolVersion": PROTOCOL_VERSION,
                "capabilities": {"tools": {}},
                "serverInfo": {"name": SERVER_NAME, "version": SERVER_VERSION},
            },
        })
        return
    if method == "ping":
        send({"jsonrpc": "2.0", "id": request_id, "result": {}})
        return
    if method == "tools/list":
        send({"jsonrpc": "2.0", "id": request_id, "result": {"tools": TOOLS}})
        return
    if method == "tools/call":
        params = request.get("params") or {}
        name = params.get("name")
        if name not in TOOL_NAMES:
            send({"jsonrpc": "2.0", "id": request_id,
                  "error": {"code": -32602, "message": "unknown tool: %s" % name}})
            return
        send({"jsonrpc": "2.0", "id": request_id, "result": tool_result(name, params.get("arguments"))})
        return
    send({"jsonrpc": "2.0", "id": request_id,
          "error": {"code": -32601, "message": "method not found: %s" % method}})


def main():
    for line in sys.stdin:
        line = line.strip()
        if not line:
            continue
        try:
            request = json.loads(line)
        except json.JSONDecodeError as exc:
            sys.stderr.write("bad JSON-RPC line: %s\n" % exc)
            continue
        handle(request)


if __name__ == "__main__":
    main()
