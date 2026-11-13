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

**Estado dos 6 itens do plano (12/11/2026, branch `fix/plano-remap`):** os seis estão **implementados**; o que falta é o
**run em jogo** que mede o conjunto. Nada foi medido ainda no cenário do bolsão.

| # | o que mudou | prova fora do jogo |
|---|---|---|
| 1 | `TryStartClimb` aceita rota completa: parado 2,5 s é sinal suficiente | build 0/0; aceite de jogo pendente |
| 2 | `FindClimb` decide por `ClimbRule` (terreno anda até 6 m; obra só até 1,6 m **e** com `StandingRoom` no topo) | seção **(ah)**, 18 casos |
| 3 | `EscapeRule`: desvio já tentado não conta (`AlreadyTried`, 1,5 m); espera 15→30→60→120 s; `MovedAway` 6 m zera | seção **(ai)** |
| 4 | `Rescue` sem `MainPartial`: 90 s parado, fora de água e de combate, com sonda de 3/5,5/8 m; fala `hc_why_trapped_out` | seção **(ai)**; `--check-config` e os 3 idiomas 0 |
| 5 | regra pura + harness | **995 PASS / 0 FAIL** (930 no início do ciclo) |
| 6 | `companions.list` devolve `stuckFor` (mesmo relógio da emergência) | MCP mock **135/0** |

**O que o plano errou e a implementação corrigiu:** o relógio da emergência **não pode** ser o `AnchorTime` — o
`Unstick` o zera na primeira linha e o `Rescue` é chamado de dentro dele, então a emergência **nunca** dispararia. Nasceu
`MovedPos`/`MovedAt` (0,5 m, mantido no `GoInner`, reiniciado no `Reset`), que os itens 4 e 6 compartilham.

**Ainda não medido:** o `Rescue` agora tem **duas portas** — o braço final do `Unstick` e o `GaveUp` com `NoExit`
(só o "não há saída física", nunca o de lava ou fogo, onde mover o corpo é pior que deixá-lo onde está). Um corpo que
fique preso **sem** chegar a nenhuma das duas (por exemplo `Follow` re-entrando no mesmo alvo antes da escada rodar)
continua fora do alcance; é a primeira coisa a olhar se o run mostrar `stuckFor` passando de 120 s.

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

**Fechado 12/11/2026 (run das 21:22-21:45, DLL desta branch):** `desistindo de` = **0** de verdade, `985/985`,
`0 pulada(s)`, `0 UNSUPPORTED`, `0 too_heavy`; 985 linhas `done:` para 985 peças distintas (0 duplicatas). O run
também pagou **1 morte** (peça 900/985, retomada após o respawn — mecanismo do item 5) e **5 quedas** na cumeeira
(item 11, não deste ciclo). Números completos em
[re/building-notes.md §17.37](re/building-notes.md#1737-o-contador-fechou-e-o-piso-começou-na-escada--o-run-de-12112026-com-o-dll-novo-12112026).

### 13. O piso é posto de longe: o andar não começa na ponta da escada — **feito e medido 12/11/2026**
Sugestão do jogador sobre três capturas: *"depois de colocar a escada, ou quando for colocar os pisos do segundo andar,
começar pelo piso na ponta da escada e ir colocando a partir dali, pra eles já irem andando por cima"*, e em seguida
*"isso também seria bom desde o primeiro piso no térreo, pra ajudar a evitar de eles ficarem presos debaixo"*.

**O defeito medido** (o parser já no `HEAD`, `CommHomeBasicBP`, 985 peças): `NearestWithin` semeia **todo** run de
peças no cursor do run anterior. Para parede, viga e telhado isso é o certo — são postos a partir de uma superfície
que já existe. Para laje é o contrário: a laje **é** a superfície, e o companheiro começava o andar por uma peça a
que ele não tinha acesso direto, só dentro do alcance do olho. Primeira peça de piso de cada andar → subida mais
próxima: **4,0 m** no 4º andar e **11,6 m** no 5º.

**A correção:** `AI/BuildFloorStart.cs` (regra pura) escolhe a **ponta da subida** que serve o andar — a `stair`/
`ladder`/`stepladder` de 0,4 a 3,0 m abaixo do nível, a até 6 m de alguma laje do run, a mais alta vencendo — e o
`NearestWithin` semeia o run de lajes ali, **térreo incluído**; o guarda "nunca pior que o arquivo" não se aplica a
esse run (o arquivo nunca começa na escada). Seção **(aj)** no harness, 31 casos.

**Medido com o build de verdade sobre o blueprint real (antes → depois):** y=0 2,8 → **2,0 m**; y=1 2,8 → **1,5 m**;
y=2 2,1 → **1,5 m**; y=4 4,0 → **1,5 m**; y=5 **11,6 → 1,5 m**. O percurso do plano muda de 2.947,9 m para 3.028,8 m
(+2,7%, todo ele dentro dos runs de piso: é o preço de começar na chegada em vez da borda mais próxima). Os andares
sem subida ao alcance (−0,5 e 6,0) ficam **exatamente** como estavam.
**Não medido:** o efeito em jogo (o `stuckFor` do run e o número de viagens). É aceite de jogo, na fila junto com o
item 12.

**Medido em jogo 12/11/2026** (run das 21:22-21:45, `stuckFor` já no DLL): os corpos nunca passaram de **~0,7 s**
parados (limiar de "mesmo lugar" = 0,5 m; o resgate só abre aos 90 s), e o run fechou com **0 `trapped at` e 0
`no way out`**. O piso passou a começar **na chegada** em todo andar cujo trecho do worker contém a subida:
**1,5-2,0 m** (térreo 2,0 m no Companion; 1,5 m nos andares 1, 4 e 5 no Companion 2). **O limite que apareceu:** a
obra é dividida em trechos contíguos (`BuildSpread`), então o worker que começa no *meio* do run de um andar começa
onde o trecho dele começa, não na escada (medido: 16,2 m no andar 1, 7,2 m no 2, e dois andares sem subida no
trecho). A regra decide onde o run começa, não reatribui trechos — alinhar a divisão do `BuildSpread` às chegadas
por andar é candidato a item novo, **só com número**. Números completos em
[re/building-notes.md §17.37](re/building-notes.md#1737-o-contador-fechou-e-o-piso-começou-na-escada--o-run-de-12112026-com-o-dll-novo-12112026).

### 15. O piso da escada sobe **antes** do apoio, e o solo não tem a socorrista do grupo — *feito 12/11/2026, run de confirmação pendente*
Os dois sintomas que o jogador viu nas capturas de 12/11/2026 — *"ele sobe no piso da ponta da escada e fica andando e
caindo ao invés de colocar os pisos à frente"* e *"ele desceu a escada sem terminar de colocar todos os pisos"* — saem
do mesmo lugar, e a hipótese dele (*"ainda não tinha colocado todos os suportes abaixo do piso superior"*) estava certa.

**O mecanismo, medido no run solo de 12/11/2026** (699/985 quando foi cancelado; 29 `no_support`, 15 apoios de
emergência, 12 `no progress`):

- `PullFloorsAbove` (`MHR_Build.cs:4882`, chamado só depois de colocar um `ladder`/`stepladder`, `:7144`) puxa **até
  2 pisos** para logo depois da escada filtrando por distância, altura e lado — e **por nada mais**. O
  `ClassOf` do parser põe `ladder` (classe 1) **antes** das vigas e postes do mesmo nível (classe 2), então o puxão
  mete o piso **à frente do próprio apoio**: `no_support` na primeira tentativa. O log confirma: `the ladder
  wood_stepladder at (-169,4, 42,8, -424,6) has no floor at its top (top 43,9)` logo depois de `2 floor piece(s) over
  the wood_stepladder brought forward`. Aí o `TryFallbackPost` espera a `SupportGrace` (~20 s), planta um `wood_beam_1`
  que **não está no blueprint** — e com o poste a escada ganha o patamar que não tinha. Ele sobe num piso que só existe
  porque a rede de segurança pegou, anda até a borda e cai.
- **No caminho solo a socorrista do grupo não existe:** `PullDirectSupport` (`:5028`) tem
  `state.Group == null → return false`. Sobra para o companheiro sozinho o `PullSupports` (`:4951`), que só procura
  **para a frente** (`i > state.Index`) e portanto nunca acha o apoio que ficou atrás, a fila de retry (8/12/20/25 s)
  e, por último, o poste. É a origem dos 29 `no_support`.
- **Um defeito menor junto:** `DirectSupportIndex` (`:5068`) decidia "já construído" por
  `MHR_BuildGroup.IsDone(state.Group, i)`, que responde **`false` para chave nula** (`MHR_BuildGroup.cs:654`) — no solo,
  peça já de pé continuava candidata a apoio.

**O que o fix faz (12/11/2026):** `LandingFloors.Select` (puro, harness `(ak)`) só deixa subir o piso cujo apoio **já
está de pé**; `PullSupportBehind` (novo, caminho sem grupo) puxa para antes da peça recusada o apoio que está **atrás**
no plano, com o mesmo `Reorder` do `PullSupports` (sem pular índice); `SettledPieces.Out` é a regra única de "peça de
pé ou desistida" para os dois caminhos; e o `PullSupports` passou a usar a janela real do jogo
(`BuildSupportBelow.MaxFlat` = 2,5 m) em vez de 1,5 m. O `ServeBarrier` ganhou instrumentação: a linha
`volta antes de subir` agora diz quantos **pisos do andar de cima** estão pendentes naquele instante — é o número que
decide se a descida precisa de regra própria (hipótese: não precisa; era consequência dos 29 pisos na fila).

Números do run de confirmação em [re/building-notes.md](re/building-notes.md).

### 16. `build_status.elapsed` no run de **um** companheiro reporta o relógio da peça, não o da ordem
O `StateSite` (a obra de um worker só, sem grupo) publica `StartedAt = state.Start`, mas no `State` esse campo é o
relógio **da peça atual**: `EnterStep` o reescreve a cada peça (`MHR_Build.cs:1370`). Medido no run solo de
12/11/2026: `elapsed` ficou **0 s** na peça 85, **2,08 s** na peça 120 — recomeçando a cada peça, quando a obra já
tinha ~15 min. No caminho com grupo o campo é `MHR_BuildGroup.CreatedAt(key)` e está certo; o defeito é só no solo.
**Consequência:** um poller/relatório que leia `elapsed` num run solo (o caso mais comum do jogador) não tem a
duração — foi preciso usar o relógio de fora (hora da linha da ordem). **Correção proposta:** um campo próprio de
início da ordem no `State` (setado em `StartBlueprint`, não em `EnterStep`) e `StateSite` lê esse. Não entrou no ciclo
do piso para não misturar medição com refactor; é pequeno e provável no harness (a conta é pura).


### 17. A bolsa acima do teto **encerrava** a obra (`too_heavy`) — *feito 13/11/2026, run de confirmação pendente*
Medido no run solo de **13/11/2026** (`CommHomeBasicBP`, 985 peças, 1 companheiro, DLL 1.9.6 com o fix do piso). A obra
ia **851/985** (86 %) quando o companheiro morreu por uma queda de 6,8 m (`AdjustPose` troca o Y pelo
`GetGroundHeight` e mirou o chão 6 m abaixo). Ele voltou, o `Reattach` entregou a ordem de volta
(`the build order ... was handed to the companion that just came back`, peça 851), andou até a própria lápide,
pegou os itens — e o peso foi a **470/300**:

```
[Build] Companion: overweight: 470/300
[Build] Companion: order ended: hc_ai_build_too_heavy (encumbered)
```

**O defeito:** `MHR_Build.cs:3688-3695` comparava o peso com `CompanionCarry.Max` e chamava `Finish(...)`. Nada da
casa tinha mudado, a peça continuava lá, e a bolsa é do companheiro — encerrar era o veredito errado. O jogador:
*"mesmo assim, ele deveria continuar construindo depois de tirar um pouco de itens/peso da bag dele"*.

**O fix (o mesmo commit):** `Phase.Unload` + `AI/BuildOverweight.cs` (puro, harness **(al)**, 27 casos). Ordem das
saídas: **baú** perto do sítio (`MHR_Chests.Usable`, 30 m, via `SetPendingDeposit` — o material não se perde, muda de
prateleira, e o `BuildStock.Withdraw` volta a buscá-lo de lá) → **chão** (`ItemDrop.DropItem` com
`m_autoPickup = false`, a mesma mecânica do `CompanionOrders.DropNamed`), e a obra segue na **mesma peça**. O que
**nunca** cai: equipamento, comida e item de missão. O que cai **primeiro**: o que **não** é material de obra (gema,
troféu, resina — a casa não paga com isso). O que cai **por último**: o material da obra (madeira, pedra...), mas
**cai também** quando é o que resta: acima do teto o companheiro fica `Encumbered` e **não anda**, então "guardar a
madeira" não é a opção segura — é a obra que para. O alvo guarda a margem da peça mais pesada do plano (teto de 20 de
folga), senão a ordem descarregaria a cada peça.

**Três defeitos da primeira versão, corrigidos ainda no mesmo ciclo (achados relendo o código, antes do run):**
1. `Keeps` e `Plan` se contradiziam — `Keeps` devolvia "fica" para gema e "pode cair" para madeira, e o `Plan` tratava
   o material como último recurso. No caso medido (470/300) o plano saía **vazio** e o fix era inerte.
2. `Plan` era **tudo ou nada** e limpava a resposta quando o alvo não era alcançável: mesma inércia, com o companheiro
   `Encumbered` para sempre. Agora devolve **tudo o que pode cair** — a bolsa tem de descer abaixo do **teto**, que é o
   que o deixa andar; o alvo (teto − folga) é só a meta boa.
3. Laço `Goto` ↔ `Unload`: desistir devolvia a fase para `Goto`, o portão do peso disparava no frame seguinte e
   reentrava com o relógio zerado — pingue-pongue até o `Timeout` de 60 s encerrar a ordem com o motivo **errado**.
   `State.GivenUpAt` guarda o peso em que a ordem desistiu; só um peso **menor** reabre a fase.

**Aceite do run de confirmação — PASSou (13/11/2026, mesmo dia).** Cenário montado para reproduzir o defeito: o
companheiro recebeu `Silver` ×300 + `Obsidian` ×60 (`companions.give`) e começou a obra a **4530/300**. O log:

```
sobrecarregado: 11 pilha(s), 4300,0 de peso no chão (4530,3 -> 230,3/300,0) - a obra continua
bolsa aliviada: 230,3/300,0 - a obra continua na peça 1/985
```

Uma descida só, na **peça 1**, e nenhum `too_heavy` em todo o run (o contador `too_heavy` do `run_watch` ficou em
**0**). A ordem seguiu de 1/985 até **185/985** e não parou pela bolsa em momento nenhum — as 11 pilhas que caíram
foram prata e obsidiana (não-material), e a madeira/pedra da obra **ficou na bolsa**, que é a ordem pretendida.
O que parou o run em 185 é **outro defeito** (item 20). O aceite de "a obra não termina em `hc_ai_build_too_heavy`"
está cumprido; o de "985/985" depende do item 20.

**Item 20 (novo, medido neste run): o companheiro fica 2 m ABAIXO do sítio e o `route full` o prende lá.**
Com 185/985 colocadas ele foi para `(-163,4, 37,9, -429,0)` — o sítio está em **39,8** — e de lá repetiu
`2,0m below where it needs to be - jumping up onto (...)` (0,9 m de subida) e `stuck (standing still 3s) ... climbing
out`, sempre voltando ao mesmo ponto. Cada peça seguinte vira `no progress ... route full` e depois
`desistindo de <peça> (took too long)`; as puladas subiram de 3 para **12** e `done` congelou em 185. A sonda
`nav.probe` em `(-163,4, 37,9, -429,0)` responde `found: false`: **não há navmesh no ponto** — ele está fora da malha,
sob o piso que a própria obra construiu por cima dele, e o `route full` é o path saindo vazio. O `climbing out` mira
um ponto a 1,1 m e o mover volta para o buraco. É o mesmo tema do item 18 (rota vertical), mas o sintoma é outro:
não é queda, é **confinamento sob o piso**. Correção proposta: quando o companheiro está abaixo do sítio e o
`nav.probe` do ponto atual não acha malha, o mover não deve tentar "subir 0,9 m" (é uma beirada, não um degrau) — deve
teleportar pela escada mais próxima ou recusar o walk spot e pedir outro. Aceite: nenhuma linha `route full` seguida de
`desistindo` no run solo. **Não corrigido — registrado com o número que o matou.**

### 21. A obra "termina" em `hc_ai_blueprint_done` com **490 peças puladas** — *medido 13/11/2026, o item mais grave; correção implementada 13/11/2026, run de confirmação pendente*
O run fechou assim, e **não** é sucesso:

```
espera: 1014,4 s (barreira 834,0 s em 177 retenção(ões) ...) · peças: 495/1029, 490 pulada(s)
order ended: hc_ai_blueprint_done
```

O que ficou de pé a partir das imagens e das contagens (`build.pieces` + o `.blueprint`):

| | plano | feito | falta |
|---|---|---|---|
| `wood_beam` | 147 | 74 | **73** |
| `wood_roof_45` | 107 | 34 | **73** |
| `wood_pole2` | 129 | 67 | **62** |
| `wood_floor` | 173 | 113 | 60 |
| `wood_beam_45` | 86 | 34 | 52 |
| `piece_chest_wood` | 65 | 26 | 39 |
| `wood_wall_roof_top_45` | 35 | 4 | 31 |
| `wood_beam_1` (apoio de emergência) | 2 | **16** | −14 (a mais) |
| **TOTAL** | **985** | **519** | **466** |

**A leitura do jogador estava certa, e a causa não é o `BuildSpread` nem o "andar por andar".** A ordem do plano
**já** sobe por andar (`BuildBlueprintParser.Ordered`: `LevelKey` → classe → altura), e o log confirma: os andares
aparecem em sequência (`38,8 → 39,8 → 40,8 → 41,8 → 42,8 → 43,8 → 44,8 → 45,8 → 46,8`, com poucas voltas). O que
aconteceu é pior e mais simples: **ele parou de subir.** A construção ficou **vertical e estreita** — as cordas de
`wood_beam`/`wood_pole2`/`wood_roof_45` que dão a **largura e a profundidade** no topo quase não foram colocadas,
enquanto as peças do miolo (chão, parede, baú) foram. Daí a forma da imagem: um miolo alto com as beiradas do
telhado penduradas.

**O erro é de veredito, não de ordem.** As puladas por andar, do log:

```
39,8: 44 + 16      40,3: 13      40,8: 11      41,8: 11      42,8: 5
44,8: 4   45,8: 1   46,8: 1   48,8: 1        (38,8: 1, 39,6: 1, 43,x: 2)
motivos: "no progress" 68 · "took too long" 44
```

E a espera dominante é a **barreira de andar**: `834,0 s` em **177 retenções**, contra `180,4 s` de fila e
`12 492 m` de caminhada. Ou seja, numa casa de **7 andares e 985 peças o companheiro passou 14 minutos parado
esperando** e, no fim, o `hc_ai_blueprint_done` foi declarado com **metade da casa em pé**. `Done` conta a peça
resolvida, e **pulada conta como resolvida** — `hc_ai_blueprint_done` com 490 puladas narra "terminou" para uma obra
que falta metade.

O agravante é o **apoio de emergência**: 16 `wood_beam_1` contra 2 do plano. Quando uma peça estrutural era pulada,
o `TryFallbackPost` plantava um poste que **não está no blueprint** — a rede de segurança virou a estrutura.

**A causa raiz, no código: o plano é uma varredura só para a FRENTE e a peça pulada nunca é revisitada.**
`Advance` (`MHR_Build.cs:1206`) avança com `int next = state.Index + 1` e **`State.Index` nunca volta** — não existe
uma segunda passada. `SkipPiece` (`:904`) e `AbandonPiece` (`:951`) fazem `SkippedIndices.Add(index)` e
`MHR_BuildGroup.AddSkipped`, e o **único** caminho que remove um índice de `SkippedIndices` é `PlacedIndices` no
`NoteDone` (`:1917`) — ou seja, sair de "pulada" exige a peça **já ter sido colocada**. Uma peça estrutural que falhou
uma vez (sem apoio, sem caminho até o walk spot) está fora do plano **para sempre**, e o laço de
`while (state.Index + 1 < state.Plan.Count)` chega ao fim e declara `hc_ai_blueprint_done`.

É por isso que a forma final é a da imagem: as `wood_beam`/`wood_pole2`/`wood_roof_45` que dão **largura e
profundidade** no topo foram as que mais falharam cedo (eram as que exigiam apoio ou caminhada longa) e ficaram
puladas; o miolo (chão, parede, baú) foi. E o `wood_beam_1` de emergência (16 contra 2 do plano) tapou o buraco
estrutural com poste que não está no blueprint.

**Correção mínima proposta (não implementada):** antes de fechar a ordem, uma **segunda passada** sobre
`SkippedIndices` — `Index` volta ao **menor índice pulado não colocado**, a peça reentra com o estado limpo
(`StateResetForPiece`), e ela só pode ser pulada de novo **uma vez** (contador por índice, teto 1-2) para não virar
laço. O `hc_ai_blueprint_done` passa a exigir `SkippedIndices` vazio (ou só o que excedeu o teto), com o número no
texto. É a mesma ideia do `ResumeIndex` que a barreira de andar já usa — a diferença é que hoje ele só serve peça
**abaixo**, nunca peça **já abandonada**.

**Correção proposta, em ordem de valor:**
1. **`hc_ai_blueprint_done` só com 0 puladas** (ou com um teto explícito e configurável): com puladas o veredito tem
   de ser "incompleto", com o número, e a ordem tem de **voltar** às puladas antes de fechar — é o que o jogador pediu
   ("tem que construir debaixo pra cima, não um lado depois o outro"): fechar o andar **inteiro**, largura incluída,
   antes de subir.
2. **A barreira de andar não pode custar 834 s de 1014 s.** Ela é `BodyRetryMaxWait` (10 s) por retenção; 177
   retenções significam que o plano sobe antes de o andar de baixo estar completo — a decisão de subir está errada na
   origem, não a espera.
3. **Pular peça estrutural (`wood_beam`, `wood_pole2`, `wood_floor`) não pode ser silencioso**: hoje ela entra na
   mesma fila de uma cadeira, e é ela que segura a largura do telhado.

**Correção implementada (13/11/2026, ciclo do `fix/piso-apoio`):** a peça dada por perdida **volta ao plano** quando a
caminhada chega ao fim, na ordem do plano (índice menor primeiro = de baixo para cima), com as tentativas e o orçamento de
auto-cura (puxão de apoio, poste de emergência, ajuste de pose) **zerados** — o segundo ataque não é cópia do primeiro, é
outro ataque: a casa em volta mudou e os recursos que foram negados voltam a estar disponíveis. A regra pura está em
`src/Companions/AI/BuildRecovery.cs` e o ponto de chamada é `MHR_Build.RecoverSkipped`, no `Advance`, **antes** do gate de
peça e do fim da ordem. **Não é regra deste blueprint:** ela lê só "é blueprint?", o conjunto de puladas e a contagem de
passes — qualquer `.blueprint` que o jogador carregar recebe a mesma segunda chance.

**Quando ela para:** um passe que **não coloca nenhuma peça** é o último (o instrumento é o mesmo do watchdog de inércia).
Um passe que coloca ganha o seguinte, então uma casa com várias peças inalcançáveis as recupera conforme a estrutura fecha,
e uma peça que o mundo realmente não aceita encerra a ordem com o número na linha em vez de segurar o companheiro ali para
sempre. O teto `BuildRecovery.MaxPasses` = 2 fecha o outro lado: cada passe **anexa** as peças ao plano (a caminhada nunca
volta sozinha), então o crescimento do plano é limitado por construção (no pior caso, 3× o tamanho original).

**O que o run de 13/11/2026 mediu e este fix ataca:** as duas lajes do andar 43,8 falharam com `no progress` (o corpo
estava em 39,7, `route partial`, a subida ainda não existia) e o companheiro ficou um andar abaixo sem rota até elas:
3 tentativas cada, `desistindo de wood_floor` duas vezes, e a obra declarada pronta em **983/985**. A casa que terminou em
volta delas é exatamente o que as sustentaria — a leitura do jogador, antes do log: *"aqueles dois ali que faltaram não
iriam cair se ele tivesse colocado, pois estariam ancorados nos das laterais"*.

Aceite: run solo do `CommHomeBasicBP` fecha **985/985** ou termina com **as peças que o mundo recusou** nomeadas na linha
final — nunca mais `blueprint_done` com peça faltando por falta de tentativa.

### 22. A escada sobe **para um andar que ainda não existe** — *medido 13/11/2026, o item do "deb.→ cima, andar inteiro"*
O jogador descreveu o defeito em três frases e as três estão certas, com número:

1. *"o segundo lançe de escada antes da hora"* — **medido no run solo de 13/11/2026** (983/985, `pit-fix.jsonl`): a
   `wood_stepladder` que sai do andar **2.0** foi posta na **peça #470** do run, quando o andar 2.0 tinha **17 de 85**
   peças e **0 de 61** vigas/postes/paredes. Ela desemboca em **4.0**, e o andar 4.0 tinha **0 de 91**. O primeiro
   lanço (índice 187, andar 0.0) sai na peça #184 com **84 de 149** do próprio andar e **0 de 54** de estrutura.
2. *"o segundo piso fica na verdade no terceiro andar"* — é literal: cada lanço sobe **dois** andares de projeto
   (0.0 → 2.0 e 2.0 → 4.0), porque o piso de 1.0 é um **mezanino** e o de 3.0 é a **água-furtada**. Ele só conta como
   "segundo andar" se o andar de baixo inteiro estiver de pé, e nunca está no momento em que a escada sobe.
3. *"tudo precisa subir junto para ter onde colocar os pisos"* — a escada é classe 1 no `BuildBlueprintParser.Ordered`
   e vem **antes** das vigas e postes do mesmo andar (classe 2). Medido: **0 %** da estrutura do próprio andar está de
   pé quando qualquer uma das 4 escadas é posta.

**Por que o `PullFloorsAbove` não salva:** ele puxa o piso do topo para logo depois da escada, mas o piso do
desembarque é classe 0 e vem **antes** das vigas no plano; o `LandingFloors.Select` (fix de 12/11) então recusa o
puxão, porque o apoio do piso ainda **não está de pé** — corretamente. O resultado líquido é o do run: a escada sobe
para um andar vazio, o corpo sobe, não acha piso, desce, e o andar de baixo é terminado **depois** (medido: das 146
peças do andar 0.0, **60** foram postas depois de o run já ter subido; das 85 do andar 2.0, **68**; das 80 do andar
3.0, **as 80**).

**Onde a regra tem de viver:** não é o `Ordered` (a ordem por andar já está certa: 26 trocas de andar no plano, 7
quedas e todas são `wood_gate`, que é último por desenho). É o `Advance`: uma escada só é *entrada* quando o andar
**que ela desemboca** já tem o piso e a estrutura abaixo dele de pé — ou o equivalente em "a escada é a última peça
do andar de baixo", que é a mesma coisa com outro nome. As peças que dependem dela (piso do topo) já estão
protegidas pelo `LandingFloors`.

**Aceite:** num run solo do `CommHomeBasicBP`, **nenhuma** das 4 escadas é posta com o andar de desembarque em 0 %; e
as peças de um andar postas depois de o run subir acima dele caem de **60/68/80/82/128** para **< 5** por andar.

### 23. O companheiro **se cerca** e tem de demolir a própria saída — *pedido do jogador 13/11/2026, medido*
O jogador viu o corpo cair num vão da obra e ficar **cercado** pela própria construção (captura de 13/11/2026), e
**abriu o buraco à mão** para soltá-lo. O pedido, literal: *"ele fica preso assim cercado e ele mesmo demoli a saída,
mas sem causar instabilidade na estrutura acima"*.

**O que o run mediu:** o relógio novo do `EscapeRule.Trapped` (`trappedFor`) **nunca chegou a 90 s** porque o jogador
soltou o corpo antes; a sonda de fuga (`Rescue`, 48 raycasts em 3 anéis) também não achou saída — o vão era fechado
por todos os lados. Ou seja: a emergência por **teleporte** não cobre este caso, e não deve cobrir; o certo é
**abrir caminho**.

**Direção da correção (registrada, não implementada):** quando `trappedFor` passa do limite **e** a sonda não acha
saída, escolher a **peça da própria obra** que fecha o bolsão (a mais próxima do corpo, na altura do peito) e
**demolir** — `WearNTear.Destroy` no objeto, registrando a peça como "a refazer" para o plano a recolocar depois. O
critério de segurança *"sem instabilidade na estrutura acima"* é medi-lo **antes** de demolir: o jogo já responde
isso (`WearNTear`/`Support`), então a regra é "só demole se o que está **acima** continuar apoiado" — a mesma
verificação que o `BuildSupportBelow` já faz para o sentido contrário. O `MHR_CompanionNavMesh.InvalidateAround` (já
usado pelo `build.delete` da ponte) é o que mantém a rota andável depois da demolição.
**Aceite:** corpo em bolsão fechado, sem saída pela sonda → demole **1** peça, sai, e a estrutura acima continua de pé
(`build.pieces` por altura igual antes/depois); a peça demolida volta ao plano e é recolocada.

### 18. `AdjustPose` troca o **Y** do alvo pelo chão — *medido 13/11/2026, não corrigido*
`AdjustPose` (`MHR_Build.cs:7275`) protege o XZ (raio de 1 m para peça estrutural) mas `BuildSite.Find` monta o
candidato com `ZoneSystem.GetGroundHeight` (`AI/BuildSite.cs:146`) — o **Y do plano é jogado fora**. Medido no run de
13/11/2026: um baú do andar de **44,9** virou alvo em **38,8** (`log ajustado: piece_chest_wood de (-161,4, 44,9, ...)
para (-161,4, 38,8, ...) - falls`) e o companheiro caiu os 6,8 m que o mataram, encerrando a obra em 851/985.
**Correção proposta:** `Find` recebe a altura do plano e o ajuste só varre XZ nessa altura (ou, para peça estrutural,
recusa qualquer pose cujo Y difira do plano mais que a tolerância). Provar no harness com a tabela pura do filtro de
candidato. É o próximo item do tema "rota vertical" (com o 11).

### 19. O early check planta poste de emergência **sem** dar chance ao `PullSupportBehind`
Medido em 13/11/2026: **19 dos 20** postes de emergência saíram do early check (`Goto`, `MHR_Build.cs:6253`), que
chama `TryFallbackPost` sem passar pela recusa `no_support` e portanto sem nunca tentar `PullSupports`/
**Correção proposta:** no early check, tentar a puxada (frente e atrás) antes do poste, exatamente como `Advance` faz
na recusa; o poste fica como último recurso. Aceite: `apoio de emergência < 5` no mesmo run solo.



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
3. Regra pura vai para o harness **no mesmo ciclo** (hoje **1112** asserções); quem implementa não afrouxa a asserção.
4. O que foi revertido fica **documentado com o número que o matou** (escopo XZ: ~33 min; `ahead` por frame: 38 vs 78
   peças/min; `CrossesPending`: 67 % do plano).
