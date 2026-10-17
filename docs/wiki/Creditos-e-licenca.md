# Créditos e licença

O **ModAI-BR-Companions** é um **fork** e existe em cima do trabalho de outras pessoas. Esta página diz quem
fez o quê.

---

## Upstream (a base do mod)

| Projeto | Autor | Licença |
|---|---|---|
| **Offline Companions Reborn** | **MaddhattrRocks** | **MIT** |
| *Offline Companions* (o original) | **ProfMags** | MIT |

https://github.com/MaddhattrRocks/Offline-Companions-Reborn

**Vem de lá:** o companheiro em si — os trabalhos (coleta, fazenda, pesca, caça, cozinha, fundição, reparo,
loot), a comida e a stamina, as habilidades, a casa e o descanso, os portais e a carroça, a IA de movimento e
combate, o menu radial e toda a UI base. A camada de IA do nosso fork é só uma **nova entrada** para essas
mesmas ações.

> O Offline Companions Reborn, por sua vez, é um fork do *Offline Companions*, do **ProfMags**.

---

## O que é nosso

O que o fork **ModAI-BR-Companions** acrescenta:

1. **Tradução completa para PT-BR** — 386 chaves de interface e 186 falas do companheiro, auditadas (sem
   texto em inglês visível ao jogador).
2. **A camada de IA:**
   - Ordens em **linguagem natural** por **texto no chat** e por **voz** (push-to-talk + STT local).
   - **Personalidade** (presets ou texto livre), **memória** por companheiro e **conversa livre**.
   - **TTS local** (Kokoro-82M via sherpa-onnx): o companheiro **fala** na posição dele no mundo.
   - O **painel da IA** (tecla `I`).
3. **Ajustes e correções:**
   - Raio de coleta maior e chatter aleatório desligado por padrão.
   - A tecla `V` não dispara mais o *Toggle Auto-Pickup* do jogo (patch condicional).
   - O eco da voz (era a zona de reverb global do jogo).
   - A ordem de pedra que não parava no alvo.
   - Os painéis do mod acima do HUD do jogo (o clique engolido no `F8`).

---

## Terceiros

| Componente | O que é | Licença / link |
|---|---|---|
| **Whisper.net** | Binding .NET para o whisper.cpp, usado no STT | MIT — https://github.com/sandrohanea/whisper.net |
| **whisper.cpp** | Implementação do Whisper em C/C++ | MIT — https://github.com/ggml-org/whisper.cpp |
| **Modelos ggml do Whisper** | Os arquivos `ggml-*.bin` | MIT — https://huggingface.co/ggerganov/whisper.cpp |
| **sherpa-onnx** | Runtime de TTS/STT (ONNX) usado no TTS | Apache-2.0 — https://github.com/k2-fsa/sherpa-onnx |
| **Kokoro-82M** | O modelo de voz que o companheiro usa | Apache-2.0 — https://huggingface.co/hexgrad/Kokoro-82M |
| **onnxruntime** | O runtime que executa os modelos ONNX (vem junto do sherpa-onnx) | MIT — https://github.com/microsoft/onnxruntime |
| **espeak-ng** | Fonemização usada pelo Kokoro (vai como **dados**, em `espeak-ng-data\`) | GPL-3.0, só os dados — https://github.com/espeak-ng/espeak-ng |
| **LM Studio** | O aplicativo que serve o LLM local | https://lmstudio.ai |
| **Qwen2.5** | A família do modelo recomendado (`qwen2.5-3b-instruct`) | Apache-2.0 — https://huggingface.co/Qwen |

> Os textos completos das licenças de terceiros viajam **dentro do pacote**, na pasta `LICENSES\`.
>
> **Referência conceitual:** o **Thrall** (ego.ai) foi estudado como referência de ideia (*all rights reserved*).
> **Nenhum** código ou binário dele é redistribuído aqui.

---

## Licença do projeto

**MIT.** Você pode usar, modificar e redistribuir, mantendo os avisos de copyright.

Os arquivos completos estão no repositório:

- [`LICENSE`](https://github.com/julianu37/ModAI-BR-Companions/blob/main/LICENSE)
- [`THIRD_PARTY_NOTICES.md`](https://github.com/julianu37/ModAI-BR-Companions/blob/main/THIRD_PARTY_NOTICES.md)
- [`NOTICE.md`](https://github.com/julianu37/ModAI-BR-Companions/blob/main/NOTICE.md)

> A pasta `LICENSES\` que vem junto do mod instalado traz os textos das licenças dos componentes de terceiros
> distribuídos com ele.
