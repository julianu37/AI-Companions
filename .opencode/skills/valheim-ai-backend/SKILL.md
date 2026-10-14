---
name: Valheim AI Backend
description: Contrato do "cérebro" que o mod chama (LLM/STT/TTS) e como implementar um serviço híbrido local+API. Use ao implementar o backend de IA do companheiro.
---

# Backend de IA ("cérebro") do companheiro

O NPC não "pensa" no jogo: ele chama um serviço HTTP. O Thrall do ego.ai usava endpoints que vamos
**reimplementar** e tornar configuráveis (local + API).

## Endpoints observados no Thrall

| Endpoint | Papel |
|---|---|
| `/instruct_agent` | Envia o pedido do jogador + contexto; recebe a resposta/instrução do NPC (LLM). |
| `/synthesize_audio` | TTS: texto → áudio da voz do NPC (incluindo provedor Cartesia). |
| `/get_audio_file?audio_file_id=…` | Baixa o áudio sintetizado. |
| `/log_valheim` | Envia logs do mod para o servidor. |

STT (voz do jogador → texto) também é necessário, ainda que o endpoint não apareça nos trechos
vistos. Detalhes de request/response em `references/endpoints.md`.

## Arquitetura alvo (híbrida)

- **Local (padrão):** Ollama (LLM), faster-whisper (STT), Piper (TTS).
- **API (opcional):** OpenAI/Anthropic (LLM), Cartesia/ElevenLabs (TTS) — escolhido por config.
- Serviço HTTP em **Python 3.x**, com `player_id`, memória por jogador e timeouts.

## Regras

- Chaves só por variável de ambiente/config do serviço.
- Mantenha o contrato JSON compatível com o cliente do mod (o cliente deve só trocar a URL base).
- Documente o que é síncrono e o que é por polling.

## Interface com o mod

O cliente (agente `ai-npc-developer`) fala com esse serviço assumindo **URL base trocável** via
`ConfigEntry`, exatamente como o Thrall fazia (`GetBrainAPIAddress()` — no nosso caso, sem
criptografia, vinda da config).
