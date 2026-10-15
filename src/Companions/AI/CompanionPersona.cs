using System;
using System.Text;

namespace Companions;

/// <summary>
/// Fase 3.5: how the companion talks and behaves. The player either names a ready-made preset ([IA]
/// PresetPersonalidade) or writes the personality in their own words ([IA] Personalidade); whichever comes out of
/// Resolve() is pasted into the model's instructions, so the companion answers in that tone. The preset texts live
/// here (not in the .cfg) to keep the config file short, and each one is plain Portuguese: the player types
/// Portuguese in this mod, so the model is told the tone in the same language.
/// </summary>
internal static class CompanionPersona
{
	internal sealed class Preset
	{
		/// <summary>Name the player writes in [IA] PresetPersonalidade (lowercase, no accents).</summary>
		internal readonly string Id;

		/// <summary>Localization key with the preset's name, for the log (hc_ai_preset_*).</summary>
		internal readonly string NameKey;

		/// <summary>The text that goes into the system prompt.</summary>
		internal readonly string Text;

		internal Preset(string id, string nameKey, string text)
		{
			Id = id;
			NameKey = nameKey;
			Text = text;
		}
	}

	/// <summary>Used when neither a preset nor a free text was chosen: a neutral, practical companion.</summary>
	internal const string DefaultText = "Um companheiro leal e prático: fala pouco, é direto e de bom humor, gosta de "
		+ "trabalho bem feito e trata o jogador como seu dono e amigo. Nunca inventa coisas que não pode fazer.";

	/// <summary>The preset used when the player asks for "custom" (or anything unknown) without writing a text.</summary>
	internal const string CustomId = "custom";

	/// <summary>Longest free text accepted: the whole prompt has to stay small for a local model.</summary>
	internal const int MaxFreeTextLength = 600;

	private static readonly Preset[] s_presets = new Preset[4]
	{
		new Preset("viking", "hc_ai_preset_viking", "Um viking leal e destemido, de fala direta e simples, que trata o "
			+ "jogador como seu chefe e jura por Odin. Fala de honra, batalha, frio e hidromel, sem frescura e sem medo."),
		new Preset("mago", "hc_ai_preset_mago", "Um mago velho e sarcástico, que fala com ironia fina, reclama do frio, "
			+ "do barro e da falta de livros, mas ajuda sempre. Usa palavras cultas e comentários ácidos."),
		new Preset("sarcastico", "hc_ai_preset_sarcastico", "Sarcástico e engraçado: responde com piadas secas, exagera "
			+ "nos problemas e adora provocar o jogador, mas nunca desobedece nem deixa de ajudar."),
		new Preset("guia", "hc_ai_preset_guia", "Um guia paciente e didático, que explica em uma frase o que vai fazer "
			+ "e dá dicas curtas sobre o jogo (comida, abrigo, perigo, recursos), sem sermão e sem enrolação.")
	};

	/// <summary>The personality that goes into the prompt: the player's free text wins over the preset.</summary>
	internal static string Resolve()
	{
		string free = Trim(ModConfig.AiPersonality != null ? ModConfig.AiPersonality.Value : "");
		string wanted = Trim(ModConfig.AiPersonalityPreset != null ? ModConfig.AiPersonalityPreset.Value : "").ToLowerInvariant();
		Preset preset = Find(wanted);

		if (free.Length > MaxFreeTextLength)
		{
			CompanionsPlugin.Log?.LogWarning($"[IA] [IA] Personalidade is {free.Length} characters long - only the first "
				+ $"{MaxFreeTextLength} are used, so the prompt stays small");
			free = free.Substring(0, MaxFreeTextLength);
		}
		if (free.Length > 0)
		{
			if (preset != null)
			{
				CompanionsPlugin.Log?.LogInfo($"[IA] Both a preset (\"{preset.Id}\") and a free text were set - the free text wins");
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

	/// <summary>What is being used right now, for the log ("viking (Viking leal)" / "texto livre" / "padrão").</summary>
	internal static string Describe()
	{
		string free = Trim(ModConfig.AiPersonality != null ? ModConfig.AiPersonality.Value : "");
		string wanted = Trim(ModConfig.AiPersonalityPreset != null ? ModConfig.AiPersonalityPreset.Value : "").ToLowerInvariant();
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
