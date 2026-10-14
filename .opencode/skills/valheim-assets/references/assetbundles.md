# Soft Referenceable Assets (Valheim 1.0 / Unity 6)

Desde 0.217.40 o Valheim empacota assets em **AssetBundles** e os referencia por um `AssetID`
de **128 bits**, com carregamento/descarregamento dinâmico e **contagem de referência**. Isso
substitui o antigo carregamento por nome em muitos fluxos.

## 1. AssetID (128 bits)

- Cada asset tem um identificador único de 128 bits (16 bytes), **estável** entre bundles.
- O jogo resolve `AssetID → asset` pelo manifesto, não pelo nome do arquivo.
- Ferramentas do jogo (`AssetID`, `AssetBundleManifest`) ajudam a **criar/ler** esses ids ao
  gerar o bundle. No mod, quase sempre se lida com um `SoftReference<T>` já contendo o id.

## 2. `SoftReference<T>` (carregar/segurar/liberar)

- `Load()` → carrega de forma síncrona e **incrementa** o contador; retorna `T`.
- `LoadAsync()` → idem, assíncrono (preferível para não travar o frame).
- `HoldReference()` → incrementa o contador **sem** devolver o objeto (útil para "pinar" um
  asset que será usado depois).
- `Release()` → **decrementa** o contador; quando chega a zero, o asset pode ser descarregado.
- Regra de ouro: **para cada** `Load`/`LoadAsync`/`HoldReference`, um `Release` correspondente.

```csharp
// Exemplo ilustrativo — confirme as assinaturas no decompilado antes de usar.
SoftReference<Sprite> iconRef = /* vindo de um bundle/manifest */;
Sprite icon = iconRef.Load();
try { /* usar icon */ }
finally { iconRef.Release(); }
```

## 3. Manifests

- Localizados em
  `C:\Program Files (x86)\Steam\steamapps\common\Valheim\valheim_Data\StreamingAssets\SoftReferenceableAssets\`
  - `manifest` — manifesto principal (bundles do jogo).
  - `manifest_extended` — entradas adicionais/complementares.
- O mod pode registrar seus próprios assets chamando **`Runtime.AddManifest(path)`** com o
  caminho do manifesto do bundle **antes** da inicialização de assets do jogo.
- Depois de registrar todos os manifests, chame **`Runtime.MakeAllAssetsLoadable()`** para que
  os assets adicionados entrem no conjunto carregável.
- Nomes de bundle devem ser **únicos**: em conflito, **o jogo tem precedência** — prefixe os
  seus (ex.: `modia.companion.*`) para não colidir.

Ordem recomendada na inicialização do plugin:

```csharp
// 1) registrar o(s) manifest(s) do mod
Runtime.AddManifest(bundleManifestPath);
// 2) tornar todos os assets carregáveis (antes do uso)
Runtime.MakeAllAssetsLoadable();
```

## 4. Montar o AssetBundle (Unity 6000.0.75f1)

1. Use o **Unity 6000.0.75f1** (mesma versão do jogo) para evitar incompatibilidade de
   serialização.
2. Configure os assets com `AssetBundle` build maps/`AssetBundleBuild[]` e gere com
   `BuildPipeline.BuildAssetBundles` (target de plataforma correto).
3. Gere o manifesto com a API do jogo: **`AssetBundleManifest.SerializeToDisk(...)`**, produzindo
   o arquivo de manifesto que o `Runtime.AddManifest(path)` vai consumir.
4. Publique o `.bundle` + manifesto junto do plugin (ou em `StreamingAssets` do mod).

Observações:

- Mantenha o bundle **pequeno e específico** (ícones, sons, prefabs próprios).
- Para assets simples (um ícone, um som) avalie carregar em/na criação em runtime em vez de
  bundle — só introduza o Unity Editor quando realmente necessário.

## 5. Liberar assets

- Ao terminar o uso, `Release()` em cada referência obtida (ver §2).
- Não deixe referências "pinadas" sem uso — isso infla a RAM e impede o descarregamento.
- Verifique no jogo (Profiler/memória) que o bundle é liberado após o último `Release()`.

## 6. Ícones e áudio

- **Ícone do mod:** **256×256 PNG** (use também no `icon.png` do pacote Thunderstore).
- **Áudio do NPC:** **WAV**; se criar `AudioClip` em runtime, **destrua** o objeto depois de
  tocar (`UnityEngine.Object.Destroy(clip)`) para não vazar.

## Regras

- Um `Release()` para cada `Load`/`LoadAsync`/`HoldReference`.
- Registre manifests e `MakeAllAssetsLoadable()` **antes** de qualquer uso de asset do mod.
- Prefixe nomes de bundle para evitar conflito (o jogo vence).
- Confirme toda assinatura da API (`Runtime`, `SoftReference<T>`, `AssetBundleManifest`) no
  decompilado de `valheim_Data\Managed` antes de codar.
