# Integração do Kokoro (sherpa-onnx) — esboço e armadilhas

O código real está em `src/Companions/AI/CompanionTts.cs` (fala), `src/Companions/Core/ModConfig.cs` (config) e
`docs/re/tts-kokoro-sherpa-onnx.md` (engenharia reversa). Isto aqui é o mapa mental + o que já nos mordeu.

## Configuração (esboço em C#)

```csharp
var config = new OfflineTtsConfig();
config.Model.Kokoro.Model    = Path.Combine(dir, "tts\\kokoro\\model.int8.onnx");
config.Model.Kokoro.Voices   = Path.Combine(dir, "tts\\kokoro\\voices.bin");
config.Model.Kokoro.Tokens   = Path.Combine(dir, "tts\\kokoro\\tokens.txt");
config.Model.Kokoro.DataDir  = Path.Combine(dir, "tts\\kokoro\\espeak-ng-data");
config.Model.Kokoro.Language = "pt-br";   // OBRIGATÓRIO (multi-lingue)
config.Model.NumThreads      = 4;         // RTF ~0,7–1,0 nesta CPU
config.Model.Sid             = 42;        // speaker id (ver references/voices.md)
config.Model.Speed           = 1.0f;      // 0,5–2,0
var tts = new OfflineTts(config);         // carrega o .onnx (1–2 s)
```

- `Language` vazio ⇒ o sherpa **recusa carregar** (`pt-br`, `en-us`, `en-gb`, `es`, `fr`, `hi`, `it`, `ja`, `zh`).
- `Sid` fora de `0..NumSpeakers-1` ⇒ o mod recusa e loga (`TtsVoice = N nao existe neste modelo`).
- `Speed` fora de 0,5–2,0 é clampado (`CompanionTts.MinSpeed`/`MaxSpeed`, usados também pelo painel).

## Nativas (o passo que mais quebra)

`sherpa-onnx-c-api.dll` **e** `onnxruntime.dll` precisam ser resolvidas por nome, mas o `DllImport` procura na
pasta do **executável** (o jogo), não na do plugin. Ordem que funciona:

1. `SetDllDirectory(<plugin>\runtimes\win-x64\native)`;
2. pré-carregar as duas por caminho absoluto com `LoadLibraryEx(..., LOAD_WITH_ALTERED_SEARCH_PATH)`;
3. `SetDllDirectory(null)` — o módulo fica no processo e o `DllImport` resolve por nome.

Se o módulo carregado não for o do plugin (outro mod trouxe um igual), o log avisa (`PreloadNative` compara o
caminho do módulo).

## Ciclo de vida e threading (regras que já custaram bug)

1. **Um** `OfflineTts` por processo, carregado lazy e mantido; `Prewarm` tenta de novo a cada **30 s** enquanto o
   jogador está no mundo (copiar a pasta com o jogo aberto funciona sem reiniciar).
2. Síntese em **thread de fundo** com `SemaphoreSlim` + filas; o `Update` só **drena** os pedaços prontos.
3. `Shutdown` **espera** a thread (`Join(5000)`) antes do `Dispose`; depois do flag `_stopping`, `Process`/`Speak`
   recusam trabalho.
4. **`audio.Handle != IntPtr.Zero` antes de `NumSamples`** — o binding devolve objeto não-nulo com handle nulo.
5. **Cache por bytes** (32 MB) e **só das linhas do mod** (respostas do LLM são novas a cada vez e expulsariam as
   fixas).
6. **Uma `AudioSource` 3D** na posição do companheiro, `reverbZoneMix = 0` (a `AudioReverbZone` global do Valheim
   cobre 1 km com 4 s de cauda), `maxDistance = [Speech] CullDistance`, `AudioClip` destruído no fim.
7. `AudioClip`/`AudioSource` por **reflexão** (`UnityEngine.AudioModule` não é referenciável no net472 deste
   projeto).

## Desempenho

| Cenário | RTF | Observação |
|---|---|---|
| esta máquina, 4 threads | **0,73–1,07** | mais rápido que o tempo real |
| Raspberry Pi 4, 4 threads (sherpa) | 3,19 | CPU fraca ⇒ fala atrasada |

24 kHz mono; ~1 KB por 0,01 s de áudio (o cache é medido em bytes, não em frases).

## Texto: o que melhora a fala

- Cortar por **sentença** (`.`, `!`, `?`, `…`) e já tocar a 1ª enquanto a 2ª é gerada — é o que o
  `SplitSentences` faz; ponto entre dígitos é decimal e uma sequência de terminadores é **um** corte.
- **Juntar frases muito curtas** (limite do modelo: <10–20 tokens sai fraco). *Ainda não implementado.*
- Cortar textos longos (>400 tokens saem apressados).

## Alternativa: Kokoro-FastAPI (HTTP)

[remsky/Kokoro-FastAPI](https://github.com/remsky/Kokoro-FastAPI) é um wrapper **FastAPI/Docker** com endpoint
**compatível com a OpenAI**:

```http
POST /v1/audio/speech   {"model":"kokoro","voice":"af_heart","input":"...","response_format":"mp3"}
GET  /v1/audio/voices
```

- Vozes: as 54 do HF; **mistura** por peso (`"af_bella(2)+af_sky(1)"`), aliases e troca por `[voice:...]` no texto.
- Formatos: mp3, wav, opus, flac, aac, pcm; streaming (primeiro token ~0,3 s em GPU, ~3,5 s em CPU antiga).
- Licença **Apache-2.0** (mesmos pesos); requer Docker + modelo (imagens COM modelo embutido).
- **Quando faz sentido para nós:** se o TTS sair do jogo para o backend (GPU, vários idiomas, mistura de vozes,
  legendas com timestamps). Para o companheiro dentro do jogo, **in-process é melhor**: sem instalação extra, sem
  HTTP, sem latência de rede.

## Fontes

- [Kokoro-82M (HF)](https://huggingface.co/hexgrad/Kokoro-82M) e
  [VOICES.md](https://huggingface.co/hexgrad/Kokoro-82M/blob/main/VOICES.md) — lista e notas das vozes.
- [hexgrad/kokoro](https://github.com/hexgrad/kokoro) — `KPipeline`, `lang_code` (`a`/`b`/`e`/`f`/`h`/`i`/`j`/`p`/`z`).
- [sherpa-onnx — Kokoro](https://k2-fsa.github.io/sherpa/onnx/tts/pretrained_models/kokoro.html) — export, mapa
  `sid → nome`, RTF, nomes dos arquivos.
- [remsky/Kokoro-FastAPI](https://github.com/remsky/Kokoro-FastAPI) — API HTTP/mistura/streaming.
