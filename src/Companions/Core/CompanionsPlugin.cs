using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Companions;

[BepInPlugin(PluginGUID, PluginName, PluginVersion)]
[BepInDependency("com.profmags.traderoverhaul", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("com.haldor.bounties", BepInDependency.DependencyFlags.SoftDependency)]
public class CompanionsPlugin : BaseUnityPlugin
{
	// The GUID is deliberately kept as the upstream's: it is the companion's network identity and what the saves and
	// the BepInEx chainloader know. Renaming it would break worlds for no gain - the display name below is what shows.
	// 1.8.3: the settings file no longer follows the GUID (see MHR_GetConfigFile / AiCompanions.cfg).
	public const string PluginGUID = "Maddhattr.OfflineCompanionsReborn";

	public const string PluginName = "AI Companions";

	public const string PluginVersion = "1.8.3";

	private static Harmony _harmony;

	internal static ManualLogSource Log;

	// 1.8.3 (i18n): the settings file has its own name. The keys and descriptions are in English now (the mod's native
	// language), so the file is NOT the GUID-named one any more: BepInEx's default (base.Config) stays pointing at
	// Maddhattr.OfflineCompanionsReborn.cfg, which the mod no longer reads. The GUID itself is untouched (world and
	// network identity). A file is created on init, so the migration below has to run BEFORE the ConfigFile exists.
	private const string ConfigFileName = "AiCompanions.cfg";

	private ConfigFile MHR_GetConfigFile()
	{
		string configPath = Path.Combine(Paths.ConfigPath, ConfigFileName);
		try
		{
			MHR_MigrateOldConfig(configPath);
		}
		catch (Exception ex)
		{
			Log.LogWarning("[Config] Couldn't copy settings from the old config file: " + ex.ToString());
		}
		ConfigFile file;
		try
		{
			// saveOnInit: false - every Bind writes the file anyway; the creation itself must not take the plugin down
			// when the path is locked (see the catch below). The metadata keeps the mod listed by the
			// ConfigurationManager (F1), which scans BaseUnityPlugin.Config.
			file = new ConfigFile(configPath, saveOnInit: false, MetadataHelper.GetMetadata(this));
			MHR_AttachToPluginConfig(file);
			return file;
		}
		catch (Exception ex)
		{
			Log.LogWarning("[Config] Couldn't create " + ConfigFileName + " (" + ex.Message + ") - falling back to the " + PluginGUID + ".cfg that BepInEx created for the plugin");
			return base.Config;
		}
	}

	/// <summary>
	/// Points <see cref="BaseUnityPlugin.Config"/> at the file we created, so the third-party config managers (F1) keep
	/// listing the mod. The property is read-only and its backing field is the only way in; a failure is harmless
	/// (the mod reads the `ConfigEntry` objects, never `Config`).
	/// </summary>
	private void MHR_AttachToPluginConfig(ConfigFile file)
	{
		try
		{
			System.Reflection.FieldInfo backing = AccessTools.Field(typeof(BaseUnityPlugin), "<Config>k__BackingField");
			if (backing != null)
			{
				backing.SetValue(this, file);
			}
		}
		catch (Exception ex)
		{
			Log.LogDebug("[Config] Couldn't attach " + ConfigFileName + " to the plugin config property: " + ex.Message);
		}
	}

	private void MHR_MigrateOldConfig(string configPath)
	{
		string previous = Path.Combine(Paths.ConfigPath, PluginGUID + ".cfg");
		string original = Path.Combine(Paths.ConfigPath, "com.profmags.companions.cfg");
		bool copied = false;
		// Only seed from the upstream file for someone coming from the ORIGINAL mod: a 1.8.x player already has the
		// GUID-named file, and the 1.8.3 reset is on purpose (copying the 2021 file would silently undo it).
		if (!File.Exists(configPath) && !File.Exists(previous) && File.Exists(original))
		{
			File.Copy(original, configPath);
			copied = true;
			Log.LogInfo("[Config] Settings copied to " + ConfigFileName + " from com.profmags.companions.cfg (the old file is no longer used and can be deleted)");
		}
		// 1.8.3: the settings moved to a new file with English keys. The old PT-keyed file is not migrated on purpose
		// (the player accepted losing it): its keys are not read any more, so the settings go back to their defaults.
		if (File.Exists(previous))
		{
			string fate = copied ? "the settings came from com.profmags.companions.cfg" : "its settings are back to their defaults";
			Log.LogInfo("[Config] " + PluginGUID + ".cfg was replaced by " + ConfigFileName + " (the settings are in English now): the old file is ignored and " + fate + ". The old file can be deleted.");
		}
	}

	// 1.4.75: ProfMags' original mod. Checked at startup instead of with [BepInIncompatibility], which Hexium lists as a tag.
	private const string OriginalGUID = "com.profmags.companions";

	private bool _started;

	private static bool OriginalInstalled()
	{
		return Chainloader.PluginInfos.ContainsKey(OriginalGUID);
	}

	private static void LogOriginalInstalled()
	{
		Log.LogError("The original Offline Companions by ProfMags (" + OriginalGUID + ") is also installed. It doesn't work on Valheim 1.0 and can't run alongside " + PluginName + ". Remove the original Offline Companions to use " + PluginName + ". " + PluginName + " has not started.");
	}

	private void Awake()
	{
		Log = base.Logger;
		Log.LogInfo("[BUILD] Companions.dll built from C# source");
		Log.LogInfo(PluginName + " v" + PluginVersion + " loading...");
		if (OriginalInstalled())
		{
			LogOriginalInstalled();
			base.enabled = false;
			return;
		}
		_started = true;
		ModConfig.Init(MHR_GetConfigFile());
		_harmony = new Harmony(PluginGUID);
		MHR_ApplyPatches();
		EquipDiag.Install(_harmony);
		ModLocalization.Init();
		ModLocalization.EnsureDefaultFile();
		CompanionChatAI.Init();
		WhisperSpike.Init();
		WhisperStt.Init();
		VoiceInput.Init();
		CompanionTts.Init();
		SceneManager.sceneLoaded += OnSceneLoaded;
		Game.m_playerInitialSpawn += CompanionManager.MHR_StarterOnce;
		ConfigPanel.Create();
		// Fase 3.7: the in-game AI panel (personality, voice, push-to-talk) - built on first open, hidden until then
		CompanionAiPanel.Create();
		FarmZonePlacer.EnsureInstance();
	}

	// 1.8.3 (patch audit): one try/catch PER PATCH CLASS. A single broken target (a soft dependency that renamed a
	// method, a game update) used to throw a HarmonyException out of the single PatchAll(assembly) call and leave
	// every patch after it unapplied - silently, since the count was only logged on the success path.
	// The selection is the one Harmony itself uses: every type carrying a [HarmonyPatch] / [HarmonyPatchAll],
	// nested classes included (a container class simply carries no patch method of its own). Types that only
	// declare patch methods are picked up too, so a renamed attribute can never make the count drop.
	private void MHR_ApplyPatches()
	{
		Type[] types;
		try
		{
			types = Assembly.GetExecutingAssembly().GetTypes();
		}
		catch (ReflectionTypeLoadException ex)
		{
			types = ex.Types;
			Log.LogWarning("[Patch] Some types of the assembly could not be loaded: " + ex.Message);
		}
		catch (Exception arg)
		{
			Log.LogError($"[Companions] Harmony PatchAll failed: {arg}");
			return;
		}
		foreach (Type t in types)
		{
			if (t == null)
			{
				continue;
			}
			// 1.8.3 (patch audit): the filter itself is guarded. MHR_IsPatchClass instantiates attributes and
			// enumerates methods, so a pathological type could throw here, abort the whole loop and escape Awake -
			// which is exactly the "zero patches applied" failure mode this per-class loop exists to remove.
			try
			{
				if (!MHR_IsPatchClass(t))
				{
					continue;
				}
			}
			catch (Exception ex)
			{
				Log.LogDebug("[Patch] skipping " + t.FullName + ": " + ex.Message);
				continue;
			}
			try
			{
				_harmony.PatchAll(t);
			}
			catch (Exception ex)
			{
				Log.LogError("[Patch] " + t.FullName + ": " + ex.Message);
			}
		}
		int num = 0;
		foreach (MethodBase patchedMethod in _harmony.GetPatchedMethods())
		{
			num++;
		}
		Log.LogInfo(string.Format("{0} loaded successfully! ({1} methods patched)", PluginName, num));
	}

	private static bool MHR_IsPatchClass(Type t)
	{
		object[] attributes = t.GetCustomAttributes(true);
		for (int i = 0; i < attributes.Length; i++)
		{
			if (attributes[i] is HarmonyPatch || attributes[i] is HarmonyPatchAll)
			{
				return true;
			}
		}
		// Belt and braces: a patch class whose target lives on the methods instead of on the type.
		MethodInfo[] methods = t.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
		for (int j = 0; j < methods.Length; j++)
		{
			if (methods[j].IsDefined(typeof(HarmonyPrefix), true) || methods[j].IsDefined(typeof(HarmonyPostfix), true) || methods[j].IsDefined(typeof(HarmonyTranspiler), true) || methods[j].IsDefined(typeof(HarmonyFinalizer), true) || methods[j].IsDefined(typeof(HarmonyReversePatch), true))
			{
				return true;
			}
		}
		return false;
	}

	// Plugins load in GUID order, so the original normally loads first and Awake catches it. Start runs after every
	// plugin has loaded, so this catches it if the order ever changes.
	private void Start()
	{
		if (_started && OriginalInstalled())
		{
			LogOriginalInstalled();
			Shutdown();
			base.enabled = false;
		}
	}

	private void Shutdown()
	{
		if (!_started)
		{
			return;
		}
		_started = false;
		SceneManager.sceneLoaded -= OnSceneLoaded;
		Game.m_playerInitialSpawn -= CompanionManager.MHR_StarterOnce;
		// Fase 3.3: stop recording (and free the microphone clip) and release the native Whisper model
		VoiceInput.Shutdown();
		WhisperStt.Shutdown();
		// Fase 3.4: stop the speech worker, the AudioSource and release the native Kokoro model
		CompanionTts.Shutdown();
		_harmony?.UnpatchSelf();
		// 1.8.3: every root the mod keeps alive across scenes is destroyed here. Only four of them used to be (the
		// settings panel, the AI panel, the appearance editor and the farm zone placer), so the wheel, the HUD and the
		// starter/home/crop/interaction panels stayed behind - reloading the plugin left the old copies running, and
		// they would still draw over the game with the patches gone.
		List<GameObject> roots = new List<GameObject>
		{
			MHR_Root(ConfigPanel.Instance),
			MHR_Root(CompanionAiPanel.Instance),
			MHR_Root(CompanionAppearancePanel.Instance),
			MHR_Root(FarmZonePlacer.Instance),
			MHR_Root(CompanionRadialMenu.Instance),
			MHR_Root(CompanionHudPanel.Instance),
			MHR_Root(StarterCompanionPanel.Instance),
			MHR_Root(HomeZonePanel.Instance),
			MHR_Root(CompanionInteractPanel.Instance),
			MHR_Root(FarmZoneCropPicker.Root),
			// the EventSystem the AI panel creates when the game has none (its own root, see EnsureEventSystem)
			CompanionAiPanel.EventSystemRoot
		};
		// The companion's voice holder ("ModIA-CompanionTts") is not here: CompanionTts.Shutdown above already
		// destroys it, together with the native model.
		for (int i = 0; i < roots.Count; i++)
		{
			if (roots[i] != null)
			{
				UnityEngine.Object.Destroy(roots[i]);
			}
		}
	}

	/// <summary>The GameObject of a mod root, or null when it was never created or is already destroyed. Unity
	/// answers a destroyed object as <c>== null</c>, which is what the caller checks.</summary>
	private static GameObject MHR_Root(UnityEngine.Object owner)
	{
		if (owner == null)
		{
			return null;
		}
		GameObject root = owner as GameObject;
		if (root != null)
		{
			return root;
		}
		Component component = owner as Component;
		if (component == null)
		{
			return null;
		}
		return component.gameObject;
	}

	private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
	{
		FontFix.Ensure("scene:" + scene.name);
		// Fase 3.5: the conversation memory lives in memory only - leaving the world forgets it
		CompanionMemory.ClearAll();
		// the carry-weight cache is keyed by Inventory reference: a new scene means new inventories (the old ones are
		// gone), so drop the snapshots and the owner references with them
		CompanionCarry.Reset();
		// the chat AI keeps per-session state too: who is speaking, the pending message, the cooldown and the slot of
		// the request in flight (a new world is a new conversation and a new set of companions)
		CompanionChatAI.Reset();
		LlmClient.Reset();
	}

	private void OnDestroy()
	{
		Shutdown();
		Log?.LogInfo("Offline Companions unloaded.");
	}

	private void Update()
	{
		try
		{
			MHR_RespawnTracker.Tick();
			MHR_ToolKeeper.Tick();
		}
		catch (Exception ex)
		{
			Log.LogWarning("[Respawn] Tracker error: " + ex.ToString());
		}
		try
		{
			MHR_Roster.Tick();
		}
		catch (Exception ex2)
		{
			Log.LogWarning("[Roster] Error: " + ex2.ToString());
		}
		try
		{
			// Fase 3.1: applies any chat order the model has finished thinking about (never blocks)
			CompanionChatAI.Tick();
			// Fase 3.20: tells the player how the "Testar conexão" of the AI panel went (a background probe, never blocks)
			LlmConnectionTest.Tick();
		}
		catch (Exception ex3)
		{
			Log.LogWarning("[IA] Error: " + ex3.ToString());
		}
		try
		{
			// SPIKE: fires the in-process Whisper.net test once, when [AI] SttTest asked for it (never blocks)
			WhisperSpike.Tick();
		}
		catch (Exception ex4)
		{
			Log.LogWarning("[STT] Error: " + ex4.ToString());
		}
		try
		{
			// Fase 3.3: push-to-talk + drains the finished transcriptions (never blocks, never allocates when idle)
			VoiceInput.Tick();
		}
		catch (Exception ex5)
		{
			Log.LogWarning("[STT] Error: " + ex5.ToString());
		}
		try
		{
			// Fase 3.4: pre-warms the voice model and drives the speech playback queue (never blocks)
			CompanionTts.Tick();
		}
		catch (Exception ex6)
		{
			Log.LogWarning("[TTS] Error: " + ex6.ToString());
		}
	}
}
