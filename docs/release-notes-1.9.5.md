# 1.9.5 — in progress

![AI Companions — AI NPC companions for Valheim](https://raw.githubusercontent.com/julianu37/AI-Companions/main/docs/images/banner.png)

> **The batch from the 10-specialist review.** Twenty fixes across localization, patches, the companion, the LLM
> layer, the UI and the release pipeline. No new gameplay feature, no config key renamed (one key added).

## What changed

- **The Shrine and the roster speak all five languages.** The "bring back" confirmation and the "last known/died"
  lines were hardcoded English — two of the keys did not exist at all — and the roster's "unsummoned" note was too.
- **Companion tombstones fail open.** If the private `UpdateFloater`/`PositionCheck` reflection breaks, the patch now
  logs once and lets the game run its own `UpdateDespawn`, instead of suppressing it forever (the "tombstone that never
  despawns" report).
- **AOE-safe HUD and audit.** `EnemyHud` no longer dereferences a missing `RectTransform`; the raycast audit knows the
  appearance panel instead of reporting the mod's own window.
- **Bed claims are written with ownership** (re-reads the ZDO after the claim and only logs "saved" when it saved).
- **No reflection where the game has a public API**: `Bed.GetOwner` (it is `zdo.GetLong(s_owner)`), `EnvMan.GetDay`,
  `Humanoid.LeftItem`/`RightItem`. Latent bug fixed with it: the old `GetLeftItem` lookup did not exist, so the
  "already holding a shield" check was always false.
- **A chest opened on another player's game counts as in use** (cook and tombstone recovery); single-player is
  unchanged.
- **The pause menu's controller navigation works on the first open** (it only came back on the second).
- **The home area size survives death, revival and the roster** (`HC_Respawn_HomeRadius`; 0 = never changed).
- **The per-companion caches are pruned on destroy** (mover, recovery, status, rise watch, container fix).
- **`[Speech] MinInterval`/`MaxInterval` are real now**: the idle chatter uses them (15–30 s by default; the code had
  90–180 s hardcoded).
- **Dead respawn code removed** (`QueueRespawn`/`ProcessRespawns`/`DoRespawn`) — no caller, and it would have duplicated
  the roster entry if revived.
- **The structured-output warning is true**: after the server refuses both structured forms, the next messages go
  straight to the old format (one request instead of three). Changing `[AI] BaseUrl`/`Model` or toggling
  `[AI] StructuredOutput` tries again. Only a 400 whose body names `response_format`/`json_schema` (or a 400 with no
  body) is a format refusal — "model not found" now fails once, with its own detail.
- **`[Advanced] ConfigVersion`**: the one-time config migrations run once and stamp the file, instead of moving a value
  you set back to an old default on every boot.
- **The DevBridge cannot take the mod down**: it starts after the patches, and a port that cannot be opened just drops
  the service.
- **F8 stopped allocating per frame**, and the shared texture of the panel loader is no longer destroyed with a panel.

## How to update

1. Close the game and **unzip over the old folder** (your `BepInEx\config\AiCompanions.cfg` is kept; one key is added).
2. Nothing to download: the models are the same ones (Whisper `ggml-small-q5_1.bin`, Kokoro `tts\kokoro\`), and they go
   **inside** `BepInEx\plugins\AI-Companions\`.
3. Companions in the world are untouched — this is a fix batch, no data migration.
