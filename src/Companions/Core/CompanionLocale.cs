using System;

namespace Companions;

/// <summary>
/// Multilanguage M1: everything the mod needs to know about ONE language, resolved from the game language in a single
/// place - the Whisper code, the Kokoro code, the voices of that language (the default one first) and the vocabulary
/// that biases the speech recognition. Built once per language (<see cref="CompanionLocale.Current"/>) and cached;
/// <see cref="CompanionLocale.Invalidate"/> drops it on a language change or scene load. Never resolved per frame.
/// </summary>
internal sealed class LocaleProfile
{
	/// <summary>Name for the log only ("Portugues (Brasil)", "English", "Espanol", "Francais").</summary>
	internal string Name;

	/// <summary>Whisper language code ("pt", "en", "es", "fr").</summary>
	internal string WhisperCode;

	/// <summary>Kokoro language code ("pt-br", "en-us", "es", "fr") - the model is multilingual, this is what picks
	/// the phonemizer (see the kokoro skill: a voice of another language sounds wrong with it).</summary>
	internal string KokoroCode;

	/// <summary>Kokoro speaker ids of this language, the <b>default one first</b> and then the best rated.</summary>
	internal int[] Voices;

	/// <summary>The voice used when the player has no choice of their own (<c>[AI] TtsVoice = 0</c>). = Voices[0].</summary>
	internal int DefaultVoice;

	/// <summary>Vocabulary sent to Whisper as the initial prompt, written in this language.</summary>
	internal string SpeechPrompt;
}

/// <summary>
/// Multilanguage M1: the ONE table that answers "what language is the game in, and what does the mod use for it?",
/// replacing the hard-coded Portuguese that the STT and the TTS carried (<c>[AI] VoiceLanguage = "pt"</c>,
/// <c>[AI] TtsLanguage = "pt-br"</c>, <c>[AI] TtsVoice = 42</c> and the pt-BR voice list of <c>CompanionTts</c>).
///
/// <para>Who wins: an explicit value the player wrote in the .cfg or in the panels <b>always</b> wins - the table is
/// only the answer for <c>"auto"</c> (the default of a new file) or an empty value. There is no migration: a file that
/// already holds <c>pt</c> / <c>pt-br</c> / <c>42</c> keeps using them.</para>
///
/// <para>Languages the Kokoro model does not speak (German, Russian, Polish, Turkish, Korean, Swedish, Ukrainian)
/// fall back to <b>English</b>: a companion that speaks with an English voice is better than a mute one. The fallback
/// is said in the log.</para>
///
/// <para>Where the language comes from: <c>Localization.instance.GetSelectedLanguage()</c> - but read <b>on the main
/// thread</b> and remembered by <see cref="NotifyLanguage"/> (the <c>Localization.SetupLanguage</c> postfix already
/// has it in hand). That read must not happen inside that postfix: it touches the platform layer, which calls
/// SetupLanguage again (the 1.8.3 hotfix). While the game has no <c>Localization</c> yet (the plugin's Awake runs
/// before it) the answer is English and it is <b>not</b> cached, so the first real language is still picked up.</para>
/// </summary>
internal static class CompanionLocale
{
	/// <summary>The sentinel that means "follow the game language" in <c>[AI] TtsLanguage</c>/<c>[AI] VoiceLanguage</c>.</summary>
	internal const string AutoCode = "auto";

	// ------------------------------------------------------------------ the table

	private static readonly LocaleProfile Portuguese = new LocaleProfile
	{
		Name = "Portugues (Brasil)",
		WhisperCode = "pt",
		KokoroCode = "pt-br",
		// 42 pf_dora (feminine), 43 pm_alex, 44 pm_santa (both masculine) - confirmed in game
		Voices = new int[] { 42, 43, 44 },
		DefaultVoice = 42,
		SpeechPrompt = "Ordens para o companheiro: colete madeira, pedra, minerio, ferro, segue, para, vem ca, ataca, "
			+ "equipa, larga, pesca, cozinha, planta, conserta, volta."
	};

	private static readonly LocaleProfile English = new LocaleProfile
	{
		Name = "English",
		WhisperCode = "en",
		KokoroCode = "en-us",
		// All 20 en-US voices of the export, best first: af_heart (A), af_bella (A-), af_nicole (B-), the C+, the C,
		// the C-, the D and am_adam (F+) last. The quality grades are the ones published by the model
		// (VOICES.md); the ids are the export's (see the kokoro skill, references/voices.md).
		Voices = new int[] { 3, 2, 6, 1, 5, 9, 14, 16, 18, 0, 7, 10, 4, 8, 12, 13, 15, 17, 19, 11 },
		DefaultVoice = 3,
		SpeechPrompt = "Companion orders: gather wood, stone, ore, iron, follow, stop, come here, attack, equip, drop, "
			+ "fish, cook, plant, repair, return."
	};

	private static readonly LocaleProfile Spanish = new LocaleProfile
	{
		Name = "Espanol",
		WhisperCode = "es",
		KokoroCode = "es",
		// 28 ef_dora (feminine), 29 em_alex (masculine) - the export has no em_santa
		Voices = new int[] { 28, 29 },
		DefaultVoice = 28,
		SpeechPrompt = "Ordenes para el companero: recoge madera, piedra, mineral, hierro, sigueme, para, ven aqui, "
			+ "ataca, equipa, suelta, pesca, cocina, planta, repara, vuelve."
	};

	private static readonly LocaleProfile French = new LocaleProfile
	{
		Name = "Francais",
		WhisperCode = "fr",
		KokoroCode = "fr",
		// 30 ff_siwis (feminine) - the only French voice of the export
		Voices = new int[] { 30 },
		DefaultVoice = 30,
		SpeechPrompt = "Ordres pour le compagnon : ramasse du bois, de la pierre, du minerai, du fer, suis-moi, arrete, "
			+ "viens ici, attaque, equipe, lache, peche, cuisine, plante, repare, reviens."
	};

	/// <summary>
	/// The Kokoro speaker ids of the export, by id (<c>name = language + gender + "_" + nickname</c>, so the gender is
	/// the second letter). Ids 0-44 were checked against the export map and, for the ones this mod lists, by listening
	/// in game; 45 and up are the Mandarin ones, which this phase does not use - confirm them (NumSpeakers in the log
	/// and <c>modia_tts_teste</c> with the id) before trusting them.
	/// </summary>
	private static readonly string[] VoiceNames = new string[53]
	{
		"af_alloy", "af_aoede", "af_bella", "af_heart", "af_jessica", "af_kore", "af_nicole", "af_nova", "af_river",
		"af_sarah", "af_sky", "am_adam", "am_echo", "am_eric", "am_fenrir", "am_liam", "am_michael", "am_onyx",
		"am_puck", "am_santa", "bf_alice", "bf_emma", "bf_isabella", "bf_lily", "bm_daniel", "bm_fable", "bm_george",
		"bm_lewis", "ef_dora", "em_alex", "ff_siwis", "hf_alpha", "hf_beta", "hm_omega", "hm_psi", "if_sara",
		"im_nicola", "jf_alpha", "jf_gongitsune", "jf_nezumi", "jf_tebukuro", "jm_kumo", "pf_dora", "pm_alex",
		"pm_santa", "zf_xiaobei", "zf_xiaoni", "zf_xiaoxiao", "zf_xiaoyi", "zm_yunjian", "zm_yunxi", "zm_yunxia",
		"zm_yunyang"
	};

	// ------------------------------------------------------------------ state

	/// <summary>The profile of the current game language (null = not resolved yet).</summary>
	private static LocaleProfile _current;

	/// <summary>
	/// The game language, remembered on the main thread (see <see cref="NotifyLanguage"/>). Kept until the next
	/// notification, so invalidating the profile never loses the language.
	/// </summary>
	private static string _gameLanguage;

	// ------------------------------------------------------------------ API

	/// <summary>
	/// The resolved profile of the game language. Cached: callers may use it freely (it is one object, its
	/// <c>Voices</c> array is never rebuilt). English (not cached) while the game has no <c>Localization</c> yet.
	/// </summary>
	internal static LocaleProfile Current
	{
		get
		{
			LocaleProfile cached = _current;
			if (cached != null)
			{
				return cached;
			}
			string selected = GameLanguage();
			if (string.IsNullOrEmpty(selected))
			{
				// the plugin's Awake runs before the game has a Localization: answer English and do NOT cache it,
				// so the first real language is picked up as soon as it is known
				return English;
			}
			LocaleProfile profile = ByGameLanguage(selected);
			if (profile == null)
			{
				CompanionsPlugin.Log?.LogInfo($"[Locale] \"{selected}\" nao tem vozes no Kokoro - usando ingles.");
				profile = English;
			}
			else
			{
				CompanionsPlugin.Log?.LogInfo($"[Locale] {profile.Name} -> {profile.WhisperCode} / {profile.KokoroCode} | "
					+ $"{profile.Voices.Length} vozes, padrao {profile.DefaultVoice} ({VoiceName(profile.DefaultVoice)})");
			}
			_current = profile;
			return profile;
		}
	}

	/// <summary>
	/// Drops the resolved profile. Called where the language can have changed: the <c>Localization.SetupLanguage</c>
	/// postfix (never reads the language there - that is what recursed) and the plugin's <c>OnSceneLoaded</c>. The next
	/// <see cref="Current"/> rebuilds it; the remembered language stays.
	/// </summary>
	internal static void Invalidate()
	{
		_current = null;
	}

	/// <summary>
	/// Remembers the game language read on the main thread (the localization code already has it) and drops the
	/// profile. Called from the same place the mod's strings are injected, so facing an unknown language costs nothing.
	/// </summary>
	internal static void NotifyLanguage(string selected)
	{
		if (string.IsNullOrEmpty(selected))
		{
			return;
		}
		_gameLanguage = selected;
		_current = null;
	}

	/// <summary>
	/// Is the game language known already? False only while the game has no <c>Localization</c> (the plugin's
	/// <c>Awake</c>): <see cref="Current"/> answers English then WITHOUT caching, so a caller that writes the language
	/// in the log can say "not known yet" instead of printing a wrong code.
	/// </summary>
	internal static bool GameLanguageKnown
	{
		get
		{
			if (!string.IsNullOrEmpty(_gameLanguage))
			{
				return true;
			}
			try
			{
				return Localization.instance != null;
			}
			catch (Exception)
			{
				return false;
			}
		}
	}

	/// <summary>Is this speaker id one of the voices of the current language?</summary>
	internal static bool HasVoice(int sid)
	{
		int[] voices = Current.Voices;
		for (int i = 0; i < voices.Length; i++)
		{
			if (voices[i] == sid)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// <c>[AI] TtsVoice</c>, <c>HC_AiVoice</c> and the panels' ids resolved against the current language: 0 ("not
	/// chosen") and any voice of <b>another</b> language become the default of this language. A voice of another
	/// language is never spoken with this phonemizer - it would come out with the wrong accent.
	/// </summary>
	internal static int ResolveVoice(int requested)
	{
		if (requested > 0 && HasVoice(requested))
		{
			return requested;
		}
		return Current.DefaultVoice;
	}

	/// <summary><c>[AI] TtsLanguage</c> resolved: "auto" (or empty) follows the game language, anything else wins.</summary>
	internal static string ResolveKokoro(string configured)
	{
		return IsAuto(configured) ? Current.KokoroCode : configured.Trim();
	}

	/// <summary><c>[AI] VoiceLanguage</c> resolved: "auto" (or empty) follows the game language, anything else wins.</summary>
	internal static string ResolveWhisper(string configured)
	{
		return IsAuto(configured) ? Current.WhisperCode : configured.Trim();
	}

	/// <summary>
	/// <c>[AI] VoiceInitialPrompt</c> resolved: empty follows the game language (the orders' vocabulary in it), any
	/// text the player wrote wins - an empty prompt is a legitimate choice too and is asked for with "none".
	/// </summary>
	internal static string ResolvePrompt(string configured)
	{
		if (IsDisabled(configured))
		{
			return "";
		}
		return string.IsNullOrWhiteSpace(configured) ? Current.SpeechPrompt : configured.Trim();
	}

	/// <summary>True for "auto"/empty, the values that mean "follow the game language".</summary>
	internal static bool IsAuto(string configured)
	{
		if (string.IsNullOrWhiteSpace(configured))
		{
			return true;
		}
		string text = configured.Trim();
		return string.Equals(text, AutoCode, StringComparison.OrdinalIgnoreCase)
			|| string.Equals(text, "automatic", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(text, "automatico", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(text, "automático", StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>The voice name of a speaker id ("pf_dora"), without the gender; "" for an id we do not know.</summary>
	internal static string VoiceName(int sid)
	{
		return sid >= 0 && sid < VoiceNames.Length ? VoiceNames[sid] : "";
	}

	/// <summary>The gender of a speaker id: "f" or "m" (the second letter of the name), "" for an unknown id.</summary>
	internal static string VoiceGender(int sid)
	{
		string name = VoiceName(sid);
		return name.Length > 1 ? name.Substring(1, 1) : "";
	}

	// ------------------------------------------------------------------ plumbing

	/// <summary>
	/// The game language: the one <see cref="NotifyLanguage"/> remembered (main thread) or, before that ever ran, the
	/// Localization itself. Null while the game has no <c>Localization</c>.
	/// </summary>
	private static string GameLanguage()
	{
		if (!string.IsNullOrEmpty(_gameLanguage))
		{
			return _gameLanguage;
		}
		try
		{
			Localization instance = Localization.instance;
			return instance != null ? instance.GetSelectedLanguage() : null;
		}
		catch (Exception)
		{
			return null;
		}
	}

	private static LocaleProfile ByGameLanguage(string selected)
	{
		switch (selected)
		{
		case "Portuguese_Brazilian":
			return Portuguese;
		case "English":
			return English;
		case "Spanish":
			return Spanish;
		case "French":
			return French;
		default:
			return null;
		}
	}

	/// <summary>
	/// "none"/"off" in <c>[AI] VoiceInitialPrompt</c>: the player wants no vocabulary bias. The mod can not tell an
	/// empty prompt from an unset one (both are ""), so the auto value is always applied for "" - but a player who
	/// wants the bias off writes "none" and gets it.
	/// </summary>
	private static bool IsDisabled(string configured)
	{
		if (string.IsNullOrWhiteSpace(configured))
		{
			return false;
		}
		string text = configured.Trim();
		return string.Equals(text, "none", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(text, "off", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(text, "desligado", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(text, "nenhum", StringComparison.OrdinalIgnoreCase);
	}
}
