# Localização, GUI e comandos (Jotunn 2.30.2)

## Localização (`LocalizationManager` / `CustomLocalization`)

Tokens localizáveis usam o prefixo `$` no prefab/item (ex.: `$item_modia_totem`). Ao adicionar
traduções, **não** inclua o `$` — o Jotunn remove se você incluir.

```csharp
private CustomLocalization _localization;

private void AddLocalizations()
{
    // Cria/reutiliza a instância de localização deste mod.
    _localization = LocalizationManager.Instance.GetLocalization();

    _localization.AddTranslation("English", new System.Collections.Generic.Dictionary<string, string>
    {
        { "item_modia_totem", "Companion Totem" },
        { "item_modia_totem_desc", "A totem that calls your companion." },
    });

    _localization.AddTranslation("Portuguese_Brazilian", new System.Collections.Generic.Dictionary<string, string>
    {
        { "item_modia_totem", "Totem do Companheiro" },
        { "item_modia_totem_desc", "Um totem que chama o seu companheiro." },
    });
}
```

Variações do manager:
- `LocalizationManager.Instance.AddToken(token, value)` — adiciona um único token.
- `LocalizationManager.Instance.AddLocalization("English", dicionario)` — atalho por idioma.
- `CustomLocalization.TryTranslate("$token")` — traduzir manualmente (útil em GUI própria).
- `CustomLocalization.AddJsonFile(lang, json)` / `AddYamlFile(...)` — carregar de arquivo/`TextAsset`.

Carga automática "side loading": coloque arquivos em
`.../plugins/<Mod>/Assets/Translations/<LanguageName>/*.json` (também aceita `.yaml`/`.yml`, que
exigem `ValheimModding-YamlDotNet`). Após tudo ser aplicado, dispara `LocalizationManager.OnLocalizationAdded`.

Padrões de idioma: `English`, `Portuguese_Brazilian`, etc. (veja a language-list do Jotunn).

## GUI (`GUIManager`)

A GUI do jogo é recriada a cada troca de cena. Crie elementos dentro de
`GUIManager.OnCustomGUIAvailable`, adicionando-os sob `GUIManager.CustomGUIFront` (na frente) ou
`CustomGUIBack` (atrás da GUI vanilla).

O padrão de menu/subpainel (usado por mods tipo o Thrall) é um **woodpanel** como container:

```csharp
private GameObject _panel;

private void Awake()
{
    GUIManager.OnCustomGUIAvailable += BuildPanel;
}

private void BuildPanel()
{
    // Não recrie se já existe.
    if (_panel != null || GUIManager.Instance == null || !GUIManager.CustomGUIFront)
    {
        return;
    }

    _panel = GUIManager.Instance.CreateWoodpanel(
        parent: GUIManager.CustomGUIFront.transform,
        anchorMin: new Vector2(0.5f, 0.5f),
        anchorMax: new Vector2(0.5f, 0.5f),
        position: new Vector2(0f, 0f),
        width: 400f,
        height: 300f,
        draggable: true);

    GUIManager.Instance.CreateText(
        text: "Companheiro",
        parent: _panel.transform,
        anchorMin: new Vector2(0.5f, 1f),
        anchorMax: new Vector2(0.5f, 1f),
        position: new Vector2(0f, -40f),
        font: GUIManager.Instance.AveriaSerifBold,
        fontSize: 24,
        color: GUIManager.Instance.ValheimOrange,
        outline: true,
        outlineColor: Color.black,
        width: 350f,
        height: 40f,
        addContentSizeFitter: false);

    _panel.SetActive(false);
}

private void TogglePanel()
{
    bool open = !_panel.activeSelf;
    _panel.SetActive(open);
    GUIManager.BlockInput(open); // libera o mouse enquanto o painel está aberto
}
```

Outros helpers: `CreateButton`, `CreateInputField`, `CreateDropDown`, `CreateToggle`,
`CreateColorPicker`, `CreateGradientPicker`, `GetSprite(nome)`. Para arrastar painéis criados
manualmente: `Jotunn.GUI.DragWindowCntrl.ApplyDragWindowCntrl(panel)`.

> Sempre chame `GUIManager.BlockInput(false)` ao fechar — o Jotunn **não** libera o input sozinho.

## Comandos de console (`CommandManager`)

```csharp
public class CompanionCommand : ConsoleCommand
{
    public override string Name => "modia";
    public override string Help => "Comandos do companheiro ModIA";

    public override void Run(string[] args)
    {
        if (args.Length == 0)
        {
            Console.instance.Print("uso: modia <spawn|talk>");
            return;
        }
        // ...
    }

    // Autocomplete opcional
    public override List<string> CommandOptionList() => null;
}

// Registro no Awake do plugin:
CommandManager.Instance.AddConsoleCommand(new CompanionCommand());
```

- Nomes de comando **devem** ser únicos.
- Para saber de onde veio o comando (console vs. chat), sobrescreva
  `Run(string[] args, Terminal context)` e escreva com `context.AddString(...)`.
