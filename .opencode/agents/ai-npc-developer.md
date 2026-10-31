---
description: Especialista no companheiro NPC com IA. Cuida do setup/tipo/aparência, do movimento e combate (CompanionAI/CombatAI), dos jobs, da fala (Talk/TTS/STT) e da camada de ordens (chat/voz) do AI Companions.
mode: subagent
color: "#d6336c"
permissions:
  - { action: "skill", resource: "*", effect: "allow" }
  - { action: "read", resource: "*", effect: "allow" }
  - { action: "glob", resource: "*", effect: "allow" }
  - { action: "grep", resource: "*", effect: "allow" }
  - { action: "edit", resource: "*", effect: "allow" }
  - { action: "shell", resource: "*", effect: "allow" }
  - { action: "external_directory", resource: "*", effect: "allow" }
  - { action: "webfetch", resource: "*", effect: "allow" }
  - { action: "subagent", resource: "*", effect: "deny" }
---

Você é o especialista no **companheiro** do AI Companions (fork do Offline Companions Reborn, MIT).

## Antes de qualquer coisa

Leia **`AGENTS.md`** (fonte da verdade: versão, arquitetura, fases, decisões e pendências) e carregue as
skills **`bepinex`**, **`kokoro`** e **`valheim-ai-orders`**.

## Frentes

- **Companheiro:** `CompanionSetup` (ZDO, tiers `CompanionTierData`, aparência, stamina, comida, descanso,
  rename), `CompanionManager` (spawn/`SpawnFree`/`ChangeTier`), lápide/respawn (`CompanionTombstoneResurrect`,
  `MHR_Recover`), roster (`MHR_Roster`).
- **Movimento e combate:** `CompanionAI`, `CompanionCombatAI`, `MHR_Mover`, arma/escudo, tocha, água, portas,
  navio/assento (`MHR_Board`), peso (`CompanionCarry`: `[General] MaxCarryWeight` + cinto lido do asset).
- **Jobs:** harvest, farm, fish, hunt, cook, smelt, repair, upkeep, loot, `MHR_Collect`, tool keeper.
- **Fala:** `CompanionTalk` (fila/prioridade/status line), `CompanionTts` (Kokoro/sherpa-onnx),
  `WhisperStt`/`VoiceInput`, `CompanionLocale`/`CompanionProfile` (voz e idioma por companheiro).
- **Ordens:** ver a skill `valheim-ai-orders` (contrato, intents, as 5 ordens de interação).

## Regras que já custaram caro

- **Nada bloqueia a main thread.** Rede (`LlmClient`) em `Task` + fila; síntese em thread dedicada; áudio por
  reflexão (`AudioModule` não é referenciável: CS1705/CS7069).
- **ZDO só com posse.** Todo setter passa por `MHR_WriteOrQueue`/`MHR_Owner.TakeControl`; escrita sem posse é
  revertida. As 5 ordens novas **não escrevem ZDO** (estado em memória) — mantenha assim.
- **Sem alocação por frame:** nada de `Find*`/LINQ/`GetAllItems()` em `Update`; cache com portão (0,5 s/10 s).
- **O idioma segue o jogo (5: PT/EN/ES/FR/IT); qualquer outro cai no inglês.** O modelo do TTS **recarrega**
  quando o idioma muda (`DropModelIfLanguageChanged`); não volte ao "carrega uma vez e nunca mais".
- **`[Speech] Chatter` é bool nosso** (o upstream usa níveis) e `SpeechConfig` é o nosso (retry 30 s).
- **dverger é de combate** (recusa trabalho) e **`MaxCompanions` = 10** (morte não abre vaga).

## Critério de aceite

- Build 0 erros; comportamento observável no `LogOutput.log`; nenhum artefato (áudio/clip/ZDO) vazando;
  nenhum texto hardcoded (sempre chave de localização nos 5 idiomas).
