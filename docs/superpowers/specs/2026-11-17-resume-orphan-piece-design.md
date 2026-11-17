# Fechar (a), (c) e (d) do item 11g — design

**Data:** 2026-11-17 · **Branch de tema:** `fix/resume-orphan-piece` (uma só para o arco inteiro)

## Intenção

A obra de 985 peças fechou uma vez com **985/985 na cena** e outra vez com **uma laje a menos** (a `#692`), e o
`reconcile_scene.py` foi o único a acusar. O que se quer não é "mais um fix de contador": é que **a obra não perca
peça sem repor** e que **o instrumento grite durante o run**, não no fim.

Decisão de produto tomada pelo jogador (17/11/2026): *"a retomada não pode confiar cegamente"* — um subconjunto barato
de verificação, não a varredura da casa inteira.

## Objetivos

- **(a)** Uma retomada (morte ou coleta) não pode marcar como colocada uma peça que sumiu do mundo; ela volta para a
  fila e é reposta.
- **(c)** O sampler abre alarme **durante** o run quando `done − in_scene` sai de zero, com prefab e posição da órfã.
- **(d)** O número de fechamento fica provado igual à cena também num run que termina **curto**.

## Não-objetivos

- Varredura completa da casa na retomada (985 ghost tests). Rejeitada pelo jogador: transforma toda morte numa pausa.
- Mexer no walk, na barreira de andar, no `BuildSpread` ou na lógica de colocação. O defeito é **de confiança no
  estado salvo**, não de construção.
- Reconciliar durante o run com o `reconcile_scene.py` completo (varre yaw, casa peças, ~7 s). O alarme do (c) é uma
  comparação de **contadores** com detalhe só quando dispara.

## Restrições que vieram do código (lidas, não supostas)

- `Standing(state, index)` (`MHR_Build.cs:5673`) **não olha a cena**: só consulta `PlacedIndices`. É o mesmo raciocínio
  circular do defeito — a função de nome promissor não serve para verificar.
- Verificação "de verdade" por peça custa um ghost check (`BuildProbe.CheckPoint`), e o `FindPlan` já evita esse custo
  em lote de propósito.
- **O caminho barato existe e o projeto já usa:** `Piece.GetAllPiecesInRadius` (varredura do jogo, sem ghost test) é o
  que `MHR_Follow`, `MHR_Mover`, `CookController`, `FarmController` e `IdleController` usam para achar peças por raio.
- `RestoreProgress` (`MHR_Build.cs:8547`) é o **único** ponto que devolve o progresso salvo, e é chamado pelos dois
  caminhos de retomada (morte e coleta). É lá que a verificação entra — um ponto, dois caminhos.
- A regra pura vive em `src/Companions/AI/` e é **linkada** no `tools/ai-intents-check/ai-intents-check.csproj` com
  asserções; `BuildProgress` (seção (at)) e `BuildPlanTotal` (seção (aq)) são o modelo a seguir.
- O `run_watch.py` já coleta `done` e `in_scene`; falta a comparação e o alarme.

## Abordagens consideradas

| # | como | por que não |
|---|---|---|
| A | Varredura completa na retomada (ghost test por peça) | Rejeitada pelo jogador: 985 testes logo após um respawn; `FindPlan` já considera caro o mesmo custo em lote |
| B | **Confirmar a cena só onde a queda aconteceu** — varrer `Piece.GetAllPiecesInRadius` num raio do corpo e do ponto da morte, conferir os índices marcados como colocados dentro desse raio, devolver à fila os que não estão lá | **escolhida** |
| C | Reconciliar a obra inteira no fim e repor | Não repõe durante a obra e é lento; resolve o sintoma |

**Escolhida: B.** É o único caminho que usa uma varredura do jogo (barata, sem ghost test) em vez de um teste por
peça, e cobre o caso medido: a laje `#692` sumiu perto do corpo (peça 851, queda de 6,8 m).

## O desenho

### 1. A regra pura (nova, testável sem Unity)

`src/Companions/AI/BuildResumeCheck.cs` — uma função só:

```
Suspects(placed saved, plan poses, center, radius) -> índices a reconferir
```

Recebe o conjunto de índices marcados como colocados, as posições do plano (XZ), um centro e um raio; devolve os
índices **dentro do raio** — os únicos que a retomada vai conferir contra a cena. Puro: sem Unity, sem `Physics`, XZ
como o resto do projeto faz. Fora do raio nada muda, byte a byte.

Uma segunda função decide o veredito com a varredura já feita:

```
Missing(suspects, standing poses) -> índices que voltam para a fila
```

`standing poses` é o resultado de `Piece.GetAllPiecesInRadius` convertido para posições; casa por XZ com a mesma
tolerância que o reconcile usa para "a mesma peça" (0,45 m) — a constante vive numa linha, com o porquê.

### 2. O lado Unity (`RestoreProgress`)

Depois de `BuildProgress.Restore` (que já existe e fica intocado):

1. `BuildResumeCheck.Suspects(...)` com centro = posição do corpo no respawn e raio = `SpreadDistance` × 2 (6 m) —
   o mesmo "duas janelas de corpo" que o spread já usa. Constante nomeada, com o porquê.
2. `Piece.GetAllPiecesInRadius` nesse raio, uma vez.
3. `Missing(...)` → os índices voltam: `PlacedIndices.Remove(i)` e o índice entra na fila do retry (`state.Retry`) com
   um log de uma linha por peça, no formato que o projeto já usa: `retomada: a peça N/M não está de pé - volta para a
   fila`.
4. `PlacedTotal` é recontado depois da remoção (é o relógio da escada de recuperação).

Fail-open: se a varredura não devolver nada (mundo não carregado, erro), **nada é removido** e o comportamento é o de
hoje. Verificar de menos é o risco aceito; verificar de mais (repor peça que está de pé) custaria uma peça duplicada.

### 3. O instrumento (`run_watch.py`) — item (c)

O sampler já grava `done` e `in_scene`. Passa a:

- calcular `gap = done − in_scene` em cada amostra;
- na primeira amostra em que `gap != 0`, imprimir **uma** linha de alarme com `gap`, `done`, `in_scene` e a hora — e
  depois só repetir se o `gap` **piorar** (não uma linha por amostra);
- quando o `gap` disparar, chamar `build.pieces` uma vez e imprimir o prefab e a posição das peças do plano sem par na
  cena (o detalhe só no alarme, para não pagar o custo por amostra).

Um caso de teste sintético no `tools/test_build_run_report.py` ou num teste próprio do `run_watch.py`: um JSONL com
`done=985 in_scene=984` tem de produzir alarme; um com `done=in_scene` não.

### 4. O item (d) — o run curto

Não é código. É o aceite: um run que **termina curto** (ordem interrompida com peça faltando de verdade) tem de
fechar com `planned − placed` igual ao número de faltantes que a reconciliação achar. Roda depois do (a) e do (c),
com a mesma `CommHomeBasicBP`.

## Como cada um será provado

| item | prova |
|---|---|
| regra pura | asserções novas numa seção do harness (linkada no csproj), incluindo os casos: dentro do raio é suspeito, fora não é, nada de pé no raio ⇒ todos voltam, tudo de pé ⇒ nada volta, lista de peças de pé vazia ⇒ **nada removido** (fail-open) |
| (a) em jogo | forçar morte com peça recém-colocada e conferir no log a linha `retomada: a peça N/M não está de pé` e que a peça reaparece na cena; sem morte, o comportamento tem de ser idêntico ao de hoje |
| (c) | caso sintético do alarme + um run real em que o alarme **não** dispara (o normal) |
| (d) | run curto: número de fechamento igual ao da reconciliação |

## Riscos e o que fica de fora

- **Verificar de menos:** uma peça que sumiu longe do corpo não é vista. Aceito conscientemente — é o preço da opção
  barata, e o item (c) passa a ser a rede que acusa.
- **Repor peça que está de pé:** se a varredura falhar em achar uma peça que existe, ela entra na fila e o walk
  encontra "já está de pé" (uma linha de log, sem custo de material). Não é buraco; é ruído.
- **Multiplayer:** a regra é solo-only, como o `RestoreProgress` já é (`pending.Group != null` retorna antes).
- **O (d) pode não fechar** se o run curto for difícil de produzir de forma reprodutível. Nesse caso o item fica
  registrado como dívida com o motivo, não como feito.

## Ordem de execução

1. Regra pura + asserções (harness verde) — nada muda em jogo ainda.
2. Ligar no `RestoreProgress`.
3. Sampler com o alarme + teste sintético.
4. Build com deploy → jogo → run de confirmação (sem morte: comportamento idêntico).
5. Run com morte forçada: a peça órfã é reposta.
6. Run curto para o (d).
7. Documentar no `AGENTS.md`, `docs/roadmap.md`, CHANGELOG e backlog; gate de release; merge na `main`.
