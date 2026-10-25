using System;

namespace Companions;

/// <summary>
/// Fase M5 (medição das 30 ações com o <c>qwen2.5-3b-instruct</c>, 30 ações × 5 idiomas): sete ordens voltaram com a
/// ação errada de forma estável, e <b>acrescentar exemplos ao contrato piorou o modelo</b> (o PT regrediu e o ES
/// "vaga" passou a vir como "nowander" depois dos exemplos). O caminho é a <b>rede determinística por palavras</b>, no
/// mesmo espírito da <see cref="PickupIntent.NeighborAction"/> (3.1 §8.2) e da
/// <see cref="GatherIntent.FromModelAction"/> (M3): <b>as palavras do jogador mandam</b>, com uma linha de log quando
/// corrigem.
///
/// <para>Quatro temas, todos estreitos (mensagem curta, casamento por <b>palavra inteira</b> entre separadores, sem
/// substring) e todos com um <b>portão de ações</b> - cada um só olha as ações em que o modelo erra de verdade:</para>
/// <list type="bullet">
/// <item><b>Postura</b> (palavra de postura + a qualidade) - <c>stance_balanced/passive/ranged/melee</c>; vale para
/// qualquer ação, menos as que carregam alvo/recurso (ver <see cref="PreciseActions"/>).</item>
/// <item><b>Vagar</b> (palavra de vagar, mais um verbo de parar no <b>início</b> ou uma palavra de parada) -
/// <c>wander</c>/<c>nowander</c>; vale para <c>stop</c>/<c>wander</c>/<c>nowander</c>/<c>none</c>.</item>
/// <item><b>Comandos</b> (palavra de comando + ignorar/negar ou aceitar/obedecer) -
/// <c>command</c>/<c>nocommand</c>; vale para <c>none</c>/<c>stop</c>/<c>command</c>/<c>nocommand</c>.</item>
/// <item><b>Trabalhos</b> (fundir/cozinhar/caçar) - <c>smelt</c>/<c>cook</c>/<c>hunt</c>; vale para
/// <c>repair</c>/<c>farm</c>/<c>wander</c>/<c>none</c>, e <b>nunca</b> quando a mensagem nomeia uma criatura
/// (o <c>attack</c> continua ganhando) nem quando ela também pede reparo.</item>
/// </list>
///
/// <para><b>Puro</b> de propósito (sem Unity, sem config, sem mundo): é chamado uma vez por mensagem - pelo
/// <see cref="CompanionOrders.Run"/>, antes de despachar a ação, e pelo <see cref="CompanionChatAI.HandleResult"/>, no
/// lado <c>none</c> ao lado da <see cref="GatherIntent"/> -, então a tabela de palavras pode ser conferida fora do
/// jogo. Nada roda por frame.</para>
/// </summary>
internal static class RadialIntent
{
	/// <summary>
	/// O maior número de palavras que a mensagem pode ter para as palavras vencerem o modelo. As ordens medidas são
	/// curtas ("cuida da fundicao", "posizione a distanza"); uma frase comprida costuma ser conversa ou uma ordem com
	/// contexto, e aí o modelo é quem sabe.
	/// </summary>
	internal const int MaxWords = 10;

	/// <summary>
	/// A ação que as palavras do jogador pedem, ou null quando não há nada a corrigir. <paramref name="reason"/> sai
	/// com a frase em PT que vai para o log ("parar de vagar", "ficar na postura à distância"...).
	/// </summary>
	/// <param name="action">A ação do modelo, já depois das correções anteriores (recurso, pickup/drop, coleta).</param>
	/// <param name="message">O que o JOGADOR disse (chat ou voz), do jeito que chegou - a pergunta olha o texto cru.</param>
	internal static string Correct(string action, string message, out string reason)
	{
		reason = null;
		if (string.IsNullOrEmpty(action) || string.IsNullOrEmpty(message))
		{
			return null;
		}
		try
		{
			// uma pergunta nunca é ordem ("você aceita comandos?", "qual é a tua postura?")
			if (message.IndexOf('?') >= 0)
			{
				return null;
			}
			string text = LlmIntent.Normalize(message);
			if (text.Length == 0 || WordCount(text) > MaxWords)
			{
				return null;
			}
			// casado entre separadores, como na GatherIntent: "vaga" nunca casa "vagabunda" de outro jeito
			string padded = "_" + text + "_";
			return Posture(action, padded, out reason)
				?? Wander(action, text, padded, out reason)
				?? Commands(action, padded, out reason)
				?? Job(action, padded, out reason);
		}
		catch (Exception e)
		{
			CompanionsPlugin.Log?.LogDebug("[IA] Couldn't read the player's own words for a posture/wander/command/job order: " + e.Message);
			reason = null;
			return null;
		}
	}

	// ------------------------------------------------------------------ posture

	/// <summary>
	/// "fica na postura equilibrada", "posizione a distanza": a palavra de postura <b>mais</b> a qualidade. É a regra
	/// mais determinística das quatro, e o modelo a erra de formas diferentes (o italiano devolveu "wander" e
	/// "smelt" para a mesma frase).
	/// </summary>
	private static string Posture(string action, string padded, out string reason)
	{
		reason = null;
		// as ordens precisas (alvo/recurso) não são tocadas: "ataca o greydwarf na postura defensiva" continua ataque
		if (IsIn(action, PreciseActions) || !HasAny(padded, StanceWords))
		{
			return null;
		}
		string stance = null;
		if (HasAny(padded, BalancedWords))
		{
			stance = "stance_balanced";
			reason = "ficar na postura equilibrada";
		}
		else if (HasAny(padded, RangedWords))
		{
			stance = "stance_ranged";
			reason = "ficar na postura à distância";
		}
		else if (HasAny(padded, MeleeWords))
		{
			stance = "stance_melee";
			reason = "ficar na postura corpo a corpo";
		}
		else if (HasAny(padded, PassiveWords))
		{
			stance = "stance_passive";
			reason = "ficar na postura defensiva";
		}
		if (stance == null || string.Equals(stance, action, StringComparison.Ordinal))
		{
			reason = null;
			return null;
		}
		return stance;
	}

	// ------------------------------------------------------------------ wander

	/// <summary>
	/// "vagueia" / "para de vagar" (e o espanhol "vaga", que o modelo passou a mandar como "nowander"): a palavra de
	/// vagar decide o tema, e o verbo de parar decide o sentido. O "para" <b>como preposição</b> ("joga a pedra para o
	/// chão") não pode disparar: ele só conta <b>no início</b> da mensagem - em qualquer outra posição vale uma palavra
	/// que só pode ser de parada ("pare", "stop", "smetti", "deja").
	/// </summary>
	private static string Wander(string action, string text, string padded, out string reason)
	{
		reason = null;
		if (!IsIn(action, WanderActions) || !HasAny(padded, WanderWords))
		{
			return null;
		}
		bool stop = IsIn(FirstWord(text), StartStopWords) || HasAny(padded, StopWords);
		string corrected = stop ? "nowander" : "wander";
		if (string.Equals(corrected, action, StringComparison.Ordinal))
		{
			return null;
		}
		reason = stop ? "parar de vagar" : "vagar";
		return corrected;
	}

	// ------------------------------------------------------------------ commands

	/// <summary>
	/// "ignore my commands" / "accetta i miei ordini": a palavra de comando mais o verbo. A negação ganha do verbo de
	/// aceitar ("não aceito mais ordens" é desligar), e sem nenhum dos dois nada é mexido.
	/// </summary>
	private static string Commands(string action, string padded, out string reason)
	{
		reason = null;
		if (!IsIn(action, CommandActions) || !HasAny(padded, CommandWords))
		{
			return null;
		}
		bool refuse = HasAny(padded, IgnoreWords) || HasAny(padded, Negations);
		string corrected;
		if (refuse)
		{
			corrected = "nocommand";
			reason = "ignorar os comandos";
		}
		else if (HasAny(padded, AcceptWords))
		{
			corrected = "command";
			reason = "aceitar comandos";
		}
		else
		{
			reason = null;
			return null;
		}
		if (string.Equals(corrected, action, StringComparison.Ordinal))
		{
			reason = null;
			return null;
		}
		return corrected;
	}

	// ------------------------------------------------------------------ jobs

	/// <summary>
	/// "cuida da fundicao" (o modelo respondeu "repair"), "cuida da cozinha", "vai caçar": o trabalho que a mensagem
	/// nomeia. Duas guardas: uma criatura nomeada mantém o <c>attack</c> ("vai caçar o troll" não vira caçada de
	/// bichos) e um verbo de reparo mantém o <c>repair</c> ("conserta a cozinha" não vira cozinheiro).
	/// </summary>
	private static string Job(string action, string padded, out string reason)
	{
		reason = null;
		if (!IsIn(action, JobActions) || HasAny(padded, CreatureWords) || HasAny(padded, RepairVerbs))
		{
			return null;
		}
		string corrected;
		if (HasAny(padded, SmeltWords))
		{
			corrected = "smelt";
			reason = "fundir minério";
		}
		else if (HasAny(padded, CookWords))
		{
			corrected = "cook";
			reason = "cozinhar";
		}
		else if (HasAny(padded, HuntWords))
		{
			corrected = "hunt";
			reason = "caçar";
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

	/// <summary>Is one of these words in the message? (whole words - the pads are the message's separators)</summary>
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

	/// <summary>The first word of a normalized message ("para_de_vagar" -> "para").</summary>
	private static string FirstWord(string text)
	{
		int index = text.IndexOf('_');
		return index < 0 ? text : text.Substring(0, index);
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

	/// <summary>
	/// The actions this net never overwrites: the ones that pick ONE creature or item (the precise order of Fase
	/// 3.6/3.6b) and the gathering orders, which carry a resource and a number. Without this, "ataca o greydwarf"
	/// would lose to a posture word in the same sentence and "colete 10 pedras" to a stray "corpo a corpo".
	/// </summary>
	private static readonly string[] PreciseActions = new string[]
	{
		"attack", "equip", "drop", "inventory", "inventory_close",
		"wood", "stone", "ore", "forage", "pickup", "nopickup"
	};

	/// <summary>The actions the wander rule may fix (the model answers "stop" for "stop wandering" and "nowander" for "vaga").</summary>
	private static readonly string[] WanderActions = new string[] { "none", "stop", "wander", "nowander", "hunt" };

	/// <summary>The actions the command rule may fix ("ignore my commands" came as "none", "ignore mes ordres" as "stop").</summary>
	private static readonly string[] CommandActions = new string[] { "none", "stop", "command", "nocommand" };

	/// <summary>The actions the job rule may fix (the model answers "repair" for the furnace and "farm" for the kitchen).</summary>
	private static readonly string[] JobActions = new string[] { "none", "repair", "farm", "wander" };

	/// <summary>"postura", "stance", "posture", "posizione", "posición".</summary>
	private static readonly string[] StanceWords = new string[]
	{
		"postura", "posturas", "stance", "stances", "posture", "postures",
		"posizione", "posizioni", "posicion", "posiciones"
	};

	/// <summary>"equilibrado", "balanced", "équilibrée", "equilibrata", "balanceado".</summary>
	private static readonly string[] BalancedWords = new string[]
	{
		"equilibrado", "equilibrada", "equilibrados", "equilibradas",
		"balanced", "balance",
		"equilibre", "equilibree", "equilibres", "equilibrees",
		"equilibrato", "equilibrata", "equilibrati", "equilibrate",
		"balanceado", "balanceada", "balanceados", "balanceadas"
	};

	/// <summary>"defensivo", "passivo", "passive", "passif", "pasiva".</summary>
	private static readonly string[] PassiveWords = new string[]
	{
		"passivo", "passiva", "passivos", "passivas", "pasivo", "pasiva", "pasivos", "pasivas",
		"passive", "passives", "passif", "passifs",
		"defensivo", "defensiva", "defensivos", "defensivas", "defensive", "defensif"
	};

	/// <summary>"à distância", "ranged", "a distanza", "a distancia".</summary>
	private static readonly string[] RangedWords = new string[]
	{
		"distancia", "distancias", "distance", "distances", "distanza", "distanze",
		"ranged", "a_distance", "a_distancia", "de_distancia"
	};

	/// <summary>"corpo a corpo", "melee", "corps à corps", "cuerpo a cuerpo".</summary>
	private static readonly string[] MeleeWords = new string[]
	{
		"corpo_a_corpo", "melee", "corps_a_corps", "cuerpo_a_cuerpo", "a_corpo_a_corpo"
	};

	/// <summary>"vagar", "vaguear", "wander", "errer", "vagare", "vagabond".</summary>
	private static readonly string[] WanderWords = new string[]
	{
		// pt
		"vagar", "vaga", "vaguear", "vagueia", "vagueie", "vagueiem", "vagueando", "vagando", "vagam", "vagueiam",
		"perambular", "perambula", "perambule",
		// en
		"wander", "wanders", "wandering", "roam", "roams", "roaming",
		// es
		"deambular", "deambula", "deambule",
		// fr
		"errer", "erre", "erres", "errez", "vagabonder", "vagabonde", "vagabondes", "vagabondez",
		// it
		"vagare", "vaghi", "vagabondare", "vagabonda", "vagabondi", "girovagare", "girovaga",
		"gironzolare", "gironzola", "gironzoli"
	};

	/// <summary>Verbs that can only mean "stop" (never a preposition), anywhere in the message.</summary>
	private static readonly string[] StopWords = new string[]
	{
		// pt
		"pare", "parem", "parar", "deixa", "deixar", "deixe", "deixem",
		// en
		"stop", "stops", "stopped", "stopping",
		// es
		"deja", "dejad", "dejar", "paren",
		// fr
		"arrete", "arreter", "arretez", "cesse", "cesser",
		// it
		"fermati", "fermatevi", "ferma", "fermare", "smetti", "smette", "smettere", "smettila", "basta"
	};

	/// <summary>
	/// The same verbs plus the ones that only count <b>at the start</b>: "para" (also the preposition of "joga a pedra
	/// para o chão") and the negations ("não vagueie", "no wandering", "non vagare").
	/// </summary>
	private static readonly string[] StartStopWords = new string[]
	{
		// pt (e as palavras que os cinco idiomas dividem)
		"para", "pare", "parem", "parar", "deixa", "deixar", "deixe", "deixem", "nao", "nunca", "jamais", "nem",
		// en
		"stop", "stops", "stopping", "no", "never", "not",
		// es
		"deja", "dejad", "dejar", "paren", "jamas",
		// fr
		"arrete", "arreter", "arretez", "cesse", "cesser", "ne", "non",
		// it
		"fermati", "fermatevi", "ferma", "fermare", "smetti", "smette", "smettere", "smettila", "basta", "mai"
	};

	/// <summary>"comando(s)", "order(s)", "ordres", "ordini", "órdenes".</summary>
	private static readonly string[] CommandWords = new string[]
	{
		// pt
		"comando", "comandos", "ordem", "ordens",
		// en
		"command", "commands", "order", "orders",
		// es
		"orden", "ordenes",
		// fr
		"ordre", "ordres",
		// it
		"ordine", "ordini"
	};

	/// <summary>The words of IGNORING an order (the "no" side first: it wins over an accept verb).</summary>
	private static readonly string[] IgnoreWords = new string[]
	{
		// pt
		"ignora", "ignore", "ignores", "ignoram", "ignorem", "ignorar", "ignorando",
		// en
		"ignoring", "ignored",
		// es
		"ignoren",
		// fr
		"ignorer", "ignorez", "ignorent",
		// it
		"ignori", "ignorate", "ignorare"
	};

	/// <summary>Negations that turn a command word into "stop taking orders" (normalized: no accents).</summary>
	private static readonly string[] Negations = new string[]
	{
		"nao", "nunca", "jamais", "nem",
		"no", "not", "never",
		"non", "pas", "mai",
		"jamas"
	};

	/// <summary>The words of ACCEPTING orders ("aceite", "accept", "accetta", "obedece").</summary>
	private static readonly string[] AcceptWords = new string[]
	{
		// pt
		"aceita", "aceite", "aceitem", "aceitas", "aceitar", "aceito", "obedece", "obedecam", "obedecer", "obedeca",
		// en
		"accept", "accepts", "accepted", "accepting", "obey", "obeys", "obeying",
		// es
		"aceptar", "acepta", "acepte", "acepten", "obedezca",
		// fr
		"accepte", "acceptes", "acceptez", "accepter", "obeis", "obeissez", "obeir",
		// it
		"accetta", "accetti", "accettate", "accettare", "obbedisci", "obbedisca", "obbedire", "obbedisce"
	};

	/// <summary>"fundir", "fundição", "smelter", "fourneau", "fonderia", "furnace", "forno", "horno".</summary>
	private static readonly string[] SmeltWords = new string[]
	{
		// pt
		"funde", "fundem", "fundir", "fundi", "fundindo", "fundicao", "fundicoes", "forja", "forjar",
		// en
		"smelt", "smelts", "smelting", "smelter", "furnace", "furnaces", "forge",
		// es
		"fundicion", "horno", "hornos",
		// fr
		"fourneau", "fourneaux", "fournaise", "fondre", "fonte",
		// it
		"fonderia", "fondere", "fondi", "fondiamo", "forno", "fornace", "forgia", "forgiare"
	};

	/// <summary>"cozinhar", "cook", "cuisine", "cucina", "cocina".</summary>
	private static readonly string[] CookWords = new string[]
	{
		// pt
		"cozinha", "cozinhe", "cozinhem", "cozinhas", "cozinhar", "cozinho", "cozinhando", "cozinheiro",
		// en
		"cook", "cooks", "cooking", "cooked", "kitchen",
		// es
		"cocina", "cocine", "cocinen", "cocinar", "cocino", "cocinando", "cocinero",
		// fr
		"cuisine", "cuisines", "cuisiner", "cuisinez", "cuisinier",
		// it
		"cucina", "cucini", "cucinare", "cucinate", "cucino", "cucinando", "cuoco"
	};

	/// <summary>"caçar", "hunt", "chasse", "cacciare", "cazar".</summary>
	private static readonly string[] HuntWords = new string[]
	{
		// pt
		"caca", "cacar", "cace", "cacem", "cacada", "cacador", "cacando",
		// en
		"hunt", "hunts", "hunting", "hunted", "hunter",
		// es
		"caza", "cazar", "cacen", "cazando", "cazador",
		// fr
		"chasse", "chasses", "chasser", "chassez", "chasseur",
		// it
		"caccia", "cacciare", "cacciate", "cacciatore", "cacciando"
	};

	/// <summary>
	/// Creatures whose name keeps the <c>attack</c>: "vai caçar o troll" is an attack on the troll, not the hunting
	/// job. The five languages of the game (the mod's own target list is not reachable here - this file is pure).
	/// </summary>
	private static readonly string[] CreatureWords = new string[]
	{
		// the ones the measurement named
		"greydwarf", "greydwarfs", "greydvergr", "troll", "trolls", "boar", "boars", "javali", "javalis",
		"jabali", "goblin", "goblins", "neck", "necks", "deer", "deers", "veado", "veados", "ciervo", "ciervos",
		"cervo", "cervi", "draugr", "draugrs",
		// the rest of the usual ones
		"sanglier", "sangliers", "cinghiale", "cinghiali", "fuling", "fulings",
		"skeleton", "skeletons", "esqueleto", "esqueletos", "squelette", "squelettes", "scheletro", "scheletri",
		"wolf", "wolves", "lobo", "lobos", "loup", "loups", "lupo", "lupi",
		"bear", "bears", "urso", "ursos", "oso", "osos", "ours", "orso", "orsi",
		"serpent", "serpents", "serpente", "serpentes", "serpiente", "serpientes",
		"deathsquito", "lox", "tick", "ticks", "seeker", "seekers", "gjall",
		"hare", "hares", "lebre", "lebres", "lievre", "lepre",
		"bat", "bats", "morcego", "morcegos", "murcielago", "chauve_souris", "pipistrello",
		"ghost", "ghosts", "fantasma", "fantasmas", "espectro", "spectre", "wraith"
	};

	/// <summary>The repair verbs: when they are there, "conserta a cozinha" keeps the repair instead of becoming cook.</summary>
	private static readonly string[] RepairVerbs = new string[]
	{
		// pt
		"conserta", "consertar", "conserto", "consertem", "repara", "reparar", "repare", "reparo", "reparem", "remenda",
		// en
		"repair", "repairs", "repairing", "fix", "fixes",
		// es
		"arregla", "arreglar", "arregle", "arreglen",
		// fr
		"reparer", "reparez", "reparent",
		// it
		"ripara", "riparare", "riparo", "riparate"
	};
}
