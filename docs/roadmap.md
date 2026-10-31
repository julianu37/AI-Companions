# Roadmap — ModIA

Recriação, para o **Valheim 1.0 atual**, de um mod de companheiro NPC com IA (inspirado no "Thrall").

Legenda: ✅ concluído · 🔄 em andamento · ⏳ pendente

---

## Trilha A — Equipe de desenvolvimento (agentes + skills)

| Item | Status |
|---|---|
| 10 agentes (1 líder + 9 especialistas) em `.opencode/agents/` | ✅ |
| 5 skills de dependências (`bepinex`, `jotunn`, `valheim-reverse-engineering`, `valheim-ai-backend`, `valheim-assets`) | ✅ |
| `AGENTS.md`, `opencode.jsonc`, references (BepInEx, Jotunn) | ✅ |
| MCPs (`alvin` = Visual Studio, `context7`, `learn`) | ✅ |
| Repositório no GitHub | ✅ |

## Trilha B — O mod

| Fase | Descrição | Status |
|---|---|---|
| **0** | Setup: toolchain, solução net472, plugin "hello world" | ✅ |
| **1** | Reconhecimento: decompilar Thrall + jogo 1.0, mapa de features e diff de API (`docs/re/`) | ✅ |
| **2** | **Núcleo do NPC** | 🔄 |
| 2.0 | Publicizer das assemblies do jogo | ✅ |
| 2.1 | Spawn/dismiss (F1/H) + seguir o jogador | ✅ |
| 2.2 | `CompanionAI : MonsterAI` (herança correta do motor de IA) | ✅ |
| 2.3 | Inventário/equip + aparência hash-based do `VisEquipment` | ✅ |
| 2.3b | Corpo base viking (clone do `Player`) + aparência padrão | ✅ |
| 2.4a | Combate: Passive/Defensive/Aggressive, seleção de alvo, revidar | ✅ |
| 2.4b | Coleta de recursos (madeira/pedra/itens) | ✅ |
| 2.4b-fix | Coleta de madeira: derrubar árvores (seguir a cascata tora→toras menores) + pontuação por rendimento real | ✅ |
| **2.6** | **Interface** | 🔄 |
| 2.6a | Hover do companheiro: nome + ações com as teclas (PT-BR, tokens de localização) | ✅ |
| 2.6b | Menu do companheiro (painel Jotunn `GUIManager`): status, aparência ao vivo, combate, coleta e teclas | ✅ |
| 2.7 | Persistência do companheiro: ao re-invocar (F1) volta o mesmo NPC (itens, equipamento, aparência e modo), com snapshot em memória + JSON opcional | ✅ |
| 2.8 | Coleta ampliada: lista curada de materiais (pedra/minérios/colhíveis) + dropdown no menu + mira na rocha + ferramenta fornecida | ✅ |
| 2.9 | Coleta: correções da auditoria (espera pelos drops, rocha "rachada", tora mais próxima, ferramenta por tier) | ✅ |
| 2.10 | Movimento/combate: proteção contra queda, portas no caminho, bloqueio/parry e filtro de alvo passivo | ✅ |
| 2.11 | Coleta: fim do loop de escolher/largar nó (seleção respeita a "coleira" do jogador) + log só na mudança | ✅ |
| 2.12 | Loot: catar os drops das criaturas abatidas pelo companheiro (patch em `Character.OnDeath` + estado de loot) | ✅ |
| 2.13 | **Membro da equipe**: comida (come sozinho do inventário), resistência (stamina) e habilidades que evoluem | ✅ |
| 2.14 | **Peso, depósito em baú e tool keeper**: teto de peso, ao encher leva a carga ao baú e retoma (ou pausa), ferramenta emprestada/devolvida do baú | ✅ |
| **2.16** | **Trabalhos (jobs)**: a mesma interface de "aponte para o trabalho" | 🔄 |
| 2.16a | Infraestrutura de jobs (`CompanionJobQueue`: um trabalho por vez + prioridade explícita) + **caçar** e **pescar** (`modia_job hunt\|fish\|stop`) | ✅ |
| 2.16b | **Cultivar** (zona de fazenda: plantar/colher/guardar), **cozinhar/bebidas** (espeto/forno/barril/caldeirão) e **fundições** (forno/alto-forno/carvoeira) — `modia_job farm\|cook\|smelt` + `modia_farm set\|clear\|info` | ✅ (v0.4.4) |
| **2.17** | **Base e casa**: cuidar da base (reparar/abastecer/colmeias/ordenar baús) e ter uma casa | ✅ (v0.4.5) |
| 2.17a | **Casa** (centro + raio): `modia_home set\|clear\|info`, gravada em `Casa/HomeZona`; o depósito prefere o baú da casa e ele volta para casa quando perdido (`Casa/VoltarSePerdido`) | ✅ (v0.4.5) |
| 2.17b | **Cuidar da base**: trabalho `upkeep` — reparar construções (`WearNTear`), abastecer fogueiras (`Fireplace`), esvaziar colmeias (`Beehive`) e ordenar baús — + modo contínuo (`modia_basecare on\|off`) | ✅ (v0.4.5) |
| **2.18** | **Qualidade de vida**: sentar no fogo, dormir na cama, seguir pelo portal e puxar a carroça | ✅ (v0.4.6) |
| 2.18a | **Descanso e sono** (`CompanionRest` + `[Descanso]`): senta na fogueira acesa (ou na casa) e dorme numa cama livre, com cura/stamina aceleradas e o **descanso** do jogo (números do `SE_Rested` + conforto); `modia_rest sit\|sleep\|stop\|info` | ✅ (v0.4.6) |
| 2.18b | **Seguir pelo portal** (`CompanionPortalFollow` + `[Portal]`): ele anda até o portal e é levado para junto de você quando o seu teleporte termina | ✅ (v0.4.6) |
| 2.18c | **Carroça** (`CompanionCart` + `[Carroca]`): `modia_cart pull\|release` — ele anda até o engate do `Vagon` e puxa a carroça pelo `ConfigurableJoint` do jogo | ✅ (v0.4.6) |
| 2.5 | Multiplayer (ZNetView/ZDO, testes com cliente/servidor) | ⏳ |
| **3** | IA e voz: comandos por voz/chat, STT, TTS, personalidade — **neste projeto, ver Trilha D** | ✅ (3.1, 3.1-saída estruturada, 3.1b, 3.1c, 3.1d, 3.2, 3.2c, 3.3, 3.3b, 3.3c, 3.3d, 3.4, 3.5, 3.6, 3.6b, 3.7, 3.8 — **todas validadas em jogo**) |
| **4** | Backend híbrido (LLM local + API), endpoints do "cérebro" | ⏳ |
| **5** | Empacotamento (README, ícone) + revisão final | ⏳ |

---

## Riscos conhecidos (do `docs/re/api-diff.md`)

1. 🔴 `VisEquipment` migrou para **hashes** de item (`int itemHash`) — *já tratado na 2.3*.
2. 🔴 `ZNet.PlayerInfo.m_host` removido → identidade por `PlatformUserID` — **afeta o `player_id` do backend** (Fase 3/4).
3. 🔴 Campos viraram `private` → **resolvido com o publicizer** (2.0).
4. 🔴 Bases viraram `protected virtual` → overrides ajustados.
5. 🟠 `m_follow` saiu de `BaseAI` para `MonsterAI` → `CompanionAI : MonsterAI` (2.2).

---

## Histórico de fases concluídas

- **Fase 0** — BepInEx 5.4.23.5 + Jotunn 2.30.2 instalados; plugin carregando no Valheim 1.0.17.
- **Fase 1** — 2 DLLs do Thrall decompilados; 4 documentos de análise em `docs/re/`.
- **Fase 2.0–2.4b** — companheiro viking com spawn/dismiss, follow, inventário/equip, aparência,
  combate e coleta de recursos (`modia_gather`/`modia_stop`, tecla F4).
- **Fase 2.4b (fix 0.2.7)** — coleta de madeira passou a **derrubar árvores**: a classificação
  segue a cadeia `TreeBase.m_logPrefab` → `TreeLog.m_subLogPrefab` (a madeira está nas toras, não na
  árvore) e a pontuação virou valor × esforço × distância, com diagnóstico por categoria em
  `Geral/LogDetalhado`.
- **Fase 2.6a (0.2.8)** — **hover do companheiro**: patch em `Character.GetHoverText`/
  `GetHoverName` (só para o NPC do mod) mostrando o nome e o mapa de teclas, com os rótulos em
  tokens do `LocalizationManager` do Jotunn e o texto cacheado por mudança de estado.
- **Fase 2.6b (0.2.9)** — **menu do companheiro**: painel de madeira do `GUIManager` (Jotunn) aberto
  pela tecla `Y` com o NPC a até 5 m, com status, aparência **ao vivo** (`VisEquipment`),
  modo de combate, coleta e a lista de teclas. Input bloqueado enquanto aberto (`BlockInput`),
  controles montados uma vez e fechamento automático se o companheiro sumir/afastar.
- **Fase 2.7 (0.3.0)** — **persistência do companheiro**: ao dispensar (H), ao morrer e no fim do
  mundo/fechar o jogo o NPC é fotografado (`CompanionStateStore`) e o próximo F1 devolve o mesmo
  companheiro (itens, equipamento, aparência e modo). Estado em memória sempre + JSON opcional em
  `BepInEx\config\modia.companion.state.json` (`Companheiro/PersistirEntreSessoes`), com versão de
  formato e itens revalidados contra o `ObjectDB`; `modia_state`/`modia_state clear` no console.
- **Fase 2.8 (0.3.1)** — **coleta ampliada**: lista curada de 39 materiais (`CompanionHarvestResources`)
  com apelidos PT/EN (madeira, pedra, minérios e colhíveis), **dropdown de recurso** no menu (com
  campo livre "Outro…"), **mira na superfície** de rochas (`GetAimPoint`) para os golpes conectarem
  e **fornecimento automático de machado/picareta** quando falta (`Coleta/FornecerFerramentas`) —
  pedra e minério passam a funcionar de primeira. Tabela de materiais no `README`.
- **Fase 2.9 (0.3.2)** — **correções de coleta da auditoria comparativa** (vs Offline Companions
  Reborn, MIT): **estado dedicado de coleta de drops** (espera 0,5 s os drops nascerem e varre o
  ponto de destruição; termina por 5 varreduras vazias ou 5 s — corrige o encerramento precoce em
  ~1,5 s), **rocha "rachada"** (segue a cópia no mesmo ponto — fim do "um golpe por pedra"),
  **tora mais próxima** (árvore derrubada → `TreeLog` imediata, preservando a cascata) e
  **ferramenta por tier** (`PickaxeIron` no padrão + melhor ferramenta da lista que atenda ao nó,
  o que destrava prata/obsidiana). Ver `docs/audit-vs-offline-companions.md`.
- **Fase 2.10 (0.3.3)** — **correções de movimento e combate da auditoria comparativa**: **proteção
  contra queda** (#2, sonda o chão à frente e desvia/aborta se cair mais que 1,2 m — configurável),
  **portas no caminho** (#5, abre/fecha `Door` sem abrir portas com chave nem em área privada),
  **bloqueio/parry** (#6, `Character.m_blocking` + `Humanoid.m_blockTimer` quando um inimigo próximo
  ataca) e **filtro de alvo passivo/neutro** (#14, ignora `AnimalAI` e criaturas que só revidam/fogem
  salvo `AtacarAnimaisNeutros = true`). Introduz o **dono do movimento por frame**
  (`CompanionMovement`/`MovementOwner`) para coleta, follow e guardas não disputarem o `MoveTo`.
  Ver `docs/audit-vs-offline-companions.md`.
- **Fase 2.11 (0.3.4)** — **fim do loop infinito da coleta** (bug crítico reportado em jogo): a
  varredura de nó passou a respeitar a **mesma "coleira" do jogador** que a guarda de distância do
  `CompanionAI` (antes ela escolhia um nó que a guarda largava no tick seguinte), o nó largado por
  "jogador se afastou" **não é re-escolhido no mesmo tick** (o companheiro **volta a seguir** na hora
  e só varre no próximo ciclo) e o log de escolha/largada **só sai quando o evento muda** (10 s de
  janela por nó+motivo). Também silencia o aviso de `Character.RaiseSkill` para o companheiro domado
  sem `Tameable` (`CompanionSkillPatches`), que era benigno mas poluía o log.
- **Fase 2.12 (0.4.0)** — **loot de criatura** (item #4 da auditoria): o companheiro **cata o que ele
  mesmo abateu**. Patch Harmony **observador** em `Character.OnDeath` lê o `m_lastHit` do falecido
  (o último golpe — o mesmo campo que o jogo usa para creditar o abate) e enfileira o ponto do abate;
  a camada `CompanionLoot`/`UpdateLoot` vai ao corpo, **espera os itens saírem do ragdoll** (eles só
  nascem quando o corpo desaparece), **segue o corpo** se ele escorregar e cata só os `ItemDrop` que
  **aquele** abate gerou (foto do "antes" ignora o que já estava no chão), respeitando raio, distância
  máxima, peso, fila de 6 e tetos de tempo. Prioridade preservada (**combate > coleta > loot/follow**,
  com o abate voltando à frente da fila e o relógio pausado) e novo `MovementOwner.Loot` no dono único
  do movimento por frame. Ver `docs/audit-vs-offline-companions.md`.
- **Fase 2.13 (0.4.1)** — **membro da equipe: comida, resistência e habilidades**. O companheiro
  **come sozinho** a comida do próprio inventário (slots próprios com a queima e a curva de bônus do
  jogo, `Character.SetMaxHealth(base + bônus)`, regeneração de `Σ m_foodRegen` a cada 10 s com
  `SEMan.ModifyHealthRegen`, efeito de status da comida e a animação de comer) — necessário porque os
  slots de comida são **privados do `Player`** e `Humanoid.ConsumeItem` é um **no-op** na base;
  **resistência própria** que importa (patches em `Character.UseStamina`/`HaveStamina`/`AddStamina` e
  postfix em `Character.CheckRun`, com gasto por corrida/nado no tick do `CompanionAI`) — sem ela,
  correr/atacar/bloquear/pular eram de graça para o NPC, porque a base é `virtual` e vazia; e
  **habilidades que evoluem** (`CompanionSkills` com a matemática do `Skills.Skill.Raise` e as
  `SkillDef` copiadas do prefab do jogador), com o `CompanionSkillPatches` **revisado**: o mesmo
  prefix que silenciava o aviso de `Tameable` agora **entrega o ganho** ao companheiro e o
  `GetSkillFactor` faz o nível valer no custo de stamina do ataque e no poder de bloqueio (o
  `GetRandomSkillFactor`, que é o multiplicador de **dano**, ficou de fora de propósito: a curva do
  jogador enfraqueceria o companheiro até ele treinar muito). Comida, stamina e perícias entram no **snapshot** (campos aditivos) e o console ganhou
  `modia_stats`. A coleta ganhou `CompanionHarvest.NotifyWaiting`: esperar stamina **não** é
  travamento (o nó não é vetado).
- **Fase 2.14 (0.4.2)** — **peso, depósito em baú e tool keeper** (itens #7 e #11 da auditoria). O
  companheiro ganhou **teto de peso** (`Companheiro/PesoMaximo`, padrão 300 kg, `0` = sem limite):
  antes de catar/coletar ele soma o peso do item (`ItemData.GetWeight`) ao que já carrega
  (`Inventory.GetTotalWeight`) e **nunca** passa do teto — vale para os drops do nó, os colhíveis e o
  **loot de criatura** (que passou a respeitar também o teto geral). Ao encher, a coleta **não
  encerra mais** (`não consegui pegar o item (inventário cheio?)` deixou de existir): o novo
  `CompanionDeposit` procura o **baú utilizável mais próximo** (`Piece.GetAllPiecesInRadius`,
  `Coleta/RaioBuscaBau` 30 m) com as regras do jogo (Container de `Piece`, `ZNetView` válida, não
  estar em uso, `PrivateArea.CheckAccess` como nas portas, privado só se for do jogador),
  **anda até ele** (`MovementOwner.Deposit`), **deposita** os materiais (`Inventory.MoveItemToThis`;
  equipamento/ferramenta/arma/comida/munição ficam com ele) e **volta** — a tarefa continua viva e o
  relógio do nó é renovado durante a viagem (ir ao baú é trabalho, não travamento). **Sem baú** ele
  **PAUSA** e avisa no HUD (a tarefa não é perdida) e **retoma sozinho** quando houver espaço.
  Prioridade **combate > coleta > depósito > loot > follow** (o abate volta para a frente da fila com
  o relógio parado). O **tool keeper** (`CompanionToolKeeper`) busca num baú a ferramenta que
  **falta** (só o tipo que falta) ou a que atende ao **tier do nó** (tier ≥ mínimo e dano > 0 contra
  as resistências, pontuação por tier/qualidade/dano/durabilidade, quebrada descartada) e **devolve**
  a emprestada quando a coleta termina (`Coleta/GuardarFerramentaNaoUsada`) — ferramenta **criada**
  nunca é devolvida. Console ganhou `modia_deposit` e o `modia_stats` mostra carga/depósito/empréstimos.
- **Fase 2.16a (0.4.3)** — **infraestrutura de TRABALHOS + CAÇAR e PESCAR**. O companheiro deixou de
  ser só "segue e coleta": o jogador **aponta para o trabalho** (`modia_job hunt|fish|stop`) e ele
  **executa de ponta a ponta** (o "aponte-os para o seu trabalho" do `docs/vision.md`).
  - **Fila única (`Companion/Jobs/CompanionJobQueue.cs`):** **um** trabalho ativo por vez; o
    controlador ativo dirige o companheiro e, ao terminar (meta, fauna esgotada, sem peixe, tempo
    esgotado), a fila fecha a conta (log + HUD) e ele **volta a seguir** você. Um comando novo
    **substitui** o trabalho em andamento; `modia_stop` para coleta + trabalho + combate.
  - **Base (`CompanionJob.cs`):** máquina de estados por trabalho (`OnStart`/`OnTick`/`OnStop`), sem
    `MonoBehaviour`, sem nada por frame (tick na camada `CompanionAI.UpdateJob`, ~20 Hz, varredura com
    intervalo e buffers reaproveitados) e **log deduplicado**.
  - **Prioridade explícita na ordem das camadas:** `combate > coleta > depósito > loot > TRABALHO >
    follow` (coleta e viagem ao baú **pausam** o trabalho, que retoma sozinho e não conta o tempo
    parado; o caçador espera o `CompanionLoot` catar o abate antes de caçar de novo). Dois donos novos
    no "dono único do movimento por frame": `MovementOwner.Hunt`/`MovementOwner.Fish`.
  - **CAÇAR (`HuntJob.cs`):** procura a **fauna** no raio (`Jobs/RaioHunt` 40 m) que **deixa algo
    cair** — o catálogo é lido dos prefabs do jogo (`Character` + `CharacterDrop`), então criaturas de
    mods entram sozinhas; nunca jogadores, **domados**, chefes ou o próprio companheiro. A presa é
    **entregue ao combate existente** (o mesmo caminho do modo Aggressive: mira, pathfinding,
    animação, hitbox, dano, bloqueio e stamina são do motor nativo), o abate é contado e o que caiu é
    **catado pelo `CompanionLoot`** (2.12) antes da próxima presa. Tem a mesma "coleira" do combate,
    troca por um alvo bem mais perto antes do primeiro golpe e veta 60 s a presa que escapou.
    Pássaros (`RandomFlyingBird`) ficaram para a 2.16b (não são `Character`: exigem estado de arco).
  - **PESCAR (`FishJob.cs` + `CompanionFishPatches.cs`):** pesca **de verdade** com o flutuador do
    jogo — margem com linha de visão, vara e isca do inventário (a isca escolhida pela chance em
    `Fish.m_baits`), arremesso com **força aprendida** de onde o flutuador caiu, espera da fisgada,
    puxão alternado (larga quando o peixe foge/linha tensiona) e o peixe guardado no inventário;
    respeita a **stamina** (descansa antes de arremessar) e a perícia de pesca vale (puxar mais
    rápido/barato). O `FishingFloat` do jogo **só conhece jogadores**, então três patches
    (`Setup`/`GetOwner`/`ReturnBait`) dão a ele uma **chave própria no ZDO** — sem eles o flutuador do
    companheiro se destruía na hora (e podia derrubar o do jogador). Fora do núcleo (2.16b): a grade
    completa de pontos de margem (`MapShore`) e o "voltar para casa com a carga" (o depósito da 2.14
    já cobre).
  - **Config `[Jobs]`** (PT-BR): `Ligado` (padrão `true`, e ele **nunca** começa um trabalho sozinho),
    `QuantidadePadrao`, `TempoMaximoPorJob` (20 min), `IntervaloVarredura`, `RaioHunt`, `RaioFish`,
    `DistanciaArremesso`, `EsperaFisgada`, `StaminaMinimaArremesso`, `FornecerVara`/`VaraPrefab`,
    `FornecerIsca`/`IscaPrefab`/`IscaQuantidade`. Console: `modia_job` + a linha do trabalho no
    `modia_stats`; HUD (canto superior esquerdo) com início, cada abate/peixe e o encerramento.
- **Fase 2.16b (0.4.4)** — os outros três trabalhos, sobre a mesma infraestrutura.
  - **CULTIVAR (`FarmJob.cs` + `FarmZone.cs` + `FarmCatalog.cs`):** o jogador define uma **zona de
    fazenda** (centro + raio) com `modia_farm set [raio]` — gravada em `Jobs/FarmZona`, sobrevive a
    reinícios; `modia_farm clear` apaga e `modia_farm info` mostra a zona e as culturas plantáveis.
    Dentro da zona ele **colhe** a planta pronta (`Pickable.Interact` + catar os frutos do chão),
    **planta** sementes do inventário (consome uma unidade e instancia o prefab com
    `TerrainModifier.SetTriggerOnPlaced`, como o `Player.PlacePiece`) e **guarda** a colheita no baú
    (o depósito da 2.14) a cada `FarmDepositarCada` itens, preservando `FarmManterSementes` de cada
    semente. As vagas exigem terreno **cultivado** (`Heightmap.IsCultivated`), bioma compatível,
    espaço livre (espaçamento ≥ raio de crescimento) e acesso em área privada. O que é plantável e o
    que é colhível vem dos **prefabs do jogo** (`Plant` + `Piece.m_resources[0]` = semente;
    `m_grownPrefabs` = frutos) — só culturas de terreno cultivado, então mudas de árvore ficam fora.
    Fora do núcleo: a UI de zona/cultura (picker/placer), a rotação em fileiras e buscar sementes em baús.
  - **COZINHAR / PREPARAR BEBIDAS (`CookJob.cs`):** opera espeto/forno de cozinha (`CookingStation`),
    barril (`Fermenter`) e caldeirão/mesa de preparo (`CraftingStation` com fogo) num raio
    (`CozinhaRaio`). Prioridade: **retirar o pronto (ou queimando)** → **tornear o barril** →
    **cozinhar no caldeirão** → **reabastecer o fogo** (a estação ou a fogueira embaixo dela, mantida
    em `CozinhaNivelCombustivel` do máximo) → **pôr comida** → **encher o barril**. Insumos vêm do
    inventário. Tudo por **RPC do ZNetView** (`RPC_AddItem`/`RPC_AddFuel`/`RPC_RemoveDoneItem`/
    `RPC_Tap`) e estado lido do ZDO (`slotN`/`slotstatusN`, `Content`/`StartTime`) — o `Interact` do
    jogo é do jogador (perícia + GUI). Fora do núcleo: a lista curada de receitas, colmeias (2.17) e
    buscar insumos em baús.
  - **FUNDIÇÕES (`SmeltJob.cs`):** forno/alto-forno e **carvoeira** (`Smelter` com `m_maxFuel == 0`).
    Prioridade: **catar o produto** do chão (o jogo larga no ponto de saída) → **combustível** →
    **minério** (escolhido pelas conversões do próprio forno, `m_conversion`), uma unidade por vez com
    `RPC_AddOre`/`RPC_AddFuel` e conferindo o ZDO antes (`FundicaoPorViagem` por ida). Fora do núcleo:
    buscar insumos em baús, ordenação entre vários fornos e pilhas de recurso.
  - **Comum aos três:** um **dono novo do movimento por frame** cada (`MovementOwner.Farm/Cook/Smelt`),
    varredura com intervalo e buffers reaproveitados, log deduplicado, HUD e `modia_stats` iguais aos
    outros trabalhos. Console: `modia_job farm|cook|smelt` e `modia_farm`.
- **Fase 2.17 (0.4.5)** — **BASE E CASA**.
  - **A CASA (`CompanionHome.cs` + `Casa/HomeZona`):** centro + raio definidos com
    `modia_home set [raio]` no ponto do jogador, gravados na config (sobrevivem a reinícios);
    `clear` apaga e `info` mostra. A casa é a referência de três coisas: o **depósito** prefere um
    baú de dentro dela (`Casa/PreferirBauDaCasa` — o baú da casa leva um desconto de distância
    igual ao raio na escolha da 2.14), o **cuidado da base** trabalha no raio dela e o
    **voltar quando perdido** (`Casa/VoltarSePerdido`, padrão desligado) traz o companheiro de volta
    quando ele fica longe da casa **e** do jogador (portal, morte longe, preso no terreno) — a
    camada roda depois do trabalho e reivindica `MovementOwner.Upkeep`.
  - **CUIDAR DA BASE (`UpkeepJob.cs` + `[Base]`):** `modia_job upkeep` (uma passada) ou
    `modia_basecare on` (modo contínuo, também `Base/Continuo`), com a prioridade
    **reparar → abastecer → colmeia → ordenar → juntar pilhas**. O **reparo** segue o caminho do
    `Player.Repair` do 1.0.17 (`WearNTear.Repair()` → `RPC_Repair`; martelo na mão, estação de craft
    da peça no alcance por `CraftingStation.HaveBuildStationInRange`, acesso na área privada e a
    chave `NoWorkbench`; **sem custo de material** — o custo é stamina (`Player.GetBuildStamina`) e a
    durabilidade do martelo; `Base/FornecerMartelo` cria um quando falta). As **fogueiras**
    (`Fireplace`) são abastecidas com o combustível do inventário pelo `RPC_AddFuel` até
    `Base/FogueiraNivel` do máximo (estado no ZDO, `ZDOVars.s_fuel`); as **colmeias** (`Beehive`) são
    esvaziadas pelo `RPC_Extract` (o mel cai no chão e é catado) a partir de `Base/ColmeiaMinima` do
    mel máximo (nível no ZDO, `ZDOVars.s_level`); e os **baús** são ordenados "igual com igual" (cada
    item repetido vai para o baú que já tem mais dele, carregando as pilhas na mão,
    `Base/OrdenarPorViagem`) com a **junção das pilhas parciais** do mesmo item
    (`Base/JuntarPilhas`), mexendo no inventário dos baús direto (nada de GUI) e conferindo o espaço
    antes de cada movimento (`CanAddItem`) para nunca perder item. No modo contínuo ele **volta ao
    centro** e espera quando não há nada para cuidar, e um `modia_stop`/`modia_job stop` desliga o
    modo. Dono novo do movimento por frame: `MovementOwner.Upkeep`. Console: `modia_job upkeep` +
    `modia_home` + `modia_basecare`. **Fora do núcleo:** o **visual da zona da casa** (o anel do
    `HomeZoneVisual`) e o **painel de ajuste** (`HomeZonePanel`) da referência, as **pilhas de
    recurso no chão** (`UpkeepPiles`), buscar combustível/insumos em baús (`FuelFetch`) e a lista de
    itens por categoria.
- **Fase 2.18 (0.4.6)** — **QUALIDADE DE VIDA** (o último bloco do SP).
  - **DESCANSO E SONO (`CompanionRest.cs` + `[Descanso]`):** quando o companheiro fica **ocioso**
    (`CompanionAI.JobIsIdle`: sem trabalho, coleta, viagem ao baú, loot nem alvo de combate) e você
    está perto (`Descanso/RaioJogador`), ele **senta** para descansar perto de uma **fogueira acesa**
    (`Fireplace.IsBurning`) — ou dentro da casa (`modia_home set`) — e **dorme numa cama livre**
    (`Bed`) de noite (`DormirDeNoite`) ou quando **você se deita** (`DormirQuandoVoceDorme`). A
    **cama do jogador é do jogador**: ele nunca usa a cama que é o seu **ponto de spawn atual**
    (`Bed.IsCurrent`) nem a de outro jogador (`Bed.GetOwner`). A **animação** sai pelo
    `ZSyncAnimation` do corpo (o mesmo componente que o `Player` usa): `SetBool("emote_sit", true)`
    para sentar e `SetBool("attach_bed", true)` para deitar (os dois **sincronizados pelo ZDO**,
    conferidos com `HasParameter`). O **sono** segue o caminho do `Player.AttachStart`/`AttachStop`
    do jogo (que é **vazio** para um NPC): posição do `Bed.m_spawnPoint`, gravidade desligada,
    colisão ignorada com os colliders da cama, `ZDOVars.s_inBed` no ZDO (é o que faz a noite passar
    mais rápido quando todos estão na cama) e `attach_bed` — desfeito inteiro ao acordar, quando ele
    levanta **ao lado** da cama. O **descanso** lê os números do `SE_Rested` vanilla
    (`ObjectDB.GetStatusEffect`: `m_baseTTL`, `m_TTLPerComfortLevel`, `m_healthRegenMultiplier`,
    `m_staminaRegenMultiplier`, `m_raiseSkillModifier`) e o **conforto** pelo caminho do jogo
    (`Cover.GetCoverForPoint` + `SE_Rested.CalculateComfortLevel`), depois de um aquecimento
    (`Descanso/TempoParaDescansar`): a stamina regenera com o multiplicador do descanso, a vida sobe
    por segundo e as perícias ganham o bônus (`CompanionSkills.RaiseSkill` ×
    `CompanionRest.SkillGainMultiplier`). Donos novos do movimento por frame:
    `MovementOwner.Rest`/`MovementOwner.Sleep`. Console: `modia_rest info|sit|sleep|stop`.
  - **SEGUIR PELO PORTAL (`CompanionPortalFollow.cs` + `[Portal]`):** no 1.0.17 quem atravessa um
    portal é o `Player` (`TeleportWorld.Interact` → `Player.TeleportTo`); o companheiro é um
    `Humanoid` e `Character.TeleportTo` é vazio na base — sem esta camada ele **fica para trás**. A
    detecção é por **observação do estado público do jogador** (`Player.IsTeleporting`, **sem patch
    Harmony**): quando o teleporte começa, guarda-se o `TeleportWorld` mais próximo e o companheiro
    **anda até o portal** (`MovementOwner.Portal`); quando o teleporte termina, ele é levado para
    junto do jogador (`CompanionAI.JobWarp`: corpo + `Rigidbody` + posição no ZDO, com
    `ZoneSystem.FindFloor` para assentar no chão). Ele **não** é levado se já estiver perto (portais
    curtos) e **não** segue se estiver ocupado (trabalho, coleta, depósito, loot, combate ou
    carroça) — aí quem resolve é o `Casa/VoltarSePerdido` (2.17). **Fora do núcleo:** o patch de
    `Player.TeleportTo`/`UpdateTeleport` da referência (aqui não há patch nenhum).
  - **CARROÇA (`CompanionCart.cs` + `[Carroca]`, `modia_cart pull|release`):** o companheiro anda
    até o **engate** do `Vagon` (`m_attachPoint.position - m_attachOffset`,
    `MovementOwner.Cart`) e chama o `Vagon.Interact` do jogo — é o próprio `ConfigurableJoint` do
    jogo que prende o corpo dele à carroça (`AttachTo`, com o `m_playerExtraPullMass`), exatamente
    como quando **você** puxa; depois de engatado ele volta a ser dirigido pelo follow nativo (é o
    que faz a carroça acompanhar você) e a camada só **vigia** (carroça destruída ou junta
    arrebentada → ele solta e volta a te seguir). Ele **nunca engata sozinho** e a carroça **não**
    atravessa portal.

---

## Trilha C — Tradução PT-BR (nosso projeto)

O upstream (Offline Companions Reborn) tem **dois** sistemas próprios de tradução, os dois alimentados por
arquivos JSON ao lado do DLL:

| Sistema | Arquivo | O que cobre |
|---|---|---|
| `ModLocalization` (UI) | `Translations/<Idioma>.json` | Painéis, menu radial, hover, HUD, mensagens do HUD, localização do Santuário/lista |
| `SpeechConfig` (falas) | `Speech/<Idioma>.json` (o `SpeechConfig.Load` monta `<pasta do plugin>/Speech/<Idioma>.json`; o `speech.json` da pasta do plugin é o reserva) | As falas do companheiro (saudações, conversa fiada, combate, fome, trabalho...) |

Idioma do jogo em PT-BR = **`Portuguese_Brazilian`**.

| Item | Status |
|---|---|
| `Translations/Portuguese_Brazilian.json` — **386 chaves** (todas as do `ModLocalization`, 1:1) | ✅ |
| `Speech/Portuguese_Brazilian.json` — **186 falas** (15 categorias, mesmas do `SpeechConfig.Defaults`) | ✅ |
| Cópia no build (`Companions.csproj` → alvo `DeployTranslations`, `Translations\**\*.json` → `$(DeployDir)\Translations\` e `Speech\**\*.json` → `$(DeployDir)\Speech\`) | ✅ |
| Textos fixos em inglês que o jogador vê (painel F8 + abas, painel da casa, HUD, hover do túmulo/cama/companheiro, recall, Santuário/lista, nomes de tipo do companheiro) localizados via `ModLocalization.Loc/LocFmt/LocOr` | ✅ |
| Descrições das configurações (~250) — só aparecem no `BepInEx\config\*.cfg`; o painel F8 mostra as chaves | ⏳ (fora do escopo; exigiria ~250 chaves novas) |
| Nomes técnicos das configurações no painel F8 (ex.: `MaxCarryWeight`) | ⏳ (mantidos iguais ao `.cfg`, por rastreabilidade) |
| Linhas de log/diagnóstico (`[Roster]`, `[Shrine]`, `[Direct]`, `[IA]`, `[TTS]`, `[STT]`, `[UiDiag]`, `[Collect]`, `[ECO]`...) | ⏳ (mantidas em inglês de propósito, para suporte) |

### Tradução PT-BR — auditoria de cobertura (branch `fix/traducao`)

Auditoria de **todo texto que o jogador pode ver**, com números (o método e os `grep`s estão no
`package/CHANGELOG.md` e nos commits da branch):

| Superfície | Antes | Depois |
|---|---|---|
| Chaves de UI (`ModLocalization` × `Translations/Portuguese_Brazilian.json`) | 374 chaves · **0 ausentes** · 7 com valor igual ao inglês (6 legítimas + `Push-to-talk`) | **386** chaves · 0 ausentes · 0 em inglês (as 6 que batem são legítimas: `Dverger`, `Stamina`, `Ok`, `Volume`) |
| Falas (`Speech/Portuguese_Brazilian.json`) | 15 categorias / 186 linhas · 0 ausentes | igual (só o `"Skål!"` é igual ao inglês — é um brinde) |
| Chamadas `Loc`/`LocFmt`/`LocOr` no `src/` | 251 referências · 1 **órfã** (`hc_radial_despawn_confirm`, num método morto) | **252 chaves literais + 4 prefixos de concatenação** (256 referências) · **0 órfãs** · **0 chaves definidas e nunca citadas** |
| Textos em inglês **hardcoded** em caminhos visíveis | 6 grupos (nome do companheiro e dos tipos, "has been resurrected!", "Hold", os 3 cumprimentos do recall, "ON"/"OFF" do F8, "Push-to-talk") | **0** (todos em chaves) |
| Docs (`README.md`, `package/README.md`, `package/GUIDE.md`, `package/CONFIGURATION.md`) | PT-BR | PT-BR (o `CHANGELOG` mantém o histórico do upstream, abaixo de 1.5.4, em inglês de propósito) |

**O que não dá para traduzir (não é nosso):** nomes de **item/criatura/cultura/cabelo/barba** (vêm dos assets do
jogo, o próprio Valheim localiza), os **tokens `$...`** do jogo (`$KEY_Use`, `$msg_skillup`, `$skill_*`, `$se_rested`),
as **chaves de configuração** (nomes internos do `.cfg`, mostrados no F8 por rastreabilidade) e as **~1500 linhas de
log/diagnóstico**, que só aparecem se o jogador abrir o `LogOutput.log`.

**Ficou de fora (de propósito):** o **histórico do upstream** no `package/CHANGELOG.md` (abaixo de 1.5.4, ~1270
linhas em inglês — é o registro dele) e o **texto do próprio jogo** (vanilla).

---

## Trilha D — Camada de IA (nosso projeto)

Ordens em **linguagem natural** por cima do mod do upstream. O companheiro continua sendo o do upstream
(radial, jobs, combate); a IA é só uma **nova entrada** para as mesmas ações — nada de novo dirige o NPC.

> **Legenda de status:** `✅ validado em jogo` = o **jogador** testou e confirmou, com evidência no `LogOutput.log`
> (a citação curta vai no status); `✅ build 0 erros ...` = verificado **só fora do jogo** (build/harness). O que
> continua aberto está em *Pendências conhecidas*, logo abaixo da tabela.

| Fase | Descrição | Status |
|---|---|---|
| **3.1** | **Ordem por texto no chat** (LLM local, API OpenAI-compatible) | ✅ validado em jogo |
| **3.2** | **Alvo da ordem**: `colete 5 madeiras` para em 5 (madeira/pedra/minério) e volta a seguir | ✅ validado em jogo |
| **3.2b** | Correção da 3.2: o número virou um **DELTA** | ❌ revertida pela 3.2c |
| **3.2c** | **Correção final da quantidade**: o número é um **ALVO TOTAL contado no inventário do companheiro**, só para o recurso pedido; com o alvo já na mochila ele nem começa e avisa | ✅ validado em jogo |
| 3.3 | **Ordens por voz**: push-to-talk (`Microphone`, 16 kHz mono) → **STT local** (Whisper.net) → **mesma caixa de ordens** do chat | ✅ validado em jogo (`[STT] TRANSCRICAO em ~2900 ms ... TEXTO: "..."` → ordem aplicada; **11 transcrições** push-to-talk no log do jogador) |
| 3.3-spike | **SPIKE STT local**: Whisper.net **in-process** (NuGet, thread de fundo) x `whisper-server.exe` filho — teste mínimo, gatilhos `modia_stt_teste` / `[IA] SttTeste` | ✅ validado em jogo (`docs/re/stt-whisper-net-spike.md`) |
| 3.3b | **Correções do teste da voz**: conflito da tecla **V** com o *Toggle Auto-Pickup* do jogo, qualidade do STT (modelo `small` + prompt inicial + trim) e medição da latência do LLM | ✅ validado em jogo (o log trouxe `[STT] CONFLITO DE TECLA: o auto-pickup do jogo esta em "<Keyboard>/V"`, as transcrições funcionaram e a latência passou a ser medida — `[IA] Tempo:`) |
| 3.3c | **Raio de coleta maior** (procura, alcance seguindo você, área com Follow off e drops) + **chatter aleatório desligado** por padrão (`[Speech] Chatter`) | ✅ validado em jogo (a coleta com o raio maior foi usada nos testes seguintes do jogador e o chatter aleatório parou — só aparecem as falas forçadas/por ordem) |
| 3.3d | **A ordem de pedra para no alvo** (`DropsOf` segue o `Destructible.m_spawnWhenDestroyed` + `OrderMet`/`OrderDoneNow`) e o texto da voz vai para o **balão do jogador** | ✅ validado em jogo (`Order target: mode 2 stops at 10 Stone in the pack` + `Order "stone" (x10)`; o balão do jogador foi testado pelo jogador) |
| 3.4 | TTS: o companheiro **fala** as respostas (Kokoro-82M local, `sherpa-onnx` + `AudioSource` 3D) | ✅ validado em jogo (`[TTS] Tocando ... na posicao de "HC_Companion(Clone)"`, `CACHE (0 ms)`, `RTF 0,83`; desligamento sem crash — `[TTS] Thread de sintese encerrada.`) |
| 3.5 | Personalidade/memória: preset + texto livre no contexto do LLM | ✅ validado em jogo |
| 3.6 | **Ordens com alvo**: `ataca o greydwarf` (engaja a criatura), `equipa o machado de bronze` (equipa do inventário dele), `larga a pedra` (joga fora) | ✅ validado em jogo |
| 3.6b | **Correção da ordem sem alvo**: quando o modelo responde `"target": null`, o item/criatura é lido das **palavras do próprio jogador**; o prompt passa a exigir `target` em `attack`/`equip`/`drop` | ✅ validado em jogo (`Order "equip": the model named no item - the player's own words named "picareta" (match 935, Fase 3.6b fallback)` → equipou) |
| 3.7 | **Painel da IA no jogo** (personalidade + voz com *Ouvir* + push-to-talk + toggles, tecla `I`) | ✅ validado em jogo (o jogador usou o painel: dropdown, teclas do jogo sem disparar, cursor/seleção e rodapé) — **ressalva:** o bug aberto do clique no painel **F8** (ver *Pendências conhecidas* abaixo) |
| 3.8 | **O companheiro abre o próprio inventário** por voz/chat (`inventory`/`inventory_close`) | ✅ validado em jogo (`Order from chat: "abre o inventario"` → `Order "inventory": Companion's window is open`) |
| 3.1 | **Saída estruturada** (`response_format`/JSON schema): garante JSON válido e destrava modelos pequenos (3B) | ✅ validado em jogo (`[IA] Saida estruturada ligada ([IA] SaidaEstruturada): ... corpo JSON de 6955 car.` + ordens funcionando com o 3B) |
| 3.1 (3B) | **Velocidade do 3B em jogo** (o modelo que a saída estruturada destravou) | ✅ validado em jogo (`[IA] Tempo: 401 ms / 340 ms / 388 ms`, contra ~3100 ms do `gemma` — **~8x mais rápido**) |
| 3.1b | **O recurso manda** (correção do teste em jogo): com `action` e `resource` inconsistentes (`colete 10 pedras` voltou como `wood`/`stone`) a ordem usa o **recurso** — o campo específico — e não a ação genérica | ✅ **validado em jogo** — log do jogador: `colete 10 pedras` → o modelo devolveu `{"action":"stone","resource":"stone","amount":10}` e o mod aplicou `Order "stone" (x10)` com `Order target: mode 2 stops at 10 Stone in the pack` em **378 ms**; + harness de 60 casos (fora do jogo) |
| 3.1c | **O contrato ensina que `action` e `resource` combinam** (a outra metade do achado): regra curta + 2 exemplos de `colete N pedras` → `stone`, porque o 3B copiava o `action:"wood"` do exemplo `colete 20 de madeira` | ✅ **validado em jogo** — depois da regra + dos 2 exemplos, o 3B passou a devolver `stone`/`stone` para `colete N pedras` (mesmo log da 3.1b) |
| 3.1d | **A ação de recolher os itens do chão** (`Jobs/MHR_Collect.cs`): a ordem `pickup` era **só um flag** - agora o companheiro **anda até o item no chão e pega** (inclusive o que ele/você largou, que é `m_autoPickup = false`), e o botão **Recolher** da roda passa a desligar mesmo o ímã | ✅ validado em jogo (teste final do jogador: *"pra mim parece excelente"*) |
| 3.9 | **Invocador na aba IA e uma aba por companheiro:** `SpawnFree` (o mesmo `SpawnCompanion` do Trader, sem cobrar), o painel com **uma aba por companheiro** (nome no título, Chamar/recall, voz/persona/volume) e o nome padrão numerado ("Guerreiro 2") para duas abas não ficarem idênticas | ✅ **publicada na 1.7.0** (`feat/multi-companheiro`) |
| 3.10 | **Perfil de IA por companheiro no ZDO:** `CompanionProfile` (voz `HC_AiVoice` + personalidade `HC_AiPersonaSet/Preset/Persona`), os valores `[IA]` como **padrão** de quem não escolheu; a lápide copia o perfil (`WriteAiProfile`/`ReadAiProfile`) e o respawn devolve | ✅ **publicada na 1.7.0** |
| 3.11 | **Nome no comando:** mensagem que começa com o nome de um companheiro real vale só para ele (prompt, persona, memória e ordem), com a lista de nomes no prompt e as **palavras do jogador** como rede de segurança; nome inexistente que **parece nome** avisa e não ordena (conservador desde a 3.19: capitalizado e fora das formas de tratamento) | ✅ **publicada na 1.7.0** |
| 3.13 / 3.15 | **Nome e tipo editáveis na aba do companheiro:** `CompanionSetup.Rename` (`HC_Name` + `Character.m_name`, claim + dono) e `CompanionManager.ChangeTier` (respawn preservando nome, inventário/equipamento com `m_equipped` limpo, aparência, casa/cama, skills, perfil de IA, id/geração, alvo da ordem, **ledger do cozinheiro, ferramentas emprestadas e stamina**) | ✅ **publicada na 1.7.0** |
| 3.17 | **A voz acompanha o balão:** `CompanionTalk.Say` fala a linha que mostra (voz do próprio companheiro, cacheável) e a deduplicação é um flag por chamada nos caminhos que já falam pela IA; `SayAsPlayer` nunca é falado e `[Speech] Chatter = false` continua valendo | ✅ **publicada na 1.7.0** |
| 3.19 | **Correções da revisão completa:** `inventory_close` no `ValidActions`/`Contract` (+ fallback quando o modelo devolve `inventory` com verbo de fechar); endereço desconhecido conservador ("mano, colete 10 pedras" volta a ordenar); nome do painel de interação no `onEndEdit` via `Rename`; troca de tipo recusada para morto e picker repintado ao morrer/renascer; ordens só gravam com posse e **repetem no Update** (`MHR_WriteOrQueue`); **id de requisição** do LLM (+ watchdog enfileira erro e descarta a resposta atrasada); lápide/`DoRespawn` comparam `HC_CompanionId`/lápide antes do nome; roster indexado por id e `RemoveStaleCopies` num portão de 1 s (dropando o inventário da cópia velha); caches de estoque por companheiro (`MHR_StockKey`); `SuppressAutoEquip` em `try/finally`; localização reinjetada quando o `Localization` é recriado; **teto `[General] MaxCompanions = 10`** nos três caminhos de criação | ✅ **publicada na 1.7.0** |
| 3.18 | **Editor de aparência com preview 3D:** `UI/CompanionAppearancePanel.cs` (botão *Aparência* na aba do companheiro) reaproveita o `CompanionPanel` do starter num **modo edição** — gênero, corte de cabelo, barba, pele, tom e sombra do cabelo, com **clone 3D** ao vivo; o apply vai por `CompanionSetup.ApplyAppearance` (ZDO `HC_Appearance`, **claim antes** do apply) **sem respawn** | ✅ **validada em jogo** (*"agora sim, perfeito!"*) |
| 3.17b | **O `stop` nomeia o trabalho:** `CompanionOrders` lê o modo do ZDO (`ActionModeHash`, a mesma chave do radial) **antes** de parar e responde `parar de coletar madeira` / `minerar` / `cozinhar` (antes dizia sempre `parar de seguir`); só a mensagem muda | ✅ **validada em jogo** (`Companheiro para de coletar madeira` → *"Ok: parar de coletar madeira"*) |
| 3.20 | **Os botões clonados do jogo e o cabelo/barba:** `interactable = true` em `CreateButton` **e** `CreateCategoryArrowButton` (o molde do botão de craft vem desabilitado e o clone ignorava todo clique — era isso que matava as setas de cabelo/barba depois do inventário); `PrepareVisualAttach` limpa o cache `m_currentHairItemHash`/`m_currentBeardItemHash` do `VisEquipment` (gravado **antes** do attach → no-op permanente quando o attach falhava) e procura o joint `Helmet`/`Head` nulo; `_storedSkinColor`/`_storedHairColor` restauram as cores antes de posicionar os sliders (eles abriam no início); diagnóstico `[AppearanceDiag]` | ✅ **validada em jogo** (*"agora sim, perfeito!"*) |
| 3.21 | **OpenRouter por padrão** (23/10/2026 — **superada pela 3.23**): `[IA] BaseUrl` = `https://openrouter.ai/api/v1`, `Modelo` = `openrouter/free`, `ApiKey` obrigatória (chave gratuita em `openrouter.ai/keys`); LM Studio/Ollama seguem como opção local. A aba IA ganhou a seção **Configurações do LLM** (toggles, URL/modelo/chave mascarada/prefixo, sliders, Aplicar e **Testar conexão** — `LlmConnectionTest`) | ✅ **validada em jogo** com o OpenRouter (ordem + conversa) |
| 3.22 | **O peso efetivo do companheiro — o Megingjord soma de verdade** (24/10/2026, bug do Nexus): `Companion/CompanionCarry.cs` soma ao `[General] MaxCarryWeight` o bônus lido do **asset** (`SE_Stats.m_addMaxCarryWeight`), com o item em **qualquer lugar da mochila**, deduplicado por referência do `StatusEffect` (dois cintos não somam) e **cacheado por inventário** (zero alocação por frame); vale no `Encumbered`, no painel/HUD (`/450`), no loot, no recolher, no ferreiro, no upkeep, na pesca/caça e no `[Gathering] OverweightThreshold`, que virou **base** (`UnloadThreshold = config + bônus` → coleta volta para casa em **448** com o cinto) | ✅ **validada em jogo** (*"ficou perfeito a correção do bug do usuário"*) |
| 3.23 | **Groq como LLM padrão** (24/10/2026, 1.8.1): `[IA] BaseUrl` = `https://api.groq.com/openai/v1`, `Modelo` = `openai/gpt-oss-20b`, `ApiKey` gratuita em `console.groq.com/keys`. **Ordem de preferência: Groq → LM Studio/Ollama (local) → OpenRouter** — o OpenRouter **continua documentado e funcionando**, mas o tier gratuito (`openrouter/free`) é lento/imprevisível e por isso saiu do padrão. `gpt-oss-20b` aceita `json_schema` em **strict** (a saída estruturada continua garantida). Limites do tier gratuito (fonte: `console.groq.com/docs/rate-limits`): **30 RPM, 1.000 RPD, 8.000 TPM, 200.000 TPD** → ~**4 ordens/min** na prática (~1.800 tokens por ordem) e **429** acima disso | ✅ build 0/0 e DLL no jogo |
| 3.24 | **Seletor de provedor + link da chave + enxugar o painel** (24/10/2026, 1.8.2, branch `feat/provedor-llm`): novo `AI/LlmProviders.cs` (tabela `Groq`/`LM Studio`/`OpenRouter`/`Personalizado` com `BaseUrl`, `Model` e `KeyUrl`, e `Suggest(baseUrl)` que **deduz** o provedor da própria URL — normalizando esquema, `/v1`, barra final, `/chat/completions` e o `localhost`↔`127.0.0.1` do LM Studio; **nenhuma config nova**, o `.cfg` continua a fonte da verdade). No `CompanionAiPanel`: dropdown "Selecione seu Provedor LLM" (o mesmo do "Tipo" do invocador) **acima da URL**, que só **preenche os campos** (grava no Aplicar) e **nunca toca na `ApiKey`**; botão **"Crie sua Chave de API Grátis AQUI"** na metade direita da linha da chave, com `GoldColor` + texto branco como um toggle ligado, **transição padrão** (realce suave no hover, sem estado depois do clique) e `Application.OpenURL` da URL fixa do provedor (Groq/OpenRouter; **some** no local/personalizado); e a **"Velocidade da fala" saiu do painel `I`** (fica só no F8) — chave `hc_ai_panel_speed` removida dos dois dicionários, painel **976 → 934** e a grade subiu 42 px sem buraco | ✅ **aprovado em jogo** (*"Aprovado!"*) |
| 4.0 | **Multilíngue com o inglês nativo** (25/10/2026, **1.8.3**, branch `languages`): **quebra o `.cfg`** — arquivo novo `AiCompanions.cfg` (o `Maddhattr.OfflineCompanionsReborn.cfg` é ignorado; GUID preservado; migrador do upstream continua), seção `[IA]` → **`[AI]`** com as **39 chaves em inglês** e **todas as descrições do `.cfg` em inglês** (237 recuperadas do upstream + as nossas escritas em inglês; referência PT em `docs/re/config-descricoes-pt-1.8.2.tsv`). O **F8 ficou bilíngue** (`LocOr("hc_cfg_<seção>_<chave>", <chave>)` + `hc_cat_*`): inglês = a própria chave, português = `Translations/Portuguese_Brazilian.json` (266 rótulos + 22 seções). Traduzir um idioma novo = `Translations/<Idioma>.json` + `Speech/<Idioma>.json` | ✅ testado em jogo (EN e PT) |

#### Backlog da revisão da 1.8.3 (não bloqueia a release)

A revisão completa (geral, Harmony, rede/ZDO e camada de IA) terminou **sem nada bloqueante**. O que foi corrigido está
no `CHANGELOG` 1.8.3; o que ficou para depois:

- **Multiplayer — sincronização de config**: as 266 chaves são locais; em MP cada peer usa o próprio `.cfg` e quem
  decide é quem **possui o ZDO naquele instante** (o Valheim migra a posse a cada 2 s). Prever um "host config"
  (chaves críticas no ZDO do companheiro ou num ZDO de mundo, ou RPC). O `HC_DeathItemRule` já é gravado no ZDO.
- **Multiplayer — escritas de ZDO sem posse** (não se perdem, mas disputam revisão): adoção de órfão em
  `CompanionSetup.HC_Owner`, `HC_HomeRadius`, claim de cama vanilla, `HC_RespawnAt`, `HC_FollowPausedByJob`,
  `ZDOVars.s_sleeping`. Padronizar por `MHR_WriteOrQueue`/`MHR_TryOwn`.
- **`ZNetScene.Destroy` bloqueado durante o unload do mundo** (`MHR_ZNetSceneDestroy_Patch` + `ProtectCompanions`):
  liberar quando `Game.instance == null || ZNetScene.instance == null`.
- **RPCs próprios** (`RPC_Wakeup`/`RPC_Sleep`): não validam remetente (impacto só cosmético hoje).
- **`ChangeTier`**: crash/disconnect no meio deixa dois ZDOs com o mesmo `HC_CompanionId` **e** a mesma `HC_Gen` (o
  `RemoveStaleCopies` só remove com `Gen` maior). Gravar `Gen + 1` no novo.
- **Tipos alternativos (dverger: Mago, Mago de Fogo, Mago de Gelo e Suporte) são DE COMBATE — e isso agora é
  intencional**: eles podem ser invocados e lutam como no upstream. A roda deles já esconde os trabalhos (o
  `if (!_isDverger)` do `CompanionRadialMenu`, idêntico ao upstream) e mantém **Follow, Repair and Restock, Despawn,
  Command, Pickup, Wander, Set Home** + as posturas — é o menu da captura do upstream. Desde a **1.8.3** (branch
  `fix/dverger-combate`) o **LLM e a tecla de comando também não conseguem** colocá-los para trabalhar:
  `CompanionSetup.IsWorkMode` + `ApplyActionMode` devolvendo `false`, recusa em `CompanionOrders` (chat/voz) e nas duas
  escritas diretas de `CompanionInteract` (`DirectSmelt`/`DirectGatherMode`), com a fala **"Sou de combate, não faço
  esse trabalho."** (`hc_ai_order_combat_only`), sem tocar no modo nem no teto da ordem; e o `ChangeTier` para um tipo
  de combate **volta ao Follow** e descarta o alvo. **O que continua como limitação:** o **painel** esconde os espaços
  de comida por tipo (`CompanionInteractPanel._builtForDverger`), e o `CanWearArmor = false` mantém as peças de
  armadura na mochila em vez de equipadas. **Inventário — hipótese, NÃO observada:** nesses tiers o
  `ContainerAwakePatch` mantém **duas** mochilas, a do `Container` (é a que o jogo **salva** e a que a UI mostra) e a do
  `Humanoid` (a que a IA/jobs leem), o que **sugere** que o que o companheiro coleta por conta própria iria para a
  segunda e não voltaria no reload. **Isso não foi confirmado em jogo** — o teste do jogador mostrou que o que ele põe
  na janela **persiste** — e a afirmação saiu da documentação do jogador de propósito. Para fechar a dúvida (30 s):
  matar algo perto de um dverger ou mandar `recolhe os itens do chão` e conferir se o loot aparece na janela dele
  (e se continua depois de sair e voltar ao mundo). **O que NÃO fazer:** trocar `Container.m_inventory` pelo inventário
  do `Humanoid` no `Awake` — foi tentado na 1.8.3 e **revertido** (`e8247ff`): o `Container.Awake` já rodou (inclusive
  o `Load` e o `InvokeRepeating("CheckForChanges")`) antes do swap, o `m_lastRevision` é carimbado no primeiro save e os
  itens que o jogador deu **deixam de persistir** (sintoma: "a madeira sumiu e o cajado voltou"; o `[ContainerDiag]`
  mostrou `SAVE items=1..6` sem nenhum `LOAD applied`). **Caminho seguro, se a hipótese se confirmar:** persistir o
  inventário do `Humanoid` numa chave **própria** do ZDO (`HC_Inv`), gravando só pelo dono (na mudança, com portão de
  tempo) e restaurando no spawn — sem tocar em `Container`, `m_inventory`, `Load` ou `CheckForChanges`; validar num
  mundo de teste antes de encostar no caminho compartilhado. **O tipo Companheiro (o padrão) está completo e validado
  em jogo.**
- **`inUse` da mochila do companheiro**: o `CompanionContainerUse` marca/libera o container ao abrir/fechar (impede dois
  jogadores editarem a mesma mochila) e libera um flag órfão após 5 s. Já validado em jogo (abrir/fechar 4 jeitos,
  troca de mundo, gamepad). Revisar só se algum dia a mochila recusar abrir sem motivo — o `Container.Load` **não**
  restaura enquanto `m_inUse` for verdadeiro, então um flag preso trava o conteúdo.
- **Chave `SKK_NoDestroy`** gravada no ZDO do companheiro é de **outro mod** (não é lida aqui) → trocar por
  `HC_NoDestroy`.
- **Companheiro com `Container` público**: qualquer jogador pode saquear a mochila. Decidir se vira privado (só o
  dono) — hoje é o comportamento herdado.
- **Navmesh próprio** (`MHR_CompanionNavMesh`): filtro de portas com `GetComponentInParent` por source/tile na main
  thread (stutter de zone-load) e `AgentType` fixo **40** (risco de colisão com outro mod que crie agente).
- **`FishingFloat.Setup` reimplementado** por prefix; **túmulo** (`TombStone.UpdateFloater`/`PositionCheck`) por
  reflexão de nomes; **`SE_Poison`** sem `Finalizer` (se `AddDamage` lançar, o TTL fica alterado).
- **Gamepad**: `Container.Interact` do companheiro recusa gamepad — o caminho alternativo (roda/HUD) precisa ser
  confirmado em jogo.
- **`[AI] Prefixo` vazio por padrão**: toda mensagem do chat vai ao LLM (inclusive conversa com outros jogadores em
  MP). Estudar `/c` como padrão ou um aviso no painel.
- **Aviso de timeout** pode ser engolido quando outra mensagem entra em voo (o jogador não sabe que a ordem morreu).
- **Prints antigos da wiki** (`config-ia.png`, `3-painel-f8.jpg`) mostram os nomes PT de antes da 1.8.3 — as legendas
  já avisam; trocar por capturas novas num próximo passe.
- **TTS é pt-BR**: com a resposta no idioma do jogador (1.8.3), uma resposta em inglês é falada com voz portuguesa.
  Avaliar idioma de voz por idioma de resposta ou silenciar o TTS quando a linha não for PT.
- **`inUse` da mochila do companheiro (1.8.3)**: o `CompanionContainerUse` libera um flag órfão depois de 5 s **quando
  este peer é dono** e nenhuma janela do `InventoryGui` mostra o container. Risco residual: se a **posse do ZDO migrar**
  entre peers com a janela aberta há mais de 5 s, o flag pode ser liberado — aceitável (quem perdeu a posse também não
  salvaria), mas anotar para o teste em servidor dedicado.
- **Assimetria do `IsOwner` no patch de durabilidade**: o `Prefix` aplica a redução de armadura em todos os clientes e o
  `Postfix` só drena durabilidade no dono (sem desync; só o log "Armor reduction" fica otimista em cliente não-dono).
- **`Hud.InRadial`**: a guarda de `m_radialMenu` na 1.8.3 é **defesa em profundidade** — o `InRadial` do jogo já checa
  nulo; o efeito prático é "os modais do mod deixam de bloquear input se o `m_radialMenu` do HUD sumir". O postfix de
  `Hud.UpdateCrosshair` (`CompanionRadialMenu`) ainda usa `m_crosshair`/`m_hoverName` sem checagem.

#### Pendências conhecidas (estado em out/2026)

O que **não** está fechado nesta trilha, listado sem enfeite:

| # | Pendência | Detalhe |
|---|---|---|
| 1 | ~~**Bug do clique no painel F8**~~ **✅ corrigido de verdade na branch `fix/camadas-ui` (a correção anterior, `fix/clique-f8`, era insuficiente)** | **A causa medida (o `[UiDiag]` do próprio painel achou):** o HUD do jogo é um canvas em **`sortingOrder = 400`** (`HUD`, com `overrideSorting`), o `EnemyHud` em 200 e o `HudMessage` em 1000 — e **todos** os canvases do mod estavam em **28..34**, isto é, **atrás** de tudo isso. O alvo que engolia o clique é o `HUD/hudroot/crosshair/HoverName` (um `TextMeshProUGUI` de **350x150** morto no **centro** da tela, exatamente onde o painel está), e ele **continua recebendo clique** mesmo invisível (o jogo o apaga com `canvasRenderer.SetAlpha(0f)`) — o que explica a **faixa** e por que rolar o painel tirava o controle dela. **Por que a tentativa anterior (31 → 33) não bastou:** **33 continua abaixo de 400**; subir um degrau dentro da mesma faixa não muda nada contra um canvas 367 degraus acima, e em empate de `sortingOrder` o uGUI desempata pela **ordem de registro dos raycasters** (`RaycasterManager`), que o canvas do jogo — registrado muito antes — ganha. O diagnóstico só ficou claro quando o audit passou a **listar as camadas da cena**: o log do jogador trouxe `HUD=400(override)`, `HudMessage=1000(override)`, `Menu=1700(override)`, `UnifiedPopup=1900(override)`, `LongPressRadial=2000(override)`, `_Console(Clone)=5000`. **Correção:** todos os canvases do mod foram para a faixa **1701..1708** (`src/Companions/UI/ModUiLayers.cs`, **fonte única** dos números) — **acima de todo o HUD do jogo** e ainda **abaixo** do `UnifiedPopup` (1900: o diálogo modal **do jogo**, uma classe vanilla de `assembly_valheim` que **não** é nossa e não pode ser movida, e um modal tem que ficar na frente de uma janela) e do `LongPressRadial` (2000: evitar o **empate** com um canvas do jogo) e muito abaixo do console (5000) e do `SwitchCursor` (12000). O mesmo `sortingOrder` alto resolve o **segundo** sintoma relatado, o **texto do centro da roda embaralhado com o hover do jogo** — o `crosshair/HoverName` desenhava **por cima** dos rótulos `_centerName`/`_centerAction`/`_centerState` do `CompanionRadialMenu`, que ficam no centro da tela. O `raycastTarget = false` dos decorativos (da correção anterior) **continua** valendo e continua certo. **O que foi descartado (com prova):** *nada dentro do painel* podia bloquear aqueles controles — no uGUI o clique vai para o gráfico de maior profundidade de render (a ordem dos irmãos), então um gráfico **ancestral** (o fundo do painel, o container da linha, o viewport do `ScrollRect`) **nunca** fica na frente dos próprios filhos; e as duas barras de scroll invisíveis, embora estejam acima das linhas, têm **10 px** de largura na borda direita e não cobrem o botão ON/OFF. **Não é regressão nossa:** as camadas 28..34 são **do upstream** (idênticas no repo de referência) e o `docs/DEV_NOTES.md` de lá registra o bug do F8 **sem solução**. **O audit (mantido e generalizado):** `[UiDiag] F8 raycast audit` — e agora também `[UiDiag] AI panel raycast audit` — roda **uma vez por sessão**, na primeira abertura do painel (nunca por frame), e reporta os alvos de clique **não-control** dentro do painel, **todas** as camadas ativas da cena (`nome=ordem`) e, para cada canvas **acima** do painel (exceto os do próprio mod, que são listados mas não sondados), os alvos que cobrem a coluna de controles do painel (11 pontos). Se a faixa voltar, o log diz **quem** é. Build: **0 erros, 0 avisos** + DLL implantada. **O teste em jogo é do jogador.** |
| 2 | **A voz não passa pelo mixer do jogo** | a `AudioSource` da fala não tem `outputAudioMixerGroup`: por isso ela **ignora os sliders de volume do Valheim** (música/SFX) e só obedece a `[IA] TtsVolume`. Roteá-la para o grupo `SFX` fica para depois — não era necessário para o eco, resolvido com `reverbZoneMix = 0` |
| 3 | ~~**`inventory_close` fora do `ValidActions`**~~ **✅ corrigido na branch `feat/multi-companheiro` (Fase 3.19)** | **O achado:** a ação existia e funcionava em toda a cadeia (`LlmIntent` → `"inventory_close"`, `CompanionOrders.CloseInventory`, o fallback `IsInventoryRequest`), mas `"inventory_close"` **não estava na lista `ValidActions`** — e é dela que sai o `enum` do JSON schema. Com `[IA] SaidaEstruturada = true` (o padrão) o modelo **não podia** devolver a ação, entendia *"fecha o inventário"* como `inventory` e a janela **abria**. **Correção:** `"inventory_close"` entrou no `ValidActions` (agora **30** ações), o `Contract` a cita com regra + 1 exemplo (`"fecha o inventário"` → `inventory_close`) e o `HandleResult` roda o `IsInventoryRequest` **também** quando o modelo devolve `inventory` **e** o texto do jogador tem verbo de fechar — a resposta errada é corrigida para `inventory_close`. **Teste em jogo (branch em teste):** abrir → `fecha o inventário` fecha, com a saída estruturada ligada e desligada; log `[IA] Order "inventory_close": window closed` |
| 4 | **Sync com o upstream — 1 release atrás** | a nossa base é a **`1.5.4`** (`267b8fe`) e o **`main`** do upstream está na **`1.5.5`** (`3b4affe`: *speech overhaul and status line, gear-first revive and stance keys, tilled-ground farming*). O serviço é **pequeno**: 1 commit, 45 arquivos, ~3.165 linhas de `.cs` em 36 arquivos e 4 novos (`CompanionStatus.cs`, `MHR_Recover.cs`, `UI/MHR_StanceKeys.cs`, `Patches/MHR_PieceDamageLog.cs`); o ponto delicado é o `CompanionTalk.cs` (**+440**, a reforma da fala — onde o nosso TTS se pluga). A história do fork **compartilha** a do upstream, então o caminho é `git merge` (docs com `--ours`). **⚠️ O alvo é o `main` — NUNCA a `claude/relaxed-archimedes-ecoh6h`:** aquela branch é o **WIP do autor** (atualizada várias vezes por dia, commits de *"Dev notes: backlog"*) e a numeração dela (até `1.5.42`) **não é release** — foi lê-la como release que gerou o erro de avaliação "38 versões atrás" (o real era 1). Não mergear, não rebasear, não usar como referência |
| 5 | **O nome do companheiro no centro do radial** | o nome do mundo (o *hover name* do jogo) aparece **por trás do centro transparente** da roda, então o texto do centro parece sobreposto (`CompanheiXuxa` nas capturas da 1.7.0). É **cosmético** e não afeta o clique (a roda está na camada 1703, acima do HUD em 400 — ver a correção das camadas). Ideias: fundo opaco no `CenterContainer` ou esconder o `HoverName` enquanto a roda está aberta (o radial já esconde o crosshair, mas não o hover name) |
| 6 | **Roster com N companheiros (risco médio)** | o `MHR_Roster` indexa por `HC_CompanionId` e o `RemoveStaleCopies` roda num portão de 1 s (dropando o inventário da cópia velha), mas o caminho foi exercitado com **poucos** companheiros: com 10 (o teto) e mortes/respawns no meio, vale um teste dedicado — é o ponto mais frágil da 1.7.0 |
| 7 | **Posse do ZDO no spawn em multiplayer** | o `ApplyAppearance`, o `Rename` e as ordens já fazem **claim antes** de escrever (`MHR_WriteOrQueue` com fila e aviso), mas o **spawn** de um companheiro novo (invocador/Trader) e a troca de tipo nunca foram testados em **servidor dedicado**. Revisar quando houver esse teste |
| 8 | **F8 só troca de idioma ao reabrir o jogo** | as abas e os rótulos do painel `F8` são montados **uma vez** (primeiro `Show`). Trocar o idioma do Valheim em runtime (o jogo aplica via `SetupLanguage`) deixa o painel no idioma antigo até reiniciar. Mitigação futura: reconstruir as abas no postfix de `SetupLanguage` (ou repintar os `TMP_Text`). **Baixa severidade** — o jogador normalmente escolhe o idioma antes de entrar no mundo |
| 9 | **Duas descrições EN citam falas em PT** | em `ModConfig` (`TtsCache` e `TtsSpeakModLines`) os exemplos entre aspas são falas reais do mod em português ("Já temos o suficiente."). É intencional (mostram o que é falado), mas quebra o "`.cfg` 100% inglês". **Cosmético** |

> **Fechado na 1.7.0:** o item **3** (`inventory_close`) e o **release/empacotamento**, que estava aberto aqui: a versão
> está no código, o `CHANGELOG` tem a seção `1.7.0`, a wiki foi republicada e os dois zips existem
> (`AI_Companions-1.7.0.zip`, ~368 MB, e `AI_Companions-1.7.0-sem-modelos.zip`, ~28,4 MB).
>
> **Fechado na 1.8.3 (25/10/2026):** tag `v1.8.3` (`c023ca1`) e GitHub Release `releases/tag/v1.8.3` publicada com os dois
> pacotes montados a partir de `pkg183\stage` — `AI_Companions-1.8.3.zip` (385.866.837 bytes, md5 `340d320d…`) e
> `AI_Companions-1.8.3-sem-modelos.zip` (29.846.898 bytes, md5 `0372b807…`). O pacote **não** leva o `speech.json` da
> raiz (é gerado em runtime quando falta o `Speech/<Idioma>.json`) nem os marcadores `__folder_managed_by_vortex` (que a
> 1.8.2 levou por engano). O `Companions.dll` dos dois zips é byte a byte o build da `main`
> (sha256 `ddae0042…`), conferido dentro do próprio zip. Falta só o upload manual no Nexus.

### Fase 3.1 — ordem por texto no chat (✅ — Unreleased)

O jogador digita `colete 20 de madeira` (ou `segue`, `para`, `vem cá`, `minere ferro`,
`fica na defensiva`) no chat do jogo e o companheiro executa.

| Peça | Arquivo | O que faz |
|---|---|---|
| Cliente HTTP | `src/Companions/AI/LlmClient.cs` | `POST {BaseUrl}/chat/completions` (LM Studio/Ollama/llama.cpp) numa `Task` de fundo + fila `ConcurrentQueue`; watchdog de timeout; nunca bloqueia a main thread |
| DTO + parser | `src/Companions/AI/LlmIntent.cs` | Extrai o JSON mesmo com texto/markdown em volta, trata `"null"`/vazio, aceita `amount` ausente e **normaliza PT-BR/EN** (ações e recursos, com sinônimos) |
| Intenção → ação | `src/Companions/AI/CompanionOrders.cs` | Reaproveita as mesmas chamadas do radial/tecla de comando (`CompanionSetup.ApplyActionMode`, `MHR_JobRules`, `SetCombatStance`, `MHR_FireSuperRecall`...) |
| Orquestração | `src/Companions/AI/CompanionChatAI.cs` | Patch Harmony em `Chat.SendText` → filtro de prefixo → LLM → ação; feedback no HUD/chat em PT-BR; cooldown e anti-spam; system prompt PT-BR |

- **Captura do chat:** postfix Harmony em `Chat.SendText(Talker.Type, string)`. Esse é o **único** caminho da
  mensagem **do próprio jogador** (o `/say` do chat passa por ele); mensagens recebidas entram por
  `Chat.OnNewChatMessage`/`Talker.RPC_Say`, então **não** são capturadas. `Ping` é ignorado.
- **Sem travar o jogo:** o POST roda em `Task.Run` (com `HttpWebRequest.Timeout`/`ReadWriteTimeout`) e o
  resultado volta por uma fila thread-safe drenada no `Update` do plugin. Nada de `.Result`/`.Wait()`.
  Um pedido por vez (slot de "em voo") + watchdog.
- **Refatoração mínima no upstream:** o corpo de `CompanionRadialMenu.SetActionMode` virou
  `CompanionSetup.ApplyActionMode(companion, mode, toggle)`; o radial chama com `toggle: true`
  (comportamento idêntico ao de antes) e o chat com `toggle: false` (ordem **seta** o modo, não alterna).
- **Config `[IA]`:** `Ligado` (true), `BaseUrl` (`http://localhost:1234/v1`), `Modelo`
  (`google/gemma-4-e4b`), `ApiKey` (""), `Temperatura` (0.1), `TimeoutSegundos` (30), `Prefixo` ("" = toda
  mensagem; ex. `/c`), `MostrarRespostaNoChat` (true), `CooldownSegundos` (3). Nenhum segredo no código;
  URL/modelo/chave/prefixo são strings do `.cfg`.
- **Ações mapeadas:** `follow`(0) `wood`(1) `stone`(2) `ore`(3) `forage`(5) `smelt`(6) `hunt`(7) `farm`(8)
  `fish`(9) `repair`(15) `cook`(17); `stop` → modo 0 + limpa alvo de combate; `come` → recall do mod;
  `sethome`(11) `wander`(12) `pickup`(13) `command`(14) e suas negações; posturas 20–23 → `SetCombatStance`.
  `none` = não é ordem. `despawn` **não** é exposto ao modelo (de propósito).
- **Pendências conhecidas (nota de status, atualizada):** a pendência de então — ordens com alvo (`atacar X`,
  `equipar Y`) ainda não têm efeito além do job — foi resolvida na **Fase 3.6** e no fallback da **3.6b**, os dois
  **validados em jogo**. O número da ordem **limita** madeira/pedra/minério desde a Fase 3.2 e é, desde a Fase 3.2c, um
  **alvo total contado no inventário do companheiro** ("colete 10" = ter 10 na mochila — ver abaixo).

### Fase 3.2 — o número da ordem limita o trabalho (✅ — Unreleased)

`colete 5 madeiras` era tratado como "liga o job Wood": o companheiro cortava até o limite da configuração
(`[Gathering] WoodLimit`, 500 por padrão). Agora o número da ordem é um **alvo por companheiro**.

| Peça | Arquivo | O que faz |
|---|---|---|
| Alvo no ZDO | `src/Companions/Companion/CompanionSetup.cs` | `HC_OrderCap` (int, 0 = sem alvo), `HC_OrderCapMode` (int, o modo a que o alvo pertence) e, desde a 3.2c, `HC_OrderCapItem` (string, o item do alvo) + `OrderCap`/`OrderCapItem`/`SetOrderCap`/`ClearOrderCap`/`CompleteOrderCap`/`OrderSeen` (escrita só no dono, `IsOwner`) |
| Ordem → alvo | `src/Companions/AI/CompanionOrders.cs` | Ordem de coleta com `amount > 0` (wood/stone/ore) grava o alvo; `stop`, `follow`, outra ordem de trabalho ou a troca de modo (`ApplyActionMode`) **limpam** o alvo |
| Alvo no job | `src/Companions/Jobs/MHR_GatherRules.cs` | `AllowTarget`/`AllowLoose` decidem o alvo em `OrderedItem`/`OrderSeen`/`Bag`/`LimitFor` (o `Name:Number` da lista, se for menor, continua ganhando no caminho da config); ao bater o alvo, ele **encerra a ordem** e volta ao follow. **Fase 3.2b (revertida pela 3.2c):** o número chegou a ser um delta (`estoque na hora da ordem + número`). **Fase 3.2c:** o número é um **alvo total contado no inventário do companheiro** e vale só para o recurso pedido — ver a seção da 3.2c |
| Fim da ordem | `src/Companions/Jobs/HarvestController.cs` | Um scan vazio passa por `MHR_GatherRules.OrderDone`: se o motivo foi o alvo, o companheiro larga a ferramenta e volta a seguir (como o `stop`) |

- **Como o alvo é lido:** direto do ZDO (um lookup de dicionário, no máximo uma vez por candidato por scan,
  **nunca por frame**). Nada de config nova e nada de alocação por frame.
- **`Name:Number` menor que o alvo continua ganhando** (`Wood:3` com a ordem `colete 5` para em 3) — no
  caminho da config, que é o que vale quando a ordem não nomeia um item (ver 3.2c).
- **Alvo apontado no radial** (`SetDirectedTarget`) **não** sofre o alvo — a fase 3.2 só mexe nas ordens de chat.
- **Coleta normal (sem ordem) inalterada:** sem alvo no ZDO, tudo continua sendo a config (radial e teclas idem).
- **Um detalhe honesto:** o alvo decide se o companheiro **começa** um alvo; a última árvore derrubada pode
  passar do número (a árvore inteira cai de uma vez). Depois disso ele para, diz "já temos madeira de sobra"
  e volta a seguir.
- **Multiplayer/reload:** o alvo vive no ZDO do companheiro, então cada companheiro para no seu próprio número
  e ele sobrevive a um reload; quem escreve é o dono (`MHR_Owner.TakeControl` antes da ordem).

### Fase 3.2b — o número da ordem é um DELTA (❌ revertida pela Fase 3.2c)

> **Revertida.** O delta não é o que o usuário quer: o número da ordem é um **alvo total contado no inventário
> do companheiro**, não "10 a mais". O código da 3.2b (`CompanionSetup.OrderCapBase`/`ClearOrderCapBase` e o
> `MHR_GatherRules.OrderLimit`) foi **removido** na 3.2c — a seção abaixo fica como registro do que foi tentado
> e por quê.

Bug reportado em jogo: *"solicito 10, ele coleta 1 ou 7 e para"*. O teto da 3.2 era comparado com o **estoque
total** (`Stock`: mochila + baús perto da casa + pilhas), então `colete 10 madeiras` virava *"tenha 10 no
total"*: com 9 de madeira guardada, o companheiro derrubava uma árvore e encerrava a ordem
(`Order ceiling reached (10 on wood) - order done`).

A 3.2b passou a ler o número como **quanto falta coletar** ("10 A MAIS"), independente do que já existe na
mochila/baús.

| Peça | Arquivo | O que fazia |
|---|---|---|
| Base da ordem | `src/Companions/Companion/CompanionSetup.cs` | `OrderCapBase(c, mode, item, stockNow)`: o estoque **do momento** em que a ordem olhou aquele item pela primeira vez, por companheiro/modo/item (dicionário estático de sessão). `ClearOrderCapBase` era chamado junto com `SetOrderCap`/`ClearOrderCap` |
| Teto efetivo | `src/Companions/Jobs/MHR_GatherRules.cs` | `OrderLimit(...)`: `capTotal = base + cap`; `AllowTarget`/`AllowLoose` usavam `min(limite da config, capTotal)` — mas a ordem **ganhava** do `WoodLimit`/`StoneLimit`/`OreLimit` (o `Name:Number` explícito, se menor, continuava ganhando) |
| Fim da ordem | `src/Companions/Jobs/HarvestController.cs` | inalterado: scan vazio + teto cumprido → `OrderDone` → volta a seguir |

- **Como a base é capturada:** na **primeira leitura daquele item durante a ordem** (dentro de `OrderLimit`), e
  não no instante em que a ordem é dada. Motivo: uma lista com `*` não tem itens para percorrer na hora, e a
  leitura é a mesma `Stock()` que o job já faz (com cache de 10s) — no máximo uma vez por item por ordem,
  **nunca por frame** e sem varredura própria do mundo. Na prática a diferença para "ler na hora da ordem" é
  de um scan.
- **Quando a base é limpa:** junto com o teto — fim da ordem (`CompleteOrderCap`), `para`/`segue`, outra ordem
  de trabalho ou troca de modo (`ApplyActionMode`). Uma nova ordem lê o estoque de novo, então repetir
  `colete 10` duas vezes soma 20 (10 + 10).
- **Depois de um reload:** o teto (`HC_OrderCap`/`HC_OrderCapMode`) continua no ZDO, a base **não** (é de
  sessão). A ordem recomeça a contagem do estoque do momento do reload — ou seja, ela coleta o número pedido
  outra vez. É o lado seguro de errar (nunca "para de imediato").
- **`Name:Number` continua ganhando se for menor** (`Wood:3` com `colete 10` para em 3) e o alvo apontado no
  radial (`SetDirectedTarget`) continua fora do teto.
- **Sem ordem nada muda:** `cap == 0` → caminho antigo (config pura), sem capturar base nenhuma.

### Fase 3.2c — o número é um ALVO TOTAL contado no INVENTÁRIO (correção final) (✅ — Unreleased)

Regra pedida pelo usuário, literal: *"peço para o companion cortar 10 madeiras e ele já tem 15 no inventário,
aí ele não executa a tarefa e diz que já temos o suficiente"*; *"se ele já tem 7 no inventário dele, ele executa
a tarefa até que tenha 10 ou mais no inventário"*.

Ou seja: **o número é um alvo total, contado APENAS na mochila do companheiro** — não conta baús nem pilhas —
**e vale apenas para o recurso pedido** (não para todos os itens da lista do modo).

| Peça | Arquivo | O que faz |
|---|---|---|
| Item da ordem | `src/Companions/AI/OrderItems.cs` (novo) | `Resolve(action, resource, mode)` traduz o recurso que o modelo devolveu (`wood`/`madeira`, `stone`/`pedra`, `iron`/`ferro`...) para o prefab do item (`Wood`, `Stone`, `IronScrap`): mapa PT-BR/EN + conferência com `ObjectDB.GetItemPrefab`, e, para o que o mapa não pega, o nome **localizado** dos itens da própria lista do modo |
| Contagem na mochila | `src/Companions/Jobs/MHR_GatherRules.cs` | `Bag(c, item)` soma o item no `Inventory` do `Humanoid` (todos os stacks). `Stock` (mochila + baús + pilhas) continua sendo o que a **config** usa |
| Alvo no ZDO | `src/Companions/Companion/CompanionSetup.cs` | `HC_OrderCap` + `HC_OrderCapMode` + **`HC_OrderCapItem`** (o item do alvo, "" quando não foi possível fixar um); `OrderCapItem(c, mode)` lê o item; `OrderSeen(c, inPackNow)` guarda o **maior** valor visto na mochila durante a ordem (sessão) e é limpo junto com o alvo |
| Ordem → alvo | `src/Companions/AI/CompanionOrders.cs` | Com `amount > 0` resolve o item e, **se a mochila já tem `amount`**, não inicia o trabalho: o modo de trabalho fica como estava e o companheiro avisa (`hc_ai_already_have`). Senão grava o alvo com o item |
| Alvo no job | `src/Companions/Jobs/MHR_GatherRules.cs` | `OrderedItem` → `AllowTarget`/`AllowLoose`: quando o alvo nomeia um item **e o alvo produz esse item**, a decisão é só dele (uma árvore não é derrubada só porque a resina ainda está abaixo do `WoodLimit`); a contagem é `Bag` e o alvo **ganha** do limite da config |
| Aviso | `src/Companions/Core/ModLocalization.cs`, `Translations/Portuguese_Brazilian.json` | `hc_ai_already_have` = "Já temos o suficiente." (PT-BR) — falado pelo companheiro e mostrado no HUD/chat |

- **Como a mochila é contada:** `Bag()` varre `Humanoid.GetInventory().GetAllItems()` (a lista interna do
  inventário, sem alocar) somando `m_stack` de todo item cujo `m_dropPrefab.name` bate com o item do alvo
  (pode haver mais de um stack). Uma vez por candidato por scan — nunca por frame.
- **Como o item da ordem é resolvido:** (1) mapa PT-BR/EN de apelidos (`madeira`→`Wood`, `pedra`→`Stone`,
  `ferro`→`IronScrap`, `resina`→`Resin`, `silex`→`Flint`...); (2) se o modelo não devolveu recurso nenhum, o
  **próprio action** resolve (`wood`→`Wood`, `stone`→`Stone`); (3) o nome localizado dos itens da lista do modo
  ("casca de ancião" → `ElderBark`); (4) tudo é conferido com `ObjectDB.GetItemPrefab` antes de usar.
- **Se o recurso não for reconhecido** (`minério`/`ore`/`metal`, que são cobre, ferro, prata e estanho ao mesmo
  tempo; ou qualquer nome desconhecido): o alvo **vale para o modo inteiro**, como na Fase 3.2 — cada item da
  lista é comparado com o número, contado na mochila. É o comportamento de reserva, e é logado assim
  (`stops at 10 of every item on the job's list`).
- **O que acontece quando o alvo é batido:** o candidato é recusado com `CapReject` e, como o scan não acha mais
  nada, `MHR_GatherRules.OrderDone` encerra a ordem e volta a seguir (fala "já temos madeira/pedra/minério de
  sobra"). A última árvore derrubada pode passar do número — ela cai inteira.
- **Depósito no meio da ordem (limitação conhecida, atenuada):** quando a mochila enche o companheiro leva a
  carga para um baú (`CompanionAI.PendingDeposit`) e a contagem da mochila cai — sem tratamento ele continuaria
  coletando. Para não começar a contagem do zero, `CompanionSetup.OrderSeen` guarda o **maior** valor visto na
  mochila **durante esta ordem** (um int por companheiro, em memória, zerado quando o alvo é escrito ou limpo).
  Na prática a ordem termina quando ele já **coletou** o número pedido, mesmo que parte já esteja no baú. O
  contraste é literal: o requisito fala de "ter N no inventário"; com depósito o número passa a ser "ter tido N
  no inventário". Fora isso nada muda (com o alvo ainda não atingido ele segue coletando normalmente).
- **Alvo que não produz o recurso pedido:** se a ordem nomeia um item e o alvo **não** o produz (ex.: pedir
  `ferro` e olhar uma rocha que só dá cobre), a decisão volta a ser a da config (`WoodLimit`/`StoneLimit`/
  `OreLimit`) — assim o companheiro nunca fica parado sem conseguir encerrar a ordem. É a mesma limitação da
  Fase 3.2 e está documentada de propósito.
- **Coleta normal, radial e directed continuam iguais:** sem ordem no ZDO, tudo é a config pura (`Stock`);
  alvo apontado no radial (`SetDirectedTarget`) nunca tem alvo de ordem (`IsDirectedHarvest`).

### Fase 3.6 — ordens com alvo: atacar, equipar e largar (✅ — validado em jogo)

O jogador aponta e digita: **`ataca o greydwarf`**, **`equipa o machado de bronze`**, **`larga a pedra`**
(e `joga fora 5 madeira`). São as **mesmas ações** do radial/tecla de comando — nada de novo dirige o NPC.

| Peça | Arquivo | O que faz |
|---|---|---|
| Contrato | `src/Companions/AI/LlmIntent.cs` | novo campo **`target`** (aceita `alvo`/`target_name`/`creature`/`item_name`) e as ações **`attack`**, **`equip`**, **`drop`**; `resource`/`amount` seguem iguais |
| Alvo (nome → coisa) | `src/Companions/AI/OrderTargets.cs` (novo) | acha a **criatura** (mira do jogador → a mais próxima no raio) e o **item** (no inventário do companheiro), casando nomes em PT-BR/EN, prefab e nome localizado |
| Intenção → ação | `src/Companions/AI/CompanionOrders.cs` | `attack` → `CompanionAI.DirectEngage(..., byOrder)`; `equip` → `Humanoid.EquipItem`; `drop` → `ItemDrop.DropItem` + `Inventory.RemoveItem` |
| Combate | `src/Companions/AI/CompanionCombatAI.cs` | `EngageByOrder`/`EngagePreyByOrder`: a criatura **nomeada numa ordem** é engajada mesmo na postura **Passiva** (a postura não muda) |
| Prompt/feedback | `src/Companions/AI/CompanionChatAI.cs` | system prompt PT-BR com `target` e exemplos; a resposta mostra a linha da própria ação (`hc_ai_attacking`, `hc_ai_equipped`, `hc_ai_dropped`...) |
| Config | `src/Companions/Core/ModConfig.cs` | **`[IA] RaioDeAlvo`** (padrão 40 m): até onde procurar a criatura citada; a que estiver **na mira** vale sempre |
| Textos | `ModLocalization.cs` + `Translations/Portuguese_Brazilian.json` | `hc_ai_attacking`, `hc_ai_no_target`, `hc_ai_no_aim`, `hc_ai_equipped`, `hc_ai_no_item`, `hc_ai_not_equipable`, `hc_ai_dropped`, `hc_ai_need_item` + os rótulos `hc_ai_order_attack/equip/drop` |

- **Como a criatura é resolvida:** (1) a que estiver **sob a mira** do jogador (raycast da câmera nas camadas
  `character*`, o mesmo caminho do comando por tecla), quando ela **casa** com o nome citado (ou quando a ordem
  não citou nome); (2) senão a **mais próxima que casa**, dentro de `[IA] RaioDeAlvo` (40 m por padrão — a mesma
  "coleira" que o combate usa para largar um alvo). Jogadores, companheiros e mortos nunca entram.
- **Como o item é resolvido:** só o **inventário do próprio companheiro** (nunca baús/pilhas), casando o nome dito
  contra o **prefab** (`AxeBronze`), o nome localizado (`Machado de bronze`) e as palavras de cada um
  (`AxeBronze` → axe + bronze), com **dicionário PT-BR/EN** (machado↔axe, picareta↔pickaxe, sucata↔scrap...),
  singular/plural e palavras vazias (`de`, `da`, `the`...) ignoradas. Empate: a primeira ocorrência no inventário.
- **Passiva + ordem de ataque:** a ordem é explícita e vale para **aquela** criatura; a postura **não é alterada**
  (o `CompanionCombatAI` só ignora a Passiva para o alvo da ordem, e volta a "não começa briga" assim que ele
  morre/some). Sem isso a ordem seria engolida em silêncio, porque a Passiva é literalmente "nunca começa uma briga".
- **`drop` sem número larga o STACK INTEIRO** ("larga a pedra" = livrar-se dela; é o mesmo que o mod faz com o lixo
  que ele descarta para abrir espaço — `MHR_GatherRules.TryMakeRoom`). Com número (`joga fora 5 madeira`), no
  máximo esse tanto, somando vários stacks. A pilha largada sai do **auto-pickup** (`m_autoPickup = false`) para
  ele não pegar a própria ordem de volta, itens equipados são desequipados antes e **itens de missão não caem**.
- **`equip`:** equipa com o `Humanoid.EquipItem` do jogo; item que **não** é equipável avisa (`hc_ai_not_equipable`).
  O auto-equip do mod pode depois trocar a arma de volta (é ele que escolhe a melhor arma para o trabalho/luta) —
  a ordem não mexe em nenhuma configuração.
- **`pega a picareta` = equipar:** o verbo "pegar" é lido como coleta quando o objeto é recurso (`pega madeira`) e
  como equipar quando é equipamento (`GearWords` em `LlmIntent`), então nada que já funcionava mudou.
- **Ordens antigas intactas:** `colete 20 de madeira`, `segue`, `para`, `vem cá`, posturas, `pega pedra`... —
  o `target` do JSON é ignorado por elas e o `resource` continua sendo o item do trabalho.
- **Multiplayer/reload:** nada novo é gravado no ZDO — a ordem é aplicada no instante em que chega (o alvo de
  ataque vive na memória do combate, como o ataque do radial).
- **Verificação:** as duas partes mais arriscadas (casamento de nomes e o parser) foram exercitadas contra o DLL
  compilado — 45 casos de nome (criaturas e itens, PT-BR e EN, e 5 que **não** podem casar) e 30 de JSON
  (ordens novas + as que já existiam), todos passando.

### Fase 3.6b — a ordem sem alvo: o modelo responde `"target": null` (✅ — validado em jogo)

**Relato do jogador:** "o comando pra dropar não funcionou". No log, com voz:

```
[IA] Order from voz: "Joga essas pedras que tu pegou no chao."
  modelo -> {"action":"drop","reply":"Largando as pedras, chefe.","target":null,"resource":null,"amount":null}
[IA] "drop" -> 0 companion(s)
[IA] Order from voz: "Joga 10 pedras no chao."
  modelo -> {"action":"drop","reply":"Jogando fora as pedras, chefe.","target":null,"resource":null,"amount":10}
[IA] "drop" -> 0 companion(s)
```

O modelo **acertou a ação** (`drop`) e até a quantidade (`10`), mas devolveu **`target` nulo** — e a Fase 3.6 só
sabia largar/equipar o que o **campo do modelo** nomeasse (`if (string.IsNullOrEmpty(intent.Target))` → "Não
entendi qual item."). O mesmo risco valia para `equip` e para `attack` (que nesse caso ainda caía na mira, o que
salvava `ataca aquele`, mas não `ataca o greydwarf`).

| Peça | Arquivo | O que faz |
|---|---|---|
| Palavras do jogador | `src/Companions/AI/OrderTargets.cs` | novo `Phrases(mensagem)`: as frases de nome do que o jogador disse, da mais específica para a mais curta; novo `FindCreatureByWords` (criatura a partir da mensagem); novo `MinPhraseScore` (900) |
| Alvo do item | `src/Companions/AI/CompanionOrders.cs` | novo `ResolveItemTarget`/`ItemMatchInPacks`: o `target` do modelo ganha **quando casa com algo que ele carrega**; quando não, a mensagem do jogador é lida e o alvo validado contra as mochilas |
| Assinatura | `src/Companions/AI/CompanionOrders.cs` + `CompanionChatAI.cs` | `Run(intent, userText)`: o texto do jogador (chat **ou** voz) passa a acompanhar a ordem; o `attack` lê a mensagem antes de cair na mira |
| Prompt | `src/Companions/AI/CompanionChatAI.cs` | `target` passa a ser **OBRIGATÓRIO** em `attack`/`equip`/`drop` (nunca `null`), com 2 exemplos novos de `drop` ("joga essas pedras que você pegou", "joga 10 pedras no chão") |

- **O que o fallback lê:** `Phrases` normaliza a mensagem (minúsculas, sem acento, `_` entre palavras) e **descarta
  o que nunca é nome**: os verbos da própria ordem (`joga`, `larga`, `equipa`, `pega`, `ataca` — nas formas que o
  jogador digita), pronomes e demonstrativos (`essas`, `aquele`, `que tu pegou`), enchimentos (`coisa`, `lixo`,
  `isso`), números escritos e **dígitos** (`10` é o `amount`), e lugares/direções (`chão`, `fora`, `mão`, `perto`,
  `pra`/`pro`/`ao`). O que sobra vira nomes de até 3 palavras, **do mais longo para o mais curto** — `machado de
  bronze` precisa ganhar de um `machado` solto, que casaria com qualquer machado. Exemplos reais (harness):
  `"Joga 10 pedras no chão."` → `pedras`; `"equipa o machado de bronze"` → `machado_bronze | machado | bronze`;
  `"ataca o javali perto da minha casa"` → `javali_casa | javali | casa`; `"joga o lixo fora"` → **nenhuma**.
- **Nada é inventado:** cada frase só é aceita se **casar de verdade** com um item de alguma mochila
  (`OrderTargets.FindItem`, o mesmo casador da 3.6: prefab, nome localizado, PT-BR/EN, singular/plural) ou com uma
  criatura no raio (`ClosestByName`). E a barra é **mais alta** para as palavras do jogador: `MinPhraseScore = 900`
  (casa palavra por palavra), contra os 450 do resto — sem isso, `"larga a pedra e a madeira"` viraria
  `pedramadeira` e casaria por **substring** com `Madeira`, e a ordem largaria o item errado em silêncio.
- **`target` do modelo continua ganhando** quando ele responde algo que o companheiro **tem** (`ItemMatchInPacks` ≥
  450, exatamente o critério da 3.6) — o fallback só entra quando o campo vem vazio ou quando o nome dele não casa
  com nada que o companheiro carregue. Se nem a mensagem resolver, o aviso antigo continua (`hc_ai_need_item` /
  `hc_ai_no_item`), e o log diz por quê (`the model named no item and nothing in the player's message answers one
  the companions carry`).
- **`attack` sem alvo:** a mensagem do jogador é lida **antes** da mira, e só quando o modelo não nomeou nada (ou
  nomeou algo que não está por perto). `"ataca aquele"` não nomeia nada (o demonstrativo é descartado) e continua
  indo para a **mira**, como na 3.6; `"ataca o greydwarf"` com `target: null` passa a engajar o greydwarf.
- **`resource` como reserva:** confirmado — o parser da 3.6 já move `resource`/`item` para `target` quando a ação
  precisa de alvo (`LlmIntent.Parse`, bloco `NeedsTarget`), então um modelo que responda
  `{"action":"drop","resource":"pedra"}` funciona desde antes; nada foi mudado aí.
- **O que NÃO mudou:** coleta/alvo no inventário (3.2c), `follow`/`stop`/`come`/posturas/jobs, radial, UI, STT,
  raios e chatter (3.3c), personalidade/memória/conversa (3.5) e o balão do jogador (3.3d) — `Run` só ganhou o
  texto do jogador como parâmetro extra, e o `Phrases`/`FindCreatureByWords` **só rodam quando o modelo não nomeia
  o alvo** (uma vez por ordem, nunca por frame; nenhum `Find*`/LINQ novo em `Update`).
- **Verificação:** as duas peças novas foram exercitadas contra o **próprio** `OrderTargets.cs` compilado num
  harness fora do jogo (stubs de Unity, `net8.0`), com 22 mensagens e uma mochila realista: as 6 receitas de teste
  casaram o item certo (pedra/madeira/machado de bronze/picareta/tocha/couro de cervo), as criaturas casaram
  (greydwarf/javali/troll) e os casos que **devem** ser recusados foram recusados (`joga o lixo fora`,
  `joga essas coisas no chão`, `ataca aquele`).
- **Receita de teste (voz ou chat):** `joga 10 pedras no chão` → deve largar **10** pedras (log: `Order "drop": the
  model named no item ... the player's own words named "pedras"`); `joga essas pedras` → larga o **stack inteiro**;
  `larga a madeira` → larga a madeira; `equipa o machado de bronze` → equipa; `ataca o greydwarf` → engaja;
  `joga o lixo fora` → continua "Não entendi qual item." (nada é inventado).

### Fase 3.5 — personalidade, memória e conversa livre (✅ — validado em jogo)

O companheiro deixa de ser só um tradutor de ordens: ele **tem um jeito de falar** (preset ou texto livre do
jogador), **lembra das últimas trocas** e, quando a mensagem **não é uma ordem**, **responde como conversa** —
falando no balão do mundo e mostrando a resposta no chat. Nada do que já funcionava mudou: uma ordem continua
executando a **mesma ação** do radial, com a **mesma** linha de feedback.

| Peça | Arquivo | O que faz |
|---|---|---|
| Personalidade | `src/Companions/AI/CompanionPersona.cs` (novo) | presets embutidos (`viking`, `mago`, `sarcastico`, `guia`) + `custom`; `Resolve()` decide o texto que vai ao prompt (texto livre **ganha** do preset; vazio = padrão neutro) e `Describe()`/`Ids()` alimentam o log |
| Memória | `src/Companions/AI/CompanionMemory.cs` (novo) | as últimas `[IA] Memoria` trocas **por companheiro**, injetadas no prompt; teto nas duas pontas (trocas por companheiro + nº de companheiros) e limpeza na morte/dismiss/desload |
| Contrato | `src/Companions/AI/LlmIntent.cs` | novo campo **`reply`** (aceita `resposta`/`fala`/`mensagem`/`message`/`texto`/`text`/`say`); sem JSON nenhum, uma frase do modelo passa a ser lida como `reply` |
| Orquestração | `src/Companions/AI/CompanionChatAI.cs` | prompt montado por mensagem (nome + personalidade + contrato + memória), escolha do companheiro que fala, conversa livre (balão + chat) e o registro da troca na memória |
| Limpeza | `src/Companions/Companion/CompanionSetup.cs` | `MHR_ResolveCompanionId`/`MHR_CachedCompanionId` (ID permanente do companheiro, lido uma vez) + `CompanionMemory.Clear` na morte (`OnCompanionDeath`) e no `OnDestroy`; `CompanionsPlugin.OnSceneLoaded` limpa tudo |
| Config | `src/Companions/Core/ModConfig.cs` | **`[IA] Personalidade`** (""), **`PresetPersonalidade`** (""), **`Memoria`** (6, 0–12) e **`Conversa`** (true) |
| Textos | `ModLocalization.cs` + `Translations/Portuguese_Brazilian.json` | `hc_ai_default_name`, `hc_ai_preset_default/custom/viking/mago/sarcastico/guia` |

- **Como a personalidade entra no prompt:** o prompt de cada mensagem começa com
  `Você é {nome}, o companheiro NPC do jogador, e ele é o seu dono.` + `Seu jeito de falar e de agir: {personalidade}`
  + `Fale sempre em português, no seu jeito, com uma ou duas frases...`, e só então vem o **contrato fixo** (ações,
  regras, exemplos — agora com `reply`) e, no fim, a **memória**. O contrato é idêntico em toda mensagem, então o
  começo do prompt continua cacheável pelo servidor local.
- **Presets oferecidos:** `viking` (leal e destemido), `mago` (velho e sarcástico), `sarcastico` (piadas secas) e
  `guia` (paciente e didático) — textos em PT-BR embutidos no código (o `.cfg` fica curto) —, mais `custom`, que
  usa o texto livre de `Personalidade`. Config vazia = companheiro padrão (leal e prático). Preset desconhecido:
  avisa no log e cai no padrão; se os dois estiverem preenchidos, o **texto livre ganha** (e o log diz).
- **Como a memória funciona:** um dicionário `id do companheiro → lista de turnos` (jogador/companheiro), no
  **máximo `2 × Memoria` mensagens** (6 trocas = 12 mensagens por padrão), cada mensagem cortada em 240 caracteres e
  sem quebras de linha; no máximo 8 conversas ao mesmo tempo (a mais antiga sai). O **formato** escolhido é **texto
  dentro do system prompt** (`Jogador: ...` / `Thorin: ...`), e não mensagens `user`/`assistant` extras: assim o
  contrato JSON continua sendo a última coisa que o modelo lê e ele nunca recebe o próprio JSON antigo de volta
  (modelos pequenos tendem a copiar/parafrasear o que veem no histórico).
- **Limpeza:** a memória é **só em memória** (nada é gravado no ZDO/save). É esquecida quando o companheiro
  **morre** (`OnCompanionDeath`), é **dispensado/despawnado** (`OnDestroy`) e quando o **mundo** é trocado
  (`OnSceneLoaded` → `ClearAll`).
- **Conversa livre × ordem:** com `Conversa = true`, `action: none` + `reply` → o companheiro **fala** o `reply`
  (balão via `CompanionTalk.Say(..., force: true)`) e o texto aparece no chat/HUD; sem `reply`, continua o
  "Não entendi a ordem." de antes. Com `Conversa = false`, o comportamento é **exatamente** o da Fase 3.1.
  Numa **ordem**, a linha do mod **ganha** e o `reply` do modelo **não** é mostrado (só logado): o companheiro não
  diz duas coisas sobre a mesma ordem, e a linha localizada é a única que sabe **nomear** a criatura/o item
  ("Atacando o troll.", "Larguei Pedra (5)."). O `reply` da ordem entra na memória, então o tom não se perde.
- **Quem fala:** o companheiro **mais próximo** que pode receber ordem (seu, `Command` ligado, dentro do
  `[IA] RecallRange`/`[Controls] RecallRange`). Diferente das ordens, a conversa **não** toma posse do companheiro
  (`MHR_Owner.TakeControl` fica só para ordens). Sem companheiro por perto a resposta ainda aparece no chat, sem balão.
- **Verificação:** o parser e a memória foram exercitados contra o DLL compilado — 12 casos de JSON (conversa com
  `reply`, aliases `resposta`/`fala`/`mensagem`, texto puro sem JSON, ordens antigas com `target`/`amount`) e a
  memória (teto de 12 mensagens, corte das mais antigas, bloco do prompt, `Clear`/`ClearAll`), todos passando.
- **Receita de teste:** `[IA] PresetPersonalidade = viking` (ou `Personalidade = é um mago sarcástico...`), entrar no
  mundo, digitar `bom dia` e depois `como você está?` — a segunda resposta deve lembrar da primeira; `o que achou da
  minha casa?` deve responder no tom; `colete 20 de madeira` deve continuar funcionando igual (Ok: coletar madeira
  (20)); `Conversa = false` deve voltar ao silêncio para conversa; `Memoria = 0` deve fazer ele esquecer a cada
  mensagem; matar/dispensar o companheiro deve zerar a memória (linha `[IA] Memory:` no `LogOutput.log`).

### Fase 3.3 — ordens por voz (push-to-talk + STT local) (✅ — validado em jogo)

O jogador **segura uma tecla e fala** (`colete 10 madeiras`, `segue`, `ataca o greydwarf`, `bom dia`); ao soltar, o
áudio é transcrito **localmente** (Whisper.net, dentro do jogo, sem HTTP) e o **texto entra exatamente no mesmo
caminho das ordens do chat** — ordem, conversa, personalidade, memória: tudo igual, porque é a **mesma função**.

| Peça | Arquivo | O que faz |
|---|---|---|
| STT reutilizável | `src/Companions/AI/WhisperStt.cs` (novo) | o spike virou serviço: **um** `WhisperFactory` carregado uma vez e **mantido carregado** (pre-warm em segundo plano), transcrição numa `Task` de fundo + `ConcurrentQueue` drenada no `Update`, watchdog, `NativeGate` (uma transcrição por vez, compartilhado com o spike) |
| Captura | `src/Companions/AI/VoiceInput.cs` (novo) | push-to-talk (`Microphone.Start` **16 kHz mono**, `loop: false`, teto `VozDuracaoMaxima`), trim de silêncio, WAV PCM 16-bit mono **em memória**, feedback PT-BR, guardas de UI/foco |
| Entrada única | `src/Companions/AI/CompanionChatAI.cs` | novo **`Submit(mensagem, origem, announceRefusal)`**: o chat (3.1) e a voz (3.3) entram por ele — cooldown, escolha do companheiro, prompt, `LlmClient`, resposta no `Update`. Mais `Announce()` e `HeardByVoice()` (o "Entendi: ..." + balão) |
| Ponte do microfone | `src/Companions/AI/VoiceInput.cs` (`Mic`) | os 8 membros de `UnityEngine.AudioModule` que o mod usa, por **reflexão** (uma vez só): ver a nota abaixo |
| Config | `src/Companions/Core/ModConfig.cs` | **`[IA] VozLigada`** (true), **`VozTecla`** (V), **`VozIdioma`** ("pt"), **`VozDuracaoMaxima`** (10 s), **`VozLimiarSilencio`** (0.02), **`VozDispositivo`** (0), **`VozMostrarTexto`** (true) |
| Textos | `ModLocalization.cs` + `Translations/Portuguese_Brazilian.json` | `hc_ai_voice_listening/heard/nothing/short/busy/timeout/error/no_mic/unavailable/ui_open/cancelled/disabled` |
| Ciclo de vida | `src/Companions/Core/CompanionsPlugin.cs` | `WhisperStt.Init`/`VoiceInput.Init` no `Awake`, `VoiceInput.Tick` no `Update`, `VoiceInput.Shutdown`/`WhisperStt.Shutdown` no `Shutdown` |

- **Formato do áudio:** `Microphone.Start(dispositivo, loop: false, duração, **16000**)` — **16 kHz mono**, o que o
  Whisper espera (o Thrall antigo gravava 8-bit a 22050 Hz e tinha de converter). O clipe é lido só até
  `Microphone.GetPosition` (o resto do buffer circular é silêncio), passa por um trim de silêncio nas pontas
  (janelas de 10 ms acima de `VozLimiarSilencio`, com 0,125 s de folga) e vira **WAV PCM 16-bit mono** montado à mão
  na memória (sem arquivo temporário). O `AudioClip` do microfone é **destruído** no fim (vazamento clássico).
- **A tecla V:** `DirectTargetKey`/`FarmZoneKey` usam `Z`, `RadialMenuKey` usa `E` (que também é o *Use* do jogo),
  `ConfigPanelKey` usa `F8`, o editor de zona usa `LeftAlt`, `F5` é o console, `F7` o reposicionamento de UI e, desde
  a **Fase 3.7**, `AiPanelKey` usa **`I`** (o painel da IA — tecla livre no jogo e no mod; ver a seção 3.7).
  **Correção (Fase 3.3b):** a 3.3 dizia que **V** não era usada pelo jogo — **era**: `V` é o *Toggle Auto-Pickup* do
  Valheim (ver a seção 3.3b abaixo), e por isso o toggle do auto-pickup foi bloqueado enquanto a voz estiver ligada.
- **Sem travar nada:** a transcrição é uma `Task` de fundo (nada de `.Result`), o resultado volta por fila drenada no
  `Update`, e o push-to-talk só faz trabalho enquanto a tecla está segurada. O modelo é carregado **uma vez**
  (`TryGetFactory`, sob lock) e **pre-carregado** em segundo plano quando o jogador entra no mundo, para a primeira
  ordem não ser mais lenta. **Nada de alocação por frame.**
- **Guardas:** sem microfone, sem modelo, jogo sem foco, janela aberta (inventário/mapa/menu/chat/console/painéis do
  mod) ou uma transcrição/pedido ainda em andamento — em todos os casos o mod **avisa no chat/HUD** em vez de gravar.
- **O texto entra no pipeline do chat:** `VoiceInput.HandleTranscript` → `CompanionChatAI.HeardByVoice(texto)` (mostra
  `Entendi: "..."` e, desde a 3.3d, põe o texto reconhecido no balão do **jogador**) → **`CompanionChatAI.Submit(texto,
  "voz", true)`**, a mesma
  função que o patch de `Chat.SendText` chama. Nenhuma lógica de ordem foi duplicada; o prefixo `[IA] Prefixo` **não**
  se aplica à voz (ele é um filtro do chat).
- **Por que `UnityEngine.AudioModule` por reflexão:** o mod compila em **net472** e referencia o pacote **Whisper.net**
  (assets `netstandard2.0`), o que fixa a fachada **netstandard 2.0**. Todo módulo do Unity 6 é construído contra
  **netstandard 2.1**, e um módulo que **não** está na closure do jogo (`AudioModule` não está — `CoreModule`,
  `AIModule`, `AnimationModule`... estão, via `assembly_valheim`/`Assembly-CSharp`) dá **CS1705** contra essa fachada
  mais antiga. Apontar o `netstandard` para a fachada 2.1 do jogo resolve o CS1705 e quebra do outro lado: com ela,
  `AudioClip.GetData(Span<float>, int)` resolve `Span<T>` no **mscorlib** (CS7069), que os *reference assemblies* do
  .NET Framework não têm. Referenciar nada e buscar os **8 membros** em runtime (`Mic`, uma vez só, nunca por frame)
  é o que mantém o build limpo sem arrastar uma segunda BCL para dentro do Mono do jogo.
- **O spike não carrega mais o modelo:** `WhisperSpike` passou a usar `WhisperStt.TryGetFactory` + `WhisperStt.NativeGate`
  (e os helpers de caminho/loader compartilhados), então **o modelo é carregado uma vez só**, seja o gatilho
  `modia_stt_teste` ou uma ordem por voz. O spike continua sendo diagnóstico.
- **Receita de teste:** entrar no mundo (o log deve mostrar `[STT] Push-to-talk: LIGADO | segure V ...` e
  `[STT] Modelo CARREGADO ... e MANTIDO carregado`), **segurar V**, falar `colete 10 madeiras`, soltar — devem
  aparecer `Ouvindo...`, `Entendi: "colete 10 madeiras"`, `Pensando...` e `Ok: coletar madeira (10)`, e o companheiro
  deve ir coletar madeira. Conferir no `LogOutput.log`: `Audio gravado: N s ... WAV PCM 16-bit mono 16000 Hz`,
  `Voz #1 (push-to-talk): ... KB de audio` e `TRANSCRICAO em N ms ... TEXTO: "..."`. Falar `bom dia` deve virar
  conversa; `ataca o greydwarf` deve engajar a criatura.

### Fase 3.3b — correções do teste da voz (tecla V, qualidade do STT, latência do LLM) (✅ — validado em jogo)

Três problemas do primeiro teste em jogo da 3.3, com a evidência do log do jogador:
`Voz #1: 1,1 s -> "Coleta deis mandem."` / `Voz #2: 0,7 s -> "O leta deles."` / `Voz #3: 1,5 s -> "coleta 10 madeira."`
(e a conversa levando `[IA] Conversation (10237 ms)` contra ~3 s das ordens).

**1. Conflito da tecla V com o auto-pickup do jogo.** No Valheim 1.0.17 (`assembly_utils` + `assembly_valheim`):

- `ZInput.Reset()` cria o botão com `AddButton("AutoPickup", KeyToPath(Key.V), rebindable: true)` → **V** é o default
  vanilla e o jogador pode reatribuir (o jogo salva como `kbmBinding_AutoPickup`);
- **`Player.Update` é o único lugar do jogo inteiro** que lê esse botão:
  `if (ZInput.GetButtonDown("AutoPickup") || flag7) { m_enableAutoPickup = !m_enableAutoPickup; Message(... "$hud_autopickup:" ...); }`;
- `ZInput.GetButtonDown(string)` (estático, em `assembly_utils`) só consulta o estado do botão.

**Solução (`src/Companions/Patches/AutoPickupKeyConflict.cs`, novo):** um **prefixo Harmony em
`ZInput.GetButtonDown(string)`** que responde `false` para `"AutoPickup"` — e só para ele — quando
`[IA] VozLigada = true` **e** o binding do jogo realmente aponta para `[IA] VozTecla`. A tecla é lida **do próprio
jogo** (`ZInput.instance.GetButtonDef("AutoPickup").GetActionPath()`, o caminho *efetivo*, então uma reatribuição
conta) e comparada com o nome Unity Input System da nossa tecla (tabela `InputKeyName`; tecla desconhecida = **não
bloqueia**). Escolhas:

| Por quê | Detalhe |
|---|---|
| Não mexe no input do jogo | o botão continua existindo, sendo pressionado e aparecendo na tela de controles; só **uma consulta** passa a responder "não" |
| Escopo mínimo | a guarda é **uma comparação de string** para todos os outros botões (o jogo consulta dezenas por frame) e `"AutoPickup"` tem **um** call site em todo o jogo |
| Condicional | nada acontece se o jogador reatribuir o auto-pickup para outra tecla, ou se `[IA] VozLigada = false` |
| Fail-open | sem `ZInput`/binding/tecla reconhecida ou com exceção → **não bloqueia** (o jogo fica como sempre foi) e o log diz o motivo |

O jogador é avisado **uma vez** (log + uma linha no chat/HUD, `hc_ai_voice_key_conflict`) de que V agora é o
push-to-talk e de que o auto-pickup pode ser reatribuído nas configurações do jogo. O push-to-talk **não** mudou
(segurar/soltar V, `VoiceInput.Key`).

**2. Qualidade da transcrição.** Três mudanças:

| Mudança | Antes | Agora | Por quê |
|---|---|---|---|
| Modelo | `whisper\ggml-base-q5_1.bin` (57 MB) | **`whisper\ggml-small-q5_1.bin`** (181 MB) | `base` erra muito em PT-BR em clipes curtos; `small` é bem melhor. O `base` **continua em disco**: dá para voltar pelo `[IA] SttModelo` |
| Prompt inicial | — | `[IA] VozPromptInicial` (default: `Ordens para o companheiro: colete madeira, pedra, minerio, ferro, segue, para, vem ca, ataca o greydwarf, equipa o machado de bronze, larga a pedra, descansa, bom dia.`) | `WhisperProcessorBuilder.WithPrompt(...)` = `initial_prompt` do whisper.cpp: enviesa o vocabulário das ordens. Confirmado no whisper.cpp v1.7.4 que ele **sobrevive ao `WithNoContext()`** (o `no_context` limpa o texto *acumulado* antes, e o prompt inicial é prependado depois) |
| Trim | limiar `0.02`, folga `0,125 s` | limiar **`0.01`**, folga **`0,2 s`** | `0,02` cortava o começo de palavras faladas baixinho (o ataque de "co-"/"ata-" ainda está abaixo do limiar) — é o que produz transcrições como `"O leta deles."`. A folga maior garante que o trim não coma a primeira sílaba |

**Custo medido do modelo (Whisper.net 1.9.1, os mesmos nativos do mod, CPU de 16 núcleos com 8 threads — a mesma
máquina do jogo; melhor de 3 passadas, `WithNoContext` + idioma fixo):**

| Áudio | `base-q5_1` | `small-q5_1` | Diferença |
|---|---|---|---|
| carga do modelo | 88 ms | 159 ms | +71 ms (uma vez, no pre-start em segundo plano) |
| clipe de **2,0 s** | **638 ms** | **2187 ms** | +1,5 s por ordem |
| áudio de 11,0 s | 710 ms | 2320 ms | +1,6 s |
| com prompt inicial | +3 a +5% | +3 a +5% | desprezível |

> O tempo **quase não depende do tamanho do clipe**: o whisper.cpp sempre preenche a janela de 30 s do encoder, então
> um pedido de 2 s custa praticamente o mesmo que um de 11 s. Ou seja, `small` custa **~1,5 s a mais por ordem por voz**
> nesta máquina (e um pouco mais com o jogo ocupando a CPU) — em troca de acertar clipes curtos em PT-BR. A transcrição
> roda em `Task` de fundo (o jogo não trava; o que o jogador sente é o `Entendi: "..."` demorando um pouco mais). Se
> preferir a resposta rápida, `[IA] SttModelo = whisper\ggml-base-q5_1.bin` volta ao modelo antigo sem reinstalar nada.

**3. Latência do LLM.** O mod agora **mede** o que importa, para a próxima resposta não ser um chute:
`[IA] Prompt: instrucoes N car. (~N tokens) + mensagem N car. = N car. (~N tokens); corpo JSON N car.` antes do
POST e `[IA] Tempo: N ms no total, resposta com N car. (~N tokens, ~N tok/s ...)` depois, com um **aviso** a partir de
5 s dizendo que o gargalo é o modelo local (não o mod). Além disso o default de **`[IA] Memoria` caiu de 6 para 4**
trocas (cada troca entra no prompt; 12 mensagens de até 240 caracteres ≈ 700 tokens de prefill) — é uma economia
modesta, porque o custo dominante é a **geração** da resposta, não o prefill.

> **Hipóteses para os 10 s da conversa (a medir no próximo teste):** (a) resposta longa demais — a conversa gera uma
> frase inteira, a ordem gera um JSON curto; (b) **carga/JIT do modelo** no LM Studio se aquela foi a primeira
> requisição depois de um tempo parado (o log antigo não distinguia); (c) prefill de um prompt maior (memória cheia +
> personalidade). O mod **não** troca o modelo do jogador: se a linha `[IA] Tempo:` mostrar poucos tok/s, a sugestão é
> um modelo menor/mais rápido (ou GPU) no servidor.

**Configs novas/alteradas em `[IA]`:** `VozPromptInicial` (novo), `SttModelo` (default `whisper\ggml-small-q5_1.bin`),
`VozLimiarSilencio` (default `0.01`), `Memoria` (default `4`), e os textos de `VozTecla`/`VozLigada` explicando o
auto-pickup. **Migração:** um `.cfg` antigo que ainda esteja **exatamente** nos defaults antigos (`ggml-base-q5_1.bin`,
`0.02`, `6`) é atualizado com uma linha no log (o modelo só troca se o arquivo novo existir no disco); qualquer outro
valor foi escolhido pelo jogador e **não** é tocado.

**Receita de teste:** entrar no mundo e conferir no log `[STT] Prompt inicial do Whisper ...`,
`[STT] Modelo CARREGADO ... ggml-small-q5_1.bin` e, na primeira vez que o jogo consultar o auto-pickup,
`[STT] CONFLITO DE TECLA: ...` (mais a linha no chat/HUD). Segurar **V** e falar: o **auto-pickup não deve mais
alternar** (nenhuma mensagem `Auto-Pickup: On/Off` no HUD) e `colete 10 madeiras` deve sair certo com mais frequência
(`TRANSCRICAO ... TEXTO: "colete 10 madeiras"`). Reatribuir o auto-pickup para outra tecla nas configurações do jogo
e reentrar: o log deve passar a dizer `[STT] Sem conflito de tecla: ...` e o auto-pickup volta a funcionar.

### Fase 3.3c — raio de coleta maior e chatter aleatório desligado (✅ — validado em jogo)

Dois pedidos do jogador, sem lógica nova: (1) o companheiro **procura e trabalha mais longe** quando você manda
coletar; (2) **sem comentários aleatórios** — as falas **contextuais** continuam. Nada de ordem, job, radial, UI, IA
ou STT foi tocado.

#### 1. Raio de coleta (defaults novos)

| Config | Antes | Agora | O que muda em jogo |
|---|---|---|---|
| `[Advanced] GatheringSearchRadius` | 30 m | **50 m** | o anel em que ele procura a próxima árvore, rocha ou planta (seguindo você, ou mandado a um alvo com a tecla de comando) |
| `[Gathering] FollowRange` | 10 m | **25 m** | madeira/pedra/minério **seguindo você**: o alvo pode estar a até 25 m de **você** |
| `[Gathering] ForageFollowRange` | 10 m | **20 m** | forrageio seguindo você (continua sendo "o que você passou perto", com mais alcance) |
| `[Gathering] Range` | 40 m | **60 m** | **Follow desligado**: a área de trabalho em volta de casa (ou de onde o job começou) |
| `[Advanced] GatheringDropRadius` | 8 m | **12 m** | até onde ele cata o que caiu de uma árvore derrubada ou rocha quebrada |

**Por que esses números:**

- **Enquanto segue você, quem limita é o `FollowRange`**, não o `GatheringSearchRadius`: `HarvestController.ScanCenter()`
  é o **companheiro** e o anel tem o `GatheringSearchRadius`, mas cada candidato ainda passa por
  `InWorkArea(...)` → `PlayerRange()` = `FollowRange` medido **a partir de você**. Ou seja, o alcance efetivo é o
  `FollowRange`. O `GatheringSearchRadius` só precisa ser **grande o bastante para não virar o gargalo**: o companheiro
  pode estar a 25 m de você e o alvo a 25 m de você **do outro lado**, então o pior caso é ~50 m do companheiro — daí
  **50 m** (era 30, que com 10 m de `FollowRange` sobrava).
- **`FollowRange` 25 m** (era 10) é o que o jogador sente: ele para de ficar colado em você e vai buscar a árvore
  mais próxima num raio bem maior. Não passa disso porque a **coleira** existe: com um alvo em andamento e Follow
  ligado, o companheiro **abandona o alvo e volta** se ficar a mais de `PlayerRange() + 15 m` de você
  (`HarvestController`, o "You moved on" da 1.4.120) — com 25 m isso dá **40 m** de folga, então o raio de 25 m é
  plenamente utilizável (com 10 m a folga era 25 m).
- **A outra coleira (`[Follow] MaxDistance`, 10 m) não atrapalha:** enquanto o job tem alvo, `CompanionAI` **pula** o
  `MHR_Follow` (`_harvest.IsActive`, 1.4.94) e quem dirige o movimento é o `HarvestController`. O `MHR_Follow` só volta
  a mandar quando o job fica **ocioso** (nada no anel) — e aí ele volta para perto de você, como sempre fez. Efeito
  prático do raio maior: ele anda até ~25 m, coleta, e **volta** para o seu lado quando não há mais nada naquele anel
  (o vaivém é mais longo do que era com 10 m — é o preço do alcance pedido).
- **Forage 20 m** (era 10): o forrageio é o único com raio próprio desde a 1.4.151, justamente para não varrer tudo em
  volta do jogador; 20 m dobra o alcance sem virar "limpa o mapa".
- **Range 60 m** (era 40) e **DropRadius 12 m** (era 8): o primeiro é a área de trabalho com Follow **desligado**
  (ninguém fica esperando você, então pode ser bem maior); o segundo é o que resolve o caso clássico da árvore que cai
  longe e deixa os troncos para trás (12 m em vez de 8).
- **`AcceptableValueRange`:** todos os novos defaults já cabem nos ranges existentes (50 ≤ 100, 25/20 ≤ 60, 60 ≤ 150,
  12 ≤ 20) — nenhum range precisou mudar. O teto de 100 m do `GatheringSearchRadius` e o de 60 m do `FollowRange`
  continuam sendo o limite de quem quiser ir além.

#### 2. Chatter aleatório desligado (`[Speech] Chatter`, novo, default **false**)

O chatter periódico vinha **só** do timer do `CompanionTalk.Update()` (`_talkTimer` → `ResetTimer()` →
`SayContextual()`), que a cada `MinInterval`–`MaxInterval` segundos escolhia uma fala pelo contexto (inimigo perto,
sobrepeso, fome, equipamento gasto, sem cama, o job em andamento) e, no fim, 30% `IdleLines` / 70% `FollowLines`.
Agora esse timer só roda com **`[Speech] Chatter = true`**:

| O que fala | Continua? | Por quê |
|---|---|---|
| `Say(text, force: true)` — confirmações de ordem ("Ok: coletar madeira (10)"), "já temos o suficiente", respostas da conversa da IA, "Entendi: ..." da voz | **Sim** | é a camada de IA / a resposta a uma ordem sua |
| `SayRandom(ActionLines)` na **mudança de modo** ("Deixa comigo!") | **Sim** | roda **antes** da guarda, no mesmo `Update()` |
| Falas de job/evento (`hc_speech_*`: peixe fisgado, plantando, reparo, "unload", tombstone, marco do smelt...) | **Sim** | são por evento, não pelo timer |
| Linhas de **ociosidade** (`[Idle] SpeechInterval`, 300 s) | **Sim** | têm configuração própria (0 = nunca) e são separadas |
| **Chatter do timer** (o "comentário aleatório") | **Não** (default) | é o que o jogador pediu para tirar |

- **Custo:** com `Chatter = false` o `Update()` **nem decrementa** o `_talkTimer` (um `if` e uma leitura de
  `ConfigEntry<bool>` por frame, o mesmo tipo de leitura que `CullDistance`/`SayCooldown` já fazem ali) — nada por
  frame, nada alocado, e o resto do `Update` (que já sai cedo quando o companheiro está longe, morto ou com painel
  aberto) fica igual.
- **Ligar de volta:** `[Speech] Chatter = true` (painel `F8` ou `.cfg`) e o comportamento antigo volta inteiro, com os
  intervalos de sempre (`MinInterval` 15 / `MaxInterval` 30).

#### 3. Migração do `.cfg`

Como os defaults mudaram, um `.cfg` que já existia continua com os valores antigos (o BepInEx mantém o que está no
arquivo). Mesma regra da 3.3b: **só** um valor que esteja **exatamente** no default antigo é atualizado, com uma linha
no log dizendo o que mudou e por quê; **qualquer** valor escolhido pelo jogador fica intacto.

```
[Config] Fase 3.3c: [Gathering] FollowRange estava no default antigo (10) - agora 25: madeira/pedra/minerio agora
sao procurados ate 25 m de voce, em vez de 10 m. Coloque 10 de volta se preferir o raio curto.
```

- Vale para os **cinco** raios: `30→50`, `10→25`, `10→20`, `40→60`, `8→12` (`Mathf.Approximately`, tolerância de
  float).
- A chave nova **`[Speech] Chatter`** não precisa de migração: o BepInEx a cria com o próprio default (**false**) e
  escreve no arquivo no primeiro save (ela aparece no `F8` e no `.cfg`).
- A migração roda **depois** do `MigrateAndClean` (1.4.149), então um `.cfg` ainda com os nomes antigos
  (`[Harvest] FollowRange`) também é coberto.
- Nada mais é mexido: nenhuma chave é apagada, nenhum valor customizado é trocado, e a migração não depende de nenhum
  outro ajuste do `Init`.

#### Receita de teste

1. **Build:** `dotnet build Companions.sln -c Release` → 0 erros, DLL copiada para
   `BepInEx\plugins\AI-Companions` (feito).
2. **Raio maior:** entrar no mundo, mandar `colete 20 de madeira` (chat ou voz, ou o radial) e andar — o companheiro
   deve **se afastar bem mais de você** (até ~25 m) para achar a próxima árvore, em vez de parar na de sempre, e
   voltar para o seu lado quando não houver mais nada por perto. Com Follow **desligado** (`[Gathering] Range`), a área
   de trabalho em volta de casa deve chegar aos 60 m. Conferir no log de diagnóstico do job
   (`Physics scan: N colliders in ...`) e, se quiser, comparar com os valores novos no `F8` (`Advanced →
   GatheringSearchRadius` 50, `Gathering → FollowRange` 25, `ForageFollowRange` 20, `Range` 60,
   `GatheringDropRadius` 12).
3. **Sem chatter:** ficar **parado perto do companheiro ~1–2 min** (ele tem de estar a menos de `CullDistance` 20 m e
   não pode haver painel aberto) — **nenhuma** fala solta deve aparecer. Dar uma ordem (`segue`, `colete madeira`) e
   conferir que a fala **contextual** continua: **"Deixa comigo!"** na troca de modo e a linha da ordem no chat/balão.
   Falar por voz (`bom dia`) e conferir que a resposta da IA continua aparecendo.
4. **Voltar ao antigo:** `[Speech] Chatter = true` e o chatter de 15–30 s volta.

### Fase 3.3d — a ordem de pedra para no alvo e o texto da voz vai para o balão do jogador (✅ — validado em jogo)

Dois problemas do teste em jogo da 3.3c: (1) `colete 10 pedras` **nunca parava**; (2) a transcrição da voz aparecia no
balão do **companheiro** em vez do **jogador**. Nada de ordem, job, radial, UI, STT ou dos raios/chatter da 3.3c foi
tocado.

#### 1. `colete 10 pedras` nunca parava (causa raiz confirmada)

Evidência do teste (`BepInEx\LogOutput.log`, 12:59–13:02): a ordem de **madeira** (`x10`) encerrava normalmente
(`[Gather] Order target reached (10 Wood in the pack) - order done, back to following`), a de **pedra** nunca — e o
companheiro subiu Pickaxes de 6 para 8 minerando `Rock_3(Clone)`/`Rock_4(Clone)` com `hit mine rock 0..4` no log do
jogo.

Lido direto dos assets do jogo (bundle `c4210710` do SoftRef, via UnityPy):

| Prefab | Componente | Drops |
|---|---|---|
| `Rock_3` | **`Destructible`**, `m_health = 1`, `m_spawnWhenDestroyed = Rock_3_frac` | **nenhuma tabela de drop** |
| `Rock_3_frac` | **`MineRock5`**, 5 áreas, `m_health = 30` | `m_dropItems` = **`Stone` 3–6** por área |
| `Rock_4` | `Destructible` (30 HP) + **`DropOnDestroyed`** | **`Stone` 3–6** |

Ou seja: **a pedra do `Rock_3` vem do `Rock_3_frac` que ele spawna ao quebrar** (um `MineRock5` que rende ~15–30 pedras
por rocha). Como o `MHR_GatherRules.DropsOf` só olhava os componentes do **próprio** alvo, `Rock_3` parecia **não
dropar nada** → o `AllowTarget` caía no `drops.Count == 0` → `"unknown drops: don't block it"` → **o teto da ordem não
valia para ele** (nem o `StoneLimit` da config). O companheiro então ia de rocha em rocha (`Rock_3` → frac → `Rock_3` →
…) sem nunca consultar o número pedido; quando a rocha quebrava, o `TryFollowCrackedRock` continuava no frac **sem
passar por um novo scan**, então também não havia conferência ali. O `Rock_4`, que tem `DropOnDestroyed`, era barrado
corretamente — por isso o inventário enchia (o log mostra `already has 10+ Stone in its pack`) e a ordem ainda assim
não encerrava.

**Correção, em três partes (todas com custo por alvo/por frame, nada de alocação por frame):**

1. **Na raiz — `DropsOf` segue o que o alvo spawna.** `MHR_GatherRules.AddDrops` (novo) além dos componentes do alvo
   (`TreeBase`/`TreeLog`/`MineRock`/`MineRock5`/`DropOnDestroyed`) entra em `Destructible.m_spawnWhenDestroyed` e
   `m_spawnWhenDamaged`, com **profundidade máxima 3** (cadeia de spawn, nunca laço) e o cache por alvo que já existia
   (`s_dropsCache`). Com isso o `Rock_3` passa a ser julgado pelas pedras do frac dele — o teto da ordem **e** os
   limites da config voltam a valer.
2. **Rede de segurança — `MHR_GatherRules.OrderMet` (novo).** Se existe **ordem ativa para o modo** e o **item pedido é
   conhecido** e o **inventário do companheiro já tem o número** (contado como na 3.2c: `OrderSeen(Bag(item))`, o teto
   do que ele já teve, para descarregar num baú não reiniciar a ordem), **TODOS** os alvos daquele modo são recusados —
   não só os que o classificador reconhece como produtores do item. Aplicado em `AllowTarget` **e** `AllowLoose`; cada
   recusa conta `CapReject`, então o caminho que já existia (`OrderDone` → `CompleteOrderCap`) encerra a ordem **uma
   vez** (linha `Order target reached (N Stone in the pack) - order done, back to following` + fala `hc_speech_plenty_*`)
   e o companheiro volta a seguir. A resposta fica **cacheada por frame e por modo** (`Per.OrderFull*`), então um scan
   paga uma contagem do inventário, não uma por candidato.
3. **Ele para na hora — `MHR_GatherRules.OrderDoneNow` (novo) chamado pelo `HarvestController.Update`.** O número só era
   conferido no *scan* (entre alvos), e uma rocha de pedra pode render uma rocha inteira antes disso. Agora, enquanto o
   companheiro está **indo (`Moving`) ou trabalhando (`Attacking`) um alvo**, o `Update` encerra a ordem assim que o
   inventário tem o número pedido. **Nunca** durante `CollectingDrops`: o que aquele alvo acabou de dropar é catado
   primeiro, para não deixar pedra no chão.

**O que NÃO muda:** sem ordem (nenhum teto) tudo é como antes; alvo apontado no **radial** (`IsDirectedHarvest`) ignora
o teto, como já ignorava; `LimitFor`/`Have`/`Stock` (limites da config) continuam iguais; ordens por chat/voz, jobs,
UI e STT intactos. **Efeito colateral assumido:** o alvo que ele já estava trabalhando ainda é terminado (é o
"a última árvore pode passar" da 3.2c) — mas agora a ordem **encerra**, em vez de continuar para sempre.

#### 2. O texto da voz vai para o balão do jogador

`CompanionChatAI.HeardByVoice(texto)` mostrava a transcrição no chat/HUD ("Entendi: …") e mandava o **companheiro mais
próximo** repetir o texto (`Speak(PickSpeaker(), texto)` → `CompanionTalk.Say` → balão do companheiro). A fala é do
**jogador**, então agora:

- `CompanionTalk.SayAsPlayer(GameObject, texto)` (novo, `internal static`) usa a mesma API do balão do companheiro —
  `Chat.instance.SetNpcText(talker, Vector3.up * 2f, SpeechCullDistance, SpeechDuration, "", texto, false)` — no
  `Player.m_localPlayer.gameObject`. Mesmo offset (`SpeechOffset` = 2 m), mesma `CullDistance` e mesma duração; sem
  cooldown (o jogador apertou a tecla, a linha tem de aparecer) e `try/catch` para nunca quebrar o jogo.
- `Player.m_localPlayer == null` → não faz nada (só o chat/HUD).
- **As respostas do companheiro continuam no balão dele**: conversa da IA, "Deixa comigo!", "já temos de sobra" seguem
  por `Speak(...)`/`CompanionTalk.Say`.

#### Receita de teste

1. **Build:** `dotnet build Companions.sln -c Release` → 0 erros, DLL copiada para
   `BepInEx\plugins\AI-Companions` (feito).
2. **Pedra para no alvo:** com o companheiro **sem pedra** no inventário, falar/digitar `pega 10 pedras`. Ele deve
   minerar e **parar** com ~10 no inventário (pode passar um pouco se a pedra que ele já estava quebrando terminar
   depois), com **uma** linha no log
   `[Gather] Order target reached (10 Stone in the pack) - order done, back to following` e a fala "já temos o
   suficiente" — e **voltar a seguir você** (não fica minerando a próxima rocha). Pedir de novo `pega 10 pedras` sem
   tirar a pedra do inventário deve responder que **já tem** o suficiente, sem iniciar a ordem.
3. **Madeira e minério (mesma regra):** `colete 10 madeiras` e `minere 10 ferro` devem encerrar do mesmo jeito (a de
   madeira já encerrava; a de minério usa o mesmo caminho).
4. **Sem ordem nada muda:** apagar o teto com `para`/`segue` e mandar `colete pedra` **sem número** — ele volta ao
   comportamento antigo (coleta até o `StoneLimit` da config). Um alvo apontado no **radial** também ignora qualquer
   teto.
5. **Voz sob a cabeça do jogador:** segurar `V`, falar `pega duas pedras` e soltar — o texto reconhecido deve aparecer
   no **balão do jogador** (e "Entendi: …" no chat/HUD). A resposta do companheiro ("Ok: coletar pedra (2)" /
   conversa) deve continuar aparecendo no balão **dele**.

### Fase 3.8 — o companheiro abre o próprio inventário por voz/chat (✅ — validado em jogo)

Pedido do jogador: *"seria possível fazer o companion abrir o inventário quando solicitado?"* — falar/digitar
`abre o teu inventário`, `mostra o inventário`, `mostra o que você carrega` e a **janela do inventário do
companheiro** abrir para o jogador. Nada de ordem, job, radial, UI, STT, balão do jogador ou dos raios/chatter foi
tocado: a ordem entra pela **mesma** caixa (`CompanionChatAI.Submit`, chat **e** voz) e termina na **mesma** chamada
que o **toque na tecla de interagir** já usa.

#### 1. Contrato do LLM: as ações `inventory` e `inventory_close`

- `CompanionChatAI.Contract` ganhou `inventory (abrir o inventário do companheiro para o jogador ver o que ele
  carrega)` na lista de ações, a regra (`inventory` quando o jogador pedir para **ver** o que ele carrega — *isso não
  é conversa*) e **2 exemplos**: `"abre o teu inventário"` e `"mostra o que você carrega"` →
  `{"action":"inventory",…}`. A parte fixa continua no começo do prompt (cacheável pelo servidor).
- `LlmIntent`: `inventory`/`inventory_close` são ações canônicas. O parser é tolerante em **duas** camadas, porque um
  modelo pequeno escreve a mesma ordem de muitas formas: (a) **aliases** exatos (`inventory`, `inventario`,
  `abrir_inventario`, `abre_o_teu_inventario`, `mostra_o_inventario`, `mochila`, `bolsa`, `bag`, `abre`, `abrir`,
  `open`, `mostra`, `mostrar`; e `fechar_inventario`, `fecha_inventario`, `fechar`, `fecha`, `close` para fechar);
  (b) **qualquer palavra da ação que carregue o nome da mochila** (`invent*`, `mochila`, `bolsa`, `bag`) — assim
  `abre_o_teu_inventario`, `open_bag` e `mostra_o_teu_inventario` caem na ação certa sem estarem na lista. A frase
  que só diz "carrega" só conta quando também pede para **ver** (`mostrar_o_que_voce_carrega`): `carregar_madeira`
  **não** é esta ordem (seria uma coleta) e continua caindo no que caía antes. `fech*`/`close*`/`escond*` na mesma
  ação viram `inventory_close`.

#### 2. `CompanionOrders.Inventory`: qual companheiro, e como a janela abre

- **Um só companheiro**, nunca todos (uma janela mostra uma mochila): o **companheiro a quem a mensagem foi
  dirigida** — `CompanionChatAI.PendingSpeaker`, o mesmo `PickSpeaker()` que já escolhe com quem o jogador está
  falando (o mais próximo dos seus, dentro do `[RecallRange]`, com os comandos ligados) — e, se ele já não existir
  quando a resposta chega, o **mais próximo que pode receber ordem** (a mesma regra, lida de novo). A escolha
  **não** toma posse dele por conta própria; quem toma é o painel do companheiro, no `Show` (como no toque).
- `companion.GetComponent<Container>()` → `CompanionInteract.RequestCompanionInventory(companion, container, player)`,
  que chama o **`CompanionInteract.OpenCompanionInventory`** (agora devolvendo `InventoryOpenResult`): o
  `InventoryGui.instance.Show(container)` + `CompanionInteractPanel.Instance?.Show(setup)` do toque. Ou seja: a
  janela, o campo de **nome**, os espaços de **comida** e o painel de **equipamento** são exatamente os que o
  jogador já vê ao interagir — nada de uma segunda janela.
- Feedback PT-BR (o companheiro também **fala** a linha do "abriu"): `hc_ai_inventory_open` = *"Abrindo o
  inventário."*; sem companheiro → `hc_ai_no_companion` (*"Nenhum companheiro seu está perto o bastante…"*); sem
  container → `hc_ai_no_container` (*"Não achei o inventário de {0}."*); sem `InventoryGui` (resposta chegando
  enquanto o mundo carrega) → `hc_ai_inventory_no_gui`; **nunca** quebra (`InventoryGui.instance == null` já era
  checado pelo próprio método).

#### 3. A posse do container (multiplayer): pede uma vez e abre sozinho

O caminho do toque, quando o container **não é nosso**, só pede a posse e avisa *"Requesting container access -
interact again in a moment"* — o jogador tem de interagir de novo. Numa ordem isso seria uma resposta ruim (o
jogador pediu **uma** vez, por voz):

- `OpenCompanionInventory` passou a **devolver** o que aconteceu (`Opened` / `Requesting` / `NoGui` / `NoContainer`)
  — os dois chamadores do toque ignoram o retorno, então nada mudou para eles.
- `RequestCompanionInventory` **guarda** o container quando a resposta é `Requesting` e o novo
  **`CompanionInteract.TickPendingInventoryOpen`** (chamado no `Update` do `CompanionInteract`, **uma vez por frame**
  — o primeiro que rodar no frame faz, guarda por `Time.frameCount`) abre a janela assim que a posse chega. O
  `ClaimOwnership` é chamado **uma única vez** (o retry só **lê** `IsOwner`), não a cada frame.
- Desiste depois de **5 s** com um aviso no HUD/chat (`hc_ai_inventory_timeout`), e larga o pedido em silêncio se o
  companheiro sumiu (morte/dispensa) ou se o jogador abriu **outra** janela nesse meio tempo — nunca rouba o foco.
- Custo: **zero** quando não há pedido pendente (uma comparação com `null` por frame).

#### 4. A ordem sem alvo do modelo (`none`): as palavras do jogador (rede de segurança)

Se o modelo responder `{"action":"none","reply":"…"}` para `abre o teu inventário` (a 3.5 trataria como conversa e
o jogador ficaria conversando com uma janela que nunca abre), o texto do jogador é lido por
`CompanionOrders.IsInventoryRequest` (novo) **antes** de virar conversa: ele exige uma **palavra da mochila**
(`inventário`, `mochila`, `bolsa`, `bag`, ou `carrega` em "mostra o que você carrega") **e** um **verbo de abrir ou
fechar** — por isso `o que tem no teu inventário?`, `quanto pesa a tua mochila?` e `abre a porta` continuam
**conversa/nenhuma ordem**, e `fecha o inventário` vira `inventory_close`. O `reply` do modelo é mantido só no log
(a linha localizada ganha, como em toda ordem).

#### 5. `fecha o inventário` (bônus)

`inventory_close` chama `InventoryGui.instance.Hide()` — exatamente o que o **ESC** faz; o painel do companheiro se
esconde sozinho no frame seguinte (o `Update` dele vigia o container do jogo). Sem janela aberta, avisa
(`hc_ai_inventory_none_open`) em vez de quebrar.

#### 6. Verificação fora do jogo (harness)

O **`LlmIntent.cs` real** e o **`IsInventoryRequest` real** (recortado verbatim do `CompanionOrders.cs`) foram
compilados num harness de console (net472, stubs de `CompanionsPlugin.Log`) e alimentados com 45 casos: 12 formas de
resposta do modelo para o inventário (todas → `inventory`/`inventory_close`), 8 respostas que já funcionavam
(`wood`/`follow`/`attack`/`equip`/`drop`/`stance_passive`/`none` — nenhuma mudou), `carregar_madeira`/
`carregar_pedra` (continuam **nenhuma ordem**, não viram inventário) e 19 mensagens de jogador para o fallback (10
que **devem** abrir/fechar e 9 que **devem** continuar conversa). Resultado: **ALL CHECKS PASSED**.

#### O que NÃO muda

Ordens por chat/voz, coleta com alvo no inventário (3.2/3.2c), atacar/equipar/largar (3.6/3.6b),
personalidade/memória/conversa (3.5), jobs, UI, radial, STT (3.3/3.3b), raios e chatter (3.3c), balão do jogador
(3.3d) e o caminho do toque na tecla de interagir — este último **idêntico** (o retorno novo do
`OpenCompanionInventory` é ignorado por ele).

#### Receita de teste

1. **Build:** `dotnet build Companions.sln -c Release` → 0 erros, DLL + `Translations\Portuguese_Brazilian.json`
   copiados para `BepInEx\plugins\AI-Companions` (feito).
2. **Chat:** digitar `abre o teu inventário` → o companheiro mais próximo responde *"Abrindo o inventário."* e a
   **janela do inventário dele** abre (mesmo painel do toque na tecla de interagir, com nome/comida/equipamento).
   No log: `[IA] Order "inventory": <nome>'s window is open`.
3. **Voz:** segurar `V`, falar `mostra o que você carrega` e soltar → mesmo resultado (o texto reconhecido aparece no
   balão do **jogador**, "Entendi: …" no chat/HUD, e a janela abre).
4. **Fechar (bônus):** com a janela aberta, `fecha o inventário` → fecha; com nada aberto → *"Nenhum inventário está
   aberto."*.
5. **Conversa preservada:** `o que tem no teu inventário?` deve ser **respondido** pelo companheiro (balão dele), sem
   abrir janela.
6. **Sem companheiro por perto:** afastar-se além do `[RecallRange]` (ou desligar os comandos dele no radial) e pedir
   → *"Nenhum companheiro seu está perto o bastante para receber essa ordem."*.

---

### Fase 3.4 — o companheiro FALA (TTS local, Kokoro-82M via sherpa-onnx) (✅ validado em jogo — `[TTS] Tocando ... na posicao de "HC_Companion(Clone)"`, `CACHE (0 ms)`, `RTF 0,83`; desligamento sem crash)

O companheiro **fala em voz alta** as respostas, **na posição dele** no mundo. A voz é sintetizada **dentro do
jogo** (sem HTTP, sem processo filho, nada sai da máquina) pelo **Kokoro-82M** através do **`sherpa-onnx`**
(`org.k2fsa.sherpa.onnx` 1.13.8, **Apache-2.0**, `netstandard2.0` → roda no net472; runtime nativo
`org.k2fsa.sherpa.onnx.runtime.win-x64`). É o espelho do `WhisperStt`: um modelo carregado uma vez, síntese fora
da main thread, fila thread-safe drenada no `Update`.

**Arquivo novo:** `src/Companions/AI/CompanionTts.cs` (+ `TtsRequest`/`TtsChunk`). Detalhes técnicos e receita de
teste em [`docs/re/tts-kokoro-sherpa-onnx.md`](re/tts-kokoro-sherpa-onnx.md).

| Peça | O que faz |
|---|---|
| `CompanionTts.TryGetTts` | carrega o `OfflineTts` **uma vez** (lazy, `lock`) e o mantém; `Prewarm()` faz isso em segundo plano quando você entra no mundo |
| Worker dedicado | **thread de fundo** própria (`ModIA-CompanionTts`), alimentada por `ConcurrentQueue` + `SemaphoreSlim`; o jogo nunca espera |
| Por frase | `SplitSentences` (`.` `!` `?` `…` e quebras de linha; um ponto entre dígitos é decimal; uma sequência de terminadores é **um** corte) — a 1ª frase toca enquanto a 2ª é gerada |
| Cache | `texto + voz + velocidade → amostras`, **só das linhas do mod** e com teto **por bytes** (32 MB, eviction do mais antigo; ver as correções da auditoria abaixo): as falas fixas saem **na hora** |
| Playback | **um** `AudioSource` **3D** na posição do companheiro (`spatialBlend=1`, `min/maxDistance`), volume de `[IA] TtsVolume`; uma fala por vez, em fila; o `AudioClip` é destruído ao terminar |
| `AudioClip`/`AudioSource` | acessados **por reflexão**, uma vez (o `UnityEngine.AudioModule` não é referenciável neste projeto — mesmo motivo do `VoiceInput.Mic`); o `transform.position` do `AudioSource` vai por `Component.transform` (CoreModule, referência direta) |
| Nativos | `sherpa-onnx-c-api.dll` + `onnxruntime.dll` pré-carregados por caminho absoluto antes do 1º uso (ver o doc) |
| Diagnóstico | `[TTS] Modelo CARREGADO em N ms`, `[TTS] Frase i/n: N ms, RTF 0,xx para N,NN s de audio`, `[TTS] Fala #n pronta: ...` e o comando de console `modia_tts_teste [texto]` |

**O que é falado** (regra: *a voz acompanha a linha que aparece no chat*):

| Situação | `modLine` | `reply` | Falado (padrão: as duas opções ligadas) |
|---|---|---|---|
| Conversa (`action=none`) | — | resposta do modelo | a **resposta do modelo** (`TtsFalarRespostas`) |
| Ordem (confirmada) | linha do mod (`"Ok: coletar madeira (10)."`) | confirmação do modelo | a **linha do mod** (`TtsFalarFalasDoMod`); com ela desligada, a resposta do modelo |
| Ordem recusada / "já temos" / sem companheiro / "não entendi" | linha do mod | (null) | a linha do mod, se `TtsFalarFalasDoMod` |
| Erro do LLM (`[IA] hc_ai_error`) | — | — | **nada** (é erro de infraestrutura, não fala do companheiro) |

Nada é falado duas vezes: o texto que não foi falado vai para o log (`[IA] Voz (Fase 3.4): falando "…"; o outro
texto ("…") fica so no log`). A decisão é a função pura `CompanionChatAI.PickSpokenText`, verificada fora do jogo
(14 casos) junto com o `SplitSentences` (17 casos) — **ALL CHECKS PASSED**.

**Prompt:** o contrato e o `BuildPrompt` passaram a exigir **UMA frase curta (até ~15 palavras)** no `reply` e a
dizer que ela é **falada em voz alta** (frase curta = fala mais rápida). O JSON do contrato não mudou.

**Config `[IA]` (PT-BR):** `TtsLigado` (true), `TtsVoz` (42; 42 = pf_dora feminina, 43 = pm_alex, 44 = pm_santa
masculinas), `TtsVelocidade` (1.0), `TtsThreads` (4 — RTF medido ~0,83, mais rápido que o tempo real),
`TtsVolume` (1.0), `TtsModelo` (`tts\kokoro\model.int8.onnx`; `voices.bin`/`tokens.txt`/`espeak-ng-data` vêm da
**mesma pasta**), `TtsIdioma` (`pt-br` — **nunca vazio**: este Kokoro é multi-lingue e a lib nativa recusa carregar
sem idioma; `pt` sozinho é português de Portugal), `TtsCache` (true), `TtsFalarRespostas` (true),
`TtsFalarFalasDoMod` (true).

**Não muda nada do resto:** ordens por chat/voz, STT, personalidade/memória/conversa, jobs, UI, radial, balão e
chatter seguem iguais. A integração são só 5 chamadas de `SpeakOutLoud(...)` no `CompanionChatAI.HandleResult`
(conversa, ordem com linha própria, ordem genérica, recusa/"já temos", "não entendi") + `Init`/`Tick`/`Shutdown`
no plugin.

### Fase 3.7 — painel da IA no jogo: personalidade, voz e push-to-talk (✅ validado em jogo — o bug do clique no painel **F8** foi corrigido de verdade na branch `fix/camadas-ui`, que subiu **este** painel junto; ver *Pendências conhecidas* e a seção *Correção — o clique engolido no painel F8* no fim do arquivo)

Tudo o que a camada de IA lê do `.cfg` passou a ser ajustável **em jogo**, num painel próprio do mod — sem editar o
arquivo à mão e sem reiniciar nada.

**Arquivo novo:** `src/Companions/UI/CompanionAiPanel.cs` (a única peça nova; o resto são ganchos de 3 linhas).

| Peça | O que faz |
|---|---|
| `CompanionAiPanel` | painel de madeira (`PanelBackground`) sobre um fundo escuro, com o mesmo formato dos outros painéis do mod: montado **uma vez** (no primeiro `Show`) e só mostrado/escondido depois |
| Tecla | **`[IA] PainelTecla`** (novo, `KeyCode`, padrão **`I`**) abre/fecha; **Esc** fecha (e, em ordem, primeiro sai da caixa de texto, depois fecha a lista aberta, depois o painel); o **X** fecha; um botão *Fechar* não existe porque o X faz isso |
| Input bloqueado | o painel entra na **mesma lista** dos outros painéis: prefixos de `Player.TakeInput` e `PlayerController.TakeInput` (`Patches/*_BlockForCompanionUI.cs`), `Hud.InRadial` (`InRadial_BlockForStarterPanel.cs`) e `VoiceInput.BlockedByUi` (para a tecla do push-to-talk não gravar com o painel aberto) |
| Não abre por cima | a tecla é recusada com o inventário, o mapa, o menu, o chat com foco, o console, a entrada de texto, a busca do comerciante ou o **painel F8** abertos (`InventoryGui.IsVisible()`, `Minimap.IsOpen()`, `Menu.IsVisible()`, `ConfigPanel.Instance.IsVisible`) — e o `ConfigPanel` recusa abrir enquanto o painel da IA está visível (correção D2 da auditoria) |
| Fecha sozinho | o jogador morreu (`Player.IsDead()`) ou saiu do mundo (`Player.m_localPlayer == null`) — checado no *refresh* de status (2×/s), nunca por frame |
| Sem companheiro perto | é config **global** do mod: o painel abre sem companheiro nenhum por perto; só o *Ouvir* e o nome no status usam o companheiro mais próximo que atenderia uma ordem (`CompanionChatAI.PickSpeaker`) |
| Status | uma linha no rodapé (2×/s): personalidade em uso • voz • tecla do push-to-talk — mais a dica da tecla do painel; o **título** nomeia o companheiro que atenderia uma ordem agora (o mesmo que o *Ouvir* usa), quando há um por perto |

**Controles e o que cada um grava** (todos escrevem no `ConfigEntry` correspondente; o BepInEx salva o `.cfg` na
hora — `ConfigFile.SaveOnConfigSet`, confirmado na fonte do BepInEx):

| Controle | Grava | Quando vale |
|---|---|---|
| Dropdown **Personalidade** (Padrão/Viking/Mago/Sarcástico/Guia/Texto livre) | `[IA] PresetPersonalidade` (id do `CompanionPersona`) | na **próxima mensagem** (o prompt é montado por mensagem) |
| Campo **Nas suas palavras** + **Aplicar** | `[IA] Personalidade` (até 600 caracteres, `CompanionPersona.MaxFreeTextLength`) **e** `[IA] PresetPersonalidade` (o do dropdown) | idem; o log/chat dizem o resultado efetivo (`CompanionPersona.Describe()`) |
| Dropdown **Voz** (42 pf_dora fem. / 43 pm_alex masc. / 44 pm_santa masc.) | `[IA] TtsVoz` | na **próxima fala** (o worker de síntese lê a config ao rodar; o cache é `texto+voz+velocidade`, então não há áudio velho) |
| **Ouvir** | nada (só fala) — usa `CompanionTts.Speak` com a frase `hc_ai_panel_tts_sample`, no companheiro mais próximo (se ele estiver dentro de `[Speech] CullDistance`; senão fala **em você**, para o teste nunca ficar mudo) | na hora; **desabilitado** se `TtsLigado = false` ou o modelo não estiver no disco (`CompanionTts.Ready`) |
| Slider **Volume** | `[IA] TtsVolume` (0..1) | no **próximo áudio** (`StartChunk` lê o volume ao tocar) |
| Slider **Velocidade** | `[IA] TtsVelocidade` (**0,5..2,0**, o mesmo intervalo do `.cfg` — `CompanionTts.MinSpeed/MaxSpeed`) | na próxima síntese |
| Tecla do **Push-to-talk** + **Alterar** | `[IA] VozTecla` via `KeyBindListener` (o **mesmo** componente do painel F8 — nada de lógica nova de captura) | na hora; o `AutoPickupKeyConflict.Reset()` é chamado para o conflito com o *Toggle Auto-Pickup* do jogo ser reavaliado com a tecla nova |
| Toggle **Ordens por voz** | `[IA] VozLigada` | na hora |
| Toggle **Companheiro fala** | `[IA] TtsLigado` | na próxima fala |
| Toggle **Falar respostas da IA** | `[IA] TtsFalarRespostas` | idem |
| Toggle **Falar falas do mod** | `[IA] TtsFalarFalasDoMod` | idem |
| Toggle **Conversa livre** | `[IA] Conversa` | na próxima mensagem |
| Slider **Memória** (0..12) | `[IA] Memoria` | na próxima mensagem |

- **Por que o dropdown é feito à mão:** um `TMP_Dropdown` exige um *template* inteiro (ScrollRect + máscara + item
  com Toggle) instanciado em runtime, e o `Dropdown` legado do Unity desenha com o `Text` legado (cuja fonte padrão o
  Valheim removeu). O do painel é **um botão-campo + uma lista de botões** — as mesmas primitivas que o mod já monta
  em todo lugar (`Image`/`Button`/`TextMeshProUGUI`), numa camada *overlay* que é o **último filho** do painel (logo,
  a lista aberta nunca é cortada por uma linha e nunca fica atrás dos controles) e que tem **`Canvas` próprio**
  (`overrideSorting`, `sortingOrder` 34) — ver a **correção da lista atrás dos controles**, no fim da Trilha D.
- **Nada de trabalho por frame:** o painel é montado uma vez; por frame só rodam a checagem da tecla (com
  `Input.GetKeyDown` **antes** das guardas de UI), o cursor e — 2×/s — o status. Nenhum `Find*`, nenhum LINQ.
- **A tecla `I`:** o mod usa `Z`, `E`, `F8`, `LeftAlt+Z`, `F7` e `V`; o jogo usa W/A/S/D, Tab, Esc, F, Q, C, R, X,
  G, H, T, M, B, Ctrl, Shift, Space e V. `I` está livre nos dois. Como é uma **letra**, a tecla só é lida quando o
  jogo **não** está com uma caixa de texto ativa: o chat (`Chat.HasFocus`), o console, a entrada de texto (placas) e o
  campo de busca do comerciante (`StoreGui`) continuam com a letra só para eles — a mesma guarda que o
  `CompanionInteract`/`VoiceInput` já usam.
- **Fontes:** o painel pega o `TMP_FontAsset` do próprio jogo (`InventoryGui` → `Hud` → `TMP_Settings`) e re-checa no
  `Show` (mesmo padrão do painel F8), porque a fonte pode não estar pronta quando o plugin carrega.
- **Textos (PT-BR):** 33 chaves novas em `ModLocalization` (padrões em inglês) **e** em
  `Translations/Portuguese_Brazilian.json` — `hc_ai_panel_*` (título com e sem o nome do companheiro, dica, rótulos,
  `Ligado`/`Desligado`, status, frase do *Ouvir*, avisos) e `hc_ai_voice_42/43/44/other`.
- **Não quebra nada:** nenhuma linha de ordem/STT/TTS/personalidade/memória/jobs/radial foi mudada. As mudanças fora
  do arquivo novo são: a config nova (`ModConfig.AiPanelKey`), dois acessores só-de-leitura
  (`CompanionPersona.Presets`, `CompanionTts.PortugueseVoiceCount/PorAt` — e `Speed`/`Volume` viraram `internal` para
  o log do *Ouvir*), o `AutoPickupKeyConflict.Reset()`, a entrada na lista de UI bloqueada (4 arquivos) e
  `Create`/`Destroy` no plugin.

### Fase 3.4/3.7 — correções da auditoria do TTS e do painel (✅ build 0 erros — validado em jogo o que é observável: o desligamento do TTS sem crash e o painel em uso; o resto é endurecimento interno)

Revisão dos commits `675a56f` (Fase 3.4, `CompanionTts.cs`) e `758bec0` (Fase 3.7, `CompanionAiPanel.cs`). Nada de
comportamento visível mudou **exceto** onde a correção era exatamente isso (o teto do cache, a faixa do slider de
velocidade e as guardas de abertura do painel).

| # | Item | Correção |
|---|---|---|
| **A1** | Corrida no `Shutdown` (nativo usado depois do `Dispose` → *AccessViolation*) | o `Shutdown` agora **espera a thread de síntese** (`_worker.Join(5000)`) antes de tocar no `_tts`; `Process`/`TryGetTts` **recusam** sintetizar/carregar quando `_stopping`; o `Dispose` passou para dentro do `lock (NativeGate)`; se o `Join` estourar, o modelo **fica carregado** (comportamento seguro de antes) |
| **A2** | `audio.NumSamples` com handle nulo (crash nativo) | o `audio.Handle != IntPtr.Zero` é checado **antes** de `NumSamples`/`Samples` (o binding devolve um objeto não-nulo mesmo com o `Generate` nativo devolvendo `NULL`); handle nulo = falha registrada no log + `continue`, e **não** se chama `Dispose` (o destroy nativo receberia `NULL`) — em vez disso `GC.SuppressFinalize` |
| **A3** | `Speak` depois do `Shutdown` | `if (_stopping) return;` no começo do `Speak` (a fala não fica na fila e o `_pending` não vaza) |
| **A4** | `Prewarm` tentava **uma vez só** | novo `MaybePrewarm()`: uma tentativa por vez, uma a cada **30 s**, enquanto o jogador estiver no mundo e o modelo **não** tiver carregado; o "modelo não encontrado" no `TryGetTts` deixou de ser definitivo (`_loadFailed`) — agora é re-checado, então copiar a pasta do modelo com o jogo aberto passa a funcionar sem reiniciar (o erro é logado **uma** vez) |
| **C2** | Cache de TTS sem teto de bytes | teto **por bytes** (`MaxCacheBytes` = **32 MB** de amostras `float`, ~29 s de áudio) **e** eviction do mais antigo (fila de inserção), em vez do `Clear()` ao bater 256 entradas; **e** só as **linhas do mod** entram no cache (`TtsRequest.Cacheable`; `CompanionChatAI.SpeakOutLoud` marca a resposta do modelo como não cacheável) — eram as respostas únicas que expulsavam as falas fixas |
| **B1** | pré-carga nativa não validava *qual* DLL entrou | `PreloadNative` compara `ModulePathOf(handle)` com o caminho esperado e, em divergência, **avisa** no log (o `DllImport` resolve por **nome-base**: outro mod/cópia na pasta do jogo pode capturar a chamada e a ABI não bater) |
| **D2** | o painel da IA abria por cima do inventário/menu | `PanelKeyPressed` passou a recusar `InventoryGui.IsVisible()`, `Menu.IsVisible()` e `Minimap.IsOpen()` (a mesma lista do `VoiceInput.BlockedByUi`) **e** o painel **F8** (`ConfigPanel.Instance.IsVisible`); o `ConfigPanel.Update` ganhou a checagem cruzada (não abre com o painel da IA visível) |
| **E1** | `hc_ai_voice_key_conflict` só existia no JSON PT-BR | par em **inglês** em `ModLocalization._englishDefaults` (o PT-BR continua no JSON); o `EnsureDefaultFile` regenera o `English.json` por contagem de chaves |
| **E2** | slider de velocidade 0,5–1,5 contra a config 0,5–2,0 | `CompanionTts.MinSpeed`/`MaxSpeed` (novos) são usados pelo **clamp** do `Speed` e pelo **slider** do painel: com `TtsVelocidade = 2,0` o handle vai até o fim e nada é rebaixado no primeiro arrasto |
| **D3** | cursor não era restaurado no `Hide()` | **não mexido, de propósito** — ver a nota abaixo |

- **D3 (por que não mexer):** quem devolve o cursor no Valheim 1.0 é o **próprio jogo**:
  `GameCamera.UpdateMouseCapture()` roda a cada frame e, com `m_mouseCapture` ligado (`= true` no `Start` fora do
  editor) e nenhuma janela aberta, faz `ZCursor.LockState = CursorLockMode.Locked; ZCursor.Hide();` — é justamente
  por isso que os painéis do mod (F8, IA, radial, casa, inicial) **reafirmam** `Cursor.lockState = None` a cada
  frame enquanto estão abertos. Ao fechar, o jogo trava o cursor de novo no frame seguinte. Se o jogador desligou a
  captura do mouse (`Ctrl+F1`), o mesmo método deixa o cursor **livre** de propósito — e um `Hide()` que forçasse
  `Locked` contrariaria essa escolha. (O `FarmZonePlacer` trava no `Hide` porque ele toma conta da câmera com o
  mouse, um caso diferente.) Fonte decompilada: `assembly_valheim.dll` → `GameCamera.UpdateMouseCapture`.
- **Defensivo:** `_voice.Index = -1` ganhou null-check e os helpers `SetCaption`/`HighlightItems` passaram a aceitar
  dropdown nulo (o resto do arquivo já era null-safe).
- **Não mexido (D4):** os sliders continuam gravando o `.cfg` a cada passo do arrasto — padrão pré-existente do
  `ConfigPanel`.
- **Arquivos:** `AI/CompanionTts.cs` (A1, A2, A3, A4, B1, C2, E2), `AI/CompanionChatAI.cs` (C2), `UI/CompanionAiPanel.cs`
  (D2, E2, defensivo), `UI/ConfigPanel.cs` (D2), `Core/ModLocalization.cs` (E1).

### Fase 3.7 — correção: a lista do dropdown do painel aparecia atrás dos controles (✅ validado em jogo)

**Relato (com screenshot):** ao abrir o dropdown de **Personalidade**, a lista (*Padrão*, *Viking leal*, *Mago
sarcástico*, …) saía **atrás** do campo do dropdown de **Voz** (`pf_dora (feminina)`), do campo *Nas suas palavras* e
do botão **Aplicar** — e a lista ainda "vazava" para cima das linhas seguintes sem cobri-las.

**Causa raiz (no `Build()`):** no uGUI a ordem de desenho é a **ordem dos irmãos** na hierarquia (o último filho
desenha na frente). O *overlay* das listas era criado como **último filho** de `_root`… e logo depois o
`BuildDropdowns()` criava os **campos** dos dois dropdowns **em `_root`** (`CreateDropdown` →
`CreateButton(_root.transform, …)`): os campos passavam a ser os últimos filhos do painel e desenhavam **acima** do
*overlay* — e as listas (filhas do *overlay*) ficavam atrás deles. O mesmo valia para o clique: os campos estavam "na
frente" no *raycast*.

**Correção (só `src/Companions/UI/CompanionAiPanel.cs`):**

| # | O que mudou | Por que resolve |
|---|---|---|
| 1 | `_overlay.SetAsLastSibling()` no fim do `Build()`, **depois** do `BuildDropdowns()` (com o porquê comentado) | o *overlay* volta a ser o último filho do painel, então as listas desenham (e recebem o clique) acima de tudo o que foi criado antes dele |
| 2 | o *overlay* ganhou **`Canvas` próprio** (`overrideSorting = true`, `sortingOrder` **34** — o painel é 33) + **`GraphicRaycaster`** | a ordem da lista deixa de depender da posição do *overlay* na hierarquia: é o que o **template de `Dropdown` do Unity** faz (`popupCanvas.overrideSorting = true; popupCanvas.sortingOrder = kHighSortingLayer`). O `GraphicRaycaster` é obrigatório — sem ele os itens da lista não receberiam clique nenhum. As constantes `PanelSortingOrder`/`OverlaySortingOrder` substituíram o `33` solto |
| 3 | *catcher* de clique fora: `Image` alfa 0 + `Button` (`transition = None`) cobrindo o painel inteiro, **primeiro filho do overlay**, ativo **só** com lista aberta, `onClick → CloseLists()` | clicar em qualquer lugar fora da lista fecha a lista (antes só fechava clicando de novo no campo). É o mesmo *blocker* do `Dropdown` do Unity (`Color.clear` + `Button` + `Hide`); por ser o **primeiro** filho, fica **atrás** das listas — o clique num item vai para o item, sem clique duplicado |
| 4 | `CloseLists()` também desativa o *catcher*; `Show()` passou a chamar `CloseLists()` (defensivo) | o *catcher* não sobrevive a um `Hide()`/`Show()` e nunca rouba o clique do painel com tudo fechado |

- **Ordem de clique conferida na fonte do uGUI:** `GraphicRaycaster.sortOrderPriority` devolve `canvas.sortingOrder`
  no modo *Screen Space Overlay* — o *canvas* do *overlay* (34) tem prioridade sobre o do painel (33), então o item da
  lista ganha o clique do campo que está atrás; dentro do mesmo *canvas* vale a ordem dos irmãos (o *catcher* é o
  primeiro). Nenhum item dispara duas vezes: o `EventSystem` entrega o evento só ao **primeiro** resultado.
- **Escondido junto com o painel:** o *overlay* continua filho de `_root`, então o `_root.SetActive(false)` do fim do
  `Build()` e do `Hide()` esconde as listas junto; o `Hide()` já chamava `CloseLists()` (estado aberto nunca sobrevive).
- **Não mudou:** TTS, STT, ordens, jobs, patches de input e o resto do painel. Sem alocação por frame, sem `Find*`/LINQ,
  null-safe.

### Fase 3.7 — correções do teste em jogo: as teclas do jogo digitando no campo, a caixa de texto e o rodapé (✅ validado em jogo)

**Relato (3 problemas):** (1) *"ao digitar 'm' na caixa de texto abre o mapa"* — com o painel aberto e o campo de texto
livre da personalidade em foco, **as teclas do jogo continuavam valendo** (`M` = mapa, `Tab` = inventário, `F5` =
console, `Enter` = chat, `C`, `R`, `G`, `X`, `B`, `Z`, `Q`, `F`, `E`, `V`, os dígitos do hotbar…); (2) a caixa de texto
"estava meio bugada" (o usuário acabou **colando** o texto); (3) *"o texto na parte de baixo onde diz as configs atuais
está muito ruim de visualizar"* — o rodapé em cinza escuro sobre madeira escura, fonte 11, com a última linha cortada
pela borda do painel.

**Causa raiz 1 — o `TakeInput` não cobre o que o jogo lê fora dele.** Os guardas que o mod já tinha
(`Player.TakeInput`/`PlayerController.TakeInput`, `Hud.InRadial`, `VoiceInput.BlockedByUi`) só valem para o que passa
por eles. O mapa é alternado em **`Minimap.Update`**, que lê `ZInput.GetButtonDown("Map")` e é barrado apenas por
*chat/console/TextInput/menu/inventário/busca do build* — **nunca** por `Player.TakeInput()`. Mesma coisa para o
inventário (`InventoryGui.Update` → `GetButtonDown("Inventory")`), o console (`Console.Update` → `"Console"`), o chat
(`Chat` → `"Chat"`) e os demais. E um `TMP_InputField` só come as teclas que ele mesmo trata (digitação, setas,
Ctrl+A/C/V/X, Esc, Enter) — todo o resto continua indo para o jogo.

**Causa raiz 2 — a caixa de texto nunca teve cursor nem seleção.** `TMP_InputField.OnEnable` é onde o TMP cria o
objeto `"Caret"` (um `TMP_SelectionCaret`) cujo `CanvasRenderer` ele guarda em `m_CachedInputRenderer` — é com ele que
**o cursor e a seleção são desenhados** (`UpdateGeometry` → `OnFillVBO` → `GenerateCaret`/`GenerateHighlight`), e o
TMP só faz isso **se o `textComponent` já estiver atribuído**. O painel montava o campo à mão com
`go.AddComponent<TMP_InputField>()` num GameObject **ativo** e atribuía `textComponent` **depois**: o `OnEnable` rodou
com o componente vazio, o objeto do cursor nunca foi criado e `UpdateGeometry` passou a sair cedo para sempre —
**sem cursor e sem destaque de seleção**. De quebra: o texto era criado com `overflowMode = Ellipsis` (o
`CreateText` do painel) e a caixa é de **uma linha**, então texto longo era **truncado com "…"** em vez de rolar; o
cursor era criado como **primeiro filho** (desenhava atrás do texto/placeholder); e `Enter` não aplicava nada.

**Causa raiz 3 — o rodapé.** Duas linhas de 11 px em `DimColor` (0,62) e (0,55) **direto sobre a madeira**, com a
segunda em `RowHint = 436` num painel de `PanelH = 470` — praticamente na borda inferior.

**Correção:**

| # | O que mudou | Por que resolve |
|---|---|---|
| 1 | **novo `Patches/TextInputKeyGuard.cs`**: `ModTextInputFocus` (registro dos campos de texto do mod + resposta *"algum em foco?"* **cacheada por frame**) e **prefixos Harmony** em `ZInput.GetButtonDown(string)`, `GetButton`, `GetButtonUp`, `GetKeyDown(KeyCode)`, `GetKey` e `GetKeyUp` que devolvem `false` enquanto um campo do mod está em foco | é o **único** ponto por onde passam as teclas do jogo (as que o `TakeInput` não cobre inclusive): o `m` do mapa, o `Tab`, o `F5`, o `Enter`, o hotbar… todos morrem aqui. Mesmo espírito do `AutoPickupKeyConflict` (prefixo condicional num leitor do `ZInput`, que **nunca** interfere quando não é o caso dele): sem campo em foco o caminho é **uma comparação de `int`** (o frame) e o método original roda igual |
| 2 | o guarda **deixa passar** os botões/teclas de **gamepad** (`"Joy*"`, `KeyCode >= JoystickButton0`) e os métodos de **mouse** (`GetMouseButton*` **não** são patchados) | o próprio painel lê `ZInput.GetButtonDown("JoyBack")`/`"JoyButtonB"` para fechar: bloquear o gamepad prenderia o jogador de controle dentro do painel. O mouse continua funcionando porque o clique na UI passa pelo `EventSystem`, não pelo `ZInput` |
| 3 | o que a UI precisa **não** é afetado: o `TMP_InputField` lê suas teclas pelo **`Input`/`Event` legado** (TMP `OnUpdateSelected` → `Event.PopEvent`) e os painéis do mod leem as deles com `Input.GetKeyDown` — nenhum dos dois passa pelo `ZInput` | digitar continua digitando o caractere, o Ctrl+A/C/V/X, o Enter, o Esc e o *key bind listener* do push-to-talk seguem iguais. O push-to-talk (`V`) já era bloqueado com o painel aberto pelo `VoiceInput.BlockedByUi`, então digitar `v` no campo **não** começa a gravar |
| 4 | `UI/CompanionAiPanel.CreateInputField` (**reescrito**): o GameObject nasce **inativo** (`SetActive(false)`) e só é ativado **depois** de `textComponent`/`textViewport`/`placeholder`/… estarem atribuídos; o `"Caret"` que o TMP cria no `OnEnable` é movido para **último filho** do `TextArea` | com o componente completo no primeiro `OnEnable` o TMP cria o objeto do cursor: **cursor piscando e seleção voltam a existir**. O cursor como último filho deixa de ser desenhado atrás do texto/placeholder |
| 5 | mais na caixa de texto: texto com `overflowMode = Overflow` + `textWrappingMode = PreserveWhitespaceNoWrap` (o `RectMask2D` do *viewport* é quem recorta e o TMP rola o texto na horizontal para seguir o cursor), `customCaretColor = true` + `caretColor` claro + `caretWidth = 2`, `selectionColor` azulada, `richText = false` (a personalidade é texto puro para o prompt), `onFocusSelectAll = false` (o clique posiciona o cursor — `OnPointerDown` → `TMP_TextUtilities.GetCursorIndexFromPosition`) e `onSubmit` → **`ApplyPersonality()`** | texto longo **rola** em vez de ser truncado com "…", o cursor aparece branco sobre a caixa escura, a seleção é visível, e **Enter aplica** como o botão *Aplicar* (o Esc do TMP é *cancel*, então não aplica) |
| 6 | rodapé (`BuildFooter`): **faixa escura** própria (`FooterBackColor`, o mesmo truque das linhas), resumo em **duas linhas** localizadas (`hc_ai_panel_status_personality` + `hc_ai_panel_status_voice`, 12,5 px, `FooterColor` quase branco, `TopLeft`), dica em 12 px (`FooterHintColor`), `overflowMode = Overflow`, e o painel cresceu **470 → 516** (`RowStatus` 412→418, `RowHint` 436→462) | o texto sai de cima da madeira (contraste de verdade), deixa de ser uma linha única gigante, e a última linha deixa de ser cortada pela borda inferior (o conteúdo termina ~18 px acima do fim do painel). O custo por frame **não** mudou: a faixa é criada **uma vez** e o *refresh* 2×/s só reescreve o texto |
| 7 | o **mesmo** guarda cobre o outro campo de texto do mod: `CompanionInteractPanel.BuildNameInput` ganhou a mesma correção de `OnEnable` (inativo → atribui → ativa) e `ModTextInputFocus.Register(_nameInput)` | o campo de nome do companheiro tinha o mesmo defeito (sem cursor/seleção) e, com o registro, digitar o nome também não dispara mais as teclas do jogo |

- **O que foi conferido na fonte (decompilado):** `Minimap.Update` lê `"Map"` sem `TakeInput` (por isso o relato);
  `ZInput.GetButtonDown/GetButton/GetButtonUp/GetKeyDown/GetKey/GetKeyUp` são estáticos e só consultam o estado do
  botão; `TMP_InputField.OnEnable` cria o `"Caret"` **apenas** com `m_TextComponent != null`; `UpdateGeometry` sai cedo
  sem `m_CachedInputRenderer`; `caretColor` devolve a cor do texto quando `customCaretColor` é falso; `Escape` no
  `KeyPressed` devolve `EditState.Finish` com `m_WasCanceled = true` (não chama `onSubmit`); Ctrl+A/C/V/X são tratados
  no mesmo `OnUpdateSelected`; `AdjustRectTransformRelativeToViewport` é quem rola o texto de uma linha.
- **Não mudou:** TTS, STT, ordens, jobs, comportamento do companheiro, o resto do painel e o do F8 (o `ConfigPanel`
  **não** tem campo de texto — só sliders, toggles e *key bind listeners* —, então não sofria do problema 1).
- **Arquivos:** `Patches/TextInputKeyGuard.cs` (novo), `UI/CompanionAiPanel.cs`, `UI/CompanionInteractPanel.cs`
  (registro + cursor), `Core/ModLocalization.cs` + `Translations/Portuguese_Brazilian.json` (as duas chaves novas;
  a antiga `hc_ai_panel_status_fmt` saiu).

### Fase 3.4 — o eco (parte 3): RESOLVIDO — era a zona de reverb global do jogo (✅ build 0 erros — branch `fix/eco-voz`; teste em jogo OK: **"limpou"**)

**Relato:** a voz do companheiro saía com **eco**, "como falar num lugar grande". O que já estava estabelecido e
descartou as hipóteses fáceis:

| Fato | O que descarta |
|---|---|
| a síntese está **limpa** (o WAV gerado fora do jogo é idêntico) | não é o Kokoro/sherpa-onnx |
| **só a voz do companheiro** ecoa (música e SFX do jogo são normais) | não é o áudio do sistema, os drivers, o fone nem um efeito global do Windows |
| o eco é **idêntico em qualquer lugar**, de perto ou de longe, e com as **3 vozes** (42/43/44) | não é distância, nem posição, nem o timbre |
| acontece com **uma única frase** | não é sobreposição da nossa fila/cache |

Conclusão (na época): um **processamento fixo da engine** aplicado à nossa `AudioSource`. A parte 3 entregou primeiro
**instrumentação** — interruptores de teste, um por vez, e um diagnóstico que fotografa a cena de áudio — porque quem
sabe **qual** processamento era o ouvido do jogador. O diagnóstico achou a causa na **primeira tentativa** e a
correção acabou sendo **uma linha**.

**O que ficou (resumo):** `[IA] TtsReverbZoneMix` (agora **opção de verdade**, padrão **0** = voz seca) e
`[IA] EcoDiag` (diagnóstico de áudio geral, só log). Os **4 interruptores de exploração** saíram.

**Arquivos:** `src/Companions/AI/CompanionTts.cs` (o `reverbZoneMix` da voz + diagnóstico), `src/Companions/Core/ModConfig.cs`
(as 2 chaves), `package/CONFIGURATION.md` (regerado). Nada mais foi tocado: `spatialBlend`, o gate de fim de fala
(`_currentEndsAt`/relógio), `StopSource`, cache, fila, síntese, prompt, painel, STT e ordens ficaram **intactos**.

#### 1. A causa raiz (o log do jogador com `EcoDiag = true`)

```
[ECO] ouvinte "Listner": AudioReverbFilter=nao, AudioLowPassFilter=nao
[ECO] Zonas de reverb: 1 carregada(s).
[ECO]   zona "Listner": preset=User, minDistance=1000, maxDistance=1000, decayTime=4, room=-1000, dentro: jogador=SIM, companheiro=SIM
```

O Valheim tem **UMA `AudioReverbZone` global**, no **próprio objeto do `AudioListener`**: raio de **1000 m**,
`decayTime` de **4 s** e `room = -1000` (−10 dB). Ou seja, **tudo dentro de 1 km** — o mundo inteiro — leva essa
cauda de 4 s. O ouvinte **não** tem filtro nenhum (nada de `AudioReverbFilter`/`AudioLowPassFilter`), então não era
efeito no ouvinte nem Doppler (as hipóteses do teste). As fontes do **jogo fogem** da zona: a música usa
`reverbZoneMix = 0`, o ambiente `reverbZoneMix = 0,1876583` (mixer `SFX`); as de SFX curtas (fogueira, abelha) usam
1, mas num som curto e repetitivo a cauda não se nota. A **nossa** `AudioSource` estava no **padrão do Unity
(`reverbZoneMix = 1`)** e sem mixer group: a fala pegava a cauda inteira — o "eco de caverna".

#### 2. A correção

`reverbZoneMix = 0` na nossa fonte: a voz sai **seca**, como a música do jogo. O jogador testou no painel F8 e
respondeu **"limpou"**.

| Item | O que mudou |
|---|---|
| `[IA] TtsReverbZoneMix` | deixou de ser interruptor de teste e virou **opção de verdade**: **padrão 0** (voz seca), faixa 0–1, para quem quiser um "espaço" sutil na voz (0,19 = o valor do ambiente do jogo) |
| Aplicação | `ApplyReverbZoneMix(source)` em `StartChunk`, escrita **só quando o valor muda** (a `AudioSource` é criada uma vez e fica na sessão): trocar no painel F8 vale **na próxima frase**, sem reiniciar, e a **primeira frase já aplica o 0** |
| `TtsBypassReverbZones`, `TtsBypassListenerEffects`, `TtsBypassEffects`, `TtsDoppler` | **removidos** (campos, `Bind`, propriedades e o código que os aplicava) — cumpriram o papel. O `.cfg` do jogo perde as 4 chaves sozinho |
| `[IA] EcoDiag` | **ficou** (bool, `false`), agora como **diagnóstico de áudio geral**: uma vez por sessão, na primeira frase, prefixo `[ECO]`, escreve o `AudioListener` (e seus filtros), **todas as `AudioReverbZone`** da cena (com os valores e se o jogador/o companheiro estão dentro) e a configuração de até 20 `AudioSource` do jogo. **Não muda o som** |
| Reflexão (`CompanionTts.Audio`) | o núcleo obrigatório (11 membros) continua igual e `reverbZoneMix` é o único membro **opcional** que a voz escreve; `dopplerLevel`, `bypass*`, `spread`, `spatialize` e `outputAudioMixerGroup` continuam resolvidos e são usados **só de leitura** pelo diagnóstico (`DescribeSource`) |

#### 3. O que ficou de fora (pendência conhecida)

A voz **não** passa por um **mixer group** do jogo (a música e o ambiente passam: `Music`/`SFX`). Roteá-la para o
`SFX` daria o volume/efeitos do grupo (é o que o jogo faz com o ambiente), mas fica para depois: não é necessário
para o eco, que já está resolvido com `reverbZoneMix = 0`.

#### 4. O diagnóstico (`EcoDiag = true`), uma vez por sessão, na primeira frase

Na main thread, no primeiro `StartChunk`, com `try/catch` e linhas curtas (prefixo `[ECO]`):

1. **`AudioListener`**: nome do GameObject e se ele tem `AudioReverbFilter` e/ou `AudioLowPassFilter`, com os valores
   principais (`reverbPreset`/`dryLevel`/`room`/`decayTime` e `cutoffFrequency`).
2. **Todas as `AudioReverbZone` da cena**: `reverbPreset`, `minDistance`, `maxDistance`, `decayTime`, `room` e **se o
   jogador e/ou o companheiro estão dentro** do raio (distância entre a zona e cada um).
3. **A receita do jogo**: uma amostra de até **20** `AudioSource` da cena (**a nossa fica de fora**), com
   `spatialBlend`, `reverbZoneMix`, `bypassReverbZones`, `bypassEffects`, `bypassListenerEffects`, `dopplerLevel`,
   `spatialize` e o **nome do mixer group** (`outputAudioMixerGroup`) — foi o que mostrou que o jogo usa `reverbZoneMix`
   baixo/zero e um grupo de mixer que a gente não usa.

As buscas usam `Resources.FindObjectsOfTypeAll(Type)` (não obsoleta, também vê objetos inativos) filtrado por
`gameObject.scene.IsValid()` (descarta prefabs/assets que só existem na memória). É caro — e é exatamente por isso que
roda **uma vez por sessão**, nunca por frame, e **nunca** com a flag desligada.

#### 5. Receita de teste (histórica — foi assim que achamos a causa)

1. No jogo, **F8** → aba **IA** → `EcoDiag` **ligado**; falar uma frase (`modia_tts_teste` no console F5 também
   serve) e mandar o log. A foto da cena respondeu sozinha (zona de reverb de 1000 m onde a voz toca; nenhum filtro
   no ouvinte; as fontes do jogo com `reverbZoneMix` 0 e 0,19).
2. `TtsBypassListenerEffects` **ligado** → ouvir uma frase (não mudou: não havia efeito no ouvinte).
3. `TtsReverbZoneMix` = **0** → ouvir. **"Limpou."** Era a zona de reverb. (O `TtsDoppler` e o `TtsBypassEffects`
   nunca chegaram a ser necessários — por isso saíram.)

Os interruptores ficaram **no padrão** ao terminar o teste, e a correção virou o padrão.



### Fase 3.1 — saída estruturada (JSON schema): o que permite modelos pequenos (✅ validado em jogo com o `qwen2.5-3b-instruct` — `[IA] Saida estruturada ligada ([IA] SaidaEstruturada): ... corpo JSON de 6955 car.` — branch `feat/json-schema`; as correções que vieram do teste estão nas seções 6, 7 e 8)

O pedido ao modelo passou a levar **`response_format`**, pedindo ao servidor que **force** a forma da resposta: um
objeto JSON com as cinco chaves do contrato e a lista fechada de ações. É a única mudança desta fase — o prompt
(`Contract`), o parser (`LlmIntent`), a personalidade/memória, o TTS, o painel, o STT e as ordens ficaram **intactos**.

**Por que:** o parser do mod sempre foi tolerante, mas a classe de erro "o modelo respondeu texto solto" não tinha
como ser evitada por prompt. Com o formato forçado pelo servidor, ela deixa de existir — e isso é o que **destrava
modelos pequenos**, que são os que respondem rápido numa CPU.

| Modelo | Prompt atual (~1766 tokens) | Com JSON schema |
|---|---|---|
| `qwen2.5-3b-instruct` | **0/6** respostas em JSON (repetia frases do prompt) | **6/6**, inclusive `target` em `attack`/`equip`/`drop` |
| `google/gemma-4-e4b` (o atual) | 6/6 | 6/6 (igual) |

(Números medidos pelo jogador, com o prompt do mod; o teste em jogo com o 3B é dele.)

#### 1. O corpo do pedido

O `LlmClient` monta o `response_format` a partir de **uma lista única de ações** —
`CompanionChatAI.ValidActions` (novo, ao lado do `Contract`, com comentário dizendo que **tem** que acompanhar o
`Contract`: 29 ações, de `follow` a `none`) — então o schema não pode divergir do que o mod entende. O JSON vai no
fim do corpo, depois de `messages` (nada mais mudou na ordem dos campos):

```json
{"model":"...","temperature":0.1,"stream":false,"messages":[{"role":"system","content":"..."},{"role":"user","content":"..."}],
 "response_format":{"type":"json_schema","json_schema":{"name":"companion_intent","strict":true,"schema":{
   "type":"object",
   "properties":{
     "action":{"type":"string","enum":["follow","wood", ... ,"none"]},
     "reply":{"type":"string"},
     "target":{"type":["string","null"]},
     "resource":{"type":["string","null"]},
     "amount":{"type":["integer","null"]}},
   "required":["action","reply","target","resource","amount"],
   "additionalProperties":false}}}}
```

O schema é montado **uma vez** (`JsonSchemaFormat`, cacheado) e os três corpos da cadeia de fallback são montados
**na main thread**, junto com os outros valores de config (a `Task` de fundo só envia). O log mostra o corpo real:
`[IA] Prompt: ... corpo JSON N car.` (com o schema, ~+680 car. sobre o corpo antigo).

#### 2. A cadeia de fallback (1 tentativa cada)

Nem todo servidor aceita `json_schema`, então o mod **nunca quebra** por causa disso:

| # | `response_format` | Quando é usada |
|---|---|---|
| 1 | `{"type":"json_schema", ...}` | sempre (com `[IA] SaidaEstruturada` ligado) |
| 2 | `{"type":"json_object"}` | só se a 1ª foi **recusada pelo formato** |
| 3 | *sem* `response_format` (comportamento antigo) | só se a 2ª também foi recusada pelo formato |

- **O que dispara uma nova tentativa:** **HTTP 400** ou o erro **citando `response_format`/`json_schema`**
  (`RejectedFormat`). **Nada mais** — timeout, socket morto, 500 e erro de rede **não** são repetidos: repetir só
  faria o jogador esperar duas vezes pelo mesmo erro. O mesmo corpo nunca é enviado duas vezes.
- **Log:** na 1ª mensagem, uma linha dizendo que a saída estruturada está ligada (nome do schema, nº de ações, tamanho
  do corpo); a cada queda, uma linha com o motivo (`... recusada pelo servidor: HTTP 400 ...`); ao chegar no formato
  antigo, **um único aviso** (`WarnNoStructuredOnce`) de que o servidor não suporta saída estruturada e que o mod segue
  sem ela; se uma tentativa posterior **funcionar**, uma linha dizendo com qual formato a resposta veio.
- **Timeout:** cada tentativa tem o seu próprio `Timeout`/`ReadWriteTimeout` = `[IA] TimeoutSegundos` (não foi
  dividido). Motivo: o caminho **normal** é a tentativa 1 e ela tem que continuar com o orçamento inteiro — um modelo
  local lento é o caso comum, e cortar o tempo dele para reservar tempo a fallbacks que quase nunca acontecem pioraria
  o caso real. Na prática a cadeia não custa quase nada, porque um 400 volta em milissegundos (o servidor recusa antes
  de gerar). O **watchdog** do slot "em voo" (`Tick`) foi estendido para cobrir as 3 tentativas
  (`TimeoutSegundos × 3 + 10 s` com a saída estruturada ligada, igual a antes quando desligada), senão ele liberaria o
  slot enquanto um fallback ainda estivesse rodando e a próxima mensagem começaria por cima.
- **Desligado** (`[IA] SaidaEstruturada = false`): uma única tentativa, exatamente o pedido de hoje.

#### 3. Config e arquivos

| Item | Onde |
|---|---|
| `[IA] SaidaEstruturada` (bool, **true**) | `src/Companions/Core/ModConfig.cs` — aparece sozinha no painel F8, aba IA |
| Corpo + cadeia + schema | `src/Companions/AI/LlmClient.cs` |
| `ValidActions` (fonte única do `enum`) | `src/Companions/AI/CompanionChatAI.cs` |
| Doc da config | `package/CONFIGURATION.md` (regerado: 263 opções) |

#### 4. Receita de teste

1. F8 → aba **IA**: `SaidaEstruturada` **ligado** (padrão). Na primeira mensagem, o log traz a linha da saída
   estruturada ligada.
2. No log, conferir que o corpo enviado tem `response_format` (`[IA] Prompt: ... corpo JSON N car.`, maior que o
   normal) e que a resposta é um JSON com as 5 chaves.
3. Trocar o modelo para um **3B** (`[IA] Modelo`) e repetir as mesmas ordens do teste da 3.6
   (`colete 20 de madeira`, `ataca o gregdwarf`, `equipa o machado de bronze`, `larga a pedra`) e as de conversa.
4. Para conferir o fallback: apontar para um servidor que recuse `response_format` (ou `SaidaEstruturada` desligado) e
   ver as linhas de queda no log sem nenhum erro no jogo.

#### 5. O que foi verificado (e o que não foi)

O **comportamento em jogo** foi **validado pelo jogador**: com a saída estruturada ligada o log traz
`[IA] Saida estruturada ligada ([IA] SaidaEstruturada): ... corpo JSON de 6955 car.` e as ordens passaram a funcionar
com o **`qwen2.5-3b-instruct`** (o modelo pequeno que respondia texto solto antes), a ~340–400 ms por ordem — o que
também **confirma na prática** a lista do `enum` contra o `Contract`. Fora do jogo (agente): o build e o JSON do corpo
(o corpo gerado é JSON válido, com 29 ações, `strict`, `required` e `additionalProperties:false`). Os **números
0/6 x 6/6** são medição do jogador, não do agente. Os **fallbacks** foram revisados no código, mas **não** foram
exercitados contra um servidor que realmente recuse o schema.

#### 6. O teste em jogo (modelo pequeno): `action` e `resource` inconsistentes — **o recurso manda**

O teste em jogo com o **`qwen2.5-3b-instruct`** (o caso que a saída estruturada destravou) achou um erro nosso. O
jogador pediu `colete 10 pedras` e o companheiro foi **coletar madeira**. O log do teste:

| Frase do jogador | O que o modelo devolveu | O que o mod fez |
|---|---|---|
| `colete 10 madeiras` | `{"action":"wood","resource":"wood","amount":10}` | madeira ✅ |
| `colete 10 pedras` | `{"action":"stone","resource":"pedra","amount":10}` | pedra ✅ |
| `colete 5 pedras` | `{"action":"wood","resource":"stone","amount":5}` | **madeira ❌** (`Order target: mode 1 stops at 5 Stone in the pack` + `Order "wood" (x5)`) |
| `colete 5 pedras` | `{"action":"wood","resource":"wood","amount":5}` | madeira ❌ (confusão do modelo) |
| `colete 10 pedras` | `{"action":"wood","resource":"wood","amount":10}` | madeira ❌ (confusão do modelo) |

São **dois problemas**: (1) **nosso** — com os dois campos inconsistentes o mod obedecia ao `action` **genérico** e
**ignorava o `resource`**, que é o campo específico e o que já tinha sido resolvido para o item que o trabalho conta
(era ele que aparecia no log do teto, "stops at 5 **Stone**", enquanto o modo aplicado era o de madeira); (2) **do
modelo pequeno** — em `colete N <coisa>` ele copia o `action:"wood"` do exemplo `"colete 20 de madeira"` do contrato
(*few-shot overfitting*, típico de 3B) — tratado na seção 7.

**Correção (defensiva):** `OrderItems.GatherAction(action, resource)` (novo) responde **qual ação de coleta o
`resource` indica**, ou `null` quando não há nada a corrigir. `CompanionOrders.Run` a chama **uma vez por mensagem**,
antes de montar o rótulo/log da ordem, e — quando ela devolve algo — troca o `action` e escreve **uma linha** no log:

```
[IA] O modelo devolveu action="wood" com resource="stone" - usei "stone" (o recurso manda).
```

- **Só as ordens de coleta** (`wood`, `stone`, `ore`, `forage`) são olhadas: `attack`/`equip`/`drop`/`inventory` e
  todas as outras passam intactas. Sem `resource` (nulo/vazio) nada muda — o comportamento de reserva da 3.2 continua.
- **Nada de tabela nova:** o nome do recurso passa pelo **mesmo** mapa de apelidos que o `OrderItems.Resolve` já usa
  (`pedra`/`stone` → `Stone`; `ferro`/`iron` → `IronScrap`) e o item é procurado entre as **entradas nomeadas das
  quatro listas que os próprios trabalhos leem** (`[Gathering] GatherWood`/`GatherStone`/`GatherOre` e
  `[Gathering] ForageItems`) — por nome de prefab **ou** pelo nome do jogo (`ItemLocal`), do mesmo jeito que as listas
  casam nomes. O mapeamento item → trabalho é a configuração do jogador, então não há uma segunda tabela para manter
  em sincronia. Uma lista que diz `*` **não nomeia item nenhum** e não decide nada.
- **Conservador:** se o recurso já é um item **da lista do próprio trabalho** da ação (par coerente), não se mexe —
  então uma lista personalizada nunca é sobreposta. `ore` continua sem item próprio ("minério" = cobre, ferro, prata e
  estanho), como na 3.2.
- **O que fica como está:** o teto da ordem (3.2c) e a contagem na mochila, o `OrderItems.Resolve`, o alvo do menu
  radial e o fallback da 3.6b (que lê as palavras do jogador). Com a ação corrigida, o `mode` aplicado e o item do
  teto passam a ser **o mesmo trabalho** (antes: modo de madeira + teto contando pedra).
- **Verificado fora do jogo** com um harness que compila os arquivos **reais** (`OrderItems`, `LlmIntent`, `MHR_List`;
  `CompanionOrders.JobMode` e as quatro listas entram como stubs): **60 casos, ALL CHECKS PASSED** — inclusive o caso
  do log (`action="wood"` + `resource="stone"` → `stone`), o espelho, os pares coerentes, recurso nulo/desconhecido,
  todas as ações que não são de coleta e as listas `*`/personalizadas/nomeadas em PT-BR. **E confirmado em jogo**
  (teste do jogador com o `qwen2.5-3b-instruct`): `colete 10 pedras` → o modelo devolveu
  `{"action":"stone","reply":"Vou buscar pedra.","target":null,"resource":"stone","amount":10}`, o mod aplicou
  `Order "stone" (x10)` com `Order target: mode 2 stops at 10 Stone in the pack` e respondeu em **378 ms**; na
  tentativa seguinte, com a mochila já com 10 de pedra, a ordem **nem começou**
  (`Order "stone" (x10) -> 0 companion(s) (and at least one already had enough)`) — o teto da 3.2c junto.

#### 7. O teste em jogo (modelo pequeno): o contrato ensina que `action` e `resource` combinam

A outra metade do mesmo achado é do **modelo**, não nossa: nas duas últimas linhas da tabela da seção 6 o
`qwen2.5-3b-instruct` devolveu `action:"wood"` **com** `resource:"wood"` para `colete 5 pedras` e `colete 10 pedras` —
ou seja, ele copiou o `action:"wood"` do exemplo **`"colete 20 de madeira"`** do contrato (o verbo `colete` era o
gatilho) e ignorou a pedra, apesar de a regra do `resource` já existir. É *few-shot overfitting*, o modo de errar típico
de um 3B: o exemplo pesa mais que a regra.

**O que mudou (só no `Contract`):** **uma** regra curta dizendo que os dois campos **têm que combinar**
(`pedra` → `action:"stone"` + `resource:"stone"`; `madeira` → `wood`/`wood`; `minério` → `ore`/`ore`; "NUNCA use action
\"wood\" para pedra") e **2 exemplos com pedra e o verbo `colete`**, logo **depois** do exemplo de madeira que causava a
cópia:

```
"colete 10 pedras" -> {"action":"stone","reply":"Vou buscar pedra.","target":null,"resource":"stone","amount":10}
"colete 5 pedras"  -> {"action":"stone","reply":"Vou buscar pedra.","target":null,"resource":"stone","amount":5}
```

- **O resto do contrato ficou intacto** (as 29 ações, o `target` obrigatório em `attack`/`equip`/`drop`, as regras do
  `amount`/`none`, os outros 19 exemplos) e o prompt cresceu **3 linhas** (~+60 tokens sobre os ~1766 medidos), porque
  ele está calibrado para o `google/gemma-4-e4b` e para o 3B.
- **Nada mais mudou:** `ValidActions`/schema, o `LlmIntent`, o `LlmClient`, a personalidade/memória, o TTS, o painel, o
  STT e as ordens ficaram iguais. A correção da seção 6 continua sendo a rede de segurança para quando o modelo insistir
  em mandar os campos trocados.
- **Verificado em jogo:** o 3B **deixou** de errar com o prompt novo — o teste do jogador (`colete 10 pedras`) voltou
  `stone`/`stone` e a ordem foi aplicada (o log está na seção 6). A correção da seção 6 continua sendo a rede de
  segurança para quando o modelo insistir em mandar os campos trocados.

#### 8. O teste em jogo (modelo pequeno): as ações vizinhas — `drop`, `pickup` e `nopickup`

O teste em jogo com o **`qwen2.5-3b-instruct`** (saída estruturada ligada, ~350 ms por ordem) achou a **mesma classe de
erro** da seção 7 — o 3B **copia os exemplos** do contrato e **confunde ações vizinhas** —, agora entre **`drop`**,
**`pickup`** e **`nopickup`**:

| Frase do jogador | O que o modelo devolveu | O que acontecia |
|---|---|---|
| `jogue fora sua madeira` (3 de 4 tentativas) | `{"action":"nopickup",...,"resource":"wood"}` | **desligava a coleta automática** em vez de largar a madeira ❌ |
| `jogue sua madeira fora` | `nopickup` | idem ❌ |
| `jugue a madeira fora` (erro de digitação) | `nopickup` | idem ❌ |
| `drop a madeira` | `drop` | largou a madeira ✅ |
| `jogue fora as pedras` | `drop` | largou as pedras ✅ |
| `junte a madeira do chão` (4 tentativas) | **50/50** entre `pickup` ✅ e `nopickup` ❌ | metade das vezes não recolhia |
| `equipe a picareta` | `{"action":"equip","target":"machado de bronze"}` (copiou o exemplo do machado) | o fallback da 3.6b salvou (equipou a **picareta** certa), mas o alvo veio errado do modelo |

São **duas** correções, no mesmo espírito da seção 7: **(8.1)** o contrato ensina as formas que falharam e **(8.2)** uma
rede de segurança nas palavras do jogador.

##### 8.1 O contrato ensina as formas que falharam

**O que mudou (só no `Contract`):** **uma** regra curta separando as três ações vizinhas — `drop` = LARGAR/JOGAR FORA um
item que o companheiro tem, `pickup` = RECOLHER itens do chão, e `nopickup` **só** quando o jogador pedir para PARAR de
recolher ("pare de recolher", "não pegue nada") — e **3 exemplos novos**, logo depois dos equivalentes:

```
"jogue fora a madeira"     -> {"action":"drop","reply":"Largando.","target":"madeira","resource":null,"amount":null}
"junte a madeira do chão"  -> {"action":"pickup","reply":"Recolhendo.","target":null,"resource":null,"amount":null}
"equipe a picareta"        -> {"action":"equip","reply":"Picareta na mão.","target":"picareta","resource":null,"amount":null}
```

- O exemplo com **`equipe`** era o que faltava para o `equip`: o contrato só tinha **`"pega a picareta"`** (Fase 3.6), e
  foi justamente a forma com **`equipe`** que falhou. E `pickup`/`nopickup` **não tinham exemplo nenhum** — os dois novos
  são os primeiros deles.
- **Só o `Contract` mudou:** `ValidActions`/schema, o `LlmIntent`, o `LlmClient`, a personalidade/memória, o TTS, o
  painel, o STT e as ordens ficaram **intactos**. O prompt cresceu **6 linhas** (~+120 tokens sobre os ~1766 medidos,
  estimativa).
- **Não verificado:** se o 3B **deixa** de errar com o prompt novo — é o teste em jogo do jogador (o prompt maior não foi
  medido contra nenhum modelo aqui). O erro de digitação (`jugue`) não é ensinável por prompt: quem o cobre é a 8.2.

##### 8.2 A rede de segurança: as palavras do jogador mandam quando a ação é vizinha

Mesmo raciocínio do "o recurso manda" (§6): `CompanionOrders.Run` chama **uma vez por mensagem**, no mesmo lugar,
**`PickupIntent.NeighborAction(action, userText)`** — novo `AI/PickupIntent.cs`, **puro** (sem Unity, sem config, sem
mundo, sem alocação por frame). Ele só olha a ação **exatamente `nopickup`** e devolve a ação que as palavras do jogador
pedem, ou `null` quando não há gatilho (e aí **tudo fica exatamente como estava**):

| O que o jogador disse | Gatilho | Ação usada |
|---|---|---|
| `jogue fora a madeira`, `jogue a madeira fora`, `joga no chão`, `larga`, `largar`, `descarta`, `soltar`, `deixa no chão` | verbo de largar/descartar (sozinho) **ou** verbo de jogar + `fora`/`no chão` **ou** verbo de deixar + `no chão` | **`drop`** |
| `junta`, `junte`, `juntar`, `apanha`, `apanhar` | verbo sozinho | **`pickup`** |
| `recolhe do chão`, `pega do chão`, `junta do chão`, `cata do chão` | verbo de pegar/recolher + `do chão` | **`pickup`** |

- **Guarda-corpos:** só com a ação **exatamente `nopickup`** (nenhuma outra é tocada); a intenção **legítima** do
  `nopickup` manda — um verbo de parar (`para`, `pare`, `parar`...) ou um `não pegue`/`não recolha` **desliga** a
  correção; e o casamento é feito **entre separadores** (`_larga_`), então `largada`/`jogada` não disparam nada. Sem
  gatilho, o comportamento é **idêntico** ao de hoje.
- **O alvo do `drop` não sai daqui:** quem o resolve continua sendo o caminho da Fase 3.6b — `OrderTargets.Phrases` lê as
  palavras do jogador e cada frase é **validada contra a mochila** (`OrderTargets.FindItem`, barra de
  `MinPhraseScore`). Nada de extração de nome nova, e nada é inventado: sem o item na mochila, o aviso antigo continua.
- O `drop` resultante continua respeitando as regras dele (item de missão nunca cai, sem número = pilha inteira, a pilha
  sai do auto-pickup) e o `pickup` é o **mesmo** toggle do menu radial (`SetAutoPickup`).
- **Uma troca deliberada:** `para` é palavra comum em português, então `joga a pedra para o chão` **não** é corrigido (o
  guarda-corpo ganha). O caso é raro e ficar quieto é mais seguro do que largar o item errado.
- **Log:** uma linha por mensagem corrigida —
  `[IA] O modelo devolveu "nopickup" mas o jogador pediu para largar - usei "drop".`
- **Verificado fora do jogo** com um harness que compila os arquivos **reais** (`PickupIntent` e `LlmIntent`; só o
  `CompanionsPlugin.Log` entra como stub): **58 casos, ALL CHECKS PASSED** — as três formas do log (inclusive a do erro de
  digitação), os três gatilhos de `drop`, os três de `pickup`, os guarda-corpos do `nopickup` legítimo, as ações que não
  são `nopickup` e as mensagens sem gatilho. **O teste em jogo é do jogador.**

**Receita de teste (para o jogador):** com o companheiro carregando madeira, `jogue fora sua madeira` deve **largar a
madeira** (e o log traz a linha da 8.2 quando o modelo insistir em `nopickup`); `junte a madeira do chão` deve
**religar** a coleta automática; `pare de recolher` deve continuar **desligando** a coleta (é o `nopickup` de verdade); e
`equipe a picareta` deve equipar a **picareta** (com o contrato novo, o `target` do modelo já deve vir certo — o
fallback da 3.6b continua sendo a rede).

### Fase 3.1 — a ação de recolher os itens do chão (o que faltava) (✅ validado em jogo — branch `feat/recolher`; teste final do jogador: *"pra mim parece excelente"*)

O jogador largou o machado (`jogue seu machado no chão` → `[IA] Order "drop": Companion dropped 1 x "Machado Nordico"`) e
depois pediu para pegar de volta (`pegue o machado nordico do chão`, `junte o machado`, `recolhe os itens`). O modelo
acertou (`[IA] Order "pickup" -> 1 companion(s)`, `Ok: recolher itens do chao`) e **nada aconteceu**: o machado ficou no
chão.

#### 1. O achado: a ordem `pickup` era só um flag

| # | Achado | Consequência |
|---|---|---|
| 1 | `CompanionOrders` (`case "pickup"`) só chamava `SetAutoPickup(companion, true)` — um flag no ZDO do companheiro (`CompanionSetup.AutoPickupHash`) | **Não existia ação nenhuma**: nenhum job do mod andava até um item no chão e o pegava. O único "pega item" automático era o ímã de 2 m do `CompanionAI.AutoPickup` |
| 2 | O que o companheiro larga é marcado `m_autoPickup = false` de propósito (`CompanionOrders.DropNamed`, `MHR_GatherRules` — *"so neither it nor anyone's auto-pickup grabs it straight back"*), que é o que o **próprio jogo** faz em `ItemDrop.OnPlayerDrop()` | O ímã (`if (component == null \|\| !component.m_autoPickup) continue;`) **e** o `MHR_Loot` (o `DropOf` dele exige `m_autoPickup`) pulam exatamente o item que o jogador pediu para pegar |
| 3 | O flag só era lido pelo `MHR_Loot` (e pelo rótulo do menu radial) | O botão **Recolher** da roda **não desligava o ímã**: o interruptor parecia quebrado (igual no upstream — não é regressão nossa) |

Os três se somam: o item largado é justamente o que ninguém pega.

#### 2. O job novo: `src/Companions/Jobs/MHR_Collect.cs`

Mesmo padrão do `MHR_Loot` (que já fazia "andar até um ponto e pegar o que há lá"): estado por companheiro
(`Dictionary<int, State>`), `Start(CompanionSetup)` chamado pela ordem e `Drive(CompanionAI, float dt)` chamado por frame.

- **Quem chama por frame:** `CompanionAI.UpdateAI`, **antes** do `MHR_Loot.Drive` — uma ordem explícita ganha o movimento,
  e o relógio do job de abate pausa sozinho enquanto ele não é dirigido (regra que o `MHR_Loot` já tinha).
- **A ordem inicia:** `CompanionOrders.Apply`, `case "pickup"` — que agora faz **as duas coisas**: liga o flag (como antes)
  **e** chama `MHR_Collect.Start(companion)`. O `nopickup` continua só desligando o flag (e desligar o flag **para** um
  recolhimento em andamento).
- **O que ele faz:** acha o item solto **mais próximo** dentro de `[Looting] CollectRadius` (novo, padrão **10**, faixa
  1..30, medido **do companheiro**), anda até ele com o `MHR_Mover` (tag `Collect`, o mesmo mover de todos os jobs) e pega
  com `Humanoid.Pickup(..., autoequip: false, autoPickupDelay: false)`. Repete com o próximo até não sobrar nada no raio,
  a mochila encher ou a ordem passar de **30 s** (tempo em que ele **não** foi dirigido — briga, descanso — não conta).
- **Por que `Humanoid.Pickup` e não o "puxão" de 15 m/s do ímã:** conferido no jogo decompilado — `Humanoid.Pickup` **não
  olha** `m_autoPickup` (só `ItemDrop.CanPickup` — o atraso de 0,5 s do spawn —, a mochila e o item de missão), e é a mesma
  chamada do `MHR_Loot`, do `MHR_PileTaker`, do `UpkeepPiles`, do `UpkeepHoney` e do `MHR_SpearThrow`. O ímã, além de só
  alcançar 2 m, é **filtrado** por `m_autoPickup` — ele nunca resolveria o caso relatado.
- **O que ele aceita:** itens soltos válidos (`ZNetView` válido), `!InTar()`, que caibam na mochila — **incluindo
  `m_autoPickup == false`** (os que o companheiro ou o jogador largaram de propósito), que é o pedido.
- **O que ele recusa:** item de missão (`m_shared.m_questItem`, a mesma regra que o `drop` aplica), `ItemDrop` que é peça
  (`IsPiece()` — o auto-pickup do próprio jogador também recusa), posição na *blacklist* do `CompanionAI`
  (`IsPositionBlacklisted`), água funda (`MHR_Water.WouldSwim` — a regra "nunca nadar por loot" de 1.4.97, valendo para
  todo mundo) e o que **não couber inteiro** na mochila (`Inventory.CanAddItem(item)` sem número, que é exatamente a
  pergunta que o `Pickup` faz; sem isso um item que não cabe seria escolhido como alvo de novo a cada frame).
- **Pega e para:** outra ordem (o `CompanionOrders.Run` derruba o recolhimento em andamento para **qualquer** ordem que não
  seja `pickup`, e o `CompanionInteract.CancelExistingActions` — o funil que a roda usa — faz o mesmo), troca de trabalho
  (o modo de trabalho mudou para um job), `Recolher` desligado, mochila cheia, morte, posse perdida (outro jogador passou a
  rodar o companheiro) e a **coleira** (`[Looting] MaxDistance`, a mesma que o `MHR_Loot` usa: você se afastou e o item
  ficou para trás).
- **Os jobs esperam:** `MHR_JobRules.OtherOrder` passou a responder `true` enquanto o recolhimento está em andamento, então
  os controladores de trabalho (`HarvestController`, `FarmController`, `CookController`, ...) **param** enquanto ele anda —
  todos dirigem pelo mesmo `MHR_Mover`, e dois tags steerando ao mesmo tempo reiniciam a rota um do outro a cada frame.
- **Logs (com o nome do companheiro, para logs com vários deles):** `[Collect] Thorin: Going to pick up "Machado Nordico"
  (3 m away)`, `[Collect] Thorin: Done: 1 item(s) picked up`, `[Collect] Thorin: nothing on the ground within 10 m -
  nothing to pick up`, e uma linha por parada (`Stopped: another order came in`, `... Recolher was turned off`, `... another
  job was picked`, `... the order took too long`, `... you've moved on`).
- **Fala:** quando não há nada no chão, o companheiro **diz** `hc_ai_collect_nothing` ("Não tem nada no chão por perto.");
  com a mochila cheia, `hc_ai_collect_full` ("Minha mochila está cheia.") — as **duas chaves novas**, no
  `Translations/Portuguese_Brazilian.json` (PT-BR) e no dicionário em inglês do `ModLocalization` (para outra língua não
  mostrar o token cru). A confirmação da ordem continua sendo a chave que já existia (`hc_ai_order_pickup`).

#### 3. O ímã e o `MHR_Loot` continuam protegidos

Nada mudou neles: o ímã mantém `if (component == null || !component.m_autoPickup) continue;` e o `MHR_Loot` mantém o
filtro no `DropOf` (`if (d == null || !d.m_autoPickup) return null;`). **Só** esta ordem explícita aceita o que foi
largado de propósito — a proteção do "larga pra dar" fica intacta.

#### 4. O botão Recolher da roda passa a valer

`CompanionAI.UpdateAI` deixou de chamar `AutoPickup(dt)` direto: ele lê o ZDO **uma vez** por frame e só chama o ímã com
o flag ligado — `pickupZdo == null || pickupZdo.GetBool(CompanionSetup.AutoPickupHash, defaultValue: true)`.

- **Com o padrão (`true`) nada muda:** é o valor lido quando a chave não existe no ZDO, então um companheiro que nunca
  mexeu no botão **Recolher** (e um companheiro sem ZDO) continua com o ímã exatamente como hoje.
- Os jobs que pegam coisas **não** passam pelo ímã: `MHR_PileTaker`, `UpkeepPiles`, `UpkeepHoney`, `MHR_SpearThrow` e o
  `HarvestController` chamam `Humanoid.Pickup` direto (conferido no código), então desligar o Recolher não quebra nenhum
  deles.

#### Receita de teste (para o jogador)

1. Com o companheiro carregando um machado, `jogue seu machado no chão` (ou `larga o machado`) → o machado cai e fica no chão.
2. `pegue o machado nordico do chão` (ou `junte o machado`, `recolhe os itens`) → ele **anda até o machado e pega**
   (`[Collect] ... Going to pick up "Machado Nordico" (N m away)` e depois `Done: 1 item(s) picked up`).
3. `recolhe os itens` sem nada no chão → ele diz **"Não tem nada no chão por perto."** e o log traz a linha do nada
   encontrado (antes: silêncio absoluto).
4. **Recolher** desligado na roda → o ímã de 2 m para de pegar itens ao passar por cima (e um recolhimento em andamento
   para). Ligar de volta → volta.
5. `para de recolher` (`nopickup`) e depois `recolhe os itens` → o flag volta e ele cata de novo.

**Teste em jogo:** o jogador confirmou o teste final da fase (*"pra mim parece excelente"*). Verificado aqui antes: o
build (0 erros, 0 avisos), a `Humanoid.Pickup`/`ItemDrop.CanPickup`/`OnPlayerDrop`/`CanAddItem` no jogo decompilado, e os
pontos de chamada do ímã e do `MHR_Loot` no código.

### Correção — o clique engolido no painel F8 e o texto do centro da roda: **a camada era a causa** (✅ build 0 erros, 0 avisos — branch `fix/camadas-ui`; **o teste em jogo é do jogador**)

O jogador reabriu o bug depois da `fix/clique-f8`: *"está a mesma merda"*. Estava mesmo — aquela correção tinha
consertado os **decorativos** do painel (que era um problema real e continua consertado), mas **não** a causa
principal. O diagnóstico `[UiDiag]` que ela mesma deixou no `ConfigPanel` achou o culpado na primeira abertura:

```
[UiDiag] F8 raycast audit: canvas "HUD" (order=400, overrideSorting) has a raycast target over the controls of the panel:
         .../IngameGui/HUD/hudroot/crosshair/HoverName (TextMeshProUGUI 350x150) at the panel point 196,-46
[UiDiag] F8 raycast audit: canvas "EnemyHud" (order=200) ... HudBase(Clone)/Name (TextMeshProUGUI 400x52) at the panel point 196,230
[UiDiag] F8 raycast audit: panel order=33, ... 27 other active canvas(es): HC_RadialCanvas=30, ..., EnemyHud=200,
         HudMessage=1000(override), HUD=400(override), Menu=1700(override), UnifiedPopup=1900(override), ...
```

**A causa:** o HUD do jogo é um canvas em **`sortingOrder = 400`** (com `overrideSorting`) e **todos** os canvases do
mod estavam em **28..34** — atrás dele. O alvo que engolia o clique é o **`crosshair/HoverName`** (350x150 **no centro
da tela**, onde o painel está), e ele **recebe clique mesmo invisível** (o jogo o apaga com
`canvasRenderer.SetAlpha(0f)` e **não** desliga o `raycastTarget`) — daí a **faixa** e daí rolar o painel resolver.
**Por que 31 → 33 não bastou:** 33 continua **abaixo de 400**. E em **empate** de `sortingOrder` o uGUI desempata pela
ordem de registro dos raycasters (`RaycasterManager`), que o canvas do jogo — registrado muito antes — ganha.

#### 1. As camadas, antes → depois (`src/Companions/UI/ModUiLayers.cs`, fonte única)

| Canvas do mod | Antes | Depois | Onde |
|---|---|---|---|
| `HC_FarmZoneHud` (`FarmZonePlacer`) | 28 | **1701** | `FarmZonePlacer.BuildHud` |
| `HC_HomeZonePanel` (`HomeZonePanel`) | 29 | **1702** | `HomeZonePanel.Build` |
| `HC_RadialCanvas` (`CompanionRadialMenu`) | 30 | **1703** | `CompanionRadialMenu.BuildUI` |
| `HC_StarterCompanionPanel` (`StarterCompanionPanel`) | 30 | **1704** | `StarterCompanionPanel.Awake` |
| `CropPickerCanvas` (`FarmZoneCropPicker`) | 31 | **1705** | `FarmZoneCropPicker` |
| `HC_ConfigPanel` (`ConfigPanel`) | 31 → 33 | **1706** | `ConfigPanel.Build` |
| `HC_AiPanel` (`CompanionAiPanel`) | 33 | **1707** | `CompanionAiPanel.Build` |
| `Overlay` (o canvas do dropdown do painel da IA) | 34 | **1708** | `CompanionAiPanel.Build` |

A ordem **relativa** entre os nossos é a de antes; mudou só a faixa. As camadas do jogo que importam, medidas no
`[UiDiag]` do log do jogador: `EnemyHud` 200, `Chat` 300, `HUD`/`MiniMap`/`wind_marker` **400**, `TopLeftMessage` 500,
`Inventory_screen` 600, `Store_Screen` 700, `BarberGui` 800, `HudMessage` **1000**, `TextInput` 1100, `TextViewer` 1200,
`ConnectionPanel` 1300, `Tutorial` 1400, `Captions_DirectionIndicators` 1500, `ClosedCaptions` 1600, `JoinCodeOverlay`
1650, `Menu` **1700**, `UnifiedPopup` **1900**, `LongPressRadial` **2000**, `_Console(Clone)` **5000**, `SwitchCursor`
12000.

**Por que 1701 e não 2000** (a sugestão inicial):

- **1701 está acima de tudo o que pode roubar o clique:** o HUD (400), o `HudMessage` (1000) e o `Menu` (1700). Os
  últimos três — inventário (600), loja (700), barbeiro (800), entrada de texto (1100) e menu (1700) — **não** podem
  abrir com um painel nosso na tela: os prefixos Harmony do mod em `Hud.InRadial` respondem `true` enquanto um painel
  está visível, e é isso que o jogo consulta (`InventoryGui.Update`: `... && !Hud.InRadial() && !Hud.InBuildUi()`;
  `Menu.Update`: `... && !Hud.InRadial()` — conferido no `assembly_valheim` decompilado).
- **Para em 1899 porque o `UnifiedPopup` está em 1900 e é do jogo.** Ele é uma classe vanilla de `assembly_valheim`
  (o popup modal que o mod usa para confirmar o *despawn* e a recuperação no santuário) e a ordem vem do prefab do
  jogo: **não dá para movê-lo**. A regra que fica é a certa de qualquer UI: um **diálogo modal** sempre na frente de
  uma **janela**.
- **2000 exatamente seria um empate** com o `LongPressRadial` do jogo — e empate o jogo ganha (ordem de registro dos
  raycasters). A faixa 2001+ funcionaria, mas deixaria o popup modal do jogo **atrás** dos nossos painéis.
- Continua muito abaixo do console (5000) e do `SwitchCursor` (12000).

#### 2. O segundo sintoma, o mesmo remédio: o texto do centro da roda

O `CompanionRadialMenu` desenha `_centerName`/`_centerAction`/`_centerState` no **centro** da roda — que é o centro da
tela, exatamente onde vive o `crosshair/HoverName` do jogo. Com o canvas do radial em 30 e o HUD em 400, o jogo
desenhava **por cima** e os dois textos se misturavam (o relato: *"ficou todo bugado o texto ... tipo com quebra de
linha nada a ver"*). Com o radial em **1703** a roda desenha por cima do hover do jogo. (O radial já esconde o
crosshair enquanto está aberto — `Hud.UpdateCrosshair` — mas **não** apagava o `HoverName` do desenho.)

#### 3. O audit: mantido e generalizado

- A lógica saiu do `ConfigPanel` para **`src/Companions/UI/ModUiAudit.cs`** e os dois painéis de tela cheia a chamam
  **uma vez por sessão**, na primeira abertura (`[UiDiag] F8 raycast audit` e `[UiDiag] AI panel raycast audit`) —
  **nunca por frame**, e nada é alocado por frame.
- O que ele reporta: (a) todo alvo de clique **dentro** do painel que **não** seja um controle (com a razão: decorativo
  sem `Selectable`, ou barra de scroll invisível) — os "on purpose" (o backdrop modal e as áreas de scroll) saem
  listados; (b) **todas** as camadas ativas da cena, `nome=ordem(override)`; (c) para cada canvas **acima** do painel,
  os alvos que cobrem a coluna de controles (11 pontos amostrados) — **um aviso por alvo**, com o caminho completo, o
  tipo e o tamanho. Um hit ali **é** a faixa invisível.
- Novidade: os canvases **do próprio mod** continuam **listados** (fazem parte do quadro) mas **não** são sondados —
  dois painéis nossos nunca ficam um sobre o outro (todos recusam abrir juntos), então um hit ali seria o audit
  achando a si mesmo.

#### 3.1. O audit vira **opt-in** (`[Advanced] UiDiag`, desligado por padrão)

O audit cumpriu o papel dele (achou o `HUD` em 400 e o `crosshair/HoverName` em cima dos controles) e **não** foi
removido — mas **uma sessão dele saiu com 6 linhas / 7.132 caracteres**, e **três** dessas linhas eram **falsos
positivos** do painel da IA:

```
[UiDiag] AI panel raycast audit: UNEXPECTED raycast target inside the panel: HC_AiPanel/AiRoot (no Selectable in its parents (decorative))
[UiDiag] AI panel raycast audit: UNEXPECTED raycast target inside the panel: HC_AiPanel/AiRoot/KeyBox (no Selectable in its parents (decorative))
[UiDiag] AI panel raycast audit: UNEXPECTED raycast target inside the panel: HC_AiPanel/AiRoot/Overlay/PersonalityList (no Selectable in its parents (decorative))
```

- **A config:** `[Advanced] UiDiag` (bool, **`false`**). Ficou na `[Advanced]` e **não** na `[IA]` porque ele **não é
  da camada de IA**: ele audita a **UI do mod** — o painel F8 **e** o painel da IA, e a faixa de camadas que os dois
  compartilham (`UI/ModUiLayers.cs`). O `[IA] EcoDiag` está na `[IA]` porque diagnostica um componente da IA (a voz do
  companheiro); este é um diagnóstico de UI, e `[Advanced]` é o balde dos ajustes que ninguém mexe no dia a dia, que é
  exatamente o caso de um despejo de diagnóstico. (A descrição da config diz o que ele loga, quando ligar e que não
  muda nada no jogo.)
- **Desligado (padrão): zero linhas e zero custo.** Os dois chamadores (`ConfigPanel.LogRaycastAudit` e
  `CompanionAiPanel.LogRaycastAudit`) saem **antes** de entrar no `ModUiAudit` — nada de montar lista, nada de
  percorrer a cena (`Resources.FindObjectsOfTypeAll<Canvas>()`) e nada de varrer os `Graphic` do painel. O flag de
  "uma vez por sessão" só é marcado quando o audit **roda de verdade**, então ligar a opção no meio da sessão (o
  toggle aparece na aba *Avançado* do F8) faz o audit sair na próxima abertura do painel, sem reiniciar o jogo.
- **Ligado, compacto:** **duas** linhas iniciais, uma vez por sessão (a de que o audit está ligado, o mesmo padrão do
  `[ECO]`, e a da allow-list — o que o audit **nunca** reporta, e por quê) e **uma linha de resumo por painel**, com
  **contagens** — os nomes dos alvos "de propósito" e dos controles (que eram o grosso do log) **não** são mais
  listados. A **lista de camadas da cena continua em uma linha**: foi ela que nomeou o `HUD` em 400, é a parte que se
  paga. Os **avisos** continuam **linha por linha**, com caminho + tipo + tamanho + o ponto sondado.
- **Os três falsos positivos do painel da IA:**
  1. **`AiRoot`** (o fundo do painel) → **`raycastTarget = false`**: é decoração e é **pai de todo controle** (nunca
     pode ficar na frente de um), e o **`Backdrop` do próprio painel** (criado **antes** do root, tela cheia, alvo de
     clique **de propósito**) já é o catcher modal — o alvo no fundo era **redundante**. É exatamente o que o
     `ConfigRoot` do painel F8 já fazia (`ConfigPanel.Build`, com o mesmo comentário).
  2. **`KeyBox`** (a caixa escura onde a tecla do push-to-talk é desenhada) → **`raycastTarget = false`**: também é
     decoração — não há nada para clicar nela (o botão *Alterar* é um GameObject à parte, ao lado) — e o clique que
     caía nela não fazia nada; agora quem o pega é o `Backdrop`, que também não faz nada.
  3. **`Overlay/PersonalityList`** e **`Overlay/VoiceList`** (o fundo das listas do dropdown) → **continuam alvos de
     clique, de propósito**, e entraram numa **allow-list documentada** do audit (`ModUiAudit.Intentional`, casada pelo
     **fim do caminho** + o motivo): os botões de opção são **2 px menores** que a lista, então o clique nessa borda (ou
     na sobra embaixo da última opção) cairia no `ListCatcher` — que **fecha** a lista — ou num controle atrás dela. A
     lista tem que engolir esse clique, que é o que um dropdown aberto faz. (As duas listas são construídas pelo mesmo
     `CreateDropdown`, então as duas aparecem no audit: o log do jogador citou uma e a `VoiceList` é idêntica.)
- **O que não mudou:** a lógica, o layout, as cores, os textos e as camadas dos painéis; o `Backdrop` continua o
  catcher modal (um clique **fora** do painel não chega ao jogo), a lista aberta continua engolindo o clique que não é
  de uma opção, e o bloqueio de atacar/andar com o painel aberto continua sendo o dos patches de input
  (`Hud.InRadial` / `Player.TakeInput` / `PlayerController.TakeInput`), **não** o alvo de clique do fundo do painel.

#### 4. O que **não** mudou

Nada de lógica, layout, texto, LLM, TTS, ordens, jobs ou STT. Nada de posição/tamanho de nada: só o `sortingOrder`
dos oito canvases e o audit. `raycastTarget` dos controles, `Esc`, o X, as abas, os ON/OFF, o *Padrão*, os sliders, o
*key bind*, o scroll do F8, o dropdown/Ouvir/push-to-talk do painel da IA e o abrir/fechar/escolher setor da roda
seguem exatamente iguais — o que mudou para eles é **só quem está na frente de quem**.

#### 5. Receita de teste (para o jogador)

1. **F8** → clicar no ON/OFF de uma linha no **meio** do painel, sem rolar: tem que responder na hora (era isso que
   não respondia). Testar também **Padrão**, um slider, o *key bind*, a troca de aba e o X.
2. **F8** com o painel aberto → rolar o conteúdo: o scroll continua funcionando.
3. Segurar a tecla da **roda** num companheiro: os rótulos do **centro** (nome / ação / estado) têm que sair limpos,
   sem o texto do hover do jogo por cima; escolher um setor e soltar continua igual.
4. **Painel da IA** (tecla `I`) → dropdown de personalidade/voz (a lista tem que abrir **por cima** dos controles e
   receber o clique), **Ouvir**, push-to-talk, e **Esc** fechando.
5. `BepInEx\LogOutput.log` → com `[Advanced] UiDiag` **desligado (o padrão)** **não** deve haver **nenhuma** linha
   `[UiDiag]`. Ligando a opção (aba *Avançado* do F8) e abrindo os dois painéis, saem uma linha
   `[UiDiag] Raycast audit enabled ([Advanced] UiDiag = true): ...`, uma
   `[UiDiag] F8 raycast audit: panel order=1706, N raycast target(s) (...), 0 unexpected; K other active canvas(es): ...`
   e uma `[UiDiag] AI panel raycast audit: panel order=1707, ...` com `0 unexpected`. **Não deve haver nenhum aviso**
   `UNEXPECTED raycast target inside the panel` nem `has a raycast target over the controls of the panel` (se houver,
   ele nomeia quem é).
6. **Painel da IA** com um dropdown **aberto** → clicar na **borda** da lista (2 px) não pode fechar a lista nem
   acionar um controle atrás dela; clicar no painel **fora** da lista fecha a lista; clicar **fora do painel** não pode
   chegar ao jogo (nem andar, nem atacar) — quem pega é o `Backdrop`, como antes.

**O que não foi possível verificar aqui (é do jogador):** tudo o que depende do jogo rodando — o clique em si, o
desenho do radial, o dropdown do painel da IA, o comportamento de clique do painel da IA depois dos dois
`raycastTarget = false` (fundo e caixa da tecla) e o conteúdo do `[UiDiag]` em jogo (ligado e desligado). Verificado
**por leitura de código** (compilado, 0 erros, 0 avisos): as camadas do jogo e as do mod, o portão `Hud.InRadial` do
`InventoryGui`/`Menu` no `assembly_valheim` decompilado, que o `UnifiedPopup` é classe vanilla (não é nossa e não foi
tocada), e que o bloqueio de input do painel da IA vem dos patches (`Hud.InRadial`, `Player.TakeInput`,
`PlayerController.TakeInput`) e não do alvo de clique do fundo do painel.

---

### Fase 2.0 — a sincronização com o upstream 1.5.8 e as 5 ordens de interação (1.9.2, branch `feat/ordens-interacao`)

Antes de tudo: **este é o primeiro sync do upstream desde a base `1.5.4`**. Ele foi feito **incremental por release**
(`1.5.5` → `1.5.6` → `1.5.7` → `1.5.8`, merges `f7c4761`, `bc6016f`, `c0b81f2`, `6a1205d` + as correções `3433292` e
`baba127`), com a regra combinada: **o upstream manda em comportamento/jogo; nós mandamos na camada de IA, na
localização, nas chaves de config e nas camadas de UI.** 121 arquivos, +14.907/−2.600, **63 byte a byte idênticos**.

#### 1. O que veio do upstream (e ficou)

Combate como um jogador (bloqueio/aparada, táticas, arcos), navios e assento, colmeias, tochas, lava, peso efetivo
(marcha, sem correr/pular, stamina), fazenda em terra arada, o Santuário de Odin (recuperar/invocar +
`[General] RecoverUnsummoned`), a **reforma da fala** (fila com prioridade, dedupe por assunto, **linha de status** com
o motivo quando o trabalho trava, mensagem no canto quando você está longe) e o **suporte a controle** (binds, F8
navegável, `[Advanced] LogLevel`). O que é **nosso** e ficou: o teto de peso `CompanionCarry` (o `CarryLimit` deles
saiu de todos os 17 pontos), `[Speech] Chatter` como **bool**, o `SpeechConfig` da M1 (retry de 30 s) e a faixa de UI
**1701+**.

#### 2. As 5 ordens (`hive`, `door`, `board`, `sit`, `deposit`)

Implementadas em branch **empilhada** para o teste ter um build só. Detalhes e armadilhas na seção da *Fase 2.0* do
`AGENTS.md`; o resumo: reusam as `Direct*` da **tecla de comando**, resolvem o alvo (apontado → mais próximo), chegam a
**um** companheiro (`MHR_InScope`), silenciam a fala aleatória da `Direct*` durante a ordem (`Silent`) e respondem com a
**sua** linha. `ValidActions` 30 → **35** (fonte do `enum` do schema) e +7 linhas em cada um dos 5 contratos.

#### 3. A auditoria antes do teste (7 frentes, read-only)

Relatório completo em **`docs/audit-upstream-1.9.2.md`**. Sete frentes independentes (global, IA/voz/ordens,
config/UI/localização, rede/ZDO, Harmony, "o que do upstream ficou de fora" e contrato/schema), com harness de reflexão
sobre a DLL real (172 + 184 casos) e o decompilado do Valheim + o Harmony do BepInEx. **27 achados**, dos quais 3 altos:

- **A1 — as zonas de plantio sumiam no túmulo** (o leitor do `HC_Respawn_FarmZones` sobreviveu ao merge, o escritor não).
- **P1 — o F8 gravava o primeiro valor de `LogLevel`/`PortalItems`/`RespawnStance`** (o nosso `BuildChoiceRow` da M2
  recebia as listas genéricas do upstream e devolvia a lista de idiomas do Whisper; o `Clamp` do BepInEx transformava
  qualquer escolha em `Off`/`AllowAll`/`Passive`).
- **B1 — "fecha a porta" fechava o inventário** (com `StructuredOutput` o modelo só pode responder `inventory_close`).

Corrigidos também: `senta` duas vezes, `deposit` contando pilhas e ignorando cerca/privacidade/posse do baú, o alvo
apontado olhando só o 1º hit, a postura roubando as 5 ordens, `MHR_Recover.Cancel` sem chamador, o perfil de IA não
sobrevivendo ao Santuário, a dupla contagem do `MaxCompanions`, o claim no "desarmar o túmulo", 4 escritas diretas de
`HC_ActionMode`, os **75 rótulos do F8 + 2 abas** sem tradução, as **5 pools de fala** (`Wood`/`Stone`/`Ore`/`Upkeep`/
`Chat`) que estavam mudas nos 5 idiomas, a chave morta `GatheringSwingInterval`, o log duplicado da localização e o
custo dos patches novos do upstream com o log desligado (H1/H2/H4/H5).

**Adiados de propósito** (com o motivo no relatório): `LogLevel = Off` ainda não cala a camada de IA (B5), o memo de peso
por frame (H3), a liberação **semântica** da vaga no Despawn (só o texto ficou honesto), a bolha/voz em companheiros
diferentes (B9, pré-existente) e a medição das 5 ações novas com o `qwen2.5-3b-instruct`.

#### 4. Receita de teste (para o jogador)

1. **Zonas de plantio** (A1): 2 zonas no Fazendeiro → matar → ressuscitar pelo túmulo → as zonas voltam.
2. **Porta** (B1): "fecha a porta" com o inventário aberto → fecha a **porta**; "fecha o inventário" → fecha a janela.
3. **As 5 ordens** (B2/B3/B4/B7/B8): sem apontar e apontando; "senta" duas vezes (mesmo fogo e outro); 50 madeiras no
   baú → "Guardando 50 item(ns)"; uma delas com "na postura passiva" → faz a ordem, não só troca a postura.
4. **Recuperação** (A7): postura pelo radial durante a recuperação → ele para de ir ao túmulo.
5. **F8** (P1): escolher `Detailed`, `AllowAll` e `Keep` e conferir no `.cfg` que gravou o escolhido.
6. **Fala** (B6): cortar madeira, minerar pedra e minério, "Repair and Restock" → falas próprias; dois companheiros
   perto → o papo (`Chat`).
7. **Multiplayer** (A3/A4), se houver: "guarda isso no baú" no baú privado de outro jogador → recusa.








