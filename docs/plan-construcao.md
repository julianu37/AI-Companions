# Plano — construção autônoma pelos companions (Trilha E)

> **Status: planejado** (31/10/2026). Nada implementado ainda — este documento é a proposta a ser
> aprovada antes de codar. Base: Valheim **1.0.20** (Unity 6) e o código do mod na **1.9.5**.
>
> **Escopo da 1ª entrega:** a **bancada de trabalho** (`piece_workbench`). O resto (baú, fogueira,
> cama, estruturas pequenas e, talvez, PlanBuild) vem nas fases seguintes — cada uma é um passo
> testável em jogo, não um "big bang".

---

## 0. A meta em uma frase

O jogador diz **"Maria, construa uma bancada de trabalho aqui"** e, alguns segundos depois, existe uma
bancada de trabalho no chão, **dele** (criador = jogador), funcionando como estação de craft — com o
companheiro tendo resolvido sozinho: receita → materiais (baú/coleta) → local válido → colocação →
confirmação.

O valor não é a bancada: é que a mesma máquina de estados serve para **qualquer peça**. Quando ela
monta sozinha uma oficina (bancada + baú + fogueira + cama), o companheiro deixa de ser "mão de obra"
e passa a ser **construtor**.

---

## 1. As sugestões, em resumo (o que eu faria e por quê)

| # | Sugestão | Por quê |
|---|---|---|
| **S1** | **Não tentar reusar `Player.TryPlacePiece`.** Escrever um validador e um executor **do companheiro**. | `TryPlacePiece` (L3041), `UpdatePlacementGhost` (L3793) e `PieceRayTest` (L4227) são `private`, presos à **câmera** e ao `m_placementGhost`, e existem só em `Player`. O companheiro é `Humanoid`. |
| **S2** | **Validador dirigido pelos campos do `Piece`**, nunca por uma tabela nossa de "o que a bancada precisa". | São ~20 flags (`m_groundOnly`, `m_groundPiece`, `m_noInWater`, `m_notOnWood`, `m_onlyInBiome`, `m_noClipping`…). Uma regra por peça envelhece; ler as flags funciona para peça vanilla, de mod e do PlanBuild. |
| **S3** | **Fase 0 é uma sonda, não código de gameplay**: comando `modia_build_probe <prefab>` que imprime flags + receita + estação + suporte e testa N pontos. | As flags da `piece_workbench` **não estão publicadas em lugar nenhum** (nem no Fandom, nem em doc de modding): estão no bundle do jogo. Sem a sonda, a gente adivinha. |
| **S4** | **O problema difícil é achar o local, não instanciar a peça.** Busca em anéis (0,5 m → `SiteSearchRadius`), com o ponto apontado como primeiro candidato. | `Instantiate` + 6 chamadas é meia hora de código (o PlanBuild faz isso em produção). Achar um ponto válido em terreno irregular, com construção em volta, é o que dá trabalho. |
| **S5** | **Ordem nova `build`, com `target` = a peça** (`ValidActions` 35 → 36), e a peça também sai das **palavras do jogador** quando o modelo devolve `target: null`. | É exatamente o padrão que já funcionou na 3.6b (`drop`/`equip`) e na 2.0 (as 5 interações). Modelo pequeno erra o alvo; a rede por palavras salva. |
| **S6** | **"aqui" tem semântica fixa**: o que o jogador **aponta** (mesmo raio da tecla de comando, 50 m) → senão **3 m à frente dele** → senão em volta do companheiro. Sem "aqui", perto da casa. | O jogador já conhece esse "aponte e mande" da tecla de comando (`CompanionInteract.PointedCollider`). Inventar outra semântica confunde. |
| **S7** | **Material: mochila → baús → coleta.** Nunca uma tabela de custo nossa: o custo é `Piece.m_resources`. | O jogo já tem `Inventory.CountItems` e o mod já tem baú usável (`MHR_ToolKeeper.UsableChest`) e coleta com teto (`HC_OrderCap`). |
| **S8** | **A verificação é do jogo, não nossa**: além do `ZNetView`/criador, conferir que a **`CraftingStation` entrou no registro** (`CraftingStation.HaveBuildStationInRange`). | "A bancada existe" ≠ "a bancada funciona". O registro é o que faz a janela de craft abrir. |
| **S9** | **Fail-open com teto de tempo** (30 s por ordem, como o `MHR_Collect`), uma tentativa de coleta e uma de local, e **sempre** uma fala final. | Job que trava é pior que job que falha. |
| **S10** | **PlanBuild só como fonte de blueprint, sem dependência.** Ler o `.blueprint` (texto) e construir nós mesmos; **não** referenciar a DLL. | O `Blueprint` do PlanBuild é `internal` (não tem API para outros mods) e ele exige **Jötunn**, que este mod **não** usa. A licença é **WTFPL** — dá para estudar/adaptar o executor dele. |

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
| A peça nascer "de verdade" | `Instantiate` + `WearNTear.OnPlaced()` (**L442**) + `Piece.SetCreator(...)` (**L429**) + `ItemDrop.MakePiece(...)` (**L1422**) + `IPlaced.OnPlaced()` + `m_placeEffect` | ✅ (as peças) |

### 2.2 O que dá para reusar (e é a maior parte do trabalho)

- **Mover até o local**: `MHR_Mover` (o mesmo que a coleta e o `MHR_Collect` usam).
- **Coletar o que falta**: `HarvestController` + `MHR_GatherRules` + o teto de ordem (`HC_OrderCap`).
- **Baús**: `MHR_ToolKeeper.UsableChest` (respeita baú privado de outro jogador) + `MHR_Chests`.
- **Peso**: `CompanionCarry.Max/Fraction` (com o cinto somando 150).
- **Falar**: `CompanionTalk.Say` (balão + TTS), `CompanionLocale` (5 idiomas).
- **Ordens**: `CompanionOrders` + `LlmIntent` + as redes por palavras (`RadialIntent`, `InteractionIntent`).
- **Posse/ZDO**: `MHR_Owner.TakeControl` / `CreatorId` / `MHR_WriteOrQueue`.
- **Não é `Player`**: `Humanoid.GetInventory()`, `Character.m_zanim`, `CompanionSkills`.

Ou seja: **80% do caminho já existe**. O que falta é exatamente o pedaço que o jogo só faz para o
jogador: **validar o local** e **fazer a peça nascer**.

### 2.3 O que **não** dá para reusar

- `TryPlacePiece` / `UpdatePlacementGhost` / `PieceRayTest`: `private` e acoplados a `Player`
  (`m_placementGhost`, `m_placementStatus`, `GameCamera`, `ZInput`). Chamar por reflexão exigiria
  montar um `Player` falso — pior que reescrever as ~20 regras.
- `Player.PlacePiece(...)` é **público**, mas é método de instância: usa `GetPlayerID()`,
  `Game.instance.GetPlayerProfile()` (estatística de build **do jogador local**), `AddKnownStation`,
  `GetRightItem()` (animação) e `AddNoise`. Serve como **referência de comportamento**, não como
  executor do companheiro (em multiplayer o `Player` do dono pode nem estar carregado nesta máquina).

### 2.4 A sequência exata do `PlacePiece` (é isso que o executor tem que fazer)

```
TerrainModifier.SetTriggerOnPlaced(true)
go = Instantiate(piece.gameObject, pos, rot)          // o prefab vem do ZNetScene/PieceTable
TerrainModifier.SetTriggerOnPlaced(false)
go.GetComponentInChildren<CraftingStation>()?.AddKnownStation(...)   // só faz sentido p/ o jogador
go.GetComponent<Piece>()?.SetCreator(playerId, platformUserId)
go.GetComponent<PrivateArea>()?.Setup(nomeDoJogador)
go.GetComponent<WearNTear>()?.OnPlaced()
go.GetComponent<ItemDrop>()?.MakePiece(sendRPC: true)
foreach (IPlaced p in go.GetComponents<MonoBehaviour>()) p.OnPlaced()
piece.m_placeEffect.Create(pos, rot, go.transform, 1f, -1, zdoId)
go.GetComponent<ZNetView>()?.GetZDO()?.SetCheated(cheated)           // só no modo cheat
AddNoise(50f)
```

O **PlanBuild** (WTFPL) faz quase isso em `PlanPiece.SpawnPiece` — a diferença é que ele **não** chama
`TerrainModifier.SetTriggerOnPlaced` nem `IPlaced.OnPlaced()`. Nós seguimos o **vanilla**, que é o
caminho mais seguro (o `IPlaced` é o que inicializa fogueira, baú, cama, placa…).

### 2.5 Armadilhas confirmadas no código (cada uma já custaria um bug)

| Armadilha | Evidência | Tratamento |
|---|---|---|
| `Piece.SetCreator` **não grava** se o ZDO não for nosso, e **nunca** sobrescreve criador ≠ 0 | `Piece.cs` L429 (`m_nview.IsOwner() && GetCreator() == 0`) | `nv.ClaimOwnership()` **antes**, e conferir `GetCreator() != 0` depois |
| Peça sem ZDO (prefab inativo) vira objeto morto no mundo | `ZNetView` só cria ZDO no `Awake` | `nv.IsValid() && nv.GetZDO() != null` ou **destruir e abortar** |
| `m_name` da peça é **token** (`$piece_workbench`) | `Piece.m_name` | falar com `Localization.instance.Localize(piece.m_name)` |
| Peça que exige estação falha **em silêncio** se a estação não estiver no raio | `HaveRequirements` → `CraftingStation.HaveBuildStationInRange` (L271) | validar `m_craftingStation` **no ponto**, não na posição do companheiro |
| `m_spaceRequirement` **não** é "espaço livre" em geral — só vale com `StationExtension` | `UpdatePlacementGhost` (bloco do `StationExtension`) | não usar como proxy de "cabe aqui"; o teste de espaço é o `m_blockRadius`/`m_noClipping` |
| Encosta/terreno irregular: o vanilla **joga a peça para o chão** (`point.y = groundHeight`) | `UpdatePlacementGhost` (`m_allowAltGroundPlacement`) | usar o mesmo ajuste só quando a flag permitir |
| Peça em cima do jogador | `CheckPlacementGhostVSPlayers` (L4195) | recusar candidato a < 1,5 m de qualquer `Player`/companheiro |

---

## 3. Arquitetura proposta

### 3.1 Componentes (5 arquivos novos + 3 pontos de integração)

| Arquivo | Papel | Responsabilidade |
|---|---|---|
| `AI/BuildCatalog.cs` | **Recipe Resolver** | nome (5 idiomas) → `Piece` prefab; allow-list por fase; devolve também `Piece.m_resources` |
| `AI/BuildStock.cs` | **Inventory Manager** | quanto tem (mochila → baús usáveis), quanto falta, e o "plano de coleta" |
| `AI/BuildSite.cs` | **Placement System** | candidatos + validação data-driven das flags → `(pos, rot, status)` com o motivo da recusa |
| `AI/BuildExecutor.cs` | **Building Executor** | a sequência da §2.4, sem `Player`, + consumo de materiais + claim |
| `Jobs/MHR_Build.cs` | **Job / Task Verification** | máquina de estados, teto de tempo, falas, e a verificação pós-colocação |

Integração (pontos que já existem):

- `AI/CompanionChatAI.cs` — `ValidActions` + `Contract` (regra e 2 exemplos por idioma) + `Header`.
- `AI/CompanionOrders.cs` — `case "build"` no `Run` (e no lado `none` do `HandleResult`).
- `AI/LlmIntent.cs` / `AI/RadialIntent.cs` — aliases (`build`, `construir`, `construa`, `levanta`,
  `ergue`, `build_*`) e `build` entra em `PreciseActions` (para "constrói uma bancada" não virar
  postura/trabalho).
- `AI/CompanionAI.cs` — `MHR_Build.Drive(this, dt)` **antes** do `MHR_Collect`/`MHR_Loot`.
- `Jobs/MHR_ToolKeeper.cs` — `MHR_JobRules.OtherOrder` responde `true` enquanto a obra roda.
- `Companion/CompanionSetup.cs` — `IsWorkMode`/`ApplyActionMode` recusam dverger (tipo de combate).

### 3.2 O fluxo de uma ordem

```
"Maria, construa uma bancada de trabalho aqui"
   │
   ├─ CompanionChatAI  → LlmIntent { action:"build", target:"bancada de trabalho", amount:1 }
   │     └─ se target vier null/inválido → BuildCatalog.Resolve(palavras do jogador)
   │
   ├─ CompanionOrders.Run → escolhe o companheiro (endereçado, senão o mais próximo)
   │     └─ MHR_Build.Start(cs, piece, siteHint: ponto apontado)
   │
   └─ MHR_Build.Drive (1×/frame, só enquanto ativo)
        Lookup      → BuildCatalog.Resolve          → "não conheço essa peça" (fala)
        CheckStock  → BuildStock.Missing            → 0? vai para FindSite
                                                    → falta? Gather (coleta com teto = falta) ou fala
        FindSite    → BuildSite.Find(hint, raio)    → achou? GoTo : "não achei lugar" (fala)
        GoTo        → MHR_Mover até 2 m do ponto
        Place       → BuildExecutor.Place           → falhou? tenta o próximo candidato (N=1..3)
        Verify      → ZNetView + criador + estação  → ok: "Bancada de trabalho construída." (fala+TTS)
        Done        → limpa estado e volta a seguir
```

### 3.3 Regras de implementação (não negociáveis)

1. **Zero alocação por frame** no caminho ocioso (`Drive` sai antes de qualquer `GetComponent`).
2. **Nada de reflexão** para o placement: os tipos (`Piece`, `WearNTear`, `CraftingStation`,
   `PrivateArea`, `ZNetView`) são públicos no assembly publicizado do projeto.
3. **Toda string nova é chave de localização** nos **5 idiomas** (`hc_ai_build_*`).
4. **Nenhuma escrita no ZDO do companheiro** para o estado da obra: é memória de sessão, como o
   `MHR_Collect` (a obra não sobrevive a relog, e não deve).
5. **Fail-open**: qualquer exceção → `Cancel` + fala + log, nunca exceção para o `Update` do jogo.

---

## 4. Fases (cada uma é um entregável testável)

### Fase 0 — Sonda: parar de adivinhar (½ dia)

**Entrega:** comando de console **de dev** `modia_build_probe [prefab]` (mesmo padrão do
`modia_tts_teste`) que imprime, para `piece_workbench` (e o que mais for passado):

- `Piece`: `m_name`, `m_resources` (item + quantidade), `m_craftingStation`, `m_canRotate`,
  `m_spaceRequirement`, `m_blockRadius`, e **todas** as flags de posicionamento;
- `WearNTear`: `m_supports`, `m_materialType`, `m_noSupportWear`;
- `CraftingStation`: `m_name`, `m_rangeBuild`;
- teste de posicionamento em **8 pontos** ao redor do jogador (2 m e 4 m, 4 direções): status e motivo.

**Saída obrigatória:** `docs/re/building-notes.md` com a ficha da bancada (o mesmo papel do
`docs/re/harvesting-notes.md` para a coleta). **Sem isso, a Fase 2 é chute.**

**Critério de aceite:** o log mostra a ficha completa e o veredito dos 8 pontos, sem exceção.

### Fase 1 — Executor puro (1 dia)

**Entrega:** `AI/BuildExecutor.cs` + comando `modia_build_test <prefab>` que coloca a peça **1,5 m à
frente do jogador**, com o material consumido da mochila do jogador (para o teste) e sem IA.

**Critério de aceite (em jogo, mundo `test3`):**

1. a peça aparece e **funciona** (a bancada abre a janela de craft);
2. `Piece.GetCreator()` = id do jogador; `ZNetView` válido; `WearNTear` com vida cheia;
3. **relog**: a peça continua lá, do mesmo dono;
4. o material foi consumido **exatamente** `m_resources` (nada a mais, nada a menos);
5. `LogOutput.log` sem exceção; nenhum objeto "fantasma" quando a posição é inválida.

> Esta fase **não** tem IA: é a fundação. Se ela falhar, nada acima funciona.

### Fase 2 — Busca de local (1,5 dia)

**Entrega:** `AI/BuildSite.cs` + comando `modia_build_find <prefab>` que **não coloca nada**: mostra o
ponto escolhido (marcador/linha no log) e o motivo de cada candidato recusado.

**Algoritmo sugerido:**

1. candidato 0 = o **ponto apontado** (raycast da câmera, mesmos layers da tecla de comando);
2. senão, anéis de 0,5 m a `SiteSearchRadius` (12 m), 8 direções por anel, ordenados por distância ao
   "hint";
3. para cada candidato: raycast de cima para baixo (`terrain`/`piece`/`static_solid`) → ponto + normal;
   `Heightmap.FindHeightmap` para saber se é terreno; aplicar as flags (§2.3 do `UpdatePlacementGhost`);
   `Location.IsInsideNoBuildLocation`; `PrivateArea.CheckAccess`; distância de jogadores/companheiros;
4. **teste de encaixe**: instanciar o prefab **desativado** (`SetActive(false)`) e checar sobreposição
   (`Physics.ComputePenetration` ou `m_noClipping`), sem tocar no mundo;
5. devolver o primeiro válido **com folga** (não o "no limite"), e no máximo 3 para tentar em ordem.

**Critério de aceite:** numa clareira acha em ≤ 20 candidatos; apontando para água, encosta íngreme,
parede ou `PrivateArea` alheia devolve o **motivo certo** (`no_ground`, `in_water`, `no_build_zone`,
`ward`, `occupied`…).

### Fase 3 — A ordem por chat/voz (1 dia)

**Entrega:** `build` no contrato e no despacho; `BuildCatalog` com a allow-list
(`piece_workbench`, e já `piece_chest_wood`, `piece_firepit`, `piece_bed` para as fases seguintes);
falas novas; **sem coleta** ainda (falta material = fala e cancela).

**Critério de aceite:**

1. `Maria, construa uma bancada de trabalho aqui` → constrói (com 10 madeira na mochila);
2. `constrói um baú` sem bancada perto → recusa com a linha certa ("precisa de uma bancada perto");
3. `constrói um portal` → "não sei construir isso" (fora da allow-list);
4. voz (push-to-talk) faz o mesmo caminho, sem lógica duplicada;
5. dverger (tipo de combate) recusa com a fala que já existe.

### Fase 4 — Coletar o que falta (1 dia)

**Entrega:** `BuildStock` ligado ao `HarvestController` com **teto = o que falta** (o `HC_OrderCap` já
faz isso), volta ao local e constrói. `[Building] GatherForBuild` (true) desliga a coleta.

**Critério de aceite:** com **0 madeira** e árvores por perto, ela coleta 10, volta e constrói;
sem árvores no raio, fala "faltam 10 de madeira" e cancela (sem loop).

### Fase 5 — A oficina mínima (1 dia)

**Entrega:** baú, fogueira e cama no catálogo, cada um com a sua particularidade tratada pelo
validador (baú e cama exigem bancada no raio; fogueira exige chão; cama precisa de espaço).

**Critério de aceite:** 4 ordens seguidas (`bancada`, `baú`, `fogueira`, `cama`) montam uma oficina
funcional, sem nenhuma ordem falhar por posicionamento.

### Fase 6 — Estruturas pequenas (2–3 dias)

**Entrega:** ordem com **plano** (`constrói um chão 2x2 e quatro paredes aqui`) → lista de peças
relativas + **snap** (`Piece.GetSnapPoints` / `FindClosestSnapPoints`) + ordem topológica
(fundação → parede → telhado) + checagem de suporte (`WearNTear.GetSupport`) antes de colocar.

**Critério de aceite:** 2x2 de piso + 4 paredes + telhado, tudo encaixado, sem peça "solta no ar".

### Fase 7 — PlanBuild (opcional, 3–5 dias)

**Duas opções — recomendo a A:**

- **A. Ler o `.blueprint` e construir nós mesmos** (sem dependência). O formato é texto simples e
  estável desde a 0.9: cabeçalhos `#Name:`/`#Creator:`/`#SnapPoints`/`#Terrain`/`#Pieces` e, em cada
  linha de peça, `prefab;categoria;px;py;pz;rx;ry;rz;rw;info;sx;sy;sz`. Precisamos de: parser (~50
  linhas), `ZNetScene.GetPrefab(name)`, e a ordem topológica da Fase 6. Peças de mod que não existirem
  no mundo são **puladas com aviso** (nunca "quase certo").
- **B. Integração com o PlanBuild** (colocar os *plans* dele e deixar o **totem** construir). Exige
  reflexão (o `Blueprint` é `internal`), depende do **Jötunn** na instalação do jogador e amarra a
  nossa IA ao ciclo de release dele. Só se o jogador quiser de verdade.

**Critério de aceite (A):** uma casinha salva pelo jogador é construída peça por peça, com o material
descontado e as peças que faltarem reportadas no chat.

---

## 5. Regras de produto (as decisões que o código vai seguir)

| Tema | Decisão proposta |
|---|---|
| **"aqui"** | ponto apontado (50 m, mesmos layers da tecla de comando) → 3 m à frente do jogador → em volta do companheiro. Sem "aqui": perto da casa (`[General]` home), a partir da Fase 5 |
| **Quem é o dono da peça** | **o jogador** (`MHR_Owner.CreatorId`), nunca o companheiro: a base é dele, o ward protege, o `IsBuiltOnMyFoundation` conta |
| **Precisa de martelo?** | **Não** na 1ª entrega (`[Building] RequireHammer = false`). O executor não depende do martelo; exigir cria um modo de falha novo ("não tenho martelo") sem ganho de jogo |
| **Precisa de estação para peças que exigem?** | **Sim**, e é o próprio `Piece.m_craftingStation` + `HaveBuildStationInRange` que decide (nada de tabela nossa) |
| **De onde sai o material** | mochila → baús usáveis no raio da casa (`UsableChest`, nunca baú privado de outro jogador) → coleta (se `GatherForBuild`) |
| **Limites** | 1 obra por ordem; `MaxPiecesPerOrder` (20) na Fase 6; `MaxDistanceFromHome` (40 m) para não construir o mapa; nunca em `Location.m_noBuild`, `PrivateArea` alheia, água ou a < 1,5 m de alguém |
| **Recusa** | fora da allow-list, sem receita, sem material e sem coleta, sem local, ou tipo de combate (dverger) → **uma** fala clara e volta a seguir |
| **Multiplayer** | **congelado** nesta rodada (decisão do jogador). O plano assume "roda onde o companheiro roda" e documenta o risco de posse em servidor dedicado |
| **Remover peça** | fora de escopo (o martelo do jogador já remove) |

---

## 6. Configuração e localização

**Seção nova `[Building]`** (8 chaves, todas em inglês no `.cfg`, como desde a 1.8.3):

| Chave | Padrão | Para quê |
|---|---|---|
| `Enabled` | `true` | liga/desliga a construção |
| `GatherForBuild` | `true` | coletar o que falta (Fase 4) |
| `UseChests` | `true` | procurar material nos baús |
| `SiteSearchRadius` | `12` | raio da busca de local |
| `MaxDistanceFromHome` | `40` | coleira da obra |
| `MaxPiecesPerOrder` | `20` | teto da Fase 6 |
| `RequireHammer` | `false` | exigir martelo na mochila |
| `Quiet` | `false` | não chamar `AddNoise` (não atrai mob) |

**Chaves de UI/fala novas** (`hc_ai_build_*`, ~10, nos 5 idiomas): `order_build`, `no_recipe`,
`not_allowed`, `missing` (`{0}` de `{1}`), `collecting`, `no_site`, `needs_station`, `done` (`{0}`),
`failed`, `full`.

> **Contagens que mudam e precisam ser atualizadas em todos os lugares** (AGENTS, CHANGELOG, wiki ×5,
> release notes): `ValidActions` **35 → 36**, configs **343 → 351** (24 → 25 seções), chaves de UI
> **1001 → ~1011**. O gate `tools/release-check.ps1` cobre traduções e `CONFIGURATION.md`; o
> `ai-intents-check` ganha os casos do `BuildCatalog`/`RadialIntent`.

---

## 7. Riscos e mitigações

| Risco | Prob. | Impacto | Mitigação |
|---|---|---|---|
| Flags da bancada diferentes do esperado (exige chão *cultivado*? exige espaço?) | média | alto | **Fase 0** (sonda) antes de qualquer regra; validador data-driven |
| Peça nasce sem dono (claim perdido) | média | alto | claim → `SetCreator` → **conferir** `GetCreator()`; se 0, destruir e falhar |
| Peça "fantasma" (sem ZDO) no mundo | baixa | alto | `nv.IsValid()` + `GetZDO() != null`; senão `Destroy` e abortar |
| Busca de local cara (raycasts) | média | médio | 1× por ordem, máx. N candidatos, cache do resultado; nunca por frame |
| Estrutura sem suporte (Fase 6) | alta | médio | ordem topológica + `WearNTear.GetSupport()` antes de colocar; recusar e reportar |
| `AddNoise(50)` atrai mob e a obra vira briga | média | baixo | `[Building] Quiet` |
| Job preso (mover/atacar no meio) | média | alto | teto de 30 s, "tempo não dirigido não conta" (regra do `MHR_Collect`), sempre uma fala final |
| MP: posse do ZDO da peça | alta | alto | **fora de escopo** (congelado); documentar em `docs/re/building-notes.md` |
| PlanBuild: `Blueprint` é `internal` e exige Jötunn | alta | médio | opção A (ler o arquivo) como padrão; B só sob pedido |
| Contagens/documentação divergirem | média | baixo | gate de release + `audit_translations.py` + doc no mesmo ciclo |

---

## 8. Como testar (receita com o DevBridge)

```
1. [Advanced] DevBridge = true, entrar no mundo (world_enter já roda devcommands/tod/god/heal)
2. companions.spawn                      → a Maria existe
3. player.give Wood 10                   → material na mochila do jogador
4. companions.give Wood 10               → material na mochila dela (Fase 3)
5. mirar no chão e digitar no chat: "Maria, construa uma bancada de trabalho aqui"
6. companions.zdo                        → estado do companheiro (modo, ordem)
7. screenshot                            → evidência visual da bancada
8. conferir no LogOutput.log: [Build] ... (uma linha por transição)
```

**Teste de regressão obrigatório:** com a obra desligada (`[Building] Enabled = false`), o
`ValidActions` continua com 36 mas a ordem responde "não sei construir" — nada mais muda no mod.

---

## 9. O que existe no ecossistema (pesquisa de 31/10/2026)

| Projeto | Licença | O que serve |
|---|---|---|
| **PlanBuild** (`sirskunkalot/PlanBuild`) | **WTFPL** | O **Plan Totem** já faz "construir planos com material" e o `PlanPiece.SpawnPiece` é um executor **sem `Player`** em produção — a melhor referência para a §2.4. O formato `.blueprint` é texto simples (`PieceEntry.FromBlueprint`). **Não** tem API pública (`Blueprint` é `internal`) e exige **Jötunn** |
| **Infinity Hammer** (`JereKuusela`) | **Unlicense** | Coloca peças em massa, mas **hookando `Player.PlacePiece`** — confirma que o caminho "sem player" é raro e que o nosso executor é o diferencial |
| **VillageNPCs / Innangard / VikingNPC** | — | NPCs decorativos/comerciantes; **nenhum constrói**. Não há mod de "NPC construtor" para copiar: a Fase 1–3 é original |
| Wiki/Fandom do Valheim | — | Não documenta as flags de `Piece` (só o comportamento observável). Daí a Fase 0 |

---

## 10. Decisões abertas (com o meu voto)

1. **"aqui" = o que eu aponto?** → **sim** (S6). É o gesto que o jogador já usa na tecla de comando.
2. **Coletar sozinha (Fase 4) já na 1ª entrega, ou só construir com o que tem?** → **Fase 3 sem
   coleta** (entrega mais rápida e menos modos de falha), **Fase 4 logo em seguida**. Se o jogador
   quiser a experiência completa de uma vez, as duas entram juntas.
3. **Exigir martelo?** → **não** (`RequireHammer = false`).
4. **Allow-list fechada ou qualquer peça do martelo?** → **allow-list** até a Fase 6 (bancada, baú,
   fogueira, cama). Peça arbitrária só com o "modo obra".
5. **PlanBuild: ler o arquivo (A) ou integrar (B)?** → **A**, e só depois de a Fase 6 estar estável.
6. **Nome da ação**: `build` (token do schema) com as palavras PT `construa/constrói/levanta/ergue/
   faz uma`; ES `construye/levanta`; FR `construis/construire`; IT `costruisci`.

---

## 11. Esforço e ordem de trabalho

| Fase | Estimativa | Depende de |
|---|---|---|
| 0 — sonda + `docs/re/building-notes.md` | ½ dia | — |
| 1 — executor | 1 dia | 0 |
| 2 — busca de local | 1,5 dia | 0, 1 |
| 3 — ordem chat/voz | 1 dia | 1, 2 |
| 4 — coleta | 1 dia | 3 |
| 5 — baú/fogueira/cama | 1 dia | 3 |
| 6 — estruturas pequenas | 2–3 dias | 2, 5 |
| 7 — PlanBuild (A) | 3–5 dias | 6 |

**MVP (Fases 0–3) ≈ 4 dias.** É o recorte que entrega a frase do §0 funcionando.

**Paralelismo possível (regra 10 do `AGENTS.md`):** Fase 0 é bloqueante e é de um agente só. Depois
disso, `BuildCatalog` + chaves de localização (docs-writer) rodam em paralelo com `BuildExecutor`
(ai-npc), e o harness do `ai-intents-check` (qa) em paralelo com a Fase 2. O orquestrador revisa o
diff, roda build + gate e commita — nada entra sem revisão.

---

## 12. Critérios de aceite da entrega (a bancada)

1. Com **10 madeira** na mochila e espaço livre, `Maria, construa uma bancada de trabalho aqui`
   coloca a bancada a **≤ 5 m** do ponto apontado, em **≤ 3 s**, e fala `hc_ai_build_done`.
2. Sem material e com `GatherForBuild`, ela coleta, volta e constrói; sem material e sem coleta, fala
   quanto falta e cancela.
3. Apontando para **água / encosta / parede / ward alheio**, ela escolhe outro ponto no raio; se não
   houver nenhum, fala `hc_ai_build_no_site` (com o motivo no log).
4. A bancada é **do jogador** (`Piece.GetCreator()` = id dele), tem `ZNetView` válido e **abre a
   janela de craft**.
5. **Relog**: a bancada continua lá, do mesmo dono, funcionando.
6. `LogOutput.log` sem exceção; **0 alocação por frame** com a obra ociosa; `[Building] Enabled =
   false` desliga tudo sem tocar no resto do mod.
7. Build **0/0**, gate `release-check.ps1` **PASS**, `ai-intents-check` com os casos novos **PASS**.

---

## 13. O que **não** está neste plano (de propósito)

- **Multiplayer dedicado** (posse do ZDO da peça, quem é o "criador" quando o dono não está online).
- **Remover/editar** peças existentes.
- **Terraformar** (nivelar terreno antes de construir) — é a Fase 7+ se o PlanBuild entrar.
- **Construir sozinho sem ordem** ("modo arquiteto"): só depois de a Fase 6 estar validada em jogo.
- **Qualquer dependência nova no `.csproj`** (nem Jötunn, nem PlanBuild).
