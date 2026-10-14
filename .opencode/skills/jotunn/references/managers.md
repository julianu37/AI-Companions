# Managers do Jotunn (2.30.2)

Todos os managers são **singletons** acessados por `XxxManager.Instance`. As classes ficam em
`Jotunn.Managers`. Cada manager expõe eventos (`OnVanilla...Available`) que indicam quando os
prefabs vanilla já podem ser clonados. Referência confirmada na fonte do Jotunn v2.30.2.

## Tabela resumida

| Manager | Para quê | Métodos-chave |
|---|---|---|
| `PrefabManager` | Cria/clona prefabs e consulta o cache de prefabs. | `AddPrefab`, `CreateEmptyPrefab`, `CreateClonedPrefab`, `GetPrefab`; evento `OnVanillaPrefabsAvailable` |
| `ItemManager` | Registra itens, receitas, efeitos de status e conversões. | `AddItem`, `AddRecipe`, `AddStatusEffect`, `AddItemConversion`, `GetItem`; evento `OnVanillaItemsAvailable` |
| `PieceManager` | Registra peças de construção, piece tables e categorias. | `AddPiece`, `AddPieceTable`, `AddPieceCategory`, `GetPiece`; evento `OnPiecesRegistered` |
| `ZoneManager` | Adiciona locais, vegetação e clutter aos biomas. | `AddCustomLocation`, `AddCustomVegetation`, `AddCustomClutter`, `CreateLocationContainer` |
| `CreatureManager` | Registra criaturas customizadas (com IA/IA do jogo). | `AddCreature`, `GetCreature`, `GetCreaturePrefab`; evento `OnVanillaCreaturesAvailable` |
| `LocalizationManager` | Adiciona traduções por token e resolve idiomas. | `GetLocalization`, `AddLocalization`, `AddToken`, `TryTranslate`; evento `OnLocalizationAdded` |
| `GUIManager` | Cria UI estilo Valheim (painéis, botões, texto) e bloqueia input. | `CreateWoodpanel`, `CreateButton`, `CreateText`, `BlockInput`, `GetSprite`; evento `OnCustomGUIAvailable` |
| `CommandManager` | Registra comandos de console/chat. | `AddConsoleCommand(ConsoleCommand)` |
| `RenderManager` | Renderiza ícones (Sprites) de prefabs em runtime. | `Render`, `EnqueueRender`; `IsometricRotation` |
| `SynchronizationManager` | Sincroniza configs do mod no servidor/cliente. | `RegisterCustomConfig(ConfigFile)`, `AddInitialSynchronization` |
| `InputManager` | Registra botões/atalhos e converte input de gamepad. | `AddButton(modGuid, ButtonConfig)`; helpers de `GamepadButton` |
| `KeyHintManager` | Registra dicas de teclas (hints) exibidas no HUD. | `AddKeyHint(KeyHintConfig)`, `RemoveKeyHint` |
| `SkillManager` | Cria skills customizadas. | `AddSkill(SkillConfig)`, `GetSkill` |
| `MinimapManager` | Desenha overlays/mapas customizados no minimapa. | `GetMapOverlay`, `GetMapDrawing`, `WorldToOverlayCoords` |
| `KitbashManager` | Adiciona "kitbash" (peças combinadas) a prefabs. | `AddKitbash(GameObject, KitbashConfig)` |

> **`ObjectManager` não existe no Jotunn 2.30.2.** Não existe uma classe `Jotunn.Managers.ObjectManager`.
> Objetos são tratados pelos managers específicos: `PrefabManager` (prefabs genéricos),
> `PieceManager` (peças) e `ZoneManager` (locais/vegetação/clutter). Confirme sempre na fonte antes de usar.

## Exemplo: registro típico em `Awake`

```csharp
private void Awake()
{
    // Prefabs vanilla ainda não estão prontos em Awake -> esperar o evento.
    PrefabManager.OnVanillaPrefabsAvailable += AddContent;
}

private void AddContent()
{
    // Comando de console
    CommandManager.Instance.AddConsoleCommand(new MyCommand());

    // Skill customizada
    SkillManager.Instance.AddSkill(new SkillConfig
    {
        Name = "$skill_modia",
        Description = "$skill_modia_desc",
        IncreaseStep = 1f,
    });

    // Dica de tecla (o KeyHintConfig usa ButtonConfig[])
    KeyHintManager.Instance.AddKeyHint(new KeyHintConfig
    {
        Item = "ModIA_CompanionSword",
        ButtonConfigs = new[] { new ButtonConfig { Name = "Use", Hint = "Usar" } },
    });

    // Executar só uma vez: o Jotunn guarda o conteúdo em cache.
    PrefabManager.OnVanillaPrefabsAvailable -= AddContent;
}
```

## Ordem de inicialização

- `Awake` do seu plugin: registre **localização, comandos, skills e configs**. Prefabs vanilla
  ainda **não** existem.
- `PrefabManager.OnVanillaPrefabsAvailable`: momento seguro para **clonar** prefabs vanilla e
  registrar itens/peças/criaturas.
- Sincronização de config: registre o `ConfigFile` em `SynchronizationManager` após criar as entries.
