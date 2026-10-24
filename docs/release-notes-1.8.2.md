# 1.8.2 — escolher o provedor de LLM virou um clique

![AI Companions — companheiros NPC com IA para Valheim, em PT-BR](https://raw.githubusercontent.com/julianu37/AI-Companions/main/docs/images/banner.png)

> **Novo no painel `I`:** um **seletor de provedor** acima da URL (Groq, LM Studio ou OpenRouter) que preenche a
> **URL** e o **modelo** sozinho — e um botão **"Crie sua Chave de API Grátis AQUI"** que abre a página da chave no
> navegador. Nada muda sozinho no seu `.cfg`.

## O que mudou

### Seletor "Selecione seu Provedor LLM"

Na seção *Configurações do LLM* do painel <kbd>I</kbd>, acima do campo **URL do servidor**, agora há um **dropdown**
(o mesmo do "Tipo" do invocador) que **abre já no provedor que o seu `.cfg` usa**:

| Provedor | URL preenchida | Modelo preenchido |
|---|---|---|
| **Groq** (padrão) | `https://api.groq.com/openai/v1` | `openai/gpt-oss-20b` |
| **LM Studio** | `http://127.0.0.1:1234/v1` | `qwen2.5-3b-instruct` |
| **OpenRouter** | `https://openrouter.ai/api/v1` | `openrouter/free` |
| **Personalizado (URL manual)** | — | — |

- Escolher **só preenche os campos**: vale depois do **Aplicar** (como todo campo de texto daquela seção).
- A **chave de API nunca é tocada** — nem preenchida, nem apagada: se você colou a do Groq e trocar para LM Studio,
  ela continua lá.
- **Personalizado** é o que aparece quando a URL do `.cfg` não é de nenhum dos três (Ollama, servidor próprio…):
  nesse caso o seletor **não mexe em nada** — ele nunca mente sobre o que está configurado, porque **deduz** o
  provedor da própria URL em vez de guardar uma escolha paralela.

### Botão "Crie sua Chave de API Grátis AQUI"

Na linha da **Chave de API**, à direita: um botão **dourado com texto branco**, igual aos botões *Ligado*, com
**realce suave** no hover e **sem** virar "Desligado" depois do clique (é uma ação, não um seletor).

- **Groq** → abre `https://console.groq.com/keys`
- **OpenRouter** → abre `https://openrouter.ai/keys`
- **LM Studio** e **Personalizado** → o botão **não aparece** (não há chave para criar)

A URL é **fixa no código** por provedor — nunca é montada com o que o jogador digitou — e a abertura do navegador é
protegida por `try/catch` (se falhar, o log diz para copiar o endereço).

### A "Velocidade da fala" saiu do painel `I`

Ela agora fica **só no painel F8** — a opção `[IA] TtsVelocidade` continua a mesma e o botão **Ouvir** usa o valor de
lá. Com isso o painel ficou **42 px mais baixo** e sem espaço vazio no meio.

## Para quem já tem o mod

- O **`.cfg` não é reescrito**: o seletor apenas **mostra** qual provedor está em uso, e a velocidade da fala continua
  no valor que você deixou.
- Se você usa **Ollama** ou outro servidor na `BaseUrl`, o seletor aparece como **"Personalizado (URL manual)"** e
  tudo continua exatamente como antes.

## Arquivos

| Arquivo | Tamanho | O que vem |
|---|---|---|
| `AI_Companions-1.8.2.zip` | ~368 MB | O mod **e** os modelos de voz (STT + TTS) — **recomendado** |
| `AI_Companions-1.8.2-sem-modelos.zip` | ~28,5 MB | Só o mod e as traduções (vem `tokens.txt` e `espeak-ng-data\`) |

> **Ao atualizar de uma versão anterior, apague a pasta antiga** `BepInEx\plugins\ModAI-BR-Companions\` antes de
> extrair: duas cópias do mesmo plugin fazem o BepInEx carregar o mesmo mod duas vezes. O `.cfg`
> (`BepInEx\config\Maddhattr.OfflineCompanionsReborn.cfg`) e o save **não** são afetados.

## Requisitos e instalação

- **Valheim 1.0.x** + **BepInExPack_Valheim 5.4.2351** (BepInEx 5).
- **Um LLM**: **Groq** (padrão — só a chave gratuita e internet) **ou** **LM Studio/Ollama** local (opcional) **ou**
  **OpenRouter** (funciona, mas o tier gratuito é mais lento).
- **Windows x64** para as duas partes de voz; **microfone** só para as ordens por voz.

Passo a passo completo, **com imagens**: https://github.com/julianu37/AI-Companions/wiki/Instalacao

## Créditos e licença

**Projeto independente** baseado no [Offline Companions Reborn](https://github.com/MaddhattrRocks/Offline-Companions-Reborn)
(**MIT**), de **Maddhattr**, que por sua vez é um fork do *Offline Companions*, de **ProfMags** (**MIT**). A atribuição
completa está em `LICENSE`, `NOTICE.md` e `THIRD_PARTY_NOTICES.md`.

Voz: [Whisper.net](https://github.com/sandrohanea/whisper.net) + modelos do **openai/whisper** (MIT) e
[Kokoro-82M](https://huggingface.co/hexgrad/Kokoro-82M) via [sherpa-onnx](https://github.com/k2-fsa/sherpa-onnx)
(Apache-2.0).
