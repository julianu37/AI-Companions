using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Companions;

/// <summary>
/// Fase 3.1: the order the model understood, in the mod's own vocabulary.
/// Parsing is deliberately forgiving: a small local model wraps its JSON in prose, uses "null" as a string,
/// drops fields or answers in Portuguese. Anything it gets roughly right still works.
/// </summary>
internal sealed class LlmIntent
{
	internal const string ActionNone = "none";

	/// <summary>Canonical action ("wood", "follow", "attack", "stance_passive"...). Never null; "none" when nothing was understood.</summary>
	internal string Action = ActionNone;

	/// <summary>Item/creature named in the order, normalized, or null.</summary>
	internal string Resource;

	/// <summary>Fase 3.6: the creature or item the order pointed at ("troll", "machado_de_bronze"), normalized, or
	/// null. Used by attack/equip/drop; when the model answered with "resource"/"item" instead, that is taken as
	/// the target (see Parse).</summary>
	internal string Target;

	/// <summary>
	/// Fase 3.11: the companion the message is addressed to ("Gruncha, colete 10 pedras" -> "Gruncha"), as the model
	/// wrote it, trimmed and <b>not</b> normalized - the name is the player's, and it is matched against the real
	/// companions later (see <see cref="CompanionNames.Find"/>). Null when the message is not addressed to anyone.
	/// </summary>
	internal string Companion;

	/// <summary>How many were asked for, or 0 when the order didn't say.</summary>
	internal int Amount;

	/// <summary>Fase 3.5: what the companion says to the player, in its own words (the model's "reply"), or null.
	/// In an order it is the short confirmation; when the message is not an order it IS the answer, and the
	/// companion speaks it (see CompanionChatAI).</summary>
	internal string Reply;

	/// <summary>Raw text the model answered with, kept for the log and for "MostrarRespostaNoChat".</summary>
	internal string Raw = "";

	internal bool IsNone => string.IsNullOrEmpty(Action) || Action == ActionNone;

	private static readonly Regex ContentRegex = new Regex("\"content\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"", RegexOptions.Compiled | RegexOptions.Singleline);

	private static readonly Regex AmountRegex = new Regex("\"(?:amount|quantidade|qtd|count|numero|number)\"\\s*:\\s*\"?\\s*(\\d+)", RegexOptions.Compiled | RegexOptions.Singleline);

	private static readonly Regex ActionFieldRegex = new Regex("\"(?:action|acao|command|comando|intent|intencao)\"\\s*:\\s*(?:\"((?:[^\"\\\\]|\\\\.)*)\"|([^,}}\\s\\]]+))", RegexOptions.Compiled | RegexOptions.Singleline);

	private static readonly Regex ResourceFieldRegex = new Regex("\"(?:resource|recurso|item|object|objeto)\"\\s*:\\s*(?:\"((?:[^\"\\\\]|\\\\.)*)\"|([^,}}\\s\\]]+))", RegexOptions.Compiled | RegexOptions.Singleline);

	// Fase 3.6: the creature/item an attack/equip/drop order points at. Kept apart from "resource" (the item a
	// gathering job is about) so the same JSON can carry both without one overwriting the other.
	private static readonly Regex TargetFieldRegex = new Regex("\"(?:target|alvo|target_name|nome_alvo|creature|item_name)\"\\s*:\\s*(?:\"((?:[^\"\\\\]|\\\\.)*)\"|([^,}}\\s\\]]+))", RegexOptions.Compiled | RegexOptions.Singleline);

	// Fase 3.11: the companion the message is addressed to ("Gruncha, colete 10 pedras"). Same shape as "target"
	// (nullable, with the aliases a small model tends to use), but the value is kept as the model wrote it: the name
	// is the player's, and it is compared to the real companions without case/accents later.
	private static readonly Regex CompanionFieldRegex = new Regex("\"(?:companion|companheiro|companion_name|nome_companheiro|destinatario|addressee)\"\\s*:\\s*(?:\"((?:[^\"\\\\]|\\\\.)*)\"|([^,}}\\s\\]]+))", RegexOptions.Compiled | RegexOptions.Singleline);

	private static readonly Regex StanceFieldRegex = new Regex("\"(?:stance|postura)\"\\s*:\\s*(?:\"((?:[^\"\\\\]|\\\\.)*)\"|([^,}}\\s\\]]+))", RegexOptions.Compiled | RegexOptions.Singleline);

	// Fase 3.5: the companion's own words. Accepts the aliases a small model tends to use for it.
	private static readonly Regex ReplyFieldRegex = new Regex("\"(?:reply|resposta|fala|mensagem|message|say|texto|text)\"\\s*:\\s*(?:\"((?:[^\"\\\\]|\\\\.)*)\"|([^,}}\\s\\]]+))", RegexOptions.Compiled | RegexOptions.Singleline);

	private static readonly Regex NonWordRegex = new Regex("[^a-z0-9]+", RegexOptions.Compiled);

	/// <summary>
	/// Pulls the assistant's message out of an OpenAI-compatible chat completion body. Falls back to the whole
	/// body when the server answers in a different shape (or when the message came back empty and the JSON is
	/// somewhere else in it), so the tolerant field search below still gets a chance.
	/// </summary>
	internal static string ExtractContent(string responseBody)
	{
		if (string.IsNullOrEmpty(responseBody))
		{
			return "";
		}
		Match match = ContentRegex.Match(responseBody);
		if (match.Success)
		{
			string content = Unescape(match.Groups[1].Value);
			if (content.Trim().Length > 0)
			{
				return content;
			}
		}
		return responseBody.Trim();
	}

	/// <summary>Parses whatever the model said into a canonical action. Never throws.</summary>
	internal static LlmIntent Parse(string text)
	{
		LlmIntent intent = new LlmIntent
		{
			Raw = text ?? ""
		};
		try
		{
			string body = intent.Raw;
			int start = body.IndexOf('{');
			int end = body.LastIndexOf('}');
			if (start >= 0 && end > start)
			{
				body = body.Substring(start, end - start + 1);
			}
			string action = ReadField(ActionFieldRegex, body, noneIsNull: false);
			string resource = ReadField(ResourceFieldRegex, body, noneIsNull: true);
			string target = ReadField(TargetFieldRegex, body, noneIsNull: true);
			string stance = ReadField(StanceFieldRegex, body, noneIsNull: true);
			string reply = ReadField(ReplyFieldRegex, body, noneIsNull: true);
			// Fase 3.11: nullable like "target", and kept as written (only trimmed) - see the Companion field
			string companion = ReadField(CompanionFieldRegex, body, noneIsNull: true);

			Match amountMatch = AmountRegex.Match(body);
			if (amountMatch.Success && int.TryParse(amountMatch.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int amount))
			{
				intent.Amount = Math.Max(0, amount);
			}

			// no JSON object at all: a one-word answer like "wood" is taken as the action
			if (string.IsNullOrEmpty(action) && start < 0 && body.Length > 0 && body.Length <= 40)
			{
				action = body;
			}

			intent.Resource = string.IsNullOrEmpty(resource) ? null : Normalize(resource);
			intent.Target = string.IsNullOrEmpty(target) ? null : Normalize(target);
			intent.Action = Canonicalize(action, stance, intent.Resource, intent.Target);
			intent.Reply = string.IsNullOrEmpty(reply) ? null : reply.Trim();
			intent.Companion = string.IsNullOrEmpty(companion) ? null : companion.Trim();

			// Fase 3.5: a model that ignored the JSON contract and simply answered in prose - in conversation mode
			// that prose IS the answer (see CompanionChatAI). A one-word answer was already taken as the action
			// above, so only a sentence long enough to be speech is accepted here, and only when nothing else was
			// understood: no order can be swallowed by this.
			if (start < 0 && intent.Reply == null && intent.IsNone && body.IndexOf(' ') > 0 && body.Length <= 300)
			{
				intent.Reply = body.Trim();
			}

			// Fase 3.6: the model often files the named creature/item under the wrong field. An order that needs a
			// target takes "resource"/"item" when "target" is empty, and a gathering order takes "target" as its
			// resource - either way the order still lands on the right action instead of being thrown away.
			if (NeedsTarget(intent.Action))
			{
				if (intent.Target == null)
				{
					intent.Target = intent.Resource;
				}
				// the name is the target now: "resource" is documented as the item a gathering job is about, and
				// attack/equip/drop never read it, so keeping a copy would only be confusing in the log
				intent.Resource = null;
			}
			else if (intent.Resource == null && intent.Target != null)
			{
				intent.Resource = intent.Target;
			}
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log?.LogDebug("[IA] Couldn't parse the model's answer: " + ex.Message);
			intent.Action = ActionNone;
		}
		return intent;
	}

	private static string ReadField(Regex regex, string body, bool noneIsNull)
	{
		Match match = regex.Match(body);
		if (!match.Success)
		{
			return null;
		}
		string value = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
		if (string.IsNullOrEmpty(value))
		{
			return null;
		}
		value = Unescape(value).Trim();
		if (value.Length == 0 || value.Equals("null", StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}
		if (noneIsNull && value.Equals("none", StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}
		return value;
	}

	/// <summary>lowercase, no accents, no punctuation, single underscores between words.</summary>
	internal static string Normalize(string value)
	{
		if (string.IsNullOrEmpty(value))
		{
			return "";
		}
		string decomposed = value.Normalize(NormalizationForm.FormD);
		StringBuilder builder = new StringBuilder(decomposed.Length);
		foreach (char c in decomposed)
		{
			UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(c);
			if (category != UnicodeCategory.NonSpacingMark)
			{
				builder.Append(char.ToLowerInvariant(c));
			}
		}
		string text = builder.ToString().Normalize(NormalizationForm.FormC);
		text = NonWordRegex.Replace(text, "_");
		return text.Trim('_');
	}

	/// <summary>Fase 3.6: actions that need to know what they point at (a creature, an item).</summary>
	internal static bool NeedsTarget(string action)
	{
		return action == "attack" || action == "equip" || action == "drop";
	}

	private static string Canonicalize(string action, string stance, string resource, string target)
	{
		string normalizedAction = Normalize(action);
		string normalizedStance = Normalize(stance);

		// the model said "this isn't an order" - that wins over anything else in the answer
		if (normalizedAction == "none" || normalizedAction == "null")
		{
			return ActionNone;
		}

		// Fase 3.8: the inventory order, which the model writes in a dozen shapes ("inventory", "abrir_inventario",
		// "mostra_o_teu_inventario", "open bag"). Anything carrying the name of the pack is read as it; the aliases
		// below only cover the exact spellings.
		if (IsInventoryWord(normalizedAction))
		{
			return IsCloseWord(normalizedAction) ? "inventory_close" : "inventory";
		}

		// an explicit "stance" field ("balanced", "defensive"...) turns the action into a stance order
		if (!string.IsNullOrEmpty(normalizedStance) && string.IsNullOrEmpty(normalizedAction))
		{
			normalizedAction = normalizedStance;
		}

		if (normalizedAction.Length == 0)
		{
			return resource != null ? JobForResource(resource) : ActionNone;
		}
		if (Aliases.TryGetValue(normalizedAction, out string canonical))
		{
			if (canonical == "#gather")
			{
				// "colete/pega/gather": which job it means comes from what was named, generic collecting otherwise
				return resource != null ? JobForResource(resource) : "forage";
			}
			if (canonical == "#take")
			{
				// Fase 3.6: "pega a picareta" hands over a tool it carries, "pega madeira" is the gathering job.
				// A verb that takes an object only means "equip" when the object is gear - see GearWords. The model
				// may file the name under "resource" or under "target", so both are looked at.
				string named = resource ?? target;
				if (LooksLikeGear(named))
				{
					return "equip";
				}
				return named != null ? JobForResource(named) : "forage";
			}
			if (canonical == "#attack")
			{
				// Fase 3.6: "ataca o troll" fights that one creature; "ataca" alone is still the hunting job
				return (resource != null || target != null) ? "attack" : "hunt";
			}
			if (canonical == "#stance")
			{
				return StanceForName(normalizedAction);
			}
			return canonical;
		}
		return ActionNone;
	}

	private static string JobForResource(string resource)
	{
		if (string.IsNullOrEmpty(resource))
		{
			return "forage";
		}
		if (Resources.TryGetValue(resource, out string job))
		{
			return job;
		}
		// the player types plurals and longer names ("pega 10 madeiras", "corta madeira de carvalho"): the job is
		// still in there, so the whole name is tried without its plural and then word by word
		string singular = Singular(resource);
		if (singular != resource && Resources.TryGetValue(singular, out job))
		{
			return job;
		}
		foreach (string word in resource.Split('_'))
		{
			if (Resources.TryGetValue(word, out job))
			{
				return job;
			}
			string stem = Singular(word);
			if (stem != word && Resources.TryGetValue(stem, out job))
			{
				return job;
			}
		}
		return "forage";
	}

	/// <summary>singular/plural, the cheap way: "madeiras" and "madeira" are the same word to us.</summary>
	private static string Singular(string word)
	{
		return (word.Length > 3 && word[word.Length - 1] == 's') ? word.Substring(0, word.Length - 1) : word;
	}

	/// <summary>Fase 3.8: does this action word name the companion's pack ("inventario", "mochila", "bag")? A phrase
	/// that only says "carrega" counts when it also asks to SEE it ("mostrar_o_que_voce_carrega"); a bare
	/// "carregar_madeira" is not this order and is left to the aliases.</summary>
	private static bool IsInventoryWord(string action)
	{
		if (action.Contains("invent"))
		{
			return true;
		}
		string[] words = action.Split('_');
		bool carries = false;
		bool see = false;
		for (int i = 0; i < words.Length; i++)
		{
			string word = words[i];
			if (word == "mochila" || word == "bolsa" || word == "bag")
			{
				return true;
			}
			if (word.StartsWith("carrega"))
			{
				carries = true;
			}
			else if (IsSeeWord(word))
			{
				see = true;
			}
		}
		return carries && see;
	}

	/// <summary>Fase 3.8: the verbs that ask to SEE something ("abre", "mostra", "open").</summary>
	private static bool IsSeeWord(string word)
	{
		return word == "abre" || word == "abrir" || word == "abra" || word == "mostra" || word == "mostrar"
			|| word == "mostre" || word == "olha" || word == "olhar" || word == "ver" || word == "veja" || word == "ve"
			|| word == "open" || word == "show" || word == "exibe" || word == "exibir";
	}

	/// <summary>Fase 3.8: is the pack order about CLOSING the window ("fecha o inventário")? Only read when the
	/// action also named the pack, so a bare "fecha" is never taken as this.</summary>
	private static bool IsCloseWord(string action)
	{
		return action.Contains("fech") || action.Contains("close") || action.Contains("escond");
	}

	private static string StanceForName(string name)
	{
		if (name.Contains("passiv") || name.Contains("defensiv") || name.Contains("defend") || name.Contains("pacific"))
		{
			return "stance_passive";
		}
		if (name.Contains("ranged") || name.Contains("distanc") || name.Contains("arco") || name.Contains("bow"))
		{
			return "stance_ranged";
		}
		if (name.Contains("melee") || name.Contains("corpo") || name.Contains("sword") || name.Contains("espada"))
		{
			return "stance_melee";
		}
		return "stance_balanced";
	}

	private static string Unescape(string value)
	{
		if (string.IsNullOrEmpty(value) || value.IndexOf('\\') < 0)
		{
			return value;
		}
		StringBuilder builder = new StringBuilder(value.Length);
		for (int i = 0; i < value.Length; i++)
		{
			char c = value[i];
			if (c != '\\' || i + 1 >= value.Length)
			{
				builder.Append(c);
				continue;
			}
			i++;
			char escape = value[i];
			switch (escape)
			{
			case 'n':
				builder.Append('\n');
				break;
			case 'r':
				builder.Append('\r');
				break;
			case 't':
				builder.Append('\t');
				break;
			case 'u':
				if (i + 4 < value.Length && ushort.TryParse(value.Substring(i + 1, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ushort code))
				{
					builder.Append((char)code);
					i += 4;
				}
				break;
			default:
				builder.Append(escape);
				break;
			}
		}
		return builder.ToString();
	}

	/// <summary>Order the model writes -> the mod's canonical action. "#gather" and "#stance" are handled in code.</summary>
	private static readonly Dictionary<string, string> Aliases = BuildAliases();

	private static Dictionary<string, string> BuildAliases()
	{
		Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.Ordinal);
		Add(map, "follow", "follow", "seguir", "segue", "siga", "me_siga", "acompanhar", "acompanha", "vem_comigo", "fica_comigo", "comigo", "junto", "junto_de_mim");
		Add(map, "come", "come", "comehere", "come_here", "vem", "vem_aqui", "vem_ca", "venha", "venha_aqui", "chama", "chamar", "chamar_todos", "recall", "volta", "volta_aqui", "aqui");
		Add(map, "wood", "wood", "madeira", "lenha", "tora", "toras", "arvore", "arvores", "tree", "trees", "chop", "cortar", "cortar_arvore", "derrubar", "finewood", "madeira_fina");
		Add(map, "stone", "stone", "pedra", "pedras", "rocha", "rochas", "flint", "grausten", "blackmarble");
		Add(map, "ore", "ore", "minerio", "minerios", "mina", "minerar", "cobre", "copper", "ferro", "iron", "prata", "silver", "estanho", "tin", "metal", "scrap");
		Add(map, "forage", "forage", "coleta", "coletar_plantas", "colher", "colheita", "plantas", "frutas", "fruta", "bagas", "berries", "berry", "cogumelos", "mushroom", "forragear", "forrageiro");
		Add(map, "smelt", "smelt", "fundir", "fundicao", "forno", "refinar", "derreter", "fundidor");
		Add(map, "hunt", "hunt", "cacar", "caca", "hunting", "perseguir", "cacar_animais");
		// Fase 3.6: "atacar/matar" WITH a named creature is a direct attack on that one; without a name it stays
		// the hunting job (the behaviour of Fase 3.1) - see Canonicalize.
		Add(map, "#attack", "attack", "atacar", "ataca", "ataque", "atacam", "matar", "mata", "mate", "matem", "kill", "abater", "abate", "eliminar", "elimina", "destruir", "engajar", "engage", "acabar_com");
		Add(map, "equip", "equip", "equipar", "equipa", "equipe", "equipem", "wield", "empunhar", "empunha", "vestir", "veste", "calcar", "por", "poe", "poem", "colocar", "coloca", "usar", "usa", "use");
		Add(map, "drop", "drop", "dropar", "drope", "largar", "larga", "largue", "larguem", "jogar_fora", "joga_fora", "jogue_fora", "jogar", "descartar", "descarta", "descarte", "soltar", "solta", "deixar", "deixa", "dispensar", "dispensa", "remover", "remove", "tirar", "tira", "livrar_se");
		Add(map, "farm", "farm", "fazenda", "plantar", "plantacao", "agricultura", "farmar", "cultivar", "colher_plantacao");
		Add(map, "fish", "fish", "pescar", "pesca", "peixe", "pescaria");
		Add(map, "repair", "repair", "reparar", "consertar", "reparo", "reabastecer", "restock", "manutencao", "manter", "arrumar");
		Add(map, "cook", "cook", "cozinhar", "cozinha", "comida", "preparar_comida", "cozinhar_comida", "culinaria");
		Add(map, "stop", "stop", "parar", "para", "pare", "cancela", "cancelar", "chega", "basta", "espere", "esperar", "aguarde", "aguardar", "fique", "fica", "quieto", "sossega");
		Add(map, "sethome", "sethome", "set_home", "definir_casa", "define_casa", "marcar_casa", "casa", "aqui_e_sua_casa", "sua_casa_aqui", "home");
		Add(map, "wander", "wander", "vagar", "passear", "explorar", "andar_por_aqui", "circulando");
		Add(map, "nowander", "nowander", "no_wander", "parar_de_vagar", "nao_vagar", "sem_vagar", "parado");
		Add(map, "pickup", "pickup", "auto_pickup", "autopickup", "recolher", "recolher_itens", "pegar_itens", "catar");
		Add(map, "nopickup", "nopickup", "no_pickup", "nao_recolher", "parar_de_recolher", "nao_pegar", "nao_catar");
		Add(map, "command", "command", "commandable", "comandos", "aceitar_comandos", "obedecer", "ligar_comandos", "me_obedeca");
		Add(map, "nocommand", "nocommand", "no_command", "nocommandable", "ignorar_comandos", "desobedecer", "desligar_comandos", "nao_obedecer");
		Add(map, "stance_balanced", "stance_balanced", "balanced", "equilibrado", "balanceado", "postura_equilibrada", "normal", "neutro", "modo_normal");
		Add(map, "stance_passive", "stance_passive", "passive", "passivo", "defensivo", "defensive", "postura_passiva", "postura_defensiva", "defender", "defender_se", "nao_atacar", "pacifico", "cuidado");
		Add(map, "stance_ranged", "stance_ranged", "ranged", "distancia", "a_distancia", "postura_a_distancia", "arco", "arqueiro", "usar_arco", "besta", "crossbow");
		Add(map, "stance_melee", "stance_melee", "melee", "corpo_a_corpo", "postura_corpo_a_corpo", "espada", "corpo", "espadachim");
		// Fase 3.8: "abre o teu inventário", "mostra o que você carrega" - the companion opens its own pack for the
		// player. The bare verbs ("abre", "mostra") are here too: they mean nothing else to us.
		Add(map, "inventory", "inventory", "inventario", "inventarios", "inventario_do_companheiro", "abrir_inventario", "abre_inventario", "abre_o_inventario", "abre_o_teu_inventario", "abre_teu_inventario", "abrir_o_inventario", "abrir_o_teu_inventario", "mostra_inventario", "mostra_o_inventario", "mostra_o_teu_inventario", "mostrar_inventario", "mostrar_o_inventario", "ver_inventario", "ver_o_inventario", "abre", "abrir", "abra", "open", "mostra", "mostrar", "mostre", "mochila", "bolsa", "bag");
		Add(map, "inventory_close", "inventory_close", "close_inventory", "fechar_inventario", "fecha_inventario", "fechar_o_inventario", "fecha_o_inventario", "fechar_janela", "fecha_janela", "fechar", "fecha", "feche", "close", "esconder_inventario");
		Add(map, "none", "none", "null", "nada", "nenhuma", "nenhum", "n_a", "conversa", "conversation", "chat", "saudacao", "oi", "ola", "bom_dia", "boa_tarde", "boa_noite", "obrigado", "obrigada", "valeu", "hello", "hi", "hey", "thanks", "pergunta", "duvida");
		// verbs that mean "go and collect something": the job comes from the resource field
		Add(map, "#gather", "gather", "collect", "coletar", "colete", "coletem", "peguem", "apanhar", "apanha", "minerar_tudo", "juntar", "junta");
		// Fase 3.6: verbs that take an object - a resource to gather ("pega madeira") or a tool to hold ("pega a
		// picareta"); which one it is comes from the word itself (see GearWords)
		Add(map, "#take", "take", "pega", "pegue", "pegar", "buscar", "busca", "busque", "trazer", "traz", "traga", "pegar_item", "buscar_item");
		return map;
	}

	private static void Add(Dictionary<string, string> map, string canonical, params string[] aliases)
	{
		foreach (string alias in aliases)
		{
			map[Normalize(alias)] = canonical;
		}
	}

	/// <summary>Fase 3.6: words that name a piece of gear the companion can hold, so "pega a picareta" is read as
	/// "equip the pickaxe" while "pega madeira" stays the gathering job. Materials, food and potions are absent on
	/// purpose - those are never equipped. Matched against the whole name and against each of its words, so
	/// "pega o machado de bronze" counts as gear too.</summary>
	private static readonly HashSet<string> GearWords = BuildGearWords();

	/// <summary>Is this the name of something the companion can hold? ("machado de bronze" -> yes)</summary>
	private static bool LooksLikeGear(string name)
	{
		if (string.IsNullOrEmpty(name))
		{
			return false;
		}
		if (GearWords.Contains(name))
		{
			return true;
		}
		foreach (string word in name.Split('_'))
		{
			if (GearWords.Contains(word) || GearWords.Contains(Singular(word)))
			{
				return true;
			}
		}
		return false;
	}

	private static HashSet<string> BuildGearWords()
	{
		HashSet<string> set = new HashSet<string>(StringComparer.Ordinal);
		string[] words = new string[68]
		{
			"machado", "axe", "picareta", "pickaxe", "espada", "sword", "faca", "knife", "adaga", "dagger",
			"lanca", "spear", "arpao", "harpoon", "arco", "bow", "besta", "crossbow", "escudo", "shield",
			"broquel", "buckler", "elmo", "helmet", "capacete", "armadura", "armor", "peitoral", "chest",
			"calcas", "pernas", "legs", "perneiras", "capa", "cape", "manto", "cloak", "tocha", "torch",
			"cajado", "staff", "martelo", "hammer", "maca", "mace", "porrete", "club", "marreta", "sledge",
			"atgeir", "atgedir", "vara", "fishingrod", "trinket", "amulet", "amuleto", "anel", "ring",
			"cinto", "belt", "ferramenta", "tool", "arma", "weapon", "flecha", "arrow", "virote", "bolt"
		};
		for (int i = 0; i < words.Length; i++)
		{
			set.Add(Normalize(words[i]));
		}
		return set;
	}

	private static readonly Dictionary<string, string> Resources = BuildResources();

	private static Dictionary<string, string> BuildResources()
	{
		Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.Ordinal);
		AddResource(map, "wood", "wood", "madeira", "lenha", "tora", "toras", "arvore", "arvores", "tree", "log", "finewood", "madeira_fina", "resin", "resina", "carvalho", "oak");
		AddResource(map, "stone", "stone", "pedra", "pedras", "rocha", "rochas", "flint", "silex", "grausten", "blackmarble", "marmore");
		AddResource(map, "ore", "ore", "minerio", "minerios", "cobre", "copper", "ferro", "iron", "prata", "silver", "estanho", "tin", "metal", "obsidiana", "obsidian", "scrap", "sucata");
		AddResource(map, "forage", "forage", "planta", "plantas", "fruta", "frutas", "baga", "bagas", "berry", "berries", "cogumelo", "cogumelos", "mushroom", "semente", "sementes", "seed", "seeds", "mel", "honey", "thistle", "cardo", "dandelion", "dente_de_leao", "cenoura", "carrot", "nabo", "turnip", "cebola", "onion", "cevada", "barley", "linho", "flax");
		return map;
	}

	private static void AddResource(Dictionary<string, string> map, string job, params string[] names)
	{
		foreach (string name in names)
		{
			map[Normalize(name)] = job;
		}
	}
}
