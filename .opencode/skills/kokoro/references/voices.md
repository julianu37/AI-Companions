# Vozes do Kokoro — ids do export e qualidade

Duas listas diferentes circulam e **confundi-las é o erro clássico**:

1. **`sid` (o que o mod usa)** — índice do export do sherpa-onnx. É o número em `[AI] TtsVoice` e `HC_AiVoice`.
2. **Nome da voz** (`pf_dora`, `af_heart`) — vem do modelo original no Hugging Face.

## 1. Mapa `sid → nome` (export `kokoro-multi-lang-v1_0`, doc do k2-fsa)

| id | voz | id | voz | id | voz |
|---|---|---|---|---|---|
| 0 | af_alloy | 18 | am_puck | 36 | im_nicola |
| 1 | af_aoede | 19 | am_santa | 37 | jf_alpha |
| 2 | af_bella | 20 | bf_alice | 38 | jf_gongitsune |
| 3 | af_heart | 21 | bf_emma | 39 | jf_nezumi |
| 4 | af_jessica | 22 | bf_isabella | 40 | jf_tebukuro |
| 5 | af_kore | 23 | bf_lily | 41 | jm_kumo |
| 6 | af_nicole | 24 | bm_daniel | **42** | **pf_dora** 🚺 |
| 7 | af_nova | 25 | bm_fable | **43** | **pm_alex** 🚹 |
| 8 | af_river | 26 | bm_george | **44** | **pm_santa** 🚹 |
| 9 | af_sarah | 27 | bm_lewis | 45 | zf_xiaobei |
| 10 | af_sky | 28 | ef_dora | 46 | zf_xiaoni |
| 11 | am_adam | 29 | em_alex | 47 | zf_xiaoxiao |
| 12 | am_echo | 30 | ff_siwis | 48 | zf_xiaoyi |
| 13 | am_eric | 31 | hf_alpha | 49 | zm_yunjian |
| 14 | am_fenrir | 32 | hf_beta | 50 | zm_yunxi |
| 15 | am_liam | 33 | hm_omega | 51 | zm_yunxia |
| 16 | am_michael | 34 | hm_psi | 52 | zm_yunyang |
| 17 | am_onyx | 35 | if_sara | — | — |

**Conferido em jogo:** 42 = `pf_dora` (feminina), 43 = `pm_alex` (masculina), 44 = `pm_santa` (masculina) — é o
default do mod até hoje (`CompanionTts.DefaultVoice` = 42).

> ⚠️ **Cuidado com a contagem.** O mapa oficial do k2-fsa para a `v1_0` tem **53 speakers (0–52)** e **não**
> inclui `em_santa` (o HF tem 3 vozes em espanhol; aqui só `ef_dora` e `em_alex`). O log do nosso mod já
> registrou **54 vozes** (`NumSpeakers`), o que provavelmente é um contador diferente — mas **antes de usar ids
> ≥ 45**, confirme: `[TTS] Modelo CARREGADO ... | N vozes` no log **e** o `modia_tts_teste` com o `sid` novo.
> Se `em_santa` estivesse no modelo, tudo de 30 em diante deslocaria 1 e `pf_dora` viraria 43 — o teste de ouvido
> (42 fala português) mostra que **não** é o caso do modelo que a gente empacota.

## 2. Qualidade publicada (Hugging Face `VOICES.md`)

A nota mede a **qualidade/alinhamento** e a **quantidade de treino** da referência (não é gosto). `A` é o topo.

### Inglês americano (prefixo `a`)

| voz | gênero | nota | voz | gênero | nota |
|---|---|---|---|---|---|
| af_heart ❤️ | F | **A** | am_fenrir | M | C+ |
| af_bella 🔥 | F | **A-** | am_michael | M | C+ |
| af_nicole 🎧 | F | B- | am_puck | M | C+ |
| af_aoede | F | C+ | am_liam | M | D |
| af_kore | F | C+ | am_onyx | M | D |
| af_sarah | F | C+ | am_echo | M | D |
| af_alloy | F | C | am_eric | M | D |
| af_nova | F | C | am_santa | M | D- |
| af_sky | F | C- | am_adam | M | F+ |
| af_jessica | F | D | af_river | F | D |

### Inglês britânico (`b`)

| voz | gênero | nota |
|---|---|---|
| bf_emma | F | B- |
| bf_isabella | F | C |
| bm_fable | M | C |
| bm_george | M | C |
| bm_lewis | M | D+ |
| bf_alice / bf_lily / bm_daniel | — | D |

### Espanhol (`e`), Francês (`f`), Italiano (`i`)

| voz | idioma | gênero | nota |
|---|---|---|---|
| ef_dora | es | F | (sem nota) |
| em_alex | es | M | (sem nota) |
| ff_siwis | fr | F | **B-** (única do fr; <11 h de treino) |
| if_sara | it | F | C |
| im_nicola | it | M | C |

(Segundo o HF existe também `em_santa` 🚹 — **não** está no export do sherpa v1_0; ver o aviso acima.)

### Hindi (`h`), Japonês (`j`), Mandarim (`z`)

| voz | idioma | gênero | nota |
|---|---|---|---|
| hf_alpha | hi | F | C |
| hf_beta | hi | F | C |
| hm_omega | hi | M | C |
| hm_psi | hi | M | C |
| jf_alpha | ja | F | C+ |
| jf_gongitsune | ja | F | C |
| jf_tebukuro | ja | F | C |
| jf_nezumi | ja | F | C- |
| jm_kumo | ja | M | C- |
| zf_xiaobei / zf_xiaoni / zf_xiaoxiao / zf_xiaoyi | zh | F | D |
| zm_yunjian / zm_yunxi / zm_yunxia / zm_yunyang | zh | M | D |

### Português do Brasil (`p`)

| voz | gênero | nota |
|---|---|---|
| pf_dora | F | (sem nota; é o default do mod) |
| pm_alex | M | (sem nota) |
| pm_santa | M | (sem nota) |

## Curadoria sugerida (se um dia houver lista por idioma)

| Idioma | 1ª escolha | Alternativas |
|---|---|---|
| pt-BR | `pf_dora` (42) | `pm_alex` (43), `pm_santa` (44) |
| en-US | `af_heart` (3) | `af_bella` (2), `af_nicole` (6), `am_fenrir` (14), `am_michael` (16), `am_puck` (18) |
| en-GB | `bf_emma` (21) | `bm_george` (26), `bm_fable` (25) |
| es | `ef_dora` (28) | `em_alex` (29) |
| fr | `ff_siwis` (30) | — |
| it | `if_sara` (35) | `im_nicola` (36) |
| hi | `hf_alpha` (31) | `hm_omega` (33) |
| ja | `jf_alpha` (37) | `jm_kumo` (41) |
| zh | `zf_xiaoxiao` (47) | qualquer (nota D) |

## Como adicionar uma voz ao mod (receita)

1. Pôr o `sid` na lista curada (`CompanionTts.PortugueseVoices` hoje; a lista por idioma é o que o plano de
   multilinguagem propõe).
2. Criar a chave de rótulo `hc_ai_voice_<sid>` em `Translations/Portuguese_Brazilian.json` **e** no dicionário
   inglês do `ModLocalization` (o rótulo é o que o painel mostra: `pf_dora (feminina)`).
3. Testar com **`modia_tts_teste`** e com o botão **Ouvir** do painel `I`; conferir o RTF no log.
4. Nada de mexer no `TtsModel`: é o mesmo `.onnx` para todas as vozes.

## Limites de texto (o que estraga a fala)

- **Curto demais (<10–20 tokens)**: sai fraco/sem corpo — juntar frases curtas em um trecho.
- **Longo demais (>400 tokens)**: sai apressado — cortar em sentenças (o `SplitSentences` já faz).
- Faixa ideal: **100–200 tokens**.
