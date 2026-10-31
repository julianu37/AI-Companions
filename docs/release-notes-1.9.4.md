# 1.9.4 — in progress

![AI Companions — AI NPC companions for Valheim](https://raw.githubusercontent.com/julianu37/AI-Companions/main/docs/images/banner.png)

> **The 1.9.3 fix was half of the fix.** 1.9.3 made "no beard" hash `0` — which is correct — but the game's attach cache
> was cleared to `0` as well, and `0` is exactly the value of "no beard". `VisEquipment.SetBeardEquipped` returns early
> when the hash matches the cache, so the beard of the previous appearance was never destroyed: a **female companion
> kept the beard of the male preview**. Fixed: **the cache is cleared to `int.MinValue`**.

## What changed

- **The attach-cache sentinel.** `CompanionSetup.ResetVisualItemCache` (the Fase 3.21 fix that makes every appearance
  apply a real attach) wrote `0` into `m_currentHairItemHash`/`m_currentBeardItemHash`. `0` is a *meaningful* value for
  the game ("no beard"), not a neutral one, so the very next `SetBeardEquipped(0)` matched the cache and returned
  **before** destroying the old beard. The sentinel is `int.MinValue` now — never a real item hash — so `0` goes
  through the destroy branch and the real hashes still attach.
- **Both paths are covered:** the real companion (`ApplyAppearance`) and the 3D preview of the appearance editor call
  `VisEquipment.UpdateVisuals` right after the setters, which reaches `UpdateEquipmentVisuals` → `SetBeardEquipped`.
- **A brand new companion was already fine** (there is no beard to destroy); the bug showed up when switching the
  preview from male to female and when editing the appearance of an existing companion.
- **Workaround on 1.9.3:** pick the `BeardNone` item in the beard list (a real hash for an invisible beard) instead of
  "None".
- **The gather cap now applies when a word net corrected the action.** "collect 10 wood" answered by the model as
  `pickup` is corrected to `wood` with the number read from the player's own text - but the job was started with the
  model's amount (0), so no cap was set: the companion gathered forever while the chat said "(10)". The normal path
  (the model returns the gather action with its number) was already fine - confirmed in game on 31/10/2026.
- **A gather order with no number no longer gets one invented by the model.** `qwen2.5-3b-instruct` answers
  "collect stones" with `{"action":"stone","amount":10}`; that silently capped an order that is meant to run until the
  player stops it (unloading at a chest when the pack fills). For the jobs that take a cap, the number is only used
  when the player's own message has one.
- **The `tokens/s` line in the log** said it counted "prompt and response together" but divided only the response
  tokens; it now says "generation - response only" and points at the `[IA] Prompt:` line above.
- **Odin's Shrine charges the offering again.** The hire panel showed the price but ran the starter's **free** spawn,
  and `StarterFix.MarkSpawned()` consumed the free starter of the world on top of it. The `if (_hire)` branch was lost
  when the 1.5.6 merge resolved `OnConfirm`; it is back, and `HireWithCoins` now takes the AI draft (voice, personality
  and the name typed in the panel) through an internal overload - `CompanionAiDraft` is `internal`, so a public
  signature with it is CS0051. The normal starter panel is untouched.
- **Changing the companion's type keeps the player's choices.** `HC_FarmZones` (the planting zones, with the crop of
  each one) and `HC_HomeRadius` are copied now, and a recovery in progress is carried over as a block
  (`HC_Recovering`/`HC_RecoverStance`/`HC_RecoverPickup`). Without it the new body was born with auto-pickup off and no
  flag to finish the recovery - not even setting a stance fixed it.
- **Dying twice while recovering no longer loses the setting.** The recovery forces auto-pickup off and the stance
  Passive; the death used to save those forced values as if the player had chosen them, so the next revive remembered
  "off" and "Passive" for good. The death now reads `HC_RecoverPickup`/`HC_RecoverStance` when the companion is
  recovering.
- **F8: a pending key binding is not cancelled by Esc anymore**, and the next key press is not written into the config
  by accident.
- **The controller legend is back** (`BuildPadHint` was dead code - nothing called it).
- **The starter and appearance panels lost the dark overlay** (`s_noDark` was never assigned; the build now has **zero
  warnings**).
- **No other change.** Same `.cfg`, same saves, same `Companions.dll` name, same models and the same AI layer.

## How to update

1. Close the game and **unzip over the old folder** (your `BepInEx\config\AiCompanions.cfg` is kept).
2. Nothing to download: the models are the same ones (Whisper `ggml-small-q5_1.bin`, Kokoro `tts\kokoro\`).
3. Companions in the world are untouched — this only changes what is sent to the game when the appearance is applied.

## Credits

Built on **Offline Companions** by ProfMags and **Offline Companions Reborn** by MaddhattrRocks (MIT) — the upstream of
this fork. Voice: **Kokoro-82M** via **sherpa-onnx**; speech recognition: **Whisper.net**. See
`THIRD_PARTY_NOTICES.md`.
