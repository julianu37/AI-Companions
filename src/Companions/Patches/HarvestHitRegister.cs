using HarmonyLib;
using UnityEngine;

namespace Companions;

// 1.5.67: hit register - the game tells the companion's HarvestController when one of its swings landed on a tree, log,
// stump or rock (user, log 9e9a7e57: "Maybe we do need a hit register on the logs. Would that help with the npc figuring
// out where to stand and not wander around so much?"). Every one of these has a public Damage(HitData) called by the
// attacker's swing.
[HarmonyPatch]
internal static class HarvestHitRegister
{
	[HarmonyPatch(typeof(TreeLog), "Damage")]
	[HarmonyPostfix]
	private static void TreeLogHit(TreeLog __instance, HitData hit) => Register(__instance, hit);

	[HarmonyPatch(typeof(TreeBase), "Damage")]
	[HarmonyPostfix]
	private static void TreeBaseHit(TreeBase __instance, HitData hit) => Register(__instance, hit);

	[HarmonyPatch(typeof(Destructible), "Damage")]
	[HarmonyPostfix]
	private static void DestructibleHit(Destructible __instance, HitData hit) => Register(__instance, hit);

	[HarmonyPatch(typeof(MineRock), "Damage")]
	[HarmonyPostfix]
	private static void MineRockHit(MineRock __instance, HitData hit) => Register(__instance, hit);

	[HarmonyPatch(typeof(MineRock5), "Damage")]
	[HarmonyPostfix]
	private static void MineRock5Hit(MineRock5 __instance, HitData hit) => Register(__instance, hit);

	private static void Register(Component target, HitData hit)
	{
		try
		{
			if (target == null || hit == null)
			{
				return;
			}
			Character attacker = hit.GetAttacker();
			if (attacker == null || attacker is Player)
			{
				return;
			}
			attacker.GetComponent<HarvestController>()?.OnHitRegistered(target.gameObject);
		}
		catch
		{
		}
	}
}
