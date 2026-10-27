> 🌍 [English](Troubleshooting) · [Português](Troubleshooting-pt) · [Español](Troubleshooting-es) · [Français](Troubleshooting-fr) · **Italiano**

# Problemi comuni

Sintomo → causa probabile → cosa fare.

**Dove guardare nel log, sempre:** `<Valheim>\BepInEx\LogOutput.log`. Le righe della mod iniziano con `[IA]`, `[STT]` e
`[TTS]`. (`[IA]` è l'**etichetta del log** — la sezione del `.cfg` è **`[AI]`**, con le chiavi in inglese.)

---

## Non risponde a niente

| Dove guardare | Cosa cercare |
|---|---|
| `LogOutput.log` | `[IA] Chat orders ready: enabled=…, endpoint=…, model=…` |

**Cause probabili, in ordine:**

1. **La `ApiKey` è vuota o sbagliata.** La mod arriva puntata su **Groq**, che **richiede** la chiave (vedi "risponde
   con un errore di autenticazione" sotto). Se usi **LM Studio** locale, la chiave resta vuota e il `BaseUrl` deve
   essere `http://127.0.0.1:1234/v1`.
2. **Il `BaseUrl` o il `Model` sono sbagliati.** Su Groq sono `https://api.groq.com/openai/v1` e
   `openai/gpt-oss-20b`. Nel LM Studio locale, il `Model` deve essere l'**API Model Identifier** che mostra, non il
   nome della cartella. Vedi **[LLM (LM Studio, Groq o OpenRouter)](LLM-it)**.
3. **Con LM Studio locale: il server non è acceso.** Apri la scheda **Developer** e controlla lo stato `Running` sulla
   porta **1234**.
4. **`[AI] Enabled` è `false`.** Accendilo nel pannello **`F8`**.
5. **`[AI] Prefix` è compilato.** Se metti `/c`, solo i messaggi che iniziano con `/c` vanno al modello.
6. **Nessun compagno è vicino / in grado di ricevere l'ordine.** Deve essere evocato.

> Prima di tutto, usa **Prova la connessione** del pannello **`I`**: dice in pochi secondi se l'URL risponde e se il
> modello compare nella lista. Ricorda che la **chiave** viene validata solo su un ordine vero (il test conferma l'URL
> e la lista dei modelli, non l'autenticazione).

---

## Risponde con un errore di autenticazione (401 / "unauthorized")

| Dove guardare | Cosa cercare |
|---|---|
| `LogOutput.log` | `[IA]` con lo stato **401** (o un messaggio *unauthorized* / *invalid api key*) |
| Pannello **`I`** → **Prova la connessione** | Può rispondere `Connessione OK` anche con la chiave sbagliata — su OpenRouter la lista dei modelli è pubblica. Il 401 compare sull'**ordine**, non sul test |

**Causa:** **Groq richiede** la `[AI] ApiKey` (e anche OpenRouter), ed è **vuota**, **sbagliata** o **cancellata** (per
esempio, dopo aver copiato un `.cfg` da un'altra macchina, o dopo un aggiornamento **dalla 1.8.2 o precedente** — è la
1.8.3 che ha azzerato le impostazioni) — oppure è la chiave di un **altro** servizio, incollata insieme a un URL che non
è il suo.

**Cosa fare:**

1. Crea o copia la chiave su **https://console.groq.com/keys** (Groq) o su **https://openrouter.ai/keys** (OpenRouter).
2. Incollala in **`[AI] ApiKey`** — nel pannello **`I`** (scheda **IA** → **Impostazioni del LLM**) o direttamente nel
   `.cfg`.
3. Clicca **Prova la connessione**; poi **Applica** (la configurazione vale al messaggio successivo).
4. Se **non** vuoi usare Groq, punta il `BaseUrl` sul server locale (`http://127.0.0.1:1234/v1`), usa il `Model` di LM
   Studio e **lascia la `ApiKey` vuota**.

> Chiave trapelata? Cancellala su **https://console.groq.com/keys** (Groq) o su
> **https://openrouter.ai/settings/keys** (OpenRouter) e creane un'altra. La `ApiKey` del `.cfg` non va condivisa né
> messa in un repository.

---

## Risponde 429 (limite di richieste o di token)

| Dove guardare | Cosa cercare |
|---|---|
| `LogOutput.log` | `[IA]` con lo stato **429** (o un messaggio *rate limit* / *too many requests*) |

**Causa:** è stato raggiunto il **tier gratuito** del servizio. Su **Groq**, con `openai/gpt-oss-20b`, i limiti sono
**30 richieste al minuto**, **1.000 al giorno**, **8.000 token al minuto** e **200.000 al giorno** — e, dato che ogni
ordine spende ~1.800 token, il tetto pratico è **~4 ordini al minuto**. Su **OpenRouter** i modelli `:free` hanno
**20 richieste al minuto** e un tetto giornaliero che dipende dalla storia dell'account.

**Cosa fare:**

1. **Aspetta qualche secondo** e manda di nuovo l'ordine — il limite è per finestra di tempo, quindi torna da solo.
2. Abbassa la frequenza: `[AI] CooldownSeconds` (**3** di default) aiuta già; alzalo se mandi ordini a raffica.
3. Riduci `[AI] Memory` (ogni scambio ricordato ingrandisce il prompt e, con lui, i token di ogni ordine).
4. Per un uso intenso usa **LM Studio/Ollama locale** — non c'è limite di richieste né di token.

> Il **429** non rompe niente: l'ordine viene rifiutato con un avviso e il gioco continua. **Non** è un errore di
> chiave — quello è il **401**, sopra.

---

## Ci mette troppo a rispondere

| Dove guardare | Cosa cercare |
|---|---|
| `LogOutput.log` | `[IA] Tempo: … ms` — la mod avvisa quando supera **5 s** |

**Cause probabili:**

1. **Il `.cfg` è ancora su OpenRouter.** Il predefinito attuale è **Groq** (`openai/gpt-oss-20b`), che risponde in una
   **frazione di secondo** nella nostra misurazione. Se il tuo `[AI] BaseUrl` è ancora `https://openrouter.ai/api/v1`,
   sei sul suo router gratuito (`openrouter/free`), che **varia** da un ordine all'altro e passa dalla coda del
   provider. Passa a Groq (vedi **[LLM (LM Studio, Groq o OpenRouter)](LLM-it)**) o usa il LM Studio locale.
2. **Non confondere la lentezza con il limite di Groq.** Il **429** risponde **subito** (l'ordine viene rifiutato con
   un avviso), non lentamente — se il sintomo è il ritardo, la causa è un'altra. Vedi "risponde 429" sopra per il
   limite.
3. **Il modello è grande o gira sulla CPU (locale).** Un 3B (`qwen2.5-3b-instruct`) risponde in ~0,35–0,4 s;
   `google/gemma-4-e4b` impiega ~3 s. I modelli più grandi richiedono praticamente una GPU.
4. **`[AI] Memory` alto.** Ogni scambio ricordato ingrandisce il prompt che il modello deve leggere. Riducilo.
5. **Il primo messaggio della sessione** include il caricamento del modello — nel LM Studio locale è naturalmente più
   lento, e sui servizi remoti il primo di solito è anche il più freddo.

> Ricorda: la richiesta gira in **background**. Anche lenta, **non** blocca il gioco.

---

## Risponde male o ignora il numero

**Causa probabile:** il modello non ha capito la risorsa o la quantità.

**Cosa fare:**

- Controlla cosa ha capito il modello: `[AI] ShowReplyInChat` mostra la risposta grezza nella chat.
- Usa **`qwen2.5-3b-instruct`** con `[AI] StructuredOutput = true` — è la combinazione che il progetto ha misurato come
  affidabile (il 3B è passato da 0/6 a **6/6** risposte valide con l'uscita strutturata).
- Nel predefinito (**Groq**, `openai/gpt-oss-20b`) il modello è sempre lo stesso, quindi la risposta è coerente. Su
  **OpenRouter** (alternativa), `openrouter/free` cambia modello a ogni richiesta — anche la qualità varia. Se l'ordine
  sbaglia sempre nello stesso modo, scegli **un modello specifico** in `[AI] Model` (i gratuiti di OpenRouter hanno
  `:free` nel nome) o usa il LM Studio locale, che è sempre lo stesso modello.
- Ricorda che **il numero è un obiettivo contato nella sua sacca**: se ne ha già abbastanza, **non** va a raccogliere, e
  questo è il comportamento corretto.
- Per gli ordini vocali, un riconoscimento scarso arriva male al modello. Vedi "il microfono non registra" sotto.

---

## Risponde nella lingua sbagliata

**Causa probabile:** il gioco è in una lingua e il compagno risponde in un'altra, o il modello scelto è debole nella
tua lingua.

**Cosa fare:**

- **Controlla la lingua del gioco.** Dalla 1.9.0 il contratto inviato al modello è nella **lingua del gioco** —
  portoghese del Brasile, inglese, spagnolo, francese e italiano — e una lingua fuori da quelle cinque ricade
  sull'**inglese**. La voce e il riconoscimento vocale seguono la stessa regola automaticamente (`[AI] VoiceLanguage` /
  `TtsLanguage` / `TtsVoice` = `auto` / `auto` / `0`).
- Usa **`qwen2.5-3b-instruct`** (buono in portoghese) se sei su LM Studio; su **Groq** (predefinito) e su
  **OpenRouter** un modello più grande di solito scrive meglio dei piccoli.
- Regola la personalità nel pannello **`I`** (preset o testo libero nella tua lingua) — entra nel prompt e tira la
  risposta verso quella lingua. Anche i preset sono tradotti nelle cinque lingue.
- Riduci `[AI] Temperature` (lo `0.1` predefinito è già basso; valori alti fanno divagare il modello).

---

## La voce ha l'eco (sembra una caverna)

**Causa:** non è un bug — Valheim ha **una zona di riverbero globale** di 1000 m sull'oggetto stesso
dell'`AudioListener`, con 4 s di coda. La sorgente audio del compagno era sul predefinito di Unity e prendeva tutta
quella coda.

**Cosa fare:** conferma che `[AI] TtsReverbZoneMix` sia a **`0`** (il predefinito). Se vuoi uno "spazio" sottile nella
voce, usa `0.19`. Vale dalla **frase successiva**, senza riavviare. Vedi **[Voce](Voice-it)**.

---

## La voce non esce, o esce bassa

| Dove guardare | Cosa cercare |
|---|---|
| `LogOutput.log` | Righe `[TTS]` (tempo di caricamento, sintesi, RTF) |

**Cause probabili:**

1. **`[AI] TtsEnabled` è `false`.** Accendilo nel pannello **`I`**.
2. **Il modello Kokoro non è al suo posto.** Controlla `tts\kokoro\` con `model.int8.onnx`, `voices.bin`, `tokens.txt` e
   `espeak-ng-data\`.
3. **Il volume è basso.** La voce **non passa dal mixer del gioco** — **ignora gli slider del volume di Valheim** e
   obbedisce **solo** a `[AI] TtsVolume`. Regola quella chiave (o lo slider nel pannello `I`).
4. **Test isolato:** nella console (**`F5`**) esegui `modia_tts_teste Buongiorno, capo!`. Se funziona lì, il problema è
   la configurazione; se non funziona, è il modello.

---

## Il microfono non registra

| Dove guardare | Cosa cercare |
|---|---|
| `LogOutput.log` | La riga `[STT] Push-to-talk:`: se è attivo, il tasto e la lista numerata dei microfoni |

**Cause probabili:**

1. **Nessuna finestra deve essere aperta.** Il tasto non registra con l'inventario, la mappa, il menu, la chat, la
   console o i pannelli della mod aperti.
2. **`[AI] VoiceEnabled` è `false`.**
3. **È selezionato il microfono sbagliato.** Il log elenca i dispositivi numerati — metti il numero in
   `[AI] VoiceDevice`.
4. **La soglia del silenzio è alta.** `[AI] VoiceSilenceThreshold` (`0.01` di default) taglia l'inizio/la fine della
   registrazione. Se il tuo microfono è debole, abbassala; se capta molto rumore, alzala.
5. **Il parlato è stato troppo breve.** Il log dice che la registrazione era troppo breve — parla più a lungo.
6. **La lingua è sbagliata.** `[AI] VoiceLanguage` è `auto` di default (la **lingua del gioco**); un codice scritto a
   mano di un'altra lingua (per esempio `pt` con il gioco in inglese) peggiora molto il riconoscimento. Dalla 1.9.0 il
   valore automatico segue il gioco e non richiede un riavvio.
7. **Test isolato:** `modia_stt_teste` nella console (**`F5`**) esegue Whisper su un WAV, **senza** microfono. Se
   trascrive, il modello è buono e il problema è la cattura.

> **Windows x64 è obbligatorio** per la voce: i nativi del **STT** (whisper.cpp) **e del TTS**
> (sherpa-onnx/onnxruntime) sono `win-x64`.

---

## Il tasto `V` attiva la raccolta automatica del gioco

**Causa:** in Valheim `V` è il tasto del *Toggle Auto-Pickup* per impostazione predefinita.

**Cosa fare:** la mod blocca già quel toggle — ma **solo** quando `[AI] VoiceEnabled` è acceso **e** il binding della
raccolta automatica del gioco punta allo **stesso** tasto. Quindi:

- **Riassegna la raccolta automatica** nella schermata dei comandi di Valheim a un altro tasto (la mod smette di
  bloccare e hai entrambi), **oppure**
- cambia `[AI] VoiceKey` a un altro tasto nel pannello **`I`**.

Se la riassegni, la mod rivaluta il conflitto subito. Vedi **[Voce](Voice-it)**.

---

## Il pannello `I` non si apre

**Cause probabili:**

1. **C'è una finestra aperta.** Il pannello non si apre con l'inventario, la mappa, il menu, il pannello **`F8`** o il
   campo di ricerca del mercante aperti. Chiudili.
2. **Il tasto è stato cambiato.** Controlla `[AI] PanelKey` (`I` di default).
3. **Stai scrivendo in un campo di testo.** In quel caso il tasto viene ignorato apposta — la mod non ruba il tasto
   della chat/console.

---

## Il pannello `F8` non risponde ai clic / un clic viene inghiottito

**Causa (già corretta):** i pannelli della mod erano sullo stesso strato di disegno dell'HUD del gioco, e l'HUD **è a
`sortingOrder = 400`**. In caso di parità di strato, uGUI decide in base all'ordine di registrazione dei raycaster — e
vince quello del gioco. C'erano anche elementi **decorativi** del pannello che ricevevano clic senza essere controlli.

**Cosa fare:**

- Aggiorna alla versione con la correzione (i canvas della mod ora sono nella fascia **1701+**, **sopra** l'HUD).
- Se il problema torna, accendi `[Advanced] UiDiag = true`: la prossima volta che i pannelli si aprono, il log scrive
  `[UiDiag]` con gli strati della scena e chi sta davanti ai controlli. Poi spegnilo di nuovo.

> Lo stesso aggiustamento dello strato ha corretto il testo del **centro del menu radiale** mescolato con il
> `crosshair` del gioco.

---

## Ho dato il Megingjord al compagno e il peso non è cambiato

| Dove guardare | Cosa cercare |
|---|---|
| Pannello **`I`** (scheda del compagno) e barra dell'**HUD** | Il peso è ancora `…/300` (o il valore che hai messo in `[General] MaxCarryWeight`) anche con la cintura nella sacca |
| Raccolta | Torna a casa per scaricare a **298** invece che intorno a **448** |

**Causa:** nelle versioni **precedenti alla 1.8.0** il bonus della cintura non veniva letto. Il limite di peso del
compagno è **della mod** (`[General] MaxCarryWeight`, 300 di default), non del gioco, e il `+150` del Megingjord è uno
*status effect* il cui unico lettore nel vanilla è il `Player`. Dato che il compagno non è un giocatore, la cintura
occupava solo spazio e pesava.

**Cosa fare:** **aggiorna la mod** (la correzione è arrivata nella 1.8.1). Dopo di che il `/450` compare in ~**0,5 s**
(con il `300` predefinito), e questa riga lo conferma nel `LogOutput.log`:

```
[Carry] "Thorin": +150 de capacidade (Megingjord) - teto 450
```

> La cintura **non deve essere equipaggiata** — vale in qualsiasi punto della sacca (su un `Humanoid` che non è un
> giocatore lo slot di utilità non sopravvive nemmeno al ricaricamento). E **due** cinture **non si sommano**: il bonus
> conta una volta. I dettagli delle due chiavi sono in **[Configurazione](Configuration-it)**.

---

## Il gioco non carica la mod

| Dove guardare | Cosa cercare |
|---|---|
| `LogOutput.log` | `AI Companions v1.9.1 loading...` e `AI Companions loaded successfully! (N methods patched)` — il `v1.9.1` è la versione che hai installato |

**Cause probabili:**

1. **La cartella è nel posto sbagliato.** Il percorso deve essere `<Valheim>\BepInEx\plugins\AI-Companions\` — con
   `Companions.dll` **dentro**.
2. **Mancano le DLL di dipendenza.** `Whisper.net.dll`, `sherpa-onnx.dll` e le `System.*` devono stare accanto a
   `Companions.dll`.
3. **BepInEx non è installato** (o è su una versione incompatibile). Usa **BepInExPack_Valheim 5.4.2351**.
4. **Versione del gioco incompatibile.** La mod è per **Valheim 1.0.x** (Unity 6).

> **L'errore PlayFab / NullReferenceException all'avvio è normale** e **non** è della mod. Non preoccuparti.

---

## Le mie impostazioni sono tornate ai valori predefiniti (o "la mod non legge il mio `.cfg`")

**Causa:** è la **1.8.3** la versione in cui il file di configurazione ha cambiato nome e lingua (per questo un file
della 1.8.2 o precedente non ha effetto). La mod legge **`BepInEx\config\AiCompanions.cfg`**;
`Maddhattr.OfflineCompanionsReborn.cfg` (delle versioni precedenti) **non viene più letto** e resta sul disco **senza
effetto** — per questo le opzioni tornano ai **valori predefiniti**, compresa la `ApiKey` vuota. Anche la sezione
dell'IA è diventata **`[AI]`**, con le chiavi in **inglese**.

**Cosa fare:**

1. Conferma che il file esista: `<Valheim>\BepInEx\config\AiCompanions.cfg` (viene creato al primo avvio).
2. Rifai quello che avevi a mano — la cosa più importante è la **`[AI] ApiKey`**. Il menu **`F8`** (o il pannello `I`)
   mostra tutto in gioco, con i nomi nella **lingua del gioco**.
3. Cancella il `.cfg` vecchio per non confonderti: **non** ricomincia a essere letto.
4. Controlla nel log la riga `[Config] Maddhattr.OfflineCompanionsReborn.cfg was replaced by AiCompanions.cfg ...`.

> **Il salvataggio, il mondo, i compagni, le case e l'inventario non sono toccati** — il **GUID** del plugin e le chiavi
> `HC_*` non sono cambiati. Solo la configurazione della mod torna al predefinito.
>
> **La 1.9.x non azzera niente.** Se il tuo file è già l'`AiCompanions.cfg` (1.8.3 o più recente), l'aggiornamento
> sposta solo i vecchi valori fissi di voce/lingua (`pt`, `pt-br`, `42` e il prompt in portoghese) su `auto` — la tua
> chiave e le altre scelte restano.

---

## Ancora non risolto?

Raccogli queste informazioni e apri una issue su
https://github.com/julianu37/AI-Companions/issues :

- L'estratto del `LogOutput.log` intorno al problema (le righe `[IA]`, `[STT]` o `[TTS]`).
- La versione della mod (appare all'avvio del log) e la versione di Valheim.
- Cosa hai fatto (chat o voce) e cosa è successo.
