using System;
using System.Collections.Generic;

namespace Companions;

/// <summary>
/// Fase M3 (bug do teste em francês): com o jogo em francês, "maria ramasse 10 bois" ("Maria, recolhe 10 madeiras")
/// voltou do modelo como <c>pickup</c> - "Je prends les bois." - e o companheiro foi catar item no chão em vez de
/// cortar madeira. A causa era o contrato do prompt (100% PT-BR, ver <see cref="CompanionChatAI.Contract"/>, agora
/// por idioma); esta é a <b>rede de segurança</b> dele, no mesmo espírito da 3.1 §8.2
/// (<see cref="PickupIntent.NeighborAction"/>) e da 3.1 §6 ("o recurso manda").
///
/// <para>Quando o modelo devolve <c>pickup</c> (ou <c>none</c>) e as palavras do jogador dizem <b>COLETAR</b> - um
/// recurso conhecido em PT/EN/ES/FR <b>e</b> um número, <b>sem</b> marcador de chão ("do chão", "par terre") e
/// <b>sem</b> "itens/objetos" -, a ordem de coleta vence: a ação passa a ser a daquele recurso (wood/stone/ore/
/// forage) e o número que o modelo deixou nulo sai do próprio texto. O caso legítimo de <c>pickup</c> (tem marcador
/// de chão) não é tocado, e <c>nopickup</c> nunca é lido aqui.</para>
///
/// <para><b>Puro</b> de propósito (sem Unity, sem config, sem mundo): é chamado uma vez por mensagem (pelo
/// <see cref="CompanionOrders.Run"/>, para <c>pickup</c>, e pelo <see cref="CompanionChatAI"/>, para <c>none</c>) e a
/// tabela de palavras pode ser conferida fora do jogo.</para>
/// </summary>
internal static class GatherIntent
{
	/// <summary>A ação que recolhe itens do chão - a única que esta correção entende como "talvez o jogador pediu coleta".</summary>
	internal const string PickupAction = "pickup";

	/// <summary>A resposta do modelo para "não é uma ordem": também é olhada, porque o modelo manda ordens de coleta para cá.</summary>
	internal const string NoneAction = "none";

	/// <summary>Esta correção só olha duas ações - nenhuma outra é mexida.</summary>
	internal static bool Accepts(string action)
	{
		return string.Equals(action, PickupAction, StringComparison.Ordinal)
			|| string.Equals(action, NoneAction, StringComparison.Ordinal);
	}

	/// <summary>
	/// A ação de coleta que as palavras do jogador pedem ("wood", "stone", "ore" ou "forage"), ou null quando não há
	/// nada a corrigir. <paramref name="amount"/> sai com a quantidade lida do texto (0 quando não há).
	/// </summary>
	/// <param name="action">A ação do modelo, já depois das correções anteriores.</param>
	/// <param name="message">O que o JOGADOR disse (chat ou voz), do jeito que chegou.</param>
	internal static string FromModelAction(string action, string message, out int amount)
	{
		amount = 0;
		if (!Accepts(action) || string.IsNullOrEmpty(message))
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
			// matched between separators, so "sol" never answers "solo" and "chao" has to appear whole
			string padded = "_" + text + "_";
			// the legitimate pickup: on the ground / on the floor / "os itens"
			if (HasAny(padded, GroundWords))
			{
				return null;
			}
			string gather = ResourceAction(padded);
			if (gather == null)
			{
				return null;   // no known resource: nothing to correct
			}
			// the number is what the model left null ("ramasse 10 bois")
			int number = NumberIn(text);
			if (number <= 0)
			{
				return null;   // no quantity: a plain "ramasse" keeps the pickup of the model
			}
			// on the "none" side the words also have to carry a collecting verb: a conversation with a number and a
			// material ("você tem 10 madeiras?") must not become an order (the model's "pickup" already says it is one)
			if (string.Equals(action, NoneAction, StringComparison.Ordinal) && !HasAny(padded, CollectVerbs))
			{
				return null;
			}
			amount = number;
			return gather;
		}
		catch (Exception e)
		{
			CompanionsPlugin.Log?.LogDebug("[IA] Couldn't read the player's own words for a gathering order: " + e.Message);
			return null;
		}
	}

	/// <summary>The gather action of the first resource named in the message, or null.</summary>
	private static string ResourceAction(string padded)
	{
		if (HasAny(padded, WoodWords))
		{
			return "wood";
		}
		if (HasAny(padded, StoneWords))
		{
			return "stone";
		}
		if (HasAny(padded, OreWords))
		{
			return "ore";
		}
		if (HasAny(padded, ForageWords))
		{
			return "forage";
		}
		return null;
	}

	/// <summary>Is one of these words/phrases in the message? (whole words - the pads are the message's separators)</summary>
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

	/// <summary>The first number of the message ("ramasse 10 bois" -> 10; 0 when there is none). Orders never ask for
	/// thousands, so the number is capped at three digits.</summary>
	private static int NumberIn(string text)
	{
		for (int i = 0; i < text.Length; i++)
		{
			if (text[i] < '0' || text[i] > '9')
			{
				continue;
			}
			int value = 0;
			int digits = 0;
			while (i < text.Length && text[i] >= '0' && text[i] <= '9' && digits < 3)
			{
				value = value * 10 + (text[i] - '0');
				digits++;
				i++;
			}
			if (value > 0)
			{
				return value;
			}
		}
		return 0;
	}

	/// <summary>The words that mean "on the ground" (pt/en/es/fr) - the legitimate <c>pickup</c> - plus the words for
	/// items ("itens", "objets"), which also mean picking things up instead of gathering.</summary>
	private static readonly string[] GroundWords = new string[]
	{
		// pt
		"chao", "chaos", "no_chao", "do_chao", "ao_chao",
		// en
		"ground", "grounds", "floor",
		// es
		"suelo", "piso",
		// fr
		"sol", "terre", "par_terre", "a_terre",
		// itens/objetos
		"item", "itens", "items", "objet", "objets", "objeto", "objetos"
	};

	/// <summary>The verbs of collecting/taking in the four languages, used only on the <c>none</c> side (see the
	/// summary of <see cref="FromModelAction"/>).</summary>
	private static readonly string[] CollectVerbs = new string[]
	{
		// pt
		"coleta", "colete", "coletem", "coletar", "junta", "junte", "juntem", "juntar", "corta", "corte", "cortar",
		"cata", "cate", "catar", "traz", "traga", "trazer", "busca", "busque", "buscar", "pega", "pegue", "pegar",
		"minera", "minere", "minerar", "extrai",
		// en
		"gather", "gathers", "collect", "get", "bring", "chop", "cut", "mine", "grab", "fetch", "pick",
		// es
		"recoge", "recoger", "recoges", "coge", "coger", "trae", "traer", "junta", "juntar", "corta", "cortar",
		"mina", "minar", "pica", "picar", "busca", "buscar", "consigue", "conseguir",
		// fr
		"ramasse", "ramasser", "ramasses", "recolte", "recolter", "prends", "prend", "prendre", "apporte", "apporter",
		"coupe", "couper", "mine", "miner", "cherche", "chercher", "recupere", "recuperer"
	};

	/// <summary>The resources of the four gathering jobs, in the four languages (normalized: no accents, lowercase).</summary>
	private static readonly string[] WoodWords = new string[]
	{
		"madeira", "madeiras", "lenha", "tronco", "troncos", "arvore", "arvores",
		"wood", "woods", "tree", "trees", "logs", "log",
		"madera", "maderas", "arbol", "arboles", "troncos",
		"bois", "buche", "buches", "arbre", "arbres", "rondins", "rondin"
	};

	private static readonly string[] StoneWords = new string[]
	{
		"pedra", "pedras", "rocha", "rochas", "pedregulho",
		"stone", "stones", "rock", "rocks",
		"piedra", "piedras", "roca", "rocas",
		"pierre", "pierres", "rocher", "rochers", "caillou", "cailloux"
	};

	private static readonly string[] OreWords = new string[]
	{
		"minerio", "minerios", "metal", "metais", "ferro", "cobre", "prata", "estanho",
		"ore", "ores", "iron", "copper", "silver", "tin", "metals",
		"mineral", "minerales", "hierro", "plata", "estano", "metales",
		"minerai", "minerais", "fer", "cuivre", "argent", "etain", "metaux"
	};

	private static readonly string[] ForageWords = new string[]
	{
		"forragem", "forragear", "forrageio", "fruta", "frutas", "baga", "bagas", "framboesa", "framboesas",
		"cogumelo", "cogumelos", "mirtilo", "mirtilos", "amora", "amoras",
		"forage", "fruit", "fruits", "berry", "berries", "raspberry", "raspberries", "mushroom", "mushrooms",
		"blueberry", "blueberries", "blackberry", "blackberries",
		"forraje", "baya", "bayas", "frambuesa", "frambuesas", "champinon", "champinones",
		"arandano", "arandanos", "mora", "moras",
		"cueillette", "baie", "baies", "framboise", "framboises", "champignon", "champignons",
		"myrtille", "myrtilles", "mure", "mures"
	};
}
