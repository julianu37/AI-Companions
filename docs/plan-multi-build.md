# Plano — vários companheiros na mesma obra (multi-build)

**Status (03/11/2026):** **P1 MVP implementado** na branch `fix/pisos-nivel-2` (**não commitado**), **sem teste em
jogo**. O código novo é `Jobs/MHR_BuildGroup.cs` (arquivo novo) mais `StartBlueprintGroup`, `Phase.WaitGroup`,
`Phase.WaitRetry`, `Advance`/`QueueRetry`/`MarkDone`/`IsLeader`/`PlanLocked` em `Jobs/MHR_Build.cs`. **P0** (quem tem
1 companheiro não vê diferença) e **P2** (navmesh/latência, **medida em jogo** em 03/11/2026) fechados; **P3** (UI do
grupo), **P4** (nomes múltiplos) e **P5** (persistência/limpeza paralela) **não**.

Números deste ciclo (03/11/2026, com o P1 dentro): build **0/0**, harness `tools/ai-intents-check` **471 asserções**
(nada mudou nele — o P1 é jogo, não rede por palavra), teste do MCP **101** (as **12** asserções novas cobrem o
contrato do `companions.build` do grupo: `workers`, `refused` e `name` vazio = todos), config **358 opções / 25 seções**,
ES/FR/IT **0 lacunas** e **nenhuma chave de tradução nova** (as recusas reusam `hc_ai_no_companion` e
`hc_ai_build_failed`). **O teste em jogo está pendente** — receita no §7.

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

**Chave do canteiro** (`MHR_BuildGroup.KeyOf`, `Jobs/MHR_BuildGroup.cs:93`): `label|anchor(x,y,z em 1 m)|yaw(5°)|owner`.
Dois companheiros que miram o mesmo ponto caem na mesma chave (o arredondamento absorve a mão do jogador) e o
canteiro de outro jogador **nunca** é juntado (`owner` = `MHR_Owner.CreatorId` do líder).

**Uma obra por jogador** (`SiteBusy`, `:111`): a segunda ordem é **recusada** ("já existe um grupo de obra em
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

**Tempos (`MHR_BuildGroup.cs:36-40`, `MHR_Build.cs:71`)**
- Claim: **20 s** (`ClaimSeconds`) — renovado a **cada frame dirigido** (`Refresh`), então o prazo é para o worker
  que **sumiu**, não para uma caminhada longa.
- Retry: **2 s** de espera antes de a peça poder ser puxada de novo (`RetryDelay`), teto de **3** tentativas por
  índice (`MHR_Build.MaxPasses`, **compartilhado pelo canteiro**).
- Espera na fila: `WaitRetry` sai depois de **60 s** sem a fila andar (`RetryWaitMax`) — um worker cujo jogo caiu não
  segura o companheiro para sempre.

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
  (`PlanLocked`, `MHR_Build.cs:689`) ficam desligados — reordenar o plano de um deixaria os índices dos outros
  apontando para outras peças (e os claims com eles). O retry do canteiro é o que cobre o que o reorder resolvia.

**Estação (bancada)**
- `PlanStations` roda **uma vez**, no plano do líder, antes da 1ª peça (a regra do jogo é por peça, `HaveBuildStationInRange`
  a 20 m com o Y achatado: uma bancada serve para todos).
- `InsertBenchStep` (auto-cura do `needs_station`) com **≥2** workers: só o **líder** tenta, e a bancada vai para o
  **fim** do plano (não insere no meio — o índice de ninguém anda); a peça recusada vai para o retry, que roda depois
  da bancada. Com 1 worker continua inserindo **na frente** da peça, como antes.

**Materiais (o que ainda não existe)**
- **Sem reserva por peça:** cada worker conta mochila + baús do canteiro para a **peça dele**. Duas pilhas podem ser
  contadas duas vezes e o segundo a chegar entrega a ordem à coleta — o pior caso é **N× o resto do plano**, com até
  **3 coletando ao mesmo tempo** (a coleta é um job por companheiro). O `Stock` do desenho é a metade aberta do P1.
- Quem vai coletar **sai** do grupo: o `HandOver` entrega o movimento à coleta, grava o `Pending` **com a chave do
  grupo** e a ordem de obra **termina** (`Finish` → `Leave`, `MHR_Build.cs:3446`). Quando a obra retoma, o
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
- Claim **revalidado antes de pagar** (`Place`, `MHR_Build.cs:3314`): se o claim expirou enquanto ele lutava/carregava
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
2. **P1 — MVP FEITO, TESTE EM JOGO PENDENTE (03/11/2026):** `MHR_BuildGroup` (plano/claims/retry), `WaitGroup`/
   `WaitRetry`, líder que nivela/limpa e promove, `PlanLocked`, bancada anexada ao fim, uma obra por jogador,
   anti-duplicata, `BlueprintWorkers` (sem nome = todos), `SetThenWorkers`/`NoteLeveled` (o **nivelamento devolve o
   grupo inteiro**) e o `companions.build` do bridge. **Aberto:** a **reserva de material** (`Stock`) e o número
   honesto da coleta com N workers (hoje cada um conta o canteiro por conta própria).
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

**Coberto pelo código (revisão A1–A12, 03/11/2026)** — falta o jogo confirmar:
- uma peça nunca é paga/colocada duas vezes (claim + `VerifyAt` + `MarkDone` por índice);
- peça de outro worker é pulada **sem** contar como feita;
- claim expira em 20 s, renovado por frame; `WaitRetry` teto de 60 s; retry com teto de 3 por índice, 2 s de espera;
- líder faz level/clear; seguidores em `WaitGroup` não andam, não têm relógio e não são pegos pelo ócio;
- líder que sai é substituído; se saiu antes do `BuildPhase`, a fase abre;
- plano **não** reordenado com ≥2; bancada anexada ao fim;
- segunda ordem do jogador é recusada (`SiteBusy`);
- nivelamento do blueprint devolve **os N** (`SetThenWorkers`/`GroupWorkers`) e `NoteLeveled` impede nivelar de novo.

**Teste em jogo (pendente) — 2 companheiros num 6x6 (46 peças):**
1. "construa a cabana aqui" **sem nome** → as duas entram (`[Build] grupo: 2 companheiro(s)...`), uma nivela/limpa e
   a outra espera; depois as duas constroem.
2. **46 peças colocadas uma vez**, nenhuma duplicada (`build.pieces` não pode ter duplicata no mesmo ponto), e as
   duas terminam (ou uma em silêncio depois da outra).
3. "para" → as duas param; matar uma → a peça dela é reivindicada pela outra; ela volta e pega a próxima
   (ou conta a peça já feita).
4. "construa a cabana aqui" com **nome** → só aquela começa (caminho antigo).
5. Segunda ordem de blueprint enquanto a primeira roda → recusada.
6. Terreno irregular: o nivelamento devolve **as duas** ao canteiro
   (`the ground is ready - back to the build (2 companheiro(s))`).

**P1 (985 peças, depois do 6x6):** o `next` é o **do canteiro** (não 2×), dois workers com peças distintas, o tempo
até 720 cai e não há loop de `no progress` (com P2).

**P4 (quando existir):** "Maria e João, construam a cabana aqui" → as duas; "Zé, colete 10 madeiras" → só o Zé;
"para, Maria" → só a Maria; "para" → todos; "Maria e João, ataquem o greydwarf" → as duas; "Maria, equipa a
picareta" → só a Maria; "Maria e o cachorro, ..." → Maria + aviso do desconhecido, sem inventar.

**Gate:** harness **471** (inalterado no P1), MCP **101** e build **0/0**.

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
- `Jobs/MHR_Build.cs` — `StartBlueprintGroup` (`:1017`), `WaitGroup` (`:1636`), `WaitRetry` (`:1683`), `Advance`
  (`:565`), `QueueRetry` (`:424`), `CountPlaced` (`:716`), `PlanLocked` (`:689`), `IsLeader` (`:699`),
  `InsertBenchStep` (`:1950`), `RejoinGroup` (`:1433`), `NamesOf`/`GroupWorkers` (`:1145`/`:1166`), `Pending.Group`.
- `AI/CompanionOrders.cs` — `Run` (modos), `Build` (`:1161`, o blueprint em `:1293`), `BlueprintWorkers` (`:1381`),
  `LevelOrder`, `IsNotAddressee`; o cancelamento de build de qualquer outra ordem (`:229-240`).
- `Jobs/MHR_Level.cs` — `SetThenWorkers` (`:138`) e a retomada com `GroupWorkers`/`NoteLeveled` (`:467-497`).
- `AI/BuildExecutor.cs` — `PlacedRadius`/`PlacedYaw` e `VerifyAt` (`:65-68`, `:278`).
- `DevBridge/Commands.cs` — `CompanionsBuild` (o ramo do blueprint, `:1107-1193`).
- `AI/CompanionNames.cs` — `FromMessage` (um nome), `Find`, `CompanionNameMatch` (P4).
- `Patches/MHR_CompanionNavMesh.cs` — agente 40, `BuildTile` sem portas; o `OnPlaced`/`Destroy` chegou na P2.
- `docs/re/navegacao-companheiro.md` §6 — o que o Vikings for Hire faz (invalidação/links).
- `docs/plan-construcao.md` — a trilha E; este plano é a extensão multi-trabalhador.
