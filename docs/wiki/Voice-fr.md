> 🌍 [English](Voice) · [Português](Voice-pt) · [Español](Voice-es) · **Français** · [Italiano](Voice-it)

# Voix

Le mod a **deux** parties de voix, et elles sont indépendantes :

- **STT** (*speech to text*) — **tu** parles et le mod comprend : maintiens **`V`**, parle et relâche.
- **TTS** (*text to speech*) — le **compagnon** parle : sa réponse sort à voix haute, à sa position dans le monde,
  dans la **langue du jeu**.

Les deux tournent **dans le jeu**, sur ta machine, sans service externe — et **les deux suivent la langue du jeu** :
les cinq langues sont portugais du Brésil, anglais, espagnol, français et italien, et `[AI] VoiceLanguage`,
`TtsLanguage` et `TtsVoice` sont **automatiques** (`auto`, `auto`, `0`). **Changer la langue du jeu ne demande pas de
redémarrer** — le modèle est chargé une fois et la phrase suivante sort déjà dans la nouvelle langue.

---

## Tu parles : le STT (Whisper)

### Comment ça marche

1. Tu **maintiens `V`** et tu parles.
2. Le mod enregistre le micro en **16 kHz mono** et coupe le silence au début et à la fin.
3. Au relâchement, l'audio est transcrit **dans le jeu** par **Whisper.net** (whisper.cpp in-process, sur le CPU) —
   pas de HTTP, pas de processus enfant, pas d'internet.
4. Le texte reconnu entre dans le **même** chemin que les ordres écrits : ordre, conversation, personnalité et mémoire
   fonctionnent pareil. Tu vois `Entendu : "…"` dans le chat.

> La touche **`V`** n'enregistre qu'avec le jeu au premier plan et **sans** aucune fenêtre ouverte (inventaire, carte,
> menu, chat, console ou les panneaux du mod).

### Modèles

| Fichier | Taille | Note |
|---|---|---|
| `whisper\ggml-small-q5_1.bin` | 181 Mo | **Par défaut.** Bien plus précis que le `base`, dans toutes les langues |
| `whisper\ggml-base-q5_1.bin` | 57 Mo | Plus rapide et moins précis — **pas dans le paquet** : télécharge-le et change-le dans `[AI] SttModel` |

Où le télécharger : https://huggingface.co/ggerganov/whisper.cpp/tree/main

Mesure du projet (CPU 16 cœurs, 8 threads, clip de 2 s) : **~638 ms** avec `base` contre **~2187 ms** avec `small`.
Le temps change à peine avec la taille du clip, parce que l'encodeur travaille toujours sur une fenêtre de 30 s.

### Réglages du STT

| Clé `[AI]` | Défaut | Ce qu'elle fait |
|---|---|---|
| `VoiceEnabled` | `true` | Active le push-to-talk. Désactivé = la touche ne fait rien (et redevient ce que le jeu en faisait) |
| `VoiceKey` | `V` | Touche du push-to-talk |
| `VoiceLanguage` | `auto` | Langue de la parole (code Whisper). `auto` suit la **langue du jeu** (PT-BR, EN, ES, FR, IT) — le modèle est multilingue et peut aussi le détecter. Un code explicite gagne toujours |
| `VoiceMaxSeconds` | `10` | Plafond de l'enregistrement, en secondes |
| `VoiceSilenceThreshold` | `0.01` | À quel point l'audio doit être fort pour compter comme de la parole, au moment de couper le silence. Monte-le si ton micro capte trop de bruit |
| `VoiceInitialPrompt` | *(vide)* | Vide = le **vocabulaire des ordres de la langue du jeu**, écrit par le mod ; il oriente la reconnaissance vers les mots des ordres. Mets les mots que **tu** dis (`none` coupe le biais) |
| `VoiceDevice` | `0` | Quel micro utiliser (index de la liste que le log imprime) |
| `VoiceShowText` | `true` | Affiche dans le chat/HUD ce que le micro a entendu |
| `SttModel` | `whisper\ggml-small-q5_1.bin` | Modèle Whisper (relatif au dossier du plugin ou chemin absolu) |

> **Micros :** au démarrage, le mod écrit la liste des micros disponibles dans le log — en commençant par
> `[STT] Push-to-talk:` puis le nombre d'appareils avec leur index et leur nom. Utilise ce nombre dans `VoiceDevice`.

### La touche `V` et le ramassage auto du jeu

Dans Valheim, **`V`** est la touche du *Toggle Auto-Pickup* par défaut. Pour ne pas voler la touche du jeu, le mod a un
patch qui **bloque le ramassage auto** — mais **seulement** quand les deux conditions sont vraies en même temps :

- `[AI] VoiceEnabled` est **activé**, **et**
- la liaison du ramassage auto du jeu pointe sur la **même** touche (`VoiceKey`).

Si tu **réassignes** le ramassage auto dans l'écran des commandes de Valheim, le mod **ne bloque rien** — le jeu reste
intact et la touche `V` reste libre pour parler. Tu peux aussi simplement changer `VoiceKey`.

---

## Il parle : le TTS (Kokoro)

### Comment ça marche

- La voix est synthétisée **dans le jeu** par **Kokoro-82M** (via `sherpa-onnx`, sur le CPU) — pas de service externe,
  pas de processus enfant, rien ne quitte ta machine.
- La parole sort **à la position du compagnon** dans le monde (son 3D), pas au milieu de l'écran.
- La synthèse tourne sur un **thread de fond** et la parole est assemblée **phrase par phrase** : la première joue
  déjà pendant que la suivante est générée. Les phrases répétées viennent du **cache**, instantanément.
- Le modèle est chargé **une fois**, en arrière-plan, quand tu entres dans le monde.

### Voix par langue

`[AI] TtsVoice = 0` (le défaut) est **automatique** : la voix par défaut de la langue du jeu. Chaque langue a sa liste
(celle par défaut en premier), et un id explicite **de cette langue** gagne.

| Langue | `TtsLanguage` | Id par défaut | Voix (celle par défaut en premier) |
|---|---|---|---|
| Portugais (Brésil) | `pt-br` | **42** | `pf_dora` (F), `pm_alex` (M), `pm_santa` (M) |
| Anglais | `en-us` | **3** | `af_heart` (F), `af_bella` (F), `af_nicole` (F), `am_fenrir` (M), `am_michael` (M), `am_puck` (M) |
| Espagnol | `es` | **28** | `ef_dora` (F), `em_alex` (M) |
| Français | `fr` | **30** | `ff_siwis` (F) |
| Italien | `it` | **35** | `if_sara` (F), `im_nicola` (M) |

> **Une voix d'une autre langue n'est pas utilisée** (elle parlerait avec le mauvais accent) : le compagnon retombe
> sur la voix par défaut de la langue active, et le panneau `I` dit laquelle est en cours. Le choix est gardé dans le
> compagnon, donc remettre le jeu dans cette langue le restaure.
>
> **Les langues que le modèle Kokoro ne parle pas** — allemand, russe, polonais, turc, coréen, suédois et ukrainien —
> utilisent la **voix anglaise** : un compagnon qui parle avec une voix anglaise est mieux qu'un muet.

### Ce qu'il dit

La règle est : **la voix suit ce qui est écrit à l'écran.**

| Clé `[AI]` | Défaut | Ce qu'elle fait |
|---|---|---|
| `TtsEnabled` | `true` | Il **dit** les réponses. Désactivé = seulement la bulle et le chat |
| `TtsVoice` | `0` | Quelle voix : `0` = **automatique** (la voix par défaut de la langue du jeu) ; un id explicite de cette langue gagne |
| `TtsVolume` | `1` | Volume de la voix (0 = muet, 1 = volume normal) |
| `TtsSpeed` | `1` | Vitesse (au-dessus de 1 parle plus vite et plus court) |
| `TtsThreads` | `4` | Cœurs du CPU pour la synthèse (4 est déjà plus rapide que le temps réel) |
| `TtsModel` | `tts\kokoro\model.int8.onnx` | Modèle Kokoro (relatif au dossier du plugin ou chemin absolu) |
| `TtsLanguage` | `auto` | Langue du texte parlé. `auto` suit la langue du jeu (`pt-br`, `en-us`, `es`, `fr`, `it`) et un code explicite gagne — **ne la laisse jamais vide**, parce que Kokoro exige une langue (`pt` seul est le portugais du Portugal) |
| `TtsCache` | `true` | Garde les phrases déjà synthétisées (les répétées sortent instantanément) |
| `TtsSpeakReplies` | `true` | Dit la **réponse du modèle** (à sa façon : *« Laisse-moi faire. »*) |
| `TtsSpeakModLines` | `true` | Dit la **phrase du mod** qui apparaît dans le chat (*« Ok : récolter du bois (10). »*) |
| `TtsReverbZoneMix` | `0` | Quelle part des zones de reverb du monde entre dans la voix. `0` = voix sèche |

- Dans un **ordre**, la phrase du mod **gagne** (la voix suit ce qui est écrit à l'écran). Désactive
  `TtsSpeakModLines` pour entendre la réponse du modèle à la place.
- Dans une **conversation**, la réponse du modèle **est** la parole.
- **Rien n'est dit deux fois :** avec les deux options activées, la phrase qui n'est pas dite va seulement dans le log.

### Modèles

| Où | Fichiers |
|---|---|
| `tts\kokoro\` | `model.int8.onnx` (109 Mo), `voices.bin` (27 Mo), `tokens.txt` et le dossier `espeak-ng-data\` |

Les voix, les tokens et l'`espeak-ng-data` sont cherchés dans le **même dossier** que le modèle.

Où le télécharger : les releases de https://github.com/k2-fsa/sherpa-onnx (paquet du modèle
**`kokoro-int8-multi-lang`**).

### L'écho de la voix (et le correctif)

Si tu entends la parole du compagnon **résonner comme dans une grotte**, ce n'est pas un bug du mod : Valheim a **une
zone de reverb globale** — une `AudioReverbZone` de **1000 m** de rayon sur l'objet `AudioListener` lui-même, avec
**4 s** de queue. Tout ce qui était à moins de 1 km recevait cette queue, et notre source audio était sur le défaut
d'Unity.

Le correctif est la clé **`[AI] TtsReverbZoneMix`**, qui arrive déjà à **`0`** (voix sèche, comme la musique du jeu).
Si tu veux un « espace » subtil dans la voix, utilise **`0.19`** — la même valeur que l'ambiance du jeu. Ça vaut pour
la **phrase suivante**, sans redémarrer.

---

## Tester depuis la console du jeu

Ouvre la console avec **`F5`** :

| Commande | Ce qu'elle fait |
|---|---|
| `modia_tts_teste` | Dit une phrase de test par le TTS (pipeline complet) |
| `modia_tts_teste <texte>` | Dit le texte que tu écris — p. ex. `modia_tts_teste Bonjour, chef !` |
| `modia_stt_teste` | Lance la reconnaissance Whisper sur un WAV, **sans** utiliser le micro |

> `modia_stt_teste` transcrit le fichier défini dans `[AI] SttWavTest` (par défaut `whisper\jfk.wav`) et écrit le
> résultat dans le log. C'est le bon test pour savoir si le modèle Whisper se charge — utile quand les ordres vocaux
> ne sortent pas.
>
> **Le paquet n'apporte pas ce WAV** (il est sorti du paquet pour économiser de la place) : mets un fichier à toi dans
> `...\AI-Companions\whisper\` et pointe `[AI] SttWavTest` dessus (chemin relatif au dossier du plugin ou absolu). La
> commande **ne prend pas** le chemin en argument — seulement la clé. Une courte parole dans ta langue, WAV PCM 16
> bits, est ce qu'elle attend.

Le log montre le temps de synthèse de chaque phrase et le **RTF** (fraction du temps réel : en dessous de 1, c'est
plus rapide que le temps réel).

---

## Limitation connue : volume du jeu

La voix du compagnon **ne passe pas par le mixeur du jeu**. Cela veut dire qu'elle **ignore les curseurs de volume de
Valheim** et n'obéit **qu'à** `[AI] TtsVolume`. Si la voix est trop forte ou trop faible par rapport au jeu, règle-la
avec cette clé (ou avec le curseur de volume du panneau **`I`**).

<!-- D4: add Español/Français/Italiano -->
