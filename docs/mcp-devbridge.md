# DevBridge — o MCP de desenvolvimento dentro da DLL

Ferramenta de **desenvolvimento**: dá a um agente de IA acesso ao Valheim rodando (estado, console, spawn,
companheiros, screenshot, log) pelo protocolo **MCP**. Vive **dentro da `Companions.dll`** (não é um segundo plugin) e
**desligada por padrão**.

> **Por que dentro da DLL:** um plugin separado exigia dois builds e dois deploys. Como módulo
> (`src/Companions/DevBridge/`), o bridge é compilado junto, usa os tipos do mod **diretamente** (sem reflexão) e
> acompanha a versão do mod. O custo é que ele vai no pacote — por isso nasce **off**.

## Arquitetura

```
agente ⇄ MCP/stdio ⇄ tools/mcp-devbridge/mcp_devbridge.py ⇄ TCP 127.0.0.1:47110 ⇄ src/Companions/DevBridge/ ⇄ main thread
```

- **`src/Companions/DevBridge/`** (dentro da `Companions.dll`):
  - `DevBridgeService.cs` — ciclo de vida (start/pump/stop), chamado pelo `CompanionsPlugin`.
  - `BridgeServer.cs` — `TcpListener` loopback, fila `ConcurrentQueue`, resposta por linha, timeout de 8 s.
  - `Commands.cs` — os comandos; chama `CompanionSetup`/`CompanionManager`/`MHR_Recover` **direto** (mesma assembly).
  - `Json.cs` — parser/serializer mínimo (sem `System.Text.Json`/`System.Web`).
- **`tools/mcp-devbridge/`** — o servidor MCP (Python 3, só stdlib) e o teste de protocolo.

**Regra que sustenta tudo:** a API do Unity/Valheim só é tocada na **main thread**. A thread do TCP enfileira; o
`Update()` do plugin drena (`Pump`). Nada fora de `Commands.Execute` toca no jogo.

## Como ligar

1. No `.cfg` (`BepInEx\config\AiCompanions.cfg`), seção `[Advanced]`:

   | chave | padrão | o que é |
   |---|---|---|
   | `DevBridge` | `false` | liga o servidor TCP local |
   | `DevBridgePort` | `47110` | porta (só loopback) |
   | `DevBridgeToken` | vazio | segredo opcional; quando setado, todo pedido precisa dele |

2. Abrir o jogo e conferir no `LogOutput.log`:

   ```
   [DevBridge] listening on 127.0.0.1:47110 (v1.9.4) - development tool, [Advanced] DevBridge
   ```

3. Teste rápido (PowerShell):

   ```powershell
   $c = New-Object Net.Sockets.TcpClient("127.0.0.1", 47110)
   $s = $c.GetStream(); $w = New-Object IO.StreamWriter($s); $r = New-Object IO.StreamReader($s)
   $w.WriteLine('{"cmd":"ping","args":{}}'); $w.Flush(); $r.ReadLine()
   $c.Close()
   ```

## Como conectar o agente (OpenCode)

```json
{
  "mcp": {
    "valheim-devbridge": {
      "type": "local",
      "command": ["python", "C:\\Users\\revenda33\\Desktop\\ModAI-BR-Companions\\tools\\mcp-devbridge\\mcp_devbridge.py"],
      "enabled": true
    }
  }
}
```

Variáveis de ambiente: `DEVRIDGE_HOST` (`127.0.0.1`), `DEVRIDGE_PORT` (`47110`), `DEVRIDGE_TOKEN` (vazio),
`DEVRIDGE_LOG` (padrão `<Valheim>\BepInEx\LogOutput.log`).

## Protocolo

Uma conexão por chamada; **um JSON por linha**, nas duas direções.

```json
{"cmd":"companions.order","args":{"action":"wood","name":"Maria"},"token":""}
{"ok":true,"data":{"action":"wood","mode":1,"applied":true}}
{"ok":false,"error":"no companion found"}
```

| cmd | args | devolve |
|---|---|---|
| `ping` | — | `bridge`, `version`, `inGame`, `world`, `mod` |
| `status` | — | jogador, posição, hp, stamina, bioma, dia, mundo |
| `console` | `command` | `command` |
| `player.give` | `item`, `amount` | `item`, `amount`, `added` |
| `player.teleport` | `x`, `y`, `z` | `x`, `y`, `z` |
| `world.spawn` | `prefab`, `count`, `x?`, `y?`, `z?` | `prefab`, `count` |
| `screenshot` | `path?` | `path` |
| `companions.list` | — | `count`, `companions[]` |
| `companions.spawn` | — | `spawned` |
| `companions.order` | `action`, `name?` | `action`, `mode`/`stance`/`value`, `applied` |
| `companions.teleport` | `name?`, `x?`, `y?`, `z?` | `name`, `x`, `y`, `z` |
| `companions.give` | `item`, `amount`, `name?` | `item`, `amount`, `added` |
| `companions.zdo` | `name?` | `name`, `keys{}`, `farmZones[]` |
| `player.kill` | — | `killed`, `hpBefore`, `hpAfter` |
| `player.inventory` | `item?`, `set?` | `count`, `stacks[]`, `totalStacks`, `weight`, `maxWeight` |
| `world.time` | `day?`, `time?`, `fraction?`, `skip?` | `before`, `time`, `day`, `fraction`, `dayLength` |
| `companions.kill` | `name?`, `damage?` | `name`, `killed`, `hpBefore`, `hpAfter` |
| `companions.tier` | `name?`, `tier` | `name`, `tier`, `prefabBefore`, `prefabAfter`, `changed` |
| `companions.hire` | — | `hired`, `price`, `coinsBefore`, `coinsAfter` |
| `companions.resurrect` | `name?` | `name`, `resurrected`, `hp`, `x/y/z`, `tombstoneId` |
| `companions.farm_zone` | `action` (`add`/`list`/`clear`), `name?`, `x/y/z`, `halfSize`, `crop` | `action`, `count`, `zones[]` |

`log_watch` não é comando do bridge: o servidor lê o `LogOutput.log` por cursor (devolve a **primeira** linha casada e
o cursor logo depois dela).

`companions.order` aceita: `follow`, `wood`, `stone`, `ore`, `forage`, `smelt`, `hunt`, `farm`, `fish`, `repair`,
`restock`, `cook`, `stay`, `stance_balanced`, `stance_passive`, `stance_ranged`, `stance_melee`, `autopickup_on`,
`autopickup_off`.

`companions.zdo` devolve as chaves `HC_*`: `Name`, `Owner`, `CompanionId`, `ActionMode`, `CombatStance`, `AutoPickup`,
`Wander`, `Follow`, `StayHome`, `HomeRadius`, `OrderCap`, `OrderCapMode`, `OrderCapItem`, `TombstoneId`, `Stamina`,
`Recovering`, `RecoverStance`, `RecoverPickup`, `Voice`, `Gen`, `FarmZones` (`"<none>"` ou `"N chars"`).

## Ferramentas MCP (23)

Genéricas: `devbridge_ping`, `game_status`, `game_console`, `player_give`, `player_teleport`, `player_kill`,
`player_inventory`, `world_spawn`, `world_time`, `screenshot` (devolve **imagem**), `log_tail`, `log_watch`.

Companheiros: `companions_list`, `companions_spawn`, `companions_order`, `companions_teleport`, `companions_give`,
`companions_zdo`, `companions_kill`, `companions_tier`, `companions_hire`, `companions_resurrect`,
`companions_farm_zone`.

Parâmetros e exemplos: `tools/mcp-devbridge/README.md`.

## Estado (31/10/2026)

| prova | resultado |
|---|---|
| build do mod com o módulo | **0 erros**, 1 aviso conhecido (`s_noDark`) |
| teste do servidor MCP (mock, sem jogo) | **41 PASS / 0 FAIL** |
| teste do parser JSON em C# | **11 PASS / 0 FAIL** |
| plugin separado no jogo | **removido**; agora só existe o módulo dentro da `Companions.dll` |
| ponta a ponta no jogo, módulo **standalone** (mundo `test3`) | **os 4 testes da 1.9.4 passaram** |
| ponta a ponta no jogo, módulo **na DLL** (mundo `test3`, 31/10/2026) | **24/24 PASS** — as **23 ferramentas**, incluindo kill/resurrect/tier/hire |

### Validação na DLL (mundo `test3`, 31/10/2026)

`python tools/mcp-devbridge/validate_e2e.py --destructive` → **18 PASS / 0 FAIL**, mais 6 ferramentas conferidas em
seguida (`player_give`, `player_teleport`, `world_spawn`, `companions_give`, `companions_spawn`, `log_watch`) e o
**caminho de sucesso do hire** (2000 → 1000 moedas) → **as 23 ferramentas, 0 falhas**.

- `ping` → `bridge: Companions.DevBridge`, `version: 1.9.4`, `inGame: true`, `world: test3`.
- `status` → `Gruncho`, hp 25/25, bioma Meadows; `player_inventory` listou os stacks reais.
- `screenshot` → PNG (base64) entregue; `log_tail` e `log_watch` leram o log real (`log_watch` casou `[DevBridge]`).
- `game_console help` e `world_time` (sem argumentos, mesmo instante) → ok; `dayLength = 1800` confirmado.
- `companions_list` → 2 companheiros (`Maria`); `companions_zdo` → todas as chaves `HC_*` resolvidas.
- `companions_order follow` → `applied: true`; `companions_teleport` e `player_teleport` → posições conferidas.
- `companions_farm_zone list` → 1 zona (a do teste 2); `companions_tier companion` → mesmo prefab (no-op esperado).
- `companions_kill` → `Maria` com `hpAfter: 0`; `companions_resurrect` → `resurrected: true`, `hp: 25`.
- `companions_hire` → `hired: false`, `reason: "not enough coins"` (0 moedas) — a recusa é o comportamento certo; com
  `player_inventory set 2000` → **`hired: true`, `price: 1000`, `coinsAfter: 1000`** (o caminho de cobrança do teste 1,
  agora pela DLL), e as moedas foram zeradas no fim.
- `player_kill` → `killed: true`, `hpAfter: 0`; o jogador respawnou no ponto de nascimento.
- `player_give`/`companions_give` (1 Wood) e `world_spawn` (Wood) → ok; `companions_spawn` → `spawned: true` (o mundo
  `test3` ficou com um companheiro a mais — dispensar pelo painel/roda se quiser).

> O mundo `test3` é de teste: o `player_kill`, o `companions_kill`/`resurrect` e o `companions_spawn` alteraram o save
> de propósito. O script é seguro por padrão (só leitura + ações inofensivas) e os destrutivos só rodam com
> `--destructive`.

### Evidências dos 4 testes da 1.9.4 (mundo `test3`, plugin standalone)

- `devbridge_ping` → `inGame: true`, `world: test3`, `mod: 1.9.4`; `companions_list` → `Maria`; `screenshot` → PNG de
  ~1,8 MB; `log_tail`/`log_watch` lendo o log real.
- **Teste 1 (oferenda) passou:** 2.000 moedas → `hired: true`, `price: 1000`, `coinsAfter: 1000`, log
  `Summoned a companion for an offering of 1000 coins (1000 left)` e **nenhuma** linha de starter; com 999 a recusa
  apareceu (`[Shrine] Odin pede uma oferenda…`).
- **Teste 2 (zonas) passou:** `farmZones=yes` nas trocas e `farmZones[0].crop = CarrotSeeds` sobreviveu ao
  round-trip Companion → Warrior → Companion (o modo cai para Follow ao virar Dverger, por desenho).
- **Teste 3 (recuperação) passou com ressalva:** morte → túmulo com 28 itens → `Revived Passive … stance 3
  (auto-pickup was True)` → `Recovery over - auto-pickup on` → `Gear back on and fed - back to stance 3`. A linha
  `[Recovery] Found its own tombstone` não apareceu porque o `MHR_ResurrectCore` já transferiu os 28 itens
  (`[Resurrect] Transferred 28 items…`); para exigi-la, o túmulo precisa continuar com itens depois do revive.
- **Teste 4 (dupla morte) passou:** duas mortes na recuperação, **duas** linhas `(auto-pickup was True)` com stance 3,
  e ao final `Recovery over - auto-pickup on`; contraprova com Recolher OFF manual → `(auto-pickup was False)` e
  `Recovery over - auto-pickup off`.
- **Bugs do bridge encontrados e corrigidos no caminho:** hash negativo é válido (`GetStableHashCode` pode ser < 0;
  o dump escondia `AutoPickup`, `Follow`, `Recovering`…), defaults do ZDO (`AutoPickup`/`Follow`/`RecoverPickup`
  ausentes valem `true`), `Inventory.AddItem` enche **um** stack (moedas 999 → `set 2000` agora soma em vários
  stacks) e `log_watch` devolvia o cursor no fim do chunk (pulava linhas em sequência).
- `world_time` reportou `dayLength = 1800` (não 1200): usar o campo retornado, nunca hardcode.
- `ApplyActionMode` grava via `MHR_WriteOrQueue`: espere ~1 s antes de ler o modo ou trocar de tier.

> **Nota:** as evidências acima são do plugin **standalone** (mundo `test3`); a validação do módulo **na DLL** está na
> subseção *Validação na DLL* — 24 checks, 0 falhas.

## Próximos passos

1. **P0 — feito (31/10/2026):** validação na DLL no mundo `test3` — **24 checks, 0 falhas** (ver acima). O mundo ficou
   com um companheiro a mais do `companions_spawn`; dispensar pelo painel/roda se quiser.
2. **P1 — o que sobrou**: o pré-check visual do Santuário (`ShrinePanel.Summon`, antes de abrir o painel) continua
   manual; `companions_hire` cobre a cobrança e o spawn, não o clique.
3. **P2 — conforto**: `companions.order` com `amount` (teto da coleta via `SetOrderCap`), `zdo_set`, empacotamento do
   servidor Python.

## Armadilhas (já resolvidas, não repetir)

- `EnvMan.GetCurrentDay()` é **privado** → reflexão (`Commands.DayNumber`).
- O console do jogo é `Console : Terminal` (global) → usar `global::Console` para não colidir com `System.Console`.
- `ScreenCapture.CaptureScreenshot` grava **no fim do quadro** → o servidor espera o arquivo (até 4 s).
- `companions.order` sem `name` usa o **primeiro** companheiro — com vários, passe o nome.
- O módulo é compilado junto: renomear um campo `*Hash` do mod **quebra o build** (é o que se quer).

## Segurança

- Bind **só** em `127.0.0.1`; nunca em `0.0.0.0`.
- `Token` opcional para máquina compartilhada.
- Ele executa comandos de console e spawna qualquer coisa: é ferramenta de dev. **Desligado por padrão** e o
  `CONFIGURATION.md` diz isso na descrição da chave.
