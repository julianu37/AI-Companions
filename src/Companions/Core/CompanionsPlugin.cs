using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using TMPro;
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
	// 2.0.0: the settings file no longer follows the GUID (see MHR_GetConfigFile / AiCompanions.cfg).
	public const string PluginGUID = "Maddhattr.OfflineCompanionsReborn";

	public const string PluginName = "AI Companions";

	public const string PluginVersion = "2.0.0";

	private static Harmony _harmony;

	internal static ManualLogSource Log;

	private bool _fontFixWarned;

	// 2.0.0 (i18n): the settings file has its own name. The keys and descriptions are in English now (the mod's native
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
		// GUID-named file, and the 2.0.0 reset is on purpose (copying the 2021 file would silently undo it).
		if (!File.Exists(configPath) && !File.Exists(previous) && File.Exists(original))
		{
			File.Copy(original, configPath);
			copied = true;
			Log.LogInfo("[Config] Settings copied to " + ConfigFileName + " from com.profmags.companions.cfg (the old file is no longer used and can be deleted)");
		}
		// 2.0.0: the settings moved to a new file with English keys. The old PT-keyed file is not migrated on purpose
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
		try
		{
			_harmony.PatchAll(Assembly.GetExecutingAssembly());
			int num = 0;
			foreach (MethodBase patchedMethod in _harmony.GetPatchedMethods())
			{
				num++;
			}
			Log.LogInfo(string.Format("{0} loaded successfully! ({1} methods patched)", PluginName, num));
		}
		catch (Exception arg)
		{
			Log.LogError($"[Companions] Harmony PatchAll failed: {arg}");
		}
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
		if (ConfigPanel.Instance != null)
		{
			UnityEngine.Object.Destroy(ConfigPanel.Instance.gameObject);
		}
		// Fase 3.7: the AI panel (its own GameObject; the backdrop and the controls go with it)
		if (CompanionAiPanel.Instance != null)
		{
			UnityEngine.Object.Destroy(CompanionAiPanel.Instance.gameObject);
		}
		// Fase 3.20: the appearance editor (it owns a camera, a RenderTexture and the preview clone - Teardown runs with
		// the GameObject, so unloading the plugin mid-edit does not leave them behind)
		if (CompanionAppearancePanel.Instance != null)
		{
			UnityEngine.Object.Destroy(CompanionAppearancePanel.Instance.gameObject);
		}
		if (FarmZonePlacer.Instance != null)
		{
			UnityEngine.Object.Destroy(FarmZonePlacer.Instance.gameObject);
		}
	}

	private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
	{
		FontFix.Ensure("scene:" + scene.name);
		// Fase 3.5: the conversation memory lives in memory only - leaving the world forgets it
		CompanionMemory.ClearAll();
		// the carry-weight cache is keyed by Inventory reference: a new scene means new inventories (the old ones are
		// gone), so drop the snapshots and the owner references with them
		CompanionCarry.Reset();
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

	private static bool IsBrokenTmpFont(TMP_FontAsset font)
	{
		if (!(font == null))
		{
			return font.name.IndexOf("LiberationSans", StringComparison.OrdinalIgnoreCase) >= 0;
		}
		return true;
	}

	private static TMP_FontAsset FindReplacementTmpFont()
	{
		TMP_Text[] array = Resources.FindObjectsOfTypeAll<TMP_Text>();
		foreach (TMP_Text tMP_Text in array)
		{
			if (!(tMP_Text == null))
			{
				TMP_FontAsset font = tMP_Text.font;
				if (!IsBrokenTmpFont(font))
				{
					return font;
				}
			}
		}
		TMP_FontAsset[] array2 = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
		TMP_FontAsset tMP_FontAsset = null;
		foreach (TMP_FontAsset tMP_FontAsset2 in array2)
		{
			if (!IsBrokenTmpFont(tMP_FontAsset2))
			{
				string text = tMP_FontAsset2.name.ToLowerInvariant();
				if (text.Contains("averia") || text.Contains("norse") || text.Contains("valheim"))
				{
					return tMP_FontAsset2;
				}
				if (tMP_FontAsset == null)
				{
					tMP_FontAsset = tMP_FontAsset2;
				}
			}
		}
		return tMP_FontAsset;
	}

	private void EnsureTmpDefaultFont(string source)
	{
		if (!IsBrokenTmpFont(TMP_Settings.defaultFontAsset))
		{
			return;
		}
		TMP_FontAsset tMP_FontAsset = FindReplacementTmpFont();
		if (tMP_FontAsset == null)
		{
			if (!_fontFixWarned)
			{
				_fontFixWarned = true;
				Log?.LogDebug("[Fonts] No replacement TMP font found yet (" + source + ") — will retry on next scene load.");
			}
			return;
		}
		TMP_Settings.defaultFontAsset = tMP_FontAsset;
		_fontFixWarned = false;
		int num = 0;
		TMP_Text[] array = Resources.FindObjectsOfTypeAll<TMP_Text>();
		foreach (TMP_Text tMP_Text in array)
		{
			if (!(tMP_Text == null) && IsBrokenTmpFont(tMP_Text.font))
			{
				tMP_Text.font = tMP_FontAsset;
				num++;
			}
		}
		Log?.LogInfo($"[Fonts] TMP default font repaired to '{tMP_FontAsset.name}' ({source}), reassigned={num}.");
	}
}
