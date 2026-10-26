> 🌍 [English](Troubleshooting) · [Português](Troubleshooting-pt) · [Español](Troubleshooting-es) · **Français** · [Italiano](Troubleshooting-it)

# Problèmes courants

Symptôme → cause probable → quoi faire.

**Où regarder dans le log, toujours :** `<Valheim>\BepInEx\LogOutput.log`. Les lignes du mod commencent par `[IA]`,
`[STT]` et `[TTS]`. (`[IA]` est l'**étiquette du log** — la section du `.cfg` est **`[AI]`**, avec les clés en anglais.)

---

## Il ne répond à rien

| Où regarder | Quoi chercher |
|---|---|
| `LogOutput.log` | `[IA] Chat orders ready: enabled=…, endpoint=…, model=…` |

**Causes probables, dans l'ordre :**

1. **L'`ApiKey` est vide ou fausse.** Le mod arrive pointé sur **Groq**, qui **exige** la clé (voir « répond une erreur
   d'authentification » plus bas). Si tu utilises **LM Studio** en local, la clé reste vide et le `BaseUrl` doit être
   `http://127.0.0.1:1234/v1`.
2. **Le `BaseUrl` ou le `Model` sont faux.** Sur Groq ce sont `https://api.groq.com/openai/v1` et
   `openai/gpt-oss-20b`. Sur LM Studio local, le `Model` doit être l'**API Model Identifier** qu'il affiche, pas le nom
   du dossier. Voir **[LLM (Groq, LM Studio ou OpenRouter)](LLM-fr)**.
3. **Avec LM Studio local : le serveur n'est pas allumé.** Ouvre l'onglet **Developer** et vérifie l'état `Running` sur
   le port **1234**.
4. **`[AI] Enabled` est à `false`.** Active-le dans le panneau **`F8`**.
5. **`[AI] Prefix` est rempli.** Si tu mets `/c`, seuls les messages qui commencent par `/c` vont au modèle.
6. **Aucun compagnon n'est à côté / apte à recevoir l'ordre.** Il doit être invoqué.

> Avant tout, utilise **Tester la connexion** du panneau **`I`** : il dit, en secondes, si l'URL répond et si le modèle
> apparaît dans la liste. Rappelle-toi que la **clé** n'est validée que sur un ordre réel (le test confirme l'URL et la
> liste des modèles, pas l'authentification).

---

## Répond une erreur d'authentification (401 / « unauthorized »)

| Où regarder | Quoi chercher |
|---|---|
| `LogOutput.log` | `[IA]` avec le statut **401** (ou un message *unauthorized* / *invalid api key*) |
| Panneau **`I`** → **Tester la connexion** | Il **peut** répondre `Connexion OK` même avec la mauvaise clé — sur OpenRouter la liste des modèles est publique. Le 401 apparaît sur l'**ordre**, pas sur le test |

**Cause :** **Groq exige** l'`[AI] ApiKey` (et OpenRouter aussi), et elle est **vide**, **fausse** ou **supprimée** (par
exemple, après avoir copié un `.cfg` d'une autre machine, ou après une mise à jour **depuis la 1.8.2 ou avant** — c'est
la 1.8.3 qui a remis les réglages à zéro) — ou c'est la clé d'un **autre** service, collée avec une URL qui n'est pas
la sienne.

**Quoi faire :**

1. Crée ou copie la clé sur **https://console.groq.com/keys** (Groq) ou sur **https://openrouter.ai/keys**
   (OpenRouter).
2. Colle-la dans **`[AI] ApiKey`** — dans le panneau **`I`** (onglet **IA** → **Réglages du LLM**) ou directement dans
   le `.cfg`.
3. Clique **Tester la connexion** ; puis **Appliquer** (la configuration vaut au message suivant).
4. Si tu ne veux **pas** utiliser Groq, pointe le `BaseUrl` sur le serveur local (`http://127.0.0.1:1234/v1`), utilise
   le `Model` de LM Studio et **laisse l'`ApiKey` vide**.

> Clé fuitée ? Supprime-la sur **https://console.groq.com/keys** (Groq) ou sur
> **https://openrouter.ai/settings/keys** (OpenRouter) et crées-en une autre. L'`ApiKey` du `.cfg` ne doit pas être
> partagée ni commitée.

---

## Répond 429 (limite de requêtes ou de tokens)

| Où regarder | Quoi chercher |
|---|---|
| `LogOutput.log` | `[IA]` avec le statut **429** (ou un message *rate limit* / *too many requests*) |

**Cause :** le **tier gratuit** du service a été atteint. Sur **Groq**, avec `openai/gpt-oss-20b`, les limites sont
**30 requêtes par minute**, **1 000 par jour**, **8 000 tokens par minute** et **200 000 par jour** — et, comme chaque
ordre dépense ~1 800 tokens, le plafond pratique est de **~4 ordres par minute**. Sur **OpenRouter**, les modèles
`:free` ont **20 requêtes par minute** et un plafond journalier qui dépend de l'historique du compte.

**Quoi faire :**

1. **Attends quelques secondes** et renvoie l'ordre — la limite est par fenêtre de temps, donc elle revient toute
   seule.
2. Baisse la fréquence : `[AI] CooldownSeconds` (**3** par défaut) aide déjà ; augmente-le si tu envoies des ordres en
   rafale.
3. Réduis `[AI] Memory` (chaque échange retenu agrandit le prompt et, avec lui, les tokens de chaque ordre).
4. Pour un usage intensif, utilise **LM Studio/Ollama en local** — il n'y a pas de limite de requêtes ni de tokens.

> Le **429** ne casse rien : l'ordre est refusé avec un avertissement et le jeu continue. Ce n'est **pas** une erreur de
> clé — ça, c'est le **401**, plus haut.

---

## Met trop de temps à répondre

| Où regarder | Quoi chercher |
|---|---|
| `LogOutput.log` | `[IA] Tempo: … ms` — le mod avertit quand ça dépasse **5 s** |

**Causes probables :**

1. **Le `.cfg` est encore sur OpenRouter.** Le défaut actuel est **Groq** (`openai/gpt-oss-20b`), qui répond en une
   **fraction de seconde** dans notre mesure. Si ton `[AI] BaseUrl` est encore `https://openrouter.ai/api/v1`, tu es
   sur son routeur gratuit (`openrouter/free`), qui **varie** d'un ordre à l'autre et passe par la file du
   fournisseur. Passe à Groq (voir **[LLM (Groq, LM Studio ou OpenRouter)](LLM-fr)**) ou utilise LM Studio en local.
2. **Ne confonds pas la lenteur avec la limite de Groq.** Le **429** répond **tout de suite** (l'ordre est refusé avec
   un avertissement), pas lentement — si le symptôme est le délai, la cause est autre. Voir « répond 429 » plus haut
   pour la limite.
3. **Ton `.cfg` local a encore `localhost` dans le `BaseUrl`.** Dans le cas LM Studio, le défaut est
   `http://127.0.0.1:1234/v1` ; si le fichier vient d'une version plus ancienne, change-le. Sous Windows, `localhost`
   résout d'abord en IPv6 (`::1`) et LM Studio n'écoute qu'en IPv4 — chaque ordre paie ~**2,5 s** de péage.
4. **Le modèle est gros ou il est sur le CPU (local).** Un 3B (`qwen2.5-3b-instruct`) répond en ~0,35–0,4 s ;
   `google/gemma-4-e4b` prend ~3 s. Les modèles plus gros exigent pratiquement un GPU.
5. **`[AI] Memory` est élevé.** Chaque échange retenu agrandit le prompt que le modèle doit lire. Réduis-le.
6. **Le premier message de la session** inclut le chargement du modèle — sur LM Studio local il est naturellement plus
   lent, et sur les services distants le premier est aussi le plus froid d'habitude.

> Rappel : la requête tourne en **arrière-plan**. Même lente, elle **ne fige pas** le jeu.

---

## Répond mal ou ignore le nombre

**Cause probable :** le modèle n'a pas compris la ressource ou la quantité.

**Quoi faire :**

- Vérifie ce que le modèle a compris : `[AI] ShowReplyInChat` affiche la réponse brute dans le chat.
- Utilise **`qwen2.5-3b-instruct`** avec `[AI] StructuredOutput = true` — c'est la combinaison que le projet a mesurée
  comme fiable (le 3B est passé de 0/6 à **6/6** réponses valides avec la sortie structurée).
- Dans le défaut (**Groq**, `openai/gpt-oss-20b`) le modèle est toujours le même, donc la réponse est cohérente. Sur
  **OpenRouter** (alternative), `openrouter/free` change de modèle à chaque requête — la qualité varie aussi. Si
  l'ordre rate toujours de la même façon, choisis **un modèle précis** dans `[AI] Model` (les gratuits d'OpenRouter ont
  `:free` dans le nom) ou utilise LM Studio local, qui est toujours le même modèle.
- Rappelle-toi que **le nombre est un objectif compté dans son sac** : s'il en a déjà assez, il **ne part pas**
  ramasser, et c'est le comportement correct.
- Pour les ordres vocaux, une mauvaise reconnaissance arrive mal au modèle. Voir « le micro n'enregistre pas » plus
  bas.

---

## Répond dans la mauvaise langue

**Cause probable :** le jeu est dans une langue et le compagnon répond dans une autre, ou le modèle choisi est faible
dans ta langue.

**Quoi faire :**

- **Vérifie la langue du jeu.** Depuis la 1.9.0, le contrat envoyé au modèle est dans la **langue du jeu** — portugais
  du Brésil, anglais, espagnol, français et italien — et une langue hors de ces cinq retombe sur l'**anglais**. La voix
  et la reconnaissance vocale suivent la même règle automatiquement (`[AI] VoiceLanguage` / `TtsLanguage` / `TtsVoice`
  = `auto` / `auto` / `0`).
- Utilise **`qwen2.5-3b-instruct`** (bon en portugais) si tu es sur LM Studio ; sur **Groq** (défaut) et sur
  **OpenRouter**, un modèle plus gros écrit en général mieux que les petits.
- Ajuste la personnalité dans le panneau **`I`** (preset ou texte libre dans ta langue) — elle entre dans le prompt et
  tire la réponse vers cette langue. Les presets sont aussi traduits dans les cinq langues.
- Réduis `[AI] Temperature` (le `0.1` par défaut est déjà bas ; des valeurs hautes font divaguer le modèle).

---

## La voix a un écho (on dirait une grotte)

**Cause :** ce n'est pas un bug — Valheim a **une zone de reverb globale** de 1000 m sur l'objet `AudioListener`
lui-même, avec une queue de 4 s. La source audio du compagnon était sur le défaut d'Unity et prenait toute cette
queue.

**Quoi faire :** vérifie que `[AI] TtsReverbZoneMix` est à **`0`** (le défaut). Si tu veux un « espace » subtil dans la
voix, utilise `0.19`. Ça vaut pour la **phrase suivante**, sans redémarrer. Voir **[Voix](Voice-fr)**.

---

## La voix ne sort pas, ou sort trop faible

| Où regarder | Quoi chercher |
|---|---|
| `LogOutput.log` | Les lignes `[TTS]` (temps de chargement, synthèse, RTF) |

**Causes probables :**

1. **`[AI] TtsEnabled` est à `false`.** Active-le dans le panneau **`I`**.
2. **Le modèle Kokoro n'est pas en place.** Vérifie `tts\kokoro\` avec `model.int8.onnx`, `voices.bin`, `tokens.txt` et
   `espeak-ng-data\`.
3. **Le volume est bas.** La voix **ne passe pas par le mixeur du jeu** — elle **ignore les curseurs de volume de
   Valheim** et n'obéit **qu'à** `[AI] TtsVolume`. Ajuste cette clé (ou le curseur du panneau `I`).
4. **Test isolé :** dans la console (**`F5`**), lance `modia_tts_teste Bonjour, chef !`. Si ça marche là, le problème
   est la configuration ; sinon, c'est le modèle.

---

## Le micro n'enregistre pas

| Où regarder | Quoi chercher |
|---|---|
| `LogOutput.log` | La ligne `[STT] Push-to-talk:` : si c'est activé, la touche et la liste numérotée des micros |

**Causes probables :**

1. **Aucune fenêtre ne doit être ouverte.** La touche n'enregistre pas avec l'inventaire, la carte, le menu, le chat,
   la console ou les panneaux du mod ouverts.
2. **`[AI] VoiceEnabled` est à `false`.**
3. **Le mauvais micro est sélectionné.** Le log liste les appareils numérotés — mets le numéro dans
   `[AI] VoiceDevice`.
4. **Le seuil de silence est haut.** `[AI] VoiceSilenceThreshold` (`0.01` par défaut) coupe le début/la fin de
   l'enregistrement. Si ton micro est faible, baisse-le ; s'il capte trop de bruit, monte-le.
5. **La parole a été trop courte.** Le log dit que l'enregistrement était trop court — parle plus longtemps.
6. **La langue est fausse.** `[AI] VoiceLanguage` est `auto` par défaut (la **langue du jeu**) ; un code écrit à la
   main d'une autre langue (par exemple `pt` avec le jeu en anglais) dégrade beaucoup la reconnaissance. Depuis la
   1.9.0, la valeur automatique suit le jeu et ne demande pas de redémarrage.
7. **Test isolé :** `modia_stt_teste` dans la console (**`F5`**) lance Whisper sur un WAV, **sans** micro. Si ça
   transcrit, le modèle est bon et le problème est la capture.

> **Windows x64 est obligatoire** pour la voix : les natifs du **STT** (whisper.cpp) **et du TTS**
> (sherpa-onnx/onnxruntime) sont `win-x64`.

---

## La touche `V` active le ramassage auto du jeu

**Cause :** dans Valheim, `V` est la touche du *Toggle Auto-Pickup* par défaut.

**Quoi faire :** le mod bloque déjà ce toggle — mais **seulement** quand `[AI] VoiceEnabled` est activé **et** que la
liaison du ramassage auto du jeu pointe sur la **même** touche. Donc :

- **Réassigne le ramassage auto** dans l'écran des commandes de Valheim sur une autre touche (le mod arrête de bloquer
  et tu as les deux), **ou**
- change `[AI] VoiceKey` pour une autre touche dans le panneau **`I`**.

Si tu le réassignes, le mod réévalue le conflit tout de suite. Voir **[Voix](Voice-fr)**.

---

## Le panneau `I` ne s'ouvre pas

**Causes probables :**

1. **Une fenêtre est ouverte.** Le panneau ne s'ouvre pas avec l'inventaire, la carte, le menu, le panneau **`F8`** ou
   le champ de recherche du marchand ouverts. Ferme-les.
2. **La touche a été changée.** Vérifie `[AI] PanelKey` (`I` par défaut).
3. **Tu tapes dans un champ de texte.** Dans ce cas la touche est ignorée exprès — le mod ne vole pas la touche du
   chat/console.

---

## Le panneau `F8` ne clique pas / un clic est avalé

**Cause (déjà corrigée) :** les panneaux du mod étaient sur la même couche de dessin que le HUD du jeu, et le HUD **est
à `sortingOrder = 400`**. Sur une égalité de couche, uGUI tranche par l'ordre d'enregistrement des raycasters — et
celui du jeu gagne. Il y avait aussi des éléments **décoratifs** du panneau qui recevaient des clics sans être des
contrôles.

**Quoi faire :**

- Mets à jour vers la version avec le correctif (les canvas du mod sont maintenant dans la plage **1701+**, **au-dessus**
  du HUD).
- Si le problème revient, active `[Advanced] UiDiag = true` : la prochaine fois que les panneaux s'ouvrent, le log écrit
  `[UiDiag]` avec les couches de la scène et qui est devant les contrôles. Désactive-le ensuite.

> Le même ajustement de couche a corrigé le texte du **centre du menu radial** mélangé avec le `crosshair` du jeu.

---

## J'ai donné le Megingjord au compagnon et le poids n'a pas changé

| Où regarder | Quoi chercher |
|---|---|
| Panneau **`I`** (onglet du compagnon) et barre du **HUD** | Le poids est toujours `…/300` (ou la valeur que tu as mise dans `[General] MaxCarryWeight`) même avec la ceinture dans le sac |
| Récolte | Il rentre décharger à **298** au lieu d'autour de **448** |

**Cause :** dans les versions **antérieures à la 1.8.0**, le bonus de la ceinture n'était pas lu. La limite de poids du
compagnon est **du mod** (`[General] MaxCarryWeight`, 300 par défaut), pas du jeu, et le `+150` du Megingjord est un
*status effect* dont le seul lecteur en vanilla est le `Player`. Comme le compagnon n'est pas un joueur, la ceinture
ne faisait qu'occuper de la place et peser.

**Quoi faire :** **mets le mod à jour** (le correctif est arrivé en 1.8.1). Après ça, le `/450` apparaît en ~**0,5 s**
(avec le `300` par défaut), et cette ligne le confirme dans `LogOutput.log` :

```
[Carry] "Thorin": +150 de capacidade (Megingjord) - teto 450
```

> La ceinture **n'a pas besoin d'être équipée** — elle compte n'importe où dans le sac (sur un `Humanoid` qui n'est pas
> un joueur, la case d'utilité ne survit même pas au rechargement). Et **deux** ceintures **ne s'empilent pas** : le
> bonus compte une fois. Les détails des deux clés sont dans **[Configuration](Configuration-fr)**.

---

## Le jeu ne charge pas le mod

| Où regarder | Quoi chercher |
|---|---|
| `LogOutput.log` | `AI Companions v1.9.1 loading...` et `AI Companions loaded successfully! (N methods patched)` — le `v1.9.1` est la version que tu as installée |

**Causes probables :**

1. **Le dossier est au mauvais endroit.** Le chemin doit être `<Valheim>\BepInEx\plugins\AI-Companions\` — avec
   `Companions.dll` **dedans**.
2. **Il manque les DLL de dépendance.** `Whisper.net.dll`, `sherpa-onnx.dll` et les `System.*` doivent être à côté de
   `Companions.dll`.
3. **BepInEx n'est pas installé** (ou il est sur une version incompatible). Utilise **BepInExPack_Valheim 5.4.2351**.
4. **Version du jeu incompatible.** Le mod est pour **Valheim 1.0.x** (Unity 6).

> **L'erreur PlayFab / NullReferenceException au démarrage est normale** et elle **n'est pas** du mod. Ne t'en
> préoccupe pas.

---

## Mes réglages sont revenus aux valeurs par défaut (ou « le mod ne lit pas mon `.cfg` »)

**Cause :** c'est la **1.8.3** qui a changé le nom et la langue du fichier de configuration (c'est pour ça qu'un
fichier de la 1.8.2 ou d'avant n'a aucun effet). Le mod lit **`BepInEx\config\AiCompanions.cfg`** ;
`Maddhattr.OfflineCompanionsReborn.cfg` (des versions précédentes) **n'est plus lu** et reste sur le disque **sans
effet** — c'est pour ça que les options reviennent aux **valeurs par défaut**, y compris l'`ApiKey` vide. La section
de l'IA est aussi devenue **`[AI]`**, avec les clés en **anglais**.

**Quoi faire :**

1. Vérifie que le fichier existe : `<Valheim>\BepInEx\config\AiCompanions.cfg` (il est créé au premier démarrage).
2. Refais ce que tu avais à la main — le plus important c'est l'**`[AI] ApiKey`**. Le menu **`F8`** (ou le panneau `I`)
   montre tout en jeu, avec les noms dans la **langue du jeu**.
3. Supprime l'ancien `.cfg` pour ne pas te tromper : il **ne** recommence pas à être lu.
4. Vérifie dans le log la ligne `[Config] Maddhattr.OfflineCompanionsReborn.cfg was replaced by AiCompanions.cfg ...`.

> **La sauvegarde, le monde, les compagnons, les maisons et l'inventaire ne sont pas touchés** — le **GUID** du plugin
> et les clés `HC_*` n'ont pas changé. Seule la configuration du mod revient au défaut.
>
> **La 1.9.x ne remet rien à zéro.** Si ton fichier est déjà `AiCompanions.cfg` (1.8.3 ou plus récent), la mise à jour
> déplace seulement les anciennes valeurs fixes de voix/langue (`pt`, `pt-br`, `42` et le prompt en portugais) vers
> `auto` — ta clé et tes autres choix restent.

---

## Toujours pas résolu ?

Rassemble ces informations et ouvre une issue sur
https://github.com/julianu37/AI-Companions/issues :

- L'extrait du `LogOutput.log` autour du problème (les lignes `[IA]`, `[STT]` ou `[TTS]`).
- La version du mod (elle apparaît au démarrage du log) et la version de Valheim.
- Ce que tu as fait (chat ou voix) et ce qui s'est passé.

<!-- D4: add Español/Français/Italiano -->
