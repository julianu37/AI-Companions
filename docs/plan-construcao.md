# Plano — construção autônoma pelos companions (Trilha E)

> **Status: planejado** (01/11/2026, v2). Nada implementado ainda — este documento é a proposta a ser
> aprovada antes de codar. Base: Valheim **1.0.20** (Unity 6), o código do mod na **1.9.5** e a pesquisa
> do ecossistema feita em 31/10–01/11/2026 (§3).
>
> **v2:** a pesquisa ampliada mudou **duas** decisões da v1: (1) o executor **reusa o `Player.PlacePiece`**
> quando o `Player` do dono está carregado (é o que o BuildOrders e o PlanBuild fazem, cada um do seu jeito),
> com um executor próprio só como reserva; (2) o alvo das **construções grandes** é o **BuildOrders** (API
> documentada, sem Jötunn, e o companion do próprio autor já constrói os planos dele) — **não** o PlanBuild.
>
> **Escopo da 1ª entrega:** a **bancada de trabalho** (`piece_workbench`). O resto vem em fases, cada uma
> testável em jogo.

---

## 0. A meta em uma frase

O jogador diz **"Maria, construa uma bancada de trabalho aqui"** e, alguns segundos depois, existe uma
bancada no chão, **dele** (criador = jogador), funcionando como estação de craft — com o companheiro tendo
resolvido sozinho: receita → materiais (baú/coleta) → local válido → colocação → confirmação.

O valor não é a bancada: é que a mesma máquina de estados serve para **qualquer peça**. Quando ela monta
sozinha uma oficina (bancada + baú + fogueira + cama), o companheiro deixa de ser "mão de obra" e passa a
ser **construtor**.

---

## 1. As sugestões, em resumo (o que eu faria e por quê)

| # | Sugestão | Por quê |
|---|---|---|
| **S1** | **Não** tentar reusar `Player.TryPlacePiece` / `UpdatePlacementGhost` / `PieceRayTest` (são `private`, presos à câmera e ao `m_placementGhost`), **mas reusar `Player.PlacePiece`**, que é público. | `PlacePiece` (L3099) faz tudo o que o vanilla faz: `Instantiate`, `AddKnownStation`, `PrivateArea.Setup`, `WearNTear.OnPlaced`, `IPlaced.OnPlaced`, `ItemDrop.MakePiece`, efeito, stats e criador = jogador. É o que o **BuildOrders** chama (`player.PlacePiece(piece, o.Pos, o.Rot, doAttack: false)`) e o que o PlanBuild reproduz à mão. |
| **S2** | **Executor próprio só como reserva**, para quando o `Player` do dono não estiver carregado nesta máquina. | Em single/host (o caso normal) o `Player` existe; em MP pode não existir. Duas rotas, uma função só (`BuildExecutor.Place`). |
| **S3** | **Validador dirigido pelos campos do `Piece`**, nunca por uma tabela nossa de "o que a bancada precisa". | São ~20 flags (`m_groundOnly`, `m_noInWater`, `m_notOnWood`, `m_onlyInBiome`, `m_noClipping`…). Regra por peça envelhece; ler as flags serve para vanilla, mod e blueprint. |
| **S4** | **Fase 0 é uma sonda, não gameplay:** `modia_build_probe <prefab>`. | As flags da `piece_workbench` não estão publicadas em lugar nenhum (nem no Fandom): estão no bundle. O autor do BuildOrders chegou à mesma conclusão ("`inspect <prefab>`: read from the live game" está nas ideias dele). |
| **S5** | **O problema difícil é achar o local, não instanciar a peça.** Busca em anéis, com o ponto apontado como primeiro candidato. | Nenhum mod do ecossistema procura local: **todos recebem poses prontas** (o jogador aponta/coloca o fantasma). Essa é a parte original do nosso trabalho. |
| **S6** | **Ordem nova `build`, com `target` = a peça** (`ValidActions` 35 → 36) **+ rede por palavras**. | Mesmo padrão que salvou o `drop`/`equip` (3.6b) e as 5 interações (2.0). |
| **S7** | **"aqui" tem semântica fixa**: o que o jogador **aponta** (50 m, mesmos layers da tecla de comando) → 3 m à frente → em volta do companheiro. Sem "aqui", perto da casa. | É o gesto que ele já usa na tecla de comando (`CompanionInteract.PointedCollider`). |
| **S8** | **Material: mochila → baús → coleta.** Custo sempre de `Piece.m_resources`. | `Inventory.CountItems`, `MHR_ToolKeeper.UsableChest` e o teto de ordem (`HC_OrderCap`) já existem. |
| **S9** | **Verificação é do jogo:** além de `ZNetView`/criador, conferir que a `CraftingStation` entrou no registro (`HaveBuildStationInRange`). | "A bancada existe" ≠ "a bancada funciona". |
| **S10** | **Fail-open com teto de 30 s** (regra do `MHR_Collect`) e **sempre** uma fala final. | Job travado é pior que job que falha. |
| **S11** | **Construções grandes = BuildOrders (integração mole, sem dependência).** O companheiro constrói os fantasmas que o jogador planejou. | O BuildOrders publica **API** (`TryCreateGhostPlan`/`TryCreateBuildingShell` até **2.048** peças) e **3 funções por `AppDomain`** (`Count`/`Next`/`Place`, mais `Blockers`) que dão a **ordem de suporte** e o **motivo** de não dar. Ele **não** exige Jötunn. |
| **S12** | **Não reinventar estabilidade, ordem de peças, armazenamento de fantasmas nem o parser de blueprint.** | O `Stability.cs` do BuildOrders tem ~19 KB reimplementando o `WearNTear.UpdateSupport`; o formato de blueprint é JSON dele (`_pieces.json` traz tamanho/snap/custo/estação de cada peça). |

---

## 2. O que o jogo já resolve (e o que não resolve)

### 2.1 O caminho que o jogador percorre hoje (vanilla)

| Passo | Onde está | Público? |
|---|---|---|
| Escolher a peça no martelo | `PieceTable.GetSelectedPiece()` | ✅ |
| Calcular a posição do "fantasma" e validar | `Player.UpdatePlacementGhost(bool)` — **L3793** | ❌ `private` |
| Achar o ponto sob a mira | `Player.PieceRayTest(...)` — **L4227** | ❌ `private` |
| Checar materiais + estação | `Player.HaveRequirements(Piece, RequirementMode)` — **L2838** | ✅ |
| Consumir materiais | `Player.ConsumeResources(...)` — **L2894** | ✅ |
| Colocar | `Player.TryPlacePiece(Piece)` — **L3041** → `Player.PlacePiece(...)` — **L3099** | ✅ (`PlacePiece` é público) |
| Saber se a receita é conhecida | `Player.IsRecipeKnown(piece.m_name)` — **L5233** | ✅ |
| A peça nascer "de verdade" | `Instantiate` + `WearNTear.OnPlaced()` (**L442**) + `Piece.SetCreator(...)` (**L429**) + `ItemDrop.MakePiece(...)` (**L1422**) + `IPlaced.OnPlaced()` + `m_placeEffect` | ✅ (as peças) |

### 2.2 O que dá para reusar (e é a maior parte do trabalho)

- **Mover até o local**: `MHR_Mover` (o mesmo da coleta e do `MHR_Collect`).
- **Coletar o que falta**: `HarvestController` + `MHR_GatherRules` + o teto de ordem (`HC_OrderCap`).
- **Baús**: `MHR_ToolKeeper.UsableChest` (respeita baú privado de outro jogador).
- **Peso**: `CompanionCarry.Max/Fraction` (com o cinto somando 150).
- **Falar**: `CompanionTalk.Say` (balão + TTS), `CompanionLocale` (5 idiomas).
- **Ordens**: `CompanionOrders` + `LlmIntent` + as redes por palavras (`RadialIntent`, `InteractionIntent`).
- **Posse/ZDO**: `MHR_Owner.TakeControl` / `CreatorId` / `MHR_WriteOrQueue`.
- **O executor do jogo**: `Player.PlacePiece(piece, pos, rot, doAttack: false)`.

Ou seja: **80% do caminho já existe**. O que falta é o pedaço que o jogo só faz para o jogador:
**validar o local** e **decidir** (peça, materiais, ponto).

### 2.3 O que **não** dá para reusar

- `TryPlacePiece` / `UpdatePlacementGhost` / `PieceRayTest`: `private` e acoplados a `Player`
  (`m_placementGhost`, `m_placementStatus`, `GameCamera`, `ZInput`). Chamar por reflexão exigiria montar
  um `Player` falso — pior que reescrever as ~20 regras.
- O `PlacePiece` **não** valida nada: ele confia que a posição já foi validada. Quem chama tem que ter
  certeza (§2.5).

### 2.4 A sequência exata do `PlacePiece` (o que a reserva tem que reproduzir)

```
TerrainModifier.SetTriggerOnPlaced(true)
go = Instantiate(piece.gameObject, pos, rot)          // o prefab vem do ZNetScene/PieceTable
TerrainModifier.SetTriggerOnPlaced(false)
go.GetComponentInChildren<CraftingStation>()?.AddKnownStation(...)   // no Player: o jogador aprende a estação
go.GetComponent<Piece>()?.SetCreator(playerId, platformUserId)
go.GetComponent<PrivateArea>()?.Setup(nomeDoJogador)
go.GetComponent<WearNTear>()?.OnPlaced()
go.GetComponent<ItemDrop>()?.MakePiece(sendRPC: true)
foreach (IPlaced p in go.GetComponents<MonoBehaviour>()) p.OnPlaced()
piece.m_placeEffect.Create(pos, rot, go.transform, 1f, -1, zdoId)
go.GetComponent<ZNetView>()?.GetZDO()?.SetCheated(cheated)           // só no modo cheat
AddNoise(50f)
```

O **PlanBuild** (WTFPL) faz isso em `PlanPiece.SpawnPiece`, **sem** `TerrainModifier.SetTriggerOnPlaced`
nem `IPlaced.OnPlaced()`. Nós seguimos o **vanilla** (o `IPlaced` é o que inicializa fogueira, baú, cama,
placa). Mas na rota normal **não precisamos disso**: `Player.PlacePiece` já faz.

### 2.5 Armadilhas confirmadas no código (cada uma já custaria um bug)

| Armadilha | Evidência | Tratamento |
|---|---|---|
| `Piece.SetCreator` **não grava** se o ZDO não for nosso, e **nunca** sobrescreve criador ≠ 0 | `Piece.cs` L429 (`m_nview.IsOwner() && GetCreator() == 0`) | Na reserva: `ClaimOwnership()` **antes** e conferir depois. Na rota `Player.PlacePiece`, o `Instantiate` é do host → o ZDO já nasce nosso |
| Peça sem ZDO (prefab inativo) vira objeto morto no mundo | `ZNetView` só cria ZDO no `Awake` | `nv.IsValid() && nv.GetZDO() != null` ou **destruir e abortar** |
| `m_name` da peça é **token** (`$piece_workbench`) | `Piece.m_name` + `Localization` | falar com `Localization.instance.Localize(piece.m_name)` |
| Peça que exige estação falha **em silêncio** se a estação não estiver no raio | `HaveRequirements` → `CraftingStation.HaveBuildStationInRange` (L271) | validar `m_craftingStation` **no ponto** (é o que o BuildOrders faz no `HelperMayBuild`) |
| `m_spaceRequirement` **não** é "espaço livre" — só vale com `StationExtension` | `UpdatePlacementGhost` | o teste de espaço real é `m_blockRadius`/`m_noClipping` |
| `PlacePiece` **não** valida: coloca em qualquer lugar | L3099 (não checa nada) | a validação é **nossa**, antes de chamar |
| Peça em cima do jogador | `CheckPlacementGhostVSPlayers` (L4195) | recusar candidato a < 0,6 m de qualquer `Player`/companheiro (a regra real é colisão de collider — E2) |
| `CraftingStation.Start()` registra a estação **no frame seguinte** | `CraftingStation.cs` L80 (`Start`) | verificar `HaveBuildStationInRange` só no frame seguinte; no mesmo frame dá `station_not_registered` (medido em 01/11/2026) |

---

## 3. O ecossistema: o que já existe (pesquisa de 31/10–01/11/2026)

### 3.1 Os cinco projetos

| Projeto | O que é | Licença | Dependências | Serve para nós? |
|---|---|---|---|---|
| **Quads Build Orders** ([GitHub](https://github.com/HardHeadHackerHead/valheim-mods/tree/main/mods/BuildOrders) · [Thunderstore](https://thunderstore.io/c/valheim/p/Quads_Lab/Quads_Build_Orders/)) | Fantasmas de construção compartilhados + planejador (F11) + **API** + helpers para "um ajudante construir" | **MIT** | **só BepInEx** | ✅ **é o alvo das construções grandes** |
| **Quad's Companion** ([fonte](https://github.com/HardHeadHackerHead/valheim-mods/tree/main/mods/AICompanion)) | Companion de IA que **já constrói os planos do BuildOrders** (dever "Build our plans") | **MIT** | BuildOrders (opcional) | ✅ **referência de implementação** (e concorrente direto) |
| **PlanBuild** ([GitHub](https://github.com/sirskunkalot/PlanBuild)) | Martelo de planejamento + **totem** que constrói planos com material + `.blueprint` | **WTFPL** | **Jötunn** | ⚠️ só pelo acervo de `.blueprint` |
| **Buildheim** ([GitHub](https://github.com/AugusDogus/Buildheim)) | Blueprint **client-only** com holograma e **autobuild pelo martelo** (usa o caminho vanilla) | **MIT** | Jötunn | ⚠️ referência de UI/posicionamento |
| **BiomeBlueprints** ([Thunderstore](https://thunderstore.io/c/valheim/p/OverDrive/BiomeBlueprints/)) | **353 blueprints prontos** para o PlanBuild, por bioma | — | PlanBuild + Jötunn + HookGen | ⚠️ **conteúdo**, não tecnologia |
| **[valheimians.com](https://www.valheimians.com/)** | Biblioteca/comunidade de builds (arquivos de blueprint) | — | — | ⚠️ conteúdo para o jogador |

### 3.2 O que o BuildOrders dá de graça (e por que ele, e não o PlanBuild)

**API pública, versionada e documentada** (`docs/buildorders-api.md`, `PlanningApiVersion = 1`), GUID
`com.quad.buildorders` (antes `com.dhack.buildorders`):

| Método | Para quê |
|---|---|
| `TryCreateGhostPlan(player, title, prefabs[], positions[], rotations[], out key, out error)` | 1–256 fantasmas em **posições absolutas**; valida tudo **antes** de mudar o mundo (peças disponíveis, receitas conhecidas, alcance de 80 m, wards, `IsInsideNoBuildLocation`) |
| `TryCreateBuildingShell(...)` | o mesmo, até **2.048** peças (uma casa inteira numa chamada) |
| `TryGetGhostAtRay(...)` | qual fantasma está sob a mira |
| `TryRemoveGhostPlan(player, key, out removed, out error)` | desfaz só o que ainda é fantasma |

**E os três helpers para um "ajudante" construir** (publicados por `AppDomain`, sem referência de assembly):

| Chave | Assinatura | O que devolve |
|---|---|---|
| `DHack.BuildOrders.Count` | `(Vector3 centro, float raio) -> int` | quantos fantasmas há perto |
| `DHack.BuildOrders.Next` | `(centro, raio, max) -> string[]` | `"id\|prefab\|x\|y\|z"` das peças **que vão ficar de pé** com o que já existe, na **ordem de suporte** (a mesma do "Build all") |
| `DHack.BuildOrders.Place` | `(string id) -> bool` | constrói aquela peça **agora** (como você, **sem gastar material**) |
| `DHack.BuildOrders.Blockers` | `(centro, raio) -> string[]` | `"kind\|count\|detail"`: peças não desbloqueadas, estação fora de alcance, chão protegido |

O `Place` deles é literalmente `player.PlacePiece(piece, o.Pos, o.Rot, doAttack: false)` — ou seja, **o
executor é o do jogo**; o material é responsabilidade de quem chama.

Além disso, o mod já traz o que a gente ia escrever:

- **`_pieces.json`** em `BepInEx/blueprints`: nome, `min`/`max` (tamanho), `snaps`, `ascend`, `boxes`,
  `material`, `cost`, `station`, `rules`, `health` de **cada peça construível** — é o dado que falta para
  posicionar com precisão;
- **blueprint em JSON** (`{name, anchor, yaw, offset, pieces:[{p,x,y,z,rx,ry,rz,g}]}`) + janela F11 para
  colocar o preview e gerar os fantasmas;
- **toolchain em Python** (`tools/blueprints/tools/`: `blueprint.py`, `shapes.py`, `stability.py`,
  `terrain.py`, `preview.py`, `extract_pieces.py`, mais exemplos: forte, casa, igreja, cozinha) — MIT;
- um **guia de design para IA** (`CLAUDE.md`) escrito justamente "so anyone who installs the mod can have an
  AI assistant design builds for them".

### 3.3 O que o Quad's Companion já faz (e o que **não** faz) — o nosso diferencial

O `mods/AICompanion/Building.cs` (MIT, ~425 linhas) é a **referência de implementação** mais próxima do que
queremos:

- é um **dever de casa** ("Build our plans"): ele olha os fantasmas do BuildOrders dentro do raio da casa,
  pega o próximo que vai ficar de pé (`Next`), anda até lá, **paga** o material (mochila → baús) e chama
  `Place`;
- exige **martelo** na mochila (`Mending.Hammer`) — decisão de design dele, não do jogo;
- quando falta material, vira um **objetivo de coleta** (`Goals.ForNeeds`) e ele vai buscar/ fabricar;
- quando não dá, pergunta a **você** (`Talk.Tell`) e explica com o `Blockers`;
- e tem o "catch up" (enquanto você estava fora, ele construiu N peças).

**O que ele NÃO faz** — e é exatamente a nossa meta:

1. **não recebe a ordem por frase** ("construa uma bancada aqui"): ele só constrói o que **você planejou**
   com o F11;
2. **não procura local**: o fantasma já veio com posição;
3. **não decide a peça nem os materiais a partir do pedido**: ele lê o fantasma;
4. **não confirma com o jogo** que a estação entrou no registro (ele assume que o `Place` deu certo).

Ou seja: **a nossa parte original é a ordem + a decisão + o local**. A parte de "executar um plano grande" já
tem dono no ecossistema — e é melhor usar o dele do que competir.

### 3.4 Quando o PlanBuild (e o resto) importa

- **PlanBuild**: só se o jogador já tem um acervo de `.blueprint` (ou quer os 353 do BiomeBlueprints / os do
  valheimians). Nesse caso o caminho barato é **converter** o `.blueprint` para o JSON do BuildOrders (o
  formato é texto: `prefab;categoria;px;py;pz;rx;ry;rz;rw;info;sx;sy;sz`) — **não** integrar com o PlanBuild.
- **Buildheim**: referência de **UI de holograma/posicionamento** e de "autobuild pelo martelo"; não é
  dependência.
- **Nenhum deles** entra no nosso `.csproj`: a integração é **em tempo de execução** (reflexão/`AppDomain`),
  como o próprio doc do BuildOrders recomenda ("avoid assembly references to custom BuildOrders types").

---

## 4. Arquitetura proposta

### 4.1 Componentes (5 nossos + 1 ponte opcional)

| Arquivo | Papel | Responsabilidade |
|---|---|---|
| `AI/BuildCatalog.cs` | **Recipe Resolver** | nome (5 idiomas) → `Piece` prefab; allow-list; `m_resources`; `IsRecipeKnown` |
| `AI/BuildStock.cs` | **Inventory Manager** | quanto tem (mochila → baús), quanto falta, plano de coleta |
| `AI/BuildSite.cs` | **Placement System** | candidatos + validação data-driven → `(pos, rot, status)` com o motivo |
| `AI/BuildExecutor.cs` | **Building Executor** | `Player.PlacePiece` quando há `Player`; senão a sequência da §2.4 |
| `Jobs/MHR_Build.cs` | **Job / Task Verification** | máquina de estados, teto, falas, verificação pós-colocação |
| `AI/BuildOrdersLink.cs` | **ponte opcional** | `Count`/`Next`/`Place`/`Blockers` por `AppDomain` + a API por reflexão; **nada** se o mod não estiver instalado |

Integração (pontos que já existem):

- `AI/CompanionChatAI.cs` — `ValidActions` + `Contract` (regra e 2 exemplos por idioma) + `Header`.
- `AI/CompanionOrders.cs` — `case "build"` no `Run` e no lado `none` do `HandleResult`.
- `AI/LlmIntent.cs` / `AI/RadialIntent.cs` — aliases e `build` em `PreciseActions`.
- `AI/CompanionAI.cs` — `MHR_Build.Drive(this, dt)` **antes** do `MHR_Collect`/`MHR_Loot`.
- `Jobs/MHR_ToolKeeper.cs` — `MHR_JobRules.OtherOrder` responde `true` enquanto a obra roda.
- `Companion/CompanionSetup.cs` — `IsWorkMode` recusa dverger (tipo de combate).

### 4.2 O fluxo de uma ordem

```
"Maria, construa uma bancada de trabalho aqui"
   │
   ├─ CompanionChatAI  → LlmIntent { action:"build", target:"bancada de trabalho" }
   │     └─ target null/inválido → BuildCatalog.Resolve(palavras do jogador)
   │
   ├─ CompanionOrders.Run → escolhe o companheiro (endereçado, senão o mais próximo)
   │     └─ MHR_Build.Start(cs, piece, siteHint: ponto apontado)
   │
   └─ MHR_Build.Drive (1×/frame, só enquanto ativo)
        Lookup      → BuildCatalog.Resolve          → "não conheço essa peça" (fala)
        CheckStock  → BuildStock.Missing            → 0? FindSite
                                                    → falta? Gather (teto = falta) ou fala
        FindSite    → BuildSite.Find(hint, raio)    → achou? GoTo : "não achei lugar" (fala)
        GoTo        → MHR_Mover até 2 m do ponto
        Place       → BuildExecutor.Place           → falhou? próximo candidato (até 3)
        Verify      → ZNetView + criador + estação  → ok: "Bancada de trabalho construída." (fala+TTS)
        Done        → limpa estado e volta a seguir
```

### 4.3 Regras de implementação (não negociáveis)

1. **Zero alocação por frame** no caminho ocioso (`Drive` sai antes de qualquer `GetComponent`).
2. **Nada de reflexão** para o placement: `Piece`, `WearNTear`, `CraftingStation`, `PrivateArea`,
   `ZNetView` são públicos no assembly publicizado do projeto. Reflexão **só** na ponte do BuildOrders.
3. **Toda string nova é chave de localização** nos **5 idiomas** (`hc_ai_build_*`).
4. **Nenhuma escrita no ZDO do companheiro** para o estado da obra: é memória de sessão (como o
   `MHR_Collect`).
5. **Fail-open**: qualquer exceção → `Cancel` + fala + log, nunca exceção para o `Update` do jogo.
6. **A ponte do BuildOrders é 100% opcional**: sem o mod, tudo funciona; com o mod, as construções grandes
   ganham a ordem de suporte e o executor dele.

---

## 5. Fases (cada uma é um entregável testável)

### Fase 0 — Sonda: parar de adivinhar (½ dia) — ✅ **feita em 01/11/2026**

**Entrega:** comando de console **de dev** `modia_build_probe [prefab] [raio]` (padrão do `modia_tts_teste`) que
imprime, para `piece_workbench` (e o que mais for passado):

- `Piece`: `m_name`, `m_resources`, `m_craftingStation`, `m_canRotate`, `m_spaceRequirement`,
  `m_blockRadius`, **todas** as flags de posicionamento;
- `WearNTear`: `m_supports`, `m_materialType`, `m_noSupportWear`;
- `CraftingStation`: `m_name`, `m_rangeBuild`;
- o veredito de **16 pontos** ao redor do jogador (2 anéis × 8 direções), com status e motivo.

**Saída obrigatória:** `docs/re/building-notes.md` com a ficha da bancada (o mesmo papel do
`docs/re/harvesting-notes.md`). **Sem isso, a Fase 2 é chute.**

**Aceite:** o log mostra a ficha completa e o veredito dos pontos, sem exceção. ✅ (23 linhas no log; 11
válidos de 16; o baú recusado com `needs_station`)

**Resultado medido (o que a fase descobriu, e que muda a E2):**

1. a regra de "espaço" da bancada é **`m_noClipping`** (não `m_spaceRequirement`, que é 0);
2. `WearNTear.m_supports` é lido na **peça de baixo** — a bancada não sustenta nada (não se constrói em cima
   dela). A primeira execução da sonda errou isso e **todos** os 16 pontos voltaram `no_support`;
3. a bancada **não** exige chão (`m_groundOnly = false`): vale piso de madeira;
4. os prefabs são `fire_pit` / `piece_bed02` / `wood_floor` — `piece_firepit` e `piece_bed` **não existem**;
5. `wood_floor` não tem flag nenhuma: o validador tem que aceitar "nada a checar".

**Além do plano:** a sonda virou também as ferramentas `build.inspect` e `build.sites` do MCP (o agente mede
sozinho, sem ler log), e o teste do servidor MCP subiu de 41 para **47 checks** (46 ferramentas).


### Fase 1 — Executor (1 dia) — ✅ **feita em 01/11/2026**

**Entrega:** `AI/BuildExecutor.cs` + comando `modia_build_test <prefab> [auto|direct]` que coloca **uma** peça
1,5 m à frente do jogador (ou no primeiro ponto válido em volta), com o material consumido da mochila do jogador,
sem IA.

**As duas rotas (medidas em jogo, as duas passam):**

| rota | quando | o que faz |
|---|---|---|
| `PlayerPlacePiece` | o `Player` do dono é o local (single/host — o caso normal) | `Player.PlacePiece(piece, pos, rot, doAttack: false)` — vanilla inteiro: `Instantiate`, `AddKnownStation`, `PrivateArea.Setup`, `WearNTear.OnPlaced`, `IPlaced.OnPlaced`, `ItemDrop.MakePiece`, efeito, stats e criador = jogador |
| `Direct` | o `Player` do dono não está carregado aqui, ou teste forçado | a sequência da §2.4 à mão (claim → `SetCreator` → `OnPlaced` → `IPlaced` → `MakePiece` → efeito), criador = `MHR_Owner.CreatorId` |

**Aceite (em jogo, `test3`, 01/11/2026):**

1. ✅ a peça aparece e **funciona** (a bancada entrou no registro de estações do jogo);
2. ✅ `Piece.GetCreator()` = id do jogador; `ZNetView` válido; vida cheia;
3. ✅ **relog** (mata o processo → abre → entra): as bancadas continuam lá, do mesmo criador, com ZDO e a
   estação registrada (`build.pieces` depois do relog: 2 peças, `mine: true`, `zdo: true`, `health: 1`,
   `station: $piece_workbench`);
4. ✅ material consumido **exatamente** `m_resources` (40 → 20 em duas colocações de 10);
5. ✅ log sem exceção; ponto inválido é **recusado antes** de qualquer coisa acontecer;
6. ✅ o log diz a rota usada em cada colocação.

**Duas armadilhas novas, achadas no teste (entraram na §2.5):**

- **`CraftingStation.Start()` registra a estação no frame SEGUINTE** — verificar `HaveBuildStationInRange` no
  mesmo frame do `Instantiate` **sempre** falha (`station_not_registered`). A verificação imediata não checa a
  estação; a diferida (o job, ou o teste 2 frames depois) checa.
- **`MinSpacing` de 1,5 m recusa o óbvio**: 1,5 m à frente do jogador é exatamente a distância do teste, e o
  vanilla coloca bancada do lado do jogador. A regra real é a colisão dos colliders do fantasma
  (`CheckPlacementGhostVSPlayers`); o guarda barato agora é **0,6 m**, e a E2 traz o teste de colisão de verdade.


### Fase 2 — Busca de local (1,5 dia)

**Entrega:** `AI/BuildSite.cs` + `modia_build_find <prefab>` que **não coloca nada**: mostra o ponto
escolhido e o motivo de cada recusa.

**Algoritmo:**

1. candidato 0 = o **ponto apontado** (raycast da câmera, layers da tecla de comando);
2. senão, anéis de 0,5 m a `SiteSearchRadius` (12 m), 8 direções por anel, ordenados pela distância ao hint;
3. por candidato: raycast de cima para baixo (`terrain`/`piece`/`static_solid`) → ponto + normal;
   `Heightmap.FindHeightmap`; as flags da §2.5; `Location.IsInsideNoBuildLocation`;
   `PrivateArea.CheckAccess`; distância de jogadores/companheiros;
4. **teste de encaixe** com o prefab **desativado** (`SetActive(false)`) e `Physics.ComputePenetration`,
   sem tocar no mundo;
5. devolver o primeiro válido **com folga**, no máximo 3 para tentar em ordem.

**Aceite:** numa clareira acha em ≤ 20 candidatos; apontando para água/encosta/parede/ward alheio devolve o
**motivo certo** (`no_ground`, `in_water`, `no_build_zone`, `ward`, `occupied`…).

### Fase 3 — A ordem por chat/voz (1 dia) — **MVP** — ✅ **feita em 01/11/2026**

**Entrega:** `build` no contrato (regra + 2 exemplos em PT, regra+exemplo em EN/ES/FR/IT), `ValidActions`
**35 → 36**, aliases no `LlmIntent`, `build` em `RadialIntent.PreciseActions`, `AI/BuildCatalog.cs` (apelido →
prefab nos 5 idiomas + allow-list), `Jobs/MHR_Build.cs` (Find → Goto → Place → Verify → Done), `case "build"`
no `CompanionOrders`, a rede `CompanionOrders.IsBuildRequest` no lado `none`, o gate dverger, a config
`[Building]` e as 13 chaves de localização nos 5 idiomas.

**Aceite (em jogo, `test3`, 01/11/2026):**

```
[IA] Order from chat: "Maria, construa uma bancada de trabalho aqui"
[IA] The message is addressed to "maria"
[IA] Request #2 -> http://127.0.0.1:1234/v1/chat/completions (model "qwen2.5-3b-instruct")
[IA] Applied "build" to 1 companion(s) in 1085 ms
[IA] Voz (Fase 3.4): falando "Ok: construir Bancada"
[Build] maria: order: build piece_workbench near (-223,8, 77,0, -268,7)
[Build] maria: site at (-223,8, 77,0, -268,7), 0,0 m from the hint, 1 tried (valid x1)
[Build] maria: placed (PlayerPlacePiece), verifying next frame
[Build] maria: done: piece_workbench at (-223,8, 77,0, -268,7)
```

1. ✅ `Maria, construa uma bancada de trabalho aqui` → constrói (o `build.pieces` achou a bancada nova com
   `mine: true` **6 s** depois da mensagem);
2. ✅ o caminho do job sozinho (sem LLM) também passa: `companions.build piece_workbench` → mesma sequência,
   bancada nova em **2 s**;
3. ✅ a linha do chat é a chave localizada (`hc_ai_order_build` → "Ok: construir Bancada"), não a chave crua;
4. ✅ a voz fala a linha do mod e o `reply` do modelo fica só no log (regra da Fase 3.4);
5. ✅ a rede do lado `none` (`IsBuildRequest`: verbo de construir + peça da allow-list + sem `?`) e o gate
   dverger (recusa com a linha "Sou de combate…") estão no lugar;
6. ✅ **peça que exige estação, sem estação por perto** — a recusa tem a **sua** linha (antes o job dizia "não achei
   lugar", que é mentira: faltava bancada, não espaço):

   ```
   [Build] Companheiro 2: order: build piece_chest_wood near (-118,4, 46,5, -164,6)
   [Build] Companheiro 2: needs a station: Bancada is not in range of the spot
   [Build] Companheiro 2: order ended: hc_ai_build_needs_station (station out of range)
   ```

   medido a **140 m** da oficina (`build.pieces radius 45` = 0 bancadas), com 10 madeira na mochila: **nada foi
   construído** (`baus depois: 0`) e **nada foi gasto** — a checagem roda antes de pagar. O motivo agora aparece
   também no log (`order ended: <chave> (<motivo>)`), que é como o teste prova qual linha saiu;
7. ✅ **teste do JOGADOR (01/11/2026):** voz (push-to-talk) **funciona**, fogueira **funciona**, e a **cama**
   respondeu *"não sei construir isso"* — o gate de receita (`IsRecipeKnown`) estava certo (`build.inspect`
   mostra `recipeKnown: false` para a `piece_bed02` neste perfil), mas a **mensagem** confundia: parecia que o
   companheiro não conhecia a peça. Agora há uma linha própria, `hc_ai_build_recipe_locked` ("Ainda não
   desbloqueamos essa receita: falta a {0}."), nos 5 idiomas, e o `companions.build` do MCP aplica o **mesmo**
   gate. Dverger (item 4) e os ajustes de mira/idioma (5 e 6) ficaram para depois, por decisão do jogador.
   **O catálogo estava errado:** existem DUAS camas — `bed` ("Cama", **Madeira x8**, receita **destravada**) e
   `piece_bed02` ("Cama de dragão", 40 de madeira de qualidade + peles + pregos, travada). O alias "cama" caía na
   cara. Corrigido: `bed` é a entrada de "cama"/"bed"/"lit"/"letto" e `piece_bed02` só responde a "cama de
   dragão"/"dragon bed". Medido em jogo: `[Build] ... done: bed at (-275,5, 81,7, -301,9)` e `camas depois: 2`.
8. 👤 **voz (push-to-talk): teste do JOGADOR** (não é pendência de código). O caminho é o **mesmo** `CompanionChatAI.Submit` do chat (nada de
   lógica duplicada) e a voz foi validada em jogo nas Fases 3.3/3.3b/3.3d, mas o agente **não tem microfone**: falar
   "Maria, construa uma bancada de trabalho aqui" segurando `V` é o único passo que fica com o jogador.

**Dois bugs que só o teste em jogo pegou:**

- **o fantasma colidia consigo mesmo**: `Transform.IsChildOf(self)` é `false`, então um collider no **root** do
  prefab era comparado com os colliders do próprio fantasma e **todo** candidato virava `clipping` (44 de 48
  recusados na primeira ordem). Corrigido com `other.gameObject == ghost || other.transform.IsChildOf(...)`;
- **`CraftingStation.Start()` registra no frame seguinte** (achado na E1): a verificação da estação é diferida.

**Ainda pendente de propósito (E4+):** coletar o que falta, checar baús, a distância da casa no `Build()` e o
teste em jogo de baú/fogueira/cama.

### Fase 4 — Coletar o que falta (1 dia) — ✅ **feita (E4b: a coleta é uma ordem separada)**

**O que entrou (medido, gate 9/9):** `AI/BuildStock.cs` — o custo sai da **mochila e depois dos baús usáveis**
(`MHR_Chests.Usable`: baú que um jogador construiu, dentro de ward que deixa passar, não privado de outro, não
aberto), **tudo ou nada** (claim antes de mexer, como manda o `MHR_Chests.Claim`); `[Building] UseChests`,
`RequireHammer` (com a linha `hc_ai_build_no_hammer`), `Quiet` (sem o `AddNoise` do caminho próprio) e o
**`MaxDistanceFromHome` barrando a ordem** no despacho (linha `hc_ai_build_too_far`). CONFIGURATION.md 349/25.

**O que foi REVERTIDO (e por quê):** a coleta **dentro** do job. A primeira versão desligava o Follow, andava até
o recurso e deixava o `HarvestController` dirigir enquanto o `MHR_Build` voltava a dirigir depois — **dois sistemas
no mesmo `MHR_Mover`**, e o movimento do companheiro ficou todo embaralhado (relato do jogador, 01/11/2026). O
`MHR_Build` voltou ao estado da E3 (`b0d4db8`) + o `BuildStock`; as chaves `GatherForBuild`/`GatherRadius` saíram.

**E4b (feita em 01/11/2026):** a coleta é uma **ordem separada**. Quando falta material e `[Building] GatherForBuild`
está ligado, a ordem de construção **encerra** com `hc_ai_build_collecting` ("Vou buscar X"), dispara a **ordem de
coleta normal** (`ApplyActionMode` + `SetOrderCap` = exatamente o que falta) e deixa um **vigia** (`s_pending`)
olhando a mochila — o vigia **não dirige**: só quando o custo está coberto ele limpa a coleta, volta ao modo Follow
e **começa uma ordem de construção nova** (que reencontra o local e caminha de volta). Um dono do `MHR_Mover` por
vez, sempre. Se o jogador trocar o trabalho, o companheiro morrer ou passarem 300 s, o pendente é esquecido.

**Medido em jogo (01/11/2026):**

```
[Build] Companheiro 2: missing: Madeira x3
[Build] Companheiro 2: gathering 3x $item_wood first (mode 1) - the build resumes after it
[Build] Companheiro 2: order ended: hc_ai_build_collecting (gathering first)
[Build] Companheiro 2: materials ready - resuming the build
[Build] Companheiro 2: order: build piece_workbench near (-283,0, 81,8, -303,7)
[Build] Companheiro 2: site at (-283,0, 81,7, -303,7), 2 tried (clipping x1, valid x1)
BANCADA NOVA em 136 s (3 madeiras coletadas + caminhada + colocação)
```

**Chave:** `[Building] GatherForBuild` (true) — desligada, a ordem só diz o que falta e encerra.

**Raio e caminhada (relato do jogador: "o raio de busca está muito baixo"):** a coleta do jogo mede o raio **do
jogador** enquanto o companheiro segue (25 m), então uma árvore a 54 m ficava fora. Agora `[Building] GatherRadius`
(**120 m**, máximo **200**) procura o recurso; se ele estiver a mais de **20 m**, o **próprio job caminha** até lá
em modo Follow (a coleta está ociosa — só um dono do `MHR_Mover`) e **só ao chegar** passa o controle à coleta.

**Aceite (mochila VAZIA, 01/11/2026):** companheiro novo com `AxeBlackMetal` + `PickaxeBlackMetal`, **0 madeira**:

```
[Build] Companheiro 2: missing: Madeira x10
[Build] Companheiro 2: gathering 10x $item_wood (mode 1) - the build resumes after it
[Build] Companheiro 2: order ended: hc_ai_build_collecting (gathering first)
[Build] Companheiro 2: materials ready - resuming the build
[Build] Companheiro 2: site at (-249,1, 83,9, -220,0), 1 tried (valid x1)
BANCADA NOVA em 96 s (0 -> 3 -> 6 -> 10 madeiras coletadas, volta e colocação)
```

**Movimento (a regressão que forçou a reversão):** medido com a ordem de coleta — o companheiro andou **22,3 m**
(de 30,6 m para 8,6 m do jogador) sem travar, e depois seguiu normalmente.

**Sem recurso no raio (o outro ramo do aceite):** com `GatherRadius = 10` de propósito, a ordem **recusa na hora**
— não entrega a coleta e não espera os 300 s:

```
[Build] missing: Madeira x10
[Build] no resource within 10 m - refusing
[Build] order ended: hc_ai_build_missing
```

E se a coleta **começar** mas não terminar em 300 s, o vigia agora **fala** o que falta (`hc_ai_build_missing`)
antes de encerrar (antes só escrevia no log).

**`MaxDistanceFromHome` exercitado** (home no companheiro, `MaxDistanceFromHome = 1` de propósito):
`{"ok": false, "error": "the spot is 4 m from the companion's home (max 1 m)"}` — a coleira barra a ordem
(no jogo a linha é `hc_ai_build_too_far`).

#### O texto original da fase (para referência)

**Entrega:** `BuildStock` ligado ao `HarvestController` com **teto = o que falta** (`HC_OrderCap`), volta e
constrói. `[Building] GatherForBuild` desliga.

**Aceite:** com **0 madeira** e árvores por perto, coleta 10, volta e constrói; sem árvores, fala "faltam 10
de madeira" e cancela (sem loop).

### Fase 5 — A oficina mínima (1 dia)

**Entrega:** baú, fogueira e cama no catálogo; as particularidades saem do validador (baú e cama exigem
bancada no raio; fogueira exige chão; cama precisa de espaço).

**Aceite:** 4 ordens seguidas (`bancada`, `baú`, `fogueira`, `cama`) montam uma oficina funcional, sem
nenhuma falhar por posicionamento.

> **"Funcional" tem dois sentidos - medido no decompilado (01/11/2026, ver `docs/re/building-notes.md` §9):**
> **colocar** peça só precisa da estação **no raio** (`HaveBuildStationInRange`, sem telhado); **craftar item** na
> bancada passa por `CheckUsable`, que exige `underRoof` e **70% de cobertura**. Então a E5 prova as **quatro peças
> colocadas** (a bancada valendo de estação para baú e cama); o telhado - a oficina **usável** - é a **E6**, e o
> primeiro template dela é o **abrigo da bancada**.

### Fase 6 — Estruturas pequenas por **template** (2–3 dias)

**Entrega:** ordem com plano (`constrói um chão 2x2 e quatro paredes aqui`) → lista de peças **relativas** ao
âncora + **snap** (`Piece.GetSnapPoints`) + ordem topológica (fundação → parede → telhado) + checagem de
suporte (`WearNTear.GetSupport`).

**Aceite:** 2x2 de piso + 4 paredes + telhado, tudo encaixado, sem peça "solta no ar". O **primeiro** template é o
**abrigo da bancada** (o telhado que o `CheckUsable` exige para craftar - §9 das notas de construção), que é o que
fecha o sentido "usável" da E5.

> A partir daqui a pergunta "template ou plano do jogador?" já está respondida pelas duas fases seguintes: o
> template é nosso (curto e previsível); o plano grande é do BuildOrders.

**Implementada em 01/11/2026 (medida em jogo):**

- **`AI/BuildTemplates.cs`** (puro, no harness - seção (j)): os templates em **coordenadas relativas ao âncora**
  (prefab + x/y/z + yaw + nível) e `Ordered()` (fundação → parede → telhado). Os deslocamentos saem dos **pontos de
  encaixe** medidos (`build.snaps`): piso com a origem no centro e o topo em y = 0, parede com a origem no centro
  (1 m acima do piso), telhado 45° subindo 2 m em 2 m (beiral 1 m fora, cumeeira 1 m dentro) - a tabela está em
  `docs/re/building-notes.md` §11.
- **`hut`** = 1 piso + 4 paredes + 2 telhados (7 peças, o aceite) e **`canopy`** = 4 postes + 4 telhados (8 peças,
  o abrigo da bancada).
- **`MHR_Build` em modo plano:** `FindAnchor` valida o **template inteiro** em cada candidato (o âncora é do
  template, não da 1ª peça), o material do plano é checado **antes** da primeira peça, e cada passo passa por
  `Goto`/`Place`/`Verify` como uma peça única - com o prazo **por peça**.
- **Apoio ("sem peça solta no ar"):** `AI/BuildSupport.cs` lê o veredito do **próprio jogo**
  (`WearNTear.UpdateSupport`/`HaveSupport` por reflexão, fail-open) depois de cada peça; o `BuildProbe.CheckPoint`
  ganhou o teste de **pegada** (caixas crescidas 0,15 m procurando o chão ou uma peça que apoia) para peças acima
  do chão, e um **`ignore`** para o companheiro não bloquear a própria peça.
- **O companheiro constrói sozinho** (decisão do jogador): o executor não usa mais o `Player.PlacePiece` para ele.
- **Aceite:** 7/7 peças `supported`, todas `placed (Direct)`; o log completo está em
  `docs/re/building-notes.md` §11. **O abrigo fecha o "usável" da E5:** a bancada sob o `canopy` responde
  `usable=True cover=0.94 underRoof=True` (o `CheckUsable` do jogo) e as descobertas ao lado `usable=False
  cover=0.18`.
- **Materiais (desenho do jogador, 01/11/2026):** o plano **começa com o que tem** na mochila e **paga peça por
  peça** (mochila → baús usáveis). Quando uma peça fica sem material, a obra **entrega a ordem à coleta** (o mesmo
  repasse da E4b: `Pending.Template`) e **volta sozinha** quando o total do plano estiver coberto. Nada de exigir o
  custo inteiro antes de começar - numa construção grande isso não faz sentido.
- **Limitações:** o chão tem que ser razoavelmente plano (o nivelamento é a **E10** e agora é visível: a obra entrega
  ao job de nivelar e retoma).

**E6b - a casa simples (pedido do jogador, 01/11/2026):** `"construa um abrigo aqui"` levanta uma **casa 8x8** com
**piso, paredes, teto, uma porta e uma janela na frente** (56 peças: 16 pisos, 14 paredes, 1 porta, 1 meia-parede de
janela, 16 telhados e 8 empenas).

- **Por que 8x8:** o telhado 45° sobe 2 m em 2 m, então a água de 4 m de uma casa de 8 m dá **exatamente 2 peças** e
  a **cumeeira fica no meio**. Numa casa de 6 m seriam 1,5 peças por água e a cumeeira cairia 1 m fora do centro.
- **A porta** (`wood_door`, Madeira x4) é 2x2 com a **mesma origem da parede** - ela **toma o lugar** de um tile.
  **A janela** é uma `wood_wall_half` (2x1, Madeira x1) a **0,5 m do piso**, deixando 1 m de abertura em cima.
- A **frente é o lado local -z** (o lado que o jogador encara): porta no tile da esquerda do centro e janela no da
  direita (medido: `build.snaps` da porta = (±1, ±1, 0), o mesmo da parede).
- **Aceite em jogo (01/11/2026):** **57/57 peças, todas `supported`** no terreno que o jogador nivelou - a
  bancada da casa, 16 pisos, 14 paredes, a janela (19/57), 16 telhados (a fileira de cima a 4,9 m dos pés), 8
  empenas e a **porta em último** (57/57). `hc_ai_build_plan_done`.
- **Quatro lições do teste:** (1) a **porta é a última** - com ela no lugar o companheiro fica trancado dentro e
  não alcança o telhado; (2) o alcance é o do jogo (**5 m do olho**, `Player.m_maxPlaceDistance`), não 3 m acima
  dos pés - a fileira de cima do telhado ficava inalcançável; (3) a **casa traz a própria bancada** como passo 0
  (a estação vem do plano; uma bancada do jogador no meio do terreno atrapalha o piso); (4) **madeira pesa 2** -
  160 unidades = 320/300 e o companheiro não anda. Detalhes em `docs/re/building-notes.md` §12.
- **Duas falas novas que o teste da casa achou:** `hc_ai_build_cant_reach` ("não consigo chegar até a peça") no
  lugar do "não achei lugar" quando o `MHR_Mover` desiste - o local está bom, o caminho não - e
  `hc_ai_build_too_heavy` ("carregado demais para construir (peso/máximo)") quando o companheiro está acima do
  limite (ele não anda; antes a ordem falhava com "não achei lugar").

### Fase 10 - Terraformar (implementada em 01/11/2026)

**Entrega:** a ordem própria **
ivele um terreno 10x10 aqui** (AI/LevelIntent.cs puro + Jobs/MHR_Level.cs) e o
nivelamento automático antes de uma estrutura (a fase Level do MHR_Build).

- **Como o jogo nivela:** não existe level duro no 1.0 - o Nivelar da enxada é um **smooth** (mud_road_v2,
  raio 3, potência 3) que puxa o terreno para a altura do clique com queda por distância e **±1 m por operação**.
- **A regra:** grid **espaçado pelo raio** (sobrepor operações as faz brigar: o grid de 1,5 m divergiu 0,55 → 2,01 m),
  alvo na **mediana**, até **10 passadas** ou a tolerância ([Building] LevelTolerance, 0,2 m), e recusa acima de
  **1,2 m** de variação (hc_ai_build_too_steep).
- **Cara de jogador:** o companheiro **não anda até cada ponto** (o mover desiste e ele treme no lugar - medido); ele
  vai ao **centro** da área, vira para cada ponto, bate a enxada (animação) e a operação cai 0,45 s depois.
- **Aceite medido:** 8 m com 1,06 m de variação → **0,33 m em 3 passadas, 165 s** (hc_ai_level_done).
- **Cancelamento:** é ordem de um tiro (qualquer outra ordem a derruba) - sem isso o companheiro ficava travado.
- **A casa NÃO nivela em lote (pedido do jogador):** ela entrega o serviço ao `MHR_Level` (a mesma ordem "nivele um
  terreno") e retoma sozinha quando o chão fica plano (`the ground is ready - back to the house`) - o nivelamento é
  **visível**, com o companheiro batendo a enxada ponto por ponto. Medido: 1,07 → 0,59 → 0,34 → 0,21 → **0,13 m em 4
  passadas** e então 57/57 peças.
- **O raise foi testado e abandonado:** as operações de "Erguer" se sobrepõem e um terreno de 0,84 m saiu 4,13 m mais
  alto (ver `docs/re/building-notes.md` §13). O nivelamento é o soft level, com a régua em **0,35 m** para a casa e
  **0,6 m** para a ordem ("bom o suficiente").

---


### Fase 7 — O companheiro constrói os planos do BuildOrders (2–3 dias) — **a ponte**

**Entrega:** `AI/BuildOrdersLink.cs` + o dever "construir os planos da casa" (ou uma ordem `build plans`):

1. **sem** o BuildOrders instalado: a fase inteira não faz nada (nem log de erro);
2. **com** ele: `Count`/`Next`/`Place`/`Blockers` por `AppDomain` (sem referência de assembly), pagando o
   material da mochila + baús (`BuildStock`), e falando o motivo quando não dá;
3. **reuso do que já existe**: `MHR_Mover` para andar até a peça, `CompanionTalk` para as falas, o
   `[Building] MaxDistanceFromHome` como coleira.

**Aceite:** o jogador planeja 20 peças no F11, o companheiro constrói todas (ou explica o que falta), sem
gastar material do jogador, e a estrutura fica de pé (a ordem de suporte vem do `Next`).

### Fase 8 — O companheiro **gera** o plano (3–5 dias, opcional)

**Entrega:** a partir de um **blueprint JSON** (do jogador, do assistente dele ou de um template nosso),
chamar `TryCreateGhostPlan`/`TryCreateBuildingShell` (até 2.048 peças) com as poses que **nós** calculamos —
e então a Fase 7 constrói.

- As poses saem do `_pieces.json` (tamanho/snap/custo/estação de cada peça) + `BuildSite` para o âncora.
- Sem o BuildOrders: o mesmo blueprint é executado pelo nosso `BuildExecutor`, peça por peça, com a nossa
  ordem topológica (mais lento, menos robusto — mas funciona sozinho).

**Aceite:** "Maria, construa a casa do arquivo `casa.json` aqui" → os fantasmas aparecem no lugar certo e ela
constrói; sem BuildOrders, ela constrói direto, peça por peça.

### Fase 9 — Acervo de terceiros (opcional, só se o jogador pedir)

Converter `.blueprint` do PlanBuild (texto) para o JSON do BuildOrders, para usar o acervo do **BiomeBlueprints**
(353 casas) e do **valheimians**. É **conversão de arquivo**, não integração de mod.

---

## 6. Regras de produto

| Tema | Decisão proposta |
|---|---|
| **"aqui"** | ponto apontado (50 m, layers da tecla de comando) → 3 m à frente do jogador → em volta do companheiro. Sem "aqui": perto da casa |
| **Dono da peça** | **o jogador** (`MHR_Owner.CreatorId`; na rota `PlacePiece` o criador já é ele) |
| **Precisa de martelo?** | **Não** (`[Building] RequireHammer = false`). O `PlacePiece` não exige; o Quad's Companion exige por design dele, não por regra do jogo |
| **Receita conhecida?** | **Sim**: `Player.IsRecipeKnown(piece.m_name)` (o gate que o BuildOrders usa). Construir o que o jogador não desbloqueou seria cheat |
| **Estação exigida** | quem decide é `Piece.m_craftingStation` + `HaveBuildStationInRange` (nada de tabela nossa) |
| **Material** | mochila → baús usáveis → coleta (se `GatherForBuild`) |
| **Limites** | 1 obra por ordem; `MaxPiecesPerOrder` (20) na Fase 6; `MaxDistanceFromHome` (40 m); nunca em `Location.m_noBuild`, ward alheia, água ou a < 1,5 m de alguém |
| **Recusa** | fora da allow-list, sem receita, sem material e sem coleta, sem local, ou tipo de combate → **uma** fala clara e volta a seguir |
| **Multiplayer** | **congelado** nesta rodada (decisão do jogador). Risco documentado: `PlacePiece` credita `Game.instance.GetPlayerProfile()` (o perfil **local**) e o `PrivateArea.Setup` usa o nome local |
| **Remover peça** | fora de escopo |

---

## 7. Configuração e localização

**Seção nova `[Building]`** (8 chaves, em inglês no `.cfg`, como desde a 1.8.3):

| Chave | Padrão | Para quê |
|---|---|---|
| `Enabled` | `true` | liga/desliga |
| `GatherForBuild` | `true` | coletar o que falta (Fase 4) |
| `UseChests` | `true` | procurar material nos baús |
| `SiteSearchRadius` | `12` | raio da busca de local |
| `MaxDistanceFromHome` | `40` | coleira da obra |
| `MaxPiecesPerOrder` | `20` | teto da Fase 6 |
| `RequireHammer` | `false` | exigir martelo na mochila |
| `Quiet` | `false` | não chamar `AddNoise` (não atrai mob) |

**Chaves de UI/fala novas** (`hc_ai_build_*`, ~10, nos 5 idiomas): `order_build`, `no_recipe`,
`not_allowed`, `missing` (`{0}` de `{1}`), `collecting`, `no_site`, `needs_station`, `done` (`{0}`),
`failed`, `plans_done`.

> **Contagens que mudam** (AGENTS, CHANGELOG, wiki ×5, release notes): `ValidActions` **35 → 36**, configs
> **343 → 351** (24 → 25 seções), chaves de UI **1001 → ~1011**. O gate `tools/release-check.ps1` cobre
> traduções e `CONFIGURATION.md`; o `ai-intents-check` ganha os casos do `BuildCatalog`/`RadialIntent`.

---

## 8. Riscos e mitigações

| Risco | Prob. | Impacto | Mitigação |
|---|---|---|---|
| Flags da bancada diferentes do esperado | média | alto | **Fase 0** antes de qualquer regra |
| `PlacePiece` coloca em local inválido (ele não valida) | alta | alto | a validação é nossa e roda **antes**; `Place` só com `status == valid` |
| Peça nasce sem dono (só na rota reserva) | média | alto | claim → `SetCreator` → **conferir** `GetCreator()`; se 0, destruir e falhar |
| Peça "fantasma" (sem ZDO) | baixa | alto | `nv.IsValid()` + `GetZDO() != null`; senão `Destroy` |
| Busca de local cara (raycasts) | média | médio | 1× por ordem, máx. N candidatos; nunca por frame |
| Estrutura sem suporte (Fase 6) | alta | médio | ordem topológica + `WearNTear.GetSupport()`; recusar e reportar |
| `AddNoise(50)` atrai mob | média | baixo | `[Building] Quiet` |
| Job preso | média | alto | teto de 30 s, "tempo não dirigido não conta", fala final |
| BuildOrders muda a API/helpers | baixa | médio | ponte **opcional** e tolerante (`PlanningApiVersion`, `try/catch`, `Blockers` para o motivo); sem referência de assembly |
| PlanBuild/BiomeBlueprints exigem Jötunn | alta | baixo | não são dependência; só conversão de arquivo, na Fase 9 |
| Dois companions construindo (nosso + Quad's) | baixa | baixo | não é nosso problema; documentar |
| MP: posse/criador | alta | alto | **fora de escopo**, documentado |

---

## 9. Como testar (receita com o DevBridge)

```
1. [Advanced] DevBridge = true, entrar no mundo (world_enter roda devcommands/tod/god/heal)
2. companions.spawn                      → a Maria existe
3. companions.give Wood 10               → material na mochila dela
4. mirar no chão e digitar: "Maria, construa uma bancada de trabalho aqui"
5. companions.zdo                        → estado do companheiro (modo, ordem)
6. screenshot                            → evidência visual
7. LogOutput.log: [Build] ... (uma linha por transição)
```

**Fase 7 (BuildOrders):** planejar 20 peças no F11 perto da casa, dar a ordem (`build plans`), conferir que
o contador do `Count` cai e que as peças ficam de pé; sem o mod instalado, a ordem responde "não sei construir
planos" e nada mais muda.

**Regressão:** com `[Building] Enabled = false`, o `ValidActions` continua com 36 mas a ordem responde "não sei
construir" — nada mais muda no mod.

---

## 10. Decisões abertas (com o meu voto)

1. **"aqui" = o que eu aponto?** → **sim** (S7).
2. **Coletar já na 1ª entrega?** → **não**: E3 constrói com o que tem, E4 logo em seguida.
3. **Exigir martelo?** → **não**.
4. **Allow-list ou qualquer peça do martelo?** → **allow-list** até a E6 + gate `IsRecipeKnown` sempre.
5. **BuildOrders ou PlanBuild para as construções grandes?** → **BuildOrders** (API, MIT, sem Jötunn, e o
   companion dele já prova o fluxo). PlanBuild só como **fonte de arquivo**, na Fase 9, se o jogador quiser.
6. **Quem desenha as construções grandes?** → **o jogador** (F11 / blueprint) nas Fases 7–8; template nosso
   para as pequenas (E6). **Não** colocar o LLM para desenhar geometria agora (caro, lento e frágil).
7. **Nome da ação**: `build` (token do schema) com PT `construa/constrói/levanta/ergue/faz uma`; ES
   `construye/levanta`; FR `construis/construire`; IT `costruisci`.

---

## 11. Esforço e ordem de trabalho

| Fase | Estimativa | Depende de |
|---|---|---|
| 0 — sonda + `docs/re/building-notes.md` | ½ dia | — |
| 1 — executor (2 rotas) | 1 dia | 0 |
| 2 — busca de local | 1,5 dia | 0, 1 |
| 3 — ordem chat/voz | 1 dia | 1, 2 |
| 4 — coleta | 1 dia | 3 |
| 5 — baú/fogueira/cama | 1 dia | 3 |
| 6 — estruturas por template | 2–3 dias | 2, 5 |
| 7 — ponte BuildOrders (construir planos) | 2–3 dias | 1, 2 |
| 8 — gerar plano de um JSON | 3–5 dias | 7 |
| 9 — conversão de `.blueprint` | 1–2 dias | 8 |

**MVP (0–3) ≈ 4 dias.** **"Oficina + planos do jogador" (0–7) ≈ 10 dias.**

**Paralelismo (regra 10 do `AGENTS.md`):** a Fase 0 é bloqueante e de um agente só. Depois: `BuildCatalog` +
localização (docs-writer) em paralelo com `BuildExecutor` (ai-npc); o harness do `ai-intents-check` (qa) em
paralelo com a Fase 2; a ponte do BuildOrders (Fase 7) pode começar assim que a Fase 1 fechar, porque não
depende da busca de local. O orquestrador revisa o diff, roda build + gate e commita.

---

## 12. Critérios de aceite da entrega (a bancada)

1. Com **10 madeira** na mochila e espaço livre, `Maria, construa uma bancada de trabalho aqui` coloca a
   bancada a **≤ 5 m** do ponto apontado, em **≤ 3 s**, e fala `hc_ai_build_done`.
2. Sem material e com `GatherForBuild`, ela coleta, volta e constrói; sem material e sem coleta, fala quanto
   falta e cancela.
3. Apontando para **água / encosta / parede / ward alheio**, escolhe outro ponto no raio; se não houver,
   `hc_ai_build_no_site` (com o motivo no log).
4. A bancada é **do jogador**, tem `ZNetView` válido e **abre a janela de craft**.
5. **Relog**: continua lá, do mesmo dono, funcionando.
6. Log sem exceção; **0 alocação por frame** com a obra ociosa; `[Building] Enabled = false` desliga tudo sem
   tocar no resto do mod.
7. Build **0/0**, gate `release-check.ps1` **PASS**, `ai-intents-check` com os casos novos **PASS**.

---

## 13. O que **não** está neste plano (de propósito)

- **Multiplayer dedicado** (posse do ZDO, criador quando o dono não está online).
- **Remover/editar** peças existentes.
- **Terraformar** — **aceito no escopo em 01/11/2026** (decisão do jogador): entra como **E10**, depois da E6 —
  nivelar o terreno antes de colocar a peça (reusando o que o BuildOrders já faz nos planos dele: `TerrainCompiler`,
  o mesmo que a enxada usa). Hoje o nosso template não nivela.
- **LLM desenhando geometria** (blueprint por prompt) — caro e frágil; o desenho é do jogador/assistente dele.
- **Qualquer dependência nova no `.csproj`** (nem Jötunn, nem PlanBuild, nem BuildOrders).

---

## 14. Fontes (pesquisa)

- **BuildOrders** — API: `docs/buildorders-api.md`; helpers: `mods/BuildOrders/Plugin.Helpers.cs`
  (`HelperPlace` → `player.PlacePiece(piece, o.Pos, o.Rot, doAttack: false)`); estabilidade:
  `mods/BuildOrders/Stability.cs`; blueprints: `mods/BuildOrders/Plugin.Blueprints.cs` + `tools/blueprints/`
  (MIT). Thunderstore 1.14.7, dependência **só** do BepInEx.
- **Quad's Companion** — `mods/AICompanion/Building.cs` (o dever "Build our plans"), `Work.cs`, `Duties.cs`,
  `Goals.cs` (MIT).
- **PlanBuild** — README + `PlanBuild/Plans/PlanPiece.cs` (`SpawnPiece`), `Blueprints/PieceEntry.cs`
  (formato `.blueprint`), `LICENSE` = **WTFPL**.
- **Buildheim** — README ("build it with your normal hammer") + `src/Buildheim/Client/AutoBuilder.cs`
  (MIT).
- **BiomeBlueprints** — Thunderstore (353 blueprints para PlanBuild; depende de PlanBuild 0.18.4 + Jötunn).
- **valheimians.com** — biblioteca/comunidade de builds.
- **Valheim 1.0.20** (decompilado local): `Player.TryPlacePiece` L3041, `Player.PlacePiece` L3099,
  `Player.UpdatePlacementGhost` L3793, `Player.HaveRequirements` L2838, `Player.ConsumeResources` L2894,
  `Player.IsRecipeKnown` L5233, `Player.PieceRayTest` L4227, `Piece.SetCreator` L429,
  `WearNTear.OnPlaced` L442, `CraftingStation.HaveBuildStationInRange` L271,
  `PrivateArea.CheckAccess` L425, `Location.IsInsideNoBuildLocation` L167,
  `Heightmap.FindHeightmap` L1124 / `FindBiome` L1147, `ZoneSystem.GetGroundHeight` L2738,
  `ItemDrop.MakePiece` L1422.
