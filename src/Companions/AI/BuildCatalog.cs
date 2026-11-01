using System;
using System.Collections.Generic;
using System.Text;

namespace Companions;

/// <summary>
/// Fase E3: the <b>recipe resolver</b> - the name the player said ("bancada de trabalho", "workbench", "établi")
/// to the prefab the game knows (<c>piece_workbench</c>). It is <b>pure</b> (no Unity, no world, no config), so the
/// offline harness can link this file and test every alias - the same design as <see cref="OrderItems"/> and
/// <see cref="PickupIntent"/>.
///
/// <para>The table is also the <b>allow-list</b>: a piece that is not here is not built, even if the model names it.
/// The four entries are the workshop (E5 of the plan); structures (E6) and blueprints (E7/E8) do not come through
/// this table.</para>
///
/// <para>Matching is by whole word over a folded text (lower case, accents removed, anything that is not a letter or
/// a digit becomes a space), and the <b>longest</b> alias wins - "bancada de trabalho" beats "bancada" - so a
/// sentence with two pieces resolves to the more specific one.</para>
/// </summary>
internal static class BuildCatalog
{
	internal sealed class Entry
	{
		internal readonly string Prefab;
		internal readonly string[] Aliases;
		/// <summary>How the piece is called in the log when the game's own name is not available yet.</summary>
		internal readonly string Label;

		internal Entry(string prefab, string label, params string[] aliases)
		{
			Prefab = prefab;
			Label = label;
			Aliases = aliases;
		}
	}

	// The workshop (E5). Aliases in the five languages of the mod plus the English modding names; the game's own
	// names are tokens ($piece_workbench) and are resolved by Localization, not here.
	private static readonly Entry[] Entries =
	{
		new Entry("piece_workbench", "bancada de trabalho",
			"bancada", "bancada de trabalho", "mesa de trabalho", "oficina",
			"workbench", "crafting table",
			"banco de trabajo", "mesa de trabajo",
			"etabli", "table de travail",
			"banco da lavoro"),
		new Entry("piece_chest_wood", "bau",
			"bau", "bau de madeira", "bauzinho",
			"chest", "wooden chest",
			"cofre", "cofre de madera",
			"coffre", "coffre en bois",
			"cassa", "bau di legno"),
		new Entry("fire_pit", "fogueira",
			"fogueira", "fogueira simples", "fogo",
			"campfire", "fire pit", "firepit",
			"fogata", "hoguera",
			"feu de camp", "feu",
			"falo", "falò"),
		new Entry("piece_bed02", "cama",
			"cama", "cama de dragao", "cama de drago",
			"bed", "dragon bed",
			"cama de dragon",
			"lit", "lit de dragon",
			"letto")
	};

	/// <summary>Every prefab this phase may build (the allow-list).</summary>
	internal static string[] Prefabs()
	{
		string[] prefabs = new string[Entries.Length];
		for (int i = 0; i < Entries.Length; i++)
		{
			prefabs[i] = Entries[i].Prefab;
		}
		return prefabs;
	}

	/// <summary>Is this prefab buildable by the order? (the allow-list gate; the recipe gate is
	/// <c>Player.IsRecipeKnown</c>, checked by the job).</summary>
	internal static bool Allowed(string prefab)
	{
		if (string.IsNullOrEmpty(prefab))
		{
			return false;
		}
		for (int i = 0; i < Entries.Length; i++)
		{
			if (string.Equals(Entries[i].Prefab, prefab, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>The prefab the text names, or null. Longest alias wins; whole words only.</summary>
	internal static string Resolve(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return null;
		}
		string haystack = " " + Fold(text) + " ";
		string best = null;
		int bestLength = 0;
		foreach (Entry entry in Entries)
		{
			foreach (string alias in entry.Aliases)
			{
				string needle = " " + Fold(alias) + " ";
				if (needle.Length > bestLength && haystack.IndexOf(needle, StringComparison.Ordinal) >= 0)
				{
					best = entry.Prefab;
					bestLength = needle.Length;
				}
			}
		}
		return best;
	}

	/// <summary>The human label for a prefab (log/refusal text), or the prefab itself when unknown.</summary>
	internal static string Label(string prefab)
	{
		for (int i = 0; i < Entries.Length; i++)
		{
			if (string.Equals(Entries[i].Prefab, prefab, StringComparison.OrdinalIgnoreCase))
			{
				return Entries[i].Label;
			}
		}
		return prefab ?? string.Empty;
	}

	/// <summary>
	/// Lower case, accents removed, everything that is not a letter or a digit turned into a single space. It is what
	/// makes "baú", "BAU" and "bau" the same word and "bancada-de-trabalho" two words.
	/// </summary>
	internal static string Fold(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return string.Empty;
		}
		StringBuilder sb = new StringBuilder(text.Length);
		bool space = true;
		foreach (char raw in text)
		{
			char c = char.ToLowerInvariant(raw);
			switch (c)
			{
				case 'á': case 'à': case 'â': case 'ã': case 'ä': c = 'a'; break;
				case 'é': case 'è': case 'ê': case 'ë': c = 'e'; break;
				case 'í': case 'ì': case 'î': case 'ï': c = 'i'; break;
				case 'ó': case 'ò': case 'ô': case 'õ': case 'ö': c = 'o'; break;
				case 'ú': case 'ù': case 'û': case 'ü': c = 'u'; break;
				case 'ç': c = 'c'; break;
				case 'ñ': c = 'n'; break;
			}
			if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
			{
				sb.Append(c);
				space = false;
			}
			else if (!space)
			{
				sb.Append(' ');
				space = true;
			}
		}
		return sb.ToString().Trim();
	}
}
