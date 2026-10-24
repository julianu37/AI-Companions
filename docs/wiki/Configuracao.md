# Configuração

Onde fica o arquivo de configuração, como editá-lo em jogo e o que cada opção da seção `[IA]` faz.

---

## Onde fica o `.cfg`

```
<Valheim>\BepInEx\config\Maddhattr.OfflineCompanionsReborn.cfg
```

O arquivo é **gerado pelo BepInEx** na primeira vez que o mod carrega, e é regravado quando você muda algo
pelos painéis. Ele tem cerca de **1540 linhas** e **~265 opções** no total, divididas em várias seções
(`[General]`, `[Gathering]`, `[Hunting]`, `[Speech]`, `[Advanced]`, `[IA]` e outras).

> O nome do arquivo (`Maddhattr.OfflineCompanionsReborn`) vem do **GUID do plugin**, mantido do upstream de
> propósito para que as suas configurações continuem valendo. É por isso que o nome do arquivo **não** é o nome
> do mod.

A seção **`[IA]`** começa por volta da **linha 871** e tem **39 chaves**. É assim que ela aparece no arquivo
(neste exemplo, com a **alternativa local** configurada: `127.0.0.1`, `qwen2.5-3b-instruct` e a chave vazia — no
**padrão** o `BaseUrl` é o OpenRouter e o `ApiKey` é obrigatório):

![A seção [IA] do .cfg: Ligado = true, BaseUrl = http://127.0.0.1:1234/v1, Modelo = qwen2.5-3b-instruct e ApiKey vazio](images/config-ia.png)

---

## Editando em jogo

Você não precisa abrir o `.cfg` no bloco de notas para as coisas do dia a dia:

| Painel | Tecla | O que edita |
|---|---|---|
| **Configurações do mod** | **`F8`** | **Todas** as opções do mod, em jogo |
| **Painel da IA** | **`I`** | A camada de IA: **Configurações do LLM** (URL, modelo, chave), personalidade, voz, volume, velocidade, memória, conversa e push-to-talk |

### Painel da IA (tecla `I`)

| O que | Como |
|---|---|
| **Personalidade** | Escolha um **preset** (Padrão, Viking leal, Mago sarcástico, Sarcástico, Guia paciente) ou escreva nas **suas palavras** e clique em **Aplicar** — vale na próxima resposta |
| **Voz** | Escolha entre as **3 vozes pt-BR** e aperte **Ouvir** para escutar uma frase de exemplo na hora |
| **Volume / Velocidade** | Sliders (o volume vale no próximo áudio, a velocidade na próxima fala) |
| **Push-to-talk** | Veja a tecla atual e aperte **Alterar** para capturar outra; um toggle liga/desliga as ordens por voz |
| **Liga/desliga** | Companheiro falar, falar respostas da IA, falar falas do mod, conversa livre |
| **Memória** | Quantas trocas recentes ele lembra (0–12) |

Tudo é salvo no `.cfg` **na hora**. O painel fecha com **`Esc`** ou no **X**, e **não precisa de companheiro
perto** (é uma configuração global).

> As chaves de **texto** (`BaseUrl`, `Modelo`, `Personalidade`…) e as de **tecla** também aparecem no `F8`, mas
> o próprio mod avisa: para texto longo, editar o `.cfg` é mais confortável.

---

## Seção `[IA]` — todas as chaves

| Chave | Padrão | O que faz |
|---|---|---|
| `Ligado` | `true` | Liga as ordens por texto. Desligado = nada é enviado ao modelo |
| `BaseUrl` | `https://openrouter.ai/api/v1` | Endpoint do LLM. `/chat/completions` é adicionado sozinho. O padrão é o **OpenRouter**; para rodar local, troque por `http://127.0.0.1:1234/v1` (LM Studio) ou `http://127.0.0.1:11434/v1` (Ollama). **No caso local** use **`127.0.0.1`** e **não** `localhost`: no Windows o `localhost` resolve primeiro para IPv6 e o servidor do LM Studio só escuta IPv4, o que custa ~**2,5 s** em cada ordem (veja **[LLM (OpenRouter ou LM Studio)](LLM-no-LM-Studio)**) |
| `Modelo` | `openrouter/free` | Nome do modelo, como o servidor lista. `openrouter/free` é o **roteador de modelos gratuitos** do OpenRouter. No LM Studio use o nome que ele mostra (ex.: `qwen2.5-3b-instruct`) |
| `ApiKey` | *(vazio)* | Chave de API do servidor. **Obrigatória no OpenRouter** (a gratuita sai em `openrouter.ai/keys`); vazia para LM Studio/Ollama local |
| `Temperatura` | `0.1` | O quanto o modelo improvisa. Mantenha baixo para a mesma ordem dar sempre o mesmo JSON |
| `TimeoutSegundos` | `30` | Segundos de espera pelo modelo. O pedido roda em segundo plano |
| `Prefixo` | *(vazio)* | Vazio = toda mensagem de chat vai ao modelo. Ex.: `/c` exige o prefixo |
| `MostrarRespostaNoChat` | `true` | Mostra a resposta crua do modelo no chat (útil em testes) |
| `CooldownSegundos` | `3` | Tempo mínimo entre duas ordens |
| `RaioDeAlvo` | `40` | Até onde (m) procurar a criatura citada numa ordem de ataque. A que você mira vale sempre |
| `Personalidade` | *(vazio)* | A personalidade **nas suas palavras** (PT-BR, até 600 caracteres). Ganha do preset |
| `PresetPersonalidade` | *(vazio)* | `viking`, `mago`, `sarcastico`, `guia` ou `custom`. Vazio/desconhecido = o padrão (leal e prático) |
| `Memoria` | `4` | Quantas trocas recentes ele lembra e envia junto da próxima mensagem. `0` = sem memória (teto 12) |
| `Conversa` | `true` | Responder conversa livre quando a mensagem não é ordem |
| `SaidaEstruturada` | `true` | Pede ao servidor o formato JSON (`response_format`/`json_schema`). É o que permite usar modelos pequenos |
| `SttTeste` | `false` | Teste: roda o reconhecimento do Whisper sozinho ~25 s após carregar e escreve no log |
| `SttModelo` | `whisper\ggml-small-q5_1.bin` | Modelo do Whisper (relativo à pasta do plugin ou caminho absoluto) |
| `SttWavTeste` | `whisper\jfk.wav` | WAV que o `modia_stt_teste` transcreve. **O pacote não traz esse arquivo** — aponte para um WAV seu |
| `VozLigada` | `true` | Liga o push-to-talk |
| `VozTecla` | `V` | Tecla do push-to-talk |
| `VozIdioma` | `pt` | Idioma da fala (código do Whisper). Vazio ou `auto` detecta |
| `VozDuracaoMaxima` | `10` | Teto da gravação, em segundos |
| `VozLimiarSilencio` | `0.01` | Quão alto o áudio precisa ser para contar como fala |
| `VozPromptInicial` | *(vocabulário das ordens)* | Prompt inicial do Whisper: enviesa o reconhecimento para as palavras das ordens |
| `VozDispositivo` | `0` | Índice do microfone (o log imprime a lista) |
| `VozMostrarTexto` | `true` | Mostra no chat/HUD o que o microfone ouviu |
| `PainelTecla` | `I` | Tecla que abre o **painel da IA** |
| `TtsLigado` | `true` | Ele **fala** as respostas (TTS local, Kokoro) |
| `TtsVoz` | `42` | Voz: `42` pf_dora (fem.), `43` pm_alex (masc.), `44` pm_santa (masc.) |
| `TtsVelocidade` | `1` | Velocidade da fala |
| `TtsThreads` | `4` | Núcleos da CPU para a síntese |
| `TtsVolume` | `1` | Volume da voz (0 = mudo) |
| `TtsModelo` | `tts\kokoro\model.int8.onnx` | Modelo do Kokoro. Vozes/tokens/espeak na **mesma pasta** |
| `TtsIdioma` | `pt-br` | Idioma do texto falado. **Nunca vazio** |
| `TtsCache` | `true` | Guarda as falas já sintetizadas |
| `TtsFalarRespostas` | `true` | Fala a resposta do modelo |
| `TtsFalarFalasDoMod` | `true` | Fala a linha do mod que aparece no chat (ganha numa ordem) |
| `TtsReverbZoneMix` | `0` | Quanto das zonas de reverb do mundo entra na voz. `0` = seca; `0,19` = um "espaço" sutil |
| `EcoDiag` | `false` | Diagnóstico de áudio: escreve no log (uma vez por sessão) o `AudioListener`, as zonas de reverb e como as fontes do jogo estão configuradas |

---

## O que **nós** mudamos em relação ao upstream

Além da tradução e da camada de IA, o projeto ajustou alguns padrões do upstream. Se você veio do Offline
Companions Reborn, isto é o que mudou de valor:

| Seção / chave | Padrão do upstream | Nosso padrão | Por quê |
|---|---|---|---|
| `[Advanced] GatheringSearchRadius` | 30 | **50** | Seguindo você, o companheiro pode estar a 25 m e o alvo a 25 m do outro lado — o anel de busca precisa cobrir ~50 m |
| `[Gathering] FollowRange` | 10 | **25** | Ele trabalha a até 25 m de você, em vez de ficar colado |
| `[Gathering] ForageFollowRange` | 10 | **20** | Forrageio continua sendo "o que você passou perto", mas com folga |
| `[Gathering] Range` | 40 | **60** | Alcance da coleta com **Seguir desligado** |
| `[Advanced] GatheringDropRadius` | 8 | **12** | Uma árvore pode cair mais longe que 8 m; os drops ficavam para trás |
| `[Looting] CollectRadius` | *(novo)* | **10** | Raio da ordem "recolhe os itens do chão" |
| `[Speech] Chatter` | `true` | **`false`** | O chatter aleatório (falinhas a cada 15–30 s) vinha só de um timer. As falas úteis continuam |
| `[Advanced] UiDiag` | *(novo)* | **`false`** | Diagnóstico de UI (cliques/camadas): opt-in, zero custo desligado |
| `[IA] EcoDiag` | *(novo)* | **`false`** | Diagnóstico de áudio do companheiro: opt-in |

> Os cinco raios de coleta são migrados automaticamente **só** se estiverem exatamente no valor antigo. Um
> valor que você escolheu à mão **nunca** é sobrescrito.

Os dois diagnósticos (`UiDiag` e `EcoDiag`) são **opt-in**: desligados não escrevem nada e não custam nada;
ligados escrevem **uma vez por sessão** e são a primeira coisa a olhar quando um clique ou um som parece
estranho.

---

## Vendo tudo no painel `F8`

O painel **`F8`** mostra **todas** as seções do `.cfg`, com os mesmos nomes de chave e os mesmos limites de
valor. É a forma mais rápida de conferir o que está valendo agora, sem sair do jogo.

![O painel F8 aberto na seção [IA], com Ligado, Temperatura, TimeoutSegundos, Memoria, VozLigada e as demais chaves](images/3-painel-f8.jpg)

> Os painéis do mod ficam **acima do HUD do jogo** na ordem de desenho — é o que garante que um clique no
> painel chegue no painel, e não no HUD.
