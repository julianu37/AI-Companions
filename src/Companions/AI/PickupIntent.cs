using System;

namespace Companions;

/// <summary>
/// Fase 3.1 (3Âª correÃ§Ã£o do teste em jogo): <c>drop</c>, <c>pickup</c> e <c>nopickup</c> sÃ£o aÃ§Ãµes vizinhas no
/// contrato, e um modelo pequeno confunde as trÃªs - "jogue fora sua madeira" voltou como <c>nopickup</c> (o
/// companheiro DESLIGAVA a coleta automÃ¡tica em vez de largar a madeira) e "junte a madeira do chÃ£o" ficou 50/50
/// entre <c>pickup</c> e <c>nopickup</c>.
///
/// A ideia Ã© a mesma do "o recurso manda" (ver OrderItems.GatherAction): quando o modelo devolve <c>nopickup</c>
/// MAS as palavras do jogador pedem claramente para LARGAR ou para RECOLHER, as palavras do jogador mandam. SÃ³ o
/// <c>nopickup</c> Ã© olhado - nenhuma outra aÃ§Ã£o Ã© mexida - e a intenÃ§Ã£o legÃ­tima do <c>nopickup</c> Ã© preservada:
/// um verbo de parar ("para", "pare") ou um "nÃ£o pegue/nÃ£o recolha" desliga a correÃ§Ã£o.
///
/// O item do <c>drop</c> NÃƒO sai daqui: quem o resolve continua sendo o caminho da Fase 3.6b (as palavras do
/// jogador lidas pelo OrderTargets.Phrases e validadas contra a mochila), entÃ£o nada de extraÃ§Ã£o de nome nova.
///
/// Pura de propÃ³sito (sem Unity, sem config, sem mundo): Ã© chamada uma vez por mensagem pelo CompanionOrders.Run
/// e a tabela de gatilhos pode ser conferida fora do jogo.
/// </summary>
internal static class PickupIntent
{
	/// <summary>A ordem de LARGAR/JOGAR FORA, quando as palavras do jogador desmentem o <c>nopickup</c> do modelo.</summary>
	internal const string DropAction = "drop";

	/// <summary>A ordem de RECOLHER itens do chÃ£o, no mesmo caso.</summary>
	internal const string PickupAction = "pickup";

	/// <summary>A aÃ§Ã£o do contrato que esta correÃ§Ã£o olha - e sÃ³ ela.</summary>
	private const string NoPickupAction = "nopickup";

	/// <summary>
	/// A aÃ§Ã£o a usar no lugar do <c>nopickup</c> que o modelo devolveu, ou null quando nÃ£o hÃ¡ nada a corrigir (o
	/// comportamento de sempre).
	/// </summary>
	/// <param name="action">A aÃ§Ã£o canÃ´nica do modelo, jÃ¡ depois da correÃ§Ã£o "o recurso manda".</param>
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
			// guarda-corpo: "nopickup" Ã© a ordem LEGÃTIMA quando o jogador pede para PARAR de recolher
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
		// "larga", "largar", "descarta", "descartar"...: o verbo sozinho jÃ¡ diz o que o jogador quer
		if (HasAffirmativePhrase(padded, DropVerbs))
		{
			return true;
		}
		// "joga fora", "jogue a madeira fora", "jugue a madeira fora" (erro de digitaÃ§Ã£o): o verbo de jogar mais o
		// lugar, em qualquer ordem da frase
		if (HasAffirmativePhrase(padded, ThrowVerbs) && (HasPhrase(padded, AwayWords) || HasPhrase(padded, DropPlaces)))
		{
			return true;
		}
		// "deixa no chÃ£o"
		return HasAffirmativePhrase(padded, LeaveVerbs) && HasPhrase(padded, DropPlaces);
	}

	/// <summary>
	/// Fase 3.23 (auditoria): same as <see cref="HasPhrase"/>, but a verb that comes right after a negation does not
	/// count. "nÃ£o jogue fora a madeira" normalized is "nao_jogue_fora_a_madeira", and the plain search matched
	/// "jogue" + "fora" - the order became a DROP of the very item the player asked to keep.
	/// </summary>
	private static bool HasAffirmativePhrase(string padded, string[] phrases)
	{
		for (int i = 0; i < phrases.Length; i++)
		{
			string needle = "_" + phrases[i] + "_";
			int at = padded.IndexOf(needle, StringComparison.Ordinal);
			while (at >= 0)
			{
				if (!NegatedAt(padded, at))
				{
					return true;
				}
				at = padded.IndexOf(needle, at + 1, StringComparison.Ordinal);
			}
		}
		return false;
	}

	/// <summary>
	/// Is the phrase at <paramref name="at"/> negated? Fase 3.24 (auditoria): the word right before it used to be the
	/// only place looked at, so "nÃ£o quero que vocÃª jogue fora a madeira" (the "nÃ£o" is four words away) came back as
	/// a DROP of the item the player asked to keep. The walk back now goes through the words that only carry the
	/// negation along ("que", "quero", "vocÃª", "por favor"...) and stops at the first word that carries a meaning of
	/// its own: "nÃ£o sei, joga fora" is still a DROP, exactly as before.
	/// </summary>
	private static bool NegatedAt(string padded, int at)
	{
		int end = at;
		for (int step = 0; step < NegationLookback; step++)
		{
			if (end <= 0)
			{
				return false;
			}
			int previous = padded.LastIndexOf('_', end - 1);
			if (previous < 0)
			{
				return false;
			}
			string word = padded.Substring(previous + 1, end - previous - 1);
			if (Array.IndexOf(Negations, word) >= 0)
			{
				return true;
			}
			if (Array.IndexOf(NegationCarriers, word) < 0)
			{
				return false;
			}
			end = previous;
		}
		return false;
	}

	/// <summary>How many words back a negation is still read through <see cref="NegationCarriers"/>.</summary>
	private const int NegationLookback = 4;

	/// <summary>
	/// Fase 3.24 (auditoria): the words that keep a negation in scope but mean nothing on their own. Only these are
	/// walked over - a verb or a noun ends the walk.
	/// </summary>
	private static readonly string[] NegationCarriers = new string[]
	{
		"que", "quero", "queria", "gostaria", "posso", "pode", "podes", "poderia", "deve", "deves", "deveria",
		"precisa", "preciso", "vai", "vou", "vamos", "va", "esta", "estou", "ta", "to", "voce", "voces", "vc", "tu",
		"senhor", "por", "favor", "pf", "pra", "pro", "agora", "mais", "me", "te", "lhe",
		"pense", "pensa", "pensar", "em", "sequer", "mesmo"
	};

	/// <summary>Does the player's message ask to RECOLHER itens do chÃ£o?</summary>
	private static bool IsPickup(string padded)
	{
		// "junta", "junte", "juntar", "apanha", "apanhar": o verbo sozinho jÃ¡ Ã© recolher
		if (HasPhrase(padded, GatherVerbs))
		{
			return true;
		}
		// "recolhe do chÃ£o", "pega do chÃ£o", "junta do chÃ£o": o verbo de pegar mais o lugar
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
	/// NOT here: it is also the preposition of "joga essas pedras para o chÃ£o" (Fase 3.19), and read as a stop verb it
	/// turned a legit drop order into "stop picking up".</summary>
	private static readonly string[] StopVerbs = new string[]
	{
		"pare", "parar", "parem",
		// Multilanguage M5: italian (and the english "stop")
		"fermati", "fermatevi", "ferma", "fermare", "smetti", "smettere", "basta", "stop"
	};

	/// <summary>Fase 3.19: "para de recolher" - the same word with "de" after it is the verb + preposition, anywhere in the
	/// message.</summary>
	private static readonly string[] StopPhrases = new string[]
	{
		"para_de", "pare_de", "parar_de", "para_com",
		// Multilanguage M5: "smetti di raccogliere", "stop picking up"
		"smetti_di", "smette_di", "smettere_di", "smettila_di", "stop_picking", "stop_collecting"
	};

	/// <summary>Fase 3.19: the verbs that are also prepositions ("para", "paro", "parei"): they only mean "stop" at the
	/// beginning or at the end of the message ("para", "agora para") - never in the middle ("joga as pedras para o
	/// chÃ£o").</summary>
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

	/// <summary>"nÃ£o pegue nada", "nÃ£o recolha": the same intent, written out.</summary>
	private static readonly string[] RefusePhrases = new string[]
	{
		"nao_pegue", "nao_pega", "nao_peguem", "nao_pegar", "nao_recolha", "nao_recolhe", "nao_recolham",
		"nao_recolher", "nao_cate", "nao_catar",
		// Multilanguage M5: italian
		"non_raccogliere", "non_raccogliere_niente", "non_raccattare", "non_prendere", "non_raccolga"
	};

	/// <summary>
	/// Fase 3.23 (auditoria): the words that turn the verb right after them into a NO. "nao jogue fora", "nunca
	/// largue": the item stays where it is. Normalize already dropped the accents ("nÃ£o" -> "nao").
	/// </summary>
	private static readonly string[] Negations = new string[]
	{
		"nao", "nunca", "jamais", "nem",
		// Multilanguage M5: french and italian
		"ne", "pas", "non", "mai"
	};

	/// <summary>The verbs that, on their own, mean "get rid of it".</summary>
	private static readonly string[] DropVerbs = new string[]
	{
		"larga", "largar", "largue", "larguem", "largou", "largando", "descarta", "descartar", "descarte",
		"descartem", "descartou", "dispensa", "dispensar", "dispense", "solta", "soltar", "solte",
		"drop", "dropa", "dropar", "drope",
		// Multilanguage M5: italian
		"lascia", "lasciare", "butta", "buttare", "buttala", "getta", "gettare", "abbandona",
		"abbandonare", "posa", "posare"
	};

	/// <summary>The verbs of throwing, which need the place ("joga fora", "jogue no chÃ£o") to mean "get rid of it".</summary>
	private static readonly string[] ThrowVerbs = new string[]
	{
		"joga", "jogar", "jogue", "joguem", "jogam", "jogando", "jogou", "juga", "jugue"
	};

	/// <summary>"deixa no chÃ£o" - leaving it behind is the same order.</summary>
	private static readonly string[] LeaveVerbs = new string[]
	{
		"deixa", "deixar", "deixe", "deixem", "deixou", "deixando"
	};

	/// <summary>The place that turns a throwing verb into "get rid of it".</summary>
	private static readonly string[] AwayWords = new string[]
	{
		"fora",
		// Multilanguage M5: italian "butta via", french "jette"
		"via"
	};

	private static readonly string[] DropPlaces = new string[]
	{
		"no_chao", "ao_chao",
		// Multilanguage M5: italian and french places ("a terra", "per terra", "par terre")
		"a_terra", "per_terra", "in_terra", "par_terre", "al_suelo", "en_el_suelo"
	};

	/// <summary>The verbs that, on their own, mean "pick things up".</summary>
	private static readonly string[] GatherVerbs = new string[]
	{
		"junta", "juntar", "junte", "juntem", "juntou", "apanha", "apanhar", "apanhe", "apanhem", "apanhou",
		// Multilanguage M5: italian ("raccogli", "raccatta" = pick up/collect what is on the ground)
		"raccogli", "raccoglie", "raccogliere", "raccatta", "raccattare", "raccatta_tutto", "raccogli_tutto"
	};

	/// <summary>The verbs of taking, which need the place ("pega do chÃ£o", "recolhe do chÃ£o").</summary>
	private static readonly string[] CollectVerbs = new string[]
	{
		"pega", "pegar", "pegue", "peguem", "pegam", "pegando", "pegou", "recolhe", "recolher", "recolha",
		"recolham", "recolheu", "cata", "catar", "cate", "catem", "catou",
		// Multilanguage M5: italian taking verbs (need the place: "prendi la legna per terra")
		"prendi", "prende", "prendere", "prendilo", "cogli", "cogliere"
	};

	/// <summary>The place that turns a taking verb into "pick it up from the ground".</summary>
	private static readonly string[] PickupPlaces = new string[]
	{
		"do_chao",
		// Multilanguage M5: italian ("per terra", "da terra", "a terra")
		"per_terra", "da_terra", "a_terra", "in_terra"
	};
}
