using HarmonyLib;

namespace Companions;

// 1.5.4: debug logging for companion attacks (user: "Should we add some logging around it to better see what is
// happening? At least until we have fixed everything."). Character.Damage runs on the attacker's side for every hit a
// melee sweep or a projectile lands, so a companion's hits are seen here and matched to its last swing or throw.
[HarmonyPatch(typeof(Character), "Damage")]
internal static class CompanionHitLog
{
	private static void Prefix(Character __instance, HitData hit)
	{
		if (hit == null || __instance == null)
		{
			return;
		}
		Character attacker = hit.GetAttacker();
		if (attacker == null || attacker == __instance)
		{
			return;
		}
		if (attacker == Player.m_localPlayer)
		{
			PlayerCombatLog.NoteHit(__instance, hit);
			return;
		}
		CompanionCombatAI combat = attacker.GetComponent<CompanionCombatAI>();
		if (combat != null)
		{
			combat.NoteHit(__instance, hit);
		}
	}
}
