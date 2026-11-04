# Talhador de pedras, peça de pedra e a pedra do Meadows — Valheim 1.0.20

Nota de engenharia reversa para o **lote do talhador de pedras** (`piece_stonecutter`) e para o furo do
`NearestResource` na coleta de pedra. Tudo abaixo é **medido**, com a fonte ao lado: `Arquivo.cs:linha` =
decompilado do jogo; `src/...:linha` = o nosso código.

## Como foi medido

| Fonte | O que é | Como |
|---|---|---|
| decompilado | `assembly_valheim.dll` da **1.0.20** | `ilspycmd` 9.1.0 → `%TEMP%\opencode\vh-decomp\valheim\*.cs` |
| assets | prefabs de verdade (flags, receitas, tabelas de drop) | `valheim_Data\StreamingAssets\SoftRef\Bundles\c4210710` lido com **UnityPy 1.25.4 + TypeTreeGenerator** sobre as DLLs do jogo (o mesmo esquema do `assets_lib.py` da equipe); `Rock_3`/`Rock_3_frac`/`piece_stonecutter`/`stone_floor_2x2` estão nesse bundle (`manifest_extended`) |
| jogo ao vivo | mundo `tests`, 04/11/2026 | DevBridge: `build.inspect`, `build.pieces`, `build.sites`, `world.prefabs`, `world_console printnetobj 40` |

> Os assets foram lidos pelo **dado serializado** (typetree do próprio `assembly_valheim`), não por
> `Resources.FindObjectsOfTypeAll` — é a mesma fonte que o `build.inspect` lê em runtime; as duas batem nos
> campos em que se cruzam (receita, `m_rangeBuild`, `m_supports`, flags).

---

## 1. `piece_stonecutter` (a estação)

### 1.1 Ficha

| campo | valor | medido em |
|---|---|---|
| `Piece.m_name` | `$piece_stonecutter` (token) | asset + `build.inspect.rawName` |
| `Piece.m_category` | **1** = `PieceCategory.Crafting` (`Piece.cs:56`) | asset |
| `Piece.m_enabled` | **true** | asset |
| `Piece.m_resources` | **Madeira 10, Ferro 2, Pedra 4** (`$item_wood`/`$item_iron`/`$item_stone`, `m_recover` 1) | `build.inspect.resources` (nomes) + asset (`m_amount` 10/2/4) |
| `Piece.m_craftingStation` | **`$piece_workbench`** → **exige bancada** a ≤ 20 m | `build.inspect.station.rawName` (`inRangeAtPlayer: true`) |
| `CraftingStation.m_rangeBuild` | **20** | asset + `build.inspect.station.rangeBuild` |
| `CraftingStation.m_discoverRange` | **4** | asset + `build.inspect` |
| `CraftingStation.m_extraRangePerLevel` | **0** (nenhuma extensão muda o alcance) | asset |
| `CraftingStation.m_craftRequireRoof` | **true** (só para a **janela de craft**, ver §1.4) | asset + `build.inspect.craftingStation.requireRoof` |
| `CraftingStation.m_craftRequireFire` | **false** | asset |
| `CraftingStation.m_useDistance` | 2 | asset |
| `CraftingStation.m_canRepair` / `m_upgrader` / `m_hasCraftTab` | true / false / true | asset |
| `WearNTear` | `m_supports` **false**, material Wood, health **200**, `m_noSupportWear` true, `m_noRoofWear` true | asset + `build.inspect.wearNTear` |
| bounds (inspect) | 3,39 × 2,20 × 1,75; `min.y` **−0,087** | `build.inspect.bounds` |

### 1.2 As flags que mudam a pose (e onde o jogo as lê)

| flag | `piece_stonecutter` | `stone_floor_2x2` | efeito no jogo |
|---|---|---|---|
| `m_noClipping` | **true** | false | `Player.cs:4100` → `TestGhostClipping(ghost, 0,2)` (`Player.cs:4176-4193`): `OverlapSphere` de 10 m na máscara de colocação × `Physics.ComputePenetration` dos colliders da peça; **penetração > 0,2 m = `Invalid`** (clipping). **true = recusa sobreposição** (o nome engana) |
| `m_clipGround` | false | **true** | `Player.cs:3993-4015`: com `m_clipGround`/`m_groundPiece`/`m_clipEverything` o fantasma fica **no ponto do raio** (afunda). O piso de pedra tem `bounds.min.y = −0,5` → **meio metro enterrado** por desenho |
| (`nem` `m_groundPiece`) | ambos false | ambos false | `Player.cs:4016-4048`: sem essas flags o fantasma **desliza** até o collider mais próximo tocar o ponto (a peça **apoia em cima** do chão) |
| `m_groundOnly` | false | false | `Player.cs:3908`: exigiria `Heightmap` sob o ponto |
| `m_notOnTiltingSurface` | **true** | false | `Player.cs:3934`: `normal.y < 0,8` → `Invalid` (**declive > ~36,9° recusa**) |
| `m_noInWater` | **true** | false | `Player.cs:3898`: com superfície de água no raio → `Invalid` |
| `m_notOnWood` | false | false | `Player.cs:3930`: recusaria madeira embaixo |
| `m_spaceRequirement` | **0** | **0** | `Player.cs:3838`: **só vale para `StationExtension`** — aqui é irrelevante |
| `m_onlyInBiome` | None (0) | None (0) | `Player.cs:4096` |
| `m_dlc` | `""` | `""` | — |

**Apoio:** `Player.cs:3890-3893` — se o `Piece` que o raio acertou **debaixo** tem `WearNTear.m_supports ==
false`, a colocação vira `Invalid`. Como o talhador tem `m_supports = false`, **nada pode ser construído
em cima dele** (é a mesma regra que já barra a mobília no nosso `BuildProbe.FootprintSupported`/`HoldsPlacedPiece`).

### 1.3 A estação instanciada pelo nosso executor entra no registro do jogo? **SIM** (código + medido)

A única porta de entrada é o `Start` da estação:

| classe → método | assinatura / trecho | observação |
|---|---|---|
| `CraftingStation.Start` | `CraftingStation.cs:80-97` | `m_nview = GetComponent<ZNetView>(); if (!m_nview \|\| m_nview.GetZDO() != null) { m_allStations.Add(this); …; m_updateExtensionTimer = 2f; }` (:83-95). **Sem ZDO válido a estação não entra — nunca** |
| `CraftingStation.OnDestroy` | `CraftingStation.cs:109-112` | `m_allStations.Remove(this)` |
| `m_allStations` | `CraftingStation.cs:72` | `private static List<CraftingStation>` (uma lista só, sem registro por nome) |
| `ZNetView.Awake` | `ZNetView.cs:43-109` | sem `m_initZDO`, cria o ZDO **síncrono** (`ZDOMan.instance.CreateNewZDO`, :91) → na `Start` da estação (frame seguinte ao `Instantiate`) o ZDO **já existe** |
| `CraftingStation.GetExtensions` | `CraftingStation.cs:318-348` | recalcula `m_buildRange = m_rangeBuild + GetExtentionCount(false) * m_extraRangePerLevel` (:325) **só** se `m_updateExtensionTimer >= 2f`; o `Start` deixa o timer em 2f (:95) → a **primeira** consulta já calcula |

**Medido no jogo (04/11/2026):** existem **2** `piece_stonecutter` de pé, em `(-140, 38,8, -428,8)` e
`(-180, 38,8, -428,8)`, com **`creator: 0`** (`build.pieces`) — ou seja, **não** vieram do `Player.PlacePiece`.
`build.sites prefab=stone_floor_2x2` nos **4 pontos a 3 m** deles responde `hasStation: true` (e nenhum
`needs_station`): `CraftingStation.HaveBuildStationInRange("$piece_stonecutter", …)` **as encontra**.
→ Estação criada fora do martelo do jogador entra no registro; o nosso caminho é o mesmo (`Instantiate` +
ZDO conferido logo depois, `BuildExecutor.cs:161-168`).

### 1.4 Como `HaveBuildStationInRange` filtra (o que responde "a estação serve para esta peça?")

`CraftingStation.HaveBuildStationInRange(string name, Vector3 point)` — `CraftingStation.cs:271-286`:

1. **nome exato**: `if (!(allStation.m_name != name))` → compara o **token** (`$piece_workbench`,
   `$piece_stonecutter`); nome localizado **nunca** casa;
2. **alcance**: `allStation.GetStationBuildRange()` (:385-389) = `m_rangeBuild` (+ extensões × `m_extraRangePerLevel`);
3. **Y achatado**: `point.y = allStation.transform.position.y` (:278) → a distância é **2D (XZ)** a partir da
   estação (não importa a altura da peça);
4. **`<` estrito** (:279), não `<=`.

Variantes (mesma lista `m_allStations`): `FindStationsInRange` (`CraftingStation.cs:288-297`, 3D, enche uma
`List`), `FindClosestStationInRange` (`CraftingStation.cs:299-316`, 3D, devolve a mais próxima `< range`).
O `build.usable` do DevBridge usa a segunda (`DevBridge/Commands.cs:582`).

**Armadilha:** se a `Start` não rodou (componente desligado, objeto inativo, ZDO nulo no frame da `Start`),
`m_updateExtensionTimer` fica **0** e `GetStationBuildRange()` devolve **0** → a estação existe mas **nunca
casa**. Não há retry: `m_allStations` só é alimentado na `Start`.

### 1.5 O que a estação **não** faz sozinha

- Colocar peça **não** consulta estação no `Player.PlacePiece` (`Player.cs:3099-3166`): o portão da estação
  está no **fantasma** (`Player.cs:1390` → `HaveRequirements(piece, RequirementMode.CanBuild)` →
  `Player.cs:2849` → `HaveBuildStationInRange(…, transform.position)`, **posição do jogador**) e no nosso
  validador (`BuildProbe.cs:555`, **posição da peça**). Nem o `Player.PlacePiece` nem o nosso
  `BuildExecutor.PlaceDirect` checam estação.
- `CheckUsable` (telhado ≥ 70 % de cobertura, `m_craftRequireRoof`) vale **só** para abrir a janela de craft
  (`CraftingStation.cs:144-171`, chamado em `:127`). **Não** vale para descobrir a estação (§4.3).

---

## 2. `stone_floor_2x2` (a peça)

| campo | valor | medido em |
|---|---|---|
| `Piece.m_name` | **`$piece_stonefloor2x2`** (sem underscore; o prefab é `stone_floor_2x2`) | asset + `build.inspect.rawName` |
| `Piece.m_craftingStation` | **`$piece_stonecutter`** (talhador a ≤ 20 m) | `build.inspect.station.rawName` (`inRangeAtPlayer: false`) |
| `Piece.m_resources` | **Pedra 6** | `build.inspect` + asset (`m_amount` 6) |
| `Piece.m_supports` (`WearNTear`) | **true** — material Stone, health **1500**, `m_noSupportWear` true, `m_noRoofWear` **false** | asset + `build.inspect.wearNTear` |
| `Piece.m_category` | **3** = `PieceCategory.BuildingStonecutter` (`Piece.cs:58`) | asset |
| `Piece.m_enabled` | **true** | asset |
| `Piece.m_noClipping` | **false** (pode sobrepor) | asset + `build.inspect.flags` |
| `Piece.m_clipGround` | **true** (afunda no ponto do raio — meio metro) | asset + `build.inspect.flags` |
| `m_groundOnly` / `m_groundPiece` / `m_notOnWood` / `m_noInWater` / `m_notOnTiltingSurface` | false / false / false / false / false | asset + `build.inspect.flags` |
| `m_spaceRequirement` / `m_onlyInBiome` / `m_dlc` | 0 / None / `""` | asset |
| `recipeKnown` (jogador, medido) | **false** | `build.inspect` (§4) |

**De qual `PieceTable` ela é filha:** da **do martelo** — o mesmo `PieceTable` das peças de madeira. Medido
por `inHammer: true` (`build.inspect`), que é exatamente a pergunta "está na tabela do `Hammer`?"
(`BuildProbe.cs:391-415`: lê `Hammer` → `ItemDrop.m_itemData.m_shared.m_buildPieces` → `m_pieces`).
O jogo faz o mesmo por outro caminho: `ObjectDB.GetAllBuildPieces` (`ObjectDB.cs:240-284`) percorre os itens e
lê `component.m_itemData.m_shared.m_buildPieces` (**`ObjectDB.cs:260`**); `Inventory.GetAllPieceTables`
(`Inventory.cs:408-417`) devolve as tabelas dos itens **que o jogador tem no inventário** (é o que o unlock
usa, `Player.cs:5384`). O martelo precisa estar na mochila.

---

## 3. Pedra no Meadows — quem dropa `$item_stone`

### 3.1 Lista medida (asset, bundle `c4210710`)

Todas as tabelas têm `m_dropChance = 1`, `m_oneOfEach = 0`; o número é `m_dropMin..m_dropMax` rolagens ×
`m_stackMin..m_stackMax` (`DropTable.GetDropList`, `DropTable.cs:103-177`), escalado por `Game.m_resourceRate`.

| prefab | classe | vida | drop | nº de vezes | pedra por unidade |
|---|---|---|---|---|---|
| `Rock_3` | `Destructible` | **1** | **sem tabela**; `m_spawnWhenDestroyed` = **`Rock_3_frac`** | — | 0 direto |
| `Rock_3_frac` | `MineRock5` | 30 | `m_dropItems`: `Stone` `m_dropMin 3`/`m_dropMax 6` | **por área** | **15–30** (5 colliders) |
| `Rock_4` | `Destructible` | 30 | `DropOnDestroyed`: `Stone` 3–6 | 1× | 3–6 |
| `Rock_7` | `Destructible` | 30 | idem | 1× | 3–6 |
| `Rock_7_meadows` | `Destructible` | **200** | idem | 1× | 3–6 |
| `MineRock_Stone` | `MineRock` | 20 | `m_dropItems`: `Stone` 1–2 | **por área** | **24–48** (24 colliders) |
| `Pickable_Stone` | `Pickable` | — | item `Stone` ×1, `m_respawnTimeMinutes` **0** | — | 1 |
| `RockDolmen_1/2/3` | tem `DropOnDestroyed` (a classe de dano não foi lida) | — | `Stone` 1–1 | 1× | 1 |
| fora do Meadows | `MineRock_Copper` (`Stone` peso 1,0 + cobre peso 0,5), `MineRock_Iron` (`Stone` peso 1,0 + ferro peso 0,5), `MineRock_Meteorite` (`Stone` peso 0,5 + `FlametalOreNew` 3–8 peso 1,0), `rock4_forest_frac` 4–8, `rock4_coast_frac` 4–8, `rock4_copper_frac` 2–4 (+ cobre), `rock3_silver_frac` 2–3 (+ prata), `rock1_mountain_frac`/`rock2_*`/`rock3_*_frac` 4–8 | | | | |
| **sem pedra** (apesar do nome) | `MineRock_Tin` (só `TinOre`), `MineRock_Obsidian` (só `Obsidian`) | | | | |

- **Dano:** os `Rock_3`/`Rock_4`/`Rock_7`/`Rock_7_meadows` e o `MineRock_Stone` têm
  `m_damages`/`m_damageModifiers` = `m_pickaxe` **Normal (0)** e **Immune (3)** em blunt/slash/pierce/**chop**/
  fogo/gelo/raio/veneno/espírito. (`HitData.DamageModifier`: `Normal=0, Resistant=1, Weak=2, Immune=3,
  Ignore=4` — `HitData.cs:67-78`; o `3` do asset **é Immune**, não "fraco".) → **só picareta**.
- **Como o drop sai:** `MineRock5` (`MineRock5.cs:404-412`) e `MineRock` (`MineRock.cs:176-190`) dropam
  `GetDropList()` **a cada área destruída**, e as áreas são **os colliders filhos**
  (`MineRock5.cs:93-110`; `MineRock.cs:221-229` guarda vida por `Health{i}` no ZDO). Os 5 colliders do
  `Rock_3_frac` e os 24 do `MineRock_Stone` foram **contados no asset** — é a origem do "5 áreas ≈ 15–30 por
  rocha" que o `docs/re/harvesting-notes.md:36-42` já tinha medido em jogo.
- `Rock_3` tem `m_health = 1` e **nenhuma** tabela: quem entrega a pedra é o `Rock_3_frac` que ele **spawna**
  (`m_spawnWhenDestroyed`, resolvido no asset para `Rock_3_frac`).

### 3.2 Onde eles aparecem (medido no jogo)

`printnetobj 40` no Meadows, jogador em `(-158,0, 38,75, -405,7)`, 04/11/2026:

| prefab | unidades |
|---|---|
| `Rock_4` | **26** |
| `Rock_7` | **8** |
| `Rock_3` | **1** |
| `Pickable_Stone` | **7** |

`ZNetScene.GetPrefabNames` (`world.prefabs`) confirma que existem `Rock_3`, `Rock_3_frac`, `Rock_4`,
`Rock_4_plains`, `Rock_4_deepnorth`, `Rock_7`, `Rock_7_meadows`, `Rock_7_deepnorth`, `Rock_destructible`,
`MineRock_Stone/Copper/Tin/Iron/Obsidian/Meteorite`, `Pickable_Stone`, `RockDolmen_1..3`,
`rock4_bigrock_frac`. **A tabela de vegetação do `ZoneSystem` por bioma não foi lida** (NÃO MEDIDO).

### 3.3 O que o nosso código **já** aceita

| nosso código | o que faz | cobre os rockões? |
|---|---|---|
| `HarvestController.DetermineHarvestModeStatic` `HarvestController.cs:549-591` | classifica o alvo em modo 1 (madeira) / 2 (pedra) / 3 (minério) | **sim**: `MineRock` sem minério → 2 (`:568-572`); `Destructible` com `pickaxe != Immune` **e** `chop == Immune` sem drop de minério → **2** (`:573-585`) |
| `HarvestController.GetHarvestCandidateWithType` `:3379-3457` | collider → objeto, por modo | **sim**: modo 1 = `TreeBase`/`TreeLog`/`stub` (`:3386-3404`); modo 3 = `MineRock5`/minério (`:3406-3436`); **modo 2 (`default`)** = `MineRock` **sem** minério (`:3441-3446`) + `Destructible` com picareta e `chop` Immune/Ignore (`:3447-3452`) |
| `HarvestController.ScanForTarget(2)` `:2998-…` | varre colliders e usa o resolvedor acima (`:3041`) | sim |
| `HarvestController.GetMinToolTier` `:1911-1943` | lê `m_minToolTier` do `Destructible`/`MineRock` | `0` nesses prefabs (medido) → qualquer picareta |
| `MHR_GatherRules.DropsOf`/`AddDrops` `MHR_GatherRules.cs:119-155` | soma drops, inclusive a cadeia `Destructible.m_spawnWhenDestroyed` (`:149-154`) | **sim** — o caso `Rock_3 → Rock_3_frac` está documentado ali (`:130-135`) |

**O furo é só na obra.** `MHR_Build.NearestResource` (`MHR_Build.cs:5794-5847`): para madeira varre
`TreeBase` (`:5801-5824`); **para o resto varre só `MineRock5`** (`:5825-5840`). Consequência medida pela
leitura do código + pela presença medida no bioma:

| fonte de pedra no Meadows | `NearestResource` acha? |
|---|---|
| `Rock_3_frac` (rocha já quebrada, `MineRock5`) | **sim** pela regra — mas **não havia nenhum** nos 40 m medidos (§3.2) |
| `Rock_3` / `Rock_4` / `Rock_7` (Destructible **em pé**) | **não** (eram 26 + 8 + 1 no mesmo raio) |
| `MineRock_Stone` | **não** |
| `Pickable_Stone` (pedra solta) | **não** — e não é alvo de harvest nenhum: cai no **forage** (`MHR_ForageAllowed` `:3459-3483`, `IsForageItemAllowed` `:3474-3483`), e o padrão de `[Gathering] ForageItems` (`ModConfig.cs:995`) **não lista `Stone`** |

---

## 4. Receita por personagem (e o que destrava a peça de pedra)

### 4.1 Onde a receita vive: no **personagem**, não no mundo

| classe → método | `arquivo:linha` | observação |
|---|---|---|
| `Player.m_knownRecipes` | `Player.cs:302` | `private readonly HashSet<string> m_knownRecipes` |
| `Player.m_knownStations` | `Player.cs:304` | `private readonly Dictionary<string,int>` (token → nível) |
| `Player.Save(ZPackage)` | `Player.cs:4688-4708` | grava `m_knownRecipes` (`:4698-4702`) e `m_knownStations` (`:4703-4708`) no **ZPackage do personagem** |
| `Player.Load(ZPackage)` | `Player.cs:4827-4850` | devolve as duas listas (`:4831`, `:4848`); `m_knownStations` só existe a partir de `Version.PlayerData.Stations` (`:4833`) |
| `PlayerProfile.SavePlayerData` | `PlayerProfile.cs:192-197` | `player.Save(zPackage)` → `m_playerData` (`:196`) |
| `PlayerProfile.SavePlayerToDisk` | `PlayerProfile.cs:227-245`, `:373-376` | grava o blob em `<pasta de personagens>\<nome>.fch` (**`:245`**) → `zPackage.Write(m_playerData)` (`:376`) |
| `Game.SavePlayerProfile` | `Game.cs:425-483` | `m_playerProfile.SavePlayerData(Player.m_localPlayer)` (`:452`) e `m_playerProfile.Save()` (`:472`) |

⇒ **É do personagem (`.fch`)**. O mundo não guarda receita: não há `m_knownRecipes` em `ZDO`, `ZoneSystem` nem
`Game` (as duas listas são campos privados do `Player`). Corrobora: neste mundo há **5** bancadas de pé (+1
`piece_workbench_ext1`, medido por `build.pieces`) e o personagem conhece a bancada (`recipeKnown: true`), mas
**não** conhece o talhador nem o piso de pedra (`recipeKnown: false` medido) — a receita não é derivada do mundo.

### 4.2 A cadeia do "E" na estação (confirmada no IL)

| passo | `arquivo:linha` | o que acontece |
|---|---|---|
| `CraftingStation.Interact(Humanoid, bool, bool)` | `CraftingStation.cs:114-135` | exige ser o `Player.m_localPlayer` (`:120`), `InUseDistance` (2 m, `:122`/`:391-394`) e **`CheckUsable`** (`:127`) |
| `CraftingStation.CheckUsable` | `CraftingStation.cs:142-173` | telhado: `Cover.GetCoverForPoint` + `underRoof` + `cover < 0,7` → recusa (`:144-163`); fogo: `m_haveFire` (`:164-171`). O talhador tem `m_craftRequireRoof = true` → **ao ar livre o E não abre nada** |
| `player.SetCraftingStation(this)` | `CraftingStation.cs:129` → `Player.cs:4372-4384` | com `station != null` → **`AddKnownStation(station)`** (`:4378`) |
| `Player.AddKnownStation(CraftingStation)` | `Player.cs:5260-5279` | `m_knownStations[m_name] = station.GetLevel()` (`:5262`/`:5274`) e **`UpdateKnownRecipesList()`** (`:5269`/`:5277`) |
| `Player.UpdateKnownRecipesList()` | `Player.cs:5369-5403` | percorre as `PieceTable`s do inventário (`:5384`) e, por peça com `HaveRequirements(piece, RequirementMode.IsKnown)` (`:5392`), chama `AddKnownPiece` |
| `Player.AddKnownPiece(Piece)` | `Player.cs:5248-5258` | `m_knownRecipes.Add(piece.m_name)` (`:5252`) — **a peça vira receita do personagem** |
| `Player.HaveRequirements(Piece, IsKnown)` | `Player.cs:2838-2852` | exige `m_knownStations.ContainsKey(piece.m_craftingStation.m_name)` (`:2844-2847`) **e** `m_knownMaterial` de cada recurso (`:2872`) |

⇒ **Respondendo à pergunta:** sim — interagir com a estação inscreve a estação e as peças dela como receita
do **personagem** (persistido no `.fch`). Para as peças de pedra isso significa: conhecer o
**`$piece_stonecutter`** **e** ter visto **pedra** (`m_knownMaterial`); a bancada só é exigida para construir o
próprio talhador.

### 4.3 O caminho que **não** precisa do E (e é o que interessa ao mod)

| classe → método | `arquivo:linha` | observação |
|---|---|---|
| `Player.UpdateStations(float)` | `Player.cs:4328-4370` | a cada **1 s** (`m_stationDiscoverTimer > 1f`, `:4331`) chama `CraftingStation.UpdateKnownStationsInRange(this)` (`:4334`) |
| `CraftingStation.UpdateKnownStationsInRange(Player)` | `CraftingStation.cs:205-215` | percorre `m_allStations` e, para toda estação a **`< m_discoverRange` (4 m, 3D)** do jogador (`:210`), chama **`player.AddKnownStation(allStation)`** (`:212`) — **sem** `CheckUsable`, sem telhado, sem fogo |

⇒ **Andar a menos de 4 m do talhador destrava a estação e as peças**, mesmo sem interagir e sem telhado.
Corrobora o estado vivo: o `stone_floor_2x2` está com `recipeKnown: false` e o talhador mais próximo está a
**29,5 m** do ponto consultado (`build.pieces`, 04/11/2026) — muito acima dos 4 m do `m_discoverRange`.

### 4.4 O que o nosso executor **não** faz hoje

`Player.PlacePiece` (`Player.cs:3099-3166`) inscreve a estação no personagem **na hora**:
`GetComponentInChildren<CraftingStation>()` → **`AddKnownStation`** (`Player.cs:3105-3109`).
O nosso `BuildExecutor.PlaceDirect` (`BuildExecutor.cs:137-228`) **não** tem esse passo (a sequência vai até
`SetCreator` `:176`, `WearNTear.OnPlaced` `:188`, `IPlaced.OnPlaced` `:196-209`, efeito `:212`).
`Player.AddKnownStation` é **público** (`Player.cs:5260`) — é a chamada que falta para o companheiro destravar
a peça no personagem **sem** depender de o jogador chegar a 4 m.

### 4.5 O portão da receita é **nosso**, não do jogo

`MHR_Build.cs:1502-1510` descarta a linha do blueprint quando `Player.IsRecipeKnown(resolved.m_name)` é falso
(`recipe not unlocked`). Esse é o único lugar onde a falta de receita barra a obra: o `Player.PlacePiece`
**não** valida estação nem receita (`Player.cs:3099-3166`), e o portão do jogo para o **jogador** é o
`HaveRequirements(piece, CanBuild)` de `Player.cs:1390` — que checa **estação em alcance + materiais**, *não*
`m_knownRecipes` (a receita esconde a peça só na UI, `PieceTable.cs:71`).

---

## 5. O que isso muda no nosso código

**Pedra:** o `HarvestController` já trata os rockões do Meadows (`Rock_3`, `Rock_4`, `Rock_7` e o
`MineRock_Stone` caem no **modo 2** por `HarvestController.cs:549-591` e `:3441-3452`), então o único conserto
da coleta é o **`MHR_Build.NearestResource`** (`MHR_Build.cs:5794-5847`): em vez de só `MineRock5`
(`:5825-5840`), ele precisa varrer os três tipos que o resolvedor do harvest já aceita (`MineRock5`,
`MineRock` não-minério e `Destructible` com `pickaxe != Immune`/`chop == Immune` — a mesma tabela de
`HarvestController.cs:3379-3457`) e, de preferência, começar pelos `Destructible` em pé — no raio medido de
40 m de Meadows havia **26 `Rock_4` + 8 `Rock_7` + 1 `Rock_3`** e **nenhum `Rock_3_frac`** (§3.2), ou seja, o
`MineRock5` que o código procura hoje **não existia** ali — e considerar o `Pickable_Stone` como
coleta barata (hoje ele só é pego pelo forage, e o padrão de `ForageItems` não lista pedra —
`ModConfig.cs:995`). **Talhador:** a estação **entra** no registro do jogo sozinha (só `Instantiate` +
`ZDO` válido na `Start` — medido), e `HaveBuildStationInRange` a encontra a ≤ 20 m com **Y achatado**; o que
falta é (a) o **plano de estações** deixar de ser só-bancada — `IsBenchKind` (`MHR_Build.cs:2692-2695`) casa
apenas `"workbench"` e por isso a peça de pedra hoje **recusa o blueprint inteiro** antes da primeira peça
(`MHR_Build.cs:1603-1618`), quando o certo é inserir `piece_stonecutter` (Madeira 10 + Ferro 2 + Pedra 4,
**exige bancada a ≤ 20 m**, `noClipping`, `notOnTiltingSurface`, `noInWater`, nada em cima dele) — e
(b) destravar a **receita** no personagem: ou chamando `Player.AddKnownStation` logo depois de colocar a
estação (o que o `Player.PlacePiece` faz e o nosso `PlaceDirect` não — `Player.cs:3105-3109` vs
`BuildExecutor.cs:137-228`), ou assumindo que o jogador vai passar a 4 m (`Player.cs:4328-4335` →
`CraftingStation.cs:205-215`); sem isso o portão `MHR_Build.cs:1502` continua descartando as peças de pedra do
arquivo, e sem **ferro** conhecido nem `AddKnownStation` resolve (`HaveRequirements` também pede
`m_knownMaterial`, `Player.cs:2872`).

---

## 6. O que **não** deu para medir (NÃO MEDIDO)

- **A vegetação do `ZoneSystem` por bioma** (a lista "esta pedra nasce no Meadows"): não há ferramenta que a
  exponha no jogo nem no decompilado (é cena/`ZoneSystem`, não prefab). A presença no Meadows foi medida por
  `printnetobj 40` (§3.2), que é o que **de fato** existe em volta do jogador.
- **`Rock_7_meadows` no Meadows deste mundo:** não apareceu nos 40 m medidos (apareceu `Rock_7`); os dois
  prefabs existem e dropam pedra igual (3–6) — qual o bioma usa não foi medido.
- **`m_knownMaterial` do personagem:** com as ferramentas atuais não é legível (`build.inspect` dá
  `recipeKnown`, não os materiais conhecidos), então o `recipeKnown: false` do talhador é compatível com
  "falta ferro conhecido" **e** com "nenhum talhador a 4 m" — as duas são indistinguíveis aqui.
- **A confirmação em jogo de que chegar a 4 m destrava a peça:** exigiria mover/teletransportar o jogador até
  o talhador, o que **grava** a estação no personagem (`.fch`) em uso. Não foi feito; a cadeia está confirmada
  **no código** (§4.3) e o estado vivo é **consistente** com ela.
- **Quantas pedras um run de coleta rende por rockão:** o asset dá a tabela (3–6 por `Rock_4`/`Rock_7`;
  15–30 por `Rock_3` via 5 áreas), mas nenhum run com o `NearestResource` corrigido foi feito.
- **`m_category` do `piece_stonecutter` como aba da UI:** o valor lido é **1** (`Crafting`) e o do piso é
  **3** (`BuildingStonecutter`); qual aba o jogo desenha a partir disso não foi verificado em tela.
- **O nome próprio da `PieceTable` do martelo:** a API não expõe; a medida de pertencimento é `inHammer = true`
  (`BuildProbe.cs:391-415`).
- **`Rock_destructible` / variantes `_deepnorth` / `_plains`:** presentes no bundle; `Rock_destructible` tem
  `m_health = 10` e **nem tabela de drop nem `m_spawnWhenDestroyed`** medidos (quem tem tabela é o
  `Rock_destructible_test`: `Stone` + ferro a 1 %); `Rock_4_plains`/`Rock_4_deepnorth`/`Rock_7_deepnorth` têm
  `Stone` na tabela mas **sem `m_dropMin/Max` lido** — todas fora do Meadows e **não medidas em jogo**.
