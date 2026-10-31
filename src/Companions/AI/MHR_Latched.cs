using HarmonyLib;
using UnityEngine;

namespace Companions;

// 1.5.73/1.5.74: a tick latched on to a companion (user, log 93298113: "Tick sucked NPC blood and NPC did nothing?" - it
// latched on and he spent 8 s "stepping back to get its breath back"; a latched tick moves with you). The game
// (Attack.TryAttach/UpdateAttach) keeps it on until its host or the tick dies, or the host dodge-rolls. 1.5.73 rolled it
// off; 1.5.74 dropped the roll (user: "I never have an issue hitting things that are standing close and was able to swing
// on a tick that was attached to me") - it is fought first and swung at, with no stepping away (CompanionCombatTargets,
// CompanionCombatTactics.TacticsFor). Ticks belong to the enemy strategies overhaul.
internal static class MHR_Latched
{
	private static readonly AccessTools.FieldRef<Humanoid, Attack> s_currentAttack = AccessTools.FieldRefAccess<Humanoid, Attack>("m_currentAttack");

	private static readonly AccessTools.FieldRef<Attack, bool> s_isAttached = AccessTools.FieldRefAccess<Attack, bool>("m_isAttached");

	private static readonly AccessTools.FieldRef<Attack, Transform> s_attachTarget = AccessTools.FieldRefAccess<Attack, Transform>("m_attachTarget");

	// a creature latched on to c (a tick feeding), or null
	internal static Character LatchedOn(Character c)
	{
		if (c == null)
		{
			return null;
		}
		Vector3 me = c.transform.position;
		foreach (Character o in Character.GetAllCharacters())
		{
			if (o == null || o == c || o.IsDead() || !(o is Humanoid h) || (o.transform.position - me).sqrMagnitude > 16f)
			{
				continue;
			}
			Attack a = s_currentAttack(h);
			if (a != null && s_isAttached(a) && s_attachTarget(a) == c.transform)
			{
				return o;
			}
		}
		return null;
	}
}
