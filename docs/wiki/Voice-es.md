> 🌍 [English](Voice) · [Português](Voice-pt) · **Español** · [Français](Voice-fr) · [Italiano](Voice-it)

# Voz

El mod tiene **dos** partes de voz, y son independientes:

- **STT** (*speech to text*) — **tú** hablas y el mod entiende: mantén **`V`**, habla y suelta.
- **TTS** (*text to speech*) — el **compañero** habla: su respuesta sale en voz alta, en su posición en el mundo, en
  el **idioma del juego**.

Las dos corren **dentro del juego**, en tu máquina, sin servicio externo — y **las dos siguen el idioma del juego**:
los cinco idiomas son portugués de Brasil, inglés, español, francés e italiano, y `[AI] VoiceLanguage`, `TtsLanguage`
y `TtsVoice` son **automáticos** (`auto`, `auto`, `0`). **Cambiar el idioma del juego no exige reiniciar** — el modelo
se carga una vez y la frase siguiente ya sale en el idioma nuevo.

---

## Tú hablas: el STT (Whisper)

### Cómo funciona

1. **Mantienes `V`** y hablas.
2. El mod graba el micrófono a **16 kHz mono** y recorta el silencio del principio y del final.
3. Al soltar, el audio se transcribe **dentro del juego** con **Whisper.net** (whisper.cpp in-process, en la CPU) —
   sin HTTP, sin proceso hijo, sin internet.
4. El texto reconocido entra en el **mismo** camino que las órdenes escritas: orden, conversación, personalidad y
   memoria funcionan igual. Ves `Entendido: "…"` en el chat.

> La tecla **`V`** solo graba con el juego en foco y **sin** ninguna ventana abierta (inventario, mapa, menú, chat,
> consola o los paneles del mod).

### Modelos

| Archivo | Tamaño | Nota |
|---|---|---|
| `whisper\ggml-small-q5_1.bin` | 181 MB | **Por defecto.** Mucho más preciso que el `base`, en cualquier idioma |
| `whisper\ggml-base-q5_1.bin` | 57 MB | Más rápido y menos preciso — **no viene en el paquete**: descárgalo y cámbialo en `[AI] SttModel` |

Dónde descargarlo: https://huggingface.co/ggerganov/whisper.cpp/tree/main

Medición del proyecto (CPU de 16 núcleos, 8 hilos, clip de 2 s): **~638 ms** con el `base` frente a **~2187 ms** con
el `small`. El tiempo apenas cambia con el tamaño del clip, porque el codificador siempre trabaja en una ventana de
30 s.

### Ajustes del STT

| Clave `[AI]` | Por defecto | Qué hace |
|---|---|---|
| `VoiceEnabled` | `true` | Enciende el push-to-talk. Apagado = la tecla no hace nada (y vuelve a hacer lo que el juego hacía con ella) |
| `VoiceKey` | `V` | Tecla del push-to-talk |
| `VoiceLanguage` | `auto` | Idioma del habla (código del Whisper). `auto` sigue el **idioma del juego** (PT-BR, EN, ES, FR, IT) — el modelo es multilingüe y también puede detectarlo. Un código explícito siempre gana |
| `VoiceMaxSeconds` | `10` | Tope de la grabación, en segundos |
| `VoiceSilenceThreshold` | `0.01` | Cuánto tiene que sonar el audio para contar como habla, al recortar el silencio. Súbelo si tu micrófono capta mucho ruido |
| `VoiceInitialPrompt` | *(vacío)* | Vacío = el **vocabulario de las órdenes del idioma del juego**, escrito por el mod; sesga el reconocimiento hacia las palabras de las órdenes. Pon las palabras que dices **tú** (`none` apaga el sesgo) |
| `VoiceDevice` | `0` | Qué micrófono usar (índice de la lista que imprime el log) |
| `VoiceShowText` | `true` | Muestra en el chat/HUD lo que oyó el micrófono |
| `SttModel` | `whisper\ggml-small-q5_1.bin` | Modelo del Whisper (relativo a la carpeta del plugin o ruta absoluta) |

> **Micrófonos:** al arrancar el mod escribe la lista de micrófonos disponibles en el log — empezando por
> `[STT] Push-to-talk:` y luego el número de dispositivos con su índice y su nombre. Usa ese número en `VoiceDevice`.

### La tecla `V` y la auto-recogida del juego

En Valheim, **`V`** es la tecla del *Toggle Auto-Pickup* por defecto. Para no robarle la tecla al juego, el mod tiene
un parche que **bloquea la auto-recogida** — pero **solo** cuando las dos condiciones son ciertas a la vez:

- `[AI] VoiceEnabled` está **encendido**, **y**
- el binding de la auto-recogida del juego apunta a la **misma** tecla (`VoiceKey`).

Si **reasignas** la auto-recogida en la pantalla de controles de Valheim, el mod **no** bloquea nada — el juego queda
intacto y la tecla `V` sigue libre para hablar. También puedes simplemente cambiar el `VoiceKey`.

---

## Él habla: el TTS (Kokoro)

### Cómo funciona

- La voz se sintetiza **dentro del juego** con **Kokoro-82M** (a través de `sherpa-onnx`, en la CPU) — sin servicio
  externo, sin proceso hijo, nada sale de tu máquina.
- El habla sale **en la posición del compañero** en el mundo (sonido 3D), no en el centro de la pantalla.
- La síntesis corre en un **hilo de fondo** y el habla se monta **por frases**: la primera ya suena mientras se genera
  la siguiente. Las frases repetidas salen de la **caché**, al momento.
- El modelo se carga **una vez**, en segundo plano, cuando entras al mundo.

### Voces por idioma

`[AI] TtsVoice = 0` (el valor por defecto) es **automático**: la voz por defecto del idioma del juego. Cada idioma
tiene su lista (la de por defecto primero), y un id explícito **de ese idioma** gana.

| Idioma | `TtsLanguage` | Id por defecto | Voces (la de por defecto primero) |
|---|---|---|---|
| Portugués (Brasil) | `pt-br` | **42** | `pf_dora` (F), `pm_alex` (M), `pm_santa` (M) |
| Inglés | `en-us` | **3** | `af_heart` (F), `af_bella` (F), `af_nicole` (F), `am_fenrir` (M), `am_michael` (M), `am_puck` (M) |
| Español | `es` | **28** | `ef_dora` (F), `em_alex` (M) |
| Francés | `fr` | **30** | `ff_siwis` (F) |
| Italiano | `it` | **35** | `if_sara` (F), `im_nicola` (M) |

> **Una voz de otro idioma no se usa** (sonaría con el acento equivocado): el compañero cae en la voz por defecto del
> idioma activo, y el panel `I` dice cuál está en uso. La elección se guarda en el compañero, así que volver el juego
> a ese idioma la restaura.
>
> **Idiomas que el modelo Kokoro no habla** — alemán, ruso, polaco, turco, coreano, sueco y ucraniano — usan la **voz
> inglesa**: un compañero que habla con voz inglesa es mejor que uno mudo.

### Qué dice

La regla es: **la voz acompaña lo que está escrito en la pantalla.**

| Clave `[AI]` | Por defecto | Qué hace |
|---|---|---|
| `TtsEnabled` | `true` | **Él habla** las respuestas. Apagado = solo el bocadillo y el chat |
| `TtsVoice` | `0` | Qué voz: `0` = **automática** (la voz por defecto del idioma del juego); un id explícito de ese idioma gana |
| `TtsVolume` | `1` | Volumen de la voz (0 = mudo, 1 = volumen normal) |
| `TtsSpeed` | `1` | Velocidad (por encima de 1 habla más rápido y más corto) |
| `TtsThreads` | `4` | Núcleos de la CPU para la síntesis (4 ya es más rápido que el tiempo real) |
| `TtsModel` | `tts\kokoro\model.int8.onnx` | Modelo del Kokoro (relativo a la carpeta del plugin o ruta absoluta) |
| `TtsLanguage` | `auto` | Idioma del texto hablado. `auto` sigue el idioma del juego (`pt-br`, `en-us`, `es`, `fr`, `it`) y un código explícito gana — **nunca lo dejes vacío**, porque el Kokoro exige un idioma (`pt` solo es portugués de Portugal) |
| `TtsCache` | `true` | Guarda las frases ya sintetizadas (las repetidas salen al momento) |
| `TtsSpeakReplies` | `true` | Dice la **respuesta del modelo** (a su manera: *"Deja que me ocupe."*) |
| `TtsSpeakModLines` | `true` | Dice la **frase del mod** que aparece en el chat (*"Vale: recoger madera (10)."*) |
| `TtsReverbZoneMix` | `0` | Cuánto de las zonas de reverb del mundo entra en la voz. `0` = voz seca |

- En una **orden**, gana la frase del mod (la voz acompaña lo que está escrito en la pantalla). Apaga
  `TtsSpeakModLines` para oír la respuesta del modelo en su lugar.
- En una **conversación**, la respuesta del modelo **es** el habla.
- **Nada se dice dos veces:** con las dos opciones encendidas, la frase que no se dice solo va al log.

### Modelos

| Dónde | Archivos |
|---|---|
| `tts\kokoro\` | `model.int8.onnx` (109 MB), `voices.bin` (27 MB), `tokens.txt` y la carpeta `espeak-ng-data\` |

Las voces, los tokens y el `espeak-ng-data` se buscan en la **misma carpeta** del modelo.

Dónde descargarlo: las releases de https://github.com/k2-fsa/sherpa-onnx (paquete del modelo
**`kokoro-int8-multi-lang`**).

### El eco de la voz (y la corrección)

Si oyes el habla del compañero **haciendo eco como en una cueva**, no es un bug del mod: Valheim tiene **una zona de
reverb global** — una `AudioReverbZone` de radio **1000 m** en el propio objeto del `AudioListener`, con **4 s** de
cola. Todo lo que estaba a menos de 1 km recibía esa cola, y nuestra fuente de audio estaba en el valor por defecto
de Unity.

La corrección es la clave **`[AI] TtsReverbZoneMix`**, que ya viene en **`0`** (voz seca, como la música del juego).
Si quieres un "espacio" sutil en la voz, usa **`0.19`** — el mismo valor que usa el ambiente del juego. Vale en la
**frase siguiente**, sin reiniciar.

---

## Probando por la consola del juego

Abre la consola con **`F5`**:

| Comando | Qué hace |
|---|---|
| `modia_tts_teste` | Dice una frase de prueba por el TTS (pipeline completo) |
| `modia_tts_teste <texto>` | Dice el texto que escribas — p. ej. `modia_tts_teste ¡Buenos días, jefe!` |
| `modia_stt_teste` | Corre el reconocimiento del Whisper sobre un WAV, **sin** usar el micrófono |

> `modia_stt_teste` transcribe el archivo definido en `[AI] SttWavTest` (por defecto `whisper\jfk.wav`) y escribe el
> resultado en el log. Es la prueba correcta para saber si el modelo del Whisper está cargando — útil cuando los
> órdenes por voz no salen.
>
> **El paquete no trae ese WAV** (salió del paquete para ahorrar espacio): pon un archivo tuyo en
> `...\AI-Companions\whisper\` y apunta `[AI] SttWavTest` a él (ruta relativa a la carpeta del plugin o absoluta).
> El comando **no** acepta la ruta como argumento — solo la clave. Un habla corta en tu idioma, WAV PCM 16 bits, es
> lo que espera.

El log muestra el tiempo de síntesis de cada frase y el **RTF** (fracción del tiempo real: por debajo de 1 es más
rápido que el tiempo real).

---

## Limitación conocida: volumen del juego

La voz del compañero **no pasa por el mixer del juego**. Eso significa que **ignora los sliders de volumen de Valheim**
y obedece **solo** a `[AI] TtsVolume`. Si la voz está demasiado alta o baja en comparación con el juego, ajústala con
esa clave (o con el slider de volumen del panel **`I`**).
