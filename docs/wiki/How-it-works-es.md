> 🌍 [English](How-it-works) · [Português](How-it-works-pt) · **Español** · [Français](How-it-works-fr) · [Italiano](How-it-works-it)

# Cómo funciona

La arquitectura del mod y el camino que recorre una orden, desde tu micrófono (o desde el chat) hasta que el
compañero actúa.

---

## Visión general

![Cómo fluye una orden: la voz (micrófono, 16 kHz) o el chat del juego van a la misma bandeja de órdenes; el compañero actúa, el LLM entiende la orden y escribe la respuesta, y la voz local (Kokoro-82M) la dice en su posición](images/flow-ai.png)

Tres piezas de IA, todas en **hilos de fondo** (el LLM puede ser remoto; el STT y el TTS son siempre locales):

| Pieza | Qué es | Dónde corre |
|---|---|---|
| **LLM** | Entiende la orden y conversa | **LM Studio/Ollama** (local, recomendado) o **Groq** (remoto, lo que viene en el `.cfg`) — cualquier endpoint compatible con OpenAI |
| **STT** | Transcribe lo que dices | `Whisper.net` (whisper.cpp) dentro del juego |
| **TTS** | Hace hablar al compañero | `sherpa-onnx` + Kokoro-82M dentro del juego |

---

## El camino de una orden

1. **Entrada.** Escribes en el chat **o** mantienes `V` y hablas. En los dos casos el texto cae en el **mismo** punto
   de entrada — no hay lógica duplicada entre chat y voz.
2. **Prompt.** El mod monta el prompt del mensaje: el **contrato** fijo (en el **idioma del juego**) + el **nombre**
   del compañero + la **personalidad** + la **memoria** reciente.
3. **LLM.** El texto va al servidor configurado (`POST {BaseUrl}/chat/completions`) en una **Task de fondo** — el
   juego nunca espera.
4. **JSON de la intención.** El modelo devuelve una intención estructurada con cinco campos:

```json
{ "action": "wood", "reply": "Voy a por madera.", "target": null, "resource": "wood", "amount": 20 }
```

5. **Parser tolerante.** El mod lee la intención aceptando variaciones (`action`, `reply`, `target`, `resource`,
   `amount`) y **valida** el objetivo contra el mundo real — nunca inventa un objeto ni una criatura.
6. **Ejecución.** La intención se vuelve la **misma acción del menú radial**: la postura de combate, el trabajo, el
   equipo, el objetivo… Todo pasa por el mismo camino que los controles normales del mod.

> La cola de resultados es **thread-safe** y se drena en el `Update`. Nada de bloquear el hilo principal.

---

## Salida estructurada (JSON schema)

La petición al LLM lleva un **JSON schema** (`response_format`) que fija las cinco claves y la **lista cerrada de
30 acciones** — la misma lista del contrato, de `follow` a `none`.

Eso es lo que **desbloquea los modelos pequeños**: con el schema, el servidor está obligado a responder en el formato
correcto. Medido con el prompt actual, el `qwen2.5-3b-instruct` daba **0/6** respuestas en JSON y pasó a **6/6**.

Si el servidor **no soporta** el formato, el mod **cae solo** en una cadena de intentos, uno a la vez:

1. `json_schema` (lo ideal)
2. `{"type":"json_object"}` (JSON suelto)
3. **sin** `response_format` (el formato antiguo)

Solo un **rechazo del formato** dispara el siguiente intento — un timeout, un error de red o un error de servidor
**no** se repiten. Una línea en el log dice cuándo está encendida la salida estructurada y **un solo aviso** aparece
si el mod llega al formato antiguo.

Clave: `[AI] StructuredOutput` (`true` por defecto).

---

## Personalidad, memoria y conversación

- **Personalidad:** presets escritos dentro del mod (**Viking leal**, **Mago sarcástico**, **Sarcástico**,
  **Guía paciente**; los nombres siguen el idioma del juego) o **texto libre** del jugador — el texto libre **gana**
  al preset. Entra en el prompt, así que el compañero responde en el tono que elegiste.
- **Memoria:** los últimos `[AI] Memory` intercambios **por compañero** (0–12). La memoria va como **texto en el
  system prompt**, nunca como mensajes extra — el contrato JSON sigue siendo lo último que lee el modelo. Se **olvida**
  cuando el compañero muere, es retirado o cambias de mundo, y **no se guarda nada en el mundo**.
- **Conversación libre:** cuando la acción es `none` y hay `reply`, el compañero **habla** (bocadillo + voz) y el
  texto aparece en el chat. En una **orden** gana la frase localizada del mod, y el `reply` solo va al log — nada se
  dice dos veces.

![El panel de IA en la pestaña de un compañero: el nombre, el tipo, el estado, Llamar y Apariencia, y Voz y personalidad de este compañero con la voz y el botón Escuchar](images/en-ai-panel-companions-editor.jpg)

---

## La voz del compañero (TTS)

- La voz **sigue el idioma del juego**: `[AI] TtsLanguage` y `TtsVoice` vienen como `auto` / `0`, así que se usa la
  voz por defecto de ese idioma a menos que hayas elegido otra. Cambiar el idioma no exige reiniciar.
- El modelo del Kokoro se carga **una vez**, en segundo plano, cuando entras al mundo.
- La síntesis corre en un **hilo de fondo dedicado**; los trozos listos vuelven por otra cola y se drenan en el
  `Update`.
- El habla se monta **por frases**: la primera ya suena mientras se genera la siguiente.
- Las **frases fijas del mod** van a una **caché** (por bytes, descartando las más antiguas) y salen al momento. Las
  respuestas del modelo, que son nuevas cada vez, **no** entran en la caché.
- La reproducción usa **una** `AudioSource` **3D** en la posición del compañero — el sonido viene de donde él está, no
  del centro de la pantalla.

La regla de **qué se dice** es simple: **la voz acompaña lo que está escrito en la pantalla**. En una orden, la frase
del mod; en una conversación, la respuesta del modelo. Nada se dice dos veces.

---

## No congelar el juego

Esto es un requisito de proyecto, no un detalle:

- Toda llamada de red (LLM) corre en una **Task de fondo**, con **timeout** configurable.
- El STT y el TTS corren en **hilos de fondo** con colas thread-safe, drenadas en el `Update`.
- Nada de `Find*` ni LINQ por frame; lo que se puede cachear por frame, se cachea.
- Los `AudioClip` y los objetos temporales se **destruyen** al terminar.

---

## Privacidad

**La voz y la transcripción nunca salen de tu máquina:**

- Tu habla se transcribe **dentro del juego** (Whisper, local).
- La voz del compañero se sintetiza **dentro del juego** (Kokoro, local).
- El **LLM** es el único que puede ser remoto: el `.cfg` viene apuntado a **Groq** (o **OpenRouter**, como
  alternativa), y ahí el texto de las órdenes y de la conversación **sale de tu máquina**. El camino recomendado
  es el **LM Studio/Ollama** local, donde nada sale (**[LLM (LM Studio, Groq u OpenRouter)](LLM-es)**).

La `ApiKey`, cuando existe, se queda solo en el `.cfg` local — nunca la subas a un repositorio ni compartas ese
archivo con la clave rellenada.

---

## Tipos de compañero

El **tipo** de un compañero viene del **prefab** que el juego usa para crearlo, y es él el que decide **qué hace el
compañero**:

- **Compañero** (el de por defecto) es una copia del **personaje del jugador** — es ese "cuerpo de jugador" el que
  usan los trabajos, y es el **único tipo completo**.
- **Mago**, **Mago de Fuego**, **Mago de Hielo** y **Apoyo** son los **dverger**, copias de los dverger del juego
  (`Dverger`, `DvergerMage`…): son **de combate**, por diseño.

**Los dverger son de combate.** Su **rueda** muestra solo las acciones de combate y de apoyo — **Seguir**, **Reparar y
reabastecer**, **Retirar**, **Comando**, **Recoger**, **Vagar**, **Fijar casa** y las **cuatro posturas**
(Equilibrado/Cuerpo a cuerpo/A distancia/Pasivo), igual que el upstream. Luchan normalmente y siguen atendiendo a los
órdenes de combate, movimiento y recogida.

Las órdenes de **trabajo** — recoger, minar, cultivar, pescar, cazar, cocinar, fundir, reparar, reabastecer — son
**rechazados**, vengan del **chat**, de la **voz** o de la **tecla de comando** (apuntando a un árbol, a una roca o a
un horno). La respuesta es siempre la misma frase: **"Soy de combate, no hago ese trabajo."** Y nada cambia: el **modo
actual** y el **objetivo de la orden** se quedan como estaban.

Cambiar el **tipo** de un compañero que estaba trabajando a un dverger también **descarta el objetivo de la orden** — él
vuelve a **seguirte**.

**Para trabajos, usa el tipo "Compañero".** En los dverger, el **panel** esconde los huecos de comida por tipo y las
piezas de armadura se quedan en la mochila en vez de equipadas.

![Cuatro compañeros alrededor del jugador en la plataforma de inicio, sus nombres en el mundo y la lista de compañeros con salud y stamina a la derecha](images/en-multiple-companions-2.jpg)

---

## Qué es del upstream y qué es nuestro

| Viene del upstream (Offline Companions Reborn, MIT) | Es nuestro |
|---|---|
| El compañero: trabajos, comida, stamina, habilidades, casa, descanso, portales, carro | **La traducción en cinco idiomas** — 737 claves de interfaz y 186 frases por idioma, con el **inglés nativo** |
| La IA de movimiento y combate, los trabajos, el menú radial y la UI base | La **capa de IA**: órdenes por chat/voz, personalidad, memoria, conversación |
| Toda la lógica de juego que activan las órdenes | El **TTS local** (Kokoro) y el **STT local** (Whisper) |
| | El **panel de IA** (tecla `I`) |
| | Ajustes: radio de recogida mayor, chatter apagado, el parche de la tecla `V` |
| | El `.cfg` con **nombre propio** (`AiCompanions.cfg`), la sección `[AI]` y las claves en inglés |

![El inventario del compañero abierto: la ventana "SOPHIA" con lo que lleva y los huecos de Comida, junto a tu inventario y al panel de fabricación](images/en-companions-inventory.jpg)

> La IA **no** sustituye nada: es solo una **nueva entrada** para las acciones que ya existían.

---

## Documentación técnica

El detalle de cada fase (el porqué de cada decisión) está en el repositorio:

- Roadmap y fases: https://github.com/julianu37/AI-Companions/blob/main/docs/roadmap.md
- Notas de ingeniería inversa de Valheim 1.0: https://github.com/julianu37/AI-Companions/tree/main/docs/re
- Changelog: https://github.com/julianu37/AI-Companions/blob/main/package/CHANGELOG.md
