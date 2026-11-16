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

**[M] 15/11/2026 — a queda medida de novo, e o que ela custa de verdade.** Run solo do `CommHomeBasicBP` no sítio
`-158,9, -427,7`: **duas** quedas, `6,5 m / 16 de dano` (peça 820, `log ajustado: wood_beam_45 de (…, 44,8, …) para
(…, 38,6, …) - falls`) e `6,9 m / 18 de dano` (peça 854, a que matou). Com **25** de vida máxima, 16 de dano já
deixa o corpo em 9 — a segunda queda **não precisou ser grande para matar**. As duas aconteceram no **terceiro
andar** (y=44,8 → y=38,6, o rés-do-chão), e o `log ajustado` da primeira mostra o mecanismo: o `AdjustFallenPose`
procurou outra pose depois da queda 1, moveu a peça para o **chão** (38,6) e o companheiro andou até lá — o custo
da queda não é só o dano, é a peça do andar de cima colocada no térreo.

A consequência que o jogador viu não é a queda em si: foi o **estado do plano**. A ordem retomou no respawn
(`back from the dead - resuming the build at piece 854`) e recomeçou a caminhar da **peça 1**, imprimindo
`a peça N/985 já está de pé` mais de 120 vezes, porque `Pending` levava o plano, o índice e a fila de retry mas
**não** os conjuntos de progresso (`PlacedIndices`/`SkippedIndices`). Isso está corrigido neste ciclo
(`AI/BuildProgress.cs` + `MHR_Build.RestoreProgress`); **a prevenção da queda continua aberta**, e agora tem um
segundo dado a favor do diagnóstico acima: a segunda queda é igual à primeira, na mesma borda do terceiro andar.

**[M] 16/11/2026 — o run seguinte: a queda de novo (3ª vez, mesma borda) e a prova de que a retomada está certa.**
Run solo do mesmo `CommHomeBasicBP`, mesmo sítio, DLL reconstruída com `BuildProgress`/`RestoreProgress`. **Cinco
quedas, uma morte**, e a que matou é a **mesma peça-e-borda** das anteriores:

```
[Build] Companion: next: piece_chest_wood (851/985) a 3,5 m
[Build] Companion: log ajustado: piece_chest_wood de (-156,6, 44,9, -421,6) para (-156,6, 38,8, -421,6) - falls
[Move]  "Companion" fell 6,7 m and took 17 fall damage
[Build] Companion: it died during the build - the order resumes after the respawn (piece 851/985)
```

O `log ajustado … 44,9 → 38,8` é o mecanismo do item 11 visto de novo: o andar de cima está a **y=44,9** e o
rés-do-chão a **38,8**, então a borda aberta do andar em construção é uma queda de **6,1 m** — com 25 de vida,
17 de dano não mata de primeira, mas mata na segunda. **A prevenção continua aberta; o item 11 tem agora três
medições independentes, todas na borda do andar mais alto, todas com o mesmo `log ajustado` arrastando a peça
para o térreo.**

**O que este run fechou é o outro lado do item:** a retomada depois da morte **não recaminha mais a obra**. No
respawn o log vai direto ao trabalho:

```
[Recovery] Collected 10 item(s) from its tombstone, 0 left
[Build] the build order of "..." was handed to the companion that just came back
[Build] Companion: back from the dead - resuming the build at piece 851
[Build] Companion: placed (Direct) em (-156,6, 44,9, -421,6), verifying next frame
```

**Zero linhas `já está de pé` no run inteiro** (contra **271** no run de 22:33 e **120+** no anterior). A morte
custou a peça 851 até ser recolocada, não as 850 que já estavam de pé.

**O fechamento do run (a reconciliação da cena, passo 10 do ciclo):**

| métrica | alvo | medido |
|---|---|---|
| peças na cena / plano | 985/985 | **985 / 985** |
| reconciliação (yaw sem semente 344°, tol 0,45 m) | ≥ 983 casadas | **982 casadas**, resíduo máx. 349 mm |
| peças fora do lugar | 0 | **1** — a `wood_beam_45` que a queda arrastou (plano y=45,0, cena y=38,8, mesmo x/z) |
| `desistindo de` / `puladas` | 0 / 0 | **0 / 0** |
| `no progress towards` | < 5 | **2** |
| `no_support` | — | 3 (nenhum vira `pulada`) |
| `UNSUPPORTED` | 0 | **0** |
| resgates (teleporte) | ≤ 3 | **0** |
| degraus temporários | ≤ 12 | **0** |
| `order ended` | `hc_ai_blueprint_done` | **`hc_ai_blueprint_done`** |
| quedas / mortes | 0 / 0 | **5 / 1** (item 11) |
| linhas/peça | — | 3,03 (contra ~7,3 no run que o jogador reprovou) |

O run **reprova no critério de quedas** — e a única peça fora do lugar é exatamente a que a queda arrastou. Os
outros onze itens do aceite fecham. É a primeira vez que a reconciliação e o relatório concordam sobre o mesmo
defeito: a queda não é só dano, é uma peça a menos no andar de cima.

**Segundo defeito, no instrumento:** a primeira execução de `reconcile_scene.py` deu **exit 0** com essa peça
faltando, porque (a) `explain_missing` comparava a coordenada **local** do plano com a **mundial** da cena — 449 a
464 m de "distância" — e (b) gravava `best` em vez do `near` que acabara de calcular, então `is None` no `main()`
nunca era verdade e **toda** faltante virava "explicada". Corrigido (comparação no espaço do mundo, usando o mesmo
transform do casamento) e coberto por `tools/test_reconcile_scene.py` (5 casos, um deles a peça 6,2 m abaixo).
**O exit 0 que ele deu às 00:12 não era evidência.**

### 11b. ~~A causa raiz do item 11~~ — **achada e fechada 16/11/2026**

O item 11 estava aberto havia cinco dias com três medições independentes e a mesma frase: "o companheiro vai até o
walk spot do andar de cima e cai". **A causa não era o walk spot.** É uma checagem que olhava **dois de três eixos**:

```csharp
// MHR_Build.AdjustPose, antes de 16/11/2026
float dx = site.Pos.x - state.Pos.x;
float dz = site.Pos.z - state.Pos.z;
if (dx * dx + dz * dz > radius * radius) return false;   // o teto estrutural é HORIZONTAL
```

O `radius` ali é o raio estrutural (1 m para parede/telhado): uma tampa **horizontal**. Não havia nada checando o
**y**. Quando o companheiro caiu do andar de cima (44,9) para o térreo (38,8) e pediu uma pose nova, o
`BuildSite.Find` respondeu com **a mesma posição x e z, um andar abaixo** — a peça estava livre ali, e o teste
horizontal passou **por zero metro**. O log de 16/11/2026 é o mecanismo inteiro numa linha:

```
log ajustado: piece_chest_wood de (-156,6, 44,9, -421,6) para (-156,6, 38,8, -421,6) - falls
```

Mesmo x, mesmo z, **6,1 m abaixo**. Essa linha é os **dois** defeitos ao mesmo tempo:

1. o baú do andar de cima era **colocado no térreo** — é a peça que a reconciliação da cena encontra fora do lugar
   em *toda* run (1 peça, sempre a mesma assinatura: mesmo x/z, y menor);
2. o companheiro, mandado **andar até ela**, caía os mesmos 6,1 m e morria.

**A correção** é a tabela pura `AI/BuildAdjustPose.cs`: `MayMove(posY, planY, dx, dz, radius)` — o ajuste move a
peça **de lado, nunca de andar** (tolerância de 1 cm, o arredondamento do próprio parser). Ligada em
`AdjustPose`, e o teste `(au)` do harness tem **o caso medido como primeiro caso**: aceita 44,9→44,9 com dx/dz
legítimos, **recusa 44,9→38,8 mesmo com dx=dz=0**.

**Por que agora o teste pega o bug:** a regra antiga, rodada contra o caso medido, devolve `true` (aceita). A nova
devolve `false`. O caso de aceitação legítima (mesmo andar, 0,8 m de lado) continua `true` nas duas — o fix não
aperta o que a regra existe para permitir.

**Segunda metade do fix, sem regressão de contador:** com o ajuste recusado, o caminho da queda caía em
`SkipPiece` e a peça virava `desistindo de` — trocar um bug por uma regressão numérica. Agora o segundo tombo
devolve a peça **para a fila** (`QueueRetry`), que é literalmente a regra do jogador ("ficar tentando até
conseguir"); e o **primeiro** tombo já tenta o ajuste (antes só o segundo tentava, então a primeira queda não
tinha resposta nenhuma). `FallAdjusted` é zerado no retry (`:4238`), então a peça pode ser tentada de novo com
uma pose nova.

### 11c. ~~A porta voltou para o meio da obra~~ — **regressão do próprio ciclo, achada e fechada 16/11/2026**

O jogador cobrou três vezes a mesma regra: *"as portas precisam ser os últimos itens colocados, pois se não ele
perde muito tempo abrindo e fechando elas toda hora pra passar"*. O `BuildBlueprintParser.ClassOf` a implementa
desde 03/11/2026: `door`/`gate` recebe a **classe 4**, a última, e o plano põe todas as portas da obra no fim do
arquivo. O `BelowIndex` do caminho de retry também a respeita: `ClassOf(...) == 4 ? -1 : index` (`:2032`).

**O gate de andar do item anterior não respeitava.** `PendingBelowPlan` e `LowestPendingBelow` perguntavam só
"`Below(step.Level, nextLevel)`?" — e uma porta do andar de baixo responde **sim**. Pior: `LowestPendingBelow`
escolhe a peça de menor `Level` e, havendo empate, a primeira na ordem do plano — então a porta era **servida**,
isto é, puxada do fim do plano para a frente da caminhada.

**Medido na run de 16/11/2026 (DLL 1.9.6 de 16:24, `CommHomeBasicBP`, 985 peças, 1 companheiro):**

| o que | medido |
|---|---|
| índices das 7 `wood_gate` no plano | **979 a 985** (o fim do arquivo, onde a classe 4 as põe) |
| posição em que foram colocadas | peças **412 a 418** de 510 concluídas |
| a linha do log | `nada acima de 41,8 m antes do andar de baixo: voltando para wood_gate (979/985) ... - 52 peça(s) pendente(s) abaixo` |

**A correção** é `BuildStoreyGate.CountsAsPending(level, nextLevel, pieceClass)`: a porta (`DoorClass` 4) **não**
conta como pendência **e não** é servida. A exclusão vale nos dois lados de propósito — excluir só de um lado ou
devolve a porta ao meio da obra (pelo outro lado), ou trava a obra para sempre (as 7 últimas peças do plano nunca
seriam colocadas, e o gate jamais deixaria subir). Ligada nas duas funções.

**O teste que pega o bug:** `(aq)` ganhou 5 asserções; a primeira —
`CountsAsPending(1.0, 2.0, DoorClass) == false` — **falha** no código de 16/11/2026 e passa no de agora. As outras
fixam que a viga/piso/telhado do andar de baixo **continuam** contando (a exceção é a porta, não a regra).

### 11d. O portão atropelava a espera da fila (a morte da peça 851) — **fechado 16/11/2026**
A run de 16/11/2026 registrou **1 morte**, e o jogador foi explícito: *"essa morte dele aí é normal, deixa
realista, afinal uma mortezunha só na run inteira tá de boa"*. A morte em si **não** é tratada como defeito. Mas o
log mostrou que ela teve um empurrão, e o empurrão é barato de tirar:

```
a queda nao e o fim da peca: piece_chest_wood volta para a fila em vez de ser dada por perdida (1 queda(s))
retry later: piece_chest_wood (falls) at (-152,5, 44,9, -427,1) - 1/3      <- a fila deu 2 s de espera
nada acima de 44,9 m antes do andar de baixo: voltando para piece_chest_wood (851/985)   <- o gate a puxou NO MESMO FRAME
"Companion" fell 6,8 m and took 18 fall damage                              <- e caiu de novo, do mesmo lugar
```

**A causa:** `NextRetryBelow` (o caminho de retry mais antigo) sempre checou `Time.time < state.RetryAt` (`:1980`).
`LowestPendingBelow`, que o gate passou a usar, lê só o **plano** e nunca o relógio da fila. Então a peça que
acabou de cair e recebeu espera era servida imediatamente — o companheiro voltava para a mesma borda no frame
seguinte.

**A correção** é `BuildStoreyGate.Servable(queued, due)`, ligada em `LowestPendingBelow`: peça na fila e ainda em
espera **não** é servível; o gate serve a próxima pendente ou espera. Isso **não** enfraquece a subida — a peça
continua pendente e continua segurando o andar (a regra do jogador); muda só **qual** peça ganha a vez. Harness
seção `(aq)`, +3 asserções.

### 11e. O run de fechamento do ciclo 16/11/2026 — **medido, 985/985**

Run solo de `CommHomeBasicBP` (985 peças, 1 companheiro) com a DLL de 16:24, medida **antes** das correções 11c e
11d entrarem (elas ainda não estavam em jogo — ver a ressalva no fim):

| métrica | run de 23:33 | **run de 16:24** |
|---|---|---|
| peças colocadas | 985/985 | **985/985** |
| `hc_ai_blueprint_done` | sim | **sim** |
| puladas (contador e fala) | 0 / 0 | **0 / 0** |
| `desistindo de` | 0 | **0** |
| quedas | 5 | **4** |
| mortes | 1 | **1** |
| resgates | 0 | **0** |
| `no progress towards` | 2 | **0** |
| `log ajustado` (peça arrastada de andar) | 1 | **0** |
| linhas por peça | 3,03 | 3,05 |
| **reconciliação da cena** | 982 casadas, **1 faltando de verdade** | **983 casadas, 2 explicadas, 0 faltando** |

**O que fechou:** a peça que a reconciliação acusava fora do lugar em *toda* run — o `wood_beam_45` do plano em
`y=45,0` colocado em `y=38,8` — **não aparece mais**: zero `log ajustado` no log inteiro. É o `BuildAdjustPose`
(seção 11b) fazendo o trabalho dele.

**O que esta run NÃO provou:** ela rodou com a DLL de 16:24, então as correções 11c (porta) e 11d (espera da fila)
**não estavam em jogo**. Os dados confirmam os dois defeitos presentes: as 7 portas colocadas na posição 412–418 de
510, e a peça 851 servida no mesmo frame da queda que culminou na morte. As duas ainda precisam de run com a DLL
nova.

### 11f. A obra fechou como "incompleta" com todas as peças de pé — **achado e fechado 16/11/2026**

A run de 16:57 (a primeira com as correções 11c e 11d em jogo) foi **um sucesso no que ela foi medir**: as 7 portas
foram colocadas nas posições **978 a 984 de 985** — as últimas, como o jogador pediu — e a cena reconciliou
**985/985, 983 casadas, 2 explicadas, 0 faltando de verdade**. Mas ela fechou com uma frase errada:

```
order ended: hc_ai_blueprint_incomplete (6 peça(s) não subiram)
```

**As seis estavam de pé.** O próprio log as encontrou, uma linha por peça, antes de fechar:

```
a peça 986/991 já está de pé (outra tentativa a deixou lá) - seguindo
a peça 987/991 já está de pé (outra tentativa a deixou lá) - seguindo
... 988, 989, 990, 991
```

**A causa:** o retry pass do companheiro único **anexa** ao próprio plano as peças que não subiram
(`state.Plan.AddRange(again)` em `Advance`), de propósito — o objeto do passo é a identidade da peça, então anexar o
mesmo objeto deixa a caminhada voltar nela sem remapeamento. O efeito colateral é que `Plan.Count` **cresce**: 985 →
991. E `PlacedIndices` guarda o **primeiro** índice de cada passo (`PieceIndex`: "a cópia no fim não é outra peça"),
então nunca passa de 985. O `Finish` comparava `placed` (985) com `planned` = `Plan.Count` (991) e concluía que
faltavam 6.

**A correção:** `State.PlanTotal`, gravado quando o plano é montado, antes de qualquer retry pass anexar. A tabela
pura `AI/BuildPlanTotal.cs` (`Of` e `Complete`) decide, e o `Finish` passa a ler o tamanho da **casa**, não o tamanho
da lista. Os dois lados da regra do jogador continuam valendo, e o harness fixa os dois: **985 de 985 fecha como
completa** e **984 de 985 continua incompleta**.

### 11g. O buraco de uma laje com a obra fechando e o `planned` em 999 — **aberto, medido 16/11/2026**

A run seguinte (DLL 18:34, `run-final-1120`) expôs que o 11f **não se confirmou em jogo**. Duas medições, ambas
independentes do log:

**1. Uma peça colocada não existe mais.** As 985 linhas `done:` conferidas uma a uma contra o `build.pieces` dão
**984 casadas e 1 órfã** — `wood_floor` em `(-160,3, 43,8, -427,5)`, a laje `#692` do plano (`y` local 4,00, mundo
`(-160.45, 44.0, -427.44)`). Ela foi `placed (Direct)` e `done ... supported` nas linhas 2161-2162 e **não está na
cena**; nenhuma peça não-pareada aparece a menos de 14 m do alvo, então não foi para o lugar errado: ela deixou o
mundo. O que veio depois, na ordem exata do log: peça **851/985** → `retry later (falls) 1/3` → `fell 6,8 m and took
18 fall damage` → morte → `resuming at piece 851` com as mesmas 985 peças. **O resume herdou `PlacedIndices` sem
verificar que as peças continuam de pé** — é a causa a atacar.

**2. O fechamento mentiu de novo, ao contrário.** `order ended: hc_ai_blueprint_incomplete (14 peça(s) não subiram)`
sobre um plano de 985. `planned` saiu **999**: a passada de retry anexou 14 peças (985 → 999) e o `PlanTotal` não
segurou. O número certo era **1**, e é a peça órfã acima.

```
3266: next: wood_stepladder (986/999) a 12,5 m
...
3293: a peça 999/999 já está de pé (outra tentativa a deixou lá) - seguindo
3295: order ended: hc_ai_blueprint_incomplete (14 peça(s) não subiram)
```

**O que isso custou em instrumentação (e não pode repetir):** o sampler viu `done=985 in_scene=984 gap=-1` em **20
amostras seguidas** e o `-1` foi lido como ruído. O `reconcile_scene.py`, que acusou a peça em um segundo, só rodou no
fim, como formalidade de aceite. **Regra nova:** o reconcile roda **durante** o run, e qualquer `gap` negativo ou peça
órfã abre alarme na hora, com prefab e posição.

**Aceite deste item:** (a) o resume não herda `PlacedIndices` sem conferir a cena; (b) `PlanTotal` sobrevive à
passagem que reconstrói o estado no resume; (c) o reconcile roda dentro do ciclo, não depois; (d) a run fecha com o
número igual ao da reconciliação.

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

### 21. A obra "termina" em `hc_ai_blueprint_done` com **490 peças puladas** — *medido 13/11/2026, o item mais grave; correção implementada 13/11/2026, **confirmada no run solo de 13/11/2026 (985/985)**; resta o item 18*
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

**Correção implementada (13/11/2026, ciclo do `fix/piso-apoio`):** a peça dada por perdida **volta ao plano**, numa
**escada de três degraus** que é a do jogador — *"entra na fila pra uma nova tentativa depois de umas outras 5 ~10 peças
colocadas e depois no fim das peças do andar da blueprint e só por ultimo no final de toda construção"*:

| degrau | quando | por quê |
|---|---|---|
| 1 — `Queue` | **6 peças colocadas** depois da falha (`BuildRecovery.QueueGap`) | cada peça que sobe é mais uma coisa segurando a próxima; o andar ainda está sendo construído |
| 2 — `Storey` | a caminhada vai **subir de andar** (`nextLevel > curLevel`) | o andar da peça está tão completo quanto vai ficar antes do próximo começar |
| 3 — `End` | o plano acabou | a casa terminada é o máximo de apoio que a peça vai ter |

Cada degrau zera as tentativas e o orçamento de auto-cura (puxão de apoio, poste de emergência, ajuste de pose): o segundo
ataque não é cópia do primeiro, é outro ataque, porque a casa em volta mudou. O contador `PlannedStep.Recovered` é por
**peça** (teto `BuildRecovery.MaxTries` = 3), então os três degraus não podem virar laço; e todo degrau depois do primeiro
exige que **alguma peça tenha subido desde a última tentativa** — uma peça que volta para a mesma casa encontra a mesma
recusa, e no fim do plano, onde nada mais é colocado, ela seria re-tentada a cada frame.

A regra pura está em `src/Companions/AI/BuildRecovery.cs` (`Decide` responde o degrau, `InsertAt` a posição) e o ponto de
chamada é `MHR_Build.RecoverDue`, no `Advance`, **depois** da fila de retry (peça que a ordem ainda segura não está dada por
perdida) e **antes** do gate de peça. No caminho solo o passo é **movido dentro do plano** com `Reorder` e a caminhada
continua de onde estava — não pula peça nenhuma, as que ficam entre a caminhada e a recuperada são colocadas no tempo delas,
que é exatamente o "depois de umas outras peças". Num **sítio** (grupo) o plano é do grupo e ninguém reordena: o único
degrau é o fim da obra, por append, onde o índice de ninguém se move. **Não é regra deste blueprint:** ela lê só o andar
(que todo plano carrega) e as contagens da própria ordem.

**O que o run de 13/11/2026 mediu e este fix ataca:** as duas lajes do andar 43,8 falharam com `no progress` (o corpo
estava em 39,7, `route partial`, a subida ainda não existia) e o companheiro ficou um andar abaixo sem rota até elas:
3 tentativas cada, `desistindo de wood_floor` duas vezes, e a obra declarada pronta em **983/985**. A casa que terminou em
volta delas é exatamente o que as sustentaria — a leitura do jogador, antes do log: *"aqueles dois ali que faltaram não
iriam cair se ele tivesse colocado, pois estariam ancorados nos das laterais"*. Com a escada, aquelas duas não esperam o
fim da obra: a 660 e a 679 voltam logo depois de outras 6 peças, com o andar 43,8 ainda em construção.

Aceite: run solo do `CommHomeBasicBP` fecha **985/985** — peça faltando não é tolerada, e o aviso de peça faltando não é
aceite alternativo (foi recusado explicitamente pelo jogador: *"pra que um aviso de peças faltando… se eu fui bem claro
que não pode ficar nenhuma peça faltando sem ser colocada?"*).

**Run de confirmação — 13/11/2026, solo, 1 companheiro, `CommHomeBasicBP` (985 peças), DLL 1.9.6:**

```
PLACAR: done 985 · puladas (contador) 1 · puladas (fala final) 0 · mortes 0
        apoio de emergência 23 colocados | 2 sem lugar
RETRY:  no progress 23 later / 2 desist · verification 3 · blocked_by_player 2 · no_support 2
ESPERA: 4,5 s medidas, 2 retenções de barreira (contra 1014,4 s / 177 retenções do run anterior)
order ended: hc_ai_blueprint_done
```

**Os dois degraus que o run usou, e as peças que voltaram de pé:**

```
linha  732: desistindo de wood_floor em (-166,5, 39,8, -436,2)
linha  932: peça dada por perdida volta ao plano (depois de outras peças): wood_floor - tentativa 1/3
linha  932: volta antes de subir: wood_floor (202/985) -> placed (Direct) -> done: wood_floor ... supported
linha 2223: desistindo de wood_wall_half em (-151,2, 43,3, -425,3)
            volta ao plano (depois de outras peças), 7 peças depois, tentativa 1/3 — também ficou de pé
```

As duas voltaram no **degrau 1** (`Queue`, 6 e 7 peças colocadas depois) e ficaram de pé na primeira nova tentativa —
exatamente a faixa *"depois de umas outras 5 ~10 peças colocadas"* que o jogador descreveu. A caminhada **não pulou**:
logo depois do `Reorder` o log mostra `next: wood_pole2 (195/985)`, o passo seguinte ao que estava em execução.

**A verificação geométrica independente (fora do mod):** as `build.pieces` do sítio (raio 32) somam **1008 = 985 do
blueprint + 23 postes de emergência**. Transformando o `.blueprint` para o mundo (rotação 76,5°, âncora nos 2 `bed`) e
comparando peça a peça por prefab com atribuição ótima: **984 das 985 casam** com distância média de **0,19 m** (máx.
1,0 m, que é o assentamento no terreno). A 985ª **não falta** — está na **altura errada**: o `piece_chest_wood` do plano em
`(-163,2, 44,9, -422,8)` foi plantado em `(-163,1, 38,8, -422,8)`, e o log diz por quê:

```
log ajustado: piece_chest_wood de (-163,1, 44,9, -422,8) para (-163,1, 38,8, -422,8) - falls
[Move] "Companion" fell 6,8 m and took 18 fall damage
```

É o **item 18** (`AdjustPose` troca o Y do plano pelo `GetGroundHeight`) aparecendo uma vez neste run — a peça existe,
o andar é que está errado. Foi a única ocorrência (`log ajustado` aparece 1× no log). Pelo critério do jogador isso ainda é
peça faltando onde ela devia estar, então o item 18 é o que falta para o aceite ficar redondo.

**O que o run ainda mostrou e não é deste fix:** 23 apoios de emergência (item 19 — o early check os planta sem tentar a
puxada antes) e 2 quedas de 4,8 m e 6,8 m, 0 mortes.

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

### 18. `AdjustPose` troca o **Y** do alvo pelo chão — *medido 13/11/2026, **ainda acontecendo no run de confirmação** (1×), não corrigido*
`AdjustPose` (`MHR_Build.cs:7275`) protege o XZ (raio de 1 m para peça estrutural) mas `BuildSite.Find` monta o
candidato com `ZoneSystem.GetGroundHeight` (`AI/BuildSite.cs:146`) — o **Y do plano é jogado fora**. Medido no run de
13/11/2026: um baú do andar de **44,9** virou alvo em **38,8** (`log ajustado: piece_chest_wood de (-161,4, 44,9, ...)
para (-161,4, 38,8, ...) - falls`) e o companheiro caiu os 6,8 m que o mataram, encerrando a obra em 851/985.
**Correção proposta:** `Find` recebe a altura do plano e o ajuste só varre XZ nessa altura (ou, para peça estrutural,
recusa qualquer pose cujo Y difira do plano mais que a tolerância). Provar no harness com a tabela pura do filtro de
candidato. É o próximo item do tema "rota vertical" (com o 11).

**No run de confirmação de 13/11/2026 (985/985) aconteceu 1×, e é a única diferença entre o plano e o que está de pé:**
`log ajustado: piece_chest_wood de (-163,1, 44,9, -422,8) para (-163,1, 38,8, -422,8) - falls`, seguido de
`fell 6,8 m and took 18 fall damage`. A verificação geométrica pós-run achou **984/985** justamente por causa dela. É o
próximo alvo do tema vertical.

### 19. O early check planta poste de emergência **sem** dar chance ao `PullSupportBehind` — *diagnóstico de 13/11/2026 **refutado** pela medição do run de confirmação*

**O diagnóstico original estava errado e a premissa não existe mais no código.** O `Goto` (`MHR_Build.cs:6485-6518`)
**já** chama, nesta ordem, `PullSupports` → `PullSupportBehind` → `PullDirectSupport` e **só então** `TryFallbackPost`.
O `PullSupportBehind` entrou no próprio ciclo do piso (`0d26a80`), então o item descreve um código que não existe mais.

**O run de confirmação de 13/11/2026 (985/985, 1 companheiro) mediu os 23 postes um a um, e nenhum deles era falta de
puxada:**

| pergunta | resposta medida |
|---|---|
| foram plantados pelo early check? | **23 de 23** (o `next:` do passo aparece imediatamente antes) |
| havia apoio do plano **já de pé** na faixa que o poste preenche (0,4-1,0 m abaixo, ≤2,5 m em XZ)? | **22 de 23** |
| havia peça do plano **ainda não colocada** nessa faixa (logo, algo a puxar)? | **1 de 23**, e era um `piece_chest_wood` (mobília, nunca apoio) |
| a peça ficou de pé logo depois do poste? | **23 de 23** |

Ou seja: a puxada não era a saída, porque **não havia nada para puxar**. O `no_support` vinha de `BuildProbe.
FootprintSupported` (`AI/BuildProbe.cs:686`) — a teste é "algum colisor dentro da **pegada** da peça carrega?", com a
pegada crescida 0,15 m. No caso do piso 298 (linha 1064 do log, `wood_floor` em `(-153,2, 40,8, -419,6)`): as vigas
que o seguram (`wood_beam` em y local 0,0, topo em 1,0) estão de pé desde as linhas 804/810/813, mas as **mais próximas
ficam a 0,97 m do centro** — fora da pegada de um piso 2×2. As meias-paredes em y local 0,5 (topo exatamente no plano do
piso) ficam a 2,1-2,4 m. O piso é recusado por um vazio de colisão que o **jogo** aceita: logo depois o poste é
plantado e a mesma peça entra na primeira tentativa.

**O que isso custa:** 23 peças que não estão no `.blueprint` numa casa de 985 (2,3 %), e a verificação geométrica
pós-run tem de descontá-las para casar o sítio com o plano (`1008 = 985 + 23`). Numa estrutura de 18 mil peças a mesma
taxa seriam ~420 postes — por isso o item vale corrigir antes de rodar o cathedral.

**Correção proposta (medida, não implementada):** a pegada do teste de apoio é a caixa do **prefab**, mas o jogo lê o
apoio pela `WearNTear` da peça sob o centro e pelas bordas até 2,5 m (é o que `BuildSupportBelow.IsDirectSupport` já
modela para a puxada). Duas saídas, ambas prováveis no harness:

1. **Barata:** em `FootprintSupported`, quando a pegada não acha nada, repetir a varredura com a meia-largura crescida
   até `BuildSupportBelow.MaxFlat` antes de responder `no_support` — o mesmo alcance que a puxada já usa, para que as
   duas regras não discordem.
2. **Estrutural:** a decisão vira tabela pura (`BuildSupportBelow.HoldsWithin`) que recebe a lista de candidatos com
   (dx, dz, drop, carrega?) e responde o veredito; o `BuildProbe` só coleta. É a forma que o harness consegue travar.

Aceite: `apoio de emergência < 5` no mesmo run solo **e** `build.pieces == 985` (sem peças fora do plano).

#### 19.2 O run de validação parou em **190/985**: o companheiro fechou o piso **sobre a própria cabeça** — *corrigido em 13/11/2026*

O run que ia validar 19.1 parou em 190 peças, e o defeito não tem relação com o poste. Lido do
`BepInEx\LogOutput.log` (985 peças no plano, 1 worker):

| linha | o que diz |
|---|---|
| 651 | `done: wood_floor at (-147,8, 39,8, -430,1)` — peça 173 |
| 655–676 | peças 174–178, mais cinco `wood_floor`, todas em **39,8** |
| 678 | `next: wood_floor (179/985) a 16,3 m` |
| 661 | `[Move|Build] the way is blocked - trying another one at (-146,9, 38,8, -426,8)` |
| 663–740 | 35 × `no progress ... route full`, walk spots todos em 39,8/39,9 |
| 858 | `[Move|Follow] trapped at (-147,7, 38,8, -429,5) for 90s with no way out` |

**A casa é que permite o defeito:** o térreo fica em 38,8, o deque em 39,8 e um `wood_wall_half` em 39,3 fecha o vão — **1 m
de pé-direito**. O companheiro andou pela XZ de cada peça com os pés em 38,8, colocou seis pisos por cima de si e o sexto
selou o crawlspace. Depois disso não havia rota para lugar nenhum.

**Duas causas, as duas medidas:**

1. **O `WalkSpot` nunca rodou para essas peças.** O portão era `state.Pos.y > me.y + 1f`; a peça está em 39,8 e os pés em
   38,8, então `39,8 > 39,8` é **falso** — o companheiro andava direto para a XZ da peça (o chão sob o deque). Agora o
   portão é `> me.y + 0,01f`.
2. **A colocação não via que a peça era uma tampa.** O `inReach` é a regra do olho do jogo e está **certa** (piso 1 m
   acima, 2 m de lado, está no alcance). O que faltava era a consequência: `CheckPoint` recebe o `CompanionSetup` como
   `ignore`, então o corpo do próprio companheiro **nunca recusa a própria peça** — exceção que existe para o piso que se
   pisa e que se estendeu ao piso que cai na cabeça. Nova tabela pura `BuildOverhead.LandsOnHead`: o corpo está sob a
   peça (caixa + raio da cápsula) **e** a peça começa acima dos pés **e** abaixo de `Headroom = 1,8 m` → é tampa;
   o companheiro **anda** em vez de colocar, e o `WalkSpot` (já corrigido) o leva para cima do deque.

Também mudou a escolha do walk spot: o primeiro passe pegava a superfície **mais perto do companheiro**, e o chão do
crawlspace (aos pés) vencia o deque a 4 m. Agora o primeiro critério é o **nível da peça** — a superfície de onde se
constrói *naquele* nível é a daquele nível, não a mais perto dos pés — e a distância só desempata entre superfícies do
mesmo nível.

Harness: seção (x) ganhou 3 casos (deque vence o chão; desempate por distância no mesmo nível; fronteira de 2,5 m) e a
seção nova (an) tem 9. **1145 PASS / 0 FAIL**, build 0/0, MCP 135/0, config doc em dia.

Aceite do run solo (o mesmo de 19.1, mais as duas linhas novas):

| métrica | antes (13/11) | alvo |
|---|---|---|
| `done` | 190 (travou, cancelado) | **985** |
| `trapped ... no way out` | 1 | **0** |
| `no progress` | 35 | **< 6** |
| `desistindo de` | 1 | **0** |
| `apoio de emergência` | 0 | **< 5** |

---

#### 19.3 A **chegada** de uma caminhada não é o **alcance** de uma colocação — *corrigido em 14/11/2026, run de validação reprovado por outro defeito (ver 19.4)*

Duas runs solo do `CommHomeBasicBP` (985 peças, 1 worker) mediram que o companheiro parava de andar **antes da hora** e
depois não colocava a peça. As duas causas saem do mesmo lugar: `MHR_Mover.Go` recebia `Reach` (2,5 m) como raio de
**chegada**, e o teste vertical era o `1,5f` fixo.

**Run A (00:19, DLL `d15044a`)** parou em **236/985** com 18 `no progress` e 2 peças dadas por perdidas. As 18 linhas
tinham assinatura única: **11 de 15** com o corpo em **38,8/38,7** e o walk spot em **39,8/39,9** — `dy` de 1,0/1,1 m e
`flat` entre 0,6 e 2,5 m. O `MHR_Mover.GoInner` considerava "cheguei" com `|Δy| <= 1,5 m`, então o corpo **1,0 m sob o
deque** (o pé-direito medido da casa) era "chegado": ele parava, os 12 s corriam, a peça ia para a fila.

**Run B (00:40, com as duas correções abaixo)** parou em **170/985** ao ser cancelada, mas com outra assinatura, nas
peças **147/148**: `walk spot (-160,9, 39,0, -428,9)` e o corpo em `(-162,4, 38,8, -427,3)` — **2,19 m do spot** e
2,97 m da peça, em pé no chão **sob** ela, onde `OverheadBlocked` recusa o piso por ser tampa. Tentada 3× e
**abandonada** (`desistindo de stone_floor_2x2 ... 1 pulada(s)`). O raio de **colocação** (`Reach` 2,5 m) sendo usado
como raio de **chegada** é o defeito: a 2,19 m do ponto, o corpo está em outro lugar.

**As correções**, ambas em tabela pura (`AI/BuildArrival.cs`, seção (ao) do harness):

| constante | valor | para quê |
|---|---|---|
| `BuildArrival.Surface` | 0,5 m | tolerância **vertical** de quem vai a uma superfície pisar nela (abaixo dos 1,0 m medidos) |
| `BuildArrival.Step` | 1,5 m | tolerância vertical histórica, para alvos que não são superfície |
| `BuildArrival.SurfaceReach` | 1,0 m | tolerância **plana** de chegada a uma superfície (abaixo dos 2,19 m medidos) |

`MHR_Mover` ganhou a sobrecarga `Go(..., float vertical)` com `[System.ThreadStatic] s_arrivalLevel`, e
`MHR_Build.Goto` passa `SurfaceArrival`/`SurfaceReach` **só** quando `state.HasWalkTo` (alvo é uma superfície); peça
aos pés continua com `Reach` e o passo de 1,5 m. Harness **1156 PASS / 0 FAIL**.

**O que o run C (01:12→01:57, com tudo) provou, medido:** a família de defeito **sumiu** — zero linhas com
`dy = 1,0/1,1` sob o deque, e a run foi de **190 → 975 done**. O que reprovou o aceite é **outro** item (19.4).

---

#### 19.4 O run C fechou "pronto" com **10 peças faltando** e **109 exclusões do watchdog** — *fechado em 14/11/2026 (19.4.2: 985/985 na cena); medido em 14/11/2026*

##### 19.4.1 A **mobília** não é âncora: a sonda perguntava a peça errada sob o raio — *corrigido em 14/11/2026*

O jogador perguntou, sobre a captura dos buracos do telhado: *"por que cama como âncora?"*. A pergunta apontou para
duas linhas concretas do código, e as duas estavam erradas pelo mesmo motivo: **mobília sendo lida como apoio**.

| defeito | onde | o que dizia | o que a medição mostrou |
|---|---|---|---|
| a sonda escolhia a regra pela **altura** | `BuildProbe.CheckPoint` (`elevated`) | `point.y - surface.y > 0.6f` → footprint; senão "peça no chão", pergunta o `m_supports` da peça sob o raio | #901/#917 (`wood_roof_45`) com o raio parando **0,16 m** sobre um **baú**: a regra virava "peça no chão", perguntava o baú (`false`) e recusava **29 vezes** — com um `wood_floor` e um `wood_wall_roof_45` (ambos `true`) a **1,00 m** embaixo |
| **todo telhado** era aceito por **qualquer** `WearNTear` | `BuildSupportBelow.HoldsPlacedPiece` | `pieceClass == 3 \|\| wearSupports` — bypass escrito em 04/11/2026 sobre a crença (já corrigida em 11/11/2026) de que "telhado tem `m_supports = false`" | com todas as peças estruturais `true` (`docs/re/support-rule-wearntear.md` §5), o bypass era um **no-op** para telhado-sobre-telhado e o único efeito vivo era o **errado**: telhado em cima de **baú/cama** passava, embora o jogo nunca leia mobília como apoio (`WearNTear.cs:920-923`) e o fantasma vanilla recuse construir ali (`Player.cs:3890-3893`) |

**Correções** (tabelas puras, seções `(ap)` e `(r)` do harness):

- `AI/BuildSupportBranch.cs` (novo) — `UsesFootprint(drop, hitSupports)`: acima de `Elevated = 0,6 m` **ou** quando o
  que o raio achou **não carrega** (`m_supports = false`, mobília), a resposta é o **footprint**. Um raio que bateu em
  mobília não é prova de que a peça está no chão.
- `BuildSupportBelow.HoldsPlacedPiece` — sem exceção por classe: **toda** peça precisa de `m_supports` para segurar
  qualquer outra (o terreno continua segurando tudo; pedra/árvore sem `WearNTear` não segura nada).

**Medido no jogo depois do deploy** (`build.check` nas 4 posições que o run D adiou): `elevated = True` e **nenhum**
`no_support` nas quatro — só `needs_station`, que é a bancada ainda não construída naquele ponto da obra. O artefato
do baú morreu.

##### 19.4.2 O run solo com a correção: **985/985 peças, 0 faltando** — *fechado em 14/11/2026*

Run solo (`CommHomeBasicBP`, 1 companheiro sem grupo, sítio limpo 974 → 0, DLL com as duas correções acima,
95 métodos patcheados). Relatório (`build_run_report.py --last`) contra o run C:

| métrica | alvo | run C | **este run** |
|---|---|---|---|
| `done` | 985 | 975 | **985** |
| peças na cena | 985 | 974 | **985** |
| `puladas` (contador) | 0 | 17 | **1** (recuperada por `BuildRecovery`) |
| `no_support` (recusas) | — | 236 | **6** (−97,5 %) |
| `apoio de emergência` | < 5 | 3 | **0** |
| `no progress` | < 6 | 20 | **10** |
| watchdog "ficam de fora" | — | 30 chamadas / 109 peças | **0 chamadas / 0 peças** |
| `UNSUPPORTED` | 0 | — | **0** |
| mortes | 0 | 0 | **1** (queda de 6,8 m do telhado — item 11; o mod retomou na peça 851 depois do respawn) |
| `order ended` | `hc_ai_blueprint_done` | done | **`hc_ai_blueprint_done`** |
| linhas de log por peça | — | — | **3,14** |

**A reconciliação peça-a-peça fecha 985/985, não pelo relatório mas pela cena.** O ajuste cena↔blueprint
(yaw **105,000°**, `tx -166,5926 ty 39,8000 tz -430,7547`, tolerância 0,45 m) casa **983/985** com
distância média **0,196 m** e máxima **0,413 m**; os **2** restantes são as **únicas duas** peças do plano em
`y = -2.00` (`wood_wall_roof_top_45`, #1 e #2, a fundação enterrada) e elas **estão na cena** a **0,31 / 0,34 m**
em XZ e **0,97 m** acima do plano — assentaram no terreno, que é mais alto que o plano ali. Nenhuma peça
faltando, nenhuma duplicata, **2 extras** = essas duas mesmas peças. O método foi validado contra o run C
antes de ser usado (reproduz os **964/985** históricos com yaw 47,000°), o que é o que permite confiar no
número desta run.

**A nota de método importa:** o yaw desta run é **105°**, não os ~46° do run C, e por isso qualquer ajuste
semeado pela solução anterior divergia (78 pares, depois 473 em bacia errada no ICP). O ajuste correto é
**sem semente**: varredura de 360° de yaw com a translação ancorada na diferença de centróides, depois
polimento da mediana do resíduo. Um ajuste semeado não é evidência.

**O que o histograma de altura comprova** (run C → este run): `46,8` **46 → 48** (as duas telhas dos buracos
das capturas), `40,9` **11 → 22** (os 11 baús que faltavam), `42,4` **3 → 0** e `38,8` **70 → 65** (os postes
de emergência, que não são mais plantados).

---

#### 19.4 O run C fechou "pronto" com **10 peças faltando** e **109 exclusões do watchdog** — *corpo do item*

O run C terminou com `order ended: hc_ai_blueprint_done` (o mod disse "pronto") e o espelho do estado discorda:

| métrica | alvo (19.2) | medido |
|---|---|---|
| `done` distintos | 985 | **975** |
| peças na cena (`build.pieces`) | 985 | **974** |
| `desistindo de` | 0 | **1** (`piece_chest_wood`) |
| `puladas` (contador) | 0 | **17** |
| `no progress` | < 6 | **20** |
| `apoio de emergência` | < 5 | **3** (✓) |
| quedas / mortes / trapped | 0 | **0 / 0 / 0** (✓) |
| watchdog "ficam de fora" | — | **30 chamadas, 109 peças (48 distintas)** |

**A forma do defeito, do relatório (`build_run_report.py --last`):** `no_support` é **91 %** das 258 recusas
(**236** tentativas de dependência) e a espera de **1470,3 s** está **toda no fim do plano** (fila: fim 1470,3 /
meio 0,0). O bloco final é o **telhado**: `wood_roof_45` em **46,8** (8 m acima do deque) que não tem apoio de pé,
pede o `wood_beam_1` de emergência (fora do blueprint) e, quando não resolve, é pulado. O `piece_chest_wood` em
**40,9** faz o mesmo por rota (`route partial`, 12–18 m, tentado 3× em 7 passes de retry) e é a única peça
**abandonada**.

**Peças nomeadas pelo watchdog, por prefab:** `piece_chest_wood` 45, `wood_floor` 36, `wood_roof_45` 17,
`wood_beam` 5, `wood_beam_45` 4, `wood_wall_half` 1, `woodwall` 1.

**Fechado pelas duas regras do plano de `fix/piso-apoio`** (implementadas como `AI/LandingFloors.cs`, seção `(ak)` —
o item 1 do plano: um piso só é puxado para diante da escada se o que o segura já está de pé — e `AI/SettledPieces.cs`
— o item 2: sem grupo, uma peça **já colocada** deixava de ser excluída de candidata a apoio, porque
`MHR_BuildGroup.IsDone` devolve `false` para chave nula). Com as duas **mais** a correção de mobília do 19.4.1, o run
solo de 14/11/2026 fechou **985/985 peças na cena** e **0 faltando** (19.4.2).

**Este item não deixa de ser a razão de existir da correção — deixa de estar aberto.** O que o run C provou foi que
um "pronto" do mod pode discordar da cena; o que fecha o item é a **reconciliação peça-a-peça contra o blueprint**
(19.4.2), não o contador `done`. A regra que fica registrada: **nenhum run é declarado concluído pelo relatório —
só pela cena reconciliada**, porque foi o relatório que disse 975 quando a cena tinha 974.

---

#### 19.1 A causa real: o poste saía **sem espera nenhuma** — *corrigido em 13/11/2026, aguardando o run de validação*

A hipótese 1 acima **não fecha as contas**: medido o log do run, o apoio já de pé de cada um dos 17 pisos citados está a
**0,81-1,02 m do centro** — *dentro* da pegada de um piso 2×2 (a caixa vai a 1,15 m do centro, mais os 0,15 m do jogo),
não fora dela. Então `FootprintSupported` deveria ter achado o apoio, e a hipótese da "pegada estreita" não explica a
recusa. O que explica está no **tempo**:

| medido no log | valor |
|---|---|
| linha `next: wood_floor (298/985)` | 1064 |
| linha do poste sob ela | **1065** (a linha seguinte) |
| linha `placed` | 1066 |
| `sem apoio ainda` (o adiamento que gasta ~20 s) antes do poste | **nenhum** |
| total de adiamentos no run inteiro | **2** (os dois pisos que *não* ganharam poste, e por não haver vão) |

`TryFallbackPost` (`MHR_Build.cs:5691`) guardava o poste com `if (support >= 0 && !FallbackPostAllowed(...))`. O `&&`
**curto-circuita**: quando `DirectSupportIndex` devolve `-1` a guarda inteira é pulada e o poste é plantado **na primeira
recusa**. E `-1` é justamente o caso "todos os apoios da janela já estão de pé" — a puxada não tem o que puxar e a espera
some junto. Não havia, em nenhum caminho, os ~20 s que `SupportGrace` promete.

**A correção** (`BuildSupportBelow.FallbackPostAllowed` de 5 parâmetros + `MHR_Build.StandingSupportClose`): quando
`DirectSupportIndex` é `-1`, o `MHR_Build` varre o plano atrás de um apoio **já de pé** ao alcance do colisor do jogo
(`BuildSupportBelow.SupportClose = 1,3 m`). Se existe, o poste espera as mesmas 3 olhadas (~20 s) em vez de sair na
primeira — porque um apoio de pé **ao alcance do jogo** prova que a recusa não é "não há nada embaixo". Apoio de pé
**longe** (ou varredura que não rodou: `null`) mantém a janela antiga exata. Harness seção (u): 5 casos novos, 1133 PASS.

**Nota registrada, fora desta correção:** a sonda `build.check` mediu, nos mesmos pontos, que o jogo **aceita** esses
pisos hoje (`ok=true`, sem `no_support`) com o sítio já construído — ou seja, o `no_support` daquele instante era, muito
provavelmente, o colisor do vizinho que ainda não existia naquele frame (a peça ao lado é colocada no mesmo momento), e
voltar depois resolvia. Isso é outro item (a sonda não reconstrói o instante); fica registrado com o número.

Aceite: `apoio de emergência < 5` no mesmo run solo **e** `build.pieces == 985` (sem peças fora do plano).




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
3. Regra pura vai para o harness **no mesmo ciclo** (hoje **1127** asserções); quem implementa não afrouxa a asserção.
4. O que foi revertido fica **documentado com o número que o matou** (escopo XZ: ~33 min; `ahead` por frame: 38 vs 78
   peças/min; `CrossesPending`: 67 % do plano).
