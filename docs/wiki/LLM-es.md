> 🌍 [English](LLM) · [Português](LLM-pt) · **Español** · [Français](LLM-fr) · [Italiano](LLM-it)

# LLM (Groq, LM Studio u OpenRouter)

El mod entiende las órdenes mandando el texto a un **LLM** por una API **compatible con OpenAI**. Hay tres caminos, y
eliges **uno**:

| Camino | Qué necesita | A dónde va el texto | Cuándo elegirlo |
|---|---|---|---|
| **Groq** — **por defecto** | internet y una **clave gratis** | **sale de tu máquina** (servicio remoto) | Quieres usarlo sin instalar nada, y rápido |
| **LM Studio** (u Ollama) — opcional | el modelo descargado y el servidor encendido | se queda **en tu máquina** | Quieres privacidad total o uso intenso |
| **OpenRouter** — alternativa | internet y una **clave gratis** | **sale de tu máquina** (servicio remoto) | Ya usas OpenRouter o quieres probar otro modelo |

Esta página empieza por **Groq**, porque **ya es el valor por defecto** del mod: lo instalas, pegas la clave y listo.
Después viene la opción **local** (**LM Studio**, que sigue funcionando igual) y, al final, **OpenRouter** — que
también sigue funcionando, pero cuyo tier gratuito es **lento** y por eso dejó de ser el valor por defecto.

> **¿Vienes de una versión anterior (o de la 1.8.2)?** En la **1.8.3** el archivo pasó a ser **`AiCompanions.cfg`** y
> las configuraciones **vuelven a los valores por defecto** — incluida la `ApiKey`, que hay que pegar otra vez. La
> sección también cambió de nombre: era `[IA]`, ahora es **`[AI]`**, con las claves en **inglés** (`Model`, no
> `Modelo`). El `.cfg` antiguo (`Maddhattr.OfflineCompanionsReborn.cfg`) se queda en el disco y **no** se lee.
> La **1.9.0 no resetea nada**: si el archivo ya es el `AiCompanions.cfg`, se queda como está (solo los valores fijos
> antiguos de voz/idioma pasan a `auto`).
> Cambiar a Groq es opcional — y es solo editar `BaseUrl`, `Model` y `ApiKey` (en el `.cfg` o en la sección
> **Ajustes del LLM** del panel `I`).

---

## 1. Groq: solo la clave e internet (por defecto)

**Groq** es un servicio que habla la **misma API que OpenAI** y sirve modelos abiertos, con un **tier gratuito**. Para
el mod, es el camino más rápido: no hay nada que instalar ni servidor que dejar abierto — el `.cfg` ya viene apuntado
a él.

### 1.1. Crea tu clave gratis

1. Abre **https://console.groq.com** y crea la cuenta (correo o login social).
2. Ve a **https://console.groq.com/keys** y haz clic en **Create API Key** (ponle cualquier nombre, p. ej. `Valheim`).
3. **Copia la clave generada** — es la que va en `[AI] ApiKey`. Se muestra **una sola vez**; si cierras la ventana sin
   copiarla, crea otra.

### 1.2. Qué poner en el `.cfg` (o en el panel)

Abre `<Valheim>\BepInEx\config\AiCompanions.cfg` y revisa la sección `[AI]`:

```ini
[AI]
BaseUrl = https://api.groq.com/openai/v1
Model = openai/gpt-oss-20b
ApiKey = <tu clave>
StructuredOutput = true
```

| Clave | Valor | Por qué |
|---|---|---|
| `BaseUrl` | `https://api.groq.com/openai/v1` | La dirección de Groq — **ya es el valor por defecto**. El `/chat/completions` se añade solo |
| `Model` | `openai/gpt-oss-20b` | El modelo abierto de OpenAI servido por Groq — **ya es el valor por defecto**. Es rápido y obedece bien a la salida estructurada |
| `ApiKey` | **tu clave** | En Groq es **obligatoria**. Vacía o mal, el servidor rechaza la petición con un error de autenticación |
| `StructuredOutput` | `true` | Pide el formato JSON al servidor — es lo que hace funcionar a los modelos pequeños. El `gpt-oss-20b` acepta `json_schema` en modo **strict** (decodificación restringida), que es el modo que usa el mod |

También puedes hacerlo **en el juego**, sin abrir el archivo: tecla **`I`** → pestaña **IA** → **Ajustes del LLM**. Es
donde están la **URL del servidor**, el **Modelo**, la **Clave de API** y los botones **Aplicar** y **Probar conexión**
— y, **encima de la URL**, el desplegable **"Elige tu proveedor LLM"**: elegir **Groq**, **LM Studio** u
**OpenRouter** **rellena la URL y el modelo** solo (la **clave no se toca** y nada se guarda antes de **Aplicar**). En
la línea de la **Clave de API** está el botón **"Crea tu clave de API gratis AQUÍ"**, que **abre en el navegador** la
página de claves del proveedor elegido (`console.groq.com/keys` en Groq, `openrouter.ai/keys` en OpenRouter) —
desaparece en LM Studio, que no tiene clave.

> Los dos son **solo un atajo**: el `.cfg` sigue mandando y es lo que cuenta. El selector **deduce** el proveedor de la
> propia `BaseUrl`, así que una URL de **Ollama** o de un servidor tuyo aparece como **Personalizado (URL manual)** y
> no se sobrescribe nada.

![El panel de IA (tecla I), pestaña IA: el desplegable "Elige tu proveedor LLM" en Groq, la URL del servidor con api.groq.com, el modelo openai/gpt-oss-20b, la clave de API enmascarada y, a la derecha de la clave, el botón "Crea tu clave de API gratis AQUÍ"](images/en-ai-panel.jpg)

> En la captura de arriba el selector está en **Groq** (el valor por defecto), con la URL
> `https://api.groq.com/openai/v1`, el modelo `openai/gpt-oss-20b` y la clave enmascarada. Cambiar el selector a
> **LM Studio** u **OpenRouter** rellena la URL y el modelo de ese proveedor — y el botón de la clave pasa a apuntar a
> su página (o desaparece, en LM Studio).

> `BaseUrl`, `Model` y `ApiKey` también aparecen en el panel **`F8`**, pero son claves de **texto**: para pegar una
> clave, el campo del panel `I` (o el propio `.cfg`) es más cómodo. Lo que guardas ahí vale en el **mensaje
> siguiente**, sin reiniciar el juego.

### 1.3. Prueba la conexión

Con la URL, el modelo y la clave rellenados, pulsa **Probar conexión** (en el panel `I`, junto a **Aplicar**). La
prueba corre **en segundo plano** — el juego no se congela — y responde en el **chat/HUD**, además de dejar una línea
`[IA]` en el log. Empieza pidiendo al servidor la **lista de modelos** y, si el servidor no tiene esa lista, manda una
petición mínima de chat. El resultado es uno de estos:

- **Conexión OK: N modelo(s) en el servidor** — la URL respondió.
- **Conexión OK: … y el modelo configurado está en la lista** — el mejor caso.
- **Conexión OK: … pero "<modelo>" no está en la lista** — la URL respondió, pero el nombre no apareció en la lista que
  devuelve el servidor. Revisa que el `Model` esté escrito exactamente `openai/gpt-oss-20b` y sigue al test en el
  juego, mandando una orden de verdad.
- **Fallo de conexión: …** — viene con el motivo (estado/mensaje del servidor): URL mal, clave vacía/mal, dirección sin
  `http://`/`https://`…

> **Quien confirma la clave es una orden de verdad.** **Probar conexión** dice que la URL responde y que el modelo está
> en la lista; si pasa pero la orden falla con **401**, el problema es la clave — mira
> **[Problemas comunes](Troubleshooting-es)**.

### 1.4. Qué esperar de la respuesta

El entendimiento sigue corriendo **en segundo plano**: un modelo lento **no** congela el juego. La latencia aparece en
el log, en cada orden:

```
[IA] Tempo: 378 ms (... tokens, ... tokens/s)
```

Con el `openai/gpt-oss-20b` en Groq la respuesta llega en una **fracción de segundo** en nuestra medición — mucho más
rápido que el tier gratuito de OpenRouter. El modelo se sirve a ~**1.000 tokens/s** y tiene ventana de **131.072
tokens** (máximo de conclusión 65.536), lo que sobra para el prompt del mod (fuente:
https://console.groq.com/docs/models). El mod avisa en el log cuando una orden pasa de **5 s**, y el
`[AI] TimeoutSeconds` (**30** por defecto) es el techo de espera.

### 1.5. Límites del tier gratuito

Lo que la documentación de Groq informa hoy para el `openai/gpt-oss-20b` en el plan gratuito (fuente:
https://console.groq.com/docs/rate-limits — los números pueden cambiar):

| Límite | Valor |
|---|---|
| Peticiones por minuto (**RPM**) | **30** |
| Peticiones por día (**RPD**) | **1.000** |
| Tokens por minuto (**TPM**) | **8.000** |
| Tokens por día (**TPD**) | **200.000** |

En la práctica, **el TPM es el límite que muerde primero**: el prompt del mod gasta unos **1.800 tokens por orden**,
lo que da **~4 órdenes por minuto**. Por encima de eso Groq responde **429** y la orden se rechaza con un aviso —
nada se rompe y nada se queda atascado; espera un poco y mándalo otra vez.

- El techo de **8.000 tokens por minuto** atiende una sesión normal de órdenes esporádicas. El uso intenso (muchos
  órdenes y conversación todo el tiempo) choca con él — ahí la **opción local** (abajo) lo resuelve, sin límite de
  peticiones.
- El `[AI] CooldownSeconds` (**3** por defecto) ayuda: evita que una ráfaga de mensajes se vuelva una ráfaga de
  peticiones.
- **429** es límite de peticiones/tokens, **401** es la clave — la diferencia está en
  **[Problemas comunes](Troubleshooting-es)**.

> **La salida estructurada sigue valiendo.** El `gpt-oss-20b` acepta `json_schema` en modo **strict** y también el
> modo *best-effort*; **streaming** y **tool use** no se soportan junto con la salida estructurada — y el mod **no**
> usa ninguno de los dos (fuente: https://console.groq.com/docs/structured-outputs).

### 1.6. Privacidad y la clave

- **Con Groq, el texto de las órdenes y de la conversación sale de tu máquina.** Va a los servidores de Groq — la
  persona, la memoria y lo que escribes entran en ese envío. **No escribas nada sensible** en el chat del compañero.
- Groq es un **servicio remoto**: su política de datos es la que vale, no la del mod.
- El reconocimiento del habla (**STT**) y la **voz del compañero** (**TTS**) siguen **locales**: solo el texto del
  orden va al LLM.
- **La clave se queda en el `.cfg`.** No compartas ese archivo con la clave rellenada y nunca subas una clave a un
  repositorio. Si se filtra, bórrala en **https://console.groq.com/keys** y crea otra.
- ¿Quieres que **no salga nada** de la máquina? Usa el LM Studio (u Ollama) justo abajo — ahí el LLM también es local.

---

## 2. Alternativa local: LM Studio (opcional)

Aquí **nada sale de tu computadora**: el modelo corre en tu máquina. Es el camino para quien quiere privacidad total,
para quien juega sin internet o para quien hace uso intenso y no quiere depender de una cola ni de un límite de
tokens. A cambio, tienes que descargar el modelo y dejar el servidor encendido durante el juego.

### 2.1. Instala el LM Studio

Descárgalo de **https://lmstudio.ai** e instálalo. Es una aplicación de ventana, sin línea de comandos.

### 2.2. Descarga el modelo

El modelo **recomendado** es el **`qwen2.5-3b-instruct`**, cuantización **Q4_K_M** (~**1,93 GB**).

En LM Studio:

1. Abre la pestaña de **búsqueda de modelos** (el icono de lupa, en la barra lateral).
2. Busca **`qwen2.5-3b-instruct`**.
3. Elige la cuantización **Q4_K_M** y descárgala (~1,93 GB).

> **¿Por qué un modelo tan pequeño?** Porque es **rápido** (órdenes en ~0,35–0,4 s) y, con la **salida estructurada**
> del mod, responde en el formato correcto de forma fiable. Un 3B corre bien incluso en CPU.

### 2.3. Carga el modelo

En la pestaña de **chat**, arriba, elige el `qwen2.5-3b-instruct` que descargaste. Se pone **READY** cuando termina de
cargar.

### 2.4. Enciende el Local Server

Abre la pestaña **Model Server** en la barra lateral (en versiones antiguas del LM Studio se llama **Developer**) y
enciende el **Local Server** (el botón **Start** / **Running**).

![El LM Studio con el Local Server encendido, el modelo cargado y el API Model Identifier](images/lmstudio-servidor.png)

Revisa en la pantalla:

| Qué mirar | Valor esperado |
|---|---|
| **Status** | `Running` |
| **Puerto** | `1234` |
| **Modelo cargado** | `qwen2.5-3b-instruct` (READY, ~1,93 GB) |
| **Parallel** | `4` |
| **Endpoints** | OpenAI-compatible (`/v1/models`, `/v1/chat/completions`) |
| **API Model Identifier** | `qwen2.5-3b-instruct` — es **este** nombre el que va en `[AI] Model` |

> El **API Model Identifier** (en el panel *Model Information*) es el nombre exacto que expone el servidor. Úsalo en el
> `.cfg`, y no el nombre de la carpeta de la descarga.

### 2.5. Qué poner en el `.cfg` para usar el LM Studio

El valor por defecto del mod es Groq, así que **cambia** las dos primeras claves y **borra** la clave de API:

```ini
[AI]
BaseUrl = http://127.0.0.1:1234/v1
Model = qwen2.5-3b-instruct
ApiKey =
StructuredOutput = true
```

| Clave | Valor | Por qué |
|---|---|---|
| `BaseUrl` | `http://127.0.0.1:1234/v1` | Dirección del servidor del LM Studio. El `/chat/completions` se añade solo |
| `Model` | `qwen2.5-3b-instruct` | El **API Model Identifier** que muestra el LM Studio |
| `ApiKey` | *(vacío)* | El LM Studio local no pide clave |
| `StructuredOutput` | `true` | Pide el formato JSON al servidor — es lo que hace funcionar a los modelos pequeños |

> El **Probar conexión** del panel `I` funciona igual aquí: cambia la URL y el modelo en los campos, prueba y solo
> después haz clic en **Aplicar**.

### 2.6. `127.0.0.1` vs `localhost` — usa la IP

En el caso local, usa la dirección **`http://127.0.0.1:1234/v1`** (IPv4) y no `localhost`.

> **En Windows, `localhost` resuelve primero a IPv6 (`::1`)**. El servidor del LM Studio solo escucha **IPv4**, así que
> cada petición intenta IPv6 primero, espera el tiempo límite y **solo después** cae a IPv4. Eso añade unos **2,5 s
> de peaje en cada orden**.

Con la dirección explícita `http://127.0.0.1:1234/v1` la respuesta vuelve directa. El texto de ayuda del `BaseUrl` en
el `.cfg` (y en el panel `F8`) también explica la diferencia.

---

## 3. OpenRouter (sigue funcionando, pero el gratuito es lento)

**OpenRouter** fue el valor por defecto hasta la **1.7.0** y **sigue funcionando**. Da acceso a varios modelos con la
misma clave, incluidos los **gratuitos** — pero el router gratuito (`openrouter/free`) es **lento e impredecible**:
cada petición elige un modelo gratuito diferente y pasa por la cola del proveedor. Por eso dejó el puesto de valor por
defecto. Úsalo si **ya tienes** la configuración o quieres **probar otro modelo** del catálogo.

```ini
[AI]
BaseUrl = https://openrouter.ai/api/v1
Model = openrouter/free
ApiKey = <tu clave de openrouter.ai/keys>
```

| Clave | Valor | Por qué |
|---|---|---|
| `BaseUrl` | `https://openrouter.ai/api/v1` | Dirección de OpenRouter |
| `Model` | `openrouter/free` | **Free Models Router**: en cada petición elige un modelo **sin coste** (y filtra los que soportan la salida estructurada) |
| `ApiKey` | **tu clave** | En OpenRouter es **obligatoria**. La gratuita sale en **https://openrouter.ai/keys** |

- Los modelos **gratuitos** (con `:free` en el nombre) tienen límite de **20 peticiones por minuto** y un **techo
  diario** por cuenta: **50 peticiones al día** para quien **nunca compró crédito** y **1.000 al día** a partir de **10
  créditos comprados** (en total, no por mes).
- La latencia **varía** de una orden a otra y puede tardar unos segundos en hora punta.
- El propio OpenRouter describe los modelos gratuitos como **para probar y para uso de bajo volumen**.
- El **Probar conexión** del panel `I` también sirve aquí — y, como la lista de modelos de OpenRouter es **pública**,
  puede responder `Conexión OK` incluso con la clave mal: quien confirma la clave es una orden de verdad.
- Con OpenRouter, el texto de las órdenes y de la conversación **sale de tu máquina**, como en Groq. Su política de
  datos es la que vale. ¿Clave filtrada? Bórrala en **https://openrouter.ai/settings/keys** y crea otra.

---

## 4. Cómo saber que está funcionando

Entra al mundo y escribe en el chat `recoge 10 de madera` (vale para Groq, para el **LM Studio** y para **OpenRouter**).
Si funciona:

- El compañero responde (y habla) una confirmación y sale a recoger.
- En el log (`BepInEx\LogOutput.log`) aparecen las líneas `[IA]` con el resultado y el tiempo.

---

## 5. Otros modelos y servidores

Cualquier endpoint **compatible con OpenAI** sirve — el mod solo hace `POST {BaseUrl}/chat/completions`.

### Modelos

| Modelo | Servidor | Observación |
|---|---|---|
| **`openai/gpt-oss-20b`** | **Groq** | **Por defecto.** Modelo abierto, ~1.000 tokens/s, ventana de 131.072 tokens. Responde en fracción de segundo |
| **`qwen2.5-3b-instruct`** | LM Studio | **Recomendado en local.** Rápido (~0,35–0,4 s por orden) y fiable con la salida estructurada |
| `openrouter/free` | OpenRouter | Router de modelos gratuitos: elige uno por petición. Sin instalar nada, pero la latencia varía |
| `google/gemma-4-e4b` | LM Studio | También funciona, pero es **mucho más lento** (~3 s por orden) |
| Modelos mayores | cualquiera | Mejores en conversación, pero **necesitan GPU** para responder rápido en local |
| Modelos de pago de Groq/OpenRouter | — | Basta cambiar el `Model` por el nombre del catálogo — y, ahí, los créditos de la cuenta son los que se gastan |

### Otros servidores

- **Ollama:** `http://127.0.0.1:11434/v1`
- **llama.cpp** con el servidor en modo OpenAI
- **API de pago** (OpenAI, etc.): apunta el `BaseUrl` y rellena la `ApiKey` — recordando que, ahí, tus mensajes **salen
  de tu máquina**, como en Groq y OpenRouter.

> **Nunca subas una clave de API a un repositorio.** La `ApiKey` se queda en el `.cfg` local; no compartas ese archivo
> con la clave rellenada.

### Ajustes útiles

| Clave `[AI]` | Por defecto | Qué hace |
|---|---|---|
| `Temperature` | `0.1` | Mantenla baja: el mismo orden debe dar siempre el mismo JSON |
| `TimeoutSeconds` | `30` | Cuánto esperar por el modelo. La petición corre en segundo plano — un modelo lento no congela el juego |
| `StructuredOutput` | `true` | Si tu servidor/modelo no lo soporta, el mod cae solo en el formato antiguo y avisa en el log |
| `CooldownSeconds` | `3` | Tiempo mínimo entre dos órdenes, para que una ráfaga de mensajes no inunde el modelo |

---

## Si no funciona

Los cuatro casos de abajo cubren casi todo:

| Qué aparece | Qué es | Qué hacer |
|---|---|---|
| **401** (*unauthorized* / *invalid api key*) | La `ApiKey` está vacía, mal o es de otro servidor | Pega la clave correcta: `console.groq.com/keys` (Groq), `openrouter.ai/keys` (OpenRouter) — o déjala **vacía** en LM Studio/Ollama |
| **429** (*rate limit*) | Límite de **peticiones o de tokens** del tier gratuito | Espera unos segundos y manda la orden otra vez; reduce la frecuencia; usa el LM Studio local para uso intenso |
| **404** / **400** | La **URL** (`BaseUrl`) o el **modelo** (`Model`) están mal | Revisa `https://api.groq.com/openai/v1` + `openai/gpt-oss-20b` (Groq) u `https://openrouter.ai/api/v1` + `openrouter/free` (OpenRouter) |
| **Tarda mucho** | Cola del proveedor o modelo grande | Mira "tarda mucho en responder" en **[Problemas comunes](Troubleshooting-es)** |

El paso a paso de cada síntoma está en **[Problemas comunes](Troubleshooting-es)** — incluidos "no responde a nada",
"responde error de autenticación", "tarda mucho en responder" y "responde mal o ignora el número".

<!-- D4: add Español/Français/Italiano -->
