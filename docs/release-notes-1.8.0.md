# 1.8.0 — o peso do companheiro agora conta o que ele carrega

![AI Companions — companheiros NPC com IA para Valheim, em PT-BR](https://raw.githubusercontent.com/julianu37/AI-Companions/main/docs/images/banner.png)

> **Correção do relato enviado no Nexus:** *"quando eu coloco a megingjord no inventário do companion ela não aumenta o
> peso em 150"*. **Resolvido** — e o teto de peso passou a respeitar qualquer item com bônus de carga.
>
> Esta release **inclui tudo da 1.7.0** (vários companheiros com aba própria, editor de aparência com preview 3D,
> OpenRouter como padrão da IA). A 1.7.1 não existiu: o número foi pulado de propósito.

## O que mudou

### Peso e capacidade de carga

- **O Megingjord (+150) passa a contar** — e qualquer item construído do mesmo jeito no jogo. O teto efetivo é
  `[General] MaxCarryWeight` **+ bônus**, e o valor é lido do **asset** (`SE_Stats.m_addMaxCarryWeight`), **não** de um
  `150` fixo: um cinto modificado para +250 funciona igual.
- **Vale com o cinto em qualquer lugar da mochila** — não precisa equipar no slot de utilidade. E vale **uma vez só**:
  dois cintos iguais não somam, como o jogo trata os efeitos de equipamento.
- **Onde o teto novo vale:** o `Encumbered` (andar lento), o painel do companheiro (`/450`) e a barra do HUD, a recusa
  de loot e do recolhimento do chão, o ferreiro, o upkeep, o "mochila cheia" da pesca/caça e o aviso de "estou cheio".
- **A coleta também soma:** `[Gathering] OverweightThreshold` virou o valor **base** (298) e o bônus entra por cima —
  com o cinto na mochila o companheiro de coleta só volta para casa perto de **448**, e continua coletando acima de 300.
- **Uma linha de log por mudança:** `[Carry] "Thorin": +150 de capacidade (Megingjord) - teto 450`.
- **Sem custo por frame:** o bônus é cacheado por inventário e recalculado quando a mochila muda.

### Por que não funcionava antes

O limite de peso do companheiro é **do mod** (`[General] MaxCarryWeight`, 300 por padrão), não do jogo: o vanilla **não**
impõe limite a um `Humanoid` que não é jogador (`Inventory.CanAddItem` só olha espaço de grade). E o `+150` do cinto é um
**status effect** cujo **único leitor** no vanilla é `Player.GetMaxCarryWeight()` — o companheiro nunca passava por ali,
então o cinto só ocupava espaço e pesava.

## O que já vinha da 1.7.0 (nesta mesma release)

- **Até 10 companheiros, cada um com identidade própria**: aba por companheiro no painel <kbd>I</kbd>, **invocador de
  graça**, **ordem e conversa pelo nome** (`Gruncha, colete 10 pedras`), **nome e tipo editáveis**, voz e personalidade
  por companheiro (sobrevivem à morte).
- **Editor de aparência com preview 3D** — gênero, cabelo, barba, pele, tom e sombra do cabelo, aplicado **sem respawn**.
- **OpenRouter como padrão, sem instalar nada**: `[IA] BaseUrl` = `https://openrouter.ai/api/v1`, `Modelo` =
  `openrouter/free`, com a **chave gratuita** de [openrouter.ai/keys](https://openrouter.ai/keys). LM Studio e Ollama
  continuam suportados como opção local. Seção **Configurações do LLM** no painel <kbd>I</kbd>, com **Testar conexão**.
- **O `para` responde o que ele estava fazendo** ("Ok: parar de coletar madeira"), e as falas do próprio mod são
  **faladas** com a voz do companheiro.
- **Correções**: botões dos painéis que morriam depois de abrir o inventário (inclusive as setas de cabelo/barba),
  cabelo/barba aplicados de verdade, sliders de cor que voltavam ao início, e a rodada de correções de ordens.

## Arquivos

| Arquivo | Tamanho | O que vem |
|---|---|---|
| `AI_Companions-1.8.0.zip` | ~368 MB | O mod **e** os modelos de voz (STT + TTS) — **recomendado** |
| `AI_Companions-1.8.0-sem-modelos.zip` | ~28,5 MB | Só o mod e as traduções (vem `tokens.txt` e `espeak-ng-data\`) |

> **Ao atualizar de uma versão anterior, apague a pasta antiga** `BepInEx\plugins\ModAI-BR-Companions\` antes de
> extrair: duas cópias do mesmo plugin fazem o BepInEx carregar o mesmo mod duas vezes. O `.cfg`
> (`BepInEx\config\Maddhattr.OfflineCompanionsReborn.cfg`) e o save **não** são afetados.

## Requisitos e instalação

- **Valheim 1.0.x** + **BepInExPack_Valheim 5.4.2351** (BepInEx 5).
- **Um LLM**: **OpenRouter** (padrão — só a chave gratuita e internet) **ou** **LM Studio/Ollama** local (opcional).
- **Windows x64** para as duas partes de voz; **microfone** só para as ordens por voz.

Passo a passo completo, **com imagens**: https://github.com/julianu37/AI-Companions/wiki/Instalacao

## Em ação

| | | |
|---|---|---|
| ![Abas por companheiro](https://raw.githubusercontent.com/julianu37/AI-Companions/main/docs/wiki/images/1-abas-do-companheiro.jpg) | ![Editor de aparência](https://raw.githubusercontent.com/julianu37/AI-Companions/main/docs/wiki/images/2-editor-de-aparencia.jpg) | ![Invocar e LLM](https://raw.githubusercontent.com/julianu37/AI-Companions/main/docs/wiki/images/3-invocar-e-llm.jpg) |
| **Uma aba por companheiro** (tecla `I`) | **Editor de aparência** com preview 3D | **Invocar** e **Configurações do LLM** |
| ![Menu radial](https://raw.githubusercontent.com/julianu37/AI-Companions/main/docs/wiki/images/4-menu-radial.jpg) | ![Inventário do companheiro](https://raw.githubusercontent.com/julianu37/AI-Companions/main/docs/wiki/images/5-inventario-do-companheiro.jpg) | ![Ordem no chat](https://raw.githubusercontent.com/julianu37/AI-Companions/main/docs/wiki/images/1-ordem-no-chat.jpg) |
| **Menu radial** (segure `E`) | **Inventário do companheiro** | **A ordem no chat** |

## Créditos e licença

**Projeto independente** baseado no [Offline Companions Reborn](https://github.com/MaddhattrRocks/Offline-Companions-Reborn)
(**MIT**), de **Maddhattr**, que por sua vez é um fork do *Offline Companions*, de **ProfMags** (**MIT**). A atribuição
completa está em `LICENSE`, `NOTICE.md` e `THIRD_PARTY_NOTICES.md`.

Voz: [Whisper.net](https://github.com/sandrohanea/whisper.net) + modelos do **openai/whisper** (MIT) e
[Kokoro-82M](https://huggingface.co/hexgrad/Kokoro-82M) via [sherpa-onnx](https://github.com/k2-fsa/sherpa-onnx)
(Apache-2.0).
