# Roadmap — ModIA

Recriação, para o **Valheim 1.0 atual**, de um mod de companheiro NPC com IA (inspirado no "Thrall").

Legenda: ✅ concluído · 🔄 em andamento · ⏳ pendente

---

## Trilha A — Equipe de desenvolvimento (agentes + skills)

| Item | Status |
|---|---|
| 11 agentes (1 líder + 10 especialistas) em `.opencode/agents/` | ✅ |
| 6 skills de dependências (`bepinex`, `jotunn`, `thunderstore-packaging`, `valheim-reverse-engineering`, `valheim-ai-backend`, `valheim-assets`) | ✅ |
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
| **3** | IA e voz: comandos por voz/chat, STT, TTS, personalidade — **no fork, ver Trilha D** | 🔄 (3.1, 3.2 ✅) |
| **4** | Backend híbrido (LLM local + API), endpoints do "cérebro" | ⏳ |
| **5** | Empacotamento (manifest, README, ícone) + revisão final | ⏳ |

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

## Trilha C — Tradução PT-BR (fork)

O upstream (Offline Companions Reborn) tem **dois** sistemas próprios de tradução, os dois alimentados por
arquivos JSON ao lado do DLL:

| Sistema | Arquivo | O que cobre |
|---|---|---|
| `ModLocalization` (UI) | `Translations/<Idioma>.json` | Painéis, menu radial, hover, HUD, mensagens do HUD, localização do Santuário/lista |
| `SpeechConfig` (falas) | `Translations/speech/<Idioma>.json` | As falas do companheiro (saudações, conversa fiada, combate, fome, trabalho...) |

Idioma do jogo em PT-BR = **`Portuguese_Brazilian`**.

| Item | Status |
|---|---|
| `Translations/Portuguese_Brazilian.json` — **263 chaves** (204 do upstream + 59 novas para textos que estavam em inglês no código) | ✅ |
| `Translations/speech/Portuguese_Brazilian.json` — **186 falas** (15 categorias, mesmas do `SpeechConfig.Defaults`) | ✅ |
| Cópia no build (`Companions.csproj` → alvo `DeployTranslations`, `Translations\**\*.json` → `$(DeployDir)\Translations\`) | ✅ |
| Textos fixos em inglês que o jogador vê (painel F8 + abas, painel da casa, HUD, hover do túmulo/cama, recall, Santuário/lista) localizados via `ModLocalization.Loc/LocFmt/LocOr` | ✅ |
| Descrições das configurações (~250) — só aparecem no `BepInEx\config\*.cfg`; o painel F8 mostra as chaves | ⏳ (fora do escopo; exigiria ~250 chaves novas) |
| Nomes técnicos das configurações no painel F8 (ex.: `MaxCarryWeight`) | ⏳ (mantidos iguais ao `.cfg`, por rastreabilidade) |
| Linhas de log/diagnóstico (`[Roster]`, `[Shrine]`, `[Direct]`...) | ⏳ (mantidas em inglês de propósito, para suporte) |

---

## Trilha D — Camada de IA (fork)

Ordens em **linguagem natural** por cima do mod do upstream. O companheiro continua sendo o do upstream
(radial, jobs, combate); a IA é só uma **nova entrada** para as mesmas ações — nada de novo dirige o NPC.

| Fase | Descrição | Status |
|---|---|---|
| **3.1** | **Ordem por texto no chat** (LLM local, API OpenAI-compatible) | ✅ validado em jogo |
| **3.2** | **Alvo da ordem**: `colete 5 madeiras` para em 5 (madeira/pedra/minério) e volta a seguir | ✅ validado em jogo |
| **3.2b** | Correção da 3.2: o número virou um **DELTA** | ❌ revertida pela 3.2c |
| **3.2c** | **Correção final da quantidade**: o número é um **ALVO TOTAL contado no inventário do companheiro**, só para o recurso pedido; com o alvo já na mochila ele nem começa e avisa | ✅ validado em jogo |
| 3.3 | Voz: captura com `Microphone` → STT (backend) → mesma caixa de ordens | ⏳ |
| 3.4 | TTS: o companheiro **fala** a confirmação (áudio do backend, `AudioSource`) | ⏳ |
| 3.5 | Personalidade/memória: preset + texto livre no contexto do LLM | ⏳ |
| 3.6 | Ordens com alvo (`atacar <criatura>`, `equipar <item>`, `largar <item>`) | ⏳ |
| 3.7 | UI do NPC: menu de personalidade/voz + keybind de push-to-talk | ⏳ |

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
- **Pendências conhecidas:** ordens com alvo (`atacar X`, `equipar Y`) ainda não têm efeito além do job
  (Fase 3.6). O número da ordem **limita** madeira/pedra/minério desde a Fase 3.2 e é, desde a Fase 3.2c, um
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

