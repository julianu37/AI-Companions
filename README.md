# ModAI-BR-Companions

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
                                                                                          └─► LLM local (LM Studio, API OpenAI-compatible) ─► ação + resposta
```

- **LLM local**: LM Studio (ou qualquer endpoint compatível com OpenAI) — usado para **entender** a ordem e
  **conversar**.
- **STT local**: `Whisper.net` (whisper.cpp) rodando **dentro do jogo**, com o modelo carregado uma vez.
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
3. No LM Studio: carregue um modelo, ligue o servidor local e confira a URL em `[IA] BaseUrl`.
4. Entre no mundo. No chat: `colete 10 madeiras`. Ou segure **`V`** e fale.

---

## Controles

| Tecla | Ação |
|---|---|
| **`V`** | **Push-to-talk**: segure e fale; solte para ele entender |
| **`Z`** | Tecla de comando: olhe para algo e toque para mandar os companheiros (mover/atacar/abrir porta/sentar/consertar). Segure para chamá-los |
| **`E`** | Segure olhando um companheiro para abrir o **menu radial**; toque para abrir o painel/inventário dele |
| **`LeftAlt` + `Z`** | Editor de zona de fazenda |
| **`F8`** | Painel de configurações do mod (todas as opções, em jogo) |

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

---

## Estrutura do repositório

| Caminho | O que é |
|---|---|
| `src/Companions/` | O mod (C#, net472): `Core/`, `Companion/`, `AI/`, `Jobs/`, `UI/`, `Patches/`, `Diagnostics/` |
| `src/Companions/AI/` | **A nossa camada de IA**: `LlmClient`, `LlmIntent`, `CompanionOrders`, `CompanionChatAI`, `WhisperStt`, `VoiceInput`, `CompanionPersona`, `CompanionMemory` |
| `Translations/`, `Speech/` | Traduções PT-BR (interface e falas) — o build copia para o plugin |
| `package/` | Arquivos de pacote (manifest, ícone, README/CHANGELOG do Thunderstore) |
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
