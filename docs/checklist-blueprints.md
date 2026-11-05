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

## Fila (em ordem de valor)

### 1. Fechar o aceite em jogo — 1 pulada pendente
- [ ] deploy do `9308123` (4º caminho do nivelamento) + restart (regras 12/13).
- [ ] rodar os **3 grandes**: `CommHomeBasicBP` (985), `madzobuild` (657), `villa` (201) — os maiores que cabem no
      canteiro (footprint ≤ 32 m, ≤ 1900 peças, 0 puladas na leitura). **Nunca medidos até o fim.**
- [ ] rodar os **7 planos** na mesma DLL e transcrever **colocadas / puladas / `cpu µs/frame` / `place` / duração**.
- [ ] **critério:** `puladas == 0` nos 7, `0 not_found`, `0 UNSUPPORTED`, `0 mortes`, **nenhum companheiro preso**
      (conferir `build.pieces` e a ausência de corpo preso no log).
      *A última pulada conhecida: `wood_beam` em y 42,8 no `longhouse` — corrigida em `1a9fe5c`, ainda não medida.*

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
- [ ] **vão do deck (~1 m)**: prende corpo. Decisão do jogador entre `DeckLift = 0`, **saia em volta** ou regra de rota.
- [ ] **cumeeira** (`wood_roof_45` a 48,8, `no progress`): navegação; `TempScaffold` vetada.

### 5. Release (só depois de 1 e 2)
- [ ] merge na `main` (`fix/pisos-nivel-2` → `main`), versão, `CHANGELOG` + `LEIA-ME` + release notes (EN).
- [ ] gate completo: `powershell -ExecutionPolicy Bypass -File tools\release-check.ps1 -Version <v>` (sem `-SkipGame`).
- [ ] staging (`tools/stage_release.py`) e zip; **Nexus é do jogador**.

## Instrumento (o que cada número quer dizer)

| campo do `Finish` | o que é | cuidado |
|---|---|---|
| `peças: P/N, S pulada(s)` | placar do **canteiro** (índices) | é a **régua**; `P + S` tem de fechar com `N` |
| `no alcance` | peças colocadas **sem andar** (1 por índice) | antes contava entradas e inflava |
| `corpo` / `dependência` / `fila` / `barreira` | segundos de espera **por worker** | somar os 5 é tempo-homem, não relógio |
| `cpu µs/frame` | custo do `Drive` por worker | declarar o fps medido (o jogo não roda a 60) |
| `caminhada` | distância **reta** na pegada | não é metro andado |
| `puladas` | peça dada por perdida | **o número que decide** |
