> 🌍 [English](Voice) · **Português** · [Español](Voice-es) · [Français](Voice-fr) · [Italiano](Voice-it)

# Voz

O mod tem **duas** partes de voz, e elas são independentes:

- **STT** (*speech to text*) — **você** fala e o mod entende: segure **`V`**, fale e solte.
- **TTS** (*text to speech*) — o **companheiro** fala: a resposta dele sai em voz alta, na posição dele no mundo, no
  **idioma do jogo**.

As duas rodam **dentro do jogo**, na sua máquina, sem serviço externo — e **as duas seguem o idioma do jogo**: os
cinco idiomas são português do Brasil, inglês, espanhol, francês e italiano, e `[AI] VoiceLanguage`, `TtsLanguage` e
`TtsVoice` são **automáticos** (`auto`, `auto`, `0`). **Trocar o idioma do jogo não exige reiniciar** — o modelo é
carregado uma vez e a frase seguinte já sai no idioma novo.

---

## Você fala: o STT (Whisper)

### Como funciona

1. Você **segura `V`** e fala.
2. O mod grava o microfone a **16 kHz mono** e corta o silêncio do começo e do fim.
3. Ao soltar, o áudio é transcrito **dentro do jogo** pelo **Whisper.net** (whisper.cpp in-process, na CPU) —
   sem HTTP, sem processo filho, sem internet.
4. O texto reconhecido entra no **mesmo** caminho das ordens digitadas: ordem, conversa, personalidade e
   memória funcionam igual. Você vê `Entendi: "…"` no chat.

> A tecla **`V`** só grava com o jogo em foco e **sem** nenhuma janela aberta (inventário, mapa, menu, chat,
> console ou os painéis do mod).

### Modelos

| Arquivo | Tamanho | Observação |
|---|---|---|
| `whisper\ggml-small-q5_1.bin` | 181 MB | **Padrão.** Bem mais preciso que o `base`, em qualquer idioma |
| `whisper\ggml-base-q5_1.bin` | 57 MB | Mais rápido e menos preciso — **não vem no pacote**: baixe e mude o `[AI] SttModel` |

Onde baixar: https://huggingface.co/ggerganov/whisper.cpp/tree/main

Medição do projeto (CPU de 16 núcleos, 8 threads, clipe de 2 s): **~638 ms** com o `base` contra **~2187 ms**
com o `small`. O tempo quase não muda com o tamanho do clipe, porque o codificador sempre trabalha numa janela
de 30 s.

### Configuração do STT

| Chave `[AI]` | Padrão | O que faz |
|---|---|---|
| `VoiceEnabled` | `true` | Liga o push-to-talk. Desligado = a tecla não faz nada (e volta a fazer o que o jogo fazia com ela) |
| `VoiceKey` | `V` | Tecla do push-to-talk |
| `VoiceLanguage` | `auto` | Idioma da fala (código do Whisper). `auto` segue o **idioma do jogo** (PT-BR, EN, ES, FR, IT) — o modelo é multilíngue e também detecta. Um código explícito sempre ganha |
| `VoiceMaxSeconds` | `10` | Teto da gravação, em segundos |
| `VoiceSilenceThreshold` | `0.01` | Quão alto o áudio precisa ser para contar como fala, ao cortar o silêncio. Aumente se o seu microfone pega muito ruído |
| `VoiceInitialPrompt` | *(vazio)* | Vazio = o **vocabulário das ordens do idioma do jogo**, escrito pelo mod; enviesa o reconhecimento para as palavras das ordens. Coloque as palavras que **você** fala (`none` desliga o viés) |
| `VoiceDevice` | `0` | Qual microfone usar (índice da lista que o log imprime) |
| `VoiceShowText` | `true` | Mostra no chat/HUD o que o microfone ouviu |
| `SttModel` | `whisper\ggml-small-q5_1.bin` | Modelo do Whisper (relativo à pasta do plugin ou caminho absoluto) |

> **Microfones:** na inicialização o mod escreve no log a lista de microfones disponíveis —
> `[STT] Push-to-talk: … N microfone(s) 0: nome, 1: nome`. Use esse número em `VoiceDevice`.

### A tecla `V` e o auto-pickup do jogo

No Valheim, **`V`** é a tecla do *Toggle Auto-Pickup* por padrão. Para não roubar a tecla do jogo, o mod tem um
patch que **bloqueia o auto-pickup** — mas **só** quando as duas condições valem ao mesmo tempo:

- `[AI] VoiceEnabled` está **ligado**, **e**
- o binding do auto-pickup do jogo aponta para a **mesma** tecla (`VoiceKey`).

Se você **reatribuir** o auto-pickup na tela de controles do Valheim, o mod **não** bloqueia nada — o jogo fica
intacto e a tecla `V` continua livre para falar. Você também pode simplesmente trocar o `VoiceKey`.

---

## Ele fala: o TTS (Kokoro)

### Como funciona

- A voz é sintetizada **dentro do jogo** pelo **Kokoro-82M** (via `sherpa-onnx`, na CPU) — sem serviço externo,
  sem processo filho, nada sai da sua máquina.
- A fala sai **na posição do companheiro** no mundo (som 3D), não no centro da tela.
- A síntese roda numa **thread de fundo** e a fala é montada por **frases**: a primeira já toca enquanto a
  próxima é gerada. Falas repetidas saem do **cache**, na hora.
- O modelo é carregado **uma vez**, em segundo plano, quando você entra no mundo.

### Vozes por idioma

`[AI] TtsVoice = 0` (o padrão) é **automático**: a voz padrão do idioma do jogo. Cada idioma tem a sua lista (a padrão
primeiro), e um id explícito **daquele** idioma ganha.

| Idioma | `TtsLanguage` | Id padrão | Vozes (a padrão primeiro) |
|---|---|---|---|
| Português (Brasil) | `pt-br` | **42** | `pf_dora` (F), `pm_alex` (M), `pm_santa` (M) |
| Inglês | `en-us` | **3** | `af_heart` (F), `af_bella` (F), `af_nicole` (F), `am_fenrir` (M), `am_michael` (M), `am_puck` (M) |
| Espanhol | `es` | **28** | `ef_dora` (F), `em_alex` (M) |
| Francês | `fr` | **30** | `ff_siwis` (F) |
| Italiano | `it` | **35** | `if_sara` (F), `im_nicola` (M) |

> **Uma voz de outro idioma não é usada** (soaria com o sotaque errado): o companheiro cai na voz padrão do idioma
> ativo, e o painel `I` diz qual está em uso. A escolha fica guardada no companheiro, então voltar o jogo para
> aquele idioma restaura a voz.
>
> **Idiomas sem voz no Kokoro** — alemão, russo, polonês, turco, coreano, sueco e ucraniano — usam a **voz inglesa**:
> um companheiro falando com voz inglesa é melhor do que um mudo.

### O que ele fala

A regra é: **a voz acompanha o que está escrito na tela.**

| Chave `[AI]` | Padrão | O que faz |
|---|---|---|
| `TtsEnabled` | `true` | Ele **fala** as respostas. Desligado = só o balão e o chat |
| `TtsVoice` | `0` | Qual voz: `0` = **automática** (a voz padrão do idioma do jogo); um id explícito daquele idioma ganha |
| `TtsVolume` | `1` | Volume da voz (0 = mudo, 1 = volume normal) |
| `TtsSpeed` | `1` | Velocidade (acima de 1 fala mais rápido e mais curto) |
| `TtsThreads` | `4` | Núcleos da CPU para a síntese (4 já é mais rápido que o tempo real) |
| `TtsModel` | `tts\kokoro\model.int8.onnx` | Modelo do Kokoro (relativo à pasta do plugin ou caminho absoluto) |
| `TtsLanguage` | `auto` | Idioma do texto falado. `auto` segue o idioma do jogo (`pt-br`, `en-us`, `es`, `fr`, `it`) e um código explícito ganha — **nunca deixe vazio**, porque o Kokoro exige um idioma (`pt` sozinho é português de Portugal) |
| `TtsCache` | `true` | Guarda as falas já sintetizadas (as repetidas saem na hora) |
| `TtsSpeakReplies` | `true` | Fala a **resposta do modelo** (o jeito dele: *"Deixa comigo."*) |
| `TtsSpeakModLines` | `true` | Fala a **linha do mod** que aparece no chat (*"Ok: coletar madeira (10)."*) |
| `TtsReverbZoneMix` | `0` | Quanto das zonas de reverb do mundo entra na voz. `0` = voz seca |

- Numa **ordem**, a linha do mod **ganha** (a voz acompanha o que está escrito na tela). Desligue
  `TtsSpeakModLines` para ouvir a resposta do modelo no lugar dela.
- Numa **conversa**, a resposta do modelo **é** a fala.
- **Nada é falado duas vezes:** com as duas opções ligadas, a linha que não é falada vai só para o log.

### Modelos

| Onde | Arquivos |
|---|---|
| `tts\kokoro\` | `model.int8.onnx` (109 MB), `voices.bin` (27 MB), `tokens.txt` e a pasta `espeak-ng-data\` |

As vozes, os tokens e o `espeak-ng-data` são procurados na **mesma pasta** do modelo.

Onde baixar: releases do https://github.com/k2-fsa/sherpa-onnx (pacote do modelo **`kokoro-int8-multi-lang`**).

### O eco da voz (e a correção)

Se você ouvir a fala do companheiro **ecoando como numa caverna**, não é bug do mod: o Valheim tem **uma zona
de reverb global** — uma `AudioReverbZone` de raio **1000 m** no próprio objeto do `AudioListener`, com
**4 s** de cauda. Tudo dentro de 1 km levava essa cauda, e a nossa fonte de áudio estava no padrão do Unity.

A correção é a chave **`[AI] TtsReverbZoneMix`**, que já vem em **`0`** (voz seca, como a música do jogo). Se
você quiser um "espaço" sutil na voz, use **`0,19`** — o mesmo valor que o ambiente do jogo usa. Vale na
**próxima frase**, sem reiniciar.

---

## Testando pelo console do jogo

Abra o console com **`F5`**:

| Comando | O que faz |
|---|---|
| `modia_tts_teste` | Fala uma frase de teste pelo TTS (pipeline completo) |
| `modia_tts_teste <texto>` | Fala o texto que você escrever — ex.: `modia_tts_teste Bom dia, chefe!` |
| `modia_stt_teste` | Roda o reconhecimento do Whisper sobre um WAV, **sem** usar o microfone |

> `modia_stt_teste` transcreve o arquivo definido em `[AI] SttWavTest` (padrão `whisper\jfk.wav`) e escreve o
> resultado no log. É o teste certo para saber se o modelo do Whisper está carregando — útil quando as ordens
> por voz não saem.
>
> **O pacote não traz esse WAV** (ele saiu do pacote para economizar espaço): ponha um arquivo seu em
> `...\AI-Companions\whisper\` e aponte `[AI] SttWavTest` para ele (caminho relativo à pasta do plugin ou
> absoluto). O comando **não** aceita o caminho como argumento — só a chave. Fala curta no seu idioma, WAV PCM 16
> bits, é o que ele espera.

O log mostra o tempo de síntese de cada frase e o **RTF** (fração do tempo real: abaixo de 1 é mais rápido que
o tempo real).

---

## Pendência conhecida: volume do jogo

A voz do companheiro **não passa pelo mixer do jogo**. Isso significa que ela **ignora os sliders de volume do
Valheim** e obedece **só** a `[AI] TtsVolume`. Se a voz estiver alta ou baixa demais em relação ao jogo, ajuste
por essa chave (ou pelo slider de volume no painel **`I`**).

<!-- D4: add Español/Français/Italiano -->
