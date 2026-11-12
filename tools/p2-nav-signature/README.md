# p2-nav-signature

Prints the **exact JSON** the MCP adapter (`tools/mcp-devbridge/mcp_devbridge.py`) sends to the in-game bridge for the
P2 navigation tools - so the C# side (`src/Companions/DevBridge/`) can be compared against it field by field.

```powershell
python tools/p2-nav-signature/signature.py
```

```
      nav_tiles  {}                                       -> {"args": {"radius": 40}, "cmd": "nav.tiles"}
      nav_tiles  {"radius": 5, "x": 10, "y": 20, "z": 30} -> {"args": {"radius": 5, "x": 10, "y": 20, "z": 30}, "cmd": "nav.tiles"}
      nav_probe  {}                                       -> {"args": {"range": 3}, "cmd": "nav.probe"}
      nav_probe  {"range": 1.5}                           -> {"args": {"range": 1.5}, "cmd": "nav.probe"}
      nav_probe  {"x": 1.5, "y": 2.5, "z": 3.5}           -> {"args": {"range": 3, "x": 1.5, "y": 2.5, "z": 3.5}, "cmd": "nav.probe"}
      nav_stats  {}                                       -> {"args": {}, "cmd": "nav.stats"}
```

## What it does and does not do

- Runs the **real adapter** as a child process and answers it with the mock bridge from
  `tools/mcp-devbridge/test_mcp_devbridge.py` (port 47198; the protocol test uses 47199, so both can run).
- **No game, no `dotnet build`, no deploy** - nothing in `src/` is read or written and it leaves the repo clean.
- It proves the **adapter payload**, not the C# answer. The C# contract is:
  - `nav.tiles` `{x,y,z,radius}` -> `{count, tiles:[{id, center:{x,y,z}, poke, build, hasData, instanceValid}]}`
  - `nav.probe` `{x,y,z,range}` -> `{found, distance, point:{x,y,z}}`
  - `nav.stats` `{}` -> `{pokes, rebuilds, maxCollectMs}`
- **`x`/`y`/`z` are optional** in `nav.tiles`/`nav.probe`: the adapter omits them when the caller does not pass them
  (the C# must then fall back to the player's position, the same pattern as `build.pieces`/`build.sites`:
  `Json.Float(args, "x", center.x)`). `radius`/`range` are **always** sent, with the adapter defaults 40 and 3.
- `nav_stats` sends no arguments at all (`"args": {}`).

Exit code 0 = every call reached the bridge; 1 = one of them did not.
