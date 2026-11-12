# Apoio de peça no Valheim 1.0 — `WearNTear` / colocação (medido em 04/11/2026)

Pergunta que originou esta nota: **um `wood_floor` cuja base fica ~0,6 m acima do topo de outro
`wood_floor` já apoiado recebe apoio do jogo?** Resposta curta: **não** — e o jogo **aceita** a peça
na colocação mesmo assim (o apoio não é testado ali); ela cai no tick seguinte.

Fontes: decompilado de `valheim_Data\Managed\assembly_valheim.dll` em
`%TEMP%\opencode\vh-decomp\valheim\` (linhas citadas são desse arquivo) + leitura do CIL com
`inspect_managed_members` (REA) + `build.inspect` / `build_snaps` do DevBridge **no jogo 1.0.20**
(mundo `tests`, build 1.9.6).

## 0. Resumo em 6 linhas

1. O teste de apoio é **geométrico** e por **collider**: `Physics.OverlapBoxNonAlloc` com a caixa de
   cada collider da peça crescida **0,3 m no total — 0,15 m por lado — em X, Y e Z**.
2. Fonte de apoio aceita: **terreno**, **qualquer sólido sem `WearNTear`** (pedra, árvore, estrutura)
   → apoio **máximo**; ou **outra peça com `WearNTear.m_supports == true`** → apoio menos a perda pela
   distância entre centros de massa.
3. `m_supports` **é `true` em todas as peças estruturais de madeira, inclusive telhados** (medido nos
   prefabs, ver §5). Só mobília (baú, cama, bancada) tem `false`.
4. A **colocação não testa apoio**: `Player.UpdatePlacementGhost` só lê o `m_supports` da **peça sob a
   mira**. Nada no caminho chama `UpdateSupport`/`HaveSupport`.
5. Peça sem apoio é **colocada** e, no tick seguinte (~1 s), `UpdateSupport` + `HaveSupport()` falso →
   dano de **100 % da vida** → a peça **cai e dropa** os materiais.
6. Consequência para o validador: **não** existe "vão generoso". Acima de **0,15 m** de folga entre
   superfícies de collider não há conexão nenhuma. O caso de 0,6 m é **~4× o limite**.

## 1. Tabela `classe → método → assinatura → observação`

| classe | método | assinatura | observação (decompilado) |
|---|---|---|---|
| `WearNTear` | `SetupColliders` | `private void SetupColliders()` | `WearNTear.cs:484-514`. Monta `m_bounds` (lista de `BoundData{pos,rot,size}`) a partir de `GetComponentsInChildren<Collider>(true)`, **ignorando `isTrigger` e collider com `attachedRigidbody`** (`:491`). `BoxCollider` → `rot = transform.rotation`, `pos = transform.position + TransformVector(center)`, `size = lossyScale * size` (`:495-499`); qualquer outro collider (MeshCollider etc.) → `rot = identity`, `pos/size = bounds` **AABB do mundo** (`:503-505`). Depois `size += 0,3` em x/y/z e `size *= 0,5` (`:507-510`) → **meia-extensão = (tamanho + 0,3)/2**, ou seja **+0,15 m por lado**. |
| `WearNTear` | `UpdateSupport` | `private void UpdateSupport()` | `WearNTear.cs:823-995`. Para **cada** bound: `Physics.OverlapBoxNonAlloc(bound.m_pos, bound.m_size, s_tempColliders, bound.m_rot, s_rayMask)` (`:871`). `s_rayMask = GetMask("piece","Default","static_solid","Default_small","terrain")` (`:285`). |
| `WearNTear` | (fonte) | — | `:903-907` collider na layer `terrain` → `flag = true`; `:908-919` collider **sem `WearNTear` no parent** → `m_support = GetMaxSupport()` e **return** (pedra/árvore/estrutura sólida = apoio máximo, **sem** o teste de altura de `:933`); `:920-923` `if (!componentInParent2.m_supports) continue;`. |
| `WearNTear` | (valor) | — | `:924-929` `num6 = Distance(COM, supporter.COM) + 0,1` (e o menor entre essa e `Distance(COM, transform.position) + 0,1`, salvo `m_forceCorrectCOMCalculation`); `:930-931` piso do perdedor horizontal `support − horizontalLoss*num6*support`; `:932-950` `FindSupportPoint` e, **só se o ponto estiver abaixo de `COM.y + 0,05`**, a perda angular entre `horizontalLoss` e `verticalLoss`; `:963-984` dois pontos a **≥ 100°** → média dos dois; `:985-990` `m_support = min(num3, maxSupport)`, gravado no ZDO (`s_support`). |
| `WearNTear` | `FindSupportPoint` | `private static Vector3 FindSupportPoint(Vector3 com, WearNTear wnt, Collider otherCollider)` | `:997-1009`. `MeshCollider` **não-convexo** → `Raycast` para baixo de até **10 m** a partir do COM (o primeiro ponto do telhado/piso abaixo); senão `otherCollider.ClosestPoint(com)`; se o raio falha → ponto médio dos dois COMs. |
| `WearNTear` | `GetMaterialProperties` | `private void GetMaterialProperties(out float maxSupport, out float minSupport, out float horizontalLoss, out float verticalLoss)` | `:1361-1426`. **Madeira: 100 / 10 / 0,2 / 0,125**. Pedra 1000/100/1/0,125; ferro 1500/20/1⁄13/1⁄13; madeira dura 140/10/1⁄6/0,1; mármore 1500/100/0,5/0,125; ashstone 2000/100/1⁄3/0,1; ancient 5000/100/0,25/1⁄15; gelo 1000/100/1⁄3/0,125; timberwood 200/10/0,2/1⁄13. |
| `WearNTear` | `HaveSupport` | `private bool HaveSupport()` | `:1011-1014` = `m_support >= GetMinSupport()` (**10** para madeira). |
| `WearNTear` | `UpdateWear` | `public void UpdateWear(float time)` | `:525-699`. Só no **dono** do ZDO (`:531`). `:568-575`: se `m_noSupportWear` → `UpdateSupport()`; `if (!HaveSupport()) num = 100f`. `:685-692`: `damage = num/100 * m_health` (= **vida cheia**) e `ApplyDamage` (salvo `GlobalKeys.NoBuildingFall`). `:681-684` só há dano se `m_piece.CanBeRemoved()` (baú/ship fechado não cai). |
| `WearNTear` | `OnPlaced` | `public void OnPlaced()` | `:442-446` `m_createTime = -1` e `m_clearCachedSupport = true` → `ShouldUpdate` (`:516-523`) passa a devolver `true` **na hora** (sem os 30 s de carência que uma peça carregada do ZDO tem). |
| `WearNTear` | `Awake` | `private void Awake()` | `:290` `m_support = GetMaxSupport()` — entre o `Awake` e o 1º `UpdateSupport` a peça **conta como apoiada**. |
| `WearNTearUpdater` | `Update`/`UpdateWearNTear` | `private void Update()` / `private void UpdateWearNTear(float, float)` | `WearNTearUpdater.cs:16,20,27-121`: orçamento de **1 s** por volta, `m_updatesPerFrame` 50 (adaptativo **5..100**). Com ~1000 peças cada peça é reavaliada ~1×/s. |
| `Player` | `UpdatePlacementGhost` | `private void UpdatePlacementGhost(bool flashGuardStone)` | `Player.cs:3793-4114`. **Não testa apoio.** `:3824` `wearNTear = piece?.GetComponent<WearNTear>()` — `piece` é a peça **atingida pela mira** (`PieceRayTest`, `:4227-4268`); `:3890-3893` `if (wearNTear && !wearNTear.m_supports) status = Invalid`. O único "apoio" do ghost é esse: **não dá para colocar peça em cima de mobília**. |
| `Player` | `TryPlacePiece` | `public bool TryPlacePiece(Piece piece)` | `Player.cs:3041-3097`. `switch (m_placementStatus)` → mensagem; **nenhum caso de apoio**. Só `PlacePiece` (`:3099`), que instancia e chama `WearNTear.OnPlaced()` (`:3116`). |
| `Player` | `PieceRayTest` | `private bool PieceRayTest(out Vector3, out Vector3, out Piece, out Heightmap, out Collider, bool)` | `:4227-4268`. `Raycast` da câmera até **50 m**, aceito se a distância ao olho for `< m_maxPlaceDistance` (campo `= 5f`, `:171`) `+ m_extraPlacementDistance`; raio passa nas layers `Player.cs:670`. |
| `Player` | `FindClosestSnapPoints` / `FindClosestSnappoint` | `private bool FindClosestSnapPoints(Transform ghost, float maxSnapDistance, out Transform a, out Transform b, List<Piece> pieces)` | `:4128-4174`. **Snap = alinhamento do fantasma**: candidatos são os `snappoint` (children com tag `snappoint`, `Piece.cs:618-628`) de peças num raio de **10 m** (`:4134`), e o par só vale se a distância for **≤ `maxSnapDistance`**, chamado com **0,5 m** (`:4071`). Não cria vínculo de apoio. |
| `Piece` | `CanBeRemoved` | `public bool CanBeRemoved()` | `Piece.cs:324-337` = `true` para piso/parede/telhado (só baú/ship em uso travam). |

### Confirmação no CIL (não é "de memória")

`inspect_managed_members` em `assembly_valheim.dll`:

- `WearNTear::SetupColliders` (token `0x060021b0`, 436 bytes de IL): **três** `ldc.r4 0.3` (IL offsets
  333/354/375) seguidos de `ldc.r4 0.5` em 395 → `(size + 0,3) * 0,5`, exatamente a meia-extensão com
  **0,15 m por lado**.
- `WearNTear::UpdateSupport` (`0x060021bd`, 1459 bytes): constantes `0.1` (756, 783 — as duas
  distâncias `+0,1`), `0.05` (864 — o gate `vector.y < COM.y + 0,05`), `1.5707964` (932 — π/2 do
  `Acos`), `0.5` (1281 — a média dos dois apoios), `100` (1337 — o `Vector3.Angle(...) >= 100f`).
- `WearNTear::HaveSupport` (`0x060021bf`, 18 bytes): `ldfld m_support` + `call GetMinSupport` +
  comparação — nada mais.
- `WearNTear::GetMaterialProperties` (`0x060021d6`, 354 bytes): `100/10/0.125/0.2` no 1º caso
  (madeira), na ordem `maxSupport, minSupport, verticalLoss, horizontalLoss`.
- `m_supports` = campo `0x0400234d`, lido em `UpdateSupport` no cache (`:838`) **e** na varredura
  principal (`:920`).

## 2. O que a colocação checa (e por que o `no_support` do jogo não existe)

`UpdatePlacementGhost` percorre: `StationExtension`, `m_blockRadius`/`m_blockingPieces`,
`m_mustConnectTo`, **`m_supports` da peça sob a mira**, água, `m_groundPiece`/`m_groundOnly`/
`m_cultivatedGroundOnly`/`m_vegetationGroundOnly`, `m_notOnWood`/`m_notOnTiltingSurface`/
`m_inCeilingOnly`/`m_notOnFloor`/`m_onlyInTeleportArea`, dungeon/neve, snap, `Location.IsInsideNoBuildLocation`,
`PrivateArea.CheckAccess`, `CheckPlacementGhostVSPlayers`, bioma e `m_noClipping` (`TestGhostClipping`).
**Em nenhum ponto há `UpdateSupport`, `HaveSupport` ou distância até um apoio.**

Ou seja: no ato da colocação **não existe veredito de apoio**. O primeiro veredito real é:
(a) o `WearNTearUpdater` no tick seguinte (a peça **cai** — 100 % de dano, dropa os materiais), ou
(b) `BuildSupport.Supported(wear)` (`src/Companions/AI/BuildSupport.cs:57-80`, reflexão em
`UpdateSupport()` + `HaveSupport()`), que é o que já aparece no log como `done: … supported`.

O que **a colocação** checa contra corpos (a cápsula do `Character`, `ComputePenetration` sem limiar) e a altura real
do corpo estão em **`docs/re/colocacao-corpo-e-altura.md`** — junto com o fato de o `ZoneSystem.GetGroundHeight` ser
só `terrain` (devolve o terreno **abaixo** de um piso; o topo de peça é `GetSolidHeight`).

## 3. Propaga por conexão de borda? A que distância?

- **Sim, propaga — mas por sobreposição de collider, não por "família"**. `UpdateSupport` não tem
  nenhuma noção de piso/telhado/parede: qualquer collider de **outra** peça com `m_supports == true`
  (ou o terreno, ou um sólido sem `WearNTear`) que entre na caixa expandida conta. O que separa
  "piso sobre piso" de "parede sobre baú" é só o flag `m_supports` — e ele é **`true` em todas as
  peças estruturais de madeira, telhados inclusive** (§5), `false` só na mobília.
- **A distância do vínculo é a da caixa: 0,15 m.** Não há raio, não há `m_supportRadius` (esse campo
  não existe mais na 1.0), não há distância máxima própria: o alcance é *o quanto a caixa do collider
  foi crescida*.
- **O "snap" não é o vínculo.** Snap é alinhamento do fantasma: candidatos a 10 m, par aceito até
  **0,5 m** (`Player.cs:4071` + `:4128-4174`). Os `snappoint`s do `wood_floor` são os **quatro cantos
  no plano da própria peça** (`(±1, 0, ±1)`, medido com `build.snaps`) → o snap de piso serve para
  **ladrilhar no mesmo plano**, não para empilhar. Um `wood_roof_45` tem snaps `(±1, +1, ∓1)` e
  `(±1, −1, ±1)` (plano da água a 45°).
- Depois de conectado, o **valor** cai com a distância entre **centros de massa** (+0,1): madeira perde
  **12,5 %/m na vertical** e **20 %/m na horizontal**; abaixo de **10** a peça cai. Com 0,15 m de folga o
  valor fica **~95–96** (não é a distância que derruba — é a geometria que decide se existe vínculo).

## 4. O caso concreto: 40,4 sobre 39,8

- Folga entre superfícies: **0,5–0,6 m**. Limite do jogo: **0,15 m**. → **não conecta**. O `no_support`
  do nosso validador é o **veredito correto**; se a peça fosse colocada, ela cairia no tick seguinte.
- Vão máximo que ainda conecta, em termos de **pivô entre dois `wood_floor`**: `+0,123` (topo do de
  baixo) `+ 0,140` (fundo do de cima) `+ 0,15` (folga) = **≈ 0,41 m** — usando os bounds que o
  `build.inspect` reporta para o prefab (`min.y = −0,140`, `max.y = +0,123`), que são os do **renderer**.
  Se o collider do piso for só a tábua de ~0,1 m (e não o mesh visual), o número cai para **≈ 0,25 m**.
  Nos dois casos 0,6 m está **fora por 0,19–0,35 m**.
- Piso a 40,4 com o de baixo a 39,8 **ganha poste desde 11/11/2026** (`ae9ebc3`): o menor candidato do
  `BuildSupportBelow.PickPost` é `wood_beam_1` (1 m) e a janela aceita é `gap − ShortTolerance(0,2) ≤
  1,0 ≤ gap + MaxOverlap(0,45)`, isto é **vão de 0,55 a 1,20 m** (`AI/BuildSupportBelow.cs:104-119`). Antes, com
  `MaxOverlap` **0,35**, a janela era **0,65 a 1,20 m** e um vão de 0,6 m ficava 5 cm abaixo do mínimo → recusa. O
  poste não "conecta por comprimento": a ligação é a **sobreposição de collider** do §4 (o beam atravessa 0,40 m da
  peça). Ver `docs/re/building-notes.md` §17.14.

## 5. Medições ao vivo (DevBridge 1.9.6, Valheim 1.0.20, mundo `tests`)

`build.inspect` → `WearNTear.m_supports` (o mesmo campo que `UpdateSupport` lê):

| prefab | `m_supports` | material | bounds y (min..max, rel. ao pivô) |
|---|---|---|---|
| `wood_floor` | **true** | Wood | −0,140 .. +0,123 (x 2,266 × z 2,288) |
| `wood_roof_45` | **true** | Wood | −1,637 .. +1,648 |
| `wood_roof` | **true** | Wood | −0,437 .. +1,552 |
| `wood_roof_top` | **true** | Wood | −0,523 .. +0,859 |
| `wood_roof_top_45` | **true** | Wood | −0,343 .. +1,296 |
| `wood_roof_icorner_45` | **true** | Wood | −0,367 .. +2,472 |
| `wood_wall_half` | **true** | Wood | −0,527 .. +0,529 |
| `wood_stair` | **true** | Wood | −0,132 .. +1,077 |
| `wood_stepladder` | **true** | Wood | −0,097 .. +2,097 |
| `wood_pole2` | **true** | Wood | −1,000 .. +1,000 |
| `wood_beam_1` | **true** | Wood | −0,200 .. +0,200 |
| `piece_chest_wood` | **false** | Wood | +0,000 .. +0,787 |

Efeitos colaterais: `wood_floor` pede 2 madeiras, vida 400, `noSupportWear = true`, estação bancada
(`rangeBuild` 20 m); `wood_roof_45` também pede bancada e tem `noRoofWear = false`.

### Correção de doc (importante)

`AGENTS.md` (seção da construção) e o docstring de `BuildProbe.FootprintSupported`
(`src/Companions/AI/BuildProbe.cs:664-670`) e de `BuildSupportBelow.HoldsPlacedPiece`
(`:158-171`) afirmam **"telhado no Valheim tem `m_supports = false`"**. **É falso na 1.0.20** — todos
os telhados medidos têm `m_supports = true`. O bypass de telhado do `HoldsPlacedPiece` (`:182`
`return pieceClass == 3 || wearSupports`) é, na prática, **no-op** (as duas pontas dão `true`), exceto
no caso errado de um telhado apoiado em mobília. O motivo real das 13 peças de telhado recusadas
tem de ser outro (geometria: a fileira de baixo de uma água a 45° fica ~2 m na diagonal, fora da
caixa expandida do centro do telhado de cima).

## 6. Recomendação para o nosso validador

1. **Não estender a caixa para baixo.** O 0,15 m já é o número do jogo
   (`(size + 0,3)/2`). Qualquer folga a mais transforma `no_support` em peça colocada **e derrubada**.
   O `no_support` do caso 0,6 m está certo.
2. **Medir o collider, não o renderer.** `BuildProbe.BoxOf` (`:343`) usa `Renderer.bounds`; o jogo usa
   os **colliders** (`SetupColliders`). Para `wood_floor` o renderer é 2,27 × 0,26 × 2,29 enquanto o
   collider é ~2 × 0,1 × 2 → o nosso teste é **mais generoso** hoje (aceita peça que o jogo derruba) e
   ainda assim recusa o caso de 0,6 m. O fiel é repetir `SetupColliders`: por collider
   (`GetComponentsInChildren<Collider>(true)`, ignorando `isTrigger` e `attachedRigidbody`), pegar
   `BoxCollider.size*lossyScale`+`TransformVector(center)` com a rotação do collider, ou `bounds` para
   os demais, crescer 0,15 e testar **cada** caixa.
3. **Sólido sem `WearNTear` também apoia** (`WearNTear.cs:908-919` dá **apoio máximo**), e hoje
   `HoldsPlacedPiece` devolve `false` para `hasWear == false` (`BuildSupportBelow.cs:178-181`) — isso é
   falso negativo em peça encostada em pedra/árvore/estrutura no `Default`.
4. **Máscara**: o jogo olha `piece | Default | static_solid | Default_small | terrain`; o nosso
   `PlaceMask()` acrescenta `piece_nonsolid`, `vehicle` e `Water` → aceita apoio que o jogo não vê.
5. **Confiar no veredito do jogo só depois da colocação** — porque no ato *não existe* veredito. O
   caminho barato e honesto já está no código: `BuildSupport.Supported(wear)` logo após o `Place`
   (`MHR_Build.cs:2905`, `:4507`). Sugestão de desenho: manter a predição como **dica** (para não andar
   até uma peça que vai cair) e deixar o `no_support` do validador ser **não-fatal** quando a margem
   estiver na faixa 0,10–0,15 m (caso de borda) — colocar, ler o veredito, e reciclar se cair.
6. **O problema real do run é o plano, não o validador**: um degrau de 0,6 m entre dois pisos não tem
   apoio em vanilla (nem poste, ver §4). As saídas são de projeto: (a) `DeckLift`/degrau ≤ 0,15 m por
   peça, (b) levar a parede/viga do andar de baixo até o piso de cima no plano (o `PullDirectSupport`
   só puxa peça **do plano**), ou (c) aceitar `MaxOverlap ≥ 0,40` e fincar um `wood_beam_1` que
   atravessa 0,4 m acima do piso (resolve o apoio, fica visível).

## 7. O que NÃO deu para verificar

- **A espessura real do collider do `wood_floor`** (se é o mesh visual de 0,263 m ou uma tábua de
  ~0,1 m). Isso muda só o número do "vão máximo entre pivôs" (0,41 m vs 0,25 m), não a regra (0,15 m
  de folga) nem o veredito do caso de 0,6 m. Não medi porque exigiria colocar peça no mundo do run em
  andamento, e não há ferramenta no DevBridge que leia o `m_bounds`/collider de um prefab.
- **Ensaio destrutivo** (colocar o piso a 0,6 m e ver cair): não feito de propósito (run em andamento,
  drops de madeira no canteiro).
- O **upstream 1.5.8 não tem nada sobre isso** (grep de `HaveSupport|UpdateSupport|m_supports|no_support`
  no clone local: 0 ocorrências) — a construção autônoma é 100 % nossa; não há implementação de
  referência para copiar.
- `1.0.20` é a versão medida; versões anteriores tinham `m_supportRadius`/`m_supportPoints` e a regra
  mudou várias vezes (esta nota vale para a 1.0).
