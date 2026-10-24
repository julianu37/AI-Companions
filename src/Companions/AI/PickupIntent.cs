using System;

namespace Companions;

/// <summary>
/// Fase 3.1 (3ª correção do teste em jogo): <c>drop</c>, <c>pickup</c> e <c>nopickup</c> são ações vizinhas no
/// contrato, e um modelo pequeno confunde as três - "jogue fora sua madeira" voltou como <c>nopickup</c> (o
/// companheiro DESLIGAVA a coleta automática em vez de largar a madeira) e "junte a madeira do chão" ficou 50/50
/// entre <c>pickup</c> e <c>nopickup</c>.
///
/// A ideia é a mesma do "o recurso manda" (ver OrderItems.GatherAction): quando o modelo devolve <c>nopickup</c>
/// MAS as palavras do jogador pedem claramente para LARGAR ou para RECOLHER, as palavras do jogador mandam. Só o
/// <c>nopickup</c> é olhado - nenhuma outra ação é mexida - e a intenção legítima do <c>nopickup</c> é preservada:
/// um verbo de parar ("para", "pare") ou um "não pegue/não recolha" desliga a correção.
///
/// O item do <c>drop</c> NÃO sai daqui: quem o resolve continua sendo o caminho da Fase 3.6b (as palavras do
/// jogador lidas pelo OrderTargets.Phrases e validadas contra a mochila), então nada de extração de nome nova.
///
/// Pura de propósito (sem Unity, sem config, sem mundo): é chamada uma vez por mensagem pelo CompanionOrders.Run
/// e a tabela de gatilhos pode ser conferida fora do jogo.
/// </summary>
internal static class PickupIntent
{
	/// <summary>A ordem de LARGAR/JOGAR FORA, quando as palavras do jogador desmentem o <c>nopickup</c> do modelo.</summary>
	internal const string DropAction = "drop";

	/// <summary>A ordem de RECOLHER itens do chão, no mesmo caso.</summary>
	internal const string PickupAction = "pickup";

	/// <summary>A ação do contrato que esta correção olha - e só ela.</summary>
	private const string NoPickupAction = "nopickup";

	/// <summary>
	/// A ação a usar no lugar do <c>nopickup</c> que o modelo devolveu, ou null quando não há nada a corrigir (o
	/// comportamento de sempre).
	/// </summary>
	/// <param name="action">A ação canônica do modelo, já depois da correção "o recurso manda".</param>
	/// <param name="message">O que o JOGADOR disse (chat ou voz), do jeito que chegou.</param>
	internal static string NeighborAction(string action, string message)
	{
		if (action != NoPickupAction || string.IsNullOrEmpty(message))
		{
			return null;
		}
		try
		{
			string text = LlmIntent.Normalize(message);
			if (text.Length == 0)
			{
				return null;
			}
			// the words are matched between separators, so "larga" never answers "largada" and a phrase like
			// "no chao" has to appear whole
			string padded = "_" + text + "_";
			// guarda-corpo: "nopickup" é a ordem LEGÍTIMA quando o jogador pede para PARAR de recolher
			if (IsStop(padded) || HasPhrase(padded, RefusePhrases))
			{
				return null;
			}
			if (IsDrop(padded))
			{
				return DropAction;
			}
			if (IsPickup(padded))
			{
				return PickupAction;
			}
			return null;
		}
		catch (Exception e)
		{
			CompanionsPlugin.Log?.LogDebug("[IA] Couldn't read the player's own words for pickup/drop: " + e.Message);
			return null;
		}
	}

	/// <summary>Does the player's message ask to LARGAR/JOGAR FORA what the companion carries?</summary>
	private static bool IsDrop(string padded)
	{
		// "larga", "largar", "descarta", "descartar"...: o verbo sozinho já diz o que o jogador quer
		if (HasPhrase(padded, DropVerbs))
		{
			return true;
		}
		// "joga fora", "jogue a madeira fora", "jugue a madeira fora" (erro de digitação): o verbo de jogar mais o
		// lugar, em qualquer ordem da frase
		if (HasPhrase(padded, ThrowVerbs) && (HasPhrase(padded, AwayWords) || HasPhrase(padded, DropPlaces)))
		{
			return true;
		}
		// "deixa no chão"
		return HasPhrase(padded, LeaveVerbs) && HasPhrase(padded, DropPlaces);
	}

	/// <summary>Does the player's message ask to RECOLHER itens do chão?</summary>
	private static bool IsPickup(string padded)
	{
		// "junta", "junte", "juntar", "apanha", "apanhar": o verbo sozinho já é recolher
		if (HasPhrase(padded, GatherVerbs))
		{
			return true;
		}
		// "recolhe do chão", "pega do chão", "junta do chão": o verbo de pegar mais o lugar
		return HasPhrase(padded, CollectVerbs) && HasPhrase(padded, PickupPlaces);
	}

	/// <summary>Is one of these (single words or whole phrases) in the player's message?</summary>
	private static bool HasPhrase(string padded, string[] phrases)
	{
		for (int i = 0; i < phrases.Length; i++)
		{
			if (padded.Contains("_" + phrases[i] + "_"))
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>The verbs that ask to STOP and can never mean anything else ("pare", "parar"...). The bare "para" is
	/// NOT here: it is also the preposition of "joga essas pedras para o chão" (Fase 3.19), and read as a stop verb it
	/// turned a legit drop order into "stop picking up".</summary>
	private static readonly string[] StopVerbs = new string[]
	{
		"pare", "parar", "parem"
	};

	/// <summary>Fase 3.19: "para de recolher" - the same word with "de" after it is the verb + preposition, anywhere in the
	/// message.</summary>
	private static readonly string[] StopPhrases = new string[]
	{
		"para_de", "pare_de", "parar_de", "para_com"
	};

	/// <summary>Fase 3.19: the verbs that are also prepositions ("para", "paro", "parei"): they only mean "stop" at the
	/// beginning or at the end of the message ("para", "agora para") - never in the middle ("joga as pedras para o
	/// chão").</summary>
	private static readonly string[] AmbiguousStopVerbs = new string[]
	{
		"para", "paro", "parei"
	};

	/// <summary>Fase 3.19: the stop verbs of <see cref="NeighborAction"/>, with the position rule above.</summary>
	private static bool IsStop(string padded)
	{
		if (HasPhrase(padded, StopVerbs) || HasPhrase(padded, StopPhrases))
		{
			return true;
		}
		string text = padded.Trim('_');
		string[] words = text.Split('_');
		for (int i = 0; i < words.Length; i++)
		{
			if (Array.IndexOf(AmbiguousStopVerbs, words[i]) < 0)
			{
				continue;
			}
			if (i == 0 || i == words.Length - 1)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>"não pegue nada", "não recolha": the same intent, written out.</summary>
	private static readonly string[] RefusePhrases = new string[]
	{
		"nao_pegue", "nao_pega", "nao_peguem", "nao_pegar", "nao_recolha", "nao_recolhe", "nao_recolham",
		"nao_recolher", "nao_cate", "nao_catar"
	};

	/// <summary>The verbs that, on their own, mean "get rid of it".</summary>
	private static readonly string[] DropVerbs = new string[]
	{
		"larga", "largar", "largue", "larguem", "largou", "largando", "descarta", "descartar", "descarte",
		"descartem", "descartou", "dispensa", "dispensar", "dispense", "solta", "soltar", "solte",
		"drop", "dropa", "dropar", "drope"
	};

	/// <summary>The verbs of throwing, which need the place ("joga fora", "jogue no chão") to mean "get rid of it".</summary>
	private static readonly string[] ThrowVerbs = new string[]
	{
		"joga", "jogar", "jogue", "joguem", "jogam", "jogando", "jogou", "juga", "jugue"
	};

	/// <summary>"deixa no chão" - leaving it behind is the same order.</summary>
	private static readonly string[] LeaveVerbs = new string[]
	{
		"deixa", "deixar", "deixe", "deixem", "deixou", "deixando"
	};

	/// <summary>The place that turns a throwing verb into "get rid of it".</summary>
	private static readonly string[] AwayWords = new string[]
	{
		"fora"
	};

	private static readonly string[] DropPlaces = new string[]
	{
		"no_chao", "ao_chao"
	};

	/// <summary>The verbs that, on their own, mean "pick things up".</summary>
	private static readonly string[] GatherVerbs = new string[]
	{
		"junta", "juntar", "junte", "juntem", "juntou", "apanha", "apanhar", "apanhe", "apanhem", "apanhou"
	};

	/// <summary>The verbs of taking, which need the place ("pega do chão", "recolhe do chão").</summary>
	private static readonly string[] CollectVerbs = new string[]
	{
		"pega", "pegar", "pegue", "peguem", "pegam", "pegando", "pegou", "recolhe", "recolher", "recolha",
		"recolham", "recolheu", "cata", "catar", "cate", "catem", "catou"
	};

	/// <summary>The place that turns a taking verb into "pick it up from the ground".</summary>
	private static readonly string[] PickupPlaces = new string[]
	{
		"do_chao"
	};
}
