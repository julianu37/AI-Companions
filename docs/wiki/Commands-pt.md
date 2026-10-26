> 🌍 [English](Commands) · **Português** · [Español](Commands-es) · [Français](Commands-fr) · [Italiano](Commands-it)

# Comandos

Teclas, ordens por chat/voz e comandos de console.

---

## Teclas

| Tecla | Ação |
|---|---|
| **`V`** | **Push-to-talk**: segure e fale; solte para ele entender |
| **`Z`** | **Tecla de comando**: olhe para algo e toque para mandar os companheiros (mover / atacar / abrir porta / sentar / consertar). **Segure** para chamá-los |
| **`E`** | **Segure** olhando um companheiro para abrir o **menu radial**; **toque** para abrir o painel / inventário dele |
| **`LeftAlt` + `Z`** | Editor de zona de fazenda |
| **`I`** | **Painel da IA**: uma **aba por companheiro** (nome, tipo, Chamar, Aparência, voz e personalidade dele) e a **aba IA** (invocador de companheiros, Configurações do LLM com o **seletor de provedor**, volume, memória, conversa e push-to-talk) — tudo salvo na hora. A **velocidade da fala** fica no **`F8`** |
| **`F8`** | Painel de configurações do mod (todas as opções, em jogo) |
| **`F5`** | Console do jogo (para os comandos de teste do mod) |

> O painel da IA (tecla **`I`**, mudável em `[AI] PanelKey`) abre **sem precisar de companheiro perto**, fecha
> com **`I`**, **`Esc`** ou no **X**, e cada controle é gravado na hora (no `.cfg`, quando é uma opção global; no
> companheiro, quando é da aba dele). Enquanto ele está aberto, o seu personagem não responde aos controles, e a
> tecla é ignorada com o inventário, o mapa, o menu ou o painel `F8` abertos.

> **Tipos:** os **dverger** (**Mago**, **Mago de Fogo**, **Mago de Gelo** e **Suporte**) são **de combate** — a roda
> mostra só as ações de combate/suporte e as ordens de **trabalho** (**chat**, **voz** ou **tecla de comando**) são
> recusadas com *"Sou de combate, não faço esse trabalho"*. Para trabalhos, use o **Companheiro**
> (**[Como funciona](How-it-works-pt#tipos-de-companheiro)**).

![O painel da IA na aba de um companheiro: as abas no topo (IA e uma por companheiro), o Nome e o Tipo com Trocar tipo, o Estado, Chamar e Aparência, e "Voz e personalidade deste companheiro" com a voz e o botão Ouvir](images/1-abas-do-companheiro.jpg)

### Menu radial (segure `E`)

Segure **`E`** olhando um companheiro para abrir a roda de ações (mover, atacar, abrir porta, sentar, consertar…).
Um **toque** em `E` abre o painel / inventário dele.

![O menu radial aberto, com as ações em volta do centro](images/4-menu-radial.jpg)

---

## Ordens por chat e por voz

Você digita no chat do jogo **ou** fala segurando **`V`**. A ordem é interpretada por um LLM: você pode dizer a
mesma coisa de **várias formas** — não precisa decorar a frase exata.

### Pelo nome

Comece a mensagem com o **nome** de um companheiro para falar **só** com ele: `Gruncha, colete 10 pedras` (o
prompt, a personalidade, a memória e a ordem são os dele). **Sem** o nome, a ordem vale para **todos** os que
puderem atendê-la. Um começo que parece nome e não existe (`Fulano, colete 10 pedras`) é avisado — e nada é
ordenado —, mas uma forma de tratamento continua valendo (`mano, colete 10 pedras`).

> O nome é o que você deu na **aba do companheiro** (painel **`I`**) ou o nome do tipo, se ele nunca foi renomeado.

### Seguir e parar

| Você diz | O que ele faz |
|---|---|
| `segue` | Volta a seguir você |
| `para` / `para de me seguir` | Para o que está fazendo e fica parado — e a resposta diz **o trabalho** que ele parou ("parar de coletar madeira", "parar de cozinhar") |
| `vem cá` | Vem até você |

> Não existe um "seguir de perto" separado: as opções são **seguir** você ou **parar**.

### Coletar

| Você diz | O que ele faz |
|---|---|
| `colete 10 madeiras` / `corta umas madeiras` | Coleta **madeira** |
| `pega 10 pedras` / `colete 10 pedras` | Coleta **pedra** |
| `minere ferro` / `minere minério` | **Minera** minério |
| `colhe as frutinhas` / `pega os cogumelos` | **Forrageia** (plantas, frutas, cogumelos) |

> **O número é um alvo.** Ele conta **no inventário do companheiro** (a mochila dele — não conta baús nem
> pilhas no chão). Se ele já tem 10 madeiras e você pede 10, ele avisa *"Já temos o suficiente."* e não sai
> para coletar. O alvo vale **só para o recurso pedido**.

### Atacar

| Você diz | O que ele faz |
|---|---|
| `ataca o greydwarf` | Ataca a criatura **citada** (mesmo na postura Passiva) |
| `mata o javali` | Idem |
| `ataca o troll` | Idem |

> A criatura é procurada **primeiro na sua mira**; depois, a mais próxima dentro de `[AI] TargetRange` (padrão
> 40 m). A que você está mirando vale **sempre**, por mais longe que esteja. *"Atacar"* sem citar criatura
> vira **caça**.

### Equipar e largar

| Você diz | O que ele faz |
|---|---|
| `equipa o machado de bronze` / `pega a picareta` | Equipa o item **que ele já carrega** |
| `larga a pedra` / `joga fora 5 madeira` | Larga o item no chão (sem número = a pilha inteira) |

> `equip` e `drop` valem só para o que ele **já tem na mochila**. Item de missão nunca cai.

### Itens no chão

| Você diz | O que ele faz |
|---|---|
| `recolhe os itens` / `junte a madeira do chão` | Anda até os itens soltos e pega **um por um** |
| `pega o que está no chão` | Idem — ele pega o que estiver **mais perto**, não um item escolhido pelo nome |
| `pare de recolher` / `não pegue nada` | **Desliga** o recolhimento automático |

> O recolhimento pega **inclusive o que ele mesmo largou de propósito** — que é justamente o que o ímã
> automático do jogo ignora. Ele desiste se o item não couber inteiro na mochila ou se ficar longe demais.

### Inventário

| Você diz | O que ele faz |
|---|---|
| `abre o teu inventário` / `mostra o que você carrega` | Abre a **janela do inventário dele** para você ver |
| `fecha o inventário` | Fecha a janela (o **X** da janela fecha sempre) |

> Fechar é uma ação própria (`inventory_close`): se o modelo devolver "abrir" para uma frase com verbo de fechar,
> o mod corrige pela **sua** frase. O **X** da janela e o `Esc` fecham sempre.

![O inventário do companheiro aberto: a janela "Companheira" com o que ela carrega e os quatro espaços de Comida, ao lado do seu inventário e do painel de produção; no meio, o hover com [E] Inventário e [Segure E] Comandos](images/5-inventario-do-companheiro.jpg)

### Postura de combate

| Você diz | O que ele faz |
|---|---|
| `fica na defensiva` | Postura **passiva** (defensiva) |
| `fica equilibrado` | Postura **equilibrada** |
| `ataca à distância` | Postura **à distância** |
| `parte pra cima` | Postura **corpo a corpo** |

### Trabalhos

| Você diz | O que ele faz |
|---|---|
| `cuida da plantação` | Trabalho de **fazenda** |
| `vai pescar` | Trabalho de **pesca** |
| `conserta as construções` | Trabalho de **reparo** |
| `vai cozinhar` | Trabalho de **cozinha** |
| `funde o minério` | Trabalho de **fundição** |
| `vai caçar` | Trabalho de **caça** |
| `define a casa aqui` | Define a **casa** |
| `vaga por aqui` / `para de vagar` | Liga/desliga o **vagar** |
| `aceita comandos` / `ignora comandos` | Liga/desliga **aceitar ordens** |

### Conversa

| Você diz | O que ele faz |
|---|---|
| `bom dia` | Responde como personagem |
| `como você está?` | Idem |
| `o que achou da minha casa?` | Idem |

> Quando a mensagem **não** é uma ordem, o companheiro **conversa**: fala a resposta em voz alta e a mostra no
> chat. Isso é controlado por `[AI] Conversation` (padrão `true`). Desligado = só ordens são entendidas.

---

## Comandos de console (`F5`)

| Comando | O que faz |
|---|---|
| `modia_tts_teste` | Fala uma frase de teste pelo TTS (pipeline completo: modelo, síntese e reprodução) |
| `modia_tts_teste <texto>` | Fala o texto que você escrever — ex.: `modia_tts_teste Bom dia, chefe!` |
| `modia_stt_teste` | Roda o reconhecimento do Whisper sobre um WAV, **sem** usar o microfone |

> `modia_stt_teste` transcreve o arquivo de `[AI] SttWavTest` (padrão `whisper\jfk.wav`) e escreve o resultado
> no log. Serve para checar se o modelo do Whisper está carregando, quando as ordens por voz não saem.
>
> **O pacote não traz esse WAV** (ele saiu do pacote para economizar espaço) — ponha um arquivo seu na pasta
> `whisper\` e aponte `[AI] SttWavTest` para ele. O comando **não** aceita o caminho como argumento.

---

## Notas importantes

- **O idioma segue o jogo.** Desde a 1.9.0 a interface, as falas, os presets de personalidade e o contrato do LLM
  estão em **cinco idiomas** (PT-BR, EN, ES, FR, IT) e seguem o **idioma do jogo** — as ordens são entendidas nesse
  idioma também, e a voz e o reconhecimento da fala são **automáticos** (`auto` / `0`).
- **O número é um alvo contado na mochila dele.** `colete 10` = **ter 10 no inventário do companheiro**. Com
  15, ele nem começa e diz que já tem o suficiente; com 7, ele coleta até 10 (a última árvore pode passar).
- **A ordem é lida pelo LLM**, então você pode falar do seu jeito. `pega 10 pedras`, `vou precisar de pedra`,
  `junta umas pedras` — todas devem cair na mesma ação.
- **`[AI] Prefix`** (padrão vazio): se você preencher, por exemplo, `/c`, **só** as mensagens que começam com
  esse prefixo vão para o modelo. O resto do chat fica em paz.
- **`[AI] CooldownSeconds`** (padrão `3`): tempo mínimo entre duas ordens, para uma rajada de mensagens não
  inundar o modelo.
- **O chat e a voz entram pelo mesmo caminho.** O que funciona digitado funciona falado, com personalidade,
  memória e conversa iguais.
- **`despawn` não é exposto ao modelo.** Você não pode mandar o companheiro desaparecer por chat ou voz.

---

## Ajustes de conveniência do mod

- **A tecla `V`** não dispara mais o *Toggle Auto-Pickup* do jogo (veja **[Voz](Voice-pt)**).
- **Raio de coleta maior:** madeira/pedra/minério são procurados a até **25 m** de você, e os drops são
  catados a até **12 m**.
- **Chatter aleatório desligado** por padrão: as falinhas soltas a cada 15–30 s não acontecem mais. As falas
  úteis (ordens, mudança de modo, respostas da IA, eventos) continuam.

Para ver todas as opções, vá em **[Configuração](Configuration-pt)**.

<!-- D4: add Español/Français/Italiano -->
