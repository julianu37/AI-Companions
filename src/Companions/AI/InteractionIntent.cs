using System;

namespace Companions;

/// <summary>
/// Fase 2.0 (audit fix): the five interaction orders (hive, door, board, sit, deposit) had no deterministic net of
/// their own, so a model answering "none" - or the neighbouring action ("inventory_close" for "fecha a porta",
/// "drop" for "guarda isso no baú") - left the player with "not understood" or with the wrong action happening.
/// This is the same idea as <see cref="PickupIntent.NeighborAction"/>, <see cref="GatherIntent.FromModelAction"/> and
/// <see cref="RadialIntent.Correct"/>: the player's own words decide, once per message, and nothing else is touched.
///
/// <para>The door rule is not repeated here: it lives in <see cref="CompanionOrders.IsDoorRequest"/>, together with
/// the pack words it has to stay apart from ("abre o teu inventário e fecha a porta" belongs to the inventory).</para>
///
/// <para><b>Pure</b> on purpose (no Unity, no config, no world): it is called once per message - by
/// <see cref="CompanionOrders.Run"/>, before the action is dispatched, and by
/// <see cref="CompanionChatAI.HandleResult"/> on the "none" side -, so the tables can be checked outside the game.
/// Whole-word matching between separators (never a substring: "porta" never answers "transportar"), short messages
/// only (a long sentence is conversation or an order with context, and there the model knows better) and never a
/// question.</para>
/// </summary>
internal static class InteractionIntent
{
	/// <summary>How many words the message may have for the words to win over the model (same bar as
	/// <see cref="RadialIntent.MaxWords"/>).</summary>
	internal const int MaxWords = 10;

	private const string NoneAction = "none";

	/// <summary>
	/// The action the player's own words ask for, or null when there is nothing to correct.
	/// </summary>
	/// <param name="action">The model's action, already past the other nets (resource, pickup, gathering).</param>
	/// <param name="message">What the player said (chat or voice), as it arrived.</param>
	/// <param name="reason">Portuguese phrase for the log ("fechar a porta"), or null when nothing was corrected.</param>
	internal static string Correct(string action, string message, out string reason)
	{
		reason = null;
		if (string.IsNullOrEmpty(action) || string.IsNullOrEmpty(message))
		{
			return null;
		}
		try
		{
			// a question is never an order ("fecha a porta?")
			if (message.IndexOf('?') >= 0)
			{
				return null;
			}
			string text = LlmIntent.Normalize(message);
			if (text.Length == 0 || WordCount(text) > MaxWords)
			{
				return null;
			}
			string padded = "_" + text + "_";
			// "nao guarda nada no bau", "para de guardar": the player is refusing, not ordering
			if (HasAny(padded, Negations) || StartsWith(text, StopWords))
			{
				return null;
			}
			string corrected;
			if (IsIn(action, InventoryActions) && CompanionOrders.IsDoorRequest(message))
			{
				// the flagship case: the schema's enum only lets the model answer "inventory_close" for
				// "fecha a porta" (that verb is an alias of the window), so the words have to decide
				corrected = "door";
				reason = "fechar a porta";
			}
			else if (IsIn(action, NoneOnly) && HasAny(padded, HiveNouns) && HasAny(padded, HiveVerbs))
			{
				corrected = "hive";
				reason = "esvaziar a colmeia";
			}
			else if (IsIn(action, NoneOnly) && HasAny(padded, BoardNouns) && HasAny(padded, BoardVerbs))
			{
				corrected = "board";
				reason = "embarcar no navio";
			}
			else if (IsIn(action, NoneOnly) && HasAny(padded, SitVerbs))
			{
				corrected = "sit";
				reason = "sentar perto do fogo";
			}
			else if (IsIn(action, DepositActions) && HasAny(padded, ChestNouns) && HasAny(padded, StoreVerbs))
			{
				// only "none" and "drop" are read: "joga a madeira no chao" has no chest word and stays a drop
				corrected = "deposit";
				reason = "guardar no bau";
			}
			else
			{
				return null;
			}
			if (string.Equals(corrected, action, StringComparison.Ordinal))
			{
				reason = null;
				return null;
			}
			return corrected;
		}
		catch (Exception e)
		{
			CompanionsPlugin.Log?.LogDebug("[IA] Nao consegui ler as palavras do jogador para uma ordem de colmeia/porta/navio/sentar/bau: " + e.Message);
			reason = null;
			return null;
		}
	}

	// ------------------------------------------------------------------ helpers

	/// <summary>Is the action exactly one of these?</summary>
	private static bool IsIn(string action, string[] actions)
	{
		for (int i = 0; i < actions.Length; i++)
		{
			if (string.Equals(action, actions[i], StringComparison.Ordinal))
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>Is one of these (single words or whole phrases) in the message? (whole words - the pads are the
	/// message's separators, so "porta" never matches "transportar")</summary>
	private static bool HasAny(string padded, string[] words)
	{
		for (int i = 0; i < words.Length; i++)
		{
			if (padded.Contains("_" + words[i] + "_"))
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>Does the message OPEN with one of these words? (used for the stop verbs: "para" in the middle is the
	/// preposition of "guarda isso para o bau")</summary>
	private static bool StartsWith(string text, string[] words)
	{
		int index = text.IndexOf('_');
		string first = index < 0 ? text : text.Substring(0, index);
		for (int i = 0; i < words.Length; i++)
		{
			if (string.Equals(first, words[i], StringComparison.Ordinal))
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>How many words the normalized message has (the underscores are the separators).</summary>
	private static int WordCount(string text)
	{
		int count = 1;
		for (int i = 0; i < text.Length; i++)
		{
			if (text[i] == '_')
			{
				count++;
			}
		}
		return count;
	}

	// ------------------------------------------------------------------ the tables

	/// <summary>The actions the door rule may fix (the inventory ones, because "fecha" is an alias of the window).</summary>
	private static readonly string[] InventoryActions = new string[] { NoneAction, "inventory", "inventory_close" };

	/// <summary>The actions only the "the model understood nothing" case may fix.</summary>
	private static readonly string[] NoneOnly = new string[] { NoneAction };

	/// <summary>The actions the "put it in the chest" rule may fix ("drop" is the neighbour a small model picks).</summary>
	private static readonly string[] DepositActions = new string[] { NoneAction, "drop" };

	/// <summary>"colmeia", "colmena", "ruche", "alveare", "beehive".</summary>
	private static readonly string[] HiveNouns = new string[]
	{
		"colmeia", "colmeias", "colmena", "colmenas", "ruche", "ruches", "alveare", "alveari",
		"beehive", "bee_hive", "hive", "hives"
	};

	/// <summary>"esvazia", "tira", "pega o mel", "vider", "svuota", "empty".</summary>
	private static readonly string[] HiveVerbs = new string[]
	{
		"esvazia", "esvaziar", "esvazie", "esvaziem", "vazia", "vaziar", "vazie", "limpa", "limpar", "limpe",
		"tira", "tirar", "tire", "tirem", "tira_o_mel", "pega", "pegar", "pegue", "peguem", "pega_o_mel",
		"recolhe", "recolher", "recolha", "coleta", "coletar", "colete", "colhe", "colher",
		"vacia", "vaciar", "vacie", "saca", "sacar", "saca_la_miel",
		"vide", "vider", "videz", "vuota", "svuota", "svuotare", "svuoti",
		"empty", "empties", "clear", "harvest"
	};

	/// <summary>"navio", "barco", "bateau", "nave", "ship", "boat" (+ "bordo" for "subir a bordo").</summary>
	private static readonly string[] BoardNouns = new string[]
	{
		"navio", "navios", "barco", "barcos", "navire", "navires", "bateau", "bateaux",
		"nave", "navi", "barca", "barche", "ship", "ships", "boat", "boats",
		"bordo", "a_bordo", "deck", "conves"
	};

	/// <summary>"embarca", "subir a bordo", "monter", "salire", "board".</summary>
	private static readonly string[] BoardVerbs = new string[]
	{
		"embarca", "embarcar", "embarque", "embarquem", "embarco", "embarcamos", "embarca_no",
		"subir", "subir_a_bordo", "sobe", "sube", "subam", "sube_a_bordo", "montar", "monta", "monte",
		"embarquer", "embarque_a", "monte_a_bord", "imbarca", "imbarcare", "salire", "sali", "sale",
		"board", "go_aboard", "get_on"
	};

	/// <summary>"senta", "sentar", "sientate", "assieds-toi", "siediti", "sit".</summary>
	private static readonly string[] SitVerbs = new string[]
	{
		"senta", "sentar", "senta_se", "sentar_se", "sentar_no", "senta_no", "senta_ai",
		"sientate", "sientese", "sienta", "sentarse", "sientate_ahi",
		"assieds", "assieds_toi", "assois", "assois_toi", "asseoir", "asseyez", "asseyez_vous",
		"siediti", "sieda", "siedetevi", "sedersi", "si accomodi",
		"sit", "sit_down", "take_a_seat"
	};

	/// <summary>"bau", "cofre", "coffre", "cassa", "chest".</summary>
	private static readonly string[] ChestNouns = new string[]
	{
		"bau", "baus", "baul", "baules", "cofre", "cofres", "arca", "arcas", "arcon", "arcones",
		"coffre", "coffres", "cassa", "casse", "chest", "chests"
	};

	/// <summary>"guarda", "coloca", "poe", "deposita", "ranger", "metti". The bare "por" is NOT here: it is the
	/// Portuguese preposition too ("passa por cima do bau"), and guessing from it would turn that into a deposit.</summary>
	private static readonly string[] StoreVerbs = new string[]
	{
		"guarda", "guardar", "guarde", "guardem", "guardou", "guarda_tudo", "guarda_isso", "guarda_no",
		"coloca", "colocar", "coloque", "coloquem", "coloca_no",
		"poe", "poem", "ponho", "ponha", "ponham", "poer", "poe_no",
		"deposita", "depositar", "deposite", "depositem", "deposit",
		"armazena", "armazenar", "armazene", "armazena_no",
		"ranger", "range", "rangere", "rangez", "ranger_dans",
		"metti", "mettere", "metta", "mettono", "metti_nella"
	};

	/// <summary>A negation anywhere in the message refuses the correction ("nao guarda nada no bau"). The English "no"
	/// is NOT here: it is the Portuguese preposition too ("guarda isso no bau"), and it would refuse every deposit.</summary>
	private static readonly string[] Negations = new string[]
	{
		"nao", "nunca", "jamais", "nem",
		"not", "never",
		"non", "pas", "ne", "mai",
		"jamas"
	};

	/// <summary>Verbs that only mean "stop" at the BEGINNING of the message ("para de guardar no bau").</summary>
	private static readonly string[] StopWords = new string[]
	{
		"para", "pare", "parem", "parar", "chega", "basta",
		"stop", "fermati", "fermatevi", "smetti", "smette", "smettere", "smettila"
	};
}
