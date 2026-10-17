# ModAI-BR-Companions

Um **companheiro NPC com IA** para **Valheim 1.0**, em **português do Brasil** — você **fala ou digita** e ele
entende e executa.

[![Assista: "Esse mod muda TUDO no Valheim! ModAI-BR-Companions"](images/video-thumb.jpg)](https://www.youtube.com/watch?v=40FnVEIbXyo)

> Vídeo do canal **SuaTV**: **"Esse mod muda TUDO no Valheim! ModAI-BR-Companions"**.
> **[Assistir no YouTube](https://www.youtube.com/watch?v=40FnVEIbXyo)** — clicar na imagem também abre.

## O que é

Este projeto é um **fork** de [Offline Companions Reborn](https://github.com/MaddhattrRocks/Offline-Companions-Reborn)
(**MIT**) — que por sua vez é um fork do *Offline Companions*, do **ProfMags** — com duas coisas nossas por cima:

1. **Tradução completa para PT-BR** (interface, menus, painéis e as falas do companheiro).
2. **Uma camada de IA**: ordens em **linguagem natural** (texto no chat ou **voz** por push-to-talk), com
   **personalidade**, **memória** e **conversa livre**.

O companheiro em si (trabalhos, comida, stamina, habilidades, casa, descanso, portais, carroça, menu radial)
vem do upstream e continua funcionando igual — a IA é só uma **nova entrada** para as mesmas ações.

> **Tudo roda na sua máquina.** Por padrão, o entendimento das ordens (LLM), o reconhecimento da fala (STT) e a
> voz sintetizada (TTS) são **100% locais**. Nada é enviado para a internet e nenhum serviço externo é usado.
> Um provedor remoto pode ser configurado, se você quiser.

## O que ele faz

| Recurso | Como |
|---|---|
| **Ordens por chat** | `colete 10 madeiras` · `segue` · `para` · `ataca o greydwarf` · `equipa o machado de bronze` |
| **Ordens por voz** | Segure **`V`**, fale e solte — a fala é transcrita dentro do jogo |
| **Ele fala** | Voz sintetizada em PT-BR, saindo **da posição dele** no mundo |
| **Personalidade** | Presets (`viking`, `mago`, `sarcastico`, `guia`) ou as suas palavras |
| **Memória e conversa** | `bom dia`, `como você está?` — ele responde como personagem e lembra da conversa |
| **Painel da IA** | Tecla **`I`**: personalidade, voz, volume, velocidade e push-to-talk, tudo salvo na hora |

## Começando

1. Veja o que você precisa em **[Requisitos](Requisitos)**.
2. Siga o passo a passo de **[Instalação](Instalacao)**.
3. Configure o LLM em **[LLM no LM Studio](LLM-no-LM-Studio)**.
4. Veja as teclas e as ordens em **[Comandos](Comandos)**.
5. Se algo não funcionar, vá em **[Problemas comuns](Problemas-comuns)**.

## Índice da wiki

| Página | O que tem |
|---|---|
| **[Requisitos](Requisitos)** | O que é obrigatório e o que é opcional |
| **[Instalação](Instalacao)** | Passo a passo com imagens |
| **[LLM no LM Studio](LLM-no-LM-Studio)** | Instalar o LM Studio, baixar o modelo e ligar o servidor |
| **[Voz](Voz)** | STT (Whisper) e TTS (Kokoro): como funcionam e como ajustar |
| **[Comandos](Comandos)** | Teclas, ordens de chat/voz e comandos de console |
| **[Configuração](Configuracao)** | O `.cfg`, a seção `[IA]` e os painéis `F8` e `I` |
| **[Problemas comuns](Problemas-comuns)** | Sintoma → causa → solução |
| **[Como funciona](Como-funciona)** | Arquitetura e o caminho de uma ordem |
| **[Créditos e licença](Creditos-e-licenca)** | Upstream, terceiros e licenças |

## Links rápidos

| O quê | Onde |
|---|---|
| **Releases (download)** | https://github.com/julianu37/ModAI-BR-Companions/releases |
| **Repositório** | https://github.com/julianu37/ModAI-BR-Companions |
| **Changelog** | https://github.com/julianu37/ModAI-BR-Companions/blob/main/package/CHANGELOG.md |
| **Upstream (MIT)** | https://github.com/MaddhattrRocks/Offline-Companions-Reborn |
| **Vídeo** | https://www.youtube.com/watch?v=40FnVEIbXyo |

A release publicada mais recente é a **1.6.2**, com **dois** arquivos para escolher:

| Arquivo | Tamanho | O que vem |
|---|---|---|
| `ModAI-BR-Companions-1.6.2-completo.zip` | ~370,5 MB | O mod **e** os modelos de voz (STT + TTS) já prontos |
| `ModAI-BR-Companions-1.6.2-sem-modelos.zip` | ~22,1 MB | Só o mod e as traduções — você baixa os modelos por fora |

> **Qual escolher?** Se você quer só instalar e usar, pegue o **completo**. Se a sua internet é lenta (ou você já
> tem os modelos), pegue o **sem-modelos** e siga a seção de modelos em **[Instalação](Instalacao)**.
