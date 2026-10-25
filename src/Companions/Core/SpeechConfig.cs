using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Companions;

[Serializable]
public class SpeechConfig
{
	public string[] Action;

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

	public string[] NoBed;

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
	private static SpeechConfig Merge(SpeechConfig loaded, string path)
	{
		SpeechConfig speechConfig = Defaults();
		if (loaded == null)
		{
			CompanionsPlugin.Log.LogWarning("[Speech] " + Path.GetFileName(path) + " has no content - using the built-in lines");
			return speechConfig;
		}
		List<string> missing = new List<string>();
		loaded.Action = Pick(loaded.Action, speechConfig.Action, "Action", missing);
		loaded.Gather = Pick(loaded.Gather, speechConfig.Gather, "Gather", missing);
		loaded.Forage = Pick(loaded.Forage, speechConfig.Forage, "Forage", missing);
		loaded.Combat = Pick(loaded.Combat, speechConfig.Combat, "Combat", missing);
		loaded.Follow = Pick(loaded.Follow, speechConfig.Follow, "Follow", missing);
		loaded.Hungry = Pick(loaded.Hungry, speechConfig.Hungry, "Hungry", missing);
		loaded.Repair = Pick(loaded.Repair, speechConfig.Repair, "Repair", missing);
		loaded.Overweight = Pick(loaded.Overweight, speechConfig.Overweight, "Overweight", missing);
		loaded.Smelt = Pick(loaded.Smelt, speechConfig.Smelt, "Smelt", missing);
		loaded.Idle = Pick(loaded.Idle, speechConfig.Idle, "Idle", missing);
		loaded.Hunt = Pick(loaded.Hunt, speechConfig.Hunt, "Hunt", missing);
		loaded.Farm = Pick(loaded.Farm, speechConfig.Farm, "Farm", missing);
		loaded.Fish = Pick(loaded.Fish, speechConfig.Fish, "Fish", missing);
		loaded.Cook = Pick(loaded.Cook, speechConfig.Cook, "Cook", missing);
		loaded.NoBed = Pick(loaded.NoBed, speechConfig.NoBed, "NoBed", missing);
		if (missing.Count > 0)
		{
			CompanionsPlugin.Log.LogInfo("[Speech] " + Path.GetFileName(path) + " is missing " + missing.Count + " categor(y/ies) - using the built-in lines: " + string.Join(", ", missing.ToArray()));
		}
		return loaded;
	}

	private static string[] Pick(string[] value, string[] fallback, string name, List<string> missing)
	{
		if (value != null && value.Length > 0)
		{
			return value;
		}
		missing.Add(name);
		return fallback;
	}

	private static SpeechConfig Defaults()
	{
		SpeechConfig speechConfig = new SpeechConfig();
		speechConfig.Action = new string[13]
		{
			"By your word!", "So it shall be.", "The gods guide my hand.", "Aye, consider it done!", "I heed your call.", "As you command.", "On it!", "Right away.", "You have my word.", "It shall be done.",
			"Say no more.", "I understand.", "Leave it to me."
		};
		speechConfig.Gather = new string[14]
		{
			"The land provides, if you know where to look.", "Good timber here.", "Odin's bounty is plentiful.", "I'll strip this land bare if I must.", "These arms were made for more than swinging axes... but it'll do.", "The earth yields its riches.", "There's good stone over here.", "Another tree falls!", "The veins run deep in this rock.", "My arms grow stronger with each swing.",
			"This ore looks promising.", "Plenty of materials nearby.", "A fine haul so far.", "The ground gives up its treasures."
		};
		speechConfig.Forage = new string[12]
		{
			"The meadows offer their gifts.", "Freya's garden blooms well here.", "Even warriors must forage.", "Ripe for the picking.", "The wild provides for those who seek.", "Fresh berries here!", "Mushrooms, growing in the shade.", "Nature hides her bounty well.", "A good harvest from the wilds.", "The forest floor is rich with life.",
			"These will make a fine stew.", "Plenty to gather in these parts."
		};
		speechConfig.Combat = new string[17]
		{
			"For Odin!", "Taste my steel!", "To Valhalla!", "Stand and fight!", "Skål! Come meet your end!", "They shall not pass!", "By Thor's hammer!", "You picked the wrong fight!", "Face me, coward!", "I'll send you to Hel!",
			"My blade thirsts!", "Come then! I am ready!", "Your end is here!", "Fight me if you dare!", "Another one falls!", "Is that all you've got?", "The Valkyries watch!"
		};
		speechConfig.Follow = new string[15]
		{
			"Lead on, I am your shield.", "Where the path takes us, I follow.", "A fine day to wander the wilds.", "My blade is yours to command.", "The road calls to us.", "I walk beside you, friend.", "Wherever you go, I go.", "Right behind you.", "The journey continues.", "I've got your back.",
			"What lies ahead, I wonder?", "Onward!", "Stay close, the wilds are dangerous.", "A good pace. I can keep up.", "Which way now?"
		};
		speechConfig.Hungry = new string[12]
		{
			"My belly roars like a troll...", "Even Fenrir ate better than this.", "A warrior fights on mead and meat, not empty guts.", "I'd trade my axe for a leg of boar right now.", "The hunger gnaws at my strength...", "I could eat an entire neck right now.", "My stomach is as empty as Ginnungagap.", "Got anything to eat?", "I'm starving here...", "A warrior without food is no warrior at all.",
			"Please, some food before I collapse.", "I'm running on fumes here."
		};
		speechConfig.Repair = new string[11]
		{
			"This blade has seen better days.", "My armor holds by thread and prayer.", "A dull edge brings a swift death.", "The smithy calls to my gear.", "Even the gods mend their weapons.", "My equipment needs attention.", "This won't hold much longer.", "A few more hits and this breaks.", "I should find a workbench soon.", "My gear is wearing thin.",
			"A warrior must maintain their tools."
		};
		speechConfig.Overweight = new string[10] { "By Odin's beard, my back is breaking!", "I carry the weight of Jotunheim on my shoulders...", "Not even Thor could haul this much further!", "My legs buckle beneath this burden!", "We must lighten this load before I collapse.", "I can barely move with all this weight!", "This is too much to carry!", "I'm overloaded!", "My pack is fit to burst!", "I need to put some of this down." };
		speechConfig.Smelt = new string[13]
		{
			"The forge fire burns bright.", "Good ore makes good steel.", "The bellows sing their song.", "Another ingot for the hoard.", "Dwarven work, this smelting.", "The flames hunger for more.", "Feeding the furnace.", "The kiln roars with life.", "More coal for the fire.", "Molten metal, a beautiful sight.",
			"The smelter works well today.", "Iron from ore, steel from iron.", "Keeping the fires stoked."
		};
		speechConfig.Idle = new string[16]
		{
			"The winds whisper of adventure...", "Skål!", "A calm before the storm, perhaps.", "I could use a horn of mead.", "I sense something stirring in the mist.", "What tales will they sing of us, I wonder?", "Nice place we've got here.", "The fire crackles nicely.", "A moment of peace... rare in these lands.", "Hmm, wonder what's for dinner.",
			"The world is quiet for now.", "Just enjoying the view.", "I could get used to this.", "Not a bad spot to rest.", "A fine hall you've built.", "Waiting for your command."
		};
		speechConfig.Hunt = new string[12]
		{
			"I see prey nearby.", "The hunt begins!", "Stay quiet... I'm stalking.", "Fresh meat for the pot!", "Got one in my sights.", "A clean shot, that's all I need.", "The wilds provide for the patient hunter.", "Tracking prey through the brush.", "Dinner's not going to catch itself.", "Easy now... nice and steady...",
			"Eikthyr would be proud.", "The hunt is on!"
		};
		speechConfig.Farm = new string[12]
		{
			"The soil is good here.", "Time to tend the crops.", "Seeds in the ground, hope in the heart.", "A good harvest takes patience.", "The crops are coming in nicely.", "Working the fields.", "Freya blesses this soil.", "Planting for the future.", "Almost ready to harvest.", "Farm work is honest work.",
			"Another row planted.", "The garden grows well."
		};
		speechConfig.Fish = new string[12]
		{
			"The water looks promising.", "Casting my line...", "Patience is a fisherman's greatest virtue.", "I feel a nibble!", "A fine catch!", "The fish are biting today.", "Nothing yet... but I'll wait.", "Come on, take the bait...", "Fishing is peaceful work.", "One more cast ought to do it.",
			"The river provides.", "Fresh fish for supper!"
		};
		speechConfig.Cook = new string[12]
		{
			"Something tasty is brewing!", "The cauldron bubbles nicely.", "A good meal lifts any warrior's spirits.", "Almost ready to serve.", "Cooking up a feast!", "The kitchen is my battlefield today.", "Mmm, smells delicious.", "Stirring the pot...", "A pinch of this, a dash of that.", "Mead's fermenting nicely.",
			"Food for the weary!", "This recipe never fails."
		};
		speechConfig.NoBed = new string[5] { "I've got no bed to call my own.", "Wish I had a bed around here.", "Every bed nearby is already taken.", "I'll sleep on the ground again, I suppose.", "A place to rest my head would be nice." };
		return speechConfig;
	}
}
