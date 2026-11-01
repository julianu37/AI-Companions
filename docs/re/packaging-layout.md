# Layout do pacote — o que vai onde no zip

> **Regra dura, com regressão registrada:** `tts\kokoro\` e `whisper\` vão **dentro de
> `BepInEx\plugins\AI-Companions\`**. A raiz do zip só leva os documentos.

## O zip

```
AI_Companions-<versão>.zip
├── BepInEx\plugins\AI-Companions\
│   ├── Companions.dll
│   ├── Whisper.net.dll, sherpa-onnx.dll, Microsoft.Bcl.AsyncInterfaces.dll, System.Buffers.dll,
│   │   System.Memory.dll, System.Numerics.Vectors.dll, System.Runtime.CompilerServices.Unsafe.dll,
│   │   System.Threading.Tasks.Extensions.dll
│   ├── runtimes\win-x64\            (nativos do Whisper.net: ggml-*.dll, whisper.dll)
│   ├── runtimes\win-x64\native\     (nativos do sherpa-onnx: onnxruntime.dll, sherpa-onnx-c-api.dll)
│   ├── tts\kokoro\                  (model.int8.onnx, voices.bin, tokens.txt, espeak-ng-data\, LICENSE)
│   ├── whisper\                     (ggml-small-q5_1.bin)
│   ├── Translations\                (Portuguese_Brazilian, Spanish, French, Italian)
│   ├── Speech\                      (os mesmos 4)
│   └── LICENSES\
├── CHANGELOG.md
├── CONFIGURATION.md
├── GUIDE.md
├── LEIA-ME.txt
├── README.md
└── icon.png
```

## Por que os modelos ficam dentro da pasta do plugin

O mod resolve os caminhos **relativos à pasta do `Companions.dll`**:

- `WhisperStt.PluginDirectory()` → `Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)`
- `WhisperStt.ModelPath` / `CompanionTts.ModelPath` → `ResolvePath(PluginDirectory(), [AI] SttModel/[AI] TtsModel, default)`
- `CompanionTts` também monta o caminho dos nativos a partir dela (`runtimes\win-x64\native`).

Modelo na **raiz do zip** é instalado em `...\Valheim\tts\` e `...\Valheim\whisper\` — o mod **não olha
lá**, então a voz (STT e TTS) não carrega numa instalação nova.

## Histórico (para não repetir)

| versão | layout | arquivos |
|---|---|---|
| **1.9.1** (Nexus) | **correto** — `BepInEx/plugins/AI-Companions/tts/kokoro/model.int8.onnx`, `.../whisper/ggml-small-q5_1.bin` | 399 (incluía `Translations\English.json`, arquivo de runtime) |
| **1.9.2 / 1.9.3** | **REGRESSÃO** — `tts/` e `whisper/` na **raiz** do zip | 398 / 395 |
| **1.9.4** | **correto de novo** — volta ao layout do 1.9.1, sem o `English.json` | 398 / 395 |

A conferência foi feita baixando os assets publicados (`gh release download v1.9.1` /
`v1.9.3`) e comparando lista a lista: a única diferença entre o 1.9.1 e o 1.9.4 é o
`Translations\English.json`, que o `ModLocalization` grava em runtime e o staging recusa de propósito.

## Gate de release (rodar antes de zipar)

1. **Montar o staging com o script** (nunca copiando a pasta à mão):

   ```
   python tools/stage_release.py --version <versão> --from "<...>\BepInEx\plugins\AI-Companions" \
       --to "pkg/<versão>/stage" --allow-source-drift
   ```

   O script recusa `speech.json`, `Translations\English.json`, `__folder_managed_by_vortex` e
   qualquer `Translations\*.json`/`Speech\*.json` fora dos 4 idiomas — **na origem e no destino** —
   e coloca `tts\`/`whisper\` dentro da pasta do plugin.

2. **Conferir o zip montado** (o script não compacta; o zip é o conteúdo de `--to`):

   ```
   python -c "import zipfile,sys; n=zipfile.ZipFile(sys.argv[1]).namelist(); \
   print('modelo:', [x for x in n if x.endswith('model.int8.onnx')]); \
   print('raiz tts/whisper:', [x for x in n if x.startswith(('tts/','whisper/'))])" <zip>
   ```

   Tem de sair `BepInEx/plugins/AI-Companions/tts/kokoro/model.int8.onnx` e **nenhum** `tts/`/`whisper/`
   na raiz.

## "Sem modelos": descontinuado

A partir da **1.9.4** só o pacote **completo** é distribuído. A última release do pacote "sem modelos" é a
**1.9.1** (Nexus). O `tools/stage_release.py --no-models` continua existindo para uso local/CI, mas não é
mais publicado — e, quando usado, tira **só** `model.int8.onnx`, `voices.bin` e `whisper\*.bin`; o
`tokens.txt` e o `espeak-ng-data\` (fonemizador) ficam, senão nem o modelo que o jogador colocar depois
funciona.

## Nunca vão no zip

- `speech.json` (gerado em runtime pelo `SpeechConfig` na pasta do plugin).
- `Translations\English.json` (gerado em runtime; o inglês é nativo no código).
- `__folder_managed_by_vortex` (marcador do gerenciador).
- Qualquer `Translations\<pasta>\*.json` (o carregador de localização do Jotunn trataria a pasta como idioma).
