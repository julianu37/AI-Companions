# Plano — o companheiro **nunca mais fica preso** (a IA sai da própria obra)

## Contexto

Medido ao vivo em 12/11/2026, duas vezes, com captura do jogador e leitura pela ponte: **os dois companheiros terminaram
cercados pela própria obra** e não saíram mais. O `Companion` ficou em `(-157,06 · 38,77 · -423,17)` e o `Companion 2`
em `(-151,02 · 38,78 · -417,61)` — **idênticos ao milímetro por mais de 20 minutos**, com HP cheio, `dead: false` e ZDO
saudável. Não é queda, não é morte, não é ZDO: é a IA.

O jogador corrigiu o meu primeiro diagnóstico: **não há teto sobre eles**. O que os cerca é `woodwall` a 0,9 m e
`wood_beam_45` / `wood_pole2` em `y=40,80` a 0,9–2,8 m — **paredes e vigas no chão, sem nada em cima**. Ou seja: um
bolsão aberto para o céu, fechado nos lados, cujo degrau é do tamanho de um corpo. O companheiro não consegue pular
para fora e **o mod não sabe escalar a obra que ele mesmo construiu**.

Resultado pretendido: **nenhuma geometria que o companheiro ajudou a construir pode prendê-lo**. Se ficar sem saída
física, ele sai pela porta que o jogador já tem — mas sozinho, e sem esperar 20 minutos.

## O que já está resolvido neste ciclo (feito e medido)

| defeito | correção | prova |
|---|---|---|
| `companions.teleport` **não movia o corpo** | delega a `CompanionAI.MHR_PlaceAt` (transform + `_body.position` + velocidade zero + `StopMoving` + `ZDO.SetPosition`) e chama `MHR_Mover.Release` | antes: respondeu `{x:-165, z:-435}` e ficou em `-152,97, -416,81`. Depois: `moved: 21,84 m` e **permaneceu** em `-165,06, -435,09` por 9 s |
| `FindCompanion` casava nome por **substring** | nome exato vence; substring é último recurso | `name=Companion` movia o `Companion 2` (o jogador viu: *"só o companion 2 veio teleportado"*) |

**`companions.order stop` está correto** e foi medido: ele devolve `build:false, level:false` quando não há ordem, e
`MHR_Build.Cancel → MHR_Mover.Release` **limpa o `GaveUp`**. O que ele não faz — e não deve fazer — é mover um corpo
sem saída física. Quem move é o teleporte, agora consertado.

## As três portas fechadas (lidas no código, cada uma com a linha)

| onde | a condição | por que falha no bolsão |
|---|---|---|
| [MHR_Mover.cs:2144](src/Companions/AI/MHR_Mover.cs#L2144) `TryStartClimb` | `if (!s.MainPartial …) return false;` | a rota é **completa** (o log diz `route full`), então a escalada **nunca é tentada** |
| [MHR_Mover.cs:2177](src/Companions/AI/MHR_Mover.cs#L2177) `FindClimb` | `if (rise > 1.0f && layer != terrain) break;` | recusa qualquer degrau >1 m de **obra construída** — inclusive a parede que ele ergueu |
| [MHR_Mover.cs:223](src/Companions/AI/MHR_Mover.cs#L223) `GaveUp` | `if (s.GaveUp) { Halt(...); return false; }` | limpa só com `shift > 4f` ou após 15 s; e o `Follow` re-entra no **mesmo** destino |

## O que o log revelou e eu não sabia: o **livelock de desvio**

```
[Move|Follow] couldn't get to (-165,0, 39,5, -399,7) from (-153,1, 38,8, -416,7) - giving up
[Move|Follow] the way is blocked - trying another one at (-152,5, 38,8, -413,8) (0,0m up)
... repete para sempre, com dois alvos alternando
```

`TryDetour` ([MHR_Mover.cs:2825](src/Companions/AI/MHR_Mover.cs#L2825)) acha o desvio `(-152,5 · 38,8 · -413,8)`, que
está **dentro** do cercado; `DetourTries` chega a 3, desiste, `Reset` zera `DetourTries` — e os mesmos 3 desvios voltam.
**Não é só falta de escalada: é um laço que consome 100 % do esforço sem nunca mudar de ideia.**

## Itens, na ordem de ataque

### 1. A escalada para de exigir rota parcial — **feito** (`e265c6d`)
`TryStartClimb` ganha a condição que importa: **a posição não muda há N segundos** (o `no progress` já detecta isso em
`Progressing`/`AnchorTime`) **e** existe um degrau alcançável na direção do alvo. Hoje ela exige `MainPartial`, que é
exatamente o que um bolsão aberto para o céu **não** tem.
**Aceite:** no bolsão de 12/11, com rota completa, a escalada é tentada em até ~8 s; o log mostra o `climbing out at`.
**Estado:** `bool climbed = s.MainPartial || still >= StillTime;` — parado por 2,5 s é sinal suficiente; o build fecha
0/0 e o harness segue verde. O aceite de jogo (a linha `climbing out at`) fica para o run do item 4.

### 2. `FindClimb` aceita a obra que o companheiro pode escalar — **feito**
A recusa atual (`rise > 1.0f && layer != terrain`) existe para não tratar parede de casa como rampa. Ela precisa de uma
**exceção medida**: um degrau de obra **≤ `ClimbRule.BuiltStepMax` (1,6 m)** com **superfície de apoio no topo** (o
`CheckCapsule` que o `TryHopUp` já usa) é escalável. Acima disso continua recusado.
**Aceite:** seção nova no harness para a regra pura de aceitação do degrau (ver item 5).
**Estado:** a regra virou `AI/ClimbRule.cs` (`CanClimb`/`KeepsGoing`) e o laço do `FindClimb` só decide com ela; o apoio
no topo é `StandingRoom` (cápsula de 0,3 m de raio, corpo de 0,5 a 1,6 m), então beirada de telhado e parede alta
continuam recusadas. Seção **(ah)** no harness, 948 PASS / 0 FAIL.

### 3. Fim do livelock: desvio repetido não conta de novo — **feito**
`TryDetour` memoriza os pontos que já tentou por peregrinação (`s.DetourTried`, como `ClimbTried` já faz) e **não
devolve o mesmo ponto**; ao esgotar, marca `GaveUp` com o motivo `no exit` **sem** o `Reset` reabrir as mesmas 3
tentativas em 15 s — o relógio de re-avaliação passa a crescer (15 s → 30 s → 60 s, teto 120 s) enquanto o corpo não
sair do raio.
**Aceite:** o log do run não repete a mesma coordenada de desvio mais de uma vez.
**Estado:** a regra pura é `AI/EscapeRule.cs` (`AlreadyTried` com raio de um corpo, `RetryWait` 15→30→60→120 e
`MovedAway = 6 m` para zerar). O `GoInner` deixou de usar o relógio fixo de 15 s e o `GiveUps` só zera quando o corpo
anda mais de 6 m ou leva `Release`. Seção **(ai)**: 966 PASS / 0 FAIL.

### 4. A saída de emergência para o corpo que não tem saída física — **feito**
Quando os itens 1–3 falharem (não há degrau, o desvio não existe), **depois de N segundos** (proposto 90 s) o mover usa
o que a ponte agora usa: `MHR_PlaceAt` para o último ponto com rota completa (`GoodPos`) — o `Rescue` que já existe
([MHR_Mover.cs:2373](src/Companions/AI/MHR_Mover.cs#L2373)) **sem** a guarda `MainPartial`.
**Regra de produto:** só para o companheiro **preso**, nunca em combate, nunca em água, e sempre **falando** (*"não tenho
saída daqui, vou pular para o último lugar seguro"*).
**Aceite:** no cenário reproduzido (bolsão cercado, sem teto), o companheiro sai **sozinho** em ≤ 120 s.
**Estado:** `EscapeRule.CanRescue` exige 90 s na mesma posição, fora de água e fora de combate; `FindEscapeSpot` usa o
`GoodPos` só quando ele está a ≥ 2 m e, se não houver, sonda anéis de 3 · 5,5 · 8 m atrás de chão seco com rota completa.
O `GoodPos` deixou de ser reescrito a cada frame (`WorthRecording`, 1 m) — era a própria posição do corpo, então o
resgate teleportava para o lugar onde ele já estava. Fala `hc_why_trapped_out`, novo nos 4 JSONs. Seção (ai) cobre a
regra: **987 PASS / 0 FAIL**; `--check-config` 0 lacunas e os 3 idiomas 0.

### 5. Regra pura + harness (a prova antes do jogo) — **feito**
`AI/ClimbRule.cs` — função pura que decide "este degrau é escalável?" dados `rise`, `layer`, `tem apoio no topo`,
`stepMax`. Linkada em `tools/ai-intents-check/ai-intents-check.csproj`, seção `(ah)`.
**Aceite:** harness verde, contagem crescendo. **948 PASS / 0 FAIL** (eram 930).

### 6. Instrumento: a ponte passa a dizer *preso*
`companions.build_status` já lê a obra; falta a **saúde do corpo**. Um campo novo em `companions.list` — `stuckFor`
(segundos desde a última mudança de posição além de 0,5 m) — deixa a próxima sessão medir "preso há X s" pela ponte em
vez de por captura de tela.
**Aceite:** no cenário do item 4, `stuckFor` sobe monotonicamente e **zera** depois da saída.

## Teste em jogo (o mesmo cenário que falhou)

1. `world_enter` (a receita documentada — nunca mouse).
2. Reproduzir o bolsão: `companions.build` do `CommHomeBasicBP` na âncora `(-158,9 · 38,8 · -427,7)` até o piso e as
   paredes do andar de baixo estarem de pé, com os dois companheiros dentro do perímetro.
3. **Aceite:** nenhum companheiro fica com `stuckFor` > 120 s; o log mostra `climbing out at` ou a linha de emergência;
   os dois terminam a obra.
4. Instrumento: `python tools/build_run_report.py --last` + `free_run_acceptance.py`.

## Riscos

| risco | mitigação |
|---|---|
| escalar parede vira "subir em casa do jogador" e estragar a navegação | o degrau máximo é **1,6 m** e exige apoio no topo; parede de casa tem 2 m+ |
| a emergência teleporta em situação legítima (esperando um portão) | só após 90 s **sem sair do raio**, nunca em combate/água, e sempre falando |
| o relógio crescente esconde um problema real | o `why` fica no status e o log nomeia o motivo uma vez por ocorrência |

## Ordem dos commits

```
fix(mover): a escalada nao exige mais rota parcial (a posicao parada e o sinal)
fix(mover): FindClimb aceita a obra escalavel (degrau <= stepMax com apoio no topo) + AI/ClimbRule + secao (ah)
fix(mover): o desvio repetido nao conta de novo - fim do livelock
feat(mover): o preso sem saida fisica volta ao ultimo ponto com rota (Rescue sem MainPartial)
feat(devbridge): companions.list conta stuckFor (preso ha quanto tempo)
docs(re): os dois companheiros cercados pela propria obra, medido e corrigido
```
