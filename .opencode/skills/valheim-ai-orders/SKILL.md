---
name: Valheim AI Orders
description: Como uma mensagem (chat ou voz) vira ordem no companheiro — pipeline CompanionChatAI → LlmClient → LlmIntent → CompanionOrders, as redes por palavras (Pickup/Gather/Radial/Interaction/IsDoorRequest/IsInventoryRequest), as 5 ordens de interação e como testar. Use ao mexer em ordens, contrato, intents ou parser.
---

# Ordens do companheiro (1.9.2)

## O caminho completo

```
Chat.SendText (patch)  ─┐
VoiceInput → WhisperStt ─┴→ CompanionChatAI.Submit(msg, origem)
      → prompt (Header + persona + memória + Contract do idioma + nomes)
      → LlmClient (POST Task + fila; id por requisição)
      → LlmIntent (parser tolerante: action/reply/companion/target/resource/amount)
      → CompanionOrders.Run(intent, userText)
          ├─ OrderItems.GatherAction   (o `resource` manda sobre a ação)
          ├─ PickupIntent.NeighborAction (drop × pickup × nopickup)
          ├─ GatherIntent              (coleta com número)
          ├─ InteractionIntent         (hive/board/sit/deposit + porta)
          ├─ RadialIntent.Correct      (postura/vagar/comando/trabalho)
          └─ Apply(action) → ApplyActionMode / SetCombatStance / DirectEngage / EquipItem / DropItem / ...
```

No lado `none` (o modelo devolveu conversa), a mesma bateria roda em `CompanionChatAI.HandleResult`:
inventário → coleta → interação → postura.

## Regras do contrato (não mexer sem medir)

- **`ValidActions` é a fonte única do `enum`** do JSON schema (35 ações hoje). Ação nova = contrato novo.
- **Não acrescente exemplos ao contrato**: medido na M5, mais few-shot **piora** o 3B. O comportamento é
  corrigido pela **rede por palavras**.
- `amount` = **alvo total na mochila** (não conta baú/pilha); `target` nunca `null` em `attack`/`equip`/`drop`;
  `reply` é uma frase curta (é falada).
- **`action` × `resource`**: quando discordam numa coleta, **o recurso manda** (log diz o que foi usado).

## As redes por palavras (puras, 1× por mensagem)

| Arquivo | Cobre | Guardas |
|---|---|---|
| `OrderItems` | apelidos de recurso (PT/EN/ES/FR/IT) → `Wood`/`Stone`/`Ore`/`Forage` | lista de cada trabalho; `*` não decide |
| `PickupIntent` | `nopickup` → `drop`/`pickup` pelos verbos | parada/não pegue desligam; palavra inteira entre separadores |
| `GatherIntent` | `pickup`/`none` + recurso + número → coleta | marcador de chão mantém `pickup`; exige verbo no lado `none` |
| `RadialIntent` | postura, vagar, comando, smelt/cook/hunt | `PreciseActions` (attack/equip/drop/inventory*/coleta/**as 5 novas**) nunca são sobrescritas |
| `InteractionIntent` | `hive`/`board`/`sit`/`deposit` | só `none` (e `drop`→`deposit`); negação/"para de" recusam |
| `CompanionOrders.IsDoorRequest` | "fecha a porta" → `door` | exige palavra de porta + verbo **e nenhuma** palavra de mochila |
| `CompanionOrders.IsInventoryRequest` | "abre o inventário" | exige palavra de mochila + verbo; pergunta não é ordem |

Regras comuns: mensagem curta (≤10 palavras), nunca com `?`, casamento por **palavra inteira entre
separadores** (`_larga_` não casa `largada`), e **uma linha de log** por correção
(`[IA] O modelo devolveu "X" mas o jogador pediu para ... - usei "Y".`).

## As 5 ordens de interação (`hive`/`door`/`board`/`sit`/`deposit`)

- Alvo: o objeto **apontado** (mesmo raio/mask/50 m da tecla; `Pointed<T>` varre **todos** os hits) e, sem
  nada apontado, o mais próximo (20 m colmeia/porta, 30 m navio, 5 m fogueira/baú).
- Um **único** companheiro: `OrderCompanion` (checa com `MHR_InScopeCheck`, toma posse só do escolhido) →
  `Single(...)` → `Direct*` (que faz `CancelExistingActions` → `TakeControl`).
- A `Direct*` fala a linha aleatória dela; durante a ordem ela é **silenciada** (`Silent`/`_orderSpeech`,
  restaurado em `finally`) e a ordem diz **a sua** linha (`hc_ai_door_closed`, `hc_ai_deposit_done` com `{0}`…).
- Falhas: porta trancada → `hc_cmd_door_locked`; colmeia vazia → `hc_cmd_hive_empty`; baú cheio →
  `hc_ai_deposit_full`; nada por perto → `hc_ai_<ação>_none`; baú fora das regras (túmulo/carrinho/navio/baú
  privado de outro jogador) → recusa.
- `deposit` conta **itens** (não pilhas), usa `MHR_Chests.Usable` + `PrivateArea` e pede a **posse** do baú.
- `sit` com `fromOrder: true` (mesmo fogo = continua sentado; outro fogo = levanta e vai).

## Endereçamento e conversa

- `CompanionNames`: mensagem que **começa** com o nome de um companheiro real vale só para ele; nome
  inexistente **capitalizado** que não seja tratamento ("mano", "bora") avisa e não ordena.
- Conversa (`action: none` + `reply`): fala no balão do companheiro (TTS com `withVoice`), não toma posse.
- `despawn` não é exposto ao modelo.

## Como testar

- **Em jogo:** chat (`colete 10 pedras`, `fecha a porta`, `guarda isso no baú`, `senta`), voz (segurar `V`),
  painel `I`; log `[IA] Order ...`, `[Order] ...`.
- **Fora do jogo (o padrão da equipe):** harness que carrega a **`Companions.dll` real** por reflexão e
  chama `LlmIntent.Parse`, `IsDoorRequest`, `InteractionIntent.Correct`, `RadialIntent.Correct`,
  `PickupIntent.NeighborAction`, `GatherIntent...`, `CompanionChatAI.ValidActions` — sem Unity, sem jogo.
  Há exemplos em `%TEMP%\opencode\audit-contrato` e nos relatórios de auditoria.
- **Modelo pequeno:** `qwen2.5-3b-instruct` (LM Studio, temperatura 0,1, o mesmo schema/contrato) para medir
  acerto por ação/idioma; **nunca** valide só a leitura.
