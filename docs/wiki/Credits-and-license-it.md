> 🌍 [English](Credits-and-license) · [Português](Credits-and-license-pt) · [Español](Credits-and-license-es) · [Français](Credits-and-license-fr) · **Italiano**

# Crediti e licenza

**AI Companions** è un **progetto indipendente** che esiste sopra il lavoro di altre persone. Questa pagina dice chi
ha fatto cosa.

---

## Upstream (la base della mod)

| Progetto | Autore | Licenza |
|---|---|---|
| **Offline Companions Reborn** | **MaddhattrRocks** | **MIT** |
| *Offline Companions* (l'originale) | **ProfMags** | MIT |

https://github.com/MaddhattrRocks/Offline-Companions-Reborn

**Viene da lì:** il compagno in sé — i lavori (raccolta, coltivazione, pesca, caccia, cucina, fusione, riparazione,
loot), il cibo e la stamina, le abilità, la casa e il riposo, i portali e il carro, l'IA di movimento e combattimento,
il menu radiale e tutta la UI di base. Lo strato di IA del nostro progetto è solo un **nuovo ingresso** per quelle
stesse azioni.

> Offline Companions Reborn, a sua volta, è un fork di *Offline Companions*, di **ProfMags**.

---

## Cosa è nostro

Quello che il progetto **AI Companions** aggiunge:

1. **La traduzione in cinque lingue** — portoghese del Brasile, inglese, spagnolo, francese e italiano: 737 chiavi di
   interfaccia e 186 frasi del compagno per lingua, verificate (nessun testo in inglese visibile a un giocatore di
   quelle lingue), con l'**inglese come lingua nativa**.
2. **Lo strato di IA:**
   - Ordini in **linguaggio naturale** via **testo nella chat** e via **voce** (push-to-talk + STT locale).
   - **Personalità** (preset o testo libero), **memoria** per compagno e **conversazione libera**.
   - **TTS locale** (Kokoro-82M tramite sherpa-onnx): il compagno **parla** nella sua posizione nel mondo.
   - Il **pannello IA** (tasto `I`).
3. **Regolazioni e correzioni:**
   - Raggio di raccolta più grande e chatter casuale spento di default.
   - Il tasto `V` non attiva più il *Toggle Auto-Pickup* del gioco (patch condizionale).
   - L'eco della voce (era la zona di riverbero globale del gioco).
   - L'ordine della pietra che non si fermava all'obiettivo.
   - I pannelli della mod sopra l'HUD del gioco (il clic inghiottito nell'`F8`).

---

## Terze parti

| Componente | Cos'è | Licenza / link |
|---|---|---|
| **Whisper.net** | Binding .NET per whisper.cpp, usato nel STT | MIT — https://github.com/sandrohanea/whisper.net |
| **whisper.cpp** | Implementazione di Whisper in C/C++ | MIT — https://github.com/ggml-org/whisper.cpp |
| **Modelli ggml di Whisper** | I file `ggml-*.bin` | MIT — https://huggingface.co/ggerganov/whisper.cpp |
| **sherpa-onnx** | Runtime TTS/STT (ONNX) usato nel TTS | Apache-2.0 — https://github.com/k2-fsa/sherpa-onnx |
| **Kokoro-82M** | Il modello di voce che usa il compagno | Apache-2.0 — https://huggingface.co/hexgrad/Kokoro-82M |
| **onnxruntime** | Il runtime che esegue i modelli ONNX (arriva con sherpa-onnx) | MIT — https://github.com/microsoft/onnxruntime |
| **espeak-ng** | Fonemizzazione usata da Kokoro (arriva come **dati**, in `espeak-ng-data\`) | GPL-3.0, solo i dati — https://github.com/espeak-ng/espeak-ng |
| **Groq** | Il servizio che serve il LLM gratuito di default (niente di suo viene ridistribuito) | https://console.groq.com — uso soggetto ai termini del servizio |
| **OpenRouter** | Il servizio di LLM remoto tenuto come alternativa (niente di suo viene ridistribuito) | https://openrouter.ai — uso soggetto ai termini del servizio |
| **LM Studio** | L'applicazione che serve il LLM locale | https://lmstudio.ai |
| **Qwen2.5** | La famiglia del modello consigliato per la modalità locale (`qwen2.5-3b-instruct`) | Apache-2.0 — https://huggingface.co/Qwen |
| **gpt-oss** | Il modello aperto servito da Groq di default (`openai/gpt-oss-20b`) | Apache-2.0 — https://huggingface.co/openai/gpt-oss-20b |

> I testi completi delle licenze di terze parti viaggiano **dentro il pacchetto**, nella cartella `LICENSES\`.
>
> **Riferimento concettuale:** **Thrall** (ego.ai) è stato studiato come riferimento di idee (*all rights reserved*).
> **Nessun** codice o binario suo viene ridistribuito qui.

---

## Licenza del progetto

**MIT.** Puoi usarlo, modificarlo e ridistribuirlo, mantenendo gli avvisi di copyright.

I file completi sono nel repository:

- [`LICENSE`](https://github.com/julianu37/AI-Companions/blob/main/LICENSE)
- [`THIRD_PARTY_NOTICES.md`](https://github.com/julianu37/AI-Companions/blob/main/THIRD_PARTY_NOTICES.md)
- [`NOTICE.md`](https://github.com/julianu37/AI-Companions/blob/main/NOTICE.md)

> La cartella `LICENSES\` che arriva con la mod installata porta i testi delle licenze dei componenti di terze parti
> distribuiti con essa.
