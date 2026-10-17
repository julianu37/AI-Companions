# Instalação

O passo a passo completo, do zero até o companheiro respondendo a uma ordem.

Antes de começar, confirme que você tem tudo o que está em **[Requisitos](Requisitos)**.

---

## 1. Tenha o Valheim instalado

O jogo precisa estar na versão **1.0.x** (Unity 6). A instalação padrão da Steam fica em:

```
C:\Program Files (x86)\Steam\steamapps\common\Valheim
```

Nos passos seguintes, `<Valheim>` é essa pasta.

---

## 2. Instale o BepInEx

O mod é um plugin BepInEx 5. Instale o **BepInExPack_Valheim 5.4.2351**:

1. Baixe em https://github.com/BepInEx/BepInEx/releases
2. Extraia na pasta `<Valheim>`, de modo que `winhttp.dll` e a pasta `BepInEx\` fiquem na **raiz** do jogo.
3. Rode o Valheim **uma vez** e feche. O BepInEx cria `BepInEx\plugins\`, `BepInEx\config\` e o
   `BepInEx\LogOutput.log`.

Documentação oficial: https://docs.bepinex.dev/

---

## 3. Baixe a release do mod

Vá em **https://github.com/julianu37/ModAI-BR-Companions/releases** e baixe **um** dos dois arquivos:

| Arquivo | Tamanho | Quando escolher |
|---|---|---|
| `ModAI_BR_Companions-1.6.3.zip` | ~367,9 MB | **Recomendado.** Já vem com os modelos de voz (STT + TTS). Instala e usa |
| `ModAI_BR_Companions-1.6.3-sem-modelos.zip` | ~28,4 MB | Internet lenta, ou você já tem os modelos e quer baixá-los separado |

> A diferença é **só** os modelos de voz. O mod e as traduções são os mesmos nos dois.
>
> *(A **1.6.2** foi a release anterior; a **1.6.3** mudou só o empacotamento.)*

---

## 4. Extraia na pasta do Valheim

O zip já vem com a estrutura de pastas pronta: dentro dele a pasta é `BepInEx\plugins\ModAI-BR-Companions\`.
Então basta extrair **na raiz do Valheim** (`<Valheim>`) que cada arquivo cai no lugar certo. Se você usa um
gerenciador de mods, aponte o zip para ele — o gerenciador instala direto, sem renomear nada.

> Se preferir fazer à mão, o resultado final tem que ser
> `<Valheim>\BepInEx\plugins\ModAI-BR-Companions\`, com o `Companions.dll` **dentro** dela. O BepInEx carrega
> plugins de **qualquer** subpasta de `BepInEx\plugins\` — manter esse nome só evita confusão na hora de atualizar.

No fim, a pasta deve ficar assim:

![A pasta do mod no Explorer, com os arquivos e as subpastas](images/pasta-do-mod.png)

```
<Valheim>\
└── BepInEx\
    └── plugins\
        └── ModAI-BR-Companions\
            ├── LICENSES\
            ├── runtimes\
            ├── Speech\                     ← falas do companheiro em PT-BR
            ├── Translations\               ← textos da interface em PT-BR
            ├── tts\                        ← modelo do TTS (Kokoro)
            ├── whisper\                    ← modelos do STT (Whisper)
            ├── Companions.dll              ← o mod
            ├── Microsoft.Bcl.AsyncInterfaces.dll
            ├── sherpa-onnx.dll
            ├── System.Buffers.dll
            ├── System.Memory.dll
            ├── System.Numerics.Vectors.dll
            ├── System.Runtime.CompilerServices.Unsafe.dll
            ├── System.Threading.Tasks.Extensions.dll
            └── Whisper.net.dll
```

> O zip também traz o `README.md`, o `CHANGELOG.md` e o `icon.png` na **raiz** — é o que o gerenciador de mods
> mostra na ficha do mod.

> Essas DLLs de `System.*` e as duas de voz (`Whisper.net.dll`, `sherpa-onnx.dll`) precisam ficar **junto do
> `Companions.dll`** — elas são as dependências que o mod usa para o STT e o TTS.

---

## 5. Modelos de voz

**Se você baixou o pacote completo, este passo está pronto** — os modelos já vieram nas pastas `whisper\` e `tts\`.

Se você baixou o **sem-modelos**, coloque os arquivos assim:

| Onde colocar | Arquivos |
|---|---|
| `...\ModAI-BR-Companions\whisper\` | `ggml-small-q5_1.bin` (181 MB) — o padrão do reconhecimento de voz |
| `...\ModAI-BR-Companions\tts\kokoro\` | `model.int8.onnx` (109 MB), `voices.bin` (27 MB), `tokens.txt` e a pasta `espeak-ng-data\` |

> No pacote **sem-modelos** o `tokens.txt` e a pasta `espeak-ng-data\` **já vêm** — faltam só o
> `ggml-small-q5_1.bin` (Whisper) e o `model.int8.onnx` + `voices.bin` (Kokoro).

Onde baixar:

- **Whisper (ggml):** https://huggingface.co/ggerganov/whisper.cpp/tree/main — pegue o `ggml-small-q5_1.bin`.
  O `ggml-base-q5_1.bin` (57 MB) é mais rápido e menos preciso; veja **[Voz](Voz)**.
- **Kokoro (sherpa-onnx):** releases do https://github.com/k2-fsa/sherpa-onnx — o pacote do modelo
  `kokoro-int8-multi-lang` (que traz o `model.int8.onnx`, o `voices.bin`, o `tokens.txt` e o `espeak-ng-data`).

> Detalhes de qualidade, velocidade e alternativas estão em **[Voz](Voz)**.

---

## 6. Configure o LLM (LM Studio)

As ordens são entendidas por um **LLM local**. O caminho mais simples é o **LM Studio** com o modelo
`qwen2.5-3b-instruct` e o servidor local ligado.

O guia completo está em **[LLM no LM Studio](LLM-no-LM-Studio)**. Em resumo:

1. Instale o LM Studio: https://lmstudio.ai
2. Baixe e carregue o `qwen2.5-3b-instruct` (Q4_K_M).
3. Ligue o **Local Server** (porta **1234**).
4. Confira no `.cfg` (`[IA]`): `BaseUrl = http://127.0.0.1:1234/v1` e `Modelo = qwen2.5-3b-instruct`.

![O LM Studio com o Local Server ligado e o modelo carregado](images/lmstudio-servidor.png)

---

## 7. Primeira execução e o log

1. Rode o Valheim.
2. Abra `<Valheim>\BepInEx\LogOutput.log`.
3. Procure estas duas linhas:

```
ModAI-BR-Companions v1.6.3 loading...
ModAI-BR-Companions loaded successfully! (N methods patched)
```

Se elas aparecerem, o mod carregou.

> O `v1.6.3` é a **versão que você instalou** (o número do zip que você baixou) e o `N` é quantos métodos do jogo
> o mod ajustou — esse número pode mudar de uma versão para outra.

> **É normal** ver um erro de **PlayFab / NullReferenceException** no boot — ele **não** é do mod e não impede
> nada. Se quiser confirmar, veja **[Problemas comuns](Problemas-comuns)**.

---

## 8. Primeiro teste em jogo

Com o mundo aberto e o companheiro ao lado:

1. **Por texto:** abra o chat do jogo e digite `colete 10 madeiras`.
   Ele deve responder e sair para coletar. *(O número é um alvo contado no inventário dele — se ele já tiver 10
   madeiras, avisa "Já temos o suficiente.")*
2. **Por voz:** segure **`V`**, fale `pega 10 pedras` e solte. O texto reconhecido aparece no chat.
3. **A voz dele:** ele deve **falar** a resposta em voz alta, na posição dele no mundo.
4. **Painel da IA:** aperte **`I`** para ajustar personalidade, voz, volume e a tecla do push-to-talk.

Para ver todas as teclas e ordens, vá em **[Comandos](Comandos)**.

---

## Checklist

- [ ] Valheim 1.0.x instalado
- [ ] BepInEx 5.4.2351 extraído na raiz do jogo
- [ ] Release **1.6.3** baixada (o pacote completo ou o sem-modelos)
- [ ] Pasta `BepInEx\plugins\ModAI-BR-Companions\` criada com o `Companions.dll` e as dependências
- [ ] Modelos de voz em `whisper\` e `tts\kokoro\`
- [ ] LM Studio com o modelo carregado e o Local Server ligado na porta 1234
- [ ] `[IA] BaseUrl` e `[IA] Modelo` conferidos no `.cfg`
- [ ] Log com `ModAI-BR-Companions loaded successfully!`
- [ ] `colete 10 madeiras` no chat funciona
- [ ] Segurar `V` e falar funciona

Se algum item falhar, veja **[Problemas comuns](Problemas-comuns)**.
