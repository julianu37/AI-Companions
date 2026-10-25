using System;
using System.Text;

namespace Companions;

/// <summary>
/// Fase 3.5: how the companion talks and behaves. The player either names a ready-made preset ([IA]
/// PresetPersonalidade) or writes the personality in their own words ([AI] Personality); whichever comes out of
/// Resolve() is pasted into the model's instructions, so the companion answers in that tone. The preset texts live
/// here (not in the .cfg) to keep the config file short, and each one is plain Portuguese: the player types
/// Portuguese in this mod, so the model is told the tone in the same language.
///
/// <para>Multilanguage M3: each preset carries the text in <b>EN, PT-BR, ES and FR</b> and the one that goes into the
/// prompt is the one of the <b>game language</b> (<see cref="Pick"/>, using the same table the voice and the speech
/// recognition use - <see cref="CompanionLocale"/>). A language without a text of its own (German, Russian...) gets
/// <b>English</b>, exactly like the voices. The player's own text (<c>[AI] Personality</c>) is <b>never</b>
/// translated: it is their words, in whatever language they wrote them.</para>
///
/// <para>Fase 3.10: the pair of values is a parameter now, not a config read - a companion may carry its own preset
/// and text in its ZDO (<see cref="CompanionProfile"/>), and the <c>[AI]</c> values are only the default. The
/// parameterless overloads still read the config, for the log lines and for a companion with nothing of its own.</para>
/// </summary>
internal static class CompanionPersona
{
	internal sealed class Preset
	{
		/// <summary>Name the player writes in [AI] PersonalityPreset (lowercase, no accents).</summary>
		internal readonly string Id;

		/// <summary>Localization key with the preset's name, for the log (hc_ai_preset_*).</summary>
		internal readonly string NameKey;

		private readonly string _pt;

		private readonly string _en;

		private readonly string _es;

		private readonly string _fr;

		/// <summary>The text that goes into the system prompt, in the language of the game (Multilanguage M3).</summary>
		internal string Text
		{
			get
			{
				return Pick(_pt, _en, _es, _fr);
			}
		}

		internal Preset(string id, string nameKey, string pt, string en, string es, string fr)
		{
			Id = id;
			NameKey = nameKey;
			_pt = pt;
			_en = en;
			_es = es;
			_fr = fr;
		}
	}

	/// <summary>Used when neither a preset nor a free text was chosen: a neutral, practical companion.</summary>
	internal static string DefaultText
	{
		get
		{
			return Pick(
				"Um companheiro leal e prático: fala pouco, é direto e de bom humor, gosta de trabalho bem feito e trata o "
					+ "jogador como seu dono e amigo. Nunca inventa coisas que não pode fazer.",
				"A loyal, practical companion: says little, is direct and good-humoured, likes a job well done and treats the "
					+ "player as their lord and friend. Never makes up things they cannot do.",
				"Un compañero leal y práctico: habla poco, es directo y de buen humor, le gusta el trabajo bien hecho y trata "
					+ "al jugador como su señor y amigo. Nunca se inventa cosas que no puede hacer.",
				"Un compagnon loyal et pratique : parle peu, va droit au but, a bon caractère, aime le travail bien fait et "
					+ "traite le joueur comme son maître et son ami. N'invente jamais ce qu'il ne peut pas faire.");
		}
	}

	/// <summary>The preset used when the player asks for "custom" (or anything unknown) without writing a text.</summary>
	internal const string CustomId = "custom";

	/// <summary>Longest free text accepted: the whole prompt has to stay small for a local model.</summary>
	internal const int MaxFreeTextLength = 600;

	private static readonly Preset[] s_presets = new Preset[4]
	{
		new Preset("viking", "hc_ai_preset_viking",
			"Um viking leal e destemido, de fala direta e simples, que trata o jogador como seu chefe e jura por Odin. "
				+ "Fala de honra, batalha, frio e hidromel, sem frescura e sem medo.",
			"A loyal, fearless viking with plain, direct speech, who treats the player as his chief and swears by Odin. "
				+ "Talks about honour, battle, cold and mead, with no fuss and no fear.",
			"Un vikingo leal y valiente, de habla directa y sencilla, que trata al jugador como su jefe y jura por Odín. "
				+ "Habla de honor, batalla, frío e hidromiel, sin remilgos y sin miedo.",
			"Un viking loyal et intrépide, au parler simple et direct, qui traite le joueur comme son chef et jure par Odin. "
				+ "Parle d'honneur, de bataille, de froid et d'hydromel, sans chichis et sans peur."),
		new Preset("mago", "hc_ai_preset_mago",
			"Um mago velho e sarcástico, que fala com ironia fina, reclama do frio, do barro e da falta de livros, mas "
				+ "ajuda sempre. Usa palavras cultas e comentários ácidos.",
			"An old, sarcastic mage who speaks with dry irony, complains about the cold, the mud and the lack of books, but "
				+ "always helps. Uses learned words and acid remarks.",
			"Un mago viejo y sarcástico, que habla con ironía fina, se queja del frío, del barro y de la falta de libros, "
				+ "pero siempre ayuda. Usa palabras cultas y comentarios ácidos.",
			"Un vieux mage sarcastique, à l'ironie fine, qui se plaint du froid, de la boue et du manque de livres, mais "
				+ "aide toujours. Emploie des mots savants et des remarques acides."),
		new Preset("sarcastico", "hc_ai_preset_sarcastico",
			"Sarcástico e engraçado: responde com piadas secas, exagera nos problemas e adora provocar o jogador, mas "
				+ "nunca desobedece nem deixa de ajudar.",
			"Sarcastic and funny: answers with dry jokes, exaggerates every problem and loves to tease the player, but "
				+ "never disobeys and never stops helping.",
			"Sarcástico y divertido: responde con chistes secos, exagera los problemas y adora provocar al jugador, pero "
				+ "nunca desobedece ni deja de ayudar.",
			"Sarcastique et drôle : répond par des vannes sèches, exagère les problèmes et adore provoquer le joueur, mais "
				+ "ne désobéit jamais et n'arrête jamais d'aider."),
		new Preset("guia", "hc_ai_preset_guia",
			"Um guia paciente e didático, que explica em uma frase o que vai fazer e dá dicas curtas sobre o jogo "
				+ "(comida, abrigo, perigo, recursos), sem sermão e sem enrolação.",
			"A patient, didactic guide who explains in one sentence what they are about to do and gives short tips about "
				+ "the game (food, shelter, danger, resources), with no lecturing and no rambling.",
			"Un guía paciente y didáctico, que explica en una frase lo que va a hacer y da consejos cortos sobre el juego "
				+ "(comida, refugio, peligro, recursos), sin sermones ni rodeos.",
			"Un guide patient et pédagogue, qui explique en une phrase ce qu'il va faire et donne de courts conseils sur "
				+ "le jeu (nourriture, abri, danger, ressources), sans faire la morale et sans blabla.")
	};

	/// <summary>
	/// The text of a preset in the language of the game (Multilanguage M3): the same rule as the voice and the speech
	/// recognition - the game language decides, and a language with no text of its own gets English. The language comes
	/// from <see cref="CompanionLocale.Current"/> (the one table, not another detection); it is read when the prompt is
	/// built (once per message), so nothing is cached and nothing runs per frame.
	/// </summary>
	private static string Pick(string pt, string en, string es, string fr)
	{
		switch (CompanionLocale.Current.KokoroCode)
		{
		case "pt-br":
			return pt;
		case "es":
			return es;
		case "fr":
			return fr;
		default:
			// en-us, and every language without a text of its own (German, Russian, Polish...)
			return en;
		}
	}

	/// <summary>
	/// The ready-made personalities, in the order they are shown in the in-game AI panel (Fase 3.7). Each one carries
	/// the id the player would write in <c>[AI] PersonalityPreset</c> and the localization key of its name.
	/// </summary>
	internal static Preset[] Presets => s_presets;

	/// <summary>The global personality (<c>[AI] PersonalityPreset</c> / <c>[AI] Personality</c>): the default of
	/// every companion (see <see cref="CompanionProfile"/>).</summary>
	internal static string Resolve()
	{
		return Resolve(ModConfig.AiPersonalityPreset != null ? ModConfig.AiPersonalityPreset.Value : "",
			ModConfig.AiPersonality != null ? ModConfig.AiPersonality.Value : "");
	}

	/// <summary>
	/// The personality that goes into the prompt of a message: the free text wins over the preset. Fase 3.10: the
	/// pair comes from <see cref="CompanionProfile"/> - the companion's own personality when the player gave it one,
	/// the global one otherwise - so this method no longer reads the config itself.
	/// </summary>
	internal static string Resolve(string presetId, string freeText)
	{
		string free = Trim(freeText);
		string wanted = Trim(presetId).ToLowerInvariant();
		Preset preset = Find(wanted);

		if (free.Length > MaxFreeTextLength)
		{
			CompanionsPlugin.Log?.LogWarning($"[IA] A personalidade livre tem {free.Length} caracteres - so os primeiros "
				+ $"{MaxFreeTextLength} sao usados, para o prompt continuar pequeno");
			free = free.Substring(0, MaxFreeTextLength);
		}
		if (free.Length > 0)
		{
			if (preset != null)
			{
				CompanionsPlugin.Log?.LogInfo($"[IA] Preset (\"{preset.Id}\") e texto livre definidos - o texto livre ganha");
			}
			return free;
		}
		if (preset != null)
		{
			return preset.Text;
		}
		if (wanted.Length > 0 && wanted != CustomId)
		{
			CompanionsPlugin.Log?.LogWarning($"[IA] Unknown personality preset \"{wanted}\" - using the default. Available: {Ids()}");
		}
		return DefaultText;
	}

	/// <summary>What the <c>[AI]</c> personality is right now, for the log ("viking (Viking leal)" / "texto livre" /
	/// "padrão").</summary>
	internal static string Describe()
	{
		return Describe(ModConfig.AiPersonalityPreset != null ? ModConfig.AiPersonalityPreset.Value : "",
			ModConfig.AiPersonality != null ? ModConfig.AiPersonality.Value : "");
	}

	/// <summary>What a personality pair is right now, for the log and the panels ("viking (Viking leal)" / "texto
	/// livre" / "padrão").</summary>
	internal static string Describe(string presetId, string freeText)
	{
		string free = Trim(freeText);
		string wanted = Trim(presetId).ToLowerInvariant();
		Preset preset = Find(wanted);
		if (free.Length > 0)
		{
			return "custom (" + ModLocalization.Loc("hc_ai_preset_custom") + ")";
		}
		if (preset != null)
		{
			return preset.Id + " (" + ModLocalization.Loc(preset.NameKey) + ")";
		}
		if (wanted.Length > 0 && wanted != CustomId)
		{
			return "unknown \"" + wanted + "\" -> default";
		}
		return "default (" + ModLocalization.Loc("hc_ai_preset_default") + ")";
	}

	/// <summary>"viking, mago, sarcastico, guia, custom" - what the player can write in the config.</summary>
	internal static string Ids()
	{
		StringBuilder builder = new StringBuilder(48);
		for (int i = 0; i < s_presets.Length; i++)
		{
			if (i > 0)
			{
				builder.Append(", ");
			}
			builder.Append(s_presets[i].Id);
		}
		return builder.Append(", ").Append(CustomId).ToString();
	}

	private static Preset Find(string id)
	{
		if (string.IsNullOrEmpty(id))
		{
			return null;
		}
		for (int i = 0; i < s_presets.Length; i++)
		{
			if (string.Equals(s_presets[i].Id, id, StringComparison.Ordinal))
			{
				return s_presets[i];
			}
		}
		return null;
	}

	private static string Trim(string value)
	{
		return value == null ? "" : value.Trim();
	}
}
