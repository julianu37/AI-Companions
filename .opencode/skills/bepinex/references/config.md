# Configuração (`ConfigFile` / `ConfigEntry`)

Namespace `BepInEx.Configuration`. Cada plugin recebe um `ConfigFile` pronto em `BaseUnityPlugin.Config`
(arquivo `BepInEx/config/<GUID>.cfg`, formato TOML-lite). Nada é gravado até a primeira opção
ser adicionada ou `Save()` ser chamado.

## Bind\<T\> — a operação principal

```csharp
ConfigEntry<T> Bind<T>(string section, string key, T defaultValue,
                       ConfigDescription configDescription = null);
ConfigEntry<T> Bind<T>(string section, string key, T defaultValue, string description);
ConfigEntry<T> Bind<T>(ConfigDefinition configDefinition, T defaultValue,
                       ConfigDescription configDescription = null);
```

Sempre retorne e guarde o `ConfigEntry<T>`; o valor fica em `entry.Value` (tipado).

```csharp
using BepInEx;
using BepInEx.Configuration;

private ConfigEntry<bool> _enabled;
private ConfigEntry<int>  _maxFollowers;

private void Awake()
{
    _enabled = Config.Bind("Geral", "Ativado", true,
        "Liga/desliga o companheiro.");

    _maxFollowers = Config.Bind("Geral", "MaxSeguidores", 2,
        new ConfigDescription("Quantos NPCs podem seguir o jogador.",
            new AcceptableValueRange<int>(0, 8)));
}
```

Tipos suportados vêm do `TomlTypeConverter` (primitivos, `string`, `enum`, `Vector2/3/4`,
`Quaternion`, `Color`, `KeyboardShortcut`, etc.). Tipo não suportado dispara `ArgumentException`.

## ConfigDescription

```csharp
ConfigDescription(string description,
                  AcceptableValueBase acceptableValues = null,
                  params object[] tags);
```

- `description`: texto (aceita `\n`, aparece como `## ` no `.cfg`).
- `acceptableValues`: `AcceptableValueRange<T>` **clampa** o valor; `AcceptableValueList<T>`
  força o valor para o primeiro item se inválido. `T` precisa bater com o tipo do entry.
- `tags`: objetos para add-ons de UI (ex.: ConfigurationManager).

```csharp
// Range (T : IComparable)
new ConfigDescription("Velocidade base.", new AcceptableValueRange<float>(0.5f, 5f));

// Lista fixa (T : IEquatable<T>)
new ConfigDescription("Perfil de IA.",
    new AcceptableValueList<string>("local", "hibrido", "api"));
```

## Reagir a mudanças

Há dois níveis de evento; ambos recebem `EventHandler<SettingChangedEventArgs>`, e o
`args.ChangedSetting` é um `ConfigEntryBase` (compare por referência).

```csharp
// Por entrada (mais comum)
_backendUrl.SettingChanged += (sender, args) =>
    Logger.LogInfo($"Nova URL do backend: {_backendUrl.Value}");

// No arquivo inteiro (qualquer opção)
Config.SettingChanged += OnAnySettingChanged;

// Recarregou o .cfg do disco
Config.ConfigReloaded += (sender, args) => Logger.LogInfo("Config recarregada.");
```

`Config.SaveOnConfigSet` (padrão `true`) grava no disco a cada mudança; com `false`, chame
`Config.Save()` manualmente. `Config.Reload()` traz o arquivo do disco.

```csharp
private void OnAnySettingChanged(object sender, SettingChangedEventArgs args)
{
    Logger.LogDebug($"Mudou: {args.ChangedSetting.Definition.Section}/{args.ChangedSetting.Definition.Key}");
}
```

## 🔐 Regra: nunca hardcodar segredos

Endereço de backend, tokens e **API keys** não vão no código nem commitados. Opções:

1. **Preferível para segredos**: variável de ambiente (`Environment.GetEnvironmentVariable`).
2. Valores não secretos (URL, porta, modelo): `ConfigEntry<string>` em `BepInEx/config`.
   Lembre que o `.cfg` é texto puro — não coloque chave de API ali em projeto distribuído.
3. Deixe a `ConfigEntry<string>` de API key **vazia por padrão** e leia o ambiente como fallback.

```csharp
private ConfigEntry<string> _backendUrl;
private ConfigEntry<string> _apiKey;

private void BindBackend()
{
    // URL não é segredo.
    _backendUrl = Config.Bind("Backend", "Url", "http://127.0.0.1:8080",
        "Endereco do cerebro de IA (LLM/STT/TTS).");

    // Nunca coloque a chave no default; deixe em branco.
    _apiKey = Config.Bind("Backend", "ApiKey", "",
        "Chave de API opcional. Prefira a variavel de ambiente MODIA_API_KEY.");
}

private string ResolveApiKey()
{
    if (!string.IsNullOrEmpty(_apiKey.Value)) return _apiKey.Value;
    return Environment.GetEnvironmentVariable("MODIA_API_KEY"); // pode retornar null
}

// Ao logar valores de config, mascare segredos.
private string Mask(string secret) =>
    string.IsNullOrEmpty(secret) ? "(vazio)" : "***";
```

Nunca logue a chave (veja `references/logging.md`).
