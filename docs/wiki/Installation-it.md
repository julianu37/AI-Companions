> 🌍 [English](Installation) · [Português](Installation-pt) · [Español](Installation-es) · [Français](Installation-fr) · **Italiano**

# Installazione

Il passo passo completo, da zero al compagno che risponde a un ordine.

Prima di iniziare, controlla di avere tutto quello che c'è in **[Requisiti](Requirements-it)**.

---

## 1. Avere Valheim installato

Il gioco deve essere alla versione **1.0.x** (Unity 6). L'installazione predefinita di Steam è in:

```
C:\Program Files (x86)\Steam\steamapps\common\Valheim
```

Nei passi successivi, `<Valheim>` è quella cartella.

---

## 2. Installa BepInEx

La mod è un plugin di BepInEx 5. Installa **BepInExPack_Valheim 5.4.2351**:

1. Scaricalo da https://github.com/BepInEx/BepInEx/releases
2. Estrailo in `<Valheim>`, in modo che `winhttp.dll` e la cartella `BepInEx\` finiscano nella **radice** del gioco.
3. Avvia Valheim **una volta** e chiudilo. BepInEx crea `BepInEx\plugins\`, `BepInEx\config\` e
   `BepInEx\LogOutput.log`.

Documentazione ufficiale: https://docs.bepinex.dev/

---

## 3. Scarica la release della mod

Vai su **https://github.com/julianu37/AI-Companions/releases** e scarica **uno** dei due file:

| File | Dimensione | Quando sceglierlo |
|---|---|---|
| `AI_Companions-1.9.1.zip` | ~314 MB | **Consigliato.** Arriva già con i modelli di voce (STT + TTS). Installa e gioca |
| `AI_Companions-1.9.1-sem-modelos.zip` | ~28,5 MB | Connessione lenta, o hai già i modelli e vuoi scaricarli a parte |

> La differenza sono **solo** i modelli di voce. La mod e le traduzioni sono le stesse in entrambi.
>
> *(La **1.9.0** è la release delle **cinque lingue** — portoghese, inglese, spagnolo, francese e italiano —, con la
> voce e il riconoscimento vocale **automatici**, seguendo la **lingua del gioco**. La **1.8.3** è la versione in cui
> l'inglese è diventato la **lingua nativa**: il `.cfg` è passato a `AiCompanions.cfg` e la sezione dell'IA è passata
> a `[AI]`, con le chiavi in inglese. La **1.8.2** ha aggiunto un **selettore del provider del LLM** e il pulsante
> della **chiave API** nel pannello `I`. La **1.8.0** ha portato la correzione del peso — il **Megingjord** +150 ora
> si somma al tetto del compagno — e la **1.8.1** cambia il LLM predefinito in **Groq** (gratis e veloce), mantenendo
> LM Studio come opzione locale e OpenRouter come alternativa. La **1.7.0** è stata la release che ha rinominato la
> mod in **AI Companions**.)*

---

## 4. Estrai nella cartella di Valheim

Lo zip arriva già con la struttura delle cartelle pronta: dentro, la cartella è `BepInEx\plugins\AI-Companions\`.
Quindi devi solo estrarlo **nella radice di Valheim** (`<Valheim>`) e ogni file finisce al posto giusto. Se usi un
gestore di mod, punta lo zip su di lui — il gestore lo installa direttamente, senza rinominare niente.

> Se preferisci farlo a mano, il risultato finale deve essere
> `<Valheim>\BepInEx\plugins\AI-Companions\`, con `Companions.dll` **dentro**. BepInEx carica i plugin da
> **qualsiasi** sottocartella di `BepInEx\plugins\` — tenere quel nome evita solo confusione quando aggiorni.

> **Aggiorni da una versione precedente?** Cancella la cartella vecchia `BepInEx\plugins\ModAI-BR-Companions\`
> **prima** di estrarre. La mod ha cambiato nome (era *ModAI-BR-Companions*), ma il **GUID** e le chiavi `HC_*` sono
> gli stessi: il **salvataggio**, i compagni, la casa e l'inventario continuano a valere. Due cartelle con lo stesso
> plugin = BepInEx carica la mod due volte.

Alla fine, la cartella deve essere così:

![La cartella della mod in Explorer, con i file e le sottocartelle](images/pasta-do-mod.png)

```
<Valheim>\
└── BepInEx\
    └── plugins\
        └── AI-Companions\
            ├── LICENSES\
            ├── runtimes\
            ├── Speech\                     ← le frasi del compagno (5 lingue)
            ├── Translations\               ← i testi dell'interfaccia (5 lingue)
            ├── tts\                        ← il modello del TTS (Kokoro)
            ├── whisper\                    ← i modelli del STT (Whisper)
            ├── Companions.dll              ← la mod
            ├── Microsoft.Bcl.AsyncInterfaces.dll
            ├── sherpa-onnx.dll
            ├── System.Buffers.dll
            ├── System.Memory.dll
            ├── System.Numerics.Vectors.dll
            ├── System.Runtime.CompilerServices.Unsafe.dll
            ├── System.Threading.Tasks.Extensions.dll
            └── Whisper.net.dll
```

> Lo zip porta anche `README.md`, `CHANGELOG.md` e `icon.png` nella **radice** — è quello che il gestore di mod mostra
> nella scheda della mod.

> Quelle DLL `System.*` e le due della voce (`Whisper.net.dll`, `sherpa-onnx.dll`) devono restare **accanto a
> `Companions.dll`** — sono le dipendenze che la mod usa per il STT e il TTS.

---

## 5. Modelli di voce

**Se hai scaricato il pacchetto completo, questo passo è fatto** — i modelli sono già arrivati nelle cartelle
`whisper\` e `tts\`.

Se hai scaricato il **senza modelli**, metti i file così:

| Dove metterli | File |
|---|---|
| `...\AI-Companions\whisper\` | `ggml-small-q5_1.bin` (181 MB) — il predefinito del riconoscimento vocale |
| `...\AI-Companions\tts\kokoro\` | `model.int8.onnx` (109 MB), `voices.bin` (27 MB), `tokens.txt` e la cartella `espeak-ng-data\` |

> Nel pacchetto **senza modelli** `tokens.txt` e la cartella `espeak-ng-data\` **arrivano già** — mancano solo
> `ggml-small-q5_1.bin` (Whisper) e `model.int8.onnx` + `voices.bin` (Kokoro).

Dove scaricarli:

- **Whisper (ggml):** https://huggingface.co/ggerganov/whisper.cpp/tree/main — prendi `ggml-small-q5_1.bin`.
  `ggml-base-q5_1.bin` (57 MB) è più veloce e meno preciso e **non è più nel pacchetto**: scaricalo nella cartella
  `whisper\` e punta `[AI] SttModel` su di lui se vuoi il più veloce; vedi **[Voce](Voice-it)**.
- **Kokoro (sherpa-onnx):** le release di https://github.com/k2-fsa/sherpa-onnx — il pacchetto del modello
  `kokoro-int8-multi-lang` (che porta `model.int8.onnx`, `voices.bin`, `tokens.txt` e `espeak-ng-data`).

> Qualità, velocità e alternative sono descritte in **[Voce](Voice-it)**.

---

## 6. Configura il LLM

Gli ordini li capisce un **LLM**. Il percorso **consigliato** è il **LM Studio** locale: installalo da
https://lmstudio.ai, prendi `qwen2.5-3b-instruct` con **Model Search**, accendi il **Local Server** (porta **1234**)
e premi **Load Model** — il passo passo con le catture è in
**[LLM (LM Studio, Groq o OpenRouter)](LLM-it)**. Se preferisci non installare niente, il `.cfg` **arriva già
puntato su Groq**: crea la **chiave gratuita** su https://console.groq.com/keys e incollala in `[AI] ApiKey`.
**OpenRouter** funziona anche (chiave gratuita su https://openrouter.ai/keys) e resta l'alternativa.

> **Senza modificare il file:** sopra l'**URL del server** c'è il menu a tendina **"Scegli il tuo provider LLM"** —
> scegliere **Groq**, **LM Studio** o **OpenRouter** **riempie l'URL e il modello** (la chiave non viene toccata;
> vale dopo **Applica**). E, sulla riga della **Chiave API**, il pulsante **"Crea qui la tua chiave API gratuita"**
> apre la pagina della chiave nel browser (`console.groq.com/keys` su Groq). La velocità della voce, che è uscita da
> questo pannello, resta nel **`F8`**.

![Il pannello IA sulla scheda IA: in Impostazioni del LLM, il menu "Scegli il tuo provider LLM" già su Groq, l'URL del server con api.groq.com, il modello openai/gpt-oss-20b, la chiave API mascherata e il pulsante "Crea qui la tua chiave API gratuita" a destra della chiave](images/en-ai-panel.jpg)

> Questa cattura è della **1.8.2**: il **selettore del provider** è su **Groq** (quello che arriva nel `.cfg`),
> l'URL e il modello vengono da Groq e il pulsante della chiave appare a destra della **Chiave API**. Se scegli
> **LM Studio**, il pulsante sparisce (non c'è chiave).

> **Con Groq, il testo degli ordini esce dalla tua macchina** (è un servizio remoto) e il tier gratuito ha un
> limite di richieste e di **token al minuto** (~4 ordini al minuto con `gpt-oss-20b`). Il **LM Studio** locale non
> ha niente di tutto questo: è il percorso consigliato, e **niente** esce dalla macchina.

La guida completa dei tre percorsi (LM Studio → Groq → OpenRouter) è in
**[LLM (LM Studio, Groq o OpenRouter)](LLM-it)**.

---

## 7. Prima esecuzione e il log

1. Avvia Valheim.
2. Apri `<Valheim>\BepInEx\LogOutput.log`.
3. Cerca queste due righe:

```
AI Companions v1.9.1 loading...
AI Companions loaded successfully! (N methods patched)
```

Se compaiono, la mod si è caricata.

> Il `v1.9.1` è la **versione che hai installato** (il numero dello zip che hai scaricato) e la `N` è quanti metodi del
> gioco la mod ha regolato — quel numero può cambiare da una versione all'altra.

> Alla **prima esecuzione dopo un aggiornamento dalla 1.8.2 o precedente** il log mostra anche che il file di
> configurazione ha cambiato nome: `[Config] Maddhattr.OfflineCompanionsReborn.cfg was replaced by AiCompanions.cfg ...`
> — è l'avviso normale della 1.8.3, e il `.cfg` vecchio può essere cancellato.

> **È normale** vedere un errore **PlayFab / NullReferenceException** all'avvio — **non** è della mod e non blocca
> niente. Se vuoi conferma, vedi **[Problemi comuni](Troubleshooting-it)**.

---

## 8. Primo test in gioco

Con il mondo aperto e il compagno accanto:

1. **Con il testo:** apri la chat del gioco e scrivi `raccogli 10 legna`.
   Dovrebbe rispondere e andare a raccogliere. *(Il numero è un obiettivo contato nel suo inventario — se ha già 10 di
   legna, avvisa "Ne abbiamo già abbastanza.")*
2. **Con la voce:** tieni premuto **`V`**, di' `raccogli 10 pietre` e rilascia. Il testo riconosciuto appare nella chat.
3. **La sua voce:** dovrebbe **dire** la risposta ad alta voce, nella sua posizione nel mondo.
4. **Pannello IA:** premi **`I`** per regolare personalità, voce, volume e il tasto del push-to-talk.

Per vedere tutti i tasti e gli ordini, vai in **[Ordini](Commands-it)**.

---

## Lista di controllo

- [ ] Valheim 1.0.x installato
- [ ] BepInEx 5.4.2351 estratto nella radice del gioco
- [ ] Release **1.9.1** scaricata (il pacchetto completo o quello senza modelli)
- [ ] Cartella `BepInEx\plugins\AI-Companions\` creata con `Companions.dll` e le dipendenze
- [ ] Modelli di voce in `whisper\` e `tts\kokoro\`
- [ ] Configurazione **controllata** in `BepInEx\config\AiCompanions.cfg` (torna ai valori predefiniti solo se vieni
      dalla **1.8.2 o precedente**) e il `.cfg` vecchio cancellato
- [ ] Chiave gratuita di Groq creata e `[AI] ApiKey` compilata (o il LM Studio locale configurato)
- [ ] `[AI] BaseUrl` e `[AI] Model` controllati nel `.cfg`
- [ ] **Prova la connessione** (pannello `I`) che risponde `Connessione OK`
- [ ] Log con `AI Companions loaded successfully!`
- [ ] `raccogli 10 legna` nella chat funziona
- [ ] Tenere premuto `V` e parlare funziona

Se qualcosa non va, vedi **[Problemi comuni](Troubleshooting-it)**.
