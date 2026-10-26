> 🌍 [English](Commands) · [Português](Commands-pt) · **Español** · [Français](Commands-fr) · [Italiano](Commands-it)

# Comandos

Teclas, órdenes por chat/voz y comandos de consola.

---

## Teclas

| Tecla | Acción |
|---|---|
| **`V`** | **Push-to-talk**: mantenla y habla; suéltala para que él entienda |
| **`Z`** | **Tecla de comando**: mira algo y tócala para mandar a los compañeros (mover / atacar / abrir una puerta / sentarse / reparar). **Mantén** para llamarlos |
| **`E`** | **Mantén** mirando a un compañero para abrir el **menú radial**; **toca** para abrir su panel / inventario |
| **`LeftAlt` + `Z`** | Editor de zona de cultivo |
| **`I`** | **Panel de IA**: una **pestaña por compañero** (nombre, tipo, Llamar, Apariencia, su voz y personalidad) y la **pestaña IA** (invocador de compañeros, ajustes del LLM con el **selector de proveedor**, volumen, memoria, conversación y push-to-talk) — todo guardado al momento. La **velocidad del habla** se queda en el **`F8`** |
| **`F8`** | Panel de configuración del mod (todas las opciones, en el juego) |
| **`F5`** | Consola del juego (para los comandos de prueba del mod) |

> El panel de IA (tecla **`I`**, cambiable en `[AI] PanelKey`) se abre **sin necesitar un compañero cerca**, se cierra
> con **`I`**, **`Esc`** o la **X**, y cada control se guarda al momento (en el `.cfg`, cuando es una opción global; en
> el compañero, cuando es de su pestaña). Mientras está abierto, tu personaje no responde a los controles, y la tecla
> se ignora con el inventario, el mapa, el menú o el panel `F8` abiertos.

> **Tipos:** los **dverger** (**Mago**, **Mago de Fuego**, **Mago de Hielo** y **Apoyo**) son **de combate** — la rueda
> muestra solo las acciones de combate/apoyo y las órdenes de **trabajo** (**chat**, **voz** o la **tecla de comando**)
> son rechazados con *"Soy de combate, no hago ese trabajo."*. Para trabajar, usa el tipo **Compañero**
> (**[Cómo funciona](How-it-works-es#tipos-de-compañero)**).

### Menú radial (mantén `E`)

Mantén **`E`** mirando a un compañero para abrir la rueda de acciones (mover, atacar, abrir una puerta, sentarse,
reparar…). Un **toque** en `E` abre su panel / inventario.

![El menú radial abierto, con las acciones alrededor del centro](images/en-radial-menu.jpg)

---

## Órdenes por chat y por voz

Escribes en el chat del juego **o** hablas manteniendo **`V`**. La orden lo interpreta un LLM: puedes decir lo mismo
de **varias formas** — no tienes que memorizar la frase exacta.

![El chat con una orden, la respuesta del compañero y el bocadillo de habla](images/en-companions-chat-orders.jpg)

### Por el nombre

Empieza el mensaje con el **nombre** de un compañero para hablar **solo** con él: `Gruncha, recoge 10 piedras` (el
prompt, la personalidad, la memoria y la orden son suyos). **Sin** el nombre, la orden vale para **todos** los que
puedan atenderlo. Un comienzo que parece un nombre y no existe (`Fulano, recoge 10 piedras`) se avisa — y no se manda
nada —, pero una forma de tratamiento sigue valiendo (`jefe, recoge 10 piedras`).

> El nombre es el que le diste en la **pestaña del compañero** (panel `I`) o el nombre del tipo, si nunca se renombró.

### Seguir y parar

| Dices | Qué hace |
|---|---|
| `sígueme` | Vuelve a seguirte |
| `para` / `para de seguirme` | Para lo que está haciendo y se queda quieto — y la respuesta dice **el trabajo** que paró ("dejar de recoger madera", "dejar de cocinar") |
| `ven aquí` | Viene hasta ti |

> No existe un "seguir de cerca" aparte: las opciones son **seguirte** o **parar**.

### Recoger

| Dices | Qué hace |
|---|---|
| `recoge 10 de madera` / `tala unos árboles` | Recoge **madera** |
| `recoge 10 piedras` / `coge 10 piedras` | Recoge **piedra** |
| `mina hierro` / `pica mineral` | **Mina** mineral |
| `coge las bayas` / `recoge las setas` | **Recolecta** (plantas, frutas, setas) |

> **El número es un objetivo.** Lo cuenta **en el inventario del compañero** (su mochila — no cuenta cofres ni pilas
> en el suelo). Si ya tiene 10 de madera y pides 10, avisa *"Ya tenemos bastante."* y no sale a recoger. El objetivo
> vale **solo para el recurso orden**.

### Atacar

| Dices | Qué hace |
|---|---|
| `ataca a ese greydwarf` | Ataca a la criatura **nombrada** (incluso en la postura Pasivo) |
| `mata al jabalí` | Lo mismo |
| `ataca al trol` | Lo mismo |

> La criatura se busca **primero en tu mira**; después, la más cercana dentro de `[AI] TargetRange` (40 m por
> defecto). La que estás mirando vale **siempre**, por lejos que esté. *"Atacar"* sin nombrar criatura se vuelve
> **caza**.

### Equipar y soltar

| Dices | Qué hace |
|---|---|
| `equipa el hacha de bronce` / `coge el pico` | Equipa el objeto **que él ya lleva** |
| `suelta la piedra` / `tira 5 de madera` | Suelta el objeto en el suelo (sin número = la pila entera) |

> `equip` y `drop` solo valen para lo que él **ya tiene en la mochila**. Un objeto de misión nunca se cae.

### Objetos en el suelo

| Dices | Qué hace |
|---|---|
| `recoge los objetos` / `recoge la madera del suelo` | Anda hasta los objetos sueltos y los coge **uno por uno** |
| `coge lo que hay en el suelo` | Lo mismo — coge lo que esté **más cerca**, no un objeto elegido por el nombre |
| `deja de recoger` / `no cojas nada` | **Apaga** la recogida automática |

> La recogida coge **incluso lo que él mismo soltó a propósito** — que es justo lo que el imán automático del juego
> ignora. Se rinde si el objeto no cabe entero en la mochila o si está demasiado lejos.

### Inventario

| Dices | Qué hace |
|---|---|
| `abre tu inventario` / `muéstrame lo que llevas` | Abre **la ventana del inventario de él** para que lo veas |
| `cierra el inventario` | Cierra la ventana (la **X** de la ventana siempre cierra) |

> Cerrar es una acción propia (`inventory_close`): si el modelo responde "abrir" para una frase con verbo de cerrar,
> el mod lo corrige por **tu** frase. La **X** de la ventana y `Esc` siempre cierran.

![El inventario del compañero abierto: la ventana "SOPHIA" con lo que lleva y los cuatro huecos de Comida, junto a tu inventario y al panel de fabricación](images/en-companions-inventory.jpg)

### Postura de combate

| Dices | Qué hace |
|---|---|
| `quédate en defensiva` | Postura **pasiva** (defensiva) |
| `quédate equilibrado` | Postura **equilibrada** |
| `ataca a distancia` | Postura **a distancia** |
| `lucha cuerpo a cuerpo` | Postura **cuerpo a cuerpo** |

### Trabajos

| Dices | Qué hace |
|---|---|
| `cuida del cultivo` | Trabajo de **cultivo** |
| `vete a pescar` | Trabajo de **pesca** |
| `repara las construcciones` | Trabajo de **reparación** |
| `ponte a cocinar` | Trabajo de **cocina** |
| `funde el mineral` | Trabajo de **fundición** |
| `vete de caza` | Trabajo de **caza** |
| `fija la casa aquí` | Fija la **casa** |
| `vaga por aquí` / `deja de vagar` | Enciende/apaga el **vagar** |
| `acepta comandos` / `ignora comandos` | Enciende/apaga **aceptar órdenes** |

### Conversación

| Dices | Qué hace |
|---|---|
| `buenos días` | Responde como personaje |
| `¿qué tal?` | Lo mismo |
| `¿qué te parece mi casa?` | Lo mismo |

> Cuando el mensaje **no** es una orden, el compañero **conversa**: dice la respuesta en voz alta y la muestra en el
> chat. Eso lo controla `[AI] Conversation` (`true` por defecto). Apagado = solo se entienden las órdenes.

---

## Comandos de consola (`F5`)

| Comando | Qué hace |
|---|---|
| `modia_tts_teste` | Dice una frase de prueba por el TTS (pipeline completo: modelo, síntesis y reproducción) |
| `modia_tts_teste <texto>` | Dice el texto que escribas — p. ej. `modia_tts_teste ¡Buenos días, jefe!` |
| `modia_stt_teste` | Corre el reconocimiento del Whisper sobre un WAV, **sin** usar el micrófono |

> `modia_stt_teste` transcribe el archivo de `[AI] SttWavTest` (`whisper\jfk.wav` por defecto) y escribe el resultado
> en el log. Sirve para comprobar si el modelo del Whisper está cargando, cuando las órdenes por voz no salen.
>
> **El paquete no trae ese WAV** (salió del paquete para ahorrar espacio) — pon un archivo tuyo en la carpeta
> `whisper\` y apunta `[AI] SttWavTest` a él. El comando **no** acepta la ruta como argumento.

---

## Notas importantes

- **El idioma sigue al juego.** Desde la 1.9.0 la interfaz, las frases, los presets de personalidad y el contrato del
  LLM están en **cinco idiomas** (PT-BR, EN, ES, FR, IT) y siguen el **idioma del juego** — las órdenes se entienden
  en ese idioma también, y la voz y el reconocimiento del habla son **automáticos** (`auto` / `0`).
- **El número es un objetivo contado en su mochila.** `recoge 10` = **tener 10 en el inventario del compañero**. Con
  15, ni empieza y dice que ya tiene bastante; con 7, recoge hasta 10 (el último árbol puede pasarse).
- **La orden la lee el LLM**, así que puedes hablar a tu manera. `coge 10 piedras`, `voy a necesitar piedra`,
  `junta unas piedras` — todas deberían caer en la misma acción.
- **`[AI] Prefix`** (vacío por defecto): si lo rellenas, por ejemplo con `/c`, **solo** los mensajes que empiezan con
  ese prefijo van al modelo. El resto del chat queda en paz.
- **`[AI] CooldownSeconds`** (`3` por defecto): tiempo mínimo entre dos órdenes, para que una ráfaga de mensajes no
  inunde el modelo.
- **El chat y la voz entran por el mismo camino.** Lo que funciona escrito funciona hablado, con la misma
  personalidad, memoria y conversación.
- **`despawn` no está expuesto al modelo.** No puedes mandar al compañero desaparecer por chat o voz.

---

## Ajustes de comodidad del mod

- **La tecla `V`** ya no dispara el *Toggle Auto-Pickup* del juego (mira **[Voz](Voice-es)**).
- **Radio de recogida mayor:** madera/piedra/mineral se buscan hasta **25 m** de ti, y los drops se cogen hasta
  **12 m**.
- **Chatter aleatorio apagado** por defecto: las frases sueltas cada 15–30 s ya no pasan. Las frases útiles (órdenes,
  cambio de modo, respuestas de la IA, eventos) siguen.

Para ver todas las opciones, ve a **[Configuración](Configuration-es)**.

<!-- D4: add Español/Français/Italiano -->
