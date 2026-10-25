# 1.8.3 — multilíngue: o mod fala o idioma do jogo

![AI Companions — companheiros NPC com IA para Valheim, em PT-BR](https://raw.githubusercontent.com/julianu37/AI-Companions/main/docs/images/banner.png)

> **⚠️ Esta versão zera as configurações.** O arquivo passa a ser **`BepInEx\config\AiCompanions.cfg`** e o antigo
> (`Maddhattr.OfflineCompanionsReborn.cfg`) **deixa de ser lido** — ele fica no disco, pode ser apagado, e o mod volta
> aos **padrões** (inclusive a `ApiKey`, que precisa ser colada de novo).
>
> **Nada do seu jogo se perde:** mundo, save, companheiros, casas, inventário e baús continuam exatamente iguais — o
> GUID do plugin e as chaves `HC_*` foram preservados.

## O que mudou

### Inglês como idioma nativo, português como tradução

O mod nasceu com as configurações traduzidas **dentro do código** (`Ligado`, `Modelo`, `TtsVoz`…), e o painel F8
desenhava esses nomes crus — resultado: com o jogo em inglês o painel `I` ficava em inglês e o F8 em português. Agora:

- **as chaves e as descrições do `.cfg` são em inglês** (o idioma do upstream e a base para qualquer tradução);
- a **seção `[IA]` virou `[AI]`** (`Enabled`, `BaseUrl`, `Model`, `ApiKey`, `Temperature`, `TimeoutSeconds`, `Prefix`,
  `ShowReplyInChat`, `CooldownSeconds`, `TargetRange`, `Personality`, `Memory`, `Conversation`, `StructuredOutput`,
  `VoiceEnabled`, `VoiceKey`, `TtsEnabled`, `TtsVoice`, `TtsSpeed`, `TtsSpeakReplies`…);
- o **F8 é bilíngue**: os nomes das opções e das abas seguem o **idioma do jogo** — em português você vê `Ligado`,
  `Modelo`, `VozTecla`; em inglês, `Enabled`, `Model`, `VoiceKey`. O painel `I` já funcionava assim.

### Tipos dverger são de combate

O **Mago**, **Mago de Fogo**, **Mago de Gelo** e **Suporte** (os dverger) lutam normalmente, e a **roda** deles mostra só
as ações de combate/suporte — igual ao upstream. Agora o **chat**, a **voz** e a **tecla de comando** também **não
conseguem** colocá-los para trabalhar: a resposta é sempre *"Sou de combate, não faço esse trabalho."*, sem mexer no
modo nem no alvo da ordem. Trocar o **tipo** de um companheiro que estava trabalhando por um desses faz ele **voltar a
seguir**. Para trabalhos, use o tipo **Companheiro**.

### Espera para renascer

O padrão de **`[Death] RespawnDelay`** (F8 → *Morte* → *Espera p/ renascer*) passou de **300 s** para **30 s** — o
companheiro morto volta sozinho em meio minuto, como um jogador.

### Para quem for traduzir

Um idioma novo passou a ser **um arquivo**: `Translations/<Idioma>.json` (interface, nomes de config e abas) +
`Speech/<Idioma>.json` (falas). O **inglês não precisa de arquivo** — as chaves **são** o texto.

### O que veio nas versões anteriores (nesta mesma linha)

- **1.8.2** — **seletor de provedor** no painel `I` (Groq / LM Studio / OpenRouter, preenchendo URL e modelo), botão
  **"Crie sua Chave de API Grátis AQUI"** e a velocidade da fala só no **F8**;
- **1.8.1** — **Groq** como LLM padrão (grátis e rápido), com o OpenRouter ainda disponível mas rebaixado;
- **1.8.0** — **peso com bônus de equipamento**: o **Megingjord** (+150) conta no teto do companheiro, com o cinto em
  qualquer lugar da mochila e sem empilhar;
- **1.7.0** — **até 10 companheiros**, cada um com aba, nome, tipo, aparência, voz e personalidade próprios; ordem pelo
  nome; editor de aparência com preview 3D.

## Como atualizar (importante)

1. Feche o jogo.
2. **Apague** a pasta antiga `BepInEx\plugins\ModAI-BR-Companions\` (se existir) e extraia este pacote — a pasta é
   `BepInEx\plugins\AI-Companions\`. Sempre **uma** cópia do mod.
3. O `BepInEx\config\AiCompanions.cfg` é criado no primeiro boot. Se você tinha o antigo, pode **apagá-lo**: ele não é
   mais lido. As configurações voltam aos padrões.
4. Abra o painel <kbd>I</kbd> → **Configurações do LLM** → escolha o provedor no seletor, cole a **sua chave** e
   **Aplicar** → **Testar conexão**.

## Arquivos

| Arquivo | Tamanho | O que vem |
|---|---|---|
| `AI_Companions-1.8.3.zip` | ~368 MB | O mod **e** os modelos de voz (STT + TTS) — **recomendado** |
| `AI_Companions-1.8.3-sem-modelos.zip` | ~28,5 MB | Só o mod e as traduções (vem `tokens.txt` e `espeak-ng-data\`) |

## Requisitos e instalação

- **Valheim 1.0.x** + **BepInExPack_Valheim 5.4.2351** (BepInEx 5).
- **Um LLM**: **Groq** (padrão — só a chave gratuita e internet) **ou** **LM Studio/Ollama** local (opcional) **ou**
  **OpenRouter** (funciona, mas o tier gratuito é mais lento).
- **Windows x64** para as duas partes de voz; **microfone** só para as ordens por voz.

Passo a passo completo, **com imagens**: https://github.com/julianu37/AI-Companions/wiki/Instalacao

## Em ação

| | | |
|---|---|---|
| ![Abas por companheiro](https://raw.githubusercontent.com/julianu37/AI-Companions/main/docs/wiki/images/1-abas-do-companheiro.jpg) | ![Editor de aparência](https://raw.githubusercontent.com/julianu37/AI-Companions/main/docs/wiki/images/2-editor-de-aparencia.jpg) | ![Painel da IA](https://raw.githubusercontent.com/julianu37/AI-Companions/main/docs/wiki/images/6-painel-ia.jpg) |
| **Uma aba por companheiro** (tecla `I`) | **Editor de aparência** com preview 3D | **Provedor de LLM** e **botão da chave** |

## Créditos e licença

**Projeto independente** baseado no [Offline Companions Reborn](https://github.com/MaddhattrRocks/Offline-Companions-Reborn)
(**MIT**), de **Maddhattr**, que por sua vez é um fork do *Offline Companions*, de **ProfMags** (**MIT**). A atribuição
completa está em `LICENSE`, `NOTICE.md` e `THIRD_PARTY_NOTICES.md`.

Voz: [Whisper.net](https://github.com/sandrohanea/whisper.net) + modelos do **openai/whisper** (MIT) e
[Kokoro-82M](https://huggingface.co/hexgrad/Kokoro-82M) via [sherpa-onnx](https://github.com/k2-fsa/sherpa-onnx)
(Apache-2.0).
