using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Companions;

// 1.5.41: speech overhaul, phase 1. The ambient lines a companion says now and then, one pool per situation, loaded
// from speech.json (or Translations/speech/<language>.json). A line can start with tags in square brackets that say
// when it fits, e.g. "[home] A fine hall you've built." or "[night,!rain] The stars are out." - see CompanionTalk.Fits.
// Tags: home, away, night, day, rain, dry, cold, indoors, outdoors, and the biomes meadows, blackforest, swamp,
// mountain, plains, mistlands, ashlands, deepnorth, ocean. "!" in front means "not". A line without tags always fits.
[Serializable]
public class SpeechConfig
{
	// 2 = the 1.5.41 layout, 3 = 1.5.43 (travelling lines tagged [away], more lines). An older file is kept as
	// speech.v<its version>.json and replaced by the new defaults.
	public int Version;

	public string[] Action;

	public string[] Wood;

	public string[] Stone;

	public string[] Ore;

	// only used when Wood/Stone/Ore are empty
	public string[] Gather;

	public string[] Forage;

	public string[] Combat;

	public string[] Follow;

	public string[] Hungry;

	public string[] Repair;

	public string[] Overweight;

	public string[] Smelt;

	public string[] Idle;

	public string[] Hunt;

	public string[] Farm;

	public string[] Fish;

	public string[] Cook;

	public string[] Upkeep;

	public string[] NoBed;

	// idle chat when it goes over to you or another companion
	public string[] Chat;

	public const int CurrentVersion = 3;

	private static SpeechConfig _instance;

	/// <summary>1.8.3: the language the current lines were read for, so <see cref="ReloadForLanguage"/> can tell a real
	/// language change from the extra <c>SetupLanguage</c> calls the game makes (startup, world load).</summary>
	/// <remarks>Multilanguage M1: it only holds a language whose <b>file really loaded</b>. It used to be set before
	/// <see cref="TryLoadFrom"/> answered, so a missing/corrupt translation left the mod on the fallback (or on the
	/// built-in lines) for the rest of the session, with no second attempt. Now a failed attempt keeps it empty and
	/// the lazy check in <see cref="Instance"/> tries again - through <see cref="_lastAttemptUtc"/>, never on every
	/// access.</remarks>
	private static string _loadedLanguage;

	/// <summary>Multilanguage M1: the lines in memory came from a fallback (the generic speech.json or the built-in
	/// defaults), not from the language's own file.</summary>
	private static bool _usingFallback;

	/// <summary>Multilanguage M1: how long a failed attempt is remembered before the disk is read again.</summary>
	private const double RetrySeconds = 30.0;

	/// <summary>Multilanguage M1: when the last load attempt ran (UTC) - the gate of the retry.</summary>
	private static DateTime _lastAttemptUtc = DateTime.MinValue;

	private static bool RetryDue()
	{
		return (DateTime.UtcNow - _lastAttemptUtc).TotalSeconds >= RetrySeconds;
	}

	public static SpeechConfig Instance
	{
		get
		{
			if (_instance == null)
			{
				Load();
				return _instance;
			}
			// 1.8.3: the spoken lines follow the game language, but this check can NOT live in the
			// Localization.SetupLanguage postfix: reading GetSelectedLanguage() there touches PlatformPrefs, which
			// initializes the platform and calls SetupLanguage again (endless recursion on the loading screen). Here
			// it is lazy and safe - by the time the companion talks, the platform is up.
			if (!_checkingLanguage)
			{
				_checkingLanguage = true;
				try
				{
					string selected = (Localization.instance != null) ? Localization.instance.GetSelectedLanguage() : null;
					// Multilanguage M1: a language whose file was not found stays "not loaded", so this is where the
					// retry happens - at most once every RetrySeconds, never on every access
					if (!string.IsNullOrEmpty(selected) && !string.Equals(selected, _loadedLanguage, StringComparison.Ordinal) && RetryDue())
					{
						Load();
					}
				}
				catch
				{
				}
				finally
				{
					_checkingLanguage = false;
				}
			}
			return _instance;
		}
	}

	/// <summary>1.8.3: re-entrancy guard for the lazy language check in <see cref="Instance"/>.</summary>
	private static bool _checkingLanguage;

	public static void Load()
	{
		string directoryName = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
		string text = "English";
		try
		{
			if (Localization.instance != null)
			{
				text = Localization.instance.GetSelectedLanguage();
			}
		}
		catch
		{
		}
		string path = Path.Combine(directoryName, "Speech", text + ".json");
		// Multilanguage M1: the attempt is remembered from here on, so a failure is retried after RetrySeconds and
		// not on every access (Instance). _loadedLanguage is only set when the language's OWN file loaded.
		_lastAttemptUtc = DateTime.UtcNow;
		if (TryLoadFrom(path))
		{
			_loadedLanguage = text;
			_usingFallback = false;
			return;
		}
		// the lines in memory are NOT this language's: forget the language, so a change back to it reloads
		_loadedLanguage = null;
		string text2 = Path.Combine(directoryName, "speech.json");
		if (_usingFallback)
		{
			// the fallback is already what is in memory and the language file has just been looked for: reading the
			// same file again would only log the same line every RetrySeconds
			return;
		}
		if (TryLoadFrom(text2))
		{
			_usingFallback = true;
			return;
		}
		_instance = Defaults();
		_usingFallback = true;
		try
		{
			string contents = JsonUtility.ToJson(_instance, prettyPrint: true);
			File.WriteAllText(text2, contents);
			CompanionsPlugin.Log.LogInfo("[Speech] Created default " + text2);
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log.LogWarning("[Speech] Failed to write defaults to " + text2 + ": " + ex.Message);
		}
	}

	public static void Reload()
	{
		// Multilanguage M1: a forced reload must really read the files again, so the "came from a fallback" mark and
		// the language are dropped with the instance
		_instance = null;
		_loadedLanguage = null;
		_usingFallback = false;
		Load();
	}

	/// <summary>1.8.3: called from the <c>Localization.SetupLanguage</c> postfix, so the companion's spoken lines follow
	/// the game language. Only a real change reloads the file - <c>SetupLanguage</c> also runs at startup and on every
	/// world load, and those calls would otherwise read the same JSON again for nothing.</summary>
	/// <remarks>Multilanguage M1: unlike the lazy check in <see cref="Instance"/>, this one is the notification itself -
	/// a different language means a real change, so it reloads right away (no retry gate).</remarks>
	public static void ReloadForLanguage(string language)
	{
		if (string.Equals(language, _loadedLanguage, StringComparison.Ordinal))
		{
			return;
		}
		Reload();
	}

	private static bool TryLoadFrom(string path)
	{
		if (!File.Exists(path))
		{
			return false;
		}
		try
		{
			string json = File.ReadAllText(path);
			SpeechConfig speechConfig = JsonUtility.FromJson<SpeechConfig>(json);
			// 1.8.3: a partial file used to come back with null arrays, and the first line of that category threw a
			// NullReferenceException (the wheel reads Action.Length). A translation is often written in pieces, so
			// every category the file leaves out keeps the built-in lines instead of taking the speech down.
			_instance = Merge(speechConfig, path);
			CompanionsPlugin.Log.LogInfo("[Speech] Loaded " + path);
			return true;
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log.LogWarning("[Speech] Failed to parse " + path + ": " + ex.Message);
			return false;
		}
	}

	/// <summary>1.8.3: fills in the categories the file doesn't have (null OR empty array - an empty list would show
	/// nothing at all) from <see cref="Defaults"/>, and says in the log which ones were missing.</summary>
	/// <remarks>1.9.2 (upstream 1.5.5, the speech overhaul): the pools are found by <b>reflection</b> over the public
	/// <c>string[]</c> fields instead of a hand-written list. The list stayed at the old 15 categories after
	/// Wood/Stone/Ore/Upkeep/Chat were added, so those pools came back <b>empty</b> from every language file:
	/// Wood/Stone/Ore silently fell back to <c>Gather</c> in <c>CompanionTalk.Or</c> and Upkeep/Chat said nothing at
	/// all. <c>Gather</c> is the one exception - it is the fallback of Wood/Stone/Ore, so it is never filled in from
	/// itself.</remarks>
	private static SpeechConfig Merge(SpeechConfig loaded, string path)
	{
		SpeechConfig speechConfig = Defaults();
		if (loaded == null)
		{
			CompanionsPlugin.Log.LogWarning("[Speech] " + Path.GetFileName(path) + " has no content - using the built-in lines");
			return speechConfig;
		}
		List<string> missing = new List<string>();
		FieldInfo[] fields = typeof(SpeechConfig).GetFields(BindingFlags.Public | BindingFlags.Instance);
		for (int i = 0; i < fields.Length; i++)
		{
			FieldInfo field = fields[i];
			if (field.FieldType != typeof(string[]) || field.Name == "Gather")
			{
				continue;
			}
			string[] pool = field.GetValue(loaded) as string[];
			if (pool != null && pool.Length > 0)
			{
				continue;
			}
			field.SetValue(loaded, field.GetValue(speechConfig));
			missing.Add(field.Name);
		}
		if (missing.Count > 0)
		{
			CompanionsPlugin.Log.LogInfo("[Speech] " + Path.GetFileName(path) + " is missing " + missing.Count + " categor(y/ies) - using the built-in lines: " + string.Join(", ", missing.ToArray()));
		}
		return loaded;
	}

	private static SpeechConfig Defaults()
	{
		SpeechConfig s = new SpeechConfig();
		s.Version = CurrentVersion;
		// said when you give it an order from the command wheel
		s.Action = new string[]
		{
			"By your word!", "So it shall be.", "Aye, consider it done!", "I heed your call.", "As you command.", "On it!", "Right away.", "It shall be done.",
			"Say no more.", "Leave it to me."
		};
		// while actually chopping (Wood job)
		s.Wood = new string[]
		{
			"Good timber here.", "Another tree falls!", "My arms grow stronger with each swing.", "Timber!", "This wood will burn well.",
			"[blackforest] The pines grow thick here.", "[meadows] Beech and birch, good and straight.", "[swamp] Ancient bark... tough going."
		};
		// while actually mining stone (Stone job)
		s.Stone = new string[]
		{
			"There's good stone over here.", "Stone for the walls.", "Chip by chip.", "This rock won't move itself.", "Solid stone, this."
		};
		// while actually mining ore (Ore job)
		s.Ore = new string[]
		{
			"This ore looks promising.", "The veins run deep in this rock.", "The ground gives up its treasures.", "Ore for the smelter.",
			"[blackforest] Copper and tin, our first real metal.", "[swamp] Iron, buried with the dead.", "[mountain] Silver, cold as the peaks."
		};
		s.Gather = new string[] { "The land provides, if you know where to look.", "A fine haul so far." };
		// while actually foraging
		s.Forage = new string[]
		{
			"The wild provides for those who seek.", "Ripe for the picking.", "Nature hides her bounty well.", "These will make a fine stew.",
			"[meadows] The meadows offer their gifts.", "[blackforest] Good pickings under these trees.", "[night] Hard to spot anything in this dark."
		};
		// only in a real fight
		s.Combat = new string[]
		{
			"For Odin!", "Taste my steel!", "Stand and fight!", "They shall not pass!", "By Thor's hammer!", "You picked the wrong fight!", "I'll send you to Hel!",
			"Come then! I am ready!", "The Valkyries watch!"
		};
		// while following you
		s.Follow = new string[]
		{
			// 1.5.43: travelling lines only away from home (1.5.42 test: "What lies ahead, I wonder?" while sitting on the throne)
			"[away] Lead on, I am your shield.", "[away] Where the path takes us, I follow.", "Right behind you.", "I've got your back.", "[away] What lies ahead, I wonder?",
			"[away] Which way now?", "[away] Keep your eyes open.", "[away] Good country for an ambush.", "[home] Anything need doing around here?", "[home] Just say the word.",
			"[day,!rain] A fine day to wander the wilds.", "[night] Keep close, the night hides much.", "[rain] Wet boots again.", "[cold] My beard is freezing.",
			"[blackforest] Watch for greydwarfs in these woods.", "[swamp] This bog stinks of death.", "[mountain] Mind the drop.", "[plains] Watch for the fuling camps.",
			"[mistlands] I can barely see my own hand.", "[ashlands] Even the air burns here.", "[deepnorth] Cold enough to freeze a troll.", "[ocean] Nothing but water and sky."
		};
		// no food in it
		s.Hungry = new string[]
		{
			"My belly roars like a troll...", "I'd trade my axe for a leg of boar right now.", "Got anything to eat?", "A warrior fights on mead and meat, not empty guts.",
			"I could eat an entire neck right now."
		};
		// its gear is badly worn
		s.Repair = new string[]
		{
			"This blade has seen better days.", "My gear is wearing thin.", "I should find a workbench soon.", "A few more hits and this breaks."
		};
		s.Overweight = new string[] { "My back is breaking under all this!", "I can barely move with all this weight!", "My pack is fit to burst!", "I need to put some of this down." };
		// while actually working the smelters
		s.Smelt = new string[]
		{
			"Feeding the furnace.", "Good ore makes good metal.", "Keeping the fires stoked.", "Another bar for the hoard.", "The bellows sing their song.", "Hot work, this.",
			"[cold] At least the furnace is warm."
		};
		// nothing to do; [home] lines only at home
		s.Idle = new string[]
		{
			"Skål!", "I could use a horn of mead.", "A moment of peace... rare in these lands.", "The world is quiet for now.", "Waiting for your command.",
			"[home] Nice place we've got here.", "[home] A fine hall you've built.", "[home] I could get used to this.", "[home,night] Good to be home for the night.",
			"[away] Strange place to stop.", "[away,night] I'd rather not linger out here after dark.", "[rain] At least the rain keeps the dust down.",
			"[day,!rain,!indoors] Just enjoying the view.", "[indoors] Cosy in here."
		};
		// while actually hunting
		s.Hunt = new string[]
		{
			"Stay quiet...", "The hunt is on!", "Fresh meat for the pot.", "Tracking prey through the brush.", "Easy now... nice and steady...", "Downwind, always downwind.",
			"[night] Hard to track in the dark.", "[rain] The rain hides our steps."
		};
		// while actually farming
		s.Farm = new string[] { "Working the fields.", "Seeds in the ground, hope in the heart.", "Farm work is honest work.", "Another row done.", "[rain] Good weather for the crops." };
		// while actually fishing
		s.Fish = new string[]
		{
			"Casting my line...", "Patience is a fisherman's greatest virtue.", "Come on, take the bait...", "Fishing is peaceful work.", "Nothing yet... but I'll wait.",
			"[rain] Fish bite better in the rain.", "[night] Quiet water tonight.", "[ocean] Something big is down there."
		};
		// while actually cooking or brewing
		s.Cook = new string[] { "A good meal lifts any warrior's spirits.", "Stirring the pot...", "A pinch of this, a dash of that.", "Food for the weary!", "Mmm, smells good." };
		// while actually doing Repair and Restock
		s.Upkeep = new string[]
		{
			"Keeping the place in shape.", "A sound roof and a warm fire.", "Everything in its place.", "A home needs looking after.", "Who left this here?",
			"[rain] Let's hope the roof holds.", "[night] Keep the fires burning."
		};
		// it has no bed (only said at night)
		s.NoBed = new string[] { "I've got no bed to call my own.", "Wish I had a bed around here.", "I'll sleep on the ground again, I suppose.", "A place to rest my head would be nice." };
		// idle chat next to you or another companion
		s.Chat = new string[]
		{
			"Need anything?", "Anything need doing?", "Quiet around here.", "[day,!rain] Fine day for it.", "[rain] Smells like rain. Oh wait.", "[night] Late, isn't it?",
			"[home] Good to have a roof over us.", "[away] Should we head home soon?"
		};
		return s;
	}
}
