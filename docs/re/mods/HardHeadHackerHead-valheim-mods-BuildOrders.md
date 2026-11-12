# Quad's Build Orders — HardHeadHackerHead/valheim-mods (1.14.7, commit `643c7a9`, 23/10/2026)

- **Fonte:** <https://github.com/HardHeadHackerHead/valheim-mods/tree/main/mods/BuildOrders> · **Licença:** MIT ·
  **Última atualização:** 23/10/2026 · **Jogo alvo:** Valheim 1.0.x (BepInEx 5)
- **JOTUNN-ONLY?** **não** (o `.csproj` não referencia Jotunn — só `UnityEngine.*` de `$(ManagedDir)`,
  `mods/BuildOrders/BuildOrders.csproj:8-12`) · **Repo público?** sim · **DLL fechada?** não
- **Nosso problema:** (a) **ordem de construção** de um plano grande e o que fazer com a peça que falha; (b)
  **apoio/estabilidade** — validar antes ou deixar o jogo decidir, e como re-tentar; (c) **estação**
  (bancada/stonecutter) — como garantir a estação no raio de cada peça.
- **Como li:** o `shell` está negado nesta sessão (sem `git clone`); leitura por `raw.githubusercontent.com` no commit
  `643c7a9`. Caminhos abaixo são relativos a `mods/BuildOrders/`. **Clone pendente** para virar *project reference*.

## O que o README promete

> "Stability: while planning, ghosts are coloured by how well they would be supported (blue solid, green to red weaker,
> red would fall), **worked out from the same rules the game uses**" — `README.md:24`

> "Walk up to a ghost and tap E to build it … **It builds even if nothing supports it yet**" — `README.md:21`

> "Other mods can add to it through its planning API" — `README.md:96`

## O que o código faz

### (b) Apoio: reimplementação do `WearNTear.UpdateSupport`, com as **propriedades do jogo por reflexão**

- Propósito declarado no cabeçalho da classe: reproduzir o `UpdateSupport` do jogo — colliders sólidos da peça,
  girados, **crescidos 0,15 m**; chão (ou qualquer sólido que não seja peça) dentro dessas caixas dá apoio cheio; de
  uma peça encostada vem o apoio dela **menos a perda do material pela distância entre centros de massa** (perda
  lateral, ou a vertical quando o apoio está embaixo); **dois apoios em lados opostos são averaged**; abaixo do mínimo
  do material a peça **quebra** e o que estava em cima é recalculado — `Stability.cs:8-15`.
- As constantes **não são chutadas**: `WearNTear.GetMaterialProperties` (max/min/perda horizontal/perda vertical) e
  `WearNTear.GetCOM`/`GetSupport` são lidos **por reflexão**, com cache por prefab — `Stability.cs:118-136`.
- Chão = `Physics.CheckBox` nas layers `terrain, Default, static_solid, Default_small`; peças reais =
  `OverlapBoxNonAlloc` na layer `piece` (`Stability.cs:177-191`). Crescimento de **0,15 m** em `Stability.cs:162-163`.
- Propagação de apoio (`Stability.cs:216-287`): laço externo **60×** e interno **60×**; cada peça pega
  `s - HLoss*dist*s` com `dist = min(COM↔COM, COM↔pos) + 0,1` (`:238-255`); média de dois apoios com
  `Vector3.Angle >= 100°` (`:261-277`); teto no `Max` (`:279`); **quebra abaixo do `Min`** e recomeça com a peça fora
  da conta (`:284-287`). O resultado guarda **de quem** veio o apoio (`From`, `:290`) — é o que a ordem e a espera usam.
- **Consequência de projeto:** eles **nunca "colocam e deixam o jogo decidir"** para o "Build all" — só constroem o
  que a simulação diz que fica de pé (`README.md:24` + `Stability.cs:8-15`).

### (a) Ordem: profundidade de apoio, não classe de peça

- `StandingOrder` (`Plugin.cs:577-604`): (1) calcula a estabilidade do lote e **descarta as que colapsam**, repetindo
  **até 6 rodadas** (tirar uma peça derruba quem estava nela) — `Plugin.cs:581-588`; (2) ordena por
  **profundidade** = quantas peças de apoio há entre ela e o chão/peça já construída, recursivo com guarda 400 e
  "laço conta como chão" (`Plugin.cs:590-599`, aplicado em `:602`); (3) desempate por **altura em degraus de 0,5 m**
  (`Mathf.Round(o.Pos.y * 2f)`) — `Plugin.cs:603`.
- Antes disso, o lote é filtrado por **raio + fantasma visível + "pode construir"** e cortado por
  `Round(y*2)` → distância → `Take(600)` (`Plugin.cs:615-620`, `Plugin.Helpers.cs:60-71`).
- Ajudante (NPC) recebe essa mesma lista pronta: `HelperNext` devolve `"id|prefab|x|y|z"` **na ordem de construção**
  (`Plugin.Helpers.cs:60-71`).

### (a) A peça que falha: **o que estava em cima espera** (não é retry às cegas)

- `BuildMany` (`Plugin.cs:530-570`): um `HashSet failed`; se o apoio (`Stab.From`) de uma peça contém peça que falhou,
  ela é marcada como falha e contada em `waited` **sem tentar** (`Plugin.cs:551`) — *"if one of those could not be
  built, what rests on it waits"* (`:527-528`). Duas peças por frame (`:553`). O relatório final separa
  `built / need materials / need station / unsupported / waiting` (`:562-567`).
- O "Build all" do jogador **não gasta material sem apoio**: `AffordableNearby` só entra no lote o que tem receita
  conhecida, estação no raio **e** custo coberto, contando o orçamento para baixo (`Plugin.cs:645-656`).
- **Nuance documentada:** build manual de **uma** peça (E) **ignora** o apoio de propósito — `README.md:21`; o laço
  `BuildMany` é que exige (`Plugin.cs:526-528`). Não é contradição: é duas portas diferentes.

### (c) Estação: três mecanismos, nenhum deles "planta a bancada"

1. **Portão por peça, na posição da peça** — `HelperMayBuild` (`Plugin.Helpers.cs:52-58`):
   `piece.m_craftingStation == null || CraftingStation.HaveBuildStationInRange(name, o.Pos) != null`; junto com
   `IsRecipeKnown`, `PrivateArea.CheckAccess` e `Location.IsInsideNoBuildLocation` (`:55-57`).
2. **Re-passe depois de erguer a estação do próprio plano** — `BuildMany` roda **até 4 passes**
   (`Plugin.cs:539-558`): *"a station built in this pass … is not near you yet when the pass is chosen, so what needs
   it waits; once a station went up and something waited for one, go round again"* (`:537-538`). É o item
   **1.14.7** do CHANGELOG (`CHANGELOG.txt:1`).
3. **Motivo explícito ao jogador** — `HelperBlockers` devolve `"kind|count|detail"` com `unlocked`, **`station`** e
   `protected` (`Plugin.Helpers.cs:73-98`). O NPC usa isso como fala (`Building.Explain`, ver a ficha do AICompanion).

- **O que eles NÃO fazem:** não craftam nem inserem bancada no plano. Sem estação, a peça é **omitida** do lote e
  contada — nunca tentada (`Plugin.cs:633-642`).
- **Detalhe de alcance:** no "Build all" a checagem é a partir de **`player.transform.position`**
  (`Plugin.cs:633`), não da peça; no ajudante é a partir da **peça** (`Plugin.Helpers.cs:57`). Como o jogador precisa
  estar perto para construir, a aproximação funciona — mas é uma aproximação.

## Lacunas (promete e não faz)

- **Auto-poste de apoio: removido.** `SupportIdPrefix = "s-"` é só legado — *"support posts placed by older versions
  (left out when a plan is moved)"* (`Plugin.Supports.cs:16`). Hoje o `Supports.cs` calcula **footprint para
  nivelar terreno** (pés das peças que devem ficar no chão + poste que não apoia em nada do plano,
  `Plugin.Supports.cs:85-134`), **não** finca poste para fechar vão. O nosso `apoio de emergência` não tem
  equivalente aqui.
- **Ordem por profundidade ≠ ordem por andar.** Não há noção de "andar" nem de "porta por último": o desempate é só
  `y*2` (`Plugin.cs:603`). Quem quiser piso→escada→parede→telhado→porta **não** acha isso aqui.
- **Custo:** a estabilidade é **O(n²)** em pares dentro do lote (`Stability.cs:197-209`) com lote de até **600**
  (`Plugin.cs:619`) e raio de **80 m** (`Stability.cs:147`). Roda a cada `_stabilityDirty`/periódico
  (`Stability.cs:115`, `:294`). Em plano de ~1000 peças isso é caro — motivo pelo qual o nosso caminho (validador
  barato por peça + retry) é defensável.

## Serve para nós?

| aspecto | veredito |
|---|---|
| ideia | **adotar** a regra dura *"nada sobe em cima de peça que falhou"* (hoje esperamos por tempo: `BuildFailure.BodyBarrierSeconds`, portão de 8/12/20/25 s) |
| implementação | **portável** (MIT, sem Jotunn) — mas **não copiar** o modelo inteiro: reimplementar `UpdateSupport` com a tabela de materiais é caro e nós já temos o veredito do jogo (`AI/BuildSupport.cs`) |
| custo | baixo para (a) — trocar "espera cega por segundos" por "espera **enquanto a peça de apoio estiver na fila**" no `MHR_BuildGroup`; alto para trazer o `Stability.cs` |

## Recomendação

**adotar ideia** (não a implementação). Duas coisas valem: (1) **dependência explícita** — gravar para cada peça
*quem* a apoia (eles guardam `From`, `Stability.cs:290`) e **não gastar tentativa** enquanto houver apoio na fila, em
vez do relógio fixo; (2) **re-passe da estação** como garantia de 2ª linha atrás do nosso `InsertBenchStep`
(o CHANGELOG deles, 1.14.7, é a prova de que o furo é real e de que a bancada do próprio plano não conta no passe que
a escolheu). **Não** trazer o `Stability.cs`: a nossa regra de 0,15 m com o veredito do jogo já cobre, e o custo
O(n²) não paga.
