# Avisos de terceiros (Third-party notices)

Este projeto pode conter código ou ideias derivados de projetos de terceiros. Os avisos de licença
exigidos estão abaixo. Ver também [`NOTICE.md`](NOTICE.md) e [`docs/references.md`](docs/references.md).

---

## Offline Companions Reborn — Licença MIT

Repositório: <https://github.com/MaddhattrRocks/Offline-Companions-Reborn>

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

> Se qualquer trecho de código derivado deste projeto for incorporado ao ModIA, **este aviso deve
> permanecer** (obrigação da licença MIT).
>
> Uso no ModIA (a licença MIT é mantida mesmo quando só a *lógica* é reaproveitada):
>
> - **Fase 2.9 (v0.3.2)** — as correções de coleta seguem a lógica de
>   `src/Companions/Jobs/HarvestController.cs` (`UpdateCollectingDrops`, `_dropScanDelayTimer`,
>   `TryFollowCrackedRock`) e de `src/Companions/Jobs/MHR_GatherRules.cs` (`NearestLog`,
>   `DropCenter`). A implementação no ModIA é própria (adaptada ao modelo de estados do
>   `CompanionAI` e aos buffers reaproveitados do `CompanionHarvestScanner`), sem cópia de arquivo.
> - **Fase 2.10 (v0.3.3)** — as correções de movimento/combate seguem a lógica de
>   `src/Companions/AI/MHR_Mover.cs` (`LedgeAhead`, `DropAhead`, `DoorAhead`, `OpenDoor`,
>   `CloseDoor`), `src/Companions/AI/DoorHandler.cs` (`IsValidClosedDoor`) e
>   `src/Companions/AI/CompanionCombatAI.cs` (`UpdateBlocking`). A implementação no ModIA é própria
>   (`CompanionMovement` + o dono do movimento por frame do `CompanionAI`), sem cópia de arquivo.
> - **Fase 2.12 (v0.4.0)** — o **loot de criatura** segue a lógica de
>   `src/Companions/Jobs/MHR_Loot.cs` (`MHR_Loot` + `MHR_Loot_OnDeath_Patch`): patch observador em
>   `Character.OnDeath` para saber quem deu o último golpe, fila limitada de abates, foto dos itens
>   que já estavam no chão (`Before`) para catar só o que aquele abate gerou, espera pelos drops que
>   saem do ragdoll, acompanhamento do corpo e limites de peso/distância/tempo. A implementação no
>   ModIA é própria (`CompanionLoot` + `CompanionLootPatches` + `CompanionAI.UpdateLoot`, adaptados ao
>   modelo de estados, ao dono do movimento por frame e aos buffers reaproveitados), sem cópia de
>   arquivo.
> - **Fase 2.13 (v0.4.1)** — **comida, resistência e habilidades** seguem a lógica de
>   `src/Companions/Companion/CompanionFood.cs` (slots de comida com tempo de queima, curva de bônus
>   `pow(restante/total, 0.3)`, regeneração a cada 10 s, efeito de status da comida e a animação de
>   comer), `src/Companions/Companion/CompanionStamina.cs` + `src/Companions/Patches/StaminaPatches.cs`
>   (stamina própria do NPC com patches em `Character.UseStamina`/`HaveStamina` e o `CheckRun` que
>   impede correr sem stamina) e `src/Companions/Companion/CompanionSkills.cs` +
>   `src/Companions/Patches/SkillPatches.cs` (perícias próprias do NPC com `SkillDef` copiadas do
>   prefab do jogador e patches em `RaiseSkill`/`GetSkillFactor`/`GetRandomSkillFactor`). A
>   implementação no ModIA é própria (`CompanionFood`/`CompanionStamina`/`CompanionSkills` +
>   `CompanionStaminaPatches`/`CompanionSkillPatches`, adaptados ao modelo de estados do `CompanionAI`,
>   ao snapshot em JSON do `CompanionStateStore` e às seções de config em PT-BR), sem cópia de arquivo.
> - **Fase 2.14 (v0.4.2)** — **peso, depósito em baú e tool keeper** seguem a lógica de
>   `src/Companions/Jobs/MHR_GatherRules.cs` (`IsOverweight`, `HandleOverweight`, `DropOff`,
>   `PackFull`, `TryMakeRoom`), `src/Companions/Jobs/MHR_ToolKeeper.cs` (`FindBestInChests`, `BestIn`,
>   `Score`, `ChestFor`, `Deposit`, `RequestUnload`, `UnloadList`) e `src/Companions/Jobs/MHR_Items.cs`
>   (`FindItemByPrefab`, `TransferOne`), além das regras de baú de `MHR_Chests`
>   (`Near`/`Usable`/`InUse`/`Open`) e do fluxo de depósito pendente de
>   `src/Companions/AI/CompanionAI.cs` (`SetPendingDeposit`/`CancelPendingDeposit`): teto de peso com
>   `Inventory.GetTotalWeight()`, procura do baú utilizável mais próximo dentro do raio, viagem até o
>   baú com depósito e retomada da tarefa (ou pausa, quando não há baú), e empréstimo/devolução de
>   ferramenta por `m_toolTier`. A implementação no ModIA é própria (`CompanionDeposit` +
>   `CompanionToolKeeper` + a camada `CompanionAI.UpdateDeposit`, adaptados ao modelo de estados, ao
>   dono do movimento por frame e aos buffers reaproveitados), sem cópia de arquivo.
> - **Fase 2.16a (v0.4.3)** — os **trabalhos (jobs)**: a **infraestrutura de fila única** e os
>   controladores de **caçar** e **pescar** seguem a lógica de `src/Companions/Jobs/HuntController.cs`
>   (estados idle/caçando/voltando, catálogo de presas lido dos prefabs — `HuntCatalog.Ensure`, com
>   `Character` + `CharacterDrop` —, troca por um alvo mais perto, veto por tempo da presa que
>   escapou, coleira do jogador, abate registrado no `MHR_Loot` e "catar antes de caçar de novo"),
>   `src/Companions/Jobs/FishController.cs` (estados mover/arremessar/esperar/puxar, escolha da isca
>   pela chance em `Fish.m_baits`, descanso por stamina antes de arremessar, força de arremesso
>   aprendida de onde o flutuador caiu — `_castScale` —, puxão alternado por fuga/tensão da linha e
>   `RestoreGear` ao parar) e `src/Companions/Patches/MHR_FishingPatches.cs` (os três patches do
>   `FishingFloat` — `Setup`/`GetOwner`/`ReturnBait` — e a chave própria de dono no ZDO, sem a qual o
>   flutuador do companheiro é destruído), além da ordem de tarefas de
>   `src/Companions/AI/CompanionAI.cs` (loot → trabalho → follow, com um controlador por vez atrás do
>   "action mode") e de `src/Companions/Jobs/MHR_JobRules.cs`. A implementação no ModIA é própria
>   (`CompanionJob`/`CompanionJobQueue`/`HuntJob`/`FishJob`/`CompanionFishPatches`, adaptados ao
>   modelo de camadas do `CompanionAI`, ao `CompanionLoot` da Fase 2.12, ao "dono único do movimento
>   por frame" e às seções de config em PT-BR), sem cópia de arquivo.
> - **Fase 2.16b (v0.4.4)** — os trabalhos de **cultivar**, **cozinhar/preparar bebidas** e **operar
>   fundições** seguem a lógica de `src/Companions/Jobs/FarmController.cs` (fases colher/plantar/
>   guardar, `BuildSeedMapping` — varredura de `ZNetScene.instance.m_prefabs` por `Plant` + `Piece` e
>   o custo da peça como semente, `FindRipePickable`, `FindPlantPositionInZone` com espaçamento por
>   `m_growRadius`, checagem de `Heightmap.IsCultivated`/bioma/`PrivateArea.CheckAccess`, `PlantSeed`
>   com `TerrainModifier.SetTriggerOnPlaced` + `Instantiate`, `StoreOutput`), de
>   `src/Companions/Jobs/FarmZone.cs`/`FarmZoneSerializer.cs` (zona de fazenda definida pelo jogador e
>   serializada), de `src/Companions/Jobs/MHR_SeedRules.cs` (famílias semente/cultura, o mínimo de
>   sementes mantido no inventário — `Keep`/`WantsStore` — e o plantio em grupos), de
>   `src/Companions/Jobs/CookController.cs` (`Scan` por `Piece.GetAllPiecesInRadius`, prioridade
>   "retirar o pronto antes de tudo", `AddToCooker` com `RPC_AddItem`/`RPC_AddFuel`,
>   `UpdateCooking` com `RPC_RemoveDoneItem` + `PickUpDrops`, `UpdateFermenting` com
>   `RPC_AddItem(hash)`/`RPC_Tap`, `UpdateCrafting` para o caldeirão), de
>   `src/Companions/Jobs/CookFuel.cs` (`FuelTarget`, `HeatSources`, `Stoke` — manter as fogueiras em
>   uma fração do combustível máximo) e de `src/Companions/Jobs/SmeltController.cs`
>   (`ScanNearbySmelters`, separação carvoeira × forno por `m_maxFuel == 0`,
>   `TryPlanRefill`/`TryPlanFuelRefill`, inserção de uma unidade por vez com `RPC_AddOre`/`RPC_AddFuel`
>   conferindo o ZDO, `CountGroundDropsNearSmelter`), além da UI de `src/Companions/UI/`
>   (`FarmZoneCropPicker`/`FarmZonePlacer`), que ficou **fora** desta entrega. A implementação no
>   ModIA é própria (`FarmJob`/`FarmZone`/`FarmCatalog`/`CookJob`/`SmeltJob`/`JobStations`, adaptados
>   à máquina de estados do `CompanionJob`, ao "dono único do movimento por frame"
>   (`MovementOwner.Farm`/`Cook`/`Smelt`), ao depósito da Fase 2.14 e às seções de config em PT-BR),
>   sem cópia de arquivo.
> - **Fase 2.17 (v0.4.5)** — **base e casa** seguem a lógica de
>   `src/Companions/Jobs/UpkeepController.cs` (as fases <c>Repair</c>/<c>Fuel</c>/<c>SortTake</c>/
>   <c>SortPut</c>/<c>Tidy</c>/<c>Hive</c>/<c>HiveCollect</c>/<c>Home</c> e a escolha por prioridade
>   <c>PlanRepair</c> → <c>PlanFuel</c> → <c>PlanHoney</c> → <c>PlanSort</c>, o martelo na mão com a
>   animação e o custo de stamina/durabilidade — <c>Hammer</c>/<c>PutHammerAway</c>/
>   <c>SwingStamina</c>, a estação de craft da peça em <c>CraftingStation.HaveBuildStationInRange</c>
>   e o <c>WearNTear.Repair()</c> —, o alvo de combustível por fração (<c>FuelTarget</c>/
>   <c>Need</c>/<c>RefuelOn</c>), a colmeia por <c>RPC_Extract</c> + catar o mel
>   (<c>PlanHoney</c>/<c>UpdateHive</c>/<c>UpdateHiveCollect</c> com o nível lido do ZDO), a
>   consolidação "igual com igual" e a junção de pilhas parciais (<c>PlanSort</c>/<c>Merge</c>/
>   <c>RoomIn</c>/<c>Key</c>/<c>Chests</c>/<c>BestDestination</c>) e o retorno ao centro
>   (<c>UpdateHome</c>), além da casa de `src/Companions/Companion/CompanionSetup.cs`
>   (<c>SetHomePosition</c>/<c>GetHomePosition</c>/<c>HasHomePosition</c>) e de
>   `src/Companions/AI/MHR_ToolKeeper.cs` (<c>NearHome</c>/<c>HomeRange</c>/<c>AtHomeRange</c>),
>   com a zona de casa desenhada por `src/Companions/Jobs/HomeZoneVisual.cs` e ajustada por
>   `src/Companions/UI/HomeZonePanel.cs` (ambos **fora** desta entrega). A implementação no ModIA é
>   própria (`CompanionHome`/`CompanionBaseCare`/`CompanionBaseCareConfig`/`UpkeepJob`, adaptados à
>   máquina de estados do `CompanionJob`, ao "dono único do movimento por frame"
>   (`MovementOwner.Upkeep`), ao depósito da Fase 2.14 e às seções de config em PT-BR), sem cópia de
>   arquivo.
> - **Fase 2.18 (v0.4.6)** — a **qualidade de vida** segue a lógica de
>   `src/Companions/Companion/CompanionRest.cs` (as máquinas de <c>DirectSit</c>/<c>DirectSleep</c>/
>   <c>BeginSleep</c>/<c>FinalizeSleep</c>/<c>StopSleeping</c>/<c>StopSitting</c>/
>   <c>UpdateSitting</c>/<c>UpdateSleeping</c>, a procura de fogueira acesa em
>   <c>FindNearbyFire</c> — <c>Fireplace.IsBurning</c> + <c>EffectArea</c> de calor —, a cama
>   <b>livre</b> de <c>TryClaimBedIfNeeded</c>/<c>FindUnclaimedBedNear</c>/<c>MHR_BedOwnerOk</c>/
>   <c>MHR_BedIsMine</c> — nunca a cama que é o ponto de spawn do dono —, o <c>PinToBed</c> com
>   gravidade desligada e colisão ignorada, o <c>SetBool("emote_sit")</c>/<c>SetBool("attach_bed")</c>
>   no <c>ZSyncAnimation</c> e o <c>ZDOVars.s_inBed</c>), de
>   `src/Companions/Companion/CompanionRestedBuff.cs` (o descanso com aquecimento — <c>StartResting</c>/
>   <c>StopResting</c>/<c>ApplyRestedBuff</c> —, os bônus lidos do <c>SE_Rested</c> via
>   <c>ObjectDB.GetStatusEffect</c> — <c>m_baseTTL</c>, <c>m_TTLPerComfortLevel</c>,
>   <c>m_healthRegenMultiplier</c>, <c>m_staminaRegenMultiplier</c>, <c>m_raiseSkillModifier</c> — e o
>   conforto por <c>Cover.GetCoverForPoint</c> + <c>SE_Rested.CalculateComfortLevel</c>), de
>   `src/Companions/Patches/PortalPatches.cs` (seguir pelo portal: enfileirar os companheiros em
>   follow quando o jogador teleporta e "warpar" para o ponto de chegada —
>   <c>WarpCompanion</c>: posição no ZDO + <c>transform</c> + <c>Rigidbody</c> +
>   <c>ZoneSystem.FindFloor</c>) e de `src/Companions/AI/CompanionAI.cs` +
>   `src/Companions/UI/CompanionInteract.cs` (a carroça: <c>SetPendingCart</c>/<c>CancelPendingCart</c>
>   e o passo <c>PendingCartAttach</c>, e o <c>DirectCart</c> que usa a carroça <b>livre</b> mais
>   próxima e solta quando já está presa — <c>m_attachPoint.position - m_attachOffset</c>,
>   <c>Vagon.Interact</c>, <c>IsAttached</c>). No ModIA a detecção do portal é por
>   <b>observação do estado público do jogador</b> (<c>Player.IsTeleporting</c>, <b>sem patch
>   Harmony</b>), o descanso é um estado do próprio mod (sem criar <c>StatusEffect</c>) e a
>   implementação é própria (`CompanionRest`/`CompanionPortalFollow`/`CompanionCart` + as camadas
>   <c>CompanionAI.UpdateRest</c>/<c>UpdatePortalFollow</c>/<c>UpdateCart</c>, adaptadas ao modelo de
>   camadas, ao "dono único do movimento por frame"
>   (`MovementOwner.Rest`/`Sleep`/`Portal`/`Cart`), ao `CompanionStamina`/`CompanionSkills` da Fase
>   2.13 e às seções de config em PT-BR), sem cópia de arquivo.


---

## Bibliotecas e modelos redistribuídos no pacote (resumo e atribuição)

O **pacote de release** (o `.zip` publicado no GitHub) redistribui, junto com o `Companions.dll`, os
binários e os modelos que o mod carrega em runtime. Os **textos completos** viajam **dentro do
pacote**, na pasta `LICENSES\` (ver [`LICENSES/LEIA-ME.txt`](../LICENSES/LEIA-ME.txt)) — o que está
abaixo é o resumo e a atribuição de cada peça.

| Componente | Onde (na pasta do plugin) | Licença | Texto no pacote |
|---|---|---|---|
| **Whisper.net** — Sandro Hanea | `Whisper.net.dll` | **MIT** | `LICENSES\Whisper.net-LICENSE.txt` |
| **whisper.cpp / ggml** — The ggml authors | `runtimes\win-x64\whisper.dll`, `ggml*.dll` | **MIT** | `LICENSES\whisper.cpp-LICENSE.txt` |
| **Pesos do Whisper** — OpenAI (convertidos pelo whisper.cpp) | `whisper\ggml-small-q5_1.bin` | **MIT** | `LICENSES\openai-whisper-modelos-LICENSE.txt` |
| **sherpa-onnx** — The Next-gen Kaldi development team | `sherpa-onnx.dll`, `runtimes\win-x64\native\sherpa-onnx-c-api.dll` | **Apache-2.0** | `LICENSES\sherpa-onnx-LICENSE.txt` |
| **onnxruntime** — Microsoft Corporation | `runtimes\win-x64\native\onnxruntime.dll` | **MIT** | `LICENSES\onnxruntime-LICENSE.txt` |
| **Kokoro-82M** — hexgrad | `tts\kokoro\model.int8.onnx`, `tts\kokoro\voices.bin` | **Apache-2.0** | `LICENSES\Kokoro-82M-LICENSE.txt` |
| **espeak-ng-data** — projeto espeak-ng | `tts\kokoro\espeak-ng-data\` | **GPL-3.0** | `LICENSES\espeak-ng-data-LICENSE.txt` |
| **ModAI-BR-Companions** — este fork | `Companions.dll` | **MIT** (do upstream) | `LICENSES\ModAI-BR-Companions-LICENSE.txt` |

> **A única peça copyleft é o `espeak-ng-data`** (GPL-3.0): ele é usado pelo modelo de voz como
> **dados** (tabelas de fonemas do espeak-ng), **não** como biblioteca ligada ao nosso código — o
> plugin não referencia nem linka nada do espeak-ng. O texto completo da GPL-3.0 acompanha o pacote.
>
> Os **modelos e binários de terceiros não estão versionados neste repositório** (ficam fora do git,
> por tamanho): eles existem no **pacote de release** e na instalação local. O `LICENSES\` é gerado
> pelo build (alvo `DeployLicenses` no `Companions.csproj`), a partir da pasta `LICENSES/` deste
> repositório.
