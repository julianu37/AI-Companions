> 🌍 [English](Requirements) · [Português](Requirements-pt) · [Español](Requirements-es) · [Français](Requirements-fr) · **Italiano**

# Requisiti

Cosa devi avere prima di installare **AI Companions**.

La mod funziona in **due strati indipendenti**: gli **ordini di testo nella chat** (hanno bisogno di un **LLM**) e gli
**ordini vocali** (hanno bisogno di un microfono). Puoi usare **solo la chat**, scrivendo — la voce è facoltativa.

## Obbligatorio

| Elemento | Versione / dettaglio |
|---|---|
| **Valheim** | **1.0.x** (motore Unity 6 — testato sulla **1.0.20**) |
| **BepInEx** | **BepInExPack_Valheim 5.4.2351** (BepInEx 5.4.23.5) — la mod è un plugin di BepInEx 5 |
| **Un LLM** | Per far capire gli ordini di testo/voce. Il percorso **consigliato** è il **LM Studio** locale: scarichi un modello (~1,9 GB) e tieni il server acceso, e niente esce dalla tua macchina. **Groq** è quello che arriva nel `.cfg`: servono solo **internet** e una **chiave gratuita** (vedi sotto), e il testo degli ordini **esce dalla tua macchina**. **OpenRouter** funziona ancora, ma il suo tier gratuito è più lento. Vedi **[LLM (LM Studio, Groq o OpenRouter)](LLM-it)** |
| **Spazio su disco** | Vedi la tabella sotto |

> **Il LLM è obbligatorio; il percorso no.** **LM Studio/Ollama** è il percorso **consigliato** ed è locale:
> niente esce, ma scarichi un modello (~1,9 GB) e tieni il server acceso mentre giochi. **Groq** è quello che
> arriva nel `.cfg`: basta la **chiave gratuita** (https://console.groq.com/keys) e internet — e il testo degli
> ordini **esce dalla tua macchina**. **OpenRouter** funziona anche (chiave gratuita su
> https://openrouter.ai/keys) e resta come **alternativa**, perché il suo tier gratuito è più lento. I tre
> percorsi — in quest'ordine di preferenza — sono in **[LLM (LM Studio, Groq o OpenRouter)](LLM-it)**.

## Facoltativo

| Elemento | Per cosa |
|---|---|
| **Microfono** | Solo per gli **ordini vocali** (push-to-talk). Senza, usa la chat |
| **GPU** | Fa differenza solo nell'opzione **locale**: il LLM in LM Studio gira **molto** più veloce con una GPU. Su Groq (e su OpenRouter) il modello gira sul loro server |
| **Modelli di voce** | Arrivano già nel pacchetto **completo**. Nel pacchetto **senza modelli** li scarichi a parte. Lo stesso `voices.bin` di Kokoro porta le voci delle **cinque lingue** |

## Spazio su disco

| Cosa | Dimensione |
|---|---|
| Pacchetto **completo** (mod + modelli) | ~314 MB (zip) |
| Pacchetto **senza modelli** (mod + traduzioni) | ~28,4 MB (zip) |
| Mod installata (cartella del plugin) | ~371 MB |
| Modello Whisper `ggml-small-q5_1.bin` (predefinito del STT) | 181 MB |
| Modello Kokoro (TTS) `model.int8.onnx` | 109 MB |
| Voci Kokoro `voices.bin` | 27 MB (le voci delle cinque lingue: PT-BR, EN, ES, FR e IT) |
| Modello del LLM **locale** (`qwen2.5-3b-instruct` Q4_K_M, LM Studio) — solo nell'opzione locale | ~1,93 GB |

> Riserva qualcosa intorno a **1 GB** per la mod e i modelli di voce, più lo spazio del modello del LLM.

## Hardware

- **CPU:** il TTS (Kokoro-82M) sintetizza sulla **CPU**. Con **4 core** genera già più veloce del tempo reale (il
  parlato esce prima del compagno). Anche il STT (Whisper) gira sulla CPU.
- **GPU:** conta solo nell'opzione **locale** — in LM Studio una GPU fa un'enorme differenza. Un modello 3B gira bene
  sulla CPU, ma è molto più veloce con una GPU. Su Groq (e su OpenRouter) il modello lo fa girare il loro server.
- **RAM:** nell'opzione locale un modello 3B quantizzato (Q4) occupa circa **2 GB** di memoria, oltre al gioco. Con
  Groq (o OpenRouter) non c'è questo consumo.

## Come installare BepInEx

BepInEx è il caricatore di plugin di Valheim. Installalo **prima** della mod:

1. Scarica **BepInExPack_Valheim 5.4.2351** dalla pagina delle release di BepInEx:
   https://github.com/BepInEx/BepInEx/releases
2. Estrai il contenuto nella cartella del gioco, in modo che `winhttp.dll` e la cartella `BepInEx\` finiscano **nella
   radice** di Valheim (`C:\Program Files (x86)\Steam\steamapps\common\Valheim` per impostazione predefinita).
3. Avvia Valheim **una volta** e chiudilo — BepInEx crea la struttura di cartelle
   (`BepInEx\plugins\`, `BepInEx\config\`, `BepInEx\LogOutput.log`).

> Se BepInEx non si carica, controlla la documentazione ufficiale: https://docs.bepinex.dev/

Dopo, segui l'**[Installazione](Installation-it)** della mod.

## Passo successivo

➡️ **[Installazione](Installation-it)**
