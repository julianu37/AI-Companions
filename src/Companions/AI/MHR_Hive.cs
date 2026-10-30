using System.Collections.Generic;
using UnityEngine;

namespace Companions;

// 1.5.90: the command key on a beehive (user, log 32bba563: "Tried to use command on a beehive and the NPC just moved to
// the area" - "honey should stay in the inventory"). Like a player: walk up to it, use it (the game's own extract - the honey
// pops out), pick the honey up and keep it.
internal static class MHR_Hive
{
	private class S
	{
		public Beehive Hive;
		public string Honey;
		public bool Extracted;
		public float Since;
		public float StepAt;
		public float ActAt;
		public Vector3 At;
		public HashSet<int> Before = new HashSet<int>();
		public int Took;
		public int HadBefore;
	}

	private static readonly Dictionary<int, S> s_s = new Dictionary<int, S>();

	private static void Log(string msg)
	{
		if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log?.LogInfo("[Hive] " + msg);
	}

	private static S Peek(CompanionAI ai)
	{
		return ai != null && s_s.TryGetValue(ai.GetInstanceID(), out S s) ? s : null;
	}

	internal static bool Active(CompanionAI ai)
	{
		S s = Peek(ai);
		return s != null && s.Hive != null;
	}

	internal static int HoneyIn(Beehive hive)
	{
		return hive?.GetComponent<ZNetView>()?.GetZDO()?.GetInt(ZDOVars.s_level) ?? 0;
	}

	internal static void Start(CompanionAI ai, Beehive hive)
	{
		if (ai == null || hive == null || hive.m_honeyItem == null)
		{
			return;
		}
		S s = new S
		{
			Hive = hive,
			Honey = hive.m_honeyItem.m_itemData.m_shared.m_name,
			Since = Time.time,
			StepAt = Time.time,
			HadBefore = ai.GetComponent<Humanoid>()?.GetInventory()?.CountItems(hive.m_honeyItem.m_itemData.m_shared.m_name) ?? 0
		};
		s_s[ai.GetInstanceID()] = s;
		MHR_Mover.Release(ai);
		if (CompanionsPlugin.LogNormal) Log($"{ai.GetComponent<Character>()?.m_name} collecting {HoneyIn(hive)} honey from the beehive at {MHR_Mover.Fmt(hive.transform.position)}");
	}

	internal static void Cancel(CompanionAI ai)
	{
		if (Active(ai))
		{
			s_s.Remove(ai.GetInstanceID());
			MHR_Mover.Release(ai);
		}
	}

	private static bool End(CompanionAI ai, string why)
	{
		if (why != null)
		{
			if (CompanionsPlugin.LogNormal) Log(ai.GetComponent<Character>()?.m_name + " - " + why);
		}
		s_s.Remove(ai.GetInstanceID());
		MHR_Mover.Release(ai);
		ai.StopMoving();
		ai.BoardingEnded();
		return false;
	}

	// honey gained since the order (its own pickups and the auto-pickup alike)
	private static int Gained(CompanionAI ai, S s)
	{
		int now = ai.GetComponent<Humanoid>()?.GetInventory()?.CountItems(s.Honey) ?? 0;
		return Mathf.Max(s.Took, now - s.HadBefore);
	}

	// every frame while ordered; true while still at it
	internal static bool Update(CompanionAI ai, float dt)
	{
		S s = Peek(ai);
		Humanoid h = ai.GetComponent<Humanoid>();
		if (s == null || h == null)
		{
			return false;
		}
		float now = Time.time;
		Beehive hive = s.Hive;
		ZNetView nview = hive != null ? hive.GetComponent<ZNetView>() : null;
		Vector3 me = ai.transform.position;
		if (!s.Extracted)
		{
			if (nview == null || !nview.IsValid())
			{
				return End(ai, "the beehive is gone");
			}
			if (HoneyIn(hive) <= 0)
			{
				return End(ai, "someone emptied the beehive first");
			}
			Vector3 point = hive.transform.position + Vector3.up * 0.5f;
			float reach = Vector3.Distance(me + Vector3.up * MHR_Chests.EyeHeight, point);
			if (reach > 2.5f)
			{
				MHR_Mover.Go(ai, dt, hive.transform.position, 1.2f, false, "Hive");
				if (MHR_Mover.GaveUp(ai) || now - s.Since > 30f)
				{
					return End(ai, "couldn't get to the beehive");
				}
				s.ActAt = 0f;
				return true;
			}
			ai.StopMoving();
			MHR_Mover.Release(ai);
			ai.LookAtPoint(point);
			if (s.ActAt == 0f)
			{
				s.ActAt = now + 0.4f;
				return true;
			}
			if (now < s.ActAt || (!ai.IsLookingAtPoint(point, 20f) && now < s.ActAt + 2f))
			{
				return true;
			}
			ai.GetComponent<ZSyncAnimation>()?.SetTrigger("interact");
			s.At = hive.m_spawnPoint != null ? hive.m_spawnPoint.position : hive.transform.position;
			s.Before = MHR_Chests.DropsNear(s.At, 3f);
			nview.InvokeRPC("RPC_Extract");
			s.Extracted = true;
			s.StepAt = now;
			return true;
		}
		// the honey pops out: pick up the new drops
		if (now - s.StepAt < 0.6f)
		{
			return true;
		}
		if (now - s.StepAt > 20f)
		{
			return End(ai, $"picked up {Gained(ai, s)} honey");
		}
		Inventory bag = h.GetInventory();
		ItemDrop nearest = null;
		float nd = float.MaxValue;
		foreach (Collider c in Physics.OverlapSphere(s.At, 3f))
		{
			ItemDrop d = c != null ? c.GetComponentInParent<ItemDrop>() : null;
			if (d == null || d.m_itemData == null || d.m_itemData.m_shared.m_name != s.Honey || s.Before.Contains(d.GetInstanceID()) || !bag.CanAddItem(d.m_itemData, 1))
			{
				continue;
			}
			float dist = Vector3.Distance(me, d.transform.position);
			if (dist < nd)
			{
				nd = dist;
				nearest = d;
			}
		}
		if (nearest == null)
		{
			return End(ai, $"picked up {Gained(ai, s)} honey - keeping it");
		}
		if (nd > 1.5f)
		{
			MHR_Mover.Go(ai, dt, nearest.transform.position, 1.0f, false, "Hive");
			return true;
		}
		if (!nearest.CanPickup())
		{
			nearest.RequestOwn();
			ai.StopMoving();
			return true;
		}
		int n = nearest.m_itemData.m_stack;
		if (h.Pickup(nearest.gameObject, autoequip: false, autoPickupDelay: false))
		{
			s.Took += n;
		}
		return true;
	}
}
