> 🌍 [English](Configuration) · [Português](Configuration-pt) · **Español** · [Français](Configuration-fr) · [Italiano](Configuration-it)

# Configuración

Dónde está el archivo de configuración, cómo editarlo en el juego y qué hace cada opción de la sección `[AI]`.

---

## Dónde está el `.cfg`

```
<Valheim>\BepInEx\config\AiCompanions.cfg
```

El archivo lo **genera el BepInEx** la primera vez que el mod carga, y se reescribe cuando cambias algo desde los
paneles. Tiene unas **1540 líneas** y **~265 opciones** en total, repartidas en varias secciones (`[General]`,
`[Gathering]`, `[Hunting]`, `[Speech]`, `[Advanced]`, `[AI]` y otras).

> El nombre del archivo **ya no** es el GUID del plugin: hasta la **1.8.2** era
> `Maddhattr.OfflineCompanionsReborn.cfg` y **a partir de la 1.8.3** es **`AiCompanions.cfg`**. El **GUID** del plugin
> sigue siendo `Maddhattr.OfflineCompanionsReborn` — es la **identidad** del mod en el juego y en la red (es lo que
> ata el mundo, la partida y los compañeros), pero **ya no** da nombre al archivo. El `.cfg` antiguo **se queda en el
> disco, ignorado**: puede borrarse sin miedo.

> **¿Vienes de la 1.8.2 o anterior?** Las configuraciones **vuelven a los valores por defecto** (fue el cambio de la
> 1.8.3: el archivo nuevo se crea ya con los valores por defecto) — incluida la **`ApiKey`**, que hay que pegar otra
> vez. **La partida, el mundo, los compañeros, las casas y el inventario no se ven afectados** (las claves `HC_*` y el
> GUID no cambiaron). Los valores del archivo antiguo **no** se migran a propósito: las claves cambiaron de nombre. La
> **1.9.x mantiene el archivo** y solo mueve los valores fijos antiguos de voz/idioma a `auto`.

La sección **`[AI]`** tiene **39 claves**. Así es como aparece en el archivo (en este ejemplo, con la **alternativa
local** configurada: `127.0.0.1`, `qwen2.5-3b-instruct` y la clave vacía — en el **valor por defecto** el `BaseUrl` es
**Groq**, con el modelo `openai/gpt-oss-20b`, y la `ApiKey` es obligatoria):

```ini
[AI]
Enabled = true
BaseUrl = http://127.0.0.1:1234/v1
Model = qwen2.5-3b-instruct
ApiKey =
Temperature = 0.1
```

> El ejemplo de arriba es la **alternativa local**; en el **valor por defecto** las cuatro primeras líneas son
> `Enabled = true`, `BaseUrl = https://api.groq.com/openai/v1`, `Model = openai/gpt-oss-20b` y
> `ApiKey = <tu clave>`. Hasta la **1.8.2** las claves y las descripciones estaban en portugués (`Ligado`, `Modelo`…);
> **a partir de la 1.8.3** están en **inglés** (`Enabled`, `Model`…), como en la tabla de abajo.

---

## Editando en el juego

No necesitas abrir el `.cfg` en el Bloc de notas para el día a día:

| Panel | Tecla | Qué edita |
|---|---|---|
| **Configuración del mod** | **`F8`** | **Todas** las opciones del mod, en el juego |
| **Panel de IA** | **`I`** | La capa de IA y **cada compañero**: la **pestaña IA** (invocador, ajustes del LLM, voz global y push-to-talk) y **una pestaña por compañero** (nombre, tipo, apariencia, su voz y personalidad) |

### Panel de IA (tecla `I`)

El panel se abre en la **pestaña IA** (sin necesitar un compañero cerca) y tiene, arriba, **una pestaña por compañero**
— cuando una de esas pestañas está abierta, el título pasa a ser **su nombre**. Las pestañas se desplazan en horizontal
cuando hay muchos compañeros. Todo se guarda **al momento**: las opciones que valen para todos se quedan en la
**pestaña IA**, y lo que es de un compañero se queda en **su pestaña**.

#### Pestaña IA

| Qué | Cómo |
|---|---|
| **Invocar un compañero nuevo** | Elige el **Tipo** y pulsa **Invocar**: nace **gratis**, sin pasar por el Trader, con el nombre del tipo numerado ("Guerrero 2"). El contador de abajo muestra cuántos están **en la escena**; el tope es `[General] MaxCompanions` (**10** por defecto, contando **vivos + muertos + perdidos**) |
| **Ajustes del LLM** | Los interruptores **Activado**, **Salida estructurada** y **Mostrar respuesta en el chat**; los campos **URL del servidor**, **Modelo**, **Clave de API** (**enmascarada**) y **Prefijo**; los sliders **Temperatura**, **Timeout (s)**, **Cooldown (s)** y **Radio de objetivo (m)**; y los botones **Aplicar** y **Probar conexión** |
| **Elige tu proveedor LLM** | El desplegable justo **encima de la URL del servidor**: elegir **Groq**, **LM Studio** u **OpenRouter** **rellena** la URL y el modelo (mira la tabla de abajo). **Personalizado (URL manual)** no toca nada |
| **Volumen** | El slider del TTS (`[AI] TtsVolume`). La **velocidad del habla** se queda **solo en el panel `F8`** (`[AI] TtsSpeed`) — el botón **Escuchar** usa el valor de allí |
| **Mantén para hablar** | La tecla del push-to-talk (**`V`** por defecto), el botón **Cambiar** para cambiarla y el interruptor **Órdenes por voz** |
| **El compañero habla / Decir respuestas de la IA / Decir frases del mod / Conversación libre** | Los interruptores de voz y conversación (`TtsEnabled`, `TtsSpeakReplies`, `TtsSpeakModLines`, `Conversation`) |
| **Memoria** | Cuántos intercambios recientes recuerda (0–12) |

> Los campos de texto (URL, modelo, clave, prefijo) solo valen después de **Aplicar** — y en el **mensaje siguiente**,
> sin reiniciar el juego. La clave nunca va al log.

> **Tipos:** los **dverger** (**Mago**, **Mago de Fuego**, **Mago de Hielo** y **Apoyo**) son **de combate**: la rueda
> muestra solo las acciones de combate/apoyo y las órdenes de **trabajo** (por el **chat**, por la **voz** o por la
> **tecla de comando**) son rechazados con *"Soy de combate, no hago ese trabajo."*. Para trabajar, usa el tipo
> **Compañero**. Vale para el **Tipo** del invocador y para **Cambiar tipo** de la pestaña del compañero — mira
> **[Cómo funciona](How-it-works-es#tipos-de-compañero)**.

**Selector de proveedor** (el subtítulo **"Elige tu proveedor LLM"**, con el mismo desplegable que el **Tipo** del
invocador) — **se abre ya en el proveedor que usa tu `.cfg`** y, al elegir, rellena los campos:

| Proveedor | URL | Modelo |
|---|---|---|
| **Groq** (por defecto) | `https://api.groq.com/openai/v1` | `openai/gpt-oss-20b` |
| **LM Studio** | `http://127.0.0.1:1234/v1` | `qwen2.5-3b-instruct` |
| **OpenRouter** | `https://openrouter.ai/api/v1` | `openrouter/free` |
| **Personalizado (URL manual)** | *(no toca nada)* | *(no toca nada)* |

> Elegir en el selector **solo rellena los campos**: vale después de **Aplicar**, como cualquier edición ahí. La
> **`ApiKey` nunca se toca** (ni se rellena ni se borra) — sigue siendo tuya. Y el selector **no es una config nueva**:
> **deduce** el proveedor de la propia `[AI] BaseUrl`, así que una URL de **Ollama** o de un servidor tuyo aparece
> como **Personalizado (URL manual)** y no se sobrescribe nada — no existe un segundo lugar que pueda divergir del
> `.cfg`.

**Botón "Crea tu clave de API gratis AQUÍ"** — en la línea de la **Clave de API**, en la mitad derecha: **abre el
navegador** en la página de claves del proveedor elegido (**Groq** → `console.groq.com/keys`; **OpenRouter** →
`openrouter.ai/keys`), para que cojas la clave sin salir del juego. **No aparece** en **LM Studio** ni en
**Personalizado** (esos no tienen clave) y **no es un selector**: hacer clic no cambia ningún estado después.

![El panel de IA en la pestaña IA: el desplegable "Elige tu proveedor LLM" en Groq, el invocador de compañeros, la URL del servidor, el modelo openai/gpt-oss-20b, la clave de API enmascarada, el botón "Crea tu clave de API gratis AQUÍ", Aplicar y Probar conexión](images/en-ai-panel.jpg)

#### Pestaña del compañero

| Qué | Cómo |
|---|---|
| **Nombre** | El campo con el nombre actual + **Aplicar** (**Enter** también aplica). El nombre vale en el HUD, en el hover, en la rueda y en las órdenes por nombre. Un compañero **muerto** no se puede renombrar |
| **Tipo** | Las flechas `<` `>` eligen y **Cambiar tipo (renace)** aplica, **con confirmación**: renace como el tipo nuevo y **todo se queda** (nombre, inventario, equipo, apariencia, voz, personalidad, casa, cama, habilidades) — solo el **trabajo en curso** empieza otra vez |
| **Estado** | **Vivo** o **Muerto** — y morir **no** abre una plaza en el tope |
| **Llamar** | Trae al compañero cerca de ti: viene andando y, si está demasiado lejos, vuelve por el mismo llamamiento del juego |
| **Apariencia** | Abre el **editor de apariencia** (justo abajo) |
| **Personalidad** | La lista con **"Seguir el patrón ([AI])"** delante y los presets (**Viking leal**, **Mago sarcástico**, **Sarcástico**, **Guía paciente**) — **elegir ya guarda**. **Con tus palabras** es el campo libre, aplicado por el **Aplicar** de al lado |
| **Voz** | La lista con **"Seguir el patrón ([AI])"** delante y las **voces del idioma del juego** — elegir ya guarda y vale en la **frase siguiente**; **Escuchar** toca una frase de ejemplo al momento. Una voz guardada de **otro** idioma no se usa (suena la voz por defecto del idioma activo) |

> La voz y la personalidad elegidas aquí son de **este** compañero, guardadas en **su ZDO**: viajan en la **lápida** y
> vuelven en el respawn. Quien está en "seguir el patrón" obedece a lo que está en la **pestaña IA** — cambiar la
> global alcanza a todos los que no tienen elección propia. **Llamar** y **Apariencia** quedan desactivados con el
> compañero muerto.

![El panel de IA en la pestaña de un compañero: las pestañas arriba (IA y una por compañero), el Nombre y el Tipo con Cambiar tipo (renace), el Estado, Llamar y Apariencia, y Voz y personalidad de este compañero con la voz y el botón Escuchar](images/en-ai-panel-companions-editor.jpg)

**Editor de apariencia** (el botón **Apariencia**): género, corte de pelo, estilo de barba, tono de piel, tono y sombra
del pelo, con un **muñeco 3D** que cambia en vivo mientras eliges. **Aplicar** escribe en el compañero **sin respawn**;
**Cancelar** cierra sin cambiar nada.

![El editor de apariencia: género masculino/femenino, corte de pelo, tono de piel, tono y sombra del pelo, con la vista previa 3D del compañero y los botones Cancelar y Aplicar](images/en-ai-panel-companions-editor-2.jpg)

El panel se cierra con **`I`**, con **`Esc`** o en la **X** — y `Esc` primero sale de un campo de texto y cierra una
lista abierta, luego el panel. La tecla **`I`** se ignora mientras escribes (chat, consola, búsqueda del comerciante,
los campos del propio panel) y con el **inventario**, el **mapa**, el **menú**, el panel **`F8`** o el selector del
primer compañero abiertos; y el panel **se cierra solo** cuando mueres o sales del mundo.

> Lo que no está en el panel `I` sigue en el **`F8`**, que edita **todas** las secciones del `.cfg` con los mismos
> nombres de clave — la pista del pie del panel dice exactamente eso.

---

## Sección `[AI]` — todas las claves

Son **39 claves**, todas en **inglés** (el **idioma nativo** del mod desde la 1.8.3 — el `.cfg` y las descripciones
incluidos). Los valores por defecto son los del código fuente: si nunca tocaste nada, esto es lo que está valiendo.

> **`[AI]` es la sección del `.cfg`; `[IA]` es la etiqueta de los logs.** Las **líneas de log** del mod siguen
> empezando por `[IA]` (`[IA] Prompt:`, `[IA] Tempo:`, `[IA] Request #…`) — es el prefijo del mensaje en
> `LogOutput.log`, no una ruta de configuración. No busques `[IA]` en el `.cfg`: ahí la sección es `[AI]`.

| Clave | Por defecto | Qué hace |
|---|---|---|
| `Enabled` | `true` | Enciende las órdenes por texto. Apagado = no se envía nada al modelo |
| `BaseUrl` | `https://api.groq.com/openai/v1` | Endpoint del LLM. El `/chat/completions` se añade solo. El valor por defecto es **Groq** (que pide la `ApiKey`); las alternativas son **LM Studio** (`http://127.0.0.1:1234/v1`) u **Ollama** (`http://127.0.0.1:11434/v1`) para correr todo local, y **OpenRouter** (`https://openrouter.ai/api/v1`), que funciona pero tiene el tier gratuito más lento. **En el caso local** usa **`127.0.0.1`** y **no** `localhost`: en Windows `localhost` resuelve primero a IPv6 y el servidor del LM Studio solo escucha IPv4, lo que cuesta ~**2,5 s** en cada orden (mira **[LLM (Groq, LM Studio u OpenRouter)](LLM-es)**) |
| `Model` | `openai/gpt-oss-20b` | Nombre del modelo, como lo lista el servidor. `openai/gpt-oss-20b` es el valor por defecto (Groq, tier gratuito). Alternativas: `openrouter/free` en OpenRouter; en LM Studio usa el nombre que muestra (p. ej. `qwen2.5-3b-instruct`) |
| `ApiKey` | *(vacío)* | Clave de API del servidor. **Obligatoria en Groq** (la gratuita sale en `console.groq.com/keys`) y también en OpenRouter (`openrouter.ai/keys`); vacía para LM Studio/Ollama local |
| `Temperature` | `0.1` | Cuánto improvisa el modelo. Mantenlo bajo para que el mismo orden dé siempre el mismo JSON |
| `TimeoutSeconds` | `30` | Segundos de espera por el modelo. La petición corre en segundo plano |
| `Prefix` | *(vacío)* | Vacío = todo mensaje del chat va al modelo. P. ej. `/c` exige el prefijo |
| `ShowReplyInChat` | `true` | Muestra en el chat la respuesta cruda del modelo (útil en pruebas) |
| `CooldownSeconds` | `3` | Tiempo mínimo entre dos órdenes |
| `TargetRange` | `40` | Hasta dónde (m) buscar la criatura nombrada en una orden de ataque. La que estás mirando vale siempre |
| `Personality` | *(vacío)* | La personalidad **con tus palabras** (hasta 600 caracteres). Gana al preset |
| `PersonalityPreset` | *(vacío)* | El valor en el `.cfg` es un id: `viking`, `mago`, `sarcastico`, `guia` o `custom`. Vacío/desconocido = el valor por defecto (leal y práctico). El panel muestra el nombre en el idioma del juego |
| `Memory` | `4` | Cuántos intercambios recientes recuerda y envía junto al mensaje siguiente. `0` = sin memoria (tope 12) |
| `Conversation` | `true` | Responder a la conversación libre cuando el mensaje no es una orden |
| `StructuredOutput` | `true` | Pide al servidor el formato JSON (`response_format`/`json_schema`). Es lo que permite usar modelos pequeños |
| `SttTest` | `false` | Prueba: corre el reconocimiento del Whisper solo ~25 s después de cargar y lo escribe en el log |
| `SttModel` | `whisper\ggml-small-q5_1.bin` | Modelo del Whisper (relativo a la carpeta del plugin o ruta absoluta) |
| `SttWavTest` | `whisper\jfk.wav` | WAV que transcribe el `modia_stt_teste`. **El paquete no trae ese archivo** — apúntalo a un WAV tuyo |
| `VoiceEnabled` | `true` | Enciende el push-to-talk |
| `VoiceKey` | `V` | Tecla del push-to-talk |
| `VoiceLanguage` | `auto` | Idioma del habla (código del Whisper). `auto` sigue el **idioma del juego** (PT-BR, EN, ES, FR, IT); un código explícito siempre gana. Vacío también vale como `auto` |
| `VoiceMaxSeconds` | `10` | Tope de la grabación, en segundos |
| `VoiceSilenceThreshold` | `0.01` | Cuánto tiene que sonar el audio para contar como habla |
| `VoiceInitialPrompt` | *(vacío)* | Prompt inicial del Whisper: sesga el reconocimiento hacia las palabras de las órdenes. Vacío = el **vocabulario de las órdenes del idioma del juego**, escrito por el mod; lo que escribas aquí gana (`none` apaga el sesgo) |
| `VoiceDevice` | `0` | Índice del micrófono (el log imprime la lista) |
| `VoiceShowText` | `true` | Muestra en el chat/HUD lo que oyó el micrófono |
| `PanelKey` | `I` | Tecla que abre el **panel de IA** |
| `TtsEnabled` | `true` | **Él habla** las respuestas (TTS local, Kokoro) |
| `TtsVoice` | `0` | Qué voz: `0` = **automática** (la voz por defecto del idioma del juego); un id explícito **de ese idioma** gana (`42` pf_dora, `43` pm_alex, `44` pm_santa en portugués; `3` af_heart, `2` af_bella, `6` af_nicole, `14` am_fenrir, `16` am_michael, `18` am_puck en inglés; `28` ef_dora, `29` em_alex en español; `30` ff_siwis en francés; `35` if_sara, `36` im_nicola en italiano) |
| `TtsSpeed` | `1` | Velocidad del habla |
| `TtsThreads` | `4` | Núcleos de la CPU para la síntesis |
| `TtsVolume` | `1` | Volumen de la voz (0 = mudo) |
| `TtsModel` | `tts\kokoro\model.int8.onnx` | Modelo del Kokoro. Voces/tokens/espeak en la **misma carpeta** |
| `TtsLanguage` | `auto` | Idioma del texto hablado (código del Kokoro). `auto` sigue el **idioma del juego** (`pt-br`, `en-us`, `es`, `fr`, `it`); un código explícito siempre gana. **Nunca vacío** — el modelo exige un idioma |
| `TtsCache` | `true` | Guarda las frases ya sintetizadas |
| `TtsSpeakReplies` | `true` | Dice la respuesta del modelo |
| `TtsSpeakModLines` | `true` | Dice la frase del mod que aparece en el chat (gana en una orden) |
| `TtsReverbZoneMix` | `0` | Cuánto de las zonas de reverb del mundo entra en la voz. `0` = seca; `0.19` = un "espacio" sutil |
| `EcoDiag` | `false` | Diagnóstico de audio: escribe en el log (una vez por sesión) el `AudioListener`, las zonas de reverb y cómo están configuradas las fuentes del juego |

---

## Idioma

**Cinco idiomas: portugués de Brasil, inglés, español, francés e italiano** — y el **inglés es el idioma nativo** del
mod:

- **El idioma del juego lo decide todo**: la interfaz (737 claves), las frases del compañero (186 líneas), los presets
  de personalidad, el contrato del LLM y la **voz y el reconocimiento del habla**. Con el juego en **portugués**, los
  nombres de las opciones del **`F8`** y las pestañas aparecen en **PT** (`Ligado`, `Modelo`, `VozTecla`…); con el
  juego en **inglés**, aparecen los nombres nativos (`Enabled`, `Model`, `VoiceKey`…); español, francés e italiano
  tienen archivo propio. El **panel `I`** también se traduce así. **Cambiar el idioma no exige reiniciar**: los
  paneles se reconstruyen en la próxima apertura y el HUD, las frases y el TTS/STT acompañan.
- **`[AI] VoiceLanguage`, `TtsLanguage` y `TtsVoice` son automáticos por defecto** (`auto`, `auto`, `0`): el mod los
  resuelve por el **idioma del juego**. Voces por idioma (la de por defecto primero): `pt-br` → `pf_dora`, `pm_alex`,
  `pm_santa`; `en-us` → `af_heart`, `af_bella`, `af_nicole`, `am_fenrir`, `am_michael`, `am_puck`; `es` → `ef_dora`,
  `em_alex`; `fr` → `ff_siwis`; `it` → `if_sara`, `im_nicola`.
- **Un valor explícito siempre gana** — y el `F8` y el panel `I` muestran el valor **efectivo**, por ejemplo
  `Automático (es · ef_dora)`.
- **En el `.cfg`**, las **claves y las descripciones están en inglés, siempre** — **no** cambia con el idioma del
  juego (lo mismo vale para el `package/CONFIGURATION.md`). Por eso el **nombre de la clave es el inglés**, incluso
  con el juego en español: la clave es el identificador de la opción, no la etiqueta que lees en la pantalla.
- **Los idiomas nuevos entran por un archivo**, sin tocar el código: `Translations/<Idioma>.json` (737 claves) trae
  los textos de la interfaz (incluidos los nombres de las opciones y de las pestañas del `F8`) y
  `Speech/<Idioma>.json` (186 líneas) trae las **frases del compañero** — el mismo esquema. **El inglés no necesita
  archivo**, porque las claves **son** el texto.
- **Los idiomas que el modelo Kokoro no habla** (alemán, ruso, polaco, turco, coreano, sueco y ucraniano) usan la
  **voz inglesa** — la interfaz y las frases pueden traducirse; el audio es en inglés.

---

## Peso y capacidad de carga

El límite de peso del compañero es **del mod**, no del juego: el vanilla **no** impone un tope a un `Humanoid` que no
es jugador. Dos claves deciden el comportamiento y **las dos son el valor base** — los objetos con **bonus de carga**
suman **por encima** de ellas:

| Sección / clave | Por defecto | Qué hace |
|---|---|---|
| `[General] MaxCarryWeight` | `300` | El tope **base** de peso del compañero (50–2000). Es el `x/300` del panel y de la barra del **HUD** y lo que dispara **`Encumbered`** (andar lento). El **Megingjord** (+150) suma por encima: con el valor por defecto, su tope efectivo pasa a **450** |
| `[Gathering] OverweightThreshold` | `298` | El peso **base** en el que el compañero de **recogida** (madera, piedra, mineral, recolección y el huerto) deja de coger cosas y vuelve a casa para descargar (50–500). Con el **Megingjord** en la mochila solo vuelve cerca de **448**. **Caza y pesca no usan esta clave** (tienen sus propias opciones de mochila llena) |

> **El bonus vale con el objeto en cualquier lugar de la mochila** — no hace falta equiparlo en el hueco de utilidad
> (en un `Humanoid` que no es jugador ni siquiera sobrevive al recargar). Y vale **una sola vez**: dos cinturones
> iguales **no se acumulan**, como el juego trata los efectos de equipo. El valor se lee del **asset**
> (`SE_Stats.m_addMaxCarryWeight`), nunca de un `150` fijo — un cinturón modificado a +250 funciona igual.
>
> El tope efectivo vale en el **`Encumbered`**, en el `x/450` del **panel del compañero** y de la barra del **HUD**, en
> la negativa de **loot** y de la **recogida del suelo**, en el **herrero**, en el **upkeep**, en el "mochila llena" de
> la **pesca/caza** y en el aviso de "estoy lleno". Cada cambio de bonus escribe **una línea** en el log (nunca por
> frame): `[Carry] "Thorin": +150 de capacidade (Megingjord) - teto 450`.

---

## Qué **cambiamos** nosotros frente al upstream

Además de la traducción y de la capa de IA, el proyecto ajustó algunos valores por defecto del upstream. Si vienes de
Offline Companions Reborn, esto es lo que cambió de valor:

| Sección / clave | Valor por defecto del upstream | Nuestro valor por defecto | Por qué |
|---|---|---|---|
| `[Advanced] GatheringSearchRadius` | 30 | **50** | Siguiéndote, el compañero puede estar a 25 m y el objetivo a 25 m al otro lado — el anillo de búsqueda tiene que cubrir ~50 m |
| `[Gathering] FollowRange` | 10 | **25** | Trabaja a hasta 25 m de ti, en vez de quedarse pegado |
| `[Gathering] ForageFollowRange` | 10 | **20** | La recolección sigue siendo "lo que pasaste cerca", pero con margen |
| `[Gathering] Range` | 40 | **60** | Alcance de la recogida con **Seguir apagado** |
| `[Advanced] GatheringDropRadius` | 8 | **12** | Un árbol puede caer más lejos de 8 m; los drops se quedaban atrás |
| `[Looting] CollectRadius` | *(nuevo)* | **10** | Radio de la orden "recoge los objetos del suelo" |
| `[General] MaxCompanions` | *(nuevo)* | **10** | Tope de compañeros por jugador, contando **vivos + muertos + perdidos** (morir no abre plaza) |
| `[Speech] Chatter` | `true` | **`false`** | El chatter aleatorio (frases cada 15–30 s) venía solo de un temporizador. Las frases útiles siguen |
| `[Advanced] UiDiag` | *(nuevo)* | **`false`** | Diagnóstico de UI (clics/capas): opt-in, cero coste apagado |
| `[AI] EcoDiag` | *(nuevo)* | **`false`** | Diagnóstico de audio del compañero: opt-in |

> Los cinco radios de recogida se migran automáticamente **solo** si están exactamente en el valor antiguo. Un valor
> que elegiste a mano **nunca** se sobrescribe.

Los dos diagnósticos (`UiDiag` y `EcoDiag`) son **opt-in**: apagados no escriben nada y no cuestan nada; encendidos
escriben **una vez por sesión** y son lo primero que mirar cuando un clic o un sonido parece raro.

---

## Viéndolo todo en el panel `F8`

El panel **`F8`** muestra **todas** las secciones del `.cfg`, con los mismos rangos de valor. Los **nombres** de las
opciones y de las **pestañas** aparecen en el **idioma del juego**: en portugués lees `Ligado`, `Modelo`, `VozTecla`…;
en inglés, los nombres nativos (`Enabled`, `Model`, `VoiceKey`…) — es la propia clave, porque el inglés no necesita
traducción. Es la forma más rápida de revisar lo que está valiendo ahora, sin salir del juego.

![El panel F8 abierto en la sección [AI], con las opciones de la capa de IA](images/en-f8-panel-advanced-configurations.jpg)

> La captura es de un build con las **claves en inglés** (`Conversation`, `StructuredOutput`, `VoiceEnabled`,
> `TtsVoice`…). Desde la **1.8.3** el `.cfg` es en inglés; con el juego en **portugués** el panel sigue mostrando los
> nombres en PT.

> Los paneles del mod quedan **por encima del HUD del juego** en el orden de dibujo — es lo que garantiza que un clic
> en el panel llegue al panel, y no al HUD.

<!-- D4: add Español/Français/Italiano -->
