> 🌍 [English](Home) · [Português](Home-pt) · [Español](Home-es) · **Français** · [Italiano](Home-it)

# AI Companions

Un **compagnon PNJ avec IA** pour **Valheim 1.0** : tu **parles ou écris** et il comprend et agit.

> **Types de compagnon : les dverger sont des combattants**
>
> **Mage**, **Mage de feu**, **Mage de glace** et **Soutien** sont des **combattants** : ils se battent normalement et
> la roue n'affiche que les actions de combat/soutien — les ordres de **travail** par chat, voix ou la touche de
> commande sont **refusés** avec *« Je suis un combattant, je ne fais pas ce travail. »*. Pour travailler, utilise le
> type **Compagnon** (détails dans **[Comment ça marche](How-it-works-fr#types-de-compagnon)**).

## En action

**1. Un ordre dans le chat** — `Sophia, ramasse du bois` : sa réponse et sa bulle de dialogue dans le monde.

![Le chat avec un ordre, la réponse du compagnon et la bulle de dialogue](images/en-companions-chat-orders.jpg)

**2. Ton premier compagnon, à ta façon** — genre, cheveux, barbe, teint, couleur des cheveux, le nom et la voix et la
personnalité de **ce** compagnon ; puis **Invoquer un compagnon**.

![Le panneau « Choisis ton compagnon » : genre masculin/féminin, coupe de cheveux, style de barbe, teint, couleur et nuance des cheveux, le nom, la voix du compagnon avec le bouton Écouter, la personnalité et le bouton Invoquer un compagnon, avec l'aperçu 3D](images/en-first-run-companion-creation.jpg)

**3. Une troupe entière** — jusqu'à **10** compagnons, chacun avec son onglet dans le panneau `I` et son nom dans la
liste à l'écran.

![Quatre compagnons autour du joueur sur la plateforme de départ, leurs noms dans le monde et la liste des compagnons avec santé et stamina à droite](images/en-multiple-companions-1.jpg)

**4. Avec toi en chemin** — ils te suivent, ils se battent et ils portent ce qu'ils ramassent.

![Trois compagnons marchant avec le joueur dans les Prairies, avec les barres de santé et de stamina de chacun à droite](images/en-multiple-companions-2.jpg)

## Ce que c'est

Ce projet est un **projet indépendant** basé sur [Offline Companions Reborn](https://github.com/MaddhattrRocks/Offline-Companions-Reborn)
(**MIT**) — qui est lui-même un fork d'*Offline Companions*, de **ProfMags** — avec deux choses de nous par-dessus :

1. **Cinq langues** — **portugais du Brésil, anglais, espagnol, français et italien** : l'interface (914 clés), les
   phrases du compagnon (221 lignes), les presets de personnalité et le contrat du LLM suivent la **langue du jeu**,
   avec l'**anglais comme langue native**, et une nouvelle langue est **un fichier**.
2. **Une couche d'IA** : des ordres en **langage naturel** (texte dans le chat ou **voix** par push-to-talk), avec
   **personnalité**, **mémoire** et **conversation libre** — et la voix et la reconnaissance vocale **choisissent la
   langue et la voix toutes seules**.

Le compagnon lui-même (travaux, nourriture, stamina, compétences, maison, repos, portails, charrette, menu radial)
vient de l'upstream et continue à fonctionner pareil — l'IA n'est qu'une **nouvelle entrée** pour les mêmes actions.

> **La voix tourne entièrement sur ta machine.** La reconnaissance vocale (STT) et la voix synthétisée (TTS) sont
> **100 % locales** : rien n'est envoyé. Comprendre les ordres utilise un **LLM** — le chemin recommandé est
> **LM Studio** (ou Ollama) en local, où **rien** ne sort ; le `.cfg` arrive pointé sur **Groq**, un **service
> distant** (le texte des ordres et de la conversation **quitte ta machine**). Voir
> **[LLM (LM Studio, Groq ou OpenRouter)](LLM-fr)**.

## Ce qu'il fait

| Fonction | Comment |
|---|---|
| **Ordres par chat** | `ramasse 10 bois` · `suis-moi` · `arrête` · `attaque ce greydwarf` · `équipe la hache en bronze` |
| **Ordres par voix** | Maintiens **`V`**, parle et relâche — la parole est transcrite dans le jeu |
| **Il parle** | Voix synthétisée **dans la langue du jeu**, sortant **de sa position** dans le monde |
| **Personnalité** | Presets (**Viking loyal**, **Mage sarcastique**, **Sarcastique**, **Guide patient** — les noms suivent la langue du jeu) ou tes propres mots |
| **Mémoire et conversation** | `bonjour`, `comment vas-tu ?` — il répond en personnage et se souvient de la conversation |
| **Plusieurs compagnons** | Jusqu'à **10**, chacun avec son onglet dans le panneau `I` ; l'invocateur en crée un nouveau **gratuitement** (sans le Trader) |
| **Ordre par le nom** | `Gruncha, ramasse 5 pierres` ne vaut que **pour lui** ; sans le nom, l'ordre vaut pour tous |
| **Apparence** | Éditeur avec **aperçu 3D** : genre, cheveux, barbe, teint et couleur des cheveux, sans renaissance |
| **Panneau de l'IA** | Touche **`I`** : un onglet par compagnon (nom, type, Appeler, Apparence, voix et personnalité), l'invocateur, le volume, le push-to-talk et les **réglages du LLM** (avec le **sélecteur de fournisseur**) — tout enregistré tout de suite |

## Exigences en bref

| Élément | Détail |
|---|---|
| **Valheim 1.0.x** + **BepInEx 5.4.2351** | La base de tout — pas à pas dans **[Installation](Installation-fr)** |
| **Un LLM** | **LM Studio** (recommandé) : le modèle tourne sur ta machine et rien n'en sort — tu le télécharges une fois (~1,9 Go) et tu laisses le serveur allumé. **Groq** (ce qui vient dans le `.cfg`) : seulement la **clé gratuite** (https://console.groq.com/keys) et internet — le texte des ordres **quitte ta machine**. **OpenRouter** fonctionne encore comme alternative |
| **Micro** | Seulement pour les ordres vocaux |

## Pour commencer

1. Regarde ce qu'il te faut dans **[Exigences](Requirements-fr)**.
2. Suis le pas à pas de **[Installation](Installation-fr)**.
3. Configure le LLM dans **[LLM (LM Studio, Groq ou OpenRouter)](LLM-fr)** — le chemin recommandé est **LM
   Studio** en local (sans clé, rien ne sort) ; **Groq** est ce qui vient dans le `.cfg` (seulement la clé
   gratuite) et **OpenRouter** est l'alternative.
4. Regarde les touches et les ordres dans **[Ordres](Commands-fr)**.
5. Si quelque chose ne marche pas, va dans **[Problèmes courants](Troubleshooting-fr)**.

## Index du wiki

| Page | Ce qu'elle contient |
|---|---|
| **[Exigences](Requirements-fr)** | Ce qui est obligatoire et ce qui est facultatif |
| **[Installation](Installation-fr)** | Pas à pas avec des images |
| **[LLM (LM Studio, Groq ou OpenRouter)](LLM-fr)** | LM Studio comme chemin recommandé, Groq comme l'option cloud qui vient dans le `.cfg` (clé gratuite) et OpenRouter comme alternative |
| **[Voix](Voice-fr)** | STT (Whisper) et TTS (Kokoro) : comment ils marchent et comment les régler |
| **[Ordres](Commands-fr)** | Touches, ordres de chat/voix et commandes de console |
| **[Configuration](Configuration-fr)** | Le `.cfg`, la section `[AI]` et les panneaux `F8` et `I` |
| **[Problèmes courants](Troubleshooting-fr)** | Symptôme → cause → solution |
| **[Comment ça marche](How-it-works-fr)** | Architecture et le chemin d'un ordre |
| **[Historique des versions](https://github.com/julianu37/AI-Companions/blob/main/package/CHANGELOG.md)** | Le changelog complet, de la 1.6.0 à la 1.9.1 |
| **[Crédits et licence](Credits-and-license-fr)** | Upstream, tiers et licences |

## Liens rapides

| Quoi | Où |
|---|---|
| **Releases (téléchargement)** | https://github.com/julianu37/AI-Companions/releases |
| **Dépôt** | https://github.com/julianu37/AI-Companions |
| **Changelog** | https://github.com/julianu37/AI-Companions/blob/main/package/CHANGELOG.md |
| **Upstream (MIT)** | https://github.com/MaddhattrRocks/Offline-Companions-Reborn |

La release publiée la plus récente est la **1.9.1** (26/10/2026), avec **deux** fichiers au choix :

| Fichier | Taille | Ce qu'il contient |
|---|---|---|
| `AI_Companions-1.9.1.zip` | ~314 Mo | Le mod **et** les modèles de voix (STT + TTS) déjà prêts |
| `AI_Companions-1.9.1-sem-modelos.zip` | ~28,5 Mo | Seulement le mod et les traductions — tu télécharges les modèles à part |

> **Lequel choisir ?** Si tu veux juste installer et jouer, prends le **complet**. Si ton internet est lent (ou si tu
> as déjà les modèles), prends le **sans modèles** et suis la section des modèles dans
> **[Installation](Installation-fr)**.

> **1.9.1 :** le paquet est ~54 Mo plus léger — le modèle `ggml-base-q5_1.bin`, qui n'était pas utilisé, n'est plus fourni
> (il reste un téléchargement facultatif).

> **La 1.9.0 en une ligne :** le mod parle **cinq langues** (portugais, anglais, espagnol, français et italien) en
> suivant la **langue du jeu**, et la **voix et la reconnaissance vocale sont automatiques** — `[AI] TtsVoice = 0` et
> `TtsLanguage` / `VoiceLanguage` = `auto`, et une valeur explicite gagne toujours. **Changer la langue du jeu ne
> demande pas de redémarrer.**

> **Tu viens d'une version plus ancienne ?** Le mod s'appelle maintenant **AI Companions** (c'était
> *ModAI-BR-Companions*) et le dossier est maintenant `BepInEx\plugins\AI-Companions\` : **supprime l'ancien dossier**
> (`BepInEx\plugins\ModAI-BR-Companions\`) avant d'extraire — deux copies du même plugin font que BepInEx charge le
> mod deux fois. **Rien de chez toi n'est perdu** : la sauvegarde, les compagnons, la maison et l'inventaire
> continuent à valoir (le GUID du plugin et les clés `HC_*` ont été conservés exprès).

> La **1.9.0** est la release des **cinq langues** : **portugais, anglais, espagnol, français et italien** —
> l'interface, les phrases du compagnon, les presets de personnalité et le contrat du LLM suivent la **langue du
> jeu**, et la voix et la reconnaissance vocale **choisissent la langue toutes seules** (`[AI] VoiceLanguage`,
> `TtsLanguage` et `TtsVoice` = `auto` / `0`). Changer la langue du jeu **ne demande pas de redémarrer** : les
> panneaux se reconstruisent à la prochaine ouverture et le HUD, les phrases et le TTS/STT suivent.
>
> La **1.8.3** est la version où l'**anglais est devenu la langue native** (clés et descriptions du `.cfg`) et le
> **portugais est devenu une traduction** — les panneaux (`I` et `F8`) suivent la **langue du jeu**, et une nouvelle
> langue entre par `Translations/<Langue>.json` + `Speech/<Langue>.json`.
>
> La **1.8.2** met le choix du LLM à un clic : dans la section **Réglages du LLM** du panneau `I`, le menu déroulant
> **« Choisis ton fournisseur LLM »** remplit l'**URL et le modèle** de **Groq**, **LM Studio** ou **OpenRouter** (la
> clé reste avec toi), et le bouton **« Crée ta clé d'API gratuite ICI »** ouvre la page de la clé dans le
> navigateur. La **vitesse de la parole** est restée **seulement dans le panneau `F8`**.
>
> La **1.8.1** apporte **Groq comme LLM par défaut** (gratuit et rapide : la clé vient de `console.groq.com/keys`),
> avec **LM Studio/Ollama** comme option locale et **OpenRouter** toujours disponible — seulement son tier gratuit est
> plus lent. Et elle garde tout ce qu'a apporté la **1.8.0** : **plusieurs compagnons**, chacun avec son onglet dans
> le panneau `I` (nom, type, apparence, voix et personnalité propres, ordre par le nom et un plafond de 10),
> l'**éditeur d'apparence avec aperçu 3D** et la section **Réglages du LLM** dans le panneau `I`.
>
> La **1.8.0** a corrigé le **poids** : le **Megingjord** (+150) et les autres objets avec un bonus de charge
> **s'ajoutent** au plafond du compagnon (le `/300` devient `/450`), en comptant avec la ceinture **n'importe où dans
> le sac** — pas besoin de l'équiper — et **une seule fois** (deux ceintures ne s'empilent pas).
