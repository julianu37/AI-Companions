# Navegação do companheiro em obras — pesquisa e plano (03/11/2026)

> Por que o companheiro "fica burro" numa obra e o que os outros mods fazem. Decisão do jogador: fazer a **A**
> agora, mantendo **B** e **C** como opções se a A não resolver.

## 1. O problema, medido

Relatos do jogador em 03/11/2026, com log:

- **loop de escada**: `heading for the ladder … / climbed the ladder … / the ladder at (…) has no floor at its top … /
  heading for the ladder …` — o guarda recusava a 2ª escada e o mover reescolhia a mesma;
- **loop de descida**: `1,8–2,0m above where it needs to be - dropping down to (…, 41,5, …)` **10×** no mesmo lugar,
  sem cair (a beirada bloqueada), até um tombo de 4,1 m;
- **`no progress` a 6–25 m** em vigas/paredes/poste de níveis acima (o piso que os sustenta ainda não existe).

## 2. O que a pesquisa mostrou

- **Vikings for Hire** (Nexus 4057, "NEW SETTLEMENT NAVIGATION, WRITTEN FROM SCRATCH"): *"a navegação antiga usava a
  navmesh do jogo, e isso não funciona para uma base real: a navmesh é feita para monstros em terreno aberto, não para
  casas. Não conecta escadas que passam por um buraco no piso nem escadinhas íngremes… trata portas como paredes… não é
  reconstruída quando você constrói ou remove algo."* Eles reescreveram a navegação.
- **Unity**: o caminho padrão é `NavMeshBuilder` + `NavMeshBuildSource` (colisores) para rebake em runtime, ou um
  **Grid Graph** (A* Pathfinding Project) com `maxStepHeight`/`maxSlope` — um A* sobre pontos andáveis.
- **O que já temos**: `Patches/MHR_CompanionNavMesh.cs` registra um **agente próprio (tipo 40)** copiando o Humanoid mas
  **ignorando portas** e subindo **0,6 m**, com `BuildTile` reescrito e rebake assíncrono. É melhor que o Humanoid, mas
  **não modela escadinha vertical nem água de telhado** — o jogo não cria link para isso.

## 3. Por que o mover fica preso

`MHR_Mover.GoInner` tem **primitivas locais** (andar, pular, subir escada, descer beirada, contornar) escolhidas por
heurística, uma por vez. Quando o caminho direto falha, ele **repete a mesma primitiva** (os dois loops acima). Não
existe uma **busca de rota alternativa** — o que o jogador pediu ("seria só ele subir pelo telhado").

## 4. As três opções

### A. Busca de rota por sondas (FEITA nesta rodada - `MHR_Mover.TryDetour`)

> **Implementado em 03/11/2026:** `TryDetour` roda no `Unstick`, depois de escada/descer, quando as outras primitivas
> falharam: 8 direcoes x 2 distancias (1,5/3,0 m) x 3 alturas (0/+1,6/+3,2 m), aceitando superficie com chao, espaco de
> cabeca, linha do peito livre e que **aproxime do alvo** ou **ganhe altura** (alvo acima); ele anda ate o ponto por 4 s
> (`HasDetour`/`Detour`) e a rota normal continua. **3 tentativas por alvo**, 1 a cada 3 s, zeradas quando o alvo muda.
> Log: `the way is blocked - trying another one at (…) (N m up)`; sem alternativa, `no alternative route near (…)` (LogDetailed).

**Correcoes da revisao (03/11/2026):** (1) `DetourTries`/`NextDetourAt` zeram no **`Reset`** (o reset tinha caido no
`Release` - a fase A morria depois de 3 detours no save); (2) ganho de altura limitado a **1,1 m em superficie vertical** e
ate **2,3 m so em superficie inclinada** (telhado) - a janela antiga elegia o **topo de parede** que o `Linecast` deixava
passar e que ele nunca alcancava; (3) `CheckCapsule` no destino quando o degrau passa de 0,4 m (cabe o corpo?); (4) o
**ganho de altura so vale em rota de build** (em Follow/Combat subia em mesa/parede com o jogador no mesmo nivel);
(5) `TryDetour` roda **depois** do `TryStartClimb` (a saida de buraco primeiro); (6) a sonda comeca em `k = 1` (nao repete a
direcao que acabou de falhar); (7) so com `IsOnGround()`.
Uma **sonda em cruz** em volta do companheiro: 8 direções × 2 distâncias (1,5 e 3,0 m) × 3 alturas (atual, +1,5 m e
+3 m — telhado/viga). Cada candidato é aceito se houver **chão** (raycast para baixo), **espaço de cabeça**, a linha do
peito até ele estiver **livre** e ele **aproximar do alvo** (XZ) — e, quando o alvo está **acima**, ganhar **altura**
(pontuação dupla para o ganho de altura). Ele então anda até o candidato por alguns segundos ("detour") e tenta de novo.
Custo baixo, contido no mover, sem estado novo no ZDO.

### B. Ligar escada/telhado na navmesh (se A não bastar)
Criar **links** entre tiles para escadinha vertical e água de telhado. Alto custo: o `Pathfinding` do jogo não expõe
links (só tiles), então seria preciso injetar `OffMeshLink`/`NavMeshLink` próprios e ensinar o `Pathfinding.GetPath` a
usá-los.

### C. Navmesh própria por voxel/grade (último recurso)
Como o Vikings for Hire: grade 3D reconstruída com a obra (colisores), A* próprio. É a solução "de verdade", mas são
dias de trabalho e mexe no coração do movimento — só com o A e o B esgotados.

## 5. Critério de aceite da A

- no loop da escada: a recusa **não** se repete (a escada fica 60 s de fora, `MarkBadLadder`);
- no loop de descida: 3 tentativas e o caminho é abandonado por 8 s (`S.DropTries`/`DropUntil`);
- com o alvo acima e o caminho direto bloqueado: ele **procura uma altura** (telhado/viga) e tenta por lá;
- nada de `no progress` repetido na **mesma** peça por mais de 12 s (o retry pass assume).

## 6. Vikings for Hire 1.6.6 — o que eles fazem de verdade (inspeção da DLL, 03/11/2026)

Fonte: pacote público do Thunderstore `Worobeyko/VikingsForHire` **1.6.6** (o Nexus exige login),
`plugins/ValheimNpc.dll` (SHA-256 `4db116450d9aad9735279158c1bfead71003f1954a5b8b7996855940218a336b`), decompilado com
`ilspycmd 9.1.0`. Tudo abaixo é **leitura de CIL/C# decompilado**, não execução.

- **Agente próprio na navmesh do jogo:** `VikingPathAgent` (postfix de `Pathfinding.SetupAgents`) chama o `AddAgent`
  privado e registra o **agent type 61** a partir das configurações do Humanoid: `agentRadius` 0.25–0.35
  (`BodyRadius` 0.37), `agentHeight` 1.5, **`agentClimb` 0.3**, `agentSlope` 85, `overrideVoxelSize` e
  **`voxelSize` 0.1**.
- **A navmesh não é substituída; é acordada:** `VikingNavmesh` usa `Pathfinding.PokeTile`/`m_tiles`; `WarmZone`
  (raio, 20 s por zona) e `Invalidate(point)` marcam `m_buildTime = -1000` nos tiles em volta (±2 m), o que faz o
  `BuildTile` do jogo reconstruí-los; `DoorSettleSeconds` 1.2.
- **Ganchos na obra:** `[HarmonyPatch(typeof(WearNTear), "OnPlaced")]` postfix e `WearNTear.Destroy` prefix chamam
  `VikingNavmesh.Invalidate(transform.position)` + `NavMap.DirtyPiece`; `Door.SetState` postfix chama
  `NavMemory.Forget` + `NavMap.DirtyPiece`. **É isso que faz o piso novo existir para o agente.**
- **Escadas com link explícito:** `StairLinks` varre peças a 40 m/4 s, acha escadas com `rise >= 1 m` e cria
  `NavMeshLinkInstance` entre o pé e o topo (0.6 m para fora), em vez de confiar só no `BuildTile`.
- **A\* próprio para os assentamentos:** `NavMap` (célula 0.5 m, 6 níveis, step 0.55, body 0.28, headroom 0.6–1.55,
  normal >= 0.55, trail/door cost 0.6, heurística peso 2, 20k expansões, jobs com budget de 300 ms, smoothing por LOS,
  memória de falha/caminho) + `NavMemory` (célula 0.5 m, `LevelTolerance` 1.2, `BlockedAt` 1.5, `MaxScore` 4,
  `MaxNodes` 4000, save a cada 300 s, versão 3). O `SmartNavigation.Enabled` é só um flag; o A* é do `NavMap`.

**Correção (03/11/2026, decompilado do Valheim 1.0.20 — `docs/re/navmesh-tiles.md`):** o "não invalida" acima é
impreciso e o `m_buildTime = -1000` literal é uma tempestade. O que o jogo faz:

- **Já re-pokeia no `GetPath`:** `PokeArea(from/to, agentType)` marca 3×3 tiles do **agente pedido**
  (`Pathfinding.cs:352-356`, `:526-541`); `Buildtiles` reconstrói **1 tile por 0,1 s** quando
  `m_pokeTime - m_buildTime > m_updateInterval` (**5 s**) (`:554-576`); o tile do canteiro **entra** no mapa, com até
  ~5 s de atraso. **Nenhum** código do jogo invalida a navmesh ao colocar/remover peça (`WearNTear.OnPlaced`/`Destroy`
  não falam com `Pathfinding`).
- **O sintoma dos 12–17 m é o `SnapToNavMesh`, não o "tile invisível":** o `GetPath` com `havePath: false` procura o
  alvo a **1,5 → 3 → 6 → 12 m** (`:458-498`); enquanto o piso não está no mapa, o alvo é **redirecionado para o chão
  sob o piso**, o mover anda para o lugar errado, o `Partial` fica true e a trava de 12 s do `Goto` queima. Medido no
  run de 03/11/2026: **36 `no progress`**, 17 `no_support`, 9 `no_ground`, 6 `too_steep`.
- **A invalidação certa (P2, reduzida):** `m_pokeTime = Time.time` **e** `m_buildTime = Time.time - 5,1` (elegível no
  próximo tique, **sem** dar prioridade máxima e **sem** derrubar o throttle), só em tiles **que já existem**
  (`Contains`, nunca `GetNavTile`/`PokeTile`, que criam tile e disparam um bake de 32×32 m), coalescido **0,35 s**
  (`HashSet<Vector3Int>`), fail-open. `-1000` só com a coalescência; sozinho ele anula os 5 s e gera ~8.900 bakes
  (985 peças × 9 tiles) contra ~500 naturais, disputando a fila única com o Humanoid.
- **Ganchos:** postfix `WearNTear.OnPlaced` (pega jogador + companheiro: `BuildExecutor.PlaceDirect` chama
  `wear.OnPlaced()`) e prefix `WearNTear.Destroy`; **mais** as chamadas diretas em `BuildExecutor.PlaceDirect`,
  `ClearScaffolds` e `build.delete` (eles usam `ZNetScene.Destroy`, que **pula** o `WearNTear.Destroy` — sem isso a
  escada provisória fica como rampa fantasma).
- **Complemento (B):** no `MHR_Mover.Plan`, quando a rota é `Partial` e o alvo está acima, dar ~6 s (o intervalo do
  bake) antes de `Unstick`/`no progress`, restrito a `Build`/`BuildGather`.
