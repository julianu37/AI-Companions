# Fase 3.3 — ordens por voz: push-to-talk → STT local → mesma caixa de ordens do chat

**Status:** build 0 erros, DLL + traduções + modelo `ggml-small-q5_1.bin` no plugin; **teste em jogo pendente**
(receita no fim). Ver também a **Fase 3.3b** (§9), que corrigiu o conflito da tecla V com o auto-pickup do jogo,
a qualidade da transcrição (modelo `small`, prompt inicial, trim) e a medição da latência do LLM.
**Decisão do spike (§7 de `stt-whisper-net-spike.md`):** o **Whisper.net in-process** foi adotado — validado em jogo
(1,4 s para transcrever 11 s de áudio, 8 threads), sem crash e sem conflito de assembly. Não há processo filho
(`whisper-server.exe`) nem HTTP no caminho da voz.

Arquivos: `src/Companions/AI/VoiceInput.cs` (novo), `src/Companions/AI/WhisperStt.cs` (novo),
`src/Companions/AI/CompanionChatAI.cs` (`Submit`/`Announce`/`HeardByVoice`), `src/Companions/Core/ModConfig.cs`
(7 configs `[IA]`), `src/Companions/Core/CompanionsPlugin.cs` (Init/Tick/Shutdown), `ModLocalization.cs` +
`Translations/Portuguese_Brazilian.json` (12 chaves `hc_ai_voice_*`), `src/Companions/Diagnostics/WhisperSpike.cs`
(passou a usar o modelo compartilhado).

---

## 1. O caminho completo

```
[IA] VozTecla segurada
      │  Input.GetKeyDown + guardas (foco, UI aberta, microfone, modelo, ocupado)
      ▼
Microphone.Start(dispositivo, loop: false, VozDuracaoMaxima, 16000)      <- 16 kHz MONO
      │  "Ouvindo..." (chat/HUD)
      ▼  tecla solta (ou teto de duração atingido)
Microphone.GetPosition -> Microphone.End -> clip.GetData
      │
      ├─ trim de silêncio nas pontas (janelas de 10 ms >= VozLimiarSilencio, ±0,125 s)
      ├─ (se o clipe não vier a 16 kHz: reamostragem linear)
      ▼
WAV PCM 16-bit mono 16 kHz montado em MEMÓRIA (44 B de cabeçalho + amostras LE)
      ▼
WhisperStt.Request(wav)                       <- main thread só enfileira e volta
      │  Task de fundo: TryGetFactory (modelo já carregado) + NativeGate + processor.Process(MemoryStream)
      ▼
fila thread-safe (SttResult)
      ▼  drenada no Update (VoiceInput.Tick)
"Entendi: \"colete 10 madeiras\"" (chat/HUD) + o texto no balão do JOGADOR (3.3d)
      ▼
CompanionChatAI.Submit(texto, "voz", announceRefusal: true)     <- MESMA função do chat
      │  cooldown, escolha do companheiro, personalidade, memória, prompt PT-BR
      ▼
LlmClient -> LlmIntent -> CompanionOrders  (idêntico a uma ordem digitada)
      ▼
"Ok: coletar madeira (10)" e o companheiro vai coletar
```

Nenhuma lógica de ordem foi duplicada: o patch de `Chat.SendText` (Fase 3.1) e a voz (Fase 3.3) entram no mesmo
`CompanionChatAI.Submit`. O `[IA] Prefixo` **não** se aplica à voz (é um filtro do chat).

## 2. Formato do áudio (e por que 16 kHz mono)

| Item | Valor |
|---|---|
| Taxa / canais | **16000 Hz, mono** — o formato que o Whisper espera |
| `Microphone.Start` | `loop: false`, `lengthSec = [IA] VozDuracaoMaxima` (1–30 s), `frequency = 16000` |
| Amostras usadas | só até `Microphone.GetPosition` (o resto do buffer circular é silêncio) |
| Trim | janelas de 10 ms; a fala vai da primeira à última janela com pico ≥ `[IA] VozLimiarSilencio`, com 0,2 s de folga em cada ponta (era 0,125 s antes da 3.3b) |
| Mínimo | ~0,33 s de fala depois do trim (abaixo disso é clique/tosse) |
| Container | **WAV PCM 16-bit mono**, 44 B de cabeçalho canônico, montado em memória (sem arquivo temporário) |
| Quem lê | `WhisperProcessor.Process(Stream)` (o `WaveParser` do Whisper.net), o mesmo caminho do spike |
| Descarte | o `AudioClip` do microfone é destruído (`Object.Destroy`) em todos os caminhos |

> O Thrall antigo gravava 8-bit a 22050 Hz e convertia depois; aqui a captura já sai no formato final.
> Se o dispositivo ignorar os 16 kHz (o clipe vem em outra taxa), o áudio é **reamostrado linearmente** para 16 kHz
> antes do WAV, com uma linha no log dizendo a taxa real.

## 3. `WhisperStt` (o spike virou serviço)

| Antes (spike) | Agora (Fase 3.3) |
|---|---|
| `WhisperFactory.FromPath` + `Dispose` a cada execução | `TryGetFactory` carrega **uma vez** e **mantém carregado** (lock de carga; o spike usa o mesmo factory) |
| WAV de arquivo, execução manual | `Request(byte[] wav)` enfileira e roda numa `Task` de fundo; resultado por `ConcurrentQueue` |
| — | `Prewarm()` carrega o modelo em segundo plano quando o jogador entra no mundo (a 1ª ordem não é mais lenta) |
| — | `Tick()` watchdog (60 s) libera o slot se uma transcrição nunca voltar; `Shutdown()` libera o nativo |
| — | `NativeGate`: **uma** transcrição por vez, seja do pipeline de voz ou do `modia_stt_teste` |
| — | `[IA] VozIdioma` → `WithLanguage("pt")` (vazio/`auto` → `WithLanguageDetection()`) |

- **Nunca bloqueia a main thread:** `Request` só lê a config, marca o slot e chama `Task.Run`; `Process` é síncrono
  mas já está fora da main thread. O `Update` só drena a fila.
- **Nada por frame:** o `Tick` do push-to-talk faz `TryDequeue` + leitura da tecla; buffers de amostra são
  reutilizados entre gravações.
- **Erros viram resultado:** qualquer exceção (modelo ausente, nativo, áudio ruim) sai como `SttResult.Ok = false`
  com a mensagem, e a main thread mostra `Não consegui entender a fala: ...` — nada escapa para o Unity.

## 4. Guardas (por que uma tecla pode não fazer nada)

| Situação | O que acontece |
|---|---|
| `[IA] VozLigada = false` | a tecla é ignorada por completo (nem lê o microfone) |
| `[IA] Ligado = false` (IA desligada) | `As ordens por voz...` / `A IA dos companheiros está desligada` |
| Sem microfone (`Microphone.devices` vazio) ou `Microphone.Start` devolveu null | `Nenhum microfone encontrado...` (avisa uma vez no log) |
| Modelo ausente (`[IA] SttModelo`) | `O reconhecimento de voz não está pronto...` |
| Jogo sem foco (`Application.isFocused`) | não grava (e cancela a gravação em andamento) |
| Janela aberta: inventário, mapa, menu, chat com foco, console, loja, seleção de peça, radial, painel do companheiro, painel de config, editor de zona, seletor de cultura | `Feche a janela antes de falar.` |
| Transcrição anterior ou pedido ao LLM ainda em andamento | `Ainda estou cuidando do que você disse.` |
| Áudio sem fala acima do limiar | `Não ouvi nada.` |
| Fala < ~0,33 s | `Foi rápido demais - segure a tecla e fale.` |
| Teto de duração atingido com a tecla ainda segurada | `Tempo máximo de gravação atingido (10s).` e transcreve o que foi gravado |

## 5. Config (`[IA]`, PT-BR no `.cfg`)

| Chave | Default | Para que serve |
|---|---|---|
| `VozLigada` | `true` | liga/desliga o push-to-talk inteiro (e, com ele, o bloqueio do auto-pickup do jogo na tecla — §9) |
| `VozTecla` | `V` | tecla de falar (segurar). **V é também o *Toggle Auto-Pickup* do jogo** (ver §9): o mod bloqueia o toggle enquanto a voz estiver ligada e o auto-pickup estiver nessa tecla. As outras teclas do mod: `Z` é comando/editor de zona, `E` é o radial (e o *Use* do jogo), `F8` é o painel, `F5` é o console, `F7` reposiciona a UI, `LeftAlt` é o modificador do editor |
| `VozIdioma` | `pt` | código do Whisper (`pt`, `en`, `es`...); vazio ou `auto` = detecção |
| `VozDuracaoMaxima` | `10` s | teto da gravação (1–30 s) |
| `VozLimiarSilencio` | `0.01` | pico (janela de 10 ms, 0–1) para contar como fala no trim; era `0.02` até a 3.3b (cortava o começo de palavras baixinho); subir em ambiente barulhento |
| `VozPromptInicial` | vocabulário das ordens (PT-BR) | **3.3b:** prompt inicial (`WithPrompt`) que enviesa o vocabulário do Whisper; vazio desliga |
| `SttModelo` | `whisper\ggml-small-q5_1.bin` | **3.3b:** modelo do Whisper (181 MB). O `ggml-base-q5_1.bin` (57 MB) continua em disco para quem preferir velocidade |
| `VozDispositivo` | `0` | índice do microfone na lista que o log imprime no início (0 = o primeiro, o padrão do Windows) |
| `VozMostrarTexto` | `true` | mostrar `Entendi: "..."` no chat/HUD + o texto reconhecido no balão do **jogador** (`CompanionTalk.SayAsPlayer`, 3.3d) |

## 6. Por que `UnityEngine.AudioModule` é acessado por reflexão

`Microphone` e `AudioClip` vivem em `UnityEngine.AudioModule.dll`. Este projeto **não pode referenciá-lo**:

- o alvo é **net472** e o pacote **Whisper.net** (assets `netstandard2.0`) fixa a fachada **netstandard 2.0**;
- todo módulo do Unity 6 é construído contra **netstandard 2.1**;
- um módulo que **já está** na closure do jogo (`CoreModule`, `AIModule`, `AnimationModule`... chegam via
  `assembly_valheim`/`Assembly-CSharp`) pode ser referenciado direto — `AudioModule` **não** está, e a referência
  dá **`CS1705`** (`AudioModule` usa `netstandard 2.1.0.0`, mais nova que a 2.0.0.0 referenciada);
- apontar o `netstandard` para a fachada **2.1 do próprio jogo** resolve o CS1705, mas quebra do outro lado:
  com ela, `AudioClip.GetData(Span<float>, int)` resolve `Span<T>` no **mscorlib** → **`CS7069`** (os *reference
  assemblies* do .NET Framework não têm `Span<T>`).

Solução: `VoiceInput.Mic` resolve **8 membros** por reflexão, **uma vez** (com 3 tentativas, porque o `Awake` do
plugin pode rodar antes do módulo de áudio carregar) e nunca por frame:

```
Microphone: devices, Start(string,bool,int,int), End(string), IsRecording(string), GetPosition(string)
AudioClip:  samples, channels, frequency, GetData(float[], int)
```

Nenhuma referência nova no `.csproj`, nenhuma segunda BCL dentro do Mono do jogo, e o build continua com
**0 avisos / 0 erros**. As chamadas acontecem só no início e no fim de uma gravação (nunca no `Update` idle).

## 7. Receita de teste in-game

1. **Preparo:** modelo em `<pasta do plugin>\whisper\ggml-small-q5_1.bin` (181 MB, o default desde a 3.3b — baixe de
   `https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small-q5_1.bin`; o `ggml-base-q5_1.bin` de 57 MB
   continua valendo pelo `[IA] SttModelo`) e os nativos em
   `<pasta do plugin>\runtimes\win-x64\` (o build já copia); VC++ Redist 2019+ instalado; LM Studio/Ollama no ar
   para o LLM (o texto reconhecido segue o mesmo caminho do chat).
2. **Entrar no mundo** e conferir no `BepInEx\LogOutput.log`:
   ```
   [STT] Push-to-talk: LIGADO | segure V para falar | 1 microfone(s) (0: ...) | audio 16000 Hz mono -> Whisper...
   [STT] Microfone acessivel por reflexao (UnityEngine.AudioModule ...): Microphone.{devices,Start,End,...}
   [STT] Prompt inicial do Whisper ([IA] VozPromptInicial, N caracteres): "Ordens para o companheiro: ..."
   [STT] Modelo CARREGADO em NNN ms e MANTIDO carregado: "...\whisper\ggml-small-q5_1.bin"
   [STT] Modelo pre-carregado em segundo plano (...)
   [STT] CONFLITO DE TECLA: o auto-pickup do jogo esta em "<Keyboard>/V" ... (na primeira consulta do jogo)
   ```
3. **Segurar `V`**, dizer **"colete 10 madeiras"** e **soltar**. No HUD/chat, em ordem:
   `Ouvindo...` → `Entendi: "colete 10 madeiras"` (e o texto no **balão do jogador**, 3.3d) → `Pensando...` →
   `Ok: coletar madeira (10)`. O companheiro deve sair coletando. **O auto-pickup não deve alternar** (nenhuma
   mensagem `Auto-Pickup: On/Off`).
4. **No log**, a cada ordem:
   ```
   [STT] Microfone escolhido: #0 "..." de 1 (0: ...)
   [STT] Audio gravado: 2,3 s (36800 amostras), WAV PCM 16-bit mono 16000 Hz, 72 KB.
   [STT] Voz #1 (push-to-talk): 72 KB de audio, idioma pt, 7 thread(s), prompt inicial N caracteres - ...
   [STT] TRANSCRICAO em NNN ms (push-to-talk) | idioma=pt | 1 segmento(s) | prompt=N car. | TEXTO: "colete 10 madeiras"
   [IA] Order from voz: "colete 10 madeiras"
   [IA] Prompt: instrucoes NNNN car. (~NNN tokens) + mensagem NN car. = NNNN car. (~NNN tokens); corpo JSON NNNN car.
   [IA] Tempo: NNN ms no total, resposta com NN car. (~NN tok., ~N tok/s ...)
   ```
5. **Outros casos:** `bom dia` → conversa (resposta no balão/chat); `ataca o greydwarf` → engaja a criatura;
   `equipa o machado de bronze` → equipa; falar baixinho/ruído → `Não ouvi nada.`; abrir o inventário e apertar `V`
   → `Feche a janela antes de falar.`; apertar `V` duas vezes seguidas → `Ainda estou cuidando do que você disse.`;
   renomear o modelo (`[IA] SttModelo` para um caminho inexistente) → `O reconhecimento de voz não está pronto...`.
6. **O spike continua funcionando e sem carregar o modelo de novo:** `modia_stt_teste` (F5) deve logar
   `Usando o modelo COMPARTILHADO com as ordens por voz (...) - sem carregar de novo.`

## 8. O que ainda falta (Fase 3.4/3.7)

- **TTS (3.4):** o companheiro responder com **voz** (`AudioSource` + áudio do backend) em vez de só o balão.
- **UI (3.7):** menu de personalidade/voz e o keybind na tela de config (hoje só no `.cfg`).

## 9. Fase 3.3b — as três correções do teste em jogo

### 9.1 A tecla V e o *Toggle Auto-Pickup* do Valheim

`V` é o default vanilla do auto-pickup: `ZInput.Reset()` → `AddButton("AutoPickup", KeyToPath(Key.V), rebindable: true)`
(o jogo salva a reatribuição como `kbmBinding_AutoPickup`) e o **único** leitor em todo o jogo é `Player.Update`:

```csharp
bool flag7 = ZInput.GetButtonDown("JoyAutoPickup") && ZInput.GetButton("JoyAltKeys");
if (ZInput.GetButtonDown("AutoPickup") || flag7)
{
    m_enableAutoPickup = !m_enableAutoPickup;
    Message(MessageHud.MessageType.TopLeft, "$hud_autopickup:" + (m_enableAutoPickup ? "$hud_on" : "$hud_off"));
}
```

`Patches/AutoPickupKeyConflict.cs` põe um **prefixo Harmony em `ZInput.GetButtonDown(string)`** (estático, em
`assembly_utils`) que devolve `false` para `"AutoPickup"` — e só para ele — quando `[IA] VozLigada` está ligado **e**
o binding *efetivo* do jogo (`ZInput.instance.GetButtonDef("AutoPickup").GetActionPath()`, comparado com a nossa tecla
em `InputKeyName`) aponta para `[IA] VozTecla`. Sem `ZInput`, sem binding, tecla desconhecida ou qualquer exceção ⇒
**não** bloqueia (o jogo fica intacto). O jogador é avisado **uma vez** no log e no chat/HUD
(`hc_ai_voice_key_conflict`). O push-to-talk não mudou: segurar/soltar `V`.

### 9.2 Qualidade da transcrição

| O quê | Antes | Agora |
|---|---|---|
| Modelo | `ggml-base-q5_1.bin` (57 MB) | **`ggml-small-q5_1.bin`** (181 MB) — bem melhor em PT-BR; o `base` fica em disco |
| Prompt inicial | — | `[IA] VozPromptInicial` → `WhisperProcessorBuilder.WithPrompt(...)` (`initial_prompt`). No whisper.cpp v1.7.4 o `no_context` limpa o texto **acumulado** e o prompt inicial é prependado **depois**, então ele continua valendo com o `WithNoContext()` que o mod usa. Se o modelo devolver o próprio prompt (áudio sem fala clara), `WhisperStt.IsPromptEcho` descarta como "nenhuma fala" |
| Trim | limiar `0.02`, folga `0,125 s` | limiar **`0.01`**, folga **`0,2 s`** — `0.02` cortava o ataque de palavras faladas baixinho |

**Custo do modelo medido** (Whisper.net 1.9.1, os mesmos nativos, 16 núcleos / 8 threads, melhor de 3):

| Áudio | `base-q5_1` | `small-q5_1` |
|---|---|---|
| carga do modelo | 88 ms | 159 ms |
| clipe de 2,0 s | 638 ms | 2187 ms |
| áudio de 11,0 s | 710 ms | 2320 ms |
| com o prompt inicial | +3 a +5% | +3 a +5% |

O tempo **quase não depende do tamanho do clipe** (o encoder sempre trabalha na janela de 30 s), então `small` custa
~1,5 s a mais **por ordem** nesta máquina — sempre em `Task` de fundo, sem travar o jogo. Voltar para `base` é só
apontar `[IA] SttModelo` para `whisper\ggml-base-q5_1.bin`.

### 9.3 Latência do LLM (medida, não adivinhada)

`LlmClient` loga `[IA] Prompt: instrucoes N car. (~N tokens) + mensagem N car. ...` antes do POST e
`[IA] Tempo: N ms no total, resposta com N car. (~N tokens, ~N tok/s ...)` depois, com aviso a partir de 5 s
(`SlowRequestMs`). `[IA] Memoria` default caiu de 6 para 4 trocas. O mod **não** troca o modelo do jogador: se a
contagem de tokens/s estiver baixa, o gargalo é o servidor (modelo maior do que a máquina aguenta, CPU em vez de GPU,
ou carga/JIT do modelo na primeira requisição depois de um tempo parado).
