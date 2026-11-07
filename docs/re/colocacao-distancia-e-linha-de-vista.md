# Colocar uma peça a 10 m: o que o **jogo** exige (distância, linha de vista, apoio, efeitos)

Pergunta da revisão 5/10 (04/11/2026), medida em jogo no run de 22:11 (324 s, 985/985): o executor do mod
**não usa o fantasma do jogador** — `AI/BuildExecutor.PlaceDirect` faz `Instantiate(piece.gameObject, pos, rot)`
(`src/Companions/AI/BuildExecutor.cs:144`) com o **nosso** alcance (`[Building] PlaceReach`, padrão 10) e a validação
**nossa** (`AI/BuildSite.Check`). 74 % das peças passaram a ser colocadas **sem o companheiro andar até elas**.

**Veredito em uma linha:** o jogo **não valida distância nem linha de vista** no caminho de colocação; a distância
(`m_maxPlaceDistance = 5`) existe **só para o jogador mirar**, num método que o mod **não** chama. Peça colocada por
`Instantiate` é tão real quanto a do jogador — com ZDO, criador, apoio e queda. Logo o risco de "colocar através da
parede" é **nosso**, e a recomendação é **não** transformar isso em recusa (§5).

Decompilado usado: `%TEMP%\opencode\vh-decomp` (`Version.cs:172` → `CurrentVersion = new GameVersion(1, 0, 21)`).
Os números de linha são os **mesmos** que o resto de `docs/re/` cita (`Player.cs:3793` = `UpdatePlacementGhost`,
`WearNTear.cs:442` = `OnPlaced`, `WearNTear.cs:484` = `SetupColliders`), então as notas são comparáveis entre si.
Ver §6 para o que **não** foi verificado.

## 1. Tabela `classe → método → assinatura → observação`

| classe | método | assinatura | observação |
|---|---|---|---|
| `Player` | `PlacePiece` | `public void PlacePiece(Piece piece, Vector3 pos, Quaternion rot, bool doAttack = true, bool cheated = false)` | `Player.cs:3099-3166`. **Não** lê distância, **não** faz ray, **não** olha `m_placementStatus`. `:3103` `Instantiate(original, pos, rot)` — a pose é **confiada**. É o único lugar do jogo que cria peça do jogador. |
| `Player` | `TryPlacePiece` | `public bool TryPlacePiece(Piece piece)` | `:3041-3097`. O **único** portão: chama `UpdatePlacementGhost(flashGuardStone: true)` (`:3043`), faz `switch (m_placementStatus)` e, no `default`, `PlacePiece(...)` (`:3093`). Sem caso de distância, sem caso de oclusão. |
| `Player` | `UpdatePlacementGhost` | `private void UpdatePlacementGhost(bool flashGuardStone)` | `:3793-4114`. Calcula o **status do fantasma**. `:3809` `PieceRayTest(...)` → o *status* sai `NoRayHits` (`:4057`) se o raio da câmera não achar nada. `:4092-4095` corpos (`BlockedbyPlayer`), `:4100-4103` **clipping** (`TestGhostClipping`, só se `piece.m_noClipping`), `:4113` `SetPlacementGhostValid`. **Nenhum** teste de "eu vejo essa pose". |
| `Player` | `LateUpdate` | `private void LateUpdate()` | `:1656-1667`. `:1664` é quem roda o `UpdatePlacementGhost` a cada frame (e `:1663` publica a posição de referência do `ZNet`) — o fantasma só existe enquanto o jogador está em modo de construção. |
| `Player` | `PieceRayTest` | `private bool PieceRayTest(out Vector3 point, out Vector3 normal, out Piece piece, out Heightmap heightmap, out Collider waterSurface, bool water)` | `:4227-4268`. `Raycast` da **câmera** até 50 m na `m_placeRayMask`; aceita o hit se `Vector3.Distance(m_eye.position, hitInfo.point) < num` (`:4245`), com `num = m_maxPlaceDistance` (`:4236`) `+ piece.m_extraPlacementDistance` (`:4242`), e se `!hitInfo.collider.attachedRigidbody` (`:4245` — superfície com Rigidbody não serve). **É o único lugar do assembly que aplica os 5 m à colocação.** |
| `Player` | `m_maxPlaceDistance` | `public float m_maxPlaceDistance = 5f` | `:171`. Usos, **todos** de jogador: `:2940`/`:2963` (raio do *remover* peça), `:3769` (realce do `WearNTear` sob a mira), `:4236` (`PieceRayTest`). Nenhum fora do `Player`. |
| `Player` | `m_placeRayMask` | `private int m_placeRayMask` | `:394` (campo), `:670` (`"Default", "static_solid", "Default_small", "piece", "piece_nonsolid", "terrain", "vehicle"`), `:3545` (desliga colliders do **fantasma** fora dessa mask), `:4179` (esfera do clipping), `:4229` (raio da mira). Nunca é usado na **peça colocada**. |
| `Player` | `TestGhostClipping` | `private bool TestGhostClipping(GameObject ghost, float maxPenetration)` | `:4176-4193`. `OverlapSphere(ghost.position, 10f, m_placeRayMask)` + `ComputePenetration > maxPenetration` (0,2 m no chamador). **É sobreposição de collider, não visibilidade** — não olha câmera nem olho. Nós replicamos em `BuildSite.Clips` (`BuildSite.cs:44` `MaxPenetration = 0.2f`, `:47` `OverlapRadius = 10f`). |
| `Player` | `CheckPlacementGhostVSPlayers` | `private bool CheckPlacementGhostVSPlayers()` | `:4195-4225`. Fase ampla `Character.GetCharactersInRange(player.position, 30f)` + `ComputePenetration` da cápsula **contra os colliders do fantasma**. Proximidade do **jogador** é só o filtro da lista; a decisão é geométrica. |
| `Piece` | `SetCreator` | `public void SetCreator(long uid, PlatformUserID platformUserID)` | `Piece.cs:429-438`. Só exige `m_nview != null && m_nview.IsOwner() && GetCreator() == 0`. **Sem** distância, **sem** `Player`, **sem** verificação de colocado. |
| `Piece` | `OnPlaced` | `public void OnPlaced()` | `Piece.cs:289-307`. Só faz algo com `m_harvest`: usa `Player.m_localPlayer.GetSkillFactor(Farming)` (`:295`), colhe num raio e **destrói** a peça (`:306`). É o **único** ponto jogador-dependente da sequência — e o nosso laço de `IPlaced` o chama (`BuildExecutor.cs:196-209`). O vanilla também (`Player.cs:3132-3136`). |
| `ZNetView` | `Awake` | `private void Awake()` | `ZNetView.cs:43-109`. Sem `m_initZDO`: `:91` `m_zdo = ZDOMan.instance.CreateNewZDO(transform.position, prefabHash)`. **Zero** validação de pose/distância. `:108` `ZNetScene.instance.AddInstance(...)`. |
| `ZDOMan` | `CreateNewZDO` | `public ZDO CreateNewZDO(Vector3 position, int prefabHash)` | `ZDOMan.cs:740-758`. Aloca `ZDOID(m_sessionID, m_nextUid++)`, dono = sessão local (`:753`), entra no dicionário (`:756`). **Sem** checagem de posição/zona. |
| `ZNetScene` | `AddInstance` | `public void AddInstance(ZDO zdo, ZNetView nview)` | `ZNetScene.cs:72-76`. `zdo.Created = true; m_instances[zdo] = nview;`. Nada mais. |
| `WearNTear` | `OnPlaced` | `public void OnPlaced()` | `WearNTear.cs:442-446`. `m_createTime = -1f; m_clearCachedSupport = true;`. **Sem** distância. Importa porque é o que **dispensa a carência de 30 s** do `ShouldUpdate` (`:516-523`, com `m_createTime = Time.time` no `Awake` `:289`). |
| `WearNTear` | `UpdateSupport` | `private void UpdateSupport()` | `:823-995`. Puro geométrico: `GetCOM()` (`:866`), `Physics.OverlapBoxNonAlloc` por `m_bounds` (`:871`), `m_supports` do vizinho (`:920`), `horizontalLoss`/`verticalLoss` (`:863`, `:931-943`), cache (`m_supportColliders`/`m_supportPositions`, `:825-857`). **Nenhuma** referência a jogador/colocador. |
| `WearNTear` | `HaveSupport` | `private bool HaveSupport()` | `:1011-1014` → `m_support >= GetMinSupport()`. |
| `WearNTear` | `UpdateWear` | `public void UpdateWear(float time)` | `:525-699`. `:533` `ZNetScene.instance.OutsideActiveArea(base.transform.position)` → **fora da área ativa** põe `m_support = max` e retorna (`:535-540`). Dentro: `:568-575` `if (m_noSupportWear) { UpdateSupport(); if (!HaveSupport()) num = 100f; }` e `:685-692` `damage = num/100f*m_health` → `ApplyDamage(damage)` (a peça sem apoio cai no tick seguinte). |
| `WearNTearUpdater` | `UpdateWearNTear` | `private void UpdateWearNTear(float deltaTime, float time)` | `:37-121`. Percorre `WearNTear.GetAllInstances()` (`:39`) em rodízio (`m_updatesPerFrame`, começa em **50**, `:16`) e chama `UpdateWear` (`:73`). **Não** filtra por distância do jogador — a única noção de perto/longe é o `OutsideActiveArea` acima. |
| `ZNetScene` | `OutsideActiveArea` / `PointInsideActiveArea` | `public bool OutsideActiveArea(Vector3 point)` / `private static bool PointInsideActiveArea(Vector2s zone, Vector3 point)` | `:380-402`. Centro = `ZNet.instance.GetReferencePosition()` (`:382`), que é a posição do **jogador local** (`Player.LateUpdate`, `Player.cs:1663`); raio = `1,5 × m_zoneSize` (ou `1`/`2` conforme `NearSimulationDistance`, `:393-402`). **Não** é o colocador. |
| `Character` | `AddNoise` | `public void AddNoise(float range)` | `Character.cs:3808-3819` → `RPC_AddNoise` (`:3821-3826`) sobe `m_noiseRange` do **próprio** personagem. Ruído é **de quem colocou**, na posição dele; nada a ver com a peça. |
| `EffectList` | `Create` | `public GameObject[] Create(Vector3 basePos, Quaternion baseRot, Transform baseParent = null, float scale = 1f, int variant = -1, ZDOID gamepadEffectsExclusiveToPlayer = default(ZDOID))` | `EffectList.cs:37-128`. `Instantiate` do prefab em `basePos` (`:67`), parental/constraint conforme os flags. O `ZDOID` `:68-76` só grava `ownerZDOUserId`/`ownerZDOId` no ZDO do **efeito**, lidos por `ZSFX.IsPlayerCreator` (`ZSFX.cs:342-361`) para atribuir vibração de controle. **Sem** distância. |
| `TerrainModifier` | `SetTriggerOnPlaced` | `public static void SetTriggerOnPlaced(bool trigger)` | `TerrainModifier.cs:144-146`, flag estática lida em `:86`/`:112` para aplicar as `TerrainOp` na hora em vez de com atraso. Sem distância. |

## 2. Pergunta 1 — o jogo valida distância? **Não.**

Cadeia inteira de uma colocação (`Player.cs:3099-3166`), que é a que o nosso `PlaceDirect` replica:

1. `TerrainModifier.SetTriggerOnPlaced(true)` (`:3102`) → `Instantiate(original, pos, rot)` (`:3103`). Nada entre
   eles.
2. `CraftingStation` → `AddKnownStation` (`:3105-3109`).
3. `Piece.SetCreator(GetPlayerID(), ...)` (`:3110-3114`) — só escreve **com posse** e **criador 0** (`Piece.cs:431`).
4. `WearNTear.OnPlaced()` (`:3116`), `ItemDrop.MakePiece` (`:3130`), `IPlaced.OnPlaced` (`:3132-3136`),
   `m_placeEffect.Create(...)` (`:3137`).
5. `AddNoise(50f)` (`:3160`) e as estatísticas de altura (`:3138-3155`).

**Em nenhum passo há distância, ray ou olho.** O `pos` que chega é usado como está.

E o `Instantiate` cria um objeto **de verdade**: `ZNetView.Awake` gera o ZDO (`ZNetView.cs:91`) e o registra
(`:108`), e `ZDOMan.CreateNewZDO` não olha posição (`ZDOMan.cs:750-758`). Não existe "validação do servidor" no
caminho: o único `PlacePiece` do assembly é o do `Player` (`grep` por `PlacePiece` → `Player.cs:3093`/`:3099` e
`Attack.cs:1509`).

**Precedente do próprio vanilla (a prova mais limpa):** o ataque que gera peça (`m_spawnOnTrigger`) faz

```csharp
player2.PlacePiece(component4, vector2 + attackDir * m_attackRange, player2.transform.rotation, doAttack: false);
```
`Attack.cs:1509` — pose **calculada** (`attackDir * m_attackRange`), **sem** fantasma, **sem** ray, **sem** os 5 m.
Se o `PlacePiece` revalidasse, essa chamada também seria recusada — e ela é código de produção.

**Nuance de jogador (não do mod):** o limite de 5 m não é da **peça**, é do **ponto atingido pelo raio**. O fantasma é
posto no `point` (`:4047`/`:4060`) e depois deslocado pelo offset do próprio collider; uma peça comprida pode
terminar com o *pivô* além dos 5 m. E `Piece.m_extraPlacementDistance` (`Piece.cs:205`, `int`) **soma** ao limite
(`Player.cs:4242`) — é o jogo admitindo, por peça, mais que 5 m.

**Conclusão prática:** os 10 m do `[Building] PlaceReach` são **100 % nossos** (`MHR_Build.cs:47-51` lê a config;
`:7161-7180` é a regra do olho). Não há limite escondido em `WearNTear`/`ZNetScene`/`Piece.SetCreator` a descobrir,
e é por isso que 985/985 sobe com 74 % das peças "sem andar": **nada no jogo reprova**.

## 3. Pergunta 2 — linha de vista: **não existe teste; a peça atrás da parede é possível**

- O jogo **não** testa se o colocador vê a pose. O que o jogador tem é **mira**, não visão: `PieceRayTest`
  (`:4227-4268`) converte o raio da câmera num **ponto** e o fantasma vai para lá. Consequência indireta: com uma
  parede entre a câmera e o alvo, o raio **para na parede** e o fantasma encosta **nela** — o jogador não "coloca
  através" porque a pose é *derivada* do raio, não porque exista um veredito de oclusão.
- As outras duas perguntas de geometria do fantasma **não** são oclusão: `TestGhostClipping` (`:4176-4193`) é
  `ComputePenetration` contra o entorno (0,2 m de tolerância) e só roda se `piece.m_noClipping` (`:4100`);
  `CheckPlacementGhostVSPlayers` (`:4195-4225`) é penetração de cápsula de corpo contra o fantasma.
- **Não há** `PlacementStatus` de oclusão. A enumeração de motivos é a do `switch` de `TryPlacePiece`
  (`:3044-3096`): `NoBuildZone`, `BlockedbyPlayer`, `PrivateZone`, `MoreSpace`, `NoTeleportArea`, `Invalid`,
  `NoRayHits`, `ExtensionMissingStation`, `WrongBiome`, `NeedCultivated`, `NeedDirt`, `NotInDungeon`, `DeepSnow`,
  `NoSnow`. **Nenhum** é "não vejo".
- O nosso validador também não testa: `BuildProbe.CheckPoint` (`BuildProbe.cs:456-619`) devolve
  `no_piece/no_ground/in_water/no_water/need_cultivated/need_dirt/not_on_wood/too_steep/not_ceiling/
  not_on_floor/no_teleport_area/not_in_dungeon/deep_snow/no_snow/wrong_biome/no_build_zone/ward/needs_station/
  too_close_piece/blocked_by_player/no_support` e o `BuildSite.Check` acrescenta `clipping` (`BuildSite.cs:86-105`).
  **Nenhum** olha de onde o companheiro está para a peça. Os `Raycast` do nosso lado do build são todos **para
  baixo** (chão): `BuildProbe.cs:474`, `MHR_Build.cs:1031`, `:5535`, `:6241`, `:7277`, `:7957`.

**Conclusão prática:** colocar uma peça a 6 m **atrás de uma parede** (dentro do nosso raio) é fisicamente aceito —
o mod **pode** e **faz** isso hoje, sempre que a pose do plano cai atrás de algo e o `BuildProbe` não reprova por
outro motivo. Não é um bug de API; é uma escolha de validação que **nós** tomamos ao não testar.

## 4. Perguntas 3 e 4 — apoio e efeitos não dependem do colocador

**Apoio (Q3): depende da peça, não do corpo.** `UpdateSupport` (`WearNTear.cs:823-995`) trabalha com o
`GetCOM()` da peça, `OverlapBox` sobre os colliders dela e o `m_supports` do vizinho — zero referência a jogador.
`HaveSupport` é `m_support >= GetMinSupport()` (`:1011-1014`). O `WearNTearUpdater` (`:37-121`) nem sabe onde o
colocador está: ele varre `WearNTear.GetAllInstances()` em rodízio.

**A única noção de distância no apoio é a área ativa, e ela é do jogador:** `UpdateWear` `:533`
`ZNetScene.instance.OutsideActiveArea(piece.transform.position)` → **fora** da área ativa o apoio é **resetado
para o máximo** e a peça **não** sofre desgaste (`:535-540`). O centro da área é
`ZNet.instance.GetReferencePosition()` (`ZNetScene.cs:382`), publicado pelo **jogador local** todo frame
(`Player.cs:1663`), com raio de ~1,5 × `m_zoneSize` (`:393-402`). Ou seja: uma peça construída **longe de qualquer
jogador** fica com `m_support = max` e **nunca cai por falta de apoio** até alguém chegar perto. No canteiro (o
jogador ao lado) o veredito é imediato — é o que já medimos.

**Efeitos/navmesh/ruído (Q4): nenhum exige proximidade do colocador.**
- `m_placeEffect.Create(pos, rot, transform, 1f, -1, zdoUid)` (`BuildExecutor.cs:212`; vanilla `Player.cs:3137`):
  `EffectList.Create` só instancia (`EffectList.cs:67`). O 6º parâmetro é `ZDOID gamepadEffectsExclusiveToPlayer`
  (`EffectList.cs:37`) e serve **só** para o ZDO do efeito dizer de quem é a vibração de controle (`:68-76` →
  `ZSFX.IsPlayerCreator`, `ZSFX.cs:349-355`). **Diferença nossa:** o vanilla passa `GetZDOID()` (o **jogador**,
  `Player.cs:3137`) e nós passamos `nv.GetZDO().m_uid` (o **uid da peça**, `BuildExecutor.cs:212`) — o som de
  colocação não é atribuído ao jogador, logo não vibra o controle. Cosmético, não é erro de pose.
- `InvalidateAround(pos)` é **nosso** (`Patches/MHR_CompanionNavMesh.cs:222-272`): marca os tiles do nosso agente 40
  ao redor do **XZ da peça**, coalescido em 0,35 s, e só tiles que já existem (`:407-410`). Não consulta jogador
  nem placer. (O `Pathfinding` do jogo tem o `PokeArea` dele; ver `docs/re/navmesh-tiles.md`.)
- `character.AddNoise(50f)` (`BuildExecutor.cs:219`) é o ruído **do companheiro**, na posição dele
  (`Character.cs:3808-3826`); criatura que ouve, ouve o companheiro — nada a ver com a peça.
- `TerrainModifier.SetTriggerOnPlaced` em volta do `Instantiate` (`BuildExecutor.cs:141`/`:153`, igual ao vanilla
  `:3102-3104`) é uma flag estática (`TerrainModifier.cs:144-146`), sem distância.
- **Um ponto de atenção de verdade (não de distância):** o nosso laço de `IPlaced` (`BuildExecutor.cs:196-209`)
  chama `Piece.OnPlaced` (`Piece.cs:289`); para uma peça com `m_harvest = true` isso usa `Player.m_localPlayer`
  (`:295`) e **destrói** a peça (`:306`). Nenhum dos 5 prefabs do catálogo (`AI/BuildCatalog.cs:40-72`) é assim, e
  o `.blueprint` só constrói peça com receita conhecida — mas um `.blueprint` da comunidade com peça de colheita
  cairia aí. Não é regressão nossa: o vanilla faz igual (`Player.cs:3132-3136`).

## 5. Pergunta 5 — veredito: **não** transformar oclusão em recusa

**O risco é nosso, sim.** A `BuildSite.Check` não tem oclusão (§3) e a `PlaceReach` é só distância. Então o mod
aceita e coloca peça que o companheiro não vê.

**Por que não vale a pena como portão:**

1. **O custo não é o raycast, é o falso negativo.** O upstream mediu isso no próprio código:
   `UpkeepController.cs:766-769` — *"a pole set under the floor only had its nearest point checked, the floor
   touching it blocked the line, and the companion stood next to it doing nothing"*. A correção deles foi testar
   **vários pontos** da peça e ignorar o que está **colado nela** (`Clear`, `:1579-1620`: `RaycastAll` na máscara
   `Default/static_solid/Default_small/piece/terrain/vehicle`, ignorando a peça-alvo, personagens, hits a menos de
   0,5 m do alvo e colliders com maior dimensão < 1,2 m). Numa casa, **sempre** há algo: o piso de cima, a parede
   em construção, o vizinho da fileira. O teste que parece barato é justamente o que gera recusa em massa.
2. **A oclusão depende de onde o companheiro está — e é isso que o run de 22:11 tirou do caminho.** A decisão
   passaria a variar com a posição instantânea do worker: a mesma peça seria aceita de um canto e recusada de
   outro, no meio de um plano travado (`PlanLocked`) com claims e a fila de retry. O `blocked_by_player` já é a
   fonte de instabilidade transitória do canteiro (Runs 9/10/11); somar um segundo motivo dependente de pose
   reintroduz o "andar até a peça" que os 74 % eliminaram.
3. **O que o jogador vê não muda no resultado.** A peça fica de pé, com apoio, e a queda é a mesma de qualquer
   peça. O que muda é o **áudio/animação** do companheiro martelando atrás de uma parede — e o mod já aceita
   animação desacoplada (a rota `Direct` nem toca na animação; o `doAttack: false` existe só na rota do jogador,
   `BuildExecutor.cs:118`).
4. **Já cobrimos o que a oclusão realmente protege.** O caso perigoso não é "a peça atrás da parede", é "a peça
   **dentro** de algo/pessoa": isso é `clipping` (vanilla, `Player.cs:4100`, replicado em `BuildSite.Clips`) e
   `blocked_by_player` (vanilla `:4092`, o nosso `BuildProbe.BlockedByCharacter`, `BuildProbe.cs:744-804`). São os
   dois que evitam peça que o jogo derrubaria ou que faz o companheiro afundar.

**Se o incômodo visual aparecer um dia, o caminho barato é (nesta ordem):**

- **(a) só diagnóstico** — um contador `no_sight` por peça no `Finish` e no `build.pieces`, reusando o
  `Clear`-style de uma linha (não entra em `ok`/`status`, não gasta tentativa, não muda a ordem). Custo: ~30 linhas
  num helper puro + asserção no harness; risco de comportamento: **zero**.
- **(b) preferência, nunca recusa** — quando houver 2+ poses válidas para a mesma peça, ordenar pela que tem linha
  livre (desempate). Não muda quem é aceito, só onde. Custo maior (mexe no `BuildSite.Find`, que hoje cresce por
  anéis) e **só** faz sentido se (a) mostrar que o caso é frequente.
- **(c) portão de verdade** — só com medida na mão que justifique, e sabendo que o número que paga é **puladas**
  (§17.16/§17.17 de `building-notes.md`): um `no_sight` recusando em canteiro fechado tende a virar watchdog.

**Recomendação:** comportamento **aceitável** para um NPC construtor; **não** implementar o portão. Se entrar algo,
entra como instrumento (a).

## 6. O que NÃO deu para verificar

- **Não rodei o harness de reflexão nem o jogo.** Tudo aqui é leitura de decompilado + do nosso código. O
  `shell` está negado para este agente nesta rodada (não consegui chamar `ilspycmd`/`dotnet`), então não há
  "chamei o método e imprimi" — e a confirmação em jogo segue sendo a do arquiteto (run de 22:11, 985/985).
- **Versão:** a árvore decompilada disponível é **1.0.21** (`Version.cs:172`), não 1.0.20 — não existe outra árvore
  nesta máquina (`glob` por `*/valheim/Player.cs` → só `vh-decomp`). As funções citadas batem linha a linha com o
  que `docs/re/` já registra para os mesmos métodos, mas **não** fiz diff 1.0.20 × 1.0.21. O único ponto em que
  isso poderia morder é `WearNTear.UpdateSupport` (o `docs/DEV_NOTES.md:1255` do **upstream** registra "support
  check rewritten (caches the supporting pieces)" na 1.0.21, e a árvore aqui **tem** o cache, `:825-857`) — a
  conclusão de que o apoio é geométrico não muda em nenhuma das duas versões, mas o texto acima descreve a **1.0.21**.
- **`Piece.m_extraPlacementDistance` por prefab:** o campo existe (`Piece.cs:205`) e é somado em `:4242`, mas o
  **valor** de cada prefab vive no asset, não no assembly — não medi quais peças têm `> 0`.
- **`Piece.m_harvest` por prefab:** mesmo caso; não sei quais prefabs ligam, então o risco do §4 fica declarado,
  não medido.
- **Multiplayer:** se o `Instantiate` de um peer não-dono da zona é aceito pelo host é outra pergunta (a posse é
  reclamada em `BuildExecutor.cs:169-172`); é a pendência (5)/(16) do `AGENTS.md` e está fora do alvo do produto.

## 7. Onde isso encosta nas notas existentes

| nota | o que ela já cobre | o que esta acrescenta |
|---|---|---|
| `docs/re/support-rule-wearntear.md` | a regra do apoio (§4-5), o `m_supports` medido na 1.0.20 e a colocação não testar apoio | a **distância** e a **área ativa** (§3/§4 daqui) |
| `docs/re/colocacao-corpo-e-altura.md` | `UpdatePlacementGhost`/`TryPlacePiece` e a janela do corpo | a pergunta explícita "o jogo valida distância?" com a lista **completa** dos usos de `m_maxPlaceDistance` |
| `docs/re/navmesh-tiles.md` | os ganchos `OnPlaced`/`Destroy` e a invalidação do tile | que o `InvalidateAround` é do XZ da **peça** e não do colocador |
| `docs/re/stone-and-stonecutter.md` | a estação não é gated no `PlacePiece` | — |
