# ModAI-BR-Companions

![ModAI-BR-Companions — companheiros NPC com IA para Valheim, em PT-BR](docs/images/banner.png)

> Um **companheiro NPC com IA** para **Valheim 1.0**, **em português do Brasil** — você **fala ou digita** e
> ele entende e executa.

Este projeto é um **fork** de [Offline Companions Reborn](https://github.com/MaddhattrRocks/Offline-Companions-Reborn)
(**MIT**) com duas coisas nossas por cima:

1. **Tradução completa para PT-BR** (interface, menus, painéis e as falas do companheiro).
2. **Uma camada de IA**: ordens em **linguagem natural** (texto no chat ou **voz** por push-to-talk), com
   **personalidade**, **memória** e **conversa** — usando **LLM e reconhecimento de voz locais**.

O companheiro em si (trabalhos, comida, stamina, habilidades, casa, descanso, portais, carroça, menu radial)
vem do upstream e continua funcionando igual — a IA é só uma **nova entrada** para as mesmas ações.

---

## O que ele faz

### 🗣️ Ordens por texto e por voz

| Como | Exemplo |
|---|---|
| **Voz** (segure **`V`**, fale, solte) | *"pega 10 pedras"* · *"corta umas madeiras"* · *"ataca o greydwarf"* |
| **Chat** (tecla de chat do jogo) | *"colete 10 madeiras"* · *"segue"* · *"para de me seguir"* · *"equipa o machado de bronze"* |

A fala é transcrita **dentro do jogo** (sem serviço externo de voz) e o texto entra no **mesmo** caminho das
ordens digitadas: ordem, conversa, personalidade e memória funcionam igual.

**Exemplos que funcionam hoje:**
`colete 10 madeiras` · `pega 10 pedras` · `minere ferro` · `segue` · `para` · `para de me seguir` ·
`vem cá` · `ataca o greydwarf` · `mata o javali` · `equipa o machado de bronze` · `pega a picareta` ·
`larga a pedra` · `joga fora 5 madeira` · `abre o teu inventário` · `fica na defensiva` · `bom dia` ·
`como você está?`

> O número da ordem é um **alvo** contado no **inventário dele**: se ele já tem 10 madeiras e você pede 10,
> ele avisa *"Já temos o suficiente."* e não sai para coletar.

> Peça para ver o que ele carrega (*"abre o teu inventário"*, *"mostra o que você carrega"*) e a **janela do
> inventário do companheiro** abre para você — a mesma do toque na tecla de interagir, com nome, comida e
> equipamento. *"Fecha o inventário"* fecha.

### 🧠 Personalidade, memória e conversa

- **Personalidade** por **preset** (`viking`, `mago`, `sarcastico`, `guia`) ou **texto livre** — entra no
  prompt do LLM, então ele responde no tom escolhido.
- **Memória curta** das últimas trocas (por companheiro).
- **Conversa livre**: *"bom dia"*, *"o que achou da minha casa?"* — ele responde como personagem.

### 🔊 Ele fala (voz sintetizada local)

O companheiro **fala em voz alta** o que ele responde, na posição dele no mundo (não no centro da tela). A voz é
sintetizada **dentro do jogo** pelo **Kokoro-82M** (via `sherpa-onnx`, CPU) — sem serviço externo, sem processo
filho, nada sai da sua máquina. Três vozes em **português do Brasil**:

| `TtsVoz` | Voz |
|---|---|
| **42** (padrão) | `pf_dora` — feminina |
| **43** | `pm_alex` — masculina |
| **44** | `pm_santa` — masculina |

> **O que ele fala:** a resposta do modelo (o jeito dele — *"Deixa comigo."*) e/ou a linha do próprio mod que
> aparece no chat (*"Ok: coletar madeira (10)."*, *"Já temos o suficiente."*). As duas opções são separadas
> (`TtsFalarRespostas` / `TtsFalarFalasDoMod`) e a regra é **a voz acompanha o que está escrito na tela**: numa
> ordem a linha do mod ganha (desligue `TtsFalarFalasDoMod` para ouvir a resposta do modelo no lugar dela). Nada
> é falado duas vezes.

> **Sem travar o jogo:** a síntese roda numa thread de fundo e a fala é montada por **frases** — a primeira frase
> já começa a tocar enquanto a próxima é gerada. Falas repetidas saem do **cache**, na hora. Teste rápido no
> console do jogo (`F5`): `modia_tts_teste`, ou `modia_tts_teste Bom dia, chefe!` para falar um texto seu.

### 🎛️ Painel da IA (tecla `I`)

Aperte **`I`** a qualquer momento (não precisa de companheiro perto) para ajustar a IA **em jogo**, sem editar o
`.cfg`:

| O que | Como |
|---|---|
| **Personalidade** | Escolha um **preset** (Padrão, Viking leal, Mago sarcástico, Sarcástico, Guia paciente) ou escreva nas **suas palavras** e clique em **Aplicar** — vale na próxima resposta |
| **Voz** | Escolha entre as 3 vozes pt-BR e aperte **Ouvir** para escutar uma frase de exemplo na hora |
| **Volume / Velocidade** | Sliders (o volume vale no próximo áudio, a velocidade na próxima fala) |
| **Push-to-talk** | Veja a tecla atual e aperte **Alterar** para capturar outra |
| **Liga/desliga** | Companheiro fala, falar respostas da IA, falar falas do mod, conversa livre e ordens por voz |
| **Memória** | Quantas trocas recentes ele lembra (0–12) |

Tudo é salvo no `.cfg` **na hora**. `Esc` ou o **X** fecham. Enquanto o painel está aberto, o seu personagem não
responde aos controles (e a tecla do push-to-talk não grava).

### ⚙️ Ajustes nossos em cima do upstream

- **Raio de coleta maior** (madeira/pedra/minério procurados até 25 m de você, drops catados até 12 m).
- **Chatter aleatório desligado** (as falinhas soltas a cada 15–30 s). As falas úteis continuam.
- **A tecla `V` é do push-to-talk**, sem disparar o *Toggle Auto-Pickup* do jogo (que continua disponível se
  você reatribuí-la nas configurações do Valheim).

---

## Como funciona (arquitetura)

```
voz (Microphone 16 kHz) ──► Whisper.net (STT, whisper.cpp in-process, CPU) ─┐
                                                                            ├─► mesma caixa de ordens ─► CompanionOrders ─► o companheiro executa
chat do jogo ───────────────────────────────────────────────────────────────┘            │
                                                                                          ├─► LLM local (LM Studio, API OpenAI-compatible) ─► ação + resposta
                                                                                          └─► Kokoro-82M (TTS, sherpa-onnx in-process, CPU) ─► ele FALA na posição dele
```

- **LLM local**: LM Studio (ou qualquer endpoint compatível com OpenAI) — usado para **entender** a ordem e
  **conversar**.
- **STT local**: `Whisper.net` (whisper.cpp) rodando **dentro do jogo**, com o modelo carregado uma vez.
- **TTS local**: `sherpa-onnx` com o modelo **Kokoro-82M** — o companheiro **fala** a resposta dentro do jogo,
  na posição dele, sem serviço externo.
- **Privacidade**: por padrão **nada sai da sua máquina** (LLM e voz são locais). Um provedor remoto pode ser
  configurado se você quiser.
- Nada disso bloqueia o jogo: tudo roda em threads de fundo.

---

## Requisitos

- **Valheim 1.0.x** (engine Unity 6) com **BepInEx 5.4.2351**.
- **LM Studio** com um modelo carregado e o servidor local ligado (padrão `http://localhost:1234/v1`).
- **Microfone** (só para as ordens por voz).
- **Windows x64** para o STT (os binários nativos do whisper.cpp são `win-x64`).

---

## Instalação

1. Instale o **BepInExPack_Valheim** e copie a pasta do mod para `BepInEx\plugins\ModAI-BR-Companions`.
2. (Opcional, para voz) deixe um modelo ggml do Whisper em `...\ModAI-BR-Companions\whisper\` — ex.
   `ggml-small-q5_1.bin`.
3. (Opcional, para ele **falar**) deixe o modelo do Kokoro em `...\ModAI-BR-Companions\tts\kokoro\`
   (`model.int8.onnx`, `voices.bin`, `tokens.txt`, `espeak-ng-data\`) — já vem no pacote.
4. No LM Studio: carregue um modelo, ligue o servidor local e confira a URL em `[IA] BaseUrl`.
5. Entre no mundo. No chat: `colete 10 madeiras`. Ou segure **`V`** e fale. Ele responde **em voz alta**.

---

## Controles

| Tecla | Ação |
|---|---|
| **`V`** | **Push-to-talk**: segure e fale; solte para ele entender |
| **`Z`** | Tecla de comando: olhe para algo e toque para mandar os companheiros (mover/atacar/abrir porta/sentar/consertar). Segure para chamá-los |
| **`E`** | Segure olhando um companheiro para abrir o **menu radial**; toque para abrir o painel/inventário dele |
| **`LeftAlt` + `Z`** | Editor de zona de fazenda |
| **`I`** | **Painel da IA**: personalidade, voz (com **Ouvir**), volume, velocidade, memória, conversa e a tecla do push-to-talk — tudo salvo na hora |
| **`F8`** | Painel de configurações do mod (todas as opções, em jogo) |

> O painel da IA (tecla **`I`**, mudável em `[IA] PainelTecla`) abre sem precisar de companheiro perto, fecha com
> **Esc** ou no **X**, e cada controle é gravado no `.cfg` no mesmo instante. Enquanto ele está aberto, o seu
> personagem não responde aos controles.

---

## Configuração da IA (`[IA]` no `BepInEx\config\...cfg`)

| Chave | Padrão | O que faz |
|---|---|---|
| `Ligado` | `true` | Liga as ordens por texto |
| `BaseUrl` | `http://localhost:1234/v1` | Endpoint do LLM (LM Studio/Ollama/API) |
| `Modelo` | `google/gemma-4-e4b` | Modelo usado |
| `ApiKey` | *(vazio)* | Só se o servidor pedir (nunca versione uma chave) |
| `Prefixo` | *(vazio)* | Vazio = toda mensagem vai ao LLM. Ex.: `/c` exige o prefixo |
| `Personalidade` / `PresetPersonalidade` | *(vazio)* | Texto livre (ganha) ou preset (`viking`, `mago`, `sarcastico`, `guia`) |
| `Memoria` | `4` | Quantas trocas recentes ele lembra (`0` desliga) |
| `Conversa` | `true` | Responder conversa livre quando não é ordem |
| `RaioDeAlvo` | `40` | Até onde procurar a criatura citada numa ordem de ataque |
| `VozLigada` | `true` | Liga o push-to-talk |
| `VozTecla` | `V` | Tecla do push-to-talk |
| `VozIdioma` | `pt` | Idioma da transcrição |
| `VozDuracaoMaxima` | `10` | Teto da gravação (s) |
| `VozPromptInicial` | *(vocabulário das ordens)* | Enviesa o reconhecimento para as palavras das ordens |
| `VozDispositivo` | `0` | Índice do microfone |
| `SttModelo` | `whisper\ggml-small-q5_1.bin` | Modelo do Whisper |
| `TtsLigado` | `true` | Ele **fala** as respostas (TTS local, Kokoro) |
| `TtsVoz` | `42` | Voz: `42` pf_dora (fem.), `43` pm_alex (masc.), `44` pm_santa (masc.) |
| `TtsVolume` / `TtsVelocidade` | `1` / `1` | Volume e velocidade da fala |
| `TtsThreads` | `4` | Núcleos para a síntese (4 = mais rápido que o tempo real) |
| `TtsFalarRespostas` | `true` | Falar a resposta do modelo |
| `TtsFalarFalasDoMod` | `true` | Falar a linha do mod que aparece no chat (ganha numa ordem) |
| `TtsCache` | `true` | Guardar as falas já sintetizadas (as repetidas saem na hora) |
| `TtsModelo` / `TtsIdioma` | `tts\kokoro\model.int8.onnx` / `pt-br` | Modelo do Kokoro e idioma (nunca vazio) |
| `PainelTecla` | `I` | Tecla que abre o **painel da IA** no jogo (personalidade, voz, push-to-talk) |

---

## Estrutura do repositório

| Caminho | O que é |
|---|---|
| `src/Companions/` | O mod (C#, net472): `Core/`, `Companion/`, `AI/`, `Jobs/`, `UI/`, `Patches/`, `Diagnostics/` |
| `src/Companions/AI/` | **A nossa camada de IA**: `LlmClient`, `LlmIntent`, `CompanionOrders`, `CompanionChatAI`, `WhisperStt`, `VoiceInput`, `CompanionTts`, `CompanionPersona`, `CompanionMemory` |
| `Translations/`, `Speech/` | Traduções PT-BR (interface e falas) — o build copia para o plugin |
| `package/` | Arquivos de pacote (ícone, README/CHANGELOG, guia e configuração) |
| `docs/` | Roadmap, visão, plano de fases, engenharia reversa e notas técnicas |
| `.opencode/` | A **equipe de agentes** usada para desenvolver o projeto |

## Documentação

- [`docs/roadmap.md`](docs/roadmap.md) — fases e status (Trilhas A–D).
- [`docs/vision.md`](docs/vision.md) — o alvo do projeto.
- [`docs/re/`](docs/re/) — engenharia reversa do Valheim 1.0 e notas do STT/voz.
- [`docs/references.md`](docs/references.md) — fontes de terceiros e licenças.
- [`AGENTS.md`](AGENTS.md) — como a equipe de agentes trabalha neste repositório.

## Build

```powershell
# Environment.props (caminhos locais) + build
dotnet build Companions.sln -c Release
# o .dll e as traduções são copiados para o DeployDir (BepInEx\plugins\...)
```

Requisitos: [.NET SDK](https://dotnet.microsoft.com/download) 8+ (ou VS 2022+) e Valheim com BepInEx instalado.
O build usa as DLLs da sua própria instalação do jogo (nenhum arquivo do jogo fica no repositório).

---

## Créditos e licença

- **Upstream:** [Offline Companions Reborn](https://github.com/MaddhattrRocks/Offline-Companions-Reborn) —
  fork do *Offline Companions* do **ProfMags** — **MIT**. O companheiro, os trabalhos, a IA de movimento/combate,
  a UI e a base de tudo isso vêm de lá.
- **Nós:** a tradução PT-BR e a **camada de IA** (LLM/voz), além de correções e ajustes.
- **STT:** [Whisper.net](https://github.com/sandrohanea/whisper.net) + [whisper.cpp](https://github.com/ggml-org/whisper.cpp)
  (MIT) e os modelos ggml do Whisper (MIT).
- Licença do projeto: **MIT** — ver [`LICENSE`](LICENSE) e [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md).
