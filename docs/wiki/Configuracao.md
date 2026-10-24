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
**padrão** o `BaseUrl` é o **Groq**, com o modelo `openai/gpt-oss-20b`, e a `ApiKey` é obrigatória):

![A seção [IA] do .cfg: Ligado = true, BaseUrl = http://127.0.0.1:1234/v1, Modelo = qwen2.5-3b-instruct e ApiKey vazio](images/config-ia.png)

---

## Editando em jogo

Você não precisa abrir o `.cfg` no bloco de notas para as coisas do dia a dia:

| Painel | Tecla | O que edita |
|---|---|---|
| **Configurações do mod** | **`F8`** | **Todas** as opções do mod, em jogo |
| **Painel da IA** | **`I`** | A camada de IA e **cada companheiro**: a **aba IA** (invocador, Configurações do LLM, voz global e push-to-talk) e **uma aba por companheiro** (nome, tipo, aparência, voz e personalidade dele) |

### Painel da IA (tecla `I`)

O painel abre na **aba IA** (sem precisar de companheiro perto) e tem, no topo, **uma aba por companheiro** — quando
uma dessas abas está aberta, o título passa a ser o **nome dele**. As abas rolam na horizontal quando há muitos
companheiros. Tudo é salvo **na hora**: as opções que valem para todos ficam na **aba IA**, e o que é de um
companheiro fica na **aba dele**.

#### Aba IA

| O que | Como |
|---|---|
| **Invocar novo companheiro** | Escolha o **Tipo** e aperte **Invocar**: ele nasce **de graça**, sem passar pelo Trader, com o nome do tipo numerado ("Guerreiro 2"). O contador abaixo mostra quantos estão **na cena**; o teto é `[General] MaxCompanions` (padrão **10**, contando **vivos + mortos + perdidos**) |
| **Configurações do LLM** | Os toggles **Ligado**, **Saída estruturada** e **Mostrar resposta no chat**; os campos **URL do servidor**, **Modelo**, **Chave de API** (**mascarada**) e **Prefixo**; os sliders **Temperatura**, **Timeout (s)**, **Cooldown (s)** e **Raio de alvo (m)**; e os botões **Aplicar** e **Testar conexão** |
| **Selecione seu Provedor LLM** | O dropdown logo **acima da URL do servidor**: escolher **Groq**, **LM Studio** ou **OpenRouter** **preenche** a URL e o modelo (veja a tabela abaixo). **Personalizado (URL manual)** não mexe em nada |
| **Volume da fala** | O slider do TTS (`[IA] TtsVolume`). A **velocidade da fala** fica **só no painel `F8`** (`[IA] TtsVelocidade`) — o botão **Ouvir** usa o valor de lá |
| **Aperte para falar** | A tecla do push-to-talk (padrão **`V`**), o botão **Alterar** para trocar e o toggle **Ordens por voz** |
| **Companheiro fala / Falar respostas da IA / Falar falas do mod / Conversa livre** | Os toggles da voz e da conversa (`TtsLigado`, `TtsFalarRespostas`, `TtsFalarFalasDoMod`, `Conversa`) |
| **Memória** | Quantas trocas recentes ele lembra (0–12) |

> Os campos de texto (URL, modelo, chave, prefixo) só valem depois do **Aplicar** — e na **próxima mensagem**,
> sem reiniciar o jogo. A chave nunca vai para o log.

**Seletor de provedor** (o subtítulo **"Selecione seu Provedor LLM"**, com o mesmo dropdown do **Tipo** do
invocador) — ele **abre já no provedor que o seu `.cfg` usa** e, ao escolher, preenche os campos:

| Provedor | URL | Modelo |
|---|---|---|
| **Groq** (padrão) | `https://api.groq.com/openai/v1` | `openai/gpt-oss-20b` |
| **LM Studio** | `http://127.0.0.1:1234/v1` | `qwen2.5-3b-instruct` |
| **OpenRouter** | `https://openrouter.ai/api/v1` | `openrouter/free` |
| **Personalizado (URL manual)** | *(não mexe em nada)* | *(não mexe em nada)* |

> Escolher no seletor **só preenche os campos**: vale depois do **Aplicar**, como qualquer edição ali. A
> **`ApiKey` nunca é tocada** (nem preenchida, nem apagada) — ela continua sendo sua. E o seletor **não é uma
> config nova**: ele **deduz** o provedor da própria `[IA] BaseUrl`, então uma URL de **Ollama** ou de um servidor
> seu aparece como **Personalizado (URL manual)** e nada é sobrescrito — não existe um segundo lugar que possa
> divergir do `.cfg`.

**Botão "Crie sua Chave de API Grátis AQUI"** — na linha da **Chave de API**, na metade direita: ele **abre o
navegador** na página de chaves do provedor escolhido (**Groq** → `console.groq.com/keys`; **OpenRouter** →
`openrouter.ai/keys`), para você pegar a chave sem sair do jogo. Ele **não aparece** no **LM Studio** nem no
**Personalizado** (esses não têm chave) e **não é um seletor**: clicar não muda estado nenhum depois.

![O painel da IA na aba IA: o seletor "Selecione seu Provedor LLM" em Groq, o invocador de companheiros, a URL do servidor, o modelo openai/gpt-oss-20b, a chave de API mascarada, o botão "Crie sua Chave de API Grátis AQUI", Aplicar e Testar conexão](images/6-painel-ia.jpg)

#### Aba do companheiro

| O que | Como |
|---|---|
| **Nome** | O campo com o nome atual + **Aplicar** (o **Enter** também aplica). O nome vale no HUD, no hover, na roda e nas ordens pelo nome. Companheiro **morto** não pode ser renomeado |
| **Tipo** | As setas `<` `>` escolhem e **Trocar tipo (renasce)** aplica, **com confirmação**: ele renasce como o tipo novo e **fica tudo** (nome, inventário, equipamento, aparência, voz, personalidade, casa, cama, skills) — só o **trabalho em andamento** recomeça |
| **Estado** | **Vivo** ou **Morto** — e morrer **não** abre vaga no teto |
| **Chamar** | Traz o companheiro para perto de você: ele vem andando e, se estiver longe demais, volta pelo mesmo chamado do jogo |
| **Aparência** | Abre o **editor de aparência** (logo abaixo) |
| **Personalidade** | A lista com **"Seguir o padrão ([IA])"** na frente e os presets (`viking`, `mago`, `sarcastico`, `guia`) — **escolher já salva**. **Nas suas palavras** é o campo livre, aplicado pelo **Aplicar** ao lado |
| **Voz** | A lista com **"Seguir o padrão ([IA])"** na frente e as **3 vozes pt-BR** — escolher já salva e vale na **próxima fala**; **Ouvir** toca uma frase de exemplo na hora |

> A voz e a personalidade escolhidas aqui são **deste** companheiro, guardadas no **ZDO** dele: elas viajam na
> **lápide** e voltam no respawn. Quem está em "seguir o padrão" obedece ao que está na **aba IA** — mudar o global
> alcança todos os que não têm escolha própria. **Chamar** e **Aparência** ficam desabilitados com o companheiro
> morto.

**Editor de aparência** (o botão **Aparência**): gênero, corte de cabelo, estilo de barba, tom de pele, tom e
sombra do cabelo, com um **boneco 3D** que muda ao vivo enquanto você escolhe. **Aplicar** grava no companheiro
**sem respawn**; **Cancelar** fecha sem mudar nada.

O painel fecha com **`I`**, com o **`Esc`** ou no **X** — e o `Esc` primeiro sai de um campo de texto e fecha uma
lista aberta, depois o painel. A tecla **`I`** é ignorada enquanto você digita (chat, console, busca do comerciante,
os campos do próprio painel) e com o **inventário**, o **mapa**, o **menu**, o painel **`F8`** ou o seletor do
primeiro companheiro abertos; e o painel **se fecha sozinho** quando você morre ou sai do mundo.

> O que não está no painel `I` continua no **`F8`**, que edita **todas** as seções do `.cfg` com os mesmos nomes de
> chave — a dica no rodapé do painel diz exatamente isso.

---

## Seção `[IA]` — todas as chaves

| Chave | Padrão | O que faz |
|---|---|---|
| `Ligado` | `true` | Liga as ordens por texto. Desligado = nada é enviado ao modelo |
| `BaseUrl` | `https://api.groq.com/openai/v1` | Endpoint do LLM. `/chat/completions` é adicionado sozinho. O padrão é o **Groq** (que pede a `ApiKey`); as alternativas são o **LM Studio** (`http://127.0.0.1:1234/v1`) ou o **Ollama** (`http://127.0.0.1:11434/v1`) para rodar tudo local, e o **OpenRouter** (`https://openrouter.ai/api/v1`), que funciona mas tem o tier gratuito mais lento. **No caso local** use **`127.0.0.1`** e **não** `localhost`: no Windows o `localhost` resolve primeiro para IPv6 e o servidor do LM Studio só escuta IPv4, o que custa ~**2,5 s** em cada ordem (veja **[LLM (Groq, LM Studio ou OpenRouter)](LLM-no-LM-Studio)**) |
| `Modelo` | `openai/gpt-oss-20b` | Nome do modelo, como o servidor lista. `openai/gpt-oss-20b` é o padrão (Groq, tier gratuito). Alternativas: `openrouter/free` no OpenRouter; no LM Studio use o nome que ele mostra (ex.: `qwen2.5-3b-instruct`) |
| `ApiKey` | *(vazio)* | Chave de API do servidor. **Obrigatória no Groq** (a gratuita sai em `console.groq.com/keys`) e também no OpenRouter (`openrouter.ai/keys`); vazia para LM Studio/Ollama local |
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

## Peso e capacidade de carga

O limite de peso do companheiro é **do mod**, não do jogo: o vanilla **não** impõe teto a um `Humanoid` que não é
jogador. Duas chaves decidem o comportamento e **as duas são o valor base** — itens com **bônus de carga** somam
**por cima** delas:

| Seção / chave | Padrão | O que faz |
|---|---|---|
| `[General] MaxCarryWeight` | `300` | O teto **base** de peso do companheiro (50–2000). É o `x/300` do painel e da barra do **HUD** e o que dispara o **`Encumbered`** (andar lento). O **Megingjord** (+150) soma por cima: no padrão, o teto efetivo dele vira **450** |
| `[Gathering] OverweightThreshold` | `298` | O peso **base** em que o companheiro de **coleta** (madeira, pedra, minério, forragem e a roça da horta) para de pegar e volta para casa para descarregar (50–500). Com o **Megingjord** na mochila ele só volta perto de **448**. **Caça e pesca não usam esta chave** (elas têm as próprias opções de mochila cheia) |

> **O bônus vale com o item em qualquer lugar da mochila** — não precisa equipar no slot de utilidade (num
> `Humanoid` que não é jogador ele nem sobrevive ao reload). E vale **uma vez só**: dois cintos iguais **não somam**,
> como o jogo trata os efeitos de equipamento. O valor é lido do **asset** (`SE_Stats.m_addMaxCarryWeight`), nunca
> de um `150` fixo — um cinto modificado para +250 funciona igual.
>
> O teto efetivo vale no **`Encumbered`**, no `x/450` do **painel do companheiro** e da barra do **HUD**, na recusa
> de **loot** e do **recolhimento do chão**, no **ferreiro**, no **upkeep**, no "mochila cheia" da **pesca/caça** e
> no aviso de "estou cheio". Cada mudança de bônus escreve **uma linha** no log (nunca por frame):
> `[Carry] "Thorin": +150 de capacidade (Megingjord) - teto 450`.

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
| `[General] MaxCompanions` | *(novo)* | **10** | Teto de companheiros por jogador, contando **vivos + mortos + perdidos** (morrer não abre vaga) |
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
