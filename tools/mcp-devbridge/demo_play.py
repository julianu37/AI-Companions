#!/usr/bin/env python3
"""Drives the player through the DevBridge: walk, run, jump, look, aim and the console commands.

This is the regression test of the MCP tools that control the character (`input_key_down`/`input_key_up`,
`input_mouse_move`, `world_nearest`, `world_aim`) and of `game_console` (dev commands + `spawn` + `tod`). It needs the
game in a world with the bridge on:

    [Advanced] DevBridge = true      (AiCompanions.cfg, restart the game)

    python tools/mcp-devbridge/demo_play.py

Exit code 0 = every check passed. It walks the player around, so run it somewhere harmless.
"""

import math
import sys
import time

import mcp_devbridge as m

FAILS = []
TOTAL = []


def check(name, ok, detail=""):
    print("%s  %s%s" % ("PASS" if ok else "FAIL", name, ("  " + detail) if detail else ""))
    TOTAL.append(name)
    if not ok:
        FAILS.append(name)


def status():
    return m.require_ok(m.call_bridge("status"))


def dist(a, b):
    return math.hypot(b["x"] - a["x"], b["z"] - a["z"])


def hold(keys, seconds):
    for key in keys:
        m.require_ok(m.call_bridge("input.key_down", key=key))
    time.sleep(seconds)
    for key in reversed(keys):
        m.require_ok(m.call_bridge("input.key_up", key=key))
    time.sleep(0.4)


def norm(deg):
    while deg > 180:
        deg -= 360
    while deg < -180:
        deg += 360
    return deg


def face_clear():
    """Turns the camera until the crosshair has room (an obstacle right in front would make the walk check read
    as a control failure). Returns the aim distance of the chosen direction."""
    best = -1.0
    for _ in range(8):
        hit = m.require_ok(m.call_bridge("world.aim"))
        distance = hit.get("distance", 999.0) if hit.get("hit") else 999.0
        if distance >= 12.0:
            return distance
        best = max(best, distance)
        m.require_ok(m.call_bridge("input.mouse_move", dx=900, dy=0))  # 45 deg at 0.05 deg/px
        time.sleep(0.35)
    return best


def main():
    ping = m.require_ok(m.call_bridge("ping"))
    check("bridge is up and in a world", bool(ping.get("inGame")), str(ping.get("world")))
    if not ping.get("inGame"):
        return 1

    # Never drive the player without the dev setup: god on (Valheim 1.0 clamps a killing blow to 1 HP) + heal.
    setup = m.require_ok(m.call_bridge("world.setup", time=0.5, god=True))
    check("world setup (devcommands + midday + god)", bool(setup.get("cheats")) and bool(setup.get("god")), str(setup))

    # --- walk -------------------------------------------------------------------------------
    clear = face_clear()
    start = status()
    hold(["W"], 2.0)
    walked = dist(start, status())
    check("walking (W) moves the player", walked > 3.0, "%.2f m in 2 s (clearance %.1f m)" % (walked, clear))

    # --- run (Shift + W: the held set has to keep both keys) --------------------------------
    time.sleep(3.0)
    before = status()
    hold(["LeftShift", "W"], 2.0)
    after = status()
    ran = dist(before, after)
    spent = before["stamina"] - after["stamina"]
    check("running (Shift+W) spends stamina", spent > 5.0, "%.1f stamina, %.2f m" % (spent, ran))

    # --- jump -------------------------------------------------------------------------------
    time.sleep(2.5)
    ground = status()["y"]
    m.require_ok(m.call_bridge("input.key", key="Space"))
    time.sleep(0.25)
    airborne = status()["y"]
    check("jumping (Space) lifts the player", airborne > ground + 0.5, "%.2f -> %.2f" % (ground, airborne))
    time.sleep(1.2)

    # --- look (mouse delta -> <Mouse>/delta) -------------------------------------------------
    yaw_before = status()["camYaw"]
    m.require_ok(m.call_bridge("input.mouse_move", dx=200, dy=0))
    time.sleep(0.4)
    yaw_after = status()["camYaw"]
    turned = norm(yaw_after - yaw_before)
    check("looking (mouse_move) turns the camera", abs(turned) > 5.0, "%.1f deg for dx=200" % turned)
    per_px = turned / 200.0

    # --- aim at the nearest tree (world_nearest + world_aim) ---------------------------------
    tree = m.require_ok(m.call_bridge("world.nearest", kind="tree", max=80))
    check("world_nearest finds a tree", bool(tree.get("found")),
          "%s at %.1f m" % (tree.get("name"), tree.get("distance", -1)))
    if tree.get("found"):
        target = (tree["x"], tree["z"])
        for _ in range(6):
            player = status()
            desired = math.degrees(math.atan2(target[0] - player["x"], target[1] - player["z"]))
            error = norm(desired - player["camYaw"])
            if abs(error) < 1.0:
                break
            m.require_ok(m.call_bridge("input.mouse_move", dx=int(round(error / per_px if per_px else 20)), dy=0))
            time.sleep(0.3)
        aimed = m.require_ok(m.call_bridge("world.aim"))
        check("world_aim reports the crosshair target", bool(aimed.get("hit")), str(aimed.get("name")))

    # --- console commands (dev commands + spawn + tod) ----------------------------------------
    # 'spawn' and 'tod' are cheats: the bridge enables devcommands first (the command is a toggle).
    axe = m.require_ok(m.call_bridge("console", command="spawn AxeBlackMetal", cheats=True))
    check("console spawn AxeBlackMetal", bool(axe.get("ok")) and bool(axe.get("prefabFound")), str(axe.get("output")))

    pick = m.require_ok(m.call_bridge("console", command="spawn PickaxeBlackMetal 5", cheats=True))
    check("console spawn PickaxeBlackMetal 5", bool(pick.get("ok")) and bool(pick.get("prefabFound")),
          str(pick.get("output")))

    m.require_ok(m.call_bridge("console", command="tod 0.2", cheats=True))
    time.sleep(0.5)
    early = status()["dayFraction"]
    m.require_ok(m.call_bridge("console", command="tod 0.5", cheats=True))
    time.sleep(0.5)
    midday = status()["dayFraction"]
    check("console tod changes the time of day", abs(midday - 0.5) < 0.02, "%.2f -> %.2f" % (early, midday))

    bad = m.require_ok(m.call_bridge("console", command="definitelynotacommand", cheats=True))
    check("console reports an unknown command", bad.get("ok") is False, str(bad.get("output")))

    print()
    print("=== %d PASS / %d FAIL ===" % (len(TOTAL) - len(FAILS), len(FAILS)))
    return 0 if not FAILS else 1


if __name__ == "__main__":
    sys.exit(main())
