# manifest.json (Thunderstore v1)

O `manifest.json` é um JSON **obrigatório** na raiz do zip do pacote. Schema v1 confirmado na
validação oficial do Thunderstore (`ManifestV1Serializer`).

## Campos

| Campo | Tipo | Obrigatório | Regras |
|---|---|---|---|
| `name` | string | sim | Só `a-z A-Z 0-9 _` (sem espaço/hífen), máx. 128. É o nome do pacote — **não mude** entre versões. |
| `version_number` | string | sim | Formato `Major.Minor.Patch` (ex.: `1.4.0`), máx. 16. Não pode repetir uma versão já publicada. |
| `website_url` | string | sim | Pode ser vazio (`""`). Se preenchido, `http`/`https`/`mailto`/`ipfs`, máx. 1024. |
| `description` | string | sim | Pode ser vazio. Máx. **256** caracteres. |
| `dependencies` | string[] | sim | Lista de refs `Namespace-Package-Version` (pode ser vazia). Sem refs duplicadas. |
| `installers` | array | não | Opcional, usado por instaladores modernos. Normalmente omitido. |

> **`author` não é um campo oficial** do schema v1. O "autor"/namespace vem da **team** da conta
> que faz o upload (`Namespace-Nome-Versão`). Vários manifestos da comunidade (inclusive o do Thrall
> que temos como referência local) incluem `"author"`, mas o Thunderstore o ignora. Mantemos por
> compatibilidade de formato, sem valor semântico.

## Dependências

Formato: `Namespace-Package-Version` (o primeiro segmento é a **team/namespace** do autor).

```
denikson-BepInExPack_Valheim-5.4.2351
ValheimModding-Jotunn-2.30.2
```

Dependências atuais do ModIA:

- `denikson-BepInExPack_Valheim-5.4.2351` (BepInEx 5.4.23.x)
- `ValheimModding-Jotunn-2.30.2`

## Estrutura do pacote (.zip)

```
ModIA-0.1.0.zip
├── manifest.json
├── icon.png            # PNG 256x256 (obrigatório)
├── README.md           # recomendado (obrigatório como "readme" do pacote)
├── CHANGELOG.md        # recomendado
└── plugins/            # vira BepInEx/plugins no r2modman
    └── ModIA.dll
```

- O zip deve conter **os arquivos na raiz** (não dentro de uma subpasta extra).
- Todo arquivo `.dll` do mod vai em `plugins/`.
- Pode-se empacotar assets/configs extras junto (dentro de `plugins/` ou de uma pasta própria).

## Ícone

- **Exatamente 256×256 pixels**, formato **PNG**.
- Nome obrigatório: `icon.png`.

## Versionamento (semver)

- `Major.Minor.Patch` numérico. Ex.: `0.1.0`, `1.10.3`.
- O `version_number` do `manifest.json` **deve bater** com a versão do `[BepInPlugin]` no código.

## Exemplo mínimo

```json
{
  "name": "ModIA",
  "version_number": "0.1.0",
  "website_url": "",
  "description": "Companheiro NPC com IA para Valheim.",
  "dependencies": [
    "denikson-BepInExPack_Valheim-5.4.2351",
    "ValheimModding-Jotunn-2.30.2"
  ]
}
```

Template pronto: `templates/manifest.template.json`.
