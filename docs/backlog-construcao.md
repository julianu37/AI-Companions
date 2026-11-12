# Backlog da construção (ordem de execução)

> Síntese das **cinco revisões de 04/11/2026** (lógica/invariantes, performance, rede/ZDO, domínio do jogo,
> idiomas/config/docs) mais as medições dos Runs 18–22. **Ordem = ordem de ataque**, não ordem de importância.
> Escopo: **single-player**; multiplayer é o **último item** e só se o jogador decidir investir (ver `AGENTS.md`,
> "ESCOPO DO PROJETO").

## O que já está medido (ponto de partida)

| indicador | valor | onde |
|---|---|---|
| melhor run (985 peças, 5 companheiros, receita de **teste**) | **12 min 07 s**, 985/985, 0 puladas, 0 mortes, 0 `UNSUPPORTED`, watchdog 0, `FALTANDO` vazio | §17.24 |
| baseline anterior (mesma casa) | 14 min 04 s | §17.22 |
| espera total / `fila` (o parque) | **414,1 s** / **4,5 s** (era 1292,8 / 1040,2) | §17.24 |
| barreira de andar | **402,7 s em 99 retenções** (era 244,9/73) — *[I]* absorveu a fila | §17.24 |
| `corpo na peça` / `sem lugar livre` | 34 peças distintas em 36 linhas (**rotação zerada** pelo `Height` 1,8) / 4,4 por 100 peças | Runs 20–21 |
| harness / config / MCP / gate | **718** / **368/25** / 122 / **9/9** | — |

## Fila de trabalho

### 1. O run da **receita de produto** (o que decide a frase "está pronta")
`BlueprintFree=false`, `PlaceReach=10`, `LevelGround=true`, `DeckLift=1`, material **pago** de mochila + baús,
inventário real (peso), jogador andando. É o único cenário que ainda **não** foi jogado e é onde vivem as pendências
(7), (11) e (12).
**Aceite:** 985/985 (ou falta **nomeada**), 1 pulada 0, 0 mortes, 0 `UNSUPPORTED`, watchdog 0, `FALTANDO` vazio,
duração ≤ ~800 s, **sem** `missing material` em loop e **sem** `too_heavy` encerrando a ordem.
**Risco previsto:** com 25–32 stacks por companheiro o `MHR_Build.cs:3564-3567` encerra a ordem daquele worker; a
reserva de material é a metade aberta do P1 (`MHR_BuildGroup.cs:27-30`).

### 2. ~~`too_steep` no nivelamento **mata a obra** (pendência 12)~~ — **fechado 11/11/2026**
`hc_ai_build_too_steep` (`Jobs/MHR_Level.cs:280`) não estava na lista de retomada (`:455`, só `done`/`already`/
`failed`), então `LevelGround` (padrão **true**) encerrava a ordem em terreno íngreme em vez de devolver ao build.

**O que foi feito:** a lista virou a tabela pura `AI/LevelResume.cs` (`Resumes`/`Message`), `hc_ai_build_too_steep`
entrou nela com **linha própria** ("the ground is too steep to level - back to the build with what it has") e a seção
**(af)** do harness a prende (14 asserções, incluindo o invariante `Resumes(x)` ⇔ `Message(x) != null`).
`hc_ai_build_no_hoe` continua fora **de propósito**: sem enxada o terreno nunca é tocado, e a obra que voltasse pediria
o mesmo nivelamento e encerraria do mesmo jeito. A guarda de 90 s (`s_levelTried` / `NoteLeveled`) já existia e impede
o nivelamento imediato na volta.
**Provado em jogo (11/11/2026)**, terreno com variação **2,48 m** em 20 m (sítio `-180,-320`, `terrain.check size=20`),
`LevelGround=true`, `CommHomeBasicBP.blueprint`, 2 companheiros com enxada — `BepInEx\LogOutput.log`:

```
[Build] blueprint footprint 32,1 m (leveling 20,0 m), ground variation 2,48 m (tolerance 0,35) - leveling needed
[Build] the ground is uneven (2,48 m in 20,0 m) - leveling first
[Level] too steep (2,48 m)
[Level] order ended: hc_ai_build_too_steep (too steep)
[Level] the ground is too steep to level - back to the build with what it has (2 companheiro(s))
[Build] clearing the site: 9 tree(s)/log(s) inside the 32,1 m footprint
```

A ordem **não** morre: o nivelamento recusa, a obra volta sozinha e segue para a limpeza do sítio. Na segunda passagem da
mesma ordem a guarda de 90 s aparece como a linha `the ground is still uneven after leveling (2,48 m) - building anyway
(the pieces that do not fit are skipped)` — sem um segundo `leveling first`, que é o laço infinito de 02/11/2026
impedido de voltar.

### 3. `step-aside` no andar sendo telado (a dívida do `Clearance`)
`BuildAsideSpot.Clearance = 1,5 m` (`AI/BuildAsideSpot.cs:39`) é um **círculo em XZ** contra peças pendentes: num
andar em obra **todo** ponto a 2,2 m está a <1,5 m de uma peça pendente → o anel cai → `sem lugar livre` → peça na
fila + barreira de 10 s. Medido: **42 de 52** casos de corpo não moviam ninguém; o andar de cima foi o gargalo de
um run inteiro (17 min 40 s).
**Tentativa revertida (com o número):** o teste exato cápsula × caixa girada (`CrossesPending`) recusou **67 % do
plano** (424 `sem lugar` em 638 peças) porque `BuildBody.Blocks` não tem tolerância e o span vertical pega piso
**acima** do candidato — revertido em `e594f7e`.
**Antes de codar:** a triagem que falta (item 4). Ela diz se é `Blocked` (regra), `StepTo` (não tem chão no nível) ou
`SafeStep` (não tem rota). **Se for `StepTo`, não existe ajuste de anel** e a resposta é aceitar a fila.

### 4. Instrumento: a triagem do `sem lugar livre` e do `no progress` (log, sem run)
- `AskStepAside` registra **por que** falhou: `Blocked` (1,5 m de pendente) × `StepTo` (chão/level) × `SafeStep`.
- `SkipPiece` com `no_support` registra o `DirectSupportIndex` e o estado dele (`done` / na fila com `At` / claimado /
  nada no raio) — separa "efeito de ordem" de "buraco real" (poste).
- `no progress` agrupado por **pose** (`prefab@x,y,z`) + ΔY do walk spot + rota `partial|full` — hoje são 10–20
  linhas que não se sabe se são a cumeeira (pendência 10) ou lugar novo.
- Poste: `apoio do plano: nenhum|reivindicado|deferido N|desistido` na linha do `apoio de emergência` — 26 postes sem
  proveniência não distinguem apoio legítimo de andaime.

### 5. Custo por frame **medido** (pendência 15)
Não existe cronômetro. Proposta: `Stopwatch` no `MHR_Build.Drive` (µs/frame por worker), contador em
`MHR_BuildGroup.Ready` (avaliações de fila/frame) e `GC.GetTotalMemory` no `Finish` (bytes por ordem). A revisão
estimou **~4,2 ms/frame** nos 5 (≈25 % de 16,6 ms) e **10–12 MB/s de GC** com a obra travada; o `ClassOf` memoizado
já tirou a alocação, mas a **varredura** de `IsBelow`/`ServeBarrier` a 60 Hz continua.
**Aceite:** `cpu: X µs/frame` na linha do `Finish` e um run com `BodyAt` de pico registrado.

### 6. A cumeeira (pendência 10) — navegação
`no progress` recorrente em `wood_roof_45` a **48,8 m** (Runs 13/15/20). O walk spot já foi corrigido (anéis 1,5/3 m,
`WalkHits` 3) e ainda falha. `TempScaffold` está **vetada** pelo jogador — a resposta é rota, não andaime.
**Antes:** a triagem do item 4 (pose + walk spot + rota) para não repetir o Run 14 (walk spot 7 m abaixo).

### 7. O companheiro fica **cercado pela própria obra** e não sai — *[M] 12/11/2026, os dois de uma vez*
`DeckLift=1` deixa ~1 m entre o piso e o chão e prende corpos (41 peças de piso foram apagadas à mão uma vez).
Opções de produto: **A** `DeckLift=0`; **B** saia em volta fechando o perímetro; **C** regra de rota para o mover não
entrar/parar sob o deck. Mas o run de 12/11 mostrou que o problema é **maior** que o deck e não depende dessas opções:
o corpo fica **cercado por paredes e vigas** (sem teto) e o mod não sabe escalar a própria obra. **Isto é item de
código**, e é o mais grave medido até agora.

**Medido ao vivo em 12/11/2026** (segunda run grátis, captura do jogador + ponte): **os dois companheiros acabaram em
bolsões cercados pela própria obra** e não saíram mais. O `Companion` ficou em `(-157,06 · 38,77 · -423,18)` e o
`Companion 2` em `(-151,02 · 38,78 · -417,61)` — posições **idênticas ao milímetro** por mais de 20 minutos, com
`hp 25`, `dead false`, ZDO saudável, e o `placed` subindo de 420 a 824 enquanto um deles trabalhava.

**O diagnóstico correto (o primeiro estava errado):** não é o vão do `DeckLift` — o `Companion 2` **não tem piso
nenhum acima dele**. A medição do raio curto mostra o que o cerca: `woodwall` a 0,9 m e `wood_beam_45`/`wood_pole2`
em `y=40,80` a 0,9–2,8 m, ou seja **vigas e paredes em volta, no chão, sem teto** — o corpo não consegue pular para
fora e o mod não sabe escalar a própria obra.

**As duas condições que fecham a única saída, lidas no código:**

| onde | a condição | por que falha aqui |
|---|---|---|
| `MHR_Mover.cs:2144` `TryStartClimb` | `if (!s.MainPartial ...) return false;` | a rota que ele acha é **completa** (o log diz `route full`), então a subida **nunca é tentada** |
| `MHR_Mover.cs:2177` `FindClimb` | `if (rise > 1.0f && layer != terrain) break;` | mesmo se tentasse, recusa qualquer degrau >1 m que seja obra construída — **recusa a própria parede a escalar** |
| `MHR_Mover.cs:223` `GaveUp` | `if (s.GaveUp) { Halt(...); return false; }` | depois de desistir ele para; só limpa com `shift > 4f`, e o alvo do `Follow` pode ser ele mesmo (`crosshair is on it`) → trava circular |

**O padrão:** ele **sabe** que está preso (a escada de `Unstick` roda, `no progress` estoura, ele fala *"I can't get up
there"*), **tenta**, e as saídas desenhadas exigem exatamente o que a situação nega (rota parcial; degrau de terreno).

**Intervenções que NÃO funcionaram** (medidas): `companions.teleport` para terreno aberto a 20 m (a IA reescreve a
posição no frame seguinte), `companions.order follow`, `companions.order stop`, e **destruir o piso sobre eles**
(`build.delete` — continuaram parados, só 1 m deslocados pelo empurrão da destruição). Isso prova que não é colisão
com o teto: é a IA insistindo num destino e nunca desistindo de desistir.

**Direção da correção:** `TryStartClimb` deveria disparar também com rota completa quando a posição **não muda há N
segundos** (o `no progress` já detecta isso); e `FindClimb` precisa de uma exceção medida para obra **construída pelo
próprio companheiro** (o `Piece`/`WearNTear` é dele, e uma cerca de 1–2 m é escalável como uma parede de pedra). Sem
essa exceção, qualquer casa que ele construa em volta de si é uma armadilha.

**Plano de execução:** [`docs/plan-companion-nunca-preso.md`](plan-companion-nunca-preso.md) — 6 itens, cada um com
aceite, incluindo o livelock de desvio medido no log (o `TryDetour` devolve o **mesmo** ponto dentro do cercado, o
`Reset` reabre as 3 tentativas a cada 15 s, e nada muda de ideia).

**Duas ferramentas consertadas neste ciclo (medidas, não teorizadas):**

| defeito | correção | prova |
|---|---|---|
| `companions.teleport` **não movia o corpo** — atribuía só `cs.transform.position`, e o `Rigidbody` reescrevia no frame seguinte | delega a `CompanionAI.MHR_PlaceAt` (transform + `_body.position` + velocidade zero + `StopMoving` + `ZDO.SetPosition`) e chama `MHR_Mover.Release` (limpa `GaveUp` e `LastGo`) | antes: respondeu `{x:-165, z:-435}` e o corpo ficou em `-152,97 · -416,81`. Depois: `moved: 21,84 m` e **permaneceu** em `-165,06 · -435,09` |
| `FindCompanion` casava nome por **substring** — `"Companion"` resolvia para `"Companion 2"` | nome **exato** vence; substring é o último recurso | o jogador viu: *"só o companion 2 veio teleportado"*; depois da correção `Companion` moveu 27,13 m e `Companion 2` 1,37 m, os dois a ~2 m do jogador |

**`companions.order stop` está correto** — medido: responde `build:false, level:false` quando não há ordem, e
`MHR_Build.Cancel → MHR_Mover.Release` **limpa o `GaveUp`** (`MHR_Mover.cs:143-151`). O que ele não faz, e não deve
fazer, é mover um corpo sem saída física; quem move é o teleporte, agora consertado.

### 8. ~~As duas frases erradas do `CHANGELOG` público (revisão de lógica, A8)~~ — **fechado 11/11/2026**
`package/CHANGELOG.md:16` dizia que os blueprints ficam em `config/…/blueprints` (o código usa `<plugin>/blueprints`,
`AI/BuildBlueprints.cs:54/70-74`) e `:35` dizia que a ordem é "por classe de peça … em vez de por altura" (o parser
ordena **por altura primeiro**, `AI/BuildBlueprintParser.cs:146`). As duas foram reescritas: a pasta é
`BepInEx/plugins/AI-Companions/blueprints/` (com a migração da antiga citada) e a ordem está descrita como
**andar a andar**, com a altura como primeira chave, a classe depois e as portas de toda a obra por último.

### 9. A data dos documentos
`AGENTS.md` e `docs/re/building-notes.md` datam os runs de hoje como **11/11/2026**; o `git log` desta máquina diz
**04/11/2026**. Uma das duas está errada em ~200 ocorrências — passe dedicado, não no meio de um ciclo de código.

### 10. Reserva de material por peça (limitação declarada do P1)
Cada worker conta a mesma pilha por conta própria (pior caso: N× o resto do plano, com até 3 coletando juntos). Só
aparece no run **pago** (item 1). O desenho é o `Stock` da metade aberta do P1 em `docs/plan-multi-build.md`.

### 11. O companheiro **cai do telhado em construção** e morre (novo 11/11/2026, medido)
No run interrompido do sítio `-180,-320` (terreno íngreme, deck a y=40) o log registra **quatro quedas** e **duas
mortes**, ambas no mesmo ponto do plano (781 e 782 de 985):

```
[Move] "Companion 2" fell 6,0 m and took 12 fall damage
[Move] "Companion 2" fell 7,3 m and took 21 fall damage     <- morte 1
[Move] "Companion 2" fell 8,5 m and took 28 fall damage     <- morte 2
[Move] "Companion 2" fell 6,8 m and took 17 fall damage
[Build] Companion 2: it died during the build - the order resumes after the respawn (piece 782/985)
```

**O que se sabe [M]:** a morte é tratada (a ordem retoma na peça, o túmulo é criado com os 11 itens e o respawn é
agendado); o que **não** existe é a prevenção. O `MHR_Mover` anda até o walk spot do telhado e a queda acontece na
subida — as peças em volta estão a y=40 e o chão a y≈34, então qualquer passo errado é uma queda de 6–8 m, que é
morte certa com 25 de vida.

**Hipótese, não medida:** o walk spot (`BuildWalkSpot`) escolhe a superfície mais alta coberta pela regra de olho, mas
nada verifica se o **caminho até ela** é andável — é o mesmo `StepTo`/`SafeStep` do item 3, um andar acima. Se for
isso, a resposta não é ajuste de anel: é recusar a peça enquanto não houver rota segura, e o item 4 (triagem) é o
pré-requisito para saber qual dos dois é.

**Por que importa para a receita de produto (item 1):** "0 mortes" está no aceite. Num terreno plano o deck fica
raso e a queda é curta; no íngreme o deck sobe e a mesma peça vira armadilha. **Não** é um defeito que se vê no run
de teste (lá o `DeckLift` é absorvido pelo terreno plano).

### 12. ~~O run **grátis** da `CommHomeBasicBP`~~ — **medido 12/11/2026**
O ciclo `fix/plano-remap` fechou os oito defeitos de código do `MHR_Build` (remapeamento do plano, "já está de pé"
antes de pagar, abandono contado, watchdog × barreira, `LastY`/walk spot, escada sob o corpo, números do fim do grupo)
mais duas refatorações — **1.586 linhas mexidas, todas medidas**. O run que os prova **rodou em 12/11/2026** e está em
[re/free-run-11-11-2026.md](re/free-run-11-11-2026.md): a colocação **não perde nem duplica** (`placed (` == `done:`
idênticos do começo ao fim, 0 `falls`, 0 mortes, 0 `UNSUPPORTED`), e o único aceite que não fechou foi **contador**.

**O defeito que o run pagou (37 peças):** a barreira de andar, ao estourar a paciência, chamava `SkipPiece` na peça
que `PendingBelowIndex` acabara de ler **da fila do canteiro** — uma peça que o canteiro ainda possuía (e que, nas 37
vezes, foi conferida de pé na cena: `sumidas de fato: 0`). A cauda `N pulada(s)` chegou a **andar para trás** (4 → 3)
porque `MarkDone` a tirava do conjunto, e o maior `N pulada(s)` do run foi **18** enquanto as linhas somavam **37**.
Correção no mesmo ciclo: o ramo de estouro só escreve `LogDebug`; peça que a fila ainda tem é **adiada**, nunca
contada (a regra que o `AbandonPiece` já escrevia), e quem conta o que realmente cai é o watchdog.
**Falta medir:** o run com o contador corrigido (`desistindo de` = 0 de verdade) — só o número, a colocação já está
provada (`985/985`, `0 mortes`, `0 UNSUPPORTED`, `0 too_heavy`, `5 quedas` na cumeeira = item 11).


## Declarado e **fora** do caminho crítico

- **Multiplayer** (pendências 5, 16, 17): troca de posse do corpo a cada 2 s para a obra em silêncio e trava o
  `SiteBusy`; pagamento por baú sem `SetInUse`. **Último item de todos**, só se o jogador investir.
- **Corpus baixado** (pendência 14): 279 de 354 blueprints perdem linha por `recipe not unlocked` e o mod **pula em
  silêncio** — falta a **tela e a fala** dizendo qual receita falta. É decisão de produto, o dado já existe
  (`BuildBlueprintReport.SkippedReasons`).
- **Criatura como bloqueador**: a sonda de corpo não testa criaturas (um javali em cima da peça passa).

## Regras que valem para qualquer item desta fila

1. **Uma mudança por build**, com o corte de aceite escrito **antes** do run.
2. **Reverter na hora** se `puladas > 0`, `desistindo > 0`, morte, `UNSUPPORTED` ou watchdog — o baseline fecha
   985/985 com 0, então qualquer um deles é regressão.
3. Regra pura vai para o harness **no mesmo ciclo** (hoje **718** asserções); quem implementa não afrouxa a asserção.
4. O que foi revertido fica **documentado com o número que o matou** (escopo XZ: ~33 min; `ahead` por frame: 38 vs 78
   peças/min; `CrossesPending`: 67 % do plano).
