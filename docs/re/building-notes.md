# Construção — o que a sonda mediu (E0)

> Fase **E0** do plano `docs/plan-construcao.md`. Medido **em jogo** (Valheim 1.0.20, mundo `test3`,
> 01/11/2026) com o comando `modia_build_probe` e com as ferramentas `build.inspect` / `build.sites` do
> DevBridge — os três leem a **mesma** função (`AI/BuildProbe.cs`), então o log do jogador e o JSON do
> agente nunca divergem.
>
> **Por que este documento existe:** as flags de `Piece` (`m_groundOnly`, `m_noClipping`, `m_noInWater`…)
> **não estão publicadas em lugar nenhum** — nem no Fandom, nem na doc de modding: elas vivem no asset
> bundle. Sem esta medição, o validador da E2 seria chute. Foi o que o autor do BuildOrders também
> escreveu nas ideias dele ("`inspect <prefab>`: every component on a piece… read from the live game").

---

## 1. Como repetir

```
# no jogo (F5)
modia_build_probe piece_workbench 4        # ficha + 16 pontos (2 anéis x 8 direções) em volta do jogador

# pelo agente (MCP valheim-devbridge)
build_inspect { "prefab": "piece_workbench" }
build_sites   { "prefab": "piece_workbench", "radius": 4, "rings": 2, "dirs": 8 }
build_sites   { "prefab": "piece_chest_wood", "x": -279, "y": 81, "z": -271, "radius": 6 }
```

Saída do comando (log, mundo `test3`, 01/11/2026):

```
[Build] Sonda: piece_workbench (raio 3.0)
[BuildProbe] piece_workbench | Bancada | hammer=sim recipeKnown=sim itemDrop=nao | estacao=…
[BuildProbe] custo: Madeira x10
[BuildProbe] flags: noInWater=sim notOnTiltingSurface=sim noClipping=sim
[BuildProbe] espaco: spaceReq=0 blockRadius=0 blocking=(nenhuma) connect=(nenhum) biome=None rotate=sim remove=sim
[BuildProbe] wear: supports=nao material=Wood health=200 noSupportWear=sim
[BuildProbe] estacao: Bancada rangeBuild=20 requireRoof=sim requireFire=nao
[BuildProbe] 16 pontos em volta de (-279,7, 81,4, -271,5) raio 3,0: 11 valido(s)
```

---

## 2. A bancada (`piece_workbench`) — a ficha completa

| Campo | Valor | Consequência para a feature |
|---|---|---|
| `m_name` / `m_description` | `$piece_workbench` (token) | falar sempre com `Localization.instance.Localize(...)` |
| `m_resources` | **Madeira x10** (`$item_wood`) | o custo sai daqui, nunca de tabela nossa |
| `m_craftingStation` | **nenhuma** | a bancada é a primeira peça: não exige estação |
| `inHammer` / `recipeKnown` | sim / sim | o jogador já desbloqueou (gate da E3) |
| `m_noInWater` | **true** | não pode na água |
| `m_notOnTiltingSurface` | **true** | `normal.y >= 0.8` (não pode em encosta íngreme) |
| `m_noClipping` | **true** | **é ESTA a regra de "espaço" da bancada** (não pode encavalar outra peça) |
| `m_groundOnly` / `m_groundPiece` | false / false | pode em piso de madeira, não só no chão |
| `m_cultivatedGroundOnly` / `m_vegetationGroundOnly` | false / false | não exige terra arada |
| `m_notOnWood` / `m_notOnFloor` / `m_inCeilingOnly` | false | sem restrição de material/andar/teto |
| `m_waterPiece` / `m_clipGround` / `m_clipEverything` | false | — |
| `m_onlyInTeleportArea` / `m_onlyInBiome` | false / `None` | — |
| `m_allowedInDungeons` | **false** | não pode em masmorra |
| `m_allowedInDeepSnow` / `m_requireDeepSnow` | false / false | Deep North: só a regra de neve profunda (> 0.15) |
| `m_spaceRequirement` / `m_blockRadius` / `m_blockingPieces` | 0 / 0 / vazio | **não** é por aqui que a bancada exige espaço |
| `m_mustConnectTo` / `m_connectRadius` | null / 0 | não precisa de conexão |
| `m_canRotate` / `m_canBeRemoved` / `m_allowRotatedOverlap` | true / true / false | gira; pode ser removida |
| `m_comfort` / `m_comfortGroup` | 0 / `None` | não dá conforto |
| `WearNTear.m_supports` | **false** | a bancada **não sustenta** nada: não dá para construir em cima dela |
| `WearNTear.m_materialType` / `m_health` | Wood / 200 | — |
| `CraftingStation.m_rangeBuild` | **20 m** | o raio em que a bancada libera construções |
| `CraftingStation.m_requireRoof` / `m_requireFire` | true / false | **para usar** (craft), não para colocar |
| `CraftingStation.m_useDistance` | 2 m | distância de uso |
| `ItemDrop` / `m_placeEffect` | não / sim | não é item; tem efeito de colocação |

### O que a sonda **corrigiu** no plano

1. **A "condição de posicionamento" da bancada é `m_noClipping`, não `m_spaceRequirement`.** A v1 do plano
   suspeitava disso ("`m_spaceRequirement` só vale com `StationExtension`"); agora está medido. O teste de
   encaixe da E2 tem que instanciar o prefab desativado e usar `Physics.ComputePenetration` (é o que o
   vanilla faz em `TestGhostClipping`, `Player.cs` L4176).
2. **`WearNTear.m_supports` é lido na peça de BAIXO, não na que está sendo colocada.** No
   `UpdatePlacementGhost`, `wearNTear` vem do `piece` do **raycast** (a peça em que o fantasma está
   apoiado): `if (wearNTear && !wearNTear.m_supports) → Invalid`. A primeira execução da sonda usou o
   `WearNTear` da própria peça e **todos** os 16 pontos voltaram `no_support` — o bug foi encontrado pela
   própria sonda (§5). Consequência real: **não se constrói em cima da bancada** (nem em cima de qualquer
   mobília).
3. **A bancada não exige chão** (`m_groundOnly = false`): ela pode ser colocada num piso de madeira. Se a
   E2 só procurasse terreno, recusaria lugares válidos.

---

## 3. As peças da oficina mínima (medido, não suposto)

| Prefab | Nome (PT) | Custo (`m_resources`) | Estação | `supports` | Flags verdadeiras |
|---|---|---|---|---|---|
| `piece_workbench` | Bancada | Madeira x10 | — | **false** | noInWater, notOnTiltingSurface, **noClipping** |
| `piece_chest_wood` | Baú | Madeira x10 | `$piece_workbench` | **false** | noInWater, notOnTiltingSurface, noClipping |
| `fire_pit` | Fogueira | Pedra x5, Madeira x2 | — | true | noInWater, **notOnWood**, notOnTiltingSurface, noClipping, **allowedInDungeons** |
| `bed` | Cama | Madeira x8 | `$piece_workbench` | true | noInWater, notOnTiltingSurface, noClipping |
| `piece_bed02` | Cama de dragão | Madeira x40, Pele de cervo x7, Couro de lobo x4, Penas x10, Pregos de ferro x15 | `$piece_workbench` | true | noInWater, notOnTiltingSurface, noClipping |
| `wood_floor` | Piso de madeira 2x2 | Madeira x2 | `$piece_workbench` | true | **(nenhuma)** |

**Armadilha de nome (custou duas chamadas):** `piece_firepit` e `piece_bed` **não existem**. Os prefabs
são `fire_pit`, `hearth`, `bonfire` e `piece_bed02`. O `BuildCatalog` (E3) resolve por **apelido** →
prefab, e o apelido "fogueira" tem que cair em `fire_pit`; o nome do jogo (`Localization`) é o que o
jogador vê, e é diferente do prefab em vários casos.

**`wood_floor` não tem flag nenhuma:** o validador tem que aceitar "nada a checar" (o piso se apoia em
qualquer coisa que o raycast achar, inclusive numa parede). Um validador que exige `heightmap` recusaria
piso em cima de viga.

---

## 4. As regras de posicionamento do vanilla (ordem e origem)

`Player.UpdatePlacementGhost` (L3793) roda estas checagens; a sonda reproduz as que **não** precisam do
fantasma, na mesma ordem. Nomes de status da sonda ↔ campo do jogo:

| Status da sonda | Regra do jogo |
|---|---|
| `no_ground` | raycast não achou nada, ou `m_groundOnly`/`m_groundPiece` sem `Heightmap` |
| `in_water` / `no_water` | `m_noInWater` com o primeiro hit na camada `Water` / `m_waterPiece` sem água |
| `need_cultivated` | `m_cultivatedGroundOnly` + `heightmap.IsCultivated(point)` |
| `need_dirt` | `m_vegetationGroundOnly` + máscara de vegetação (AshLands inverte) |
| `not_on_wood` | `m_notOnWood` + `WearNTear.m_materialType` da peça de baixo (Wood/HardWood) |
| `too_steep` | `m_notOnTiltingSurface` + `normal.y < 0.8` |
| `not_ceiling` / `not_on_floor` | `m_inCeilingOnly` (`normal.y > -0.5`) / `m_notOnFloor` (`normal.y > 0.1`) |
| `no_teleport_area` | `m_onlyInTeleportArea` + `EffectArea.IsPointInsideArea(Teleport)` |
| `not_in_dungeon` | `!m_allowedInDungeons` + `Character.InInterior(point)` (y > 3000) |
| `deep_snow` / `no_snow` | `m_allowedInDeepSnow` / `m_requireDeepSnow` + máscara de cultivo |
| `wrong_biome` | `m_onlyInBiome` & `Heightmap.FindBiome(point)` |
| `no_build_zone` | `Location.IsInsideNoBuildLocation(point)` |
| `ward` | `PrivateArea.CheckAccess(point, raio, flash: false, wardCheck: false)` |
| `needs_station` | `m_craftingStation` + `CraftingStation.HaveBuildStationInRange` |
| `too_close_piece` | `m_blockRadius` + `m_blockingPieces` |
| `blocked_by_player` | jogador/companheiro a menos de **1,5 m** (`BuildProbe.MinSpacing`) |
| `no_support` | `WearNTear.m_supports` da **peça de baixo** |
| `valid` | nenhuma das acima |

**Ainda fora da sonda (entra na E2):** `m_noClipping` (`Physics.ComputePenetration` com o prefab
desativado), o snap (`Piece.GetSnapPoints`, 0,5 m) e a sobreposição de peças iguais
(`IsOverlappingOtherPiece`).

---

## 5. Evidência: a varredura de 16 pontos (mundo `test3`)

```
centro (-279,7, 81,4, -271,5) raio 3,0 -> 11 validos de 16
  r1 d0 OK              r1 d1 OK              r1 d2 blocked_by_player   r1 d3 blocked_by_player
  r1 d4 OK              r1 d5 OK              r1 d6 OK                  r1 d7 OK
  r2 d0 OK              r2 d1 no_build_zone   r2 d2 blocked_by_player   r2 d3 blocked_by_player
  r2 d4 OK              r2 d5 OK              r2 d6 OK                  r2 d7 OK
```

- **`blocked_by_player`**: o jogador está a ~2 m; é o `CheckPlacementGhostVSPlayers` do jogo.
- **`no_build_zone`**: há uma `Location` com `m_noBuild` a ~3 m (o `test3` tem um acampamento perto) —
  **não** é bug da sonda: é o jogo recusando.
- Todos os pontos com `chao=Terrain`, `heightmap=sim`, `normalY 0,87–1,00`, `queda=0,00`.

### O bug que a sonda pegou (e que teria ido para a E2)

Primeira execução: **0 válidos de 16**, todos `no_support`. Causa: a sonda lia o `WearNTear` **da própria
peça** em vez do da peça de baixo (§2.2). Corrigido em `BuildProbe.CheckPoint`; a segunda execução deu
**11 de 16**. É exatamente o tipo de erro que a fase 0 existe para pegar antes de escrever o validador.

### `piece_chest_wood` — a prova de que a checagem de estação funciona

```
build_sites piece_chest_wood (raio 4, 1 anel, 6 direções) -> 0 validos
  no_build_zone   [no_build_zone, needs_station]
  no_build_zone   [no_build_zone, needs_station]
  needs_station   [needs_station]  (x4)
```

Sem bancada por perto, o baú é recusado com `needs_station` — a mesma regra que o BuildOrders usa no
`HelperMayBuild` (`CraftingStation.HaveBuildStationInRange`).

---

## 6. O que a E0 entrega (estado)

| Item | Onde | Estado |
|---|---|---|
| Sonda compartilhada (ficha + pontos) | `src/Companions/AI/BuildProbe.cs` | ✅ |
| Comando `modia_build_probe [prefab] [raio]` | `src/Companions/AI/BuildProbeCommand.cs` | ✅ (medido no log) |
| `build.inspect` / `build.sites` no MCP | `DevBridge/Commands.cs` + `tools/mcp-devbridge` | ✅ (46 ferramentas, 47 checks) |
| Esta nota | `docs/re/building-notes.md` | ✅ |

**Pendências que a E0 deixou para a E2** (de propósito):

1. `m_noClipping` de verdade (prefab desativado + `Physics.ComputePenetration`), porque exige instanciar;
2. o snap de 0,5 m (`Piece.GetSnapPoints`) — só importa na E6 (estruturas);
3. `m_blockRadius`/`m_blockingPieces` está implementado, mas **nenhuma** das 5 peças medidas usa; fica
   coberto por teste quando aparecer uma peça que use (ex.: `piece_chest_private`? conferir na E5);
4. `Heightmap.GetCultivationMask` da neve usa o bioma do **ponto**, enquanto o vanilla usa o bioma
   **atual do jogador** (`Player.m_currentBiome`): diferença conhecida, irrelevante no Meadows e no Black
   Forest, a rever se alguém construir no Deep North.

---

## 7. E1 — o executor: as duas rotas (medido em 01/11/2026)

O `AI/BuildExecutor.cs` coloca a peça por uma de duas rotas, e **as duas passaram** no teste em jogo
(`modia_build_test piece_workbench auto|direct`, mundo `test3`):

```
[Build] Colocado: Bancada em (-224.4, 76.6, -265.7) | rota PlayerPlacePiece | criador 1738272368 | custo Madeira x10
[Build] OK: a peca esta no mundo, e do jogador, tem ZDO e a estacao entrou no registro do jogo
[Build] Colocado: Bancada em (-225.1, 76.8, -263.6) | rota Direct | criador 1738272368 | custo Madeira x10
[Build] OK: a peca esta no mundo, e do jogador, tem ZDO e a estacao entrou no registro do jogo
```

| prova | resultado |
|---|---|
| material | 40 → 20 de madeira (duas peças de 10) — **exato** |
| criador | `1738272368` (o id do jogador) nas duas rotas |
| relog (mata o processo → abre → entra) | `build.pieces` achou **2 bancadas**, `mine: true`, `zdo: true`, `health: 1` (100%), `station: $piece_workbench` |
| ponto inválido | recusado **antes** de instanciar (`no_support` ao tentar em cima da bancada recém-colocada — correto) |

### As duas armadilhas que só o teste mostra

1. **`CraftingStation.Start()` registra a estação no frame seguinte.** A primeira verificação (no mesmo
   frame do `PlacePiece`) disse `station_not_registered` nas duas rotas. Não era bug do executor: o
   `Start` do `CraftingStation` (L80) é quem faz `m_allStations.Add(this)`, e a Unity chama `Start` depois
   do `Instantiate`. **A verificação da estação tem que ser diferida** (o teste usa 2 frames; o job vai
   re-checar no `Update` seguinte).
2. **`MinSpacing = 1,5 m` recusava o ponto óbvio.** O teste coloca a peça a 1,5 m do jogador e o guarda
   barato também era 1,5 m → `blocked_by_player` (por arredondamento de float). No vanilla, colocar uma
   bancada a 1 m de você é normal: a regra real é a **colisão dos colliders do fantasma** com o jogador
   (`CheckPlacementGhostVSPlayers`). O guarda barato passou a **0,6 m**; o teste de colisão entra na E2.

### O que o executor **não** faz (de propósito)

- **não valida** a posição (o `PlacePiece` confia na pose; quem valida é a E2);
- **não cobra** o material (o `Consume`/`Missing` é do chamador, como na API do BuildOrders);
- **não decide** a peça (isso é o `BuildCatalog`, E3).

### Ferramenta nova do MCP: `build.pieces`

`build.pieces` (raio, `filter`) lista as peças **já no mundo** com prefab, nome, criador, `mine`, distância,
posição, `zdo`, vida e estação. Foi com ela que o relog foi provado de fora do jogo — e é ela que vai
conferir as construções do companheiro na E3. O servidor MCP passou a **49 ferramentas** e **53 checks**.

---

## 8. E3 — a ordem completa (medido em 01/11/2026)

```
[IA] Order from chat: "Maria, construa uma bancada de trabalho aqui"
[IA] Request #2 -> http://127.0.0.1:1234/v1/chat/completions (model "qwen2.5-3b-instruct")
[IA] Applied "build" to 1 companion(s) in 1085 ms
[IA] Voz (Fase 3.4): falando "Ok: construir Bancada"
[Build] maria: order: build piece_workbench near (-223,8, 77,0, -268,7)
[Build] maria: site at (-223,8, 77,0, -268,7), 0,0 m from the hint, 1 tried (valid x1)
[Build] maria: placed (PlayerPlacePiece), verifying next frame
[Build] maria: done: piece_workbench at (-223,8, 77,0, -268,7)
```

`build.pieces` achou a bancada nova **6 s** depois da mensagem (`mine: true`); pelo caminho do job sozinho
(`companions.build piece_workbench`, sem LLM) foram **2 s**. O `qwen2.5-3b-instruct` (LM Studio) acertou
`action:"build"` + `target` na primeira tentativa.

### O bug que só o jogo mostrou: o fantasma colidia consigo mesmo

A primeira ordem em jogo deu **0 de 48** candidatos válidos, todos `clipping`. Causa:
`Transform.IsChildOf(transform)` é **false para o próprio objeto**, então um collider no **root** do prefab
(a bancada tem um) era comparado com os colliders do próprio fantasma em `Physics.ComputePenetration` — o
fantasma "penetrava" a si mesmo em todo candidato. Correção em `BuildSite.Clips`:

```csharp
if (other.gameObject == ghost || other.transform.IsChildOf(ghost.transform)) continue;
```

Depois: `site at ..., 33 tried (clipping x32, valid x1)` na encosta e `1 tried (valid x1)` no plano — a
contagem por motivo (`tally`) é o que torna esse tipo de erro visível no log.

### O que a E3 **não** faz (por plano)

- **não coleta** o que falta (E4): paga da mochila, tudo ou nada;
- **não procura em baús** (E4: `BuildStock`);
- **não aplica** `[Building] MaxDistanceFromHome` no despacho da ordem (a chave existe; entra na E4/E5 com a
  casa);
- **não constrói** estruturas nem blueprints (E6–E8).

### As duas camas (medido com a sonda, 01/11/2026)

| prefab | nome | custo | estação | `recipeKnown` |
|---|---|---|---|---|
| `bed` | Cama | **Madeira x8** | `$piece_workbench` | **true** |
| `piece_bed02` | Cama de dragão | Madeira de qualidade x40, Pele de cervo x7, Couro de lobo x4, Penas x10, Pregos de ferro x15 | `$piece_workbench` | **false** (neste perfil) |

O alias "cama" caía na cama de dragão (a cara, travada) e a ordem respondia "não sei construir isso". O
`BuildCatalog` agora tem as duas: `bed` para "cama"/"bed"/"lit"/"letto" e `piece_bed02` só para
"cama de dragão"/"dragon bed". Medido em jogo: `done: bed` e `camas depois: 2`.

### E4 — a lição do movimento (01/11/2026)

A coleta embutida na ordem de construção **não pode** acontecer: o `MHR_Build` desligava o Follow, andava até o
recurso e devolvia o controle ao `HarvestController`; como o `MHR_Build.Drive` volta a rodar no frame seguinte e o
harvest também, os dois escrevem no mesmo `MHR_Mover` (tags diferentes) e o companheiro **embaralha o movimento**.
Regra que fica: **um dono do movimento por vez**. O desenho final (E4b): a ordem de construção **encerra** com
`hc_ai_build_collecting`, dispara a **ordem de coleta normal** (modo + teto = o que falta) e deixa um **vigia**
(`s_pending`) que **não dirige** — quando o custo está coberto ele limpa a coleta, volta ao modo Follow e começa
uma ordem de construção **nova** (que reencontra o local e caminha de volta). Medido: 3 madeiras coletadas e a
bancada no lugar em **136 s**; com mochila vazia (0 madeira), **96 s**.

**O raio:** a coleta mede o raio **do jogador** enquanto segue (25 m), e foi por isso que a busca parecia minúscula.
`[Building] GatherRadius` (**120 m**, máx. **200**) acha o recurso; a mais de 20 m o **job caminha** até ele em
modo Follow (coleta ociosa) e **só ao chegar** entrega a ordem — um dono do movimento por vez.

**Sem recurso no raio:** recusa **na hora** (`no resource within N m - refusing` → `hc_ai_build_missing`), sem
entregar a coleta nem esperar o prazo; e no estouro dos 300 s o vigia **fala** o que falta antes de encerrar.

**`companions.sethome`** (MCP): põe a casa onde o companheiro está (o mesmo `SetHomePosition` da roda) — foi como
a coleira `[Building] MaxDistanceFromHome` foi exercitada (`the spot is 4 m from the companion's home (max 1 m)`).

### A recusa de estação (medida a 140 m da oficina)

```
[Build] Companheiro 2: order: build piece_chest_wood near (-118,4, 46,5, -164,6)
[Build] Companheiro 2: needs a station: Bancada is not in range of the spot
[Build] Companheiro 2: order ended: hc_ai_build_needs_station (station out of range)
```

`build.pieces radius 45` = **0 bancadas**, 10 madeira na mochila, `baus depois: 0` — a checagem de estação roda
**antes** de pagar, então a ordem não custa nada. O `Finish` agora loga `order ended: <chave> (<motivo>)`: é o que
permite provar no log qual linha o companheiro falou (a fala em si vai para o balão/TTS).


---

## 9. A bancada em qualquer lugar, mas **usar** exige telhado (decompilado, 01/11/2026)

Ponto do jogador, confirmado no código do jogo 1.0.20: **construir** a bancada não exige nada além de chão
(flags `noInWater` / `notOnTiltingSurface` / `noClipping`, medidas na §1), mas **usar** a bancada (craftar item)
exige **telhado**. São duas checagens **diferentes** - e é por isso que a confusão aparece:

| onde | o que checa | telhado? |
|---|---|---|
| `CraftingStation.HaveBuildStationInRange(name, point)` (`CraftingStation.cs` L271) | nome da estação + `GetStationBuildRange()`; achata o `y` do ponto no `y` da estação | **não** |
| `CraftingStation.CheckUsable(player, showMessage)` (`CraftingStation.cs` L142) | `m_craftRequireRoof` -> `Cover.GetCoverForPoint(m_roofCheckPoint.position)` e exige `underRoof` **e** `coverPercentage >= 0,7`; `m_craftRequireFire` -> `m_haveFire` | **sim** (70% de cobertura) |

Quem chama o quê:

- **Colocar peça** (baú, cama, qualquer peça com `m_craftingStation`) -> `HaveBuildStationInRange`, a mesma função
  que o `BuildProbe.CheckPoint` usa para o motivo `needs_station`. **Sem telhado.**
- **Craftar item na bancada** (a janela do inventário) -> `InventoryGui` L1574 / L2164 / L2198 e
  `CraftingStation.Interact` L127 chamam `CheckUsable`. **Com telhado** (`$msg_stationneedroof` /
  `$msg_stationtooexposed`).

O asset da bancada: `m_craftRequireRoof = true`, `m_craftRequireFire = false`, `m_rangeBuild = 20`,
`m_useDistance = 2` (o `build.inspect` mostra os três; o `m_roofCheckPoint` é o ponto do prefab usado na cobertura).

**Consequência para o plano:**

- **E5** (oficina mínima) entrega as **quatro peças colocadas**: a bancada serve de estação para o baú e a cama
  **sem telhado** (é o caminho do `needs_station`) - é o que a ordem de construção tem de provar.
- **"Oficina funcional" no sentido do jogador** (craftar na bancada) precisa do **telhado** -> entra na **E6**
  (estruturas por template), e o primeiro template é justamente o **abrigo da bancada** (piso + 4 paredes + telhado).
- O companheiro **não** passa por `CheckUsable` em nada que ele faça hoje: os jobs dele não craftam em bancada.

## 10. E5 - as quatro ordens seguidas (medido em 01/11/2026)

Quatro ordens **pelo chat** (`qwen2.5-3b-instruct` no LM Studio), uma atrás da outra, com 40 madeira e 10 pedra na
mochila do companheiro (o material é E4; aqui só se prova o **posicionamento**):

```
[IA] Order from chat: "Companheiro, construa um(a) bancada de trabalho aqui"
[Build] site at (-197,9, 71,4, -239,3), 10,0 m from the hint, 15 tried (clipping x14, valid x1)
[Build] done: piece_workbench at (-197,9, 71,4, -239,3)         4 s
[IA] Order from chat: "Companheiro, construa um(a) bau aqui"
[Build] done: piece_chest_wood at (-197,9, 69,2, -249,2)        4 s   (bancada a 10 m: needs_station satisfeito)
[IA] Order from chat: "Companheiro, construa um(a) fogueira aqui"
[Build] done: fire_pit at (-198,2, 69,0, -249,3)                2 s
[IA] Order from chat: "Companheiro, construa um(a) cama aqui"
[Build] done: bed at (-188,2, 70,0, -249,3)                     6 s   (bancada a ~10 m)
```

**4/4, nenhuma falha de posicionamento**, todas terminando em `hc_ai_build_done`. O LLM devolveu `build` nas quatro
(532 / 1282 / 1355 / 1296 ms) e o `BuildCatalog` resolveu `bancada de trabalho` / `bau` / `fogueira` / `cama`.

**A mira é o `hint` da ordem** (`BuildFindCommand.Hint`, raio de 50 m): a primeira tentativa construiu a bancada a
**42 m** do jogador porque a câmera estava no horizonte. Para testar "aqui", a nova ferramenta do DevBridge
`player.look {down}` vira a câmera (usa o `Player.SetMouseLook` do jogo; **positivo = olhar para baixo** - o
`SetMouseLook` **subtrai** o y do delta, então o parâmetro vai negado).

**A particularidade da fogueira (`notOnWood`) exercitada:** com o ponto em cima de uma peça de **madeira** (a
fogueira que já estava lá - `WearNTear.m_materialType = Wood`), o validador devolve `not_on_wood` para a fogueira e
`valid` para o baú e a cama (que **não** têm a flag):

```
fire_pit          {'not_on_wood': 1, 'valid': 3}
   chao: fire_pit(Clone) | wearSupports: True | motivos: ['not_on_wood']
bed               {'valid': 4}
piece_chest_wood  {'valid': 4}
```

**A "falta de espaço" da cama é o `noClipping`, não o `m_spaceRequirement`:** o `build.inspect` mostra
`spaceRequirement = 0` para as três peças - o `m_spaceRequirement` do vanilla só vale para `StationExtension`
(`Player.cs` L3838). O espaço da cama é o teste de **encaixe do fantasma** (5 recusas `clipping` antes do ponto
válido no teste das 4 ordens), exatamente o mesmo caminho da bancada.

**A ficha das três peças novas** (`build.inspect`): baú = Madeira x10, `m_craftingStation` = Bancada, flags
`noInWater notOnTiltingSurface noClipping`, `supports=false`; fogueira = Pedra x5 + Madeira x2, **sem estação**,
flags `noInWater notOnWood notOnTiltingSurface noClipping allowedInDungeons`; cama = Madeira x8, estação Bancada,
`supports=true`. A **cama de dragão** (`piece_bed02`, 40 de madeira fina + 7 de couro + 4 de pele de lobo + 10 penas
+ 15 pregos de ferro) tem `recipeKnown=false` no mundo de teste - é o caminho da recusa `hc_ai_build_recipe_locked`.


---

## 11. E6 - a estrutura por template (medido em 01/11/2026)

**Os pontos de encaixe** (`build.snaps`, `Piece.GetSnapPoints` - os filhos com a tag `snappoint`), que são a
**fonte** dos deslocamentos do template (nada de chute):

| prefab | snaps (no espaço do prefab) | leitura |
|---|---|---|
| `wood_floor` (2x2) | (±1, 0, ±1) | origem no **centro** do piso, **topo em y = 0** -> a grade é de 2 em 2 m |
| `woodwall` | (±1, ±1, 0) | origem no **centro** de uma parede de 2 x 2 m -> sobre um piso ela fica **1 m acima** |
| `wood_roof_45` | (±1, ±1, ∓1) | sobe **2 m em 2 m** no eixo z, origem no centro; o **beiral** 1 m fora e a **cumeeira** 1 m dentro |
| `wood_pole2` | (0, ±1, 0) | 2 m, origem no centro |
| `wood_wall_roof_45` | (0, 2, 0) e (±1, 0, 0) | a empena: base em y = 0, topo em y = 2 |
| `wood_beam_45` | (-1, -1, 0) / (1, 1, 0) | viga em 45° |

**As medidas** (`build.inspect` -> `bounds`, dos renderers): `wood_floor` 2,27 x 0,26 x 2,29 (topo em +0,12);
`woodwall` 2,25 x 2,16 x 0,53; `wood_roof_45` 3,12 x 3,28 x 2,30 (o beiral explica o tamanho); `wood_pole2`
0,42 x 2,00 x 0,42; bancada 3,74 x 1,85 x 2,10 (a base no origin); baú 1,52 x 0,79 x 1,80; cama 2,25 x 0,74 x 3,78.

**Os dois templates** (`AI/BuildTemplates.cs`, puro e no harness - seção (j)):

- **`hut`** (cabana) = **1 piso + 4 paredes + 2 telhados** (7 peças) - o aceite da E6. Piso em (0,0,0), paredes a
  y = 1 nos quatro lados, telhados a y = 3 com 1 m de beiral e cumeeira 2 m acima das paredes.
- **`canopy`** (abrigo da bancada) = **4 postes + 4 telhados** (8 peças) - o telhado que o `CheckUsable` exige (§9).

**Aceite medido em jogo (cabana):** 7/7 peças, **todas `supported`**, todas pelo caminho **`Direct`**:

```
[Build] order: build plan hut (7 pieces) near (-202,0, 69,3, -251,7)
[Build] plan hut: 7 pieces, 6 poses checked, anchor at (-202,0, 69,3, -251,7)
[Build] done: hut at (-202,0, 69,3, -251,7) (1/7) supported          <- piso
[Build] done: woodwall at (-202,6, 70,3, -252,5) (2/7) supported     <- parede
...                                                                   <- 4 paredes
[Build] done: wood_roof_45 at (-202,6, 72,3, -252,5) (6/7) supported  <- telhado
[Build] done: wood_roof_45 at (-201,4, 72,3, -251,0) (7/7) supported
[Build] order ended: hc_ai_build_plan_done
```

### As três lições de jogo (todas custaram uma rodada)

1. **O apoio não existe antes das peças.** Validar o plano inteiro *antes* de construir recusou o telhado com
   `no_support` - as paredes em que ele se apoia ainda eram fantasmas. Agora o plano valida a **geometria** antes
   (ignorando `no_support` para nível > 0) e **revalida cada peça na hora de colocar**, com as anteriores de pé
   (`MHR_Build.Place`).
2. **O companheiro bloqueia a própria peça.** Ele anda até 2,5 m da peça para colocá-la, então a 5ª parede voltou
   `blocked_by_player` (ele mesmo dentro do raio de 0,6 m). `BuildProbe.CheckPoint` ganhou um **`ignore`** (o próprio
   companheiro); o jogador e os outros companheiros continuam contando.
3. **O âncora é do template, não da primeira peça.** Procurar o local só para o passo 0 pôs o abrigo **11 m** longe
   da bancada (o poste cabia ali, o resto não). O novo `FindAnchor` valida o **template inteiro** em cada candidato
   (o mesmo anel de busca, 3 anéis x 8 direções).

**O chão tem que ser plano.** O abrigo de 4x2 m precisa dos 4 postes no mesmo nível: num terreno inclinado os
postes do lado de baixo ficam sem apoio e o plano recusa (foi o que aconteceu sobre a bancada da E5, num barranco) -
o `FindAnchor` então procura outro ponto. Nivelar o terreno é a **E10**.

**O companheiro constrói sozinho (decisão do jogador, 01/11/2026).** O executor **não** usa mais o
`Player.PlacePiece` para o companheiro: a peça é criada pela sequência própria (`Instantiate` + claim +
`SetCreator` + `OnPlaced` + `IPlaced`), com o criador = o **dono** (o jogador) e o barulho saindo do **companheiro**
(`[Building] Quiet` silencia). O caminho do `Player.PlacePiece` dava o crédito, o barulho e as estatísticas de
`MaxBuildingHeight` ao **jogador** - e quem constrói é o companheiro. Ele fica só para um teste sem construtor.
