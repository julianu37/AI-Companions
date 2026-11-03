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
| `AI/BuildBlueprintParser.cs` | `Parse` (V1/V2) e `Ordered` (classe/andar/altura + `NearestWithin`) |
| `AI/LevelIntent.cs` | a ordem de nivelar o terreno e o tamanho (E10) |
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
