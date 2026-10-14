---
name: Thunderstore Packaging
description: Como empacotar um mod de Valheim — manifest.json, dependências, ícone, README/CHANGELOG e estrutura do pacote. Use ao preparar um release.
---

# Empacotamento (formato Thunderstore)

Empacotar o mod no formato Thunderstore, mesmo que só para uso local/r2modman.

## Estrutura do pacote

```
<ModName>/
├── manifest.json
├── icon.png            # 256x256 PNG
├── README.md
├── CHANGELOG.md
└── plugins/
    └── <ModName>.dll
```

## manifest.json

Campos: `name`, `author`, `version_number` (semver), `website_url`, `description`, `dependencies`.
Dependências no formato `Author-Package-Version`. Ver `references/manifest.md` e o template
`templates/manifest.template.json`.

Dependências atuais do ModIA:

```
denikson-BepInExPack_Valheim-5.4.2351
ValheimModding-Jotunn-2.30.2
```

## Checklist de release

- Versão do `manifest.json` **bate** com a do `[BepInPlugin]`.
- `icon.png` exatamente 256×256.
- README com instalação/uso/teclas atualizados; CHANGELOG com a versão.
- `plugins/` contém só o(s) DLL(s) do mod.

## Nota

Uso pessoal/estudo: **não publicar** sem decisão explícita. O `manifest.json`/`README.md` do Thrall
que está no projeto serve só como referência de formato.
