#!/usr/bin/env python3
"""Plays the game through the DevBridge: walk, run, jump, look, aim and chop a tree.

This is the regression test of player control (the MCP tools `input_key_down`/`input_key_up`,
`input_mouse_move`, `world_nearest`, `world_aim`). It needs the game in a world with the bridge on:

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


def wood():
    inv = m.require_ok(m.call_bridge("player.inventory"))
    return sum(s["amount"] for s in inv.get("stacks", []) if s["prefab"] == "Wood")


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

    # --- aim at the nearest tree -------------------------------------------------------------
    tree = m.require_ok(m.call_bridge("world.nearest", kind="tree", max=80))
    check("world_nearest finds a tree", bool(tree.get("found")),
          "%s at %.1f m" % (tree.get("name"), tree.get("distance", -1)))
    if not tree.get("found"):
        return 1

    player = status()
    desired = math.degrees(math.atan2(tree["x"] - player["x"], tree["z"] - player["z"]))
    for _ in range(6):
        error = norm(desired - status()["camYaw"])
        if abs(error) < 1.5:
            break
        m.require_ok(m.call_bridge("input.mouse_move", dx=int(round(error / per_px if per_px else 20)), dy=0))
        time.sleep(0.35)
    aimed = m.require_ok(m.call_bridge("world.aim"))
    check("world_aim sees the tree", bool(aimed.get("tree")), str(aimed.get("name")))

    # --- approach ----------------------------------------------------------------------------
    for _ in range(40):
        if m.require_ok(m.call_bridge("world.nearest", kind="tree", max=80))["distance"] <= 3.0:
            break
        hold(["W"], 0.5)
    near = m.require_ok(m.call_bridge("world.nearest", kind="tree", max=80))["distance"]
    check("the player reaches the tree", near <= 4.0, "%.2f m" % near)

    # --- chop --------------------------------------------------------------------------------
    # The swing costs ~14 stamina, so the loop waits for it (a starved player just swings nothing).
    if near < 2.2:
        hold(["S"], 0.35)
    wood_before = wood()
    chopped = False
    swings = 0
    deadline = time.time() + 120
    while time.time() < deadline:
        if status()["stamina"] < 20:
            time.sleep(1.5)
            continue
        m.require_ok(m.call_bridge("input.mouse_down", x=840, y=525))
        time.sleep(0.10)
        m.require_ok(m.call_bridge("input.mouse_up", x=840, y=525))
        swings += 1
        time.sleep(0.9)
        if wood() > wood_before:
            chopped = True
            break
    check("attacking (mouse) chops the tree", chopped, "%d swings, wood %d -> %d" % (swings, wood_before, wood()))

    print()
    print("=== %d PASS / %d FAIL ===" % (len(TOTAL) - len(FAILS), len(FAILS)))
    return 0 if not FAILS else 1


if __name__ == "__main__":
    sys.exit(main())
