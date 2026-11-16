#!/usr/bin/env python3
"""Reconcile a built scene against its blueprint: does every planned piece exist in the world?

The proof that a construction has no missing piece is the SCENE, piece by piece -- the run report is
narrative (it counts what the order said it did), and the run of 15/11/2026 closed "ready" with 10
pieces missing while the log was green.

The method is the one measured in docs/re/ciclo-de-run-devbridge.md step 10, and it is SEED-FREE on
purpose: the blueprint stores LOCAL coordinates and the scene stores WORLD ones, and the rigid
transform between them is unknown on every run (the yaw follows the player's facing, ~105 deg in one
run and ~46 deg in another). A fit seeded from a previous run's answer matches 78 of 985 pieces and an
ICP started from that seed diverges into a mirrored basin (473 pairs at -45 deg). So the yaw is
swept 0..360 in 1 deg steps, translation anchored on the centroid difference at each step, pieces
assigned greedily per prefab within 0.45 m, then polished (yaw to 0.02 deg with a median-residual
translation update each step).

Usage:
    python tools/reconcile_scene.py --blueprint <file.blueprint> --scene <scene.json> [--tol 0.45]
    python tools/reconcile_scene.py --blueprint X --pieces '{"pieces": [...]}'

`--scene` is the JSON saved from the DevBridge `build.pieces` reply (either the whole reply or its
`data`), which carries a `pieces` list of {prefab, x, y, z, rot...}.

Exit code 0 = every planned piece is in the scene (or is explained by an extra of the same prefab),
1 = something is genuinely missing, 2 = the inputs could not be read.
"""

import argparse
import json
import math
import sys


def load_blueprint(path):
    """Read a .blueprint file: `prefab;category;x;y;z;rx;ry;rz;rw;info;scale`, local coordinates.

    Blank lines and `#` comments are ignored; a line with fewer than 4 fields is not a piece.
    """
    pieces = []
    with open(path, encoding="utf-8", errors="replace") as handle:
        for line in handle:
            line = line.strip()
            if not line or line.startswith("#"):
                continue
            fields = line.split(";")
            if len(fields) < 4:
                continue
            try:
                pieces.append({
                    "prefab": normalise(fields[0]),
                    "x": float(fields[2]),
                    "y": float(fields[3]),
                    "z": float(fields[4]),
                })
            except (ValueError, IndexError):
                continue
    return pieces


def normalise(prefab):
    """The prefab key the blueprint and the scene share.

    A scene piece is the INSTANCE of a prefab, and Unity names instances `<prefab>(Clone)` while the
    blueprint stores the bare prefab name - comparing them raw yields zero pairs at every yaw.
    """
    name = (prefab or "").strip()
    for suffix in ("(Clone)", " (Clone)"):
        if name.endswith(suffix):
            name = name[: -len(suffix)]
    return name.strip()


def load_scene(path=None, raw=None):
    """Read the scene from a DevBridge `build.pieces` payload (reply or its `data`)."""
    if raw is not None:
        payload = json.loads(raw)
    else:
        with open(path, encoding="utf-8", errors="replace") as handle:
            payload = json.load(handle)
    if isinstance(payload, dict) and "data" in payload and isinstance(payload["data"], dict):
        payload = payload["data"]
    pieces = payload.get("pieces") if isinstance(payload, dict) else payload
    if not isinstance(pieces, list):
        raise ValueError("no 'pieces' list in the scene payload")
    out = []
    for piece in pieces:
        try:
            out.append({
                "prefab": normalise(piece["prefab"]),
                "x": float(piece.get("x", 0.0)),
                "y": float(piece.get("y", 0.0)),
                "z": float(piece.get("z", 0.0)),
            })
        except (KeyError, TypeError, ValueError):
            continue
    return out


def centroid(pieces):
    if not pieces:
        return (0.0, 0.0, 0.0)
    n = len(pieces)
    return (
        sum(p["x"] for p in pieces) / n,
        sum(p["y"] for p in pieces) / n,
        sum(p["z"] for p in pieces) / n,
    )


def transform(piece, yaw, tx, ty, tz):
    """Blueprint (local) -> world: rotate about Y by `yaw`, then translate."""
    cos_y = math.cos(yaw)
    sin_y = math.sin(yaw)
    return (
        cos_y * piece["x"] + sin_y * piece["z"] + tx,
        piece["y"] + ty,
        -sin_y * piece["x"] + cos_y * piece["z"] + tz,
    )


def anchor(plan, scene, yaw):
    """Translation that puts the plan's centroid over the scene's centroid at this yaw."""
    mx, my, mz = centroid(plan)
    sx, sy, sz = centroid(scene)
    cos_y = math.cos(yaw)
    sin_y = math.sin(yaw)
    return (
        sx - (cos_y * mx + sin_y * mz),
        sy - my,
        sz - (-sin_y * mx + cos_y * mz),
    )


def match(plan, scene, yaw, tx, ty, tz, tol):
    """Greedy assignment per prefab within `tol` metres. Returns (pairs, residuals)."""
    buckets = {}
    for index, piece in enumerate(scene):
        buckets.setdefault(piece["prefab"], []).append(index)
    used = set()
    pairs = []
    residuals = []
    limit = tol * tol
    for plan_index, piece in enumerate(plan):
        px, py, pz = transform(piece, yaw, tx, ty, tz)
        best = None
        best_d = limit
        for scene_index in buckets.get(piece["prefab"], ()):
            if scene_index in used:
                continue
            other = scene[scene_index]
            dx = other["x"] - px
            dy = other["y"] - py
            dz = other["z"] - pz
            d = dx * dx + dy * dy + dz * dz
            if d <= best_d:
                best_d = d
                best = scene_index
        if best is not None:
            used.add(best)
            pairs.append((plan_index, best))
            residuals.append((
                scene[best]["x"] - px,
                scene[best]["y"] - py,
                scene[best]["z"] - pz,
            ))
    return pairs, residuals


def median(values):
    ordered = sorted(values)
    n = len(ordered)
    if n == 0:
        return 0.0
    mid = n // 2
    return ordered[mid] if n % 2 else (ordered[mid - 1] + ordered[mid]) * 0.5


def rotate(piece, yaw):
    cos_y = math.cos(yaw)
    sin_y = math.sin(yaw)
    return (
        cos_y * piece["x"] + sin_y * piece["z"],
        -sin_y * piece["x"] + cos_y * piece["z"],
    )


def sweep(plan, scene, tol, quantum=0.5, sample=160, candidates=12):
    """Seed-free fit: sweep the yaw 0..360 in 1 deg and, at each yaw, VOTE on the translation.

    Anchoring the translation on the centroid difference (the recipe of the run cycle) only works when
    the scene holds the whole plan - a scene captured mid-build (219 of 985 pieces in the run of
    15/11/2026) has the centroid of the storey that exists, not of the house, and every candidate
    yaw then lands nowhere. So the translation is searched too, by a Hough vote: for every scene
    piece and every rotated plan piece the offset between them is quantised into a cell
    (`quantum` metres) and the cell with the most votes is the translation. Nothing is seeded from a
    previous run - the vote is over the data. The best few (yaw, offset) candidates are then scored
    with the real greedy match, and the winner goes to the polish.
    """
    scene_prefabs = {p["prefab"] for p in scene}
    usable = [p for p in plan if p["prefab"] in scene_prefabs]
    if len(usable) > sample:
        # evenly spaced, not the first N: the plan is ordered by storey and the first N are one corner
        step = len(usable) / float(sample)
        usable = [usable[int(i * step)] for i in range(sample)]
    if not usable or not scene:
        return (0, 0.0, 0.0, 0.0, 0.0, [], [])

    scored = []
    for degree in range(360):
        yaw = math.radians(float(degree))
        rotated = [(rotate(p, yaw), p["y"]) for p in usable]
        votes = {}
        for other in scene:
            ox = other["x"]
            oy = other["y"]
            oz = other["z"]
            for (px, pz), py in rotated:
                key = (int(round((ox - px) / quantum)), int(round((oy - py) / quantum)),
                       int(round((oz - pz) / quantum)))
                votes[key] = votes.get(key, 0) + 1
        # the few best cells for this yaw (a tie in votes is a tie in the fit)
        top = sorted(votes.items(), key=lambda item: -item[1])[:2]
        for (cell_x, cell_y, cell_z), count in top:
            scored.append((count, yaw, cell_x * quantum, cell_y * quantum, cell_z * quantum))
    scored.sort(key=lambda item: -item[0])

    best = (-1, 0.0, 0.0, 0.0, 0.0, [], [])
    for _count, yaw, tx, ty, tz in scored[:candidates]:
        pairs, residuals = match(plan, scene, yaw, tx, ty, tz, tol)
        if len(pairs) > best[0]:
            best = (len(pairs), yaw, tx, ty, tz, pairs, residuals)
    return best


def polish(plan, scene, yaw, tx, ty, tz, tol):
    """Refine the yaw in shrinking steps (1.2 -> 0.12 -> 0.02 deg), moving the translation by the
    median residual each step; stop when the step is below 1e-4 rad."""
    best = (len(match(plan, scene, yaw, tx, ty, tz, tol)[0]), yaw, tx, ty, tz)
    for step_deg in (1.2, 0.12, 0.02):
        step = math.radians(step_deg)
        while step >= 1e-4:
            improved = False
            for direction in (1.0, -1.0):
                candidate_yaw = best[1] + direction * step
                pairs, residuals = match(plan, scene, candidate_yaw, best[2], best[3], best[4], tol)
                if not pairs:
                    continue
                # walk the translation by the median residual so the yaw is judged with the fit centred
                tx = best[2] + median([r[0] for r in residuals])
                ty = best[3] + median([r[1] for r in residuals])
                tz = best[4] + median([r[2] for r in residuals])
                pairs, _ = match(plan, scene, candidate_yaw, tx, ty, tz, tol)
                if len(pairs) > best[0]:
                    best = (len(pairs), candidate_yaw, tx, ty, tz)
                    improved = True
            if not improved:
                step *= 0.5
    return best


def explain_missing(plan, scene, pairs, tol, yaw, tx, ty, tz):
    """For each unmatched planned piece, is there a scene piece of the SAME prefab nearby?

    That is how the documented run read its two "missing" pieces: the only two pieces of the plan at
    y = -2.00 (buried foundation) stood 0.97 m above the plan, on ground that is higher there. A
    missing piece with an extra of the same prefab a metre away is not a hole in the house.

    The comparison is in WORLD space: the plan stores LOCAL coordinates, so the piece has to be put
    through the very transform the match used before it is measured against a scene extra. Comparing
    the raw plan coordinate with a world one yields hundreds of metres and "explains" everything.
    """
    matched_plan = {p for p, _ in pairs}
    matched_scene = {s for _, s in pairs}
    extras = [scene[i] for i in range(len(scene)) if i not in matched_scene]
    out = []
    for index, piece in enumerate(plan):
        if index in matched_plan:
            continue
        wx, wy, wz = transform(piece, yaw, tx, ty, tz)
        near = None
        best = None
        for extra in extras:
            if extra["prefab"] != piece["prefab"]:
                continue
            d = math.dist((wx, wy, wz), (extra["x"], extra["y"], extra["z"]))
            if best is None or d < best:
                best = d
        if best is not None and best <= 3.0 * tol:
            near = best
        out.append({
            "index": index,
            "prefab": piece["prefab"],
            "x": piece["x"], "y": piece["y"], "z": piece["z"],
            "world": [wx, wy, wz],
            "nearest_same_prefab_m": best,
            "extra_same_prefab_m": near,
        })
    return out, len(extras)


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--blueprint", required=True, help="the .blueprint file (local coordinates)")
    parser.add_argument("--scene", help="JSON saved from the DevBridge build.pieces reply")
    parser.add_argument("--pieces", help="the same reply as a JSON string, instead of --scene")
    parser.add_argument("--tol", type=float, default=0.45, help="match tolerance in metres (default 0.45)")
    parser.add_argument("--json", action="store_true", help="print the result as JSON")
    args = parser.parse_args()

    try:
        plan = load_blueprint(args.blueprint)
        scene = load_scene(args.scene, args.pieces)
    except (OSError, ValueError) as error:
        print("reconcile_scene: %s" % error, file=sys.stderr)
        return 2
    if not plan or not scene:
        print("reconcile_scene: %d planned piece(s), %d in the scene" % (len(plan), len(scene)), file=sys.stderr)
        return 2

    count, yaw, tx, ty, tz, pairs, residuals = sweep(plan, scene, args.tol)
    count, yaw, tx, ty, tz = polish(plan, scene, yaw, tx, ty, tz, args.tol)
    pairs, residuals = match(plan, scene, yaw, tx, ty, tz, args.tol)
    missing, extras = explain_missing(plan, scene, pairs, args.tol, yaw, tx, ty, tz)

    unexplained = [m for m in missing if m["extra_same_prefab_m"] is None]
    explained = len(missing) - len(unexplained)
    result = {
        "planned": len(plan),
        "scene": len(scene),
        "matched": len(pairs),
        "yaw_deg": math.degrees(yaw) % 360.0,
        "translation": [tx, ty, tz],
        "tolerance_m": args.tol,
        "missing": len(missing),
        "missing_explained": explained,
        "missing_unexplained": len(unexplained),
        "extra_scene_pieces": extras,
        "mismatch_mm": max((math.dist((0, 0, 0), r) for r in residuals), default=0.0) * 1000.0,
        "unexplained": unexplained,
    }
    if args.json:
        print(json.dumps(result, indent=2))
    else:
        print("plano %d pecas, cena %d pecas, casadas %d (yaw %.2f deg, tol %.2f m)"
              % (result["planned"], result["scene"], result["matched"], result["yaw_deg"], args.tol))
        print("faltantes no plano: %d (explicadas por extra do mesmo prefab: %d, sem explicacao: %d)"
              % (result["missing"], explained, len(unexplained)))
        print("extras na cena: %d; residuo maximo %.0f mm" % (extras, result["mismatch_mm"]))
        for item in unexplained[:40]:
            print("  FALTA  %-28s em (%.2f, %.2f, %.2f)" % (item["prefab"], item["x"], item["y"], item["z"]))
        if len(unexplained) > 40:
            print("  ... e %d outra(s)" % (len(unexplained) - 40))

    # The acceptance is "no planned piece unaccounted for": a piece with an extra of the same prefab
    # next to it is a piece that stood somewhere else, not a hole.
    return 0 if not unexplained else 1


if __name__ == "__main__":
    sys.exit(main())
