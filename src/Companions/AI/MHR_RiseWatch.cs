using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Companions;

// 1.5.70: diagnostic - a companion that shoots up faster and steeper than it can climb, without a jump or a ladder (log 31cb468a: from
// the front door at floor level to 4.4 m above the room within 3 s, no jump/ladder/warp line, then 6 minutes up there -
// user: "There is no way onto the roof"). Logs where it was and where it ended up, what it was doing and the building
// pieces round both spots, to see whether the physics pushed it up out of a piece.
internal static class MHR_RiseWatch
{
	private struct Sample
	{
		public float At;

		public Vector3 Pos;

		public string Doing;

		public bool Jumped;
	}

	private const float Every = 0.25f;

	private const float Window = 2f;

	private static readonly Dictionary<int, List<Sample>> s_samples = new Dictionary<int, List<Sample>>();

	private static readonly Dictionary<int, float> s_next = new Dictionary<int, float>();

	private static readonly Dictionary<int, float> s_quietUntil = new Dictionary<int, float>();

	// the game's own jump clock (Character.Jump sets it to 0)
	private static readonly AccessTools.FieldRef<Character, float> s_jumpTimer = AccessTools.FieldRefAccess<Character, float>("m_jumpTimer");

	internal static void Tick(CompanionSetup cs)
	{
		if (!CompanionsPlugin.LogDetailed)
		{
			return;
		}
		try
		{
			if (cs == null)
			{
				return;
			}
			int id = cs.GetInstanceID();
			float now = Time.time;
			if (s_next.TryGetValue(id, out var next) && now < next)
			{
				return;
			}
			s_next[id] = now + Every;
			Character c = cs.GetComponent<Character>();
			CompanionAI ai = cs.GetComponent<CompanionAI>();
			if (c == null || c.IsDead() || c.IsAttached())
			{
				s_samples.Remove(id);
				return;
			}
			if (!s_samples.TryGetValue(id, out var list))
			{
				list = new List<Sample>();
				s_samples[id] = list;
			}
			Vector3 pos = cs.transform.position;
			bool jumped = s_jumpTimer(c) < 1.2f || (ai != null && MHR_Mover.Climbing(ai));
			Sample prev = list.Count > 0 ? list[list.Count - 1] : default(Sample);
			bool hasPrev = list.Count > 0;
			list.Add(new Sample { At = now, Pos = pos, Doing = CompanionStatus.Describe(cs), Jumped = jumped });
			while (list.Count > 0 && now - list[0].At > Window)
			{
				list.RemoveAt(0);
			}
			if (s_quietUntil.TryGetValue(id, out var quiet) && now < quiet)
			{
				return;
			}
			// 1.5.70 (log c6fee971): only faster and steeper than anyone can climb - more than 1 m up in one sample (0.25 s),
			// steeper than a walkable slope, no jump or ladder - not walking up the stairs to the door (2.7 m over 5 m)
			if (!hasPrev || now - prev.At > 0.45f || jumped || prev.Jumped)
			{
				return;
			}
			Sample low = prev;
			float rise = pos.y - low.Pos.y;
			float across = new Vector2(pos.x - low.Pos.x, pos.z - low.Pos.z).magnitude;
			// 1.5.73: not the game moving it - a dungeon's inside is 5000 m up (log 702b4b9c: "rose 5001 m")
			if (rise < 1f || rise < across * 1.5f || rise > 50f)
			{
				return;
			}
			s_quietUntil[id] = now + 5f;
			CompanionsPlugin.Log.LogWarning($"[Diag] \"{c.m_name}\" rose {rise:F1} m in {now - low.At:F1} s from {low.Pos:F1} to {pos:F1} ({across:F1} m across), no jump or ladder; doing: {low.Doing ?? "?"} -> {CompanionStatus.Describe(cs) ?? "?"}; pieces at the start: {PiecesNear(low.Pos)}; at the end: {PiecesNear(pos)}");
		}
		catch
		{
		}
	}

	private static string PiecesNear(Vector3 p)
	{
		HashSet<string> names = new HashSet<string>();
		// solid colliders only - a bed's, fire's or portal's base zone is a big trigger (log c6fee971 listed the portal 16 m off)
		foreach (Collider col in Physics.OverlapSphere(p + Vector3.up * 0.9f, 1.2f, -1, QueryTriggerInteraction.Ignore))
		{
			Piece piece = col != null ? col.GetComponentInParent<Piece>() : null;
			if (piece != null)
			{
				names.Add(piece.m_name + " at " + piece.transform.position.ToString("F1"));
			}
			if (names.Count >= 8)
			{
				break;
			}
		}
		return names.Count > 0 ? string.Join(", ", names) : "none";
	}
}
