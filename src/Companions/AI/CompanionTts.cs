using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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

	/// <summary>
	/// The voice of this line, resolved on the <b>main thread</b> when the line is queued (0 = use
	/// <c>[AI] TtsVoice</c>). Fase 3.10: a companion may have its own voice in its ZDO (<see cref="CompanionProfile"/>),
	/// and the ZDO must never be read from the synthesis thread.
	/// </summary>
	internal int Voice;

	/// <summary>
	/// May the sentences of this line be kept in the cache? True for the mod's own lines (they repeat word for
	/// word, which is what the cache is for) and false for the model's replies (new every time - caching them
	/// would push the fixed lines out and grow the heap, see <see cref="MaxCacheBytes"/>).
	/// </summary>
	internal bool Cacheable = true;
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
/// <item>fixed lines are <b>cached</b> (text + voice + speed → samples), so a repeated line is instant; only the
/// mod's own lines go in (the model's replies are new every time) and the cache is capped by <b>bytes</b>, not by
/// number of entries;</item>
/// <item>playback is a single 3D <see cref="AudioSource"/> placed at the companion that is talking, one line at a
/// time (a queue), volume from config; the <c>AudioClip</c> is destroyed as soon as it finishes.</item>
/// </list>
///
/// <b>What is spoken</b> (the voice follows the line that is shown in the chat/HUD):
/// <list type="bullet">
/// <item>free conversation: the model's <c>reply</c> - <c>[AI] TtsSpeakReplies</c>;</item>
/// <item>an order: the mod's own localized confirmation ("Atacando o troll.", "Ok: coletar madeira (10).") -
/// <c>[AI] TtsSpeakModLines</c>; with that one off, the model's <c>reply</c> for the order is spoken instead
/// (the reply is never shown for an order - the mod's line wins in the chat - so it is voice-only there);</item>
/// <item>a refusal, an error or "nothing understood": the mod's line, again under <c>[AI] TtsSpeakModLines</c>.</item>
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
	/// <summary>
	/// Voice used when the player has no choice of their own: the default of the <b>game language</b> (42 = pf_dora
	/// in Portuguese, 3 = af_heart in English...). Multilanguage M1: it was the pt-BR constant 42.
	/// </summary>
	internal static int DefaultVoice => CompanionLocale.Current.DefaultVoice;

	/// <summary>Where the model is expected when [AI] TtsModel is left at its default (relative to the plugin folder).</summary>
	internal const string DefaultModelRelative = @"tts\kokoro\model.int8.onnx";

	/// <summary>
	/// This Kokoro model is <b>multi-lingual</b> and the native side <b>refuses to load it without a language</b>
	/// ("You are using a multi-lingual Kokoro model ... please pass --kokoro-lexicon or --kokoro-lang"), so this is
	/// never empty. "pt-br" is Brazilian Portuguese; plain "pt" is European Portuguese. Only a last resort now: the
	/// language comes from <see cref="CompanionLocale.ResolveKokoro"/> and is never empty.
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

	/// <summary>Sentences kept in the cache. A second, hard cap on top of <see cref="MaxCacheBytes"/>.</summary>
	private const int MaxCacheEntries = 256;

	/// <summary>
	/// How much <b>audio</b> the cache may hold: 32 MB of <c>float</c> samples (~29 s of 24 kHz mono audio, i.e.
	/// a couple of dozen sentences).
	///
	/// The cap is by <b>bytes</b>, not by entry, because one entry is a raw <c>float[]</c>: a 180 character
	/// sentence is ~12 s of audio = ~1.1 MB, so an entry cap of 256 allowed hundreds of MB in the Mono heap.
	/// Entries are dropped oldest-first, and the model's replies are never stored at all (see
	/// <see cref="TtsRequest.Cacheable"/>): they are new every time, so letting them in would evict the fixed
	/// lines - the very thing the cache exists for - on every message.
	/// </summary>
	private const long MaxCacheBytes = 32L * 1024L * 1024L;

	/// <summary>
	/// How long <see cref="Shutdown"/> waits for the synthesis thread before giving up on freeing the model. The
	/// worker leaves its loop as soon as it sees the stopping flag (its wait is at most 500 ms), so the only way
	/// to hit this is a native <c>Generate</c> that never returns - and then the model must stay loaded.
	/// </summary>
	private const int ShutdownJoinMs = 5000;

	/// <summary>How long between two pre-load attempts while the model file is not there yet (see MaybePrewarm).</summary>
	private const float PrewarmRetrySeconds = 30f;

	/// <summary>Bounds of <c>[AI] TtsSpeed</c> (ModConfig binds 0,5–2,0): the clamp here and the panel slider
	/// use these, so the three can never drift apart (the panel used to clamp at 1,5 and silently lowered a 2,0).</summary>
	internal const float MinSpeed = 0.5f;

	internal const float MaxSpeed = 2f;

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

	/// <summary>Insertion order of <see cref="_cache"/>, so the oldest sentence is the first one to be dropped.</summary>
	private static readonly Queue<string> _cacheOrder = new Queue<string>();

	/// <summary>Bytes of samples held by the cache right now (a float is 4 bytes) - the cap that really matters.</summary>
	private static long _cacheBytes;

	private static readonly object _loadLock = new object();

	/// <summary>Guards the one-time creation of the synthesis thread.</summary>
	private static readonly object _workerLock = new object();

	private static readonly char[] BreakChars = new char[3] { ',', ';', ':' };

	private static OfflineTts _tts;

	/// <summary>
	/// Multilanguage M1 (fix 2.0.0): the language the loaded model was <b>built</b> with. The Kokoro model is one file
	/// for every language, but the phonemizer (espeak-ng) is chosen when it is loaded - so a model loaded while the game
	/// was in English keeps reading Portuguese with English phonemes (it sounds like English) no matter which voice is
	/// selected. Compared on every line; when it differs from <see cref="Language"/> the model is rebuilt.
	/// </summary>
	private static string _loadedLanguage;

	/// <summary>The language code a warning was already written for, so a hand-edited <c>[AI] TtsLanguage</c> does not
	/// flood the log with the same line on every spoken sentence.</summary>
	private static string _warnedLanguage;

	/// <summary>
	/// Set by <see cref="NoticeLanguageChange"/> and consumed by the synthesis thread - the only owner of
	/// <see cref="_tts"/>. The reload cannot happen on the calling thread: freeing the native model while a sentence is
	/// being generated in it is an AccessViolation, not an exception.
	/// </summary>
	private static volatile bool _reloadRequested;

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

	/// <summary>1 while a pre-load attempt is running (see <see cref="MaybePrewarm"/>).</summary>
	private static int _prewarmInFlight;

	/// <summary>1 once the model is loaded (or failed for good): no more pre-load attempts.</summary>
	private static int _prewarmDone;

	/// <summary>When the next pre-load attempt may start (main thread only).</summary>
	private static float _nextPrewarmAttempt;

	/// <summary>The "not on disk yet" warning is logged once, not on every retry.</summary>
	private static bool _warnedPrewarmMissing;

	/// <summary>Multilanguage M1: the effective language/voice are said once per session (see LogEffectiveOnce).</summary>
	private static bool _loggedEffective;

	private static bool _warnedMissingModel;

	/// <summary>TryGetTts logs the "model not found" error once (the pre-load retry calls it again).</summary>
	private static bool _warnedMissingModelFile;

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

	/// <summary>True once the <c>[IA] EcoDiag</c> block has run: it is a once-per-session thing (first sentence).</summary>
	private static bool _ecoDiagDone;

	/// <summary>The <c>reverbZoneMix</c> already written to the source (NaN = nothing written yet).</summary>
	private static float _appliedReverbZoneMix = float.NaN;

	/// <summary>[AI] TtsEnabled.</summary>
	internal static bool Enabled => ModConfig.TtsEnabled == null || ModConfig.TtsEnabled.Value;

	/// <summary>[AI] TtsSpeakReplies: speak the model's reply (conversation, and an order that has no spoken line of its own).</summary>
	internal static bool SpeakReplies => ModConfig.TtsSpeakReplies == null || ModConfig.TtsSpeakReplies.Value;

	/// <summary>[AI] TtsSpeakModLines: speak the mod's own localized feedback line (the one shown in the chat).</summary>
	internal static bool SpeakModLines => ModConfig.TtsSpeakModLines == null || ModConfig.TtsSpeakModLines.Value;

	private static bool CacheEnabled => ModConfig.TtsCache == null || ModConfig.TtsCache.Value;

	// ------------------------------------------------------------------ Fase 3.4 (parte 3): o eco da voz
	//
	// O eco tinha UMA causa, confirmada em jogo com o diagnostico: o Valheim carrega UMA AudioReverbZone global
	// (no proprio objeto do AudioListener, raio de 1000 m, decayTime de 4 s, room -1000) e a nossa AudioSource
	// estava no padrao do Unity (reverbZoneMix = 1), entao a fala pegava a cauda inteira - o "eco de caverna".
	// O jogo foge dela: a musica usa reverbZoneMix = 0 e o ambiente 0,1876583 (mixer SFX). ReverbZoneMix = 0
	// (o padrao) deixa a voz seca e foi o que resolveu no teste; EcoDiag escreve a foto do audio no log, para
	// investigar qualquer problema de som mais tarde (nunca por frame, so uma vez por sessao).

	/// <summary>[IA] EcoDiag: write the one-shot audio diagnosis to the log (see <see cref="RunEcoDiagnosticsOnce"/>).</summary>
	private static bool EcoDiagnostics => ModConfig.TtsEcoDiag != null && ModConfig.TtsEcoDiag.Value;

	/// <summary>
	/// What the voice uses when <c>[IA] TtsReverbZoneMix</c> is missing: <b>0 = dry</b>. Unity's own default for
	/// <c>AudioSource.reverbZoneMix</c> is 1, which is exactly what made the companion's speech pick up the world's
	/// global reverb zone (1000 m of radius, 4 s of tail) and sound like a cave.
	/// </summary>
	private const float DefaultReverbZoneMix = 0f;

	/// <summary>[IA] TtsReverbZoneMix: how much of the world's reverb zones reaches the companion's voice (0 = dry).</summary>
	private static float ReverbZoneMix
	{
		get
		{
			float value = ModConfig.TtsReverbZoneMix != null ? ModConfig.TtsReverbZoneMix.Value : DefaultReverbZoneMix;
			return Mathf.Clamp01(value);
		}
	}

	/// <summary>
	/// [AI] TtsVoice: the Kokoro speaker id. Multilanguage M1: it is resolved against the game language
	/// (<see cref="CompanionLocale.ResolveVoice"/>), so 0 ("automatic") and a voice of <b>another</b> language become
	/// the default voice of this one - a pt-BR voice with an English phonemizer would come out with the wrong accent.
	/// </summary>
	internal static int Voice
	{
		get
		{
			int value = ModConfig.TtsVoice != null ? ModConfig.TtsVoice.Value : 0;
			return CompanionLocale.ResolveVoice(value);
		}
	}

	/// <summary>
	/// The voice for the log, honest about where it came from (Multilanguage M2 fix - it said "auto" even when the
	/// player had chosen a voice of <b>another</b> language and it had been replaced):
	/// <c>voz 42 (pf_dora)</c> for an explicit voice of this language, <c>voz 3 (auto, af_heart)</c> for "automatic",
	/// and <c>voz 3 (a 42 (pf_dora) e de outro idioma; usando a padrao do idioma)</c> when the pick was overruled.
	/// </summary>
	private static string VoiceLogText()
	{
		int configured = ModConfig.TtsVoice != null ? ModConfig.TtsVoice.Value : 0;
		int voice = Voice;
		if (configured <= 0)
		{
			return $"voz {voice} (auto, {CompanionLocale.VoiceName(voice)})";
		}
		if (CompanionLocale.HasVoice(configured))
		{
			return $"voz {voice} ({CompanionLocale.VoiceName(voice)})";
		}
		return $"voz {voice} (a {configured} ({CompanionLocale.VoiceName(configured)}) e de outro idioma; usando a padrao do idioma)";
	}

	/// <summary>[AI] TtsSpeed.</summary>
	internal static float Speed
	{
		get
		{
			float value = ModConfig.TtsSpeed != null ? ModConfig.TtsSpeed.Value : 1f;
			return Mathf.Clamp(value, MinSpeed, MaxSpeed);
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
	internal static float Volume
	{
		get
		{
			float value = ModConfig.TtsVolume != null ? ModConfig.TtsVolume.Value : 1f;
			return Mathf.Clamp01(value);
		}
	}

	/// <summary>
	/// [AI] TtsLanguage, never empty (the multi-lingual Kokoro model needs it). Multilanguage M1: "auto" (and empty)
	/// follow the game language through <see cref="CompanionLocale.ResolveKokoro"/>; an explicit code wins.
	/// </summary>
	internal static string Language
	{
		get
		{
			string value = CompanionLocale.ResolveKokoro(ModConfig.TtsLanguage != null ? ModConfig.TtsLanguage.Value : null);
			// ResolveKokoro never answers empty (the profile carries a code); this stays as the last resort so the
			// native side can never be asked to load the model without a language
			return string.IsNullOrWhiteSpace(value) ? DefaultLanguage : value.Trim();
		}
	}

	/// <summary>True when the language in use is the automatic one (the game's).</summary>
	private static bool LanguageIsAuto => CompanionLocale.IsAuto(ModConfig.TtsLanguage != null ? ModConfig.TtsLanguage.Value : null);

	/// <summary>The model file to load: [AI] TtsModel (relative to the plugin folder, or absolute).</summary>
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
	///
	/// <para>Multilanguage M1: what is printed here is the <b>setting</b>, never the resolved language/voice - this
	/// runs before the game has a Localization (the plugin's Awake), so the resolution would answer English. The
	/// effective values are said once the player is in the world (<see cref="LogEffectiveOnce"/>).</para>
	/// </summary>
	internal static void Init()
	{
		try
		{
			string model = ModelPath;
			string dir = Path.GetDirectoryName(model) ?? "";
			string cacheInfo = CacheEnabled
				? $"ligado (so as linhas do proprio mod, teto {MaxCacheBytes / (1024 * 1024)} MB de audio)"
				: "desligado";
			CompanionsPlugin.Log?.LogInfo($"[TTS] Fala do companheiro (Kokoro-82M via sherpa-onnx, CPU, local): "
				+ $"{(Enabled ? "LIGADO" : "desligado ([AI] TtsEnabled = false)")} | modelo \"{model}\" ({DescribeFile(model)}) | "
				+ $"voz {ConfiguredVoiceText()} | idioma {ConfiguredLanguageText()} | {Threads} thread(s) | velocidade {Speed:0.##} | "
				+ $"volume {Volume:0.##} | cache {cacheInfo}.");
			CompanionsPlugin.Log?.LogInfo($"[TTS] O que e falado: a resposta do modelo (TtsFalarRespostas={(SpeakReplies ? "sim" : "nao")}) "
				+ $"e as linhas do proprio mod (TtsFalarFalasDoMod={(SpeakModLines ? "sim" : "nao")}). Com os dois ligados a linha que "
				+ "aparece no chat e a que sai em voz; a outra so vai para o log (nada e falado duas vezes).");
			CompanionsPlugin.Log?.LogInfo("[TTS] Voz padrao (a do idioma do jogo: e a que a aba IA do painel mostra, e a de quem nao tem voz propria) "
				+ "e a de cada companheiro. Fase 3.10: cada companheiro pode ter a SUA voz, guardada no ZDO dele (aba dele no painel, e o "
				+ "painel do primeiro companheiro) - a voz e resolvida na hora da fala e entra na chave do cache, entao cada voz tem o seu audio.");
			CompanionsPlugin.Log?.LogInfo($"[TTS] Vozes (\"{Path.Combine(dir, VoicesFileName)}\", {DescribeFile(Path.Combine(dir, VoicesFileName))}), "
				+ $"tokens (\"{Path.Combine(dir, TokensFileName)}\", {DescribeFile(Path.Combine(dir, TokensFileName))}) e "
				+ $"espeak-ng-data (\"{Path.Combine(dir, EspeakDataDirName)}\") - tudo procurado na MESMA pasta do modelo.");
			// fix 2.0.0: [AI] TtsLanguage changed in the F8 (or by hand in the .cfg) - the loaded model has the phonemizer
			// of the old language, so it has to be rebuilt. The synthesis thread does it (see DropModelIfLanguageChanged).
			if (ModConfig.TtsLanguage != null)
			{
				ModConfig.TtsLanguage.SettingChanged += delegate
				{
					NoticeLanguageChange();
				};
			}
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log?.LogWarning("[TTS] Init falhou: " + ex);
		}
	}

	/// <summary>
	/// Multilanguage M1: says (once per session, on the main thread, once the player is in the world - i.e. after the
	/// language is really known) which language and voice are in use, and which voices this language has. It also
	/// <b>resolves and caches</b> the language profile here, on the main thread: the model load reads it from a
	/// background task, and the game language may not be read there.
	/// </summary>
	private static void LogEffectiveOnce()
	{
		if (_loggedEffective || Player.m_localPlayer == null)
		{
			return;
		}
		_loggedEffective = true;
		try
		{
			LocaleProfile profile = CompanionLocale.Current;
			CompanionsPlugin.Log?.LogInfo($"[TTS] idioma efetivo: {Language}{(LanguageIsAuto ? " (auto)" : "")} | {VoiceLogText()}");
			CompanionsPlugin.Log?.LogInfo($"[TTS] Vozes do idioma \"{profile.Name}\" ({profile.KokoroCode}), da melhor para a pior: "
				+ $"{DescribeVoices(profile)}. A voz de um companheiro que seja de outro idioma NAO e usada (ele fala com a padrao deste).");
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log?.LogDebug("[TTS] Falha ao logar o idioma efetivo: " + ex.Message);
		}
	}

	/// <summary>
	/// Loads the model in the background, before the first line needs it. Called from <see cref="MaybePrewarm"/>
	/// (which is what decides whether a new attempt is due); does nothing while another attempt is running.
	/// </summary>
	internal static void Prewarm()
	{
		if (!Enabled || _stopping)
		{
			return;
		}
		if (Interlocked.CompareExchange(ref _prewarmInFlight, 1, 0) != 0)
		{
			return;
		}
		Task.Run(delegate
		{
			try
			{
				if (TryGetTts(out OfflineTts _, out string error))
				{
					Volatile.Write(ref _prewarmDone, 1);
					CompanionsPlugin.Log?.LogInfo("[TTS] Modelo pre-carregado em segundo plano (a primeira fala nao vai esperar por ele).");
				}
				else if (_loadFailed || _stopping)
				{
					// a hard failure (bad model, native libraries, shutdown): trying again would only repeat it
					Volatile.Write(ref _prewarmDone, 1);
					CompanionsPlugin.Log?.LogWarning("[TTS] Nao consegui pre-carregar o modelo: " + error);
				}
				else
				{
					// the file is simply not there yet: keep the door open (see MaybePrewarm) instead of staying
					// mute for the rest of the session
					if (!_warnedPrewarmMissing)
					{
						_warnedPrewarmMissing = true;
						CompanionsPlugin.Log?.LogWarning("[TTS] Nao consegui pre-carregar o modelo: " + error
							+ $" Vou tentar de novo a cada {PrewarmRetrySeconds:0} s enquanto voce estiver no mundo (o modelo pode ainda nao estar no disco).");
					}
				}
			}
			catch (Exception ex)
			{
				CompanionsPlugin.Log?.LogWarning("[TTS] Excecao ao pre-carregar o modelo: " + ex);
			}
			finally
			{
				Volatile.Write(ref _prewarmInFlight, 0);
			}
		});
	}

	/// <summary>
	/// Warms the model once the player is in the world - and <b>tries again</b> while the model is not there yet.
	/// The old version was single-shot: if the file was not on disk at that exact moment (the model folder copied
	/// after the first spawn, a big download still being written), the companion stayed mute until a restart.
	/// One attempt at a time, one every <see cref="PrewarmRetrySeconds"/>, and only until the model is loaded or
	/// the load failed for good. Runs on the main thread once per frame but does nothing (a couple of bool reads)
	/// unless a retry is really due.
	/// </summary>
	private static void MaybePrewarm()
	{
		if (!Enabled || _stopping || _loadFailed || Volatile.Read(ref _prewarmDone) != 0)
		{
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
	/// The companion says something. Called on the main thread (from the AI layer); returns immediately - the line is
	/// queued and synthesized on the background worker. Fase 3.10: the line is spoken with <b>this companion's</b>
	/// voice (<see cref="CompanionProfile.GetVoice"/>), which is <c>[AI] TtsVoice</c> when it has none of its own.
	/// </summary>
	internal static void Speak(CompanionSetup companion, string text, bool cacheable = true)
	{
		Speak(companion != null ? companion.gameObject : null, text, cacheable,
			companion != null ? CompanionProfile.GetVoice(companion) : 0);
	}

	/// <summary>
	/// Same, for whoever is talking (a companion, or the player for the console test). <paramref name="talker"/> is
	/// only used to place the 3D sound, so null is accepted (the sound then stays where it was).
	/// <paramref name="cacheable"/> is false for the model's own replies (see <see cref="TtsRequest.Cacheable"/>).
	/// The voice is <c>[AI] TtsVoice</c>: a caller that knows the companion (its tab in the panel, the starter preview)
	/// passes the voice itself through <see cref="Speak(GameObject, string, bool, int)"/>.
	///
	/// <para>Fase 3.12 (auditoria): the voice is resolved <b>here</b>, on the main thread, and travels with the line.
	/// It used to be read by the synthesis thread, which is the one place in the mod that must never touch the config
	/// (or anything else that belongs to the main thread).</para>
	/// </summary>
	internal static void Speak(GameObject talker, string text, bool cacheable = true)
	{
		Speak(talker, text, cacheable, Voice);
	}

	/// <summary>
	/// Same, with the voice of the line decided by the caller: <paramref name="voice"/> 0 means "the default"
	/// (<c>[AI] TtsVoice</c>), which is also what a caller that does not care passes. Used by the panels, whose
	/// "Ouvir" has to preview the voice of the context - the companion's own on its tab, the global one on the AI tab.
	/// </summary>
	internal static void Speak(GameObject talker, string text, bool cacheable, int voice)
	{
		// after Shutdown the worker is gone: queueing here would leave the line in the queue forever (and the
		// pending counter with it)
		if (_stopping || !Enabled || string.IsNullOrEmpty(text))
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
					+ "Coloque a pasta tts\\kokoro do modelo (model.int8.onnx + voices.bin + tokens.txt + espeak-ng-data) no plugin ou ajuste [AI] TtsModel.");
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
		int lineVoice = voice > 0 ? voice : Voice;
		_requests.Enqueue(new TtsRequest
		{
			Speaker = talker,
			Text = trimmed,
			Cacheable = cacheable,
			Voice = lineVoice
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
		CompanionsPlugin.Log?.LogInfo($"[TTS] Fala enfileirada: \"{trimmed}\" (voz {(lineVoice > 0 ? lineVoice.ToString() : "padrao " + Voice)}, "
			+ $"{Volatile.Read(ref _pending)} pedido(s) na fila).");
	}

	/// <summary>
	/// Called every frame from the plugin's Update. Never blocks and never allocates when idle: it warms the model
	/// once the player is in the world, watches a synthesis that never comes back and drives the playback queue.
	/// </summary>
	internal static void Tick()
	{
		try
		{
			LogEffectiveOnce();
			MaybePrewarm();
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
		// Wait for the synthesis thread to really be gone before touching the model. It only checks the stopping
		// flag at the top of its loop, so it can be between two sentences (busy == 0) with the model in hand: the
		// old busy check let Dispose() run under a live thread, and a Generate on a freed handle is an
		// AccessViolation - a native crash, not something a catch can handle.
		Thread worker = _worker;
		if (worker != null && !worker.Join(ShutdownJoinMs))
		{
			CompanionsPlugin.Log?.LogWarning($"[TTS] A thread de sintese nao terminou em {ShutdownJoinMs} ms - o modelo do Kokoro ficou carregado "
				+ "(nao descarto um modelo nativo que pode estar em uso).");
			return;
		}
		if (Volatile.Read(ref _busy) != 0)
		{
			// belt and suspenders: with the thread gone this cannot happen, and if it somehow does the model stays
			CompanionsPlugin.Log?.LogWarning("[TTS] Ainda havia uma sintese em andamento no descarregamento - o modelo ficou carregado.");
			return;
		}
		// Fase 3.23 (auditoria): the worker is gone, so nothing else writes to the queues/cache - free them here. The
		// cache alone can hold MaxCacheBytes (32 MB) of float[], and the session is over.
		FreeQueuesAndCache();
		// the worker is gone, so the gate is free; taking it anyway keeps the rule "the native model is only
		// touched under NativeGate" (the worker holds it across the Generate call)
		lock (NativeGate)
		{
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
	}

	/// <summary>
	/// Fase 3.23 (auditoria): drops everything that is still buffered - the playback queue, the requests waiting for
	/// synthesis and the sentence cache (up to <see cref="MaxCacheBytes"/> of audio) - and zeroes the pending counter.
	/// Called from <see cref="Shutdown"/> after the worker is gone, so nothing puts anything back.
	/// </summary>
	private static void FreeQueuesAndCache()
	{
		int sentences = 0;
		long bytes = 0L;
		lock (_cache)
		{
			sentences = _cache.Count;
			bytes = _cacheBytes;
			_cache.Clear();
			_cacheOrder.Clear();
			_cacheBytes = 0L;
		}
		int chunks = 0;
		while (_ready.TryDequeue(out TtsChunk _))
		{
			chunks++;
		}
		int requests = 0;
		while (_requests.TryDequeue(out TtsRequest _))
		{
			requests++;
		}
		Interlocked.Exchange(ref _pending, 0);
		CompanionsPlugin.Log?.LogInfo($"[TTS] Descarregamento: {sentences} frase(s) do cache ({bytes / (1024 * 1024)} MB de audio), "
			+ $"{chunks} trecho(s) de fala e {requests} pedido(s) liberados da memoria.");
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
			// fix 2.0.0: the language changed while the model was loaded - rebuild it here, before the next line, so the
			// first sentence after the switch already comes out in the new language (the load takes a second or two, and
			// this is the synthesis thread, so the game never waits for it).
			if (_reloadRequested)
			{
				_reloadRequested = false;
				try
				{
					DropModelIfLanguageChanged();
					if (TryGetTts(out OfflineTts _, out string error))
					{
						CompanionsPlugin.Log?.LogInfo($"[TTS] Modelo recarregado no idioma \"{Language}\" antes da proxima fala.");
					}
					else if (!string.IsNullOrEmpty(error))
					{
						CompanionsPlugin.Log?.LogWarning("[TTS] Nao consegui recarregar o modelo no novo idioma: " + error);
					}
				}
				catch (Exception ex)
				{
					CompanionsPlugin.Log?.LogWarning("[TTS] Falha ao recarregar o modelo no novo idioma: " + ex);
				}
			}
			while (_requests.TryDequeue(out TtsRequest request))
			{
				try
				{
					if (!_stopping)
					{
						Process(request);
					}
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
		if (_stopping)
		{
			return;
		}
		// fix 2.0.0: the model carries the phonemizer of the language it was loaded in, so a language change (the game's
		// or [AI] TtsLanguage) has to rebuild it - this is the synthesis thread, the only place where it is safe.
		DropModelIfLanguageChanged();
		if (!TryGetTts(out OfflineTts tts, out string error))
		{
			CompanionsPlugin.Log?.LogWarning($"[TTS] Nao consegui falar \"{request.Text}\": {error}");
			return;
		}
		if (_stopping)
		{
			// Shutdown ran while the model was being fetched: the native library may be gone by now, so nothing
			// else here may touch tts (Generate on a freed handle is an AccessViolation, not an exception)
			return;
		}
		int job = Interlocked.Increment(ref _jobs);
		// Fase 3.12: the line carries its voice (resolved on the main thread when it was queued); the config is never
		// read from this thread. 0/negative would be speaker 0 (another language), so the default answers for it.
		int voice = EffectiveVoice(tts, request.Voice > 0 ? request.Voice : DefaultVoice);
		float speed = Speed;
		bool cache = CacheEnabled && request.Cacheable;
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
			bool generateFailed = false;
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
							// The handle is checked FIRST: the binding always builds a non-null object, even when
							// the native call returned NULL (the handle is then IntPtr.Zero), and NumSamples/Samples
							// go straight into Marshal.PtrToStructure(Handle) - a null dereference is an
							// AccessViolationException, which no catch here could survive.
							if (audio == null || audio.Handle == IntPtr.Zero)
							{
								generateFailed = true;
							}
							else if (audio.NumSamples > 0)
							{
								samples = audio.Samples;
							}
						}
						finally
						{
							// with a null handle there is nothing to free, and handing NULL to the native destroy
							// is at best pointless: suppress the finalizer instead of calling Dispose on it
							if (audio != null)
							{
								if (audio.Handle == IntPtr.Zero)
								{
									GC.SuppressFinalize(audio);
								}
								else
								{
									audio.Dispose();
								}
							}
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
					if (generateFailed)
					{
						CompanionsPlugin.Log?.LogWarning($"[TTS] A sintese nativa falhou (handle nulo) na frase {i + 1}/{sentences.Length}: \"{sentence}\"");
					}
					else
					{
						CompanionsPlugin.Log?.LogWarning($"[TTS] A frase {i + 1}/{sentences.Length} voltou vazia: \"{sentence}\"");
					}
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
	/// The voice actually used: the one the line carries (a companion's own voice, Fase 3.10) or <c>[AI] TtsVoice</c>,
	/// but never outside the loaded model - a speaker id the model does not have would make the native call fail.
	/// <see cref="DefaultVoice"/> (the default voice of the game language) is the fallback.
	/// </summary>
	private static int EffectiveVoice(OfflineTts tts, int wanted)
	{
		int voice = wanted;
		if (tts != null && (voice < 0 || voice >= tts.NumSpeakers))
		{
			return tts.NumSpeakers > DefaultVoice ? DefaultVoice : 0;
		}
		return voice;
	}

	/// <summary>
	/// Multilanguage M1 (fix 2.0.0): the game language (or <c>[AI] TtsLanguage</c>) changed, and the loaded model speaks
	/// the language it was <b>built</b> with (see <see cref="_loadedLanguage"/>). All this does is ask the synthesis
	/// thread to rebuild it: that thread is the only user of the native model, and it is where freeing it is safe. Cheap
	/// and Unity-free, so it can be called from the <c>Localization.SetupLanguage</c> postfix and from the config hook.
	/// </summary>
	internal static void NoticeLanguageChange()
	{
		try
		{
			if (!Enabled || _stopping || _tts == null)
			{
				// nothing loaded: the next load already reads the new language, nothing to rebuild
				_reloadRequested = false;
				return;
			}
			_reloadRequested = true;
			_wake.Release();
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log?.LogDebug("[TTS] Falha ao avisar da troca de idioma: " + ex.Message);
		}
	}

	/// <summary>
	/// Frees the model when the language it was built with is not the effective one any more, so the next
	/// <see cref="TryGetTts"/> builds it again with the right phonemizer. <b>Only the synthesis thread calls this</b>;
	/// the audio cache goes with it, because every sample in it was generated with the old phonemizer.
	/// </summary>
	private static void DropModelIfLanguageChanged()
	{
		string language = Language;
		lock (_loadLock)
		{
			if (_tts == null || string.Equals(_loadedLanguage, language, StringComparison.Ordinal))
			{
				return;
			}
			// A code the model does not really speak (a typo in the .cfg, "de", "en-gb"...) would make the native load
			// fail - and then the voice would be gone until a restart. Keeping the model that works is always better than
			// swapping it for nothing: the log says what happened, once per code.
			if (!IsSupportedLanguage(language))
			{
				if (!string.Equals(_warnedLanguage, language, StringComparison.Ordinal))
				{
					_warnedLanguage = language;
					CompanionsPlugin.Log?.LogWarning($"[TTS] \"{language}\" nao tem voz propria neste Kokoro: o modelo "
						+ $"carregado (\"{_loadedLanguage}\") continua e nada foi recarregado. O padrao e \"auto\" - com ele, "
						+ "qualquer idioma do jogo fora dos 5 cai no ingles (en-us / af_heart).");
				}
				return;
			}
			string previous = _loadedLanguage;
			try
			{
				_tts.Dispose();
			}
			catch (Exception ex)
			{
				CompanionsPlugin.Log?.LogDebug("[TTS] Falha ao liberar o modelo do idioma anterior: " + ex.Message);
			}
			_tts = null;
			_loadedLanguage = null;
			_warnedLanguage = null;
			ClearCache();
			CompanionsPlugin.Log?.LogInfo($"[TTS] Idioma efetivo \"{language}\" (o modelo estava carregado em \"{previous}\") - "
				+ "modelo liberado para carregar no idioma certo e cache de audio esvaziado (eram amostras do idioma antigo).");
		}
	}

	/// <summary>
	/// The language codes the mod speaks - the same five the interface, the lines, the presets and the prompt contract
	/// have, and each one synthesized with the shipped model out of the game (kokoro skill, 30/10/2026). This is the
	/// gate of <see cref="DropModelIfLanguageChanged"/>: anything else must not take a working model away.
	/// </summary>
	private static readonly string[] SupportedLanguages = new string[] { "pt-br", "en-us", "es", "fr", "it" };

	/// <summary>Is this code one of <see cref="SupportedLanguages"/>?</summary>
	internal static bool IsSupportedLanguage(string code)
	{
		if (string.IsNullOrEmpty(code))
		{
			return false;
		}
		for (int i = 0; i < SupportedLanguages.Length; i++)
		{
			if (string.Equals(SupportedLanguages[i], code.Trim(), StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>Throws every synthesized sentence away (a language change makes all of them wrong).</summary>
	private static void ClearCache()
	{
		lock (_cache)
		{
			_cache.Clear();
			_cacheOrder.Clear();
			_cacheBytes = 0;
		}
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
			if (_stopping)
			{
				// never (re)load after Shutdown: the new model would be used by a native library that is already
				// disposed (and nothing would free it)
				error = "o TTS foi encerrado";
				return false;
			}
			if (_loadFailed)
			{
				error = _loadError;
				return false;
			}
			string modelPath = ModelPath;
			if (!File.Exists(modelPath))
			{
				// deliberately NOT sticky: the folder may be copied while the game is running, and the pre-load
				// retry (MaybePrewarm) is what turns that into a working voice without a restart. The error is
				// logged once, so the retry does not fill the log.
				_loadError = $"modelo nao encontrado em \"{modelPath}\"";
				if (!_warnedMissingModelFile)
				{
					_warnedMissingModelFile = true;
					CompanionsPlugin.Log?.LogError($"[TTS] {_loadError}. Coloque a pasta tts\\kokoro do modelo no plugin ou ajuste [AI] TtsModel.");
				}
				error = _loadError;
				return false;
			}
			try
			{
				EnsureNativeLibraries();
				string dir = Path.GetDirectoryName(modelPath) ?? "";
				int threads = Threads;
				string language = Language;
				if (!IsSupportedLanguage(language) && !string.Equals(_warnedLanguage, language, StringComparison.Ordinal))
				{
					_warnedLanguage = language;
					CompanionsPlugin.Log?.LogWarning($"[TTS] [AI] TtsLanguage = \"{language}\" nao tem voz propria neste Kokoro. "
						+ $"O padrao e \"auto\": com ele, qualquer um dos 5 idiomas ({string.Join(", ", SupportedLanguages)}) usa "
						+ "a voz dele e QUALQUER outro idioma do jogo (de, ru, pl, tr, ko, sv, uk...) cai no ingles (en-us / "
						+ "af_heart), como sempre foi. O modelo vai carregar o codigo escrito mesmo assim, mas as falas do mod "
						+ "sao escritas em PT/EN/ES/FR/IT.");
				}
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
				if (_stopping)
				{
					// Shutdown ran while the model was loading: free what was just built instead of handing it out
					// (and never assign it to _tts, so nothing can reach it afterwards)
					try
					{
						tts.Dispose();
					}
					catch (Exception ex)
					{
						CompanionsPlugin.Log?.LogDebug("[TTS] Falha ao liberar o modelo carregado no shutdown: " + ex.Message);
					}
					tts = null;
					error = "o TTS foi encerrado";
					return false;
				}
				_tts = tts;
				_loadedLanguage = language;
				int loadedVoice = Voice;
				CompanionsPlugin.Log?.LogInfo($"[TTS] Modelo CARREGADO em {watch.ElapsedMilliseconds} ms e MANTIDO carregado: \"{modelPath}\" "
					+ $"(sherpa-onnx {SherpaOnnxVersion()}, Kokoro-82M, CPU, {threads} thread(s), idioma \"{language}\") | "
					+ $"{tts.NumSpeakers} vozes | saida {tts.SampleRate} Hz mono | voz {loadedVoice} ({CompanionLocale.VoiceName(loadedVoice)}).");
				if (loadedVoice >= tts.NumSpeakers)
				{
					CompanionsPlugin.Log?.LogWarning($"[TTS] A voz {loadedVoice} ({CompanionLocale.VoiceName(loadedVoice)}) nao existe neste modelo "
						+ $"({tts.NumSpeakers} vozes) - a voz {DefaultVoice} ({CompanionLocale.VoiceName(DefaultVoice)}) vai ser usada. "
						+ $"Vozes do idioma \"{language}\": {DescribeVoices(CompanionLocale.Current)}");
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
		string actual = ModulePathOf(handle);
		CompanionsPlugin.Log?.LogInfo($"[TTS] Nativa carregada: \"{path}\" -> {actual ?? "(caminho desconhecido)"} (handle 0x{handle.ToInt64():X})");
		if (!SameFile(actual, path))
		{
			// Windows identifies a module by its full path, so a second copy of the same base name is loaded as a
			// separate module - but the managed binding asks for it by NAME (DllImport("sherpa-onnx-c-api")), and
			// that resolves to whichever copy got there first. A different version there means a broken ABI, so
			// say it out loud instead of failing later inside the native call.
			CompanionsPlugin.Log?.LogWarning($"[TTS] ATENCAO: o modulo de \"{Path.GetFileName(path)}\" que o processo esta usando e \"{actual}\", e NAO o do plugin "
				+ $"(\"{path}\"). O binding resolve a nativa pelo NOME-BASE, entao as chamadas podem cair no modulo errado (outro mod, ou uma copia na pasta "
				+ "do jogo, de versao diferente) e a ABI pode nao bater. Feche o outro mod/versao que usa sherpa-onnx/onnxruntime.");
		}
	}

	/// <summary>The path of the module behind a handle, or <b>null</b> when it cannot be read (never a made-up path).</summary>
	private static string ModulePathOf(IntPtr handle)
	{
		try
		{
			StringBuilder buffer = new StringBuilder(1024);
			uint length = GetModuleFileName(handle, buffer, (uint)buffer.Capacity);
			return length > 0 ? buffer.ToString() : null;
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log?.LogDebug("[TTS] Nao consegui ler o caminho do modulo nativo: " + ex.Message);
			return null;
		}
	}

	/// <summary>
	/// Is the module Windows really has behind the handle the file we asked for? False means another copy of the
	/// same base name was already loaded. When the path cannot be read (or cannot be compared) this says "same",
	/// so an unreadable path never turns into a false alarm.
	/// </summary>
	private static bool SameFile(string actual, string expected)
	{
		if (string.IsNullOrEmpty(actual) || string.IsNullOrEmpty(expected))
		{
			return true;
		}
		try
		{
			return string.Equals(Path.GetFullPath(actual), Path.GetFullPath(expected), StringComparison.OrdinalIgnoreCase);
		}
		catch (Exception)
		{
			return true;
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
		// Fase 3.4 (parte 3): the reverb zone mix of the voice. The source itself is created once, so this only
		// writes when the value really changed - a value typed in the F8 panel is heard on the very next sentence,
		// without a restart, and the default (0 = dry) is applied on the first sentence.
		ApplyReverbZoneMix(_source);
		if (EcoDiagnostics)
		{
			RunEcoDiagnosticsOnce(chunk.Speaker);
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

	// ---------------------------------------------------------------- o eco (Fase 3.4, parte 3)

	/// <summary>
	/// The <c>reverbZoneMix</c> of the voice (<c>[IA] TtsReverbZoneMix</c>), written to the source whenever it
	/// differs from the value already applied (the first sentence included). The source is created once and kept
	/// for the session, so there is nothing to redo on the sentences that follow.
	///
	/// Unity's default is 1, and the Valheim world carries a <b>global</b> reverb zone (1000 m of radius, 4 s of
	/// tail) that then colours the whole sentence - the "cave echo" the players heard. The mod's default is 0
	/// (dry), which is what the game itself does with the music (0) and the ambience (0,1876583). A Unity build
	/// without the member leaves the voice as it is and says so in the log, once.
	/// </summary>
	private static void ApplyReverbZoneMix(Component source)
	{
		if (source == null)
		{
			return;
		}
		float mix = ReverbZoneMix;
		if (!float.IsNaN(_appliedReverbZoneMix) && Mathf.Approximately(_appliedReverbZoneMix, mix))
		{
			return;
		}
		// Written down before the setter: a member this Unity does not have will not appear later, so there is no
		// point in trying again on every sentence.
		_appliedReverbZoneMix = mix;
		try
		{
			StringBuilder applied = new StringBuilder();
			AppendApplied(applied, "reverbZoneMix=" + mix.ToString("0.##", CultureInfo.InvariantCulture), Audio.SetReverbZoneMix(source, mix));
			CompanionsPlugin.Log?.LogInfo("[TTS] ReverbZoneMix da voz: " + applied.ToString());
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log?.LogWarning("[TTS] Nao consegui aplicar o reverbZoneMix da voz: " + Audio.Describe(ex));
		}
	}

	/// <summary>Adds one applied switch to the line (and says so when the Unity member behind it does not exist).</summary>
	private static void AppendApplied(StringBuilder builder, string text, bool applied)
	{
		if (builder.Length > 0)
		{
			builder.Append(", ");
		}
		builder.Append(text);
		if (!applied)
		{
			builder.Append(" (sem efeito: este Unity nao tem o membro)");
		}
	}

	/// <summary>
	/// The one-shot audio diagnosis (<c>[IA] EcoDiag</c>), on the main thread and on the <b>first sentence</b>.
	/// It writes the picture of the scene's audio to the log: does the <c>AudioListener</c> carry a reverb/low-pass
	/// filter, which reverb zones are loaded (and how big they are, and whether the player and the companion are
	/// inside each one), and how the game configures its <b>own</b> sources (mixer group, bypass flags, reverb zone
	/// mix) - which is the recipe we matched to kill the echo (the global 1000 m zone, <c>reverbZoneMix = 0</c>).
	///
	/// The searches here walk every loaded object of a type, which is exactly why this runs <b>once per session</b>,
	/// never per frame - and never at all with the flag off. It only writes to the log: no sound is touched.
	/// </summary>
	private static void RunEcoDiagnosticsOnce(GameObject speaker)
	{
		if (_ecoDiagDone)
		{
			return;
		}
		_ecoDiagDone = true;
		try
		{
			CompanionsPlugin.Log?.LogInfo("[ECO] Diagnostico de audio LIGADO ([IA] EcoDiag = true): nada muda no som, isto so escreve no log, uma vez por sessao.");
			LogEcoListener();
			LogEcoReverbZones(speaker);
			LogEcoSources();
			CompanionsPlugin.Log?.LogInfo("[ECO] Fim do diagnostico de audio.");
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log?.LogWarning("[ECO] O diagnostico do eco falhou: " + ex);
		}
	}

	/// <summary>Does the scene's listener carry a reverb/low-pass filter (a filter there would colour everything)?</summary>
	private static void LogEcoListener()
	{
		Type type = Audio.FindType(AudioListenerTypeName);
		if (type == null)
		{
			CompanionsPlugin.Log?.LogInfo($"[ECO] Ouvinte: {AudioListenerTypeName} nao existe neste Unity.");
			return;
		}
		UnityEngine.Object[] listeners = FindLoaded(type);
		if (listeners.Length == 0)
		{
			CompanionsPlugin.Log?.LogInfo("[ECO] Ouvinte: nenhum AudioListener carregado.");
			return;
		}
		CompanionsPlugin.Log?.LogInfo($"[ECO] Ouvinte: {listeners.Length} AudioListener carregado(s).");
		for (int i = 0; i < listeners.Length; i++)
		{
			Component listener = listeners[i] as Component;
			if (listener == null)
			{
				continue;
			}
			GameObject holder = listener.gameObject;
			Component reverb = FindComponent(holder, Audio.FindType(ReverbFilterTypeName));
			Component lowPass = FindComponent(holder, Audio.FindType(LowPassFilterTypeName));
			string reverbInfo = reverb != null
				? $" (preset={Member(reverb, "reverbPreset")}, dryLevel={Member(reverb, "dryLevel")}, room={Member(reverb, "room")}, decayTime={Member(reverb, "decayTime")})"
				: "";
			string lowPassInfo = lowPass != null ? $" (cutoff={Member(lowPass, "cutoffFrequency")})" : "";
			CompanionsPlugin.Log?.LogInfo($"[ECO]   ouvinte \"{holder.name}\": AudioReverbFilter={(reverb != null ? "SIM" : "nao")}{reverbInfo}, "
				+ $"AudioLowPassFilter={(lowPass != null ? "SIM" : "nao")}{lowPassInfo}");
		}
	}

	/// <summary>Every reverb zone in the scene, and whether the player and the companion are inside each one.</summary>
	private static void LogEcoReverbZones(GameObject speaker)
	{
		Type type = Audio.FindType(ReverbZoneTypeName);
		if (type == null)
		{
			CompanionsPlugin.Log?.LogInfo($"[ECO] Zonas de reverb: {ReverbZoneTypeName} nao existe neste Unity (nao ha como o eco vir de uma zona).");
			return;
		}
		UnityEngine.Object[] zones = FindLoaded(type);
		if (zones.Length == 0)
		{
			CompanionsPlugin.Log?.LogInfo("[ECO] Zonas de reverb: nenhuma carregada (o eco NAO vem de uma zona de reverb).");
			return;
		}
		GameObject player = Player.m_localPlayer != null ? Player.m_localPlayer.gameObject : null;
		CompanionsPlugin.Log?.LogInfo($"[ECO] Zonas de reverb: {zones.Length} carregada(s).");
		int inScene = 0;
		int shown = 0;
		for (int i = 0; i < zones.Length; i++)
		{
			Component zone = zones[i] as Component;
			if (zone == null || !InScene(zone))
			{
				continue;
			}
			inScene++;
			if (shown >= EcoDiagSampleSize)
			{
				continue;
			}
			shown++;
			float maxDistance = MemberFloat(zone, "maxDistance");
			CompanionsPlugin.Log?.LogInfo($"[ECO]   zona \"{zone.gameObject.name}\": preset={Member(zone, "reverbPreset")}, "
				+ $"minDistance={Member(zone, "minDistance")}, maxDistance={Member(zone, "maxDistance")}, "
				+ $"decayTime={Member(zone, "decayTime")}, room={Member(zone, "room")}, "
				+ $"dentro: jogador={(IsWithin(zone, player, maxDistance) ? "SIM" : "nao")}, "
				+ $"companheiro={(IsWithin(zone, speaker, maxDistance) ? "SIM" : "nao")}");
		}
		CompanionsPlugin.Log?.LogInfo($"[ECO] Zonas de reverb: {inScene} na cena, {shown} mostrada(s).");
	}

	/// <summary>
	/// A sample of the game's <b>own</b> AudioSources (ours is left out): this is the recipe to compare against. If
	/// the game always plays through a mixer group, or always with a bypass flag set, that is the difference we are
	/// looking for.
	/// </summary>
	private static void LogEcoSources()
	{
		Type type = Audio.SourceType;
		if (type == null)
		{
			CompanionsPlugin.Log?.LogInfo("[ECO] Fontes do jogo: o tipo AudioSource ainda nao esta resolvido.");
			return;
		}
		UnityEngine.Object[] all = FindLoaded(type);
		CompanionsPlugin.Log?.LogInfo($"[ECO] Fontes do jogo: {all.Length} AudioSource carregado(s); amostra de ate {EcoDiagSampleSize} da cena (a nossa fica de fora).");
		int inScene = 0;
		int shown = 0;
		for (int i = 0; i < all.Length; i++)
		{
			Component source = all[i] as Component;
			if (source == null || source == _source || !InScene(source))
			{
				continue;
			}
			inScene++;
			if (shown >= EcoDiagSampleSize)
			{
				continue;
			}
			shown++;
			CompanionsPlugin.Log?.LogInfo($"[ECO]   fonte \"{source.gameObject.name}\" (pai \"{ParentName(source)}\"): " + Audio.DescribeSource(source));
		}
		CompanionsPlugin.Log?.LogInfo($"[ECO] Fontes do jogo: {inScene} na cena, {shown} mostrada(s).");
	}

	/// <summary>Every loaded object of a type (<c>Resources.FindObjectsOfTypeAll</c> - non-obsolete and also sees inactive ones).</summary>
	private static UnityEngine.Object[] FindLoaded(Type type)
	{
		if (type == null)
		{
			return EmptyObjects;
		}
		try
		{
			return Resources.FindObjectsOfTypeAll(type) ?? EmptyObjects;
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log?.LogInfo($"[ECO] Nao consegui listar os objetos de {type.Name}: " + Audio.Describe(ex));
			return EmptyObjects;
		}
	}

	/// <summary>Is this object really in the loaded scene (and not a prefab/asset that only lives in memory)?</summary>
	private static bool InScene(Component component)
	{
		try
		{
			return component != null && component.gameObject.scene.IsValid();
		}
		catch (Exception)
		{
			return false;
		}
	}

	/// <summary>The first component of that type on the object, or null (the diagnosis never throws).</summary>
	private static Component FindComponent(GameObject holder, Type type)
	{
		if (holder == null || type == null)
		{
			return null;
		}
		try
		{
			return holder.GetComponent(type);
		}
		catch (Exception)
		{
			return null;
		}
	}

	/// <summary>The value of a member of an arbitrary Unity component, as text ("?" when it is not readable).</summary>
	private static string Member(object instance, string name)
	{
		object value = ReadMember(instance, name);
		if (value == null)
		{
			return "?";
		}
		try
		{
			if (value is bool flag)
			{
				return flag ? "true" : "false";
			}
			return Convert.ToString(value, CultureInfo.InvariantCulture) ?? "?";
		}
		catch (Exception)
		{
			return "?";
		}
	}

	/// <summary>The same, as a number (NaN when the member is not readable) - used for the "inside the zone" check.</summary>
	private static float MemberFloat(object instance, string name)
	{
		object value = ReadMember(instance, name);
		if (value == null)
		{
			return float.NaN;
		}
		try
		{
			return Convert.ToSingle(value, CultureInfo.InvariantCulture);
		}
		catch (Exception)
		{
			return float.NaN;
		}
	}

	/// <summary>
	/// Reads a public member (property first, then field) of an arbitrary Unity component. The diagnosis must never
	/// fail because this Unity build names something differently, so anything unreadable comes back as null.
	/// </summary>
	private static object ReadMember(object instance, string name)
	{
		if (instance == null || string.IsNullOrEmpty(name))
		{
			return null;
		}
		try
		{
			Type type = instance.GetType();
			PropertyInfo property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
			if (property != null)
			{
				return property.GetValue(instance, null);
			}
			FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
			if (field != null)
			{
				return field.GetValue(instance);
			}
		}
		catch (Exception)
		{
			// um membro ilegivel e "?" - o diagnostico nunca vira erro
		}
		return null;
	}

	/// <summary>Is the target inside the radius of a reverb zone? (an unreadable radius counts as "no").</summary>
	private static bool IsWithin(Component zone, GameObject target, float maxDistance)
	{
		if (zone == null || target == null || float.IsNaN(maxDistance) || maxDistance < 0f)
		{
			return false;
		}
		try
		{
			return (zone.transform.position - target.transform.position).sqrMagnitude <= maxDistance * maxDistance;
		}
		catch (Exception)
		{
			return false;
		}
	}

	/// <summary>The name of the object this one hangs from (it is what tells two SFX of the same name apart).</summary>
	private static string ParentName(Component component)
	{
		try
		{
			Transform parent = component.transform.parent;
			return parent != null ? parent.name : "(sem pai)";
		}
		catch (Exception)
		{
			return "?";
		}
	}

	/// <summary>How many objects each block of the echo diagnosis lists (it is a log, not a dump).</summary>
	private const int EcoDiagSampleSize = 20;

	private const string AudioListenerTypeName = "UnityEngine.AudioListener";

	private const string ReverbZoneTypeName = "UnityEngine.AudioReverbZone";

	private const string ReverbFilterTypeName = "UnityEngine.AudioReverbFilter";

	private const string LowPassFilterTypeName = "UnityEngine.AudioLowPassFilter";

	/// <summary>An empty result for <see cref="FindLoaded"/> (never null, so the callers never have to check).</summary>
	private static readonly UnityEngine.Object[] EmptyObjects = new UnityEngine.Object[0];

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
		if (samples == null || samples.Length == 0)
		{
			return;
		}
		lock (_cache)
		{
			if (!_cache.ContainsKey(key))
			{
				_cache[key] = samples;
				_cacheOrder.Enqueue(key);
				_cacheBytes += samples.Length * 4L;
			}
			TrimCache();
		}
	}

	/// <summary>
	/// Drops the oldest sentences until the cache is inside both caps. The byte cap is the one that matters (it is
	/// what keeps the Mono heap bounded); the entry cap is a hard ceiling for very short sentences. Called with the
	/// cache lock held.
	/// </summary>
	private static void TrimCache()
	{
		int removed = 0;
		while ((_cacheBytes > MaxCacheBytes || _cache.Count > MaxCacheEntries) && _cacheOrder.Count > 0)
		{
			string oldest = _cacheOrder.Dequeue();
			if (_cache.TryGetValue(oldest, out float[] dropped))
			{
				_cache.Remove(oldest);
				_cacheBytes -= dropped.Length * 4L;
				removed++;
			}
		}
		if (removed > 0)
		{
			CompanionsPlugin.Log?.LogDebug($"[TTS] Cache de falas: {removed} frase(s) mais antiga(s) descartada(s) - agora {_cache.Count} frase(s), "
				+ $"{_cacheBytes / (1024 * 1024)} MB de audio (tetos: {MaxCacheBytes / (1024 * 1024)} MB / {MaxCacheEntries} frases).");
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

	/// <summary>Multilanguage M1: the voices of one language, "id = name (f/m)" - used by the startup logs.</summary>
	private static string DescribeVoices(LocaleProfile profile)
	{
		StringBuilder builder = new StringBuilder();
		for (int i = 0; i < profile.Voices.Length; i++)
		{
			int sid = profile.Voices[i];
			if (i > 0)
			{
				builder.Append(", ");
			}
			builder.Append(sid).Append(" = ").Append(CompanionLocale.VoiceName(sid))
				.Append(" (").Append(CompanionLocale.VoiceGender(sid)).Append(')');
		}
		return builder.ToString();
	}

	/// <summary>Multilanguage M1: [AI] TtsLanguage as the .cfg has it (the resolution needs the game language, which
	/// is not known yet in the plugin's Awake).</summary>
	private static string ConfiguredLanguageText()
	{
		string value = ModConfig.TtsLanguage != null ? ModConfig.TtsLanguage.Value : null;
		return CompanionLocale.IsAuto(value) ? "auto (o do jogo)" : "\"" + value.Trim() + "\"";
	}

	/// <summary>Multilanguage M1: [AI] TtsVoice as the .cfg has it, with the name of an explicit id.</summary>
	private static string ConfiguredVoiceText()
	{
		int value = ModConfig.TtsVoice != null ? ModConfig.TtsVoice.Value : 0;
		if (value <= 0)
		{
			return "0 (auto - a voz padrao do idioma do jogo)";
		}
		string name = CompanionLocale.VoiceName(value);
		return name.Length > 0 ? $"{value} ({name})" : value.ToString();
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
		CompanionsPlugin.Log?.LogInfo($"[TTS] Teste do TTS pelo comando de console: \"{text}\""
			+ (speaker != null ? $" (voz do companheiro mais proximo: {CompanionProfile.GetVoice(speaker)})" : $" (voz padrao: {Voice})"));
		// Fase 3.10: with a companion next to the player the test speaks in ITS voice, like a normal line does
		if (speaker != null)
		{
			Speak(speaker, text);
		}
		else
		{
			Speak(talker, text);
		}
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

		// Fase 3.4 (parte 3): OPTIONAL members. The 11 above are the core (without any of them there is no voice);
		// these only drive the echo test switches and the diagnosis, so a missing one is a warning, never a failure.
		private static PropertyInfo _reverbZoneMix;

		private static PropertyInfo _bypassReverbZones;

		private static PropertyInfo _bypassEffects;

		private static PropertyInfo _bypassListenerEffects;

		private static PropertyInfo _dopplerLevel;

		private static PropertyInfo _spread;

		private static PropertyInfo _spatialize;

		private static PropertyInfo _outputAudioMixerGroup;

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

		/// <summary>
		/// Fase 3.4 (parte 3): the <b>optional</b> members of the voice's audio configuration. <see cref="SetReverbZoneMix"/>
		/// returns false when this Unity build does not have the member, and the caller says so in the log (the voice
		/// itself keeps working, which is the whole point of keeping it out of the required set above). The rest of the
		/// members are read-only here: <see cref="DescribeSource"/> samples the game's own sources with them, which is
		/// how the diagnosis found the recipe that killed the echo.
		/// </summary>
		internal static bool SetReverbZoneMix(Component source, float value)
		{
			return SetIfPresent(source, _reverbZoneMix, value);
		}

		/// <summary>The AudioSource type, already resolved (the echo diagnosis samples the game's own sources with it).</summary>
		internal static Type SourceType
		{
			get
			{
				return _sourceType;
			}
		}

		/// <summary>
		/// One line of the "game recipe" diagnosis: how one of the game's <b>own</b> sources is configured. This is
		/// the comparison that shows whether the game plays through a mixer group, or with a bypass flag set, that
		/// our source does not use.
		/// </summary>
		internal static string DescribeSource(object source)
		{
			return $"spatialBlend={Read(source, _spatialBlend)}, reverbZoneMix={Read(source, _reverbZoneMix)}, "
				+ $"bypassReverbZones={Read(source, _bypassReverbZones)}, bypassEffects={Read(source, _bypassEffects)}, "
				+ $"bypassListenerEffects={Read(source, _bypassListenerEffects)}, dopplerLevel={Read(source, _dopplerLevel)}, "
				+ $"spatialize={Read(source, _spatialize)}, mixer={MixerName(source)}";
		}

		private static void Set(Component source, PropertyInfo property, object value)
		{
			property.SetValue(source, value, null);
		}

		/// <summary>Writes an optional member; false when the member does not exist in this Unity (nothing was written).</summary>
		private static bool SetIfPresent(Component source, PropertyInfo property, object value)
		{
			if (source == null || property == null)
			{
				return false;
			}
			try
			{
				property.SetValue(source, value, null);
				return true;
			}
			catch (Exception ex)
			{
				CompanionsPlugin.Log?.LogDebug($"[TTS] Nao consegui aplicar \"{property.Name}\": " + Describe(ex));
				return false;
			}
		}

		/// <summary>Reads an optional member as text ("?" when it is missing or unreadable) - never a guess.</summary>
		private static string Read(object instance, PropertyInfo property)
		{
			if (instance == null || property == null)
			{
				return "?";
			}
			try
			{
				object value = property.GetValue(instance, null);
				if (value == null)
				{
					return "null";
				}
				if (value is bool flag)
				{
					return flag ? "true" : "false";
				}
				return Convert.ToString(value, CultureInfo.InvariantCulture) ?? "?";
			}
			catch (Exception)
			{
				return "?";
			}
		}

		/// <summary>The name of the mixer group this source outputs to (none = the AudioListener's own default).</summary>
		private static string MixerName(object source)
		{
			if (source == null || _outputAudioMixerGroup == null)
			{
				return "(sem membro)";
			}
			try
			{
				object group = _outputAudioMixerGroup.GetValue(source, null);
				if (group == null)
				{
					return "(nenhum)";
				}
				return group is UnityEngine.Object unityObject ? unityObject.name : "(?)";
			}
			catch (Exception)
			{
				return "?";
			}
		}

		/// <summary>
		/// Looks up one of the optional members. A lookup that throws (an ambiguous name, a build without it) must
		/// not reach the <c>Resolve</c> catch - that would turn a missing test switch into "no voice at all".
		/// </summary>
		private static PropertyInfo OptionalProperty(string name)
		{
			try
			{
				return _sourceType.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
			}
			catch (Exception ex)
			{
				CompanionsPlugin.Log?.LogDebug($"[TTS] Membro opcional \"{name}\" nao pode ser procurado: " + ex.Message);
				return null;
			}
		}

		/// <summary>The names of the optional members this Unity does not have, or null when it has all of them.</summary>
		private static string MissingOptional()
		{
			StringBuilder missing = new StringBuilder();
			AppendMissing(missing, "reverbZoneMix", _reverbZoneMix);
			AppendMissing(missing, "bypassReverbZones", _bypassReverbZones);
			AppendMissing(missing, "bypassEffects", _bypassEffects);
			AppendMissing(missing, "bypassListenerEffects", _bypassListenerEffects);
			AppendMissing(missing, "dopplerLevel", _dopplerLevel);
			AppendMissing(missing, "spread", _spread);
			AppendMissing(missing, "spatialize", _spatialize);
			AppendMissing(missing, "outputAudioMixerGroup", _outputAudioMixerGroup);
			return missing.Length == 0 ? null : missing.ToString();
		}

		private static void AppendMissing(StringBuilder builder, string name, PropertyInfo property)
		{
			if (property != null)
			{
				return;
			}
			if (builder.Length > 0)
			{
				builder.Append(", ");
			}
			builder.Append(name);
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
				// Fase 3.4 (parte 3): the optional ones (the echo test switches and the diagnosis). Each lookup is
				// wrapped on its own: a member this Unity build does not have must only make its switch inert, never
				// take the voice down with it.
				_reverbZoneMix = OptionalProperty("reverbZoneMix");
				_bypassReverbZones = OptionalProperty("bypassReverbZones");
				_bypassEffects = OptionalProperty("bypassEffects");
				_bypassListenerEffects = OptionalProperty("bypassListenerEffects");
				_dopplerLevel = OptionalProperty("dopplerLevel");
				_spread = OptionalProperty("spread");
				_spatialize = OptionalProperty("spatialize");
				_outputAudioMixerGroup = OptionalProperty("outputAudioMixerGroup");
				string missingOptional = MissingOptional();
				if (missingOptional != null)
				{
					CompanionsPlugin.Log?.LogWarning("[TTS] Membros de audio OPCIONAIS que este Unity nao tem "
						+ "(os interruptores de teste do eco ligados a eles ficam sem efeito): " + missingOptional);
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
		/// over the loaded assemblies if that fails. <c>internal</c> (not private) because the echo diagnosis of the
		/// outer class also needs it: the accessibility domain of a private member stops at the type that declares it.
		/// </summary>
		internal static Type FindType(string name)
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
