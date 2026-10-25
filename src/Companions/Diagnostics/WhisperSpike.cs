using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using Whisper.net;
using Whisper.net.LibraryLoader;
using Whisper.net.Logger;

namespace Companions;

/// <summary>
/// SPIKE (prova de conceito) do STT local: roda o <b>Whisper.net in-process</b> (bindings do whisper.cpp via
/// NuGet, nada de processo filho nem HTTP) dentro do jogo, transcreve um WAV de teste e escreve o resultado no
/// log. Serve para decidir entre (A) Whisper.net in-process e (B) whisper-server.exe como processo filho: se o
/// in-process der crash do jogo ou conflito de assembly, a decisão é (B).
///
/// Desde a Fase 3.3 o pipeline de voz (<see cref="VoiceInput"/>/<see cref="WhisperStt"/>) usa o <b>mesmo</b> modelo:
/// o factory vem de <see cref="WhisperStt.TryGetFactory"/> (carregado uma vez, mantido carregado) e a transcrição
/// roda sob <see cref="WhisperStt.NativeGate"/> - o spike é só diagnóstico e não carrega nada por conta própria.
///
/// Nada de pipeline de voz aqui (push-to-talk, captura, encode, TTS): é só o teste.
///
/// Garantias:
/// - tudo roda numa <b>thread de fundo</b> (Task.Run) - a main thread nunca espera pelo modelo nem pela
///   transcrição (o mesmo cuidado do LlmClient);
/// - tudo dentro de try/catch: nenhuma exceção escapa para o Unity (mensagem completa + stack no log);
/// - uma execução por vez (o comando pode ser digitado de novo enquanto roda, e o gatilho automático existe).
///
/// Gatilhos:
/// - comando de console <c>modia_stt_teste</c>;
/// - <c>[AI] SttTest</c> = true dispara sozinho ~25 s depois do plugin carregar (para o mundo já estar pronto).
///
/// Como o loader acha os nativos: ver <see cref="PrepareNativeLibraryPath"/>.
/// </summary>
internal static class WhisperSpike
{
	/// <summary>Comando de console (F5) que roda o teste.</summary>
	internal const string CommandName = "modia_stt_teste";

	/// <summary>Espera antes do gatilho automático, para o mundo estar carregado.</summary>
	private const float AutoDelaySeconds = 25f;

	private const string DefaultWavRelative = @"whisper\jfk.wav";

	/// <summary>Nomes que precisam estar na pasta do plugin (diagnóstico: só existência, não carrega nada).</summary>
	private static readonly string[] ManagedDependencies =
	{
		"Whisper.net.dll",
		"Microsoft.Bcl.AsyncInterfaces.dll",
		"System.Threading.Tasks.Extensions.dll",
		"System.Memory.dll",
		"System.Buffers.dll",
		"System.Numerics.Vectors.dll",
		"System.Runtime.CompilerServices.Unsafe.dll"
	};

	/// <summary>0 = livre, 1 = rodando. Uma execução por vez.</summary>
	private static int _running;

	private static bool _commandRegistered;

	// Gatilho automático: escrito e lido só na main thread (Init/Tick).
	private static bool _autoArmed;

	private static bool _autoFired;

	private static float _autoAt = -1f;

	/// <summary>
	/// Chamado uma vez no Awake do plugin (main thread): arma o gatilho automático e diz no log como disparar na mão.
	/// </summary>
	internal static void Init()
	{
		try
		{
			bool auto = ModConfig.SttTest != null && ModConfig.SttTest.Value;
			if (auto)
			{
				_autoArmed = true;
				_autoAt = Time.realtimeSinceStartup + AutoDelaySeconds;
			}
			string model = WhisperStt.ModelPath;
			string wav = ModConfig.SttWav != null ? ModConfig.SttWav.Value : DefaultWavRelative;
			CompanionsPlugin.Log?.LogInfo($"[STT] Spike do Whisper.net in-process pronto (Whisper.net 1.9.1 + Whisper.net.Runtime 1.9.1, CPU). "
				+ $"Comando de console: \"{CommandName}\". Gatilho automatico: {(auto ? $"LIGADO, em {AutoDelaySeconds:0} s" : "desligado ([AI] SttTest = false)")}. "
				+ $"Modelo: \"{model}\" | WAV: \"{wav}\" (relativos a pasta do plugin quando nao forem caminho absoluto). "
				+ "O modelo e compartilhado com as ordens por voz (AI/WhisperStt.cs): ele e carregado uma vez so.");
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log?.LogWarning("[STT] Init falhou: " + ex);
		}
	}

	/// <summary>Chamado a cada frame pelo Update do plugin (main thread). Só faz trabalho quando o gatilho automático venceu.</summary>
	internal static void Tick()
	{
		if (!_autoArmed || _autoFired)
		{
			return;
		}
		if (Time.realtimeSinceStartup < _autoAt)
		{
			return;
		}
		_autoFired = true;
		Run("automatico ([AI] SttTest = true)");
	}

	/// <summary>
	/// Dispara o teste numa thread de fundo. Volta na hora: quem chamou (main thread) nunca espera.
	/// </summary>
	internal static void Run(string reason)
	{
		if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
		{
			CompanionsPlugin.Log?.LogInfo("[STT] O teste ja esta rodando - ignorando este pedido.");
			return;
		}
		CompanionsPlugin.Log?.LogInfo($"[STT] Teste do Whisper.net iniciado ({reason}) - rodando em thread de fundo, o jogo nao para.");
		Task.Run(delegate
		{
			try
			{
				Execute(reason);
			}
			catch (Exception ex)
			{
				// rede de segurança: nada pode escapar para o Unity
				CompanionsPlugin.Log?.LogError("[STT] Excecao nao tratada no spike do Whisper.net: " + ex);
			}
			finally
			{
				Volatile.Write(ref _running, 0);
				CompanionsPlugin.Log?.LogInfo("[STT] Teste do Whisper.net encerrado (a thread de fundo terminou).");
			}
		});
	}

	/// <summary>
	/// O teste em si, já fora da main thread.
	/// </summary>
	private static void Execute(string reason)
	{
		ManualLogSource log = CompanionsPlugin.Log;
		log?.LogInfo($"[STT] ===== Whisper.net in-process: teste ({reason}) =====");

		string pluginDir = WhisperStt.PluginDirectory();
		log?.LogInfo($"[STT] Pasta do plugin: \"{pluginDir}\"");
		log?.LogInfo($"[STT] Thread de fundo: {Thread.CurrentThread.ManagedThreadId} | Processador: {Environment.ProcessorCount} nucleos");

		string modelPath = WhisperStt.ModelPath;
		string wavPath = WhisperStt.ResolvePath(pluginDir, ModConfig.SttWav != null ? ModConfig.SttWav.Value : null, DefaultWavRelative);
		log?.LogInfo($"[STT] Modelo: \"{modelPath}\" ({DescribeFile(modelPath)})");
		log?.LogInfo($"[STT] WAV de teste: \"{wavPath}\" ({DescribeFile(wavPath)})");

		LogDependencyFiles(pluginDir);
		LogLoadedAssemblies();

		if (!File.Exists(modelPath))
		{
			log?.LogError($"[STT] Modelo nao encontrado em \"{modelPath}\". Coloque o ggml (ex.: ggml-small-q5_1.bin) na pasta whisper\\ do plugin ou ajuste [AI] SttModel.");
			return;
		}
		if (!File.Exists(wavPath))
		{
			log?.LogError($"[STT] WAV de teste nao encontrado em \"{wavPath}\". Coloque um WAV (16 kHz, mono, PCM) na pasta whisper\\ do plugin ou ajuste [AI] SttWavTest.");
			return;
		}

		// A partir daqui qualquer coisa que falhe é do Whisper.net/nativo: reportamos tudo e paramos.
		WhisperStt.PrepareNativeLibraryPath();

		IDisposable whisperLogger = null;
		try
		{
			// O logger do Whisper.net (inclui o debug do proprio loader nativo: onde ele procurou o whisper.dll).
			try
			{
				whisperLogger = LogProvider.AddLogger(delegate(WhisperLogLevel level, string message)
				{
					CompanionsPlugin.Log?.LogDebug($"[STT][whisper:{level}] {message}");
				});
			}
			catch (Exception ex)
			{
				log?.LogWarning("[STT] Nao consegui instalar o logger do Whisper.net: " + ex.Message);
			}

			// GetRuntimeInfo() forca o carregamento da biblioteca nativa e devolve a arquitetura/CPU detectada
			// (AVX, AVX2, FMA...). E a primeira coisa que pode falhar com "Failed to load native whisper library".
			string runtimeInfo = null;
			try
			{
				runtimeInfo = WhisperFactory.GetRuntimeInfo();
			}
			catch (Exception ex)
			{
				log?.LogError("[STT] FALHOU ao carregar a biblioteca nativa do whisper (WhisperFactory.GetRuntimeInfo). " + ex);
				return;
			}
			log?.LogInfo($"[STT] Runtime nativo OK: biblioteca={RuntimeOptions.LoadedLibrary?.ToString() ?? "?"} | ordem={Join(RuntimeOptions.RuntimeLibraryOrder)} | LibraryPath=\"{RuntimeOptions.LibraryPath}\"");
			log?.LogInfo($"[STT] whisper_print_system_info: {(string.IsNullOrEmpty(runtimeInfo) ? "(vazio)" : runtimeInfo.Trim())}");

			Transcribe(log, modelPath, wavPath);
		}
		catch (Exception ex)
		{
			log?.LogError("[STT] FALHA no teste do Whisper.net: " + ex);
		}
		finally
		{
			try
			{
				whisperLogger?.Dispose();
			}
			catch
			{
				// o logger é só diagnóstico
			}
			LogSpanDiagnostics(log);
		}
	}

	/// <summary>
	/// Transcreve o WAV de teste com o <b>mesmo</b> modelo do pipeline de voz: o factory vem de
	/// <see cref="WhisperStt.TryGetFactory"/> (carregado uma vez e mantido carregado - o spike não carrega nem
	/// descarrega mais nada). A transcrição em si roda sob o <see cref="WhisperStt.NativeGate"/>, o mesmo cadeado das
	/// ordens por voz, para as duas coisas nunca usarem o modelo nativo ao mesmo tempo.
	/// </summary>
	private static void Transcribe(ManualLogSource log, string modelPath, string wavPath)
	{
		int threads = Mathf.Clamp(Environment.ProcessorCount - 1, 1, 8);
		StringBuilder text = new StringBuilder(256);
		int segments = 0;
		string language = null;

		if (!WhisperStt.TryGetFactory(out WhisperFactory factory, out string error))
		{
			log?.LogError($"[STT] Nao consegui obter o modelo compartilhado ({modelPath}): {error}");
			return;
		}
		log?.LogInfo($"[STT] Usando o modelo COMPARTILHADO com as ordens por voz ({modelPath}) - sem carregar de novo.");

		lock (WhisperStt.NativeGate)
		{
			using (WhisperProcessor processor = factory.CreateBuilder()
				.WithLanguageDetection()   // deixa o modelo decidir o idioma: serve tanto para o jfk.wav (en) quanto para um WAV em PT-BR
				.WithThreads(threads)
				.WithNoContext()
				.WithSegmentEventHandler(delegate(SegmentData segment)
				{
					segments++;
					if (string.IsNullOrEmpty(language))
					{
						language = segment.Language;
					}
					text.Append(segment.Text);
					CompanionsPlugin.Log?.LogDebug($"[STT] segmento {segments}: [{segment.Start:mm\\:ss\\.ff} - {segment.End:mm\\:ss\\.ff}] \"{segment.Text}\"");
				})
				.Build())
			{
				Stopwatch transcribeWatch = Stopwatch.StartNew();
				using (FileStream stream = File.OpenRead(wavPath))
				{
					processor.Process(stream);
				}
				transcribeWatch.Stop();

				string result = text.ToString().Trim();
				log?.LogInfo($"[STT] TRANSCRICAO em {transcribeWatch.ElapsedMilliseconds} ms | threads={threads} | idioma={(string.IsNullOrEmpty(language) ? "?" : language)} | {segments} segmento(s)");
				log?.LogInfo($"[STT] TEXTO: \"{result}\"");
				if (result.Length == 0)
				{
					log?.LogWarning("[STT] A transcricao voltou vazia - o WAV pode nao ter fala, ou o modelo pode estar errado (o ggml-small-q5_1.bin e um modelo valido?).");
				}
			}
		}
		log?.LogInfo("[STT] O modelo continua carregado (o pipeline de voz usa o mesmo).");
	}

	/// <summary>Pasta do plugin: a pasta do Companions.dll (onde estão Whisper.net.dll e runtimes\).</summary>
	private static string PluginDirectory()
	{
		return WhisperStt.PluginDirectory();
	}

	private static string DescribeFile(string path)
	{
		try
		{
			FileInfo info = new FileInfo(path);
			if (!info.Exists)
			{
				return "nao existe";
			}
			return $"{info.Length / 1024} KB, {info.LastWriteTime:yyyy-MM-dd HH:mm}";
		}
		catch (Exception ex)
		{
			return "erro ao ler: " + ex.Message;
		}
	}

	/// <summary>Só confere se as DLLs gerenciadas e os nativos chegaram na pasta do plugin (não carrega nada).</summary>
	private static void LogDependencyFiles(string pluginDir)
	{
		StringBuilder present = new StringBuilder();
		StringBuilder missing = new StringBuilder();
		foreach (string name in ManagedDependencies)
		{
			if (File.Exists(Path.Combine(pluginDir, name)))
			{
				present.Append(name).Append(' ');
			}
			else
			{
				missing.Append(name).Append(' ');
			}
		}
		string nativeDir = Path.Combine(pluginDir, "runtimes", "win-x64");
		StringBuilder natives = new StringBuilder();
		if (Directory.Exists(nativeDir))
		{
			foreach (string file in Directory.GetFiles(nativeDir, "*.dll"))
			{
				natives.Append(Path.GetFileName(file)).Append(' ');
			}
		}
		CompanionsPlugin.Log?.LogInfo($"[STT] Gerenciadas na pasta do plugin: {present.ToString().Trim()}");
		if (missing.Length > 0)
		{
			CompanionsPlugin.Log?.LogWarning($"[STT] Gerenciadas FALTANDO na pasta do plugin: {missing.ToString().Trim()} (o build copia: veja o target DeployWhisperNet)");
		}
		CompanionsPlugin.Log?.LogInfo($"[STT] Nativos em \"{nativeDir}\": {(natives.Length > 0 ? natives.ToString().Trim() : "(pasta vazia/inexistente)")}");
	}

	/// <summary>
	/// O que já está carregado no processo antes de tocar no Whisper.net: é aqui que se vê se o Mono do Unity já
	/// trouxe o stub de System.Memory/System.Runtime.CompilerServices.Unsafe do jogo. Usa GetAssemblies() (não
	/// Assembly.Load) para não forçar o carregamento de nada.
	/// </summary>
	private static void LogLoadedAssemblies()
	{
		try
		{
			StringBuilder builder = new StringBuilder();
			foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				AssemblyName name = assembly.GetName();
				if (name.Name == "System.Memory" || name.Name == "System.Buffers" || name.Name == "System.Numerics.Vectors"
					|| name.Name == "System.Runtime.CompilerServices.Unsafe" || name.Name == "Microsoft.Bcl.AsyncInterfaces"
					|| name.Name == "System.Threading.Tasks.Extensions" || name.Name == "Whisper.net")
				{
					builder.Append(name.Name).Append('=').Append(name.Version).Append(" [").Append(LocationOf(assembly)).Append("] ");
				}
			}
			CompanionsPlugin.Log?.LogInfo($"[STT] Assemblies relevantes ja carregados: {(builder.Length > 0 ? builder.ToString().Trim() : "(nenhum)")}");
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log?.LogDebug("[STT] Nao consegui listar os assemblies carregados: " + ex.Message);
		}
	}

	/// <summary>
	/// Depois do teste: de onde o runtime realmente tirou Span&lt;T&gt;/Memory&lt;T&gt;. O jogo traz um stub vazio de
	/// System.Memory.dll em valheim_Data\Managed, então saber se o Span veio de lá, do mscorlib ou da NOSSA copia
	/// é o que fecha o diagnóstico de conflito de assembly.
	/// </summary>
	private static void LogSpanDiagnostics(ManualLogSource log)
	{
		try
		{
			Type span = typeof(Span<byte>);
			log?.LogInfo($"[STT] Span<T> veio de: {span.Assembly.GetName().Name} {span.Assembly.GetName().Version} [{LocationOf(span.Assembly)}]");
		}
		catch (Exception ex)
		{
			log?.LogWarning("[STT] Nao consegui descobrir de onde veio Span<T> (possivel conflito de assembly): " + ex);
		}
		try
		{
			Assembly whisper = typeof(WhisperFactory).Assembly;
			log?.LogInfo($"[STT] Whisper.net.dll carregado de: {whisper.GetName().Version} [{LocationOf(whisper)}]");
		}
		catch (Exception ex)
		{
			log?.LogDebug("[STT] Nao consegui localizar o Whisper.net.dll: " + ex.Message);
		}
	}

	private static string LocationOf(Assembly assembly)
	{
		try
		{
			return string.IsNullOrEmpty(assembly.Location) ? "sem Location" : assembly.Location;
		}
		catch (Exception)
		{
			return "sem Location";
		}
	}

	private static string Join(List<RuntimeLibrary> libraries)
	{
		if (libraries == null || libraries.Count == 0)
		{
			return "(vazia)";
		}
		StringBuilder builder = new StringBuilder();
		foreach (RuntimeLibrary library in libraries)
		{
			builder.Append(library).Append(' ');
		}
		return builder.ToString().Trim();
	}

	/// <summary>
	/// Postfix no <c>Console.Awake</c> (o mesmo gancho que o CommandManager do Jotunn usa por baixo) para registrar
	/// o comando de console. O construtor de <c>Terminal.ConsoleCommand</c> se registra sozinho no dicionário
	/// <c>Terminal.commands</c>; só falta reconstruir a lista de autocompletar.
	/// </summary>
	[HarmonyPatch(typeof(Console), "Awake")]
	private static class ConsoleAwake_Patch
	{
		private static void Postfix(Console __instance)
		{
			WhisperSpike.RegisterConsoleCommand(__instance);
		}
	}

	private static void RegisterConsoleCommand(Console console)
	{
		if (_commandRegistered)
		{
			return;
		}
		try
		{
			// O mod não referencia o Jotunn hoje, então o comando é registrado com o mesmo mecanismo que o
			// CommandManager do Jotunn usa internamente: construir o Terminal.ConsoleCommand (ele mesmo se
			// registra em Terminal.commands) e reconstruir a lista de autocompletar.
			new Terminal.ConsoleCommand(
				CommandName,
				"ModIA: roda o teste do Whisper.net in-process (STT local) e escreve o resultado no log do BepInEx.",
				(Terminal.ConsoleEvent)delegate(Terminal.ConsoleEventArgs args)
				{
					Run("comando de console " + CommandName);
				},
				false,   // isCheat: funciona sem devcommands
				false,   // isNetwork
				false,   // onlyServer
				false,   // isSecret: aparece no "help"
				false,   // allowInDevBuild
				false,   // hideBehindDevCommands
				null,    // optionsFetcher
				false,   // alwaysRefreshTabOptions
				false,   // remoteCommand
				false);  // onlyAdmin
			AccessTools.Method(typeof(Terminal), "updateCommandList")?.Invoke(console, null);
			_commandRegistered = true;
			CompanionsPlugin.Log?.LogInfo($"[STT] Comando de console \"{CommandName}\" registrado (digite no console do jogo, F5).");
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log?.LogWarning($"[STT] Nao consegui registrar o comando de console \"{CommandName}\": " + ex);
		}
	}
}
