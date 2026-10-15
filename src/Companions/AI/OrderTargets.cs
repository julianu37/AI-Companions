using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Companions;

/// <summary>
/// Fase 3.6: turns the name an order spoke ("ataca o greydwarf", "equipa o machado de bronze", "larga a pedra")
/// into the creature in the world / the item in the companion's pack it means.
///
/// The player types Portuguese while the model may answer in Portuguese or English, the game's prefabs are English
/// ("AxeBronze") and the in-game names are localized ("Machado de bronze"), so the match is deliberately forgiving:
///   1. the name written the same way (accents, case and punctuation ignored);
///   2. every word of the order found in the name, word by word, with a small PT-BR/EN dictionary and singular/
///      plural ignored ("machado de bronze" -> "Machado de bronze" and -> "AxeBronze", "bronze axe" too);
///   3. the order's name contained in the candidate (or the other way round) - the last resort.
/// Whatever is left is "the order named nothing we know", and the caller says so.
///
/// For a creature the player's own aim wins when it is the one the order named (point at it and say "ataca aquele"),
/// otherwise the closest match within the configured radius is taken. Nothing here runs per frame: it is called
/// once per chat order.
/// </summary>
internal static class OrderTargets
{
	/// <summary>How good a name match has to be to be used at all. Partial, single-word hits stay below it.</summary>
	internal const int MinScore = 450;

	/// <summary>Fase 3.6b: the bar the player's own words have to clear to be used as a name, which is higher than
	/// <see cref="MinScore"/> on purpose. A message is not a name: two things named in one sentence ("larga a pedra
	/// e a madeira") would otherwise be read as one word ("pedramadeira") and matched by mere substring, and the
	/// order would silently land on the wrong one. Only a real word-for-word match (900 and up) counts here.</summary>
	internal const int MinPhraseScore = 900;

	private const float AimRange = 60f;

	private static int _aimMask = -1;

	// ---------------------------------------------------------------- creatures

	/// <summary>The creature an order named, or the one under the player's aim when the order named none.
	/// Null when nothing matches - the caller warns the player.</summary>
	/// <param name="target">What LlmIntent normalized out of the model's "target" field, or null.</param>
	/// <param name="player">The player giving the order (the search is centered on them).</param>
	/// <param name="range">How far from the player a creature may be, in meters.</param>
	internal static Character FindCreature(string target, Player player, float range)
	{
		if (player == null)
		{
			return null;
		}
		try
		{
			// 1. the creature under the crosshair, when it is the one the order named (or the order named none)
			Character aimed = AimedCreature();
			if (aimed != null && Usable(aimed, player))
			{
				int aimScore = string.IsNullOrEmpty(target) ? 1000 : Score(target, NameOf(aimed), PrefabName(aimed));
				if (aimScore >= MinScore)
				{
					Log($"Order target \"{target}\" -> the creature under the crosshair (\"{NameOf(aimed)}\")");
					return aimed;
				}
			}
			if (string.IsNullOrEmpty(target))
			{
				return null;
			}
			// 2. the closest creature whose name matches
			Character best = ClosestByName(target, player, range, out int seen, out int bestScore, out float bestDist);
			if (best == null)
			{
				Log($"Order target \"{target}\": no match among {seen} creature(s) within {range:F0} m");
				return null;
			}
			Log($"Order target \"{target}\" -> \"{NameOf(best)}\" ({bestDist:F1} m, score {bestScore})");
			return best;
		}
		catch (Exception e)
		{
			CompanionsPlugin.Log?.LogWarning("[IA] Couldn't look for the creature an order named: " + e.Message);
			return null;
		}
	}

	/// <summary>The closest creature within <paramref name="range"/> whose name answers to <paramref name="target"/>,
	/// or null. The player, their companions, anything dead and anything the net doesn't own are never candidates.
	/// <paramref name="seen"/> is how many creatures were looked at (for the log); <paramref name="score"/> and
	/// <paramref name="distance"/> describe the winner. Never logs by itself - the callers own the wording.</summary>
	private static Character ClosestByName(string target, Player player, float range, out int seen, out int score, out float distance)
	{
		seen = 0;
		score = 0;
		distance = float.MaxValue;
		if (player == null || string.IsNullOrEmpty(target))
		{
			return null;
		}
		Vector3 me = player.transform.position;
		List<Character> all = Character.GetAllCharacters();
		if (all == null)
		{
			return null;
		}
		Character best = null;
		int bestScore = 0;
		float bestDist = float.MaxValue;
		for (int i = 0; i < all.Count; i++)
		{
			Character c = all[i];
			if (!Usable(c, player))
			{
				continue;
			}
			float dist = Vector3.Distance(me, c.transform.position);
			if (dist > range)
			{
				continue;
			}
			seen++;
			int candidate = Score(target, NameOf(c), PrefabName(c));
			if (candidate < MinScore)
			{
				continue;
			}
			if (best == null || candidate > bestScore || (candidate == bestScore && dist < bestDist))
			{
				best = c;
				bestScore = candidate;
				bestDist = dist;
			}
		}
		if (best != null)
		{
			score = bestScore;
			distance = bestDist;
		}
		return best;
	}

	/// <summary>
	/// Fase 3.6b: the creature the player's OWN words named, for when the model answered "target": null (the reported
	/// bug: "ataca o greydwarf" came back with no target, so nothing was engaged).
	///
	/// Only the name search runs here: the creature under the crosshair is the caller's own fallback, so "ataca
	/// aquele" keeps working exactly as it did in Fase 3.6. Nothing is guessed either - every phrase of the message
	/// has to answer a creature's name with a score over <see cref="MinPhraseScore"/>, so "ataca aquele" and "ataca
	/// perto de casa" name nothing and the caller falls back to the aim.
	/// </summary>
	/// <param name="phrase">The piece of the message that matched ("greydwarf"), for the log and the feedback line.</param>
	internal static Character FindCreatureByWords(string message, Player player, float range, out string phrase)
	{
		phrase = null;
		if (player == null)
		{
			return null;
		}
		try
		{
			List<string> phrases = Phrases(message);
			for (int i = 0; i < phrases.Count; i++)
			{
				Character found = ClosestByName(phrases[i], player, range, out int seen, out int score, out float dist);
				if (found == null || score < MinPhraseScore)
				{
					continue;
				}
				phrase = phrases[i];
				Log($"Order target from the player's own words: \"{phrase}\" -> \"{NameOf(found)}\" ({dist:F1} m, score {score}, {seen} creature(s) in range)");
				return found;
			}
		}
		catch (Exception e)
		{
			CompanionsPlugin.Log?.LogWarning("[IA] Couldn't look for the creature the player named: " + e.Message);
		}
		return null;
	}

	/// <summary>The creature under the player's crosshair, or null. Uses the same layers as the mod's own command ray.</summary>
	private static Character AimedCreature()
	{
		Camera camera = Utils.GetMainCamera();
		if (camera == null)
		{
			return null;
		}
		int mask = AimMask();
		if (mask == 0)
		{
			return null;
		}
		if (!Physics.Raycast(camera.transform.position, camera.transform.forward, out RaycastHit hit, AimRange, mask))
		{
			return null;
		}
		return hit.collider != null ? hit.collider.GetComponentInParent<Character>() : null;
	}

	private static int AimMask()
	{
		if (_aimMask < 0)
		{
			try
			{
				_aimMask = LayerMask.GetMask("character", "character_net", "character_ghost", "character_noenv");
			}
			catch
			{
				_aimMask = 0;
			}
		}
		return _aimMask;
	}

	/// <summary>Something an order may point at: alive, not the player and not one of the player's companions.</summary>
	private static bool Usable(Character c, Player player)
	{
		if (c == null || c == player || c.IsPlayer() || c.GetHealth() <= 0f || c.IsDead())
		{
			return false;
		}
		if (c.GetComponent<CompanionSetup>() != null)
		{
			return false;
		}
		ZNetView nv = c.GetComponent<ZNetView>();
		return nv == null || nv.IsValid();
	}

	/// <summary>The creature's in-game name, localized ("$enemy_greydwarf" -> "Greydwarf").</summary>
	internal static string NameOf(Character c)
	{
		return c != null ? Localize(c.m_name) : "?";
	}

	/// <summary>The creature's prefab name ("Greydwarf"), which never depends on the game's language.</summary>
	private static string PrefabName(Character c)
	{
		try
		{
			return HuntController.PrefabName(c);
		}
		catch
		{
			return null;
		}
	}

	// ---------------------------------------------------------------- items

	/// <summary>The item in the companion's own pack that the order named, or null. Only ever looks at the pack -
	/// never at chests - so "larga a pedra" can't empty a chest by accident.</summary>
	/// <param name="score">How good the match was (for the log), 0 when nothing was found.</param>
	internal static ItemDrop.ItemData FindItem(Humanoid humanoid, string target, out int score)
	{
		score = 0;
		if (humanoid == null || string.IsNullOrEmpty(target))
		{
			return null;
		}
		try
		{
			Inventory inventory = humanoid.GetInventory();
			if (inventory == null)
			{
				return null;
			}
			List<ItemDrop.ItemData> items = inventory.GetAllItems();
			if (items == null)
			{
				return null;
			}
			ItemDrop.ItemData best = null;
			int bestScore = 0;
			for (int i = 0; i < items.Count; i++)
			{
				ItemDrop.ItemData item = items[i];
				int candidate = Score(target, ItemNames(item));
				if (candidate < MinScore || candidate <= bestScore)
				{
					continue;
				}
				bestScore = candidate;
				best = item;
			}
			score = bestScore;
			return best;
		}
		catch (Exception e)
		{
			CompanionsPlugin.Log?.LogWarning("[IA] Couldn't look for the item an order named: " + e.Message);
			return null;
		}
	}

	/// <summary>How well an item answers to the name an order spoke (used while dropping, stack by stack).</summary>
	internal static int ScoreItem(string target, ItemDrop.ItemData item)
	{
		return Score(target, ItemNames(item));
	}

	/// <summary>Every name an item answers to: the prefab ("AxeBronze"), the localized name ("Machado de bronze").</summary>
	private static string[] ItemNames(ItemDrop.ItemData item)
	{
		if (item == null || item.m_shared == null)
		{
			return null;
		}
		string prefab = item.m_dropPrefab != null ? item.m_dropPrefab.name : null;
		string shared = item.m_shared.m_name;
		string local = Localize(shared);
		if (string.IsNullOrEmpty(prefab))
		{
			return new string[2] { shared, local };
		}
		return new string[3] { prefab, shared, local };
	}

	/// <summary>The item's in-game name, localized ("$item_axe_bronze" -> "Machado de bronze").</summary>
	internal static string NameOf(ItemDrop.ItemData item)
	{
		if (item == null || item.m_shared == null)
		{
			return "?";
		}
		return Localize(item.m_shared.m_name);
	}

	private static string Localize(string token)
	{
		if (string.IsNullOrEmpty(token))
		{
			return "?";
		}
		try
		{
			string text = Localization.instance != null ? Localization.instance.Localize(token) : token;
			return string.IsNullOrEmpty(text) ? token : text;
		}
		catch
		{
			return token;
		}
	}

	// ---------------------------------------------------------------- Fase 3.6b: the player's own words

	/// <summary>How many words of a message a name may span ("machado de bronze" is two).</summary>
	private const int MaxWindow = 3;

	/// <summary>How many phrases of each length are tried, so a long message can't turn into a long search.</summary>
	private const int MaxPerSize = 4;

	/// <summary>
	/// Fase 3.6b: the noun phrases of what the PLAYER said, most specific first, for when the model answered
	/// "target": null. "Joga essas pedras que tu pegou no chão" -> pedras; "equipa o machado de bronze" ->
	/// machado_bronze, machado, bronze; "ataca o greydwarf" -> greydwarf.
	///
	/// Only the words that carry meaning are used: the order's own verbs ("joga", "larga", "equipa", "pega"),
	/// pronouns ("essas", "aquele", "que tu pegou"), fillers ("coisa", "isso"), numbers ("10") and places
	/// ("chão", "fora") are dropped (see <see cref="Filler"/>), and so are the articles/prepositions
	/// <see cref="Score"/> already ignores. Whatever is left is combined into names of up to
	/// <see cref="MaxWindow"/> words, longest first, because "machado de bronze" must win over a bare "machado"
	/// (which would answer any axe). Nothing here decides anything by itself: every phrase still has to match a
	/// real item in a pack or a real creature in the world, so a stray word is refused by the caller.
	///
	/// Runs once per order, and only when the model named nothing - never per frame.
	/// </summary>
	internal static List<string> Phrases(string message)
	{
		List<string> phrases = new List<string>(MaxWindow * MaxPerSize + 1);
		if (string.IsNullOrEmpty(message))
		{
			return phrases;
		}
		try
		{
			string normalized = LlmIntent.Normalize(message);
			if (normalized.Length == 0)
			{
				return phrases;
			}
			string[] tokens = normalized.Split('_');
			List<string> words = new List<string>(tokens.Length);
			for (int i = 0; i < tokens.Length; i++)
			{
				if (Meaningful(tokens[i]))
				{
					words.Add(tokens[i]);
				}
			}
			if (words.Count == 0)
			{
				return phrases;
			}
			// everything the player said that means something, as one name ("picareta de ferro" -> picareta_ferro)
			if (words.Count > MaxWindow)
			{
				AddPhrase(phrases, Join(words, 0, words.Count));
			}
			for (int size = Math.Min(MaxWindow, words.Count); size >= 1; size--)
			{
				int taken = 0;
				for (int start = 0; start + size <= words.Count && taken < MaxPerSize; start++)
				{
					if (AddPhrase(phrases, Join(words, start, size)))
					{
						taken++;
					}
				}
			}
			return phrases;
		}
		catch (Exception e)
		{
			CompanionsPlugin.Log?.LogDebug("[IA] Couldn't read the player's own words: " + e.Message);
			return phrases;
		}
	}

	/// <summary>Is this word of a message a possible name? Numbers, the order's own verbs, pronouns, fillers and
	/// places are not (they are what made "joga 10 pedras no chão" unreadable as a name).</summary>
	private static bool Meaningful(string word)
	{
		if (string.IsNullOrEmpty(word) || word.Length < 3)
		{
			return false;
		}
		if (Stopwords.Contains(word) || Filler.Contains(word) || Filler.Contains(Stem(word)))
		{
			return false;
		}
		for (int i = 0; i < word.Length; i++)
		{
			if (!char.IsDigit(word[i]))
			{
				return true;
			}
		}
		return false;   // all digits: that is the amount, never a name
	}

	private static string Join(List<string> words, int start, int count)
	{
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

	/// <summary>Keeps the phrase when it is new and there is room; true when it was kept.</summary>
	private static bool AddPhrase(List<string> phrases, string phrase)
	{
		if (string.IsNullOrEmpty(phrase) || phrases.Count >= MaxWindow * MaxPerSize + 1 || phrases.Contains(phrase))
		{
			return false;
		}
		phrases.Add(phrase);
		return true;
	}

	// ---------------------------------------------------------------- matching

	/// <summary>How well one name answers to another. 0 = no match at all. Never throws.</summary>
	internal static int Score(string target, params string[] candidates)
	{
		if (string.IsNullOrEmpty(target) || candidates == null)
		{
			return 0;
		}
		try
		{
			string t = LlmIntent.Normalize(target);
			if (t.Length == 0)
			{
				return 0;
			}
			string tFlat = t.Replace("_", "");
			HashSet<string> targetWords = WordSet(target);
			string targetFirst = LeadingWord(target);
			int best = 0;
			for (int i = 0; i < candidates.Length; i++)
			{
				string candidate = candidates[i];
				if (string.IsNullOrEmpty(candidate))
				{
					continue;
				}
				string c = LlmIntent.Normalize(candidate);
				if (c.Length == 0)
				{
					continue;
				}
				int score;
				if (c == t)
				{
					score = 1000;
				}
				else
				{
					string cFlat = c.Replace("_", "");
					if (cFlat == tFlat)
					{
						score = 990;
					}
					else
					{
						HashSet<string> candidateWords = WordSet(candidate);
						if (targetWords.Count > 0 && candidateWords.Count > 0 && Covers(targetWords, candidateWords, cFlat))
						{
							int extra = Mathf.Max(0, candidateWords.Count - targetWords.Count);
							score = Mathf.Max(MinScore, 900 - extra * 25);
							// the noun usually comes first in both languages: "ferro" is IronScrap, not "Picareta de ferro"
							if (targetFirst != null && Same(targetFirst, LeadingWord(candidate)))
							{
								score += 60;
							}
						}
						else if (tFlat.Length >= 4 && cFlat.Contains(tFlat))
						{
							score = 700;
						}
						else if (cFlat.Length >= 4 && tFlat.Contains(cFlat))
						{
							score = 650;
						}
						else
						{
							score = 0;
						}
					}
				}
				if (score > best)
				{
					best = score;
				}
			}
			return best;
		}
		catch (Exception e)
		{
			CompanionsPlugin.Log?.LogDebug("[IA] Name match failed: " + e.Message);
			return 0;
		}
	}

	/// <summary>Every word of the order's name appears in the candidate's (directly, as a synonym or inside it).</summary>
	private static bool Covers(HashSet<string> targetWords, HashSet<string> candidateWords, string candidateFlat)
	{
		foreach (string word in targetWords)
		{
			if (candidateWords.Contains(word))
			{
				continue;
			}
			bool found = false;
			List<string> alternatives = Alternatives(word);
			for (int i = 0; i < alternatives.Count; i++)
			{
				if (candidateWords.Contains(alternatives[i]))
				{
					found = true;
					break;
				}
			}
			if (!found && word.Length >= 4 && candidateFlat.Contains(word))
			{
				found = true;
			}
			if (!found)
			{
				return false;
			}
		}
		return true;
	}

	private static bool Same(string a, string b)
	{
		if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
		{
			return false;
		}
		if (a == b)
		{
			return true;
		}
		List<string> alternatives = Alternatives(a);
		for (int i = 0; i < alternatives.Count; i++)
		{
			if (alternatives[i] == b)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>The word a name starts with ("Machado de bronze" -> machado, "AxeBronze" -> axe, "$item_axe_bronze"
	/// -> axe), used to prefer the item the order actually named over one that merely contains the word.</summary>
	private static string LeadingWord(string name)
	{
		if (string.IsNullOrEmpty(name))
		{
			return null;
		}
		foreach (string part in CamelWords(name))
		{
			foreach (string word in LlmIntent.Normalize(part).Split('_'))
			{
				if (word.Length == 0 || Stopwords.Contains(word))
				{
					continue;
				}
				return Stem(word);
			}
		}
		return null;
	}

	/// <summary>The meaningful words of a name: "Machado de bronze" -> machado, bronze; "AxeBronze" -> axe, bronze.</summary>
	private static HashSet<string> WordSet(string name)
	{
		HashSet<string> set = new HashSet<string>(StringComparer.Ordinal);
		if (string.IsNullOrEmpty(name))
		{
			return set;
		}
		AddWords(set, LlmIntent.Normalize(name));
		// prefab names are written together and in camelCase ("BlackMetalScrap" -> black, metal, scrap)
		foreach (string part in CamelWords(name))
		{
			AddWords(set, LlmIntent.Normalize(part));
		}
		return set;
	}

	private static void AddWords(HashSet<string> set, string normalized)
	{
		if (string.IsNullOrEmpty(normalized))
		{
			return;
		}
		foreach (string word in normalized.Split('_'))
		{
			AddWord(set, word);
		}
	}

	private static void AddWord(HashSet<string> set, string word)
	{
		if (string.IsNullOrEmpty(word) || Stopwords.Contains(word))
		{
			return;
		}
		set.Add(word);
		string stem = Stem(word);
		if (stem != word)
		{
			set.Add(stem);
		}
	}

	/// <summary>Split a prefab name on its capitals: "AxeBronze" -> Axe, Bronze. Keeps the separators as words too.</summary>
	private static List<string> CamelWords(string name)
	{
		List<string> words = new List<string>(4);
		if (string.IsNullOrEmpty(name))
		{
			return words;
		}
		StringBuilder current = new StringBuilder(16);
		for (int i = 0; i < name.Length; i++)
		{
			char c = name[i];
			if (char.IsUpper(c) && current.Length > 0)
			{
				words.Add(current.ToString());
				current.Length = 0;
			}
			current.Append(c);
		}
		if (current.Length > 0)
		{
			words.Add(current.ToString());
		}
		return words;
	}

	/// <summary>singular/plural, the cheap way: "pedras" and "pedra" are the same word to us.</summary>
	private static string Stem(string word)
	{
		return (word.Length > 3 && word[word.Length - 1] == 's') ? word.Substring(0, word.Length - 1) : word;
	}

	/// <summary>The word itself, its stem and every word of its PT-BR/EN group.</summary>
	private static List<string> Alternatives(string word)
	{
		List<string> list = new List<string>(4);
		AddAlternative(list, word);
		AddAlternative(list, Stem(word));
		if (Synonyms.TryGetValue(word, out List<string> group))
		{
			for (int i = 0; i < group.Count; i++)
			{
				AddAlternative(list, group[i]);
			}
		}
		return list;
	}

	private static void AddAlternative(List<string> list, string word)
	{
		if (string.IsNullOrEmpty(word))
		{
			return;
		}
		if (!list.Contains(word))
		{
			list.Add(word);
		}
		string stem = Stem(word);
		if (stem != word && !list.Contains(stem))
		{
			list.Add(stem);
		}
	}

	private static void Log(string message)
	{
		CompanionsPlugin.Log?.LogInfo("[IA] " + message);
	}

	// ---------------------------------------------------------------- word lists

	private static readonly HashSet<string> Stopwords = new HashSet<string>(StringComparer.Ordinal)
	{
		"o", "a", "os", "as", "um", "uma", "de", "da", "do", "das", "dos", "em", "no", "na", "nos", "nas",
		"e", "com", "para", "por", "que", "meu", "minha", "seu", "sua", "the", "of", "an", "and", "with", "for",
		"item", "enemy", "animal", "piece", "se", "tier", "level"
	};

	/// <summary>
	/// Fase 3.6b: the words of a message that are NEVER the name of an item or a creature, so the fallback that reads
	/// the player's own words can't take one of them for one. They are the order's own verbs ("joga", "larga",
	/// "equipa", "pega", "ataca", in the forms a player actually types), pronouns and demonstratives ("essas",
	/// "aquele", "que tu pegou"), fillers ("coisa", "lixo", "isso"), numbers written out ("dez") and the places and
	/// directions an order talks about ("chão", "fora", "mão", "perto"). Digits are dropped separately.
	///
	/// Kept deliberately conservative: a word that could name something ("pedra", "madeira", "machado", "troll") is
	/// not here. And even a word that slips through cannot invent an item - every phrase still has to answer a real
	/// item in a companion's pack or a real creature in the world (see Phrases and its callers).
	/// </summary>
	private static readonly HashSet<string> Filler = new HashSet<string>(StringComparer.Ordinal)
	{
		// the order's own verbs - dropping/leaving ("joga 10 pedras no chão")
		"drop", "dropar", "dropa", "drope", "dropei", "dropou", "dropando", "larga", "largar", "largue", "larguem",
		"largou", "largando", "joga", "jogar", "jogue", "jogou", "jogam", "jogando", "solta", "soltar", "solte",
		"soltem", "soltou", "soltando", "deixa", "deixar", "deixe", "deixem", "deixou", "deixando", "descarta",
		"descartar", "descarte", "descartou", "dispensa", "dispensar", "dispense", "dispensou", "remove", "remover",
		"remova", "removeu", "tira", "tirar", "tire", "tirou", "livrar", "livra", "livre", "livrou", "limpa",
		"limpar", "limpe", "limpou", "manda", "mandar", "mande", "mandou", "arremessa", "arremessar", "arremesse",
		// taking/holding ("pega a picareta", "equipa o machado")
		"pega", "pegar", "pegue", "peguem", "pegou", "pegam", "pegando", "busca", "buscar", "busque", "buscou",
		"traz", "traga", "trazer", "trouxe", "trazendo", "apanha", "apanhar", "apanhe", "apanhou", "recolhe",
		"recolher", "recolha", "recolheu", "cata", "catar", "cate", "catou", "junta", "juntar", "junte", "juntou",
		"coleta", "coletar", "colete", "coletem", "coletou", "coletando", "equipa", "equipar", "equipe", "equipem",
		"equipou", "equipando", "empunha", "empunhar", "empunhe", "empunhou", "veste", "vestir", "vista", "vestiu",
		"calca", "calcar", "calce", "calcou", "poe", "poem", "coloca", "colocar", "coloque", "colocou", "colocando",
		"usa", "usar", "use", "usou", "usando", "segura", "segurar", "segure", "guarda", "guardar", "guarde",
		// attacking ("ataca o greydwarf")
		"ataca", "atacar", "ataque", "ataquem", "atacou", "atacam", "atacando", "mata", "matar", "mate", "matem",
		"matou", "matam", "matando", "abate", "abater", "abateu", "elimina", "eliminar", "elimine", "eliminou",
		"destruir", "destroi", "destruiu", "engaja", "engajar", "engaje", "engajou", "caca", "cacar", "cace",
		"cacou", "persegue", "perseguir", "persiga",
		// English, for a player who mixes the two
		"kill", "attack", "take", "equip", "gather", "collect", "wear", "hold", "throw", "away", "out", "that",
		"those", "these", "this", "them", "please", "just", "now", "all", "some", "any", "there", "give",
		// pronouns, demonstratives and fillers ("essas pedras que tu pegou")
		"essas", "esse", "essa", "esses", "isso", "isto", "este", "esta", "estes", "estas", "aquele", "aquela",
		"aquilo", "aqueles", "aquelas", "tu", "voce", "voces", "vcs", "eu", "ele", "ela", "eles", "elas", "mim",
		"comigo", "te", "ti", "lhe", "lhes", "nosso", "nossa", "dele", "dela", "mesmo", "mesma", "tal", "tais",
		"coisa", "coisas", "treco", "troco", "negocio", "bagulho", "trem", "lixo", "sobra", "sobrando", "sobrar",
		"sobraram", "extra", "tudo", "toda", "todo", "todas", "todos", "nenhum", "nenhuma", "algum", "alguma",
		"alguns", "algumas", "outro", "outra", "outros", "outras", "mais", "menos", "metade", "pouco", "pouca",
		"muito", "muita", "muitos", "muitas", "apenas", "somente", "ainda", "tambem", "agora", "depois", "entao",
		"rapido", "devagar",
		// places and directions the order talks about
		"chao", "mao", "fora", "dentro", "perto", "longe", "aqui", "ali", "acola", "terra", "baixo", "cima",
		"frente", "tras", "lado", "pra", "pro", "ao", "aos", "pelo", "pela", "num", "numa", "sobre", "sem", "ate",
		// numbers written out and units
		"dois", "duas", "tres", "quatro", "cinco", "seis", "sete", "oito", "nove", "dez", "onze", "doze", "vinte",
		"trinta", "quarenta", "cinquenta", "cem", "cento", "mil", "duzia", "unidade", "unidades", "peca", "pecas",
		"pedaco", "pedacos", "pilha", "pilhas", "monte", "montes", "stack", "stacks", "bocado", "bocados"
	};

	/// <summary>Words that mean the same thing in the two languages the model mixes (PT-BR and the game's English).
	/// Built once; each word points at every other word of its group, so the lookup is a plain dictionary hit.</summary>
	private static readonly Dictionary<string, List<string>> Synonyms = BuildSynonyms();

	private static Dictionary<string, List<string>> BuildSynonyms()
	{
		Dictionary<string, List<string>> map = new Dictionary<string, List<string>>(StringComparer.Ordinal);
		AddGroup(map, "axe", "machado");
		AddGroup(map, "pickaxe", "picareta");
		AddGroup(map, "sword", "espada");
		AddGroup(map, "knife", "faca", "dagger", "adaga");
		AddGroup(map, "spear", "lanca", "harpoon", "arpao");
		AddGroup(map, "bow", "arco");
		AddGroup(map, "crossbow", "besta");
		AddGroup(map, "arrow", "flecha");
		AddGroup(map, "bolt", "virote");
		AddGroup(map, "shield", "escudo", "buckler", "broquel");
		AddGroup(map, "helmet", "elmo", "capacete", "cap");
		AddGroup(map, "armor", "armadura");
		AddGroup(map, "chest", "peitoral", "torso");
		AddGroup(map, "legs", "pernas", "calcas", "perneiras");
		AddGroup(map, "cape", "capa", "cloak", "manto");
		AddGroup(map, "torch", "tocha");
		AddGroup(map, "staff", "cajado", "vara");
		AddGroup(map, "hammer", "martelo");
		AddGroup(map, "mace", "maca", "club", "porrete", "clava");
		AddGroup(map, "sledge", "marreta");
		AddGroup(map, "atgeir", "atgedir");
		AddGroup(map, "fishingrod", "varadepesca");
		AddGroup(map, "wood", "madeira", "lenha", "log", "tora", "wooden");
		AddGroup(map, "stone", "pedra", "rocha", "rock");
		AddGroup(map, "iron", "ferro");
		AddGroup(map, "copper", "cobre");
		AddGroup(map, "silver", "prata");
		AddGroup(map, "tin", "estanho");
		AddGroup(map, "bronze", "bronze");
		AddGroup(map, "black", "negro", "negra", "preta", "preto");
		AddGroup(map, "metal", "metal");
		AddGroup(map, "scrap", "sucata");
		AddGroup(map, "resin", "resina");
		AddGroup(map, "hide", "couro", "pele", "leather");
		AddGroup(map, "feather", "pena", "pluma");
		AddGroup(map, "meat", "carne");
		AddGroup(map, "crystal", "cristal");
		AddGroup(map, "flint", "silex");
		AddGroup(map, "obsidian", "obsidiana");
		AddGroup(map, "antler", "chifre", "corno");
		AddGroup(map, "fine", "fina", "fino");
		AddGroup(map, "greydwarf", "anao", "anoes");
		AddGroup(map, "greyling", "anao");
		AddGroup(map, "troll", "troll");
		AddGroup(map, "skeleton", "esqueleto");
		AddGroup(map, "draugr", "draugr");
		AddGroup(map, "wolf", "lobo");
		AddGroup(map, "bear", "urso");
		AddGroup(map, "boar", "javali", "porco");
		AddGroup(map, "deer", "cervo", "veado");
		AddGroup(map, "hare", "lebre", "coelho");
		AddGroup(map, "chicken", "galinha");
		AddGroup(map, "serpent", "serpente", "cobra");
		AddGroup(map, "dragon", "dragao");
		AddGroup(map, "ghost", "fantasma");
		AddGroup(map, "goblin", "fuling", "duende");
		AddGroup(map, "mosquito", "pernilongo");
		AddGroup(map, "root", "raiz");
		AddGroup(map, "fish", "peixe");
		AddGroup(map, "neck", "neck");
		AddGroup(map, "lox", "lox");
		return map;
	}

	/// <summary>Every word of a group answers to every other word of it ("machado" -> axe, and "axe" -> machado).</summary>
	private static void AddGroup(Dictionary<string, List<string>> map, params string[] words)
	{
		List<string> group = new List<string>(words.Length);
		for (int i = 0; i < words.Length; i++)
		{
			string word = words[i];
			if (!string.IsNullOrEmpty(word))
			{
				group.Add(word);
			}
		}
		for (int i = 0; i < group.Count; i++)
		{
			string word = group[i];
			List<string> others;
			if (!map.TryGetValue(word, out others))
			{
				others = new List<string>(group.Count);
				map[word] = others;
			}
			for (int j = 0; j < group.Count; j++)
			{
				string other = group[j];
				if (other != word && !others.Contains(other))
				{
					others.Add(other);
				}
			}
			string stem = Stem(word);
			if (stem != word && !map.ContainsKey(stem))
			{
				map[stem] = others;
			}
		}
	}
}
