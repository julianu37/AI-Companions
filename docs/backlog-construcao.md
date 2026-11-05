# Backlog da construção (ordem de execução)

> Síntese das **cinco revisões de 04/11/2026** (lógica/invariantes, performance, rede/ZDO, domínio do jogo,
> idiomas/config/docs), das medições dos Runs 18–22 e do **redesenho da execução** (§17.26: runs de 20:55, 21:33,
> 21:49 e 22:11). **Ordem = ordem de ataque**, não ordem de importância.
> Escopo: **single-player**; multiplayer é o **último item** e só se o jogador decidir investir (ver `AGENTS.md`,
> "ESCOPO DO PROJETO").

## O que já está medido (ponto de partida)

| indicador | valor | onde |
|---|---|---|
| melhor run (985 peças, 5 companheiros, receita de **teste**) | **324 s (5 min 24 s)**, 985/985, 0 puladas, 0 mortes, 0 `UNSUPPORTED`, watchdog 0, `FALTANDO` vazio | **§17.26** |
| antes do redesenho (a mesma casa) | **727 s** (1 Hz) e **844 s** (baseline) | §17.24 / §17.22 |
| `caminhada` | **2,5 m/peça** (era **8,4** no run de 20:55) | §17.26 |
| peças colocadas **sem andar** (`no alcance`) | **728/985 = 74 %** (e **91 %** das linhas `next:`, outro denominador) | §17.26(B) |
| espera (soma dos 5) / `fila` | **598,0 s** ≈ **119,6 s por worker** / não transcrita no run de 22:11 | §17.26(A) |
| barreira de andar | **539,2 s em 106 retenções** (era 1322,0/165 sem a partição) | §17.26 |
| desbalanceamento por worker | **C6 259 × C5 126** (**2,05×**) — a partição é por **contagem**, não por custo | §17.26(F) |
| `disputa` / `sem lugar livre` / `travada não voltou` | **150** / **149** / **31** (era **109**) | §17.26(E) |
| cpu do `Drive` | **97–226 µs/frame** por worker — o instrumento **existe** agora | §17.26(E) |
| harness / config / MCP / gate | **775** / **368/25** / 122 / **9/9** | — |

**A ordem de ataque dos itens novos** (§17.26, medidos): **1 → 11 → 12 → 13 → 14 → 15 → 2 → 3 …**. Os itens 11–14 são
os próximos passos que o redesenho **deixou medidos**; o 15 é dívida de contagem do harness (barata, dono do
`tools/`); os itens 2–10 continuam válidos e **não** foram reordenados (a numeração é estável de propósito, porque
`AGENTS.md` e as notas citam os números).

## Fila de trabalho

### 1. O run da **receita de produto** (o que decide a frase "está pronta")
`BlueprintFree=false`, `PlaceReach=10`, `LevelGround=true`, `DeckLift=1`, material **pago** de mochila + baús,
inventário real (peso), jogador andando. É o único cenário que ainda **não** foi jogado e é onde vivem as pendências
(7), (11) e (12). **É o item que falta para a meta do jogador** (*"a `CommHomeBasicBP` em menos de 5 minutos"*): a
**receita de teste** já faz **324 s** (§17.26) e faltam **24 s** — e na receita **de produto**, que é mais caro
(material pago, `PlaceReach` 10, nivelamento ligado), a distância é maior.
**Aceite:** 985/985 (ou falta **nomeada**), **0 puladas**, 0 mortes, 0 `UNSUPPORTED`, watchdog 0, `FALTANDO` vazio,
**sem** `missing material` em loop, **sem** `too_heavy` encerrando a ordem, e a **duração medida e comparada com os
324 s da receita de teste**. A meta do jogador é **≤ 300 s**; se a receita paga não fechar em 5 min na primeira
tentativa, o número que sair é o marco (não se reescreve a meta).
**Risco previsto:** com 25–32 stacks por companheiro o `MHR_Build.cs:3564-3567` encerra a ordem daquele worker; a
reserva de material é a metade aberta do P1 (`MHR_BuildGroup.cs:27-30`).

### 2. `too_steep` no nivelamento **mata a obra** (pendência 12)
`hc_ai_build_too_steep` (`Jobs/MHR_Level.cs:282`) não está na lista de retomada (`:455`, só `done`/`already`/
`failed`), então `LevelGround` (padrão **true**) encerra a ordem em terreno íngreme em vez de devolver ao build.
**Menor mudança:** incluir a recusa do nivelamento no `resumeBuild` (a obra volta e constrói o que dá).
**Aceite:** num terreno com variação > `LevelTolerance`, a obra **não** encerra — ela constrói ou diz o que falta.

### 3. `step-aside` no andar sendo telado (a dívida do `Clearance`)
`BuildAsideSpot.Clearance = 1,5 m` (`AI/BuildAsideSpot.cs:39`) é um **círculo em XZ** contra peças pendentes: num
andar em obra **todo** ponto a 2,2 m está a <1,5 m de uma peça pendente → o anel cai → `sem lugar livre` → peça na
fila + barreira de 10 s. Medido: **42 de 52** casos de corpo não moviam ninguém; o andar de cima foi o gargalo de
um run inteiro (17 min 40 s).
**Tentativa revertida (com o número):** o teste exato cápsula × caixa girada (`CrossesPending`) recusou **67 % do
plano** (424 `sem lugar` em 638 peças) porque `BuildBody.Blocks` não tem tolerância e o span vertical pega piso
**acima** do candidato — revertido em `e594f7e`.
**Antes de codar:** a triagem que falta (item 4). Ela diz se é `Blocked` (regra), `StepTo` (não tem chão no nível) ou
`SafeStep` (não tem rota). **Se for `StepTo`, não existe ajuste de anel** e a resposta é aceitar a fila.

### 4. Instrumento: a triagem do `sem lugar livre` e do `no progress` (log, sem run)
- `AskStepAside` registra **por que** falhou: `Blocked` (1,5 m de pendente) × `StepTo` (chão/level) × `SafeStep`.
- `SkipPiece` com `no_support` registra o `DirectSupportIndex` e o estado dele (`done` / na fila com `At` / claimado /
  nada no raio) — separa "efeito de ordem" de "buraco real" (poste).
- `no progress` agrupado por **pose** (`prefab@x,y,z`) + ΔY do walk spot + rota `partial|full` — hoje são 10–20
  linhas que não se sabe se são a cumeeira (pendência 10) ou lugar novo.
- Poste: `apoio do plano: nenhum|reivindicado|deferido N|desistido` na linha do `apoio de emergência` — 26 postes sem
  proveniência não distinguem apoio legítimo de andaime.

### 5. Custo por frame **medido** (pendência 15) — **fechado no `µs/frame`**
O cronômetro **existe**: `Stopwatch` no `MHR_Build.Drive` (`s_cpuClock`, `MHR_Build.cs:4525`, acumulado em
`:4546-4547`) e a linha do `Finish` traz `cpu: Z µs/frame em FR frame(s), goto …, place …` (`:8983-8992`). Medido no
run de 22:11: **97–226 µs/frame por worker** — **bem abaixo** dos **~4,2 ms/frame** (5 workers) estimados pela revisão,
e o `ClassOf` memoizado já tirou a alocação (§17.25).
**O que continua aberto:** o `BodyAt` no **pico**, o frame time com a obra **travada** e um contador de avaliações de
`Ready` + `GC.GetTotalMemory` (a proposta original, não implementada).
**Aceite do resto:** `BodyAt` de pico registrado num run com a obra travada.

### 6. A cumeeira (pendência 10) — navegação
`no progress` recorrente em `wood_roof_45` a **48,8 m** (Runs 13/15/20). O walk spot já foi corrigido (anéis 1,5/3 m,
`WalkHits` 3) e ainda falha. `TempScaffold` está **vetada** pelo jogador — a resposta é rota, não andaime.
**Antes:** a triagem do item 4 (pose + walk spot + rota) para não repetir o Run 14 (walk spot 7 m abaixo).

### 7. Vão do deck — **decisão do jogador**
`DeckLift=1` deixa ~1 m entre o piso e o chão e prende corpos (41 peças de piso foram apagadas à mão uma vez).
Opções: **A** `DeckLift=0`; **B** saia em volta fechando o perímetro; **C** regra de rota para o mover não
entrar/parar sob o deck. Nada disso se decide sem ele.

### 8. As duas frases erradas do `CHANGELOG` público (revisão de lógica, A8)
`package/CHANGELOG.md:16` diz que os blueprints ficam em `config/…/blueprints` (o código usa `<plugin>/blueprints`,
`AI/BuildBlueprints.cs:54/70-74`) e `:35` diz que a ordem é "por classe de peça … em vez de por altura" (o parser
ordena **por altura primeiro**, `AI/BuildBlueprintParser.cs:146`). Correção de uma linha cada, com o mesmo ciclo.

### 9. A data dos documentos
`AGENTS.md` e `docs/re/building-notes.md` datam os runs de hoje como **11/11/2026**; o `git log` desta máquina diz
**04/11/2026**. Uma das duas está errada em ~200 ocorrências — passe dedicado, não no meio de um ciclo de código.
**O §17.26 foi escrito com a data do ciclo (`04/11/2026`)** e cita o **baseline** (§17.22/§17.24) sem repetir data:
a divergência **continua** e agora aparece **dentro** da mesma nota (a linha do §17.26 não é evidência de qual das duas
está certa).

### 10. Reserva de material por peça (limitação declarada do P1)
Cada worker conta a mesma pilha por conta própria (pior caso: N× o resto do plano, com até 3 coletando juntos). Só
aparece no run **pago** (item 1). O desenho é o `Stock` da metade aberta do P1 em `docs/plan-multi-build.md`.

### 11. Balancear o **chunk por custo**, não por contagem (§17.26(F))
`BuildSpread.Chunk` (`AI/BuildSpread.cs:473`) divide a fase em trechos de tamanho **quase igual** — e as peças **não
custam o mesmo**: um `wood_roof_45` no alto custa **caminhada** (e às vezes uma barreira); um `wood_floor` no chão
custa um passo. **Medido:** **C6 259 × C5 126** peças (**2,05×**) na mesma obra, e o `StealTail`
(`MHR_Build.cs:1546`) **não** reequilibra — ele só age quando o trecho **acabou**.
**Menor mudança:** um peso por peça (classe + altura + ΔY do walk spot, que o `BuildWalkSpot` já calcula) e o corte por
**soma de peso** acumulada, mantendo as duas propriedades do harness: cobertura exata da fase e desvio máximo de **1
peça** (que viraria desvio máximo de **uma fatia** de peso).
**Aceite:** a razão entre o mais e o menos produtivo cai de **2,05×** para **≤ 1,3×**, com a duração **igual ou menor**
(324 s) e **0 puladas** — senão reverte e fica o número.

### 12. Partição por **coluna XZ** (trecho espacial, não faixa de índice) (§17.26(D))
O corte por **índice** é um trecho **contíguo no espaço** porque o parser faz um tour de vizinho mais próximo dentro
da run (`BuildBlueprintParser.Ordered`/`NearestWithin`). Isso **deixa de valer** quando a peça volta pela **fila**
(`NextRetry` → `EnterStep`, `MHR_Build.cs:2202-2213`) ou pelo `NoteStay`: ela entra **sem** o `ChunkBounds` e pode cair
no canto de outro — é uma das leituras para a `disputa` **150**.
**Desenho:** a partição guarda a **faixa XZ** (ou a coluna do plano) além do par de índices, e a fila passa a respeitar
a faixa do **dono** daquela coluna. Ganho esperado: `disputa` **~0** e estabilidade quando a fila devolve peças fora de
ordem.
**Antes:** a triagem do item 4 (dizer se os 150 são **fila** ou **pegada**).

### 13. Varredura do alcance — *"nunca passar do lado de uma peça colocável sem colocá-la"* (§17.26(G)5)
Hoje o `Advance` tenta **uma** peça ao alcance (`StayPick` → `BuildSpread.Nearest`, `MHR_Build.cs:1768`) e, se ela
falhar, **anda**: a peça seguinte ao alcance **não** é tentada — a regra do jogador **não** está implementada.
**Desenho:** varrer as **N** peças ao alcance antes de andar + **memória de "já tentei daqui"** (pose + índices já
recusados naquela posição, para não repetir a falha a cada frame). `StayTries` = **6** (`:192`) é o orçamento de
recusas **por peça**; a varredura precisa do seu próprio teto.
**Aceite:** `no alcance / peças` acima dos **74 %** atuais com a **duração igual ou menor**, e **0 puladas** (o guarda
de sempre: peça recusada **não** pode virar peça perdida).

### 14. `dependência` partida em **colega vivo × órfã** (§17.26(G)2)
A `dependência` é o maior balde da barreira (**539,2 s em 106 retenções**, ~**90 s por worker**) e o `PendingBelow`
(`MHR_BuildGroup.cs:1256`) **não distingue** "o colega está construindo o apoio" (espera **legítima**, é o desenho) de
"o apoio é de outra fase e **ninguém** o vai construir" (**órfã** → a peça devia ir para o retry ou ganhar poste).
**Menor mudança:** carimbar no `PendingBelow` o estado do apoio — **claimado por um worker vivo**, `done`, na fila com
`At`, ou **nada no raio** (o mesmo vocabulário que o item 4 pede para o log) — e somar em dois baldes na linha do
`Finish`.
**Aceite:** dois números na linha; o run **não** muda (é instrumento, como o item 4).

### 15. Fechar as **contas do harness** (dono: `tools/`) — 3 dívidas medidas no §17.26
1. **A repulsão não tem asserção.** `BuildSpread.NearestApart`/`PickNearApart`/`Apart`
   (`AI/BuildSpread.cs:333`/`:183`/`:390`) **não são chamadas** pelo `Program.cs` — a única ocorrência de `Apart` lá é
   um **comentário** (`:1928`), enquanto o docstring do código promete *"the harness pins the table next to the one of
   `Nearest`"* (`:330-331`). É a única peça do redesenho **sem** tabela (o P0 tem a (aa), a partição tem a (ab)).
2. **O `tools/ai-intents-check/README.md` está atrás:** a tabela de seções **para na (w)** e o total impresso é
   **656**, atrás de (x)/(y)/(z)/(aa)/(ab).
3. **7 asserções não estão itemizadas:** a soma por seção dá **768** contra as **775** medidas (`718 + 15 + 22 + 20`; o
   `711` que estava no `AGENTS.md` era stale e diferia em 7 do `718` do ciclo anterior).
**Aceite:** as três fechadas no mesmo ciclo — tabela da repulsão com casos de borda (exatamente 3 m = livre, corpo
`NaN` ignorado, sem corpos = `Nearest`, fallback que nunca passa fome), README com as seções (x)–(ab) e o total
itemizado batendo com o `dotnet run`.

## Declarado e **fora** do caminho crítico

- **Multiplayer** (pendências 5, 16, 17): troca de posse do corpo a cada 2 s para a obra em silêncio e trava o
  `SiteBusy`; pagamento por baú sem `SetInUse`. **Último item de todos**, só se o jogador investir.
- **Corpus baixado** (pendência 14): 279 de 354 blueprints perdem linha por `recipe not unlocked` e o mod **pula em
  silêncio** — falta a **tela e a fala** dizendo qual receita falta. É decisão de produto, o dado já existe
  (`BuildBlueprintReport.SkippedReasons`).
- **Criatura como bloqueador**: a sonda de corpo não testa criaturas (um javali em cima da peça passa).

## Regras que valem para qualquer item desta fila

1. **Uma mudança por build**, com o corte de aceite escrito **antes** do run.
2. **Reverter na hora** se `puladas > 0`, `desistindo > 0`, morte, `UNSUPPORTED` ou watchdog — o baseline fecha
   985/985 com 0, então qualquer um deles é regressão.
3. Regra pura vai para o harness **no mesmo ciclo** (hoje **775** asserções); quem implementa não afrouxa a asserção.
4. O que foi revertido fica **documentado com o número que o matou** (escopo XZ: ~33 min; `ahead` por frame: 38 vs 78
   peças/min; `CrossesPending`: 67 % do plano).
