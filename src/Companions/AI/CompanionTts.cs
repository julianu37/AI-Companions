using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using SherpaOnnx;
using UnityEngine;

namespace Companions;

/// <summary>One sentence that is ready to be played. Built on the background worker, played on the main thread.</summary>
internal sealed class TtsChunk
{
	/// <summary>24 kHz mono floats (the Kokoro sample rate), straight from the native side.</summary>
	internal float[] Samples;

	internal int SampleRate;

	/// <summary>Who is talking (used to place the 3D AudioSource). Read only on the main thread.</summary>
	internal GameObject Speaker;

	internal string Text;

	internal bool FromCache;

	internal int SynthesisMs;
}

/// <summary>One line waiting to be synthesized (a whole companion line, split into sentences by the worker).</summary>
internal sealed class TtsRequest
{
	internal GameObject Speaker;

	internal string Text;
}

/// <summary>
/// Fase 3.4: the companion <b>talks</b>. Local text-to-speech with <b>Kokoro-82M</b> through <b>sherpa-onnx</b>
/// (NuGet <c>org.k2fsa.sherpa.onnx</c>, Apache-2.0, in-process - no HTTP, no child process, nothing leaves the
/// machine), the same way <see cref="WhisperStt"/> does the listening half.
///
/// What it says is decided by <see cref="CompanionChatAI"/> (the AI layer) and handed over by
/// <see cref="Speak(CompanionSetup, string)"/>: the model's <c>reply</c> and/or the mod's own localized feedback
/// line - see the two <c>[IA] TtsFalar*</c> settings and the "what is spoken" note below.
///
/// Shape of the service (all of it deliberately mirrors the voice pipeline):
/// <list type="bullet">
/// <item>the <see cref="OfflineTts"/> model is loaded <b>once</b>, lazily, on a background thread
/// (<see cref="Prewarm"/> does it as soon as the player is in the world, so the first line is not slower);</item>
/// <item>synthesis runs on a <b>dedicated background thread</b> (never the main thread, never a thread-pool thread
/// that the game's own work needs); a thread-safe request queue feeds it and a thread-safe ready queue is drained
/// in the plugin's <c>Update</c>;</item>
/// <item>the line is split into <b>sentences</b> and synthesized one at a time: the first sentence starts playing
/// while the next one is still being generated, so the companion begins to talk right away instead of after the
/// whole line;</item>
/// <item>fixed lines are <b>cached</b> (text + voice + speed → samples), so a repeated line is instant;</item>
/// <item>playback is a single 3D <see cref="AudioSource"/> placed at the companion that is talking, one line at a
/// time (a queue), volume from config; the <c>AudioClip</c> is destroyed as soon as it finishes.</item>
/// </list>
///
/// <b>What is spoken</b> (the voice follows the line that is shown in the chat/HUD):
/// <list type="bullet">
/// <item>free conversation: the model's <c>reply</c> - <c>[IA] TtsFalarRespostas</c>;</item>
/// <item>an order: the mod's own localized confirmation ("Atacando o troll.", "Ok: coletar madeira (10).") -
/// <c>[IA] TtsFalarFalasDoMod</c>; with that one off, the model's <c>reply</c> for the order is spoken instead
/// (the reply is never shown for an order - the mod's line wins in the chat - so it is voice-only there);</item>
/// <item>a refusal, an error or "nothing understood": the mod's line, again under <c>[IA] TtsFalarFalasDoMod</c>.</item>
/// </list>
/// Nothing is spoken twice: when both settings are on, the visible line wins and the other text is only logged.
///
/// Like the STT half, every call is null-safe and nothing allocates or blocks per frame: the per-frame work is
/// "is a clip playing?" plus a queue read.
///
/// The native libraries are loaded explicitly before the first use - see <see cref="EnsureNativeLibraries"/>.
/// </summary>
internal static class CompanionTts
{
	/// <summary>Voice used by default: <b>42 = pf_dora</b>, one of the three Brazilian Portuguese voices.</summary>
	internal const int DefaultVoice = 42;

	/// <summary>The Brazilian Portuguese voices of this Kokoro model (confirmed by listening): 42 feminine, 43 and 44 masculine.</summary>
	private static readonly int[] PortugueseVoices = new int[3] { 42, 43, 44 };

	/// <summary>Where the model is expected when [IA] TtsModelo is left at its default (relative to the plugin folder).</summary>
	internal const string DefaultModelRelative = @"tts\kokoro\model.int8.onnx";

	/// <summary>
	/// This Kokoro model is <b>multi-lingual</b> and the native side <b>refuses to load it without a language</b>
	/// ("You are using a multi-lingual Kokoro model ... please pass --kokoro-lexicon or --kokoro-lang"), so this is
	/// never empty. "pt-br" is Brazilian Portuguese; plain "pt" is European Portuguese.
	/// </summary>
	internal const string DefaultLanguage = "pt-br";

	private const string VoicesFileName = "voices.bin";

	private const string TokensFileName = "tokens.txt";

	private const string EspeakDataDirName = "espeak-ng-data";

	private const string SherpaNativeName = "sherpa-onnx-c-api.dll";

	private const string OnnxRuntimeNativeName = "onnxruntime.dll";

	/// <summary>Console command (F5) that speaks a test line without needing the LLM or the microphone.</summary>
	internal const string CommandName = "modia_tts_teste";

	private const string TestPhrase = "Aqui é o seu companheiro. Estou pronto para o que você precisar.";

	/// <summary>A line longer than this is cut: a runaway model must not keep the companion talking for a minute.</summary>
	private const int MaxTextChars = 400;

	/// <summary>Lines waiting for the worker. Beyond this the new line is dropped (the LLM has its own cooldown).</summary>
	private const int MaxPendingRequests = 3;

	/// <summary>Sentences kept in the cache. Small on purpose: it is the fixed lines that repeat, not the model's.</summary>
	private const int MaxCacheEntries = 256;

	/// <summary>A sentence longer than this is broken at a comma/space, so the first audio comes out sooner.</summary>
	private const int MaxPartChars = 180;

	/// <summary>Where the 3D sound sits relative to the companion (roughly its mouth/head).</summary>
	private const float SpeechOffsetY = 1.6f;

	/// <summary>One native synthesis at a time (the worker is single, this is a belt-and-suspenders gate).</summary>
	private static readonly object NativeGate = new object();

	/// <summary>Lines waiting to be synthesized.</summary>
	private static readonly ConcurrentQueue<TtsRequest> _requests = new ConcurrentQueue<TtsRequest>();

	/// <summary>Sentences ready to play, in order.</summary>
	private static readonly ConcurrentQueue<TtsChunk> _ready = new ConcurrentQueue<TtsChunk>();

	/// <summary>Wakes the worker when a line is queued.</summary>
	private static readonly SemaphoreSlim _wake = new SemaphoreSlim(0);

	/// <summary>text+voice+speed → samples. Written by the worker, read by the worker (only the worker touches it).</summary>
	private static readonly Dictionary<string, float[]> _cache = new Dictionary<string, float[]>();

	private static readonly object _loadLock = new object();

	/// <summary>Guards the one-time creation of the synthesis thread.</summary>
	private static readonly object _workerLock = new object();

	private static readonly char[] BreakChars = new char[3] { ',', ';', ':' };

	private static OfflineTts _tts;

	private static bool _loadFailed;

	private static string _loadError;

	private static string _modelPath;

	private static volatile bool _stopping;

	private static volatile Thread _worker;

	private static int _pending;

	private static int _jobs;

	private static int _busy;

	private static long _busyStartedTicks;

	private static bool _warnedSlow;

	private static bool _prewarmRequested;

	private static bool _warnedMissingModel;

	private static bool _warnedNoAudio;

	private static int _nativesReady;

	private static int _nativesFailed;

	private static string _nativesError;

	private static int _commandRegistered;

	// Playback state: main thread only.
	private static Component _source;

	private static object _currentClip;

	private static GameObject _currentSpeaker;

	private static float _currentEndsAt;

	private static bool _playing;

	/// <summary>[IA] TtsLigado.</summary>
	internal static bool Enabled => ModConfig.TtsEnabled == null || ModConfig.TtsEnabled.Value;

	/// <summary>[IA] TtsFalarRespostas: speak the model's reply (conversation, and an order that has no spoken line of its own).</summary>
	internal static bool SpeakReplies => ModConfig.TtsSpeakReplies == null || ModConfig.TtsSpeakReplies.Value;

	/// <summary>[IA] TtsFalarFalasDoMod: speak the mod's own localized feedback line (the one shown in the chat).</summary>
	internal static bool SpeakModLines => ModConfig.TtsSpeakModLines == null || ModConfig.TtsSpeakModLines.Value;

	private static bool CacheEnabled => ModConfig.TtsCache == null || ModConfig.TtsCache.Value;

	/// <summary>[IA] TtsVoz: the Kokoro speaker id (42/43/44 are the pt-BR voices).</summary>
	internal static int Voice
	{
		get
		{
			int value = ModConfig.TtsVoice != null ? ModConfig.TtsVoice.Value : DefaultVoice;
			return value < 0 ? DefaultVoice : value;
		}
	}

	/// <summary>[IA] TtsVelocidade.</summary>
	private static float Speed
	{
		get
		{
			float value = ModConfig.TtsSpeed != null ? ModConfig.TtsSpeed.Value : 1f;
			return Mathf.Clamp(value, 0.5f, 2f);
		}
	}

	/// <summary>[IA] TtsThreads: 4 measured best on a Ryzen 7 5700X (RTF 0.83); more only fights the game for cores.</summary>
	private static int Threads
	{
		get
		{
			int value = ModConfig.TtsThreads != null ? ModConfig.TtsThreads.Value : 4;
			return Mathf.Clamp(value, 1, 16);
		}
	}

	/// <summary>[IA] TtsVolume.</summary>
	private static float Volume
	{
		get
		{
			float value = ModConfig.TtsVolume != null ? ModConfig.TtsVolume.Value : 1f;
			return Mathf.Clamp01(value);
		}
	}

	/// <summary>[IA] TtsIdioma, never empty (the multi-lingual Kokoro model needs it).</summary>
	internal static string Language
	{
		get
		{
			string value = ModConfig.TtsLanguage != null ? ModConfig.TtsLanguage.Value : null;
			return string.IsNullOrWhiteSpace(value) ? DefaultLanguage : value.Trim();
		}
	}

	/// <summary>The model file to load: [IA] TtsModelo (relative to the plugin folder, or absolute).</summary>
	internal static string ModelPath
	{
		get
		{
			string cached = _modelPath;
			if (cached != null)
			{
				return cached;
			}
			string configured = ModConfig.TtsModel != null ? ModConfig.TtsModel.Value : null;
			cached = WhisperStt.ResolvePath(WhisperStt.PluginDirectory(), configured, DefaultModelRelative);
			_modelPath = cached;
			return cached;
		}
	}

	/// <summary>True when a line could be synthesized right now (the model file is there and nothing failed before).</summary>
	internal static bool Ready
	{
		get
		{
			if (_loadFailed)
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
	/// Called once from the plugin's Awake (main thread). Only reads the config and says what the companion's voice
	/// will be; the model itself is loaded by <see cref="Prewarm"/>, off the main thread.
	/// </summary>
	internal static void Init()
	{
		try
		{
			string model = ModelPath;
			string dir = Path.GetDirectoryName(model) ?? "";
			CompanionsPlugin.Log?.LogInfo($"[TTS] Fala do companheiro (Kokoro-82M via sherpa-onnx, CPU, local): "
				+ $"{(Enabled ? "LIGADO" : "desligado ([IA] TtsLigado = false)")} | modelo \"{model}\" ({DescribeFile(model)}) | "
				+ $"voz {Voice}{VoiceName(Voice)} | idioma \"{Language}\" | {Threads} thread(s) | velocidade {Speed:0.##} | "
				+ $"volume {Volume:0.##} | cache {(CacheEnabled ? "ligado" : "desligado")}.");
			CompanionsPlugin.Log?.LogInfo($"[TTS] O que e falado: a resposta do modelo (TtsFalarRespostas={(SpeakReplies ? "sim" : "nao")}) "
				+ $"e as linhas do proprio mod (TtsFalarFalasDoMod={(SpeakModLines ? "sim" : "nao")}). Com os dois ligados a linha que "
				+ "aparece no chat e a que sai em voz; a outra so vai para o log (nada e falado duas vezes).");
			CompanionsPlugin.Log?.LogInfo($"[TTS] Vozes em portugues do Brasil deste modelo: 42 = pf_dora (feminina), 43 = pm_alex (masculina), "
				+ "44 = pm_santa (masculina). As outras sao de outros idiomas (45 soa chines).");
			if (!IsPortugueseVoice(Voice))
			{
				CompanionsPlugin.Log?.LogInfo($"[TTS] A voz {Voice} nao e uma das vozes pt-BR (42/43/44) - o companheiro vai falar com sotaque de outro idioma.");
			}
			CompanionsPlugin.Log?.LogInfo($"[TTS] Vozes (\"{Path.Combine(dir, VoicesFileName)}\", {DescribeFile(Path.Combine(dir, VoicesFileName))}), "
				+ $"tokens (\"{Path.Combine(dir, TokensFileName)}\", {DescribeFile(Path.Combine(dir, TokensFileName))}) e "
				+ $"espeak-ng-data (\"{Path.Combine(dir, EspeakDataDirName)}\") - tudo procurado na MESMA pasta do modelo.");
			if (string.IsNullOrWhiteSpace(ModConfig.TtsLanguage != null ? ModConfig.TtsLanguage.Value : null))
			{
				CompanionsPlugin.Log?.LogInfo($"[TTS] [IA] TtsIdioma esta vazio - usando \"{DefaultLanguage}\": este Kokoro e multi-lingue e a lib nativa "
					+ "RECUSA carregar sem um idioma (\"You are using a multi-lingual Kokoro model ... please pass --kokoro-lexicon or --kokoro-lang\").");
			}
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log?.LogWarning("[TTS] Init falhou: " + ex);
		}
	}

	/// <summary>
	/// Loads the model in the background, before the first line needs it. Called once the player is in the world
	/// (Tick); does nothing after the first call. Same idea as <see cref="WhisperStt.Prewarm"/>.
	/// </summary>
	internal static void Prewarm()
	{
		if (!Enabled)
		{
			return;
		}
		Task.Run(delegate
		{
			try
			{
				if (TryGetTts(out OfflineTts _, out string error))
				{
					CompanionsPlugin.Log?.LogInfo("[TTS] Modelo pre-carregado em segundo plano (a primeira fala nao vai esperar por ele).");
				}
				else
				{
					CompanionsPlugin.Log?.LogWarning("[TTS] Nao consegui pre-carregar o modelo: " + error);
				}
			}
			catch (Exception ex)
			{
				CompanionsPlugin.Log?.LogWarning("[TTS] Excecao ao pre-carregar o modelo: " + ex);
			}
		});
	}

	/// <summary>
	/// The companion says something. Called on the main thread (from the AI layer); returns immediately - the line is
	/// queued and synthesized on the background worker.
	/// </summary>
	internal static void Speak(CompanionSetup companion, string text)
	{
		Speak(companion != null ? companion.gameObject : null, text);
	}

	/// <summary>
	/// Same, for whoever is talking (a companion, or the player for the console test). <paramref name="talker"/> is
	/// only used to place the 3D sound, so null is accepted (the sound then stays where it was).
	/// </summary>
	internal static void Speak(GameObject talker, string text)
	{
		if (!Enabled || string.IsNullOrEmpty(text))
		{
			return;
		}
		if (Player.m_localPlayer == null)
		{
			return;
		}
		string trimmed = text.Trim();
		if (trimmed.Length == 0)
		{
			return;
		}
		if (trimmed.Length > MaxTextChars)
		{
			trimmed = trimmed.Substring(0, MaxTextChars);
		}
		if (!Ready)
		{
			if (!_warnedMissingModel)
			{
				_warnedMissingModel = true;
				CompanionsPlugin.Log?.LogWarning($"[TTS] O modelo do TTS nao esta em \"{ModelPath}\" - o companheiro nao vai falar. "
					+ "Coloque a pasta tts\\kokoro do modelo (model.int8.onnx + voices.bin + tokens.txt + espeak-ng-data) no plugin ou ajuste [IA] TtsModelo.");
				CompanionChatAI.Announce(ModLocalization.Loc("hc_ai_tts_unavailable"));
			}
			return;
		}
		if (Interlocked.Increment(ref _pending) > MaxPendingRequests)
		{
			Interlocked.Decrement(ref _pending);
			CompanionsPlugin.Log?.LogInfo($"[TTS] Ja ha {MaxPendingRequests} falas na fila - esta foi descartada: \"{trimmed}\"");
			return;
		}
		_requests.Enqueue(new TtsRequest
		{
			Speaker = talker,
			Text = trimmed
		});
		EnsureWorker();
		try
		{
			_wake.Release();
		}
		catch (SemaphoreFullException)
		{
			// impossible with the default max, but a dropped line must never throw at the caller
		}
		CompanionsPlugin.Log?.LogInfo($"[TTS] Fala enfileirada: \"{trimmed}\" ({Volatile.Read(ref _pending)} pedido(s) na fila).");
	}

	/// <summary>
	/// Called every frame from the plugin's Update. Never blocks and never allocates when idle: it warms the model
	/// once the player is in the world, watches a synthesis that never comes back and drives the playback queue.
	/// </summary>
	internal static void Tick()
	{
		try
		{
			if (!_prewarmRequested && Enabled && Player.m_localPlayer != null)
			{
				_prewarmRequested = true;
				Prewarm();
			}
			Watchdog();
			Playback();
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log?.LogWarning("[TTS] Tick falhou: " + ex);
		}
	}

	/// <summary>Stops the worker and frees the native model (plugin shutdown, main thread).</summary>
	internal static void Shutdown()
	{
		_stopping = true;
		try
		{
			_wake.Release();
		}
		catch (Exception)
		{
			// the worker may already be gone
		}
		StopCurrent();
		if (_source != null)
		{
			try
			{
				UnityEngine.Object.Destroy(_source.gameObject);
			}
			catch (Exception ex)
			{
				CompanionsPlugin.Log?.LogDebug("[TTS] Falha ao destruir o AudioSource: " + ex.Message);
			}
			_source = null;
		}
		if (Volatile.Read(ref _busy) != 0)
		{
			// disposing the model under a running synthesis would take the native library down with it
			CompanionsPlugin.Log?.LogWarning("[TTS] Ainda havia uma sintese em andamento no descarregamento - o modelo ficou carregado.");
			return;
		}
		lock (_loadLock)
		{
			try
			{
				_tts?.Dispose();
			}
			catch (Exception ex)
			{
				CompanionsPlugin.Log?.LogDebug("[TTS] Falha ao liberar o modelo do Kokoro: " + ex.Message);
			}
			_tts = null;
		}
	}

	// ---------------------------------------------------------------- worker (background thread)

	/// <summary>Creates the synthesis worker once. Cheap to call on every line.</summary>
	private static void EnsureWorker()
	{
		if (_worker != null)
		{
			return;
		}
		lock (_workerLock)
		{
			if (_worker != null)
			{
				return;
			}
			Thread worker = new Thread(WorkerLoop)
			{
				IsBackground = true,
				Name = "ModIA-CompanionTts"
			};
			_worker = worker;
			worker.Start();
		}
	}

	/// <summary>
	/// The worker: takes lines in order, splits each into sentences and synthesizes them one at a time, publishing
	/// every sentence as soon as it is ready (that is what lets the first one play while the next is generated).
	/// Anything that fails is logged; no exception ever escapes to the Unity side.
	/// </summary>
	private static void WorkerLoop()
	{
		CompanionsPlugin.Log?.LogInfo($"[TTS] Thread de sintese iniciada (thread {Thread.CurrentThread.ManagedThreadId}).");
		while (!_stopping)
		{
			try
			{
				_wake.Wait(500);
			}
			catch (Exception)
			{
				return;
			}
			while (_requests.TryDequeue(out TtsRequest request))
			{
				try
				{
					Process(request);
				}
				catch (Exception ex)
				{
					CompanionsPlugin.Log?.LogError("[TTS] FALHA ao sintetizar: " + ex);
				}
				finally
				{
					Interlocked.Decrement(ref _pending);
				}
			}
		}
		CompanionsPlugin.Log?.LogInfo("[TTS] Thread de sintese encerrada.");
	}

	/// <summary>
	/// One whole line, already off the main thread: split into sentences, synthesize each one (or take it from the
	/// cache) and publish it for playback. The per-sentence log line carries the <b>RTF</b> (synthesis time divided
	/// by the audio time), which is what tells whether the voice can keep up.
	/// </summary>
	private static void Process(TtsRequest request)
	{
		if (!TryGetTts(out OfflineTts tts, out string error))
		{
			CompanionsPlugin.Log?.LogWarning($"[TTS] Nao consegui falar \"{request.Text}\": {error}");
			return;
		}
		int job = Interlocked.Increment(ref _jobs);
		int voice = EffectiveVoice(tts);
		float speed = Speed;
		bool cache = CacheEnabled;
		string[] sentences = SplitSentences(request.Text);
		if (sentences.Length == 0)
		{
			return;
		}
		CompanionsPlugin.Log?.LogInfo($"[TTS] Fala #{job}: {sentences.Length} frase(s), voz {voice}, velocidade {speed:0.##}, "
			+ $"{Threads} thread(s), cache {(cache ? "ligado" : "desligado")} - sintetizando em thread de fundo, o jogo nao para.");
		long startedTicks = DateTime.UtcNow.Ticks;
		long totalSamples = 0;
		int totalRate = 24000;
		for (int i = 0; i < sentences.Length; i++)
		{
			string sentence = sentences[i];
			string key = CacheKey(voice, speed, sentence);
			float[] samples = null;
			int synthesisMs = 0;
			bool fromCache = false;
			if (cache && TryGetCached(key, out float[] cached))
			{
				samples = cached;
				fromCache = true;
			}
			if (samples == null)
			{
				lock (NativeGate)
				{
					Interlocked.Exchange(ref _busyStartedTicks, DateTime.UtcNow.Ticks);
					Volatile.Write(ref _busy, 1);
					try
					{
						Stopwatch watch = Stopwatch.StartNew();
						// NOTE: OfflineTtsGeneratedAudio has a Dispose() method but does NOT implement
						// IDisposable (checked in the package metadata), so it cannot go in a using block -
						// the try/finally below is the only way to free the native buffer.
						OfflineTtsGeneratedAudio audio = tts.Generate(sentence, speed, voice);
						try
						{
							watch.Stop();
							synthesisMs = (int)watch.ElapsedMilliseconds;
							if (audio != null && audio.NumSamples > 0)
							{
								samples = audio.Samples;
							}
						}
						finally
						{
							audio?.Dispose();
						}
					}
					finally
					{
						Volatile.Write(ref _busy, 0);
						Interlocked.Exchange(ref _busyStartedTicks, 0L);
					}
				}
				if (samples == null || samples.Length == 0)
				{
					CompanionsPlugin.Log?.LogWarning($"[TTS] A frase {i + 1}/{sentences.Length} voltou vazia: \"{sentence}\"");
					continue;
				}
				if (cache)
				{
					StoreCached(key, samples);
				}
			}
			int rate = tts.SampleRate;
			if (rate <= 0)
			{
				rate = 24000;
			}
			totalRate = rate;
			double audioSeconds = samples.Length / (double)rate;
			totalSamples += samples.Length;
			double rtf = audioSeconds > 0.0 ? synthesisMs / 1000.0 / audioSeconds : 0.0;
			string timing = fromCache
				? "CACHE (0 ms)"
				: $"{synthesisMs} ms, RTF {rtf:0.00}{(rtf > 0.0 && rtf < 1.0 ? " (mais rapido que o tempo real)" : "")}";
			CompanionsPlugin.Log?.LogInfo($"[TTS] Frase {i + 1}/{sentences.Length}: {timing} para {audioSeconds:0.00} s de audio "
				+ $"({rate} Hz mono, {samples.Length} amostras, {samples.Length * 4 / 1024} KB) | \"{sentence}\"");
			_ready.Enqueue(new TtsChunk
			{
				Samples = samples,
				SampleRate = rate,
				Speaker = request.Speaker,
				Text = sentence,
				FromCache = fromCache,
				SynthesisMs = synthesisMs
			});
		}
		int totalElapsedMs = (int)((DateTime.UtcNow.Ticks - startedTicks) / TimeSpan.TicksPerMillisecond);
		double totalSeconds = totalSamples / (double)totalRate;
		CompanionsPlugin.Log?.LogInfo($"[TTS] Fala #{job} pronta: {totalElapsedMs} ms no total, {totalSeconds:0.00} s de audio "
			+ $"({totalSamples} amostras), RTF {(totalSeconds > 0.0 ? totalElapsedMs / 1000.0 / totalSeconds : 0.0):0.00} | \"{request.Text}\"");
	}

	/// <summary>
	/// The voice actually used: <c>[IA] TtsVoz</c>, but never outside the loaded model - a speaker id the model does
	/// not have would make the native call fail. <see cref="DefaultVoice"/> (pf_dora) is the fallback.
	/// </summary>
	private static int EffectiveVoice(OfflineTts tts)
	{
		int voice = Voice;
		if (tts != null && (voice < 0 || voice >= tts.NumSpeakers))
		{
			return tts.NumSpeakers > DefaultVoice ? DefaultVoice : 0;
		}
		return voice;
	}

	/// <summary>
	/// The Kokoro model, loaded <b>once</b> and kept. Safe to call from any thread; the first caller does the load
	/// (and the native-library setup), the others wait for it.
	/// </summary>
	internal static bool TryGetTts(out OfflineTts tts, out string error)
	{
		lock (_loadLock)
		{
			tts = _tts;
			if (tts != null)
			{
				error = null;
				return true;
			}
			if (_loadFailed)
			{
				error = _loadError;
				return false;
			}
			string modelPath = ModelPath;
			if (!File.Exists(modelPath))
			{
				_loadFailed = true;
				_loadError = $"modelo nao encontrado em \"{modelPath}\"";
				CompanionsPlugin.Log?.LogError($"[TTS] {_loadError}. Coloque a pasta tts\\kokoro do modelo no plugin ou ajuste [IA] TtsModelo.");
				error = _loadError;
				return false;
			}
			try
			{
				EnsureNativeLibraries();
				string dir = Path.GetDirectoryName(modelPath) ?? "";
				int threads = Threads;
				string language = Language;
				// The struct constructors fill every string with "" and every sub-config with its own defaults; the
				// explicit assignments below make that independent of the language version (a struct left at its
				// default value would marshal null strings into the native call).
				OfflineTtsConfig config = new OfflineTtsConfig();
				config.Model = new OfflineTtsModelConfig();
				config.Model.Kokoro = new OfflineTtsKokoroModelConfig();
				config.RuleFsts = "";
				config.RuleFars = "";
				config.MaxNumSentences = 1;
				config.SilenceScale = 0.2f;
				config.Model.NumThreads = threads;
				config.Model.Debug = 0;
				config.Model.Provider = "cpu";
				config.Model.Kokoro.Model = modelPath;
				config.Model.Kokoro.Voices = Path.Combine(dir, VoicesFileName);
				config.Model.Kokoro.Tokens = Path.Combine(dir, TokensFileName);
				config.Model.Kokoro.DataDir = Path.Combine(dir, EspeakDataDirName);
				config.Model.Kokoro.Lang = language;
				Stopwatch watch = Stopwatch.StartNew();
				tts = new OfflineTts(config);
				watch.Stop();
				if (tts.NumSpeakers <= 0)
				{
					throw new InvalidOperationException($"o modelo carregou mas nao tem vozes (NumSpeakers={tts.NumSpeakers})");
				}
				_tts = tts;
				CompanionsPlugin.Log?.LogInfo($"[TTS] Modelo CARREGADO em {watch.ElapsedMilliseconds} ms e MANTIDO carregado: \"{modelPath}\" "
					+ $"(sherpa-onnx {SherpaOnnxVersion()}, Kokoro-82M, CPU, {threads} thread(s), idioma \"{language}\") | "
					+ $"{tts.NumSpeakers} vozes | saida {tts.SampleRate} Hz mono.");
				if (Voice >= tts.NumSpeakers)
				{
					CompanionsPlugin.Log?.LogWarning($"[TTS] [IA] TtsVoz = {Voice} nao existe neste modelo ({tts.NumSpeakers} vozes) - "
						+ $"a voz {DefaultVoice} (pf_dora) vai ser usada. As vozes pt-BR sao 42, 43 e 44.");
				}
				error = null;
				return true;
			}
			catch (Exception ex)
			{
				_loadFailed = true;
				_loadError = ex.Message;
				CompanionsPlugin.Log?.LogError("[TTS] FALHOU ao carregar o modelo do Kokoro (o companheiro nao vai falar). " + ex);
				error = _loadError;
				tts = null;
				return false;
			}
		}
	}

	// ---------------------------------------------------------------- native libraries

	/// <summary>
	/// Makes the sherpa-onnx native libraries reachable from the <c>DllImport</c> calls.
	///
	/// The managed binding (<c>sherpa-onnx.dll</c>, netstandard2.0) is a plain <c>[DllImport("sherpa-onnx-c-api")]</c>
	/// - there is <b>no</b> loader helper inside it (unlike Whisper.net, which resolves its own path), so the search
	/// order of the process decides where Windows looks. In Unity/Mono that order is the game folder first
	/// (<c>AppContext.BaseDirectory</c> is <c>valheim_Data\Managed</c>, not the plugin folder), so a bare name would
	/// not be found next to <c>Companions.dll</c>.
	///
	/// What is done here, before the very first native use:
	/// <list type="number">
	/// <item><c>SetDllDirectory(pastaDosNativos)</c> - the Win32 call that puts a folder on the search path (exactly
	/// the remedy the task asked for);</item>
	/// <item><c>LoadLibraryEx(caminhoAbsoluto, NULL, LOAD_WITH_ALTERED_SEARCH_PATH)</c> for <b>onnxruntime.dll</b>
	/// (the dependency) and then <b>sherpa-onnx-c-api.dll</b> - an explicit preload by full path. The flag makes
	/// Windows look for the dependencies <b>in the DLL's own folder</b>, so <c>onnxruntime.dll</c> is found even
	/// without the search-path change;</item>
	/// <item><c>SetDllDirectory(NULL)</c> right after, restoring the default search order - the preload already put
	/// the modules in the process, and once a module is loaded <c>DllImport("sherpa-onnx-c-api")</c> resolves to the
	/// already-loaded module by name, with no file search at all. Nothing is left changed for the game or for other
	/// mods.</item>
	/// </list>
	///
	/// The DLLs live in the package layout, <c>&lt;plugin&gt;\runtimes\win-x64\native\</c> (the build copies them
	/// there, see the DeploySherpaOnnx target); the plugin folder itself is accepted as a fallback.
	/// </summary>
	private static void EnsureNativeLibraries()
	{
		if (Volatile.Read(ref _nativesReady) != 0)
		{
			return;
		}
		if (Volatile.Read(ref _nativesFailed) != 0)
		{
			throw new InvalidOperationException("bibliotecas nativas do sherpa-onnx indisponiveis: " + _nativesError);
		}
		string pluginDir = WhisperStt.PluginDirectory();
		string nativeDir = Path.Combine(pluginDir, "runtimes", "win-x64", "native");
		string sherpa = FirstExisting(Path.Combine(nativeDir, SherpaNativeName), Path.Combine(pluginDir, SherpaNativeName));
		string onnx = FirstExisting(Path.Combine(nativeDir, OnnxRuntimeNativeName), Path.Combine(pluginDir, OnnxRuntimeNativeName));
		if (sherpa == null || onnx == null)
		{
			Volatile.Write(ref _nativesFailed, 1);
			_nativesError = $"nao encontrei {SherpaNativeName} e/ou {OnnxRuntimeNativeName} em \"{nativeDir}\" nem em \"{pluginDir}\"";
			CompanionsPlugin.Log?.LogError($"[TTS] {_nativesError}. O build copia as nativas do pacote (target DeploySherpaOnnx).");
			throw new InvalidOperationException(_nativesError);
		}
		string searchDir = Path.GetDirectoryName(sherpa);
		CompanionsPlugin.Log?.LogInfo($"[TTS] Carregando as bibliotecas nativas do sherpa-onnx de \"{searchDir}\": {SherpaNativeName} + {OnnxRuntimeNativeName}.");
		try
		{
			SetDllDirectory(searchDir);
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log?.LogWarning("[TTS] SetDllDirectory falhou (sigo com o preload por caminho absoluto): " + ex.Message);
		}
		try
		{
			PreloadNative(onnx);
			PreloadNative(sherpa);
		}
		finally
		{
			try
			{
				SetDllDirectory(null);
			}
			catch (Exception)
			{
				// restoring the search path is a courtesy; failing to do it must not stop the TTS
			}
		}
		Volatile.Write(ref _nativesReady, 1);
	}

	private static void PreloadNative(string path)
	{
		IntPtr handle = LoadLibraryEx(path, IntPtr.Zero, LoadWithAlteredSearchPath);
		if (handle == IntPtr.Zero)
		{
			int code = Marshal.GetLastWin32Error();
			throw new InvalidOperationException($"LoadLibraryEx falhou para \"{path}\" (erro Win32 {code})");
		}
		CompanionsPlugin.Log?.LogInfo($"[TTS] Nativa carregada: \"{path}\" -> {ModulePathOf(handle)} (handle 0x{handle.ToInt64():X})");
	}

	private static string ModulePathOf(IntPtr handle)
	{
		try
		{
			StringBuilder buffer = new StringBuilder(1024);
			uint length = GetModuleFileName(handle, buffer, (uint)buffer.Capacity);
			return length > 0 ? buffer.ToString() : "(caminho desconhecido)";
		}
		catch (Exception ex)
		{
			return "(erro ao ler o caminho: " + ex.Message + ")";
		}
	}

	private static string FirstExisting(params string[] candidates)
	{
		for (int i = 0; i < candidates.Length; i++)
		{
			try
			{
				if (File.Exists(candidates[i]))
				{
					return candidates[i];
				}
			}
			catch (Exception)
			{
				// an unreadable candidate is simply not it
			}
		}
		return null;
	}

	// ---------------------------------------------------------------- playback (main thread)

	/// <summary>
	/// The per-frame playback step. One line at a time: while a clip is playing its source follows the companion;
	/// when it ends the next ready sentence starts in the same frame (no gap between the sentences of a line).
	/// </summary>
	private static void Playback()
	{
		// Time.unscaledTime, not Time.time: an open menu pauses the game clock but not the AudioSource, and the
		// queue must keep moving with the sound that is really playing.
		if (_playing && UnityEngine.Time.unscaledTime >= _currentEndsAt)
		{
			StopCurrent();
		}
		if (!_playing)
		{
			while (_ready.TryDequeue(out TtsChunk chunk))
			{
				if (chunk.Speaker == null)
				{
					CompanionsPlugin.Log?.LogDebug("[TTS] Descartando uma fala: o companheiro nao existe mais.");
					continue;
				}
				if (!StartChunk(chunk))
				{
					continue;
				}
				break;
			}
		}
		if (!_playing)
		{
			return;
		}
		if (_currentSpeaker == null)
		{
			CompanionsPlugin.Log?.LogDebug("[TTS] O companheiro que estava falando desapareceu - parando a fala.");
			StopCurrent();
			return;
		}
		_source.transform.position = _currentSpeaker.transform.position + Vector3.up * SpeechOffsetY;
	}

	/// <summary>Turns one ready sentence into an AudioClip and starts playing it. Returns false when it could not.</summary>
	private static bool StartChunk(TtsChunk chunk)
	{
		if (chunk.Samples == null || chunk.Samples.Length == 0 || chunk.SampleRate <= 0)
		{
			return false;
		}
		if (!Audio.Available)
		{
			if (!_warnedNoAudio)
			{
				_warnedNoAudio = true;
				CompanionsPlugin.Log?.LogWarning("[TTS] Nao consegui acessar o audio do Unity (UnityEngine.AudioModule): o companheiro nao vai falar. " + Audio.Error);
			}
			return false;
		}
		if (!EnsureSource())
		{
			return false;
		}
		object clip = null;
		try
		{
			clip = Audio.CreateClip("ModIA-CompanionTts", chunk.Samples, chunk.SampleRate);
			if (clip == null)
			{
				CompanionsPlugin.Log?.LogWarning("[TTS] Nao consegui criar o AudioClip da fala.");
				return false;
			}
			Audio.SetClip(_source, clip);
			Audio.SetVolume(_source, Volume);
			Audio.SetMaxDistance(_source, CullDistance);
			Audio.Play(_source);
			_currentClip = clip;
			_currentSpeaker = chunk.Speaker;
			_currentEndsAt = UnityEngine.Time.unscaledTime + chunk.Samples.Length / (float)chunk.SampleRate;
			_playing = true;
			CompanionsPlugin.Log?.LogInfo($"[TTS] Tocando {(chunk.FromCache ? "(do cache) " : "")}\"{chunk.Text}\" "
				+ $"({chunk.Samples.Length / (float)chunk.SampleRate:0.00} s, {chunk.SampleRate} Hz) na posicao de \"{chunk.Speaker.name}\".");
			return true;
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log?.LogWarning("[TTS] Falha ao tocar a fala: " + ex);
			if (clip != null)
			{
				DestroyClip(clip);
			}
			return false;
		}
	}

	/// <summary>Stops the clip that is playing and frees it (an AudioClip that is never destroyed is a leak).</summary>
	private static void StopCurrent()
	{
		_playing = false;
		_currentSpeaker = null;
		_currentEndsAt = 0f;
		if (_source != null)
		{
			try
			{
				Audio.SetClip(_source, null);
				Audio.Stop(_source);
			}
			catch (Exception ex)
			{
				CompanionsPlugin.Log?.LogDebug("[TTS] Falha ao parar o AudioSource: " + ex.Message);
			}
		}
		if (_currentClip != null)
		{
			DestroyClip(_currentClip);
			_currentClip = null;
		}
	}

	private static void DestroyClip(object clip)
	{
		try
		{
			Audio.Destroy(clip);
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log?.LogDebug("[TTS] Falha ao destruir o AudioClip: " + ex.Message);
		}
	}

	/// <summary>The one 3D AudioSource, created on first use and kept for the session.</summary>
	private static bool EnsureSource()
	{
		if (_source != null)
		{
			return true;
		}
		try
		{
			GameObject holder = new GameObject("ModIA-CompanionTts");
			UnityEngine.Object.DontDestroyOnLoad(holder);
			Component source = Audio.AddSource(holder);
			if (source == null)
			{
				UnityEngine.Object.Destroy(holder);
				return false;
			}
			Audio.Configure(source, CullDistance);
			_source = source;
			CompanionsPlugin.Log?.LogInfo("[TTS] AudioSource 3D criado: a fala sai na posicao do companheiro (nao no centro da tela).");
			return true;
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log?.LogWarning("[TTS] Falha ao criar o AudioSource: " + ex);
			return false;
		}
	}

	/// <summary>How far the voice is still audible (the same cull distance the speech bubble uses).</summary>
	private static float CullDistance
	{
		get
		{
			float value = ModConfig.SpeechCullDistance != null ? ModConfig.SpeechCullDistance.Value : 20f;
			return Mathf.Clamp(value, 5f, 120f);
		}
	}

	// ---------------------------------------------------------------- cache

	private static string CacheKey(int voice, float speed, string text)
	{
		return voice + "|" + speed.ToString("0.##") + "|" + text;
	}

	private static bool TryGetCached(string key, out float[] samples)
	{
		lock (_cache)
		{
			return _cache.TryGetValue(key, out samples);
		}
	}

	private static void StoreCached(string key, float[] samples)
	{
		lock (_cache)
		{
			if (_cache.Count >= MaxCacheEntries)
			{
				_cache.Clear();
				CompanionsPlugin.Log?.LogInfo($"[TTS] Cache de falas cheio ({MaxCacheEntries}) - esvaziado.");
			}
			_cache[key] = samples;
		}
	}

	// ---------------------------------------------------------------- text

	/// <summary>
	/// Splits a line into sentences (on <c>. ! ? …</c> and line breaks) so the first piece can be spoken while the
	/// rest is still being generated; a piece that is still long is broken at a comma/space. The Kokoro model itself
	/// could split, but doing it here is what makes the companion start talking sooner.
	///
	/// Careful with two things a naive split gets wrong: a run of terminators is <b>one</b> break ("Talvez... nao
	/// sei." must not become "Talvez.", ".", ".", "nao sei."), and a dot between digits is a decimal point, not the
	/// end of a sentence ("1.5 de altura").
	/// </summary>
	private static string[] SplitSentences(string text)
	{
		List<string> parts = new List<string>(4);
		int start = 0;
		int i = 0;
		while (i < text.Length)
		{
			char c = text[i];
			if (!IsTerminator(c))
			{
				i++;
				continue;
			}
			if (c == '.' && IsDigit(text, i - 1) && IsDigit(text, i + 1))
			{
				// a decimal point ("1.5"), not the end of a sentence
				i++;
				continue;
			}
			// one break for the whole run of terminators (and the blanks right after it): "...", "!?", ". "
			int end = i + 1;
			while (end < text.Length && (IsTerminator(text[end]) || text[end] == ' ' || text[end] == '\t'))
			{
				end++;
			}
			AddPart(parts, text, start, end);
			start = end;
			i = end;
		}
		if (start < text.Length)
		{
			AddPart(parts, text, start, text.Length);
		}
		if (parts.Count == 0)
		{
			string whole = text.Trim();
			if (whole.Length > 0)
			{
				parts.Add(whole);
			}
		}
		return parts.ToArray();
	}

	private static bool IsTerminator(char c)
	{
		return c == '.' || c == '!' || c == '?' || c == '…' || c == '\n' || c == '\r';
	}

	private static bool IsDigit(string text, int index)
	{
		return index >= 0 && index < text.Length && text[index] >= '0' && text[index] <= '9';
	}

	private static void AddPart(List<string> parts, string text, int from, int to)
	{
		string part = text.Substring(from, to - from).Trim();
		while (part.Length > MaxPartChars)
		{
			int cut = part.LastIndexOfAny(BreakChars, MaxPartChars);
			if (cut <= 0)
			{
				cut = part.LastIndexOf(' ', MaxPartChars);
			}
			if (cut <= 0)
			{
				break;
			}
			string head = part.Substring(0, cut).Trim();
			if (head.Length > 0)
			{
				parts.Add(head);
			}
			part = part.Substring(cut + 1).Trim();
		}
		if (part.Length > 0)
		{
			parts.Add(part);
		}
	}

	// ---------------------------------------------------------------- diagnostics

	/// <summary>Logs a synthesis that is taking far too long (a native call that never returns) - the game is not blocked.</summary>
	private static void Watchdog()
	{
		if (Volatile.Read(ref _busy) == 0)
		{
			_warnedSlow = false;
			return;
		}
		long started = Interlocked.Read(ref _busyStartedTicks);
		if (started == 0L || _warnedSlow)
		{
			return;
		}
		double seconds = (DateTime.UtcNow.Ticks - started) / (double)TimeSpan.TicksPerSecond;
		if (seconds < 30.0)
		{
			return;
		}
		_warnedSlow = true;
		CompanionsPlugin.Log?.LogWarning($"[TTS] Uma sintese esta demorando {seconds:0} s sem responder. O jogo nao esta travado "
			+ "(a sintese roda em thread de fundo), mas a fala vai sair atrasada - veja [IA] TtsThreads e o tamanho do modelo.");
	}

	private static bool IsPortugueseVoice(int voice)
	{
		for (int i = 0; i < PortugueseVoices.Length; i++)
		{
			if (PortugueseVoices[i] == voice)
			{
				return true;
			}
		}
		return false;
	}

	private static string VoiceName(int voice)
	{
		switch (voice)
		{
		case 42:
			return " (pf_dora, feminina)";
		case 43:
			return " (pm_alex, masculina)";
		case 44:
			return " (pm_santa, masculina)";
		default:
			return "";
		}
	}

	private static string SherpaOnnxVersion()
	{
		try
		{
			return typeof(OfflineTts).Assembly.GetName().Version?.ToString() ?? "?";
		}
		catch (Exception)
		{
			return "?";
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
			return $"{info.Length / 1024} KB";
		}
		catch (Exception ex)
		{
			return "erro ao ler: " + ex.Message;
		}
	}

	// ---------------------------------------------------------------- console command (diagnostic)

	/// <summary>
	/// Postfix on <c>Console.Awake</c> (the same hook <see cref="WhisperSpike"/> uses) to register
	/// <c>modia_tts_teste [texto]</c>: it speaks the given text (or a default phrase) through the whole pipeline -
	/// model, synthesis, playback - without needing the LLM or the microphone. Handy to check the voice, the volume
	/// and the RTF in the log.
	/// </summary>
	[HarmonyPatch(typeof(Console), "Awake")]
	private static class ConsoleAwake_Patch
	{
		private static void Postfix(Console __instance)
		{
			CompanionTts.RegisterConsoleCommand(__instance);
		}
	}

	private static void RegisterConsoleCommand(Console console)
	{
		if (_commandRegistered != 0)
		{
			return;
		}
		try
		{
			new Terminal.ConsoleCommand(
				CommandName,
				"ModIA: fala um texto com a voz do companheiro (TTS local, Kokoro). Sem argumento fala uma frase de teste.",
				(Terminal.ConsoleEvent)delegate(Terminal.ConsoleEventArgs args)
				{
					RunTestCommand(args);
				},
				false,
				false,
				false,
				false,
				false,
				false,
				null,
				false,
				false,
				false);
			AccessTools.Method(typeof(Terminal), "updateCommandList")?.Invoke(console, null);
			Interlocked.Exchange(ref _commandRegistered, 1);
			CompanionsPlugin.Log?.LogInfo($"[TTS] Comando de console \"{CommandName} [texto]\" registrado (digite no console do jogo, F5).");
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log?.LogWarning($"[TTS] Nao consegui registrar o comando de console \"{CommandName}\": " + ex);
		}
	}

	private static void RunTestCommand(Terminal.ConsoleEventArgs args)
	{
		string text = TestPhrase;
		try
		{
			if (args != null && args.Length > 1)
			{
				StringBuilder builder = new StringBuilder();
				for (int i = 1; i < args.Length; i++)
				{
					if (i > 1)
					{
						builder.Append(' ');
					}
					builder.Append(args[i]);
				}
				string custom = builder.ToString().Trim();
				if (custom.Length > 0)
				{
					text = custom;
				}
			}
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log?.LogDebug("[TTS] Nao consegui ler o texto do comando: " + ex.Message);
		}
		GameObject talker = null;
		CompanionSetup speaker = CompanionChatAI.PickSpeaker();
		if (speaker != null)
		{
			talker = speaker.gameObject;
		}
		else if (Player.m_localPlayer != null)
		{
			talker = Player.m_localPlayer.gameObject;
		}
		CompanionsPlugin.Log?.LogInfo($"[TTS] Teste do TTS pelo comando de console: \"{text}\"");
		Speak(talker, text);
	}

	// ---------------------------------------------------------------- native entry points

	private const uint LoadWithAlteredSearchPath = 0x00000008u;

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern bool SetDllDirectory(string lpPathName);

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hFile, uint dwFlags);

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern uint GetModuleFileName(IntPtr hModule, StringBuilder lpFilename, uint nSize);

	/// <summary>
	/// The whole <c>UnityEngine.AudioModule</c> surface this service uses - <c>AudioClip</c> and <c>AudioSource</c> -
	/// reached by reflection, looked up <b>once</b>.
	///
	/// Why not a plain reference: the same reason <see cref="VoiceInput"/> reaches <c>Microphone</c>/<c>AudioClip</c>
	/// this way. This project compiles for net472 and now references two netstandard2.0 packages (Whisper.net and
	/// sherpa-onnx), which pin the netstandard <b>2.0</b> facade; every Unity 6 module is built against netstandard
	/// <b>2.1</b>, and a module outside the game's own reference closure is a hard CS1705 against that older facade
	/// (AudioModule is not in the closure - CoreModule/AIModule/... are, through assembly_valheim, which is why they
	/// can be referenced directly). Pointing netstandard at the game's 2.1 facade fixes CS1705 but breaks
	/// <c>AudioClip.GetData(Span&lt;float&gt;, int)</c> the other way (CS7069: the .NET Framework reference assemblies
	/// have no Span). Looking the members up at runtime is the only option that keeps the build clean without
	/// dragging a second BCL into the game's Mono.
	///
	/// The calls here happen once per line (never per frame): create the clip, set clip/volume/distance, Play/Stop.
	/// The only per-frame work in the service is the <c>transform.position</c> of the source, which goes through
	/// <c>Component.transform</c> (CoreModule, referenced directly).
	/// </summary>
	private static class Audio
	{
		private const string AudioClipTypeName = "UnityEngine.AudioClip";

		private const string AudioSourceTypeName = "UnityEngine.AudioSource";

		private static readonly Type[] CreateSignature = new Type[5]
		{
			typeof(string),
			typeof(int),
			typeof(int),
			typeof(int),
			typeof(bool)
		};

		private static readonly Type[] SetDataSignature = new Type[2]
		{
			typeof(float[]),
			typeof(int)
		};

		private static bool _resolved;

		private static int _attempts;

		private static string _error = "nao resolvido";

		private static Type _clipType;

		private static Type _sourceType;

		private static MethodInfo _create;

		private static MethodInfo _setData;

		private static PropertyInfo _clip;

		private static PropertyInfo _volume;

		private static PropertyInfo _spatialBlend;

		private static PropertyInfo _minDistance;

		private static PropertyInfo _maxDistance;

		private static PropertyInfo _loop;

		private static PropertyInfo _playOnAwake;

		private static MethodInfo _play;

		private static MethodInfo _stop;

		internal static bool Available
		{
			get
			{
				Resolve();
				return _clipType != null && _sourceType != null;
			}
		}

		internal static string Error
		{
			get
			{
				Resolve();
				return _error;
			}
		}

		internal static object CreateClip(string name, float[] samples, int sampleRate)
		{
			object clip = _create.Invoke(null, new object[5] { name, samples.Length, 1, sampleRate, false });
			if (clip == null)
			{
				return null;
			}
			object filled = _setData.Invoke(clip, new object[2] { samples, 0 });
			if (filled is bool ok && !ok)
			{
				// SetData returning false means the samples did not fit the clip - not worth keeping
				Destroy(clip);
				return null;
			}
			return clip;
		}

		internal static void Destroy(object clip)
		{
			if (clip is UnityEngine.Object unityObject)
			{
				UnityEngine.Object.Destroy(unityObject);
			}
		}

		internal static Component AddSource(GameObject holder)
		{
			return holder.AddComponent(_sourceType);
		}

		internal static void Configure(Component source, float maxDistance)
		{
			Set(source, _playOnAwake, false);
			Set(source, _loop, false);
			Set(source, _spatialBlend, 1f);
			Set(source, _minDistance, 1.5f);
			Set(source, _maxDistance, maxDistance);
		}

		internal static void SetClip(Component source, object clip)
		{
			Set(source, _clip, clip);
		}

		internal static void SetVolume(Component source, float volume)
		{
			Set(source, _volume, volume);
		}

		internal static void SetMaxDistance(Component source, float distance)
		{
			Set(source, _maxDistance, distance);
		}

		internal static void Play(Component source)
		{
			_play.Invoke(source, null);
		}

		internal static void Stop(Component source)
		{
			_stop.Invoke(source, null);
		}

		private static void Set(Component source, PropertyInfo property, object value)
		{
			property.SetValue(source, value, null);
		}

		/// <summary>The real message of a reflection failure (Invoke wraps everything in TargetInvocationException).</summary>
		internal static string Describe(Exception ex)
		{
			if (ex is TargetInvocationException && ex.InnerException != null)
			{
				return ex.InnerException.Message;
			}
			return ex.Message;
		}

		private static void Resolve()
		{
			// a failure is not permanent: the audio module is a Unity module and the first look can happen before it
			// is loaded. Three attempts, then it stays off (the reason is already in the log).
			if (_resolved || _attempts >= 3)
			{
				return;
			}
			_attempts++;
			try
			{
				_clipType = FindType(AudioClipTypeName);
				_sourceType = FindType(AudioSourceTypeName);
				if (_clipType == null || _sourceType == null)
				{
					Fail($"tipo nao encontrado ({AudioClipTypeName}={_clipType != null}, {AudioSourceTypeName}={_sourceType != null})");
					return;
				}
				const BindingFlags Static = BindingFlags.Public | BindingFlags.Static;
				const BindingFlags Instance = BindingFlags.Public | BindingFlags.Instance;
				_create = _clipType.GetMethod("Create", Static, null, CreateSignature, null);
				_setData = _clipType.GetMethod("SetData", Instance, null, SetDataSignature, null);
				_clip = _sourceType.GetProperty("clip", Instance);
				_volume = _sourceType.GetProperty("volume", Instance);
				_spatialBlend = _sourceType.GetProperty("spatialBlend", Instance);
				_minDistance = _sourceType.GetProperty("minDistance", Instance);
				_maxDistance = _sourceType.GetProperty("maxDistance", Instance);
				_loop = _sourceType.GetProperty("loop", Instance);
				_playOnAwake = _sourceType.GetProperty("playOnAwake", Instance);
				_play = _sourceType.GetMethod("Play", Instance, null, Type.EmptyTypes, null);
				_stop = _sourceType.GetMethod("Stop", Instance, null, Type.EmptyTypes, null);
				if (_create == null || _setData == null || _clip == null || _volume == null || _spatialBlend == null
					|| _minDistance == null || _maxDistance == null || _loop == null || _playOnAwake == null || _play == null || _stop == null)
				{
					Fail("membro da API de audio nao encontrado (a API do Unity mudou?)");
					return;
				}
				_resolved = true;
				_error = null;
				CompanionsPlugin.Log?.LogInfo($"[TTS] Audio do Unity acessivel por reflexao ({_clipType.Assembly.GetName().Name} "
					+ $"{_clipType.Assembly.GetName().Version}): AudioClip.{{Create,SetData}} + AudioSource.{{clip,volume,spatialBlend,minDistance,maxDistance,Play,Stop}}");
			}
			catch (Exception ex)
			{
				Fail(ex.Message);
			}
		}

		private static void Fail(string reason)
		{
			_clipType = null;
			_sourceType = null;
			_error = reason;
			if (_attempts == 1)
			{
				CompanionsPlugin.Log?.LogWarning("[TTS] Nao consegui acessar o audio do Unity: " + reason);
			}
		}

		/// <summary>
		/// The type by its assembly-qualified name first (the game's audio module is loaded at startup), and by a walk
		/// over the loaded assemblies if that fails.
		/// </summary>
		private static Type FindType(string name)
		{
			Type type = Type.GetType(name + ", UnityEngine.AudioModule", false);
			if (type != null)
			{
				return type;
			}
			foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				try
				{
					type = assembly.GetType(name, false);
				}
				catch (Exception)
				{
					type = null;
				}
				if (type != null)
				{
					return type;
				}
			}
			return null;
		}
	}
}
