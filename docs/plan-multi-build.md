# Plano — vários companheiros na mesma obra (multi-build)

**Status:** desenho, 03/11/2026. **P2 (navmesh/latência) implementado e medido em jogo em 03/11/2026**; P0/P1/P3–P5 não
implementados. O teste atual (1 companheiro, `CommHomeBasicBP`) é a base; a obra de 985 peças mostrou que o gargalo
é a **ordem/navegação**, não o número de trabalhadores.

## 1. O que existe hoje (medido no código)

- **Modos de trabalho** (`wood`, `stone`, `ore`, `forage`, `smelt`, `hunt`, `farm`, `fish`, `repair`, `restock`,
  `cook`, `stay`): sem nome, `CompanionOrders.Run` percorre **todos** os companheiros do jogador; com um nome,
  `IsNotAddressee` deixa só o nomeado. É o "todos obedecem" que fica.
- **Ordens de um tiro** (`build`, `level`, `attack`, `equip`, `drop`, `inventory`, `come`): usam **um** alvo —
  `Build`/`LevelOrder` fazem `target = addressee ?? speaker`, e o `speaker` é o companheiro mais próximo que pode
  receber ordem (`CompanionChatAI.PickSpeaker`). **Hoje `build` sem nome já vai para um só**, não para todos.
- **`MHR_Build` é por companheiro** (`s_state` por `GetInstanceID`), com plano, `Pending` (coleta/morte),
  `Reattach` e estações próprios. Dois `StartBlueprint` no mesmo anchor = **dois planos** sobre as mesmas peças.
- **Nome:** `CompanionNames.FromMessage` devolve **um** nome (o primeiro antes da vírgula/segmento); o campo
  `companion` do modelo é um `Find` separado. Não existe "Maria e João".
- **Navmesh:** `MHR_CompanionNavMesh` (agente 40, climb 0.6, `Pathfinding.BuildTile` sem portas) não invalida
  **explicitamente**; o jogo re-pokeia no `GetPath` e reconstrói o tile em até **5 s** (`m_updateInterval`). O
  `no progress` de 12–17 m é o `SnapToNavMesh` (1,5/3/6/12 m), não o tile invisível. A P2 reduz a latência
  (`m_pokeTime` + `m_buildTime = now - 5,1`, coalescido) e cobre a remoção (`ClearScaffolds`/`build.delete` via
  `ZNetScene.Destroy`). Detalhe: `docs/re/navmesh-tiles.md` e `docs/re/navegacao-companheiro.md` §6.

## 2. Objetivo e não-objetivo

**Objetivo**
1. N companheiros na **mesma** obra: um plano, peças distintas, materiais pagos uma vez.
2. "Maria e João, construam X" → as duas entram no grupo; "Zé, colete" → só o Zé; "para" → todos; "para, Maria" →
   só a Maria.
3. Manter o "todos obedecem" dos modos sem nome e o `build` de um só até o grupo existir.

**Não-objetivo (agora)**
- Persistir o grupo no save/mundo (o build atual também não persiste; o `Pending` é em memória).
- Multiplayer dedicado, offscreen workers, UI completa do grupo.
- Contratar/gerenciar o grupo pelo painel.

## 3. Modelo: `MHR_BuildGroup`

**Chave do grupo:** `blueprint` (arquivo/label) + `anchor` arredondado (1 m) + `rot` arredondada (5°) + owner.
Um grupo por canteiro; se o jogador mandar construir **outra** coisa em outro anchor com um grupo ativo, a ordem
recusa e pede "para" (evita duas casas ao mesmo tempo).

**Grupo (dono do plano)**
- `Plan`: `List<Step>` com `Prefab`, `Piece`, `Pos`, `Rot`, `State` (`Pending`, `Claimed`, `Placed`, `Skipped`,
  `Retry`), `Worker`, `ClaimAt`, `Attempts`, `Why`.
- `Phase`: `Level` → `Clear` → `Build` → `Retry`, uma vez para o grupo.
- `Placed`, `Skipped`, `PlannedStations`, `Anchor`, `Rot`, `CreatedAt`, `LastProgressAt`.
- `Stock`: reservas por item/baú (quem reservou o quê) para não contar a mesma pilha duas vezes.
- **Só a main thread** toca no grupo; claims e conclusões são atômicos por frame.

**Trabalhador (`MHR_Build` por companheiro)**
- `Worker`: `CompanionSetup`, `Step` atual, `ClaimAt`, `LastProgress`, `Handover` (coleta/nivelamento).
- `Drive`: pede `TryClaim(worker)` → anda → paga → coloca → verifica → `Complete(step)`/`Retry(step)`.
- `Release` ao parar/morrer/perder posse; `Expire` do claim (20 s, ou os 12 s de "no progress") devolve a peça.

**Atribuição**
- `TryClaim` escolhe a peça **não reivindicada** mais perto do trabalhador (mesmo andar, `PreferNearFloor`), com
  penalidade para peças perto de outro trabalhador (espalhar) e para peças já em retry.
- `PullSupports`/`PullFloorsAbove` marcam as peças puxadas como reservadas à peça recusada; o mesmo trabalhador
  (ou qualquer um livre) as constrói antes; o `PullUntil` continua valendo.
- Uma peça `Claimed` por outro trabalhador **não** entra no `TryClaim`; se o claim expirar, volta a `Pending`.

**Nivelar/limpar**
- P1: **um** trabalhador faz `Level`/`Clear` (o `MHR_Level`/harvest já são de um); os outros esperam o grupo
  entrar em `Build` (ou seguem/aguardam o jogador). Paralelizar a limpeza fica para P5.

**Materiais**
- `PlanMissing` do grupo conta o **restante não colocado** (não o total), e a mochila/baús **somados** dos
  trabalhadores; o `Stock` reserva o que cada um vai gastar. Quem coleta é **um** trabalhador (o sem claim ou o de
  menor progresso), pedindo o que falta para o **grupo**; os outros continuam nas peças que ainda podem pagar.
  Ao voltar, ele reivindica de novo.
- Estação: `PlanStations`/`InsertBenchStep` rodam **uma vez** no plano do grupo; uma bancada colocada por qualquer
  trabalhador serve para todos (a regra do jogo é por peça/raio).

**Morte e retomada**
- O `Pending` do trabalhador guarda a **chave do grupo** + índice/peça; o `Reattach` acha o grupo e reivindica de
  novo (a peça dele expirou e pode ter sido feita por outro — nesse caso ele pega a próxima).
- O grupo **não** morre com o trabalhador; se todos morrerem, ele fica em `Build` com `LastProgressAt` e o
  `CheckPending` de cada um retoma.
- `OnSceneLoaded`/`Shutdown` limpam o grupo (como o `s_state` hoje).

**Parar**
- "para" sem nome: `CancelAll` do grupo (todos os workers saem; o grupo é descartado).
- "para, Maria": remove só a Maria; o grupo continua com os outros; se ficar vazio, descarta.
- Uma ordem de **modo** em um companheiro (ex.: "Zé, colete") tira **aquele** trabalhador do grupo (ele vai fazer
  o modo) e os outros continuam; o "para" explícito é o que derruba todos. Decidir com o jogador (pergunta 4).

## 4. Nomes múltiplos (P4 — por último)

- `CompanionNameMatch.FromMessageList(message, names)` (puro, testável): lê o **prefixo** antes do primeiro verbo
  de ordem/segmento; separa por `,`, `;`, `e`, `&`, `+`, `e o/a`; casa nomes **existentes** (sem caixa/acento,
  artigo opcional); conectores não são nomes. Conservador: um token que não é nome conhecido nem conector
  **para** a lista no último nome válido (nunca inventa); um primeiro token desconhecido com cara de nome continua
  usando o `AddressCandidate`/aviso atual.
- `CompanionNames.FromMessage` devolve `List<CompanionSetup>`; `Find` (campo do modelo) continua um só. Prioridade:
  **as palavras do jogador** (lista) vencem o campo do modelo; se a lista estiver vazia, o campo do modelo; se
  ambos vazios, o comportamento antigo (modos: todos; build: speaker).
- `CompanionOrders`: `addressee` vira uma lista pequena (`Addressees`); `IsNotAddressee` consulta a lista. Ações
  inerentemente de um alvo (`equip`, `drop`, `inventory`, `come`) usam o **primeiro que pode** (com log); modos,
  `attack` e `build` usam **todos** da lista.
- Prompt: o `PromptLine` já lista os nomes; uma linha curta "o jogador pode chamar um ou mais" (sem exemplo novo
  no contrato do 3B; a lista é lida localmente). `ValidActions` **não** muda.
- Harness: casos puros de `FromMessageList` ("Maria e João", "maria, joao", "Maria e o Zé", "Maria e o cachorro",
  "colete 10 pedras", "Ei, Maria e João", nomes repetidos, nome desconhecido) + `IsNotAddressee`.

## 5. Fases (ordem de implementação)

1. **P0 (agora):** manter o comportamento; **não** dar `build` sem nome esperando vários (hoje vai para o speaker).
2. **P1 — grupo:** `MHR_BuildGroup` + workers/claims + stop/um + morte/retomada + materiais/estação + `Build`
   aceitando a lista de alvos (sem nome = todos os que podem receber ordem; com nome = os nomeados). Teste com
   **2 companheiros** num **6x6** (46 peças) antes do 985.
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
4. **P3 — UI/feedback:** progresso do grupo (`N/M`, workers), quem está com qual peça; o painel por companheiro
   mostra "obra: X".
5. **P4 — nomes múltiplos:** `FromMessageList` + `Addressees` + os filtros + harness. **Por último** entre as
   frentes principais (pedido do jogador, 03/11/2026): o grupo e a navmesh primeiro, porque são o que faz a obra
   andar; a lista de nomes é conveniência de comando.
6. **P5 — persistência/multiplayer/limpeza paralela** (depois).

## 6. Invariantes e riscos

- **Main thread only**; claim/completo atômicos; nenhuma peça com dois donos; nenhum material pago duas vezes.
- **O "todos obedecem" dos modos sem nome não muda.** O `build` de um só (speaker) só muda em P1, e o teste
  atual (1 companheiro) tem que continuar idêntico.
- `Pending`/`Reattach`/`HC_CompanionId`/`MHR_Owner` continuam sendo a base; o grupo é **em memória** no cliente
  do dono; outro jogador não entra.
- O `PreferNearFloor`/`PullSupports`/`PullFloorsAbove` passam a operar no plano do grupo, não no plano do worker.
- **Não** duplicar peça/estação/coleta; `PlanStations`/`InsertBenchStep` uma vez.
- O `MHR_Build.Active`/`OtherOrder`/harvest da limpeza precisam responder pelo **grupo** (a limpeza é fase do
  grupo; o harvest do `Clear` continua sendo a ferramenta).
- A invalidação da navmesh (P2) reduz a latência do piso novo (~5 s → ~0,3–1,3 s) e cobre a remoção; a
  **carência do mover** ataca os `no progress` medidos (36). Sem as duas, o grupo pode ter o mesmo `no progress`
  de hoje, só que com mais trabalhadores.

## 7. Critério de aceite

**P1 (2 companheiros, 6x6):** um grupo, **46 peças colocadas uma vez**, nenhuma duplicada; os dois param com
"para"; matar um → a peça dele é reivindicada pelo outro; ele volta e pega a próxima; a estação é planejada uma vez.
**P1 (985):** o `next` é o **do grupo** (não 2×), dois workers com peças distintas, materiais pagos uma vez por
peça; o tempo até 720 cai; sem loop de `no progress` (com P2).
**P4:** "Maria e João, construam a cabana aqui" → as duas; "Zé, colete 10 madeiras" → só o Zé; "para, Maria" → só
a Maria; "para" → todos; "Maria e João, ataquem o greydwarf" → as duas; "Maria, equipa a picareta" → só a Maria;
"Maria e o cachorro, ..." → Maria + aviso do desconhecido, sem inventar.
**Harness:** 461 + os casos novos da lista de nomes; build 0/0.

## 8. Perguntas para o jogador (decidir antes do P1)

1. `build` **sem nome** com 3 companheiros: todos entram no grupo, ou só o speaker até você nomear?
2. Um nome **desconhecido** na lista ("Maria e o cachorro"): faz os conhecidos e avisa, ou recusa a ordem toda?
3. "para, Maria" tira só a Maria e o grupo continua? "para" derruba todos?
4. Um modo (ex.: "Zé, colete") tira o Zé do grupo e os outros continuam, ou derruba o grupo todo?
5. Persistir o grupo no save (retomar depois de reiniciar) fica para depois?
6. A limpeza/nivelamento em paralelo (vários) fica para depois, com um só no P1?

## 9. Referências

- `AI/CompanionOrders.cs` — `Run` (modos), `Build` (`target = addressee ?? speaker`), `LevelOrder`,
  `IsNotAddressee`.
- `AI/CompanionNames.cs` — `FromMessage` (um nome), `Find`, `CompanionNameMatch`.
- `Jobs/MHR_Build.cs` — `State` por `CompanionAI`, `StartBlueprint`/`StartPlan`, `Advance`, `PreferNearFloor`,
  `Pending`/`Reattach`, `Finish`/`Cancel`.
- `Patches/MHR_CompanionNavMesh.cs` — agente 40, `BuildTile` sem portas; falta o `OnPlaced`/`Destroy`.
- `docs/re/navegacao-companheiro.md` §6 — o que o Vikings for Hire faz (invalidação/links).
- `docs/plan-construcao.md` — a trilha E; este plano é a extensão multi-trabalhador.
