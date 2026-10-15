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
| **3** | IA e voz: comandos por voz/chat, STT, TTS, personalidade — **no fork, ver Trilha D** | 🔄 (3.1, 3.2, 3.2c, 3.3, 3.5, 3.6, 3.6b ✅) |
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
| 3.3 | **Ordens por voz**: push-to-talk (`Microphone`, 16 kHz mono) → **STT local** (Whisper.net) → **mesma caixa de ordens** do chat | ✅ build 0 erros + DLL no plugin (teste em jogo pendente) |
| 3.3-spike | **SPIKE STT local**: Whisper.net **in-process** (NuGet, thread de fundo) x `whisper-server.exe` filho — teste mínimo, gatilhos `modia_stt_teste` / `[IA] SttTeste` | ✅ validado em jogo (`docs/re/stt-whisper-net-spike.md`) |
| 3.3b | **Correções do teste da voz**: conflito da tecla **V** com o *Toggle Auto-Pickup* do jogo, qualidade do STT (modelo `small` + prompt inicial + trim) e medição da latência do LLM | ✅ build 0 erros + DLL/modelo no plugin (teste em jogo pendente) |
| 3.3c | **Raio de coleta maior** (procura, alcance seguindo você, área com Follow off e drops) + **chatter aleatório desligado** por padrão (`[Speech] Chatter`) | ✅ build 0 erros + DLL no plugin (teste em jogo pendente) |
| 3.4 | TTS: o companheiro **fala** a confirmação (áudio do backend, `AudioSource`) | ⏳ |
| 3.5 | Personalidade/memória: preset + texto livre no contexto do LLM | ✅ validado em jogo |
| 3.6 | **Ordens com alvo**: `ataca o greydwarf` (engaja a criatura), `equipa o machado de bronze` (equipa do inventário dele), `larga a pedra` (joga fora) | ✅ validado em jogo |
| 3.6b | **Correção da ordem sem alvo**: quando o modelo responde `"target": null`, o item/criatura é lido das **palavras do próprio jogador**; o prompt passa a exigir `target` em `attack`/`equip`/`drop` | ✅ build 0 erros + DLL no plugin (teste em jogo pendente) |
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

### Fase 3.6 — ordens com alvo: atacar, equipar e largar (✅ — build; teste em jogo pendente)

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

### Fase 3.6b — a ordem sem alvo: o modelo responde `"target": null` (✅ — build; teste em jogo pendente)

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

### Fase 3.5 — personalidade, memória e conversa livre (✅ — build; teste em jogo pendente)

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

### Fase 3.3 — ordens por voz (push-to-talk + STT local) (✅ — build; teste em jogo pendente)

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
  `ConfigPanelKey` usa `F8`, o editor de zona usa `LeftAlt`, `F5` é o console e `F7` o reposicionamento de UI.
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

### Fase 3.3b — correções do teste da voz (tecla V, qualidade do STT, latência do LLM) (✅ — build; teste em jogo pendente)

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

### Fase 3.3c — raio de coleta maior e chatter aleatório desligado (✅ — build; teste em jogo pendente)

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
   `BepInEx\plugins\ModAI-BR-Companions` (feito).
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

### Fase 3.3d — a ordem de pedra para no alvo e o texto da voz vai para o balão do jogador (✅ — build; teste em jogo pendente)

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
   `BepInEx\plugins\ModAI-BR-Companions` (feito).
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

