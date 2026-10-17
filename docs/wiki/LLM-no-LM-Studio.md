# LLM no LM Studio

O mod entende as ordens mandando o texto para um **LLM local** por uma API **compatível com a OpenAI**. O
caminho mais simples é o **LM Studio** rodando na sua máquina — assim nada sai do seu computador.

Esta página mostra como instalar, baixar o modelo, ligar o servidor e o que colocar no `.cfg`.

---

## 1. Instale o LM Studio

Baixe em **https://lmstudio.ai** e instale. É um aplicativo de janela, sem linha de comando.

---

## 2. Baixe o modelo

O modelo **recomendado** é o **`qwen2.5-3b-instruct`**, quantização **Q4_K_M** (~**1,93 GB**).

No LM Studio:

1. Abra a aba de **busca de modelos** (o ícone de lupa, na barra lateral).
2. Procure por **`qwen2.5-3b-instruct`**.
3. Escolha a quantização **Q4_K_M** e baixe (~1,93 GB).

> **Por que um modelo tão pequeno?** Porque ele é **rápido** (ordens em ~0,35–0,4 s) e, com a **saída
> estruturada** do mod, responde no formato certo de forma confiável. Um 3B roda bem até em CPU.

---

## 3. Carregue o modelo

Na aba de **chat**, no topo, escolha o `qwen2.5-3b-instruct` que você baixou. Ele fica **READY** quando terminar
de carregar.

---

## 4. Ligue o Local Server

Abra a aba **Model Server** na barra lateral (nas versões antigas do LM Studio ela se chama **Developer**) e
ligue o **Local Server** (o botão **Start** / **Running**).

![O LM Studio com o Local Server ligado, o modelo carregado e o API Model Identifier](images/lmstudio-servidor.png)

Confira na tela:

| O que olhar | Valor esperado |
|---|---|
| **Status** | `Running` |
| **Porta** | `1234` |
| **Modelo carregado** | `qwen2.5-3b-instruct` (READY, ~1,93 GB) |
| **Parallel** | `4` |
| **Endpoints** | OpenAI-compatible (`/v1/models`, `/v1/chat/completions`) |
| **API Model Identifier** | `qwen2.5-3b-instruct` — é **este** nome que vai no `[IA] Modelo` |

> O **API Model Identifier** (no painel *Model Information*) é o nome exato que o servidor expõe. Use-o no
> `.cfg`, e não o nome da pasta do download.

---

## 5. O que colocar no `.cfg`

Abra `<Valheim>\BepInEx\config\Maddhattr.OfflineCompanionsReborn.cfg` e ajuste a seção `[IA]`:

```ini
[IA]
BaseUrl = http://127.0.0.1:1234/v1
Modelo = qwen2.5-3b-instruct
ApiKey =
SaidaEstruturada = true
```

| Chave | Valor | Por quê |
|---|---|---|
| `BaseUrl` | `http://127.0.0.1:1234/v1` | Endereço do servidor do LM Studio. O `/chat/completions` é adicionado sozinho |
| `Modelo` | `qwen2.5-3b-instruct` | O **API Model Identifier** que o LM Studio mostra |
| `ApiKey` | *(vazio)* | O LM Studio local não pede chave |
| `SaidaEstruturada` | `true` | Pede o formato JSON ao servidor — é o que faz modelos pequenos funcionarem |

> Você também pode mudar `Modelo` e `BaseUrl` pelo painel **`F8`** em jogo, mas essas duas chaves são de
> **texto** e o próprio mod avisa que é mais fácil editá-las no arquivo.

---

## 6. `127.0.0.1` vs `localhost` — use o IP

O padrão do mod é `http://localhost:1234/v1`, e ele **funciona** — mas custa caro.

> **No Windows, o `localhost` resolve primeiro para o IPv6 (`::1`)**. O servidor do LM Studio só escuta **IPv4**,
> então cada requisição tenta o IPv6 primeiro, espera o tempo limite e **só depois** cai para o IPv4. Isso
> adiciona cerca de **2,5 s de pedágio em cada ordem**.

Usando o endereço explícito **`http://127.0.0.1:1234/v1`** (IPv4), a resposta volta direto. **É por isso que
recomendamos trocar `localhost` por `127.0.0.1`.**

---

## 7. Como saber que está funcionando

Entre no mundo e digite no chat `colete 10 madeiras`. Se funcionar:

- O companheiro responde (e fala) uma confirmação e sai para coletar.
- No log (`BepInEx\LogOutput.log`) aparecem as linhas `[IA]` com o resultado.

A latência medida aparece no log:

```
[IA] Tempo: 378 ms (… tokens, … tokens/s)
```

O mod avisa no log quando uma ordem passa de **5 s**.

---

## 8. Alternativas e desempenho

### Outros modelos

| Modelo | Observação |
|---|---|
| **`qwen2.5-3b-instruct`** | **Recomendado.** Rápido (~0,35–0,4 s por ordem) e confiável com a saída estruturada |
| `google/gemma-4-e4b` | Também funciona, mas é **bem mais lento** (~3 s por ordem) |
| Modelos maiores | Melhores em conversa, mas **precisam de GPU** para responder rápido |

### Outros servidores

Qualquer endpoint **compatível com a OpenAI** serve — o mod só faz `POST {BaseUrl}/chat/completions`:

- **Ollama:** `http://127.0.0.1:11434/v1`
- **llama.cpp** com servidor no modo OpenAI
- **API paga** (OpenAI, etc.): aponte o `BaseUrl` e preencha o `ApiKey` — lembrando que, aí, as suas mensagens
  **saem da sua máquina**.

> **Nunca versione uma chave de API.** O `ApiKey` fica no `.cfg` local; não compartilhe esse arquivo com a chave
> preenchida.

### Ajustes úteis

| Chave `[IA]` | Padrão | O que faz |
|---|---|---|
| `Temperatura` | `0.1` | Mantenha baixo: a mesma ordem deve dar sempre o mesmo JSON |
| `TimeoutSegundos` | `30` | Quanto esperar pelo modelo. O pedido roda em segundo plano — um modelo lento não trava o jogo |
| `SaidaEstruturada` | `true` | Se o seu servidor não suportar, o mod cai sozinho no formato antigo e avisa no log |
| `CooldownSegundos` | `3` | Tempo mínimo entre duas ordens, para uma rajada de mensagens não inundar o modelo |

---

## Se não funcionar

Vá em **[Problemas comuns](Problemas-comuns)** — os casos "ele não responde a nada" e "demora muito para
responder" cobrem os problemas mais comuns de configuração do LM Studio.
