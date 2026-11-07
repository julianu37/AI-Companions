---
name: Valheim DevBridge (MCP)
description: Como dirigir o Valheim pelo DevBridge â€” o mÃ³dulo in-game (TCP 127.0.0.1:47110, sobe com o mod) e o adaptador MCP (tools/mcp-devbridge), com world_enter, companions/build/level, build.pieces/delete, logs, screenshots e as receitas de teste em jogo. Use sempre que precisar testar o mod no jogo.
---

# DevBridge (MCP de desenvolvimento)

## O que Ã© (e o que sobe com o quÃª)

- **MÃ³dulo in-game:** `src/Companions/DevBridge/`, compilado na `Companions.dll`. **Sobe com o mod** quando
  `[Advanced] DevBridge = true` (padrÃ£o desligado), ouvindo `127.0.0.1:47110` (loopback). Ele roda comandos de
  console, spawna coisas e mexe no mundo: **nÃ£o** Ã© feature de jogador.
- **Adaptador MCP:** `tools/mcp-devbridge/mcp_devbridge.py`, configurado em `.opencode/opencode.jsonc`
  (`mcp.servers.valheim-devbridge`). Ã‰ ele que o OpenCode *spawna* para expor as ferramentas. Se as ferramentas
  nÃ£o aparecerem, o adaptador nÃ£o subiu â€” **o lado do jogo pode estar de pÃ© mesmo assim**. DÃ¡ para falar direto:
  `import mcp_devbridge; mcp_devbridge.call_bridge("ping")` (um JSON por linha, uma conexÃ£o por chamada).
- **Antes de tudo:** `devbridge_ping` â†’ `{"inGame": false}` = menu principal; `world_enter` carrega o **Ãºltimo mundo**
  (Space â†’ Down â†’ Enter Ã—3) e roda o setup (`devcommands` uma vez, `tod 0.5`, `god`, `heal`, stamina infinita).
- **Doc completo:** `docs/mcp-devbridge.md` (arquitetura/protocolo/57 ferramentas) e
  `tools/mcp-devbridge/README.md` (parÃ¢metros e exemplos).

## Receita de teste de construÃ§Ã£o

1. **Radiografar antes de ordenar:** `build_pieces` (raio em volta do canteiro), `terrain_check` (variaÃ§Ã£o do
   terreno; `flat=true` pula o nivelamento), `companions_inventory` (machado/picareta/enxada) e `companions_zdo`
   (`ActionMode`, `Follow`, `Recovering`).
2. **Ferramentas na mochila:** `companions_give` (`item`, `amount`, `name`). O nivelamento precisa de `Hoe`; o
   machado/picareta vÃªm do tier.
3. **Ordenar:** `companions_build` aceita `prefab` (allow-list), `plan` (`hut`/`canopy`) ou
   `blueprint` (`.blueprint` em `BepInEx/plugins/AI-Companions/blueprints`, nome ou caminho completo), com `name` e
   Ã¢ncora `x/y/z` opcional. Sem `x/y/z`, a Ã¢ncora Ã© a mira do jogador (`player_look` com `down` positivo olha para
   baixo; `world_aim` mostra onde a mira bate). O blueprint Ã© **centrado no hint** (o `y` Ã© o chÃ£o).
4. **Acompanhar:** `log_tail`/`log_watch` (linhas `[Build] ... next: (N/983)` sÃ£o o Ã­ndice real), `build_pieces`
   para contar o que estÃ¡ de pÃ©, `companions_list` para posiÃ§Ã£o/HP. O build anda rÃ¡pido: o log pode estar alguns
   segundos atrÃ¡s da contagem.
5. **Limpar:** `build_delete` (destrÃ³i peÃ§as de jogador no raio; `filter` por nome, `drops` leva os itens soltos).
   O raio Ã© **3D** â€” numa casa alta, use fatias de `y` (ex.: `y+7/8`) para nÃ£o deixar o telhado. **Nunca** apague a
   base do jogador: confira `build_pieces` (`mine`, `creator`, posiÃ§Ã£o) e limite o raio/coordenadas ao canteiro.
6. **Prova visual:** `screenshot` (imagem) ou `render_view` (cÃ¢mera independente, `x/z` + `yaw`/`pitch`/`dist`).

## Armadilhas que jÃ¡ custaram caro

- **`companions_order` cancela obra e nivelamento.** NÃ£o use â€œpara conferirâ€ no meio de um build; para parar, Ã©
  intencional.
- **`devcommands` Ã© toggle.** `world_enter`/`world_setup` sÃ³ mandam uma vez; nÃ£o repita no escuro.
- **`player_kill` recusa em `god`/ghost/fly.** Desligue com `game_console` antes.
- **`build_delete` usa distÃ¢ncia 3D do centro** e o `forcedelete` do jogo tem teto de raio; para varrer Ã¡rea grande,
  chame por fatias.
- **O schema do adaptador pode ficar atrÃ¡s do comando C#.** Se o tool recusar um argumento que o C# aceita (foi o
  caso do `blueprint`), chame `call_bridge` direto e **corrija o schema/payload** no mesmo ciclo.
- **`log_tail` devolve as Ãºltimas N linhas no momento da chamada**, nÃ£o um stream. Para esperar um evento, use
  `log_watch` (com `since` do cursor anterior); para progresso, leia o Ãºltimo `next: (N/M)`.
- **Multiplayer/dedicado:** o bridge Ã© do processo local; nÃ£o assume que o ZDO/posse do outro jogador estÃ£o
  disponÃ­veis.

## Checklist do teste em jogo

- [ ] `devbridge_ping` com `inGame: true` e o mundo certo.
- [ ] Canteiro radiografado e base do jogador preservada (coordenadas/raio).
- [ ] Ferramentas e materiais mÃ­nimos; `missing`/`missingStation` vazios no `companions_build`.
- [ ] Progresso por `next: (N/M)` e `build_pieces`, com `[Build]` sem `TRAVADO`/`stuck` repetido.
- [ ] Retomada apÃ³s coleta/morte (mesma Ã¢ncora e mesma peÃ§a) quando o teste exigir.
- [ ] Limpeza com `build_delete` por fatias e screenshot final.
