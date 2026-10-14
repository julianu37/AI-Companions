---
name: BepInEx
description: Como criar plugins BepInEx para Valheim — plugin/ciclo de vida, config, logging, Harmony e build/deploy. Use sempre que escrever ou revisar código de plugin.
---

# BepInEx para Valheim

BepInEx é o carregador de mods. Todo plugin do ModIA é uma classe `BaseUnityPlugin` decorada com
atributos do BepInEx, e usa Harmony para alterar o jogo.

## Quando usar

Sempre que o trabalho envolver: criar/alterar o plugin principal, adicionar configuração, logs,
patches Harmony, ou ajustar o build/deploy. Combine com a skill `jotunn` quando mexer em conteúdo
do jogo.

## Alvo desta máquina

- Valheim 1.0.x, Unity 6000.0.75f1, **BepInExPack_Valheim 5.4.2351** (BepInEx 5.4.23.x).
- Target: **net472**. Jogo em `C:\Program Files (x86)\Steam\steamapps\common\Valheim`.

## Fluxo

1. Ler `references/plugin-e-ciclo-de-vida.md` para o esqueleto do plugin e os atributos.
2. Configuração conforme `references/config.md`.
3. Logs conforme `references/logging.md`.
4. Patches conforme `references/harmony.md`.
5. Build/deploy conforme `references/build-deploy.md` (`Environment.props`, cópia para `plugins`).
6. Template pronto em `templates/PluginTemplate.cs`.

## Regras

- GUID `modia.<feature>`; logging via `Logger`; config via `ConfigEntry`; **sem segredos no código**.
- Confirme APIs na fonte oficial do BepInEx (reference `bepinex`) quando houver dúvida.
