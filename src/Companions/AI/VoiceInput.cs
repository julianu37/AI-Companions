using System;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace Companions;

/// <summary>
/// Fase 3.3: voice orders - push-to-talk.
///
/// Hold <c>[IA] VozTecla</c> (V by default) to talk, release it to send what you said: the recording is trimmed,
/// encoded as a WAV (PCM 16-bit mono at 16 kHz, the format Whisper expects) and handed to <see cref="WhisperStt"/>,
/// which transcribes it on a background thread. The text then goes into the <b>same order box as the chat</b>
/// (<see cref="CompanionChatAI.Submit"/>): orders, conversation, personality, memory - all identical to typing it.
///
/// Fase 3.3b: V is also the game's "Toggle Auto-Pickup" key. While <c>[IA] VozLigada</c> is on and the game's
/// auto-pickup is bound to the same key, the mod swallows that toggle (<see cref="AutoPickupKeyConflict"/>) so the key
/// belongs to the push-to-talk - the player can move the auto-pickup to another key in the game's own controls screen.
///
/// What this class does per frame while idle: drains finished transcriptions, reads the key and, when it is pressed,
/// runs the guards below. Nothing here allocates or blocks: the microphone only runs while the key is held, and the
/// only heavy work (the transcription) is off the main thread.
///
/// Guards (why a press may do nothing):
/// <list type="bullet">
/// <item>no microphone, or the model file is missing (<c>[IA] SttModelo</c>);</item>
/// <item>the game is not focused, or a window is open (inventory, map, menu, chat, console, the mod's own panels) -
/// so talking never types into the chat or walks the character;</item>
/// <item>a previous transcription or a previous order to the model is still running (one at a time).</item>
/// </list>
/// </summary>
internal static class VoiceInput
{
	/// <summary>Everything in this pipeline runs at 16 kHz mono - what Whisper expects.</summary>
	private const int SampleRate = WhisperStt.SampleRate;

	/// <summary>10 ms windows when looking for the speech inside the recording.</summary>
	private const int WindowSamples = SampleRate / 100;

	/// <summary>
	/// 0.2 s kept before and after the speech, so the trim never clips a word. Fase 3.3b: this was 0.125 s and short
	/// clips came back with the first syllable missing ("O leta deles." for a 0.7 s recording), which is exactly what a
	/// pad that is too short looks like - the trim starts at the first window that crosses the threshold, and the
	/// onset of a word ("co-", "ata-") is often still below it.
	/// </summary>
	private const int PadSamples = SampleRate / 5;

	/// <summary>Anything shorter than ~0.33 s of speech is a click or a cough, not an order.</summary>
	private const int MinSpeechSamples = SampleRate / 3;

	/// <summary>The microphone clip, as an <c>object</c>: see <see cref="Mic"/> for why it is not typed.</summary>
	private static object _clip;

	private static string _device;

	private static bool _deviceResolved;

	private static bool _recording;

	private static float _recordStartedAt;

	private static int _maxSeconds = 10;

	private static bool _prewarmRequested;

	private static bool _warnedNoMic;

	private static bool _warnedMissingModel;

	/// <summary>Scratch buffers, reused between recordings (the sizes are fixed by the clip length).</summary>
	private static float[] _raw;

	private static float[] _mono;

	/// <summary>[IA] VozLigada.</summary>
	internal static bool Enabled => ModConfig.VozEnabled == null || ModConfig.VozEnabled.Value;

	/// <summary>[IA] VozTecla - the push-to-talk key (V by default). Also read by <see cref="AutoPickupKeyConflict"/>.</summary>
	internal static KeyCode Key => ModConfig.VozTecla != null ? ModConfig.VozTecla.Value : KeyCode.V;

	/// <summary>Called once from the plugin's Awake (main thread): reports the key, the microphones and the model.</summary>
	internal static void Init()
	{
		try
		{
			KeyCode key = Key;
			string[] devices = Mic.Available ? Mic.Devices() : null;
			CompanionsPlugin.Log?.LogInfo($"[STT] Push-to-talk: {(Enabled ? "LIGADO" : "desligado ([IA] VozLigada = false)")} | "
				+ $"segure {key} para falar | {devices?.Length ?? 0} microfone(s){DescribeDevices(devices)} | "
				+ $"audio {SampleRate} Hz mono -> Whisper, texto no mesmo caminho das ordens do chat.");
			if (!Mic.Available)
			{
				CompanionsPlugin.Log?.LogWarning("[STT] Nao consegui acessar o microfone: " + Mic.Error);
			}
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log?.LogWarning("[STT] Init do push-to-talk falhou: " + ex);
		}
	}

	/// <summary>
	/// Called every frame from the plugin's Update. Drains the finished transcriptions and runs the push-to-talk.
	/// </summary>
	internal static void Tick()
	{
		try
		{
			WhisperStt.Tick();
			while (WhisperStt.TryDequeue(out SttResult result))
			{
				WhisperStt.MarkDone();
				HandleTranscript(result);
			}

			if (!Enabled || Player.m_localPlayer == null)
			{
				return;
			}
			if (!_prewarmRequested)
			{
				// in the world: load the model now, in the background, so the first order is not slower
				_prewarmRequested = true;
				WhisperStt.Prewarm();
			}

			if (_recording)
			{
				if (!Application.isFocused)
				{
					CancelRecording("o jogo perdeu o foco");
					return;
				}
				if (!Input.GetKey(Key))
				{
					StopRecording(cutByTime: false);
				}
				else if (UnityEngine.Time.realtimeSinceStartup - _recordStartedAt >= _maxSeconds)
				{
					StopRecording(cutByTime: true);
				}
				return;
			}

			if (!Input.GetKeyDown(Key) || !Application.isFocused)
			{
				return;
			}
			if (BlockedByUi())
			{
				Announce("hc_ai_voice_ui_open");
				return;
			}
			StartRecording();
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log?.LogWarning("[STT] Erro no push-to-talk: " + ex);
			CancelRecording(null);
		}
	}

	/// <summary>Stops any recording in progress (plugin shutdown).</summary>
	internal static void Shutdown()
	{
		if (_recording)
		{
			CancelRecording(null);
		}
	}

	/// <summary>
	/// Any open window means "not talking now": the same list the mod's command key uses, plus the mod's own panels.
	/// Cheap on purpose - it only runs on the frame the key goes down.
	/// </summary>
	private static bool BlockedByUi()
	{
		if (InventoryGui.IsVisible() || Minimap.IsOpen() || Menu.IsVisible())
		{
			return true;
		}
		if (TextInput.IsVisible() || Console.IsVisible() || StoreGui.IsVisible())
		{
			return true;
		}
		if (Chat.instance != null && Chat.instance.HasFocus())
		{
			return true;
		}
		if (Hud.IsPieceSelectionVisible())
		{
			return true;
		}
		if (CompanionRadialMenu.Instance != null && CompanionRadialMenu.Instance.IsVisible)
		{
			return true;
		}
		if (CompanionInteractPanel.Instance != null && CompanionInteractPanel.Instance.IsVisible)
		{
			return true;
		}
		if (ConfigPanel.Instance != null && ConfigPanel.Instance.IsVisible)
		{
			return true;
		}
		if (FarmZonePlacer.Instance != null && FarmZonePlacer.Instance.IsVisible)
		{
			return true;
		}
		// Fase 3.7: the AI panel owns the keyboard while it is open (the push-to-talk key is one of its controls)
		if (CompanionAiPanel.Instance != null && CompanionAiPanel.Instance.IsVisible)
		{
			return true;
		}
		if (FarmZoneCropPicker.IsOpen)
		{
			return true;
		}
		// Fase 3.19 (auditoria): the starter panel and the home zone panel own the keyboard too - they were missing
		// here, so push-to-talk started recording under them (and the voice path could see the keys they use)
		if (StarterCompanionPanel.Instance != null && StarterCompanionPanel.Instance.IsVisible)
		{
			return true;
		}
		if (HomeZonePanel.Instance != null && HomeZonePanel.Instance.IsVisible)
		{
			return true;
		}
		// Fase 3.20: the appearance editor of an existing companion owns the keyboard too (it has no text box, but the
		// push-to-talk key must not start a recording under it)
		if (CompanionAppearancePanel.IsOpen)
		{
			return true;
		}
		return false;
	}

	private static void StartRecording()
	{
		if (ModConfig.AiEnabled == null || !ModConfig.AiEnabled.Value)
		{
			Announce("hc_ai_disabled");
			return;
		}
		if (!EnsureDevice())
		{
			Announce("hc_ai_voice_no_mic");
			return;
		}
		if (WhisperStt.Busy || LlmClient.Busy)
		{
			Announce("hc_ai_voice_busy");
			return;
		}
		if (!WhisperStt.Ready)
		{
			if (!_warnedMissingModel)
			{
				_warnedMissingModel = true;
				CompanionsPlugin.Log?.LogWarning($"[STT] O modelo do Whisper nao esta em \"{WhisperStt.ModelPath}\" - as ordens por voz nao funcionam. "
					+ "Coloque o ggml (ex.: ggml-small-q5_1.bin) na pasta whisper\\ do plugin ou ajuste [IA] SttModelo.");
			}
			Announce("hc_ai_voice_unavailable");
			return;
		}

		_maxSeconds = Mathf.Clamp(Mathf.RoundToInt(ModConfig.VozMaxSeconds != null ? ModConfig.VozMaxSeconds.Value : 10f), 1, 30);
		object clip = null;
		try
		{
			// 16 kHz MONO: the format Whisper expects (the old Thrall recorded 8-bit at 22050 Hz and had to convert)
			clip = Mic.Start(_device, _maxSeconds, SampleRate);
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log?.LogWarning("[STT] Microphone.Start falhou: " + Mic.Describe(ex));
		}
		if (clip == null)
		{
			Announce("hc_ai_voice_no_mic");
			return;
		}
		_clip = clip;
		_recording = true;
		_recordStartedAt = UnityEngine.Time.realtimeSinceStartup;
		int frequency = Mic.Frequency(clip);
		int channels = Mic.Channels(clip);
		if (frequency != SampleRate || channels != 1)
		{
			CompanionsPlugin.Log?.LogInfo($"[STT] O microfone deu um clipe de {frequency} Hz com {channels} canal(is) - "
				+ $"vai ser convertido para {SampleRate} Hz mono antes do Whisper.");
		}
		Announce("hc_ai_voice_listening");
	}

	/// <summary>
	/// Key released (or the cap reached): stop the microphone, cut the silence off both ends, encode the WAV and hand
	/// it to the STT. The clip is destroyed here - a microphone clip that is never destroyed is a leak.
	/// </summary>
	private static void StopRecording(bool cutByTime)
	{
		_recording = false;
		int recorded = 0;
		try
		{
			recorded = Mic.GetPosition(_device);
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log?.LogDebug("[STT] Microphone.GetPosition falhou: " + Mic.Describe(ex));
		}
		try
		{
			Mic.End(_device);
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log?.LogDebug("[STT] Microphone.End falhou: " + Mic.Describe(ex));
		}

		object clip = _clip;
		_clip = null;
		if (clip == null)
		{
			return;
		}
		try
		{
			if (cutByTime)
			{
				CompanionChatAI.Announce(ModLocalization.LocFmt("hc_ai_voice_timeout", _maxSeconds));
			}
			float[] samples = ReadSamples(clip, recorded);
			if (samples == null || samples.Length == 0)
			{
				Announce("hc_ai_voice_nothing");
				return;
			}
			int frequency = Mic.Frequency(clip);
			if (frequency != SampleRate)
			{
				samples = Resample(samples, frequency, SampleRate);
			}
			float threshold = ModConfig.VozSilenceThreshold != null ? ModConfig.VozSilenceThreshold.Value : 0.01f;
			float[] speech = TrimSilence(samples, threshold);
			if (speech == null)
			{
				CompanionsPlugin.Log?.LogInfo($"[STT] Nenhuma fala acima do limiar de silencio ({threshold:0.###}) nos {samples.Length} quadros gravados.");
				Announce("hc_ai_voice_nothing");
				return;
			}
			if (speech.Length < MinSpeechSamples)
			{
				CompanionsPlugin.Log?.LogInfo($"[STT] Gravacao curta demais ({speech.Length} de {SampleRate} amostras = {speech.Length / (float)SampleRate:0.00} s).");
				Announce("hc_ai_voice_short");
				return;
			}
			byte[] wav = EncodeWav(speech, SampleRate);
			CompanionsPlugin.Log?.LogInfo($"[STT] Audio gravado: {speech.Length / (float)SampleRate:0.0} s ({speech.Length} amostras), "
				+ $"WAV PCM 16-bit mono {SampleRate} Hz, {wav.Length / 1024} KB.");
			if (!WhisperStt.Request(wav, "push-to-talk"))
			{
				Announce("hc_ai_voice_busy");
			}
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log?.LogWarning("[STT] Falha ao preparar o audio gravado: " + Mic.Describe(ex));
			Announce("hc_ai_voice_error", Mic.Describe(ex));
		}
		finally
		{
			DestroyClip(clip);
		}
	}

	private static void CancelRecording(string reason)
	{
		_recording = false;
		if (!string.IsNullOrEmpty(reason))
		{
			CompanionsPlugin.Log?.LogInfo("[STT] Gravacao cancelada (" + reason + ").");
		}
		try
		{
			if (Mic.IsRecording(_device))
			{
				Mic.End(_device);
			}
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log?.LogDebug("[STT] Microphone.End falhou no cancelamento: " + Mic.Describe(ex));
		}
		object clip = _clip;
		_clip = null;
		DestroyClip(clip);
		if (!string.IsNullOrEmpty(reason))
		{
			Announce("hc_ai_voice_cancelled");
		}
	}

	/// <summary>
	/// The text the microphone produced goes into the SAME order box the chat uses - nothing is duplicated here.
	/// The recognised text is shown first (so a wrong transcription is visible), then the order runs.
	/// </summary>
	private static void HandleTranscript(SttResult result)
	{
		if (result == null)
		{
			return;
		}
		if (!result.Ok)
		{
			CompanionsPlugin.Log?.LogWarning($"[STT] A transcricao falhou: {result.Error}");
			CompanionChatAI.Announce(ModLocalization.LocFmt("hc_ai_voice_error", result.Error ?? "?"));
			return;
		}
		string text = result.Text == null ? "" : result.Text.Trim();
		if (text.Length == 0)
		{
			Announce("hc_ai_voice_nothing");
			return;
		}
		if (ModConfig.VozShowText == null || ModConfig.VozShowText.Value)
		{
			CompanionChatAI.HeardByVoice(text);
		}
		CompanionChatAI.Submit(text, "voz", announceRefusal: true);
	}

	/// <summary>The microphone for this session: [IA] VozDispositivo (0 = the first, the Windows default).</summary>
	private static bool EnsureDevice()
	{
		try
		{
			if (!Mic.Available)
			{
				if (!_warnedNoMic)
				{
					_warnedNoMic = true;
					CompanionsPlugin.Log?.LogWarning("[STT] Sem acesso ao microfone: " + Mic.Error);
				}
				_device = null;
				return false;
			}
			string[] devices = Mic.Devices();
			if (devices == null || devices.Length == 0)
			{
				if (!_warnedNoMic)
				{
					_warnedNoMic = true;
					CompanionsPlugin.Log?.LogWarning("[STT] Nenhum microfone encontrado (Microphone.devices vazio) - as ordens por voz nao funcionam.");
				}
				_device = null;
				return false;
			}
			if (!string.IsNullOrEmpty(_device) && Array.IndexOf(devices, _device) >= 0)
			{
				return true;
			}
			int index = ModConfig.VozDevice != null ? ModConfig.VozDevice.Value : 0;
			if (index < 0 || index >= devices.Length)
			{
				CompanionsPlugin.Log?.LogWarning($"[STT] [IA] VozDispositivo = {index} nao existe ({devices.Length} microfone(s)) - usando o primeiro.");
				index = 0;
			}
			_device = devices[index];
			if (!_deviceResolved)
			{
				_deviceResolved = true;
				CompanionsPlugin.Log?.LogInfo($"[STT] Microfone escolhido: #{index} \"{_device}\" de {devices.Length}{DescribeDevices(devices)}");
			}
			return true;
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log?.LogWarning("[STT] Falha ao listar os microfones: " + Mic.Describe(ex));
			return false;
		}
	}

	/// <summary>
	/// The recorded part of the clip as mono floats at the clip's own rate. The microphone clip is a fixed-size ring
	/// buffer, so only the samples up to <c>Microphone.GetPosition</c> are real - the rest is silence.
	/// </summary>
	private static float[] ReadSamples(object clip, int recorded)
	{
		int channels = Mathf.Max(1, Mic.Channels(clip));
		int total = Mic.Samples(clip) * channels;
		if (total <= 0)
		{
			return null;
		}
		int wanted = recorded > 0 ? Mathf.Clamp(recorded * channels, 0, total) : total;
		if (wanted <= 0)
		{
			return null;
		}
		if (_raw == null || _raw.Length < total)
		{
			_raw = new float[total];
		}
		if (!Mic.GetData(clip, _raw, 0))
		{
			return null;
		}
		if (channels == 1)
		{
			if (wanted == total)
			{
				return _raw;
			}
			float[] cut = new float[wanted];
			Array.Copy(_raw, 0, cut, 0, wanted);
			return cut;
		}
		int frames = wanted / channels;
		if (_mono == null || _mono.Length < frames)
		{
			_mono = new float[frames];
		}
		for (int frame = 0; frame < frames; frame++)
		{
			float sum = 0f;
			int at = frame * channels;
			for (int channel = 0; channel < channels; channel++)
			{
				sum += _raw[at + channel];
			}
			_mono[frame] = sum / channels;
		}
		float[] output = new float[frames];
		Array.Copy(_mono, 0, output, 0, frames);
		return output;
	}

	/// <summary>
	/// Cuts the silence off both ends: scans 10 ms windows, keeps everything between the first and the last window
	/// whose peak reaches <paramref name="threshold"/>, plus a small pad. Null when nothing was loud enough.
	/// </summary>
	private static float[] TrimSilence(float[] samples, float threshold)
	{
		int first = -1;
		int last = -1;
		for (int start = 0; start < samples.Length; start += WindowSamples)
		{
			int end = Math.Min(start + WindowSamples, samples.Length);
			float peak = 0f;
			for (int i = start; i < end; i++)
			{
				float value = samples[i];
				if (value < 0f)
				{
					value = -value;
				}
				if (value > peak)
				{
					peak = value;
				}
			}
			if (peak >= threshold)
			{
				if (first < 0)
				{
					first = start;
				}
				last = end;
			}
		}
		if (first < 0)
		{
			return null;
		}
		int from = Math.Max(0, first - PadSamples);
		int to = Math.Min(samples.Length, last + PadSamples);
		float[] trimmed = new float[to - from];
		Array.Copy(samples, from, trimmed, 0, trimmed.Length);
		return trimmed;
	}

	/// <summary>Linear resampling, used only when the microphone ignores the 16 kHz request.</summary>
	private static float[] Resample(float[] samples, int fromRate, int toRate)
	{
		if (fromRate <= 0 || toRate <= 0 || fromRate == toRate)
		{
			return samples;
		}
		int count = (int)((long)samples.Length * toRate / fromRate);
		if (count <= 0)
		{
			return samples;
		}
		float[] output = new float[count];
		double step = (double)fromRate / toRate;
		for (int i = 0; i < count; i++)
		{
			double position = i * step;
			int index = (int)position;
			float fraction = (float)(position - index);
			float a = samples[index];
			float b = index + 1 < samples.Length ? samples[index + 1] : a;
			output[i] = a + (b - a) * fraction;
		}
		return output;
	}

	/// <summary>
	/// WAV PCM 16-bit mono, written by hand (44-byte canonical header + little-endian samples): this is the format
	/// <see cref="WhisperStt"/> reads back, and it keeps the recording in memory - no temporary file on disk.
	/// </summary>
	private static byte[] EncodeWav(float[] samples, int sampleRate)
	{
		const int header = 44;
		byte[] buffer = new byte[header + samples.Length * 2];
		WriteAscii(buffer, 0, "RIFF");
		WriteInt(buffer, 4, 36 + samples.Length * 2);
		WriteAscii(buffer, 8, "WAVE");
		WriteAscii(buffer, 12, "fmt ");
		WriteInt(buffer, 16, 16);
		WriteShort(buffer, 20, 1);
		WriteShort(buffer, 22, 1);
		WriteInt(buffer, 24, sampleRate);
		WriteInt(buffer, 28, sampleRate * 2);
		WriteShort(buffer, 32, 2);
		WriteShort(buffer, 34, 16);
		WriteAscii(buffer, 36, "data");
		WriteInt(buffer, 40, samples.Length * 2);
		for (int i = 0; i < samples.Length; i++)
		{
			float value = samples[i];
			if (value > 1f)
			{
				value = 1f;
			}
			else if (value < -1f)
			{
				value = -1f;
			}
			short sample = (short)(value * 32767f);
			buffer[header + i * 2] = (byte)(sample & 0xFF);
			buffer[header + i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
		}
		return buffer;
	}

	private static void WriteAscii(byte[] buffer, int offset, string text)
	{
		for (int i = 0; i < text.Length; i++)
		{
			buffer[offset + i] = (byte)text[i];
		}
	}

	private static void WriteInt(byte[] buffer, int offset, int value)
	{
		buffer[offset] = (byte)(value & 0xFF);
		buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
		buffer[offset + 2] = (byte)((value >> 16) & 0xFF);
		buffer[offset + 3] = (byte)((value >> 24) & 0xFF);
	}

	private static void WriteShort(byte[] buffer, int offset, int value)
	{
		buffer[offset] = (byte)(value & 0xFF);
		buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
	}

	private static void DestroyClip(object clip)
	{
		if (clip == null)
		{
			return;
		}
		try
		{
			Mic.Destroy(clip);
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log?.LogDebug("[STT] Falha ao destruir o clipe do microfone: " + ex.Message);
		}
	}

	private static string DescribeDevices(string[] devices)
	{
		if (devices == null || devices.Length == 0)
		{
			return "";
		}
		StringBuilder builder = new StringBuilder();
		builder.Append(" (");
		for (int i = 0; i < devices.Length; i++)
		{
			if (i > 0)
			{
				builder.Append(", ");
			}
			builder.Append(i).Append(": ").Append(devices[i]);
		}
		return builder.Append(')').ToString();
	}

	private static void Announce(string key)
	{
		CompanionChatAI.Announce(ModLocalization.Loc(key));
	}

	private static void Announce(string key, object arg)
	{
		CompanionChatAI.Announce(ModLocalization.LocFmt(key, arg));
	}

	/// <summary>
	/// The whole <c>UnityEngine.AudioModule</c> surface this mod uses - <c>Microphone</c> and <c>AudioClip</c> -
	/// reached by reflection, and looked up <b>once</b> (every call here happens on a key press, never per frame).
	///
	/// Why not a plain reference: this mod compiles for net472 and references the Whisper.net package, whose
	/// netstandard2.0 assets pin the netstandard <b>2.0</b> facade. Every Unity 6 module is built against
	/// netstandard <b>2.1</b>, and a module that is not already in the game's reference closure is a hard CS1705
	/// against that older facade (AudioModule is not - CoreModule/AIModule/AnimationModule/... are, through
	/// assembly_valheim/Assembly-CSharp, which is why they can be referenced directly).
	///
	/// Pointing netstandard at the game's own 2.1 facade fixes CS1705 but breaks the other way: with that facade
	/// <c>AudioClip.GetData(Span&lt;float&gt;, int)</c> resolves <c>Span&lt;T&gt;</c> to mscorlib (CS7069), and the
	/// .NET Framework reference assemblies have no Span. Referencing nothing and looking the 8 members up at runtime
	/// is the only option that keeps the build clean without dragging a second BCL into the game's Mono.
	/// </summary>
	private static class Mic
	{
		private const string MicrophoneTypeName = "UnityEngine.Microphone";

		private const string AudioClipTypeName = "UnityEngine.AudioClip";

		private static readonly Type[] StartSignature = new Type[4]
		{
			typeof(string),
			typeof(bool),
			typeof(int),
			typeof(int)
		};

		private static readonly Type[] DeviceSignature = new Type[1] { typeof(string) };

		private static readonly Type[] GetDataSignature = new Type[2]
		{
			typeof(float[]),
			typeof(int)
		};

		private static bool _resolved;

		private static int _attempts;

		private static string _error = "nao resolvido";

		private static Type _microphone;

		private static Type _audioClip;

		private static PropertyInfo _devices;

		private static MethodInfo _start;

		private static MethodInfo _end;

		private static MethodInfo _isRecording;

		private static MethodInfo _getPosition;

		private static PropertyInfo _samples;

		private static PropertyInfo _channels;

		private static PropertyInfo _frequency;

		private static MethodInfo _getData;

		internal static bool Available
		{
			get
			{
				Resolve();
				return _microphone != null && _audioClip != null;
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

		internal static string[] Devices()
		{
			return (string[])_devices.GetValue(null, null);
		}

		internal static object Start(string device, int seconds, int frequency)
		{
			return _start.Invoke(null, new object[4] { device, false, seconds, frequency });
		}

		internal static void End(string device)
		{
			_end.Invoke(null, new object[1] { device });
		}

		internal static bool IsRecording(string device)
		{
			return (bool)_isRecording.Invoke(null, new object[1] { device });
		}

		internal static int GetPosition(string device)
		{
			return (int)_getPosition.Invoke(null, new object[1] { device });
		}

		internal static int Samples(object clip)
		{
			return (int)_samples.GetValue(clip, null);
		}

		internal static int Channels(object clip)
		{
			return (int)_channels.GetValue(clip, null);
		}

		internal static int Frequency(object clip)
		{
			return (int)_frequency.GetValue(clip, null);
		}

		internal static bool GetData(object clip, float[] data, int offset)
		{
			return (bool)_getData.Invoke(clip, new object[2] { data, offset });
		}

		internal static void Destroy(object clip)
		{
			if (clip is UnityEngine.Object unityObject)
			{
				UnityEngine.Object.Destroy(unityObject);
			}
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
			// a failure is not permanent: the audio module is a Unity module, and the first look can happen before it
			// is loaded (the plugin's Awake). Three attempts, then it stays off (and the reason is already in the log).
			if (_resolved || _attempts >= 3)
			{
				return;
			}
			_attempts++;
			try
			{
				_microphone = FindType(MicrophoneTypeName);
				_audioClip = FindType(AudioClipTypeName);
				if (_microphone == null || _audioClip == null)
				{
					Fail($"tipo nao encontrado ({MicrophoneTypeName}={_microphone != null}, {AudioClipTypeName}={_audioClip != null})");
					return;
				}
				const BindingFlags Flags = BindingFlags.Public | BindingFlags.Static;
				_devices = _microphone.GetProperty("devices", Flags);
				_start = _microphone.GetMethod("Start", Flags, null, StartSignature, null);
				_end = _microphone.GetMethod("End", Flags, null, DeviceSignature, null);
				_isRecording = _microphone.GetMethod("IsRecording", Flags, null, DeviceSignature, null);
				_getPosition = _microphone.GetMethod("GetPosition", Flags, null, DeviceSignature, null);
				_samples = _audioClip.GetProperty("samples", BindingFlags.Public | BindingFlags.Instance);
				_channels = _audioClip.GetProperty("channels", BindingFlags.Public | BindingFlags.Instance);
				_frequency = _audioClip.GetProperty("frequency", BindingFlags.Public | BindingFlags.Instance);
				_getData = _audioClip.GetMethod("GetData", BindingFlags.Public | BindingFlags.Instance, null, GetDataSignature, null);
				if (_devices == null || _start == null || _end == null || _isRecording == null || _getPosition == null
					|| _samples == null || _channels == null || _frequency == null || _getData == null)
				{
					Fail("membro da API do microfone nao encontrado (a API do Unity mudou?)");
					return;
				}
				_resolved = true;
				_error = null;
				CompanionsPlugin.Log?.LogInfo($"[STT] Microfone acessivel por reflexao ({_microphone.Assembly.GetName().Name} "
					+ $"{_microphone.Assembly.GetName().Version}): Microphone.{{devices,Start,End,IsRecording,GetPosition}} + AudioClip.{{samples,channels,frequency,GetData}}");
			}
			catch (Exception ex)
			{
				Fail(ex.Message);
			}
		}

		private static void Fail(string reason)
		{
			_microphone = null;
			_audioClip = null;
			_error = reason;
			if (_attempts == 1)
			{
				CompanionsPlugin.Log?.LogWarning("[STT] Nao consegui acessar o microfone do Unity: " + reason);
			}
		}

		/// <summary>
		/// The type by its assembly-qualified name first (the game's audio module is loaded at startup), and by a walk
		/// over the loaded assemblies if that fails - never a hard failure if the module is somewhere unexpected.
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
