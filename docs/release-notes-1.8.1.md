# 1.8.1 — LLM padrão agora é o Groq (grátis e rápido)

![AI Companions — companheiros NPC com IA para Valheim, em PT-BR](https://raw.githubusercontent.com/julianu37/AI-Companions/main/docs/images/banner.png)

> **Só muda o padrão do LLM.** Se você já configurou o seu (OpenRouter, LM Studio, Ollama), **nada muda**: o `.cfg` não é
> sobrescrito — o mod só usa o valor novo em instalação nova, ou quando você clicar em **Aplicar** no painel.

## O que mudou

### Groq como padrão

- `[IA] BaseUrl` agora nasce **`https://api.groq.com/openai/v1`**
- `[IA] Modelo` agora nasce **`openai/gpt-oss-20b`**
- `[IA] ApiKey` continua obrigatória — a **chave gratuita** sai em
  [console.groq.com/keys](https://console.groq.com/keys)

O motivo é direto: o tier gratuito do **OpenRouter** (`openrouter/free`) é **lento e imprevisível** para uma ordem de jogo,
e o `openai/gpt-oss-20b` do Groq responde em fração de segundo.

### Ordem de preferência

| # | Caminho | Como fica |
|---|---|---|
| 1 | **Groq** (padrão) | grátis, rápido, precisa da chave e de internet |
| 2 | **LM Studio / Ollama** (opcional) | local, nada sai da sua máquina — `http://127.0.0.1:1234/v1` + `qwen2.5-3b-instruct` |
| 3 | **OpenRouter** | continua funcionando e documentado, mas o gratuito é mais lento |

Trocar é só editar `BaseUrl`/`Modelo`/`ApiKey` no `.cfg` ou na seção **Configurações do LLM** do painel <kbd>I</kbd>
(com o botão **Testar conexão**).

### O que continua igual

- **A saída estruturada** (JSON schema em modo *strict*) — o `openai/gpt-oss-20b` suporta, então a resposta continua sendo
  JSON válido e nenhuma ordem se perde.
- A **voz** (reconhecimento e fala) continua **100% local** em qualquer um dos três caminhos.
- Tudo o que veio na **1.8.0**: até 10 companheiros com aba própria, editor de aparência com preview 3D, e o **peso com
  bônus de equipamento** (Megingjord soma +150, com o cinto na mochila, sem empilhar).

## Limites do tier gratuito do Groq (para não ter surpresa)

| Limite | Valor |
|---|---|
| Requisições por minuto | **30** |
| Requisições por dia | **1.000** |
| Tokens por minuto (TPM) | **8.000** |
| Tokens por dia (TPD) | **200.000** |

Como cada ordem gasta ~**1.800 tokens** de prompt, o limite que morde primeiro é o de **tokens por minuto**:
dá cerca de **4 ordens por minuto**. Passando disso, o Groq responde **429** e a ordem é recusada com aviso — nada
quebra, nada fica preso. (Fonte: documentação do Groq — `console.groq.com/docs/rate-limits`.)

## Arquivos

| Arquivo | Tamanho | O que vem |
|---|---|---|
| `AI_Companions-1.8.1.zip` | ~368 MB | O mod **e** os modelos de voz (STT + TTS) — **recomendado** |
| `AI_Companions-1.8.1-sem-modelos.zip` | ~28,5 MB | Só o mod e as traduções (vem `tokens.txt` e `espeak-ng-data\`) |

> **Ao atualizar de uma versão anterior, apague a pasta antiga** `BepInEx\plugins\ModAI-BR-Companions\` antes de
> extrair: duas cópias do mesmo plugin fazem o BepInEx carregar o mesmo mod duas vezes. O `.cfg`
> (`BepInEx\config\Maddhattr.OfflineCompanionsReborn.cfg`) e o save **não** são afetados.

## Requisitos e instalação

- **Valheim 1.0.x** + **BepInExPack_Valheim 5.4.2351** (BepInEx 5).
- **Um LLM**: **Groq** (padrão — só a chave gratuita e internet) **ou** **LM Studio/Ollama** local (opcional) **ou**
  **OpenRouter**.
- **Windows x64** para as duas partes de voz; **microfone** só para as ordens por voz.

Passo a passo completo, **com imagens**: https://github.com/julianu37/AI-Companions/wiki/Instalacao

## Créditos e licença

**Projeto independente** baseado no [Offline Companions Reborn](https://github.com/MaddhattrRocks/Offline-Companions-Reborn)
(**MIT**), de **Maddhattr**, que por sua vez é um fork do *Offline Companions*, de **ProfMags** (**MIT**). A atribuição
completa está em `LICENSE`, `NOTICE.md` e `THIRD_PARTY_NOTICES.md`.

Voz: [Whisper.net](https://github.com/sandrohanea/whisper.net) + modelos do **openai/whisper** (MIT) e
[Kokoro-82M](https://huggingface.co/hexgrad/Kokoro-82M) via [sherpa-onnx](https://github.com/k2-fsa/sherpa-onnx)
(Apache-2.0).
