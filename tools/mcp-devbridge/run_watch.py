#!/usr/bin/env python3
"""Live sampler for a build run: the DevBridge (real pieces in the scene) crossed with the log's counters.

Why both: the log says what the mod *thinks* it did (`done:`, `blocked_by_player`, `no progress`), while
`build.pieces` says what *exists* in the scene, with a position and a height per piece. A run where the two
disagree is the interesting one - a piece logged as done that never appeared, or a piece in the scene the mod
does not count.

Each sample is one JSON line on stdout (use `--out FILE` to keep them). The log counters are read from a
baseline line of `LogOutput.log` taken at start, so an earlier order in the same session does not leak in.

    python tools/mcp-devbridge/run_watch.py --x -158.9 --z -427.7 --radius 30 --every 20 --out run.jsonl
"""
import argparse
import json
import time
from collections import Counter

import mcp_devbridge

LOG = mcp_devbridge.LOG_PATH

# (label, substring) - the same counters the run reports are written from
COUNTERS = [
    ("done", "done: "),
    # NOT the bare word "skipped": the log prints "10 tree(s) skipped: too close to the buildings" and that
    # counter jumped 0 -> 4 in this run without a single piece being skipped (measured 11/11/2026). The piece
    # skips are counted by the giving-up line with the running total.
    ("skipped", "pulada(s)"),
    ("no_progress", "no progress towards"),
    ("blocked_by_player", "blocked_by_player"),
    ("no_free_spot", "sem lugar livre"),
    ("queued", "vai para a fila"),
    ("posts", "apoio de emergência"),
    ("falls", "fell "),
    ("deaths", "died during the build"),
    ("giving_up", "desistindo de"),
    ("chest_takes", "from the chest"),
    ("too_heavy", "too_heavy"),
    ("unsupported", "UNSUPPORTED"),
    ("ended", "order ended"),
]


def log_lines(baseline):
    try:
        with open(LOG, encoding="utf-8", errors="replace") as handle:
            return handle.read().splitlines()[baseline:]
    except OSError:
        return []


def line_count():
    try:
        with open(LOG, encoding="utf-8", errors="replace") as handle:
            return sum(1 for _ in handle)
    except OSError:
        return 0


def sample(args, baseline):
    started = time.time()
    pieces = mcp_devbridge.call_bridge("build.pieces", x=args.x, z=args.z, radius=args.radius)
    elapsed_ms = (time.time() - started) * 1000.0
    data = pieces.get("data", {}) if pieces.get("ok") else {}
    listed = data.get("pieces") or []
    heights = [p["y"] for p in listed]
    lines = log_lines(baseline)
    row = {
        "t": time.strftime("%H:%M:%S"),
        "mcp_ms": round(elapsed_ms, 1),
        "in_scene": data.get("count"),
        "top": dict(Counter(p["prefab"].replace("(Clone)", "") for p in listed).most_common(6)),
        "y_min": round(min(heights), 2) if heights else None,
        "y_max": round(max(heights), 2) if heights else None,
    }
    for label, needle in COUNTERS:
        row[label] = sum(1 for line in lines if needle in line)
    for line in reversed(lines):
        if "next: " in line:
            row["next"] = line.split("next: ", 1)[1].split(" - ")[0]
            break
    status = mcp_devbridge.call_bridge("status").get("data", {})
    roster = mcp_devbridge.call_bridge("companions.list").get("data", {})
    row["hp"] = [c.get("hp") for c in roster.get("companions", [])]
    row["dist"] = [round(c.get("distanceToPlayer", 0), 1) for c in roster.get("companions", [])]
    row["player"] = [round(status.get(k, 0), 1) for k in ("x", "y", "z")]
    return row


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--x", type=float, required=True)
    parser.add_argument("--z", type=float, required=True)
    parser.add_argument("--radius", type=float, default=30.0)
    parser.add_argument("--every", type=float, default=20.0)
    parser.add_argument("--count", type=int, default=0, help="0 = until interrupted")
    parser.add_argument("--out", default=None)
    parser.add_argument("--baseline", type=int, default=-1,
                        help="line of LogOutput.log where the order starts (default: the current end)")
    args = parser.parse_args()
    baseline = args.baseline if args.baseline >= 0 else line_count()
    sink = open(args.out, "a", encoding="utf-8") if args.out else None
    try:
        done = 0
        while args.count == 0 or done < args.count:
            try:
                row = sample(args, baseline)
            except Exception as exc:  # noqa: BLE001 - a sampler must survive one bad call
                row = {"t": time.strftime("%H:%M:%S"), "error": "%s: %s" % (type(exc).__name__, exc)}
            text = json.dumps(row, ensure_ascii=False)
            print(text, flush=True)
            if sink is not None:
                sink.write(text + "\n")
                sink.flush()
            done += 1
            if args.count == 0 or done < args.count:
                time.sleep(args.every)
    except KeyboardInterrupt:
        pass
    finally:
        if sink is not None:
            sink.close()


if __name__ == "__main__":
    main()
