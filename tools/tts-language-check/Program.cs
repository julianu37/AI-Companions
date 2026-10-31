using System;
using System.Diagnostics;
using System.IO;
using SherpaOnnx;

namespace TtsLanguageCheck;

/// <summary>
/// Speaks one sentence in each of the five languages the mod has, with the model and the natives it ships, and checks
/// the voice ids of CompanionLocale against the model. Run it with the game closed:
/// <code>
/// set AI_COMPANIONS_DIR=...\BepInEx\plugins\AI-Companions
/// copy "%AI_COMPANIONS_DIR%\runtimes\win-x64\native\*.dll" bin\Release\net472
/// TtsLanguageCheck.exe
/// </code>
/// </summary>
internal static class Program
{
	private const string DefaultPluginDir =
		@"C:\Program Files (x86)\Steam\steamapps\common\Valheim\BepInEx\plugins\AI-Companions";

	private static int _fail;

	private static string _kokoroDir;

	/// <summary>What src/Companions/Core/CompanionLocale.cs says for one language.</summary>
	private sealed class Language
	{
		internal string Name;
		internal string Code;
		internal int[] Voices;
		internal string Sentence;
	}

	private static void Main()
	{
		string pluginDir = Environment.GetEnvironmentVariable("AI_COMPANIONS_DIR");
		if (string.IsNullOrWhiteSpace(pluginDir))
		{
			pluginDir = DefaultPluginDir;
		}
		_kokoroDir = Path.Combine(pluginDir, "tts", "kokoro");
		Console.WriteLine("modelo: " + _kokoroDir);
		if (!File.Exists(Path.Combine(_kokoroDir, "model.int8.onnx")))
		{
			Console.WriteLine("FALHA: nao achei model.int8.onnx - rode o build do mod (ele copia a pasta tts\\kokoro) ou");
			Console.WriteLine("        aponte AI_COMPANIONS_DIR para a pasta do plugin.");
			Environment.Exit(2);
		}

		Language[] languages =
		{
			new Language { Name = "Portugues (Brasil)", Code = "pt-br", Voices = new[] { 42, 43, 44 },
				Sentence = "Bom dia! Vou cortar madeira e voltar ja." },
			new Language { Name = "English", Code = "en-us", Voices = new[] { 3, 2, 6, 14, 16, 18 },
				Sentence = "Good morning! I'll chop some wood and be right back." },
			new Language { Name = "Espanol", Code = "es", Voices = new[] { 28, 29 },
				Sentence = "Buenos dias! Voy a cortar madera y vuelvo enseguida." },
			new Language { Name = "Francais", Code = "fr", Voices = new[] { 30 },
				Sentence = "Bonjour ! Je vais couper du bois et je reviens tout de suite." },
			new Language { Name = "Italiano", Code = "it", Voices = new[] { 35, 36 },
				Sentence = "Buongiorno! Vado a tagliare la legna e torno subito." }
		};

		Console.WriteLine();
		Console.WriteLine("=== 1. cada idioma carrega e sintetiza com a voz padrao ===");
		int speakers = -1;
		foreach (Language lang in languages)
		{
			OfflineTts tts = Load(lang.Code);
			if (tts == null)
			{
				continue;
			}
			if (speakers < 0)
			{
				speakers = tts.NumSpeakers;
			}
			int voice = lang.Voices[0];
			Check(lang.Code + ": voz padrao " + voice + " existe no modelo (" + tts.NumSpeakers + " vozes)",
				voice < tts.NumSpeakers, "voz " + voice + " >= " + tts.NumSpeakers);
			Generate(tts, lang.Sentence, voice, lang.Code + " voz " + voice);
			Dispose(tts);
		}

		Console.WriteLine();
		Console.WriteLine("=== 2. todas as vozes que o painel oferece existem no modelo ===");
		foreach (Language lang in languages)
		{
			foreach (int voice in lang.Voices)
			{
				Check(lang.Name + " voz " + voice + " < NumSpeakers (" + speakers + ")", speakers > 0 && voice < speakers,
					"NumSpeakers=" + speakers);
			}
		}

		Console.WriteLine();
		Console.WriteLine("=== 3. o idioma muda MESMO o audio (por isso o modelo e recarregado quando ele muda) ===");
		long samplesPt = 0;
		long samplesEn = 0;
		string text = "Bom dia! Vou cortar madeira e voltar ja.";
		OfflineTts pt = Load("pt-br");
		if (pt != null)
		{
			OfflineTtsGeneratedAudio audio = Generate(pt, text, 42, "texto PT com lang=pt-br");
			if (audio != null)
			{
				samplesPt = audio.NumSamples;
			}
			Dispose(pt);
		}
		OfflineTts en = Load("en-us");
		if (en != null)
		{
			OfflineTtsGeneratedAudio audio = Generate(en, text, 42, "texto PT com lang=en-us");
			if (audio != null)
			{
				samplesEn = audio.NumSamples;
			}
			Dispose(en);
		}
		Check("o mesmo texto em pt-br e en-us gera audio diferente", samplesPt > 0 && samplesEn > 0 && samplesPt != samplesEn,
			samplesPt + " vs " + samplesEn);

		Console.WriteLine();
		Console.WriteLine("=== 4. os codigos que o F8 NAO oferece ===");
		string[][] extras =
		{
			new[] { "en-gb", "Good morning! I'll chop some wood.", "21" },
			new[] { "fr-fr", "Bonjour ! Je vais couper du bois et je reviens tout de suite.", "30" },
			new[] { "pt", "Bom dia! Vou cortar madeira e voltar ja.", "42" }
		};
		foreach (string[] extra in extras)
		{
			OfflineTts tts = Load(extra[0]);
			if (tts == null)
			{
				continue;
			}
			// en-gb/fr-fr are expected to FAIL here (the binding throws on Generate); pt is expected to work. Either way
			// this section is information, not a regression - the five languages above are what the release guarantees.
			Generate(tts, extra[1], int.Parse(extra[2]), extra[0] + " voz " + extra[2],
				expectSuccess: extra[0] == "pt");
			Dispose(tts);
		}

		Console.WriteLine();
		Console.WriteLine(_fail == 0 ? "TODOS OS TESTES PASSARAM" : (_fail + " FALHA(S)"));
		Environment.Exit(_fail == 0 ? 0 : 1);
	}

	private static OfflineTts Load(string code)
	{
		try
		{
			OfflineTtsConfig config = new OfflineTtsConfig();
			config.Model = new OfflineTtsModelConfig();
			config.Model.Kokoro = new OfflineTtsKokoroModelConfig();
			config.RuleFsts = "";
			config.RuleFars = "";
			config.MaxNumSentences = 1;
			config.SilenceScale = 0.2f;
			config.Model.NumThreads = 4;
			config.Model.Debug = 0;
			config.Model.Provider = "cpu";
			config.Model.Kokoro.Model = Path.Combine(_kokoroDir, "model.int8.onnx");
			config.Model.Kokoro.Voices = Path.Combine(_kokoroDir, "voices.bin");
			config.Model.Kokoro.Tokens = Path.Combine(_kokoroDir, "tokens.txt");
			config.Model.Kokoro.DataDir = Path.Combine(_kokoroDir, "espeak-ng-data");
			config.Model.Kokoro.Lang = code;
			Stopwatch watch = Stopwatch.StartNew();
			OfflineTts tts = new OfflineTts(config);
			watch.Stop();
			bool ok = tts.NumSpeakers > 0;
			Check("carrega com lang=\"" + code + "\"", ok, "NumSpeakers=" + tts.NumSpeakers);
			Console.WriteLine("   [" + code + "] carregado em " + watch.ElapsedMilliseconds + " ms, " + tts.NumSpeakers
				+ " vozes, saida " + tts.SampleRate + " Hz");
			if (!ok)
			{
				Dispose(tts);
				return null;
			}
			return tts;
		}
		catch (Exception ex)
		{
			Check("carrega com lang=\"" + code + "\"", false, ex.GetType().Name + ": " + ex.Message);
			return null;
		}
	}

	private static OfflineTtsGeneratedAudio Generate(OfflineTts tts, string text, int voice, string what, bool expectSuccess = true)
	{
		try
		{
			Stopwatch watch = Stopwatch.StartNew();
			OfflineTtsGeneratedAudio audio = tts.Generate(text, 1.0f, voice);
			watch.Stop();
			if (audio == null || audio.Handle == IntPtr.Zero || audio.NumSamples <= 0)
			{
				Report(what + " gera audio", false, expectSuccess, audio == null ? "audio=null" : "amostras=" + audio.NumSamples);
				return null;
			}
			double seconds = (double)audio.NumSamples / audio.SampleRate;
			double rtf = seconds > 0 ? watch.Elapsed.TotalSeconds / seconds : 0.0;
			Console.WriteLine("   " + what + ": " + audio.NumSamples + " amostras, " + seconds.ToString("0.00")
				+ " s de audio, " + watch.ElapsedMilliseconds + " ms, RTF " + rtf.ToString("0.00"));
			Report(what + " gera audio", true, expectSuccess, "");
			return audio;
		}
		catch (Exception ex)
		{
			Report(what + " gera audio", false, expectSuccess, ex.GetType().Name + ": " + ex.Message);
			return null;
		}
	}

	/// <summary>A check that may be expected to fail (the codes the F8 does not offer).</summary>
	private static void Report(string what, bool ok, bool expectSuccess, string detail)
	{
		if (ok == expectSuccess)
		{
			Console.WriteLine((expectSuccess ? "ok    " : "ok    ") + what + (expectSuccess ? "" : " (falhou, como documentado)"));
			return;
		}
		if (!expectSuccess)
		{
			// it worked on this build: useful information, not a regression
			Console.WriteLine("INFO  " + what + " (funcionou nesta build - a lista do F8 pode ser revista)");
			return;
		}
		_fail++;
		Console.WriteLine("FAIL  " + what + "   [" + detail + "]");
	}

	private static void Dispose(OfflineTts tts)
	{
		try
		{
			tts.Dispose();
		}
		catch (Exception)
		{
		}
	}

	private static void Check(string what, bool ok, string detail)
	{
		if (ok)
		{
			Console.WriteLine("ok    " + what);
			return;
		}
		_fail++;
		Console.WriteLine("FAIL  " + what + "   [" + detail + "]");
	}
}
