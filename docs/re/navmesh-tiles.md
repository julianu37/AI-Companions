# Navmesh por tile do Valheim 1.0 — inventário e invalidação (P2 do multi-build)

**Fonte:** decompilado `%TEMP%\opencode\vh-decomp\valheim\*.cs` (assembly_valheim do Valheim 1.0.20).
**Data:** 03/11/2026. **Nada aqui foi provado por harness nesta rodada** (o shell do subagente é negado):
tudo é leitura do decompilado, com `arquivo:linha`. O que depende do **prefab/scene** (valores serializados
de `Pathfinding`) está marcado como *não verificado*.

## 1. Inventário completo de `Pathfinding` (arquivo inteiro: 808 linhas)

| membro | assinatura | visibilidade | linha |
|---|---|---|---|
| `NavMeshTile` | classe aninhada privada | private | `Pathfinding.cs:8` |
| `NavMeshTile.m_tile` | `Vector3Int` | public (do tipo privado) | `:10` |
| `NavMeshTile.m_center` | `Vector3` | public | `:12` |
| `NavMeshTile.m_pokeTime` | `float = -1000f` | public | `:14` |
| `NavMeshTile.m_buildTime` | `float = -1000f` | public | `:16` |
| `NavMeshTile.m_data` | `NavMeshData` | public | `:18` |
| `NavMeshTile.m_instance` | `NavMeshDataInstance` | public | `:20` |
| `NavMeshTile.m_links1/2` | `List<KeyValuePair<Vector3, NavMeshLinkInstance>>` | public | `:22`, `:24` |
| `AgentType` | enum: `Humanoid=1, TrollSize, HugeSize, HorseSize, HumanoidNoSwim, HumanoidAvoidWater, Fish, HumanoidBig, BigFish, GoblinBruteSize, HumanoidBigNoSwim, Abomination, SeekerQueen` | public | `:27` |
| `AreaType` | enum: `Default, NotWalkable, Jump, Water` | public | `:44` |
| `AgentSettings` | classe aninhada privada (`m_agentType`, `m_build` `NavMeshBuildSettings`, `m_canWalk`, `m_avoidWater`, `m_canSwim`, `m_swimDepth`, `m_areaMask=-1`) | private | `:52` |
| `m_instance` | `static Pathfinding` | private | `:83` |
| `m_layers`, `m_waterLayers` | `LayerMask` | **public** (serializado) | `:85`, `:87` |
| `m_tiles` | `Dictionary<Vector3Int, NavMeshTile>` | private | `:89` |
| `m_tileSize` | `float = 32f` | **public** | `:91` |
| `m_defaultCost`, `m_waterCost`, `m_linkCost`, `m_linkWidth` | `float` (1/4/10/1) | public | `:93`–`:99` |
| `m_updateInterval` | `float = 5f` | **public** | `:101` |
| `m_tileTimeout` | `float = 30f` | **public** | `:103` |
| `m_tileHeight` / `m_tileY` | `const float 6000f` / `2500f` | private const | `:105`, `:107` |
| `m_updatePathfindingTimer` | `float` | private | `:109` |
| `m_queuedAreas` | `Queue<Vector3Int>` — **declarado e nunca usado** | private | `:111` |
| `m_linkRemoveQueue` / `m_tileRemoveQueue` | filas de remoção | private | `:113`, `:115` |
| `m_cachedTileID` / `m_cachedTile` | `Vector3Int(-9999999,…)` / `NavMeshTile` | private | `:117`, `:119` |
| `m_agentSettings` | `List<AgentSettings>` | private | `:121` |
| `m_buildOperation` | `AsyncOperation` | private | `:123` |
| `m_buildTile` | `NavMeshTile` | private | `:125` |
| `m_edgeBuildQueue` | `List<KeyValuePair<NavMeshTile, NavMeshTile>>` — **declarado e nunca usado** | private | `:127` |
| `m_path` | `NavMeshPath` | private | `:129` |
| `instance` | `public static Pathfinding instance => m_instance` | **public** | `:131` |
| `Awake` | `private void Awake()` | private | `:133` |
| `ClearAgentSettings` | `private void ClearAgentSettings()` | private | `:140` |
| `OnDestroy` | `private void OnDestroy()` | private | `:156` |
| `AddAgent` | `private AgentSettings AddAgent(AgentType type, AgentSettings copy = null)` | private | `:170` |
| `SetupAgents` | `private void SetupAgents()` | private | `:188` |
| `GetSettings` | `private AgentSettings GetSettings(AgentType agentType)` | private | `:261` |
| `GetAgentID` | `private int GetAgentID(AgentType agentType)` | private | `:266` |
| `Update` | `private void Update()` | private | `:271` |
| `DestroyAllLinks` | `private void DestroyAllLinks()` | private | `:288` |
| `DestroyQueuedNavmeshData` | `private void DestroyQueuedNavmeshData()` | private | `:296` |
| `UpdatePathfinding` | `private void UpdatePathfinding()` | private | `:315` |
| `HavePath` | `public bool HavePath(Vector3 from, Vector3 to, AgentType agentType)` | **public** | `:321` |
| `FindValidPoint` | `public bool FindValidPoint(out Vector3 point, Vector3 center, float range, AgentType agentType)` | **public** | `:326` |
| `IsUnderTerrain` | `private bool IsUnderTerrain(Vector3 p)` | private | `:343` |
| `GetPath` | `public bool GetPath(Vector3 from, Vector3 to, List<Vector3> path, AgentType agentType, bool requireFullPath = false, bool cleanup = true, bool havePath = false)` | **public** | `:352` |
| `CleanPath` / `FindNextNode` / `SnapToNavMesh` | auxiliares de rota | private | `:396`, `:446`, `:458` |
| `TimeoutTiles` | `private void TimeoutTiles()` | private | `:508` |
| `PokeArea` | `private void PokeArea(Vector3 point, AgentType agentType)` | private | `:526` |
| `PokePoint` | `private void PokePoint(Vector3 point, AgentType agentType)` | private | `:543` |
| `PokeTile` | `private void PokeTile(Vector3Int tileID)` | private | `:549` |
| `Buildtiles` | `private void Buildtiles()` (nome com "t" minúsculo) | private | `:554` |
| `BuildTile` | `private void BuildTile(NavMeshTile tile)` | private | `:578` |
| `IsBuilding` | `private bool IsBuilding()` | private | `:627` |
| `UpdateAsyncBuild` | `private bool UpdateAsyncBuild()` | private | `:636` |
| `ClearLinks` | `private void ClearLinks(NavMeshTile)` / `(List<…>)` | private | `:656`, `:662` |
| `RebuildLinks` | `private void RebuildLinks(NavMeshTile tile)` | private | `:671` |
| `ConnectAlongEdge` / `CompareLinks` | auxiliares de link | private | `:680`, `:716` |
| `SnapToNearestGround` / `FindGround` | auxiliares | private | `:732`, `:748` |
| `GetNavTile` | `private NavMeshTile GetNavTile(Vector3, AgentType)` / `(Vector3Int)` | private | `:770`, `:776` |
| `GetTile` | `private Vector3Int GetTile(Vector3 point, AgentType agent)` | private | `:797` |
| `GetTilePos` | `public Vector3 GetTilePos(Vector3Int id)` | **public** | `:804` |

**Não existe** (procurado no arquivo inteiro e por `grep` em todo o decompilado): `PiecePlaced`, `PieceRemoved`,
`Invalidate`, `InvalidateTile`, `RebuildTile`, `BuildAllTiles`, `m_buildCount`, `m_maxTilesPerFrame`,
`Start()`, `LateUpdate()`, coroutine de build. A única API pública que **invalida** algo é `FindValidPoint`
(que dá `poke` de 1 tile como efeito colateral).

## 2. Chave do dicionário e tempo

`Pathfinding.cs:797` — **o `z` do `Vector3Int` é o agent; o `y` é o Z do mundo**:

```csharp
private Vector3Int GetTile(Vector3 point, AgentType agent)
{
	int x = Mathf.FloorToInt((point.x + m_tileSize / 2f) / m_tileSize);
	int y = Mathf.FloorToInt((point.z + m_tileSize / 2f) / m_tileSize);
	return new Vector3Int(x, y, (int)agent);
}
```

Confirmado nos três lugares que leem a chave de volta: `BuildTile` (`:583`), `RebuildLinks` (`:673`) e
`GetTilePos` (`:804`, usa `id.x`/`id.y` e fixa `y = 2500f`). Ou seja: **o patch está certo** ao ler
`((Vector3Int)tile.m_tile).z` como o agent.

Sentinela: `-1000f` em `m_pokeTime` **e** `m_buildTime` (`:14`, `:16`) — é o estado "nunca poked / nunca built".
Depois de construir, o **chamador** grava `m_buildTime = Time.time` (`:574`, fora de `BuildTile` — então o
nosso Prefix devolver `false` **não** impede essa gravação).

## 3. Quem reconstrói (o rate limit inteiro)

`Pathfinding.cs:271`:

```csharp
private void Update()
{
	if (!IsBuilding())
	{
		m_updatePathfindingTimer += Time.deltaTime;
		if (m_updatePathfindingTimer > 0.1f)
		{
			m_updatePathfindingTimer = 0f;
			UpdatePathfinding();      // Buildtiles() + TimeoutTiles()
		}
		if (!IsBuilding())
		{
			DestroyQueuedNavmeshData();
		}
	}
}
```

`Pathfinding.cs:554` — a fila é o próprio dicionário; escolhe **um** tile por chamada (10 Hz):

```csharp
private void Buildtiles()
{
	if (UpdateAsyncBuild()) { return; }            // uma operação assíncrona por vez
	NavMeshTile navMeshTile = null;
	float num = 0f;
	foreach (KeyValuePair<Vector3Int, NavMeshTile> tile in m_tiles)
	{
		float num2 = tile.Value.m_pokeTime - tile.Value.m_buildTime;
		if (num2 > m_updateInterval && (navMeshTile == null || num2 > num))
		{
			navMeshTile = tile.Value;
			num = num2;
		}
	}
	if (navMeshTile != null)
	{
		BuildTile(navMeshTile);
		navMeshTile.m_buildTime = Time.time;
	}
}
```

Ou seja: **`pokeTime - buildTime > 5f`** (a `m_updateInterval`), ganha o de **maior** delta (sem `Sort`,
comparação estrita `>` → empate fica com o primeiro na ordem do dicionário), **1 tile por tique de 0,1 s**, e
nada é construído enquanto `m_buildOperation` não termina (`IsBuilding` gateia o `Update` inteiro, inclusive
`TimeoutTiles` e as remoções).

## 4. Quem invalida tile hoje: **ninguém**

`grep` por `BuildTile|m_tiles|PokeTile|m_buildTime` em **todo** o decompilado devolve **somente**
`Pathfinding.cs`. `grep` por `Pathfinding.instance` devolve **três** consumidores:

| chamador | linha | o que faz |
|---|---|---|
| `BaseAI.HavePath` / `BaseAI.GetPath` | `BaseAI.cs:943`, `:960` | rota de IA (`GetPath` → `PokeArea`) |
| `NavmeshTest.Update` | `NavmeshTest.cs:22` | objeto de teste |
| `SpawnAbility` | `SpawnAbility.cs:388` | rota para invocação |

E `GetPath` poked 3×3 (`Pathfinding.cs:352-356`):

```csharp
public bool GetPath(Vector3 from, Vector3 to, List<Vector3> path, AgentType agentType, …)
{
	path?.Clear();
	PokeArea(from, agentType);
	PokeArea(to, agentType);
	…
```

```csharp
private void PokeArea(Vector3 point, AgentType agentType)      // :526
{
	Vector3Int tile = GetTile(point, agentType);
	PokeTile(tile);
	for (int i = -1; i <= 1; i++)
		for (int j = -1; j <= 1; j++)
			if (j != 0 || i != 0)
				PokeTile(new Vector3Int(tile.x + j, tile.y + i, tile.z));
}

private void PokeTile(Vector3Int tileID)                        // :549
{
	GetNavTile(tileID).m_pokeTime = Time.time;                  // cria o tile se não existir
}
```

`ZoneSystem`, `Player`, `Piece`, `Door`, `CraftingStation`, `WearNTear`, `MineRock` e `Heightmap` **não**
tocam em `Pathfinding` — nem ao colocar peça, nem ao destruir, nem ao alterar o terreno (`TerrainModifier.PokeHeightmaps`
→ `Heightmap.Poke` → `Regenerate`, `Heightmap.cs:312`, sem navmesh). **O jogo não invalida a navmesh quando uma
peça entra ou sai**: o mapa se cura porque alguma IA pediu rota naquela área e o `m_updateInterval = 5 s`
reconstrói o tile.

Consequência prática: um tile **que está sendo consultado** é reconstruído no máximo a cada ~5 s, sempre.
Um tile **sem consulta** morre em 30 s (`TimeoutTiles`, `:508`, 1 remoção por tique).

## 5. `WearNTear`: os dois métodos existem, e nenhum fala com a navmesh

`WearNTear.cs:442`:

```csharp
public void OnPlaced()
{
	m_createTime = -1f;
	m_clearCachedSupport = true;
}
```

`WearNTear.cs:1290` (**privado**):

```csharp
private void Destroy(HitData hitData = null, bool blockDrop = false)
{
	Bed component = GetComponent<Bed>();
	if (component != null && m_nview.IsOwner() && Game.instance != null)
	{
		Game.instance.RemoveCustomSpawnPoint(component.GetSpawnPoint());
	}
	m_nview.GetZDO().Set(ZDOVars.s_health, 0f);
	m_nview.GetZDO().Set(ZDOVars.s_support, 0f);
	m_support = 0f;
	m_health = 0f;
	ClearCachedSupport();
	if ((bool)m_piece && !blockDrop) { m_piece.DropResources(hitData); }
	if (m_onDestroyed != null) { m_onDestroyed(); }
	…
	ZNetScene.instance.Destroy(base.gameObject);
}
```

Quem chama:

| método | chamadores | linha |
|---|---|---|
| `OnPlaced()` | **só** `Player.PlacePiece` | `Player.cs:3116` |
| `Destroy(...)` | `ApplyDamage` (HP ≤ 0) e `RPC_Remove` | `WearNTear.cs:1265`, `:1286` |
| `ApplyDamage(float, HitData)` | `UpdateWear` (desgaste) e `RPC_Damage` (dano) | `WearNTear.cs:690`, `:1222` |
| `Remove(bool)` | público → `RPC_Remove` → `Destroy` | `WearNTear.cs:1274` |
| `OnDestroy` (MonoBehaviour) | Unity, em **toda** destruição do GameObject | `WearNTear.cs:362` |

O `Destroy` privado **sempre** termina em `ZNetScene.instance.Destroy` (`:1328`) → `UnityEngine.Object.Destroy`
(`ZNetScene.cs:129`) → `WearNTear.OnDestroy` roda. Mas o caminho inverso **não** existe: remoção por
`ZNetScene.Destroy`, `ZDO` destruído, `forcedelete`/`build.delete` ou unload de zona chama `Object.Destroy`
direto e **pula** o `WearNTear.Destroy`.

## 6. Colocar peça por código: o que roda e o que não roda

`Player.PlacePiece` (`Player.cs:3099`) é o único lugar do jogo que dispara os ganchos de "colocado":

```csharp
GameObject gameObject = UnityEngine.Object.Instantiate(original, pos, rot);
…
gameObject.GetComponent<PrivateArea>()?.Setup(Game.instance.GetPlayerProfile().GetName());
gameObject.GetComponent<WearNTear>()?.OnPlaced();          // :3116
…
gameObject.gameObject.GetComponent<ItemDrop>()?.MakePiece(sendRPC: true);
m_placed.Clear();
gameObject.GetComponents(m_placed);                         // :3132
foreach (IPlaced item in m_placed) { item.OnPlaced(); }     // :3133-3136
```

O caminho de spawn por ZDO **não** chama nada disso: `ZNetScene.CreateObject` (`ZNetScene.cs:88-114`) só
`Instantiate`; `ZNetScene.RPC_SpawnObject` (`:447-458`) idem. Nesse caminho rodam apenas `ZNetView.Awake`
(`ZNetView.cs:43`, que **cria um ZDO novo** em `:88-106` quando não há `m_initZDO`) e `WearNTear.Awake`
(`:263`, cujo corpo só executa com `m_nview.GetZDO() != null`) + `Start` (`:338`).

O nosso executor **replica** o `PlacePiece` e chama o gancho explicitamente —
`BuildExecutor.cs:178-182`:

```csharp
WearNTear wear = go.GetComponent<WearNTear>();
if (wear != null)
{
	wear.OnPlaced();
}
```

Logo `WearNTear.OnPlaced` **cobre a peça do companheiro** (rota Direct) e a rota `Player.PlacePiece`
(`BuildExecutor.cs:111`) também. `MHR_Build` usa `BuildExecutor.Place` (`Jobs/MHR_Build.cs:1560`, `:2805`).

## 7. Receita de invalidação (o que dá para fazer sem `Player.PlacePiece`)

Não há API pública. O caminho mínimo, todo por reflexão sobre campos/métodos privados (o patch já faz isso):

1. `GetTile(Vector3, AgentType)` (`:797`) → devolve o `Vector3Int` certo (não duplicar a matemática de
   `m_tileSize`, que é campo serializado).
2. **Ler o tile do `m_tiles`** (dicionário privado, `:89`) com a chave do passo 1: **`TryGetValue`**. Não usar
   `GetNavTile` (`:776`) nem `PokeTile` (`:549`): o `GetNavTile` **cria** o tile quando ele não existe (entra no
   dicionário com `poke`/`build` em `-1000`) e o `PokeTile` já **cria e pokeia** (`poke = Time.time` contra
   `build = -1000` → delta de ~1000 s → **elegível no próximo tique**, um bake de 32×32 m por tile criado). Tile
   inexistente = **ignora**; o `GetPath` do jogo cria o dele quando alguém pedir rota por ali.
3. `m_pokeTime = Time.time` (mantém o tile vivo — `TimeoutTiles`, `:508`, mata em 30 s sem poke).
4. `m_buildTime = Time.time - (m_updateInterval + 0.1f)` → **`Time.time - 5,1f`** com o `m_updateInterval = 5f`
   default (`:101`): o delta de `Buildtiles` (`:132`) passa de 5 s e o tile fica **elegível no próximo tique de
   0,1 s** (`:271`). É o mínimo que o jogo permite sem furar o rate limit.
   **Não usar `-1000f`**: `pokeTime - buildTime` vira `Time.time + 1000` e **todo** tile invalidado vira
   elegível na hora, com prioridade máxima — 985 peças × 9 tiles ≈ **8.900 bakes** contra ~500 naturais, na fila
   única do `Buildtiles` (1 tile por tique), disputando com o Humanoid. `m_buildTime` no **futuro** faz o delta
   ficar negativo → **suprime** o rebuild.
5. **Coalescer** (0,35 s, `HashSet<Vector3Int>` de tiles pendentes) e **fail-open** (campo ausente = no-op + log).
   A coalescência é sobre o **número de escritas** (uma limpeza em lote repete o mesmo tile dezenas de vezes no
   mesmo tique) — ela **não** salva o `-1000`, que muda **o que** o jogo reconstrói, não só **quando**.

Alternativa sem reflexão para o passo 3: `FindValidPoint(out _, pos, range, (AgentType)40)` (`:326`) é
**pública** e poked 1 tile (não 3×3 como `PokeArea`) — mas não mexe em `m_buildTime` (o tile fica sujeito aos 5 s).

Custo: 1 rebuild de tile = `CollectSources` síncrono na main thread sobre um bounds de
`m_tileSize × 6000 × m_tileSize` + `UpdateNavMeshDataAsync` (`:623`). É **exatamente** o custo que o vanilla já
paga; a parte assíncrona fica fora da main thread e só uma roda por vez.

## 8. Não verificado

- `m_tileSize = 32f`, `m_updateInterval = 5f`, `m_tileTimeout = 30f` são **inicializadores de campo**; o
  componente `Pathfinding` vive numa cena e pode ter valores serializados diferentes. Conferir em jogo com
  `Pathfinding.instance.m_tileSize` (campo público). Um canteiro de 32 m cai em **1 a 4 tiles** dependendo do
  alinhamento com múltiplos de `m_tileSize`.
- Nenhum harness de reflexão rodou nesta rodada (shell negado ao subagente); nada foi medido em jogo.
- Custo real (ms) de um `CollectSources` de 32 m com uma casa de 983 peças.
- Se `m_layers` inclui a layer `piece` — **inferência forte** (o filtro de `Door` do nosso prefix só faz
  diferença se as portas entrarem na lista de sources), não leitura do prefab.
- Ordem multiplayer: se o cliente também roda `ZNetScene.Destroy` para peça de outro dono.
- `nav.tiles` filtra em **XZ** (o `m_center.y` do tile é 2500, o plano da grade); o JSON reporta o X/Z do tile
  com o y consultado, que é o que o jogador vê.
