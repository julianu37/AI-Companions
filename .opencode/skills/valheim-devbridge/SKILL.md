---
name: Valheim DevBridge (MCP)
description: Como dirigir o Valheim pelo DevBridge — o módulo in-game (TCP 127.0.0.1:47110, sobe com o mod) e o adaptador MCP (tools/mcp-devbridge), com world_enter, companions/build/level, build.pieces/delete, logs, screenshots e as receitas de teste em jogo. Use sempre que precisar testar o mod no jogo.
---

# DevBridge (MCP de desenvolvimento)

## O que é (e o que sobe com o quê)

- **Módulo in-game:** `src/Companions/DevBridge/`, compilado na `Companions.dll`. **Sobe com o mod** quando
  `[Advanced] DevBridge = true` (padrão desligado), ouvindo `127.0.0.1:47110` (loopback). Ele roda comandos de
  console, spawna coisas e mexe no mundo: **não** é feature de jogador.
- **Adaptador MCP:** `tools/mcp-devbridge/mcp_devbridge.py`, configurado em `.opencode/opencode.jsonc`
  (`mcp.servers.valheim-devbridge`). É ele que o OpenCode *spawna* para expor as ferramentas. Se as ferramentas
  não aparecerem, o adaptador não subiu — **o lado do jogo pode estar de pé mesmo assim**. Dá para falar direto:
  `import mcp_devbridge; mcp_devbridge.call_bridge("ping")` (um JSON por linha, uma conexão por chamada).
- **Antes de tudo:** `devbridge_ping` → `{"inGame": false}` = menu principal; `world_enter` carrega o **último mundo**
  (Space → Down → Enter ×3) e roda o setup (`devcommands` uma vez, `tod 0.5`, `god`, `heal`, stamina infinita).
- **Doc completo:** `docs/mcp-devbridge.md` (arquitetura/protocolo/57 ferramentas) e
  `tools/mcp-devbridge/README.md` (parâmetros e exemplos).

## Receita de teste de construção

1. **Radiografar antes de ordenar:** `build_pieces` (raio em volta do canteiro), `terrain_check` (variação do
   terreno; `flat=true` pula o nivelamento), `companions_inventory` (machado/picareta/enxada) e `companions_zdo`
   (`ActionMode`, `Follow`, `Recovering`).
2. **Ferramentas na mochila:** `companions_give` (`item`, `amount`, `name`). O nivelamento precisa de `Hoe`; o
   machado/picareta vêm do tier.
3. **Ordenar:** `companions_build` aceita `prefab` (allow-list), `plan` (`hut`/`canopy`) ou
   `blueprint` (`.blueprint` em `BepInEx/config/AI-Companions/blueprints`, nome ou caminho completo), com `name` e
   âncora `x/y/z` opcional. Sem `x/y/z`, a âncora é a mira do jogador (`player_look` com `down` positivo olha para
   baixo; `world_aim` mostra onde a mira bate). O blueprint é **centrado no hint** (o `y` é o chão).
4. **Acompanhar:** `log_tail`/`log_watch` (linhas `[Build] ... next: (N/983)` são o índice real), `build_pieces`
   para contar o que está de pé, `companions_list` para posição/HP. O build anda rápido: o log pode estar alguns
   segundos atrás da contagem.
5. **Limpar:** `build_delete` (destrói peças de jogador no raio; `filter` por nome, `drops` leva os itens soltos).
   O raio é **3D** — numa casa alta, use fatias de `y` (ex.: `y+7/8`) para não deixar o telhado. **Nunca** apague a
   base do jogador: confira `build_pieces` (`mine`, `creator`, posição) e limite o raio/coordenadas ao canteiro.
6. **Prova visual:** `screenshot` (imagem) ou `render_view` (câmera independente, `x/z` + `yaw`/`pitch`/`dist`).

## Armadilhas que já custaram caro

- **`companions_order` cancela obra e nivelamento.** Não use “para conferir” no meio de um build; para parar, é
  intencional.
- **`devcommands` é toggle.** `world_enter`/`world_setup` só mandam uma vez; não repita no escuro.
- **`player_kill` recusa em `god`/ghost/fly.** Desligue com `game_console` antes.
- **`build_delete` usa distância 3D do centro** e o `forcedelete` do jogo tem teto de raio; para varrer área grande,
  chame por fatias.
- **O schema do adaptador pode ficar atrás do comando C#.** Se o tool recusar um argumento que o C# aceita (foi o
  caso do `blueprint`), chame `call_bridge` direto e **corrija o schema/payload** no mesmo ciclo.
- **`log_tail` devolve as últimas N linhas no momento da chamada**, não um stream. Para esperar um evento, use
  `log_watch` (com `since` do cursor anterior); para progresso, leia o último `next: (N/M)`.
- **Multiplayer/dedicado:** o bridge é do processo local; não assume que o ZDO/posse do outro jogador estão
  disponíveis.

## Checklist do teste em jogo

- [ ] `devbridge_ping` com `inGame: true` e o mundo certo.
- [ ] Canteiro radiografado e base do jogador preservada (coordenadas/raio).
- [ ] Ferramentas e materiais mínimos; `missing`/`missingStation` vazios no `companions_build`.
- [ ] Progresso por `next: (N/M)` e `build_pieces`, com `[Build]` sem `TRAVADO`/`stuck` repetido.
- [ ] Retomada após coleta/morte (mesma âncora e mesma peça) quando o teste exigir.
- [ ] Limpeza com `build_delete` por fatias e screenshot final.
