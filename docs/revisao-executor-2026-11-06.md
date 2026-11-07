# Revisão do executor (MHR_Build) — 06/11/2026

Revisão independente de `Place`, da barreira de andar e do retry, feita em HEAD `ff9d8ab` (código igual ao de `2b469d6`,
o guarda de 2 tentativas). Objetivo: explicar por que o companheiro fica sob o piso (§17.31 de `building-notes.md`) e
achar o que mais pode regredir. **Nada foi corrigido de comportamento** — esta revisão não pede patch, pede medição.

Legenda: **CONFIRMADO** = lido no código e checado por mim nesta rodada; **REVISOR** = lido pelo revisor, não reconferido;
**HIPÓTESE** = mecanismo plausível sem medição.

## ALTO

### A2 — o `Goto` apaga o destino de "saio para" (CONFIRMADO)
- `Place` (guarda `SelfStep < 2 && OwnBodyOn`) define `WalkTo = outside`, `WalkFor = Index`, `Phase = Goto`.
- O `Goto` só mantém o destino quando `ReachWalk && (SelfWalk || Pos.y > me.y + 1)`. Para peça **à altura dos pés**
  (≤ 1 m acima) e `SelfWalk = false`, cai no `else if` e zera `HasWalkTo` e `WalkFor`.
- Resultado: o companheiro não anda. As duas tentativas são **no-op** e a 3ª coloca a peça com o corpo embaixo.
- Bate com o rastro: as duas linhas `saio para (-158,1, 38,8, -430,0)` às 38 s, posição constante entre elas.
- `SelfAside` não sofre disso porque define `SelfWalk = true`; o guarda do `Place` não define.
- **Conserto candidato (NÃO aplicado):** `state.SelfWalk = true` no ramo do `Place` — mas **só isso não basta** (A3).

### A3 — o destino pode ser a própria coluna da peça (CONFIRMADO no rastro, mecanismo REVISOR)
- Das 11 tentativas medidas no §17.31, **2 são passo de 0 m** (peças 81 e 191: destino = coluna da peça).
- `WalkSpot` varre `sample = 0` (a coluna da peça) e `BuildWalkSpot.Choose` escolhe pela menor distância XZ a `me`,
  sem excluir a superfície onde o companheiro já está.
- **Par obrigatório com A2:** com `SelfWalk = true`, 2 das 11 tentativas continuariam sendo 0 m. O conserto tem de
  **excluir destinos a menos de X m de `me`** (e dentro da pegada da peça) em `BuildWalkSpot.Choose`, com caso no harness.

### A4 — "sem walk spot" é `LogDebug` (CONFIRMADO)
- `LogDebug` não tem gate próprio: a mensagem é montada e só some pelo nível do BepInEx. Numa run normal,
  o fracasso do `WalkSpot` é **silencioso**. (Regra 6 do AGENTS.)
- O ramo do `Place` em que `WalkSpot` é falso não loga nada e cai direto na colocação.

### A5 — `SelfAside` tem uma única chamada e quase nunca dispara (CONFIRMADO)
- `grep SelfAside`: 1 chamada, dentro do ramo `blocked_by_player`. O `Check` ignora o próprio corpo (`ignore`), então
  o caminho "corpo do próprio colocador" nunca chega lá. Na run do §17.31: `está no meu corpo` = **0** linhas.

### M4 — entrada `Blocked` não tem teto: o plano pode crescer sem fim (CONFIRMADO no código)
- `QueueRetry`: `Kind.Blocked` faz `pending.Blocked++` e **retorna** antes de `pending.Tries++` (linha ~1037–1044).
  O teto `Tries > MaxPasses` (linha ~1047) **nunca** é alcançado por corpo.
- `Advance` (retry pass, linha ~2160): `state.Plan.AddRange(again)` a cada passe, e `Passes++` sem limite próprio.
  `MaxPasses` só é lido no teto de `Tries`.
- Consequência: uma peça travada por um corpo que não sai (o jogador parado em cima, por exemplo) é re-anexada ao
  plano a cada passe. Hoje o que a segura é o **watchdog de 60 s** (`StallOut`), não uma regra de retry.
- **Magnitude NÃO medida.** Medir antes de corrigir: run com o jogador parado sobre uma peça e contar `retry pass N`.

## MÉDIO

- **M1 (REVISOR)** — `ServeBarrier`/`MayTake`: a barreira é um relógio de 5 s desde a 1ª recusa; não olha se o corpo
  ainda está sob a peça. Não há relação causal provada entre a barreira e o `fits: false`.
- **M2 (CONFIRMADO, comentário)** — `BuildFailure.cs` diz "10 s" em dois lugares (cabeçalho e `BodyRetryMaxWait`), mas a
  constante é `5f` e o harness assere 5. Código e teste concordam; o texto não. Corrigir em comentário, num commit próprio.
- **M3 (REVISOR)** — `ServeBarrier`, ramo `!BodyWaitsFor`: libera o andar em silêncio, sem log, com a peça de baixo ainda
  na fila.
- **M5 (HIPÓTESE)** — `InsertBenchStep` (`Insert`), `PullSupports` (`RemoveAt`/`InsertRange`) e `PullFloorsAbove`
  (`Clear`/`AddRange`) mexem no plano no caminho solo. Os conjuntos por índice (`PlacedIndices`, `SkippedIndices`,
  `BarrierServed`, `FallAdjusted`, `PulledSupport`, `FallbackPosts`, `StayedIndices`) podem não ser remapeados. Não verificado.
- **M6 (REVISOR, CONFIRMÁVEL)** — `StallOut` (solo): `index < 0 → continue` sem log e sem contar. A peça sai da fila e do
  denominador sem linha. Viola a regra 6.

## BAIXO

- **B1** `AbandonPiece`: quando `TryQueueRetry` aceita, sai sem log (a peça vai para a fila do site em silêncio).
- **B2** `RetryAt` é um relógio único no solo: uma dependência de 25 s segura a barreira de outra que venceria em 2 s.
  A correção do grupo (`ReadySoonest`) não existe no solo.
- **B3** O ramo `SelfWalk` de `SelfAside` não incrementa `SelfStep` nem `BodyTries`. Limitado por "no progress" de 12 s.
- **B4** `EnterStep` não zera `LastY` nem `NextJumpAt`. Hipótese; não achei o caminho que deixaria `ResumeIndex` sujo.
- **B5** `DriveCore`: `GetComponent` por frame por companheiro em obra; `LogDebug` com concatenação por frame
  (`PendingBelowIndex` no `WaitRetry`). Custo pequeno, mas é alocação no caminho quente.
- **B6** Comentários com "10 s" no `ServeBarrier`, no `BuildFailure` e no `Program.cs` do harness (~linha 855). Ver M2.

## Não é bug

- O guarda de 2 tentativas é desenho intencional (`2b469d6`). O bug é o no-op (A2/A3), não a existência do guarda.
- `RequeueSameStorey` não existe mais. O risco de M4 é análogo, não igual.
- `Advance` com `IsDone` → `continue` silencioso é correto: o site já tem a peça.
- `BodyBarrierSeconds = 5` está pinado no harness. A escolha 5 s × 10 s segue decisão do jogador.

## Não verificado

- Build e jogo: não rodados nesta revisão (o revisor não tinha shell; eu rodei só o gate sem deploy).
- Internos de `BuildProbe.BlockerAt`/`CheckPoint`, `Verify`, `ClearSite`, `WaitGroup`, `TryScaffold`, `PullSupports` e
  as alocações de `MHR_BuildGroup.NextRetry` não foram lidos por inteiro.
- O elo "no-op de 38 s → entrada de 70 s" continua **hipótese**. A2 explica o 38 s e torna a colocação por cima
  inevitável; não explica por que o companheiro andou para o vão 32 s depois.

## Próximo passo (ordem sugerida, cada item com medição)

1. **Teste puro antes de qualquer patch:** caso no harness para `BuildWalkSpot.Choose` com `me` = coluna da peça (deve
   não escolher o próprio ponto quando há alternativa). Hoje isso não está coberto.
2. **A/B de comportamento (1 worker, madzobuild, `DeckLift 0`, canteiro zerado), com UMA variável por rodada:**
   - (a) A2 + A3 juntos (é um só conserto); compara com a linha de base do §17.31 (183/612, `fits:false`, 79 % parado);
   - (b) só depois, M4 (teto de passes para `Blocked`), se a medição do item de M4 mostrar crescimento.
3. **Logs antes de medir:** trocar `LogDebug` do "sem walk spot" por `Log` (A4) — mudança de log, não de comportamento.
4. **Comentários** (M2/B6) em commit próprio.

Cada item acima exige deploy → restart (regra 13) e a decisão de rodar fica com o jogador.
