# Fase 3.4 — o companheiro fala: TTS local (Kokoro-82M via sherpa-onnx)

**Status:** build **0 erros** (0 avisos); `Companions.dll`, `sherpa-onnx.dll` (gerenciada) e as duas nativas
(`onnxruntime.dll`, `sherpa-onnx-c-api.dll`) na pasta do plugin; o modelo do Kokoro já estava lá.
**Teste em jogo pendente** (receita no fim). O spike que validou a API/RTF está resumido em §1 e §6.

Arquivos: `src/Companions/AI/CompanionTts.cs` (**novo**), `src/Companions/AI/CompanionChatAI.cs`
(`PickSpokenText`/`SpeakOutLoud` + prompt de 1 frase), `src/Companions/Core/ModConfig.cs` (10 configs `[IA]`),
`src/Companions/Core/CompanionsPlugin.cs` (`Init`/`Tick`/`Shutdown`),
`src/Companions/Core/ModLocalization.cs` + `Translations/Portuguese_Brazilian.json` (1 chave nova),
`src/Companions/Companions.csproj` (`PackageReference` + target `DeploySherpaOnnx`).

---

## 1. O que foi validado antes de escrever o mod

Um harness de console **net472** (fora do jogo, mesmo alvo do mod) exercitou a API real do pacote com o modelo
real, e foi ele que decidiu os detalhes abaixo:

| Item | Resultado medido |
|---|---|
| Pacote | `org.k2fsa.sherpa.onnx` **1.13.8** (`lib\netstandard2.0\sherpa-onnx.dll`, 50 KB) + `...runtime.win-x64` 1.13.8 |
| Referências do gerenciado | **só `netstandard, Version=2.0.0.0`** (nada de System.Text.Json/Extensions) |
| Licença | **Apache-2.0** (uso livre) |
| Carga do modelo | **997 ms** |
| `NumSpeakers` / `SampleRate` | **54** / **24000 Hz mono** |
| RTF com `num_threads=4` | **0,73 a 0,91** (frases de 1,3 a 4,6 s) — gera **mais rápido que o tempo real** |
| `Lang=""` | a lib nativa **RECUSA** ("You are using a multi-lingual Kokoro model ... please pass `--kokoro-lexicon` or `--kokoro-lang`") |
| `Lang="pt-br"` | carrega e fala em português do Brasil |
| Vozes pt-BR | **42 = pf_dora** (feminina), **43 = pm_alex** (masculina), **44 = pm_santa** (masculina); 45 soa chinês |
| `OfflineTtsGeneratedAudio` | tem `Dispose()` mas **NÃO implementa `IDisposable`** → `using` **não compila** (CS1674); só `try/finally` + `Dispose()` |

## 2. O caminho completo

```
CompanionChatAI.HandleResult (main thread)
      │  PickSpokenText(TtsFalarFalasDoMod, TtsFalarRespostas, linhaDoMod, reply)
      ▼
CompanionTts.Speak(companheiro, texto)            <- só enfileira e volta
      │  ConcurrentQueue<TtsRequest> + SemaphoreSlim.Release()
      ▼
thread de fundo "ModIA-CompanionTts" (uma só, em ordem)
      │  SplitSentences(texto) -> 1..n frases
      │  por frase: cache?  senão  OfflineTts.Generate(frase, velocidade, voz)
      ▼
ConcurrentQueue<TtsChunk>  (amostras float 24 kHz mono + quem fala)
      ▼  drenada no Update (CompanionTts.Tick -> Playback)
AudioClip.Create + SetData + AudioSource.Play  -> som 3D na posição do companheiro
```

Nada de ordem/job/STT/UI foi duplicado: a Fase 3.4 só **acrescenta uma saída de voz** nos mesmos pontos em que o
`CompanionChatAI` já mostrava o texto.

## 3. Carregamento das bibliotecas nativas (o ponto delicado)

O binding gerenciado é literalmente:

```csharp
internal static class Dll { public const string Filename = "sherpa-onnx-c-api"; }
[DllImport(Dll.Filename, CallingConvention = CallingConvention.Cdecl)]
private static extern IntPtr SherpaOnnxCreateOfflineTts(ref OfflineTtsConfig config);
```

Ou seja: **não há loader próprio** (diferente do Whisper.net, que resolve o caminho por conta própria). Quem
decide onde o Windows procura é a **ordem de busca do processo**, e no Unity/Mono o `AppContext.BaseDirectory` é
`valheim_Data\Managed` — **não** a pasta do plugin. Um nome puro (`sherpa-onnx-c-api`) não seria achado ao lado do
`Companions.dll`.

`CompanionTts.EnsureNativeLibraries()` roda **antes do primeiro uso** e faz, nesta ordem:

1. `SetDllDirectory("<plugin>\runtimes\win-x64\native")` — a chamada Win32 que põe uma pasta na ordem de busca
   (a receita sugerida);
2. `LoadLibraryEx(caminhoAbsoluto, NULL, LOAD_WITH_ALTERED_SEARCH_PATH)` para **`onnxruntime.dll`** (a dependência)
   e depois para **`sherpa-onnx-c-api.dll`** — o pré-carregamento explícito por caminho absoluto. A flag faz o
   Windows procurar as dependências **na pasta da própria DLL**, então `onnxruntime.dll` é achado mesmo sem a
   mudança de ordem de busca;
3. `SetDllDirectory(NULL)` **logo depois**, restaurando a ordem de busca padrão. O pré-carregamento já deixou os
   módulos no processo, e uma vez carregado um módulo o `DllImport("sherpa-onnx-c-api")` resolve para **o módulo já
   carregado** (a busca por nome, com `LoadLibrary`, casa com o módulo existente) — sem nenhuma busca em disco.
   **Nada fica alterado** para o jogo nem para outros mods.

O `handle` de cada `LoadLibraryEx` é conferido (0 = erro com o código Win32 no log) e o caminho real do módulo é
lido com `GetModuleFileName` — o log mostra exatamente **qual** DLL foi carregada, de onde:

```
[TTS] Carregando as bibliotecas nativas do sherpa-onnx de "...\runtimes\win-x64\native": sherpa-onnx-c-api.dll + onnxruntime.dll.
[TTS] Nativa carregada: "...\onnxruntime.dll" -> "...\onnxruntime.dll" (handle 0x7FFF95D60000)
[TTS] Nativa carregada: "...\sherpa-onnx-c-api.dll" -> "...\sherpa-onnx-c-api.dll" (handle 0x7FFF9B1F0000)
```

A pasta procurada é `runtimes\win-x64\native\` (layout do pacote, igual ao do Whisper) e a pasta do plugin é
aceita como reserva. Se as DLLs não estiverem em nenhum dos dois lugares, o erro é claro e o companheiro
simplesmente não fala (o jogo segue).

> **Verificado fora do jogo:** o harness carregou as duas nativas exatamente por esse caminho (passo 1–3) e
> sintetizou áudio de verdade, com os caminhos absolutos impressos acima.

## 4. `CompanionTts` (o serviço)

| Peça | Decisão |
|---|---|
| Modelo | carregado **uma vez** (`TryGetTts`, `lock _loadLock`) e **mantido**; `Prewarm()` carrega em segundo plano quando você entra no mundo (a 1ª fala não paga os ~1 s) |
| Thread | **dedicada** (`new Thread`, `IsBackground = true`, nome `ModIA-CompanionTts`), não uma `Task` de pool: é trabalho longo e permanente, e o pool é do jogo |
| Fila de entrada | `ConcurrentQueue<TtsRequest>` + `SemaphoreSlim` (o worker espera com timeout de 500 ms para poder sair no shutdown); **teto de 3 pedidos** (o excedente é descartado e dito no log) |
| Fila de saída | `ConcurrentQueue<TtsChunk>` drenada no `Update`; cada `TtsChunk` leva amostras + taxa + quem fala |
| Voz fora de alcance | `EffectiveVoice`: `[IA] TtsVoz` mas **nunca fora** de `NumSpeakers` (um id inexistente faria a chamada nativa falhar) |
| Watchdog | se uma síntese passar de 30 s, o log avisa **uma vez** (o jogo não está travado: é thread de fundo) |
| Shutdown | marca `_stopping`, acorda o worker, para o áudio, destrói o `AudioSource` e **espera a thread** (`_worker.Join(5000)`) **antes** de liberar o nativo (`lock (NativeGate)`); se o `Join` estourar (um `Generate` que não volta), o modelo fica carregado. `Process`/`TryGetTts`/`Speak` recusam trabalhar depois de `_stopping` (nada de recarregar o modelo depois do shutdown, nem de `Generate` sobre handle morto) |

**Nunca bloqueia a main thread:** `Speak` só lê a config, marca o slot e enfileira; `Generate` é síncrono mas já
está na thread de fundo. O `Update` só faz um `TryDequeue` (nada alocado) e, quando há fala tocando, **um**
`transform.position`.

## 5. Por frase, com cache (por que a resposta começa rápido)

O texto é cortado em **frases** (`SplitSentences`) e cada frase é sintetizada **e publicada** na hora; o
`Playback` toca a primeira enquanto o worker gera a segunda. Sem isso, uma resposta de 3 frases só começaria a
soar depois de ~3 s de síntese.

Regras do corte (todas verificadas num harness com o código real, **17 casos, ALL CHECKS PASSED**):

- cortes em `.` `!` `?` `…` e quebras de linha;
- **uma sequência de terminadores é um corte só**: `"Talvez... nao sei."` → `["Talvez...", "nao sei."]`
  (e **não** `"Talvez."`, `"."`, `"."`, `"nao sei."`);
- **ponto entre dígitos não é fim de frase**: `"Ele tem 1.5 de altura. Depois falamos."` →
  `["Ele tem 1.5 de altura.", "Depois falamos."]`;
- `:` e `,` **não** cortam (`"Ok: coletar madeira (10)"` é **uma** frase — a linha do mod sai inteira, sem pausa
  artificial no meio);
- uma frase ainda maior que 180 caracteres é quebrada na última vírgula/espaço antes do limite (o modelo tagarela
  não faz o jogador esperar a frase toda).

O **cache** (`[IA] TtsCache`, ligado) guarda `voz + velocidade + frase → amostras`. É o que faz uma fala fixa
(`"Já temos o suficiente."`) sair **na hora** — e como o cache é por **frase**, uma fala que mistura uma parte
repetida com uma nova aproveita a parte repetida.

Dois limites, depois da auditoria (ver §12):

- **só as linhas do mod entram**: `TtsRequest.Cacheable` é `false` para o `reply` do modelo
  (`CompanionChatAI.SpeakOutLoud` marca), porque uma resposta do modelo é **nova a cada vez** — deixá-las entrar era
  o que **expulsava as falas fixas** (o motivo de o cache existir) a cada mensagem;
- **teto por bytes**, não por entrada: `MaxCacheBytes` = **32 MB** de amostras `float` (~29 s de 24 kHz mono; uma
  frase de 180 caracteres ≈ 1,1 MB) com **eviction do mais antigo** (`_cacheOrder`, uma fila de inserção) e
  `MaxCacheEntries` = 256 como teto duro para frases muito curtas. Antes o teto era de 256 **entradas** e o cache
  era **esvaziado** inteiro ao encher — com frases longas isso permitia centenas de MB no heap do Mono.

## 6. Latência (medida, não adivinhada)

No harness (Ryzen 7 5700X, `num_threads=4`), com o modelo já carregado:

| Frase | Síntese | Áudio | RTF |
|---|---|---|---|
| "Deixa comigo." | 1210 ms | 1,33 s | 0,91 |
| "Vou buscar madeira, chefe. Já volto com o que precisar." | 2915 ms | 3,61 s | 0,81 |
| "Atacando o troll!" | 1036 ms | 1,43 s | 0,73 |
| "Bom dia! Tudo bem com você hoje? Eu estou pronto para trabalhar." | 3901 ms | 4,63 s | 0,84 |
| (2ª vez, mesma instância, cache desligado) "Deixa comigo." | 961 ms | 1,33 s | 0,73 |

RTF **< 1** = a síntese é mais rápida que a reprodução, então o companheiro nunca fica devendo áudio. `[IA]
TtsThreads = 4` é o melhor nesta máquina (mais threads competem com o jogo). O log de cada frase traz o RTF:

```
[TTS] Frase 1/2: 1210 ms, RTF 0.91 (mais rapido que o tempo real) para 1.33 s de audio (24000 Hz mono, 31800 amostras, 124 KB) | "Deixa comigo."
[TTS] Frase 2/2: CACHE (0 ms) para 0.90 s de audio (...) | "Ja volto."
[TTS] Fala #3 pronta: 2110 ms no total, 2.23 s de audio (53520 amostras), RTF 0.95 | "Deixa comigo. Ja volto."
```

## 7. Playback (3D, uma por vez)

- **um** `AudioSource` num `GameObject` próprio (`ModIA-CompanionTts`, `DontDestroyOnLoad`), criado no primeiro
  uso e mantido;
- **3D** (`spatialBlend = 1`, `minDistance = 1.5`, `maxDistance = [Speech] CullDistance`), posicionado **na
  posição do companheiro** (offset `+1.6 m`) a cada frame enquanto ele fala — a voz sai **dele**, não do centro da
  tela, e acompanha se ele andar;
- `loop = false`, `playOnAwake = false`, volume de `[IA] TtsVolume`;
- **uma fala por vez** (fila): a próxima frase começa no mesmo frame em que a anterior termina (sem buraco entre
  frases); o `AudioClip` é destruído assim que acaba (um `AudioClip` que nunca é destruído é vazamento);
- o relógio é `Time.unscaledTime` (um menu aberto pausa o `Time.time` mas **não** o `AudioSource`), então a fila
  continua andando junto com o som que está realmente tocando;
- fala cujo companheiro morreu/sumiu é descartada (checagem no dequeue, na main thread).

## 8. Por que `AudioClip`/`AudioSource` por reflexão

O mesmo motivo do `VoiceInput.Mic`: o projeto compila para **net472** e agora referencia **dois** pacotes
`netstandard2.0` (Whisper.net e sherpa-onnx), o que fixa a fachada **netstandard 2.0**. Todo módulo do Unity 6 é
construído contra **netstandard 2.1** e `UnityEngine.AudioModule` **não está** na closure de referências do jogo
(`CoreModule`/`AIModule`/... estão, via `assembly_valheim`) → referenciá-lo dá **CS1705**. Apontar o `netstandard`
para a fachada 2.1 do jogo resolve o CS1705 e quebra do outro lado (`AudioClip.GetData(Span<float>, int)` →
**CS7069**, os *reference assemblies* do .NET Framework não têm `Span<T>`).

`CompanionTts.Audio` resolve **11 membros** por reflexão, **uma vez** (3 tentativas, porque o `Awake` do plugin
pode rodar antes do módulo de áudio carregar):

```
AudioClip:   Create(string,int,int,int,bool) [estático], SetData(float[],int)
AudioSource: clip, volume, spatialBlend, minDistance, maxDistance, loop, playOnAwake, Play(), Stop()
```

As chamadas acontecem **uma vez por frase** (nunca por frame). O único trabalho por frame é o
`transform.position` do `AudioSource`, que vai por `Component.transform` (CoreModule, referência direta) — sem
reflexão e sem alocação.

## 9. Config (`[IA]`, PT-BR no `.cfg`)

| Chave | Default | Para que serve |
|---|---|---|
| `TtsLigado` | `true` | liga/desliga a fala (com ela desligada o modelo **nem é carregado**) |
| `TtsVoz` | `42` | voz do Kokoro: **42** pf_dora (fem.), **43** pm_alex (masc.), **44** pm_santa (masc.) |
| `TtsVelocidade` | `1` | velocidade da fala (0,5–2 — **o slider do painel da IA usa o mesmo intervalo**, `CompanionTts.MinSpeed/MaxSpeed`) |
| `TtsThreads` | `4` | núcleos da síntese (1–16; 4 = RTF ~0,83 medido) |
| `TtsVolume` | `1` | volume (0 = mudo) |
| `TtsModelo` | `tts\kokoro\model.int8.onnx` | modelo; `voices.bin`/`tokens.txt`/`espeak-ng-data` vêm da **mesma pasta** |
| `TtsIdioma` | `pt-br` | idioma (**nunca vazio**; `pt` sozinho = português de Portugal) |
| `TtsCache` | `true` | cache das falas (por frase) |
| `TtsFalarRespostas` | `true` | falar a resposta do modelo (`reply`) |
| `TtsFalarFalasDoMod` | `true` | falar a linha do mod que aparece no chat (numa **ordem** ela ganha) |

**Interruptores de teste do eco (parte 3, todos com o default = como está hoje):** `EcoDiag` (`false`), `TtsReverbZoneMix`
(`1`), `TtsBypassReverbZones` (`false`), `TtsBypassListenerEffects` (`false`), `TtsBypassEffects` (`false`) e
`TtsDoppler` (`true`). Ver a seção 13.

## 10. Receita de teste in-game

1. **Preparo:** o modelo já está em `<plugin>\tts\kokoro\` (`model.int8.onnx` 109 MB, `voices.bin` 27 MB,
   `tokens.txt`, `espeak-ng-data\`); as nativas em `<plugin>\runtimes\win-x64\native\` (o build copia). LM Studio
   no ar para o LLM.
2. **Entrar no mundo** e conferir no `BepInEx\LogOutput.log`:
   ```
   [TTS] Fala do companheiro (Kokoro-82M via sherpa-onnx, CPU, local): LIGADO | modelo "...\tts\kokoro\model.int8.onnx" (111527 KB) | voz 42 (pf_dora, feminina) | idioma "pt-br" | 4 thread(s) | ...
   [TTS] Audio do Unity acessivel por reflexao (UnityEngine.AudioModule ...): AudioClip.{Create,SetData} + AudioSource.{clip,volume,...}
   [TTS] Nativa carregada: "...\onnxruntime.dll" -> ... (handle 0x...)
   [TTS] Nativa carregada: "...\sherpa-onnx-c-api.dll" -> ... (handle 0x...)
   [TTS] Modelo CARREGADO em ~1000 ms e MANTIDO carregado: "..." (sherpa-onnx 1.13.8.0, Kokoro-82M, CPU, 4 thread(s), idioma "pt-br") | 54 vozes | saida 24000 Hz mono.
   [TTS] Modelo pre-carregado em segundo plano (a primeira fala nao vai esperar por ele).
   ```
3. **Teste direto (sem LLM nem microfone):** abra o console (`F5`) e digite
   `modia_tts_teste Bom dia, chefe! Tudo pronto por aqui.` → o companheiro mais próximo (ou o jogador) **fala**, e
   o log mostra uma linha `[TTS] Frase i/n: ... RTF ...` por frase. No `[TTS] Tocando "..."` confirma a reprodução.
4. **Ordem por chat:** digitar `colete 10 madeiras` → no chat aparece `Pensando...` e depois a linha do mod
   (`Ok: coletar madeira (10)`) — **e o companheiro fala** *"Ok: coletar madeira (10)."* na posição dele.
   Com `TtsFalarFalasDoMod = false` ele fala a resposta do modelo (*"Deixa comigo."*) no lugar.
5. **Ordem por voz:** segurar `V`, falar `ataca o greydwarf`, soltar → transcrição no balão do **jogador** e, depois
   da ação, a fala `"Atacando greydwarf."` saindo **dele**.
6. **Cache:** repetir uma **linha do mod** (ex.: `colete 10 madeiras` duas vezes) → no log,
   `[TTS] Frase 1/1: CACHE (0 ms) ...` (só as linhas do mod entram no cache; a resposta do modelo é nova a cada
   vez e é sempre sintetizada).
7. **Vozes:** trocar `[IA] TtsVoz` para `43` e repetir o passo 3 (masculina).
8. **Volume/velocidade:** `TtsVolume = 0.3` e `TtsVelocidade = 1.3` e repetir.
9. **Casos de falha:** renomear `tts\kokoro\model.int8.onnx` → `[TTS] O modelo do TTS nao esta em ...` (uma vez, no
   log e no chat: *"A voz do companheiro não está pronta (falta o modelo do Kokoro?)."*) e o jogo segue normal;
   **devolver o arquivo com o jogo aberto** → em até **30 s** o log mostra
   `[TTS] Modelo pre-carregado em segundo plano ...` e a fala volta a funcionar **sem reiniciar** (A4);
   `TtsLigado = false` → nada é falado e o modelo não é carregado.
10. **Sem travar:** durante uma fala longa (passo 3 com um texto grande), andar/lutar — o jogo **não** deve
    engasgar (a síntese é thread de fundo).

## 11. O que ainda falta

- **3.7 (UI):** ✅ **feito** — `UI/CompanionAiPanel.cs` (tecla **`I`** / `[IA] PainelTecla`) escolhe a voz, o volume,
  a velocidade, o que é falado, a personalidade, a memória e a tecla do push-to-talk, com o botão **Ouvir** para
  ouvir a voz escolhida na hora. Ver `docs/roadmap.md` (Fase 3.7). Falta o menu radial como atalho para ele.
- Vozes por companheiro (hoje `[IA] TtsVoz` é global) e amostras de referência (o Kokoro não faz clonagem).

## 12. Auditoria (Fase 3.4/3.7) — o que foi endurecido

Revisão dos commits `675a56f`/`758bec0`. Nada de comportamento visível mudou, exceto os limites citados.

| # | Problema | Correção |
|---|---|---|
| A1 | **Corrida no `Shutdown`**: `_stopping` + `Dispose()` sem esperar a thread. O worker só olha o flag no topo do laço, então podia estar **entre duas frases** (`_busy == 0`) e o `Generate` seguinte rodava sobre o handle já liberado → **AccessViolation** (crash nativo, não é exceção capturável) | `_worker.Join(5000)` **antes** de tocar no `_tts`; `Process`/`TryGetTts` recusam sintetizar/carregar com `_stopping`; `Dispose` dentro do `lock (NativeGate)`; `Join` estourado ⇒ o modelo **fica carregado** |
| A2 | `audio.NumSamples` com handle nulo: o binding devolve um `OfflineTtsGeneratedAudio` **não-nulo** com `Handle == IntPtr.Zero` quando o `Generate` nativo devolve `NULL`, e `NumSamples`/`Samples` fazem `Marshal.PtrToStructure(Handle)` → *AccessViolation* | `audio.Handle != IntPtr.Zero` checado **antes** de `NumSamples`/`Samples`; handle nulo = falha no log + `continue`, sem `Dispose` (o destroy nativo receberia `NULL`) — `GC.SuppressFinalize` no lugar |
| A3 | `Speak` depois do `Shutdown` deixava a fala na fila e o `_pending` estourado | `if (_stopping) return;` no começo |
| A4 | `Prewarm` era **single-shot** (`_prewarmRequested`): se o modelo não estivesse no disco naquele instante, nunca mais tentava — e "modelo não encontrado" marcava `_loadFailed` (definitivo) | `MaybePrewarm`: uma tentativa por vez, uma a cada **30 s**, enquanto no mundo; "não encontrado" **não** é mais `_loadFailed` (re-checado, erro logado uma vez) ⇒ copiar a pasta do modelo com o jogo aberto passa a funcionar |
| C2 | Cache sem teto de **bytes** (256 entradas × `float[]` cru ≈ centenas de MB) e cada resposta única do modelo **expulsava as falas fixas** | teto de **32 MB** de áudio com eviction do mais antigo + **só as linhas do mod** entram (`TtsRequest.Cacheable`) |
| B1 | A pré-carga não validava **qual** DLL foi carregada: se outra versão de `onnxruntime.dll`/`sherpa-onnx-c-api.dll` já estivesse no processo, o `DllImport` (resolvido por **nome-base**) podia cair nela | `PreloadNative` compara `ModulePathOf(handle)` com o caminho esperado e **avisa** no log quando diverge |
| D2 | (painel 3.7) a tecla abria o painel por cima do inventário/menu/F8 | a tecla é recusada com `InventoryGui`/`Menu`/`Minimap`/`ConfigPanel` visíveis, e o `ConfigPanel` não abre com o painel da IA visível |
| E1/E2 | `hc_ai_voice_key_conflict` só no JSON PT-BR; slider de velocidade 0,5–1,5 contra 0,5–2,0 na config | par em inglês nos padrões; `MinSpeed`/`MaxSpeed` compartilhados |
| D3 | cursor não restaurado no `Hide()` do painel | **nada a fazer**: `GameCamera.UpdateMouseCapture` (decompilado) trava o cursor a cada frame quando não há janela aberta — e deixa livre de propósito para quem desliga a captura com `Ctrl+F1` |

## 13. O eco da voz (parte 3) — instrumentação, não correção

**Sintoma:** a voz do companheiro sai com eco, "como falar num lugar grande". **Só** ela: música e SFX do jogo são
normais. A síntese está limpa (o WAV gerado fora do jogo é idêntico), o eco é idêntico em qualquer lugar, de perto ou
de longe e com as 3 vozes (42/43/44), e acontece com **uma única frase** — logo não é a fila, o cache, a distância, o
timbre nem o áudio do sistema. É um **processamento fixo da engine** aplicado à nossa `AudioSource`; **qual** deles é
o ouvido do jogador que decide, e é para isso que esta parte existe.

### 13.1 Os seis interruptores (`[IA]`, default = como está hoje)

| Chave | Tipo | Default | Escrito na fonte quando difere do default |
|---|---|---|---|
| `EcoDiag` | bool | `false` | — (só log) |
| `TtsReverbZoneMix` | float 0–1 | `1` | `reverbZoneMix` |
| `TtsBypassReverbZones` | bool | `false` | `bypassReverbZones = true` |
| `TtsBypassListenerEffects` | bool | `false` | `bypassListenerEffects = true` (**suspeito principal**) |
| `TtsBypassEffects` | bool | `false` | `bypassEffects = true` |
| `TtsDoppler` | bool | `true` | `dopplerLevel = 0` |

`ApplyEcoTestSwitches(source)` é chamado **uma vez por frase** (`StartChunk`, antes do `Play`) — a `AudioSource` é
criada uma vez só, então reler a config a cada frase é o que faz um interruptor mexido no painel F8 valer **na próxima
frase**, sem reiniciar. Com tudo no default o método **sai antes do primeiro setter**: nenhum setter, nenhuma linha de
log, som idêntico ao de antes desta parte. Com algum valor diferente sai uma linha por frase
(`[TTS] Modo de audio da frase: reverbZoneMix=0, bypassListenerEffects=true`), que é a prova de que o interruptor
chegou na fonte. Se um membro opcional não existir neste Unity, o interruptor dele fica inerte e a linha diz
`(sem efeito: este Unity nao tem o membro)`.

### 13.2 Diagnóstico (`EcoDiag = true`), uma vez por sessão

Na main thread, no **primeiro** `StartChunk`, prefixo `[ECO]`, tudo em `try/catch` (nada aqui pode derrubar a voz):

| Bloco | O que sai |
|---|---|
| Ouvinte | nome do GameObject do `AudioListener` e se ele tem `AudioReverbFilter` (`reverbPreset`, `dryLevel`, `room`, `decayTime`) e/ou `AudioLowPassFilter` (`cutoffFrequency`) |
| Zonas | **todas** as `AudioReverbZone` da cena: `reverbPreset`, `minDistance`, `maxDistance`, `decayTime`, `room` e **se o jogador e/ou o companheiro estão dentro** do raio |
| Receita | amostra de até **20** `AudioSource` da cena (**a nossa fora**): `spatialBlend`, `reverbZoneMix`, `bypassReverbZones`, `bypassEffects`, `bypassListenerEffects`, `dopplerLevel`, `spatialize` e o **nome do mixer group** |

Busca: `Resources.FindObjectsOfTypeAll(Type)` filtrado por `gameObject.scene.IsValid()` (descarta prefabs/assets que
só existem na memória). É caro de propósito — **uma vez por sessão**, nunca por frame, e **nunca** com a flag
desligada.

### 13.3 API conferida no `UnityEngine.AudioModule.dll` do jogo

`AudioSource`: `reverbZoneMix`, `bypassReverbZones`, `bypassEffects`, `bypassListenerEffects`, `dopplerLevel`,
`spread`, `spatialize` (bool), `outputAudioMixerGroup` (`AudioMixerGroup`) — todos existem, todos resolvidos **uma
vez** por reflexão e todos **opcionais** (o núcleo obrigatório segue sendo os 11 membros de sempre).
`AudioReverbZone`: `reverbPreset` (`AudioReverbPreset`), `minDistance`/`maxDistance`/`decayTime` (**float**),
`room` (**int**). `AudioReverbFilter`: `reverbPreset`, `dryLevel`, `room`, `decayTime`. `AudioLowPassFilter`:
`cutoffFrequency`. `Resources.FindObjectsOfTypeAll(Type)` → `Object[]` (não obsoleta; a `Object.FindObjectsOfType`
equivalente está marcada `[Obsolete]` no Unity 6).

### 13.4 Receita de teste

Um interruptor por vez, uma frase entre cada um, e o log com as linhas `[TTS] Modo de audio da frase` / `[ECO]`:

1. `EcoDiag` ligado → falar uma frase → mandar o log (a foto da cena já pode responder sozinha).
2. `TtsBypassListenerEffects` ligado → o eco sumiu? era o efeito no ouvinte.
3. Senão `TtsReverbZoneMix` = 0 (ou `TtsBypassReverbZones`) → sumiu? era zona de reverb.
4. Senão `TtsDoppler` desligado → sumiu? era Doppler.
5. Senão `TtsBypassEffects` ligado.
6. Deixar **tudo no default** ao terminar (é o som de hoje).

