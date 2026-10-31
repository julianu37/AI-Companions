using System;

namespace Companions;

internal static class StarterFix
{
	// true when it cleared the flag just now (the starter panel is then shown even with a companion around)
	internal static bool ResetIfRequested()
	{
		try
		{
			if (ModConfig.ResetStarterFlag == null || !ModConfig.ResetStarterFlag.Value)
			{
				return false;
			}
			Player localPlayer = Player.m_localPlayer;
			string text = Key();
			if (!(localPlayer == null) && text != null)
			{
				localPlayer.m_customData.Remove(text);
				ModConfig.ResetStarterFlag.Value = false;
				if (Game.instance != null)
				{
					Game.instance.SavePlayerProfile(setLogoutPoint: false);
				}
				if (CompanionsPlugin.LogNormal) Log("[CompanionManager] ResetStarterFlag: cleared the starter-companion flag for this world/character; the starter panel can show again.");
				return true;
			}
		}
		catch (Exception ex)
		{
			if (CompanionsPlugin.LogNormal) Log("[CompanionManager] ResetStarterFlag failed: " + ex.Message);
		}
		return false;
	}

	internal static void MarkSpawned()
	{
		try
		{
			Player localPlayer = Player.m_localPlayer;
			string text = Key();
			if (!(localPlayer == null) && text != null)
			{
				localPlayer.m_customData[text] = "1";
				if (Game.instance != null)
				{
					Game.instance.SavePlayerProfile(setLogoutPoint: false);
				}
				if (CompanionsPlugin.LogNormal) Log("[CompanionManager] Starter companion spawned - marked this world as done for the character.");
			}
		}
		catch (Exception ex)
		{
			if (CompanionsPlugin.LogNormal) Log("[CompanionManager] MarkSpawned failed: " + ex.Message);
		}
	}

	private static string Key()
	{
		ZNet instance = ZNet.instance;
		if (instance == null)
		{
			return null;
		}
		return $"HC_StarterCompanion_{instance.GetWorldUID()}";
	}

	private static void Log(string s)
	{
		if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log?.LogInfo(s);
	}
}
