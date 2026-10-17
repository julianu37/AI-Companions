# ModAI-BR-Companions 1.6.0 — o companheiro entende você (e fala)

**A camada de IA chegou: ordens por chat e por voz, o companheiro fala, personalidade/memória e um painel no jogo — tudo local, em PT-BR.**

Este é o primeiro release do fork com a IA (Fases 3.1 a 3.8). Até aqui o mod era "só" o companheiro do Offline Companions Reborn em português; a partir de agora você conversa com ele.

## O que é

Um fork em **PT-BR** do [Offline Companions Reborn](https://github.com/MaddhattrRocks/Offline-Companions-Reborn) (**MIT**), de **Maddhattr** (que por sua vez continua o "Offline Companions", de **ProfMags**), para **Valheim 1.0**. O companheiro já era um membro da equipe: segue, trabalha (madeira/pedra/minério, caça, pesca, fazenda, cozinha, fundição, reparo e reabastecimento da base), luta, come, descansa, dorme, usa portais e carroça. Neste release ele ganha **ouvidos, boca e cabeça** — e continua sendo um NPC de Valheim, não um chatbot.

## Destaques

- **Ordens por texto no chat:** `colete 10 madeiras`, `segue`, `para`, `minere ferro`, `fica na defensiva`. Um **LLM local** (qualquer servidor com API compatível com a OpenAI) devolve a intenção em JSON e o mod executa **a mesma ação do menu radial**.
- **Ordens por voz:** segure **V**, fale e solte. O áudio é transcrito **dentro do jogo** (STT local, Whisper.net in-process) e entra no mesmo caminho do chat — mesma ordem, mesma personalidade, mesma memória.
- **O companheiro fala:** TTS local (**Kokoro-82M** via **sherpa-onnx**), em PT-BR, com a voz saindo da posição dele no mundo (3D), por frase e com cache — sem travar o jogo.
- **Personalidade, memória e conversa livre:** `bom dia` e perguntas viram conversa de personagem (5 presets PT-BR + texto livre seu), com memória das últimas trocas **por companheiro**.
- **Ordens com alvo:** `ataca o greydwarf`, `equipa o machado de bronze`, `larga a pedra`, `joga a madeira fora`.
- **Recolher itens do chão:** `recolhe os itens` / `pega o machado do chão` — inclusive o que ele mesmo largou de propósito.
- **Painel da IA no jogo** (tecla **I**): personalidade, voz (com o botão **Ouvir**), volume, velocidade, push-to-talk, memória e conversa — sem sair do jogo.
- **Saída estruturada (JSON schema)** no pedido ao modelo: é o que faz **modelos pequenos** (ex.: `qwen2.5-3b-instruct`) acertarem o formato.
- **Tradução PT-BR auditada:** 386 chaves de UI e 186 falas, **0 ausentes e 0 textos em inglês visíveis** (nome do companheiro, hovers, mensagens do jogo e cumprimentos do recall incluídos).
- **Correções:** o **eco da voz** (era a zona de reverb global do jogo — a voz agora sai seca), a **ordem de pedra** que não parava no alvo, e o **painel F8** que engolia cliques (as camadas da UI do mod agora ficam acima do HUD do jogo).

## Instalação

1. Tenha o **BepInExPack_Valheim 5.4.2351** instalado.
2. Extraia a pasta `ModAI-BR-Companions` deste pacote para `...\Valheim\BepInEx\plugins\`.
3. Os **modelos** (STT `whisper\` e TTS `tts\`) ficam **dentro** dessa pasta — o pacote **completo** já os traz; o **"sem modelos"** não.

O passo a passo completo, os requisitos, as teclas, a configuração do LLM, os comandos de console, as licenças e os problemas conhecidos estão no **`LEIA-ME.txt`** que vem dentro do zip.

## Requisitos

- **Valheim 1.0.x** (alvo deste release: **1.0.17**).
- **BepInExPack_Valheim 5.4.2351** (BepInEx 5.4.23.5). **O Jotunn não é necessário** — este mod não referencia nenhuma API do framework (nem dependência declarada, nem referência no build).
- **Windows x64** (as duas metades da voz usam binários nativos de Windows) e um **microfone** para as ordens por voz.
- Para o companheiro **entender** ordens: um **servidor de LLM local** compatível com a API da OpenAI — o padrão é o **LM Studio** em `http://127.0.0.1:1234/v1` com um modelo pequeno (ex.: `qwen2.5-3b-instruct`), configurado em `[IA] BaseUrl` / `[IA] Modelo` no `.cfg`. O `BaseUrl` aceita Ollama, llama.cpp, vLLM etc.
- **Multiplayer:** todo jogador precisa do mod; servidor dedicado também.

## Os modelos

Os modelos são grandes e por isso o release vem em **dois pacotes**:

| Pacote | Conteúdo | Tamanho |
|---|---|---|
| **Completo** | o mod + os modelos de voz e fala | **368 MB** (zip) |
| **Sem modelos** | só o mod (você copia os seus) | **20 MB** (zip) |

Os modelos, por dentro:

| Modelo | Para que serve | Tamanho |
|---|---|---|
| `whisper\ggml-small-q5_1.bin` | **STT** — o que ele **ouve** (padrão desde a Fase 3.3b; bem melhor em português que o `base`) | **181 MB** |
| `tts\kokoro\model.int8.onnx` | **TTS** — o que ele **fala** (Kokoro-82M quantizado) | **109 MB** |
| `tts\kokoro\voices.bin` | as vozes do Kokoro (o mod usa as 3 pt-BR: 42 feminina, 43 e 44 masculinas) | **27 MB** |
| `tts\kokoro\espeak-ng-data\` | fonemização do espeak-ng (usada pelo Kokoro) | **17 MB** |

> Se você ficar com o pacote **sem modelos**: ponha o `.bin` do Whisper em `...\ModAI-BR-Companions\whisper\` e o Kokoro (`model.int8.onnx`, `voices.bin`, `tokens.txt` e a pasta `espeak-ng-data`) em `...\ModAI-BR-Companions\tts\kokoro\`. O modelo do Whisper `ggml-base-q5_1.bin` (57 MB) também funciona e é mais rápido, mas erra mais em português — o `.cfg` (`[IA] SttModelo`) permite trocar.

## Licenças e créditos

- **Este mod: MIT** — é a licença do upstream, e o aviso de copyright do **ProfMags** e do **Maddhattr** é mantido junto das nossas mudanças, como manda a licença. Ver `THIRD_PARTY_NOTICES.md`, `NOTICE.md` e `LICENSE`.
- **Offline Companions Reborn** (Maddhattr) e **Offline Companions** (ProfMags) — **MIT**.
- **Whisper.net** (sandrohanea) + **whisper.cpp** (ggerganov) — **MIT** — redistribuídos (o STT).
- **sherpa-onnx** (k2-fsa), NuGet `org.k2fsa.sherpa.onnx` 1.13.8 — **Apache-2.0** — redistribuído (o TTS).
- **onnxruntime** — **MIT** — redistribuído (runtime do modelo de voz, junto com o sherpa-onnx).
- **Kokoro-82M** (hexgrad) — **Apache-2.0** — o modelo de voz, redistribuído em `tts\kokoro\` (o arquivo `LICENSE` da pasta traz o texto da licença).
- **Modelos do Whisper** — redistribuídos pelo projeto whisper.cpp/OpenAI.
- **Thrall** (ego.ai) — *all rights reserved*: citado apenas como **referência conceitual** de estudo; **nenhum** código ou binário dele é redistribuído aqui.

Não é afiliado nem endossado pela Iron Gate Studio, Coffee Stain Publishing, ProfMags ou Maddhattr. "Valheim" e demais marcas pertencem aos seus donos. Se algum dos autores quiser que algo seja removido, alterado ou incorporado, é só pedir.

## Problemas conhecidos

- **Volume da voz:** a fala do companheiro **não** passa pelo mixer do jogo — ela **ignora os sliders de volume do Valheim** (música/SFX). Quem manda é o `[IA] TtsVolume` (ou o slider do painel da IA, tecla **I**).
- **Sem os modelos** (ou sem o LLM ligado) o mod carrega normalmente, mas **não fala / não entende voz** (e sem o LLM não entende ordens escritas).
- **Primeira ordem por voz** pode demorar mais: o modelo do Whisper é carregado na primeira vez que você usa o microfone (ele já tenta carregar em segundo plano ao entrar no mundo). A primeira frase falada tem o mesmo comportamento.
- **`localhost` no `[IA] BaseUrl`:** em alguns Windows a resolução de IPv6 atrasa ou falha a conexão com o LM Studio — use `127.0.0.1`.
- **Tecla V:** por padrão ela é também o *Toggle Auto-Pickup* do Valheim. O mod **bloqueia** o auto-pickup enquanto a voz está ligada e a tecla aponta para o V, e avisa você uma vez; se você reatribuir o auto-pickup no jogo, o mod não bloqueia nada (o jogo fica intacto).
- **O `.cfg` é o mesmo do upstream** (`BepInEx\config\Maddhattr.OfflineCompanionsReborn.cfg`): as opções que você já tinha são migradas automaticamente, e as novas aparecem sozinhas.

---

Código-fonte: <https://github.com/julianu37/ModAI-BR-Companions>
