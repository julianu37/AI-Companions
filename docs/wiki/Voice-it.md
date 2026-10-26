> 🌍 [English](Voice) · [Português](Voice-pt) · [Español](Voice-es) · [Français](Voice-fr) · **Italiano**

# Voce

La mod ha **due** parti vocali, e sono indipendenti:

- **STT** (*speech to text*) — **tu** parli e la mod capisce: tieni premuto **`V`**, parla e rilascia.
- **TTS** (*text to speech*) — il **compagno** parla: la sua risposta esce ad alta voce, nella sua posizione nel mondo,
  nella **lingua del gioco**.

Entrambe girano **dentro il gioco**, sulla tua macchina, senza servizio esterno — e **entrambe seguono la lingua del
gioco**: le cinque lingue sono portoghese del Brasile, inglese, spagnolo, francese e italiano, e `[AI] VoiceLanguage`,
`TtsLanguage` e `TtsVoice` sono **automatici** (`auto`, `auto`, `0`). **Cambiare la lingua del gioco non richiede un
riavvio** — il modello viene caricato una volta e la frase successiva esce già nella lingua nuova.

---

## Tu parli: il STT (Whisper)

### Come funziona

1. **Tieni premuto `V`** e parli.
2. La mod registra il microfono a **16 kHz mono** e taglia il silenzio all'inizio e alla fine.
3. Al rilascio, l'audio viene trascritto **dentro il gioco** da **Whisper.net** (whisper.cpp in-process, sulla CPU) —
   niente HTTP, niente processo figlio, niente internet.
4. Il testo riconosciuto entra nello **stesso** percorso degli ordini scritti: ordine, conversazione, personalità e
   memoria funzionano uguale. Vedi `Ho capito: "…"` nella chat.

> Il tasto **`V`** registra solo con il gioco in primo piano e **senza** nessuna finestra aperta (inventario, mappa,
> menu, chat, console o i pannelli della mod).

### Modelli

| File | Dimensione | Nota |
|---|---|---|
| `whisper\ggml-small-q5_1.bin` | 181 MB | **Predefinito.** Molto più preciso del `base`, in qualsiasi lingua |
| `whisper\ggml-base-q5_1.bin` | 57 MB | Più veloce e meno preciso — **non è nel pacchetto**: scaricalo e cambialo in `[AI] SttModel` |

Dove scaricarlo: https://huggingface.co/ggerganov/whisper.cpp/tree/main

Misura del progetto (CPU a 16 core, 8 thread, clip di 2 s): **~638 ms** con `base` contro **~2187 ms** con `small`. Il
tempo cambia appena con la dimensione del clip, perché l'encoder lavora sempre su una finestra di 30 s.

### Impostazioni del STT

| Chiave `[AI]` | Predefinito | Cosa fa |
|---|---|---|
| `VoiceEnabled` | `true` | Attiva il push-to-talk. Spento = il tasto non fa niente (e torna a fare quello che faceva il gioco con lui) |
| `VoiceKey` | `V` | Tasto del push-to-talk |
| `VoiceLanguage` | `auto` | Lingua del parlato (codice Whisper). `auto` segue la **lingua del gioco** (PT-BR, EN, ES, FR, IT) — il modello è multilingue e può anche rilevarla. Un codice esplicito vince sempre |
| `VoiceMaxSeconds` | `10` | Tetto della registrazione, in secondi |
| `VoiceSilenceThreshold` | `0.01` | Quanto deve essere forte l'audio per contare come parlato, quando taglia il silenzio. Alzalo se il tuo microfono capta molto rumore |
| `VoiceInitialPrompt` | *(vuoto)* | Vuoto = il **vocabolario degli ordini della lingua del gioco**, scritto dalla mod; orienta il riconoscimento verso le parole degli ordini. Metti le parole che dici **tu** (`none` spegne l'orientamento) |
| `VoiceDevice` | `0` | Quale microfono usare (indice della lista che il log stampa) |
| `VoiceShowText` | `true` | Mostra nella chat/HUD quello che ha sentito il microfono |
| `SttModel` | `whisper\ggml-small-q5_1.bin` | Modello Whisper (relativo alla cartella del plugin o percorso assoluto) |

> **Microfoni:** all'avvio la mod scrive nel log la lista dei microfoni disponibili — iniziando con
> `[STT] Push-to-talk:` e poi il numero di dispositivi con il loro indice e nome. Usa quel numero in `VoiceDevice`.

### Il tasto `V` e la raccolta automatica del gioco

In Valheim, **`V`** è il tasto del *Toggle Auto-Pickup* per impostazione predefinita. Per non rubare il tasto al
gioco, la mod ha una patch che **blocca la raccolta automatica** — ma **solo** quando entrambe le condizioni sono vere
insieme:

- `[AI] VoiceEnabled` è **attivo**, **e**
- il binding della raccolta automatica del gioco punta allo **stesso** tasto (`VoiceKey`).

Se **riassegni** la raccolta automatica nella schermata dei comandi di Valheim, la mod **non blocca niente** — il gioco
resta intatto e il tasto `V` resta libero per parlare. Puoi anche semplicemente cambiare `VoiceKey`.

---

## Lui parla: il TTS (Kokoro)

### Come funziona

- La voce viene sintetizzata **dentro il gioco** da **Kokoro-82M** (tramite `sherpa-onnx`, sulla CPU) — nessun servizio
  esterno, nessun processo figlio, niente esce dalla tua macchina.
- Il parlato esce **nella posizione del compagno** nel mondo (suono 3D), non al centro dello schermo.
- La sintesi gira su un **thread di fondo** e il parlato viene montato **frase per frase**: la prima suona già mentre
  la successiva viene generata. Le frasi ripetute escono dalla **cache**, all'istante.
- Il modello viene caricato **una volta**, in background, quando entri nel mondo.

### Voci per lingua

`[AI] TtsVoice = 0` (il predefinito) è **automatico**: la voce predefinita della lingua del gioco. Ogni lingua ha la
sua lista (quella predefinita per prima), e un id esplicito **di quella lingua** vince.

| Lingua | `TtsLanguage` | Id predefinito | Voci (quella predefinita per prima) |
|---|---|---|---|
| Portoghese (Brasile) | `pt-br` | **42** | `pf_dora` (F), `pm_alex` (M), `pm_santa` (M) |
| Inglese | `en-us` | **3** | `af_heart` (F), `af_bella` (F), `af_nicole` (F), `am_fenrir` (M), `am_michael` (M), `am_puck` (M) |
| Spagnolo | `es` | **28** | `ef_dora` (F), `em_alex` (M) |
| Francese | `fr` | **30** | `ff_siwis` (F) |
| Italiano | `it` | **35** | `if_sara` (F), `im_nicola` (M) |

> **Una voce di un'altra lingua non viene usata** (suonerebbe con l'accento sbagliato): il compagno ricade nella voce
> predefinita della lingua attiva, e il pannello `I` dice quale è in uso. La scelta resta salvata nel compagno, quindi
> riportare il gioco a quella lingua la ripristina.
>
> **Le lingue che il modello Kokoro non parla** — tedesco, russo, polacco, turco, coreano, svedese e ucraino — usano
> la **voce inglese**: un compagno che parla con voce inglese è meglio di uno muto.

### Cosa dice

La regola è: **la voce segue quello che è scritto sullo schermo.**

| Chiave `[AI]` | Predefinito | Cosa fa |
|---|---|---|
| `TtsEnabled` | `true` | **Lui dice** le risposte. Spento = solo il fumetto e la chat |
| `TtsVoice` | `0` | Quale voce: `0` = **automatica** (la voce predefinita della lingua del gioco); un id esplicito di quella lingua vince |
| `TtsVolume` | `1` | Volume della voce (0 = muto, 1 = volume normale) |
| `TtsSpeed` | `1` | Velocità (sopra 1 parla più veloce e più corto) |
| `TtsThreads` | `4` | Core della CPU per la sintesi (4 è già più veloce del tempo reale) |
| `TtsModel` | `tts\kokoro\model.int8.onnx` | Modello Kokoro (relativo alla cartella del plugin o percorso assoluto) |
| `TtsLanguage` | `auto` | Lingua del testo parlato. `auto` segue la lingua del gioco (`pt-br`, `en-us`, `es`, `fr`, `it`) e un codice esplicito vince — **non lasciarla mai vuota**, perché Kokoro richiede una lingua (`pt` da solo è portoghese del Portogallo) |
| `TtsCache` | `true` | Conserva le frasi già sintetizzate (le ripetute escono all'istante) |
| `TtsSpeakReplies` | `true` | Dice la **risposta del modello** (a modo suo: *"Lascia fare a me."*) |
| `TtsSpeakModLines` | `true` | Dice la **frase della mod** che appare nella chat (*"Ok: raccogliere legna (10)."*) |
| `TtsReverbZoneMix` | `0` | Quanto delle zone di riverbero del mondo entra nella voce. `0` = voce asciutta |

- In un **ordine**, vince la frase della mod (la voce segue quello che è scritto sullo schermo). Spegni
  `TtsSpeakModLines` per sentire al suo posto la risposta del modello.
- In una **conversazione**, la risposta del modello **è** il parlato.
- **Niente viene detto due volte:** con le due opzioni accese, la frase che non viene detta va solo nel log.

### Modelli

| Dove | File |
|---|---|
| `tts\kokoro\` | `model.int8.onnx` (109 MB), `voices.bin` (27 MB), `tokens.txt` e la cartella `espeak-ng-data\` |

Le voci, i token e l'`espeak-ng-data` vengono cercati nella **stessa cartella** del modello.

Dove scaricarlo: le release di https://github.com/k2-fsa/sherpa-onnx (pacchetto del modello
**`kokoro-int8-multi-lang`**).

### L'eco della voce (e la correzione)

Se senti il parlato del compagno **che riecheggia come in una caverna**, non è un bug della mod: Valheim ha **una zona
di riverbero globale** — una `AudioReverbZone` con raggio di **1000 m** sull'oggetto stesso dell'`AudioListener`, con
**4 s** di coda. Tutto quello che stava entro 1 km prendeva quella coda, e la nostra sorgente audio era sul valore
predefinito di Unity.

La correzione è la chiave **`[AI] TtsReverbZoneMix`**, che arriva già a **`0`** (voce asciutta, come la musica del
gioco). Se vuoi uno "spazio" sottile nella voce, usa **`0.19`** — lo stesso valore che usa l'ambiente del gioco. Vale
dalla **frase successiva**, senza riavviare.

---

## Provare dalla console del gioco

Apri la console con **`F5`**:

| Comando | Cosa fa |
|---|---|
| `modia_tts_teste` | Dice una frase di prova con il TTS (pipeline completo) |
| `modia_tts_teste <testo>` | Dice il testo che scrivi — per esempio `modia_tts_teste Buongiorno, capo!` |
| `modia_stt_teste` | Esegue il riconoscimento Whisper su un WAV, **senza** usare il microfono |

> `modia_stt_teste` trascrive il file impostato in `[AI] SttWavTest` (predefinito `whisper\jfk.wav`) e scrive il
> risultato nel log. È il test giusto per sapere se il modello Whisper si sta caricando — utile quando gli ordini
> vocali non escono.
>
> **Il pacchetto non porta quel WAV** (è uscito dal pacchetto per risparmiare spazio): metti un file tuo in
> `...\AI-Companions\whisper\` e punta `[AI] SttWavTest` su di lui (percorso relativo alla cartella del plugin o
> assoluto). Il comando **non** prende il percorso come argomento — solo la chiave. Un parlato breve nella tua lingua,
> WAV PCM 16 bit, è quello che si aspetta.

Il log mostra il tempo di sintesi di ogni frase e il **RTF** (frazione del tempo reale: sotto 1 è più veloce del tempo
reale).

---

## Limitazione nota: volume del gioco

La voce del compagno **non passa dal mixer del gioco**. Questo vuol dire che **ignora gli slider del volume di
Valheim** e obbedisce **solo** a `[AI] TtsVolume`. Se la voce è troppo alta o troppo bassa rispetto al gioco, regolala
con quella chiave (o con lo slider del volume nel pannello **`I`**).

<!-- D4: add Español/Français/Italiano -->
