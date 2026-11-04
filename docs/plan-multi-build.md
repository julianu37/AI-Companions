# Plano — vários companheiros na mesma obra (multi-build)

**Status (04/11/2026):** **P1 MEDIDO EM JOGO** na branch `fix/pisos-nivel-2` (3 companheiros na `CommHomeBasicBP`,
`BlueprintFree = true`): com `PlaceReach = 10` (padrão) **970 + 15 = 985** peças — a fala final fecha, **`no_ground`
~25 → 0**, **`blocked_by_player` 17+ → 0**, `log ajustado` **6**, 1 morte; com `PlaceReach = 15` (config de teste) a
passada principal chegou a **987/987**, **972 colocadas** e **0 mortes**, sobrando **13**; no **Run 3** (15 + a
guarda anti-loop) **`970 peças, 16 puladas`** = **986**, **0 mortes** e **382 KB** de log, e as 16 nomeadas revelaram
o **plano abaixo do terreno** — o desenho novo é a **obra inteira subir `[Building] DeckLift`** (padrão 1) com as
pontas de estaca assentadas no chão, **medido no run seguinte** (§7 e `docs/re/building-notes.md` §17.10: ~**972 peças
de pé**, **0 mortes**, piso ~**1 m** acima do chão e escadas das entradas encontrando — o deck está certo; sobraram
**6 telhados** em 42,8/44,8/46,8 e **4 `falls`**, atacados no lote de 04/11, **sem run ainda**). O código é
`Jobs/MHR_BuildGroup.cs` mais
`StartBlueprintGroup`/`Phase.WaitGroup`/`Phase.WaitRetry`/`QueueRetry`/`MarkDone`/`IsLeader`/`PlanLocked` em
`Jobs/MHR_Build.cs`, e o ciclo de 04/11 fechou o **portão de apoio** (`AI/BuildFailure.cs`), o **watchdog de
estagnação**, a **cobertura de bancadas** e o validador (`attached`/`nudge`/`blocked_by_player`) — detalhe medido em
`docs/re/building-notes.md` §17. **P0** (quem tem 1 companheiro não vê diferença) e **P2** (navmesh/latência,
**medida em jogo** em 03/11/2026) fechados; **P3** (UI do grupo), **P4** (nomes múltiplos) e **P5**
(persistência/limpeza paralela) **não**.

Números deste ciclo (04/11/2026): build **0/0**, harness `tools/ai-intents-check` **549 asserções** (471 + **23** da
seção **(p)**, `AI/BuildFailure.cs`, + **26** da **(q)**, `AI/BuildPieceKind.cs`, + **29** da **(r)**,
`AI/BuildSupportBelow.cs` — as **10** últimas de `CanSupport`, telhado apoia telhado; as três tabelas puras e
linkadas), teste do MCP **101** (as **12** asserções de 03/11 cobrem o contrato do `companions.build` do grupo),
config **360 opções / 25 seções** (medido com `python tools/gen_config_doc.py --check`), ES/FR/IT **0 lacunas**, dívida
do `gen_translations.py` **45** e **nenhuma chave de tradução nova** (as recusas reusam `hc_ai_no_companion` e
`hc_ai_build_failed`). **O que falta medir** está no §7 (o run do lote novo, o run com material pago e o teste pequeno
de 2 companheiros num 6x6).

## 1. O que existe hoje (medido no código)

- **Modos de trabalho** (`wood`, `stone`, `ore`, `forage`, `smelt`, `hunt`, `farm`, `fish`, `repair`, `restock`,
  `cook`, `stay`): sem nome, `CompanionOrders.Run` percorre **todos** os companheiros do jogador; com um nome,
  `IsNotAddressee` deixa só o nomeado. É o "todos obedecem" que fica.
- **Ordens de um tiro** (`build`, `level`, `attack`, `equip`, `drop`, `inventory`, `come`): usam **um** alvo —
  `target = addressee ?? speaker`, e o `speaker` é o companheiro mais próximo que pode receber ordem
  (`CompanionChatAI.PickSpeaker`). **Até o P1**, `build` sem nome ia para **um só** sempre; **depois do P1** isso
  passou a valer só para **template** e **peça única** — o **blueprint** sem nome vai para todos (§3).
- **`MHR_Build` é por companheiro** (`s_state` por `GetInstanceID`), com plano, `Pending` (coleta/morte),
  `Reattach` e estações próprios. Dois `StartBlueprint` no mesmo anchor = **dois planos** sobre as mesmas peças — por
  isso o P1 nunca chama um `StartBlueprint` por companheiro: chama **um** `StartBlueprintGroup`.
- **`MHR_BuildGroup` agora existe** (P1 MVP): o canteiro é o dono do **plano**, dos **claims por índice** e da **fila
  de retry**; o `MHR_Build` de cada companheiro virou o *worker* (§3).
- **Nome:** `CompanionNames.FromMessage` devolve **um** nome (o primeiro antes da vírgula/segmento); o campo
  `companion` do modelo é um `Find` separado. "Maria e João" continua sendo P4 — o grupo não depende disso: sem nome,
  entram todos os que podem receber ordem.
- **Navmesh:** `MHR_CompanionNavMesh` (agente 40, climb 0.6, `Pathfinding.BuildTile` sem portas) não invalida
  **explicitamente**; o jogo re-pokeia no `GetPath` e reconstrói o tile em até **5 s** (`m_updateInterval`). O
  `no progress` de 12–17 m é o `SnapToNavMesh` (1,5/3/6/12 m), não o tile invisível. A P2 reduz a latência
  (`m_pokeTime` + `m_buildTime = now - 5,1`, coalescido) e cobre a remoção (`ClearScaffolds`/`build.delete` via
  `ZNetScene.Destroy`). Detalhe: `docs/re/navmesh-tiles.md` e `docs/re/navegacao-companheiro.md` §6.

## 2. Objetivo e não-objetivo

**Objetivo**
1. N companheiros na **mesma** obra: **um** plano, peças distintas, **nenhuma peça colocada duas vezes**. O material
   é pago uma vez **por peça**; a reserva compartilhada por pilha (o `Stock` do desenho) **não** existe ainda — é a
   metade aberta do P1 (§3, *Limitações declaradas*).
2. "Construam a cabana aqui" sem nome → **todos** os que podem receber ordem entram; "Zé, construa X" → só o Zé;
   "para" → todos; "para, Maria" → só a Maria.
3. Manter o "todos obedecem" dos modos sem nome e o `build` de **um** para template/peça única.

**Não-objetivo (agora)**
- Persistir o grupo no save/mundo (o build atual também não persiste; o `Pending` é em memória).
- Multiplayer dedicado, offscreen workers, UI completa do grupo.
- Contratar/gerenciar o grupo pelo painel.
- **Nomes múltiplos** ("Maria e João") — é P4; o grupo aceita **um** nome ou nenhum.

## 3. Modelo implementado: `MHR_BuildGroup` (P1 MVP)

**Chave do canteiro** (`MHR_BuildGroup.KeyOf`, `Jobs/MHR_BuildGroup.cs:101`): `label|anchor(x,y,z em 1 m)|yaw(5°)|owner`.
Dois companheiros que miram o mesmo ponto caem na mesma chave (o arredondamento absorve a mão do jogador) e o
canteiro de outro jogador **nunca** é juntado (`owner` = `MHR_Owner.CreatorId` do líder).

**Uma obra por jogador** (`SiteBusy`, `:119`): a segunda ordem é **recusada** ("já existe um grupo de obra em
andamento") e o jogador tem que dizer "para" antes. A checagem é **antes** de o líder começar qualquer coisa, para
não perturbar um canteiro em andamento.

**Grupo (dono do plano)**
- `Plan`: **a lista exata do líder** (`MHR_Build.PlannedStep`: `Prefab`, `Piece`, `Pos`, `Rot`) — os seguidores
  recebem a **mesma referência** via `storedPlan` (o mesmo caminho da retomada depois da coleta/morte), então os
  índices casam peça por peça.
- `BuildPhase`: o canteiro está pronto (o líder nivelou/limpou) — é o portão dos seguidores.
- `LeaderId`, `Workers` (`List<int>` de `GetInstanceID`), `Claims`/`ClaimAt` (índice → worker/hora), `Done`
  (`HashSet<int>`: a peça contada **uma vez**), `Retry` (fila do canteiro), `Placed`, `Skipped`.
- **Só a main thread** toca no grupo (claims e veredito são por frame: sem lock, buffer de rascunho em campo).

**Tempos (`MHR_BuildGroup.cs:36`, `MHR_Build.cs:85` e `:581`)**
- Claim: **20 s** (`ClaimSeconds`) — renovado a **cada frame dirigido** (`Refresh`), então o prazo é para o worker
  que **sumiu**, não para uma caminhada longa.
- Retry **real**: **2 s** de espera antes de a peça poder ser puxada de novo (`RetryDelay`), teto de **3** tentativas
  por índice (`MHR_Build.MaxPasses`, **compartilhado pelo canteiro**).
- Retry por **dependência** (portão de apoio, 04/11/2026): **não** gasta tentativa e volta com espera crescente —
  **8 / 12 / 20 / 25 s** (`DependencyDelay`).
- Watchdog de estagnação: o canteiro pode ficar **60 s sem uma peça subir** (`StallSeconds`, contado do
  `LastProgressAt` do grupo, não do relógio de quem espera) antes de a fila ser descartada e a ordem encerrar pelos
  números normais. Substituiu o antigo `RetryWaitMax` de 60 s, que media a espera de **um** worker e não dizia se o
  canteiro estava andando.

**Quem entra**
- **Blueprint sem nome:** `CompanionOrders.BlueprintWorkers` (`AI/CompanionOrders.cs:1381`) pega todos os
  companheiros que podem receber ordem — do **dono** (`MHR_Owner.IsMine`: `MHR_InScope` **não** checa dono e, num
  mundo de dois jogadores, uma ordem sem nome levaria o companheiro do outro), com `MHR_InScope` (Command ligado,
  dentro do `RecallRange`), que **não** é *combat-only* (`CanWearArmor`) e cuja **própria** casa está a
  ≤ `[Building] MaxDistanceFromHome` do canteiro. Morto não entra (quem filtra é o `StartBlueprintGroup`, que também
  não aceita o corpo sem `CompanionAI`).
- **Blueprint com nome:** só ele — e o caminho é **idêntico** ao de antes (`StartBlueprintGroup` com 1 chama
  `StartBlueprint` direto: sem claims, sem espera, sem grupo).
- **Template e peça única:** continuam com **um** (dois workers construiriam a mesma peça duas vezes).

**Líder**
- O primeiro da lista. Faz `Phase.Level`/`Phase.Clear` (nivelar e limpar o canteiro) e é **quem abre** o
  `BuildPhase` (em `Drive`, quando já saiu de `Level`/`Clear`), pegando a peça dele no mesmo instante para os
  seguidores não começarem na mesma.
- Seguidores nascem em **`Phase.WaitGroup`** (o plano do líder, `StartBlueprint(..., resuming: true, storedPlan)`):
  **não** têm relógio de timeout nem guarda de peso, não andam e não são tomados pelo job de ócio (a ordem está
  viva). Quando o portão abre, cada um pega um índice livre e começa a andar.
- **Se o líder sai** (foi coletar, morreu, "para, Fulano"), o próximo é **promovido** (`Leave` → `LeaderId =
  Workers[0]`). Se ele saiu **antes** de abrir o `BuildPhase`, a fase **abre**: os outros entram com o canteiro como
  está (o que não ficar de pé vai para o retry). O líder é perguntado ao **grupo** (`IsLeader`), nunca a um flag do
  worker — um flag copiado no início mentiria depois da troca.

**Atribuição**
- Cada worker anda pelo plano **por índice** e **reivindica** a peça (`TryClaim`); a peça que está com outro é
  **pulada** (o índice segue, nada é pago e ela **não** conta como feita), e pode voltar pela fila de retry.
- **Não** é "a peça não reivindicada mais perto" como o desenho dizia: a decisão foi **ordem do plano + claims**,
  porque com vários workers andando pelo mesmo índice um "mais perto" por worker embaralharia a ordem do plano (e as
  peças que dependem uma da outra: piso → parede → telhado).
- `PreferNearFloor`, `PullSupports` e `PullFloorsAbove` continuam valendo com **um** worker; com **≥2**
  (`PlanLocked`, `MHR_Build.cs:908`) ficam desligados — reordenar o plano de um deixaria os índices dos outros
  apontando para outras peças (e os claims com eles). O retry do canteiro é o que cobre o que o reorder resolvia.

**Estação (bancada)**
- `PlanStations` roda **uma vez**, no plano do líder, antes da 1ª peça (a regra do jogo é por peça, `HaveBuildStationInRange`
  a 20 m com o Y achatado: uma bancada serve para todos).
- **Cobertura (04/11/2026, medido):** até **12** bancadas planejadas (`MaxPlannedBenches`) e **32** buscas
  (`MaxBenchSearches`), com a bancada **fora do footprint** e no **terreno** (até 4 âncoras: anel perto, anel longe e
  ±40° no anel longe). A passada termina com `station: N peça(s) precisam, M planejada(s), K sem cobertura` e nomeia
  cada peça sem cobertura, separando **"nenhum lugar fora do footprint"** de **"orçamento esgotado"**. Antes eram 6
  buscas e a casa de 32 m ficava com 2 bancadas (o meio caía em `needs_station`).
- `InsertBenchStep` (auto-cura do `needs_station`) com **≥2** workers: só o **líder** tenta, e a bancada vai para o
  **fim** do plano (não insere no meio — o índice de ninguém anda); a peça recusada vai para o retry, que roda depois
  da bancada. Com 1 worker continua inserindo **na frente** da peça, como antes.

**O portão de apoio (04/11/2026, medido)**
- Toda peça que não sobe passa por `QueueRetry` e é **classificada** (`AI/BuildFailure.cs`, pura, seção **(p)** do
  harness): **dependência** (`no_support`, `no support`, `needs_station`, e `no_ground` **com** algo embaixo) **não
  gasta tentativa e nunca conta como pulada** — volta com espera crescente (8/12/20/25 s); **falha real** (`no
  progress`, `too_steep`, `clipping`, `can't reach it`, `verification: …`, `no_ground` sem nada embaixo, status
  desconhecido) gasta uma das 3.
- Por que existe: na cumeeira o `no_support` acontecia **antes** de o apoio subir, as 3 tentativas queimavam e a peça
  ficava fora da casa (medido em 03/11).
- **Watchdog:** se o canteiro passa **60 s sem uma peça subir**, a fila é descartada (cada índice contado uma vez),
  nomeada no log, e a ordem encerra — uma dependência que nunca vem (buraco, piso que não existe) não segura a obra
  para sempre.
- **Apoio imediato (`PullDirectSupport`, 04/11/2026):** num grupo ninguém reordena o plano (`PlanLocked`), então o
  **apoio direto** de uma peça recusada com `no_support` é **puxado por fora da ordem** (`AI/BuildSupportBelow.cs`: 0,4–2,5 m
  abaixo × 2,5 m em XZ, seção **(r)**; só peça **estrutural** e nunca já construída), sem tocar no plano —
  a peça recusada fica na fila do canteiro como **dependência**. **Quem pode apoiar** é o `CanSupport`
  (`AI/BuildSupportBelow.cs:72`, lote de 04/11/2026): **telhado apoia telhado** (a fileira de baixo da água — sem isso a
  **empenha** nunca puxava nada, §7), telhado no piso/parede/viga/escada, **porta nunca apoia** e piso/parede **não** são
  apoiados por telhado; **mobília** já foi cortada antes pelo `BuildPieceKind.IsStructural`. **Guarda obrigatória:**
  `State.PulledSupport` (`MHR_Build.cs:213`/`:3110`) puxa **uma vez por índice de peça** e no máximo **24 por ordem**; na
  2ª recusa o pull não acontece e ela segue o portão. **Por que:** o build `290afc1` entrou em loop infinito (recusa →
  puxa → apoio falha → recusa) e escreveu **344 MB** de log em ~10 min, travando o jogo — o watchdog não pegou porque só
  é lido em `WaitRetry` e o loop nunca espera (`docs/re/building-notes.md` §17.8).

**Materiais (o que ainda não existe)**
- **Sem reserva por peça:** cada worker conta mochila + baús do canteiro para a **peça dele**. Duas pilhas podem ser
  contadas duas vezes e o segundo a chegar entrega a ordem à coleta — o pior caso é **N× o resto do plano**, com até
  **3 coletando ao mesmo tempo** (a coleta é um job por companheiro). O `Stock` do desenho é a metade aberta do P1.
- Quem vai coletar **sai** do grupo: o `HandOver` entrega o movimento à coleta, grava o `Pending` **com a chave do
  grupo** e a ordem de obra **termina** (`Finish` → `Leave`, `MHR_Build.cs:3876`). Quando a obra retoma, o
  `RejoinGroup` põe o companheiro de volta no canteiro e ele reivindica a peça de novo (ou segue, se outro a
  construiu).

**Fim, morte e retomada**
- Ao acabar o plano, o worker puxa a fila do canteiro (`NextRetry`) ou espera (`WaitRetry`). O **último** a sair
  fala o número do **canteiro** (`hc_ai_blueprint_done`, `Placed`/`Skipped` do grupo); os outros terminam em
  silêncio ("a lista de peças acabou para este companheiro (o grupo continua)").
- `OnDeath`, `Cancel`, `DiscardState` e `Finish` tiram o worker e **liberam a peça dele** na hora. O grupo **não**
  morre com um worker (só quando o último sai). O `Pending` guarda a **chave do grupo**; `RejoinGroup` reentra e
  reivindica — se a peça já estiver **de pé** (`VerifyAt`), ela é contada uma vez e ele segue para a próxima.
- Grupo **em memória**: não vai para o save (como o `Pending`).

**Anti-duplicata**
- Claim **revalidado antes de pagar** (`Place`, `MHR_Build.cs:3927`): se o claim expirou enquanto ele lutava/carregava
  madeira, a pose é perguntada ao mundo **antes** de pagar de novo — peça que outro já construiu **nunca** é colocada
  duas vezes.
- `MarkDone` conta **1× por índice**; `VerifyAt` (pose **e** yaw: `PlacedRadius` = 0,35 m, `PlacedYaw` = ±25°,
  `AI/BuildExecutor.cs:65-68`) é o "já está de pé".
- Peça que não fica de pé sai do plano do worker e vai para a **fila do canteiro** (dedup por índice); quando as
  tentativas acabam ela é **pulada** — o único lugar onde uma peça é dada por perdida.

**Parar**
- **"para" sem nome** → `MHR_Build.CancelAll` → `MHR_BuildGroup.CancelAll`: derruba **todos** os canteiros (as peças
  já colocadas ficam).
- **"para, Maria"** → `Cancel(Maria)` → `Leave`: tira **só** ela, libera a peça dela e o grupo continua (o próximo
  assume o canteiro se ela era o líder).
- **Um modo num companheiro** (ex.: "Zé, colete") tira **aquele** worker: `Apply` cancela o build do endereçado
  (`CompanionOrders.cs:229-240`) e o `Drive` também encerra com "another job was picked" quando o modo muda.
- Um modo **sem nome** cai no `CancelAll` (a mesma regra de sempre: ordem sem alvo derruba tudo).

**DevBridge** (`companions.build` com `blueprint`): `name` vazio = todos os companheiros do jogador; `workers` = em
quantos a ordem entrou; `refused` (só quando não entrou ninguém) diz por quê (`no companion in range`,
`the site is busy`, `missing station: …`, `missing piece: …`, `not started`). O reply **não** expõe o nivelamento. O
teste mock do MCP (**101 PASS**, **12 asserções novas para o contrato do grupo**) cobre justamente esse contrato.

**Limitações declaradas (P1 MVP)**
1. **Sem reserva de material por peça** — cada worker conta mochila + baús por conta própria: duas pilhas podem ser
   contadas duas vezes, o pior caso é **N× o resto do plano** e até **3 coletando ao mesmo tempo**. O `Stock` do
   desenho é a metade aberta do P1.
2. **Grupo só de blueprint** — template e peça única continuam com **um** (dois workers fariam a mesma peça duas
   vezes).
3. **Em memória** — o canteiro não vai para o save; reiniciar o mundo perde o grupo (as peças colocadas ficam).
4. **Quem para o quê:** "para" = **todos**; "para, <nome>" = **um**; um **modo** num companheiro tira **só ele**; um
   modo **sem** nome derruba tudo.
5. **Nivelar/limpar com um** (o líder) — paralelizar a limpeza é P5.
6. **Plano não reordenado com ≥2** e bancada anexada ao fim: menos eficiente que o reorder, mas é o que mantém os
   índices (e os claims) honestos.
7. **Um nome só como endereço** (nomes múltiplos é P4) e **uma obra por jogador** (`SiteBusy`).

## 4. Nomes múltiplos (P4 — por último)

- `CompanionNameMatch.FromMessageList(message, names)` (puro, testável): lê o **prefixo** antes do primeiro verbo
  de ordem/segmento; separa por `,`, `;`, `e`, `&`, `+`, `e o/a`; casa nomes **existentes** (sem caixa/acento,
  artigo opcional); conectores não são nomes. Conservador: um token que não é nome conhecido nem conector
  **para** a lista no último nome válido (nunca inventa); um primeiro token desconhecido com cara de nome continua
  usando o `AddressCandidate`/aviso atual.
- `CompanionNames.FromMessage` devolve `List<CompanionSetup>`; `Find` (campo do modelo) continua um só. Prioridade:
  **as palavras do jogador** (lista) vencem o campo do modelo; se a lista estiver vazia, o campo do modelo; se
  ambos vazios, o comportamento antigo (modos: todos; build: todos os que podem receber ordem, no blueprint).
- `CompanionOrders`: `addressee` vira uma lista pequena (`Addressees`); `IsNotAddressee` consulta a lista. Ações
  inerentemente de um alvo (`equip`, `drop`, `inventory`, `come`) usam o **primeiro que pode** (com log); modos,
  `attack` e `build` usam **todos** da lista. `BlueprintWorkers` passa a receber a lista em vez de um endereçado.
- Prompt: o `PromptLine` já lista os nomes; uma linha curta "o jogador pode chamar um ou mais" (sem exemplo novo
  no contrato do 3B; a lista é lida localmente). `ValidActions` **não** muda.
- Harness: casos puros de `FromMessageList` ("Maria e João", "maria, joao", "Maria e o Zé", "Maria e o cachorro",
  "colete 10 pedras", "Ei, Maria e João", nomes repetidos, nome desconhecido) + `IsNotAddressee`.

## 5. Fases (ordem de implementação)

1. **P0 — feito:** o comportamento de **1** companheiro é o de antes (claim sempre concedido, `IsLeader` sempre
   verdadeiro, `PlanLocked` falso, retry do worker). Com 1 worker o `StartBlueprintGroup` chama `StartBlueprint`.
2. **P1 — MEDIDO EM JOGO (04/11/2026, 3 companheiros, `BlueprintFree`):** `MHR_BuildGroup` (plano/claims/retry),
   `WaitGroup`/`WaitRetry`, líder que nivela/limpa e **é promovido quando sai**, `PlanLocked`, bancada anexada ao fim,
   uma obra por jogador, anti-duplicata, `BlueprintWorkers` (sem nome = todos), `SetThenWorkers`/`NoteLeveled` (o
   **nivelamento devolve o grupo inteiro**) e o `companions.build` do bridge. O ciclo de 04/11 acrescentou o **portão
   de apoio** (`BuildFailure`, dependência não gasta tentativa), o **watchdog de 60 s**, a **cobertura de bancadas**
   (12/32, fora do footprint, no terreno) e o **validador ampliado**: `AttachedToStructure` a **4,0 m** (era 2,6 — o
   grid do telhado tem ~2 m e a vizinha fica na diagonal, a ~2,8–3,5 m), **`NudgePiece` universal** (qualquer peça com
   `too_steep`/`no_ground`, com o raio do `BuildPieceKind`: **estrutural ≤ 1 m**, **mobília até 5 m** desde o `290afc1`,
   que subiu de 2,5 m e passou a varrer 3 anéis × 12) e `blocked_by_player` **sem contar companheiro**
   (`BlockedByCharacter` ignora quem tem `CompanionAI` — eram 17+). **Medido nos runs (§7):**
   `PlaceReach = 10` → **970/985** (15 puladas, `no_ground` ~25 → **0**, `blocked_by_player` 17+ → **0**,
   `log ajustado` 6, 1 morte); `PlaceReach = 15` (só config) → passada principal **987/987**, **972 colocadas** e **0
   mortes**; **Run 3** (15 + guarda anti-loop) → **`970 peças, 16 puladas`** = **986**, **0 mortes** e **382 KB** de
   log; **Run 4** (com o **deck**, `DeckLift = 1`) → ~**972 peças de pé**, **0 mortes**, deck certo, **6 telhados**
   nomeados e **4 `falls`**. **Fechado depois:** o lote das 13 (nudge de mobília 5 m/3×12, `TryBenchAt` sem `clipping`, apoio imediato)
   e o **deck (`DeckLift`)**. **Aberto:** a **reserva de material** (`Stock` — cada worker conta o canteiro por conta
   própria), um run com **material pago**, o run do lote desta rodada (**telhado apoia telhado** + **`falls` com uma
   segunda tentativa a 1 m**, `building-notes` §17.10, + o **apoio de emergência** do `SupportFallback`, §17.11) e o
   teste pequeno de 2 companheiros num 6x6.
3. **P2 — navmesh/latência — MEDIDO EM JOGO (implementado e testado em 03/11/2026):** (a) **medido o `why`** do run de 03/11/2026:
   **36 `no progress`**, 17 `no_support`, 9 `no_ground`, 6 `too_steep` — os 36 **não** são "tile invisível": são o
   `SnapToNavMesh` (1,5/3/6/12 m) mandando o alvo para o chão sob o piso, com a trava de 12 s do `Goto` queimando;
   (b) **carência do mover** (`MHR_Mover.Plan`: rota `Partial` + alvo acima → ~6 s antes de `Unstick`/`no progress`,
   só `Build`/`BuildGather`) — ataca os 36; (c) **invalidação coalescida** (`m_pokeTime` + `m_buildTime = now - 5,1`,
   só tiles existentes, 0,35 s; postfix `OnPlaced` + prefix `Destroy` + chamadas em
   `PlaceDirect`/`ClearScaffolds`/`build.delete`/`MHR_Level.NextPass`), config `[Movement] PathingOnBuild` (padrão
   `true`), tools `nav.tiles`/`nav.probe`/`nav.stats` e harness puro `NavTileMath`. O `-1000` literal é tempestade
   (985×9 bakes); detalhe em `docs/re/navmesh-tiles.md` e `docs/re/navegacao-companheiro.md` §6.1. **Medido em jogo
   (03/11/2026):** o gancho levou o `pokes` 0 → 1 e o `rebuilds` +9 numa peça colocada pelo companheiro (2 no
   `build.delete`), `maxCollectMs` 2,65 ms; a obra foi de **720/983 para 1045/1078** e parou por **falta de
   material** (77 madeiras, coleta 300 s sem entregar, mochila em 302/300) — a contagem de `why` não foi feita
   (o log foi sobrescrito no restart).
4. **P3 — UI/feedback (não feito):** progresso do canteiro (`N/M`, workers), quem está com qual peça; o painel por
   companheiro mostra "obra: X".
5. **P4 — nomes múltiplos (não feito):** `FromMessageList` + `Addressees` + os filtros + harness. **Por último**
   entre as frentes principais (pedido do jogador, 03/11/2026): o grupo e a navmesh primeiro, porque são o que faz a
   obra andar; a lista de nomes é conveniência de comando.
6. **P5 — persistência/multiplayer/limpeza paralela (não feito).**

## 6. Invariantes e riscos

- **Main thread only**; claim/conclusão atômicos; nenhuma peça com dois donos; nenhum material pago duas vezes
  **pela mesma peça** (o material *contado* duas vezes entre worker é a limitação aberta, não uma peça paga duas
  vezes).
- **O "todos obedecem" dos modos sem nome não muda.** O `build` de um só continua para template/peça única, e o
  teste atual (1 companheiro) tem que continuar idêntico.
- `Pending`/`Reattach`/`HC_CompanionId`/`MHR_Owner` continuam sendo a base; o grupo é **em memória** no cliente
  do dono; outro jogador não entra.
- Com **≥2** workers o plano **não** é reordenado (`PlanLocked`), e a bancada vai para o **fim** — qualquer coisa que
  mude índices no meio do plano quebra os claims e o `Pending` (que guarda a **mesma lista**).
- O `MHR_Build.Active`/`OtherOrder`/harvest da limpeza respondem pelo **grupo** (`Worker`/`IsLeader`): o harvest só
  continua durante a fase `Clear` do líder.
- A invalidação da navmesh (P2) reduz a latência do piso novo (~5 s → ~0,3–1,3 s) e cobre a remoção; a
  **carência do mover** ataca os `no progress` medidos (36). Sem as duas, o grupo pode ter o mesmo `no progress`
  de hoje, só que com mais trabalhadores.
- **Pior caso de material declarado:** N × o resto do plano (cada worker conta mochila+baús por conta própria) e até
  3 coletando ao mesmo tempo. Não é bug — é o P1 sem `Stock`.

## 7. Critério de aceite

**Medido em jogo (04/11/2026, `CommHomeBasicBP`, 3 companheiros, `BlueprintFree = true`)** — quatro runs: 1 e 2 diferem
só no `PlaceReach` (10 = padrão do código; 15 = config de teste), o **Run 3** roda em 15 **com a guarda anti-loop**
(§17.8 de `docs/re/building-notes.md`), e o **Run 4** é o primeiro **com o deck** (`DeckLift = 1`, o padrão). O detalhe
está lá em §17.6, §17.9 e §17.10:

| | Run 1 (`PlaceReach = 10`) | Run 2 (`PlaceReach = 15`) | **Run 3 (15 + guarda)** |
|---|---|---|---|
| colocadas | **970** | **972** | **970** (`969` `done`) |
| fora | **15** | **13** (watchdog) | **16** (watchdog, nomeadas) |
| total | **fecham**: `970 peças, 15 puladas` (970 + 15 = **985**) | passada principal **987/987** | **986** (`970 peças, 16 puladas`) |
| mortes | 1 | **0** | **0** |
| log | — | — | **382 KB** (sem loop) |
| `no_ground` | **0** (era ~25) | — | — |
| `blocked_by_player` | **0** (era 17+) | — | — |
| `log ajustado` | **6** | — | — |

- **fechou:** os **contadores** (`Placed`/`Skipped` por índice → a fala final fecha com o plano), a **cumeeira** deixa de
  ser `no_ground` (~25 → **0**, `AttachedRadius` 4,0 m), o **companheiro não bloqueia** a colocação (17+ → **0**,
  `BlockedByCharacter`), o **nudge** age (`log ajustado` 6) e o **baú/cama** de mobília passam a ser ajustados; com
  15 m a ordem chega a **0 mortes** e a passada principal a **987/987**; o **Run 3** mostra **0 mortes e 382 KB de log**
  com a guarda anti-loop (o `290afc1` tinha feito 344 MB);
- **o grupo em si:** um plano por índices distintos (`a peça X já foi construída pelo grupo`), **promoção do líder**
  (`o líder do grupo ... saiu - outro companheiro assumiu`) e **retomada** no mesmo plano (`voltou para o grupo ... na
  peça X`) continuam valendo nos três runs;
- **portão de apoio:** as peças que só esperavam o apoio **não** gastaram tentativa (74 no primeiro run do dia) e as
  que não fecharam foram ditas pelo **watchdog** (15/13/**16**), cada uma **nomeada** (`sem apoio: <prefab> em (x, y, z)`);
- **Run 3 — o que sobrou aponta um bug de plano, não de worker:** as 16 nomeadas formam **colunas verticais**
  (`wood_beam_45` em y 37,8/39,8 a oeste; `wood_floor` 39,8/43,8 + `wood_roof_45` 41,8/45,8 a leste; 4 baús em 39,9) e
  a visão (`render_view`) mostrou o telhado **quase completo com um buraco no lado leste**. Causa: o plano ancora pela
  **origem do arquivo** e a fundação do blueprint está **1 m abaixo do terreno** — o jogo recusa a peça enterrada e tudo
  acima falha `no_support` em cascata. **Desenho novo** (substitui o clamp por peça do `0f13878`, que punha o piso no
  nível do chão): a **obra inteira sobe `[Building] DeckLift`** (**padrão 1**, faixa **0..3**, rótulo
  `hc_cfg_building_decklift`) e as **pontas de estaca** que ainda ficariam abaixo do terreno **assentam no chão**
  (`MHR_Build.cs:1102-1124`, log `a obra sobe X m (deck) - N peça(s) de fundação assentadas no terreno`). Sem a config o
  "subir pelo ponto mais baixo" levantava **2,00 m** (as estacas do arquivo estão enterradas 2 m). **Medido no Run 4.**
- **Run 4 — o primeiro com o deck:** ~**972 peças de pé**, **0 mortes** e o **deck confirmado** (piso do térreo ~**1 m**
  acima do chão, apoiado nas estacas, e as **escadas das entradas encontrando** o piso); **2 dos 3** workers terminaram
  com o watchdog em `0 peça(s) sem apoio` e o **terceiro** parou com **6** nomeadas — todas **telhado em 42,8/44,8/46,8**
  — mais **4** peças dadas por **`falls`**. A visão (`render_view`) mostrou a **faixa de telhado da água sul** faltando e
  a **empenha leste aberta**: o plano abaixo do terreno deixou de ser o problema, o que sobrou é a **camada de cima do
  telhado**. Diagnóstico e correção em `docs/re/building-notes.md` §17.10: **telhado apoia telhado** no pull
  (`BuildSupportBelow.CanSupport`) e **`falls` com uma tentativa em pose ajustada até 1 m**
  (`AdjustFallenPose`/`AdjustPose`). **Sem run** com o código novo.

**Coberto pelo código e ainda sem run dedicado:**
- claim expira em 20 s, renovado por frame; retry real teto de 3 por índice, 2 s; espera por dependência 8/12/20/25 s;
  watchdog de 60 s sem peça subir;
- seguidores em `WaitGroup` sem relógio e sem guarda de peso; líder que sai antes do `BuildPhase` abre a fase;
- plano **não** reordenado com ≥2; bancada anexada ao fim; `station: N precisam, M planejadas, K sem cobertura`;
- segunda ordem do jogador recusada (`SiteBusy`);
- nivelamento do blueprint devolve **os N** (`SetThenWorkers`/`GroupWorkers`) e `NoteLeveled` impede nivelar de novo.

**Falta medir:**
1. **O lote de 04/11 (o aceite que falta):** a **empenha leste** fechar e a **água sul** ganhar a faixa — as 6 de
   42,8/44,8/46,8 fora de `sem apoio: <prefab> em (x, y, z)` e em `done: ... supported`; `log ajustado ... - falls`
   aparecendo **antes** de um `skipping it` (a peça que cair de novo depois do ajuste é pulada — não há terceira
   chance); watchdog idealmente `0 peça(s) sem apoio` nos **3** workers; e o **apoio de emergência**
   (`[Building] SupportFallback`, `building-notes` §17.11) plantando o `wood_beam` sob as peças que o plano deixa sem
   apoio — `apoio de emergência: wood_beam sob <prefab> em (x, y, z)` no log, uma vez por peça e teto de 24.
2. **O deck (`DeckLift`) — medido:** Run 4 (~**972** de pé, **0 mortes**, piso ~1 m, escadas encontrando); o que ficou
   pendente dele é só o telhado do item 1. `DeckLift = 0` mantém o nível do arquivo (só assenta as estacas).
3. **As 13 peças do Run 2 — atacadas e sem run dedicado:** **2 baús** `piece_chest_wood` em (-168,2, 39,9, -434,9) por
   `too_steep`; **1 bancada** `piece_workbench` em (-178,3, 38,6, -431,7) por `clipping` (a âncora externa colide); parte
   do **andar de cima** (`wood_floor`/`wood_roof_45` em 43,8/45,8) por `no_support` até o watchdog. O lote do `290afc1`
   (nudge de mobília **5 m** com **3 anéis × 12**, `TryBenchAt` rejeitando `clipping`, apoio imediato) está no código e o
   Run 4 é o primeiro com ele — **nenhum baú e nenhuma bancada** entre as 6 nomeadas, então os dois primeiros itens
   parecem cobertos; o `no_support` do andar de cima era o **plano abaixo do terreno** (§17.9) e, agora, a **empenha**
   (§17.10). Aceite formal ainda não.
4. **Run com material pago** (sem `BlueprintFree`): 3 workers pagando peça por peça — é onde a falta de reserva de
   material aparece (o pior caso documentado é N× o resto do plano, com até 3 coletando ao mesmo tempo).
5. **2 companheiros num 6x6 (46 peças)** — o teste pequeno que fecha a mecânica sem o ruído de uma casa de 985 peças:
   as duas entram com a ordem sem nome, uma nivela/limpa, as duas constroem, "para" para as duas, matar uma não para a
   outra, e com nome só aquela começa.

**P4 (quando existir):** "Maria e João, construam a cabana aqui" → as duas; "Zé, colete 10 madeiras" → só o Zé;
"para, Maria" → só a Maria; "para" → todos; "Maria e João, ataquem o greydwarf" → as duas; "Maria, equipa a
picareta" → só a Maria; "Maria e o cachorro, ..." → Maria + aviso do desconhecido, sem inventar.

**Gate:** harness **549** (471 + 23 da seção **(p)** + 26 da **(q)** + **29** da **(r)**), MCP **101** e build **0/0**.

## 8. Perguntas para o jogador — respostas registradas (03/11/2026)

1. `build` **sem nome** com vários companheiros: **todos entram no grupo** (os que podem receber ordem). Com nome,
   só o nomeado.
2. Nome **desconhecido** na lista ("Maria e o cachorro"): **continua aberta** — só faz sentido no P4 (hoje a lista
   não existe; um nome desconhecido não acha endereçado e o blueprint sem endereçado vai para todos).
3. "para, Maria" **tira só a Maria** e o grupo continua; "para" **derruba todos**. Implementado.
4. Um modo (ex.: "Zé, colete") **tira só o Zé** e os outros continuam. Implementado.
5. Persistir o grupo no save: **fica para depois** (P5). O grupo é em memória.
6. Limpeza/nivelamento em paralelo: **um só no P1** (o líder); os N voltam **depois** do nivelamento. P5.

## 9. Referências

- `Jobs/MHR_BuildGroup.cs` — o canteiro: `KeyOf`, `SiteBusy`, `TryCreate`/`Join`/`Leave`, claims (`TryClaim`,
  `Refresh`, `Release`, `ReleaseAll`), `MarkDone`, `TryQueueRetry`/`NextRetry`, `IsLeader`, `BuildPhase`.
- `Jobs/MHR_Build.cs` — `StartBlueprintGroup` (`:1281`), `WaitGroup` (`:1906`), `WaitRetry` (`:1961`), `Advance`
  (`:767`), `QueueRetry` (`:491`), `CountPlaced` (`:938`), `PlanLocked` (`:908`), `IsLeader` (`:918`),
  `InsertBenchStep` (`:2354`), `RejoinGroup` (`:1697`), `NamesOf`/`GroupWorkers` (`:1409`/`:1430`), `Pending.Group`,
  `StallSeconds`/`StallOut` (`:85`/`:2017`), `DependencyDelay`/`GroundUnder` (`:581`/`:573`), `PullDirectSupport`/
  `DirectSupportIndex` (`:3094`/`:3128`), `NudgePiece` (`:4148`), `AdjustFallenPose`/`AdjustPose` (`:4172`/`:4200`) e
  as guardas `State.PulledSupport`/`State.FallAdjusted` (`:213`/`:217`).
- `AI/CompanionOrders.cs` — `Run` (modos), `Build` (`:1161`, o blueprint em `:1295`), `BlueprintWorkers` (`:1381`),
  `LevelOrder`, `IsNotAddressee`; o cancelamento de build de qualquer outra ordem (`:229-240`).
- `AI/BuildFailure.cs` — **o portão de apoio** (04/11/2026): `Classify`/`IsDependency` puros (a tabela da seção **(p)**
  do harness), sem Unity e sem config; o mundo entra por `MHR_Build.GroundUnder` (`:573`).
- `AI/BuildPieceKind.cs` — **estrutural × mobília** (04/11/2026, seção **(q)** do harness): `Classify`/`IsStructural`
  por nome do prefab; decide o raio do `NudgePiece` (**1 m** × **5 m**) e a busca larga (3 anéis × 12) em
  `MHR_Build.cs:125-129`/`:4159-4160`.
- `AI/BuildSupportBelow.cs` — **o apoio direto de uma peça** (04/11/2026, seção **(r)**, **29 casos**): `IsDirectSupport`
  (0,4–2,5 m **abaixo** × 2,5 m em XZ) e `Rank` (a queda pesa 10×), mais o `CanSupport` (`:72`) — **telhado apoia
  telhado** (a fileira de baixo da água), telhado no piso/parede/escada, **porta nunca apoia** e piso/parede **não**
  apoiados por telhado; é a decisão do `PullDirectSupport` (`MHR_Build.cs:3094`/`:3152`), cuja guarda é
  `State.PulledSupport` (`:213`/`:3110`, uma vez por índice e teto de 24).
- `AI/BuildProbe.cs` — `AttachedRadius` **4,0 m** (`:49`, `AttachedToStructure` em `:641`) e
  `BlockedByCharacter` (`:705`: companheiro com `CompanionAI` não conta como jogador).
- `Jobs/MHR_Level.cs` — `SetThenWorkers` (`:138`) e a retomada com `GroupWorkers`/`NoteLeveled` (o `NoteLeveled` é do
  `MHR_Build`, `:315`, chamado em `MHR_Level.cs:477`).
- `AI/BuildExecutor.cs` — `PlacedRadius`/`PlacedYaw` e `VerifyAt` (`:65-68`, `:278`).
- `DevBridge/Commands.cs` — `CompanionsBuild` (o ramo do blueprint, `:1107-1193`).
- `AI/CompanionNames.cs` — `FromMessage` (um nome), `Find`, `CompanionNameMatch` (P4).
- `Patches/MHR_CompanionNavMesh.cs` — agente 40, `BuildTile` sem portas; o `OnPlaced`/`Destroy` chegou na P2.
- `docs/re/navegacao-companheiro.md` §6 — o que o Vikings for Hire faz (invalidação/links).
- `docs/plan-construcao.md` — a trilha E; este plano é a extensão multi-trabalhador.
