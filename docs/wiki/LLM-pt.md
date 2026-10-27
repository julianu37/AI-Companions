> 🌍 [English](LLM) · **Português** · [Español](LLM-es) · [Français](LLM-fr) · [Italiano](LLM-it)

# LLM (LM Studio, Groq ou OpenRouter)

O mod entende as ordens mandando o texto para um **LLM** por uma API **compatível com a OpenAI**. Há três
caminhos, e você escolhe **um**:

| Caminho | O que precisa | Para onde vai o texto | Quando escolher |
|---|---|---|---|
| **LM Studio** (ou Ollama) — **recomendado** | o modelo baixado e o servidor ligado | fica **na sua máquina** | Quer privacidade, sem chave e sem limite de pedidos |
| **Groq** — nuvem | internet e uma **chave gratuita** | **sai da sua máquina** (serviço remoto) | Quer funcionando sem instalar nada, e rápido |
| **OpenRouter** — alternativa | internet e uma **chave gratuita** | **sai da sua máquina** (serviço remoto) | Já usa o OpenRouter ou quer testar outro modelo |

A página começa pelo **LM Studio**, o caminho recomendado: o modelo roda no seu computador e nada sai dele. Depois vem
o **Groq**, o serviço de nuvem que só precisa da chave gratuita, e por último o **OpenRouter** — que também funciona,
mas cujo tier gratuito é **lento**.

> **Já tem um `.cfg` de uma versão anterior (ou da 1.8.2)?** Na **1.8.3** o arquivo passou a ser
> **`AiCompanions.cfg`** e as configurações **voltam aos padrões** — inclusive a `ApiKey`, que precisa ser colada
> de novo. A seção também mudou de nome: era `[IA]`, agora é **`[AI]`**, com as chaves em **inglês** (`Model`, não
> `Modelo`). O `.cfg` antigo (`Maddhattr.OfflineCompanionsReborn.cfg`) fica no disco e **não** é lido.
> A **1.9.x não zera nada**: se o arquivo já é o `AiCompanions.cfg`, ele fica como está (só os padrões fixos antigos
> de voz/idioma viram `auto`).
> Trocar de provedor é só editar `BaseUrl`, `Model` e `ApiKey` (no `.cfg` ou na seção **Configurações do LLM** do
> painel `I`).

---

## 1. LM Studio: local, privado e recomendado

O **LM Studio** é um aplicativo que roda o modelo **no seu próprio computador**: nada sai da sua máquina, não há
chave nem fila, e não há limite de pedidos ou de tokens. Os prints abaixo são do **LM Studio 0.4.26**.

### 1.1. Instale o LM Studio

Baixe em **https://lmstudio.ai** e instale. É um aplicativo de janela, sem linha de comando.

### 1.2. Baixe o modelo

Na barra lateral, abra **Model Search** (o ícone de lupa), procure **`qwen2.5-3b-instruct`** e escolha
**`lmstudio-community/Qwen2.5-3B-Instruct-GGUF`**.

*O item **Model Search** na barra lateral.*

![LM Studio 0.4.26 com o item Model Search destacado na barra lateral](images/lmstudio-1-model-search.png)

A página do modelo mostra *Full GPU Offload Possible*: baixe a quantização **Q4_K_M** (**~1,93 GB**).

*O resultado da busca, com a quantização **Q4_K_M** (1,93 GB) e o botão **Download**.*

![LM Studio Model Search: a busca por qwen2.5-3b-instruct, o resultado lmstudio-community/Qwen2.5-3B-Instruct-GGUF, a quantização Q4_K_M de 1,93 GB e o botão Download](images/lmstudio-2-download-qwen.png)

> **Por que um modelo tão pequeno?** Porque ele é **rápido** (ordens em ~0,35–0,4 s) e, com a **saída estruturada** do
> mod, responde no formato certo de forma confiável. Um 3B roda bem até em CPU.

### 1.3. Ligue o Local Server

Na barra lateral, abra **Local Server** e confira que o **Status** é **Running** (verde) na porta **1234**.

*O servidor no ar: **Status: Running** na porta 1234.*

![Tela Model Server do LM Studio: Status Running (verde) na porta 1234, o botão + Load Model destacado e Loaded Models ainda vazio](images/lmstudio-3-local-server.png)

### 1.4. Carregue o modelo

Aperte **+ Load Model** e escolha **Qwen2.5-3B-Instruct Q4_K_M**.

*A lista do **+ Load Model**, com o `Qwen2.5 3B Instruct Q4_K_M` selecionado.*

![Janela Load Model do LM Studio: Qwen2.5 3B Instruct Q4_K_M destacado na lista de modelos](images/lmstudio-4-load-model.png)

### 1.5. Confirme o API Identifier e carregue

A tela de carga mostra o **API Identifier** **`qwen2.5-3b-instruct`** — é **esse** nome que vai no `[AI] Model` (o nome
exato que o servidor expõe, não o nome da pasta do download). O **Context Length** é **8192**, o **GPU Offload** está
no máximo e a memória estimada é de cerca de **2 GB** de RAM/VRAM.

*A tela de carga: **API Identifier** `qwen2.5-3b-instruct`, **Context Length** 8192 e **GPU Offload** no máximo.*

![Tela de carga do LM Studio: API Identifier qwen2.5-3b-instruct, Context Length 8192, GPU Offload 36, uso de memória estimado de 2,03 GB e o botão Load Model](images/lmstudio-5-load-settings.png)

Aperte **Load Model**. Quando terminar, **Loaded Models** mostra **READY** e `llm qwen2.5-3b-instruct` (1,93 GB) — pode
ir para o jogo.

*O modelo pronto: **READY**, `llm qwen2.5-3b-instruct`, **Size 1,93 GB** e **Parallel 4**.*

![Loaded Models do LM Studio: o selo READY, llm qwen2.5-3b-instruct, Size 1,93 GB e Parallel 4; à direita, o API Model Identifier qwen2.5-3b-instruct](images/lmstudio-6-ready.png)

### 1.6. Aponte o mod para ele

No jogo, abra o painel **`I`** → **Configurações do LLM** → o dropdown de provedor → **LM Studio** (ele preenche a URL
e o modelo) → **Aplicar** → **Testar conexão**. Ou edite o `.cfg`, seção `[AI]`:

```ini
BaseUrl = http://127.0.0.1:1234/v1
Model   = qwen2.5-3b-instruct
ApiKey  = (vazio)
```

> **O mod nasce apontado para o Groq** (`https://api.groq.com/openai/v1` + `openai/gpt-oss-20b`): por isso você escolhe
> o **LM Studio** aqui (no dropdown ou no `.cfg`) — é uma mudança de dois minutos.

> O **Testar conexão** do painel `I` funciona igual aqui: mude a URL e o modelo nos campos, teste e só depois clique
> em **Aplicar**.

---

## 2. Groq: só a chave e a internet (nuvem)

O **Groq** é um serviço que fala a **mesma API da OpenAI** e serve modelos abertos, com um **tier gratuito**. Para o
mod, é a opção de nuvem mais simples: não há nada para instalar nem servidor para deixar aberto — e o `.cfg` já vem
apontado para ele.

### 2.1. Crie a sua chave gratuita

1. Abra **https://console.groq.com** e crie a conta (e-mail ou login social).
2. Vá em **https://console.groq.com/keys** e clique em **Create API Key** (dê um nome qualquer, ex.: `Valheim`).
3. **Copie a chave gerada** — é ela que vai no `[AI] ApiKey`. Ela aparece **uma vez só**; se fechar a janela sem
   copiar, crie outra.

### 2.2. O que colocar no `.cfg` (ou no painel)

Abra `<Valheim>\BepInEx\config\AiCompanions.cfg` e confira a seção `[AI]`:

```ini
[AI]
BaseUrl = https://api.groq.com/openai/v1
Model = openai/gpt-oss-20b
ApiKey = <a sua chave>
StructuredOutput = true
```

| Chave | Valor | Por quê |
|---|---|---|
| `BaseUrl` | `https://api.groq.com/openai/v1` | Endereço do Groq. O `/chat/completions` é adicionado sozinho |
| `Model` | `openai/gpt-oss-20b` | O modelo aberto da OpenAI servido pelo Groq. É rápido e obedece bem à saída estruturada |
| `ApiKey` | **a sua chave** | No Groq ela é **obrigatória**. Vazia ou errada, o servidor recusa o pedido com erro de autenticação |
| `StructuredOutput` | `true` | Pede o formato JSON ao servidor — é o que faz modelos pequenos funcionarem. O `gpt-oss-20b` aceita `json_schema` em modo **strict** (decodificação restrita), que é o modo que o mod usa |

Você também pode fazer isso **em jogo**, sem abrir o arquivo: tecla **`I`** → aba **IA** → **Configurações do
LLM**. É onde ficam a **URL do servidor**, o **Modelo**, a **Chave de API** e os botões **Aplicar** e **Testar
conexão** — e, **acima da URL**, o dropdown **"Selecione seu Provedor LLM"**: escolher **Groq**, **LM Studio** ou
**OpenRouter** **preenche a URL e o modelo** sozinho (a **chave não é tocada** e nada é gravado antes do
**Aplicar**). Na linha da **Chave de API** há o botão **"Crie sua Chave de API Grátis AQUI"**, que **abre no
navegador** a página de chaves do provedor escolhido (`console.groq.com/keys` no Groq, `openrouter.ai/keys` no
OpenRouter) — some no LM Studio, que não tem chave.

> Os dois são **só um atalho**: o `.cfg` continua valendo e é ele que manda. O seletor **deduz** o provedor da
> própria `BaseUrl`, então uma URL de **Ollama** ou de um servidor seu aparece como **Personalizado (URL manual)**
> e nada é sobrescrito.

![O painel da IA (tecla I), aba IA: o seletor "Selecione seu Provedor LLM" em Groq, a URL do servidor com api.groq.com, o modelo openai/gpt-oss-20b, a chave de API mascarada e, à direita da chave, o botão "Crie sua Chave de API Grátis AQUI"](images/6-painel-ia.jpg)

> No print acima o seletor está em **Groq** (o provedor que vem no `.cfg`), com a URL `https://api.groq.com/openai/v1`, o modelo
> `openai/gpt-oss-20b` e a chave mascarada. Trocar o seletor para **LM Studio** ou **OpenRouter** preenche a URL e o
> modelo daquele provedor — e o botão da chave passa a apontar para a página dele (ou some, no LM Studio).

> `BaseUrl`, `Model` e `ApiKey` também aparecem no painel **`F8`**, mas são chaves de **texto**: para colar uma
> chave, o campo do painel `I` (ou o próprio `.cfg`) é mais confortável. O que você salva ali vale na **próxima
> mensagem**, sem reiniciar o jogo.

### 2.3. Teste a conexão

Com a URL, o modelo e a chave preenchidos, aperte **Testar conexão** (no painel `I`, ao lado de **Aplicar**). O
teste roda **em segundo plano** — o jogo não trava — e responde no **chat/HUD**, além de deixar uma linha `[IA]
Conexao:` no log. Ele começa pedindo a **lista de modelos** ao servidor e, se o servidor não tiver essa lista,
manda um pedido mínimo de chat. O resultado é um destes:

- **Conexão OK: N modelo(s) no servidor** — a URL respondeu.
- **Conexão OK: … e o modelo configurado está na lista** — o melhor caso.
- **Conexão OK: … mas "<modelo>" não está na lista** — a URL respondeu, mas o nome não apareceu na lista que o
  servidor devolve. Confira se o `Model` está escrito exatamente `openai/gpt-oss-20b` e siga para o teste em jogo,
  mandando uma ordem de verdade.
- **Falha na conexão: …** — vem com o motivo (status/mensagem do servidor): URL errada, chave vazia/errada,
  endereço sem `http://`/`https://`…

> **Quem confirma a chave é uma ordem de verdade.** O **Testar conexão** diz que a URL responde e que o modelo está
> na lista; se ele passar mas a ordem falhar com **401**, o problema é a chave — veja
> **[Problemas comuns](Troubleshooting-pt)**.

### 2.4. O que esperar da resposta

O entendimento continua rodando **em segundo plano**: um modelo lento **não** trava o jogo. A latência aparece no
log, em cada ordem:

```
[IA] Tempo: 378 ms (… tokens, … tokens/s)
```

Com o `openai/gpt-oss-20b` no Groq a resposta chega em **fração de segundo** na nossa medição — bem mais rápido que
o tier gratuito do OpenRouter. O modelo é servido a ~**1.000 tokens/s** e tem janela de **131.072 tokens** (máximo
de conclusão 65.536), o que sobra para o prompt do mod (fonte: https://console.groq.com/docs/models). O mod avisa no
log quando uma ordem passa de **5 s**, e o `[AI] TimeoutSeconds` (padrão **30**) é o teto de espera.

### 2.5. Limites do tier gratuito

O que a documentação do Groq informa hoje para o `openai/gpt-oss-20b` no plano gratuito (fonte:
https://console.groq.com/docs/rate-limits — os números podem mudar):

| Limite | Valor |
|---|---|
| Requisições por minuto (**RPM**) | **30** |
| Requisições por dia (**RPD**) | **1.000** |
| Tokens por minuto (**TPM**) | **8.000** |
| Tokens por dia (**TPD**) | **200.000** |

Na prática, **o TPM é o limite que morde primeiro**: o prompt do mod gasta cerca de **1.800 tokens por ordem**, o
que dá **~4 ordens por minuto**. Acima disso o Groq responde **429** e a ordem é recusada com aviso — nada quebra e
nada fica preso; espere um pouco e mande de novo.

- O teto de **8.000 tokens por minuto** atende uma sessão normal de ordens esporádicas. Uso intenso (muitas ordens
  e conversa o tempo todo) esbarra nele — aí a **opção local** (seção 1) resolve, sem limite de pedidos.
- O `[AI] CooldownSeconds` (padrão **3**) ajuda: ele evita que uma rajada de mensagens vire uma rajada de pedidos.
- **429** é limite de requisições/tokens, **401** é chave — a diferença está em **[Problemas comuns](Troubleshooting-pt)**.

> **A saída estruturada continua valendo.** O `gpt-oss-20b` aceita `json_schema` em modo **strict** e também o modo
> *best-effort*; **streaming** e **tool use** não são suportados junto com a saída estruturada — e o mod **não** usa
> nenhum dos dois (fonte: https://console.groq.com/docs/structured-outputs).

### 2.6. Privacidade e a chave

- **Com o Groq, o texto das ordens e da conversa sai da sua máquina.** Ele vai para os servidores do Groq — a
  persona, a memória e o que você digita entram nesse envio. **Não escreva nada sensível** no chat do companheiro.
- O Groq é um **serviço remoto**: a política de dados dele é que vale, não a do mod.
- O reconhecimento de voz (**STT**) e a **voz do companheiro** (**TTS**) continuam **locais**: só o texto da ordem
  vai para o LLM.
- **A chave fica no `.cfg`.** Não compartilhe esse arquivo com a chave preenchida e nunca versione uma chave. Se ela
  vazar, apague em **https://console.groq.com/keys** e crie outra.
- Quer **nada saindo** da máquina? Use o LM Studio (ou o Ollama) na seção 1 — aí o LLM também é local.

---

## 3. OpenRouter (continua funcionando, mas o gratuito é lento)

O **OpenRouter** foi o padrão até a **1.7.0** e **continua funcionando**. Ele dá acesso a vários modelos com uma
mesma chave, inclusive os **gratuitos** — mas o roteador gratuito (`openrouter/free`) é **lento e imprevisível**:
cada pedido escolhe um modelo gratuito diferente e passa pela fila do provedor. Por isso ele saiu do lugar de
padrão. Use se você **já tem** a configuração ou quer **testar outro modelo** do catálogo.

```ini
[AI]
BaseUrl = https://openrouter.ai/api/v1
Model = openrouter/free
ApiKey = <a sua chave de openrouter.ai/keys>
```

| Chave | Valor | Por quê |
|---|---|---|
| `BaseUrl` | `https://openrouter.ai/api/v1` | Endereço do OpenRouter |
| `Model` | `openrouter/free` | **Free Models Router**: a cada pedido ele escolhe um modelo **sem custo** (e filtra os que suportam a saída estruturada) |
| `ApiKey` | **a sua chave** | No OpenRouter ela é **obrigatória**. A gratuita sai em **https://openrouter.ai/keys** |

- Os modelos **gratuitos** (com `:free` no nome) têm limite de **20 pedidos por minuto** e um **teto diário** por
  conta: **50 pedidos por dia** para quem **nunca comprou crédito** e **1.000 por dia** a partir de **10 créditos
  comprados** (no total, não por mês).
- A latência **varia** de uma ordem para outra e pode demorar alguns segundos em horário de pico.
- O próprio OpenRouter descreve os modelos gratuitos como **para experimentar e para uso de baixo volume**.
- O **Testar conexão** do painel `I` também serve aqui — e, como a lista de modelos do OpenRouter é **pública**, ele
  pode responder `Conexão OK` mesmo com a chave errada: quem confirma a chave é uma ordem de verdade.
- Com o OpenRouter, o texto das ordens e da conversa **sai da sua máquina**, como no Groq. A política de dados dele
  é que vale. Chave vazada? Apague em **https://openrouter.ai/settings/keys** e crie outra.

---

## 4. Como saber que está funcionando

Entre no mundo e digite no chat `colete 10 madeiras` (vale para o Groq, para o **LM Studio** e para o
**OpenRouter**). Se funcionar:

- O companheiro responde (e fala) uma confirmação e sai para coletar.
- No log (`BepInEx\LogOutput.log`) aparecem as linhas `[IA]` com o resultado e o tempo.

---

## 5. Outros modelos e servidores

Qualquer endpoint **compatível com a OpenAI** serve — o mod só faz `POST {BaseUrl}/chat/completions`.

### Modelos

| Modelo | Servidor | Observação |
|---|---|---|
| **`openai/gpt-oss-20b`** | **Groq** | **Opção de nuvem.** Modelo aberto, ~1.000 tokens/s, janela de 131.072 tokens. Responde em fração de segundo |
| **`qwen2.5-3b-instruct`** | LM Studio | **Recomendado.** Rápido (~0,35–0,4 s por ordem) e confiável com a saída estruturada |
| `openrouter/free` | OpenRouter | Roteador de modelos gratuitos: escolhe um por pedido. Sem instalar nada, mas a latência varia |
| `google/gemma-4-e4b` | LM Studio | Também funciona, mas é **bem mais lento** (~3 s por ordem) |
| Modelos maiores | qualquer | Melhores em conversa, mas **precisam de GPU** para responder rápido no local |
| Modelos pagos do Groq/OpenRouter | — | Basta trocar o `Model` pelo nome do catálogo — e, aí, os créditos da conta é que são gastos |

### Outros servidores

- **Ollama:** `http://127.0.0.1:11434/v1`
- **llama.cpp** com servidor no modo OpenAI
- **API paga** (OpenAI, etc.): aponte o `BaseUrl` e preencha o `ApiKey` — lembrando que, aí, as suas mensagens
  **saem da sua máquina**, como no Groq e no OpenRouter.

> **Nunca versione uma chave de API.** O `ApiKey` fica no `.cfg` local; não compartilhe esse arquivo com a chave
> preenchida.

### Ajustes úteis

| Chave `[AI]` | Padrão | O que faz |
|---|---|---|
| `Temperature` | `0.1` | Mantenha baixo: a mesma ordem deve dar sempre o mesmo JSON |
| `TimeoutSeconds` | `30` | Quanto esperar pelo modelo. O pedido roda em segundo plano — um modelo lento não trava o jogo |
| `StructuredOutput` | `true` | Se o seu servidor/modelo não suportar, o mod cai sozinho no formato antigo e avisa no log |
| `CooldownSeconds` | `3` | Tempo mínimo entre duas ordens, para uma rajada de mensagens não inundar o modelo |

---

## Se não funcionar

Os quatro casos abaixo cobrem quase tudo:

| O que aparece | O que é | O que fazer |
|---|---|---|
| **401** (*unauthorized* / *invalid api key*) | A `ApiKey` está vazia, errada ou de outro servidor | Cole a chave certa: `console.groq.com/keys` (Groq), `openrouter.ai/keys` (OpenRouter) — ou deixe **vazio** no LM Studio/Ollama |
| **429** (*rate limit*) | Limite de **requisições ou de tokens** do tier gratuito | Espere alguns segundos e mande a ordem de novo; reduza a frequência; use o LM Studio local para uso intenso |
| **404** / **400** | A **URL** (`BaseUrl`) ou o **modelo** (`Model`) estão errados | Confira `https://api.groq.com/openai/v1` + `openai/gpt-oss-20b` (Groq) ou `https://openrouter.ai/api/v1` + `openrouter/free` (OpenRouter) |
| **Demora muito** | Fila do provedor ou modelo grande | Veja "a resposta demora" em **[Problemas comuns](Troubleshooting-pt)** |

O passo a passo de cada sintoma está em **[Problemas comuns](Troubleshooting-pt)** — inclusive "ele não responde a
nada", "responde erro de autenticação", "demora muito para responder" e "responde errado ou ignora o número".
