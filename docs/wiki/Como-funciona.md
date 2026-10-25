# Como funciona

A arquitetura do mod e o caminho que uma ordem percorre, do seu microfone (ou do chat) até o companheiro
executar.

---

## Visão geral

```
voz (Microphone 16 kHz) ──► Whisper.net (STT, whisper.cpp in-process, CPU) ─┐
                                                                            ├─► mesma caixa de ordens ─► CompanionOrders ─► o companheiro executa
chat do jogo ───────────────────────────────────────────────────────────────┘            │
                                                                                          ├─► LLM (Groq por padrão, ou LM Studio/Ollama local, ou OpenRouter) ─► ação + resposta
                                                                                          └─► Kokoro-82M (TTS, sherpa-onnx in-process, CPU) ─► ele FALA na posição dele
```

Três peças de IA, todas em **threads de fundo** (o LLM pode ser remoto; o STT e o TTS são sempre locais):

| Peça | O que é | Onde roda |
|---|---|---|
| **LLM** | Entende a ordem e conversa | **OpenRouter** (padrão, remoto) ou LM Studio/Ollama (local) — qualquer endpoint OpenAI-compatible |
| **STT** | Transcreve a sua fala | `Whisper.net` (whisper.cpp) dentro do jogo |
| **TTS** | Faz o companheiro falar | `sherpa-onnx` + Kokoro-82M dentro do jogo |

---

## O caminho de uma ordem

1. **Entrada.** Você digita no chat **ou** segura `V` e fala. Nos dois casos o texto cai no **mesmo** ponto de
   entrada — não há lógica duplicada entre chat e voz.
2. **Prompt.** O mod monta o prompt da mensagem: o **contrato** fixo + o **nome** do companheiro + a
   **personalidade** + a **memória** recente.
3. **LLM.** O texto vai para o servidor configurado (`POST {BaseUrl}/chat/completions`) numa **Task de fundo** —
   o jogo nunca espera.
4. **JSON da intenção.** O modelo devolve uma intenção estruturada com cinco campos:

```json
{ "action": "wood", "reply": "Vou buscar madeira.", "target": null, "resource": "wood", "amount": 20 }
```

5. **Parser tolerante.** O mod lê a intenção aceitando variações (`action`, `reply`, `target`, `resource`,
   `amount`) e **valida** o alvo contra o mundo real — nunca inventa item nem criatura.
6. **Execução.** A intenção vira a **mesma ação do menu radial**: a postura de combate, o trabalho, o
   equipamento, o alvo… Tudo passa pelo mesmo caminho que os controles normais do mod.

> A fila de resultados é **thread-safe** e é drenada no `Update`. Nada de bloquear a main thread.

---

## A saída estruturada (JSON schema)

O pedido ao LLM leva um **JSON schema** (`response_format`) que fixa as cinco chaves e a **lista fechada de
30 ações** — a mesma lista do contrato, de `follow` a `none`.

É isso que **destrava modelos pequenos**: com o schema, o servidor é obrigado a responder no formato certo.
Medido com o prompt atual, o `qwen2.5-3b-instruct` dava **0/6** respostas em JSON e passou a **6/6**.

Se o servidor **não suportar** o formato, o mod **cai sozinho** numa cadeia de tentativas, uma de cada vez:

1. `json_schema` (o ideal)
2. `{"type":"json_object"}` (JSON solto)
3. **sem** `response_format` (o formato antigo)

Só uma **recusa do formato** dispara a próxima tentativa — timeout, erro de rede ou erro de servidor **não**
são repetidos. Uma linha no log diz quando a saída estruturada está ligada e **um único aviso** aparece se o
mod chegar ao formato antigo.

Chave: `[AI] StructuredOutput` (padrão `true`).

---

## Personalidade, memória e conversa

- **Personalidade:** presets PT-BR escritos dentro do mod (`viking`, `mago`, `sarcastico`, `guia`) ou **texto
  livre** do jogador — o texto livre **ganha** do preset. Ela entra no prompt, então o companheiro responde
  no tom escolhido.
- **Memória:** as últimas `[AI] Memory` trocas **por companheiro** (0–12). A memória vai como **texto no
  system prompt**, nunca como mensagens extras — o contrato JSON continua sendo a última coisa que o modelo lê.
  Ela é **esquecida** quando o companheiro morre, é dispensado ou você troca de mundo, e **nada é salvo no
  mundo**.
- **Conversa livre:** quando a ação é `none` e há `reply`, o companheiro **fala** (balão + voz) e o texto
  aparece no chat. Numa **ordem** a linha localizada do mod ganha, e o `reply` só vai para o log — nada é dito
  duas vezes.

---

## A voz do companheiro (TTS)

- O modelo do Kokoro é carregado **uma vez**, em segundo plano, quando você entra no mundo.
- A síntese roda numa **thread de fundo dedicada**; os pedaços prontos voltam por outra fila e são drenados no
  `Update`.
- A fala é montada **por frase**: a primeira já toca enquanto a próxima é gerada.
- As **falas fixas do mod** entram num **cache** (por bytes, com descarte das mais antigas) e saem na hora. As
  respostas do modelo, que são novas a cada vez, **não** entram no cache.
- A reprodução usa **uma** `AudioSource` **3D** na posição do companheiro — o som vem de onde ele está, não do
  centro da tela.

A regra de **o que é falado** é simples: **a voz acompanha o que está escrito na tela**. Numa ordem, a linha
do mod; numa conversa, a resposta do modelo. Nada é falado duas vezes.

---

## Não travar o jogo

Este é um requisito de projeto, não um detalhe:

- Toda chamada de rede (LLM) roda em **Task de fundo**, com **timeout** configurável.
- O STT e o TTS rodam em **threads de fundo** com filas thread-safe, drenadas no `Update`.
- Nada de `Find*` ou LINQ por frame; o que dá para cachear por frame, é cacheado.
- Os `AudioClip` e objetos temporários são **destruídos** ao terminar.

---

## Privacidade

**A voz e a transcrição nunca saem da sua máquina:**

- A sua fala é transcrita **dentro do jogo** (Whisper, local).
- A voz do companheiro é sintetizada **dentro do jogo** (Kokoro, local).
- O **LLM** é o único que pode ser remoto: por padrão é o **Groq** (ou o **OpenRouter**, como alternativa), e aí o
  texto das ordens e da conversa **sai da sua máquina**. Para não enviar nada, use o **LM Studio/Ollama** local
  (**[LLM (Groq, LM Studio ou OpenRouter)](LLM-no-LM-Studio)**).

O `ApiKey`, quando existe, fica só no `.cfg` local — nunca versione nem compartilhe esse arquivo com a chave
preenchida.

---

## Tipos de companheiro

O **tipo** de um companheiro vem do **prefab** que o jogo usa para criá-lo, e é ele que decide **o que o
companheiro faz**:

- **Companheiro** (o padrão) é uma cópia do **personagem do jogador** — é esse "corpo de jogador" que os
  trabalhos usam, e é o **único tipo completo**.
- **Mago**, **Mago de Fogo**, **Mago de Gelo** e **Suporte** são os **dverger**, cópias dos dverger do jogo
  (`Dverger`, `DvergerMage`…): eles são **de combate**, por desenho.

**Os dverger são de combate.** A **roda** deles mostra só as ações de combate e de suporte — **Follow**, **Repair
and Restock**, **Despawn**, **Command**, **Pickup**, **Wander**, **Set Home** e as **quatro posturas**
(Balanced/Melee/Ranged/Passive), igual ao upstream. Eles lutam normalmente e continuam atendendo às ordens de
combate, de movimento e de recolher.

Ordens de **trabalho** — coleta, mineração, fazenda, pesca, caça, cozinha, fundição, reparo, reabastecimento — são
**recusadas**, venham do **chat**, da **voz** ou da **tecla de comando** (mirar numa árvore, numa rocha ou num
smelter). A resposta é sempre a mesma fala: **"Sou de combate, não faço esse trabalho."** E nada muda: o **modo
atual** e o **alvo da ordem** ficam como estavam.

Trocar o **tipo** de um companheiro que estava trabalhando por um dverger também **descarta o alvo da ordem** — ele
volta a **seguir** você.

**Para trabalhos, use o tipo "Companheiro".** Nos dverger, duas limitações continuam:

- **a mochila que a IA usa não é salva** — o que **você** coloca na janela do inventário fica, mas o que ele
  **coleta ou equipa por conta dele se perde** ao sair do mundo;
- o **painel** esconde os espaços de comida por tipo, e as peças de armadura ficam na mochila em vez de equipadas.

---

## O que é do upstream e o que é nosso

| Vem do upstream (Offline Companions Reborn, MIT) | É nosso |
|---|---|
| O companheiro: trabalhos, comida, stamina, skills, casa, descanso, portais, carroça | **Tradução PT-BR completa** (386 chaves de UI + 186 falas) sobre um mod **multilíngue**, com o **inglês nativo** |
| A IA de movimento e combate, os jobs, o menu radial e a UI base | A **camada de IA**: ordens por chat/voz, personalidade, memória, conversa |
| Toda a lógica de jogo que as ordens acionam | O **TTS local** (Kokoro) e o **STT local** (Whisper) |
| | O **painel da IA** (tecla `I`) |
| | Ajustes: raio de coleta maior, chatter desligado, o patch da tecla `V` |
| | O `.cfg` com **nome próprio** (`AiCompanions.cfg`), seção `[AI]` e chaves em inglês |

> A IA **não** substitui nada: ela é só uma **nova entrada** para as ações que já existiam.

---

## Documentação técnica

O detalhe de cada fase (o porquê de cada decisão) está no repositório:

- Roadmap e fases: https://github.com/julianu37/AI-Companions/blob/main/docs/roadmap.md
- Notas de engenharia reversa do Valheim 1.0: https://github.com/julianu37/AI-Companions/tree/main/docs/re
- Changelog: https://github.com/julianu37/AI-Companions/blob/main/package/CHANGELOG.md
