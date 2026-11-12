# Colocação contra corpos, altura do corpo e `GetGroundHeight` (lido no decompilado, 04/11/2026)

Pergunta de origem: **quais fatos do jogo limitam a correção de construção com 5 companheiros** — bloqueio por
corpo, apoio/queda, `GetGroundHeight` sobre piso construído e altura real do corpo.

Fontes: decompilado de `valheim_Data\Managed\assembly_valheim.dll` (1.0.20) em
`%TEMP%\opencode\vh-decomp\valheim\` — as linhas citadas são desse arquivo. O que é **medido** em jogo vem do log
dos runs de 11/11/2026 (`docs/re/building-notes.md` §17.13/§17.16) e está marcado como tal. **Nada foi re-medido
nesta rodada** (sem acesso ao jogo/harness daqui): o resto é **lido no código**.

## 0. Resumo em 6 linhas

1. **Corpo bloqueia por `Physics.ComputePenetration`**, collider do fantasma × **`CapsuleCollider`** de **todo
   `Character`** num raio de **30 m do jogador que coloca** — `Player.cs:4195-4225`. **Tolerância zero** (qualquer
   penetração recusa; toque exato, sem sobreposição, é livre).
2. **Não existe `Character.m_height`/`m_radius` na 1.0.20**: o corpo é o `CapsuleCollider m_collider` do prefab
   (`Character.cs:340`, `:663`, `GetCollider()` `:3787-3790`, `GetHeight()` `:2182-2185`), e **nada no código
   escreve altura/raio**. Medido em jogo: **1,80 m de altura, r 0,30** (log `cápsula 39,90-41,70 m, r 0,30`).
3. **O jogo não move ninguém**: recusa com `PlacementStatus.BlockedbyPlayer` → `$msg_blocked` e devolve `false`
   (`Player.cs:4092-4095`, `:3049-3051`). Mover o companheiro é 100 % do nosso `[Building] StepAside`.
4. **`m_noClipping` tem outra régua:** `TestGhostClipping` só recusa com penetração **> 0,2 m**, e contra
   `m_placeRayMask` — que **não** tem layer de personagem (`Player.cs:4176-4193`, `:4100`, máscara em `:670`).
5. **Navmesh:** o agente `Humanoid` é **altura 1,8 / raio 0,4 / degrau 0,3 / inclinação 85** (`Pathfinding.cs:191-195`);
   o nosso agente (`Agent = 40`) é cópia dele com **`agentClimb = 0,6`** (`Patches/MHR_CompanionNavMesh.cs:100-105`).
6. **`ZoneSystem.GetGroundHeight` é só layer `terrain`** (`ZoneSystem.cs:676`, `:2738-2747`): sobre um piso
   construído devolve **o terreno abaixo** (ou o próprio `p.y`, se o raio não achar nada) — **nunca** o topo da peça.
   O que devolve topo de peça é `GetSolidHeight` (máscara com `piece`, `:2761-2770`).

## 1. Tabela `classe → método → assinatura → observação`

| classe | método | assinatura | observação (decompilado) |
|---|---|---|---|
| `Player` | `UpdatePlacementGhost` | `private void UpdatePlacementGhost(bool flashGuardStone)` | `Player.cs:3793-4114`. `:4092-4095` `if (CheckPlacementGhostVSPlayers()) m_placementStatus = PlacementStatus.BlockedbyPlayer;` — é a **última** das recusas antes do `SetPlacementGhostValid` (`:4113`). **Não** testa apoio (§2 de `support-rule-wearntear.md`). |
| `Player` | `TryPlacePiece` | `public bool TryPlacePiece(Piece piece)` | `:3041-3097`. Chama o `UpdatePlacementGhost` **de novo** (`:3043`) antes do `switch`, então o status é do instante da colocação. `BlockedbyPlayer` → `Message($msg_blocked)` + `return false` (`:3049-3051`). Daqui até o `PlacePiece` (`:3093`) **nada** toca em `Character`: nenhum empurrão, nenhum teleporte. |
| `Player` | `CheckPlacementGhostVSPlayers` | `private bool CheckPlacementGhostVSPlayers()` | `:4195-4225`. `Character.GetCharactersInRange(base.transform.position, 30f, list)` (`:4202`) → **30 m em volta de quem coloca**, e são **todos os `Character`** (`Character.cs:4013-4023`), não só jogadores: criatura também bloqueia. Ignora collider `isTrigger`/desligado, `MeshCollider` **não convexo** e o root do ghost (`:4206-4214`); depois, para cada collider restante × cada corpo: `Physics.ComputePenetration(collider, collider.transform.position, collider.transform.rotation, collider2, ..., out _, out _)` (`:4218`) → **`true` = recusa, sem limiar de profundidade**. |
| `Character` | `GetCollider` | `public CapsuleCollider GetCollider()` | `Character.cs:3787-3790` = `m_collider`; atribuído em `Awake` (`:663`) com `GetComponent<CapsuleCollider>()` — **é o collider do mesmo GameObject do Character**, e as dimensões vêm do **prefab**. Grep de `m_collider.height|radius|center =` no assembly: **0 ocorrências** (nada muda em runtime; não há agachar). |
| `Character` | `GetHeight` / `GetRadius` | `public float GetHeight()` / `public float GetRadius()` | `:2182-2185` `Mathf.Max(m_collider.height, m_collider.radius * 2f)`; `:2173-2180` `m_collider.radius` (× escala quando há `Rigidbody`). |
| `Player` | `TestGhostClipping` | `private bool TestGhostClipping(GameObject ghost, float maxPenetration)` | `:4176-4193`, chamado só em `:4100` (`component.m_noClipping`) com **0,2 m**: `OverlapSphere(ghost.position, 10f, m_placeRayMask)` + `ComputePenetration(...) && distance > maxPenetration` → `PlacementStatus.Invalid` (`$msg_invalidplacement`). `m_placeRayMask` (`:670`) = `Default, static_solid, Default_small, piece, piece_nonsolid, terrain, vehicle` — **sem** `character*`. |
| `Character` | `GetCharactersInRange` | `public static void GetCharactersInRange(Vector3 point, float radius, List<Character> characters)` | `:4013-4023`. Percorre `s_characters` (todo Character **instanciado**, vivos ou não) e filtra por `DistanceSqr < radius²`. Raio do jogo: **30 m do jogador**, não da peça. |
| `WearNTear` | `SetupColliders` / `UpdateSupport` / `UpdateWear` | ver `support-rule-wearntear.md` §1 | Confirmado de novo nesta rodada: `(size + 0,3) / 2` (`WearNTear.cs:507-510`) = **0,15 m por lado**; `m_support = maxSupport` + `return` se houver **terreno** na caixa (`:903-907` → `flag` → `:953-962`); `num = 100f` sem apoio (`:568-575`) → `damage = num/100*m_health` (`:687`) → `ApplyDamage` (`:1250-1272`) → `Destroy` (`:1290`) → `m_piece.DropResources` (`:1302-1305`). |
| `Pathfinding` | `SetupAgents` | `private void SetupAgents()` | `Pathfinding.cs:188-259`. `AgentType.Humanoid`: `agentHeight 1.8`, `agentClimb 0.3`, `agentRadius 0.4`, `agentSlope 85` (`:191-195`). |
| `ZoneSystem` | `GetGroundHeight` | `public float GetGroundHeight(Vector3 p)` / `public bool GetGroundHeight(Vector3 p, out float height)` | `ZoneSystem.cs:2738-2747` / `:2749-2759`. `Raycast` de `y=6000` para baixo, **10000 m**, máscara `m_terrainRayMask` = `LayerMask.GetMask("terrain")` (`:676`). Sem hit: a versão `float` devolve **`p.y`** (fail-open); a versão `bool` devolve `false` e `height = 0`. |
| `ZoneSystem` | `GetSolidHeight` (4 overloads) | `public float GetSolidHeight(Vector3 p)`, `(Vector3, out float, int heightMargin = 1000)`, `(Vector3, float radius, out float, Transform ignore)`, `(Vector3, out float, out Vector3, out GameObject)` | `:2761-2824`. Máscara `m_solidRayMask` = `Default, static_solid, Default_small, piece, terrain` (`:678`) → **inclui peça**: é a ferramenta certa para "o que tem sob os pés". Rejeita collider com `attachedRigidbody`. `FindFloor` (`:2840-2849`) é o mesmo com raio de 1000 m a partir de `p + 1 m`. |

## 2. O que isso decide (consequências práticas)

### 2.1 Bloqueio por corpo

- **O jogo é mais rígido que a nossa sonda em um ponto e mais frouxo em outro.** Rigidez: `ComputePenetration` recusa
  **qualquer** sobreposição (o nosso `BuildBody.Blocks` trata **toque como livre**, por comparação de spans). Frouxidão:
  o jogo só olha corpos a **30 m do jogador** e só os **`Character`**; a nossa sonda olha jogadores + os **nossos**
  companheiros com `CapsuleOf`/`AABB` (`AI/BuildProbe.cs:744-802`), sem criaturas — é o item (8) das pendências.
- **Colocar e "esperar o corpo sair" não é preciso para nós:** o executor não passa pelo fantasma (`BuildExecutor`
  usa `PlaceDirect` = `Instantiate` + `OnPlaced`, `AI/BuildExecutor.cs:137`), então o `blocked_by_player` do log é
  **nosso**, não do jogo. Quem colocasse pelo `Player.PlacePiece` teria a recusa de graça; nós temos de perguntar.
- **O jogo nunca move o corpo** — nem o jogador, nem companheiro. "Sair da frente" só existe porque nós mandamos
  (`[Building] StepAside`).

### 2.2 Altura: 2,02 m de vão × corpo de 1,80 m

- **Fisicamente cabe, raspando:** 2,02 m de piso a terreno, menos a espessura do collider do piso **abaixo do pivô**
  (o renderer mede `min.y = -0,140`; a espessura do **collider** não foi medida — §5 de
  `support-rule-wearntear.md`), dá **1,88–1,97 m livres**; o corpo é **1,80 m** → folga de **8–17 cm**.
- **A rota é o gargalo, não o corpo:** o agente é **altura 1,8 / raio 0,4** e o Recast quantiza em voxels (ordem de
  `agentRadius/3` ≈ 0,13 m). Uma folga de 8–17 cm é **menos de um voxel** → o tile **pode não existir**, e aí o
  companheiro não tem rota para baixo do piso mesmo cabendo. **Não verificado** em jogo (exigiria `nav.probe` sob o
  piso).
- **O vão de 1 m do `DeckLift` (piso 39,78 / terreno 38,78) é outra história:** 0,9 m livres contra corpo de 1,8 m e
  agente de 1,8 m — **não cabe nada**; quem estiver ali está preso e não há tile. É exatamente o §17.13 de
  `building-notes.md` (a pendência do vão do deck), e explica por que o sintoma foi "corpos presos", não "corpos
  empurrados".

### 2.3 `GetGroundHeight` não é "o chão que se pisa"

- **Sobre piso construído devolve o terreno por baixo** (pode ser 1 m, 2 m ou 10 m abaixo). Não é bug: é a definição
  (máscara `terrain`).
- Para o `DeckLift` o uso está **certo**: `buried = GetGroundHeight(pos) - pos.y` (`Jobs/MHR_Build.cs:1838`) é
  literalmente "quanto a peça está **sob o terreno**", e piso construído não deve contar.
- Para o **poste de emergência** o uso é de **fallback**: a base vem do laço de `Raycast` com a máscara de peças
  (`MHR_Build.cs:5059-5082`) e o `GetGroundHeight` só entra quando nenhuma superfície serviu (`:5082`). O risco é
  real e estreito: com a única base "que segura" fora de `PostReach` (10 m), o fallback devolve **o terreno**, que
  pode estar **abaixo do piso** — o poste nasce no chão e atravessa o piso. Se o `GetGroundHeight` não achar terreno
  (masmorra/vazio) devolve `piece.y` → `gap = 0` → `PickPost` falha e **não** há poste (fail-safe).
- **Se a intenção for "o que tem sob a peça", o método do jogo é `GetSolidHeight`** (máscara com `piece` + `terrain`,
  `ZoneSystem.cs:2761`), que já tem overload com `radius` e `ignore` — a mesma família que o `Character` usa para
  pousar.
- Efeito colateral do "assentar no terreno" (`MHR_Build.cs:1859-1863`): `pos.y = ground` põe **o pivô** no terreno;
  numa peça com bounds negativos grandes (ex.: `wood_pole2`, −1,0..+1,0) isso enterra metade da peça. Ela fica
  **estável** (terreno = apoio máximo), mas o que aparece no mundo é o topo, não a peça inteira.

### 2.4 Apoio: "colocar e ver cair" funciona, e por que não compensa

- **O jogo aceita a peça sem apoio na hora** (`UpdatePlacementGhost` não tem veredito de apoio) e **derruba depois**:
  `OnPlaced` zera `m_createTime` (`WearNTear.cs:442-446`) → `ShouldUpdate` devolve `true` na hora (`:516-523`) →
  próximo `UpdateWear` com `num = 100` → dano de **100 % da vida** → `Destroy` → **dropa os materiais**
  (`:1302-1305`). O `WearNTearUpdater` gasta **1 s por volta** (`WearNTearUpdater.cs:20`, `:60-73`) → **~1 s** numa
  base normal; numa zona de ~1000 peças a volta é a mesma, então continua ~1 s.
- **Consequência:** "colocar e ver cair" **prova** o apoio, mas custa material + uma das 3 tentativas + drops no
  canteiro. É aceitável como último recurso na faixa 0,10–0,15 m (recomendação §6 de `support-rule-wearntear.md`),
  **não** como estratégia para vãos de 0,6 m.
- **Peça no nível do terreno é estável**, e por um motivo bruto: qualquer collider de `terrain` na caixa expandida
  (0,15 m por lado) faz `m_support = maxSupport` e `return` (`WearNTear.cs:903-907` + `:953-962`) — o terreno
  **anula** a conta de perda por distância. Como o pivô no nível do terreno deixa o collider da peça **meio
  enterrado**, a sobreposição é certa (não é tangência).

## 3. O que NÃO deu para verificar

- **Espessura/forma do collider de cada prefab** (a tabela §5 de `support-rule-wearntear.md` é de **renderer**): é o
  que transforma "2,02 m de vão" em "1,88 ou 1,97 m livres". Exigiria ler o `Collider` do prefab no jogo.
- **Se existe tile de navmesh sob um piso a ~1,9 m do chão:** precisa de `nav.probe`/`nav.tiles` no jogo com a casa
  de referência e o agente 40. Não medido.
- **Semântica fina de `Physics.ComputePenetration` na tangência exata** (PhysX, não é código gerenciado). O que o CIL
  prova é que **não há limiar**: o `distance` só é usado no `TestGhostClipping` (> 0,2 m), nunca no
  `CheckPlacementGhostVSPlayers`.
- **As dimensões 1,80 m / r 0,30 são do prefab**, não do código (0 atribuições no assembly): o número vem do log de
  um run (`building-notes.md` §17.16) e **não** foi re-medido nesta rodada. Um companheiro com prefab de corpo
  diferente (anão, criatura) teria outra cápsula — e é o `CapsuleOf` que lê o real.
- **Nada do upstream** se aplica: a construção autônoma é 100 % nossa (grep de `HaveSupport|m_supports|no_support` no
  clone 1.5.8: 0 ocorrências).
