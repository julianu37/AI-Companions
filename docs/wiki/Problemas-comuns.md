# Problemas comuns

Sintoma → causa provável → o que fazer.

**Onde olhar no log, sempre:** `<Valheim>\BepInEx\LogOutput.log`. As linhas do mod começam com `[IA]`, `[STT]`
e `[TTS]`. (O `[IA]` é a **tag do log** — a seção do `.cfg` é **`[AI]`**, com as chaves em inglês.)

---

## Ele não responde a nada

| Onde olhar | O que procurar |
|---|---|
| `LogOutput.log` | `[IA] Chat orders ready: enabled=…, endpoint=…, model=…` |

**Causas prováveis, na ordem:**

1. **A `ApiKey` está vazia ou errada.** O mod vem apontado para o **Groq**, que **exige** a chave (veja
   "responde erro de autenticação" abaixo). Se você usa o **LM Studio** local, a chave fica vazia e o `BaseUrl`
   tem que ser `http://127.0.0.1:1234/v1`.
2. **O `BaseUrl` ou o `Model` estão errados.** No Groq são `https://api.groq.com/openai/v1` e
   `openai/gpt-oss-20b`. No LM Studio local, o `Model` tem que ser o **API Model
   Identifier** que ele mostra, não o nome da pasta. Veja **[LLM (Groq, LM Studio ou OpenRouter)](LLM-no-LM-Studio)**.
3. **Usando o LM Studio local: o servidor não está ligado.** Abra a aba **Developer** e confira o status `Running`
   na porta **1234**.
4. **`[AI] Enabled` está `false`.** Ligue no painel **`F8`**.
5. **`[AI] Prefix` está preenchido.** Se você puser `/c`, só mensagens que começam com `/c` vão ao modelo.
6. **Nenhum companheiro está por perto / apto a receber ordem.** Ele precisa estar spawnado.

> Antes de tudo, use o **Testar conexão** do painel **`I`**: ele diz, em segundos, se a URL responde e se o
> modelo aparece na lista. Lembre que a **chave** só é validada numa ordem de verdade (o teste confirma a URL e a
> lista de modelos, não a autenticação).

---

## Responde erro de autenticação (401 / "unauthorized")

| Onde olhar | O que procurar |
|---|---|
| `LogOutput.log` | `[IA]` com o status **401** (ou uma mensagem de *unauthorized* / *invalid api key*) |
| Painel **`I`** → **Testar conexão** | **Pode** responder `Conexão OK` mesmo com a chave errada — no OpenRouter a lista de modelos é pública. O 401 aparece na **ordem**, não no teste |

**Causa:** o **Groq exige** a `[AI] ApiKey` (e o OpenRouter também), e ela está **vazia**, **errada** ou
**apagada** (por exemplo, depois de você copiar um `.cfg` de outra máquina, ou depois de atualizar para a
**1.8.3**, que zera as configurações) — ou é a chave de **outro** serviço, colada junto com uma URL que não é a dele.

**O que fazer:**

1. Crie ou copie a chave em **https://console.groq.com/keys** (Groq) ou em **https://openrouter.ai/keys**
   (OpenRouter).
2. Cole em **`[AI] ApiKey`** — no painel **`I`** (aba **IA** → **Configurações do LLM**) ou direto no `.cfg`.
3. Clique em **Testar conexão**; depois em **Aplicar** (a configuração vale na próxima mensagem).
4. Se você **não** quer usar o Groq, aponte o `BaseUrl` para o servidor local
   (`http://127.0.0.1:1234/v1`), use o `Model` do LM Studio e **deixe a `ApiKey` vazia**.

> Chave vazada? Apague em **https://console.groq.com/keys** (Groq) ou
> **https://openrouter.ai/settings/keys** (OpenRouter) e crie outra. O `ApiKey` do `.cfg` não deve ser
> compartilhado nem versionado.

---

## Responde 429 (limite de requisições ou de tokens)

| Onde olhar | O que procurar |
|---|---|
| `LogOutput.log` | `[IA]` com o status **429** (ou uma mensagem de *rate limit* / *too many requests*) |

**Causa:** o **tier gratuito** do serviço foi atingido. No **Groq**, com o `openai/gpt-oss-20b`, os limites são
**30 requisições por minuto**, **1.000 por dia**, **8.000 tokens por minuto** e **200.000 por dia** — e, como cada
ordem gasta ~1.800 tokens, o teto prático é de **~4 ordens por minuto**. No **OpenRouter**, os modelos `:free` têm
**20 pedidos por minuto** e um teto diário que depende do histórico da conta.

**O que fazer:**

1. **Espere alguns segundos** e mande a ordem de novo — o limite é por janela de tempo, então ele volta sozinho.
2. Diminua a frequência: o `[AI] CooldownSeconds` (padrão **3**) já ajuda; aumente-o se você manda ordens em rajada.
3. Reduza o `[AI] Memory` (cada troca lembrada aumenta o prompt e, com ele, os tokens de cada ordem).
4. Para uso intenso, use o **LM Studio/Ollama local** — não há limite de pedidos nem de tokens.

> O **429** não quebra nada: a ordem é recusada com aviso e o jogo continua. Ele **não** é erro de chave — isso é o
> **401**, acima.

---

## Demora muito para responder

| Onde olhar | O que procurar |
|---|---|
| `LogOutput.log` | `[IA] Tempo: … ms` — o mod avisa quando passa de **5 s** |

**Causas prováveis:**

1. **O `.cfg` ainda está no OpenRouter.** O padrão atual é o **Groq** (`openai/gpt-oss-20b`), que responde em
   **fração de segundo** na nossa medição. Se o seu `[AI] BaseUrl` continua `https://openrouter.ai/api/v1`, você
   está no roteador gratuito dele (`openrouter/free`), que **varia** de uma ordem para outra e passa pela fila do
   provedor. Troque pelo Groq (veja **[LLM (Groq, LM Studio ou OpenRouter)](LLM-no-LM-Studio)**) ou use o LM Studio
   local.
2. **Não confunda lentidão com o limite do Groq.** O **429** responde **na hora** (a ordem é recusada com aviso),
   não devagar — se o sintoma é demora, a causa é outra. Veja "responde 429" acima para o caso do limite.
3. **O seu `.cfg` local ainda está com `localhost` no `BaseUrl`.** No caso do LM Studio, o padrão é
   `http://127.0.0.1:1234/v1`; se o arquivo é de uma versão anterior, troque. No Windows, `localhost` resolve
   primeiro para IPv6 (`::1`) e o LM Studio só escuta IPv4 — cada ordem paga ~**2,5 s** de pedágio.
4. **O modelo é grande ou está na CPU (local).** Um 3B (`qwen2.5-3b-instruct`) responde em ~0,35–0,4 s; o
   `google/gemma-4-e4b` leva ~3 s. Modelos maiores praticamente exigem GPU.
5. **`[AI] Memory` alta.** Cada troca lembrada aumenta o prompt que o modelo precisa ler. Reduza.
6. **A primeira mensagem da sessão** inclui o carregamento do modelo — no LM Studio local ela é naturalmente mais
   lenta, e nos serviços remotos a primeira também costuma ser a mais fria.

> Lembre: o pedido roda em **segundo plano**. Mesmo lento, ele **não** trava o jogo.

---

## Responde errado ou ignora o número

**Causa provável:** o modelo não entendeu o recurso ou a quantidade.

**O que fazer:**

- Confira o que o modelo entendeu: `[AI] ShowReplyInChat` mostra a resposta crua no chat.
- Use o **`qwen2.5-3b-instruct`** com `[AI] StructuredOutput = true` — é a combinação que o projeto mediu como
  confiável (o 3B passou de 0/6 para **6/6** respostas válidas com a saída estruturada).
- No padrão (**Groq**, `openai/gpt-oss-20b`) o modelo é sempre o mesmo, então a resposta é consistente. No
  **OpenRouter** (alternativa), o `openrouter/free` troca de modelo a cada pedido — a qualidade também varia. Se a
  ordem errar sempre do mesmo jeito, escolha **um modelo específico** no `[AI] Model` (os gratuitos do OpenRouter
  têm `:free` no nome) ou use o LM Studio local, que é sempre o mesmo modelo.
- Lembre que **o número é um alvo contado na mochila dele**: se ele já tem o suficiente, ele **não** sai para
  coletar, e isso é o comportamento correto.
- Para as ordens por voz, um reconhecimento ruim chega errado ao modelo. Veja "o microfone não grava" abaixo.

---

## Responde em inglês

**Causa provável:** o modelo escolhido não é bom em português, ou a personalidade/preset está vazia e o modelo
está improvisando.

**O que fazer:**

- Use o **`qwen2.5-3b-instruct`** (bom em PT-BR) se estiver no LM Studio; no **Groq** (padrão) e no **OpenRouter**,
  um modelo maior costuma escrever melhor em português do que os pequenos.
- Ajuste a personalidade no painel **`I`** (preset ou texto livre em português) — ela entra no prompt e puxa a
  resposta para o português.
- Reduza a `[AI] Temperature` (o padrão `0.1` já é baixo; valores altos fazem o modelo divagar).

---

## A voz tem eco (parece uma caverna)

**Causa:** não é bug — o Valheim tem **uma zona de reverb global** de 1000 m no próprio objeto do
`AudioListener`, com 4 s de cauda. A fonte de áudio do companheiro estava no padrão do Unity e pegava essa
cauda inteira.

**O que fazer:** confirme que `[AI] TtsReverbZoneMix` está em **`0`** (o padrão). Se quiser um "espaço" sutil
na voz, use `0,19`. Vale na **próxima frase**, sem reiniciar. Veja **[Voz](Voz)**.

---

## A voz não sai, ou sai baixa

| Onde olhar | O que procurar |
|---|---|
| `LogOutput.log` | Linhas `[TTS]` (tempo de carga, síntese, RTF) |

**Causas prováveis:**

1. **`[AI] TtsEnabled` está `false`.** Ligue no painel **`I`**.
2. **O modelo do Kokoro não está no lugar.** Confira `tts\kokoro\` com `model.int8.onnx`, `voices.bin`,
   `tokens.txt` e `espeak-ng-data\`.
3. **O volume está baixo.** A voz **não passa pelo mixer do jogo** — ela **ignora os sliders de volume do
   Valheim** e obedece **só** a `[AI] TtsVolume`. Ajuste essa chave (ou o slider do painel `I`).
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
2. **`[AI] VoiceEnabled` está `false`.**
3. **O microfone errado está selecionado.** O log lista os dispositivos numerados — ponha o número em
   `[AI] VoiceDevice`.
4. **O limiar de silêncio está alto.** `[AI] VoiceSilenceThreshold` (padrão `0.01`) corta o começo/fim da gravação.
   Se o seu microfone é baixinho, reduza; se pega muito ruído, aumente.
5. **A fala foi curta demais.** O log diz `[STT] Gravacao curta demais (…)` — fale por mais tempo.
6. **Teste isolado:** `modia_stt_teste` no console (**`F5`**) roda o Whisper sobre um WAV, **sem** microfone.
   Se ele transcrever, o modelo está bom e o problema é de captura.

> **Windows x64 é obrigatório** para a voz: os nativos do **STT** (whisper.cpp) **e do TTS** (sherpa-onnx/onnxruntime)
> são `win-x64`.

---

## A tecla `V` liga o auto-pickup do jogo

**Causa:** no Valheim, `V` é o *Toggle Auto-Pickup* por padrão.

**O que fazer:** o mod já bloqueia esse toggle — mas **só** quando `[AI] VoiceEnabled` está ligado **e** o binding
do auto-pickup do jogo aponta para a **mesma** tecla. Então:

- **Reatribua o auto-pickup** na tela de controles do Valheim para outra tecla (o mod deixa de bloquear e você
  tem os dois), **ou**
- mude `[AI] VoiceKey` para outra tecla no painel **`I`**.

Se você reatribuir, o mod reavalia o conflito na hora. Veja **[Voz](Voz)**.

---

## O painel `I` não abre

**Causas prováveis:**

1. **Uma janela está aberta.** O painel não abre com inventário, mapa, menu, painel **`F8`** ou o campo de
   busca do comerciante abertos. Feche-os.
2. **A tecla foi trocada.** Confira `[AI] PanelKey` (padrão `I`).
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

## Dei o Megingjord ao companheiro e o peso não mudou

| Onde olhar | O que procurar |
|---|---|
| Painel **`I`** (aba do companheiro) e barra do **HUD** | O peso continua `…/300` (ou o valor que você pôs em `[General] MaxCarryWeight`) mesmo com o cinto na mochila |
| Coleta | Ele volta para casa para descarregar em **298** em vez de perto de **448** |

**Causa:** em versões **anteriores à 1.8.0** o bônus do cinto não era lido. O limite de peso do companheiro é
**do mod** (`[General] MaxCarryWeight`, 300 por padrão), não do jogo, e o `+150` do Megingjord é um *status effect*
cujo único leitor no vanilla é o `Player`. Como o companheiro não é um jogador, o cinto só ocupava espaço e pesava.

**O que fazer:** **atualize para a 1.8.1**. Depois disso o `/450` aparece em ~**0,5 s** (com o `300` padrão), e esta
linha confirma no `LogOutput.log`:

```
[Carry] "Thorin": +150 de capacidade (Megingjord) - teto 450
```

> O cinto **não precisa ser equipado** — vale com ele em qualquer lugar da mochila (num `Humanoid` que não é jogador
> o slot de utilidade nem sobrevive ao reload). E **dois** cintos **não somam**: o bônus conta uma vez só. Os detalhes
> das duas chaves estão em **[Configuração](Configuracao)**.

---

## O jogo não carrega o mod

| Onde olhar | O que procurar |
|---|---|
| `LogOutput.log` | `AI Companions v1.8.3 loading...` e `AI Companions loaded successfully! (N methods patched)` — o `v1.8.3` é a versão que você instalou |

**Causas prováveis:**

1. **A pasta está no lugar errado.** O caminho tem que ser
   `<Valheim>\BepInEx\plugins\AI-Companions\` — com o `Companions.dll` **dentro** dela.
2. **Faltam as DLLs de dependência.** `Whisper.net.dll`, `sherpa-onnx.dll` e as `System.*` precisam estar
   junto do `Companions.dll`.
3. **O BepInEx não está instalado** (ou está numa versão incompatível). Use o **BepInExPack_Valheim 5.4.2351**.
4. **Versão do jogo incompatível.** O mod é para o **Valheim 1.0.x** (Unity 6).

> **O erro de PlayFab / NullReferenceException no boot é normal** e **não** é do mod. Não se preocupe com ele.

---

## Minhas configurações voltaram ao padrão (ou "o mod não lê o meu `.cfg`")

**Causa:** na **1.8.3** o arquivo de configuração mudou de nome e de idioma. O mod lê
**`BepInEx\config\AiCompanions.cfg`**; o `Maddhattr.OfflineCompanionsReborn.cfg` (das versões anteriores)
**não é mais lido** e fica no disco **sem efeito** — por isso as opções voltam aos **padrões**, inclusive a
`ApiKey` em branco. A seção da IA também virou **`[AI]`**, com as chaves em **inglês**.

**O que fazer:**

1. Confirme que o arquivo existe: `<Valheim>\BepInEx\config\AiCompanions.cfg` (ele é criado no primeiro boot).
2. Refaça o que você tinha à mão — o mais importante é a **`[AI] ApiKey`**. O menu **`F8`** (ou o painel `I`)
   mostra tudo em jogo, com os nomes no **idioma do jogo**.
3. Apague o `.cfg` antigo para não se confundir: ele **não** volta a ser lido.
4. Confira no log a linha `[Config] Maddhattr.OfflineCompanionsReborn.cfg was replaced by AiCompanions.cfg ...`.

> **Save, mundo, companheiros, casas e inventário não são afetados** — o **GUID** do plugin e as chaves `HC_*`
> não mudaram. Só a configuração do mod volta ao padrão.

---

## Ainda não resolveu?

Junte estas informações e abra uma issue em
https://github.com/julianu37/AI-Companions/issues :

- O trecho do `LogOutput.log` em volta do problema (as linhas `[IA]`, `[STT]` ou `[TTS]`).
- A versão do mod (aparece no boot do log) e a versão do Valheim.
- O que você fez (chat ou voz) e o que aconteceu.
