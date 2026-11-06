# Navmesh: qual agente o corpo usa (e por que `nav.tiles`/`nav.probe` mentem)

**Fonte:** decompilado `%TEMP%\opencode\vh-decomp\valheim` (Valheim 1.0.20) + a nossa árvore.
**Data:** 06/11/2026. **Escopo:** o booleano "o corpo deve navegar com o agente 40 (nosso) ou com o `Humanoid`?".
Tudo abaixo é **leitura de código**; o único harness rodado foi o da rodada anterior (shell negado a este subagente).

## 1. A chave de `m_tiles` — `(tileX, tileZ, agent)`

`Pathfinding.GetTile` (`Pathfinding.cs:797-802`): `x = Floor((px + size/2)/size)`, **`y = Floor((pz + size/2)/size)`** e
**`z = (int)agent`**. Confirmado nos três leitores: `BuildTile` (`:583` `(AgentType)tile.m_tile.z`), `RebuildLinks`
(`:673`) e `GetTilePos` (`:804-807`, usa `id.x`/`id.y` e fixa `y = 2500f`). Logo **o `key.z` é o agente** (o enum
`Pathfinding.AgentType`, `:27-42`), e o `key.y` é o **Z do mundo**.
**Sim, o mesmo tile 32×32 m pode existir para `Humanoid` e não existir para o 40** — são duas entradas independentes,
cada uma com seu `m_data`/`m_instance`/`m_pokeTime`/`m_buildTime`, e cada uma morre sozinha (`TimeoutTiles`, `:508-524`).

## 2. Quem lê `m_pathAgentType`

`BaseAI.m_pathAgentType` (`BaseAI.cs:60`) é usado **em dois lugares e só**:
`HavePath` (`BaseAI.cs:943`) e `FindPath` (`BaseAI.cs:960`) → argumento de
`Pathfinding.instance.GetPath/HavePath(..., m_pathAgentType)`. O `Character` não participa.
**Não existe `NavMeshAgent` no jogo:** `grep NavMeshAgent` no assembly inteiro = **0** ocorrências (só a referência
`UnityEngine.AIModule` no `.csproj`); o corpo anda por `Character.SetMoveDir` (`BaseAI.StopMoving`, `BaseAI.cs:932-935`).
Portanto **nada lê um `agentTypeID` de componente**: o filtro que vale é `GetSettings(agentType).m_build.agentTypeID`
(`Pathfinding.cs:368`, `:404`, `:473`, `:706`), isto é, **o id que o `NavMesh.CreateSettings()` devolveu** — que **não**
é o valor do enum (40 é só a chave do dicionário e o índice de `m_agentSettings`).
*Observação não testada:* `FindValidPoint` (`:326-341`) passa `agentTypeID = (int)settings.m_agentType` (`:332`), o valor
do **enum**, diferente de todo o resto — se o id do `CreateSettings` não for o do enum, ele nunca acha nada.

## 3. O bake do jogo: **um navmesh por (tile, agente), em runtime** — não há bake de cena

`NavMeshSurface` e `CalculateTriangulation`: **0** ocorrências no assembly. Todo o build é `Pathfinding.BuildTile`
(`:578-625`): `NavMeshBuilder.CollectSources(bounds 32×6000×32, m_layers)`, `NavMeshBuilder.UpdateNavMeshDataAsync(
tile.m_data, settings.m_build, …)` e, no fim, `NavMesh.AddNavMeshData(tile.m_data)` (`:648`).
Ou seja: **o jogo bakes o agente que a chave pedir** — `GetSettings((AgentType)tile.m_tile.z)` (`:583-584`). Não existe
"o jogo só bakeia o Humanoid"; existe "ninguém pokeia um tile de outro agente, então ele nunca nasce". O 40 é servido
pelo mesmo caminho (o nosso `BuildTile` prefix só tira as fontes de `Door`, `MHR_CompanionNavMesh.cs:153`).
**Custo:** um `UpdateNavMeshDataAsync` de `32×6000×32` por tile, e o `CollectSources` é síncrono na main thread
(medido 2,65 ms/tile no §8 de `navmesh-tiles.md`).

## 4. O `poke` do `PathingOnBuild` rebakeia **só o agente 40**

`PokeTile` grava `m_pokeTime` na **chave que recebe** (`:549-552`) e `PokeArea` monta as 9 chaves com o mesmo
`agentType` (`:526-541`). O nosso `Flush` só marca `z == (int)Agent` (`MHR_CompanionNavMesh.cs:401` e o filtro `:333`)
e só toca tiles que **já existem** (`:424`). Então: **nunca toca tile `Humanoid`**. E `Buildtiles` escolhe **1 tile por
tique de 0,1 s em todos os agentes juntos**, pelo maior `poke−build` (`:562-570`), com uma única `m_buildOperation`
(`:627-634`) — os nossos tiles **disputam a fila** com os `Humanoid` das criaturas.

**Isto explica a contradição medida**, junto com `Q1`: a chave `z==40` só existe onde **alguma rota com o 40** foi
pedida (`GetNavTile` **cria** ao poke, `:776-795`), e morre em 30 s sem poke (`:508-524`). `nav.tiles` = 1 tile no ponto
(é o que o nosso `Plan` pokeia a 1 Hz) e 0 na grade de 4 m (tiles vizinhos nunca pedidos); `nav.probe` = `false` onde o
jogador anda. **`found:false` não quer dizer "não dá para andar" — quer dizer "não há tile construído do agente 40 aqui"**.
Reforço: `GetPath` pokeia e **consulta em seguida** (`:352-365`) — tile recém-criado está **vazio**; a rota só pode
funcionar ≥ 5 s (gate de `m_updateInterval`) + 1 build assíncrono depois.

## 5. `Pathfinding.AddAgent` **não é** API do Unity

É método privado do próprio `Pathfinding` (`:170-186`): cria um `AgentSettings` cujo `m_build = NavMesh.CreateSettings()`
(`:68-72`) e **copia 4 campos** do "copy" (`agentHeight/agentClimb/agentRadius/agentSlope`, `:177-183`). O chamamos por
reflexão em `MHR_CompanionNavMesh.cs:101` (o campo `s_addAgent` é `:40`), com `Humanoid` como copy, e forçamos
`agentClimb = 0.6f` (`:104-105`).
`NavMesh.CreateSettings()` é API pública de runtime (docs Unity 6000.0: devolve id **positivo e único**) e não existe
`NavMesh.AddLinkAgent`; o "agente novo" é só uma entrada de settings — o navmesh dele vem de
`UpdateNavMeshDataAsync(..., settings.m_build, ...)` + `NavMesh.AddNavMeshData` (`:623`/`:648`), e **qualquer**
`NavMesh.SamplePosition/CalculatePath/Raycast` com esse `agentTypeID` o enxerga. **Não há bake de cena para "estar
depois"; o `AddAgent` no Postfix de `SetupAgents` é o ponto certo** (o `ClearAgentSettings`, `:140-154`, roda *dentro*
do original e apagaria o nosso se fôssemos antes).

## Decisão: **(B)**, com uma correção

A resposta é **(B)**, e não é uma questão de "geometria do agente errado": **não existe agente por componente** (0
`NavMeshAgent` em todo o assembly); `m_pathAgentType` só escolhe **qual mapa** o `FindPath` vanilla consulta. A prova de
que a diferença não é de geometria: o nosso agente copia `agentHeight 1.8` e `agentRadius 0.4` do `Humanoid`
(`MHR_CompanionNavMesh.cs:100-105` ← `Pathfinding.cs:191-195`); só mudam o `agentClimb` (0.6) e a remoção das fontes de
`Door`. Num vão de 1,86 m o veredito seria o **mesmo** nos dois → a contradição "1 tile × 0 tiles" é **cobertura e idade
de tile** (Q1 + Q4), não o mundo. E o `ApplyAgent` **não está com o agente errado**: ele governa só o fallback vanilla
(`BaseAI.FindPath`/`HavePath`), que deve usar o mapa **denso** das criaturas; pôr o 40 ali o mandaria para o mapa esparso
que só existe onde nós pokeamos.

**(B) em 3 linhas:** (1) `nav.tiles`/`nav.probe` devem **medir o `Humanoid`** (ou reportar os dois) — e o `Plan` do `MHR_Mover.cs:903` deve **cair para `Humanoid`** quando a rota com o 40 falhar, antes de virar "sem rota → linha reta" (hoje `AgentOrHumanoid`, `MHR_CompanionNavMesh.cs:33`, só cai quando o setup do 40 **falhou**). (2) O **40 não pode ser removido**: é o mapa que o próprio mover usa em 6 chamadas (`MHR_Mover.cs:903`, `:1336`, `:2225`, `:3350`, `:3372`, `:3793` — mais 2 no `MHR_ToolKeeper.cs:1247`/`:1318`) e o que faz portas e degraus de 0,6 m funcionarem (medido: 985/985). (3) Nada de `ApplyAgent`: `CompanionPrefabs.cs:257` / `CompanionAI.cs:372` / `MHR_Mover.cs:4072` (`Humanoid` + `HumanoidAvoidWater`) estão certos.

## O que **não** deu para verificar

- Shell negado a este subagente → **nenhum** harness/`nav.probe` novo; não medi `s_agentTypeId`, nem quantos tiles `40`
  existem num canto de jogo, nem se o vão de 1,86 m gera superfície para `agentHeight 1.8` (só a leitura de que os dois
  agentes têm o **mesmo** height/radius, logo o mesmo veredito).
- **Hipótese não medida do zigue-zague:** `SnapToNavMesh` (`Pathfinding.cs:458-465`) **sobe o `from` para a altura do
  terreno** quando ele está abaixo, e `GetPath` recusa `Partial` com corner sob o terreno (`:372-377`) — um corpo dentro
  do vão pode receber uma rota que **começa no lado de fora**, e andar "para fora" sem sair é o que se viu.
- `FindValidPoint` (`:332`, id = valor do enum) e o estouro do `m_buildOperation` único sob obra não foram testados.
