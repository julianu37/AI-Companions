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

	/// <summary>Canonical action ("wood", "follow", "stance_passive"...). Never null; "none" when nothing was understood.</summary>
	internal string Action = ActionNone;

	/// <summary>Item/creature named in the order, normalized, or null.</summary>
	internal string Resource;

	/// <summary>How many were asked for, or 0 when the order didn't say.</summary>
	internal int Amount;

	/// <summary>Raw text the model answered with, kept for the log and for "MostrarRespostaNoChat".</summary>
	internal string Raw = "";

	internal bool IsNone => string.IsNullOrEmpty(Action) || Action == ActionNone;

	private static readonly Regex ContentRegex = new Regex("\"content\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"", RegexOptions.Compiled | RegexOptions.Singleline);

	private static readonly Regex AmountRegex = new Regex("\"(?:amount|quantidade|qtd|count|numero|number)\"\\s*:\\s*\"?\\s*(\\d+)", RegexOptions.Compiled | RegexOptions.Singleline);

	private static readonly Regex ActionFieldRegex = new Regex("\"(?:action|acao|command|comando|intent|intencao)\"\\s*:\\s*(?:\"((?:[^\"\\\\]|\\\\.)*)\"|([^,}}\\s\\]]+))", RegexOptions.Compiled | RegexOptions.Singleline);

	private static readonly Regex ResourceFieldRegex = new Regex("\"(?:resource|recurso|item|target|alvo|object|objeto)\"\\s*:\\s*(?:\"((?:[^\"\\\\]|\\\\.)*)\"|([^,}}\\s\\]]+))", RegexOptions.Compiled | RegexOptions.Singleline);

	private static readonly Regex StanceFieldRegex = new Regex("\"(?:stance|postura)\"\\s*:\\s*(?:\"((?:[^\"\\\\]|\\\\.)*)\"|([^,}}\\s\\]]+))", RegexOptions.Compiled | RegexOptions.Singleline);

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
			string stance = ReadField(StanceFieldRegex, body, noneIsNull: true);

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
			intent.Action = Canonicalize(action, stance, intent.Resource);
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

	private static string Canonicalize(string action, string stance, string resource)
	{
		string normalizedAction = Normalize(action);
		string normalizedStance = Normalize(stance);

		// the model said "this isn't an order" - that wins over anything else in the answer
		if (normalizedAction == "none" || normalizedAction == "null")
		{
			return ActionNone;
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
		if (Resources.TryGetValue(resource, out string job))
		{
			return job;
		}
		return "forage";
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
		Add(map, "hunt", "hunt", "cacar", "caca", "atacar", "attack", "matar", "kill", "abater", "hunting", "perseguir");
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
		Add(map, "none", "none", "null", "nada", "nenhuma", "nenhum", "n_a", "conversa", "conversation", "chat", "saudacao", "oi", "ola", "bom_dia", "boa_tarde", "boa_noite", "obrigado", "obrigada", "valeu", "hello", "hi", "hey", "thanks", "pergunta", "duvida");
		// verbs that mean "go and collect something": the job comes from the resource field
		Add(map, "#gather", "gather", "collect", "coletar", "colete", "coletem", "pegar", "pega", "pegue", "peguem", "buscar", "busca", "busque", "trazer", "traz", "traga", "apanhar", "apanha", "minerar_tudo", "juntar", "junta");
		return map;
	}

	private static void Add(Dictionary<string, string> map, string canonical, params string[] aliases)
	{
		foreach (string alias in aliases)
		{
			map[Normalize(alias)] = canonical;
		}
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
