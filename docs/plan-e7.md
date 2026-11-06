# E7 — construir os planos do jogador: análise e decisão (02/11/2026, rev. 2)

> **Pergunta:** como o companheiro constrói uma estrutura **grande** que o jogador planejou, sem a gente reinventar
> planejador, biblioteca de blueprints e ordem de apoio?
>
> **Premissa corrigida:** o jogador **não usa** o PlanBuild hoje. O link do repo abre normal (o 404 foi o caminho
> errado da minha busca, não o projeto). O critério que decide é: **qual caminho dá a maior biblioteca de builds com
> a menor instalação extra**.
>
> **DECIDIDO E FEITO (02/11/2026): B.** O caminho do `.blueprint` está implementado e medido em jogo - ver
> `docs/re/building-notes.md` §15: `hs_meadows_hut` (BiomeBlueprints) **28/28 `supported`** e o `TestBox_V2` do
> PlanBuild 5/5 com 1 linha pulada. Config `[Building] BlueprintFree` (false) e `BlueprintMaxPieces` (2000), ordem pelo
> **nome do arquivo**. A ponte do BuildOrders (A) fica para depois, quando o jogador quiser planejar no F11.

---

## 1. O que já existe (estado conferido em 02/11/2026)

| Projeto | Versão / data | Dependências | Licença | O que dá |
|---|---|---|---|---|
| **Quads Build Orders** (HardHeadHackerHead) | **1.14.7** (17/10/2026) | **só BepInEx** | MIT | Planejador F11, blueprints **JSON**, `_pieces.json`, toolchain Python e **API pública de "ajudante"** (`Count`/`Next`/`Place`/`Blockers`) |
| **Quad's Companion** (mesmo autor) | **0.25.1** (22/10/2026) | só BepInEx (BuildOrders opcional) | MIT | Já **constrói os planos do BuildOrders** — prova de fluxo |
| **PlanBuild** (sirskunkalot) | **0.20.0** (01/10/2026) | BepInEx **+ Jotunn 2.30.1** | WTFPL | Martelo de planejamento, totem que constrói com material, **`.blueprint`** (captura, salvar/carregar, espelhar) |
| **BiomeBlueprints** (OverDrive) | 1.0.3 (27/08/2026) | PlanBuild 0.18.4 + Jotunn | — | **353 blueprints** prontos por bioma |
| **valheimians.com** | — | — | — | Biblioteca/comunidade de builds em `.blueprint` |
| **Buildheim** (AugusDogus) | — | Jotunn | MIT | Blueprint client-only + autobuild pelo martelo (referência de UI) |

**Onde está o conteúdo:** a **biblioteca grande é de `.blueprint`** (PlanBuild): 353 do BiomeBlueprints + o site
inteiro + a captura do próprio jogo. A biblioteca do BuildOrders é pequena (exemplos e uma toolchain para dev).

---

## 2. Os três caminhos possíveis

### (A) Ponte do BuildOrders — "constrói o que eu marquei no F11"

- **Jogador:** instala o BuildOrders (só BepInEx) e planeja no F11; ou importa um JSON.
- **Nós:** **2-3 dias** (API documentada, `AppDomain`, provada pelo Quad's Companion).
- **Achado de build:** **difícil** — planejar 200 peças à mão não é "escolher uma build"; o acervo é pequeno.
- **Risco:** baixo. **Instalação extra:** 1 mod.

### (B) Importar `.blueprint` — "constrói esta build que eu baixei"

- **Jogador:** baixa um `.blueprint` (BiomeBlueprints/valheimians) ou captura a própria; põe na pasta do mod e diz
  "construa a casa viking aqui". **Não precisa do PlanBuild instalado** para construir.
- **Nós:** **4-6 dias** — parser (1), ordem de apoio a partir de alturas/tipo (1-2), executar com o `MHR_Build` (0,5,
  já validado), terreno do blueprint (1, ou ignorar na v1), testes/docs (1-2).
- **Achado de build:** **o melhor** — 353 prontos + o site + a captura; e sem instalar mais nada em runtime.
- **Risco:** médio (formato do PlanBuild muda; blueprint com peça de mod de terceiro não resolve).
- **Instalação extra:** **nenhuma** (o arquivo basta). O PlanBuild só é necessário para *capturar* builds.

### (C) Ler os fantasmas do PlanBuild — "constrói o plano que eu acabei de colocar"

- **Jogador:** instala o PlanBuild (+Jotunn) e coloca o fantasma do blueprint com o martelo dele.
- **Nós:** **2-3 dias** — reflexão na lista estática `PlanPiece.m_planPieces` + `originalPiece` + transform.
- **Achado de build:** bom (a mesma biblioteca), **mas** só para quem já instalou PlanBuild+Jotunn.
- **Risco:** médio-baixo (reflexão, fail-open). **Instalação extra:** 2 mods (PlanBuild + Jotunn).
- **Só vale se o jogador (ou o público) já usar o PlanBuild.**

### (D) Delegar ao totem do PlanBuild (rejeitada)

`PlanTotem.UpdatePlanTotem` é coroutine com contabilidade de material própria, sem API pública; e o jogador teria que
colocar um totem — o companheiro viraria "operador de totem". Fora.

---

## 3. Comparação

| critério | A (BuildOrders) | **B (`.blueprint`)** | C (fantasmas do PlanBuild) |
|---|---|---|---|
| facilidade de achar uma build | baixa | **alta** | alta (mas exige PlanBuild) |
| dificuldade para o jogador | média (F11 peça por peça) | **baixa** (arquivo + frase) | baixa, se já usa PlanBuild |
| instalação extra | 1 mod | **nenhuma** | 2 mods (PlanBuild + Jotunn) |
| dificuldade de desenvolvimento | **2-3 dias** | 4-6 dias | 2-3 dias |
| risco de quebrar | baixo | médio (formato) | médio-baixo |
| reaproveita o que temos | pouco | **muito** (executor, estoque, apoio) | muito |

---

## 4. Recomendação: **B primeiro; A depois (barata); C só se o público pedir**

1. **B** é o único caminho que dá a **biblioteca grande** com **zero instalação extra** — e "encontrar uma build pra
   fazer" é exatamente a pergunta do jogador.
2. **B reaproveita** o que a E6 já validou: o `MHR_Build` em modo plano (57/57), o `BuildStock` (mochila → baús →
   coleta com retomada), a porta por último e o `BuildSupport`. O trabalho novo é **ler o arquivo** e **ordenar**.
3. **A** continua valendo para planejar **dentro do jogo** (F11): é barata (2-3 dias), a API é pública e o fluxo já
   foi provado por outro mod. Entra depois, como segunda ponte opcional.
4. **C** só faz sentido para quem já tem PlanBuild+Jotunn; é a mais fácil de implementar, mas a de maior custo de
   instalação para o jogador. Deixar para o fim (ou nunca).

### Como a **B** funcionaria (o desenho)

1. **Pasta de builds:** `BepInEx/plugins/AI-Companions/blueprints/*.blueprint` (era
   `BepInEx/config/AI-Companions/blueprints` até 04/11/2026; o mod cria a pasta no primeiro uso e **copia** a antiga
   uma vez, sem sobrescrever; o LEIA-ME/wiki
   ensina a baixar).
2. **Ordem:** "construa a `<nome>` aqui" / `build <nome>` → o `BuildCatalog`-like resolve o nome do arquivo (sem
   acento, sem espaços); sem nome, ele lista as builds disponíveis na fala ("tenho 3: casa viking, celeiro, torre").
3. **Âncora:** o ponto mirado (o mesmo `FindAnchor`/validador de hoje); o blueprint é posicionado relativo à âncora
   com o **yaw da mira** (como os templates), e o `FindAnchor` valida o conjunto todo.
4. **Leitura:** `.blueprint` é JSON (`PieceEntry`: prefab, posição, rotação, escala). Nomes de peça → `Piece` pelo
   `BuildProbe`; peça desconhecida → fala "esta build usa uma peça que você não tem" e não começa.
5. **Ordem de apoio:** agrupar por altura (piso → parede → telhado) e validar cada peça com o `BuildSupport`; peça sem
   apoio vai para o fim e, se continuar sem apoio, é pulada com uma linha no log.
6. **Material:** `BuildStock` (mochila → baús → coleta do resto com retomada no ponto exato), como nos templates.
7. **Terreno:** v1 **ignora** o `TerrainModEntry` (o jogador nivela antes com a ordem `level`, se quiser); v2 aplica
   pelo `BuildTerrain` se o jogador pedir.
8. **Sem arquivo / build inválida:** responde na hora, sem quebrar nada.

### Aceite da B

- Baixar um `.blueprint` de exemplo, colocar na pasta, dizer "construa a `<nome>` aqui" e ver o companheiro construir
  **todas** as peças do arquivo, com `supported` em cada uma, pagando de mochila/baús e coletando o que faltar;
- log: `blueprint <nome>: N pieces`, `piece k/N`, o motivo de cada pulo;
- nome errado → fala a lista; peça desconhecida → fala e não começa;
- sem pasta/arquivo: nada quebra.

## 7. Avaliação do **AICompanion** (Quad) como módulo (03/11/2026)

O jogador perguntou se não estamos reinventando a roda, já que o **AICompanion** do mesmo repo (MIT) "constrói sozinho".
Li o `mods/AICompanion/Building.cs` (424 linhas) e varri os **48 arquivos** do projeto. O que ele faz, exatamente:

1. `Count(home, raio)` a cada 5 s → quantos fantasmas do BuildOrders faltam perto da casa;
2. `Next(home, raio, 24)` a cada 10 s → até 24 peças **na ordem de apoio** ("as que vão ficar de pé"), com uma
   *blacklist* por peça de 600 s (`BuildSkip`);
3. exige **martelo** (faz um se não tiver) - decisão de projeto deles, não do jogo;
4. paga da **mochila**; se não cobre, faz uma **viagem aos baús** levando o material de **várias** peças de uma vez
   (orçamento de 60% da capacidade de carga) e `Take` no baú;
5. se nem os baús cobrem, vira um **objetivo** (`Goals.ForNeeds`): coletar/fundir/fabricar/pedir a você - e um
   `CatchUp` faz o mesmo enquanto você estava fora;
6. `Place(id)` = **`Player.PlacePiece` deles** (crédito/barulho/estatística vão para o jogador) e desconta o material;
7. quando nada dá, usa `Blockers` (`unlocked|station|protected`) para **dizer por que** não dá.

**Nivelamento de terreno: NÃO faz.** A varredura dos 48 arquivos não achou `Terrain`/`Hoe`/nivelar (só o
`Attack_SpawnOnHitTerrain` do jogo e "level" de bancada/EpicMMO). Quem nivela é o planejador do BuildOrders (ação
"Level" em blueprint), não o companheiro. Ou seja: a nossa E10 (enxada ponto por ponto) é **mais** que eles, e é o
nosso diferencial.

**Por que NÃO importar o AICompanion "como módulo":** ele não é um módulo de construção - é um **cérebro de companheiro
inteiro** (48 arquivos: `Brain`, `Duties`, `Work`, `Life`, `Needs`, `Menu`, `Journal`, `Profile`, `StarLevels`,
`EpicMmo`...). O equivalente no nosso mod é `CompanionSetup` + `CompanionAI` + os jobs. Trazer "como módulo" seria
trazer um segundo cérebro e brigar com o nosso. A licença MIT permite; a arquitetura não recomenda.

**O que vale trazer (as ideias, não o framework):**

| deles | como entra no nosso |
|---|---|
| `Count`/`Next`/`Place`/`Blockers` (AppDomain) | **a ponte** (`AI/BuildOrdersLink.cs`): ordem de apoio e "por que não" calculados pelo planejador |
| `TryCreateBuildingShell` (2.048 peças validadas) | transformar o `.blueprint` num **plano do BuildOrders** e deixar a estabilidade para ele |
| `Blockers` → fala (`unlocked`/`station`/`protected`) | as nossas recusas de plano passam a usar o motivo real do planejador |
| `piece.FreeBuildKey()` (mundo com custo zero) | o `BlueprintFree` passa a **respeitar o modificador do mundo** também |
| viagem aos baús com orçamento de várias peças | já temos (`BuildStock.WithdrawTemplate`), mas o orçamento de 60% é uma ideia boa para builds grandes |
| martelo obrigatório | **não** copiar (o nosso executor não precisa; é escolha de projeto) |
| `Place` deles (`Player.PlacePiece`) | **não** copiar: o nosso executor coloca sozinho (E6) - mas para um plano do BuildOrders a peça só existe com o `Place` deles, então ali o crédito vai para o jogador (documentar) |

**Decisão proposta:** `AI/BuildOrdersLink.cs` (2-3 dias): os 4 helpers por `AppDomain`, o `Shell` por reflexão com
`PlanningApiVersion == 1`, e um modo `Orders` no `MHR_Build` (`Next` → andar → pagar → `Place` → verificar, com
`Blockers` na recusa). Com BuildOrders instalado, o `.blueprint` **também** passa pelo `Shell` (estabilidade deles);
sem ele, a nossa leitura continua valendo. É o "módulo" no sentido certo: a **lógica** deles dentro da nossa casa.

### Perguntas abertas

1. ~~Confirma a B primeiro?~~ **Confirmado: B.** O A (F11) fica para o próximo passo
2. **De onde vêm as builds?** BiomeBlueprints (353) e valheimians são `.blueprint` do PlanBuild — quer que eu já
   deixe um pacote de exemplos na pasta (2-3 pequenas) para testar?
3. **Materiais:** mochila → baús → coleta (recomendação) — ou o jogador quer "material infinito" numa opção de teste?
4. **Nome da ordem:** `build <nome>` / "construa a casa viking aqui" / "constrói a build aqui"?
