using System;
using System.Collections.Generic;
using UnityEngine;

namespace Companions;

/// <summary>
/// Fase 3.2c: the resource an order named ("colete 10 madeiras") as an item prefab the game knows ("Wood"), so
/// the gather jobs can count it in the companion's own pack and stop at the number asked for.
/// The system prompt asks the model for the resource in English, but the player types Portuguese, so both are
/// mapped; anything the map misses is looked up against the in-game names of the job's own list ("casca de
/// ancião" -> ElderBark). Whatever is left is "the order named no item" - the number then caps the whole job,
/// exactly as it did before this phase.
/// Only ever called when a chat order arrives, never per frame, and it never touches the world.
/// </summary>
internal static class OrderItems
{
	/// <summary>The item prefab the order asked for, or null when it can't be pinned to one item the game knows.</summary>
	/// <param name="action">The canonical action ("wood", "stone", "ore"...).</param>
	/// <param name="resource">What LlmIntent normalized out of the model's "resource" field, or null.</param>
	/// <param name="mode">The work mode the order turned into (its list is the last place to look for a name).</param>
	internal static string Resolve(string action, string resource, int mode)
	{
		try
		{
			string key = LlmIntent.Normalize(resource);
			if (key.Length > 0)
			{
				string prefab;
				if (Aliases.TryGetValue(key, out prefab) && Known(prefab))
				{
					return prefab;
				}
				// the in-game names of the job's own list: any item of it can be named in Portuguese
				string local = MatchList(key, mode);
				if (local != null)
				{
					return local;
				}
				return null;   // named something we don't know: the job's own list decides (the 3.2 behaviour)
			}
			// no resource named at all ("corte 10 madeiras" -> action wood): the action is the resource
			string byAction = ActionItem(action);
			return (byAction != null && Known(byAction)) ? byAction : null;
		}
		catch (Exception e)
		{
			CompanionsPlugin.Log?.LogDebug("[IA] Couldn't resolve the item an order named: " + e.Message);
			return null;
		}
	}

	/// <summary>The item an action alone stands for. "ore" is deliberately absent: it is copper, iron, silver and
	/// tin at once, so an order that only says "minério" keeps the Fase 3.2 behaviour (the whole job).</summary>
	private static string ActionItem(string action)
	{
		switch (action)
		{
		case "wood":
			return "Wood";
		case "stone":
			return "Stone";
		default:
			return null;
		}
	}

	/// <summary>Looks for the named resource among the in-game names of the job's list ("pedra" -> Stone,
	/// "casca de ancião" -> ElderBark). Only the list's named entries are walked (a "*" list names none).</summary>
	private static string MatchList(string key, int mode)
	{
		MHR_List list = MHR_GatherRules.Parse(mode);
		if (list == null || list.Order.Count == 0)
		{
			return null;
		}
		for (int i = 0; i < list.Order.Count; i++)
		{
			string name = list.Order[i];
			if (Same(name, key) || Same(MHR_GatherRules.ItemLocal(name), key) || Same(MHR_List.Local(name), key))
			{
				return name;
			}
		}
		return null;
	}

	/// <summary>Is this prefab an item the game knows? When the object database isn't up yet there is nothing to
	/// check against, so the map is trusted.</summary>
	private static bool Known(string prefab)
	{
		try
		{
			return ObjectDB.instance == null || ObjectDB.instance.GetItemPrefab(prefab) != null;
		}
		catch
		{
			return false;
		}
	}

	private static bool Same(string a, string b)
	{
		if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
		{
			return false;
		}
		return LlmIntent.Normalize(a).Replace("_", "") == b.Replace("_", "");
	}

	/// <summary>What the model may answer in "resource" -> the item prefab. English (what the prompt asks for) and
	/// Portuguese (what the player types). Every entry is checked with ObjectDB before it is used, so a name that
	/// doesn't exist in the game simply falls through to the list lookup.</summary>
	private static readonly Dictionary<string, string> Aliases = BuildAliases();

	private static Dictionary<string, string> BuildAliases()
	{
		Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.Ordinal);
		Add(map, "Wood", "wood", "madeira", "lenha", "madeiras", "arvore", "arvores", "tree", "trees", "chop", "cortar", "tronco");
		Add(map, "FineWood", "finewood", "fine_wood", "madeira_fina");
		Add(map, "RoundLog", "roundlog", "round_log", "tora", "toras");
		Add(map, "ElderBark", "elderbark", "elder_bark", "casca", "casca_de_ancian", "casca_de_anciao");
		Add(map, "YggdrasilWood", "yggdrasilwood", "yggdrasil_wood", "madeira_de_yggdrasil", "yggdrasil");
		Add(map, "Blackwood", "blackwood", "black_wood", "madeira_negra");
		Add(map, "Resin", "resin", "resina");
		Add(map, "Stone", "stone", "pedra", "pedras", "rocha", "rochas", "rock", "rocks");
		Add(map, "Flint", "flint", "silex", "pedra_de_fogo");
		Add(map, "BlackMarble", "blackmarble", "black_marble", "marmore", "marmore_negro");
		Add(map, "Grausten", "grausten");
		Add(map, "Obsidian", "obsidian", "obsidiana");
		Add(map, "CopperOre", "copperore", "copper_ore", "copper", "cobre", "minerio_de_cobre");
		Add(map, "TinOre", "tinore", "tin_ore", "tin", "estanho", "minerio_de_estanho");
		Add(map, "IronScrap", "ironscrap", "iron_scrap", "iron", "ferro", "sucata", "scrap", "minerio_de_ferro");
		Add(map, "SilverOre", "silverore", "silver_ore", "silver", "prata", "minerio_de_prata");
		Add(map, "BlackMetalScrap", "blackmetalscrap", "black_metal_scrap", "blackmetal", "metal_negro", "sucata_negra");
		Add(map, "CopperScrap", "copperscrap", "copper_scrap", "sucata_de_cobre");
		Add(map, "FlametalOreNew", "flametalorenew", "flametal_ore", "flametal");
		Add(map, "Crystal", "crystal", "cristal");
		return map;
	}

	private static void Add(Dictionary<string, string> map, string prefab, params string[] names)
	{
		for (int i = 0; i < names.Length; i++)
		{
			map[LlmIntent.Normalize(names[i])] = prefab;
		}
	}
}
