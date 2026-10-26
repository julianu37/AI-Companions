# Third-party notices

This project may contain code or ideas derived from third-party projects. The required license notices are
below. See also [`NOTICE.md`](NOTICE.md) and [`docs/references.md`](docs/references.md).

---

## Offline Companions Reborn — MIT License

Repository: <https://github.com/MaddhattrRocks/Offline-Companions-Reborn>

```
MIT License

Copyright (c) ProfMags (original "Offline Companions" mod)
Copyright (c) 2026 Maddhattr (Valheim 1.0 compatibility fork, "Offline Companions - Reborn")

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

> If any piece of code derived from this project is incorporated into ModIA, **this notice must remain**
> (an obligation of the MIT license).
>
> Use in ModIA (the MIT license is kept even when only the *logic* is reused):
>
> - **Phase 2.9 (v0.3.2)** — the gathering fixes follow the logic of
>   `src/Companions/Jobs/HarvestController.cs` (`UpdateCollectingDrops`, `_dropScanDelayTimer`,
>   `TryFollowCrackedRock`) and of `src/Companions/Jobs/MHR_GatherRules.cs` (`NearestLog`,
>   `DropCenter`). The implementation in ModIA is our own (adapted to the state model of
>   `CompanionAI` and to the reused buffers of `CompanionHarvestScanner`), with no file copy.
> - **Phase 2.10 (v0.3.3)** — the movement/combat fixes follow the logic of
>   `src/Companions/AI/MHR_Mover.cs` (`LedgeAhead`, `DropAhead`, `DoorAhead`, `OpenDoor`,
>   `CloseDoor`), `src/Companions/AI/DoorHandler.cs` (`IsValidClosedDoor`) and
>   `src/Companions/AI/CompanionCombatAI.cs` (`UpdateBlocking`). The implementation in ModIA is our own
>   (`CompanionMovement` + the per-frame movement owner of `CompanionAI`), with no file copy.
> - **Phase 2.12 (v0.4.0)** — the **creature loot** follows the logic of
>   `src/Companions/Jobs/MHR_Loot.cs` (`MHR_Loot` + `MHR_Loot_OnDeath_Patch`): an observer patch on
>   `Character.OnDeath` to know who dealt the last blow, a bounded queue of kills, a snapshot of the items
>   that were already on the ground (`Before`) so it only picks up what that kill produced, a wait for the drops
>   that come out of the ragdoll, tracking of the body and limits of weight/distance/time. The implementation in
>   ModIA is our own (`CompanionLoot` + `CompanionLootPatches` + `CompanionAI.UpdateLoot`, adapted to the
>   state model, to the per-frame movement owner and to the reused buffers), with no file copy.
> - **Phase 2.13 (v0.4.1)** — **food, stamina and skills** follow the logic of
>   `src/Companions/Companion/CompanionFood.cs` (food slots with a burn time, the bonus curve
>   `pow(remaining/total, 0.3)`, regeneration every 10 s, the food status effect and the eating
>   animation), `src/Companions/Companion/CompanionStamina.cs` + `src/Companions/Patches/StaminaPatches.cs`
>   (the NPC's own stamina with patches on `Character.UseStamina`/`HaveStamina` and the `CheckRun` that
>   prevents running without stamina) and `src/Companions/Companion/CompanionSkills.cs` +
>   `src/Companions/Patches/SkillPatches.cs` (the NPC's own skills with `SkillDef` copied from the
>   player prefab and patches on `RaiseSkill`/`GetSkillFactor`/`GetRandomSkillFactor`). The
>   implementation in ModIA is our own (`CompanionFood`/`CompanionStamina`/`CompanionSkills` +
>   `CompanionStaminaPatches`/`CompanionSkillPatches`, adapted to the state model of `CompanionAI`,
>   to the JSON snapshot of `CompanionStateStore` and to the PT-BR config sections), with no file copy.
> - **Phase 2.14 (v0.4.2)** — **weight, chest deposit and tool keeper** follow the logic of
>   `src/Companions/Jobs/MHR_GatherRules.cs` (`IsOverweight`, `HandleOverweight`, `DropOff`,
>   `PackFull`, `TryMakeRoom`), `src/Companions/Jobs/MHR_ToolKeeper.cs` (`FindBestInChests`, `BestIn`,
>   `Score`, `ChestFor`, `Deposit`, `RequestUnload`, `UnloadList`) and `src/Companions/Jobs/MHR_Items.cs`
>   (`FindItemByPrefab`, `TransferOne`), plus the chest rules of `MHR_Chests`
>   (`Near`/`Usable`/`InUse`/`Open`) and the pending deposit flow of
>   `src/Companions/AI/CompanionAI.cs` (`SetPendingDeposit`/`CancelPendingDeposit`): a weight cap with
>   `Inventory.GetTotalWeight()`, a search for the nearest usable chest inside the radius, a trip to the
>   chest with the deposit and a resumption of the task (or a pause, when there is no chest), and the lending/return of a
>   tool through `m_toolTier`. The implementation in ModIA is our own (`CompanionDeposit` +
>   `CompanionToolKeeper` + the `CompanionAI.UpdateDeposit` layer, adapted to the state model, to the
>   per-frame movement owner and to the reused buffers), with no file copy.
> - **Phase 2.16a (v0.4.3)** — the **jobs**: the **single-queue infrastructure** and the
>   **hunt** and **fish** controllers follow the logic of `src/Companions/Jobs/HuntController.cs`
>   (idle/hunting/returning states, a prey catalog read from the prefabs — `HuntCatalog.Ensure`, with
>   `Character` + `CharacterDrop` —, swapping for a closer target, a time veto on the prey that
>   escaped, the player leash, the kill recorded in `MHR_Loot` and "pick up before hunting again"),
>   `src/Companions/Jobs/FishController.cs` (move/cast/wait/pull states, bait choice by
>   the chance in `Fish.m_baits`, a stamina break before casting, the cast strength
>   learned from where the float landed — `_castScale` —, a pull alternating by escape/line tension and
>   `RestoreGear` when stopping) and `src/Companions/Patches/MHR_FishingPatches.cs` (the three patches of
>   `FishingFloat` — `Setup`/`GetOwner`/`ReturnBait` — and the own owner key in the ZDO, without which the
>   companion's float is destroyed), plus the task order of
>   `src/Companions/AI/CompanionAI.cs` (loot → job → follow, with one controller at a time behind the
>   "action mode") and of `src/Companions/Jobs/MHR_JobRules.cs`. The implementation in ModIA is our own
>   (`CompanionJob`/`CompanionJobQueue`/`HuntJob`/`FishJob`/`CompanionFishPatches`, adapted to the
>   layer model of `CompanionAI`, to the `CompanionLoot` of Phase 2.12, to the "single owner of the movement
>   per frame" and to the PT-BR config sections), with no file copy.
> - **Phase 2.16b (v0.4.4)** — the **farming**, **cooking/preparing meads** and **operating
>   smelters** jobs follow the logic of `src/Companions/Jobs/FarmController.cs` (harvest/plant/
>   store phases, `BuildSeedMapping` — a scan of `ZNetScene.instance.m_prefabs` for `Plant` + `Piece` and
>   the cost of the piece as the seed, `FindRipePickable`, `FindPlantPositionInZone` with spacing by
>   `m_growRadius`, a check of `Heightmap.IsCultivated`/biome/`PrivateArea.CheckAccess`, `PlantSeed`
>   with `TerrainModifier.SetTriggerOnPlaced` + `Instantiate`, `StoreOutput`), of
>   `src/Companions/Jobs/FarmZone.cs`/`FarmZoneSerializer.cs` (a farm zone defined by the player and
>   serialized), of `src/Companions/Jobs/MHR_SeedRules.cs` (seed/crop families, the minimum of
>   seeds kept in the inventory — `Keep`/`WantsStore` — and planting in groups), of
>   `src/Companions/Jobs/CookController.cs` (`Scan` through `Piece.GetAllPiecesInRadius`, the priority
>   "take out the ready one before anything else", `AddToCooker` with `RPC_AddItem`/`RPC_AddFuel`,
>   `UpdateCooking` with `RPC_RemoveDoneItem` + `PickUpDrops`, `UpdateFermenting` with
>   `RPC_AddItem(hash)`/`RPC_Tap`, `UpdateCrafting` for the cauldron), of
>   `src/Companions/Jobs/CookFuel.cs` (`FuelTarget`, `HeatSources`, `Stoke` — keeping the fires at
>   a fraction of the maximum fuel) and of `src/Companions/Jobs/SmeltController.cs`
>   (`ScanNearbySmelters`, separating kiln × furnace by `m_maxFuel == 0`,
>   `TryPlanRefill`/`TryPlanFuelRefill`, inserting one unit at a time with `RPC_AddOre`/`RPC_AddFuel`
>   checking the ZDO, `CountGroundDropsNearSmelter`), plus the UI of `src/Companions/UI/`
>   (`FarmZoneCropPicker`/`FarmZonePlacer`), which stayed **out** of this delivery. The implementation in
>   ModIA is our own (`FarmJob`/`FarmZone`/`FarmCatalog`/`CookJob`/`SmeltJob`/`JobStations`, adapted
>   to the state machine of `CompanionJob`, to the "single owner of the movement per frame"
>   (`MovementOwner.Farm`/`Cook`/`Smelt`), to the deposit of Phase 2.14 and to the PT-BR config sections),
>   with no file copy.
> - **Phase 2.17 (v0.4.5)** — **base and home** follow the logic of
>   `src/Companions/Jobs/UpkeepController.cs` (the <c>Repair</c>/<c>Fuel</c>/<c>SortTake</c>/
>   <c>SortPut</c>/<c>Tidy</c>/<c>Hive</c>/<c>HiveCollect</c>/<c>Home</c> phases and the priority choice
>   <c>PlanRepair</c> → <c>PlanFuel</c> → <c>PlanHoney</c> → <c>PlanSort</c>, the hammer in hand with the
>   animation and the stamina/durability cost — <c>Hammer</c>/<c>PutHammerAway</c>/
>   <c>SwingStamina</c>, the piece's craft station in <c>CraftingStation.HaveBuildStationInRange</c>
>   and the <c>WearNTear.Repair()</c> —, the fuel target by fraction (<c>FuelTarget</c>/<c>Need</c>/
>   <c>RefuelOn</c>), the hive through <c>RPC_Extract</c> + picking up the honey
>   (<c>PlanHoney</c>/<c>UpdateHive</c>/<c>UpdateHiveCollect</c> with the level read from the ZDO), the
>   "like with like" consolidation and the merging of partial stacks (<c>PlanSort</c>/<c>Merge</c>/
>   <c>RoomIn</c>/<c>Key</c>/<c>Chests</c>/<c>BestDestination</c>) and the return to the center
>   (<c>UpdateHome</c>), plus the home of `src/Companions/Companion/CompanionSetup.cs`
>   (<c>SetHomePosition</c>/<c>GetHomePosition</c>/<c>HasHomePosition</c>) and of
>   `src/Companions/AI/MHR_ToolKeeper.cs` (<c>NearHome</c>/<c>HomeRange</c>/<c>AtHomeRange</c>),
>   with the home zone drawn by `src/Companions/Jobs/HomeZoneVisual.cs` and adjusted by
>   `src/Companions/UI/HomeZonePanel.cs` (both **out** of this delivery). The implementation in ModIA is
>   our own (`CompanionHome`/`CompanionBaseCare`/`CompanionBaseCareConfig`/`UpkeepJob`, adapted to the
>   state machine of `CompanionJob`, to the "single owner of the movement per frame"
>   (`MovementOwner.Upkeep`), to the deposit of Phase 2.14 and to the PT-BR config sections), with no file
>   copy.
> - **Phase 2.18 (v0.4.6)** — the **quality of life** follows the logic of
>   `src/Companions/Companion/CompanionRest.cs` (the machines of <c>DirectSit</c>/<c>DirectSleep</c>/
>   <c>BeginSleep</c>/<c>FinalizeSleep</c>/<c>StopSleeping</c>/<c>StopSitting</c>/
>   <c>UpdateSitting</c>/<c>UpdateSleeping</c>, the search for a lit fire in
>   <c>FindNearbyFire</c> — <c>Fireplace.IsBurning</c> + <c>EffectArea</c> of heat —, the <b>free</b> bed
>   of <c>TryClaimBedIfNeeded</c>/<c>FindUnclaimedBedNear</c>/<c>MHR_BedOwnerOk</c>/
>   <c>MHR_BedIsMine</c> — never the bed that is the owner's spawn point —, the <c>PinToBed</c> with
>   gravity disabled and collision ignored, the <c>SetBool("emote_sit")</c>/<c>SetBool("attach_bed")</c>
>   in the <c>ZSyncAnimation</c> and the <c>ZDOVars.s_inBed</c>), of
>   `src/Companions/Companion/CompanionRestedBuff.cs` (the rest with warmth — <c>StartResting</c>/
>   <c>StopResting</c>/<c>ApplyRestedBuff</c> —, the bonuses read from the <c>SE_Rested</c> through
>   <c>ObjectDB.GetStatusEffect</c> — <c>m_baseTTL</c>, <c>m_TTLPerComfortLevel</c>,
>   <c>m_healthRegenMultiplier</c>, <c>m_staminaRegenMultiplier</c>, <c>m_raiseSkillModifier</c> — and the
>   comfort through <c>Cover.GetCoverForPoint</c> + <c>SE_Rested.CalculateComfortLevel</c>), of
>   `src/Companions/Patches/PortalPatches.cs` (following through the portal: queueing the companions in
>   follow when the player teleports and "warping" to the arrival point —
>   <c>WarpCompanion</c>: position in the ZDO + <c>transform</c> + <c>Rigidbody</c> +
>   <c>ZoneSystem.FindFloor</c>) and of `src/Companions/AI/CompanionAI.cs` +
>   `src/Companions/UI/CompanionInteract.cs` (the cart: <c>SetPendingCart</c>/<c>CancelPendingCart</c>
>   and the <c>PendingCartAttach</c> step, and the <c>DirectCart</c> that uses the nearest <b>free</b>
>   cart and lets go when it is already attached — <c>m_attachPoint.position - m_attachOffset</c>,
>   <c>Vagon.Interact</c>, <c>IsAttached</c>). In ModIA the portal detection is by
>   <b>observing the player's public state</b> (<c>Player.IsTeleporting</c>, <b>without a Harmony
>   patch</b>), the rest is a state of the mod itself (with no <c>StatusEffect</c> created) and the
>   implementation is our own (`CompanionRest`/`CompanionPortalFollow`/`CompanionCart` + the layers
>   <c>CompanionAI.UpdateRest</c>/<c>UpdatePortalFollow</c>/<c>UpdateCart</c>, adapted to the layer model,
>   to the "single owner of the movement per frame"
>   (`MovementOwner.Rest`/`Sleep`/`Portal`/`Cart`), to the `CompanionStamina`/`CompanionSkills` of Phase
>   2.13 and to the PT-BR config sections), with no file copy.


---

## Libraries and models redistributed in the package (summary and attribution)

The **release package** (the `.zip` published on GitHub) redistributes, along with `Companions.dll`, the
binaries and the models the mod loads at runtime. The **full texts** travel **inside the
package**, in the `LICENSES\` folder (see [`LICENSES/LEIA-ME.txt`](../LICENSES/LEIA-ME.txt)) — what is
below is the summary and the attribution of each piece.

| Component | Where (in the plugin folder) | License | Text in the package |
|---|---|---|---|
| **Whisper.net** — Sandro Hanea | `Whisper.net.dll` | **MIT** | `LICENSES\Whisper.net-LICENSE.txt` |
| **whisper.cpp / ggml** — The ggml authors | `runtimes\win-x64\whisper.dll`, `ggml*.dll` | **MIT** | `LICENSES\whisper.cpp-LICENSE.txt` |
| **Whisper weights** — OpenAI (converted by whisper.cpp) | `whisper\ggml-small-q5_1.bin` | **MIT** | `LICENSES\openai-whisper-modelos-LICENSE.txt` |
| **sherpa-onnx** — The Next-gen Kaldi development team | `sherpa-onnx.dll`, `runtimes\win-x64\native\sherpa-onnx-c-api.dll` | **Apache-2.0** | `LICENSES\sherpa-onnx-LICENSE.txt` |
| **onnxruntime** — Microsoft Corporation | `runtimes\win-x64\native\onnxruntime.dll` | **MIT** | `LICENSES\onnxruntime-LICENSE.txt` |
| **Kokoro-82M** — hexgrad | `tts\kokoro\model.int8.onnx`, `tts\kokoro\voices.bin` | **Apache-2.0** | `LICENSES\Kokoro-82M-LICENSE.txt` |
| **espeak-ng-data** — the espeak-ng project | `tts\kokoro\espeak-ng-data\` | **GPL-3.0** | `LICENSES\espeak-ng-data-LICENSE.txt` |
| **AI Companions** — this project | `Companions.dll` | **MIT** (from the upstream) | `LICENSES\AI_Companions-LICENSE.txt` |

> **The only copyleft piece is `espeak-ng-data`** (GPL-3.0): it is used by the voice model as
> **data** (the espeak-ng phoneme tables), **not** as a library linked to our code — the
> plugin neither references nor links anything from espeak-ng. The full GPL-3.0 text ships with the package.
>
> The **third-party models and binaries are not versioned in this repository** (they stay outside git,
> because of their size): they exist in the **release package** and in the local installation. The `LICENSES\`
> is generated by the build (the `DeployLicenses` target in `Companions.csproj`), from the `LICENSES/` folder of
> this repository.
