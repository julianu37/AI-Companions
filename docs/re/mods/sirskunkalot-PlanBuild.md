# PlanBuild — sirskunkalot/PlanBuild (0.20.0, commit `e12ed86`, 01/10/2026)

**JOTUNN-ONLY** — depende de `ValheimModding-Jotunn` (`manifest.json`, dependências) e do pacote `JotunnLib 2.30.1`
(`PlanBuild/PlanBuild.csproj:178-179`). Vale como **ideia e formato**, nunca como código colável no nosso runtime.

- **Fonte:** <https://github.com/sirskunkalot/PlanBuild> · **Licença:** **WTFPL** · **Última atualização:** 01/10/2026
  (commit `e12ed86`) · **Jogo alvo:** Valheim 1.0.x
- **Repo público?** sim · **DLL fechada?** não
- **Nosso problema:** (a) ordem de construção de um plano grande, (b) apoio/estabilidade, (c) estação no raio de cada
  peça — **do lado de quem executa** um plano (o totem), e o **formato `.blueprint`** que já consumimos.
- **Como li:** `shell` negado nesta sessão (sem clone); leitura por `raw.githubusercontent.com` no commit `e12ed86`.
  Caminhos relativos a `PlanBuild/`. **Clone pendente** para *project reference*.

## O que o README promete

> "Planned pieces that are **unsupported** can not be finished. These pieces are also slightly more transparent so you
> can see what is and isn't supported. **The planned pieces themselves do not require support** [para existir]."
> — `README.md:29`

> "Build a **Plan Totem** near your planned structures to be able to add resources in a centralized location for all
> individual pieces on the plan." — `README.md:37`

> "Copy existing structures into Blueprints and rebuild them as planned or regular pieces … saved in and loaded from
> the filesystem as `.blueprint` files." — `README.md:61`

## O que o código faz

### (b) Apoio: **o fantasma É um `WearNTear` de verdade — quem calcula é o jogo**

Este é o truque central, e é o oposto do BuildOrders (que reimplementa):

- Cada peça de plano herda do prefab real: `m_minSupport = wearNTear.GetMinSupport()` e
  `m_maxSupport = wearNTear.GetMaxSupport()` — `Plans/PlanPiece.cs:45-47`.
- `CalculateSupported()` **não calcula nada**: lê o valor de apoio que o **jogo escreveu no ZDO do fantasma** e compara
  com o mínimo — `PlanPiece.cs:93-106` (default `m_maxSupport` porque `ZDOVars.s_support` é chave de sessão, `:102-105`).
- Para o jogo realmente escrever esse valor no fantasma, eles **patcham** `WearNTear.UpdateSupport` com um **postfix**
  que força `GetZDO().Set(ZDOVars.s_support, m_support)` — o vanilla só escreve quando difere do cache
  (`PlanPiece.cs:799-810`); mais prefixos em `WearNTear.GetSupport` (`:775-777`) e `WearNTear.HaveSupport` (`:787-789`).
- Peças que "não gastam com falta de apoio" (`m_noSupportWear`, ex. `blackmarble_floor_large`) são forçadas a `false`
  no prefab de plano para não lerem como "sem apoio" — `PlanPiece.cs:95-98`, campo em `:269`.
- **Portão duro:** o totem só constrói o que tem `HasSupport()` — `Plans/PlanTotem.cs:174` (contagem) e `:179`
  (condição para gastar material). Sem apoio, a peça **fica para o próximo ciclo**.
- O jogador também é barrado: build manual de peça de plano sem apoio ⇒ mensagem
  `$message_plan_piece_not_enough_support` — `PlanPiece.cs:502-504`.

### (a) Ordem: `m_maxSupport` desc → Y asc → distância, num laço de 3 s

- `FindPlanPiecesInRange` (`PlanTotem.cs:139-144`):
  `OrderByDescending(m_maxSupport).ThenBy(position.y).ThenBy(distance)`.
  Traduzindo: **material mais resistente primeiro** (`GetMaxSupport` é por material), depois **de baixo para cima**,
  depois o mais perto. **Não** há profundidade de apoio (como no BuildOrders) nem classe de peça.
- Laço `UpdatePlanTotem` (`PlanTotem.cs:147-238`): a cada **3 s** (`:156`), **só no dono** (`m_nview.IsOwner()`,
  `:179`), em `try/catch` **por peça** para exceção de uma não matar a coroutine (`:215-220`).
- **Peça que falha:** não há retry nem skip — ela simplesmente **não é construída** naquele tique e volta a ser
  avaliada 3 s depois, para sempre (`:170-221`). É "soft retry infinito" por polling, o oposto do skip de 600 s do
  AICompanion. O totem **nunca** tenta a peça sem ter **apoio + material + estação** (`:179-195`).
- A ordem de fato consumida por quem só quer "de baixo para cima" é o **Y asc** — que é a mesma ideia do nosso
  `StoreyOf`, mas **sem** as classes (piso→escada→parede→telhado→porta).

### (c) Estação: checagem **na posição da peça**, com o `noworkbench` respeitado

- `HasRequiredCraftingStationInRange()` (`PlanPiece.cs:202-210`):
  `originalPiece.m_craftingStation == null || NoWorkbenchRequired || CraftingStation.HaveBuildStationInRange(name, transform.position)`.
  Ou seja: **na peça**, não no jogador (o BuildOrders faz no jogador no "Build all").
- `NoWorkbenchRequired` honra a chave global `NoWorkbench` (`PlanPiece.cs:212-216`) — detalhe que o nosso validador
  deveria considerar (`[Building]`/cheat `noworkbench` no mundo).
- Se falta estação: a peça é **omitida**, o nome da estação entra em `m_missingCraftingStations`
  (`PlanTotem.cs:196-199`) e o totem mostra o que falta. **Ele não crafta bancada.**
- Mas: **a estação do plano funciona** — `SpawnPiece` chama `Player.AddKnownStation` quando o criador é o jogador
  local (`PlanPiece.cs:721-725`), e um `WearNTear.OnPlaced()` é disparado em toda peça (`:739-743`). Então uma
  bancada que faz parte do plano passa a valer nos tiques seguintes (é o mesmo efeito do re-passe do BuildOrders,
  aqui implícito no polling de 3 s).

### O executor: `SpawnPiece` (referência de "como colocar sem o `Player.PlacePiece`")

- `Instantiate(prefab, position, rotation)` + `OnPieceReplaced` + efeitos/estatística do jogador local
  (`PlanPiece.cs:714-738`), `WearNTear.OnPlaced()` (`:742`), `TextReceiver` (`:745-749`), `ItemDrop.MakePiece`
  (`:750-754`) e **`Piece.SetCreator(creatorID, ...)` por último** (`:755`). É praticamente o nosso E6 — a diferença é
  que eles fazem via RPC do dono (`RPC_SpawnPieceAndDestroy`, `:693-712`).

## Lacunas (promete e não faz)

- **Não é "auto build do jogador":** o totem precisa que **você** ponha material no baú dele (8×8, `PlanTotem.cs:70-71`)
  e ele não anda, não busca, não crafta estação. Sem material, fica parado em silêncio (só lista o que falta,
  `:222-234`).
- **Ordem crua para plano grande:** `m_maxSupport` desc põe pedra antes de madeira e não conhece andar; num plano
  tipo `CommHomeBasicBP` (983 peças) isso tende a repetir a falha que já medimos (telhado antes do piso).
- **Sem relatório do que falhou:** não há contagem de "puladas"/"sem apoio" ao jogador — o `m_supportedPieces` (`:19`)
  não é exposto como resultado da obra, só como estado do totem.
- **Custo:** 1 `WearNTear` + 1 `ZNetView` **por peça de plano** (blueprint de 985 peças = 985 objetos vivos),
  reavaliados a cada 3 s. É o preço do truque elegante de apoio.

## Serve para nós?

| aspecto | veredito |
|---|---|
| ideia | **adotar** o `noworkbench` (`GlobalKeys.NoWorkbench`) no nosso validador de estação — barato e correto |
| implementação | **não portável** — Jotunn-only e o truque de apoio depende de patchar `WearNTear.UpdateSupport` em peças-fantasma que nós não temos (nosso plano é dado, não GameObject) |
| custo | baixo (o `noworkbench`); médio se quiséssemos expor "por que não" no totem (não se aplica) |

## Recomendação

**adaptar** (só dois detalhes) e **descartar** o resto como implementação. (1) Honrar `GlobalKeys.NoWorkbench` no
`PlanStations`/`BuildSite.Check` — hoje o nosso mod exige bancada sempre, e num mundo com `noworkbench` o jogo
aceitaria sem; (2) usar `HasRequiredCraftingStationInRange` como **modelo de escopo** (checar na peça, não no
companheiro) para conferir se o nosso `HaveBuildStationInRange` está no ponto certo. **Não** copiar nada de
`PlanPiece`/`PlanTotem`: é Jotunn e o apoio por `WearNTear` fantasma não é o nosso desenho.
