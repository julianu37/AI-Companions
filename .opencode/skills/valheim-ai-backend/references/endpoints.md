# Contrato do "cérebro" (endpoints)

O NPC não "pensa" no jogo: o mod faz chamadas HTTP a um serviço (o *cérebro*) que roda LLM,
STT e TTS. Abaixo, os endpoints **observados no Thrall** (cliente do ego.ai) e um contrato JSON
**proposto e consistente** para a nossa reimplementação. Todos usam JSON UTF-8, exceto áudio.

Convenção: `player_id` identifica o jogador (ex.: `steam_76561198...`). Toda resposta de erro
usa HTTP 4xx/5xx e corpo `{"error": {"code": "...", "message": "..."}}`.

## 1. `POST /instruct_agent` — LLM (raciocínio + instrução)

Recebe o pedido do jogador + contexto e devolve fala e ações do NPC. **Síncrono** (bloqueia até
o LLM responder; use timeout generoso, ex. 30 s).

Request:

```json
{
  "player_id": "steam_76561198000000000",
  "agent_id": "companion_01",
  "message": "Me segue e me ajuda a cortar madeira",
  "context": {
    "game": "valheim",
    "biome": "Meadows",
    "day": 42,
    "time_of_day": 12.5,
    "player": {"health": 100.0, "stamina": 80.0, "position": [123.4, 12.0, -88.1]},
    "nearby_entities": ["Greydwarf", "Boar"],
    "inventory_summary": {"wood": 34, "stone": 12}
  },
  "history": [
    {"role": "player", "content": "Bom dia!"},
    {"role": "agent", "content": "Bom dia, guerreiro."}
  ],
  "actions_enabled": ["follow", "attack", "gather", "use_item"]
}
```

Response:

```json
{
  "request_id": "req_2f1c9a",
  "instruction": {
    "speech": "Claro, eu sigo você e corto madeira.",
    "emote": "wave",
    "actions": [
      {"type": "follow", "target": "player"},
      {"type": "gather", "target": "tree"}
    ]
  },
  "voice": "companion",
  "tts": {"enabled": true, "use_cartesia": false},
  "metadata": {"model": "ollama/llama3.1:8b", "latency_ms": 420}
}
```

## 2. TTS — `GET /synthesize_audio` + `GET /get_audio_file`

`/synthesize_audio` enfileira a síntese e devolve um **id**; o áudio é baixado depois (evita
segurar um WAV gigante na resposta JSON). **Assíncrono / polling curto.**

`GET /synthesize_audio?text=..&voice=..&player_id=..&use_cartesia=..`

| Query | Tipo | Descrição |
|---|---|---|
| `text` | string | Texto a sintetizar (URL-encoded). |
| `voice` | string | Nome da voz do NPC (ex.: `companion`). |
| `player_id` | string | Dono do áudio; isola cache por jogador. |
| `use_cartesia` | bool | `true` força provedor Cartesia; `false` usa o provedor padrão/local. |

Response (`200`):

```json
{
  "audio_file_id": "aud_8f3c21",
  "player_id": "steam_76561198000000000",
  "duration_ms": 1840,
  "format": "wav",
  "sample_rate": 22050,
  "status": "ready"
}
```

`GET /get_audio_file?audio_file_id=..&player_id=..` devolve os **bytes** do áudio
(`Content-Type: audio/wav`), não JSON. `404` se o id expirou/é de outro `player_id`.

Polling: se `status` vier `"pending"`, aguarde ~200 ms e repita `synthesize_audio` (ou um
`GET /synthesize_audio?audio_file_id=..&player_id=..` para consultar) até `"ready"`; então baixe
por `get_audio_file`. Limite de tentativas (~25) e timeout total (ex. 15 s).

## 3. STT — `POST /transcribe_audio`

Áudio do jogador → texto. Não aparece nos trechos vistos do Thrall, mas é necessário para voz.
Pode ser **síncrono** (frases curtas) ou **assíncrono** (áudio longo), com `job_id` + polling.

`POST /transcribe_audio` (`multipart/form-data`):

| Campo | Tipo | Descrição |
|---|---|---|
| `file` | binário | Áudio do microfone (WAV/PCM 16 kHz mono). |
| `player_id` | string | Dono da transcrição. |
| `language` | string | ISO-639-1 (ex.: `pt`); `auto` para detectar. |

Response síncrono (`200`):

```json
{
  "text": "me ajuda a construir uma casa",
  "language": "pt",
  "confidence": 0.91,
  "duration_ms": 1520,
  "model": "faster-whisper/small"
}
```

Variante assíncrona: `POST` devolve `{"job_id": "stt_...", "status": "pending"}` e
`GET /transcribe_audio/{job_id}` retorna o objeto acima quando `status == "ready"`.

## 4. `POST /log_valheim` — telemetria/logs

Envia logs do mod para o serviço. **Fire-and-forget** (não bloqueie o jogo; descarte em caso de
timeout). Pode enviar em lote.

Request:

```json
{
  "player_id": "steam_76561198000000000",
  "level": "info",
  "timestamp": "2026-10-13T12:00:00Z",
  "message": "companheiro entrou em modo follow",
  "context": {"mod_version": "0.1.0", "scene": "main", "biome": "Meadows"}
}
```

Ou lote: `{"entries": [ {...}, {...} ]}`. Response: `{"ok": true}` (ou `204 No Content`).

## 5. Arquitetura híbrida (local + API)

O cérebro é um serviço **Python 3.x** que expõe os endpoints acima e internamente escolhe o
backend por `ConfigEntry`/env. O mod só troca a **URL base** — o contrato JSON não muda.

| Papel | Local (padrão) | API (opcional) |
|---|---|---|
| LLM (`/instruct_agent`) | **Ollama** | OpenAI / Anthropic |
| STT (`/transcribe_audio`) | **faster-whisper** | OpenAI Whisper API |
| TTS (`/synthesize_audio`) | **Piper** | Cartesia / ElevenLabs |

Seleção por config (exemplos): `brain.backend.llm = ollama|openai|anthropic`,
`brain.backend.tts = piper|cartesia|elevenlabs`, `brain.backend.stt = faster_whisper|openai`.
O flag `use_cartesia` do `/synthesize_audio` é atalho de compatibilidade para forçar Cartesia.

Chaves de API **nunca** entram no mod: ficam em variável de ambiente do serviço
(`OPENAI_API_KEY`, `ANTHROPIC_API_KEY`, `CARTESIA_API_KEY`, `ELEVENLABS_API_KEY`) ou no config
do serviço.

### Síncrono vs. polling

| Endpoint | Modo |
|---|---|
| `/instruct_agent` | Síncrono (timeout ~30 s). |
| `/log_valheim` | Fire-and-forget. |
| `/transcribe_audio` | Síncrono p/ frases curtas; polling p/ longas. |
| `/synthesize_audio` | Enfileira → polling curto até `ready`. |
| `/get_audio_file` | Síncrono (download de bytes). |

## Regras

- **Chaves só por env/config do serviço** — jamais hardcodar no mod ou no repositório.
- A **URL base do serviço é trocável** via `ConfigEntry` no mod (analogia ao
  `GetBrainAPIAddress()` do Thrall, mas sem criptografia e vinda da config).
- Mantenha o contrato JSON compatível com o cliente do mod; versione o schema e documente o que
  é síncrono e o que é polling.
- Sempre defina `player_id` para memória/isolamento por jogador e cache de áudio.
