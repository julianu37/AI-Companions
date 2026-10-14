# Visão do companheiro — o alvo ANTES do LLM

> "Companheiros não são animais de estimação — são membros da equipe com seus próprios inventários,
> equipamentos, comida, resistência, habilidades e fala. Aponte-os para o seu trabalho e eles
> realmente o farão: lutarão ao seu lado, cortarão lenha, minerarão, coletarão recursos, cultivarão,
> pescarão, caçarão, cozinharão e prepararão bebidas, operarão suas fundições, cuidarão da sua base,
> puxarão sua carroça, sentarão perto da fogueira, dormirão em camas e seguirão você através de
> portais. Tudo (aparência, inventário, equipamentos, trabalho, casa, habilidades, propriedade) é
> salvo com o mundo."

Este é o **alvo do ModIA antes de plugarmos o "cérebro"** (LLM/voz/STT/TTS). A IA entra **por cima**
de um companheiro que já é um membro funcional da equipe — não como muleta para o que falta.

---

## Decisão de escopo (2026-10-14)

**Foco: single-player primeiro.** O **2.15 (ZDO / "salvo com o mundo")** e o **2.5 (multiplayer)**
ficam **adiados** por decisão do dono do projeto. Ordem adotada:

**2.13** (comida/stamina/skills) → **2.14** (peso/depósito/ferramentas) → **2.16** (jobs) →
**2.17** (base/casa) → **2.18** (qualidade de vida) → **Fase 3** (fala/LLM).

Consequência aceita: enquanto o ZDO não existir, o estado do companheiro continua no arquivo
`BepInEx/config/modia.companion.state.json` (funciona bem em single-player).

---

## Onde estamos (v0.3.x)

| Capacidade | Status |
|---|---|
| Aparência (corpo viking, cabelo/barba, cor de pele/cabelo) | ✅ |
| Inventário próprio (8×4) + **peso** | ✅ |
| Equipar itens / loadout padrão | ✅ |
| Lutar ao seu lado (Passive/Defensive/Aggressive, bloqueio/parry) | ✅ |
| Cortar lenha / minerar / coletar (drops, rocha rachada, ferramenta por tier) | ✅ |
| Seguir você (proteção contra queda, **abre portas**) | ✅ |
| Menu PT-BR + hover com ações | ✅ |
| Cata o que abateu (drops de criatura) | ✅ |
| **Comida / resistência (stamina)** | ✅ |
| **Habilidades (skills) que evoluem** | ✅ |
| Peso, **depósito em baú** e *tool keeper* | ✅ |
| Caçar · pescar | ✅ |
| Cultivar · cozinhar/preparar bebidas · operar fundições | ✅ |
| Cuidar da base (reparar, abastecer fogueiras, colmeias, **ordenar baús**) | ✅ |
| Casa (home) | ✅ |
| Fogueira (sentar/descansar) · dormir em cama · **seguir por portais** · puxar carroça | ✅ |
| Combate: escolher arma/escudo por alvo | 🟡 parcial (vanilla `EquipBestWeapon`) |
| **Fala** | ❌ (é a Fase 3) |
| **Salvo com o mundo (ZDO)** | ⏳ adiado (SP primeiro) — hoje é JSON em `BepInEx/config` |

> **Marco: v0.4.6** — o companheiro já é um **membro da equipe** em single-player (trabalha, come,
> cansa, evolui, cuida da base e da casa). Falta a **fala/IA** (Fase 3) e, se/quando quisermos, o
> **ZDO/multiplayer**.

---

## Lacunas e ordem sugerida (antes do LLM)

### 2.13 — Comida, resistência e habilidades
O companheiro **come** (usa a comida que tem no inventário, ganha os bônus), tem **stamina** que
importa (correr/atacar/minerar gastam) e **evolui habilidades** (cortar, picareta, combate…).
Hoje ele nem passa fome nem cansa — o que o deixa "decorativo".

### 2.14 — Peso, depósito e ferramentas (#7 da auditoria)
Inventário com **peso**; ao encher, **deposita num baú** (home/unload) e retoma o trabalho em vez de
encerrar a tarefa. Inclui o *tool keeper* (buscar/devolver a ferramenta certa).

### 2.15 — Persistência NO MUNDO (ZDO) — **pré-requisito do resto**
"Tudo salvo com o mundo": aparência, inventário, equipamentos, **trabalho atual**, **casa**,
habilidades e propriedade guardados no **ZDO/save**, não num arquivo local. Esta é a mudança
arquitetural mais importante e **também destrava o multiplayer** (Fase 2.5).

### 2.16 — Trabalhos (jobs)
`caçar` · `cultivar` · `pescar` · `cozinhar`/`bebidas` · `operar fundições` — a mesma interface de
"aponte para o trabalho" (fila de tarefas), reaproveitando os padrões do Offline Companions Reborn.

### 2.17 — Base e casa
`cuidar da base` (reparar, reabastecer fogueiras, **ordenar baús**) e **casa/home** (onde ele
deposita, dorme e volta).

### 2.18 — Qualidade de vida
`sentar perto da fogueira` · `dormir em camas` · `seguir você através de portais` · `puxar carroça`.

### Fase 3 — Fala e IA (por último)
Só então o "cérebro": comandos por voz/chat, STT/TTS, personalidade e conversa — apontando o
companheiro para os trabalhos que **já existem**.

---

## Princípios

1. **Primeiro o companheiro funcional, depois o LLM.** A IA escolhe *qual* trabalho; os trabalhos já
   têm de funcionar sem ela.
2. **Companheiro = membro da equipe**, não item de conveniência: ele tem inventário, comida, cansaço
   e habilidades próprias.
3. **Tudo salvo com o mundo** (ZDO), para sobreviver a reinícios e funcionar em multiplayer.
4. Reaproveitar **padrões** do Offline Companions Reborn (MIT) — com atribuição — e confirmar APIs no
   jogo 1.0.17.
