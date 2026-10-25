# AI Companions — Referência de configurações

Todas as configurações do mod: o que cada uma faz e com que valor ela começa.

**Onde mudar:** no jogo, aperte `F8` para abrir o painel de configurações. Você também pode editar `BepInEx/config/AiCompanions.cfg` com qualquer editor de texto, com o jogo fechado.

**Dicas**

- Uma opção mostrada como fração (como `0.5`) significa 50%. `0.25` é 25%, `1` é 100%.
- As distâncias estão em metros. Para comparar: um jogador tem cerca de 2 m de altura e o alcance de construção de uma bancada é de 20 m.
- **Opções de lista** usam todas o mesmo formato, separadas por vírgulas:
  - `*` = tudo
  - `Nome` = só esse
  - `-Nome` = nunca esse
  - `Nome:Número` = esse, com a sua própria quantidade (`0` = sem limite)
  - Os nomes podem ser o nome do item ou da criatura no jogo ou o nome interno (prefab). Os nomes que você pode usar em cozinha, caça e pesca são escritos em `OfflineCompanionsReborn.Cooking.txt`, `.Hunting.txt` e `.Fishing.txt` na pasta de configuração a cada vez que o jogo inicia.
- Atualizando de uma versão antiga? As opções que você mudou são movidas para os nomes novos automaticamente na primeira vez que você entra no mundo. **Desde o 2.0.0** o arquivo é `AiCompanions.cfg` e as chaves e descrições são em inglês (o idioma nativo do mod; o português é uma tradução): o `.cfg` antigo (`Maddhattr.OfflineCompanionsReborn.cfg`) é ignorado e as configurações voltam aos valores padrão.
- Os nomes das seções abaixo são os mesmos do arquivo `.cfg` (e das abas do painel `F8`, que as mostram em português): `General` = **Geral**, `Controls` = **Controles**, `Follow` = **Seguir**, `Movement` = **Movimento**, `Combat` = **Combate**, `Food` = **Comida**, `Stamina` = **Stamina**, `Rest` = **Descanso**, `Death` = **Morte**, `Looting` = **Saque**, `GearRepair` = **Reparo de Equip.**, `Gathering` = **Coleta**, `Hunting` = **Caça**, `Fishing` = **Pesca**, `Farming` = **Plantio**, `Smelting` = **Fundição**, `Cooking` = **Cozinha**, `RepairAndRestock` = **Reparar e Reabastecer**, `Idle` = **Ócio**, `Speech` = **Fala**, `Advanced` = **Avançado**, `AI` = **IA**.

## Seções

- [General](#general)
- [Controls](#controls)
- [Follow](#follow)
- [Movement](#movement)
- [Combat](#combat)
- [Food](#food)
- [Stamina](#stamina)
- [Rest](#rest)
- [Death](#death)
- [Looting](#looting)
- [Gear Repair](#gear-repair)
- [Gathering](#gathering)
- [Hunting](#hunting)
- [Fishing](#fishing)
- [Farming](#farming)
- [Smelting](#smelting)
- [Cooking](#cooking)
- [Repair and Restock](#repair-and-restock)
- [Idle](#idle)
- [Speech](#speech)
- [Advanced](#advanced)
- [AI](#ai)

## General

O básico: quanto custa um companheiro, a vida, a stamina e os limites de carga dele, e até onde vai a "casa".

**CompanionPrice** — Padrão: 2000 · Faixa: 0 – 50000  
O preço de um companheiro extra no Trader, em moedas do banco compartilhado. 0 = de graça.

**MaxCompanions** — Padrão: 10 · Faixa: 1 – 10  
Fase 3.19: o número máximo de companheiros que você pode ter neste mundo (10 é o teto). A conta inclui os mortos e os perdidos: a lápide traz o MESMO companheiro de volta, então morrer não abre vaga. Ao chegar no limite, o Trader, o invocador do painel da IA e o companheiro inicial recusam criar outro (o Santuário continua podendo recuperar um companheiro morto). Para abrir vaga, dispense um pelo menu da roda (Dispensar).

**SpawnStarterCompanion** — Padrão: Ligado  
Na primeira vez que você entra num mundo com um personagem, é oferecido um companheiro de graça.

**ResetStarterFlag** — Padrão: Desligado  
Coloque true para receber de novo a oferta do companheiro inicial grátis no mundo atual (no próximo carregamento do mundo). Ele volta sozinho para false.

**ShowCompanionRoster** — Padrão: Desligado  
Coloque true para listar os seus companheiros neste mundo e o status de cada um: Vivo, Morto (a lápide dele vai trazê-lo de volta), Perdido (não sobrou nada dele - dá para recuperar) ou Desconhecido (num servidor dedicado, um companheiro que não está por perto não pode ser verificado). Ele volta sozinho para false.

**RecoverLostCompanion** — Padrão: Desligado  
Coloque true para trazer de volta um companheiro Perdido, ao seu lado, com o equipamento e as configurações do último salvamento da lista. Um companheiro Desconhecido precisa que você ligue a opção uma segunda vez em até 60 s para confirmar (ele pode ainda estar vivo em algum lugar). Se o companheiro antigo aparecer depois, ele é removido. Ele volta sozinho para false.

**ProtectCompanions** — Padrão: Ligado  
Impede que outros mods apaguem um companheiro de vez (por exemplo, mods que limpam criaturas de áreas protegidas). Os companheiros ainda podem morrer, deixar lápide e renascer normalmente, e o botão Dispensar continua funcionando. Quando algo é bloqueado, o log diz o que tentou. Desligue só se outro mod precisar remover companheiros.

**ShowMinimapPanel** — Padrão: Ligado  
Mostra o painel de status dos companheiros embaixo do minimapa.

**ShowOverheadBars** — Padrão: Ligado  
Mostra as barras de vida e stamina em cima da cabeça de cada companheiro.

**MaxCarryWeight** — Padrão: 300 · Faixa: 50 – 2000  
O peso BASE que um companheiro consegue carregar (um jogador carrega 300). Companheiros de coleta e de caça voltam para casa para descarregar antes de chegar nesse limite. Itens que aumentam a capacidade de carga somam por cima deste valor: o Megingjord, por exemplo, dá +150 e vale com o cinto em qualquer lugar da mochila do companheiro (não precisa equipar), então com este valor em 300 o teto dele passa a 450. Dois cintos iguais não somam: o bônus conta uma vez só, como no jogo.

**BaseHealth** — Padrão: 25 · Faixa: 1 – 500  
A vida de um companheiro antes da comida (um jogador tem 25).

**BaseStamina** — Padrão: 50 · Faixa: 1 – 500  
A stamina de um companheiro antes da comida (um jogador tem 50).

**MaxFoodSlots** — Padrão: 3 · Faixa: 1 – 5  
Quantas comidas diferentes um companheiro pode ter comido ao mesmo tempo (um jogador tem 3).

**StatusEffectsOnRegen** — Padrão: Ligado  
Efeitos de status mudam a regeneração de vida e stamina de um companheiro como mudam a de um jogador: Molhado e Com Frio deixam mais lenta, hidroméis e outros buffs mudam. Desligado = os companheiros regeneram no ritmo normal, sejam quais forem os efeitos.

**HomeTripRange** — Padrão: 60 · Faixa: 10 – 500  
A que distância de casa (em metros) um companheiro ainda volta para as idas ao baú: buscar ou devolver ferramentas, consertar uma ferramenta quebrada, esvaziar as bolsas. Mais longe do que isso, um companheiro sem a ferramenta do trabalho avisa e segue você até vocês dois estarem perto de casa de novo.

**HomeRadius** — Padrão: 50 · Faixa: 5 – 200  
Tamanho inicial (em metros) da área da casa de um companheiro em volta do ponto de casa - onde ele procura uma cama para reivindicar. O tamanho de cada companheiro pode ser mudado depois no painel da casa dele.

## Controls

As teclas e até onde os seus comandos alcançam.

**DirectTargetKey** — Padrão: `Z`  
A tecla de comando. Aperte olhando para alguma coisa para mandar os companheiros até ela (ir até lá, atacar, abrir a porta, sentar perto do fogo, consertar na bancada...). Segure para chamá-los até você.

**RadialMenuKey** — Padrão: `E`  
Segure esta tecla olhando para um companheiro para abrir a roda de comandos dele.

**ConfigPanelKey** — Padrão: `F8`  
Abre o painel de configurações no jogo, onde todas estas opções podem ser mudadas.

**FarmZoneKey** — Padrão: `Z`  
Aperte junto com FarmZoneModifier: abre o editor de zonas de fazenda.

**FarmZoneModifier** — Padrão: `LeftAlt`  
Segure esta tecla e aperte FarmZoneKey para abrir o editor de zonas de fazenda.

**RecallRange** — Padrão: 40 · Faixa: 0 – 500  
A que distância (em metros) um companheiro tem que estar de você para atender um chamado (segurando a tecla de comando) ou uma ordem de apontar e comandar. Companheiros com Comando desligado na roda ignoram os dois. 0 = sem limite.

**SuperRecallHoldTime** — Padrão: 2 · Faixa: 0.5 – 10  
Continue segurando a tecla de comando por esta quantidade de segundos para chamar todos os companheiros, a qualquer distância, inclusive os que estão com Comando desligado.

## Follow

Como os companheiros seguem você.

**MaxDistance** — Padrão: 10 · Faixa: 4 – 30  
A que distância um companheiro que segue você deixa você chegar antes de se mover para acompanhar. Ele escolhe o próprio lugar dentro desse raio (ao lado, atrás, às vezes à frente) e muda de lugar de vez em quando.

**IndoorDistance** — Padrão: 4 · Faixa: 2 – 10  
O mesmo raio enquanto você está num lugar fechado (sob um teto), para os companheiros ficarem no mesmo ambiente que você.

**Regroup** — Padrão: Ligado  
Quando você fica parado por RegroupTime segundos, os companheiros voltam e esperam perto de você. Desligado = eles ficam onde pararam.

**RegroupTime** — Padrão: 45 · Faixa: 5 – 600  
Segundos que você fica parado antes de os companheiros se reagruparem em volta de você (veja Regroup).

**SitIndoors** — Padrão: Ligado  
Quando você para num lugar fechado, os companheiros sentam perto de você: numa cadeira ou banco livre, se houver, senão no chão. Perto de casa, eles esvaziam as bolsas primeiro.

**TeleportDistance** — Padrão: 50 · Faixa: 20 – 200  
Um companheiro que segue você e fica esta quantidade de metros para trás aparece do seu lado: na hora, além de 1.5x essa distância, ou quando ele não consegue mais te ver e não está chegando mais perto.

**StuckTeleport** — Padrão: Desligado  
Seguindo você, um companheiro que ficou preso por 15 segundos num lugar de onde não consegue te ver aparece do seu lado. Desligado = ele continua tentando achar o próprio caminho.

**FormationSpacing** — Padrão: 3 · Faixa: 1 – 10  
Espaço (em metros) entre companheiros que andam lado a lado em formação (quando seguem algo que não é você).

**FormationCatchUp** — Padrão: 15 · Faixa: 5 – 50  
Em formação, um companheiro a esta quantidade de metros do lugar dele corre para alcançar.

## Movement

Como os companheiros se movem: velocidades, até onde enxergam e como contornam obstáculos (portas, degraus, água, pulos).

**WalkSpeed** — Padrão: 2 · Faixa: 0.5 – 5  
Velocidade de caminhada (um jogador anda a cerca de 2). Passa a valer depois que o mundo é recarregado.

**RunSpeed** — Padrão: 7 · Faixa: 2 – 15  
Velocidade de corrida (um jogador corre a cerca de 7). Passa a valer depois que o mundo é recarregado.

**SwimSpeed** — Padrão: 2 · Faixa: 0.5 – 5  
Velocidade de natação (um jogador nada a cerca de 2). Passa a valer depois que o mundo é recarregado.

**TurnSpeed** — Padrão: 300 · Faixa: 50 – 600  
Com que rapidez um companheiro gira (graus por segundo). Passa a valer depois que o mundo é recarregado.

**ViewRange** — Padrão: 20 · Faixa: 5 – 100  
A que distância (em metros) um companheiro consegue ver inimigos e outras criaturas. Passa a valer depois que o mundo é recarregado.

**ViewAngle** — Padrão: 120 · Faixa: 30 – 360  
O campo de visão de um companheiro (em graus). Passa a valer depois que o mundo é recarregado.

**Pathing** — Padrão: Ligado  
Os companheiros acham o caminho com o próprio mapa do mundo, onde portas não bloqueiam a passagem (eles as abrem ao chegar) e degraus de até 0,6 m são subíveis (eles pulam). Desligado = o mesmo mapa que as criaturas usam, onde toda porta fechada é parede e a maioria das escadas não se conecta. Passa a valer depois de reiniciar o jogo.

**WorkRunDistance** — Padrão: 25 · Faixa: 0 – 500  
Os companheiros andam enquanto trabalham (plantar, idas ao baú, fundição, cozinha, tarefas, consertos, coleta) e só correm quando o destino está mais longe do que esta quantidade de metros. 0 = nunca corre para trabalhar. Seguir você, lutar e caçar não são afetados.

**DryDetour** — Padrão: 30 · Faixa: 0 – 500  
Os companheiros preferem ficar secos, como um jogador: quando o caminho até algum lugar cruza água, eles dão a volta por terra, desde que o caminho seco não seja mais longo do que 1.5x o caminho nadando mais esta quantidade de metros. Senão, eles nadam.

**JobSwim** — Padrão: 20 · Faixa: 0 – 200  
O máximo de natação (em metros) que um trabalho aceita para chegar a alguma coisa: o saque de um abate, uma presa, um pássaro, um ponto de pesca. Mais longe do que isso, ele deixa pra lá.

**MaxDrop** — Padrão: 3 · Faixa: 1 – 4  
Quando uma linha reta é bem mais curta que o caminho de volta (descer a escada e voltar, contornar um muro), os companheiros a pegam como um jogador: pulam degraus, caem de beiradas de até esta quantidade de metros (jogadores não tomam dano de queda abaixo de 4 m) e pulam pequenos vãos (JumpGap).

**StuckDrop** — Padrão: 8 · Faixa: 3 – 20  
Quando um companheiro está preso acima de onde precisa estar e não há outro jeito de descer (do alto de uma escada, de uma plataforma, de uma beirada), ele pula uma queda de até esta quantidade de metros, como um jogador que prefere tomar um pouco de dano de queda a ficar parado. Uma escada para descer vem primeiro, e ele nunca aceita uma queda que custaria mais de 30% da vida dele naquele momento (companheiros tomam dano de queda como um jogador: nenhum até 4 m).

**JumpGap** — Padrão: 1.5 · Faixa: 0.5 – 3  
O vão mais largo (em metros) que um companheiro salta nesse tipo de atalho. 0.5 = nenhum vão.

**JumpWaterDepth** — Padrão: 0.3 · Faixa: 0.05 – 2  
Água com pelo menos esta profundidade (em metros) no caminho de um companheiro é saltada quando o trecho é estreito o bastante (JumpWaterWidth), como um jogador pula um riacho pequeno em vez de atravessá-lo correndo. Água rasa mais larga é atravessada a pé.

**JumpWaterWidth** — Padrão: 3 · Faixa: 1 – 4  
O trecho de água mais largo (em metros) que um companheiro salta.

**SwimStaminaShore** — Padrão: 0.25 · Faixa: 0 – 1  
Quando a stamina de um companheiro nadando cai abaixo desta fração (0.25 = 25%), ele vai para a margem mais próxima, seja o que for que estivesse fazendo.

## Combat

Quando os companheiros lutam e como escolhem arma e escudo.

**EngageOnNotice** — Padrão: Ligado  
Ligado: os companheiros atacam uma criatura assim que ela percebe você ou um companheiro. Desligado: eles esperam até ela vir de fato para cima de você (alerta). Qualquer coisa que os ataque é sempre revidada, e a Caça vai atrás da presa de qualquer jeito.

**HealthRetreatPct** — Padrão: 0.3 · Faixa: 0.05 – 0.9  
Abaixo desta fração de vida (0.3 = 30%) um companheiro luta na defensiva: pega o melhor escudo de bloqueio e não abre com tocha.

**FleeHealthPct** — Padrão: 0.15 · Faixa: 0.05 – 0.6  
Abaixo desta fração de vida (0.15 = 15%) um companheiro foge do inimigo.

**WeaponSwitchMargin** — Padrão: 0.1 · Faixa: 0 – 0.5  
Uma arma diferente precisa ser esta fração melhor contra o inimigo atual (0.1 = 10%) para um companheiro trocar para ela, para não ficar trocando de um lado para o outro.

**ShieldMultiAttackerCount** — Padrão: 3 · Faixa: 1 – 10  
Quando esta quantidade de inimigos ataca um companheiro ao mesmo tempo, ele troca para o escudo com o melhor bloqueio.

**BowSwitchDistance** — Padrão: 15 · Faixa: 5 – 60  
Um companheiro com arco ou besta troca para ele quando o alvo está mais longe do que isso (em metros).

**MeleeSwitchDistance** — Padrão: 12 · Faixa: 3 – 40  
Ele volta para a arma de corpo a corpo quando o alvo chega mais perto do que isso (em metros).

**UseTorchOpener** — Padrão: Ligado  
Contra inimigos fracos a fogo, abre com uma tocha para incendiá-los e depois volta para a melhor arma.

**TorchOpenerTimeout** — Padrão: 4 · Faixa: 1 – 10  
Segundos tentando incendiar um inimigo antes de desistir e voltar para a arma.

**TorchOpenerCooldown** — Padrão: 10 · Faixa: 0 – 60  
Segundos antes de o mesmo inimigo poder ser aberto com tocha de novo.

**TorchOpenerMinTargetHealth** — Padrão: 40 · Faixa: 0 – 500  
Sem abertura com tocha em inimigos com vida máxima menor que isso - não vale a pena.

## Food

Quando os companheiros bebem hidroméis.

**HealthMeadThreshold** — Padrão: 0.5 · Faixa: 0.1 – 0.9  
Um companheiro bebe um hidromel de cura da bolsa quando a vida cai abaixo desta fração (0.5 = 50%).

**StaminaMeadThreshold** — Padrão: 0.25 · Faixa: 0.05 – 0.9  
Ele bebe um hidromel de stamina quando a stamina cai abaixo desta fração (0.25 = 25%).

**MeadCooldown** — Padrão: 10 · Faixa: 1 – 60  
Segundos depois de beber um hidromel antes de beber outro.

## Stamina

Com que rapidez os companheiros gastam e recuperam stamina.

**RegenRate** — Padrão: 6 · Faixa: 1 – 30  
Stamina que um companheiro recupera por segundo quando não está usando nenhuma.

**RunDrainRate** — Padrão: 10 · Faixa: 1 – 30  
Stamina gasta por segundo correndo.

**SneakDrainRate** — Padrão: 5 · Faixa: 1 – 30  
Stamina gasta por segundo se esgueirando.

**SwimDrainRate** — Padrão: 10 · Faixa: 1 – 30  
Stamina gasta por segundo nadando.

**RegenDelay** — Padrão: 1 · Faixa: 0.1 – 5  
Segundos depois de usar stamina antes de ela começar a voltar.

## Rest

Dormir em camas, sentar perto do fogo e o efeito Descansado.

**SleepAtNight** — Padrão: Ligado  
À noite, um companheiro que esteja dentro de BedRange da cama reivindicada para o que está fazendo e vai dormir, inclusive quando está trabalhando. De manhã ele volta ao trabalho.

**SleepWhenPlayerSleeps** — Padrão: Ligado  
Quando você entra numa cama, os seus companheiros dentro de BedRange das camas reivindicadas vão dormir também, mesmo que ainda não seja noite. O pulo da noite espera até 30 segundos por eles.

**BedRange** — Padrão: 50 · Faixa: 10 – 200  
A que distância (em metros) um companheiro tem que estar da cama reivindicada para que as duas opções acima o mandem dormir.

**HealRate** — Padrão: 2 · Faixa: 0.5 – 20  
Vida que um companheiro recupera por segundo enquanto senta perto de um fogo ou dorme.

**PlayerRange** — Padrão: 5 · Faixa: 2 – 20  
Quando você senta perto de um fogo (a emoção de sentar), companheiros sem trabalho dentro desta quantidade de metros de você sentam ali também.

**FireRange** — Padrão: 5 · Faixa: 2 – 20  
A que distância (em metros) um fogo tem que estar para um companheiro sentar ao lado dele.

**WarmupTime** — Padrão: 20 · Faixa: 5 – 120  
Segundos sentado perto de um fogo antes de um companheiro ficar Descansado, como um jogador.

**DirectedTimeout** — Padrão: 300 · Faixa: 30 – 1800  
Quando você manda um companheiro sentar perto de um fogo, ele se levanta depois desta quantidade de segundos.

## Death

O que acontece quando um companheiro morre e como ele volta.

**AutoRespawn** — Padrão: Ligado  
Um companheiro morto volta sozinho depois de RespawnDelay segundos, se ninguém o trouxer de volta da lápide antes.

**RespawnDelay** — Padrão: 300 · Faixa: 30 – 3600  
Segundos depois da morte antes de um companheiro voltar sozinho.

**RespawnAtWorldSpawn** — Padrão: Desligado  
Um companheiro que volta sozinho e não tem cama reivindicada aparece nas pedras do ponto inicial do mundo. Desligado = do seu lado.

**DeathItemRule** — Padrão: 0 · Faixa: 0 – 3  
O que um companheiro perde quando morre. 0 = a regra de penalidade de morte do mundo, igual à de um jogador. 1 = mantém tudo. 2 = mantém o equipamento vestido, o resto vai para a lápide. 3 = tudo vai para a lápide. A opção do dono é usada mesmo que a morte seja processada pelo jogo de outro jogador.

**TombstoneRecoveryRange** — Padrão: 50 · Faixa: 5 – 100  
Um companheiro anda até a própria lápide que sobrou e a esvazia quando está dentro desta quantidade de metros dela.

**ShrineBuildable** — Padrão: Ligado  
O Santuário de Odin pode ser construído com o martelo (aba Diversos, precisa de uma bancada). Use-o para ver os seus companheiros e trazer de volta um perdido. Passa a valer no próximo carregamento do mundo; santuários já construídos continuam funcionando.

**ShrineRecipe** — Padrão: `Stone:10,Wood:5,GreydwarfEye:3`  
Quanto custa construir o Santuário de Odin, em pares Item:Quantidade separados por vírgulas (nomes de item como no comando spawn do jogo). Passa a valer no próximo carregamento do mundo.

**ShrineModel** — Padrão: `Tombstone`  
A aparência do Santuário de Odin: Tombstone (a lápide de um jogador) ou Ward (a pedra de área protegida). Passa a valer no próximo carregamento do mundo, também para santuários já construídos.

**SkillLoss** — Padrão: 0.25 · Faixa: 0 – 1  
Fração das habilidades que um companheiro perde quando morre (0.25 = 25%, 0 = nenhuma).

## Looting

Recolher o que um abate deixou no chão.

**PickUpKills** — Padrão: Ligado  
Depois que um companheiro mata alguma coisa, ele vai até lá e recolhe o que a criatura deixou (coisas que você mesmo pegaria com o auto-pickup), como um jogador faria. Precisa de Recolher ligado na roda.

**Radius** — Padrão: 6 · Faixa: 2 – 20  
A que distância em volta do abate (em metros) os drops são procurados.

**WaitSeconds** — Padrão: 10 · Faixa: 2 – 60  
Tempo máximo de espera (em segundos) ao lado do corpo para os drops saírem.

**MaxDistance** — Padrão: 30 · Faixa: 5 – 100  
Abates mais longe do que isso (em metros), ou dos quais você se afastou esta distância enquanto ele te segue, ficam para trás.

**CollectRadius** — Padrão: 10 · Faixa: 1 – 30  
Com a ordem "recolhe os itens do chão", a que distância (em metros) do companheiro ele procura coisas soltas no chão - inclusive o que você ou ele largou de propósito. Ele anda até cada item e pega um por um, até não sobrar nada no raio, a mochila encher ou a ordem passar de 30 segundos.

**AutoPickupRange** — Padrão: 2 · Faixa: 0.5 – 10  
Com Recolher ligado na roda, um companheiro pega itens dentro desta quantidade de metros dele enquanto anda, como o auto-pickup de um jogador.

## Gear Repair

Consertar as próprias armas, armaduras e ferramentas do companheiro numa bancada.

**DurabilityThreshold** — Padrão: 0.5 · Faixa: 0.1 – 0.9  
Um companheiro leva o equipamento até uma bancada para consertar quando algo cai abaixo desta fração de durabilidade (0.5 = 50%).

**HomeThreshold** — Padrão: 0.8 · Faixa: 0.1 – 1  
Em casa, onde uma bancada está perto, o equipamento é consertado um pouco antes: abaixo desta fração (0.8 = 80%). 1 = a qualquer desgaste. Longe de casa vale o DurabilityThreshold.

**NoStationWait** — Padrão: 600 · Faixa: 30 – 3600  
Quando um item gasto não pode ser consertado em nenhuma bancada ao alcance (digamos que precise de uma forja negra e não haja nenhuma por perto), ou o caminho até a bancada falhou, esse item fica de lado por esta quantidade de segundos antes de ele procurar de novo.

**StationRange** — Padrão: 20 · Faixa: 5 – 60  
A que distância (em metros) um companheiro procura uma bancada para consertar. Perto de casa, qualquer bancada perto de casa conta.

## Gathering

Os trabalhos de Madeira, Pedra, Minério e Forrageio.

**Range** — Padrão: 60 · Faixa: 10 – 150  
Madeira, Pedra, Minério e Forrageio com Seguir desligado: a que distância (em metros) de onde ele está trabalhando ele vai atrás de árvores, rochas e plantas - em volta de casa quando o trabalho começou perto dela, senão em volta de onde foi iniciado. Ele continua trabalhando ali quando você vai embora. (Fase 3.3c: 60, era 40.)

**FollowRange** — Padrão: 25 · Faixa: 5 – 60  
Madeira, Pedra e Minério com Seguir ligado: uma árvore ou rocha tem que estar dentro desta quantidade de metros de você. Sem nada tão perto, o companheiro só segue você. (Com Seguir desligado veja Range; o Forrageio tem ForageFollowRange.) Fase 3.3c: 25, era 10 - ele trabalha a até 25 m de você em vez de ficar colado em você, e só abandona um alvo quando ele passa de 15 m além disso.

**ForageFollowRange** — Padrão: 20 · Faixa: 3 – 60  
Forrageio com Seguir ligado: uma planta tem que estar dentro desta quantidade de metros de você - as frutinhas que você passar por perto, não tudo em volta. Sem nada tão perto, o companheiro só segue você. (Com Seguir desligado veja Range.) Fase 3.3c: 20, era 10 - ainda é curto o bastante para o Forrageio continuar sendo "o que você passou por perto", e não uma varredura de toda a área.

**PickUpLoose** — Padrão: Ligado  
Trabalhos de Madeira e Pedra, só no início do jogo: também pegar à mão o que está no chão - galhos (trabalho de Madeira), pedras soltas e sílex (trabalho de Pedra) - desde que estejam em GatherWood / GatherStone e abaixo da quantidade deles. Início do jogo = não há machado de bronze ou melhor na mochila do companheiro, na sua mochila ou nos baús perto de casa, e não há forja em casa para fazer um. Sem ferramenta para o trabalho (sem machado para Madeira, sem picareta para Pedra), pegar as coisas do chão é tudo o que ele faz.

**WoodLimit** — Padrão: 500 · Faixa: 0 – 100000  
Quantos de cada material de madeira (contando a mochila do companheiro e os baús perto da casa dele) o trabalho de Madeira coleta antes de parar. 0 = sem limite. Números por item em GatherWood têm prioridade sobre este.

**StoneLimit** — Padrão: 500 · Faixa: 0 – 100000  
Igual ao WoodLimit, para o trabalho de Pedra e para GatherStone.

**OreLimit** — Padrão: 500 · Faixa: 0 – 100000  
Igual ao WoodLimit, para o trabalho de Minério e para GatherOre.

**GatherWood** — Padrão: veja abaixo  
O que o trabalho de Madeira coleta. Alvos só são trabalhados se derrubarem algo desta lista que ainda esteja abaixo da quantidade; drops fora da lista ficam no chão. Um nome sem número usa o WoodLimit. Formato da lista (igual para todas as listas): * = tudo, Nome = só esse, -Nome = nunca esse, Nome:Número = esse com a sua própria quantidade (0 = sem limite). Os nomes são nomes de prefab ou do jogo.

Valor padrão:

```
Wood, FineWood, RoundLog, ElderBark, YggdrasilWood, Blackwood, Resin
```

**GatherStone** — Padrão: `Stone, Flint, BlackMarble, Grausten`  
O que o trabalho de Pedra coleta. Alvos só são trabalhados se derrubarem algo desta lista que ainda esteja abaixo da quantidade; drops fora da lista ficam no chão. Um nome sem número usa o StoneLimit. Formato da lista (igual para todas as listas): * = tudo, Nome = só esse, -Nome = nunca esse, Nome:Número = esse com a sua própria quantidade (0 = sem limite). Os nomes são nomes de prefab ou do jogo.

**GatherOre** — Padrão: veja abaixo  
O que o trabalho de Minério coleta. Alvos só são trabalhados se derrubarem algo desta lista que ainda esteja abaixo da quantidade; drops fora da lista ficam no chão. Um nome sem número usa o OreLimit. Formato da lista (igual para todas as listas): * = tudo, Nome = só esse, -Nome = nunca esse, Nome:Número = esse com a sua própria quantidade (0 = sem limite). Os nomes são nomes de prefab ou do jogo.

Valor padrão:

```
CopperOre, TinOre, IronScrap, SilverOre, BlackMetalScrap, CopperScrap, FlametalOreNew, Obsidian
```

**ForageItems** — Padrão: veja abaixo  
O que o trabalho de Forrageio pega. Nome:Número para de pegar aquele item quando houver essa quantidade (mochila + baús perto de casa). Formato da lista (igual para todas as listas): * = tudo, Nome = só esse, -Nome = nunca esse, Nome:Número = esse com a sua própria quantidade (0 = sem limite). Os nomes são nomes de prefab ou do jogo.

Valor padrão:

```
Raspberry,Blueberries,Cloudberry,Mushroom,MushroomYellow,MushroomBlue,MushroomMagecap,MushroomJotunPuffs,MushroomSmokePuff,Thistle,Dandelion,Carrot,Turnip,Onion,Barley,Flax,RoyalJelly,Honey
```

**WoodSkipTrees** — Padrão: `Oak`  
Árvores de pé que o trabalho de Madeira nunca derruba, comparadas por parte do nome da árvore (Oak, Birch, Beech, Pine, Fir, SwampTree, YggaShoot...). Toras e tocos ainda são limpos.

**BaseProtectRadius** — Padrão: 10 · Faixa: 0 – 50  
O trabalho de Madeira não derruba uma árvore de pé que tenha qualquer coisa construída por jogadores dentro desta quantidade de metros do tronco. 0 = desligado.

**JunkCleanup** — Padrão: Ligado  
Quando a mochila de um companheiro de coleta está cheia e ele não tem baú de casa para descarregar, ele joga fora tranqueiras (materiais e troféus que não estão em nenhuma lista de coleta) para abrir espaço. Equipamento, ferramentas, munição, comida, poções, sementes e qualquer coisa em NeverDiscard são sempre mantidos.

**NeverDiscard** — Padrão: `Trophy`  
Itens que a limpeza de tranqueiras nunca joga fora, comparados por parte do nome do item.

**OverweightThreshold** — Padrão: 298 · Faixa: 50 – 500  
O peso (valor BASE) em que um companheiro de coleta - madeira, pedra, minério, forragem e a roça da horta - para de pegar coisas e volta para casa para descarregar. Itens que aumentam a capacidade de carga somam por cima deste valor, como no MaxCarryWeight: com o Megingjord na mochila (+150) e este valor em 298, ele só volta para casa perto de 448. Caça e pesca não usam este limite (elas têm as próprias opções de mochila cheia).

## Hunting

O trabalho de Caça.

**Prey** — Padrão: `*`  
O que o trabalho de Caça persegue: criaturas que derrubam carne (e pássaros, veja Birds). Nome:Número com uma criatura ou uma carne dá àquela carne o próprio alvo em vez de MeatTarget / FeatherTarget. Animais domados, chefes e companheiros nunca são caçados. Os nomes que você pode usar estão listados em OfflineCompanionsReborn.Hunting.txt na pasta de configuração (escrito na inicialização). Formato da lista (igual para todas as listas): * = tudo, Nome = só esse, -Nome = nunca esse, Nome:Número = esse com a sua própria quantidade (0 = sem limite). Os nomes são nomes de item/criatura (prefab ou do jogo).

**MeatTarget** — Padrão: 100 · Faixa: 0 – 10000  
Caçar uma criatura só enquanto uma das carnes que ela derruba estiver abaixo desta quantidade (contando a mochila do companheiro e os baús perto da casa dele). Couro, peles e outros drops são pegos de brinde. 0 = sem limite.

**Range** — Padrão: 40 · Faixa: 10 – 200  
Com Seguir desligado: a que distância (em metros) de onde está caçando (casa, se estiver perto dela, senão de onde o trabalho foi iniciado) um companheiro procura presas.

**FollowRange** — Padrão: 40 · Faixa: 5 – 100  
Com Seguir ligado: a presa tem que estar dentro desta quantidade de metros de você. Sem nada tão perto, o companheiro só segue você.

**ChaseExtra** — Padrão: 15 · Faixa: 0 – 100  
Quanto além de Range / FollowRange um companheiro persegue uma presa que foge antes de deixá-la ir.

**BagFull** — Padrão: 0.9 · Faixa: 0.3 – 1  
Com Seguir desligado e casa definida: quando a mochila está assim cheia (por peso, ou por falta de espaços livres) o caçador vai para casa e guarda a caça, depois volta.

**Birds** — Padrão: Ligado  
Também atirar em pássaros (corvos, gaivotas, pássaros de mods) pelas penas: quando um pousa ao alcance, o caçador chega de mansinho e atira com arco ou besta (só se tiver um com munição), depois pega os drops. Pássaros excluídos na lista Prey (-Crow, -Seagal) ficam em paz.

**FeatherTarget** — Padrão: 100 · Faixa: 0 – 10000  
Atirar em pássaros só enquanto o que eles derrubam (penas) estiver abaixo desta quantidade, contando a mochila e os baús perto de casa. 0 = sem limite.

**Sneak** — Padrão: Ligado  
Chegar de mansinho em bichos ariscos (cervos, lebres...) que ainda não perceberam ninguém, como um jogador: agachado e sem correr, para não espantá-los. Companheiros agachados são mais silenciosos e mais difíceis de notar, como um jogador se esgueirando (luz e habilidade de Furtividade contam).

**ThrowSpear** — Padrão: Ligado  
Um caçador com lança na mão a arremessa em bichos ariscos (cervos, lebres...) de longe, como um jogador, depois anda até lá e pega a lança de volta. Só quando ele tem outra arma na mochila para continuar a luta.

**ThrowRange** — Padrão: 12 · Faixa: 4 – 25  
A que distância (em metros) um caçador arremessa a lança. Arremessos mais longos erram mais.

**ThrowStandoff** — Padrão: 10 · Faixa: 3 – 25  
Com uma lança para arremessar em bichos ariscos, um caçador chega só até esta distância (em metros), para e arremessa, em vez de andar até lá para esfaquear. Nunca mais que ThrowRange menos 1 m.

**SneakFrom** — Padrão: 20 · Faixa: 5 – 60  
Mais longe do que isso (em metros) de uma presa que ainda não o notou, um caçador anda (sem correr) em vez de se esgueirar, como um jogador - se esgueirar o caminho todo gasta muita stamina. Mais perto, ele se agacha e se esgueira.

**SwitchCloserBy** — Padrão: 5 · Faixa: 0 – 50  
Antes do primeiro golpe, um caçador troca para outro animal que esteja pelo menos esta quantidade de metros mais perto que a presa atual (e veja SwitchShare). 0 = nunca troca.

**SwitchShare** — Padrão: 0.6 · Faixa: 0.1 – 1  
...e que esteja no máximo nesta fração da distância até a presa (0.6 = 60%).

**SneakRestStamina** — Padrão: 0.3 · Faixa: 0 – 0.9  
Esgueirar-se gasta stamina, como para um jogador (ficar parado não gasta). Quando um caçador se aproximando da presa cai abaixo desta fração de stamina, ele para onde está, continua agachado e recupera o fôlego. 0 = nunca para.

**SneakResumeStamina** — Padrão: 0.6 · Faixa: 0.1 – 1  
Um caçador descansando enquanto se esgueira continua quando a stamina volta a ficar acima desta fração.

**ChaseRunStamina** — Padrão: 0.3 · Faixa: 0 – 1  
Perseguindo uma presa que disparou, um caçador só corre enquanto a stamina estiver acima desta fração; abaixo disso, ele trota e deixa a stamina voltar, como um jogador. 0 = corre sempre.

## Fishing

O trabalho de Pesca.

**ScanRadius** — Padrão: 30 · Faixa: 5 – 100  
A que distância (em metros) de onde o trabalho de Pesca foi iniciado o companheiro procura peixes para pescar. Ele fica por ali e pesca a água por perto.

**Fish** — Padrão: `*`  
Quais peixes pescar. Nome:Número dá àquele peixe o próprio alvo em vez de FishTarget. Os nomes dos peixes estão listados em OfflineCompanionsReborn.Fishing.txt na pasta de configuração (escrito na inicialização). Formato da lista (igual para todas as listas): * = tudo, Nome = só esse, -Nome = nunca esse, Nome:Número = esse com a sua própria quantidade (0 = sem limite).

**FishTarget** — Padrão: 100 · Faixa: 0 – 10000  
Pescar um tipo de peixe só enquanto houver menos que esta quantidade (mochila + baús perto de casa). 0 = sem limite.

**CastWait** — Padrão: 60 · Faixa: 10 – 600  
Segundos de espera por uma fisgada antes de recolher e tentar de novo (um peixe que escapou, o lugar errado).

**BagFull** — Padrão: 0.9 · Faixa: 0.3 – 1  
Perto de casa: quando a mochila está assim cheia (por peso, ou por falta de espaços livres) o companheiro vai para casa e guarda a pesca, depois volta a pescar.

## Farming

O trabalho de Fazenda.

**Range** — Padrão: 30 · Faixa: 5 – 100  
A que distância (em metros) em volta de si um companheiro de fazenda procura plantações para colher, zonas de fazenda para plantar e baús.

**PlantSpacing** — Padrão: 0.75 · Faixa: 0.25 – 3  
O menor espaço (em metros) entre plantas. Uma planta que precise de mais espaço para crescer fica com esse espaço.

**SeedMinimum** — Padrão: 20 · Faixa: 0 – 1000  
O mínimo de cada item plantável (sementes de cenoura, nabo, cebola, cevada, linho e por aí) que o fazendeiro mantém, contando a mochila e os baús perto da casa dele. Ele nunca planta sementes se isso levar o estoque abaixo disso. Cevada, linho e outras culturas que crescem de si mesmas ainda são plantadas, e o fazendeiro mantém essa quantidade na mochila em vez de guardar nos baús.

**SeedTarget** — Padrão: 100 · Faixa: 0 – 5000  
Enquanto uma semente estiver abaixo deste número, o fazendeiro cultiva plantas de semente (plantando cenoura, nabo, cebola...) junto das culturas normais para repor o estoque. Abaixo do SeedMinimum ele só cultiva plantas de semente daquela cultura. Neste número ou acima, só culturas.

**SeedCarry** — Padrão: 40 · Faixa: 1 – 200  
Quantos de cada item plantável o fazendeiro carrega. Quando abre um baú para pegar sementes, ele leva até esta quantidade de cada tipo da lista PlantAny (e vegetais para plantas de semente enquanto aquela semente estiver abaixo do SeedTarget), para poder encher os campos sem voltar para cada tipo. Ele mantém até esta quantidade na mochila em vez de guardar nos baús.

**PlantGroupSize** — Padrão: 4 · Faixa: 1 – 100  
Quantos de uma mesma planta o fazendeiro coloca antes de trocar para o próximo tipo, para um campo misto ficar em blocos pequenos em vez de uma cultura só. 1 = totalmente misturado.

**NextCropRange** — Padrão: 3 · Faixa: 0 – 10  
Depois de colher uma planta, o fazendeiro vai direto para a próxima planta madura dentro desta quantidade de metros, como quem trabalha uma fileira, em vez de parar e escanear o campo inteiro. 0 = sempre para e escaneia.

**PlantAny** — Padrão: veja abaixo  
O que o fazendeiro planta nas zonas de fazenda marcadas como Qualquer Cultura. * = toda cultura que cresce em terra cultivada (mudas de árvore nunca são plantadas numa zona Qualquer, a não ser que sejam nomeadas). Nome:Número para de plantar aquela cultura quando houver essa quantidade do que ela dá (cenouras para CarrotSeeds, cevada para Barley...), contando a mochila e os baús perto de casa. Itens adicionados por outros mods também funcionam. Formato da lista (igual para todas as listas): * = tudo, Nome = só esse, -Nome = nunca esse, Nome:Número = esse com a sua própria quantidade (0 = sem limite). Os nomes são nomes de item/criatura (prefab ou do jogo).

Valor padrão:

```
CarrotSeeds, TurnipSeeds, OnionSeeds, Barley, Flax, MushroomJotunPuffs, MushroomMagecap, VineberrySeeds
```

## Smelting

O trabalho de Fundição.

**Range** — Padrão: 25 · Faixa: 5 – 100  
A que distância (em metros) em volta de si um companheiro de fundição procura fornalhas, fornos de carvão, fornos e baús. Perto de casa ele também usa os baús em volta de casa ([General] HomeTripRange).

**MaxCarryOre** — Padrão: 20 · Faixa: 1 – 100  
O máximo de minério (ou outro insumo) que um companheiro leva a uma fornalha numa viagem.

**MaxCarryFuel** — Padrão: 40 · Faixa: 1 – 100  
O máximo de combustível (carvão, madeira) que um companheiro leva a uma fornalha numa viagem.

**UsePiles** — Padrão: Ligado  
Quando os baús não têm o que uma fornalha ou um forno de carvão precisa (madeira para o forno, combustível...), o trabalho de Fundição desmonta uma pilha disso (Pilha de Madeira...) com um martelo, como um jogador, e usa. Precisa de um martelo na mochila.

## Cooking

O trabalho de Cozinha.

**Range** — Padrão: 25 · Faixa: 5 – 60  
A que distância em volta de si (em metros) um companheiro de cozinha procura estações de cozinha, fermentadores e baús. Perto de casa ele também usa os baús em volta de casa ([General] HomeTripRange).

**IngredientReserve** — Padrão: 20 · Faixa: 0 – 1000  
Um ingrediente cru só é usado enquanto os baús ao alcance (mais a bolsa do companheiro) tiverem mais que esta quantidade, para sempre sobrar algo para você cozinhar.

**FoodTarget** — Padrão: 20 · Faixa: 1 – 1000  
Cozinhar uma comida até os baús ao alcance terem esta quantidade dela.

**BatchSize** — Padrão: 5 · Faixa: 1 – 50  
Quantas de uma comida (ou base de hidromel) fazer de uma vez, como um jogador cozinhando uma fornada. Ainda para no FoodTarget / MeadBaseTarget e nunca entra na reserva de ingredientes. Numa estação de cozinha ou forno também é limitado pelos espaços livres.

**Foods** — Padrão: `*`  
O que pode ser cozinhado. Nome:Número é o alvo próprio daquela comida em vez do FoodTarget. Os nomes que você pode usar estão listados em OfflineCompanionsReborn.Cooking.txt na pasta de configuração (escrito na inicialização). Formato da lista (igual para todas as listas): * = tudo, Nome = só esse, -Nome = nunca esse, Nome:Número = esse com a sua própria quantidade (0 = sem limite). Os nomes são nomes de item/criatura (prefab ou do jogo).

**StoreWithCookedFood** — Padrão: Ligado  
Guardar comida cozida num baú que já tem aquela comida; se não houver, num que tenha outras comidas cozidas; senão, no baú mais próximo da área de cozinha. Desligado = o baú mais próximo com espaço.

**MakeMead** — Padrão: Ligado  
Preparar hidromel: fazer bases de hidromel e enchê-las nos fermentadores. Desligado = só comida (hidromel que já terminou num fermentador ainda é tirado).

**MeadBaseTarget** — Padrão: 10 · Faixa: 0 – 1000  
Preparar uma base de hidromel (uma em MeadFocus) até os baús ao alcance terem esta quantidade dela.

**MeadTarget** — Padrão: 20 · Faixa: 0 – 1000  
Manter os fermentadores abastecidos com uma base de hidromel até os baús ao alcance terem esta quantidade do hidromel pronto.

**MeadFocus** — Padrão: veja abaixo  
Bases de hidromel para preparar PRIMEIRO, pelo último chefe derrotado, para se preparar para o próximo (as outras bases que você conhece ainda são preparadas depois da comida, dentro de MeadBaseTarget / MeadTarget). Formato: chefe: Base, Base; chefe: ... na ordem em que você os venceu. Um chefe é o nome dele ou parte dele (eikthyr, gdking = The Elder, bonemass, dragon = Moder, goblinking = Yagluth, queen, fader, kall = Kall Fimbulbringer, ou um chefe de mod); start = nenhum vencido ainda. Os chefes que o seu jogo tem, e os nomes das bases, estão listados em OfflineCompanionsReborn.Cooking.txt na pasta de configuração.

Valor padrão:

```
start: MeadBaseHealthMinor, MeadBaseStaminaMinor;
eikthyr: MeadBaseHealthMinor, MeadBaseStaminaMinor, MeadBasePoisonResist;
gdking: MeadBasePoisonResist, MeadBaseHealthMedium, MeadBaseStaminaMinor;
bonemass: MeadBaseFrostResist, MeadBaseHealthMedium, MeadBaseStaminaMedium;
dragon: MeadBaseHealthMedium, MeadBaseStaminaMedium, MeadBaseTasty, BarleyWineBase;
goblinking: MeadBaseHealthMajor, MeadBaseStaminaMedium, MeadBaseEitrMinor, MeadBaseLightfoot;
queen: BarleyWineBase, MeadBaseHealthMajor, MeadBaseStaminaLingering, MeadBaseHealthLingering, MeadBaseEitrMinor;
fader: MeadBaseFrostResist, MeadBaseHealthMajor, MeadBaseHealthLingering, MeadBaseStaminaLingering, MeadBaseEitrMinor;
kall: MeadBaseFrostResist, MeadBaseHealthMajor, MeadBaseHealthLingering, MeadBaseStaminaLingering, MeadBaseEitrMinor
```

**FuelLevel** — Padrão: 0.5 · Faixa: 0.1 – 1  
Trabalho de Cozinha: mantém os fogos embaixo dos pontos de cozinha que usa (espeto, estação de ferro, caldeirão) e o forno nesta fração de cheio (0.5 = 50%). Quando um cai abaixo da metade disso, ele reabastece. Só o suficiente para cozinhar - o Reparar e Reabastecer enche mais os fogos, se estiver ligado.

**UsePiles** — Padrão: Ligado  
Trabalho de Cozinha: sem combustível nos baús, desmonta uma pilha disso (Pilha de Madeira...) com um martelo, como um jogador. Precisa de um martelo na mochila.

**UseCauldron** — Padrão: Ligado  
Trabalho de Cozinha: usar o caldeirão (sopas, ensopados e outras comidas fabricadas).

**UsePrepTable** — Padrão: Ligado  
Trabalho de Cozinha: usar a mesa de preparo (massa, tortas cruas e coisas do tipo).

**UseMeadKetill** — Padrão: Ligado  
Trabalho de Cozinha: usar o alambique de hidromel (bases de hidromel).

**UseCookingStation** — Padrão: Ligado  
Trabalho de Cozinha: usar a estação de cozinha / espeto sobre o fogo.

**UseIronCookingStation** — Padrão: Ligado  
Trabalho de Cozinha: usar a estação de cozinha de ferro.

**UseOven** — Padrão: Ligado  
Trabalho de Cozinha: usar o forno de pedra (pão, tortas; ele também recebe madeira).

**UseFermenter** — Padrão: Ligado  
Trabalho de Cozinha: usar fermentadores (enchendo-os com bases de hidromel e tirando o hidromel).

**UseBeehives** — Padrão: Ligado  
Trabalho de Cozinha: usar colmeias (recolhendo mel).

**UseOther** — Padrão: Ligado  
Trabalho de Cozinha: usar qualquer outra estação de cozinha, por exemplo uma adicionada por outro mod.

## Repair and Restock

O trabalho de Reparar e Reabastecer (cuidar da sua base).

**Repair** — Padrão: Ligado  
Reparar e Reabastecer: consertar peças de construção danificadas perto de casa com um martelo, como um jogador (um golpe por peça; a bancada da peça tem que estar ao alcance e uma área protegida tem que permitir; gasta stamina e desgasta o martelo). Um martelo é buscado num baú se ele não tiver nenhum.

**RepairBelow** — Padrão: 0.75 · Faixa: 0.1 – 1  
Consertar peças abaixo desta fração de vida (0.75 = 75% ou menos; 1 = qualquer dano). Cada conserto é um golpe que deixa a peça como nova, como o de um jogador.

**Reach** — Padrão: 4.5 · Faixa: 2 – 6  
A que distância (em metros, dos olhos) o companheiro alcança uma peça para consertá-la. Um jogador alcança cerca de 5 m.

**Restock** — Padrão: Ligado  
Reparar e Reabastecer: manter fogos, tochas e braseiros perto de casa abastecidos, um pedaço de combustível por vez como um jogador. O combustível vem da bolsa ou é buscado num baú; o que sobra volta para um baú. As opções Refuel... abaixo escolhem os tipos.

**FuelBelow** — Padrão: 0.8 · Faixa: 0.1 – 1  
Reabastecer um fogo quando o combustível dele estiver abaixo desta fração de cheio (0.8 = 80%). Ele é enchido até o topo.

**RefuelFires** — Padrão: Ligado  
Reabastecer: fogueiras, fogos de chão e fogueiras grandes.

**RefuelHearths** — Padrão: Ligado  
Reabastecer: lareiras.

**RefuelBraziers** — Padrão: Ligado  
Reabastecer: braseiros (de pé e pendurados).

**RefuelStandingTorches** — Padrão: Ligado  
Reabastecer: tochas de pé (madeira, ferro, verde, azul, névoa).

**RefuelWallTorches** — Padrão: Ligado  
Reabastecer: arandelas e tochas de parede.

**RefuelHotTubs** — Padrão: Ligado  
Reabastecer: banheiras quentes.

**RefuelOther** — Padrão: Ligado  
Reabastecer: qualquer outra coisa que queime combustível e não seja dos tipos acima (lanternas, a abóbora-lanterna, coisas adicionadas por outros mods).

**Honey** — Padrão: Ligado  
Reparar e Reabastecer: esvaziar as colmeias da sua área e guardar o mel no baú que já tem mais mel.

**HoneyAt** — Padrão: 0.5 · Faixa: 0.1 – 1  
Esvaziar uma colmeia quando ela estiver pelo menos nesta fração cheia (0.5 = metade, 1 = só quando cheia).

**Sort** — Padrão: Ligado  
Reparar e Reabastecer: organizar os baús da sua área (em volta de casa, ou de onde o trabalho foi iniciado), igual com igual: cada tipo de item é levado para o baú que já tem mais dele, e pilhas parciais são juntadas.

**SortItems** — Padrão: `*`  
Quais itens são organizados. Separados por vírgulas: * = tudo, Nome = só esse, -Nome = nunca esse (ex.: *, -Coins, -Wood deixa moedas e madeira onde estão). Nomes de prefab ou do jogo.

**SortGear** — Padrão: Desligado  
Organizar também as coisas que não empilham (armas, armaduras, ferramentas). Desligado: só itens empilháveis são movidos.

**Piles** — Padrão: Ligado  
Reparar e Reabastecer: empilhar madeira, pedra e coisas do tipo dos baús em pilhas (a Pilha de Madeira, Pilha de Pedra, Pilha de Madeira Fina... do jogo), construídas com um martelo como um jogador. Só ao lado de pilhas do mesmo tipo que você já construiu - ele nunca começa uma pilha nova sozinho.

**PileKeep** — Padrão: 100 · Faixa: 0 – 10000  
Quantos de cada material ficam nos baús para construção: o que passa disso vai para pilhas, e quando os baús caem abaixo desse número uma pilha é desmontada (com o martelo, como um jogador) e o material volta para os baús.

**PileItems** — Padrão: `*`  
Quais materiais viram pilhas. Separados por vírgulas: * = todo material do qual você tem pilhas, Nome = só esse, -Nome = nunca esse, Nome:Número = mantenha esta quantidade dele nos baús em vez do PileKeep. Nomes de prefab ou do jogo (Wood, Stone, FineWood, RoundLog...).

**AwayRadius** — Padrão: 30 · Faixa: 5 – 100  
Iniciado longe de casa (ou sem casa definida): a que distância (em metros) de onde o trabalho começou ele conserta, reabastece e organiza. Iniciado perto de casa, o alcance é [General] HomeTripRange em volta de casa.

**WorkTime** — Padrão: 1 · Faixa: 0.3 – 5  
Segundos por ação: um golpe de conserto, abrir um baú. Colocar combustível leva um pouco mais da metade disso por pedaço. (O jogo não conserta a mesma peça mais de uma vez por segundo.)

**GiveUp** — Padrão: 90 · Faixa: 20 – 600  
Segundos que ele continua tentando chegar ao alcance de uma peça, fogo ou baú antes de deixar isso de lado por alguns minutos.

**OrderWait** — Padrão: 30 · Faixa: 0 – 600  
Depois que você dá uma ordem ao companheiro (mandá-lo a algum lugar, sentar perto de um fogo...), ele espera esta quantidade de segundos quando a ordem termina antes de voltar às tarefas dele.

## Idle

O que um companheiro faz quando o trabalho dele não tem nada para fazer.

**Enabled** — Padrão: Ligado  
Quando o trabalho dele fica um tempo sem nada para fazer (e ele não está seguindo você), o companheiro fica à toa perto do trabalho em vez de ficar parado: pequenas tarefas, sentar, dar uma volta, conversar. Ele para no instante em que o trabalho tem serviço de novo.

**StartAfter** — Padrão: 10 · Faixa: 1 – 300  
Segundos sem nada para fazer antes de começar a ficar à toa.

**Radius** — Padrão: 8 · Faixa: 2 – 40  
A que distância (em metros) de onde o trabalho o deixou um companheiro anda, senta ou procura companhia enquanto está à toa.

**Chores** — Padrão: Ligado  
Enquanto estiver à toa, fazer pequenas tarefas por perto: consertar peças danificadas, reabastecer fogos, recolher mel, guardar o que carrega (usa as configurações de Reparar e Reabastecer).

**ChoreRadius** — Padrão: 15 · Faixa: 2 – 60  
A que distância (em metros) ele procura tarefas de ócio.

**Sit** — Padrão: Ligado  
Enquanto estiver à toa, sentar um pouco - numa cadeira ou banco livre por perto, senão no chão.

**Wander** — Padrão: Ligado  
Enquanto estiver à toa, passear por perto e dar uma olhada nas estações e baús.

**Socialize** — Padrão: Ligado  
Enquanto estiver à toa, ir até você ou outro companheiro por perto e conversar de vez em quando.

**SpeechInterval** — Padrão: 300 · Faixa: 0 – 3600  
No máximo a cada esta quantidade de segundos ele diz por que está à toa (e conversa enquanto socializa mais ou menos com metade dessa frequência). 0 = nunca.

## Speech

Com que frequência os companheiros falam e quanto tempo as falas ficam na tela.

**Chatter** — Padrão: Desligado  
Os companheiros falam sozinhos de vez em quando quando nada está acontecendo: uma linha sobre uma luta por perto, estar com peso demais, estar com fome, equipamento gasto, falta de cama, o trabalho em que estão, ou só seguir você. Desligado (Fase 3.3c) = nenhuma fala aleatória. Eles continuam falando quando algo de fato acontece: uma ordem (chat ou voz), uma mudança de modo ("Deixa comigo!"), as respostas da IA, um peixe pescado, um marco de fazenda/conserto/fundição, uma lápide encontrada, descarregar perto de casa. As falas de ócio são separadas ([Idle] SpeechInterval). Ligue para voltar ao comportamento antigo.

**MinInterval** — Padrão: 15 · Faixa: 5 – 120  
Tempo mais curto (em segundos) entre as falas de um companheiro quando nada está acontecendo. Só é usado enquanto Chatter estiver ligado.

**MaxInterval** — Padrão: 30 · Faixa: 10 – 300  
Tempo mais longo (em segundos) entre as falas de um companheiro quando nada está acontecendo. Só é usado enquanto Chatter estiver ligado.

**Duration** — Padrão: 5 · Faixa: 1 – 15  
Segundos que uma linha de fala fica na tela.

**SayCooldown** — Padrão: 3 · Faixa: 1 – 30  
Tempo mínimo (em segundos) entre duas falas de qualquer companheiro, para não falarem todos ao mesmo tempo.

**CullDistance** — Padrão: 20 · Faixa: 5 – 60  
Falas de companheiros mais longe do que isso (em metros) não são mostradas.

## Advanced

Ajustes finos de tempos e distâncias. A maioria dos jogadores nunca precisa mexer nisso.

**RosterSaveInterval** — Padrão: 60 · Faixa: 10 – 600  
Segundos entre os salvamentos dos seus companheiros na lista (equipamento e configurações), usados para recuperar um companheiro perdido.

**GatheringSearchRadius** — Padrão: 50 · Faixa: 5 – 100  
Coleta: a que distância (em metros) em volta de si um companheiro procura árvores, rochas e plantas quando está seguindo você ou foi mandado a uma com a tecla de comando (com Seguir desligado vale [Gathering] Range). Fase 3.3c: 50, era 30 - seguindo, o companheiro pode estar a 25 m de você ([Gathering] FollowRange) e o alvo a até 25 m de você do outro lado, então o anel em que ele procura precisa alcançar cerca de 50 m.

**GatheringScanInterval** — Padrão: 4 · Faixa: 1 – 30  
Coleta: segundos entre as procuras pela próxima árvore, rocha ou planta.

**GatheringSwingInterval** — Padrão: 2.5 · Faixa: 0.5 – 5  
Coleta: segundos entre golpes numa árvore ou rocha.

**GatheringDropRadius** — Padrão: 12 · Faixa: 2 – 20  
Coleta: a que distância (em metros) em volta de uma árvore derrubada ou rocha quebrada os drops são pegos. Fase 3.3c: 12, era 8 - uma árvore pode cair (ou uma rocha se espalhar) mais longe que 8 m, e os drops ficavam para trás.

**GatheringSkipTime** — Padrão: 60 · Faixa: 5 – 300  
Coleta: segundos em que uma árvore, rocha ou planta que ele não conseguiu alcançar fica ignorada.

**FarmScanInterval** — Padrão: 4 · Faixa: 1 – 30  
Fazenda: segundos entre as procuras por plantações para colher e terra para plantar.

**FarmUseDistance** — Padrão: 2 · Faixa: 1 – 5  
Fazenda: a que distância (em metros) um companheiro tem que estar de uma plantação ou baú para usá-los.

**SmeltScanInterval** — Padrão: 3 · Faixa: 1 – 30  
Fundição: segundos entre as olhadas nas fornalhas e baús.

**SmeltUseDistance** — Padrão: 2.5 · Faixa: 1 – 5  
Fundição: a que distância (em metros) um companheiro tem que estar de uma fornalha para colocar ou tirar coisas.

**GearRepairScanInterval** — Padrão: 5 · Faixa: 1 – 30  
Conserto de equipamento: segundos entre as verificações de desgaste do equipamento de um companheiro.

**GearRepairTickInterval** — Padrão: 0.8 · Faixa: 0.1 – 5  
Conserto de equipamento: segundos por item consertado na bancada.

**GearRepairUseDistance** — Padrão: 2.5 · Faixa: 1 – 5  
Conserto de equipamento: a que distância (em metros) um companheiro tem que estar da bancada para consertar.

**RestockScanInterval** — Padrão: 5 · Faixa: 1 – 60  
Reparar e Reabastecer: segundos entre as procuras por algo para consertar, reabastecer ou organizar.

**AttackCooldown** — Padrão: 0.225 · Faixa: 0.1 – 2  
Combate: tempo mínimo (em segundos) entre os ataques de um companheiro, somado ao tempo de ataque de cada arma.

**MeleeTooClose** — Padrão: 0.3 · Faixa: 0 – 1.5  
Combate: mais perto do que isso (em metros, da borda do alvo) um companheiro dá um passo para trás antes de golpear com uma arma de corpo a corpo - uma estocada ou golpe que começa dentro do alvo erra, como um jogador em cima de um javali.

**MeleeReachMargin** — Padrão: 0.2 · Faixa: 0 – 1  
Combate: um companheiro golpeia com uma arma de corpo a corpo só quando está pelo menos esta quantidade (em metros) dentro do alcance da arma, não bem na borda dele, onde os acertos são incertos.

**FoodRegenInterval** — Padrão: 10 · Faixa: 1 – 60  
Comida: segundos entre os tiques de regeneração de vida da comida (o de um jogador é 10).

**FoodCheckInterval** — Padrão: 1 · Faixa: 0.5 – 10  
Comida: segundos entre as verificações se um companheiro deve comer ou beber.

**TombstoneScanInterval** — Padrão: 5 · Faixa: 1 – 30  
Morte: segundos entre as procuras pela própria lápide que sobrou por perto.

**TombstoneNavTimeout** — Padrão: 120 · Faixa: 10 – 600  
Morte: segundos que um companheiro continua tentando chegar à própria lápide antes de desistir.

**UiDiag** — Padrão: Desligado  
Diagnóstico de UI: ligado, escreve no log o que está na frente dos controles do painel F8 e do painel da IA (uma vez por sessão, na primeira vez que cada painel é aberto): quantos alvos de clique cada painel tem (os controles e os que são de propósito), todas as camadas ativas da cena (nome e ordem) e, para cada canvas acima do painel, os alvos que cobrem a coluna de controles. É o que se liga quando um clique ou uma UI parece estranha (um botão que não responde, um texto por cima, um clique engolido): o log diz quem está na frente. Desligado (padrão) não escreve nada e não custa nada; ligado o log cresce bastante (uma linha de resumo por painel, mais uma linha por alvo suspeito). Não muda nada no jogo - só diagnostica.

## AI

A camada de IA do projeto: ordens por texto e por voz, personalidade, memória, o reconhecimento de voz e a fala do companheiro.

**Enabled** — Padrão: Ligado  
Phase 3.1: type an order in the game chat ("collect 20 wood", "follow", "stop") and the companions carry it out. The order is interpreted by an LLM (Groq by default, or a local server, or OpenRouter) through an OpenAI-compatible API. Off = nothing is sent to the model.

**BaseUrl** — Padrão: `https://api.groq.com/openai/v1`  
Base URL of the OpenAI-compatible server. Default: Groq (https://api.groq.com/openai/v1), which asks for the ApiKey - the free key is at console.groq.com/keys. Alternatives: LM Studio (http://127.0.0.1:1234/v1) or Ollama (http://127.0.0.1:11434/v1) to run everything locally - with those two use 127.0.0.1 instead of localhost (on Windows localhost resolves to IPv6 first and the server only listens on IPv4, which costs about 2.5 s per order); or OpenRouter (https://openrouter.ai/api/v1), which works but the free tier is usually much slower. /chat/completions is added automatically. This is a text option: edit it in the .cfg file.

**Model** — Padrão: `openai/gpt-oss-20b`  
Name of the model sent to the server. Default: openai/gpt-oss-20b (Groq, free tier). Others: openrouter/free on OpenRouter; on LM Studio use the name the server lists (e.g. qwen2.5-3b-instruct). Text option: edit it in the .cfg file.

**ApiKey** — Padrão: ``  
API key, if the server asks for one. On Groq it is required (the free one is at console.groq.com/keys) and on OpenRouter too (openrouter.ai/keys). Leave it empty for LM Studio/Ollama running on your machine. Text option: edit it in the .cfg file. Never put a key in a shared config.

**Temperature** — Padrão: 0.1 · Faixa: 0 – 1  
How much the model may improvise. Keep it low so the same order always gives the same JSON.

**TimeoutSeconds** — Padrão: 30 · Faixa: 1 – 300  
Seconds to wait for the model before giving up. The request runs in the background, so a slow model never freezes the game.

**Prefix** — Padrão: ``  
Only chat messages that start with this go to the model (for example /c). Empty = every chat message is sent. Text option: edit it in the .cfg file.

**ShowReplyInChat** — Padrão: Ligado  
Shows the model's raw reply in the chat, so you can see what it understood. Useful while testing; turn it off for a cleaner chat.

**CooldownSeconds** — Padrão: 3 · Faixa: 0 – 60  
Minimum time (in seconds) between two orders, so a burst of chat messages does not flood the model.

**TargetRange** — Padrão: 40 · Faixa: 5 – 200  
Phase 3.6: how far from you (in metres) an order that names a creature searches for it ("attack the greydwarf"). The creature you are aiming at always counts, however far away it is. 40 m is the same leash at which the combat code gives up on a target.

**Personality** — Padrão: ``  
Phase 3.5: your companion's personality in your own words - how it speaks and behaves ("speaks like a loyal, blunt viking", "a sarcastic mage who complains about the cold"). This goes into the model's instructions, so the companion answers in that tone. When filled in it wins over PersonalityPreset. Up to 600 characters. Text option: edit it in the .cfg file.

**PersonalityPreset** — Padrão: ``  
Phase 3.5: a ready-made personality, written in Portuguese inside the mod: viking (loyal and fearless), mago (old and sarcastic), sarcastico (dry jokes), guia (patient and didactic), or custom (uses Personality). Empty or unknown = the default (a loyal, practical companion). Text option: edit it in the .cfg file.

**Memory** — Padrão: 4 · Faixa: 0 – 12  
Phase 3.5: how many recent exchanges (one message from you + one from the companion) the companion remembers and sends along with the next message, to keep the thread of the conversation. 0 = no memory. Kept per companion, in memory only (nothing is saved in the world) and forgotten when the companion dies or is dismissed. Every exchange makes the prompt the model has to read bigger, so keep it small for faster replies (Phase 3.3b: 4, was 6).

**Conversation** — Padrão: Ligado  
Phase 3.5: when the message is NOT an order ("good morning", "how are you?", "what did you think of my house?"), the companion answers as conversation: it says the reply out loud (speech bubble) and shows it in the chat. Orders keep working exactly as before. Off = only orders are understood, as in Phase 3.1.

**StructuredOutput** — Padrão: Ligado  
Asks the server to force the JSON format (response_format/json_schema). It keeps the model's reply always valid and is what allows small, fast models (e.g. a 3B) to be used without them answering loose text. Turn it off if your server does not support it - the mod falls back to the old format on its own and says so in the log.

**SttTest** — Padrão: Desligado  
SPIKE (test): runs the in-process Whisper.net speech recognition test by itself, about 25 seconds after the plugin loads, and writes the result to the log. Off = only the "modia_stt_teste" console command runs it. It is a simple test: it does not listen to the microphone and changes no companion behaviour.

**SttModel** — Padrão: `whisper\ggml-small-q5_1.bin`  
Whisper model the speech recognition loads (the voice orders AND the "modia_stt_teste" console command), relative to the plugin folder or an absolute path. Default whisper\ggml-small-q5_1.bin (181 MB, Phase 3.3b - much better in Portuguese than the old whisper\ggml-base-q5_1.bin, 57 MB, which stays on disk if you want to go back: measured on a 16-core CPU with 8 threads, 638 ms per 2 s clip with base against 2187 ms with small, and the time barely changes with the clip length because the encoder always works on a 30 s window). Text option: edit it in the .cfg file.

**SttWavTest** — Padrão: `whisper\jfk.wav`  
SPIKE (test): the WAV file to transcribe, relative to the plugin folder (or an absolute path). It must be PCM WAV (16 kHz mono is ideal). Text option: edit it in the .cfg file.

**VoiceEnabled** — Padrão: Ligado  
Phase 3.3: hold VoiceKey and speak, and the companion accepts what you said - the same orders you can type in the chat ("collect 10 wood", "follow", "attack the greydwarf"). The speech is recognised locally, inside the game, by Whisper.net with the SttModel model: nothing is sent anywhere. Off = the key does nothing (and, in Phase 3.3b, the key goes back to doing what the game did with it).

**VoiceKey** — Padrão: `V`  
Phase 3.3: push-to-talk key. Hold it while you speak, release it for the speech to be recognised and carried out. It only works with the game focused and no window open (inventory, map, menu, chat, console, the mod's own panels). NOTE (Phase 3.3b): V is also the game's "Toggle Auto-Pickup". While VoiceEnabled is on, the mod blocks that toggle when the game's auto-pickup is bound to this same key, so the key stays free for talking - rebind auto-pickup in the game's own controls screen (or pick another key here) if you want it back.

**VoiceLanguage** — Padrão: `pt`  
Phase 3.3: language of the speech, as a Whisper code (pt, en, es...). Empty or "auto" lets the model detect it. Text option: edit it in the .cfg file.

**VoiceMaxSeconds** — Padrão: 10 · Faixa: 1 – 30  
Phase 3.3: longest recording (in seconds) for a voice order. Holding the key longer than this stops the recording and transcribes what was captured.

**VoiceSilenceThreshold** — Padrão: 0.01 · Faixa: 0 – 0.5  
Phase 3.3: how loud the audio must be (peak level of a 10 ms window, from 0 to 1) to count as speech when the silence at the start and the end of the recording is trimmed. Phase 3.3b: 0.01 (was 0.02) - at 0.02 the quiet start of a word was often cut, and that is what made short orders come back wrong. Raise it if your microphone picks up a lot of room noise.

**VoiceInitialPrompt** — Padrão: veja abaixo  
Phase 3.3b: initial prompt sent to Whisper before every transcription. It biases the vocabulary, and that is what makes short orders ("collect 10 wood") come out right instead of guessed. Put in the words you really say (the orders, the creature and item names); empty = no bias. Text option: edit it in the .cfg file.

Valor padrão:

```
Ordens para o companheiro: colete madeira, pedra, minerio, ferro, segue, para, vem ca, ataca o greydwarf, equipa o machado de bronze, larga a pedra, descansa, bom dia.
```

**VoiceDevice** — Padrão: 0 · Faixa: 0 – 8  
Phase 3.3: which microphone to record from, as the index of the list the log prints at startup ([STT] Push-to-talk: ... 0: name, 1: name). 0 = the first one (the Windows default).

**VoiceShowText** — Padrão: Ligado  
Phase 3.3: shows what the microphone heard ("Entendi: "collect 10 wood"") in the chat and on the HUD. Phase 3.3d: the transcription appears in the PLAYER's own bubble (not the companion's), so a wrong transcription is easy to spot - the one who answers is the companion, with its own bubble and VOICE. Off = the recognised text is only written to the log.

**PanelKey** — Padrão: `I`  
Phase 3.7: opens the AI panel in game. The AI tab has the free companion summoner, the speech volume and speed, the memory, the conversation and the push-to-talk key. Each companion has its own tab, with the name, the tier (reborn), the voice (with the Listen button) and ITS personality - the [AI] values below are the default of whoever has no choice of its own. Every change is saved right away. The default is I: neither the game nor the mod uses that key. The key is not stolen from a text box (the chat, the console and the search field are left in peace). Key option: edit it in the .cfg file or click it in the F8 panel.

**TtsEnabled** — Padrão: Ligado  
Phase 3.4: the companion SPEAKS the replies out loud. The speech is synthesised on your computer by Kokoro-82M (via sherpa-onnx, inside the game, no child process and nothing leaving your machine), in the TtsVoice voice, and comes out where the companion is (not in the middle of the screen). Off = it only shows the bubble and the chat, as before. The first speech of every session waits for the model to load (~1 s, in the background, when you enter the world).

**TtsVoice** — Padrão: 42 · Faixa: 0 – 100  
Phase 3.4: which Kokoro voice to use. The Brazilian Portuguese voices of this model are: 42 = pf_dora (feminine, the default), 43 = pm_alex (masculine) and 44 = pm_santa (masculine). The other numbers belong to other languages (45, for example, sounds Chinese). 0 (or any negative value) = the default voice (42).

**TtsSpeed** — Padrão: 1 · Faixa: 0.5 – 2  
Phase 3.4: how fast the companion speaks (1 = normal; above 1 speaks faster). It also makes the speech shorter.

**TtsThreads** — Padrão: 4 · Faixa: 1 – 16  
Phase 3.4: how many CPU cores the speech synthesis may use. Measured on this machine (Ryzen 7 5700X): 4 threads generate faster than real time (RTF ~0.83), so the speech is ready before the companion needs it; more threads do not speed it up and compete with the game.

**TtsVolume** — Padrão: 1 · Faixa: 0 – 1  
Phase 3.4: volume of the companion's voice (0 = muted, 1 = the game's normal volume).

**TtsModel** — Padrão: `tts\kokoro\model.int8.onnx`  
Phase 3.4: the Kokoro model the companion speaks with (relative to the plugin folder, or an absolute path). The voices (voices.bin), the tokens (tokens.txt) and espeak-ng-data are looked for in the SAME folder as the model. Text option: edit it in the .cfg file.

**TtsLanguage** — Padrão: `pt-br`  
Phase 3.4: language of the spoken text, in the format Kokoro expects. This model is multi-lingual and REQUIRES a language: "pt-br" is Brazilian Portuguese ("pt" alone is European Portuguese). Empty goes back to "pt-br". Text option: edit it in the .cfg file.

**TtsCache** — Padrão: Ligado  
Phase 3.4: keeps the speeches already synthesised (text + voice + speed) to play them again instantly. The mod's fixed lines ("Já temos o suficiente.") come out instantly; the memory used is small. Off = every speech is synthesised again.

**TtsSpeakReplies** — Padrão: Ligado  
Phase 3.4: speaks the model's reply out loud (the "reply" field). In a conversation it is its answer; in an order, when TtsSpeakModLines is off, it is the confirmation in its own way ("Deixa comigo.").

**TtsSpeakModLines** — Padrão: Ligado  
Phase 3.4: speaks out loud the mod's own line that shows in the chat/HUD ("Ok: coletar madeira (10).", "Atacando o troll.", "Já temos o suficiente."). In an ORDER it wins over the model's reply (the voice follows what is written on the screen); turn it off to hear the model's reply instead. Nothing is spoken twice: with both options on, the line that is not spoken only goes to the log.

**TtsReverbZoneMix** — Padrão: 0 · Faixa: 0 – 1  
How much of the world's reverb zones goes into the companion's voice. 0 (default) = dry voice. Valheim has a global reverb zone (1000 m radius, 4 s tail) that made the companion's speech 'echo like in a cave' - the game's music uses 0 and the ambience 0.19. Raise it a little (e.g. 0.19) if you want a subtle 'space' in the voice. It applies to the next sentence, no restart.

**EcoDiag** — Padrão: Desligado  
Audio diagnostics: when on, writes to the log (once per session, on the first spoken sentence) the scene's AudioListener, the world's reverb zones and how the game's own sources are configured (mixer, reverb, bypass). Use it to investigate any sound problem with the companion. It does not change the sound - it only reports.
