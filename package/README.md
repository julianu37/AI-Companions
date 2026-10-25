# AI Companions

![AI Companions — companheiros NPC com IA para Valheim, em PT-BR](https://raw.githubusercontent.com/julianu37/AI-Companions/main/docs/images/banner.png)

Um **companheiro NPC com IA** para **Valheim 1.0**, **em português do Brasil** — você **fala ou digita** e ele
entende e executa.

Este é um **projeto independente** baseado em **[Offline Companions Reborn](https://github.com/MaddhattrRocks/Offline-Companions-Reborn)**
(de **Maddhattr**), que por sua vez é a atualização para o Valheim 1.0
do **Offline Companions** de **ProfMags**.

> **Todo o crédito do conceito, do design e do código original é do ProfMags** (e da atualização, do Maddhattr).
> O companheiro, os trabalhos, a IA de movimento/combate, a interface e a base de tudo isso vêm de lá.
> **O que é nosso:** a **tradução completa para PT-BR** e a **camada de IA** (LLM + voz).

> **Código-fonte:** [julianu37/AI-Companions](https://github.com/julianu37/AI-Companions)
> — guia, referência de configurações e relato de bugs.

---

## 🇧🇷 O que nós adicionamos

### 🗣️ Ordens por voz e por texto (IA)

**Segure `V`**, fale e solte — ou digite no chat. A fala é transcrita **dentro do jogo** e o texto entra no
mesmo caminho das ordens digitadas. Exemplos:

`colete 10 madeiras` · `pega 10 pedras` · `minere ferro` · `segue` · `para` · `para de me seguir` · `vem cá` ·
`ataca o greydwarf` · `mata o javali` · `equipa o machado de bronze` · `pega a picareta` · `larga a pedra` ·
`joga fora 5 madeira` · `abre o teu inventário` · `fica na defensiva` · `bom dia` · `como você está?`

- O número da ordem é um **alvo** contado no **inventário dele**: se ele já tem 10 madeiras e você pede 10,
  ele avisa *"Já temos o suficiente."* e não sai para coletar.
- **Ver o que ele carrega:** `abre o teu inventário` / `mostra o que você carrega` abre a **janela do inventário
  do companheiro** (a mesma do toque na tecla de interagir); `fecha o inventário` fecha.
- **A voz é local:** o reconhecimento de fala e a **voz do companheiro** rodam **na sua máquina** — nada disso
  é enviado para fora. O **LLM** (que entende a ordem) usa o **Groq** por padrão, um **serviço remoto**:
  o texto das ordens e da conversa **sai da sua máquina**. Para não enviar nada, use o **LM Studio** local
  (`[AI] BaseUrl = http://127.0.0.1:1234/v1`).

### 🔊 Ele fala (voz sintetizada local)

O companheiro **fala em voz alta** o que responde, na posição dele no mundo. A voz é sintetizada **dentro do
jogo** pelo **Kokoro-82M** (via `sherpa-onnx`, CPU) — sem serviço externo. Três vozes em **português do Brasil**:
`42` = `pf_dora` (feminina, padrão), `43` = `pm_alex` (masculina), `44` = `pm_santa` (masculina).

- **O que ele fala:** a resposta do modelo (*"Deixa comigo."*) e/ou a linha do mod que aparece no chat
  (*"Ok: coletar madeira (10)."*, *"Já temos o suficiente."*) — duas opções separadas
  (`TtsSpeakReplies` / `TtsSpeakModLines`). Numa ordem a linha do mod ganha (a voz acompanha o que está
  escrito na tela); desligue `TtsSpeakModLines` para ouvir a resposta do modelo. Nada é falado duas vezes.
- **Sem travar o jogo:** a síntese roda em thread de fundo, fala por **frases** (a primeira já toca enquanto a
  próxima é gerada) e as falas repetidas saem do **cache**.
- **Teste rápido** no console do jogo (`F5`): `modia_tts_teste` (ou `modia_tts_teste Bom dia, chefe!`).

### 🧠 Personalidade, memória e conversa

- **Personalidade** por preset (`viking`, `mago`, `sarcastico`, `guia`) ou **texto livre** — ele responde no tom.
- **Memória curta** das últimas trocas, por companheiro.
- **Conversa livre:** pergunte algo e ele responde como personagem.

### ⚙️ Ajustes nossos

- **Raio de coleta maior** (madeira/pedra/minério até 25 m de você; drops catados até 12 m).
- **Sem falinhas aleatórias** a cada 15–30 s (as falas úteis continuam).
- **A tecla `V` é do push-to-talk** e não dispara o *Toggle Auto-Pickup* do jogo.
- **Interface, menus e falas 100% em PT-BR** — sobre um mod **multilíngue** (o **inglês é o idioma nativo**: os
  painéis seguem o idioma do jogo e um idioma novo é **um arquivo**).

---

## O que este mod faz

Ele adiciona **aliados NPC persistentes** ao Valheim. Um companheiro é oferecido a você na primeira vez que
entra num mundo.

Companheiros **não são bichinhos de estimação** — são colegas de equipe com inventário, equipamento, comida,
resistência, habilidades e fala próprios. Aponte para o trabalho e eles fazem: lutam ao seu lado, cortam lenha,
mineram, coletam, cultivam, pescam, caçam, cozinham e preparam bebidas, operam suas fundições, cuidam da sua base,
puxam sua carroça, sentam perto da fogueira, dormem em camas e seguem você através de portais.

Tudo (aparência, inventário, equipamento, trabalho, casa, habilidades, propriedade) é **salvo com o mundo** e
sobrevive a sessões, trocas de zona, reinícios de servidor e mortes.

O guia completo está em `GUIDE.md` e a referência de todas as configurações em `CONFIGURATION.md`.

<details>
<summary><b>Comandando seus companheiros</b></summary>

- **Toque** a tecla de interagir num companheiro para abrir o painel dele: inventário, equipamento, comida e nome.
- **Segure** a tecla de interagir para abrir a **roda de comandos**: escolha um trabalho, ligue/desligue o
  *seguir*, defina a casa dele e alterne *Vagar*, *Recolher* e *Comando*. O anel interno tem as posturas de combate.
- **Aponte e mande:** mire em algo e aperte a tecla de comando (`Z` por padrão). Ele deduz pelo contexto:
  inimigo — atacar · recurso — coletar · carroça — puxar · navio — embarcar · cama — dormir · fogueira — sentar ·
  porta — abrir · baú — guardar as coisas · bancada — consertar o equipamento · lápide — recuperar · chão — ir até lá.
- **Segure** a tecla de comando para chamar os companheiros próximos; continue segurando por alguns segundos para
  chamar **todos**, a qualquer distância.
- **Suas ordens vêm primeiro:** enquanto ele cumpre uma ordem, o trabalho espera e retoma depois.
- **Definir casa** marca onde ele mora: ele reivindica uma cama perto, traz ferramentas para lá, esvazia as bolsas
  e trabalha em volta.

</details>

<details>
<summary><b>Combate</b></summary>

Os companheiros lutam com uma IA defensiva: levantam o escudo e bloqueiam, dão parry e contra-atacam, e trocam para
o melhor escudo de bloqueio quando vários inimigos atacam ao mesmo tempo. Escolhem a arma pelas **fraquezas e
resistências** do inimigo, não pelo dano bruto, e abrem com tocha contra inimigos fracos a fogo.

Arcos e bestas são usados quando há munição; bestas são carregadas corretamente, e eles voltam para o corpo a corpo
quando o inimigo se aproxima.

Eles não arrumam briga com criaturas que estão na delas: por padrão atacam depois que a criatura percebe você ou
ele. Qualquer coisa que os ataque é revidada. Abaixo de certo nível de vida lutam na defensiva; com vida muito
baixa, correm.

**Posturas** (anel interno da roda):

| Postura | Comportamento |
|:---|:---|
| **Equilibrado** | Escolhe corpo a corpo ou à distância conforme a situação |
| **Passivo** | Nunca começa briga; só segue ou trabalha |
| **À distância** | Prefere arco/besta e mantém distância |
| **Corpo a corpo** | Fica nas armas de perto |

Depois da luta, ele vai buscar o que o abate deixou (com *Recolher* ligado).

</details>

<details>
<summary><b>Coleta: madeira, pedra, minério e forrageio</b></summary>

- **Seguindo você**, ele vai atrás de árvores, rochas ou plantas **perto de você** enquanto vocês andam
  (até 25 m por padrão) — e segue você quando não há nada por perto.
- **Sem seguir**, ele trabalha uma **área**: em volta de casa, se o trabalho começou perto dela; senão, em volta
  de onde você o deixou (até 60 m).
- **O quê e quanto:** as listas e quantidades decidem o que ele coleta e onde para. Itens fora da lista ficam no chão.
- **Início de jogo:** sem machado de bronze e sem forja, ele também recolhe **galhos, pedras e sílex** do chão.
- **Segurança:** ele se afasta de árvores caindo e não derruba árvores coladas nas suas construções.
- **Bolsa cheia:** ele descarrega num baú (ou carroça/navio por perto).

</details>

<details>
<summary><b>Caça e pesca</b></summary>

**Caçar**
- Vai atrás de criaturas que dão carne, até ter o suficiente de cada tipo. Nunca caça animais domados, chefes nem
  outros companheiros.
- Chega de mansinho em bichos ariscos (como cervos).
- Atira em pássaros por penas (precisa de arco/besta).
- Recolhe o que caiu e leva para casa quando a bolsa enche.

**Pescar**
- Lança com vara e isca, escolhendo a melhor isca para os peixes por perto.
- Espera o fisgar, dá o golpe e recolhe olhando a linha.
- Cada tipo de peixe tem uma meta — ele para quando você já tem o suficiente.

</details>

<details>
<summary><b>Fazenda</b></summary>

- Colhe as plantações maduras, fileira por fileira, e guarda a colheita.
- Replanta nas **zonas de fazenda** que você marca com o editor (`LeftAlt` + `Z` por padrão). A zona pode ser de
  uma cultura específica ou "qualquer".
- Mantém uma **reserva de sementes** — planta sementes para repor o estoque e nunca usa as suas últimas.
- Respeita as áreas protegidas (wards), como um jogador construindo.

</details>

<details>
<summary><b>Fundição e cozinha</b></summary>

**Fundir**
- Mantém fornos, fundições e carvoeiras abastecidos com minério e combustível dos baús próximos (e de casa).
- Recolhe o produto e guarda.

**Cozinhar**
- Usa **todos** os tipos de estação como um jogador: caldeirão, mesa de preparo, alambique de hidromel, espeto,
  estação de ferro, forno, barris fermentadores e colmeias.
- Cozinha até cada comida atingir a meta e sempre deixa uma reserva de ingredientes para você.
- Prepara hidromel em lotes, enche e tira dos barris, e organiza a comida cozida junto da mesma espécie.

</details>

<details>
<summary><b>Reparar e reabastecer (cuidando da sua base)</b></summary>

Um trabalho para o companheiro que fica em casa (ou num posto avançado) — desde que você (ou outro jogador) esteja
perto o bastante para a área continuar carregada:

- **Reparar:** conserta peças danificadas com um martelo, um golpe por vez, como um jogador.
- **Reabastecer:** mantém fogueiras, braseiros, tochas e banheiras quentes abastecidos.
- **Colmeias:** esvazia e guarda o mel.
- **Pilhas:** empilha madeira e pedra sobrando dos baús junto das pilhas que você já fez.
- **Organizar:** arruma os baús "igual com igual", levando cada item para o baú que já tem mais daquele item.

Ele busca o que precisa (martelo, combustível) nos baús e devolve o que sobra.

</details>

<details>
<summary><b>Quando não há nada para fazer</b></summary>

Quando o trabalho acaba e ele não está seguindo você, ele fica por perto em vez de parar no lugar: faz pequenas
tarefas, senta numa cadeira ou no chão, dá uma volta olhando as estações e baús, ou vem conversar. Ele volta ao
trabalho no instante em que houver algo a fazer.

</details>

<details>
<summary><b>Equipamento, ferramentas e conserto</b></summary>

- Cada companheiro tem inventário completo e equipa a melhor arma, escudo e armadura que carrega.
- **Ferramentas:** os trabalhos pegam a ferramenta que precisam (machado, picareta, cultivador, vara e isca,
  martelo…) nos baús de casa e devolvem depois.
- **Conserto:** equipamento gasto é levado a uma bancada que um jogador poderia usar.
- **Bolsas:** ao trocar de trabalho ou voltar para casa, ele esvazia o que recolheu nos baús.

</details>

<details>
<summary><b>Comida, descanso, camas e morte</b></summary>

- **Comida:** ele come em até três espaços, como um jogador, usando a comida do próprio inventário. Bebe hidroméis
  de cura e resistência quando precisa.
- **Descanso:** ganha o efeito *Descansado* perto de uma fogueira — e senta com você quando você senta.
- **Camas:** cada companheiro reivindica uma cama, que mostra o nome dele. Vai dormir à noite (e quando você dorme)
  e volta ao trabalho de manhã.
- **Morte:** deixa uma lápide no mapa até ser esvaziada; volta depois de um tempo na cama (ou ao seu lado) e
  recolhe o próprio equipamento quando chega perto da lápide.

</details>

<details>
<summary><b>Multiplayer</b></summary>

- **Seu companheiro continua seu:** ele trata o dono como o seu jogador — segue você, dorme quando você dorme e
  cumpre as suas ordens, não importa qual computador o esteja rodando.
- **Baús são seguros de compartilhar:** ele não usa baú aberto, baú privado de outro jogador nem baú atrás de uma
  área protegida que ele não pode abrir. Itens movidos são salvos corretamente, sem perdas nem duplicação.
- **Instale em todo lugar:** num servidor dedicado, o servidor também precisa do mod.

</details>

<details>
<summary><b>Configurações</b></summary>

Tudo fica em `BepInEx/config/AiCompanions.cfg` e também pode ser mudado **em jogo** com o
painel de configurações (**`F8`** por padrão). Cada opção tem descrição clara.

> **Atualizando para a 2.0.0?** O arquivo de configuração mudou de nome (era `Maddhattr.OfflineCompanionsReborn.cfg`,
> que **não é mais lido** e pode ser apagado) e as **configurações voltam aos padrões** — inclusive a **`ApiKey`**,
> que precisa ser colada de novo. A seção da IA virou **`[AI]`** e as **chaves são em inglês** (`Enabled`, `Model`,
> `TtsVoice`…). **Save, mundo, companheiros, casas e inventário não são afetados** (o GUID do plugin e as chaves
> `HC_*` não mudaram).

A camada de IA tem o seu **próprio painel** (tecla **`I`** por padrão, mudável em `[AI] PanelKey`): as
**configurações do LLM** (URL, modelo, chave e o **Testar conexão**), personalidade (preset ou texto livre), voz
do companheiro (com o botão **Ouvir**), volume, memória, conversa livre, os toggles do que é
falado e a tecla do **push-to-talk** (botão *Alterar*). Tudo é salvo no `.cfg` na hora.
Ele abre sem precisar de companheiro perto e fecha com `Esc`.

Na seção **Configurações do LLM**, o dropdown **"Selecione seu Provedor LLM"** (acima da URL do servidor)
**preenche a URL e o modelo** de **Groq**, **LM Studio** ou **OpenRouter** — a chave não é tocada e nada vale antes
do **Aplicar**; uma URL de Ollama/servidor próprio aparece como **Personalizado (URL manual)**. Ao lado do campo da
**Chave de API**, o botão **"Crie sua Chave de API Grátis AQUI"** abre a página da chave no navegador (some no LM
Studio, que não tem chave). A **velocidade da fala** ficou **só no painel `F8`**.

As seções incluem **General, Controls, Follow, Movement, Combat, Food, Stamina, Rest, Death, Looting, Gear Repair**,
uma por trabalho (**Gathering, Hunting, Fishing, Farming, Smelting, Cooking, Repair and Restock**), **Idle,
Speech** e **Advanced** — além das nossas em **`[AI]`** (ordens por texto/voz, personalidade, memória, Whisper).

**Listas** (o que coletar, cozinhar, caçar, plantar…) usam o mesmo formato:
- `*` = tudo
- `Nome` = só esse
- `-Nome` = nunca esse
- `Nome:Número` = esse, com a sua própria quantidade

Ao atualizar de uma versão antiga, as opções que você mudou são migradas automaticamente — **exceto na 2.0.0**,
que troca o arquivo (novo nome, chaves em inglês) e por isso **volta aos padrões**: é só reconfigurar (a tabela de
todas as chaves está na **[wiki, em Configuração](https://github.com/julianu37/AI-Companions/wiki/Configuracao)**).

</details>

---

## Instalação

1. Instale o **BepInExPack_Valheim** (BepInEx 5.4.23.5 para o Valheim 1.0.x).
2. Copie a pasta do mod para `BepInEx/plugins/AI-Companions` (ou use um gerenciador de mods).
3. **Para as ordens por voz:** deixe um modelo do Whisper (ex. `ggml-small-q5_1.bin`) em
   `.../AI-Companions/whisper/`.
4. **Para ele falar:** o modelo do Kokoro já vem no pacote, em `.../AI-Companions/tts/kokoro/`.
5. **Para a IA:** crie a **chave gratuita** em **[console.groq.com/keys](https://console.groq.com/keys)** e cole em
   `[AI] ApiKey`. O `BaseUrl` (`https://api.groq.com/openai/v1`) e o `Model` (`openai/gpt-oss-20b`) **já vêm
   configurados**. Quer rodar local? Instale o **[LM Studio](https://lmstudio.ai/)**, carregue o
   `qwen2.5-3b-instruct`, ligue o servidor e use `[AI] BaseUrl = http://127.0.0.1:1234/v1` com o `ApiKey`
   **vazio**. No jogo, o **Testar conexão** (painel `I`) diz se a URL responde; a **chave** só é validada numa
   ordem de verdade.

**Requisitos:** Valheim **1.0.x** (testado na **1.0.20**) + BepInEx (o BepInExPack já traz o Harmony). Para as ordens, um **LLM**:
**Groq** (só a chave gratuita e internet), **LM Studio** local (opcional) ou **OpenRouter** (alternativa). As
ordens por voz exigem **Windows x64** e um **microfone**.

**Multiplayer:** todo jogador precisa do mod, e o servidor dedicado também.

## Planos futuros

- Continuar melhorando a IA do companheiro e as ordens em linguagem natural.
- Mais recursos de voz: **mais vozes** e escolha de voz pelo menu radial (o **painel da IA**, tecla `I`, já permite
  escolher a voz e a personalidade **em jogo** desde a Fase 3.7).
- Mais recursos de multiplayer.
- Revisão de habilidades e atributos em cima dos de um jogador.

## Problemas conhecidos

- **Tipos de companheiro:** os **dverger** (**Mago**, **Mago de Fogo**, **Mago de Gelo** e **Suporte**) são **de
  combate** — a roda mostra só as ações de combate/suporte e as ordens de **trabalho** (pelo chat, pela voz ou pela
  tecla de comando) são **recusadas** com *"Sou de combate, não faço esse trabalho"*. A mochila que eles usam por
  conta própria **não é salva** (o que você coloca na janela fica) e o painel esconde os espaços de comida por tipo.
  Para trabalhar, use o **Companheiro**.
- **Volume da voz:** a fala do companheiro **não** passa pelo mixer do jogo, então ela **ignora os sliders de volume do
  Valheim** (música/SFX). Quem manda no volume dela é o `[AI] TtsVolume` (ou o slider do painel da IA).
- *(Corrigido na 1.6.0)* **Painel `F8`:** o clique do mouse não respondia numa faixa dos controles (havia um alvo de
  clique invisível do HUD do jogo **na frente** do painel — o HUD usa `sortingOrder` 400 e os painéis do mod estavam
  abaixo). Agora **todos** os canvases do mod ficam na faixa **1701+**, acima do HUD. O painel da IA (tecla `I`) nunca
  teve o problema.

## Código-fonte

Código completo no GitHub: **[julianu37/AI-Companions](https://github.com/julianu37/AI-Companions)**.

## Licença

**MIT** — ver `LICENSE`. É a mesma licença do Offline Companions original; mantemos o aviso de copyright do
ProfMags (e do Maddhattr) junto das nossas mudanças, como manda a licença.

## Aviso

Este é uma **versão independente** de um mod da comunidade, mantido para uso pessoal/estudo em PT-BR, com a nossa
camada de IA por cima. Não é afiliado nem endossado pelo ProfMags, pelo Maddhattr ou pela Iron Gate.
Se algum dos autores quiser que algo seja removido, alterado ou incorporado, é só pedir.

Parte do código foi escrita com auxílio de ferramentas de IA, com instrução específica, revisão e testes antes de
cada versão.
