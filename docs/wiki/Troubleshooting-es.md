> 🌍 [English](Troubleshooting) · [Português](Troubleshooting-pt) · **Español** · [Français](Troubleshooting-fr) · [Italiano](Troubleshooting-it)

# Problemas comunes

Síntoma → causa probable → qué hacer.

**Dónde mirar en el log, siempre:** `<Valheim>\BepInEx\LogOutput.log`. Las líneas del mod empiezan por `[IA]`, `[STT]`
y `[TTS]`. (`[IA]` es la **etiqueta del log** — la sección del `.cfg` es **`[AI]`**, con las claves en inglés.)

---

## No responde a nada

| Dónde mirar | Qué buscar |
|---|---|
| `LogOutput.log` | `[IA] Chat orders ready: enabled=…, endpoint=…, model=…` |

**Causas probables, en orden:**

1. **La `ApiKey` está vacía o mal.** El mod viene apuntado a **Groq**, que **exige** la clave (mira "responde error de
   autenticación" abajo). Si usas el **LM Studio** local, la clave se queda vacía y el `BaseUrl` tiene que ser
   `http://127.0.0.1:1234/v1`.
2. **El `BaseUrl` o el `Model` están mal.** En Groq son `https://api.groq.com/openai/v1` y `openai/gpt-oss-20b`. En el
   LM Studio local, el `Model` tiene que ser el **API Model Identifier** que muestra, no el nombre de la carpeta. Mira
   **[LLM (LM Studio, Groq u OpenRouter)](LLM-es)**.
3. **Usando el LM Studio local: el servidor no está encendido.** Abre la pestaña **Developer** y revisa el estado
   `Running` en el puerto **1234**.
4. **`[AI] Enabled` está en `false`.** Enciéndelo en el panel **`F8`**.
5. **`[AI] Prefix` está relleno.** Si pones `/c`, solo los mensajes que empiezan por `/c` van al modelo.
6. **Ningún compañero está cerca / apto para recibir la orden.** Tiene que estar invocado.

> Antes de nada, usa **Probar conexión** del panel **`I`**: dice, en segundos, si la URL responde y si el modelo
> aparece en la lista. Recuerda que la **clave** solo se valida en una orden de verdad (la prueba confirma la URL y la
> lista de modelos, no la autenticación).

---

## Responde error de autenticación (401 / "unauthorized")

| Dónde mirar | Qué buscar |
|---|---|
| `LogOutput.log` | `[IA]` con el estado **401** (o un mensaje de *unauthorized* / *invalid api key*) |
| Panel **`I`** → **Probar conexión** | **Puede** responder `Conexión OK` incluso con la clave mal — en OpenRouter la lista de modelos es pública. El 401 aparece en la **orden**, no en la prueba |

**Causa:** **Groq exige** la `[AI] ApiKey` (y OpenRouter también), y está **vacía**, **mal** o **borrada** (por ejemplo,
después de copiar un `.cfg` de otra máquina, o después de actualizar **de la 1.8.2 o anterior** — la 1.8.3 fue la
versión que reseteó las configuraciones) — o es la clave de **otro** servicio, pegada junto con una URL que no es la
suya.

**Qué hacer:**

1. Crea o copia la clave en **https://console.groq.com/keys** (Groq) o en **https://openrouter.ai/keys** (OpenRouter).
2. Pégala en **`[AI] ApiKey`** — en el panel **`I`** (pestaña **IA** → **Ajustes del LLM**) o directamente en el
   `.cfg`.
3. Pulsa **Probar conexión**; luego **Aplicar** (la configuración vale en el mensaje siguiente).
4. Si **no** quieres usar Groq, apunta el `BaseUrl` al servidor local (`http://127.0.0.1:1234/v1`), usa el `Model` del
   LM Studio y **deja la `ApiKey` vacía**.

> ¿Clave filtrada? Bórrala en **https://console.groq.com/keys** (Groq) o en **https://openrouter.ai/settings/keys**
> (OpenRouter) y crea otra. La `ApiKey` del `.cfg` no debe compartirse ni subirse a un repositorio.

---

## Responde 429 (límite de peticiones o de tokens)

| Dónde mirar | Qué buscar |
|---|---|
| `LogOutput.log` | `[IA]` con el estado **429** (o un mensaje de *rate limit* / *too many requests*) |

**Causa:** se alcanzó el **tier gratuito** del servicio. En **Groq**, con el `openai/gpt-oss-20b`, los límites son
**30 peticiones por minuto**, **1.000 al día**, **8.000 tokens por minuto** y **200.000 al día** — y, como cada orden
gasta ~1.800 tokens, el techo práctico es de **~4 órdenes por minuto**. En **OpenRouter**, los modelos `:free` tienen
**20 peticiones por minuto** y un techo diario que depende del historial de la cuenta.

**Qué hacer:**

1. **Espera unos segundos** y manda la orden otra vez — el límite es por ventana de tiempo, así que vuelve solo.
2. Baja la frecuencia: el `[AI] CooldownSeconds` (**3** por defecto) ya ayuda; súbelo si mandas órdenes en ráfaga.
3. Reduce la `[AI] Memory` (cada intercambio recordado hace el prompt más grande y, con él, los tokens de cada orden).
4. Para uso intenso, usa el **LM Studio/Ollama local** — no hay límite de peticiones ni de tokens.

> El **429** no rompe nada: la orden se rechaza con un aviso y el juego sigue. **No** es un error de clave — eso es el
> **401**, arriba.

---

## Tarda mucho en responder

| Dónde mirar | Qué buscar |
|---|---|
| `LogOutput.log` | `[IA] Tempo: … ms` — el mod avisa cuando pasa de **5 s** |

**Causas probables:**

1. **El `.cfg` sigue en OpenRouter.** El valor por defecto actual es **Groq** (`openai/gpt-oss-20b`), que responde en
   una **fracción de segundo** en nuestra medición. Si tu `[AI] BaseUrl` sigue en `https://openrouter.ai/api/v1`,
   estás en su router gratuito (`openrouter/free`), que **varía** de una orden a otra y pasa por la cola del
   proveedor. Cambia a Groq (mira **[LLM (LM Studio, Groq u OpenRouter)](LLM-es)**) o usa el LM Studio local.
2. **No confundas lentitud con el límite de Groq.** El **429** responde **al momento** (la orden se rechaza con un
   aviso), no despacio — si el síntoma es la demora, la causa es otra. Mira "responde 429" arriba para el límite.
3. **El modelo es grande o está en la CPU (local).** Un 3B (`qwen2.5-3b-instruct`) responde en ~0,35–0,4 s; el
   `google/gemma-4-e4b` tarda ~3 s. Los modelos mayores prácticamente exigen GPU.
4. **`[AI] Memory` alto.** Cada intercambio recordado hace más grande el prompt que el modelo tiene que leer. Bájalo.
5. **El primer mensaje de la sesión** incluye la carga del modelo — en el LM Studio local es naturalmente más lento, y
   en los servicios remotos el primero suele ser también el más frío.

> Recuerda: la petición corre en **segundo plano**. Aunque sea lenta, **no** congela el juego.

---

## Responde mal o ignora el número

**Causa probable:** el modelo no entendió el recurso o la cantidad.

**Qué hacer:**

- Revisa lo que entendió el modelo: `[AI] ShowReplyInChat` muestra la respuesta cruda en el chat.
- Usa el **`qwen2.5-3b-instruct`** con `[AI] StructuredOutput = true` — es la combinación que el proyecto midió como
  fiable (el 3B pasó de 0/6 a **6/6** respuestas válidas con la salida estructurada).
- En el valor por defecto (**Groq**, `openai/gpt-oss-20b`) el modelo es siempre el mismo, así que la respuesta es
  consistente. En **OpenRouter** (alternativa), el `openrouter/free` cambia de modelo a cada petición — la calidad
  también varía. Si la orden falla siempre de la misma manera, elige **un modelo concreto** en `[AI] Model` (los
  gratuitos de OpenRouter llevan `:free` en el nombre) o usa el LM Studio local, que es siempre el mismo modelo.
- Recuerda que **el número es un objetivo contado en su mochila**: si ya tiene bastante, **no** sale a recoger, y ese
  es el comportamiento correcto.
- Para las órdenes por voz, un reconocimiento malo llega mal al modelo. Mira "el micrófono no graba" abajo.

---

## Responde en otro idioma

**Causa probable:** el juego está en un idioma y el compañero responde en otro, o el modelo elegido es flojo en tu
idioma.

**Qué hacer:**

- **Revisa el idioma del juego.** Desde la 1.9.0 el contrato que se envía al modelo está en el **idioma del juego** —
  portugués de Brasil, inglés, español, francés e italiano — y un idioma fuera de esos cinco cae en el **inglés**. La
  voz y el reconocimiento del habla siguen la misma regla automáticamente (`[AI] VoiceLanguage` / `TtsLanguage` /
  `TtsVoice` = `auto` / `auto` / `0`).
- Usa el **`qwen2.5-3b-instruct`** (bueno en español) si estás en LM Studio; en **Groq** (por defecto) y en
  **OpenRouter**, un modelo mayor suele escribir mejor que los pequeños.
- Ajusta la personalidad en el panel **`I`** (preset o texto libre en tu idioma) — entra en el prompt y empuja la
  respuesta a ese idioma. Los presets también están traducidos a los cinco idiomas.
- Reduce la `[AI] Temperature` (el `0.1` por defecto ya es bajo; valores altos hacen que el modelo divague).

---

## La voz tiene eco (parece una cueva)

**Causa:** no es un bug — Valheim tiene **una zona de reverb global** de 1000 m en el propio objeto del `AudioListener`,
con 4 s de cola. La fuente de audio del compañero estaba en el valor por defecto de Unity y recogía toda esa cola.

**Qué hacer:** confirma que `[AI] TtsReverbZoneMix` está en **`0`** (el valor por defecto). Si quieres un "espacio"
sutil en la voz, usa `0.19`. Vale en la **frase siguiente**, sin reiniciar. Mira **[Voz](Voice-es)**.

---

## La voz no sale, o sale baja

| Dónde mirar | Qué buscar |
|---|---|
| `LogOutput.log` | Líneas `[TTS]` (tiempo de carga, síntesis, RTF) |

**Causas probables:**

1. **`[AI] TtsEnabled` está en `false`.** Enciéndelo en el panel **`I`**.
2. **El modelo del Kokoro no está en su sitio.** Revisa `tts\kokoro\` con `model.int8.onnx`, `voices.bin`, `tokens.txt`
   y `espeak-ng-data\`.
3. **El volumen está bajo.** La voz **no pasa por el mixer del juego** — **ignora los sliders de volumen de Valheim** y
   obedece **solo** a `[AI] TtsVolume`. Ajusta esa clave (o el slider del panel `I`).
4. **Prueba aislada:** en la consola (**`F5`**), ejecuta `modia_tts_teste ¡Buenos días, jefe!`. Si funciona ahí, el
   problema es de configuración; si no, es del modelo.

---

## El micrófono no graba

| Dónde mirar | Qué buscar |
|---|---|
| `LogOutput.log` | La línea `[STT] Push-to-talk:`: si está encendido, la tecla y la lista numerada de micrófonos |

**Causas probables:**

1. **Ninguna ventana puede estar abierta.** La tecla no graba con el inventario, el mapa, el menú, el chat, la consola
   o los paneles del mod abiertos.
2. **`[AI] VoiceEnabled` está en `false`.**
3. **El micrófono equivocado está seleccionado.** El log lista los dispositivos numerados — pon el número en
   `[AI] VoiceDevice`.
4. **El umbral de silencio está alto.** `[AI] VoiceSilenceThreshold` (`0.01` por defecto) recorta el principio/final de
   la grabación. Si tu micrófono es bajito, bájalo; si capta mucho ruido, súbelo.
5. **El habla fue demasiado corta.** El log dice que la grabación fue demasiado corta — habla más tiempo.
6. **El idioma está mal.** `[AI] VoiceLanguage` es `auto` por defecto (el **idioma del juego**); un código escrito a
   mano de otro idioma (por ejemplo `pt` con el juego en inglés) empeora mucho el reconocimiento. Desde la 1.9.0 el
   valor automático sigue al juego y no necesita reiniciar.
7. **Prueba aislada:** `modia_stt_teste` en la consola (**`F5`**) corre el Whisper sobre un WAV, **sin** micrófono. Si
   transcribe, el modelo está bien y el problema es de captura.

> **Windows x64 es obligatorio** para la voz: los nativos del **STT** (whisper.cpp) **y del TTS**
> (sherpa-onnx/onnxruntime) son `win-x64`.

---

## La tecla `V` activa la auto-recogida del juego

**Causa:** en Valheim, `V` es la tecla del *Toggle Auto-Pickup* por defecto.

**Qué hacer:** el mod ya bloquea ese toggle — pero **solo** cuando `[AI] VoiceEnabled` está encendido **y** el binding
de la auto-recogida del juego apunta a la **misma** tecla. Entonces:

- **Reasigna la auto-recogida** en la pantalla de controles de Valheim a otra tecla (el mod deja de bloquear y tienes
  las dos), **o**
- cambia `[AI] VoiceKey` a otra tecla en el panel **`I`**.

Si la reasignas, el mod reevalúa el conflicto al momento. Mira **[Voz](Voice-es)**.

---

## El panel `I` no se abre

**Causas probables:**

1. **Hay una ventana abierta.** El panel no se abre con el inventario, el mapa, el menú, el panel **`F8`** o el campo
   de búsqueda del comerciante abiertos. Ciérralos.
2. **La tecla se cambió.** Revisa `[AI] PanelKey` (`I` por defecto).
3. **Estás escribiendo en un campo de texto.** En ese caso la tecla se ignora a propósito — el mod no roba la tecla
   del chat/consola.

---

## El panel `F8` no hace clic / un clic se pierde

**Causa (ya corregida):** los paneles del mod estaban en la misma capa de dibujo que el HUD del juego, y el HUD **está
en `sortingOrder = 400`**. En un empate de capa, uGUI desempata por el orden de registro de los raycasters — y gana el
del juego. También había elementos **decorativos** del panel recibiendo clics sin ser controles.

**Qué hacer:**

- Actualiza a la versión con la corrección (los canvas del mod ahora están en la franja **1701+**, **por encima** del
  HUD).
- Si el problema vuelve, enciende `[Advanced] UiDiag = true`: la próxima vez que se abran los paneles, el log escribe
  `[UiDiag]` con las capas de la escena y quién está delante de los controles. Después apágalo otra vez.

> El mismo ajuste de capa arregló el texto del **centro del menú radial** mezclado con el `crosshair` del juego.

---

## Le di el Megingjord al compañero y el peso no cambió

| Dónde mirar | Qué buscar |
|---|---|
| Panel **`I`** (pestaña del compañero) y barra del **HUD** | El peso sigue en `…/300` (o el valor que pusiste en `[General] MaxCarryWeight`) incluso con el cinturón en la mochila |
| Recogida | Vuelve a casa para descargar en **298** en vez de cerca de **448** |

**Causa:** en versiones **anteriores a la 1.8.0** el bonus del cinturón no se leía. El límite de peso del compañero es
**del mod** (`[General] MaxCarryWeight`, 300 por defecto), no del juego, y el `+150` del Megingjord es un *status
effect* cuyo único lector en el vanilla es el `Player`. Como el compañero no es un jugador, el cinturón solo ocupaba
sitio y pesaba.

**Qué hacer:** **actualiza el mod** (la corrección llegó en la 1.8.1). Después de eso el `/450` aparece en ~**0,5 s**
(con el `300` por defecto), y esta línea lo confirma en el `LogOutput.log`:

```
[Carry] "Thorin": +150 de capacidade (Megingjord) - teto 450
```

> El cinturón **no necesita estar equipado** — vale en cualquier lugar de la mochila (en un `Humanoid` que no es
> jugador el hueco de utilidad ni siquiera sobrevive al recargar). Y **dos** cinturones **no se acumulan**: el bonus
> cuenta una vez. Los detalles de las dos claves están en **[Configuración](Configuration-es)**.

---

## El juego no carga el mod

| Dónde mirar | Qué buscar |
|---|---|
| `LogOutput.log` | `AI Companions v1.9.1 loading...` y `AI Companions loaded successfully! (N methods patched)` — el `v1.9.1` es la versión que instalaste |

**Causas probables:**

1. **La carpeta está en el sitio equivocado.** La ruta tiene que ser `<Valheim>\BepInEx\plugins\AI-Companions\` — con
   el `Companions.dll` **dentro** de ella.
2. **Faltan las DLL de dependencia.** `Whisper.net.dll`, `sherpa-onnx.dll` y las `System.*` tienen que estar junto al
   `Companions.dll`.
3. **El BepInEx no está instalado** (o está en una versión incompatible). Usa el **BepInExPack_Valheim 5.4.2351**.
4. **Versión del juego incompatible.** El mod es para **Valheim 1.0.x** (Unity 6).

> **El error de PlayFab / NullReferenceException al arrancar es normal** y **no** es del mod. No te preocupes por él.

---

## Mis ajustes volvieron a los valores por defecto (o "el mod no lee mi `.cfg`")

**Causa:** la **1.8.3** fue la versión en la que el archivo de configuración cambió de nombre y de idioma (por eso un
archivo de la 1.8.2 o anterior no tiene efecto). El mod lee **`BepInEx\config\AiCompanions.cfg`**;
`Maddhattr.OfflineCompanionsReborn.cfg` (de las versiones anteriores) **ya no se lee** y se queda en el disco **sin
efecto** — por eso las opciones vuelven a los **valores por defecto**, incluida la `ApiKey` en blanco. La sección de la
IA también pasó a **`[AI]`**, con las claves en **inglés**.

**Qué hacer:**

1. Confirma que el archivo existe: `<Valheim>\BepInEx\config\AiCompanions.cfg` (se crea en el primer arranque).
2. Rehaz lo que tenías a mano — lo más importante es la **`[AI] ApiKey`**. El menú **`F8`** (o el panel `I`) muestra
   todo en el juego, con los nombres en el **idioma del juego**.
3. Borra el `.cfg` antiguo para no confundirte: **no** vuelve a leerse.
4. Revisa en el log la línea `[Config] Maddhattr.OfflineCompanionsReborn.cfg was replaced by AiCompanions.cfg ...`.

> **La partida, el mundo, los compañeros, las casas y el inventario no se ven afectados** — el **GUID** del plugin y
> las claves `HC_*` no cambiaron. Solo la configuración del mod vuelve a los valores por defecto.
>
> **La 1.9.x no resetea nada.** Si tu archivo ya es el `AiCompanions.cfg` (1.8.3 o más nuevo), la actualización solo
> mueve los valores fijos antiguos de voz/idioma (`pt`, `pt-br`, `42` y el prompt en portugués) a `auto` — tu clave y
> tus otras elecciones se quedan.

---

## ¿Aún no se resolvió?

Junta esta información y abre un issue en
https://github.com/julianu37/AI-Companions/issues :

- El trozo del `LogOutput.log` alrededor del problema (las líneas `[IA]`, `[STT]` o `[TTS]`).
- La versión del mod (aparece en el arranque del log) y la versión de Valheim.
- Qué hiciste (chat o voz) y qué pasó.
