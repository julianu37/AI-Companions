# Prefabs, itens, receitas, peças e efeitos (Jotunn 2.30.2)

Entidades customizadas herdam de `CustomEntity` (namespace `Jotunn.Entities`). Os *Configs*
(`Jotunn.Configs`) resolvem as referências do jogo em runtime, evitando `NullReferenceException`.

> Regra de ouro (dos próprios tutoriais do Jotunn): só referencie **nomes de prefabs existentes**.
> Clonagens de prefabs vanilla só podem ser feitas quando o evento
> `PrefabManager.OnVanillaPrefabsAvailable` disparar — não em `Awake`.

## Dependência do Jotunn no plugin

```csharp
using Jotunn;

[BepInPlugin("modia.companion", "ModIA Companion", "0.1.0")]
[BepInDependency(Main.ModGuid)] // "com.jotunn.jotunn"
public class CompanionPlugin : BaseUnityPlugin { /* ... */ }
```

## Item: `CustomItem` + `ItemConfig` (com ícone e requerimentos)

```csharp
// Ícone: um Sprite (asset bundle, AssetUtils.LoadSpriteFromFile, ou RenderManager.Render).
ItemConfig config = new ItemConfig
{
    Name = "$item_modia_totem",              // token localizável
    Description = "$item_modia_totem_desc",
    CraftingStation = CraftingStations.Workbench,
    Weight = 1.5f,
    StackSize = 1,
    Icons = new[] { icon },                   // Sprite[]; com >1 vira variante
};

// AddRequirement(item, quantidade, quantidadePorNível)
config.AddRequirement("Wood", 5, 2);
config.AddRequirement("Stone", 10, 5);

// Clona o prefab vanilla "SwordIron" e aplica a config.
CustomItem totem = new CustomItem("ModIA_Totem", "SwordIron", config);

// Registra. Se a config tiver Requirements, o Jotunn cria a receita automaticamente.
ItemManager.Instance.AddItem(totem);
```

Outros construtores úteis de `CustomItem`: `(GameObject prefab, bool fixReference, ItemConfig)`,
`(string name, bool addZNetView, ItemConfig)`, `(AssetBundle, string assetName, bool fixReference, ItemConfig)`.

## Receita: `CustomRecipe` + `RecipeConfig`

```csharp
RecipeConfig recipeConfig = new RecipeConfig
{
    Item = "ModIA_Totem",                     // prefab do item a fabricar
    Amount = 1,
    CraftingStation = CraftingStations.Workbench,
    MinStationLevel = 1,
};
recipeConfig.AddRequirement("Wood", 5);
recipeConfig.AddRequirement("Stone", 10);

ItemManager.Instance.AddRecipe(new CustomRecipe(recipeConfig));
```

Alternativa: montar o `Recipe` (`ScriptableObject.CreateInstance<Recipe>()`) e usar
`new CustomRecipe(recipe, fixReference: true, fixRequirementReferences: true)`.
Receitas são carregadas **depois** de todos os itens. Também há `ItemManager.AddRecipesFromJson(path)`.

## Peça: `CustomPiece` + `PieceConfig`

```csharp
PieceConfig pieceConfig = new PieceConfig
{
    Name = "$piece_modia_shrine",
    Description = "$piece_modia_shrine_desc",
    PieceTable = PieceTables.Hammer,          // qual ferramenta constrói
    CraftingStation = CraftingStations.Workbench,
    Category = PieceCategories.Misc,
    Icon = RenderManager.Instance.Render(prefab, RenderManager.IsometricRotation),
};
pieceConfig.AddRequirement("Wood", 2);

PieceManager.Instance.AddPiece(new CustomPiece(prefab, fixReference: false, pieceConfig));
```

Para uma peça **clonada** de vanilla, use `new CustomPiece("nome", "baseName", pieceConfig)` dentro
do evento `OnVanillaPrefabsAvailable`.

## Efeito de status: `CustomStatusEffect`

```csharp
StatusEffect effect = ScriptableObject.CreateInstance<StatusEffect>();
effect.name = "SE_ModIA_Blessing";
effect.m_name = "$se_modia_blessing";
effect.m_icon = icon;
effect.m_startMessageType = MessageHud.MessageType.Center;
effect.m_startMessage = "$se_modia_blessing_start";
effect.m_stopMessage = "$se_modia_blessing_stop";

ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(effect, fixReference: false));

// Attach num item (ex.: efeito ao equipar):
itemDrop.m_itemData.m_shared.m_equipStatusEffect = effect;
```

## Prefab genérico: `CustomPrefab`

```csharp
var custom = new CustomPrefab(assetBundle, "MeuPrefab", fixReference: false);
PrefabManager.Instance.AddPrefab(custom);
```

## Managers responsáveis

| Entidade | Registro |
|---|---|
| `CustomItem` / `CustomRecipe` / `CustomStatusEffect` | `ItemManager.Instance` |
| `CustomPiece` / `CustomPieceTable` | `PieceManager.Instance` |
| `CustomPrefab` | `PrefabManager.Instance` |
| `CustomCreature` | `CreatureManager.Instance` |
| `CustomLocation` / `CustomVegetation` | `ZoneManager.Instance` |

> `fixReference: true` é necessário quando o prefab usa referências "mocked" (criadas por
> `Mock<T>.Create`), principalmente ao carregar via `AssetBundle` antes dos assets do jogo.
