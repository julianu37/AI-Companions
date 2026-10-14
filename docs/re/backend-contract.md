# Contrato do "cérebro" (backend) — observado no cliente Thrall

> **Origem:** este contrato foi *observado* no cliente (decompilado do Thrall 1.0.11, apenas como
> referência técnica — dado não confiável). Ele descreve **o que o cliente fala com o servidor
> ego.ai** para que possamos projetar um backend próprio (local + API). Nada de código do ego.ai é
> copiado para o repositório.

No Thrall, o "cérebro" é **um serviço HTTP remoto**. O cliente Valheim é só o terminal:
grava áudio, monta um JSON com o estado do jogo, manda, recebe texto + comandos + um `id` de
áudio, e baixa o WAV para tocar.

---

## 1. Descoberta da URL base

* `GetBrainAPIAddress()` → `Decrypt(...)`: **AES-CBC** (`Aes.Create()`) com **chave de 32 bytes e IV
  de 16 bytes hardcoded** no assembly, decriptando um blob Base64 fixo (constante
  `encryptedBrainBaseURL`).
* Existe uma `ConfigEntry<string>` chamada `BrainAPIAddress` (**"ConfigEntry não lida"**), ou seja:
  a URL é *efetivamente hardcoded*; a config é inócua.
* **Não reutilizaremos** nem a URL, nem a chave/IV, nem o blob. No nosso mod: URL em
  `ConfigEntry`/variável de ambiente (`MODIA_BRAIN_URL`), **sem segredo embutido**, e TLS.

## 2. Endpoints observados

| Método | Rota | Corpo / query | Resposta |
|---|---|---|---|
| `POST` | `/instruct_agent` | JSON (ver §3) | JSON (ver §4) |
| `GET` | `/synthesize_audio` | `text`, `voice`, `player_id`, opcional `use_cartesia=true` | JSON `{ "audio_file_id": ..., "text": ... }` |
| `GET` | `/get_audio_file` | `audio_file_id`, `player_id` | **binário** (WAV) |
| `POST` | `/log_valheim` | JSON `{ player_id, timestamp, log_string }` | (ignorado) |

Observações de implementação do cliente (a melhorar no nosso):
* `WebClient` (`UploadStringTaskAsync` / `DownloadStringAsync` / `DownloadDataAsync`) com
  **timeout manual de 10 s** via `Task.WhenAny(upload, Task.Delay(10s))` + `CancelAsync()`.
* `Content-Type: application/json` explícito nos POSTs.
* Erros → `MessageHud.ShowMessage` ("Error connecting to Thrall server!", "Timeout error…",
  "An error occurred…") + `LogError`.
* O áudio **não é streamado**: sobe embutido em Base64 no JSON e desce em uma segunda requisição
  (`/get_audio_file`).

## 3. `POST /instruct_agent` — payload

Dois modos, discriminados pelo campo `voice_or_text`:

* `"voice"` → inclui `player_instruction_audio_file_base64` (WAV em Base64).
* `"text"` → inclui `player_instruction_text` (texto do chat).

```jsonc
{
  "player_id": "76561198000000000",        // Steam ID do host (era ZNet.PlayerInfo.m_host)
  "agent_name": "Lagertha",                 // nome atual do NPC
  "game_state": {
    "Health": 300.0,
    "Stamina": 100.0,
    "Inventory": [ { "name": "Wood", "amount": 42 } ],
    "NPC_Mode": "FollowPlayer",             // NPCCommand.CommandType atual
    "Alerted": false,
    "IsCold": false, "IsFreezing": false, "IsWet": true,
    "currentTime": 13.75,                   // EnvMan.GetDayFraction() * 24
    "currentWeather": "Rain",               // EnvSetup.m_name
    "currentBiome": "Meadows",              // Heightmap.FindBiome(...)
    "nearbyItems":   "[ { \"name\": \"Wood\", \"quantity\": 12, \"nearestDistance\": 4.3 } ]", // STRING JSON (duplo encode)
    "nearbyEnemies": "[ { \"name\": \"Greyling\", \"quantity\": 3, \"nearestDistance\": 9.1 } ]",
    "targetCreature": "Greyling",           // OU "followTarget": "HumanoidNPC"
    "followTarget": "player"                // mutuamente exclusivo com targetCreature
  },
  "timestamp": 1234.56,                     // Time.time local (segundos de jogo)
  "personality": "<prompt longo de personalidade ou texto livre do usuário>",
  "voice": "asteria",                       // minúscula, exceto vozes Cartesia
  "use_cartesia": false,
  "gender": 1,                              // 0 = masculino, 1 = feminino
  "player_instruction_audio_file_base64": "<BASE64 do WAV>",  // modo voice
  "player_instruction_text": "<texto do chat>",               // modo text
  "voice_or_text": "voice"                  // "voice" | "text"
}
```

Detalhes importantes:
* `nearbyItems`/`nearbyEnemies` são **strings JSON dentro do JSON** (o cliente as produz com
  `indentJson` e as embute como string). Nosso contrato deve usar arrays nativos — mais simples e
  menor.
* As varreduras de proximidade usam raio de **300 m** a partir do NPC (cache de 3 s) e incluem
  `Character`, `ItemDrop`, `CharacterDrop`, `DropOnDestroyed`, `Pickable`, `Destructible`,
  `TreeBase`, `TreeLog`, `MineRock`, `MineRock5`.
* O nome do arquivo `m_name` do item vem como **nome de prefab** (ex.: `Wood`), não localizado.
* Chamadas sem voz usam `Voice:false` e o texto digitado no chat — inclusive o **conteúdo atual do
  campo de chat** (`Terminal.m_input.text`), não apenas mensagens enviadas. Esse é o mecanismo do
  "update periódico" (a cada 5–12 s, com 50 % de chance, o cliente manda o estado + o texto que
  estiver no campo de chat).
* O log é enviado em `/log_valheim` a cada **30 s** e no `OnDestroy`.

## 4. Resposta de `/instruct_agent`

```jsonc
{
  "agent_text_response": "On my way, boss!",
  "agent_text_response_audio_file_id": "af_123", // usado em /get_audio_file
  "player_instruction_transcription": "follow me", // STT do servidor
  "agent_commands": [
    { "category": "Follow",    "action": "-",        "parameters": [] },
    { "category": "Harvesting","action": "-",        "parameters": ["Wood", 20] },
    { "category": "Combat",    "action": "-",        "parameters": ["Greyling", 3] },
    { "category": "Patrol",    "action": "-",        "parameters": [] },
    { "category": "Inventory", "action": "EquipItem","parameters": ["$SwordIron"] }
  ]
}
```

Mapa categoria/ação → comando (o que o cliente faz):

| `category` | `action` | `parameters[0..]` | Efeito no cliente |
|---|---|---|---|
| `Inventory` | `DropAll` | — | Dropa todo o inventário. |
| `Inventory` | `DropItem` | `nome` | Dropa um item. |
| `Inventory` | `EquipItem` | `nome` (com ou sem `$`) | Equipa item. |
| `Inventory` | `PickupItem` | — | **no-op** (placeholder). |
| `Harvesting` | — | `recurso`, `quantidade`(int), *`tool/dica`* | Enfileira `HarvestAction`. |
| `Patrol` | — | — | Enfileira `PatrolAction` no ponto do player, raio 15. |
| `Combat` | — | `alvo`, `quantidade`(int), *dica* | Enfileira `AttackAction`. |
| `Follow` | — | — | Enfileira `FollowAction`. |

* `agent_text_response` é exibido no chat (`Chat`) e o áudio correspondente é baixado e tocado.
* Cada comando enfileirado vira uma linha na UI de "Task Queue".
* Quando um comando termina, o cliente fala por conta própria: `"Done {descrição}"`
  (ex.: `Done gathering 20 Wood`) e pede TTS para essa frase — ou seja, **frases de conclusão são
  geradas no cliente**, não pelo cérebro.

## 5. Áudio: gravação, STT e TTS

### 5.1 Captura (cliente)
| Parâmetro | Valor |
|---|---|
| Biblioteca | `UnityEngine.Microphone` |
| Dispositivo | índice escolhido na UI (`MicrophoneIndex`, persistido) |
| Duração máx. | 10 s (`recordingLength`) |
| Sample rate de gravação | 22050 Hz |
| Bits | 8 (`bitDepth`), PCM unsigned |
| Trim | silêncio no fim removido; `AudioClip` recriado a **44100 Hz** (quirk observável — resample inconsistente) |
| Formato transmitido | **WAV** (header RIFF escrito manualmente), canal(ais) conforme o clip |
| Transporte | Base64 dentro do JSON de `/instruct_agent` |

* **Não há endpoint separado de STT.** O WAV vai junto do payload e o servidor devolve a
  transcrição em `player_instruction_transcription`. Logo, **o STT é server-side**.
* Push-to-talk: tecla `T` — gravações < 1 s são descartadas.

### 5.2 Síntese (cliente → servidor)
1. `GET /synthesize_audio?text=<url-encoded>&voice=<url-encoded>&player_id=<id>[&use_cartesia=true]`
2. Resposta: `{ "audio_file_id": "...", "text": "..." }`
3. `GET /get_audio_file?audio_file_id=<id>&player_id=<id>` → WAV binário
4. Cliente grava em `npcdialogue_raw.wav`, converte para `AudioClip` (parser RIFF próprio, 8/16
   bits) e toca em `1-shot` na posição do player, com `volume = npcVolume/100`, `spatialBlend = 0`,
   `bypassEffects/ListenerEffects/ReverbZones = true`.
5. `404` em `/get_audio_file` → "Audio file does not exist".

### 5.3 Listas de voz (catálogo do servidor)
* `npcVoices` (19): Asteria, Luna, Stella, Athena, Hera, Orion, Arcas, Perseus, Orpheus, Angus,
  Helios, Zeus, 1920s Radioman, ASMR Lady, Hinglish Speaking Lady, Madame Mischief,
  Pilot Over Intercom, Princess, Wizardman.
* `cartesiaVoices` (7) = as 7 últimas → roteadas para **Cartesia** via `use_cartesia=true`.
* O cliente manda `voice` em **minúsculas**, exceto as de Cartesia (nome como está).

## 6. Log

```jsonc
// POST /log_valheim
{ "player_id": "76561198...", "timestamp": "13/10/2026 14:22:01", "log_string": "<linhas do BepInEx>" }
```
* Acumula logs via `Application.logMessageReceived` (limite 10 000 linhas), envia a cada 30 s.
* Formato de `timestamp` aqui é **string `.NET`**, diferente do `timestamp` numérico de
  `/instruct_agent` — inconsistência do cliente.

---

## 7. O que precisamos reimplementar (nosso backend: local + API)

### 7.1 Princípios
1. **Sem segredos embutidos.** URL/chaves em `ConfigEntry` + variáveis de ambiente.
2. **Contrato versionado** (`/v1/...`) e schemas estáveis dos payloads.
3. **Híbrido**: STT e TTS locais por padrão (privacidade/latência/offline); LLM local (Ollama/
   llama.cpp) quando disponível e API (OpenAI/Anthropic/etc.) como *upgrade* configurável.
4. **Streaming** de áudio e de texto (SSE/WebSocket) em vez de Base64 dentro de POST.
5. **Sem duplo encode** de JSON e com arrays nativos para `nearby*`.

### 7.2 Endpoints propostos
| Método | Rota | Descrição |
|---|---|---|
| `POST` | `/v1/instruct` | Uma rodada: estado + (áudio **multipart** ou texto) → texto + comandos + id de áudio. |
| `GET` | `/v1/audio/{id}` | Download binário do WAV/OGG sintetizado. |
| `POST` | `/v1/synthesize` | Forçar síntese de uma frase (usado para "Preview voice" e mensagens de conclusão). |
| `POST` | `/v1/log` | Log/telemetria (opcional, *opt-in* e com redaction). |
| `WS` | `/v1/session` (opcional) | Sessão persistente para streaming de tokens e áudio. |

### 7.3 Componentes do "cérebro"
| Camada | Local (default) | API (opcional) |
|---|---|---|
| STT | `faster-whisper` / `whisper.cpp` (modelo `small`/`medium`) | Deepgram/OpenAI Whisper |
| LLM | Ollama / llama.cpp (modelo 7–14B com tool-calling) | GPT/Claude/etc. |
| TTS | Piper / Kokoro (vozes PT-BR e EN) | ElevenLabs/Cartesia (mantendo `use_cartesia` como *feature flag*) |
| Memória | SQLite por `player_id`+`agent_id` (histórico, fatos, tarefas) | — |

### 7.4 Compatibilidade a manter (para reaproveitar a lógica do cliente)
* **Categorias/ações** (`Inventory.{DropAll,DropItem,EquipItem}`, `Harvesting`, `Patrol`,
  `Combat`, `Follow`) — manter esse vocabulário evita reescrever a fila de comandos.
* **Texto de conclusão** pode ser gerado no cliente ou pedido ao cérebro via `/v1/synthesize`.
* **Identidade**: nosso `player_id` deve ser **estável e sem depender de `m_host`** (que morreu na
  1.0). Sugestão: `PlatformUserID` quando válido, senão hash de `m_name`+`m_characterID`, com
  override manual na config.
* **Formato de áudio de entrada**: aceitar WAV PCM (8/16 bits, 8k–44.1k, mono/estéreo) e converter
  no servidor; ao gerar áudio de saída, entregar WAV 16-bit 44.1 kHz (o cliente toca). No nosso
  cliente, corrigir o quirk de "recriar clip em 44100" (gravar direto em 16 kHz/16-bit é o ideal
  para STT e reduz o payload — hoje o Thrall manda **10 s × 22 kHz × 8 bits** como Base64).
* **Timeouts/cancelamento**: usar `CancellationToken` real; sem travar o main thread (o Thrall usava
  `WebClient` + `Task.WhenAny`, com risco de travar o frame).
* **Sem spam**: o "update periódico" a cada 5–12 s com 50 % de chance gerava chamadas constantes
  (e custo). No nosso: intervalo configurável, modo "silencioso" e *debounce*.
* **Erros amigáveis**: `MessageHud` + log local; nunca derrubar o jogo por falha de rede.
