# 1.7.0 — vários companheiros, cada um com identidade própria

![AI Companions — companheiros NPC com IA para Valheim, em PT-BR](https://raw.githubusercontent.com/julianu37/AI-Companions/main/docs/images/banner.png)

> **O mod agora se chama AI Companions** (era *ModAI-BR-Companions*). O **GUID, o `.cfg`, o `Companions.dll` e as
> chaves `HC_*` foram preservados**: atualizar **não** perde configuração, companheiro, casa, inventário nem save.

## O que mudou

### Vários companheiros, cada um com identidade própria

- **Invocador de graça** na aba IA: escolha o **tipo** (Guerreiro, Arqueiro, Mago, Cozinheiro, Coletor, Fazendeiro) e o
  companheiro nasce pronto, sem passar pelo Trader.
- **Uma aba por companheiro** no painel <kbd>I</kbd> — nome, tipo, aparência, voz, personalidade e Chamar/recall.
- **Ordem e conversa pelo nome**: `Gruncha, colete 10 pedras` vale **só** para ele (prompt, personalidade, memória e
  ordem são os dele). Sem o nome, a ordem continua valendo para todos.
- **Nome e tipo editáveis** — trocar o tipo renasce o companheiro preservando nome, inventário, equipamento,
  aparência, casa/cama, skills, perfil de IA, stamina, ferramentas emprestadas e o registro do cozinheiro.
- **Voz e personalidade por companheiro** (guardadas no ZDO dele, e a lápide as devolve no respawn). O que está no
  `[IA]` passa a ser o **padrão** de quem não tem escolha própria.
- **Teto de 10 companheiros** (`[General] MaxCompanions`), contando o roster (vivos + mortos + perdidos) — morrer não
  abre vaga; o Santuário continua recuperando os mortos.

### Editor de aparência com preview 3D

Gênero, corte de cabelo, barba, cor de pele, tom e sombra do cabelo — com o **boneco mudando ao vivo** e o **Aplicar**
gravando no companheiro **sem respawn**.

### IA

- **OpenRouter é o padrão, sem instalar nada**: `[IA] BaseUrl` = `https://openrouter.ai/api/v1`, `[IA] Modelo` =
  `openrouter/free` — basta colar a **chave gratuita** de [openrouter.ai/keys](https://openrouter.ai/keys) na `ApiKey`.
- **LM Studio e Ollama continuam suportados** como opção **local/offline** (troque a URL e o modelo no `.cfg`).
- **Configurações do LLM no jogo**: a aba IA ganhou URL, modelo, chave (mostrada **mascarada**), prefixo, memória,
  temperatura, **Aplicar** e **Testar conexão** — sem editar arquivo. As opções avançadas seguem no painel
  <kbd>F8</kbd>.
- **O `para` responde o que ele estava fazendo**: `para` enquanto coleta madeira → *"Ok: parar de coletar madeira"*
  (antes dizia sempre "parar de seguir"). Minerando, cozinhando, reparando… cada trabalho com a sua frase.
- **As falas do próprio mod passam a ser faladas**: "Preciso de uma picareta", as falas de evento e a mudança de modo
  saem na **voz do companheiro** (TTS local), com a mesma deduplicação das respostas da IA.

### Correções

- **Botões dos painéis do mod não "morriam" mais**: os botões clonados do jogo herdavam `interactable = false` do
  molde do botão de craft e paravam de responder depois de abrir o inventário — era isso que matava as **setas de
  cabelo e barba**.
- **Cabelo e barba aplicados de verdade**: o jogo cacheia o hash do item **antes** de anexar; quando o anexo falhava
  uma vez, toda mudança daquele corte virava no-op silencioso. O cache é limpo a cada aplicação e o joint da cabeça é
  procurado quando está nulo.
- **Sliders de cor voltavam ao início**: as cores guardadas eram sobrescritas pelos padrões na montagem do painel.
- **Ordens**: `inventory_close` entrou no contrato (30 ações) e "fecha o inventário" não abre mais a janela; o nome no
  comando ficou conservador; a escrita no ZDO espera a posse (com fila) em vez de se perder no multiplayer; o LLM
  ganhou **id de requisição** (resposta atrasada é descartada, sem aplicar ordem velha); a lápide e o respawn comparam
  o id do companheiro antes do nome; os caches de estoque passaram a ser **por companheiro**.
- **Layout**: a coluna do meio do painel do starter com o *Ouvir* visível e sem sobreposição; o campo de nome já vem
  preenchido; o rodapé do painel <kbd>I</kbd> ganhou faixa própria e duas linhas de resumo.

## Arquivos

| Arquivo | Tamanho | O que vem |
|---|---|---|
| `AI_Companions-1.7.0.zip` | ~368 MB | O mod **e** os modelos de voz (STT + TTS) — **recomendado** |
| `AI_Companions-1.7.0-sem-modelos.zip` | ~28,4 MB | Só o mod e as traduções (vem `tokens.txt` e `espeak-ng-data\`) |

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
