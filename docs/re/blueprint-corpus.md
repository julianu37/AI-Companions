# O corpus de `.blueprint` que entrou na pasta do mod (medido em 04/11/2026)

Pergunta que originou esta nota: **o que acontece quando o jogador joga 353 planos de terceiros na pasta de
blueprints do mod, e o que esse corpus diz sobre o produto?** Ele existia como um `.zip` em `Downloads`
(`OverDrive-BiomeBlueprints-1.0.3.zip`) e passou a viver na pasta que o mod lê.

**Legenda (vale para o arquivo inteiro):**

- **[M] medido** — número/estado que saiu do disco, do `.zip`, do CSV da auditoria ou do log do jogo nesta sessão.
- **[I] implicação** — leitura de produto sobre o medido; **não** é fato verificado. Nada aqui foi
  implementado por causa desta nota.
- **[?] não medido** — o que a medição **não** cobre (lista no §7).

Fontes: o `.zip` (contagem e layout, medidos no disco), o CSV da auditoria
`%TEMP%\opencode\blueprint-audit.csv` (um `blueprints.inspect` por arquivo, leitor do próprio mod) e o código citado
por `arquivo:linha`. Nada foi construído **para** esta nota, e o único arquivo que ela cria é ela mesma.

## 0. Resumo em 11 linhas

1. **[M]** O pacote tinha **353 `.blueprint`** + **353 `.png`** de miniatura num único nível `blueprints/`, e o número
   **não era folclore**: o código cita "353 files (BiomeBlueprints)" em dois comentários
   (`AI/BuildBlueprints.cs:189` e `:343`).
2. **[M]** A cópia para `BepInEx\plugins\AI-Companions\blueprints` foi **sem sobrescrever**: **350** novos,
   **3** já existiam, e o `CommHomeBasicBP.blueprint` do jogador ficou intacto. A pasta fechou com **354
   `.blueprint`, 62,8 MB**.
3. **[M]** Por bioma (prefixo do nome): **ashlands 164 · blackforest 43 · plains 42 · meadows 34 · mistlands 33 ·
   swamp 21 · mountain 16** — as 7 categorias somam **353**; o 354º é o arquivo do jogador, sem prefixo.
4. **[M]** A auditoria leu os 354 nomes únicos (**366 linhas** no relatório, **9** duplicadas): **279 de 354**
   arquivos têm linha pulada, **387.024 linhas** puladas no total.
5. **[M]** **100 %** das puladas são `recipe not unlocked` (`AI/BuildBlueprintSkip.cs:18`) — **nenhuma** linha caiu
   por prefab inexistente (`not a piece here`, `:15`, não disparou em arquivo nenhum).
6. **[M]** Campeão: **`blackmarble_1x1` ×1.838** num único arquivo (Ashlands); **`darkwood_roof`** é o `top_reason`
   de **19** arquivos; **`wood_wall_log_4x0.5`** aparece **48×** num arquivo; **`piece_beehive`** 15 / 9 / 7 em três.
7. **[M]** Tamanho: **95** arquivos com **≥800 peças**, o maior com **8.371**; o menor tem **0**.
8. **[M]** Estações: **101** arquivos pedem **só bancada** (ou nenhuma); o resto pede talhador/forja/forja negra/
   artesão/mesa de magia.
9. **[M]** Do corpus, subiram em jogo o `hs_meadows_stilt_hut` (**32/32**, 0 puladas) e o `hs_blackforest_hut`
   (**28/28** em 24 s); `hs_meadows_house`, `hs_meadows_manor` e `hs_meadows_hobbit_repairhut` estavam **em
   andamento** quando esta nota foi escrita.
10. **[I]** O risco real de "blueprint baixado" **não é mod faltando** — é **receita não desbloqueada**, e hoje o
    jogador só vê o **número** de puladas, nunca o **motivo** (§6.1).
11. **[I]** E a pasta onde o corpus entrou é a que a release **leva**: o staging copiaria os **62,8 MB**, embora o
    `docs/references.md:15` já registre o pacote como "uso local, **não vai no pacote**" (§6.4).

## 1. O que entrou na pasta [M]

### 1.1 O pacote de origem

| medida | valor |
|---|---|
| arquivo | `OverDrive-BiomeBlueprints-1.0.3.zip` (em `Downloads`) |
| tamanho | **30,1 MB** |
| `.blueprint` | **353** |
| `.png` (miniatura) | **353** (um por blueprint, mesmo nome) |
| layout | **um único nível** `blueprints/`, **sem** subpasta |
| total de `.blueprint` na pasta do mod depois da cópia | **354** |
| tamanho da pasta do mod (`*.blueprint`) | **62,8 MB** |

As miniaturas entraram junto (a cópia foi do pacote inteiro). **O mod não lê as `.png`**: o leitor só abre
`*.blueprint` (`Directory.GetFiles(folder, "*.blueprint")`, `AI/BuildBlueprints.cs:207`) e as ocorrências de `.png`
no `src/` são as texturas de UI embutidas (`Core/TextureLoader.cs:42`/`:78`) e os screenshots do DevBridge
(`DevBridge/Commands.cs:2169`/`:2235`). Elas ocupam espaço na pasta e **não são usadas por nada hoje** — nenhuma
tela lista miniatura de blueprint.

### 1.2 A cópia (sem sobrescrever)

| medida | valor |
|---|---|
| arquivos novos | **350** |
| já existiam (duplicados do mesmo pacote) | **3** |
| `CommHomeBasicBP.blueprint` (do jogador) | **intacto** |
| `.blueprint` na pasta ao final | **354** |

O `CommHomeBasicBP.blueprint` continua legível pelo leitor do mod: a linha dele no CSV da auditoria é
**985 peças, 0 puladas, 7 andares, 990 linhas**, estações `$piece_stonecutter` + `$piece_workbench`, 23 prefabs. Ou
seja: a cópia não estragou o arquivo do jogador — é o mesmo plano de 985 peças que já tinha sido construído.

### 1.3 Onde o código lê (para não confundir pasta)

- A pasta é **`<plugin>\blueprints`** = `BepInEx\plugins\AI-Companions\blueprints`, resolvida por
  `Assembly.Location` (`AI/BuildBlueprints.cs:70-114`) — como `tts\`, `whisper\` e `Translations\`. É **conteúdo**,
  não config.
- Ela é **criada no primeiro uso** e a migração da antiga `BepInEx\config\AI-Companions\blueprints` só roda quando a
  pasta nova **ainda não existe** (`:77-84`) — é **cópia, nunca move**, e **não sobrescreve** homônimo
  (`AI/BuildBlueprints.cs:122-186`, log `[Build] blueprints: N arquivo(s) migrados de config para plugins`, `:174`).
  O pacote novo traz a pasta cheia, então a migração **não** roda nesta instalação.
- A listagem é **não recursiva** (`Directory.GetFiles`, `:195-218`) com **cache de 10 s** (`:188-192`). O pacote é
  plano (`blueprints/*.blueprint`), então nenhum arquivo ficou invisível ao mod por causa de subpasta.

## 2. Por bioma [M]

Contagem pelo **prefixo do nome** do arquivo:

| bioma | arquivos |
|---|---|
| `ashlands` | **164** |
| `blackforest` | **43** |
| `plains` | **42** |
| `meadows` | **34** |
| `mistlands` | **33** |
| `swamp` | **21** |
| `mountain` | **16** |
| **soma** | **353** |

**Conta:** a soma dá **353**, não 354 — o arquivo que sobra é o `CommHomeBasicBP.blueprint` do jogador, que não tem
prefixo de bioma. E a distribuição é **quase metade Ashlands** (164/353 = 46 %): o corpus é, na prática, um pacote
de fim de jogo. **[I]** Isso é o pano de fundo do §6.1: o conteúdo mais numeroso é justamente o que exige receita
que um mundo novo não tem.

## 3. A auditoria com o leitor do próprio mod [M]

### 3.1 O que roda

```
python tools/mcp-devbridge/audit_blueprint_corpus.py --text
```

- Usa o comando **`blueprints.inspect`** do DevBridge (`tools/mcp-devbridge/audit_blueprint_corpus.py:12-16`), que é o
  **mesmo leitor** da ordem e do painel de obra (`AI/BuildBlueprintReport.Read`, chamado em
  `DevBridge/Commands.cs:700` e em `UI/CompanionBuildPanel.cs:1088`). O relatório e o build **não podem discordar**:
  é o mesmo `BuildBlueprintSkip` (`AI/BuildBlueprintSkip.cs:8-10`).
- **Não começa nada**: `blueprints.inspect` só lê o arquivo e resolve os prefabs contra o mundo carregado
  (`DevBridge/Commands.cs:674-678`).
- CSV em **`%TEMP%\opencode\blueprint-audit.csv`**, uma linha por arquivo, com as colunas
  `file;sub;in_mod_library;duplicate_of;name;pieces;skipped;storeys;lines;pieceLines;brokenLines;stations;top_reason;prefabs;materials_skipped`.
- O script varre **as duas** pastas (a do plugin e a antiga `config`) e marca duplicata por **SHA-256**
  (`:169-179`) — por isso o relatório tem mais linhas do que nomes únicos (§3.2).

### 3.2 Arquivos

| medida | valor |
|---|---|
| nomes únicos | **354** |
| linhas no relatório | **366** |
| linhas marcadas como duplicadas | **9** |

**Conta:** 366 − 354 = 12, e **9** dessas linhas são duplicadas — as **3** restantes não foram abertas nesta
medição (§7). O que importa para o produto: **existe arquivo repetido no corpus**, e o mod não deduplica por
conteúdo (ele lista nomes).

### 3.3 Linhas puladas

| medida | valor |
|---|---|
| arquivos **com** pelo menos uma linha pulada | **279 de 354** |
| linhas puladas no total | **387.024** |

**Conta:** **79 %** dos arquivos (279 de 354) perdem pelo menos uma peça, e **138** deles perdem **mais da metade**
(§3.4) — a perda não está distribuída: o mesmo corpus tem **87** arquivos sem perder nada.

### 3.4 Tamanho dos planos

| faixa de peças | arquivos |
|---|---|
| **< 20** | **61** |
| **20–49** | **34** |
| **50–199** | **99** |
| **200–799** | **77** |
| **≥ 800** | **95** |
| menor / maior | **0 / 8.371** |

| linhas puladas (%) | arquivos |
|---|---|
| **0 %** | **87** |
| **1–10 %** | **36** |
| **11–50 %** | **105** |
| **> 50 %** | **138** |

**Conta:** as duas tabelas somam **366** — foram contadas sobre as **linhas do relatório** (com os duplicados), não
sobre os 354 nomes. E as duas se cruzam de um jeito duro: **138** arquivos perdem **mais da metade** das linhas e
**87** não perdem nada — a distribuição é **bimodal** (não é "meio quebrado" por igual).

**Conta (arquivo com 0 peças):** o mínimo **0** significa que existe arquivo que, depois do filtro, **não tem
peça nenhuma** para construir. Pelo código, plano vazio é **recusa nomeada**, não ordem vazia
(`MHR_Build.cs:1808-1813`, `plan.Count == 0` → falha dizendo o primeiro prefab que não deu para usar). **[?]** não
foi aberto **qual** arquivo é esse (§7).

### 3.5 O motivo das puladas — 100 % receita

| motivo | ocorrências |
|---|---|
| `recipe not unlocked` (`AI/BuildBlueprintSkip.cs:18`) | **100 %** dos casos |
| `not a piece here` (`:15`) | **0** |

**A consequência mais importante do corpus:** **nenhum** prefab dos 354 arquivos deixou de existir no jogo — todos
resolvem (`BuildProbe.PieceOf`, `MHR_Build.cs:1778`). O que falta é o jogador **conhecer a receita** daquele prefab
naquele mundo (`Player.IsRecipeKnown`, `MHR_Build.cs:1784`). Ou seja: **[I]** "blueprint de mod" não é o problema
deste corpus; **progressão de mundo** é.

### 3.6 Os campeões (o que mais falta)

| prefab | medida |
|---|---|
| `blackmarble_1x1` | **1.838** linhas puladas num **único** arquivo (Ashlands) |
| `wood_wall_log_4x0.5` | **48** num arquivo |
| `darkwood_roof` | é o `top_reason` de **19** arquivos |
| `piece_beehive` | **15** num arquivo, **9** noutro, **7** noutro |

**Exemplo de um arquivo inteiro** (linha do CSV, `hs_ashlands_20999_valheimian.blueprint`): **301 peças** no plano,
**943 linhas puladas**, **11 andares**, 1.290 linhas, estações `$piece_artisanstation` + `$piece_blackforge` +
`$piece_forge` + `$piece_magetable` + `$piece_stonecutter` + `$piece_workbench`, `top_reason`
`Piece_grausten_wall_1x2 (recipe not unlocked) x65`. **[I]** É o retrato do §6.1: o plano que sobra tem **cerca de
1/4** das linhas de peça do arquivo, e o jogador não tem como saber disso antes de mandar construir.

## 4. Estações por arquivo [M]

| conjunto de estações | arquivos |
|---|---|
| **só `workbench`** (ou nenhuma) | **101** |
| `workbench` + `stonecutter` | **34** |
| + `forge` | **42** |
| + `blackforge` | **59** |
| com `artisanstation` | **34 · 26 · 10** |
| com `magetable` | **26 · 11 · 3 · 1** |

Leitura da tabela: as linhas com `+` são **encadeamentos** (o arquivo também exige a estação das linhas acima), e as
duas últimas foram medidas como **listas** de três e de quatro contagens, **não** como um total. A partição exata
por conjunto não foi registrada (§7); o total por token se reconta do CSV (coluna `stations`, tokens separados por
`|`). A medição juntou **"só `workbench`"** e **"nenhuma estação"** na mesma linha (**101**) — as duas não foram
separadas (§7).

**[I]** A informação existe e **não é mostrada**: o relatório carrega `Stations` (`AI/BuildBlueprintReport.cs:62`) e
o `blueprints.inspect` a devolve (`DevBridge/Commands.cs:737`), mas o painel de obra usa **um único** campo do
relatório (`UI/CompanionBuildPanel.cs:1115`) — ver §6.2.

## 5. O que já subiu em jogo com esse corpus [M]

| arquivo | plano | resultado medido |
|---|---|---|
| `hs_meadows_stilt_hut` | **32** peças, **1** andar, **50** linhas | **32/32**, **0** puladas, **0** mortes, **0** `UNSUPPORTED`, **32** peças no mundo e **0** duplicatas (`build.pieces`), **100** linhas de log |
| `hs_blackforest_hut` | **28** peças | **28/28** em **24 s**, **0** puladas |
| `hs_meadows_house` | **60** peças | **60/60** em **120 s**, **0 puladas** (medido em 11/11/2026) |
| `hs_meadows_manor` | **96** peças | **92/96** em **152 s**, **5 puladas** (medido em 11/11/2026) |
| `hs_meadows_hobbit_repairhut` | **62** peças | **61/62** em **220 s**, **1 pulada** e **2 `UNSUPPORTED`** (medido em 11/11/2026) |

Os três ficaram **em andamento** quando esta nota foi escrita e fecharam depois; o que eles mediram do `DeckLift`
(terreno plano em 38,78, obra 1 m no ar e peça mais baixa em 39,78) está em `docs/re/building-notes.md` §17.23 — e a
conta do Meadow Manor **não fecha** (92 + 5 = 97 contra 96), registrada como saiu do log.

Do `hs_meadows_stilt_hut`: as **18** linhas do arquivo que **não** viraram peça (50 − 32) **não geraram recusa
nenhuma** — o filtro do `StartBlueprint` conta e segue (`MHR_Build.cs:1776-1807`). E "0 duplicatas" no
`build.pieces` é o teste que importa: o plano não coloca a mesma peça duas vezes.

**Ressalva de medida:** os **24 s** vêm do relógio em volta do comando, **não** do log — o `LogOutput.log` do jogo
não tem timestamp por linha (§7).

## 6. O que isso diz sobre o produto [I]

> Daqui para baixo é **leitura**, não medida. Nada disso foi implementado; o objetivo é apontar onde o corpus
> aperta uma decisão.

### 6.1 O risco real de "blueprint baixado" é receita, não mod faltando

Medido (§3.5): **100 %** das 387.024 linhas puladas são `recipe not unlocked`; **zero** por prefab inexistente. Quem
baixa um plano de **Ashlands** num mundo de **Meadows** perde a maior parte das peças — o `hs_ashlands_20999_*` do
§3.6 entrega **301** peças de **1.244** linhas de peça (num arquivo de **1.290** linhas).

O que o jogador vê hoje:

| onde | o que aparece | fonte |
|---|---|---|
| painel de obra, arquivo selecionado | `Selected: {0} - {1} pieces, {2} skipped, {3} lines` — **o número**, sem o motivo | `UI/CompanionBuildPanel.cs:1115`, `Core/ModLocalization.cs:930` |
| fala do fim da obra | `The {0} is up: {1} pieces, {2} skipped.` — **o número**, sem o motivo | `Jobs/MHR_Build.cs:1277`, `Core/ModLocalization.cs:619` |
| log (`LogOutput.log`) | uma linha: `[Build] blueprint <label>: N line(s) skipped, first: <prefab> (<razão>)` — **só o primeiro** | `Jobs/MHR_Build.cs:1849-1852` |

Ou seja: **pular em silêncio** é quase isso — o **total** aparece em dois lugares, o **motivo** só no arquivo de log,
e o **detalhamento por prefab** não aparece em lugar nenhum para o jogador. E esse detalhamento **já está pronto**:
`BuildBlueprintReport.SkippedReasons` (`AI/BuildBlueprintReport.cs:64`, com `prefab`/`reason`/`count`) é servido pelo
bridge como `skippedReasons` (`DevBridge/Commands.cs:715-724`).

O que o produto deveria dizer, na mesma voz do mod: *"faltam **N** peças: você não conhece a receita de **mármore
negro**"* — a mesma informação do `skippedReasons`, **antes** da ordem, no painel e na fala. É a diferença entre
"subiu 32 peças de 50 e eu não sei por quê" e uma decisão do jogador ("então vou construir uma de Meadows").

### 6.2 A estação não bloqueia a obra, mas é informação que o jogador deveria ver

**101** arquivos pedem só bancada (ou nenhuma); o resto pede talhador, forja, forja negra, artesão e mesa de magia
(§4). Como a **`[Building] BlueprintStations`** nasce **`true`** (`Core/ModConfig.cs:991`: *"A blueprint builds
without crafting stations: none is planned and a piece is not refused for a missing station."*), **isso não
bloqueia** a obra — o blueprint dispensa a estação, o template e a peça única continuam exigindo
(`AI/BuildStationCover.cs:35-42`).

O que fica de fora é **contexto**: o mesmo plano num mundo sem talhador **sobe** (com a chave no padrão) mas o
jogador não saberia que aquele plano foi desenhado para forja negra + mesa de magia. Hoje o painel **não** mostra
`Stations` — ele usa um único campo do relatório (`UI/CompanionBuildPanel.cs:1115`) e o relatório **tem** a estação
por token (`AI/BuildBlueprintReport.cs:62`, devolvida em `DevBridge/Commands.cs:737`). É o mesmo buraco do §6.1, com
outro dado: **informação pronta, tela ausente**.

### 6.3 Tamanho: 95 arquivos com ≥800 peças — a guarda e o tempo entram na conta

Medido (§3.4): **95** arquivos com **≥800** peças e o maior com **8.371**. A `[Building] BlueprintMaxPieces` tem
padrão **2000**, faixa **1..20000** (`Core/ModConfig.cs:992`) e recusa o **arquivo inteiro** acima disso
(`Jobs/MHR_Build.cs:1737-1743`, log `N pieces is over the limit (M)`).

- **A guarda existe e é grossa:** um arquivo de 8.371 peças é recusado inteiro. **[?]** quantos dos 354 passam de
  2000 **não foi contado** (§7) — é a primeira conta a fazer se o corpus for distribuído.
- **O tempo é o outro lado:** o run de referência do projeto é de **985 peças / ~14 min com 5 companheiros**
  (`docs/re/building-notes.md` §17). Os 95 arquivos da faixa **≥800** estão nessa ordem de grandeza ou acima dela;
  um plano de 8.371 peças é **8,5×** o run de referência. **Nenhum** arquivo dessa faixa foi construído (§7).

### 6.4 O corpus está na pasta **que o pacote leva** — e a nota de referências já tinha dito que não

Duas medidas que **não conversam**:

- `docs/references.md:15` registra o pacote como **"sem licença declarada ⚠️ uso local"** e, na mesma linha,
  **"não vai no pacote"** (o `hs_meadows_hut` do teste saiu dele).
- `tools/stage_release.py:306-313` copia **todo** `*.blueprint` da pasta do plugin para o `.zip` da release.

Ou seja: a decisão está escrita e o **staging não sabe dela**. Na próxima release, o pacote passaria a levar
**62,8 MB** de planos sem licença declarada, cujo uso autorizado é local. As **353 `.png`** **não** entram (o staging
só copia `*.blueprint` e lista os outros como "arquivo solto em blueprints\ (nao entra)", `:308-313`) — ficam só no
disco de quem baixou o pacote.

**[I]** Isso é a versão de empacotamento do mesmo problema do §6.1: informação existe, decisão existe, e a ponta
automática não a consulta. As saídas continuam sendo duas, e a escolha é do jogador: (a) o staging **pula**
`blueprints\` (o zip não leva plano de ninguém — coerente com `references.md:15`) ou (b) a linha do
`references.md` muda para "distribui com atribuição", com o crédito do OverDrive junto. O que **não** dá é levar
**62,8 MB** por omissão. Ver `docs/re/packaging-layout.md` para o gate de conferência do zip.

## 7. O que não foi medido

1. **Duração em segundos de cada obra.** O `LogOutput.log` **não tem timestamp por linha**; os **24 s** do
   `hs_blackforest_hut` vêm do relógio em volta do comando e não são comparáveis entre runs.
2. **Planos grandes na prática (≥800 peças).** **Nenhum** dos arquivos dessa faixa foi construído. Não há medida de
   tempo, de mortes, de `no progress` nem de watchdog para eles; o maior número medido no projeto continua sendo o
   `CommHomeBasicBP` (985 índices, 5 companheiros, ~14 min, `docs/re/building-notes.md` §17).
3. **Quantos arquivos passam de `BlueprintMaxPieces` (2000).** A faixa **≥800** tem **95** arquivos e o máximo é
   **8.371**, mas a contagem acima de 2000 não foi feita — e é ela que decide se a guarda derruba plano em massa.
4. **Qual arquivo tem 0 peças** (§3.4) e **quais são os 3 registros** que sobram na conta 366 × 354 (§3.2).
5. **A partição por conjunto de estações.** O §4 dá os encadeamentos e as listas medidos, não a tabela
   `conjunto → N` fechada; ela se reconta do CSV (coluna `stations`). Em particular, **"só `workbench`"** e
   **"nenhuma estação"** ficaram na **mesma** linha de 101 e não foram separados.
6. **A conta de material** de cada arquivo (`materials`/`materialsSkipped` no CSV) não foi analisada nesta nota — o
   corpus tem arquivos com **materiais que o mundo não produz** (Ashlands), o que agrava o §6.1.
7. **Multiplayer.** O filtro pergunta a receita ao **`Player.m_localPlayer`** (`Jobs/MHR_Build.cs:1782-1784`). Em
   servidor dedicado cada cliente pode conhecer receitas diferentes, e **não** foi medido qual jogador decide o que
   é pulado — nem o que acontece com dois jogadores de progressões diferentes na mesma obra.
8. **O comportamento do painel com 354 arquivos** (tempo de listagem, usabilidade do filtro
   `hc_ui_build_bp_filter`) não foi medido.
9. **Se os planos cabem no terreno.** Footprint, altura e nivelamento dos arquivos grandes não foram medidos; um
   plano de 8.371 peças tem outra ordem de grandeza de área que a casa de referência.
10. **Se o OverDrive autoriza redistribuição.** `docs/references.md:15` registra o pacote como **"sem licença
    declarada"**; o que o autor permite de fato não foi verificado (a página do Thunderstore não foi lida nesta
    nota) — é o que decide o §6.4.
11. **Os três runs que estavam em andamento** (`hs_meadows_house` 60, `hs_meadows_manor` 96,
    `hs_meadows_hobbit_repairhut` 62): **medidos depois** (11/11/2026) — **60/60** em **120 s**, **92/96** em **152 s**
    (**5 puladas**) e **61/62** em **220 s** (**1 pulada**, **2 `UNSUPPORTED`**). O que eles mediram está em
    `docs/re/building-notes.md` §17.23; a conta do Manor (**92 + 5 = 97** contra **96**) **não fecha** e não foi
    explicada. O que continua não medido é a **causa** das 5 e da 1, e os **2 `UNSUPPORTED`** antes da correção do
    `DeckLift`.

## 8. Receita de reprodução

```
# 1) contar o que veio (disco)
dir "BepInEx\plugins\AI-Companions\blueprints\*.blueprint"

# 2) ver o que o mod lista (não recursivo, com cache de 10 s)
#    DevBridge ligado: [Advanced] DevBridge = true no AiCompanions.cfg
#    blueprints.list  -> pasta, count e os nomes

# 3) a auditoria completa (não começa ordem nenhuma)
python tools/mcp-devbridge/audit_blueprint_corpus.py --text
#    CSV em %TEMP%\opencode\blueprint-audit.csv (uma linha por arquivo)

# 4) um arquivo só, com material e andares
#    blueprints.inspect  file=hs_meadows_stilt_hut
```

O passo 3 exige **o mundo carregado** (os prefabs são resolvidos contra o jogo, `DevBridge/Commands.cs:697-698`) e
o passo 4 também. Nenhum dos dois coloca peça: `blueprints.inspect` só lê o arquivo.
