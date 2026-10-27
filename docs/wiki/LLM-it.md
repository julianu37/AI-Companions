> 🌍 [English](LLM) · [Português](LLM-pt) · [Español](LLM-es) · [Français](LLM-fr) · **Italiano**

# LLM (LM Studio, Groq o OpenRouter)

La mod capisce gli ordini mandando il testo a un **LLM** tramite un'API **compatibile con OpenAI**. Ci sono tre
percorsi e ne scegli **uno**:

| Percorso | Cosa serve | Dove va il testo | Quando sceglierlo |
|---|---|---|---|
| **LM Studio** (o Ollama) — **consigliato** | il modello scaricato e il server acceso | resta **sulla tua macchina** | Vuoi privacy, senza chiave e senza limiti di richieste |
| **Groq** — cloud | internet e una **chiave gratuita** | **esce dalla tua macchina** (servizio remoto) | Vuoi che funzioni senza installare niente, e veloce |
| **OpenRouter** — alternativa | internet e una **chiave gratuita** | **esce dalla tua macchina** (servizio remoto) | Usi già OpenRouter o vuoi provare un altro modello |

La pagina parte da **LM Studio**, il percorso consigliato: il modello gira sul tuo computer e niente esce da lì. Poi
viene **Groq**, il servizio cloud che ha bisogno solo della chiave gratuita, e per ultimo **OpenRouter** — che funziona
ancora, ma il cui tier gratuito è **lento**.

> **Vieni da una versione precedente (o dalla 1.8.2)?** Nella **1.8.3** il file è diventato **`AiCompanions.cfg`** e le
> impostazioni **tornano ai valori predefiniti** — compresa la `ApiKey`, che va incollata di nuovo. Anche la sezione ha
> cambiato nome: era `[IA]`, ora è **`[AI]`**, con le chiavi in **inglese** (`Model`, non `Modelo`). Il `.cfg` vecchio
> (`Maddhattr.OfflineCompanionsReborn.cfg`) resta sul disco e **non** viene letto.
> La **1.9.x non azzera niente**: se il file è già `AiCompanions.cfg`, resta com'è (solo i vecchi valori fissi di
> voce/lingua passano su `auto`).
> Cambiare provider è solo modificare `BaseUrl`, `Model` e `ApiKey` (nel `.cfg` o nella sezione **Impostazioni del
> LLM** del pannello `I`).

---

## 1. LM Studio: locale, privato e consigliato

**LM Studio** è un'applicazione che fa girare il modello **sul tuo computer**: niente esce dalla tua macchina, non c'è
chiave né coda, e non ci sono limiti di richieste o di token. Le catture qui sotto sono di **LM Studio 0.4.26**.

### 1.1. Installa LM Studio

Scaricalo da **https://lmstudio.ai** e installalo. È un'applicazione a finestra, senza riga di comando.

### 1.2. Scarica il modello

Nella barra laterale, apri **Model Search** (l'icona della lente), cerca **`qwen2.5-3b-instruct`** e scegli
**`lmstudio-community/Qwen2.5-3B-Instruct-GGUF`**.

*La voce **Model Search** nella barra laterale.*

![LM Studio 0.4.26 con la voce Model Search evidenziata nella barra laterale](images/lmstudio-1-model-search.png)

La pagina del modello mostra *Full GPU Offload Possible*: scarica la quantizzazione **Q4_K_M** (**~1,93 GB**).

*Il risultato della ricerca, con la quantizzazione **Q4_K_M** (1,93 GB) e il pulsante **Download**.*

![LM Studio Model Search: la ricerca di qwen2.5-3b-instruct, il risultato lmstudio-community/Qwen2.5-3B-Instruct-GGUF, la quantizzazione Q4_K_M da 1,93 GB e il pulsante Download](images/lmstudio-2-download-qwen.png)

> **Perché un modello così piccolo?** Perché è **veloce** (ordini in ~0,35–0,4 s) e, con l'**uscita strutturata** della
> mod, risponde nel formato giusto in modo affidabile. Un 3B gira bene anche su CPU.

### 1.3. Accendi il Local Server

Nella barra laterale, apri **Local Server** e controlla che lo **Status** sia **Running** (verde) sulla porta **1234**.

*Il server acceso: **Status: Running** sulla porta 1234.*

![Schermata Model Server di LM Studio: Status Running (verde) sulla porta 1234, il pulsante + Load Model evidenziato e Loaded Models ancora vuoto](images/lmstudio-3-local-server.png)

### 1.4. Carica il modello

Premi **+ Load Model** e scegli **Qwen2.5-3B-Instruct Q4_K_M**.

*La lista del **+ Load Model**, con `Qwen2.5 3B Instruct Q4_K_M` selezionato.*

![Finestra Load Model di LM Studio: Qwen2.5 3B Instruct Q4_K_M evidenziato nella lista dei modelli](images/lmstudio-4-load-model.png)

### 1.5. Conferma l'API Identifier e carica

La schermata di caricamento mostra l'**API Identifier** **`qwen2.5-3b-instruct`** — è **questo** nome che va in
`[AI] Model` (il nome esatto che il server espone, non il nome della cartella scaricata). Il **Context Length** è
**8192**, il **GPU Offload** è al massimo e la memoria stimata è di circa **2 GB** di RAM/VRAM.

*La schermata di caricamento: **API Identifier** `qwen2.5-3b-instruct`, **Context Length** 8192 e **GPU Offload** al massimo.*

![Schermata di caricamento di LM Studio: API Identifier qwen2.5-3b-instruct, Context Length 8192, GPU Offload 36, memoria stimata 2,03 GB e il pulsante Load Model](images/lmstudio-5-load-settings.png)

Premi **Load Model**. Quando finisce, **Loaded Models** mostra **READY** e `llm qwen2.5-3b-instruct` (1,93 GB) — puoi
andare al gioco.

*Il modello pronto: **READY**, `llm qwen2.5-3b-instruct`, **Size 1,93 GB** e **Parallel 4**.*

![Loaded Models di LM Studio: il badge READY, llm qwen2.5-3b-instruct, Size 1,93 GB e Parallel 4; a destra, l'API Model Identifier qwen2.5-3b-instruct](images/lmstudio-6-ready.png)

### 1.6. Punta la mod su di lui

In gioco, apri il pannello **`I`** → **Impostazioni del LLM** → il menu del provider → **LM Studio** (riempie l'URL e il
modello) → **Applica** → **Prova la connessione**. Oppure modifica il `.cfg`, sezione `[AI]`:

```ini
BaseUrl = http://127.0.0.1:1234/v1
Model   = qwen2.5-3b-instruct
ApiKey  = (vuoto)
```

> **La mod nasce puntata su Groq** (`https://api.groq.com/openai/v1` + `openai/gpt-oss-20b`): per questo scegli **LM
> Studio** qui (nel menu o nel `.cfg`) — è un cambio di due minuti.

> **Prova la connessione** del pannello `I` funziona uguale qui: cambia l'URL e il modello nei campi, prova e solo dopo
> clicca **Applica**.

---

## 2. Groq: solo la chiave e internet (cloud)

**Groq** è un servizio che parla la **stessa API di OpenAI** e serve modelli aperti, con un **tier gratuito**. Per la
mod è l'opzione cloud più semplice: non c'è niente da installare né server da lasciare acceso — e il `.cfg` arriva già
puntato su di lui.

### 2.1. Crea la tua chiave gratuita

1. Apri **https://console.groq.com** e crea l'account (e-mail o login social).
2. Vai su **https://console.groq.com/keys** e clicca **Create API Key** (dagli un nome qualsiasi, per esempio
   `Valheim`).
3. **Copia la chiave generata** — è quella che va in `[AI] ApiKey`. Viene mostrata **una volta sola**; se chiudi la
   finestra senza copiarla, creane un'altra.

### 2.2. Cosa mettere nel `.cfg` (o nel pannello)

Apri `<Valheim>\BepInEx\config\AiCompanions.cfg` e controlla la sezione `[AI]`:

```ini
[AI]
BaseUrl = https://api.groq.com/openai/v1
Model = openai/gpt-oss-20b
ApiKey = <la tua chiave>
StructuredOutput = true
```

| Chiave | Valore | Perché |
|---|---|---|
| `BaseUrl` | `https://api.groq.com/openai/v1` | L'indirizzo di Groq. Il `/chat/completions` viene aggiunto da solo |
| `Model` | `openai/gpt-oss-20b` | Il modello aperto di OpenAI servito da Groq. È veloce e segue bene l'uscita strutturata |
| `ApiKey` | **la tua chiave** | Su Groq è **obbligatoria**. Vuota o sbagliata, il server rifiuta la richiesta con un errore di autenticazione |
| `StructuredOutput` | `true` | Chiede al server il formato JSON — è quello che fa funzionare i modelli piccoli. `gpt-oss-20b` accetta `json_schema` in modalità **strict** (decodifica vincolata), che è la modalità usata dalla mod |

Puoi farlo anche **in gioco**, senza aprire il file: tasto **`I`** → scheda **IA** → **Impostazioni del LLM**. È dove
stanno l'**URL del server**, il **Modello**, la **Chiave API** e i pulsanti **Applica** e **Prova la connessione** — e,
**sopra l'URL**, il menu a tendina **"Scegli il tuo provider LLM"**: scegliere **Groq**, **LM Studio** o **OpenRouter**
**riempie l'URL e il modello** da solo (la **chiave non viene toccata** e niente viene salvato prima di **Applica**).
Sulla riga della **Chiave API** c'è il pulsante **"Crea qui la tua chiave API gratuita"**, che **apre nel browser** la
pagina delle chiavi del provider scelto (`console.groq.com/keys` su Groq, `openrouter.ai/keys` su OpenRouter) —
sparisce su LM Studio, che non ha chiave.

> Entrambi sono **solo una scorciatoia**: il `.cfg` comanda ancora ed è lui che conta. Il selettore **deduce** il
> provider dalla stessa `BaseUrl`, quindi un URL di **Ollama** o di un server tuo appare come **Personalizzato (URL
> manuale)** e non viene sovrascritto niente.

![Il pannello IA (tasto I), scheda IA: il menu "Scegli il tuo provider LLM" su Groq, l'URL del server con api.groq.com, il modello openai/gpt-oss-20b, la chiave API mascherata e, a destra della chiave, il pulsante "Crea qui la tua chiave API gratuita"](images/en-ai-panel.jpg)

> Nella cattura sopra il selettore è su **Groq** (il provider che arriva nel `.cfg`), con l'URL `https://api.groq.com/openai/v1`, il
> modello `openai/gpt-oss-20b` e la chiave mascherata. Cambiare il selettore su **LM Studio** o **OpenRouter** riempie
> l'URL e il modello di quel provider — e il pulsante della chiave inizia a puntare alla sua pagina (o sparisce, su LM
> Studio).

> `BaseUrl`, `Model` e `ApiKey` compaiono anche nel pannello **`F8`**, ma sono chiavi di **testo**: per incollare una
> chiave, il campo del pannello `I` (o il `.cfg` stesso) è più comodo. Quello che salvi lì vale dal **messaggio
> successivo**, senza riavviare il gioco.

### 2.3. Prova la connessione

Con URL, modello e chiave compilati, premi **Prova la connessione** (nel pannello `I`, accanto ad **Applica**). Il test
gira **in background** — il gioco non si blocca — e risponde nella **chat/HUD**, oltre a lasciare una riga `[IA]` nel
log. Inizia chiedendo al server la **lista dei modelli** e, se il server non ha quella lista, manda una richiesta di
chat minima. Il risultato è uno di questi:

- **Connessione OK: N modello/i sul server** — l'URL ha risposto.
- **Connessione OK: … e il modello configurato è nella lista** — il caso migliore.
- **Connessione OK: … ma "<modello>" non è nella lista** — l'URL ha risposto, ma il nome non è apparso nella lista che
  il server restituisce. Controlla che il `Model` sia scritto esattamente `openai/gpt-oss-20b` e passa al test in gioco,
  mandando un ordine vero.
- **Connessione fallita: …** — arriva con il motivo (stato/messaggio del server): URL sbagliato, chiave vuota/sbagliata,
  indirizzo senza `http://`/`https://`…

> **Quello che conferma la chiave è un ordine vero.** **Prova la connessione** dice che l'URL risponde e che il modello
> è nella lista; se passa ma l'ordine fallisce con **401**, il problema è la chiave — vedi
> **[Problemi comuni](Troubleshooting-it)**.

### 2.4. Cosa aspettarsi dalla risposta

La comprensione continua a girare **in background**: un modello lento **non** blocca il gioco. La latenza compare nel
log, a ogni ordine:

```
[IA] Tempo: 378 ms (... tokens, ... tokens/s)
```

Con `openai/gpt-oss-20b` su Groq la risposta arriva in una **frazione di secondo** nella nostra misurazione — molto più
veloce del tier gratuito di OpenRouter. Il modello è servito a ~**1.000 token/s** e ha una finestra di **131.072 token**
(completamento massimo 65.536), che avanza per il prompt della mod (fonte:
https://console.groq.com/docs/models). La mod avvisa nel log quando un ordine supera **5 s**, e
`[AI] TimeoutSeconds` (**30** di default) è il tetto di attesa.

### 2.5. Limiti del tier gratuito

Quello che la documentazione di Groq indica oggi per `openai/gpt-oss-20b` nel piano gratuito (fonte:
https://console.groq.com/docs/rate-limits — i numeri possono cambiare):

| Limite | Valore |
|---|---|
| Richieste al minuto (**RPM**) | **30** |
| Richieste al giorno (**RPD**) | **1.000** |
| Token al minuto (**TPM**) | **8.000** |
| Token al giorno (**TPD**) | **200.000** |

In pratica **il TPM è il limite che morde per primo**: il prompt della mod spende circa **1.800 token per ordine**, il
che dà **~4 ordini al minuto**. Oltre, Groq risponde **429** e l'ordine viene rifiutato con un avviso — niente si rompe
e niente resta bloccato; aspetta un po' e mandalo di nuovo.

- Il tetto di **8.000 token al minuto** regge una sessione normale di ordini sporadici. L'uso intenso (molti ordini e
  conversazione sempre) lo urta — lì l'**opzione locale** (sezione 1) risolve, senza limite di richieste.
- `[AI] CooldownSeconds` (**3** di default) aiuta: evita che una raffica di messaggi diventi una raffica di richieste.
- **429** è un limite di richieste/token, **401** è la chiave — la differenza è in
  **[Problemi comuni](Troubleshooting-it)**.

> **L'uscita strutturata continua a valere.** `gpt-oss-20b` accetta `json_schema` in modalità **strict** e anche la
> modalità *best-effort*; **streaming** e **tool use** non sono supportati insieme all'uscita strutturata — e la mod
> **non** usa nessuno dei due (fonte: https://console.groq.com/docs/structured-outputs).

### 2.6. Privacy e la chiave

- **Con Groq, il testo degli ordini e della conversazione esce dalla tua macchina.** Va sui server di Groq — la
  persona, la memoria e quello che scrivi entrano in quell'invio. **Non scrivere niente di sensibile** nella chat del
  compagno.
- Groq è un **servizio remoto**: vale la sua politica sui dati, non quella della mod.
- Il riconoscimento vocale (**STT**) e la **voce del compagno** (**TTS**) restano **locali**: solo il testo dell'ordine
  va al LLM.
- **La chiave resta nel `.cfg`.** Non condividere quel file con la chiave compilata e non mettere mai una chiave in un
  repository. Se trapela, cancellala su **https://console.groq.com/keys** e creane un'altra.
- Vuoi che **non esca niente** dalla macchina? Usa LM Studio (o Ollama) nella sezione 1 — lì anche il LLM è locale.

---

## 3. OpenRouter (funziona ancora, ma il gratuito è lento)

**OpenRouter** è stato il predefinito fino alla **1.7.0** e **continua a funzionare**. Dà accesso a vari modelli con la
stessa chiave, compresi i **gratuiti** — ma il router gratuito (`openrouter/free`) è **lento e imprevedibile**: ogni
richiesta sceglie un modello gratuito diverso e passa dalla coda del provider. Per questo ha lasciato il posto di
predefinito. Usalo se **hai già** la configurazione o vuoi **provare un altro modello** del catalogo.

```ini
[AI]
BaseUrl = https://openrouter.ai/api/v1
Model = openrouter/free
ApiKey = <la tua chiave di openrouter.ai/keys>
```

| Chiave | Valore | Perché |
|---|---|---|
| `BaseUrl` | `https://openrouter.ai/api/v1` | L'indirizzo di OpenRouter |
| `Model` | `openrouter/free` | **Free Models Router**: a ogni richiesta sceglie un modello **gratuito** (e filtra quelli che supportano l'uscita strutturata) |
| `ApiKey` | **la tua chiave** | Su OpenRouter è **obbligatoria**. La gratuita esce da **https://openrouter.ai/keys** |

- I modelli **gratuiti** (con `:free` nel nome) hanno un limite di **20 richieste al minuto** e un **tetto giornaliero**
  per account: **50 richieste al giorno** per chi **non ha mai comprato credito** e **1.000 al giorno** da **10 crediti
  comprati** (in totale, non al mese).
- La latenza **varia** da un ordine all'altro e può richiedere qualche secondo nelle ore di punta.
- OpenRouter stesso descrive i modelli gratuiti come **per provare e per uso a basso volume**.
- **Prova la connessione** del pannello `I` funziona anche qui — e, dato che la lista dei modelli di OpenRouter è
  **pubblica**, può rispondere `Connessione OK` anche con la chiave sbagliata: quello che conferma la chiave è un
  ordine vero.
- Con OpenRouter il testo degli ordini e della conversazione **esce dalla tua macchina**, come con Groq. Vale la sua
  politica sui dati. Chiave trapelata? Cancellala su **https://openrouter.ai/settings/keys** e creane un'altra.

---

## 4. Come sapere che funziona

Entra nel mondo e scrivi nella chat `raccogli 10 legna` (vale per Groq, per **LM Studio** e per **OpenRouter**). Se
funziona:

- Il compagno risponde (e parla) una conferma e va a raccogliere.
- Nel log (`BepInEx\LogOutput.log`) compaiono le righe `[IA]` con il risultato e il tempo.

---

## 5. Altri modelli e server

Qualsiasi endpoint **compatibile con OpenAI** va bene — la mod fa solo `POST {BaseUrl}/chat/completions`.

### Modelli

| Modello | Server | Nota |
|---|---|---|
| **`openai/gpt-oss-20b`** | **Groq** | **Opzione cloud.** Modello aperto, ~1.000 token/s, finestra di 131.072 token. Risponde in una frazione di secondo |
| **`qwen2.5-3b-instruct`** | LM Studio | **Consigliato.** Veloce (~0,35–0,4 s per ordine) e affidabile con l'uscita strutturata |
| `openrouter/free` | OpenRouter | Router di modelli gratuiti: ne sceglie uno per richiesta. Niente da installare, ma la latenza varia |
| `google/gemma-4-e4b` | LM Studio | Funziona anche, ma è **molto più lento** (~3 s per ordine) |
| Modelli più grandi | qualsiasi | Migliori in conversazione, ma **servono una GPU** per rispondere veloce in locale |
| Modelli a pagamento di Groq/OpenRouter | — | Basta cambiare il `Model` con il nome del catalogo — e lì sono i crediti dell'account a essere spesi |

### Altri server

- **Ollama:** `http://127.0.0.1:11434/v1`
- **llama.cpp** con il server in modalità OpenAI
- **API a pagamento** (OpenAI, ecc.): punta il `BaseUrl` e compila la `ApiKey` — ricordando che, lì, i tuoi messaggi
  **escono dalla tua macchina**, come con Groq e OpenRouter.

> **Non mettere mai una chiave API in un repository.** La `ApiKey` resta nel `.cfg` locale; non condividere quel file
> con la chiave compilata.

### Impostazioni utili

| Chiave `[AI]` | Predefinito | Cosa fa |
|---|---|---|
| `Temperature` | `0.1` | Tienila bassa: lo stesso ordine deve dare sempre lo stesso JSON |
| `TimeoutSeconds` | `30` | Quanto aspettare il modello. La richiesta gira in background — un modello lento non blocca il gioco |
| `StructuredOutput` | `true` | Se il tuo server/modello non lo supporta, la mod ricade da sola sul formato vecchio e avvisa nel log |
| `CooldownSeconds` | `3` | Tempo minimo tra due ordini, perché una raffica di messaggi non inondi il modello |

---

## Se non funziona

I quattro casi sotto coprono quasi tutto:

| Cosa appare | Cos'è | Cosa fare |
|---|---|---|
| **401** (*unauthorized* / *invalid api key*) | La `ApiKey` è vuota, sbagliata o di un altro server | Incolla la chiave giusta: `console.groq.com/keys` (Groq), `openrouter.ai/keys` (OpenRouter) — o lasciala **vuota** su LM Studio/Ollama |
| **429** (*rate limit*) | Limite di **richieste o di token** del tier gratuito | Aspetta qualche secondo e manda di nuovo l'ordine; riduci la frequenza; usa il LM Studio locale per l'uso intenso |
| **404** / **400** | L'**URL** (`BaseUrl`) o il **modello** (`Model`) sono sbagliati | Controlla `https://api.groq.com/openai/v1` + `openai/gpt-oss-20b` (Groq) o `https://openrouter.ai/api/v1` + `openrouter/free` (OpenRouter) |
| **Ci mette troppo** | Coda del provider o modello grande | Vedi "ci mette troppo a rispondere" in **[Problemi comuni](Troubleshooting-it)** |

Il passo passo di ogni sintomo è in **[Problemi comuni](Troubleshooting-it)** — compresi "non risponde a niente",
"risponde con un errore di autenticazione", "ci mette troppo a rispondere" e "risponde male o ignora il numero".
