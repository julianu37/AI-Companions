---
name: Jotunn
description: Como usar o framework Jotunn no Valheim — Managers, prefabs, itens, receitas, peças, localização, GUI e comandos. Use ao criar conteúdo de jogo.
---

# Jotunn (Valheim Modding Library)

Jotunn é o framework que abstrai a criação de conteúdo e a interoperabilidade de mods no Valheim.
Versão alvo: **2.30.2**.

## Quando usar

Sempre que for criar/registrar conteúdo: prefabs, itens, receitas, peças, criaturas, localização,
GUI, comandos, vegetação, locais (ZoneSystem) ou sincronização de config.

## Managers principais

- `PrefabManager`, `ItemManager`, `PieceManager`, `ZoneManager`, `CreatureManager`
- `LocalizationManager`, `GUIManager`, `CommandManager`, `RenderManager`, `SynchronizationManager`

Detalhes em `references/managers.md`. Conteúdo em `references/prefabs-itens-receitas.md` e
`references/localizacao-gui.md`. Template em `templates/JotunnSetup.cs`.

## Regras

- Declare dependência e compatibilidade no plugin:
  `[BepInDependency(Jotunn.Main.ModGuid)]` e `[NetworkCompatibility(CompatibilityLevel..., VersionStrictness...)]`.
- Registre o conteúdo na fase correta (`Awake`/`Start`) respeitando a ordem de init dos managers.
- Confirme nomes/assinaturas na **fonte do Jotunn** (reference `jotunn`) — não de memória.
- Para assets, combine com a skill `valheim-assets`.

## Interface com o mod

O menu de customização do NPC (padrão do Thrall) usa `GUIManager.Instance.CreateWoodpanel(...)` —
veja `references/localizacao-gui.md`.
