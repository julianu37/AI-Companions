# Auditoria comparativa — ModIA × Offline Companions Reborn (MIT)

Data: 2026-10-13 · Base do ModIA: v0.3.1 · Referência:
[Offline Companions Reborn](https://github.com/MaddhattrRocks/Offline-Companions-Reborn) (MIT).

Escopo: **(A) coleta** e **(B) IA de combate/movimento**.

## Resumo

Nosso mod está mais **organizado e defensivo** em vários pontos (varredura com buffer pré-alocado e
camada configurável, cache por prefab, estimativa de rendimento por cascata de toras,
`TryResolveResource` com apelidos PT-BR, patches Harmony mínimos/observadores). A referência cobre
**três famílias que não temos**: (1) **coleta de drops de criatura** (couro/pena/osso/hide), (2)
**pesca**, (3) uma **camada de movimento própria** (`MHR_Mover`) que abre portas e evita
precipícios/fogo. No combate, a referência **bloqueia/aparar** e **escolhe escudo/arma por alvo**.

## Achados priorizados

| # | Área | Prio | Esf | Correção |
|---|------|------|-----|----------|
| 1 | Coleta: encerramento precoce | **Crítico** | P | Esperar os drops caírem antes de contar varredura vazia; só parar após timeout pós-destruição |
| 2 | Movimento: precipício | **Crítico** | M | Checar `DropAhead`/`LedgeAhead` antes de `MoveTo` (coleta e follow) |
| 3 | Coleta: rocha "rachada" | Alto | M | Seguir a cópia `cracked` no mesmo ponto (`Destructible.m_spawnWhenDamaged`) |
| 4 | Coleta: drops de criatura | Alto | M–G | Patch em `Character.OnDeath` + estado de loot (buscar o que o próprio abate dropou) |
| 5 | Movimento: portas | Alto | M | Abrir/fechar `Door` no caminho |
| 6 | Combate: bloqueio/parry | Alto | M | `Character.SetBlocking` quando inimigo próximo ataca; revidar após stagger |
| 7 | Coleta: peso/inventário cheio | Alto | G | Limite de peso + depósito em baú; não encerrar a tarefa ao falhar `Pickup` |
| 8 | Coleta: ferramenta de tier alto | Alto | P | Default incluir `PickaxeIron` (ou fornecer conforme o tier do nó) |
| 9 | Coleta: estado de drops | Médio | M | Estado `CollectingDrops` com `DropScanDelay` (~0,5 s) |
| 10 | Coleta: auto-pickup geral | Médio | M | Puxar itens `m_autoPickup` próximos (com checagem de peso) |
| 11 | Coleta: tool keeper | Médio | G | Buscar/emprestar ferramenta em baú e devolver |
| 12 | Combate: escudo por alvo | Médio | M | Escudo de parry × escudo de block |
| 13 | Água: não nadar por alvo/loot | Médio | M | `AvoidChase` para alvo em água funda |
| 14 | Combate: filtro de alvo | Médio | P | Excluir passivos (`AnimalAI`, `m_avoidLand`, `m_passiveAggresive`) |
| 15 | Coleta: limite/estoque por item | Médio | M | Listas `Nome:Qtd` contando baús |
| 16 | Coleta: tora mais próxima | Médio | P | Ao destruir `TreeBase`, mirar a `TreeLog` mais próxima |
| 17 | Coleta: água/construção/limites | Baixo | P | Raio de proteção de base configurável (hoje 2,5 m fixo) |
| 18 | Robustez/estilo | Baixo | P | `[NetworkCompatibility]`; evitar `GetAllItems()` por varredura |

## Detalhes dos itens críticos/altos

### #1 — Encerramento precoce da coleta
Ao derrubar uma árvore, toras/drops levam alguns frames para spawnar. Nossa varredura imediata pode
não achar nada e **encerrar a tarefa em ~1,5 s**. Referência: `HarvestController.UpdateCollectingDrops`
espera `_dropScanDelayTimer = 0.5 s` antes de seguir.

### #2 — Movimento sem proteção contra queda
`ApproachAndWork` chama `MoveTo` direto. Referência: `MHR_Mover.LedgeAhead` antes de cada passo;
`MHR_GatherRules.ApproachPoint` escolhe o lado a montante.

### #3 — Rocha "rachada"
`Destructible.m_spawnWhenDamaged` troca o objeto por uma cópia "cracked" no 1º golpe; o nó original
some e re-varremos. Referência: `HarvestController.TryFollowCrackedRock` reencontra a cópia no mesmo
ponto (raio 2,5 m). **Sintoma: "1 golpe por pedra, anda a esmo".**

### #4 — Drops de criatura (ausente)
Referência: `MHR_Loot` + patch `Character.OnDeath`; vai ao corpo e cata só o que aquele abate gerou
(respeitando peso/distância). Hoje o companheiro mata um cervo e **deixa o couro no chão**.

### #5 — Portas
Sem tratamento de `Door`; seguindo o jogador para dentro de casa, **trava na porta**. Referência:
`DoorAhead` + `Door.Interact` (e não abre portas com `m_keyItem` nem em `PrivateArea` sem acesso).

### #6 — Bloqueio/parry
Referência: `CompanionCombatAI.UpdateBlocking` bloqueia quando inimigo a <8 m está `InAttack()`.
Como compilamos contra a assembly **publicized**, dá para chamar `Character.SetBlocking` direto.

### #7 — Peso/inventário cheio
Hoje a coleta **termina** ao encher. Referência: `MHR_GatherRules.IsOverweight/HandleOverweight/DropOff`
leva a carga ao baú mais próximo e retoma.

### #8 — Ferramenta de tier insuficiente
`PickaxeAntler` é tier 1 → não cava prata/obsidiana (tier 2). Incluir `PickaxeIron` no default
resolve o caso comum com baixo risco.

## O que NÃO vale copiar agora

- **`MHR_Mover` completo** (~2.900 linhas): copiar só `LedgeAhead`/`DropAhead`, `EscapeFire` e portas.
- **`FishController`** (pesca completa), **`FarmController`/`MHR_SeedRules`**, **`CookController`**,
  **`SmeltController`**, **`RepairController`**, **`UpkeepController`**, **`CompanionRest`**,
  navio/vagonete/portal: sistemas independentes, fora do escopo atual.
- **`MHR_ToolKeeper` completo**: só depois do sistema de depósito (#7).
- **Stances + `WeaponLogic.BalancedPrefix`**, **`TorchOpener`**, **`MHR_Crossbow`**: recortes pequenos,
  só se aparecer problema concreto.

## Riscos/regressões a vigiar

1. **Coleta × combate:** replicar o padrão "combate vence" ao adicionar loot/portas/depósito.
2. **Um "dono" do movimento por frame:** definir prioridade única (hoje `UpdateHarvest` compete com `base.UpdateAI`).
3. **Trashing de arma** (harvest × combate) pode gerar loop de equip.
4. **Patch em `Character.OnDeath`** roda para todo personagem → Prefix barato, `try/catch`, checar `IsOwner()`.
5. **Multiplayer:** estado da coleta é estático/global; cliente sem autoridade vê "coleta ativa" sem efeito.
6. **Performance:** há `GetAllItems()` por varredura; reutilizar buffers ao adicionar loot/depósito.
7. **Blacklist/limites** (`NodeGiveUpSeconds=7`, `BlacklistSeconds=90`) mais agressivos que a referência.
8. **Não quebrar a cascata `TreeBase → TreeLog`** (é o que faz árvore ganhar de galho).
9. **Proteção de base** (2,5 m fixo) → tornar configurável se aumentar.
10. **`CountParts`** conta triggers como seções; revisar se o rendimento de rocha ficar estranho.
