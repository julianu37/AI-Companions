# Logging

Tudo passa por um `ManualLogSource` (`BepInEx.Logging`). Dentro de um `BaseUnityPlugin` você já
tem um pronto: a propriedade `protected ManualLogSource Logger`, nomeada com o nome do plugin.

Fora de uma instância de plugin, crie um source nomeado:

```csharp
using BepInEx.Logging;

internal static readonly ManualLogSource Log =
    BepInEx.Logging.Logger.CreateLogSource("ModIA");
```

> `BepInEx.Logging.Logger` é a fábrica estática; a propriedade `Logger` do plugin é o seu
> `ManualLogSource` de instância. Dentro do plugin, use `Logger.LogInfo(...)` direto.

## Métodos

```csharp
Logger.LogInfo(object data);     // informação normal        (visível por padrão)
Logger.LogWarning(object data);  // algo suspeito, mas segue
Logger.LogError(object data);    // falha; o mod continua
Logger.LogDebug(object data);    // detalhe de depuração      (oculto por padrão)
Logger.LogMessage(object data);  // mensagem ao usuário
Logger.LogFatal(object data);    // falha fatal

Logger.Log(LogLevel.Warning, data); // nível arbitrário
```

`LogLevel` é `[Flags]` (`None`, `Fatal`, `Error`, `Warning`, `Message`, `Info`, `Debug`, `All`);
pode-se combinar com `|` (útil no `LogLevels` do BepInEx). O BepInEx também captura o `Debug.Log`
do Unity e a saída do Harmony (`HarmonyLogSource`).

## Boas práticas

- **Prefixe/padronize** mensagens com o contexto: `Logger.LogWarning($"[Net] timeout: {e.Message}")`.
  O nome do source (nome do plugin) já aparece no log, então evite repetir o nome do mod.
- **Não logue em `Update`/`FixedUpdate` nem por frame**: enche o `LogOutput.log` e aloca.
  Logue em transições de estado, uma vez, ou atrás de um flag de debug.
- **Nunca vaze segredos.** Não logue API keys, tokens nem o corpo de requisições autenticadas.
  Mascare (`"***"`) se precisar indicar presença.
- Use `LogDebug` para o que só interessa ao desenvolvedor e `LogInfo` para marcos de inicialização.
- Evite `Debug.Log` do Unity dentro do mod; use o `Logger` para tudo sair no log padrão do BepInEx.
- **Não concatene por frame** (ex.: `LogInfo("x=" + x)`) — mesmo filtrado, a string já foi alocada.
  Construa a mensagem só quando for logar.

```csharp
// Bom: uma vez, em transição.
private void OnDestroy()
{
    Logger.LogInfo("CompanionPlugin descarregado.");
}

// Ruim: todo frame.
private void Update()
{
    // Logger.LogInfo($"tick {Time.frameCount}");  // NÃO faça isso
}

// Segredos: mascare.
Logger.LogDebug($"ApiKey configurada: {(string.IsNullOrEmpty(apiKey) ? "nao" : "sim")}");
```
