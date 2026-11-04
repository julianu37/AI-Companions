# Plano — vários companheiros na mesma obra (multi-build)

**Status (04/11/2026):** **P1 MEDIDO EM JOGO** na branch `fix/pisos-nivel-2` (3 companheiros na `CommHomeBasicBP`,
`BlueprintFree = true`): com `PlaceReach = 10` (padrão) **970 + 15 = 985** peças — a fala final fecha, **`no_ground`
~25 → 0**, **`blocked_by_player` 17+ → 0**, `log ajustado` **6**, 1 morte; com `PlaceReach = 15` (config de teste) a
passada principal chegou a **987/987**, **972 colocadas** e **0 mortes**, sobrando **13**; no **Run 3** (15 + a
guarda anti-loop) **`970 peças, 16 puladas`** = **986**, **0 mortes** e **382 KB** de log, e as 16 nomeadas revelaram
o **plano abaixo do terreno** — o desenho novo é a **obra inteira subir `[Building] DeckLift`** (padrão 1) com as
pontas de estaca assentadas no chão, **medido no run seguinte** (§7 e `docs/re/building-notes.md` §17.10: ~**972 peças
de pé**, **0 mortes**, piso ~**1 m** acima do chão e escadas das entradas encontrando — o deck está certo; sobraram
**6 telhados** em 42,8/44,8/46,8 e **4 `falls`**, atacados no lote de 04/11 e medidos no **Run 8** — `UNSUPPORTED` = 0 e
**0 `falls`** (§7, item 1)). O código é
`Jobs/MHR_BuildGroup.cs` mais
`StartBlueprintGroup`/`Phase.WaitGroup`/`Phase.WaitRetry`/`QueueRetry`/`MarkDone`/`IsLeader`/`PlanLocked` em
`Jobs/MHR_Build.cs`, e o ciclo de 04/11 fechou o **portão de apoio** (`AI/BuildFailure.cs`), o **watchdog de
estagnação**, a **cobertura de bancadas** e o validador (`attached`/`nudge`/`blocked_by_player`) — detalhe medido em
`docs/re/building-notes.md` §17. **P0** (quem tem 1 companheiro não vê diferença) e **P2** (navmesh/latência,
**medida em jogo** em 03/11/2026) fechados; **P3** (UI do grupo), **P4** (nomes múltiplos) e **P5**
(persistência/limpeza paralela) **não**. **Estado de 11/11/2026 (Runs 7–11, §7):** o lote do poste fechou o degrau de
0,6 m (**Run 8**, 987/987), o **Run 9** rodou com o **jogador andando** no canteiro → **977 + 11 = 988** (as 11 puladas
medidas em 2 causas), o **Run 10** **mediu a barreira de andar** (`[Building] BodyRetry`, lote `fbda4ee`) → **989 + 1 =
990**, com puladas **11 → 1** (a única é a **7ª bancada** com `clipping`, e é ela que fecha a conta: **983 + 7 = 990**),
e o **Run 11** é o **primeiro run com `0 puladas`** — **987 + 0 = 987** (`987 peças, 0 puladas`), com a sonda por
**cápsula × caixa girada** e a **`[Building] StepAside` ligada**, **0 `UNSUPPORTED`**, **0 mortes**, **0 `falls`**,
watchdog **0**, **1020 peças** no canteiro (**987 + 33 postes**) e **23/23 prefabs** do blueprint no lugar. O
`blocked_by_player` **não** se compara direto (Run 9 **235 terminais** × Run 10 **141 terminais + 224 curtas** × Run 11
**98 terminais + 145 curtas**, com sonda e barreira madura); **o "bloqueador companheiro" do Run 10 era falso positivo da
sonda** (peça tratada como disco de 1 m): a sonda agora é a **cápsula real × caixa girada** e a `StepAside` (2,2 m,
nunca o jogador) é a **segunda linha** — **as duas medidas no Run 11**; aberto: a sonda **não testa criaturas** e as
**2 `stone_floor_2x2`** do arquivo, que são **teto do blueprint** nesta instalação (receita do talhador de pedras).

Números deste ciclo (11/11/2026): build **0/0**, harness `tools/ai-intents-check` **642 asserções** (471 + **28** da
seção **(p)**, `AI/BuildFailure.cs` — inclui as constantes da barreira —, + **26** da **(q)**, `AI/BuildPieceKind.cs`,
+ **66** da **(r)**, `AI/BuildSupportBelow.cs` — `CanSupport`, `PickPost`, `UsefulSurface` e `HoldsPlacedPiece` —,
+ **18** da **(s)**, `AI/BuildBody.cs` — `Blocks`, `HalfY` e o par do **falso positivo** do Run 10 —, + **27** da **(t)** — a **barreira
de andar** — e + **6** da
**(u)** — `BuildSupportBelow.FallbackPostAllowed`, `SupportGrace` = **2**), teste do MCP **107** (as **12** asserções de
03/11 cobrem o contrato
do `companions.build` do grupo e as **6** de 11/11 o `stop` do `companions.order`), config **363 opções / 25 seções**
(as chaves novas são a `[Building] BodyRetry` e a `[Building] StepAside`),
ES/FR/IT **0 lacunas**, dívida do `gen_translations.py --check` **47** por idioma e **nenhuma chave de tradução funcional
nova** (as recusas reusam `hc_ai_no_companion` e `hc_ai_build_failed`; os rótulos `hc_cfg_building_bodyretry` e
`hc_cfg_building_stepaside` são os das chaves novas). **O Run 11 não acrescenta número de build/harness/config:** ele roda
na árvore já medida acima (**harness 642**, **config 363/25**) e o que traz é **medição em jogo** — não foi feita
reconferência de build/harness nesta rodada (§17.17).
**O que falta medir** está no §7 (o **`FallbackPostAllowed`**, o vão do deck, run com material pago, o teste pequeno de
2 companheiros num 6x6) — a **barreira de andar** foi medida no Run 10 (§7 e `docs/re/building-notes.md` §17.16) e a
**sonda por cápsula** + o **`StepAside`** no Run 11 (§7 e §17.17).

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
  (`AI/BuildSupportBelow.cs:145`, lote de 04/11/2026): **telhado apoia telhado** (a fileira de baixo da água — sem isso a
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
teste mock do MCP (**107 PASS**, **12 asserções para o contrato do grupo** em 03/11/2026 e **6** do `stop` em
11/11/2026) cobre justamente esse contrato.

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
   que subiu de 2,5 m e passou a varrer 3 anéis × 12) e o **poste de emergência** (`SupportFallback`: base na superfície
   de baixo, comprimento pelo **vão** via `PickPost` — ver §17.11). **`blocked_by_player`: o ignore de companheiro foi
   REVERTIDO** — a peça era colocada **dentro** do companheiro e ele **afundava**; hoje companheiro **é obstáculo**
   (`BlockedByCharacter`, `BuildProbe.cs:719-729`), o bloqueio voltou a aparecer (**20** no run de 5) e é **retry**
   (falha real: gasta uma das 3, não é pulada na hora). **Medido nos runs (§7):**
   `PlaceReach = 10` → **970/985** (15 puladas, `no_ground` ~25 → **0**, `blocked_by_player` 17+ → **0** *naquele
   build, com o ignore ligado*, `log ajustado` 6, 1 morte); `PlaceReach = 15` (só config) → passada principal
   **987/987**, **972 colocadas** e **0 mortes**; **Run 3** (15 + guarda anti-loop) → **`970 peças, 16 puladas`** =
   **986**, **0 mortes** e **382 KB** de log; **Run 4** (com o **deck**, `DeckLift = 1`) → ~**972 peças de pé**, **0
   mortes**, deck certo, **6 telhados** nomeados e **4 `falls`**; **Run 5** (5 companheiros, antes do poste por vão) →
   **972 + 13 = 985**, watchdog **0**, **0 mortes**, `apoio de emergência` **0**; **Run 6** (11/11, com os dois lotes)
   → **969 + 18 = 987**, watchdog **0**, **telhado fechado**, **0 postes**, **1 morte**, `blocked_by_player` ≈**770**
   (o corpo conta e o jogador estava no canteiro); **Run 7** (11/11, o degrau de 0,6 m com `MaxOverlap` 0,35) →
   travou em **950/987**; **Run 8** (11/11, validação do lote do poste) → **987/987**, **0 puladas**, **39 postes**,
   **`UNSUPPORTED` 0**, **watchdog `0`**, **0 mortes**; **Run 9** (11/11, o jogador **andando** no canteiro) →
   **977 + 11 = 988**, **watchdog `0`**, **`UNSUPPORTED` 0**, **1 morte** (queda de telhado, **ignorada por decisão
   do jogador**) e as **11 puladas** com **2 causas medidas** (1 portão por corpo + 10 numa coluna que esperou um
   apoio **do plano** que não estava no **mundo**). **Fechado depois:** o lote das 13 (nudge de mobília 5 m/3×12,
   `TryBenchAt` sem `clipping`, apoio imediato), o **deck (`DeckLift`)**, o **poste por vão** (`PickPost` +
   `UsefulSurface`) e a **regra do telhado** (`HoldsPlacedPiece` — **inócua**: o `m_supports` do telhado é `true`,
   `docs/re/support-rule-wearntear.md` §5), e o **degrau de 0,6 m** (o run do degrau travou em **950/987** —
   `building-notes` §17.12 — e o **Run 8** fez os pisos de **40,4** subirem em cima de **39** postes, §17.14).
   **Aberto:** o **bloqueador companheiro** da barreira — o caso do Run 10 era **falso positivo** da sonda (disco de
   1 m no lugar da peça), corrigido pela **cápsula real × caixa girada**; a `StepAside` **rodou ligada no Run 11** e a
   sonda por cápsula **é a do Run 11** (as duas **medidas**, §17.17 — o que resta aberto nelas é a **criatura** e a
   **contagem** das linhas do step-aside); a barreira em si foi **MEDIDA**: puladas
   **11 → 1** no Run 10 (`building-notes` §17.16) e **1 → 0** no Run 11 (§17.17), o **vão do deck** (`DeckLift = 1` prende
   corpos; 3 opções, decisão do jogador — §17.13), a **reserva de material** (`Stock` — cada worker conta
   o canteiro por conta própria), um run com **material pago** e o teste pequeno de 2 companheiros num 6x6.
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

**Medido em jogo (04/11/2026, `CommHomeBasicBP`, `BlueprintFree = true`)** — cinco runs: 1 e 2 diferem só no
`PlaceReach` (10 = padrão do código; 15 = config de teste), o **Run 3** roda em 15 **com a guarda anti-loop** (§17.8 de
`docs/re/building-notes.md`), o **Run 4** é o primeiro **com o deck** (`DeckLift = 1`, o padrão) e o **Run 5** é o de
**5 companheiros**, antes do poste por vão. O detalhe
está lá em §17.6, §17.9 e §17.10 (**os Runs 6, 7, 8, 9 e 10, de 11/11/2026, vêm nos bullets abaixo** — §17.12 a §17.16):

| | Run 1 (`PlaceReach = 10`) | Run 2 (`PlaceReach = 15`) | **Run 3 (15 + guarda)** |
|---|---|---|---|
| colocadas | **970** | **972** | **970** (`969` `done`) |
| fora | **15** | **13** (watchdog) | **16** (watchdog, nomeadas) |
| total | **fecham**: `970 peças, 15 puladas` (970 + 15 = **985**) | passada principal **987/987** | **986** (`970 peças, 16 puladas`) |
| mortes | 1 | **0** | **0** |
| log | — | — | **382 KB** (sem loop) |
| `no_ground` | **0** (era ~25) | — | — |
| `blocked_by_player` | **0** (era 17+) — **revertido depois**, ver o Run 5 | — | — |
| `log ajustado` | **6** | — | — |

- **fechou:** os **contadores** (`Placed`/`Skipped` por índice → a fala final fecha com o plano), a **cumeeira** deixa de
  ser `no_ground` (~25 → **0**, `AttachedRadius` 4,0 m), o **nudge** age (`log ajustado` 6) e o **baú/cama** de mobília
  passam a ser ajustados; com 15 m a ordem chega a **0 mortes** e a passada principal a **987/987**; o **Run 3** mostra
  **0 mortes e 382 KB de log** com a guarda anti-loop (o `290afc1` tinha feito 344 MB);
- **o padrão `blocked_by_player` foi revertido:** o lote que ignorava companheiro no `BlockedByCharacter` foi **tirado**
  — a peça era colocada **dentro** do companheiro e ele **afundava** (regressão registrada). Hoje companheiro **é
  obstáculo** (`MinSpacing` 0,6 m) e o `blocked_by_player` **voltou**: **20** no Run 5, **retry** (falha real no
  portão, gasta uma das 3 tentativas) e não perda imediata — a peça sobe quando ele sai de cima;
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
- **Run 5 — 5 companheiros, antes do poste por vão:** **972 colocadas + 13 puladas = 985**, watchdog **0**, **0
  mortes**, `blocked_by_player` **20** (o companheiro **é** obstáculo de novo — ver a reversão acima) e
  **`apoio de emergência` 0**: o `apoio imediato` disparou **24×** puxando o piso de baixo, mas a coluna terminava
  **sem base** — o poste fixo de 2 m ficava com **1 m enterrado** e o jogo recusava o próprio poste com `no_support`
  (`sem lugar para o apoio de emergência … - no_support`). Lote desta rodada: o poste passou a ser **escolhido pelo
  vão** — base na superfície de baixo (`Physics.Raycast` 10 m), comprimento pelo **`PickPost`** (mais curto que
  alcança: até **0,20 m mais curto** / **0,35 m mais longo**), candidatos `wood_beam_1`/`wood_beam`/`wood_pole2`/
  `wood_beam_45` com o comprimento medido dos snaps; sem peça vertical de ~4 m em vanilla, vão > ~3,1 m **não recebe
  poste** (`docs/re/building-notes.md` §17.11). **Sem run** com o poste novo.
- **Run 6 — 11/11/2026, 5 companheiros, `DeckLift = 1`, `BlueprintFree`, com os dois lotes (poste por vão + telhado):**
  **969 colocadas + 18 puladas = 987**, **watchdog `0`**, canteiro com **985 de 987**, **`apoio de emergência` = 0**
  (não era apoio que faltava), **telhado visualmente fechado** (`render_view`), **1 morte** e `blocked_by_player`
  **≈770** — o **corpo conta** (janela do `BuildBody`, `building-notes` §17.11) e o **jogador estava dentro do
  canteiro**. O lote do telhado: `FootprintSupported` exigia `WearNTear.m_supports` e ganhou o bypass de classe 3 em
  `BuildSupportBelow.HoldsPlacedPiece` (`:172`) — **o motivo escrito na época ("telhado tem `m_supports = false`") está
  ERRADO**: medido na 1.0.20 todo telhado de madeira é `m_supports = true`, o bypass é **inócuo** e a causa das 13
  peças em `no_support` era **geometria** (`docs/re/support-rule-wearntear.md` §5, `building-notes` §17.12). O run do
  **degrau de 0,6 m** do mesmo dia **travou em 950/987** com o `no_support` **certo** (8 pisos + 12 baús sem apoio
  possível, `wood_floor` 297/298/304 repetindo) — naquele build o poste não cobria o vão porque o `MaxOverlap` era
  **0,35**. O lote do poste (`ae9ebc3`, 11/11/2026) **fechou isso**: `MaxOverlap` **0,45**
  (`AI/BuildSupportBelow.cs:119`), janela do `wood_beam_1` `[0,55; 1,20]`, mais o `holds` na base, a guarda de mobília e
  o aceite só com `no_support` único (`building-notes` §17.14); o run de validação é o **Run 8**, abaixo.
- **Run 8 — 11/11/2026, a validação do lote do poste (5 companheiros, `DeckLift = 1`, `BlueprintFree`, `PlaceReach`
  15, `SupportFallback` ligado, `TempScaffold` desligado):** **987 de 987 índices colocados e `skip` = 0**,
  **39 postes de emergência com 0 recusas**, **1026 peças** no canteiro (**987** do plano = 983 do arquivo + **4
  bancadas** + **39** postes), **watchdog `0`**, **`UNSUPPORTED` = 0**, **0 mortes**, **0 `falls`**, **telhado
  fechado** e **empena leste fechada** nas três visões; `blocked_by_player` **120** (116 voltaram à fila + 4
  `desistindo`, todas recuperadas por outro worker) e `no progress` **11**; o **jogador ficou fora** do canteiro,
  então o bloqueio foi
  **companheiro contra companheiro** (`BuildProbe.cs:724-753` conta os dois). O **degrau de 0,6 m** subiu: os
  `wood_floor` de **40,4** que o Run 7 deixou em `no_support` ganharam poste de 1 m (log
  `apoio de emergência: wood_beam_1 (1,00 m) sob wood_floor em (…)`, 6 deles em 40,4 e o resto em 39,3/41,4/44,4/
  45,4/46,4; **1** é `wood_beam_45` de **2,83 m**). Contadores do **grupo** (`Placed`/`Skipped` são **conjuntos de
  índices**): o `Finish` loga só a chave e a fala final não tocou, então o `987`/`0` **não** aparece no log. As **2
  linhas** que o parser descarta são `stone_floor_2x2` (`recipe not unlocked` — receita do **talhador de pedras**,
  `build.inspect` medido), limite do arquivo/save: é o que fecha a **conta do arquivo** (985 linhas − 2 = 983 → + 4
  bancadas = 987 índices) que aparecia como "2 peças" não explicadas desde o Run 6. Detalhe em
  `docs/re/building-notes.md` §17.14.
- **Run 9 — 11/11/2026, o jogador andando no canteiro (5 companheiros, `DeckLift = 1`, `BlueprintFree`,
  `PlaceReach = 15`, `SupportFallback` ligado, `TempScaffold` desligado):** a **única** diferença para o Run 8 é que o
  jogador foi posicionado **uma vez** antes da ordem e depois **andou livre** (a guarda de posição foi removida —
  pedido dele: *"pra que eu possa andar com ele pelo jogo depois sem ser teleportado"*). **977 colocadas + 11 puladas
  = 988**, com a **fala final medida** (`977 peças, 11 puladas` — no Run 8 ela não tocou), **watchdog `0`**,
  **`UNSUPPORTED` = 0**, **1023 peças** no canteiro, **5 bancadas** (o Run 8 tinha 4) e **`blocked_by_player` 235**;
  **1 morte** — o companheiro 4 caiu **8,1 m do telhado** na peça **962/988**, deixou **lápide com 26 itens** e o
  **próprio companheiro 4 encerrou a ordem** depois do respawn (`hc_ai_blueprint_done`), o `OnDeath`/`Reattach` do
  §15.2t funcionando com o corpo novo (leitura do log); a queda é **ignorada por decisão do jogador**, não é
  investigação aberta. As
  **11 puladas** têm **2 causas medidas**: **1 `wood_gate`** em (-163,3, 40,8, -422,9) por `blocked_by_player`
  repetido (era onde o jogador estava parado — o portão da entrada) e **10 numa coluna em x≈-152,7** (3 `wood_floor`
  em 39,8/40,8/40,8, 3 `wood_roof_45` em 42,8 e 4 `piece_chest_wood` em 40,9) em `no_support` até o watchdog. **A
  cascata:** a vizinhança está de pé (5 `wood_beam_1` em 39,3 = postes, pisos em 39,8, vigas em 39,8/40,8/41,8) e o
  que faltou foi **o tile de 39,8**, recusado **por corpo** e mandado ao retry; o piso de **40,8** vê peça **do
  plano** embaixo (`DirectSupportIndex`, `MHR_Build.cs:3177`) e por isso **não pede poste** (`:3275-3278`, "nunca
  onde o plano já tem apoio") — mas essa peça **não estava no mundo**, então a coluna congelou. Não é o `no_support`
  do §17.12: o apoio existia **no plano**, não no **mundo**. Detalhe em `docs/re/building-notes.md` §17.15.
- **Run 10 — 11/11/2026, a barreira de andar MEDIDA (lote `fbda4ee`; 5 companheiros, `DeckLift = 1`, `PlaceReach = 15`,
  `BlueprintFree`, `SupportFallback` ligado, `TempScaffold` desligado e **`[Building] BodyRetry = true`**, o padrão):** a
  variável é a mesma do Run 9 — o jogador foi posicionado **uma vez** em **(-166,92, 39,92, -401,59)** e depois **andou
  livre**, chegando a **(-180, 38,2, -423)** e **entrando no canteiro** em vários momentos. **989 colocadas + 1 pulada =
  990** (a fala final **tocou**: `989 peças, 1 puladas`), **watchdog `0`**, **`UNSUPPORTED` = 0**, **1022 peças** no
  canteiro (**989 + 33 postes**), **33 postes** de emergência com **0 recusas**, **7 bancadas** inseridas (**6 de pé** +
  a **7ª** recusada) e **23/23 prefabs do
  blueprint no lugar** (`accept_run.py`: `TODAS AS PECAS DO BLUEPRINT NO LUGAR`); **1 morte**, ignorada por decisão do
  jogador. **A única pulada NÃO é peça da casa:** `piece_workbench` em (-156,5, 38,4, -410,6) por **`clipping`** — a
  **7ª bancada** do `PlanStations`/`InsertBenchStep` (as **outras 6** subiram) e o índice que **fecha a conta** (**983 do
  arquivo + 7 = 990**; o run nasceu com **986** e **4** bancadas entraram durante a obra). **Frente ao Run 9
  (mesma config, mesma casa):** puladas **11 → 1**, watchdog/`UNSUPPORTED` **0/0 → 0/0** e canteiro **1023 → 1022** — o
  `blocked_by_player` **não** se compara (Run 9 **235 terminais**, sem curta naquela árvore, × Run 10 **141 terminais +
  224 curtas**) — e **16 peças** chegaram a **`curta 3/3`** e voltaram **antes** de o plano subir: o plano
  **andou** depois das curtas (peças **367-379** colocadas) e as paredes deixaram de subir com o térreo aberto. É o
  defeito do Run 9 resolvido. Detalhe (constantes, linhas de log, a tabela e a ressalva de contagem): `docs/re/building-notes.md`
  §17.16.
- **Run 11 — 11/11/2026, o primeiro run com `0 puladas` (sonda por cápsula × caixa girada + os dois furos da barreira;
  5 companheiros, `DeckLift = 1`, `PlaceReach = 15`, `BlueprintFree`, `[Building] BodyRetry = true` e
  `[Building] StepAside = true`):** a chave que **distingue** este run do Run 10 é a `StepAside` — ela **não existia**
  na árvore do Run 10 (lote posterior a `fbda4ee`). Variável do jogador igual à do Run 9: posicionado **uma vez** em
  **(-166,92, 39,92, -401,59)** e depois **andando livre**, **entrando no canteiro várias vezes** e **subindo até y 45**
  (em cima da obra). **987 colocadas + 0 puladas = 987** (a fala final **tocou**: `987 peças, 0 puladas`), **watchdog
  `0`**, **`UNSUPPORTED` = 0**, **0 mortes**, **0 `falls`**, **1020 peças** no canteiro (**987 + 33 postes**), **4
  bancadas** inseridas e **todas colocadas** — a **7ª bancada** com `clipping` do Run 10 **não apareceu** — e **23/23
  prefabs do blueprint no lugar** (`accept_run.py`). Corpo: **98** recusas terminais (`blocked_by_player`) + **145**
  curtas (2,5 s), com a **barreira disparando 56×** (`nada acima de … m é pego por … s`). **O plano do Run 11 tem 987
  índices** (983 do arquivo + **4** bancadas) contra **990** (983 + **7**) do Run 10 — **menos bancadas**, então o total
  absoluto é menor: o número que conta é **puladas 1 → 0**. **Fora do plano continuam só as 2 `stone_floor_2x2`**, puladas
  na **leitura** (`recipe not unlocked`, `MHR_Build.cs:1502-1509`): são peça do **talhador de pedras** (alcance 20 m),
  exigem **pedra no inventário do jogador** (vazio, medido) e o catálogo do mod (`AI/BuildCatalog.cs:40-72`, **5**
  prefabs) **não constrói talhador** — é o **teto do blueprint** nesta instalação, não falha da obra. Detalhe (tabela
  Run 10 × Run 11, o que não foi contado): `docs/re/building-notes.md` §17.17.
- **A sonda de corpo e o `StepAside` — MEDIDOS no Run 11:** o "bloqueador companheiro" do Run 10 era **FALSO POSITIVO** da
  sonda; o caso medido (`wood_beam` em (-168,1, 41,8, -421,1) × **Companheiro 6** em (-167,1, 39,9, -421,5) —
  **1,08 m** em XZ e **1,9 m** abaixo) **não** era corpo atravessando peça: `BuildProbe` calculava
  `flat = max(size.x, size.z)/2` e tratava a peça como um **disco de 1 m em qualquer direção**, ignorando **pivô e
  rotação** — um feixe de 2 m virava disco de 1 m e batia no companheiro 1,08 m ao lado. O jogo testa o ghost contra a
  **cápsula do `Character`** (`Player.cs:4195-4225`, `ComputePenetration`). **O conserto não é número:** a sonda passou a
  usar a **cápsula real** (`Collider.bounds.min.y`/`max.y` + raio, `BuildProbe.CapsuleOf`, `AI/BuildProbe.cs:813`) contra
  a **caixa girada** da peça (`center = point + rot*(box.center − piece.transform.position)`, AABB do OBB pelo
  `BuildBody.HalfY`, `:733-793`), com **toque = livre**; o log imprime os números na 1ª recusa (`corpo na peça X -
  cápsula 0,00-1,80 m, r 0,30`). A **`[Building] StepAside`** (bool, padrão true, `Core/ModConfig.cs:174`/`:962`) é a
  **segunda linha de defesa**: quem coloca **pede** (`MHR_BuildGroup.RequestStepAside`, dedup de **4 s**) e o companheiro
  **em espera** anda **2,2 m** com o próprio mover (8 direções, `MHR_Mover.SafeStep`; passo de até **2,5 s**) — **nunca
  teleporte, nunca o jogador**; desligada, a peça espera (o comportamento do Run 10). **Medido no Run 11:** as duas
  rodaram no run de **0 puladas** — a sonda é a que conta as **98** recusas terminais e a `StepAside` estava **ligada**
  o tempo todo; a **contagem** das linhas `pedindo para sair da frente`/`saindo da frente` **não** foi medida. **Em
  aberto:** a sonda **não testa criaturas** (um javali em cima da peça ainda passa) — `docs/re/building-notes.md`
  §17.16/§17.17.
- **Ressalva do Run 6 + pendência aberta (o vão do deck):** depois do Run 6 o arquiteto **apagou 41 peças do piso do
  deck** (**25 + 16**) para **libertar o jogador e os companheiros presos no vão de ~1 m**. O **Run 8** voltou a
  colocar o **plano inteiro** (987/987 índices) no mesmo canteiro, então o piso apagado **entrou de novo** — é
  inferência dos índices colocados, não uma contagem de `build.pieces`. O **vão do deck (`DeckLift = 1`)** continua a
  pendência: (1) `DeckLift = 0`
  (sem deck), (2) **saia em volta** fechando o perímetro, (3) **regra de rota** para o mover não entrar/parar sob o
  deck. A decisão é do jogador (`building-notes` §17.13).

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
   chance); watchdog idealmente `0 peça(s) sem apoio` nos **3** workers; e o **poste de emergência**
   (`[Building] SupportFallback`, `building-notes` §17.11) subindo **pelo vão** — log
   `apoio de emergência: <prefab> (<len> m) sob <peça> em (…)`, uma vez por peça e teto de 24; sem poste que alcance
   (vão > ~3,1 m) ele diz `- vão de X,XX m` e a peça segue a fila.
   **Atendido pelo Run 8 (11/11/2026):** a **empenha leste** aparece **fechada** nas três visões, o **poste de
   emergência** subiu **39 vezes** (`apoio de emergência: wood_beam_1 (1,00 m) sob …`, **0** recusas) e o apoio das
   peças de pé passou a ter o veredito do jogo: **`UNSUPPORTED` = 0**. **Fica em aberto daqui:** a **água sul** (as
   visões do Run 8 foram lidas como "telhado fechado + empena leste", a faixa sul **não** foi reportada à parte) e o
   `log ajustado … - falls` **antes** de um `skipping it` — o Run 8 teve **0 `falls`**, então esse caminho **não foi
   exercitado**.
2. **O deck (`DeckLift`) — medido e com pendência aberta:** Run 4 (~**972** de pé, **0 mortes**, piso ~1 m, escadas
   encontrando) e Run 6 (11/11, telhado fechado); o que falta é o **vão do deck**: `DeckLift = 1` cria ~1 m entre o piso
   e o chão e **prende corpos** (41 peças do piso foram **apagadas** para libertar o jogador e os companheiros). Opções:
   `DeckLift = 0`, saia em volta ou regra de rota no mover — **decisão do jogador** (`building-notes` §17.13).
   `DeckLift = 0` mantém o nível do arquivo (só assenta as estacas).
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
6. **A sonda de corpo e a `StepAside` — MEDIDAS no Run 11 (11/11/2026, §17.17):** a barreira foi medida no Run 10
   (puladas **11 → 1** — §17.16) e o "bloqueador companheiro" daquele run era **falso positivo** do disco. A sonda agora é
   a **cápsula real × caixa girada** (a regra do jogo, com **toque = livre**) e o `StepAside` (2,2 m, via mover, nunca o
   jogador) é a **segunda linha** — as duas rodaram no **Run 11**, o primeiro com **`0 puladas`** (`987 peças, 0 puladas`,
   98 terminais + 145 curtas, barreira disparada **56×**). **Aberto:** a sonda **não testa criaturas** e a **contagem** das
   linhas do step-aside **não** foi feita (o que se mediu é o resultado com a chave ligada, não quantas vezes ele andou).
   **Também sem run:** o `FallbackPostAllowed` (o poste de emergência assume o apoio **do plano** quando ele foi desistido
   ou quando ninguém o reivindica e a peça já esperou `SupportGrace` = 2, ~20 s) — `docs/re/building-notes.md` §17.16.
7. **As 2 `stone_floor_2x2` do arquivo (teto do blueprint, com o porquê medido):** são descartadas na **leitura**
   (`recipe not unlocked`, `MHR_Build.cs:1502-1509`) porque a receita é do **talhador de pedras** (alcance 20 m) e ela
   exige **pedra** — o inventário do jogador está **vazio** (medido) — e o catálogo do mod (`AI/BuildCatalog.cs:40-72`,
   5 prefabs) **não constrói talhador**. Para exercitar esse caminho é preciso **pedra no jogador + talhador de pé a
   ≤20 m** do canteiro; **não testado** em nenhum run. §17.17.

**P4 (quando existir):** "Maria e João, construam a cabana aqui" → as duas; "Zé, colete 10 madeiras" → só o Zé;
"para, Maria" → só a Maria; "para" → todos; "Maria e João, ataquem o greydwarf" → as duas; "Maria, equipa a
picareta" → só a Maria; "Maria e o cachorro, ..." → Maria + aviso do desconhecido, sem inventar.

**Gate:** harness **642** (471 + **28** da **(p)** + 26 da **(q)** + **66** da **(r)** + 18 da **(s)** + **27** da **(t)**
+ **6** da **(u)**), MCP **107** e build **0/0**.

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
  `InsertBenchStep` (`:2403`), `RejoinGroup` (`:1697`), `NamesOf`/`GroupWorkers` (`:1409`/`:1430`), `Pending.Group`,
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
