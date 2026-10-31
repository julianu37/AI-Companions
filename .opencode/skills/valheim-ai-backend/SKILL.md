---
name: Valheim AI Backend
description: Contrato real do "cérebro" do companheiro (LLM por API compatível com OpenAI: Groq, LM Studio/Ollama, OpenRouter), o schema JSON, a cadeia de fallback e o que é local (STT/TTS no processo). Use ao mexer em LlmClient, contrato, schema ou provedor.
---

# O "cérebro" do companheiro — contrato real (1.9.2)

> **Não existe serviço nosso.** O mod fala **direto** com um endpoint compatível com a OpenAI
> (`POST {BaseUrl}/chat/completions`). O `docs/re/backend-contract.md` é o **registro histórico** do que o
> Thrall (ego.ai) esperava; **não** é o nosso contrato.

## O que roda onde

| Peça | Onde roda | Como |
|---|---|---|
| LLM (intenção + conversa) | **fora** | HTTP, API compatível com OpenAI |
| STT (voz → texto) | **no jogo** | `Whisper.net` in-process (`ggml-small-q5_1.bin`), 16 kHz mono |
| TTS (texto → voz) | **no jogo** | Kokoro-82M via `sherpa-onnx` (skill `kokoro`) |
| Memória/persona | **no jogo** | prompt montado por mensagem (`CompanionPersona`/`CompanionMemory`) |

## Provedores (ordem de preferência)

| Provedor | `BaseUrl` | `Model` | Observação |
|---|---|---|---|
| **Groq** (padrão) | `https://api.groq.com/openai/v1` | `openai/gpt-oss-20b` | grátis com `ApiKey` (`console.groq.com/keys`); tier free: **30 RPM, 1.000 RPD, 8.000 TPM, 200.000 TPD** (~4 ordens/min) |
| **LM Studio / Ollama** | `http://127.0.0.1:1234/v1` | `qwen2.5-3b-instruct` | offline/local; use **`127.0.0.1`**, não `localhost` (IPv6 custa ~2,5 s) |
| **OpenRouter** | `https://openrouter.ai/api/v1` | `openrouter/free` | funciona, mas o tier free é lento/imprevisível |

Privacidade: com Groq/OpenRouter o texto da ordem **sai da máquina**; com LM Studio/Ollama, não. A voz é
sempre local.

## O pedido

- `messages`: **um** `system` com `Header` (quem é o companheiro + personalidade + aviso da fala) +
  **contrato do idioma** (`ContractPt/En/Es/Fr/It`, escolhido pelo `CompanionLocale.Current.KokoroCode`) +
  memória em texto (`Jogador: …` / `Nome: …`) e **um** `user` com a mensagem.
- O **contrato é a última coisa** que o modelo lê (o JSON schema é fixo).
- `response_format` (padrão, `[AI] StructuredOutput = true`): `json_schema` com
  `name = companion_intent`, `strict = true`, `additionalProperties = false`, **6 chaves obrigatórias**
  (`action`, `reply`, `companion`, `target`, `resource`, `amount`) e o `enum` de `action` saindo de
  **`CompanionChatAI.ValidActions`** (fonte única — hoje **35** ações). **Tem que acompanhar o contrato.**
- `temperature` = `[AI] Temperature`.
- **Cadeia de fallback** (1 tentativa cada, `LlmClient.Post`): `json_schema` → `{"type":"json_object"}` →
  sem `response_format`. Só uma **recusa de formato** desce a cadeia (`RejectedFormat`: **HTTP 400** ou erro
  citando `response_format`/`json_schema`); timeout/500/rede **não** são repetidos.
- **Watchdog** do slot em voo: `TimeoutSeconds × 3 + 10 s` com a saída estruturada ligada.
- `id` por requisição (`LlmResult.Id`): resposta atrasada é descartada, nunca aplicada.

## A resposta

Um JSON com as 6 chaves. `action` é uma das `ValidActions`; `reply` é **uma frase curta** (é falada);
`target` nunca é `null` em `attack`/`equip`/`drop`; `amount` é **alvo total** para coleta (contado na
mochila). Parser tolerante em `LlmIntent` (aliases, `fuzzy`, ação desconhecida → `none`).

## Config `[AI]` que afeta o cérebro

`Enabled`, `BaseUrl`, `Model`, `ApiKey`, `Temperature`, `TimeoutSeconds`, `Prefix`, `ShowReplyInChat`,
`CooldownSeconds`, `TargetRange`, `Personality`, `PersonalityPreset`, `Memory`, `Conversation`,
`StructuredOutput`. A chave aparece **mascarada** no painel `I`; o teste de conexão é `LlmConnectionTest`.

## Armadilhas (já custaram caro)

- **Acrescentar exemplos ao contrato piora o 3B** (medido na M5): a correção de comportamento é **rede por
  palavras** (`PickupIntent`/`GatherIntent`/`RadialIntent`/`InteractionIntent`/`IsDoorRequest`), não mais
  few-shot. Ver a skill `valheim-ai-orders`.
- **O `enum` do schema só aceita as 35 ações**: se o modelo precisa dizer "porta", ele diz `door`; se
  devolver `inventory_close` para "fecha a porta", quem decide é a **mensagem**, não a ação.
- `localhost` x `127.0.0.1`: ~2,5 s por ordem no LM Studio.
- O `Prefix` (`[AI] Prefix`) é **só do chat** — a voz não usa prefixo.
- Endpoint logado **sem query string** (não vazar chave).

## Comprovantes

`stats` no log: `[IA] Prompt: … tokens`, `[IA] Tempo: … ms`, `[IA] Response (200) :: …`. Harness de
reflexão sobre a DLL real para contrato/schema/parser (ver a skill `valheim-ai-orders`).
