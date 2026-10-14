# Harmony (patches)

O BepInEx 5 traz o **HarmonyX** (`0Harmony.dll`, namespace `HarmonyLib`). Use-o para alterar
métodos do jogo sem editar o binário. Patches devem ser **mínimos e defensivos**: guarde contra
`null`, não assuma estado do jogo e não engula exceções.

## Bootstrap

```csharp
using HarmonyLib;

private Harmony _harmony;

private void Awake()
{
    _harmony = new Harmony("modia.companion"); // use o mesmo GUID do plugin
    _harmony.PatchAll();                        // aplica TODOS os [HarmonyPatch] desta assembly
    // ou: _harmony.PatchAll(typeof(MyPatchClass));
    // ou: _harmony.PatchAll(typeof(CompanionPlugin).Assembly);
}

private void OnDestroy()
{
    _harmony.UnpatchSelf(); // remove todos os patches deste Harmony
    // _harmony.Unpatch(original, HarmonyPatchType.All, _harmony.Id);
}
```

Crie **um** `Harmony` por plugin, com o `Id` = GUID. Não use `PatchAll` de libs externas que
não sejam suas.

## Declarar um patch

```csharp
// Alvo por nome (evita quebrar se renomearem o parâmetro, mas ainda depende do nome do método)
[HarmonyPatch(typeof(Player), "Jump")]
internal static class Player_Jump_Patch
{
    // Prefix: roda ANTES do original.
    private static void Prefix(Player __instance)
    {
        // ...
    }

    // Postfix: roda DEPOIS; dá para trocar o resultado com ref __result.
    private static void Postfix(Player __instance, ref bool __result)
    {
        // ...
    }
}
```

Overloads resolvidos por assinatura:

```csharp
[HarmonyPatch(typeof(ZNet), nameof(ZNet.RPC_PeerInfo), new[] { typeof(ZRpc) })]
```

Prefix pode **pular o original** retornando `bool`:

```csharp
[HarmonyPatch(typeof(Character), nameof(Character.Damage))]
internal static class Character_Damage_Patch
{
    private static bool Prefix(Character __instance, ref HitData hit) // false = não executa o original
    {
        if (__instance == null || hit == null) return true; // defensivo: deixa o jogo seguir
        return true;
    }
}
```

## Argumentos especiais

| Nome | O que é |
| --- | --- |
| `__instance` | A instância do objeto (métodos de instância). |
| `__result` | Valor de retorno; use `ref` para alterar. |
| `__state` | Valor passado do Prefix para o Postfix (mesmo tipo nos dois). |
| `__args` | `object[]` com os argumentos originais. |
| `___campoPrivado` | Acesso a um campo do alvo (use 3+ `_`). |
| `__originalMethod` | `MethodBase` original (para checagens). |

```csharp
[HarmonyPatch(typeof(Inventory), nameof(Inventory.AddItem), new[] { typeof(ItemDrop.ItemData) })]
internal static class Inventory_AddItem_Patch
{
    private static void Prefix(Inventory __instance, ItemDrop.ItemData item, out bool __state)
        => __state = item != null && item.m_shared.m_name == "$item_sword";

    private static void Postfix(bool __state)
    {
        if (__state) { /* o item adicionado era a espada */ }
    }
}
```

## AccessTools

Para pegar membros que você precisa passar a `Patch` na mão ou acessar de outros lugares:

```csharp
using HarmonyLib;

var m  = AccessTools.Method(typeof(Player), "Jump");
var md = AccessTools.Method(typeof(ZNet), nameof(ZNet.RPC_PeerInfo), new[] { typeof(ZRpc) });
FieldInfo f = AccessTools.Field(typeof(Character), "m_maxHealth");
PropertyInfo p = AccessTools.PropertyGetter(typeof(Player), "IsDead");
Type t = AccessTools.TypeByName("Player");
```

## Patch manual (fora do `PatchAll`)

```csharp
var original = AccessTools.Method(typeof(Player), "Jump");
_harmony.Patch(original,
    prefix:  new HarmonyMethod(AccessTools.Method(typeof(Player_Jump_Patch), nameof(Player_Jump_Patch.Prefix))),
    postfix: new HarmonyMethod(AccessTools.Method(typeof(Player_Jump_Patch), nameof(Player_Jump_Patch.Postfix))));
```

`Patch`/`PatchAll` lançam `HarmonyException` se a assinatura não casar — proteja a aplicação num
`try/catch` e logue o erro (veja `references/logging.md`).

## Transpiler (avançado)

Para reescrever o IL quando Prefix/Postfix não bastam:

```csharp
[HarmonyPatch(typeof(SomeGameClass), nameof(SomeGameClass.SomeMethod))]
internal static class SomeGameClass_Transpiler
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var matcher = new CodeMatcher(instructions);
        // ... matcher.MatchForward(...).Insert(...)
        return matcher.InstructionEnumeration();
    }
}
```

Prioridade e ordenação entre mods: `[HarmonyPriority(Priority.High)]`, `[HarmonyBefore("outro.guid")]`,
`[HarmonyAfter("outro.guid")]`.
