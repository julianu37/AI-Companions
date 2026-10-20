# Changelog

Todas as mudanças deste projeto, derivado do Offline Companions, do ProfMags. As versões anteriores à 1.4.0 são o mod original (veja o histórico do mod original, do ProfMags, para essas versões) mais o primeiro lote de correções de compatibilidade com a API do Valheim 1.0 que fizeram o mod carregar e rodar. **As entradas abaixo de 1.5.4 são o histórico do upstream e ficam como estão (em inglês) — as nossas seções (a partir da 1.6.0) são nossas.**

## 1.6.4 - 19/10/2026

**Padroes do `[IA]` corretos e apresentacao como projeto independente.** Para quem ja configurou, nada muda no jogo:

- **`[IA] BaseUrl` agora nasce `http://127.0.0.1:1234/v1`** (era `http://localhost:1234/v1`). No Windows o `localhost` resolve primeiro para IPv6 e o servidor do LM Studio so escuta IPv4 - cada ordem pagava cerca de 2,5 s de espera por causa disso. Quem ja tem o `.cfg` **nao** e afetado: o valor configurado continua valendo.
- **`[IA] Modelo` agora nasce `qwen2.5-3b-instruct`** (era `google/gemma-4-e4b`) - o modelo medido em ~0,35-0,4 s por ordem com a saida estruturada.
- O texto de ajuda do `BaseUrl` passou a explicar `127.0.0.1` vs `localhost`.
- A documentacao (README, LEIA-ME, wiki e notas de release) deixa de se apresentar como "fork" e passa a "projeto independente baseado no Offline Companions Reborn". A atribuicao MIT continua obrigatoria e mantida.

## 1.6.3 — 17/10/2026

**Reempacotado.** Nada muda no jogo:

- Mesmo conteúdo da 1.6.2, com a pasta do mod em `BepInEx/plugins/ModAI-BR-Companions/` e o
  `README.md`/`CHANGELOG.md`/`icon.png` na **raiz** do pacote.
- O `.zip` passa a se chamar `ModAI_BR_Companions-1.6.3.zip` (nome exato `Nome-Versão`, sem sufixo).

## 1.6.2 — 17/10/2026

**Ícone novo e pacote enxuto.** Nada muda no jogo:

- **Ícone novo** — a arte do mod (os três companheiros + o "AI COMPANIONS PT-BR"), em **256×256**.
  O banner completo está no repositório (`docs/images/banner.png`) e no topo do README.
- O pacote passa a ir **sem os modelos** (~20 MB, em vez de ~368 MB). Os **modelos (STT e TTS)
  continuam no release do GitHub**, e o LEIA-ME/README dizem exatamente onde colocá-los (a pasta `whisper\` e a
  `tts\kokoro\`).

## 1.6.1 — 17/10/2026

**Pacote enxuto.** Nada muda no jogo:

- O pacote **não leva mais o WAV de teste** (`whisper\jfk.wav`): vai só o que o mod precisa para
  funcionar. O comando de console `modia_stt_teste` continua funcionando e aceita um caminho de WAV como argumento.
- O `.zip` publicado passa a se chamar `ModAI_BR_Companions-1.6.1.zip` (nome exato `Nome-Versão`, sem sufixo).

## 1.6.0 — 16/10/2026

**A camada de IA: ordens por chat e por voz, o companheiro fala, personalidade/memória e um painel no jogo — tudo local, em PT-BR.** Este é o primeiro release do fork com a IA (Fases 3.1 a 3.8); o detalhe técnico de cada uma está nas seções abaixo.

**Destaques:**
- **Ordens por texto no chat:** `colete 10 madeiras`, `segue`, `para`, `minere ferro` — um **LLM local** (API compatível com a OpenAI) devolve a intenção e o companheiro executa a mesma ação do menu radial.
- **Ordens por voz:** segure **V**, fale e solte — **STT local** (Whisper.net, dentro do jogo, sem internet).
- **O companheiro fala:** **TTS local** (Kokoro-82M via sherpa-onnx), em PT-BR, com a voz saindo da posição dele.
- **Personalidade, memória e conversa livre:** `bom dia`, perguntas — ele responde como personagem e lembra da conversa.
- **Ordens com alvo:** `ataca o greydwarf`, `equipa o machado de bronze`, `larga a pedra` (e `joga a madeira fora`).
- **Recolher itens do chão:** `recolhe os itens` / `pega o machado do chão` — inclusive o que ele mesmo largou.
- **Painel da IA no jogo** (tecla **I**): personalidade, voz (com o botão **Ouvir**), push-to-talk, volume/velocidade, memória e conversa.
- **Tradução PT-BR auditada:** 386 chaves de UI e 186 falas, sem texto em inglês visível ao jogador.
- **Correções:** o **eco da voz** (era a zona de reverb global do jogo — a voz agora sai seca), a **ordem de pedra** que não parava no alvo, e o **painel F8** que engolia cliques (as camadas da UI do mod agora ficam acima do HUD do jogo).

#### Fases incluídas neste release

Tradução PT-BR: auditoria de cobertura (o que o jogador lê, em inglês, era bug) + Fase 3.1: ordens por texto no chat (IA local) + Fase 3.2/3.2c: o número da ordem é um ALVO TOTAL contado no inventário do companheiro + Fase 3.6: ordens com alvo (atacar/equipar/largar) + Fase 3.6b: a ordem sem alvo do modelo (drop/equip/attack caem para as palavras do jogador) + Fase 3.5: personalidade, memória e conversa livre + Fase 3.3: ordens por voz (push-to-talk + STT local) + Fase 3.3b: correções do teste da voz (tecla V, qualidade do STT, latência) + Fase 3.3c: raio de coleta maior e chatter aleatório desligado + Fase 3.3d: a ordem de pedra para no alvo e o texto da voz vai para o balão do jogador + Fase 3.8: o companheiro abre o próprio inventário por voz/chat + Fase 3.4: o companheiro FALA (TTS local, Kokoro-82M via sherpa-onnx) + Fase 3.7: painel da IA no jogo (personalidade, voz com Ouvir, push-to-talk) + Fase 3.4 (parte 3): o eco do TTS RESOLVIDO — era a zona de reverb global do jogo (a voz sai seca) + Fase 3.1: saída estruturada (JSON schema) — destrava modelos pequenos + Tradução PT-BR (fork ModAI-BR-Companions) + correção: o painel F8 não engole mais cliques (camada 31→33 + raycast dos decorativos) + correção (a de verdade): os painéis e a roda do mod vão para a faixa **1701+** — **a camada era a causa** do clique engolido no F8 (o HUD do jogo está em 400; o 31→33 era insuficiente) + correção: o diagnóstico de raycast vira opt-in (`[Advanced] UiDiag`, padrão desligado — eram ~7 KB por sessão) e para de avisar sobre os alvos de clique intencionais do painel da IA
- **O diagnóstico de raycast vira opt-in e para de avisar sobre alvos intencionais (branch `fix/uidiag`).** O audit
  `[UiDiag]` (o que achou o `HUD` do jogo em 400 engolindo os cliques do F8) **não** foi removido — ele é a resposta
  para "quem está na frente dos controles?" — mas uma sessão dele saiu com **6 linhas / 7.132 caracteres** e **três**
  dessas linhas eram **falsos positivos** do painel da IA (`HC_AiPanel/AiRoot`, `HC_AiPanel/AiRoot/KeyBox`,
  `HC_AiPanel/AiRoot/Overlay/PersonalityList`).
  - **Config nova: `[Advanced] UiDiag`** (bool, **desligado**). Ficou na `[Advanced]` e não na `[IA]` porque **não é da
    camada de IA**: ele audita a UI do mod inteira (o painel F8 **e** o da IA, e a faixa de camadas de
    `UI/ModUiLayers.cs`) — o `[IA] EcoDiag` está na `[IA]` porque diagnostica um componente da IA (a voz). A descrição
    diz o que ele loga, quando ligar (investigar clique/UI estranha) e que não muda nada no jogo.
  - **Desligado (padrão): zero linhas e zero custo.** Os dois chamadores (`ConfigPanel` e `CompanionAiPanel`) saem
    **antes** de entrar no `ModUiAudit`: nada de montar lista, nada de percorrer a cena. O flag de "uma vez por sessão"
    só é marcado quando o audit roda de verdade, então ligar a opção no meio da sessão vale na próxima abertura.
  - **Ligado, compacto:** duas linhas iniciais, uma vez por sessão (a de que o audit está ligado e a da allow-list — o
    que ele **nunca** reporta) e **uma linha de resumo por painel** com **contagens** (controles / de propósito /
    unexpected) — os **nomes** dos alvos decorativos, que eram o grosso do log, saíram. A **lista de camadas da cena
    continua em uma linha** (foi ela que nomeou o `HUD` em 400) e os **avisos** continuam **linha por linha** (caminho +
    tipo + tamanho + o ponto sondado).
  - **Os falsos positivos:** `AiRoot` (fundo do painel) e `KeyBox` (a caixa da tecla) viraram `raycastTarget = false` —
    são **decoração**, nunca podem ficar na frente de um controle (o fundo é pai de todos) e o **`Backdrop`** do painel
    (tela cheia, criado antes do root, alvo de clique **de propósito**) já é o catcher modal; era o mesmo que o
    `ConfigRoot` do F8 já fazia. As **barras de fundo das listas do dropdown** (`Overlay/PersonalityList` e
    `Overlay/VoiceList`) **continuam alvos de propósito** (os itens são 2 px menores que a lista e o clique nessa borda
    tem que morrer na lista, senão chega ao `ListCatcher` e **fecha** a lista) e entraram numa **allow-list documentada**
    do audit (`ModUiAudit.Intentional`, casada pelo fim do caminho + o motivo).
  - **Nada de lógica/layout/texto mudou** (nem LLM, TTS, STT, ordens, jobs, tradução ou camadas). Build: **0 erros,
    0 avisos** + DLL implantada. **O teste em jogo é do jogador.**
- **Tradução PT-BR — auditoria de cobertura (branch `fix/traducao`).** Auditoria de **todo texto que o jogador pode
  ver**: as chaves de UI (`ModLocalization` × `Translations/Portuguese_Brazilian.json`), as falas
  (`Speech/Portuguese_Brazilian.json`), os painéis, os hovers, as mensagens do `MessageHud` e os docs. **374 chaves de
  UI: 0 ausentes, 0 em inglês** (as 7 que batiam com o inglês eram legítimas) → **386 chaves: 0 ausentes, 0 em
  inglês** depois das 12 novas; **15 categorias de fala / 186 linhas: 0 ausentes** (só o "Skål!" é igual ao inglês, é
  um brinde). **Os textos em inglês que apareciam para o jogador foram para a localização** (chave nova em PT-BR **e**
  o par em inglês no `ModLocalization`):
  - **o nome do companheiro** — `CompanionPrefabs` escrevia `m_name = def.DisplayName` ("Companion", "Warrior",
    "Mage"...) no prefab, e era isso que o **HUD**, o **hover** e a **roda** mostravam num companheiro sem nome
    próprio. Agora `CompanionTierData.LocalName(def)` resolve as chaves novas `hc_tier_companion` /
    `_warrior` / `_mage` / `_fire_mage` / `_ice_mage` / `_support` (**Companheiro**, **Guerreiro**, **Mago**, **Mago
    de Fogo**, **Mago de Gelo**, **Suporte**), com o nome em inglês como reserva — o mesmo padrão que o `MHR_Shrine`
    já usava. Vale também para os botões de **Sub-Tipo**, o botão de **compra** ("Comprar Mago de Gelo (500)"), o
    nome do **baú** do companheiro e a mensagem "Seu novo mago chegou!";
  - **"X has been resurrected!"** do túmulo → `hc_msg_resurrected` (*"{0} foi ressuscitado!"*);
  - **"Hold"** do hover do companheiro (`[Hold E] Comandos`) → `hc_hover_hold` (*"Segure"*);
  - **os três cumprimentos do recall** ("I've been looking for you!" / "There you are!" / "Finally found you!"),
    que estavam em inglês **no código** do `ReturnLogic` e o companheiro **falava** → `hc_cmd_return_1..3`
    (*"Estive te procurando!"*, *"Aí está você!"*, *"Finalmente te encontrei!"*);
  - **"ON"/"OFF"** dos toggles do painel **F8** → as chaves do radial (`hc_radial_on`/`hc_radial_off`, *LIGADO* /
    *DESLIGADO*);
  - **"Push-to-talk"** (rótulo, rodapé e o aviso do conflito com o auto-pickup do jogo) → **"Aperte para falar"**;
  - os reservas **"Companion"** que ainda sobravam em caminhos visíveis (`CompanionHudPanel`, o campo de nome do
    painel, o nome do túmulo, o nome do companheiro na lista do santuário, o nome do efeito *Rested*, a mensagem de
    espera do sono) → `hc_msg_name_default` (*"Companheiro"*);
  - **`hc_radial_despawn_confirm`** era uma chave **órfã**: o `CompanionRadialMenu` ainda a referencia (num método
    morto desde que a confirmação virou `YesNoPopup`) e ela não existia em nenhum dos dois lados — voltou com
    *"Confirmar?"*, para o token cru nunca aparecer se aquele caminho voltar à vida.
  - **Fora do escopo (não é nosso):** os nomes de **item/criatura/estilo de cabelo e barba** vêm dos assets do jogo
    (localizados pelo próprio Valheim), os **tokens `$...`** do jogo, as **chaves de configuração** (nomes internos
    do `.cfg`) e as ~1500 linhas de **log/diagnóstico** (`[IA]`, `[TTS]`, `[STT]`, `[UiDiag]`, `[Collect]`, `[ECO]`,
    `[Move]`, `[Direct]`, ...) que o jogador só vê se abrir o `LogOutput.log`. O **`CHANGELOG`** mantém o histórico do
    upstream (abaixo de 1.5.4) em inglês de propósito — é o registro dele.
- **Os painéis e a roda do mod agora ficam ACIMA do HUD do jogo — o clique engolido no painel F8 e o texto do centro da
  roda, resolvidos pela camada (branch `fix/camadas-ui`).** O jogador reabriu o bug depois da `fix/clique-f8` (*"está a
  mesma merda"*), e estava certo: aquela correção acertou os **decorativos** (problema real, continua consertado) mas
  **não** a causa principal. O diagnóstico `[UiDiag]` que ela mesma deixou no painel achou o culpado:
  ```
  [UiDiag] F8 raycast audit: canvas "HUD" (order=400, overrideSorting) has a raycast target over the controls of the panel:
           .../IngameGui/HUD/hudroot/crosshair/HoverName (TextMeshProUGUI 350x150) at the panel point 196,-46
  [UiDiag] F8 raycast audit: panel order=33, ... EnemyHud=200, HudMessage=1000(override), HUD=400(override),
           Menu=1700(override), UnifiedPopup=1900(override), ...
  ```
  - **A causa:** o HUD do jogo é um canvas em **`sortingOrder = 400`** (com `overrideSorting`) e **todos** os canvases
    do mod estavam em **28..34** — atrás dele. O alvo que engolia o clique é o **`crosshair/HoverName`** (350x150 no
    **centro** da tela, onde o painel está), e ele **recebe clique mesmo invisível** (o jogo o apaga com
    `canvasRenderer.SetAlpha(0f)` e **não** desliga o `raycastTarget`) — daí a **faixa** e daí rolar o painel resolver.
    **Por que 31 → 33 não bastou:** 33 continua **abaixo de 400** (e em empate de `sortingOrder` o uGUI desempata pela
    ordem de registro dos raycasters, que o canvas do jogo ganha).
  - **A correção:** todos os canvases do mod foram para a faixa **1701..1708**, com os números numa **fonte única**
    (`src/Companions/UI/ModUiLayers.cs`): `FarmZonePlacer` 28→**1701**, `HomeZonePanel` 29→**1702**,
    `CompanionRadialMenu` 30→**1703**, `StarterCompanionPanel` 30→**1704**, `FarmZoneCropPicker` 31→**1705**,
    `ConfigPanel` 33→**1706**, `CompanionAiPanel` 33→**1707** e o overlay do dropdown 34→**1708**. A ordem relativa
    entre os nossos é a de antes.
  - **Por que 1701 e não 2000:** 1701 já está acima do HUD (400), do `HudMessage` (1000) e do `Menu` (1700), e para
    **abaixo** do `UnifiedPopup` (1900) — o popup modal **do jogo** (classe vanilla de `assembly_valheim`, não é nossa e
    não pode ser movida): um modal tem que ficar na frente de uma janela. E 2000 seria **empate** com o
    `LongPressRadial` do jogo, empate que o jogo ganha. Continua muito abaixo do console (5000) e do `SwitchCursor`
    (12000). Os painéis do mod seguem bloqueando inventário/loja/menu (`Hud.InRadial`), então esses nunca ficam atrás
    de nós.
  - **O segundo sintoma, o mesmo remédio:** o radial desenha `_centerName`/`_centerAction`/`_centerState` no **centro**
    da tela, onde vive o `crosshair/HoverName`; com o radial em 30 e o HUD em 400 o jogo desenhava por cima e os textos
    se misturavam. Com o radial em **1703**, não mais.
  - **Audit mantido e generalizado:** a lógica saiu do `ConfigPanel` para `src/Companions/UI/ModUiAudit.cs`; agora
    `[UiDiag] F8 raycast audit` **e** `[UiDiag] AI panel raycast audit` rodam **uma vez por sessão** (nunca por frame) e
    reportam os alvos de clique não-control dentro do painel, **todas** as camadas ativas da cena e quem, acima do
    painel, cobre a coluna de controles. Os canvases do próprio mod são listados mas não sondados.
  - **Não mudou nada de lógica, layout, texto, LLM, TTS, ordens, jobs ou STT** — só o `sortingOrder` dos oito canvases
    e o audit. Build: **0 erros, 0 avisos** + DLL implantada. **O teste em jogo é do jogador.**
- **O painel F8 não engole mais cliques (branch `fix/clique-f8`).** O relato do jogador ("há algo invisível na frente
  dos controles numa **faixa** — clicar no ON/OFF de uma linha não fazia nada; depois de rolar o painel o mesmo botão
  funcionava") tinha **duas** coisas somadas, e as duas valiam para o mesmo sintoma:
  - **A camada.** O canvas do `ConfigPanel` estava em **31** — a **mesma** camada do UI do jogo. Num empate de
    `sortingOrder` o uGUI desempata pela **ordem de registro dos raycasters** (`RaycasterManager`), e o canvas do jogo
    (registrado muito antes) ganha: o `GraphicRaycaster` dele entrega o clique a um alvo **invisível** seu (texto de
    `MessageHud`/HUD apagado com `canvasRenderer.SetAlpha(0f)`, que **continua** recebendo clique) numa **faixa fixa no
    centro da tela**. O painel é centralizado, então rolar o painel tira o controle da faixa — exatamente o que o
    jogador viu. O **painel da IA** (tecla `I`) nunca teve o problema porque sempre esteve em **33**. O `ConfigPanel`
    foi para **33** (a mesma camada; os dois painéis recusam abrir juntos).
  - **Os decorativos.** Todo `Graphic` decorativo do painel era `raycastTarget = true` (padrão do uGUI): o fundo do
    painel, as faixas das linhas, o viewport do `ScrollRect` e — os piores, por serem os **últimos irmãos do viewport**,
    ou seja os gráficos **mais acima de todo o painel** — a barra de scroll **invisível** e o **handle** dela (10 px na
    borda direita). Agora **só** os controles (abas, ON/OFF, Padrão, slider fundo+handle, *key bind*, X) e as duas
    **áreas de scroll** (o viewport do conteúdo e a coluna de abas — os GameObjects onde vivem os `ScrollRect`s, que
    precisam do alvo para a roda do mouse e o arrasto) continuam com `raycastTarget = true`; a barra invisível e o
    handle viraram `false` **e** passaram a ser os **primeiros** irmãos do viewport (o `ScrollRect` os guarda por
    referência, então nada muda para a roda/arrasto). O que foi **descartado com prova** está no `docs/roadmap.md`
    (*Pendências conhecidas*): no uGUI um gráfico **ancestral** nunca fica na frente dos próprios filhos (o fundo da
    coluna de abas é um alvo de clique e os botões de aba sempre responderam).
  - **Diagnóstico (uma vez por sessão, na primeira abertura).** `[UiDiag] F8 raycast audit: panel order=33, N control
    raycast target(s), M on purpose (...), 0 unexpected; K other active canvas(es): <nome=sortingOrder, ...>; 0 raycast
    target(s) of another canvas over the controls.` — e, se houver, um **aviso por alvo de outra camada** que cubra a
    coluna de controles do painel (11 pontos amostrados). Se a faixa voltar, o log diz **quem** é.
  - Build: **0 erros, 0 avisos** + DLL implantada. **O teste em jogo é do jogador.**
- **A ordem "recolhe os itens do chão" finalmente recolhe (Fase 3.1 — `MHR_Collect`: o que faltava).** O jogador largou o
  machado (`jogue seu machado no chão` → `Order "drop": Companion dropped 1 x "Machado Nordico"`) e depois pediu para pegar
  de volta (`pegue o machado nordico do chão`, `junte o machado`, `recolhe os itens`): o modelo acertava a ordem
  (`Order "pickup" -> 1 companion(s)`, `Ok: recolher itens do chao`) e **nada acontecia** — o machado ficava no chão.
  - **O achado (três coisas que se somam):** (1) a ação `pickup` **só setava um flag** no ZDO do companheiro e **nenhuma**
    ação do mod andava até um item no chão para pegá-lo (o único "pega item" era o ímã de 2 m do `CompanionAI.AutoPickup`);
    (2) o que o companheiro larga é marcado `m_autoPickup = false` de propósito (é o que o próprio jogo faz em
    `ItemDrop.OnPlayerDrop()`), e o ímã **e** o `MHR_Loot` pulam esses itens de propósito — justamente o que o jogador
    pediu para pegar; (3) o flag só era lido pelo `MHR_Loot` e pelo rótulo da roda, então o botão **Recolher** não
    desligava o ímã.
  - **Novo `src/Companions/Jobs/MHR_Collect.cs`** (mesmo padrão do `MHR_Loot`: estado por companheiro, `Start` pela ordem e
    `Drive` por frame pelo `CompanionAI.UpdateAI`, antes do `MHR_Loot.Drive`): acha o item solto mais próximo dentro de
    `[Looting] CollectRadius` (**novo, padrão 10**, faixa 1..30), **anda até ele** com o `MHR_Mover` e pega com
    `Humanoid.Pickup` — que **não olha `m_autoPickup`** (conferido no jogo decompilado), então **inclusive o que foi largado
    de propósito** volta. Repete com o próximo até não sobrar nada no raio, a mochila encher ou a ordem passar de **30 s**.
  - **Recusa:** item de missão (a regra do `drop`), peça (`IsPiece()`), posição na *blacklist* do `CompanionAI`, água funda
    (nunca nadar por loot) e o que não couber **inteiro** na mochila. **Para** com outra ordem (chat **e** roda), troca de
    trabalho, `Recolher` desligado, mochila cheia, morte, posse perdida ou a coleira (`[Looting] MaxDistance`); e os
    **jobs esperam** enquanto ele anda (`MHR_JobRules.OtherOrder`).
  - **A ordem faz as duas coisas:** `pickup` liga o flag (como antes) **e** inicia o recolhimento; `nopickup` continua só
    desligando o flag. O ímã e o `MHR_Loot` **não mudaram** (a proteção do "larga pra dar" fica intacta).
  - **O botão Recolher da roda passa a valer:** o ímã agora é condicionado ao flag (lido **uma vez** por frame), com o
    padrão `true` — um companheiro que nunca mexeu no botão fica exatamente como hoje. Os jobs que pegam coisas
    (`MHR_PileTaker`, `UpkeepPiles`, `UpkeepHoney`, `MHR_SpearThrow`, `HarvestController`) chamam `Humanoid.Pickup` direto
    e não dependem do ímã.
  - **Fala nova** (quando não há nada no chão e quando a mochila está cheia): `hc_ai_collect_nothing` /
    `hc_ai_collect_full`, em PT-BR e no dicionário em inglês. Logs: `[Collect] <Nome>: Going to pick up "X" (N m away)`,
    `Done: N item(s) picked up`, `nothing on the ground within N m`.
  - Build: **0 erros, 0 avisos**. O teste em jogo é do jogador.
- **"Jogue fora" vira `drop` mesmo quando o modelo diz `nopickup` (Fase 3.1 — rede de segurança).** O mesmo teste em jogo
  mostrou que o contrato sozinho não bastava: quando o modelo **insiste** em `nopickup` para uma ordem de largar (ou de
  recolher), as **palavras do jogador** decidem — o mesmo raciocínio do "o recurso manda".
  - **`PickupIntent.NeighborAction`** (novo, `AI/PickupIntent.cs`, **puro**) é chamado **uma vez por mensagem** pelo
    `CompanionOrders.Run`, no mesmo lugar da correção do `resource`: com a ação **exatamente `nopickup`** e um verbo claro
    de largar (`joga fora`, `jogue a madeira fora`, `larga`, `largar`, `descarta`, `soltar`, `deixa no chão`...) a ordem
    vira **`drop`**; com um verbo claro de recolher (`junta`, `junte`, `apanha`, `recolhe do chão`, `pega do chão`...) vira
    **`pickup`**. O log mostra a troca em uma linha.
  - **Guarda-corpos:** o `nopickup` **legítimo** é preservado (`para`/`pare`/`parar` ou `não pegue`/`não recolha` desligam
    a correção), só a ação `nopickup` é olhada e, sem gatilho, o comportamento é idêntico ao de hoje.
  - **O alvo do `drop` continua vindo do caminho da Fase 3.6b** (as palavras do jogador lidas pelo `OrderTargets.Phrases`
    e validadas contra a mochila) — nada de extração de nome nova, e o item de missão continua nunca caindo.
  - Verificado fora do jogo (harness com os arquivos reais): **58 casos, ALL CHECKS PASSED**.
- **O contrato separa largar, recolher e parar de recolher (Fase 3.1 — teste em jogo com o 3B).** O
  `qwen2.5-3b-instruct` confunde **ações vizinhas**: `jogue fora sua madeira` (e as variantes `jogue sua madeira fora` e
  `jugue a madeira fora`, com erro de digitação) voltava como **`nopickup`** — o companheiro **desligava a coleta
  automática** em vez de largar a madeira — e `junte a madeira do chão` ficava **50/50** entre `pickup` e `nopickup`.
  - **Uma regra curta no `Contract`** separa as três: `drop` = largar/jogar fora o que o companheiro tem, `pickup` =
    recolher itens do chão, e `nopickup` **só** quando o jogador pedir para PARAR de recolher ("pare de recolher", "não
    pegue nada").
  - **3 exemplos novos**, logo depois dos equivalentes: `"jogue fora a madeira"` → `drop`, `"junte a madeira do chão"` →
    `pickup` (os dois não tinham exemplo nenhum) e **`"equipe a picareta"`** → `equip` (o contrato só tinha `"pega a
    picareta"`, e foi a forma com `equipe` que falhou).
  - **Só o `Contract` mudou** (`ValidActions`/schema, `LlmIntent`, `LlmClient`, personalidade/memória, TTS, painel, STT e
    ordens intactos) e o prompt cresceu **6 linhas** (~+120 tokens). Se o 3B deixa de errar com o prompt novo é o teste em
    jogo do jogador.
- **O prompt agora ensina que `action` e `resource` têm que combinar (Fase 3.1 — a outra metade do achado do teste).**
  Com o `qwen2.5-3b-instruct`, `colete 5 pedras` voltou como `{"action":"wood","resource":"wood"}`: o modelo copiava o
  `action:"wood"` do exemplo `"colete 20 de madeira"` do contrato (o verbo **`colete`** era o gatilho) e ignorava a
  pedra, mesmo com a regra do `resource` já existindo — *few-shot overfitting*, o jeito típico de um 3B errar.
  - **O `Contract` ganhou uma regra curta** (`pedra` → `action:"stone"` + `resource:"stone"`; `madeira` → `wood`/`wood`;
    `minério` → `ore`/`ore`; "NUNCA use action \"wood\" para pedra") **e 2 exemplos com pedra e o verbo `colete`**,
    logo **depois** do exemplo de madeira que causava a cópia.
  - **O resto do contrato ficou intacto** (as 29 ações, o `target` obrigatório em `attack`/`equip`/`drop`, os outros
    exemplos) e o prompt cresceu **3 linhas** (~+60 tokens sobre os ~1766 medidos), porque ele está calibrado para o
    `gemma-4-e4b` e para o 3B.
  - **Nada mais mudou** (schema/`ValidActions`, `LlmIntent`, `LlmClient`, personalidade/memória, TTS, painel, STT,
    ordens): a correção acima continua sendo a rede de segurança para quando o modelo insistir em trocar os campos.
    Se o 3B **deixa** de errar com o prompt novo é o teste em jogo do jogador.
- **O recurso manda: `colete 10 pedras` não vira mais coleta de madeira (Fase 3.1 — correção do teste em jogo).** No
  teste com o **`qwen2.5-3b-instruct`** o companheiro foi **coletar madeira** quando o jogador pediu `colete 10 pedras`.
  A causa tinha duas partes: **nossa** — com `action` e `resource` inconsistentes (`{"action":"wood","resource":"stone"}`)
  o mod obedecia ao `action` **genérico** e **ignorava o `resource`**, que é o campo específico (era ele que aparecia
  no log do teto, "stops at 5 **Stone**", enquanto o trabalho aplicado era o de madeira); e **do modelo pequeno**, que
  em `colete N <coisa>` copia o `action:"wood"` do exemplo do contrato. Esta parte corrige a nossa:
  - **O `resource` ganha quando os dois discordam sobre qual trabalho é** (`OrderItems.GatherAction`, novo, chamado
    uma vez por mensagem pelo `CompanionOrders.Run`): o log mostra a correção em **uma linha**
    (`[IA] O modelo devolveu action="wood" com resource="stone" - usei "stone" (o recurso manda).`).
  - **Só nas ordens de coleta** (`wood`/`stone`/`ore`/`forage`), e só quando o recurso é item de **outro** trabalho:
    `attack`, `equip`, `drop`, `inventory` e as demais não são tocadas, e um `resource` nulo/desconhecido mantém o
    comportamento de sempre.
  - **Sem tabela nova:** o recurso passa pelo mesmo mapa de apelidos que o `OrderItems.Resolve` já usa e o item é
    procurado nas **entradas nomeadas das quatro listas que os trabalhos leem** (`GatherWood`/`GatherStone`/`GatherOre`
    /`ForageItems`), por prefab ou pelo nome do jogo. Par já coerente (o recurso é da lista do próprio trabalho) não é
    mexido; lista `*` não decide nada.
  - **O teto da ordem, a contagem na mochila, o `Resolve`, o alvo do radial e o fallback das palavras do jogador ficam
    como estavam.** Verificado fora do jogo (harness com os arquivos reais): **60 casos, ALL CHECKS PASSED**.
- **A resposta do modelo agora é garantidamente um JSON (Fase 3.1 — saída estruturada).** O pedido ao modelo passou a
  levar `response_format` com um **JSON schema** (`companion_intent`) que fixa as cinco chaves do contrato e a **lista
  fechada das 29 ações** — é isso que impede a resposta em texto solto, que o prompt sozinho não conseguia evitar. O
  ganho prático é poder usar um **modelo pequeno e rápido**: medido com o prompt atual (~1766 tokens), o
  `qwen2.5-3b-instruct` acertava **0 de 6** respostas em JSON (repetia frases do prompt) e passou a acertar **6 de 6**,
  inclusive o alvo em `atacar`/`equipar`/`largar`; o `google/gemma-4-e4b` (o padrão) continua igual.
  - **`[IA] SaidaEstruturada`** (novo, **ligado** por padrão) é a opção: aparece sozinha no painel F8, aba IA, e
    desligá-la volta exatamente ao pedido de antes.
  - **Nenhum servidor fica de fora:** se o servidor recusar o schema (**HTTP 400**, ou erro citando
    `response_format`/`json_schema`), o mod tenta **uma vez** com `{"type":"json_object"}` e, se falhar de novo,
    **uma vez sem `response_format`** (o comportamento antigo), avisando **uma vez no log**. Erro de rede ou timeout
    **não** são repetidos: o mesmo pedido nunca é enviado duas vezes e o mod nunca quebra por causa disso.
  - **Nada mais mudou:** o prompt, o parser, a personalidade/memória, o TTS, o painel, o STT e as ordens ficaram
    iguais.
- **O eco da voz do companheiro está CORRIGIDO (Fase 3.4, parte 3).** O diagnóstico (`EcoDiag = true`) mostrou a
  causa na primeira tentativa: o Valheim tem **uma zona de reverb global**, no **próprio objeto do ouvinte**, com
  raio de **1000 m**, **4 s** de cauda e `room` de **−10 dB** — ou seja, **tudo** dentro de 1 km levava essa cauda de
  4 s, e a voz do companheiro era a única coisa que deixava isso evidente (a música e os sons do jogo são curtos ou
  repetitivos). As fontes do próprio jogo **fogem** dela (a música usa `reverbZoneMix = 0`, o ambiente 0,19), mas a
  nossa fonte estava no **padrão do Unity (`1`)** e pegava o eco inteiro. **Correção:** a voz agora sai **seca**
  (`reverbZoneMix = 0`) — ouvido em jogo e aprovado ("limpou").
  - **`[IA] TtsReverbZoneMix`** (novo, faixa 0–1, **padrão 0**) é a opção de verdade: `0` = voz seca; suba um pouco
    (ex.: `0,19`, o valor que o ambiente do jogo usa) se quiser um "espaço" sutil na voz. Vale **na próxima frase**,
    sem reiniciar.
  - **Os 4 interruptores de teste do eco saíram** (`TtsBypassReverbZones`, `TtsBypassListenerEffects`,
    `TtsBypassEffects` e `TtsDoppler`): cumpriram o papel. Eles desaparecem sozinhos do seu `.cfg` — as suas outras
    opções não são afetadas.
  - **`[IA] EcoDiag`** continua existindo (padrão desligado), agora como **diagnóstico de áudio**: uma vez por sessão,
    na primeira frase falada, escreve no log o ouvinte da cena, as zonas de reverb do mundo e como as fontes do
    próprio jogo estão configuradas. **Não muda o som** — serve para investigar qualquer problema de som no futuro.
  - **Pendência conhecida** (para depois): a voz **não** passa por um grupo de mixer do jogo (a música e o ambiente
    passam). Não é necessário para o eco, que já está resolvido.
- **O caminho até a correção (Fase 3.4, parte 3) — interruptores de teste e diagnóstico.**
  A voz do companheiro sai com **eco** ("como falar num lugar grande"), e o que já está estabelecido descarta as
  hipóteses fáceis: a síntese está **limpa** (o WAV de fora do jogo é idêntico), **só a voz do companheiro** ecoa
  (música e SFX do jogo são normais), o eco é **idêntico em qualquer lugar, de perto ou de longe e com as 3 vozes
  (42/43/44)** e acontece com **uma única frase** (não é sobreposição da fila). É um **processamento fixo da engine**
  na nossa `AudioSource`. Quem diz **qual** é o ouvido do jogador, então esta parte começou como **instrumentação**
  (interruptores, um por vez, e o diagnóstico acima). Resultado: **o diagnóstico respondeu na primeira tentativa** e
  a correção virou uma linha (`reverbZoneMix = 0`). O registro do que foi feito na exploração:
  - **6 chaves novas em `[IA]`** naquele momento, todas com o valor padrão = como era antes: `EcoDiag` (false —
    diagnóstico só no log), `TtsReverbZoneMix` (1.0, faixa 0–1), `TtsBypassReverbZones` (false),
    `TtsBypassListenerEffects` (false, o suspeito principal), `TtsBypassEffects` (false) e `TtsDoppler` (true).
    Aparecem **no painel F8, aba IA** (o `Bind` já alimenta `ModConfig.Categories`, nenhuma mudança no painel foi
    precisa) e valem **na próxima frase**, sem reiniciar. **No fim sobraram 2**: `TtsReverbZoneMix` (agora 0) e
    `EcoDiag` — os outros 4 foram removidos.
  - **Aplicação** (`StartChunk` → `ApplyReverbZoneMix`): a `AudioSource` é criada uma vez e fica na sessão, então o
    `reverbZoneMix` é escrito **só quando o valor muda** — trocar no painel F8 vale na próxima frase, sem reiniciar,
    e a primeira frase já aplica o padrão. Sai uma linha no log quando escreve
    (`[TTS] ReverbZoneMix da voz: reverbZoneMix=0`).
  - **Diagnóstico** (`EcoDiag = true`), **uma vez por sessão** na primeira frase, prefixo `[ECO]`: o `AudioListener`
    (nome + `AudioReverbFilter`/`AudioLowPassFilter` e seus valores), **todas as `AudioReverbZone`** da cena
    (`reverbPreset`/`minDistance`/`maxDistance`/`decayTime`/`room` + se o jogador e o companheiro estão **dentro** do
    raio) e **a receita do jogo**: amostra de até 20 `AudioSource` da cena (a nossa fora) com `spatialBlend`,
    `reverbZoneMix`, os `bypass*`, `dopplerLevel`, `spatialize` e o **nome do mixer group**. É caro de propósito:
    roda uma vez por sessão, nunca por frame, e nunca com a flag desligada.
  - **Reflexão:** o núcleo obrigatório (os 11 membros) continua igual; `reverbZoneMix` é o único membro **opcional**
    que a voz escreve; `bypassReverbZones`, `bypassEffects`, `bypassListenerEffects`, `dopplerLevel`, `spread`,
    `spatialize` e `outputAudioMixerGroup` são **opcionais e só de leitura** — quem usa é o **diagnóstico**, para
    fotografar as fontes do jogo. Um membro que não exista não derruba a voz (com aviso no log). Nomes conferidos no
    `UnityEngine.AudioModule.dll` do jogo.
  - **Não mudou:** `spatialBlend`, o gate de fim de fala, `StopSource`, cache, fila, síntese, prompt, painel, STT,
    ordens e jobs. Branch: **`fix/eco-voz`**.
- **Correções do teste em jogo do painel da IA (Fase 3.7) — as teclas do jogo digitando no campo, a caixa de texto e o
  rodapé.** Três relatos do teste em jogo, três causas raiz:
  - **digitar `m` na caixa de texto abria o mapa** (e o mesmo valia para `Tab`, `F5`, `Enter`, `C`, `R`, `G`, `X`, `B`,
    `Z`, `Q`, `F`, `E`, `V` e os dígitos do hotbar). Os guardas de input que o mod já tinha (`Player.TakeInput`,
    `PlayerController.TakeInput`, `Hud.InRadial`, `VoiceInput.BlockedByUi`) só barram o que **passa por eles** — e o
    mapa é alternado em **`Minimap.Update`**, que lê `ZInput.GetButtonDown("Map")` e **nunca** consulta
    `Player.TakeInput()` (o mesmo vale para o inventário, o console e o chat). Além disso, um `TMP_InputField` só come
    as teclas que ele mesmo trata. **Correção:** novo `Patches/TextInputKeyGuard.cs` — `ModTextInputFocus` (registro dos
    campos de texto do mod + resposta "algum em foco?" **cacheada por frame**) e **prefixos Harmony** em
    `ZInput.GetButtonDown/GetButton/GetButtonUp/GetKeyDown/GetKey/GetKeyUp` que devolvem `false` enquanto um campo do
    mod está em foco (mesmo espírito do `AutoPickupKeyConflict`: sem campo em foco o caminho é **uma comparação de
    `int`** e o jogo roda exatamente igual). Ficam de fora os botões/teclas de **gamepad** (`"Joy*"`, `JoystickButton0`
    para cima) e os métodos de **mouse** — o painel lê `ZInput.GetButtonDown("JoyBack")`/`"JoyButtonB"` para fechar e o
    clique na UI passa pelo `EventSystem`. Digitar continua digitando o caractere (o `TMP_InputField` lê suas teclas
    pelo `Input`/`Event` legado, nunca pelo `ZInput`) e o push-to-talk segue bloqueado com o painel aberto pelo
    `VoiceInput.BlockedByUi` (a tecla `V` só escreve `v` no campo).
  - **a caixa de texto "meio bugada" (sem cursor e sem seleção).** `TMP_InputField.OnEnable` é onde o TMP cria o objeto
    `"Caret"` cujo `CanvasRenderer` (`m_CachedInputRenderer`) desenha **o cursor e a seleção** — e ele só faz isso se o
    `textComponent` **já estiver atribuído**. O painel montava o campo com `AddComponent<TMP_InputField>()` num
    GameObject **ativo** e atribuía o `textComponent` **depois**: o cursor nunca foi criado e `UpdateGeometry` passou a
    sair cedo para sempre. **Correção:** o GameObject nasce **inativo** e só é ativado **depois** de tudo atribuído (o
    `"Caret"` é movido para último filho, para não desenhar atrás do texto); o texto passou a usar `Overflow` +
    `PreserveWhitespaceNoWrap` (o `RectMask2D` do viewport recorta e o TMP rola o texto na horizontal — antes o
    `Ellipsis` truncava com "..." e o texto parecia pular), cursor claro de 2 px, `selectionColor`, `richText = false`,
    o clique posicionando o cursor e **Enter aplicando** como o botão *Aplicar*. O campo de **nome do companheiro**
    (`CompanionInteractPanel.BuildNameInput`) tinha o mesmo defeito e ganhou a mesma correção.
  - **o rodapé do painel estava ilegível.** Eram duas linhas de 11 px em cinza escuro **direto sobre a madeira**, com a
    segunda praticamente na borda inferior do painel. **Correção:** faixa escura própria (o mesmo truque das linhas de
    configuração), resumo em **duas linhas** localizadas (`hc_ai_panel_status_personality` + `hc_ai_panel_status_voice`,
    12,5 px quase branco) e dica em 12 px, e o painel cresceu **470 -> 516** para nada ficar cortado. A faixa é criada
    uma vez só e o *refresh* continua 2x/s.
  Nada de TTS/STT/ordens/jobs/comportamento do companheiro mudou. Arquivos: `Patches/TextInputKeyGuard.cs` (novo),
  `UI/CompanionAiPanel.cs`, `UI/CompanionInteractPanel.cs`, `Core/ModLocalization.cs`,
  `Translations/Portuguese_Brazilian.json`.
- **Correção — a lista do dropdown do painel da IA aparecia ATRÁS dos controles (Fase 3.7).** Ao abrir o dropdown de
  **Personalidade**, a lista saía desenhada **por trás** do campo do dropdown de **Voz**, do campo *Nas suas palavras*
  e do botão **Aplicar** (relato com screenshot). No uGUI a ordem de desenho é a **ordem dos irmãos** na hierarquia e
  os **campos** dos dropdowns eram criados **depois** do *overlay* das listas, então passavam a ser os últimos filhos
  do painel e desenhavam por cima dele. Agora:
  - o *overlay* volta a ser o **último filho** do painel depois de os campos existirem (`_overlay.SetAsLastSibling()`
    no fim do `Build()`, com o porquê comentado);
  - o *overlay* ganhou um **`Canvas` próprio** (`overrideSorting = true`, `sortingOrder` = **34**, um acima do painel,
    que é 33) com **`GraphicRaycaster`** — é o que o **próprio template de `Dropdown` do Unity** faz
    (`popupCanvas.overrideSorting = true; popupCanvas.sortingOrder = kHighSortingLayer`): a lista passa a desenhar
    (e a receber o clique) acima do painel sem depender de onde o *overlay* está na hierarquia;
  - **clicar fora da lista fecha a lista:** um *catcher* invisível (`Image` com alfa 0 + `Button` com
    `transition = None` — o mesmo *blocker* do `Dropdown` do Unity) cobre o painel inteiro como **primeiro filho do
    overlay**: fica atrás das listas, então o clique num item vai para o item, e só é **ativado enquanto há lista
    aberta** (fora disso roubaria o clique do painel).
  Nada de TTS/STT/ordens/jobs/patches mudou; sem alocação por frame, sem `Find*`/LINQ. Arquivo único:
  `src/Companions/UI/CompanionAiPanel.cs`.
- **Correções da auditoria (Fases 3.4 e 3.7) — o TTS e o painel da IA à prova de borda.** Nada do que já
  funcionava mudou de comportamento; o que mudou foi o que dava crash, vazava memória ou ignorava a config:
  - **A1 — corrida no `Shutdown` do TTS (crash nativo):** o `Shutdown` agora **espera a thread de síntese**
    (`Join(5000)`) antes de liberar o modelo, `Process`/`TryGetTts` recusam sintetizar/carregar depois do
    `Shutdown` e o `Dispose` passou para dentro do gate do nativo. Antes a thread podia estar **entre duas frases**
    (contador de "ocupado" em 0) e o modelo era liberado com ela viva — o próximo `Generate` sobre o handle morto
    derrubava o jogo (AccessViolation, que nenhum `catch` pega). Se o `Join` estourar, o modelo **fica carregado**
    (como antes: melhor segurar do que descarregar em uso).
  - **A2 — áudio nativo com handle nulo:** o binding devolve um objeto **não-nulo** mesmo quando a síntese nativa
    falha (handle `IntPtr.Zero`), e `NumSamples`/`Samples` fazem `PtrToStructure` no handle — ou seja, ler
    `NumSamples` com handle nulo era um crash. Agora o handle é checado **antes**, a falha vai para o log e a frase
    é pulada.
  - **A3 — `Speak` depois do `Shutdown`:** não enfileira mais (a fala ficaria na fila para sempre).
  - **A4 — pré-carga do modelo com nova tentativa:** o `Prewarm` tentava **uma vez**; agora tenta de novo a cada
    **30 s** enquanto você está no mundo, até carregar. Junto com isso, "modelo não encontrado" deixou de ser
    definitivo: copiar a pasta do modelo com o jogo aberto passa a funcionar **sem reiniciar** (o erro é logado
    uma vez só).
  - **C2 — cache do TTS com teto por bytes:** o teto era por **entrada** (256) e cada entrada é um `float[]` cru
    (frase de 180 caracteres ≈ 1,1 MB) — dava para passar de centenas de MB, e cada resposta única do modelo
    **expulsava as falas fixas**. Agora o teto é **32 MB de áudio** (descartando as frases mais antigas) e **só as
    linhas do mod entram no cache** — as respostas do modelo são novas a cada vez.
  - **B1 — pré-carga nativa confere *qual* DLL entrou:** se `onnxruntime.dll`/`sherpa-onnx-c-api.dll` já estavam
    carregados de outro caminho (outro mod ou cópia na pasta do jogo), o log agora **avisa** — o `DllImport`
    resolve por **nome-base** e podia acabar numa versão com ABI diferente.
  - **D2 — o painel da IA não abre por cima de nada:** a tecla é recusada com o inventário, o mapa, o menu, o chat
    com foco, o console, a entrada de texto, a busca do comerciante ou o **painel F8** abertos — e o painel F8, por
    sua vez, não abre enquanto o painel da IA está visível (nunca mais um painel sobre o outro).
  - **E1 — texto que faltava em inglês:** `hc_ai_voice_key_conflict` (o aviso do conflito com o auto-pickup do
    jogo) só existia no JSON PT-BR; agora tem o par em inglês nos padrões (outro idioma não mostra mais o token
    cru).
  - **E2 — slider de velocidade alinhado com a config:** o painel ia de 0,5 a 1,5 enquanto `[IA] TtsVelocidade`
    aceita até 2,0 — com 2,0 no `.cfg` o valor aparecia como "2.00" e o primeiro arrasto rebaixava para 1,5 sem
    avisar. Agora os dois usam os **mesmos** limites (`CompanionTts.MinSpeed/MaxSpeed`).
  - **D3 — cursor ao fechar o painel:** **nada a fazer** (documentado): quem devolve o cursor é o próprio jogo
    (`GameCamera.UpdateMouseCapture`, a cada frame, quando não há janela aberta) — e forçá-lo contrariaria quem
    desliga a captura do mouse de propósito (`Ctrl+F1`).
- **Fase 3.7 — painel da IA no jogo (tecla `I`): personalidade, voz com *Ouvir*, push-to-talk e toggles.** Tudo o
  que a IA lê do `.cfg` passou a ser ajustável **em jogo**, sem editar arquivo nenhum e sem reiniciar. Arquivo novo:
  `src/Companions/UI/CompanionAiPanel.cs` (painel de madeira no estilo dos outros painéis do mod). A tecla nova é
  **`[IA] PainelTecla`**, padrão **`I`** (livre no jogo e no mod: `Z`/`E`/`F8`/`LeftAlt+Z`/`F7`/`V` são nossas,
  W/A/S/D, Tab, Esc, F, Q, C, R, X, G, H, T, M, B, Ctrl, Shift, Space e V são do jogo). Como é uma **letra**, ela
  não é roubada de uma caixa de texto: o chat, o console, a entrada de texto e a busca do comerciante continuam com a
  letra só para eles. Abre/fecha com a tecla, com **Esc** e com o **X**; bloqueia o input do jogador enquanto está
  aberto (mesmos prefixos Harmony dos outros painéis + `Hud.InRadial` + `VoiceInput.BlockedByUi`) e fecha sozinho se
  o jogador morrer ou sair do mundo. **Não precisa de companheiro perto** (é config global).
  - **Personalidade:** dropdown (Padrão/Viking/Mago/Sarcástico/Guia/Texto livre) + campo de **texto livre** +
    **Aplicar** → gravam `[IA] PresetPersonalidade` e `[IA] Personalidade` (o texto livre ganha, como sempre) e o
    log/chat dizem o resultado efetivo (`CompanionPersona.Describe()`). Vale na **próxima mensagem**.
  - **Voz (TTS):** dropdown das **três vozes pt-BR** (42 `pf_dora` feminina, 43 `pm_alex` masculina, 44 `pm_santa`
    masculina) + botão **Ouvir** (fala uma frase de exemplo pelo **mesmo** pipeline — worker, cache e playback 3D —
    e fica desabilitado se o TTS estiver desligado ou o modelo faltando) + sliders de **volume** (`TtsVolume`) e
    **velocidade** (`TtsVelocidade`, 0,5–2,0 no painel, o mesmo intervalo do `.cfg`). A voz vale na **próxima fala**; o volume, no próximo áudio.
  - **Push-to-talk:** mostra a tecla atual (`[IA] VozTecla`) e o botão **Alterar** captura uma nova com o **mesmo**
    `KeyBindListener` do painel F8 (nada de lógica nova) + o toggle `VozLigada`. Ao trocar a tecla, o
    `AutoPickupKeyConflict.Reset()` reavalia o conflito com o *Toggle Auto-Pickup* do jogo (Fase 3.3b).
  - **Toggles/valores:** `TtsLigado`, `TtsFalarRespostas`, `TtsFalarFalasDoMod`, `Conversa` e o slider de `Memoria`
    (0..12).
  - **Salva na hora:** cada controle escreve no seu `ConfigEntry` e o BepInEx grava o `.cfg` no mesmo instante
    (`ConfigFile.SaveOnConfigSet`). Sem bloqueio de main thread, sem alocação por frame (o painel é montado **uma
    vez**, no primeiro `Show`; por frame só a tecla, o cursor e um status 2×/s) e sem segredos no código.
  - **Textos:** 33 chaves PT-BR novas em `ModLocalization` + `Translations/Portuguese_Brazilian.json`
    (`hc_ai_panel_*`, `hc_ai_voice_42/43/44/other`).
- **Fase 3.4 — o companheiro FALA: voz sintetizada local (Kokoro-82M via sherpa-onnx).** O companheiro agora
  **fala em voz alta** o que responde, **na posição dele** no mundo. A voz é sintetizada **dentro do jogo** pelo
  **Kokoro-82M** através do **`sherpa-onnx`** (NuGet `org.k2fsa.sherpa.onnx` 1.13.8, **Apache-2.0**, in-process) —
  sem HTTP, sem processo filho, nada sai da sua máquina. É o espelho do `WhisperStt`: modelo carregado **uma vez**,
  síntese **fora da main thread**, fila thread-safe drenada no `Update`. Nada do resto mudou (ordens por chat/voz,
  STT, personalidade/memória/conversa, jobs, UI, radial, balão e chatter seguem iguais):
  - **Vozes em português do Brasil:** `[IA] TtsVoz` **42** = `pf_dora` (feminina, padrão), **43** = `pm_alex`
    (masculina), **44** = `pm_santa` (masculina). Os outros números do modelo são de outros idiomas.
  - **Por FRASE, com cache:** o texto é cortado em frases (`.` `!` `?` `…`, quebras de linha; um ponto entre
    dígitos é decimal; uma sequência de terminadores é **um** corte) e a **primeira frase já toca enquanto a
    próxima é gerada** — a resposta começa a soar rápido. Falas repetidas (texto + voz + velocidade) saem do
    **cache**, na hora — e **só as linhas do mod** entram nele (as respostas do modelo são novas a cada vez), com
    teto de **32 MB de áudio** (ver as correções da auditoria no topo).
  - **Playback 3D:** **um** `AudioSource` na posição do companheiro (`spatialBlend = 1`), volume de
    `[IA] TtsVolume`, **uma fala por vez** (fila, sem sobrepor), e o `AudioClip` é destruído ao terminar.
  - **O que é falado** (regra: *a voz acompanha a linha que aparece no chat*): a **resposta do modelo**
    (`[IA] TtsFalarRespostas`) e/ou a **linha do próprio mod** (`[IA] TtsFalarFalasDoMod`, ex. *"Ok: coletar
    madeira (10)."*, *"Já temos o suficiente."*). Numa **ordem** a linha do mod ganha (desligue
    `TtsFalarFalasDoMod` para ouvir a resposta do modelo no lugar dela); numa **conversa** é a resposta do
    modelo. Nada é falado duas vezes (o texto que não foi falado vai para o log). Erro do LLM não é falado.
  - **Prompt:** o contrato passou a exigir **UMA frase curta (até ~15 palavras)** no `reply` e a dizer que ela é
    **falada em voz alta** — frase curta = fala mais rápida. O JSON do contrato não mudou.
  - **Config `[IA]` (PT-BR):** `TtsLigado` (true), `TtsVoz` (42), `TtsVelocidade` (1.0), `TtsThreads` (**4** — RTF
    medido ~0,83, mais rápido que o tempo real), `TtsVolume` (1.0), `TtsModelo` (`tts\kokoro\model.int8.onnx`;
    `voices.bin`/`tokens.txt`/`espeak-ng-data` vêm da mesma pasta), `TtsIdioma` (**`pt-br`** — este Kokoro é
    multi-lingue e a lib nativa **recusa** carregar sem idioma; `pt` sozinho é português de Portugal),
    `TtsCache` (true), `TtsFalarRespostas` (true), `TtsFalarFalasDoMod` (true).
  - **Carregamento dos nativos (Unity/Mono):** o binding gerenciado é um `[DllImport("sherpa-onnx-c-api")]` puro
    (não tem loader próprio como o Whisper.net), e no Unity o Windows procuraria na pasta do **jogo**, não na do
    plugin. `CompanionTts.EnsureNativeLibraries` roda antes do primeiro uso: `SetDllDirectory(<plugin>\runtimes\
    win-x64\native)`, **pré-carrega** `onnxruntime.dll` e `sherpa-onnx-c-api.dll` por caminho absoluto com
    `LoadLibraryEx(..., LOAD_WITH_ALTERED_SEARCH_PATH)` e **restaura** a ordem de busca (`SetDllDirectory(NULL)`)
    — os módulos ficam no processo e o `DllImport` resolve por nome. O log diz qual DLL veio de onde.
  - **`AudioClip`/`AudioSource` por reflexão:** o `UnityEngine.AudioModule` não é referenciável neste projeto
    (netstandard 2.0 fixado pelos pacotes de voz → CS1705; a fachada 2.1 do jogo quebra `Span<T>` → CS7069), o
    mesmo motivo do `VoiceInput.Mic`. A resolução é feita **uma vez** (11 membros) e as chamadas acontecem uma vez
    por frase — o único trabalho por frame é o `transform.position` do `AudioSource` (CoreModule, direto).
  - **Diagnóstico:** `[TTS] Modelo CARREGADO em N ms`, `[TTS] Frase i/n: N ms, RTF 0,xx para N,NN s de audio`,
    `[TTS] Fala #n pronta: ...` e o comando de console **`modia_tts_teste [texto]`** (`F5`) para falar um texto
    sem precisar do LLM nem do microfone.
  - **PT-BR:** `hc_ai_tts_unavailable` (*"A voz do companheiro não está pronta (falta o modelo do Kokoro?)."*).
  - **Verificação:** o `SplitSentences` **real** (17 casos) e o `PickSpokenText` **real** (14 casos, a tabela do
    que é falado) compilados num harness de console (net472) — **ALL CHECKS PASSED**; a API do sherpa-onnx e o
    carregamento dos nativos validados num harness que sintetizou áudio de verdade (carga ~1 s, 54 vozes, 24 kHz,
    **RTF 0,73–0,91** com 4 threads). Detalhes: `docs/re/tts-kokoro-sherpa-onnx.md`.

- **Fase 3.8 — "abre o teu inventário": o companheiro abre o próprio inventário para o jogador.** Pedido do jogador:
  *"seria possível fazer o companion abrir o inventário quando solicitado?"*. Falar/digitar `abre o teu inventário`,
  `mostra o inventário`, `mostra o que você carrega` (ou `fecha o inventário`, de bônus) e a **janela do inventário
  do companheiro** abre — a **mesma** do toque na tecla de interagir, com nome, comida e equipamento. A ordem entra
  pela **mesma** caixa das outras (`CompanionChatAI.Submit` — chat **e** voz) e termina na **mesma** chamada que o
  toque usa (`CompanionInteract.OpenCompanionInventory` → `InventoryGui.Show(container)` +
  `CompanionInteractPanel.Show(setup)`). Nada de ordem, job, radial, UI, STT, balão do jogador ou dos raios/chatter
  foi tocado:
  - **Contrato do LLM:** nova ação `inventory` (`inventory_close` para fechar) na lista de ações, na regra e em **2
    exemplos** do system prompt (`"abre o teu inventário"`, `"mostra o que você carrega"`). O parser (`LlmIntent`)
    aceita a ordem em muitas formas: aliases exatos (`inventario`, `abrir_inventario`, `abre_o_teu_inventario`,
    `mostra_o_inventario`, `mochila`, `bolsa`, `bag`, `abre`, `abrir`, `open`, `mostra`, `mostrar`; `fecha*`/`close*`
    para fechar) **e** qualquer ação que carregue o nome da mochila (`invent*`, `mochila`, `bolsa`, `bag`) —
    `open_bag` e `mostrar_o_que_voce_carrega` caem na ação certa. A frase que só diz "carrega" precisa também pedir
    para **ver**: `carregar_madeira` **não** vira inventário (continua uma coleta/nenhuma ordem).
  - **Um companheiro só** (uma janela mostra uma mochila): o companheiro a quem a mensagem foi dirigida
    (`CompanionChatAI.PendingSpeaker`, o `PickSpeaker()` que já escolhe com quem o jogador fala) e, se ele já não
    existir quando a resposta chega, o mais próximo que pode receber ordem. A escolha **não** toma posse dele — quem
    toma é o painel do companheiro no `Show`, como no toque.
  - **Posse do container (multiplayer):** quando o container não é nosso, o caminho do toque só pedia a posse e
    mandava o jogador interagir de novo. `OpenCompanionInventory` passou a **devolver** o resultado (`Opened`/
    `Requesting`/`NoGui`/`NoContainer` — os chamadores do toque ignoram, então nada mudou para eles) e a ordem
    **pede uma vez e abre sozinha** quando a posse chega: `CompanionInteract.RequestCompanionInventory` guarda o
    container e `TickPendingInventoryOpen` (no `Update`, **uma vez por frame**, `ClaimOwnership` **uma única vez**)
    abre a janela assim que `IsOwner` responde; desiste em **5 s** com aviso, e larga o pedido em silêncio se o
    companheiro sumiu ou se o jogador abriu outra janela. Custo **zero** quando não há pedido pendente.
  - **Rede de segurança (como a 3.6b):** se o modelo responder `none` para `abre o teu inventário`,
    `CompanionOrders.IsInventoryRequest` lê as **palavras do jogador** e exige uma palavra da mochila **e** um verbo
    de abrir/fechar — `o que tem no teu inventário?`, `quanto pesa a tua mochila?` e `abre a porta` continuam
    conversa/nenhuma ordem.
  - **PT-BR:** `hc_ai_inventory_open` (*"Abrindo o inventário."*, também falado no balão do companheiro),
    `hc_ai_inventory_requesting`, `hc_ai_inventory_timeout`, `hc_ai_inventory_no_gui`, `hc_ai_inventory_closed`,
    `hc_ai_inventory_none_open`, `hc_ai_no_container`, `hc_ai_order_inventory(_close)`.
  - **Verificação:** o `LlmIntent.cs` **real** e o `IsInventoryRequest` **real** (recortado verbatim) compilados num
    harness de console (net472) e exercitados com **45 casos** (12 formas de resposta do modelo, 8 respostas que já
    funcionavam, `carregar_madeira`/`carregar_pedra` que **não** podem virar inventário e 19 mensagens de jogador para
    o fallback): **ALL CHECKS PASSED**. Receita em jogo: `abre o teu inventário` (chat), `mostra o que você carrega`
    (voz) e `fecha o inventário`.
- **Fase 3.6b — a ordem sem alvo: o modelo responde `"target": null`.** Relato do jogador: "o comando pra dropar não
  funcionou". No log (voz), o modelo acertou a ação e a quantidade mas devolveu `"target": null` nos dois pedidos
  (`"Joga essas pedras que tu pegou no chao."` e `"Joga 10 pedras no chao."`) e a Fase 3.6 só sabia largar o que o
  **campo do modelo** nomeasse — resultado: `[IA] "drop" -> 0 companion(s)` e "Não entendi qual item.". O mesmo valia
  para `equip` e para `attack` (`ataca o greydwarf` não engajava nada quando o campo vinha vazio). Duas frentes, sem
  tocar em nada do que já funcionava (coleta/alvo no inventário, jobs, radial, UI, STT, raios/chatter, personalidade/
  memória/conversa e o balão do jogador seguem idênticos):
  - **Fallback pelas palavras do jogador (o principal).** `CompanionOrders.Run` passa a receber o texto do jogador
    (`result.UserText` — vale para o chat **e** para a voz, que entram pelo mesmo `Submit`). Quando o modelo não
    nomeia o alvo, o novo `OrderTargets.Phrases(mensagem)` extrai as frases de nome do que o jogador disse — só as
    palavras que **podem** ser nome entram: os verbos da ordem (`joga`, `larga`, `equipa`, `pega`, `ataca`), pronomes
    e demonstrativos (`essas`, `aquele`, `que tu pegou`), enchimentos (`coisa`, `lixo`, `isso`), números escritos e
    **dígitos** (`10` é o `amount`) e lugares/direções (`chão`, `fora`, `mão`, `perto`, `pra`/`pro`/`ao`) são
    descartados; o resto vira nomes de até 3 palavras, **do mais longo para o mais curto** (para `machado de bronze`
    ganhar de um `machado` solto, que casaria com qualquer machado). Cada frase é então **validada** contra as
    mochilas (`OrderTargets.FindItem`, o mesmo casador da 3.6) ou contra as criaturas no raio
    (`OrderTargets.FindCreatureByWords` → `ClosestByName`), com uma barra **mais alta** que a do resto
    (`MinPhraseScore = 900`, contra `MinScore = 450`): sem isso `"larga a pedra e a madeira"` viraria `pedramadeira`
    e casaria por **substring** com `Madeira`, largando o item errado em silêncio. O `target` do modelo continua
    ganhando sempre que casa com algo que o companheiro carrega; se nem a mensagem resolver, o aviso antigo continua
    (`hc_ai_need_item`/`hc_ai_no_item`) e o log explica. Em `attack` a mensagem é lida **antes** da mira, e só quando
    o modelo não nomeou nada: `ataca aquele` (que não nomeia nada) continua indo para a mira, como na 3.6.
  - **Prompt reforçado.** `[IA]` system prompt (`CompanionChatAI.Contract`): `target` é **OBRIGATÓRIO** em
    `attack`/`equip`/`drop`, com as mesmas palavras do jogador e **nunca** `null`/vazio, mais 2 exemplos explícitos de
    `drop` (`"joga essas pedras que você pegou"` → `"pedra"`, `"joga 10 pedras no chão"` → `"pedra"` + `amount: 10`)
    e a nota de que o item vai **sempre** em `target`, nunca em `resource`. O prompt continua enxuto (3 linhas a mais)
    e a parte fixa continua no começo, cacheável pelo servidor.
  - **Confirmado:** o parser da 3.6 já aceitava o item em `resource` quando `target` vinha nulo
    (`LlmIntent.Parse`, bloco `NeedsTarget`) — nada foi mudado aí.
  - **Verificação:** as peças novas foram exercitadas contra o **próprio** `OrderTargets.cs` compilado num harness
    fora do jogo (stubs de Unity), com 22 mensagens e uma mochila realista — as receitas casaram o item/criatura certo
    e os casos que **devem** ser recusados foram recusados (`joga o lixo fora`, `joga essas coisas no chão`,
    `ataca aquele`). Receita de teste em jogo: `joga 10 pedras no chão` (larga 10), `joga essas pedras` (larga o
    stack), `larga a madeira`, `equipa o machado de bronze`, `ataca o greydwarf`.
- **Fase 3.3d — a ordem de pedra para no alvo e o texto da voz vai para o balão do jogador.** Dois problemas do teste
  em jogo, sem lógica nova de ordem/job/UI/STT (nada de radial, IA ou raios da 3.3c foi tocado).
  - **`colete 10 pedras` nunca parava.** Causa confirmada no jogo (bundle `c4210710`, `Assets/world/Props/Rock_3.prefab`)
    e no log: `Rock_3` é um **`Destructible` com `health = 1` e NENHUMA tabela de drop** — a pedra vem do
    `Rock_3_frac` que ele **spawna ao ser destruído** (`m_spawnWhenDestroyed`), e esse frac é um **`MineRock5`** com
    3-6 `Stone` por área de golpe (5 áreas ≈ 15-30 pedras por rocha). Como o `MHR_GatherRules.DropsOf` só olhava os
    componentes do próprio alvo, `Rock_3` parecia "não dropa nada" → o `AllowTarget` caía no
    `drops.Count == 0 → "unknown drops: don't block it"` → **o teto da ordem (e o próprio `StoneLimit`) não valia
    para ele**, então o companheiro minerava para sempre (no log: 10+ pedras com a ordem de 2, Pickaxes 6→7→8 e
    nenhuma linha de "alvo atingido" para pedra; a ordem de **madeira** parava normalmente).
    - **Na raiz:** `DropsOf` agora **segue o que o alvo spawna** ao quebrar (`Destructible.m_spawnWhenDestroyed` e
      `m_spawnWhenDamaged`, com limite de profundidade 3 e cache por alvo que já existia) — `Rock_3` passa a ser
      julgado pelas pedras do frac dele, como já era o caso do `Rock_4` (que tem `DropOnDestroyed` com 3-6 pedras).
    - **Rede de segurança (o pedido do jogador):** novo `MHR_GatherRules.OrderMet` — quando há **ordem ativa para o
      modo** e o **item pedido é conhecido** e o **inventário do companheiro já tem o número** (contado como na
      3.2c, `OrderSeen(Bag)`, teto do que ele já teve), **TODOS** os alvos daquele modo são recusados, não só os que
      produzem o item. Vale para `AllowTarget` **e** `AllowLoose`; cada recusa conta como `CapReject`, então o
      caminho de `OrderDone`/`CapRejects` que já existia encerra a ordem **uma vez** ("Order target reached … - order
      done, back to following") e o companheiro volta a seguir. Sem ordem, o comportamento é o de sempre; alvo
      apontado no **radial** (`IsDirectedHarvest`) **ignora** o teto, como antes.
    - **Ele para na hora:** `HarvestController.Update` chama o novo `MHR_GatherRules.OrderDoneNow` enquanto está
      **indo ou trabalhando** um alvo (nunca durante a coleta do que aquele alvo dropou, para não deixar pedra no
      chão) — sem isso o número só era conferido no próximo *scan*, e uma rocha de pedra pode render uma rocha
      inteira antes disso. Custo: uma contagem do inventário por frame (a mesma que o `IsOverweight()` já faz), e só
      com ordem ativa.
  - **O texto da voz aparece sob a cabeça do JOGADOR.** A transcrição do push-to-talk (`HeardByVoice`) era repetida
    pelo companheiro mais próximo (`Speak(PickSpeaker(), texto)`), o que punha a **fala do jogador** no balão do
    companheiro. Agora ela vai para o balão do **jogador** (`CompanionTalk.SayAsPlayer` →
    `Chat.instance.SetNpcText(Player.m_localPlayer.gameObject, Vector3.up * 2f, SpeechCullDistance, SpeechDuration,
    "", texto, false)` — mesmo offset/distância/duração do balão do companheiro, sem cooldown) e o feedback
    "Entendi: …" continua no **chat/HUD**. `Player.m_localPlayer == null` não faz nada. As respostas do companheiro
    (conversa da IA, "Deixa comigo!", "já temos de sobra") **continuam** no balão dele.
- **Fase 3.3c — raio de coleta maior e chatter aleatório desligado.** Dois pedidos do jogador, sem lógica nova (nenhuma
  ordem, job, radial, UI, IA ou STT foi tocado).
  - **O companheiro procura e trabalha mais longe.** Os cinco raios da coleta subiram de default:
    `[Advanced] GatheringSearchRadius` **30 → 50 m** (o anel em que ele procura a próxima árvore/rocha/planta seguindo
    você ou mandado a um alvo), `[Gathering] FollowRange` **10 → 25 m** (madeira/pedra/minério seguindo você: o alvo
    pode estar a até 25 m de **você**), `[Gathering] ForageFollowRange` **10 → 20 m** (forrageio),
    `[Gathering] Range` **40 → 60 m** (com Follow **desligado**, a área de trabalho em volta de casa) e
    `[Advanced] GatheringDropRadius` **8 → 12 m** (o que ele cata de uma árvore derrubada/rocha quebrada).
    **Por que 50 no `GatheringSearchRadius`:** seguindo você, o limite real é o `FollowRange` (cada candidato passa por
    `InWorkArea` → distância de **você**), mas o companheiro pode estar a 25 m de você e o alvo a 25 m do outro lado —
    o pior caso são ~50 m do companheiro, então o anel tem de chegar lá para não virar o gargalo. **Coleira conferida:**
    com alvo em andamento o `HarvestController` **abandona** o alvo se ficar a mais de `FollowRange + 15 m` de você
    (25 + 15 = **40 m** de folga, contra 25 m antes) e o `MHR_Follow` (`[Follow] MaxDistance` 10 m) só volta a mandar
    quando o job fica **ocioso** — então o raio novo é utilizável de verdade (o efeito colateral é um vaivém mais
    longo: ele vai até ~25 m, coleta e volta quando o anel esvazia). Os `AcceptableValueRange` já acomodavam todos os
    valores novos (50 ≤ 100, 25/20 ≤ 60, 60 ≤ 150, 12 ≤ 20) — nenhum range mudou.
  - **Sem comentários aleatórios:** nova config **`[Speech] Chatter`** (bool, default **false**). O chatter periódico
    vinha **só** do timer do `CompanionTalk.Update()` (`_talkTimer` → `ResetTimer()` → `SayContextual()`, a cada
    `MinInterval`–`MaxInterval` s, escolhendo a fala pelo contexto e caindo em 30% `IdleLines` / 70% `FollowLines`);
    com `Chatter = false` o timer **não é nem decrementado**. **Continuam falando** (nada de silêncio no que importa):
    as falas **forçadas** da camada de IA (confirmação de ordem, "já temos o suficiente", respostas da conversa,
    "Entendi: ..." da voz), a linha de **mudança de modo** (`SayRandom(ActionLines)`, o "Deixa comigo!" — roda **antes**
    da guarda), as falas de **evento** (`hc_speech_*`: peixe fisgado, plantando, reparo, unload, tombstone, marco do
    smelt) e as linhas de **ociosidade** (`[Idle] SpeechInterval`, 300 s, com configuração própria). `Chatter = true`
    devolve o comportamento antigo inteiro. Custo: um `if` e uma leitura de `ConfigEntry<bool>` por frame no lugar do
    decremento — nada por frame, nada alocado.
  - **Migração:** como os defaults mudaram, um `.cfg` existente mantém os valores antigos (o BepInEx guarda o que está
    no arquivo). Mesma regra da 3.3b: um valor que esteja **exatamente** no default antigo (`30`/`10`/`10`/`40`/`8`) é
    atualizado com uma linha no log explicando; qualquer valor escolhido pelo jogador **não** é tocado. A chave nova
    `[Speech] Chatter` é criada pelo próprio BepInEx com o default **false** (aparece no `F8` e no `.cfg`). A migração
    roda depois do `MigrateAndClean` (1.4.149), então um `.cfg` ainda com os nomes antigos (`[Harvest] FollowRange`)
    também é coberto.
- **Fase 3.3b — correções do teste da voz (tecla V, qualidade do STT e latência do LLM).** Três problemas do
  primeiro teste em jogo, com evidência no log (`"Coleta deis mandem."` para 1,1 s de fala; `"O leta deles."` para
  0,7 s; conversa em `10237 ms` contra ~3 s das ordens).
  - **A tecla V não alterna mais o auto-pickup do jogo.** `V` é o *Toggle Auto-Pickup* do Valheim 1.0.17
    (`ZInput.Reset()` → `AddButton("AutoPickup", KeyToPath(Key.V))`; lido em **um** lugar: `Player.Update`). Novo
    `Patches/AutoPickupKeyConflict.cs`: um prefixo Harmony em **`ZInput.GetButtonDown(string)`** responde `false` só
    para `"AutoPickup"`, e só quando `[IA] VozLigada = true` **e** o binding do jogo (lido do próprio jogo,
    `GetButtonDef("AutoPickup").GetActionPath()`) aponta para `[IA] VozTecla` — reatribuir o auto-pickup no jogo
    desliga o bloqueio automaticamente, e qualquer dúvida (tecla desconhecida, exceção) deixa o jogo como está. O
    jogador é avisado **uma vez** (log + uma linha no chat/HUD) de que pode reatribuir o auto-pickup nas
    configurações. O push-to-talk continua igual (segurar/soltar **V**).
  - **Transcrição melhor em PT-BR:** modelo default passa de `whisper\ggml-base-q5_1.bin` (57 MB) para
    **`whisper\ggml-small-q5_1.bin`** (181 MB, baixado de `ggerganov/whisper.cpp`; o `base` **fica em disco** para
    quem quiser voltar pelo `[IA] SttModelo` — medido nesta máquina, 8 threads: **638 ms (base) x 2187 ms (small)** para
    um clipe de 2 s, sempre em `Task` de fundo, sem travar o jogo); **prompt inicial** novo
    (`[IA] VozPromptInicial`, `WhisperProcessorBuilder.WithPrompt`, +3-5% de tempo) com o vocabulário das
    ordens, para o modelo não "adivinhar" clipes curtos (e um descarte do prompt quando o modelo o repete de volta);
    e o **trim** ficou mais conservador (`VozLimiarSilencio` default `0.01` em vez de `0.02`, folga de `0,2 s` em vez
    de `0,125 s`), porque o limiar antigo cortava o começo de palavras faladas baixinho.
  - **Latência do LLM medida:** o log agora mostra o tamanho real do prompt
    (`[IA] Prompt: instrucoes N car. (~N tokens) + mensagem N car. ...`) e o tempo com a resposta
    (`[IA] Tempo: N ms no total, resposta com N car. (~N tokens, ~N tok/s ...)`), com aviso a partir de 5 s apontando
    o modelo local como gargalo; `[IA] Memoria` default caiu de **6 para 4** trocas (prompt menor). O mod **não**
    troca o modelo do jogador.
  - **Migração:** um `.cfg` antigo que ainda esteja exatamente nos defaults antigos (`ggml-base-q5_1.bin`, `0.02`,
    `6`) é atualizado com uma linha no log (o modelo só troca se o arquivo novo estiver no disco); valores escolhidos
    pelo jogador não são tocados. Nada mais muda: ordens por chat, IA, jobs, UI, radial e o resto do STT ficam iguais.
- **Fase 3.3 — ordens por voz (push-to-talk + STT local com Whisper.net).** Segure **V** e fale (`colete 10 madeiras`,
  `segue`, `ataca o greydwarf`, `bom dia`); ao soltar, o áudio é transcrito **dentro do jogo** (Whisper.net, sem HTTP,
  nada sai da máquina) e o **texto entra exatamente no mesmo caminho das ordens do chat** — ordem, conversa,
  personalidade e memória iguais, porque é a **mesma função** (`CompanionChatAI.Submit`).
  - **Captura:** `Microphone.Start(dispositivo, loop: false, duração, 16000)` — **16 kHz mono**, o formato que o
    Whisper espera (nada de 8-bit/22050 Hz como no Thrall antigo). O clipe é lido só até `Microphone.GetPosition`,
    o silêncio das pontas é cortado (janelas de 10 ms acima de `[IA] VozLimiarSilencio`, com folga de 0,125 s) e o
    áudio vira **WAV PCM 16-bit mono montado em memória** (sem arquivo temporário). O `AudioClip` é destruído no fim.
  - **STT reutilizável (`AI/WhisperStt.cs`):** o spike virou serviço — o modelo é carregado **uma vez** e **mantido
    carregado** (pre-carregado em segundo plano ao entrar no mundo), a transcrição roda numa `Task` de fundo e volta
    por uma fila thread-safe drenada no `Update`, com watchdog e `NativeGate` (uma transcrição por vez). **A main
    thread nunca espera** e nada é alocado por frame. O spike (`modia_stt_teste`) passou a usar o **mesmo** modelo,
    então ele **não** é carregado duas vezes.
  - **Feedback:** `Ouvindo...` ao começar; ao terminar, `Entendi: "colete 10 madeiras"` no chat/HUD e o companheiro
    **repete no balão**; depois `Pensando...` e a linha de sempre (`Ok: coletar madeira (10)`). Nada reconhecido,
    gravação curta demais, sem microfone, sem modelo, janela aberta, jogo sem foco, pedido anterior em andamento —
    tudo isso **avisa** em PT-BR em vez de gravar.
  - **Configs novas em `[IA]`:** `VozLigada` (true), `VozTecla` (**V**), `VozIdioma` (`pt`), `VozDuracaoMaxima`
    (10 s), `VozLimiarSilencio` (0.02), `VozDispositivo` (0 = o primeiro) e `VozMostrarTexto` (true).
  - **`UnityEngine.AudioModule` por reflexão (8 membros, uma vez só):** o mod compila em net472 e referencia o pacote
    Whisper.net (assets netstandard2.0), o que fixa a fachada **netstandard 2.0**; todo módulo do Unity 6 é
    construído contra **netstandard 2.1** e um módulo fora da closure do jogo (`AudioModule` está fora) dá **CS1705**
    contra ela. Apontar o netstandard para a fachada 2.1 do jogo resolve o CS1705 mas quebra `AudioClip.GetData`
    (`Span<T>` no mscorlib, CS7069). A ponte por reflexão é o que mantém o build limpo sem levar uma segunda BCL
    para dentro do Mono. Nenhuma referência nova no `.csproj`.
  - **Nada quebrado:** adição pura (nenhuma mudança em ordens por chat, IA, jobs, UI ou radial); o único ponto novo
    no código existente é o `Submit` compartilhado (o patch de `Chat.SendText` passou a chamá-lo, com o mesmo
    comportamento de antes) e duas chamadas novas no `Awake`/`Update`/`Shutdown` do plugin.
- **SPIKE — STT local: Whisper.net in-process (teste, nada de pipeline de voz).** Prova de conceito para decidir
  entre o Whisper.net dentro do jogo e um `whisper-server.exe` como processo filho. Roda o modelo de verdade numa
  **thread de fundo** (a main thread nunca espera), transcreve um WAV de teste e escreve tudo no log:
  `[STT]` versão/arquitetura detectada (`whisper_print_system_info`: AVX/AVX2/FMA), tempo de **carga do modelo**,
  tempo de **transcrição**, número de segmentos, idioma e o **texto transcrito**; qualquer falha sai com a
  mensagem completa + stack (inclusive `Failed to load native whisper library`).
  - **Gatilhos:** comando de console **`modia_stt_teste`** (F5) e a config **`[IA] SttTeste`** (bool, default
    `false`), que dispara sozinho ~25 s depois do plugin carregar (mundo já pronto). Caminhos por
    **`[IA] SttModelo`** (default `whisper\ggml-base-q5_1.bin`) e **`[IA] SttWavTeste`** (default
    `whisper\jfk.wav`), relativos à pasta do plugin ou absolutos.
  - **Como o loader nativo foi apontado para a nossa pasta:** no net472 o pacote resolve o alvo
    `netstandard2.0`, cujo `NativeLibraryLoader` faz `Path.GetDirectoryName(RuntimeOptions.LibraryPath)` e procura
    `<pasta>\runtimes\win-x64\whisper.dll`. `WhisperFactory.FromPath` **não** tem `libraryPath` na 1.9.1, então
    usamos `RuntimeOptions.LibraryPath = <pasta do plugin>\Whisper.net.dll` (definido antes de qualquer uso do
    factory) — a pasta do plugin passa a ser a primeira sondada, em vez de `AppContext.BaseDirectory` (que no
    Unity é a pasta do jogo). A ordem de runtimes é travada em **CPU** (só existe `runtimes\win-x64`).
  - **Build/deploy:** `PackageReference` de `Whisper.net` 1.9.1 + `Whisper.net.Runtime` 1.9.1 e o novo target
    **`DeployWhisperNet`**, que copia para a pasta do plugin **7 DLLs gerenciadas** (Whisper.net,
    Microsoft.Bcl.AsyncInterfaces, System.Threading.Tasks.Extensions, System.Memory, System.Buffers,
    System.Numerics.Vectors, System.Runtime.CompilerServices.Unsafe) e os **4 nativos** em `runtimes\win-x64`
    (`whisper.dll` + `ggml-*.dll`). `Microsoft.Extensions.AI.Abstractions`/`System.Text.Json` ficam **fora**
    (`ExcludeAssets="all"`): só servem ao `WhisperSpeechToTextClient`, que não é usado, e arrastariam
    `System.Text.Json` para dentro do Mono do Unity.
  - **Nada de bloqueio e nada quebrado:** o spike é adição pura (nenhuma mudança em ordens por chat, IA ou jobs);
    a única chamada nova no jogo é um postfix em `Console.Awake` (para registrar o comando de console) e dois
    `try/catch` de `Tick()` no `Update`. Detalhes e o que procurar no log: **`docs/re/stt-whisper-net-spike.md`**.
- **Fase 3.5 — personalidade, memória e conversa livre.** O companheiro agora **tem um jeito de falar**, **lembra
  das últimas trocas** e, quando a mensagem **não é uma ordem** (`bom dia`, `como você está?`, `o que achou da minha
  casa?`), **responde como conversa**: fala no balão do mundo (`CompanionTalk.Say`) e mostra a resposta no chat.
  - **Personalidade:** novo `[IA] Personalidade` (texto livre do jogador, até 600 caracteres) e
    `[IA] PresetPersonalidade` com presets embutidos em PT-BR — **`viking`** (leal e destemido), **`mago`** (velho e
    sarcástico), **`sarcastico`** (piadas secas), **`guia`** (paciente e didático) — mais **`custom`** (usa o texto
    livre). Texto livre **ganha** do preset; vazio = companheiro padrão (leal e prático); preset desconhecido avisa
    no log e cai no padrão. O texto entra no começo do prompt, junto com o **nome** do companheiro
    (`Você é Thorin, o companheiro NPC do jogador, e ele é o seu dono.` / `Seu jeito de falar e de agir: ...`).
  - **Memória:** novo `[IA] Memoria` (6; 0 = desligada, teto 12). Guarda as últimas trocas (jogador ↔ companheiro)
    **por companheiro**, com teto de `2 × Memoria` mensagens (240 caracteres cada, sem quebras de linha) e no
    máximo 8 conversas ao mesmo tempo. Vai no prompt como **texto** (`Jogador: ...` / `Thorin: ...`), e não como
    mensagens `user`/`assistant` extras — o contrato JSON continua sendo a última coisa que o modelo lê e ele nunca
    recebe o próprio JSON antigo de volta. É **só em memória** (nada no ZDO/save) e é **esquecida** na morte
    (`OnCompanionDeath`), no dismiss/despawn (`OnDestroy`) e na troca de mundo.
  - **Conversa livre × ordem:** o JSON ganhou **`reply`** (aceita `resposta`/`fala`/`mensagem`/`message`/`texto`/
    `text`/`say`; sem JSON nenhum, uma frase do modelo passa a ser lida como `reply`). Numa **ordem** nada mudou: a
    **linha do mod ganha** e o `reply` é só logado (o companheiro não diz duas coisas sobre a mesma ordem, e a linha
    localizada é a que sabe **nomear** criatura/item: "Atacando o troll."). Em conversa, o `reply` é falado (balão
    forçado) e mostrado no chat. Novo `[IA] Conversa` (true): `false` volta ao comportamento exato da Fase 3.1.
  - **Quem fala:** o companheiro **mais próximo** que pode receber ordem (seu, `Command` ligado, dentro do recall).
    A conversa **não** toma posse do companheiro (isso continua só nas ordens). Sem companheiro perto, a resposta
    ainda aparece no chat, sem balão.
  - **Arquivos novos:** `src/Companions/AI/CompanionPersona.cs` (presets + resolução) e
    `src/Companions/AI/CompanionMemory.cs` (memória por companheiro + bloco do prompt). `CompanionSetup` ganhou
    `MHR_ResolveCompanionId`/`MHR_CachedCompanionId` (o ID permanente, lido uma vez) para a memória ser arquivada
    por companheiro (e não pelo nome).
  - **Textos PT-BR:** `hc_ai_default_name`, `hc_ai_preset_default/custom/viking/mago/sarcastico/guia`.
  - **Nada de bloqueio:** tudo continua em uma única chamada de fundo (sem `.Result`/`.Wait()`), o prompt é montado
    uma vez por mensagem (nunca por frame) e a memória é limitada nas duas pontas.
- **Fase 3.6 — ordens com alvo: o companheiro ataca a criatura que você nomeou, equipa o item que ele tem e
  larga o que você mandar.** `ataca o greydwarf`, `mata o javali`, `equipa o machado de bronze`, `pega a
  picareta`, `larga a pedra`, `joga fora 5 madeira`.
  - **Contrato novo do modelo:** o JSON ganhou o campo **`target`** (aceita `alvo`, `target_name`, `creature`,
    `item_name`) e as ações **`attack`**, **`equip`** e **`drop`**; `resource`/`amount` continuam iguais e o
    system prompt (PT-BR) explica as três, com exemplos, pedindo o alvo **nas palavras do jogador**.
  - **Como a criatura é achada:** primeiro a que está **na sua mira** (raycast da câmera, o mesmo caminho do
    comando por tecla) quando ela casa com o nome dito — aponte e diga "ataca aquele"; senão a **mais próxima
    que casa** dentro do novo **`[IA] RaioDeAlvo`** (40 m por padrão, a mesma coleira que o combate usa).
    Jogadores, companheiros e mortos nunca entram. Nada achado = **"Não vejo nenhum X por perto."**.
  - **Como o item é achado:** só no **inventário do companheiro** (nunca baús), casando o nome dito contra o
    prefab (`AxeBronze`), o nome localizado (`Machado de bronze`) e as palavras de cada um, com dicionário
    PT-BR/EN (machado↔axe, picareta↔pickaxe, sucata↔scrap...), singular/plural e palavras vazias ignoradas.
  - **Passiva:** uma ordem explícita de ataque vale **para aquela criatura mesmo na postura Passiva** — e a
    postura **não muda** (ele volta a "não começa briga" assim que o alvo morre/some). Sem isso a ordem seria
    engolida em silêncio, porque Passiva é literalmente "nunca começa uma briga".
  - **`drop` sem número larga o stack inteiro** ("larga a pedra" = livrar-se dela — o mesmo que o mod faz com o
    lixo que descarta para abrir espaço); com número, no máximo esse tanto. A pilha largada sai do auto-pickup
    (`m_autoPickup = false`) para ele não pegar a própria ordem de volta, item equipado é desequipado antes e
    **item de missão não cai**.
  - **`equip`** usa o `Humanoid.EquipItem` do jogo (item que não é equipável avisa). O auto-equip do mod pode
    depois trocar a arma de volta — é ele que escolhe a melhor arma para o trabalho/luta; a ordem não mexe em
    nenhuma configuração.
  - **`pega a picareta` = equipar, `pega madeira` = coletar:** o verbo "pegar" é lido pelo objeto (equipamento
    → equipar; recurso → trabalho de coleta), então **nada que já funcionava mudou**.
  - **Feedback próprio** em PT-BR (`hc_ai_attacking`, `hc_ai_no_target`, `hc_ai_no_aim`, `hc_ai_equipped`,
    `hc_ai_no_item`, `hc_ai_not_equipable`, `hc_ai_dropped`, `hc_ai_need_item`) no HUD/chat, e o companheiro
    **fala** a confirmação ("Atacando o troll.", "Equipei Machado de bronze.", "Larguei Pedra (5).").
  - **Novo arquivo:** `src/Companions/AI/OrderTargets.cs` (nome → criatura/item). O combate ganhou
    `EngageByOrder`/`EngagePreyByOrder`; o radial e as ordens antigas ficaram **iguais**.
- **Fase 3.1 — o jogador digita a ordem no chat do jogo e o companheiro executa.** `colete 20 de madeira`,
  `segue`, `para`, `vem cá`, `minere ferro`, `fica na defensiva`... A frase vai para um **LLM local** por uma
  API compatível com a OpenAI (`POST {BaseUrl}/chat/completions`; LM Studio, Ollama, llama.cpp, vLLM) e a
  intenção devolvida em JSON vira a **mesma ação** do menu radial/tecla de comando. Novos arquivos:
  `src/Companions/AI/LlmClient.cs` (cliente HTTP), `LlmIntent.cs` (DTO + parser tolerante),
  `CompanionOrders.cs` (intenção → ação do mod) e `CompanionChatAI.cs` (patch do chat + orquestração).
- **Nunca trava o jogo:** o POST roda numa `Task` de fundo e a resposta volta por uma fila thread-safe
  drenada no `Update` do plugin (sem `.Result`/`.Wait()`), com timeout configurável, um pedido por vez e
  watchdog. Erro de rede vira uma linha no HUD/chat, nunca uma exceção no jogo.
- **Captura do chat:** postfix Harmony em `Chat.SendText(Talker.Type, string)` — o caminho da mensagem **do
  próprio jogador** (as mensagens recebidas entram por `Chat.OnNewChatMessage` e não são capturadas).
  `Ping` é ignorado.
- **Novo `[IA]` no `.cfg`** (nada de segredo no código): `Ligado` (true), `BaseUrl`
  (`http://localhost:1234/v1`), `Modelo` (`google/gemma-4-e4b`), `ApiKey` (""), `Temperatura` (0.1),
  `TimeoutSegundos` (30), `Prefixo` ("" = toda mensagem; ex. `/c` só as mensagens com esse prefixo),
  `MostrarRespostaNoChat` (true, mostra a resposta crua do modelo no chat) e `CooldownSegundos` (3).
- **Ações entendidas:** seguir, madeira, pedra, minério, coleta (plantas), fundir, caçar, plantar, pescar,
  reparar/reabastecer, cozinhar; parar (volta a seguir + larga o alvo de combate), vir até você (recall),
  definir casa, vagar/parar de vagar, recolher/parar de recolher, aceitar/ignorar comandos e as posturas
  (equilibrada/defensiva/à distância/corpo a corpo). `despawn` **não** é exposto ao modelo.
- **Refatoração mínima:** o corpo de `CompanionRadialMenu.SetActionMode` virou
  `CompanionSetup.ApplyActionMode(companion, mode, toggle)` — o radial continua alternando (`toggle: true`),
  as ordens do chat **setam** o modo (`toggle: false`).
- **Feedback em PT-BR** via localização (`hc_ai_*`): `Pensando...`, `Ok: coletar madeira (20)`,
  `Não entendi a ordem.`, `Nenhum companheiro seu está perto o bastante...`, `A IA não respondeu: ...`, e a
  nova aba **IA** no painel F8.
- **Fase 3.2c — o número da ordem é um ALVO TOTAL contado no INVENTÁRIO do companheiro (correção final).**
  Regra do usuário: *"peço 10 madeiras e ele já tem 15 no inventário, então ele não executa e diz que já temos
  o suficiente"*; *"se ele já tem 7, ele coleta até ter 10 ou mais"*. Ou seja: o número é **um alvo total**
  (não um delta — a leitura de "10 a mais" da 3.3/3.2b foi **revertida e removida**), contado **apenas na
  mochila do companheiro** (não conta baús nem pilhas) e **só para o recurso pedido**.
  - **Item da ordem:** `src/Companions/AI/OrderItems.cs` (novo) traduz o recurso que o modelo devolveu
    (`wood`/`madeira`, `stone`/`pedra`, `ferro`/`iron`...) para o prefab (`Wood`, `Stone`, `IronScrap`), com
    mapa PT-BR/EN, conferência em `ObjectDB.GetItemPrefab` e, como reserva, o nome localizado dos itens da
    própria lista do modo. **Não reconheceu?** O alvo vale para o modo inteiro (como na 3.2).
  - **Contagem:** `MHR_GatherRules.Bag()` soma o item no `Inventory` do `Humanoid` (todos os stacks). O
    `Stock` (mochila + baús + pilhas) continua sendo o que a config usa (`WoodLimit`/`StoneLimit`/`OreLimit`).
  - **Já tem o suficiente:** a ordem **nem começa** e o modo de trabalho fica como estava; o companheiro diz
    "Já temos o suficiente." (nova chave `hc_ai_already_have`, PT-BR) e o HUD/chat mostra o mesmo.
  - **Só o recurso pedido:** com o alvo nomeando `Wood`, a árvore não é mais derrubada só porque a **resina**
    ainda está abaixo do `WoodLimit` (era o "continua cortando para chegar a 10 de resina").
  - **Ao bater o alvo:** a ordem é encerrada e o companheiro volta a seguir (fala "já temos ... de sobra").
    A última árvore pode passar do número — ela cai inteira.
  - **Depósito (limitação atenuada):** se a mochila encher e ele levar a carga para um baú, a contagem não
    recomeça do zero — `CompanionSetup.OrderSeen` guarda o **maior** valor visto na mochila durante a ordem.
    Ou seja, o alvo passa a significar "ter **tido** N no inventário" quando há depósito.
  - **Sem ordem, radial e `SetDirectedTarget` seguem exatamente como antes** (config pura).
- **Fase 3.2 — o número da ordem passou a valer: `colete 5 madeiras` para em 5.** Antes a ordem só ligava o
  job e o companheiro cortava até o limite da configuração (`[Gathering] WoodLimit`, 500). Agora o número vira
  um **alvo por companheiro**, guardado no ZDO dele (`HC_OrderCap` / `HC_OrderCapMode` / `HC_OrderCapItem`):
  funciona com vários companheiros (cada um com o seu número), sobrevive a um reload e é limpo quando a ordem
  termina, quando você diz `para`/`segue`, quando outra ordem de trabalho chega ou quando o modo de trabalho
  muda (radial incluído). Vale para **madeira, pedra e minério**; `Name:Number` da lista, se for menor,
  continua ganhando no caminho da config.
- **Ao bater o alvo o companheiro para e volta a seguir** (como o `stop`), com a fala de "já temos de sobra"
  e uma linha no log.
- **Fase 3.3/3.2b (REVERTIDA — ver 3.2c acima).** Chegou a valer em Unreleased que o número era um DELTA
  ("colete 10" = 10 A MAIS): a base do estoque do momento da ordem era somada ao número. Não é o que o
  usuário quer — o código (`CompanionSetup.OrderCapBase` e o `MHR_GatherRules.OrderLimit`) foi **removido**.
- Tradução completa do **sistema de localização** para `Portuguese_Brazilian`: `Translations/Portuguese_Brazilian.json`
  com **263 chaves** — as 204 do upstream e 59 novas para textos que estavam fixos em inglês no código.
  Chaves e placeholders (`{0}`, `{1}`) idênticos ao inglês; o `English.json` continua sendo o fallback e é
  regenerado sozinho quando faltam chaves.
- Tradução das **186 falas do companheiro** em `Speech/Portuguese_Brazilian.json` (o sistema de
  fala próprio do mod, `SpeechConfig`): saudações e ordens, conversa fiada, combate, fome, peso, reparo,
  fundição, caça, pesca, plantio, cozinha e "sem cama".
- O build agora copia `Translations\**\*.json` da raiz do repositório para `$(DeployDir)\Translations\`
  (alvo `DeployTranslations` no `Companions.csproj`) — de onde o mod lê UI e falas em runtime.
- Textos que estavam fixos em inglês e que o jogador vê passaram a usar a localização: título e abas do painel
  de configurações (F8, com as 21 categorias traduzidas), botões `Default`/`Aperte uma tecla...`, painel da
  zona da casa (título, raio e dica de controle), barras de vida/stamina do HUD, hover do túmulo
  (`Segure para ressuscitar`) e da cama (`Cama de X`), mensagem de recall e todo o **Santuário de Odin** /
  lista de companheiros (nome da peça, descrição, status Vivo/Morto/Perdido/Desconhecido, avisos e recuperação).
- Continua em inglês (fora do sistema de localização): as ~250 **descrições das configurações** (aparecem só no
  `BepInEx\config\*.cfg`; o painel F8 mostra as chaves), os **nomes técnicos das configurações** nas abas do
  painel e as **linhas de log** de diagnóstico (mantidas em inglês de propósito).

## 1.5.4
- Hunting: with a spear to throw at a deer (and another weapon in its pack), a companion sneaks only to throwing distance, stops and throws, instead of walking right up and stabbing it. New setting Hunting > ThrowStandoff (10 m; it throws from anywhere within ThrowRange, 12 m).
- Hunting: a companion with a spear as its only weapon throws it too once a deer (or other timid game) has noticed it, then lets the animal go and fetches the spear before hunting on.
- Hunting: a companion waits for the stamina a throw needs instead of closing in to stab, and the log says why it isn't throwing yet.
- Hunting: sneaking costs stamina like a player's, so a companion sneaking up on prey stops crouched to get its breath back when low (Hunting > SneakRestStamina 30%, carries on at SneakResumeStamina 60%).
- Hunting: a companion walks quietly (no running) up to prey that hasn't noticed it and only crouches within 20 m of it (Hunting > SneakFrom), like a player, instead of sneaking all the way.
- Hunting: if a much closer animal turns up before a companion has struck at its prey (closer by at least 5 m and under 60% of the distance - Hunting > SwitchCloserBy and SwitchShare), it goes for that one instead.
- Sneaking costs less stamina as a companion's Sneak skill goes up, the same way it does for a player (up to 75% less).
- Fixed: a companion that took a while sneaking up on prey (resting on the way) gave up on it after a minute without attacking, ignored that animal for a minute and went after one further away. Hunting now allows 3 minutes, and the log says when and why a target is given up.
- Hunting: while sneaking a companion wades shallow water and walks round instead of jumping, which stood it up, made noise and spent stamina (it only hops a step it's really stuck on).
- Hunting: chasing prey that bolted, a companion only runs while it has stamina to spare and jogs otherwise (Hunting > ChaseRunStamina 30%), so it doesn't reach the prey with an empty stamina bar.
- Fixed: ordering a companion to attack a deer (or other harmless game) did nothing - it dropped it straight away as "not a threat". The order now starts a hunt: it sneaks up and throws a spear like on the Hunt job.
- Hunting: with Follow on, a companion looks for prey up to 40 m from you (was 25 m - Hunting > FollowRange), closer to what you can see yourself.
- Melee: companions swing once they are a little inside their weapon's reach (Advanced > MeleeReachMargin, 0.2 m), not right at its edge where hits are hit-or-miss (user test: a stab at an unaware deer from the very edge of the spear's reach missed).
- Melee: companions aim a swing or stab at the middle of the target instead of its top (it went over a low animal's back), step back before swinging when standing right on top of it (Advanced > MeleeTooClose 0.3 m), and only stab once they face the target squarely.
- Log (debug): every melee swing, shot and spear throw is logged with distance, height and facing, followed by whether it hit (and for how much) or missed - to track down missed attacks.
- Fixed: after switching the job (e.g. turning Hunt off) a companion kept walking back to a chest for a tool the new job doesn't use, even after you called it back.
- Tools: take a tool out of a companion's pack (e.g. its bow) and it works without it for this job instead of going home for another one. Picking the job again lets it fetch tools again.

## 1.5.3
- Ladders: a tower of short ladders with landings between is now planned as one climb - the companion works out the whole chain at the start and goes from each ladder's top straight to the next one's foot without stopping. Chains that worked are remembered for a while, so after a fall (or the next time) it gets straight back on.
- Fixed: after loading in, a companion with Follow off followed you anyway (the return-to-you step on load switched follow on and never switched it back properly). It now leaves companions with Follow off alone.
- Ladders: a companion walks into a ladder and lets it lift it, like a player, and only hops when that doesn't work; it finishes each ladder on the landing before turning for the next one (it used to turn early and walk off the side).
- Ladders: a companion lines up square in front of each ladder first (like a doorway), walks straight into it and is bumped up it every half second just like you; only when it's really stuck does it hop.
- Fixed: going up a tower of ladders, a companion could walk or hop off the open side of a landing (a 9 m fall). The usual edge check now applies on ladder landings, and it never hops towards an edge.
- Fixed: a companion with nothing to do walked off - back towards where it was first spawned or last brought back, or to its home point up to 60 m away - even right after you sent it somewhere. It now waits and idles where it is, and around home only when it's at home.
- Quieter log: a cornered, fleeing companion logs once every few seconds instead of every moment.
- Ladders: companions walk up ladders instead of running, and when low on stamina they get their breath back on a landing before the next ladder (never part way up).
- Fixed: a companion with a melee weapon swung at an enemy a floor above it (hitting the ladder in between) instead of climbing up to it. Melee range now counts height, and on its way up a ladder it finishes the climb before it fights.
- Fixed: at the top of a ladder a companion could carry on walking the ladder's way and out through an opening in a tower wall (a 10 m fall). It now stops at the ladder's top end or the edge of the landing and turns for the next ladder.
- Fixed: after falling off a chain of ladders a companion couldn't find the first ladder again ("no ladder found" right beside it).
- Fixed: following your path up a tower, a companion could jump up onto a wall top beside the path and fall off the other side.
- Ladders: going down a tower of ladders works the same way - planned once, then down each ladder from its top to its foot and straight on to the next, instead of stopping short of each bottom.
- Companions recognise ladders that are part of the world (a ruined tower's), not only ones you built, so they can climb to an enemy at the top.
- Quieter log: the weapon-choice lines during fights are now debug-only.
- Fixed: at the top of a ladder a companion leapt onto the floor next to it and could carry straight on over a small floor and off the far side. It now steps off onto a floor that is level and close, looks first the way the ladder faces, and only takes spots with real footing.
- Fixed: on a sloping ladder a companion walked on past its top end while making sure the climb was over, ending up off to the side of the next ladder. It now stops there, and checks for a drop only as far as the next ladder's foot.
- Ladders: at the top of one ladder a companion that still has further to climb goes straight for the next ladder instead of stepping off first, and walks round a small opening to reach it (only a drop of over 4 m stops it).
- Ladders: a companion walks all the way to the foot of a ladder before pushing into it, instead of pushing in from beside it and walking along its edge; a narrow opening on the way no longer makes it give the ladder up.
- Ladders: walking to the next ladder a companion keeps to the floor - if the straight line would cut across the open side of a narrow floor, it goes round the corner instead, and only gives the ladder up when there is no floor all the way.
- Ladders: floor and edge checks look at a foot-sized area, so the gaps between a stair-like ladder's rungs no longer read as an edge or "no floor".
- Ladders: a companion pressed against a wall beside a ladder stops trying to get closer after a moment and climbs on from where it is.
- Fixed: at the top of a ladder a companion already at your height could jump up onto a wall top and over the side (an 11 m fall). It only jumps up when you're higher, and never onto something with a harmful drop just past it.
- Ladders: smoother climbing up a chain of ladders - shorter pauses on each landing (it steps on to the next ladder sooner), and while it waits at the top it turns towards the next ladder.
- Fixed: a companion only looked for the next ladder when its target was more than 2 m up, so with an enemy exactly one floor up it stepped off instead and could fall. Now from 1.5 m.
- Fixed: after a fall a companion wouldn't use the ladders it had just climbed again ("no ladder found" right beside them).
- Follow: a companion standing right beside the path you took steps onto it there, instead of walking round to where you last were on its level (which could mean going round a hole).

## 1.5.2
- Fixed: hunting with a melee weapon (spear, axe, knife - no bow ready) - the companion stopped 20 m away and swung at the air. It now sneaks up and strikes.
- Hunters throw their spear at skittish game (deer, hares...) when they have another weapon in their pack, then go and pick it back up.
- New [Hunting] ThrowSpear (on) and ThrowRange (12 m).
- Fixed: a spear counted as a ranged weapon (its long AI range is the throw), so a companion stabbed at the air from far off. Melee weapons now close to their real reach.
- A companion's thrown spears, arrows and bolts lead a running target and aim a little high for the drop, like a player.
- Fixed: following you, a companion could jump a stream and straight back when your path changed; it now wades or goes round instead of jumping back within a few seconds.
- Fixed: a creature it can't reach in deep water (a crocodile watching from the river) made a companion stand still, starting and dropping the fight over and over. It now leaves it unless it's attacked.
- Fixed: arrows were picked as a "bow" when the companion had arrows but no bow.
- Fixed: a companion out hunting far from home gave up its bed because the bed wasn't loaded; it only looks for its bed when it's nearby.
- Companions climb wooden ladders (push into them and hop up, like a player) to follow you up a tower or reach something on a platform, and jump from the top onto a ledge (or up onto a wall top and on to the floor) when the ladder stops short.
- Ladders stacked on top of each other are climbed as one tall ladder, all the way to the top.
- Companions jump onto a ladder whose bottom is off the ground, or when walking into it doesn't lift them, like a player, and try another way after 4 jumps that don't get them on.
- Ladder choice: companions pick a ladder whose foot is on their level and that they can walk to, and whose top gets them closest to where they are going; they climb a chain of short ladders one after another and leave a ladder alone for a while if it keeps getting them nowhere.
- Ladders: a ladder foot close by is walked to straight, without stepping off the edge of a small floor; a companion that falls off on the way looks again from where it lands instead of jumping at the ladder from below.
- Companions stop at the edge of a drop that would hurt (over 3 m, not into deep water) instead of walking off it, whether following, fighting or working, and look for a ladder or a safe way down instead.
- Gap jumps over a drop that would hurt need room to spare (80% of the companion's jump), and a companion never steps off the top of a ladder over such a drop.
- Follow: when you go up or down somewhere the companion can't find its own way (a narrow tower of ladders), it follows the exact path you took, ladders and jumps included - except a drop that would cost it more than 30% of its health.
- Follow: a companion on another floor from you keeps working its way to you (ladders included) instead of counting as "close enough" and idling, and never stops in the middle of a climb.
- Companions no longer try to jump onto a ladder from under a floor: a ladder that starts from a floor above them is climbed from that floor once they get up there.
- Ladders: companions walk round to the foot of a ladder instead of into the wall beside it, and skip a ladder that didn't get them up for a few minutes.
- Companions take fall damage like a player (none up to 4 m; Feather Fall works for them too).
- Companions go back down the ladder they came up. Only with no other way down do they jump down a drop: up to 8 m and never one costing more than 30% of their health. New [Movement] StuckDrop.
- When stuck just below where it needs to be, a companion jumps up onto something it can stand on nearby, like a player; it no longer steps backwards off a high ledge when stuck.
- Companions can use the game's use-to-climb ladders to reach something up out of reach.

## 1.5.1
- Fixed: a companion that died while you were dead too (for example both drowning at sea) left no tombstone and never came back - it now leaves its tombstone and respawns as normal, wherever you respawn.
- Fixed: a companion whose respawn time came while its tombstone was far away could be respawned again and again (one every second) - it now comes back once.
- README and guide: on a dedicated server the server needs the mod too - a server without it deletes companions.
- New companion roster: your game saves each companion's gear and settings every minute and when you log out or quit, per world.
- New [General] ShowCompanionRoster: lists your companions as Alive, Dead, Lost or Unknown.
- New [General] RecoverLostCompanion: brings a Lost companion back next to you (an Unknown one asks you to confirm).
- A recovered companion's old copy is removed if it turns up later, and its old tombstone no longer respawns it.
- New [Advanced] RosterSaveInterval (60 s): how often the roster is saved.
- New Odin's Shrine (hammer, Misc, at a workbench: 10 stone, 5 wood, 3 greydwarf eyes): shows your companions and their status in the game's message window, and offers to bring back a lost one.
- Fixed: a companion looked for a free bed about every 30 minutes instead of every 30 seconds (at night it still looked every few seconds).
- Fixed: a companion kept saying it had no bed after it had found one, until the next night.
- A bed claimed by a companion that no longer exists is free again (single player and host).
- The log now says why a companion couldn't find a bed to claim.
- Fixed: a companion could lose its bed when you loaded in, if the bed hadn't appeared yet - it now waits up to a minute for it.
- A companion can use a bed of its owner's that isn't the owner's spawn point, so clicking its bed by accident no longer takes it away. Once you make it your spawn point, the companion gives it up straight away.
- New [Death] ShrineBuildable, ShrineRecipe and ShrineModel (Tombstone or Ward): turn the shrine off, change its cost or its look.

## 1.5.0
- Major release: everything from 1.4.86 to 1.4.156 below (new movement, idle behaviour, Repair and Restock, cooking, gathering like Hunt, orders first, multiplayer ownership and chest safety, cleaned-up settings), plus the new Companion Guide (GUIDE.md) and Settings Reference (CONFIGURATION.md).

## 1.4.156
- New [Cooking] MakeMead (on by default): off = the cook makes food only - no mead bases and no filling fermenters.

## 1.4.155
- New [Combat] EngageOnNotice (on by default): companions attack a creature as soon as it has noticed you or them, as before 1.4.154. Off = they wait until it's actually coming for you.

## 1.4.154
- Companions no longer open fire on a creature that has only noticed you - only once it's actually coming for you or them (anything that attacks is still fought).
- After a kill, a companion that finds no body and no drops yet waits a few seconds longer for them to come out instead of giving up straight away.

## 1.4.153
- A full gathering companion away from home still unloads into a nearby cart or ship (as before); it now also skips one someone has open on another player's game, or one behind a ward it can't open.

## 1.4.152
- Multiplayer: a companion run by another player's game still treats you as its player - it follows you, sleeps when you sleep and sits when you sit, not whoever's game is running it.
- Multiplayer: orders you give your companion always take effect (they were lost when another player's game was running it).
- Multiplayer: companions no longer lose or duplicate items when they use chests or pick things up.
- Companions don't use chests someone has open on another player's game, private chests other players built, chests behind a ward they can't open, or carts and ship holds.
- Companions respect wards for fermenters, beehives and planting, like a player.
- Crops and wood piles a companion makes are credited to its own player; portal trips with another player's game running the companion now stick.
- Smelt and Cook also use the chests around home, so they no longer take down the wood piles Repair and Restock just built.
- Companions only pick up the honey or pile drops from the hive or pile they emptied themselves.
- Wood, Stone and Ore with Follow on now default to 10 m, like Forage ([Gathering] FollowRange; an existing config keeps its own number).

## 1.4.151
- Forage with Follow on now only picks plants within 10 m of you (new [Gathering] ForageFollowRange) - the berries you happen to walk past, not everything around. Wood, Stone and Ore keep FollowRange (20 m).

## 1.4.150
- Fixed: a patch meant only for companions stopped every creature's attacks from leaving effects on the ground.
- Fixed: Smelt could put ore or fuel into a smelter that was no longer in its pack (duplicating it).
- Fixed: waking up, or getting up from the fire, made a worker whose job turned Follow off follow you out of the base.
- Fixed: a hunter walking home with its catch barely fought back when attacked on the way.
- Fixed: the open-door order couldn't be replaced by a new order and ran during fights and loot pickup.
- Fixed: a gathering companion going over its carry weight in a fight walked to a chest with its back to the enemy.
- Fixed: the walk to the stone pile and the step aside from a falling tree ignored orders and loot pickup.
- Fixed: after switching away from Fish between casts the companion kept the rod in hand and stopped equipping new gear.
- Fixed: a second companion could sit down in the chair an idling companion was already sitting in.
- Fixed: a companion's tombstone didn't float in water or get moved back up if it fell through the ground.
- Fixed: Cancel and leaving a gathering job didn't turn Follow back on; setting Follow yourself is now remembered over the job's own switch.
- Fixed: jobs and idling kept going while seated on a ship, swimming for the shore, or sitting by a fire.
- Fixed: jobs took over a chest you had open (items changing in your chest window) and could clear its "in use" mark.
- Fixed: Smelt and Farm left a chest lid open when they were interrupted.
- Fixed: tool trips could start in the middle of an order; idle chatting could follow you away from the job.
- Fixed: an idle chore cut short kept its fuel in the pack; Farm could lose seeds from the chest with a nearly full pack.
- Fixed: two farmers in one field could plant on the same spot; the bow could flicker while stalking a bird.
- Fixed: after a death and respawn, turning the job off didn't turn Follow back on.
- The "waiting for companions" sleep message only shows on the host, where companions actually hold the night skip.
- The one-time config fix-ups (ForageItems "*", old ViewRange/ViewAngle) only run when upgrading an old config file.

## 1.4.149
- Config clean-up: settings regrouped into clear sections (General, Controls, Follow, Movement, Combat, Food, Stamina, Rest, Death, Looting, GearRepair, one per job, Idle, Speech, Advanced).
- Settings that moved or were renamed keep the values you set - they're carried over automatically the first time you load in.
- Removed 22 settings that did nothing (old combat, AI and Stay Home settings).
- Old leftover lines are cleared out of the config file (removed settings, the old Homestead and Door sections).
- Every setting's description rewritten in plain words, with units.
- Low-level tuning (scan intervals, use distances, tick timings) moved to a new [Advanced] section.
- Movement ViewRange and ViewAngle now actually work (defaults 20 m and 120 degrees - what companions always used).

## 1.4.148
- Fixed: a companion sitting on a ship flooded the log with "Setting linear velocity of a kinematic body is not supported" warnings - ship seats now work like chairs and beds.

## 1.4.147
- Fixed: a running job could pull a companion away from boarding a ship, opening a door, or sitting by a fire / going to bed when you told it to - every job now waits for these orders too.
- Repair and Restock and the idle behaviour wait for all your orders, not just "go there".

## 1.4.146
- Fixed: ordering a companion to repair at a station was cancelled straight away while its job had a task - your order now goes first and the job waits for it.
- The repair log now says why a repair trip stopped (combat, loot, another order, being talked to, the job has work).

## 1.4.145
- Fixed: a "go there" order (or hitching a cart, putting things in a chest, fetching a tombstone) and a running job steered at the same time - every job now waits until the order is done.
- Fixed: a gear repair trip could run alongside picking up loot after a fight - it now waits too.
- A worn item no station nearby can repair is left alone for 10 minutes before it looks again (new [Repair] NoStationWait).
- A gear repair trip that can't find a way to the station, or takes over 2 minutes, gives up; that item then waits the same 10 minutes.

## 1.4.144
- Fixed: with Cook on, a gear repair trip and the cook's task walked at the same time and the companion stood still - the repair trip now waits while Cook, Farm, Hunt or Fish has a task.
- Fixed: an item it can't repair nearby (an arbalest needing the black forge) sent it to the forge over and over to top up nearly-new gear - only the worn items now decide the trip.

## 1.4.143
- New idle behaviour: a companion with nothing to do no longer just stands there - after 10 s it idles near its job until work turns up again.
- Idle chores: repairs damaged pieces, tops up fires, collects honey and puts away what it carries nearby.
- Idle rest: sits on a free chair or bench nearby, or on the ground, for a while.
- Idle wander: strolls about and looks over the stations and chests near its job.
- Idle company: walks over to you or another companion nearby and chats now and then.
- Now and then says why it's idle ("Nothing left to chop here.", "Waiting for the crops to grow.").
- New [Idle] settings: Enabled, StartAfter, Radius, Chores, ChoreRadius, Sit, Wander, Socialize, SpeechInterval.

## 1.4.142
- Code clean-up, no change in behaviour: the build has no warnings any more (old Unity names updated, unused leftovers removed).

## 1.4.141
- Movement clean-up, part 2: the old steering and "unstuck" code is gone - no more stray jumps while a companion stands working (it jumped after standing still for 2 seconds with any job on), and no more teleporting to you after five failed unstuck tries during a fight. The new movement handles all of it.
- Swimming for the shore when low on stamina now uses the new movement (the old way had stopped working).
- The "open this door" command now walks up and opens the door with the new movement; the old door helper and its [Door] settings are gone (doors on the way were already handled by the new movement).

## 1.4.140
- Companions line up with a doorway before going through it, like a player: when their way leads through a door they first step onto the line straight out from the middle of the doorway, then walk straight through. They no longer cut the corner and walk into the leaf of an open door.

## 1.4.139
- Shorter stop at doors: after closing a door that was open towards it, the companion tries to open it again every moment instead of waiting, so it goes through as soon as the door has finished swinging shut.

## 1.4.138
- The open-door fix sometimes didn't find the door at all: inside a house there are so many walls and beams close by that the door search ran out of room before reaching the door. It now looks much further through the list, and an open door is closed after about a second of being held up next to it instead of three.

## 1.4.137
- Still stuck at the open door: the door's leaf wasn't straight ahead, so the look-ahead missed it. When a companion gets stuck it now closes any open door right next to it (and reopens it from its side if that's the way).

## 1.4.136
- Companions got stuck for a moment on a door that was already open towards them (opened from the other side). Like a player, they now close it and, if their way is through it, open it again from their side so it swings away.

## 1.4.135
- Farm paused for a few seconds while planting: it had walked as close to the plant spot as it needed to, but Farm measured the distance differently (straight-line, slope included) and kept waiting. Farm, Smelt and Cook now accept "arrived" the same way the movement does.

## 1.4.134
- Movement clean-up, part 1: Farm, Smelt and Cook no longer have their own "stuck" timers - they give a trip up only when the movement itself gives up (or after 2 minutes). Farm's old timer counted waiting at a door or for stamina as being stuck.
- The last pieces of the retired Homestead / Stay Home code are gone (patrolling home, teleporting home, the home-zone row on the companion panel).

## 1.4.133
- Gear repair: at home it walked to the workbench every time a tool dropped to 99% (the hammer, after each building repair). It now waits until gear is below [Repair] HomeThreshold (80%).
- Gear repair only uses a station a player could use right now - under a roof if it needs one, with a fire if it needs one.
- Repair and Restock: sending the companion somewhere with the command no longer fights the job. It goes, waits there [RepairAndRestock] OrderWait (30 s), then goes back to its chores.
- Repair and Restock now empties beehives (once half full, [RepairAndRestock] HoneyAt) and puts the honey away in the chest that already holds the most honey ([RepairAndRestock] Honey).

## 1.4.132
- Repair and Restock no longer puts a pile on top of another pile - piles can't carry each other and the new one broke. Piles only go beside each other now, on the ground or floor.

## 1.4.131
- Cook job keeps its heat going: the fires under the cooking spots it uses (spit, iron cooking station, cauldron) and the oven are kept at [Cooking] FuelLevel (50%) - when one drops below half of that it's topped back up, one piece at a time. Fuel from its pack or the chests, or a wood pile taken down with a hammer ([Cooking] UsePiles).
- When cooking in the oven it now fills it to FuelLevel instead of a flat 3 pieces.
- (Repair and Restock only fuels fires, torches, braziers and hot tubs - never kilns, smelters or the oven.)

## 1.4.130
- Smelt job: when the chests have no wood for the kiln (or no fuel for a smelter), it takes down a pile of it with a hammer, like a player, picks up the material and uses that. Needs a hammer in its pack; [Smelting] UsePiles turns it off.

## 1.4.129
- Piles are a resource now: when the chests drop below PileKeep, Repair and Restock takes a pile down with the hammer like a player (top of a stack first), picks up the material and puts it back in the chests.
- Smarter pile placement: it scores every free spot beside or on top of a pile and picks the best. It won't narrow a walkway, and won't build by a door, portal, bed, chair, crafting station, cooking spot, fermenter, beehive, chest or a smelter/kiln's ports (its back is fine). It prefers spots backed by a wall or another pile.

## 1.4.128
- After a fight on Forage (or Wood, Stone, Ore, Smelt, Farm, Cook) the companion stuttered: it tried to walk to its next job target and to what the kill dropped at the same time. It now picks up the drops first, then goes back to the job.

## 1.4.127
- Wood, stone and other materials in piles near home now count toward the Wood/Stone/Ore jobs' limits (WoodLimit, StoneLimit and per-item amounts), like what's in the chests.

## 1.4.126
- Repair and Restock now stacks wood, stone and similar materials from the chests into piles (Stack of Wood, Stone Pile, Fine Wood Stack...), built with a hammer like a player. It only adds to piles of the same kind you already built, beside them or on top, and leaves [RepairAndRestock] PileKeep (100) of each in the chests.
- New settings: Piles, PileKeep, PileItems (* / Name / -Name / Name:Number to keep a different amount).

## 1.4.125
- Repair and Restock: when the companion's own gear needed repairing (its hammer, worn from the repairs) it got stuck between walking to the workbench and walking to the next piece. Gear repair now goes first and the job carries on afterwards.

## 1.4.124
- Repair and Restock: the companion took its hammer out and then just stood there - it kept checking for the hammer as a weapon, which the game doesn't count it as. It now swings.
- It also stood next to a pole set under the floor doing nothing: the floor touching the pole counted as "in the way". It now looks at several points on a piece, ignores what's touching it, and if it still can't reach a piece (or fire) from where it stands it moves on after 3 seconds and says why in the log.

## 1.4.123
- Picking up branches, stones and flint is now early-game only: it stops once there's a bronze-or-better axe in the companion's pack, your pack or the chests near home, or a forge at home (it used to depend only on the companion's own axe).

## 1.4.122
- Wood and Stone jobs: a companion with only a stone or flint axe, or no axe at all, also picks up what's lying on the ground - branches on the Wood job, loose stones and flint on the Stone job (on the job's list and under its amount). Whichever is closer, a tree/rock or something on the ground, comes first.
- With no tool for the job (no axe for Wood, no pickaxe for Stone) it only picks things up, instead of standing around.
- New [Harvest] PickUpLoose setting (on by default) to turn this off.

## 1.4.121
- Wood, Stone, Ore and Forage with Follow off now work an area like Hunt: around home when started near home, otherwise around where they were started (new [Harvest] Range, 40 m), and keep going when you walk away instead of stopping 40 m from you.
- With nothing left to gather they walk back to the middle of that area.

## 1.4.120
- Wood, Stone, Ore and Forage work like Hunt: picking one no longer turns Follow off, and picking Follow no longer turns them off.
- With Follow on they only go for trees, rocks and plants within [Harvest] FollowRange (20 m) of you, and otherwise follow; if you move on, they leave what they were working on and catch up.
- Gathering companions that follow you put their load away when you get home, like a hunter.

## 1.4.119
- Cook: each cooking appliance can be switched off on its own - Cauldron, PrepTable, MeadKetill, CookingStation, IronCookingStation, Oven, Fermenter, Beehives and OtherStations (modded ones).
- Repair and Restock now sorts chests wherever it was started, not only at home: started near home it works around home, anywhere else within AwayRadius of where it was started (and goes back there when idle, instead of walking home).

## 1.4.118
- Repair and Restock now leaves pieces above 75% health alone by default (RepairBelow 0.75, was 1 = any damage).
- Each kind of fire can be left alone: RefuelFires, RefuelHearths, RefuelBraziers, RefuelStandingTorches, RefuelWallTorches, RefuelHotTubs and RefuelOther (lanterns, modded fires and the like).

## 1.4.117
- Repair, Restock and Homestead were three wheel jobs that didn't really do the work; they're now one job, "Repair and Restock", that walks over and does each task the way a player would.
- Repair: with a hammer in hand it walks up to each damaged building piece near home and repairs it with a swing (needs the piece's workbench in range and ward access, and costs stamina and hammer wear like a player). If it has no hammer it fetches one from a chest.
- Restock: fires, torches and braziers low on fuel are topped up one piece at a time, with fuel from its pack or fetched from a chest; any fuel left over goes back in a chest.
- New chest sorting: each kind of item is carried to the chest near home that already holds the most of it, and part-stacks are merged (needs a home set; only stackable items unless SortGear is on).
- New [RepairAndRestock] settings: Repair, RepairBelow, Reach, Restock, FuelBelow, Sort, SortItems (* / Name / -Name), SortGear, AwayRadius, ScanInterval, WorkTime, GiveUp.
- The Restock and Homestead wheel slots are now "Pending" and do nothing when clicked; companions saved on Restock switch to Repair and Restock, and Homestead is turned off (Wander still lets them move around home).

## 1.4.116
- After each catch the fisher casts at the closest fish it wants from where it stands, instead of the one it first picked further out.
- The first cast of a session no longer goes twice as far as needed (it starts from a full cast reaching about 24 m and keeps learning).

## 1.4.115
- The fisher may stand on a rock again, like a player, as long as there's something to stand on.
- If it falls off its spot it gets back on; if it falls into the water with a fish on the line, the fish is lost; and if it falls off the same spot twice it fishes from somewhere else for a while.

## 1.4.114
- The fisher may stand in shallow water (up to about waist deep) to fish, but prefers dry land.
- It only fishes from firm footing: level ground at least half a meter all round, so no more hopping onto a small rock and falling off when it casts.
- It prefers fish it can reach with a shorter cast.

## 1.4.113
- Fixed the fisher finding almost nowhere to fish from (1.4.112 was too strict on beaches): any dry ground or dock with water within 5 m counts again, as long as the water is at least a meter away.

## 1.4.112
- Fixed companions getting stuck floating in the water: while swimming they could get stuck steering at a point right under them and never try to get out.
- The fisher never fishes from the water: if it slips in, it reels in and climbs back out to its spot first.
- Fishing spots are at least a meter back from the water's edge, so it doesn't slip in while fishing.

## 1.4.111
- The fisher gets its breath back before each cast: it waits until its stamina is nearly full, so it doesn't start reeling a fish on an empty bar.
- While fishing, companions eat their most stamina-rich food first, and near home they fetch a few stamina foods from the chests if they have none (and put back what's left after the job).
- The running dive into water only happens for a real swim across, not to reach something just off the bank or to get round a fence end.

## 1.4.110
- Companions prefer staying dry like a player: when the way somewhere crosses water they go round on land unless that's a long way in another direction, and otherwise swim (General > DryDetour).
- Jobs (fishing spots, prey, birds, kill drops) accept a short swim to get somewhere and only give up on a long one (General > JobSwim, 20 m). Drops floating in deep water are still left.
- Companions take a running dive into the water whenever their route really does swim, not only while following you.

## 1.4.109
- Fixed companions counting as having stamina at zero, so a fisher out of stamina still landed its fish (a player loses it). This also applies to anything else that needs stamina.
- The fisher fishes for fish up to 20 m out (its full cast reaches about 24 m), so it finds somewhere to fish much sooner.
- Companions only take a running dive into deep water while following you, not on the way to a job.
- Less log spam from the small stamina drains while reeling.

## 1.4.108
- Fixed the fisher's float never coming out: the game's float setup crashed for companions every time the cast hit the water (and kept spawning floats).
- The fisher finds places to fish from much faster and more reliably: it maps the shore and docks around it that are right next to deep water, and picks the one nearest a fish it wants.
- It casts no further than about 14 m (fish come to the float from 10 m around it), and picks another fish when the one it was after has swum off.

## 1.4.107
- Fixed the fisher never finding a spot to fish from on a dock or a high bank: it looked for ground from below the dock, so it only saw seabed. Docks, banks and walls up to 10 m above the water count now, and it fishes from where it stands when a fish is within a cast.

## 1.4.106
- Fish redone: the companion fishes for real like a player, with the game's own float and the fish actually in the water. Fish come to the float, bite, get hooked when it strikes in time, fight, and can get away or break the line.
- It stays around where you started the job, looks for fish within Fishing > ScanRadius, walks to the bank by the one it wants and uses the bait that fish takes best.
- Reeling uses stamina and raises the Fishing skill, and a higher skill reels faster and cheaper, as for a player. Bait is only used up when something takes it.
- New Fishing > Fish list and FishTarget (100 of each, pack + chests near home). Near home it takes the catch home when the pack is full, then goes back to fishing. Fish names are in OfflineCompanionsReborn.Fishing.txt.
- The old simulated fishing settings (WaitTime, ReelTime, HookChance, MissChance, ReelStaminaDrain) are gone; new ones: CastWait, BagFull.
- Every list setting now works the same way: * = everything, Name, -Name = never this one, Name:Number = this one with its own amount. That's Foods, Prey, Fish, PlantAny, ForageItems, GatherWood, GatherStone and GatherOre.
- New amounts: Name:Number in Foods sets that food's target, in Prey that creature's (or meat's) target, in ForageItems stops picking that item at that many, in PlantAny stops planting that crop at that many.

## 1.4.105
- Companions get up in the bed like a player when they wake, then step down beside it (they were playing the get-up on the floor).

## 1.4.104
- Move here, put items in a chest, Repair buildings, Restock, going back to a tombstone and healers going to someone hurt now use the companion movement: routes, doors, stairs, climbing out of pits and keeping out of deep water. A move order it can't find a way to is dropped (the log says so).
- Chasing enemies in a fight uses the companion movement too, falling back to the game's own only when it can't find a way.
- The last steps up to a tree or rock, dodging a falling tree, the door handler and following a non-player use it as well.

## 1.4.103
- Hunters chase prey the way companions follow you: proper routes, climbing out of pits, no jumping into water. Prey they'd have to swim to, or can't find a way to, is left alone.
- With a bow or crossbow, prey down in a moat or pit is shot from the edge instead of followed down.
- A companion far from you and out of sight no longer warps to you while it's still making its way there; it walks.

## 1.4.102
- Companions no longer go straight back to bed right after you load in.
- Companions get up beside the bed onto the floor, instead of on the bed (where they slid off the end).
- Bird hunting: shoots from up to 30 m and jogs into range, since birds only stay down a few seconds; never swims after a bird.

## 1.4.101
- Fixed companions getting up from a chair into the hearth (or onto a table) in front of it and getting stuck inside it: they now step down onto open floor beside the chair.
- A companion still in a fire after 3 seconds (stuck in it) is moved to the nearest safe spot.

## 1.4.100
- Hunters shoot birds for their feathers: when a crow, gull or modded bird lands within range, they sneak up and shoot it with a bow or crossbow, then pick up the drops (Hunting > Birds, FeatherTarget).
- Fixed a companion getting stuck standing in a hearth: it now looks further for a way out, jumps out like a player, and after 6 seconds steps onto the nearest safe spot.
- When a command reaches no companion, the log now says why (too far away for RecallRange, Command switched off, or not yours).

## 1.4.99
- Jumps don't have to be perfect: a companion jumps across water when it can land somewhere shallow enough to stand, then jumps again, like a player. It keeps a quarter of its stamina back for these.
- Shortcuts through water it can wade are no longer ruled out, and gaps are judged a little less strictly.

## 1.4.98
- Companions jump like a player: their own run speed (with mead, wet, cold and other speed effects) carries them across, and a higher Jump skill jumps further.
- Jumping now costs companions stamina like it does a player, and with too little stamina for a full jump they don't try a stream or gap they can't clear; they wade or go round.
- Streams and gaps are only jumped when the companion can actually make it from where it is (width, landing height, speed, skill and stamina all count).

## 1.4.97
- Companions no longer swim out for a kill's drops or follow a body floating into deep water; they wait on the bank and leave what's out of reach.
- Fixed companions staying crouched (sneaking) while swimming.
- Shortcuts are more like a player's: when the straight way is much shorter, companions drop off ledges up to 3 m and leap gaps up to 1.5 m instead of going down the stairs and round (General > MaxDrop, JumpGap).
- Cooks no longer eat the honey they collect for the kitchen; it's put away with the rest.

## 1.4.96
- Companions sneak up on deer, hares and other skittish game that hasn't noticed anyone yet: crouched and not running, so it doesn't bolt (Hunting > Sneak).
- A crouching companion is now really sneaking, like a player: quiet footsteps and harder to spot (light and Sneak skill count). Before, it only looked like it.
- Companions jump narrow shallow streams and puddles too, not only water deep enough to swim, and wade through wider shallow water (General > JumpWaterDepth, JumpWaterWidth).
- Fixed a companion missing the drops of its first kill when it fought something else straight after: fights no longer use up its looking time, it follows a body that slides or floats off, and it looks further around once it gets there.

## 1.4.95
- Fixed companions giving up on a kill's drops about a second after setting off, before reaching the body ("nothing dropped").
- Companions wade into shallow water for a kill's drops and follow drops that float off, but don't swim out for them.
- Cooks walk to each honey after harvesting a hive, so honey that rolls off behind a hive on a slope gets picked up too.
- Wet, Cold, meads and other status effects now change a companion's health and stamina regeneration like a player's (General > StatusEffectsOnRegen).

## 1.4.94
- Hunt works with Follow on: the companion goes after game near you (FollowRange, 25 m) and keeps up with you when there's nothing close.
- Hunt with Follow off hunts around where it is: home if it's near home, otherwise where the job was started (Range, 40 m), and walks back there between hunts.
- Hunt goes after anything that drops meat, modded creatures included; never tamed animals, bosses or companions. New [Hunting] Prey list (* = all, -Name to exclude); the names are in OfflineCompanionsReborn.Hunting.txt.
- Hunt stops on a creature once you have MeatTarget (100) of each meat it drops, counting the pack and the chests near home. Leather and hides are picked up as a bonus.
- A hunter that follows you keeps its catch until you're home, then puts it away. With Follow off near home it walks home to unload when its pack is full.
- Hunt uses a bow first and melee second, and fights anything that attacks it the normal way (blocking and parrying included) before going back to its prey.
- Hunt no longer stops when there's no bow or arrows away from home; it hunts with its melee weapon.
- After any kill, a companion goes over and picks up what the creature dropped, waiting for drops that come out of the body. New [Looting] settings; needs Auto Pickup on.

## 1.4.93
- Cooks collect honey from beehives around home (within HomeTripRange, 60 m by default; at a camp, within the cooking range): from a full hive, right away when a mead base is waiting on honey, and from any half-full hive when there's nothing else to cook. The honey gets put away with the rest.
- Fixed the "Setting linear velocity of a kinematic body is not supported" warning while a companion sleeps in a bed.

## 1.4.92
- Reviewed mead plan: after each boss, cooks brew the mead bases you'll want for the next one first (Eikthyr through Fader, plus Kall Fimbulbringer). The list sets the order, it doesn't limit it: other mead bases you know are still brewed after food, within the same targets.
- Bosses are read from the game itself, so the MeadFocus setting works with new and modded bosses too; OfflineCompanionsReborn.Cooking.txt lists them.
- The log now says why a cook isn't brewing a priority mead base (enough already, recipe not known, no usable station, or an ingredient at its reserve).

## 1.4.91
- Fixed cooks never actually putting mead bases into fermenters (the base disappeared from their bag and the fermenter stayed empty), and thinking a full fermenter was empty.
- Fixed companions trying to stand on top of a fermenter (or chest, station, bed) next to the one they're using. They now stand on the floor beside it.

## 1.4.90
- Fixed cooking (and other chest trips) not reaching any chest in a room with a fire. The "keep out of fires" check now only avoids the flames themselves, not the whole area around a fire.
- Companions carry their best parrying shield again instead of the tower shield. They still switch to the tower shield in a fight when they're hurt or outnumbered.
- Companions no longer put their gear back on piece by piece when you load in. They're already dressed, like you.

## 1.4.89
- Companions jump over a small fire in their way, like a player, and only walk round a big one. Fixed a cook getting stuck stepping in and out of a fire pit on the way to the cauldron.
- Fixed the cook trying the same recipe over and over when an ingredient is only in a chest it can't reach. Such chests no longer count, and a recipe that can't be made is left alone for a minute.
- Companions can use chests on ledges and shelves from the floor below: they look for a place to stand under an overhang too, and check whether they can see the chest from a little higher (like a player's camera).

## 1.4.88
- Fixed companions running back and forth under a chest up on a ledge (seen when emptying their bags after turning Cook off).
- Companions keep out of fires: they don't pick a spot in one, walk round one in their way, and step straight out if they end up in one (seen in a tight kitchen, standing in the fire pit).
- Companions reach chests like a player: anything within about 4 m of their eyes that they can see, so chests up on a ledge or a high shelf can be used from the floor below.

## 1.4.87
- Cooking rewritten. Companions now use every cooking station like a player: cauldron and food preparation table (with a fire and roof, like you need), cooking station, iron cooking station and oven (they put food on, take it off when it's done, and keep the oven fed with wood). Bread and pies go from the prep table to the oven.
- They make the best food they can from recipes you know, until there are 20 of it in the chests nearby, and never use a raw ingredient once 20 or fewer are left, so you always have some to cook with yourself. All amounts are in the new [Cooking] settings.
- Mead: they brew and ferment the meads you'll want for the next boss (the MeadFocus setting, by the last boss beaten), keeping 10 of each mead base and 20 of each finished mead, and tap fermenters when they're done.
- They take finished food off any cooking station nearby before it burns.
- Cooked food goes in a chest that already has it, else one with other cooked food, else the one nearest the cooking area. Food you gave them to eat stays with them.
- They cook in batches like a player (Cooking > BatchSize, 5 by default): several at the cauldron or prep table in one trip, several slots on a cooking station or oven at once.
- The [Cooking] Foods setting limits what they cook (* = everything, -Name to leave one out). OfflineCompanionsReborn.Cooking.txt in the config folder lists the names, mods included.

## 1.4.86
- Smelting now uses the same movement as farming and gathering: routes through doors and up stairs, the shared stuck handling, and a spot it can stand on by each chest and by the smelter's ore and fuel slots. Chests it can't get to are skipped.
- Companions walk up stairs normally again and only jump a step that stops them.
- Removed a false "hasn't moved" warning in the log while a companion stands and fights.

## 1.4.85
- Companions now find their way with their own map of the world. Closed doors don't block their way (they open each one as they reach it, however many there are), and they hop up stairs and steps the normal map doesn't connect. New setting General > CompanionPathing (on) turns it off if it causes trouble.
- Companions find a place to stand by chests in narrow spots more often (anywhere within reach, from more directions).

## 1.4.84
- Fixed the farmer never storing crops in chests up the stairs indoors. Companions now head for the bottom of stairs the game's pathing doesn't use and hop up them.
- Companions can find their way through several closed doors in a row (up to 3), not just one.
- Farmers plant in rows from the far edge of the zone back toward where they came in, instead of in a ring around themselves until they're boxed in by their own crops.
- Companions coming home with no job now put away what they picked up, like after turning a job off. This also happens after loading in at home.
- Companions hop over low things in the way (logs, low walls) instead of running around them.

## 1.4.83
- Fixed companions running a long way round (and warping) instead of hopping up steps or a low wall. When the way round is much longer, they now go straight and jump over it like a player.
- Following companions no longer warp to you while you can still see them, unless they're half as far again as FollowTeleportDistance.
- Fixed companions switching mid-fight to creatures that weren't after anyone (boars while exploring).
- Companions now carry their shield with a one-handed weapon all the time, not only once a fight starts.

## 1.4.82
- Fixed the farmer carrying crops to a chest after every few. It now picks everything ripe first and stores when nothing ripe is left or its pack is nearly full.
- Fixed the farmer planting a few seeds and then stopping.
- Fixed following companions taking a long way round (down past the beach) when you went a shorter way. They now follow your path.
- A standing companion stays put while you're within 2.5 m of it, so it's easy to walk up to.
- Fixed gatherers walking off ledges (into a moat) while stepping in to reach a tree or rock.
- A companion stuck in a pit (a dry moat) now climbs out like a player: it picks the lowest, least steep way up nearby, sprints at it and keeps jumping, resting when out of stamina. If that fails, it's moved back up to where it last had a way to its target.
- Companions now play the interact animation when opening a chest for farming, smelting and putting tools away.

## 1.4.81
- Fixed companions walking all the way home to empty their bags or put a tool back when you turn a job off out in the field. They now do it once they're back home.
- Fixed the Wood job equipping a crossbow and standing there. Bows and crossbows are never used as gathering tools.
- Fixed following companions swimming across a moat when you walked in through the gate. They now follow your path when their own way would mean a swim.
- When you set off, a companion that was standing still now waits 1.5 seconds before following, so it's easy to walk up to, with Wander on or off.
- Fixed setting a farm zone (modifier + Z) also recalling your companions and turning their job off.

## 1.4.80
- Fixed Stone and Ore companions hitting each rock once and moving on to the next. Many rocks turn into a cracked version on the first hit, and companions took that as the rock being gone. They now keep mining the cracked rock until it breaks.

## 1.4.79
- Fixed following companions running off to a gate on the other side of the base. When they can't get to their spot, they now follow your path instead.
- Fixed the farmer picking chests it can't reach (up on a ledge, behind a wall) and running around the building trying. It now only uses chests it can get to.
- Companions no longer go through a door unless it actually leads to where they're going.
- Fixed gatherers swinging at big rocks without reaching them. They now step in until the rock is in reach.
- Gatherers wait a moment for drops to land before moving on to the next rock.
- Companions only start fights with creatures that are going after you or them. Anything that attacks them is still fought.

## 1.4.78
- Fixed the farmer running back and forth through a gate on the way to a chest. Companions now walk to a spot by chests and crafting stations that they can actually reach.
- Fixed companions sidestepping and hopping around between crops while farming.
- Fixed gatherers swinging at a rock over and over without hitting it.
- Fixed companions picking fights with harmless animals (modded crabs and dolphins) and staying "in combat" with something far away, which stopped their job. They now let go of anything more than 40 m away.
- Picking a job now turns Follow off, so the companion stays at the work. Turning the job off turns Follow back on, and picking Follow turns the job off.
- Companions only repair barely-worn gear when they're actually at home (within 25 m), not anywhere within HomeTripRange.
- Smoother following when you walk close to a companion.

## 1.4.77
The third part of the new movement system starts: jobs. Farming and the Wood, Stone, Ore and Forage jobs now move the same way as following and trips.
- Farm and gathering companions find their way around walls, open doors and gates, handle water and get unstuck like they do on trips.
- Gatherers working their way around something to reach a tree or rock no longer give up on it too early. If they really can't get there, they pick another target.
- Companions wait a moment before following when you set off, and stay put while you're right next to them, so they're easy to grab.
- The Wander toggle now also controls following. On (now the default for every companion): moves around you and idles when you stop. Off: keeps one spot near you and stands still.
- Fixed thousands of log warnings while a companion sat on a chair.

## 1.4.76
The second part of the new movement system: following you.
- Companions follow like a player. Each picks its own spot beside, behind or sometimes ahead of you, and moves it now and then.
- Several companions spread out around you instead of lining up behind you.
- Following uses the new movement: doors, water, stream jumps and getting unstuck all work while following.
- In doorways, on stairs and in narrow paths, companions follow your path and spread out again when there's room.
- When you stop, companions wait where they are and idle nearby.
- After you've stood still for a while, companions come back and wait near you. New settings `Regroup` and `RegroupTime` (Follow, on, 45 s).
- Indoors, companions stay closer (`IndoorDistance`, Follow, default 4 m) and sit down on a free chair or the floor when you stop. Near home they empty their bags first. New setting `SitIndoors` (Follow, on).
- New setting `MaxDistance` (Follow, default 10 m). `FormationOffset` and `FormationCatchupDist` no longer do anything.

## 1.4.75
- Fixed companions walking back and forth while farming. With a job on, they now stand still between tasks instead of wandering back to their idle spot.
- The farmer now goes straight to the next ripe crop close by, like working down a row, instead of pausing and rescanning the field. New setting `NextCropRange` (Farm, default 3 m, 0 = off).
- Fixed companions getting stuck in doorways when no route could start from where they stood. They now step a meter or two to find one.
- Fixed doors being closed behind a companion that was still stuck next to them.
- Fixed a weapon check that never ran. Companions in Passive stance now hold their strongest weapon.
- Farm scans now write one line to the log instead of one per crop.
- Reborn no longer shows as incompatible with ProfMags' original mod. It still won't start if the original is installed, and the log says to remove it.
- The mod is now built from regular C# source code. It should play the same; if something seems different, please send your log.

## 1.4.74
The first part of a new movement system. Tool trips, emptying bags, repair trips and walking to bed now all move the same way. Following and jobs move over in later updates.
- Fixed companions on trips going the wrong way around walls, falling into moats and giving up after 20 seconds. They now follow the route corner by corner.
- Companions open closed doors and gates on their way, including from the bottom of short stairs, and close them again behind them.
- When stuck, companions step back to one side, jump toward the next turn of their route, try another way around, and only then give up.
- Companions in deep water swim to the bank nearest where they're going, and only one they can climb out on.
- Companions walk the last few steps, jog in between, and run when the destination is far away (`WorkRunDistance`).
- Fixed companions avoiding all water when low on stamina. Wading is always fine; they only avoid swimming if their stamina won't last, and otherwise wait on the bank to rest.
- Companions jump narrow streams to stay dry and take a running jump into water they're going to swim.
- Fixed companions ignoring targets in shallow water and chasing modded sea animals. They now follow targets into wading depth, and only into swimming depth if attacked and armed with a bow or crossbow.
- Fixed companions chasing animals that never attack (deer, and modded fish and dolphins). These are now left alone.
- Fixed crossbows never firing. Companions now reload them like a player.
- Fixed bows and crossbows counting as ready without the matching ammo.
- Fixed automatic repairs only finding a station within 20 m. Near home, any station near home counts.
- Fixed companions retrying a repair trip to a station they can't reach. They now look for another tool, or say it's broken and follow you.
- New setting `FollowStuckTeleport` (General, off by default): a following companion stuck out of sight for 15 seconds appears next to you.

## 1.4.73
A review of the Wood, Stone and Ore jobs turned up several places where companions didn't play by a player's rules.
- Fixed companions damaging a tree or rock they couldn't reach. They now pick a new target instead.
- Fixed every swing at a tree landing an extra hit. It's one hit per swing, and only when the swing connects.
- Fixed pickaxe swings that hit the ground counting as hits on the rock.
- Fixed companions being left with no weapon or shield after tool, fight and job switches. Their normal gear now comes back.
- One-handed tools no longer take the shield or torch off.
- A broken tool now comes off and the companion switches to its best weapon, then repairs or replaces the tool near home.
- Fixed swings wearing down a shield or torch.
- Fixed companions standing on stumps and logs and shuffling forward while swinging.
- Fixed overweight companions flickering in and out of their job. They now say they're too heavy, pause the job and check for somewhere to unload.
- Drop-off rules: chests only near home; away from home, also a nearby cart, ship or outpost chest. Never tombstones or other players' private chests.
- Stone from the Ore job goes in a pile a few meters from the deposit instead of being hauled home.
- Fixed large copper and tin deposits being abandoned partway. They're now mined until finished.
- After a tree falls, companions go to the nearest log, if your gather list and limits allow it.
- Companions collect drops from around a rock and pick items up from a player's reach (about 2 m).
- Fixed the Forage job skipping wild barley and flax.
- Companions turn toward their work instead of snapping to face it.

## 1.4.72
- Fixed companions taking off all their armor after their inventory reloaded. They now keep wearing their gear.

## 1.4.71
- Fixed companions being unable to open a door from the bottom of the stairs below it. They now open doors from anywhere in reach.
- The same in-reach rule applies to smelters, kilns, cauldrons, fermenters, fires, chairs, crafting stations, repairs and chests. Planting still needs them on the field.
- Fixed companions walking home from far away when a tool broke. Chest and repair trips only happen within `HomeTripRange` (General, default 60 m); further away they say so, pause the job and follow you.
- Near home, a broken job tool is repaired at a workbench or forge, or replaced from a chest.
- Near home, companions repair any wear on their gear, not just items below `DurabilityThreshold`.
- Gatherers stand a little further back so they no longer end up on top of stumps.
- Emptying bags now uses `HomeTripRange` too (was a fixed 40 m).

## 1.4.70
- Fixed the farmer taking vegetables out of a chest for seed plants and storing them straight back. They stay in its pack until that seed reaches `SeedTarget`.
- Fixed companions spinning in a corner trying to reach stacked chests. They now use chests and pick crops from anywhere in reach, up to about 2 m above or below.
- Companions sprint in bursts: run down to half stamina (30% in a fight), walk back up to 90%, then run again.
- When a job is changed or turned off, or Homestead turned on, a companion near home puts away what it gathered first. It keeps equipped items and the gear, ammo, food and mead you gave it. If it's away from home, it does this when it's back.

## 1.4.69
- Fixed companions waking up beside the bed instead of where they slept.
- Companions walk while working and only run to far targets. New setting `WorkRunDistance` (General, default 25 m, 0 = never run for work).
- Fixed the farmer making a separate chest trip for each seed type. It now takes up to `SeedCarry` (Farm, default 40) of each type in one trip.

## 1.4.68
- Fixed the farmer giving up on a field or chest just above or below it. It now keeps walking until it's actually there.
- Fixed the farmer planting one crop until it ran out of seeds. It now switches type every `PlantGroupSize` plants.

## 1.4.67
- Fixed farm zones never finding seeds in chests.
- Fixed companions on tool trips being blocked from a chest by nearby chests and furniture. Only walls, floors and large solid pieces block reach now.
- Companions only head for a chest they can walk all the way to, and try another chest with the tool if one can't be reached.
- The stuck jump only kicks in when really stuck, not while walking around a building.
- The default `PlantAny` list uses the real names `MushroomJotunPuffs` and `MushroomMagecap`. Short names still work, and existing configs keep their old value.

## 1.4.66
- Fixed companions missing nearby chests in large bases. Every chest in range is now checked.
- Fixed a farmer with only a few seeds and no vegetables never planting. It now plants the seeds to get started.
- Fixed companions taking tools out of chests through walls. They now open chests only from a spot they can walk to with nothing solid in between.
- Companions stuck on stairs or ledges on tool trips and farm work jump toward where they're going.
- `PlantAny` names ignore capital letters, and "Nothing to plant" log lines show how many seeds and vegetables were found.
- Debug logging for tool trips and farm work: position, destination and whether there's a path, every 2 seconds.

## 1.4.65
- Fixed a job and Stay Home both steering the companion back and forth. Picking one now turns the other off.
- "Stay Home" on the command wheel is now called "Homestead".
- Fixed companions crawling slowly away from a chest when Follow was picked mid tool trip. Trips now finish first, and a companion that's attacked fights and then carries on.
- Fixed Any farm zones planting tree seeds. They now only plant what's on the new `PlantAny` list (Farm).
- New setting `PlantGroupSize` (default 4): plant this many of one type before switching, so mixed fields end up in small blocks.
- New settings `SeedMinimum` (default 20, replaces `SeedReserve`) and `SeedTarget` (default 100) control when the farmer grows seed plants or crops.
- Barley, flax and other self-seeding crops are always planted, keeping `SeedMinimum` in the pack for replanting.

## 1.4.64
- New gather lists `GatherWood`, `GatherStone` and `GatherOre` (Harvest) set what each job collects and how much, with `WoodLimit`, `StoneLimit` and `OreLimit` (500 each) as category limits.
- Limits count the companion's pack and chests near home. Once everything is at the limit, it says so and waits.
- New setting `WoodSkipTrees` (default `Oak`): trees the Wood job never fells.
- New setting `BaseProtectRadius` (default 10 m): the Wood job won't fell a tree that close to anything built.
- Companions step clear of falling trees and splitting logs, and approach from the uphill side on slopes.
- A gatherer with a full pack takes a load to the home chest. With no home chest, `JunkCleanup` (on by default) drops junk to make room; gear, tools, ammo, food, seeds and `NeverDiscard` items are kept.
- Fixed the Ore job mining plain boulders that only drop stone.

## 1.4.63
- Fixed tool trips giving up before reaching the chest. Companions now walk to the front of the chest and only give up after 20 seconds without progress or 2 minutes.
- Repair (buildings), Restock and Hunt now pause during a tool trip like the other jobs.
- Routine farm scanning lines only show at Debug log level.

## 1.4.62
- When a job needs a tool the companion doesn't have, it takes the best one from a chest near home.
- Borrowed tools, ammo and bait go back to the chest when the job is turned off or changed. Tools it already had stay with it.
- If there's no tool in the chests, the companion says what it needs.
- The farmer keeps a reserve of carrot, turnip and onion seeds, planting vegetables as seed plants when short. New setting `SeedReserve` (Farm, default 20, 0 = off).

## 1.4.61
- Fixed item icons from a chest showing over the companion's inventory.
- Fixed companions being pushed to the side when getting out of bed.
- Fixed Farm and gathering jobs restarting every time the companion's inventory changed.
- Fixed the farmer leaving harvested crops on the ground and storing unrelated items in the crop chest.
- Forage leaves crops in your farm fields for the Farm job.
- Fixed companions standing in an open doorway for 8 seconds and giving up on their target.
- Fixed the "[Diag] ... hasn't moved" warning showing while a companion was working.

## 1.4.60
- Fixed companions vanishing when another mod deleted them. They're now protected and can still die and respawn normally. New setting `ProtectCompanions` (General, on by default).

## 1.4.59
- Fixed companions being deleted inside active wards by the "Guardstone wards remove monsters" mod.

## 1.4.58
- Fixed Farm filling the log with "Invalid IL code" errors and never farming when another mod changes the terrain lookup.
- Farm, Fish and the gathering jobs now write what they're doing to the log.
- Fixed one food message being logged every frame.

## 1.4.57
- Recall now only reaches companions within `RecallRange` (Controls, default 40 m) that have Command on. Point-and-command orders use the same range.
- Hold the command key for `SuperRecallHoldTime` (Controls, default 2 seconds) to recall every companion at any distance.

## 1.4.56
- When you get into bed, companions near their own beds go to bed too.
- At night, companions near their bed go to sleep even while working, and pick the job back up in the morning.
- Fixed the night skip waiting forever on a companion that couldn't reach a bed. It now gives up after 30 seconds.
- Recalling your companions wakes any that are asleep.
- New settings (Sleep): `SleepAtNight`, `SleepWhenPlayerSleeps` and `BedRange` (default 50 m).

## 1.4.55
- Fixed controller stick errors filling the log and the stick not working on the command wheel.

## 1.4.54
- The mod's ID is now `Maddhattr.OfflineCompanionsReborn` and it shows as "Offline Companions Reborn" in the config manager. Settings carry over automatically.
- Reborn won't load if ProfMags' original Offline Companions is also installed. Remove the original to use Reborn.

## 1.4.53
- Every companion now has a permanent ID that stays with it through death and respawn.
- Fixed companions collecting another companion's tombstone when they shared a name and owner.
- Beds remember companions by ID, so renaming a companion keeps its bed.
- The config file is now `Maddhattr.OfflineCompanionsReborn.cfg`. Settings are copied over automatically; the old `com.profmags.companions.cfg` can be deleted.
- Fixed the starter companion window never opening with Valheim Plus's "disable arrived message" setting.

## 1.4.52
- **Change:** Despawn on the command wheel now asks you to confirm with a Yes/No box that names the companion and says its items will be left in a tombstone. This replaces the old "press the wedge again within 3 seconds" confirm, and works the same with mouse or controller.

## 1.4.51
- **Issue:** Dragging an item a companion was wearing into your own inventory left it looking equipped there.
- **Fix:** The companion takes the item off first, so it arrives in your inventory as a normal item.
- **Issue:** Despawn on the command wheel gave an error and did nothing, left over from the 1.4.32 bed update.
- **Fix:** Despawn works again. The companion leaves a tombstone with everything it was carrying, and its bed is freed up.

## 1.4.50
- **Issue:** After you slept through the night, a companion got out of bed, climbed right back in, then got up again a few seconds later.
- **Fix:** When your sleep ends the night, companions get up and stay up.
- **Issue:** A companion that came back with its gear only put on one piece, and didn't put on the rest until later.
- **Fix:** If it's still putting on one piece, it now waits and then puts on the rest.

## 1.4.49
- **Issue:** When a companion kept all its items through death, its map marker vanished right away because the tombstone was empty, even though that tombstone is where you'd go to bring it back.
- **Fix:** The marker now stays while the companion is waiting to be revived. After it comes back, the marker only stays if items were left in the tombstone.
- Cleaned up a leftover debug message that showed up as a warning in the log.

## 1.4.48
- **Issue:** A companion's bed said "unclaimed" while the companion was dead, and could show "HC_Companion(Clone)" again after it came back.
- **Fix:** The bed claim is now saved on the bed itself. It keeps the companion's real name through death, travel, and reloading the game, and other players see it too.
- **Issue:** After respawning, a companion with items left in a tombstone far away would stand around, refuse to fight, and ignore Follow for a couple of minutes, then give up on its items for good.
- **Fix:** A companion only goes after its tombstone once it's actually nearby. Until then it follows, fights, and works as normal, and it never forgets the items. It fights off attackers before going back to looting, and it remembers every one of its tombstones, not just the latest.
- **Issue:** A companion's tombstone disappeared from the map as soon as it respawned, even though its items were still in the tombstone.
- **Fix:** The map marker now stays until the tombstone is emptied, whether you empty it or the companion does.

## 1.4.47
- **Issue:** Auto-respawn always brought a companion back at its tombstone, never at its bed or next to you. When the tombstone was far away, the respawn failed completely.
- **Fix:** Auto-respawn now uses the claimed bed first (a free spot beside it), then next to you, or the spawn stones if you've turned that on.
- **Issue:** After dying and coming back, a companion forgot which bed was its own.
- **Fix:** Companions keep their claimed bed through death.
- **Issue:** Companions never said they needed a bed, even though that line was added in 1.4.30.
- **Fix:** They now speak up when they don't have a bed.

## 1.4.46
- **Issue:** A dead companion's auto-respawn timer stopped, and started over from zero, whenever you went far from its tombstone or reloaded the game.
- **Fix:** The respawn time is now saved with the world. The companion comes back on time no matter where you are, even after quitting and reloading.
- A companion that respawns while you're far from its tombstone leaves any dropped items in the tombstone and goes back for them. An empty tombstone clears itself the next time its area loads.
- A companion with no claimed bed now respawns next to you by default. New setting `AutoRespawnAtWorldSpawnWhenNoBed` sends it to the spawn stones instead. This replaces `AutoRespawnNearOwner`.
- In multiplayer, only the companion's owner's game brings it back, so two players can't respawn the same companion.
- Controller: the stance area of the command wheel is bigger, so stances are easier to pick.

## 1.4.45
- **Issue:** In 1.4.44, the command wheel wouldn't highlight anything on a controller, and a companion that had slept wouldn't follow you after waking.
- **Fix:** Corrected two new pieces of code that crashed when they ran.
- **Issue:** Companion crafting (cooking and homestead work) and taking seeds for farming could crash, left over from the 1.4.29 game-patch update.
- **Fix:** Corrected the same kind of problem in both.

## 1.4.44
- **Issue:** A companion's bed could show "HC_Companion(Clone)" instead of its name.
- **Fix:** The bed's name label is now only set when the companion claims the bed, using its real name.
- **Issue:** Companions going to bed ended up lying on the floor next to it.
- **Fix:** Companions now lie in the bed the same way a player does.
- **Issue:** Every time a companion slept, it quietly moved its Home point to a spot beside the bed, even if you never used Set Home.
- **Fix:** Home now only changes when you use Set Home.
- Added logging for controller stance selection in the command wheel, to help tune how it feels.

## 1.4.43
- **New:** What a companion loses when it dies is now one setting, `DeathItemRule` (under Respawn). 0 = follow the world's death penalty setting, the same as a player would (default). 1 = keep everything. 2 = keep equipped gear, drop the rest. 3 = drop everything. This replaces the old `DropInventoryOnDeath` and `DropEquippedGearOnDeath` toggles.
- In multiplayer, a companion always uses its owner's setting, even if another player's game happens to be handling it when it dies.
- **Issue:** When a companion auto-respawned at its bed, it instantly pulled its dropped items out of the tombstone, even from across the map.
- **Fix:** After an auto-respawn, dropped items stay in the tombstone and the companion goes back for them. Resurrecting a companion by hand at its tombstone still gives the items back right away.
- **Issue:** When keeping items through death, a companion could lose some of them. Anything past 16 stacks was deleted, and about half of the items that should have been kept went into the tombstone instead.
- **Fix:** Kept items now all come back with the companion.

## 1.4.42
- **Issue:** On a controller, picking a combat stance in the command wheel was really hard. The stance only highlighted while the stick was held at a very specific partial push, so it kept flickering off.
- **Fix:** Stance selection is now "sticky." Push the stick partway toward a stance (up for Balanced, right for Passive, down for Ranged, left for Melee) and it stays highlighted after you let go. Press the use button to apply it. The stance area is also a bit larger. Pushing the stick all the way out still selects the outer actions like before. Mouse and keyboard are unchanged.

## 1.4.41
- **Issue:** For some players (reported with the game set to Russian), the game could hang on the loading screen for a couple of minutes and then close.
- **Fix:** The mod was reloading its translation files every time the game refreshed its language settings, which some setups do many times at startup. It now only reloads when the language actually changes.

## 1.4.40
- **Issue:** A companion running the smelter would get the kiln lit but then keep interrupting itself every time it tried to insert ore or fuel, never finishing the job.
- **Fix:** Removed a bug that made the companion think its job had changed every time it used an item from its own inventory. It now stays on task.

## 1.4.39
- **Issue:** Companions could silently fail (and spam the log) while inserting ore into a smelter.
- **Fix:** Corrected a missing piece of data the game expects when adding ore. Fuel insertion was never affected.

## 1.4.38
- **Issue:** Follow and Stay Home could both end up "on" at once, with Stay Home quietly winning forever.
- **Fix:** Follow and Stay Home are now mutually exclusive — turning one on turns the other off. "Set Home" also no longer turns Stay Home on by itself; it just sets the home point.

## 1.4.37
- **Issue:** A companion could crash if it picked up ore/fuel from a chest at the exact moment its gear happened to re-equip.
- **Fix:** Closed that timing gap so it's handled cleanly instead of crashing.

## 1.4.36
- **Issue:** Companions could get "stuck" wandering near a chest instead of using it, without ever giving up and trying somewhere else.
- **Fix:** The stuck-detection now checks whether the companion is actually making progress toward its target, not just whether it's moving at all — so it correctly notices, gives up on that chest, and tries another.

## 1.4.35
- **Issue:** A companion's claimed bed showed a confusing "Unclaimed (claimed by ...)" label with a garbled name.
- **Fix:** It now reads cleanly as "\<Companion's Name\>'s Bed."

## 1.4.34
- **Issue:** Companions weren't sleeping, "Set Home" seemed to silently fail, and companions moved erratically near beds.
- **Fix:** All three turned out to be the same root cause — a bed-ownership check that was failing every time. Fixed, and all three symptoms went away together.

## 1.4.33
- **Issue:** A rare crash could hit bed logic, tombstones, or a few other spots depending on the exact build, seemingly at random.
- **Fix:** Found and corrected the underlying bug everywhere it appeared in the mod, not just the one reported spot.

## 1.4.32
- Companions now claim a specific bed as their own — permanently, not just while sleeping in it — the same way a player would settle into one. That bed shows "(claimed by \<name\>)" and is their auto-respawn point.
- A companion automatically claims a bed when you Set Home nearby, after a short delay if they don't have one, or if they notice you've claimed a bed and they haven't.
- Pointing at a bed and commanding a companion is now a manual override: claims it if free, sleeps in it if already theirs, or refuses if it belongs to someone else.
- Claiming a new bed releases the old one back to the pool.
- Companions no longer grab a random nearby bed at night — if they don't have a claimed one, they'll say so.

## 1.4.31
- Added an automatic respawn timer for dead companions, alongside the existing manual tombstone resurrect. Configurable delay, default 5 minutes.
- Auto-respawn happens at a companion's claimed bed if it has one, otherwise world spawn (or optionally near the owner).
- Companions now keep 100% of their inventory by default when they die, instead of always dropping it. Two new options let you change that.
- If items were dropped, the companion will walk back and loot its own tombstone once it's close enough. The tombstone disappears automatically once nothing's left in it.
- A companion's tombstone now shows up on the map, like a player's own death marker.

## 1.4.30
- Companions no longer take over a bed a player has personally claimed.
- A bed a companion is using now shows "claimed by \<companion name>" so it's clear it's taken.
- Companions will speak up if they don't have a bed to sleep in.

## 1.4.29
- Updated for a Valheim game patch that changed how a few things work under the hood (inventory, gear visuals, piece placement). No behavior change intended — this just keeps the mod working.

## 1.4.28
- **Issue:** Companions could get stuck standing at "bow range" even after correctly switching to melee when they ran out of arrows.
- **Fix:** Positioning now follows the weapon actually in hand, not the companion's general combat stance.

## 1.4.27
- **Issue:** A direct attack command could bypass a companion's normal combat AI (weapon choice, ammo checks, fleeing) entirely.
- **Fix:** Direct attacks now go through the same AI as everything else.
- Added a separate "flee" health threshold, distinct from the shield-retreat threshold.
- Companions can now equip a shield while fleeing.
- A companion that gets cornered while fleeing now defends itself instead of just standing there.

## 1.4.26
- Shield choice is now handled entirely by the health/threat-aware logic, instead of sometimes being overridden by "pick whatever blocks the most."
- "Return to me" now walks back normally at short range, or does a disguised teleport-recall if you're too far away, instead of risking getting stuck.
- Companions now fall in with a nearby real player and keep fighting if their owner dies mid-session, instead of standing idle.

## 1.4.17 – 1.4.25
- Fixed shields defaulting to the tower shield far more often than intended.
- Fixed companions not properly falling back to melee when they ran out of arrows.
- Rebuilt "come to me" / return-to-owner as reliable distance tracking instead of an older timer approach that could get stuck.
- Fixed a few "return to me" bugs (not firing on companion death; wrongly pulling in companions not set to Follow).
- Tracked down and fixed several different "companion just stands there" reports.

## 1.4.13 – 1.4.16
- Added companion health/stamina status bars above their heads, then simplified them based on feedback.

## 1.4.4 – 1.4.12
- Added a "torch opener" behavior so companions can light their way in the dark.
- Fixed a weapon-swap loop, a "stuck on Passive after death" bug, a version-string bug, and a water-safety stuck state.

## 1.4.1 – 1.4.3
- Fixed a long-standing bug where a companion's inventory could show garbage tooltips instead of real items.

## 1.4.0
- Initial Valheim 1.0 compatibility pass — updated several game functions that changed in the 1.0 update so the mod loads and runs at all.
