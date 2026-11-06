# Backlog da varredura de 05/11/2026

> **Por que este arquivo existe:** os 26 achados da varredura de 8 frentes estão em `AGENTS.md`, pendência (26) —
> mas o `AGENTS.md` tem 1.700 linhas e ninguém o lê de manhã. Aqui é a **fila**, numerada, com custo e ordem.
> A fonte do detalhe (`arquivo:linha`, evidência) continua sendo o `AGENTS.md`; este arquivo é o "o que atacar".
>
> **Legenda de confiança:** ✔ = o arquiteto verificou no código/JOGO · relato = veio do especialista e **não** foi
> conferido (REGRA 15: conferir antes de agir).

## Fila

| # | achado | conf. | custo | estado |
|---|---|---|---|---|
| **1** | **Companheiro preso entre lajes** (`fits: false` com `64/64`). Três furos no "sai de cima da peça": `SelfStep < 2` não é terminal; o destino vinha do `WalkSpot` (que devolve o chão onde ele já está); o `Goto` descarta o destino quando a peça está a exatamente +1,00 m | ✔ medido + print | **duas tentativas, as duas revertidas** — ver AGENTS (27) | **ABERTO, precisa de desenho** |
| **2** | `LevelKey` (`:118`) e `PullFloorsAbove` (`:6058`) **excluem `foundation`** de "é laje" → telhado não acha a laje que cobre e sobe antes do piso (`no_support` + 20 s). É o *"ladder with no floor again"* que o jogador reportou | relato + ✔ leitura | 2 linhas num helper puro + asserção | aberto |
| **3** | **Criatura não é obstáculo** na sonda de corpo (`BuildProbe.cs:791/814` varre jogadores + companheiros) — javali em cima da peça passa e é cuspido sob o piso. O jogo usa `GetCharactersInRange(30 m)` (`Player.cs:4202`) | ✔ código | trocar por `Character.GetAllCharacters()`; 1 laço | aberto |
| **4** | **`StoreyOf` vivo na doc** (AGENTS + 6 linhas do `building-notes`) descrevendo mecanismo removido | ✔ | ~10 min | aberto (banner já posto no §15.2l) |
| **5** | **Nivelar pode destruir casa/horta vizinha**: o raio de 3 m da operação passa da área pedida e não há checagem de peças no entorno; peça que perde contato leva **100 % de dano** (`WearNTear.cs:568-575`) | relato | precisa checar peças no entorno | aberto — **medir antes** |
| **6** | **`Reattach` aloca `new List<int>` por frame** por companheiro ocioso enquanto existir **uma** pendência (`:3960`) | relato | ~3 linhas | aberto |
| **7** | **2 textos em inglês na tela**: `PadBindListener.cs:50` (`"Press a button..."`) e `ConfigPanel.cs:1196` (`"[A] Change [X] Clear …"`); não existe `hc_ui_press_button` | ✔ | 2 chaves × 4 JSONs + 4 linhas | aberto |
| **8** | **Máscara do apoio mais permissiva que a do jogo** (`piece_nonsolid`/`vehicle`/`Water` em `BuildProbe.cs:61` vs `WearNTear.cs:285`) | relato | alinhar → reduz `UNSUPPORTED` | aberto |
| **9** | **Telhado apoiado em mobília** passa: `HoldsPlacedPiece` aceita qualquer `WearNTear` para a classe 3, e o jogo pula `m_supports == false` (`WearNTear.cs:920-923`) | ✔ já registrado no §17.12 | corrigir o bypass | aberto |
| **10** | **`PulledOk >= 24` literal** = o mesmo 24 de `MaxFallbackPosts` (`:237`), sem constante | ✔ | 1 constante | aberto |
| **11** | **Poste de emergência não é verificado** depois de colocado (`:6501-6515`) | relato | 1 chamada a `BuildSupport.Supported` | aberto |
| **12** | **Repulsão sem asserção** (`NearestApart`/`PickNearApart`/`Apart`) e o comentário de `BuildSpread.cs:330-331` **promete** que tem | ✔ | ~15 linhas no harness | aberto |
| **13** | **`SetCreator` sem platform id** → HUD mostra "autor desconhecido" na casa do companheiro | relato | 1 linha | aberto |
| **14** | **0 de 15 citações** `MHR_Build.cs:NNNN` na doc ainda apontam para o texto citado | ✔ | citar por **nome de símbolo**, não por linha | aberto |
| **15** | **Harness 778 em 11 lugares** (real **807**); README do harness para na seção **(w)** | ✔ | mecânico | aberto |
| **16** | **`ServeBarrier`/`NextRetry` varrem a fila por frame por worker** (suspeito dos `389 µs/frame`) | relato | medir antes | aberto |
| **17** | **`BlueprintStations = true`** entrega casa que **não dá para demolir/reparar** sem estação (`Player.cs:2928-2936`) | relato | documentar na chave | aberto |
| **18** | **`stage_release` levaria ~62,8 MB** de `.blueprint` do corpus | relato | decisão do jogador | **decisão** |
| **19** | **`PluginVersion` 1.9.6 com tag publicada** e +120 commits | ✔ | decisão do jogador | **decisão** |
| **20** | Mortos: `MaxLevelPasses:240`, `State.Tried:361`, `Phase.Done:339` | relato | trivial | aberto |
| **21** | `Log()` monta a string antes do gate (`:9449`) | relato | reordenar | aberto |
| **22** | `docs/mcp-devbridge.md` diz **46** ferramentas (são **62**) | ✔ | mecânico | aberto |
| **23** | `CompanionLocale.cs:16-42` esquece o `it` no comentário | relato | trivial | aberto |
| **24** | `.gitignore` sem `*.log` / `Translations/English.json` / `speech.json` | relato | 3 linhas | aberto |
| **25** | Item 6 do gate valida a **DLL publicada** e passa se o deploy foi pulado; item 1 só olha erro, não aviso | relato | assert de mtime | aberto |
| **26** | `stage_release` allow-list de DLL sincronizada à mão com o csproj | relato | assert | aberto |

## O que **não** mexer (e por quê)

- **`Advance`** (340 linhas, 6–7 níveis) — todas as medições passam por ele; extrair antes de estabilizar convida regressão.
- **Tirar o `ignore` do colocador** — barraria **metade das peças de piso** (`wood_beam_1` no chão dos pés); custo nunca medido.
- **`PreferNearFloor`** (guarda de 1,5 m) — é o caminho que deu **575 → 981/986**.

## Ordem recomendada

**1 → 2 → 3 → 4** (o defeito ativo, depois os dois baratos que resolvem sintoma reportado, depois a doc),
e as **decisões (18/19)** antes de qualquer release.
