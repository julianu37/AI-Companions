# Checklist — os companheiros construindo blueprints

> **O que é este arquivo:** o "onde eu parei" do esforço de fazer o companheiro construir `.blueprint` sozinho.
> O `AGENTS.md` é a referência (arquitetura, regras, tudo); **este** é a fila de trabalho, com o critério de aceite e
> o comando exato de cada passo. Atualizar **no mesmo commit** em que o passo muda de estado.
>
> **Aceite do esforço (o que fecha):** na **receita de produto** (`BlueprintFree = false`, `[Building] PlaceReach = 10`,
> `LevelGround = true`, peso real, material nos baús) o `CommHomeBasicBP` (985 peças) **sem pulada, sem morte, sem
> `UNSUPPORTED`, sem companheiro preso** e **abaixo de 300 s**.

## Regras que não se negociam (quebrar qualquer uma invalida o run)

- [ ] **um número = uma DLL**: nada de deploy no meio da medição (regra 13); matar a medição antes, ou esperar.
- [ ] **deploy → restart**: a DLL só vale depois do boot; a prova é `DLL.LastWriteTime` **anterior** ao `StartTime`.
- [ ] entrar no mundo pela **regra 12**: `world_enter` (Espaço em rajadas → **seta uma vez** → 3× Enter a **0,5 s**),
      jogo lançado **desanexado** (`Start-Process valheim.exe -console`).
- [ ] **nunca** editar o `BepInEx\config\AiCompanions.cfg` do jogador.
- [ ] `[Building] TempScaffold = false` (vetado pelo jogador); nunca teleportar o jogador; nunca mover o jogador.
- [ ] a contagem vem da **linha do `Finish`** (`peças: P/N, S pulada(s)`), **nunca** de linhas `done:`.
- [ ] o artefato do run vira arquivo em **`docs/runs/`** no mesmo ciclo (o `LogOutput.log` é apagado a cada boot).

## Receita de teste (a bancada) — hoje

```
5 companheiros · canteiro vazio entre os planos · âncora (-160, 38.78, -428.8)
[Building] PlaceReach 15 · DeckLift 1 · BlueprintFree true · LevelGround false
BlueprintStations true · SpreadWorkers/SpreadNear/BodyRetry/StepAside/ReachWalk true
mochilas zeradas: companions.inventory action=clear
```

**Receita de produto (o gate de verdade, nunca rodada):** `PlaceReach 10`, `BlueprintFree false`, `LevelGround true`,
peso real, material nos baús do canteiro.

## Feito e medido (não repetir)

- [x] **geometria do plano é rígida** — `BuildPieceKind.IsTip`: só ponta enterrada assenta sozinha; piso/parede/telhado
      nunca se movem isolados. `stone_cottage`: **24/64 → 64/64** (`61be6be`, §17.27).
- [x] **o lote das revisões R11–R20** (`881632c`): `PlanLocked` segue o canteiro; `GaveUp` limpo na fronteira da peça;
      `Vertical` com guarda; `not_found` de peça nossa destruída → `no support`; `too_steep`/`no_hoe`/sem peça de nível e
      o 4º caminho do nivelamento → **constrói assim mesmo**; `MoveFromChests` não destrói material; `AddKnownStation`
      ensina a receita; `BuildSite.Find` não joga a peça do 2º andar no chão; `status` prioriza
      bloqueio/estação/apoio; dreno da fila só no **último** worker; **índice 0 validado**; `CheckPending` sem stat por
      frame; `Reattach` sem alocação por frame; `try/catch` nos patches da navmesh.
- [x] **instrumentação honesta** (`aaa25a9` + `1069cfe`): `no alcance` por **índice**; `posições` = posição do **corpo**;
      o relatório não escolhe mais a variante com mais puladas; `companions.inventory action=clear` alcançável;
      `companions.order stop` responde `cancelled` de verdade.
- [x] **custo por peça**: `FindPlaced` usa `Piece.GetAllPiecesInRadius` (sem alocar por peça) — `cf1e1a8`.
- [x] **`DependencyCap = 3`**: a peça que nunca recebe apoio para de ser servida em ~40 s em vez de ~200 s.
- [x] **viga que pende do telhado** (`1a9fe5c`): `Ordered` a constrói junto do telhado (igloo/longhouse); o tour de
      vizinho mais próximo usa a **mesma** classe (`Cls`). Harness **778**.
- [x] **companheiro volta a ser obstáculo** (`4ebce87`): sem ele a peça nasce dentro da cápsula e o corpo **fica preso**
      (`UpdateMotion` anula a depenetração, 2 m/s). Custo medido do bloqueio: **6,2 s** num plano de 148 s.
- [x] **7 planos numa DLL só** (`c345f8c`): **846 peças em 188 s** = 0,22 s/peça, `corpo 0,0 s`, 0 `not_found`,
      0 `UNSUPPORTED`, 0 mortes. Baseline do mesmo dia: 861 s. Artefato em `docs/runs/`.
- [x] **a varredura dos 82** (05/11/2026, a mesma DLL, canteiro zerado por plano): **82/82** fecham
      `placed + skipped == plan`, **8410 peças em 5730 s (0,68 s/peça)**, **0 mortes / 0 `UNSUPPORTED` / 0 travados**.
      Artefato: `docs/runs/2026-11-05-corpus-82-planos.json`. **A pulada se concentra em 3 famílias** — e é isso que
      muda a fila: `stone_manor` 5 de 6 planos (**15** peças), `longhouse` 5/6 (7), `stone_house` 4/6 (6),
      `stilt_house` 4/6 (5), `stilt_cottage` 2/6 (3), `stone_cottage` 1/6 (3), `hut` 1/7 (2), `stilt_hut` 1/6 (2),
      `great_hall` 1/6 (1) = **24 planos / 44 peças**; `villa`, `manor`, `house`, `cottage`, `igloo` e os dois grandes
      fecham **100 %** (o `CommHomeBasicBP` **985/985 em 470 s**, ainda **170 s** acima da meta dos 5 min).
- [x] **`IsTip` segurou nas 6 variantes de `stone_cottage`** (5 fecham 64/64; só a de meadows pula 3) — era o plano
      que parava em 24/64 (§17.27). O log mostra a regra agindo em **todos** os 82 (`N peça(s) enterradas mantidas na
      pose do plano`, `MHR_Build.cs:3032`): o assentamento por peça não quebra mais o plano rígido.
- [x] **a rodada de UM companheiro** (05/11/2026, **abortada de propósito em 40/82** para não medir na DLL velha):
      nos **40 comparáveis**, **1 ganha 16 / 5 ganha 18 / 6 empates**, soma **1697 s × 1896 s (−10 %)** e puladas
      **7 × 14**. **Empate técnico em tempo**, metade das puladas com 1. **Não** virar "1 é mais rápido":
      `hs_meadows_house` deu **36 s** avulso e **96–105 s** na varredura (mesma DLL) — a variação de sessão come 2,7×.
      O ganho real do 1 é nos planos de **deck/estaca** (−80 a −124 s), onde ele anda em ordem estrita e
      **`espera 0,0`**; com 5 aparece `sem apoio ainda` e poste de emergência. Dado: `%TEMP%\opencode\blueprint-all-1w.json`.
- [x] **o vão do deck, medido** (05/11/2026): terreno **38,78** / piso **39,78** = vão **1,00 m**; cápsula **1,80 m**;
      Companheiro 3 medido em **y 37,90** (**0,88 m dentro do terreno**) e só saiu quando o piso foi **demolido**.
      Separação que estava faltando: `4ebce87` = peça nascer **em cima** do corpo; **entrar** no corredor é outro
      defeito, e é o que a **regra de teto** resolve.

## Tentado e DESFEITO — não repetir sem um número novo

> Cada linha aqui foi **implementada, medida e revertida**. O motivo está escrito com o número que a matou; repetir
> qualquer uma sem um run que a contradiga é **regressão**.

| tentativa | por que parecia boa | por que foi desfeita (o número) | onde está hoje |
|---|---|---|---|
| **ignorar o corpo do companheiro** na sonda | `corpo 0,0 s` e 861 → 188 s | **piso nasce em cima do companheiro e ele fica preso** (print do jogador, 05/11): o `UpdateMotion` reescreve `linearVelocity` a cada fixed step e a depenetração (2 m/s) perde. O ganho **não** era do corpo (era o `DependencyCap`) | **laço restaurado** (`4ebce87`); custo do bloqueio **6,2 s** |
| **`IgnoreCollision` corpo × peça** | o corpo atravessaria a peça | ele **cai do piso que acabou de subir** (a obra usa o telhado como andaime); é estado do PhysX local e morre ao recriar collider | rejeitada na onda 1, **não implementar** |
| **empurrar o corpo depois de colocar** | "o jogo resolve" | a física **já tenta** e o resultado medido foi o companheiro **1 m abaixo do chão**; empurrar por `transform` é teleporte disfarçado | rejeitada (onda 1) |
| **teleporte de resgate** | tirar o preso na hora | o jogo o **esmaga de novo** no tick seguinte; e o resgate existente (`MHR_PlaceAt`) **não dispara no mesmo nível** | rejeitada (onda 1) |
| **barrar só no pick da peça** (O2) | mais barato que barrar no place | a **janela entre pick e place** reabre exatamente o preso | **proibida** (§17.30) |
| **`ahead` por frame** na espera (Run 19) | reagir rápido | **dobrou** o tempo: **38 peças/min contra 78** e `corpo na peça` **130** em 347 peças (5 corpos disputando o mesmo canto) | **1 Hz** (`AdvanceAskSeconds = 1f`) |
| **barreira de andar com escopo XZ de 6 m** | localizar a espera | **~33 min** (2,4× a base) e `dependência` **206** | revertida (`8ec9de7`) |
| **filtro de destino do step-aside (`CrossesPending`)** | não pisar em peça pendente | **67 % do plano recusado** | revertida (`8ec9de7`) |
| **`Release`/`PlanLocked` = "2+ workers"** | reordenar o plano com 1 worker | com o canteiro em **1** worker a reordenação desalinha `Claims`/`Done`/`Retry` (tudo é por **índice**) | `PlanLocked = Group != null` |
| **clamp por peça do deck** (`0f13878`) | assentar a fundação no chão | o piso ficava **no nível do chão** (o jogador queria deck) **e** quebrava o plano rígido (vão de 1 m → peça destruída) | **lift rígido** + `IsTip` (§17.23/§17.27) |
| **`DeckLift` como deslocamento forçado** | subir a obra sempre | os planos rasos do corpus **flutuavam 1,00 m** e caíam | virou **teto** (`clamp(buried, 0, wanted)`) |
| **sonda por "disco"** (`max(size.x, size.z)/2`) | simples | **falso positivo**: um feixe de 2 m virava disco de 1 m e "prendia" o companheiro a 1,08 m (Run 10) | **cápsula real × caixa girada** |
| **`HasLanding` no build** (pular escada sem pouso) | não subir para o vazio | a casa **ficava sem escada** | removido; quem recusa o vazio é o **mover** (`LadderLanding`) |
| **nivelar andando até cada ponto** (enxada) | "como um jogador" | o mover **desistia e ele tremia** no lugar | opera do **centro** (animação + 0,45 s) |
| **grade denso no nivelamento** (1,5 m) | mais preciso | **divergia**: 0,55 → 2,01 m; o espaçado converge 1,01 → 0,39 | grade **espaçado pelo raio** (3 m) |
| **`rise`/`FillLow` no nivelamento** | levantar o terreno | "não compõe" e foi **vetado pelo jogador** ("esquece o rise") | código morto, `Fill` sempre `false` |
| **escada provisória (`TempScaffold`)** | alcançar a cumeeira | o canteiro **encheu de escadas** | **vetada pelo jogador**, default `false` |
| **`RetryReady`** | filtrar a fila | **100 % morto** (`NextRetry == -1 ⟹ false`, sempre) | removido (`c0826b4`) |
| **`ClassOf` sem memoização** | código simples | **~3.000 strings/frame** (170–210 KB ≈ 10–12 MB/s de GC) | memoizado (`Dictionary` `Ordinal`) |
| **executor pela rota `Player.PlacePiece`** | "mais parecido com o jogador" | dá **crédito/barulho/estatística ao jogador** e não devolve o objeto | `Instantiate` próprio (`PlaceDirect`) |
| **contar `done:` para medir** | simples | a janela do log **se mistura entre ordens** (`112/108`!) | **linha do `Finish`** |
| **dock do `no alcance` por entradas** | — | dava **296 %** de peças | 1 por **índice** (`StayedIndices`) |
| **relatório escolhendo a variante com mais puladas** | "a linha mais completa" | no empate pegava **985/985/0 × 984/985/1** e reportava 1 pulada a mais | desempate por `placed` e `-skipped` |
| **dreno da fila em TODA saída de worker** (meu bug de hoje) | fechar a conta | marcava como puladas as peças **dos outros** e desfazia o `AbandonPiece` | só o **último** drena (`!IsShared`) |
| **login por clique** e depois por `SendKeys`/`PostMessage` | "sem tocar no jogo" | coordenada erra quando o cinemático muda a tela; `MainWindowHandle` é o **console do BepInEx**; o Unity 6 ignora `WM_KEYDOWN` | **regra 12**: teclas **pelo bridge** |
| **login com 3 tentativas fixas** | orçamento claro | o bridge responde **no boot** (3 s), as 3 tentativas acabavam **no cinemático** e o login falhava | **rodadas até entrar**, prazo de parede |
| **repetir a seta em cada rodada do login** | "idempotente" | no menu cada `Down` **desce a seleção** → o Enter abria a tela errada | seta **uma vez** |

**Coisas que continuam de pé mas NÃO se sustentam no uso** (não são regressão; são instrumento morto):
`dispersão` (`SpreadWorkers`) sai **`0 adiada(s)` em todos os runs** desde que entrou — o caminho existe e tem 22 asserções,
mas **nunca é exercitado em jogo**; e as **24 asserções da repulsão** não existem (o `BuildSpread` promete estar pinado).



## Fila (em ordem de valor)

### 1. Fechar o aceite em jogo — a última pulada conhecida **continua**
- [ ] deploy do `9308123` (4º caminho do nivelamento) + restart (regras 12/13).
- [x] rodar os **3 grandes** — feito na varredura dos 82: `CommHomeBasicBP` **985/985 em 470 s** (35 postes),
      `madzobuild` **612/612 em 121 s** (47 postes), `villa` **178/178 em 36–56 s**.
- [x] rodar os **7 planos** na mesma DLL: **846 peças em 188 s**, 0 puladas (arte em `docs/runs/`).
- [ ] **critério:** `puladas == 0`, `0 not_found`, `0 UNSUPPORTED`, `0 mortes`, **nenhum companheiro preso**
      (conferir `build.pieces` e a ausência de corpo preso no log).
      *A última pulada conhecida: `wood_beam` em y **42,8** no `longhouse` — o `1a9fe5c` mudou a **ordem** (a viga sobe
      junto do telhado) e **não** eliminou o caso: na varredura dos 82 ela voltou **11×** (`desistindo de wood_beam
      (verification: no support)`, y **42,8 / 44,8**), e é a família `longhouse` que concentra 5 de 6 planos com
      pulada. **É o alvo nº 1.***

### 2. A receita de produto (o gate real da release)
- [ ] `PlaceReach 10`, `BlueprintFree false`, `LevelGround true`, peso real, material nos baús, jogador **andando**.
- [ ] medir `CommHomeBasicBP`: **< 300 s**, 985/985, 0 puladas, 0 mortes.
- [ ] se o peso derrubar a obra: `hc_ai_build_too_heavy` encerra **o worker** — a saída é a mochila/baús (pendência 11).

### 3. Planejamento do "ignorar o corpo sem prender" — só teoria até aqui
- [ ] **onda 1/5 feita** (10 de 50 rodadas): 9 defeitos na sonda; **não há cura depois de colocar**.
- [ ] **onda 2**: geometria e margem — começa resolvendo se os **+0,3 m** do jogo são do **collider físico** ou só do
      **`m_bounds`** (R1 × R5 se contradizem; a resposta decide se a faixa de 15 cm existe).
- [ ] ondas 3 (arbitragem), 4 (desempenho/medição), 5 (plano de implementação).
- [ ] ao fim: decidir **O0/O1/O2/O3** (ver `docs/re/building-notes.md` §17.30) — **O2 é proibida** (a janela entre pick e
      place reabre o preso).

### 4. Pendências sem causa
- [ ] **morte no hobbit** (peça 47/62, logo depois de um `wood_wall_roof_45`, **sem linha de queda**) — apareceu no
      `seq9` e não no `seq8`. Abrir o log da morte, decidir se é criatura, fall ou squeeze.
- [ ] **vão do deck (~1 m)** — **medido em jogo (05/11/2026)**: terreno 38,78, piso 39,78, corpo em **y 37,90**
      (0,88 m dentro do terreno); só saiu quando o piso foi **demolido**. Cápsula 1,80 m × vão 1,00 m.
      **Decisão do jogador: regra de teto/clearance no mover** (`4ebce87` NÃO resolve — ele impede a peça nascer em
      cima, não o corpo entrar). O mover não tem teste de teto (`MHR_Mover.cs`: `DropAhead` `:2802` olha para baixo).
      Desenho em `docs/re/deck-clearance.md`.
- [ ] **cumeeira** (`wood_roof_45` a 48,8, `no progress`): navegação; `TempScaffold` vetada.
- [ ] **⚠️ ordens fecham com o denominador INFLADO** (05/11/2026, rodada de 1 worker + confirmado no código):
      `hs_*_stone_cottage` **64/76** e `hobbit_repairhut` **62/67** — mas **não falta peça**: `64 + 12 duplicatas = 76`
      e as 12 subiram pelo retry. Causa: `Advance:1996` faz `Plan.AddRange(retry)` sem filtrar o já colocado (só no
      caminho **sem grupo**), `AbandonPiece` grava o **índice cru** (`:1137`/`:1150`) → "1 pulada" fantasma, e o
      `Finish` só imprime.
      **Critério de aceite corrigido (eu tinha errado):** **`placed` contra o tamanho BASE do plano** (o `64` da linha
      de abertura), **não** `placed + skipped == plan` — esse reprova plano fisicamente perfeito e travaria ordens
      (o run fez retry em **5 de 40** planos).

### 5. Release (só depois de 1 e 2)
- [ ] merge na `main` (`fix/pisos-nivel-2` → `main`), versão, `CHANGELOG` + `LEIA-ME` + release notes (EN).
- [ ] gate completo: `powershell -ExecutionPolicy Bypass -File tools\release-check.ps1 -Version <v>` (sem `-SkipGame`).
- [ ] staging (`tools/stage_release.py`) e zip; **Nexus é do jogador**.

## Instrumento (o que cada número quer dizer)

| campo do `Finish` | o que é | cuidado |
|---|---|---|
| `peças: P/N, S pulada(s)` | placar do **canteiro** (índices) | a régua é **`P` contra o `N` da ABERTURA da ordem** — o `N` do `Finish` foi inflado pelo retry até o lote de 05/11/2026 (`Plan.AddRange` sem filtro) |
| `no alcance` | peças colocadas **sem andar** (1 por índice) | antes contava entradas e inflava |
| `corpo` / `dependência` / `fila` / `barreira` | segundos de espera **por worker** | somar os 5 é tempo-homem, não relógio |
| `cpu µs/frame` | custo do `Drive` por worker | declarar o fps medido (o jogo não roda a 60) |
| `caminhada` | distância **reta** na pegada | não é metro andado |
| `puladas` | peça dada por perdida | **o número que decide** |

**Três escritores de `SkippedIndices`, um silencioso** (medido na varredura dos 82): `MHR_Build.cs:1113` loga
`desistindo de <prefab> (<why>)` (`:1116`), `:4978` loga `sem apoio: <prefab> em (...)` (`:4985`, só com `LogNormal`) e
**`AbandonPiece` (`:1150-1151`) conta a pulada sem logar**. Saíram **70** linhas nomeadas (**59** `sem apoio:` + **11**
`desistindo`) para **44** peças: **a linha é por worker e a peça é por plano**, então elas **não formam contagem**.
Quem quiser pulada por peça: **nomear no `AbandonPiece` primeiro** — hoje o número fecha e a peça fica anônima.
