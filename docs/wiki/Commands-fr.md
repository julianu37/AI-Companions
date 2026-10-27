> 🌍 [English](Commands) · [Português](Commands-pt) · [Español](Commands-es) · **Français** · [Italiano](Commands-it)

# Ordres

Touches, ordres par chat/voix et commandes de console.

---

## Touches

| Touche | Action |
|---|---|
| **`V`** | **Push-to-talk** : maintiens-la et parle ; relâche pour qu'il comprenne |
| **`Z`** | **Touche de commande** : regarde quelque chose et tape dessus pour envoyer les compagnons (bouger / attaquer / ouvrir une porte / s'asseoir / réparer). **Maintiens** pour les appeler |
| **`E`** | **Maintiens** en regardant un compagnon pour ouvrir le **menu radial** ; **tape** pour ouvrir son panneau / inventaire |
| **`LeftAlt` + `Z`** | Éditeur de zone de culture |
| **`I`** | **Panneau de l'IA** : un **onglet par compagnon** (nom, type, Appeler, Apparence, sa voix et sa personnalité) et l'**onglet IA** (invocateur de compagnons, réglages du LLM avec le **sélecteur de fournisseur**, volume, mémoire, conversation et push-to-talk) — tout enregistré tout de suite. La **vitesse de la parole** reste dans le **`F8`** |
| **`F8`** | Panneau des réglages du mod (toutes les options, en jeu) |
| **`F5`** | Console du jeu (pour les commandes de test du mod) |

> Le panneau de l'IA (touche **`I`**, modifiable dans `[AI] PanelKey`) s'ouvre **sans avoir besoin d'un compagnon à
> côté**, se ferme avec **`I`**, **`Esc`** ou la **X**, et chaque contrôle est enregistré tout de suite (dans le
> `.cfg`, quand c'est une option globale ; dans le compagnon, quand c'est de son onglet). Tant qu'il est ouvert, ton
> personnage ne répond pas aux contrôles, et la touche est ignorée avec l'inventaire, la carte, le menu ou le panneau
> `F8` ouverts.

> **Types :** les **dverger** (**Mage**, **Mage de feu**, **Mage de glace** et **Soutien**) sont des **combattants** —
> la roue n'affiche que les actions de combat/soutien et les ordres de **travail** (**chat**, **voix** ou la **touche
> de commande**) sont refusés avec *« Je suis un combattant, je ne fais pas ce travail. »*. Pour travailler, utilise le
> type **Compagnon** (**[Comment ça marche](How-it-works-fr#types-de-compagnon)**).

### Menu radial (maintiens `E`)

Maintiens **`E`** en regardant un compagnon pour ouvrir la roue d'actions (bouger, attaquer, ouvrir une porte,
s'asseoir, réparer…). Un **tap** sur `E` ouvre son panneau / inventaire.

![Le menu radial ouvert, avec les actions autour du centre](images/en-radial-menu.jpg)

---

## Ordres par chat et par voix

Tu écris dans le chat du jeu **ou** tu parles en maintenant **`V`**. L'ordre est interprété par un LLM : tu peux dire
la même chose de **plusieurs façons** — pas besoin de mémoriser la phrase exacte.

![Le chat avec un ordre, la réponse du compagnon et la bulle de dialogue](images/en-companions-chat-orders.jpg)

### Par le nom

Commence le message par le **nom** d'un compagnon pour parler **seulement** à lui : `Gruncha, ramasse 5 pierres` (le
prompt, la personnalité, la mémoire et l'ordre sont les siens). **Sans** le nom, l'ordre vaut pour **tous** ceux qui
peuvent l'exécuter. Un début qui ressemble à un nom et qui n'existe pas (`Tartempion, ramasse 10 pierres`) est
signalé — et rien n'est ordonné —, mais une forme d'adresse continue à valoir (`chef, ramasse 10 pierres`).

> Le nom est celui que tu as donné dans l'**onglet du compagnon** (panneau `I`) ou le nom du type, s'il n'a jamais été
> renommé.

### Suivre et arrêter

| Tu dis | Ce qu'il fait |
|---|---|
| `suis-moi` | Se remet à te suivre |
| `arrête` / `arrête de me suivre` | Arrête ce qu'il fait et reste immobile — et la réponse dit **le travail** qu'il a arrêté (« arrêter de récolter du bois », « arrêter de cuisiner ») |
| `viens ici` | Vient vers toi |

> Il n'y a pas de « suivre de près » séparé : les options sont **te suivre** ou **s'arrêter**.

### Ramasser

| Tu dis | Ce qu'il fait |
|---|---|
| `ramasse 10 bois` / `coupe du bois` | Ramasse du **bois** |
| `ramasse 10 pierres` / `prends 10 pierres` | Ramasse de la **pierre** |
| `mine du fer` / `mine du minerai` | **Mine** du minerai |
| `cueille les baies` / `ramasse les champignons` | **Cueille** (plantes, fruits, champignons) |

> **Le nombre est un objectif.** Il le compte **dans l'inventaire du compagnon** (son sac — il ne compte pas les
> coffres ni les piles au sol). S'il a déjà 10 bois et que tu demandes 10, il dit *« On en a déjà assez. »* et ne part
> pas ramasser. L'objectif ne vaut que **pour la ressource demandée**.

### Attaquer

| Tu dis | Ce qu'il fait |
|---|---|
| `attaque ce greydwarf` | Attaque la créature **nommée** (même en posture Passif) |
| `tue le sanglier` | Pareil |
| `attaque le troll` | Pareil |

> La créature est cherchée **d'abord dans ta mire** ; ensuite, la plus proche dans `[AI] TargetRange` (40 m par
> défaut). Celle que tu vises **compte toujours**, aussi loin qu'elle soit. *« Attaquer »* sans nommer de créature
> devient de la **chasse**.

### Équiper et lâcher

| Tu dis | Ce qu'il fait |
|---|---|
| `équipe la hache en bronze` / `prends la pioche` | Équipe l'objet **qu'il porte déjà** |
| `lâche la pierre` / `jette 5 bois` | Lâche l'objet au sol (sans nombre = toute la pile) |

> `equip` et `drop` ne valent que pour ce qu'il **a déjà dans son sac**. Un objet de quête ne tombe jamais.

### Objets au sol

| Tu dis | Ce qu'il fait |
|---|---|
| `ramasse les objets` / `ramasse le bois par terre` | Marche jusqu'aux objets au sol et les ramasse **un par un** |
| `prends ce qui est au sol` | Pareil — il prend ce qui est **le plus proche**, pas un objet choisi par le nom |
| `arrête de ramasser` / `ne ramasse rien` | **Désactive** le ramassage automatique |

> Le ramassage prend **même ce qu'il a lâché exprès** — ce que l'aimant automatique du jeu ignore justement. Il
> renonce si l'objet ne rentre pas entier dans son sac ou s'il est trop loin.

### Inventaire

| Tu dis | Ce qu'il fait |
|---|---|
| `ouvre ton inventaire` / `montre-moi ce que tu portes` | Ouvre **sa fenêtre d'inventaire** pour que tu voies |
| `ferme l'inventaire` | Ferme la fenêtre (la **X** de la fenêtre ferme toujours) |

> Fermer est une action à part (`inventory_close`) : si le modèle répond « ouvrir » pour une phrase avec un verbe de
> fermeture, le mod corrige par **ta** phrase. La **X** de la fenêtre et `Esc` ferment toujours.

![L'inventaire du compagnon ouvert : la fenêtre « SOPHIA » avec ce qu'elle porte et les quatre cases de Nourriture, à côté de ton inventaire et du panneau d'artisanat](images/en-companions-inventory.jpg)

### Posture de combat

| Tu dis | Ce qu'il fait |
|---|---|
| `reste en défense` | Posture **passive** (défensive) |
| `reste équilibré` | Posture **équilibrée** |
| `attaque à distance` | Posture **à distance** |
| `combats au corps à corps` | Posture **corps à corps** |

### Travaux

| Tu dis | Ce qu'il fait |
|---|---|
| `occupe-toi de la culture` | Travail de **culture** |
| `va pêcher` | Travail de **pêche** |
| `répare les constructions` | Travail de **réparation** |
| `va cuisiner` | Travail de **cuisine** |
| `fonds le minerai` | Travail de **fonte** |
| `va chasser` | Travail de **chasse** |
| `définis la maison ici` | Définit la **maison** |
| `flâne dans le coin` / `arrête de flâner` | Active/désactive le **flâner** |
| `accepte les ordres` / `ignore les ordres` | Active/désactive **accepter les ordres** |

### Conversation

| Tu dis | Ce qu'il fait |
|---|---|
| `bonjour` | Répond en personnage |
| `comment vas-tu ?` | Pareil |
| `que penses-tu de ma maison ?` | Pareil |

> Quand le message **n'est pas** un ordre, le compagnon **converse** : il dit la réponse à voix haute et l'affiche
> dans le chat. C'est contrôlé par `[AI] Conversation` (`true` par défaut). Désactivé = seuls les ordres sont compris.

---

## Commandes de console (`F5`)

| Commande | Ce qu'elle fait |
|---|---|
| `modia_tts_teste` | Dit une phrase de test par le TTS (pipeline complet : modèle, synthèse et lecture) |
| `modia_tts_teste <texte>` | Dit le texte que tu écris — p. ex. `modia_tts_teste Bonjour, chef !` |
| `modia_stt_teste` | Lance la reconnaissance Whisper sur un WAV, **sans** utiliser le micro |

> `modia_stt_teste` transcrit le fichier de `[AI] SttWavTest` (`whisper\jfk.wav` par défaut) et écrit le résultat dans
> le log. C'est là pour vérifier si le modèle Whisper se charge, quand les ordres vocaux ne sortent pas.
>
> **Le paquet n'apporte pas ce WAV** (il est sorti du paquet pour économiser de la place) — mets un fichier à toi dans
> le dossier `whisper\` et pointe `[AI] SttWavTest` dessus. La commande **ne prend pas** le chemin en argument.

---

## Notes importantes

- **La langue suit le jeu.** Depuis la 1.9.0, l'interface, les phrases, les presets de personnalité et le contrat du
  LLM sont en **cinq langues** (PT-BR, EN, ES, FR, IT) et suivent la **langue du jeu** — les ordres sont compris dans
  cette langue aussi, et la voix et la reconnaissance vocale sont **automatiques** (`auto` / `0`).
- **Le nombre est un objectif compté dans son sac.** `ramasse 10` = **avoir 10 dans l'inventaire du compagnon**. Avec
  15, il ne commence même pas et dit qu'il en a déjà assez ; avec 7, il ramasse jusqu'à 10 (le dernier arbre peut
  dépasser).
- **L'ordre est lu par le LLM**, donc tu peux parler à ta façon. `prends 10 pierres`, `je vais avoir besoin de pierre`,
  `ramasse des pierres` — toutes devraient tomber sur la même action.
- **`[AI] Prefix`** (vide par défaut) : si tu le remplis, par exemple avec `/c`, **seuls** les messages qui commencent
  par ce préfixe vont au modèle. Le reste du chat est laissé tranquille.
- **`[AI] CooldownSeconds`** (`3` par défaut) : temps minimum entre deux ordres, pour qu'une rafale de messages
  n'inonde pas le modèle.
- **Le chat et la voix entrent par le même chemin.** Ce qui marche écrit marche parlé, avec la même personnalité, la
  même mémoire et la même conversation.
- **`despawn` n'est pas exposé au modèle.** Tu ne peux pas ordonner au compagnon de disparaître par chat ou par voix.

---

## Ajustements de confort du mod

- **La touche `V`** ne déclenche plus le *Toggle Auto-Pickup* du jeu (voir **[Voix](Voice-fr)**).
- **Rayon de ramassage plus grand :** bois/pierre/minerai sont cherchés jusqu'à **25 m** de toi, et les objets tombés
  sont ramassés jusqu'à **12 m**.
- **Chatter aléatoire désactivé** par défaut : les petites phrases toutes les 15–30 s n'arrivent plus. Les phrases
  utiles (ordres, changement de mode, réponses de l'IA, événements) restent.

Pour voir toutes les options, va dans **[Configuration](Configuration-fr)**.
