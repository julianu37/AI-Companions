> 🌍 [English](Home) · [Português](Home-pt) · [Español](Home-es) · [Français](Home-fr) · **Italiano**

# AI Companions

Un **compagno PNG con IA** per **Valheim 1.0**: gli **parli o scrivi** e lui capisce e agisce.

> **Tipi di compagno: i dverger sono da combattimento**
>
> **Mago**, **Mago del fuoco**, **Mago del ghiaccio** e **Supporto** sono **da combattimento**: combattono
> normalmente e la ruota mostra solo le azioni di combattimento/supporto — gli ordini di **lavoro** via chat, voce o
> il tasto di comando sono **rifiutati** con *"Sono da combattimento, non faccio questo lavoro."*. Per lavorare, usa
> il tipo **Compagno** (dettagli in **[Come funziona](How-it-works-it#tipi-di-compagno)**).

## In azione

**1. Un ordine nella chat** — `Sophia, raccogli legna`: la sua risposta e il fumetto nel mondo.

![La chat con un ordine, la risposta del compagno e il fumetto](images/en-companions-chat-orders.jpg)

**2. Il tuo primo compagno, come vuoi tu** — genere, capelli, barba, tono della pelle, tono dei capelli, il nome e la
voce e la personalità di **quel** compagno; poi **Evoca un compagno**.

![Il pannello "Scegli il tuo compagno": genere maschile/femminile, taglio di capelli, stile di barba, tono della pelle, tono e sfumatura dei capelli, il nome, la voce del compagno con il pulsante Ascolta, la personalità e il pulsante Evoca un compagno, con l'anteprima 3D](images/en-first-run-companion-creation.jpg)

**3. Una squadra intera** — fino a **10** compagni, ognuno con la sua scheda nel pannello `I` e il suo nome
nell'elenco sullo schermo.

![Quattro compagni intorno al giocatore sulla piattaforma di partenza, i loro nomi nel mondo e l'elenco dei compagni con salute e stamina a destra](images/en-multiple-companions-1.jpg)

**4. Con te in viaggio** — ti seguono, combattono e portano quello che raccolgono.

![Tre compagni che camminano con il giocatore nei Prati, con le barre di salute e stamina di ognuno a destra](images/en-multiple-companions-2.jpg)

## Cos'è

Questo progetto è un **progetto indipendente** basato su [Offline Companions Reborn](https://github.com/MaddhattrRocks/Offline-Companions-Reborn)
(**MIT**) — che a sua volta è un fork di *Offline Companions*, di **ProfMags** — con due cose nostre sopra:

1. **Cinque lingue** — **portoghese del Brasile, inglese, spagnolo, francese e italiano**: l'interfaccia (914 chiavi),
   le frasi del compagno (221 righe), i preset di personalità e il contratto del LLM seguono la **lingua del gioco**,
   con l'**inglese come lingua nativa**, e una lingua nuova è **un file**.
2. **Uno strato di IA**: ordini in **linguaggio naturale** (testo nella chat o **voce** con push-to-talk), con
   **personalità**, **memoria** e **conversazione libera** — e la voce e il riconoscimento vocale **scelgono la lingua
   e la voce da soli**.

Il compagno in sé (lavori, cibo, stamina, abilità, casa, riposo, portali, carro, menu radiale) viene dall'upstream e
continua a funzionare uguale — l'IA è solo un **nuovo ingresso** per le stesse azioni.

> **La voce gira tutta sulla tua macchina.** Il riconoscimento vocale (STT) e la voce sintetizzata (TTS) sono
> **100% locali**: non viene inviato niente. Capire gli ordini usa un **LLM** — il percorso consigliato è il **LM
> Studio** (o Ollama) locale, dove **niente** esce; il `.cfg` arriva puntato su **Groq**, un **servizio remoto**
> (il testo degli ordini e della conversazione **esce dalla tua macchina**). Vedi
> **[LLM (LM Studio, Groq o OpenRouter)](LLM-it)**.

## Cosa fa

| Funzione | Come |
|---|---|
| **Ordini via chat** | `raccogli 10 legna` · `seguimi` · `fermati` · `attacca quel greydwarf` · `equipaggia l'ascia di bronzo` |
| **Ordini via voce** | Tieni premuto **`V`**, parla e rilascia — il parlato viene trascritto dentro il gioco |
| **Lui parla** | Voce sintetizzata **nella lingua del gioco**, che esce **dalla sua posizione** nel mondo |
| **Personalità** | Preset (**Vichingo leale**, **Mago sarcastico**, **Sarcastico**, **Guida paziente** — i nomi seguono la lingua del gioco) o le tue parole |
| **Memoria e conversazione** | `buongiorno`, `come stai?` — risponde come personaggio e ricorda la conversazione |
| **Più compagni** | Fino a **10**, ognuno con la sua scheda nel pannello `I`; l'evocatore ne crea uno nuovo **gratis** (senza il Trader) |
| **Ordine per nome** | `Gruncha, raccogli 5 pietre` vale **solo** per lui; senza il nome, l'ordine vale per tutti |
| **Aspetto** | Editor con **anteprima 3D**: genere, capelli, barba, pelle e tono dei capelli, senza rinascere |
| **Pannello IA** | Tasto **`I`**: una scheda per compagno (nome, tipo, Richiama, Aspetto, voce e personalità), l'evocatore, volume, push-to-talk e le **impostazioni del LLM** (con il **selettore del provider**) — tutto salvato subito |

## Requisiti in breve

| Elemento | Dettaglio |
|---|---|
| **Valheim 1.0.x** + **BepInEx 5.4.2351** | La base di tutto — passo passo in **[Installazione](Installation-it)** |
| **Un LLM** | **LM Studio** (consigliato): il modello gira sulla tua macchina e non esce niente — lo scarichi una volta (~1,9 GB) e tieni il server acceso. **Groq** (quello che arriva nel `.cfg`): solo la **chiave gratuita** (https://console.groq.com/keys) e internet — il testo degli ordini **esce dalla tua macchina**. **OpenRouter** funziona ancora come alternativa |
| **Microfono** | Solo per gli ordini vocali |

## Per iniziare

1. Guarda cosa ti serve in **[Requisiti](Requirements-it)**.
2. Segui il passo passo di **[Installazione](Installation-it)**.
3. Configura il LLM in **[LLM (LM Studio, Groq o OpenRouter)](LLM-it)** — il percorso consigliato è il **LM
   Studio** locale (senza chiave, niente esce); **Groq** è quello che arriva nel `.cfg` (solo la chiave
   gratuita) e **OpenRouter** è l'alternativa.
4. Guarda i tasti e gli ordini in **[Ordini](Commands-it)**.
5. Se qualcosa non funziona, vai in **[Problemi comuni](Troubleshooting-it)**.

## Indice del wiki

| Pagina | Cosa contiene |
|---|---|
| **[Requisiti](Requirements-it)** | Cosa è obbligatorio e cosa è facoltativo |
| **[Installazione](Installation-it)** | Passo passo con immagini |
| **[LLM (LM Studio, Groq o OpenRouter)](LLM-it)** | LM Studio come percorso consigliato, Groq come l'opzione cloud che arriva nel `.cfg` (chiave gratuita) e OpenRouter come alternativa |
| **[Voce](Voice-it)** | STT (Whisper) e TTS (Kokoro): come funzionano e come regolarli |
| **[Ordini](Commands-it)** | Tasti, ordini chat/voce e comandi della console |
| **[Configurazione](Configuration-it)** | Il `.cfg`, la sezione `[AI]` e i pannelli `F8` e `I` |
| **[Problemi comuni](Troubleshooting-it)** | Sintomo → causa → soluzione |
| **[Come funziona](How-it-works-it)** | Architettura e il percorso di un ordine |
| **[Cronologia delle versioni](https://github.com/julianu37/AI-Companions/blob/main/package/CHANGELOG.md)** | Il changelog completo, dalla 1.6.0 alla 1.9.1 |
| **[Crediti e licenza](Credits-and-license-it)** | Upstream, terzi e licenze |

## Link rapidi

| Cosa | Dove |
|---|---|
| **Release (download)** | https://github.com/julianu37/AI-Companions/releases |
| **Repository** | https://github.com/julianu37/AI-Companions |
| **Changelog** | https://github.com/julianu37/AI-Companions/blob/main/package/CHANGELOG.md |
| **Upstream (MIT)** | https://github.com/MaddhattrRocks/Offline-Companions-Reborn |

La release pubblicata più recente è la **1.9.1** (26/10/2026), con **due** file tra cui scegliere:

| File | Dimensione | Cosa contiene |
|---|---|---|
| `AI_Companions-1.9.1.zip` | ~314 MB | La mod **e** i modelli di voce (STT + TTS) già pronti |
| `AI_Companions-1.9.1-sem-modelos.zip` | ~28,5 MB | Solo la mod e le traduzioni — scarichi i modelli a parte |

> **Quale scegliere?** Se vuoi solo installare e giocare, prendi il **completo**. Se la tua connessione è lenta (o hai
> già i modelli), prendi il **senza modelli** e segui la sezione dei modelli in **[Installazione](Installation-it)**.

> **1.9.1:** il pacchetto è ~54 MB più leggero — il modello `ggml-base-q5_1.bin`, che non veniva usato, non è più incluso
> (resta un download facoltativo).

> **La 1.9.0 in una riga:** la mod parla **cinque lingue** (portoghese, inglese, spagnolo, francese e italiano)
> seguendo la **lingua del gioco**, e la **voce e il riconoscimento vocale sono automatici** — `[AI] TtsVoice = 0` e
> `TtsLanguage` / `VoiceLanguage` = `auto`, e un valore esplicito vince sempre. **Cambiare la lingua del gioco non
> richiede un riavvio.**

> **Vieni da una versione precedente?** La mod ora si chiama **AI Companions** (era *ModAI-BR-Companions*) e la
> cartella ora è `BepInEx\plugins\AI-Companions\`: **cancella la cartella vecchia**
> (`BepInEx\plugins\ModAI-BR-Companions\`) prima di estrarre — due copie dello stesso plugin fanno caricare la mod due
> volte a BepInEx. **Niente di tuo si perde**: il salvataggio, i compagni, la casa e l'inventario continuano a valere
> (il GUID del plugin e le chiavi `HC_*` sono stati conservati apposta).

> La **1.9.0** è la release delle **cinque lingue**: **portoghese, inglese, spagnolo, francese e italiano** —
> l'interfaccia, le frasi del compagno, i preset di personalità e il contratto del LLM seguono la **lingua del gioco**,
> e la voce e il riconoscimento vocale **scelgono la lingua da soli** (`[AI] VoiceLanguage`, `TtsLanguage` e `TtsVoice`
> = `auto` / `0`). Cambiare la lingua del gioco **non richiede un riavvio**: i pannelli si ricostruiscono alla
> prossima apertura e l'HUD, le frasi e il TTS/STT seguono.
>
> La **1.8.3** è la versione in cui l'**inglese è diventato la lingua nativa** (chiavi e descrizioni del `.cfg`) e il
> **portoghese è diventato una traduzione** — i pannelli (`I` e `F8`) seguono la **lingua del gioco**, e una lingua
> nuova entra con `Translations/<Lingua>.json` + `Speech/<Lingua>.json`.
>
> La **1.8.2** mette la scelta del LLM a un clic: nella sezione **Impostazioni del LLM** del pannello `I`, il menu a
> tendina **"Scegli il tuo provider LLM"** riempie l'**URL e il modello** di **Groq**, **LM Studio** o **OpenRouter**
> (la chiave resta con te), e il pulsante **"Crea qui la tua chiave API gratuita"** apre la pagina della chiave nel
> browser. La **velocità della voce** è rimasta **solo nel pannello `F8`**.
>
> La **1.8.1** porta **Groq come LLM predefinito** (gratis e veloce: la chiave esce da `console.groq.com/keys`), con
> **LM Studio/Ollama** come opzione locale e **OpenRouter** ancora disponibile — solo che il suo tier gratuito è più
> lento. E mantiene tutto quello che ha portato la **1.8.0**: **più compagni**, ognuno con la sua scheda nel pannello
> `I` (nome, tipo, aspetto, voce e personalità propri, ordine per nome e un tetto di 10), l'**editor dell'aspetto con
> anteprima 3D** e la sezione **Impostazioni del LLM** nel pannello `I`.
>
> La **1.8.0** ha corretto il **peso**: il **Megingjord** (+150) e gli altri oggetti con bonus di carico **si
> sommano** al tetto del compagno (il `/300` diventa `/450`), valendo con la cintura **in qualsiasi punto della
> sacca** — non serve equipaggiarla — e **una volta sola** (due cinture non si accumulano).
