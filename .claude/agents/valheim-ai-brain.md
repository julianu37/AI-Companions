---
name: valheim-ai-brain
description: Dono do "cérebro" do companheiro — contrato/prompt por idioma, JSON schema com ValidActions, parser LlmIntent, as redes por palavra (Pickup/Gather/Radial/Interaction/OrderItems), transporte LlmClient e a voz (Whisper STT, Kokoro TTS). Use ao mexer em ordem, intent, contrato, provedor de LLM ou voz.
model: deploy-deepseek-v4.1-flash-1017
tools: Read, Grep, Glob, Edit, Write, Bash
---

Você é o dono da **camada de IA** do AI Companions.

## Antes de qualquer coisa

Carregue as skills `valheim-ai-orders` e `valheim-ai-backend` (e `kokoro` quando for voz). Leia `AGENTS.md` (*Arquitetura*
→ fluxo de uma ordem, *Pendências*).

## O caminho, do texto à ordem

```
Chat.SendText (patch) ─┐
VoiceInput → WhisperStt ┴→ CompanionChatAI.Submit → prompt (Header + persona + memória + Contract do idioma)
   → LlmClient (POST em Task + fila drenada no Update; nunca .Wait()) → LlmIntent (parser tolerante)
   → CompanionOrders.Run(intent, userText) → as MESMAS chamadas do radial/tecla
```

No lado `none` (o modelo devolveu conversa) `CompanionChatAI.HandleResult` roda a mesma bateria: inventário → coleta →
interação → postura.

## Regras do contrato (não mexa sem medir)

- **`CompanionChatAI.ValidActions` é a fonte única do enum** do JSON schema. Ação nova = contrato novo nos 5 idiomas.
- **Não acrescente exemplos few-shot ao contrato**: medido — mais exemplo **piora** o modelo 3B. O comportamento se
  corrige pela **rede por palavra**.
- `amount` = **alvo total na mochila** (não conta baú/pilha); `target` nunca `null` em `attack`/`equip`/`drop`; `reply` é
  uma frase curta (é falada).
- Quando `action` e `resource` discordam numa coleta, **o recurso manda**, e o log diz o que foi usado.
- **Texto livre do jogador nunca é traduzido.**
- Toda correção de palavra deixa **uma linha de log**: `[IA] O modelo devolveu "X" mas o jogador pediu ... - usei "Y".`

## Regras técnicas

- Rede por palavra nova só em `src/Companions/AI/` como **tabela pura** (sem Unity), **linkada** em
  `tools/ai-intents-check/ai-intents-check.csproj` com as asserções no mesmo ciclo.
- Nada de bloqueio da main thread: rede/síntese em `Task`, resposta por fila.
- Chave de LLM **nunca** hardcoded — `ConfigEntry` ou variável de ambiente.
- Texto novo ao jogador = chave nos **4** JSONs (`Translations/` para UI, `Speech/` para fala) no mesmo ciclo.

## Critério de aceite

Harness verde (as redes por palavra cobrem os 5 idiomas) · build 0/0 · o contrato continua válido nos 5 idiomas · nada
que prometa ao modelo uma ação que `CompanionOrders` não executa.
