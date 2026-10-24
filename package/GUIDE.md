# AI Companions — Guia do companheiro

Como os seus companheiros funcionam: o que cada trabalho faz, como preparar e o que esperar. Se você ficou se perguntando por que um companheiro fez (ou não fez) alguma coisa, a resposta provavelmente está aqui.

A maioria dos números abaixo são os padrões e podem ser mudados nas configurações (veja `CONFIGURATION.md`, ou aperte `F8` no jogo).

## Conteúdo

1. [Primeiros passos](#primeiros-passos)
2. [Conversando com o companheiro](#conversando-com-o-companheiro)
3. [A roda de comandos](#a-roda-de-comandos)
4. [Apontar e comandar](#apontar-e-comandar)
5. [Casa](#casa)
6. [Seguindo você](#seguindo-você)
7. [Trabalhos](#trabalhos)
   - [Madeira, pedra, minério e forrageio](#madeira-pedra-minério-e-forrageio)
   - [Caça](#caça)
   - [Pesca](#pesca)
   - [Fazenda (e como marcar as zonas de fazenda)](#fazenda)
   - [Fundição](#fundição)
   - [Cozinha](#cozinha)
   - [Reparar e reabastecer](#reparar-e-reabastecer)
8. [Quando não há nada para fazer](#quando-não-há-nada-para-fazer)
9. [Ferramentas, baús e carga](#ferramentas-baús-e-carga)
10. [Combate](#combate)
11. [Comida, stamina e vida](#comida-stamina-e-vida)
12. [Dormir, descansar e camas](#dormir-descansar-e-camas)
13. [Morte e volta](#morte-e-volta)
14. [Locomoção](#locomoção)
15. [Habilidades](#habilidades)
16. [Multiplayer](#multiplayer)
17. [Solução de problemas](#solução-de-problemas)

---

## Primeiros passos

- Na primeira vez que você entra num mundo com um personagem, é oferecido um companheiro de graça.
- Outras formas de conseguir mais companheiros estão por vir.
- Um companheiro novo já segue você. Dê a ele equipamento e comida: ele equipa a melhor arma, escudo e armadura que estiver carregando, e come do próprio inventário.
- Para dar um trabalho a ele, abra a roda de comandos (abaixo) e escolha um. A maioria dos trabalhos funciona melhor depois que você define a **casa** dele.

## Conversando com o companheiro

**Toque** a tecla de interagir (`E`) olhando para um companheiro para abrir o **painel** dele:

- o inventário e o equipamento dele
- a comida dele
- o nome dele (você pode renomear)

Arraste itens para dentro e para fora como num baú. Tudo o que você dá a ele é dele: ele guarda e não coloca num baú.

## A roda de comandos

**Segure** a tecla de interagir (`E`) olhando para um companheiro para abrir a **roda de comandos** dele. Leve o mouse (ou o analógico) até uma fatia e clique.

| Fatia | O que faz |
|:---|:---|
| **Seguir** | Liga ou desliga o seguir você |
| **Madeira / Pedra / Minério / Coleta** | Trabalhos de coleta (a fatia **Coleta** é o forrageio) |
| **Caçar** | Trabalho de caça |
| **Pescar** | Trabalho de pesca |
| **Plantar** | Trabalho de fazenda (clique com o botão direito para abrir o editor de zonas de plantio) |
| **Fundir** | Trabalho de fundição |
| **Cozinhar** | Trabalho de cozinha e preparo de bebidas |
| **Reparar e Reabastecer** | Cuida da sua base |
| **Definir Casa** | Marca como casa o lugar onde o companheiro está |
| **Vagar** | Seguindo você: ligado = ele se movimenta e olha em volta quando você para; desligado = ele fica num lugar só, parado |
| **Recolher** | Recolhe itens pelo caminho e depois dos abates (como o auto-pickup) |
| **Comando** | Se este companheiro responde à tecla de comando (apontar e chamar) |
| **Dispensar** | Remove o companheiro (pergunta antes) |

Escolher um trabalho de novo, ou escolher **Seguir**, desliga o trabalho. O **anel interno** tem as [posturas de combate](#combate).

**Trabalhos e Seguir:**
- **Madeira, Pedra, Minério, Coleta e Caçar** podem rodar com Seguir ligado ou desligado. Com Seguir ligado, eles só vão atrás do que está perto de você conforme vocês andam. Com Seguir desligado, eles trabalham uma área.
- **Os outros trabalhos** (Pescar, Plantar, Fundir, Cozinhar, Reparar e Reabastecer) desligam o Seguir quando você os escolhe, e o religam quando o trabalho termina.

## Apontar e comandar

Olhe para alguma coisa e aperte a **tecla de comando** (`Z`). Os companheiros por perto fazem o que faz sentido para o que você está apontando:

| Aponte para… | Eles… |
|:---|:---|
| Um inimigo | Atacam ele |
| Uma árvore, rocha ou minério | Coletam |
| Uma carroça | Engatam e puxam (aponte de novo para soltar) |
| Um navio | Embarcam e sentam num lugar |
| Uma cama | Vão dormir nela (aponte de novo para acordá-los) |
| Um fogo | Sentam ao lado (aponte de novo para se levantar) |
| Uma porta fechada | Abrem |
| Um baú | Guardam o que recolheram |
| Uma bancada | Consertam o equipamento ali |
| A lápide dele | Vão buscar as coisas dele |
| O chão | Andam até lá e esperam |

- **Segure** a tecla de comando para chamar os seus companheiros até você.
- **Continue segurando** por cerca de 2 segundos para chamar **todos** os companheiros, por mais longe que estejam, inclusive os que estão com Comando desligado.
- Só companheiros a até 40 m atendem a um comando normal ou a um chamado.

**As ordens vêm primeiro.** Enquanto um companheiro cumpre a sua ordem, o trabalho dele espera. Depois ele volta ao que estava fazendo. Um companheiro de Reparar e Reabastecer fica onde você o mandou por 30 segundos antes de voltar às tarefas dele.

## Casa

**Definir Casa** (na roda de comandos) marca como casa o lugar onde o companheiro está e abre o painel da casa, onde você pode mudar o tamanho da área da casa.

A casa importa porque os companheiros:

- **reivindicam uma cama** dentro da área da casa (mostrada com o nome deles)
- **buscam ferramentas** nos baús em volta de casa e devolvem as emprestadas (tire uma ferramenta da mochila dele e ele trabalha sem ela naquele trabalho)
- **esvaziam as bolsas** nos baús em volta de casa quando voltam ou trocam de trabalho
- **trabalham em volta de casa** quando um trabalho é iniciado perto dela
- **consertam o equipamento** um pouco mais cedo em casa, onde uma bancada está perto

"A volta de casa" significa dentro de 60 m do ponto de casa (a opção `HomeTripRange`).

## Seguindo você

- Um companheiro que segue você escolhe o próprio lugar perto de você: ao lado, atrás, às vezes à frente. Ele se move quando você se afasta mais de 10 m (4 m em lugares fechados).
- **Fique parado por 45 segundos** e os seus companheiros voltam e esperam pertinho de você.
- **Pare num lugar fechado** e eles sentam perto de você: numa cadeira ou banco livre, senão no chão. Perto de casa, eles esvaziam as bolsas primeiro.
- Se um companheiro ficar muito para trás (50 m) e não conseguir te ver, ele alcança você aparecendo do seu lado.
- Eles atravessam portais com você.

## Trabalhos

Todo trabalho pausa para uma luta, para as suas ordens, para a hora de dormir e para pegar o saque, e depois continua. Quando um trabalho fica sem serviço, veja [Quando não há nada para fazer](#quando-não-há-nada-para-fazer).

> **Os companheiros só trabalham enquanto você está perto.** O jogo descarrega as partes do mundo onde não há nenhum jogador, e tudo nelas congela, companheiros incluídos. Um companheiro trabalhando em casa enquanto você está longe explorando não está trabalhando; ele continua quando você volta. No multiplayer, a área fica carregada enquanto qualquer jogador estiver perto dela.

### Madeira, pedra, minério e forrageio

**Onde eles trabalham**
- **Seguir ligado:** só árvores, rochas ou plantas dentro de 25 m de você (20 m para o forrageio), conforme vocês andam. Nada tão perto? Eles só seguem você. Pense nisso como "passamos por umas framboesas, vamos pegar".
- **Seguir desligado:** eles trabalham uma área. Em volta de casa, se o trabalho começou perto dela; senão, em volta de onde você os deixou, até 60 m de distância. Eles continuam trabalhando ali quando você vai embora.

**O que eles coletam**
- Cada trabalho tem uma lista do que coletar e de quanto manter (500 de cada por padrão, contando a mochila do companheiro e os baús em casa). Quando já tem o suficiente, eles param.
- Coisas que não estão na lista (como pedra de um veio de cobre) ficam no chão.
- **Madeira:** derruba árvores e corta toras e tocos. Por padrão ele ignora **Carvalho** (a opção `WoodSkipTrees`) e não derruba árvores a menos de 10 m de qualquer coisa que você construiu. Ele também sai da frente de árvores caindo.
- **Pedra:** minera rochas para conseguir pedra, sílex e coisas do tipo.
- **Minério:** minera veios de minério. A pedra que sobra do veio é jogada numa pilha longe do minério, para não enterrá-lo.
- **Coleta (forrageio):** pega frutinhas, cogumelos, flores e coisas do tipo. Não precisa de ferramenta.

**No início do jogo**
Se não houver machado de bronze (ou melhor) em lugar nenhum e nem uma forja em casa para fazer um, os trabalhos de Madeira e Pedra também pegam galhos, pedras e sílex do chão. Um companheiro sem machado nem picareta nenhuma só pega coisas do chão.

**Ferramentas**
Eles precisam de um machado para Madeira e de uma picareta para Pedra e Minério. A ferramenta que falta é buscada nos baús de casa. Se a ferramenta não cortar o que está ali (um machado de pedra numa árvore antiga, por exemplo), você recebe uma mensagem.

**Quando a mochila enche**
- Perto de casa: eles descarregam nos baús de casa.
- Longe de casa: eles descarregam num baú, carroça ou navio a até 30 m.
- Se não houver onde descarregar, eles jogam fora as tranqueiras de que não precisam (nada de equipamento, comida, ferramentas ou qualquer coisa nas listas deles).

### Caça

- **O quê:** criaturas que derrubam carne, até ter o suficiente de cada tipo de carne (100 por padrão, contando a mochila e os baús de casa). Couro e outros drops são pegos de brinde. Animais domados, chefes e outros companheiros nunca são caçados.
- **Chegando de mansinho:** eles andam em silêncio até bichos ariscos como cervos e lebres, e depois se esgueiram agachados nos últimos 20 m (Hunting > SneakFrom), para o bicho não disparar. Esgueirar-se gasta stamina como a de um jogador: quando ela fica baixa, eles param, continuam agachados e recuperam o fôlego (Hunting > SneakRestStamina). Esgueirando-se, eles atravessam água rasa a pé em vez de pular. Perseguindo uma presa que disparou, eles só correm enquanto têm stamina de sobra (Hunting > ChaseRunStamina).
- **Armas:** um arco ou besta com munição vem primeiro. Sem nenhum, eles caçam com uma arma de corpo a corpo: chegam de mansinho e golpeiam.
- **Lanças de arremesso:** um caçador com uma lança na mão a arremessa em bichos ariscos de até 12 m (Hunting > ThrowRange), desde que tenha outra arma na mochila para continuar a luta — ou, com a lança como única arma, quando a presa já o notou (aí ele pega a lança antes de continuar a caça). Ele se esgueira só até a distância de arremesso (Hunting > ThrowStandoff, 10 m), para e arremessa, e espera a stamina que o arremesso precisa em vez de andar até lá para esfaquear. Depois da caça ele anda até lá e pega a lança de volta. Desligue isso em Hunting > ThrowSpear.
- **Pássaros:** com arco ou besta e munição, eles atiram em corvos e gaivotas pelas penas.
- **Seguir ligado:** eles caçam a até 40 m de você (Hunting > FollowRange). **Seguir desligado:** eles caçam a até 40 m em volta de onde foram postos a trabalhar.
- **Perseguição:** eles perseguem uma presa que corre, até 15 m mais longe, e depois deixam ela ir.
- **Mochila cheia:** com Seguir desligado e uma casa definida, eles trazem a caça para casa e voltam para o mato.

### Pesca

**Como preparar**
- Dê ao companheiro uma **vara de pesca** e **isca**, e deixe ele perto da água.
- Ele pesca em volta do lugar onde você iniciou o trabalho, até 30 m de distância.

**Como ele pesca**
- Ele procura peixes ao alcance e escolhe um lugar na margem.
- Ele usa a melhor isca que tem para aqueles peixes, lança na distância certa e espera até 60 segundos por uma fisgada.
- Quando um peixe fisga, ele dá o golpe e depois recolhe com cuidado, aliviando quando a linha estica.

**Limites**
- Cada tipo de peixe tem uma meta (100 por padrão, contando a mochila dele e os baús de casa). Quando ela é atingida, ele deixa aquele tipo em paz.
- Perto de casa, ele traz a pesca para casa quando a mochila está quase cheia.

### Fazenda

O fazendeiro **planta** nas zonas de plantio que você marca e **colhe** as plantações maduras por perto.

**Como marcar uma zona de plantio**

1. **Cultive a terra primeiro**, com um cultivador, como você faria para plantar você mesmo. O fazendeiro só planta em terra cultivada, e ainda não cultiva a terra sozinho.
2. **Abra o editor de zonas de plantio**, de uma das duas formas:
   - Fique perto do seu fazendeiro e aperte `Left Alt` + `Z`. Isso abre o editor do companheiro mais próximo.
   - Ou clique com o botão direito em **Plantar** na roda de comandos dele.
3. Um quadrado verde mostra onde a zona ficaria. Controles:

   | Mouse e teclado | Controle | Ação |
   |:---|:---|:---|
   | Botão esquerdo | A | Coloca a zona |
   | Botão direito numa zona | X | Remove ela |
   | Scroll do mouse | Bumpers | Aumenta ou diminui (de 4 m a 30 m de lado) |
   | Segurar botão direito + scroll | D-pad | Gira a zona |
   | Esc (ou `Left Alt` + `Z` de novo) | Start | Fecha o editor |

4. Depois de colocar uma zona, escolha a **cultura** dela, ou **Qualquer Cultura**.
5. Cada companheiro pode ter até **4 zonas**. Depois coloque o companheiro no trabalho de **Plantar**.

**Plantar**
- **Uma zona com cultura:** o fazendeiro planta aquela cultura ali.
- **Uma zona "Qualquer Cultura":** ele planta da lista `PlantAny` (cenoura, nabo, cebola, cevada, linho, cogumelos e frutinhas de videira por padrão). Ele planta em blocos pequenos de 4 de cada, então o campo fica misturado mas organizado. Mudas de árvore nunca são plantadas numa zona "qualquer".
- Ele espaça as plantas para cada uma ter espaço para crescer, e não planta dentro de uma área protegida que não deixa ele entrar.

**Sementes**
- Ele carrega até 40 de cada semente, pegas dos baús.
- Ele sempre mantém pelo menos 20 de cada semente e não planta as últimas.
- Quando uma semente cai abaixo de 100, ele planta algumas plantas de semente (cenoura, nabo, cebola…) junto das culturas para repor o estoque.

**Colher**
Ele pega as plantações maduras, indo direto para a próxima madura por perto como quem trabalha uma fileira, e guarda a colheita num baú.

### Fundição

- **Abastecer:** mantém fornos de carvão, fornalhas, fornos e altos-fornos alimentados. Ele traz o insumo (minério, madeira para o forno de carvão…) e o combustível (carvão, madeira) dos baús por perto, e dos baús de casa quando está perto de casa.
- **Produção:** recolhe o que sai e guarda.
- **Sem combustível nos baús:** ele pode desmontar uma pilha de madeira com um martelo e usar aquela madeira. Precisa de um martelo na mochila.
- **Tamanho da viagem:** ele carrega até 20 de insumo e 40 de combustível por viagem.

### Cozinha

**Estações que ele usa**
- caldeirão
- mesa de preparo de comida
- alambique de hidromel
- estação de cozinha e espeto
- estação de cozinha de ferro
- forno de pedra
- barris de fermentação
- colmeias

Cada tipo pode ser desligado nas configurações. Ele só usa receitas que você conhece.

**O que ele faz**
- **Comida:** cozinha cada comida até os baús por perto (e os de casa) terem 20 dela. Ele cozinha em fornadas de 5, como um jogador.
- **Reserva:** ele sempre deixa pelo menos 20 de cada ingrediente cru, para sobrar para você.
- **Hidromel:** prepara bases de hidromel, enche os barris de fermentação e tira o hidromel pronto, até ter 10 de cada base e 20 de cada hidromel. Não quer hidromel? Desligue `MakeMead` e ele só cozinha comida.
- **O seu progresso:** ele prepara primeiro as bases de hidromel que você vai precisar para o próximo chefe. Por exemplo, depois do Bonemass são resistência ao gelo e cura e stamina médias.
- **Guardar:** a comida cozida vai para um baú que já tem aquela comida (ou outras comidas cozidas).

**Fogos e mel**
- Ele mantém os fogos embaixo das estações de cozinha que usa e o forno em cerca de metade da capacidade, e reabastece quando ficam baixos.
- Sem madeira nos baús, ele pode desmontar uma pilha de madeira.
- Ele recolhe mel das colmeias para as receitas dele.

### Reparar e reabastecer

O trabalho para um companheiro que você deixa cuidando de uma base. Inicie perto de casa e ele trabalha em volta de casa. Inicie em qualquer outro lugar (num posto avançado, digamos) e ele trabalha dentro de 30 m de onde você iniciou.

Em ordem de prioridade:

1. **Reparar:** conserta peças de construção abaixo de 75% de vida com um martelo, um golpe em cada. Ele precisa da bancada da peça por perto e de acesso à área protegida, como um jogador.
2. **Reabastecer:** mantém os fogos abastecidos, colocando combustível um pedaço por vez e completando qualquer um que esteja abaixo de 80%. Cada um destes pode ser ligado ou desligado:
   - fogueiras e fogos de chão
   - lareiras
   - braseiros
   - tochas de pé
   - tochas de parede e arandelas
   - banheiras quentes
   - qualquer outra coisa que queime combustível

   Ele não abastece fornos de carvão nem fornalhas; isso é o trabalho de Fundição.
3. **Colmeias:** esvazia colmeias que estejam pelo menos na metade e junta o mel com o resto do seu mel.
4. **Pilhas:** mantém 100 de cada material de construção (madeira, pedra…) nos baús. O excedente vira pilhas (Pilha de Madeira, Pilha de Pedra…), construídas ao lado de pilhas que você já fez. Ele nunca começa um lugar de pilha nova sozinho. Quando os baús caem abaixo de 100, ele desmonta uma pilha para reabastecê-los.
5. **Organizar:** arruma os baús igual com igual, levando cada item para o baú que já tem mais dele e juntando pilhas parciais. Equipamento (armas, armaduras, ferramentas) não é movido, a não ser que você ligue isso.

Ele busca um martelo e combustível nos baús se precisar, e devolve o que sobra.

## Quando não há nada para fazer

Se um companheiro fica 10 segundos sem nada para fazer e não está seguindo você, ele fica à toa dentro de 8 m do trabalho dele em vez de ficar parado. Ele pode:

- **fazer uma pequena tarefa por perto:** consertar uma peça, reabastecer um fogo, recolher mel, guardar o que está carregando
- **sentar** numa cadeira ou banco livre (ou no chão) por um tempo
- **dar uma volta** até uma estação ou baú para dar uma olhada
- **conversar:** ir até você ou outro companheiro e puxar assunto

De vez em quando (no máximo a cada 5 minutos) ele diz por que está à toa, por exemplo "Nada para cozinhar agora." ou "Esperando as plantações crescerem."

No instante em que o trabalho dele tem algo para fazer, ele volta ao serviço. Ele também para de ficar à toa por causa de uma luta, das suas ordens, da hora de dormir, ou de você abrir o painel ou a roda dele.

## Ferramentas, baús e carga

**Ferramentas**
- Os trabalhos buscam a ferramenta de que precisam nos baús de casa: machado, picareta, cultivador, vara e isca, martelo, arco e flechas.
- Quando o trabalho termina, a ferramenta emprestada volta para o baú dela.

**Consertar equipamento**
- Um companheiro leva o equipamento gasto até uma bancada quando algo cai abaixo de 50%, ou abaixo de 80% em casa.
- Ele só usa bancadas que um jogador poderia usar naquele momento: sob um teto, com fogo se precisar.
- Se nenhuma bancada por perto puder consertar um item (por exemplo, ele precisa de uma forja negra que você não construiu), esse item fica de lado por 10 minutos em vez de ser tentado de novo e de novo.

**Esvaziar as bolsas**
- Quando você troca o trabalho de um companheiro, ou ele volta para casa, ele guarda o que pegou enquanto trabalhava.
- O que você deu a ele, ele mantém.

**Quais baús ele usa**
- Os companheiros usam baús como um jogador. Eles não usam um baú que alguém deixou aberto, o baú privado de outro jogador, nem um baú atrás de uma área protegida que eles não podem abrir.
- Trabalhos que procuram um baú não usam carroças nem porões de navio. As exceções: um coletor com a mochila cheia e longe de casa pode descarregar num deles, e você sempre pode apontar um companheiro para um.

**Carga**
Um companheiro consegue carregar 300 de peso, igual a um jogador.

## Combate

**Posturas** (anel interno da roda de comandos; no controle, empurre o analógico até a metade em direção a uma):

| Postura | Comportamento |
|:---|:---|
| **Equilibrado** | Escolhe corpo a corpo ou arco/besta conforme a distância |
| **Passivo** | Nunca começa briga; só segue ou trabalha |
| **À distância** | Prefere arco ou besta e mantém distância |
| **Corpo a corpo** | Fica nas armas de perto |

**Quando eles lutam**
- Eles atacam uma criatura depois que ela percebe você ou eles. Com a opção `EngageOnNotice` desligada, eles esperam até ela vir de fato para cima de você.
- Eles deixam em paz os animais que nunca atacam, como cervos. A Caça vai atrás de bicho de propósito.
- Qualquer coisa que os ataque é sempre revidada.

**Como eles lutam**
- **Bloqueio:** eles bloqueiam com escudo, dão parry e contra-atacam.
- **Vários atacantes:** eles trocam para o melhor escudo de bloqueio (um escudo torre, digamos) quando 3 ou mais inimigos atacam ao mesmo tempo.
- **Escolha da arma:** eles escolhem a arma que faz mais dano a *este* inimigo, usando as fraquezas e resistências dele.
- **Fogo:** eles abrem com uma tocha contra inimigos fracos a fogo, e depois voltam.
- **Arcos e bestas:** só são usados quando eles carregam a munição certa, a 15 m ou mais. Abaixo de 12 m eles voltam para o corpo a corpo.
- **Vida baixa:** abaixo de 30% eles lutam na defensiva, e abaixo de 15% eles correm.
- **Ataques pelas costas:** companheiros não tomam dano de ataque pelas costas.

**Depois da luta**
Com Recolher ligado, eles vão pegar o que o abate deixou (a até 30 m). Eles não nadam para pegar.

## Comida, stamina e vida

- **Comida:** os companheiros têm 3 espaços de comida como um jogador e comem do próprio inventário quando um espaço fica vazio. A comida dá vida e stamina do mesmo jeito que dá para você.
- **Vida e stamina base:** 25 de vida e 50 de stamina antes da comida, igual a um jogador.
- **Hidroméis:** eles bebem um hidromel de cura abaixo de 50% de vida e um hidromel de stamina abaixo de 25% de stamina.
- **Efeitos de status:** estar molhado, com frio, descansado e coisas do tipo mudam a regeneração deles como mudam a sua.
- **Correr e nadar:** os dois gastam stamina. Um companheiro nadando com pouca stamina (25%) vai para a margem.

## Dormir, descansar e camas

**Camas**
- Cada companheiro reivindica uma cama livre na área da casa dele. A cama mostra o nome dele.
- Ele também pode usar uma das suas camas, desde que não seja o seu ponto de renascimento. Assim, clicar na cama de um companheiro por acidente não a toma dele (a cama continua mostrando o nome do companheiro). Dormir nela toma, porque isso a transforma no seu ponto de renascimento. Ele nunca usa a cama de outro jogador.
- Sem uma casa definida, ele só pega uma cama a até 20 m de uma cama sua.
- Ele mantém a cama depois de morrer e quando você sai e volta ao jogo.
- Aponte a tecla de comando para uma cama para mandar um companheiro dormir nela.

**Dormir**
- **À noite:** um companheiro a até 50 m da cama dele para o que está fazendo e vai dormir. Isso inclui companheiros trabalhando. Ele volta ao trabalho de manhã.
- **Quando você dorme:** os seus companheiros vão dormir também, mesmo que ainda não seja noite. O pulo da noite espera até 30 segundos por eles.

**Descansar perto de um fogo**
- Quando você senta perto de um fogo, companheiros por perto sem trabalho sentam também.
- Depois de 20 segundos perto de um fogo eles ganham o efeito *Descansado*. Sentar também cura.
- Quando você manda um companheiro sentar perto de um fogo com a tecla de comando, ele fica ali até 5 minutos, ou até você apontar para o fogo de novo.

## Morte e volta

- **Lápide:** um companheiro que morre deixa uma lápide, marcada no seu mapa até ser esvaziada.
- **O que ele perde:** isso segue a opção de penalidade de morte do seu mundo, como para um jogador. Você pode mudar isso (manter tudo, manter o equipamento vestido, ou largar tudo).
- **Volta:** depois de 5 minutos ele volta sozinho na cama dele, ou do seu lado se não tiver cama. O tempo continua contando enquanto você está fora ou offline.
- **As coisas dele:** quando ele volta e está perto da própria lápide, ele anda até lá e recolhe as coisas dele. Você também pode apontar a tecla de comando para a lápide.
- **Habilidades:** ele perde 25% do progresso das habilidades, mais ou menos como um jogador.

### Recuperando um companheiro perdido

O seu jogo mantém uma lista dos seus companheiros em cada mundo, salvando o equipamento e as configurações de cada um a cada minuto enquanto ele está perto de você, e sempre que o jogo salva o seu personagem (inclusive quando você sai ou fecha o jogo). Se um companheiro sumir de vez (por exemplo, apagado por um servidor sem o mod), você pode trazê-lo de volta.

**Santuário de Odin.** Construa um com o martelo (aba Diversos, perto de uma bancada) por 10 pedras, 5 madeiras e 3 olhos de greydwarf. Ele parece uma lápide (ou uma pedra de área protegida, com Death > ShrineModel). Use-o para ver os seus companheiros neste mundo e o status de cada um. Para cada um que estiver Perdido ou Desconhecido ele pergunta se deve trazê-lo de volta; diga Sim e ele aparece ao lado do santuário, diga Não para passar ao próximo. Você pode mudar o custo em Death > ShrineRecipe, ou desligar o santuário em Death > ShrineBuildable.

**Pela configuração** (sem precisar de santuário):

- **Ver o status:** ligue **General > ShowCompanionRoster** na configuração. Você verá cada companheiro como **Vivo**, **Morto** (a lápide dele vai trazê-lo de volta), **Perdido** (não sobrou nada dele, então pode ser recuperado) ou **Desconhecido**.
- **Trazer um de volta:** ligue **General > RecoverLostCompanion**. O primeiro companheiro Perdido aparece do seu lado com o equipamento e as configurações do último salvamento da lista. As duas opções se desligam sozinhas depois.

O que os status significam:

- **Desconhecido:** num servidor dedicado o seu jogo só consegue verificar a área em volta de você, então um companheiro que não está por perto fica Desconhecido. Ele pode ainda estar vivo em algum lugar. O santuário acrescenta um aviso quando pergunta; pela configuração, ligue RecoverLostCompanion uma segunda vez em até 60 segundos para recuperá-lo assim mesmo. Se ele tiver morrido, a lápide dele também guarda o que estava nele.
- **Sem duplicatas:** se o companheiro antigo aparecer depois, ele é removido, e a lápide antiga dele não vai trazê-lo de volta.
- **Lápides antigas:** um companheiro recuperado ainda conhece as próprias lápides. Como um que renasceu, ele anda até lá e recolhe as coisas quando está perto de uma (dentro de Death > TombstoneRecoveryRange, 50 m). Você também pode esvaziá-la você mesmo.
- Um companheiro só entra na lista depois de ficar perto de você na versão 1.5.1 ou mais nova. Um companheiro dispensado sai da lista.

## Locomoção

Os companheiros acham o próprio caminho:

- **Portas:** eles abrem portas e portões ao chegar, se alinham com o vão e fecham a porta atrás de si.
- **Degraus e beiradas:** eles pulam degraus e descem de beiradas de até 3 m (sem dano de queda) quando isso é bem mais curto do que dar a volta.
- **Escadas de mão:** para subir até onde você está (uma torre, uma plataforma), eles sobem uma escada de madeira como você faria: andam até o pé dela e entram nela, e a escada os levanta. Se a escada começa acima do chão, ou empurrar contra ela não os levanta, eles pulam para dentro dela; depois de alguns pulos que não dão certo, eles tentam outro jeito de subir. Uma escada que começa de um piso é subida a partir daquele piso, não é pulada de baixo. Escadas empilhadas uma em cima da outra são subidas até o topo. Se a escada para um pouco antes, eles pulam do topo para o piso ou a beirada ao lado, ou para cima de um muro e de lá para o piso. Uma torre de escadas curtas com patamares no meio é subida como uma escada só: eles planejam a corrente inteira primeiro, vão de cada topo direto para o pé seguinte, e lembram as correntes que funcionaram (depois de uma queda eles voltam direto para cima). Descer funciona do mesmo jeito. Eles escolhem uma escada cujo pé eles consigam alcançar andando no próprio nível e cujo topo os deixe o mais perto possível de onde vão, e sobem uma corrente de escadas uma atrás da outra. Uma escada que não funcionou, ou que sempre os deixa de volta onde começaram, fica de lado por alguns minutos.
- **Seguindo o seu caminho:** quando você está em outro andar e eles não conseguem achar o caminho sozinhos (uma torre apertada de escadas curtas e pisos pequenos), eles vão até o último lugar onde você estava no nível deles e seguem os seus passos exatos dali, entrando em cada escada do jeito que você entrou e pulando onde você pulou. Se você pulou de algo que custaria a eles mais de 30% da vida, eles procuram outro jeito de descer.
- **Beiradas:** eles não andam para fora de uma queda de mais de 3 m (Movement > MaxDrop) sobre chão seco — eles param na beirada e procuram uma escada, degraus ou uma queda segura.
- **Descer:** eles usam a escada ou dão a volta quando existe, ou descem pela escada de mão por onde subiram. Só quando não há outro jeito de descer eles pulam uma queda, de até 8 m (Movement > StuckDrop) e nunca uma que custaria mais de 30% da vida deles naquele momento. Um vão sobre uma queda perigosa só é saltado com folga.
- **Dano de queda:** companheiros tomam dano de queda como um jogador: nenhum até 4 m, e maior quanto mais longe caírem (o efeito Feather Fall ajuda eles também).
- **Água:** eles pulam riachos estreitos e vãos pequenos. Eles contornam a água quando o caminho seco não é muito mais longo, e senão nadam.
- **Andar ou correr:** eles andam enquanto trabalham e correm quando o que estão indo buscar está a mais de 25 m.
- **Fogo e piche:** eles evitam fogo e poços de piche.

## Habilidades

Os companheiros ficam melhores no que fazem, como um jogador. Lutar aumenta as habilidades de arma deles, e os trabalhos aumentam as habilidades correspondentes (corte de madeira, picaretas, pesca, furtividade…). Quando uma habilidade sobe de nível, você recebe uma mensagem.

## Multiplayer

- **O seu companheiro continua seu.** Às vezes o jogo de outro jogador é quem roda o seu companheiro (o jogo entrega os personagens para o computador do jogador por perto que ele quiser). Mesmo assim, ele segue você, dorme quando você dorme e senta quando você senta.
- **As suas ordens sempre funcionam.** Quando você dá uma ordem ou abre a roda dele, o seu jogo assume o controle dele.
- **Baús compartilhados são seguros.** Itens que um companheiro move são salvos corretamente, sem perdas nem duplicação. Ele não usa um baú que outro jogador deixou aberto.
- **O crédito vai para o dono.** As plantações que ele planta e as pilhas que ele constrói contam como construídas pelo dono dele.
- **Todo mundo precisa do mod, inclusive o servidor.** Num servidor dedicado, instale o mod no servidor e no jogo de cada jogador. Um servidor (ou um anfitrião) sem o mod apaga qualquer companheiro que carregar. Você pode trazer um companheiro apagado de volta pela lista (veja [Recuperando um companheiro perdido](#recuperando-um-companheiro-perdido)), mas é melhor não perdê-lo. Um servidor dedicado sempre mantém carregada a área em volta do centro do mundo, então companheiros perto do ponto inicial do mundo correm risco mesmo com todo mundo online. Jogadores sem o mod simplesmente não conseguem ver os companheiros.

## Solução de problemas

**O meu companheiro só está parado ali.**
Normalmente o trabalho dele não tem nada para fazer, e ele começa a ficar à toa em alguns segundos. Motivos comuns:
- **Cozinhar:** ele está deixando a reserva de ingredientes para você.
- **Coleta:** os itens chegaram aos limites.
- **Plantar:** não há terra cultivada nas zonas dele.
- **Fundir:** ele não tem insumo nos baús.

Confira o log (abaixo) para saber o motivo.

**O meu companheiro sumiu.**
Use o Santuário de Odin, ou ligue **General > ShowCompanionRoster**, para ver o status dele. Se estiver Perdido ou Desconhecido, veja [Recuperando um companheiro perdido](#recuperando-um-companheiro-perdido).

**Ele não fez nada enquanto eu estava fora.**
O jogo descarrega as partes do mundo onde não há nenhum jogador, e os companheiros ali congelam até alguém voltar. Um companheiro só trabalha enquanto você (ou outro jogador) está perto o bastante para a área dele ficar carregada.

**Ele não vai dormir.**
Ele precisa de uma cama livre na área da casa dele (defina uma casa primeiro) e tem que estar a até 50 m da cama.

**Ele vive indo até uma bancada para consertar.**
Em casa, o equipamento é consertado quando cai abaixo de 80%. Você pode mudar `HomeThreshold` nas configurações de conserto de equipamento.

**Ele ignora um baú.**
Esse baú pode estar aberto, ser privado (construído por outro jogador), estar atrás de uma área protegida, ou fora do alcance dele.

**Lendo o log**
Os companheiros escrevem o que estão fazendo e por quê em `BepInEx/LogOutput.log`, por exemplo "Nothing to cook right now" ou "No station within 20m can repair…". Se algo parecer errado, esse arquivo é a melhor coisa para incluir num relato de bug.
