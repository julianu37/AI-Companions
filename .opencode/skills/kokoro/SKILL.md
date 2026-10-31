---
name: Kokoro TTS
description: Como o companheiro fala (Kokoro-82M via sherpa-onnx) — catálogo de vozes por idioma, ids do export, códigos de idioma, integração nativa no Unity e armadilhas. Use ao mexer em voz, idioma do TTS ou no catálogo de vozes.
---

# Kokoro no AI Companions (voz do companheiro)

O mod **não** usa HTTP para falar: o Kokoro-82M roda **dentro do jogo**, pelo
[sherpa-onnx](https://github.com/k2-fsa/sherpa-onnx) 1.13.8 (`org.k2fsa.sherpa.onnx`), em `CompanionTts`.
O `LlmClient` é a única coisa que sai da máquina.

## O que é o modelo

- **Kokoro-82M**: 82 milhões de parâmetros, pesos **Apache-2.0**, saída **24 kHz mono**.
  `hexgrad/Kokoro-82M` no Hugging Face; o G2P é o [misaki](https://github.com/hexgrad/misaki) + fallback **espeak-ng**.
- **Um único arquivo de modelo serve todos os idiomas** — quem escolhe a língua é o `[AI] TtsLanguage`
  (fonemizador/espeak), não o `.onnx`. Não existe "modelo por idioma".
- No jogo, os arquivos ficam em `BepInEx\plugins\AI-Companions\tts\kokoro\`:
  `model.int8.onnx` (109 MB), `voices.bin` (27 MB), `tokens.txt`, `espeak-ng-data\` e `LICENSE`.

## Idiomas que o Kokoro fala

| Prefixo | Idioma | espeak | Valheim (`GetSelectedLanguage`) |
|---|---|---|---|
| `a` | inglês americano | `en-us` | `English` |
| `b` | inglês britânico | `en-gb` | (não existe no jogo; opção do jogador) |
| `e` | espanhol | `es` | `Spanish` |
| `f` | francês | **`fr`** (`fr-fr` **não funciona** — ver abaixo) | `French` |
| `h` | hindi | `hi` | (não existe no jogo) |
| `i` | italiano | `it` | `Italian` |
| `j` | japonês | (G2P próprio) | `Japanese` |
| `p` | português do Brasil | `pt-br` | `Portuguese_Brazilian` |
| `z` | mandarim | (G2P próprio) | `Chinese` |

> **`pt-br`, não `pt`** (pergunta recorrente): o próprio `espeak-ng-data\lang\roa\pt` diz `name Portuguese (Portugal)`
> com `phonemes pt-pt`; o `lang\roa\pt-BR` diz `name Portuguese (Brazil)`, `language pt-br`. No harness o mesmo texto
> sai com **80.058** amostras em `pt-br` e **72.154** em `pt` — são vozes diferentes, e a voz do mod (`pf_dora`) é a
> brasileira. Trocar para `pt` mudaria o sotaque de quem já funciona.

`[AI] TtsLanguage` é **obrigatório** para este modelo (vazio ⇒ o sherpa recusa carregar): `pt-br`, `en-us`,
`en-gb`, `es`, `fr`, `hi`, `it`, `ja`, `zh`.

**Medido no jogo (30/10/2026, harness com o modelo empacotado):** funcionam `pt-br`, `pt`, `en-us`, `es`, `fr`,
`it`, `ja`, `zh`, `hi`. **`en-gb` e `fr-fr` carregam (`NumSpeakers` > 0) mas `Generate` estoura
`NullReferenceException`** — o código tem que ser exatamente o do espeak que este export embarca (`fr`, não `fr-fr`;
`en-us`, não `en-gb`). Por isso a lista do F8 do mod tem **apenas os 5** que ele fala (`pt-br`, `en-us`, `es`, `fr`,
`it`), e um código fora deles não derruba o modelo que está carregado.

Três nuances que a lista de `lang_code` do projeto Python **não** conta:

- `misaki[ja]` e `misaki[zh]` são **extras do pacote Python** (`pip install misaki[ja]`). A gente **não** usa
  `KPipeline`/misaki: no jogo quem fonemiza é o sherpa-onnx (espeak-ng como fallback), então isso não se aplica.
- A doc do sherpa para o export `v1_0` diz que ele **só adiciona suporte a inglês e chinês**; o nosso **pt-BR
  funciona** via espeak (`language=pt-br`, validado em jogo). **`ja` e `zh` não foram validados** aqui, e o
  pacote que a gente embarca **não traz** os `lexicon-zh.txt` / `*-zh.fst` que a doc do k2-fsa usa — se um dia
  entrar chinês, testar com esses arquivos antes.
- `en` **não** é um código: o Kokoro separa **`en-us`** (prefixo `a`) e **`en-gb`** (prefixo `b`). Voz e idioma
  precisam combinar (voz `a*` com `en-gb` sai com sotaque trocado).

**Fora dos 8** (alemão, russo, polonês, turco, coreano, sueco, ucraniano): o Kokoro **não tem voz**. Decisão
tomada no plano: **fala com voz inglesa** (`en-us`) e registra no log.

## A voz é um `sid` (índice do export, não do HF)

`[AI] TtsVoice` e `HC_AiVoice` (por companheiro, 0 = segue o global) são o **speaker id do export do sherpa**.
O mapa do export `kokoro-multi-lang-v1_0` (doc oficial do k2-fsa) está em `references/voices.md` — e é ele que
vale: **42 = `pf_dora`, 43 = `pm_alex`, 44 = `pm_santa`** (conferido em jogo).

> ⚠️ **Não inferir ids pela lista do Hugging Face nem pela ordem alfabética**: a lista do HF tem 54 vozes e o
> mapa do sherpa v1_0 tem **53** (falta `em_santa`), então do 45 em diante o índice pode deslocar. Antes de usar
> um `sid` novo, conferir `NumSpeakers` no log (`[TTS] Modelo CARREGADO ... | N vozes`) e ouvir o `sid`.
> O nosso log já mostrou **54** — confirmar qual export está na pasta antes de confiar em ids ≥ 45.

Nome da voz: `<idioma><gênero>_<nome>` (`p`+`f` = `pf_dora`; `a`+`m` = `am_michael`). O gênero é `f`/`m`.

## Qualidade e limites de texto (importante para as falas do mod)

As notas do HF medem **qualidade/alinhamento** e **quantidade de treino** da voz (`A` … `F`); em pt/es/zh não
há nota publicada (dados pequenos, mas o pt funciona bem em jogo). As melhores para nós:

- **en-US**: `af_heart` (A), `af_bella` (A-), `am_fenrir`/`am_michael`/`am_puck` (C+), `af_nicole` (B-)
- **en-GB**: `bf_emma` (B-), `bm_george`/`bm_fable` (C)
- **fr**: `ff_siwis` (B-, é a única); **hi**: `hf_alpha`/`hf_beta`/`hm_omega`/`hm_psi` (C)
- **it**: `if_sara`/`im_nicola` (C); **ja**: `jf_alpha` (C+), as outras são piadas curtas
- **zh**: todas D (é o ponto fraco do modelo); **pt/es**: as 3/2 do export, sem nota

O modelo rende melhor em **100–200 tokens** (de ~500): **frases muito curtas (<10–20 tokens) saem fracas** e
**textos longos (>400) saem apressados**. A implicação para o mod: **juntar frases curtas** em um trecho e
**cortar** textos longos — o `CompanionTts.SplitSentences` já corta por frase; a união de frases curtas é
melhoria pendente.

## Integração neste projeto (o que já está resolvido — não refazer)

- **Nativas**: `sherpa-onnx-c-api.dll` + `onnxruntime.dll` em `runtimes\win-x64\native`. O `DllImport` procura na
  pasta do **jogo**, não na do plugin: `EnsureNativeLibraries` faz `SetDllDirectory(plugin\runtimes\win-x64\native)`,
  pré-carrega as duas por caminho absoluto com `LoadLibraryEx(..., LOAD_WITH_ALTERED_SEARCH_PATH)` e restaura.
- **`AudioClip`/`AudioSource` por reflexão** (o `UnityEngine.AudioModule` não é referenciável aqui: CS1705/CS7069).
- **Uma `AudioSource` 3D** na posição do companheiro, **`reverbZoneMix = 0`** (a `AudioReverbZone` global do
  Valheim tem raio de 1000 m e 4 s de cauda — sem isso a fala ecoa), uma fala por vez, `AudioClip` destruído no fim.
- **Síntese em thread de fundo** (`SemaphoreSlim` + filas), cache por **bytes** (32 MB) só das linhas do mod.
- **Trocar o idioma recarrega o modelo (2.0.0)**: o `Lang` escolhe o **fonemizador no load**, então o `OfflineTts`
  um-dia-carregado ficava no idioma antigo (voz `pf_dora` lendo português com fonemas ingleses, com o log dizendo
  `idioma pt-br`). Agora `_loadedLanguage` + `DropModelIfLanguageChanged` (só a thread de síntese libera, nunca outra)
  e `NoticeLanguageChange` (postfix do `SetupLanguage` + `SettingChanged` do `[AI] TtsLanguage`) reconstroem o modelo
  antes da fala seguinte e **esvaziam o cache** (as amostras eram do fonemizador antigo). Não reverter para "carrega
  uma vez e nunca mais".
- **RTF medido nesta máquina**: 0,73–1,07 com 4 threads (mais rápido que o tempo real). Na Raspberry Pi 4 o
  sherpa mede 3,19 com 4 threads — CPU fraca é o caso em que a fala "atrasa".
- **`Shutdown`** espera a thread (`Join(5000)`) e libera o `OfflineTts`; `Process`/`Speak` recusam depois do
  `_stopping`.
- **`audio.Handle != IntPtr.Zero` antes de `NumSamples`** — o binding devolve objeto não-nulo com handle nulo e
  isso é `AccessViolation`.

## Como testar rápido

- Console do jogo: **`modia_tts_teste <texto>`** sintetiza uma frase com a voz/idioma atuais e loga o RTF.
- Painel `I` → aba do companheiro → **Ouvir** fala a amostra com a voz **daquele** companheiro.
- `BepInEx\LogOutput.log`: `[TTS] Modelo CARREGADO ... | N vozes`, `Frase i/N: X ms, RTF y`, `AudioSource 3D`
  e `ReverbZoneMix da voz`.

## Mais detalhes

- `references/voices.md` — mapa `sid → nome` do export do sherpa + tabela completa de qualidade do HF.
- `references/integration.md` — esboço do `OfflineTtsConfig`, alternativas (Kokoro-FastAPI/HTTP) e a lista de
  armadilhas.
- `docs/re/tts-kokoro-sherpa-onnx.md` (engenharia reversa do que foi feito) e `docs/plan-multilinguagem.md`.
- Fontes: [VOICES.md](https://huggingface.co/hexgrad/Kokoro-82M/blob/main/VOICES.md),
  [kokoro](https://github.com/hexgrad/kokoro),
  [Kokoro-FastAPI](https://github.com/remsky/Kokoro-FastAPI),
  [sherpa-onnx — Kokoro](https://k2-fsa.github.io/sherpa/onnx/tts/pretrained_models/kokoro.html).
- Licenças: pesos **Apache-2.0**, `espeak-ng-data` **GPL-3.0** (uso como dado, não linkado), sherpa-onnx
  **Apache-2.0**; avisos em `LICENSES\` na pasta do plugin.
