using System;
using System.Collections.Generic;
using System.Text;

namespace Companions;

/// <summary>
/// Fase 3.11: <b>who the player is talking to</b>. With several companions, "colete 10 pedras" used to be handed to
/// every one of them - three companions dropping what they were doing to chop the same tree. The player can now call
/// one by name ("Gruncha, colete 10 pedras") and only that one takes the order; the others are not touched at all.
///
/// <para>Two halves, and this class is the single place both go through:</para>
/// <list type="bullet">
/// <item><see cref="PromptLine"/> lists the player's companions by name in the prompt, so the model can fill the
/// <c>companion</c> key of the contract (see <see cref="CompanionChatAI.Contract"/>);</item>
/// <item><see cref="FromMessage"/> is the safety net for the model that leaves the key empty or invents a name: the
/// <b>player's own words</b> are read for a name that <b>really exists</b> among the player's companions. Only a real
/// name counts - nothing is invented, and a word that merely looks like a name is refused (the same spirit as the
/// Fase 3.6b fallback and <see cref="PickupIntent"/>).</item>
/// </list>
///
/// <para>The string work lives in <see cref="CompanionNameMatch"/> (no Unity, so the matching rules can be checked
/// outside the game); this class is the Unity side: whose companions, what their names are, and what happens on a
/// tie. Runs once per message (and once per prompt), never per frame.</para>
/// </summary>
internal static class CompanionNames
{
	/// <summary>
	/// How many names the prompt lists. The prompt is sent with every message and is already ~1.800 tokens, so the
	/// list is capped: a player with more companions than this still gets the safety net of
	/// <see cref="FromMessage"/>, which knows every name.
	/// </summary>
	private const int MaxListedNames = 12;

	/// <summary>
	/// The local player's companions in the scene (dead ones included: their names still have to be recognized, so a
	/// message to a dead companion never becomes an order to everybody else). Ownership is the check the rest of the
	/// mod uses (<see cref="MHR_Owner.IsMine"/>), so another player's companions are never here. Allocates a list:
	/// called once per message, never per frame.
	/// </summary>
	internal static List<CompanionSetup> Mine()
	{
		List<CompanionSetup> list = new List<CompanionSetup>(4);
		if (Player.m_localPlayer == null)
		{
			return list;
		}
		IReadOnlyList<CompanionSetup> all = CompanionSetup.AllCompanions;
		for (int i = 0; i < all.Count; i++)
		{
			CompanionSetup setup = all[i];
			if (setup == null || setup._mhrStale || !MHR_Owner.IsMine(setup))
			{
				continue;
			}
			if (setup.GetComponent<ZNetView>()?.GetZDO() == null)
			{
				continue;
			}
			list.Add(setup);
		}
		return list;
	}

	/// <summary>
	/// The name of a companion as the player sees it: the one given to it (the ZDO's <c>HC_Name</c>, which the
	/// interact panel writes when it is renamed) or, for a companion that was never renamed, the name it carries as a
	/// Character (the tier name the hover shows). Never null; "" only when there is no companion at all.
	/// </summary>
	internal static string NameOf(CompanionSetup setup)
	{
		ZDO zdo = setup != null ? setup.GetComponent<ZNetView>()?.GetZDO() : null;
		string name = zdo != null ? zdo.GetString(CompanionSetup.NameHash, "") : null;
		if (string.IsNullOrEmpty(name))
		{
			Character character = setup != null ? setup.GetComponent<Character>() : null;
			name = character != null ? character.m_name : null;
		}
		return name == null ? "" : name.Trim();
	}

	/// <summary>
	/// The line the prompt carries: <c>Companheiros seus: Gruncha, Gruncho, Grunchinho.</c> (in Portuguese, like the
	/// rest of the contract), or "" when the player has none. Dead companions are left out - they cannot take an order
	/// - and so is the remainder of a list longer than <see cref="MaxListedNames"/>.
	/// </summary>
	internal static string PromptLine()
	{
		List<CompanionSetup> mine = Mine();
		StringBuilder builder = new StringBuilder(96);
		int shown = 0;
		for (int i = 0; i < mine.Count && shown < MaxListedNames; i++)
		{
			CompanionSetup setup = mine[i];
			Character character = setup.GetComponent<Character>();
			if (character != null && character.GetHealth() <= 0f)
			{
				continue;
			}
			string name = NameOf(setup);
			if (name.Length == 0)
			{
				continue;
			}
			if (shown > 0)
			{
				builder.Append(", ");
			}
			builder.Append(name);
			shown++;
		}
		return shown == 0 ? "" : "Companheiros seus: " + builder + ".\n";
	}

	/// <summary>
	/// The companion a name (from the model's <c>companion</c> key) really points at, or null when no companion of the
	/// local player answers to it. Compared without case and without accents; a leading article is ignored
	/// ("o Gruncha"). Two companions that answer to the same name: the first one wins and the tie is said in the log
	/// (the walk is deterministic, so the same message always picks the same one).
	/// </summary>
	internal static CompanionSetup Find(string spoken)
	{
		if (string.IsNullOrEmpty(spoken))
		{
			return null;
		}
		List<CompanionSetup> mine = Mine();
		if (mine.Count == 0)
		{
			return null;
		}
		CompanionSetup found = null;
		int matches = 0;
		for (int i = 0; i < mine.Count; i++)
		{
			if (!CompanionNameMatch.Same(spoken, NameOf(mine[i])))
			{
				continue;
			}
			if (found == null)
			{
				found = mine[i];
			}
			matches++;
		}
		if (matches > 1)
		{
			LogTie(spoken, matches, found);
		}
		return found;
	}

	/// <summary>
	/// The safety net: does the player's OWN message start with (or open with, before a comma) the name of one of their
	/// companions? "Gruncha, colete 10 pedras", "gruncha colete 10 pedras" and "Ei, Gruncha, ..." all answer her; a
	/// message that names nobody answers null and the order keeps its old, every-companion behaviour.
	///
	/// <para>Only whole names that <b>exist</b> are accepted (never a word that looks like one), and the name is looked
	/// for at the beginning of the message and in the part before its first comma - not in the middle of a sentence, so
	/// "colete 10 pedras perto do Gruncha" stays a normal order.</para>
	/// </summary>
	/// <param name="matchedName">The name as the companion carries it, for the log and the lines - null when nobody
	/// was named.</param>
	internal static CompanionSetup FromMessage(string message, out string matchedName)
	{
		matchedName = null;
		if (string.IsNullOrEmpty(message))
		{
			return null;
		}
		List<CompanionSetup> mine = Mine();
		if (mine.Count == 0)
		{
			return null;
		}
		string[] names = new string[mine.Count];
		for (int i = 0; i < mine.Count; i++)
		{
			names[i] = NameOf(mine[i]);
		}
		string matched = CompanionNameMatch.InMessage(message, names);
		if (matched == null)
		{
			return null;
		}
		matchedName = matched;
		// the same name on two companions: the first one wins (the same rule as Find), and the tie is logged.
		// Fase 3.12: the count uses Same (case/accents ignored), like the match itself - two companions "Gruncha" and
		// "Grüncha" are the same name to the player, and an exact comparison would miss the tie.
		int matches = 0;
		CompanionSetup found = null;
		for (int i = 0; i < mine.Count; i++)
		{
			if (!CompanionNameMatch.Same(names[i], matched))
			{
				continue;
			}
			if (found == null)
			{
				found = mine[i];
			}
			matches++;
		}
		if (matches > 1)
		{
			LogTie(matched, matches, found);
		}
		return found;
	}

	/// <summary>
	/// Fase 3.12: the opening of the message that <b>looks like a call to someone the mod doesn't know</b> ("Fulano" in
	/// "Fulano, colete 10 pedras"), or null when the message opens with an order, a greeting, or the name of a real
	/// companion. It is what lets the caller say "I don't know any companion called Fulano" instead of quietly
	/// handing the order to everybody - and, on the other side, what keeps a message that names nobody ("colete 10
	/// pedras") on the old, every-companion path.
	/// </summary>
	internal static string UnknownAddress(string message)
	{
		if (string.IsNullOrEmpty(message))
		{
			return null;
		}
		// a real companion answers first: then this is not an unknown name
		if (FromMessage(message, out _) != null)
		{
			return null;
		}
		string candidate = CompanionNameMatch.AddressCandidate(message);
		return candidate;
	}

	private static void LogTie(string spoken, int matches, CompanionSetup found)
	{
		CompanionsPlugin.Log?.LogInfo($"[IA] \"{spoken}\" answers to {matches} companions with the same name - using \"{NameOf(found)}\" (the first one)");
	}
}

/// <summary>
/// Fase 3.11: the <b>string rules</b> of "who is the player talking to", with no Unity in them (so they can be checked
/// outside the game): the name list is just text, and so is the player's message.
///
/// <list type="bullet">
/// <item>names are compared <b>without case and without accents</b> (<see cref="LlmIntent.Normalize"/>), because the
/// player types "gruncha" and the companion is "Gruncha"; nothing else is normalized, and the name is never
/// rewritten;</item>
/// <item>a leading article is ignored on both sides ("o Gruncha");</item>
/// <item>in the message, the name is looked for at the <b>beginning</b> (with or without a comma after it) and in the
/// <b>part before the first comma</b> ("Ei, Gruncha, ...") - never in the middle of a sentence;</item>
/// <item>only a whole name from the list counts: no prefixes, no fuzzy matching, nothing invented.</item>
/// </list>
/// </summary>
internal static class CompanionNameMatch
{
	/// <summary>Longest name, in words, looked for in the player's message ("Gruncha" is 1, "Ana Maria" is 2).</summary>
	internal const int MaxNameWords = 4;

	/// <summary>
	/// Shortest name accepted <b>from the message</b> (Fase 3.12): a one or two letter name ("Lu", "Bo") would answer
	/// every "oi", "a", "já" of a normal sentence, so the message route needs three letters. The model's field is
	/// still matched against the full name (<see cref="Same"/>), because there the name was written on purpose.
	/// </summary>
	internal const int MinMessageNameLength = 3;

	/// <summary>Where the addressed part of a message ends: "Gruncha, colete 10 pedras" / "Ei, Gruncha, ...".</summary>
	private static readonly char[] SegmentSeparators = new char[3] { ',', ';', ':' };

	/// <summary>What separates the words of an address ("Ana Maria, ...").</summary>
	private static readonly char[] WordSeparators = new char[3] { ' ', '\t', '\n' };

	/// <summary>The articles a name may be preceded by, in the model's field or in the player's words ("o Gruncha").</summary>
	private static readonly HashSet<string> Articles = new HashSet<string>(StringComparer.Ordinal)
	{
		"o", "a", "os", "as", "the"
	};

	/// <summary>
	/// The words an address may open with before the name ("ei Gruncha, ...", "olha Gruncha, ..."). One of them is
	/// skipped before the name is looked for, so the very common "ei, Gruncha" works with or without a comma - and
	/// only one, so a full sentence never becomes an address by accident. They are also never accepted AS a name read
	/// from the message (Fase 3.12): a companion called "Olha" must not hijack "olha, o greydwarf está atacando".
	/// </summary>
	private static readonly HashSet<string> AddressPrefixes = new HashSet<string>(StringComparer.Ordinal)
	{
		"ei", "oi", "ola", "hey", "ah", "oh", "psiu", "escuta", "olha", "entao", "opa", "alo"
	};

	/// <summary>
	/// Openings that are not an address to anyone, even when they come before a comma (Fase 3.12): greetings,
	/// fillers and politeness. They are what keeps "bom dia, tudo bem?", "certo, colete 10 pedras" and
	/// "por favor, segue" from being read as a call to a companion nobody knows. The order verbs and the greetings
	/// the parser already knows are covered separately (<see cref="LlmIntent.IsIntentWord"/>), so this list only adds
	/// the social words that never reach the parser.
	/// </summary>
	private static readonly HashSet<string> NotAnAddress = new HashSet<string>(StringComparer.Ordinal)
	{
		"bom_dia", "boa_tarde", "boa_noite", "tudo_bem", "tudo_bom", "e_ai", "eai", "beleza", "blz", "tranquilo",
		"valeu", "obrigado", "obrigada", "desculpa", "desculpe", "por_favor", "please", "thanks", "hello", "hi",
		"ok", "okay", "certo", "claro", "sim", "nao", "tudo_certo", "como_vai", "como_esta", "calma", "espera",
		"cuidado", "socorro", "ajuda", "vamos", "rapido", "agora", "forca",
		// the openers a player uses to call the ATTENTION of the world, not of one companion
		"atencao", "galera", "pessoal", "gente", "rapaziada", "amigo", "amiga", "chefe", "patrao", "senhor",
		"senhora", "veja", "olhe", "escuta", "psiuzinho"
	};

	/// <summary>Longest address candidate accepted by <see cref="AddressCandidate"/>, in characters (a call is short).</summary>
	private const int MaxAddressChars = 24;

	/// <summary>
	/// Fase 3.19 (auditoria): the forms of address and the fillers that are <b>never</b> a companion's name, even
	/// capitalized and followed by a comma ("Mano, colete 10 pedras", "Companheiro, segue", "Bora, ..."). The
	/// candidate used to be refused only when it was in <see cref="NotAnAddress"/>, so an ordinary "mano," made the
	/// mod answer "I don't know that companion" and order nothing. A real companion with one of these names is still
	/// recognized, because the name lookup runs first (see <see cref="CompanionNames.UnknownAddress"/>).
	/// </summary>
	private static readonly HashSet<string> CommonAddressWords = new HashSet<string>(StringComparer.Ordinal)
	{
		"mano", "mana", "manos", "manas", "maninho", "companheiro", "companheira", "companheiros", "companheiras",
		"parceiro", "parceira", "parca", "cara", "cria", "vei", "veio", "bro", "brow", "bru", "mermao", "meu", "minha",
		"irmao", "irma", "tio", "tia", "filho", "filha", "garoto", "garota", "moleque", "molecada", "turma", "mocada",
		"time", "equipe", "galera", "pessoal", "gente", "rapaziada", "bora", "vamos", "vamo", "dale", "partiu",
		"guerreiro", "guerreiros", "guerreira", "soldado", "soldados", "camarada", "camaradas", "chefe", "patrao",
		"senhor", "senhora", "doutor", "doutora", "professor", "professora", "amigo", "amiga", "amigos", "amigas",
		"querido", "querida", "lindo", "linda", "criatura", "bicho", "rapaz", "moca", "jovem", "velho", "velha",
		"campeao", "campeoes", "mestre", "mestres", "vizinho", "vizinha", "chefao", "nobre", "senhorio", "colega", "colegas"
	};

	/// <summary>
	/// Fase 3.12: the part of the message that <b>looks like a call to someone</b> ("Fulano" in "Fulano, colete 10
	/// pedras"), or null when the message doesn't open with one. It answers "is this message addressed to somebody?"
	/// for names the mod doesn't know - and it is deliberately narrow, because a false positive here means an order
	/// that is refused:
	/// <list type="bullet">
	/// <item>the call has to be followed by a comma, a semicolon or a colon - without a separator "colete 10 pedras"
	/// is an order, not an address;</item>
	/// <item>it has to be short (1 to <see cref="MaxNameWords"/> words, <see cref="MaxAddressChars"/> characters at
	/// most): a whole sentence before the comma is not a name;</item>
	/// <item>it cannot be a word the mod already knows (an order verb, a greeting: <see cref="LlmIntent.IsIntentWord"/>),
	/// a social word (<see cref="NotAnAddress"/>), an article or a lone address prefix ("ei," / "olha,") - those are
	/// how people talk, not a name.</item>
	/// </list>
	///
	/// <para>Fase 3.19 (auditoria): on top of that, the opening has to <b>look like a name</b> - capitalized and not one
	/// of the common forms of address (<see cref="CommonAddressWords"/>). Before this, "mano, colete 10 pedras" and
	/// "companheiro, segue" were read as a call to a companion nobody knows, and the order was refused instead of
	/// going to everybody. When in doubt the message is <b>not</b> an address: the order runs as usual.</para>
	/// </summary>
	internal static string AddressCandidate(string message)
	{
		if (string.IsNullOrEmpty(message))
		{
			return null;
		}
		int cut = message.IndexOfAny(SegmentSeparators);
		if (cut <= 0)
		{
			return null;
		}
		string head = message.Substring(0, cut).Trim();
		if (head.Length < MinMessageNameLength || head.Length > MaxAddressChars)
		{
			return null;
		}
		string[] words = head.Split(WordSeparators, StringSplitOptions.RemoveEmptyEntries);
		if (words.Length == 0 || words.Length > MaxNameWords)
		{
			return null;
		}
		string key = Key(head);
		if (key.Length < MinMessageNameLength)
		{
			return null;
		}
		if (NotAnAddress.Contains(key) || AddressPrefixes.Contains(key) || Articles.Contains(key))
		{
			return null;
		}
		if (LlmIntent.IsIntentWord(head))
		{
			return null;
		}
		// Fase 3.19: and it has to look like a name (see the summary above) - "mano," is how a player opens a sentence
		if (!LooksLikeName(head, key))
		{
			return null;
		}
		// everything before the separator is an address-looking name: hand it back exactly as the player wrote it,
		// so the warning can quote it
		return head;
	}

	/// <summary>Fase 3.19: the shape of a name - it opens with a capital letter and none of its words is a common form
	/// of address ("Mano", "Companheiro", "Bora"). A name the player really gave a companion is matched before this
	/// (<see cref="InMessage"/>), so a companion called "Mano" still answers to "Mano, ...".</summary>
	private static bool LooksLikeName(string head, string key)
	{
		if (head.Length == 0 || !char.IsUpper(head[0]))
		{
			return false;
		}
		string[] parts = key.Split('_');
		for (int i = 0; i < parts.Length; i++)
		{
			if (CommonAddressWords.Contains(parts[i]))
			{
				return false;
			}
		}
		return !CommonAddressWords.Contains(key);
	}

	/// <summary>
	/// The name of the companion the message is addressed to, or null. <paramref name="names"/> is what the player sees
	/// on each companion; the match is returned as it appears in that list.
	/// </summary>
	internal static string InMessage(string message, IReadOnlyList<string> names)
	{
		if (string.IsNullOrEmpty(message) || names == null || names.Count == 0)
		{
			return null;
		}
		// Fase 3.12: only the names a message may really be calling - three letters at least, and never one of the
		// words people open a sentence with (see AddressPrefixes)
		List<string> usable = new List<string>(names.Count);
		for (int i = 0; i < names.Count; i++)
		{
			string nameKey = Key(names[i]);
			if (nameKey.Length < MinMessageNameLength || AddressPrefixes.Contains(nameKey) || Articles.Contains(nameKey))
			{
				continue;
			}
			usable.Add(names[i]);
		}
		if (usable.Count == 0)
		{
			return null;
		}
		// "Gruncha, colete" and "Ei, Gruncha, colete": the name is the whole first part, or the second one
		string[] segments = message.Split(SegmentSeparators, 3);
		int scanned = Math.Min(segments.Length, 2);
		for (int i = 0; i < scanned; i++)
		{
			string found = InSegment(segments[i], usable);
			if (found != null)
			{
				return found;
			}
		}
		return null;
	}

	/// <summary>Is this the name of that companion? (case and accents ignored, leading article ignored)</summary>
	internal static bool Same(string spoken, string name)
	{
		string key = Key(spoken);
		return key.Length > 0 && key == Key(name);
	}

	/// <summary>A name (or what the player wrote) as a comparison key: lowercase, no accents, no punctuation, no leading article.</summary>
	internal static string Key(string value)
	{
		string key = LlmIntent.Normalize(value ?? "");
		int split = key.IndexOf('_');
		if (split > 0 && Articles.Contains(key.Substring(0, split)))
		{
			key = key.Substring(split + 1);
		}
		return key;
	}

	private static string InSegment(string segment, IReadOnlyList<string> names)
	{
		string key = Key(segment);
		if (key.Length == 0)
		{
			return null;
		}
		// the whole part is the name: "Gruncha", "Gruncha Silva", "o Gruncha"
		string found = FindIn(names, key);
		if (found != null)
		{
			return found;
		}
		string[] words = key.Split('_');
		// "ei Gruncha, ..." / "olha Gruncha, ...": the address may open with one of those words
		int first = (words.Length > 1 && AddressPrefixes.Contains(words[0])) ? 1 : 0;
		int longest = Math.Min(MaxNameWords, words.Length - first);
		for (int size = longest; size >= 1; size--)
		{
			// "Gruncha colete 10 pedras": the name opens the message (or comes right after the address word)
			found = FindIn(names, Key(Join(words, first, size)));
			if (found != null)
			{
				return found;
			}
		}
		return null;
	}

	/// <summary>The first name of the list whose key is <paramref name="wanted"/>, or null.</summary>
	private static string FindIn(IReadOnlyList<string> names, string wanted)
	{
		if (wanted.Length == 0)
		{
			return null;
		}
		for (int i = 0; i < names.Count; i++)
		{
			if (Key(names[i]) == wanted)
			{
				return names[i];
			}
		}
		return null;
	}

	private static string Join(string[] words, int start, int count)
	{
		if (count <= 0 || start < 0 || start + count > words.Length)
		{
			return "";
		}
		StringBuilder builder = new StringBuilder(count * 8);
		for (int i = 0; i < count; i++)
		{
			if (i > 0)
			{
				builder.Append('_');
			}
			builder.Append(words[start + i]);
		}
		return builder.ToString();
	}
}
