# Build e deploy (net472 → BepInEx\plugins)

Alvo: **.NET Framework 4.7.2 (net472)**, C#. Jogo em
`C:\Program Files (x86)\Steam\steamapps\common\Valheim`.

## Estrutura da solução

```
ModIA.sln
Environment.props        # VALHEIM_INSTALL (+ opcionais)
DoPrebuild.props         # liga o prebuild do Jotunn
src/ModIA.Companion/
  ModIA.Companion.csproj
  CompanionPlugin.cs
```

`Environment.props` e `DoPrebuild.props` ficam na **raiz da solução** (onde está o `.sln`), pois o
`Paths.props` do Jotunn os procura em `$(SolutionDir)`.

## Environment.props

```xml
<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="Current" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <PropertyGroup>
    <VALHEIM_INSTALL>C:\Program Files (x86)\Steam\steamapps\common\Valheim</VALHEIM_INSTALL>
    <!-- Opcionais (têm default derivado do VALHEIM_INSTALL):
    <BEPINEX_PATH>$(VALHEIM_INSTALL)\BepInEx</BEPINEX_PATH>
    <MOD_DEPLOYPATH>$(BEPINEX_PATH)\plugins</MOD_DEPLOYPATH>
    -->
  </PropertyGroup>
</Project>
```

Com `VALHEIM_INSTALL` definido, o `Paths.props` do Jotunn deriva:
`VALHEIM_MANAGED = $(VALHEIM_INSTALL)\valheim_Data\Managed` e `BEPINEX_PATH = $(VALHEIM_INSTALL)\BepInEx`.

## DoPrebuild.props

```xml
<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="Current" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <PropertyGroup>
    <ExecutePrebuild>true</ExecutePrebuild>
  </PropertyGroup>
</Project>
```

Liga o `JotunnBuildTask` (prebuild): ele **publiciza** as assemblies do jogo
(`assembly_valheim.dll`, etc.) e as disponibiliza para referência. Só roda quando
`VALHEIM_INSTALL` existe e as binárias mudaram desde a última execução. Para desligar, remova o
arquivo (ou deixe `ExecutePrebuild` diferente de `true`).

> Alternativa sem Jotunn: gere as publicized assemblies manualmente com o **AssemblyPublicizer** e
> referencie-as por `HintPath`.

## csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net472</TargetFramework>
    <LangVersion>latest</LangVersion>
    <AssemblyName>ModIA.Companion</AssemblyName>
    <RootNamespace>ModIA.Companion</RootNamespace>
    <Version>0.1.0</Version>
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
  </PropertyGroup>

  <ItemGroup>
    <!-- Permite compilar net472 sem instalar o Developer Pack completo. -->
    <PackageReference Include="Microsoft.NETFramework.ReferenceAssemblies" Version="1.0.3" PrivateAssets="all" />
    <!-- Framework Jotunn (Valheim). -->
    <PackageReference Include="JotunnLib" Version="2.30.2" />
  </ItemGroup>

  <ItemGroup>
    <!-- Núcleo do BepInEx 5 + HarmonyX (0Harmony). NÃO copiar para plugins. -->
    <Reference Include="BepInEx">
      <HintPath>$(BEPINEX_PATH)\core\BepInEx.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="0Harmony">
      <HintPath>$(BEPINEX_PATH)\core\0Harmony.dll</HintPath>
      <Private>false</Private>
    </Reference>

    <!-- Assemblies do jogo (prefira as publicized geradas pelo prebuild). -->
    <Reference Include="assembly_valheim">
      <HintPath>$(VALHEIM_MANAGED)\assembly_valheim.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="assembly_guiutils">
      <HintPath>$(VALHEIM_MANAGED)\assembly_guiutils.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="mscorlib">
      <HintPath>$(VALHEIM_MANAGED)\mscorlib.dll</HintPath>
      <Private>false</Private>
    </Reference>
  </ItemGroup>

  <!-- Copia o .dll para BepInEx\plugins após o build. -->
  <Target Name="CopyToPlugins" AfterTargets="Build">
    <PropertyGroup>
      <_DeployDir Condition="'$(MOD_DEPLOYPATH)' != ''">$(MOD_DEPLOYPATH)</_DeployDir>
      <_DeployDir Condition="'$(_DeployDir)' == ''">$(BEPINEX_PATH)\plugins</_DeployDir>
    </PropertyGroup>
    <Copy SourceFiles="$(TargetPath)" DestinationFolder="$(_DeployDir)" />
    <Message Importance="High" Text="Deploy: $(TargetPath) -> $(_DeployDir)" />
  </Target>
</Project>
```

Notas:

- `mscorlib`: alguns templates referenciam o `mscorlib.dll` da pasta `Managed` para casar com o
  runtime do Unity; marque `Private=false`. Se seu projeto já usa
  `Microsoft.NETFramework.ReferenceAssemblies`, pode não ser necessário.
- `Private=false` evita copiar `BepInEx.dll`, `0Harmony.dll` e as DLLs do jogo junto do seu mod —
  elas já existem no jogo.
- Use `AssemblyVersion`/`Version` consistentes com o `[BepInPlugin(...)]`.

## Comandos

```powershell
# Restaura NuGet (JotunnLib, ReferenceAssemblies).
dotnet restore

# Build (o prebuild do Jotunn roda e publiciza as assemblies na 1ª vez).
dotnet build -c Release

# Sobrescrevendo o caminho do jogo sem editar o Environment.props:
dotnet build -c Release /p:VALHEIM_INSTALL="C:\Program Files (x86)\Steam\steamapps\common\Valheim"
```

O resultado é copiado para `...\Valheim\BepInEx\plugins\ModIA.Companion.dll`.

## Deploy e verificação

1. Build gera o `.dll` já copiado para `BepInEx\plugins`.
2. Inicie o Valheim com BepInEx; confira `BepInEx\LogOutput.log` (sem exceções, com a linha de
   inicialização do plugin).
3. Rode `/p:Configuration=Debug` para `LogDebug`/PDBs, se precisar depurar.

## Armadilhas comuns

- `VALHEIM_INSTALL` sem `Environment.props` → referências do jogo não resolvem.
- Não referenciar as **publicized** assemblies → `private`/`internal` do jogo inacessíveis.
- Copiar `BepInEx.dll`/`0Harmony.dll`/`assembly_valheim.dll` para `plugins` → conflito de versão.
  Mantenha `Private=false`.
- GUID do plugin diferente do `.dll`/pasta → confusão no log; padronize `modia.<feature>`.
