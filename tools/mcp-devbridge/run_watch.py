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


def gap_alarm(row, previous_gap):
    """The line to print when the counter and the scene disagree, or None.

    Measured 16/11/2026: the sampler saw done=985 in_scene=984 for 20 samples in a row and the -1 was read as
    noise - the orphan piece was only named by the reconciliation, at the end, as an acceptance formality.

    Measured 17/11/2026 on a healthy run: a single deviation is ordinary, because the piece exists in the world
    a moment before its `done:` line is written (+3, +4, +7 once each, never the same value twice in a row). The
    hole of 16/11 kept the SAME gap for 20 samples. So one sighting is a race and three in a row is a hole.

    `previous_gap` is the streak the caller keeps: how many samples in a row told this story, with the sign of
    the gap. It is None before the first sighting and flips sign (starting over at 1) when the gap changes sign.
    """
    done = row.get("done")
    in_scene = row.get("in_scene")
    if not isinstance(done, int) or not isinstance(in_scene, int):
        return None
    # the sign is the backlog's own (16/11/2026: done=985, in_scene=984 -> gap=-1): how much the SCENE is
    # short of what the mod claims, negative meaning a piece the counter counts and the world does not have
    gap = in_scene - done
    if gap == 0:
        return None
    # previous_gap is the STREAK (how many samples in a row told this story), sign carrying the gap's sign
    streak = 1
    if previous_gap is not None and (previous_gap > 0) == (gap > 0):
        streak = abs(previous_gap) + 1
    if streak < 3:
        return None
    return "ALARME %s: o contador do mod diz %d peca(s) e a cena tem %d (gap %+d) ha %d amostra(s)" % (
        row.get("t", "?"), done, in_scene, gap, streak)



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
        last_gap = None
        while args.count == 0 or done < args.count:
            try:
                row = sample(args, baseline)
            except Exception as exc:  # noqa: BLE001 - a sampler must survive one bad call
                row = {"t": time.strftime("%H:%M:%S"), "error": "%s: %s" % (type(exc).__name__, exc)}
            text = json.dumps(row, ensure_ascii=False)
            print(text, flush=True)
            alarm = gap_alarm(row, last_gap)
            if alarm is not None:
                print(alarm, flush=True)
            # keep the streak for the next sample: how many in a row told this story, with the gap's sign
            done_n, scene_n = row.get("done"), row.get("in_scene")
            if isinstance(done_n, int) and isinstance(scene_n, int):
                now = scene_n - done_n
                same = last_gap is not None and now != 0 and (last_gap > 0) == (now > 0)
                streak = (abs(last_gap) + 1) if same else 1
                last_gap = (streak if now > 0 else -streak) if now != 0 else None
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
