> 🌍 [English](How-it-works) · [Português](How-it-works-pt) · [Español](How-it-works-es) · **Français** · [Italiano](How-it-works-it)

# Comment ça marche

L'architecture du mod et le chemin que prend un ordre, de ton micro (ou du chat) jusqu'au compagnon qui agit.

---

## Vue d'ensemble

![Comment un ordre circule : la voix (micro, 16 kHz) ou le chat du jeu vont dans la même boîte d'ordres ; le compagnon agit, le LLM comprend l'ordre et écrit la réponse, et la voix locale (Kokoro-82M) la dit à sa position](images/flow-ai.png)

Trois pièces d'IA, toutes sur des **threads de fond** (le LLM peut être distant ; le STT et le TTS sont toujours
locaux) :

| Pièce | Ce qu'elle est | Où elle tourne |
|---|---|---|
| **LLM** | Comprend l'ordre et discute | **LM Studio/Ollama** (local, recommandé) ou **Groq** (distant, ce qui vient dans le `.cfg`) — n'importe quel endpoint compatible OpenAI |
| **STT** | Transcrit ta parole | `Whisper.net` (whisper.cpp) dans le jeu |
| **TTS** | Fait parler le compagnon | `sherpa-onnx` + Kokoro-82M dans le jeu |

---

## Le chemin d'un ordre

1. **Entrée.** Tu écris dans le chat **ou** tu maintiens `V` et tu parles. Dans les deux cas le texte tombe dans le
   **même** point d'entrée — il n'y a pas de logique dupliquée entre chat et voix.
2. **Prompt.** Le mod assemble le prompt du message : le **contrat** fixe (dans la **langue du jeu**) + le **nom** du
   compagnon + la **personnalité** + la **mémoire** récente.
3. **LLM.** Le texte va au serveur configuré (`POST {BaseUrl}/chat/completions`) dans une **Task de fond** — le jeu
   n'attend jamais.
4. **JSON de l'intention.** Le modèle renvoie une intention structurée avec cinq champs :

```json
{ "action": "wood", "reply": "Je vais chercher du bois.", "target": null, "resource": "wood", "amount": 20 }
```

5. **Parser tolérant.** Le mod lit l'intention en acceptant des variations (`action`, `reply`, `target`, `resource`,
   `amount`) et **valide** la cible contre le monde réel — il n'invente jamais un objet ni une créature.
6. **Exécution.** L'intention devient la **même action que le menu radial** : la posture de combat, le travail,
   l'équipement, la cible… Tout passe par le même chemin que les contrôles normaux du mod.

> La file de résultats est **thread-safe** et est vidée dans `Update`. Rien ne bloque le thread principal.

---

## Sortie structurée (JSON schema)

La requête au LLM porte un **JSON schema** (`response_format`) qui fixe les cinq clés et la **liste fermée de
30 actions** — la même liste que le contrat, de `follow` à `none`.

C'est ce qui **débloque les petits modèles** : avec le schema, le serveur est obligé de répondre au bon format. Mesuré
avec le prompt actuel, `qwen2.5-3b-instruct` donnait **0/6** réponses en JSON et est passé à **6/6**.

Si le serveur **ne prend pas en charge** le format, le mod **retombe tout seul** dans une chaîne d'essais, un à la
fois :

1. `json_schema` (l'idéal)
2. `{"type":"json_object"}` (JSON libre)
3. **sans** `response_format` (l'ancien format)

Seul un **refus du format** déclenche l'essai suivant — un timeout, une erreur réseau ou une erreur serveur **ne sont
pas** réessayés. Une ligne dans le log dit quand la sortie structurée est active et **un seul avertissement**
apparaît si le mod atteint l'ancien format.

Clé : `[AI] StructuredOutput` (`true` par défaut).

---

## Personnalité, mémoire et conversation

- **Personnalité :** presets écrits dans le mod (**Viking loyal**, **Mage sarcastique**, **Sarcastique**,
  **Guide patient** ; les noms suivent la langue du jeu) ou **texte libre** du joueur — le texte libre **gagne** sur le
  preset. Il entre dans le prompt, donc le compagnon répond dans le ton que tu as choisi.
- **Mémoire :** les derniers `[AI] Memory` échanges **par compagnon** (0–12). La mémoire passe en **texte dans le
  system prompt**, jamais en messages en plus — le contrat JSON reste la dernière chose que le modèle lit. Elle est
  **oubliée** quand le compagnon meurt, est renvoyé ou que tu changes de monde, et **rien n'est enregistré dans le
  monde**.
- **Conversation libre :** quand l'action est `none` et qu'il y a un `reply`, le compagnon **parle** (bulle + voix) et
  le texte apparaît dans le chat. Dans un **ordre**, la phrase localisée du mod gagne, et le `reply` ne va que dans le
  log — rien n'est dit deux fois.

![Le panneau de l'IA sur l'onglet d'un compagnon : le nom, le type, l'état, Appeler et Apparence, et Voix et personnalité de ce compagnon avec la voix et le bouton Écouter](images/en-ai-panel-companions-editor.jpg)

---

## La voix du compagnon (TTS)

- La voix **suit la langue du jeu** : `[AI] TtsLanguage` et `TtsVoice` arrivent en `auto` / `0`, donc la voix par
  défaut de cette langue est utilisée sauf si tu en as choisi une. Changer la langue ne demande pas de redémarrer.
- Le modèle Kokoro est chargé **une fois**, en arrière-plan, quand tu entres dans le monde.
- La synthèse tourne sur un **thread de fond dédié** ; les morceaux prêts reviennent par une autre file et sont vidés
  dans `Update`.
- La parole est assemblée **phrase par phrase** : la première joue déjà pendant que la suivante est générée.
- Les **phrases fixes du mod** vont dans un **cache** (par octets, en écartant les plus anciennes) et sortent
  instantanément. Les réponses du modèle, qui sont nouvelles à chaque fois, **n'entrent pas** dans le cache.
- La lecture utilise **une** `AudioSource` **3D** à la position du compagnon — le son vient de là où il est, pas du
  milieu de l'écran.

La règle de **ce qui est dit** est simple : **la voix suit ce qui est écrit à l'écran**. Dans un ordre, la phrase du
mod ; dans une conversation, la réponse du modèle. Rien n'est dit deux fois.

---

## Ne pas figer le jeu

C'est une exigence de projet, pas un détail :

- Chaque appel réseau (LLM) tourne dans une **Task de fond**, avec un **timeout** configurable.
- Le STT et le TTS tournent sur des **threads de fond** avec des files thread-safe, vidées dans `Update`.
- Pas de `Find*` ni de LINQ par frame ; ce qui peut être mis en cache par frame l'est.
- Les `AudioClip` et les objets temporaires sont **détruits** à la fin.

---

## Confidentialité

**La voix et la transcription ne quittent jamais ta machine :**

- Ta parole est transcrite **dans le jeu** (Whisper, local).
- La voix du compagnon est synthétisée **dans le jeu** (Kokoro, local).
- Le **LLM** est le seul qui peut être distant : le `.cfg` arrive pointé sur **Groq** (ou **OpenRouter**, comme
  alternative), et là le texte des ordres et de la conversation **quitte ta machine**. Le chemin recommandé est
  **LM Studio/Ollama** en local, où rien ne sort (**[LLM (LM Studio, Groq ou OpenRouter)](LLM-fr)**).

L'`ApiKey`, quand il y en a une, reste seulement dans le `.cfg` local — ne la mets jamais dans un dépôt et ne partage
pas ce fichier avec la clé remplie.

---

## Types de compagnon

Le **type** d'un compagnon vient du **prefab** que le jeu utilise pour le créer, et c'est lui qui décide **ce que le
compagnon fait** :

- **Compagnon** (par défaut) est une copie du **personnage du joueur** — c'est ce « corps de joueur » que les travaux
  utilisent, et c'est le **seul type complet**.
- **Mage**, **Mage de feu**, **Mage de glace** et **Soutien** sont les **dverger**, des copies des dverger du jeu
  (`Dverger`, `DvergerMage`…) : ce sont des **combattants**, par conception.

**Les dverger sont des combattants.** Leur **roue** n'affiche que les actions de combat et de soutien — **Suivre**,
**Réparer et réapprovisionner**, **Renvoyer**, **Ordres**, **Ramasser**, **Flâner**, **Définir la maison** et les
**quatre postures** (Équilibré/Corps à corps/À distance/Passif), comme l'upstream. Ils se battent normalement et
continuent à répondre aux ordres de combat, de déplacement et de ramassage.

Les ordres de **travail** — récolter, miner, cultiver, pêcher, chasser, cuisiner, fondre, réparer, réapprovisionner —
sont **refusés**, qu'ils viennent du **chat**, de la **voix** ou de la **touche de commande** (en visant un arbre, une
roche ou un fourneau). La réponse est toujours la même phrase : **« Je suis un combattant, je ne fais pas ce
travail. »** Et rien ne change : le **mode actuel** et la **cible de l'ordre** restent comme ils étaient.

Changer le **type** d'un compagnon qui travaillait pour un dverger **abandonne aussi la cible de l'ordre** — il se
remet à te **suivre**.

**Pour les travaux, utilise le type « Compagnon ».** Sur les dverger, le **panneau** cache les cases de nourriture par
type et les pièces d'armure restent dans le sac au lieu d'être équipées.

![Quatre compagnons autour du joueur sur la plateforme de départ, leurs noms dans le monde et la liste des compagnons avec santé et stamina à droite](images/en-multiple-companions-2.jpg)

---

## Ce qui vient de l'upstream et ce qui est à nous

| Vient de l'upstream (Offline Companions Reborn, MIT) | Est à nous |
|---|---|
| Le compagnon : travaux, nourriture, stamina, compétences, maison, repos, portails, charrette | **La traduction en cinq langues** — 914 clés d'interface et 221 phrases par langue, avec l'**anglais natif** |
| L'IA de déplacement et de combat, les travaux, le menu radial et l'UI de base | La **couche d'IA** : ordres par chat/voix, personnalité, mémoire, conversation |
| Toute la logique de jeu que les ordres déclenchent | Le **TTS local** (Kokoro) et le **STT local** (Whisper) |
| | Le **panneau de l'IA** (touche `I`) |
| | Ajustements : rayon de ramassage plus grand, chatter désactivé, le patch de la touche `V` |
| | Le `.cfg` avec un **nom à lui** (`AiCompanions.cfg`), la section `[AI]` et les clés en anglais |

![L'inventaire du compagnon ouvert : la fenêtre « SOPHIA » avec ce qu'elle porte et les cases de Nourriture, à côté de ton inventaire et du panneau d'artisanat](images/en-companions-inventory.jpg)

> L'IA ne **remplace** rien : c'est seulement une **nouvelle entrée** pour les actions qui existaient déjà.

---

## Documentation technique

Le détail de chaque phase (le pourquoi de chaque décision) est dans le dépôt :

- Roadmap et phases : https://github.com/julianu37/AI-Companions/blob/main/docs/roadmap.md
- Notes d'ingénierie inverse de Valheim 1.0 : https://github.com/julianu37/AI-Companions/tree/main/docs/re
- Changelog : https://github.com/julianu37/AI-Companions/blob/main/package/CHANGELOG.md
