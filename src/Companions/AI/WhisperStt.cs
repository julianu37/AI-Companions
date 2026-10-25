using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Whisper.net;
using Whisper.net.LibraryLoader;

namespace Companions;

/// <summary>
/// One finished (or failed) transcription. Filled on the background thread, applied on the main thread.
/// </summary>
internal sealed class SttResult
{
	internal bool Ok;

	internal string Error;

	/// <summary>What the model heard, already trimmed. Empty when the audio had no speech the model could read.</summary>
	internal string Text;

	/// <summary>Language the model used (or detected), for the log.</summary>
	internal string Language;

	internal int Segments;

	internal int ElapsedMs;

	internal string Reason;
}

/// <summary>
/// Fase 3.3: local speech-to-text, reusable. This is the Whisper.net spike (Diagnostics/WhisperSpike.cs) turned
/// into a service: the same in-process bindings, the same native-loader setup, but
/// <list type="bullet">
/// <item>the model is loaded <b>once</b> and <b>kept loaded</b> (the spike loaded and disposed it on every run), so a
/// voice order does not pay the load time again;</item>
/// <item>a transcription runs on a <b>background task</b> and comes back through a thread-safe queue that
/// <see cref="VoiceInput"/> drains in Update - the main thread never waits (the same pattern as <see cref="LlmClient"/>);</item>
/// <item>only one transcription touches the native model at a time (<see cref="NativeGate"/>), whoever asks: the voice
/// pipeline or the diagnostic spike.</item>
/// </list>
///
/// The audio it takes is a complete <b>WAV PCM 16-bit mono 16 kHz</b> file in memory (what
/// <see cref="VoiceInput"/> records and encodes): 16 kHz mono is the format Whisper expects, and going through the
/// WAV reader is the path the spike already proved in game.
/// </summary>
internal static class WhisperStt
{
	/// <summary>The sample rate the whole voice pipeline works at: 16 kHz mono, which is what Whisper expects.</summary>
	internal const int SampleRate = 16000;

	/// <summary>
	/// Where the ggml model is expected when [AI] SttModel is left at its default (relative to the plugin folder).
	/// Fase 3.3b: <b>small-q5_1</b> (181 MB) instead of base-q5_1 (57 MB) - the short clips of a voice order ("colete
	/// 10 madeiras") came back wrong far too often with base, and small is markedly better in PT-BR. It costs roughly
	/// 2-3x the CPU time per transcription (still well under a second for a 2 s clip on a 8-thread CPU, and it never
	/// blocks the game: the transcription runs on a background task).
	/// </summary>
	internal const string DefaultModelRelative = @"whisper\ggml-small-q5_1.bin";

	/// <summary>The model the default replaced (Fase 3.3): kept on disk, and [AI] SttModel can still point at it.</summary>
	internal const string PreviousModelRelative = @"whisper\ggml-base-q5_1.bin";

	/// <summary>Finished transcriptions, waiting for the main thread to read them.</summary>
	private static readonly ConcurrentQueue<SttResult> _finished = new ConcurrentQueue<SttResult>();

	/// <summary>Guards the one-time model load.</summary>
	private static readonly object _loadLock = new object();

	/// <summary>
	/// Guards the native model: only one transcription at a time uses it. Shared with the diagnostic spike, so a
	/// manual <c>modia_stt_teste</c> and a voice order can never run at the same time.
	/// </summary>
	internal static readonly object NativeGate = new object();

	private static WhisperFactory _factory;

	private static bool _loadFailed;

	private static string _loadError;

	/// <summary>
	/// Fase 3.24 (auditoria): the signature (size + last write) of the file the failed load was taken from. A model
	/// copied while the game is open is present but truncated - a load failure that heals by itself once the copy
	/// finishes - so a DIFFERENT file clears <see cref="_loadFailed"/> and is tried again; the same file is not.
	/// </summary>
	private static string _loadFailedSignature;

	/// <summary>The load failure itself is logged once, not on every retry.</summary>
	private static bool _warnedLoadFailed;

	/// <summary>The "model not found" error is logged once (the pre-load retry calls TryGetFactory again).</summary>
	private static bool _warnedMissingModelFile;

	private static string _modelPath;

	private static int _busy;

	private static float _busySince;

	private static int _jobs;

	/// <summary>1 while a pre-load attempt is running (see <see cref="MaybePrewarm"/>).</summary>
	private static int _prewarmInFlight;

	/// <summary>1 once the model is loaded (or failed for good): no more pre-load attempts.</summary>
	private static int _prewarmDone;

	/// <summary>When the next pre-load attempt may start (main thread only).</summary>
	private static float _nextPrewarmAttempt;

	/// <summary>The "not on disk yet" warning is logged once, not on every retry.</summary>
	private static bool _warnedPrewarmMissing;

	/// <summary>The configured [AI] SttModel value the cached <see cref="_modelPath"/> was built from.</summary>
	private static string _modelPathSource;

	/// <summary>True while a transcription is in flight (one at a time, so two orders can't fight over the model).</summary>
	internal static bool Busy => Volatile.Read(ref _busy) != 0;

	/// <summary>The model file to load: [AI] SttModel (relative to the plugin folder, or absolute).</summary>
	internal static string ModelPath
	{
		get
		{
			string configured = ModConfig.SttModel != null ? ModConfig.SttModel.Value : null;
			string cached = _modelPath;
			// Fase 3.23 (auditoria): the path used to be cached for the whole session, so fixing [AI] SttModel in the
			// F8 panel changed nothing until the game was restarted. It is resolved again when the config value changes.
			if (cached != null && string.Equals(configured, _modelPathSource, StringComparison.Ordinal))
			{
				return cached;
			}
			cached = ResolvePath(PluginDirectory(), configured, DefaultModelRelative);
			_modelPathSource = configured;
			_modelPath = cached;
			return cached;
		}
	}

	/// <summary>
	/// Fase 3.3b: the initial prompt (vocabulary bias) for the decoder, from [AI] VoiceInitialPrompt. Empty = none.
	/// Read on the main thread (<see cref="Request"/>) and carried to the background task.
	/// </summary>
	internal static string Prompt
	{
		get
		{
			string value = ModConfig.VozPromptInitial != null ? ModConfig.VozPromptInitial.Value : null;
			return string.IsNullOrWhiteSpace(value) ? "" : value.Trim();
		}
	}

	/// <summary>True when a transcription could be attempted right now (the model file is there and nothing failed before).</summary>
	internal static bool Ready
	{
		get
		{
			if (IsLoadFailureCurrent())
			{
				return false;
			}
			try
			{
				return File.Exists(ModelPath);
			}
			catch (Exception)
			{
				return false;
			}
		}
	}

	/// <summary>
	/// Fase 3.24 (auditoria): is the recorded load failure still about the file that is on disk now? False after the
	/// file changed (a copy that finished, a new model), which is what lets the load be tried again.
	/// </summary>
	private static bool IsLoadFailureCurrent()
	{
		return _loadFailed && string.Equals(ModelSignature(ModelPath), _loadFailedSignature, StringComparison.Ordinal);
	}

	/// <summary>Size and last write of the model file: any change means a different (possibly complete) file.</summary>
	private static string ModelSignature(string path)
	{
		try
		{
			FileInfo info = new FileInfo(path);
			if (!info.Exists)
			{
				return "missing";
			}
			return info.Length.ToString(CultureInfo.InvariantCulture) + "@" + info.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture);
		}
		catch (Exception)
		{
			return "?";
		}
	}

	/// <summary>
	/// Called once from the plugin's Awake (main thread). Resolves the model path and says what the voice orders will
	/// use; it does <b>not</b> load the model here (that is <see cref="Prewarm"/>, which runs on a background thread).
	/// </summary>
	internal static void Init()
	{
		try
		{
			string language = NormalizeLanguage(ModConfig.VozLanguage != null ? ModConfig.VozLanguage.Value : null);
			string prompt = Prompt;
			CompanionsPlugin.Log?.LogInfo($"[STT] STT local (Whisper.net in-process, CPU) pronto para as ordens por voz: "
				+ $"modelo \"{ModelPath}\" ({DescribeFile(ModelPath)}) | idioma {(language ?? "detectado pelo modelo")} | "
				+ $"{Threads} thread(s) | audio {SampleRate} Hz mono. O modelo e carregado UMA vez e fica carregado.");
			if (prompt.Length > 0)
			{
				CompanionsPlugin.Log?.LogInfo($"[STT] Prompt inicial do Whisper ([AI] VoiceInitialPrompt, {prompt.Length} caracteres): \"{prompt}\" "
					+ "- ele enviesa o vocabulario das ordens; vazio desliga o vies.");
			}
			else
			{
				CompanionsPlugin.Log?.LogInfo("[STT] Prompt inicial do Whisper desligado ([AI] VoiceInitialPrompt vazio).");
			}
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log?.LogWarning("[STT] Init falhou: " + ex);
		}
	}

	/// <summary>
	/// Loads the model in the background, before the first voice order needs it, so the first order is not slower than
	/// the others. Called from <see cref="MaybePrewarm"/> (which is what decides whether a new attempt is due); does
	/// nothing while another attempt is already running.
	/// </summary>
	internal static void Prewarm()
	{
		if (Interlocked.CompareExchange(ref _prewarmInFlight, 1, 0) != 0)
		{
			return;
		}
		Task.Run(delegate
		{
			try
			{
				if (TryGetFactory(out WhisperFactory _, out string error))
				{
					Volatile.Write(ref _prewarmDone, 1);
					CompanionsPlugin.Log?.LogInfo("[STT] Modelo pre-carregado em segundo plano (a primeira ordem por voz nao vai esperar por ele).");
				}
				else if (_loadFailed)
				{
					// a hard failure (a bad model, a native library problem): trying again would only repeat it -
					// unless the file itself changed since then, and then MaybePrewarm keeps the door open
					// (Fase 3.24: a model copied while the game was open is truncated, not missing)
					if (IsLoadFailureCurrent())
					{
						Volatile.Write(ref _prewarmDone, 1);
						CompanionsPlugin.Log?.LogWarning("[STT] Nao consegui pre-carregar o modelo: " + error);
					}
				}
				else
				{
					// the file is simply not there yet: keep the door open (see MaybePrewarm) instead of leaving the
					// voice off for the rest of the session
					if (!_warnedPrewarmMissing)
					{
						_warnedPrewarmMissing = true;
						CompanionsPlugin.Log?.LogWarning("[STT] Nao consegui pre-carregar o modelo: " + error
							+ $" Vou tentar de novo a cada {PrewarmRetrySeconds:0} s enquanto voce estiver no mundo (o modelo pode ainda nao estar no disco).");
					}
				}
			}
			catch (Exception ex)
			{
				CompanionsPlugin.Log?.LogWarning("[STT] Excecao ao pre-carregar o modelo: " + ex);
			}
			finally
			{
				Volatile.Write(ref _prewarmInFlight, 0);
			}
		});
	}

	/// <summary>
	/// How long between two pre-load attempts while the model file is not there yet (see <see cref="MaybePrewarm"/>).
	/// </summary>
	private const float PrewarmRetrySeconds = 30f;

	/// <summary>
	/// Warms the model once the player is in the world - and <b>tries again</b> while the model is not there yet.
	/// Fase 3.23 (auditoria): this mirrors the TTS pre-load (<c>CompanionTts.MaybePrewarm</c>). The old version was
	/// single-shot: copying the <c>whisper\</c> folder after the first spawn (or fixing [AI] SttModel) left the voice off
	/// until a restart. One attempt at a time, one every <see cref="PrewarmRetrySeconds"/>, and only until the model is
	/// loaded or the load failed for good. Runs on the main thread once per frame but does nothing (a couple of
	/// bool reads) unless a retry is really due.
	/// </summary>
	internal static void MaybePrewarm()
	{
		if (Volatile.Read(ref _prewarmDone) != 0)
		{
			return;
		}
		if (IsLoadFailureCurrent())
		{
			// Fase 3.24 (auditoria): the same file that failed is not tried again; a changed one is (see TryGetFactory)
			return;
		}
		if (Player.m_localPlayer == null || UnityEngine.Time.unscaledTime < _nextPrewarmAttempt)
		{
			return;
		}
		if (Volatile.Read(ref _prewarmInFlight) != 0)
		{
			// one attempt at a time; Prewarm itself takes the flag (it is the only place that does)
			return;
		}
		_nextPrewarmAttempt = UnityEngine.Time.unscaledTime + PrewarmRetrySeconds;
		Prewarm();
	}

	/// <summary>
	/// Queues one WAV (PCM 16-bit mono 16 kHz) to be transcribed. Called on the main thread; returns immediately -
	/// the transcription itself happens on a background task. False when one is already running.
	/// </summary>
	internal static bool Request(byte[] wav, string reason)
	{
		if (wav == null || wav.Length <= 44)
		{
			return false;
		}
		if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
		{
			CompanionsPlugin.Log?.LogInfo("[STT] Ja existe uma transcricao em andamento - este audio foi descartado.");
			return false;
		}
		// every config value is read here, on the main thread (the same care LlmClient takes)
		_busySince = UnityEngine.Time.realtimeSinceStartup;
		string language = NormalizeLanguage(ModConfig.VozLanguage != null ? ModConfig.VozLanguage.Value : null);
		string prompt = Prompt;
		int threads = Threads;
		int job = Interlocked.Increment(ref _jobs);

		CompanionsPlugin.Log?.LogInfo($"[STT] Voz #{job} ({reason}): {wav.Length / 1024} KB de audio, idioma {(language ?? "automatico")}, "
			+ $"{threads} thread(s), prompt inicial {(prompt.Length > 0 ? $"{prompt.Length} caracteres" : "desligado")} "
			+ $"- transcrevendo em thread de fundo, o jogo nao para.");
		Task.Run(delegate
		{
			Work(wav, reason, language, prompt, threads);
		});
		return true;
	}

	/// <summary>Called from Update(): hands back one finished transcription, or false when there is nothing to do.</summary>
	internal static bool TryDequeue(out SttResult result)
	{
		return _finished.TryDequeue(out result);
	}

	/// <summary>Called from Update() when a result has been handled: releases the in-flight slot.</summary>
	internal static void MarkDone()
	{
		Interlocked.Exchange(ref _busy, 0);
	}

	/// <summary>
	/// Called from Update() right after draining: a transcription that hangs (a native call that never returns) must
	/// not lock the voice orders out forever.
	/// </summary>
	internal static void Tick()
	{
		if (Busy && UnityEngine.Time.realtimeSinceStartup - _busySince > WatchdogSeconds)
		{
			CompanionsPlugin.Log?.LogWarning("[STT] Uma transcricao passou do tempo limite sem responder - liberando o espaco na fila.");
			Interlocked.Exchange(ref _busy, 0);
		}
	}

	/// <summary>Frees the native model. Called from the plugin's shutdown.</summary>
	internal static void Shutdown()
	{
		if (Busy)
		{
			// disposing the factory under a running transcription would take the native library down with it
			CompanionsPlugin.Log?.LogWarning("[STT] Ainda havia uma transcricao em andamento no descarregamento - o modelo ficou carregado.");
			return;
		}
		lock (_loadLock)
		{
			try
			{
				_factory?.Dispose();
			}
			catch (Exception ex)
			{
				CompanionsPlugin.Log?.LogDebug("[STT] Falha ao liberar o modelo do Whisper: " + ex.Message);
			}
			_factory = null;
		}
	}

	/// <summary>
	/// Loads the model once and keeps it. Shared with the diagnostic spike, so the model is never loaded twice.
	/// Safe to call from any thread; the first caller does the load, the others wait for it.
	/// </summary>
	internal static bool TryGetFactory(out WhisperFactory factory, out string error)
	{
		lock (_loadLock)
		{
			factory = _factory;
			if (factory != null)
			{
				error = null;
				return true;
			}
			if (_loadFailed)
			{
				// Fase 3.24 (auditoria): a load failure used to be permanent. A file copied with the game open is
				// present and TRUNCATED, which fails differently from "not on disk yet" and also heals by itself: the
				// failed signature is kept and any change in the file (size or write time) clears the flag and tries
				// again. An unchanged file is not tried twice (the log said it once, and it stays that way).
				if (string.Equals(ModelSignature(ModelPath), _loadFailedSignature, StringComparison.Ordinal))
				{
					error = _loadError;
					return false;
				}
				_loadFailed = false;
				_loadError = null;
				CompanionsPlugin.Log?.LogInfo("[STT] O arquivo do modelo mudou desde a falha de carga - tentando carrega-lo de novo.");
			}
			string modelPath = ModelPath;
			try
			{
				if (!File.Exists(modelPath))
				{
					// Fase 3.23 (auditoria): a missing file is NOT a permanent failure (the TTS already works this
					// way): the player may copy the whisper\ folder with the game open, and MaybePrewarm tries again
					// every PrewarmRetrySeconds. Only a real load failure sets _loadFailed.
					_loadError = $"modelo nao encontrado em \"{modelPath}\"";
					if (!_warnedMissingModelFile)
					{
						_warnedMissingModelFile = true;
						CompanionsPlugin.Log?.LogError($"[STT] {_loadError}. Coloque o ggml (ex.: ggml-small-q5_1.bin) na pasta whisper\\ do plugin ou ajuste [AI] SttModel.");
					}
					error = _loadError;
					return false;
				}
				PrepareNativeLibraryPath();
				Stopwatch watch = Stopwatch.StartNew();
				factory = WhisperFactory.FromPath(modelPath, new WhisperFactoryOptions
				{
					UseGpu = false,
					DelayInitialization = false
				});
				watch.Stop();
				_factory = factory;
				CompanionsPlugin.Log?.LogInfo($"[STT] Modelo CARREGADO em {watch.ElapsedMilliseconds} ms e MANTIDO carregado: \"{modelPath}\" "
					+ $"(whisper.cpp via Whisper.net, CPU, runtime={RuntimeOptions.LoadedLibrary?.ToString() ?? "?"})");
				error = null;
				return true;
			}
			catch (Exception ex)
			{
				_loadFailed = true;
				_loadError = ex.Message;
				_loadFailedSignature = ModelSignature(modelPath);
				if (!_warnedLoadFailed)
				{
					_warnedLoadFailed = true;
					CompanionsPlugin.Log?.LogError("[STT] FALHOU ao carregar o modelo do Whisper (as ordens por voz nao vao funcionar). " + ex);
				}
				else
				{
					CompanionsPlugin.Log?.LogWarning("[STT] O modelo do Whisper falhou de novo: " + ex.Message);
				}
				error = _loadError;
				factory = null;
				return false;
			}
		}
	}

	/// <summary>
	/// The transcription itself, already off the main thread. Anything that fails is turned into a result: no exception
	/// ever escapes to the Unity side.
	/// </summary>
	private static void Work(byte[] wav, string reason, string language, string prompt, int threads)
	{
		SttResult result = new SttResult
		{
			Reason = reason
		};
		long started = DateTime.UtcNow.Ticks;
		try
		{
			if (!TryGetFactory(out WhisperFactory factory, out string error))
			{
				result.Ok = false;
				result.Error = error;
			}
			else
			{
				StringBuilder text = new StringBuilder(256);
				int[] segments = new int[1];
				string[] detected = new string[1];
				lock (NativeGate)
				{
					WhisperProcessorBuilder builder = factory.CreateBuilder()
						.WithThreads(threads)
						.WithNoContext()
						.WithSegmentEventHandler(delegate(SegmentData segment)
						{
							segments[0]++;
							if (string.IsNullOrEmpty(detected[0]))
							{
								detected[0] = segment.Language;
							}
							text.Append(segment.Text);
						});
					if (!string.IsNullOrEmpty(prompt))
					{
						// Fase 3.3b: the initial prompt (vocabulary bias). It survives WithNoContext on purpose:
						// whisper.cpp clears the *accumulated* past text first and prepends the initial prompt after
						// that, so no_context only means "don't condition on what this recording already produced".
						builder = builder.WithPrompt(prompt);
					}
					builder = string.IsNullOrEmpty(language) ? builder.WithLanguageDetection() : builder.WithLanguage(language);
					using (WhisperProcessor processor = builder.Build())
					using (MemoryStream stream = new MemoryStream(wav, false))
					{
						processor.Process(stream);
					}
				}
				result.Ok = true;
				result.Text = text.ToString().Trim();
				result.Language = detected[0];
				result.Segments = segments[0];
				if (IsPromptEcho(result.Text, prompt))
				{
					// the classic downside of an initial prompt: with audio that has no clear speech the model can
					// simply repeat the prompt back. It never looks like a real order (nobody says "Ordens para o
					// companheiro..."), so it is thrown away instead of becoming a bogus order.
					CompanionsPlugin.Log?.LogInfo("[STT] O Whisper repetiu o prompt inicial (audio sem fala clara) - tratando como nenhuma fala.");
					result.Text = "";
				}
			}
		}
		catch (Exception ex)
		{
			result.Ok = false;
			result.Error = ex.Message;
			CompanionsPlugin.Log?.LogError("[STT] FALHA na transcricao: " + ex);
		}
		result.ElapsedMs = (int)((DateTime.UtcNow.Ticks - started) / TimeSpan.TicksPerMillisecond);
		if (result.Ok)
		{
			CompanionsPlugin.Log?.LogInfo($"[STT] TRANSCRICAO em {result.ElapsedMs} ms ({reason}) | idioma={Value(result.Language)} | "
				+ $"{result.Segments} segmento(s) | prompt={(string.IsNullOrEmpty(prompt) ? "desligado" : prompt.Length + " car.")} | TEXTO: \"{result.Text}\"");
		}
		else
		{
			CompanionsPlugin.Log?.LogWarning($"[STT] Transcricao falhou em {result.ElapsedMs} ms ({reason}): {result.Error}");
		}
		_finished.Enqueue(result);
	}

	private static float WatchdogSeconds => 60f;

	private static int Threads => Math.Min(Math.Max(Environment.ProcessorCount - 1, 1), 8);

	/// <summary>How many characters a transcription has to share with the START of the prompt to count as an echo.</summary>
	private const int PromptEchoMinChars = 12;

	/// <summary>
	/// True when the transcription is just the initial prompt being read back (the model does that on audio with no
	/// clear speech). Only the <b>beginning</b> of the prompt counts - nobody says "Ordens para o companheiro" - so a
	/// real order ("colete madeira, pedra...") can never be thrown away by this.
	/// </summary>
	private static bool IsPromptEcho(string text, string prompt)
	{
		if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(prompt))
		{
			return false;
		}
		int limit = Math.Min(text.Length, prompt.Length);
		int run = 0;
		while (run < limit && char.ToLowerInvariant(text[run]) == char.ToLowerInvariant(prompt[run]))
		{
			run++;
		}
		return run >= PromptEchoMinChars;
	}

	private static string Value(string text)
	{
		return string.IsNullOrEmpty(text) ? "?" : text;
	}

	/// <summary>
	/// A Whisper language code ("pt", "en"...). Empty, "auto" or anything with no letters means "let the model detect
	/// it". "pt-BR" becomes "pt": whisper.cpp wants the bare code.
	/// </summary>
	internal static string NormalizeLanguage(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return null;
		}
		string text = value.Trim().ToLowerInvariant();
		if (text == "auto" || text == "automatico" || text == "detection" || text == "deteccao")
		{
			return null;
		}
		int cut = text.IndexOfAny(new char[3] { '-', '_', ' ' });
		if (cut > 0)
		{
			text = text.Substring(0, cut);
		}
		return text.Length == 0 ? null : text;
	}

	/// <summary>Pasta do plugin: a pasta do Companions.dll (onde estao Whisper.net.dll e runtimes\).</summary>
	internal static string PluginDirectory()
	{
		try
		{
			string location = Assembly.GetExecutingAssembly().Location;
			if (!string.IsNullOrEmpty(location))
			{
				string dir = Path.GetDirectoryName(location);
				if (!string.IsNullOrEmpty(dir))
				{
					return dir;
				}
			}
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log?.LogWarning("[STT] Nao consegui descobrir a pasta do plugin pelo Assembly.Location: " + ex.Message);
		}
		return Directory.GetCurrentDirectory();
	}

	/// <summary>Config (relativa a pasta do plugin, ou absoluta) -> caminho completo.</summary>
	internal static string ResolvePath(string pluginDir, string configured, string fallbackRelative)
	{
		string value = string.IsNullOrWhiteSpace(configured) ? fallbackRelative : configured.Trim().Trim('"');
		try
		{
			return Path.IsPathRooted(value) ? Path.GetFullPath(value) : Path.GetFullPath(Path.Combine(pluginDir, value));
		}
		catch (Exception)
		{
			return Path.Combine(pluginDir, fallbackRelative);
		}
	}

	/// <summary>
	/// Aponta o loader nativo do Whisper.net para a NOSSA pasta.
	///
	/// No netstandard2.0 (o alvo que o net472 resolve) o Whisper.net 1.9.1 usa
	/// <c>NativeLibraryLoader.GetRuntimePaths()</c>, que monta a lista de pastas assim:
	/// <code>
	/// var assemblySearchPaths = new[] {
	///     GetSafeDirectoryName(RuntimeOptions.LibraryPath),   // = Path.GetDirectoryName(LibraryPath)
	///     AppDomain.CurrentDomain.RelativeSearchPath,
	///     AppDomain.CurrentDomain.BaseDirectory,              // no Unity = a pasta do JOGO
	///     GetSafeDirectoryName(assemblyLocation),             // = pasta do Whisper.net.dll
	///     ...
	/// };
	/// // e para cada uma procura: &lt;pasta&gt;\runtimes\win-x64\whisper.dll (+ ggml-*.dll)
	/// </code>
	/// Ou seja: <c>RuntimeOptions.LibraryPath</c> e tratado como um <b>caminho de arquivo</b> e o que vale e a pasta
	/// dele (<c>Path.GetDirectoryName</c>). Passando o caminho do Whisper.net.dll (que fica na pasta do plugin), a
	/// PRIMEIRA pasta procurada passa a ser a do plugin - e nao a pasta do jogo (AppContext.BaseDirectory).
	///
	/// Tambem travamos a ordem de runtimes em CPU: so existe <c>runtimes\win-x64</c> no pacote CPU, entao nao faz
	/// sentido sondar CUDA/Vulkan/CoreML/OpenVINO antes (cada sonda pode tentar abrir driver de GPU).
	///
	/// Precisa ser chamado ANTES de qualquer WhisperFactory/uso nativo (a documentacao do RuntimeOptions diz isso).
	/// </summary>
	internal static void PrepareNativeLibraryPath()
	{
		try
		{
			string managedDll = Path.Combine(PluginDirectory(), "Whisper.net.dll");
			RuntimeOptions.LibraryPath = managedDll;
			RuntimeOptions.RuntimeLibraryOrder = new List<RuntimeLibrary> { RuntimeLibrary.Cpu };
			CompanionsPlugin.Log?.LogInfo($"[STT] Loader nativo apontado para a pasta do plugin: RuntimeOptions.LibraryPath=\"{managedDll}\" "
				+ $"(a pasta procurada sera \"{Path.Combine(PluginDirectory(), "runtimes", "win-x64")}\") | ordem de runtimes: CPU");
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log?.LogWarning("[STT] Nao consegui apontar o loader nativo para a pasta do plugin: " + ex.Message);
		}
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
}
