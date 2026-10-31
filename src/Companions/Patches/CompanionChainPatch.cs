using HarmonyLib;

namespace Companions;

// 1.5.53: a companion mid-combo starts its next swing as soon as the current one allows it, the way a player holding
// attack does. Player overrides HaveQueuedChain (queued attack or attack held -> Attack.CanStartChainAttack, true once the
// swing animation's Chain event fired); Humanoid's own returns false, so a non-player waited for the whole swing to end.
// Player doesn't call the base method, so this only reaches non-players.
[HarmonyPatch(typeof(Humanoid), "HaveQueuedChain")]
internal static class CompanionChainPatch
{
	private static void Postfix(Humanoid __instance, ref bool __result)
	{
		if (__result || __instance == null)
		{
			return;
		}
		CompanionCombatAI combat = __instance.GetComponent<CompanionCombatAI>();
		if (combat != null && combat.WantsChain)
		{
			__result = combat.CurrentAttackCanChain();
			return;
		}
		// 1.5.61: chopping and mining chain the same way
		HarvestController harvest = __instance.GetComponent<HarvestController>();
		if (harvest != null && harvest.WantsChain)
		{
			__result = harvest.CurrentAttackCanChain();
		}
	}
}
