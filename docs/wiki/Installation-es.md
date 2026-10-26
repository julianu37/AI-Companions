> 🌍 [English](Installation) · [Português](Installation-pt) · **Español** · [Français](Installation-fr) · [Italiano](Installation-it)

# Instalación

El paso a paso completo, de cero hasta el compañero respondiendo a una orden.

Antes de empezar, confirma que tienes todo lo que está en **[Requisitos](Requirements-es)**.

---

## 1. Ten Valheim instalado

El juego tiene que estar en la versión **1.0.x** (Unity 6). La instalación por defecto de Steam está en:

```
C:\Program Files (x86)\Steam\steamapps\common\Valheim
```

En los pasos siguientes, `<Valheim>` es esa carpeta.

---

## 2. Instala BepInEx

El mod es un plugin de BepInEx 5. Instala **BepInExPack_Valheim 5.4.2351**:

1. Descárgalo de https://github.com/BepInEx/BepInEx/releases
2. Extráelo en `<Valheim>`, de modo que `winhttp.dll` y la carpeta `BepInEx\` queden en la **raíz** del juego.
3. Ejecuta Valheim **una vez** y ciérralo. BepInEx crea `BepInEx\plugins\`, `BepInEx\config\` y
   `BepInEx\LogOutput.log`.

Documentación oficial: https://docs.bepinex.dev/

---

## 3. Descarga la release del mod

Ve a **https://github.com/julianu37/AI-Companions/releases** y descarga **uno** de los dos archivos:

| Archivo | Tamaño | Cuándo elegirlo |
|---|---|---|
| `AI_Companions-1.9.0.zip` | ~368 MB | **Recomendado.** Ya viene con los modelos de voz (STT + TTS). Instala y juega |
| `AI_Companions-1.9.0-sem-modelos.zip` | ~28,5 MB | Internet lenta, o ya tienes los modelos y quieres bajarlos aparte |

> La diferencia es **solo** los modelos de voz. El mod y las traducciones son los mismos en los dos.
>
> *(La **1.9.0** es la release de los **cinco idiomas** — portugués, inglés, español, francés e italiano —, con la voz
> y el reconocimiento del habla **automáticos**, siguiendo el **idioma del juego**. La **1.8.3** es donde el inglés
> se volvió el **idioma nativo**: el `.cfg` pasó a ser `AiCompanions.cfg` y la sección de la IA pasó a `[AI]`, con
> las claves en inglés. La **1.8.2** puso un **selector de proveedor de LLM** y el botón de la **clave de API** en el
> panel `I`. La **1.8.0** trajo la corrección del peso — el **Megingjord** +150 pasa a sumar al tope del compañero —
> y la **1.8.1** cambia el LLM por defecto a **Groq** (gratis y rápido), manteniendo el LM Studio como opción local y
> el OpenRouter como alternativa. La **1.7.0** fue la release que renombró el mod a **AI Companions**.)*

---

## 4. Extrae en la carpeta de Valheim

El zip ya viene con la estructura de carpetas lista: dentro de él la carpeta es `BepInEx\plugins\AI-Companions\`.
Así que solo tienes que extraerlo **en la raíz de Valheim** (`<Valheim>`) y cada archivo cae en su sitio. Si usas un
gestor de mods, apunta el zip a él — el gestor lo instala directamente, sin renombrar nada.

> Si prefieres hacerlo a mano, el resultado final tiene que ser
> `<Valheim>\BepInEx\plugins\AI-Companions\`, con el `Companions.dll` **dentro** de ella. BepInEx carga plugins de
> **cualquier** subcarpeta de `BepInEx\plugins\` — mantener ese nombre solo evita confusión al actualizar.

> **¿Actualizas desde una versión anterior?** Borra la carpeta antigua `BepInEx\plugins\ModAI-BR-Companions\`
> **antes** de extraer. El mod cambió de nombre (era *ModAI-BR-Companions*), pero el **GUID** y las claves `HC_*` son
> los mismos: la **partida**, los compañeros, la casa y el inventario siguen valiendo. Dos carpetas con el mismo
> plugin = BepInEx carga el mod dos veces.

Al final, la carpeta tiene que quedar así:

![La carpeta del mod en el Explorador, con los archivos y las subcarpetas](images/pasta-do-mod.png)

```
<Valheim>\
└── BepInEx\
    └── plugins\
        └── AI-Companions\
            ├── LICENSES\
            ├── runtimes\
            ├── Speech\                     ← las frases del compañero (5 idiomas)
            ├── Translations\               ← los textos de la interfaz (5 idiomas)
            ├── tts\                        ← el modelo del TTS (Kokoro)
            ├── whisper\                    ← los modelos del STT (Whisper)
            ├── Companions.dll              ← el mod
            ├── Microsoft.Bcl.AsyncInterfaces.dll
            ├── sherpa-onnx.dll
            ├── System.Buffers.dll
            ├── System.Memory.dll
            ├── System.Numerics.Vectors.dll
            ├── System.Runtime.CompilerServices.Unsafe.dll
            ├── System.Threading.Tasks.Extensions.dll
            └── Whisper.net.dll
```

> El zip también trae el `README.md`, el `CHANGELOG.md` y el `icon.png` en la **raíz** — es lo que el gestor de mods
> muestra en la ficha del mod.

> Esas DLL de `System.*` y las dos de voz (`Whisper.net.dll`, `sherpa-onnx.dll`) tienen que quedarse **junto al
> `Companions.dll`** — son las dependencias que el mod usa para el STT y el TTS.

---

## 5. Modelos de voz

**Si descargaste el paquete completo, este paso está hecho** — los modelos ya vinieron en las carpetas `whisper\` y
`tts\`.

Si descargaste el **sin modelos**, pon los archivos así:

| Dónde ponerlos | Archivos |
|---|---|
| `...\AI-Companions\whisper\` | `ggml-small-q5_1.bin` (181 MB) — el modelo por defecto del reconocimiento del habla |
| `...\AI-Companions\tts\kokoro\` | `model.int8.onnx` (109 MB), `voices.bin` (27 MB), `tokens.txt` y la carpeta `espeak-ng-data\` |

> En el paquete **sin modelos** el `tokens.txt` y la carpeta `espeak-ng-data\` **ya vienen** — solo faltan el
> `ggml-small-q5_1.bin` (Whisper) y el `model.int8.onnx` + `voices.bin` (Kokoro).

Dónde descargarlos:

- **Whisper (ggml):** https://huggingface.co/ggerganov/whisper.cpp/tree/main — toma el `ggml-small-q5_1.bin`.
  El `ggml-base-q5_1.bin` (57 MB) es más rápido y menos preciso; mira **[Voz](Voice-es)**.
- **Kokoro (sherpa-onnx):** las releases de https://github.com/k2-fsa/sherpa-onnx — el paquete del modelo
  `kokoro-int8-multi-lang` (que trae el `model.int8.onnx`, el `voices.bin`, el `tokens.txt` y el `espeak-ng-data`).

> La calidad, la velocidad y las alternativas están detalladas en **[Voz](Voice-es)**.

---

## 6. Configura el LLM (Groq)

Las órdenes las entiende un **LLM**. El mod ya viene apuntado a **Groq**, que es el camino más simple: no hay nada
que instalar — solo la **clave gratis** e internet.

1. Crea la cuenta y la clave en **https://console.groq.com/keys**.
2. En `<Valheim>\BepInEx\config\AiCompanions.cfg`, sección `[AI]`, revisa los tres valores
   (los dos primeros **ya son los valores por defecto**; solo la clave la rellenas tú):

   ```ini
   BaseUrl = https://api.groq.com/openai/v1
   Model = openai/gpt-oss-20b
   ApiKey = <tu clave>
   ```

3. En el juego, pulsa **`I`** → pestaña **IA** → **Ajustes del LLM** → **Probar conexión** (también puedes pegar la
   clave y cambiar la URL/modelo ahí mismo, sin abrir el archivo).

> **Sin editar el archivo:** encima de la **URL del servidor** está el desplegable **"Elige tu proveedor LLM"** —
> elegir **Groq**, **LM Studio** u **OpenRouter** **rellena la URL y el modelo** (la clave no se toca; vale después
> de **Aplicar**). Y, en la línea de la **Clave de API**, el botón **"Crea tu clave de API gratis AQUÍ"** abre la
> página de la clave en el navegador (`console.groq.com/keys` en Groq). La velocidad del habla, que salió de este
> panel, se queda en el **`F8`**.

![El panel de IA en la pestaña IA: en Ajustes del LLM, el desplegable "Elige tu proveedor LLM" ya en Groq, la URL del servidor con api.groq.com, el modelo openai/gpt-oss-20b, la clave de API enmascarada y el botón "Crea tu clave de API gratis AQUÍ" a la derecha de la clave](images/en-ai-panel.jpg)

> Esta captura es de la **1.8.2**: el **selector de proveedor** está en **Groq** (el valor por defecto), la URL y el
> modelo vienen de Groq y el botón de la clave aparece a la derecha de la **Clave de API**. Si eliges **LM Studio**,
> el botón desaparece (no hay clave).

> **Con Groq, el texto de las órdenes sale de tu máquina** (es un servicio remoto) y el tier gratuito tiene un límite
> de peticiones y de **tokens por minuto** (~4 órdenes por minuto con el `gpt-oss-20b`). Para correr **todo local** —
> y que **nada** salga — usa **LM Studio** como **alternativa opcional**: instálalo desde https://lmstudio.ai,
> descarga el `qwen2.5-3b-instruct` (Q4_K_M), enciende el **Local Server** (puerto **1234**) y cambia en el `.cfg`:
> `BaseUrl = http://127.0.0.1:1234/v1` y `Model = qwen2.5-3b-instruct` (con la `ApiKey` **vacía**).

La guía completa de los tres caminos (Groq → LM Studio local → OpenRouter) está en
**[LLM (Groq, LM Studio u OpenRouter)](LLM-es)**.

---

## 7. Primera ejecución y el log

1. Ejecuta Valheim.
2. Abre `<Valheim>\BepInEx\LogOutput.log`.
3. Busca estas dos líneas:

```
AI Companions v1.9.0 loading...
AI Companions loaded successfully! (N methods patched)
```

Si aparecen, el mod cargó.

> El `v1.9.0` es la **versión que instalaste** (el número del zip que descargaste) y la `N` es cuántos métodos del
> juego ajustó el mod — ese número puede cambiar de una versión a otra.

> En la **primera ejecución después de actualizar de la 1.8.2 o anterior** el log también muestra que el archivo de
> configuración cambió de nombre: `[Config] Maddhattr.OfflineCompanionsReborn.cfg was replaced by AiCompanions.cfg ...`
> — es el aviso normal de la 1.8.3, y el `.cfg` antiguo puede borrarse.

> **Es normal** ver un error de **PlayFab / NullReferenceException** al arrancar — **no** es del mod y no bloquea
> nada. Si quieres confirmarlo, mira **[Problemas comunes](Troubleshooting-es)**.

---

## 8. Primera prueba en el juego

Con el mundo abierto y el compañero al lado:

1. **Por texto:** abre el chat del juego y escribe `recoge 10 de madera`.
   Debería responder e irse a recoger. *(El número es un objetivo contado en su inventario — si ya tiene 10 de
   madera, avisa "Ya tenemos bastante.")*
2. **Por voz:** mantén **`V`**, di `recoge 10 piedras` y suelta. El texto reconocido aparece en el chat.
3. **Su voz:** debería **decir** la respuesta en voz alta, en su posición en el mundo.
4. **Panel de IA:** pulsa **`I`** para ajustar personalidad, voz, volumen y la tecla del push-to-talk.

Para ver todas las teclas y órdenes, ve a **[Comandos](Commands-es)**.

---

## Lista de comprobación

- [ ] Valheim 1.0.x instalado
- [ ] BepInEx 5.4.2351 extraído en la raíz del juego
- [ ] Release **1.9.0** descargada (el paquete completo o el sin modelos)
- [ ] Carpeta `BepInEx\plugins\AI-Companions\` creada con el `Companions.dll` y las dependencias
- [ ] Modelos de voz en `whisper\` y `tts\kokoro\`
- [ ] Configuración **revisada** en `BepInEx\config\AiCompanions.cfg` (solo vuelve a los valores por defecto si vienes
      de la **1.8.2 o anterior**) y el `.cfg` antiguo borrado
- [ ] Clave gratis de Groq creada y `[AI] ApiKey` rellenada (o el LM Studio local configurado)
- [ ] `[AI] BaseUrl` y `[AI] Model` revisados en el `.cfg`
- [ ] **Probar conexión** (panel `I`) respondiendo `Conexión OK`
- [ ] Log con `AI Companions loaded successfully!`
- [ ] `recoge 10 de madera` en el chat funciona
- [ ] Mantener `V` y hablar funciona

Si algo falla, mira **[Problemas comunes](Troubleshooting-es)**.

<!-- D4: add Español/Français/Italiano -->
