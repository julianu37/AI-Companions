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
