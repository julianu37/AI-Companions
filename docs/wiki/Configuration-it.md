> 🌍 [English](Configuration) · [Português](Configuration-pt) · [Español](Configuration-es) · [Français](Configuration-fr) · **Italiano**

# Configurazione

Dove si trova il file di configurazione, come modificarlo in gioco e cosa fa ogni opzione della sezione `[AI]`.

---

## Dove si trova il `.cfg`

```
<Valheim>\BepInEx\config\AiCompanions.cfg
```

Il file viene **generato da BepInEx** la prima volta che la mod si carica, e viene riscritto quando cambi qualcosa dai
pannelli. Ha circa **1540 righe** e **~265 opzioni** in totale, divise in varie sezioni (`[General]`, `[Gathering]`,
`[Hunting]`, `[Speech]`, `[Advanced]`, `[AI]` e altre).

> Il nome del file **non è più** il GUID del plugin: fino alla **1.8.2** era
> `Maddhattr.OfflineCompanionsReborn.cfg` e **dalla 1.8.3** è **`AiCompanions.cfg`**. Il **GUID** del plugin resta
> `Maddhattr.OfflineCompanionsReborn` — è l'**identità** della mod nel gioco e sulla rete (è quello che lega mondo,
> salvataggio e compagni), ma **non** dà più il nome al file. Il `.cfg` vecchio **resta sul disco, ignorato**: può
> essere cancellato senza paura.

> **Vieni dalla 1.8.2 o precedente?** Le impostazioni **tornano ai valori predefiniti** (è stato il cambiamento della
> 1.8.3: il file nuovo nasce già con i valori predefiniti) — compresa la **`ApiKey`**, che va incollata di nuovo. **Il
> salvataggio, il mondo, i compagni, le case e l'inventario non sono toccati** (le chiavi `HC_*` e il GUID non sono
> cambiati). I valori del file vecchio **non** vengono migrati apposta: le chiavi hanno cambiato nome. La **1.9.x
> mantiene il file** e sposta solo i vecchi valori fissi di voce/lingua su `auto`.

La sezione **`[AI]`** ha **39 chiavi**. Ecco come appare nel file (in questo esempio, con l'**alternativa locale**
configurata: `127.0.0.1`, `qwen2.5-3b-instruct` e la chiave vuota — nel **predefinito** il `BaseUrl` è **Groq**, con il
modello `openai/gpt-oss-20b`, e la `ApiKey` è obbligatoria):

```ini
[AI]
Enabled = true
BaseUrl = http://127.0.0.1:1234/v1
Model = qwen2.5-3b-instruct
ApiKey =
Temperature = 0.1
```

> L'esempio sopra è l'**alternativa locale**; nel **predefinito** le prime quattro righe sono
> `Enabled = true`, `BaseUrl = https://api.groq.com/openai/v1`, `Model = openai/gpt-oss-20b` e
> `ApiKey = <la tua chiave>`. Fino alla **1.8.2** le chiavi e le descrizioni erano in portoghese (`Ligado`, `Modelo`…);
> **dalla 1.8.3** sono in **inglese** (`Enabled`, `Model`…), come nella tabella sotto.

---

## Modificare in gioco

Non devi aprire il `.cfg` nel Blocco note per il quotidiano:

| Pannello | Tasto | Cosa modifica |
|---|---|---|
| **Impostazioni della mod** | **`F8`** | **Tutte** le opzioni della mod, in gioco |
| **Pannello IA** | **`I`** | Lo strato di IA e **ogni compagno**: la **scheda IA** (evocatore, impostazioni del LLM, voce globale e push-to-talk) e **una scheda per compagno** (nome, tipo, aspetto, la sua voce e personalità) |

### Pannello IA (tasto `I`)

Il pannello si apre sulla **scheda IA** (senza bisogno di un compagno vicino) e ha, in alto, **una scheda per
compagno** — quando una di quelle schede è aperta, il titolo diventa **il suo nome**. Le schede scorrono in orizzontale
quando ci sono molti compagni. Tutto viene salvato **subito**: le opzioni che valgono per tutti restano nella **scheda
IA**, e quello che è di un compagno resta nella **sua scheda**.

#### Scheda IA

| Cosa | Come |
|---|---|
| **Evoca un nuovo compagno** | Scegli il **Tipo** e premi **Evoca**: nasce **gratis**, senza passare dal Trader, con il nome del tipo numerato ("Guerriero 2"). Il contatore sotto mostra quanti sono **nella scena**; il tetto è `[General] MaxCompanions` (**10** di default, contando **vivi + morti + persi**) |
| **Impostazioni del LLM** | Gli interruttori **Attivo**, **Uscita strutturata** e **Mostra la risposta nella chat**; i campi **URL del server**, **Modello**, **Chiave API** (**mascherata**) e **Prefisso**; gli slider **Temperatura**, **Timeout (s)**, **Cooldown (s)** e **Raggio del bersaglio (m)**; e i pulsanti **Applica** e **Prova la connessione** |
| **Scegli il tuo provider LLM** | Il menu a tendina proprio **sopra l'URL del server**: scegliere **Groq**, **LM Studio** o **OpenRouter** **riempie** l'URL e il modello (vedi la tabella sotto). **Personalizzato (URL manuale)** non tocca niente |
| **Volume** | Lo slider del TTS (`[AI] TtsVolume`). La **velocità della voce** resta **solo nel pannello `F8`** (`[AI] TtsSpeed`) — il pulsante **Ascolta** usa il valore da lì |
| **Tieni premuto per parlare** | Il tasto del push-to-talk (**`V`** di default), il pulsante **Cambia** per cambiarlo e l'interruttore **Ordini vocali** |
| **Il compagno parla / Leggi le risposte dell'IA / Leggi le frasi della mod / Conversazione libera** | Gli interruttori di voce e conversazione (`TtsEnabled`, `TtsSpeakReplies`, `TtsSpeakModLines`, `Conversation`) |
| **Memoria** | Quanti scambi recenti ricorda (0–12) |

> I campi di testo (URL, modello, chiave, prefisso) valgono solo dopo **Applica** — e al **messaggio successivo**,
> senza riavviare il gioco. La chiave non va mai nel log.

> **Tipi:** i **dverger** (**Mago**, **Mago del fuoco**, **Mago del ghiaccio** e **Supporto**) sono **da
> combattimento**: la ruota mostra solo le azioni di combattimento/supporto e gli ordini di **lavoro** (via **chat**,
> **voce** o **tasto di comando**) vengono rifiutati con *"Sono da combattimento, non faccio questo lavoro."*. Per
> lavorare, usa il tipo **Compagno**. Vale per il **Tipo** dell'evocatore e per **Cambia tipo** della scheda del
> compagno — vedi **[Come funziona](How-it-works-it#tipi-di-compagno)**.

**Selettore del provider** (il sottotitolo **"Scegli il tuo provider LLM"**, con lo stesso menu del **Tipo**
dell'evocatore) — si **apre già sul provider che usa il tuo `.cfg`** e, quando ne scegli uno, riempie i campi:

| Provider | URL | Modello |
|---|---|---|
| **Groq** (predefinito) | `https://api.groq.com/openai/v1` | `openai/gpt-oss-20b` |
| **LM Studio** | `http://127.0.0.1:1234/v1` | `qwen2.5-3b-instruct` |
| **OpenRouter** | `https://openrouter.ai/api/v1` | `openrouter/free` |
| **Personalizzato (URL manuale)** | *(non tocca niente)* | *(non tocca niente)* |

> Scegliere nel selettore **riempie solo i campi**: vale dopo **Applica**, come qualsiasi modifica lì. La **`ApiKey`
> non viene mai toccata** (né riempita né cancellata) — resta tua. E il selettore **non è un'impostazione nuova**:
> **deduce** il provider dalla stessa `[AI] BaseUrl`, quindi un URL di **Ollama** o di un server tuo appare come
> **Personalizzato (URL manuale)** e non viene sovrascritto niente — non esiste un secondo posto che possa divergere
> dal `.cfg`.

**Pulsante "Crea qui la tua chiave API gratuita"** — sulla riga della **Chiave API**, nella metà destra: **apre il
browser** sulla pagina delle chiavi del provider scelto (**Groq** → `console.groq.com/keys`; **OpenRouter** →
`openrouter.ai/keys`), così prendi la chiave senza uscire dal gioco. **Non appare** su **LM Studio** né su
**Personalizzato** (quelli non hanno chiave) e **non è un selettore**: cliccarlo non cambia nessuno stato dopo.

![Il pannello IA sulla scheda IA: il menu "Scegli il tuo provider LLM" su Groq, l'evocatore di compagni, l'URL del server, il modello openai/gpt-oss-20b, la chiave API mascherata, il pulsante "Crea qui la tua chiave API gratuita", Applica e Prova la connessione](images/en-ai-panel.jpg)

#### Scheda del compagno

| Cosa | Come |
|---|---|
| **Nome** | Il campo con il nome attuale + **Applica** (**Invio** applica anche). Il nome vale nell'HUD, nell'hover, nella ruota e negli ordini per nome. Un compagno **morto** non può essere rinominato |
| **Tipo** | Le frecce `<` `>` scelgono e **Cambia tipo (rinasce)** applica, **con conferma**: rinasce come il tipo nuovo e **resta tutto** (nome, inventario, equipaggiamento, aspetto, voce, personalità, casa, letto, abilità) — solo il **lavoro in corso** ricomincia |
| **Stato** | **Vivo** o **Morto** — e morire **non** libera un posto nel tetto |
| **Richiama** | Porta il compagno vicino a te: viene camminando e, se è troppo lontano, torna con il richiamo del gioco stesso |
| **Aspetto** | Apre l'**editor dell'aspetto** (subito sotto) |
| **Personalità** | La lista con **"Segui il predefinito ([AI])"** davanti e i preset (**Vichingo leale**, **Mago sarcastico**, **Sarcastico**, **Guida paziente**) — **scegliere salva già**. **Con le tue parole** è il campo libero, applicato dall'**Applica** accanto |
| **Voce** | La lista con **"Segui il predefinito ([AI])"** davanti e le **voci della lingua del gioco** — scegliere salva già e vale dalla **frase successiva**; **Ascolta** suona una frase di esempio subito. Una voce salvata per un'**altra** lingua non viene usata (suona la voce predefinita della lingua attiva) |

> La voce e la personalità scelte qui sono di **questo** compagno, salvate nel **suo ZDO**: viaggiano nella **lapide** e
> tornano al respawn. Chi è su "segui il predefinito" obbedisce a quello che c'è nella **scheda IA** — cambiare la
> globale raggiunge tutti quelli che non hanno una scelta propria. **Richiama** e **Aspetto** sono disattivati con il
> compagno morto.

![Il pannello IA sulla scheda di un compagno: le schede in alto (IA e una per compagno), il Nome e il Tipo con Cambia tipo (rinasce), lo Stato, Richiama e Aspetto, e Voce e personalità di questo compagno con la voce e il pulsante Ascolta](images/en-ai-panel-companions-editor.jpg)

**Editor dell'aspetto** (il pulsante **Aspetto**): genere, taglio di capelli, stile di barba, tono della pelle, tono e
sfumatura dei capelli, con un **manichino 3D** che cambia dal vivo mentre scegli. **Applica** scrive nel compagno
**senza respawn**; **Annulla** chiude senza cambiare niente.

![L'editor dell'aspetto: genere maschile/femminile, taglio di capelli, tono della pelle, tono e sfumatura dei capelli, con l'anteprima 3D del compagno e i pulsanti Annulla e Applica](images/en-ai-panel-companions-editor-2.jpg)

Il pannello si chiude con **`I`**, con **`Esc`** o sulla **X** — e `Esc` prima esce da un campo di testo e chiude una
lista aperta, poi il pannello. Il tasto **`I`** viene ignorato mentre scrivi (chat, console, ricerca del mercante, i
campi del pannello stesso) e con l'**inventario**, la **mappa**, il **menu**, il pannello **`F8`** o il selettore del
primo compagno aperti; e il pannello **si chiude da solo** quando muori o esci dal mondo.

> Quello che non è nel pannello `I` resta nel **`F8`**, che modifica **tutte** le sezioni del `.cfg` con gli stessi nomi
> di chiave — il suggerimento nel piè di pagina del pannello dice esattamente questo.

---

## Sezione `[AI]` — tutte le chiavi

Sono **39 chiavi**, tutte in **inglese** (la **lingua nativa** della mod dalla 1.8.3 — il `.cfg` e le descrizioni
comprese). I valori predefiniti sono quelli del codice sorgente: se non hai mai toccato niente, questo è quello che
vale.

> **`[AI]` è la sezione del `.cfg`; `[IA]` è l'etichetta dei log.** Le **righe di log** della mod iniziano sempre con
> `[IA]` (`[IA] Prompt:`, `[IA] Tempo:`, `[IA] Request #…`) — è il prefisso del messaggio in `LogOutput.log`, non un
> percorso di configurazione. Non cercare `[IA]` nel `.cfg`: lì la sezione è `[AI]`.

| Chiave | Predefinito | Cosa fa |
|---|---|---|
| `Enabled` | `true` | Accende gli ordini di testo. Spento = non viene inviato niente al modello |
| `BaseUrl` | `https://api.groq.com/openai/v1` | Endpoint del LLM. Il `/chat/completions` viene aggiunto da solo. Il predefinito è **Groq** (che chiede la `ApiKey`); le alternative sono **LM Studio** (`http://127.0.0.1:1234/v1`) o **Ollama** (`http://127.0.0.1:11434/v1`) per far girare tutto in locale, e **OpenRouter** (`https://openrouter.ai/api/v1`), che funziona ma ha il tier gratuito più lento. Vedi **[LLM (LM Studio, Groq o OpenRouter)](LLM-it)** |
| `Model` | `openai/gpt-oss-20b` | Nome del modello, come lo elenca il server. `openai/gpt-oss-20b` è il predefinito (Groq, tier gratuito). Alternative: `openrouter/free` su OpenRouter; su LM Studio usa il nome che mostra (per esempio `qwen2.5-3b-instruct`) |
| `ApiKey` | *(vuoto)* | Chiave API del server. **Obbligatoria su Groq** (la gratuita esce da `console.groq.com/keys`) e anche su OpenRouter (`openrouter.ai/keys`); vuota per LM Studio/Ollama locale |
| `Temperature` | `0.1` | Quanto improvvisa il modello. Tienila bassa perché lo stesso ordine dia sempre lo stesso JSON |
| `TimeoutSeconds` | `30` | Secondi di attesa per il modello. La richiesta gira in background |
| `Prefix` | *(vuoto)* | Vuoto = ogni messaggio della chat va al modello. Per esempio `/c` richiede il prefisso |
| `ShowReplyInChat` | `true` | Mostra nella chat la risposta grezza del modello (utile nei test) |
| `CooldownSeconds` | `3` | Tempo minimo tra due ordini |
| `TargetRange` | `40` | Fino a dove (m) cercare la creatura nominata in un ordine di attacco. Quella che stai mirando vale sempre |
| `Personality` | *(vuoto)* | La personalità **con le tue parole** (fino a 600 caratteri). Vince sul preset |
| `PersonalityPreset` | *(vuoto)* | Il valore nel `.cfg` è un id: `viking`, `mago`, `sarcastico`, `guia` o `custom`. Vuoto/sconosciuto = il predefinito (leale e pratico). Il pannello mostra il nome nella lingua del gioco |
| `Memory` | `4` | Quanti scambi recenti ricorda e manda insieme al messaggio successivo. `0` = senza memoria (tetto 12) |
| `Conversation` | `true` | Rispondere alla conversazione libera quando il messaggio non è un ordine |
| `StructuredOutput` | `true` | Chiede al server il formato JSON (`response_format`/`json_schema`). È quello che permette i modelli piccoli |
| `SttTest` | `false` | Test: esegue il riconoscimento Whisper da solo ~25 s dopo il caricamento e lo scrive nel log |
| `SttModel` | `whisper\ggml-small-q5_1.bin` | Modello Whisper (relativo alla cartella del plugin o percorso assoluto) |
| `SttWavTest` | `whisper\jfk.wav` | WAV che `modia_stt_teste` trascrive. **Il pacchetto non porta quel file** — puntalo a un WAV tuo |
| `VoiceEnabled` | `true` | Accende il push-to-talk |
| `VoiceKey` | `V` | Tasto del push-to-talk |
| `VoiceLanguage` | `auto` | Lingua del parlato (codice Whisper). `auto` segue la **lingua del gioco** (PT-BR, EN, ES, FR, IT); un codice esplicito vince sempre. Vuoto vale anche come `auto` |
| `VoiceMaxSeconds` | `10` | Tetto della registrazione, in secondi |
| `VoiceSilenceThreshold` | `0.01` | Quanto deve essere forte l'audio per contare come parlato |
| `VoiceInitialPrompt` | *(vuoto)* | Prompt iniziale di Whisper: orienta il riconoscimento verso le parole degli ordini. Vuoto = il **vocabolario degli ordini della lingua del gioco**, scritto dalla mod; quello che scrivi qui vince (`none` spegne l'orientamento) |
| `VoiceDevice` | `0` | Indice del microfono (il log stampa la lista) |
| `VoiceShowText` | `true` | Mostra nella chat/HUD quello che ha sentito il microfono |
| `PanelKey` | `I` | Tasto che apre il **pannello IA** |
| `TtsEnabled` | `true` | **Lui dice** le risposte (TTS locale, Kokoro) |
| `TtsVoice` | `0` | Quale voce: `0` = **automatica** (la voce predefinita della lingua del gioco); un id esplicito **di quella lingua** vince (`42` pf_dora, `43` pm_alex, `44` pm_santa in portoghese; `3` af_heart, `2` af_bella, `6` af_nicole, `14` am_fenrir, `16` am_michael, `18` am_puck in inglese; `28` ef_dora, `29` em_alex in spagnolo; `30` ff_siwis in francese; `35` if_sara, `36` im_nicola in italiano) |
| `TtsSpeed` | `1` | Velocità della voce |
| `TtsThreads` | `4` | Core della CPU per la sintesi |
| `TtsVolume` | `1` | Volume della voce (0 = muto) |
| `TtsModel` | `tts\kokoro\model.int8.onnx` | Modello Kokoro. Voci/token/espeak nella **stessa cartella** |
| `TtsLanguage` | `auto` | Lingua del testo parlato (codice Kokoro). `auto` segue la **lingua del gioco** (`pt-br`, `en-us`, `es`, `fr`, `it`); un codice esplicito vince sempre. **Mai vuota** — il modello richiede una lingua |
| `TtsCache` | `true` | Conserva le frasi già sintetizzate |
| `TtsSpeakReplies` | `true` | Dice la risposta del modello |
| `TtsSpeakModLines` | `true` | Dice la frase della mod che appare nella chat (vince in un ordine) |
| `TtsReverbZoneMix` | `0` | Quanto delle zone di riverbero del mondo entra nella voce. `0` = asciutta; `0.19` = uno "spazio" sottile |
| `EcoDiag` | `false` | Diagnostica audio: scrive nel log (una volta per sessione) l'`AudioListener`, le zone di riverbero e come sono configurate le sorgenti del gioco |

---

## Lingua

**Cinque lingue: portoghese del Brasile, inglese, spagnolo, francese e italiano** — e l'**inglese è la lingua nativa**
della mod:

- **La lingua del gioco decide tutto**: l'interfaccia (737 chiavi), le frasi del compagno (186 righe), i preset di
  personalità, il contratto del LLM e la **voce e il riconoscimento vocale**. Con il gioco in **portoghese**, i nomi
  delle opzioni del **`F8`** e le schede appaiono in **PT** (`Ligado`, `Modelo`, `VozTecla`…); con il gioco in
  **inglese**, appaiono i nomi nativi (`Enabled`, `Model`, `VoiceKey`…); spagnolo, francese e italiano hanno un file
  proprio. Anche il **pannello `I`** viene tradotto così. **Cambiare la lingua non richiede un riavvio**: i pannelli si
  ricostruiscono alla prossima apertura e l'HUD, le frasi e il TTS/STT seguono.
- **`[AI] VoiceLanguage`, `TtsLanguage` e `TtsVoice` sono automatici di default** (`auto`, `auto`, `0`): la mod li
  risolve dalla **lingua del gioco**. Voci per lingua (quella predefinita per prima): `pt-br` → `pf_dora`, `pm_alex`,
  `pm_santa`; `en-us` → `af_heart`, `af_bella`, `af_nicole`, `am_fenrir`, `am_michael`, `am_puck`; `es` → `ef_dora`,
  `em_alex`; `fr` → `ff_siwis`; `it` → `if_sara`, `im_nicola`.
- **Un valore esplicito vince sempre** — e l'`F8` e il pannello `I` mostrano il valore **effettivo**, per esempio
  `Automatico (it · if_sara)`.
- **Nel `.cfg`**, le **chiavi e le descrizioni sono in inglese, sempre** — **non** cambia con la lingua del gioco
  (vale lo stesso per `package/CONFIGURATION.md`). È per questo che il **nome della chiave è l'inglese**, anche con il
  gioco in italiano: la chiave è l'identificatore dell'opzione, non l'etichetta che leggi sullo schermo.
- **Le lingue nuove entrano con un file**, senza toccare il codice: `Translations/<Lingua>.json` (737 chiavi) porta i
  testi dell'interfaccia (compresi i nomi delle opzioni e delle schede dell'`F8`) e `Speech/<Lingua>.json` (186 righe)
  porta le **frasi del compagno** — lo stesso schema. **L'inglese non ha bisogno di un file**, perché le chiavi
  **sono** il testo.
- **Le lingue che il modello Kokoro non parla** (tedesco, russo, polacco, turco, coreano, svedese e ucraino) usano la
  **voce inglese** — l'interfaccia e le frasi possono essere tradotte; l'audio è in inglese.

---

## Peso e capacità di carico

Il limite di peso del compagno è **della mod**, non del gioco: il vanilla **non** impone un tetto a un `Humanoid` che
non è un giocatore. Due chiavi decidono il comportamento e **tutte e due sono il valore base** — gli oggetti con un
**bonus di carico** si sommano **sopra**:

| Sezione / chiave | Predefinito | Cosa fa |
|---|---|---|
| `[General] MaxCarryWeight` | `300` | Il tetto **base** di peso del compagno (50–2000). È il `x/300` del pannello e della barra dell'**HUD** e quello che fa scattare **`Encumbered`** (camminare lento). Il **Megingjord** (+150) si somma sopra: con il predefinito, il suo tetto effettivo diventa **450** |
| `[Gathering] OverweightThreshold` | `298` | Il peso **base** al quale il compagno di **raccolta** (legna, pietra, minerale, raccolta e l'orto) smette di prendere cose e torna a casa per scaricare (50–500). Con il **Megingjord** nella sacca torna solo intorno a **448**. **Caccia e pesca non usano questa chiave** (hanno le loro opzioni di sacca piena) |

> **Il bonus vale con l'oggetto in qualsiasi punto della sacca** — non serve equipaggiarlo nello slot di utilità (su un
> `Humanoid` che non è un giocatore non sopravvive nemmeno al ricaricamento). E vale **una volta sola**: due cinture
> uguali **non si sommano**, come il gioco tratta gli effetti dell'equipaggiamento. Il valore viene letto
> dall'**asset** (`SE_Stats.m_addMaxCarryWeight`), mai da un `150` fisso — una cintura modificata a +250 funziona
> uguale.
>
> Il tetto effettivo vale nell'**`Encumbered`**, nel `x/450` del **pannello del compagno** e della barra dell'**HUD**,
> nel rifiuto del **loot** e della **raccolta da terra**, nel **fabbro**, nell'**upkeep**, nel "sacca piena" di
> **pesca/caccia** e nell'avviso "sono pieno". Ogni cambio di bonus scrive **una riga** nel log (mai per frame):
> `[Carry] "Thorin": +150 de capacidade (Megingjord) - teto 450`.

---

## Cosa **noi** abbiamo cambiato rispetto all'upstream

Oltre alla traduzione e allo strato di IA, il progetto ha regolato alcuni valori predefiniti dell'upstream. Se vieni da
Offline Companions Reborn, questo è quello che è cambiato di valore:

| Sezione / chiave | Predefinito dell'upstream | Il nostro predefinito | Perché |
|---|---|---|---|
| `[Advanced] GatheringSearchRadius` | 30 | **50** | Seguendoti, il compagno può essere a 25 m e l'obiettivo a 25 m dall'altra parte — l'anello di ricerca deve coprire ~50 m |
| `[Gathering] FollowRange` | 10 | **25** | Lavora fino a 25 m da te, invece di starti attaccato |
| `[Gathering] ForageFollowRange` | 10 | **20** | La raccolta resta "quello che hai superato", ma con margine |
| `[Gathering] Range` | 40 | **60** | Portata della raccolta con **Segui spento** |
| `[Advanced] GatheringDropRadius` | 8 | **12** | Un albero può cadere più lontano di 8 m; gli oggetti restavano indietro |
| `[Looting] CollectRadius` | *(nuovo)* | **10** | Raggio dell'ordine "raccogli gli oggetti a terra" |
| `[General] MaxCompanions` | *(nuovo)* | **10** | Tetto di compagni per giocatore, contando **vivi + morti + persi** (morire non libera un posto) |
| `[Speech] Chatter` | `true` | **`false`** | Il chatter casuale (frasette ogni 15–30 s) veniva solo da un timer. Le frasi utili restano |
| `[Advanced] UiDiag` | *(nuovo)* | **`false`** | Diagnostica dell'UI (clic/strati): opt-in, zero costo da spenta |
| `[AI] EcoDiag` | *(nuovo)* | **`false`** | Diagnostica audio del compagno: opt-in |

> I cinque raggi di raccolta vengono migrati automaticamente **solo** se sono esattamente al vecchio valore. Un valore
> scelto a mano **non** viene mai sovrascritto.

Le due diagnostiche (`UiDiag` e `EcoDiag`) sono **opt-in**: spente non scrivono niente e non costano niente; accese
scrivono **una volta per sessione** e sono la prima cosa da guardare quando un clic o un suono sembra strano.

---

## Vedere tutto nel pannello `F8`

Il pannello **`F8`** mostra **tutte** le sezioni del `.cfg`, con gli stessi intervalli di valore. I **nomi** delle
opzioni e delle **schede** appaiono nella **lingua del gioco**: in portoghese leggi `Ligado`, `Modelo`, `VozTecla`…; in
inglese, i nomi nativi (`Enabled`, `Model`, `VoiceKey`…) — è la chiave stessa, perché l'inglese non ha bisogno di
traduzione. È il modo più veloce per controllare quello che vale adesso, senza uscire dal gioco.

![Il pannello F8 aperto sulla sezione [AI], con le opzioni dello strato di IA](images/en-f8-panel-advanced-configurations.jpg)

> La cattura viene da un build con le **chiavi in inglese** (`Conversation`, `StructuredOutput`, `VoiceEnabled`,
> `TtsVoice`…). Dalla **1.8.3** il `.cfg` è in inglese; con il gioco in **portoghese** il pannello continua a mostrare
> i nomi in PT.

> I pannelli della mod stanno **sopra l'HUD del gioco** nell'ordine di disegno — è quello che garantisce che un clic
> sul pannello arrivi al pannello, e non all'HUD.
