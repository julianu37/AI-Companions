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
	/// <c>Player.m_maxPlaceDistance</c> (5 m, decompiled 1.0.20). The flat cap used to be 2,5 m and could not
	/// reach the upper roof row of the house (y = 5, i.e. 4,9 m above the feet) - the companion walked in circles
	/// until the order timed out (player report, 01/11/2026: "parece que não alcança").
	/// </summary>
	internal const float MaxPlace = 5f;
	/// <summary>E7 (review, 03/11/2026): the walk still stops at <see cref="Reach"/>; the flat cap opens to
	/// <c>MaxPlace - 0,5</c> (4,5 m) only after 3 s without getting closer, and the eye distance is always this 5 m.</summary>

	/// <summary>The companion's eye height above its feet (the game measures the placement distance from the eye).</summary>
	private const float EyeHeight = 1.5f;
	/// <summary>E7 (player's idea, 03/11/2026: "temporary ladders"): the temporary step of a scaffold. A wood STAIR and
	/// not a ladder: it is a ramp the mover walks up as plain geometry (no ladder logic and no landing to fall from - the
	/// trap of 03/11/2026). Measured with build.snaps: +1 m, rising towards its own -Z.</summary>
	private const string ScaffoldPrefab = "wood_stair";
	/// <summary>
	/// How many temporary stairs one ORDER may place. It is a tool, not scaffolding for the whole house: with the budget
	/// per piece the player's test of 03/11/2026 ended with the site covered in stairs (985 pieces x 3). Each one is +1 m
	/// and it is removed as soon as the piece it was placed for goes up.
	/// </summary>
	internal const int MaxScaffolds = 4;
	/// <summary>How many floor pieces one step may pull forward (a landing needs ONE tile; 2 is slack).</summary>
	private const int MaxPulledFloors = 2;

	/// <summary>E10: how many leveling passes a job may apply before it gives up (each moves the ground by at most 1 m).</summary>
	internal const int MaxLevelPasses = 4;
	/// <summary>E10: above this height variation (metres) the soft level cannot win - the spot is refused instead of digging a crater.</summary>
	internal const float MaxLevelVariation = 1.8f;

	/// <summary>E4b: how long a pending build waits for the gather <b>without any progress</b> before it is dropped. Any
	/// wood that arrives resets it (see <see cref="Pending.LastProgressAt"/>).</summary>
	internal const float GatherTimeout = 300f;

	/// <summary>E7 (player's request, 03/11/2026): how long the site clearing may take before the build goes on without
	/// it - clearing a whole forest is not a build site.</summary>
	internal const float ClearBudget = 240f;
	/// <summary>E7: how long one tree/log may take before it is given up (the harvest can be stuck on it).</summary>
	private const float ClearPerObstacle = 45f;
	/// <summary>E7: most obstacles one order clears (what is left is handled by the retry/skip of the pieces).</summary>
	private const int ClearMaxObstacles = 40;
	/// <summary>E7: consecutive refusals (no axe, or one below the tree's tier) that mean "I cannot cut here".</summary>
	private const int ClearMaxRejects = 5;
	/// <summary>E7: the only station this mod can build by itself (the piece the player's own bench is).</summary>
	private const string BenchPrefab = "piece_workbench";
	/// <summary>E7: most workbenches one order adds (a 30 m house with chests on three floors needs a few; a broken file
	/// must not turn the plan into a bench factory).</summary>
	private const int MaxPlannedBenches = 8;
	/// <summary>E7: most spot searches for those benches - every search runs the game's placement ghost up to 96 times and
	/// a site with nothing free must not freeze the frame (one search is one <c>BuildSite.Check</c> ring walk).</summary>
	private const int MaxBenchSearches = 6;
	/// <summary>The workbench's coverage in metres when the prefab cannot be read: <c>CraftingStation.m_rangeBuild</c> is 20
	/// (the radius in which a station frees building - measured, see <c>docs/re/building-notes.md</c>).</summary>
	private const float BenchDefaultRadius = 20f;

	/// <summary>E7: the clearing square never grows past this. It was 30 m and the player's plan measured 32,1 m, so the
	/// outer ring of the site kept its trees and the logs of the ones inside rolled into it (report, 03/11/2026).</summary>
	private const float ClearMaxSize = 40f;

	private const string LogTag = "[Build] ";

	private enum Phase
	{
		/// <summary>E10: leveling the ground before a plan (the house needs a flat 8x8).</summary>
		Level,
		/// <summary>E7 (player's request, 03/11/2026): cutting the trees/stumps/logs inside the plan's footprint before
		/// the first piece - they refuse the floors and block the walking.</summary>
		Clear,
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
		/// <summary>E7: the blueprint being built (its label; null for a template or a single piece).</summary>
		internal string Blueprint;
		/// <summary>E7: the file of that blueprint - the order is resumed from it after a gather (the plan is rebuilt).</summary>
		internal string BlueprintFile;
		/// <summary>E7: [Building] BlueprintFree - do not pay for the pieces (a test/creative switch).</summary>
		internal bool Free;
		/// <summary>E7: pieces placed and pieces skipped (a blueprint skips what it cannot stand instead of stopping).</summary>
		internal int PlacedCount;
		internal int Skipped;
		/// <summary>
		/// E7 (a ideia do "Build all" deles sem o BuildOrders, 03/11/2026): a piece that does not stand YET (no support: its
		/// floor is not up, its wall is missing) goes to this queue instead of being lost. When the plan ends it is tried
		/// again - the second pass is what makes a house converge (floors, then walls, then roof).
		/// </summary>
		internal List<PlannedStep> Retry;
		internal int Passes;
		/// <summary>E7: the square the site clearing covers (the plan's footprint, centred on the aim).</summary>
		internal Vector3 ClearCenter;
		internal float ClearSize;
		/// <summary>E7: the tree/log being cut right now and since when (one that cannot be cut is given up).</summary>
		internal GameObject ClearTarget;
		internal float ClearTargetAt;
		/// <summary>E7: when the clearing began and how many obstacles it saw / tried.</summary>
		internal float ClearStart;
		/// <summary>E7: the 2 s after a tree falls (the harvest walks its drops before the next obstacle).</summary>
		internal float ClearWait;
		/// <summary>E7: this piece already passed the up-front support check (once per piece, not per frame).</summary>
		internal bool Checked;
		/// <summary>E7: one extra look 2 s after the square reads empty (a felled tree spawns its logs a moment later and
		/// a log rolls).</summary>
		internal int ClearEmptyScans;
		/// <summary>E7: the obstacles that were given up on already got their second look.</summary>
		internal bool ClearSkipTried;
		internal int ClearTotal;
		internal int ClearCount;
		internal int ClearRejects;
		/// <summary>E7: obstacles the harvest refused (no tool for it) - never tried again.</summary>
		internal readonly HashSet<int> ClearSkip = new HashSet<int>();
		/// <summary>E7: workbenches inserted in front of a piece refused with <c>needs_station</c> (the self-healing
		/// half of the station pass, capped like it).</summary>
		internal int BenchInserts;
		/// <summary>E7: a template clears the site after the leveling (a blueprint does it before the first piece).</summary>
		internal bool NeedsClear;
		/// <summary>E7: the cooldown of the "jump and place" hop (it must not be a bunny hop every frame).</summary>
		internal float NextJumpAt;
		/// <summary>E7 (test of 03/11/2026: the companion fell off a ladder to nowhere until it died): the last height
		/// seen and how many big drops happened while walking to the current piece.</summary>
		internal float LastY;
		internal int FallDrops;
		/// <summary>E7 (player's report, 03/11/2026: "she is struggling with the tip of the roof"): the closest it ever got
		/// to the piece and since when - one it cannot get closer to for 12 s is retried instead of timing out in 60 s.</summary>
		internal float BestDistance;
		internal float BestAt;
		/// <summary>E7 (player's idea, 03/11/2026): the temporary stairs placed to reach a piece above the 5 m eye rule.
		/// They are NOT part of the plan and are removed when the order ends.</summary>
		internal readonly List<GameObject> Scaffolds = new List<GameObject>();
		/// <summary>E7: how many temporary stairs this ORDER already placed, and the cooldown before the next attempt
		/// (the budget is per order, never per piece - 985 pieces x 3 covered the site in stairs).</summary>
		internal int ScaffoldTries;
		internal float NextScaffoldAt;
		/// <summary>E7 (review, 03/11/2026): the pieces accepted while a temporary stair stood - they are asked again for
		/// support before the stair is removed, so a roof that was held up by it does not get a hole.</summary>
		internal readonly List<GameObject> Scaffolded = new List<GameObject>();
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
	/// <summary>E7: cached layer mask of the scaffold probe (-1 = not resolved yet).</summary>
	private static int s_scaffoldMask = -1;

	/// <summary>E7: when a blueprint last asked for the ground to be leveled (a guard against the measure/level loop).</summary>
	private static readonly Dictionary<string, float> s_levelTried = new Dictionary<string, float>();

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
		/// <summary>E7: the blueprint waiting for its materials (label and file; null for a template/piece).</summary>
		internal string Blueprint;
		internal string BlueprintFile;
		/// <summary>E7 (player's report, 03/11/2026: "it fell and died, and the build did not go on"): the order is waiting
		/// for the RESPAWN - it resumes by itself (same plan, same anchor, same piece) when the companion is alive again.</summary>
		internal bool AfterDeath;
		/// <summary>E7 (player's report, 03/11/2026: "he died and did not go on with the build"): the PERMANENT id of the
		/// companion. The respawn may build a new object (a new instance id), and the pending is keyed by it - the id is what
		/// lets <see cref="Reattach"/> hand the order to the new body.</summary>
		internal string CompanionId;
		/// <summary>E7: the pieces that did not stand yet when the gather took over - the resumed order must carry them, or
		/// the 2nd storey of the house is lost for good (the retry pass of the first part never runs).</summary>
		internal List<PlannedStep> Retry;
		internal int Passes;
		internal Vector3 Hint;
		internal int Mode;
		internal string Item;
		/// <summary>E6b: the shortfall that was handed over (for the log/speech - the watcher recomputes it).</summary>
		internal int Amount;
		/// <summary>E6b: the plan that was handed over, so the watcher can recompute the shortfall without rebuilding it
		/// (the player's test of the 4x4 looped because it compared the BAG TOTAL with the SHORTFALL: the bag already had
		/// the 2 it asked for and the door still needed 4).</summary>
		internal List<PlannedStep> Plan;
		/// <summary>E6b: when the watcher last recomputed the shortfall (once a second is enough).</summary>
		internal float NextCheck;
		/// <summary>E6b: when the count last went up. The gather timeout counts from HERE, not from the handover - a
		/// gather that keeps bringing wood never times out (the player's test lost 36/39 to the fixed 300 s clock).</summary>
		internal float LastProgressAt;
		/// <summary>E6b: where the plan stopped - the watcher resumes at the SAME anchor and from this piece.</summary>
		internal int Index;
		internal Vector3 Anchor;
		internal Quaternion AnchorRot;
		/// <summary>E6b: Follow was on when the gather was handed over (HandOver turns it off) - restore it after.</summary>
		internal bool FollowWas;
		/// <summary>E6b: when the gather was first seen with nothing to do (0 = it is working).</summary>
		internal float IdleSince;
		/// <summary>E6b: when the "nothing to work" line was last written (it is a watch, not an event).</summary>
		internal float LastIdleLog;
		/// <summary>E6b: the last shortfall text written to the log (the watcher is otherwise silent while it waits).</summary>
		internal string LastLoggedText;
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
		DiscardState(ai);
		s_state[ai.GetInstanceID()] = state;
		DropPending(ai, cs);
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
			// E7 (review, 03/11/2026): the retry queue has to exist for a template too - the up-front support check sends the
			// piece that does not stand yet to `QueueRetry`, and with a null list it was silently skipped for good
			Retry = new List<PlannedStep>(),
			Phase = anchorKnown ? Phase.Find : Phase.Level,
			StartIndex = startIndex,
			Anchor = anchor,
			AnchorRot = anchorRot,
			AnchorKnown = anchorKnown,
			// E7: the site clearing runs for a template too, right after the leveling (and never on a resume)
			NeedsClear = !anchorKnown && (ModConfig.BuildClearSite == null || ModConfig.BuildClearSite.Value),
			ClearCenter = hint,
			ClearSize = !anchorKnown ? FootprintSize(template) : 0f
		};
		DiscardState(ai);
		s_state[ai.GetInstanceID()] = state;
		DropPending(ai, cs);
		Log(ai, "order: build plan " + template.Name + " (" + template.Steps.Length + " pieces) near (" + F(hint.x) + ", " + F(hint.y) + ", " + F(hint.z) + ")");
		return true;
	}

	/// <summary>How many times the pieces that did not stand are tried again before they are given up.</summary>
	internal const int MaxPasses = 3;

	/// <summary>
	/// E7: the current piece goes back to the retry queue (it may stand once its neighbours are up) or is counted as
	/// skipped when the passes are used up. This is the "build what stands, come back for the rest" loop of their
	/// BuildOrders, done with the game's own verdict instead of a stability simulation.
	/// </summary>
	private static void QueueRetry(CompanionAI ai, State state, string why)
	{
		if (state.Retry != null && state.Passes < MaxPasses && state.Piece != null)
		{
			state.Retry.Add(new PlannedStep { Prefab = state.Prefab, Piece = state.Piece, Pos = state.Pos, Rot = state.Rot });
			Log(ai, "retry later: " + state.Prefab + " (" + why + ") at (" + F(state.Pos.x) + ", " + F(state.Pos.y) + ", "
				+ F(state.Pos.z) + ") - pass " + state.Passes);
			return;
		}
		state.Skipped++;
		Log(ai, "skipping " + state.Prefab + " (" + why + ") at (" + F(state.Pos.x) + ", " + F(state.Pos.y) + ", "
			+ F(state.Pos.z) + ") - " + state.Skipped + " skipped");
	}

	/// <summary>
	/// E6/E7: moves to the next piece of the plan (a template or a blueprint) or ends the order. Returns true while
	/// there is a next piece to build.
	/// </summary>
	private static bool Advance(CompanionAI ai, State state)
	{
		if (state.Plan != null && state.Index + 1 >= state.Plan.Count
			&& state.Retry != null && state.Retry.Count > 0 && state.Passes < MaxPasses)
		{
			state.Passes++;
			List<PlannedStep> again = state.Retry;
			state.Retry = new List<PlannedStep>();
			int first = state.Plan.Count;
			state.Plan.AddRange(again);
			state.Index = first - 1;
			Log(ai, "retry pass " + state.Passes + " with " + again.Count + " piece(s) that did not stand yet");
		}
		if (state.Plan != null && state.Index + 1 < state.Plan.Count)
		{
			state.Index++;
			PlannedStep next = state.Plan[state.Index];
			state.Prefab = next.Prefab;
			state.Piece = next.Piece;
			state.Pos = next.Pos;
			state.Rot = next.Rot;
			state.Placed = null;
			state.Start = Time.time;
			state.Checked = false;
			state.FallDrops = 0;
			state.BestDistance = 0f;
			state.BestAt = Time.time;
			state.NextScaffoldAt = 0f;
			state.Phase = Phase.Goto;
			Log(ai, "next: " + next.Prefab + " (" + (state.Index + 1) + "/" + state.Plan.Count + ")");
			return true;
		}
		if (state.Blueprint != null)
		{
			Finish(ai, state, "hc_ai_blueprint_done", null, state.Blueprint, state.PlacedCount, state.Skipped);
			return false;
		}
		if (state.Template != null)
		{
			Finish(ai, state, "hc_ai_build_plan_done", null, state.Template.Label, state.Plan != null ? state.Plan.Count : 0);
			return false;
		}
		// a single piece
		Finish(ai, state, "hc_ai_build_done", null, LabelOf(state));
		return false;
	}

	/// <summary>
	/// E7: builds a <b>blueprint</b> from the player's library (<c>.blueprint</c> from PlanBuild/BiomeBlueprints/
	/// valheimians). The poses are the blueprint's own, turned by the yaw of the player's aim and anchored where they
	/// are looking; the order is bottom-up (the loader sorted by height) and a piece that cannot stand in this spot is
	/// skipped instead of ending the build. <paramref name="missingPrefab"/> names the first piece the game does not
	/// have (the caller refuses with that name).
	/// </summary>
	/// <summary>E7: how a blueprint order starts - <see cref="Leveling"/> means the hoe job took over and the build
	/// comes back by itself when the ground is ready.</summary>
	internal enum BlueprintStart { Failed, Started, Leveling }

	internal static BlueprintStart StartBlueprint(CompanionSetup cs, Blueprint blueprint, Vector3 hint, out string missingPrefab, out string missingStation)
	{
		return StartBlueprint(cs, blueprint, hint, out missingPrefab, out missingStation, 0, Quaternion.identity, false);
	}

	/// <summary>
	/// E7 (player's test, 03/11/2026): <b>resumes</b> a blueprint after the gather handover - the same anchor, the same
	/// rotation and the same piece. The order used to come back as "build one wood_beam" (a single piece) and ended, so a
	/// 985-piece house lost the other 700 pieces the moment it ran out of wood. The plan is rebuilt from the file (that is
	/// deterministic) and the indices line up; the leveling and the clearing are NOT repeated.
	/// </summary>
	private static BlueprintStart ResumeBlueprint(CompanionSetup cs, Blueprint blueprint, Vector3 hint, int startIndex,
		Quaternion anchorRot, out string missingPrefab, out string missingStation, List<PlannedStep> storedPlan)
	{
		return StartBlueprint(cs, blueprint, hint, out missingPrefab, out missingStation, startIndex, anchorRot, true, storedPlan);
	}

	private static BlueprintStart StartBlueprint(CompanionSetup cs, Blueprint blueprint, Vector3 hint, out string missingPrefab,
		out string missingStation, int startIndex, Quaternion anchorRot, bool resuming, List<PlannedStep> storedPlan = null)
	{
		missingPrefab = null;
		missingStation = null;
		if (cs == null || blueprint == null || blueprint.Pieces.Count == 0)
		{
			return BlueprintStart.Failed;
		}
		CompanionAI ai = cs.GetComponent<CompanionAI>();
		if (ai == null)
		{
			return BlueprintStart.Failed;
		}
		int max = ModConfig.BuildBlueprintMaxPieces != null ? ModConfig.BuildBlueprintMaxPieces.Value : 2000;
		if (blueprint.Pieces.Count > max)
		{
			CompanionsPlugin.Log?.LogWarning("[Build] blueprint " + blueprint.Label + ": " + blueprint.Pieces.Count
				+ " pieces is over the limit (" + max + ")");
			return BlueprintStart.Failed;
		}
		Player owner = MHR_Owner.Of(cs);
		Vector3 forward = owner != null ? owner.transform.forward : cs.transform.forward;
		// a resumed order keeps the rotation it was given (the player may be looking somewhere else by now)
		Quaternion rot = resuming ? anchorRot : Quaternion.LookRotation(Flat(forward), Vector3.up);
		// every prefab is resolved BEFORE the first piece. A blueprint is somebody else's design: the lines that are
		// not a buildable piece in THIS game (an item, a rug, a mod's prefab) or whose recipe the player has not
		// unlocked are SKIPPED and counted - refusing the whole build because of one line would make most downloaded
		// blueprints useless. With nothing buildable at all it IS a refusal, naming the first prefab it could not use.
		List<PlannedStep> plan = new List<PlannedStep>();
		int skipped = 0;
		string firstSkip = null;
		Player local = Player.m_localPlayer;
		// the blueprint's origin is where its AUTHOR stood when capturing it, not the middle of the build: centring the
		// footprint on the aim makes "construa a maccommhomebasicbp aqui" put the house where the player is looking
		float minX = float.MaxValue;
		float maxX = float.MinValue;
		float minZ = float.MaxValue;
		float maxZ = float.MinValue;
		bool hasBounds = false;
		foreach (BlueprintPiece piece in blueprint.Pieces)
		{
			if (piece == null)
			{
				continue;
			}
			minX = Mathf.Min(minX, piece.Pos.x);
			maxX = Mathf.Max(maxX, piece.Pos.x);
			minZ = Mathf.Min(minZ, piece.Pos.z);
			maxZ = Mathf.Max(maxZ, piece.Pos.z);
			hasBounds = true;
		}
		Vector3 centre = hasBounds ? new Vector3((minX + maxX) * 0.5f, 0f, (minZ + maxZ) * 0.5f) : Vector3.zero;
		foreach (BlueprintPiece piece in blueprint.Pieces)
		{
			Piece resolved = BuildProbe.PieceOf(piece.Prefab);
			if (resolved == null)
			{
				if (missingPrefab == null)
				{
					missingPrefab = piece.Prefab;
				}
				if (firstSkip == null)
				{
					firstSkip = piece.Prefab + " (not a piece here)";
				}
				skipped++;
				continue;
			}
			if (local != null && !local.IsRecipeKnown(resolved.m_name))
			{
				if (firstSkip == null)
				{
					firstSkip = piece.Prefab + " (recipe not unlocked)";
				}
				skipped++;
				continue;
			}
			plan.Add(new PlannedStep
			{
				Prefab = piece.Prefab,
				Piece = resolved,
				Pos = hint + rot * Vector3.Scale(piece.Pos - centre, new Vector3(piece.Scale, piece.Scale, piece.Scale)),
				Rot = rot * piece.Rot
			});
		}
		if (plan.Count == 0)
		{
			CompanionsPlugin.Log?.LogWarning("[Build] blueprint " + blueprint.Label + ": nothing buildable here ("
				+ skipped + " line(s) skipped, first '" + (missingPrefab ?? "?") + "')");
			return BlueprintStart.Failed;
		}
		if (skipped > 0)
		{
			CompanionsPlugin.Log?.LogInfo("[Build] blueprint " + blueprint.Label + ": " + skipped
				+ " line(s) skipped, first: " + (firstSkip ?? "?"));
		}
		float resumeSize = 0f;
		if (resuming && storedPlan != null && storedPlan.Count > 0)
		{
			// E7: the resumed order reuses the EXACT plan of the first part - the workbenches the station pass added are
			// part of it. Rebuilding it would add fewer benches now that they stand and every index after them would shift,
			// so the pieces in between would never be built.
			plan.Clear();
			plan.AddRange(storedPlan);
			// E7 (review B4, 03/11/2026): on a resume the clearing square is not rebuilt, so it is measured from the plan -
			// without it InsertBenchStep thinks the footprint is 0 and puts the bench INSIDE the house
			float rx0 = float.MaxValue;
			float rx1 = float.MinValue;
			float rz0 = float.MaxValue;
			float rz1 = float.MinValue;
			foreach (PlannedStep step in plan)
			{
				rx0 = Mathf.Min(rx0, step.Pos.x);
				rx1 = Mathf.Max(rx1, step.Pos.x);
				rz0 = Mathf.Min(rz0, step.Pos.z);
				rz1 = Mathf.Max(rz1, step.Pos.z);
			}
			if (rx1 >= rx0)
			{
				resumeSize = Mathf.Clamp(Mathf.Max(rx1 - rx0, rz1 - rz0) + 1.5f, 2f, ClearMaxSize);
			}
		}
		// E7 (player's request, 03/11/2026): THE WORKBENCH IS PLANNED, NOT ASSUMED. The game's rule is per piece
		// (CraftingStation.HaveBuildStationInRange from the piece's own pose) and the workbench covers 5 m: "there is a
		// bench near the site" - the old check, against the hint - passed while most of the chests of a 30 m house had
		// none. CommHomeBasicBP has ~70 chests on three floors and brings no workbench at all.
		missingStation = null;
		if (!resuming)
		{
			float extent = hasBounds ? Mathf.Max(maxX - minX, maxZ - minZ) : 0f;
			PlanStations(cs, plan, hint, extent * 0.5f + 2.5f, rot, Self(ai), ai);
		}
		// a station this build cannot make by itself (stonecutter, forge, black forge...) is still a refusal, and it names
		// the station instead of dying at the piece
		foreach (PlannedStep step in plan)
		{
			if (step.Piece == null || step.Piece.m_craftingStation == null || IsBenchKind(step.Piece.m_craftingStation.m_name))
			{
				continue;
			}
			if (CraftingStation.HaveBuildStationInRange(step.Piece.m_craftingStation.m_name, step.Pos) == null)
			{
				missingStation = step.Piece.m_craftingStation.m_name;
				CompanionsPlugin.Log?.LogInfo("[Build] blueprint " + blueprint.Label + ": a station is missing ("
					+ missingStation + ") - refusing before the first piece");
				return BlueprintStart.Failed;
			}
		}
		// E7 (player's call, 02/11/2026): LEVEL THE GROUND FIRST, like the templates do. A downloaded build on uneven
		// ground has its floors refused one after the other (no_support); the hoe job takes over and the build comes
		// back by itself, centred where the player aimed.
		if (!resuming && hasBounds && (ModConfig.BuildLevelGround == null || ModConfig.BuildLevelGround.Value))
		{
			// the level job works up to 20 m (its own clamp): measuring a 32 m footprint against a 20 m leveling made an
			// ENDLESS loop in the player's test (02/11/2026) - "leveling first" -> "already flat" -> back to the build
			// -> "leveling needed" again. The check uses the SAME area the job will work, and a second pass in a row
			// proceeds anyway (the pieces that do not fit are skipped and counted).
			float footprint = Mathf.Max(maxX - minX, maxZ - minZ) + 1.5f;
			float size = Mathf.Clamp(footprint, 2f, 20f);
			float tolerance = ModConfig.BuildLevelTolerance != null ? ModConfig.BuildLevelTolerance.Value : 0.35f;
			float minY;
			float maxY;
			float target;
			bool flat = BuildTerrain.FlatEnough(hint, size, tolerance, out minY, out maxY, out target);
			Log(ai, "blueprint footprint " + footprint.ToString("F1") + " m (leveling " + size.ToString("F1")
				+ " m), ground variation " + (maxY - minY).ToString("F2") + " m (tolerance " + tolerance.ToString("F2")
				+ ") - " + (flat ? "flat enough" : "leveling needed"));
			float tried;
			bool justTried = s_levelTried.TryGetValue(blueprint.File, out tried) && Time.time - tried < 90f;
			if (!flat && !justTried)
			{
				Humanoid humanoid = ai.GetComponent<Humanoid>();
				Inventory bag = humanoid != null ? humanoid.GetInventory() : null;
				if (bag != null && bag.HaveItem("$item_hoe") && BuildTerrain.Available()
					&& MHR_Level.Start(cs, hint, size, blueprint.File, true))
				{
					s_levelTried[blueprint.File] = Time.time;
					Log(ai, "the ground is uneven (" + (maxY - minY).ToString("F2") + " m in " + size.ToString("F1")
						+ " m) - leveling first");
					return BlueprintStart.Leveling;
				}
				Log(ai, "the ground is uneven (" + (maxY - minY).ToString("F2") + " m) and there is no hoe - building anyway");
			}
			else if (!flat)
			{
				Log(ai, "the ground is still uneven after leveling (" + (maxY - minY).ToString("F2")
					+ " m) - building anyway (the pieces that do not fit are skipped)");
			}
		}
		bool free = ModConfig.BuildBlueprintFree != null && ModConfig.BuildBlueprintFree.Value;
		int start = resuming ? Mathf.Clamp(startIndex, 0, plan.Count - 1) : 0;
		// E7 (player's request, 03/11/2026): CLEAR THE SITE FIRST. The trees, stumps and fallen logs inside the footprint
		// refuse the floors (no_support) and block the walking (the mover gives up after 8 s), and the leveling does not
		// cut anything: the player's 985-piece plan on a wooded hill got 8 "can't reach it" in a row and never got past
		// the first row. The harvest does the cutting (its directed target is the same one the radial uses).
		bool clear = !resuming && hasBounds && (ModConfig.BuildClearSite == null || ModConfig.BuildClearSite.Value);
		float clearSize = clear ? Mathf.Clamp(Mathf.Max(maxX - minX, maxZ - minZ) + 1.5f, 2f, ClearMaxSize) : resumeSize;
		State state = new State
		{
			Active = true,
			Start = Time.time,
			LastDrive = Time.time,
			StartMode = ModeOf(cs),
			Blueprint = blueprint.Label,
			BlueprintFile = blueprint.File,
			Free = free,
			Skipped = skipped,
			Hint = hint,
			Forward = forward,
			Anchor = hint,
			AnchorRot = rot,
			AnchorKnown = true,
			Plan = plan,
			Retry = new List<PlannedStep>(),
			Index = start,
			Prefab = plan[start].Prefab,
			Piece = plan[start].Piece,
			Pos = plan[start].Pos,
			Rot = plan[start].Rot,
			Phase = clear ? Phase.Clear : Phase.Goto,
			ClearCenter = hint,
			ClearSize = clearSize
		};
		DiscardState(ai);
		s_state[ai.GetInstanceID()] = state;
		DropPending(ai, cs);
		Log(ai, "blueprint " + blueprint.Label + ": " + plan.Count + " pieces near (" + F(hint.x) + ", " + F(hint.y) + ", " + F(hint.z) + ")"
			+ (start > 0 ? " - resuming at piece " + (start + 1) : string.Empty)
			+ (free ? " - BlueprintFree is on (no material)" : string.Empty));
		return BlueprintStart.Started;
	}

	internal static bool Active(Component c)
	{
		State state;
		return StateOf(c, out state) && state.Active;
	}

	/// <summary>E7: this companion's build order is <b>clearing the site</b> - the harvest is the tool then, so it is the
	/// only job allowed to run (it must not be held still by the order).</summary>
	internal static bool Clearing(Component c)
	{
		State state;
		return StateOf(c, out state) && state.Active && state.Phase == Phase.Clear;
	}

	/// <summary>
	/// The build state of the companion a component belongs to. The states are keyed by the <see cref="CompanionAI"/>'s
	/// instance id and the callers ask from other behaviours of the same GameObject (HarvestController, IdleController,
	/// CompanionTalk...): comparing the id of the <b>component</b> never matched, so <c>MHR_JobRules.OtherOrder</c>
	/// answered false while a build was running and the idle job kept walking - two movers over the same body, which is
	/// why the companion shook in place and stopped (player's report, 03/11/2026).
	/// </summary>
	/// <summary>E7 (review M2, 03/11/2026): a new order drops the build that was waiting for materials - it used to survive
	/// and resurrect itself (on its old anchor) as soon as the new order ended.</summary>
	private static void DropPending(CompanionAI ai, CompanionSetup cs)
	{
		if (ai == null)
		{
			return;
		}
		Pending pending;
		if (s_pending.TryGetValue(ai.GetInstanceID(), out pending) && pending != null)
		{
			s_pending.Remove(ai.GetInstanceID());
			RestoreFollow(ai, cs, pending);
			CompanionSetup.ClearOrderCap(cs);
			Log(ai, "a build waiting for materials was dropped (a new order came)");
		}
	}

	private static bool StateOf(Component c, out State state)
	{
		state = null;
		if (c == null)
		{
			return false;
		}
		CompanionAI ai = c as CompanionAI;
		if (ai == null)
		{
			ai = c.GetComponent<CompanionAI>();
		}
		return ai != null && s_state.TryGetValue(ai.GetInstanceID(), out state) && state != null;
	}

	internal static void Cancel(Component c)
	{
		if (c == null)
		{
			return;
		}
		CompanionAI owner = c as CompanionAI;
		if (owner == null)
		{
			owner = c.GetComponent<CompanionAI>();
		}
		if (owner == null)
		{
			return;
		}
		State state;
		Pending pending = null;
		if (s_state.TryGetValue(owner.GetInstanceID(), out state) && state != null && state.Active)
		{
			ClearScaffolds(owner, state);
			state.Active = false;
			s_state.Remove(owner.GetInstanceID());
			// E7 (review, 03/11/2026): the pending must fall even when there is no ACTIVE state (the gather handover removes
			// the state and leaves the watcher) - its key is the instance id, which Unity reuses
			if (s_pending.TryGetValue(owner.GetInstanceID(), out pending))
			{
				s_pending.Remove(owner.GetInstanceID());
			}
			CompanionAI ai = owner;
			if (ai != null)
			{
				MHR_Mover.Release(ai);
			}
			ReleaseHarvest(ai, state);
			// E6b: the gather turned Follow off - another order cancels the build and Follow has to come back
			RestoreFollow(ai, c.GetComponent<CompanionSetup>(), pending);
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
				// E7 (review, 03/11/2026): a temporary stair must not survive the order - "seguir", "para" and any other
				// order without a target come through here, and a piece left in the save is junk in the player's house
				ClearScaffolds(null, state);
				state.Active = false;
			}
			s_state.Remove(key);
		}
	}

	/// <summary>
	/// E7 (player's report, 03/11/2026: "he died and did not go on with the build"): the death has to be seen HERE, called from
	/// <c>CompanionSetup.OnCompanionDeath</c>, because <see cref="Drive"/> never runs on a dead body - the order was dropped by
	/// the respawn's re-initialisation (mode 0) without a pending being written. It saves the plan, removes the state and takes
	/// the temporary stairs with it.
	/// </summary>
	internal static void OnDeath(CompanionAI ai)
	{
		if (ai == null)
		{
			return;
		}
		State state;
		if (!s_state.TryGetValue(ai.GetInstanceID(), out state) || state == null)
		{
			return;
		}
		bool saved = TrySaveDeathPending(ai, state, ai.GetComponent<CompanionSetup>());
		ClearScaffolds(ai, state);
		state.Active = false;
		s_state.Remove(ai.GetInstanceID());
		MHR_Mover.Release(ai);
		if (!saved)
		{
			Log(ai, "the build ended with the companion's death (there was no plan to resume)");
		}
	}

	/// <summary>Saves the running plan as a pending that waits for the respawn. True when there was a plan to save.</summary>
	private static bool TrySaveDeathPending(CompanionAI ai, State state, CompanionSetup cs)
	{
		if (state == null || state.Plan == null || state.Plan.Count == 0
			|| (state.Blueprint == null && state.Template == null))
		{
			return false;
		}
		s_pending[ai.GetInstanceID()] = new Pending
		{
			Prefab = state.Prefab,
			Template = state.Template != null ? state.Template.Name : null,
			Blueprint = state.Blueprint,
			BlueprintFile = state.BlueprintFile,
			Hint = state.Hint,
			Anchor = state.AnchorKnown ? state.Anchor : state.Hint,
			AnchorRot = state.AnchorRot,
			Index = state.Index,
			Plan = state.Plan,
			Retry = state.Retry,
			Passes = state.Passes,
			AfterDeath = true,
			CompanionId = cs != null ? cs.MHR_ResolveCompanionId() : null,
			Start = Time.time,
			LastProgressAt = Time.time
		};
		Log(ai, "it died during the build - the order resumes after the respawn (piece " + (state.Index + 1) + "/"
			+ state.Plan.Count + ")");
		return true;
	}

	/// <summary>
	/// E7 (player's report, 03/11/2026: "he died and did not go on with the build"): the respawn may build a NEW object, so the
	/// pending saved under the old instance id is handed to this body. Called from <c>CompanionSetup.TryInit</c> with the
	/// permanent companion id.
	/// </summary>
	internal static void Reattach(CompanionSetup cs)
	{
		if (cs == null)
		{
			return;
		}
		CompanionAI ai = cs.GetComponent<CompanionAI>();
		if (ai == null)
		{
			return;
		}
		// NOT the cached one: in TryInit the permanent id is still empty (it is resolved later, on demand) -
		// MHR_ResolveCompanionId reads/creates it from the ZDO and is safe here (review of the test of 03/11/2026)
		string id = cs.MHR_ResolveCompanionId();
		if (string.IsNullOrEmpty(id))
		{
			return;
		}
		int key = ai.GetInstanceID();
		List<int> keys = new List<int>(s_pending.Keys);
		foreach (int old in keys)
		{
			if (old == key)
			{
				continue;
			}
			Pending pending;
			if (!s_pending.TryGetValue(old, out pending) || pending == null || !pending.AfterDeath || pending.CompanionId != id)
			{
				continue;
			}
			s_pending.Remove(old);
			pending.Start = Time.time;
			pending.LastProgressAt = Time.time;   // the clock restarts with the new body
			s_pending[key] = pending;
			CompanionsPlugin.Log?.LogInfo("[Build] the build order of \"" + id + "\" was handed to the companion that just came back");
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
			TrySaveDeathPending(ai, state, cs);
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
		else if (state.Phase != Phase.GatherWalk && state.Phase != Phase.Clear && Time.time - state.Start > Timeout)
		{
			// E7: the clearing has its own budget (ClearBudget) - a 60 s clock would end the order on the second tree
			why = "the order took too long";
		}
		// E6 (measured 01/11/2026): an encumbered companion cannot walk - the game's Encumbered effect slows it to a
		// crawl and MHR_Mover gives up after 8 s without progress, which used to end the order with "no site" (a lie).
		// BUG do teste do jogador (03/11/2026): CompanionCarry.Fraction faz Mathf.Clamp01, entao "Fraction > 1" NUNCA era
		// verdade - a fala hc_ai_build_too_heavy era codigo morto e o companheiro tentava andar carregado (o mover
		// desistia em 8 s e a ordem dizia "can't get to the spot", uma mentira). Compara o peso com o teto de verdade.
		// E7: while CLEARING the site the baggage is not an error - the wood of the chopped trees lands in the same bag
		// the build pays from, and the check fires on the first building frame anyway (with the honest numbers).
		if (why == null && state.Phase != Phase.Clear && humanoid != null && humanoid.GetInventory() != null
			&& humanoid.GetInventory().GetTotalWeight() > CompanionCarry.Max(humanoid))
		{
			Log(ai, "overweight: " + humanoid.GetInventory().GetTotalWeight().ToString("F0") + "/" + CompanionCarry.Max(humanoid).ToString("F0"));
			Finish(ai, state, "hc_ai_build_too_heavy", "encumbered", humanoid.GetInventory().GetTotalWeight().ToString("F0"), CompanionCarry.Max(humanoid).ToString("F0"));
			return false;
		}
		if (why == "the order took too long" && state.Blueprint != null)
		{
			// E7 (player's test, 02/11/2026): a 983-piece blueprint always has a piece the companion cannot reach in
			// 60 s (a roof corner, a spot inside the hill). Skipping it keeps the other 982 going - and the log says
			// WHERE, so a bad anchor is visible instead of "the order took too long"
			state.Skipped++;
			Log(ai, "skipping " + state.Prefab + " (took too long) at (" + F(state.Pos.x) + ", " + F(state.Pos.y) + ", "
				+ F(state.Pos.z) + ") - " + state.Skipped + " skipped");
			return Advance(ai, state);
		}
		if (why != null)
		{
			Finish(ai, state, "hc_ai_build_failed", why, LabelOf(state));
			return false;
		}

		switch (state.Phase)
		{
			case Phase.Level:
				return LevelGround(ai, state);
			case Phase.Clear:
				return ClearSite(ai, state, dt);
			case Phase.Find:
				return state.Template != null ? FindPlan(ai, state) : Find(ai, state);
			case Phase.Goto:
				return Goto(ai, state, cs, dt);
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
	/// E7 (player's request, 03/11/2026): <b>clear the build site</b> before the first piece. The trees, stumps and fallen
	/// logs inside the plan's footprint refuse the floors and block the walking, and the leveling does not cut anything.
	///
	/// <para>This only <b>picks the next obstacle and waits</b>: the cutting itself is the <see cref="HarvestController"/>'s
	/// <b>directed target</b> - the very path the radial uses to send the companion at one tree - so the axe, the tool
	/// tier, the walking, the swings and the drops are the game's own code (and the wood goes to the bag the build pays
	/// from, a bonus).</para>
	///
	/// <para>Every stop is deliberate: an obstacle the harvest refuses (no axe, or one below the tree's tier) is not tried
	/// again, one it cannot finish in <see cref="ClearPerObstacle"/> seconds is given up, and
	/// <see cref="ClearMaxObstacles"/> obstacles or <see cref="ClearBudget"/> seconds end the clearing - the build goes on
	/// and the pieces that do not fit are retried and skipped anyway.</para>
	/// </summary>
	private static bool ClearSite(CompanionAI ai, State state, float dt)
	{
		HarvestController harvest = ai.GetComponent<HarvestController>();
		if (harvest == null)
		{
			Log(ai, "there is no harvest to clear the site with - building as it is");
			state.Phase = state.Template != null ? Phase.Find : Phase.Goto;
			return true;
		}
		if (state.ClearStart <= 0f)
		{
			state.ClearStart = Time.time;
			state.ClearTotal = CountObstacles(state);
			if (state.ClearTotal == 0)
			{
				Log(ai, "the site is clear (nothing to cut inside " + F(state.ClearSize) + " m)");
				state.Phase = state.Template != null ? Phase.Find : Phase.Goto;
				return true;
			}
			Log(ai, "clearing the site: " + state.ClearTotal + " tree(s)/log(s) inside the " + F(state.ClearSize)
				+ " m footprint");
			Say(ai, "hc_ai_build_clearing", state.ClearTotal);
		}
		if (state.ClearTarget != null)
		{
			// the harvest stops the directed target to take a load to a chest when the bag is at the unload threshold
			// (measured 03/11/2026: "Too heavy - taking a load to chest", 44 s, and the 45 s timer gave up on a tree that
			// was fine). Overweight is not stuck: the clock waits.
			Humanoid body = ai.GetComponent<Humanoid>();
			Inventory pack = body != null ? body.GetInventory() : null;
			if (pack != null && pack.GetTotalWeight() >= CompanionCarry.UnloadThreshold(pack) - 2f)
			{
				// hauling a load to a chest is not the clearing working: neither the per-obstacle clock nor the order
				// budget may run (measured 03/11/2026: 261 s of budget, ~60 s of it carrying wood)
				state.ClearTargetAt = Time.time;
				state.ClearStart += dt;
			}
			// the object is destroyed when the tree falls (a stump and the logs are new objects): that is the "done"
			if (Time.time - state.ClearTargetAt > ClearPerObstacle)
			{
				state.ClearSkip.Add(state.ClearTarget.GetInstanceID());
				Log(ai, "giving up on \"" + state.ClearTarget.name + "\" (over " + ClearPerObstacle.ToString("F0") + " s)");
				harvest.CancelDirectedTarget();
				state.ClearTarget = null;
			}
			return true;
		}
		if (!ReferenceEquals(state.ClearTarget, null))
		{
			// the reference is still there but the object is gone (Unity's == null): the tree is down. Wait a moment so the
			// harvest grabs the drops of the cut (the wood is the same bag the build pays from) before the next tree.
			state.ClearTarget = null;
			state.ClearWait = Time.time + 2f;
			return true;
		}
		if (Time.time < state.ClearWait)
		{
			return true;
		}
		if (state.ClearCount >= ClearMaxObstacles || Time.time - state.ClearStart > ClearBudget)
		{
			harvest.CancelDirectedTarget();
			state.Phase = state.Template != null ? Phase.Find : Phase.Goto;
			// the order's clock follows the PIECE (the site is clear before the first one) - without this the first piece
			// (here the workbench the station pass added) was already far past the 60 s and was skipped as "took too long"
			state.Start = Time.time;
			state.Checked = false;
			Log(ai, "clearing stopped (" + state.ClearCount + " cut, " + (Time.time - state.ClearStart).ToString("F0")
				+ " s) - building with what is clear");
			return true;
		}
		GameObject next = NextObstacle(state);
		if (next == null)
		{
			// E7 (player's report, 03/11/2026: "the logs of the trees were left rolling in the site"): a felled tree
			// spawns its logs a moment after it is destroyed and a log keeps rolling, so an empty square is not the end.
			// One more look 2 s later, and the obstacles that were given up on get a second chance before the build.
			if (state.ClearEmptyScans < 1)
			{
				state.ClearEmptyScans = 1;
				state.ClearWait = Time.time + 2f;
				return true;
			}
			if (!state.ClearSkipTried && state.ClearSkip.Count > 0)
			{
				state.ClearSkipTried = true;
				int again = state.ClearSkip.Count;
				state.ClearSkip.Clear();
				state.ClearEmptyScans = 0;
				Log(ai, "second look at " + again + " obstacle(s) left behind");
				return true;
			}
			harvest.CancelDirectedTarget();
			state.Phase = state.Template != null ? Phase.Find : Phase.Goto;
			// the order's clock follows the PIECE: without this the first piece was already 156 s old when the clearing
			// ended and it was skipped as "took too long" (measured 03/11/2026)
			state.Start = Time.time;
			state.Checked = false;
			Log(ai, "the site is clear (" + state.ClearCount + " obstacle(s) in " + (Time.time - state.ClearStart).ToString("F0")
				+ " s)");
			return true;
		}
		if (!harvest.SetDirectedTarget(next))
		{
			// no axe / a tier below the tree: the harvest said no and would say no again next frame
			state.ClearSkip.Add(next.GetInstanceID());
			state.ClearRejects++;
			Log(ai, "can't cut \"" + next.name + "\" (no tool for it) - leaving it");
			if (state.ClearRejects >= ClearMaxRejects)
			{
				state.Phase = state.Template != null ? Phase.Find : Phase.Goto;
				Log(ai, "nothing here I can cut - building as it is");
			}
			return true;
		}
		state.ClearRejects = 0;
		state.ClearEmptyScans = 0;
		state.ClearCount++;
		state.ClearTarget = next;
		state.ClearTargetAt = Time.time;
		Log(ai, "clearing the site: cutting \"" + next.name + "\" (#" + state.ClearCount + ")");
		return true;
	}

	/// <summary>Is this the station the mod can build (the workbench)?</summary>
	private static bool IsBenchKind(string station)
	{
		return station != null && station.IndexOf("workbench", StringComparison.OrdinalIgnoreCase) >= 0;
	}

	/// <summary>The bench's own coverage, read from the prefab (<c>CraftingStation.m_rangeBuild</c>), with 20 m as the floor.</summary>
	private static float BenchRadius(Piece bench)
	{
		CraftingStation station = bench != null ? bench.GetComponent<CraftingStation>() : null;
		return station != null && station.m_rangeBuild > 0f ? station.m_rangeBuild : BenchDefaultRadius;
	}

	/// <summary>
	/// E7 (player's request, 03/11/2026): <b>every piece that needs a workbench is made to have one</b>. The pass walks the
	/// plan in order; for a piece with no workbench in range of <b>its own pose</b> (and none already planned near it) it
	/// <b>inserts a workbench right before it</b>, at a pose the game accepts. The executor builds it like any other piece:
	/// it is paid from the same bag/chests and the plan's shortfall counts it. Benches the blueprint itself brings cover
	/// what is around them.
	/// </summary>
	private static int PlanStations(CompanionSetup cs, List<PlannedStep> plan, Vector3 centre, float outside, Quaternion rot,
		Component who, CompanionAI ai)
	{
		Piece bench = BuildProbe.PieceOf(BenchPrefab);
		if (bench == null || plan == null || plan.Count == 0)
		{
			return 0;
		}
		float radius = BenchRadius(bench);
		List<Vector3> planned = new List<Vector3>();
		foreach (PlannedStep step in plan)
		{
			if (step.Prefab != null && IsBenchKind(step.Prefab))
			{
				planned.Add(step.Pos);
			}
		}
		int needed = 0;
		int added = 0;
		int tries = 0;
		for (int i = 0; i < plan.Count && added < MaxPlannedBenches && tries < MaxBenchSearches; i++)
		{
			PlannedStep step = plan[i];
			if (step.Piece == null || step.Piece.m_craftingStation == null || !IsBenchKind(step.Piece.m_craftingStation.m_name))
			{
				continue;
			}
			needed++;
			if (CraftingStation.HaveBuildStationInRange(step.Piece.m_craftingStation.m_name, step.Pos) != null)
			{
				continue;
			}
			if (CoveredBy(planned, step.Pos, radius))
			{
				continue;
			}
			// a bench INSIDE the house would take the place of a floor (the blueprint's own grid) - it goes OUTSIDE the
			// footprint, on the piece's side of the site, and its 20 m still cover the piece (the height is ignored by
			// HaveBuildStationInRange)
			Vector3 outward = step.Pos - centre;
			outward.y = 0f;
			if (outward.sqrMagnitude < 1f)
			{
				outward = new Vector3(1f, 0f, 0f);
			}
			Vector3 anchor = centre + outward.normalized * Mathf.Max(outside, 2.5f);
			Vector3 spot;
			Quaternion benchRot;
			tries++;
			if (!FindBenchSpot(bench, anchor, rot, who, out spot, out benchRot)
				&& !FindBenchSpot(bench, step.Pos, rot, who, out spot, out benchRot))
			{
				LogDebug(ai, "no room for a workbench next to the " + step.Prefab + " at (" + F(step.Pos.x) + ", "
					+ F(step.Pos.y) + ", " + F(step.Pos.z) + ") - it will be refused and retried");
				continue;
			}
			plan.Insert(i, new PlannedStep { Prefab = BenchPrefab, Piece = bench, Pos = spot, Rot = benchRot });
			planned.Add(spot);
			added++;
			i++;   // the bench that was just inserted is the step to look at now
			Log(ai, "workbench for the " + step.Prefab + " (none in range of it): added at (" + F(spot.x) + ", "
				+ F(spot.y) + ", " + F(spot.z) + ") - covers " + radius.ToString("F1") + " m");
		}
		if (needed > 0)
		{
			Log(ai, "station: " + needed + " piece(s) need a workbench, " + added + " workbench(es) added to the plan");
		}
		return added;
	}

	/// <summary>
	/// E7 (player's report, 03/11/2026): <b>insert a workbench right before the piece that was refused with
	/// <c>needs_station</c></b>. The station pass plans from the world as it was when the order started; a bench that is
	/// destroyed later (or the plan's own bench being far away) leaves part of a 32 m site without a station, and the
	/// pieces there were only retried - the wall of the house that never went up. The bench becomes the step being carried
	/// out; the refused piece comes right after it (one index down the plan).
	/// </summary>
	private static bool InsertBenchStep(CompanionAI ai, State state, CompanionSetup cs, string refusedPrefab)
	{
		Piece bench = BuildProbe.PieceOf(BenchPrefab);
		if (bench == null)
		{
			return false;
		}
		Vector3 centre = state.AnchorKnown ? state.Anchor : state.Pos;
		Vector3 outward = state.Pos - centre;
		outward.y = 0f;
		if (outward.sqrMagnitude < 1f)
		{
			outward = new Vector3(1f, 0f, 0f);
		}
		float outside = Mathf.Max(state.ClearSize * 0.5f, 6f) + 2.5f;
		Vector3 anchor = centre + outward.normalized * outside;
		Vector3 spot;
		Quaternion benchRot;
		if (!FindBenchSpot(bench, anchor, state.Rot, Self(ai), out spot, out benchRot)
			&& !FindBenchSpot(bench, state.Pos, state.Rot, Self(ai), out spot, out benchRot))
		{
			Log(ai, "no room for a workbench near the " + refusedPrefab + " - the piece is only retried");
			return false;
		}
		state.Plan.Insert(state.Index, new PlannedStep { Prefab = BenchPrefab, Piece = bench, Pos = spot, Rot = benchRot });
		state.BenchInserts++;
		state.Prefab = BenchPrefab;
		state.Piece = bench;
		state.Pos = spot;
		state.Rot = benchRot;
		state.Placed = null;
		state.Start = Time.time;
		state.Checked = false;
		state.Phase = Phase.Goto;
		Log(ai, "no workbench in range of the " + refusedPrefab + " - one added in front of it at (" + F(spot.x) + ", "
			+ F(spot.y) + ", " + F(spot.z) + ")");
		return true;
	}

	/// <summary>Is <paramref name="point"/> inside the coverage of one of the planned benches? <b>Horizontal</b> distance:
	/// <c>HaveBuildStationInRange</c> flattens the point's Y onto the station's Y (measured, `docs/re/building-notes.md`), so
	/// a bench on the ground floor covers the chests of the upper floors too.</summary>
	private static bool CoveredBy(List<Vector3> spots, Vector3 point, float radius)
	{
		for (int i = 0; i < spots.Count; i++)
		{
			float dx = spots[i].x - point.x;
			float dz = spots[i].z - point.z;
			if (Mathf.Sqrt(dx * dx + dz * dz) <= radius)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// A pose next to <paramref name="near"/> <b>at the piece's own height</b> (a bench inside the house stands on the floor,
	/// not on the terrain under it) that the game's placement rules accept. The four yaws are tried because the bench may
	/// not fit in one of them (the same thing a player does).
	/// </summary>
	private static bool FindBenchSpot(Piece bench, Vector3 near, Quaternion rot, Component who, out Vector3 spot,
		out Quaternion benchRot)
	{
		// the OUTDOOR spot is on the ground (a bench outside the house stands on the terrain, not at the floor's height);
		// the fallback (the piece's own spot) keeps its height, for a bench that has to go inside
		float groundY = ZoneSystem.instance != null ? ZoneSystem.instance.GetGroundHeight(near) : near.y;
		bool outdoors = Mathf.Abs(groundY - near.y) < 3f;
		for (int ring = 0; ring <= 1; ring++)
		{
			float distance = 1.6f * ring;
			for (int dir = 0; dir < 8; dir++)
			{
				float angle = Mathf.PI * 2f * dir / 8f;
				Vector3 candidate = new Vector3(near.x + Mathf.Cos(angle) * distance, outdoors ? groundY : near.y,
					near.z + Mathf.Sin(angle) * distance);
				for (int yaw = 0; yaw < 4; yaw++)
				{
					Quaternion candidateRot = rot * Quaternion.Euler(0f, yaw * 90f, 0f);
					Dictionary<string, object> check = BuildSite.Check(bench, candidate, candidateRot, who);
					if (Ok(check))
					{
						spot = candidate;
						benchRot = candidateRot;
						return true;
					}
				}
			}
		}
		spot = near;
		benchRot = rot;
		return false;
	}

	/// <summary>The companion's bag (the scaffold is paid from it, like any piece).</summary>
	private static Inventory BagOf(CompanionAI ai)
	{
		Humanoid body = ai != null ? ai.GetComponent<Humanoid>() : null;
		return body != null ? body.GetInventory() : null;
	}

	/// <summary>
	/// E7 (player's idea, 03/11/2026: "temporary ladders"): places a <b>temporary</b> wood stair next to the companion,
	/// rising towards the piece, so a piece above the game's 5 m eye rule (the ridge, the top of a gable, the last row of
	/// a roof) becomes reachable. It is a RAMP the mover walks up as geometry - no ladder logic and no landing to fall
	/// from (the trap of the same test). Paid like any piece (bag, then the site's chests), removed as soon as the piece it
	/// was placed for goes up (never with the companion within 1,5 m), and the end of the order only cleans leftovers: it is
	/// not part of the plan and does not stay in the player's house. At most <see cref="MaxScaffolds"/> for the whole ORDER,
	/// one every 4 s.
	/// </summary>
	private static bool TryScaffold(CompanionAI ai, State state, CompanionSetup cs)
	{
		if (ModConfig.BuildTempScaffold == null || !ModConfig.BuildTempScaffold.Value)
		{
			return false;
		}
		if (ai == null || state.ScaffoldTries >= MaxScaffolds || Time.time < state.NextScaffoldAt)
		{
			return false;
		}
		Piece stair = BuildProbe.PieceOf(ScaffoldPrefab);
		// the bag is resolved AFTER the guards: the rejected paths must not pay a GetComponent per frame (review, 03/11/2026)
		Inventory bag = BagOf(ai);
		if (stair == null || bag == null)
		{
			return false;
		}
		Vector3 me = ai.transform.position;
		// the cooldown is armed BEFORE the station test: without a workbench in range this method would scan every
		// station of the world on every frame otherwise (review, 03/11/2026)
		state.NextScaffoldAt = Time.time + 4f;
		// every piece needs a workbench in range in 1.0 and the stair is no exception - asking ONCE here saves up to 8 ghost
		// checks in the loop below (and says nothing when the plan's own bench does not reach this corner)
		if (stair.m_craftingStation != null
			&& CraftingStation.HaveBuildStationInRange(stair.m_craftingStation.m_name, me) == null)
		{
			return false;   // "not now": no attempt is spent, a bench may be built later
		}
		Vector3 toPiece = state.Pos - me;
		toPiece.y = 0f;
		if (toPiece.sqrMagnitude < 0.25f)
		{
			return false;
		}
		toPiece.Normalize();
		// measured with build.snaps (03/11/2026): the stair rises towards its own -Z, so its +Z faces AWAY from the piece
		Quaternion rot = Quaternion.LookRotation(-toPiece, Vector3.up);
		bool useChests = ModConfig.BuildUseChests == null || ModConfig.BuildUseChests.Value;
		Vector3 stock = state.Plan != null && state.AnchorKnown ? state.Anchor : state.Pos;
		// "not now" does NOT spend an attempt: the wood may still come from the gather or a chest (review, 03/11/2026)
		if (!state.Free && BuildStock.Missing(bag, stair, stock, cs, useChests).Count > 0)
		{
			return false;
		}
		int mask = ScaffoldMask();
		string last = null;
		for (int ring = 0; ring <= 1; ring++)
		{
			float distance = 1.2f + ring * 1.1f;
			for (int i = 0; i < 4; i++)
			{
				Vector3 candidate = me + Quaternion.Euler(0f, -35f + i * 23f, 0f) * toPiece * distance;
				RaycastHit hit;
				if (mask == 0
					|| !Physics.Raycast(new Vector3(candidate.x, me.y + 2f, candidate.z), Vector3.down, out hit, 3f, mask,
						QueryTriggerInteraction.Ignore))
				{
					continue;
				}
				candidate.y = hit.point.y + 0.02f;
				if (Mathf.Abs(candidate.y - me.y) > 1.2f)
				{
					continue;   // a floor too far above/below: he could not walk onto it
				}
				if (BlockedByPlan(state, candidate))
				{
					last = "blocked by the plan";
					continue;   // a planned piece (or one waiting in the retry queue) goes here
				}
				Dictionary<string, object> check = BuildSite.Check(stair, candidate, rot, cs);
				if (!Ok(check))
				{
					last = check != null ? Convert.ToString(check["status"]) : "unknown";
					continue;
				}
				if (!state.Free)
				{
					Dictionary<string, int> missing;
					if (!BuildStock.Take(bag, stair, stock, cs, useChests, out missing))
					{
						Log(ai, "no wood for a temporary stair");
						return false;
					}
				}
				BuildResult placed = BuildExecutor.Place(cs, stair, candidate, rot);
				if (!placed.Ok)
				{
					if (!state.Free)
					{
						Refund(bag, stair);
					}
					last = "executor: " + placed.Error;
					continue;
				}
				state.Scaffolds.Add(placed.GameObject);
				state.ScaffoldTries++;   // the budget counts STAIRS PLACED, not attempts (a 'blocked by the plan' is not a stair)
				state.BestDistance = 0f;
				state.BestAt = Time.time;
				Log(ai, "temporary stair at (" + F(candidate.x) + ", " + F(candidate.y) + ", " + F(candidate.z)
					+ ") to reach the " + state.Prefab + " above - it is removed when the order ends");
				return true;
			}
		}
		// say WHY there is no stair (the usual one is needs_station at the far side of a big plan) - once per 4 s, bounded
		// by MaxScaffolds for the whole order
		Log(ai, "no room for a temporary stair near the " + state.Prefab + " (" + (last ?? "no ground") + ")");
		return false;
	}

	/// <summary>Layer mask of the scaffold's ground probe (cached: <c>LayerMask.GetMask</c> allocates an array per call).</summary>
	private static int ScaffoldMask()
	{
		if (s_scaffoldMask < 0)
		{
			s_scaffoldMask = LayerMask.GetMask("Default", "static_solid", "piece", "terrain");
		}
		return s_scaffoldMask;
	}

	/// <summary>
	/// Is a planned piece going to take this spot? The scaffold must not steal a floor's place, and the queue that
	/// matters is not only <c>Plan</c>: a piece waiting in <c>Retry</c> comes back to that same pose (review, 03/11/2026).
	/// The test is 1,5 m in XZ and 2 m in Y - a piece 20 m above does not care about a stair on the ground.
	/// </summary>
	private static bool BlockedByPlan(State state, Vector3 spot)
	{
		if (NearPiece(state.Pos, spot))
		{
			return true;
		}
		if (state.Plan != null)
		{
			for (int i = state.Index + 1; i < state.Plan.Count; i++)
			{
				PlannedStep step = state.Plan[i];
				if (step != null && NearPiece(step.Pos, spot))
				{
					return true;
				}
			}
		}
		if (state.Retry != null)
		{
			for (int i = 0; i < state.Retry.Count; i++)
			{
				PlannedStep step = state.Retry[i];
				if (step != null && NearPiece(step.Pos, spot))
				{
					return true;
				}
			}
		}
		return false;
	}

	private static bool NearPiece(Vector3 piece, Vector3 spot)
	{
		float dx = piece.x - spot.x;
		float dz = piece.z - spot.z;
		return dx * dx + dz * dz < 2.25f && Mathf.Abs(piece.y - spot.y) < 2f;
	}

	/// <summary>
	/// Removes the temporary stairs of an order (end of the plan, cancellation, death - the death path ends in
	/// <see cref="Finish"/> too). The plan's own pieces stay, and the wood comes back to the bag: the stair is a tool, not a
	/// piece of the house. <paramref name="ai"/> may be null (CancelAll has no component to ask, and then nothing is
	/// refunded). A stair is KEPT only when a piece placed <b>next to it</b> has no support without it - see
	/// <see cref="StairHoldsPiece"/> (review, 03/11/2026).
	/// </summary>
	private static void ClearScaffolds(CompanionAI ai, State state)
	{
		if (state == null || state.Scaffolds.Count == 0)
		{
			return;
		}
		int removed = 0;
		int kept = 0;
		foreach (GameObject go in state.Scaffolds)
		{
			if (go == null)
			{
				continue;
			}
			if (StairHoldsPiece(state, go))
			{
				kept++;
				continue;
			}
			try
			{
				Piece piece = go.GetComponent<Piece>();
				ZNetView view = go.GetComponent<ZNetView>();
				bool gone = false;
				if (view != null && view.IsValid())
				{
					// the claim before the destroy is what makes ZNetScene.Destroy enqueue the ZDO - without it the other
					// clients keep the piece (ZNetView.Destroy is exactly ZNetScene.instance.Destroy)
					view.ClaimOwnership();
					view.Destroy();
					gone = true;
				}
				else if (ZNetScene.instance != null)
				{
					ZNetScene.instance.Destroy(go);
					gone = true;
				}
				else
				{
					UnityEngine.Object.Destroy(go);
				}
				// the wood comes back ONLY when the piece really left the world AND was really paid: with BlueprintFree the
				// stair is never paid (refunding would create wood), and the fallback destroy keeps the ZDO alive
				// (review, 03/11/2026)
				if (gone && !state.Free && ai != null && piece != null)
				{
					Refund(BagOf(ai), piece);
				}
				removed++;
			}
			catch (Exception ex)
			{
				CompanionsPlugin.Log?.LogWarning("[Build] could not remove a temporary stair: " + ex.Message);
			}
		}
		state.Scaffolds.Clear();
		state.Scaffolded.Clear();
		if (removed > 0)
		{
			CompanionsPlugin.Log?.LogInfo("[Build] " + removed + " temporary stair(s) removed");
		}
		if (kept > 0)
		{
			CompanionsPlugin.Log?.LogWarning("[Build] kept " + kept + " temporary stair(s): a piece placed next to it has no support without it");
		}
	}

	/// <summary>
	/// Does a piece accepted while THIS stair stood lose its support without it? The stair is switched off (its colliders)
	/// for the question: while it stands it is part of the support network (<c>WearNTear.m_supports</c>) and the answer
	/// would always be "supported" - that was the hole of the first version (review, 03/11/2026). Only the pieces within
	/// 4 m (XZ) and 3 m (Y) of the stair are asked, so one unsupported piece does not keep every stair of the order.
	/// </summary>
	private static bool StairHoldsPiece(State state, GameObject stair)
	{
		if (stair == null || state.Scaffolded.Count == 0)
		{
			return false;
		}
		Vector3 at = stair.transform.position;
		List<Collider> off = new List<Collider>();
		foreach (Collider collider in stair.GetComponentsInChildren<Collider>())
		{
			if (collider != null && collider.enabled)
			{
				collider.enabled = false;
				off.Add(collider);
			}
		}
		bool holds = false;
		List<WearNTear> asked = new List<WearNTear>();
		try
		{
			foreach (GameObject go in state.Scaffolded)
			{
				if (go == null)
				{
					continue;
				}
				Vector3 piece = go.transform.position;
				float dx = at.x - piece.x;
				float dz = at.z - piece.z;
				if (dx * dx + dz * dz > 16f || Mathf.Abs(at.y - piece.y) > 3f)
				{
					continue;
				}
				WearNTear wear = go.GetComponent<WearNTear>();
				if (wear == null)
				{
					continue;
				}
				asked.Add(wear);
				if (BuildSupport.Supported(wear) == false)
				{
					holds = true;
					break;
				}
			}
		}
		finally
		{
			foreach (Collider collider in off)
			{
				if (collider != null)
				{
					collider.enabled = true;
				}
			}
			// the question WROTE WearNTear.m_support for the pieces asked with the stair still off - ask again now that
			// it is back, or the piece we decided to protect would keep the low number the game uses to break it
			// (review, 03/11/2026)
			foreach (WearNTear wear in asked)
			{
				if (wear != null)
				{
					BuildSupport.Supported(wear);
				}
			}
		}
		return holds;
	}

	/// <summary>
	/// E7 (player's requests, 03/11/2026: "he has to know he should place the piece at the top of the ladder and build the floor
	/// there" and "he can build the floor from the bottom up too, he does not need to wait until he is at the floor height"): the
	/// floors that land on <paramref name="topY"/> are moved to the FRONT of the plan, right after the piece just placed. The
	/// storey order builds the next floor only when the whole storey below is done, so the upper floor waited and the ladder sat
	/// in the void; with the floor pulled forward it is built from the level the companion is standing on (the game places a
	/// piece up to 5 m from the eye, so a floor 2 m above his feet is within reach) and the pieces upstairs stop failing with
	/// no_support.
	/// </summary>
	/// <summary>The top of the ladder just placed: the <b>highest collider</b> of the piece (a prefab may have more than one
	/// renderer and the first is not always the body), falling back to the renderer, then to <c>Pos.y + 2</c>.</summary>
	private static float LadderTop(State state)
	{
		if (state.Placed != null)
		{
			try
			{
				float top = float.MinValue;
				foreach (Collider collider in state.Placed.GetComponentsInChildren<Collider>())
				{
					if (collider != null && collider.bounds.max.y > top)
					{
						top = collider.bounds.max.y;
					}
				}
				if (top > float.MinValue)
				{
					return top;
				}
			}
			catch (Exception)
			{
			}
			try
			{
				Renderer renderer = state.Placed.GetComponentInChildren<Renderer>();
				if (renderer != null)
				{
					return renderer.bounds.max.y;
				}
			}
			catch (Exception)
			{
			}
		}
		return state.Pos.y + 2f;
	}

	/// <summary>
	/// E7 (player's requests, 03/11/2026: "he has to know he should place the piece at the top of the ladder and build the floor
	/// there" and "he can build the floor from the bottom up too, he does not need to wait until he is at the floor height"): the
	/// floor pieces that land on <paramref name="topY"/> are moved to the FRONT of the plan, right after the piece just placed -
	/// the nearest ones first (a landing needs one tile), and on a ladder only the side it rises to. The plan is rebuilt in place
	/// (<c>Clear</c> + <c>AddRange</c>) because <c>Pending.Plan</c> holds the same list reference for a resumed order.
	///
	/// <para>The first version moved the pieces with <c>RemoveAt</c>/<c>Insert</c> while walking the list backwards: the indexes
	/// stayed valid but the neighbour just below the cursor was skipped and the order came out reversed - and the skipped one was
	/// exactly the tile nearest the ladder (review with a simulation, 03/11/2026).</para>
	/// </summary>
	private static void PullFloorsAbove(CompanionAI ai, State state, float topY, bool ladder)
	{
		if (state.Plan == null || state.Placed == null || state.Index + 1 >= state.Plan.Count)
		{
			return;
		}
		Vector3 rise = ladder ? state.Rot * Vector3.back : Vector3.zero;   // a ladder rises towards its own -Z
		List<int> candidates = new List<int>();
		List<float> distance = new List<float>();
		for (int i = state.Index + 1; i < state.Plan.Count; i++)
		{
			PlannedStep step = state.Plan[i];
			if (step == null || step.Prefab == null || step.Prefab.IndexOf("floor", StringComparison.OrdinalIgnoreCase) < 0)
			{
				continue;
			}
			float dx = step.Pos.x - state.Pos.x;
			float dz = step.Pos.z - state.Pos.z;
			float flat = dx * dx + dz * dz;
			if (flat > 6.25f || Mathf.Abs(step.Pos.y - topY) > 1.2f)
			{
				continue;
			}
			if (ladder && rise.sqrMagnitude > 0.01f && dx * rise.x + dz * rise.z < -0.5f)
			{
				continue;   // behind the ladder, on the side it does not climb to
			}
			int at = candidates.Count;
			while (at > 0 && distance[at - 1] > flat)
			{
				at--;
			}
			candidates.Insert(at, i);
			distance.Insert(at, flat);
		}
		if (candidates.Count == 0)
		{
			return;
		}
		int take = Mathf.Min(MaxPulledFloors, candidates.Count);
		HashSet<int> chosen = new HashSet<int>();
		List<PlannedStep> pulled = new List<PlannedStep>();
		for (int i = 0; i < take; i++)
		{
			chosen.Add(candidates[i]);
			pulled.Add(state.Plan[candidates[i]]);
		}
		List<PlannedStep> head = state.Plan.GetRange(0, state.Index + 1);
		List<PlannedStep> rest = new List<PlannedStep>();
		for (int i = state.Index + 1; i < state.Plan.Count; i++)
		{
			if (!chosen.Contains(i))
			{
				rest.Add(state.Plan[i]);
			}
		}
		state.Plan.Clear();
		state.Plan.AddRange(head);
		state.Plan.AddRange(pulled);
		state.Plan.AddRange(rest);
		Log(ai, pulled.Count + " floor piece(s) over the " + state.Prefab + " brought forward so it leads somewhere");
	}

	/// <summary>
	/// E7 (player's report, 03/11/2026: "a ladder with no floor again"): a piece refused with <c>no_support</c> needs what is
	/// directly under it. The pieces in the plan within 1,5 m in XZ and 0,5-2,6 m below it (up to 3) are moved to right after
	/// the current step, so they are built BEFORE it comes back from the retry queue. Nothing is added or removed.
	/// </summary>
	private static bool PullSupports(CompanionAI ai, State state)
	{
		if (state.Plan == null || state.Index + 1 >= state.Plan.Count)
		{
			return false;
		}
		List<int> found = new List<int>();
		List<PlannedStep> pulled = new List<PlannedStep>();
		for (int i = state.Index + 1; i < state.Plan.Count && found.Count < 3; i++)
		{
			PlannedStep step = state.Plan[i];
			if (step == null || step.Prefab == null)
			{
				continue;
			}
			float dx = step.Pos.x - state.Pos.x;
			float dz = step.Pos.z - state.Pos.z;
			if (dx * dx + dz * dz > 2.25f)
			{
				continue;
			}
			float dy = state.Pos.y - step.Pos.y;
			if (dy < 0.5f || dy > 2.6f)
			{
				continue;
			}
			found.Add(i);
			pulled.Add(step);
		}
		if (pulled.Count == 0)
		{
			return false;
		}
		for (int i = found.Count - 1; i >= 0; i--)
		{
			state.Plan.RemoveAt(found[i]);
		}
		state.Plan.InsertRange(state.Index + 1, pulled);
		Log(ai, pulled.Count + " support piece(s) under the " + state.Prefab + " brought forward so it can stand");
		return true;
	}

	/// <summary>Is this pose close enough to a temporary stair to have been held up by it? (the same 4 m / 3 m window)</summary>
	private static bool NearScaffold(State state, Vector3 pos, float radius = 6f)
	{
		foreach (GameObject stair in state.Scaffolds)
		{
			if (stair == null)
			{
				continue;
			}
			Vector3 at = stair.transform.position;
			float dx = at.x - pos.x;
			float dz = at.z - pos.z;
			if (dx * dx + dz * dz < radius * radius && Mathf.Abs(at.y - pos.y) < 3f)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// E7 (review, 03/11/2026): a NEW build order replaces the state of the running one, so it takes the temporary stairs
	/// of the old one with it - otherwise "build the house" followed by "build the bench" left the stairs in the world.
	/// </summary>
	private static void DiscardState(CompanionAI ai)
	{
		if (ai == null)
		{
			return;
		}
		State old;
		if (!s_state.TryGetValue(ai.GetInstanceID(), out old) || old == null)
		{
			return;
		}
		ClearScaffolds(ai, old);
		old.Active = false;
		s_state.Remove(ai.GetInstanceID());
		MHR_Mover.Release(ai);
	}



	/// <summary>Cancels the directed harvest the clearing set (any ending of the order must not leave the companion
	/// swinging at a tree of a build that no longer exists).</summary>
	private static void ReleaseHarvest(CompanionAI ai, State state)
	{
		if (ai == null || state == null || (state.Phase != Phase.Clear && state.ClearTarget == null))
		{
			return;
		}
		HarvestController harvest = ai.GetComponent<HarvestController>();
		if (harvest != null)
		{
			harvest.CancelDirectedTarget();
		}
		state.ClearTarget = null;
	}

	/// <summary>The nearest tree/log inside the footprint that has not been given up yet (null when none).</summary>
	private static GameObject NextObstacle(State state)
	{
		float half = state.ClearSize * 0.5f;
		GameObject best = null;
		float bestDistance = float.MaxValue;
		foreach (TreeBase tree in UnityEngine.Object.FindObjectsByType<TreeBase>(FindObjectsSortMode.None))
		{
			// a standing tree is a point (its pivot is the trunk)
			ConsiderObstacle(state, tree != null ? tree.gameObject : null, half, false, ref best, ref bestDistance);
		}
		foreach (TreeLog log in UnityEngine.Object.FindObjectsByType<TreeLog>(FindObjectsSortMode.None))
		{
			// a LOG is a body: its pivot sits at one end and a log that rolled lies partly outside the square - the
			// bounds decide, otherwise a trunk inside the site counts as outside (player's report, 03/11/2026)
			ConsiderObstacle(state, log != null ? log.gameObject : null, half, true, ref best, ref bestDistance);
		}
		foreach (Destructible stub in UnityEngine.Object.FindObjectsByType<Destructible>(FindObjectsSortMode.None))
		{
			// E7 (player's report, 03/11/2026: "some stumps were left"): chopping a tree leaves a stub - a Destructible whose
			// name says "stub" (the same rule the harvest uses to classify it) - and a stump under a floor refuses the piece
			// exactly like a log. A stump does not move, so the pivot is enough.
			if (stub == null || stub.gameObject.name.IndexOf("stub", StringComparison.OrdinalIgnoreCase) < 0)
			{
				continue;
			}
			ConsiderObstacle(state, stub.gameObject, half, false, ref best, ref bestDistance);
		}
		return best;
	}

	private static int CountObstacles(State state)
	{
		float half = state.ClearSize * 0.5f;
		int count = 0;
		foreach (TreeBase tree in UnityEngine.Object.FindObjectsByType<TreeBase>(FindObjectsSortMode.None))
		{
			if (IsObstacle(state, tree != null ? tree.gameObject : null, half, false))
			{
				count++;
			}
		}
		foreach (TreeLog log in UnityEngine.Object.FindObjectsByType<TreeLog>(FindObjectsSortMode.None))
		{
			if (IsObstacle(state, log != null ? log.gameObject : null, half, true))
			{
				count++;
			}
		}
		foreach (Destructible stub in UnityEngine.Object.FindObjectsByType<Destructible>(FindObjectsSortMode.None))
		{
			if (stub != null && stub.gameObject.name.IndexOf("stub", StringComparison.OrdinalIgnoreCase) >= 0
				&& IsObstacle(state, stub.gameObject, half, false))
			{
				count++;
			}
		}
		return count;
	}

	private static void ConsiderObstacle(State state, GameObject go, float half, bool byBounds, ref GameObject best,
		ref float bestDistance)
	{
		if (!IsObstacle(state, go, half, byBounds))
		{
			return;
		}
		float distance = Vector3.Distance(state.ClearCenter, go.transform.position);
		if (distance < bestDistance)
		{
			bestDistance = distance;
			best = go;
		}
	}

	/// <summary>Is the object inside the clearing square? A standing tree is judged by its pivot; a log (or anything with
	/// a body) by its <b>bounds</b>, because a trunk lying in the site can have its pivot outside it.</summary>
	private static bool IsObstacle(State state, GameObject go, float half, bool byBounds)
	{
		if (go == null || state.ClearSkip.Contains(go.GetInstanceID()))
		{
			return false;
		}
		Vector3 centre = state.ClearCenter;
		if (!byBounds)
		{
			Vector3 point = go.transform.position;
			return Mathf.Abs(point.x - centre.x) <= half && Mathf.Abs(point.z - centre.z) <= half;
		}
		Vector3 pivot = go.transform.position;
		// the bounds of every log in the world would be a lot of work per scan: the pivot filter keeps it local (a log is
		// never 12 m long)
		float margin = half + 12f;
		if (Mathf.Abs(pivot.x - centre.x) > margin || Mathf.Abs(pivot.z - centre.z) > margin)
		{
			return false;
		}
		Bounds bounds = BoundsOf(go);
		if (bounds.size == Vector3.zero)
		{
			return Mathf.Abs(pivot.x - centre.x) <= half && Mathf.Abs(pivot.z - centre.z) <= half;
		}
		return bounds.max.x >= centre.x - half && bounds.min.x <= centre.x + half
			&& bounds.max.z >= centre.z - half && bounds.min.z <= centre.z + half;
	}

	/// <summary>The object's own box (every renderer below it), or a zero-size box at its pivot when it has none.</summary>
	private static Bounds BoundsOf(GameObject go)
	{
		Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
		Bounds box = new Bounds(go.transform.position, Vector3.zero);
		bool any = false;
		for (int i = 0; i < renderers.Length; i++)
		{
			Renderer renderer = renderers[i];
			if (renderer == null)
			{
				continue;
			}
			if (!any)
			{
				box = renderer.bounds;
				any = true;
			}
			else
			{
				box.Encapsulate(renderer.bounds);
			}
		}
		return box;
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
		// E7: the template's order is level -> clear -> find (a blueprint levels before the state exists and clears first)
		state.Phase = state.NeedsClear ? Phase.Clear : Phase.Find;
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
				// E6d (player's test, 02/11/2026): "ele foi em outro terreno". The ring was 4 m and the house could end up
				// 4 m (level) + 8 m (anchor search) away from where the player aimed. It stays close now: a small ring to
				// dodge a bump, never another spot.
				float angle = Mathf.PI * 2f * (i - 1) / 8f;
				candidate = state.Hint + new Vector3(Mathf.Cos(angle) * 1.5f, 0f, Mathf.Sin(angle) * 1.5f);
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
		// E7 (review C1, 03/11/2026): the leveling may GIVE UP (the soft level has a floor) and the order comes back here -
		// without this guard it is an endless level -> build -> level (the blueprint path has s_levelTried for exactly
		// this; a template uses its name). One try per 90 s; after that the house is built with the ground it has.
		float templateTriedAt;
		if (s_levelTried.TryGetValue("template:" + state.Template.Name, out templateTriedAt) && Time.time - templateTriedAt < 90f)
		{
			Log(ai, "the ground is still uneven (" + bestVariation.ToString("F2") + " m) after leveling - building anyway");
			return true;
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
		s_levelTried["template:" + state.Template.Name] = Time.time;
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
		// E6d (player's test, 02/11/2026): the level job is centred on the ANCHOR (see StartPlan), so the footprint plus a
		// small margin is all the square needs - a corner of the house outside the leveled square was refused with
		// "no_support" and the 6x6 stopped at 2/46.
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

	private static bool Goto(CompanionAI ai, State state, CompanionSetup cs, float dt)
	{
		// E7 (player's report, 03/11/2026: "he is only walking around the site"): the upper storeys of a blueprint were
		// walked to one by one and refused with no_support - 60 in a row across 40 s - because what holds a 2nd/3rd floor
		// up is not up yet. The verdict is the SAME from far away (the pose is the piece's, not the companion's), so the
		// piece is deferred to the retry pass BEFORE the walk: one cheap raycast instead of a 20 m round trip.
		// E7 (player's idea, 03/11/2026: "the ladder of the build must be made from the floor, like it was in the
		// beginning"): a ladder IS a piece of the house - it is placed when the plan reaches it, standing on the floor (the
		// support is the floor below, which the plan built before it). What must NOT happen is the MOVER climbing a ladder
		// whose top has no landing yet: that is what killed him in the hall of a blueprint, and it is guarded in MHR_Mover
		// (LadderLanding). The piece used to be deferred to the retry pass, which left the house without a ladder for hours.
		if (!state.Checked && cs != null && (state.Template != null || state.Blueprint != null)
			&& state.Plan != null && state.Index > 0)
		{
			state.Checked = true;
			Dictionary<string, object> early = BuildSite.Check(state.Piece, state.Pos, state.Rot, cs);
			if (early != null && !Ok(early) && Convert.ToString(early["status"]) == "no_support")
			{
				// E7 (player's report, 03/11/2026: "a ladder with no floor again"): the floor over a ladder is pulled forward and
				// then refused with no_support because the frame UNDER it is still ahead in the plan - the supports come first.
				PullSupports(ai, state);
				LogDebug(ai, "deferring " + state.Prefab + " at (" + F(state.Pos.x) + ", " + F(state.Pos.y) + ", "
					+ F(state.Pos.z) + "): it needs what is below it, which is not up yet - retry pass");
				QueueRetry(ai, state, "no_support");
				return Advance(ai, state);
			}
		}
		Vector3 me = ai.transform.position;
		// E7 (same test): a big drop while walking to the piece means the way is a trap (a ladder into the void, a hole) -
		// two of them on the same piece and it is skipped instead of tried again (it fell to death in a loop)
		if (state.LastY > 0.01f && me.y < state.LastY - 0.8f)
		{
			state.FallDrops++;
			if (state.FallDrops >= 2 && state.Blueprint != null)
			{
				Log(ai, "falling from the way to the " + state.Prefab + " - skipping it (" + state.FallDrops + " fall(s))");
				state.Skipped++;
				state.FallDrops = 0;
				return Advance(ai, state);
			}
		}
		state.LastY = me.y;
		// E7 (same report): walking to a piece it can never get closer to (a roof tip behind the ridge, a beam over a hole)
		// burned the whole 60 s timeout and looked like "struggling". 12 s without getting 0,5 m closer and the piece goes
		// to the retry pass - it may stand once the neighbours are up.
		float walkGap = Vector3.Distance(me, state.Pos);
		if (state.BestDistance <= 0f || walkGap < state.BestDistance - 0.5f)
		{
			state.BestDistance = walkGap;
			state.BestAt = Time.time;
		}
		else if (state.Blueprint != null && Time.time - state.BestAt > 12f)
		{
			// E7 (player's idea, 03/11/2026 - revised after the player's test: "the site filled with little stairs"): a stair
			// only helps when the piece is ABOVE the eye's 5 m - if he is stuck for any other reason (a wall, a hole) it would
			// be junk. And it is placed only here, after 12 s of real struggle, never just because he walked past.
			Vector3 eyeNow = new Vector3(me.x, me.y + EyeHeight, me.z);
			if (new Vector2(me.x - state.Pos.x, me.z - state.Pos.z).magnitude <= MaxPlace
				&& Vector3.Distance(eyeNow, state.Pos) > MaxPlace
				&& TryScaffold(ai, state, cs))
			{
				return true;
			}
			Log(ai, "no progress towards the " + state.Prefab + " for 12 s (" + F(walkGap) + " m away) - retrying it later");
			QueueRetry(ai, state, "no progress");
			return Advance(ai, state);
		}
		float flat = new Vector2(me.x - state.Pos.x, me.z - state.Pos.z).magnitude;
		// the game places a piece up to 5 m from the EYE (Player.m_maxPlaceDistance); the companion's eye is ~1,5 m
		// above its feet, so a roof 4,9 m up is within reach even though the feet are 4,9 m below it
		Vector3 eye = new Vector3(me.x, me.y + EyeHeight, me.z);
		// E7 (player's report, 03/11/2026: "he is still struggling to place a piece on the high roof"): the flat cap was
		// Reach + 0,5 = 3,0 m while the game allows 5 m from the EYE - the gable top at 42,8 with the companion standing
		// 3,15 m away flat (3,3 m in 3D) was refused by 15 cm and he walked in circles for 12 s. The eye rule is the
		// game's own; the flat cap only has to keep him from placing through the far side of the house.
		bool inReach = flat <= Reach + 0.5f && Vector3.Distance(eye, state.Pos) <= MaxPlace;
		// E7 (player's report, 03/11/2026: "he is still struggling to place a piece on the high roof"): the flat cap of 3,0 m
		// was stricter than the game (5 m from the eye) and refused a gable top 3,15 m away by 15 cm. The relaxed cap is used
		// ONLY when he stopped getting closer for 3 s: the normal walk still ends at Reach, so a piece is not placed from
		// 4,5 m just because he walked past the radius.
		if (!inReach && flat <= MaxPlace - 0.5f && Vector3.Distance(eye, state.Pos) <= MaxPlace
			&& Time.time - state.BestAt > 3f)
		{
			inReach = true;
		}
		if (inReach)
		{
			ai.StopMoving();
			MHR_Mover.Release(ai);
			state.Phase = Phase.Place;
			return true;
		}
		// E7 (player's idea, 03/11/2026): the "pulinho" - a player jumps to reach a beam that is just above the game's 5 m
		// rule; the eye goes up with the body, so the same rule then covers it. Only with the feet already in place and the
		// piece at most 1 m beyond the reach (a hop, not a flight), with a cooldown so it is not a bunny hop.
		if (flat <= Reach + 0.5f && Time.time >= state.NextJumpAt)
		{
			Character body = ai.GetComponent<Character>();
			float gap = Vector3.Distance(eye, state.Pos);
			if (body != null && !body.IsDead() && body.IsOnGround() && gap > MaxPlace && gap <= MaxPlace + 1.0f)
			{
				body.Jump(false);
				state.NextJumpAt = Time.time + 0.9f;
				LogDebug(ai, "a hop to reach the " + state.Prefab + " at " + (gap - MaxPlace).ToString("F1") + " m beyond the reach");
			}
		}
		MHR_Mover.Go(ai, dt, state.Pos, Reach, true, "Build");
		if (MHR_Mover.GaveUp(ai))
		{
			// "no site" was a lie here: the spot is fine, the way to it is not (a wall, a tree, the furniture the player
			// built, or the companion being too heavy to walk) - measured 01/11/2026
			Log(ai, "can't get to the spot");
			if (state.Blueprint != null)
			{
				QueueRetry(ai, state, "can't reach it");
				return Advance(ai, state);
			}
			Finish(ai, state, "hc_ai_build_cant_reach", null, LabelOf(state));
			return false;
		}
		return true;
	}

	private static bool Place(CompanionAI ai, State state, CompanionSetup cs, Inventory bag)
	{
		// E6: a plan step is checked AGAIN now, with the pieces the plan already built standing - the roof's support
		// comes from the walls, and up-front they were only planned (measured: the roof was refused with no_support)
		if ((state.Template != null || state.Blueprint != null) && state.Plan != null && state.Index > 0)
		{
			Dictionary<string, object> check = BuildSite.Check(state.Piece, state.Pos, state.Rot, cs);
			if (!Ok(check))
			{
				string status = check != null ? Convert.ToString(check["status"]) : "unknown";
				// E6d (player's test, 02/11/2026): the 6x6 floor was refused with no_support on flat ground and the same
				// pose passes BuildSite.Check from the bridge - the line carries the pose and the ground it hit so the
				// next test says WHY instead of guessing
				try
				{
					Dictionary<string, object> ground = check != null && check.ContainsKey("ground") ? check["ground"] as Dictionary<string, object> : null;
					Log(ai, "piece " + state.Prefab + " refused now (" + status + ") at (" + F(state.Pos.x) + ", " + F(state.Pos.y) + ", "
						+ F(state.Pos.z) + ") yaw " + F(state.Rot.eulerAngles.y) + (ground != null
							? " | ground=" + Convert.ToString(ground["name"]) + " drop=" + Convert.ToString(ground["drop"])
								+ " heightmap=" + Convert.ToString(ground["isHeightmap"]) + " wear=" + Convert.ToString(ground["wearSupports"])
							: string.Empty) + " - the plan stops");
				}
				catch (Exception)
				{
					Log(ai, "piece " + state.Prefab + " refused now (" + status + ") - the plan stops");
				}
				// E7 (player's report, 03/11/2026): a piece refused with needs_station gets a workbench inserted RIGHT BEFORE
				// it (the station pass only reads the world as it was when the order started; a bench destroyed later - or
				// the plan's own bench being far - left the far side of the site with none, and the piece was only retried)
				if (status == "needs_station" && state.Blueprint != null && state.Plan != null && state.Piece != null
					&& state.Piece.m_craftingStation != null && IsBenchKind(state.Piece.m_craftingStation.m_name)
					&& state.BenchInserts < MaxPlannedBenches)
				{
					string refusedPrefab = state.Prefab;
					if (InsertBenchStep(ai, state, cs, refusedPrefab))
					{
						return true;   // the bench is the step being carried out now
					}
				}
				// E7 (player's test, 03/11/2026): a BLUEPRINT never ends on needs_station - the workbench the station pass
				// added may still be ahead in the plan, and the piece is retried at the end (the order died here: bench
				// skipped, first floor refused, whole 985-piece house lost)
				if (status == "needs_station" && state.Blueprint == null && state.Piece != null && state.Piece.m_craftingStation != null)
				{
					// say WHICH station is missing, not "it doesn't fit" (E6: a plan may bring its own, and if it did
					// not reach the spot the player has to know what is missing)
					string station = Localization.instance != null ? Localization.instance.Localize(state.Piece.m_craftingStation.m_name) : state.Piece.m_craftingStation.m_name;
					Finish(ai, state, "hc_ai_build_needs_station", status, station);
					return false;
				}
				if (state.Blueprint != null)
				{
					// E7: a blueprint is a build somebody else designed - a piece that does not fit in THIS spot (or that
					// the terrain refuses) is retried at the end (the neighbours may not be up yet), not a reason to drop
					// the whole build
					QueueRetry(ai, state, status);
					return Advance(ai, state);
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
		// E7 (player's report, 03/11/2026: "he went to chop wood instead of taking it from the chests next to the player"):
		// the chests are the SITE's stock, so they are looked for around the site's ANCHOR, not around the piece. At the far
		// corner of a 32 m house the base chests were ~35 m away - outside the 30 m search - and the companion went
		// gathering with more than a thousand wood still in them.
		Vector3 stock = state.Plan != null && state.AnchorKnown ? state.Anchor : state.Pos;
		Dictionary<string, int> missing;
		if (!state.Free && !BuildStock.Take(bag, state.Piece, stock, cs, useChests, out missing))
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
					// E6b (player's test, 02/11/2026): the line says the number the GATHER was asked for (the rest of the
					// plan), not the piece's 1 wood - the player heard "1" while the companion would bring 75
					string asked = state.GatherItem != null
						? (Localization.instance != null ? Localization.instance.Localize(state.GatherItem) : state.GatherItem) + " x" + state.GatherAmount
						: MissingText(missing);
					Finish(ai, state, "hc_ai_build_collecting", "gathering first", asked, LabelOf(state));
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
			if (state.Blueprint != null)
			{
				QueueRetry(ai, state, "verification: " + reason);
				return Advance(ai, state);
			}
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
			if (state.Blueprint != null)
			{
				QueueRetry(ai, state, "no support");
				return Advance(ai, state);
			}
			Finish(ai, state, "hc_ai_build_unsupported", "no support", LabelOf(state));
			return false;
		}
		Log(ai, "done: " + state.Prefab + " at (" + F(state.Pos.x) + ", " + F(state.Pos.y) + ", " + F(state.Pos.z) + ") yaw " + F(state.Rot.eulerAngles.y)
			+ (state.Template != null ? " (" + (state.Index + 1) + "/" + state.Plan.Count + ")" : string.Empty)
			+ (supported.HasValue ? " supported" : string.Empty));
		state.PlacedCount++;
		// E7 (player's requests, 03/11/2026: "he has to know he should place the piece at the top of the ladder and build
		// the floor there" + "he can build the floor from the bottom up too, he does not need to wait until he is at the floor
		// height"): a floor that lands on what was just placed comes to the front of the plan, so the upper floor is built
		// from the level he is standing on instead of waiting for the whole storey below to finish
		bool isLadder = state.Prefab != null && state.Prefab.IndexOf("ladder", StringComparison.OrdinalIgnoreCase) >= 0;
		float above = float.NaN;
		if (isLadder)
		{
			above = LadderTop(state);
		}
		else if (state.Placed != null)
		{
			WearNTear support = state.Placed.GetComponent<WearNTear>();
			if (support != null && support.m_supports)
			{
				above = state.Pos.y + 2f;   // one storey (the game's grid is 2 m)
			}
		}
		if (!float.IsNaN(above))
		{
			PullFloorsAbove(ai, state, above, isLadder);
		}
		bool usedStairs = state.Scaffolds.Count > 0 && NearScaffold(state, state.Pos);
		if (usedStairs && state.Placed != null)
		{
			state.Scaffolded.Add(state.Placed);
		}
		// E7 (player's test, 03/11/2026: "the site filled with little stairs"): the stair did its job - it goes away NOW,
		// not at the end of the order. ClearScaffolds keeps it only when this very piece has no support without it.
		// E7 (review, 03/11/2026): the stair is removed now, but not with him standing on it - he went up for the piece, so a
		// 1 m drop on the next frame would count as a fall (two of them skip the piece). 1,5 m around the stair = still on it.
		if (usedStairs && !NearScaffold(state, ai.transform.position, 1.5f))
		{
			ClearScaffolds(ai, state);
		}
		// E6/E7: next piece of the plan (the timeout is per piece, so a long plan is not a slow piece). The same block
		// serves a template and a blueprint - both have state.Plan
		return Advance(ai, state);
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
		// E7 (player's test, 03/11/2026): a BLUEPRINT asks for the rest of the plan too - it was ordered "2 wood" (the
		// piece's cost) while 1927 were missing, so the gather stopped at nothing and the watcher had to wait for the bag
		if (state.Template != null || (state.Blueprint != null && state.Plan != null))
		{
			Humanoid humanoid = ai.GetComponent<Humanoid>();
			Inventory bag = humanoid != null ? humanoid.GetInventory() : null;
			bool useChests = ModConfig.BuildUseChests == null || ModConfig.BuildUseChests.Value;
			// E6b (player's request, 02/11/2026): take what the chests hold BEFORE going out - the wood in a chest is
			// wood the house can use, and the player must not watch it chop while a chest still holds 30
			Vector3 stock = state.Plan != null && state.AnchorKnown ? state.Anchor : state.Pos;
			int moved = state.Template != null
				? BuildStock.WithdrawTemplate(bag, state.Template, stock, cs, useChests)
				: 0;
			if (moved > 0)
			{
				Log(ai, "withdrew " + moved + " item(s) from the chests before gathering");
			}
			// only what is LEFT of the plan (from the piece that is short): the whole template asked for 122 when 75 were missing
			Dictionary<string, int> total = state.Plan != null
				? PlanMissing(bag, state.Plan, state.Index, stock, cs, useChests)
				: TemplateMissing(bag, state.Template, stock, cs, useChests);
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
		// E6b (player's test, 02/11/2026): Follow goes OFF for the gather. Wood/Stone/Ore/Forage keep Follow on upstream
		// ("targets of opportunity while following you" - MHR_JobRules.KeepsFollow), which leashes the harvest to
		// [Gathering] FollowRange around the PLAYER. Around a new house every tree is protected (10 m of any piece) or
		// already gone, so the order handed over and the companion just followed the player, with nothing to chop, until
		// the 300 s timeout. With Follow off the gather works the AREA of the spot ([Gathering] Range) - what a build
		// order needs - and Follow comes back when the build resumes (RestoreFollow).
		bool follow = cs != null && cs.GetFollow();
		if (follow)
		{
			cs.SetFollow(false);
			Log(ai, "follow off while it gathers for the build (it works the area around the spot, [Gathering] Range)");
		}
		CompanionSetup.ApplyActionMode(cs, state.GatherMode, false);
		if (follow && cs != null)
		{
			// the queued Follow write may land after the mode one: make sure the old follow target is gone now
			cs.ApplyFollowMode(state.GatherMode, true);
		}
		CompanionSetup.SetOrderCap(cs, state.GatherMode, state.GatherAmount, state.GatherItem);
		s_pending[ai.GetInstanceID()] = new Pending
		{
			Prefab = state.Prefab,
			Template = state.Template != null ? state.Template.Name : null,
			Blueprint = state.Blueprint,
			BlueprintFile = state.BlueprintFile,
			Hint = state.Hint,
			Mode = state.GatherMode,
			Item = state.GatherItem,
			Amount = state.GatherAmount,
			Index = state.Index,
			Plan = state.Plan,
			Retry = state.Retry,
			Passes = state.Passes,
			Anchor = state.AnchorKnown ? state.Anchor : state.Hint,
			AnchorRot = state.AnchorRot,
			FollowWas = follow,
			Start = Time.time,
			LastProgressAt = Time.time
		};
		Log(ai, "gathering " + state.GatherAmount + "x " + state.GatherItem + " (mode " + state.GatherMode + ") - the build resumes after it");
	}

	/// <summary>E6b: undoes the Follow that <see cref="HandOver"/> turned off. Only when it is still off (you may have
	/// turned it back on yourself) and only when this pending was the one that turned it off.</summary>
	private static void RestoreFollow(CompanionAI ai, CompanionSetup cs, Pending pending)
	{
		if (pending == null || !pending.FollowWas || cs == null)
		{
			return;
		}
		pending.FollowWas = false;
		if (!cs.GetFollow())
		{
			cs.SetFollow(true);
			Log(ai, "follow back on");
		}
	}

	/// <summary>E6b: ends a pending build with the line that says what is still missing (the order must never end in
	/// silence) - shared by the 300 s timeout and by "the gather found nothing it can work". For a plan the number is the
	/// REST of the plan (from the piece that is short), never the whole template.</summary>
	private static void EndPendingMissing(CompanionAI ai, CompanionSetup cs, Pending pending, Inventory bag,
		BuildTemplates.Template template, Piece piece, bool useChests)
	{
		if (pending == null)
		{
			return;
		}
		s_pending.Remove(ai.GetInstanceID());
		RestoreFollow(ai, cs, pending);
		CompanionSetup.ClearOrderCap(cs);
		// the order is over: back to Follow (mode 0). Without this the companion stayed in WOOD mode with no target and
		// kept working logs on the way home - the player's "it never stopped gathering" (02/11/2026).
		CompanionSetup.ApplyActionMode(cs, CompanionSetup.ModeFollow, false);
		Dictionary<string, int> still;
		if (template != null && pending.Index > 0)
		{
			List<PlannedStep> plan = new List<PlannedStep>();
			foreach (BuildTemplates.Step step in template.Ordered())
			{
				Piece stepPiece = BuildProbe.PieceOf(step.Prefab);
				if (stepPiece != null)
				{
					plan.Add(new PlannedStep { Prefab = step.Prefab, Piece = stepPiece });
				}
			}
			still = PlanMissing(bag, plan, pending.Index, pending.Anchor, cs, useChests);
		}
		else if (template != null)
		{
			still = TemplateMissing(bag, template, pending.Anchor, cs, useChests);
		}
		else if (pending.Plan != null && pending.Plan.Count > 0)
		{
			// a blueprint (or a plan without its template): the REST of the plan from the piece that is short
			still = PlanMissing(bag, pending.Plan, pending.Index, pending.Hint, cs, useChests);
		}
		else
		{
			still = BuildStock.Missing(bag, piece, pending.Anchor, cs, useChests);
		}
		string label = template != null
			? template.Label
			: (!string.IsNullOrEmpty(pending.Blueprint)
				? pending.Blueprint
				: (piece != null && Localization.instance != null ? Localization.instance.Localize(piece.m_name) : pending.Prefab));
		Say(ai, "hc_ai_build_missing", MissingText(still), label);
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
		return PlanMissing(bag, plan, 0, center, who, useChests);
	}

	/// <summary>
	/// E6b (player's test, 02/11/2026): the shortfall of the plan <b>from step <paramref name="startIndex"/> on</b> - the
	/// whole template counted what was already built and asked for 122 wood when 75 were missing.
	/// </summary>
	private static Dictionary<string, int> PlanMissing(Inventory bag, List<PlannedStep> plan, int startIndex, Vector3 center, Component who, bool useChests)
	{
		Dictionary<string, int> missing = new Dictionary<string, int>();
		if (bag == null || plan == null || plan.Count == 0)
		{
			return missing;
		}
		Dictionary<string, int> have = BuildStock.Available(bag, plan[0].Piece, center, who, useChests);
		Dictionary<string, int> need = new Dictionary<string, int>();
		for (int index = Mathf.Clamp(startIndex, 0, plan.Count - 1); index < plan.Count; index++)
		{
			PlannedStep step = plan[index];
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
		int protectedTrees = 0;
		if (wood)
		{
			foreach (TreeBase tree in UnityEngine.Object.FindObjectsByType<TreeBase>(FindObjectsSortMode.None))
			{
				if (tree == null)
				{
					continue;
				}
				// E6b (player's test, 02/11/2026): the gather refuses trees next to player-placed pieces (the base guard,
				// 10 m) and handing over to one of those left the order sitting in Follow. The nearest tree has to be one
				// the harvest can actually take.
				if (MHR_GatherRules.GatherRefusesTree(tree.gameObject))
				{
					protectedTrees++;
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
		if (protectedTrees > 0)
		{
			// E6b: says why the companion walks past the closest tree (it is inside the base guard radius)
			CompanionsPlugin.Log?.LogInfo("[Build] " + protectedTrees + " tree(s) skipped: too close to the buildings (base guard)");
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
		// E7 (test of 03/11/2026: "he died and did not go on with the build"): the body that comes back may be a NEW object and
		// the permanent id is only resolved later in TryInit, so the order is handed over HERE too (the dictionary holds one or
		// two entries) - the init hook alone was not enough.
		CompanionSetup owner = ai.GetComponent<CompanionSetup>();
		if (owner != null)
		{
			Reattach(owner);
		}
		Pending pending;
		if (!s_pending.TryGetValue(ai.GetInstanceID(), out pending) || pending == null)
		{
			return;
		}
		CompanionSetup cs = ai.GetComponent<CompanionSetup>();
		Humanoid humanoid = ai.GetComponent<Humanoid>();
		Inventory bag = humanoid != null ? humanoid.GetInventory() : null;
		ZNetView nv = ai.GetComponent<ZNetView>();
		if (cs == null || bag == null || nv == null || !nv.IsValid() || !nv.IsOwner() || humanoid == null
			|| (humanoid.IsDead() && !pending.AfterDeath))
		{
			s_pending.Remove(ai.GetInstanceID());
			RestoreFollow(ai, cs, pending);
			return;
		}
		if (pending.AfterDeath)
		{
			if (humanoid.IsDead())
			{
				return;      // waiting for the respawn - nothing else to do
			}
			// alive again: resume the SAME order (the plan travels with the pending)
			s_pending.Remove(ai.GetInstanceID());
			bool back;
			if (pending.BlueprintFile != null)
			{
				Blueprint saved = BuildBlueprints.Load(pending.BlueprintFile);
				string ignoredPrefab;
				string ignoredStation;
				back = saved != null
					&& ResumeBlueprint(cs, saved, pending.Hint, pending.Index, pending.AnchorRot, out ignoredPrefab,
						out ignoredStation, pending.Plan) == BlueprintStart.Started;
			}
			else
			{
				BuildTemplates.Template savedTemplate = pending.Template != null ? BuildTemplates.ByName(pending.Template) : null;
				back = savedTemplate != null
					&& StartPlan(cs, savedTemplate, pending.Hint, pending.Index, pending.Anchor, pending.AnchorRot, true);
			}
			if (back)
			{
				State fresh;
				if (s_state.TryGetValue(ai.GetInstanceID(), out fresh) && fresh != null)
				{
					fresh.Retry = pending.Retry;
					fresh.Passes = pending.Passes;
				}
				Log(ai, "back from the dead - resuming the build at piece " + (pending.Index + 1));
			}
			else
			{
				Log(ai, "could not resume the build after the respawn - the order ends");
			}
			return;
		}
		// E6b: a plan waiting for its materials (a template, or a blueprint) or a single piece
		BuildTemplates.Template template = pending.Template != null ? BuildTemplates.ByName(pending.Template) : null;
		Blueprint blueprint = pending.BlueprintFile != null ? BuildBlueprints.Load(pending.BlueprintFile) : null;
		Piece piece = pending.Template == null && pending.BlueprintFile == null ? BuildProbe.PieceOf(pending.Prefab) : null;
		bool broken = pending.Template != null ? template == null
			: (pending.BlueprintFile != null ? blueprint == null : piece == null);
		if (broken)
		{
			s_pending.Remove(ai.GetInstanceID());
			RestoreFollow(ai, cs, pending);
			return;
		}
		if (!pending.AfterDeath && ModeOf(cs) != pending.Mode)
		{
			s_pending.Remove(ai.GetInstanceID());
			RestoreFollow(ai, cs, pending);
			Log(ai, "the work changed - the build order was dropped");
			return;
		}
		bool useChests = ModConfig.BuildUseChests == null || ModConfig.BuildUseChests.Value;
		// E6b (player's test, 02/11/2026): the clock counts from the last wood that ARRIVED, not from the handover. The
		// fixed 300 s cut a gather that was at 36/39 and the build was dropped with the last three pieces missing.
		if (Time.time - pending.LastProgressAt > GatherTimeout)
		{
			Log(ai, "gathering made no progress for " + GatherTimeout.ToString("F0") + " s - saying what is missing");
			EndPendingMissing(ai, cs, pending, bag, template, piece, useChests);
			return;
		}
		// E6b (player's test, 02/11/2026): the watcher RECOMPUTES what is still missing with the same function the build
		// uses (the plan from the piece that was short, or the piece's own cost) and resumes when nothing is missing.
		// Comparing the bag TOTAL with the shortfall looped the 4x4 forever: the bag already had the 2 it asked for and
		// the door's 4 were never covered. Once a second is enough for this.
		if (Time.time < pending.NextCheck)
		{
			return;
		}
		pending.NextCheck = Time.time + 1f;
		Dictionary<string, int> still = pending.Plan != null && pending.Plan.Count > 0
			? PlanMissing(bag, pending.Plan, pending.Index, pending.Anchor, cs, useChests)
			: BuildStock.Missing(bag, piece, pending.Anchor, cs, useChests);
		int got = 0;
		foreach (KeyValuePair<string, int> shortfall in still)
		{
			got += shortfall.Value;
		}
		// E6b (player's request, 02/11/2026): the plan asked for the WHOLE structure's shortfall, which can be more than
		// the bag holds. When the bag is nearly full the build resumes with what it has - the rest is gathered on the
		// next shortfall instead of standing in the forest waiting for a number that never fits.
		bool nearlyFull = humanoid.GetInventory().GetTotalWeight() >= CompanionCarry.Max(humanoid) * 0.8f;
		if (got > 0 && !nearlyFull)
		{
			// E6b diagnostics (the player's test showed the order sitting in the gather without a word): one line per
			// change of the shortfall, so the log says WHY the build is still waiting
			string shortText = MissingText(still);
			if (pending.LastLoggedText != shortText)
			{
				pending.LastLoggedText = shortText;
				pending.LastProgressAt = Time.time;
				Log(ai, "waiting for materials: " + shortText
					+ (string.IsNullOrEmpty(pending.Template) && string.IsNullOrEmpty(pending.Blueprint) ? string.Empty
						: " for the " + (pending.Template ?? pending.Blueprint))
					+ " (mode " + ModeOf(cs) + ", expected " + pending.Mode + ")");
			}
			// E6b (player's test, 02/11/2026): the gather can end up with NOTHING it may work - every tree around the spot
			// is protected by the base guard or already gone, the player carried the wood away. Before, the order waited
			// the whole 300 s in silence while the companion followed the player; now 20 s of an idle gather (not in
			// combat, not resting) ends it saying what is missing.
			HarvestController harvest = ai.GetComponent<HarvestController>();
			bool working = (harvest != null && harvest.IsActive) || ai.IsInCombat || CompanionRest.MHR_ShouldPauseWork(ai);
			if (working || Time.time - pending.Start < 5f)
			{
				pending.IdleSince = 0f;
				return;   // still gathering
			}
			if (pending.IdleSince <= 0f)
			{
				pending.IdleSince = Time.time;
				if (Time.time - pending.LastIdleLog > 60f)
				{
					pending.LastIdleLog = Time.time;
					Log(ai, "the gather has nothing to work - watching it for 20 s");
				}
				return;
			}
			if (Time.time - pending.IdleSince < 20f)
			{
				return;
			}
			Log(ai, "the gather found nothing it can work - saying what is missing");
			EndPendingMissing(ai, cs, pending, bag, template, piece, useChests);
			return;   // still gathering
		}
		Log(ai, "gather done (" + (nearlyFull ? "bag nearly full" : "nothing missing") + ") - resuming");
		s_pending.Remove(ai.GetInstanceID());
		RestoreFollow(ai, cs, pending);
		CompanionSetup.ClearOrderCap(cs);
		// back to Follow mode BEFORE the build starts: the harvest must not keep steering while the build does
		CompanionSetup.ApplyActionMode(cs, CompanionSetup.ModeFollow, false);
		bool resumed;
		if (template != null)
		{
			resumed = StartPlan(cs, template, pending.Hint, pending.Index, pending.Anchor, pending.AnchorRot, true);
		}
		else if (blueprint != null)
		{
			string ignoredPrefab;
			string ignoredStation;
			resumed = ResumeBlueprint(cs, blueprint, pending.Hint, pending.Index, pending.AnchorRot, out ignoredPrefab,
				out ignoredStation, pending.Plan) == BlueprintStart.Started;
		}
		else
		{
			resumed = Start(cs, pending.Prefab, pending.Hint);
		}
		if (resumed)
		{
			// E7 (player's test, 03/11/2026): the pieces that did not stand yet travelled with the pending order (they are
			// the upper storeys, waiting for what is below them). A resumed order starts with an empty retry queue, so
			// without this the 2nd floor of the house was dropped on every gather.
			State fresh;
			if (pending.Retry != null && pending.Retry.Count > 0 && s_state.TryGetValue(ai.GetInstanceID(), out fresh) && fresh != null)
			{
				fresh.Retry = pending.Retry;
				fresh.Passes = pending.Passes;
				Log(ai, "carrying " + pending.Retry.Count + " piece(s) that did not stand yet to the resumed order");
			}
			Log(ai, "materials ready - resuming the build");
		}
		else
		{
			string label = pending.Template ?? pending.Blueprint ?? pending.Prefab;
			Log(ai, "could not resume the " + label + " - the order ends");
			Say(ai, "hc_ai_build_failed", label);
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
		ClearScaffolds(ai, state);
		state.Active = false;
		s_state.Remove(ai.GetInstanceID());
		MHR_Mover.Release(ai);
		ReleaseHarvest(ai, state);
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
		// E7 (review B1, 03/11/2026): [Advanced] LogLevel is the switch - a 985-piece plan writes thousands of lines
		if (!CompanionsPlugin.LogNormal)
		{
			return;
		}
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
