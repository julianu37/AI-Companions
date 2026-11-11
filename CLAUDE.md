# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this repository is

A **BepInEx 5 mod for Valheim 1.0** (C#, net472/Mono, Harmony), named **AI Companions**, derived from
[Offline Companions Reborn](https://github.com/MaddhattrRocks/Offline-Companions-Reborn) (MIT — see `NOTICE.md`).
The upstream already ships the companion NPC (jobs, food, stamina, skills, home, rest, portals, radial menu). This
project adds two layers on top: an **AI layer** (LLM orders from chat or voice, local Whisper STT, local Kokoro TTS)
and **5 languages** (PT-BR, EN, ES, FR, IT).

**`AGENTS.md` (repo root, PT-BR) is the operational source of truth** — current version, measured test counts,
command list, pitfalls, open work. Read it before starting; it overrides guesswork. Detailed history lives in
`docs/` (internal docs are PT-BR, public docs/wiki are EN).

## Setup

`Environment.props` is **gitignored** — copy `Environment.props.example` to `Environment.props` and set `ValheimDir`
(and `DeployDir` to copy the DLL into `BepInEx\plugins\<mod folder>` after every build). There is **no NuGet package
for Valheim**; game assemblies are referenced in place from `$(ValheimDir)\valheim_Data\Managed` and
`$(ValheimDir)\BepInEx\core`. Jotunn is **not** used (no dependency, no reference).

## Commands

```powershell
# build — NOTE: this also DEPLOYS to the game folder (DeployDir); in a worktree/agent use SkipDeploy
dotnet build Companions.sln -c Release
dotnet build Companions.sln -c Release -p:SkipDeploy=true

# harness for the AI word nets + pure tables (needs the build above: cross-checks Companions.dll by reflection)
dotnet run --project tools/ai-intents-check -c Release
#   -- --list | --only=(p),(aa) | --quiet | --strict   (FAIL/SKIP are never filtered; a bad key exits 2)

# MCP DevBridge adapter, mock mode (no game needed)
python tools/mcp-devbridge/test_mcp_devbridge.py

# translation / config gates (all exit non-zero on failure)
python tools/audit_translations.py --check-config            # hc_cfg_* labels, 4 JSONs
python tools/audit_translations.py --lang Spanish            # also French, Italian
python tools/gen_config_doc.py --check src/Companions/Core/ModConfig.cs package/CONFIGURATION.md
python tools/gen_translations.py --check                     # known debt: fails with 109 keys (tool gap, not a translation gap)

# release gate: build, version x zip name, 3 translations, locale fallback, config doc, MCP, harness
powershell -NoProfile -ExecutionPolicy Bypass -File tools\release-check.ps1 -Version 1.9.6
#   -SkipGame (locale fallback reads the published DLL in the game folder) | -SkipIntents (slowest item)

# report for one build run, read from BepInEx\LogOutput.log
python tools/build_run_report.py [log] [--last|--all] [--json]

# GitHub Wiki source (docs/wiki/) -> separate .wiki repo
python tools/publish_wiki.py --check
```

**There is no CI** (no `.github/`) — every check is a local script above.

**The harness has no per-test filter**: it is one `net472` executable that runs everything and prints
`=== N PASS / M FAIL ===`. Exit `0` = all pass, `1` = any FAIL or SKIP, `2` = it could not start (game assemblies
missing) or an unknown `--only` key was given. `--only=(p),(aa)` filters the **report**, not the run — every section
still executes, and `Fail`/`Skip` always print. To iterate on one rule, add or edit its assertions in
`tools/ai-intents-check/Program.cs` (`--list` prints the sections in run order). Run harness and builds **serially** —
two at once fight over `obj/bin`.

## Architecture

`src/Companions/` — one assembly, namespace `Companions`:

| folder | contents |
|---|---|
| `Core/` | `CompanionsPlugin` (entrypoint, `PluginVersion`, GUID, config file), `ModConfig` (all settings), `ModLocalization`, `CompanionLocale`, `SpeechConfig` |
| `Companion/` | setup, spawn/prefabs, appearance, tiers, skills, stamina, food, rest, talk (TTS), death/respawn |
| `AI/` | movement and combat (`CompanionAI`, `CompanionCombatAI`, `MHR_Mover`, water/lava, doors) **+ the AI layer**: `LlmClient`, `LlmIntent`, `CompanionOrders`, `CompanionChatAI`, plus the **pure tables** (`RadialIntent`, `PickupIntent`, `GatherIntent`, `InteractionIntent`, `OrderItems`, `BuildFailure`, `BuildSupportBelow`, `BuildBody`, `BuildSpread`, `BuildWalkSpot`, `BuildAsideSpot`, `BuildMaterials`, …) |
| `Jobs/` | harvest, farm, fish, hunt, cook, smelt, repair, upkeep, loot, tool keeper, **`MHR_Build`** (single build order), **`MHR_BuildGroup`** (a crew on one plan), `MHR_Level` |
| `UI/` | panels, radial menu, HUD, config UI, zone placers, `ModUiLayers` (single source of the canvas sorting orders, band 1701+) |
| `Patches/` | Harmony patches on vanilla classes |
| `DevBridge/` | dev-only TCP loopback server on `127.0.0.1:47110`, off by default (`[Advanced] DevBridge`) |

**Order flow:** chat (`Chat.SendText`) or voice (push-to-talk → local Whisper) → `CompanionChatAI` (per-language
prompt contract + OpenAI-compatible JSON schema) → `LlmClient` (background `Task`, answer drained on the main thread
via a queue — never `.Wait()`) → `LlmIntent` (parser) → `CompanionOrders` → **the same calls the radial menu/command
key use**. `CompanionChatAI.ValidActions` is the single source of the action enum in the JSON schema; the word nets in
`AI/` are the safety net outside the model, and player free text is **never** translated.

**Build:** `MHR_Build` builds a single piece, a template (`plan=hut|canopy|tent|house|house4|house6`) or a
`.blueprint` file. Order is walk → floor → stairs → wall → roof → door (door last); support, stations and retries are
governed by the game's own `WearNTear` verdict, not by simulation. Do not redesign this from memory: read
`docs/re/building-notes.md` (measured runs) and `docs/backlog-construcao.md` (work queue with acceptance criteria).

**Localization:** English is native in code (`ModLocalization` defaults, `SpeechConfig` defaults); there is **no**
`English.json`. Only 4 JSONs each in `Translations/` (UI) and `Speech/` (spoken lines), loaded by **overwrite** —
a missing key falls back to English, not to the raw key. Config labels `hc_cfg_<section>_<key>` exist **only** in the
4 JSONs and are enforced by `audit_translations.py --check-config` — a new `ModConfig` option requires the key in all
four files in the same cycle. `Speech/` is kept out of `Translations/` on purpose (Jotunn's auto loader would treat a
subfolder as a language). UI strings and companion lines PT-BR-first; **code comments in English, log lines in PT-BR**.

## Verification loop that actually proves a change

- **Pure rule in `src/Companions/AI/`:** create the file, **link it in `tools/ai-intents-check/ai-intents-check.csproj`
  via `<Compile Include>`**, add assertions. The harness compiles the real sources (nothing copied, except a few
  stubs in `Stubs.cs` that section (h) cross-checks against the built DLL). Only harness green + build 0 errors/0
  warnings proves the change.
- `package/CONFIGURATION.md` is **generated** — never hand-edit; the `--check` above is the gate.
- The player tests in game; the agent writes the change, runs the harness, and reports what was and was not measured.

## Release order

`PluginVersion` in `src/Companions/Core/CompanionsPlugin.cs` + a section in `package/CHANGELOG.md` → gate
`release-check.ps1` **9/9** → staging with `python tools/stage_release.py --version <v> --from <plugin folder> --to
pkg\<v>\stage` (allow-list; **never** assemble the zip by hand) → tag `v<v>` → GitHub release is the agent's job;
Nexus is the player's, with the guide in `pkg\<v>\nexus\`. Staging lives in `pkg\` (gitignored) — never `%TEMP%`,
because the downloaded models live there.

## Pitfalls that cost a session

- **The build deploys.** Plain `dotnet build` copies the DLL into the game folder. Use `-p:SkipDeploy=true` when you
  must not touch the install.
- **`gh` resolves the `upstream` remote by default** (this fork shares history): always pass
  `gh release <cmd> -R julianu37/AI-Companions` (or `gh repo view julianu37/AI-Companions`, positional).
- **Paths resolved by `Assembly.Location`, not config:** `tts\`, `whisper\`, `blueprints\`, `Translations\` and
  `Speech\` must sit **inside** the plugin folder. This broke voice on fresh installs in 1.9.2/1.9.3.
- **Preserved on purpose** (worlds, saves and network depend on it): the GUID `Maddhattr.OfflineCompanionsReborn`,
  the assembly name `Companions.dll` / `Companions.csproj`, the `HC_*` keys. The config file, however, is
  `BepInEx\config\AiCompanions.cfg` (since 1.8.3).
- **Two installed copies = the same GUID loaded twice.** Vortex names the folder `plugins\Companions\` — point
  `DeployDir` there or remove the other copy.
- **Load check:** `BepInEx\LogOutput.log` must contain `Loading [AI Companions <version>]` and
  `loaded successfully! (N methods patched)`.
- **Config UI canvas layers live in `ModUiLayers`** — do not write sort orders as literals in panels (the 1701 band
  exists because the game's HUD is at 400 and its modal popup at 1900 sits above the mod on purpose).
- **Workflow:** code goes in a `feat/<theme>` / `fix/<theme>` branch (never `main`); one commit per hypothesis;
  the tree ends clean; docs land in the same commit cycle; no `--force`.
- **Documented dates run ahead of `git log`** — a known, registered divergence (`docs/backlog-construcao.md` item 9);
  don't "fix" run dates to match commits.

## Where to read the rest

`docs/roadmap.md` · `docs/plan-fases.md` (upstream map) · `docs/re/camada-ia-e-construcao.md` (AI + build history) ·
`docs/re/building-notes.md` (measured build runs) · `docs/backlog-construcao.md` (queue) ·
`docs/re/packaging-layout.md` · `docs/mcp-devbridge.md` (DevBridge protocol and test recipes) ·
`docs/re/stt-*.md`, `docs/re/tts-kokoro-sherpa-onnx.md` (local voice) · `docs/wiki/` (GitHub Wiki source) ·
`.opencode/skills/` (domain skills: `bepinex`, `kokoro`, `valheim-ai-backend`, `valheim-ai-orders`,
`valheim-devbridge`, `valheim-mod-research`, `valheim-reverse-engineering`, `valheim-assets`, `jotunn`).
