using System;
using System.Collections.Generic;
using UnityEngine;

namespace Companions;

/// <summary>
/// Fase E10: the <b>leveling job</b> - "nivele um terreno 10x10 aqui". It works the ground the way a player does:
/// walks to a point, swings the <b>hoe</b>, the operation lands, walks to the next point - and comes back for another
/// pass when the ground is still uneven. Nothing is instant: the whole area is never leveled in one frame.
///
/// <para>Rules of the house, the same ones <see cref="MHR_Collect"/> and <see cref="MHR_Build"/> follow: the state
/// lives in memory, a fight in between does not use up the job's time, there is a timeout, and every ending
/// speaks.</para>
///
/// <para><b>The game's leveling is soft</b> (measured 01/11/2026): the "level ground" of the hoe is a smooth operation
/// that pulls the terrain toward the point with a falloff and moves it by at most ~1 m in total, so the job refuses
/// a bank steeper than <see cref="MaxVariation"/> instead of digging a crater, and it needs a few passes on a gentle
/// slope.</para>
/// </summary>
internal static class MHR_Level
{
	/// <summary>How close to a point it stops and works (metres).</summary>
	internal const float Reach = 1.8f;

	/// <summary>How long between two swings (the companion turns, swings, the operation lands, the next point).</summary>
	internal const float SwingSeconds = 0.45f;

	/// <summary>Longest the whole job may take (time not driven - a fight, resting - does not count).</summary>
	internal const float Timeout = 900f;

	/// <summary>Above this height variation the soft level cannot win - the order refuses instead of digging.</summary>
	internal const float MaxVariation = 1.2f;

	/// <summary>How many times the whole area is worked before the job gives up.</summary>
	internal const int MaxPasses = 10;

	private const string LogTag = "[Level] ";

	private enum Phase
	{
		Prepare,
		GoTo,
		Work,
		Done
	}

	private sealed class State
	{
		internal bool Active;
		internal float Start;
		internal float LastDrive;
		internal int StartMode;
		internal Vector3 Center;
		internal float Size;
		internal float Target;
		internal int Pass;
		internal int Index;
		internal List<Vector3> Points;
		internal Phase Phase;
		internal float WorkUntil;
		internal float WorkAt;
	}

	private static readonly Dictionary<int, State> s_state = new Dictionary<int, State>();

	/// <summary>Starts a leveling job over a square area centred on <paramref name="center"/>.</summary>
	internal static bool Start(CompanionSetup cs, Vector3 center, float size)
	{
		if (cs == null)
		{
			return false;
		}
		CompanionAI ai = cs.GetComponent<CompanionAI>();
		if (ai == null)
		{
			return false;
		}
		State state = new State
		{
			Active = true,
			Start = Time.time,
			LastDrive = Time.time,
			StartMode = ModeOf(cs),
			Center = center,
			Size = Mathf.Clamp(size, 2f, 20f),
			Phase = Phase.Prepare
		};
		s_state[ai.GetInstanceID()] = state;
		Log(ai, "order: level " + state.Size.ToString("F1") + " m around (" + F(center.x) + ", " + F(center.y) + ", " + F(center.z) + ")");
		return true;
	}

	internal static bool Active(Component c)
	{
		if (c == null)
		{
			return false;
		}
		State state;
		return s_state.TryGetValue(c.GetInstanceID(), out state) && state != null && state.Active;
	}

	internal static void Cancel(Component c)
	{
		if (c == null)
		{
			return;
		}
		State state;
		if (s_state.TryGetValue(c.GetInstanceID(), out state) && state != null && state.Active)
		{
			state.Active = false;
			s_state.Remove(c.GetInstanceID());
			CompanionAI ai = c.GetComponent<CompanionAI>();
			if (ai != null)
			{
				MHR_Mover.Release(ai);
			}
		}
	}

	internal static void CancelAll()
	{
		List<int> keys = new List<int>(s_state.Keys);
		foreach (int key in keys)
		{
			State state;
			if (s_state.TryGetValue(key, out state))
			{
				state.Active = false;
			}
			s_state.Remove(key);
		}
	}

	/// <summary>Driven from <c>CompanionAI.UpdateAI</c>: true while the job is running.</summary>
	internal static bool Drive(CompanionAI ai, float dt)
	{
		if (ai == null)
		{
			return false;
		}
		State state;
		if (!s_state.TryGetValue(ai.GetInstanceID(), out state) || state == null || !state.Active)
		{
			return false;
		}
		if (Time.time - state.LastDrive > 0.5f)
		{
			state.Start += Time.time - state.LastDrive;
		}
		state.LastDrive = Time.time;
		CompanionSetup cs = ai.GetComponent<CompanionSetup>();
		Humanoid humanoid = ai.GetComponent<Humanoid>();
		ZNetView nv = ai.GetComponent<ZNetView>();
		Character self = ai.GetComponent<Character>();
		string why = null;
		if (cs == null || humanoid == null || nv == null)
		{
			why = "no body";
		}
		else if (self != null && self.IsDead())
		{
			why = "it's dead";
		}
		else if (nv.IsValid() && !nv.IsOwner())
		{
			why = "another player's game runs it now";
		}
		else if (ModeOf(cs) != state.StartMode && MHR_JobRules.IsJobMode(ModeOf(cs)))
		{
			why = "another job was picked";
		}
		else if (Time.time - state.Start > Timeout)
		{
			why = "the order took too long";
		}
		if (why != null)
		{
			Finish(ai, state, "hc_ai_level_failed", why);
			return false;
		}
		switch (state.Phase)
		{
			case Phase.Prepare:
				return Prepare(ai, state, humanoid);
			case Phase.GoTo:
				return GoTo(ai, state, dt);
			case Phase.Work:
				return Work(ai, state);
			default:
				Finish(ai, state, null, "done");
				return false;
		}
	}

	/// <summary>Reads the ground, the hoe and the target height once, then builds the grid of points to work.</summary>
	private static bool Prepare(CompanionAI ai, State state, Humanoid humanoid)
	{
		Inventory bag = humanoid != null ? humanoid.GetInventory() : null;
		if (bag == null || !bag.HaveItem("$item_hoe"))
		{
			Log(ai, "no hoe in the bag");
			Finish(ai, state, "hc_ai_build_no_hoe", "no hoe", "0");
			return false;
		}
		float tolerance = ModConfig.BuildLevelTolerance != null ? ModConfig.BuildLevelTolerance.Value : BuildTerrain.DefaultTolerance;
		float min;
		float max;
		float target;
		if (BuildTerrain.FlatEnough(state.Center, state.Size, tolerance, out min, out max, out target))
		{
			Log(ai, "already flat (" + (max - min).ToString("F2") + " m)");
			Finish(ai, state, "hc_ai_level_already", null, (max - min).ToString("F1"));
			return false;
		}
		float variation = max - min;
		if (variation > MaxVariation)
		{
			Log(ai, "too steep (" + variation.ToString("F2") + " m)");
			Finish(ai, state, "hc_ai_build_too_steep", "too steep", variation.ToString("F1"));
			return false;
		}
		state.Target = target;
		state.Points = Grid(state.Center, state.Size, BuildTerrain.Radius());
		state.Pass = 1;
		state.Index = 0;
		state.Phase = Phase.GoTo;
		Log(ai, "variation " + variation.ToString("F2") + " m -> target " + target.ToString("F1") + ", " + state.Points.Count + " point(s) per pass");
		return true;
	}

	/// <summary>The points of one pass, in a snake order (the way a player works the ground: back and forth).</summary>
	private static List<Vector3> Grid(Vector3 center, float size, float radius)
	{
		List<Vector3> points = new List<Vector3>();
		float spacing = Mathf.Max(1.5f, radius);
		int steps = Mathf.Clamp(Mathf.CeilToInt(size / spacing), 1, 10);
		float cell = size / steps;
		for (int i = 0; i < steps; i++)
		{
			for (int k = 0; k < steps; k++)
			{
				int j = (i % 2 == 0) ? k : (steps - 1 - k);
				float x = center.x - size * 0.5f + (i + 0.5f) * cell;
				float z = center.z - size * 0.5f + (j + 0.5f) * cell;
				float y = ZoneSystem.instance != null ? ZoneSystem.instance.GetGroundHeight(new Vector3(x, center.y, z)) : center.y;
				points.Add(new Vector3(x, y, z));
			}
		}
		return points;
	}

	/// <summary>
	/// The companion works the area from ONE spot: the operation is placed at the point (up to ~5 m away, the game's
	/// own placement range), so it does not have to stand on each one. Walking to every point of an 8x8 area made the
	/// mover give up on the ones behind a slope or a piece - the player saw it shaking in place ("can't reach point
	/// N", 01/11/2026).
	/// </summary>
	private static bool GoTo(CompanionAI ai, State state, float dt)
	{
		Vector3 me = ai.transform.position;
		float flat = new Vector2(me.x - state.Center.x, me.z - state.Center.z).magnitude;
		if (flat <= 3f && Mathf.Abs(me.y - state.Center.y) <= 3f)
		{
			ai.StopMoving();
			MHR_Mover.Release(ai);
			state.Phase = Phase.Work;
			state.WorkAt = Time.time;
			state.WorkUntil = Time.time;
			return true;
		}
		MHR_Mover.Go(ai, dt, state.Center, 3f, true, "Level");
		if (MHR_Mover.GaveUp(ai))
		{
			// work from where it stands instead of freezing
			Log(ai, "can't get to the middle of the area - working from here");
			MHR_Mover.Release(ai);
			state.Phase = Phase.Work;
			state.WorkAt = Time.time;
			state.WorkUntil = Time.time;
		}
		return true;
	}

	/// <summary>The swing: face the point, play the hoe animation and let the operation land - then the next point.</summary>
	private static bool Work(CompanionAI ai, State state)
	{
		if (state.Points == null || state.Index >= state.Points.Count)
		{
			return NextPass(ai, state);
		}
		if (Time.time < state.WorkUntil)
		{
			return true;
		}
		Vector3 point = state.Points[state.Index];
		Humanoid humanoid = ai.GetComponent<Humanoid>();
		if (humanoid != null && Time.time - state.WorkAt < 0.15f)
		{
			Vector3 look = point - ai.transform.position;
			look.y = 0f;
			if (look.sqrMagnitude > 0.01f)
			{
				ai.transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
			}
			ZSyncAnimation zanim = humanoid.GetComponent<ZSyncAnimation>();
			string animation = BuildTerrain.HoeAnimation(humanoid.GetInventory());
			if (zanim != null && !string.IsNullOrEmpty(animation))
			{
				zanim.SetTrigger(animation);
			}
		}
		if (BuildTerrain.ApplyOne(point, state.Target))
		{
			LogDebug(ai, "worked point " + (state.Index + 1) + "/" + state.Points.Count + " of pass " + state.Pass);
		}
		state.Index++;
		state.WorkAt = Time.time;
		state.WorkUntil = Time.time + SwingSeconds;
		if (state.Index >= state.Points.Count)
		{
			return NextPass(ai, state);
		}
		return true;
	}

	/// <summary>End of a pass: is the ground flat enough? If not, another pass over the same area.</summary>
	private static bool NextPass(CompanionAI ai, State state)
	{
		float tolerance = ModConfig.BuildLevelTolerance != null ? ModConfig.BuildLevelTolerance.Value : BuildTerrain.DefaultTolerance;
		float min;
		float max;
		float target;
		bool flat = BuildTerrain.FlatEnough(state.Center, state.Size, tolerance, out min, out max, out target);
		Log(ai, "pass " + state.Pass + " done: " + (max - min).ToString("F2") + " m of variation" + (flat ? " (flat enough)" : string.Empty));
		if (flat)
		{
			Finish(ai, state, "hc_ai_level_done", null, (max - min).ToString("F1"));
			return false;
		}
		if (state.Pass >= MaxPasses)
		{
			Finish(ai, state, "hc_ai_level_failed", "still uneven", (max - min).ToString("F1"));
			return false;
		}
		state.Pass++;
		state.Index = 0;
		state.Points = Grid(state.Center, state.Size, BuildTerrain.Radius());
		state.Phase = Phase.GoTo;
		return true;
	}

	private static void Finish(CompanionAI ai, State state, string key, string why, params object[] args)
	{
		state.Active = false;
		s_state.Remove(ai.GetInstanceID());
		MHR_Mover.Release(ai);
		Log(ai, "order ended: " + (key ?? "(silent)") + (string.IsNullOrEmpty(why) ? string.Empty : " (" + why + ")"));
		if (key != null)
		{
			Say(ai, key, args);
		}
	}

	private static int ModeOf(CompanionSetup cs)
	{
		ZNetView nv = cs != null ? cs.GetComponent<ZNetView>() : null;
		ZDO zdo = nv != null ? nv.GetZDO() : null;
		return zdo != null ? zdo.GetInt(CompanionSetup.ActionModeHash, 0) : 0;
	}

	private static string F(float value)
	{
		return value.ToString("F1");
	}

	private static void Say(CompanionAI ai, string key, params object[] args)
	{
		try
		{
			CompanionTalk talk = ai != null ? ai.GetComponent<CompanionTalk>() : null;
			if (talk == null)
			{
				return;
			}
			string text = ModLocalization.Loc(key);
			if (args != null && args.Length > 0)
			{
				try
				{
					text = string.Format(text, args);
				}
				catch (FormatException)
				{
					// a translation with the wrong placeholder count must not break the order
				}
			}
			talk.Say(text, true);
		}
		catch (Exception ex)
		{
			LogDebug(ai, "couldn't speak: " + ex.Message);
		}
	}

	private static string Who(CompanionAI ai)
	{
		ZDO zdo = ai != null ? ai.GetComponent<ZNetView>()?.GetZDO() : null;
		return (zdo != null ? zdo.GetString(CompanionSetup.NameHash, "Companion") : "Companion") + ": ";
	}

	private static void Log(CompanionAI ai, string message)
	{
		if (CompanionsPlugin.Log != null)
		{
			CompanionsPlugin.Log.LogInfo(LogTag + Who(ai) + message);
		}
	}

	private static void LogDebug(CompanionAI ai, string message)
	{
		if (CompanionsPlugin.Log != null)
		{
			CompanionsPlugin.Log.LogDebug(LogTag + Who(ai) + message);
		}
	}
}
