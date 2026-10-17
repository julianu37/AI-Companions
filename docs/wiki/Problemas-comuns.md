# Problemas comuns

Sintoma → causa provável → o que fazer.

**Onde olhar no log, sempre:** `<Valheim>\BepInEx\LogOutput.log`. As linhas do mod começam com `[IA]`, `[STT]`
e `[TTS]`.

---

## Ele não responde a nada

| Onde olhar | O que procurar |
|---|---|
| `LogOutput.log` | `[IA] Chat orders ready: enabled=…, endpoint=…, model=…` |

**Causas prováveis, na ordem:**

1. **O LM Studio não está com o servidor ligado.** Abra a aba **Developer** e confira o status `Running` na
   porta **1234**.
2. **O `BaseUrl` ou o `Modelo` estão errados.** O `Modelo` tem que ser o **API Model Identifier** que o LM
   Studio mostra, não o nome da pasta. Veja **[LLM no LM Studio](LLM-no-LM-Studio)**.
3. **`[IA] Ligado` está `false`.** Ligue no painel **`F8`**.
4. **`[IA] Prefixo` está preenchido.** Se você puser `/c`, só mensagens que começam com `/c` vão ao modelo.
5. **Nenhum companheiro está por perto / apto a receber ordem.** Ele precisa estar spawnado.

---

## Demora muito para responder

| Onde olhar | O que procurar |
|---|---|
| `LogOutput.log` | `[IA] Tempo: … ms` — o mod avisa quando passa de **5 s** |

**Causas prováveis:**

1. **Você está usando `localhost` no `BaseUrl`.** No Windows, `localhost` resolve primeiro para IPv6 (`::1`) e
   o LM Studio só escuta IPv4 — cada ordem paga ~**2,5 s** de pedágio. Troque por
   **`http://127.0.0.1:1234/v1`**.
2. **O modelo é grande ou está na CPU.** Um 3B responde em ~0,35–0,4 s; o `google/gemma-4-e4b` leva ~3 s.
   Modelos maiores praticamente exigem GPU.
3. **`[IA] Memoria` alta.** Cada troca lembrada aumenta o prompt que o modelo precisa ler. Reduza.
4. **A primeira mensagem da sessão** inclui o carregamento do modelo do LM Studio — ela é naturalmente mais
   lenta.

> Lembre: o pedido roda em **segundo plano**. Mesmo lento, ele **não** trava o jogo.

---

## Responde errado ou ignora o número

**Causa provável:** o modelo não entendeu o recurso ou a quantidade.

**O que fazer:**

- Confira o que o modelo entendeu: `[IA] MostrarRespostaNoChat` mostra a resposta crua no chat.
- Use o **`qwen2.5-3b-instruct`** com `[IA] SaidaEstruturada = true` — é a combinação que o projeto mediu como
  confiável (o 3B passou de 0/6 para **6/6** respostas válidas com a saída estruturada).
- Lembre que **o número é um alvo contado na mochila dele**: se ele já tem o suficiente, ele **não** sai para
  coletar, e isso é o comportamento correto.
- Para as ordens por voz, um reconhecimento ruim chega errado ao modelo. Veja "o microfone não grava" abaixo.

---

## Responde em inglês

**Causa provável:** o modelo escolhido não é bom em português, ou a personalidade/preset está vazia e o modelo
está improvisando.

**O que fazer:**

- Use o **`qwen2.5-3b-instruct`** (bom em PT-BR).
- Ajuste a personalidade no painel **`I`** (preset ou texto livre em português) — ela entra no prompt e puxa a
  resposta para o português.
- Reduza a `[IA] Temperatura` (o padrão `0.1` já é baixo; valores altos fazem o modelo divagar).

---

## A voz tem eco (parece uma caverna)

**Causa:** não é bug — o Valheim tem **uma zona de reverb global** de 1000 m no próprio objeto do
`AudioListener`, com 4 s de cauda. A fonte de áudio do companheiro estava no padrão do Unity e pegava essa
cauda inteira.

**O que fazer:** confirme que `[IA] TtsReverbZoneMix` está em **`0`** (o padrão). Se quiser um "espaço" sutil
na voz, use `0,19`. Vale na **próxima frase**, sem reiniciar. Veja **[Voz](Voz)**.

---

## A voz não sai, ou sai baixa

| Onde olhar | O que procurar |
|---|---|
| `LogOutput.log` | Linhas `[TTS]` (tempo de carga, síntese, RTF) |

**Causas prováveis:**

1. **`[IA] TtsLigado` está `false`.** Ligue no painel **`I`**.
2. **O modelo do Kokoro não está no lugar.** Confira `tts\kokoro\` com `model.int8.onnx`, `voices.bin`,
   `tokens.txt` e `espeak-ng-data\`.
3. **O volume está baixo.** A voz **não passa pelo mixer do jogo** — ela **ignora os sliders de volume do
   Valheim** e obedece **só** a `[IA] TtsVolume`. Ajuste essa chave (ou o slider do painel `I`).
4. **Teste isolado:** no console (**`F5`**), rode `modia_tts_teste Bom dia, chefe!`. Se funcionar aí, o
   problema é de configuração; se não, é do modelo.

---

## O microfone não grava

| Onde olhar | O que procurar |
|---|---|
| `LogOutput.log` | `[STT] Push-to-talk: LIGADO … N microfone(s) 0: nome, 1: nome` |

**Causas prováveis:**

1. **Nenhuma janela pode estar aberta.** A tecla não grava com inventário, mapa, menu, chat, console ou os
   painéis do mod abertos.
2. **`[IA] VozLigada` está `false`.**
3. **O microfone errado está selecionado.** O log lista os dispositivos numerados — ponha o número em
   `[IA] VozDispositivo`.
4. **O limiar de silêncio está alto.** `[IA] VozLimiarSilencio` (padrão `0.01`) corta o começo/fim da gravação.
   Se o seu microfone é baixinho, reduza; se pega muito ruído, aumente.
5. **A fala foi curta demais.** O log diz `[STT] Gravacao curta demais (…)` — fale por mais tempo.
6. **Teste isolado:** `modia_stt_teste` no console (**`F5`**) roda o Whisper sobre um WAV, **sem** microfone.
   Se ele transcrever, o modelo está bom e o problema é de captura.

> **Windows x64 é obrigatório** para a voz: os nativos do **STT** (whisper.cpp) **e do TTS** (sherpa-onnx/onnxruntime)
> são `win-x64`.

---

## A tecla `V` liga o auto-pickup do jogo

**Causa:** no Valheim, `V` é o *Toggle Auto-Pickup* por padrão.

**O que fazer:** o mod já bloqueia esse toggle — mas **só** quando `[IA] VozLigada` está ligado **e** o binding
do auto-pickup do jogo aponta para a **mesma** tecla. Então:

- **Reatribua o auto-pickup** na tela de controles do Valheim para outra tecla (o mod deixa de bloquear e você
  tem os dois), **ou**
- mude `[IA] VozTecla` para outra tecla no painel **`I`**.

Se você reatribuir, o mod reavalia o conflito na hora. Veja **[Voz](Voz)**.

---

## O painel `I` não abre

**Causas prováveis:**

1. **Uma janela está aberta.** O painel não abre com inventário, mapa, menu, painel **`F8`** ou o campo de
   busca do comerciante abertos. Feche-os.
2. **A tecla foi trocada.** Confira `[IA] PainelTecla` (padrão `I`).
3. **Você está digitando em um campo de texto.** Nesse caso a tecla é ignorada de propósito — o mod não rouba
   a tecla do chat/console.

---

## O painel `F8` não clica / um clique é engolido

**Causa (já corrigida):** os painéis do mod estavam na mesma camada de desenho do HUD do jogo, e o HUD
**está em `sortingOrder = 400`**. Num empate de camada, o uGUI desempata pela ordem de registro dos raycasters
— e o do jogo ganha. Havia ainda elementos **decorativos** do painel recebendo clique sem serem controles.

**O que fazer:**

- Atualize para a versão com a correção (os canvases do mod agora ficam na faixa **1701+**, **acima** do HUD).
- Se o problema voltar, ligue `[Advanced] UiDiag = true`: na próxima abertura dos painéis o log escreve
  `[UiDiag]` com as camadas da cena e quem está na frente dos controles. Depois desligue de novo.

> O mesmo ajuste de camada resolveu o texto do **centro do menu radial** embaralhado com o `crosshair` do jogo.

---

## O jogo não carrega o mod

| Onde olhar | O que procurar |
|---|---|
| `LogOutput.log` | `ModAI-BR-Companions v1.6.2 loading...` e `ModAI-BR-Companions loaded successfully! (N methods patched)` — o `v1.6.2` é a versão que você instalou |

**Causas prováveis:**

1. **A pasta está no lugar errado.** O caminho tem que ser
   `<Valheim>\BepInEx\plugins\ModAI-BR-Companions\` — com o `Companions.dll` **dentro** dela.
2. **Faltam as DLLs de dependência.** `Whisper.net.dll`, `sherpa-onnx.dll` e as `System.*` precisam estar
   junto do `Companions.dll`.
3. **O BepInEx não está instalado** (ou está numa versão incompatível). Use o **BepInExPack_Valheim 5.4.2351**.
4. **Versão do jogo incompatível.** O mod é para o **Valheim 1.0.x** (Unity 6).

> **O erro de PlayFab / NullReferenceException no boot é normal** e **não** é do mod. Não se preocupe com ele.

---

## Ainda não resolveu?

Junte estas informações e abra uma issue em
https://github.com/julianu37/ModAI-BR-Companions/issues :

- O trecho do `LogOutput.log` em volta do problema (as linhas `[IA]`, `[STT]` ou `[TTS]`).
- A versão do mod (aparece no boot do log) e a versão do Valheim.
- O que você fez (chat ou voz) e o que aconteceu.
