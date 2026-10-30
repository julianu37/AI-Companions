using System;
using System.Collections.Generic;
using UnityEngine;

namespace Companions;

public static class MHR_Diag
{
	private class Track
	{
		public Vector3 Anchor;

		public float Since;

		public bool Logged;
	}

	private static readonly Dictionary<int, Track> s_track = new Dictionary<int, Track>();

	private static float s_next;

	public static void Tick()
	{
		if (!CompanionsPlugin.LogDetailed)
		{
			return;
		}
		float time = Time.time;
		if (time < s_next)
		{
			return;
		}
		s_next = time + 1f;
		Player localPlayer = Player.m_localPlayer;
		IReadOnlyList<CompanionSetup> allCompanions = CompanionSetup.AllCompanions;
		if (localPlayer == null || allCompanions == null)
		{
			s_track.Clear();
			return;
		}
		Vector3 position = localPlayer.transform.position;
		for (int i = 0; i < allCompanions.Count; i++)
		{
			CompanionSetup companionSetup = allCompanions[i];
			if (companionSetup == null)
			{
				continue;
			}
			int instanceID = companionSetup.GetInstanceID();
			ZNetView component = companionSetup.GetComponent<ZNetView>();
			if (component == null || !component.IsValid() || !component.IsOwner() || !companionSetup.GetFollow() || component.GetZDO().GetInt(CompanionSetup.ActionModeHash) != 0)
			{
				s_track.Remove(instanceID);
				continue;
			}
			Vector3 position2 = companionSetup.transform.position;
			float num = Vector3.Distance(position2, position);
			if (!s_track.TryGetValue(instanceID, out var value))
			{
				Track track = new Track();
				track.Anchor = position2;
				track.Since = time;
				value = track;
				s_track[instanceID] = value;
			}
			// 1.4.86: standing still in a fight (shooting from where it is) isn't being stuck
			else if (num <= 10f || Vector3.Distance(position2, value.Anchor) > 1f || (companionSetup.GetComponent<CompanionAI>()?.IsInCombat ?? false))
			{
				value.Anchor = position2;
				value.Since = time;
				value.Logged = false;
			}
			else if (!value.Logged && !(time - value.Since < 10f))
			{
				value.Logged = true;
				Report(companionSetup, component, position2, num, time - value.Since);
			}
		}
	}

	private static void Report(CompanionSetup cs, ZNetView nv, Vector3 pos, float dist, float stuckFor)
	{
		try
		{
			string text = "Companion";
			Character component = cs.GetComponent<Character>();
			if (component != null && !string.IsNullOrEmpty(component.m_name))
			{
				text = component.m_name;
			}
			int num = ((nv.GetZDO() == null) ? (-1) : nv.GetZDO().GetInt(CompanionSetup.ActionModeHash));
			CompanionAI component2 = cs.GetComponent<CompanionAI>();
			CompanionRest component3 = cs.GetComponent<CompanionRest>();
			string text2 = "";
			if (Active(cs.GetComponent<HarvestController>()))
			{
				text2 += "harvest ";
			}
			if (Active(cs.GetComponent<SmeltController>()))
			{
				text2 += "smelt ";
			}
			if (Active(cs.GetComponent<FarmController>()))
			{
				text2 += "farm ";
			}
			if (Active(cs.GetComponent<FishController>()))
			{
				text2 += "fish ";
			}
			if (Active(cs.GetComponent<CookController>()))
			{
				text2 += "cook ";
			}
			if (Active(cs.GetComponent<RepairController>()))
			{
				text2 += "repair-gear ";
			}
			if (Active(cs.GetComponent<UpkeepController>()))
			{
				text2 += "repair-restock ";
			}
			if (text2.Length == 0)
			{
				text2 = "none";
			}
			string text3 = ((!(component2 == null)) ? ("followTarget=" + component2.MHR_DiagFollowName() + " foundPath=" + component2.MHR_DiagFoundPath() + " inCombat=" + component2.IsInCombat + " recoveringTombstone=" + component2.IsRecoveringTombstone) : "no AI");
			string text4 = ((!(component3 == null)) ? ("sleeping=" + component3.IsSleeping + " navigatingToRest=" + component3.IsNavigating + " sittingOrResting=" + component3.IsResting) : "no rest");
			if (CompanionsPlugin.Log != null)
			{
				CompanionsPlugin.Log.LogWarning($"[Diag] \"{text}\" is set to follow but hasn't moved for {stuckFor:F0}s (you are {dist:F0}m away) at {pos}. jobMode={num} activeTask={text2.Trim()} {text3} {text4}");
			}
		}
		catch (Exception ex)
		{
			if (CompanionsPlugin.Log != null)
			{
				CompanionsPlugin.Log.LogWarning("[Diag] report failed: " + ex.Message);
			}
		}
	}

	private static bool Active(HarvestController c)
	{
		return c != null && c.IsActive;
	}

	private static bool Active(SmeltController c)
	{
		return c != null && c.IsActive;
	}

	private static bool Active(FarmController c)
	{
		return c != null && c.IsActive;
	}

	private static bool Active(FishController c)
	{
		return c != null && c.IsActive;
	}

	private static bool Active(CookController c)
	{
		return c != null && c.IsActive;
	}

	private static bool Active(RepairController c)
	{
		return c != null && c.IsActive;
	}

	private static bool Active(UpkeepController c)
	{
		return c != null && c.IsActive;
	}
}
