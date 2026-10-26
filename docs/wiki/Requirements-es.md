> 🌍 [English](Requirements) · [Português](Requirements-pt) · **Español** · [Français](Requirements-fr) · [Italiano](Requirements-it)

# Requisitos

Lo que necesitas tener antes de instalar **AI Companions**.

El mod funciona en **dos capas independientes**: las **órdenes por texto en el chat** (necesitan un **LLM**) y las
**órdenes por voz** (necesitan un micrófono). Puedes usar **solo el chat**, escribiendo — la voz es opcional.

## Obligatorio

| Elemento | Versión / detalle |
|---|---|
| **Valheim** | **1.0.x** (motor Unity 6 — probado en la **1.0.20**) |
| **BepInEx** | **BepInExPack_Valheim 5.4.2351** (BepInEx 5.4.23.5) — el mod es un plugin de BepInEx 5 |
| **Un LLM** | Para que se entiendan las órdenes de texto/voz. Por defecto es **Groq**: solo necesita **internet** y una **clave gratis** (mira abajo). La alternativa **local** (**LM Studio** u **Ollama**) es **opcional** y no manda nada fuera; **OpenRouter** sigue funcionando, pero su tier gratuito es más lento. Mira **[LLM (Groq, LM Studio u OpenRouter)](LLM-es)** |
| **Espacio en disco** | Mira la tabla de abajo |

> **El LLM es obligatorio; el camino no.** **Groq** es el valor por defecto: basta la **clave gratis**
> (https://console.groq.com/keys) e internet — y el texto de las órdenes **sale de tu máquina**. **LM Studio/Ollama**
> es la **alternativa local, opcional**: nada sale, pero descargas un modelo (~1,9 GB) y dejas el servidor encendido
> mientras juegas. **OpenRouter** también funciona (clave gratis en https://openrouter.ai/keys) y queda como
> **alternativa**, porque su tier gratuito es más lento. Los tres caminos — en ese orden de preferencia — están en
> **[LLM (Groq, LM Studio u OpenRouter)](LLM-es)**.

## Opcional

| Elemento | Para qué |
|---|---|
| **Micrófono** | Solo para las **órdenes por voz** (push-to-talk). Sin él, usa el chat |
| **GPU** | Solo marca la diferencia en la opción **local**: el LLM en LM Studio corre **mucho** más rápido con una GPU. En Groq (y en OpenRouter), el modelo corre en su servidor |
| **Modelos de voz** | Ya vienen en el paquete **completo**. En el paquete **sin modelos** los descargas aparte. El mismo `voices.bin` del Kokoro trae las voces de los **cinco idiomas** |

## Espacio en disco

| Qué | Tamaño |
|---|---|
| Paquete **completo** (mod + modelos) | ~368 MB (zip) |
| Paquete **sin modelos** (mod + traducciones) | ~28,4 MB (zip) |
| Mod instalado (carpeta del plugin) | ~428 MB |
| Modelo del Whisper `ggml-small-q5_1.bin` (por defecto del STT) | 181 MB |
| Modelo del Whisper `ggml-base-q5_1.bin` (más rápido, menos preciso) | 57 MB |
| Modelo del Kokoro (TTS) `model.int8.onnx` | 109 MB |
| Voces del Kokoro `voices.bin` | 27 MB (las voces de los cinco idiomas: PT-BR, EN, ES, FR e IT) |
| Modelo del LLM **local** (`qwen2.5-3b-instruct` Q4_K_M, LM Studio) — solo en la opción local | ~1,93 GB |

> Reserva algo en torno a **1 GB** para el mod y los modelos de voz, más el espacio del modelo del LLM.

## Hardware

- **CPU:** el TTS (Kokoro-82M) sintetiza en la **CPU**. Con **4 núcleos** ya genera más rápido que el tiempo real (el
  habla sale delante del compañero). El STT (Whisper) también corre en la CPU.
- **GPU:** solo importa en la opción **local** — en LM Studio una GPU marca una diferencia enorme. Un modelo 3B corre
  bien en CPU, pero es mucho más rápido con GPU. En Groq (y en OpenRouter), el modelo lo corre su servidor.
- **RAM:** en la opción local, un modelo 3B cuantizado (Q4) ocupa unos **2 GB** de memoria, además del juego. Con Groq
  (o OpenRouter) no hay ese consumo.

## Cómo instalar el BepInEx

El BepInEx es el cargador de plugins de Valheim. Instálalo **antes** del mod:

1. Descarga el **BepInExPack_Valheim 5.4.2351** de la página de releases del BepInEx:
   https://github.com/BepInEx/BepInEx/releases
2. Extrae el contenido en la carpeta del juego, de forma que `winhttp.dll` y la carpeta `BepInEx\` queden **en la
   raíz** de Valheim (`C:\Program Files (x86)\Steam\steamapps\common\Valheim` por defecto).
3. Ejecuta Valheim **una vez** y ciérralo — el BepInEx crea la estructura de carpetas
   (`BepInEx\plugins\`, `BepInEx\config\`, `BepInEx\LogOutput.log`).

> Si el BepInEx no carga, revisa la documentación oficial: https://docs.bepinex.dev/

Después de eso, sigue la **[Instalación](Installation-es)** del mod.

## Siguiente paso

➡️ **[Instalación](Installation-es)**

<!-- D4: add Español/Français/Italiano -->
