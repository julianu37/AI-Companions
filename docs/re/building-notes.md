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
pasta **`blueprints/`** - é só copiar os `.blueprint` que quiser para `BepInEx\config\AI-Companions\blueprints\`.
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

**Onde os arquivos ficam:** `BepInEx/config/AI-Companions/blueprints/` (o mod cria a pasta). A ordem usa o **nome do
arquivo** sem a extensao ("construa a hs_meadows_hut aqui"), porque o `#Name:` de muitas builds e "Custom Name".

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
