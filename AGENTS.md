# AGENTS.md — ModAI-BR-Companions

Mod de **companheiro NPC com IA** para **Valheim 1.0**, em **PT-BR**.

Este projeto é um **fork** de [Offline Companions Reborn](https://github.com/MaddhattrRocks/Offline-Companions-Reborn)
(**MIT**), que já implementa a visão de "companheiro como membro da equipe" (trabalhos, comida, stamina,
skills, casa, dormir, portais, carroça, menu radial). Nosso trabalho aqui é:

1. **Traduzir completamente para PT-BR** (o mod tem sistema próprio de tradução).
2. **Adicionar a camada de IA** (LLM/voz/fala) por cima — a "Fase 3".

O trabalho é feito pela equipe de agentes em `.opencode/agents/` (comandada por `valheim-mod-lead`) e pelas
skills em `.opencode/skills/`. Leia este arquivo antes de agir.

## Upstream e licença

- Upstream (parent do fork): `MaddhattrRocks/Offline-Companions-Reborn` — **MIT**.
- **Atribuição obrigatória**: ver `THIRD_PARTY_NOTICES.md` e `NOTICE.md`. Mantenha os avisos MIT.
- Ao puxar mudanças do upstream: `git fetch upstream` (adicione o remote se não existir).

## Alvo e versões

- **Valheim 1.0.17** — engine **Unity 6000.0.75f1 (Unity 6)**.
- **BepInExPack_Valheim 5.4.2351** (BepInEx 5.4.23.5).
- **Jotunn 2.30.2**.
- Target: **.NET Framework 4.7.2 (net472)**, C#.
- Versão atual do fork: **1.5.4** (ver `src/Companions/Core/CompanionsPlugin.cs` e `package/manifest.json`).

## Caminhos desta máquina

- Repositório: `C:\Users\revenda33\Desktop\ModAI-BR-Companions`
- Jogo: `C:\Program Files (x86)\Steam\steamapps\common\Valheim`
- Plugins: `...\BepInEx\plugins\ModAI-BR-Companions`
- Traduções (runtime): `...\BepInEx\plugins\ModAI-BR-Companions\Translations\*.json` (UI) e
  `...\Speech\*.json` (falas do companheiro). As fontes ficam em `Translations/` e `Speech/` na raiz do
  repositório e o build as copia (`Companions.csproj` → alvo `DeployTranslations`).
- Idioma do jogo em PT-BR: **`Portuguese_Brazilian`** (os arquivos são `Translations/Portuguese_Brazilian.json`
  e `Speech/Portuguese_Brazilian.json`).

## Build

- `Environment.props` na raiz define `ValheimDir` e `DeployDir` (é **gitignored**; há o
  `Environment.props.example`).
- **Build:** `dotnet build Companions.sln -c Release` → gera `src/Companions/bin/Release/net472/Companions.dll`
  e copia para `DeployDir`.
- **Teste:** iniciar o jogo e conferir `BepInEx\LogOutput.log` (deve listar
  `Loading [Offline Companions Reborn <versão>]`).

## Arquitetura do fork (`src/Companions/`)

| Pasta | Conteúdo |
|---|---|
| `Core/` | Entry point, config, **localização**, speech config, helpers (reflexão, texturas, input) |
| `Companion/` | Setup, spawn/prefabs, aparência, tiers, skills, stamina, comida, descanso, **talk**, morte/respawn |
| `AI/` | Movimento e combate: `CompanionAI`, `CompanionCombatAI`, arma/escudo, tocha, água, portas, `MHR_Mover`; **camada de IA**: `LlmClient`, `LlmIntent`, `CompanionOrders`, `CompanionChatAI` |
| `Jobs/` | Trabalhos: harvest, farm, fish, hunt, cook, smelt, repair, upkeep, loot, tool keeper |
| `UI/` | Painéis, **menu radial**, HUD, config, zone placers, fixes de fonte/inventário |
| `Patches/` | Patches Harmony nas classes vanilla |
| `Diagnostics/` | Logs de diagnóstico (`[AIGate]`, `[EQUIPDIAG]`, `[UiDiag]`, `[Diag]`) |

Notas de desenvolvimento do upstream: `docs/DEV_NOTES.md`; handoff: `docs/SESSION_HANDOFF.md`.

## Camada de IA (Fase 3.1 — ordens por texto no chat; Fase 3.2/3.2b — o número da ordem é um delta que limita o trabalho)

O jogador digita a ordem no chat do jogo (`colete 20 de madeira`, `segue`, `para`) e um **LLM local**
(API compatível com a OpenAI: `POST {BaseUrl}/chat/completions`) devolve a intenção em JSON, que vira a
**mesma ação** do menu radial / tecla de comando. Fluxo: `CompanionChatAI` (patch Harmony em
`Chat.SendText` + system prompt PT-BR) → `LlmClient` (POST em `Task` de fundo + fila thread-safe drenada
no `Update`) → `LlmIntent` (parser tolerante) → `CompanionOrders` (intenção → `CompanionSetup.ApplyActionMode`,
`SetCombatStance`, `MHR_JobRules`, recall...).

- Config `[IA]` no `.cfg` (sem segredos no código): `Ligado`, `BaseUrl`, `Modelo`, `ApiKey`, `Temperatura`,
  `TimeoutSegundos`, `Prefixo`, `MostrarRespostaNoChat`, `CooldownSegundos`.
- `Chat.SendText(Talker.Type, string)` é o caminho da mensagem **do próprio jogador** (as recebidas entram
  por `Chat.OnNewChatMessage`) — é o único ponto capturado.
- `despawn` não é exposto ao modelo. O `amount` é um **DELTA** para madeira/pedra/minério (Fase 3.2 + 3.2b):
  vira um teto no ZDO do companheiro (`HC_OrderCap`/`HC_OrderCapMode`) e o limite efetivo é
  `estoque na hora da ordem + quantidade pedida` (`CompanionSetup.OrderCapBase`), aplicado em
  `MHR_GatherRules.OrderLimit` (usado por `AllowTarget`/`AllowLoose`). Ou seja, `colete 10` = **10 a mais**,
  mesmo com a mochila/baús cheios; ao bater o teto ele encerra a ordem e volta a seguir. Para os outros jobs o
  número segue sendo só informativo.
- Backend/contrato de referência: `docs/re/backend-contract.md`; mapa da fase: `docs/roadmap.md` (Trilha D).

## Nossas docs

- `docs/vision.md` — a visão/escopo (companheiro como membro da equipe) e o que falta.
- `docs/roadmap.md` — fases e status.
- `docs/plan-fases.md` — plano das fases com mapa do upstream.
- `docs/audit-vs-offline-companions.md` — auditoria do nosso mod anterior vs o upstream.
- `docs/re/` — engenharia reversa do Valheim 1.0 (APIs, diff de versão).
- `docs/references.md` — fontes de terceiros e licenças.

## Skills (carregue antes de escrever/revisar código)

- `bepinex` · `jotunn` · `thunderstore-packaging` · `valheim-reverse-engineering` · `valheim-ai-backend` · `valheim-assets`

## Regras de trabalho

1. **Carregue a skill** da dependência envolvida antes de codar ou revisar.
2. **Não quebre o comportamento do upstream** — nossas mudanças são: **tradução PT-BR** + **camada de IA**.
   Mudanças de lógica só com motivo claro e documentado.
3. **Tradução**: use o sistema de localização do próprio mod (`Translations/*.json`, `ModLocalization.cs`) —
   UI/hover/HUD — e `Translations/speech/<Idioma>.json` para as falas (`SpeechConfig`).
   Não hardcode PT-BR no código quando houver token de localização.
4. **Sem segredos** hardcoded (chaves de LLM/API via `ConfigEntry` ou variável de ambiente).
5. **Sempre commitar e dar push** ao terminar (`git add -A && git commit && git push`), mensagem no padrão
   `tipo(escopo): resumo`.
6. Atualize a documentação correspondente (`README.md`, `package/CHANGELOG.md`, `docs/roadmap.md`,
   `AGENTS.md`) quando algo mudar.

## MCP servers disponíveis

- **`alvin`** — MCP do **Visual Studio** (build, debug, Output, terminal, testes). Requer o VS aberto.
- **`context7`** — documentação atualizada de bibliotecas.
- **`learn`** — Microsoft Learn (.NET/C#/Azure).
- **`github`** — repositório/PRs/issues (requer auth via `/mcps`).

## Definição de pronto

- Compila em Release sem erros.
- Carrega no Valheim 1.0 sem exceção no `LogOutput.log`.
- Textos novos em **PT-BR** via localização.
- Sem regressão perceptível de performance.
