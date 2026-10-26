> 🌍 [English](LLM) · [Português](LLM-pt) · [Español](LLM-es) · [Français](LLM-fr) · **Italiano**

# LLM (Groq, LM Studio o OpenRouter)

La mod capisce gli ordini mandando il testo a un **LLM** tramite un'API **compatibile con OpenAI**. Ci sono tre
percorsi e ne scegli **uno**:

| Percorso | Cosa serve | Dove va il testo | Quando sceglierlo |
|---|---|---|---|
| **Groq** — **predefinito** | internet e una **chiave gratuita** | **esce dalla tua macchina** (servizio remoto) | Vuoi usarlo senza installare niente, e veloce |
| **LM Studio** (o Ollama) — facoltativo | il modello scaricato e il server acceso | resta **sulla tua macchina** | Vuoi privacy totale o uso intenso |
| **OpenRouter** — alternativa | internet e una **chiave gratuita** | **esce dalla tua macchina** (servizio remoto) | Usi già OpenRouter o vuoi provare un altro modello |

Questa pagina parte da **Groq**, perché **è già il predefinito** della mod: lo installi, incolli la chiave e hai finito.
Poi viene l'opzione **locale** (**LM Studio**, che continua a funzionare uguale) e, per ultimo, **OpenRouter** — che
funziona ancora, ma il cui tier gratuito è **lento** e per questo non è più il predefinito.

> **Vieni da una versione precedente (o dalla 1.8.2)?** Nella **1.8.3** il file è diventato **`AiCompanions.cfg`** e le
> impostazioni **tornano ai valori predefiniti** — compresa la `ApiKey`, che va incollata di nuovo. Anche la sezione ha
> cambiato nome: era `[IA]`, ora è **`[AI]`**, con le chiavi in **inglese** (`Model`, non `Modelo`). Il `.cfg` vecchio
> (`Maddhattr.OfflineCompanionsReborn.cfg`) resta sul disco e **non** viene letto.
> La **1.9.x non azzera niente**: se il file è già `AiCompanions.cfg`, resta com'è (solo i vecchi valori fissi di
> voce/lingua passano su `auto`).
> Passare a Groq è facoltativo — ed è solo modificare `BaseUrl`, `Model` e `ApiKey` (nel `.cfg` o nella sezione
> **Impostazioni del LLM** del pannello `I`).

---

## 1. Groq: solo la chiave e internet (predefinito)

**Groq** è un servizio che parla la **stessa API di OpenAI** e serve modelli aperti, con un **tier gratuito**. Per la
mod è il percorso più veloce: non c'è niente da installare né server da lasciare acceso — il `.cfg` arriva già puntato
su di lui.

### 1.1. Crea la tua chiave gratuita

1. Apri **https://console.groq.com** e crea l'account (e-mail o login social).
2. Vai su **https://console.groq.com/keys** e clicca **Create API Key** (dagli un nome qualsiasi, per esempio
   `Valheim`).
3. **Copia la chiave generata** — è quella che va in `[AI] ApiKey`. Viene mostrata **una volta sola**; se chiudi la
   finestra senza copiarla, creane un'altra.

### 1.2. Cosa mettere nel `.cfg` (o nel pannello)

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
| `BaseUrl` | `https://api.groq.com/openai/v1` | L'indirizzo di Groq — **è già il predefinito**. Il `/chat/completions` viene aggiunto da solo |
| `Model` | `openai/gpt-oss-20b` | Il modello aperto di OpenAI servito da Groq — **è già il predefinito**. È veloce e segue bene l'uscita strutturata |
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

> Nella cattura sopra il selettore è su **Groq** (il predefinito), con l'URL `https://api.groq.com/openai/v1`, il
> modello `openai/gpt-oss-20b` e la chiave mascherata. Cambiare il selettore su **LM Studio** o **OpenRouter** riempie
> l'URL e il modello di quel provider — e il pulsante della chiave inizia a puntare alla sua pagina (o sparisce, su LM
> Studio).

> `BaseUrl`, `Model` e `ApiKey` compaiono anche nel pannello **`F8`**, ma sono chiavi di **testo**: per incollare una
> chiave, il campo del pannello `I` (o il `.cfg` stesso) è più comodo. Quello che salvi lì vale dal **messaggio
> successivo**, senza riavviare il gioco.

### 1.3. Prova la connessione

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

### 1.4. Cosa aspettarsi dalla risposta

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

### 1.5. Limiti del tier gratuito

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
  conversazione sempre) lo urta — lì l'**opzione locale** (sotto) risolve, senza limite di richieste.
- `[AI] CooldownSeconds` (**3** di default) aiuta: evita che una raffica di messaggi diventi una raffica di richieste.
- **429** è un limite di richieste/token, **401** è la chiave — la differenza è in
  **[Problemi comuni](Troubleshooting-it)**.

> **L'uscita strutturata continua a valere.** `gpt-oss-20b` accetta `json_schema` in modalità **strict** e anche la
> modalità *best-effort*; **streaming** e **tool use** non sono supportati insieme all'uscita strutturata — e la mod
> **non** usa nessuno dei due (fonte: https://console.groq.com/docs/structured-outputs).

### 1.6. Privacy e la chiave

- **Con Groq, il testo degli ordini e della conversazione esce dalla tua macchina.** Va sui server di Groq — la
  persona, la memoria e quello che scrivi entrano in quell'invio. **Non scrivere niente di sensibile** nella chat del
  compagno.
- Groq è un **servizio remoto**: vale la sua politica sui dati, non quella della mod.
- Il riconoscimento vocale (**STT**) e la **voce del compagno** (**TTS**) restano **locali**: solo il testo dell'ordine
  va al LLM.
- **La chiave resta nel `.cfg`.** Non condividere quel file con la chiave compilata e non mettere mai una chiave in un
  repository. Se trapela, cancellala su **https://console.groq.com/keys** e creane un'altra.
- Vuoi che **non esca niente** dalla macchina? Usa LM Studio (o Ollama) qui sotto — lì anche il LLM è locale.

---

## 2. Alternativa locale: LM Studio (facoltativo)

Qui **niente esce dal tuo computer**: il modello gira sulla tua macchina. È il percorso per chi vuole privacy totale,
per chi gioca senza internet o per un uso intenso senza dipendere da una coda o da un limite di token. In cambio devi
scaricare il modello e tenere il server acceso mentre giochi.

### 2.1. Installa LM Studio

Scaricalo da **https://lmstudio.ai** e installalo. È un'applicazione a finestra, senza riga di comando.

### 2.2. Scarica il modello

Il modello **consigliato** è **`qwen2.5-3b-instruct`**, quantizzazione **Q4_K_M** (~**1,93 GB**).

In LM Studio:

1. Apri la scheda di **ricerca dei modelli** (l'icona della lente, nella barra laterale).
2. Cerca **`qwen2.5-3b-instruct`**.
3. Scegli la quantizzazione **Q4_K_M** e scaricala (~1,93 GB).

> **Perché un modello così piccolo?** Perché è **veloce** (ordini in ~0,35–0,4 s) e, con l'**uscita strutturata** della
> mod, risponde nel formato giusto in modo affidabile. Un 3B gira bene anche su CPU.

### 2.3. Carica il modello

Nella scheda **chat**, in alto, scegli il `qwen2.5-3b-instruct` che hai scaricato. Diventa **READY** quando finisce di
caricare.

### 2.4. Accendi il Local Server

Apri la scheda **Model Server** nella barra laterale (nelle versioni vecchie di LM Studio si chiama **Developer**) e
accendi il **Local Server** (il pulsante **Start** / **Running**).

![LM Studio con il Local Server acceso, il modello caricato e l'API Model Identifier](images/lmstudio-servidor.png)

Controlla sullo schermo:

| Cosa guardare | Valore atteso |
|---|---|
| **Status** | `Running` |
| **Porta** | `1234` |
| **Modello caricato** | `qwen2.5-3b-instruct` (READY, ~1,93 GB) |
| **Parallel** | `4` |
| **Endpoints** | OpenAI-compatible (`/v1/models`, `/v1/chat/completions`) |
| **API Model Identifier** | `qwen2.5-3b-instruct` — è **questo** nome che va in `[AI] Model` |

> L'**API Model Identifier** (nel pannello *Model Information*) è il nome esatto che il server espone. Usalo nel
> `.cfg`, e non il nome della cartella scaricata.

### 2.5. Cosa mettere nel `.cfg` per usare LM Studio

Il predefinito della mod è Groq, quindi **cambia** le prime due chiavi e **cancella** la chiave API:

```ini
[AI]
BaseUrl = http://127.0.0.1:1234/v1
Model = qwen2.5-3b-instruct
ApiKey =
StructuredOutput = true
```

| Chiave | Valore | Perché |
|---|---|---|
| `BaseUrl` | `http://127.0.0.1:1234/v1` | Indirizzo del server di LM Studio. Il `/chat/completions` viene aggiunto da solo |
| `Model` | `qwen2.5-3b-instruct` | L'**API Model Identifier** che LM Studio mostra |
| `ApiKey` | *(vuoto)* | Il LM Studio locale non chiede una chiave |
| `StructuredOutput` | `true` | Chiede al server il formato JSON — è quello che fa funzionare i modelli piccoli |

> **Prova la connessione** del pannello `I` funziona uguale qui: cambia l'URL e il modello nei campi, prova e solo dopo
> clicca **Applica**.

### 2.6. `127.0.0.1` vs `localhost` — usa l'IP

Nel caso locale usa l'indirizzo **`http://127.0.0.1:1234/v1`** (IPv4) e non `localhost`.

> **Su Windows, `localhost` risolve prima in IPv6 (`::1`)**. Il server di LM Studio ascolta solo su **IPv4**, quindi
> ogni richiesta prova prima IPv6, aspetta il timeout e **solo dopo** ricade su IPv4. Questo aggiunge circa **2,5 s di
> pedaggio a ogni ordine**.

Con l'indirizzo esplicito `http://127.0.0.1:1234/v1` la risposta torna subito. Anche il testo di aiuto di `BaseUrl` nel
`.cfg` (e nel pannello `F8`) spiega la differenza.

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
| **`openai/gpt-oss-20b`** | **Groq** | **Predefinito.** Modello aperto, ~1.000 token/s, finestra di 131.072 token. Risponde in una frazione di secondo |
| **`qwen2.5-3b-instruct`** | LM Studio | **Consigliato in locale.** Veloce (~0,35–0,4 s per ordine) e affidabile con l'uscita strutturata |
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

<!-- D4: add Español/Français/Italiano -->
