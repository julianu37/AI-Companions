# Requisitos

O que você precisa ter antes de instalar o **AI Companions**.

O mod funciona em **duas camadas independentes**: as **ordens por texto no chat** (precisam de um **LLM**) e
as **ordens por voz** (precisam de microfone). Você pode usar **só o chat**, digitando — a voz é opcional.

## Obrigatório

| Item | Versão / detalhe |
|---|---|
| **Valheim** | **1.0.x** (engine Unity 6 — testado na **1.0.20**) |
| **BepInEx** | **BepInExPack_Valheim 5.4.2351** (BepInEx 5.4.23.5) — o mod é um plugin BepInEx 5 |
| **Windows x64** | **Obrigatório** para a voz: os binários nativos do **STT** (whisper.cpp) **e do TTS** (sherpa-onnx + onnxruntime) são `win-x64`. Sem eles, nem as ordens por voz nem a fala do companheiro funcionam |
| **Um LLM** | Para as ordens por texto/voz serem entendidas. O padrão é o **Groq**: só precisa de **internet** e de uma **chave gratuita** (veja abaixo). A alternativa **local** (**LM Studio** ou **Ollama**) é **opcional** e não manda nada para fora; o **OpenRouter** continua funcionando, mas o tier gratuito é mais lento. Veja **[LLM (Groq, LM Studio ou OpenRouter)](LLM-no-LM-Studio)** |
| **Espaço em disco** | Veja a tabela abaixo |

> **O LLM é obrigatório; o caminho não.** O **Groq** é o padrão: basta a **chave gratuita**
> (https://console.groq.com/keys) e internet — e o texto das ordens **sai da sua máquina**. O **LM Studio/Ollama** é
> a **alternativa local, opcional**: nada sai, mas você baixa um modelo (~1,9 GB) e deixa o servidor ligado
> enquanto joga. O **OpenRouter** também funciona (chave gratuita em https://openrouter.ai/keys) e fica como
> **alternativa**, porque o tier gratuito dele é mais lento. Os três caminhos — nessa ordem de preferência — estão
> em **[LLM (Groq, LM Studio ou OpenRouter)](LLM-no-LM-Studio)**.

## Opcional

| Item | Para quê |
|---|---|
| **Microfone** | Só para as **ordens por voz** (push-to-talk). Sem ele, use o chat |
| **GPU** | Só faz diferença na opção **local**: o LLM no LM Studio roda **muito** mais rápido com uma GPU. No Groq (e no OpenRouter), o modelo roda no servidor deles |
| **Modelos de voz** | Já vêm no pacote **completo**. No pacote **sem-modelos** você baixa por fora |

## Espaço em disco

| O quê | Tamanho |
|---|---|
| Pacote **completo** (mod + modelos) | ~368 MB (zip) |
| Pacote **sem-modelos** (mod + traduções) | ~28,4 MB (zip) |
| Mod instalado (pasta do plugin) | ~428 MB |
| Modelo do Whisper `ggml-small-q5_1.bin` (padrão do STT) | 181 MB |
| Modelo do Whisper `ggml-base-q5_1.bin` (mais rápido, menos preciso) | 57 MB |
| Modelo do Kokoro (TTS) `model.int8.onnx` | 109 MB |
| Vozes do Kokoro `voices.bin` | 27 MB |
| Modelo do LLM **local** (`qwen2.5-3b-instruct` Q4_K_M, LM Studio) — só na opção local | ~1,93 GB |

> Reserve algo em torno de **1 GB** para o mod e os modelos de voz, mais o espaço do modelo do LLM.

## Hardware

- **CPU:** o TTS (Kokoro-82M) sintetiza na **CPU**. Com **4 núcleos** ele já gera mais rápido do que o tempo real
  (a fala sai na frente do companheiro). O STT (Whisper) também roda na CPU.
- **GPU:** só importa na opção **local** — no LM Studio uma GPU faz uma diferença enorme. Um modelo 3B roda bem em
  CPU, mas fica bem mais rápido com GPU. No Groq (e no OpenRouter), quem roda o modelo é o servidor deles.
- **RAM:** na opção local, um modelo 3B quantizado (Q4) ocupa cerca de **2 GB** de memória, além do jogo. Com o
  Groq (ou o OpenRouter) não há esse consumo.

## Como instalar o BepInEx

O BepInEx é o carregador de plugins do Valheim. Instale-o **antes** do mod:

1. Baixe o **BepInExPack_Valheim 5.4.2351** na página de releases do BepInEx:
   https://github.com/BepInEx/BepInEx/releases
2. Extraia o conteúdo na pasta do jogo, de forma que o `winhttp.dll` e a pasta `BepInEx\` fiquem **na raiz** do
   Valheim (`C:\Program Files (x86)\Steam\steamapps\common\Valheim` por padrão).
3. Rode o Valheim **uma vez** e feche — o BepInEx cria a estrutura de pastas
   (`BepInEx\plugins\`, `BepInEx\config\`, `BepInEx\LogOutput.log`).

> Se o BepInEx não carregar, confira a documentação oficial: https://docs.bepinex.dev/

Depois disso, siga a **[Instalação](Instalacao)** do mod.

## Próximo passo

➡️ **[Instalação](Instalacao)**
