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
	public const string PluginGUID = "Maddhattr.OfflineCompanionsReborn";

	public const string PluginName = "Offline Companions Reborn";

	public const string PluginVersion = "1.5.4";

	private static Harmony _harmony;

	internal static ManualLogSource Log;

	private bool _fontFixWarned;

	private ConfigFile MHR_GetConfigFile()
	{
		try
		{
			MHR_MigrateOldConfig();
		}
		catch (Exception ex)
		{
			Log.LogWarning("[Config] Couldn't copy settings from the old config file: " + ex.ToString());
		}
		return base.Config;
	}

	private void MHR_MigrateOldConfig()
	{
		ConfigFile config = base.Config;
		string configFilePath = config.ConfigFilePath;
		string text = Path.Combine(Paths.ConfigPath, "com.profmags.companions.cfg");
		if (!File.Exists(configFilePath) && File.Exists(text))
		{
			File.Copy(text, configFilePath);
			config.Reload();
			Log.LogInfo("[Config] Settings copied to Maddhattr.OfflineCompanionsReborn.cfg from com.profmags.companions.cfg (the old file is no longer used and can be deleted)");
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
			Log.LogInfo(string.Format("{0} loaded successfully! ({1} methods patched)", "Offline Companions Reborn", num));
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
		SceneManager.sceneLoaded += OnSceneLoaded;
		Game.m_playerInitialSpawn += CompanionManager.MHR_StarterOnce;
		ConfigPanel.Create();
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
		_harmony?.UnpatchSelf();
		if (ConfigPanel.Instance != null)
		{
			UnityEngine.Object.Destroy(ConfigPanel.Instance.gameObject);
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
		}
		catch (Exception ex3)
		{
			Log.LogWarning("[IA] Error: " + ex3.ToString());
		}
		try
		{
			// SPIKE: fires the in-process Whisper.net test once, when [IA] SttTeste asked for it (never blocks)
			WhisperSpike.Tick();
		}
		catch (Exception ex4)
		{
			Log.LogWarning("[STT] Error: " + ex4.ToString());
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
