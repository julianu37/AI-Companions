# Backlog pós-1.9.4 — o que ficou sem correção (detalhado)

Origem: revisão de 10 especialistas (31/10/2026) + decisões do jogador. Cada item diz **o que está quebrado
hoje**, **o que pode quebrar se ficar**, **por que corrigir** e **o esforço**. Nada aqui foi corrigido na 1.9.4
(exceto o que está marcado como feito no `docs/roadmap.md`).

## A. Vitória rápida (1 linha cada, alto valor)

| # | O que está quebrado hoje | O que pode quebrar | Por que corrigir | Onde | Esforço |
|---|---|---|---|---|---|
| A1 | `FightLikePlayer` cai em `true` se o config não estiver pronto (Init falhou, chamada antes do `Init`): o companheiro volta ao combate do upstream | O jogador vê o comportamento que a 1.9.4 reverteu, sem explicação | Consistência com o default documentado; é a única config cujo fallback contradiz o default | `AI/CompanionCombatTactics.cs:75` (`?? true`) | 1 linha |
| A2 | `IsInventoryRequest` não tem a guarda de `?`: **"abre o inventário?" executa** (abre a janela) | Pergunta vira ordem por engano | A invariante "pergunta não é ordem" vale nas outras 3 redes | `AI/CompanionOrders.cs:739-777` | 1 linha |
| A3 | O modal de recuperar companheiro no Santuário é **inglês fixo** (`"Bring X back here?"`, `"Warning:"`) | Jogador PT/ES/FR/IT não entende a confirmação de uma ação destrutiva (gasta a lápide) | 5 chaves já existem no JSON PT; é a promessa de 5 idiomas | `UI/ShrinePanel.cs:130,133,137` | 3 chamadas + 1 chave nova |
| A4 | O roster mostra `" (unsummoned - Odin's Shrine can bring it back...)"` em inglês | Texto em inglês no meio da UI traduzida | Mesma regra do A3 | `Companion/MHR_Roster.cs:875` (e `:851` cai no fallback) | 2 chaves novas nos 5 idiomas |
| A5 | A durabilidade da armadura não cai pela parte de **fogo/veneno/espírito** do golpe (o `RPC_Damage` zera esses campos antes do postfix ler) | Armadura dura mais que no vanilla; o jogador não entende por que a peça não desgasta | É o dano que mais desgasta armadura no jogo | `Patches/DurabilityPatches.cs:104` | Prefix + `__state` (padrão do `CompanionBlockLog`) |
| A6 | `CompanionHitLog` roda em **todo** dano do jogo sem `try/catch` | Uma exceção no log **aborta o dano do golpe** (e o frame do atacante) | Todos os vizinhos de caminho quente são fail-open | `Patches/CompanionHitLog.cs:11-32` | `try/catch` |
| A7 | `TombstonePreventDespawnPatch` faz `catch { return false; }` (fail-**closed**, silencioso) | Se a reflexão falhar (jogo mudou), a lápide do companheiro **nunca despawna** — o sintoma que o jogador já viu | Devolver o `UpdateDespawn` ao jogo é o comportamento seguro | `Patches/TombstonePreventDespawnPatch.cs:22-37` | `catch` com aviso + `return true` |
| A8 | `EnemyHud` faz `transform.GetComponent<RectTransform>().rect.height` sem null-check | NRE dentro do `ShowHud` do jogo se o prefab do `Health` deixar de ser UI | O código já prevê o caso degenerado (`num <= 0 → 8f`), falta só o componente nulo | `Patches/EnemyHudPatches.cs:53-58` | 2 linhas |
| A9 | `readme_to_nexus.py` manda instalar em `BepInEx/plugins/OfflineCompanionsReborn/` | O jogador instala na pasta errada e reporta "não carrega" | É o texto que vira a página do Nexus | `tools/readme_to_nexus.py:15` | 1 linha |
| A10 | O `CONFIGURATION.md` gerado mapeia **22 das 24 seções** (sem `Controller`/`Torch`) | O jogador PT não descobre que `[Controller]` é "Gamepad" e `[Torch]` é "Tocha" | Doc gerada e embarcada; o erro é do gerador | `tools/gen_config_doc.py` | mapa das 24 + regenerar |
| A11 | `AI/PickupIntent.cs` está com UTF-8 duplamente codificado nos comentários (`3Âª correÃ§Ã£o`) | Um dia alguém copia uma tabela com acento e o casamento quebra em silêncio | É o arquivo da rede `drop × pickup × nopickup`; a doc do projeto é o produto | `AI/PickupIntent.cs` (comentários) | reescrever o arquivo |
| A12 | `ModUiAudit` não conhece o painel de aparência (1709) | Com `[Advanced] UiDiag = true`, a própria janela do mod é reportada como "canvas de fora em cima dos controles" | Falso positivo no diagnóstico que existe justamente para achar clique engolido | `UI/ModUiAudit.cs:162-178` | 1 condição |

## B. Médio (comportamento ou performance)

| # | O que está quebrado hoje | O que pode quebrar | Por que corrigir | Onde | Esforço |
|---|---|---|---|---|---|
| B1 | O `amount` do **modelo** ganha do número do **jogador** quando a mensagem tem número: "colete 2 madeiras" com o 3B dizendo 20 → coleta 20 | Ordem com quantidade errada (o jogador pediu 2) | Doutrina do projeto: as palavras do jogador mandam; hoje o caminho da rede usa o texto e o caminho do modelo usa o modelo | `CompanionOrders.cs:187-198` | pequeno |
| B2 | `SetHomeRadius` escreve **direto** (sem `MHR_WriteOrQueue`) e `HC_HomeRadius` não viaja no túmulo nem no roster | No MP a escolha é revertida sem aviso; no SP o raio volta a **50 m** na morte/recuperação | O `ChangeTier` já copia — a intenção existe, falta o caminho da morte | `CompanionSetup.cs:2658`, `CompanionTombstoneResurrect.cs:181-203` | médio |
| B3 | O claim da cama escreve num ZDO lido **antes** do `ClaimOwnership()` (sem reler nem checar posse) | No MP o claim se perde em silêncio: o companheiro "esquece" a cama e ela fica livre para outro | Mesma classe de bug já corrigida no `CompanionProfile.Write` | `CompanionRest.cs:1633-1641` | pequeno |
| B4 | `CompanionEncumbrance` soma o **peso inteiro** do inventário por frame (postfix de `Character.IsEncumbered`) | ~24 mil somas/s com 10 companheiros a 60 fps — trabalho que o vanilla não fazia | Regra "sem trabalho por frame em método quente" | `Patches/CompanionEncumbrance.cs:57-67` | pequeno (cache do `CompanionCarry`) |
| B5 | `ReturnLogic.Tick` aloca `Hashtable` com chave `int` (boxing) e faz **reflexão com `object[2]` novo** por frame | 3-4 objetos por companheiro por frame (~1.800/s com 10) | Caminho quente (`CompanionAI.UpdateAI`) | `AI/ReturnLogic.cs:129-143,452-500` | pequeno |
| B6 | `CompanionStatus.Doing` chama `Loc` a cada frame com o companheiro sobrecarregado | Alocação por frame, para sempre, por companheiro | O `if (!_wasHeavy)` logo abaixo já mostra o padrão certo | `CompanionSetup.cs:1153` | 3 linhas |
| B7 | `HC_Food_0..4` não é copiado no `ChangeTier` | O corpo novo nasce de barriga vazia: `MaxStamina` cai e ele **come outra refeição** da mochila | O `ChangeTier` existe para "carregar tudo o que faz o companheiro ser aquele companheiro" | `CompanionManager.cs:659-719` | pequeno |
| B8 | Caches estáticos por `GetInstanceID()` nunca são podados (`MHR_Mover`, `MHR_Recover`, `CompanionStatus`, `MHR_RiseWatch`, `ContainerFix`) | Cresce por morte/respawn/troca de tipo até sair do mundo (o `ContainerFix` guarda `byte[]` do ZDO) | Sessões longas com muitos respawns | vários | pequeno |
| B9 | `CompanionGone`/`GetStatuses` percorrem **todos os ZDO do mundo** na main thread | Trava proporcional ao tamanho do mundo; o painel do Santuário paga a varredura a cada abertura | Mundos grandes/MP | `MHR_Roster.cs:776-835` | médio |
| B10 | `WarnNoStructuredOnce` diz "não vai tentar de novo nesta sessão", mas a cadeia de 3 tentativas é remontada **a cada mensagem** | Num servidor que recusa `response_format`, **3 requisições por ordem** para sempre (3× a cota do Groq free) | Custo e latência; o log mente | `LlmClient.cs:453-463` × `:524-556` | pequeno |
| B11 | Qualquer **HTTP 400** conta como "recusa de formato" (inclusive "model not found") | Modelo errado gasta 3 requisições e o log acusa o motivo errado | Diagnóstico correto; o corpo do 400 tem a pista | `LlmClient.cs:409-418` | pequeno |
| B12 | Os exemplos dos 5 contratos têm **5 chaves** (sem `companion`) contra o schema de 6 | No fallback sem schema o modelo pode omitir `companion` e a ordem vai sem endereço | O 3B copia o exemplo (few-shot overfitting já documentado) | `CompanionChatAI.cs:156,240,323,408,492` | pequeno |
| B13 | `[Speech] MinInterval`/`MaxInterval` são configs **mortas** (o código fixa 90–180 s) e 6 lugares prometem 15–30 s | Mexer no slider não muda nada; doc mentindo | Config que não faz o que promete é bug | `CompanionTalk.cs:312` × `ModConfig.cs:1037` | pequeno |
| B14 | Reflexões dispensáveis: `Bed.GetOwner` (é `zdo.GetLong(s_owner)`), `EnvMan.GetCurrentDay` (é público), `Humanoid.RightItem/LeftItem` (públicos) | Se o jogo renomear, falha em silêncio: "não tem tocha/escudo" e o bloqueio para | Custo zero trocar por API pública | `CompanionRest.cs:13`, `DevBridge/Commands.cs:290`, `ReflectionHelper.cs:12` | pequeno |
| B15 | `Container.IsInUse()` (flag **local**) usado em 3 pontos em vez de `MHR_Chests.InUse` (local + ZDO) | No MP um baú aberto no outro cliente parece livre: dois mexendo no mesmo baú | O padrão certo já existe no mod | `CookController.cs:1699,1738`, `CompanionAI.cs:976` | 3 chamadas |
| B16 | `Container.SetInUse` derreferencia `Player.m_localPlayer` e o mod chama no teardown | NRE ao sair do mundo com container aberto; o flag "in use" pode ficar preso | Chamadas em caminho onde o player já é `null` | `CompanionContainerUse.cs:64,87` | pequeno |
| B17 | `TargetHealth()` usa o default sem o multiplicador de **nível de mundo** | Em mundo com nível > 0 o alvo parece mais fraco antes do 1º golpe (decisão de "mais um golpe derruba") | Correção de leitura do jogo | `HarvestController.cs:2220-2242` | pequeno |
| B18 | Código morto `QueueRespawn`/`ProcessRespawns`/`DoRespawn` (não copia `HC_CompanionId`) | Se alguém religar, o companheiro renasce com id novo → **duas entradas no roster** (consome vaga) | Remover ou alinhar com o `MHR_ResurrectCore` | `CompanionManager.cs:803-992` | pequeno |

## C. Maior/arriscado (exige decisão)

| # | O que está quebrado hoje | O que pode quebrar | Por que corrigir | Onde | Esforço |
|---|---|---|---|---|---|
| C1 | **Bloco de multiplayer**: `MHR_WriteOrQueue` nunca enfileira (o `IsOwner()` é otimista), ordem de chat/voz alcança companheiro de **outro jogador**, `LootTombstone` sem claim (item nos dois lugares), janela da mochila antes da posse, desarme da lápide, `Roster.Recover` sem `try/catch`, `EnsureLoaded`/`s_byId`, dono vazio = "de todo mundo" | Perda/duplicação de item, ordem no companheiro errado, corpo meio-construído | Só faz sentido com servidor para testar | vários | grande |
| C2 | As migrações de config rodam **em todo boot** e revertem escolha legítima (o log diz "escolhas suas não são tocadas") | Jogador com `Memory = 6`/raio antigo vê o valor voltar a cada inicialização | Relato clássico de "o mod desfaz minha config" | `ModConfig.cs:1140,1161,1165` | médio (carimbo `ConfigVersion`) |
| C3 | O DevBridge sobe **antes** do Harmony e sem `try/catch` | Porta 47110 ocupada ⇒ `SocketException` no `Awake` ⇒ BepInEx marca o plugin como falho: **sem patches e sem painéis** | Ferramenta de dev não pode derrubar o mod | `CompanionsPlugin.cs:156`, `BridgeServer.cs:47-51` | pequeno |
| C4 | O `MenuPatches` só fia a navegação no `UpdateNavigation`, que roda **antes** do `Show` injetar o botão | Na **1ª** abertura o controle não alcança o "Mod Options" (o fix #14 só vale da 2ª em diante) | É o mesmo sintoma que o #14 atacou | `MenuPatches.cs:19-58` | pequeno |
| C5 | O F8 aloca por frame (`GetComponentsInChildren<Selectable>` + concatenação da legenda) | GC contínuo com o painel aberto | Regra "sem alocação por frame" | `ConfigPanel.cs:577-583,1120` | pequeno |
| C6 | Falta um **gate único de release**: `release-check` (build + traduções + locale + config + MCP), `merge-harness` sem fonte versionada, `locale-fallback-check.ps1` imprime 2 regras em vez de asseverar, `audit_translations.py` no PT sempre sai 1, `gen_config_doc.py` sem `--check` | Release passa com doc/config divergente ou teste que não falha | Evita a classe de bug que a 1.9.4 passou o dia caçando | `tools/*` | médio |
| C7 | `ConfigPanel.OnDestroy` destrói a **textura compartilhada** do `TextureLoader`; `CompanionAiPanel` não libera `_panelSprite` no rebuild por idioma | Mina: o cache fica com textura destruída e todos os painéis caem na cor chapada até reiniciar | Vazamento/mina de UI | `ConfigPanel.cs:715-727`, `CompanionAiPanel.cs:957,1043` | pequeno |
| C8 | A wiki anuncia 1.9.1 e "914 chaves" (86 ocorrências em 5 idiomas) | Quem instala pelo guia baixa duas releases atrás | **Decisão do jogador:** atualizar só quando ele pedir | `docs/wiki/*` | médio |

## Feito na 1.9.4 (para não repetir)

- **Lápide com o dono certo** (`TombStone.Setup` recebia o id da lápide no lugar do id do jogador) — testado em jogo.
- **Memória da conversa localizada** (EN nativo + PT/ES/FR/IT) e **antes** do contrato.
- **Staging por allow-list** (`tools/stage_release.py`) e **layout do zip** de volta ao do 1.9.1 (modelos dentro de
  `BepInEx\plugins\AI-Companions`); "sem-modelos" descontinuado (última: 1.9.1).
- **224 testes versionados** das redes por palavras (`tools/ai-intents-check`).
- **Menu do controle** (não aponta mais para si mesmo) e **AOE** (não recebe `null` do `SpawnOnHitTerrain`).
