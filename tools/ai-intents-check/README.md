# ai-intents-check — as redes por palavras da camada de IA, fora do jogo

Harness **versionado** (não vive em `%TEMP%`) que compila os **fontes reais** das redes por palavras e roda as
asserções fora do jogo. Substitui os harnesses de reflexão avulsos usados nas auditorias: o mesmo teste, sempre, com
o mesmo resultado.

```powershell
# 1. o build do mod (é dele que sai a DLL que o cross-check lê)
dotnet build Companions.sln -c Release

# 2. o harness
dotnet run --project tools/ai-intents-check
```

Sai `=== N PASS / M FAIL ===` + `TODOS OS TESTES PASSARAM` e **exit 0** quando não há FAIL nem SKIP; `1` quando há
falha (ou algo foi pulado) e `2` quando o harness não conseguiu subir (assemblies do jogo ausentes).

## O que ele compila (nada é copiado nem reimplementado)

`ai-intents-check.csproj` linka, direto de `src/`:

| Arquivo | O que é testado |
|---|---|
| `AI/RadialIntent.cs` | postura, vagar, comando, `smelt`/`cook`/`hunt` + as guardas |
| `AI/PickupIntent.cs` | `nopickup` → `drop`/`pickup` pelos verbos do jogador |
| `AI/GatherIntent.cs` | coleta com número (5 idiomas) e o `pickup` legítimo |
| `AI/InteractionIntent.cs` | `hive`/`door`/`board`/`sit`/`deposit` + as recusas |
| `AI/LlmIntent.cs` | `Parse`, `ExtractContent`, `Normalize`, `IsIntentWord` |
| `AI/OrderItems.cs` | `Resolve`/`GatherAction` — o item da ordem nos 5 idiomas |
| `AI/CompanionNames.cs` | `CompanionNameMatch` (as regras de "quem foi chamado") |
| `AI/BuildCatalog.cs` | `Resolve`/`HasWord`/`HasAnyWord` — o nome da peça → prefab (E5) |
| `AI/BuildTemplates.cs` | os templates da E6/E6d (aliases nos 5 idiomas, peças, ordem) |
| `AI/BuildBlueprintParser.cs` | `Parse` (V1/V2), `Ordered` (classe/andar/altura + `NearestWithin`) e a chave do telhado (`StoreyKey`) |
| `AI/LevelIntent.cs` | a ordem de nivelar o terreno e o tamanho (E10) |
| `AI/BuildFailure.cs` | `Classify`/`IsDependency` — o **portão de apoio** da obra (dependência × falha real), a tabela de `why` |
| `AI/BuildPieceKind.cs` | `Classify`/`IsStructural` — **estrutura × mobília**: quanto uma peça pode sair da pose do plano (1 m × 5 m) |
| `AI/BuildSupportBelow.cs` | `IsDirectSupport`/`Rank`/`CanSupport`/`PickPost`/`UsefulSurface` — o **apoio direto** de uma peça (a janela 0,4–2,5 m abaixo × 2,5 m em XZ), as classes que apoiam o quê, o **poste de emergência** que alcança um vão e a **caminhada do raio** até a superfície útil |
| `AI/BuildBody.cs` | `Blocks`/`HalfY` — a **janela do corpo** na colocação (`blocked_by_player`): a cápsula do personagem contra a caixa **girada** da peça (o painel vertical e o plano, no frame da peça) |
| `AI/BuildStationCover.cs` | `Required` — a **bancada exigida só fora do blueprint** (`[Building] BlueprintStations`): template e peça única nunca dispensam, blueprint dispensa com a chave ligada |
| `AI/BuildMaterials.cs` | `Add`/`Total` — a **necessidade de materiais** de um plano (custo por peça × quantidade, o recurso repetido soma, entrada sem item ou com quantidade ≤ 0 ignorada e contada), a mesma conta que `BuildStock` paga peça por peça |
| `AI/MHR_Owner.cs`, `Core/MHR_List.cs` | dependências reais dos acima |

`Stubs.cs` tem **só** os membros do lado Unity/config que esses arquivos tocam, e cada um está comentado com o
arquivo:linha real de onde veio:

- `CompanionsPlugin.Log` (o logger do BepInEx);
- `ModConfig` — as quatro listas de coleta com os **valores de fábrica** (`ModConfig.cs:928-931`), num `ConfigFile`
  que **nunca** toca o disco (o `.cfg` do jogador não é lido nem escrito). Se o default de uma lista mudar no
  `ModConfig.cs`, o texto aqui tem de acompanhar (só o caso "lista do trabalho" da seção (f) depende dele);
- `CompanionSetup` — as constantes de modo (`CompanionSetup.cs:137-161`) e as chaves de ZDO. Se um número de modo
  mudar, a cópia do `JobMode` diverge da DLL e a seção (h) acusa;
- `CompanionLocale` — só `Current.KokoroCode`;
- `MHR_GatherRules` — só `Parse`/`ItemLocal`, fiéis ao comportamento **fora do jogo** (sem `ObjectDB`);
- `CompanionOrders` — só `JobMode` e `IsDoorRequest`, **cópias verbatim** (`CompanionOrders.cs:1168-1195` e
  `812-869`), porque o resto da classe precisa do mundo. A cópia é conferida contra a DLL real na seção (h) — se
  ela divergir, o harness **falha**;
- `ObjectDB` (namespace global, de propósito) — o banco de itens do jogo. O tipo real **não carrega** fora do Unity
  (`UnityEngine.Object` estoura no inicializador de tipo), e `OrderItems.Known` é escrito justamente para o caso
  "ainda não há banco de itens, então o mapa é confiado" — é esse caminho que o harness exercita.

## As seções

| Seção | Conteúdo |
|---|---|
| (0) | pré-condições: `ObjectDB.instance == null` (sem mundo) |
| (a) | `RadialIntent.Correct`: `"postura equilibrada"` → `stance_balanced`, `"para de vagar"` → `nowander`, `"ignora os comandos"` → `nocommand`, `"acende o forno"` → `smelt`, `"cuida da cozinha"` → `cook`, `"vai caçar"` → `hunt`, mais as guardas (criatura nomeada mantém o ataque, verbo de reparo mantém o `repair`, `?` nunca corrige, >10 palavras nunca corrige, ação precisa nunca é sobrescrita) |
| (b) | `PickupIntent.NeighborAction`: `"jogue fora a madeira"` → `drop`, `"junte a madeira do chão"` → `pickup`, `"para de recolher"`/`"não pegue nada"` mantêm `nopickup`, negação a 4 palavras (`"não quero que você jogue fora…"`) e palavra inteira (`"largada"`) |
| (c) | `GatherIntent.FromModelAction`: `"colete 10 pedras"` → `stone/10`, `"ramasse 10 bois"` → `wood/10`, `"raccogli 5 pietre"` → `stone/5`, `"pega a madeira do chão"` mantém `pickup`, lado `none` exige verbo de coleta |
| (d) | `InteractionIntent.Correct`: `"fecha a porta"` (com o modelo dizendo `inventory_close`) → `door`, `"senta na fogueira"` → `sit`, `"guarda isso no baú"` (com `drop`) → `deposit`, `"esvazia a colmeia"` → `hive`, `"sobe no barco"` → `board`, e as recusas (pergunta, `"para de…"`, negação, `"joga a madeira no chão"`, mochila na mesma frase) |
| (e) | `LlmIntent.Parse`/`ExtractContent`: JSON válido, JSON com texto em volta, `amount` entre aspas, `amount` inválido/negativo, ação desconhecida → `none`, `"null"` → `none`, uma palavra solta, `target` × `resource` em `drop`, corpo de `chat/completions` |
| (f) | `OrderItems.Resolve` nos **5 idiomas** (PT/EN/ES/FR/IT) para madeira, pedra, minério e forrageio, o caminho "sem recurso" (`wood` → `Wood`, `ore` → `null`), o recurso desconhecido, e a **lista do trabalho** como último lugar procurado; `GatherAction` (o recurso manda) |
| (g) | `CompanionNameMatch`: nome no começo, com vírgula, depois de `"Ei,"`, nunca no meio da frase, artigo, acento, e o `AddressCandidate` (`"Fulano,"` sim; `"mano,"`, `"bom dia,"`, sem separador, `"Ei,"` não) |
| (h) | as cópias do `Stubs.cs` contra a **`Companions.dll` real** por reflexão (18 mensagens × `IsDoorRequest`, 18 ações × `JobMode`) |
| (i) | `BuildCatalog`: o nome da peça vira o prefab (bancada, baú, fogueira, cama) nos 5 idiomas e as palavras (`HasWord`/`HasAnyWord`) |
| (j) | `BuildTemplates`: os nomes nos 5 idiomas, a composição de cada estrutura (cabana 7, abrigo 8, casa 57/46/21, tenda 4), a ordem (bancada → piso → parede → telhado → porta) e os deslocamentos medidos |
| (k) | `LevelIntent`: a ordem de nivelar nos 5 idiomas e o tamanho que o jogador disse |
| (l) | `BuildBlueprintParser.Parse`: V1/V2, vírgula decimal, linha quebrada, arquivo vazio, `(Clone)` fora do prefab, e `Ordered` pela classe/altura |
| (m) | `BuildBlueprintParser.NearestWithin` (a ordem **dentro** do grupo): o zigue-zague vira caminho curto (12 m em vez de 30,05), os grupos não se misturam (y=0 antes de y=4 e o de cima começa onde o de baixo parou), a porta continua por último (inclusive o run das portas), o run de 1 peça não é tocado, 5 mm de altura não abrem grupo, o guloso **nunca fica pior que o arquivo** (o caso da armadilha 0/10/20 a partir de x=10: 30 m × 20 m, o arquivo fica) e a estabilidade (duas listas iguais → mesma ordem; a lista do chamador não é reordenada) |
| (n) | `BuildBlueprintParser.StoreyKey` (a chave do telhado): a peça de **telhado** (classe 3) pertence ao andar da **laje que ela cobre** — o piso mais próximo **acima** dela (0,3–6 m, 2,5 m em XZ). Casos: telhado escrito ANTES do piso a **3 m** (pé-direito real) → o piso vem antes; piso a **5 m** (pé-direito alto) → o piso ainda vem antes; piso a **7 m** (fora da janela) → não conta e o telhado fica no andar de baixo; piso **2,4 m ao lado** (dentro dos 2,5 m) conta e **3 m ao lado** não conta; com pisos a 3 m e 5 m a laje é a **mais próxima** (a ordem sai 3, 0, 5); telhado sozinho e telhado sobre um piso do **mesmo nível** (dy = 0 não é "acima") → andar de baixo; **porta por último** com piso de baixo, parede, piso de cima e telhado; e um andar só (piso → parede → telhado). **Cobertura provada por mutação** (03/11/2026, cópia do parser em `%TEMP%`, `src/` intocado): voltando a chave a `StoreyOf` puro **6 asserções falham** (3 m, 5 m, 2,4 m ao lado, mais próxima, alturas e a ordem completa do caso da porta); com a chave antiga `StoreyOf + 2` (commit `63d8096`) **as mesmas 6 falham** — antes desta seção o harness passava nos dois casos |
| (o) | `NavTileMath.Axis` (a matemática do tile da navmesh, `floor((world + size/2)/size)`): 10 casos — as emendas em ±16 nos dois lados (o `Floor`, não o truncamento) e a borda de 2 m (`15` × `17`). |
| (p) | `BuildFailure.Classify`/`IsDependency`/`IsBlocked` (**o portão de apoio** da obra, `AI/BuildFailure.cs`): 28 casos — `no_support`, `no support` (duas palavras), `needs_station` e `no_ground` **com** algo embaixo (`groundBelow=true`) são **`Dependency`** (não gastam tentativa, nunca contam como pulada); `blocked_by_player` é a classe **`Blocked`** (04/11/2026: um corpo no lugar não é culpa da peça — também não gasta tentativa, com ou sem `groundBelow` e em qualquer caixa, `BLOCKED_BY_PLAYER` incluso); `no_ground` **sem** nada embaixo, `no progress`, `clipping`, `too_steep`, `can't reach it`, `took too long`, `verification: not_found`/`verification: fallen`, `""`, `null`, e um status desconhecido são **`Real`** (gastam uma das 3 tentativas). Cobre também maiúsculas (`NO_SUPPORT`/`NO_GROUND` continuam dependência — a comparação ignora caixa), a forma de conveniência `IsDependency` nos dois sentidos do `no_ground`, o `IsBlocked` nos dois sentidos e as **constantes da barreira de andar** (`BodyRetryDelay` 2 s, `BodyRetryStep` 1 m, `BodyRetryMaxWait` 25 s). |
| (q) | `BuildPieceKind.Classify`/`IsStructural` (**estrutura × mobília**, `AI/BuildPieceKind.cs`): 26 casos — piso, fundação, parede (inteira e meia), telhado, empena, viga, pole, escada, escadinha, porta e portão = **`Structural`** (o ajuste de pose só pode andar **≤ 1 m**); baú, cama (as duas), fogueira, bancada, fermentador e caldeirão = **`Furniture`** (até **5 m**, e 3 anéis × 12 direções na segunda tentativa); maiúsculas (`WOOD_FLOOR`, `Piece_Chest_Wood`) caem no mesmo lado; `""`/`null` = **`Structural`** (peça sem nome não se move) e um nome que o vocabulário não conhece (`xyz`) = **`Furniture`** (mobília é "tudo o que não é estrutura": a peça ainda sobe). É a decisão que o `NudgePiece` usa depois de um `too_steep`/`no_ground`. |
| (r) | `BuildSupportBelow.IsDirectSupport`/`Rank`/`CanSupport`/`PickPost`/`UsefulSurface`/`HoldsPlacedPiece` (**o apoio de uma peça**, `AI/BuildSupportBelow.cs`): 66 casos — a janela do que segura uma peça (0,4 a 2,5 m **abaixo**, até 2,5 m em XZ, o caso medido do andar de cima a 1 m) e as bordas (mesmo andar = vizinha e não apoio, peça **acima** = nunca, 0,39/0,4 m, 2,5/2,6 m abaixo, 2,5/2,6 m ao lado, diagonais de 2,40/2,55 m e o lado negativo); o `Rank` (a queda pesa **10×** a distância: o de 0,5 m abaixo ganha do de 2 m, e com a mesma queda o mais perto ganha) e os três números da janela; a **classe do pull** (`CanSupport`): **telhado sobre telhado** (a fileira de baixo da água — a empenha leste do run de 04/11 ficou aberta porque todo telhado era recusado como apoio), telhado apoiado no **piso** 2 m abaixo (roof 46,8 / floor 44,8, medido), na **parede/viga** e na **escada**; piso e parede **não** são apoiados por telhado; telhado e piso **não** são apoiados por porta; piso sobre piso; e a porta como **peça** (apoiada por parede). O **poste do apoio de emergência** (`PickPost`): o comprimento sai do **vão** entre a peça e a superfície de baixo — 1,02 m → viga de 1 m (o caso medido do deck), 1,4 m → **nenhum** (1 m curto demais, 2 m passaria), 1,9/2,2 m → viga de 2 m, 2,5/2,87 m → a de 45° (2,83 m), 3,2 m → nenhum, 0,5 m → nenhum (passaria através da peça); os dois vãos **medidos** da casa de 11/11 — **0,60 m** (o degrau entre dois pisos) → viga de 1 m e **1,60 m** → viga de 2 m — e o **buraco** de 1,30 m → **nenhum** (1 m fica 0,1 m além da tolerância e 2 m passaria 0,7 m); prefab inexistente (comprimento 0) é pulado, a escolha é pelo **comprimento** e não pela ordem, lista nula → −1, e os dois números da janela (0,20 m de folga para baixo, **0,45 m** de sobreposição — com 0,35 o vão de 0,60 m ficava sem poste). A **caminhada do raio** (`UsefulSurface`): a beira do telhado a **0,16 m** + o deck a **1,02 m** → o **deck** (o caso medido das 318 recusas "vão de 0,16 m"), só o deck, duas superfícies coladas (0,16/0,35) → nenhuma (terreno), a borda de 0,40 m que serve e 0,39 que ainda é colado, o primeiro utilizável mandando na ordem do raio, só as `count` primeiras entrando, e sem hit/sem lista → terreno; e a superfície tem de **segurar** (04/11/2026: o poste nasceu em cima de **baú** e o jogo recusou o poste com `no_support` 95 vezes) — profundidade 0,5/1,2 m com `holds` falso/verdadeiro → a de 1,2 m, e as duas falsas → −1 (terreno). E o **apoio da peça colocada** (`HoldsPlacedPiece`, o `FootprintSupported`): **telhado sobre telhado** com `m_supports = false` → **true** (o caso das 13 peças que ficaram 5 rodadas de fora), telhado no topo de parede/viga, telhado no **terreno**; piso sobre piso/viga que carrega → true e piso/parede/escada sobre peça com `m_supports = false` → **false** (mobília não carrega); pedra/árvore (sem `WearNTear`) não apoia nada; e a **porta** apoiada por parede. |
| (s) | `BuildBody` (**a janela do corpo** na colocação, `AI/BuildBody.cs`): 18 casos — o **caso medido** (piso do deck a 39,8 sobre o companheiro com os pés em 38,78 → **bloqueia**; era o bug "dois companions travados embaixo do piso"), o mesmo piso a 2,5 m → livre e a 1,5 m → **livre** (o **disco** de 1,59 m recusava: era falso positivo — só a largura real da caixa conta); piso **2 m acima da cabeça** → livre e piso **no nível dos pés** → bloqueia; **parede** de 2 m com a base na altura do corpo → bloqueia e a mesma parede inteira acima da cabeça → livre (o companheiro passa embaixo); companheiro **de pé no deck**: a telha vizinha a 2 m → livre e **na emenda (1,4 m) → livre** (o topo da peça é o pé do corpo: o disco recusava, falso positivo); **Run 10** (medido): o feixe de 2 m com o fundo em 41,7 sobre a cápsula 39,9–41,7 → **livre**, e a mesma geometria com a cápsula até 41,9 → **bloqueia** (a cápsula decide); o **pivô** do telhado (centro +1,5 m: o fundo real é `point+1,3`, não `point-0,2`) → livre; a **viga de 45°** (`HalfY` ≈ 0,78 m) → bloqueia contra 0,1 m sem a inclinação → livre; companheiro **ao lado** da parede (0,5 m do plano médio) → livre e **encostado** (0,35 m) → bloqueia (o par fixa o raio de 0,3 m); e o companheiro **debaixo do piso** (XZ dentro) → bloqueia. |
| (t) | **a barreira de andar** (04/11/2026, o Run 9: a escadinha da entrada, peças 8-10 de 988, recusada por corpo e só retomada no fim): 27 casos — `BuildFailure.IsTransient` é **só** `blocked_by_player` (13 checagens sobre 14 entradas: `blocked_by_player`/`BLOCKED_BY_PLAYER` → true; `no_support`, `no support`, `needs_station`, `no_ground`, `clipping`, `too_close_piece`, `no progress`, `can't reach it`, `took too long`, `verification: not_found`, `""`+`null` → false); `BuildFailure.ShortRetry` (10: 0/1/2 → true, 3/4 → false, a caixa e quatro falsos de outros motivos); as **constantes** da barreira (3 curtas, 2,5 s, 1,5 m de mesmo andar, 20 s); e o **nível** da peça (`BuildBlueprintParser.Line.Level`, a mesma chave do `Ordered`): o telhado herda o piso que cobre (3 m → nível 3), a escada tem o próprio nível (2, com um piso no mesmo nível) e preenchê-lo **não muda a ordem** (piso/parede do térreo → piso de cima → telhado). |
| (u) | `BuildSupportBelow.FallbackPostAllowed` (**o poste de emergência quando o apoio do plano não vem**, `AI/BuildSupportBelow.cs`): 6 casos — apoio **desistido** (no `SkippedIndex`) → `true` mesmo com outro companheiro em cima (ele nunca vai subir); apoio **reivindicado** (`OwnerOf != 0`) → `false` mesmo depois de 5 esperas (está chegando); sem reivindicação e com **0 ou 1 espera** → `false` (o plano ainda tem a chance — o caso que congelou a coluna do Run 9) e com **2 esperas** → `true` (o terceiro olhar, ~20 s = 8 s + 12 s do `DependencyDelay`); e `SupportGrace == 2`. |
| (v) | `BuildStationCover.Required` (**a bancada exigida só fora do blueprint**, `AI/BuildStationCover.cs`): 5 casos — template com a chave ligada → **true** e com a chave desligada → **true** (a chave não alcança template nem peça única); blueprint com a chave ligada → **false** (dispensada) e desligada → **true** (regra vanilla); e o **fallback da config ausente** (`BuildStationCover.Default`, o mesmo número que o `MHR_Build.StationRequired` usa) com blueprint → **false**. |
| (w) | `BuildMaterials.Add`/`Total` (**a necessidade de materiais de um plano**, `AI/BuildMaterials.cs`): 9 casos — 2 peças iguais multiplicam (4 × 2 = 8); o recurso repetido no mesmo prefab soma (2 + 3 = 5); peça sem receita custa 0 e conta em `noRecipe` (1); plano vazio → nada e `noRecipe` 0; `count = 0` não soma e não ignora entrada; item nulo/vazio e quantidade ≤ 0 ignorados **e contados** (3 de 4); 2 peças diferentes com o mesmo recurso somam (4 + 2×2 = 8); valor alto (1000 × 1000) não vira negativo; e `Total` duas vezes em dicionário novo dá o mesmo resultado (sem estado entre chamadas). |

Hoje: **775 asserções medidas** — 656 até a (w) (471 até a (o) + 28 da (p) + 26 da (q) + 66 da (r) + 18 da (s) + 27 da (t) + 6 da (u) + 5 da (v) + 9 da (w)), **+ 10 da (x)** (`BuildWalkSpot.Choose`), **+ 18 da (y)** (`BuildAsideSpot.Plan`), **+ 22 da (z)** (`BuildSpread`, a dispersão), **+ 15 da (aa)** (`BuildSpread.Nearest`), **+ 22 da (ab)** (`BuildSpread.Chunk`), **+ 5** do ciclo da `fila`/`DeckLift` (sem seção nova) e **+ 20 da (ac)** (`BuildPieceKind.IsTip`, §17.27 do `building-notes`).

A soma itemizada dá **768**: **7** asserções antigas não estão em nenhuma seção listada aqui (dívida registrada em `docs/backlog-construcao.md` item 15). O número que vale é o medido, e é o que o gate roda.

## O que **não** é coberto (e por quê)

- **`CompanionOrders.Run` / `CompanionChatAI.HandleResult`** (a ordem de chamada das redes e o despacho): precisam de
  companheiro, ZDO, inventário e mundo. É o que o teste em jogo e o `valheim-devbridge` cobrem.
- **O lado Unity do `CompanionNames`** (`Mine`/`Find`/`FromMessage`/`PromptLine`): idem (Player, `ZNetView`, ZDO,
  `Character`, `GetComponent`). O harness cobre as **regras de string** (`CompanionNameMatch`), que é onde os bugs de
  endereçamento moram.
- **"Esse prefab é um item que o jogo tem"** (`OrderItems.Known`): fora do jogo não há `ObjectDB`; o harness roda o
  ramo "sem banco de itens, o mapa é confiado". Um nome errado no mapa só apareceria em jogo.
- **`CompanionChatAI.ValidActions` / `Contract`** (35 ações e o schema): não são redes por palavras; o número é
  conferido por reflexão na DLL em auditoria. Se quiser fixá-lo aqui, é uma seção nova lendo a DLL.
- **`IsInventoryRequest`**: o harness cobre o caso da porta que o `InteractionIntent` usa; a outra metade (abrir a
  mochila) não foi pedida nesta rodada.

## Convenções

- **`KNOWN-FAIL`**: quando um caso revela um bug real, o caso **não** é consertado nem vira `PASS`: use
  `KnownFail("nome", aindaQuebrado, "detalhe")`. Ele sai como `KNOWN-FAIL` (fora da conta PASS/FAIL) e, se o bug
  sumir, sai como `KNOWN-FAIL*` com o aviso de que a marca pode sair. **Nenhum caso usa a marca hoje.**
- O harness **não** altera `src/`, não escreve config e não roda o jogo.
- `AI_COMPANIONS_DLL` aponta para outra `Companions.dll` no cross-check; `VALHEIM_DIR`/`AI_COMPANIONS_MANAGED`
  apontam para a instalação do jogo (por padrão, o caminho do `Environment.props` / Steam).

## Quando rodar

- Depois de mexer em qualquer um dos arquivos linkados acima (o build já compila o harness; o `dotnet run` é a
  prova).
- Antes de release que mexa em ordens, parser, contrato ou nas redes por palavras.
- Trava da regra 4 (o harness só ganha asserção): `python tools/check_harness_additions.py` falha se alguma `Check(`/`KnownFail(` foi removida ou alterada contra HEAD (`--base <ref> [--to <ref>]` para um commit; `--selftest` para os casos sintéticos).
