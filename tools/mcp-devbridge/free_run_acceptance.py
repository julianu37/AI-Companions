#!/usr/bin/env python3
"""Reads the acceptance of a free build run from the BRIDGE (the state) and, for the events only, from the log.

Two questions, and they are different ones:
  * the PLACEMENT - does the mod lose or duplicate a piece? `placed` comes from the site's own counters
    (`companions.build_status`, the same snapshot the build panel draws), never from a log line.
  * the COUNTERS - `desistindo de` is still a log line: the bridge does not count give-ups yet. The 12/11 run was
    made with a barrier that counted a piece the site's queue still owned, so its count was wrong; each line is
    checked against the scene (a give-up on a piece that is standing is a false count).

    python -I tools/mcp-devbridge/free_run_acceptance.py            # the live site, or the last one in the log
    python -I tools/mcp-devbridge/free_run_acceptance.py --log-only  # no game needed
"""
import os
import re
import sys

_HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, _HERE)
import mcp_devbridge as bridge  # noqa: E402

POSE = re.compile(r"em \((-?[\d,]+), (-?[\d,]+), (-?[\d,]+)\)")


def read_log():
    with open(bridge.LOG_PATH, encoding="utf-8", errors="replace") as handle:
        return handle.read().splitlines()


def site():
    """The live site from the bridge, or None when the game is closed or the order already finished."""
    try:
        reply = bridge.call_bridge("companions.build_status")
    except RuntimeError as exc:
        print("aviso: ponte indisponivel (%s)" % exc)
        return None
    if not reply.get("ok"):
        print("aviso: build_status falhou: %s" % reply.get("error"))
        return None
    data = reply.get("data") or {}
    return data if data.get("site") else None


def main():
    log_only = "--log-only" in sys.argv
    lines = read_log()
    live = None if log_only else site()

    skips = [line for line in lines if "desistindo de" in line]
    ends = [line for line in lines if "order ended" in line]
    totals = [line for line in lines if "peças:" in line]
    falls = sum(1 for line in lines if "fell " in line and "fall damage" in line)
    deaths = sum(1 for line in lines if "died during the build" in line)
    unsupported = sum(1 for line in lines if "UNSUPPORTED" in line)
    heavy = sum(1 for line in lines if "too_heavy" in line)

    print("=== colocacao (a ponte: o estado, nao o log) ===")
    if live:
        placed, done, plan = live["placed"], live["placed"] + live["skipped"], live["plan"]
        print("label          : %s" % live.get("label"))
        print("fase           : %s%s" % (live.get("phase"), " (pausada)" if live.get("paused") else ""))
        print("placed         : %d" % placed)
        print("skipped        : %d" % live["skipped"])
        print("plano          : %d  (faltam %d)" % (plan, live.get("remaining", plan - done)))
        print("workers        : %d  %s" % (live.get("workers", 0),
                                           ", ".join("%s%s" % (m.get("name"), " (lider)" if m.get("leader") else "")
                                                     for m in live.get("members") or [])))
        print("fechou         : %s" % ("SIM" if live.get("done") else "ainda nao"))
        status = 0 if placed <= plan else 20
    else:
        # no live site: the log is all there is. `placed (` is written once per placement.
        placed = sum(1 for line in lines if "placed (" in line)
        done = sum(1 for line in lines if "done: " in line)
        print("placed (       : %d" % placed)
        print("done:          : %d" % done)
        print("iguais         : %s" % ("SIM" if placed == done else
                                       "em voo: +%d (normal com peca sendo verificada)" % (placed - done)
                                       if 0 < placed - done <= 2 else "NAO <-- DEFEITO"))
        status = 0 if 0 <= placed - done <= 2 else 20

    print()
    print("=== termos duros (log: sao eventos, nao estado) ===")
    print("quedas         : %d" % falls)
    print("mortes         : %d" % deaths)
    print("UNSUPPORTED    : %d" % unsupported)
    print("too_heavy      : %d" % heavy)

    standing = missing = 0
    missing_lines = []
    if not log_only:
        scene = bridge.call_bridge("build.pieces", x=-158.9, z=-427.7, radius=34)
        pieces = scene.get("data", {}).get("pieces", []) if scene.get("ok") else []
        for line in skips:
            found = POSE.search(line)
            if not found:
                continue
            x, y, z = (float(v.replace(",", ".")) for v in found.groups())
            if [p for p in pieces
                    if abs(p["x"] - x) < 1.0 and abs(p["z"] - z) < 1.0 and abs(p["y"] - y) < 1.0]:
                standing += 1
            else:
                missing += 1
                missing_lines.append(line.strip())

    print()
    print("=== contador (o defeito de 12/11/2026) ===")
    print("desistindo de  : %d" % len(skips))
    if log_only:
        print("  (--log-only: nao conferido contra a cena)")
    else:
        print("  de pe            : %d" % standing)
        print("  sumidas de fato  : %d" % missing)
        for line in missing_lines:
            print("  " + line)

    print()
    print("=== fim ===")
    print("order ended    : %s" % (ends[-1].split("] ")[-1] if ends else "(ainda nao)"))
    print("pecas          : %s" % (totals[-1].split("] ")[-1] if totals else "(ainda nao)"))

    ok = (deaths == 0 and unsupported == 0 and heavy == 0 and missing == 0 and status == 0)
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
