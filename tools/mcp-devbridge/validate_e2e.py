#!/usr/bin/env python3
"""End-to-end validation of the in-DLL DevBridge, with the game running.

Safe by default: only reads state and does harmless actions (a screenshot, a follow order, a console `help`).
The destructive tools (kill, tier, hire, resurrect, farm_zone) are validated in the `test3` world and only run here
with `--destructive`.

    python tools/mcp-devbridge/validate_e2e.py
    python tools/mcp-devbridge/validate_e2e.py --destructive --wait 600
"""

import argparse
import json
import os
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import mcp_devbridge as mcp  # noqa: E402

PASS = []
FAIL = []
SKIP = []


def check(name, fn):
    try:
        data = fn()
        PASS.append(name)
        print("PASS  %-42s %s" % (name, _short(data)))
        return data
    except Exception as exc:  # noqa: BLE001
        FAIL.append(name)
        print("FAIL  %-42s %s" % (name, exc))
        return None


def skip(name, why):
    SKIP.append(name)
    print("SKIP  %-42s %s" % (name, why))


def _short(data):
    text = json.dumps(data, ensure_ascii=False)
    return text if len(text) <= 110 else text[:107] + "..."


def wait_for_game(seconds):
    deadline = time.time() + seconds
    last = None
    while time.time() < deadline:
        try:
            data = mcp.require_ok(mcp.call_bridge("ping"))
            last = data
            if data.get("inGame"):
                return data
        except Exception as exc:  # noqa: BLE001
            last = {"error": str(exc)}
        time.sleep(2)
    raise RuntimeError("the world was not loaded in %d s (last: %s)" % (seconds, last))


def first_companion():
    data = mcp.require_ok(mcp.call_bridge("companions.list"))
    companions = data.get("companions") or []
    return companions[0] if companions else None


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--wait", type=int, default=300, help="seconds to wait for the world to load")
    parser.add_argument("--destructive", action="store_true", help="also run kill/tier/hire/resurrect/farm_zone")
    args = parser.parse_args()

    print("waiting for the game (world loaded)...")
    try:
        ping = wait_for_game(args.wait)
    except Exception as exc:  # noqa: BLE001
        print("FAIL  %-42s %s" % ("world loaded", exc))
        return 1
    PASS.append("world loaded")
    print("PASS  %-42s %s" % ("world loaded", _short(ping)))

    check("ping", lambda: mcp.require_ok(mcp.call_bridge("ping")))
    status = check("status", lambda: mcp.require_ok(mcp.call_bridge("status")))
    check("player_inventory (list)", lambda: mcp.require_ok(mcp.call_bridge("player.inventory")))
    check("log_tail", lambda: mcp.log_tail(10))
    check("game_console help", lambda: mcp.require_ok(mcp.call_bridge("console", command="help")))
    check("world_time (read/same)", lambda: mcp.require_ok(mcp.call_bridge("world.time")))
    check("screenshot", lambda: mcp.screenshot({}))

    listing = check("companions_list", lambda: mcp.require_ok(mcp.call_bridge("companions.list")))
    companion = first_companion()
    if companion is None:
        skip("companions_zdo", "no companion in the world")
        skip("companions_order follow", "no companion in the world")
        skip("companions_teleport", "no companion in the world")
    else:
        name = companion.get("name")
        check("companions_zdo", lambda: mcp.require_ok(mcp.call_bridge("companions.zdo", name=name)))
        check("companions_order follow", lambda: mcp.require_ok(mcp.call_bridge("companions.order", action="follow", name=name)))
        check("companions_teleport", lambda: mcp.require_ok(mcp.call_bridge("companions.teleport", name=name)))

    if args.destructive:
        check("companions_farm_zone list", lambda: mcp.require_ok(mcp.call_bridge("companions.farm_zone", action="list")))
        check("companions_tier (companion)", lambda: mcp.require_ok(mcp.call_bridge("companions.tier", tier="companion")))
        if companion is not None:
            check("companions_kill", lambda: mcp.require_ok(mcp.call_bridge("companions.kill", name=companion.get("name"))))
            check("companions_resurrect", lambda: mcp.require_ok(mcp.call_bridge("companions.resurrect")))
        else:
            skip("companions_kill", "no companion in the world")
            skip("companions_resurrect", "no companion in the world")
        check("companions_hire", lambda: mcp.require_ok(mcp.call_bridge("companions.hire")))
        check("player_kill", lambda: mcp.require_ok(mcp.call_bridge("player.kill")))
    else:
        for name in ("player_kill", "companions_kill", "companions_resurrect", "companions_tier",
                     "companions_farm_zone", "companions_hire"):
            skip(name, "destructive - validated in the test3 world; use --destructive to run here")

    print()
    print("=== %d PASS / %d FAIL / %d SKIP ===" % (len(PASS), len(FAIL), len(SKIP)))
    if FAIL:
        print("failed: " + ", ".join(FAIL))
    return 0 if not FAIL else 1


if __name__ == "__main__":
    sys.exit(main())
