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

**O abrigo deixa a bancada usável (medido):** a bancada sob o `canopy` responde
`usable=True cover=0.94 underRoof=True` (`CraftingStation.CheckUsable` + `Cover.GetCoverForPoint`), e as bancadas
descobertas ao lado respondem `usable=False cover=0.18` / `0.24` - a regra do telhado do §9, fechando o "usável" da
E5. O `build.usable` procura a bancada **mais próxima** do ponto: `HaveBuildStationInRange` devolve a primeira do
registro, e com duas no raio ele respondia pela errada (foi o que fez o primeiro teste parecer que o abrigo não
funcionava).

**Materiais de um plano (desenho do jogador, 01/11/2026):** a obra começa com o que a mochila tem e paga **peça por
peça** (mochila → baús usáveis, `BuildStock.Take`). Quando uma peça fica sem material, o `MHR_Build` **entrega a ordem
à coleta** (`HandOver` + `Pending.Template`) e o **vigia** (`CheckPending`) rearma a coleta até o **total do template**
estar coberto (`TemplateMissing`), então **retoma a obra sozinha**. O antigo "o plano exige tudo antes" saiu: numa
construção grande não faz sentido, e o bug do teste (nivelou, faltou madeira e **parou**) era exatamente isso.

**A retomada é no ponto exato (dois bugs achados no teste do jogador, 01/11/2026):**

1. **O vigia esperava o total da casa.** Depois de entregar "faltam 2 madeiras", ele comparava com o **custo inteiro**
   do template (123) - a mochila nunca chegava lá e a ordem estourava os 300 s ("gathering took too long") com as 2 já
   no saco. Agora ele espera **o que faltou** (`Pending.Amount` do item) e retoma na hora.
2. **A retomada recomeçava do zero.** `StartPlan` refazia a busca de âncora e começava no passo 0 - o jogador viu uma
   **segunda bancada** e uma **casa nova ao lado** ("ele cria outra workbench e começa uma casa do zero"). Agora o
   `Pending` guarda **o índice da peça e a âncora** (`Index`/`Anchor`/`AnchorRot`) e o `StartPlan(..., anchorKnown:
   true)` **continua da mesma pose e da peça onde parou** (`resuming at the same anchor ... from piece N`).

**A coleta de um plano pede o que falta para a CASA TODA (pedido do jogador, 02/11/2026):** se ele tem que cortar
madeira de qualquer jeito, volta com o suficiente para o resto da obra (ou até a mochila encher) em vez de uma peça
por vez. `BeginGather` usa o **total do template** (`TemplateMissing`) para o item; o vigia retoma quando o número é
atingido **ou** a mochila está a **80%** do limite (`CompanionCarry.Max`) - o total pode não caber no saco.
**Onde o material foi pego agora aparece no log** (`[Build] stock: took Nx <item> from the bag|the chest "..." (N m
from the spot)`) - antes era silencioso e o jogador (com razão) duvidou do baú.

**Antes de sair para coletar, ele RECOLHE DOS BAÚS (pedido do jogador, 02/11/2026):** o jogador viu uma pilha de 50
(da qual ele deixou 8) e outra de 30 intocada enquanto o companheiro ia cortar madeira. A obra pagava peça por peça
(pegando só o que a peça precisava) e o cálculo da coleta contava o baú, mas o material ficava lá. Agora o
`BuildStock.WithdrawTemplate` **move para a mochila** o que os baús têm, até o total da estrutura, **antes** de
`TemplateMissing` decidir o que coletar - e a linha `[Build] stock: moved Nx <item> from the chest "..." to the bag`
mostra cada retirada. O que sobra no baú continua lá (a obra usa nas peças seguintes).

**O número da coleta é o RESTO do plano (bug do teste, 02/11/2026):** o `PlanMissing` contava o template inteiro e
pedia **122** madeiras quando faltavam ~75 (24 de 57 peças já no chão). Agora ele recebe o **índice da peça** e só soma
do que ainda falta construir (`PlanMissing(bag, plan, startIndex, ...)`); o `TemplateMissing` continua valendo para a
primeira peça (nada construído = plano inteiro). **A fala segue o mesmo número:** `hc_ai_build_collecting` usa
`state.GatherItem x state.GatherAmount` (o que a coleta pediu) em vez do buraco da peça - o jogador ouviu "Vou buscar
1 madeira" enquanto a ordem era de 75.

**O chão tem que ser plano.** O abrigo de 4x2 m precisa dos 4 postes no mesmo nível: num terreno inclinado os
postes do lado de baixo ficam sem apoio e o plano recusa (foi o que aconteceu sobre a bancada da E5, num barranco) -
o `FindAnchor` então procura outro ponto. Nivelar o terreno é a **E10**.

**O companheiro constrói sozinho (decisão do jogador, 01/11/2026).** O executor **não** usa mais o
`Player.PlacePiece` para o companheiro: a peça é criada pela sequência própria (`Instantiate` + claim +
`SetCreator` + `OnPlaced` + `IPlaced`), com o criador = o **dono** (o jogador) e o barulho saindo do **companheiro**
(`[Building] Quiet` silencia). O caminho do `Player.PlacePiece` dava o crédito, o barulho e as estatísticas de
`MaxBuildingHeight` ao **jogador** - e quem constrói é o companheiro. Ele fica só para um teste sem construtor.


---

## 12. E6b - a casa simples (medido em 01/11/2026)

**Aceite:** 57/57 peças, **todas `supported`**, todas `placed (Direct)`, no terreno nivelado pelo jogador:

```
plan house: 57 pieces, 56 poses checked, anchor at (-279,3, 81,7, -299,0)
done: house ... (1/57) supported                  <- a bancada da propria casa
done: wood_floor ... (2..17/57) supported         <- 16 pisos
done: woodwall ... (18..32/57) supported          <- 14 paredes
done: wood_wall_half ... (19/57) supported        <- a JANELA (peitoril de 0,5 m)
done: wood_roof_45 ... (33..48/57) supported      <- 16 telhados (a fileira de cima a y = 86,7)
done: wood_wall_roof_45 ... (49..56/57) supported <- 8 empenas
done: wood_door ... (57/57) supported             <- a PORTA, por ultimo
order ended: hc_ai_build_plan_done
```

`build.pieces` no local: **16 pisos, 14 paredes, 1 porta, 1 meia-parede, 16 telhados, 8 empenas e 1 bancada** = 57,
todas `mine=True`, `zdo=True`, `health=1.00`.

### As quatro coisas que o teste da casa ensinou

1. **A porta tem que ser a ÚLTIMA.** Com as quatro paredes de pé e a porta no lugar, o companheiro fica **trancado
   dentro** e não alcança o telhado ("the order took too long" em 33/57). A porta é nível **3** no template; o vão
   aberto deixa ele entrar e sair durante a obra.
2. **O alcance é o do jogo: 5 m do OLHO** (`Player.m_maxPlaceDistance = 5f`), não 3 m acima dos pés. A fileira de
   cima do telhado fica a **4,9 m** dos pés (y = 5) e era inalcançável - o companheiro andava em círculos até o
   prazo ("parece que não alcança", relato do jogador).
3. **A casa traz a própria bancada** (primeira peça, canto de dentro): toda peça de madeira precisa de estação no
   raio, e uma bancada do jogador **no meio do terreno** atrapalha o piso ("a bancada atrapalhou"). Com a bancada
   como **passo 0 do plano**, a casa se constrói sozinha - e o planejamento passou a aceitar `needs_station` como
   "a estação vem do plano" (a checagem real é no `Place`, com a bancada já de pé).
4. **Madeira pesa 2** (não 1): 160 unidades = **320/300** de peso, o companheiro fica `Encumbered` e o `MHR_Mover`
   desiste em 8 s. A casa custa **123 unidades (246 de peso)** - cabe, mas com pouca folga.

**Custo da casa:** 123 unidades de madeira (16x2 pisos + 14x2 paredes + 10 bancada + 1 meia-parede + 16x2 telhados +
8x2 empenas + 4 porta). **Tempo:** a resolução do plano leva ~35 s (56 poses com o fantasma) e a obra ~5-8 min.


---

## 12.1 E6c - a entrega da coleta: Follow, guarda da base e o prazo (medido em 02/11/2026)

**A causa raiz estava no Follow, não na coleta.** O modo madeira **mantém o Follow ligado** de propósito
(`MHR_JobRules.KeepsFollow`: com follow ele trabalha "o que estiver perto de você e senão te segue", o comportamento
da roda/chat do upstream). Com Follow ligado o harvest só enxerga `[Gathering] FollowRange` (**25 m**) ao redor do
**jogador** — e no canteiro de uma casa nova a árvore mais próxima estava a **1,3 m** de uma peça construída, então o
guarda da base (`BaseProtectRadius`, 10 m) a recusava **em silêncio** (`AllowTarget` → `NearBuildings` → `OtherRejects`,
que não loga). Resultado do teste: a ordem entregava a coleta e o companheiro ficava em Follow, sem nada para cortar,
até o prazo de 300 s.

**As cinco correções (todas medidas no teste em jogo):**

1. **Follow desligado na entrega** (`HandOver`): `cs.SetFollow(false)` e um `ApplyFollowMode(mode, force: true)` logo
   depois, porque a escrita do Follow passa por `MHR_WriteOrQueue` e pode ficar na fila. Com Follow off o harvest
   trabalha a **área do canteiro** (`[Gathering] Range` = 60 m, âncora em `UpdateAnchor`), que é o que uma obra
   precisa. O valor anterior vai em `Pending.FollowWas` e o `RestoreFollow` devolve o Follow em **todas** as saídas:
   retomada, prazo, sem-recurso, troca de trabalho e `MHR_Build.Cancel` (outra ordem chegando). Só devolve se ainda
   estiver desligado (o jogador pode ter ligado na mão).
2. **A caminhada do build não escolhe árvore protegida:** `MHR_GatherRules.GatherRefusesTree` (hoje = guarda da base)
   filtra o `NearestResource`, com a linha `[Build] N tree(s) skipped: too close to the buildings (base guard)`.
   Antes ele andava até a árvore e a coleta a recusava.
3. **O prazo deixou de ser relógio fixo:** `Pending.LastProgressAt` é atualizado a cada madeira que **entra** e o prazo
   de 300 s conta desde o último progresso (`gathering made no progress for 300 s`). O teste perdeu **36/39** porque o
   relógio fixo cortou uma coleta que estava funcionando.
4. **Vigia de ociosidade:** se o harvest fica **20 s** sem nada para fazer (e não está em combate nem pausado por
   `CompanionRest.MHR_ShouldPauseWork`), a ordem encerra dizendo o que falta - com o **resto do plano** (`PlanMissing`
   a partir do índice), não o template inteiro - em vez de esperar 300 s calado. O aviso de vigia é 1×/min.
5. **Ao encerrar, volta ao modo Follow** (`ApplyActionMode(ModeFollow)`): antes ele ficava em **modo madeira** sem
   alvo e continuava trabalhando troncos no caminho de volta - o relato "não parou a coleta".

**Aceite em jogo (02/11/2026):** o jogador testou e respondeu *"agora parece que funcionou perfeitamente"*. O log da
ordem que fechou a casa, na íntegra:

```
[Build] withdrew 1 item(s) from the chests before gathering
[Build] gathering 15x $item_wood (mode 1) - the build resumes after it
[Build] gather done (have 15/15) - resuming
[Build] follow back on
[Build] resuming at the same anchor (-105,8, 35,6, -391,7) from piece 51
[Build] next: wood_door (57/57)
[Build] done: wood_door at (-109,7, 36,6, -393,1) (57/57) supported
```

Ou seja: o baú foi usado antes de sair, o número pedido era o **resto** (15, não o template de 123), ela **parou em
15/15**, o Follow voltou, a obra retomou **no mesmo âncora a partir da peça 51** e a **porta foi a última** das 57
peças. No teste anterior (antes destas correções) a contagem tinha chegado a **4 → 36/39** cortando faia, bétula e
troncos e só travou num tronco fora de alcance (`Its swings aren't reaching "beech_log_half"`, 27-32 m) - que com o
prazo por progresso não derruba mais a obra.

**O custo da casa, conferido com `build.inspect`:** **123 madeiras** (246 de peso) - bancada 10, 16 pisos 32, 14
paredes 28, janela (meia parede) 1, 16 telhados 32, 8 empenas 16 e porta 4. Com o limite de 300 e a regra dos 80% da
mochila, uma casa pode exigir **duas viagens** de coleta quando ele já está carregado.

## 14. E6d - a tenda, a casa 4x4 e a casa 6x6 (medido em 02/11/2026)

Pedido do jogador: faltavam na E6 a **tenda**, o **barraco** e as construções **4x4** e **6x6**.

| template | nome na ordem | peças | madeira | medido |
|---|---|---|---|---|
| `tent` | "tenda", "toldo", "barraca" | 4 (2 postes + 2 telhados, 1 água) | 8 | **4/4 `supported`** |
| `hut` | "cabana", **"barraco"**, "choupana" | 7 | 22 | já medido (E6) |
| `canopy` | "abrigo da bancada" | 8 | 12 | já medido (E6) |
| `house4` | "casa 4x4", "casa pequena" | 21 | 51 | **bancada no anexo fora**, colocada primeiro (a estação cobre a casa) |
| `house6` | "casa 6x6", "casa média" | 46 | 101 | **46/46 `supported`** (`plan_done`) |
| `house` | "casa", "abrigo" | 57 | 123 | já medido (E6b) |

**A casa é uma função do tamanho** (`HouseSteps(tiles, doorSlot, windowSlot, roofAlongZ)`): 2x2 tiles = 4x4,
3x3 = 6x6, 4x4 = 8x8. Pisos, paredes, porta (última), janela, telhado e empenas saem da mesma receita; o que muda é o
telhado, porque a água de 45° cobre 2 m:

- **4x4** (meio vão 2 m): 1 peça por água, beiral **na** parede, as duas peças fecham na cumeeira. A bancada (3,7 x
  2,1 m) não cabe no cômodo de 4x4, então ela vai num **anexo fora**, encostada na parede direita (4,2 m do centro) e
  **primeiro passo** do plano - a estação cobre a casa toda (o canto mais longe fica ~6,3 m; o alcance do jogo é 8).
- **6x6** (meio vão 3 m): 2 peças por água com o beiral **1 m além da parede** (escolha do jogador) e **4 postes de
  2 m** em cada canto do beiral, porque telhado 1 m para fora da parede não tem onde apoiar. Medido: 46/46.
- **8x8** (meio vão 4 m): 2 peças por água, beiral na parede - o desenho original da E6b, intacto.

**A tenda** é um toldo de 1 água de 4x2 m: 2 postes de 2 m seguram a aresta alta e as 2 peças de telhado descem até o
chão 2 m à frente. Os postes ficam **nos snap points** da aresta alta (x = ±2), que é de onde vem o apoio das peças.

**Três correções que vieram do teste:**

1. **A bancada do 6x6 fica na MESMA posicao da 8x8** (canto dos fundos). A primeira tentativa foi encostar a
   bancada na parede esquerda, girada 90 graus, para nao atravessar a porta - mas ai ela ficou **em cima de uma peca de
   piso** e aquele piso era recusado com `no_support` (`ground=collider`, o tampo da bancada a 1,17 m). O que resolveu o
   corredor foi mover a **porta do 6x6 para a vaga da direita** (`doorSlot: 2`).
2. **O telhado do 6x6 e girado** (`roofAlongZ: true`, pedido do jogador: "rotaciona o telhado da 6x6"): a cumeeira
   corre em **z** e a **empena (triangulo) fica em cima da porta**. As empenas estavam com o **yaw trocado** e entravam
   como asas ("as paredes 45 parecem invertidas"): no lado +x a empena sobe para **-x**, que e **yaw 180** (yaw 0 sobe
   para +x). Corrigido, o jogador respondeu **"tudo certo!"**.
3. **A trava de 90 graus no yaw do plano foi tentada e REVERTIDA.** O plano usava o yaw livre da mira (348,6 no teste)
   e os pisos entravam tortos; travar em 90 graus parecia a correcao, mas a casa saiu **deitada/torta** na pratica e o
   jogador pediu para desfazer. O plano voltou a usar o yaw da mira, **igual a 8x8** (que sempre funcionou).
4. **O anel de procura de terreno caiu de 4 m para 1,5 m** ("ele foi em outro terreno"): a casa ia para ate 4 m
   (nivelamento) + 8 m (ancora) do ponto que o jogador mirou.
5. **Peca de nivel 0 tem que ter apoio no chao na validacao do plano.** Os postes do beiral, quando empilhados (paredes
   de 4 m), precisam entrar no nivel da fiada de cima - no nivel 0 a validacao exige apoio no chao e a busca de ancora
   falha inteira (`no_support`, que o jogador ouviu como "nao tem espaco" num lugar onde a 8x8 cabia).

**Aceite em jogo (02/11/2026):** com as empenas no yaw certo, o jogador testou a 6x6 e respondeu **"tudo certo!"**.
A casa e 6x6 com 46 pecas / 101 madeiras (2 m de parede, beiral de 1 m com 4 postes, bancada, porta e janela).

**Armadilha de apelido (custou um teste vermelho):** o Fold tira os acentos, então `"casa pequeña"` (ES) e
`"casa pequena"` (PT) viram **a mesma string** - o `Resolve` empata e o primeiro template da lista ganha. A casa 8x8
tinha `"casa pequeña"` e a 4x4 `"casa pequena"`; a 4x4 nunca casava. O apelido ES saiu da 8x8 (ficou `"casita"`) e o
harness passou a cobrir os dois idiomas.

## 15. E7 - construindo os planos do jogador: o `.blueprint` (medido em 02/11/2026)

A biblioteca grande de builds do Valheim e a do **PlanBuild**: 353 blueprints do **BiomeBlueprints**, o acervo do
**valheimians** e a captura do proprio jogo. O companheiro passou a **ler esses arquivos** e construir com o executor
da E6 - **sem** precisar do PlanBuild instalado (o PlanBuild so serve para *criar* ou *colocar* o fantasma).

### 15.1 O formato (texto, nao JSON)

```
#Name: Meadow Hut
#Creator: ...
#Category: Blueprints
#SnapPoints
<linhas de snap>
#Terrain
<linhas de terreno>
#Pieces
<prefab>;<categoria>;<px>;<py>;<pz>;<rx>;<ry>;<rz>;<rw>;<info>;<sx>;<sy>;<sz>
```

- numeros com **ponto** (arquivos antigos com virgula sao aceitos: a virgula vira ponto antes do parse);
- **V1** (captura antiga) nao tem cabecalho: todas as linhas sao pecas;
- secoes desconhecidas (`#TerrainHeight` de outro mod) sao **puladas** ate o proximo cabecalho conhecido.

**O que lemos:** `#Name:` (o nome bonito) e a secao `#Pieces`. `#SnapPoints` e `#Terrain` ficam de fora na v1 (o
jogador nivela antes com a ordem `level`, se quiser).

### 15.2 O que o executor faz com isso

- **Qual nome ganha (teste do chat, 02/11/2026):** o **nome mais longo** entre o template e o arquivo. O 3B
  respondeu `target: "hut"` para "construa a hs_meadows_hut aqui" e o template da cabana ganhava; agora o arquivo
  (`hs_meadows_hut`, 15 caracteres) vence o alias (`hut`, 3) e, no empate, o template embutido ganha (um arquivo
  chamado "casa" nao toma o lugar de "construa uma casa").
- **Custo por mensagem:** a pasta e listada com **cache de 10 s** e o casamento e pelo **nome do arquivo** (sem abrir
  353 arquivos por mensagem); o `#Name:` interno so e lido quando a pasta tem ate 25 builds.
- **Prefab:** `BuildProbe.PieceOf` (exato e, se falhar, **sem caixa** - os arquivos do BiomeBlueprints escrevem
  `Piece_grausten_floor_2x2` enquanto o jogo tem `piece_grausten_floor_2x2`).
- **Nao e peca** (item, rug, mobilia de outro mod) ou **receita nao desbloqueada:** a linha e **pulada e contada** (uma
  build baixada nao pode ser recusada por causa de uma linha). So recusa quando **nada** e construivel.
- **Ordem:** de baixo para cima (`y` crescente, estavel no empate) - piso, parede, telhado.
- **Posicao:** o blueprint e ancorado **no ponto mirado**, girado pelo yaw da mira (como os templates).
- **Material:** mochila -> baus -> coleta (o mesmo `BuildStock`), com retomada no ponto exato. `[Building]
  BlueprintFree` (padrao **false**) constroi sem pagar, para teste.
- **Peca que nao cabe/paisagem recusa:** e **pulada** (`state.Skipped`), nao derruba a build; o fim diz
  `{colocadas}` e `{puladas}`.
- **Estacao:** a regra do jogo vale (uma build baixada **nao traz** bancada) - o `needs_station` diz qual estacao falta.
  Na pratica: coloque uma bancada perto antes (ou mande o companheiro construir uma).

### 15.2b As licoes do teste com uma build da comunidade (Nexus 1807, 985 pecas)

O jogador testou o `Community starter home` (MutantArtCat, `.blueprint` de 2022, 985 pecas). Quatro problemas que so
aparecem num arquivo grande e antigo - todos corrigidos:

1. **Ordem de construcao:** ordenar so por altura pegou um `wood_wall_roof_top_45` como "primeira peca" (a origem do
   arquivo fica **abaixo** da obra, entao a peca mais baixa era um topo de telhado). Agora a ordem e por **classe**
   (`BuildBlueprintParser.ClassOf`): piso/bancada (0) -> parede/viga/poste/porta/mobilia (1) -> telhado/empena (2), e
   dentro da classe por altura.
2. **Ancora:** a origem do arquivo e **onde o autor estava**, nao o meio da obra - a casa de 30x30 m era ancorada num
   canto. Agora o **footprint e centrado no ponto mirado**.
3. **Nivelar primeiro** (pedido do jogador): o caminho do `.blueprint` ia direto para a primeira peca, e num desnivel de
   2,7 m os pisos eram recusados um a um. Agora ele entrega o nivelamento ao job da enxada e **retoma sozinho**
   (`MHR_Level.Start(..., arquivo, blueprint: true)` -> `StartBlueprint` no fim). Duas armadilhas medidas:
   o nivelador trabalha no maximo **20 m** e medir 32 m gerava **loop** ("leveling first" -> "already flat" -> de novo);
   agora a medicao usa a **mesma area** e uma segunda tentativa seguida **prossegue** (as pecas que nao couberem sao
   puladas). Com desnivel acima do limite o job responde `hc_ai_build_too_steep` (o jogador precisa de um planalto).
4. **Nada derruba a build:** peca que **nao da para alcancar** em 60 s (`took too long`), que o caminho nao chega
   (`can't reach it`) ou que **nao materializa** (`verification failed: not_found`) e **pulada e contada** com a pose no
   log - antes a ordem morria na peca 2, na 50, ou no primeiro canto impossivel.
5. **Estacao antes da primeira peca:** o blueprint nao traz bancada, e a ordem morria na 2a peca com `needs_station`.
   Agora a checagem e **antes de comecar** (a menos que o proprio arquivo traga uma bancada) e a fala diz qual estacao
   falta.

**A build de 985 pecas, depois das correcoes (02/11/2026):** `construa CommHomeBasicBP` -> **pisos subindo de verdade**
(76/983 no log, todos `supported`, sem loop de nivelamento) e, quando a madeira acabou, a coleta assumiu pedindo o
**total que falta para a casa: 2293**. O vigia retoma a cada vez que a mochila enche (a casa inteira nao cabe no saco),
como nas estruturas.

### 15.2c Retry passes - o "constroi o que fica de pe e volta para o resto" (03/11/2026)

O `Next` do **BuildOrders** devolve so as pecas **que vao ficar de pe** com o que ja existe (a estabilidade deles).
Nos nao precisamos da simulacao: usamos o **veredito do proprio jogo** (a regra da E6) e **retentamos**. Toda peca de
blueprint que nao fica de pe nesta passada - `no_support`, `can't reach it`, `took too long` ou
`verification failed: not_found` - vai para a fila (`State.Retry`) em vez de ser perdida; quando o plano termina, a fila
e anexada e tentada de novo (`MaxPasses = 3`). O que continuar falhando na ultima passada e **pulado e contado**.
Log: `retry later: <peca> (<motivo>) at (x, y, z) - pass N` e `retry pass N with X piece(s) that did not stand yet`.

**Medicao (03/11/2026):** no arquivo de 985 pecas o retry entrou em acao (`retry later: wood_floor (can't reach it)
... pass 0`) - mas a causa era o **teste**, nao o canteiro: o companheiro estava com **200 madeiras (400 de peso, teto
300)**, ou seja *encumbered*, e o `MHR_Mover` desiste em 8 s sem progresso. Com **120 madeiras (240 de peso)** ele
**andou e construiu 60 pecas** em ~3 min (todas `supported`) e so entao entregou a coleta (2325 que faltavam).
**O guarda do peso era codigo morto** (`CompanionCarry.Fraction` faz `Clamp01`, entao `Fraction > 1` nunca era verdade):
corrigido para comparar o peso real com `CompanionCarry.Max` - a ordem agora diz `hc_ai_build_too_heavy` em vez de
culpar o lugar. Num plano grande o **local plano e aberto** continua sendo pre-requisito (o nivelamento nao corta morro
nem derruba arvore).

### 15.2d Limpar o canteiro antes da primeira peca (03/11/2026)

Pedido do jogador depois do teste do arquivo de 985 pecas: as arvores, tocos e troncos caidos **dentro do footprint**
recusam o piso (`no_support`) e barram o caminho (o `MHR_Mover` desiste em ~8 s sem progresso) - e o nivelamento **nao
corta nada**. A ordem agora tem a fase `Phase.Clear`, que roda **antes** da primeira peca (e **depois** do nivelamento,
que e uma operacao de terreno: a enxada alisa o chao com as arvores em cima - por isso a ordem e nivelar, limpar,
construir).

**Quem corta e o harvest**, pelo **alvo dirigido** (`HarvestController.SetDirectedTarget`, o mesmo caminho do radial que
manda o companheiro numa arvore): machado, tier da ferramenta, caminhada, golpes e drops sao o codigo do jogo. O
`MHR_Build` so **escolhe o proximo obstaculo e espera** - quando o objeto some (a arvore cai e viram toco/tronco novos),
ele pega o proximo. `SetDirectedTarget` passou a devolver **`bool`** (false = recusou: sem ferramenta ou tier abaixo), e
ai o obstaculo entra na lista de ignorados em vez de prender a ordem por 45 s.

**Orcamentos (todos de proposito):** 45 s por obstaculo, **40 obstaculos** e **240 s** por ordem; **5 recusas seguidas**
("nao tenho machado") encerram a limpeza. O que sobrar fica para o **retry/skip** das pecas - a casa nao para por causa
de uma arvore. O guarda de **peso** fica suspenso durante a fase Clear (a madeira das arvores cai na **mesma mochila** que
paga a obra - virou material) e volta a valer no primeiro frame de construcao, com os numeros honestos.

**Config `[Building] ClearSite`** (bool, **true**). Fala nova: `hc_ai_build_clearing` ("Vou limpar o canteiro antes de
construir ({0} arvores/troncos).") nos 5 idiomas. Logs: `clearing the site: N tree(s)/log(s) inside the X m footprint`,
`clearing the site: cutting "Beech1" (3/12)`, `giving up on "..." (over 45 s)`, `can't cut "..." (no tool for it) -
leaving it`, `the site is clear (12 obstacle(s) in 96 s)`.

**Medido em jogo (03/11/2026, `CommHomeBasicBP` de 983 pecas, canteiro com arvores):** nivelamento **0,37 -> 0,13 m**
em 2 passadas (`[Level]`), depois **3 arvores dentro do quadrado de 30 m** (footprint de 32,1 m) e **7 obstaculos** no
total (as arvores derrubadas viram toco/tronco, que tambem sao alvo) em **156 s** - dos quais **44 s** parado porque a
mochila chegou ao limite e o harvest levou a carga para um bau ("Too heavy - taking a load to chest"): o **timer por
obstaculo pausa** enquanto o companheiro esta acima do `UnloadThreshold` (o `ClearPerObstacle` de 45 s dava a arvore por
perdida nesse caso - aconteceu). Depois: `the site is clear (7 obstacle(s) in 156 s)` e a construcao comecou.

**O problema que o teste expos (as 60 recusas):** o **2o e o 3o piso** do plano (y = 42,8/43,8/44,8, `drop` de 3,9 a 5,9 m)
foram recusados com `no_support` **~60 vezes em 40 s**, cada um com a caminhada ate a peca - o jogador viu "ele so anda
pela obra". O veredito e o **mesmo de longe** (a pose e da peca), entao o `Goto` agora faz o `BuildSite.Check` **antes de
andar** (uma vez por peca, `State.Checked`) e manda a peca para o retry pass no `no_support`. Tambem: ao terminar a
limpeza o relogio da ordem e **reancorado** (`state.Start`), senao a 1a peca chegava com 156 s e era pulada como "took too
long" (aconteceu **1 vez** - `skipping wood_floor (took too long) ... - 3 skipped`, contando as 2 linhas do blueprint).

**Os troncos que ficaram rolando no canteiro (relato do jogador, 03/11/2026):** quatro causas somadas, todas
corrigidas. (1) O quadrado parava em **30 m** e o footprint medido era **32,1 m** - a borda do canteiro nunca era
limpa; o teto foi para **40 m** (o numero de obstaculos e o prazo continuam segurando). (2) O filtro de "dentro do
quadrado" olhava o **pivo** do objeto: a tora tem o pivo numa das pontas e uma tora que rolou fica **meio fora** - para
`TreeLog` o teste agora e pelos **limites** (`Renderer.bounds` de todos os filhos, com um filtro de pivo de +12 m para
nao medir tora do mundo inteiro); arvore em pe continua sendo julgada pelo pivo. (3) A arvore derrubada **spawna as
toras um instante depois** e a tora continua rolando: um quadrado vazio nao e o fim - o job da **uma varredura extra
2 s depois**. (4) O obstaculo dado como perdido (o `giving up on "Birch_log"` do teste, causado pelo "too heavy") entrava
na lista de ignorados **para sempre**: agora ha uma **segunda chance** para eles antes de a obra comecar
(`second look at N obstacle(s) left behind`).

### 15.2e Os tres achados do fim do teste (03/11/2026)

**(1) O blueprint voltava como UMA peca.** Quando a madeira acabou, o `BeginGather` entregou a ordem a coleta e o watcher
(`CheckPending`) retomou com `Start(cs, pending.Prefab, ...)` - ou seja `order: build wood_beam` e `order ended:
hc_ai_build_done` 6 s depois: **a casa de 983 pecas virou uma viga**. O `Pending` guarda `Template` (template) mas nao
guardava o **blueprint**; a retomada so sabia refazer plano de template. Agora `Pending` leva `Blueprint`/`BlueprintFile`
e o **`ResumeBlueprint`** reabre o arquivo, remonta o plano (deterministico) e continua **na mesma ancora, na mesma
rotacao e na mesma peca** (`Index`), **sem repetir nivelamento nem limpeza**. E o **`Retry`** (as pecas que nao ficaram de
pe nem nesta passada - o 2o piso) viaja no `Pending` e volta para o estado retomado: sem isso a casa perdia o piso de
cima em cada coleta.

**(2) A coleta do blueprint pedia 2 madeiras.** O `BeginGather` so trocava o pedido pelo **resto do plano** quando
`state.Template != null`; um blueprint caia no custo da peca (`gathering 2x $item_wood`) enquanto o watcher esperava
**1927**. Agora o blueprint tambem pede o resto do plano (`PlanMissing` da peca que faltou); o saque previo dos baus
continua so para template (o `BuildStock.Take` por peca ja usa os baus).

**(3) Cepos ficando no canteiro** (relato do jogador): a varredura procurava `TreeBase` e `TreeLog`, mas o **cepo** e um
`Destructible` cujo nome tem `"stub"` (a mesma regra que o harvest usa para classifica-lo) - e um cepo embaixo de um piso
recusa a peca como um tronco. Entraram os tres (arvore, tronco, cepo), com `DetermineHarvestModeStatic` devolvendo modo 1
para o cepo.

**Obra medida:** 223/983 pecas em ~7 min (~4 s/peca), todas `supported`; a partir da peca ~190 ele passou a pagar dos
baus (2 madeiras por peca, baus a 3-25 m) e 3 `wood_beam` falharam a verificacao (`not_found`) e foram para o retry.

### 15.2f A bancada: a cobertura e por peca (03/11/2026)

Pedido do jogador: a obra tem que **verificar a bancada e o raio dela**, e construir uma em lugar adequado quando nao
cobre. A regra do jogo e **por peca** - `CraftingStation.HaveBuildStationInRange(name, ponto)` (a mesma funcao do
`Place`) - e o raio e **`m_rangeBuild` = 20 m**, nao 5 (o `5` e o `m_spaceRequirement` que apareceu no teste da bancada;
ver a tabela do §3). Dois detalhes medidos antes e que decidem o desenho: a funcao **achata o Y** do ponto no Y da
estacao (uma bancada no terreo cobre os baus do 2o andar) e a bancada **nao precisa estar na mesma peca de chao**.

O `CommHomeBasicBP` tem **~70 `piece_chest_wood`** em tres andares e **nao traz bancada nenhuma**; a checagem antiga
perguntava uma vez, do **centro do footprint** (`hint`), e passava com uma bancada qualquer a 20 m - e os baus que
estivessem fora desse raio cairiam em `needs_station` um a um.

**Agora (`PlanStations`, antes da primeira peca):** o plano e percorrido em ordem; para cada peca com
`piece.m_craftingStation` (so a bancada e "construivel" pelo mod), se **nenhuma** estacao daquele tipo esta no raio
**daquela** peca e **nenhuma bancada planejada** ja a cobre (distancia **horizontal**), uma **bancada e inserida no plano
logo antes dela** - procurada em aneis de **0 / 1,6 / 3,2 m** (`FindBenchSpot`, `ring 0..2`, 8 direcoes) e com as 4
rotacoes, pelo `BuildSite.Check` (apoio, espaco, clipe). **A bancada de fora fica NO CHAO**
(`ZoneSystem.GetGroundHeight`); o Y da peca so vale como fallback, quando o ponto esta a 3 m ou mais do terreno (bancada
que tem que ir para dentro, no piso). O executor constroi como qualquer peca: sai
da mesma mochila/baus e entra no `PlanMissing` (10 madeiras cada). Teto de **8 bancadas** por ordem, e o passe para de
procurar depois de **6 buscas** (`MaxBenchSearches`) - sitio sem lugar livre nao trava o frame. Log:
`workbench for the <peca> (none in range of it): added at (x, y, z) - covers 20,0 m` e o resumo
`station: N piece(s) need a workbench, M workbench(es) added to the plan`. Estacoes que o mod **nao** fabrica
(stonecutter, forge...) continuam sendo recusa nomeada, como antes.

**A REGRA E MAIS DURA DO QUE PARECIA (medido em jogo, 03/11/2026):** no Valheim 1.0 **toda peca** tem
`m_craftingStation` = bancada - nao e so movel. O log do teste disse `station: 983 piece(s) need a workbench` (as 983
pecas do plano) e `piece wood_floor refused now (needs_station)`: sem bancada no raio **da peca** nada sobe, nem piso.

**Os tres bugs do primeiro teste (03/11/2026, todos corrigidos):** (1) a saida "clearing stopped" (orcamento) **nao
reancorava** o relogio da ordem - so a saida "the site is clear" fazia isso -, entao a 1a peca (a bancada) chegou com
261 s de limpeza nas costas e foi pulada como `skipping piece_workbench (took too long)`; (2) o `needs_station` **matava a
ordem inteira** (o `Finish hc_ai_build_needs_station` vem antes do `QueueRetry` do blueprint), e com a bancada pulada o
2o passo (`wood_floor`) derrubou as 983 pecas (`order ended: hc_ai_build_needs_station`); agora, num blueprint,
`needs_station` vai para o **retry** (a bancada pode estar mais a frente no plano); (3) a bancada era procurada **perto da
peca**, ou seja **dentro** da casa, e ia **tomar o lugar de um piso** - agora ela vai **fora do footprint**, no lado da
peca (`centro + direcao * (metade do footprint + 2,5 m)`) e **no chao** (`ZoneSystem.GetGroundHeight`), com o fallback
antigo se nao houver lugar la. O orcamento da limpeza tambem **pausa** enquanto ele leva carga ao bau (na medição foram
~60 s dos 261 s carregando madeira).

**Segundo teste (03/11/2026, com as correcoes):** funcionou o que tinha que funcionar - `workbench ... added at
(-140,5, 37,7, -431,4)` e `(-172,8, 38,8, -443,8)` (**fora** do footprint, 17,8 m do centro), `the site is clear (1
obstacle(s) in 33 s)`, **`done: piece_workbench ... supported`** e os pisos atras, pagos da mochila e de baus a 16-24 m -
**119 pecas** e 98 `no_support` (os andares de cima, que o retry pass pega no fim). O `needs_station` nao matou mais nada.

**O achado seguinte (relato do jogador: "ele foi cortar madeira ao inves de vir pegar nos baus aqui perto do player"):**
os baus eram procurados **ao redor da PECA** (`BuildStock.ChestRadius` = 30 m), e no canto oposto de uma casa de 32 m os
baus da base ficavam a ~35 m -> `missing: Madeira x2` -> entregou a coleta com **mais de mil madeiras ainda nos baus**
(o vigia, que conta a partir da **ancora**, dizia `Madeira x890`). Agora o `Take`, o `WithdrawTemplate` e o `PlanMissing`
procuram o estoque **ao redor da ancora da obra** (`state.Anchor`, o centro do plano) - o bau e o estoque do **canteiro**,
nao da peca.

**Terceiro teste (03/11/2026): a bancada agora e AUTO-CURADA.** O plano acrescentou **1** bancada (a de
(-172,8) do teste anterior continuava de pe e cobria o outro lado - a checagem funcionou) e colocou os pisos; dali a pouco
o jogador **derrubou a bancada junto com a obra** que estava limpando, e as pecas do lado leste (x -141..-150) comecaram a
ser recusadas com `needs_station` - `build.check` confirmou: `hasStation=false` a 24-27 m de qualquer bancada (o sitio
tem 32 m e as bancadas que sobraram ficam na base). Antes isso so ia para o **retry** (a parede nunca subia). Agora o
proprio `Place`, ao ver `needs_station` numa peca que precisa de bancada, chama o **`InsertBenchStep`**: procura um lugar
(fora do footprint, no lado da peca, no chao, 4 rotacoes, `BuildSite.Check`), **insere a bancada no plano logo antes
daquela peca** e a torna o passo atual; a peca recusada vem no indice seguinte. Teto de 8 por ordem (o mesmo
`MaxPlannedBenches`), log `no workbench in range of the <peca> - one added in front of it at (x, y, z)`.

**Retomada:** a ordem retomada usa o **plano exato** da primeira parte (`pending.Plan`) - as bancadas que ela mesma
acrescentou fazem parte dele. Reconstruir o plano agora (com as bancadas ja de pe) inseriria menos bancadas e **todo
indice depois delas andaria**, perdendo as pecas do meio em silencio.

### 15.2g "O companheiro esta tremendo e parou" - dois donos do movimento (03/11/2026)

Relato do jogador no teste seguinte: o companheiro **tremia no lugar e parava**, enquanto o log mostrava pecas subindo
normalmente (`done: wood_floor ... supported`) **e** o job de ocio trabalhando no meio da obra:

```
[Idle|Companheiro 2] Nothing to do - idling around (-148,5, 38,9, -428,6)
[Build] Companheiro 2: done: wood_floor at ...
```

**Causa:** `MHR_Build.Active(Component c)` (e `MHR_Level.Active`) comparavam `c.GetInstanceID()` com o dicionario, mas o
estado e guardado pelo id do **`CompanionAI`**. Quem chama e outro comportamento do **mesmo GameObject**
(`HarvestController`, `IdleController`, `CompanionTalk`...), cujo `GetInstanceID()` e **outro** - a comparacao **nunca**
casava. Resultado: `MHR_JobRules.OtherOrder` respondia **false** durante uma obra, o job de ocio continuava andando e
**os dois `MHR_Mover` empurravam o mesmo corpo** - tremor e travamento. O `Cancel` tinha o mesmo defeito (nao removia
nada). Medido: 0 excecoes no log, nenhum erro - so o `[Idle]` no meio do `[Build]`.

**Correcao:** `StateOf(Component)`/`Owner(Component)` resolvem o **`CompanionAI`** do GameObject (o proprio componente
ou `GetComponent`) e usam o id dele; `Active`/`Cancel` passam por ali nos dois jobs. Com isso o `OtherOrder` responde
**true** durante a obra (todos os jobs param, como o desenho sempre disse) e o **harvest ganhou uma unica excecao**: com
`MHR_Build.Clearing(c)` verdadeiro (fase `Clear`) ele e a ferramenta da limpeza e continua trabalhando.

### 15.2h O nivelamento que nao converge para de andar (03/11/2026)

No teste seguinte o `[Level]` rodou **5 passadas de 24 s** com a mesma variacao (`0,26 m` -> `0,26 m` -> `0,26 m`) e ia
ate a `MaxPasses = 10` (~4 min) porque o `NextPass` so parava em "flat" ou no teto de passadas - o "para quando nao
melhora" estava escrito na doc do job mas **nao no codigo**. O nivelar do jogo e um **smooth com teto por operacao**:
o terreno converge para um piso e nao desce mais. Agora o `NextPass` guarda a variacao da passada anterior
(`State.LastVariation`/`Stalled`) e **duas passadas seguidas sem ganho** (>= 0,02 m de folga) encerram com
`hc_ai_level_failed` ("no improvement") - e a obra **retoma** nesse caso (a consequencia obrigatoria logo abaixo).

**Consequencia obrigatoria (03/11/2026, relato "nao fez"):** quando o nivelamento **desiste** (`hc_ai_level_failed`,
"no improvement"), a obra tambem tem que **retomar** - antes o `MHR_Level.Finish` so chamava o build nos casos
`hc_ai_level_done`/`hc_ai_level_already`, entao um sitio que parava em 0,30 m contra a tolerancia de 0,20 m terminava a
ordem inteira sem colocar uma peca. Agora `resumeBuild` inclui o `hc_ai_level_failed` e o log diz
`the ground is as flat as it gets - back to the build`; o `StartBlueprint` cai no guarda de 90 s (`s_levelTried`) e
constroi com o terreno que tem (as pecas que nao couberem vao para o retry).

### 15.2i O 2o/3o piso: escada e escadinha sao classe propria, antes das paredes (03/11/2026)

Relato do jogador no teste com a casa de 983 pecas ("ele esta com problemas no 2o piso, parece que esqueceu a parte da
escada"). O que o log e o `build.pieces` mostraram: a escadinha **estava construida** (2 `wood_stepladder` no piso
terreo a 38,8 e 2 no 2o piso a 40,8 - a "corrente de 2 escadas ate 42,9" que o `MHR_Mover` planeja), mas o companheiro
ficava **preso a 2,8 m do pe da escada**:

```
[Move|Build] climbing a chain of 2 ladders up to 42,9 (first foot (-163,6, 38,9, -439,3))
[Move|Build] stuck on ladder 1/2 at (-166,3, 38,8, -440,2) - looking again
```

E `build.pieces` em volta do ponto mostrou **parede no caminho**: `wood_wall_half` em (-165,5, 38,3, -440,2) e `woodwall`
em (-165,5, 39,8, -440,2) - o **poço da escada ja estava fechado**. Causa: `BuildBlueprintParser.ClassOf` punha
**escada/escadinha na MESMA classe das paredes** (classe 1, ordenada por altura), entao as paredes em volta subiam
**antes** da escadinha de 40,8 - e o caminho para o pe dela deixava de existir. As pecas de cima (y = 45,8/47,8) ficavam
inalcancaveis: `couldn't get to (-152,2, 47,8, -421,7) from (-152,7, 38,9, -422,1) - giving up`.

**Correcao:** classe propria para **circulacao vertical** (0 = piso/bancada, **1 = escada, escadinha, escada de mao**,
2 = paredes/vigas/postes/portas/moveis, 3 = telhado). A obra passa a subir na ordem que um jogador usa: piso -> **a
maneira de subir** -> paredes -> telhado. Medido no mesmo teste: 787/983 com as pecas de cima pendentes (o retry pass
tenta-las de novo no fim).

### 15.2j Porta e portao por ULTIMO (sugestao do jogador, 03/11/2026)

"No deixar as portas de cada andar por ultimo, ja que ele perde tempo abrindo e fechando toda hora". Correto: uma porta
posta na fase das paredes vira uma porta que o `MHR_Mover` **abre e fecha em cada passagem** durante o resto da obra (o
tratamento de porta do mover e para atravessar, nao para ficar aberta). E a mesma regra que os **templates** ja seguem
(a porta e sempre a ultima peca, senao o companheiro se tranca dentro).

**Correcao:** `ClassOf` ganhou a classe **4** para `door`/`gate`, depois do telhado (0 piso, 1 escada, 2 paredes,
3 telhado, **4 porta/portao**). Durante a obra o vao da porta fica **aberto** (parede em volta segura o conjunto) e a
porta entra no fim - sem abrir/fechar, sem tempo perdido e sem risco de trancar.

**Correcao (relato do jogador, 03/11/2026: "ele fez as portas no terreo enquanto trabalha no terreo"):** a primeira versao
da regra punha porta/portao no fim **do andar** (classe 4 dentro do andar), o que ainda deixava a porta do terreo fechada
durante o resto da obra. Agora `door`/`gate` sao a **ultima coisa da OBRA inteira**: o `Ordered` ordena primeiro por
"e porta? 0/1" e so depois por andar/classe/altura - as portas de todos os andares entram no fim, quando ninguem mais
precisa atravessar.

### 15.2k A obra anda ANDAR POR ANDAR + o pulinho do alcance (03/11/2026)

Dois achados do mesmo teste (screenshot do jogador: paredes do terreo de pe, escada grande a direita e o vao do 2o piso
vazio, com a armacao do telhado ja subindo).

**(1) A ordem era por CLASSE no plano inteiro:** `Ordered` fazia `ClassOf` e depois `Py`, entao **todos os pisos da casa**
(terreo, 2o, 3o) entravam na primeira leva. Os de cima sao recusados com `no_support` (falta a parede embaixo) e caiam no
**retry pass do fim** - resultado: o telhado subia **antes** do piso que o sustenta e a casa parecia "sem o 2o andar".
Agora o plano e ordenado **por andar**: o andar de uma peca e o **ultimo piso (`floor`) na mesma altura ou abaixo dela**
(`StoreyOf`), e dentro do andar vale a ordem de sempre (piso -> escada -> parede -> telhado -> porta). E como um jogador
constroi, e cada andar ja tem o de baixo pronto quando comeca.

**(2) O "pulinho" (ideia do jogador):** a regra do jogo e 5 m do **olho** ate a peca (`Player.m_maxPlaceDistance`). Uma
viga 10 cm acima disso obrigava o companheiro a achar escada/andar de cima. Agora, com os **pes ** no lugar (distancia
horizontal dentro do `Reach`) e a peca **ate 1 m alem** do alcance, ele **pula** (`Character.Jump`) e coloca no ar - o
olho sobe com o corpo e a mesma regra cobre a peca, exatamente o que um jogador faz com viga de telhado. Cooldown de
**0,9 s** (`State.NextJumpAt`) para nao virar pula-pula, e o log (Detailed) diz
`a hop to reach the <peca> at 0,4 m beyond the reach`.

### 15.2l Escadinha que termina no vazio + queda em loop (03/11/2026)

Relato do jogador com screenshot: *"ele ficou em loop caindo da escada ate morrer"*. A casa tem um **salao de pe-direito
duplo**: naquela area **nao existe piso no nivel 2** (o "2o piso" do blueprint ali e o 3o nivel). O plano traz uma
`wood_stepladder` naquele nivel, ela e colocada (o `BuildSite.Check` valida a base e as paredes, **nao o topo**), o
`MHR_Mover` monta a corrente de escadas e **sobe** - e o topo da no vazio. Ele cai, tenta de novo, cai... ate morrer.

> **Superado em 03/11/2026 (§15.2r):** o `HasLanding` e o adiamento para o retry **saíram**. A escada é **peça do plano**
> (colocada quando o plano chega nela, apoiada no piso) e quem recusa a **subida** para o vazio é o
> `MHR_Mover.LadderLanding` (fail-open: só o vazio). O texto abaixo fica como histórico do acidente.

**Duas travas (as duas no `MHR_Build`):**

1. **Escadinha que termina no vazio nao e colocada:** antes de andar ate a peca, um `HasLanding` sonda **para baixo** do
   topo (a 2,6 m da base) no proprio ponto e num anel de 0,6 m (o jogador fica **ao lado** da escada, nao em cima dela);
   sem piso/peca ali, a peca conta como `skipped` com a linha `skipping <peca> at (...) - its top has no floor to stand
   on`. Fail-open se as layers nao existirem.
**Correcao de um erro NOSSO no item 1 (relato do jogador, 03/11/2026: "ele nao fez a escada"):** a primeira versao da
guarda **pulava** a escadinha para sempre (`state.Skipped++`), e a casa terminava **sem ela**. O certo e **adiar**: sem piso
no topo *ainda*, a escada vai para o **retry** (`QueueRetry`, `top has no floor yet`) e e colocada no **retry pass** do
fim, quando o piso de cima existe; so se continuar sem piso nas 3 passadas ela e pulada e contada. O log agora e
`the <peca> at (x, y, z) has no floor on top yet - retrying it at the end`.

2. **Queda repetida = peca pulada:** um **tombo de mais de 0,8 m entre frames** enquanto anda ate a peca conta
   (`State.FallDrops`); **duas** quedas na mesma peca e ela e pulada (`falling from the way to the <peca> - skipping it`),
   em vez de tentar de novo. O contador zera a cada peca nova.

O companheiro **morreu** no loop e voltou sozinho (respawn do mod) - o `companions.list` mostrou `dead: false`, 25/25.

**Corrigido em seguida (03/11/2026): o andar e POR AREA.** `StoreyOf` passou a receber a lista de pisos e a peca: o andar
e o **piso mais alto abaixo dela a ate 3 m em XZ** (um jogador constroi em volta dele), depois ate **8 m**, e so como
ultimo recurso o piso mais alto do predio abaixo. Num **salao de pe-direito duplo** a escadinha do nivel intermediario
tem, a 3/8 m, apenas o **piso do terreo** - entao ela entra junto com o terreo e o piso do 3o nivel nasce depois, na ordem
certa ("ele deveria comecar a fazer o piso a partir da escada"). O `ClassOf` continua dentro do andar: piso -> escada ->
parede -> telhado -> porta.

**Ainda em aberto (proximo passo):** o andar de uma peca hoje e o ultimo `floor` do **predio inteiro** abaixo dela; num
salao isso mente (a escadinha do nivel 2 tem piso do terreo abaixo, mas nao do lado). O certo e o andar **por area**
(ultimo piso a ~2 m em XZ da peca), que e o que faria o piso nascer a partir da escada, como o jogador pediu.

### 15.2m A ponta do telhado: 60 s por peca inalcancavel (03/11/2026)

Relato do jogador com screenshot (ela em cima das aguas, na ponta): *"ela ta apanhando pra fazer essa ponta do telhado"*. O
log mostra duas coisas: as pecas da **empena no topo** (`wood_wall_roof_top_45` a 43,8, 45,8 e 47,8 na mesma coluna) sao
recusadas com `no_support` - precisam da de baixo, e o **retry pass** resolve (e o `Goto` ja adia sem andar, pela checagem
antecipada de apoio); e as **aguas do telhado na ponta** (`wood_roof_45` a 41,8/43,8) caem em `took too long`: o
companheiro anda/sobe, **nao consegue se aproximar** e so desiste no **timeout de 60 s** - 2 a 3 minutos por peca, que e o
"apanhar" que o jogador viu.

**A solucao que ELE achou (relato do jogador com screenshot, 03/11/2026: "ele foi esperto e subiu pelo telhado pra fazer a
ponta no alto"):** quando a peca da ponta nao fecha por baixo, o `MHR_Mover` **sobe pela propria agua do telhado** (o mesmo
`Steer`/`LedgeAhead` que usa em escada, com o passo e o pulo) e coloca a peca **de cima**: o log confirma 20 pecas acima de
43 m, incluindo `wood_roof_45` a **43,8 e 45,8** e `wood_wall_roof_top_45` a **44,8 e 46,8**. Ou seja o "apanhar" era o
**caminho**, nao a peca: com o tempo (ou a trava de 12 s) ele acha o angulo. Vale registrar porque e o comportamento
desejado: **ele usa a propria obra como andaime**, sem teleporte nem trapaça - e a razao de a trava de progresso ser
conservadora (so desiste depois de 12 s *sem chegar mais perto*, nunca por tempo total).

**Correcao:** nova trava de **progresso** no `Goto`: guarda a menor distancia ja alcancada ate a peca
(`State.BestDistance`/`BestAt`, zerados por peca); **12 s sem chegar 0,5 m mais perto** mandam a peca para o **retry**
(`no progress towards the <peca> for 12 s (N m away) - retrying it later`), em vez de esperar os 60 s. Continua valendo o
retry pass (ate 3 passadas) e o pulo com contagem no fim.

### 15.2n A obra retoma depois da morte (03/11/2026)

Relato do jogador: *"ele caiu e morreu, ai nao continuou a build"*. O `Drive` terminava a ordem com `why = "it's dead"` e
**nada** guardava o plano - depois do respawn (30 s, no lar ou na lapide) o companheiro voltava em Follow e a obra ficava
pela metade.

**Correcao:** na morte, o estado do plano vai para o **`Pending`** com a flag `AfterDeath`: prefab, template/blueprint,
`Hint`, `Anchor`, `AnchorRot`, `Index`, `Plan`, `Retry` e `Passes`. O `CheckPending` (que roda sempre que nao ha obra
ativa) **espera o respawn** (`humanoid.IsDead()` com `AfterDeath` nao descarta o pending) e, vivo de novo, **retoma na
mesma peca, ancora e rotacao** pelo `ResumeBlueprint`/`StartPlan(..., anchorKnown: true)`, carregando o `Retry`.
Logs: `it died during the build - the order resumes after the respawn (piece N/M)` e `back from the dead - resuming the
build at piece N`. Um `stop` do jogador continua **derrubando** o pending (`Cancel` limpa `s_pending`).

### 15.2o O alcance da colocacao era menor que o do jogo (03/11/2026)

Relato do jogador: *"ele ainda esta apanhando tentando colocar uma peca no teto alto"*. O log da peca presa dizia
`no progress towards the wood_wall_roof_top_45 for 12 s (3,3 m away)` - ou seja, ele estava a **3,3 m em 3D** (3,15 m
na horizontal, ~1 m abaixo da peca) e o teste do proprio mod recusava colocar. A regra era **dupla**:

```csharp
bool inReach = flat <= Reach + 0.5f && Vector3.Distance(eye, state.Pos) <= MaxPlace;   // 3,0 m planos + 5 m do olho
```

O jogo coloca a peca a ate **5 m do OLHO** (`Player.m_maxPlaceDistance`), e o companheiro ainda tem 1,5 m de olho
acima dos pes: um beiral 1 m acima dos pes, a 3,15 m na horizontal, esta **dentro** da regra do jogo (distancia do
olho ~3,3 m) e era recusado por **15 cm** no limite plano - e ele andava em circulos 12 s ate o retry. **Correcao:**
o limite plano **normal** continua `Reach + 0,5` (**3,0 m**) e ele **abre para `MaxPlace - 0,5` (4,5 m) so depois de
**3 s sem chegar mais perto** (revisao de 03/11/2026: senao ele passaria a colocar peca de longe sempre); a regra do olho
continua sendo a do jogo (5 m) e o `Reach` (2,5 m) voltou a limitar a colocacao (e tambem o "pulinho", `Reach + 0,5`).

### 15.2p A escadinha volta para o plano assim que o piso existe (03/11/2026)

> **Superado em 03/11/2026 (§15.2r):** o `RequeueLadders` foi **removido** junto com o adiamento - a escada não é mais
> adiada, é colocada quando o plano chega nela. O texto abaixo fica como histórico.

Pergunta do jogador com screenshot: *"pq nao tem escada aqui ainda?"*. As 4 `wood_stepladder` do plano foram adiadas
com `top has no floor yet` (o piso de cima ainda nao existia) e iam para o **retry pass final** - que so roda depois da
**ultima** peca de uma casa de 985 (5 h de obra), entao a casa inteira subia **sem escada nenhuma**. Agora, **sempre
que um piso e colocado**, o `RequeueLadders` varre a fila de retry e devolve ao plano (logo depois da peca atual) as
escadas que ja tem pouso (`HasLanding`), com a linha `N ladder(s) have their floor now - back in the plan`.

**Medido com `build.snaps` no jogo (03/11/2026)** - os snaps locais das duas pecas de subida:

| prefab | snaps de baixo | snaps de cima | sobe para | ganho |
|---|---|---|---|---|
| `wood_stair` | y=0, z=+1 | y=1, z=-1 | **-Z** local | +1 m |
| `wood_stepladder` | y=0, z=+1 | y=2, z=-1 | **-Z** local | **+2 m** |

Consequencia para qualquer andaime: a peca tem que ser girada com o **-Z virado para a peca alvo**
(`rot = LookRotation(ponto - peca)`), senao ela sobe para o lado contrario.

### 15.2q A escadinha provisoria (03/11/2026)

Pedido do jogador: *"vamos tentar aquele seu plano de fazer escadinhas provisorias, pq ele ainda esta apanhando tentando
colocar uma peca no teto alto"*. A ideia: quando a peca esta **acima do alcance do olho** (5 m) e o companheiro ja esta
parado embaixo dela, um degrau resolve o que andar nao resolve.

**A peca: `wood_stair`, e nao `wood_stepladder`.** Medido com `build.inspect` (03/11/2026): as duas custam **2 madeiras**,
exigem **bancada em 20 m** (`$piece_workbench`, rangeBuild=20) e tem `WearNTear.supports = true` (precisam de apoio).
A diferenca e o comportamento: a `wood_stepladder` sobe **+2 m** mas e **escada** (pelo nome, `MHR_Mover.IsLadder` casa,
`MHR_Mover.cs:1172-1176`) e o topo **sem piso** e a armadilha de queda de 03/11/2026 (hoje barrada pelo
`MHR_Mover.LadderLanding`); a `wood_stair` sobe
**+1 m** e e uma **rampa** - o mover sobe como geometria pura (26,6° passa no `rise <= 1,1` da linha de rota,
`MHR_Mover.cs:2291`, e a `LedgeAhead` nao a le como borda). Para andaime, rampa que nao precisa de pouso e o certo.

**Direcao (medida, `build.snaps`):** a rampa sobe para o **-Z local** (`y=0,z=+1` -> `y=1,z=-1`), entao o `+Z` tem que
apontar para LONGE da peca: `Quaternion.LookRotation(-toPiece)`.

**Como entra no `MHR_Build.Goto`:** **so em blueprint**, quando a trava de **12 s sem progresso** dispara com
`flat <= MaxPlace` (ele esta perto, mas o beiral/empena corta o caminho) **e** a peca esta acima do alcance do olho - o
gatilho "peca alta" que existia antes foi removido (era ele que enchia o sitio de escadas). O limite plano normal continua **3,0 m** (`Reach + 0,5`); os **4,5 m** so
valem depois de **3 s sem chegar mais perto** (review, 03/11/2026: senao ele passaria a colocar peca de longe sempre). O `TryScaffold` procura um ponto em **2 aneis (1,2 e 2,3 m) num leque de -35° a +34°** (4 direcoes), **raycast para baixo** para assentar no piso (recusa desnivel > 1,2 m), recusa ponto onde o **plano ainda vai por peca ou onde uma peca espera no retry** (`BlockedByPlan`, **1,5 m em XZ e 2 m em Y**), valida com `BuildSite.Check` e paga pelo `BuildStock.Take` (mochila -> baus) - falhou o `BuildExecutor.Place`, o `Refund` devolve. Teto de **4 escadas COLOCADAS por ordem** (`MaxScaffolds`; tentativa recusada nao gasta), 1 a cada 4 s, e a escada e **removida quando a peca sobe** (`Verify`), **nunca com ele em cima** (1,5 m ao redor). Config **`[Building] TempScaffold`** (bool, padrao **false** desde o teste de 03/11/2026, com migracao `true -> false`).

**Limpeza:** as escadas provisorias **nao sao do plano** - ficam em `State.Scaffolds` e o `ClearScaffolds` as remove no
**fim da ordem** (`Finish`), no **cancelamento** (`Cancel`), no **`CancelAll`** (ordem sem endereçado: "segue", "para",
"colete 20"), quando uma **ordem nova de build** substitui a que estava rodando (`DiscardState`), no **despawn** do companheiro, ao
**entrar/sair do mundo** (`OnSceneLoaded`) e no **`Shutdown`** do plugin (senao a peca ficaria no save). Nao sao contadas como peca colocada.

**Apoio antes de remover (revisado duas vezes em 03/11/2026):** a primeira versao perguntava `BuildSupport.Supported`
com a escada **de pe** - e a escada e um `WearNTear` com `m_supports`, entao a resposta era sempre "tem apoio" e a
protecao nao protegia nada. Agora o `StairHoldsPiece` **desliga os colliders daquela escada**, pergunta o apoio so das
pecas a **4 m em XZ / 3 m em Y** dela (`State.Scaffolded`, que so guarda pecas colocadas perto de alguma escada) e
religa os colliders; se alguma perdeu o apoio, **so aquela** escada fica, com `LogWarning` (`kept N temporary
stair(s)`). O `Supported` **escreve** `WearNTear.m_support`, entao as pecas consultadas sao perguntadas **de novo** depois
de religar os colliders (senao a peca protegida ficaria com o numero baixo que o jogo usa para quebra-la). A madeira volta
para a mochila **so quando a peca realmente saiu do mundo e foi paga** (`!state.Free`, ZDO removido) - no `BlueprintFree` a
escada nunca e paga, e estornar ali criaria madeira do nada.

### 15.2r A escada e peca da obra; o pouso e do mover (03/11/2026)

Pedido do jogador: *"a escada da build deve ser feita a partir do piso como no comeco era"*. O adiamento que eu tinha
posto (sem `HasLanding`, a escada ia para o retry e so entrava no fim) deixava a casa **horas sem escada** - e o
companheiro precisa dela para o andar de cima. A separacao certa e outra: **a peca e do plano** (e colocada quando o
plano chega nela, apoiada no piso de baixo, como qualquer peca) e **a subida e do mover** (ele nao sobe escada sem
pouso no topo).

**O guarda (`MHR_Mover.LadderLanding`), refeito DUAS vezes na revisao:** a 1a versao sondava um anel em torno do
**centro** da peca (falso negativo: a `wood_stepladder` tem 2 m de profundidade, as sondas caiam na propria rampa) e aceitava
qualquer solido na janela (falso positivo: topo de parede/telhado). A 2a tentou exigir piso horizontal + **1,85 m de
espaco** para o corpo - e isso recusava **todo andar com teto** (pe-direito de 2 m com laje de 0,25 m da ~1,75 m livres):
outro falso negativo, o pior tipo. **Versao final, regressao em primeiro lugar: so o VAZIO e recusado.** O guarda pergunta
o contrario - "existe QUALQUER coisa ai em cima que nao seja a propria escada?" - e aceita se houver piso/topo de parede ate
um raycast de **1,4 m para baixo** a partir de `alvo + 0,4` (cobre ~1,0 m abaixo do alvo) ou de **1,3 m para cima** a
partir de `alvo + 0,3` (~1,6 m acima), para a escada que termina um pouco curta, caso
do `LadderTopHop`). O alvo e `min(topo da peca, needY + 0,5)`, entao uma **pilha** de escadas e julgada no piso do andar em
construcao, nao no topo da pilha. Sem nada acima **e** nada abaixo = o vazio do salao onde ele caiu em loop. Fail-open sem
mascara ou sem `Piece`. Vale **so nas rotas de build** (`tag` "Build"/"BuildGather"); a corrente lembrada (`UseKnownChain`)
**nao** e usada no build (ela nao passa pelo guarda - o build replaneja, que e o que ele ja faz).

Limitacao conhecida: o `AutoJumpLedge` ainda levanta quem **anda** na direcao da escada (`CompanionLadderBumpPatch`), entao
uma rota que empurre o corpo contra ela sobe sem passar pelo guarda - o caminho observado era o `Climb`.

### 15.2s O patamar da escada vem primeiro (03/11/2026)

Pedido do jogador com screenshot: *"ele tem que identificar que deveria colocar a peca do fim da escada e ja fazer o piso
ali"*. A ordem do plano constroi o andar de cima **so depois** de todas as paredes do terreo (`StoreyOf`), entao a
`wood_stepladder` era colocada e ficava **sem patamar** por muito tempo - ele subia e ficava empoleirado na beirada sem
piso para trabalhar, e as pecas de cima falhavam com `no_support`.

**Correcao (generalizada no pedido seguinte: "ele pode colocar o piso de baixo pra cima tambem"):** o inverso do adiamento
antigo. Sempre que uma peca que **sustenta** e aceita (`Verify`), o `PullFloorsAbove` varre o resto do plano e **puxa para a
frente** (logo depois dela, `Index+1`) os **pisos** que caem na altura alvo: prefab com "floor", XZ a ate **2,5 m** e
|Y - alvo| <= **1,2 m**. O alvo e o **topo da escada** (`Renderer.bounds.max.y`, fallback `Pos.y + 2`) ou, para parede/viga/
piso/pole (qualquer peca com `WearNTear.m_supports`), **um andar acima** (`Pos.y + 2`). Teto de **6 pecas** por passo, com o
guarda `insertAt >= i`.

**A 1a versao foi reprovada na revisao (simulacao do especialista, 03/11/2026):** mover com `RemoveAt`/`Insert` andando a
lista de tras para frente mantinha os indices validos mas **pulava o vizinho logo abaixo do cursor** e invertia a ordem -
e o pulado era justamente o tile **mais perto da escada** (o do print do jogador). A versao final **reconstroi a lista no
lugar** (`Clear` + `AddRange`, porque `Pending.Plan` guarda a mesma referencia): `head` (0..Index) + ate **2 pisos**
escolhidos por **distancia** + o resto na ordem original, e numa escada so o lado para onde ela sobe
(`state.Rot * Vector3.back`). O topo vem do **maior collider** (um prefab pode ter mais de um renderer e o primeiro nem
sempre e o corpo), com fallback para o renderer e para `Pos.y + 2`. Log: `N floor piece(s) over the <peca> brought forward
so it leads somewhere`.

Como o jogo coloca peca a ate 5 m do olho, um piso 2 m acima dos pes esta **ao alcance** - entao ele constroi o piso do
andar de cima **do nivel em que esta**, sem esperar terminar o andar de baixo (que era o que deixava a escada sem patamar).

Fica valendo o resto: o piso ainda passa por `BuildSite.Check` antes de andar (se a parede daquele canto nao subiu, ele
vai para o retry pass como qualquer peca) e o mover continua recusando subir para o vazio (`LadderLanding`).

### 15.2t A obra retoma depois da morte - agora de verdade (03/11/2026)

Relato do jogador: *"ele morreu e nao continuou a build"*, mesmo com o `AfterDeath` da `c7b9c13`. Duas causas somadas:

1. **O `Drive` nunca ve a morte.** Ele e chamado pelo `CompanionAI.UpdateAI`, que nao roda com o corpo morto - entao o
   ramo `self.IsDead()` que gravava o `Pending` **nunca executava**. O que acontecia: o respawn reinicializava o companheiro
   (`[Setup] Initialized - mode=0`) e a obra morria sem plano salvo.
2. **O respawn pode criar um objeto novo.** O `Pending` era chaveado pelo `GetInstanceID()` do GameObject; com um corpo
   novo o id muda e o pending ficava orfao.

**Correcao:** o `MHR_Build.OnDeath(ai)` e chamado do **`CompanionSetup.OnCompanionDeath`** (o gancho de morte de verdade) e
grava o `Pending` com `AfterDeath` **e o id permanente** do companheiro (`HC_CompanionId`), remove o estado, solta o mover
e leva as escadas provisorias. O `MHR_Build.Reattach(cs)` roda no **`TryInit`** (todo corpo que nasce/renasce) e passa o
pending do id antigo para o novo, reiniciando os relogios. Logs: `it died during the build - the order resumes after the
respawn (piece N/M)` e `the build order of "<id>" was handed to the companion that just came back`.

**2o bug, do proprio teste seguinte (03/11/2026):** o log mostrou `it died during the build - the order resumes after the
respawn (piece 767/985)` (o `OnDeath` funcionou) e **nenhuma** linha de `handed to the companion`. Causa: o `Reattach` roda
no `TryInit` e lia `MHR_CachedCompanionId`, mas o **id permanente (`_mhrId`) so e resolvido depois**, sob demanda
(`MHR_ResolveCompanionId`, chamado no `MHR_Tick`) - naquele instante ele era `""` e o metodo saia. **Correcao:** o
`Reattach` e o `TrySaveDeathPending` usam `MHR_ResolveCompanionId()` (le/cria o id no ZDO, seguro no `TryInit`) **e** o
`CheckPending` chama o `Reattach` **todo frame** antes de procurar o pending - o dicionario tem uma ou duas entradas, e
assim a entrega nao depende da ordem de inicializacao.

### 15.2u A ordem das pecas: vizinho mais proximo dentro do grupo (03/11/2026)

Relato do jogador: *"ate o piso do terreo ele fica de um lado para o outro da build colocando as pecas"*. A ordem do plano
sempre foi **andar -> classe (piso -> escada -> parede -> telhado -> porta) -> altura -> ordem do arquivo `.blueprint`**, e o
ultimo criterio e o problema: o `.blueprint` esta na ordem em que o autor desenhou as pecas, nao num caminho. Como o
companheiro anda ate **cada** peca (`MHR_Mover.Go` com a pose dela), duas pecas vizinhas no arquivo e distantes no mundo viram
uma travessia.

**Correcao (o desempate que o jogador pediu):** o `BuildBlueprintParser.Ordered` continua montando os grupos com os mesmos
quatro criterios e, no fim, o **`NearestWithin`** percorre a lista em **runs** que compartilham (flag de porta, `StoreyOf`,
`ClassOf`, `Py` +-0,01) e reordena **cada run** por um caminho **guloso de vizinho mais proximo**, comecando na posicao da
ultima peca do run anterior (o primeiro run parte da primeira peca). **Nada muda de grupo**, nada e adicionado ou removido, e
porta/portao continuam no fim de tudo.

**Duas redes de seguranca (revisao, 03/11/2026):** (1) **cache no `BuildBlueprints.Load`** por caminho + `LastWriteTimeUtc`
- o `CheckPending` chamava o parser **a cada frame** enquanto uma obra de blueprint esperava material (a leitura do arquivo
de 985 linhas + a ordenacao caíam na main thread o tempo todo); (2) o guloso **nunca fica pior que o arquivo**: o
`PathLength` compara o caminho novo com o do run original e mantem o menor (garantia, nao medicao - o guloso deu 12-23% do
caminho original em todos os casos realistas, inclusive 19.477 -> 2.636 numa casa de 1066 pecas embaralhada).

O parser e **puro** (sem Unity), entao o harness o linka: a ordenacao tem secao propria em `tools/ai-intents-check`
(zigue-zague, grupos que nao se misturam, porta por ultimo, run de 1 peca e estabilidade).

### 15.2v Telhado/empena antes do piso do andar de cima (03/11/2026) - corrigido em 15.2w

Relato do jogador com screenshot: *"ele ainda fica tentando colocar aquelas vigas la de cima antes da hora"*. No log da
rodada de 03/11/2026 (715/985) a sequencia mostra **classe 3** entrando antes dos pisos do andar seguinte:

```
done: wood_wall_roof_45      at (…, 40,8, …) supported   <- empena
done: wood_roof_45           at (…, 41,8, …) supported   <- agua do telhado
done: wood_wall_roof_45      at (…, 42,8, …) supported
```
E os pisos de cima, quando chegam, sao adiados: `retry later: wood_floor (no_support) at (…, 43,8, …)`.

**Causa (provavel, pelo codigo):** o `StoreyOf` acha o andar pelo **piso mais alto abaixo da peca**; uma peca de telhado
cuja laje de apoio ainda nao existe resolve para o andar de **baixo**, e dentro desse andar a classe 3 vem **depois das
paredes e antes do andar seguinte inteiro** (o `Ordered` ordena por andar antes de por classe). Resultado: sobe telhado
antes do piso que ele cobre.

**Nao corrigido de proposito nesta rodada** (o jogador pediu para documentar e esperar o fim da obra). Caminhos de
correcao a avaliar depois: (a) uma peca de **telhado** pertence ao andar da **laje que ela cobre** (um acima da que a
sustenta), nao ao de baixo; (b) ordenar telhado **depois** dos pisos de todos os andares; (c) puxar para a frente o piso
que falta quando a peca de telhado for adiada por `no_support` (o inverso do `PullFloorsAbove`).

**Achado seguinte (03/11/2026, mesma rodada):** o relato virou *"de novo a escada sem piso"*, e o log mostra que **nao e**
o adiamento antigo - o patamar **foi puxado** e **nao tinha apoio**:

```
done: wood_stepladder at (-166,0, 40,8, -436,3) supported
1 floor piece(s) over the wood_stepladder brought forward so it leads somewhere
next: wood_floor (547/985)
retry later: wood_floor (no_support) at (-164,8, 43,8, -435,4) - pass 0
```

O `PullFloorsAbove` fez a parte dele (o piso veio logo depois da escada), mas o **piso em 43,8 nao tem o que o sustente**
(as vigas/paredes daquele nivel ainda nao existem), entao ele cai no `no_support` e a escada fica sem pouso ate o retry.
**O que falta e ordenar por dependencia, nao por classe:** quando o piso puxado falha por `no_support`, as pecas que o
sustentam (as vigas/paredes logo abaixo) precisam vir antes dele. Caminho de correcao (a decidir): no `QueueRetry` do
piso - ou no proprio `PullFloorsAbove` - puxar tambem os **apoios** desse piso (mesmo XZ, Y logo abaixo), repetindo ate 2
niveis. E o mesmo tipo de problema do telhado antes do piso (§15.2v acima): a ordem atual nao modela dependencia.

### 15.2w Dependencia: o telhado espera o piso e o piso sem apoio puxa os apoios (03/11/2026)

Os dois achados abertos (§15.2v) tinham a **mesma causa**: a ordem era **andar -> classe -> altura -> arquivo**, sem modelar
**dependencia**. Duas correcoes cirurgicas:

1. **A chave de ordenacao (parser, puro) - `LevelKey`:** o nivel de uma peca e a **altura dela mesma** (`round(Py, 2)`),
   entao a obra sobe **nivel por nivel** (relato do jogador: *"andar a andar seria perfeito"*): num nivel, piso -> escada ->
   parede -> telhado -> porta. **Excecao:** uma peca de **telhado/empena** (classe 3) pertence a **laje que ela cobre** (o
   piso mais proximo acima, 0,3-6 m, 2,5 m em XZ) ou, se nao houver laje acima, ao proprio nivel - o que cobre o caso do
   **telhado que comeca no chao** (o jogador confirmou que a build tem um). Tres tentativas anteriores foram reprovadas:
   mover o piso com `RemoveAt/Insert` (tirava o piso do andar dele), `StoreyOf + 2` (so valia para pe-direito de 2 m) e a
   chave pelo **piso abaixo** (`StoreyOf`), que punha uma parede em y=0 no andar do piso em y=-0,5 e a construia **antes**
   desse piso - a causa do canteiro "feio" com pisos soltos em varios niveis.
1b. **`PullFloorsAbove` voltou a ser SO da escada:** generaliza-lo para qualquer peca que sustenta (`WearNTear.m_supports`)
   puxava um piso 2 m acima de **cada** viga/poste e encheu o canteiro de pisos flutuantes (relato: *"ta muito feio esse
   canteiro"*). O piso do patamar continua sendo puxado pelo **topo da escada**.

2. **`PullSupports` (runtime):** quando uma peca e recusada com **`no_support`** (o caso do piso do patamar em 43,8), as
   pecas do plano **abaixo dela** (mesmo XZ a 1,5 m, 0,5-2,6 m de altura, ate 3) sao movidas para logo depois do passo
   atual, entao sao construidas **antes** de a peca voltar do retry. Era o furo do `PullFloorsAbove`: o piso era puxado,
   mas o que o sustenta continuava la na frente.

Log novo: `N support piece(s) under the <peca> brought forward so it can stand`.

### 15.2x O loop da escada: recusa que nao marca a escada como ruim (03/11/2026)

Relato do jogador com screenshot: *"tava indo tao bem, mas fica subindo/descendo e caindo da escada"*. O log mostra o ciclo:

```
heading for the ladder wood_stepladder, foot at (…), top at 40,9
at the foot of the ladder - climbing / climbed the ladder at (-163,1, 40,3, -443,2)
the ladder at (-165,9, 41,8, -443,1) has no floor at its top (top 42,9) - not climbing it
1,7m below ... heading for the ladder ... (de novo)
```

**Causa:** o guarda `LadderLanding` recusava a 2a escada (o piso de 42,9 ainda nao existe) mas **nao marcava a escada como
ruim**, entao a busca de rota (a cada 2 s) escolhia a mesma de novo - subir, recusar, descer, subir. O relogio de progresso
de 12 s do build nao vencia porque cada subida aproximava da peca e reiniciava o relogio. **Correcao:** a recusa chama
`MarkBadLadder(id, now, 60f)` - a escada sai da rota por 1 min, o relogio vence e a peca vai para o retry pass (quando o piso
de cima existir ela volta). Mecanismo que ja existia (usado em queda/recusa de cadeia); nenhuma regra nova.

### 15.2y A descida bloqueada ("ficou um tempo bugado aqui em cima", 03/11/2026)

Relato do jogador: *"ficou um tempo bugado aqui em cima, mas depois caiu e parece que esta continuando"*. O log mostra o
`TryDropDown` escolhendo **a mesma descida** para 41,5 a partir de 42,2 **dez vezes seguidas** (uma linha por tentativa),
sem cair - a beirada estava bloqueada - ate um tombo de 4,1 m resolver. **Causa:** o `TryDropDown` nao tinha contador; a
cada frame em que o caminho ficava preso ele reescolhia a mesma beirada. **Correcao:** `S.DropTries`/`DropUntil` - tres
tentativas da mesma altura e aquele caminho de descida e abandonado por 8 s (`the way down at (...) is blocked - giving up on
it for a moment`), com o contador zerando quando a altura muda 0,5 m. Nenhuma regra de queda mudou.

### 15.3 Medido em jogo (02/11/2026)

| arquivo | pecas | resultado |
|---|---|---|
| `TestBox_V2.blueprint` (fixture do PlanBuild) | 6 | 5/5 `supported`, 1 pulada (`stone_floor_2x2`, receita nao desbloqueada), `hc_ai_blueprint_done` |
| `hs_meadows_hut.blueprint` (BiomeBlueprints, "Meadow Hut") | 28 | **28/28 `supported`** - pisos, vigas, paredes, **porta**, telhado e as tampas de empena - `hc_ai_blueprint_done` (com uma bancada perto) |
| o **chat** com o LLM: "construa a hs_meadows_hut aqui" | 28 | resolve o **arquivo** (nao o template "hut") - `[Build] blueprint Meadow Hut: 28 pieces` no log |

Sem a bancada perto o `wood_beam` recusa com `needs_station` e a ordem encerra dizendo qual estacao falta (medido).

**De onde baixar (02/11/2026):** as 3 saíram do **BiomeBlueprints** (OverDrive) -
<https://thunderstore.io/c/valheim/p/OverDrive/BiomeBlueprints/> (download direto:
<https://thunderstore.io/package/download/OverDrive/BiomeBlueprints/1.0.3/>). Dentro do zip, os 353 arquivos estão na
pasta **`blueprints/`** - é só copiar os `.blueprint` que quiser para `BepInEx\plugins\AI-Companions\blueprints\` (o
mod **copia** sozinho a pasta antiga, `BepInEx\config\AI-Companions\blueprints\`, na primeira vez que a nova não existe —
11/11/2026).
Outras fontes: **valheimians.com** (acervo da comunidade) e a **captura do próprio PlanBuild** (salva um `.blueprint` do
que você construiu). O mod **não** precisa do PlanBuild para ler nenhum deles.

**As 3 builds de teste deixadas na pasta (02/11/2026, baixadas do BiomeBlueprints):**

| arquivo (o nome que se fala) | `#Name:` | peças | pede |
|---|---|---|---|
| `hs_meadows_hut` | Meadow Hut | 28 | bancada no raio (vigas) - **28/28 validada** |
| `hs_meadows_stilt_hut` | Meadow Stilt Hut | 32 | bancada + palafitas (`wood_pole2`) |
| `hs_meadows_hive_tower` | Hive Tower (de **ModestyPooh**, <https://www.valheimians.com/build/hive-tower/>) | 75 | bancada + **colmeias** (`piece_beehive`, receita de 10 abelhas na rainha) |

As grandes (4554/1244/505/184 peças) e as fixtures do PlanBuild ficaram em `blueprints\grandes\` e
`blueprints\teste\` - o mod **não lê subpasta**, então elas não aparecem nas ordens.

**Caso pendente (o jogador perguntou em 02/11/2026):** o `Community starter home` (Nexus 1807) é um `.blueprint` de
**2022** com peças vanilla e baús/camas de mobília - o leitor aceita o formato (V1/V2) e baú/cama são `Piece`; o que
pode acontecer é alguma linha usar um prefab que mudou desde então (ela é **pulada e contada**, não derruba a build). O
download é do jogador (login no Nexus e a licença **proíbe redistribuir** o arquivo); o teste em jogo ainda não foi
feito.

**Onde os arquivos ficam:** `BepInEx\plugins\AI-Companions\blueprints\` — a pasta do **DLL** (`Assembly.Location`, a
mesma de `tts\`/`whisper\`/`Translations\`), não mais `BepInEx\config\AI-Companions\blueprints\` (até 11/11/2026: a
mudança foi pedido do jogador, porque a biblioteca é **conteúdo** e assim o pacote pode levá-la). O mod **cria a pasta**
no primeiro uso e, na primeira vez em que ela **não existe**, **copia** a antiga **uma vez** - nunca move e nunca
sobrescreve um arquivo homônimo (`AI/BuildBlueprints.cs` → `MigrateLegacy`; log
`[Build] blueprints: N arquivo(s) migrados de config para plugins`); se a pasta nova já existe (o pacote a traz), a
migração **não roda**. A ordem usa o **nome do arquivo** sem a extensao ("construa a hs_meadows_hut aqui"), porque o
`#Name:` de muitas builds e "Custom Name".

## 13. E10 - nivelar o terreno com a enxada (medido em 01/11/2026)

**Como o jogo nivela (decompilado 1.0.20):** a enxada não tem uma operação de "level" duro. O menu dela é uma
`PieceTable` com 7 peças; a de **"Nivelar"** (`$piece_levelground`) é o prefab `mud_road_v2`, cujo `TerrainOp` tem
`m_smooth = true` (**raio 3, potência 3**) e o paint. Dos **28** `TerrainOp` registrados no `ObjectDB`, **nenhum** tem
`m_level = true` - o único caminho é o **soft**.

O `TerrainComp.SmoothTerrain` puxa cada vértice do heightmap para a **altura do ponto** com queda por distância
(`t = 1 - (d/r)^3`), com o delta **limitado a ±1 m por operação**; a operação aplica no `Awake` (instanciar o prefab),
vai por `RPC_ApplyOperation` e o heightmap **assenta em ~0,5 s** (medido: a variação não muda mais depois disso).

### O que funciona e o que não funciona (medido)

| tentativa | resultado |
|---|---|
| **grid espaçado no raio** (9 ops num 9,5 m), 1 passada | 1,01 → 0,76 m ✓ |
| 4 passadas do mesmo grid | → 0,39 m ✓ (convergindo) |
| **grid denso** (1,5 m, 49 ops), 1 passada | 0,55 → 0,95 m ✗ |
| 4 passadas do grid denso | → 1,33 → 2,01 m ✗ (as operações **brigam** entre si) |
| alvo = altura **máxima** (só levantar) | também divergiu ✗ |
| banco de **3 m** | estaciona em ~1,35 m ✗ (o ±1 m do soft não vence) |

**Dois becos sem saída (medidos, para não repetir):**

- **O raise** ("Erguer", `raise_v2`, raio 2, delta 1,0, até ±8 m) parecia a resposta para barrancos, mas **não compõe**:
  as operações **sobrepõem-se** e cada vértice recebe 1-4 delas - uma área de 0,84 m saiu **4,13 m** mais alta; com a
  grade espaçada em 2x o raio ainda subiu ~2,4 m. Revertido a pedido do jogador ("esquece o rise", 01/11/2026); o
  código ficou só como ferramenta (`LevelToMax`/`FillLow`/`ApplyFill`), **sem uso no job**. Os montes do teste ficaram
  no mundo `test3`.
- **O grid denso** (espaçamento 1,5 m, 49 ops) **diverge mesmo com 2 s entre as passadas**: 0,85 → 1,25 → 2,41 →
  1,68 → 2,44 → 1,78 → 2,44... (um ciclo de 2). As operações sobrepostas brigam entre si - não é (só) timing.

**O nivelamento é VISÍVEL (pedido do jogador, 01/11/2026):** a casa **não** aplica as operações em lote - ela
**entrega o serviço ao `MHR_Level`** (o mesmo job da ordem "nivele um terreno") e **retoma sozinha** quando o chão
fica plano (o mesmo repasse da coleta da E4b: um dono do trabalho por vez). A casa termina com
`hc_ai_build_leveling` ("Vou nivelar o terreno primeiro") e o job trabalha **ponto por ponto, virando e batendo a
enxada**; no fim, `the ground is ready - back to the house` e o `MHR_Build` recomeça o plano.

**Aceite medido (mundo `tests`, 01/11/2026):** "Companheiro 2, construa uma casa aqui" numa área de **1,07 m** de
variação:

```
[Build] flattest spot around the hint: 1,07 m of variation
[Level] order: level 9,5 m around (-206,1, 39,3, -427,3)
[Build] leveling first, with the hoe (9,5 m, 1,07 m of variation)   <- a casa entrega
[Level] pass 1: 0,59  ->  pass 2: 0,34  ->  pass 3: 0,21  ->  pass 4: 0,13 m (flat enough)
[Level] order ended: hc_ai_level_done
[Build] the ground is ready - back to the house                      <- a casa volta sozinha
[Build] plan house: 57 pieces, 56 poses checked -> 57/57 supported -> hc_ai_build_plan_done
```

**A régua ("bom o suficiente"):** a **casa** usa `[Building] LevelTolerance` = **0,35 m** (é o limite físico do piso:
acima disso o piso fica sem apoio e o jogo derruba) e a **ordem de nivelar** aceita **0,6 m** - um piso a 0,6 m já lê
como plano, e a ordem não precisa de espelho.

**A regra:** o grid é **espaçado pelo raio** (sem sobreposição), o alvo é a **mediana**, e a variação máxima aceita é
**1,2 m** - acima disso a ordem recusa (`hc_ai_build_too_steep`) em vez de cavar um buraco.

### O job (`Jobs/MHR_Level.cs`) - "nivele um terreno 10x10 aqui"

- **Tamanho da mensagem:** `LevelIntent.Size` lê "10x10", "6 x 6", "12 por 12", "12 by 12", "8m", "8 metros" (2..40 m;
  sem tamanho = 8). O `IsRequest` casa o verbo nos 5 idiomas (nivele/nivela/nivelar, level, flatten, allana, aplatir,
  livella...).
- **Trabalha de UM lugar:** anda até o **centro** da área e daí gira e bate a enxada em cada ponto (a operação é
  colocada **no ponto**, a até ~5 m - o alcance de colocação do jogo). **Andar até cada ponto não funciona:** o
  `MHR_Mover` desiste dos pontos atrás de uma lomba/peça e o jogador viu o companheiro **tremendo no lugar**
  ("can't reach point N").
- **Cara de jogador:** vira para o ponto, dispara a animação de ataque da **enxada** (`ZSyncAnimation.SetTrigger`) e
  **0,45 s** depois a operação cai; o próximo ponto. Cada passada é uma volta completa; até **10 passadas** ou a
  tolerância (`[Building] LevelTolerance`, **0,2 m**).
- **É ordem de um tiro:** qualquer outra ordem (ou um "para") cancela - sem isso o companheiro ficava preso (o
  `Drive` devolvia `true` e o AI normal nunca mais rodava: "ficou todo travado").
- **Sem enxada:** `hc_ai_build_no_hoe`; já plano: `hc_ai_level_already`; fim: `hc_ai_level_done` com a variação.

**Aceite medido:** numa área de 8 m com **1,06 m** de variação, o companheiro nivelou em **3 passadas** -
1,06 → 0,62 → 0,44 → **0,33 m** - em **165 s**, andando até o meio e batendo a enxada ponto por ponto
(`order ended: hc_ai_level_done`). O jogador: *"parece que melhorou bastante a terraplanagem"*.

**A casa usa o mesmo caminho (E6):** `MHR_Build` tem uma fase `Level` antes do `FindAnchor` - escolhe o canto **mais
plano** dos 9 candidatos, nivela com a enxada (passadas de 1,5 s) e só então procura o local da obra.

## 16. Teste da `CommHomeBasicBP` (985 peças, 03/11/2026) — o teto do andar

- **Medido:** a obra subiu ~650 peças (709 no raio, incluindo a base) e entrou em `no_support` e
  `no progress ... for 12 s (12–17 m)` nos pisos do **nível 2** (y≈42,8), com o companheiro em y≈42,9. O `TryDetour`
  da fase A não resolveu; o `Goto` desiste depois de 12 s sem chegar 0,5 m mais perto e a peça vai para o retry pass.
- **Hipótese principal:** o piso do nível 2 é colocado de fora para dentro e o mover não atravessa o vão (não há piso
  sob o próximo tile); `LadderLanding`/`TryDetour` não têm por onde chegar. As fases B (links na navmesh) e C
  (navmesh própria) continuam as opções de `docs/re/navegacao-companheiro.md`.
- **Fix em teste (branch `fix/pisos-nivel-2`, 03/11/2026):** `MHR_Build.PreferNearFloor` reordena os pisos do
  **mesmo nível** pelo mais perto do companheiro (primeiro um que ele coloque **sem andar**, 3,0 m; depois até 7 m),
  uma vez por peça e **sem tocar** no que `PullSupports`/`PullFloorsAbove` puxou (`PullUntil`) nem no piso 1 andar
  acima/abaixo (guarda de 1,5 m). Cobre também a **primeira peça da retomada** (morte/gather), que era o caso medido.
  **Aceite:** o mesmo `CommHomeBasicBP` passa dos 720/983 sem `no progress ... 12 s` repetido nos pisos do nível 2.
- **Medido (03/11/2026, branch `fix/pisos-nivel-2` com a P2 da navmesh + o `PreferNearFloor`):** a mesma
  `CommHomeBasicBP` foi de **575 → 981/986** peças (estrutura inteira: piso, paredes, 2º andar, telhado e o portão
  por último) e, quando o retry pass reenfileirou ~92 peças, chegou a **1045/1078** — contra as ~650–720 que
  travavam no nível 2. Morreu uma vez (peça 864) e **retomou sozinha no respawn** (`it died during the build ...
  resumes after the respawn`, 865/986). **A contagem de `why` não foi feita** (o log foi sobrescrito no restart
  seguinte); a evidência é a progressão do vigia.
- **O que parou o run não foi a navegação:** aos 1045/1078 faltaram **77 madeiras**; ele andou até uma `Beech1` a
  30 m, a coleta ficou **300 s** sem entregar (`waiting for materials: Madeira x77 (mode 1)` → `gathering made no
  progress for 300 s`) e o `EndPendingMissing` devolveu o Follow (`follow back on`). A leitura seguinte já mostrou
  **302/300 de peso** (`hc_ai_build_too_heavy` na ordem seguinte, que morreu em 1 min): o suspeito nº 1 é a
  **mochila no teto** — o `HandOver` manda coletar sem olhar o peso e, carregado, o companheiro não pega mais nada.
  **Pendência nova:** guarda de peso antes do `HandOver` (ou descarte no baú).

## 17. O grupo de obra e o portão de apoio (04/11/2026)

**O run:** a mesma `CommHomeBasicBP`, agora com **3 companheiros** no mesmo canteiro, `[Building] BlueprintFree = true`
(sem pagar material, para o teste medir construção e não coleta) e `[Building] PlaceReach = 10`.

### 17.1 O que o grupo fez (medido)

- **Distribuição:** o plano foi repartido por **índices distintos** (1 plano, claims por índice, retry do canteiro) —
  nenhum worker construiu a peça de outro (`a peça X já foi construída pelo grupo - seguindo para a próxima`).
- **Promoção de líder:** a morte do líder foi coberta pelo grupo
  (`o líder do grupo "..." saiu - outro companheiro assumiu o canteiro`, `MHR_BuildGroup.cs:218`) e o canteiro seguiu.
- **Retomada:** o companheiro que voltou (respawn) reentrou no mesmo plano e na peça onde estava
  (`voltou para o grupo "..." na peça X`, `MHR_Build.cs:1701`).
- **Primeiro run do dia (antes do lote do validador):** ~**957/985 colocadas**, **0 peças descartadas por
  dependência** (as que não ficaram de pé por falta de apoio voltaram, não foram puladas) e **74** linhas
  `sem apoio ainda ... - não gasta tentativa, volta com espera`.
- **Navegação:** `no progress` **58 → 6** com o `PlaceReach = 10` e **mortes 5 → 0** na primeira rodada com 10 m
  (os números de comparação são os runs de 03/11, com o alcance do jogo).
- **O que sobrou** desse primeiro run apontou dois achados (as duas correções estão no código e **foram medidas depois**):
  a **cumeeira em y 47,8** era recusada com `no_ground` porque o raio vertical da sonda passa pelo **vão entre as duas
  águas** (`BuildProbe.cs:468` → `AttachedToStructure`, `:641`: `Physics.OverlapSphere` atrás de
  `Piece`/`WearNTear`); o **baú e a cama no barranco** eram `too_steep` e ganharam o `NudgePiece`
  (`MHR_Build.cs:4148`: `BuildSite.Find` a **2,5 m** para mobília — estrutural só **1 m** —, 2 anéis × 8 direções, 48
  candidatos, ignorando o próprio companheiro — o run perdeu **4** peças ali; hoje a mobília vai a **5 m**, §17.6).
- **Resultado final (os três runs com o lote completo): §17.6** — **970/985** com `PlaceReach = 10`, **972
  colocadas + 0 mortes** com `15` e, no **Run 3** (15 + guarda anti-loop), **`970 peças, 16 puladas`** = **986**, **0
  mortes** e **382 KB** de log; o que sobrou no Run 3 é o **plano abaixo do terreno**, agora atacado pelo **deck
  (`DeckLift`)** — §17.9, **medido no run do §17.10** (deck certo; sobrou a **empenha**).

### 17.2 O portão de apoio (`AI/BuildFailure.cs` + `MHR_Build.QueueRetry`)

- **Por que existe (medido em 03/11):** as peças da cumeeira falhavam com `no_support` **antes** de o apoio existir,
  gastavam as 3 tentativas e ficavam fora da casa.
- **Classificação pura** (`BuildFailure.Classify`, `:51`) — a tabela é a que o harness assegura (seção **(p)**):
  - **`Dependency`** (não gasta tentativa, **nunca** conta como pulada): `no_support`, `no support` (as duas grafias),
    `needs_station` e `no_ground` **quando a sonda achou algo embaixo** (`groundBelow`).
  - **`Real`** (gasta uma das `MaxPasses` = 3): `no progress`, `blocked_by_player`, `clipping`,
    `too_steep`, `can't reach it`, `took too long`, `verification: ...`, um `no_ground` **sem** nada embaixo (buraco,
    mar, beirada) e qualquer status desconhecido; `null`/vazio é real.
  - A comparação **ignora caixa** (`NO_SUPPORT` continua dependência).
- **O mundo entra num lugar só:** `MHR_Build.IsDependency` (`:528`) pergunta `GroundUnder` (`:543`) apenas para
  `no_ground` — o mesmo raio da sonda (4 m acima, 12 m no total, `BuildProbe.PlaceMask`).
- **Espera crescente** (`MHR_Build.DependencyDelay`, `:551`): **8 s** na 1ª deferição, **12 s** na 2ª, **20 s** na 3ª e
  **25 s** daí em diante — o que falta pode estar subindo agora (`MHR_BuildGroup.TryQueueRetry(key, index, dependency)`,
  `:502`: incrementa `Dependency` e **não** toca em `Attempts`).
- **Watchdog de estagnação** (`MHR_Build.StallSeconds` = **60 s**, `:85`): o relógio conta **desde a última peça que
  subiu** (`MHR_BuildGroup.LastProgressAt`, atualizado em `MarkDone`, `:481`), não desde que um worker começou a
  esperar. Se nada subiu em 60 s, `StallOut` (`:1951`) descarta a fila (`AbandonRetry`, `:592`), **conta cada índice uma
  vez** (`SkippedIndex`) e nomeia as peças no log; a ordem termina pelo caminho normal.
- **Contadores honestos** (`MHR_Build.SkipPiece`, `:602`): todo abandono passa por um lugar e `Placed`/`Skipped` são
  **conjuntos de índices** — uma peça dada por perdida que depois subiu conta como colocada, nunca como as duas
  (`MarkDone` tira do `SkippedIndex`, `:478`).

### 17.3 A cobertura de bancadas (medido no mesmo run)

- **O problema:** `PlanStations` parava depois de **6 buscas** e a casa de 32 m ficava com **2 bancadas** a ~18,5 m do
  centro — o meio do plano caía em `needs_station` no meio da obra (e, num grupo, a bancada de socorro vai para o
  **fim** do plano, então aquelas peças esperavam a casa toda).
- **Agora** (`MHR_Build.cs:2192`): orçamento **por bancada** — até **12 planejadas** (`MaxPlannedBenches`, `:100`) e
  **32 buscas** (`MaxBenchSearches`, `:104`) — e a busca só tenta **fora do footprint**, no lado da peça
  (`FindBenchSpotOutside`, `:2409`): o anel perto, o anel longe (~1,5× ou +2 m) e **±40° no anel de longe** (até **4
  âncoras**; as giradas ficam no anel de longe porque 40° no raio curto de um quadrado de 32 m cai **dentro** dele).
- **A bancada externa fica no terreno** (`FindBenchSpot(..., onGround: true)`, `:2368`): a tolerância antiga de 3 m
  recusava bancada em barranco (o chão de um sítio de 32 m fica metros abaixo do piso). Cada busca custa **36** checagens
  (9 posições × 4 yaws) em vez de 64.
- **Uma bancada planejada cobre a peça** sem gastar busca (`CoveredBy`), e a bancada de socorro (`InsertBenchStep`,
  `:2288`) continua: com ≥2 workers, anexada ao **fim** do plano (índice de ninguém anda).
- **Resumo honesto no fim da passada** (`:2259`): `station: N peça(s) precisam, M planejada(s), K sem cobertura`, e cada
  peça sem cobertura é nomeada com prefab e pose, separando **"nenhum lugar fora do footprint"** de **"orçamento
  esgotado (M/12 planejadas, S/32 buscas)"**.

### 17.4 O validador: `attached`, `nudge` e o companheiro que bloqueava

- **`attached`** (`BuildProbe.AttachedToStructure`, `:637`): quando o raio vertical da sonda **não acha nada**, a pose
  ainda é aceita se houver collider de `Piece`/`WearNTear` dentro de `AttachedRadius` (`:49`). Só roda quando o raio
  falhou (é raro), então custa um `OverlapSphere` nas poses suspeitas. O apoio de verdade continua sendo o veredito do
  **jogo** (`WearNTear` no `Place`/`Verify`) — o probe não inventa apoio, só não recusa por um raio que passou no vão.
  **O raio era menor que o grid (04/11/2026):** com **2,6 m** a esfera não achava a vizinha do telhado — o grid de
  telhado é de ~2 m e a peça mais próxima de uma `wood_roof_45` fica na **diagonal, a ~2,8–3,5 m**. Medido no run: o
  `log ajustado` ficou em **0** e a cumeeira continuou presa em `no_ground`. Agora são **4,0 m** (`:49`), e o custo é o
  mesmo (a esfera só roda quando o raio falhou) — **medido depois: `no_ground` ~25 → 0** (§17.6, Run 1).
- **`nudge`** (`NudgePiece`, `MHR_Build.cs:3910`): **qualquer peça** recusada pelo **lugar** (`too_steep`/`no_ground`)
  ganha **uma** chance na pose válida mais próxima (`BuildSite.Find`), desde que caiba no `MaxPlace` (plano e olho). Só
  a pose de **trabalho** muda (`state.Pos`/`state.Rot`); o plano guarda a original, então um retry futuro tenta de novo
  de lá. É a resposta ao barranco que o nivelamento não alcançou (a enxada cobre 20 m e o sítio tem 32 m).
  **Até 04/11 ele só valia para peça de chão (`m_groundOnly`/`m_groundPiece`)** — e o run mostrou baús e uma cama em
  **y 39,9** (e 43,9) recusados com `too_steep` que **nunca eram ajustados**, porque são mobília e não peça "de chão".
  Agora o raio vem do tipo da peça (`AI/BuildPieceKind.cs`, seção **(q)** do harness): **estrutural** (piso, parede,
  telhado, viga, pole, escada, porta, portão) só anda **≤ 1 m** — 2,5 m fora do plano e a parede/telhado não fecha com
  a casa, e o resultado é conferido em XZ (`:3924-3932`); **mobília** (baú, cama, fogueira, bancada, fermentador) vai
  até **2,5 m**, porque a pose dela no blueprint é **sugestão**. Nome vazio/desconhecido fica do lado seguro
  (`Structural` para vazio, `Furniture` para nome não reconhecido — a peça ainda sobe).
- **`blocked_by_player` era companheiro contra companheiro (04/11/2026):** o teste de personagem
  (`BuildProbe.BlockedByCharacter`, `:705`) contava **companheiro como jogador**, e no canteiro os três trabalhavam
  lado a lado da peça — **17+** recusas no run. Agora quem tem `CompanionAI` é **ignorado** (`:714-717` e `:729-732`):
  o jogo **empurra** um NPC, não recusa a peça. O teste contra um **jogador de verdade** continua igual.

### 17.5 Gate do ciclo

Harness **601 PASS** (471 até a **(o)** + **23** da **(p)** — `AI/BuildFailure.cs` — + **26** da **(q)** —
`AI/BuildPieceKind.cs` — + **66** da **(r)** — `AI/BuildSupportBelow.cs`: `CanSupport`, o `PickPost`, a
`UsefulSurface` e o `HoldsPlacedPiece` — + **15** da **(s)** — `AI/BuildBody.cs`, a janela do corpo; os quatro arquivos
linkados no `ai-intents-check.csproj`), MCP **107 PASS**, **config 361/25** (medido com
`python tools/gen_config_doc.py --check`), ES/FR/IT **0 lacunas**, dívida do `gen_translations.py` **45** e build
**0/0**.

### 17.6 O resultado dos três runs (04/11/2026)

Os três runs são a mesma casa (`CommHomeBasicBP`), os mesmos 3 companheiros, `BlueprintFree = true` e o lote do
validador já no código (4,0 m, `NudgePiece` universal, companheiro fora do `blocked_by_player`). A diferença entre os
dois primeiros é `[Building] PlaceReach`: **10** (o padrão do código) × **15** (valor de teste, mudado só no `.cfg` —
o **default continua 10**, `ModConfig.cs:948`, faixa 5..20). O **Run 3** roda em 15 e já com a **guarda anti-loop** do
§17.8.

| | Run 1 | Run 2 | **Run 3** |
|---|---|---|---|
| `PlaceReach` | **10** | **15** (config) | **15** (config) + guarda |
| colocadas | **970** | **972** | **970** (`969` `done` no log) |
| fora (puladas) | **15** | **13** (watchdog) | **16** (watchdog, **nomeadas**) |
| total fechado | **985** (`970 peças, 15 puladas`) | passada principal **987/987** | **986** (`970 peças, 16 puladas`) |
| mortes | **1** | **0** | **0** |
| log | — | — | **382 KB** (sem loop) |
| `no_ground` | **0** (era ~25) | — | — |
| `blocked_by_player` | **0** (era **17+**) | — | — |
| `log ajustado` (nudge) | **6** | — | — |
| `too_steep` | **4** | — | — |
| `no progress` | **69** | — | — |
| watchdog | `15 peça(s) sem apoio` | `13 peça(s) sem apoio` | **16 peça(s) sem apoio** |

- **O que fechou no Run 1:** o `no_ground` da cumeeira **zerou** (~25 → 0) com o `AttachedRadius` de **4,0 m**, o
  `blocked_by_player` **zerou** (17+ → 0) com o companheiro fora do teste de personagem, e o `log ajustado` **6** é o
  `NudgePiece` universal agindo (antes o ajuste era 0). Os contadores fecham: **970 + 15 = 985**, que é o total do
  plano, e a fala final diz **"970 peças, 15 puladas"** (`A {0} está de pé: {1} peças, {2} puladas.`).
- **Run 2 (só config):** `PlaceReach = 15` levou a passada principal a **987/987**, com **972 colocadas** e **0
  mortes**. O canteiro tinha **984 peças** no raio (inclui a base do jogador) e o `PlanStations` **insere bancadas** no
  plano (`MHR_Build.cs:2250`), então os denominadores (984/985/987) não são o mesmo número por construção — os valores
  acima são os do log, `Placed`/`Skipped` contam **índices**.
- **Run 3 (04/11/2026, com a guarda anti-loop do §17.8):** a fala final foi **"970 peças, 16 puladas"** = **986** (o
  plano ganhou **1 bancada** em relação ao Run 1), **969** linhas `done`, **0 mortes**, **382 KB** de log (o loop do
  `290afc1` valia 344 MB) e **976 peças** no raio do canteiro (inclui a base do jogador). As **16** ficaram
  **nomeadas** pelo watchdog (`sem apoio: <prefab> em (x, y, z)`, `MHR_Build.cs:2021`) — todas em **colunas verticais**:
  `wood_beam_45` em **y 37,8 e 39,8** (oeste, ~(-175, -427,7)); `wood_floor` em **39,8 e 43,8** + `wood_roof_45` em
  **41,8/45,8** (leste, ~(-157 a -166, -419 a -429)); e **4 baús** em **(-157 a -159, 39,9, -419 a -420)**. A **visão**
  (`render_view`, duas capturas) confirmou o diagnóstico no espaço: telhado **quase completo**, com **buraco no lado
  leste** (a água norte perto da cumeeira e a empena leste) — as colunas que faltam. Causa e correção: **§17.9**.
- **As 13 que sobraram no Run 2, com a causa de cada uma:**
  1. **2 baús** (`piece_chest_wood`) em **(-168,2, 39,9, -434,9)** — `too_steep`: o nudge de mobília tem **2,5 m** e
     não achou pose válida nesse raio (é o achado que motivou o lote seguinte).
  2. **1 bancada** (`piece_workbench`) em **(-178,3, 38,6, -431,7)** — **`clipping`**: a âncora externa encontrada
     (`FindBenchSpotOutside`/`TryBenchAt`, `MHR_Build.cs:2409`/`:2445`) colide com algo que o `Ok(check)` não estava
     rejeitando (o `TryBenchAt` não olha `clipping` hoje).
  3. **≈10 do andar de cima** — `wood_floor`/`wood_roof_45` em **43,8/45,8**, `no_support` **até o watchdog**: a
     cadeia de apoio do andar de cima não fecha (o piso/telhado de cima não tem, no momento da tentativa, o que o
     sustenta — e o portão de apoio, corretamente, não gasta tentativa: espera até o watchdog).
- **O lote seguinte entrou no `290afc1`** (harness **539** naquele build): nudge de **mobília até 5 m** com **3 anéis ×
  12 direções** (`NudgeFurnitureRadius` = 5, `MHR_Build.cs:126`, com o orçamento `NudgeChecksWide` = 37 × 4 em `:121`;
  o estrutural fica no **1 m** de antes, `:129`), `TryBenchAt` **rejeitando `clipping`** e a **cadeia de apoio do andar
  de cima** (o apoio imediato — que na 1ª versão virou loop e travou o jogo, §17.8). Esses três são exatamente as 13
  peças acima e o **primeiro run com eles é o de §17.10**: as **6 nomeadas** de lá são todas **telhado em 42,8/44,8/46,8**
  (nenhum baú e nenhuma bancada entre elas) e a **empenha** que sobrou é outro caminho, o de `CanSupport`.

### 17.7 O que fica pendente

- **As 13 peças do Run 2** (2 baús `too_steep`, 1 bancada `clipping`, ~10 pisos/telhados de cima `no_support`): o lote
  do `290afc1` (§17.6) atacou os três e o run de **§17.10** é o primeiro com ele — os baús e a bancada não voltaram a
  ser nomeados pelo watchdog, e o que sobrou de lá foi a **empenha** (6 telhados de 42,8/44,8/46,8), corrigida neste
  ciclo (§17.10) — **aceite medido ainda não**.
- **A empenha e o `falls` (§17.10):** as 6 do telhado (telhado apoia telhado, `CanSupport`) e as 4 dadas por `falls`
  (uma segunda tentativa em pose ajustada a 1 m) **ainda não têm run** com o código novo.
- **Run com material pago** (sem `BlueprintFree`): o P1 nunca foi medido pagando peça por peça com 3 workers; a
  **reserva de material** continua não existindo (cada worker conta mochila+baús por conta própria → pior caso N× o
  resto do plano).
- **Grupo em memória** (não persiste) e **só de blueprint** (template/peça seguem com um).
- **Guarda de peso antes do `HandOver`** — a pendência do §16 continua aberta.

### 17.8 O incidente do apoio imediato (04/11/2026) — a lição

- **O que era (build `290afc1`):** o apoio imediato por índice — `MHR_Build.PullDirectSupport` (`:3048`) escolhendo o
  **apoio direto** de uma peça recusada com `no_support` (`AI/BuildSupportBelow.cs`, a janela de 0,4–2,5 m **abaixo** ×
  2,5 m em XZ, seção **(r)** do harness) e **entrando nele fora de ordem**, sem tocar no plano. A justificativa
  teórica estava no próprio código (`:3045`): "a queda é sempre positiva, então o pull só desce o plano e **não pode
  entrar em loop**".
- **O que aconteceu no run de 3 companheiros:** a teoria não sobreviveu. Peça recusada por `no_support` → o worker
  puxa o apoio direto → **o apoio falha** → a peça volta pela fila → é recusada de novo → **puxa de novo** — sem nunca
  sair do ciclo. Em **~10 minutos** o log chegou a **344 MB**, com **1.403.973** linhas `sem apoio ainda` e
  **332.967** `apoio imediato`; os três companheiros ficaram parados com o índice congelado (**434/930/722**) e **o
  jogo travou** — a sessão teve de ser encerrada. O log foi preservado e truncado em
  `%TEMP%\opencode\log-loop-290afc1.log`.
- **A correção (build `797003c`):** `State.PulledSupport` (`HashSet<int>`, `MHR_Build.cs:209`) com a guarda em
  `:3064` — o apoio é puxado **uma vez por índice de peça** e no máximo **24 por ordem**:

  ```csharp
  if (!state.PulledSupport.Add(state.Index) || state.PulledSupport.Count > 24)
  {
      return false;
  }
  ```

  Na **segunda** recusa da mesma peça o pull **não** acontece: ela segue o caminho normal do portão — volta como
  **dependência** (espera **8/12/20/25 s**, sem gastar tentativa) e, se nada subir em **60 s**, o watchdog a deixa de
  fora nomeando-a. Build **0/0** e o gate **daquele** ciclo em **539** (471 + 23 (p) + 26 (q) + **19** da **(r)**, com
  `BuildSupportBelow` linkado — a seção foi a **29** casos no §17.10). **Medido no run seguinte:** o log ficou em
  **30 KB nos primeiros 20 s** (o loop escreveu ~344 MB em ~10 min).
- **Por que o watchdog de estagnação não salvou:** ele só é **lido** em `WaitRetry` (`MHR_Build.cs:1904`), e no loop o
  worker **nunca chega a esperar** — sai da recusa direto para o apoio puxado; o relógio do canteiro
  (`LastProgressAt`) nunca era consultado. Pior: cada volta **escrevia log**, então o loop piorava o estrago em vez de
  se auto-limitar.
- **A lição (vale para qualquer gancho novo):**
  1. **Gancho que reordena o trabalho por índice precisa de guarda de idempotência por peça e de um teto por ordem.**
     "A geometria garante que não repete" não é guarda — foi exatamente a hipótese que caiu.
  2. **O watchdog de estagnação não protege contra loop que não espera** (e que continua gerando log): ele olha
     `LastProgressAt` e só é consultado no caminho de espera.
  3. **Log de diagnóstico dentro de caminho que pode repetir por frame precisa de dedupe/rate-limit próprio** —
     `[Build]` escreve por evento; num ciclo de recusa→pull ele virou 344 MB.

### 17.9 Do clamp ao deck: `DeckLift` + estacas assentadas (04/11/2026)

- **O problema (as 16 do Run 3, medidas):** o plano ancora pela **origem do arquivo** do blueprint — `centre` é o centro
  em XZ com **`y = 0`** (`MHR_Build.cs:1043`) e a pose sai de `hint + rot·(piece.Pos − centre)`
  (`:1069`) —, então o **Y do arquivo** é somado ao Y da mira *como está*. Na `CommHomeBasicBP` a fundação está em
  **y 37,8** e o terreno do sítio é plano na altura da mira: **1 m abaixo do chão**. O jogo **recusa/destrói peça
  enterrada**, e o que estava acima dela falha em **cascata** de `no_support` — exatamente as colunas verticais
  nomeadas no §17.6 (`wood_beam_45` em 37,8/39,8 a oeste; `wood_floor` 39,8/43,8 e `wood_roof_45` 41,8/45,8 a leste) e
  os 4 baús em 39,9. Nenhum ajuste de alcance ou de bancada resolveria: **a base da casa não existia**.
- **A 1ª correção (`0f13878`) e por que ela caiu:** um **clamp por peça** — `if (placed.y < GetGroundHeight(placed) −
  0,05) placed.y = ground`. Ela tirava a peça enterrada de baixo do chão, mas **deixava o piso do térreo no nível do
  chão**, e o jogador não queria isso: *"a obra inteira deve subir um pouco, apoiada nas estacas, tipo um deck"*.
- **O desenho final (substitui o clamp):** a **obra inteira sobe `[Building] DeckLift` metros** e cada peça que ainda
  ficaria abaixo do terreno **assenta no chão** — um bloco único, `MHR_Build.cs:1102-1124`:

  ```csharp
  float lift = ModConfig.BuildDeckLift != null ? ModConfig.BuildDeckLift.Value : DeckLiftDefault;  // :1104, :111
  for (int i = 0; i < plan.Count; i++)
  {
      Vector3 pos = plan[i].Pos;
      pos.y += lift;
      if (ZoneSystem.instance != null)
      {
          float ground = ZoneSystem.instance.GetGroundHeight(pos);
          if (pos.y < ground - 0.05f)
          {
              pos.y = ground;   // a ponta de estaca pisa no chão em vez de sumir sob ele
              grounded++;
          }
      }
      plan[i].Pos = pos;
  }
  ```

  `DeckLift` é float, **padrão 1**, faixa **0..3** (`ModConfig.cs:949`; rótulo `hc_cfg_building_decklift`, "Deck
  height" no inglês). **0** mantém o nível do arquivo (mas a peça enterrada continua assentando — é o que o jogo
  recusa). O log resumo é `a obra sobe X m (deck) - N peça(s) de fundação assentadas no terreno` (`:1124`).
  Por que **não** seguir o ponto mais baixo do plano: as estacas do arquivo estão enterradas **2 m** sob o piso, então
  "subir pelo mais baixo" levantava a casa inteira **2,00 m** (medido no run de 04/11) — um deck, não uma torre.
- **Alcance:** o bloco roda quando o **plano é montado** (`StartBlueprint`); a **retomada** reusa o plano guardado
  (`storedPlan`) e herda o que já foi calculado. Os baús/camas de 39,9 do Run 3 (mobília, `too_steep`) continuam
  dependendo do nudge — o deck é outro assunto.
- **O que o run devia mostrar (e o que o §17.10 mediu):** o **piso do térreo ~1 m acima do chão**, apoiado nas estacas e
  com a escada alcançando — **confirmado**; `N peça(s) de fundação assentadas` **> 0** no log (as pontas que iam abaixo
  do chão); as 16 do
  Run 3 fora de `sem apoio: <prefab> em (x, y, z)` (as colunas do lado **leste** fecham o buraco da visão) — o que
  sobrou foi a **camada de cima do telhado**, com causa própria (§17.10); e nenhuma
  peça enterrada recusada. Build **0/0** e harness **539** naquele build; **aceite medido ainda não**.
- **Medido no run seguinte (04/11/2026):** o deck está **certo** (piso do térreo **~1 m** acima do chão, escadas das
  entradas encontrando) e o **plano abaixo do terreno** deixou de ser o problema — o que falta agora é a **empenha** e
  as 4 peças perdidas por `falls`: **§17.10**.

### 17.10 A empenha e o `falls` (04/11/2026)

**O run (o primeiro com o deck):** a mesma `CommHomeBasicBP`, 3 companheiros, `BlueprintFree = true` e
`[Building] DeckLift = 1` (o padrão), já com o lote do `290afc1` (nudge de mobília **5 m** em 3 anéis × 12, `TryBenchAt`
recusando `clipping`) e a guarda anti-loop do §17.8. Resultado: **~972 peças de pé**, **0 mortes**, e o **deck
confirmado** — o piso do térreo fica **~1 m acima do chão**, apoiado nas estacas, e as **escadas das entradas
encontram** o piso (o aceite que o §17.9 devia). O que sobrou **não** é mais o plano abaixo do terreno: **2 dos 3**
workers terminaram com o watchdog em `0 peça(s) sem apoio` e o **terceiro** parou com **6 peças nomeadas**
(`sem apoio: <prefab> em (x, y, z)`), mais **4** peças dadas por **`falls`**.

- **A imagem (`render_view`):** a **faixa de telhado da água sul** faltando e a **empenha leste aberta** — buraco na
  **camada de cima**, não mais a cascata de colunas do Run 3 (§17.6): o que está embaixo está de pé.
- **O diagnóstico das 6 (medido):** as 6 estão em **y 42,8 / 44,8 / 46,8** — as **fileiras de cima da empena/da água**.
  Numa empena a fileira de cima é sustentada pela **de baixo**, que também é **telhado** (`wood_wall_roof_45`/`_top` caem
  na **classe 3** de `BuildBlueprintParser.ClassOf`, `AI/BuildBlueprintParser.cs:295-298`). O `DirectSupportIndex`
  (`MHR_Build.cs:3177`) — o que o `PullDirectSupport` (`:3143`) pode puxar — **excluía toda peça de classe 3** como
  apoio: só piso/parede/viga/escada podiam ser puxados. Para essas 6 **não havia apoio a puxar**: o pull devolvia `-1`,
  a peça voltava pela fila como **dependência** (sem gastar tentativa, §17.2) e o watchdog a deixava de fora aos **60 s**.
  Ou seja: **a empenha não fecharia nunca** por esse caminho, por mais tempo que se esperasse — não era falta de
  tentativa, era o candidato nunca sendo considerado.
- **A correção (1): telhado apoia telhado** — `BuildSupportBelow.CanSupport(pieceClass, candidateClass)`
  (`AI/BuildSupportBelow.cs:145`), chamado em `DirectSupportIndex` (`MHR_Build.cs:3177`, a chamada em `:3201`) **depois**
  do `BuildPieceKind.IsStructural` (`:3197`):
  - **telhado (3)** é apoiado por **telhado (3)** — a fileira de baixo da mesma água, o caso medido —, por **piso (0)**
    (o `roof` em 46,8 sobre o `floor` em 44,8, medido), por **parede/viga (2)** e por **escada (1)**;
  - **piso (0) e parede (2) não são apoiados por telhado** (um telhado acima não segura nada) — é o que impede o pull de
    puxar um telhado para uma parede que ainda não subiu;
  - **porta (4) nunca apoia nada** — e a **porta como peça** continua podendo ser apoiada por parede, então "portas por
    último" segue valendo;
  - **mobília nunca entra**, e não é o `CanSupport` que a barra: o `BuildPieceKind.IsStructural` já a corta antes
    (`:3197`) — baú/cama sob um piso não é o que o jogo lê como apoio;
  - **a janela (0,4–2,5 m abaixo × 2,5 m em XZ) e o `Rank` (a queda pesa 10×) não mudaram** — o que mudou é **quem**
    pode ser candidato.
- **A correção (2): `falls` com uma segunda chance** — as **4** foram dadas por perdidas com `falling from the way to
  the ... - skipping it (2 fall(s))` (`MHR_Build.cs:3836`): o companheiro subia e caía **duas vezes** no caminho até a
  pose (topo de empena sobre o vazio) e a peça era pulada na hora, sem tentativa nenhuma em outro lugar. Agora, antes
  de desistir, ele **ajusta a pose uma vez, até 1 m** (`AdjustFallenPose`, `:4172`, chamado em `:3830`) — o caminho é
  **só de blueprint** (`state.Blueprint != null`, `:3825`), o mesmo do skip que ele substitui. O `State.FallAdjusted`
  (`:217`) guarda o **índice do plano** — como o `PulledSupport` (`:213`) —, então é **uma tentativa por peça**; a
  queda é da **caminhada**, então repetir o ajuste a cada duas quedas só o mandaria no mesmo buraco. O raio é o do
  **nudge estrutural** (`NudgeStructuralRadius` = **1 m**, `:129`: 1 m ao lado é a mesma casa) e a busca é a **mesma
  metade de código** do `NudgePiece`: `AdjustPose` (`:4200`) — `BuildSite.Find` 2 anéis × 8, teto em XZ do próprio raio,
  regra do olho do jogo (`MaxPlace`) e a linha de log. O `NudgePiece` (`:4148`) passou a **delegar** nela (`:4160`),
  então os dois caminhos (nudge e queda) não podem divergir. O log é o do nudge com o status novo:
  `log ajustado: <prefab> de (...) para (...) - falls`; o `BestDistance`/`BestAt` da caminhada **zeram**
  (`:4188-4189`) para a trava de "12 s sem chegar mais perto" não punir o alvo novo, e o `FallDrops` é zerado no
  caminho ajustado (`:3832`) — a peça ganha um par novo de quedas para o ajuste valer. **Sem pose válida** (fora do
  alcance do olho, todas as direções ocupadas, o `BuildSite.Find` recusando) ele desiste **como antes**: o ajuste não
  esconde a peça inalcançável.
- **O que ler no run (o aceite que falta):**
  1. a **empenha leste fecha** e a **água sul** ganha a faixa: as 6 de **42,8/44,8/46,8** saem de
     `sem apoio: <prefab> em (x, y, z)` e aparecem como `done: <prefab> ... supported`;
  2. **`log ajustado ... - falls`** aparece **antes** de um `skipping it` — é o ajuste agindo nas 4; a peça que cair de
     novo **depois** do ajuste é pulada como antes (não há terceira chance);
  3. watchdog idealmente `0 peça(s) sem apoio` nos **3** workers (2 fecharam assim neste run);
  4. o **deck** segue como medido (piso ~1 m, escadas encontrando), com
     `a obra sobe 1 m (deck) - N peça(s) de fundação assentadas no terreno` no log.
  Build **0/0** e harness **549** **naquele build** (**539** + as **10** novas da seção **(r)**, então **29** casos:
  `CanSupport` com telhado sobre telhado, telhado no piso/parede/escada, piso e parede **não** apoiados por telhado, a
  porta que nunca apoia e a porta **como peça**; hoje o harness é **601** — §17.11/§17.12 e o lote do poste no §17.14);
  **aceite medido ainda não**.

### 17.11 O apoio de emergência (`[Building] SupportFallback`, 04/11/2026)

- **O que é:** a resposta para a peça do plano recusada com `no_support` **quando o plano não tem nada embaixo dela** —
  `DirectSupportIndex(state) < 0`, o caso em que o pull do apoio imediato (§17.8) responde `-1` e a peça só podia
  esperar o watchdog. O companheiro **finca um poste vertical sob ela**, com o **topo tocando a base da peça** e a
  **base na superfície de baixo** (o chão, o piso do andar de baixo, o topo de uma viga), validado pelas regras do
  **jogo** (`BuildSite.Check`: chão, estação no raio, sem clipping), **pago** como qualquer peça (`BuildStock.Take` —
  baús/mochila; de graça com `BlueprintFree`) e colocado pelo executor. A peça recusada **mantém índice, claim e
  pose** e sobe em cima do poste no frame seguinte — a coluna fecha **de baixo para cima** (o piso de 40,8 ganha um
  poste no chão, o seguinte um poste sobre aquele piso, e assim por diante).
- **O bug da 1ª versão (medido no run de 5 companheiros):** o poste era um `wood_beam` fixo de **2 m com o centro em
  `y − 1`** — então, para uma peça ~1 m acima do chão, a **base ficava 1 m enterrada** e o **jogo recusava o próprio
  poste** (`sem lugar para o apoio de emergência … - no_support`). Naquele run o `apoio imediato` disparou **24×**
  (puxando o piso de baixo), mas a **coluna terminava sem base** e o `apoio de emergência` ficou em **0** — todas as
  tentativas morreram com a base enterrada.
- **O desenho novo (`TryFallbackPost`, `MHR_Build.cs:3243`): primeiro a superfície, e ela não é a primeira que o raio
  acha.** O raio desce de `piece.y + 0,05` por **`PostReach` = 10 m** (`:92`), mas **em laço**: cada passada que acha
  uma superfície **mais perto que `MinPostGap` = 0,4 m** (`:97`) é **atravessada**, e a próxima começa **5 cm abaixo do
  hit** (`:3292`), até **`MaxPostPasses` = 4** (`:101`) ou o fundo do alcance. Os vãos medidos vão para um buffer fixo
  (`s_postDepths`, `:105`, sem alocação por recusa) e a decisão é **pura**: `BuildSupportBelow.UsefulSurface(depths,
  holds, count, minGap)` (`AI/BuildSupportBelow.cs:139` — assinatura do lote de 11/11/2026, §17.14) devolve o **primeiro
  vão que serve** — fundo (≥ 0,4 m) **e** que segura — ou `-1` (aí usa o
  `GetGroundHeight`, fail-open). **Motivo medido:** **318 recusas** com `vão de 0,16 m` — uma **beira de telhado colada**
  sob o piso de 40,8 parava o raio, e o apoio útil era o **piso do deck a 1,02 m** embaixo.
- **O vão e a escolha do poste:** `gap = piece.y − baseY` (`:3321`); o poste é o **mais curto que alcança**,
  `BuildSupportBelow.PickPost` (`AI/BuildSupportBelow.cs:80`) — até **0,20 m mais curto** (`ShortTolerance`, `:107`: a
  caixa de apoio do jogo é crescida em 0,15 m) e no máximo **0,45 m mais longo** (`MaxOverlap`, `:119` — era **0,35**
  até o lote de 11/11/2026, §17.14; com 0,45 o vão medido de **0,60 m** passa a caber). O centro é
  `baseY + comprimento/2` (`:3337`) — é o que põe a base **na superfície** em vez
  de enterrá-la. Candidatos, na ordem (`FallbackPostPrefabs`, `:79`): `wood_beam_1`, `wood_beam`, `wood_pole2`,
  `wood_beam_45`, com o comprimento **medido dos snaps de cada prefab** (`MeasurePosts` `/`PostAxis` — uma vez só, em
  cache: um `no_support` não pode varrer prefab por frame).

| prefab | comprimento | origem | fonte |
|---|---|---|---|
| `wood_beam_1` | **1,00 m** | centro | sem medição no repo (nome/prefab; o harness usa 1 m) |
| `wood_beam` | **2,00 m** | centro | sem medição no repo (o valor que a 1ª versão usava) |
| `wood_pole2` | **2,00 m** | **centro** (snaps `(0, ±1, 0)`) | §11 + `build.inspect` (bounds 0,42 × **2,00** × 0,42) |
| `wood_beam_45` | **2,83 m** | centro | §11: snaps `(-1,-1,0)`/`(1,1,0)` → diagonal √8 |

  **Não existe peça vertical de ~4 m em vanilla**, então um vão acima de ~3,28 m (2,83 + 0,45) **não recebe poste** — e
  empilhar dois postes está fora do desenho: a peça segue o caminho normal (fila/watchdog).
- **As guardas:** **uma vez por peça** (`State.FallbackPosts`) e **teto de 24 por ordem** (`MaxFallbackPosts = 24`,
  `:115`); **peça não estrutural não recebe poste** (`BuildPieceKind.IsStructural`, `:3266`); **nunca** onde o plano tem
  apoio (`:3275` — ali o caminho é o pull).
- **Os logs:** sucesso `apoio de emergência: <prefab> (<len> m) sob <peça> em (x, y, z)`; recusas
  `sem lugar para o apoio de emergência sob <peça> em (…) - vão de X,XX m` (`:3326-3328`, nenhum poste alcança — e o
  sufixo **`(N superfície(s) rasa(s) ou sem apoio ignorada(s))`** diz quantas o laço atravessou), `… - <status> (vão X,XX m)`
  (o `BuildSite.Check` recusou a pose por outra razão que não o apoio), `sem material para o apoio de emergência …` e
  `… - <erro> (vão X,XX m)` (o executor falhou — a
  madeira volta para a mochila).
- **A config:** `[Building] SupportFallback` (bool, **padrão true**, `ModConfig.cs:953`; rótulo
  `hc_cfg_building_supportfallback`, "Emergency support" no inglês — `ModLocalization.cs:843`). Desligada, a peça só
  espera e fica de fora, como antes.
- **A regressão do afundamento (revertida no mesmo lote):** o lote anterior tinha feito `BlockedByCharacter`
  (`BuildProbe.cs:705`) **ignorar companheiro** para zerar o `blocked_by_player`. O efeito colateral apareceu no run de
  5: a peça era colocada **dentro** do companheiro e ele **afundava**. O ignore foi **revertido** — hoje o companheiro
  **é obstáculo** de novo, o `blocked_by_player` **voltou a aparecer** (**20** no run de 5) e é **retry, não perda**: é
  falha **real** no portão (`AI/BuildFailure.cs:39`, gasta uma das 3 tentativas), a peça volta para a fila e sobe quando
  o companheiro sai de cima — o que não acontece mais é a peça nascer dentro dele.
- **A janela do corpo (`AI/BuildBody.cs`, seção (s) do harness):** o teste de 0,6 m era **ponto × pés** e deixava um
  piso **1 m acima da cabeça** passar (com o deck de 1 m, dois companheiros foram **espremidos 1 m abaixo do chão** e
  travaram — relato "dois companions travados embaixo do piso"). Agora o **corpo é uma coluna** (pés −0,5 m até +2,0 m,
  `Below`/`Above`) e a **peça é uma caixa**: raio plano da peça **+ `Radius` = 0,45 m** em XZ (`Spacing`, nunca menos que
  os 0,6 m do probe) e o vão vertical da peça em Y (`Blocks`); quem está **ao lado** ou **acima da cabeça** não bloqueia.
  `BlockedByCharacter` (`BuildProbe.cs:720`) passou a receber a peça e usa essa janela para **todo** personagem —
  jogador **e** companheiro (o docstring `:714-718` já explica a reversão do ignore). Harness: **15** casos na **(s)**
  **naquele lote**.
  > **Superado em 11/11/2026 (§17.16):** a "coluna + disco de 0,45 m" era **conservadora demais** — o disco de 1 m
  > ignorava **pivô e rotação** e o **Run 10** pagou por isso (um feixe de 2 m "prendia" um companheiro a 1,08 m, falso
  > positivo). A janela hoje é a **cápsula real** do corpo contra a **caixa girada** da peça, com **toque = livre**
  > (`BuildProbe.CapsuleOf`, `BlockedByCharacter` em `BuildProbe.cs:733-793`, `BuildBody.Blocks`/`HalfY`; **(s)** com
  > **18** casos). A regra que fica desta seção: o teste é o **corpo**, não um ponto, e o **companheiro conta**.
  Efeito no run: `blocked_by_player` **≈770**, porque o **jogador estava dentro do canteiro** e o corpo dele conta de
  verdade.
- **O run de 5 companheiros (antes deste lote):** **972 colocadas + 13 puladas = 985**, watchdog **0**, **0 mortes**,
  `blocked_by_player` **20** (já com o companheiro valendo como obstáculo — a reversão acima), `apoio de emergência`
  **0** e **24** `apoio imediato` (puxadas do piso de baixo): as **13** restantes são justamente as colunas cuja base
  nunca existia, e por isso o poste nunca subiu.
- **O que ela fecha:** as **16** do Run 3 (§17.6 — colunas na **empenha leste** e no **canto noroeste**, 40,8→46,8, + 4
  baús) e as **6** de 42,8/44,8/46,8 que sobraram no run do deck (§17.10), que são exatamente o caso "o autor não pôs
  nada embaixo". **Estado: medido no run de 11/11/2026 (§17.13)** — build **0/0** e harness **601** (596 naquele run;
  o lote do poste é o §17.14); no run, **0 postes
  foram necessários** (não era apoio que faltava) e o telhado fechou — mas **não** por esta regra: o §17.12 mostrou que
  a classe do telhado era **inócua** (o flag é `true`) e que o que travava era **geometria**.

### 17.12 O telhado, o `m_supports` e o degrau de 0,6 m (`HoldsPlacedPiece`) — texto corrigido

> **Correção factual (fonte: `docs/re/support-rule-wearntear.md`, medido em 04/11/2026).** A versão anterior deste § dizia
> que "no Valheim **telhado tem `m_supports = false`**" e que era por isso que 13 peças ficavam em `no_support`. **É falso
> na 1.0.20**, medido com o `build.inspect` do DevBridge — ver §1 e §5 da nota.

- **O medido no run de 11/11:** **13 peças** de telhado (42,8/44,8/46,8) ficaram **5 rodadas** presas em `no_support` e a
  casa não fechava — com a fileira de baixo **de pé** ao lado delas.
- **Os flags, medidos (`m_supports` do `WearNTear` do prefab, o mesmo campo que o `UpdateSupport` lê):**

| prefab | `m_supports` |
|---|---|
| `wood_roof` · `wood_roof_45` · `wood_roof_top` · `wood_roof_top_45` · `wood_roof_icorner_45` | **true** |
| `wood_floor` · `wood_wall_half` | **true** |
| `wood_stair` · `wood_stepladder` | **true** |
| `wood_pole2` · `wood_beam_1` | **true** |
| `piece_chest_wood` (mobília) | **false** |

  **Todas as peças estruturais de madeira — telhados inclusive — são `true`**; só a **mobília** é `false`. (Os bounds Y
  de cada prefab e a confirmação no CIL estão em `docs/re/support-rule-wearntear.md` §1/§5.)
- **Consequência 1 — o bypass de classe 3 é inócuo:** `BuildSupportBelow.HoldsPlacedPiece(pieceClass, isTerrain,
  hasWear, wearSupports)` (`AI/BuildSupportBelow.cs:172`), o `return pieceClass == 3 || wearSupports` de `:182`,
  chamado em `BuildProbe.cs:693`, foi escrito sobre a premissa errada, mas **não mudou comportamento nenhum**: com o
  flag `true` nas duas pontas, "telhado ← qualquer `WearNTear`" e "telhado ← só quem tem `m_supports`" dão o **mesmo
  resultado**. Ele **ficou por segurança** (a intenção — telhado não é mobília — continua válida), e a **única** ponta
  em que ele **não** é inócuo é a **errada**: um telhado apoiado em **mobília** (`m_supports = false`) passa no nosso
  teste; o jogo recusaria pela mira (`Player.cs:3890-3893`) e o telhado cairia de qualquer jeito. As **outras classes**
  continuam exigindo `m_supports` — senão uma parede subiria em cima de um baú — e o que não tem `WearNTear` (pedra,
  árvore, item no chão) não é apoio. **Os docstrings que repetem a premissa errada** são os de
  `BuildSupportBelow.HoldsPlacedPiece` (`:158-171`) e de `BuildProbe.FootprintSupported` (`:659-671`): corrigi-los é
  lote de **código**, não deste doc.
- **Consequência 2 — a causa real das 13 era geometria:** o `no_support` do telhado vinha do **alcance de apoio**, não
  do flag. O `FootprintSupported` (`AI/BuildProbe.cs:672`) testa a caixa do **renderer** (`BoxOf`, `:343`) crescida
  **0,15 m**; numa água a 45° a fileira de baixo fica **~2 m na diagonal**, **fora** dessa caixa (§5 da nota) — nada a
  ver com carregar apoio.
- **A regra do jogo (a que o validador tenta copiar):** o apoio é **geométrico e por collider** — cada collider é
  crescido **0,3 m no total (0,15 m por lado)** em X/Y/Z (`WearNTear.SetupColliders`: `size += 0,3; size *= 0,5`,
  `WearNTear.cs:484-514`) e o `UpdateSupport` (`:823-995`) consulta as layers
  `piece|Default|static_solid|Default_small|terrain`. **A colocação NÃO testa apoio:** o `Player.UpdatePlacementGhost`
  (`Player.cs:3793-4114`) só lê o `m_supports` da peça **sob a mira** — nada ali chama `UpdateSupport`/`HaveSupport`.
  A peça sem conexão (`m_support` 0) é **colocada** e o `UpdateWear` (`:525-699`) aplica **100 % de dano** no tick
  seguinte (~1 s): ela **cai e dropa** os materiais (`:568-575`/`:685-692`). **Não existe "vão generoso"** — acima de
  **0,15 m** de folga entre superfícies **não há conexão nenhuma**.
- **O run de 11/11 do degrau de 0,6 m — aqui o `no_support` estava CERTO:** **8 `wood_floor`** em y **40,4/41,4** — a
  primeira fileira é o **degrau de 0,6 m** sobre o piso do deck em 39,8 (o caso da nota §4: **~4×** o limite de 0,15 m,
  **fora por 0,19–0,35 m**) — mais os **12 baús** que ficavam em cima deles. É o run que **ficou no retry** (diferente do
  §17.13, que fechou em **969 + 18 = 987**): repetia os mesmos índices (`wood_floor` **297/298/304**) e **travou em
  950/987**. Insistir nelas
  seria colocar peça que **o jogo derruba** no segundo seguinte — o `no_support` não escondia bug do validador. **E o
  poste não cobria esse vão naquele build:** com `MaxOverlap` **0,35 m** (`AI/BuildSupportBelow.cs:111` na época), o
  `wood_beam_1` (1 m) só
  alcançava vão de **0,65 a 1,20 m** (`PickPost`, `:80`; os dois números em `:107`/`:119`), e a lista de candidatos do mod
  não tem peça mais curta (`FallbackPostPrefabs`, `MHR_Build.cs:79`). **Isso mudou em 11/11/2026 — §17.14.**
- **Conserto feito, fechado no lote de 11/11/2026 (`ae9ebc3`) — §17.14:** o `MaxOverlap` foi para **0,45**
  (`AI/BuildSupportBelow.cs:119`), então o `wood_beam_1` cobre **`[0,55; 1,20]`** e o **degrau de 0,6 m passa a receber
  poste** (a ligação é **sobreposição de collider**, não comprimento exato; o beam atravessa 0,40 m da peça). O que **não**
  entrou no código é o outro item que o `package/CHANGELOG.md:316` anuncia: **"sólido sem `WearNTear` volta a apoiar"** —
  `HoldsPlacedPiece` continua devolvendo **false** para `hasWear == false` (`:209-212`) e o `PostBaseHolds` passa
  `wear != null` (`MHR_Build.cs:3391`). Divergência **doc público × código**, anotada no §17.14.
- **O que sobra é desenho do plano, não validador** (nota §6): degrau/`DeckLift` **≤ 0,15 m por peça**, ou a
  parede/viga do andar de baixo **no plano** até o piso de cima (o `PullDirectSupport` só puxa peça **do plano**). A
  terceira saída (aceitar `MaxOverlap ≥ 0,40` e fincar um `wood_beam_1` atravessando) **foi a escolhida** e está no código
  desde 11/11/2026 (`0,45`, §17.14); as duas primeiras continuam **decisão do jogador** (o `DeckLift` é dele).
- **Irmão, não gêmeo, do `CanSupport`** (`BuildSupportBelow.cs:145`, a regra do **pull**): lá **telhado só apoia
  telhado** e **porta nunca apoia**; aqui o telhado é apenas **apoiado** por qualquer `WearNTear`. As duas concordam no
  essencial: **porta nunca segura** e **telhado nunca segura um piso**.
- **Harness:** seção **(r)** de **51 → 61** casos (+10 do `HoldsPlacedPiece`; hoje **66** com o lote de 11/11/2026 —
  §17.14). **Ressalva:** os casos `:947` ("telhado
  sobre telhado: a fileira de baixo apoia pela borda") e `:951` ("piso NAO e apoiado por telhado") de
  `tools/ai-intents-check/Program.cs` passam `m_supports = false` numa peça **estrutural**, estado que **não existe** na
  1.0.20 (só mobília é `false`); com o valor real (`true`) o `HoldsPlacedPiece` devolve `true` nos dois — quem barra o
  piso sobre telhado é o **`CanSupport`** (o pull), não o flag. Os casos provam a **regra da classe**, não o bypass;
  ajustar o `why` é do **dono de `tools/**`**.
- **O que a nota declara como não verificado:** a espessura real do collider do `wood_floor` (muda só o "vão máximo
  entre pivôs": 0,41 m pelo renderer, ~0,25 m por uma tábua de 0,1 m) e o ensaio destrutivo (colocar o piso a 0,6 m e
  vê-lo cair) — não feito de propósito, com o run em andamento (`docs/re/support-rule-wearntear.md` §7).

### 17.13 O run de 11/11/2026 (5 companheiros, `DeckLift = 1`) — resultado e a ressalva

- **Números:** **969 colocadas + 18 puladas = 987** (`hc_ai_blueprint_done`), **watchdog `0`**, canteiro com **985 das
  987** peças, **1 morte** e **`apoio de emergência` = 0** — com o poste por vão e a caminhada do raio no lugar, **não
  era apoio que faltava**.
- **O telhado fechou:** a visão (`render_view`) mostrou o telhado **visualmente fechado** — é o aceite do §17.12 (as 13
  de 42,8/44,8/46,8 que passavam 5 rodadas em `no_support`).
- **`blocked_by_player` ≈ 770**, e o motivo é o §17.11: o **corpo conta** de verdade (a janela do `BuildBody`), e o
  **jogador estava dentro do canteiro** — cada peça perto dele volta para a fila (retry real) até ele sair.
- **Ressalva honesta (estado atual da casa):** depois do run o arquiteto **apagou 41 peças do piso do deck**
  (**25 + 16**) para **libertar o jogador e os companheiros presos no vão de 1 m do deck**. Ou seja, o que estava no mundo
  logo depois do Run 6 **não** tinha o piso íntegro da `CommHomeBasicBP`. **Atualização (Run 8, 11/11/2026):** o plano
  inteiro foi recolocado no mesmo canteiro (**987/987** índices — §17.14), então o piso apagado **voltou**; a ressalva
  fica como história do Run 6, não como estado de hoje.
- **Pendência aberta — o vão do deck (`DeckLift = 1`):** a folga de ~1 m entre o piso do deck e o chão prende corpos
  (foi o mesmo caminho do relato "dois companions travados embaixo do piso", §17.11). As três saídas em discussão, e a
  decisão é **do jogador**:
  1. **`DeckLift = 0`** — sem deck: acaba o vão, acaba a casa "apoiada nas estacas";
  2. **saia em volta** (fechar o perímetro do deck, transformando o vão num porão fechado);
  3. **regra de rota** no mover: não entrar/parar sob o deck (o companheiro contorna, o vão deixa de ser armadilha).

### 17.14 O lote do poste (11/11/2026, `ae9ebc3`) — os furos medidos e o que mudou

- **O número que abriu o lote:** o **Run 7** fechou em **950/987** com **95 recusas** de `sem lugar para o apoio de
  emergência … - no_support`. A leitura errada seria "faltou lugar": o poste era reprovado por um check **genérico** — a
  base que o raio achava era uma **beira de telhado** que o próprio `MinPostGap` manda atravessar (§17.11) —, e os dois
  vãos que o run tinha (**0,60 m** no degrau entre pisos, **1,60 m** entre a janela de 1 m e a de 2 m) caíam nos
  **buracos** do `PickPost` com `MaxOverlap` **0,35**.
- **Furo 1 — a tolerância (`MaxOverlap` 0,35 → 0,45):** `AI/BuildSupportBelow.cs:119`. As janelas por comprimento
  passaram a ser: `wood_beam_1` (1 m) **`[0,55; 1,20]`**, `wood_beam`/`wood_pole2` (2 m) **`[1,55; 2,20]`**,
  `wood_beam_45` (2,83 m) **`[2,38; 3,03]`** (docstring do `PickPost`, `:62-79`). Os **buracos em vanilla** são
  **1,20–1,55 m** e **2,20–2,38 m** (não há peça entre 1 m e 2 m; a de 2,83 m é o degrau seguinte): os vãos medidos
  **0,60** e **1,60 m** agora têm poste, e o **1,30 m** do harness continua **sem** (1 m fica 0,10 m além da tolerância de
  baixo, 2 m passaria 0,70 m). Com 0,45 o `wood_beam_1` **atravessa 0,40 m** da peça que segura — e isso é o jogo, não
  licença nossa: a ligação é **sobreposição de collider**, não comprimento exato (§4/§5 de
  `docs/re/support-rule-wearntear.md`). A divergência **doc × código** que o §17.12 anotou (docstring descrevendo
  `[0,55; 1,20]` com o código em 0,35) **acabou**.
- **Furo 2 — a base tem de segurar (`holds`):** profundidade sozinha não bastava. No Run 7 o **topo de um baú** foi
  aceito como base (a mobília é a **única** classe com `m_supports = false` na 1.0.20, §5 da nota do `WearNTear`), o
  poste subiu sobre o nada e o jogo recusou **o próprio poste** com `no_support`. Agora cada hit do raio carrega o
  veredito num buffer paralelo: `s_postHolds` (`MHR_Build.cs:106-111`), preenchido por `PostBaseHolds` (`:3383-3392`,
  que pergunta o `HoldsPlacedPiece` com a **classe 2 fixa** — o poste é viga/poste, então valem terreno e peça com
  `m_supports`), e o `BuildSupportBelow.UsefulSurface(depths, holds, count, minGap)` (`:121-158`) devolve o **primeiro**
  índice com `depths[i] >= minGap` **e** `holds[i]` (ou −1 sem buffer/flag). A caminhada **atravessa** a superfície rasa
  **ou** a que não segura, e a recusa nomeia o que ficou de fora:
  `- vão de X,XX m (N superfície(s) rasa(s) ou sem apoio ignorada(s))` (`:3325-3328`).
- **Furo 3 — guarda de mobília:** `if (!BuildPieceKind.IsStructural(state.Prefab)) return false;` (`:3263-3269`). Peça
  do plano que **não é estrutural** (baú, cama, fogueira) **não ganha poste**: o apoio dela é o piso do plano, e esses
  prefabs têm `m_noClipping` — o jogo recusaria o poste ao lado como `clipping`, pago e perdido.
- **Furo 4 — o aceite era genérico:** o poste caía no `BuildSite.Check` inteiro. Agora ele passa quando a **única** razão
  é o apoio: `if (!Ok(check) && !(useful >= 0 && OnlyNoSupport(check)))` (`:3340-3347`), com `OnlyNoSupport`
  (`:4951-4966`) exigindo `reasons.Count == 1 && reasons[0] == "no_support"`. A prévia de apoio é a **nossa**
  (o `BuildProbe` lê a peça sob o **centro** e a caixa do footprint) e é **conservadora**; o jogo conecta o poste à base
  por **sobreposição de collider**. Qualquer outra razão (`clipping`, `needs_station`, `ward`, `blocked_by_player`,
  `too_steep`) continua barrando, e se o poste cair a peça segue o **retry/`falls`** normal — sem caso especial. O log do
  executor também diz o vão: `… - <erro> (vão X,XX m)` (`:3363-3364`).
- **O que NÃO mudou (de propósito):** a **regra de apoio do jogo** continua **0,15 m de sobreposição entre colliders**
  (`docs/re/support-rule-wearntear.md` §4/§5); o `BuildProbe.FootprintSupported` (`AI/BuildProbe.cs:676`) está
  **intocado** (mesma caixa crescida em 0,15 m, mesmo `HoldsPlacedPiece`); e `MinPostGap` (0,4 m), `MaxPostPasses` (4),
  `PostReach` (10 m), `MaxFallbackPosts` (24 por ordem) e o "nunca onde o plano já tem apoio" seguem iguais.
  O `MaxOverlap` é **constante de código**, não config: **nenhuma** chave nova no F8 (a desta função continua
  `[Building] SupportFallback`).
- **Divergência que sobra (doc público × código):** o `package/CHANGELOG.md` (11/11/2026) anuncia, no mesmo lote, que
  **"sólido sem `WearNTear`"** (pedra, árvore, estrutura) **volta a apoiar** — **isso não está no código**:
  `HoldsPlacedPiece` continua devolvendo `false` para `hasWear == false` (`AI/BuildSupportBelow.cs:209-212`) e o
  `PostBaseHolds` passa `wear != null` (`MHR_Build.cs:3390-3391`). Quem for medir o poste sobre pedra/árvore precisa
  saber disto. (O `package/**` não é desta frente: fica registrado para o dono.)
- **Docstrings corrigidos no mesmo lote:** `BuildProbe.cs:659-671` e `BuildSupportBelow.cs:183-196` — o motivo escrito
  antes ("telhado tem `m_supports = false`") era **falso**; a regra é **0,15 m de sobreposição** e o bypass de classe 3 é
  **inócuo** com os valores medidos (§5 da nota do `WearNTear`).
- **Testes:** harness **596 → 601**, com a seção **(r)** de **61 → 66** — **3** `PostCase` novos (**0,60 → 0**,
  **1,30 → −1**, **1,60 → 1**), **2** `SurfaceCase` de `holds` (a de 0,5 m não segura e a de 1,2 m segura → índice 1;
  nenhuma segura → −1) e a asserção do número reescrita (`MaxOverlap == 0,45`; ela existia em 0,35 — por isso o total
  sobe **5**, não 6). MCP **101 → 107**: as **6** do `stop` (`tools/mcp-devbridge/test_mcp_devbridge.py:211-231`), que
  também entraram no `docs/mcp-devbridge.md` e no `tools/ai-intents-check/README.md:81,84`.
- **Resultado do Run 8 (medido em jogo, 11/11/2026) — o lote validado:** **987 de 987 índices colocados, `skip` = 0**;
  **39 postes de emergência**, **0 recusas**; **1026 peças** no canteiro; **watchdog `0`**, **`UNSUPPORTED` = 0**, **0
  mortes** e **0 `falls`**; telhado **fechado**, **empena leste fechada**, **porta** e **escada das entradas** no
  lugar nas três visões (`render_view`). Onde cada número veio (as `linha N` abaixo são do `LogOutput.log` **desta**
  sessão de 11/11/2026 — o log cresce, então procure pelo texto, não confie no número da linha):
  - **Montagem do run:** **5 companheiros** num plano só (`grupo de obra "MACCommHomeBasicBP": 5 companheiro(s) no
    mesmo plano (987 peças)`, log linha 167), `BlueprintFree = true`, `[Building] DeckLift = 1`, `PlaceReach = 15`,
    `SupportFallback = true`, `TempScaffold = false` e `LogLevel = Normal` (`AiCompanions.cfg:398/404/409/456/461/135`);
    o **jogador ficou fora do canteiro** o run inteiro (leitura do arquiteto; o `game_status` de agora — (-164,9, 39,5,
    -405,3) — está a ~24 m do centro do plano, (-160,0, 38,8, -428,8)).
  - **O plano:** **987** = **983** peças do arquivo + **4 bancadas** do `PlanStations` (`station: 983 peça(s) precisam,
    4 planejada(s), 0 sem cobertura`, linha 148), com o deck do §17.10 (`a obra sobe 1,00 m (deck) - 2 peça(s) de
    fundação assentadas no terreno`, linha 142).
  - **O fim da ordem:** os **4** primeiros workers saíram calados (`a lista de peças acabou para este companheiro (o
    grupo continua)`, linhas 3878-3881) e o **último** (companheiro 2) fechou com `hc_ai_blueprint_done` (linha 3882)
    — o cursor tinha chegado em `next: wood_gate (987/987)` (linha 3864) e a fila de retry do canteiro estava vazia
    (a ordem só termina depois de `NextRetry`/`RetryPending`, `MHR_Build.cs:877-893`). **Ressalva:** o **número**
    (`placed`/`skipped`) vem do **contador do grupo** (`MHR_BuildGroup.Placed`/`Skipped`, `MHR_Build.cs:903-904`) — o
    `Finish` loga **só a chave** (`:913`) e a fala final **não tocou** (nenhuma linha `[TTS]` depois do fim da ordem),
    então o log **não imprime** `987`/`0`.
  - **As 4 peças que um worker desistiu foram colocadas por outro** (é o que faz o `skip` fechar em 0):
    `desistindo de wood_floor (blocked_by_player)` (-156,1, 39,8, -436,5) na linha 1207 → `done: wood_floor …
    supported` na 1238; `woodwall` (-162,6, 42,8, -422,9) 2526 → 3045; `woodwall` (-161,6, 42,8, -421,2) 2532 → 3075;
    `wood_beam` (-151,1, 44,8, -431,2) 3150 → 3181. O `MarkDone` **tira o índice** do `SkippedIndex`
    (`MHR_BuildGroup.cs:474-478`): peça dada por perdida e depois de pé conta **uma vez, como colocada**.
  - **Os postes:** **39** — **38** `wood_beam_1` (**1,00 m**) + **1** `wood_beam_45` (**2,83 m**, sob um `wood_beam`
    em (-158,8, 41,3, -428,3), linha 2278) — e **0** `sem lugar para o apoio de emergência`. O **degrau de 0,6 m**
    que o Run 7 deixou em `no_support` (§17.12) **subiu**: os `wood_floor` de **40,4** receberam poste nas linhas 1233,
    1250, 1264, 1352, 1365 e 1771 — (-152,3, 40,4, -424,9), (-157,1, 40,4, -438,3), (-150,5, 40,4, -425,9),
    (-164,2, 40,4, -434,5), (-166,8, 40,4, -430,9) e (-158,8, 40,4, -437,3) —, e o mesmo aconteceu **acima** (41,4,
    44,4, 45,4), nos telhados de 42,4/46,4 e em quatro `wood_floor_1x1` de 44,4. **O teto de 24 é por worker**
    (`State.FallbackPosts`, `:259-266`; teste em `:3259`): 39 num canteiro com 5 companheiros **não** é exceção ao teto.
  - **Canteiro:** **1026 peças** = **987** do plano + **39** postes (aritmética dos índices e do log de postes; **não**
    recontei com `build.pieces` nesta nota).
  - **`UNSUPPORTED` = 0:** nenhuma peça de pé perdeu apoio no veredito do próprio jogo (`MHR_Build.Verify`,
    `MHR_Build.cs:4561`) — a busca por `UNSUPPORTED` no log dá **zero** linhas. **Watchdog `0`:** nenhuma linha de
    `sem progresso há 60,0 s` nem de `sem apoio: <prefab> em (…)` (`LogStall`, `:2100-2107`) — o lote não precisou
    deixar peça de fora no fim.
  - **`blocked_by_player` = 120 recusas**, das quais **116** voltaram para a fila do canteiro e **4** terminaram em
    `desistindo` (as 4 acima, todas recuperadas). Como o **jogador estava fora**, o corpo que bloqueia é
    **companheiro**: `BlockedByCharacter` conta jogador **e** companheiro (`BuildProbe.cs:724-753`) e, num canteiro de
    5 workers, o bloqueio é **trânsito interno** — ainda é **falha real** (gasta uma das 3 tentativas), por isso o
    número alto é esperado e não é o mesmo caso do Run 6 (lá o jogador estava **dentro** do canteiro). **`no progress`
    = 11.**
  - **0 mortes e 0 `falls`:** nenhuma linha de morte/respawn no log e nenhuma de queda (`falling from the way …`,
    `MHR_Build.cs:4127`, e `log ajustado … - falls`, `:4522`).
- **As 2 `stone_floor_2x2` (o mistério das "2 peças" desde o Run 6):** o arquivo tem **985 linhas aceitas** e o parser
  **descarta 2** — `blueprint MACCommHomeBasicBP: 2 line(s) skipped, first: stone_floor_2x2 (recipe not unlocked)`
  (linha 143, repetida a cada worker). O descarte é do `MHR_Build.cs:1117-1125` (`Player.IsRecipeKnown(resolved.m_name)`
  falso); **não** é falta de estação. Medido ao vivo com `build.inspect prefab=stone_floor_2x2`: `recipeKnown = false`,
  estação **`$piece_stonecutter`** ("Talhador de pedras", `rangeBuild` **20 m**), **6 pedras**, `m_supports = true`. Ou
  seja: são peças que exigem **receita desbloqueada** (talhador) neste mundo — **limite do arquivo/save, não falha da
  obra**. Isto explica a **conta do arquivo** (985 linhas − 2 = 983 peças → + 4 bancadas = **987** índices), que
  aparecia como "2 peças" não explicadas desde o Run 6. **Ressalva de leitura:** o **985 do canteiro** do Run 6
  (`building-notes` §17.13: "canteiro com 985 das 987") é **outra** conta — peças de pé contra índices do plano — e
  **não** é o 985 de linhas do arquivo.

### 17.15 O Run 9 (11/11/2026) — o jogador andando no canteiro, as 11 puladas e o defeito da barreira

- **O que muda em relação ao Run 8:** a **guarda de posição do jogador foi removida**. Ele foi posicionado **uma vez**
  antes da ordem e depois **andou livre** pelo canteiro — pedido dele: *"pra que eu possa andar com ele pelo jogo
  depois sem ser teleportado"*. É o único parâmetro que muda: o resto da config é o do Run 8 (**5 companheiros**,
  `BlueprintFree = true`, `DeckLift = 1`, `PlaceReach = 15`, `SupportFallback = true`, `TempScaffold = false`). O
  objetivo do run é justamente medir o que o corpo **móvel** faz com o canteiro.
- **Números (medidos):** **977 colocadas + 11 puladas = 988** — a fala final **tocou** desta vez:
  `977 peças, 11 puladas` (no Run 8 ela não tocou e o `987/0` veio dos contadores do grupo, §17.14); **watchdog `0`**;
  **`UNSUPPORTED` = 0** (o veredito do próprio jogo, `MHR_Build.Verify`); **1023 peças** no canteiro; **5 bancadas**
  inseridas; **`blocked_by_player` = 235**; **1 morte** (abaixo).
  - **Os 988 índices:** **983** peças do arquivo + **5 bancadas** (o Run 8 tinha **4**, do `PlanStations`). A conta do
    arquivo é a do §17.14 (985 linhas − 2 `stone_floor_2x2` com `recipe not unlocked` = 983). **Não medido neste
    run:** quantas das 5 bancadas são **planejadas** e quantas vieram do **`InsertBenchStep`** (a auto-cura,
    `MHR_Build.cs:2403`).
  - **As 1023 do canteiro:** pela mesma aritmética do Run 8 (**canteiro = colocadas + peças fora do plano**, lá
    1026 = 987 + 39), sobram **46 peças fora do plano** — os **postes de emergência**. **Ressalva:** este run **não
    trouxe a contagem de `apoio de emergência`**; os **46** são **derivados da conta**, não lidos do log.
  - **`blocked_by_player` = 235** com o jogador andando: ~2× o Run 8 (**120**, jogador fora) e **muito** abaixo do
    Run 6 (**≈770**, jogador **dentro** do canteiro). Os três não são comparáveis ao pé da letra — o número mede
    **trânsito de corpo ao redor das peças**, não "jogador parado", e a posição dele muda a cada peça no Run 9.
- **A morte (registrada, e ignorada por decisão do jogador):** o **companheiro 4** caiu **8,1 m do telhado** na peça
  **962/988**, deixou **lápide com 26 itens**, e o mod logou
  `it died during the build - the order resumes after the respawn (piece 962/988)` (`MHR_Build.cs:1691`). Depois do
  respawn, **o próprio companheiro 4 encerrou a ordem** com `hc_ai_blueprint_done` (`MHR_Build.cs:913`/`:916`) — lido
  do log: o `OnDeath` gravou o pending e o **corpo novo** retomou o plano (o `Reattach` do §15.2t), terminando na mão de
  quem caiu. **Não é investigação aberta:** o jogador decidiu **não** perseguir este caso. Queda de telhado durante uma
  obra de 988 peças é risco conhecido (§17.10, `falls`) e o `AdjustPose` cobre a **peça**, não o **corpo** — nada aqui
  sugere regressão, e não foi tratado como uma.
- **As 11 puladas — duas causas, medidas:**
  1. **1 `wood_gate`** em **(-163,3, 40,8, -422,9)**: `blocked_by_player` **repetido** até esgotar — era exatamente
     onde o **jogador estava parado** (o portão da entrada). Sem corpo ali, a peça sobe.
  2. **10 peças numa coluna em x≈-152,7**: **3 `wood_floor`** (**39,8 / 40,8 / 40,8**), **3 `wood_roof_45`**
     (**42,8**) e **4 `piece_chest_wood`** (**40,9**) — todas em `no_support` ("sem apoio ainda … não gasta
     tentativa, volta com espera", o portão do §17.2) até o **watchdog** encerrar.
- **A cascata da coluna (o achado que vale a nota):** a **vizinhança está de pé** — **5 `wood_beam_1` em 39,3** (postes
  de emergência), pisos em **39,8** e vigas em **39,8 / 40,8 / 41,8**. O que faltou foi **o tile exato de 39,8**: ele
  foi recusado **por corpo** e mandado para a **fila de retry**. O piso de **40,8** logo acima dele vê, na **lista do
  plano**, uma peça **abaixo de si** (`DirectSupportIndex`, `MHR_Build.cs:3177`, chamado em `:3150` e `:3275`) e por
  isso **não pede poste**: a guarda do apoio de emergência é explícita —
  `// the plan has something under it: the pull is the path (never a post over the plan's own support)`
  (`:3275-3278`). Só que **a peça do plano não estava no mundo**: `DirectSupportIndex` responde pela **lista**, não
  pelo estado do canteiro. Resultado: a coluna inteira espera um apoio que está **na fila de retry** e **congela até
  o watchdog**.
  - **Isto não é o `no_support` do §17.12.** Lá faltava apoio **de verdade** (0,6 m de vão sem peça que alcançasse) e
    o **poste por vão** resolveu. Aqui o apoio **existe no plano** e **não existe no mundo**: o caminho declarado
    ("o pull é o caminho") é o certo — ele só chega **tarde**, porque o retry da peça recusada por corpo acontece
    **no fim**.
  - Ou seja: as **11 puladas** e o defeito abaixo são **o mesmo problema de ordem**, vistos por dois ângulos. A
    coluna é a versão **silenciosa** (o portão de dependência espera 8/12/20/25 s, não gasta tentativa e nunca conta
    como pulada — mas o watchdog encerra igual); a escada da entrada é a versão **visível** (o jogador vê a casa
    subir sem a escada).
- **O defeito que o jogador viu:** as **escadas da entrada** são as peças **8, 9 e 10 de 988** do plano — tentaram
  **primeiro** (a obra anda **por andar** e, dentro dele, piso → escada → parede → telhado → porta: `StoreyOf`,
  `AI/BuildBlueprintParser.cs:327`; §15.2i/§15.2j/§15.2k), foram recusadas **por corpo** e **só subiram na fase de
  retry, no fim**, depois da casa inteira. É exatamente a queixa,
  com print: *"essa parte da escadinha da entrada e esses dois pisos já deveriam estar ali antes de continuarem
  construindo pra cima"* e *"recusado por causa de alguém em cima/na posição, eles podem pular, mas retornar e tentar
  novamente antes de continuar erguendo pra cima"*.
- **O desenho da barreira (a regra do jogador):** recusa **por corpo** pode pular, mas a peça **volta e é re-tentada
  antes** de o plano **subir de andar**. **O estado mudou depois desta seção:** a barreira foi implementada no lote
  `fbda4ee` e **MEDIDA no Run 10** — desenho, constantes, evidência de log e tabela Run 9 × Run 10 em **§17.16**. O que
  esta seção mediu foi o **defeito**; §17.16 mede a **correção**.
- **O que este run NÃO mediu:** a contagem de `apoio de emergência` (só a diferença de **46** no canteiro, derivada);
  quantas bancadas foram **planejadas** × **auto-curadas**; e o comportamento da barreira (que ainda não existia nesta
  árvore naquele momento). **Config:** o run não mudou nenhuma chave em relação ao Run 8 e a chave da barreira
  (`[Building] BodyRetry`) **só entra no lote `fbda4ee`**, medido no Run 10 (§17.16) — as **361 opções / 25 seções** eram
  a medida de 04/11/2026, não uma reconferência daquela rodada.

### 17.16 O Run 10 (11/11/2026) — a barreira de andar **MEDIDA** (lote `fbda4ee`)

- **A pergunta do run:** a **barreira de andar** resolve o defeito do Run 9? Lá as **escadas da entrada** (peças **8, 9 e
  10 de 988**) foram recusadas **por corpo** e só subiram no **retry do fim**, depois da casa inteira (§17.15). A regra do
  jogador: *"eles podem pular, mas retornar e tentar novamente antes de continuar erguendo pra cima"*.
- **Montagem:** **5 companheiros**, `BlueprintFree = true`, `[Building] DeckLift = 1`, `PlaceReach = 15`,
  `SupportFallback = true` e **`[Building] BodyRetry = true`** (o **padrão** da chave). A variável é a mesma do Run 9: o
  jogador foi posicionado **uma vez** em **(-166,92, 39,92, -401,59)** e depois **andou livre** — chegou a
  **(-180, 38,2, -423)** e **entrou no canteiro** em vários momentos. Mesma casa (`CommHomeBasicBP`).

**O desenho, como está no código:**

1. **A recusa por corpo tem classe própria** — `blocked_by_player` é `BuildFailure.Kind.Blocked` (`AI/BuildFailure.cs:19-25`,
   `:74-78`, `Classify` em `:107`): **não gasta** nenhuma das 3 tentativas e **nunca** conta como pulada. Quem responde sim
   é `BuildFailure.IsTransient` (só `blocked_by_player`, ignorando a caixa).
2. **Três curtas na MESMA pose** — `MaxShortRetries` = **3** (`BuildFailure.cs:36`) com `ShortRetryDelay` = **2,5 s**
   (`:40`), contadas **para o canteiro** e não por worker (`MHR_BuildGroup.TryShortBody`, `Jobs/MHR_BuildGroup.cs:549`;
   fase `Phase.BodyRetry` em `MHR_Build.cs:204`/`:4664`). Quem dispara é o caminho da recusa: `TryBodyRetry`
   (`MHR_Build.cs:4803`, `:4896`), que **antes** carimba a barreira do canteiro (`:4916` → `MHR_BuildGroup.NoteBody`,
   `MHR_BuildGroup.cs:525`). Nada foi **pago** (a recusa acontece antes do `BuildStock.Take`) e o claim sobrevive às curtas
   (o `Refresh` roda por frame, então os 20 s de expiração nunca veem a espera). Esgotadas as 3, a peça vai para a **fila do
   canteiro** como `Blocked`: espera `BodyRetryDelay` = **2 s** (`BuildFailure.cs:56`, usado em `MHR_BuildGroup.cs:719-724`)
   em vez da espera crescente de dependência. **São dois números diferentes de propósito** (2,5 s da curta, 2 s da fila).
3. **A barreira do canteiro** — a **primeira** recusa daquele índice carimba o relógio, e a escrita é **idempotente**
   (`MHR_BuildGroup.cs:532-536`): jogador parado não empurra o relógio. Enquanto ela viver (`BodyBarrierSeconds` = **20 s**,
   `BuildFailure.cs:50`), **nada acima de 1,5 m** do nível da peça é pego (`BodySameStorey` = 1,5, `:45`; `MayTake`,
   `MHR_BuildGroup.cs:587`/`:597`) — vale no `Advance` (`MHR_Build.cs:922`), na fila do canteiro (`NextRetry`,
   `MHR_BuildGroup.cs:773-778`) e no serviço da barreira (`NextRetryBelow`, `:857`). A barreira de **subida**
   (`StoreyBarrier`, `MHR_Build.cs:1081`) **é desenhada sempre que há peça pendente ABAIXO** da próxima
   (`PendingBelowIndex`, `:1103`), qualquer que seja a altura da caminhada; ela **serve** a peça mais baixa que já venceu
   (`ServeBarrier`, `:1125`), **espera** enquanto ela vem e **desiste** em `BodyRetryMaxWait` = **25 s** (`:66`) — é o maior
   `DependencyDelay`, justamente para nunca cortar uma peça que espera apoio do próprio plano.
   **Dois furos reais corrigidos no lote:** (i) a versão do Run 10 **só desenhava** quando a próxima peça estava **>1 m**
   acima (`BodyRetryStep` = 1 m, hoje constante **sem uso**): a parede do 1º andar tem o **mesmo y** do feixe (**41,8**)
   com os tiles pendentes em **39,8**, então **nunca desenhava**; (ii) a paciência contava **do desenho**, mas um
   `dependency` espera até **25 s** (`DependencyDelay`) e ela expirava **antes de a peça ficar devida** — agora a paciência
   só corre **depois do "due"** (`PendingBelowAt`, `MHR_Build.cs:1253`, usado em `:1141-1145`), com **25 s de tentativas
   reais** e o watchdog de **60 s** como teto.
   **Refinada no mesmo lote:** a barreira roda **antes do `TryClaim`** (`MHR_Build.cs:945`/`:960`) — peça adiada não fica
   reivindicada 20 s por quem a adiou — e, servida a peça da fila, o `Advance` **volta ao índice da subida**
   (`State.ResumeIndex`, `MHR_Build.cs:898-902`, carimbado em `:1163`), sem re-caminhar o que já subiu.
4. **Escopo e interruptores** — só **blueprint** (`BarrierOn`, `MHR_Build.cs:1149`); template e peça única ficam de fora.
   A barreira é **descartada** quando a fila é limpa (`ClearBodies`, `MHR_BuildGroup.cs:622`) e o índice que sai da fila
   leva consigo a barreira e a contagem de curtas (`ForgetBody`, `:635`).

**O que o log do run mostrou (medido):**

- `um corpo está na peça 354 do canteiro (nível 1,0) - nada acima de 2,5 m é pego por 20 s` — é o `NoteBody`
  (`MHR_BuildGroup.cs:538-540`): nível **1,0** + 1,5 m = **2,5 m**, e **20 s**.
- `um corpo está na peça 507/990 (blocked_by_player) - tentando de novo em 2,5 s (curta 1/3)` … `curta 3/3` — o caminho do
  grupo em `MHR_Build.cs:4923-4925`.
- A linha `placed (Direct) em (x, y, z), verifying next frame` (`MHR_Build.cs:4878-4879`) publica a **pose** — é o que
  permite ler no log **qual peça subiu onde** e quais ficaram.
- **16 peças** chegaram a **`curta 3/3`** e ainda assim voltaram **antes** de o plano subir: o plano **andou** depois das
  curtas (peças **367-379** colocadas) e as paredes deixaram de subir com o térreo aberto — que era exatamente o defeito do
  Run 9.

**Resultado:** **989 colocadas + 1 pulada = 990** (a fala final **tocou**: `989 peças, 1 puladas`), **watchdog `0`**,
**0 `UNSUPPORTED`**, **1022 peças** no canteiro, **33 postes** de emergência com **0 recusas** (`sem lugar para o apoio` =
**0**), **7 bancadas** inseridas (**6 de pé** + a **7ª** recusada por `clipping`) e **23/23 prefabs do blueprint no lugar**
(`accept_run.py`: `TODAS AS PECAS DO BLUEPRINT
NO LUGAR`). **1 morte**, ignorada por decisão do jogador (mesma regra do Run 9, §17.15: não é investigação aberta).
**O canteiro fecha na aritmética: 989 colocadas + 33 postes = 1022.**

| | Run 9 | **Run 10** |
|---|---|---|
| `blocked_by_player` — recusas **terminais** | **235** | **141** |
| `blocked_by_player` — **curtas** (2,5 s) | — | **224** |
| peças puladas | **11** | **1** (a 7ª bancada abaixo) |
| curtas que chegaram a `3/3` | — | **16** peças |
| watchdog / `UNSUPPORTED` | 0 / 0 | **0 / 0** |
| canteiro | **1023** | **1022** |

- **Como ler a tabela (correção honesta):** o **`235 → 5`** que esta seção trazia **não existe** — os números medidos são
  **235 terminais** no Run 9 (lá **não havia curta**: a `BodyRetry` não existia naquela árvore) contra **141 terminais +
  224 curtas** no Run 10, e as **16 peças** em `curta 3/3` são as que **esgotaram** as curtas e foram para a fila. Como o
  critério **e** a árvore mudaram, a linha do `blocked_by_player` **não é comparável**; o número **comparável** é o das
  **puladas** (**11 → 1**). Ressalva extra: os números do Run 10 saíram da sonda **disco** (antes do conserto por
  cápsula), então parte das recusas por corpo pode ser **falso positivo** — o **Run 11** mede com a cápsula real
  (**medido**: puladas **1 → 0**, §17.17).
- **A ÚNICA pulada não é peça da casa:** `piece_workbench` em **(-156,5, 38,4, -410,6)**, recusada por **`clipping`**. É a
  **7ª bancada** — das que o `PlanStations`/`InsertBenchStep` inserem para cobrir a exigência de estação por peça
  (§17.3) — e as **outras 6** subiram. Por isso o aceite da casa fecha: **23/23 prefabs do blueprint no
  lugar** (`accept_run.py`). A bancada é o **meio**, não a obra. O `clipping` de bancada já tinha aparecido no Run 2
  (`piece_workbench` em (-178,3, 38,6, -431,7), §17.6): a âncora externa colide. **Continua aberto**, sem correção nesta
  rodada.

**O "bloqueador COMPANHEIRO" do Run 10 era FALSO POSITIVO da sonda — o conserto foi a cápsula real, não um número:**

- **O que o Run 10 leu:** o `wood_beam` em **(-168,1, 41,8, -421,1)** com o **Companheiro 6** em **(-167,1, 39,9,
  -421,5)** (**1,08 m** em XZ, **1,9 m** abaixo) parecia corpo contra peça — a peça esgotava as 3 curtas, a barreira
  vencia os **20 s** e ela caía no retry do fim. **A análise seguinte mostrou que não era corpo: era a sonda.** O
  `BuildProbe` calculava `flat = max(size.x, size.z)/2` e tratava a peça como um **disco de 1 m em qualquer direção**,
  **ignorando pivô e rotação**: um feixe de **2 m** virava um disco de **1 m** e "batia" num companheiro a **1,08 m**.
  O jogo testa o ghost contra a **cápsula do `Character`** (`Player.cs:4195-4225`, `ComputePenetration`) — a peça é uma
  **caixa**, não um disco.
- **O conserto (não é ajuste de número):** `BuildProbe.CapsuleOf` (`AI/BuildProbe.cs:813`) devolve a **cápsula real** do
  corpo (`Collider.bounds.min.y`/`max.y` + raio; o fallback 1,8 m / 0,3 m é logado **uma vez**) e `BlockedByCharacter`
  (`:733-793`) monta a **caixa girada** da peça na pose — `center = point + rot*(box.center −
  piece.transform.position)`, `halfY` pelo `BuildBody.HalfY` (AABB do OBB) — e pergunta o `BuildBody.Blocks`
  (`AI/BuildBody.cs:42`), que trata **toque como livre** (`pieceTop <= capsuleBottom || pieceBottom >= capsuleTop`). O
  log diz os números reais na **1ª** recusa de cada peça: `corpo na peça X - cápsula 39,90-41,70 m, r 0,30`
  (`MHR_Build.cs:5030-5032`, `CapsuleText` em `:5074`), **uma vez por peça** (`State.BodyLogged`).
- **O que entrou mesmo assim — `[Building] StepAside` (bool, padrão `true`, `Core/ModConfig.cs:174`/`:962`), a
  segunda linha de defesa:** quem **coloca** a peça
  lê o corpo que a bloqueia (`BuildProbe.BlockerAt`, `MHR_Build.cs:5049`); se for um **companheiro** (o **jogador nunca é
  movido**, `:5054-5057`), o canteiro registra o pedido (`MHR_BuildGroup.RequestStepAside`, `:669`; dedup de **4 s**,
  `StepAsideRepeat`, `MHR_BuildGroup.cs:49`) e o companheiro **em espera** o consome **uma vez** (`TakeStepAside`, `:690`,
  válido **5 s** — `StepAsideSeconds`, `:45`). Ele então escolhe o mais perto de **8** pontos a **2,2 m** da peça
  (`AsideDistance`, `MHR_Build.cs:5092`; `AsideSpot`, `:5155`), no **próprio nível dos pés** (raycast aceita chão a 0,8 m)
  e alcançável (`MHR_Mover.SafeStep`), e anda até lá **com o mover** (`MHR_Mover.Go`, `:5118`/`:5145`; passo de até
  **2,5 s**, `AsideSeconds`, `:5096`) — **nunca teleporte, nunca empurrão**: passo bloqueado é só um passo que não
  aconteceu. A resposta só existe no **wait** (`WaitRetry`, `:2402`/`:2408`): worker que não está esperando **não** consome
  o pedido, e o pedido expira se a peça já foi resolvida. Desligada, a peça espera o companheiro sair sozinho (o
  comportamento do Run 10).
- **Medida no Run 11 (11/11/2026) — §17.17:** a `StepAside` foi implementada em **11/11/2026** e **não existia** na
  árvore do Run 10 (o lote é posterior a `fbda4ee`); ela rodou **ligada** (`true`, o padrão) no **Run 11**, o run com
  **0 puladas**. As duas linhas de log do caminho — `"<nome>" está na peça N - pedindo para sair da frente` (`:5067`) e
  `saindo da frente (peça em x, z) para (x, z)` (`:5143`) — **não foram contadas** nesta rodada: o que está medido é o
  resultado **com a chave ligada**, não quantas vezes o companheiro foi pedido a sair (§17.17).
- **Em aberto:** a sonda **não testa criaturas** — um **javali em cima da peça** ainda passa, porque a janela só percorre
  `Player.GetAllPlayers()` e `CompanionSetup.AllCompanions` (`AI/BuildProbe.cs:756-791`).

- **Contas desta nota (honestidade, corrigida):** o plano tem **990** índices = **983** peças do arquivo (§17.14: 985
  linhas − 2 `stone_floor_2x2` com `recipe not unlocked`) + **7 bancadas** inseridas pelo `PlanStations`/`InsertBenchStep`
  — o run **nasceu com 986** (= 983 + **3**) e **4** bancadas entraram **durante a obra**; **6** ficaram de pé e a **7ª**
  é a peça pulada por `clipping`. **Não há "sobra de 1"**: o índice que faltava é exatamente essa bancada. O canteiro
  **fecha**: **989 + 33 = 1022** (não reconferido com `build.pieces`).
- **Config e testes desta rodada:** **363 opções / 25 seções** (`gen_config_doc.py --check` → `363 settings, 25 sections`)
  — as chaves novas são a `[Building] BodyRetry` (bool, **padrão true**, `Core/ModConfig.cs:171`/`:958`; rótulo
  `hc_cfg_building_bodyretry` = "Retry blocked pieces", `Core/ModLocalization.cs:846`) e a `[Building] StepAside` (bool,
  **padrão true**, `:174`/`:962`; rótulo `hc_cfg_building_stepaside` = "Move aside", `:849`) — as duas já nos **4** JSONs
  (`Translations/*.json:2888` e `:2892`, nessa ordem). **Harness 642** (`dotnet run --project tools/ai-intents-check`:
  `471 + 28 (p) + 26 (q) + 66 (r) + 18 (s) + 27 (t) + 6 (u)`): a **(s)** cresceu com o `BuildBody.Blocks`/`HalfY` (viga a
  45°) e o par do **falso positivo** do Run 10; a seção **(t)** é a
  barreira — **27** casos (`IsTransient`, `ShortRetry`, as constantes e o `BuildBlueprintParser.Line.Level`, a mesma chave
  do `Ordered`) — e a **(u)** (**6**) é a `FallbackPostAllowed`, que destrava o poste de emergência quando o apoio do plano
  está reivindicado e não vem (`SupportGrace` = **2**: ~**20 s** = 8 + 12 s de espera por dependência; **imediato** se o
  apoio foi desistido) — o congelamento da coluna do Run 9, §17.15; **em teste, sem run**. MCP **107** (inalterado).

### 17.17 O Run 11 (11/11/2026) — o primeiro run com **`0 puladas`** (sonda por cápsula + os dois furos da barreira)

- **A pergunta do run:** o lote da **sonda de corpo** (cápsula real × caixa girada) mais os **dois furos da barreira de
  andar** (`PendingBelowIndex` e a paciência só **depois do "due"**, §17.16) mantêm a casa de pé **sem pulada**? E a
  `[Building] StepAside` — implementada em 11/11/2026 e **sem run** até aqui (§17.16) — roda em jogo?
- **Montagem:** **5 companheiros**, `BlueprintFree = true`, `[Building] DeckLift = 1`, `PlaceReach = 15`,
  **`[Building] BodyRetry = true`** e **`[Building] StepAside = true`** (as duas no **padrão**). O `SupportFallback` e o
  `TempScaffold` **não foram reconferidos** nesta rodada (a leitura é a dos runs anteriores). A chave que **distingue**
  este run do Run 10 é a `StepAside`: o Run 10 rodou no lote `fbda4ee`, **antes** do lote que a implementou (§17.16), e
  aqui ela está **ligada**. Mesma casa (`CommHomeBasicBP`). **Jogador:** posicionado **uma vez** em
  **(-166,92, 39,92, -401,59)** e depois **andando livre** — **entrou no canteiro várias vezes** e **subiu até y 45**
  (em cima da obra). É a variável mais dura dos runs com o corpo na obra: o jogador é obstáculo e não é movido pelo mod.

**Resultado (o melhor run até agora):**

- **987 colocadas + 0 puladas = 987** — a fala final **tocou** e foi medida: **`987 peças, 0 puladas`**.
- **watchdog `0`**, **0 `UNSUPPORTED`** (o veredito do próprio jogo, `MHR_Build.Verify`), **0 mortes**, **0 `falls`**.
- **1020 peças** no canteiro = **987 do plano + 33 postes** de emergência. **4 bancadas** inseridas e **todas
  colocadas** — a **7ª bancada** recusada por `clipping` no Run 10 **não apareceu** nesta rodada (§17.16).
- **23/23 prefabs** do blueprint no lugar (`accept_run.py`: `TODAS AS PECAS DO BLUEPRINT NO LUGAR`).
- **Corpo (sonda nova):** **98** recusas **terminais** (`blocked_by_player`) + **145** **curtas** (2,5 s), e a
  **barreira disparou 56×** — a linha `nada acima de … m é pego por … s` (`MHR_BuildGroup.NoteBody`,
  `MHR_BuildGroup.cs:538-540`) apareceu **56** vezes no log.

**O que este run validou do lote:**

1. **A sonda é a cápsula real × caixa girada** (`BuildProbe.CapsuleOf`, `AI/BuildProbe.cs:813`, chamada de
   `BlockedByCharacter`, `:733-793`, com o `BuildBody.Blocks`/`HalfY`) e **não** o disco de `max(size.x, size.z)/2`.
   O efeito medido é indireto mas consistente: o corpo **ainda** bloqueia (**98** terminais, prova de que a sonda **vê**
   o corpo de verdade) e as recusas **caem** frente ao Run 10 (**141 + 224 → 98 + 145**). Não há, nesta rodada, contagem
   de falsos positivos do disco — a comparação é a da tabela abaixo, com a ressalva de que **critério e sonda mudaram
   juntos**.
2. **Os dois furos da barreira estão fechados — medido pelo uso:** a barreira **disparou 56×**. No Run 10 ela só era
   **desenhada** quando a próxima peça estava **>1 m** acima (`BodyRetryStep`) e a paciência contava **do desenho**,
   vencendo antes de a peça ficar **devida**; agora **qualquer peça pendente abaixo** desenha (`PendingBelowIndex`,
   `MHR_Build.cs:1103`/`:1213`) e a paciência só corre **depois do "due"** (`PendingBelowAt`, `:1253`, usado em
   `:1141-1145`). O número **56** é a evidência de que o caminho é exercitado **de verdade** num run em que o jogador
   anda no canteiro.
3. **`[Building] StepAside = true` roda dentro do run mais duro** e o resultado **não tem pulada**. Isso tira a chave
   do estado "em teste, sem run". **O que NÃO foi medido:** a **contagem** das linhas do step-aside
   (`"<nome>" está na peça N - pedindo para sair da frente`, `MHR_Build.cs:5067`, e
   `saindo da frente (peça em x, z) para (x, z)`, `:5143`) — o log desta rodada **não foi contado** por esse filtro, então
   não se afirma **quantas** vezes o companheiro foi pedido a sair nem **quantas** vezes ele andou. O que se afirma é o
   que foi medido: **com a chave ligada, 0 puladas**.
4. **`0 falls` de novo:** o caminho `log ajustado … - falls` (`AdjustFallenPose`) **não foi exercitado** (o Run 8 também
   teve 0), então ele continua **sem run** — não há terceira chance para a peça que cai duas vezes.
5. **`0 mortes`:** Runs 9 e 10 tiveram **1 morte cada** (a do Run 9 foi queda de 8,1 m do telhado, ignorada por decisão
   do jogador, §17.15); aqui **nenhuma**. É um **dado**, não uma prova de correção — não houve lote dedicado à queda e o
   jogador subiu a **y 45** sem que nada caísse.

**Run 10 × Run 11 (o que é e o que não é comparável):**

| | Run 10 (`fbda4ee`) | **Run 11** |
|---|---|---|
| índices do plano | **990** (983 + **7** bancadas) | **987** (983 + **4** bancadas) |
| colocadas | 989 | **987** |
| **puladas** | **1** (a 7ª bancada, `clipping`) | **0** |
| bancadas inseridas | 7 (**6 de pé**) | **4, todas colocadas** |
| `blocked_by_player` — terminais | 141 | **98** |
| `blocked_by_player` — curtas (2,5 s) | 224 | **145** |
| vezes que a barreira disparou | não contado | **56** |
| watchdog / `UNSUPPORTED` | 0 / 0 | **0 / 0** |
| mortes / `falls` | 1 / não contado | **0 / 0** |
| canteiro | **1022** (989 + 33) | **1020** (**987 + 33**) |
| sonda de corpo | **disco** de `max(size.x, size.z)/2` | **cápsula real × caixa girada** |

- **Como ler a tabela (honestidade):** o **total absoluto cai** (990 → 987) porque o **plano mudou**: o Run 11 tem
  **987 índices** (983 do arquivo + **4** bancadas) contra **990** (983 + **7**). O planejador inseriu **3 bancadas a
  menos** e **todas** as 4 subiram — o número que importa é **puladas 1 → 0**, não o total. O `blocked_by_player`
  **não é comparável direto** (mudaram **critério e sonda**: a árvore do Run 10 media o disco, a do Run 11 mede a cápsula
  e a barreira madura); o comparável é a linha das **puladas**.
- **O canteiro fecha na aritmética: 987 + 33 = 1020.**

**O que continua fora do plano — as 2 `stone_floor_2x2` (limite do arquivo/save, com o porquê medido):**

- O arquivo tem **985 linhas** e **2** são descartadas na **leitura** — não é falha de colocação: o
  `MHR_Build.cs:1502-1509` descarta a linha quando `Player.IsRecipeKnown(resolved.m_name)` é falso e loga
  `<prefab> (recipe not unlocked)`. **985 − 2 = 983**, que é a conta que fecha os índices do plano.
- **Por que a receita não está destravada:** `stone_floor_2x2` é peça do **talhador de pedras** (`build.inspect` medido:
  `$piece_stonecutter`, alcance **20 m**), e a receita do talhador **exige pedra** — o inventário do jogador estava
  **vazio** nesta rodada (medido). Ou seja: falta **pedra no jogador** para a receita existir e **um talhador a ≤20 m**
  para a peça ser aceita.
- **O catálogo do mod não resolve isso:** `AI/BuildCatalog.cs:40-72` é a lista fechada da oficina mínima — **5** prefabs
  (`piece_workbench`, `piece_chest_wood`, `fire_pit`, `bed`, `piece_bed02`). **Não há talhador de pedras** no catálogo,
  então o companheiro **não constrói** a estação que destravaria a peça. (O texto antigo dizia "só bancada/baú/cama";
  a lista real é de **5** e nenhuma delas é o talhador — a conclusão não muda, a contagem sim.)
- **Consequência honesta:** as 2 peças são o **teto do blueprint** nesta instalação, não um bug da obra. Para medir um
  run com elas seria preciso **pedra no inventário do jogador + talhador de pedras de pé e a ≤20 m** do canteiro — isso
  **não foi testado** em nenhum run até aqui.
- **Atualização (Run 13, §17.18):** o **teto caiu** — com a receita aprendida na sessão e a
  `[Building] BlueprintStations` (padrão **true**) as **2 subiram sem talhador no mundo**. E a regra de descarte mudou
  de lugar: hoje ela é `BuildBlueprintSkip.Reason` (`AI/BuildBlueprintSkip.cs:25-36`), chamada em
  `MHR_Build.cs:1504-1506` — o `:1502-1509` citado acima é o mesmo trecho, com o texto extraído para a classe pura que
  o `blueprints.inspect` também usa.
- **Ficha medida da estação e da peça (04/11/2026):** `docs/re/stone-and-stonecutter.md` — receita do talhador
  (Madeira 10 + Ferro 2 + Pedra 4), que ele **exige bancada** a ≤20 m, `m_rangeBuild` **20**, `m_discoverRange` **4**,
  `noClipping`/`notOnTiltingSurface`/`noInWater` ligados, `m_supports = false` (nada em cima dele), a prova de que a
  estação instanciada **fora** do `Player.PlacePiece` entra em `m_allStations` (`build.sites → hasStation: true`), a
  cadeia de unlock (`UpdateStations` 1 s → 4 m → `AddKnownStation` → `UpdateKnownRecipesList`) e o que o nosso
  `PlaceDirect` **não** faz (o `AddKnownStation` de `Player.cs:3105-3109`).

**Contas e limites desta seção:** os números acima são os **medidos** no run (fala final, contadores do log,
`accept_run.py` e a contagem do canteiro); **não** foram medidos nesta rodada — a contagem das linhas do `StepAside`, a
contagem de falsos positivos do disco (não há como reler a sonda antiga), quais dos 33 postes vieram da
`FallbackPostAllowed` (§17.16 continua **sem run** para ela) e o comportamento do `log ajustado … - falls` (0 `falls`).
A árvore é a do §17.16 — **363 opções / 25 seções** (`gen_config_doc.py`) e **harness 642** —, medidas naquele lote:
**não** houve reconferência nesta rodada e **nenhuma chave nova** entrou com o run.

### 17.18 O Run 13 (11/11/2026) — a `[Building] BlueprintStations` **MEDIDA**: a casa sobe sem bancada e as 2 de pedra entram

> A numeração é a do **contador de runs do jogador**; a seção anterior documentada é o **Run 11** (§17.17).

- **A pergunta do run:** com a `[Building] BlueprintStations` (lote `f93f290`, o **padrão true** do código) ligada —
  **nenhuma bancada planejada** e **nenhuma peça recusada por estação** — a `CommHomeBasicBP` sobe **igual ou melhor**
  do que com o `PlanStations` trabalhando? E as **2 `stone_floor_2x2`** que ficaram fora de **todos** os runs até o
  §17.17 conseguem entrar no plano **sem talhador no mundo**?
- **Por que as 2 de pedra eram o teste:** são peça do **talhador de pedras** (`$piece_stonecutter`, alcance **20 m**) e
  o que faltava era a **receita** — a leitura descarta a linha quando `Player.IsRecipeKnown` é falso
  (`BuildBlueprintSkip.Reason`, `AI/BuildBlueprintSkip.cs:25-36`, motivo `recipe not unlocked`). Neste run a receita
  foi **aprendida na sessão** (um talhador temporário foi construído e **apagado antes da ordem**), então a leitura
  não descarta mais nenhuma das 985 linhas.
- **Montagem:** **5 companheiros**, `BlueprintFree = true`, `[Building] PlaceReach = 15`,
  **`[Building] LevelGround = false`** (ver o item (c) abaixo — é contorno, não desenho),
  **`[Building] BlueprintStations = true`** (`Core/ModConfig.cs:191`/`:980`), `TempScaffold = false` e o **jogador
  andando**: entrou no canteiro **várias vezes**, como nos Runs 9–11. Mesma casa (`CommHomeBasicBP`, **985 linhas**).

**Resultado:**

- **Plano 985 = 983 do arquivo + as 2 `stone_floor_2x2`.** A conta que o §17.17 fechava em **983** fecha em **985**
  porque **nada foi descartado na leitura**.
- **984 colocadas + 1 pulada = 985** — a fala final fecha no **plano**, não no arquivo.
- **0 bancadas inseridas** (os Runs 8–11 tinham **4–7**): com a chave ligada o `PlanStations` **não é chamado**
  (`MHR_Build.cs:1609-1632`) e a linha que substitui o resumo diz
  `[Build] blueprint <arquivo>: estações não exigidas por config (N peça(s) que precisariam)`
  (`MHR_Build.cs:1629-1630`).
- **As 2 peças de pedra de pé SEM nenhum talhador no mundo.** A linha medida:
  `done: stone_floor_2x2 at (-159,7, 39,3, -424,6) yaw 228,9 supported` — depois de **2** recusas por corpo. Como o
  plano tem **985** índices e a **única** pulada é a cumeeira (abaixo), **as duas** subiram.
- **30 postes** de emergência e **1014 peças** no canteiro = **984 + 30** (a aritmética fecha; sem bancada inserida não
  há parcela de bancada).
- **watchdog `0`**, **0 `UNSUPPORTED`** (o veredito do próprio jogo em `MHR_Build.Verify`), **0 mortes**.

**A peça de pedra sem estação — o que exatamente mudou de comportamento:** o `BuildSite.Check` recebe
`requireStation = false` para a ordem de **blueprint** com a chave ligada (`MHR_Build.cs:5667-5672`), então
`needs_station` **não é adicionado** às razões (`AI/BuildProbe.cs:566-569`) — e o `hasStation` do resultado **continua
dizendo a verdade do mundo** (`:636`), que é o campo que serve de prova: a peça passou com `hasStation = false`. O que
**não** mudou: `clipping`, `ward`, água, terreno, corpo e receita continuam barrando; e **template e peça única**
seguem exigindo a estação (`AI/BuildStationCover.cs:35-42` — `isBlueprint == false` → `true`, sempre).

**A única fora do plano — a cumeeira é navegação, não apoio nem alcance:**

- `wood_roof_45` em **(-152,6, 48,8, -423,6)**, a **cumeeira**, cerca de **10 m** acima do chão do canteiro.
- O caminho medido: `no progress towards the wood_roof_45 for 12 s (22,8 m away) - retrying it later`
  (`MHR_Build.cs:5580`) → nova tentativa a **23,5 m** → `desistindo de wood_roof_45 (no progress) em (-152,6, 48,8,
  -423,6) - 1 pulada(s)` (`MHR_Build.cs:779-780`). As duas leituras são o **mesmo** defeito: em **12 s** o companheiro
  **não chegou mais perto** — ele **se afastou 0,7 m**. É a trava de progresso do `Goto` (§15.2m), **não** o portão de
  apoio: a recusa é `no progress`, não `no_support`.
- **Não é alcance:** a peça estava a **22,8 m** e o `PlaceReach` do teste era **15** (o padrão é **10**) — o limite de
  colocação nem entrou na conta. Faltou **rota**.
- **A escadinha provisória não é a resposta:** o `[Building] TempScaffold` existe exatamente para "12 s sem chegar mais
  perto **+** peça acima do alcance" (§15.2q), mas o jogador **vetou** — a chave fica **false** e o problema tem de ser
  resolvido por **navegação** (a mesma frente da pendência (1) e dos `no progress` do §17.13/§17.15).

**O ciclo de teste — o que apareceu e foi consertado, na ordem (11/11/2026):**

1. **(a) A âncora da ordem.** `companions.build` **sem `x`/`z`** ancorava longe: a casa foi para o **barranco** e o run
   **perdeu-se**. Agora, sem coordenada, a âncora é o **ponto sob a mira** (`BuildFindCommand.Hint`,
   `AI/BuildFindCommand.cs:127-149`: raio de 50 m com a máscara de colocação, ignorando o jogador) e, sem nada sob a
   mira, a **posição do jogador + 4 m na frente** (`DevBridge/Commands.cs:1313-1321`) — a mesma origem que a ordem de
   chat usa (o `hint` da mira).
2. **(b) Um companheiro pesado derrubava a obra.** `hc_ai_build_too_heavy` (`Core/ModLocalization.cs:626`): cada
   companheiro carregava **25–32 stacks**, e o `Drive` encerra a ordem **daquele worker** quando o peso passa do teto
   (`MHR_Build.cs:3120-3128`, log `overweight: <peso>/<teto>` + `order ended: hc_ai_build_too_heavy (encumbered)`); o
   `Finish` **tira o worker do grupo** (`:7006-7013`), então **com todos acima do peso o canteiro morre**. O contorno do
   ciclo foi uma **ferramenta de teste**: `companions.inventory action=clear` (`commit f36e323`,
   `DevBridge/Commands.cs:1832-1847` — o próprio comentário diz que é ferramenta de teste). **No jogo isso não existe:**
   o caminho é a mochila do jogador e a guarda de peso real (§17.19).
3. **(c) O nivelamento opcional matando a obra.** Com `LevelGround` no **padrão (true)** o nivelamento recusou o
   terreno com `hc_ai_build_too_steep` (`Core/ModLocalization.cs:628`) e **a obra não voltou**: a retomada só conhecia
   `hc_ai_level_done`/`hc_ai_level_already`/`hc_ai_level_failed` (`Jobs/MHR_Level.cs:455`), e a chave `too_steep` sai
   no `:282` **sem estar nessa lista**. Contornado no run com **`LevelGround = false`**; o defeito **continua no
   código** — é a pendência (12). **Corrigido em 11/11/2026** (sem run ainda): a lista virou a tabela pura
   `AI/LevelResume.cs`, a chave entrou com linha própria e a seção **(af)** do harness a prende. O run da receita de
   produto (item 1 do backlog) é onde isso se mede em jogo.
4. **(d) A biblioteca de blueprints sem jogar.** `blueprints.list` e `blueprints.inspect` no bridge
   (`DevBridge/Commands.cs:104-105` → `BlueprintsList`/`BlueprintsInspect`, `:628-746`) e no MCP (`blueprints_list`,
   `blueprints_inspect`): a pasta com tamanho/linhas/data por arquivo e, por arquivo, **o que a ordem faria** — peças,
   linhas quebradas, `skippedReasons` com o motivo (`BuildBlueprintSkip`), estações, limites, andares e materiais —
   **sem começar nada** (o leitor é o `BuildBlueprintReport`, o mesmo do painel da obra,
   `AI/BuildBlueprintReport.cs:14`). O teste do MCP foi a **122 checks** e a lista do MCP a **62 ferramentas**
   (`tools/mcp-devbridge/test_mcp_devbridge.py:223`).

**Run 11 × Run 13 (o que é e o que não é comparável):**

| | Run 11 (§17.17) | **Run 13** |
|---|---|---|
| índices do plano | **987** (983 + **4** bancadas) | **985** (983 + **2** de pedra) |
| colocadas | 987 | **984** |
| **puladas** | **0** | **1** (a cumeeira, `no progress`) |
| bancadas inseridas | 4 (todas colocadas) | **0** |
| `stone_floor_2x2` | **fora** do plano (receita travada) | **2 de pé, sem talhador no mundo** |
| postes de emergência | 33 | **30** |
| canteiro | **1020** (987 + 33) | **1014** (984 + 30) |
| watchdog / `UNSUPPORTED` | 0 / 0 | **0 / 0** |
| mortes / `falls` | 0 / 0 | **0** / não contado |
| jogador | andando no canteiro (até y 45) | **andando no canteiro** |

- **Como ler:** o plano do Run 13 é **menor** (985 × 987) porque o Run 11 tinha **4** bancadas dentro do plano e aqui
  há **0** — o número absoluto não é o placar. O que o run mede é outra coisa: **a casa sobe sem bancada**, as **2 de
  pedra entram** e a **única** peça que falta é de **navegação**. **As puladas (0 → 1) não são placar de regressão:** no
  Run 11 **nenhuma** peça ficou de fora, mas o canteiro **se acumula entre runs** (peça que já está de pé conta como
  colocada), então a cumeeira pode simplesmente ter ficado para trás nesta passada — o que se afirma aqui é o **motivo
  medido** (`no progress` de 22,8 → 23,5 m), não uma comparação de qualidade entre os dois runs.
- **O canteiro fecha na aritmética: 984 + 30 = 1014.**

**Contas e limites desta seção:** os números são os do run (fala final, contadores do log, `build.pieces` e o canteiro);
**não** foram medidos nesta rodada: `falls`, a contagem de recusas por corpo (só as **2** da peça de pedra que estão na
linha), a contagem das linhas do `StepAside` (§17.16/§17.17 seguem sem essa contagem), o `N` da linha
"peça(s) que precisariam" e quantos dos **30** postes vieram da `FallbackPostAllowed` (§17.16 continua **sem run**
para ela). O resto da árvore é o **medido** fora do run: **656 PASS** no harness (`dotnet run --project
tools/ai-intents-check -c Release`, depois das seções **(v)** e **(w)** — o Run 11 rodou na árvore de **642**, §17.17;
a seção **(x)**, do walk spot deste run, levou o harness a **666** e a **(y)**, do Run 14, a **684** — §17.20),
**MCP 122**, **config 365/25** (`python tools/gen_config_doc.py`; o `--check` continua sendo o gate) e **0 ausentes no
PT / 0 lacunas em ES-FR-IT** no `audit_translations.py`.

**Achado de localização deste lote — CORRIGIDO no mesmo dia (11/11/2026):** os rótulos
`hc_cfg_building_blueprintstations` (`Core/ModLocalization.cs:852`) e `hc_cfg_controls_buildpanelkey` (`:855`) estavam
**só no inglês nativo** e mostravam a **chave crua** no F8 em PT/ES/FR/IT. Os dois entraram nos **4** JSONs —
`hc_cfg_controls_buildpanelkey` em `Translations/*.json:1416` ("Tecla do painel de obra" / "Tecla del panel de obra" /
"Touche du panneau de chantier" / "Tasto del pannello cantiere") e `hc_cfg_building_blueprintstations` em `:2872`
("Blueprint: sem estações" / "Blueprint: sin estaciones" / "Blueprint : sans établis" / "Blueprint: senza stazioni") —, na
**mesma posição** dos 4 arquivos (a ordem do PT é o que o auditor cobra). Resultado medido: o modo PT foi de **2
ausentes para 0** e o `--lang` ficou em **0 lacunas** nos três. A dívida do `gen_translations.py --check` **subiu de 47
para 49** — não é regressão: o mapa do gerador indexa por **texto PT** e as duas strings novas (`Tecla do painel de
obra`, `Blueprint: sem estações`) não estão nele; ele só grava quando a lista está **vazia**, então segue sem
sobrescrever nada.

### 17.19 Receita de teste × comportamento — o que é do teste e o que é do jogador

A regra é do jogador (11/11/2026), e ela existe porque os números dos runs são de uma **bancada de teste**, não de uma
partida: *"âncora, mochilas, auto-pickup, etc é só nos testes, depois dos testes ficam normal"* e *"ninguém vai fazer a
mesma construção no mesmo lugar"*. Traduzindo para o projeto: **preparar o canteiro para repetir a mesma casa no mesmo
lugar é da receita**; qualquer coisa que **só funcione assim** é bug de desenho.

**Só de teste (montagem do run — não é o mod funcionando):**

| item do run | como foi montado | por que não é comportamento |
|---|---|---|
| **âncora fixa no script** | `companions.build blueprint=… x=… y=… z=…` (coordenada medida de um run anterior) | o jogador **não digita coordenada**: a ordem ancora na **mira**/posição dele (`BuildFindCommand.Hint`, `DevBridge/Commands.cs:1313-1321`). Repetir o **mesmo ponto** é o que permite comparar runs — e é justamente o que **ninguém** faz no jogo |
| **`BlueprintFree = true`** | peça de graça, sem tirar de mochila/baú (`Core/ModConfig.cs:979`, padrão **false**) | **dinheiro infinito de teste**: no jogo a casa custa o mesmo que qualquer obra e é paga peça por peça (`BuildStock.Take`) |
| **`PlaceReach = 15`** | alcance de colocação esticado (`:965`, padrão **10**, faixa 5..20) | é config **do jogador** para partidas impacientes; o run usa **15** (contra o padrão **10**) e é isso que permite afirmar que a cumeeira **não** parou por alcance — ela parou por **rota** (22,8 m de distância, `no progress`) |
| **`LevelGround = false`** | nivelamento desligado (`:976`, padrão **true**) | **contorno** de um defeito (item (c) do §17.18): no jogo o nivelamento é o padrão e a obra **tem** de voltar quando ele dá `too_steep` |
| **mochila zerada (`companions.inventory action=clear`)** | `RemoveAll()` na mochila de cada companheiro (`DevBridge/Commands.cs:1836-1847`) | ferramenta **de teste, declarada como tal** no código: apaga os stacks sem dropar nada. No jogo o companheiro carrega o que tem e a guarda de peso é **real** (`MHR_Build.cs:3120-3128`) |
| **`companions.order action=autopickup_off`** | auto-pickup **do companheiro** desligado (`DevBridge/Commands.cs:2397-2401`) | evita que a mochila recém-zerada encha de lixo andando pelo canteiro; no jogo o **auto-pickup é o do jogador** e a configuração dele vale — o mod não o desliga |
| **ferramentas dadas por comando** | `devcommands` + spawn/equipamento pelo bridge | no jogo a ferramenta é a do **jogador** (ou o companheiro pega a dele); o mod não dá item |
| **talhador temporário** | construído para **aprender a receita** e **apagado antes da ordem** | é a condição que faltava (§17.17) e agora está no save do jogador; o run não prova que a peça aparece sozinha num mundo sem a receita |
| **jogador posicionado uma vez** | posição fixa antes da ordem e depois **livre** (andando no canteiro) | o jogo é andar; a posição inicial fixa existe para os runs começarem iguais |

**Produto (o que o jogador recebe, com o default do código):**

| item | default | onde |
|---|---|---|
| **âncora na mira/posição do jogador** | — (é a ordem) | `BuildFindCommand.Hint` (`AI/BuildFindCommand.cs:127-149`); sem mira, jogador + 4 m (`DevBridge/Commands.cs:1318-1321`) |
| **peso e auto-pickup do jogador** | do jogo | o companheiro **sai** da obra com `hc_ai_build_too_heavy` quando passa do teto (`MHR_Build.cs:3120-3128`; com o canteiro inteiro pesado a ordem morre) — a mochila do jogador e as regras de pickup **não** são tocadas |
| **`[Building] PlaceReach`** | **10** (5..20) | `Core/ModConfig.cs:161`/`:965` — `MaxPlace` do `MHR_Build` |
| **`[Building] DeckLift`** | **1** (0..3) | `:164`/`:966` (o deck) |
| **`[Building] SupportFallback`** | **true** | `:167`/`:967` (o poste de emergência) |
| **`[Building] BodyRetry`** | **true** | `:171`/`:968` (a barreira de andar) |
| **`[Building] StepAside`** | **true** | `:174`/`:969` (o passo de 2,2 m) |
| **`[Building] LevelGround`** | **true** | `:182`/`:976` (nivelar antes de construir) |
| **`[Building] TempScaffold`** | **false** | `:187`/`:978` (escadinha provisória — **vetada pelo jogador**) |
| **`[Building] BlueprintFree`** | **false** | `:188`/`:979` (só teste/criativo) |
| **`[Building] BlueprintStations`** | **true** | `:191`/`:980` — **medida no Run 13**: dispensa a bancada **só no blueprint**; template e peça única seguem na regra do vanilla (`AI/BuildStationCover.cs:35-42`) |
| **receita não aprendida** | **não** há chave de config | a linha é descartada com `recipe not unlocked` (`AI/BuildBlueprintSkip.cs:25-36`) — **não** existe `BlueprintUnknownRecipes` no código: quem decide é `Player.IsRecipeKnown` |

- **O que NÃO vira default de jogo:** `BlueprintFree`, `PlaceReach` alto, mochila zerada, `autopickup_off`, âncora fixa
  e ferramenta de console. Nenhum deles é "o mod funcionando"; todos são **bancada**.
- **A frase "ninguém vai fazer a mesma construção no mesmo lugar" é uma regra de desenho:** o canteiro dos runs é
  **preparado** (o mesmo ponto, a mesma casa, o mesmo chão já nivelado por um run anterior). Se o código passar a
  **depender** disso — âncora que só fecha num ponto, plano que só sobe com o terreno já plano, peça que só entra com a
  mochila vazia —, o run bonito esconde um bug para o jogador. É por isso que esta seção existe: **número de run não
  é promessa de comportamento**.

### 17.20 O Run 14 (11/11/2026) — o ping-pong do `StepAside` e o walk spot 7 m abaixo

> A numeração é a do **contador de runs do jogador**; a seção anterior é o **Run 13** (§17.18). Este run nasceu do
> relato do jogador no fim da obra — *"eles tão muito travados aqui no final da build"* (`AI/BuildAsideSpot.cs:7`) — e
> mediu **dois** defeitos de uma vez: o passo do `StepAside` caindo **na peça seguinte** e o **walk spot** de uma peça
> alta saindo **7 m abaixo** dela. O lote `1ee93b1` (11/11/2026) consertou os dois.

**Resultado do run:**

- **984 colocadas + 1 pulada = 985** — o total do plano é **985**, como no Run 13 (que era **983 do arquivo + as 2
  `stone_floor_2x2`**).
- A **única** peça fora do plano é um `wood_roof_45` em **(-161,9, 46,8, -426,2)** por **`blocked_by_player`** — é o
  corpo na peça, **não** o `no progress` da cumeeira do Run 13: pose diferente (46,8 × 48,8) e motivo diferente.
- O que este run **não** informa nesta seção (não lido para isso): postes de emergência, peças no canteiro, mortes e
  `falls`. O que ele mede é o **mecanismo** dos dois defeitos (abaixo), não o placar.

**Run 13 × Run 14 — o placar é o mesmo e não é o ponto:**

| | Run 13 (§17.18) | **Run 14** |
|---|---|---|
| plano | 985 (983 + 2 de pedra) | **985** (o mesmo) |
| colocadas | 984 | **984** |
| **puladas** | **1** | **1** |
| motivo da pulada | `no progress` (cumeeira em **48,8**) | **`blocked_by_player`** (peça em **46,8**) |
| defeito que o run expôs | **`no progress`** da peça alta (a rota; origem do `BuildWalkSpot`) | walk spot **7 m abaixo** + **ping-pong do `StepAside`** |

#### (A) O `StepAside` caía na peça seguinte — o ping-pong **medido**

O **defeito** que o log mostrou (as linhas do `MHR_Build.cs:6296`/`:6375` e a peça seguinte):

```
"Companheiro 6" está na peça 909 - pedindo para sair da frente
saindo da frente (peça em -160,7, -424,6) para (-162,3, -426,2)      → o destino é a peça 910
"Companheiro 6" está na peça 910 - pedindo para sair da frente
```

- **Como ler:** o `StepAside` escolhia o destino como **o mais perto dos 8 pontos a 2,2 m** ao redor da peça — uma
  regra que **não conhece o plano**. Na **última fileira do telhado** as peças do plano ficam a **~2 m** umas das outras
  (as fileiras de telhado/empena são de 2 m), então um passo de 2,2 m **cai na seguinte**: a saída da peça **909** ia
  **em cima da 910**, a da **910** caía na **911**, e dois companheiros passaram o fim da obra **se trocando em volta da
  cumeeira** enquanto o plano **não andava**.
- **A conta fecha com as duas poses medidas:** o destino (-162,3, -426,2) está a **0,4 m** em XZ da pose da peça que
  foi **pulada** (-161,9, -426,2) — o companheiro saía da frente de uma peça **para dentro** da seguinte, que é o que
  o filtro novo proíbe (abaixo). É aritmética sobre as duas poses do log, não uma medida nova.

**O conserto — `AI/BuildAsideSpot.cs` (novo, puro) + o anti-repetição no grupo:**

- **A regra pura** (`AI/BuildAsideSpot.cs`, **nenhum** `Physics`/`Time` — vetores e floats, como o `BuildWalkSpot`),
  com **18** casos na seção **(y)** do harness:
  - anel de **8** direções (`Ring`, `:46`) a **2,2 m** (`MHR_Build.AsideDistance`, `MHR_Build.cs:6322`);
  - **rank** (`Rank`, `:177`): **0** = atrás do colocador (`dot ≥ 0,5` com o vetor "para trás"), **1** = os dois
    **lados** (`dot > -0,5`), **2** = a **frente — que nunca é usada** (é onde o plano tem as peças seguintes);
  - dentro do mesmo rank (`Better`, `:134`): a direção mais **"atrás"** primeiro, depois o **passo mais curto**, e as
    coordenadas como desempate (o resultado não depende da ordem do anel — `TieSlack`/`CoordinateSlack`, `:49`/`:54`);
  - **filtro** (`Blocked`, `:193`): o destino a menos de **1,5 m em XZ** (`Clearance`, `:39`) de uma peça **pendente**
    do plano **dentro de ±2 m de altura** (`Height`, `:43`) **sai da lista** — piso do andar de cima não bloqueia;
  - **nenhum candidato = nenhum pedido** (`Plan` responde **0** — o laço para em `:117-120` e o `return count` está
    em `:126`).
- **O lado Unity** (`MHR_Build.AsideSpot`, `MHR_Build.cs:6389`): as peças **pendentes** (não feitas e não desistidas) num
  raio de **2,2 + 1,5 = 3,7 m** vêm do `MHR_BuildGroup.PendingNear` (`Jobs/MHR_BuildGroup.cs:860` — varre o plano do
  grupo, `Done`/`SkippedIndex` fora); o `BuildAsideSpot.Plan` ordena; o **primeiro** candidato que é lugar de ficar
  **ganha**: chão por um raio **para baixo** de 4 m que começa **1,5 m acima** do ponto, **mesmo nível** (|Δy| ≤ 0,8 m,
  um passo e não uma queda) e **passo seguro** (`MHR_Mover.SafeStep` — `StepTo`, `MHR_Build.cs:6426`). Um destino a
  menos de **1 m do próprio colocador** é descartado (`:6404-6409`): a 1ª preferência olha para trás, que é onde ele
  está — o `SafeStep` **não tem personagem na máscara** (o comentário do lote, `:6403-6405`) e o companheiro andaria
  **para dentro** de quem pediu.
- **Sem destino livre, o pedido não é feito** (`AskStepAside` devolve **true**, `MHR_Build.cs:6269-6277`): a peça vai
  **para a fila do canteiro** (`NoteBody` + `QueueRetry` + `Advance`, `:6063-6065`) e a **barreira de andar continua
  desenhada** — o plano não sobe por cima da peça só porque um companheiro não pôde sair. A linha do log diz o motivo:
  `sem lugar livre para ele sair da frente, não peço (a peça vai para a fila)`.
- **Anti-repetição por dupla** (`MHR_BuildGroup.RequestStepAside`, `MHR_BuildGroup.cs:791`): a mesma dupla
  (quem coloca, quem sai) sobre a **mesma peça** em < **4 s** (`StepAsideRepeat`, `:49`) é `Deduped`; a mesma dupla em
  **peça diferente** em < **20 s** (`StepAsidePairSeconds`, `:58`) responde **`Repeating`** — **não pede**, a peça vai
  para a fila (`MHR_Build.cs:6281-6291`). As chaves são `PairKey` (`:819`, um `long` com os dois ids) e o dicionário
  `Pairs` (`:136`), limpo junto com a fila (`ClearBodies`, `:775`). **Detalhe que importa:** o registro do par **só é
  escrito quando o pedido é aceito** (`:812-813`), então o `Repeating` **não renova** a janela — a dupla fica sem
  pedidos até **20 s depois do último pedido aceito**, e não 20 s a cada recusa. É a rede **de tempo**: o filtro das
  peças pendentes é a rede **de espaço**, e as duas cobrem o mesmo caso por lados diferentes.
- **Custo:** `PendingNear` varre o plano (~**1000** `PlannedStep` no `CommHomeBasicBP`) **por pedido** — alguns por
  minuto, nunca por frame (o buffer é único e reusado, `MHR_Build.cs:116-120`).

#### (B) O walk spot saía 7 m abaixo — os anéis de 1,5/3 m e o `WalkHits`

- **A linha medida:** `no progress ... (23,4 m away, walk spot (-161,1, 41,9, -422,4))` para uma peça de **telhado**.
  O walk spot é o ponto **de onde** a peça é colocada (o `BuildWalkSpot` do Run 13, §17.18) — e ele saía em **41,9**
  para uma peça a **48,8**: **7 m abaixo** dela, com o companheiro **parado longe** (`23,4 m`) até a trava de 12 s.
- **A causa:** os raios eram na **vertical da peça** (0,8/1,6/2,4 m) e cada coluna pegava só o **primeiro** hit. Só que
  no **topo da empena** o plano não tem **piso embaixo** da peça: a coluna atravessava a empena e achava o piso do
  andar de baixo, e a fileira **de onde a peça é colocada** está **ao lado**, não sob ela. Além disso, uma coluna cujo
  1º hit **não é superfície** (o **corpo** de um companheiro passando por cima do ponto, a peça de cima, mobília) era
  **perdida inteira** — no comentário do lote, com **5 companheiros no telhado** isso era a maior parte do anel.
- **O conserto** (`MHR_Build.WalkSpot`, `MHR_Build.cs:5797`):
  - **anéis** de **1,5 m** e **3 m** (`s_walkRadii`, `:99`) além da coluna da própria peça: **17 raios** por peça
    (1 + 8 + 8). As fileiras de telhado/empena são de **2 m**, então 3 m alcança a fileira vizinha e o piso a que as
    escadas do plano levam;
  - **`WalkHits = 3`** (`:106`): a coluna **atravessa** até **3** hits que **não são superfície** — corpo em cima do
    ponto, peça acima, mobília — em vez de perder a coluna no primeiro; o 1º que é **superfície estrutural** (terreno
    ou peça com `WearNTear` que não é mobília, `StructuralSurface`) é o que ela vale, e a superfície aceita tem de estar
    **abaixo da peça** ou no máximo **0,5 m acima** (`<= peça + 0,5 m`, `BuildWalkSpot.SameStoreyAbove`);
  - **dedup por collider + altura** (`NoteSurface`, `:5858`): duas direções que caem no **mesmo** piso/telhado contam
    **uma** superfície (o plano é largo; sem isso o `N` do log não significaria nada);
  - **log com o "onde olhou"**: `walk spot (x, y, z) - a peça está X m acima (N superfície(s) em M raio(s))`
    (`:5654-5656`) e, na linha do `no progress`, o walk spot **e** os contadores (`:5704-5707`) — o `M raio(s)` é o
    **custo medido no próprio run**.
- **Custo e frequência:** **17 raios/peça** no caso comum (até **51** com corpo em cima em todas as colunas), **uma
  vez por índice** (`EnterStep` zera `WalkRays`/`WalkFound`, `:1141-1142`) e **de novo quando a pose muda**
  (`:5544-5545`), **nunca por frame**. O **`PlaceReach` não mudou**: os anéis mudam **onde** as superfícies são
  procuradas, não a distância de colocação (a regra do olho continua sendo o teto).

#### O que fica **a medir no Run 15**

1. **A cumeeira sobe?** A peça que ficou fora no Run 13 (`wood_roof_45` em (-152,6, 48,8, -423,6), `no progress` de
   22,8 → 23,5 m) é o teste do (B): com os anéis ela tem de achar a superfície **ao lado** e chegar. Essa pose é fixa e
   reproduz o caso; se o `no progress` voltar, o log agora diz **quantas** superfícies e **quantos** raios — é o mesmo
   experimento, com o dado que faltava.
2. **O ping-pong acabou?** O Run 14 mostrou o **defeito**; o lote muda o **destino** e põe o corte de 20 s. O que se
   mede no Run 15 é a **contagem** das linhas `pedindo para sair da frente`/`saindo da frente` **por peça** e quantas
   vezes aparece o `não peço` (sem lugar livre) e o `não peço de novo` (`Repeating`) — **nenhuma** dessas contagens foi
   feita ainda (é a mesma dívida do item (8) das pendências do `AGENTS.md`).
3. **A peça pulada por corpo volta?** No Run 14 a única fora foi `blocked_by_player` em (-161,9, 46,8, -426,2). Com o
   pedido podendo **não ser feito**, a peça vai para a fila — o Run 15 diz se ela **sobe** quando o corpo sai (e a
   barreira de andar é o que a mantém na vez).
4. **O custo:** o log passa a imprimir `M raio(s)` por peça e o `PendingNear` roda por pedido — nenhum dos dois foi
   cronometrado. O número sai de graça do Run 15 (é só ler as linhas), mas **não** foi medido em ms.

**O que o Run 15 (§17.21) respondeu disto:** a **cumeeira não subiu** — a peça fora foi outra `wood_roof_45`, em
(-151,3, 48,8, -431,3), por `no progress` —, e a **contagem** das linhas `pedindo para sair da frente` saiu (**33**, com
a ressalva de métrica do §17.21); o **custo em ms** continua sem medida. O A/B completo está no §17.21.

**Contas e limites desta seção:** o run é o **Run 14** do jogador (**984 + 1 = 985**, a única pulada por
`blocked_by_player` em (-161,9, 46,8, -426,2)); as duas linhas do ping-pong e a do `no progress` com `walk spot` são as
**do log do run**, e a distância de **0,4 m** entre o destino e a peça pulada é **aritmética** sobre as poses medidas.
O resto é o **medido fora do run**: harness **684 PASS** (666 + **18** da seção **(y)**, seção (x) com **10**), build
**0/0** e gate **9/9** no lote `1ee93b1`; **config 365/25** (o lote **não** acrescentou chave) e **nenhuma chave de
tradução nova** (as linhas novas são **log em PT-BR**, não texto de jogador). Esta seção **não** rodou harness, build nem
gate — os números são os do lote, e o que o run **não** contou está listado acima.

### 17.21 A dispersão dos workers (11/11/2026, `[Building] SpreadWorkers`, lote `a29dedc`) — o A/B dos Runs 15 e 16

> Continuação do **Run 14** (§17.20) pelo contador de runs do jogador. O Run 14 mediu **dois defeitos** de rota e de
> passo; este lote ataca a **disputa** que o jogador descreveu — *"eles parecem estar disputando pra ver quem coloca a
> peça primeiro, em vez de trabalhar em equipe"* (`AI/BuildSpread.cs:7`) — e mede o efeito num **A/B**. **A ressalva do
> fim vale para tudo:** no run em que a dispersão estava ligada o contador dela saiu **0**.

#### (A) Por que existe: a ordem do plano põe vizinhos em índices vizinhos

- O plano de um blueprint é ordenado por **andar → classe → altura** e cada run de chaves iguais é percorrida como
  caminho de **vizinho mais próximo** (`BuildBlueprintParser.Ordered`/`NearestWithin`, §15.2u). A consequência é direta:
  **índices consecutivos são vizinhos no espaço**, então cinco companheiros no mesmo plano chegam ao **mesmo canto** ao
  mesmo tempo. A medida do Run 14: **88 `corpo na peça`** e **32** pedidos de step-aside (§17.20).
- **A regra** (`AI/BuildSpread.cs`, **puro**: vetores e floats, sem `Physics`, sem `Time`, sem grupo — a tabela é a seção
  **(z)** do harness):
  - ao pegar a próxima peça, o worker olha uma **janela de 6** (`Window`, `:41`) — a peça dele e as 5 seguintes,
    **até o fim da run** (`SameRun`, `:127`: `Level` e altura com folga de **0,01 m** — `KeySlack`, `:47`, a mesma do
    parser — e a **classe exata**; a primeira peça de **outra run encerra** a janela, porque é o próprio plano que diz
    onde a fileira acaba);
  - escolhe a **primeira** da janela que está a **≥ 3 m em XZ** (`SpreadDistance`, `:34`) de **todo claim ativo** de
    outro worker (`Far`, `:58`; `Pick`, `:94`). Os 3 m são **duas janelas de corpo** (`BuildAsideSpot.Clearance` =
    1,5 m): uma é "estar no caminho", duas é "trabalhar sem pisar no outro";
  - **nenhuma livre → pega a primeira mesmo** (`Pick` responde **0**): é **exatamente o comportamento antigo**, e é o
    **fallback** que garante que a peça nunca fica sem dono;
  - candidata que **não é número** nunca é escolhida, e um **claim** que não é número é **ignorado** — uma pose quebrada
    no canteiro não pode congelar a equipe;
  - os claims vêm de `MHR_BuildGroup.ClaimPoses` (`Jobs/MHR_BuildGroup.cs:515`) com **claim expirado fora**
    (`ClaimSeconds` = 20 s): é "o que a equipe está construindo **agora**", não o histórico.
- **O que NÃO muda:** o **plano nunca é reordenado**, e os **claims**, a **barreira de andar** e o **teto de retry**
  ficam iguais. A peça que o worker passou não é perdida: entra na **fila que já existe**, marcada `Spread`
  (`RetryEntry.Spread`, `MHR_BuildGroup.cs:81`, escrita por `NoteSpread`, `:1077`), **sem gastar tentativa** (nada
  falhou), e só é puxada quando a **vizinhança está livre** (`SpreadFree`, `:1117`, dentro do `NextRetry`, `:1152`). Um
  **retry real** — a peça foi caminhada e não ficou de pé — **nunca** ganha essa espera: `TryQueueRetry` limpa a marca
  (`:1043`), porque a falha espera **o que a segura**, não espaço.
- **Fast path:** `SpreadOn` (`MHR_Build.cs:999`) só liga com **plano + blueprint + grupo compartilhado + a chave**.
  Template, peça única e **1** worker não entram — o caminho é byte a byte o de antes.
- **A chave:** `[Building] SpreadWorkers` (bool, **padrão true**, `Core/ModConfig.cs:978`; rótulo
  `hc_cfg_building_spreadworkers`, "Spread the crew out", `Core/ModLocalization.cs:850`), lida a cada uso. O
  `CurrentConfigVersion` foi para **199** (`ModConfig.cs:1278`) **sem migração** — não há default antigo a corrigir.
- **O log:** **uma** linha no `Finish` (`MHR_Build.cs:7551`), `dispersão: N adiada(s), M retomada(s) · step-aside: K
  pedido(s)` — os três números que no Run 14 exigiram `grep` linha a linha —, e o detalhe por salto só com
  `LogDetailed` (`:7686`): `dispersão: a peça X/N fica para depois - pegando Y (K adiada(s))`. O `K` é `AsideAsked`
  (`:6438`) e conta **todo** pedido aceito; a linha crua `… pedindo para sair da frente` é **1 por peça**
  (`AsideLogged` zerado em `EnterStep`, `:1270`).

#### (B) O A/B (11/11/2026): a mesma casa, a mesma âncora, 5 workers, canteiro vazio nos dois

| | **Run 15** (árvore `1ee93b1`, **sem** a dispersão) | **Run 16** (árvore `a29dedc`, **com**) |
|---|---|---|
| colocadas | **984 + 1 pulada = 985** | **985/985** |
| a única fora | `wood_roof_45` em **(-151,3, 48,8, -431,3)** por **`no progress`** | **nenhuma** (`FALTANDO` vazio, **23/23** prefabs) |
| `corpo na peça` | **194** | **50** |
| `pedindo para sair da frente` | **33** | **12** (`step-aside` da linha do `Finish`) |
| `no progress` | **66** | **4** |

- **O Run 16, além da tabela:** **0 mortes**, **0 `UNSUPPORTED`**, **watchdog `0`** e **1014 peças** no canteiro.
- **As duas árvores diferem só nisso:** `1ee93b1` → `0f33e54` (**só doc**) → `a29dedc` (a dispersão). Os dois consertos
  do `1ee93b1` — o **destino** do `StepAside` e o `WalkSpot` em **anéis de 1,5/3 m** com `WalkHits = 3` (§17.20) —
  estão **nas duas**, então o A/B é "com a chave × sem a chave", não "antes × depois" dos consertos.
- **A queda dos três contadores é real como número, e é só isso que ela vale.** O run com a dispersão **fecha a casa**
  (985/985) e o baseline deixa a **cumeeira** fora de novo (48,8, `no progress`) — a mesma peça que o Run 13 perdeu em
  (-152,6, 48,8, -423,6) e que o Run 14 perdeu em outra pose, a 46,8 (§17.20). Isso é um **fato do run**, não a prova
  do mecanismo.
- **Ressalva honesta — o contador `dispersão:` saiu `0 adiada(s) / 0 retomada(s)`.** Neste run a regra **nunca precisou
  adiar ninguém** (a primeira da janela já estava a 3 m dos claims em toda peça). Se ela **não agiu**, a queda dos
  marcadores **não se atribui a ela**: o que falta é o **A/B limpo** — a **mesma árvore**, `SpreadWorkers = false` ×
  `true` —, que **não foi rodado**; e, mesmo nele, caminhos, corpos e tempo não se repetem, então o ruído é grande.
- **Uma segunda ressalva, de métrica:** o **33** do Run 15 são **linhas de log** (uma por peça com pedido, `AsideLogged`
  em `MHR_Build.cs:1270`) e o **12** do Run 16 é o **contador** do resumo (`AsideAsked`, `:6438`), que conta **mais**
  do que as linhas (todo `Asked`). Como o contador é o lado que conta **mais**, a **direção** (caiu) é confiável — no
  Run 16 houve **no máximo 12** pedidos —, mas os dois números **não são a mesma unidade**.
- **O que este A/B NÃO diz:** que a dispersão seja a causa de a casa fechar; que o `WalkSpot` em anéis baste (o **Run
  15** tem os anéis e **não** fechou); e **nada** sobre custo — o `ClaimPoses` roda **por peça** da janela, nunca por
  frame, e **não foi cronometrado**.

#### (C) O Run 17 — em andamento

**Run 17** (11/11/2026, **canteiro zerado de propósito**, dispersão ligada) é a confirmação da **casa do zero**, sem
herança de peças de runs anteriores no terreno. **Status: em andamento — sem número** (o placar entra aqui quando o
jogador fechar o run). O que ele responde:

1. a **casa do zero** fecha com a dispersão ligada? (o Run 16 fechou, mas sobre o canteiro dos anteriores);
2. o contador `dispersão:` sai **≠ 0** em algum run? É a pergunta que o Run 16 deixou aberta — a regra existe, mas
   **não se mediu** um run em que ela **adie** peça;
3. as linhas de disputa (`corpo na peça` / `pedindo`) se sustentam no mesmo patamar (**50** / **12**) ou o número do
   Run 16 era sorte de caminho.

**Contas e limites desta seção:** o A/B é o do jogador — **Runs 15 e 16**, de 11/11/2026, mesma casa/âncora/5 workers
e canteiro vazio nos dois; os números são os **do log dos runs**, e o único cálculo aqui é a aritmética óbvia
(`984 + 1 = 985`). O **medido fora dos runs**: harness **706 PASS** (684 + **22** da seção **(z)** — **11** de `Pick`,
**6** de `Far`, **5** de `SameRun`; hoje **711**, ver §17.22), **config 367/25** (`SpreadWorkers` é a **367ª**; o
`package/CONFIGURATION.md` foi
regenerado e o `--check` responde `[ok] … (367 settings, 25 sections)`), build **0/0**, gate **9/9** e MCP **122**.
**Nenhuma** chave de tradução de jogador nova — a linha da dispersão é **log em PT-BR**. **Localização (fechada no
ciclo do Run 18):** o rótulo `hc_cfg_building_spreadworkers` entrou nos **4** JSONs (PT/ES/FR/IT) e o
`audit_translations.py` fechou em **0 ausentes / 0 sobrando / 0 categorias ausentes** nos quatro idiomas — ver
`AGENTS.md`, pendências. **Não medido:** o
custo por peça do `ClaimPoses`, **quantas** peças a dispersão adiou (o contador deu **0** no Run 16; no Run 18 deu
**1012**, mas ver §17.22(B) — não é tamanho de fila), o **A/B limpo** e o **Run 17**.

### 17.22 O Run 18 (11/11/2026) — a `fila` era uma barreira invisível (e o Run 19 é o A/B)

> Continuação do §17.21. A linha da `espera` que o Run 17 estreou mediu, no Run 18, **mais tempo parado do que todos os
> marcadores de disputa juntos** — e a revisão independente mostrou que **80 %** dele não era disputa nenhuma: era um
> worker preso esperando uma fila que ele **não precisava** esperar. Este lote corrige a causa em **duas linhas** e
> instrumenta o que faltava medir. *No dia, o A/B (Run 19) ainda não tinha rodado — os números daqui são de **um** run.
> Depois ele foi medido no **§17.24**: o Run 19 (por frame) foi **revertido** e o **Run 22** (1 Hz) fechou o A/B.*

#### (A) O Run 18 medido

| parâmetro | valor |
|---|---|
| plano | `CommHomeBasicBP`, **985 peças** |
| companheiros | **5** |
| `[Building] DeckLift` / `PlaceReach` | **1** / **15** |
| `BlueprintFree` / `LevelGround` | **true** / **false** |
| canteiro | **vazio** |
| jogador | **andando** (livre depois da ordem; entrou no canteiro várias vezes) |
| duração | **14 min 04 s** (**844 s**, relógio **externo** — o `LogOutput.log` **não tem timestamp**) |

**Resultado do run:** **985/985 colocadas**, **0 puladas**, **0 mortes**, **0 `UNSUPPORTED`**, **watchdog `0`**,
**1010 peças** no canteiro (**985 + 25 postes**), **25 postes** de emergência, dispersão **1012 adiada(s) / 32
retomada(s)** (ver (B) — não é tamanho de fila), `corpo na peça` **33**, `blocked_by_player` **58** (**48** recusas de
peça por corpo + **10** linhas `sem lugar para o apoio`) e `desistindo` **0**.

**A espera, por worker** (a linha do `Finish`, formato do Run 17 — sem `fim`/`meio` e sem os segundos do portão):

| worker | espera | barreira (corpo / dependência) | `BodyHolds` | fila | passo |
|---|---|---|---|---|---|
| C2 | **255,0 s** | 54,8 s (0,9 / 53,9) | 16 | 197,1 s | 3,1 s |
| C3 | **242,0 s** | 28,5 s (6,3 / 22,2) | 12 | 211,0 s | 2,5 s |
| C4 | **260,4 s** | 47,9 s (4,8 / 43,1) | 13 | 212,5 s | 0,0 s |
| C5 | **289,9 s** | 62,6 s (3,9 / 58,6) | 16 | 225,2 s | 2,1 s |
| C6 | **245,5 s** | 51,1 s (6,8 / 44,3) | 16 | 194,4 s | 0,0 s |
| **total** | **1292,8 s** | **244,9 s** | **73** | **1040,2 s (80 %)** | **7,7 s** |

**Contas (fecham):** 255,0 + 242,0 + 260,4 + 289,9 + 245,5 = **1292,8 s**; 197,1 + 211,0 + 212,5 + 225,2 + 194,4 =
**1040,2 s** (80,46 % do total); 54,8 + 28,5 + 47,9 + 62,6 + 51,1 = **244,9 s** (22,7 de corpo + 222,1 de dependência,
arredondamento de 0,1); os `BodyHolds` somam **73**; o passo, **7,7 s**. O **portão saiu `0`** — e esse zero é **sintoma,
não medida** (ver (C)).

#### (B) O que a revisão independente corrigiu — duas correções que importam

1. **A soma é por worker, não do run.** 1292,8 s ÷ 5 = **258,6 s por worker** num run de **844 s** = **30,6 %** do
   **tempo-homem**. E **4 dos 5** workers encerraram **antes** (`Leave`, `last=false`): só a espera do **último**, na
   parte crítica, encurta o run. **Nada se dimensiona pelos 1040 s** — é soma de cinco esperas que correram **em
   paralelo**.
2. **`1012 adiadas` não é tamanho de fila.** `state.SpreadSkipped += passed` (`MHR_Build.cs:1210`) soma o
   `passedCount` do `BuildSpread.Pick`, que é o **índice do escolhido** (`AI/BuildSpread.cs:113`; no máximo **5** por
   peça) e o `NoteSpread` **deduplica por índice do plano** (`MHR_BuildGroup.cs:1141-1152`). É **cota superior** de
   entradas criadas, não a fila — a fila do canteiro tem no máximo um índice por peça pendente.

E dois achados de leitura que mudam a interpretação dos baldes:

- **"dependência" (222,1 s) não é falta de apoio.** O `PendingBelow` (`MHR_BuildGroup.cs:1256`) **não olha o relógio
  nem os claims** (`:1251-1254`, escrito no próprio código): boa parte desses 222 s é esperar **o colega que constrói
  a peça de baixo** — que é o desenho (a barreira serve a peça de baixo antes de subir), não um defeito.
- **`entry.Blocked` nunca é zerado** (`MHR_BuildGroup.cs:1075-1080` acha a entrada existente e `:1097` incrementa):
  um corpo que passou **uma vez** pelo lugar respondia "corpo" em **toda** espera seguinte daquela peça, **inflando**
  "corpo" e **deflacionando** "dependência".

#### (C) O defeito: `:3620` e o `ahead`

O caminho da espera (`WaitRetry`) chamava o `Advance` — o único que pergunta o portão do canteiro (`MayTake`) e a
barreira de andar — **só quando a fila do canteiro estava vazia** (`:3620`). Só que o `Advance` **já tem** essa guarda
no fim do plano (`:1227`): a condição do lado de fora era **redundante e mais restritiva**, e o efeito foi o inverso
do pretendido.

**Quem entrava na espera no MEIO do plano** — e entra por **duas portas**, o portão do canteiro (`:1151`) e a barreira
de andar (`:1433`) — **nunca mais re-perguntava o `MayTake`**. Ficava parado até o `entry.At` vencer (**2 a 25 s**,
`BuildFailure.BlockedDelay`/`DependencyDelay`) **com peça livre logo à frente**, e o tempo caía em **`fila`**, não em
barreira. É a explicação do "3 parados de uma vez": três workers no mesmo estado, cada um esperando um relógio de fila
que não era o caminho.

**E é a explicação do `BarrierHolds` = 0:** ele só conta quando o portão é desenhado **fora** do `WaitRetry`
(`if (state.Phase == Phase.WaitRetry) … else state.BarrierHolds++`, hoje `:1164-1172`). Como os workers já estavam em
`WaitRetry`, o portão respondia `false` sem contar — **nenhuma** retenção de portão no resumo, com 197–225 s de "fila"
por worker.

#### (D) O que foi aplicado (sem config nova, sem tradução nova)

- **O patch de comportamento:** `bool ahead = state.Index + 1 < state.Plan.Count; if (ahead) return Advance(ai, state,
  dt);` — `MHR_Build.cs:3689-3693`, com o porquê escrito no comentário (`:3684-3688`). No **fim do plano** o caminho
  continua **byte a byte** o de antes; o `PreferNearFloor` não roda em grupo (`PlanLocked`), então **não há
  reordenação por frame**.
- **O livelock existe e é limitado:** o `Advance` pode devolver "espera" e o `WaitRetry` voltar a ele, mas o relógio é
  único — `BodyRetryMaxWait` **é** `BodyBarrierSeconds` = **10 s** (`AI/BuildFailure.cs:61`/`:86`, o "relógio único"
  do Run 17) — e o watchdog de **60 s** (`StallSeconds`) é o teto. O custo do livelock é **uma pulada**, e é por isso
  que `puladas` é o **guarda do run** (abaixo).
- **Instrumento novo:** `WaitGate` (`MHR_Build.cs:480`, acumulado em `:1168`), o tempo do **portão**, que **não caía
  em balde nenhum** porque o `Advance` roda com `dt = 0` (default em `:1113`); `WaitQueueEnd` **×** `WaitAhead`
  (`:467`/`:474`, a fila partida por **onde o caminho está**); `Advance(..., float dt = 0f)` repassando o `dt` ao
  `StoreyBarrier` (`:1350-1387`, que antes contava **zero** no frame em que desenhava a barreira); e
  **`BlockedRecently`** (`MHR_BuildGroup.cs:752`, lido em `MHR_Build.cs:1550`) no lugar de `Blocked > 0` — corpo só
  se `Time.time - BodyAt[index] < BodyBarrierSeconds`, com o carimbo idempotente do `NoteBody`
  (`MHR_BuildGroup.cs:782-786`).
- **A linha do `Finish`:** `fila E s (fim P s, meio Q s)` e `portão N (G s)`, e o **`total` passou a incluir o
  portão** (`MHR_Build.cs:7688-7696`). O `N` é o `BarrierHolds` (quantas vezes o portão segurou) e o `G`, os segundos
  dele — não confundir com as retenções da **barreira** (`BodyHolds`, a coluna da tabela do (A)).
- **O relatório:** `tools/build_run_report.py` entende **os dois formatos** (`:184-196`) — o baseline do Run 18
  continua legível — e imprime a fila por metade e os segundos do portão (`:656-682`).

#### (E) O corte do A/B (Run 19) — **rodado e fechado no §17.24**

O Run 19 é o **A/B** da mesma casa, com este patch. Aceite:

| medida | corte |
|---|---|
| `fila` do resumo | **≤ 400 s** (era 1040,2 s) |
| `WaitAhead` (fila do **meio** do plano) | **≈ 0** (por construção; se crescer, o `ahead` parou de pegar o caso) |
| espera do **último** worker | **≤ 120 s** (a maior medida foi **289,9 s**, o C5) |
| duração | **≤ 720 s** (era 844 s) |

**Reverte** o lote se aparecer **`puladas > 0`**, **`desistindo > 0`**, **morte**, **`UNSUPPORTED`** ou **watchdog**.
**Estado hoje:** build **0 erros / 0 avisos** (`-p:SkipDeploy=true`), harness **711 PASS**, `gen_config_doc.py --check`
`[ok] (367 settings, 25 sections)`. **O Run 19 foi rodado e o resultado está no §17.24:** o caminho **por frame**
(este `ahead` sem throttle) **dobrou** o tempo e foi **revertido**; o que valeu foi o **1 Hz** do Run 22 (§17.24(D)) —
`fila` **4,5 s**, duração **727 s** (**7 s** acima do corte de 720 s), **985/985**, **0 puladas**.

#### (F) Onde cada ponto mora, e o que não foi medido

As linhas citadas no lote são as da árvore **anterior** ao patch (as mesmas dos comentários do código). Mapa para a
árvore de hoje:

| ponto | árvore do lote | nesta árvore |
|---|---|---|
| a chamada do `Advance` só com a fila vazia (**o defeito**) | `:3620` | substituída por `:3689-3693` |
| a guarda de fim de plano do `Advance` | `:1227` | `:1149-1151` (o `while`) |
| o portão `MayTake` dentro do `Advance` | `:1151` | `:1160` |
| a contagem do `BarrierHolds` (fora do `WaitRetry`) | `:1149` | `:1164-1172` |
| a barreira de andar desenhada no `Advance` | `:1433` | `:1182` → `StoreyBarrier` `:1350` / `ServeBarrier` `:1397` |

**Não medido:** **quanto** dos 222,1 s de "dependência" é o colega e quanto é apoio que não veio (o `PendingBelow` **não
distingue** — é desenho, não medida); o **custo por frame** das chamadas extras do `Advance` (estimado em **~4,2 ms**,
§17.25); e se o `WaitAhead` fica mesmo ≈ 0. Também não se mediu **`1012` como fila** — e não se deve: ver (B), item 2.
O "**se o patch encurta o run**" desta lista **saiu**: medido no **§17.24** (**844 → 727 s**).

### 17.23 O `DeckLift` virou **teto** (11/11/2026) — a obra que subia 1 m no ar e caía

> O §17.10 mediu o deck funcionando na **casa de referência** (estacas enterradas → a obra sobe 1 m e o térreo fica 1 m
> acima do chão). Esta seção mede o **mesmo código no corpus**: cinco planos desenhados **no chão** subiam os mesmos
> 1,00 m e iam para o ar. A chave passou a ser **teto**, não deslocamento forçado.

#### (A) O que foi medido — terreno plano em **38,78 m**

| blueprint (rótulo do log) | linha de log medida |
|---|---|
| Meadow Stilt Hut · Forest Hut · Meadow House · Meadow Manor · Hobbit Repairhut | `a obra sobe 1,00 m (deck) - 0 peça(s) de fundação assentadas no terreno` |
| `MACCommHomeBasicBP` | `a obra sobe 1,00 m (deck) - 3 peça(s) de fundação assentadas no terreno` |

**A peça mais baixa da obra medida no mundo: 39,78 m** = **+1,00 m exatos** sobre o terreno (38,78). E a obra estava
**flutuando**: peça sem apoio não segura nada — a regra do jogo está medida em `docs/re/support-rule-wearntear.md` §5
(apoio = **0,15 m de sobreposição de collider**, a colocação **não** testa apoio e o `UpdateWear` aplica **100 % de
dano** no tick seguinte): a peça **cai**, **dropa o material** e volta para a fila — é o desperdício que o jogador viu.

**Os três runs de 1 companheiro** (os três "em andamento" do §5 de `docs/re/blueprint-corpus.md`; os tamanhos —
**60**, **96** e **62** peças — batem com `hs_meadows_house`, `hs_meadows_manor` e `hs_meadows_hobbit_repairhut`):

| plano | resultado | duração | fora |
|---|---|---|---|
| Meadow House (**60** peças) | **60/60**, 0 puladas | **120 s** | — |
| Meadow Manor (**96** peças) | **92/96**, **5 puladas** | **152 s** | **5** |
| Hobbit Repairhut (**62** peças) | **61/62**, **1 pulada** | **220 s** | **1** + **2 `UNSUPPORTED`** |

**Ressalva de conta:** em Meadow Manor **92 colocadas + 5 puladas = 97 contra 96 do plano** — **não fecha**. As duas
saídas possíveis são uma bancada inserida pelo `InsertBenchStep` (o plano cresce um índice) ou uma peça contada nos
dois lados; **não foi medido qual**. O número fica registrado como saiu do log, sem ajuste. (Hobbit fecha: 61 + 1 =
62.)

#### (B) A correção: `Clamp(buried, 0, wanted)`

- **O lift deixou de ser o valor da config** e passou a ser `Mathf.Clamp(buried, 0f, wanted)` — `MHR_Build.cs:1845`.
- **`buried`** = o quanto a **peça mais funda** do plano está **sob** o terreno: `GetGroundHeight(pos) - pos.y`
  (**> 0 = enterrada**), medido **uma vez por ordem**, dentro do `StartBlueprint` (`MHR_Build.cs:1830-1844`).
- **Banda morta de 0,05 m** (`:1848-1851`): uma peça **3 cm** enterrada **não** capa a config em 0,03 m — 0,05 m é a
  **mesma tolerância** do laço de assentamento (`:1860`), que é quem trata o resíduo.
- **Consequência:** plano desenhado no chão → **0,00 m**; estacas enterradas **2 m** → sobe o **valor da config** e o
  laço assenta o resto das pontas no terreno (o comportamento do §17.10, intacto). **A config virou teto.**
- **A descrição da chave** foi junto (`Core/ModConfig.cs:975`): *"The MOST the whole blueprint may rise above the
  ground: … It only rises what the plan actually needs - a plan whose poles are buried 2 m rises the full value, a
  plan already drawn on the ground rises nothing (a build lifted in the air has no support and falls). 0 keeps the
  blueprint's own level."* O `package/CONFIGURATION.md` foi **regenerado** e o `gen_config_doc.py --check` responde
  `[ok] (367 settings, 25 sections)` — **nenhuma chave nova**, só o texto.

#### (C) As linhas de log, antes × depois

| caso | antes | depois |
|---|---|---|
| os 5 planos rasos do corpus | `a obra sobe 1,00 m (deck) - 0 peça(s) …` | `a obra sobe 0,00 m (deck pedido 1,00 m) - 0 peça(s) de fundação assentadas no terreno, nada enterrado` |
| a casa de referência (estacas enterradas) | `a obra sobe 1,00 m (deck) - 3 peça(s) …` | **igual, byte a byte** |

- O `capped` é `lift < wanted - 0.001f` (`MHR_Build.cs:1868`): quando **capou**, a linha diz **os dois números** —
  primeiro o que a obra precisava, depois o que a config pedia (`:1875-1876`) — e, se nada estava enterrado, diz
  `nada enterrado` em vez de esconder o motivo (`:1878-1881`).
- Quando **não** capou, a linha é a de antes **caractere por caractere** — de propósito: o run congelado da casa
  (`MACCommHomeBasicBP`, 985 peças) é lido por `grep` (comentário em `:1871-1874`).
- A linha só é impressa se `lift > 0,001` **ou** houve assentamento **ou** capou (`:1869`): um plano no chão com a
  config em `0` não vira ruído.

#### (D) O que ficou pendente

- **O run de revalidação não foi rodado:** a escada do corpus com **5 companheiros** e o **A/B da casa** (o mesmo
  `CommHomeBasicBP` do §17.22, para provar que o deck da casa de referência **não** mudou). O que se espera — e **não**
  está medido — é que os 5 planos rasos fiquem no chão, que os **2 `UNSUPPORTED`** do Hobbit desapareçam (eles são o
  jogo recusando apoio numa obra que flutuava) e que Meadow Manor pare de perder as 5 peças.
- **A pendência do vão do deck continua** para planos com estacas enterradas: a casa de referência **ainda** sobe
  1 m (é o que a config pede e o que as estacas precisam) — ver o §17.13 e a pendência aberta no `AGENTS.md`. O que
  esta correção faz é **não criar** o vão em plano que não precisa dele.
- **`DeckLift = 0`** continua "mantém o nível do arquivo"; o efeito disso num plano com estacas enterradas **não** foi
  medido nesta rodada.

### 17.24 O parque da espera (11/11/2026) — a fila que nunca esvaziava e a regra do 1 Hz

> Continuação do §17.22. Lá o defeito era o `WaitRetry` só olhar o plano com a fila **vazia**; aqui se mede **por que a
> fila nunca ficava vazia** — e o run que fecha o A/B que ficou pendente lá. A correção do §17.22 **funcionou** (a `fila`
> caiu de **1040,2 s** para **4,5 s**), mas o caminho óbvio — re-perguntar o plano **por frame** — **custou metade da
> obra** (Run 19). O que fica é o throttle de **1 Hz**.

#### (A) O defeito: `RetryPending` responde "tem entrada", não "tem peça para mim"

- `MHR_BuildGroup.RetryPending` é literalmente `group.Retry.Count > 0` (`Jobs/MHR_BuildGroup.cs:1181-1185`): responde
  **"existe alguma entrada na fila do canteiro"**, não "existe **peça que eu possa pegar agora**". [M]
- Uma entrada de **dependência** é **imortal**: `TryQueueRetry(..., BuildFailure.Kind.Dependency)` faz
  `entry.Dependency++` e `entry.At = Time.time + DependencyDelay(...)` e **retorna sem gastar tentativa e sem remover**
  (`MHR_BuildGroup.cs:1089-1094`). O atraso cresce e **para em 25 s** (`MHR_Build.DependencyDelay`,
  `Jobs/MHR_Build.cs:876-891`: **8** → **12** → **20** → **25 s**). Uma peça esperando o apoio que o **colega** vai
  construir mantém a fila "não vazia" **pelo resto da obra**. [M]
- Com o `:3620` do §17.22 (só chamar `Advance` com a fila vazia), o worker que **entrava na espera** — pelas **duas
  portas**, o portão (`MHR_Build.cs:1177-1194`) e a barreira (`:3662`) — **nunca mais olhava o plano**. [M]
- **A assinatura do jogador ("3 parados, às vezes 5") é exatamente isso:** cada worker entra quando é atingido (o seu
  `entry.At`, 2–25 s) e **não volta** — não é *um* worker esperando, são **N** workers parados em momentos diferentes e
  **sem caminho de saída**. [I]

#### (B) O que foi aplicado: 1 Hz

- `AdvanceAskSeconds = 1f` (`MHR_Build.cs:221`) e `State.AdvanceAskedAt` (`:497`), **zerado no `EnterStep`** (`:1341`)
  para a peça nova abrir uma janela nova.
- O `WaitRetry` re-pergunta o plano **no máximo 1×/s**: `if (ahead && Time.time - state.AdvanceAskedAt >=
  AdvanceAskSeconds) { state.AdvanceAskedAt = Time.time; return Advance(ai, state, dt); }` (`:3722-3726`), e o `dt` é
  **repassado** em todos os chamadores que o têm — é o que faz o `WaitGate` deixar de somar zero (o achado do §17.22(D)).
- O `RetryPending` continua sendo o **teste grosso** ("tem algo na fila para alguém") e o `false` dele fecha a ordem do
  grupo (`:3713-3716`): no **fim do plano** o caminho segue **byte a byte** o de antes. [M]
- **O comentário do teste fica no código** (`:3717-3721`) explicando por que **não** existe um segundo predicado
  ("posso pegar algo agora?"): o `NextRetry`, que roda antes no mesmo frame, já percorreu a mesma fila com o mesmo
  `Ready` e **clamou** o que estava pronto — a resposta seria `false` por construção. [M]

#### (C) Por que 1 Hz e não por frame — o Run 19 é a prova

O Run 19 usou o caminho **por frame** (o `if (ahead) return Advance(...)` do §17.22(D) **sem** throttle) e o resultado é
uma **regressão medida**: **38 peças/min contra 78** — a taxa **caiu à metade** — com `corpo na peça` **130** nas **347
peças** que ele entregou, contra **33** no run inteiro do Run 18 (`MHR_Build.cs:3702-3707`, o comentário que fica lá para
ninguém repetir). A causa também está registrada: o `Advance` faz `ReleaseAllOf` **ao entrar** e os **5** passam a
disputar o **mesmo canto** — a espera foi trocada por contenção, e cinco companheiros acabam **em cima da peça que está
sendo colocada**. [M]

**Regra, com o número que a sustenta: a re-pergunta do plano dentro da espera é 1 Hz por worker — nunca por frame.**

#### (D) O run medido — a mesma receita do Run 18 (`CommHomeBasicBP`, **985 peças**, **5 companheiros**, canteiro vazio)

| medida | antes (**Run 18**) | depois (**Run 22**) |
|---|---|---|
| duração | **844 s (14 min 04 s)** | **727 s (12 min 07 s)** |
| `espera` total (5 workers) | **1292,8 s** | **414,1 s** |
| **`fila`** | **1040,2 s** | **4,5 s** |
| barreira | **244,9 s** / **73** retenções | **402,7 s** / **99** retenções |
| dependência | **222,1 s** | **357,0 s** |
| colocadas / puladas | **985/985 · 0 puladas** | **985/985 · 0 puladas** |
| mortes / `UNSUPPORTED` / watchdog | **0 / 0 / 0** | **0 / 0 / 0** |
| `FALTANDO` | vazio | **vazio (23/23 prefabs)** |
| postes de emergência | **25** | **26** (**11** `sem lugar para o apoio`) |
| dispersão | **1012** adiadas / **32** retomadas | **1287** / **6** |

#### (E) Leitura obrigatória desta tabela

- **[I] A barreira subiu porque absorveu a fila.** 244,9 → 402,7 s (**+64,4 %**) e 73 → 99 retenções: a espera **mudou de
  balde**, não necessariamente de tamanho. "Dependência" idem (222,1 → 357,0 s, +60,7 %). Não se lê isso como piora.
- **[I] O que decide continua sendo a `duração` e as `puladas`.** A `espera` é **soma de 5 workers em paralelo**
  (§17.22(B), item 1): **68 %** de queda na espera (1292,8 → 414,1 s) viraram **13,9 %** de queda na duração
  (844 → 727 s) — exatamente o que aquele item previu (só a espera do **último** worker encurta o run).
- **[I] A conta fecha:** 402,7 (barreira) + 4,5 (fila) + **6,9** (passo + portão, e o portão saiu **0,0 s**) = **414,1 s**.
  O passo por worker **não** foi transcrito aqui.
- **Cortes do §17.22(E), conferidos um por um:** `fila ≤ 400 s` → **4,5 s** ✅; `duração ≤ 720 s` → **727 s**, **7 s
  acima** ⚠️; `WaitAhead ≈ 0` e a espera do **último** worker **não** foram transcritas para esta tabela. **O que fecha a
  pendência são as `puladas` (0), as mortes (0), o `UNSUPPORTED` (0) e o watchdog (0) — os guardas de reversão — e não o
  corte de duração**, que ficou **7 s** fora e fica registrado como ficou. O **`desistindo`** também **não** foi
  transcrito aqui.

#### (F) Dois lotes revertidos no mesmo dia, com o número que os matou

1. **Escopo XZ de 6 m nas duas barreiras** (`BodyScope`/`BodyNear` em `MayTake`/`IsBelow`): run completo de **~33 min**
   (**2,4×** a linha de base), `dependência` **206** retenções e `sem lugar livre` **179** — o telhado subia **noutro
   canto** antes de a coluna fechar. Revertido em **`8ec9de7`**. [M]
2. **Filtro de destino do `step-aside`** (rejeitar candidato cujo corpo ainda cruza a peça que está sendo colocada):
   `sem lugar livre` **179** e a **mesma** duração de ~33 min. Revertido **no mesmo commit** (`8ec9de7`). [M]

Os dois números estão aqui para **ninguém repetir**: o problema do telhado não é "esperar mais" nem "escolher outro
canto" — é a **coluna fechar de baixo para cima**, que é o desenho do §17.22.

#### (G) O que ficou aberto desta rodada

- **O `portão` continua `0,0 s`** mesmo com o `dt` **repassado** (`WaitGate += dt` em `MHR_Build.cs:1185`, só quando
  `Phase == WaitRetry`, `:1181-1186`) — e o `BarrierHolds` voltou a contar (**99**). Com re-pergunta de **1×/s**, esses
  segundos só poderiam ser o `dt` do frame que re-pergunta. **O instrumento ainda não explica por quê.** [M]
- **`step-aside`: 42 linhas `sem lugar` para 10 pedidos.** Os dois números **contam coisas diferentes** e não formam
  taxa: a linha `"…" está na peça X - sem lugar livre para ele sair da frente` é **1 por peça** (`AsideLogged`, zerado
  em `EnterStep`, `MHR_Build.cs:1332`; impressa em `:6569-6573`) e o `AsideAsked` (`:538`, incrementado em `:6595`)
  conta **todo pedido aceito** na linha do `Finish` (`:7721`). Fica registrado como saiu do log, com a ressalva de
  métrica do §17.21. [M]
- **Os 6,9 s de "passo" não foram abertos por worker** — a soma bate, o detalhe não foi lido.
- **Um comentário ficou para trás:** `MHR_Build.cs:1461` ainda diz *"the wait is re-asked every frame"* — texto anterior
  ao throttle; o comportamento de hoje é **1×/s** (§17.25(D)).

### 17.25 O custo por frame da espera (11/11/2026) — 4,2 ms, 10–12 MB/s de GC e uma varredura morta

> Revisão de performance do caminho da espera (§17.22/§17.24) com `arquivo:linha`. Duas das três suspeitas se
> confirmaram, a terceira era **código morto** — e o que continua **não existindo** é o cronômetro no jogo (ver (C)).

#### (A) O que a revisão mediu [M]

- **Três varreduras a 60 Hz por worker:** o `WaitRetry` chama `ServeBarrier` **todo frame** (`MHR_Build.cs:3662`) e
  `NextRetry` **todo frame** (`:3691`); o `RetryReady` era uma **segunda** varredura da **mesma** fila com o **mesmo**
  predicado. O throttle de 1 s do `AdvanceAskSeconds` (`:3722`) estrangula **só o `Advance`**, não estas três.
- **Com a fila de 985 e 99 barreiras: ~4,2 ms/frame nos 5 workers ≈ 25 % do orçamento de 16,6 ms**, **com alocação**.
- **`ClassOf` alocava uma string por chamada** (`(prefab ?? "").ToLowerInvariant()`): a regra rodava **por chamada** e o
  `IsBelow` (`MHR_BuildGroup.cs:1371`, chamado em `:1306` e `:1342`) pergunta por **entrada elegível × 2–3 varreduras** →
  **~3.000 strings/frame ≈ 170–210 KB/frame ≈ 10–12 MB/s de GC**, exatamente na **fase travada**.
- **`RetryReady` era 100 % morto:** o `Ready` inclui `ClaimFree` (`MHR_BuildGroup.cs:1196-1218`) e o `NextRetry` — que
  roda **antes, no mesmo frame** — pergunta a mesma coisa e **clama**; logo `NextRetry == -1 ⟹ RetryReady == false`,
  **sempre**. Era uma varredura inteira da fila por frame por worker para responder `false` por construção.

#### (B) O que foi corrigido (commit `c0826b4`) [M]

- **`RetryReady` removido** — a regra virou **uma só** cópia em `Ready`, que o `NextRetry` mantém viva
  (`MHR_BuildGroup.cs:1187-1195`, o comentário que registra por que a segunda varredura saiu).
- **`ClassOf` memoizado**: `Dictionary<string, int>` com `StringComparer.Ordinal` (`AI/BuildBlueprintParser.cs:302`) e a
  regra em `ClassOfOnce` (`:321`), que roda **no primeiro prefab**; o valor guardado é **exatamente** o que a regra
  devolve (`:306-317`). Efeito: **0 strings/frame** nesse caminho, e o cache é limitado (~100 prefabs no corpus inteiro).
- **Gate do ciclo:** harness **711**, gate **9/9**.

#### (C) O que continua sem medidor [M]

- **`µs/frame` do `Drive`:** **não existe instrumento** — não há `Stopwatch` em `MHR_Build.cs`.
- **`BodyAt` no pico e no fim do run**, e o **frame time com a obra travada**: não medidos.
- **A proposta (não implementada):** `Stopwatch` no `Drive` + contador em `Ready` + `GC.GetTotalMemory` na linha do
  `Finish` — os três números na mesma linha que já carrega a espera (`:7717-7725`).
- **Ressalva de método:** os **4,2 ms / 25 %** são da revisão de código sobre as varreduras, **não** de cronômetro no
  jogo; servem para dizer **onde** o custo está, não para fechar orçamento. [I]

#### (D) Drift de comentário registrado

`MHR_Build.cs:1461` afirma *"the wait is re-asked every frame"* — é texto **anterior** ao throttle de 1 Hz. O código de
hoje re-pergunta **1×/s** (`:3722`); o comentário é o único ponto do caminho que ainda promete o comportamento do Run 19.


