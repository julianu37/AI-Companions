---
name: BepInEx
description: Como o plugin BepInEx deste mod funciona (plugin/ciclo de vida, config, logging com LogLevel, Harmony e os alvos de build/deploy). Use sempre que escrever ou revisar código de plugin, config ou build.
---

# BepInEx neste projeto (1.9.2)

O mod é **um** plugin `BaseUnityPlugin` (`Core/CompanionsPlugin.cs`). Antes de qualquer detalhe técnico,
leia o `AGENTS.md`: ele é a fonte da verdade (arquitetura, fases, decisões e pendências).

## Identidade (não mexer)

- **GUID `Maddhattr.OfflineCompanionsReborn`** — preservado de propósito (saves, ZDOs e o mundo dependem dele).
- Assembly/`AssemblyName` **`Companions`**; `.csproj` **`Companions.csproj`**; chaves `HC_*` do ZDO.
- Versão **só** em `PluginVersion` (`CompanionsPlugin.cs`). Não existe `<Version>` no `.csproj`.
- Arquivo de config: **`BepInEx\config\AiCompanions.cfg`** (seção `[AI]` em inglês; o
  `Maddhattr.OfflineCompanionsReborn.cfg` é ignorado e há migrador do `com.profmags.companions.cfg`).

## Alvo

- Valheim **1.0.x** (testado 1.0.20), Unity **6000.0.75f1**, **BepInExPack_Valheim 5.4.2351** (5.4.23.5).
- **net472**. **Jotunn NÃO é usado** (não referencie nem declare dependência dele).

## Config

- `ModConfig.Init(ConfigFile)` é o lugar de todo `Bind`; **340 opções em 24 seções** hoje.
- **Chaves e descrições em inglês** (o PT é tradução em `Translations/Portuguese_Brazilian.json`, via
  `hc_cfg_<seção>_<chave>`); o F8 mostra `LocOr(...)` e cai na própria chave em inglês.
- Defaults que mudam de valor precisam de **migração** em `ApplyMultilanguageDefaults` (regra: só o padrão
  antigo **exato**), como as da 3.3b/3.3c/multilinguagem e a do `CompanionPrice` (2000→1000, do upstream).
- Remover uma chave é seguro: `MigrateAndClean` apaga as órfãs e guarda um `.bak`.
- `CONFIGURATION.md` é **gerado** (`tools/gen_config_doc.py src/Companions/Core/ModConfig.cs package/CONFIGURATION.md`)
  — regenere sempre que um texto de config mudar.

## Logging

- Prefixos: `[IA]` (camada de IA, de propósito mesmo com a seção `[AI]`), `[TTS]`, `[STT]`, `[Locale]`,
  `[Setup]`, `[Order]`, `[Collect]`, `[Diag]`, `[UiDiag]`, `[ECO]`.
- **`[Advanced] LogLevel`** (`Off`/`Normal`/`Detailed`) é o portão oficial: use `CompanionsPlugin.LogNormal`
  / `LogDetailed` **antes** de montar a string. `LogWarning`/`LogError` são sempre ligados.
- Nada de log por frame; um por evento (fala, ordem, scan).

## Harmony

- `Harmony` com `PluginGUID`, aplicado **classe por classe** (try/catch por tipo) em `MHR_ApplyPatches`;
  `UnpatchSelf()` no `Shutdown`. **Sem `[HarmonyPriority]`** por desenho: os patches são
  ordem-independentes.
- Detalhes e armadilhas: `references/harmony.md` (e a skill não substitui o decompilado — confirme alvo).

## Build e deploy

- `Environment.props` (gitignored) define `ValheimDir`/`DeployDir`.
- `dotnet build Companions.sln -c Release` → `src/Companions/bin/Release/net472/Companions.dll` e copia para
  `...\BepInEx\plugins\AI-Companions\` (alvos `DeployToPlugins`, `DeployTranslations`, `DeployWhisperNet`,
  `DeploySherpaOnnx`, `DeployLicenses`).
- **Uma cópia só** do mod em `plugins` (Vortex usa `plugins\Companions\`): duas = GUID duplicado.
- Teste: `LogOutput.log` com `Loading [AI Companions <versão>]` + `loaded successfully! (N methods patched)`.
- **Nunca troque versões de pacote** (Whisper.net 1.9.1, sherpa-onnx 1.13.8, System.Text.Json 10.0.2):
  é a stack validada da 1.9.1/1.9.2.

## Regras

- Sem segredos no código (`[AI] ApiKey` é `ConfigEntry`).
- Comentários de código em **inglês**, mensagens de log em **PT-BR**, texto ao jogador **sempre** por chave
  de localização.
- Confirme APIs na fonte oficial do BepInEx (reference `bepinex`) quando houver dúvida.
