# 1.9.3 — the beard fix

![AI Companions — AI NPC companions for Valheim](https://raw.githubusercontent.com/julianu37/AI-Companions/main/docs/images/banner.png)

> **One bug, found by the 1.9.2 log:** a companion **without a beard** was asking the game for a beard that does not
> exist. The game logged `Missing attach item: 371857150`, the `[AppearanceDiag]` warning fired on every appearance
> apply, and the beard slot of the companion carried a meaningless hash. Fixed: **"no beard" is hash `0`**.

## What changed

- **The beard hash.** `CompanionSetup.ApplyAppearance` (and the appearance preview) built the "no beard" value with
  `"".GetStableHashCode()`, which is **not** zero. The decompiled `VisEquipment.SetBeardEquipped` destroys the current
  beard and only calls `AttachItem` when the hash is **not** zero — so passing that number made the game look for a
  prefab that does not exist. Now the mod sends `0` for "no beard" and the real prefab hash only when there is one.
- **The `[AppearanceDiag]` warning** only fires when the prefab **exists** and the object did not appear. Before, any
  companion without a beard raised it on every apply.
- **No other change.** Same `.cfg`, same saves, same `Companions.dll` name, same models and the same AI layer.

## How to update

1. Close the game and **unzip over the old folder** (your `BepInEx\config\AiCompanions.cfg` is kept).
2. Nothing to download: the models are the same ones (Whisper `ggml-small-q5_1.bin`, Kokoro `tts\kokoro\`).
3. Companions in the world are untouched — this only changes what is sent to the game when the appearance is applied.

## Credits

Built on **Offline Companions** by ProfMags and **Offline Companions Reborn** by MaddhattrRocks (MIT) — the upstream of
this fork. Voice: **Kokoro-82M** via **sherpa-onnx**; speech recognition: **Whisper.net**. See
`THIRD_PARTY_NOTICES.md`.
