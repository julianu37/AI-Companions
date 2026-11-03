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
| `companions.give` | `item`, `amount`, `name?` | `item`, `amount`, `added` (ou `removed` com `amount` **negativo**) |
| `player.stamina` | `on?` (padrão `true`) | `infiniteStamina`, `stamina`, `maxStamina` |
| `player.teleport` | `x`, `y`, `z` | `x`, `y`, `z` |
| `player.look` | `down?`, `right?` (graus) | `down`, `right`, `forward{}`, `distance`, `x`, `y`, `z` |
| `world.spawn` | `prefab`, `count`, `x?`, `y?`, `z?` | `prefab`, `count` |
| `build.inspect` | `prefab` | `found`, `name`, `resources[]`, `station{}`, `flags{}`, `space{}`, `wearNTear{}`, `craftingStation{}`, `inHammer`, `recipeKnown` |
| `build.sites` | `prefab`, `x?`, `y?`, `z?`, `radius?`, `rings?`, `dirs?` | `total`, `valid`, `points[]` (cada um com `status`, `reasons[]`, `ground{}`, `biome`, `cultivated`, `hasStation`, `wardOk`, `noBuildZone`) |
| `build.delete` | `x?`, `y?`, `z?`, `radius?`, `filter?`, `drops?` | `destroyed`, `items` (limpeza de área de teste: apaga as peças num raio de até 120 m; `filter` por nome, `drops` leva os itens soltos junto) |
| `build.pieces` | `x?`, `y?`, `z?`, `radius?`, `filter?` | `count`, `pieces[]` (`prefab`, `name`, `creator`, `mine`, `distance`, `zdo`, `health`, `station`) |
| `build.find` | `prefab`, `x?`, `y?`, `z?`, `radius?`, `rings?`, `dirs?` | `ok`, `status`, `pos{}`, `yaw`, `distance`, `tried`, `reasons[]`, `tally{}` |
| `build.check` | `prefab`, `x`, `y`, `z`, `yaw?` | o **mesmo** veredito do `BuildSite.Check` numa pose **exata** (o `y` não é achatado no chão - é assim que se testa parede sobre piso e telhado sobre parede) |
| `build.snaps` | `prefab` | `count`, `points[]` (`name`, `x`, `y`, `z`) - os `snappoint` do prefab (a origem dos deslocamentos dos templates da E6) |
| `build.usable` | `x?`, `y?`, `z?` | `station`, `usable` (o `CraftingStation.CheckUsable` do jogo: a regra do **telhado**, 70% de cobertura), `cover`, `underRoof`, `x/y/z` da estação **mais próxima** |
| `companions.build` | `prefab?`, `plan?`, `blueprint?`, `name?`, `x/y/z?` | `started`, `blueprint`, `pieces`, `missing`, `missingStation`, `name` |
| `companions.sethome` | `name?` | `name`, `x`, `y`, `z` |
| `screenshot` | `path?` | `path` |
| `render.view` | `x`, `z`, `y?`, `yaw?`, `pitch?`, `dist?`, `size?` | `path`, `size`, `bytes`, `target{}`, `camera{}` |
| `input.focus` | — | `focused`, `window` |
| `input.key` / `input.key_down` / `input.key_up` | `key` | `key`, `vk`, `mode`, `sent`, `focused` |
| `input.text` | `text` | `text`, `sent`, `focused` |
| `input.click` | `x`, `y` (pixels da tela do jogo) | `x`, `y`, `sent`, `focused` |
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

`companions.order` **cancela `MHR_Build` e `MHR_Level`** em qualquer ação aceita (ordem de um tiro, como no
chat/roda) e aceita: `follow`, `wood`, `stone`, `ore`, `forage`, `smelt`, `hunt`, `farm`, `fish`, `repair`,
`restock`, `cook`, `stay`, `stance_balanced`, `stance_passive`, `stance_ranged`, `stance_melee`, `autopickup_on`,
`autopickup_off`.

`companions.zdo` devolve as chaves `HC_*`: `Name`, `Owner`, `CompanionId`, `ActionMode`, `CombatStance`, `AutoPickup`,
`Wander`, `Follow`, `StayHome`, `HomeRadius`, `OrderCap`, `OrderCapMode`, `OrderCapItem`, `TombstoneId`, `Stamina`,
`Recovering`, `RecoverStance`, `RecoverPickup`, `Voice`, `Gen`, `FarmZones` (`"<none>"` ou `"N chars"`).

## Ferramentas MCP (57 em 03/11/2026)

Genéricas: `devbridge_ping`, `game_status` (posição, hp/stamina, bioma, dia, **yaw** do player, **camYaw/pitch** da
câmera e **god**), `game_console` (**roda um comando do console e devolve a saída**: liga os dev commands sozinho e,
num `spawn`, diz se o prefab existe), `player_give`, `player_teleport`, `player_kill`, `player_inventory`, `world_spawn`,
`world_time`, `world_aim` (o que está **sob a mira**: nome, distância, ponto, se é árvore/criatura), `world_nearest`
(a **árvore** ou **criatura** mais próxima — o alvo para mirar), `world_prefabs` (os nomes de prefab que o mundo
conhece, filtrados — é como se descobre `AxeBlackMetal` em vez de adivinhar), `world_setup` (**devcommands + `tod` +
`god on` + `heal`** — o setup para dirigir o player), `screenshot` (a visão do jogador), `render_view` (câmera
**off-screen**, não mexe na visão do jogador), `log_tail`, `log_watch`.

Construção (E0 da Trilha E): `build_inspect` (a **ficha** de uma peça construível, lida do prefab no jogo: receita,
estação, **todas** as flags de posicionamento do `Piece`, regras de espaço/conexão, `WearNTear`, raio de ward, se está
no martelo e se a receita é conhecida) e `build_sites` (o veredito de **anéis de pontos** em volta do jogador ou de
`x/y/z`, com status estável — `valid`, `no_ground`, `in_water`, `no_build_zone`, `ward`, `needs_station`… — sem colocar
nada). `build_pieces` (E1) lista as peças **já no mundo** num raio (prefab, nome, criador, `mine`, distância, `zdo`,
vida, estação) — é como o teste de relog foi provado. É a mesma função dos comandos `modia_build_probe` e
`modia_build_test` (`AI/BuildProbe.cs`, `AI/BuildExecutor.cs`); a medição está em `docs/re/building-notes.md`.

Input (teclado/mouse no **Unity Input System** — o caminho que o jogo lê — e também no `SendInput` legado, que é o
da UI do mod): `input_focus`, `input_key`, `input_key_down`, `input_key_up` (seguram teclas, então `Shift+W` corre e
`W` anda), `input_text`, `input_click`, `input_mouse_down`, `input_mouse_up`, `input_mouse_move` (**gira a câmera**:
manda o delta do `<Mouse>/delta`, que é o eixo do `ZInput.GetMouseDelta`).

Companheiros: `companions_list`, `companions_spawn`, `companions_order`, `companions_teleport`, `companions_give`,
`companions_zdo`, `companions_kill`, `companions_dismiss` (**o mesmo caminho do menu radial**: equipamento vai para a
lápide e o corpo sai), `companions_inventory` (o que ele carrega), `companions_tier`, `companions_hire`,
`companions_resurrect`, `companions_farm_zone`.

Lápides: `world_tombstones` (as carregadas perto de você com **itens, `inUse`, `isOwner`, `tombstoneId`,
`respawnPrefab` e o conteúdo**), `world_tombstone_take` (saque pelo `Container.TakeAll`, o mesmo do "cabe na
mochila"), `world_tombstone_destroy` (limpeza de teste) e `world_tombstone_diag` (estado do componente,
`IsInvoking`, ZNetView válido e um `tick` que chama o `UpdateDespawn` privado).

Parâmetros e exemplos: `tools/mcp-devbridge/README.md`.

## Estado (31/10/2026)

| prova | resultado |
|---|---|
| build do mod com o módulo | **0 erros**, 1 aviso conhecido (`s_noDark`) |
| teste do servidor MCP (mock, sem jogo) | **47 PASS / 0 FAIL** (46 ferramentas) |
| teste do parser JSON em C# | **11 PASS / 0 FAIL** |
| plugin separado no jogo | **removido**; agora só existe o módulo dentro da `Companions.dll` |
| ponta a ponta no jogo, módulo **standalone** (mundo `test3`) | **os 4 testes da 1.9.4 passaram** |
| ponta a ponta no jogo, módulo **na DLL** (mundo `test3`, 31/10/2026) | **24/24 PASS** — as **23 ferramentas**, incluindo kill/resurrect/tier/hire |
| **E0 da construção** (mundo `test3`, 01/11/2026) | `build.inspect piece_workbench` devolveu a ficha real (Madeira x10, sem estação, `noInWater`/`notOnTiltingSurface`/`noClipping`, `supports=false`, `rangeBuild=20`); `build.sites` deu **11 válidos de 16** (2 `blocked_by_player`, 3 `no_build_zone`); `build.sites piece_chest_wood` deu **0 válidos** com `needs_station` (a prova da checagem de estação); o comando `modia_build_probe` gravou as 23 linhas no log. Detalhes e as 5 peças medidas: `docs/re/building-notes.md` |
| **controle do player** (mundo `test3`, 31/10/2026) | **andar, correr, pular, olhar, atacar e cortar uma árvore** — `Shift+W` gastou stamina (50 → 33,6), `mouse_move` girou a câmera, 16 golpes derrubaram a `Beech1` e a madeira entrou no inventário (2 → 3) |
| **setup automático ao entrar** (mundo `test3`, 31/10/2026) | `world_enter` devolveu `setup: {cheats: true, god: true, hp: 25}` e `dayFraction 0.5`; 3 greylings baixaram o HP para 10,76 e **o player não morreu** (o `god` do Valheim 1.0 trava o HP em 1), `heal` devolveu 25 |
| **comandos de console** (mundo `test3`, 31/10/2026) | `spawn PickaxeBlackMetal 5` → `ok: true, prefabFound: true`; `spawn AxeBlackmetal` → `prefabFound: false` (o certo é `AxeBlackMetal`); `tod 0.5` → saída real `Setting time of day:0,5` e `dayFraction 0.5`; comando inválido → `ok: false` com a mensagem do jogo; `demo_play.py` **12 PASS / 0 FAIL** |

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

## Receitas

### Entrar no mundo sozinho

**`world_enter`** faz a sequência que o jogador usa e espera o mundo carregar:

| passo | tecla |
|---|---|
| 1 | `Space` (pula a intro) |
| 2 | `DownArrow` (seleciona o mundo) |
| 3-5 | `Return` ×3 (abre/confirma/entra) |

Equivalente manual: `input_key` com essas teclas e ~1 s entre elas; depois `ping` até `inGame: true`. Se parar no
meio, `screenshot` mostra a tela.

**Depois de carregar, o `world_enter` roda o `world.setup` sozinho** (`setup: false` desliga): `devcommands` (só se
ainda não estiver ligado - o comando é um **toggle**), `tod 0.5` (meio-dia), `god on` + `heal` e **stamina infinita**
(`stamina`, padrão **ligado**; `stamina: false` desliga). Sem isso o agente morre para um greyling enquanto está
ocupado com o bridge.

### Stamina infinita nas sessões de teste

Pedido do jogador (02/11/2026): as sessões de teste de construção cortam muita madeira e a barra de stamina acaba
no meio. O `god` não ajuda nisso (ele só trava o HP em 1) e o console do jogo **não** tem comando de stamina - o
`setkey` desta build aceita **só o nome** (não existe `staminarate`). Então o DevBridge faz: com a opção ligada
(`world.setup` com `stamina` ou `player.stamina`), o `Pump` (main thread) **repõe a stamina a cada 0,25 s** quando
ela está abaixo do máximo. No Valheim 1.0 não existe setter (`Character` tem `GetStamina`/`UseStamina`, não
`SetStamina`), então o valor é escrito por **reflexão** no campo privado - a busca percorre a **hierarquia toda**
(`Player` → `Humanoid` → `Character`; na prática o campo é o `Player.m_stamina`) e ele é escolhido **pelo valor**
(o `float` cujo conteúdo é igual ao `GetStamina()` na hora), não pelo nome; uma linha no log diz qual foi
(`[DevBridge] stamina infinita: Player.m_stamina`). É ferramenta de dev: sem `[Advanced] DevBridge` nada disso roda.

### Deixar o player invencível (e por que o `god` sozinho não basta)

`world_setup` (ou `game_console` com `devcommands`, `tod 0.5`, `god on`, `heal`). **No Valheim 1.0 o `god` não
cancela o dano:** ele **trava o HP em 1** quando o golpe mataria (`Character.cs`, `health <= 0 && InGodMode() →
health = 1`). Medido em jogo: 3 greylings baixaram o HP de 25 para 10,76 em 15 s e **o player não morreu**; o `heal`
devolveu 25. Ou seja: `god` cumpre "não morrer", mas o HP continua caindo — para os inimigos **ignorarem** o player
existe o `ghost on` (comando do jogo, não ligado por padrão).

### Rodar comandos de console (`devcommands`, `spawn`, `tod`)

`game_console` roda **o mesmo comando que você digitaria no F5** e devolve o que ele imprimiu:

| pedido | chamada | resposta |
|---|---|---|
| ligar os cheats | `game_console command=devcommands` | `{"cheats": true}` (o bridge só manda uma vez: o comando é **toggle**) |
| item no chão | `game_console command="spawn PickaxeBlackMetal 5"` | `{"ok": true, "prefabFound": true, "output": []}` |
| meio-dia | `game_console command="tod 0.5"` | `{"ok": true, "output": ["Setting time of day:0,5"]}` |
| comando errado | `game_console command=naoexiste` | `{"ok": false, "output": ["'naoexiste' is not a recognized command..."]}` |

- **Os cheats vêm ligados por padrão** (`cheats: true`): sem `devcommands` o jogo **recusa** `spawn`/`tod`/`god` em
  silêncio. O `game_console` liga antes de rodar e só manda o `devcommands` se `Console.m_cheat` estiver falso.
- **A saída é real:** o bridge lê o `Terminal.m_chatBuffer` (por reflexão) antes e depois e devolve só as linhas novas
  — é assim que `tod` diz "Setting time of day:0,5" e o `help` devolve a lista inteira.
- **`spawn` avisa se o prefab existe** (`prefabFound`). Isso pega erro de digitação/caixa: **`AxeBlackmetal` não
  existe**, o certo é **`AxeBlackMetal`** (o Valheim é case-sensitive). Para descobrir o nome, use
  **`world_prefabs filter=axe`** → `AxeBlackMetal`, `AxeBronze`, `AxeFlint`...; `filter=pickaxe` → `PickaxeBlackMetal`...
- `spawn` **joga o item no chão** (aos pés do player). Para ir direto para a mochila, `player_give`.
- `spawn` aceita os extras do jogo: `spawn <prefab> [amount] [level] [radius] [p/e/i]` — `p` tenta pegar, `e` equipa.
  Ex.: `spawn AxeBlackMetal 1 1 1 e` já equipa o machado.

### Testar um painel (ex.: o bind do F8)

1. `input_key F8` → `screenshot` (o painel abriu).
2. `input_click` na linha da tecla → `input_key Escape`.
3. `screenshot`: o painel **continua aberto** e o bind foi cancelado (o fix do `Capturing`).

### Ver o mundo de outro ângulo

`render_view` com `x`/`z` do ponto, `yaw`/`pitch` orbitando e `dist` — a câmera é off-screen, não mexe na visão do jogador.

### Jogar pelo agente (andar, correr, pular, olhar, atacar, cortar árvore)

Provado em jogo no mundo `test3` (31/10/2026): o agente andou, correu, pulou, girou a câmera, atacou com o machado e
**derrubou uma árvore** ("Adicionado(a) Madeira", tronco no chão, madeira no inventário). A receita:

1. **Andar/correr:** `input_key_down W` → `time.sleep(...)` → `input_key_up W`. Para correr, segure
   `LeftShift` **junto** (`input_key_down LeftShift`, `input_key_down W`, depois solte os dois). O estado do teclado
   acumula as teclas seguradas (`HashSet<Key> Held`), senão o segundo `key_down` soltava o primeiro.
2. **Pular:** `input_key Space`. Custa stamina (50 → 39,5).
3. **Olhar (câmera):** `input_mouse_move dx dy` — manda o **delta** do `<Mouse>/delta`, zerado no frame seguinte. A
   calibração medida no jogo é **0,05 grau/px** (`dx = erro_em_graus / 0,05`).
4. **Mirar em algo real:** `world_nearest kind=tree` (ou `creature`) dá a posição do alvo; `status` dá `x/z` do player
   e `camYaw`. O yaw do alvo é `atan2(tx - px, tz - pz)` em graus; gire com `input_mouse_move` até o erro < 1,5°.
   `world_aim` confirma o que está na mira (`tree: true`, distância, ponto).
5. **Chegar perto:** segure `W` em passos de 0,5 s até `world_nearest` dizer ~3 m.
6. **Atacar:** `input_mouse_down` + `input_mouse_up` no centro da tela (840, 525 no cliente 1680×1050). Cada golpe de
   machado custa ~14 de stamina; a árvore cai e a madeira entra no inventário (`player_inventory`).

Números medidos: andar ~3,5 m/s; correr ~5,1 m/s (stamina 50 → 33,6 em 2 s); 16 golpes derrubaram a `Beech1`.

## Armadilhas (já resolvidas, não repetir)

- **O `ZInput` do Valheim 1.0 lê o Unity Input System NOVO** (`Keyboard.current[key].wasPressedThisFrame`), não o
  `Input` legado. `SendInput` sozinho **não** alimenta o menu do jogo: por isso o F8 do mod (legado) funcionava e o
  menu não. `input.key` agora faz as duas coisas: `SendInput` (UI do mod) **e**
  `InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(key))`, com a soltura no frame seguinte
  (`BridgeServer.Pump` → `InputSim.PumpNew`). Foi o que destravou o `world_enter`.
- **Foco:** o Windows bloqueia `SetForegroundWindow` vindo do processo do jogo em background. O servidor MCP roda sob o
  app em foco e foca a janela (`FindWindowW("UnityWndClass")`) antes de cada input; o bridge ainda faz `SetFocus` na
  thread dona da janela.
- **Mouse:** o clique e o olhar vão por `SendInput` **e** pelo Input System (`MouseState.WithButton`,
  `<Mouse>/delta`). No **jogo** isso funciona (atacar, mirar, girar a câmera); na UI do **mod** (uGUI/`Input` legado)
  só chega com a janela realmente em primeiro plano — a injeção do Input System não chega nela.
- **Teclas seguradas:** um `QueueStateEvent` **substitui** o estado inteiro do teclado. `input_key_down` guarda as
  teclas num `HashSet<Key>` e manda todas juntas; sem isso o `Shift+W` corria como `W` (o segundo `key_down` soltava o
  primeiro) e o `key_up` de uma tecla soltava todas.
- `EnvMan.GetCurrentDay()` é **privado** → reflexão (`Commands.DayNumber`).
- O console do jogo é `Console : Terminal` (global) → usar `global::Console` para não colidir com `System.Console`.
- `ScreenCapture.CaptureScreenshot` grava **no fim do quadro** → o servidor espera o arquivo (até 4 s).
- `companions.order` sem `name` usa o **primeiro** companheiro — com vários, passe o nome.
- O módulo é compilado junto: renomear um campo `*Hash` do mod **quebra o build** (é o que se quer).
- **Encher um baú não tem ferramenta** (pedido do jogador em 03/11/2026: *"criar 4 baús cheios de madeira do lado do
  player"* — **cancelado** por ele quando soube do custo). O `world.spawn` cria o `piece_chest_wood` mas ele nasce
  **vazio**, e o conteúdo de um `Container` só chega ao ZDO pelo `Container.Save` (**privado**, disparado no
  `Inventory.m_onChanged` → `OnContainerChanged`); o bridge escreve só chaves tipadas (`companions.zdo`), não o
  `byte[]` dos itens. Encher exigiria um comando novo (`world.chest`: `Player.PlacePiece` + `Inventory.AddItem`), e
  comando novo só vale **depois de reiniciar o jogo** (a DLL carrega no boot) — o que derrubaria a obra de 985 peças
  que estava em andamento. Alternativa sem reinício: `player.give` põe a madeira no inventário do jogador (mas ele
  fica acima do peso e anda travado), e o companheiro já corta o que falta pelo próprio canteiro.

## Segurança

- Bind **só** em `127.0.0.1`; nunca em `0.0.0.0`.
- `Token` opcional para máquina compartilhada.
- Ele executa comandos de console e spawna qualquer coisa: é ferramenta de dev. **Desligado por padrão** e o
  `CONFIGURATION.md` diz isso na descrição da chave.
