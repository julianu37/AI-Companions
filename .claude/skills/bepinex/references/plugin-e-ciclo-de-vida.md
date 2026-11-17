# Plugin e ciclo de vida (BepInEx 5.4.x)

Valheim 1.0.x · Unity 6000.0.75f1 · BepInExPack_Valheim 5.4.2351 (BepInEx 5.4.23.x) · net472.

Um plugin é uma classe que herda `BepInEx.BaseUnityPlugin` (que é um `UnityEngine.MonoBehaviour`)
e é anotada com `[BepInPlugin]`. O BepInEx instancia a classe e a anexa a um GameObject
(`BepInEx_Manager`), então os callbacks do Unity valem.

> Namespace raiz no BepInEx 5 é `BepInEx` (o `BaseUnityPlugin`, os atributos, `PluginInfo`).
> `ConfigFile`/`ConfigEntry` ficam em `BepInEx.Configuration` e `ManualLogSource` em
> `BepInEx.Logging`.

## Atributos

| Atributo | Uso |
| --- | --- |
| `[BepInPlugin(guid, name, version)]` | **Obrigatório.** Metadados: GUID único, nome legível e versão. |
| `[BepInProcess("valheim.exe")]` | Carrega só sob esse processo (sem isso, carrega em qualquer um). `AllowMultiple`. |
| `[BepInDependency(guid, flags)]` | Dependência de outro plugin. Pode repetir. |
| `[BepInIncompatibility(guid)]` | Não carrega se o outro plugin estiver presente. |

Regras dos valores:

- **GUID**: formato `modia.<feature>` (ex.: `modia.companion`). O BepInEx só aceita
  `^[a-zA-Z0-9\._\-]+$`; GUID inválido faz o plugin ser ignorado com warning.
- **Versão**: string legível por `System.Version` (ex.: `"0.1.0"`). Se não parsear, o plugin é ignorado.
- **Process**: `"valheim.exe"` (o filtro também aceita sem `.exe`, case-insensitive).

### BepInDependency e DependencyFlags

Flags **reais** do BepInEx 5.4.x:

```csharp
public enum DependencyFlags
{
    HardDependency = 1, // padrão: não carrega sem o outro plugin
    SoftDependency = 2  // carrega sem o outro plugin (ordem respeitada se existir)
}
```

```csharp
[BepInDependency("com.jotunn.jotunn", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("modia.core")] // HardDependency implícito
```

Com versão mínima (sempre tratado como dependência **hard**):

```csharp
[BepInDependency("com.jotunn.jotunn", "2.30.2")] // exige >= 2.30.2
```

> ⚠️ **Não invente flags.** `DependencyFlags` no BepInEx 5.4.x tem **somente**
> `HardDependency` e `SoftDependency`. Não existe `DontLoad` (nem `Optional`, `LoadBefore`...).
> Se o plugin-alvo não existir e você não quiser falhar, use `SoftDependency`. Para *proibir* a
> convivência, use `[BepInIncompatibility]`; para não declarar relação, simplesmente não coloque `[BepInDependency]`.

## Ciclo de vida

Ordem dos callbacks usada no dia a dia (todos têm assinatura `void X()`):

```
Awake  ->  OnEnable  ->  Start  ->  FixedUpdate  ->  Update  ->  LateUpdate  ->  OnDisable  ->  OnDestroy
```

- **Awake**: primeira coisa. Use para montar estado, ler config e aplicar patches Harmony.
  `Config` e `Logger` já estão prontos aqui.
- **Start**: depois que todos os `Awake` rodaram (inclui outros plugins). Bom para depender de
  outros plugins já inicializados.
- **Update**: todo frame. **Evite alocar** e **evite logar** aqui.
- **OnDestroy**: limpeza. Desfaça patches (`harmony.UnpatchSelf()`), libere eventos/handles.

O BepInEx cria a instância no `Chainloader.Start()`; o `GameObject` é `DontDestroyOnLoad`.

## Propriedades da `BaseUnityPlugin`

| Membro | Tipo | Observação |
| --- | --- | --- |
| `Info` | `PluginInfo` | `Info.Metadata.GUID/Name/Version`, `Info.Instance`, `Info.Location`, `Info.Dependencies`. Público. |
| `Logger` | `ManualLogSource` | `protected`. Já nomeado com o nome do plugin. |
| `Config` | `ConfigFile` | Público. Arquivo `BepInEx/config/<GUID>.cfg`, criado ao gravar a 1ª opção. |

> `Logger` é um `ManualLogSource` de instância, não o `BepInEx.Logging.Logger` estático.

## Exemplo mínimo

```csharp
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace ModIA.Companion
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInProcess("valheim.exe")]
    [BepInDependency("com.jotunn.jotunn", BepInDependency.DependencyFlags.SoftDependency)]
    public class CompanionPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "modia.companion";
        public const string PluginName = "ModIA Companion";
        public const string PluginVersion = "0.1.0";

        internal static CompanionPlugin Instance { get; private set; }

        private Harmony _harmony;

        private void Awake()
        {
            Instance = this;

            // PatchAll() aplica todos os [HarmonyPatch] desta assembly.
            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll();

            Logger.LogInfo($"{PluginName} v{PluginVersion} pronto (GUID={Info.Metadata.GUID}).");
        }

        private void Start()
        {
            Logger.LogDebug("Start: todos os Awake já rodaram.");
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
            Instance = null;
        }
    }
}
```

Veja `references/config.md`, `references/logging.md` e `references/harmony.md` para o resto.
Um esqueleto completo está em `templates/PluginTemplate.cs`.
