> 🌍 [English](Home) · **Português** · [Español](Home-es) · [Français](Home-fr) · [Italiano](Home-it)

# AI Companions

Um **companheiro NPC com IA** para **Valheim 1.0**, em **português do Brasil** — você **fala ou digita** e ele
entende e executa.

> **Tipos de companheiro: os dverger são de combate**
>
> **Mago**, **Mago de Fogo**, **Mago de Gelo** e **Suporte** são **de combate**: lutam normalmente e a roda mostra só
> as ações de combate/suporte — ordens de **trabalho** por chat, voz ou tecla de comando são **recusadas** com
> *"Sou de combate, não faço esse trabalho"*. Para trabalhos, use o **Companheiro** (detalhes em
> **[Como funciona](How-it-works-pt#tipos-de-companheiro)**).

## Em ação

**1. Uma ordem no chat** — `colete 10 madeiras`: a resposta dele e o balão de fala no mundo.

![O chat com a ordem "colete 10 madeiras", a resposta do companheiro e o balão de fala](images/1-ordem-no-chat.jpg)

**2. Uma aba por companheiro (tecla `I`)** — nome, tipo, Chamar, Aparência e a voz/personalidade **daquele**
companheiro.

![O painel da IA com uma aba por companheiro (Bruxinho, Companheira, Companheiro, Xuxa, Zé): nome, tipo, Trocar tipo, Chamar, Aparência, personalidade e voz com o botão Ouvir](images/1-abas-do-companheiro.jpg)

**3. Editor de aparência** — gênero, corte de cabelo, barba, pele, tom e sombra do cabelo, com o boneco mudando
**ao vivo** enquanto você escolhe.

![O editor de aparência: gênero masculino/feminino, corte de cabelo, estilo de barba, tom de pele, tom e sombra do cabelo, com o preview 3D do companheiro](images/2-editor-de-aparencia.jpg)

**4. Painel `F8`, seção `[AI]`** — as opções da camada de IA, com os mesmos nomes de chave do `.cfg`.

![O painel F8 aberto na seção [AI], com as opções da camada de IA](images/3-painel-f8.jpg)

**5. Menu radial (segure `E` olhando o companheiro)** — as ações dele à mão.

![O menu radial aberto, com as ações em volta do centro](images/4-menu-radial.jpg)

## O que é

Este projeto é um **projeto independente** baseado em [Offline Companions Reborn](https://github.com/MaddhattrRocks/Offline-Companions-Reborn)
(**MIT**) — que por sua vez é um fork do *Offline Companions*, do **ProfMags** — com duas coisas nossas por cima:

1. **Cinco idiomas** — **português do Brasil, inglês, espanhol, francês e italiano**: a interface (914 chaves), as
   falas do companheiro (221 linhas), os presets de personalidade e o contrato do LLM seguem o **idioma do jogo**,
   com o **inglês como idioma nativo**, e um idioma novo é **um arquivo**.
2. **Uma camada de IA**: ordens em **linguagem natural** (texto no chat ou **voz** por push-to-talk), com
   **personalidade**, **memória** e **conversa livre** — e a voz e o reconhecimento da fala **escolhem o idioma e a
   voz sozinhos**.

O companheiro em si (trabalhos, comida, stamina, habilidades, casa, descanso, portais, carroça, menu radial)
vem do upstream e continua funcionando igual — a IA é só uma **nova entrada** para as mesmas ações.

> **A voz roda toda na sua máquina.** O reconhecimento da fala (STT) e a voz sintetizada (TTS) são **100%
> locais**: nada é enviado. O entendimento das ordens usa um **LLM** — o caminho recomendado é o **LM Studio**
> (ou Ollama) local, onde **nada** sai; o `.cfg` vem apontado para o **Groq**, um **serviço remoto** (o texto
> das ordens e da conversa **sai da sua máquina**). Veja
> **[LLM (LM Studio, Groq ou OpenRouter)](LLM-pt)**.

## O que ele faz

| Recurso | Como |
|---|---|
| **Ordens por chat** | `colete 10 madeiras` · `segue` · `para` · `ataca o greydwarf` · `equipa o machado de bronze` |
| **Ordens por voz** | Segure **`V`**, fale e solte — a fala é transcrita dentro do jogo |
| **Ele fala** | Voz sintetizada **no idioma do jogo**, saindo **da posição dele** no mundo |
| **Personalidade** | Presets (**Viking leal**, **Mago sarcástico**, **Sarcástico**, **Guia paciente** — os nomes seguem o idioma do jogo) ou as suas palavras |
| **Memória e conversa** | `bom dia`, `como você está?` — ele responde como personagem e lembra da conversa |
| **Vários companheiros** | Até **10**, cada um com a sua aba no painel `I`; o invocador cria um novo **de graça** (sem o Trader) |
| **Ordem pelo nome** | `Gruncha, colete 10 pedras` vale **só** para ele; sem o nome, a ordem vale para todos |
| **Aparência** | Editor com **preview 3D**: gênero, cabelo, barba, pele e tom do cabelo, sem respawn |
| **Painel da IA** | Tecla **`I`**: uma aba por companheiro (nome, tipo, Chamar, Aparência, voz e personalidade), o invocador, volume, push-to-talk e as **configurações do LLM** (com o **seletor de provedor**) — tudo salvo na hora |

## Requisitos em resumo

| Item | Detalhe |
|---|---|
| **Valheim 1.0.x** + **BepInEx 5.4.2351** | A base de tudo — passo a passo em **[Instalação](Installation-pt)** |
| **Um LLM** | **LM Studio** (recomendado): o modelo roda na sua máquina e nada sai — você baixa uma vez (~1,9 GB) e deixa o servidor ligado. **Groq** (o que vem no `.cfg`): só a **chave gratuita** (https://console.groq.com/keys) e internet — o texto das ordens **sai da sua máquina**. O **OpenRouter** continua funcionando como alternativa |
| **Microfone** | Só para as ordens por voz |

## Começando

1. Veja o que você precisa em **[Requisitos](Requirements-pt)**.
2. Siga o passo a passo de **[Instalação](Installation-pt)**.
3. Configure o LLM em **[LLM (LM Studio, Groq ou OpenRouter)](LLM-pt)** — o caminho recomendado é o **LM Studio**
   local (sem chave, nada sai); o **Groq** é o que vem no `.cfg` (só a chave gratuita) e o **OpenRouter** é a
   alternativa.
4. Veja as teclas e as ordens em **[Comandos](Commands-pt)**.
5. Se algo não funcionar, vá em **[Problemas comuns](Troubleshooting-pt)**.

## Índice da wiki

| Página | O que tem |
|---|---|
| **[Requisitos](Requirements-pt)** | O que é obrigatório e o que é opcional |
| **[Instalação](Installation-pt)** | Passo a passo com imagens |
| **[LLM (LM Studio, Groq ou OpenRouter)](LLM-pt)** | O LM Studio como caminho recomendado, o Groq como a opção de nuvem que vem no `.cfg` (chave gratuita) e o OpenRouter como alternativa |
| **[Voz](Voice-pt)** | STT (Whisper) e TTS (Kokoro): como funcionam e como ajustar |
| **[Comandos](Commands-pt)** | Teclas, ordens de chat/voz e comandos de console |
| **[Configuração](Configuration-pt)** | O `.cfg`, a seção `[AI]` e os painéis `F8` e `I` |
| **[Problemas comuns](Troubleshooting-pt)** | Sintoma → causa → solução |
| **[Como funciona](How-it-works-pt)** | Arquitetura e o caminho de uma ordem |
| **[Histórico de versões](https://github.com/julianu37/AI-Companions/blob/main/package/CHANGELOG.md)** | O changelog completo, da 1.6.0 à 1.9.1 |
| **[Créditos e licença](Credits-and-license-pt)** | Upstream, terceiros e licenças |

## Links rápidos

| O quê | Onde |
|---|---|
| **Releases (download)** | https://github.com/julianu37/AI-Companions/releases |
| **Repositório** | https://github.com/julianu37/AI-Companions |
| **Changelog** | https://github.com/julianu37/AI-Companions/blob/main/package/CHANGELOG.md |
| **Upstream (MIT)** | https://github.com/MaddhattrRocks/Offline-Companions-Reborn |

A release publicada mais recente é a **1.9.1** (26/10/2026), com **dois** arquivos para escolher:

| Arquivo | Tamanho | O que vem |
|---|---|---|
| `AI_Companions-1.9.1.zip` | ~314 MB | O mod **e** os modelos de voz (STT + TTS) já prontos |
| `AI_Companions-1.9.1-sem-modelos.zip` | ~28,5 MB | Só o mod e as traduções — você baixa os modelos por fora |

> **Qual escolher?** Se você quer só instalar e usar, pegue o **completo**. Se a sua internet é lenta (ou você já
> tem os modelos), pegue o **sem-modelos** e siga a seção de modelos em **[Instalação](Installation-pt)**.

> **1.9.1:** o pacote está ~54 MB mais leve — o modelo `ggml-base-q5_1.bin`, que não era usado, não vem mais (continua
> sendo um download opcional).

> **A 1.9.0 em uma linha:** o mod fala **cinco idiomas** (português, inglês, espanhol, francês e italiano) seguindo o
> **idioma do jogo**, e a **voz e o reconhecimento da fala são automáticos** — `[AI] TtsVoice = 0` e `TtsLanguage` /
> `VoiceLanguage` = `auto`, e um valor explícito continua ganhando. **Trocar o idioma do jogo não exige reiniciar.**

> **Já tem uma versão anterior?** O mod passou a se chamar **AI Companions** (era *ModAI-BR-Companions*) e a pasta
> agora é `BepInEx\plugins\AI-Companions\`: **apague a pasta antiga** (`BepInEx\plugins\ModAI-BR-Companions\`)
> antes de extrair — duas cópias do mesmo plugin fazem o BepInEx carregar o mod duas vezes. **Nada seu se perde**:
> o save, os companheiros, a casa e o inventário continuam valendo (o GUID do plugin e as chaves `HC_*` foram
> preservados de propósito).

> A **1.9.0** é a release dos **cinco idiomas**: **português, inglês, espanhol, francês e italiano** — a interface,
> as falas do companheiro, os presets de personalidade e o contrato do LLM seguem o **idioma do jogo**, e a voz e o
> reconhecimento da fala **escolhem o idioma sozinhos** (`[AI] VoiceLanguage`, `TtsLanguage` e `TtsVoice` =
> `auto` / `0`). Trocar o idioma do jogo **não exige reiniciar**: os painéis se reconstroem na próxima abertura e o
> HUD, as falas e o TTS/STT acompanham.
>
> A **1.8.3** é onde o **inglês virou o idioma nativo** (chaves e descrições do `.cfg`) e o **português virou
> tradução** — os painéis (`I` e `F8`) seguem o **idioma do jogo**, e um idioma novo entra por
> `Translations/<Idioma>.json` + `Speech/<Idioma>.json`.
>
> A **1.8.2** deixa a escolha do LLM a um clique: na seção **Configurações do LLM** do painel `I`, o dropdown
> **"Selecione seu Provedor LLM"** preenche a **URL e o modelo** de **Groq**, **LM Studio** ou **OpenRouter** (a
> chave continua com você), e o botão **"Crie sua Chave de API Grátis AQUI"** abre a página da chave no navegador.
> A **velocidade da fala** ficou **só no painel `F8`**.
>
> A **1.8.1** traz o **Groq como LLM padrão** (grátis e rápido: a chave sai em `console.groq.com/keys`), com o
> **LM Studio/Ollama** como opção local e o **OpenRouter** ainda disponível — só que o tier gratuito dele é mais lento.
> E mantém tudo o que a **1.8.0** trouxe: **vários companheiros**, cada um com a sua aba no painel `I` (nome, tipo,
> aparência, voz e personalidade próprias, ordem pelo nome e um teto de 10), o **editor de aparência com preview 3D** e
> a seção **Configurações do LLM** no painel `I`.
>
> A **1.8.0** corrigiu o **peso**: o **Megingjord** (+150) e os demais itens com bônus de carga **somam** no teto do
> companheiro (o `/300` vira `/450`), valendo com o cinto em **qualquer lugar da mochila** — não precisa equipar —
> e **uma vez só** (dois cintos não empilham).
