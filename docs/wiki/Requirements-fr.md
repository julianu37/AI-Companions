> 🌍 [English](Requirements) · [Português](Requirements-pt) · [Español](Requirements-es) · **Français** · [Italiano](Requirements-it)

# Exigences

Ce qu'il te faut avant d'installer **AI Companions**.

Le mod fonctionne en **deux couches indépendantes** : les **ordres par texte dans le chat** (ils ont besoin d'un
**LLM**) et les **ordres par la voix** (ils ont besoin d'un micro). Tu peux utiliser **seulement le chat**, en
écrivant — la voix est facultative.

## Obligatoire

| Élément | Version / détail |
|---|---|
| **Valheim** | **1.0.x** (moteur Unity 6 — testé sur **1.0.20**) |
| **BepInEx** | **BepInExPack_Valheim 5.4.2351** (BepInEx 5.4.23.5) — le mod est un plugin BepInEx 5 |
| **Un LLM** | Pour que les ordres de texte/voix soient compris. Le chemin **recommandé** est **LM Studio** en local : tu télécharges un modèle (~1,9 Go) et tu laisses le serveur allumé, et rien ne sort de ta machine. **Groq** est ce qui vient dans le `.cfg` : il faut seulement **internet** et une **clé gratuite** (voir plus bas), et le texte des ordres **quitte ta machine**. **OpenRouter** fonctionne encore, mais son tier gratuit est plus lent. Voir **[LLM (LM Studio, Groq ou OpenRouter)](LLM-fr)** |
| **Espace disque** | Voir la table plus bas |

> **Le LLM est obligatoire ; le chemin ne l'est pas.** **LM Studio/Ollama** est le chemin **recommandé** et il est
> local : rien ne sort, mais tu télécharges un modèle (~1,9 Go) et tu laisses le serveur allumé pendant que tu
> joues. **Groq** est ce qui vient dans le `.cfg` : la **clé gratuite** (https://console.groq.com/keys) et
> internet suffisent — et le texte des ordres **quitte ta machine**. **OpenRouter** fonctionne aussi (clé
> gratuite sur https://openrouter.ai/keys) et reste comme **alternative**, parce que son tier gratuit est plus
> lent. Les trois chemins — dans cet ordre de préférence — sont dans
> **[LLM (LM Studio, Groq ou OpenRouter)](LLM-fr)**.

## Facultatif

| Élément | Pour quoi |
|---|---|
| **Micro** | Seulement pour les **ordres vocaux** (push-to-talk). Sans micro, utilise le chat |
| **GPU** | Ça ne change quelque chose que dans l'option **locale** : le LLM dans LM Studio tourne **beaucoup** plus vite avec un GPU. Sur Groq (et sur OpenRouter), le modèle tourne sur leur serveur |
| **Modèles de voix** | Ils viennent déjà dans le paquet **complet**. Dans le paquet **sans modèles**, tu les télécharges à part. Le même `voices.bin` de Kokoro porte les voix des **cinq langues** |

## Espace disque

| Quoi | Taille |
|---|---|
| Paquet **complet** (mod + modèles) | ~314 Mo (zip) |
| Paquet **sans modèles** (mod + traductions) | ~28,4 Mo (zip) |
| Mod installé (dossier du plugin) | ~371 Mo |
| Modèle Whisper `ggml-small-q5_1.bin` (défaut du STT) | 181 Mo |
| Modèle Kokoro (TTS) `model.int8.onnx` | 109 Mo |
| Voix Kokoro `voices.bin` | 27 Mo (les voix des cinq langues : PT-BR, EN, ES, FR et IT) |
| Modèle du LLM **local** (`qwen2.5-3b-instruct` Q4_K_M, LM Studio) — seulement dans l'option locale | ~1,93 Go |

> Prévois quelque chose autour de **1 Go** pour le mod et les modèles de voix, plus l'espace du modèle du LLM.

## Matériel

- **CPU :** le TTS (Kokoro-82M) synthétise sur le **CPU**. Avec **4 cœurs**, il génère déjà plus vite que le temps
  réel (la parole sort devant le compagnon). Le STT (Whisper) tourne aussi sur le CPU.
- **GPU :** ça ne compte que dans l'option **locale** — dans LM Studio un GPU fait une différence énorme. Un modèle 3B
  tourne bien sur le CPU, mais il est bien plus rapide avec un GPU. Sur Groq (et sur OpenRouter), c'est leur serveur
  qui fait tourner le modèle.
- **RAM :** dans l'option locale, un modèle 3B quantifié (Q4) prend environ **2 Go** de mémoire, en plus du jeu. Avec
  Groq (ou OpenRouter), il n'y a pas cette consommation.

## Comment installer BepInEx

BepInEx est le chargeur de plugins de Valheim. Installe-le **avant** le mod :

1. Télécharge **BepInExPack_Valheim 5.4.2351** depuis la page des releases de BepInEx :
   https://github.com/BepInEx/BepInEx/releases
2. Extrais le contenu dans le dossier du jeu, de sorte que `winhttp.dll` et le dossier `BepInEx\` se retrouvent **à la
   racine** de Valheim (`C:\Program Files (x86)\Steam\steamapps\common\Valheim` par défaut).
3. Lance Valheim **une fois** et ferme-le — BepInEx crée la structure de dossiers
   (`BepInEx\plugins\`, `BepInEx\config\`, `BepInEx\LogOutput.log`).

> Si BepInEx ne se charge pas, regarde la documentation officielle : https://docs.bepinex.dev/

Après ça, suis l'**[Installation](Installation-fr)** du mod.

## Étape suivante

➡️ **[Installation](Installation-fr)**
