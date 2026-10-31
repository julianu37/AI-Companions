---
name: Jotunn
description: Referência do framework Jotunn (Managers, prefabs, itens, receitas, peças, localização, GUI, comandos). NÃO é usado por este mod — consulte apenas se um dia decidirmos adotá-lo.
---

# Jotunn (Valheim Modding Library) — referência, **não usado aqui**

> ⚠️ **Este mod NÃO usa Jotunn.** O `.csproj` não o referencia, o plugin não declara
> `[BepInDependency]` dele e há **zero** chamadas de API no código. A única relação é defensiva: a pasta
> `Speech/` fica fora de `Translations/` porque o carregador de localização do Jotunn trataria
> `Translations\<pasta>\*.json` como um idioma.
>
> **Não** adicione Jotunn a `Companions.csproj`, **não** o liste como requisito em release/README e **não**
> delegue trabalho "de Jotunn" neste projeto. Se um dia adotarmos, aí sim esta skill vale.

Jotunn é o framework que abstrai a criação de conteúdo e a interoperabilidade de mods no Valheim.
Versão de referência: **2.30.2**.

## Quando valeria a pena

Criar/registrar conteúdo: prefabs, itens, receitas, peças, criaturas, localização, GUI, comandos,
vegetação, locais (ZoneSystem) ou sincronização de config.

## Managers principais

- `PrefabManager`, `ItemManager`, `PieceManager`, `ZoneManager`, `CreatureManager`
- `LocalizationManager`, `GUIManager`, `CommandManager`, `RenderManager`, `SynchronizationManager`

Detalhes em `references/managers.md`. Conteúdo em `references/prefabs-itens-receitas.md` e
`references/localizacao-gui.md`. Template em `templates/JotunnSetup.cs`.

## Regras

- Declare dependência e compatibilidade no plugin:
  `[BepInDependency(Jotunn.Main.ModGuid)]` e `[NetworkCompatibility(...)]`.
- Registre o conteúdo na fase correta (`Awake`/`Start`) respeitando a ordem de init dos managers.
- Confirme nomes/assinaturas na **fonte do Jotunn** (reference `jotunn`) — não de memória.
- Para assets, combine com a skill `valheim-assets`.

## O que o nosso mod usa no lugar

- Prefabs/aparência: clones de prefabs vanilla (`CompanionPrefabs`, `CompanionSetup.ApplyAppearance`).
- UI: painéis próprios de madeira (`UI/CompanionPanel.cs` e derivados), faixa **1701+** (`ModUiLayers`).
- Localização: `ModLocalization` + `Translations/*.json` (inglês nativo, sem arquivo).
- Comandos de console: `BaseUnityPlugin` + `ConsoleCommand` do BepInEx? Não — usamos `Terminal`/`Chat`
  do jogo e o comando `modia_tts_teste` via `Console` patch.
