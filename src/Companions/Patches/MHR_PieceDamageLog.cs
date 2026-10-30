using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Companions;

// 1.5.44: debug logging - what damages a building piece while a companion is near it (user: "I had to repair the hearth
// again. Somehow when the NPC jumps onto it and is stuck for a moment it does damage to the hearth but not the NPC.").
// Nothing in our code hits pieces outside of commands, so log every damage a piece takes within 5 m of a companion: the
// hit type, who did it, and how much (no hit = the game's own wear: rain, water, lava, missing support).
[HarmonyPatch(typeof(WearNTear), "ApplyDamage")]
internal static class MHR_PieceDamageLog
{
	private static readonly Dictionary<int, float> s_last = new Dictionary<int, float>();

	private static void Prefix(WearNTear __instance, float damage, HitData hitData)
	{
		try
		{
			if (__instance == null || damage <= 0f)
			{
				return;
			}
			Vector3 at = __instance.transform.position;
			CompanionSetup near = null;
			foreach (CompanionSetup cs in CompanionSetup.AllCompanions)
			{
				if (cs != null && Vector3.Distance(cs.transform.position, at) <= 5f)
				{
					near = cs;
					break;
				}
			}
			if (near == null)
			{
				return;
			}
			int id = __instance.GetInstanceID();
			if (s_last.TryGetValue(id, out var t) && Time.time - t < 2f)
			{
				return;
			}
			if (s_last.Count > 500)
			{
				s_last.Clear();
			}
			s_last[id] = Time.time;
			Piece p = __instance.GetComponent<Piece>();
			string name = (p != null) ? (MHR_List.Local(p.m_name) ?? p.m_name) : __instance.name;
			string why;
			if (hitData == null)
			{
				why = "wear (rain, water, lava or missing support)";
			}
			else
			{
				Character attacker = hitData.GetAttacker();
				why = "hit " + hitData.m_hitType + (attacker != null ? " by " + attacker.m_name : " (no attacker)") + ", fire " + hitData.m_damage.m_fire.ToString("F1") + ", chop " + hitData.m_damage.m_chop.ToString("F1") + ", blunt " + hitData.m_damage.m_blunt.ToString("F1");
			}
			ZNetView nv = near.GetComponent<ZNetView>();
			string who = (nv?.GetZDO() != null) ? nv.GetZDO().GetString(CompanionSetup.NameHash, "companion") : "companion";
			if (CompanionsPlugin.LogDetailed) CompanionsPlugin.Log.LogDebug("[Piece] " + name + " at " + MHR_Mover.Fmt(at) + " took " + damage.ToString("F1") + " damage (" + why + ") - " + who + " is " + Vector3.Distance(near.transform.position, at).ToString("F1") + " m away at " + MHR_Mover.Fmt(near.transform.position));
		}
		catch
		{
		}
	}
}
