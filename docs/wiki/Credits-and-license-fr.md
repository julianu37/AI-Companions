> 🌍 [English](Credits-and-license) · [Português](Credits-and-license-pt) · [Español](Credits-and-license-es) · **Français** · [Italiano](Credits-and-license-it)

# Crédits et licence

**AI Companions** est un **projet indépendant** qui existe par-dessus le travail d'autres personnes. Cette page dit
qui a fait quoi.

---

## Upstream (la base du mod)

| Projet | Auteur | Licence |
|---|---|---|
| **Offline Companions Reborn** | **MaddhattrRocks** | **MIT** |
| *Offline Companions* (l'original) | **ProfMags** | MIT |

https://github.com/MaddhattrRocks/Offline-Companions-Reborn

**Vient de là :** le compagnon lui-même — les travaux (récolte, culture, pêche, chasse, cuisine, fonte, réparation,
loot), la nourriture et la stamina, les compétences, la maison et le repos, les portails et la charrette, l'IA de
déplacement et de combat, le menu radial et toute l'UI de base. La couche d'IA de notre projet n'est qu'une **nouvelle
entrée** pour ces mêmes actions.

> Offline Companions Reborn, lui, est un fork d'*Offline Companions*, de **ProfMags**.

---

## Ce qui est à nous

Ce que le projet **AI Companions** ajoute :

1. **La traduction en cinq langues** — portugais du Brésil, anglais, espagnol, français et italien : 914 clés
   d'interface et 221 phrases du compagnon par langue, auditées (aucun texte en anglais visible pour un joueur de ces
   langues), avec l'**anglais comme langue native**.
2. **La couche d'IA :**
   - Ordres en **langage naturel** par **texte dans le chat** et par **voix** (push-to-talk + STT local).
   - **Personnalité** (presets ou texte libre), **mémoire** par compagnon et **conversation libre**.
   - **TTS local** (Kokoro-82M via sherpa-onnx) : le compagnon **parle** à sa position dans le monde.
   - Le **panneau de l'IA** (touche `I`).
3. **Ajustements et correctifs :**
   - Rayon de ramassage plus grand et chatter aléatoire désactivé par défaut.
   - La touche `V` ne déclenche plus le *Toggle Auto-Pickup* du jeu (patch conditionnel).
   - L'écho de la voix (c'était la zone de reverb globale du jeu).
   - L'ordre de pierre qui ne s'arrêtait pas à la cible.
   - Les panneaux du mod au-dessus du HUD du jeu (le clic avalé sur `F8`).

---

## Tiers

| Composant | Ce que c'est | Licence / lien |
|---|---|---|
| **Whisper.net** | Binding .NET pour whisper.cpp, utilisé dans le STT | MIT — https://github.com/sandrohanea/whisper.net |
| **whisper.cpp** | Implémentation de Whisper en C/C++ | MIT — https://github.com/ggml-org/whisper.cpp |
| **Modèles ggml de Whisper** | Les fichiers `ggml-*.bin` | MIT — https://huggingface.co/ggerganov/whisper.cpp |
| **sherpa-onnx** | Runtime TTS/STT (ONNX) utilisé dans le TTS | Apache-2.0 — https://github.com/k2-fsa/sherpa-onnx |
| **Kokoro-82M** | Le modèle de voix que le compagnon utilise | Apache-2.0 — https://huggingface.co/hexgrad/Kokoro-82M |
| **onnxruntime** | Le runtime qui exécute les modèles ONNX (vient avec sherpa-onnx) | MIT — https://github.com/microsoft/onnxruntime |
| **espeak-ng** | Phonémisation utilisée par Kokoro (vient comme **données**, dans `espeak-ng-data\`) | GPL-3.0, les données seulement — https://github.com/espeak-ng/espeak-ng |
| **Groq** | Le service qui sert le LLM gratuit par défaut (rien de lui n'est redistribué) | https://console.groq.com — usage soumis aux conditions du service |
| **OpenRouter** | Le service de LLM distant gardé comme alternative (rien de lui n'est redistribué) | https://openrouter.ai — usage soumis aux conditions du service |
| **LM Studio** | L'application qui sert le LLM local | https://lmstudio.ai |
| **Qwen2.5** | La famille du modèle recommandé pour le mode local (`qwen2.5-3b-instruct`) | Apache-2.0 — https://huggingface.co/Qwen |
| **gpt-oss** | Le modèle ouvert servi par Groq par défaut (`openai/gpt-oss-20b`) | Apache-2.0 — https://huggingface.co/openai/gpt-oss-20b |

> Les textes complets des licences de tiers voyagent **dans le paquet**, dans le dossier `LICENSES\`.
>
> **Référence conceptuelle :** **Thrall** (ego.ai) a été étudié comme référence d'idées (*all rights reserved*).
> **Aucun** code ni binaire de lui n'est redistribué ici.

---

## Licence du projet

**MIT.** Tu peux l'utiliser, le modifier et le redistribuer, en gardant les avis de copyright.

Les fichiers complets sont dans le dépôt :

- [`LICENSE`](https://github.com/julianu37/AI-Companions/blob/main/LICENSE)
- [`THIRD_PARTY_NOTICES.md`](https://github.com/julianu37/AI-Companions/blob/main/THIRD_PARTY_NOTICES.md)
- [`NOTICE.md`](https://github.com/julianu37/AI-Companions/blob/main/NOTICE.md)

> Le dossier `LICENSES\` qui vient avec le mod installé apporte les textes des licences des composants tiers
> distribués avec lui.
