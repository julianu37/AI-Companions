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
            return {"ok": True, "data": {"action": request["args"]["action"], "applied": True}}
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
        check("tools/list has 30 tools", len(tools) == 30, "%d tools" % len(tools))
        for expected in ("devbridge_ping", "game_status", "companions_list", "companions_order", "screenshot", "log_tail",
                         "player_kill", "player_inventory", "world_time", "companions_kill", "companions_tier",
                         "companions_hire", "companions_resurrect", "companions_farm_zone", "log_watch"):
            check("tools/list includes %s" % expected, expected in names)

        ping = rpc("tools/call", {"name": "devbridge_ping", "arguments": {}}, request_id=3)
        text = ping["result"]["content"][0]["text"]
        check("devbridge_ping reaches the bridge", "DevBridge" in text and "1.9.4" in text, text.replace("\n", " ")[:60])

        listing = rpc("tools/call", {"name": "companions_list", "arguments": {}}, request_id=4)
        text = listing["result"]["content"][0]["text"]
        check("companions_list returns the companions", "Maria" in text and "Thorin" in text)

        order = rpc("tools/call", {"name": "companions_order", "arguments": {"action": "wood", "name": "Maria"}}, request_id=5)
        text = order["result"]["content"][0]["text"]
        check("companions_order forwards the action", '"wood"' in text and '"applied": true' in text)
        check("companions_order sends the name", any(r.get("args", {}).get("name") == "Maria" for r in bridge.requests))

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
        ):
            reply = rpc("tools/call", {"name": tool, "arguments": arguments}, request_id=request_id)
            text = reply.get("result", {}).get("content", [{}])[0].get("text", "")
            check("%s forwards %s" % (tool, command), '"echo": "%s"' % command in text, text.replace("\n", " ")[:50])

        check("companions_kill sends the name",
              any(r.get("cmd") == "companions.kill" and r.get("args", {}).get("name") == "Maria" for r in bridge.requests))
        check("world_time sends day and fraction",
              any(r.get("cmd") == "world.time" and r.get("args", {}).get("fraction") == 0.5 for r in bridge.requests))

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
