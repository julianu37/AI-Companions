using HarmonyLib;
using UnityEngine;

namespace Companions;

public static class StaminaPatches
{
	[HarmonyPatch(typeof(Character), "UseStamina")]
	private static class UseStamina_Patch
	{
		private static bool Prefix(Character __instance, float stamina)
		{
			CompanionStamina component = __instance.GetComponent<CompanionStamina>();
			if (component == null)
			{
				return true;
			}
			float stamina2 = component.Stamina;
			component.Drain(stamina);
			if (stamina < 0.5f)
			{
				return false;   // per-frame trickles (reeling, hooked fish) would flood the log
			}
			if (CompanionsPlugin.LogDetailed) CompanionsPlugin.Log.LogDebug($"[Stamina] UseStamina — drained {stamina:F1} " + $"({stamina2:F1} → {component.Stamina:F1} / {component.MaxStamina:F1}) " + "companion=\"" + __instance.m_name + "\"");
			return false;
		}
	}

	[HarmonyPatch(typeof(Character), "CheckRun")]
	private static class CheckRun_Patch
	{
		private static void Postfix(Character __instance, ref bool __result)
		{
			if (__result)
			{
				CompanionStamina component = __instance.GetComponent<CompanionStamina>();
				if (component != null && component.Stamina <= 0f)
				{
					__result = false;
				}
			}
		}
	}

	[HarmonyPatch(typeof(Character), "HaveStamina")]
	private static class HaveStamina_Patch
	{
		private static bool Prefix(Character __instance, float amount, ref bool __result)
		{
			CompanionStamina component = __instance.GetComponent<CompanionStamina>();
			if (component == null)
			{
				return true;
			}
			// 1.4.109: strictly more than, like the player (Player.HaveStamina) - at 0 stamina "have stamina?" is no.
			// It said yes at 0, so a companion reeling in a fish with no stamina left kept it (vanilla loses it).
			__result = component.Stamina > amount;
			if (!__result && Time.time - _lastLogTime > 2f)
			{
				_lastLogTime = Time.time;
				if (CompanionsPlugin.LogDetailed) CompanionsPlugin.Log.LogDebug($"[Stamina] HaveStamina FAILED — need {amount:F1} " + $"have {component.Stamina:F1} / {component.MaxStamina:F1} " + "companion=\"" + __instance.m_name + "\"");
			}
			return false;
		}
	}

	private static float _lastLogTime;

	private const float LogInterval = 2f;
}
