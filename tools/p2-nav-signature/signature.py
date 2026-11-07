#!/usr/bin/env python3
"""Prints the EXACT JSON lines the MCP adapter sends for the P2 navigation tools.

`nav_tiles`, `nav_probe` and `nav_stats` are thin wrappers over the bridge commands `nav.tiles`, `nav.probe` and
`nav.stats`; the C# side must accept exactly what this prints. It runs the real adapter (`tools/mcp-devbridge/
mcp_devbridge.py`) as a child process against the mock bridge of `test_mcp_devbridge.py` - no game, no build, nothing
in `src/` is touched, and the repo is left clean.

    python tools/p2-nav-signature/signature.py

Exit code 0 when every call reached the bridge; 1 otherwise.
"""

import json
import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
BRIDGE_DIR = os.path.join(os.path.dirname(HERE), "mcp-devbridge")
sys.path.insert(0, BRIDGE_DIR)

import test_mcp_devbridge as harness  # noqa: E402  (the __main__ guard keeps main() from running)

harness.PORT = 47198  # the protocol test uses 47199, so both can run without colliding
PORT = harness.PORT

CASES = [
    ("nav_tiles", {}),
    ("nav_tiles", {"x": 10, "y": 20, "z": 30, "radius": 5}),
    ("nav_probe", {}),
    ("nav_probe", {"range": 1.5}),
    ("nav_probe", {"x": 1.5, "y": 2.5, "z": 3.5}),
    ("nav_stats", {}),
]


def main():
    bridge = harness.MockBridge()
    bridge.start()
    env = dict(os.environ)
    env["DEVRIDGE_PORT"] = str(PORT)
    proc = subprocess.Popen([sys.executable, os.path.join(BRIDGE_DIR, "mcp_devbridge.py")],
                            stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                            text=True, encoding="utf-8", env=env)

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

    failed = 0
    try:
        rpc("initialize", {"protocolVersion": "2024-11-05", "capabilities": {},
                           "clientInfo": {"name": "signature", "version": "0"}})
        for index, (tool, arguments) in enumerate(CASES, start=10):
            before = len(bridge.requests)
            reply = rpc("tools/call", {"name": tool, "arguments": arguments}, request_id=index)
            sent = bridge.requests[before:]
            tool_args = json.dumps(arguments, sort_keys=True)
            if not sent:
                failed += 1
                print("FAIL  %-10s %s -> no request reached the bridge (%s)"
                      % (tool, tool_args, reply.get("result", {}).get("content")))
                continue
            for request in sent:
                print("      %-10s %-40s -> %s" % (tool, tool_args, json.dumps(request, sort_keys=True)))
    finally:
        try:
            proc.stdin.close()
        except OSError:
            pass
        proc.wait(timeout=5)
        bridge.shutdown()

    print()
    print("=== %d case(s), %d FAIL ===" % (len(CASES), failed))
    return 0 if not failed else 1


if __name__ == "__main__":
    sys.exit(main())
