using System;
using System.Collections.Generic;
using UnityEngine;

namespace Companions;

/// <summary>
/// Fase E3: the <b>build job</b> - the state machine behind "Maria, construa uma bancada de trabalho aqui". It is
/// the only part that ties the four pieces together:
///
/// <code>
/// Find    BuildSite.Find      -> a pose that passes the game's rules (or "no site")
/// Goto    MHR_Mover           -> walk to it
/// Place   BuildExecutor       -> pay from its bag, place, verify the object
/// Verify  BuildExecutor.Verify-> the station is in the game's registry (next frame)
/// Done    CompanionTalk.Say   -> one line, always
/// </code>
///
/// <para>Rules of the house, the same ones <see cref="MHR_Collect"/> follows: the state lives in memory (a build
/// order does not survive a relog), a fight in between does not use up the order's time, there is a timeout, and
/// every ending speaks. While it runs, <c>MHR_JobRules.OtherOrder</c> answers true, so the other jobs hold still -
/// they all steer through the same <c>MHR_Mover</c>.</para>
///
/// <para><b>What E3 does not do (by plan):</b> gather the missing materials (E4) or build structures/blueprints
/// (E6-E8). It builds from its own bag only, and only what <see cref="BuildCatalog"/> allows.</para>
/// </summary>
internal static class MHR_Build
{
	/// <summary>Longest an order may take (walking + placing). Time not driven (a fight, resting) does not count.</summary>
	internal const float Timeout = 60f;

	/// <summary>How close to the spot it stops and places (horizontal).</summary>
	internal const float Reach = 2.5f;

	/// <summary>
	/// How far from the companion's <b>eye</b> a piece may be placed. It is the game's own
	/// <c>Player.m_maxPlaceDistance</c> (5 m, decompiled 1.0.20): the old rule (2,5 m flat and 3 m up) could not
	/// reach the upper roof row of the house (y = 5, i.e. 4,9 m above the feet) - the companion walked in circles
	/// until the order timed out (player report, 01/11/2026: "parece que não alcança").
	/// </summary>
	internal const float MaxPlace = 5f;

	/// <summary>The companion's eye height above its feet (the game measures the placement distance from the eye).</summary>
	private const float EyeHeight = 1.5f;

	/// <summary>E10: how many leveling passes a job may apply before it gives up (each moves the ground by at most 1 m).</summary>
	internal const int MaxLevelPasses = 4;
	/// <summary>E10: above this height variation (metres) the soft level cannot win - the spot is refused instead of digging a crater.</summary>
	internal const float MaxLevelVariation = 1.8f;

	/// <summary>E4b: how long a pending build waits for the gather before it is dropped.</summary>
	internal const float GatherTimeout = 300f;

	private const string LogTag = "[Build] ";

	private enum Phase
	{
		/// <summary>E10: leveling the ground before a plan (the house needs a flat 8x8).</summary>
		Level,
		Find,
		Goto,
		Place,
		/// <summary>E4b: walking to the resource BEFORE the gather mode is set (only the build steers here).</summary>
		GatherWalk,
		Verify,
		Done
	}

	private sealed class State
	{
		internal bool Active;
		internal float Start;
		internal float LastDrive;
		internal int StartMode;
		internal string Prefab;
		internal Piece Piece;
		internal Vector3 Hint;
		internal Vector3 Forward;
		internal Vector3 Pos;
		internal Quaternion Rot;
		internal Phase Phase;
		internal int Tried;
		internal GameObject Placed;
		/// <summary>E6: when the piece was placed (the support verdict is asked again for a moment).</summary>
		internal float PlacedAt;
		internal Vector3 GatherTarget;
		internal int GatherMode;
		internal string GatherItem;
		internal int GatherAmount;
		/// <summary>E6: the template being built (null for a single-piece order).</summary>
		internal BuildTemplates.Template Template;
		/// <summary>E6: the resolved poses of the plan, in build order.</summary>
		internal List<PlannedStep> Plan;
		/// <summary>E6: which step is being built (0-based).</summary>
		internal int Index;
		/// <summary>E6b: resume after a gather at the SAME anchor and from the piece where it stopped.</summary>
		internal bool AnchorKnown;
		internal Vector3 Anchor;
		internal Quaternion AnchorRot;
		internal int StartIndex;
	}

	/// <summary>E6: one piece of a plan, already resolved to a world pose.</summary>
	private sealed class PlannedStep
	{
		internal string Prefab;
		internal Piece Piece;
		internal Vector3 Pos;
		internal Quaternion Rot;
	}

	private static readonly Dictionary<int, State> s_state = new Dictionary<int, State>();

	/// <summary>
	/// E4b: a build order waiting for its materials. The companion is gathering with the NORMAL gather order (the
	/// harvest owns the movement - one owner at a time, the lesson of 01/11/2026); this record only watches the bag
	/// and, when the piece is covered, starts a fresh build order (which re-finds the spot and walks back).
	/// </summary>
	private sealed class Pending
	{
		internal string Prefab;
		/// <summary>E6b: the template of the plan waiting for its materials (null for a single piece).</summary>
		internal string Template;
		internal Vector3 Hint;
		internal int Mode;
		internal string Item;
		/// <summary>E6b: the shortfall that was handed over (the watcher resumes when the bag covers it).</summary>
		internal int Amount;
		/// <summary>E6b: where the plan stopped - the watcher resumes at the SAME anchor and from this piece.</summary>
		internal int Index;
		internal Vector3 Anchor;
		internal Quaternion AnchorRot;
		/// <summary>E6b: the last count written to the log (the watcher is otherwise silent while it waits).</summary>
		internal int LastLogged = -1;
		internal float Start;
	}

	private static readonly Dictionary<int, Pending> s_pending = new Dictionary<int, Pending>();

	/// <summary>Starts an order. <paramref name="prefab"/> must be in <see cref="BuildCatalog"/>; the caller has
	/// already checked the config and the recipe.</summary>
	internal static bool Start(CompanionSetup cs, string prefab, Vector3 hint)
	{
		if (cs == null || string.IsNullOrEmpty(prefab))
		{
			return false;
		}
		Piece piece = BuildProbe.PieceOf(prefab);
		if (piece == null)
		{
			return false;
		}
		CompanionAI ai = cs.GetComponent<CompanionAI>();
		if (ai == null)
		{
			return false;
		}
		Player owner = MHR_Owner.Of(cs);
		Vector3 forward = owner != null ? owner.transform.forward : cs.transform.forward;
		State state = new State
		{
			Active = true,
			Start = Time.time,
			LastDrive = Time.time,
			StartMode = ModeOf(cs),
			Prefab = prefab,
			Piece = piece,
			Hint = hint,
			Forward = forward,
			Phase = Phase.Find
		};
		s_state[ai.GetInstanceID()] = state;
		Log(ai, "order: build " + prefab + " near (" + F(hint.x) + ", " + F(hint.y) + ", " + F(hint.z) + ")");
		return true;
	}

	/// <summary>
	/// E6: starts a <b>plan</b> - a template (several pieces, foundation first). The caller has checked the config;
	/// the material of the whole plan is checked in <see cref="FindPlan"/>, before anything is paid.
	/// </summary>
	internal static bool StartPlan(CompanionSetup cs, BuildTemplates.Template template, Vector3 hint)
	{
		return StartPlan(cs, template, hint, 0, Vector3.zero, Quaternion.identity, false);
	}

	/// <summary>E6b: the same plan, resumed at the SAME anchor and from <paramref name="startIndex"/> - what the watcher does after the gather (otherwise it started a second house next to the first).</summary>
	internal static bool StartPlan(CompanionSetup cs, BuildTemplates.Template template, Vector3 hint, int startIndex, Vector3 anchor, Quaternion anchorRot, bool anchorKnown)
	{
		if (cs == null || template == null)
		{
			return false;
		}
		CompanionAI ai = cs.GetComponent<CompanionAI>();
		if (ai == null)
		{
			return false;
		}
		Player owner = MHR_Owner.Of(cs);
		Vector3 forward = owner != null ? owner.transform.forward : cs.transform.forward;
		State state = new State
		{
			Active = true,
			Start = Time.time,
			LastDrive = Time.time,
			StartMode = ModeOf(cs),
			Template = template,
			Prefab = template.Name,
			Hint = hint,
			Forward = forward,
			Plan = new List<PlannedStep>(),
			Phase = anchorKnown ? Phase.Find : Phase.Level,
			StartIndex = startIndex,
			Anchor = anchor,
			AnchorRot = anchorRot,
			AnchorKnown = anchorKnown
		};
		s_state[ai.GetInstanceID()] = state;
		Log(ai, "order: build plan " + template.Name + " (" + template.Steps.Length + " pieces) near (" + F(hint.x) + ", " + F(hint.y) + ", " + F(hint.z) + ")");
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
			s_pending.Remove(c.GetInstanceID());
			CompanionAI ai = c.GetComponent<CompanionAI>();
			if (ai != null)
			{
				MHR_Mover.Release(ai);
			}
		}
	}

	internal static void CancelAll()
	{
		s_pending.Clear();
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

	/// <summary>Driven from <c>CompanionAI.UpdateAI</c> before <see cref="MHR_Collect"/> and <see cref="MHR_Loot"/>:
	/// true while the order is running.</summary>
	internal static bool Drive(CompanionAI ai, float dt)
	{
		if (ai == null)
		{
			return false;
		}
		State state;
		if (!s_state.TryGetValue(ai.GetInstanceID(), out state) || state == null || !state.Active)
		{
			// E4b: nothing to drive, but a build may be waiting for the gather to finish
			CheckPending(ai);
			return false;
		}
		// a fight in between (or anything else that kept UpdateAI from driving us) does not use up the order's time
		if (Time.time - state.LastDrive > 0.5f)
		{
			state.Start += Time.time - state.LastDrive;
			LogDebug(ai, "back to the build");
		}
		state.LastDrive = Time.time;

		CompanionSetup cs = ai.GetComponent<CompanionSetup>();
		Humanoid humanoid = ai.GetComponent<Humanoid>();
		Inventory bag = humanoid != null ? humanoid.GetInventory() : null;
		ZNetView nv = ai.GetComponent<ZNetView>();
		ZDO zdo = nv != null ? nv.GetZDO() : null;
		Character self = ai.GetComponent<Character>();
		string why = null;
		if (cs == null || humanoid == null || bag == null || zdo == null || self == null)
		{
			why = "no pack";
		}
		else if (self.IsDead())
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
		else if (state.Phase != Phase.GatherWalk && Time.time - state.Start > Timeout)
		{
			why = "the order took too long";
		}
		// E6 (measured 01/11/2026): an encumbered companion cannot walk - the game's Encumbered effect slows it to a
		// crawl and MHR_Mover gives up after 8 s without progress, which used to end the order with "no site" (a lie).
		if (why == null && humanoid != null && CompanionCarry.Fraction(humanoid) > 1f)
		{
			Log(ai, "overweight: " + humanoid.GetInventory().GetTotalWeight().ToString("F0") + "/" + CompanionCarry.Max(humanoid).ToString("F0"));
			Finish(ai, state, "hc_ai_build_too_heavy", "encumbered", humanoid.GetInventory().GetTotalWeight().ToString("F0"), CompanionCarry.Max(humanoid).ToString("F0"));
			return false;
		}
		if (why != null)
		{
			Finish(ai, state, "hc_ai_build_failed", why);
			return false;
		}

		switch (state.Phase)
		{
			case Phase.Level:
				return LevelGround(ai, state);
			case Phase.Find:
				return state.Template != null ? FindPlan(ai, state) : Find(ai, state);
			case Phase.Goto:
				return Goto(ai, state, dt);
			case Phase.Place:
				return Place(ai, state, cs, bag);
			case Phase.GatherWalk:
				return GatherWalk(ai, state, cs, dt);
			case Phase.Verify:
				return Verify(ai, state, cs);
			default:
				Finish(ai, state, null, "done");
				return false;
		}
	}

	/// <summary>
	/// E10: the ground of a plan is leveled <b>before</b> the first piece when it is uneven (a house needs a flat 8x8:
	/// the walls and the roof are all at the anchor's height). It uses the game's own leveling - the hoe's level piece,
	/// the same operation a player clicking the hoe applies - and needs a <b>hoe</b> in the bag, which is the point of
	/// the whole thing: the companion works the ground like a player would.
	///
	/// <para>The game's "level ground" is a <b>soft</b> level (it pulls the terrain toward the operation's height with a
	/// falloff, at most 1 m per operation and ~0,35 m per pass on real ground), so the spot is chosen as the
	/// <b>flattest</b> one around the hint, the passes are spaced by 1,5 s (the heightmap settles in ~0,5 s) and the
	/// job gives up when a pass stops improving.</para>
	/// </summary>
	private static bool LevelGround(CompanionAI ai, State state)
	{
		state.Phase = Phase.Find;
		if (state.Template == null || ModConfig.BuildLevelGround == null || !ModConfig.BuildLevelGround.Value)
		{
			return true;
		}
		float size = FootprintSize(state.Template);
		float tolerance = ModConfig.BuildLevelTolerance != null ? ModConfig.BuildLevelTolerance.Value : BuildTerrain.DefaultTolerance;
		// the flattest spot around the hint: a hillside has one corner that is much better than the rest, and the soft
		// level cannot win a 3 m bank anyway
		Vector3 best = state.Hint;
		float bestVariation = float.MaxValue;
		float bestTarget = state.Hint.y;
		for (int i = 0; i < 9; i++)
		{
			Vector3 candidate = state.Hint;
			if (i > 0)
			{
				float angle = Mathf.PI * 2f * (i - 1) / 8f;
				candidate = state.Hint + new Vector3(Mathf.Cos(angle) * 4f, 0f, Mathf.Sin(angle) * 4f);
				candidate.y = ZoneSystem.instance != null ? ZoneSystem.instance.GetGroundHeight(candidate) : state.Hint.y;
			}
			float min;
			float max;
			float target;
			BuildTerrain.FlatEnough(candidate, size, tolerance, out min, out max, out target);
			float variation = max - min;
			if (variation < bestVariation)
			{
				bestVariation = variation;
				best = candidate;
				bestTarget = target;
			}
		}
		state.Hint = best;
		float size2 = size;
		float min2;
		float max2;
		float target2;
		if (BuildTerrain.FlatEnough(best, size2, tolerance, out min2, out max2, out target2))
		{
			Log(ai, "ground is flat enough (" + (max2 - min2).ToString("F2") + " m of variation in " + size.ToString("F1") + " m)");
			return true;
		}
		Log(ai, "flattest spot around the hint: " + bestVariation.ToString("F2") + " m of variation (target " + bestTarget.ToString("F1") + ")");
		if (bestVariation > MaxLevelVariation)
		{
			Log(ai, "too steep to level (" + bestVariation.ToString("F2") + " m)");
			Finish(ai, state, "hc_ai_build_too_steep", "too steep", bestVariation.ToString("F1"));
			return false;
		}
		Humanoid humanoid = ai.GetComponent<Humanoid>();
		Inventory bag = humanoid != null ? humanoid.GetInventory() : null;
		if (bag == null || !bag.HaveItem("$item_hoe"))
		{
			Log(ai, "the ground is uneven (" + bestVariation.ToString("F2") + " m) and there is no hoe");
			Finish(ai, state, "hc_ai_build_no_hoe", "no hoe", bestVariation.ToString("F1"));
			return false;
		}
		if (!BuildTerrain.Available())
		{
			Log(ai, "the hoe's level piece was not found in this game version");
			Finish(ai, state, "hc_ai_build_level_failed", "no level piece", bestVariation.ToString("F1"));
			return false;
		}
		string animation = BuildTerrain.HoeAnimation(bag);
		ZSyncAnimation zanim = humanoid != null ? humanoid.GetComponent<ZSyncAnimation>() : null;
		if (zanim != null && !string.IsNullOrEmpty(animation))
		{
			zanim.SetTrigger(animation);
		}
		// the companion works the ground with the HOE IN HAND, point by point (MHR_Level) - not 16 operations in one
		// frame: the player wants to SEE the work (request of 01/11/2026), and the house resumes by itself when the
		// ground is ready (the same handover the E4b gather uses - one owner of the work at a time)
		CompanionSetup self = ai.GetComponent<CompanionSetup>();
		if (self == null || !MHR_Level.Start(self, best, size, state.Template.Name))
		{
			Finish(ai, state, "hc_ai_build_level_failed", "could not start the leveling", bestVariation.ToString("F1"));
			return false;
		}
		Log(ai, "leveling first, with the hoe (" + size.ToString("F1") + " m, " + bestVariation.ToString("F2") + " m of variation)");
		Finish(ai, state, "hc_ai_build_leveling", "leveling first", size.ToString("F1"));
		return false;
	}
	/// <summary>The side of the square the template needs, with a margin for the walls' thickness.</summary>
	private static float FootprintSize(BuildTemplates.Template template)
	{
		if (template == null)
		{
			return 4f;
		}
		float minX;
		float maxX;
		float minZ;
		float maxZ;
		template.Footprint(out minX, out maxX, out minZ, out maxZ);
		return Mathf.Max(maxX - minX, maxZ - minZ) + 1.5f;
	}

	/// <summary>
	/// E6: resolves a template into world poses. The first step (a floor) is found with the normal site search around
	/// the hint; every other step is the anchor plus its offset (turned by the anchor's yaw) and goes through the
	/// <b>same validator</b> - with a small nudge when the ground is uneven. A single piece that cannot be placed stops
	/// the plan <b>before</b> anything is paid.
	/// </summary>
	private static bool FindPlan(CompanionAI ai, State state)
	{
		List<BuildTemplates.Step> steps = state.Template.Ordered();
		if (steps.Count == 0)
		{
			Finish(ai, state, "hc_ai_build_failed", "empty plan", state.Template.Label);
			return false;
		}
		Piece anchorPiece = BuildProbe.PieceOf(steps[0].Prefab);
		if (anchorPiece == null)
		{
			Finish(ai, state, "hc_ai_build_failed", "unknown prefab " + steps[0].Prefab, state.Template.Label);
			return false;
		}
		float radius = ModConfig.BuildSiteSearchRadius != null ? ModConfig.BuildSiteSearchRadius.Value : 8f;
		Vector3 anchor;
		Quaternion baseRot;
		string anchorStatus;
		int anchorTried;
		if (state.AnchorKnown)
		{
			// E6b: resuming after a gather - the SAME anchor and the SAME poses, so the plan continues where it stopped
			// (searching again could pick another spot and start a second house next to the first one)
			anchor = state.Anchor;
			baseRot = state.AnchorRot;
			Log(ai, "resuming at the same anchor (" + F(anchor.x) + ", " + F(anchor.y) + ", " + F(anchor.z) + ") from piece " + (state.StartIndex + 1));
		}
		else if (!FindAnchor(steps, state.Hint, state.Forward, radius, Self(ai), out anchor, out baseRot, out anchorStatus, out anchorTried))
		{
			Log(ai, "plan: no anchor - " + anchorTried + " poses tried, last " + anchorStatus);
			Piece firstPiece = BuildProbe.PieceOf(steps[0].Prefab);
			if (firstPiece != null && firstPiece.m_craftingStation != null && anchorStatus == "needs_station")
			{
				string station = Localization.instance != null ? Localization.instance.Localize(firstPiece.m_craftingStation.m_name) : firstPiece.m_craftingStation.m_name;
				Finish(ai, state, "hc_ai_build_needs_station", "no candidate had the station", station);
				return false;
			}
			Finish(ai, state, "hc_ai_build_no_site", anchorStatus, state.Template.Label);
			return false;
		}
		List<PlannedStep> plan = new List<PlannedStep>();
		string failure = null;
		string failureStatus = null;
		int checkedPoses = 0;
		foreach (BuildTemplates.Step step in steps)
		{
			Piece piece = BuildProbe.PieceOf(step.Prefab);
			if (piece == null)
			{
				failure = step.Prefab;
				failureStatus = "unknown prefab";
				break;
			}
			Vector3 pos = anchor + baseRot * new Vector3(step.X, step.Y, step.Z);
			Quaternion rot = baseRot * Quaternion.Euler(0f, step.Yaw, 0f);
			if (plan.Count > 0)
			{
				// the anchor was found by the search; every other piece has to pass the validator at its pose. The
				// SUPPORT of a piece above the ground is not knowable here: it comes from the pieces of the plan, which
				// are not placed yet - it is checked again at placement time, when they exist (E6, measured 01/11/2026:
				// the roof was refused with no_support because the walls it stands on were still only planned).
				checkedPoses++;
				Dictionary<string, object> check = BuildSite.Check(piece, pos, rot, Self(ai));
				if (!OkForPlan(check, step.Level > 0))
				{
					bool nudged = false;
					foreach (Vector3 nudge in Nudges())
					{
						Dictionary<string, object> again = BuildSite.Check(piece, pos + nudge, rot, Self(ai));
						checkedPoses++;
						if (OkForPlan(again, step.Level > 0))
						{
							pos += nudge;
							nudged = true;
							break;
						}
					}
					if (!nudged)
					{
						failure = step.Prefab;
						failureStatus = Convert.ToString(check["status"]);
						break;
					}
				}
			}
			plan.Add(new PlannedStep { Prefab = step.Prefab, Piece = piece, Pos = pos, Rot = rot });
		}
		if (failure != null)
		{
			Log(ai, "plan: " + failure + " refused (" + failureStatus + ") - nothing was paid");
			Finish(ai, state, "hc_ai_build_plan_no_site", failureStatus, state.Template.Label, failure);
			return false;
		}
		// E6b (player's design, 01/11/2026): the plan starts with what the bag has and pays piece by piece (bag, then the
		// chests). When a piece is short the build hands the order to the gather and resumes by itself - the same E4b
		// handover the single pieces use. Requiring the whole cost up front made no sense for a big structure.
		Humanoid humanoid = ai.GetComponent<Humanoid>();
		Inventory bag = humanoid != null ? humanoid.GetInventory() : null;
		bool useChests = ModConfig.BuildUseChests == null || ModConfig.BuildUseChests.Value;
		Dictionary<string, int> missing = PlanMissing(bag, plan, anchor, ai, useChests);
		if (missing.Count > 0)
		{
			Log(ai, "plan short at the start: " + MissingText(missing) + " - the gather takes over when the first piece needs it");
		}
		state.Plan = plan;
		state.Anchor = anchor;
		state.AnchorRot = baseRot;
		state.AnchorKnown = true;
		state.Index = Mathf.Clamp(state.StartIndex, 0, plan.Count - 1);
		state.Piece = plan[state.Index].Piece;
		state.Pos = plan[state.Index].Pos;
		state.Rot = plan[state.Index].Rot;
		state.Phase = Phase.Goto;
		Log(ai, "plan " + state.Template.Name + ": " + plan.Count + " pieces, " + checkedPoses + " poses checked, anchor at ("
			+ F(anchor.x) + ", " + F(anchor.y) + ", " + F(anchor.z) + ")");
		return true;
	}

	/// <summary>
	/// E6: the anchor of a plan - the spot that accepts <b>every piece of the template</b>, not just the first one. It
	/// is the same ring search the single-piece order uses, but each candidate is the whole structure: measured
	/// 01/11/2026, searching only the first piece put the bench shelter 11 m away from the bench.
	/// </summary>
	private static bool FindAnchor(List<BuildTemplates.Step> steps, Vector3 hint, Vector3 forward, float radius, Component ignore,
		out Vector3 anchor, out Quaternion rot, out string status, out int tried)
	{
		anchor = hint;
		rot = Quaternion.LookRotation(Flat(forward), Vector3.up);
		status = "no_candidate";
		tried = 0;
		int rings = 3;
		int dirs = 8;
		List<Vector3> candidates = new List<Vector3> { hint };
		for (int ring = 0; ring < rings; ring++)
		{
			float distance = radius * (ring + 1) / rings;
			for (int dir = 0; dir < dirs; dir++)
			{
				float angle = Mathf.PI * 2f * dir / dirs;
				candidates.Add(hint + new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance));
			}
		}
		foreach (Vector3 candidate in candidates)
		{
			float y = ZoneSystem.instance != null ? ZoneSystem.instance.GetGroundHeight(candidate) : candidate.y;
			Vector3 spot = new Vector3(candidate.x, y, candidate.z);
			bool all = true;
			// a big template (the house has 54 pieces) would cost 25 x 54 ghost tests: the search samples the
			// FOUNDATION (up to 5 pieces, spread through the list) and FindPlan validates the whole plan once, at the
			// anchor that won
			foreach (BuildTemplates.Step step in Sample(steps))
			{
				Piece piece = BuildProbe.PieceOf(step.Prefab);
				if (piece == null)
				{
					all = false;
					status = "unknown_prefab";
					break;
				}
				Vector3 pos = spot + rot * new Vector3(step.X, step.Y, step.Z);
				Quaternion pieceRot = rot * Quaternion.Euler(0f, step.Yaw, 0f);
				tried++;
				Dictionary<string, object> check = BuildSite.Check(piece, pos, pieceRot, ignore);
				if (!OkForPlan(check, step.Level > 0))
				{
					all = false;
					status = check != null ? Convert.ToString(check["status"]) : "unknown";
					break;
				}
			}
			if (all)
			{
				anchor = spot;
				status = "valid";
				return true;
			}
		}
		return false;
	}

	/// <summary>Up to five foundation pieces spread through the template (the sample the anchor search validates).</summary>
	private static List<BuildTemplates.Step> Sample(List<BuildTemplates.Step> steps)
	{
		List<BuildTemplates.Step> ground = new List<BuildTemplates.Step>();
		foreach (BuildTemplates.Step step in steps)
		{
			if (step.Level == 0)
			{
				ground.Add(step);
			}
		}
		if (ground.Count == 0)
		{
			ground.Add(steps[0]);
		}
		if (ground.Count <= 5)
		{
			return ground;
		}
		List<BuildTemplates.Step> sample = new List<BuildTemplates.Step>();
		int[] picks = { 0, ground.Count / 4, ground.Count / 2, ground.Count * 3 / 4, ground.Count - 1 };
		foreach (int pick in picks)
		{
			BuildTemplates.Step step = ground[pick];
			if (!sample.Contains(step))
			{
				sample.Add(step);
			}
		}
		return sample;
	}

	private static Vector3 Flat(Vector3 direction)
	{
		direction.y = 0f;
		return direction.sqrMagnitude < 0.01f ? Vector3.forward : direction.normalized;
	}

	private static bool Find(CompanionAI ai, State state)
	{
		// a piece that needs a station (a chest, a bed) is refused with its own line BEFORE the search: without the
		// station every candidate would fail with needs_station, and "no place for it here" would be a lie - the
		// player needs a workbench, not more room.
		if (state.Piece != null && state.Piece.m_craftingStation != null
			&& CraftingStation.HaveBuildStationInRange(state.Piece.m_craftingStation.m_name, state.Hint) == null)
		{
			string station = Localization.instance != null ? Localization.instance.Localize(state.Piece.m_craftingStation.m_name) : state.Piece.m_craftingStation.m_name;
			Log(ai, "needs a station: " + station + " is not in range of the spot");
			Finish(ai, state, "hc_ai_build_needs_station", "station out of range", station);
			return false;
		}
		float radius = ModConfig.BuildSiteSearchRadius != null ? ModConfig.BuildSiteSearchRadius.Value : 8f;
		BuildSiteResult site = BuildSite.Find(state.Piece, state.Hint, state.Forward, radius, 3, 8, 48, Self(ai));
		state.Tried = site.Tried;
		if (!site.Ok)
		{
			Log(ai, "no site: " + site.Tried + " tried, last " + site.Status + " " + TallyText(site.Tally));
			// the search may still end on needs_station when the station is near the player but not near the spot
			if (state.Piece != null && state.Piece.m_craftingStation != null && site.Tally.ContainsKey("needs_station"))
			{
				string station = Localization.instance != null ? Localization.instance.Localize(state.Piece.m_craftingStation.m_name) : state.Piece.m_craftingStation.m_name;
				Finish(ai, state, "hc_ai_build_needs_station", "no candidate had the station", station);
				return false;
			}
			Finish(ai, state, "hc_ai_build_no_site", null, LabelOf(state));
			return false;
		}
		state.Pos = site.Pos;
		state.Rot = site.Rot;
		state.Phase = Phase.Goto;
		Log(ai, "site at (" + F(site.Pos.x) + ", " + F(site.Pos.y) + ", " + F(site.Pos.z) + "), " + F(site.Distance) + " m from the hint, "
			+ site.Tried + " tried" + (site.Tally.Count > 0 ? " (" + TallyText(site.Tally) + ")" : string.Empty));
		return true;
	}

	private static bool Goto(CompanionAI ai, State state, float dt)
	{
		Vector3 me = ai.transform.position;
		float flat = new Vector2(me.x - state.Pos.x, me.z - state.Pos.z).magnitude;
		// the game places a piece up to 5 m from the EYE (Player.m_maxPlaceDistance); the companion's eye is ~1,5 m
		// above its feet, so a roof 4,9 m up is within reach even though the feet are 4,9 m below it
		Vector3 eye = new Vector3(me.x, me.y + EyeHeight, me.z);
		bool inReach = flat <= Reach + 0.5f && Vector3.Distance(eye, state.Pos) <= MaxPlace;
		if (inReach)
		{
			ai.StopMoving();
			MHR_Mover.Release(ai);
			state.Phase = Phase.Place;
			return true;
		}
		MHR_Mover.Go(ai, dt, state.Pos, Reach, true, "Build");
		if (MHR_Mover.GaveUp(ai))
		{
			// "no site" was a lie here: the spot is fine, the way to it is not (a wall, a tree, the furniture the player
			// built, or the companion being too heavy to walk) - measured 01/11/2026
			Log(ai, "can't get to the spot");
			Finish(ai, state, "hc_ai_build_cant_reach", null, LabelOf(state));
			return false;
		}
		return true;
	}

	private static bool Place(CompanionAI ai, State state, CompanionSetup cs, Inventory bag)
	{
		// E6: a plan step is checked AGAIN now, with the pieces the plan already built standing - the roof's support
		// comes from the walls, and up-front they were only planned (measured: the roof was refused with no_support)
		if (state.Template != null && state.Plan != null && state.Index > 0)
		{
			Dictionary<string, object> check = BuildSite.Check(state.Piece, state.Pos, state.Rot, cs);
			if (!Ok(check))
			{
				string status = check != null ? Convert.ToString(check["status"]) : "unknown";
				Log(ai, "piece " + state.Prefab + " refused now (" + status + ") - the plan stops");
				if (status == "needs_station" && state.Piece != null && state.Piece.m_craftingStation != null)
				{
					// say WHICH station is missing, not "it doesn't fit" (E6: a plan may bring its own, and if it did
					// not reach the spot the player has to know what is missing)
					string station = Localization.instance != null ? Localization.instance.Localize(state.Piece.m_craftingStation.m_name) : state.Piece.m_craftingStation.m_name;
					Finish(ai, state, "hc_ai_build_needs_station", status, station);
					return false;
				}
				Finish(ai, state, "hc_ai_build_plan_no_site", status, state.Template.Label, state.Prefab);
				return false;
			}
		}
		// [Building] RequireHammer: off by default (the executor does not need one); when the player turns it on, a
		// companion without a hammer refuses before paying anything
		if (ModConfig.BuildRequireHammer != null && ModConfig.BuildRequireHammer.Value && !HasHammer(bag))
		{
			Log(ai, "no hammer in the bag (RequireHammer is on)");
			Finish(ai, state, "hc_ai_build_no_hammer", "no hammer", LabelOf(state));
			return false;
		}
		// E4 (the safe half): the bag first, then the chests it may use, all or nothing. Gathering stays OUT of this
		// job - an attempt to gather inside it turned Follow off and let the harvest steer while the AI also steered,
		// and the companion's movement went haywire (player report, 01/11/2026). Gathering is its own order.
		bool useChests = ModConfig.BuildUseChests == null || ModConfig.BuildUseChests.Value;
		Dictionary<string, int> missing;
		if (!BuildStock.Take(bag, state.Piece, state.Pos, cs, useChests, out missing))
		{
			Log(ai, "missing: " + MissingText(missing));
			// E4b: the gather is a SEPARATE order. This one ends (with the line that says it is going to get it), the
			// normal gather order takes over the movement, and a small watcher starts a new build order when the
			// materials are in the bag. One owner of MHR_Mover at a time - the 01/11/2026 lesson.
			// E6b: a plan gathers too now - the piece that is short hands the order to the gather and the plan resumes
			// by itself (the player's design: start with what the bag has, take from the chests, and go gather the rest)
			bool gather = ModConfig.BuildGatherForBuild == null || ModConfig.BuildGatherForBuild.Value;
			if (gather)
			{
				GatherStart start = BeginGather(ai, state, cs, missing);
				if (start == GatherStart.Walking)
				{
					return true;   // the build walks to the resource; the gather takes over when it arrives
				}
				if (start == GatherStart.Handed)
				{
					Finish(ai, state, "hc_ai_build_collecting", "gathering first", MissingText(missing), LabelOf(state));
					return false;
				}
			}
			Finish(ai, state, "hc_ai_build_missing", null, MissingText(missing), LabelOf(state));
			return false;
		}
BuildResult result = BuildExecutor.Place(cs, state.Piece, state.Pos, state.Rot);
		if (!result.Ok)
		{
			Log(ai, "executor failed (" + result.Route + "): " + result.Error);
			// the material went back to the bag: the order failed, it does not lose the wood
			Refund(bag, state.Piece);
			Finish(ai, state, "hc_ai_build_failed", result.Error, LabelOf(state));
			return false;
		}
		state.Placed = result.GameObject;
		state.PlacedAt = Time.time;
		state.Phase = Phase.Verify;
		Log(ai, "placed (" + result.Route + "), verifying next frame");
		return true;
	}

	private static bool Verify(CompanionAI ai, State state, CompanionSetup cs)
	{
		long creator = BuildExecutor.CreatorOf(cs, Player.m_localPlayer);
		string reason = state.Placed != null
			? BuildExecutor.Verify(state.Placed, state.Piece, creator)
			: BuildExecutor.VerifyAt(state.Piece, state.Pos, creator, out state.Placed);
		if (reason != null)
		{
			Log(ai, "verification failed: " + reason);
			Finish(ai, state, "hc_ai_build_failed", reason, LabelOf(state));
			return false;
		}
		// E6: the acceptance "sem peça solta no ar" - the game's own verdict (WearNTear.UpdateSupport/HaveSupport).
		// It is asked for every piece, single or plan; null (the reflection did not resolve) is never a refusal.
		// The support of a piece flows from what is below it, and the piece below may have had its own support computed
		// in the same frame: a "no support" answer is asked again for a couple of frames before it becomes a refusal.
		WearNTear wear = state.Placed != null ? state.Placed.GetComponent<WearNTear>() : null;
		bool? supported = BuildSupport.Supported(wear);
		if (supported == false)
		{
			if (Time.time - state.PlacedAt < 1.5f)
			{
				return true;   // ask again next frame (the piece below may still be settling)
			}
			float? value = BuildSupport.Value(wear);
			float? min = BuildSupport.Min(wear);
			Log(ai, "UNSUPPORTED: " + state.Prefab + " (support " + (value.HasValue ? value.Value.ToString("F1") : "?")
				+ " < min " + (min.HasValue ? min.Value.ToString("F1") : "?") + ")");
			Finish(ai, state, "hc_ai_build_unsupported", "no support", LabelOf(state));
			return false;
		}
		Log(ai, "done: " + state.Prefab + " at (" + F(state.Pos.x) + ", " + F(state.Pos.y) + ", " + F(state.Pos.z) + ")"
			+ (state.Template != null ? " (" + (state.Index + 1) + "/" + state.Plan.Count + ")" : string.Empty)
			+ (supported.HasValue ? " supported" : string.Empty));
		// E6: next piece of the plan (the timeout is per piece, so a long plan is not a slow piece)
		if (state.Template != null && state.Plan != null && state.Index + 1 < state.Plan.Count)
		{
			state.Index++;
			PlannedStep next = state.Plan[state.Index];
			state.Prefab = next.Prefab;
			state.Piece = next.Piece;
			state.Pos = next.Pos;
			state.Rot = next.Rot;
			state.Placed = null;
			state.Start = Time.time;
			state.Phase = Phase.Goto;
			Log(ai, "next: " + next.Prefab + " (" + (state.Index + 1) + "/" + state.Plan.Count + ")");
			return true;
		}
		if (state.Template != null && state.Plan != null)
		{
			Finish(ai, state, "hc_ai_build_plan_done", null, state.Template.Label, state.Plan.Count);
			return false;
		}
		Finish(ai, state, "hc_ai_build_done", null, LabelOf(state));
		return false;
	}

	/// <summary>Gives the piece's cost back to the bag when the placement itself failed (the consume already
	/// happened). Never called after a success.</summary>
	private enum GatherStart
	{
		None,
		Walking,
		Handed
	}

	/// <summary>
	/// E4b: decides how the gather starts. If the nearest resource is far (beyond the harvest's own reach around
	/// the player, which is what made it look like a 25 m radius), the BUILD walks there first - with the mode still
	/// Follow, so the harvest is idle and only the build steers. On arrival <see cref="HandOver"/> gives the order
	/// to the normal gather and the build ends.
	/// </summary>
	private static GatherStart BeginGather(CompanionAI ai, State state, CompanionSetup cs, Dictionary<string, int> missing)
	{
		// E6b (player's request, 02/11/2026): for a PLAN the gather asks for the WHOLE structure's shortfall, not the
		// piece's - if it has to chop wood anyway, it brings enough for the rest of the house (or until the bag is
		// nearly full) and comes back once instead of once per piece.
		Dictionary<string, int> need = missing;
		if (state.Template != null)
		{
			Humanoid humanoid = ai.GetComponent<Humanoid>();
			Inventory bag = humanoid != null ? humanoid.GetInventory() : null;
			bool useChests = ModConfig.BuildUseChests == null || ModConfig.BuildUseChests.Value;
			// E6b (player's request, 02/11/2026): take what the chests hold BEFORE going out - the wood in a chest is
			// wood the house can use, and the player must not watch it chop while a chest still holds 30
			int moved = BuildStock.WithdrawTemplate(bag, state.Template, state.Pos, cs, useChests);
			if (moved > 0)
			{
				Log(ai, "withdrew " + moved + " item(s) from the chests before gathering");
			}
			Dictionary<string, int> total = TemplateMissing(bag, state.Template, state.Pos, cs, useChests);
			if (total.Count > 0)
			{
				need = total;
			}
		}
		string item = null;
		int mode = 0;
		int amount = 0;
		foreach (KeyValuePair<string, int> kv in need)
		{
			int candidate = BuildStock.GatherMode(kv.Key);
			if (candidate == 0)
			{
				continue;
			}
			item = kv.Key;
			mode = candidate;
			amount = kv.Value;
			break;
		}
		if (mode == 0 || cs == null)
		{
			return GatherStart.None;
		}
		state.GatherMode = mode;
		state.GatherItem = item;
		state.GatherAmount = amount;
		float radius = ModConfig.BuildGatherRadius != null ? ModConfig.BuildGatherRadius.Value : 120f;
		Transform resource = NearestResource(state.Pos, radius, item);
		if (resource == null)
		{
			// nothing to collect inside the radius: answer NOW with what is missing instead of handing the order to a
			// harvest that has nothing to do and waiting the 300 s out (acceptance of E4: "no trees in range -> says
			// what is missing and ends, no loop")
			Log(ai, "no resource within " + radius.ToString("F0") + " m - refusing");
			return GatherStart.None;
		}
		float distance = Vector3.Distance(state.Pos, resource.position);
		if (resource != null && distance > 20f)
		{
			state.GatherTarget = resource.position;
			state.Phase = Phase.GatherWalk;
			Log(ai, "missing " + amount + "x " + item + " - walking to " + resource.gameObject.name + " at " + distance.ToString("F0") + " m first");
			return GatherStart.Walking;
		}
		HandOver(ai, state, cs);
		return GatherStart.Handed;
	}

	/// <summary>Gives the movement to the normal gather order (mode + target = the shortfall) and leaves the
	/// pending build for the watcher. Called once, with nothing else steering.</summary>
	private static void HandOver(CompanionAI ai, State state, CompanionSetup cs)
	{
		CompanionSetup.ApplyActionMode(cs, state.GatherMode, false);
		CompanionSetup.SetOrderCap(cs, state.GatherMode, state.GatherAmount, state.GatherItem);
		s_pending[ai.GetInstanceID()] = new Pending
		{
			Prefab = state.Prefab,
			Template = state.Template != null ? state.Template.Name : null,
			Hint = state.Hint,
			Mode = state.GatherMode,
			Item = state.GatherItem,
			Amount = state.GatherAmount,
			Index = state.Index,
			Anchor = state.Anchor,
			AnchorRot = state.AnchorRot,
			Start = Time.time
		};
		Log(ai, "gathering " + state.GatherAmount + "x " + state.GatherItem + " (mode " + state.GatherMode + ") - the build resumes after it");
	}

	/// <summary>E4b: the build's own walk to the resource (the gather mode is NOT set yet, so nothing else steers).</summary>
	private static bool GatherWalk(CompanionAI ai, State state, CompanionSetup cs, float dt)
	{
		if (Vector3.Distance(ai.transform.position, state.GatherTarget) <= 4f || MHR_Mover.GaveUp(ai))
		{
			ai.StopMoving();
			MHR_Mover.Release(ai);
			HandOver(ai, state, cs);
			Finish(ai, state, "hc_ai_build_collecting", "arrived at the resource", state.GatherItem + " x" + state.GatherAmount, LabelOf(state));
			return false;
		}
		MHR_Mover.Go(ai, dt, state.GatherTarget, 3f, true, "BuildGather");
		return true;
	}

	/// <summary>The shortfall of the WHOLE plan (all pieces together) against the bag and the usable chests.</summary>
	/// <summary>
	/// E6b: the shortfall of a whole <b>template</b> (all its pieces together) against the bag and the usable chests -
	/// the watcher of a plan waiting for materials uses it.
	/// </summary>
	private static Dictionary<string, int> TemplateMissing(Inventory bag, BuildTemplates.Template template, Vector3 center, Component who, bool useChests)
	{
		Dictionary<string, int> missing = new Dictionary<string, int>();
		if (bag == null || template == null || template.Steps.Length == 0)
		{
			return missing;
		}
		Dictionary<string, int> need = new Dictionary<string, int>();
		foreach (BuildTemplates.Step step in template.Steps)
		{
			Piece piece = BuildProbe.PieceOf(step.Prefab);
			if (piece == null || piece.m_resources == null)
			{
				continue;
			}
			foreach (Piece.Requirement requirement in piece.m_resources)
			{
				if (requirement == null || requirement.m_resItem == null)
				{
					continue;
				}
				string name = requirement.m_resItem.m_itemData.m_shared.m_name;
				int amount = requirement.GetAmount(1);
				if (amount <= 0)
				{
					continue;
				}
				int seen;
				need.TryGetValue(name, out seen);
				need[name] = seen + amount;
			}
		}
		Dictionary<string, int> have = BuildStock.Available(bag, BuildProbe.PieceOf(template.Steps[0].Prefab), center, who, useChests);
		foreach (KeyValuePair<string, int> kv in need)
		{
			int got;
			have.TryGetValue(kv.Key, out got);
			if (got < kv.Value)
			{
				missing[kv.Key] = kv.Value - got;
			}
		}
		return missing;
	}

	private static Dictionary<string, int> PlanMissing(Inventory bag, List<PlannedStep> plan, Vector3 center, Component who, bool useChests)
	{
		Dictionary<string, int> missing = new Dictionary<string, int>();
		if (bag == null || plan == null || plan.Count == 0)
		{
			return missing;
		}
		Dictionary<string, int> have = BuildStock.Available(bag, plan[0].Piece, center, who, useChests);
		Dictionary<string, int> need = new Dictionary<string, int>();
		foreach (PlannedStep step in plan)
		{
			if (step.Piece == null || step.Piece.m_resources == null)
			{
				continue;
			}
			foreach (Piece.Requirement requirement in step.Piece.m_resources)
			{
				if (requirement == null || requirement.m_resItem == null)
				{
					continue;
				}
				string name = requirement.m_resItem.m_itemData.m_shared.m_name;
				int amount = requirement.GetAmount(1);
				if (amount <= 0)
				{
					continue;
				}
				int seen;
				need.TryGetValue(name, out seen);
				need[name] = seen + amount;
			}
		}
		foreach (KeyValuePair<string, int> kv in need)
		{
			int got;
			have.TryGetValue(kv.Key, out got);
			if (got < kv.Value)
			{
				missing[kv.Key] = kv.Value - got;
			}
		}
		return missing;
	}

	/// <summary>The small offsets tried around a planned pose (the ground under a template is never perfectly flat).</summary>
	private static IEnumerable<Vector3> Nudges()
	{
		yield return new Vector3(0f, 0.25f, 0f);
		yield return new Vector3(0f, -0.25f, 0f);
		yield return new Vector3(0.25f, 0f, 0f);
		yield return new Vector3(-0.25f, 0f, 0f);
		yield return new Vector3(0f, 0f, 0.25f);
		yield return new Vector3(0f, 0f, -0.25f);
		yield return new Vector3(0f, 0.5f, 0f);
		yield return new Vector3(0f, -0.5f, 0f);
		yield return new Vector3(0.5f, 0f, 0f);
		yield return new Vector3(-0.5f, 0f, 0f);
		yield return new Vector3(0f, 0f, 0.5f);
		yield return new Vector3(0f, 0f, -0.5f);
	}

	private static bool Ok(Dictionary<string, object> check)
	{
		return check != null && check["ok"] is bool ok && ok;
	}

	/// <summary>
	/// E6: the verdict of a planned pose. A piece <b>above the ground</b> is allowed to fail only on
	/// <c>no_support</c> here - its support comes from the pieces of the plan, which are still ghosts at this point;
	/// <see cref="Place"/> checks it again when they are standing. <c>needs_station</c> is also allowed: the plan may
	/// bring its own station (the house brings a workbench), so the station is only provable at placement time.
	/// </summary>
	private static bool OkForPlan(Dictionary<string, object> check, bool aboveGround)
	{
		if (check == null)
		{
			return false;
		}
		if (check["ok"] is bool ok && ok)
		{
			return true;
		}
		List<string> reasons = check["reasons"] as List<string>;
		if (reasons == null || reasons.Count == 0)
		{
			return false;
		}
		foreach (string reason in reasons)
		{
			if (reason == "needs_station")
			{
				continue;
			}
			if (aboveGround && reason == "no_support")
			{
				continue;
			}
			return false;
		}
		return true;
	}

	/// <summary>The nearest tree (wood) or rock (stone/ore) inside <c>[Building] GatherRadius</c>, once per order.</summary>
	private static Transform NearestResource(Vector3 center, float radius, string item)
	{
		string folded = (item ?? string.Empty).ToLowerInvariant();
		bool wood = folded.Contains("wood") || folded.Contains("roundlog");
		Transform best = null;
		float bestDistance = radius;
		if (wood)
		{
			foreach (TreeBase tree in UnityEngine.Object.FindObjectsByType<TreeBase>(FindObjectsSortMode.None))
			{
				if (tree == null)
				{
					continue;
				}
				float distance = Vector3.Distance(center, tree.transform.position);
				if (distance < bestDistance)
				{
					bestDistance = distance;
					best = tree.transform;
				}
			}
		}
		else
		{
			foreach (MineRock5 rock in UnityEngine.Object.FindObjectsByType<MineRock5>(FindObjectsSortMode.None))
			{
				if (rock == null)
				{
					continue;
				}
				float distance = Vector3.Distance(center, rock.transform.position);
				if (distance < bestDistance)
				{
					bestDistance = distance;
					best = rock.transform;
				}
			}
		}
		return best;
	}

	/// <summary>
	/// E4b: the watcher. Runs only when there is no build order running (so it never steers): if the gather is
	/// still short it does nothing, and when the bag covers the piece it clears the gather and starts a fresh build
	/// order (which re-finds the spot and walks back). The player changing the work mode, death or the gather
	/// timeout forget the pending build.
	/// </summary>
	private static void CheckPending(CompanionAI ai)
	{
		Pending pending;
		if (!s_pending.TryGetValue(ai.GetInstanceID(), out pending) || pending == null)
		{
			return;
		}
		CompanionSetup cs = ai.GetComponent<CompanionSetup>();
		Humanoid humanoid = ai.GetComponent<Humanoid>();
		Inventory bag = humanoid != null ? humanoid.GetInventory() : null;
		ZNetView nv = ai.GetComponent<ZNetView>();
		if (cs == null || bag == null || nv == null || !nv.IsValid() || !nv.IsOwner() || humanoid == null || humanoid.IsDead())
		{
			s_pending.Remove(ai.GetInstanceID());
			return;
		}
		// E6b: a plan waiting for its materials (the template) or a single piece
		BuildTemplates.Template template = pending.Template != null ? BuildTemplates.ByName(pending.Template) : null;
		Piece piece = pending.Template != null ? null : BuildProbe.PieceOf(pending.Prefab);
		if (pending.Template != null ? template == null : piece == null)
		{
			s_pending.Remove(ai.GetInstanceID());
			return;
		}
		if (ModeOf(cs) != pending.Mode)
		{
			s_pending.Remove(ai.GetInstanceID());
			Log(ai, "the work changed - the build order was dropped");
			return;
		}
		bool useChests = ModConfig.BuildUseChests == null || ModConfig.BuildUseChests.Value;
		if (Time.time - pending.Start > GatherTimeout)
		{
			s_pending.Remove(ai.GetInstanceID());
			CompanionSetup.ClearOrderCap(cs);
			// the order has to SAY what is still missing (acceptance of E4: it must not end in silence)
			Dictionary<string, int> still = template != null
				? TemplateMissing(bag, template, pending.Hint, cs, useChests)
				: BuildStock.Missing(bag, piece, pending.Hint, cs, useChests);
			string label = template != null
				? template.Label
				: (Localization.instance != null ? Localization.instance.Localize(piece.m_name) : pending.Prefab);
			Log(ai, "gathering took too long - saying what is missing");
			Say(ai, "hc_ai_build_missing", MissingText(still), label);
			return;
		}
		// E6b (bug found in the player's test, 01/11/2026): the watcher resumes when the SHORTFALL that was handed over
		// is covered - the plan pays piece by piece, so waiting for the whole template made the order time out with the
		// piece's wood already in the bag ("gathering took too long" after gathering the 2 wood it asked for)
		Piece stock = template != null ? BuildProbe.PieceOf(template.Steps[0].Prefab) : piece;
		Dictionary<string, int> have = BuildStock.Available(bag, stock, pending.Hint, cs, useChests);
		int got;
		have.TryGetValue(pending.Item ?? string.Empty, out got);
		// E6b (player's request, 02/11/2026): the plan asked for the WHOLE structure's shortfall, which can be more than
		// the bag holds. When the bag is nearly full the build resumes with what it has - the rest is gathered on the
		// next shortfall instead of standing in the forest waiting for a number that never fits.
		bool nearlyFull = humanoid.GetInventory().GetTotalWeight() >= CompanionCarry.Max(humanoid) * 0.8f;
		if (got < pending.Amount && !nearlyFull)
		{
			// E6b diagnostics (the player's test showed the order sitting in the gather without a word): one line per
			// change of the count, so the log says WHY the build is still waiting
			if (pending.LastLogged != got)
			{
				pending.LastLogged = got;
				Log(ai, "waiting for " + pending.Item + ": have " + got + "/" + pending.Amount
					+ (string.IsNullOrEmpty(pending.Template) ? string.Empty : " for the " + pending.Template)
					+ " (mode " + ModeOf(cs) + ", expected " + pending.Mode + ")");
			}
			return;   // still gathering
		}
		Log(ai, "gather done (have " + got + "/" + pending.Amount + (nearlyFull ? ", bag nearly full" : string.Empty) + ") - resuming");
		s_pending.Remove(ai.GetInstanceID());
		CompanionSetup.ClearOrderCap(cs);
		// back to Follow mode BEFORE the build starts: the harvest must not keep steering while the build does
		CompanionSetup.ApplyActionMode(cs, CompanionSetup.ModeFollow, false);
		bool resumed = template != null
			? StartPlan(cs, template, pending.Hint, pending.Index, pending.Anchor, pending.AnchorRot, true)
			: Start(cs, pending.Prefab, pending.Hint);
		if (resumed)
		{
			Log(ai, "materials ready - resuming the build");
		}
		else
		{
			Log(ai, "could not resume the " + (pending.Template ?? pending.Prefab) + " - the order ends");
			Say(ai, "hc_ai_build_failed", pending.Template ?? pending.Prefab);
		}
	}

	/// <summary>Is a hammer in the bag? (only asked when <c>[Building] RequireHammer</c> is on).</summary>
	private static bool HasHammer(Inventory bag)
	{
		return bag != null && bag.HaveItem("$item_hammer");
	}

	private static void Refund(Inventory bag, Piece piece)
	{
		if (bag == null || piece == null || piece.m_resources == null)
		{
			return;
		}
		foreach (Piece.Requirement requirement in piece.m_resources)
		{
			if (requirement == null || requirement.m_resItem == null)
			{
				continue;
			}
			int amount = requirement.GetAmount(1);
			if (amount > 0)
			{
				bag.AddItem(requirement.m_resItem.gameObject, amount);
			}
		}
	}

	private static void Finish(CompanionAI ai, State state, string key, string why, params object[] args)
	{
		state.Active = false;
		s_state.Remove(ai.GetInstanceID());
		MHR_Mover.Release(ai);
		// one line per order end, with the key that was spoken: it is how a test proves which refusal came out
		Log(ai, "order ended: " + (key ?? "(silent)") + (string.IsNullOrEmpty(why) ? string.Empty : " (" + why + ")"));
		if (key != null)
		{
			Say(ai, key, args);
		}
	}

	private static string LabelOf(State state)
	{
		if (state == null)
		{
			return string.Empty;
		}
		try
		{
			return Localization.instance != null ? Localization.instance.Localize(state.Piece != null ? state.Piece.m_name : state.Prefab) : state.Prefab;
		}
		catch (Exception)
		{
			return state.Prefab;
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

	private static string MissingText(Dictionary<string, int> missing)
	{
		if (missing == null || missing.Count == 0)
		{
			return "(nada)";
		}
		List<string> parts = new List<string>();
		foreach (KeyValuePair<string, int> kv in missing)
		{
			string name = Localization.instance != null ? Localization.instance.Localize(kv.Key) : kv.Key;
			parts.Add(name + " x" + kv.Value);
		}
		return string.Join(", ", parts.ToArray());
	}

	private static string TallyText(Dictionary<string, int> tally)
	{
		if (tally == null || tally.Count == 0)
		{
			return string.Empty;
		}
		List<string> parts = new List<string>();
		foreach (KeyValuePair<string, int> kv in tally)
		{
			parts.Add(kv.Key + " x" + kv.Value);
		}
		return string.Join(", ", parts.ToArray());
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

	/// <summary>The companion itself, as the "character to ignore" of the validator: it walks up to the piece, so it
	/// would always be inside the spacing radius of its own build (E6, measured: the 5th wall refused with
	/// <c>blocked_by_player</c> because the companion was standing there).</summary>
	private static Component Self(CompanionAI ai)
	{
		return ai != null ? ai.GetComponent<CompanionSetup>() : null;
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
