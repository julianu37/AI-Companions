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
		// the same name on two companions: the first one wins (the same rule as Find), and the tie is logged
		int matches = 0;
		CompanionSetup found = null;
		for (int i = 0; i < mine.Count; i++)
		{
			if (!string.Equals(names[i], matched, StringComparison.Ordinal))
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

	/// <summary>Where the addressed part of a message ends: "Gruncha, colete 10 pedras" / "Ei, Gruncha, ...".</summary>
	private static readonly char[] SegmentSeparators = new char[3] { ',', ';', ':' };

	/// <summary>The articles a name may be preceded by, in the model's field or in the player's words ("o Gruncha").</summary>
	private static readonly HashSet<string> Articles = new HashSet<string>(StringComparer.Ordinal)
	{
		"o", "a", "os", "as", "the"
	};

	/// <summary>
	/// The words an address may open with before the name ("ei Gruncha, ...", "olha Gruncha, ..."). One of them is
	/// skipped before the name is looked for, so the very common "ei, Gruncha" works with or without a comma - and
	/// only one, so a full sentence never becomes an address by accident.
	/// </summary>
	private static readonly HashSet<string> AddressPrefixes = new HashSet<string>(StringComparer.Ordinal)
	{
		"ei", "oi", "ola", "hey", "ah", "oh", "psiu", "escuta", "olha", "entao", "opa", "alo"
	};

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
		// "Gruncha, colete" and "Ei, Gruncha, colete": the name is the whole first part, or the second one
		string[] segments = message.Split(SegmentSeparators, 3);
		int scanned = Math.Min(segments.Length, 2);
		for (int i = 0; i < scanned; i++)
		{
			string found = InSegment(segments[i], names);
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
