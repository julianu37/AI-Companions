"""Audit of the .blueprint corpus with the MOD's own reader (read-only).

Calls the DevBridge command `blueprints.inspect` (the same `BuildBlueprintReport.Read` the build
panel and `MHR_Build.StartBlueprint` use) for every .blueprint it finds, and prints the aggregates plus
a CSV. It STARTS NOTHING: no order, no world change - `blueprints.inspect` only reads the file and
resolves the prefabs against the loaded world.

Why the absolute path: `blueprints.inspect` takes a full path as it is (`Commands.BlueprintPath`), so the
files the mod would NOT list (its library folder is non-recursive, `BuildBlueprints.Files`) can be
measured too - that difference is itself a finding.

Usage (from the repo root, with Valheim running and the DevBridge on):
    python tools/mcp-devbridge/audit_blueprint_corpus.py [--out <csv>] [--json]

Exit code 0 when every file answered, 1 when at least one failed (the failures are in the report).
"""

import argparse
import hashlib
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import mcp_devbridge  # noqa: E402  (the adapter next to this file)

GAME = r"C:\Program Files (x86)\Steam\steamapps\common\Valheim"
PLUGIN_LIBRARY = os.path.join(GAME, r"BepInEx\plugins\AI-Companions\blueprints")
LEGACY_LIBRARY = os.path.join(GAME, r"BepInEx\config\AI-Companions\blueprints")


def bridge(cmd, **args):
    return mcp_devbridge.call_bridge(cmd, **args)


def list_files(root):
    """Every .blueprint under `root`, with the relative sub-folder ('' = the top level)."""
    found = []
    if not os.path.isdir(root):
        return found
    for base, _dirs, names in os.walk(root):
        for name in sorted(names):
            if name.lower().endswith(".blueprint"):
                rel = os.path.relpath(base, root)
                found.append({
                    "path": os.path.join(base, name),
                    "name": name,
                    "sub": "" if rel == "." else rel,
                    "root": root,
                })
    return found


def sha256(path):
    digest = hashlib.sha256()
    with open(path, "rb") as handle:
        for chunk in iter(lambda: handle.read(1 << 20), b""):
            digest.update(chunk)
    return digest.hexdigest()


def inspect(path):
    reply = bridge("blueprints.inspect", file=path)
    if not reply.get("ok"):
        return None, reply.get("error") or "unknown error"
    return reply.get("data"), None


def median(values):
    if not values:
        return None
    ordered = sorted(values)
    mid = len(ordered) // 2
    if len(ordered) % 2:
        return ordered[mid]
    return (ordered[mid - 1] + ordered[mid]) / 2.0


def band(pieces):
    if pieces < 50:
        return "<50"
    if pieces <= 200:
        return "50-200"
    if pieces <= 800:
        return "200-800"
    return ">800"


def print_compact(summary, records):
    """The tables the audit asks for, in text."""
    print("FILES")
    print("  on disk (both folders, recursive) : %d" % summary["files"]["on_disk_total"])
    print("  unique content                   : %d (%d duplicates)" % (summary["files"]["unique"],
                                                                       summary["files"]["duplicates"]))
    print("  visible to the mod (top level)   : %d" % summary["files"]["visible_to_mod"])
    print("  parsed / failed                  : %d / %d" % (summary["files"]["parsed"],
                                                             summary["files"]["failed"]))
    for failure in summary["files"]["failures"]:
        print("    FAILED %s: %s" % (failure["file"], failure["error"]))
    print("  outside the mod library:")
    for entry in summary["files"]["outside_mod_library"]:
        print("    %-48s [%s]" % (entry["file"], entry["sub"]))

    print("\nPIECES  min %s  median %s  max %s  sum %s" % (
        summary["pieces"]["min"], summary["pieces"]["median"], summary["pieces"]["max"],
        summary["pieces"]["sum"]))
    print("STOREYS %s" % summary["storeys"])
    print("BROKEN LINES %s" % (summary["lines"]["broken_files"] or "none"))
    print("LINES LOST  %s" % (summary["lines"]["parsed_lines_lost"] or "none"))

    print("\nSKIPPED  files %d  total %d" % (summary["skipped"]["files_with_skips"],
                                             summary["skipped"]["total"]))
    for reason, count in sorted(summary["skipped"]["by_reason"].items(), key=lambda kv: -kv[1]):
        print("  reason: %-28s %d" % (reason, count))
    print("  top prefabs skipped:")
    for prefab, entry in sorted(summary["skipped"]["by_prefab"].items(), key=lambda kv: -kv[1]["count"])[:20]:
        print("    %-42s %6d  %s" % (prefab, entry["count"],
                                     ", ".join("%s x%d" % (k, v) for k, v in entry["reasons"].items())))
    print("  ONLY skipped (never in a plan):")
    for prefab, entry in list(summary["skipped"]["only_skipped"].items())[:20]:
        print("    %-42s %6d" % (prefab, entry["count"]))

    print("\nSTATIONS (token -> files / lines)")
    for token, entry in sorted(summary["stations"].items(), key=lambda kv: -kv[1]["lines"]):
        print("  %-28s %2d files  %6d lines" % (token, entry["files"], entry["lines"]))

    print("\nPREFABS (top 20 of the plans)")
    for prefab, entry in sorted(summary["prefabs"].items(), key=lambda kv: -kv[1]["pieces"])[:20]:
        print("  %-42s %6d pieces  %d files" % (prefab, entry["pieces"], entry["files"]))

    print("\nBANDS")
    for band_name in ("<50", "50-200", "200-800", ">800"):
        entry = summary["bands"].get(band_name)
        if not entry:
            continue
        print("  %-8s files %2d  pieces %s  storeys median %s max %s" % (
            band_name, entry["files"], entry["pieces"], entry["storeys_median"], entry["storeys_max"]))

    print("\nPER FILE")
    print("  %-46s %5s %5s %5s %6s %6s %6s" % ("file", "piece", "skip", "stor", "lines", "statns", "prefab"))
    for r in records:
        if not r["ok"]:
            print("  %-46s FAILED %s" % (r["file"], r["error"]))
            continue
        mark = " " if r["in_mod_library"] else "*"
        print("%s %-46s %5d %5d %5d %6d %6d %6d" % (
            mark, r["file"], r["pieces"], r["skipped"], r["storeys"], r["lines"],
            r["piecesNeedingStation"], len(r["prefabs"])))
    print("  (* = not visible to the mod)")

    print("\nRISKS")
    for risk in summary["risks"]:
        detail = {k: v for k, v in risk.items() if k not in ("file", "risk")}
        print("  %-46s %-34s %s" % (risk["file"], risk["risk"], detail))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--out", default=os.path.join(os.environ.get("TEMP", "."), "opencode",
                                                     "blueprint-audit.csv"))
    parser.add_argument("--json", action="store_true", help="print the whole report as JSON")
    parser.add_argument("--text", action="store_true", help="print the compact tables instead")
    args = parser.parse_args()

    listing = bridge("blueprints.list")
    listed = listing.get("data") if listing.get("ok") else {}

    files = []
    seen = {}
    for root in (PLUGIN_LIBRARY, LEGACY_LIBRARY):
        for entry in list_files(root):
            entry["hash"] = sha256(entry["path"])
            entry["in_mod_library"] = os.path.normcase(root) == os.path.normcase(PLUGIN_LIBRARY) and entry["sub"] == ""
            duplicate = seen.get(entry["hash"])
            entry["duplicate_of"] = duplicate["path"] if duplicate else None
            if not duplicate:
                seen[entry["hash"]] = entry
            files.append(entry)

    records = []
    for entry in files:
        data, error = inspect(entry["path"])
        record = {
            "file": entry["name"],
            "sub": entry["sub"],
            "path": entry["path"],
            "duplicate_of": entry["duplicate_of"],
            "in_mod_library": entry["in_mod_library"],
        }
        if data is None:
            record.update({"ok": False, "error": error})
        else:
            record.update({
                "ok": True,
                "name": data.get("name"),
                "lines": data.get("lines"),
                "pieceLines": data.get("pieceLines"),
                "brokenLines": data.get("brokenLines"),
                "pieces": data.get("pieces"),
                "skipped": data.get("skipped"),
                "storeys": data.get("storeys"),
                "piecesNeedingStation": data.get("piecesNeedingStation"),
                "stations": data.get("stations") or {},
                "prefabs": data.get("prefabs") or {},
                "skippedReasons": data.get("skippedReasons") or [],
                "bounds": data.get("bounds"),
                "materials": data.get("materials") or {},
                "materialsSkipped": data.get("materialsSkipped"),
            })
        records.append(record)

    unique = [r for r in records if not r["duplicate_of"]]
    good = [r for r in unique if r["ok"]]
    bad = [r for r in unique if not r["ok"]]

    # ---- aggregates -------------------------------------------------------------------------
    pieces = [r["pieces"] for r in good]
    summary = {
        "library": {
            "folder": listed.get("path"),
            "count_reported_by_mod": listed.get("count"),
            "listed": [e.get("file") for e in listed.get("blueprints", [])],
        },
        "files": {
            "on_disk_total": len(files),
            "unique": len(unique),
            "duplicates": len(files) - len(unique),
            "visible_to_mod": sum(1 for r in unique if r["in_mod_library"]),
            "outside_mod_library": [{"file": r["file"], "sub": r["sub"]} for r in unique if not r["in_mod_library"]],
            "parsed": len(good),
            "failed": len(bad),
            "failures": [{"file": r["file"], "sub": r["sub"], "error": r["error"]} for r in bad],
        },
        "pieces": {
            "min": min(pieces) if pieces else None,
            "median": median(pieces),
            "max": max(pieces) if pieces else None,
            "sum": sum(pieces),
        },
        "lines": {
            "broken_files": [{"file": r["file"], "brokenLines": r["brokenLines"]} for r in good if r["brokenLines"]],
            "parsed_lines_lost": [
                {"file": r["file"], "lines": r["lines"], "pieceLines": r["pieceLines"],
                 "pieces_plus_skipped": r["pieces"] + r["skipped"]}
                for r in good if r["pieceLines"] != r["pieces"] + r["skipped"]
            ],
        },
        "storeys": {},
        "skipped": {
            "files_with_skips": sum(1 for r in good if r["skipped"] > 0),
            "total": sum(r["skipped"] for r in good),
            "by_reason": {},
            "by_prefab": {},
        },
        "stations": {},
        "prefabs": {},
        "bands": {},
        "risks": [],
    }

    for r in good:
        summary["storeys"][str(r["storeys"])] = summary["storeys"].get(str(r["storeys"]), 0) + 1

    for r in good:
        for reason in r["skippedReasons"]:
            key = reason["reason"]
            summary["skipped"]["by_reason"][key] = summary["skipped"]["by_reason"].get(key, 0) + reason["count"]
            per = summary["skipped"]["by_prefab"].setdefault(reason["prefab"], {"count": 0, "reasons": {}})
            per["count"] += reason["count"]
            per["reasons"][key] = per["reasons"].get(key, 0) + reason["count"]

    for r in good:
        for token, count in r["stations"].items():
            entry = summary["stations"].setdefault(token, {"files": 0, "lines": 0})
            entry["files"] += 1
            entry["lines"] += count

    for r in good:
        for prefab, count in r["prefabs"].items():
            entry = summary["prefabs"].setdefault(prefab, {"pieces": 0, "files": 0})
            entry["pieces"] += count
            entry["files"] += 1

    skipped_only = {p: v for p, v in summary["skipped"]["by_prefab"].items() if p not in summary["prefabs"]}
    summary["skipped"]["only_skipped"] = dict(sorted(skipped_only.items(), key=lambda kv: -kv[1]["count"]))

    for r in good:
        b = band(r["pieces"])
        entry = summary["bands"].setdefault(b, {"files": 0, "storeys": [], "pieces": []})
        entry["files"] += 1
        entry["storeys"].append(r["storeys"])
        entry["pieces"].append(r["pieces"])
    for b, entry in summary["bands"].items():
        entry["storeys_median"] = median(entry["storeys"])
        entry["storeys_max"] = max(entry["storeys"]) if entry["storeys"] else 0

    known_station = "$piece_workbench"
    for r in good:
        total = r["pieces"] + r["skipped"]
        if total and r["skipped"] * 10 > total:
            summary["risks"].append({"file": r["file"], "risk": "skipped > 10% of the lines",
                                     "skipped": r["skipped"], "lines": total})
        if r["storeys"] > 8:
            summary["risks"].append({"file": r["file"], "risk": "storeys > 8", "storeys": r["storeys"]})
        if r["brokenLines"]:
            summary["risks"].append({"file": r["file"], "risk": "broken lines",
                                     "brokenLines": r["brokenLines"]})
        exotic = [t for t in r["stations"] if t != known_station]
        if exotic:
            summary["risks"].append({"file": r["file"], "risk": "station we cannot place",
                                     "stations": exotic})
        if r["materialsSkipped"]:
            summary["risks"].append({"file": r["file"], "risk": "prefab without a recipe here",
                                     "materialsSkipped": r["materialsSkipped"]})

    # ---- CSV --------------------------------------------------------------------------------
    os.makedirs(os.path.dirname(args.out), exist_ok=True)
    with open(args.out, "w", encoding="utf-8", newline="") as handle:
        handle.write("file;sub;in_mod_library;duplicate_of;name;pieces;skipped;storeys;lines;pieceLines;"
                     "brokenLines;stations;top_reason;prefabs;materials_skipped\n")
        for r in records:
            if not r["ok"]:
                handle.write("%s;%s;;%s;ERROR %s;;;;;;;;;;\n" % (r["file"], r["sub"],
                                                                r["duplicate_of"] or "", r["error"]))
                continue
            top = sorted(r["skippedReasons"], key=lambda x: -x["count"])
            top_reason = ("%s (%s) x%d" % (top[0]["prefab"], top[0]["reason"], top[0]["count"])) if top else ""
            handle.write("%s;%s;%s;%s;%s;%d;%d;%d;%d;%d;%d;%s;%s;%d;%d\n" % (
                r["file"], r["sub"], "yes" if r["in_mod_library"] else "no", r["duplicate_of"] or "",
                (r["name"] or "").replace(";", ","), r["pieces"], r["skipped"], r["storeys"], r["lines"],
                r["pieceLines"], r["brokenLines"], "|".join(sorted(r["stations"])), top_reason,
                len(r["prefabs"]), r["materialsSkipped"] or 0))

    if args.json:
        print(json.dumps({"summary": summary, "records": records}, indent=2, ensure_ascii=False))
    elif args.text:
        print_compact(summary, records)
    else:
        print(json.dumps(summary, indent=2, ensure_ascii=False))
    print("\nCSV: " + args.out)
    return 0 if not bad else 1


if __name__ == "__main__":
    sys.exit(main())
