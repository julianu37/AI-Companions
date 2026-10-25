using System;
using System.Collections.Generic;
using BepInEx.Configuration;
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

	/// <summary>Fase 3.1 (correção do teste em jogo): the gather action the order's "resource" points at, or null when
	/// there is nothing to correct. A small model mixes the two fields up ("colete 10 pedras" came back as action
	/// "wood" with resource "stone", and the mod obeyed the generic action, chopping wood while counting stone), and
	/// the resource is the specific field - the one already resolved into the item the job counts - so when the two
	/// disagree about WHICH job it is, the resource wins. Only the gather actions are looked at, and only when the
	/// resource is one of another gather job's own items: attack/equip/drop/inventory, a null/unknown resource and an
	/// already coherent pair are left exactly as they were.</summary>
	/// <param name="action">The canonical action the model answered ("wood", "stone", "ore", "forage"...).</param>
	/// <param name="resource">What the model answered in "resource", or null.</param>
	internal static string GatherAction(string action, string resource)
	{
		try
		{
			int mode = CompanionOrders.JobMode(action);
			if (!IsGather(mode))
			{
				return null;   // not one of the four gathering jobs: nothing here has an opinion about it
			}
			string key = LlmIntent.Normalize(resource);
			if (key.Length == 0)
			{
				return null;   // no resource named: everything works as before
			}
			if (OnListOf(mode, key))
			{
				return null;   // coherent: the resource is one of the job's own items
			}
			string byResource = ActionOf(ModeOf(key));   // some other gathering job the resource belongs to
			return byResource != null && byResource != action ? byResource : null;
		}
		catch (Exception e)
		{
			CompanionsPlugin.Log?.LogDebug("[IA] Couldn't tell which job the order's resource belongs to: " + e.Message);
			return null;
		}
	}

	/// <summary>The four orders that turn into a gathering job (the modes, so the caller's own action -> mode table
	/// stays the single source of that mapping).</summary>
	private static bool IsGather(int mode)
	{
		return mode == CompanionSetup.ModeGatherWood || mode == CompanionSetup.ModeGatherStone
			|| mode == CompanionSetup.ModeGatherOre || mode == CompanionSetup.ModeForage;
	}

	/// <summary>The gather action a mode stands for.</summary>
	private static string ActionOf(int mode)
	{
		switch (mode)
		{
		case CompanionSetup.ModeGatherWood:
			return "wood";
		case CompanionSetup.ModeGatherStone:
			return "stone";
		case CompanionSetup.ModeGatherOre:
			return "ore";
		case CompanionSetup.ModeForage:
			return "forage";
		default:
			return null;
		}
	}

	/// <summary>The gathering job a named resource belongs to, as its mode, or -1 when it can't be told. The name is
	/// first put through the same alias map Resolve uses ("pedra" and "stone" both become "Stone"), then looked for
	/// among the named entries of the four gather lists the jobs themselves read (GatherWood/GatherStone/GatherOre/
	/// ForageItems) - the item -> job mapping is the player's own configuration, so there is no second table here to
	/// keep in sync. A list that says "*" names no item and tells us nothing.</summary>
	private static int ModeOf(string key)
	{
		string prefab;
		if (Aliases.TryGetValue(key, out prefab) && prefab != null)
		{
			int byPrefab = ModeOfName(prefab);
			if (byPrefab >= 0)
			{
				return byPrefab;
			}
		}
		// the lists may name it themselves: a forage item ("raspberry", "cogumelo"), a custom entry of the player's
		return ModeOfName(key);
	}

	/// <summary>Which of the four gather lists names this item, as a mode, or -1 when none does.</summary>
	private static int ModeOfName(string name)
	{
		string key = LlmIntent.Normalize(name);
		if (key.Length == 0)
		{
			return -1;
		}
		for (int i = 0; i < GatherModes.Length; i++)
		{
			if (OnListOf(GatherModes[i], key))
			{
				return GatherModes[i];
			}
		}
		return -1;
	}

	/// <summary>Is this item one of the named entries of that job's list? By prefab name or by the in-game name the
	/// player sees ("cogumelo" -> Mushroom), exactly how the lists match names themselves.</summary>
	private static bool OnListOf(int mode, string key)
	{
		switch (mode)
		{
		case CompanionSetup.ModeGatherWood:
			return OnList(ModConfig.GatherWood, key);
		case CompanionSetup.ModeGatherStone:
			return OnList(ModConfig.GatherStone, key);
		case CompanionSetup.ModeGatherOre:
			return OnList(ModConfig.GatherOre, key);
		case CompanionSetup.ModeForage:
			return OnList(ModConfig.ForageItems, key);
		default:
			return false;
		}
	}

	/// <summary>Walks the named entries of a list ("*" and the "-Name" ones are not named entries and never match).</summary>
	private static bool OnList(ConfigEntry<string> entry, string key)
	{
		if (entry == null)
		{
			return false;
		}
		MHR_List list = MHR_List.Get(entry.Value);
		for (int i = 0; i < list.Order.Count; i++)
		{
			string name = list.Order[i];
			if (Same(name, key) || Same(MHR_GatherRules.ItemLocal(name), key))
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>The four gathering jobs, in the order a resource is looked for among their lists.</summary>
	private static readonly int[] GatherModes = new int[]
	{
		CompanionSetup.ModeGatherWood,
		CompanionSetup.ModeGatherStone,
		CompanionSetup.ModeGatherOre,
		CompanionSetup.ModeForage
	};

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

	/// <summary>What the model may answer in "resource" -> the item prefab. English (what the prompt asks for),
	/// Portuguese, Spanish and French (what the player types - Multilanguage M3). Every entry is checked with ObjectDB
	/// before it is used, so a name that doesn't exist in the game simply falls through to the list lookup.</summary>
	private static readonly Dictionary<string, string> Aliases = BuildAliases();

	private static Dictionary<string, string> BuildAliases()
	{
		Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.Ordinal);
		Add(map, "Wood", "wood", "madeira", "lenha", "madeiras", "arvore", "arvores", "tree", "trees", "chop", "cortar", "tronco",
			"madera", "maderas", "arbol", "arboles", "bois", "buche", "buches", "arbre", "arbres", "rondin", "rondins");
		Add(map, "FineWood", "finewood", "fine_wood", "madeira_fina", "madera_fina", "bois_fin");
		Add(map, "RoundLog", "roundlog", "round_log", "tora", "toras", "tronco", "troncos", "bille", "billes");
		Add(map, "ElderBark", "elderbark", "elder_bark", "casca", "casca_de_ancian", "casca_de_anciao", "corteza", "corteza_de_anciano", "ecorce", "ecorce_d_ancien");
		Add(map, "YggdrasilWood", "yggdrasilwood", "yggdrasil_wood", "madeira_de_yggdrasil", "yggdrasil", "madera_de_yggdrasil", "bois_d_yggdrasil");
		Add(map, "Blackwood", "blackwood", "black_wood", "madeira_negra", "madera_negra", "bois_noir");
		Add(map, "Resin", "resin", "resina", "resine");
		Add(map, "Stone", "stone", "pedra", "pedras", "rocha", "rochas", "rock", "rocks",
			"piedra", "piedras", "roca", "rocas", "pierre", "pierres", "rocher", "rochers", "caillou", "cailloux");
		Add(map, "Flint", "flint", "silex", "pedra_de_fogo", "pedernal", "piedra_de_fuego", "silex");
		Add(map, "BlackMarble", "blackmarble", "black_marble", "marmore", "marmore_negro", "marmol", "marmol_negro", "marbre", "marbre_noir");
		Add(map, "Grausten", "grausten");
		Add(map, "Obsidian", "obsidian", "obsidiana", "obsidienne");
		Add(map, "CopperOre", "copperore", "copper_ore", "copper", "cobre", "minerio_de_cobre", "mineral_de_cobre", "minerai_de_cuivre", "cuivre");
		Add(map, "TinOre", "tinore", "tin_ore", "tin", "estanho", "minerio_de_estanho", "mineral_de_estano", "etain", "minerai_d_etain");
		Add(map, "IronScrap", "ironscrap", "iron_scrap", "iron", "ferro", "sucata", "scrap", "minerio_de_ferro",
			"hierro", "chatarra", "mineral_de_hierro", "fer", "ferraille", "minerai_de_fer");
		Add(map, "SilverOre", "silverore", "silver_ore", "silver", "prata", "minerio_de_prata", "plata", "mineral_de_plata", "argent", "minerai_d_argent");
		Add(map, "BlackMetalScrap", "blackmetalscrap", "black_metal_scrap", "blackmetal", "metal_negro", "sucata_negra",
			"metal_negro_chatarra", "metal_noir", "ferraille_de_metal_noir");
		Add(map, "CopperScrap", "copperscrap", "copper_scrap", "sucata_de_cobre", "chatarra_de_cobre", "ferraille_de_cuivre");
		Add(map, "FlametalOreNew", "flametalorenew", "flametal_ore", "flametal");
		Add(map, "Crystal", "crystal", "cristal", "cristallo");
		// Multilanguage M3: the forage items the four languages name (the "forrageio" order says "colhe framboesas")
		Add(map, "Raspberry", "raspberry", "raspberries", "framboesa", "framboesas", "frambuesa", "frambuesas", "framboise", "framboises");
		Add(map, "Blueberries", "blueberry", "blueberries", "mirtilo", "mirtilos", "arandano", "arandanos", "myrtille", "myrtilles");
		Add(map, "Mushroom", "mushroom", "mushrooms", "cogumelo", "cogumelos", "champinon", "champinones", "seta", "setas", "champignon", "champignons");
		Add(map, "Raspberry", "mora", "moras", "mure", "mures", "amora", "amoras");
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
