# 1.6.4 — padrões do `[IA]` corretos e projeto independente

![ModAI-BR-Companions — companheiros NPC com IA para Valheim, em PT-BR](https://raw.githubusercontent.com/julianu37/ModAI-BR-Companions/main/docs/images/banner.png)

> **Quem já tem o mod configurado não precisa fazer nada:** esta versão só muda o que uma instalação **nova** recebe.

## O que mudou

- **`[IA] BaseUrl` agora nasce `http://127.0.0.1:1234/v1`** (era `http://localhost:1234/v1`).
  No Windows, o `localhost` resolve primeiro para IPv6 e o servidor do LM Studio só escuta IPv4 — cada ordem pagava
  cerca de **2,5 s** de espera por causa disso. Agora o padrão já é o caminho rápido.
- **`[IA] Modelo` agora nasce `qwen2.5-3b-instruct`** (era `google/gemma-4-e4b`) — o modelo medido em ~0,35–0,4 s
  por ordem com a saída estruturada, e o que a wiki recomenda desde o começo.
- O texto de ajuda do `BaseUrl` passou a **explicar** o `127.0.0.1` vs `localhost` (em vez de ensinar o `localhost`).
- O projeto deixa de se apresentar como "fork" e passa a **projeto independente baseado no Offline Companions
  Reborn** — a atribuição MIT continua obrigatória e mantida em `LICENSE`, `NOTICE.md` e `THIRD_PARTY_NOTICES.md`.

## Arquivos

| Arquivo | Tamanho | O que vem |
|---|---|---|
| `ModAI_BR_Companions-1.6.4.zip` | ~368 MB | O mod **e** os modelos de voz (STT + TTS) — **recomendado** |
| `ModAI_BR_Companions-1.6.4-sem-modelos.zip` | ~28 MB | Só o mod e as traduções (o pacote traz `tokens.txt` e `espeak-ng-data\`) |

## Requisitos e instalação

- **Valheim 1.0.x** + **BepInExPack_Valheim 5.4.2351** (BepInEx 5).
- **LM Studio** com um modelo local (`qwen2.5-3b-instruct`) — ou qualquer servidor compatível com a API da OpenAI.
- **Windows x64** para as duas partes de voz; **microfone** só para as ordens por voz.

Passo a passo completo, **com imagens**: https://github.com/julianu37/ModAI-BR-Companions/wiki/Instalacao

## Em ação

| | |
|---|---|
| ![Ordem no chat](https://raw.githubusercontent.com/julianu37/ModAI-BR-Companions/main/docs/wiki/images/1-ordem-no-chat.jpg) | ![Painel da IA](https://raw.githubusercontent.com/julianu37/ModAI-BR-Companions/main/docs/wiki/images/2-painel-da-ia.jpg) |
| **A ordem no chat** — ele entende e responde | **Painel da IA** (tecla `I`) |
| ![Painel F8](https://raw.githubusercontent.com/julianu37/ModAI-BR-Companions/main/docs/wiki/images/3-painel-f8.jpg) | ![Menu radial](https://raw.githubusercontent.com/julianu37/ModAI-BR-Companions/main/docs/wiki/images/4-menu-radial.jpg) |
| **Seção `[IA]` no painel F8** | **Menu radial** (segure `E`) |

## Créditos e licença

**Projeto independente** baseado no [Offline Companions Reborn](https://github.com/MaddhattrRocks/Offline-Companions-Reborn)
(**MIT**), de **Maddhattr**, que por sua vez é um fork do *Offline Companions*, de **ProfMags** (**MIT**). O
companheiro em si (trabalhos, comida e stamina, habilidades, casa e descanso, portais e carroça, IA de movimento e
combate, menu radial e a UI base) vem de lá; o nosso projeto acrescenta a **tradução PT-BR** e a **camada de IA**.

Voz e reconhecimento usam [Whisper.net](https://github.com/sandrohanea/whisper.net) +
[whisper.cpp](https://github.com/ggml-org/whisper.cpp) (MIT), [sherpa-onnx](https://github.com/k2-fsa/sherpa-onnx)
(Apache-2.0), [Kokoro-82M](https://huggingface.co/hexgrad/Kokoro-82M) (Apache-2.0) e
[onnxruntime](https://github.com/microsoft/onnxruntime) (MIT). Os textos das licenças viajam no pacote, em `LICENSES\`.

Licença: **MIT** — ver [`LICENSE`](https://github.com/julianu37/ModAI-BR-Companions/blob/main/LICENSE),
[`NOTICE.md`](https://github.com/julianu37/ModAI-BR-Companions/blob/main/NOTICE.md) e
[`THIRD_PARTY_NOTICES.md`](https://github.com/julianu37/ModAI-BR-Companions/blob/main/THIRD_PARTY_NOTICES.md).
