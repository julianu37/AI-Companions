# Spike — STT local: Whisper.net in-process (Valheim 1.0 / Unity 6 / net472)

**Status:** implementado, build 0 erros, **validado em jogo** (`modia_stt_teste` / `[IA] SttTeste = true`).
**Decisão:** **(A) Whisper.net in-process** — sem crash do jogo, sem conflito de assembly, 1,4 s para transcrever
11 s de áudio. Desde a **Fase 3.3** o spike **compartilha o modelo** com o pipeline de voz
(`AI/WhisperStt.cs`, ver `stt-voz-pipeline.md`): o `WhisperFactory` é carregado uma vez e **mantido carregado**, e
as duas coisas passam pelo mesmo `NativeGate` — o modelo **não** é carregado duas vezes.

Arquivos: `src/Companions/Diagnostics/WhisperSpike.cs`, `src/Companions/Companions.csproj`
(`PackageReference` + target `DeployWhisperNet`), `src/Companions/Core/ModConfig.cs` (3 configs `[IA]`),
`src/Companions/Core/CompanionsPlugin.cs` (`WhisperSpike.Init`/`Tick`).

---

## 1. Pacotes e por que a árvore é "podada"

| Pacote | Versão | Observação |
|---|---|---|
| `Whisper.net` | 1.9.1 | No **net472** o NuGet resolve o alvo **`netstandard2.0`** (que usa `DllImport` + `LoadLibrary` no Windows). |
| `Whisper.net.Runtime` | 1.9.1 | Só traz `build/Whisper.net.Runtime.targets` + os nativos; os `win-x64` são copiados para `bin\...\runtimes\win-x64\`. |
| `Microsoft.Extensions.AI.Abstractions` | 10.2.0 | **`ExcludeAssets="all"`** — ver abaixo. |
| `System.Text.Json` | 10.0.2 | **`ExcludeAssets="all"`** — ver abaixo. |

O `Whisper.net` 1.9.1 arrasta `Microsoft.Extensions.AI.Abstractions` **apenas** por causa do
`WhisperSpeechToTextClient` (a ponte com `Microsoft.Extensions.AI`), que **não usamos**. Esse pacote exige
`System.Text.Json` 10.0.2, que por sua vez arrasta `System.Text.Encodings.Web`, `System.IO.Pipelines`, etc. —
peso e risco de conflito desnecessários dentro do Mono do Unity. Os assets dos dois ficam de fora
(`ExcludeAssets="all"`) e o deploy copia uma **lista fechada** de DLLs.

> Se algum dia aparecer um `TypeLoadException` citando `Microsoft.Extensions.AI`, a correção é remover o
> `ExcludeAssets` e passar a copiar também `Microsoft.Extensions.AI.Abstractions.dll` + `System.Text.Json.dll`
> (+ `System.Text.Encodings.Web.dll` + `System.IO.Pipelines.dll`).

## 2. Como o loader acha os nativos (fonte: 1.9.1, `NativeLibraryLoader.GetRuntimePaths`)

```csharp
var assemblySearchPaths = new[] {
    GetSafeDirectoryName(RuntimeOptions.LibraryPath),   // Path.GetDirectoryName(LibraryPath)
    AppDomain.CurrentDomain.RelativeSearchPath,
    AppDomain.CurrentDomain.BaseDirectory,              // no Unity = pasta do JOGO
    GetSafeDirectoryName(assemblyLocation),             // pasta do Whisper.net.dll
    GetSafeDirectoryName(Environment.GetCommandLineArgs().FirstOrDefault()),
}.Where(...).Distinct();

// para cada pasta: <pasta>\runtimes\win-x64\{whisper.dll, ggml-base-whisper.dll, ggml-cpu-whisper.dll, ggml-whisper.dll}
```

Três consequências importantes:

1. `RuntimeOptions.LibraryPath` **não é um diretório**: o código faz `Path.GetDirectoryName` nele. Passar
   `...\runtimes\win-x64\whisper.dll` faria o loader procurar `...\runtimes\win-x64\runtimes\win-x64`.
2. `AppContext.BaseDirectory` no Unity é a pasta **do jogo**, não a do plugin — por isso não basta deixar o
   default.
3. `WhisperFactory.FromPath(path, libraryPath: ...)` **não existe** na 1.9.1 (o `FromPath` só tem `options`).

**Solução adotada:** `RuntimeOptions.LibraryPath = <pasta do plugin>\Whisper.net.dll` (definido **antes** de
qualquer uso do `WhisperFactory`, como a doc do `RuntimeOptions` exige). Como o loader usa a *pasta* desse
caminho, a primeira pasta sondada passa a ser a do plugin →
`<pasta do plugin>\runtimes\win-x64`, que é exatamente onde o `DeployWhisperNet` deixa os nativos.

Também travamos `RuntimeOptions.RuntimeLibraryOrder = [Cpu]`: só existe `runtimes\win-x64` (pacote CPU), então
não faz sentido sondar CUDA/CUDA12/Vulkan/CoreML/OpenVINO antes (cada sonda mexe com driver de GPU). Em
`netstandard2.0` a checagem de AVX é compilada fora (`#if !NETSTANDARD`), então o runtime CPU é aceito direto.

### Os nativos em si

`LoadLibrary(<caminho completo>\ggml-base-whisper.dll)` → `ggml-cpu-whisper.dll` → `ggml-whisper.dll` →
`whisper.dll`, nessa ordem (dependências antes). Depois o P/Invoke de verdade é
`[DllImport("whisper")]` (`DllImportsNativeWhisper`, o caminho do `netstandard2.0`): o Windows resolve pelo
nome-base do módulo já carregado — é justamente por isso que o pré-carregamento das `ggml-*.dll` importa.
Requer **VC++ Redist 2019+** (nesta máquina: 2022 v14.51 ✓).

## 3. Árvore de dependências gerenciadas (o que vai para a pasta do plugin)

`Whisper.net.dll` (netstandard2.0) referencia:
`netstandard 2.0.0.0`, `Microsoft.Extensions.AI.Abstractions 10.2.0.0`, `Microsoft.Bcl.AsyncInterfaces 10.0.0.7`,
`System.Threading.Tasks.Extensions 4.2.1.0`, `System.Memory 4.0.2.0`.

`ReferenceCopyLocalPaths` (com `CopyLocalLockFileAssemblies=true`) fecha a lista; o target copia só:

| DLL | Versão (assembly) | Por quê |
|---|---|---|
| `Whisper.net.dll` | 1.9.1.0 | os bindings |
| `Microsoft.Bcl.AsyncInterfaces.dll` | 10.0.0.7 | `IAsyncDisposable` (o `WhisperProcessor` implementa) |
| `System.Threading.Tasks.Extensions.dll` | 4.2.4.0 | `ValueTask` |
| `System.Memory.dll` | 4.0.5.0 | `Span<T>`/`Memory<T>` |
| `System.Buffers.dll` | 4.0.5.0 | dependência do `System.Memory` |
| `System.Numerics.Vectors.dll` | 4.1.6.0 | dependência do `System.Memory` |
| `System.Runtime.CompilerServices.Unsafe.dll` | 6.0.3.0 | dependência do `System.Memory` |

**Achado relevante (Mono do Unity 6):**

- `valheim_Data\Managed\mscorlib.dll` **já contém** `System.Span<T>`, `System.Memory<T>`,
  `System.ReadOnlySpan<T>`, `System.Buffers.*` (perfil netstandard2.1).
- `valheim_Data\Managed\System.Memory.dll` é uma **fachada**: 4.608 bytes, 0 tipos, **26 type forwards** →
  mscorlib. Ou seja, a versão do jogo (4.0.99.0) funciona como *stub* de compilação, não como implementação.
- `valheim_Data\Managed\System.Runtime.CompilerServices.Unsafe.dll` (6.0.0.0, 18 KB) tem a implementação real.
- `System.Buffers` / `System.Numerics.Vectors` **não** existem na pasta do jogo.

Por isso as nossas cópias do NuGet são levadas junto: assim o `Whisper.net` + a *sua* closure ficam
consistentes entre si (o resto do jogo continua usando o `Span<T>` do mscorlib, que é outro tipo — mas o nosso
código não usa `Span<T>`). Se o Mono preferir as fachadas dele, os tipos vêm do mscorlib e também funciona.
O log do spike imprime **de onde veio o `Span<T>`** justamente para fechar esse diagnóstico.

## 4. Deploy (`DeployWhisperNet`, depois de `DeployToPlugins`)

```
plugins\AI-Companions\
├── Companions.dll
├── Whisper.net.dll (+ 6 shims acima)
└── runtimes\win-x64\{whisper.dll, ggml-whisper.dll, ggml-base-whisper.dll, ggml-cpu-whisper.dll}
```

Só `win-x64` (12 nativos de 3 arquiteturas ficariam ~10 MB à toa).

## 5. O teste (`WhisperSpike`)

- Thread de fundo (`Task.Run`), uma execução por vez (`Interlocked`), `try/catch` em tudo — nada escapa para o Unity.
- `WhisperFactory.GetRuntimeInfo()` **primeiro**: força o carregamento nativo e devolve a arquitetura/CPU
  (AVX/AVX2/FMA) via `whisper_print_system_info`.
- `LogProvider.AddLogger` joga o log do próprio loader do Whisper.net (`Debug`) no log do BepInEx — é o que
  mostra **em que pasta** ele procurou o `whisper.dll`.
- O factory vem de `WhisperStt.TryGetFactory` (**Fase 3.3**): o modelo é carregado **uma vez** e **mantido
  carregado** (o tempo de carga é medido lá, no primeiro uso de qualquer origem — spike ou voz). O spike **não**
  descarrega mais nada no fim; a transcrição roda sob `WhisperStt.NativeGate`.
- `CreateBuilder().WithLanguageDetection().WithThreads(núcleos-1, teto 8).WithNoContext()` +
  `WithSegmentEventHandler` → `processor.Process(stream)` (síncrono, mas já estamos fora da main thread) →
  mede o **tempo de TRANSCRIÇÃO**, conta segmentos e concatena o texto.

### Gatilhos

| Gatilho | Como |
|---|---|
| Comando de console | `modia_stt_teste` (F5). Registrado com o **mesmo mecanismo que o `CommandManager` do Jotunn usa por baixo**: um postfix em `Console.Awake` que constrói um `Terminal.ConsoleCommand` (o construtor se registra sozinho em `Terminal.commands`) e chama o privado `Terminal.updateCommandList` via `AccessTools`. **O mod não referencia o Jotunn hoje**, então não foi criada uma dependência nova só para o gatilho (ver nota no fim). |
| Config | `[IA] SttTeste` (bool, **default `false`**) → dispara ~25 s após o plugin carregar. |
| Caminhos | `[IA] SttModelo` (default `whisper\ggml-base-q5_1.bin`), `[IA] SttWavTeste` (default `whisper\jfk.wav`) — relativos à pasta do plugin (`Assembly.GetExecutingAssembly().Location`) ou absolutos. |

## 6. O que ler no `BepInEx\LogOutput.log`

Procure por `[STT]`. Sequência esperada:

```
[STT] Spike do Whisper.net in-process pronto (Whisper.net 1.9.1 ...). Comando de console: "modia_stt_teste". ...
[STT] Comando de console "modia_stt_teste" registrado (digite no console do jogo, F5).
[STT] Teste do Whisper.net iniciado (comando de console modia_stt_teste) - rodando em thread de fundo, o jogo nao para.
[STT] Pasta do plugin: "C:\...\plugins\AI-Companions"
[STT] Modelo: "...\whisper\ggml-base-q5_1.bin" (58308 KB, ...)      <-- tamanho esperado ~57 MB
[STT] WAV de teste: "...\whisper\jfk.wav" (344 KB, ...)
[STT] Gerenciadas na pasta do plugin: Whisper.net.dll Microsoft.Bcl.AsyncInterfaces.dll ...
[STT] Nativos em "...\runtimes\win-x64": ggml-base-whisper.dll ggml-cpu-whisper.dll ggml-whisper.dll whisper.dll
[STT] Assemblies relevantes ja carregados: (nenhum)                <-- aqui veria um System.Memory do jogo, se houvesse
[STT] Loader nativo apontado para a pasta do plugin: RuntimeOptions.LibraryPath="...\Whisper.net.dll" (...)
[STT] Runtime nativo OK: biblioteca=Cpu | ordem=Cpu | LibraryPath="..."
[STT] whisper_print_system_info: ... AVX = 1 | AVX2 = 1 | FMA = 1 ...
[STT] Usando o modelo COMPARTILHADO com as ordens por voz (...) - sem carregar de novo.
[STT] TRANSCRICAO em NNN ms | threads=7 | idioma=en | 1 segmento(s)
[STT] TEXTO: "And so my fellow Americans, ask not what your country can do for you, ask what you can do for your country."
[STT] O modelo continua carregado (o pipeline de voz usa o mesmo).
[STT] Span<T> veio de: System.Memory 4.0.5.0 [C:\...\plugins\AI-Companions\System.Memory.dll]
[STT] Whisper.net.dll carregado de: 1.9.1.0 [C:\...\plugins\AI-Companions\Whisper.net.dll]
[STT] Teste do Whisper.net encerrado (a thread de fundo terminou).
```

> Se a linha do modelo **não** aparecer, é porque o modelo já tinha sido carregado (pelo pre-warm da voz ou por uma
> ordem anterior): a linha `Modelo CARREGADO em NNN ms e MANTIDO carregado` aparece **uma vez** no log, na primeira
> necessidade.

Sinais de problema e o que significam:

| Log | Significado |
|---|---|
| `FALHOU ao carregar a biblioteca nativa do whisper ... Failed to load native whisper library` | nativos não encontrados/carregados; a mensagem completa + stack vêm em seguida. Ajustar `LibraryPath`/`runtimes\win-x64` (ou VC++ Redist). |
| `TypeLoadException` / `MissingMethodException` com `System.Memory` | conflito com o *stub* do jogo — seria o cenário que decidiria a favor de (B); **não aconteceu** (a decisão foi (A)). |
| `FileNotFoundException` com `Microsoft.Extensions.AI` | algum caminho tocou o `WhisperSpeechToTextClient`; ver §1. |
| `TRANSCRICAO ... 0 segmento(s)` / `TEXTO: ""` | nativo carregou mas o áudio/modelo não deu texto. |
| Log sem `[STT]` nenhum | o plugin não chegou no `WhisperSpike.Init` — ver o começo do log. |

## 7. Nota sobre o Jotunn

O pedido original era registrar o comando pelo `CommandManager` do Jotunn "como os outros comandos do mod".
Este projeto **não referencia o Jotunn** (o `Companions.dll` não tem nenhuma referência a ele; os comandos
`modia_*` da Trilha B são do mod anterior). Para não introduzir uma dependência nova (e um risco de o mod não
carregar sem o Jotunn) só por causa de um gatilho de teste, o comando foi registrado com o **mesmo mecanismo
interno** que o `CommandManager` usa. Se o Jotunn for adotado no mod, trocar é uma linha:
`CommandManager.Instance.AddConsoleCommand(new ... : ConsoleCommand { ... })`.
