> 🌍 [English](Credits-and-license) · [Português](Credits-and-license-pt) · **Español** · [Français](Credits-and-license-fr) · [Italiano](Credits-and-license-it)

# Créditos y licencia

**AI Companions** es un **proyecto independiente** que existe encima del trabajo de otras personas. Esta página dice
quién hizo qué.

---

## Upstream (la base del mod)

| Proyecto | Autor | Licencia |
|---|---|---|
| **Offline Companions Reborn** | **MaddhattrRocks** | **MIT** |
| *Offline Companions* (el original) | **ProfMags** | MIT |

https://github.com/MaddhattrRocks/Offline-Companions-Reborn

**Viene de ahí:** el compañero en sí — los trabajos (recogida, cultivo, pesca, caza, cocina, fundición, reparación,
loot), la comida y la stamina, las habilidades, la casa y el descanso, los portales y el carro, la IA de movimiento y
combate, el menú radial y toda la UI base. La capa de IA de nuestro proyecto es solo una **nueva entrada** para esas
mismas acciones.

> Offline Companions Reborn, a su vez, es un fork de *Offline Companions*, de **ProfMags**.

---

## Qué es nuestro

Lo que el proyecto **AI Companions** añade:

1. **La traducción en cinco idiomas** — portugués de Brasil, inglés, español, francés e italiano: 737 claves de
   interfaz y 186 frases del compañero por idioma, auditadas (sin texto en inglés visible para un jugador de esos
   idiomas), con el **inglés como idioma nativo**.
2. **La capa de IA:**
   - Órdenes en **lenguaje natural** por **texto en el chat** y por **voz** (push-to-talk + STT local).
   - **Personalidad** (presets o texto libre), **memoria** por compañero y **conversación libre**.
   - **TTS local** (Kokoro-82M vía sherpa-onnx): el compañero **habla** en su posición en el mundo.
   - El **panel de IA** (tecla `I`).
3. **Ajustes y correcciones:**
   - Radio de recogida mayor y chatter aleatorio apagado por defecto.
   - La tecla `V` ya no dispara el *Toggle Auto-Pickup* del juego (parche condicional).
   - El eco de la voz (era la zona de reverb global del juego).
   - La orden de piedra que no paraba en el objetivo.
   - Los paneles del mod por encima del HUD del juego (el clic tragado en el `F8`).

---

## Terceros

| Componente | Qué es | Licencia / enlace |
|---|---|---|
| **Whisper.net** | Binding .NET para whisper.cpp, usado en el STT | MIT — https://github.com/sandrohanea/whisper.net |
| **whisper.cpp** | Implementación de Whisper en C/C++ | MIT — https://github.com/ggml-org/whisper.cpp |
| **Modelos ggml de Whisper** | Los archivos `ggml-*.bin` | MIT — https://huggingface.co/ggerganov/whisper.cpp |
| **sherpa-onnx** | Runtime de TTS/STT (ONNX) usado en el TTS | Apache-2.0 — https://github.com/k2-fsa/sherpa-onnx |
| **Kokoro-82M** | El modelo de voz que usa el compañero | Apache-2.0 — https://huggingface.co/hexgrad/Kokoro-82M |
| **onnxruntime** | El runtime que ejecuta los modelos ONNX (viene con sherpa-onnx) | MIT — https://github.com/microsoft/onnxruntime |
| **espeak-ng** | Fonemización usada por Kokoro (viene como **datos**, en `espeak-ng-data\`) | GPL-3.0, solo los datos — https://github.com/espeak-ng/espeak-ng |
| **Groq** | El servicio que sirve el LLM gratuito por defecto (nada de él se redistribuye) | https://console.groq.com — uso sujeto a los términos del servicio |
| **OpenRouter** | El servicio de LLM remoto mantenido como alternativa (nada de él se redistribuye) | https://openrouter.ai — uso sujeto a los términos del servicio |
| **LM Studio** | La aplicación que sirve el LLM local | https://lmstudio.ai |
| **Qwen2.5** | La familia del modelo recomendado para el modo local (`qwen2.5-3b-instruct`) | Apache-2.0 — https://huggingface.co/Qwen |
| **gpt-oss** | El modelo abierto servido por Groq por defecto (`openai/gpt-oss-20b`) | Apache-2.0 — https://huggingface.co/openai/gpt-oss-20b |

> Los textos completos de las licencias de terceros viajan **dentro del paquete**, en la carpeta `LICENSES\`.
>
> **Referencia conceptual:** se estudió **Thrall** (ego.ai) como referencia de ideas (*all rights reserved*).
> **Ningún** código ni binario suyo se redistribuye aquí.

---

## Licencia del proyecto

**MIT.** Puedes usarlo, modificarlo y redistribuirlo, manteniendo los avisos de copyright.

Los archivos completos están en el repositorio:

- [`LICENSE`](https://github.com/julianu37/AI-Companions/blob/main/LICENSE)
- [`THIRD_PARTY_NOTICES.md`](https://github.com/julianu37/AI-Companions/blob/main/THIRD_PARTY_NOTICES.md)
- [`NOTICE.md`](https://github.com/julianu37/AI-Companions/blob/main/NOTICE.md)

> La carpeta `LICENSES\` que viene con el mod instalado trae los textos de las licencias de los componentes de
> terceros distribuidos con él.
