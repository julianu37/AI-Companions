---
name: Valheim Assets
description: AssetBundles e SoftReferenceableAssets no Valheim 1.0 (Unity 6) — como criar, carregar e liberar assets próprios (modelos, ícones, sons).
---

# Assets no Valheim 1.0

Desde 0.217.40 o Valheim usa **Soft Referenceable Assets**: assets são empacotados em AssetBundles
e referenciados por um `AssetID` de 128 bits, com carregamento/descarregamento dinâmico e contagem
de referência.

## Quando usar

Só quando o mod precisar de assets próprios (modelos, texturas, ícones, áudio). Para lógica pura,
não é necessário.

## Conceitos-chave

- `SoftReference<T>` — `Load()`/`LoadAsync()` incrementam o contador; `Release()` decrementa.
- Manifs do jogo em `Valheim_Data\StreamingAssets\SoftReferenceableAssets` (`manifest`,
  `manifest_extended`).
- É possível carregar bundles do próprio mod via `Runtime.AddManifest(...)` antes da inicialização.
- Nomes de bundle devem ser únicos (o jogo tem precedência em conflito).

## Pipeline

1. Montar o AssetBundle no **Unity 6000.0.75f1** (mesma versão do jogo).
2. Gerar o manifest (a API do jogo expõe `AssetBundleManifest.SerializeToDisk`).
3. Carregar por `AssetID` e **liberar** com `Release()`.

## Ícones e áudio

- Ícone do mod: **256×256 PNG**.
- Áudio do NPC: WAV; ao criar `AudioClip` em runtime, destruir depois de tocar.

## Regras

- Para cada `Load`, um `Release` (evitar vazamento/inflar RAM).
- Só introduza o Unity Editor quando necessário; prefira runtime para assets simples.
