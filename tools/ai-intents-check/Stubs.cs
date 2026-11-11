using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration;
using UnityEngine;

namespace Companions
{
	// ================================================================================================================
	// The compile shims of tools/ai-intents-check.
	//
	// The files listed in ai-intents-check.csproj are the REAL sources: the word nets (RadialIntent, PickupIntent,
	// GatherIntent, InteractionIntent), the parser (LlmIntent), the item map (OrderItems), the name rules
	// (CompanionNameMatch, inside CompanionNames.cs) and the list reader (MHR_List). They are pure, and they are
	// compiled here exactly as they are compiled into Companions.dll.
	//
	// What is here is the FEW members of the Unity/config side they touch, and nothing else:
	//   * CompanionsPlugin.Log   - the BepInEx logger (only LogDebug/LogInfo are called by the linked files);
	//   * ModConfig              - the four gather lists, with the values the .cfg ships with (ModConfig.cs -> the
	//                              "Gathering" Bind calls);
	//   * CompanionSetup         - the work-mode constants (CompanionSetup.cs -> ModeFollow..ModeCook) and the name/owner
	//                              ZDO keys;
	//   * CompanionLocale        - only Current.KokoroCode (CompanionNames.PromptLine and BuildTemplates.Template.Label);
	//   * MHR_GatherRules        - only Parse/ItemLocal, the two members OrderItems reads (see the note there);
	//   * CompanionOrders        - only JobMode and IsDoorRequest, the two pure members the nets call. Both are verbatim
	//                              copies (see the note on CompanionOrders below) and the harness CHECKS them against the
	//                              built Companions.dll (Program.CrossCheckDoorRule), so they cannot drift silently.
	//
	// 11/11/2026: the citations here name the MEMBER, not a line range. A "file.cs:812-869" is a promise about a file
	// somebody else edits - six of them had already drifted (the gather lists were cited as 928-931, which is the
	// [Combat] section; the lists live at 1020-1023). A method name survives an edit above it.
	//
	// Nothing in this file decides what an order means: every rule the tests assert lives in src/.
	// ================================================================================================================

	/// <summary>The BepInEx logger, reduced to the calls the linked sources make. Counts the debug lines so a test
	/// could assert that a net logged nothing on the happy path (it does not - see Program).</summary>
	internal sealed class HarnessLog
	{
		internal int DebugLines;
		internal int InfoLines;
		internal readonly List<string> Last = new List<string>();

		internal void LogDebug(string message) { DebugLines++; Remember(message); }
		internal void LogInfo(string message) { InfoLines++; Remember(message); }
		internal void LogWarning(string message) { Remember(message); }
		internal void LogError(string message) { Remember(message); }
		internal void LogMessage(string message) { Remember(message); }

		private void Remember(string message)
		{
			Last.Add(message);
			if (Last.Count > 20)
			{
				Last.RemoveAt(0);
			}
		}
	}

	/// <summary>Core/CompanionsPlugin.cs is a BepInEx plugin (BaseUnityPlugin): only its static Log is read.</summary>
	internal static class CompanionsPlugin
	{
		internal static HarnessLog Log = new HarnessLog();
	}

	/// <summary>
	/// Core/ModConfig.cs: the four gather lists. The values are the ones the file ships with (the same text the
	/// player's AiCompanions.cfg has on a fresh install), bound to a ConfigFile that is never written to disk - the
	/// harness must not touch the player's configuration. Setting .Value in a test is allowed: it only changes this
	/// in-memory copy (SaveOnConfigSet is off).
	/// </summary>
	internal static class ModConfig
	{
		internal static ConfigEntry<string> GatherWood;
		internal static ConfigEntry<string> GatherStone;
		internal static ConfigEntry<string> GatherOre;
		internal static ConfigEntry<string> ForageItems;

		static ModConfig()
		{
			// saveOnInit: false and SaveOnConfigSet: false -> the file is never created, read or written.
			ConfigFile file = new ConfigFile(
				Path.Combine(Path.GetTempPath(), "ai-intents-check-" + Guid.NewGuid().ToString("N") + ".cfg"), false);
			file.SaveOnConfigSet = false;
			GatherWood = file.Bind("Gathering", "GatherWood",
				"Wood, FineWood, RoundLog, ElderBark, YggdrasilWood, Blackwood, Resin");
			GatherStone = file.Bind("Gathering", "GatherStone", "Stone, Flint, BlackMarble, Grausten");
			GatherOre = file.Bind("Gathering", "GatherOre",
				"CopperOre, TinOre, IronScrap, SilverOre, BlackMetalScrap, CopperScrap, FlametalOreNew, Obsidian");
			ForageItems = file.Bind("Gathering", "ForageItems",
				"Raspberry,Blueberries,Cloudberry,Mushroom,MushroomYellow,MushroomBlue,MushroomMagecap,MushroomJotunPuffs,"
				+ "MushroomSmokePuff,Thistle,Dandelion,Carrot,Turnip,Onion,Barley,Flax,RoyalJelly,Honey");
		}
	}

	/// <summary>
	/// Companion/CompanionSetup.cs, reduced to what the linked files read: the work-mode constants (the real values,
	/// <c>CompanionSetup.cs</c> → <c>ModeFollow</c> … <c>ModeCook</c>), the two ZDO keys and the roster. It derives from
	/// MonoBehaviour because the real one does (MHR_Owner.IsMine takes a Component). The harness never instantiates it:
	/// the Unity half of CompanionNames needs a live world and is out of scope (see the README).
	/// </summary>
	internal sealed class CompanionSetup : MonoBehaviour
	{
		internal const int ModeFollow = 0;
		internal const int ModeGatherWood = 1;
		internal const int ModeGatherStone = 2;
		internal const int ModeGatherOre = 3;
		internal const int ModeStay = 4;
		internal const int ModeForage = 5;
		internal const int ModeSmelt = 6;
		internal const int ModeHunt = 7;
		internal const int ModeFarm = 8;
		internal const int ModeFish = 9;
		internal const int ModeRepairBuildings = 15;
		internal const int ModeRestock = 16;
		internal const int ModeCook = 17;

		/// <summary>The ZDO keys of the name and the owner (real: CompanionSetup.NameHash / OwnerHash). Only their
		/// type matters here - nothing in this harness reaches a ZDO.</summary>
		internal static readonly int NameHash = "HC_Name".GetHashCode();
		internal static readonly int OwnerHash = "HC_Owner".GetHashCode();

		internal static IReadOnlyList<CompanionSetup> AllCompanions = new List<CompanionSetup>();

#pragma warning disable 0649 // the real field is written by the spawn code; here it only has to exist
		internal bool _mhrStale;
#pragma warning restore 0649
	}

	/// <summary>Core/CompanionLocale.cs, reduced to the one property CompanionNames.PromptLine reads.
	/// <para><c>KokoroCode</c> is writable on purpose (11/11/2026): the per-language texts of the build templates
	/// (<c>BuildTemplates.Template.Label</c>) are picked by it, so a test sets it to each of the five languages and checks
	/// the name that comes out - the same switch <c>CompanionPersona.Pick</c> uses in the mod. Nothing else writes it, and
	/// it starts at the real default (pt-br).</para></summary>
	internal sealed class CompanionLocale
	{
		internal string KokoroCode = "pt-br";

		internal static readonly CompanionLocale Current = new CompanionLocale();
	}

	/// <summary>
	/// Jobs/MHR_GatherRules.cs, reduced to the two members OrderItems reads. Both are faithful outside the game:
	/// <list type="bullet">
	/// <item><c>Parse(mode)</c> is <c>ListFor(mode)</c> + <c>MHR_List.Get</c> (<c>MHR_GatherRules.cs</c> → <c>Parse</c>). The
	/// real one also calls <c>CheckKnown</c>, which only logs a warning about a misspelt name when <c>ObjectDB.instance</c>
	/// exists - it is a no-op here. Note the real <c>ListFor</c> answers null for any mode but 1/2/3 (there is no
	/// forage list in it), and this stub keeps that.</item>
	/// <item><c>ItemLocal(prefab)</c> is the ObjectDB lookup (<c>MHR_GatherRules.cs</c> → <c>ItemLocal</c>): with no world
	/// there is no item prefab, so it answers null - exactly what the real one answers outside the game.</item>
	/// </list>
	/// </summary>
	internal static class MHR_GatherRules
	{
		internal static MHR_List Parse(int mode)
		{
			string raw = ListFor(mode);
			return raw == null ? null : MHR_List.Get(raw);
		}

		internal static string ItemLocal(string prefab)
		{
			// The real one walks ObjectDB.instance (null outside the game) -> null. Never a fabricated name.
			return null;
		}

		private static string ListFor(int mode)
		{
			switch (mode)
			{
			case 1: return ModConfig.GatherWood != null ? ModConfig.GatherWood.Value : null;
			case 2: return ModConfig.GatherStone != null ? ModConfig.GatherStone.Value : null;
			case 3: return ModConfig.GatherOre != null ? ModConfig.GatherOre.Value : null;
			default: return null;
			}
		}
	}

	/// <summary>
	/// AI/CompanionOrders.cs, reduced to the two PURE members the nets call. Everything else in that file needs the
	/// world (companions, ZDO, inventory), so it cannot be linked here.
	///
	/// <para><b>IsDoorRequest</b> is a verbatim copy of <c>CompanionOrders.cs</c> → <c>IsDoorRequest</c> (with the
	/// <c>PackWords</c> set it shares with <c>IsInventoryRequest</c>). Because a copy can drift, Program.CrossCheckDoorRule
	/// loads the built Companions.dll and compares this copy with the real method on a corpus - the harness fails if
	/// they ever disagree.</para>
	///
	/// <para><b>JobMode</b> is a verbatim copy of <c>CompanionOrders.cs</c> → <c>JobMode</c>.</para>
	/// </summary>
	internal static class CompanionOrders
	{
		/// <summary>The work mode behind an order, or -1 when the action isn't a job (<c>CompanionOrders.cs</c> → <c>JobMode</c>).</summary>
		internal static int JobMode(string action)
		{
			switch (action)
			{
			case "wood":
				return CompanionSetup.ModeGatherWood;
			case "stone":
				return CompanionSetup.ModeGatherStone;
			case "ore":
				return CompanionSetup.ModeGatherOre;
			case "forage":
				return CompanionSetup.ModeForage;
			case "smelt":
				return CompanionSetup.ModeSmelt;
			case "hunt":
				return CompanionSetup.ModeHunt;
			case "farm":
				return CompanionSetup.ModeFarm;
			case "fish":
				return CompanionSetup.ModeFish;
			case "repair":
				return CompanionSetup.ModeRepairBuildings;
			case "cook":
				return CompanionSetup.ModeCook;
			default:
				return -1;
			}
		}

		/// <summary>Does the player's own message ask for a DOOR? (copy of <c>CompanionOrders.cs</c> → <c>IsDoorRequest</c>)</summary>
		internal static bool IsDoorRequest(string message)
		{
			if (string.IsNullOrEmpty(message) || message.IndexOf('?') >= 0)
			{
				return false;
			}
			string text = LlmIntent.Normalize(message);
			if (text.Length == 0)
			{
				return false;
			}
			bool door = false;
			bool verb = false;
			bool pack = false;
			string[] words = text.Split('_');
			for (int i = 0; i < words.Length; i++)
			{
				string word = words[i];
				if (DoorWords.Contains(word))
				{
					door = true;
					continue;
				}
				if (PackWords.Contains(word))
				{
					pack = true;
					continue;
				}
				if (DoorVerbs.Contains(word))
				{
					verb = true;
				}
			}
			return door && verb && !pack;
		}

		/// <summary>copy of <c>CompanionOrders.cs</c> → <c>PackWords</c></summary>
		private static readonly HashSet<string> PackWords = new HashSet<string>(StringComparer.Ordinal)
		{
			"inventario", "inventarios", "invent", "inventory", "mochila", "bolsa", "bag", "carrega", "carregas"
		};

		/// <summary>copy of <c>CompanionOrders.cs</c> → <c>DoorWords</c></summary>
		private static readonly HashSet<string> DoorWords = new HashSet<string>(StringComparer.Ordinal)
		{
			"porta", "portas", "portao", "portoes", "puerta", "puertas", "porte", "portes", "door", "doors"
		};

		/// <summary>copy of <c>CompanionOrders.cs</c> → <c>DoorVerbs</c></summary>
		private static readonly HashSet<string> DoorVerbs = new HashSet<string>(StringComparer.Ordinal)
		{
			// pt
			"abre", "abrir", "abra", "abram", "abrem", "fecha", "fechar", "feche", "fechem", "fecham",
			// en
			"open", "opens", "close", "closes", "shut",
			// es
			"cierra", "cerrar", "cierre", "cierren",
			// fr
			"ouvre", "ouvrir", "ouvrez", "ferme", "fermer", "fermez",
			// it
			"apri", "aprire", "apra", "chiudi", "chiudere", "chiuda"
		};
	}
}

// ================================================================================================================
// The game's item database (assembly_valheim), reduced to the one member OrderItems.Known() reads.
//
// It is in the GLOBAL namespace on purpose - that is where the game's type lives - so the linked OrderItems.cs
// (namespace Companions) binds to this one. The real type cannot be used here at all: it derives from
// UnityEngine.ScriptableObject, and touching it runs the type initializer of UnityEngine.Object, which throws
// without the engine (TypeInitializationException) - so the real Known() would answer "not a known item" for
// everything and Resolve() would return null.
//
// The null instance is exactly the case the real code is written for: OrderItems.Known says "when the object
// database isn't up yet there is nothing to check against, so the map is trusted". What the harness therefore does
// NOT cover is "is this prefab an item the game really has" (that needs the game - see the README).
// ================================================================================================================
internal sealed class ObjectDB
{
#pragma warning disable 0649 // always null here: there is no world, and that is the case OrderItems.Known trusts
	internal static ObjectDB instance;
#pragma warning restore 0649

	internal object GetItemPrefab(string name)
	{
		return null;
	}
}
