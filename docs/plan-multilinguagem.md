# Plano — multilinguagem completa (EN / PT-BR / ES / FR)

Status: **M1 feita** (branch `feat/idiomas-auto`; M2–M5 pendentes). Base: skill `.opencode/skills/kokoro/`.

> **M1 entregue (ver `AGENTS.md`, *Fase M1 da multilinguagem*):** `Core/CompanionLocale.cs` (tabela única +
> `Resolve*`), `auto` como padrão de `[AI] VoiceLanguage`, `[AI] TtsLanguage` e `[AI] TtsVoice` (0 = automático),
> `[AI] VoiceInitialPrompt` vazio = vocabulário do idioma, e a correção do `_loadedLanguage` do `SpeechConfig`
> (portão de 30 s). O log diz `[Locale] English -> en / en-us | 20 vozes, padrao 3 (af_heart)` e, já no mundo,
> `[TTS] idioma efetivo: pt-br (auto) | voz 42 (auto, pf_dora)`. Sem UI nova e sem traduções nesta fase.

## Decisões (do jogador, 25/10/2026)

1. **`auto` primeiro**: a config ganha o sentinela "Automático"; valor explícito do jogador **sempre ganha**.
2. Idioma **sem voz no Kokoro** (de, ru, pl, tr, ko, sv, uk): **fala com voz inglesa** (`en-us`) e registra no log.
3. Idiomas desta leva: **EN, PT-BR, ES e FR**.
4. As traduções (UI, falas, vocabulário do STT) são **geradas** e depois revisadas pelo jogador.

Decisões confirmadas em 25/10/2026 (segunda rodada):

- **Vozes: todas as do idioma** (o `VOICES.md` é explícito), na ordem **padrão primeiro** e depois as melhores notas.
  Nada de curadoria "2–4": pt = 3, en-US = 20, es = 2, fr = 1 (o resto de cada família fica para quando entrar).
- **A lista mostra só as vozes do idioma ativo**; idioma sem Kokoro → lista e padrão **ingleses** (`en-us`).
- **Voz de companheiro de outro idioma: não é usada.** Se o `HC_AiVoice` guardado não pertence ao idioma ativo, a
  fala cai no **padrão do idioma ativo** (o ZDO não é reescrito — voltar o jogo para PT restaura a escolha).
- **`VoiceInitialPrompt` e presets de personalidade** por idioma.
- O idioma efetivo aparece **no F8 e no painel `I`** (`Automático (pt-BR · pf_dora)`).

## O que já existe (não refazer)

| Peça | Hoje |
|---|---|
| UI | `Translations/<Idioma>.json` (PT com ~400 chaves: UI + `hc_cfg_*` + `hc_cat_*` + vozes) e o **inglês embutido** em `ModLocalization._englishDefaults` (não precisa de arquivo) |
| Falas | `Speech/<Idioma>.json` (15 categorias / 186 linhas); fallback `speech.json` gerado |
| STT | `[AI] VoiceLanguage` (**"pt" fixo**), `SttModel` (Whisper multi-lingue), `VoiceInitialPrompt` (**vocabulário em PT**) |
| TTS | `[AI] TtsLanguage` (**"pt-br" fixo**), `TtsModel` (o mesmo .onnx para tudo), `TtsVoice` (42), `CompanionTts.PortugueseVoices = {42,43,44}` |
| Voz por companheiro | `HC_AiVoice` (0 = segue o global), rótulos `hc_ai_voice_<sid>` |
| Idioma do jogo | `Localization.instance.GetSelectedLanguage()` → `Portuguese_Brazilian`, `English`, `Spanish`, `French`… |

## Arquitetura proposta

### 1. `Locale` (novo, `Core/CompanionLocale.cs`)

Uma tabela única, fonte de verdade:

| `GetSelectedLanguage()` | Whisper | Kokoro | Vozes (curadas) | Padrão | UI | Falas |
|---|---|---|---|---|---|---|
| `Portuguese_Brazilian` | `pt` | `pt-br` | 42, 43, 44 | 42 | `Portuguese_Brazilian.json` | idem |
| `English` | `en` | `en-us` | 3, 2, 14, 16 | 3 | (embutido) | (embutido) |
| `Spanish` | `es` | `es` | 28, 29 | 28 | `Spanish.json` | idem |
| `French` | `fr` | `fr` | 30 | 30 | `French.json` | idem |
| qualquer outra | `en` | `en-us` | 3, 2, 14, 16 | 3 | (embutido) | (embutido) |

- Resolvido **uma vez por cena/troca de idioma** (gancho no postfix de `Localization.SetupLanguage`, que já
  existe), nunca no `Update`.
- Italiano/japonês/chinês ficam na tabela como "conhecidos, fora desta leva".

### 2. Config (`ModConfig`)

| Chave | Hoje | Depois |
|---|---|---|
| `[AI] VoiceLanguage` | `"pt"` | **`"auto"`** (padrão) — resolve pelo idioma; código explícito ganha |
| `[AI] TtsLanguage` | `"pt-br"` | **`"auto"`** — idem |
| `[AI] TtsVoice` | `42` | **`0` = automático** (voz padrão do idioma); `42/43/44` continuam explícitos |
| `[AI] TtsModel` | caminho | **inalterado** (um modelo para todos) |
| `[AI] VoiceInitialPrompt` | lista em PT | **vazio = automático** (vocabulário do idioma); texto digitado ganha |
| `[AI] TtsDefaultGender` (novo?) | — | **não criar** nesta leva (a "voz padrão" já resolve) |

Sem migração: `.cfg` existente mantém `pt`/`pt-br`/`42` (explícitos) e continua igual; só o arquivo **novo**
nasce em automático. O F8 mostra o efetivo ao lado.

### 3. Voz e painéis

- `CompanionTts.PortugueseVoices` → `Locale.Voices` (por idioma) + `Locale.DefaultVoice`.
- `CompanionProfile.VoiceIds/Labels`: lista do **idioma resolvido** + "seguir o padrão"; valor fora da lista
  mostra `hc_ai_voice_other` (chave já existe).
- **F8**: `TtsVoice` deixa de ser slider numérico e vira o **dropdown de nomes** (mesmo widget do painel `I`),
  com a opção "Automático (pf_dora — pt-BR)".
- **Painel `I`**: a linha de voz mostra `Automático (pt-BR · pf_dora)`; o global e o do companheiro seguem a
  mesma lista.

### 4. Conteúdo por idioma (gerado)

| Arquivo | Conteúdo |
|---|---|
| `Translations/Spanish.json`, `Translations/French.json` | ~400 chaves (UI + `hc_cfg_*` + `hc_cat_*` + `hc_ai_voice_*`) |
| `Speech/Spanish.json`, `Speech/French.json` | 15 categorias / ~186 linhas |
| `VoiceInitialPrompt` (no `ModConfig`) | vocabulário de ordens em ES/FR (`colecta 10 maderas`, `récolte 10 bois`…) |
| `CompanionPersona` | texto dos 5 presets em EN/ES/FR |

O inglês **continua sem arquivo** (as chaves são o texto) — inclusive as falas (o `Defaults()` já é EN).

## Fases (cada uma com teste em jogo)

| # | Entrega | Arquivos | Critério de aceite |
|---|---|---|---|
| **M1** | `Locale` + resolução + config `auto` + log do que resolveu; **corrige** o bug do `_loadedLanguage` (o idioma não fica mais "preso" se o arquivo falhar) | `Core/CompanionLocale.cs` (novo), `ModConfig`, `CompanionTts`, `WhisperStt`, `SpeechConfig` | com o jogo em PT/EN/ES/FR: o log diz `TTS: pt-br, voz 42 (pf_dora)` / `en-us, voz 3`…, a fala sai na voz certa e o STT usa o código certo. `.cfg` com valor explícito **não muda** |
| **M2** | UI: dropdown no F8 + "Automático" no painel `I`; lista por idioma no companheiro | `UI/ConfigPanel`, `UI/CompanionAiPanel`, `AI/CompanionProfile` | F8 mostra nomes (`pf_dora (feminina)`) e "Automático (…)" em vez do número; trocar a voz pelo painel vale na próxima fala |
| **M3** | Traduções ES/FR (UI + falas + vocabulário do STT + presets) | `Translations/`, `Speech/`, `ModLocalization`, `CompanionPersona`, `ModConfig` | jogo em ES e FR: F8, HUD, balões e falas no idioma; ordem por voz em ES/FR entendida |
| **M4** | Polimento: rótulos do HUD que hoje ficam presos no idioma antigo (achado #1 da auditoria), docs e wiki | `UI/CompanionHudPanel`, `docs/wiki/*`, `CHANGELOG` | trocar o idioma sem reiniciar atualiza HUD, F8, painel `I` e falas |
| **M5** | (depois) IT/JA/ZH + revisão dos textos gerados | — | chinês só com os `lexicon-zh.txt`/`*-zh.fst` que **não** embarcamos hoje |

Estimativa: M1 ~1 dia, M2 ~1 dia, M3 ~2–3 dias (geração + revisão), M4 ~½ dia.

## Riscos e mitigação

- **Voz de outro idioma no companheiro**: respeitada + aviso no log (não resetar escolha do jogador).
- **Cache do TTS**: a chave já é `voz+velocidade+frase`; trocar de idioma muda a voz ⇒ cache novo, sem vazamento
  (o teto de 32 MB continua).
- **Arquivo de falas ausente/corrompido**: M1 corrige o `_loadedLanguage` para tentar de novo (com portão de
  tempo) em vez de ficar em inglês até reiniciar.
- **`auto` surpreendendo**: o efetivo aparece no F8/painel e todo valor explícito é intocável.
- **Tradução gerada**: revisão do jogador antes de publicar; nenhuma release sem o "ok" dele.

## Como testar (receita por idioma)

1. Fechar o jogo, trocar o idioma do Valheim, entrar no mundo.
2. `LogOutput.log`: `[TTS] Modelo CARREGADO ... idioma "xx-yy"` e a linha nova `[Locale] <idioma do jogo> -> <whisper>/<kokoro>/voz N`.
3. Painel `I` → **Ouvir** (voz do companheiro) e o **F8** (opções com os nomes).
4. Chat: `colete 10 madeiras` (PT) / `gather 10 wood` (EN) / `recoge 10 maderas` (ES) / `récolte 10 bois` (FR).
5. Segurar `V` e falar a mesma ordem (STT no idioma).
6. Sem reiniciar: trocar o idioma do jogo e conferir HUD/painéis/falas (M4).

## Fora de escopo

- Wiki em outros idiomas (fica PT; o mod já fala o idioma do jogo).
- Traduzir os ~1.500 logs/diagnósticos (são técnicos, em PT por escolha).
- Voz para os idiomas sem Kokoro (de/ru/pl/tr/ko/sv/uk) — usam `en-us` por decisão.
