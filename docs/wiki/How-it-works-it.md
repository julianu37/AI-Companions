> 🌍 [English](How-it-works) · [Português](How-it-works-pt) · [Español](How-it-works-es) · [Français](How-it-works-fr) · **Italiano**

# Come funziona

L'architettura della mod e il percorso che fa un ordine, dal tuo microfono (o dalla chat) fino al compagno che agisce.

---

## Panoramica

![Come scorre un ordine: la voce (microfono, 16 kHz) o la chat del gioco vanno nella stessa casella degli ordini; il compagno agisce, il LLM capisce l'ordine e scrive la risposta, e la voce locale (Kokoro-82M) la dice nella sua posizione](images/flow-ai.png)

Tre pezzi di IA, tutti su **thread di fondo** (il LLM può essere remoto; il STT e il TTS sono sempre locali):

| Pezzo | Cos'è | Dove gira |
|---|---|---|
| **LLM** | Capisce l'ordine e conversa | **LM Studio/Ollama** (locale, consigliato) o **Groq** (remoto, quello che arriva nel `.cfg`) — qualsiasi endpoint compatibile con OpenAI |
| **STT** | Trascrive quello che dici | `Whisper.net` (whisper.cpp) dentro il gioco |
| **TTS** | Fa parlare il compagno | `sherpa-onnx` + Kokoro-82M dentro il gioco |

---

## Il percorso di un ordine

1. **Ingresso.** Scrivi nella chat **o** tieni premuto `V` e parli. In entrambi i casi il testo finisce nello
   **stesso** punto d'ingresso — non c'è logica duplicata tra chat e voce.
2. **Prompt.** La mod monta il prompt del messaggio: il **contratto** fisso (nella **lingua del gioco**) + il **nome**
   del compagno + la **personalità** + la **memoria** recente.
3. **LLM.** Il testo va al server configurato (`POST {BaseUrl}/chat/completions`) in una **Task di fondo** — il gioco
   non aspetta mai.
4. **JSON dell'intenzione.** Il modello restituisce un'intenzione strutturata con cinque campi:

```json
{ "action": "wood", "reply": "Vado a prendere la legna.", "target": null, "resource": "wood", "amount": 20 }
```

5. **Parser tollerante.** La mod legge l'intenzione accettando variazioni (`action`, `reply`, `target`, `resource`,
   `amount`) e **valida** l'obiettivo contro il mondo reale — non inventa mai un oggetto né una creatura.
6. **Esecuzione.** L'intenzione diventa la **stessa azione del menu radiale**: la posizione di combattimento, il
   lavoro, l'equipaggiamento, l'obiettivo… Tutto passa dallo stesso percorso dei controlli normali della mod.

> La coda dei risultati è **thread-safe** e viene svuotata nell'`Update`. Niente che blocchi il thread principale.

---

## Uscita strutturata (JSON schema)

La richiesta al LLM porta un **JSON schema** (`response_format`) che fissa le cinque chiavi e la **lista chiusa di
30 azioni** — la stessa lista del contratto, da `follow` a `none`.

È quello che **sblocca i modelli piccoli**: con lo schema il server è obbligato a rispondere nel formato giusto.
Misurato con il prompt attuale, `qwen2.5-3b-instruct` dava **0/6** risposte in JSON ed è passato a **6/6**.

Se il server **non supporta** il formato, la mod **ricade da sola** in una catena di tentativi, uno alla volta:

1. `json_schema` (l'ideale)
2. `{"type":"json_object"}` (JSON libero)
3. **senza** `response_format` (il formato vecchio)

Solo un **rifiuto del formato** fa scattare il tentativo successivo — un timeout, un errore di rete o un errore del
server **non** vengono ripetuti. Una riga nel log dice quando l'uscita strutturata è accesa e **un solo avviso**
compare se la mod arriva al formato vecchio.

Chiave: `[AI] StructuredOutput` (`true` di default).

---

## Personalità, memoria e conversazione

- **Personalità:** preset scritti dentro la mod (**Vichingo leale**, **Mago sarcastico**, **Sarcastico**,
  **Guida paziente**; i nomi seguono la lingua del gioco) o **testo libero** del giocatore — il testo libero **vince**
  sul preset. Entra nel prompt, quindi il compagno risponde nel tono che hai scelto.
- **Memoria:** gli ultimi `[AI] Memory` scambi **per compagno** (0–12). La memoria va come **testo nel system
  prompt**, mai come messaggi extra — il contratto JSON resta l'ultima cosa che il modello legge. Viene **dimenticata**
  quando il compagno muore, viene congedato o cambi mondo, e **niente viene salvato nel mondo**.
- **Conversazione libera:** quando l'azione è `none` e c'è un `reply`, il compagno **parla** (fumetto + voce) e il
  testo appare nella chat. In un **ordine** vince la frase localizzata della mod, e il `reply` va solo nel log —
  niente viene detto due volte.

![Il pannello IA sulla scheda di un compagno: il nome, il tipo, lo stato, Richiama e Aspetto, e Voce e personalità di questo compagno con la voce e il pulsante Ascolta](images/en-ai-panel-companions-editor.jpg)

---

## La voce del compagno (TTS)

- La voce **segue la lingua del gioco**: `[AI] TtsLanguage` e `TtsVoice` arrivano come `auto` / `0`, quindi viene usata
  la voce predefinita di quella lingua a meno che tu non ne abbia scelta una. Cambiare la lingua non richiede un
  riavvio.
- Il modello Kokoro viene caricato **una volta**, in background, quando entri nel mondo.
- La sintesi gira su un **thread di fondo dedicato**; i pezzi pronti tornano da un'altra coda e vengono svuotati
  nell'`Update`.
- Il parlato viene montato **frase per frase**: la prima suona già mentre la successiva viene generata.
- Le **frasi fisse della mod** vanno in una **cache** (per byte, scartando le più vecchie) ed escono all'istante. Le
  risposte del modello, che sono nuove ogni volta, **non** entrano nella cache.
- La riproduzione usa **una** `AudioSource` **3D** nella posizione del compagno — il suono viene da dove si trova lui,
  non dal centro dello schermo.

La regola di **cosa viene detto** è semplice: **la voce segue quello che è scritto sullo schermo**. In un ordine, la
frase della mod; in una conversazione, la risposta del modello. Niente viene detto due volte.

---

## Non bloccare il gioco

Questo è un requisito del progetto, non un dettaglio:

- Ogni chiamata di rete (LLM) gira in una **Task di fondo**, con **timeout** configurabile.
- Il STT e il TTS girano su **thread di fondo** con code thread-safe, svuotate nell'`Update`.
- Niente `Find*` né LINQ per frame; quello che si può mettere in cache per frame, viene messo.
- Gli `AudioClip` e gli oggetti temporanei vengono **distrutti** alla fine.

---

## Privacy

**La voce e la trascrizione non escono mai dalla tua macchina:**

- Il tuo parlato viene trascritto **dentro il gioco** (Whisper, locale).
- La voce del compagno viene sintetizzata **dentro il gioco** (Kokoro, locale).
- Il **LLM** è l'unico che può essere remoto: il `.cfg` arriva puntato su **Groq** (o **OpenRouter**, come
  alternativa), e lì il testo degli ordini e della conversazione **esce dalla tua macchina**. Il percorso
  consigliato è il **LM Studio/Ollama** locale, dove niente esce
  (**[LLM (LM Studio, Groq o OpenRouter)](LLM-it)**).

La `ApiKey`, quando c'è, resta solo nel `.cfg` locale — non metterla mai in un repository e non condividere quel file
con la chiave compilata.

---

## Tipi di compagno

Il **tipo** di un compagno viene dal **prefab** che il gioco usa per crearlo, ed è lui che decide **cosa fa il
compagno**:

- **Compagno** (il predefinito) è una copia del **personaggio del giocatore** — è quel "corpo da giocatore" che i
  lavori usano, ed è l'**unico tipo completo**.
- **Mago**, **Mago del fuoco**, **Mago del ghiaccio** e **Supporto** sono i **dverger**, copie dei dverger del gioco
  (`Dverger`, `DvergerMage`…): sono **da combattimento**, per progetto.

**I dverger sono da combattimento.** La loro **ruota** mostra solo le azioni di combattimento e di supporto — **Segui**,
**Ripara e rifornisci**, **Congeda**, **Ordini**, **Raccogli**, **Gironzola**, **Imposta la casa** e le **quattro
posizioni** (Equilibrato/Corpo a corpo/A distanza/Passivo), come l'upstream. Combattono normalmente e continuano a
eseguire gli ordini di combattimento, movimento e raccolta.

Gli ordini di **lavoro** — raccogliere, estrarre, coltivare, pescare, cacciare, cucinare, fondere, riparare, rifornire —
vengono **rifiutati**, che arrivino dalla **chat**, dalla **voce** o dal **tasto di comando** (mirando un albero, una
roccia o una fornace). La risposta è sempre la stessa frase: **"Sono da combattimento, non faccio questo lavoro."** E
niente cambia: la **modalità attuale** e l'**obiettivo dell'ordine** restano come stavano.

Cambiare il **tipo** di un compagno che stava lavorando in un dverger **scarta anche l'obiettivo dell'ordine** — torna a
**seguirti**.

**Per i lavori, usa il tipo "Compagno".** Nei dverger il **pannello** nasconde gli slot del cibo per tipo e i pezzi
dell'armatura restano nella sacca invece di essere equipaggiati.

![Quattro compagni intorno al giocatore sulla piattaforma di partenza, i loro nomi nel mondo e l'elenco dei compagni con salute e stamina a destra](images/en-multiple-companions-2.jpg)

---

## Cosa viene dall'upstream e cosa è nostro

| Viene dall'upstream (Offline Companions Reborn, MIT) | È nostro |
|---|---|
| Il compagno: lavori, cibo, stamina, abilità, casa, riposo, portali, carro | **La traduzione in cinque lingue** — 914 chiavi di interfaccia e 221 frasi per lingua, con l'**inglese nativo** |
| L'IA di movimento e combattimento, i lavori, il menu radiale e la UI di base | Lo **strato di IA**: ordini via chat/voce, personalità, memoria, conversazione |
| Tutta la logica di gioco che gli ordini attivano | Il **TTS locale** (Kokoro) e il **STT locale** (Whisper) |
| | Il **pannello IA** (tasto `I`) |
| | Regolazioni: raggio di raccolta più grande, chatter spento, la patch del tasto `V` |
| | Il `.cfg` con un **nome proprio** (`AiCompanions.cfg`), la sezione `[AI]` e le chiavi in inglese |

![L'inventario del compagno aperto: la finestra "SOPHIA" con quello che porta e gli slot del Cibo, accanto al tuo inventario e al pannello di creazione](images/en-companions-inventory.jpg)

> L'IA **non** sostituisce niente: è solo un **nuovo ingresso** per le azioni che esistevano già.

---

## Documentazione tecnica

Il dettaglio di ogni fase (il perché di ogni decisione) è nel repository:

- Roadmap e fasi: https://github.com/julianu37/AI-Companions/blob/main/docs/roadmap.md
- Note di reverse engineering di Valheim 1.0: https://github.com/julianu37/AI-Companions/tree/main/docs/re
- Changelog: https://github.com/julianu37/AI-Companions/blob/main/package/CHANGELOG.md
