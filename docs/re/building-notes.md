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

### 15.3 Medido em jogo (02/11/2026)

| arquivo | pecas | resultado |
|---|---|---|
| `TestBox_V2.blueprint` (fixture do PlanBuild) | 6 | 5/5 `supported`, 1 pulada (`stone_floor_2x2`, receita nao desbloqueada), `hc_ai_blueprint_done` |
| `hs_meadows_hut.blueprint` (BiomeBlueprints, "Meadow Hut") | 28 | **28/28 `supported`** - pisos, vigas, paredes, **porta**, telhado e as tampas de empena - `hc_ai_blueprint_done` (com uma bancada perto) |
| o **chat** com o LLM: "construa a hs_meadows_hut aqui" | 28 | resolve o **arquivo** (nao o template "hut") - `[Build] blueprint Meadow Hut: 28 pieces` no log |

Sem a bancada perto o `wood_beam` recusa com `needs_station` e a ordem encerra dizendo qual estacao falta (medido).

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
