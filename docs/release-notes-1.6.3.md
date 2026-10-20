# 1.6.3 — reempacotado para gerenciadores de mods

![ModAI-BR-Companions — companheiros NPC com IA para Valheim, em PT-BR](https://raw.githubusercontent.com/julianu37/ModAI-BR-Companions/main/docs/images/banner.png)

> **Nada muda no jogo.** Esta release é a **1.6.2 reempacotada** no padrão que os gerenciadores de mods esperam.

## O que mudou

- A pasta do mod agora vem em **`BepInEx/plugins/ModAI-BR-Companions/`** — o gerenciador (ou a extração na raiz do
  Valheim) instala direto, sem renomear nada.
- `README.md`, `CHANGELOG.md` e `icon.png` ficam na **raiz** do pacote.
- **Dois arquivos para escolher:**

| Arquivo | Tamanho | O que vem |
|---|---|---|
| `ModAI_BR_Companions-1.6.3.zip` | ~368 MB | O mod **e** os modelos de voz (STT + TTS) já prontos — **recomendado** |
| `ModAI_BR_Companions-1.6.3-sem-modelos.zip` | ~28 MB | Só o mod e as traduções — você baixa os modelos por fora |

- O `whisper\jfk.wav` continua **fora** do pacote (saiu na 1.6.1).

## O que já estava na 1.6.2 (e continua igual)

- **Tradução completa para PT-BR** — interface, menus, painéis e as falas do companheiro.
- **Ordens por texto e por voz** — `colete 10 madeiras`, `segue`, `ataca o greydwarf`, `abre o teu inventário`…
  digitadas no chat ou **faladas** (segure **`V`**).
- **Ele fala** — voz sintetizada em PT-BR, saindo da posição dele no mundo (Kokoro-82M, dentro do jogo).
- **Personalidade, memória e conversa livre** — presets em PT-BR ou um texto nas suas palavras.
- **Painel da IA** (tecla **`I`**) — personalidade, voz (com **Ouvir**), volume, velocidade, memória e push-to-talk.
- **Ajustes do projeto** — raio de coleta maior, chatter aleatório desligado e a tecla **`V`** sem disparar o
  auto-pickup do jogo.

## O que você precisa

- **Valheim 1.0.x** com **BepInExPack_Valheim 5.4.2351** (o Jotunn **não** é necessário).
- **LM Studio** (ou qualquer servidor compatível com a API da OpenAI) com um modelo carregado — o recomendado é o
  **`qwen2.5-3b-instruct`** (Q4_K_M).
- **Windows x64** para as duas partes de voz (os binários nativos são `win-x64`).
- **Microfone** — só para as ordens por voz; o chat funciona sozinho.

## Instalação

O passo a passo completo, **com imagens** (incluindo o LM Studio), está na wiki:
**https://github.com/julianu37/ModAI-BR-Companions/wiki/Instalacao**

Resumo: instale o BepInEx → extraia o pacote na raiz do Valheim → carregue o modelo no LM Studio e ligue o
**Local Server** (porta 1234) → no `.cfg` (`[IA]`) use `BaseUrl = http://127.0.0.1:1234/v1` e
`Modelo = qwen2.5-3b-instruct` → entre no mundo e digite `colete 10 madeiras`.

> **Use `127.0.0.1`, não `localhost`:** no Windows o `localhost` resolve primeiro para IPv6 e o servidor do
> LM Studio só escuta IPv4 — cada ordem paga ~2,5 s de espera por causa disso.

## Em ação

| | |
|---|---|
| ![Ordem no chat](https://raw.githubusercontent.com/julianu37/ModAI-BR-Companions/main/docs/wiki/images/1-ordem-no-chat.jpg) | ![Painel da IA](https://raw.githubusercontent.com/julianu37/ModAI-BR-Companions/main/docs/wiki/images/2-painel-da-ia.jpg) |
| **A ordem no chat** — o companheiro entende e responde | **Painel da IA** (tecla `I`) — personalidade, voz e push-to-talk |
| ![Painel F8](https://raw.githubusercontent.com/julianu37/ModAI-BR-Companions/main/docs/wiki/images/3-painel-f8.jpg) | ![Menu radial](https://raw.githubusercontent.com/julianu37/ModAI-BR-Companions/main/docs/wiki/images/4-menu-radial.jpg) |
| **Seção `[IA]` no painel F8** — tudo configurável em jogo | **Menu radial** (segure `E` olhando o companheiro) |

## Créditos e licença

**Projeto independente** baseado no [Offline Companions Reborn](https://github.com/MaddhattrRocks/Offline-Companions-Reborn) (**MIT**),
de **Maddhattr**, que é um fork do *Offline Companions*, de **ProfMags** (**MIT**). O companheiro em si (trabalhos,
comida e stamina, habilidades, casa e descanso, portais e carroça, IA de movimento e combate, menu radial e a UI
base) vem de lá. Nosso projeto acrescenta a **tradução PT-BR** e a **camada de IA** (ordens por texto/voz, STT, TTS,
personalidade e memória).

Voz e reconhecimento usam [Whisper.net](https://github.com/sandrohanea/whisper.net) +
[whisper.cpp](https://github.com/ggml-org/whisper.cpp) (MIT), [sherpa-onnx](https://github.com/k2-fsa/sherpa-onnx)
(Apache-2.0), [Kokoro-82M](https://huggingface.co/hexgrad/Kokoro-82M) (Apache-2.0) e
[onnxruntime](https://github.com/microsoft/onnxruntime) (MIT). Os textos das licenças viajam no pacote, em
`LICENSES\`.

Licença do projeto: **MIT** — ver [`LICENSE`](https://github.com/julianu37/ModAI-BR-Companions/blob/main/LICENSE),
[`NOTICE.md`](https://github.com/julianu37/ModAI-BR-Companions/blob/main/NOTICE.md) e
[`THIRD_PARTY_NOTICES.md`](https://github.com/julianu37/ModAI-BR-Companions/blob/main/THIRD_PARTY_NOTICES.md).
