# Plano das fases (2.13 → 2.18) — com mapa do Offline Companions Reborn

Referência (MIT, uso livre): <https://github.com/MaddhattrRocks/Offline-Companions-Reborn>
Checkout local (reference do OpenCode): `...\repos\github.com\MaddhattrRocks\Offline-Companions-Reborn`.

**Foco atual: single-player.** ZDO/multiplayer adiados (ver `docs/vision.md`).
Regra: **reaproveitar lógica** da referência (com atribuição MIT), sempre confirmando APIs no
`assembly_valheim.dll` 1.0.17.

---

## 2.13 — Comida, resistência (stamina) e habilidades

**Objetivo:** o companheiro **come** (consome comida do próprio inventário, ganha os bônus de HP/regen),
tem **stamina** que importa (correr/atacar/minerar gastam) e **evolui skills** (cortar, picareta,
combate…). Hoje ele não passa fome nem cansa — o que mais o deixa "decorativo".

| Referência | O que aproveitar |
|---|---|
| `Companion/CompanionFood.cs` | gestão de comida (escolher/comer, timers, bônus, reposição) |
| `Companion/CompanionStamina.cs` | stamina própria, gasto/regen, dormir/descansar |
| `Companion/CompanionSkills.cs` | skills do companheiro e evolução |
| `Companion/CompanionTierData.cs`, `CompanionTierDef.cs` | tiers de progressão |
| `Companion/OverheadStats.cs` | HUD de stats sobre a cabeça (opcional) |

**Nosso:** `Companion/CompanionFood.cs`, `CompanionStamina.cs`, `CompanionSkills.cs`; integrar em
`CompanionAI`/`CompanionState` (comida entra no snapshot).
**Atenção:** hoje o `CompanionSkillPatches` **silencia** `Character.RaiseSkill` para o companheiro — ao
implementar skills, rever esse patch.

**Config:** `[Companheiro]` comer automaticamente, limiar de fome/stamina; `[Skills]` evolução ligada.
**Aceite:** ele come sozinho quando a comida acaba (bônus visível), cansa ao correr/minerar, e a skill
sobe ao cortar/minerar/lutar.

---

## 2.14 — Peso, depósito em baú e *tool keeper* (item #7 da auditoria)

**Objetivo:** inventário com **peso**; ao encher, **depositar num baú** e **retomar** o trabalho (hoje
ele **encerra** a tarefa); buscar/devolver a ferramenta certa.

| Referência | O que aproveitar |
|---|---|
| `Jobs/MHR_GatherRules.cs` | `IsOverweight`, `HandleOverweight`, `DropOff`, `PackFull` |
| `Jobs/MHR_ToolKeeper.cs` | `FindBestTool`/`BestInChests` (por `m_toolTier`), empréstimo/devolução |
| `Jobs/MHR_Items.cs`, `Jobs/MHR_PileTaker.cs` | helpers de itens/pilhas |

**Nosso:** estender `CompanionInventory` (peso/limite), novo `CompanionDeposit` (baú mais próximo),
`CompanionToolKeeper`.
**Config:** `[Coleta]` limite de peso, raio de busca de baú, comportamento ao encher (depositar/pausar).
**Aceite:** com inventário cheio ele **vai ao baú, deposita e volta** a coletar (não encerra a tarefa).

---

## 2.16 — Trabalhos (jobs)

**Objetivo:** a mesma interface de "aponte para o trabalho" para: **caçar · cultivar · pescar ·
cozinhar/preparar bebidas · operar fundições**.

> **Status:** **2.16a ✅ (v0.4.3)** — infraestrutura de jobs (`Companion/Jobs/`: `CompanionJob`,
> `CompanionJobQueue` com prioridade explícita `combate > coleta > depósito > loot > trabalho >
> follow`) + **caçar** (`HuntJob`) e **pescar** (`FishJob` + os patches do `FishingFloat`).
> Console: `modia_job hunt|fish|stop`.
> **2.16b ⏳** — cultivar (`FarmController`/`FarmZone`), cozinhar (`CookController`/`CookFuel`),
> fundições (`SmeltController`), caçar **pássaros** (arco: o "Birding" da referência) e a UI do
> trabalho (menu/hover).

| Referência | Job |
|---|---|
| `Jobs/HuntController.cs` | **caçar** (alvo por tipo, abate + loot) |
| `Jobs/FarmController.cs`, `FarmZone.cs`, `FarmZoneSerializer.cs`, `FarmZoneVisual.cs`, `MHR_SeedRules.cs` | **cultivar** (zonas de fazenda, sementes, plantio/colheita) |
| `Jobs/FishController.cs` | **pescar** (lançar, fisgar, puxar) |
| `Jobs/CookController.cs`, `CookFuel.cs` | **cozinhar** / bebidas (estações, combustível) |
| `Jobs/SmeltController.cs` | **fundições** (alimentar/retirar) |
| `Jobs/IdleController.cs` | estado ocioso (faz o companheiro "viver") |
| `UI/FarmZoneCropPicker.cs`, `UI/FarmZonePlacer.cs` | UI de zona de fazenda |

**Nosso:** `Companion/Jobs/` com uma **fila de tarefas** (`CompanionJobQueue`) e um controlador por job,
todos respeitando a prioridade **combate > coleta > job > follow**.
**Config:** `[Jobs]` ligar/desligar cada job, raios, limites.
**Aceite:** `modia_job hunt|farm|fish|cook|smelt` inicia e o companheiro executa de ponta a ponta.

---

## 2.17 — Base e casa

**Objetivo:** **cuidar da base** (reparar construções, reabastecer fogueiras, **ordenar baús**) e ter uma
**casa/home** (onde deposita, dorme e volta).

| Referência | O que aproveitar |
|---|---|
| `Jobs/RepairController.cs` | reparar construções/itens |
| `Jobs/UpkeepController.cs`, `UpkeepHoney.cs`, `UpkeepPiles.cs` | manutenção (fogueiras, colmeias, pilhas) |
| `Jobs/HomeZoneVisual.cs`, `UI/HomeZonePanel.cs` | **zona de casa** (home) e sua UI |
| `Companion/CompanionRest.cs` | descanso/recuperação na casa |

**Nosso:** `Companion/CompanionBaseCare.cs`, `CompanionHome.cs` (+ visual da zona), integrando com o
depósito (2.14).
**Config:** `[Base]` ligar cuidado, raio, quais tarefas (reparar/abastecer/ordenar).
**Aceite:** `modia_home set` define a casa; `modia_basecare on` faz ele reparar/abastecer/ordenar.

---

## 2.18 — Qualidade de vida

**Objetivo:** **sentar perto da fogueira**, **dormir em camas**, **seguir você através de portais**,
**puxar sua carroça**.

| Referência | O que aproveitar |
|---|---|
| `Companion/CompanionRest.cs` | sentar/descansar perto do fogo, dormir |
| `Companion/CompanionRestedBuff.cs` | buff de descanso |
| `AI/ReturnLogic.cs`, `MHR_Roster.cs`, `MHR_Shrine.cs`, `MHR_RespawnTracker.cs`, `Companion/CompanionTombstoneResurrect.cs` | voltar para casa, "roster", shrine, respawn |
| `AI/MHR_Follow.cs`, `MHR_Jump.cs` | follow melhorado (portais, saltos) |
| `AI/WaterSafety.cs`, `TorchOpener.cs` | segurança na água, tocha |
| `UI/CompanionRadialMenu.cs`, `CompanionInteract*.cs`, `CompanionPanel.cs`, `CompanionHudPanel.cs`, `ConfigPanel.cs` | **menu radial**, painel de interação, HUD, painel de config |
| `Companion/CompanionTalk.cs`, `Core/SpeechConfig.cs` | base de "fala" (pré-LLM) |

**Nosso:** `Companion/CompanionRest.cs` (fogueira/cama), `CompanionPortalFollow.cs`, `CompanionCart.cs`,
e evolução da UI (`CompanionRadialMenu`).
**Aceite:** ele senta no fogo à noite, dorme na cama, atravessa portal com você e puxa a carroça.

---

## Notas transversais

- **Fila de tarefas única** (`CompanionJobQueue`) com prioridade explícita — evita que job/coleta/combate
  disputem o movimento por frame (mesmo cuidado já aplicado na 2.10).
- **Persistência:** cada sistema novo entra no `CompanionState` (enquanto o ZDO estiver adiado).
- **UI:** evoluir para o **menu radial** (`CompanionRadialMenu`) — hoje temos o painel (tecla Y).
- **Atribuição:** ao adaptar código da referência, manter o aviso MIT (`THIRD_PARTY_NOTICES.md`) e citar
  a fonte no commit.
