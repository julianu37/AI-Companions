#!/usr/bin/env python3
"""Reads the acceptance of the free run (12/11/2026) from the log and the scene.

Two questions, and they are different ones:
  * the PLACEMENT - does the mod lose or duplicate a piece? `placed (` must equal `done:` at every point of the
    log and the pieces that were "given up" must be standing (the 12/11 finding: they always were).
  * the COUNTERS - the run was made with a barrier that counted a piece the site's queue still owned, so its
    `desistindo de` count is expected to be wrong and is reported here, not judged.

    python -I tools/mcp-devbridge/free_run_acceptance.py
"""
import re
import sys

sys.path.insert(0, __file__.rsplit("\\", 1)[0].rsplit("/", 1)[0])
import mcp_devbridge as bridge  # noqa: E402

LOG = bridge.LOG_PATH
POSE = re.compile(r"em \((-?[\d,]+), (-?[\d,]+), (-?[\d,]+)\)")


def main():
    with open(LOG, encoding="utf-8", errors="replace") as handle:
        lines = handle.read().splitlines()
    placed = sum(1 for line in lines if "placed (" in line)
    done = sum(1 for line in lines if "done: " in line)
    skips = [line for line in lines if "desistindo de" in line]
    ends = [line for line in lines if "order ended" in line]
    totals = [line for line in lines if "peças:" in line]
    falls = sum(1 for line in lines if "fell " in line and "fall damage" in line)
    deaths = sum(1 for line in lines if "died during the build" in line)
    unsupported = sum(1 for line in lines if "UNSUPPORTED" in line)
    heavy = sum(1 for line in lines if "too_heavy" in line)

    scene = bridge.call_bridge("build.pieces", x=-158.9, z=-427.7, radius=34)
    pieces = scene.get("data", {}).get("pieces", []) if scene.get("ok") else []

    standing = missing = 0
    missing_lines = []
    for line in skips:
        found = POSE.search(line)
        if not found:
            continue
        x, y, z = (float(v.replace(",", ".")) for v in found.groups())
        near = [p for p in pieces
                if abs(p["x"] - x) < 1.0 and abs(p["z"] - z) < 1.0 and abs(p["y"] - y) < 1.0]
        if near:
            standing += 1
        else:
            missing += 1
            missing_lines.append(line.strip())

    print("=== colocacao ===")
    print("placed (       : %d" % placed)
    print("done:          : %d" % done)
    # `placed (Direct) ... verifying next frame` is written BEFORE the piece is verified, so mid-run placed is
    # done + 1 while one piece is in flight. Two pieces in flight at once (the crew) is done + 2; that is healthy.
    print("iguais         : %s" % ("SIM" if placed == done else
                                   "em voo: +%d (normal com peça sendo verificada)" % (placed - done)
                                   if 0 < placed - done <= 2 else "NAO <-- DEFEITO"))
    print("build.pieces   : %d" % len(pieces))
    print()
    print("=== termos duros ===")
    print("quedas         : %d" % falls)
    print("mortes         : %d" % deaths)
    print("UNSUPPORTED    : %d" % unsupported)
    print("too_heavy      : %d" % heavy)
    print()
    print("=== contador (o defeito medido) ===")
    print("desistindo de  : %d" % len(skips))
    print("  de pe            : %d" % standing)
    print("  sumidas de fato  : %d" % missing)
    for line in missing_lines:
        print("  " + line)
    print()
    print("=== fim ===")
    print("order ended    : %s" % (ends[-1].split("] ")[-1] if ends else "(ainda nao)"))
    print("peças          : %s" % (totals[-1].split("] ")[-1] if totals else "(ainda nao)"))
    return 0 if 0 <= placed - done <= 2 and missing == 0 and deaths == 0 and unsupported == 0 and heavy == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
