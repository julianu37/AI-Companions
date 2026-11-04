#!/usr/bin/env python3
"""Protocol test for the MCP server, with a mock DevBridge (no game needed).

It starts a fake bridge on a loopback port, runs server/mcp_devbridge.py as a child process and exercises the MCP
handshake, the tool list and a few tool calls end to end. Exit code 0 = all checks passed.

    python server/test_mcp_devbridge.py
"""

import json
import os
import socket
import subprocess
import sys
import tempfile
import threading
import time

HERE = os.path.dirname(os.path.abspath(__file__))
SERVER = os.path.join(HERE, "mcp_devbridge.py")
PORT = 47199

PASSED = []
FAILED = []


def check(name, condition, detail=""):
    if condition:
        PASSED.append(name)
        print("PASS  %-46s %s" % (name, detail))
    else:
        FAILED.append(name)
        print("FAIL  %-46s %s" % (name, detail))


class MockBridge(threading.Thread):
    """Answers the bridge protocol the way the C# plugin does."""

    daemon = True

    def __init__(self):
        super().__init__()
        self.sock = socket.socket()
        self.sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        self.sock.bind(("127.0.0.1", PORT))
        self.sock.listen(8)
        self.stop = False
        self.requests = []

    def run(self):
        while not self.stop:
            try:
                conn, _ = self.sock.accept()
            except OSError:
                return
            with conn:
                buf = b""
                while not buf.endswith(b"\n"):
                    chunk = conn.recv(65536)
                    if not chunk:
                        break
                    buf += chunk
                if not buf:
                    continue
                request = json.loads(buf.decode("utf-8"))
                self.requests.append(request)
                conn.sendall((json.dumps(self.answer(request)) + "\n").encode("utf-8"))

    def answer(self, request):
        cmd = request.get("cmd")
        if cmd == "ping":
            return {"ok": True, "data": {"bridge": "DevBridge", "version": "1.0.0", "inGame": True, "mod": "1.9.4"}}
        if cmd == "status":
            return {"ok": True, "data": {"inGame": True, "player": "Tester", "x": 1.0, "y": 2.0, "z": 3.0}}
        if cmd == "companions.list":
            return {"ok": True, "data": {"count": 2, "companions": [{"name": "Maria"}, {"name": "Thorin"}]}}
        if cmd == "companions.order":
            args = request.get("args") or {}
            if str(args.get("action", "")).lower() in ("stop", "cancel", "para"):
                # Mirrors the C# answer (DevBridge/Commands.cs, CompanionsStop): the stop order cancels the
                # build and the leveling, and an empty name stops every companion and answers the list.
                if args.get("name"):
                    return {"ok": True, "data": {"action": "stop", "cancelled": True, "name": args["name"],
                                                 "build": False, "level": False}}
                return {"ok": True, "data": {"action": "stop", "cancelled": True, "build": True, "level": False,
                                             "companions": [{"name": "Maria", "build": True, "level": False},
                                                            {"name": "Thorin", "build": False, "level": False}]}}
            return {"ok": True, "data": {"action": request["args"]["action"], "applied": True}}
        if cmd == "nav.tiles":
            # Mirrors the C# answer for P2 (the echo/args are this mock's, so the forwarded payload is visible).
            return {"ok": True, "data": {
                "echo": cmd,
                "args": request.get("args"),
                "count": 2,
                "tiles": [
                    {"id": "1,2,3", "center": {"x": 1.0, "y": 2.0, "z": 3.0}, "poke": 0.5, "build": 0.25,
                     "hasData": True, "instanceValid": True},
                    {"id": "5,6,7", "center": {"x": 5.0, "y": 6.0, "z": 7.0}, "poke": 0.0, "build": 1.0,
                     "hasData": False, "instanceValid": False},
                ],
            }}
        if cmd == "nav.probe":
            return {"ok": True, "data": {
                "echo": cmd,
                "args": request.get("args"),
                "found": True,
                "distance": 1.25,
                "point": {"x": 1.5, "y": 2.5, "z": 3.5},
            }}
        if cmd == "nav.stats":
            return {"ok": True, "data": {
                "echo": cmd,
                "args": request.get("args"),
                "pokes": 42,
                "rebuilds": 3,
                "maxCollectMs": 1.75,
            }}
        if cmd == "companions.build":
            # Mirrors the C# answer (DevBridge/Commands.cs, CompanionsBuild): `echo`/`args` are this mock's
            # bookkeeping so the forwarded payload stays visible, the rest is the shape the tool documents.
            args = request.get("args") or {}
            data = {"echo": cmd, "args": args}
            if "blueprint" in args:
                if args["blueprint"] == "empty_site":
                    # mock-only sentinel for "nothing started": the C# answers Ok with workers 0 + the reason
                    data.update({"blueprint": "empty_site", "file": "empty_site.blueprint", "pieces": 1,
                                 "started": False, "workers": 0, "missing": None, "missingStation": None,
                                 "name": None, "refused": "no companion in range"})
                else:
                    # one site for the GROUP: name empty = every companion of the player joins (two here)
                    data.update({"blueprint": args["blueprint"], "file": args["blueprint"] + ".blueprint",
                                 "pieces": 28, "started": True, "workers": 1 if args.get("name") else 2,
                                 "missing": None, "missingStation": None, "name": "maria"})
            else:
                data.update({"started": True, "workers": 1, "prefab": args.get("plan") or args.get("prefab"),
                             "plan": args.get("plan"), "pieces": 1, "name": "maria",
                             "hint": {"x": 0.0, "y": 0.0, "z": 0.0}})
            return {"ok": True, "data": data}
        return {"ok": True, "data": {"echo": cmd, "args": request.get("args")}}

    def shutdown(self):
        self.stop = True
        try:
            self.sock.close()
        except OSError:
            pass


def main():
    bridge = MockBridge()
    bridge.start()

    log_path = os.path.join(tempfile.gettempdir(), "devbridge-test-log.txt")
    with open(log_path, "w", encoding="utf-8") as handle:
        handle.write("[Info   :AI Companions] line one\n[Info   :AI Companions] line two\n")

    env = dict(os.environ)
    env["DEVRIDGE_PORT"] = str(PORT)
    env["DEVRIDGE_LOG"] = log_path

    proc = subprocess.Popen(
        [sys.executable, SERVER],
        stdin=subprocess.PIPE,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
        encoding="utf-8",
        env=env,
    )

    def rpc(method, params=None, request_id=1):
        message = {"jsonrpc": "2.0", "id": request_id, "method": method}
        if params is not None:
            message["params"] = params
        proc.stdin.write(json.dumps(message) + "\n")
        proc.stdin.flush()
        line = proc.stdout.readline()
        if not line:
            raise RuntimeError("the server closed stdout (stderr: %s)" % proc.stderr.read())
        return json.loads(line)

    try:
        init = rpc("initialize", {"protocolVersion": "2024-11-05", "capabilities": {}, "clientInfo": {"name": "test", "version": "0"}})
        check("initialize returns the server info", init.get("result", {}).get("serverInfo", {}).get("name") == "valheim-devbridge",
              init.get("result", {}).get("serverInfo", {}).get("name"))
        check("initialize advertises tools", "tools" in init.get("result", {}).get("capabilities", {}))

        tools = rpc("tools/list", request_id=2).get("result", {}).get("tools", [])
        names = {tool["name"] for tool in tools}
        check("tools/list has 60 tools", len(tools) == 60, "%d tools" % len(tools))
        for expected in ("devbridge_ping", "game_status", "companions_list", "companions_order", "screenshot", "log_tail",
                         "player_kill", "player_inventory", "world_time", "companions_kill", "companions_tier",
                         "companions_hire", "companions_resurrect", "companions_farm_zone", "log_watch",
                         "build_inspect", "build_sites", "build_find", "build_pieces", "companions_build", "companions_sethome", "player_look", "build_check", "build_snaps", "build_usable", "terrain_check", "build_hoe", "companions_level",
                         "nav_tiles", "nav_probe", "nav_stats"):
            check("tools/list includes %s" % expected, expected in names)

        ping = rpc("tools/call", {"name": "devbridge_ping", "arguments": {}}, request_id=3)
        text = ping["result"]["content"][0]["text"]
        check("devbridge_ping reaches the bridge", "DevBridge" in text and '"inGame"' in text, text.replace("\n", " ")[:60])

        listing = rpc("tools/call", {"name": "companions_list", "arguments": {}}, request_id=4)
        text = listing["result"]["content"][0]["text"]
        check("companions_list returns the companions", "Maria" in text and "Thorin" in text)

        order = rpc("tools/call", {"name": "companions_order", "arguments": {"action": "wood", "name": "Maria"}}, request_id=5)
        text = order["result"]["content"][0]["text"]
        check("companions_order forwards the action", '"wood"' in text and '"applied": true' in text)
        check("companions_order sends the name", any(r.get("args", {}).get("name") == "Maria" for r in bridge.requests))

        # The stop order (player's request, 11/11/2026): "companions.order action=stop" used to answer
        # "unknown action: stop" - there was no way to cancel an order from the bridge.
        order_tool = next((t for t in tools if t["name"] == "companions_order"), {})
        check("companions_order documents the stop order",
              "stop" in (order_tool.get("description") or "").lower(), order_tool.get("description"))
        stop = rpc("tools/call", {"name": "companions_order", "arguments": {"action": "stop", "name": "Maria"}},
                   request_id=24)
        stop_data = json.loads(stop["result"]["content"][0]["text"])
        check("companions_order forwards stop",
              any(r.get("cmd") == "companions.order" and (r.get("args") or {}).get("action") == "stop"
                  for r in bridge.requests))
        check("companions_order stop answers action/cancelled",
              stop_data.get("action") == "stop" and stop_data.get("cancelled") is True, stop_data)
        check("companions_order stop answers build/level",
              "build" in stop_data and "level" in stop_data, sorted(stop_data))
        all_stop = json.loads(rpc("tools/call", {"name": "companions_order", "arguments": {"action": "para"}},
                                  request_id=25)["result"]["content"][0]["text"])
        check("companions_order stop without a name answers the list",
              isinstance(all_stop.get("companions"), list) and len(all_stop["companions"]) == 2, sorted(all_stop))
        check("companions_order stop lists build/level per companion",
              all("build" in one and "level" in one for one in all_stop["companions"]), all_stop["companions"])

        tail = rpc("tools/call", {"name": "log_tail", "arguments": {"lines": 5}}, request_id=6)
        text = tail["result"]["content"][0]["text"]
        check("log_tail reads the log file", "line two" in text)
        check("log_tail returns a byte cursor", '"cursor"' in text)

        watched = rpc("tools/call", {"name": "log_watch",
                                     "arguments": {"pattern": "line two", "since": 0, "timeout_ms": 2000}},
                      request_id=20)
        text = watched["result"]["content"][0]["text"]
        check("log_watch finds an existing line", '"matched": true' in text and "line two" in text)
        first = rpc("tools/call", {"name": "log_watch",
                                   "arguments": {"pattern": "line", "since": 0, "timeout_ms": 2000}},
                    request_id=22)
        first_text = first["result"]["content"][0]["text"]
        first_data = json.loads(first_text)
        check("log_watch returns the first matching line", first_data["lines"] == ["[Info   :AI Companions] line one"],
              first_data["lines"])
        second = rpc("tools/call", {"name": "log_watch",
                                    "arguments": {"pattern": "line", "since": first_data["cursor"], "timeout_ms": 2000}},
                     request_id=23)
        second_text = second["result"]["content"][0]["text"]
        check("log_watch cursor continues after the match", "line two" in second_text)
        missed = rpc("tools/call", {"name": "log_watch",
                                    "arguments": {"pattern": "never happens", "timeout_ms": 100}},
                     request_id=21)
        text = missed["result"]["content"][0]["text"]
        check("log_watch returns a clean timeout", '"matched": false' in text and '"timeout_ms": 100' in text)

        for request_id, tool, arguments, command in (
            (22, "player_kill", {}, "player.kill"),
            (23, "player_inventory", {"item": "Coins", "set": 1500}, "player.inventory"),
            (24, "world_time", {"day": 3, "fraction": 0.5}, "world.time"),
            (25, "companions_kill", {"name": "Maria"}, "companions.kill"),
            (26, "companions_tier", {"name": "Maria", "tier": "warrior"}, "companions.tier"),
            (27, "companions_hire", {}, "companions.hire"),
            (28, "companions_resurrect", {"name": "Maria"}, "companions.resurrect"),
            (29, "companions_farm_zone", {"action": "add", "crop": "CarrotSeeds"}, "companions.farm_zone"),
            (30, "build_inspect", {"prefab": "piece_workbench"}, "build.inspect"),
            (31, "build_sites", {"prefab": "piece_workbench", "radius": 3}, "build.sites"),
            (32, "build_pieces", {"radius": 5}, "build.pieces"),
            (33, "build_find", {"prefab": "piece_workbench", "radius": 5}, "build.find"),
            (34, "companions_build", {"prefab": "piece_workbench", "name": "Maria"}, "companions.build"),
            (40, "companions_build", {"plan": "hut"}, "companions.build"),
            (35, "companions_sethome", {"name": "Maria"}, "companions.sethome"),
            (36, "player_look", {"down": 40}, "player.look"),
            (37, "build_check", {"prefab": "woodwall", "x": 1, "y": 2, "z": 3, "yaw": 90}, "build.check"),
            (38, "build_snaps", {"prefab": "wood_roof_45"}, "build.snaps"),
            (39, "build_usable", {}, "build.usable"),
            (41, "terrain_check", {"size": 8}, "terrain.check"),
            (42, "build_hoe", {}, "build.hoe"),
            (43, "companions_level", {"size": 8}, "companions.level"),
            (44, "nav_tiles", {}, "nav.tiles"),
            (45, "nav_tiles", {"x": 10, "y": 20, "z": 30, "radius": 5}, "nav.tiles"),
            (46, "nav_probe", {}, "nav.probe"),
            (47, "nav_probe", {"x": 1.5, "y": 2.5, "z": 3.5, "range": 1.5}, "nav.probe"),
            (48, "nav_stats", {}, "nav.stats"),
        ):
            reply = rpc("tools/call", {"name": tool, "arguments": arguments}, request_id=request_id)
            text = reply.get("result", {}).get("content", [{}])[0].get("text", "")
            check("%s forwards %s" % (tool, command), '"echo": "%s"' % command in text, text.replace("\n", " ")[:50])

        check("build_inspect sends the prefab",
              any(r.get("cmd") == "build.inspect" and r.get("args", {}).get("prefab") == "piece_workbench" for r in bridge.requests))
        check("build_sites sends prefab and radius",
              any(r.get("cmd") == "build.sites" and r.get("args", {}).get("radius") == 3 for r in bridge.requests))

        check("companions_kill sends the name",
              any(r.get("cmd") == "companions.kill" and r.get("args", {}).get("name") == "Maria" for r in bridge.requests))
        check("world_time sends day and fraction",
              any(r.get("cmd") == "world.time" and r.get("args", {}).get("fraction") == 0.5 for r in bridge.requests))

        # --- companions.build: the blueprint GROUP contract (P1 multi-build) ----------------------------------
        build_tool = next((tool for tool in tools if tool["name"] == "companions_build"), {})
        build_desc = build_tool.get("description", "")
        name_prop = (build_tool.get("inputSchema", {}).get("properties") or {}).get("name") or {}
        check("companions_build documents workers/refused",
              "workers" in build_desc and "refused" in build_desc, build_desc[:60])
        check("companions_build says an empty name means everybody",
              "every companion" in (name_prop.get("description") or "").lower(), name_prop.get("description"))

        def build_args():
            """Every payload sent to companions.build, in call order (read fresh, so later calls count)."""
            return [r.get("args") or {} for r in bridge.requests if r.get("cmd") == "companions.build"]

        def build_data(arguments, request_id):
            reply = rpc("tools/call", {"name": "companions_build", "arguments": arguments}, request_id=request_id)
            return json.loads(reply["result"]["content"][0]["text"])

        group = build_data({"blueprint": "hs_meadows_hut"}, 60)
        check("companions_build forwards a nameless blueprint without a name (everybody joins)",
              any(a.get("blueprint") == "hs_meadows_hut" and "name" not in a for a in build_args()), build_args())
        check("companions_build returns started/workers for a group site",
              group.get("started") is True and group.get("workers") == 2, group.get("workers"))
        check("companions_build keeps refused out of a started order", "refused" not in group, sorted(group))
        check("companions_build answer has no leveling (it left the contract)", "leveling" not in group, sorted(group))

        named = build_data({"blueprint": "hs_meadows_hut", "name": "Maria"}, 61)
        check("companions_build sends the name of a named blueprint worker",
              any(a.get("blueprint") == "hs_meadows_hut" and a.get("name") == "Maria" for a in build_args()),
              build_args())
        check("companions_build returns a single worker for a named site", named.get("workers") == 1,
              named.get("workers"))

        empty = build_data({"blueprint": "empty_site"}, 62)
        check("companions_build answers a refused site with started false and workers 0",
              empty.get("started") is False and empty.get("workers") == 0, empty)
        check("companions_build returns refused when nothing started",
              empty.get("refused") == "no companion in range", empty.get("refused"))

        single = build_data({"prefab": "piece_workbench"}, 63)
        check("companions_build returns workers 1 for a single piece",
              single.get("started") is True and single.get("workers") == 1, single.get("workers"))
        check("companions_build returns no refused for a single piece", "refused" not in single, sorted(single))

        # --- P2 navigation instrumentation: payload forwarded and answer shape --------------------------------
        def nav_args(cmd):
            """Every payload sent to one bridge command, in call order (read fresh, so later calls count)."""
            return [r.get("args") or {} for r in bridge.requests if r.get("cmd") == cmd]

        tiles_reply = rpc("tools/call", {"name": "nav_tiles", "arguments": {"radius": 12}}, request_id=49)
        tiles_data = json.loads(tiles_reply["result"]["content"][0]["text"])
        probe_reply = rpc("tools/call", {"name": "nav_probe", "arguments": {"x": 7, "y": 8, "z": 9}}, request_id=50)
        probe_data = json.loads(probe_reply["result"]["content"][0]["text"])
        stats_reply = rpc("tools/call", {"name": "nav_stats", "arguments": {}}, request_id=51)
        stats_data = json.loads(stats_reply["result"]["content"][0]["text"])

        check("nav_tiles sends radius 40 by default",
              any(a.get("radius") == 40 for a in nav_args("nav.tiles")), nav_args("nav.tiles"))
        check("nav_tiles leaves the centre out by default (the C# uses the player)",
              any(a.get("radius") == 40 and "x" not in a and "y" not in a and "z" not in a
                  for a in nav_args("nav.tiles")), nav_args("nav.tiles"))
        check("nav_tiles sends x/y/z when given",
              any(a.get("x") == 10 and a.get("y") == 20 and a.get("z") == 30 and a.get("radius") == 5
                  for a in nav_args("nav.tiles")), nav_args("nav.tiles"))
        check("nav_tiles sends a radius of 12 when given",
              any(a.get("radius") == 12 for a in nav_args("nav.tiles")), [a.get("radius") for a in nav_args("nav.tiles")])
        check("nav_tiles returns count and the tiles",
              tiles_data.get("count") == 2 and len(tiles_data.get("tiles") or []) == 2, tiles_data.get("count"))
        first_tile = (tiles_data.get("tiles") or [{}])[0]
        check("nav_tiles tile has id/center/poke/build/hasData/instanceValid",
              first_tile.get("id") == "1,2,3" and first_tile.get("center", {}).get("y") == 2.0
              and "poke" in first_tile and "build" in first_tile
              and first_tile.get("hasData") is True and first_tile.get("instanceValid") is True, first_tile)

        check("nav_probe sends range 3 by default",
              any(a.get("range") == 3 and "x" not in a for a in nav_args("nav.probe")), nav_args("nav.probe"))
        check("nav_probe sends x/y/z and the range when given",
              any(a.get("x") == 1.5 and a.get("y") == 2.5 and a.get("z") == 3.5 and a.get("range") == 1.5
                  for a in nav_args("nav.probe")), nav_args("nav.probe"))
        check("nav_probe returns found/distance/point",
              probe_data.get("found") is True and probe_data.get("distance") == 1.25
              and probe_data.get("point", {}).get("z") == 3.5, probe_data)

        check("nav_stats returns pokes/rebuilds/maxCollectMs",
              stats_data.get("pokes") == 42 and stats_data.get("rebuilds") == 3
              and stats_data.get("maxCollectMs") == 1.75, stats_data)
        check("nav_stats sends no arguments",
              all(a == {} for a in nav_args("nav.stats")), nav_args("nav.stats"))

        unknown = rpc("tools/call", {"name": "does_not_exist", "arguments": {}}, request_id=7)
        check("unknown tool is a JSON-RPC error", unknown.get("error", {}).get("code") == -32602)

        pong = rpc("ping", request_id=8)
        check("ping answers {}", pong.get("result") == {})

        missing = rpc("tools/call", {"name": "game_console", "arguments": {}}, request_id=9)
        check("a missing argument is an isError result", missing.get("result", {}).get("isError") is True,
              missing.get("result", {}).get("content", [{}])[0].get("text", "")[:50])
    finally:
        try:
            proc.stdin.close()
        except Exception:  # noqa: BLE001
            pass
        proc.wait(timeout=5)
        bridge.shutdown()
        try:
            os.remove(log_path)
        except OSError:
            pass

    print()
    print("=== %d PASS / %d FAIL ===" % (len(PASSED), len(FAILED)))
    if FAILED:
        print("failed: " + ", ".join(FAILED))
    return 0 if not FAILED else 1


if __name__ == "__main__":
    sys.exit(main())







